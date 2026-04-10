// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013 Thames Valley Environmental Records Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
// Copyright © 2020 Greenspace Information for Greater London CIC
// Copyright © 2025-2026 Andy Foy Consulting
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

using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Enums;
using HLU.Exceptions;
using HLU.UI.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the main window bulk update functionality.
    /// </summary>
    class ViewModelWindowMainBulkUpdate
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;
        private WindowBulkUpdate _windowBulkUpdate;
        private ViewModelWindowBulkUpdate _viewModelBulkUpdate;

        private AddInSettings _addInSettings;

        //private HluDataSet.incid_ihs_matrixDataTable _ihsMatrixTable = new();
        //private HluDataSet.incid_ihs_formationDataTable _ihsFormationTable = new();
        //private HluDataSet.incid_ihs_managementDataTable _ihsManagementTable = new();
        //private HluDataSet.incid_ihs_complexDataTable _ihsComplexTable = new();

        private bool _bulkDeleteOrphanBapHabitats;
        private bool _bulkDeletePotentialBapHabitats;
        private bool _bulkDeleteIHSCodes;
        private bool _bulkDeleteSecondaryCodes;
        private bool _bulkCreateHistory;
        private string _bulkDeterminationQuality;
        private string _bulkInterpretationQuality;

        private bool _osmmBulkUpdateMode;

        //private DataTable _incidSelectionBackup;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowMainBulkUpdate class.
        /// </summary>
        /// <param name="viewModelMain">The main view model instance.</param>
        /// <param name="addInSettings">The add-in settings instance.</param>
        public ViewModelWindowMainBulkUpdate(ViewModelWindowMain viewModelMain, AddInSettings addInSettings)
        {
            _viewModelMain = viewModelMain;
            _addInSettings = addInSettings;
        }

        #endregion Constructor

        #region Bulk Update

        /// <summary>
        /// Starts standard Bulk Update mode.
        /// </summary>
        public void StartStandardBulkUpdate()
        {
            // Store whether we are starting in OSMM Bulk update
            // mode or just standard Bulk Update mode
            _osmmBulkUpdateMode = false;

            // Update the modes in the main view model
            _viewModelMain.BulkUpdateMode = true;

            // Clear the changed flag
            _viewModelMain.Changed = false;

            // Clear all the form fields
            _viewModelMain.ClearForm();

            // Clone the blank row as the dirty-check baseline.
            // Any field the user subsequently edits will differ
            // from this clone, making IsDirtyIncid() return true.
            _viewModelMain.CloneIncidCurrentRow();

            // Select the habitat tab
            _viewModelMain.TabItemSelected = 0;

            // Clear any interface warning and error messages
            _viewModelMain.ResetWarningsErrors();

            // RefreshAll is called by SetWorkModeFlag when BulkUpdateMode is set above.
            _viewModelMain.RefreshAll();
        }

        /// <summary>
        /// Displays the bulk update window.
        /// </summary>
        public void ShowBulkUpdateWindow()
        {
            // Backup the current selection (filter).
            //_incidSelectionBackup = _viewModelMain.IncidSelection;

            // Get the default settings
            bool deleteOrphanBapHabitats = _addInSettings.BulkUpdateDeleteOrphanBapHabitats;
            bool deletePotentialBapHabitats = _addInSettings.BulkUpdateDeletePotentialBapHabitats;
            bool deleteIHSCodes = _addInSettings.BulkUpdateDeleteIHSCodes;
            bool deleteSecondaryCodes = _addInSettings.BulkUpdateDeleteSecondaryCodes;
            bool createHistory = _addInSettings.BulkUpdateCreateHistoryRecords;
            string determinationQuality = _addInSettings.BulkUpdateDeterminationQuality;
            string interpretationQuality = _addInSettings.BulkUpdateInterpretationQuality;
            bool primaryChanged = true;

            // If in OSMM Bulk Update mode set the mandatory options
            if (_osmmBulkUpdateMode == true)
            {
                // Flag the primary habitat has not been changed (so that the user
                // has no control over these options).
                primaryChanged = false;
                deleteOrphanBapHabitats = true;
                deletePotentialBapHabitats = true;
                deleteIHSCodes = true;
                deleteSecondaryCodes = true;
                createHistory = true;
            }
            // If the primary habitat has not been changed
            else if (_viewModelMain.IncidPrimary == null)
            {
                // Flag the primary habitat has not changed
                primaryChanged = false;
                deleteOrphanBapHabitats = false;
                deletePotentialBapHabitats = false;
                deleteSecondaryCodes = false;
            }

            // create window
            _windowBulkUpdate = new()
            {
                // Set ArcGIS Pro as the parent
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Count the non-blank rows from the user interface
            int sourceCount = (from nr in _viewModelMain.IncidSourcesRows
                               where nr.source_id != Int32.MinValue
                               select nr).Count();

            // Display the bulk update interface to prompt the user
            // to select the options they want to use.
            _viewModelBulkUpdate = new ViewModelWindowBulkUpdate(_viewModelMain, _osmmBulkUpdateMode,
                deleteOrphanBapHabitats, deletePotentialBapHabitats, deleteIHSCodes,
                deleteSecondaryCodes, sourceCount, createHistory, determinationQuality,
                interpretationQuality, primaryChanged);

            if (_osmmBulkUpdateMode == true)
                _viewModelBulkUpdate.DisplayName = "OSMM Bulk Update";
            else
                _viewModelBulkUpdate.DisplayName = "Bulk Update";

            _viewModelBulkUpdate.RequestClose -= ViewModelBulkUpdate_RequestClose; // Safety: avoid double subscription.
            _viewModelBulkUpdate.RequestClose +=
                new ViewModelWindowBulkUpdate.RequestCloseEventHandler(ViewModelBulkUpdate_RequestClose);

            // allow all controls in window to bind to ViewModel by setting DataContext
            _windowBulkUpdate.DataContext = _viewModelBulkUpdate;

            // show window
            _windowBulkUpdate.ShowDialog();
        }

        /// <summary>
        /// Handles the RequestClose event from the bulk update view model. If the user selected to apply the update then
        /// the bulk update is applied using the specified options.
        /// </summary>
        /// <param name="apply">Indicates whether the bulk update should be applied.</param>
        /// <param name="bulkDeleteOrphanBapHabitats">Indicates whether orphan BAP habitats should be deleted.</param>
        /// <param name="bulkDeletePotentialBapHabitats">Indicates whether potential BAP habitats should be deleted.</param>
        /// <param name="bulkDeleteIHSCodes">Indicates whether IHS codes should be deleted.</param>
        /// <param name="bulkDeleteSecondaryCodes">Indicates whether secondary codes should be deleted.</param>
        /// <param name="bulkCreateHistory">Indicates whether history should be created.</param>
        /// <param name="bulkDeterminationQuality">Specifies the determination quality for the bulk update.</param>
        /// <param name="bulkInterpretationQuality">Specifies the interpretation quality for the bulk update.</param>
        private async void ViewModelBulkUpdate_RequestClose(bool apply,
            bool bulkDeleteOrphanBapHabitats,
            bool bulkDeletePotentialBapHabitats,
            bool bulkDeleteIHSCodes,
            bool bulkDeleteSecondaryCodes,
            bool bulkCreateHistory,
            string bulkDeterminationQuality,
            string bulkInterpretationQuality)
        {
            _viewModelBulkUpdate.RequestClose -= ViewModelBulkUpdate_RequestClose;
            _windowBulkUpdate.Close();

            // If the user selected to apply the update then
            // perform the update using the options set.
            if (apply == true)
            {
                // Set the options for processing the bulk update
                _bulkDeleteOrphanBapHabitats = bulkDeleteOrphanBapHabitats;
                _bulkDeletePotentialBapHabitats = bulkDeletePotentialBapHabitats;
                _bulkDeleteIHSCodes = bulkDeleteIHSCodes;
                _bulkDeleteSecondaryCodes = bulkDeleteSecondaryCodes;
                _bulkCreateHistory = bulkCreateHistory;
                _bulkDeterminationQuality = bulkDeterminationQuality;
                _bulkInterpretationQuality = bulkInterpretationQuality;

                // Apply the bulk update
                if (_osmmBulkUpdateMode == true)
                    await ApplyOSMMBulkUpdateAsync();
                else
                    await ApplyBulkUpdateAsync();
            }
        }

        /// <summary>
        /// Applies the bulk update.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ApplyBulkUpdateAsync()
        {
            // Reason and Process are required to write history records.
            if (String.IsNullOrEmpty(_viewModelMain.Reason) || String.IsNullOrEmpty(_viewModelMain.Process))
            {
                MessageBox.Show("A Reason and Process must be selected in the toolbar before applying a Bulk Update.",
                    "HLU: Bulk Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _viewModelMain.ChangeCursor(Cursors.Wait, "Applying bulk update ...");

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            bool success = false;

            // Start a GIS edit operation
            EditOperation editOperation = new()
            {
                Name = "Bulk Update GIS Features"
            };

            try
            {
                // Build a blank WHERE clause based on incid column
                string incidWhereClause = String.Format(" WHERE {0} = {1}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteValue("{0}"));

                // Build the SELECT statement based on the incid where clause
                string selectStatementIncid = String.Format("SELECT {0} FROM {1}{2}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.habitat_primaryColumn.ColumnName),
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName), incidWhereClause);

                // Update the last modified date & user fields on the incid table
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                _viewModelMain.IncidCurrentRow.last_modified_date = nowDtTm;
                _viewModelMain.IncidCurrentRow.last_modified_user_id = _viewModelMain.UserID;

                // Update the primary and secondary habitats on the incid table
                _viewModelMain.IncidCurrentRow.habitat_primary = _viewModelMain.IncidPrimary;
                _viewModelMain.IncidCurrentRow.habitat_secondaries = _viewModelMain.IncidSecondarySummary;

                // Update the habitat class if the primary habitat has changed
                if (!_viewModelMain.IncidCurrentRow.Ishabitat_primaryNull())
                    _viewModelMain.IncidCurrentRow.habitat_class =
                        _viewModelMain.GetHabitatClassForPrimary(_viewModelMain.IncidCurrentRow.habitat_primary);

                // Update the habitat version if the primary habitat has changed
                if (!_viewModelMain.IncidCurrentRow.Ishabitat_primaryNull())
                    _viewModelMain.IncidCurrentRow.habitat_version =
                        _viewModelMain.GetHabitatVersionForPrimary(_viewModelMain.IncidCurrentRow.habitat_primary);

                // Build a collection of the updated columns in the incid table
                var incidUpdateCols = _viewModelMain.HluDataset.incid.Columns.Cast<DataColumn>()
                    .Where(c => c.Ordinal != _viewModelMain.HluDataset.incid.incidColumn.Ordinal &&
                        !_viewModelMain.IncidCurrentRow.IsNull(c.Ordinal));

                // Build an UPDATE statement for the incid table
                string updateVals = String.Join(",", _viewModelMain.HluDataset.incid.Columns.Cast<DataColumn>()
                .Where(c => c.Ordinal != _viewModelMain.HluDataset.incid.incidColumn.Ordinal &&
                    !_viewModelMain.IncidCurrentRow.IsNull(c.Ordinal))
                .Select(c => String.Format("{0} = {1}",
                    _viewModelMain.DataBase.QuoteIdentifier(c.ColumnName),
                    _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow[c.Ordinal]))));

                string updateStatementIncid = !incidUpdateCols.Any() ? String.Empty :
                    String.Format("UPDATE {0} SET {1}", _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName), updateVals);

                // If all secondary codes are to be deleted then add
                // the secondary habitat summary to the list of columns to update
                if ((_bulkDeleteSecondaryCodes) && (_viewModelMain.IncidCurrentRow.habitat_secondaries == null))
                {
                    updateStatementIncid = String.Format("{0}, {1} = {2}",
                        updateStatementIncid,
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.habitat_secondariesColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow.habitat_secondaries));
                }

                // If all IHS multiplex codes are to be deleted clear then add
                // the IHS habitat code to the list of columns to update
                if (_bulkDeleteIHSCodes)
                {
                    _viewModelMain.IncidCurrentRow.ihs_habitat = null;

                    updateStatementIncid = String.Format("{0}, {1} = {2}",
                        updateStatementIncid,
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.ihs_habitatColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow.ihs_habitat));
                }

                // Finally, add the where clause to the update command
                updateStatementIncid = String.Concat(updateStatementIncid, incidWhereClause);

                // Build DELETE statements for all IHS multiplex rows
                List<string> ihsMultiplexDeleteStatements = [];

                // If all IHS multiplex codes are to be deleted build
                // DELETE statements for all IHS multiplex rows
                if (_bulkDeleteIHSCodes)
                {
                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_matrix.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_matrix.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_formation.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_formation.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_management.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_management.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_complex.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_complex.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));
                }

                // Build DELETE statement for all secondary rows
                string secondaryDelStatement = null;

                // If all secondary codes are to be deleted
                if (_bulkDeleteSecondaryCodes)
                {
                    // Build DELETE statements for all secondary rows
                    secondaryDelStatement =
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_secondary.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_secondary.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}"));
                }

                // Filter out any source rows not set (because the maximum number of blank rows are
                // created at the start of the bulk process so any not used need to be removed)
                _viewModelMain.IncidSourcesRows = FilterUpdateRows<HluDataSet.incid_sourcesDataTable,
                    HluDataSet.incid_sourcesRow>(_viewModelMain.IncidSourcesRows);

                // Filter out any condition rows not set (because a single null row is
                // created so many need to be removed if it's still null)
                _viewModelMain.IncidConditionRows = FilterUpdateRows<HluDataSet.incid_conditionDataTable,
                    HluDataSet.incid_conditionRow>(_viewModelMain.IncidConditionRows);

                // Get the column ordinal for the incid column on the incid table
                int incidOrdinal =
                    _viewModelMain.IncidSelection.Columns[_viewModelMain.HluDataset.incid.incidColumn.ColumnName].Ordinal;

                // Loop through each row in the incid selection
                foreach (DataRow r in _viewModelMain.IncidSelection.Rows)
                {
                    // Set the incid for the current row
                    string currIncid = r[incidOrdinal].ToString();

                    // Store the rows from the user interface
                    ObservableCollection<SecondaryHabitat> secondaryHabitats = _viewModelMain.IncidSecondaryHabitats;

                    // Perform the bulk updates on the data tables
                    BulkUpdateAdo(currIncid, secondaryHabitats, selectStatementIncid, updateStatementIncid, null,
                        ihsMultiplexDeleteStatements, secondaryDelStatement, _bulkDeleteOrphanBapHabitats, _bulkDeletePotentialBapHabitats);
                }

                // Perform the bulk updates on the GIS data, shadow copy in DB and history
                await BulkUpdateGisAsync(incidOrdinal, _viewModelMain.IncidSelection, _bulkDeleteSecondaryCodes, _bulkCreateHistory, Operations.BulkUpdate, nowDtTm, editOperation);

                // Commit the transaction and accept the changes ???
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                // Force re-loading data from db
                _viewModelMain.IncidTable.Clear();

                success = true;
            }
            catch (Exception ex)
            {
                // Rollback the transaction
                _viewModelMain.DataBase.RollbackTransaction();

                try
                {
                    // Abort the GIS updates
                    editOperation.Abort();
                }
                catch
                {
                    // Ignore abort failures.
                }

                // Get the SQL error message (if it is one) or the exception message.
                string exMessage = DbBase.GetSqlErrorMessage(ex);

                // Show error message
                MessageBox.Show(String.Format("Bulk update failed. The error message returned was:\n\n{0}",
                    ex.Message), "HLU: Bulk Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Stop the Bulk Update mode and reset all the controls
                await BulkUpdateResetControlsAsync();
            }

            // Show success message
            if (success)
                _viewModelMain.ShowSuccess("Bulk update succeeded.", MessageCategory.BulkUpdate);
        }

        /// <summary>
        /// Cancels the Bulk Update mode.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task CancelBulkUpdateAsync()
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Stopping Bulk Update mode ...");

            // Stop the Bulk Update mode and reset all the controls
            await BulkUpdateResetControlsAsync();

            // Reset the cursor back to normal.
            _viewModelMain.ChangeCursor(Cursors.Arrow);
        }

        /// <summary>
        /// Stops the Bulk Update mode and resets all
        /// the controls to normal.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task BulkUpdateResetControlsAsync()
        {
            // Force the Incid table to be refilled because it has been
            // updated directly in the database rather than via the
            // local copy.
            _viewModelMain.RefillIncidTable = true;

            // Re-enable the tab control and individual tabs.
            _viewModelMain.TabControlDataEnabled = true;
            _viewModelMain.TabItemHabitatEnabled = true;
            _viewModelMain.TabItemIHSEnabled = true;

            // Show the history tab
            _viewModelMain.ShowHistoryTab = true;
            _viewModelMain.ShowIHSTab = true;

            // Select the habitat tab
            _viewModelMain.TabItemSelected = 0;

            // Stop the Bulk Update mode
            _viewModelMain.BulkUpdateMode = false;

            // Clear the active filter.
            _viewModelMain.SuppressUserNotifications = true;
            await _viewModelMain.ClearFilterAsync(true);
            _viewModelMain.SuppressUserNotifications = false;

            // Re-read the current map selection.
            await _viewModelMain.GetMapSelectionAsync(false);
        }

        #endregion Bulk Update

        #region OSMM Bulk Update

        /// <summary>
        /// Starts OSMM Bulk Update mode.
        /// </summary>
        public void StartOSMMBulkUpdate()
        {
            // Can't start OSMM Bulk Update mode if the bulk OSMM source hasn't been set.
            if (_addInSettings.BulkOSMMSourceId == null)
            {
                // Show warning message
                _viewModelMain.ShowWarning(
                    "The Bulk OSMM Source has not been set.\n\n" +
                    "Please set the Bulk OSMM Source in the Options.",
                    MessageCategory.BulkUpdate);

                return;
            }

            // Store whether we are starting in OSMM Bulk update
            // mode or just standard Bulk Update mode
            _osmmBulkUpdateMode = true;

            // Update the modes in the main view model
            _viewModelMain.OSMMBulkUpdateMode = true;

            // Clear all the form fields.
            _viewModelMain.ClearForm();

            // Disable the Habitat and IHS tabs
            _viewModelMain.TabItemHabitatEnabled = false;
            _viewModelMain.TabItemIHSEnabled = false;

            // Clear the selection (filter).
            _viewModelMain.IncidSelection = null;

            // Select the priority habitats tab
            _viewModelMain.TabItemSelected = 2;

            // Clear any interface warning and error messages
            _viewModelMain.ResetWarningsErrors();
            //_viewModelMain.RefreshAll(); // Now done when setting mode.

            // Reset the database counts
            _viewModelMain.SelectedIncidsInDBCount = 0;
            _viewModelMain.SelectedFragsInDBCount = 0;

            // Open the OSMM Update filter
            _viewModelMain.OpenWindowQueryOSMM(true);
        }

        /// <summary>
        /// Applies the pending OSMM Bulk Updates.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ApplyOSMMBulkUpdateAsync()
        {
            // Reason and Process are required to write history records.
            if (String.IsNullOrEmpty(_viewModelMain.Reason) || String.IsNullOrEmpty(_viewModelMain.Process))
            {
                MessageBox.Show("A Reason and Process must be selected in the toolbar before applying an OSMM Bulk Update.",
                    "HLU: OSMM Bulk Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _viewModelMain.ChangeCursor(Cursors.Wait, "Applying OSMM bulk update ...");

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            bool success = false;

            // Start a GIS edit operation
            EditOperation editOperation = new()
            {
                Name = "OSMM Bulk Update GIS Features"
            };

            try
            {
                // Build a blank WHERE clause based on incid column
                string incidWhereClause = String.Format(" WHERE {0} = {1}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteValue("{0}"));

                // Build the SELECT statement based on the incid where clause
                string selectStatementIncid = String.Format("SELECT {0} FROM {1}{2}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.habitat_primaryColumn.ColumnName),
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName), incidWhereClause);

                // Update the last modified date & user fields on the incid table
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                _viewModelMain.IncidCurrentRow.last_modified_date = nowDtTm;
                _viewModelMain.IncidCurrentRow.last_modified_user_id = _viewModelMain.UserID;

                // Set the primary habitat on the incid table to a non-null value
                // to indicate that the column has/will change
                _viewModelMain.IncidCurrentRow.habitat_primary = "";

                // Build a collection of the updated columns in the incid table
                var incidUpdateCols = _viewModelMain.HluDataset.incid.Columns.Cast<DataColumn>()
                    .Where(c => c.Ordinal != _viewModelMain.HluDataset.incid.incidColumn.Ordinal &&
                        !_viewModelMain.IncidCurrentRow.IsNull(c.Ordinal));

                // If all IHS codes are to be deleted clear
                string updateIHSHabitat = "";
                List<string> ihsMultiplexDeleteStatements = [];
                if (_bulkDeleteIHSCodes)
                {
                    // Build a statement to update the IHS habitat code
                    _viewModelMain.IncidCurrentRow.ihs_habitat = null;

                    updateIHSHabitat = String.Format(", {0} = {1}",
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.ihs_habitatColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow.ihs_habitat));

                    // Build DELETE statements for all IHS multiplex rows
                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_matrix.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_matrix.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_formation.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_formation.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_management.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_management.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));

                    ihsMultiplexDeleteStatements.Add(
                        String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_complex.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_ihs_complex.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}")));
                }

                // Build DELETE statement for all secondary rows
                string secondaryDelStatement = null;

                // If all secondary codes are to be deleted
                // (this should always be true for OSMM bulk updates)
                if (_bulkDeleteSecondaryCodes)
                {
                    // Build DELETE statement for all secondary habitat rows.
                    secondaryDelStatement = String.Format("DELETE FROM {0} WHERE {1} = {2}",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_secondary.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_secondary.incidColumn.ColumnName),
                        _viewModelMain.DataBase.QuoteValue("{0}"));
                }

                // Build an UPDATE statement for the incid_osmm_updates table
                string updateStatementIncidOSMMUpdates = String.Format("UPDATE {0} SET {1} = -1, {2} = {3}, {4} = {5}",
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_osmm_updates.TableName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_osmm_updates.statusColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_osmm_updates.last_modified_dateColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteValue(nowDtTm),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid_osmm_updates.last_modified_user_idColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteValue(_viewModelMain.UserID))
                    + incidWhereClause;

                // Get the column ordinal for the incid column on the incid table
                int incidOrdinal =
                    _viewModelMain.IncidSelection.Columns[_viewModelMain.HluDataset.incid.incidColumn.ColumnName].Ordinal;

                // Loop through each row in the incid selection
                foreach (DataRow r in _viewModelMain.IncidSelection.Rows)
                {
                    // Set the incid for the current row
                    string currIncid = r[incidOrdinal].ToString();
                    string[] relValues = [currIncid];

                    // Load the incid_osmm_updates table for the current incid
                    HluDataSet.incid_osmm_updatesDataTable incidOSMMUpdatesTable = (HluDataSet.incid_osmm_updatesDataTable)_viewModelMain.HluDataset.incid_osmm_updates.Copy();
                    HluDataSet.incid_osmm_updatesRow[] incidOSMMUpdatesRows = _viewModelMain.GetIncidChildRowsDb(relValues,
                        _viewModelMain.HluTableAdapterManager.incid_osmm_updatesTableAdapter, ref incidOSMMUpdatesTable);

                    // Get the osmm_xref_id
                    int incidOSMMXrefId = incidOSMMUpdatesRows[0].osmm_xref_id;

                    // Get the lut_osmm_habitat_xref row for the current osmm_xref_id
                    IEnumerable<HluDataSet.lut_osmm_habitat_xrefRow> osmmHabitatXref = from x in _viewModelMain.HluDataset.lut_osmm_habitat_xref
                                                                                       where x.is_local && x.osmm_xref_id == incidOSMMXrefId
                                                                                       select x;

                    // Continue if a row is found
                    if (osmmHabitatXref != null)
                    {
                        // Get the new primary habitat code from the lut_osmm_habitat_xref table
                        string newIncidHabitatPrimary = osmmHabitatXref.ElementAt(0).habitat_primary;

                        // Set the primary habitat on the incid table
                        _viewModelMain.IncidCurrentRow.habitat_primary = newIncidHabitatPrimary;

                        // Get the habitat class for the primary habitat
                        if (!_viewModelMain.IncidCurrentRow.Ishabitat_primaryNull())
                            _viewModelMain.IncidCurrentRow.habitat_class =
                                _viewModelMain.GetHabitatClassForPrimary(_viewModelMain.IncidCurrentRow.habitat_primary);

                        // Get the habitat version for the primary habitat class
                        if (!_viewModelMain.IncidCurrentRow.Ishabitat_primaryNull())
                            _viewModelMain.IncidCurrentRow.habitat_version =
                            _viewModelMain.GetHabitatVersionForPrimary(_viewModelMain.IncidCurrentRow.habitat_primary);

                        // Build an UPDATE statement for the incid table
                        string updateVals = String.Join(",", _viewModelMain.HluDataset.incid.Columns.Cast<DataColumn>()
                        .Where(c => c.Ordinal != _viewModelMain.HluDataset.incid.incidColumn.Ordinal &&
                            !_viewModelMain.IncidCurrentRow.IsNull(c.Ordinal))
                        .Select(c => String.Format("{0} = {1}",
                            _viewModelMain.DataBase.QuoteIdentifier(c.ColumnName),
                            _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow[c.Ordinal]))));

                        string updateStatementIncid = !incidUpdateCols.Any() ? String.Empty :
                            String.Format("UPDATE {0} SET {1}", _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName), updateVals);

                        // Clear the list of secondary habitat rows for the class.
                        SecondaryHabitat.SecondaryHabitatList = [];

                        // Switch off validation in the secondary habitat environment.
                        SecondaryHabitat.PrimarySecondaryCodeValidation = 0;

                        // Get the new secondary habitats list from the lut_osmm_habitat_xref table
                        string newIncidHabitatSecondaries = osmmHabitatXref.ElementAt(0).habitat_secondaries;

                        // If all secondary codes are to be deleted then build an update
                        // string to clear the secondary habitat summary
                        string updateHabitatSecondaries = "";
                        if (newIncidHabitatSecondaries == null)
                        {
                            _viewModelMain.IncidCurrentRow.habitat_secondaries = null;

                            updateHabitatSecondaries = String.Format(", {0} = {1}",
                                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.habitat_secondariesColumn.ColumnName),
                                _viewModelMain.DataBase.QuoteValue(_viewModelMain.IncidCurrentRow.habitat_secondaries));
                        }
                        else
                        {
                            // Split secondaries string into list of secondary codes
                            List<string> insertSecondaryStatements = [];

                            // Create new rows in the secondary table
                            try
                            {
                                // Split the list by spaces, commas or points
                                string pattern = @"\s|\.|\,";
                                Regex rgx = new(pattern);
                                string[] splitSecondaryHabitats = rgx.Split(newIncidHabitatSecondaries);

                                // Sort the list in ascending order
                                Array.Sort(splitSecondaryHabitats);

                                // Process each secondary habitat code
                                for (int i = 0; i < splitSecondaryHabitats.Length; i++)
                                {
                                    string secondaryCode = splitSecondaryHabitats[i];
                                    if (secondaryCode != null)
                                    {
                                        // Lookup the secondary group for the secondary code
                                        IEnumerable<string> q = null;
                                        q = (from s in _viewModelMain.SecondaryHabitatCodesAll
                                             where s.code == secondaryCode
                                             select s.code_group);

                                        // If the secondary group has been found
                                        string secondaryGroup = null;
                                        if ((q != null) && (q.Any()))
                                        {
                                            secondaryGroup = q.First();

                                            // Add secondary habitat to the list and table if it isn't already in the list
                                            if (SecondaryHabitat.SecondaryHabitatList == null ||
                                                !SecondaryHabitat.SecondaryHabitatList.Any(sh => sh.Secondary_habitat == secondaryCode))
                                                _viewModelMain.AddSecondaryHabitat(true, -1, currIncid, secondaryCode, secondaryGroup);
                                        }
                                    }
                                }
                            }
                            catch { }

                            // If any secondary codes were added to the collection
                            if (SecondaryHabitat.SecondaryHabitatList != null)
                            {
                                // Build a concatenated string of the secondary habitats
                                string secondaryCodeDelimiter = _addInSettings.SecondaryCodeDelimiter;
                                string secondarySummary = String.Join(secondaryCodeDelimiter, [.. SecondaryHabitat.SecondaryHabitatList.OrderBy(s => s.Secondary_habitat_int).ThenBy(s => s.Secondary_habitat).Select(s => s.Secondary_habitat).Distinct()]);

                                // Build an update string to set the secondary habitat
                                _viewModelMain.IncidCurrentRow.habitat_secondaries = secondarySummary;

                                updateHabitatSecondaries = String.Format(", {0} = {1}",
                                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.habitat_secondariesColumn.ColumnName),
                                    _viewModelMain.DataBase.QuoteValue(secondarySummary));
                            }
                        }

                        // Add the secondary habitat update to the update command
                        updateStatementIncid += updateHabitatSecondaries;

                        // Add the IHS habitat update string to the update command
                        updateStatementIncid += updateIHSHabitat;

                        // Finally, add the where clause to the update command
                        updateStatementIncid += incidWhereClause;

                        // Filter out any rows not set (because the maximum number of blank rows are
                        // created above so any not used need to be removed)
                        _viewModelMain.IncidSourcesRows = FilterUpdateRows<HluDataSet.incid_sourcesDataTable,
                            HluDataSet.incid_sourcesRow>(_viewModelMain.IncidSourcesRows);

                        // Filter out any condition rows not set (because a single null row is
                        // created so many need to be removed if it's still null)
                        _viewModelMain.IncidConditionRows = FilterUpdateRows<HluDataSet.incid_conditionDataTable,
                            HluDataSet.incid_conditionRow>(_viewModelMain.IncidConditionRows);

                        // Get the rows from the secondary habitats class as an
                        // observable collection
                        ObservableCollection<SecondaryHabitat> secondaryHabitats = _viewModelMain.IncidSecondaryHabitats;

                        // Perform the bulk updates on the data tables
                        BulkUpdateAdo(currIncid, secondaryHabitats, selectStatementIncid, updateStatementIncid, updateStatementIncidOSMMUpdates,
                            ihsMultiplexDeleteStatements, secondaryDelStatement, _bulkDeleteOrphanBapHabitats, _bulkDeletePotentialBapHabitats);
                    }
                }

                // Perform the bulk updates on the GIS data, shadow copy in DB and history
                await BulkUpdateGisAsync(incidOrdinal, _viewModelMain.IncidSelection, _bulkDeleteSecondaryCodes, _bulkCreateHistory, Operations.OSMMUpdate, nowDtTm, editOperation);

                // Commit the transaction and accept the changes ???
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                // force re-loading data from db
                _viewModelMain.IncidTable.Clear();

                success = true;
            }
            catch (Exception ex)
            {
                // Rollback the transaction
                _viewModelMain.DataBase.RollbackTransaction();

                try
                {
                    // Abort the GIS updates
                    editOperation.Abort();
                }
                catch
                {
                    // Ignore abort failures.
                }

                // Get the SQL error message (if it is one) or the exception message.
                string exMessage = DbBase.GetSqlErrorMessage(ex);

                // Show error message
                MessageBox.Show(String.Format("OSMM Bulk update failed. The error message returned was:\n\n{0}",
                    ex.Message), "HLU: OSMM Bulk Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Stop the Bulk Update mode and reset all the controls
                await OSMMBulkUpdateResetControlsAsync();
            }

            // Show success message
            if (success)
                _viewModelMain.ShowSuccess("OSMM Bulk update succeeded.", MessageCategory.BulkUpdate);
        }

        /// <summary>
        /// Cancels the OSMM Bulk Update mode.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task CancelOSMMBulkUpdateAsync()
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Stopping OSMM Bulk Update mode ...");

            // Stop the OSMM Bulk Update mode and reset all the controls
            await OSMMBulkUpdateResetControlsAsync();

            // Reset the cursor back to normal.
            _viewModelMain.ChangeCursor(Cursors.Arrow);
        }

        /// <summary>
        /// Stops the OSMM Bulk Update mode and resets all
        /// the controls to normal.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMBulkUpdateResetControlsAsync()
        {
            // Force the Incid table to be refilled because it has been
            // updated directly in the database rather than via the
            // local copy.
            _viewModelMain.RefillIncidTable = true;

            // Re-enable the tab control and individual tabs.
            _viewModelMain.TabControlDataEnabled = true;
            _viewModelMain.TabItemHabitatEnabled = true;
            _viewModelMain.TabItemIHSEnabled = true;

            // Show the history tab
            _viewModelMain.ShowHistoryTab = true;

            // Stop the OSMM Bulk Update mode
            _viewModelMain.OSMMBulkUpdateMode = false;

            // Select the habitat tab
            _viewModelMain.TabItemSelected = 0;

            // Clear the active filter.
            _viewModelMain.SuppressUserNotifications = true;
            await _viewModelMain.ClearFilterAsync(true);
            _viewModelMain.SuppressUserNotifications = false;

            // Re-read the current map selection.
            await _viewModelMain.GetMapSelectionAsync(false);
        }

        #endregion OSMM Bulk Update

        #region Database & GIS Updates

        /// <summary>
        /// Perform the bulk update on the database tables using an ADO connection.
        /// </summary>
        /// <param name="currIncid">The incid to update.</param>
        /// <param name="selectStatementIncid">The SELECT command template.</param>
        /// <param name="updateStatementIncid">The UPDATE command template.</param>
        /// <param name="deleteExistingRows">if set to <c>1</c> [delete existing rows].</param>
        /// <param name="ihsMultiplexDeleteStatements">The ihs multiplex delete statements.</param>
        /// <exception cref="Exception">
        /// Failed to delete IHS multiplex rows.
        /// or
        /// Failed to update incid table.
        /// or
        /// No database row for incid
        /// </exception>
        private void BulkUpdateAdo(string currIncid,
            ObservableCollection<SecondaryHabitat> secondaryHabitats,
            string selectStatementIncid,
            string updateStatementIncid,
            string updateStatementIncidOSMMUpdates,
            List<string> ihsMultiplexDeleteStatements,
            string secondaryDeleteStatement,
            bool bulkDeleteOrphanBapHabitats,
            bool bulkDeletePotentialBapHabitats)
        {
            // ---------------------------------------------------------------------
            // Update the incid table
            // ---------------------------------------------------------------------
            // Execute the UPDATE incid statement for the current row
            if (!String.IsNullOrEmpty(updateStatementIncid))
            {
                try
                {
                    _viewModelMain.DataBase.ExecuteNonQuery(
                        String.Format(updateStatementIncid, currIncid),
                        _viewModelMain.DataBase.Connection.ConnectionTimeout,
                        CommandType.Text);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to update table [{_viewModelMain.HluDataset.incid.TableName}].", ex);
                }
            }

            // ---------------------------------------------------------------------
            // Delete existing secondary habitat rows
            // ---------------------------------------------------------------------
            // Execute the DELETE secondary habitats statement for the current row
            if (!String.IsNullOrEmpty(secondaryDeleteStatement))
            {
                try
                {
                    _viewModelMain.DataBase.ExecuteNonQuery(
                        String.Format(secondaryDeleteStatement, currIncid),
                        _viewModelMain.DataBase.Connection.ConnectionTimeout,
                        CommandType.Text);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete secondary habitat rows in table [{_viewModelMain.HluDataset.incid_secondary.TableName}].", ex);
                }
            }

            // ---------------------------------------------------------------------
            // Delete any orphaned IHS multiplex rows
            // ---------------------------------------------------------------------
            // Execute the DELETE incid_ihs_xxx statements for the current row
            if (ihsMultiplexDeleteStatements != null)
            {
                foreach (string s in ihsMultiplexDeleteStatements)
                    try
                    {
                        _viewModelMain.DataBase.ExecuteNonQuery(
                            String.Format(s, currIncid),
                            _viewModelMain.DataBase.Connection.ConnectionTimeout,
                            CommandType.Text);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to delete IHS multiplex rows.", ex);
                    }
            }

            // ---------------------------------------------------------------------
            // Update the incid_osmm_updates table
            // ---------------------------------------------------------------------
            // Execute the UPDATE incid_osmm_updates statement for the current row
            if (!String.IsNullOrEmpty(updateStatementIncidOSMMUpdates))
            {
                try
                {
                    _viewModelMain.DataBase.ExecuteNonQuery(
                        String.Format(updateStatementIncidOSMMUpdates, currIncid),
                        _viewModelMain.DataBase.Connection.ConnectionTimeout,
                        CommandType.Text);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to update OSMM update rows in table [{_viewModelMain.HluDataset.incid_osmm_updates.TableName}].", ex);
                }
            }

            // Create an array of the value of the current incid
            object[] relValues = [currIncid];

            // ---------------------------------------------------------------------
            // Retrieve the primary habitat
            // ---------------------------------------------------------------------
            // Execute the SELECT statement to get the primary habitat code
            object retValue = _viewModelMain.DataBase.ExecuteScalar(String.Format(selectStatementIncid, currIncid),
                _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text);

            if (retValue == null)
                throw new Exception($"No database row for incid '{currIncid}'");

            // Set the primary habitat code
            string primaryHabitat = retValue.ToString();

            // ---------------------------------------------------------------------
            // Update the secondary habitats
            // ---------------------------------------------------------------------
            // Store a copy of the table for the current incid
            HluDataSet.incid_secondaryDataTable secondaryTable =
                (HluDataSet.incid_secondaryDataTable)_viewModelMain.HluDataset.incid_secondary.Clone();
            // Load the child rows for the secondary habitat table for the supplied incid
            _viewModelMain.GetIncidChildRowsDb(relValues,
                _viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter, ref secondaryTable);
            // Update the rows in the database
            BulkUpdateSecondary(currIncid, primaryHabitat, secondaryTable, secondaryHabitats);

            // ---------------------------------------------------------------------
            // Update the BAP habitats
            // ---------------------------------------------------------------------
            // Store a copy of the table for the current incid
            HluDataSet.incid_bapDataTable bapTable =
                (HluDataSet.incid_bapDataTable)_viewModelMain.HluDataset.incid_bap.Clone();
            // Load the child rows for the bap table for the supplied incid
            _viewModelMain.GetIncidChildRowsDb(relValues,
                _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter, ref bapTable);
            // Update the rows in the database
            BulkUpdateBap(currIncid, primaryHabitat, bapTable, secondaryHabitats, bulkDeleteOrphanBapHabitats, bulkDeletePotentialBapHabitats);

            // ---------------------------------------------------------------------
            // Update the conditions
            // ---------------------------------------------------------------------
            // Store the row from the user interface
            HluDataSet.incid_conditionRow[] incidConditionRows = _viewModelMain.IncidConditionRows;
            // Store a copy of the table for the current incid
            HluDataSet.incid_conditionDataTable incidConditionTable =
                (HluDataSet.incid_conditionDataTable)_viewModelMain.HluDataset.incid_condition.Clone();

            // Count the non-blank rows from the user interface
            int newConditionRows = (from nc in incidConditionRows
                                    where nc.condition != null
                                    select nc).Count();

            // If there are new condition rows then delete the old conditions
            bool deleteCondition = newConditionRows > 0;
            // Update the rows in the database
            BulkUpdateAdoIncidRelatedTable(deleteCondition, currIncid, relValues,
                _viewModelMain.HluTableAdapterManager.incid_conditionTableAdapter,
                incidConditionTable, ref incidConditionRows);

            // ---------------------------------------------------------------------
            // Update the sources
            // ---------------------------------------------------------------------
            // Store the rows from the user interface
            HluDataSet.incid_sourcesRow[] incidSourcesRows = _viewModelMain.IncidSourcesRows;
            // Store a copy of the table for the current incid
            HluDataSet.incid_sourcesDataTable incidSourcesTable =
                (HluDataSet.incid_sourcesDataTable)_viewModelMain.HluDataset.incid_sources.Clone();

            // Count the non-blank rows from the user interface
            int newSourceRows = (from ns in incidSourcesRows
                                 where ns.source_id != Int32.MinValue
                                 select ns).Count();

            // If there are new source rows then delete the old sources
            bool deleteSources = newSourceRows > 0;
            // Update the rows in the database
            BulkUpdateAdoIncidRelatedTable(deleteSources, currIncid, relValues,
                _viewModelMain.HluTableAdapterManager.incid_sourcesTableAdapter,
                incidSourcesTable, ref incidSourcesRows);
        }

        /// <summary>
        /// Bulk updates the incid related tables using an ADO connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <param name="deleteExistingRows">if set to <c>1</c> [delete existing rows].</param>
        /// <param name="currIncid">The current incid.</param>
        /// <param name="relValues">An array of the current incid.</param>
        /// <param name="adapter">The table adapter.</param>
        /// <param name="dbTable">The database table to update.</param>
        /// <param name="uiRows">The user interface rows.</param>
        private void BulkUpdateAdoIncidRelatedTable<T, R>(bool deleteExistingRows, string currIncid,
            object[] relValues, HluTableAdapter<T, R> adapter, T dbTable, ref R[] uiRows)
            where T : DataTable, new()
            where R : DataRow
        {
            if ((uiRows != null) && (uiRows.Any(rm =>
                !rm.IsNull(dbTable.PrimaryKey[0].Ordinal))))
            {
                // Create a cloned set of rows for the supplied incid
                T newRows = CloneUpdateRows<T, R>(uiRows, currIncid);

                // Load the child rows for the required data table for the supplied incid
                _viewModelMain.GetIncidChildRowsDb(relValues, adapter, ref dbTable);

                // Update the child rows for the supplied incid to match the cloned rows
                BulkUpdateAdoChildTable<T, R>(deleteExistingRows, [.. newRows.AsEnumerable().Cast<R>()], ref dbTable, adapter);

                uiRows = [.. newRows.AsEnumerable().Cast<R>()];
            }
        }

        /// <summary>
        /// Filters out any rows not set (because the maximum number of blank
        /// rows are created at the start of the bulk process so any not used
        /// need to be removed)
        /// </summary>
        /// <typeparam name="T">The type of the data table.</typeparam>
        /// <typeparam name="R">The type of the data row.</typeparam>
        /// <param name="rows">The rows.</param>
        /// <returns>The filtered rows.</returns>
        private R[] FilterUpdateRows<T, R>(R[] rows)
            where T : DataTable
            where R : DataRow
        {
            List<R> newRows = new(rows.Length);

            if ((rows == null) || (rows.Length == 0) || (rows[0] == null))
                return [.. newRows];

            T table = (T)rows[0].Table;

            var q = from rel in table.ParentRelations.Cast<DataRelation>()
                    where rel.ParentTable.TableName.StartsWith("lut_", StringComparison.CurrentCultureIgnoreCase) &&
                          rel.ParentColumns.Length == 1
                    select rel;

            var lookup = (from c in table.Columns.Cast<DataColumn>()
                          let p = from rel in q
                                  where rel.ChildColumns.Length == 1 && rel.ChildColumns[0].Ordinal == c.Ordinal
                                  select rel.ParentColumns[0]
                          select new
                          {
                              ChildColumnOrdinal = c.Ordinal,
                              ParentColumnOrdinal = p.Any() ? p.ElementAt(0).Ordinal : -1,
                              ParentTable = p.Any() ? p.ElementAt(0).Table.TableName : String.Empty
                          }).ToArray();

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null)
                    continue;
                bool add = true;
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    if ((lookup[j].ParentColumnOrdinal != -1) && (!_viewModelMain.HluDataset.Tables[lookup[j].ParentTable]
                        .AsEnumerable().Any(r => r[lookup[j].ParentColumnOrdinal].Equals(rows[i][j]))))
                    {
                        add = false;
                        break;
                    }
                }
                if (add)
                    newRows.Add(rows[i]);
            }

            return [.. newRows];
        }

        /// <summary>
        /// Clones the rows in a data table, changing the incid column in the
        /// cloned rows to match the supplied incid value.
        /// </summary>
        /// <typeparam name="T">The type of the data table.</typeparam>
        /// <typeparam name="R">The type of the data row.</typeparam>
        /// <param name="rows">The rows to be cloned.</param>
        /// <param name="incid">The incid value to be set in the cloned rows.</param>
        /// <returns>The cloned rows with the updated incid value.</returns>
        private T CloneUpdateRows<T, R>(R[] rows, string incid)
            where T : DataTable, new()
            where R : DataRow
        {
            T newRows = new();
            int incidOrdinal = newRows.Columns[_viewModelMain.HluDataset.incid.incidColumn.ColumnName].Ordinal;

            for (int i = 0; i < rows.Length; i++)
            {
                object[] itemArray = new object[rows[i].ItemArray.Length];
                Array.Copy(rows[i].ItemArray, itemArray, itemArray.Length);
                itemArray[incidOrdinal] = incid;
                R rm = (R)newRows.NewRow();
                rm.ItemArray = itemArray;
                newRows.Rows.Add(rm);
            }

            return newRows;
        }

        /// <summary>
        /// Uses the secondary code to identify existing records and distinguish
        /// them from new ones to be inserted.
        /// </summary>
        /// <param name="currIncid">The current incid.</param>
        /// <param name="primaryHabitat">The primary habitat.</param>
        /// <param name="incidSecondaryTable">The current incid secondary table in the database.</param>
        /// <param name="secondaryHabitats">The secondary habitats from the user interface.</param>
        /// <param name="deleteSecondaryRows">if set to <c>true</c> delete any existing secondary habitat rows.</param>
        /// <exception cref="Exception"></exception>
        private void BulkUpdateSecondary(string currIncid,
            string primaryHabitat,
            HluDataSet.incid_secondaryDataTable incidSecondaryTable,
            ObservableCollection<SecondaryHabitat> secondaryHabitats)
        {
            // Loop through all the secondary habitats from the user interface
            foreach (SecondaryHabitat sh in secondaryHabitats)
            {
                // Get any rows for the current secondary habitat already in the database
                IEnumerable<HluDataSet.incid_secondaryRow> dbRows =
                    incidSecondaryTable.Where(r => r.secondary == sh.Secondary_habitat);

                // Count how many rows match the current secondary code
                switch (dbRows.Count())
                {
                    // If the current secondary habitat is not already in the database
                    case 0: // Insert the newly added secondary habitat
                        HluDataSet.incid_secondaryRow newRow = _viewModelMain.IncidSecondaryTable.Newincid_secondaryRow();
                        newRow.ItemArray = sh.ToItemArray(_viewModelMain.RecIDs.NextIncidSecondaryId, currIncid);
                        if (sh.IsValid(false, newRow)) // reset Bulk Update mode for full validation of a new row
                            _viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter.Insert(newRow);
                        break;
                    // If the current secondary habitat is already in the database
                    // then ignore it as there are no columns to update
                    case 1: // Nothing to update in the row
                        break;

                    default: // impossible if rules properly enforced
                        break;
                }
            }
        }

        /// <summary>
        /// Uses the bap_habitat code to identify existing records and distinguish
        /// them from new ones to be inserted.
        /// </summary>
        /// <param name="currIncid">The current incid.</param>
        /// <param name="primaryHabitat">The primary habitat.</param>
        /// <param name="incidBapTable">The current incid bap table in the database.</param>
        /// <param name="secondaryHabitats">The secondary habitats from the user interface.</param>
        /// <param name="deleteOrphanBapRows">if set to <c>true</c> delete any orphan primary bap rows.</param>
        /// <param name="deletePotentialBapRows">if set to <c>true</c> delete any potential bap rows.</param>
        /// <exception cref="Exception"></exception>
        private void BulkUpdateBap(string currIncid,
            string primaryHabitat,
            HluDataSet.incid_bapDataTable incidBapTable,
            ObservableCollection<SecondaryHabitat> secondaryHabitats,
            bool deleteOrphanBapRows,
            bool deletePotentialBapRows)
        {
            // Get a list of all the primary (mandatory) bap habitats
            IEnumerable<string> mandatoryBap = _viewModelMain.MandatoryBapEnvironments(primaryHabitat,
                secondaryHabitats);

            // Get an array of the columns not be updated in the table
            int[] skipOrdinals = [
                _viewModelMain.HluDataset.incid_bap.bap_idColumn.Ordinal,
                _viewModelMain.HluDataset.incid_bap.incidColumn.Ordinal,
                _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.Ordinal ];

            List<HluDataSet.incid_bapRow> updateRows = [];
            HluDataSet.incid_bapRow updateRow;

            // Get all the BAP habitats from the user interface
            IEnumerable<BapEnvironment> beUI = from b in _viewModelMain.IncidBapRowsAuto.Concat(_viewModelMain.IncidBapRowsUser)
                                               group b by b.Bap_habitat into habs
                                               select habs.First();

            // Loop through all the BAP habitats from the user interface
            foreach (BapEnvironment be in beUI)
            {
                // Check if the current BAP habitat is primary (mandatory) or secondary
                bool isSecondary = !mandatoryBap.Contains(be.Bap_habitat);

                // Flag the current BAP habitat as secondary
                if (isSecondary)
                    be.MakeSecondary();

                // Get any rows for the current BAP habitat already in the database
                IEnumerable<HluDataSet.incid_bapRow> dbRows =
                    incidBapTable.Where(r => r.bap_habitat == be.Bap_habitat);

                // Count how many rows match the current BAP habitat
                switch (dbRows.Count())
                {
                    // If the current BAP habitat is not already in the database
                    case 0: // Insert the newly added BAP habitat
                        HluDataSet.incid_bapRow newRow = _viewModelMain.IncidBapTable.Newincid_bapRow();
                        newRow.ItemArray = be.ToItemArray(_viewModelMain.RecIDs.NextIncidBapId, currIncid);
                        if (be.IsValid(false, isSecondary, newRow)) // reset Bulk Update mode for full validation of a new row
                            _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Insert(newRow);
                        break;
                    // If the current BAP habitat is already in the database
                    case 1: // Update the existing row
                        updateRow = dbRows.ElementAt(0);
                        object[] itemArray = be.ToItemArray();
                        for (int i = 0; i < itemArray.Length; i++)
                        {
                            if ((itemArray[i] != null) && (Array.IndexOf(skipOrdinals, i) == -1))
                                updateRow[i] = itemArray[i];
                        }
                        updateRows.Add(updateRow);
                        break;

                    default: // impossible if rules properly enforced
                        break;
                }
            }

            // Delete any previously primary BAP environments from the database
            if (deleteOrphanBapRows)
            {
                var delRows = incidBapTable.Where(r => !mandatoryBap.Contains(r.bap_habitat)
                    && BapEnvironment.IsSecondary(r) == false);
                foreach (HluDataSet.incid_bapRow r in delRows)
                    _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Delete(r);
            }
            else
            {
                // Change any existing rows in the database that are no longer primary
                // (mandatory) BAP habitats to secondary
                incidBapTable.Where(r => !mandatoryBap.Contains(r.bap_habitat)
                    && BapEnvironment.IsSecondary(r) == false).ToList().ForEach(delegate (HluDataSet.incid_bapRow r)
                {
                    updateRows.Add(BapEnvironment.MakeSecondary(r));
                });
            }

            // Determine if there are any primary BAP habitats that aren't
            // in the user interface (they must have come from an OSMM
            // bulk update)
            var newBap = from p in mandatoryBap
                         where !beUI.Any(row => row.Bap_habitat == p)
                         select new BapEnvironment(false, false, -1, currIncid, p, _bulkDeterminationQuality, _bulkInterpretationQuality, "Based on OSMM Update");

            // Insert any new primary BAP environments that aren't
            // in the user interface
            foreach (BapEnvironment be in newBap)
            {
                // Get any rows for the current BAP habitat already in the database
                IEnumerable<HluDataSet.incid_bapRow> dbRows =
                    incidBapTable.Where(r => r.bap_habitat == be.Bap_habitat);

                switch (dbRows.Count())
                {
                    // If the current BAP habitat is not already in the database
                    case 0: // Insert the newly added BAP habitat
                        HluDataSet.incid_bapRow newRow = _viewModelMain.IncidBapTable.Newincid_bapRow();
                        newRow.ItemArray = be.ToItemArray(_viewModelMain.RecIDs.NextIncidBapId, currIncid);
                        if (be.IsValid(false, false, newRow)) // reset Bulk Update mode for full validation of a new row
                            _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Insert(newRow);
                        break;
                    // If the current BAP habitat is already in the database
                    case 1: // Update the existing row
                        updateRow = dbRows.ElementAt(0);
                        object[] itemArray = be.ToItemArray();
                        for (int i = 0; i < itemArray.Length; i++)
                        {
                            if ((itemArray[i] != null) && (Array.IndexOf(skipOrdinals, i) == -1))
                                updateRow[i] = itemArray[i];
                        }
                        updateRows.Add(updateRow);
                        break;

                    default: // impossible if rules properly enforced
                        break;
                }
            }

            // Update the BAP habitat if there are any rows to update
            if (updateRows.Count > 0)
            {
                if (_viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Update(updateRows.ToArray()) == -1)
                    throw new Exception($"Failed to update [{_viewModelMain.HluDataset.incid_bap.TableName}] table.");
            }

            // Delete any previously secondary BAP environments from the database
            // if they are not in the user interface
            if (deletePotentialBapRows)
            {
                var delRows = incidBapTable.Where(r => !mandatoryBap.Contains(r.bap_habitat) &&
                    BapEnvironment.IsSecondary(r) == true &&
                    !_viewModelMain.IncidBapRowsUser.Any(be => be.Bap_habitat == r.bap_habitat));
                foreach (HluDataSet.incid_bapRow r in delRows)
                    _viewModelMain.HluTableAdapterManager.incid_bapTableAdapter.Delete(r);
            }
        }

        /// <summary>
        /// Update the GIS layer.
        /// </summary>
        /// <param name="incidOrdinal">The incid ordinal.</param>
        /// <param name="incidSelection">The incid selection.</param>
        /// <exception cref="Exception">
        /// Failed to update GIS layer shadow copy
        /// or
        /// Failed to update GIS layer for incid
        /// </exception>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task BulkUpdateGisAsync(int incidOrdinal, DataTable incidSelection, bool deleteSecondaryCodes, bool createHistory, Operations operation, DateTime nowDtTm, EditOperation editOperation)
        {
            // Get the columns and values to be updated in GIS
            DataColumn[] updateColumns;
            object[] updateDBValues;
            object[] updateGISValues;
            BulkUpdateGisColumns(deleteSecondaryCodes, out updateColumns, out updateDBValues, out updateGISValues);

            List<List<SqlFilterCondition>> incidWhereClause;
            DataTable historyTable = null;

            // If there are no columns to update then just create the history
            // rows from the GIS layer.
            if ((updateColumns == null) || (updateColumns.Length == 0))
            {
                // if history is to be created
                if (createHistory)
                {
                    // Build a WHERE clause for all the incids in the DB shadow copy of GIS layer
                    incidWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(ViewModelWindowMain.IncidPageSize,
                        _viewModelMain.HluDataset.incid_mm_polygons.incidColumn.Ordinal, _viewModelMain.HluDataset.incid_mm_polygons,
                        incidSelection.AsEnumerable().Select(r => r.Field<string>(incidOrdinal)));

                    // Build a history table for every incid in the DB shadow copy of GIS layer
                    foreach (List<SqlFilterCondition> w in incidWhereClause)
                    {
                        // Retrieve all the GIS rows for the current incid
                        DataTable historyTmp = _viewModelMain.GISApplication.SqlSelect(false, true, _viewModelMain.HistoryColumns, w);

                        // Append history rows to the history table
                        if (historyTmp != null)
                        {
                            if (historyTable == null)
                            {
                                historyTable = historyTmp;
                            }
                            else
                            {
                                foreach (DataRow r in historyTmp.Rows)
                                    historyTable.ImportRow(r);
                            }
                        }
                    }

                    // Write history — no GIS edit operation is involved in this branch,
                    // so the throw from HistoryWrite will propagate cleanly to the caller.
                    if (historyTable != null)
                    {
                        ViewModelWindowMainHistory vmHist = new(_viewModelMain);
                        vmHist.HistoryWrite(null, historyTable, operation, nowDtTm);
                    }
                }
            }
            // Otherwise, update the GIS layer and DB shadow layer and
            // then create the history rows from the GIS layer.
            else
            {
                // Build an UPDATE statement for the DB shadow copy of GIS layer
                string updateVals = String.Join(", ",
                    updateColumns.Select((c, index) => String.Format("{0} = {1}",
                        _viewModelMain.DataBase.QuoteIdentifier(c.ColumnName),
                        _viewModelMain.DataBase.QuoteValue(updateDBValues[index]))));

                string incidMMPolygonsUpdateCmdTemplate = String.Format(
                    "UPDATE {0} SET {1} WHERE {2}",
                    _viewModelMain.DataBase.QualifyTableName(
                        _viewModelMain.HluDataset.incid_mm_polygons.TableName),
                    updateVals, "{0}");

                // Build a WHERE clause for the rows to update in the DB shadow copy of GIS layer
                incidWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(1,
                    _viewModelMain.HluDataset.incid_mm_polygons.incidColumn.Ordinal, _viewModelMain.HluDataset.incid_mm_polygons,
                    incidSelection.AsEnumerable().Select(r => r.Field<string>(incidOrdinal)));

                // Execute the UPDATE statement for each incid in the DB shadow copy of GIS layer
                foreach (List<SqlFilterCondition> whereClause in incidWhereClause)
                {
                    try
                    {
                        _viewModelMain.DataBase.ExecuteNonQuery(
                            String.Format(incidMMPolygonsUpdateCmdTemplate,
                                _viewModelMain.DataBase.WhereClause(false, true, true, whereClause)),
                            _viewModelMain.DataBase.Connection.ConnectionTimeout,
                            CommandType.Text);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to update GIS layer shadow copy in table [{_viewModelMain.HluDataset.incid_mm_polygons.TableName}].", ex);
                    }

                    // Get the current values from the GIS layer
                    DataTable historyTmp = await _viewModelMain.GISApplication.GetHistoryAsync(
                        _viewModelMain.HistoryColumns, whereClause);

                    if (historyTmp == null)
                        throw new Exception($"Failed to retrieve GIS history for incid '{whereClause[0].Value}'");

                    // Queue the GIS feature updates onto the shared edit operation
                    await _viewModelMain.GISApplication.UpdateFeaturesAsync(updateColumns,
                        updateGISValues, _viewModelMain.HistoryColumns, whereClause, editOperation);

                    // Append history rows to the history table
                    if (createHistory)
                    {
                        if (historyTable == null)
                        {
                            historyTable = historyTmp;
                        }
                        else
                        {
                            foreach (DataRow r in historyTmp.Rows)
                                historyTable.ImportRow(r);
                        }
                    }
                }

                // Write history for the affected incids before executing the GIS edit
                // operation, so that a history failure aborts the GIS edits and keeps
                // the database and GIS layer in sync (matching the standard update path).
                if (createHistory && (historyTable != null))
                {
                    ViewModelWindowMainHistory vmHist = new(_viewModelMain);
                    vmHist.HistoryWrite(null, historyTable, operation, nowDtTm);
                }

                // Execute (commit) all queued GIS feature updates in one operation
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (String.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";

                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                // Save edits to clear the pending edit state
                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");
            }
        }

        /// <summary>
        /// Set the columns and values to be updated in GIS.
        /// </summary>
        /// <param name="deleteSecondaryCodes">if set to <c>true</c> delete any existing secondary habitat codes.</param>
        /// <param name="updateColumns">The columns to update.</param>
        /// <param name="updateDBValues">The values to update in the DB shadow copy of GIS layer.</param>
        /// <param name="updateGISValues">The values to update in the GIS layer.</param>
        private void BulkUpdateGisColumns(bool deleteSecondaryCodes, out DataColumn[] updateColumns, out object[] updateDBValues, out object[] updateGISValues)
        {
            List<DataColumn> updateColumnList = [];
            List<object> updateDBValueList = [];
            List<object> updateGISValueList = [];

            // Check if a new primary habitat has been set
            if (!_viewModelMain.IncidCurrentRow.Ishabitat_primaryNull())
            {
                // Add the primary habitat column and value
                updateColumnList.Add(_viewModelMain.HluDataset.incid_mm_polygons.habprimaryColumn);
                updateDBValueList.Add(_viewModelMain.IncidCurrentRow.habitat_primary);
                updateGISValueList.Add(_viewModelMain.IncidCurrentRow.habitat_primary);
            }

            // Check if new secondary habitats have been set or
            // existing secondary codes are to be deleted
            if (!_viewModelMain.IncidCurrentRow.Ishabitat_secondariesNull() || deleteSecondaryCodes)
            {
                // Add the secondary habitat column
                updateColumnList.Add(_viewModelMain.HluDataset.incid_mm_polygons.habsecondColumn);

                // Add the secondary habitat value
                if (_viewModelMain.IncidCurrentRow.Ishabitat_secondariesNull())
                {
                    updateDBValueList.Add(null);
                    updateGISValueList.Add("");
                }
                else
                {
                    updateDBValueList.Add(_viewModelMain.IncidCurrentRow.habitat_secondaries);
                    updateGISValueList.Add(_viewModelMain.IncidCurrentRow.habitat_secondaries);
                }
            }

            // Check if a new determination quality has been set
            if (!_viewModelMain.IncidCurrentRow.Isquality_determinationNull())
            {
                // Add the determination quality column and value
                updateColumnList.Add(_viewModelMain.HluDataset.incid_mm_polygons.determqtyColumn);
                updateDBValueList.Add(_viewModelMain.IncidCurrentRow.quality_determination);
                updateGISValueList.Add(_viewModelMain.IncidCurrentRow.quality_determination);
            }

            // Check if a new interpretation quality has been set
            if (!_viewModelMain.IncidCurrentRow.Isquality_interpretationNull())
            {
                // Add the interpretation quality column and value
                updateColumnList.Add(_viewModelMain.HluDataset.incid_mm_polygons.interpqtyColumn);
                updateDBValueList.Add(_viewModelMain.IncidCurrentRow.quality_interpretation);
                updateGISValueList.Add(_viewModelMain.IncidCurrentRow.quality_interpretation);
            }

            // Add any other history columns to be updated
            var addCols = _viewModelMain.HistoryColumns.Where(h =>
                _viewModelMain.HluDataset.incid_mm_polygons.Columns.Contains(h.ColumnName) &&
                _viewModelMain.IncidCurrentRow.Table.Columns.Contains(h.ColumnName) &&
                !_viewModelMain.IncidCurrentRow.IsNull(h.ColumnName));

            if (addCols.Any())
            {
                updateColumnList.AddRange(addCols);
                updateDBValueList.AddRange(_viewModelMain.HistoryColumns.Where(h =>
                    _viewModelMain.HluDataset.incid_mm_polygons.Columns.Contains(h.ColumnName) &&
                    _viewModelMain.IncidCurrentRow.Table.Columns.Contains(h.ColumnName) &&
                    !_viewModelMain.IncidCurrentRow.IsNull(h.ColumnName))
                    .Select(h => _viewModelMain.IncidCurrentRow[h.ColumnName]));
                updateGISValueList.AddRange(_viewModelMain.HistoryColumns.Where(h =>
                    _viewModelMain.HluDataset.incid_mm_polygons.Columns.Contains(h.ColumnName) &&
                    _viewModelMain.IncidCurrentRow.Table.Columns.Contains(h.ColumnName) &&
                    !_viewModelMain.IncidCurrentRow.IsNull(h.ColumnName))
                    .Select(h => _viewModelMain.IncidCurrentRow[h.ColumnName]));
            }

            // Set the output arrays
            updateColumns = [.. updateColumnList];
            updateDBValues = [.. updateDBValueList];
            updateGISValues = [.. updateGISValueList];
        }

        /// <summary>
        /// Bulk updates the required child table using an ADO connection.
        /// </summary>
        /// <typeparam name="T">The type of the data table.</typeparam>
        /// <typeparam name="R">The type of the data row.</typeparam>
        /// <param name="deleteExistingRows">if set to <c>1</c> delete any existing rows.</param>
        /// <param name="newRows">The new rows to be inserted.</param>
        /// <param name="dbRows">The database rows.</param>
        /// <exception cref="ArgumentException">
        /// dbRows
        /// or
        /// newRows
        /// or
        /// Table must have a single column primary key of type Int32 - dbRows
        /// </exception>
        /// <exception cref="Exception">
        /// </exception>
        private void BulkUpdateAdoChildTable<T, R>(bool deleteExistingRows, R[] newRows, ref T dbRows,
            HluTableAdapter<T, R> adapter)
            where T : DataTable, new()
            where R : DataRow
        {
            if (dbRows == null)
                throw new("dbRows");
            if (newRows == null)
                throw new("newRows");
            if (adapter == null)
                throw new("adapter");

            // Check the data table primary key is an integer
            if ((dbRows.PrimaryKey.Length != 1) || (dbRows.PrimaryKey[0].DataType != typeof(Int32)))
                throw new ArgumentException("Table must have a single column primary key of type Int32", nameof(dbRows));

            // Get the primary key column ordinal
            int pkOrdinal = dbRows.PrimaryKey[0].Ordinal;

            // Create an enumerable of all the target data table rows
            R[] dbRowsEnum = [.. dbRows.AsEnumerable().Select(r => (R)r)];

            // Get an array of all the columns in the target data table (except the primary key)
            DataColumn[] cols = [.. dbRows.Columns.Cast<DataColumn>().Where(c => c.Ordinal != pkOrdinal)];

            // Select only new rows with non-blank data or non-duplicate data
            R[] newRowsNoDups = [.. (from nr in newRows where cols.Any(col => !nr.IsNull(col.Ordinal)) && !dbRowsEnum.Any(dr => !cols.Any(c => !dr[c.Ordinal].Equals(nr[c.Ordinal]))) select nr).OrderBy(r => r[pkOrdinal])];

            // Get the number of non-blank row with non-duplicate data corresponding to child table dbRows
            int numRowsNew = newRowsNoDups.Length;

            // Exit if no existing rows are to be retained and there are no new rows to add
            if ((deleteExistingRows) && (numRowsNew == 0))
                return;

            // Select only existing data table rows not in the new rows
            R[] oldRows = [.. (from dr in dbRowsEnum where cols.Any(col => !dr.IsNull(col.Ordinal)) && !newRows.Any(nr => cols.Count(c => nr[c.Ordinal].Equals(dr[c.Ordinal])) == cols.Length) select dr).OrderBy(r => r[pkOrdinal])];

            // Delete all the existing database rows
            System.Array.ForEach([.. dbRowsEnum],
                new Action<R>(r => adapter.Delete(r)));

            // Set the property name for the primary key
            // Set the property name for the primary key
            string recordIdPropertyName = String.Concat(dbRows.TableName.Split('_')
                .Select(s => String.Format("{0}{1}", char.ToUpper(s[0]), s.Substring(1))))
                .Insert(0, "Next") + "Id";

            // Set the property info for the primary key property name
            PropertyInfo recordIDPropInfo = typeof(RecordIds).GetProperty(recordIdPropertyName);

            // Insert any new rows into the data table
            int pkMax = -1;
            foreach (R newRow in newRows)
            {
                // Store the new row primary key
                int pkValue = (int)newRow[pkOrdinal];
                pkMax = pkValue;

                // Set the primary key to the next value
                if (!dbRows.Columns[pkOrdinal].AutoIncrement)
                    newRow[pkOrdinal] = (int)recordIDPropInfo.GetValue(_viewModelMain.RecIDs, null);

                // Insert the new row
                adapter.Insert(newRow);

                // Restore the new row primary key
                newRow[pkOrdinal] = pkValue;
            }

            // Get the maximum number of child rows for the current table
            int maxRowsDb = 0;
            switch (dbRows.TableName.ToLower())
            {
                case "incid_condition":
                    maxRowsDb = 1;
                    break;

                case "incid_ihs_matrix":
                    maxRowsDb = 3;
                    break;

                case "incid_ihs_formation":
                    maxRowsDb = 2;
                    break;

                case "incid_ihs_management":
                    maxRowsDb = 2;
                    break;

                case "incid_ihs_complex":
                    maxRowsDb = 2;
                    break;

                case "incid_sources":
                    maxRowsDb = 3;
                    break;
            }

            // Re-insert any old rows not in the new rows
            if (!deleteExistingRows)
            {
                foreach (R oldRow in oldRows)
                {
                    // Increment the primary key integer
                    pkMax += 1;

                    // If there are still spare rows
                    if (pkMax < maxRowsDb)
                    {
                        // Store the new row primary key
                        int pkValue = (int)oldRow[pkOrdinal];

                        // Set the primary key to the next value
                        if (!dbRows.Columns[pkOrdinal].AutoIncrement)
                            oldRow[pkOrdinal] = (int)recordIDPropInfo.GetValue(_viewModelMain.RecIDs, null);
                        else
                            oldRow[pkOrdinal] = pkMax;

                        // Insert the new row
                        adapter.Insert(oldRow);

                        // Restore the new row primary key
                        oldRow[pkOrdinal] = pkValue;
                    }
                }
            }

            // Accept any changes outstanding for the table
            if (pkMax >= 0)
            {
                if (newRows.Length != 0)
                    newRows[0].Table.AcceptChanges();
                else
                    oldRows[0].Table.AcceptChanges();
            }
        }

        #endregion Database & GIS Updates
    }
}