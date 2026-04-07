// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HLU.Data.Model;
using HLU.Data.Connection;
using HLU.UI.ViewModel;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.UserControls;
using HLU.Helpers;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Represents a summary of the OSMM Updates in the database for the selected flags. This will be used
    /// to display the counts of updates in different statuses (Rejected, Ignored, Proposed, Pending, Applied, Total).
    /// </summary>
    public class OSMMUpdates
    {
        #region Fields

        public string Process { get; set; }
        public string Spatial { get; set; }
        public string Change { get; set; }
        public int Rejected { get; set; }
        public int Ignored { get; set; }
        public int Proposed { get; set; }
        public int Pending { get; set; }
        public int Applied { get; set; }
        public int Total { get; set; }

        /// <summary>
        /// Numeric value of <see cref="Process"/> for correct numeric sort in the DataGrid.
        /// Falls back to <see cref="int.MaxValue"/> when the value is not a plain integer.
        /// </summary>
        public int ProcessSortKey =>
            int.TryParse(Process, out int n) ? n : int.MaxValue;

        #endregion Fields
    }

    /// <summary>
    /// Contains the view model for the OSMM Updates Query window.
    /// </summary>
    class ViewModelWindowQueryOSMM : ViewModelBase, INotifyPropertyChanged
    {
        #region Fields

        ViewModelWindowMain _viewModelMain;

        public static HluDataSet HluDatasetStatic = null;

        private HluDataSet _hluDataset;
        private DbBase _db;

        private Cursor _windowCursor = Cursors.Arrow;

        private ICommand _applyOSMMFilterCommand;
        private ICommand _resetOSMMFilterCommand;
        private ICommand _cancelOSMMFilterCommand;

        private int _osmmUpdatesCountRejected = -1;
        private int _osmmUpdatesCountIgnored = -1;
        private int _osmmUpdatesCountPending = -1;
        private int _osmmUpdatesCountApplied = -1;
        private int _osmmUpdatesCountProposed = -1;

        private int _filterCount = -1;

        private string _osmmUpdatesStatus;

        private HluDataSet.lut_osmm_updates_processRow[] _osmmProcessFlags;
        private HluDataSet.lut_osmm_updates_spatialRow[] _osmmSpatialFlags;
        private HluDataSet.lut_osmm_updates_changeRow[] _osmmChangeFlags;
        private string _osmmProcessFlag;
        private string _osmmSpatialFlag;
        private string _osmmChangeFlag;

        private string _codeAnyRow = Settings.Default.CodeAnyRow;

        private string _displayName = "OSMM Updates Filter";

        private readonly ObservableCollection<OSMMUpdates> _osmmUpdatesSummary = [];
        private readonly ObservableCollection<OSMMUpdates> _tableTotal = [];

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelWindowQueryOSMM"/> class. This will set the
        /// initial values for the OSMM Updates filter and count the updates in the database.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="hluDataset">The HLU dataset.</param>
        /// <param name="hluDatabase">The HLU database.</param>
        /// <param name="viewModelMain">The main view model.</param>
        public ViewModelWindowQueryOSMM(HluDataSet hluDataset, DbBase hluDatabase, ViewModelWindowMain viewModelMain)
        {
            HluDatasetStatic = hluDataset;
            _hluDataset = hluDataset;
            _db = hluDatabase;
            _viewModelMain = viewModelMain;

            // Reset all the filter fields.
            _osmmProcessFlag = _codeAnyRow;
            _osmmSpatialFlag = _codeAnyRow;
            _osmmChangeFlag = _codeAnyRow;

            // Set the default status value
            if (_viewModelMain.IsOsmmBulkMode)
                _osmmUpdatesStatus = "Pending";
            else
                _osmmUpdatesStatus = "Proposed";

            // Counts and summary are loaded asynchronously via LoadAsync().
        }

        /// <summary>
        /// Loads OSMM update counts and the summary table asynchronously.
        /// Called after the window is shown so the UI thread is not blocked.
        /// </summary>
        public async Task LoadAsync()
        {
            // Show the wait cursor whilst loading the values.
            ChangeCursor(Cursors.Wait);

            await CountOSMMUpdatesAsync(suppressCursorChange: true);
            await LoadOSMMUpdatesSummaryAsync(suppressCursorChange: true);

            // Reset the cursor back to normal.
            ChangeCursor(Cursors.Arrow);
        }

        /// <summary>
        /// Set the filter fields to the selected row values and count the number of OSMM
        /// Updates in the database for the selected flags.
        /// </summary>
        /// <param name="selectedRow">The selected OSMM updates row.</param>
        public async void OSMMUpdatesSelectedRow(OSMMUpdates selectedRow)
        {
            // Set the filter fields to the selected row values
            if (selectedRow != null && selectedRow.Change != "Total")
            {
                _osmmProcessFlag = selectedRow.Process;
                _osmmSpatialFlag = selectedRow.Spatial;
                _osmmChangeFlag = selectedRow.Change;

                // Count the incid_osmm_update rows for the initial values.
                await CountOSMMUpdatesAsync();

                OnPropertyChanged(nameof(IncidOSMMUpdatesProcessFlag));
                OnPropertyChanged(nameof(IncidOSMMUpdatesSpatialFlag));
                OnPropertyChanged(nameof(IncidOSMMUpdatesChangeFlag));
            }
        }

        #endregion Constructor

        #region ViewModelBase Members

        /// <summary>
        /// Gets or sets the display name for this view model. This is used when a view binds to this view model.
        /// </summary>
        /// <value>The display name for this view model.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        /// <summary>
        /// Gets the window title for this view model. This is used when a view binds to this view model.
        /// </summary>
        /// <value>The window title for this view model.</value>
        public override string WindowTitle
        {
            get { return _displayName; }
        }

        #endregion ViewModelBase Members

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string processFlag, string spatialFlag, string changeFlag, string status, bool apply);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Apply Command

        /// <summary>
        /// Gets the command to apply the OSMM Updates filter. This will set the filter
        /// fields to the selected values and count the number of OSMM Updates in the
        /// database for the selected flags.
        /// </summary>
        /// <value>The command to apply the OSMM Updates filter.</value>
        public ICommand ApplyOSMMFilterCommand
        {
            get
            {
                if (_applyOSMMFilterCommand == null)
                {
                    Action<object> applyOSMMFilterAction = new(this.ApplyOSMMFilterClicked);
                    _applyOSMMFilterCommand = new RelayCommand(applyOSMMFilterAction, param => this.CanApplyOSMMFilter);
                }
                return _applyOSMMFilterCommand;
            }
        }

        /// <summary>
        /// Handles event when Apply button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void ApplyOSMMFilterClicked(object param)
        {
            HluDatasetStatic = null;

            // Close the window and apply the filter with the selected flags.
            RequestClose?.Invoke(IncidOSMMUpdatesProcessFlag, IncidOSMMUpdatesSpatialFlag, IncidOSMMUpdatesChangeFlag, IncidOSMMUpdatesStatus, true);
        }

        /// <summary>
        /// Gets a value indicating whether the OSMM Updates filter can be applied. This will
        /// be true if there are any OSMM Updates in the database for the selected flags.
        /// </summary>
        /// <value><c>true</c> if the OSMM Updates filter can be applied; otherwise, <c>false</c>.</value>
        public bool CanApplyOSMMFilter
        {
            get
            {
                return (_filterCount > 0);
            }
        }

        #endregion Apply Command

        #region Reset Command

        /// <summary>
        /// Gets the command to reset the OSMM Updates filter. This will set the filter fields
        /// to the default values and count the number of OSMM Updates in the database for the
        /// initial values.
        /// </summary>
        /// <value>The command to reset the OSMM Updates filter.</value>
        public ICommand ResetOSMMFilterCommand
        {
            get
            {
                if (_resetOSMMFilterCommand == null)
                {
                    Action<object> resetOSMMFilterAction = new(this.ResetOSMMFilterClicked);
                    _resetOSMMFilterCommand = new RelayCommand(resetOSMMFilterAction, param => this.CanResetOSMMFilter);
                }
                return _resetOSMMFilterCommand;
            }
        }

        /// <summary>
        /// Handles event when Reset button is clicked. This will set the filter fields to
        /// the default values and count the number of OSMM Updates in the database for
        /// the initial values.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void ResetOSMMFilterClicked(object param)
        {
            // Reset all the filter fields.
            _osmmProcessFlag = _codeAnyRow;
            _osmmSpatialFlag = _codeAnyRow;
            _osmmChangeFlag = _codeAnyRow;

            // Set the default status value
            if (_viewModelMain.IsOsmmBulkMode)
                _osmmUpdatesStatus = "Pending";
            else
                _osmmUpdatesStatus = "Proposed";

            // Count the incid_osmm_update rows for the initial values.
            await CountOSMMUpdatesAsync();

            OnPropertyChanged(nameof(IncidOSMMUpdatesProcessFlag));
            OnPropertyChanged(nameof(IncidOSMMUpdatesSpatialFlag));
            OnPropertyChanged(nameof(IncidOSMMUpdatesChangeFlag));
            OnPropertyChanged(nameof(IncidOSMMUpdatesStatus));
        }

        /// <summary>
        /// Gets a value indicating whether the OSMM Updates filter can be reset. This will be
        /// true if there are any OSMM Updates in the database for the initial values.
        /// </summary>
        /// <value><c>true</c> if the OSMM Updates filter can be reset; otherwise, <c>false</c>.</value>
        public bool CanResetOSMMFilter
        {
            get
            {
                return true;
            }
        }

        #endregion Reset Command

        #region Cancel Command

        /// <summary>
        /// Gets the command to cancel the OSMM Updates filter. This will set the filter fields to
        /// the default values and count the number of OSMM Updates in the database for the
        /// initial values.
        /// </summary>
        /// <value>The command to cancel the OSMM Updates filter.</value>
        public ICommand CancelOSMMFilterCommand
        {
            get
            {
                if (_cancelOSMMFilterCommand == null)
                {
                    Action<object> cancelOSMMFilterAction = new(this.CancelCommandClicked);
                    _cancelOSMMFilterCommand = new RelayCommand(cancelOSMMFilterAction);
                }

                return _cancelOSMMFilterCommand;
            }
        }

        /// <summary>
        /// Handles event when Cancel button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void CancelCommandClicked(object param)
        {
            HluDatasetStatic = null;

            // Close the window without applying any filter.
            RequestClose?.Invoke(null, null, null, null, false);
        }

        #endregion Cancel Command

        #region Flags

        /// <summary>
        /// Gets or sets the list of available process flag options from the class. This will include
        /// an "Any" option to show all OSMM Updates regardless of the process flag value.
        /// </summary>
        /// <value>The list of available process flag options.</value>
        public HluDataSet.lut_osmm_updates_processRow[] IncidOSMMUpdatesProcessFlags
        {
            get
            {
                // Load the process flag options from the dataset if they haven't already been loaded.
                if ((_osmmProcessFlags == null) || (_osmmProcessFlags.Length == 0))
                {
                    _osmmProcessFlags = [.. (from m in _hluDataset.lut_osmm_updates_process
                                         select m).OrderBy(m => m.sort_order).ThenBy(m => m.description)];
                }

                // Add an "Any" option to show all OSMM Updates regardless of the process flag value.
                HluDataSet.lut_osmm_updates_processRow[] osmmProcessFlags;
                osmmProcessFlags = [.. AnyRowOSMMUpdatesProcess(-3).Concat(_osmmProcessFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return osmmProcessFlags;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred process flag option. This will be used to filter the OSMM
        /// Updates in the database and count the number of OSMM Updates for the selected flags.
        /// </summary>
        /// <value>The preferred process flag option.</value>
        public string IncidOSMMUpdatesProcessFlag
        {
            get { return _osmmProcessFlag; }
            set
            {
                _osmmProcessFlag = value;

                // Count the incid_osmm_update rows for the selected flag.
                _ = CountOSMMUpdatesAsync();
            }
        }

        /// <summary>
        /// Gets or sets the list of available spatial flag options from the class. This will include
        /// an "Any" option to show all OSMM Updates regardless of the spatial flag value.
        /// </summary>
        /// <value>The list of available spatial flag options.</value>
        public HluDataSet.lut_osmm_updates_spatialRow[] IncidOSMMUpdatesSpatialFlags
        {
            get
            {
                // If the spatial flag options haven't already been loaded, load them from the dataset.
                if ((_osmmSpatialFlags == null) || (_osmmSpatialFlags.Length == 0))
                {
                    _osmmSpatialFlags = [.. (from m in _hluDataset.lut_osmm_updates_spatial
                                         select m).OrderBy(m => m.sort_order).ThenBy(m => m.description)];
                }

                // Add an "Any" option to show all OSMM Updates regardless of the spatial flag value.
                HluDataSet.lut_osmm_updates_spatialRow[] osmmSpatialFlags;
                osmmSpatialFlags = [.. AnyRowOSMMUpdatesSpatial(-3).Concat(_osmmSpatialFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return osmmSpatialFlags;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred spatial flag option. This will be used to filter the OSMM
        /// Updates in the database and count the number of OSMM Updates for the selected flags.
        /// </summary>
        /// <value>The preferred spatial flag option.</value>
        public string IncidOSMMUpdatesSpatialFlag
        {
            get { return _osmmSpatialFlag; }
            set
            {
                _osmmSpatialFlag = value;

                // Count the incid_osmm_update rows for the selected flag.
                _ = CountOSMMUpdatesAsync();
            }
        }

        /// <summary>
        /// Gets or sets the list of available change flag options from the class. This will include
        /// an "Any" option to show all OSMM Updates regardless of the change flag value.
        /// </summary>
        /// <value>The list of available change flag options.</value>
        public HluDataSet.lut_osmm_updates_changeRow[] IncidOSMMUpdatesChangeFlags
        {
            get
            {
                // If the change flag options haven't already been loaded, load them from the dataset.
                if ((_osmmChangeFlags == null) || (_osmmChangeFlags.Length == 0))
                {
                    _osmmChangeFlags = [.. (from m in _hluDataset.lut_osmm_updates_change
                                        select m).OrderBy(m => m.sort_order).ThenBy(m => m.description)];
                }

                // Add an "Any" option to show all OSMM Updates regardless of the change flag value.
                HluDataSet.lut_osmm_updates_changeRow[] osmmChangeFlags;
                osmmChangeFlags = [.. AnyRowOSMMUpdatesChange(-3).Concat(_osmmChangeFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return osmmChangeFlags;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred change flag option. This will be used to filter the OSMM
        /// Updates in the database and count the number of OSMM Updates for the selected flags.
        /// </summary>
        /// <value>The preferred change flag option.</value>
        public string IncidOSMMUpdatesChangeFlag
        {
            get { return _osmmChangeFlag; }
            set
            {
                _osmmChangeFlag = value;

                // Count the incid_osmm_update rows for the selected flag.
                _ = CountOSMMUpdatesAsync();
            }
        }

        #endregion Flags

        #region Statuses

        /// <summary>
        /// Gets or sets the list of available show OSMM Update options from
        /// the class.
        /// </summary>
        /// <value>
        /// The list of subset update actions.
        /// </value>
        public string[] IncidOSMMUpdatesStatuses
        {
            get
            {
                // If the show OSMM Update options haven't already been loaded, load them from the settings.
                string[] osmmUpdateStatuses;
                if (_viewModelMain.IsOsmmBulkMode)
                    osmmUpdateStatuses = ["Pending"];
                else
                    osmmUpdateStatuses = [.. Settings.Default.OSMMUpdatesStatuses.Cast<string>()];

                return osmmUpdateStatuses;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred show OSMM Update option.
        /// </summary>
        /// <value>
        /// The preferred show OSMM Update option.
        /// </value>
        public string IncidOSMMUpdatesStatus
        {
            get { return _osmmUpdatesStatus; }
            set
            {
                _osmmUpdatesStatus = value;

                // Count the incid_osmm_update rows for the selected status.
                _ = CountOSMMUpdatesAsync();
            }
        }

        #endregion Statuses

        #region Any Rows

        /// <summary>
        /// Gets a row with the code for "Any" to show all OSMM Updates regardless of the process
        /// flag value. This will be used to add an "Any" option to the list of process flag options.
        /// </summary>
        /// <param name="sortOrder">The sort order for the "Any" row.</param>
        /// <returns>An array containing the "Any" row.</returns>
        private HluDataSet.lut_osmm_updates_processRow[] AnyRowOSMMUpdatesProcess(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_processRow anyRow = _hluDataset.lut_osmm_updates_process.Newlut_osmm_updates_processRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        /// <summary>
        /// Gets a row with the code for "Any" to show all OSMM Updates regardless of the spatial
        /// flag value. This will be used to add an "Any" option to the list of spatial flag options.
        /// </summary>
        /// <param name="sortOrder">The sort order for the "Any" row.</param>
        /// <returns>An array containing the "Any" row.</returns>
        private HluDataSet.lut_osmm_updates_spatialRow[] AnyRowOSMMUpdatesSpatial(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_spatialRow anyRow = _hluDataset.lut_osmm_updates_spatial.Newlut_osmm_updates_spatialRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        /// <summary>
        /// Gets a row with the code for "Any" to show all OSMM Updates regardless of the change
        /// flag value. This will be used to add an "Any" option to the list of change flag options.
        /// </summary>
        /// <param name="sortOrder">The sort order for the "Any" row.</param>
        /// <returns>An array containing the "Any" row.</returns>
        private HluDataSet.lut_osmm_updates_changeRow[] AnyRowOSMMUpdatesChange(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_changeRow anyRow = _hluDataset.lut_osmm_updates_change.Newlut_osmm_updates_changeRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        #endregion Any Rows

        #region Counts

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and rejected status.
        /// This will be used to show the number of rejected OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of rejected OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesRejectedCount
        {
            get { return _osmmUpdatesCountRejected == -1 ? null : String.Format("{0:n0}", _osmmUpdatesCountRejected); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and ignored status.
        /// This will be used to show the number of ignored OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of ignored OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesIgnoredCount
        {
            get { return _osmmUpdatesCountIgnored == -1 ? null : String.Format("{0:n0}", _osmmUpdatesCountIgnored); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and pending status.
        /// This will be used to show the number of pending OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of pending OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesPendingCount
        {
            get { return _osmmUpdatesCountPending == -1 ? null : String.Format("{0:n0}", _osmmUpdatesCountPending); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and proposed status.
        /// This will be used to show the number of proposed OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of proposed OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesProposedCount
        {
            get { return _osmmUpdatesCountProposed == -1 ? null : String.Format("{0:n0}", _osmmUpdatesCountProposed); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and applied status.
        /// This will be used to show the number of applied OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of applied OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesAppliedCount
        {
            get { return _osmmUpdatesCountApplied == -1 ? null : String.Format("{0:n0}", _osmmUpdatesCountApplied); }
        }

        /// <summary>
        /// Count the number of OSMM Updates in the database for the selected flags asynchronously.
        /// </summary>
        public async Task CountOSMMUpdatesAsync(bool suppressCursorChange = false)
        {
            // Show the wait cursor whilst loading the values.
            if (!suppressCursorChange) ChangeCursor(Cursors.Wait);

            // Reset the counts to -1 to indicate they are being loaded. This will show a blank
            // value in the UI until the counts are loaded.
            _osmmUpdatesCountRejected = -1;
            _osmmUpdatesCountIgnored = -1;
            _osmmUpdatesCountApplied = -1;
            _osmmUpdatesCountPending = -1;
            _osmmUpdatesCountProposed = -1;

            // Build the WHERE clause for the SQL query based on the selected flags. Only include
            // conditions for flags that are not "Any".
            StringBuilder whereClause = new();

            // Append the selected flags to the WHERE clause if they are not "Any". The "Any" option
            // will show all OSMM Updates regardless of the flag value, so no need to filter by that
            // flag in the query.
            if (!String.IsNullOrEmpty(_osmmProcessFlag) && _osmmProcessFlag != _codeAnyRow)
            {
                whereClause.Append(String.Format(" AND {0} = {1}",
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.process_flagColumn.ColumnName), _osmmProcessFlag));
            }

            if (!String.IsNullOrEmpty(_osmmSpatialFlag) && _osmmSpatialFlag != _codeAnyRow)
            {
                whereClause.Append(String.Format(" AND {0} = {1}",
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.spatial_flagColumn.ColumnName), _db.QuoteValue(_osmmSpatialFlag)));
            }

            if (!String.IsNullOrEmpty(_osmmChangeFlag) && _osmmChangeFlag != _codeAnyRow)
            {
                whereClause.Append(String.Format(" AND {0} = {1}",
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.change_flagColumn.ColumnName), _db.QuoteValue(_osmmChangeFlag)));
            }

            // Capture locals for use inside Task.Run (avoids closure over 'this' members that may change).
            string tableName = _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName);
            string statusCol = _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName);
            string where = whereClause.ToString();
            int timeout = _db.Connection.ConnectionTimeout;

            // Run all five COUNT queries on a background thread so the UI thread is not blocked.
            (int rejected, int ignored, int applied, int pending, int proposed) = await Task.Run(() =>
            {
                // Count the number of OSMM Updates in the database for the selected flags and each status.
                int rejectedCount = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}", tableName, statusCol, -99, where),
                    timeout, CommandType.Text);
                int ignoredCount = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}", tableName, statusCol, -2, where),
                    timeout, CommandType.Text);
                int appliedCount = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}", tableName, statusCol, -1, where),
                    timeout, CommandType.Text);
                int pendingCount = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}", tableName, statusCol, 0, where),
                    timeout, CommandType.Text);
                int proposedCount = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM {0} WHERE {1} > {2}{3}", tableName, statusCol, 0, where),
                    timeout, CommandType.Text);

                // Return the counts as a tuple.
                return (rejectedCount, ignoredCount, appliedCount, pendingCount, proposedCount);
            });

            // Update the counts in the view model with the results from the background thread.
            _osmmUpdatesCountRejected = rejected;
            _osmmUpdatesCountIgnored = ignored;
            _osmmUpdatesCountApplied = applied;
            _osmmUpdatesCountPending = pending;
            _osmmUpdatesCountProposed = proposed;

            // Set the filter count based on the selected status. This will be used to determine
            // whether the filter can be applied.
            if (!String.IsNullOrEmpty(_osmmUpdatesStatus) && _osmmUpdatesStatus != _codeAnyRow)
            {
                switch (_osmmUpdatesStatus)
                {
                    case "Rejected":
                        _filterCount = _osmmUpdatesCountRejected;
                        break;
                    case "Ignored":
                        _filterCount = _osmmUpdatesCountIgnored;
                        break;
                    case "Proposed":
                        _filterCount = _osmmUpdatesCountProposed;
                        break;
                    case "Pending":
                        _filterCount = _osmmUpdatesCountPending;
                        break;
                }
            }

            OnPropertyChanged(nameof(IncidOSMMUpdatesRejectedCount));
            OnPropertyChanged(nameof(IncidOSMMUpdatesIgnoredCount));
            OnPropertyChanged(nameof(IncidOSMMUpdatesAppliedCount));
            OnPropertyChanged(nameof(IncidOSMMUpdatesPendingCount));
            OnPropertyChanged(nameof(IncidOSMMUpdatesProposedCount));

            OnPropertyChanged(nameof(CanApplyOSMMFilter));

            // Reset the cursor back to normal.
            if (!suppressCursorChange) ChangeCursor(Cursors.Arrow);
        }

        #endregion Counts

        #region OSMM Updates Summary

        /// <summary>
        /// Gets the cached OSMM Updates summary list. Populated by <see cref="LoadOSMMUpdatesSummaryAsync"/>.
        /// </summary>
        public ObservableCollection<OSMMUpdates> OSMMUpdatesSummary => _osmmUpdatesSummary;

        /// <summary>
        /// Loads the OSMM Updates summary from the database asynchronously and notifies the UI.
        /// </summary>
        public async Task LoadOSMMUpdatesSummaryAsync(bool suppressCursorChange = false)
        {
            // Show the wait cursor whilst loading the values.
            if (!suppressCursorChange) ChangeCursor(Cursors.Wait);

            // Create the select SQL.
            string sql;
            {
                string sqlColumns = String.Format("{0}, {1}, {2}, {3}, COUNT(*) As RecCount",
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.process_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.spatial_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.change_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName));
                string sqlGroupBy = String.Format("{0}, {1}, {2}, {3}",
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.process_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.spatial_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.change_flagColumn.ColumnName),
                    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName));
                sql = String.Format(
                    "SELECT DISTINCT {0} FROM {1} GROUP BY {3} ORDER BY {3}",
                    sqlColumns,
                    _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                    _db.QualifyTableName(_hluDataset.lut_osmm_habitat_xref.TableName),
                    sqlGroupBy);
            }

            // Get the query timeout.
            int timeout = _db.Connection.ConnectionTimeout;

            // Run the heavy reader query on a background thread.
            List<OSMMUpdates> result = null;
            List<OSMMUpdates> total = null;

            try
            {
                (result, total) = await Task.Run(() =>
                {
                    List<OSMMUpdates> dataTable = [];
                    IDataReader dataReader = null;

                    // Reset the counts and totals.
                    int rejectedCount = 0;
                    int ignoredCount = 0;
                    int proposedCount = 0;
                    int pendingCount = 0;
                    int appliedCount = 0;
                    int allCount = 0;
                    int rejectedTotal = 0;
                    int ignoredTotal = 0;
                    int proposedTotal = 0;
                    int pendingTotal = 0;
                    int appliedTotal = 0;
                    int allTotal = 0;

                    try
                    {
                        dataReader = _db.ExecuteReader(sql, timeout, CommandType.Text);

                        if (dataReader == null) throw new Exception($"Error reading values from {_hluDataset.incid_osmm_updates.TableName}");

                        string processFlag, lastProcessFlag = null;
                        string spatialFlag, lastSpatialFlag = null;
                        string changeFlag, lastChangeFlag = null;
                        int status;
                        int recs;
                        OSMMUpdates dataRow;

                        // Load the list with the results.
                        while (dataReader.Read())
                        {
                            processFlag = dataReader.GetValue(0).ToString();
                            spatialFlag = dataReader.GetValue(1).ToString();
                            changeFlag = dataReader.GetValue(2).ToString();
                            status = (int)dataReader.GetValue(3);
                            recs = (int)dataReader.GetValue(4);

                            lastProcessFlag ??= processFlag;
                            lastSpatialFlag ??= spatialFlag;
                            lastChangeFlag ??= changeFlag;

                            // If this is a different group.
                            if (processFlag != lastProcessFlag || spatialFlag != lastSpatialFlag || changeFlag != lastChangeFlag)
                            {
                                // Add the results as a new row.
                                dataRow = new()
                                {
                                    Process = lastProcessFlag,
                                    Spatial = lastSpatialFlag,
                                    Change = lastChangeFlag,
                                    Rejected = rejectedCount,
                                    Ignored = ignoredCount,
                                    Proposed = proposedCount,
                                    Pending = pendingCount,
                                    Applied = appliedCount,
                                    Total = allCount
                                };
                                dataTable.Add(dataRow);

                                // Update the totals.
                                rejectedTotal += rejectedCount; ignoredTotal += ignoredCount; proposedTotal += proposedCount; pendingTotal += pendingCount; appliedTotal += appliedCount; allTotal += allCount;

                                // Reset the counts.
                                rejectedCount = 0; ignoredCount = 0; proposedCount = 0; pendingCount = 0; appliedCount = 0; allCount = 0;

                                // Save the last group values;
                                lastProcessFlag = processFlag;
                                lastSpatialFlag = spatialFlag;
                                lastChangeFlag = changeFlag;
                            }

                            // Update the counts.
                            if (status == -99) rejectedCount = recs;
                            if (status == -2) ignoredCount = recs;
                            if (status == 0) pendingCount = recs;
                            if (status == -1) appliedCount = recs;
                            if (status > 0) proposedCount += recs;
                            allCount += recs;
                        }

                        // Add the last results as a new row.
                        OSMMUpdates lastRow = new()
                        {
                            Process = lastProcessFlag,
                            Spatial = lastSpatialFlag,
                            Change = lastChangeFlag,
                            Rejected = rejectedCount,
                            Ignored = ignoredCount,
                            Proposed = proposedCount,
                            Pending = pendingCount,
                            Applied = appliedCount,
                            Total = allCount
                        };
                        dataTable.Add(lastRow);

                        // Update the totals.
                        rejectedTotal += rejectedCount; ignoredTotal += ignoredCount; proposedTotal += proposedCount; pendingTotal += pendingCount; appliedTotal += appliedCount; allTotal += allCount;
                    }
                    finally
                    {
                        if ((dataReader != null) && (!dataReader.IsClosed))
                            dataReader.Close();
                    }

                    // Build the totals row.
                    List<OSMMUpdates> tot = [new OSMMUpdates
                    {
                        Process = "",
                        Change = "",
                        Spatial = "Total",
                        Rejected = rejectedTotal,
                        Ignored = ignoredTotal,
                        Proposed = proposedTotal,
                        Pending = pendingTotal,
                        Applied = appliedTotal,
                        Total = allTotal
                    }];

                    // Return the results and totals.
                    return (dataTable, tot);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);
                result = null;
                total = null;
            }
            finally
            {
                // Reset the cursor back to normal.
                if (!suppressCursorChange) ChangeCursor(Cursors.Arrow);
            }

            // Populate the observable collections in-place so the DataGrid receives
            // incremental CollectionChanged notifications rather than a full ItemsSource reset.
            _osmmUpdatesSummary.Clear();
            if (result != null)
                foreach (OSMMUpdates row in result)
                    _osmmUpdatesSummary.Add(row);

            _tableTotal.Clear();
            if (total != null)
                foreach (OSMMUpdates row in total)
                    _tableTotal.Add(row);
        }

        /// <summary>
        /// Gets a summary of the OSMM Updates totals in the database for the selected flags.
        /// </summary>
        public ObservableCollection<OSMMUpdates> OSMMUpdatesSummaryTotal => _tableTotal;

        #endregion OSMM Updates Summary

        #region Cursor

        /// <summary>
        /// Gets the cursor type to use when the cursor is over the window.
        /// </summary>
        /// <value>
        /// The window cursor type.
        /// </value>
        public Cursor WindowCursor { get { return _windowCursor; } }

        /// <summary>
        /// Changes the cursor type to use when the cursor is over the window. This will be used to show a
        /// wait cursor during long-running operations.
        /// </summary>
        /// <param name="cursorType">The cursor type to set.</param>
        public void ChangeCursor(Cursor cursorType)
        {
            _windowCursor = cursorType;
            OnPropertyChanged(nameof(WindowCursor));

            if (cursorType == Cursors.Wait)
                DispatcherHelper.DoEvents();
        }

        #endregion Cursor
    }
}