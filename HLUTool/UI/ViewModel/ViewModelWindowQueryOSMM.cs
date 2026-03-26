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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        List<OSMMUpdates> _tableTotal;

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

            // Count the incid_osmm_update rows for the initial values.
            CountOSMMUpdates();
        }

        /// <summary>
        /// Set the filter fields to the selected row values and count the number of OSMM
        /// Updates in the database for the selected flags.
        /// </summary>
        /// <param name="selectedRow">The selected OSMM updates row.</param>
        public void OSMMUpdatesSelectedRow(OSMMUpdates selectedRow)
        {
            // Set the filter fields to the selected row values
            if (selectedRow != null && selectedRow.Change != "Total")
            {
                _osmmProcessFlag = selectedRow.Process;
                _osmmSpatialFlag = selectedRow.Spatial;
                _osmmChangeFlag = selectedRow.Change;

                // Count the incid_osmm_update rows for the initial values.
                CountOSMMUpdates();

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
        private void ResetOSMMFilterClicked(object param)
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
            CountOSMMUpdates();

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
                CountOSMMUpdates();
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
                CountOSMMUpdates();
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
                CountOSMMUpdates();
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
                CountOSMMUpdates();
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
            get { return String.Format("{0:n0}", _osmmUpdatesCountRejected); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and ignored status.
        /// This will be used to show the number of ignored OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of ignored OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesIgnoredCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountIgnored); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and pending status.
        /// This will be used to show the number of pending OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of pending OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesPendingCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountPending); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and proposed status.
        /// This will be used to show the number of proposed OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of proposed OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesProposedCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountProposed); }
        }

        /// <summary>
        /// Gets the count of OSMM Updates in the database for the selected flags and applied status.
        /// This will be used to show the number of applied OSMM Updates for the selected flags and
        /// determine whether the filter can be applied.
        /// </summary>
        /// <value>The count of applied OSMM Updates for the selected flags.</value>
        public string IncidOSMMUpdatesAppliedCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountApplied); }
        }

        /// <summary>
        /// Count the number of OSMM Updates in the database for the selected flags.
        /// </summary>
        public void CountOSMMUpdates()
        {
            // Show the wait cursor whilst loading the values.
            ChangeCursor(Cursors.Wait);

            _osmmUpdatesCountRejected = -1;
            _osmmUpdatesCountIgnored = -1;
            _osmmUpdatesCountApplied = -1;
            _osmmUpdatesCountPending = -1;
            _osmmUpdatesCountProposed = -1;

            StringBuilder whereClause = new();

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

            // Count the total number of rejected OSMM updates in the database for
            // the selected flags.
            _osmmUpdatesCountRejected = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}",
                _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                -99,
                whereClause),
                _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of ignored OSMM updates in the database for
            // the selected flags.
            _osmmUpdatesCountIgnored = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}",
                _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                -2,
                whereClause),
                _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of applied OSMM updates in the database for
            // the selected flags.
            _osmmUpdatesCountApplied = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}",
                _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                -1,
                whereClause),
                _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of pending OSMM updates in the database for
            // the selected flags.
            _osmmUpdatesCountPending = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}{3}",
                _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                0,
                whereClause),
                _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of proposed OSMM updates in the database for
            // the selected flags.
            _osmmUpdatesCountProposed = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} > {2}{3}",
                _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                0,
                whereClause),
                _db.Connection.ConnectionTimeout, CommandType.Text);

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
            ChangeCursor(Cursors.Arrow);

        }

        #endregion Counts

        #region OSMM Updates Summary

        /// <summary>
        /// Gets a summary of the OSMM Updates in the database for the selected flags. This will be used
        /// to show the number of OSMM Updates for each status and determine whether the filter can be applied.
        /// </summary>
        /// <value>A list of OSMM Updates summary for the selected flags.</value>
        public List<OSMMUpdates> OSMMUpdatesSummary
        {
            get
            {
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

                // Show the wait cursor whilst loading the values.
                ChangeCursor(Cursors.Wait);

                // Define a new data table to hold the results.
                List<OSMMUpdates> dataTable = [];

                // Create a data reader to retrieve the rows for
                // the required column.
                IDataReader dataReader = null;

                try
                {
                    // Load the data reader to retrieve the rows for
                    // the required column.
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
                    String sql = String.Format(
                        "SELECT DISTINCT {0} FROM {1} GROUP BY {3} ORDER BY {3}",
                        sqlColumns,
                        _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                        _db.QualifyTableName(_hluDataset.lut_osmm_habitat_xref.TableName),
                        sqlGroupBy);

                    dataReader = _db.ExecuteReader(sql,
                        _db.Connection.ConnectionTimeout, CommandType.Text);

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
                        //summary = dataReader.GetValue(4).ToString();
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
                            rejectedTotal += rejectedCount;
                            ignoredTotal += ignoredCount;
                            proposedTotal += proposedCount;
                            pendingTotal += pendingCount;
                            appliedTotal += appliedCount;
                            allTotal += allCount;

                            // Reset the counts.
                            rejectedCount = 0;
                            ignoredCount = 0;
                            proposedCount = 0;
                            pendingCount = 0;
                            appliedCount = 0;
                            allCount = 0;

                            // Save the last group values;
                            lastProcessFlag = processFlag;
                            lastSpatialFlag = spatialFlag;
                            lastChangeFlag = changeFlag;
                        }

                        // Update the counts.
                        if (status == -99)
                            rejectedCount = recs;
                        if (status == -2)
                            ignoredCount = recs;
                        if (status == 0)
                            pendingCount = recs;
                        if (status == -1)
                            appliedCount = recs;
                        if (status > 0)
                            proposedCount += recs;
                        allCount += recs;

                    }

                    // Add the last results as a new row.
                    dataRow = new OSMMUpdates
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
                    rejectedTotal += rejectedCount;
                    ignoredTotal += ignoredCount;
                    proposedTotal += proposedCount;
                    pendingTotal += pendingCount;
                    appliedTotal += appliedCount;
                    allTotal += allCount;

                    // Add the totals as a new row.
                    _tableTotal = [];
                    dataRow = new OSMMUpdates
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
                    };
                    _tableTotal.Add(dataRow);

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow);

                    return dataTable;
                }
                catch (Exception ex)
                {
                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow);

                    MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    return null;
                }
                finally
                {
                    // Close the data reader.
                    if ((dataReader != null) && (!dataReader.IsClosed))
                        dataReader.Close();
                }
            }

        }

        /// <summary>
        /// Gets a summary of the OSMM Updates in the database for the selected flags. This will be used
        /// to show the number of OSMM Updates for each status and determine whether the filter can be applied.
        /// </summary>
        /// <value>A list of OSMM Updates summary for the selected flags.</value>
        public List<OSMMUpdates> OSMMUpdatesSummaryTotal
        {
            get
            {
                return _tableTotal;
            }
        }

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