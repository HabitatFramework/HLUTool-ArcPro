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
using System.Windows.Input;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using HLU.Data.Model;
using HLU.UI.ViewModel;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.UserControls;
using System.Collections.ObjectModel;
using HLU.UI.View;

namespace HLU.UI.ViewModel
{
    partial class ViewModelOptions : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;

        private ICommand _saveCommand;
        private ICommand _cancelCommand;
        //private ICommand _browseMapPathCommand;
        //private ICommand _browseExportPathCommand;
        private ICommand _browseSqlPathCommand;
        private string _displayName = "Options";

        private HluDataSet.incid_mm_polygonsDataTable _incidMMPolygonsTable = new();
        private List<int> _gisIDColumnOrdinals;

        // Database options
        private int? _dbConnectionTimeout = Settings.Default.DbConnectionTimeout;
        private int? _incidTablePageSize = Settings.Default.IncidTablePageSize;

        // GIS/Export options
        private int _preferredGis = Settings.Default.PreferredGis;
        private string _mapPath = Settings.Default.MapPath;
        private int? _minAutoZoom = Settings.Default.MinAutoZoom;
        private string _exportPath = Settings.Default.ExportPath;

        // History options
        private SelectionList<string> _historyColumns;
        private int? _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

        // Interface options
        private string _preferredHabitatClass = Settings.Default.PreferredHabitatClass;
        private bool _showGroupHeaders = Settings.Default.ShowGroupHeaders;
        private bool _showIHSTab = Settings.Default.ShowIHSTab;
        private bool _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
        private bool _showHabitatSuggestions = Settings.Default.ShowHabitatSecondariesSuggested;
        private bool _showNVCCodes = Settings.Default.ShowNVCCodes;
        private bool _showHabitatSummary = Settings.Default.ShowHabitatSummary;
        private string[] _showOSMMUpdatesOptions;
        private string _showOSMMUpdatesOption = Settings.Default.ShowOSMMUpdatesOption;

        private string _preferredSecondaryGroup = Settings.Default.PreferredSecondaryGroup;
        private string[] _secondaryCodeOrderOptions;
        private string _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;
        private string _secondaryCodeDelimiter = Settings.Default.SecondaryCodeDelimiter;

        // Updates options
        private int? _subsetUpdateAction = Settings.Default.SubsetUpdateAction;
        private string[] _clearIHSUpdateActions;
        private string _clearIHSUpdateAction = Settings.Default.ClearIHSUpdateAction;
        private bool _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;
        private bool _resetOSMMUpdatesStatus = Settings.Default.ResetOSMMUpdatesStatus;
        private int _habitatSecondaryCodeValidation = Settings.Default.HabitatSecondaryCodeValidation;
        private int _primarySecondaryCodeValidation = Settings.Default.PrimarySecondaryCodeValidation;
        private int _qualityValidation = Settings.Default.QualityValidation;
        private int _potentialPriorityDetermQtyValidation = Settings.Default.PotentialPriorityDetermQtyValidation;

        // Filter options
        private int? _getValueRows = Settings.Default.GetValueRows;
        private int? _warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;
        private string _sqlPath = Settings.Default.SqlPath;

        // Dates options
        private string _seasonSpring = Settings.Default.SeasonNames[0];
        private string _seasonSummer = Settings.Default.SeasonNames[1];
        private string _seasonAutumn = Settings.Default.SeasonNames[2];
        private string _seasonWinter = Settings.Default.SeasonNames[3];
        private string _vagueDateDelimiter = Settings.Default.VagueDateDelimiter;

        // Bulk Update options
        private bool _bulkDeleteOrphanBapHabitats = Settings.Default.BulkUpdateDeleteOrphanBapHabitats;
        private bool _bulkDeletePotentialBapHabitats = Settings.Default.BulkUpdateDeletePotentialBapHabitats;
        private bool _bulkDeleteSecondaryCodes = Settings.Default.BulkUpdateDeleteSecondaryCodes;
        private bool _bulkCreateHistoryRecords = Settings.Default.BulkUpdateCreateHistoryRecords;
        private bool _bulkDeleteIHSCodes = Settings.Default.BulkUpdateDeleteIHSCodes;
        private string _bulkDeterminationQuality = Settings.Default.BulkUpdateDeterminationQuality;
        private string _bulkInterpretationQuality = Settings.Default.BulkUpdateInterpretationQuality;
        private Nullable<int> _bulkOSMMSourceId = Settings.Default.BulkOSMMSourceId;

        // Backup variables
        private string _bakMapPath;
        //private string _bakExportPath;
        private string _bakSqlPath;

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public ViewModelOptions()
        {
            NavigationItems =
        [
            new() { Name = "Database", Content = new DatabaseOptions() },
            new () { Name = "Export", Content = new ExportOptions() },
            new () { Name = "History", Content = new HistoryOptions() },
            new () { Name = "Interface", Content = new InterfaceOptions() },
            new () { Name = "Updates", Content = new UpdatesOptions() },
            new () { Name = "SQL", Content = new SQLOptions() },
            new () { Name = "Dates", Content = new DatesOptions() },
            new () { Name = "Bulk Update", Content = new BulkUpdateOptions() }
        ];

            SelectedView = NavigationItems.First();
        }

