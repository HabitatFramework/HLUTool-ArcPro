// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013 Thames Valley Environmental Records Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
//
// This file is part of HLUTool.
//
// HLUTool is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HLUTool is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with HLUTool.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using HLU.Data;
using HLU.Data.Model;
using System.Threading.Tasks;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for the main window updates handling.
    /// </summary>
    class ViewModelWindowMainUpdate
    {
        #region Fields

        ViewModelWindowMain _viewModelMain;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelWindowMainUpdate"/> class.
        /// </summary>
        /// <param name="viewModelMain">The view model main.</param>
        public ViewModelWindowMainUpdate(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Writes changes made to current incid back to database and GIS layer.
        /// Also synchronizes shadow copy of GIS layer in DB and writes history.
        /// </summary>
        internal async Task<bool> UpdateAsync()
        {
            // Start database transaction
            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            // Start a GIS edit operation
            EditOperation editOperation = new()
            {
                Name = "Update GIS Features"
            };

            try
            {
                _viewModelMain.ChangeCursor(Cursors.Wait, "Saving ...");

                // Store row index for reloading the row after the update
                int incidCurrRowIx = _viewModelMain.IncidCurrentRowIndex;

                // Previously only changes to fields on the incid table triggered the
                // last modified date & user fields to be updated.
                // Update the incid table regardless of which attributes have changed.
                IncidCurrentRowDerivedValuesUpdate(_viewModelMain);

                // Update the DateTime fields to whole seconds.
                // Fractions of a second can cause rounding differences when
                // comparing DateTime fields later in some databases.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);
                _viewModelMain.IncidCurrentRow.last_modified_date = nowDtTm;
                _viewModelMain.IncidCurrentRow.last_modified_user_id = _viewModelMain.UserID;

                // Update the incid row
                if (_viewModelMain.HluTableAdapterManager.incidTableAdapter.Update(
                    (HluDataSet.incidDataTable)_viewModelMain.HluDataset.incid.GetChanges()) == -1)
                    throw new Exception(String.Format("Failed to update '{0}' table.",
                        _viewModelMain.HluDataset.incid.TableName));

                // Update IHS tables
                UpdateIHSTables();

                // Update condition rows
                if ((_viewModelMain.IncidConditionRows != null) && _viewModelMain.IsDirtyIncidCondition())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_conditionTableAdapter.Update(
                        (HluDataSet.incid_conditionDataTable)_viewModelMain.HluDataset.incid_condition.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_condition.TableName));
                }

                // Update the secondary rows
                if (_viewModelMain.IsDirtyIncidSecondary()) UpdateSecondary();

                // Update the BAP rows
                if (_viewModelMain.IsDirtyIncidBap()) UpdateBap();

                //TODO: Check if the source rows are dirty?
                // Update the source rows
                if (_viewModelMain.IncidSourcesRows != null)
                {
                    int j = 0;
                    for (int i = 0; i < _viewModelMain.IncidSourcesRows.Length; i++)
                        if (_viewModelMain.IncidSourcesRows[i] != null)
                            _viewModelMain.IncidSourcesRows[i].sort_order = ++j;

                    if (_viewModelMain.HluTableAdapterManager.incid_sourcesTableAdapter.Update(
                        (HluDataSet.incid_sourcesDataTable)_viewModelMain.IncidSourcesTable.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update {0} table.",
                            _viewModelMain.HluDataset.incid_sources.TableName));
                }

                // If there are OSMM update rows for this incid, and
                // if the OSMM update status is to be reset after manual
                // updates, and if the OSMM update status > 0 (proposed)
                // or status = 0 (pending) ...
                if ((_viewModelMain.IncidOSMMUpdatesRows.Length > 0) &&
                   (_viewModelMain.ResetOSMMUpdatesStatus) &&
                   (_viewModelMain.IncidOSMMUpdatesRows[0].status >= 0))
                {
                    // Set the update flag to "Ignored"
                    _viewModelMain.IncidOSMMUpdatesRows[0].status = -2;
                    _viewModelMain.IncidOSMMUpdatesRows[0].last_modified_date = nowDtTm;
                    _viewModelMain.IncidOSMMUpdatesRows[0].last_modified_user_id = _viewModelMain.UserID;
                }

                // Update the OSMM Update rows
                if ((_viewModelMain.IncidOSMMUpdatesRows != null) && _viewModelMain.IsDirtyIncidOSMMUpdates())
                {
                    if (_viewModelMain.HluTableAdapterManager.incid_osmm_updatesTableAdapter.Update(
                        (HluDataSet.incid_osmm_updatesDataTable)_viewModelMain.HluDataset.incid_osmm_updates.GetChanges()) == -1)
                        throw new Exception(String.Format("Failed to update '{0}' table.",
                            _viewModelMain.HluDataset.incid_osmm_updates.TableName));
                }

                // ---------------------------------------------------------------------
                // Update all of the GIS rows corresponding to this incid
                // ---------------------------------------------------------------------

                // Set the SQL condition for the update
                List<SqlFilterCondition> incidCond = new([
                    new SqlFilterCondition(_viewModelMain.HluDataset.incid_mm_polygons,
                        _viewModelMain.HluDataset.incid_mm_polygons.incidColumn, _viewModelMain.Incid) ]);

                // Get the current values from the GIS layer
                DataTable historyTable = await _viewModelMain.GISApplication.GetHistoryAsync(
                     _viewModelMain.HistoryColumns, incidCond);

                // Check if a history table was returned
                if (historyTable == null)
                    throw new Exception("Error updating GIS layer.");
                else if (historyTable.Rows.Count == 0)
                    throw new Exception("No GIS features were updated.");

                //TODO: GIS layer shadow copy update - Set length and area for each polygon (if possible)?
                // Perform database shadow copy update
                if (!UpdateGISShadowCopy(incidCond))
                {
                    throw new Exception("Failed to update database copy of GIS layer.");
                }

                // Build a list of columns and values to update
                DataColumn[] updateColumns = [
                    _viewModelMain.HluDataset.incid_mm_polygons.habprimaryColumn,
                    _viewModelMain.HluDataset.incid_mm_polygons.habsecondColumn,
                    _viewModelMain.HluDataset.incid_mm_polygons.determqtyColumn,
                    _viewModelMain.HluDataset.incid_mm_polygons.interpqtyColumn ];

                object[] updateValues = [ _viewModelMain.IncidPrimary ?? "",
                        _viewModelMain.IncidSecondarySummary ?? "",
                        _viewModelMain.IncidQualityDetermination ?? "",
                        _viewModelMain.IncidQualityInterpretation ?? ""];

                // Queue updates to the GIS layer
                await _viewModelMain.GISApplication.UpdateFeaturesAsync(updateColumns, updateValues,
                     _viewModelMain.HistoryColumns, incidCond, editOperation);

                // Save the history returned from GIS
                Dictionary<int, string> fixedValues = new()
                {
                    { _viewModelMain.HluDataset.history.incidColumn.Ordinal, _viewModelMain.Incid }
                };
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);
                vmHist.HistoryWrite(fixedValues, historyTable, Operations.AttributeUpdate, nowDtTm);

                // Commit updates to the GIS layer
                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // Commit GIS EditOperation
                        if (!await editOperation.ExecuteAsync())
                        {
                            throw new Exception("Failed to update GIS layer.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error updating GIS features: " + ex.Message, ex);
                    }
                });

                // Commit the transation and accept the changes
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();
                _viewModelMain.Saved = true;

                // Recount the incid rows in the database
                _viewModelMain.IncidRowCount(true);

                // Reload the current row index
                _viewModelMain.IncidCurrentRowIndex = incidCurrRowIx;

                return true;
            }
            catch (Exception ex)
            {
                // Rollback the database updates
                _viewModelMain.DataBase.RollbackTransaction();

                try
                {
                    // Abort the GIS updates
                    editOperation.Abort();
                }
                catch (Exception ex2)
                {
                    MessageBox.Show("Error" + ex2.Message);
                }

                // Flag the updates weren't saved
                _viewModelMain.Saved = false;

                MessageBox.Show(string.Format("Your changes could not be saved. The error message returned was:\n\n{0}",
                    ex.Message), "HLU: Save Error", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;
            }
            finally
            {
                _viewModelMain.SavingAttempted = true;
                _viewModelMain.Saving = false;
                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }
        }

        //TODO: Needed?
        /// <summary>
        /// Update the related IHS tables.
        /// </summary>
        private void UpdateIHSTables()
        {
            UpdateTableIfDirty(_viewModelMain.IncidIhsMatrixRows, _viewModelMain.IsDirtyIncidIhsMatrix,
                _viewModelMain.HluTableAdapterManager.incid_ihs_matrixTableAdapter, _viewModelMain.HluDataset.incid_ihs_matrix);

            UpdateTableIfDirty(_viewModelMain.IncidIhsFormationRows, _viewModelMain.IsDirtyIncidIhsFormation,
                _viewModelMain.HluTableAdapterManager.incid_ihs_formationTableAdapter, _viewModelMain.HluDataset.incid_ihs_formation);

            UpdateTableIfDirty(_viewModelMain.IncidIhsManagementRows, _viewModelMain.IsDirtyIncidIhsManagement,
                _viewModelMain.HluTableAdapterManager.incid_ihs_managementTableAdapter, _viewModelMain.HluDataset.incid_ihs_management);

            UpdateTableIfDirty(_viewModelMain.IncidIhsComplexRows, _viewModelMain.IsDirtyIncidIhsComplex,
                _viewModelMain.HluTableAdapterManager.incid_ihs_complexTableAdapter, _viewModelMain.HluDataset.incid_ihs_complex);
        }

        /// <summary>
        /// Updates the specified table if there are pending changes.
        /// </summary>
        /// <remarks>This method checks if the table has unsaved changes by invoking the <paramref
        /// name="isDirty"/> function. If changes are detected, it attempts to update the table using the provided
        /// <paramref name="tableAdapter"/> and the modified rows from <paramref name="datasetTable"/>. If the update
        /// operation fails, an exception is thrown.</remarks>
        /// <param name="dataRows">The data rows to check for changes. This parameter must not be <see langword="null"/> if updates are
        /// required.</param>
        /// <param name="isDirty">A function that determines whether the table has unsaved changes. The function should return <see
        /// langword="true"/> if changes exist; otherwise, <see langword="false"/>.</param>
        /// <param name="tableAdapter">The table adapter used to perform the update operation. This must support the <c>Update</c> method for
        /// applying changes.</param>
        /// <param name="datasetTable">The dataset table containing the changes to be updated. This must support the <c>GetChanges</c> method to
        /// retrieve modified rows.</param>
        /// <exception cref="Exception">Thrown if the update operation fails, indicating that the table could not be updated successfully.</exception>
        private void UpdateTableIfDirty(object dataRows, Func<bool> isDirty, dynamic tableAdapter, dynamic datasetTable)
        {
            if (dataRows != null && isDirty())
            {
                if (tableAdapter.Update((DataTable)datasetTable.GetChanges()) == -1)
                {
                    throw new Exception(string.Format("Failed to update '{0}' table.", datasetTable.TableName));
                }
            }
        }

        /// <summary>
        /// Updates the GIS shadow copy (of the incid_mm_polygons table).
        /// </summary>
        /// <param name="incidCond"></param>
        /// <returns></returns>
        private bool UpdateGISShadowCopy(List<SqlFilterCondition> incidCond)
        {
            string updateStatement = string.Format(
                "UPDATE {0} SET {1}={2}, {3}={4}, {5}={6}, {7}={8} WHERE {9}",
                _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.habprimaryColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidPrimary),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.habsecondColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidSecondarySummary),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.determqtyColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidQualityDetermination),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.interpqtyColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidQualityInterpretation),
                _viewModelMain.DataBase.WhereClause(false, true, true, incidCond));

            return _viewModelMain.DataBase.ExecuteNonQuery(updateStatement,
                _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) != -1;
        }

        /// <summary>
        /// Updates BAP environment rows corresponding to current incid.
        /// </summary>
        private void UpdateBap()
        {
            if (_viewModelMain.IncidBapRowsAuto == null)
                _viewModelMain.IncidBapRowsAuto = [];
            if (_viewModelMain.IncidBapHabitatsUser == null)
                _viewModelMain.IncidBapHabitatsUser = [];

            // remove duplicate codes
            IEnumerable<BapEnvironment> beAuto = from b in _viewModelMain.IncidBapRowsAuto
                                                 group b by b.bap_habitat into habs
                                                 select habs.First();

            IEnumerable<BapEnvironment> beUser = from b in _viewModelMain.IncidBapHabitatsUser
                                                 where !beAuto.Any(a => a.bap_habitat == b.bap_habitat)
                                                 group b by b.bap_habitat into habs
                                                 select habs.First();

            var currentBapRows = beAuto.Concat(beUser);

            List<HluDataSet.incid_bapRow> newRows = [];
            List<HluDataSet.incid_bapRow> updateRows = [];
            HluDataSet.incid_bapRow updateRow;

            foreach (BapEnvironment be in currentBapRows)
            {
                if (be.bap_id == -1) // new BAP environment
                {
                    be.bap_id = _viewModelMain.RecIDs.NextIncidBapId;
                    be.incid = _viewModelMain.Incid;
                    HluDataSet.incid_bapRow newRow = _viewModelMain.IncidBapTable.Newincid_bapRow();
                    newRow.ItemArray = be.ToItemArray();
                    newRows.Add(newRow);
                }
                // Get the new values for every updated bap row from the bap data grid.
                else if ((updateRow = UpdateIncidBapRow(be)) != null)
                {
                    // If a row is returned from the data grid add it to the list
                    // of updated rows.
                    updateRows.Add(updateRow);
                }
            }

            // Delete any rows that haven't been marked as deleted but are
            // no longer in the current rows.
            _viewModelMain.IncidBapRows.Where(r => r.RowState != DataRowState.Deleted &&
                !currentBapRows.Any(g => g.bap_id == r.bap_id)).ToList()
                .ForEach(delegate(HluDataSet.incid_bapRow row) { row.Delete(); });

            // Update the table to remove the deleted rows.
            if (_viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Update(
                _viewModelMain.IncidBapRows.Where(r => r.RowState == DataRowState.Deleted).ToArray()) == -1)
                throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_bap.TableName));

            // If there are any rows that have been updated.
            if (updateRows.Count > 0)
            {
                // Update the table to update the updated rows.
                if (_viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Update(updateRows.ToArray()) == -1)
                    throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_bap.TableName));
            }

            // Insert the new rows into the table.
            foreach (HluDataSet.incid_bapRow r in newRows)
                _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Insert(r);

        }

        /// <summary>
        /// Updates secondary habitat rows corresponding to current incid.
        /// </summary>
        private void UpdateSecondary()
        {
            if (_viewModelMain.IncidSecondaryHabitats == null)
                _viewModelMain.IncidSecondaryHabitats = [];

            // remove duplicate codes
            IEnumerable<SecondaryHabitat> currSecondaryRows = from s in _viewModelMain.IncidSecondaryHabitats
                                                        group s by new { s.secondary_group, s.secondary_habitat } into secs
                                                        select secs.First();

            List<HluDataSet.incid_secondaryRow> newRows = [];
            List<HluDataSet.incid_secondaryRow> updateRows = [];
            HluDataSet.incid_secondaryRow updateRow;

            foreach (SecondaryHabitat sh in currSecondaryRows)
            {
                if (sh.secondary_id == -1) // new secondary habitat environment
                {
                    sh.secondary_id = _viewModelMain.RecIDs.NextIncidSecondaryId;
                    sh.incid = _viewModelMain.Incid;
                    HluDataSet.incid_secondaryRow newRow = _viewModelMain.IncidSecondaryTable.Newincid_secondaryRow();
                    newRow.ItemArray = sh.ToItemArray();
                    newRows.Add(newRow);
                }
                // Get the new values for every updated secondary habitat row from the
                // secondary habitat data grid.
                else if ((updateRow = UpdateIncidSecondaryRow(sh)) != null)
                {
                    // If a row is returned from the data grid add it to the list
                    // of updated rows.
                    updateRows.Add(updateRow);
                }
            }

            // Delete any rows that haven't been marked as deleted but are
            // no longer in the current rows.
            _viewModelMain.IncidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted &&
                !currSecondaryRows.Any(g => g.secondary_id == r.secondary_id)).ToList()
                .ForEach(delegate(HluDataSet.incid_secondaryRow row) { row.Delete(); });

            // Update the table to remove the deleted rows.
            if (_viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter.Update(
                _viewModelMain.IncidSecondaryRows.Where(r => r.RowState == DataRowState.Deleted).ToArray()) == -1)
                throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_secondary.TableName));

            // If there are any rows that have been updated.
            if (updateRows.Count > 0)
            {
                // Update the table to update the updated rows.
                if (_viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter.Update(updateRows.ToArray()) == -1)
                    throw new Exception(String.Format("Failed to update {0} table.", _viewModelMain.HluDataset.incid_secondary.TableName));
            }

            // Insert the new rows into the table.
            foreach (HluDataSet.incid_secondaryRow r in newRows)
                _viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter.Insert(r);

        }

        /// <summary>
        /// Writes the values from a BapEnvironment object bound to the BAP data grids into the corresponding incid_bap DataRow.
        /// </summary>
        /// <param name="be">BapEnvironment object bound to data grid on form.</param>
        /// <returns>Updated incid_bap row, or null if no corresponding row was found.</returns>
        private HluDataSet.incid_bapRow UpdateIncidBapRow(BapEnvironment be)
        {
            var q = _viewModelMain.IncidBapRows.Where(r => r.RowState != DataRowState.Deleted && r.bap_id == be.bap_id);
            if (q.Count() == 1)
            {
                if (!be.IsValid()) return null;
                HluDataSet.incid_bapRow oldRow = q.ElementAt(0);
                object[] itemArray = be.ToItemArray();
                for (int i = 0; i < itemArray.Length; i++)
                    oldRow[i] = itemArray[i];
                return oldRow;
            }
            return null;
        }

        /// <summary>
        /// Writes the values from a SecondaryHabitat object bound to the secondaries data grids
        /// into the corresponding incid_secondary DataRow.
        /// </summary>
        /// <param name="sh">SecondaryHabitat object bound to data grid on form.</param>
        /// <returns>Updated incid_secondary row, or null if no corresponding row was found.</returns>
        private HluDataSet.incid_secondaryRow UpdateIncidSecondaryRow(SecondaryHabitat sh)
        {
            var q = _viewModelMain.IncidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted && r.secondary_id == sh.secondary_id);
            if (q.Count() == 1)
            {
                if (!sh.IsValid()) return null;
                HluDataSet.incid_secondaryRow oldRow = q.ElementAt(0);
                object[] itemArray = sh.ToItemArray();
                for (int i = 0; i < itemArray.Length; i++)
                    oldRow[i] = itemArray[i];
                return oldRow;
            }
            return null;
        }

        /// <summary>
        /// Updates those columns of IncidCurrentRow in main view model that are not directly updated
        /// by properties (to enable undo if update cancelled).
        /// </summary>
        /// <param name="viewModelMain">Reference to main window view model.</param>
        internal static void IncidCurrentRowDerivedValuesUpdate(ViewModelWindowMain viewModelMain)
        {
            // Clear IHS values on update (if required) depending on user settings
            bool clearIHSCodes = false;
            switch (viewModelMain.ClearIHSUpdateAction)
            {
                case "Clear on change in primary code only":
                    // Check if the primary habitat has changed
                    if (viewModelMain.IncidCurrentRow.IsNull(viewModelMain.HluDataset.incid.habitat_primaryColumn)
                        || (String.IsNullOrEmpty(viewModelMain.IncidCurrentRow.habitat_primary)))
                    {
                        if (viewModelMain.IncidPrimary != null)
                            clearIHSCodes = true;
                    }
                    else if ((viewModelMain.IncidPrimary == null)
                        || (viewModelMain.IncidCurrentRow.habitat_primary != viewModelMain.IncidPrimary))
                    {
                        clearIHSCodes = true;
                    }
                    break;
                case "Clear on change in primary or secondary codes only":
                    // Check if the primary habitat has changed
                    if (viewModelMain.IncidCurrentRow.IsNull(viewModelMain.HluDataset.incid.habitat_primaryColumn)
                        || (String.IsNullOrEmpty(viewModelMain.IncidCurrentRow.habitat_primary)))
                    {
                        if (viewModelMain.IncidPrimary != null)
                            clearIHSCodes = true;
                    }
                    else if ((viewModelMain.IncidPrimary == null)
                        || (viewModelMain.IncidCurrentRow.habitat_primary != viewModelMain.IncidPrimary))
                    {
                        clearIHSCodes = true;
                    }
                    else
                    {
                        // Check if the secondary habitats have changed
                        if (viewModelMain.IncidCurrentRow.IsNull(viewModelMain.HluDataset.incid.habitat_secondariesColumn)
                            || (String.IsNullOrEmpty(viewModelMain.IncidCurrentRow.habitat_secondaries)))
                        {
                            if (viewModelMain.IncidSecondarySummary != null)
                                clearIHSCodes = true;
                        }
                        else if ((viewModelMain.IncidSecondarySummary == null)
                            || (viewModelMain.IncidCurrentRow.habitat_secondaries != viewModelMain.IncidSecondarySummary))
                        {
                            clearIHSCodes = true;
                        }
                    }
                    break;
                case "Clear on any change":
                    clearIHSCodes = true;
                    break;
                default:    // "Don't clear"
                    break;
            }

            // Clear the IHS codes if required
            if (clearIHSCodes)
            {
                viewModelMain.IncidCurrentRow.ihs_habitat = null;
                viewModelMain.RemoveIncidIhsMatrixRows();
                viewModelMain.RemoveIncidIhsFormationRows();
                viewModelMain.RemoveIncidIhsManagementRows();
                viewModelMain.RemoveIncidIhsComplexRows();
            }

            // Update other incid vales
            viewModelMain.IncidCurrentRow.habitat_primary = viewModelMain.IncidPrimary;
            viewModelMain.IncidCurrentRow.habitat_secondaries = viewModelMain.IncidSecondarySummary;
            viewModelMain.IncidCurrentRow.habitat_version = viewModelMain.HabitatVersion;
        }

        /// <summary>
        /// Updates the incid modified columns following a physical or
        /// logical split or merge.
        /// </summary>
        /// <param name="incid">The incid.</param>
        /// <param name="nowDtTm">The current date and time.</param>
        /// <exception cref="Exception">Failed to update incid table modified details.</exception>
        internal void UpdateIncidModifiedColumns(string incid, DateTime nowDtTm)
        {
            String updateStatement = String.Format("UPDATE {0} SET {1} = {2}, {3} = {4} WHERE {5} = {6}",
                _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.last_modified_dateColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(nowDtTm),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.last_modified_user_idColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(_viewModelMain.UserID),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(incid));

            if (_viewModelMain.DataBase.ExecuteNonQuery(updateStatement,
                _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) == -1)
                throw new Exception("Failed to update incid table modified details.");
        }
    }

    #endregion Methods
}