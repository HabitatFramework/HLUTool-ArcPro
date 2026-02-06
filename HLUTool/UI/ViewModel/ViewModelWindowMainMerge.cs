// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
// Copyright © 2019-2022 Greenspace Information for Greater London CIC
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
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Properties;
using HLU.UI.View;
using System.Windows.Threading;
using System.Threading.Tasks;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using CommandType = System.Data.CommandType;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains methods to perform logical and physical merges of selected GIS features.
    /// </summary>
    class ViewModelWindowMainMerge
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;
        private WindowMergeFeatures _mergeFeaturesWindow;
        private ViewModelWindowMergeFeatures<HluDataSet.incidDataTable, HluDataSet.incidRow> _mergeFeaturesViewModelLogical;
        private ViewModelWindowMergeFeatures<HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>
            _mergeFeaturesViewModelPhysical;
        private int _mergeResultFeatureIndex;

        #endregion Fields

        #region Constructor

        public ViewModelWindowMainMerge(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Logical Merge

        /// <summary>
        /// Attempts to perform a logical merge operation on the currently selected map features.
        /// </summary>
        /// <remarks>The logical merge can only be performed if more than one feature is selected on the
        /// map and all selected features exist in the database. If these conditions are not met, the method displays an
        /// informational message to the user and returns <see langword="false"/>.</remarks>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the logical
        /// merge was successful; otherwise, <see langword="false"/>.</returns>
        internal async Task<bool> LogicalMergeAsync()
        {
            // Check one or more features are selected.
            if ((_viewModelMain.GisSelection == null) || (_viewModelMain.GisSelection.Rows.Count == 0))
            {
                MessageBox.Show("Cannot logically merge: Nothing is selected on the map.", "HLU: Logical Merge",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Check more than one feature is selected.
            if (_viewModelMain.GisSelection.Rows.Count <= 1)
            {
                MessageBox.Show("Cannot logically merge: Map selection must contain more than one feature for a merge.",
                    "HLU: Logical Merge", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Check all selected features are in the database.
            if (!_viewModelMain.CheckSelectedToidFrags(false))
            {
                MessageBox.Show("Cannot logically merge: One or more selected map features missing from database.",
                    "HLU: Logical Merge", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Double check the selection count (this shouldn't be true).
            if (_viewModelMain.SelectedIncidsInGISCount <= 0)
                return false;

            // Perform the logical merge.
            if (!await PerformLogicalMergeAsync())
                return false;

            //// If the selected features share the same incid and toid.
            //if ((_viewModelMain.IncidsSelectedMapCount == 1) && (_viewModelMain.ToidsSelectedMapCount == 1))
            //{
            //    // Prompt the user to perform a physical merge as well.
            //    if (MessageBox.Show("Perform physical merge as well?", "HLU: Physical Merge",
            //    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            //    {
            //        //TODO: Needed?
            //        //// Restore the selection.
            //        //_viewModelMain.GisSelection.Clear();
            //        //foreach (HluDataSet.incid_mm_polygonsRow r in polygons)
            //        //{
            //        //    DataRow newRow = _viewModelMain.GisSelection.NewRow();
            //        //    for (int i = 0; i < _viewModelMain.GisIDColumnOrdinals.Length; i++)
            //        //        newRow[i] = r[_viewModelMain.GisIDColumnOrdinals[i]];
            //        //    _viewModelMain.GisSelection.Rows.Add(newRow);
            //        //}

            //        // Perform the physical merge.
            //        return await PerformPhysicalMergeAsync();
            //    }
            //}

            return true;
        }

        /// <summary>
        /// Performs a logical merge operation on selected features, optionally prompting the user to perform a physical
        /// merge as well.
        /// </summary>
        /// <remarks>This method performs a logical merge of selected features by consolidating their data
        /// into a single feature. The user is prompted to select which feature to keep, and the remaining features are
        /// updated accordingly. If <paramref name="physicallyMerge"/> is <see langword="true"/>, the user is given the
        /// option to perform a physical merge after the logical merge completes.  The method ensures that database
        /// transactions are used to maintain data integrity during the merge process. If an error occurs, the
        /// transaction is rolled back, and the operation is aborted.  Preconditions: - The number of selected features
        /// must be greater than zero; otherwise, the method returns <see langword="false"/>.  Postconditions: - The
        /// selected features are logically merged, with the data consolidated into the chosen feature. - If the user
        /// opts for a physical merge, additional operations are performed to merge the features physically. - The
        /// database and application state are updated to reflect the changes.  Exceptions: - If an error occurs during
        /// the merge process, an error message is displayed to the user, and the operation is aborted.</remarks>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the merge
        /// operation succeeds; otherwise, <see langword="false"/>.</returns>
        private async Task<bool> PerformLogicalMergeAsync()
        {
            // Get the incid rows to select from.
            HluDataSet.incidDataTable incidTable = new();
            _viewModelMain.HluTableAdapterManager.incidTableAdapter.Fill(incidTable,
                ViewModelWindowMainHelpers.IncidSelectionToWhereClause(ViewModelWindowMain.IncidPageSize,
                _viewModelMain.IncidTable.incidColumn.Ordinal, _viewModelMain.IncidTable, _viewModelMain.IncidsSelectedMap));

            // Check there are incids to select from.
            if (incidTable.Count == 0)
                return false;

            // Get the DB copy rows to update.
            HluDataSet.incid_mm_polygonsDataTable selectTable = new();
            _viewModelMain.GetIncidMMPolygonRows(ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                _viewModelMain.GisSelection.Select(), _viewModelMain.GisIDColumnOrdinals,
                ViewModelWindowMain.IncidPageSize, selectTable), ref selectTable);

            // Check there are DB copy rows to update.
            if (selectTable.Count == 0)
                return false;

            // Create the merge features window.
            _mergeFeaturesWindow = new WindowMergeFeatures
            {
                // Set ArcGIS Pro as the parent.
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Prompt the user to choose which incid to keep.
            _mergeFeaturesViewModelLogical = new(incidTable, _viewModelMain.GisIDColumnOrdinals,
                _viewModelMain.IncidTable.incidColumn.Ordinal, selectTable.Select(r => r).ToArray(),
                _viewModelMain.GISApplication)
            {
                DisplayName = "Select INCID To Keep"
            };

            // Handle the RequestClose event to get the selected feature index.
            _mergeFeaturesViewModelLogical.RequestClose -= _mergeFeaturesViewModelLogical_RequestClose; // Safety: avoid double subscription.
            _mergeFeaturesViewModelLogical.RequestClose += new ViewModelWindowMergeFeatures<HluDataSet.incidDataTable,
                    HluDataSet.incidRow>.RequestCloseEventHandler(_mergeFeaturesViewModelLogical_RequestClose);

            // Set the DataContext for data binding.
            _mergeFeaturesWindow.DataContext = _mergeFeaturesViewModelLogical;

            // Reset the result feature index.
            _mergeResultFeatureIndex = -1;

            // Show the window.
            _mergeFeaturesWindow.ShowDialog();

            // Return false if the user didn't choose an incid.
            if (_mergeResultFeatureIndex == -1)
                return false;

            _viewModelMain.ChangeCursor(Cursors.Wait, "Merging ...");
            bool success = true;

            // Check if a transaction is already started.
            bool startTransaction = _viewModelMain.DataBase.Transaction == null;

            // Begin a transaction if not already started.
            if (startTransaction)
                _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            // Start a GIS edit operation.
            EditOperation editOperation = new()
            {
                Name = "Logical Merge GIS Features"
            };

            // Flag the GIS edit as not executed yet.
            bool gisExecuted = false;

            try
            {
                // Get the incid to keep.
                string keepIncid = incidTable[_mergeResultFeatureIndex].incid;

                // Identify polygon rows that will be moved to the kept incid.
                List<HluDataSet.incid_mm_polygonsRow> polygonsToUpdate = selectTable
                    .Where(r => !String.Equals(r.incid, keepIncid, StringComparison.Ordinal))
                    .ToList();

                // Update existing history rows only for polygons that are being moved.
                List<(string Incid, string Toid, string ToidFragId)> losingPolygonKeys = polygonsToUpdate
                    .Select(r => (r.incid, r.toid, r.toidfragid))
                    .Where(k =>
                        !String.IsNullOrWhiteSpace(k.incid) &&
                        !String.IsNullOrWhiteSpace(k.toid) &&
                        !String.IsNullOrWhiteSpace(k.toidfragid))
                    .Distinct()
                    .ToList();

                //// Update the history for the losing polygons.
                //UpdateHistoryForLogicalMerge(
                //    losingPolygonKeys,
                //    keepIncid);

                // Fractions of a second can cause rounding differences when
                // comparing DateTime fields later in some databases.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                // Update selected features except keepIncid.
                DataTable historyTable = await _viewModelMain.GISApplication.MergeFeaturesLogicallyAsync(
                    keepIncid,
                    _viewModelMain.HistoryColumns,
                    editOperation);

                // If the merge failed, throw an exception.
                if ((historyTable == null) || (historyTable.Rows.Count == 0))
                    throw new Exception("Failed to update GIS layer.");

                // Build a list of the columns to update (not the key columns or the length/area).
                List<KeyValuePair<int, object>> updateFields = [];
                var keepPolygon = selectTable.FirstOrDefault(r => r.incid == keepIncid);
                if (keepPolygon != null)
                {
                    updateFields = (from c in selectTable.Columns.Cast<DataColumn>()
                                    where (c.Ordinal != _viewModelMain.HluDataset.incid_mm_polygons.incidColumn.Ordinal) &&
                                        (c.Ordinal != _viewModelMain.HluDataset.incid_mm_polygons.toidColumn.Ordinal) &&
                                        (c.Ordinal != _viewModelMain.HluDataset.incid_mm_polygons.toidfragidColumn.Ordinal) &&
                                        (c.Ordinal != _viewModelMain.HluDataset.incid_mm_polygons.shape_lengthColumn.Ordinal) &&
                                        (c.Ordinal != _viewModelMain.HluDataset.incid_mm_polygons.shape_areaColumn.Ordinal)
                                    select new KeyValuePair<int, object>(c.Ordinal, keepPolygon[c.Ordinal])).ToList();
                }

                // Update the shadow DB copy of the GIS layer.
                foreach (HluDataSet.incid_mm_polygonsRow r in polygonsToUpdate)
                {
                    r.incid = keepIncid;
                    for (int i = 0; i < updateFields.Count; i++)
                        r[updateFields[i].Key] = updateFields[i].Value;
                }

                // Commit the updates to the database.
                if (_viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter.Update(selectTable) == -1)
                    throw new Exception($"Failed to update table [{_viewModelMain.HluDataset.incid_mm_polygons.TableName}].");

                // Insert history rows (fixed value keepIncid).
                Dictionary<int, string> fixedValues = new()
                    {
                        { _viewModelMain.HluDataset.history.incidColumn.Ordinal, keepIncid }
                    };

                // Write the history records.
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);
                vmHist.HistoryWrite(fixedValues, historyTable, Operations.LogicalMerge, nowDtTm);

                // Create a SQL query to find any incid records no longer in use.
                string sqlCount = new(String.Format("SELECT {0}.{1} FROM {0} LEFT JOIN {2} ON {2}.{3} = {0}.{1} WHERE {0}.{1} IN ({4}) GROUP BY {0}.{1} HAVING COUNT({2}.{3}) = 0",
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.IncidTable.TableName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.incidColumn.ColumnName),
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_mm_polygons.incidColumn.ColumnName),
                    string.Join(",", incidTable.Where(r => r.incid != keepIncid).Select(r =>
                        _viewModelMain.DataBase.QuoteValue(r.incid)))));

                // Execute the count query.
                IDataReader delReader = _viewModelMain.DataBase.ExecuteReader(sqlCount,
                    _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text);

                // Check the reader was created successfully.
                if (delReader == null)
                    throw new Exception("Error counting incid and incid_mm_polygons database records.");

                // Build a list of the incids to delete
                List<string> deleteIncids = [];
                while (delReader.Read())
                    deleteIncids.Add(delReader.GetString(0));
                delReader.Close();

                // If there are any incids to delete, build and execute the delete statement.
                if (deleteIncids.Count > 0)
                {
                    // Create a SQL delete statement to remove the unused incid records.
                    string deleteStatement = String.Format(
                        "DELETE FROM {0} WHERE {1} IN ({2})",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                        String.Join(",", deleteIncids.Select(i => _viewModelMain.DataBase.QuoteValue(i)).ToArray()));

                    // Execute the delete statement.
                    try
                    {
                        int numAffected = _viewModelMain.DataBase.ExecuteNonQuery(
                            deleteStatement,
                            _viewModelMain.DataBase.Connection.ConnectionTimeout,
                            CommandType.Text);

                        // If any rows were affected, refresh the total incid count.
                        if (numAffected > 0) _viewModelMain.IncidRowCount(true);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to delete unused rows from table [{_viewModelMain.HluDataset.incid.TableName}].", ex);
                    }
                }

                // Update the last modified details of the kept incid
                _viewModelMain.ViewModelUpdate.UpdateIncidModifiedColumns(keepIncid, nowDtTm);

                // Execute the GIS edit operation.
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (String.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";

                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                // Flag the GIS edit as complete.
                gisExecuted = true;

                // Commit the transaction if started here.
                if (startTransaction)
                {
                    _viewModelMain.DataBase.CommitTransaction();
                    _viewModelMain.HluDataset.AcceptChanges();
                }
            }
            catch (Exception ex)
            {
                // Rollback the transaction on error.
                if (startTransaction) _viewModelMain.DataBase.RollbackTransaction();
                success = false;

                // Ensure any queued but unexecuted GIS edits are discarded.
                if (!gisExecuted)
                {
                    try
                    {
                        editOperation.Abort();
                    }
                    catch
                    {
                        // Ignore abort failures.
                    }
                }

                // Get the SQL error message (if it is one) or the exception message.
                string exMessage = DbBase.GetSqlErrorMessage(ex);

                MessageBox.Show("Merge operation failed. The error message returned was:\n\n" +
                    exMessage, "HLU Merge Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    // Re-count the incid records in the database.
                    _viewModelMain.IncidRowCount(true);

                    // Reset the incid and map selections but don't move
                    // to the first incid in the database.
                    await _viewModelMain.ClearFilterAsync(false);

                    // Synch with the GIS selection.
                    // Force the Incid table to be refilled because it has been
                    // updated directly in the database rather than via the
                    // local copy.
                    _viewModelMain.RefillIncidTable = true;

                    // Get the GIS layer selection again.
                    await _viewModelMain.GetMapSelectionAsync(true);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }

            return success;
        }

        /// <summary>
        /// Handles the closure of the merge features view model and updates the selected feature index.
        /// </summary>
        /// <param name="selectedIndex">The index of the selected feature to be used after the merge operation.</param>
        private void _mergeFeaturesViewModelLogical_RequestClose(int selectedIndex)
        {
            // Unsubscribe from the event to prevent memory leaks.
            _mergeFeaturesViewModelLogical.RequestClose -= _mergeFeaturesViewModelLogical_RequestClose;
            _mergeFeaturesWindow.Close();

            // Set the result selected feature index.
            _mergeResultFeatureIndex = selectedIndex;
        }

        #endregion Logical Merge

        #region Physical Merge

        /// <summary>
        /// Performs a physical merge operation on selected GIS features and updates the database to reflect the
        /// changes.
        /// </summary>
        /// <remarks>This method merges multiple GIS features into a single feature, updates the database
        /// to synchronize with the GIS layer,  and records the history of the merge operation. The method ensures
        /// that the GIS layer and database remain consistent after the merge.</remarks>
        /// <returns><see langword="true"/> if the merge operation completes successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="Exception">Thrown if the GIS merge operation fails, or if the database synchronization process encounters an error.</exception>
        internal async Task<bool> PhysicalMergeAsync()
        {
            // Check one or more features are selected.
            if ((_viewModelMain.GisSelection == null) || (_viewModelMain.GisSelection.Rows.Count == 0))
            {
                MessageBox.Show("Cannot physically merge: Nothing is selected on the map.", "HLU: Physical Merge",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Check more than one feature is selected.
            if (_viewModelMain.GisSelection.Rows.Count <= 1)
            {
                MessageBox.Show("Cannot physically merge: Map selection must contain more than one feature for a merge.",
                    "HLU: Physical Merge", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Check all selected features are in the database.
            if (!_viewModelMain.CheckSelectedToidFrags(false))
            {
                MessageBox.Show("Cannot physically merge: One or more selected map features missing from database.",
                    "HLU: Physical Merge", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            // Check if selected features share same incid and toid.
            if ((_viewModelMain.SelectedIncidsInGISCount != 1) || (_viewModelMain.SelectedToidsInGISCount != 1))
                return false;

            // Perform the physical merge.
            return await PerformPhysicalMergeAsync();
        }

        /// <summary>
        /// Performs a physical merge operation on selected GIS features and updates the database to reflect the
        /// changes.
        /// </summary>
        /// <remarks>This method merges multiple GIS features into a single feature, updates the database
        /// to synchronize with the GIS layer,  and records the history of the merge operation. The method ensures
        /// that the GIS layer and database remain consistent after the merge.</remarks>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the merge
        /// operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="Exception">Thrown if the GIS merge operation fails, or if the database synchronization process encounters an error.</exception>
        private async Task<bool> PerformPhysicalMergeAsync()
        {
            // Get the DB copy rows to update.
            HluDataSet.incid_mm_polygonsDataTable selectTable = new();
            _viewModelMain.GetIncidMMPolygonRows(ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                _viewModelMain.GisSelection.Select(), _viewModelMain.GisIDColumnOrdinals,
                ViewModelWindowMain.IncidPageSize, selectTable), ref selectTable);

            // Check there are DB copy rows to update.
            if (selectTable.Count == 0)
                return false;

            // Check the GIS layer and DB are in sync
            if (selectTable.Count != _viewModelMain.GisSelection.Rows.Count)
                throw new Exception($"GIS Layer and database are out of sync:\n{_viewModelMain.SelectedFragsInGISCount} map polygons, {selectTable.Count} rows in table {_viewModelMain.HluDataset.incid_mm_polygons.TableName}.");

            // Get the lowest toidfragid in selection to assign to the result feature.
            string newToidFragmentID = selectTable.Min(r => r.toidfragid);

            // Choose (or prompt user to select) which feature to keep
            if (selectTable.GroupBy(r => r.incid).Count() == 1)
            {
                int minFragmID = Int32.Parse(newToidFragmentID);
                _mergeResultFeatureIndex = selectTable.Select((r, index) =>
                    Int32.Parse(r.toidfragid) == minFragmID ? index : -1).First(i => i != -1);
            }
            else
            {
                //TODO: This shouldn't be possible (more than 1 incid), so remove?
                // Create the merge features window
                _mergeFeaturesWindow = new WindowMergeFeatures
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                _mergeFeaturesViewModelPhysical = new(selectTable,
                    _viewModelMain.GisIDColumnOrdinals, _viewModelMain.IncidTable.incidColumn.Ordinal,
                    null, _viewModelMain.GISApplication)
                {
                    DisplayName = "Select Feature To Keep"
                };

                // Handle the RequestClose event to get the selected feature index
                _mergeFeaturesViewModelPhysical.RequestClose -= _mergeFeaturesViewModelPhysical_RequestClose; // Safety: avoid double subscription.
                _mergeFeaturesViewModelPhysical.RequestClose += new ViewModelWindowMergeFeatures
                    <HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>
                    .RequestCloseEventHandler(_mergeFeaturesViewModelPhysical_RequestClose);

                // Set the DataContext for data binding.
                _mergeFeaturesWindow.DataContext = _mergeFeaturesViewModelPhysical;

                // Reset the result feature index.
                _mergeResultFeatureIndex = -1;

                // Show the window.
                _mergeFeaturesWindow.ShowDialog();
            }

            // Return false if the user didn't choose an incid.
            if (_mergeResultFeatureIndex == -1)
                return false;

            _viewModelMain.ChangeCursor(Cursors.Wait, "Merging ...");
            bool success = true;

            // Check if a transaction is already started.
            bool startTransaction = _viewModelMain.DataBase.Transaction == null;

            // Begin a transaction if not already started.
            if (startTransaction)
                _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            // Start a GIS edit operation.
            EditOperation editOperation = new()
            {
                Name = "Physical Merge GIS Features"
            };

            // Flag the GIS edit as not executed yet.
            bool gisExecuted = false;

            try
            {
                // Fractions of a second can cause rounding differences when
                // comparing DateTime fields later in some databases.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                // Update the last modified details of the kept incid.
                _viewModelMain.ViewModelUpdate.UpdateIncidModifiedColumns(selectTable[0].incid, nowDtTm);

                // Build a where clause of features to keep.
                List<List<SqlFilterCondition>> resultFeatureWhereClause =
                    ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                    [selectTable[_mergeResultFeatureIndex]],
                        _viewModelMain.GisIDColumnOrdinals, ViewModelWindowMain.IncidPageSize, selectTable);

                if (resultFeatureWhereClause.Count != 1)
                    throw new Exception("Error getting result feature from database.");

                // Build a where clause of features to merge.
                List<List<SqlFilterCondition>> mergeFeaturesWhereClause =
                    ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                    selectTable.Where((r, index) => index != _mergeResultFeatureIndex).ToArray(),
                        _viewModelMain.GisIDColumnOrdinals, ViewModelWindowMain.IncidPageSize, selectTable);

                if (mergeFeaturesWhereClause.Count <= 0)
                    throw new Exception("Error getting merge feature(s) from database.");

                // HistoryTable contains rows of features merged into result feature (i.e. no longer existing)
                // and last row with data of result feature (remaining in GIS, lowest toidfragid of merged features)
                // this last row must be removed before writing history
                // but is needed to update geometry fields in incid_mm_polygons.
                DataTable historyTable = await _viewModelMain.GISApplication.MergeFeaturesPhysicallyAsync(
                    newToidFragmentID,
                    resultFeatureWhereClause[0].Select(c => c.Clone()).ToList(),
                    _viewModelMain.HistoryColumns,
                    editOperation);

                // If the merge failed, throw an exception.
                if ((historyTable == null) || (historyTable.Rows.Count == 0))
                    throw new Exception("Failed to update GIS layer.");

                // Update the history table to reflect there is now only one feature.
                DataTable resultTable = historyTable.Clone();
                DataRow resultRow = historyTable.AsEnumerable().FirstOrDefault(r =>
                    r.Field<string>(_viewModelMain.HluDataset.history.toidfragidColumn.ColumnName) == newToidFragmentID);

                if (resultRow == null)
                    throw new Exception("Failed to obtain geometry data of result feature from GIS.");

                // Load the result row and remove it from the history table.
                resultTable.LoadDataRow(resultRow.ItemArray, true);
                resultRow.Delete();
                historyTable.AcceptChanges();

                // Build a list of merged toidfragids (excluding the kept fragment).
                List<string> mergedToidFragIds = selectTable
                    .Where((r, index) => index != _mergeResultFeatureIndex)
                    .Select(r => r.toidfragid)
                    .Where(f => !String.IsNullOrWhiteSpace(f))
                    .Distinct()
                    .ToList();

                // Update existing history rows for physically merged (deleted) fragments
                // so history is preserved against the kept fragment.
                UpdateHistoryForPhysicalMerge(
                    selectTable[0].incid,
                    selectTable[0].toid,
                    mergedToidFragIds,
                    newToidFragmentID);

                // Synchronize the DB shadow copy of the GIS layer.
                MergeSynchronizeIncidMMPolygons(selectTable, resultTable, newToidFragmentID,
                    resultFeatureWhereClause[0], mergeFeaturesWhereClause);

                // Create fixed values for history write.
                Dictionary<int, string> fixedValues = new()
                    {
                        { _viewModelMain.HluDataset.history.incidColumn.Ordinal, selectTable[0].incid },
                        { _viewModelMain.HluDataset.history.toidColumn.Ordinal, selectTable[0].toid },
                        { _viewModelMain.HluDataset.history.toidfragidColumn.Ordinal, newToidFragmentID }
                    };

                // Write the history records.
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);
                vmHist.HistoryWrite(fixedValues, historyTable, Operations.PhysicalMerge, nowDtTm);

                // Execute the GIS edit operation.
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (String.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";

                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                // Flag the GIS edit as complete.
                gisExecuted = true;

                // Commit the transaction if started here.
                if (startTransaction)
                {
                    _viewModelMain.DataBase.CommitTransaction();
                    _viewModelMain.HluDataset.AcceptChanges();
                }
            }
            catch (Exception ex)
            {
                // Rollback the transaction on error.
                if (startTransaction) _viewModelMain.DataBase.RollbackTransaction();
                success = false;

                // Ensure any queued but unexecuted GIS edits are discarded.
                if (!gisExecuted)
                {
                    try
                    {
                        editOperation.Abort();
                    }
                    catch
                    {
                        // Ignore abort failures.
                    }
                }

                // Get the SQL error message (if it is one) or the exception message.
                string exMessage = DbBase.GetSqlErrorMessage(ex);

                MessageBox.Show("Merge operation failed. The error message returned was:\n\n" +
                    exMessage, "HLU Merge Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    // Re-count the incid records in the database.
                    _viewModelMain.IncidRowCount(true);

                    // Reset the incid and map selections but don't move
                    // to the first incid in the database.
                    await _viewModelMain.ClearFilterAsync(false);

                    // Synch with the GIS selection.
                    // Force the Incid table to be refilled because it has been
                    // updated directly in the database rather than via the
                    // local copy.
                    _viewModelMain.RefillIncidTable = true;

                    // Get the GIS layer selection again.
                    await _viewModelMain.GetMapSelectionAsync(true);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }

            return success;
        }

        /// <summary>
        /// Handles the closure of the merge features view model and updates the selected feature index.
        /// </summary>
        /// <param name="selectedIndex">The index of the selected feature to be set after the view model is closed.</param>
        private void _mergeFeaturesViewModelPhysical_RequestClose(int selectedIndex)
        {
            // Unsubscribe from the event to prevent memory leaks.
            _mergeFeaturesViewModelPhysical.RequestClose -= _mergeFeaturesViewModelPhysical_RequestClose;
            _mergeFeaturesWindow.Close();

            // Set the result selected feature index.
            _mergeResultFeatureIndex = selectedIndex;
        }

        #endregion Physical Merge

        #region Merge Synchronization

        /// <summary>
        /// Synchronizes and merges polygon features in the specified dataset by updating the result feature with new
        /// attributes and removing merged features from the dataset.
        /// </summary>
        /// <remarks>This method performs the following operations: <list type="bullet">
        /// <item><description>Updates the result feature with the new identifier and, if applicable, the combined shape
        /// length and area of the merged features.</description></item> <item><description>Deletes the merged features
        /// from the dataset.</description></item> <item><description>Ensures transactional integrity by wrapping the
        /// operations in a database transaction.</description></item> </list> The method supports different geometry
        /// types (point, line, polygon) and adjusts the update logic accordingly.</remarks>
        /// <param name="selectTable">The source table containing the polygon features to be merged.</param>
        /// <param name="resultTable">The table containing the result feature to be updated with the merged attributes.</param>
        /// <param name="newToidFragmentID">The new identifier to assign to the result feature after the merge.</param>
        /// <param name="resultFeatureWhereClause">A list of conditions used to identify the result feature to be updated.</param>
        /// <param name="mergeFeaturesWhereClause">A collection of condition lists, where each list specifies the criteria for identifying the features to be
        /// merged and removed from the dataset.</param>
        private void MergeSynchronizeIncidMMPolygons(HluDataSet.incid_mm_polygonsDataTable selectTable,
            DataTable resultTable, string newToidFragmentID, List<SqlFilterCondition> resultFeatureWhereClause,
            List<List<SqlFilterCondition>> mergeFeaturesWhereClause)
        {
            // Create an update statement for the result feature: lowest toidfragid
            // in the selection set and sum of shape_length/shape_area of merged features
            string updateWhereClause = _viewModelMain.DataBase.WhereClause(false, true, true, resultFeatureWhereClause);
            string updateStatement = null;
            switch (_viewModelMain.GisLayerType)
            {
                // Update just the toidfragid for point geometries.
                case GeometryTypes.Point:
                    updateStatement = String.Format("UPDATE {0} SET {1} = {2} WHERE {3}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.toidfragidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(newToidFragmentID), updateWhereClause);
                    break;

                // Update toidfragid and shape_length for line geometries.
                case GeometryTypes.Line:
                    double plineLength = resultTable.Rows[0].Field<double>(ViewModelWindowMain.HistoryGeometry1ColumnName);
                    updateStatement = String.Format("UPDATE {0} SET {1} = {2}, {3} = {4} WHERE {5}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.toidfragidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(newToidFragmentID),
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.shape_lengthColumn.ColumnName),
                        plineLength, updateWhereClause);
                    break;

                // Update toidfragid, shape_length and shape_area for polygon geometries.
                case GeometryTypes.Polygon:
                    double shapeLength = resultTable.Rows[0].Field<double>(ViewModelWindowMain.HistoryGeometry1ColumnName);
                    double shapeArea = resultTable.Rows[0].Field<double>(ViewModelWindowMain.HistoryGeometry2ColumnName);
                    updateStatement = String.Format("UPDATE {0} SET {1} = {2}, {3} = {4}, {5} = {6} WHERE {7}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.toidfragidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(newToidFragmentID),
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.shape_lengthColumn.ColumnName), shapeLength,
                        _viewModelMain.DataBase.QuoteIdentifier(
                            _viewModelMain.HluDataset.incid_mm_polygons.shape_areaColumn.ColumnName),
                        shapeArea, updateWhereClause);
                    break;
            }

            // Check if a transaction is already started.
            bool startTransaction = _viewModelMain.DataBase.Transaction == null;

            // Start a transaction if not already started.
            if (startTransaction) _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            try
            {
                // Delete the merged polygons from shadow table in the DB.
                List<List<SqlFilterCondition>> cleanList = _viewModelMain.DataBase.JoinWhereClauseLists(mergeFeaturesWhereClause);
                foreach (List<SqlFilterCondition> oneWhereClause in cleanList)
                {
                    // Create a delete statement for each set of where clauses
                    String deleteStatement = String.Format("DELETE FROM {0} WHERE {1}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName),
                        _viewModelMain.DataBase.WhereClause(false, true, true, oneWhereClause));

                    // Execute the delete statement.
                    try
                    {
                        _viewModelMain.DataBase.ExecuteNonQuery(
                            deleteStatement,
                            _viewModelMain.DataBase.Connection.ConnectionTimeout,
                            CommandType.Text);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to delete from table [{_viewModelMain.HluDataset.incid_mm_polygons.TableName}].", ex);
                    }
                }

                // Execute the update statement.
                try
                {
                    _viewModelMain.DataBase.ExecuteNonQuery(
                        updateStatement,
                        _viewModelMain.DataBase.Connection.ConnectionTimeout,
                        CommandType.Text);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to update table [{_viewModelMain.HluDataset.incid_mm_polygons.TableName}].", ex);
                }

                // Commit the transaction if started here.
                if (startTransaction) _viewModelMain.DataBase.CommitTransaction();
            }
            catch
            {
                // Rollback the transaction on error.
                if (startTransaction) _viewModelMain.DataBase.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// Updates existing history rows for a logical merge so that history belonging to the losing polygon keys
        /// (incid + toid + toidfragid) is reassigned to the kept incid.
        /// </summary>
        /// <param name="losingPolygonKeys">
        /// The polygon keys (losing incid + toid + toidfragid) that are being merged into the kept incid.
        /// </param>
        /// <param name="keepIncid">The kept incid.</param>
        private void UpdateHistoryForLogicalMerge(
            List<(string Incid, string Toid, string ToidFragId)> losingPolygonKeys,
            string keepIncid)
        {
            // Check parameters
            if (String.IsNullOrWhiteSpace(keepIncid)) return;
            if ((losingPolygonKeys == null) || (losingPolygonKeys.Count == 0)) return;

            // Remove any keys that already belong to the kept incid (belt-and-braces).
            losingPolygonKeys = losingPolygonKeys
                .Where(k => !String.Equals(k.Incid, keepIncid, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            // If no losing keys remain, exit.
            if (losingPolygonKeys.Count == 0) return;

            // Build a values clause for the update statement.
            string valuesClause = String.Join(",",
                losingPolygonKeys.Select(k => String.Format(
                    "({0}, {1}, {2})",
                    _viewModelMain.DataBase.QuoteValue(k.Incid),
                    _viewModelMain.DataBase.QuoteValue(k.Toid),
                    _viewModelMain.DataBase.QuoteValue(k.ToidFragId))));

            // Build the update statement (more efficient and safer than multiple OR conditions).
            string updateStatement = String.Format(
                "UPDATE h SET {0} = {1} " +
                "FROM {2} h " +
                "INNER JOIN (VALUES {3}) v(old_incid, old_toid, old_toidfragid) " +
                "ON h.{4} = v.old_incid AND h.{5} = v.old_toid AND h.{6} = v.old_toidfragid",
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.incidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(keepIncid),
                _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.history.TableName),
                valuesClause,
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.incidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.toidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.toidfragidColumn.ColumnName));

            // Execute the update statement within the caller's transaction.
            try
            {
                _viewModelMain.DataBase.ExecuteNonQuery(
                    updateStatement,
                    _viewModelMain.DataBase.Connection.ConnectionTimeout,
                    CommandType.Text);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update history rows for logical merge in table [{_viewModelMain.HluDataset.history.TableName}].", ex);
            }
        }

        /// <summary>
        /// Updates existing history rows for a physical merge so that any history belonging
        /// to merged (deleted) fragments is reassigned to the kept fragment.
        /// </summary>
        /// <param name="incid">The incid shared by all merged features.</param>
        /// <param name="toid">The toid shared by all merged features.</param>
        /// <param name="oldToidFragIds">The fragment IDs that will be deleted from incid_mm_polygons.</param>
        /// <param name="newToidFragId">The fragment ID that remains after the merge.</param>
        private void UpdateHistoryForPhysicalMerge(
            string incid,
            string toid,
            List<string> oldToidFragIds,
            string newToidFragId)
        {
            // Check parameters
            if (String.IsNullOrWhiteSpace(incid)) return;
            if (String.IsNullOrWhiteSpace(toid)) return;
            if (String.IsNullOrWhiteSpace(newToidFragId)) return;
            if ((oldToidFragIds == null) || (oldToidFragIds.Count == 0)) return;

            // Remove the kept frag id if it accidentally appears in the list.
            oldToidFragIds = oldToidFragIds
                .Where(f => !String.IsNullOrWhiteSpace(f) && !String.Equals(f, newToidFragId, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            // If no old frag ids remain, exit.
            if (oldToidFragIds.Count == 0) return;

            // Build the IN list for the update statement.
            string inList = String.Join(",",
                oldToidFragIds.Select(f => _viewModelMain.DataBase.QuoteValue(f)).ToArray());

            // Build the update statement.
            string updateStatement = String.Format(
                "UPDATE {0} SET {1} = {2} WHERE {3} = {4} AND {5} = {6} AND {1} IN ({7})",
                _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.history.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.toidfragidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(newToidFragId),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.incidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(incid),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.history.toidColumn.ColumnName),
                _viewModelMain.DataBase.QuoteValue(toid),
                inList);

            // Execute the update statement within the caller's transaction.
            try
            {
                _viewModelMain.DataBase.ExecuteNonQuery(
                    updateStatement,
                    _viewModelMain.DataBase.Connection.ConnectionTimeout,
                    CommandType.Text);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update history rows for physical merge in table [{_viewModelMain.HluDataset.history.TableName}].", ex);
            }
        }

        #endregion Merge Synchronization
    }
}