        /// <summary>
        /// Get the default values from settings.
        /// </summary>
        /// <remarks></remarks>
        public ViewModelOptions(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;

            _gisIDColumnOrdinals = Settings.Default.GisIDColumnOrdinals.Cast<string>()
                .Select(s => Int32.Parse(s)).ToList();

            _historyColumns = new SelectionList<string>(_incidMMPolygonsTable.Columns.Cast<DataColumn>()
                .Where(c => !_gisIDColumnOrdinals.Contains(c.Ordinal)
                    && !c.ColumnName.StartsWith("shape_"))
                .Select(c => EscapeAccessKey(c.ColumnName)).ToArray());

            List<int> historyColumnOrdinals = Settings.Default.HistoryColumnOrdinals.Cast<string>()
                .Select(s => Int32.Parse(s)).Where(i => !_gisIDColumnOrdinals.Contains(i) &&
                    !_incidMMPolygonsTable.Columns[i].ColumnName.StartsWith("shape_")).ToList();

            foreach (SelectionItem<string> si in _historyColumns)
                si.IsSelected = historyColumnOrdinals.Contains(
                    _incidMMPolygonsTable.Columns[UnescapeAccessKey(si.Item)].Ordinal);

            // Store the map path so that it can be reset if the user
            // cancels updates to the options.
            _bakMapPath = _mapPath;


            NavigationItems =
        [
            new() { Name = "Database", Content = new DatabaseOptions() }
            //new () { Name = "GIS/Export", Content = new GisView() },
            //new () { Name = "History", Content = new HistoryView() }
        ];

            SelectedView = NavigationItems.First();
        }

        private string EscapeAccessKey(string s)
        {
            return s.Replace("_", "__");
        }

        private string UnescapeAccessKey(string s)
        {
            return s.Replace("__", "_");
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
        public delegate void RequestCloseEventHandler(bool saveSettings);

        // declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion

        #region Navigation Items

        private NavigationItem _selectedView;
        public NavigationItem SelectedView
        {
            get => _selectedView;
            set
            {
                _selectedView = value;
                OnPropertyChanged(nameof(SelectedView));
            }
        }
        #endregion Navigation Items

        #region Save Command

        /// <summary>
        /// Create Save button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    Action<object> saveAction = new(this.SaveCommandClick);
                    _saveCommand = new RelayCommand(saveAction, param => this.CanSave);
                }

