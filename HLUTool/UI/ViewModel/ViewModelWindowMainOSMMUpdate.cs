// HLUTool is used to view and maintain habitat and land use GIS data.
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
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HLU.Data;
using HLU.Data.Model;
using HLU.Data.Model.HluDataSetTableAdapters;
using HLU.GISApplication;
using HLU.Properties;

namespace HLU.UI.ViewModel
{
    //---------------------------------------------------------------------
    // CHANGED: CR49 Process proposed OSMM Updates
    // Functionality to process proposed OSMM Updates.
    //
    class ViewModelWindowMainOSMMUpdate
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;

        #endregion Fields

        #region Constructor

        public ViewModelWindowMainOSMMUpdate(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region OSMM Update

        /// <summary>
        /// Initiates the OSMM Update mode, disabling all tabs and refreshing the user interface.
        /// </summary>
        /// <remarks>This method sets the application into OSMM Update mode by enabling the corresponding
        /// mode flag,  disabling controls across all tabs, and refreshing the user interface to reflect the updated
        /// state.  It is intended to prepare the application for operations specific to OSMM updates.</remarks>
        public void StartOSMMUpdate()
        {
            // Start the OSMM Update mode
            _viewModelMain.OSMMUpdateMode = true;

            // Disable all the tabs
            _viewModelMain.TabHabitatControlsEnabled = false;
            _viewModelMain.TabIhsControlsEnabled = false;
            _viewModelMain.TabDetailsControlsEnabled = false;
            _viewModelMain.TabSourcesControlsEnabled = false;

            // Refresh all the controls
            _viewModelMain.RefreshAll();

            // Open the OSMM Update filter
            //---------------------------------------------------------------------
            // FIX: 101 Enable get map selection when in OSMM update mode.
            //
            //_viewModelMain.OpenWindowQueryOSMM(true);
            //---------------------------------------------------------------------
        }

        /// <summary>
        /// Writes changes made to current incid_osmm_updates back to database.
        /// </summary>
        /// <remarks>This method updates the <c>last_modified_date</c> and <c>last_modified_user_id</c>
        /// fields of the OSMM record to the current date and time (rounded to the nearest second) and the current user
        /// ID, respectively. The status field is updated based on the <paramref name="updateStatus"/> parameter.
        /// Changes are committed to the database within a transaction. If an error occurs, the transaction is rolled
        /// back, and an error message is displayed.</remarks>
        /// <param name="updateStatus">Specifies the update action to perform. Valid values are: <list type="bullet"> <item><description><c>1</c>:
        /// Skip the update and increment the status if it is greater than 0.</description></item>
        /// <item><description><c>0</c>: Accept the update and reset the status to 0.</description></item>
        /// <item><description><c>-99</c>: Reject the update and set the status to -99.</description></item> </list></param>
        /// <returns><see langword="true"/> if the update operation completes successfully; otherwise, <see langword="false"/>.</returns>
        internal bool OSMMUpdate(int updateStatus)
        {
            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            try
            {
                _viewModelMain.ChangeCursor(Cursors.Wait, "Updating ...");

                // Only update DateTime fields to whole seconds.
                // Fractions of a second can cause rounding differences when
                // comparing DateTime fields later in some databases.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);
                //---------------------------------------------------------------------
                _viewModelMain.IncidOSMMUpdatesRows[0].last_modified_date = nowDtTm;
                _viewModelMain.IncidOSMMUpdatesRows[0].last_modified_user_id = _viewModelMain.UserID;

                // Determine the status flag as required
                int newStatus = _viewModelMain.IncidOSMMUpdatesRows[0].status;
                switch (updateStatus)
                {
                    case 1:     // Skip update
                        if (newStatus > 0)
                            newStatus += 1;
                        break;
                    case 0:    // Accept update
                        newStatus = 0;
                        break;
                    case -99:   // Reject update
                        newStatus = -99;
                        break;
                }

                // Set the status flag
                _viewModelMain.IncidOSMMUpdatesRows[0].status = newStatus;

                // Update the incid_osmm_updates table
                if (_viewModelMain.HluTableAdapterManager.incid_osmm_updatesTableAdapter.Update(
                    (HluDataSet.incid_osmm_updatesDataTable)_viewModelMain.HluDataset.incid_osmm_updates.GetChanges()) == -1)
                    throw new Exception(String.Format("Failed to update '{0}' table.",
                        _viewModelMain.HluDataset.incid_osmm_updates.TableName));

                // Commit the changes
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();
                _viewModelMain.Saved = true;

                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                //
                //// Move to the next Incid
                //_viewModelMain.IncidCurrentRowIndex += 1;
                //---------------------------------------------------------------------

                return true;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();
                MessageBox.Show("OSMM UpdateAsync failed. The error message returned was:\n\n" +
                    ex.Message, "HLU: OSMM Update", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }
        }

        /// <summary>
        /// Writes changes made to all the remaining selected incid_osmm_updates back to the database.
        /// </summary>
        /// <remarks>This method performs a bulk update operation on the dataset, applying the specified
        /// OSMM status to the current incident and all subsequent incidents. The operation is performed within a
        /// database transaction to ensure atomicity. If the operation succeeds, the changes are committed  and the
        /// dataset is updated. If an error occurs, the transaction is rolled back, and an error  message is displayed
        /// to the user.  The method temporarily changes the cursor to indicate a long-running operation and restores it
        /// upon completion. After a successful update, the current incident index is moved beyond the end  of the
        /// dataset to indicate that all incidents have been processed.</remarks>
        /// <param name="updateStatus">The new OSMM status to apply. This value is used to update the relevant incidents.</param>
        internal void OSMMUpdateAll(int updateStatus)
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Updating all ...");

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            try
            {
                // Apply the updates to the current incid and all following incids
                BulkIncidOSMMUpdates(updateStatus, _viewModelMain.IncidOSMMUpdatesRows[0].incid);

                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                MessageBox.Show("OSMM update succeeded.", "HLU: OSMM Update",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Move beyond the end of the Incids (to show they have
                // all been processed)
                _viewModelMain.IncidCurrentRowIndex = _viewModelMain.IncidSelection.Rows.Count + 1;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();
                MessageBox.Show("OSMM UpdateAsync Failed. The error message returned was:\n\n" +
                    ex.Message, "HLU: OSMM Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _viewModelMain.ChangeCursor(Cursors.Arrow, String.Empty);
            }
        }

        #endregion OSMM Update

        #region Bulk OSMM Updates

        /// <summary>
        /// Updates the status and metadata for records in the "incid_osmm_updates" table based on the specified status
        /// and starting incident identifier.
        /// </summary>
        /// <remarks>This method constructs and executes an SQL UPDATE statement to modify the
        /// "incid_osmm_updates" table. If a custom WHERE clause is defined in the
        /// <c>_viewModelMain.OSMMUpdateWhereClause</c>, it will be used to filter the records to update. Otherwise,
        /// the method will iterate through the selected rows in <c>_viewModelMain.IncidSelection</c> and update
        /// records individually based on the incident identifier. The method ensures that the "last_modified_date"
        /// field is updated to the current date and time (rounded to the nearest second) and the
        /// "last_modified_user_id" field is updated to the current user ID.</remarks>
        /// <param name="updateStatus">The new status value to be applied to the records.</param>
        /// <param name="fromIncid">The starting incident identifier. Records with an incident identifier greater than  or equal to this value
        /// will be updated.</param>
        /// <exception cref="Exception">Thrown if the update operation fails for any record in the "incid_osmm_updates" table.</exception>
        private void BulkIncidOSMMUpdates(int updateStatus, string fromIncid)
        {
            // Get the incid column number
            int incidOrdinal =
                _viewModelMain.IncidSelection.Columns[_viewModelMain.HluDataset.incid.incidColumn.ColumnName].Ordinal;

            // Get the column names
            string incidColumn = _viewModelMain.HluDataset.incid_osmm_updates.incidColumn.ColumnName;
            string statusColumn = _viewModelMain.HluDataset.incid_osmm_updates.statusColumn.ColumnName;
            string last_modified_dateColumn = _viewModelMain.HluDataset.incid_osmm_updates.last_modified_dateColumn.ColumnName;
            string last_modified_user_idColumn = _viewModelMain.HluDataset.incid_osmm_updates.last_modified_user_idColumn.ColumnName;

            // Get the current date/time
            // Only update DateTime fields to whole seconds.
            // Fractions of a second can cause rounding differences when
            // comparing DateTime fields later in some databases.
            DateTime currDtTm = DateTime.Now;
            DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day, currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

            // Get the current userid
            string user_id = _viewModelMain.UserID;

            // Use the stored where clause if set
            if (_viewModelMain.OSMMUpdateWhereClause != null)
            {
                // Build an UPDATE statement for the osmm_incid_updates table
                string updateCommand = String.Format("UPDATE {0} SET {1} = {2}, {3} = {4}, {5} = {6} WHERE {7} >= {8} AND {9}",
                    _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_osmm_updates.TableName),
                    _viewModelMain.DataBase.QuoteIdentifier(statusColumn),
                    updateStatus,
                    _viewModelMain.DataBase.QuoteIdentifier(last_modified_dateColumn),
                    _viewModelMain.DataBase.QuoteValue(nowDtTm),
                    _viewModelMain.DataBase.QuoteIdentifier(last_modified_user_idColumn),
                    _viewModelMain.DataBase.QuoteValue(user_id),
                    _viewModelMain.DataBase.QuoteIdentifier(incidColumn),
                    _viewModelMain.DataBase.QuoteValue(fromIncid),
                    _viewModelMain.OSMMUpdateWhereClause);

                // Update the incid for the current row
                if (_viewModelMain.DataBase.ExecuteNonQuery(updateCommand,
                    _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) == -1)
                    throw new Exception("Failed to update incid_osmm_updates table.");
            }
            else
            {
                // Get the from incid number
                int fromIncidNum = RecordIds.IncidNumber(fromIncid);

                // Build an UPDATE statement for the osmm_incid_updates table
                string updateCommand = String.Format("UPDATE {0} SET {1} = {2}, {3} = {4}, {5} = {6} WHERE {7} = {8}",
                            _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_osmm_updates.TableName),
                            _viewModelMain.DataBase.QuoteIdentifier(statusColumn),
                            updateStatus,
                            _viewModelMain.DataBase.QuoteIdentifier(last_modified_dateColumn),
                            _viewModelMain.DataBase.QuoteValue(nowDtTm),
                            _viewModelMain.DataBase.QuoteIdentifier(last_modified_user_idColumn),
                            _viewModelMain.DataBase.QuoteValue(user_id),
                            _viewModelMain.DataBase.QuoteIdentifier(incidColumn),
                            _viewModelMain.DataBase.QuoteValue("{0}"));

                // Loop through all rows in the selection
                foreach (DataRow r in _viewModelMain.IncidSelection.Rows)
                {
                    // Get the incid of the current row
                    string currIncid = r[incidOrdinal].ToString();

                    // Get the incid number
                    int currIncidNum = RecordIds.IncidNumber(currIncid);

                    // Check the incid is to be updated
                    if (currIncidNum >= fromIncidNum)
                    {
                        // Update the incid for the current row
                        if (_viewModelMain.DataBase.ExecuteNonQuery(String.Format(updateCommand, currIncid),
                            _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text) == -1)
                            throw new Exception("Failed to update incid_osmm_updates table.");
                    }
                }
            }
        }

        #endregion Bulk OSMM Updates

        #region Cancel OSMM Update

        /// <summary>
        /// Cancels the ongoing OSMM update process and resets the associated controls.
        /// </summary>
        /// <remarks>This method should be called to terminate an in-progress OSMM update operation.  It
        /// ensures that any related controls are reset to their default state.</remarks>
        public void CancelOSMMUpdate()
        {
            OSMMUpdateResetControls();
        }

        #endregion Cancel OSMM Update

        #region Reset Controls

        /// <summary>
        /// Resets the controls and state related to the OSMM update mode.
        /// </summary>
        /// <remarks>This method performs the following actions: <list type="bullet"> <item>Resets the
        /// incident filter and sets the current row index to 1.</item> <item>Disables the bulk update mode.</item>
        /// <item>Enables all tabs in the user interface.</item> <item>Refreshes all controls to reflect the updated
        /// state.</item> <item>Resets the cursor to the default arrow.</item> </list> This method is typically called
        /// to exit the OSMM update mode and restore the default application state.</remarks>
        private void OSMMUpdateResetControls()
        {
            // Reset the incid filter
            _viewModelMain.OSMMUpdateMode = null;
            _viewModelMain.IncidCurrentRowIndex = 1;

            // Stop the bulk update mode
            _viewModelMain.OSMMUpdateMode = false;

            // Enable all the tabs
            _viewModelMain.TabHabitatControlsEnabled = true;
            _viewModelMain.TabIhsControlsEnabled = true;
            _viewModelMain.TabDetailsControlsEnabled = true;
            _viewModelMain.TabSourcesControlsEnabled = true;
            _viewModelMain.TabItemHistoryEnabled = true;

            // Refresh all the controls
            _viewModelMain.RefreshAll();

            // Reset the cursor
            _viewModelMain.ChangeCursor(Cursors.Arrow, String.Empty);
        }

        #endregion Reset Controls
    }
}