﻿// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

namespace HLU.UI.ViewModel
{
    //---------------------------------------------------------------------
    // CHANGED: CR49 Process proposed OSMM Updates
    // Functionality to process proposed OSMM Updates.
    //

    public class OSMMUpdates
    {
        public string Process { get; set; }
        public string Spatial { get; set; }
        public string Change { get; set; }
        //public string Summary { get; set; }
        public int Rejected { get; set; }
        public int Ignored { get; set; }
        public int Proposed { get; set; }
        public int Pending { get; set; }
        public int Applied { get; set; }
        public int Total { get; set; }
    }

    class ViewModelWindowQueryOSMM : ViewModelBase, INotifyPropertyChanged
    {
        //public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

        //public event SelectionChangedEventHandler SelectionChanged;

        ViewModelWindowMain _viewModelMain;

        public static HluDataSet HluDatasetStatic = null;

        #region Fields

        private HluDataSet _hluDataset;
        private DbBase _db;

        private Cursor _cursorType = Cursors.Arrow;

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

        //private DataRow _osmmUpdatesSelected;

        private HluDataSet.lut_osmm_updates_processRow[] _osmmProcessFlags;
        private HluDataSet.lut_osmm_updates_spatialRow[] _osmmSpatialFlags;
        private HluDataSet.lut_osmm_updates_changeRow[] _osmmChangeFlags;
        private string _osmmProcessFlag;
        private string _osmmSpatialFlag;
        private string _osmmChangeFlag;

        private string _codeAnyRow = Settings.Default.CodeAnyRow;

        private string _displayName = "OSMM Updates Filter";

        List<OSMMUpdates> _tableTotal;

        #endregion

        #region Constructor

        /// <summary>
        /// Get the default values from settings.
        /// </summary>
        /// <remarks></remarks>
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
            if (_viewModelMain.OSMMBulkUpdateMode == true)
                _osmmUpdatesStatus = "Pending";
            else
                _osmmUpdatesStatus = "Proposed";

            // Count the incid_osmm_update rows for the initial values.
            CountOSMMUpdates();
        }

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

        #endregion

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        public override string WindowTitle
        {
            get { return _displayName; }
        }

        #endregion

        #region RequestClose

        // declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string processFlag, string spatialFlag, string changeFlag, string status, bool apply);

        // declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion

        #region Apply Command

        /// <summary>
        /// OSMM Updates Apply Filter command.
        /// </summary>
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
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void ApplyOSMMFilterClicked(object param)
        {
            HluDatasetStatic = null;
            this.RequestClose(IncidOSMMUpdatesProcessFlag, IncidOSMMUpdatesSpatialFlag, IncidOSMMUpdatesChangeFlag, IncidOSMMUpdatesStatus, true);
        }

        public bool CanApplyOSMMFilter
        {
            get
            {
                return (_filterCount > 0);
            }
        }

        #endregion

        #region Reset Command