                return _saveCommand;
            }
        }

        /// <summary>
        /// Handles event when Save button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void SaveCommandClick(object param)
        {
            // Database options
            Settings.Default.DbConnectionTimeout = (int)_dbConnectionTimeout;
            Settings.Default.IncidTablePageSize = (int)_incidTablePageSize;

            // GIS/Export options
            Settings.Default.PreferredGis = _preferredGis;
            Settings.Default.MapPath = _mapPath;
            Settings.Default.MinAutoZoom = (int)_minAutoZoom;
            Settings.Default.ExportPath = _exportPath;

            // History options
            //TOOO: Fix this
            //Settings.Default.HistoryColumnOrdinals =
            //[
            //    .. _historyColumns.Where(c => c.IsSelected)
            //        .Select(c => _incidMMPolygonsTable.Columns[UnescapeAccessKey(c.Item)].Ordinal.ToString()).ToArray(),
            //];
            Settings.Default.HistoryDisplayLastN = (int)_historyDisplayLastN;

            // Interface options
            Settings.Default.PreferredHabitatClass = _preferredHabitatClass;
            Settings.Default.ShowGroupHeaders = _showGroupHeaders;
            Settings.Default.ShowIHSTab = _showIHSTab;
            Settings.Default.ShowSourceHabitatGroup = _showSourceHabitatGroup;
            Settings.Default.ShowHabitatSecondariesSuggested = _showHabitatSuggestions;
            Settings.Default.ShowNVCCodes = _showNVCCodes;
            Settings.Default.ShowHabitatSummary = _showHabitatSummary;
            Settings.Default.ShowOSMMUpdatesOption = _showOSMMUpdatesOption;

            Settings.Default.PreferredSecondaryGroup = _preferredSecondaryGroup;
            Settings.Default.SecondaryCodeOrder = _secondaryCodeOrder;
            Settings.Default.SecondaryCodeDelimiter = _secondaryCodeDelimiter;

            // Updates options
            Settings.Default.SubsetUpdateAction = (int)_subsetUpdateAction;
            Settings.Default.ClearIHSUpdateAction = _clearIHSUpdateAction;
            Settings.Default.NotifyOnSplitMerge = _notifyOnSplitMerge;
            Settings.Default.ResetOSMMUpdatesStatus = _resetOSMMUpdatesStatus;
            Settings.Default.HabitatSecondaryCodeValidation = _habitatSecondaryCodeValidation;
            Settings.Default.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation;
            Settings.Default.QualityValidation = _qualityValidation;
            Settings.Default.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation;

            // Filter options
            Settings.Default.GetValueRows = (int)_getValueRows;
            Settings.Default.WarnBeforeGISSelect = (int)_warnBeforeGISSelect;
            Settings.Default.SqlPath = _sqlPath;

            // Dates options
            Settings.Default.SeasonNames[0] = _seasonSpring;
            Settings.Default.SeasonNames[1] = _seasonSummer;
            Settings.Default.SeasonNames[2] = _seasonAutumn;
            Settings.Default.SeasonNames[3] = _seasonWinter;
            Settings.Default.VagueDateDelimiter = _vagueDateDelimiter;

            // Bulk update options
            Settings.Default.BulkUpdateDeleteOrphanBapHabitats = _bulkDeleteOrphanBapHabitats;
            Settings.Default.BulkUpdateDeletePotentialBapHabitats = _bulkDeletePotentialBapHabitats;
            Settings.Default.BulkUpdateDeleteSecondaryCodes = _bulkDeleteSecondaryCodes;
            Settings.Default.BulkUpdateCreateHistoryRecords = _bulkCreateHistoryRecords;
            Settings.Default.BulkUpdateDeleteIHSCodes = _bulkDeleteIHSCodes;

            // OSMM bulk update options
            Settings.Default.BulkUpdateDeterminationQuality = _bulkDeterminationQuality;
            Settings.Default.BulkUpdateInterpretationQuality = _bulkInterpretationQuality;
            Settings.Default.BulkOSMMSourceId = (int)_bulkOSMMSourceId;
            //---------------------------------------------------------------------

            Settings.Default.Save();

            this.RequestClose(true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool CanSave { get { return String.IsNullOrEmpty(Error); } }

        #endregion

        #region Cancel Command

        /// <summary>
        /// Create Cancel button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    Action<object> cancelAction = new(this.CancelCommandClick);
                    _cancelCommand = new RelayCommand(cancelAction);
                }

                return _cancelCommand;
            }
        }

        /// <summary>
        /// Handles event when Cancel button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CancelCommandClick(object param)
        {
            // Reset the map path incase the user has selected a new
            // path and is now cancelling the changes.
            Settings.Default.MapPath = _bakMapPath;
            this.RequestClose(false);
        }

        #endregion

        #region Database

        public int? DbConnectionTimeout
        {
            get { return _dbConnectionTimeout; }
            set { _dbConnectionTimeout = value; }
        }

        public int? IncidTablePageSize
        {
            get { return _incidTablePageSize; }
            set { _incidTablePageSize = value; }
        }

        #endregion

        #region GIS/Export

        /// <summary>
        /// Gets the default minimum auto zoom scale text.
        /// </summary>
        /// <value>
        /// The Minimum auto zoom scale text.
        /// </value>
        public string MinAutoZoomText
        {
            get
            {
                string distUnits = Settings.Default.MapDistanceUnits;
                return string.Format("Minimum auto zoom size [{0}]", distUnits);
            }
        }

        /// <summary>
        /// Gets or sets the default minimum auto zoom scale.
        /// </summary>
        /// <value>
        /// The Minimum auto zoom scale.
        /// </value>
        public int? MinAutoZoom
        {
            get { return _minAutoZoom; }
            set { _minAutoZoom = value; }
        }

        #endregion

        #region History

        public SelectionList<string> HistoryColumns
        {
            get { return _historyColumns; }
            set { _historyColumns = value; }
        }

        public int? HistoryDisplayLastN
        {
            get { return _historyDisplayLastN; }
            set { _historyDisplayLastN = value; }
        }

        #endregion

        #region Interface

        //---------------------------------------------------------------------
        // CHANGED: CR29 (Habitat classification and conversion to IHS)
        // Add an option for the user to select their preferred
        // habitat class which will be automatically selected when
        // the tool first starts.
        //
        /// <summary>
        /// Gets or sets the list of possible habitat class codes.
        /// </summary>
        /// <value>
        /// The list of possible habitat class codes.
        /// </value>
        public HluDataSet.lut_habitat_classRow[] HabitatClassCodes
        {
            get { return ViewModelWindowMain.HabitatClasses; }
            set { }
        }

        public string PreferredHabitatClass
        {
            get
            {
                var q = HabitatClassCodes.Where(h => h.code == _preferredHabitatClass);
                if (q.Any())
                    return _preferredHabitatClass;
                else
                    return null;
            }
            set
            {
                _preferredHabitatClass = value;
            }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the preferred option to show or hide group headers.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding group headers.
        /// </value>
        public bool ShowGroupHeaders
        {
            get { return _showGroupHeaders; }
            set { _showGroupHeaders = value; }
        }

        public bool ShowIHSTab
        {
            get { return _showIHSTab; }
            set { _showIHSTab = value; }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat categories.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat categories.
        /// </value>
        public bool ShowSourceHabitatGroup
        {
            get { return _showSourceHabitatGroup; }
            set { _showSourceHabitatGroup = value; }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat suggestions.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat suggestions.
        /// </value>
        public bool ShowHabitatSuggestions
        {
            get { return _showHabitatSuggestions; }
            set { _showHabitatSuggestions = value; }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide NVC Codes.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding NVC Codes.
        /// </value>
        public bool ShowNVCCodes
        {
            get { return _showNVCCodes; }
            set { _showNVCCodes = value; }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat summary.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat summary.
        /// </value>
        public bool ShowHabitatSummary
        {
            get { return _showHabitatSummary; }
            set { _showHabitatSummary = value; }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        // A new option to enable the user to determine whether to show
        // the OSMM update attributes for the current incid.
        //
        /// <summary>
        /// Gets or sets the list of available show OSMM Update options from
        /// the class.
        /// </summary>
        /// <value>
        /// The list of subset update actions.
        /// </value>
        public string[] ShowOSMMUpdatesOptions
        {
            get
            {
                if (_showOSMMUpdatesOptions == null)
                {
                    _showOSMMUpdatesOptions = Settings.Default.ShowOSMMUpdatesOptions.Cast<string>().ToArray();
                }

                return _showOSMMUpdatesOptions;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred show OSMM Update option.
        /// </summary>
        /// <value>
        /// The preferred show OSMM Update option.
        /// </value>
        public string ShowOSMMUpdatesOption
        {
            get { return _showOSMMUpdatesOption; }
            set
            {
                _showOSMMUpdatesOption = value;
            }
        }
        //---------------------------------------------------------------------

        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodes
        {
            get { return ViewModelWindowMain.SecondaryGroupsAll; }
            set { }
        }

        public string PreferredSecondaryGroup
        {
            get
            {
                var q = SecondaryGroupCodes.Where(h => h.code == _preferredSecondaryGroup);
                if (q.Any())
                    return _preferredSecondaryGroup;
                else
                    return null;
            }
            set
            {
                _preferredSecondaryGroup = value;
            }
        }

        /// <summary>
        /// Gets or sets the secondary code order options.
        /// </summary>
        /// <value>
        /// The secondary code order options.
        /// </value>
        public string[] SecondaryCodeOrderOptions
        {
            get
            {
                if (_secondaryCodeOrderOptions == null)
                {
                    _secondaryCodeOrderOptions = Settings.Default.SecondaryCodeOrderOptions.Cast<string>().ToArray();
                }

                return _secondaryCodeOrderOptions;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the secondary code order choice.
        /// </summary>
        /// <value>
        /// The secondary code order choice.
        /// </value>
        public string SecondaryCodeOrder
        {
            get { return _secondaryCodeOrder; }
            set
            {
                _secondaryCodeOrder = value;
            }
        }

        /// <summary>
        /// Gets or sets the secondary code delimiter.
        /// </summary>
        /// <value>
        /// The secondary code delimiter.
        /// </value>
        public string SecondaryCodeDelimiter
        {
            get { return _secondaryCodeDelimiter; }
            set
            {
                _secondaryCodeDelimiter = value;
            }
        }

        #endregion

        #region Updates

        /// <summary>
        /// Gets or sets the list of available subset update actions from
        /// the enum.
        /// </summary>
        /// <value>
        /// The list of subset update actions.
        /// </value>
        public SubsetUpdateActions[] SubsetUpdateActions
        {
            get
            {
                return Enum.GetValues(typeof(SubsetUpdateActions)).Cast<SubsetUpdateActions>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred subset update action.
        /// </summary>
        /// <value>
        /// The preferred subset update action.
        /// </value>
        public SubsetUpdateActions? SubsetUpdateAction
        {
            get { return (SubsetUpdateActions)_subsetUpdateAction; }
            set
            {
                _subsetUpdateAction = (int)value;
            }
        }

        /// <summary>
        /// Gets or sets the clear IHS update actions.
        /// </summary>
        /// <value>
        /// The clear IHS update actions.
        /// </value>
        public string[] ClearIHSUpdateActions
        {
            get
            {
                if (_clearIHSUpdateActions == null)
                {
                    _clearIHSUpdateActions = Settings.Default.ClearIHSUpdateActions.Cast<string>().ToArray();
                }

                return _clearIHSUpdateActions;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the clear IHS update action.
        /// </summary>
        /// <value>
        /// The clear IHS update action.
        /// </value>
        public string ClearIHSUpdateAction
        {
            get { return _clearIHSUpdateAction; }
            set
            {
                _clearIHSUpdateAction = value;
            }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR39 (Split and merge complete messages)
        // A new option to enable the user to determine if they
        // want to be notified following the completion of a
        // split or merge.
        //
        /// <summary>
        /// Gets or sets the choice of whether the user will
        /// be notified when a split or merge has completed.
        /// </summary>
        /// <value>
        /// If the user will be notified after a split or merge.
        /// </value>
        public bool NotifyOnSplitMerge
        {
            get { return _notifyOnSplitMerge; }
            set { _notifyOnSplitMerge = value; }
        }
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        // A new option to enable the user to determine whether to reset
        // the OSMM update process flag when manually updating the current
        // incid.
        // 
        /// <summary>
        /// Gets or sets the preferred option to reset the OSMM Update
        /// process flag when applying manual updates.
        /// </summary>
        /// <value>
        /// The preferred option to reset the OSMM Update process flag
        /// when applying manual updates.
        /// </value>
        public bool ResetOSMMUpdatesStatus
        {
            get { return _resetOSMMUpdatesStatus; }
            set { _resetOSMMUpdatesStatus = value; }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Gets the habitat/secondary code validation options.
        /// </summary>
        /// <value>
        /// The primary/secondary code validation options.
        /// </value>
        public HabitatSecondaryCodeValidationOptions[] HabitatSecondaryCodeValidationOptions
        {
            get
            {
                return Enum.GetValues(typeof(HabitatSecondaryCodeValidationOptions)).Cast<HabitatSecondaryCodeValidationOptions>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the habitat/secondary code validation choice.
        /// </summary>
        /// <value>
        /// The primary/secondary code validation choice.
        /// </value>
        public HabitatSecondaryCodeValidationOptions? HabitatSecondaryCodeValidation
        {
            get { return (HabitatSecondaryCodeValidationOptions)_habitatSecondaryCodeValidation; }
            set
            {
                _habitatSecondaryCodeValidation = (int)value;
            }
        }

        /// <summary>
        /// Gets the primary/secondary code validation options.
        /// </summary>
        /// <value>
        /// The primary/secondary code validation options.
        /// </value>
        public PrimarySecondaryCodeValidationOptions[] PrimarySecondaryCodeValidationOptions
        {
            get
            {
                return Enum.GetValues(typeof(PrimarySecondaryCodeValidationOptions)).Cast<PrimarySecondaryCodeValidationOptions>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the primary/secondary code validation choice.
        /// </summary>
        /// <value>
        /// The primary/secondary code validation choice.
        /// </value>
        public PrimarySecondaryCodeValidationOptions? PrimarySecondaryCodeValidation
        {
            get { return (PrimarySecondaryCodeValidationOptions)_primarySecondaryCodeValidation; }
            set
            {
                _primarySecondaryCodeValidation = (int)value;
            }
        }

        /// <summary>
        /// Gets or sets the quality validation options.
        /// </summary>
        /// <value>
        /// The quality validation options.
        /// </value>
        public QualityValidationOptions[] QualityValidationOptions
        {
            get
            {
                return Enum.GetValues(typeof(QualityValidationOptions)).Cast<QualityValidationOptions>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the quality validation choice.
        /// </summary>
        /// <value>
        /// The quality validation choice.
        /// </value>
        public QualityValidationOptions? QualityValidation
        {
            get { return (QualityValidationOptions)_qualityValidation; }
            set
            {
                _qualityValidation = (int)value;
            }
        }

        /// <summary>
        /// Gets or sets the quality validation options.
        /// </summary>
        /// <value>
        /// The quality validation options.
        /// </value>
        public PotentialPriorityDetermQtyValidationOptions[] PotentialPriorityDetermQtyValidationOptions
        {
            get
            {
                return Enum.GetValues(typeof(PotentialPriorityDetermQtyValidationOptions)).Cast<PotentialPriorityDetermQtyValidationOptions>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the quality validation choice.
        /// </summary>
        /// <value>
        /// The quality validation choice.
        /// </value>
        public PotentialPriorityDetermQtyValidationOptions? PotentialPriorityDetermQtyValidation
        {
            get { return (PotentialPriorityDetermQtyValidationOptions)_potentialPriorityDetermQtyValidation; }
            set
            {
                _potentialPriorityDetermQtyValidation = (int)value;
            }
        }
        #endregion

        #region Filter

        //---------------------------------------------------------------------
        // CHANGED: CR5 (Select by attributes interface)
        // A new option to enable the user to select how many rows
        // to retrieve when getting values for a data column when
        // building a sql query.
        //
        /// <summary>
        /// Gets or sets the maximum number of value rows to retrieve.
        /// </summary>
        /// <value>
        /// The maximum get value rows.
        /// </value>
        public int? GetValueRows
        {
            get { return _getValueRows; }
            set { _getValueRows = value; }
        }
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // CHANGED: CR5 (Select by attributes interface)
        // A new option to enable the user to determine when to warn
        // the user before performing a GIS selection.
        //
        /// <summary>
        /// Gets or sets the list of available warn before GIS selection
        /// options from the enum.
        /// </summary>
        /// <value>
        /// The list of options for warning before any GIS selection.
        /// </value>
        public WarnBeforeGISSelect[] WarnBeforeGISSelectOptions
        {
            get
            {
                return Enum.GetValues(typeof(WarnBeforeGISSelect)).Cast<WarnBeforeGISSelect>()
                    .ToArray();
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the preferred warning before any GIS selection option.
        /// </summary>
        /// <value>
        /// The preferred option for warning before any GIS selection.
        /// </value>
        public WarnBeforeGISSelect? WarnBeforeGISSelect
        {
            get { return (WarnBeforeGISSelect)_warnBeforeGISSelect; }
            set
            {
                _warnBeforeGISSelect = (int)value;
            }
        }
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // CHANGED: CR5 (Select by attributes interface)
        // A new option to enable the user to set the default
        // folder for saving and loading SQL queries.
        //
        /// <summary>
        /// Get the browse SQL path command.
        /// </summary>
        /// <value>
        /// The browse SQL path command.
        /// </value>
        public ICommand BrowseSqlPathCommand
        {
            get
            {
                if (_browseSqlPathCommand == null)
                {
                    Action<object> browseSqlPathAction = new(this.BrowseSqlPathClicked);
                    _browseSqlPathCommand = new RelayCommand(browseSqlPathAction);
                }

                return _browseSqlPathCommand;
            }
        }

        /// <summary>
        /// Action when the browse SQL path button is clicked.
        /// </summary>
        /// <param name="param"></param>
        private void BrowseSqlPathClicked(object param)
        {
            _bakSqlPath = _sqlPath;
            SqlPath = String.Empty;
            SqlPath = GetSqlPath();

            if (String.IsNullOrEmpty(SqlPath))
            {
                SqlPath = _bakSqlPath;
            }
            OnPropertyChanged(nameof(SqlPath));
        }

        /// <summary>
        /// Gets or sets the default SQL path.
        /// </summary>
        /// <value>
        /// The SQL path.
        /// </value>
        public string SqlPath
        {
            get { return _sqlPath; }
            set { _sqlPath = value; }
        }

        /// <summary>
        /// Prompt the user to set the default SQL path.
        /// </summary>
        /// <returns></returns>
        public static string GetSqlPath()
        {
            try
            {
                string sqlPath = Settings.Default.SqlPath;

                FolderBrowserDialog openFolderDlg = new()
                {
                    Description = "Select Sql Query Default Directory",
                    SelectedPath = sqlPath,
                    //openFolderDlg.RootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    ShowNewFolderButton = true
                };
                if (openFolderDlg.ShowDialog() == DialogResult.OK)
                {
                    if (Directory.Exists(openFolderDlg.SelectedPath))
                        return openFolderDlg.SelectedPath;
                }
            }
            catch { }

            return null;
        }
        //---------------------------------------------------------------------

        #endregion

        #region Date

        public string SeasonSpring
        {
            get { return _seasonSpring; }
            set { _seasonSpring = value; }
        }

        public string SeasonSummer
        {
            get { return _seasonSummer; }
            set { _seasonSummer = value; }
        }

        public string SeasonAutumn
        {
            get { return _seasonAutumn; }
            set { _seasonAutumn = value; }
        }

        public string SeasonWinter
        {
            get { return _seasonWinter; }
            set { _seasonWinter = value; }
        }

        public string VagueDateDelimiter
        {
            get { return _vagueDateDelimiter; }
            set
            {
                if (value != CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator)
                    _vagueDateDelimiter = value;
            }
        }

        #endregion

        #region Bulk Update

        /// <summary>
        /// Checks if the user has bulk update authority.
        /// </summary>
        public bool CanBulkUpdate
        {
            get
            {
                return _viewModelMain.CanBulkUpdate;
            }
        }

        /// <summary>
        /// Gets or sets the default option to delete invalid secondary
        /// codes when applying bulk updates.
        /// </summary>
        /// <value>
        /// The default option for deleting invalid secondary codes.
        /// </value>
        public bool BulkDeleteSecondaryCodes
        {
            get { return _bulkDeleteSecondaryCodes; }
            set
            {
                _bulkDeleteSecondaryCodes = value;
            }
        }

        /// <summary>
        /// Gets or sets the default option to delete orphan bap habitats
        /// when applying bulk updates.
        /// </summary>
        /// <value>
        /// The default option for deleting orphan bap habitats.
        /// </value>
        public bool BulkDeleteOrphanBapHabitats
        {
            get { return _bulkDeleteOrphanBapHabitats; }
            set { _bulkDeleteOrphanBapHabitats = value; }
        }

        /// <summary>
        /// Gets or sets the default option to delete potential bap habitats
        /// when applying bulk updates.
        /// </summary>
        /// <value>
        /// The default option for deleting potential bap habitats.
        /// </value>
        public bool BulkDeletePotentialBapHabitats
        {
            get { return _bulkDeletePotentialBapHabitats; }
            set { _bulkDeletePotentialBapHabitats = value; }
        }

        /// <summary>
        /// Gets or sets the default option to create history records
        /// when applying bulk updates.
        /// </summary>
        /// <value>
        /// The default option for creating history records.
        /// </value>
        public bool BulkCreateHistoryRecords
        {
            get { return _bulkCreateHistoryRecords; }
            set { _bulkCreateHistoryRecords = value; }
        }

        /// <summary>
        /// Gets or sets the default option to delete IHS codes
        /// when applying bulk updates.
        /// </summary>
        /// <value>
        /// The default option for deleting IHS codes.
        /// </value>
        public bool BulkDeleteIHSCodes
        {
            get { return _bulkDeleteIHSCodes; }
            set { _bulkDeleteIHSCodes = value; }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process bulk OSMM Updates
        //
        /// <summary>
        /// Gets or sets the list of determination qualities that
        /// can be used when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The list of determination qualities.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] BulkDeterminationQualityCodes
        {
            get { return _viewModelMain.BapDeterminationQualityCodesAuto; }
        }

        /// <summary>
        /// Gets or sets the default option for the determination
        /// quality when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The default option for determination quality.
        /// </value>
        public string BulkDeterminationQuality
        {
            get { return _bulkDeterminationQuality; }
            set { _bulkDeterminationQuality = value; }
        }

        /// <summary>
        /// Gets or sets the list of interpretation qualities that
        /// can be used when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The list of interpretation qualities.
        /// </value>
        public HluDataSet.lut_quality_interpretationRow[] BulkInterpretationQualityCodes
        {
            get { return _viewModelMain.InterpretationQualityCodes; }
        }

        /// <summary>
        /// Gets or sets the default option for the interpretation
        /// quality when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The default option for interpretation quality.
        /// </value>
        public string BulkInterpretationQuality
        {
            get { return _bulkInterpretationQuality; }
            set { _bulkInterpretationQuality = value; }
        }

        public HluDataSet.lut_sourcesRow[] SourceNames
        {
            get { return _viewModelMain.SourceNames; }
        }

        /// <summary>
        /// Gets or sets the default option for the OSMM
        /// source name when performing OSMM bulk updates.
        /// </summary>
        /// <value>
        /// The default option for the OSMM source name.
        /// </value>
        public Nullable<int> OSMMSourceId
        {
            get { return _bulkOSMMSourceId; }
            set { _bulkOSMMSourceId = value; }
        }
        //---------------------------------------------------------------------

        #endregion

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                StringBuilder error = new();

                // Database options
                if (Convert.ToInt32(DbConnectionTimeout) <= 0 || DbConnectionTimeout == null)
                    error.Append("\n" + "Enter a database timeout greater than 0 seconds.");
                if (Convert.ToInt32(IncidTablePageSize) <= 0 || IncidTablePageSize == null)
                    error.Append("\n" + "Enter a database page size greater than 0 rows.");

                // GIS options
                if (Convert.ToInt32(MinAutoZoom) < 100 || MinAutoZoom == null)
                    error.Append("\n" + "Minimum auto zoom scale must be at least 100.");
                if (Convert.ToInt32(MinAutoZoom) > Settings.Default.MaxAutoZoom)
                    error.Append("\n" + String.Format("Minimum auto zoom scale must not be greater than {0}.", Settings.Default.MaxAutoZoom));

                // History options
                if (Convert.ToInt32(HistoryDisplayLastN) <= 0 || HistoryDisplayLastN == null)
                    error.Append("\n" + "Number of history rows to be displayed must be greater than 0.");

                // Interface options
                if (PreferredHabitatClass == null)
                    error.Append("Select your preferred habitat class.");
                if (ShowOSMMUpdatesOption == null)
                    error.Append("Select the option of when to display any OSMM Updates.");
                if (PreferredSecondaryGroup == null)
                    error.Append("Select your preferred secondary group.");
                if (HabitatSecondaryCodeValidation == null)
                    error.Append("Select option of when to validate habitat/secondary codes.");
                if (PrimarySecondaryCodeValidation == null)
                    error.Append("Select option of when to validate primary/secondary codes.");
                if (String.IsNullOrEmpty(SecondaryCodeDelimiter))
                    error.Append("\n" + "You must enter a secondary code delimiter character.");
                else if (SecondaryCodeDelimiter.Length > 2)
                    error.Append("\n" + "Secondary code delimiter must be one or two characters.");
                else
                {
                    Match m = secondaryCodeDelimeterRegex().Match(SecondaryCodeDelimiter);
                    if (m.Success == true)
                        error.Append("\n" + "Secondary code delimiter cannot contain letters or numbers.");
                }

                // Update options
                if (SubsetUpdateAction == null)
                    error.Append("Select the action to take when updating an incid subset.");
                if (ClearIHSUpdateAction == null)
                    error.Append("Select when to clear IHS codes after an update.");

                // Filter options
                if (Convert.ToInt32(GetValueRows) <= 0 || GetValueRows == null)
                    error.Append("\n" + "Number of value rows to be retrieved must be greater than 0.");
                if (Convert.ToInt32(GetValueRows) > Settings.Default.MaxGetValueRows)
                    error.Append("\n" + String.Format("Number of value rows to be retrieved must not be greater than {0}.", Settings.Default.MaxGetValueRows));

                // Date options
                if (String.IsNullOrEmpty(SeasonSpring))
                    error.Append("\n" + "You must enter a season name for spring.");
                if (String.IsNullOrEmpty(SeasonSummer))
                    error.Append("\n" + "You must enter a season name for summer.");
                if (String.IsNullOrEmpty(SeasonAutumn))
                    error.Append("\n" + "You must enter a season name for autumn.");
                if (String.IsNullOrEmpty(SeasonWinter))
                    error.Append("\n" + "You must enter a season name for winter.");
                if (String.IsNullOrEmpty(VagueDateDelimiter))
                    error.Append("\n" + "You must enter a vague date delimiter character.");
                else if (VagueDateDelimiter.Length > 1)
                    error.Append("\n" + "Vague date delimiter must be a single character.");
                else
                {
                    Match m = vagueDateDelimeterRegex().Match(VagueDateDelimiter);
                    if (m.Success == true)
                        error.Append("\n" + "Vague date delimiter cannot contain letters or numbers.");
                }

                // Bulk Update options
                if (BulkDeterminationQuality == null)
                    error.Append("Select the default determination quality for new priority habitats.");
                if (BulkInterpretationQuality == null)
                    error.Append("Select the default interpration quality for new priority habitats.");
                if (OSMMSourceId == null)
                    error.Append("Select the default source name for OS MasterMap.");

                if (error.Length > 0)
                    return error.ToString();
                else
                    return null;
            }
        }

        public string this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    // Database options
                    case "DbConnectionTimeout":
                        if (Convert.ToInt32(DbConnectionTimeout) <= 0 || DbConnectionTimeout == null)
                            error = "Error: Enter a database timeout greater than 0 seconds.";
                        break;
                    case "IncidTablePageSize":
                        if (Convert.ToInt32(IncidTablePageSize) <= 0 || IncidTablePageSize == null)
                            error = "Error: Enter a database page size greater than 0 rows.";
                        if (Convert.ToInt32(IncidTablePageSize) > 1000 || IncidTablePageSize == null)
                            error = "Error: Enter a database page size no more than 1000 rows.";
                        break;

                    // GIS options
                    case "MinAutoZoom":
                        if (Convert.ToInt32(MinAutoZoom) < 100 || MinAutoZoom == null)
                            error = "Error: Minimum auto zoom scale must be at least 100.";
                        if (Convert.ToInt32(MinAutoZoom) > Settings.Default.MaxAutoZoom)
                            error = String.Format("Error: Minimum auto zoom scale must not be greater than {0}.", Settings.Default.MaxAutoZoom);
                        break;

                    // History options
                    case "HistoryDisplayLastN":
                        if (Convert.ToInt32(HistoryDisplayLastN) <= 0 || HistoryDisplayLastN == null)
                            error = "Error: Number of history rows to be displayed must be greater than 0.";
                        break;

                    // Interface options
                    case "PreferredHabitatClass":
                        if (PreferredHabitatClass == null)
                            error = "Error: Select your preferred habitat class.";
                        break;
                    case "ShowOSMMUpdatesOption":
                        if (ShowOSMMUpdatesOption == null)
                            error = "Error: Select option of when to display any OSMM Updates.";
                        break;
                    case "PreferredSecondaryGroup":
                        if (PreferredSecondaryGroup == null)
                            error = "Error: Select your preferred secondary group.";
                        break;
                    case "SecondaryCodeDelimiter":
                        if (String.IsNullOrEmpty(SecondaryCodeDelimiter))
                            error = "Error: You must enter a secondary code delimiter character.";
                        else if (SecondaryCodeDelimiter.Length > 2)
                            error = "Error: Secondary code delimiter must be one or two characters.";
                        else
                        {
                            Match m = secondaryCodeDelimeterRegex().Match(SecondaryCodeDelimiter);
                            if (m.Success == true)
                                error = "Error: Secondary code delimiter cannot contain letters or numbers.";
                        }
                        break;

                    // Update options
                    case "SubsetUpdateAction":
                        if (SubsetUpdateAction == null)
                            error = "Error: Select the action to take when updating an incid subset.";
                        break;
                    case "ClearIHSUpdateAction":
                        if (ClearIHSUpdateAction == null)
                            error = "Error: Select when to clear IHS codes after an update.";
                        break;
                    case "HabitatSecondaryCodeValidation":
                        if (HabitatSecondaryCodeValidation == null)
                            error = "Error: Select option of when to validate habitat/secondary codes.";
                        break;
                    case "PrimarySecondaryCodeValidation":
                        if (PrimarySecondaryCodeValidation == null)
                            error = "Error: Select option of when to validate primary/secondary codes.";
                        break;
                    case "QualityValidation":
                        if (QualityValidation == null)
                            error = "Error: Select option of when to validate determination and interpretation quality.";
                        break;
                    case "PotentialPriorityDetermQtyValidation":
                        if (PotentialPriorityDetermQtyValidation == null)
                            error = "Error: Select option of when to validate potential priority habitat determination quality.";
                        break;

                    // Filter options
                    case "GetValueRows":
                        if (Convert.ToInt32(GetValueRows) <= 0 || GetValueRows == null)
                            error = "Error: Number of value rows to be retrieved must be greater than 0.";
                        if (Convert.ToInt32(GetValueRows) > Settings.Default.MaxGetValueRows)
                            error = String.Format("Error: Number of value rows to be retrieved must not be greater than {0}.", Settings.Default.MaxGetValueRows);
                        break;

                    // Date options
                    case "SeasonSpring":
                        if (String.IsNullOrEmpty(SeasonSpring))
                            error = "Error: You must enter a season name for spring.";
                        break;
                    case "SeasonSummer":
                        if (String.IsNullOrEmpty(SeasonSummer))
                            error = "Error: You must enter a season name for summer.";
                        break;
                    case "SeasonAutumn":
                        if (String.IsNullOrEmpty(SeasonAutumn))
                            error = "Error: You must enter a season name for autumn.";
                        break;
                    case "SeasonWinter":
                        if (String.IsNullOrEmpty(SeasonWinter))
                            error = "Error: You must enter a season name for winter.";
                        break;
                    case "VagueDateDelimiter":
                        if (String.IsNullOrEmpty(VagueDateDelimiter))
                            error = "Error: You must enter a vague date delimiter character.";
                        else if (VagueDateDelimiter.Length > 1)
                            error = "Error: Vague date delimiter must be a single character.";
                        else
                        {
                            Match m = vagueDateDelimeterRegex().Match(VagueDateDelimiter);
                            if (m.Success == true)
                                error = "Error: Vague date delimiter cannot contain letters or numbers.";
                        }
                        break;

                    // Bulk Update options
                    case "BulkDeterminationQuality":
                        if (BulkDeterminationQuality == null)
                            error = "Error: Select the default determination quality for new priority habitats.";
                        break;
                    case "BulkInterpretationQuality":
                        if (BulkInterpretationQuality == null)
                            error = "Error: Select the default interpretation quality for new priority habitats.";
                        break;
                    case "OSMMSourceId":
                        if (OSMMSourceId == null)
                            error = "Error: Select the default source name for OS MasterMap.";
                        break;

                }

                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        [GeneratedRegex(@"[a-zA-Z0-9]")]
        private static partial Regex secondaryCodeDelimeterRegex();

        [GeneratedRegex(@"[a-zA-Z0-9]")]
        private static partial Regex vagueDateDelimeterRegex();

        #endregion
    }
}