        /// <summary>
        /// OSMM Updates Reset Filter command.
        /// </summary>
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
        /// Handles event when Reset button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void ResetOSMMFilterClicked(object param)
        {
            // Reset all the filter fields.
            _osmmProcessFlag = _codeAnyRow;
            _osmmSpatialFlag = _codeAnyRow;
            _osmmChangeFlag = _codeAnyRow;

            // Set the default status value
            if (_viewModelMain.OSMMBulkUpdateMode == true)
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

        public bool CanResetOSMMFilter
        {
            get
            {
                return true;
            }
        }

        #endregion

        #region Cancel Command

        /// <summary>
        /// OSMM Updates Cancel Filter command.
        /// </summary>
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
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CancelCommandClicked(object param)
        {
            HluDatasetStatic = null;
            this.RequestClose(null, null, null, null, false);
        }

        #endregion

        public HluDataSet.lut_osmm_updates_processRow[] IncidOSMMUpdatesProcessFlags
        {
            get
            {
                if ((_osmmProcessFlags == null) || (_osmmProcessFlags.Length == 0))
                {
                    _osmmProcessFlags = (from m in _hluDataset.lut_osmm_updates_process
                                         select m).OrderBy(m => m.sort_order).ThenBy(m => m.description).ToArray();
                }

                HluDataSet.lut_osmm_updates_processRow[] osmmProcessFlags;
                osmmProcessFlags = AnyRowOSMMUpdatesProcess(-3).Concat(_osmmProcessFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                return osmmProcessFlags;
            }
            set { }
        }

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

        public HluDataSet.lut_osmm_updates_spatialRow[] IncidOSMMUpdatesSpatialFlags
        {
            get
            {
                if ((_osmmSpatialFlags == null) || (_osmmSpatialFlags.Length == 0))
                {
                    _osmmSpatialFlags = (from m in _hluDataset.lut_osmm_updates_spatial
                                         select m).OrderBy(m => m.sort_order).ThenBy(m => m.description).ToArray();
                }

                HluDataSet.lut_osmm_updates_spatialRow[] osmmSpatialFlags;
                osmmSpatialFlags = AnyRowOSMMUpdatesSpatial(-3).Concat(_osmmSpatialFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                return osmmSpatialFlags;
            }
            set { }
        }

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

        public HluDataSet.lut_osmm_updates_changeRow[] IncidOSMMUpdatesChangeFlags
        {
            get
            {
                if ((_osmmChangeFlags == null) || (_osmmChangeFlags.Length == 0))
                {
                    _osmmChangeFlags = (from m in _hluDataset.lut_osmm_updates_change
                                        select m).OrderBy(m => m.sort_order).ThenBy(m => m.description).ToArray();
                }

                HluDataSet.lut_osmm_updates_changeRow[] osmmChangeFlags;
                osmmChangeFlags = AnyRowOSMMUpdatesChange(-3).Concat(_osmmChangeFlags).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                return osmmChangeFlags;
            }
            set { }
        }

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
                string[] osmmUpdateStatuses;
                if (_viewModelMain.OSMMBulkUpdateMode == true)
                    osmmUpdateStatuses = ["Pending"];
                else
                    osmmUpdateStatuses = Settings.Default.OSMMUpdatesStatuses.Cast<string>().ToArray();

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

        private HluDataSet.lut_osmm_updates_processRow[] AnyRowOSMMUpdatesProcess(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_processRow anyRow = _hluDataset.lut_osmm_updates_process.Newlut_osmm_updates_processRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        private HluDataSet.lut_osmm_updates_spatialRow[] AnyRowOSMMUpdatesSpatial(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_spatialRow anyRow = _hluDataset.lut_osmm_updates_spatial.Newlut_osmm_updates_spatialRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        private HluDataSet.lut_osmm_updates_changeRow[] AnyRowOSMMUpdatesChange(int sortOrder)
        {
            HluDataSet.lut_osmm_updates_changeRow anyRow = _hluDataset.lut_osmm_updates_change.Newlut_osmm_updates_changeRow();
            anyRow.code = _codeAnyRow;
            anyRow.sort_order = sortOrder;
            anyRow.description = String.Empty;
            return [anyRow];
        }

        public string IncidOSMMUpdatesRejectedCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountRejected); }
        }

        public string IncidOSMMUpdatesIgnoredCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountIgnored); }
        }

        public string IncidOSMMUpdatesPendingCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountPending); }
        }

        public string IncidOSMMUpdatesProposedCount
        {
            get { return String.Format("{0:n0}", _osmmUpdatesCountProposed); }
        }

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

        /// <summary>
        /// Summarise the OSMM Updates in the database.
        /// </summary>
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
                    //string sqlColumns = String.Format("{0}, {1}, {2}, {3}, {4}, COUNT(*) As RecCount",
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.process_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.spatial_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.change_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.lut_osmm_habitat_xref.ihs_summaryColumn.ColumnName));
                    //string sqlGroupBy = String.Format("{0}, {1}, {2}, {3}, {4}",
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.process_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.spatial_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.change_flagColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.statusColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.lut_osmm_habitat_xref.ihs_summaryColumn.ColumnName));
                    //String sql = String.Format(
                    //    "SELECT DISTINCT {0} FROM {1},{2} WHERE {1}.{4} = {2}.{5} GROUP BY {3} ORDER BY {3}",
                    //    sqlColumns,
                    //    _db.QualifyTableName(_hluDataset.incid_osmm_updates.TableName),
                    //    _db.QualifyTableName(_hluDataset.lut_osmm_habitat_xref.TableName),
                    //    sqlGroupBy,
                    //    _db.QuoteIdentifier(_hluDataset.incid_osmm_updates.osmm_xref_idColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDataset.lut_osmm_habitat_xref.osmm_xref_idColumn.ColumnName));

                    dataReader = _db.ExecuteReader(sql,
                        _db.Connection.ConnectionTimeout, CommandType.Text);

                    if (dataReader == null) throw new Exception(String.Format("Error reading values from {0}", _hluDataset.incid_osmm_updates.TableName));

                    string processFlag, lastProcessFlag = null;
                    string spatialFlag, lastSpatialFlag = null;
                    string changeFlag, lastChangeFlag = null;
                    int status;
                    //string summary;
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

                        if (lastProcessFlag == null)
                            lastProcessFlag = processFlag;
                        if (lastSpatialFlag == null)
                            lastSpatialFlag = spatialFlag;
                        if (lastChangeFlag == null)
                            lastChangeFlag = changeFlag;

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
        /// Count the total number of OSMM Updates in the database.
        /// </summary>
        public List<OSMMUpdates> OSMMUpdatesSummaryTotal
        {
            get
            {
                return _tableTotal;
            }
        }

        #region Cursor

        /// <summary>
        /// Gets the cursor type to use when the cursor is over the window.
        /// </summary>
        /// <value>
        /// The window cursor type.
        /// </value>
        public Cursor WindowCursor { get { return _cursorType; } }

        public void ChangeCursor(Cursor cursorType)
        {
            _cursorType = cursorType;
            OnPropertyChanged(nameof(WindowCursor));
            if (cursorType == Cursors.Wait)
                DispatcherHelper.DoEvents();
        }

        #endregion

    }
    //---------------------------------------------------------------------
}
