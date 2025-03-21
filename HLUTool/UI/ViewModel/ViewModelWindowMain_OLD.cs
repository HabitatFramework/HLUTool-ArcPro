﻿// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013-2014, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014, 2018 Sussex Biodiversity Record Centre
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Data.Model.HluDataSetTableAdapters;
using HLU.Date;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.View;
using HLU.UI.ViewModel;

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System.Windows.Threading;

using CommandType = System.Data.CommandType;
using Azure.Identity;
using System.Runtime.InteropServices;
using Azure;
using ArcGIS.Desktop.Internal.Framework.Controls;
using System.Windows.Controls;

using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;
using ArcGIS.Desktop.Internal.Mapping.Locate;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using HLU.UI.UserControls.Toolbar;
using System.Drawing.Printing;
using HLU.UI.UserControls;


namespace HLU.UI.ViewModel
{

    public partial class ViewModelWindowMain : PanelViewModelBase, INotifyPropertyChanged
    {

        #region Fields

        #region Commands

        private ICommand _navigateFirstCommand;
        private ICommand _navigatePreviousCommand;
        private ICommand _navigateNextCommand;
        private ICommand _navigateLastCommand;
        private ICommand _navigateIncidCommand;
        private ICommand _filterByAttributesCommand;
        private ICommand _filterByAttributesOSMMCommand;
        private ICommand _filterByIncidCommand;
        private ICommand _selectOnMapCommand;
        private ICommand _selectAllOnMapCommand;
        private ICommand _clearFilterCommand;
        private ICommand _readMapSelectionCommand;
        private ICommand _editPriorityHabitatsCommand;
        private ICommand _editPotentialHabitatsCommand;
        private ICommand _addSecondaryHabitatCommand;
        private ICommand _addSecondaryHabitatListCommand;
        private ICommand _logicalSplitCommand;
        private ICommand _physicalSplitCommand;
        private ICommand _logicalMergeCommand;
        private ICommand _physicalMergeCommand;
        private ICommand _updateCommand;
        private ICommand _bulkUpdateCommandMenu;
        private ICommand _cancelBulkUpdateCommand;
        private ICommand _osmmUpdateCommandMenu;
        private ICommand _osmmUpdateAcceptCommandMenu;
        private ICommand _osmmUpdateRejectCommandMenu;
        private ICommand _osmmSkipCommand;
        private ICommand _osmmAcceptCommand;
        private ICommand _osmmRejectCommand;
        private ICommand _osmmBulkUpdateCommandMenu;
        private ICommand _exportCommand;
        private ICommand _closeCommand;
        private ICommand _copyCommand;
        private ICommand _pasteCommand;
        private ICommand _autoZoomSelectedOffCommand;
        private ICommand _autoZoomSelectedWhenCommand;
        private ICommand _autoZoomSelectedAlwaysCommand;
        private ICommand _autoSelectOnGisCommand;
        private ICommand _zoomSelectionCommand;
        private ICommand _aboutCommand;

        #endregion Commands

        #region Windows

        private WindowMainCopySwitches _copySwitches = new();
        private WindowAbout _windowAbout;
        private ViewModelWindowAbout _viewModelAbout;
        private WindowQueryIncid _windowQueryIncid;
        private ViewModelWindowQueryIncid _viewModelWinQueryIncid;
        private WindowQuerySecondaries _windowQuerySecondaries;
        private ViewModelWindowQuerySecondaries _viewModelWinQuerySecondaries;
        private WindowQueryAdvanced _windowQueryAdvanced;
        private ViewModelWindowQueryAdvanced _viewModelWinQueryAdvanced;
        private WindowQueryOSMM _windowQueryOSMM;
        private ViewModelWindowQueryOSMM _viewModelWinQueryOSMM;
        private WindowWarnOnGISSelect _windowWarnGISSelect;
        private ViewModelWindowWarnOnGISSelect _viewModelWinWarnGISSelect;
        private WindowNotifyOnSplitMerge _windowWarnSplitMerge;
        private ViewModelWindowNotifyOnSplitMerge _viewModelWinWarnSplitMerge;
        private WindowWarnOnSubsetUpdate _windowWarnSubsetUpdate;
        private WindowCompletePhysicalSplit _windowCompSplit;
        private ViewModelCompletePhysicalSplit _vmCompSplit;
        private ViewModelWindowWarnOnSubsetUpdate _viewModelWinWarnSubsetUpdate;
        private ViewModelWindowMainBulkUpdate _viewModelBulkUpdate;
        private ViewModelWindowMainOSMMUpdate _viewModelOSMMUpdate;
        private ViewModelWindowMainUpdate _viewModelUpd;
        private WindowEditPriorityHabitats _windowEditPriorityHabitats;
        private ViewModelWindowEditPriorityHabitats _viewModelWinEditPriorityHabitats;
        private WindowEditPotentialHabitats _windowEditPotentialHabitats;
        private ViewModelWindowEditPotentialHabitats _viewModelWinEditPotentialHabitats;

        #endregion Windows

        #region Option fields

        // Database options
        private int _dbConnectionTimeout;

        // GIS/Export options
        private int _minZoom;

        // History options
        private DataColumn[] _historyColumns;
        private int _historyDisplayLastN;

        // Interface options
        private string _preferredHabitatClass;
        private bool _showGroupHeaders;
        private bool _showIHSTab;
        private bool _showSourceHabitatGroup;
        private bool _showHabitatSecondariesSuggested;
        private bool _showNVCCodes;
        private bool _showHabitatSummary;
        private string _showOSMMUpdates;
        private string _preferredSecondaryGroup;
        private string _secondaryCodeOrder;
        private string _secondaryCodeDelimiter;

        // Updates options
        private int _subsetUpdateAction;
        private string _clearIHSUpdateAction;
        private bool _notifyOnSplitMerge;
        private bool _resetOSMMUpdatesStatus;
        private int _habitatSecondaryCodeValidation;
        private int _primarySecondaryCodeValidation;
        private int _qualityValidation;
        private int _potentialPriorityDetermQtyValidation;

        // Filter options
        private int _warnBeforeGISSelect;

        // Dates options
        // None

        #endregion Option fields

        #region Dataset

        private DbBase _db;
        private ArcProApp _gisApp;
        private GeometryTypes _gisLayerType = GeometryTypes.Polygon;
        private HluDataSet _hluDS;
        private TableAdapterManager _hluTableAdapterMgr;
        private IEnumerable<DataRelation> _hluDataRelations;
        private RecordIds _recIDs;
        private int _incidCurrentRowIndex;
        private DataTable _incidSelection;
        private DataTable _gisSelection;
        private DataTable _incidMMPolygonSelection;

        private HluDataSet.incidRow _incidCurrentRow;
        private HluDataSet.incidRow _incidCurrentRowClone;
        private HluDataSet.incid_ihs_matrixRow[] _incidIhsMatrixRows;
        private HluDataSet.incid_ihs_formationRow[] _incidIhsFormationRows;
        private HluDataSet.incid_ihs_managementRow[] _incidIhsManagementRows;
        private HluDataSet.incid_ihs_complexRow[] _incidIhsComplexRows;
        private HluDataSet.incid_bapRow[] _incidBapRows;
        private HluDataSet.incid_sourcesRow[] _incidSourcesRows;
        private HluDataSet.incid_osmm_updatesRow[] _incidOSMMUpdatesRows;
        private HluDataSet.historyRow[] _incidHistoryRows;
        private HluDataSet.incid_conditionRow[] _incidConditionRows;
        private HluDataSet.incid_secondaryRow[] _incidSecondaryRows;

        private HluDataSet.lut_reasonRow[] _reasonCodes;
        private HluDataSet.lut_processRow[] _processCodes;
        private HluDataSet.lut_quality_determinationRow[] _qualityDeterminationCodes;
        private HluDataSet.lut_quality_interpretationRow[] _qualityInterpretationCodes;

        private IEnumerable<HluDataSet.lut_boundary_mapRow> _lutBoundaryMap;
        private IEnumerable<HluDataSet.lut_conditionRow> _lutCondition;
        private IEnumerable<HluDataSet.lut_condition_qualifierRow> _lutConditionQualifier;
        private IEnumerable<HluDataSet.lut_habitat_classRow> _lutHabitatClass;
        private IEnumerable<HluDataSet.lut_habitat_typeRow> _lutHabitatType;
        private IEnumerable<HluDataSet.lut_habitat_type_primaryRow> _lutHabitatTypePrimary;
        private IEnumerable<HluDataSet.lut_habitat_type_secondaryRow> _lutHabitatTypeSecondary;
        private IEnumerable<HluDataSet.lut_ihs_complexRow> _lutIhsComplex;
        private IEnumerable<HluDataSet.lut_ihs_formationRow> _lutIhsFormation;
        private IEnumerable<HluDataSet.lut_ihs_habitatRow> _lutIhsHabitat;
        private IEnumerable<HluDataSet.lut_ihs_managementRow> _lutIhsManagement;
        private IEnumerable<HluDataSet.lut_ihs_matrixRow> _lutIhsMatrix;
        private IEnumerable<HluDataSet.lut_importanceRow> _lutImportance;
        private IEnumerable<HluDataSet.lut_legacy_habitatRow> _lutLegacyHabitat;
        private IEnumerable<HluDataSet.lut_osmm_habitat_xrefRow> _lutOsmmHabitatXref;
        private IEnumerable<HluDataSet.lut_primaryRow> _lutPrimary;
        private IEnumerable<HluDataSet.lut_primary_bap_habitatRow> _lutPrimaryBapHabitat;
        private IEnumerable<HluDataSet.lut_primary_categoryRow> _lutPrimaryCategory;
        private IEnumerable<HluDataSet.lut_primary_secondaryRow> _lutPrimarySecondary;
        private IEnumerable<HluDataSet.lut_processRow> _lutProcess;
        private IEnumerable<HluDataSet.lut_quality_determinationRow> _lutQualityDetermination;
        private IEnumerable<HluDataSet.lut_quality_interpretationRow> _lutQualityInterpretation;
        private IEnumerable<HluDataSet.lut_reasonRow> _lutReason;
        private IEnumerable<HluDataSet.lut_secondaryRow> _lutSecondary;
        private IEnumerable<HluDataSet.lut_secondary_bap_habitatRow> _lutSecondaryBapHabitat;
        private IEnumerable<HluDataSet.lut_secondary_groupRow> _lutSecondaryGroup;
        private IEnumerable<HluDataSet.lut_sourcesRow> _lutSources;

        private CodeDescriptionBool[] _primaryCodes;
        private HluDataSet.lut_sourcesRow[] _sourceNames;
        private HluDataSet.lut_habitat_classRow[] _sourceHabitatClassCodes;
        private HluDataSet.lut_importanceRow[] _sourceImportanceCodes;
        private HluDataSet.lut_habitat_typeRow[] _bapHabitatCodes;
        private HluDataSet.lut_boundary_mapRow[] _boundaryMapCodes;

        private HluDataSet.lut_conditionRow[] _conditionCodes;
        private HluDataSet.lut_condition_qualifierRow[] _conditionQualifierCodes;

        private HluDataSet.lut_secondary_groupRow[] _secondaryGroupsValid;
        private HluDataSet.lut_secondary_groupRow[] _secondaryGroups;
        private static HluDataSet.lut_secondary_groupRow[] _secondaryGroupsAll; // Used in the options window
        private HluDataSet.lut_secondaryRow[] _secondaryCodesAll;
        private HluDataSet.lut_secondaryRow[] _secondaryCodesValid;
        private IEnumerable<string> _secondaryCodesMandatory;

        private ObservableCollection<SecondaryHabitat> _incidSecondaryHabitats;

        private ObservableCollection<BapEnvironment> _incidBapRowsAuto;
        private ObservableCollection<BapEnvironment> _incidBapRowsUser;

        private HluDataSet.lut_legacy_habitatRow[] _legacyHabitatCodes;

        private HistoryRowEqualityComparer _histRowEqComp = new();
        private HluDataSet.lut_habitat_classRow[] _habitatClassCodes;
        private static HluDataSet.lut_habitat_classRow[] _habitatClasses; // Used in the options window
        private HluDataSet.lut_habitat_typeRow[] _habitatTypeCodes;

        #endregion Dataset

        #region Variables

        private bool _showingReasonProcessGroup = false;
        private bool _showingOSMMPendingGroup = false;

        private double _incidArea;
        private double _incidLength;
        private string _process;
        private string _reason;
        private string _habitatClass;
        private string _habitatType;
        private string _habitatSecondariesMandatory;
        private string _habitatSecondariesSuggested;
        private string _habitatTips;
        private string _secondaryGroup;
        private string _secondaryHabitat;
        private bool _reasonProcessEnabled = true;
        private bool _tabControlDataEnabled = true;
        private int _tabItemSelected = 0;
        private bool _tabItemHabitatEnabled = true;
        private bool _tabItemIHSEnabled = true;
        private bool _tabItemPriorityEnabled = true;
        private bool _tabItemDetailsEnabled = true;
        private bool _tabItemSourcesEnabled = true;
        private bool _tabItemHistoryEnabled = true;
        private bool _tabHabitatControlsEnabled = true;
        private bool _tabIhsControlsEnabled = true;
        private bool _tabPriorityControlsEnabled = true;
        private bool _tabDetailsControlsEnabled = true;
        private bool _tabSourcesControlsEnabled = true;
        private bool _windowEnabled = true;

        private bool _pasting = false;
        private bool _changed = false;
        private bool _readingMap = false;
        private bool _saving = false;
        private bool _closing = false;
        private bool _autoSplit = true;
        private bool _splitting = false;
        private bool _filterByMap = false;
        private bool _osmmUpdating = false;
        private Cursor _windowCursor = Cursors.Arrow;
        private DataColumn[] _gisIDColumns;
        private int[] _gisIDColumnOrdinals;
        private IEnumerable<string> _incidsSelectedMap;
        private IEnumerable<string> _toidsSelectedMap;
        private IEnumerable<string> _fragsSelectedMap;
        private int _incidsSelectedDBCount = 0;
        private int _toidsSelectedDBCount = 0;
        private int _fragsSelectedDBCount = 0;
        private int _incidsSelectedMapCount = 0;
        private int _toidsSelectedMapCount = 0;
        private int _fragsSelectedMapCount = 0;

        private int _toidsIncidGisCount = 0;
        private int _fragsIncidGisCount = 0;
        private int _toidsIncidDbCount = 0;
        private int _fragsIncidDbCount = 0;

        private int _origIncidConditionCount = 0;
        private int _origIncidIhsMatrixCount = 0;
        private int _origIncidIhsFormationCount = 0;
        private int _origIncidIhsManagementCount = 0;
        private int _origIncidIhsComplexCount = 0;
        private int _origIncidSourcesCount = 0;
        private SqlFilterCondition _incidMMPolygonsIncidFilter;
        private int _incidRowCount;
        private int _incidPageRowNo;
        private int _incidPageRowNoMin = 0;
        private int _incidPageRowNoMax = 0;
        private string _incidIhsHabitat;
        private string _incidPrimary;
        private string _incidPrimaryCategory;
        private string _incidNVCCodes;
        private string _incidSecondarySummary;
        private string _incidLastModifiedUser;
        private DateTime _incidLastModifiedDate;
        private string _incidLegacyHabitat;
        private int _incidOSMMUpdatesOSMMXref;
        private int _incidOSMMUpdatesProcessFlag;
        private string _incidOSMMUpdatesSpatialFlag;
        private string _incidOSMMUpdatesChangeFlag;
        private Nullable<int> _incidOSMMUpdatesStatus;
        private Dictionary<Type, List<SqlFilterCondition>> _childRowFilterDict;
        private Dictionary<Type, string> _childRowOrderByDict;
        private List<List<SqlFilterCondition>> _incidSelectionWhereClause;
        private string _osmmUpdateWhereClause;
        private List<string> _exportMdbs = [];
        private string _userName;
        private string _appVersion;
        private bool _betaVersion;
        private string _dbVersion;
        private string _dataVersion;
        private Nullable<bool> _isAuthorisedUser;
        private Nullable<bool> _canBulkUpdate;
        private Nullable<bool> _bulkUpdateMode = false;
        private string _osmmAcceptTag = "A_ccept";
        private string _osmmRejectTag = "Re_ject";
        private Nullable<bool> _canOSMMUpdate;
        private Nullable<bool> _osmmUpdateMode = false;
        private Nullable<bool> _osmmBulkUpdateMode = false;
        private bool _osmmUpdatesEmpty = false;
        private bool _osmmUpdateCreateHistory;
        private string _codeAnyRow;

        private VagueDateInstance _incidConditionDateEntered;

        private VagueDateInstance _incidSource1DateEntered;
        private VagueDateInstance _incidSource2DateEntered;
        private VagueDateInstance _incidSource3DateEntered;
        private string _codeDeleteRow;
        private string _processingMsg = "Processing ...";
        private bool _saved = false;
        private bool _savingAttempted;
        private List<string> _habitatWarnings = [];
        private List<string> _priorityWarnings = [];
        private List<string> _detailsWarnings = [];
        private List<string[]> _conditionWarnings = null;
        private List<string[]> _source1Warnings = null;
        private List<string[]> _source2Warnings = null;
        private List<string[]> _source3Warnings = null;
        private List<string> _habitatErrors = [];
        private List<string> _priorityErrors = [];
        private List<string> _detailsErrors = [];
        private List<string[]> _conditionErrors = null;
        private List<string[]> _source1Errors = null;
        private List<string[]> _source2Errors = null;
        private List<string[]> _source3Errors = null;

        private bool _updateCancelled = true;
        private bool _updateAllFeatures = true;
        private bool _refillIncidTable = false;
        private int _autoZoomSelection;
        private bool _autoSelectOnGis;

        private ActiveLayerComboBox _activeLayerComboBox;
        private ReasonComboBox _reasonComboBox;
        private ProcessComboBox _processComboBox;

        #endregion Fields

        #region Static Variables

        internal static string _historyGeometry1ColumnName;
        internal static string _historyGeometry2ColumnName;
        internal static int _incidPageSize;

        #endregion Static fields

        #endregion

        #region Constructor

        /// <summary>
        /// Initialise settings for main window.
        /// </summary>
        /// <returns></returns>
        internal async Task<bool> InitializeToolPaneAsync()
        {
            // Get add-in database options
            _dbConnectionTimeout = _addInSettings.DbConnectionTimeout;
            _incidPageSize = _addInSettings.IncidTablePageSize;

            // Get add-in dates options
            VagueDate.Delimiter = _addInSettings.VagueDateDelimiter; // Set in the vague date class
            VagueDate.SeasonNames = _addInSettings.SeasonNames.Cast<string>().ToArray(); // Set in the vague date class

            // Get add-in validation options
            _habitatSecondaryCodeValidation = _addInSettings.HabitatSecondaryCodeValidation;
            _primarySecondaryCodeValidation = _addInSettings.PrimarySecondaryCodeValidation;
            SecondaryHabitat.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation; // Set in the secondary habitat class
            _qualityValidation = _addInSettings.QualityValidation;
            _potentialPriorityDetermQtyValidation = _addInSettings.PotentialPriorityDetermQtyValidation;
            BapEnvironment.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation; // Used in the priority habitat class

            // Get add-in updates options
            _subsetUpdateAction = _addInSettings.SubsetUpdateAction;
            _clearIHSUpdateAction = _addInSettings.ClearIHSUpdateAction;
            _secondaryCodeDelimiter = _addInSettings.SecondaryCodeDelimiter;
            _resetOSMMUpdatesStatus = _addInSettings.ResetOSMMUpdatesStatus;

            // Get user filter options
            _warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;

            // Get user GIS options
            _minZoom = Settings.Default.MinAutoZoom;

            // Get user interface options
            _preferredHabitatClass = Settings.Default.PreferredHabitatClass;
            _showGroupHeaders = Settings.Default.ShowGroupHeaders;
            _showIHSTab = Settings.Default.ShowIHSTab;
            _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
            _showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
            _showNVCCodes = Settings.Default.ShowNVCCodes;
            _showHabitatSummary = Settings.Default.ShowHabitatSummary;
            _showOSMMUpdates = Settings.Default.ShowOSMMUpdatesOption;
            _preferredSecondaryGroup = Settings.Default.PreferredSecondaryGroup;
            _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;

            // Get user updates options
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;

            // Get user history options
            _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

            // Get application settings
            _codeDeleteRow = Settings.Default.CodeDeleteRow;
            _autoZoomSelection = Settings.Default.AutoZoomSelection;
            _autoSelectOnGis = Settings.Default.AutoSelectOnGis;
            _codeAnyRow = Settings.Default.CodeAnyRow;

            // Initialise statics.
            HistoryGeometry1ColumnName = Settings.Default.HistoryGeometry1ColumnName;
            HistoryGeometry2ColumnName = Settings.Default.HistoryGeometry2ColumnName;

            try
            {
                // Open database connection and test whether it points to a valid HLU database
                while (true)
                {
                    if ((_db = DbFactory.CreateConnection(DbConnectionTimeout)) == null)
                        throw new Exception("No database connection.");

                    _hluDS = new HluDataSet();

                    string errorMessage;
                    if (!_db.ContainsDataSet(_hluDS, out errorMessage))
                    {
                        // Clear the current database settings as they are clearly not valid.
                        DbFactory.ClearSettings();

                        if (String.IsNullOrEmpty(errorMessage))
                        {
                            errorMessage = String.Empty;
                        }
                        else if (errorMessage.Length > 200)
                        {
                            if (MessageBox.Show("There were errors loading data from the database." +
                                "\n\nWould like to see a list of those errors?", "HLU: Initialise Dataset",
                                MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                                ShowMessageWindow.ShowMessage(errorMessage, "HLU Dataset");
                            errorMessage = String.Empty;
                        }
                        if (MessageBox.Show("There were errors loading data from the database." +
                            errorMessage + "\n\nWould you like to connect to another database?", "HLU: Initialise Dataset",
                            MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                            throw new Exception("cancelled");
                    }
                    else
                    {
                        break;
                    }
                }

                ChangeCursor(Cursors.Wait, "Initiating ...");

                // Create table adapter manager for the dataset and connection
                _hluTableAdapterMgr = new TableAdapterManager(_db, TableAdapterManager.Scope.AllButMMPolygonsHistory);

                // Fill a dictionary of parent-child tables and relations between them
                _hluDataRelations = HluDataset.Relations.Cast<DataRelation>();

                // Translate DataRelation objects into database condtions and build order by clauses
                _childRowFilterDict = BuildChildRowFilters();
                _childRowOrderByDict = BuildChildRowOrderByClauses();

                // Fill lookup tables (at least lut_site_id must be filled at this point)
                _hluTableAdapterMgr.Fill(_hluDS, TableAdapterManager.Scope.Lookup, false);

                // Create RecordIds object for the db
                _recIDs = new RecordIds(_db, _hluDS, _hluTableAdapterMgr, GisLayerType);

                //---------------------------------------------------------------------
                // CHANGED: CR30 (Database validation on start-up)
                // Check the assembly version is not earlier than the
                // minimum required dataset application version.
                if (!CheckVersion())
                    return false;
                //---------------------------------------------------------------------

                // Wire up event handler for copy switches
                _copySwitches.PropertyChanged += new PropertyChangedEventHandler(_copySwitches_PropertyChanged);

                int result;
                // Columns that identify map polygons and are returned by GIS
                _gisIDColumnOrdinals = (from s in Settings.Default.GisIDColumnOrdinals.Cast<string>()
                                        where Int32.TryParse(s, out result) && (result >= 0) &&
                                        (result < _hluDS.incid_mm_polygons.Columns.Count)
                                        select Int32.Parse(s)).ToArray();
                _gisIDColumns = _gisIDColumnOrdinals.Select(i => _hluDS.incid_mm_polygons.Columns[i]).ToArray();

                // Columns to be displayed in history (always includes _gisIDColumns)
                _historyColumns = InitializeHistoryColumns(_historyColumns);

                // Create scratch database
                ScratchDb.CreateScratchMdb(_hluDS.incid, _hluDS.incid_mm_polygons, DbConnectionTimeout);

                // Count rows of incid table
                IncidRowCount(true);

                // Load all of the lookup tables
                LoadLookupTables();

                //TODO: Don't do this until the active map has been checked.
                // Move to first row
                await MoveIncidCurrentRowIndexAsync(1);

                // Check the active map is valid (don't check result at this stage)
                await CheckActiveMapAsync();

                // Initialise the main update view model
                _viewModelUpd = new ViewModelWindowMainUpdate(this);

                // Get the BAP determination quality defaults
                GetBapDefaults();

                // Set the validation option for potential priority habitats
                BapEnvironment.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation;

                // Clear the status bar (or reset the cursor to an arrow)
                ChangeCursor(Cursors.Arrow, null);

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message != "cancelled")
                    MessageBox.Show(ex.Message, "HLU: Initialise Application",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                //TODO: App.Current.Shutdown
                //App.Current.Shutdown();
                return false;
            }
        }

        /// <summary>
        /// Load the sources for the data grid combo boxes.
        /// </summary>
        /// <returns></returns>
        internal bool LoadComboBoxSources()
        {
            try
            {
                // Open database connection and test whether it points to a valid HLU database
                while (true)
                {
                    if ((_db = DbFactory.CreateConnection(DbConnectionTimeout)) == null)
                        throw new Exception("No database connection.");

                    _hluDS = new HluDataSet();

                    string errorMessage;
                    if (!_db.ContainsDataSet(_hluDS, out errorMessage))
                    {
                        // Clear the current database settings as they are clearly not valid.
                        DbFactory.ClearSettings();

                        if (String.IsNullOrEmpty(errorMessage))
                        {
                            errorMessage = String.Empty;
                        }
                        else if (errorMessage.Length > 200)
                        {
                            if (MessageBox.Show("There were errors loading data from the database." +
                                "\n\nWould like to see a list of those errors?", "HLU: Initialise Dataset",
                                MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                                ShowMessageWindow.ShowMessage(errorMessage, "HLU Dataset");
                            errorMessage = String.Empty;
                        }
                        if (MessageBox.Show("There were errors loading data from the database." +
                            errorMessage + "\n\nWould you like to connect to another database?", "HLU: Initialise Dataset",
                            MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                            throw new Exception("cancelled");
                    }
                    else
                    {
                        break;
                    }
                }

                // Create table adapter manager for the dataset and connection
                _hluTableAdapterMgr = new TableAdapterManager(_db, TableAdapterManager.Scope.AllButMMPolygonsHistory);

                // Fill lookup tables (at least lut_site_id must be filled at this point)
                _hluTableAdapterMgr.Fill(_hluDS, TableAdapterManager.Scope.Lookup, false);

                // Load all of the lookup tables
                LoadLookupTables();

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message != "cancelled")
                    MessageBox.Show(ex.Message, "HLU: Initialise Application",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                //TODO: App.Current.Shutdown
                //App.Current.Shutdown();
                return false;
            }
        }

        private async Task<bool> CheckActiveMapAsync()
        {
            // Check the GIS workspace
            ChangeCursor(Cursors.Wait, "Checking GIS workspace ...");

            // Create a new GIS functions object if necessary.
            if (_gisApp == null || _gisApp.MapName == null || MapView.Active is null || MapView.Active.Map.Name != _gisApp.MapName)
                _gisApp = new();

            // Get the instance of the active layer ComboBox in the ribbon.
            if (_activeLayerComboBox == null)
                _activeLayerComboBox = ActiveLayerComboBox.GetInstance();

            // Check if there is an active map.
            if (_gisApp.MapName != null)
            {
                //DONE: ArcGIS
                // Check if the GIS workspace is valid
                if (!await _gisApp.IsHluWorkspaceAsync(ActiveLayerName))
                {
                    // Clear the status bar (or reset the cursor to an arrow)
                    ChangeCursor(Cursors.Arrow, null);

                    // Clear the list of valid HLU layer names.
                    AvailableHLULayerNames = [];

                    // Clear the list of layer names in ComboBox in the ribbon.
                    //ActiveLayerComboBox.UpdateLayerNames(null);

                    // Force the ComboBox to reinitialise (if it is loaded).
                    _activeLayerComboBox?.Initialize();

                    // Display an error message.
                    ShowMessage("Invalid HLU workspace.", MessageType.Warning);

                    // Hide the dockpane.
                    DockpaneVisibility = Visibility.Hidden;

                    return false;
                }

                // Set the list of valid HLU layer names.
                AvailableHLULayerNames = new ObservableCollection<string>(_gisApp.ValidHluLayerNames);

                // Update the list of layer names in the ComboBox in the ribbon.
                //ActiveLayerComboBox.UpdateLayerNames(AvailableHLULayerNames);

                // Update the active layer name for the ComboBox in the ribbon.
                //ActiveLayerComboBox.UpdateActiveLayer(ActiveLayerName);

                // If the ComboBox has still not been initialised.
                if (_activeLayerComboBox == null)
                {
                    // Switch the GIS layer.
                    if (await _gisApp.IsHluLayer(ActiveLayerName, true))
                    {
                        // Refresh the layer name
                        OnPropertyChanged(nameof(ActiveLayerName));

                        // Get the GIS layer selection and warn the user if no
                        // features are found
                        await ReadMapSelectionAsync(true);
                    }
                }
                // Otherwise (re)initialise the ComboBox, select the active
                // layer and let that switch the GIS layer.
                else
                {
                    // Force the ComboBox to reinitialise (if it is loaded).
                    _activeLayerComboBox.Initialize();

                    // Update the selection in the ComboBox (if it is loaded)
                    // to match the current active layer.
                    _activeLayerComboBox.SetSelectedItem(ActiveLayerName);
                }

                //DONE: No needed as triggered by above when setting active layer?
                // Read the selected features from the map
                //await ReadMapSelectionAsync(false);
            }
            else
            {
                // Clear the status bar (or reset the cursor to an arrow)
                ChangeCursor(Cursors.Arrow, null);

                // Display an error message.
                ShowMessage("No active map.", MessageType.Warning);

                // Hide the dockpane.
                DockpaneVisibility = Visibility.Hidden;

                return false;
            }

            // Clear the status bar (or reset the cursor to an arrow)
            ChangeCursor(Cursors.Arrow, null);

            return true;
        }

        /// <summary>
        /// Get columns to be saved to history when altering GIS layer.
        /// </summary>
        /// <param name="historyColumns">Old value to be restored if method fails.</param>
        /// <returns>Array of columns to be saved to history when altering GIS layer.
        /// Always includes _gisIDColumns.</returns>
        private DataColumn[] InitializeHistoryColumns(DataColumn[] historyColumns)
        {
            try
            {
                // Make sure that all the available history columns are updated when
                // creating history even if the user only wants to display some of them.
                return _gisIDColumns.Concat(_hluDS.incid_mm_polygons.Columns.Cast<DataColumn>()
                    .Where(c => !_gisIDColumnOrdinals.Contains(c.Ordinal)
                        && !c.ColumnName.StartsWith("shape_"))).ToArray();
            }
            catch { return historyColumns; }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR30 (Database validation on start-up)
        /// <summary>
        /// Check the addin version is greater than or equal to the
        /// application version from the lut_version table in the database.
        /// </summary>
        private bool CheckVersion()
        {
            // Get the addin version.
            var addInInfo = FrameworkApplication.GetAddInInfos().First(addIn => addIn.Name == "HLUTool");
            var addInVersion = addInInfo.Version;

            // Get the minimum application, database and data versions from the database.
            String lutAppVersion = "0.0.0";
            String lutDbVersion = "0";
            String lutDataVersion = "";
            if (_hluDS.lut_version.Count > 0)
            {
                lutAppVersion = _hluDS.lut_version.ElementAt(_hluDS.lut_version.Count - 1).app_version;
                lutDbVersion = _hluDS.lut_version.ElementAt(_hluDS.lut_version.Count - 1).db_version;
                lutDataVersion = _hluDS.lut_version.ElementAt(_hluDS.lut_version.Count - 1).data_version;
            }
            else
            {
                return false;
            }

            // Convert the version strings to version mumbers.
            Version addVersion = new(addInVersion);
            Version appVersion = new(lutAppVersion);

            // Compare the addin and application versions.
            if (addVersion.CompareTo(appVersion) < 0)
            {
                // Trap error if database requires a later application version.
                throw new Exception(String.Format("The minimum application version must be {0}.", appVersion.ToString()));
            }

            // Get the minimum database version.
            string minDbVersion = Settings.Default.MinimumDbVersion;

            // Compare the minimum database version.
            if (Base36.Base36ToNumber(lutDbVersion) < Base36.Base36ToNumber(minDbVersion))
            {
                // Trap error if application requires a later database version.
                throw new Exception(String.Format("The minimum database version must be {0}.", minDbVersion));
            }

            // Store the application, database and data versions for displaying in the 'About' box.
            _appVersion = addInVersion;
            _dbVersion = lutDbVersion;
            _dataVersion = lutDataVersion;

            return true;
        }


        /// <summary>
        /// Loads all of the lookup tables (with the exception of a few
        /// loaded elsewhere).
        /// </summary>
        public void LoadLookupTables()
        {
            // Get the list of boundary map values from the lookup table.
            if (_lutBoundaryMap == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_boundary_map.IsInitialized && HluDataset.lut_boundary_map.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_boundary_mapTableAdapter == null)
                        _hluTableAdapterMgr.lut_boundary_mapTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_boundary_mapDataTable, HluDataSet.lut_boundary_mapRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_boundary_mapDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutBoundaryMap = HluDataset.lut_boundary_map;
            }

            // Get the list of condition code values from the lookup table.
            if (_lutCondition == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_condition.IsInitialized && (HluDataset.lut_condition.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_conditionTableAdapter == null)
                        _hluTableAdapterMgr.lut_conditionTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_conditionDataTable, HluDataSet.lut_conditionRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_conditionDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutCondition = from c in HluDataset.lut_condition
                                where c.is_local
                                select c;
            }

            // Get the list of condition qualifier values from the lookup table.
            if (_lutConditionQualifier == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_condition_qualifier.IsInitialized && (HluDataset.lut_condition_qualifier.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_condition_qualifierTableAdapter == null)
                        _hluTableAdapterMgr.lut_condition_qualifierTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_condition_qualifierDataTable, HluDataSet.lut_condition_qualifierRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_condition_qualifierDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutConditionQualifier = from cq in HluDataset.lut_condition_qualifier
                                         where cq.is_local
                                         select cq;
            }

            // Get the list of habitat class values from the lookup table.
            if (_lutHabitatClass == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_habitat_class.IsInitialized && HluDataset.lut_habitat_class.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_habitat_classTableAdapter == null)
                        _hluTableAdapterMgr.lut_habitat_classTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_habitat_classDataTable, HluDataSet.lut_habitat_classRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_habitat_classDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutHabitatClass = from hc in HluDataset.lut_habitat_class
                                   where hc.is_local
                                   select hc;
            }

            // Get the list of habitat type values from the lookup table.
            if (_lutHabitatType == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_habitat_type.IsInitialized && HluDataset.lut_habitat_type.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_habitat_typeTableAdapter == null)
                        _hluTableAdapterMgr.lut_habitat_typeTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_habitat_typeDataTable, HluDataSet.lut_habitat_typeRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_habitat_typeDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutHabitatType = from hc in HluDataset.lut_habitat_type
                                  where hc.is_local
                                  select hc;
            }

            // Get the list of habitat type primary values from the lookup table.
            if (_lutHabitatTypePrimary == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_habitat_type_primary.IsInitialized && HluDataset.lut_habitat_type_primary.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_habitat_type_primaryTableAdapter == null)
                        _hluTableAdapterMgr.lut_habitat_type_primaryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_habitat_type_primaryDataTable, HluDataSet.lut_habitat_type_primaryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_habitat_type_primaryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutHabitatTypePrimary = from htp in HluDataset.lut_habitat_type_primary
                                         where htp.is_local
                                         select htp;
            }

            // Get the list of habitat type secondary values from the lookup table.
            if (_lutHabitatTypeSecondary == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_habitat_type_secondary.IsInitialized && HluDataset.lut_habitat_type_secondary.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_habitat_type_secondaryTableAdapter == null)
                        _hluTableAdapterMgr.lut_habitat_type_secondaryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_habitat_type_secondaryDataTable, HluDataSet.lut_habitat_type_secondaryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_habitat_type_secondaryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutHabitatTypeSecondary = from htp in HluDataset.lut_habitat_type_secondary
                                         where htp.is_local
                                         select htp;
            }

            // Get the list of ihs complex code values from the lookup table.
            if (_lutIhsComplex == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_ihs_complex.IsInitialized && (HluDataset.lut_ihs_complex.Rows.Count == 0))
                {
                    // Load the lookup table.
                    _hluTableAdapterMgr.lut_ihs_complexTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_ihs_complexDataTable, HluDataSet.lut_ihs_complexRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_ihs_complexDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutIhsComplex = from ihc in HluDataset.lut_ihs_complex
                                 select ihc;
            }

            // Get the value from the lookup table.
            if (_lutIhsFormation == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_ihs_formation.IsInitialized && (HluDataset.lut_ihs_formation.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_ihs_formationTableAdapter == null)
                        _hluTableAdapterMgr.lut_ihs_formationTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_ihs_formationDataTable, HluDataSet.lut_ihs_formationRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_ihs_formationDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutIhsFormation = from ihf in HluDataset.lut_ihs_formation
                                   select ihf;
            }

            // Get the list of ihs habitat code values from the lookup table.
            if (_lutIhsHabitat == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_ihs_habitat.IsInitialized && HluDataset.lut_ihs_habitat.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_ihs_habitatTableAdapter == null)
                        _hluTableAdapterMgr.lut_ihs_habitatTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_ihs_habitatDataTable, HluDataSet.lut_ihs_habitatRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_ihs_habitatDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutIhsHabitat = from ih in HluDataset.lut_ihs_habitat
                                 where ih.is_local
                                 select ih;
            }

            // Get the value from the lookup table.
            if (_lutIhsManagement == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_ihs_management.IsInitialized && (HluDataset.lut_ihs_management.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_ihs_managementTableAdapter == null)
                        _hluTableAdapterMgr.lut_ihs_managementTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_ihs_managementDataTable, HluDataSet.lut_ihs_managementRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_ihs_managementDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutIhsManagement = from ihm in HluDataset.lut_ihs_management
                                    select ihm;
            }

            // Get the list of ihs matrix code values from the lookup table.
            if (_lutIhsMatrix == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_ihs_matrix.IsInitialized && (HluDataset.lut_ihs_matrix.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_ihs_matrixTableAdapter == null)
                        _hluTableAdapterMgr.lut_ihs_matrixTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_ihs_matrixDataTable, HluDataSet.lut_ihs_matrixRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_ihs_matrixDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutIhsMatrix = from ihx in HluDataset.lut_ihs_matrix
                                select ihx;
            }

            // Get the list of importance code values from the lookup table.
            if (_lutImportance == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_importance.IsInitialized &&
                    HluDataset.lut_importance.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_importanceTableAdapter == null)
                        _hluTableAdapterMgr.lut_importanceTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_importanceDataTable,
                                HluDataSet.lut_importanceRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_importanceDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutImportance = HluDataset.lut_importance;
            }

            // Get the list of legacy habitat code values from the lookup table.
            if (_lutLegacyHabitat == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_legacy_habitat.IsInitialized && HluDataset.lut_legacy_habitat.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_legacy_habitatTableAdapter == null)
                        _hluTableAdapterMgr.lut_legacy_habitatTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_legacy_habitatDataTable, HluDataSet.lut_legacy_habitatRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_legacy_habitatDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutLegacyHabitat = HluDataset.lut_legacy_habitat;
            }

            // Get the list of OSMM habitat cross-reference values from the lookup table.
            if (_lutOsmmHabitatXref == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_osmm_habitat_xref.IsInitialized && HluDataset.lut_osmm_habitat_xref.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_osmm_habitat_xrefTableAdapter == null)
                        _hluTableAdapterMgr.lut_osmm_habitat_xrefTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_osmm_habitat_xrefDataTable, HluDataSet.lut_osmm_habitat_xrefRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_osmm_habitat_xrefDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutOsmmHabitatXref = from ohx in HluDataset.lut_osmm_habitat_xref
                                      where ohx.is_local
                                      select ohx;
            }

            // Get the list of primary code values from the lookup table.
            if (_lutPrimary == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_primary.IsInitialized && HluDataset.lut_primary.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_primaryTableAdapter == null)
                        _hluTableAdapterMgr.lut_primaryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_primaryDataTable, HluDataSet.lut_primaryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_primaryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutPrimary = from p in HluDataset.lut_primary
                              where p.is_local
                              select p;
            }

            // Get the list of primary bap habitat code values from the lookup table.
            if (_lutPrimaryBapHabitat == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_primary_bap_habitat.IsInitialized && HluDataset.lut_primary_bap_habitat.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_primary_bap_habitatTableAdapter == null)
                        _hluTableAdapterMgr.lut_primary_bap_habitatTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_primary_bap_habitatDataTable, HluDataSet.lut_primary_bap_habitatRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_primary_bap_habitatDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutPrimaryBapHabitat = from p in HluDataset.lut_primary_bap_habitat
                                        select p;
            }

            // Get the list of primary category values from the lookup table.
            if (_lutPrimaryCategory == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_primary_category.IsInitialized && HluDataset.lut_primary_category.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_primary_categoryTableAdapter == null)
                        _hluTableAdapterMgr.lut_primary_categoryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_primary_categoryDataTable, HluDataSet.lut_primary_categoryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_primary_categoryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutPrimaryCategory = from pc in HluDataset.lut_primary_category
                                      where pc.is_local
                                      select pc;
            }

            // Get the list of primary secondary code values from the lookup table.
            if (_lutPrimarySecondary == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_primary_secondary.IsInitialized && HluDataset.lut_primary_secondary.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_primary_secondaryTableAdapter == null)
                        _hluTableAdapterMgr.lut_primary_secondaryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_primary_secondaryDataTable, HluDataSet.lut_primary_secondaryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_primary_secondaryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutPrimarySecondary = from ps in HluDataset.lut_primary_secondary
                                       where ps.is_local
                                       select ps;
            }

            // Get the list of process code values from the lookup table
            if (_lutProcess == null)
            {
                // If the data table if not already loaded.
                if (HluDataset.lut_process.IsInitialized && (HluDataset.lut_process.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_processTableAdapter == null)
                        _hluTableAdapterMgr.lut_processTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_processDataTable, HluDataSet.lut_processRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_processDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutProcess = HluDataset.lut_process;
            }

            // Get the list of quality determination values from the lookup table.
            if (_lutQualityDetermination == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_quality_determination.IsInitialized &&
                    HluDataset.lut_quality_determination.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_quality_determinationTableAdapter == null)
                        _hluTableAdapterMgr.lut_quality_determinationTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_quality_determinationDataTable,
                                HluDataSet.lut_quality_determinationRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_quality_determinationDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutQualityDetermination = HluDataset.lut_quality_determination;
            }

            // Get the list of quality interpretation values from the lookup table.
            if (_lutQualityInterpretation == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_quality_interpretation.IsInitialized &&
                    HluDataset.lut_quality_interpretation.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_quality_interpretationTableAdapter == null)
                        _hluTableAdapterMgr.lut_quality_interpretationTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_quality_interpretationDataTable,
                                HluDataSet.lut_quality_interpretationRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_quality_interpretationDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutQualityInterpretation = HluDataset.lut_quality_interpretation;
            }

            // Get the list of reason code values from the lookup table.
            if (_lutReason == null)
            {
                // If the data table if not already loaded.
                if (HluDataset.lut_reason.IsInitialized && (HluDataset.lut_reason.Rows.Count == 0))
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_reasonTableAdapter == null)
                        _hluTableAdapterMgr.lut_reasonTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_reasonDataTable, HluDataSet.lut_reasonRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_reasonDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutReason = HluDataset.lut_reason;
            }

            // Get the list of secondary code values from the lookup table.
            if (_lutSecondary == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_secondary.IsInitialized && HluDataset.lut_secondary.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_secondaryTableAdapter == null)
                        _hluTableAdapterMgr.lut_secondaryTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_secondaryDataTable, HluDataSet.lut_secondaryRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_secondaryDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutSecondary = from s in HluDataset.lut_secondary
                                where s.is_local
                                select s;
            }

            // Get the list of secondary bap habitat code values from the lookup table.
            if (_lutSecondaryBapHabitat == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_secondary_bap_habitat.IsInitialized && HluDataset.lut_secondary_bap_habitat.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_secondary_bap_habitatTableAdapter == null)
                        _hluTableAdapterMgr.lut_secondary_bap_habitatTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_secondary_bap_habitatDataTable, HluDataSet.lut_secondary_bap_habitatRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_secondary_bap_habitatDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutSecondaryBapHabitat = from p in HluDataset.lut_secondary_bap_habitat
                                          select p;
            }

            // Get the list of secondary group code values from the lookup table.
            if (_lutSecondaryGroup == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_secondary_group.IsInitialized && HluDataset.lut_secondary_group.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_secondary_groupTableAdapter == null)
                        _hluTableAdapterMgr.lut_secondary_groupTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_secondary_groupDataTable, HluDataSet.lut_secondary_groupRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset, [typeof(HluDataSet.lut_secondary_groupDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutSecondaryGroup = from sg in HluDataset.lut_secondary_group
                                     where sg.is_local
                                     select sg;
            }

            // Get the list of source code values from the lookup table.
            if (_lutSources == null)
            {
                // If the lookup table if not already loaded.
                if (HluDataset.lut_sources.IsInitialized &&
                    HluDataset.lut_sources.Count == 0)
                {
                    // Load the lookup table.
                    if (_hluTableAdapterMgr.lut_sourcesTableAdapter == null)
                        _hluTableAdapterMgr.lut_sourcesTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_sourcesDataTable,
                                HluDataSet.lut_sourcesRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_sourcesDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutSources = HluDataset.lut_sources;
            }
        }

        #endregion

        #region Internal properties

        internal ArcProApp GISApplication
        {
            get { return _gisApp; }
        }

        internal DbBase DataBase
        {
            get { return _db; }
        }

        internal HluDataSet HluDataset
        {
            get
            {
                //TODO: Needed?
                //if (_hluDS == null) InitializeToolPaneAsync();
                return _hluDS;
            }
        }

        internal IEnumerable<DataRelation> HluDataRelations
        {
            get { return _hluDataRelations; }
        }

        internal TableAdapterManager HluTableAdapterManager
        {
            get { return _hluTableAdapterMgr; }
        }

        internal int[] GisIDColumnOrdinals
        {
            get { return _gisIDColumnOrdinals; }
        }

        internal DataColumn[] GisIDColumns
        {
            get { return _gisIDColumns; }
        }

        internal DataColumn[] HistoryColumns
        {
            get { return _historyColumns; }
        }

        public static string HistoryGeometry1ColumnName { get => _historyGeometry1ColumnName; set => _historyGeometry1ColumnName = value; }

        public static string HistoryGeometry2ColumnName { get => _historyGeometry2ColumnName; set => _historyGeometry2ColumnName = value; }

        public static int IncidPageSize { get => _incidPageSize; set => _incidPageSize = value; }

        internal DataTable GisSelection
        {
            get { return _gisSelection; }
            set { _gisSelection = value; }
        }

        internal DataTable IncidSelection
        {
            get { return _incidSelection; }
            set { _incidSelection = value; }
        }

        internal List<List<SqlFilterCondition>> IncidSelectionWhereClause
        {
            get { return _incidSelectionWhereClause; }
            set { _incidSelectionWhereClause = value; }
        }

        internal string OSMMUpdateWhereClause
        {
            get { return _osmmUpdateWhereClause; }
            set { _osmmUpdateWhereClause = value; }
        }

        internal List<string> ExportMdbs
        {
            get { return _exportMdbs; }
            set { _exportMdbs = value; }
        }

        internal DateTime IncidLastModifiedDateVal
        {
            get { return _incidLastModifiedDate; }
            set { _incidLastModifiedDate = value; }
        }

        internal string IncidLastModifiedUserId
        {
            get { return _incidLastModifiedUser; }
            set { if ((IncidCurrentRow != null) && (value != null)) _incidLastModifiedUser = value; }
        }

        internal HluDataSet.incid_ihs_matrixRow[] IncidIhsMatrixRows
        {
            get { return _incidIhsMatrixRows; }
            set { _incidIhsMatrixRows = value; }
        }

        internal HluDataSet.incid_ihs_formationRow[] IncidIhsFormationRows
        {
            get { return _incidIhsFormationRows; }
            set { _incidIhsFormationRows = value; }
        }

        internal HluDataSet.incid_ihs_managementRow[] IncidIhsManagementRows
        {
            get { return _incidIhsManagementRows; }
            set { _incidIhsManagementRows = value; }
        }

        internal HluDataSet.incid_ihs_complexRow[] IncidIhsComplexRows
        {
            get { return _incidIhsComplexRows; }
            set { _incidIhsComplexRows = value; }
        }

        internal HluDataSet.incid_secondaryRow[] IncidSecondaryRows
        {
            get { return _incidSecondaryRows; }
            set { _incidSecondaryRows = value; }
        }

        internal HluDataSet.incid_conditionRow[] IncidConditionRows
        {
            get { return _incidConditionRows; }
            set { _incidConditionRows = value; }
        }

        internal HluDataSet.incid_bapRow[] IncidBapRows
        {
            get { return _incidBapRows; }
            set { _incidBapRows = value; }
        }

        internal HluDataSet.incid_sourcesRow[] IncidSourcesRows
        {
            get { return _incidSourcesRows; }
            set { _incidSourcesRows = value; }
        }

        internal HluDataSet.incid_osmm_updatesRow[] IncidOSMMUpdatesRows
        {
            get { return _incidOSMMUpdatesRows; }
            set { _incidOSMMUpdatesRows = value; }
        }

        internal ObservableCollection<BapEnvironment> IncidBapRowsAuto
        {
            get { return _incidBapRowsAuto; }
            set { _incidBapRowsAuto = value; }
        }

        internal ObservableCollection<BapEnvironment> IncidBapRowsUser
        {
            get { return _incidBapRowsUser; }
            set { _incidBapRowsUser = value; }
        }

        internal RecordIds RecIDs
        {
            get { return _recIDs; }
            set { _recIDs = value; }
        }

        internal bool Saved
        {
            get { return _saved; }
            set { _saved = value; }
        }

        internal bool Pasting
        {
            get { return _pasting; }
            set { _pasting = value; }
        }

        internal bool Changed
        {
            get { return _changed; }
            set
            {
                // If this is another change by the user but the data is no longer
                // dirty (i.e. the user has reversed out their changes) then
                // reset the changed flag.
                if (value == true && !IsDirty)
                    _changed = false;
                else
                    _changed = value;

                OnPropertyChanged(nameof(CanUpdate));
            }
        }

        internal bool Saving
        {
            get { return _saving; }
            set { _saving = value; }
        }

        internal bool SavingAttempted
        {
            get { return _savingAttempted; }
            set { _savingAttempted = value; }
        }

        internal IEnumerable<string> IncidsSelectedMap
        {
            get { return _incidsSelectedMap; }
        }

        internal IEnumerable<string> ToidsSelectedMap
        {
            get { return _toidsSelectedMap; }
        }

        internal IEnumerable<string> FragsSelectedMap
        {
            get { return _fragsSelectedMap; }
        }

        internal int IncidsSelectedMapCount
        {
            get { return _incidsSelectedMapCount; }
        }

        internal int ToidsSelectedMapCount
        {
            get { return _toidsSelectedMapCount; }
        }

        internal int FragsSelectedMapCount
        {
            get { return _fragsSelectedMapCount; }
        }

        internal ViewModelWindowMainUpdate ViewModelUpdate
        {
            get { return _viewModelUpd; }
        }

        internal List<string> HabitatWarnings
        {
            get { return _habitatWarnings; }
            set { _habitatWarnings = value; }
        }

        internal List<string> PriorityWarnings
        {
            get { return _priorityWarnings; }
            set { _priorityWarnings = value; }
        }

        internal List<string[]> ConditionWarnings
        {
            get { return _conditionWarnings; }
            set { _conditionWarnings = value; }
        }

        internal List<string> DetailsWarnings
        {
            get { return _detailsWarnings; }
            set { _detailsWarnings = value; }
        }

        internal List<string[]> Source1Warnings
        {
            get { return _source1Warnings; }
            set { _source1Warnings = value; }
        }

        internal List<string[]> Source2Warnings
        {
            get { return _source2Warnings; }
            set { _source2Warnings = value; }
        }

        internal List<string[]> Source3Warnings
        {
            get { return _source3Warnings; }
            set { _source3Warnings = value; }
        }

        internal List<string> HabitatErrors
        {
            get { return _habitatErrors; }
            set { _habitatErrors = value; }
        }

        internal List<string> PriorityErrors
        {
            get { return _priorityErrors; }
            set { _priorityErrors = value; }
        }

        internal List<string[]> ConditionErrors
        {
            get { return _conditionErrors; }
            set { _conditionErrors = value; }
        }

        internal List<string> DetailsErrors
        {
            get { return _detailsErrors; }
            set { _detailsErrors = value; }
        }

        internal List<string[]> Source1Errors
        {
            get { return _source1Errors; }
            set { _source1Errors = value; }
        }

        internal List<string[]> Source2Errors
        {
            get { return _source2Errors; }
            set { _source2Errors = value; }
        }

        internal List<string[]> Source3Errors
        {
            get { return _source3Errors; }
            set { _source3Errors = value; }
        }

        internal bool RefillIncidTable
        {
            get { return _refillIncidTable; }
            set { _refillIncidTable = true; }
        }

        public int DbConnectionTimeout
        {
            get { return _dbConnectionTimeout; }
        }

        internal string ClearIHSUpdateAction
        {
            get { return _clearIHSUpdateAction; }
        }

        #endregion

        #region Defaults

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process bulk OSMM Updates
        //
        /// <summary>
        /// Get the BAP determination quality default descriptions
        /// from the lookup table and update them in the settings.
        /// </summary>
        private void GetBapDefaults()
        {
            try
            {
                // Get the user added ('NP' = not present) description
                object result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_quality_determination.descriptionColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_quality_determination.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_quality_determination.codeColumn.ColumnName),
                    _db.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded)), _db.Connection.ConnectionTimeout, CommandType.Text);
                if (result != null)
                {
                    Settings.Default.BAPDeterminationQualityUserAddedDesc = (string)result;
                    Settings.Default.Save();
                }

                // Get the previous ('PP' = previously present) description
                result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_quality_determination.descriptionColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_quality_determination.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_quality_determination.codeColumn.ColumnName),
                    _db.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious)), _db.Connection.ConnectionTimeout, CommandType.Text);
                if (result != null)
                {
                    Settings.Default.BAPDeterminationQualityPreviousDesc = (string)result;
                    Settings.Default.Save();
                }
            }
            catch
            {
            }
        }
        //---------------------------------------------------------------------

        #endregion

        #region Cursor

        public void ChangeCursor(Cursor cursorType, string processingMessage)
        {
            //TODO: ChanceCursor
            ProgressUpdate(processingMessage, -1, -1);

            //_windowCursor = cursorType;
            //_windowEnabled = cursorType != Cursors.Wait;

            //OnPropertyChanged(nameof(TabControlDataEnabled));
            //if (cursorType == Cursors.Arrow)
            //    _processingMsg = "Processing ...";
            //else
            //    _processingMsg = processingMessage;

            //OnPropertyChanged(nameof(StatusBar));
            //if (cursorType == Cursors.Wait)
            //    DispatcherHelper.DoEvents();
        }

        #endregion

        #region Button Images

        /// <summary>
        /// Get the image for the AddSecondaryHabitat button.
        /// </summary>
        public static ImageSource ButtonAddSecondaryHabitatImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Icons/AddRowSquare.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Get the image for the AddSecondaryHabitatList button.
        /// </summary>
        public static ImageSource ButtonAddSecondaryHabitatListImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Icons/AddScript.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Get the image for the ReadMapSelection button.
        /// </summary>
        public static ImageSource ButtonReadMapSelectionImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Icons/ReadMapSelection.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Get the image for the EditPriorityHabitats button.
        /// </summary>
        public static ImageSource ButtonEditPriorityHabitatsImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Icons/ZoomTable.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Get the image for the EditPotentialHabitats button.
        /// </summary>
        public static ImageSource ButtonEditPotentialHabitatsImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Icons/ZoomTable.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        #endregion Button Images

        #region Processing

        /// <summary>
        /// Is the form processing?
        /// </summary>
        public Visibility IsProcessing
        {
            get
            {
                if (_processStatus != null)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        private double _progressValue;

        /// <summary>
        /// Gets the value to set on the progress
        /// </summary>
        public double ProgressValue
        {
            get
            {
                return _progressValue;
            }
            set
            {
                _progressValue = value;

                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        private double _maxProgressValue;

        /// <summary>
        /// Gets the max value to set on the progress
        /// </summary>
        public double MaxProgressValue
        {
            get
            {
                return _maxProgressValue;
            }
            set
            {
                _maxProgressValue = value;

                OnPropertyChanged(nameof(MaxProgressValue));
            }
        }

        private string _processStatus;

        /// <summary>
        /// ProgressStatus Text
        /// </summary>
        public string ProcessStatus
        {
            get
            {
                return _processStatus;
            }
            set
            {
                _processStatus = value;

                OnPropertyChanged(nameof(ProcessStatus));
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(ProgressAnimating));
            }
        }

        private string _progressText;

        /// <summary>
        /// Progress bar Text
        /// </summary>
        public string ProgressText
        {
            get
            {
                return _progressText;
            }
            set
            {
                _progressText = value;

                OnPropertyChanged(nameof(ProgressText));
            }
        }

        /// <summary>
        /// Is the progress wheel animating?
        /// </summary>
        public Visibility ProgressAnimating
        {
            get
            {
                if (_progressText != null)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update the progress bar.
        /// </summary>
        /// <param name="processText"></param>
        /// <param name="progressValue"></param>
        /// <param name="maxProgressValue"></param>
        public void ProgressUpdate(string processText = null, int progressValue = -1, int maxProgressValue = -1)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // Check if the values have changed and update them if they have.
                if (progressValue >= 0)
                    ProgressValue = progressValue;

                if (maxProgressValue != 0)
                    MaxProgressValue = maxProgressValue;

                if (_maxProgressValue > 0)
                    ProgressText = _progressValue == _maxProgressValue ? "Done" : $@"{_progressValue * 100 / _maxProgressValue:0}%";
                else
                    ProgressText = null;

                ProcessStatus = processText;
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                  () =>
                  {
                      // Check if the values have changed and update them if they have.
                      if (progressValue >= 0)
                          ProgressValue = progressValue;

                      if (maxProgressValue != 0)
                          MaxProgressValue = maxProgressValue;

                      if (_maxProgressValue > 0)
                          ProgressText = _progressValue == _maxProgressValue ? "Done" : $@"{_progressValue * 100 / _maxProgressValue:0}%";
                      else
                          ProgressText = null;

                      ProcessStatus = processText;
                  });
            }
        }

        #endregion Processing

        #region User ID

        public string UserID
        {
            get
            {
                if (!String.IsNullOrEmpty(Environment.UserDomainName))
                    return Environment.UserDomainName + @"\" + Environment.UserName;
                else
                    return Environment.UserName;
            }
        }

        public string UserName
        {
            get { return _userName; }
        }

        public bool IsAuthorisedUser
        {
            get
            {
                if (_isAuthorisedUser == null) GetUserInfo();
                return _isAuthorisedUser == true;
            }
        }

        /// <summary>
        /// Checks the current userid is found in the lut_table, determines
        /// if the user has bulk update authority and retrieves the user's
        /// name.
        /// </summary>
        private void GetUserInfo()
        {
            try
            {
                object result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_user.bulk_updateColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_user.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_user.user_idColumn.ColumnName),
                    _db.QuoteValue(this.UserID)), _db.Connection.ConnectionTimeout, CommandType.Text);
                if (result != null)
                {
                    _isAuthorisedUser = true;
                    _canBulkUpdate = (bool)result;
                }
                else
                {
                    _isAuthorisedUser = false;
                    _canBulkUpdate = false;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR9 (Current userid)
                // Get the current user's username from the lut_table to display with
                // the userid in the 'About' box.
                //
                result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_user.user_nameColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_user.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_user.user_idColumn.ColumnName),
                    _db.QuoteValue(this.UserID)), _db.Connection.ConnectionTimeout, CommandType.Text);
                if (result != null)
                {
                    _userName = (string)result;
                }
                else
                {
                    _userName = "(guest)";
                }
                //---------------------------------------------------------------------
            }
            catch
            {
                _isAuthorisedUser = null;
                _canBulkUpdate = null;
            }
        }

        #endregion

        #region Close Command

        /// <summary>
        /// Returns the command that, when invoked, attempts
        /// to remove this workspace from the user interface.
        /// </summary>
        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                    _closeCommand = new RelayCommand(param => this.OnRequestClose(true));

                return _closeCommand;
            }
        }

        /// <summary>
        /// Raised when main window should be closed.
        /// </summary>
        public event EventHandler RequestClose;

        public void OnRequestClose(bool check)
        {
            // Set the event handler to close the application
            EventHandler handler = this.RequestClose;
            if (handler != null)
            {
                //---------------------------------------------------------------------
                // FIX: 106 Check if user is sure before closing application.
                //
                if ((check == false) || (MessageBox.Show("Close HLU Tool. Are you sure?", "HLU: Exit", MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes))
                {
                    // Indicate the application is closing.
                    _closing = true;
                    //---------------------------------------------------------------------

                    // Check there are no outstanding edits.
                    _readingMap = false;
                    MessageBoxResult userResponse = CheckDirty();

                    switch (userResponse)
                    {
                        case MessageBoxResult.Yes:
                            //if (!_viewModelUpd.Update()) return;
                            break;
                        case MessageBoxResult.No:
                            break;
                        case MessageBoxResult.Cancel:
                            return;
                    }

                    //TODO: ArcGIS
                    //if (HaveGisApp && MessageBox.Show(String.Format("Close {0} as well?",
                    //    _gisApp.ApplicationType), "HLU: Exit", MessageBoxButton.YesNo,
                    //    MessageBoxImage.Question) == MessageBoxResult.Yes)
                    //{
                    //    _gisApp.Close();

                    //    ScratchDb.CleanUp();

                    //    if (_exportMdbs != null)
                    //    {
                    //        foreach (string path in _exportMdbs)
                    //        {
                    //            try { File.Delete(path); }
                    //            catch { }
                    //        }
                    //    }
                    //}

                    //TODO: ArcGIS
                    //// Call the event handle to close the application
                    //handler(this, EventArgs.Empty);
                }
            }
        }

        //---------------------------------------------------------------------
        // FIX: 106 Check if user is sure before closing application.
        //
        /// <summary>
        /// Is the application already in the process of closing.
        /// </summary>
        /// <returns></returns>
        public bool IsClosing
        {
            get { return _closing; }
        }
        //---------------------------------------------------------------------

        #endregion

        #region Copy and Paste

        public WindowMainCopySwitches CopySwitches
        {
            get { return _copySwitches; }
            set { _copySwitches = value; }
        }

        void _copySwitches_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.StartsWith("Copy"))
                OnPropertyChanged(nameof(CanCopy));
            else
                OnPropertyChanged(nameof(CanPaste));
        }

        /// <summary>
        /// Copy command.
        /// </summary>
        public ICommand CopyCommand
        {
            get
            {
                if (_copyCommand == null)
                {
                    Action<object> copyAction = new(this.CopyClicked);
                    _copyCommand = new RelayCommand(copyAction, param => this.CanCopy);
                }
                return _copyCommand;
            }
        }

        private void CopyClicked(object param)
        {
            _copySwitches.CopyValues(this);
        }

        public bool CanCopy
        {
            get
            {
                return IncidCurrentRow != null && _copySwitches != null &&
                    typeof(WindowMainCopySwitches).GetProperties().Where(p => p.Name.StartsWith("Copy"))
                    .Any(p => (bool)typeof(WindowMainCopySwitches).GetProperty(p.Name)
                        .GetValue(_copySwitches, null));
            }
        }

        /// <summary>
        /// Paste command.
        /// </summary>
        public ICommand PasteCommand
        {
            get
            {
                if (_pasteCommand == null)
                {
                    Action<object> pasteAction = new(this.PasteClicked);
                    _pasteCommand = new RelayCommand(pasteAction, param => this.CanPaste);
                }
                return _pasteCommand;
            }
        }

        private void PasteClicked(object param)
        {
            _copySwitches.PasteValues(this);
        }

        public bool CanPaste
        {
            get
            {
                return IncidCurrentRow != null && _copySwitches != null &&
                    typeof(WindowMainCopySwitches).GetProperties().Where(p => !p.Name.StartsWith("Copy"))
                    .Any(p => typeof(WindowMainCopySwitches).GetProperty(p.Name)
                        .GetValue(_copySwitches, null) != null);
            }
        }

        #endregion

        #region Checks

        public bool CheckInSync(string action, string itemType, string itemTypes = "")
        {
            if (itemTypes == "")
                itemTypes = itemType;

            // Check if the GIS and database are in sync.
            if ((_toidsIncidGisCount > _toidsIncidDbCount) ||
               (_fragsIncidGisCount > _fragsIncidDbCount))
            {
                if (_fragsIncidGisCount == 1)
                    MessageBox.Show(string.Format("{0} feature not found in database.", itemType), string.Format("HLU: {0}", action),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(string.Format("{0} features not found in database.", itemTypes), string.Format("HLU: {0}", action),
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        #endregion Checks

        #region Navigation Commands

        /// <summary>
        /// Navigate to first record command.
        /// </summary>
        public ICommand NavigateFirstCommand
        {
            get
            {
                if (_navigateFirstCommand == null)
                {
                    Action<object> navigateFirstAction = new(this.NavigateFirstClicked);
                    _navigateFirstCommand = new RelayCommand(navigateFirstAction, param => this.CanNavigateBackward);
                }
                return _navigateFirstCommand;
            }
        }

        private async void NavigateFirstClicked(object param)
        {
            // Move to first record.
            await NavigateToRecordAsync(1);
        }

        /// <summary>
        /// Navigate to previous record command.
        /// </summary>
        public ICommand NavigatePreviousCommand
        {
            get
            {
                if (_navigatePreviousCommand == null)
                {
                    Action<object> navigatePreviousAction = new(this.NavigatePreviousClicked);
                    _navigatePreviousCommand = new RelayCommand(navigatePreviousAction, param => this.CanNavigateBackward);
                }
                return _navigatePreviousCommand;
            }
        }

        private async void NavigatePreviousClicked(object param)
        {
            // Move to previous record.
            await NavigateToRecordAsync(_incidCurrentRowIndex - 1);
        }

        public bool CanNavigateBackward
        {
            get { return IncidCurrentRowIndex > 1; }
        }

        /// <summary>
        /// Navigate to next record command.
        /// </summary>
        public ICommand NavigateNextCommand
        {
            get
            {
                if (_navigateNextCommand == null)
                {
                    Action<object> navigateNextAction = new(this.NavigateNextClicked);
                    _navigateNextCommand = new RelayCommand(navigateNextAction, param => this.CanNavigateForward);
                }
                return _navigateNextCommand;
            }
        }

        private async void NavigateNextClicked(object param)
        {
            // Move to next record.
            await NavigateToRecordAsync(_incidCurrentRowIndex + 1);
        }

        public bool CanNavigateForward
        {
            get
            {
                return ((IsFiltered && (IncidCurrentRowIndex < _incidSelection.Rows.Count)) ||
                    (!IsFiltered && (IncidCurrentRowIndex < _incidRowCount)));
            }
        }

        /// <summary>
        /// Navigate to last record command.
        /// </summary>
        public ICommand NavigateLastCommand
        {
            get
            {
                if (_navigateLastCommand == null)
                {
                    Action<object> navigateLastAction = new(this.NavigateLastClicked);
                    _navigateLastCommand = new RelayCommand(navigateLastAction, param => this.CanNavigateForward);
                }
                return _navigateLastCommand;
            }
        }

        private async void NavigateLastClicked(object param)
        {
            // Move to last record.
            await NavigateToRecordAsync(IsFiltered ? _incidSelection.Rows.Count : _incidRowCount);
        }

        private async Task NavigateToRecordAsync(int value)
        {
            // Show the wait cursor and processing message in the status area
            // whilst moving to the new Incid.
            ChangeCursor(Cursors.Wait, "Selecting record ...");

            // Move to the first record.
            await MoveIncidCurrentRowIndexAsync(value);

            ChangeCursor(Cursors.Arrow, null);

            // Check if the GIS and database are in sync.
            CheckInSync("Selection", "Map");
        }

        /// <summary>
        /// Navigate to the specified record command.
        /// </summary>
        public ICommand NavigateIncidCommand
        {
            get
            {
                if (_navigateIncidCommand == null)
                {
                    Action<object> navigateIncidAction = new(this.NavigateIncidClicked);
                    _navigateIncidCommand = new RelayCommand(navigateIncidAction, param => this.CanNavigateIncid);
                }
                return _navigateIncidCommand;
            }
        }

        public async void NavigateIncidClicked(object param)
        {
            if (param is string newText && int.TryParse(newText, out int value))
            {
                // Show the wait cursor and processing message in the status area
                // whilst moving to the new Incid.
                ChangeCursor(Cursors.Wait, "Selecting record ...");

                // Move to the required incid current row (don't wait).
                await MoveIncidCurrentRowIndexAsync(value);

                ChangeCursor(Cursors.Arrow, null);

                // Check if the GIS and database are in sync.
                CheckInSync("Selection", "Map");
            }
        }

        public bool CanNavigateIncid
        {
            get
            {
                return (IncidCurrentRowIndex > 1) ||
                    ((IsFiltered && (IncidCurrentRowIndex < _incidSelection.Rows.Count)) ||
                    (!IsFiltered && (IncidCurrentRowIndex < _incidRowCount)));
            }
        }

        #endregion

        #region Split

        /// <summary>
        /// Logical Split command.
        /// </summary>
        public ICommand LogicalSplitCommand
        {
            get
            {
                if (_logicalSplitCommand == null)
                {
                    Action<object> logicalSplitAction = new(this.LogicalSplitClicked);
                    _logicalSplitCommand = new RelayCommand(logicalSplitAction, param => this.CanLogicallySplit);
                }
                return _logicalSplitCommand;
            }
        }

        /// <summary>
        /// LogicalSplitCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private void LogicalSplitClicked(object param)
        {
            // Logically split the selected features (don't wait).
            LogicalSplitAsync();
        }

        /// <summary>
        /// LogicalSplitCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private async Task LogicalSplitAsync()
        {
            _autoSplit = false;

            // Get the GIS layer selection again (just in case)
            await ReadMapSelectionAsync(false);

            _autoSplit = true;

            // Check the selected rows are unique before attempting to split them.
            if (!_gisApp.SelectedRowsUnique())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database going out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            ViewModelWindowMainSplit vmSplit = new(this);

            // Execute the logical split and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmSplit.LogicalSplitAsync())
                NotifySplitMerge("Logical split completed.");
        }

        /// <summary>
        /// Physical Split command.
        /// </summary>
        public ICommand PhysicalSplitCommand
        {
            get
            {
                if (_physicalSplitCommand == null)
                {
                    Action<object> physicalSplitAction = new(this.PhysicalSplitClicked);
                    _physicalSplitCommand = new RelayCommand(physicalSplitAction, param => this.CanPhysicallySplit);
                }
                return _physicalSplitCommand;
            }
        }

        /// <summary>
        /// PhysicalSplitCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private void PhysicalSplitClicked(object param)
        {
            // Physically split the selected features (don't wait).
            PhysicalSplitAsync();
        }

        /// <summary>
        /// PhysicalSplitCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private async Task PhysicalSplitAsync()
        {
            _autoSplit = false;

            // Get the GIS layer selection again (just in case)
            await ReadMapSelectionAsync(false);

            _autoSplit = true;

            ViewModelWindowMainSplit vmSplit = new(this);

            // Execute the physical split and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmSplit.PhysicalSplitAsync())
                NotifySplitMerge("Physical split completed.");
        }

        /// <summary>
        /// At least one feature in selection that share the same incid, but *not* toid and toidfragid
        /// </summary>
        private bool CanLogicallySplit
        {
            get
            {
                return (_bulkUpdateMode == false && _osmmUpdateMode == false) &&
                    EditMode && !String.IsNullOrEmpty(Reason) && !String.IsNullOrEmpty(Process) &&
                    (_gisSelection != null) && (_incidsSelectedMapCount == 1) &&
                    ((_gisSelection.Rows.Count > 0) && ((_toidsSelectedMapCount > 1) || (_fragsSelectedMapCount > 0)) ||
                    (_gisSelection.Rows.Count == 1)) &&
                    // Only enable split/merge after select from map
                    (_filterByMap == true) &&
                    //---------------------------------------------------------------------
                    // CHANGED: CR7 (Split/merge options)
                    // Only enable logical split menu/button if a subset of all the
                    // features for the current incid have been selected.
                    ((_toidsIncidGisCount < _toidsIncidDbCount) ||
                    (_fragsIncidGisCount < _fragsIncidDbCount));
                //---------------------------------------------------------------------
            }
        }

        /// <summary>
        /// At least two features in selection that share the same incid, toid and toidfragid
        /// </summary>
        private bool CanPhysicallySplit
        {
            get
            {
                return (_bulkUpdateMode == false && _osmmUpdateMode == false) &&
                    EditMode && !String.IsNullOrEmpty(Reason) && !String.IsNullOrEmpty(Process) &&
                    (_gisSelection != null) && (_gisSelection.Rows.Count > 1) &&
                    // Only enable split/merge after select from map
                    (_filterByMap == true) &&
                    (_incidsSelectedMapCount == 1) && (_toidsSelectedMapCount == 1) && (_fragsSelectedMapCount == 1);
            }
        }

        #endregion

        #region Merge

        /// <summary>
        /// Logical Merge command.
        /// </summary>
        public ICommand LogicalMergeCommand
        {
            get
            {
                if (_logicalMergeCommand == null)
                {
                    Action<object> logcalMergeAction = new(this.LogicalMergeClicked);
                    _logicalMergeCommand = new RelayCommand(logcalMergeAction, param => this.CanLogicallyMerge);
                }
                return _logicalMergeCommand;
            }
        }

        /// <summary>
        /// LogicalMergeCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private void LogicalMergeClicked(object param)
        {
            // Logically merge the selected features (don't wait).
            LogicalMergeAsync();
        }

        /// <summary>
        /// LogicalMergeCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private async Task LogicalMergeAsync()
        {
            // Get the GIS layer selection again (just in case)
            await ReadMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!_gisApp.SelectedRowsUnique())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database going out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            ViewModelWindowMainMerge vmMerge = new(this);

            // Execute the logical merge and wait for the result.
            // Notify the user following the completion of the merge.
            if (await vmMerge.LogicalMergeAsync())
                NotifySplitMerge("Logical merge completed.");
        }

        /// <summary>
        /// Physical Merge command.
        /// </summary>
        public ICommand PhysicalMergeCommand
        {
            get
            {
                if (_physicalMergeCommand == null)
                {
                    Action<object> logcalMergeAction = new(this.PhysicalMergeClicked);
                    _physicalMergeCommand = new RelayCommand(logcalMergeAction, param => this.CanPhysicallyMerge);
                }
                return _physicalMergeCommand;
            }
        }

        /// <summary>
        /// PhysicalMergeCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private void PhysicalMergeClicked(object param)
        {
            // Physically merge the selected features (don't wait).
            PhysicalMergeAsync();
        }

        /// <summary>
        /// PhysicalMergeCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private async Task PhysicalMergeAsync()
        {
            // Get the GIS layer selection again (just in case)
            await ReadMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!_gisApp.SelectedRowsUnique())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database going out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            ViewModelWindowMainMerge vmMerge = new(this);

            // Execute the physical merge and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmMerge.PhysicalMergeAsync())
                NotifySplitMerge("Physical merge completed.");
        }


        /// <summary>
        /// At least one feature in selection that do not share the same incid or toidfragid
        /// </summary>
        private bool CanLogicallyMerge
        {
            get
            {
                return (_bulkUpdateMode == false && _osmmUpdateMode == false) &&
                    EditMode && !String.IsNullOrEmpty(Reason) && !String.IsNullOrEmpty(Process) &&
                    _gisSelection != null && _gisSelection.Rows.Count > 1 &&
                    // Only enable split/merge after select from map
                    (_filterByMap == true) &&
                    (_incidsSelectedMapCount > 1) && (_fragsSelectedMapCount > 1);
            }
        }

        /// <summary>
        /// At least one feature in selection that share the same incid and toid but *not* the same toidfragid
        /// </summary>
        private bool CanPhysicallyMerge
        {
            get
            {
                return (_bulkUpdateMode == false && _osmmUpdateMode == false) &&
                    EditMode && !String.IsNullOrEmpty(Reason) && !String.IsNullOrEmpty(Process) &&
                    _gisSelection != null && _gisSelection.Rows.Count > 1 &&
                    // Only enable split/merge after select from map
                    (_filterByMap == true) &&
                    (_incidsSelectedMapCount == 1) && (_toidsSelectedMapCount == 1) && (_fragsSelectedMapCount > 1);
            }
        }

        #endregion

        #region Notify SplitMerge

        //---------------------------------------------------------------------
        // CHANGED: CR39 (Split and merge complete messages)
        // Check the options to see if the user wants to be notified
        // following the completion of a split or merge, and display
        // the supplied message if they do.
        //
        /// <summary>
        /// Notify the user following the completion of a split of merge
        /// if the options specify they want to be notified.
        /// </summary>
        private void NotifySplitMerge(string msgText)
        {
            if (_notifyOnSplitMerge)
            {
                _windowWarnSplitMerge = new()
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowWarnSplitMerge.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinWarnSplitMerge = new ViewModelWindowNotifyOnSplitMerge(msgText);

                // when ViewModel asks to be closed, close window
                _viewModelWinWarnSplitMerge.RequestClose +=
                    new ViewModelWindowNotifyOnSplitMerge.RequestCloseEventHandler(_viewModelWinWarnSplitMerge_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowWarnSplitMerge.DataContext = _viewModelWinWarnSplitMerge;

                // show window
                _windowWarnSplitMerge.ShowDialog();
            }
        }

        /// <summary>
        /// Update the user settings when the split merge request window is closed.
        /// </summary>
        void _viewModelWinWarnSplitMerge_RequestClose()
        {
            _viewModelWinWarnSplitMerge.RequestClose -= _viewModelWinWarnSplitMerge_RequestClose;
            _windowWarnSplitMerge.Close();

            // Update the user notify setting
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;

        }
        //---------------------------------------------------------------------

        #endregion

        #region Update

        /// <summary>
        /// Update command.
        /// </summary>
        public ICommand UpdateCommand
        {
            get
            {
                if (_updateCommand == null)
                {
                    Action<object> updateAction = new(this.UpdateClicked);
                    _updateCommand = new RelayCommand(updateAction, param => this.CanUpdate);
                }
                return _updateCommand;
            }
        }

        private void UpdateClicked(object param)
        {
            // Update the attributes (don't wait).
            UpdateAsync(param);
        }

        /// <summary>
        /// UpdateCommand event handler.
        /// </summary>
        /// <param name="param"></param>
        private async Task UpdateAsync(object param)
        {
            // Check if the GIS and database are in sync.
            if (!CheckInSync("Save", "Cannot save: Map"))
                return;

            // If there are no features selected in the GIS (because there is no
            // active filter).
            if (_incidsSelectedMapCount <= 0)
            {
                // Ask the user before re-selecting the current incid features in GIS.
                if (MessageBox.Show("There are no features selected in the GIS.\n" +
                            "Would you like to apply the changes to all features for this incid?", "HLU: Save",
                            MessageBoxButton.YesNoCancel, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Set the status to processing and the cursor to wait.
                    ChangeCursor(Cursors.Wait, "Selecting ...");

                    // Select all features for current incid
                    SelectOnMap(false);

                    // If there are still no features selected in the GIS this suggests
                    // that the feature layer contains only a subset of the database
                    // features so this incid cannot be updated.
                    if (_incidsSelectedMapCount <= 0)
                    {
                        // Reset the cursor back to normal
                        ChangeCursor(Cursors.Arrow, null);

                        return;
                    }

                    // Count the number of toids and fragments for the current incid
                    // selected in the GIS and in the database.
                    CountToidFrags();

                    // Refresh all the status type fields.
                    RefreshStatus();

                    // Reset the cursor back to normal
                    ChangeCursor(Cursors.Arrow, null);
                }
                else
                {
                    return;
                }
            }

            // If in bulk update mode then perform the bulk update and exit.
            if (_bulkUpdateMode == true)
            {
                BulkUpdateClicked(param);
                return;
            }

            // Check if the record has changed and if it hasn't ask the user
            // if they still want to update the record (to create new history).
            //
            // Currently, in theory, this can't happen because the Apply button
            // shouldn't be enabled unless some changes have been made by the
            // user. But this logic is retained just in case.
            MessageBoxResult userResponse = CheckClean();
            switch (userResponse)
            {
                case MessageBoxResult.Yes:
                    break;
                case MessageBoxResult.No:
                    Changed = false;
                    return;
                case MessageBoxResult.Cancel:
                    return;
            }

            // Set the saving in progress flags.
            _saving = true;
            _savingAttempted = false;

            // If there is no filter active (and hence all the features for the
            // current incid are to be updated) or all of the features for the
            // current incid have been selected in GIS then update them all and exit.
            if ((!IsFiltered) || (_fragsIncidGisCount == _fragsIncidDbCount))
            {
                // If saving hasn't already been attempted, when the features for
                // the current incid were selected in the map (above), then
                // do the update now.
                if (!_savingAttempted)
                {
                    // Update the current incid.
                    _saving = true;
                    _savingAttempted = false;
                    await _viewModelUpd.UpdateAsync();
                }
                return;
            }

            ChangeCursor(Cursors.Wait, "Filtering ...");

            DispatcherHelper.DoEvents();

            // Initialise the GIS selection table.
            _gisSelection = NewGisSelectionTable();

            // Recheck the selected features in GIS to make sure they
            // all belong to the current incid (passing a new GIS
            // selection table so that it knows the columns to return.
            _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);

            // Count the number of toids and fragments for the current incid
            // selected in the GIS and in the database.
            CountToidFrags();

            // Refresh all the status type fields.
            RefreshStatus();

            ChangeCursor(Cursors.Arrow, null);

            // If there are no features for the current incid
            // selected in GIS then cancel the update.
            if (_fragsIncidGisCount < 1)
            {
                MessageBox.Show("No map features for the current incid are selected in the active layer.",
                    "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // If all of the features for the current incid have been
            // selected in GIS then update them all.
            if (_fragsIncidGisCount == _fragsIncidDbCount)
            {
                _saving = true;
                _savingAttempted = false;
                await _viewModelUpd.UpdateAsync();
            }
            else
            {
                // Check if/how the subset of features for the incid should be updated.
                _updateCancelled = false;
                if (ConfirmSubsetUpdate())
                {
                    // The user does not want to update all the features for the incid
                    // then logically split the subset of features first
                    if (_updateAllFeatures == false)
                    {
                        // Set the status to processing and the cursor to wait.
                        ChangeCursor(Cursors.Wait, "Splitting ...");

                        // Logically split the features for the current incid into a new incid.
                        ViewModelWindowMainSplit vmSplit = new(this);
                        _splitting = true;
                        if (!await vmSplit.LogicalSplitAsync())
                        {
                            //MessageBox.Show("Could not complete logical split - update cancelled.\nPlease invoke the Split command before applying any updates.",
                            //    "HLU: Save", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            _updateCancelled = true;
                        }
                        _splitting = false;


                        // If the update failed then restore any active filter exactly as
                        // it was.
                        if (_updateCancelled == true)
                        {
                            // Reset the status message and the cursor.
                            ChangeCursor(Cursors.Arrow, null);
                            return;
                        }

                    }
                    // Apply the updates on the current incid.
                    _saving = true;
                    _savingAttempted = false;
                    await _viewModelUpd.UpdateAsync();

                    // Recount the number of toids and fragments for the current incid
                    // selected in the GIS and in the database.
                    CountToidFrags();

                    // Refresh all the status type fields.
                    RefreshStatus();

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map");
                }
                else
                {
                    MessageBox.Show("Cannot Save: The changes have not been applied - the update was cancelled.",
                        "HLU: Save", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    // Clear the saving in progress flags so that the (still) pending
                    // changes aren't automatically applied when moving to another
                    // incid (or refreshing the current incid).
                    _saving = false;
                    _savingAttempted = true;

                    return;
                }
            }
        }

        /// <summary>
        /// Update is disabled if not currently in edit mode, if no changes have been
        /// made by the user, if we're not currently in bulk update mode with no records
        /// selected, or if the current record is in error.
        /// </summary>
        public bool CanUpdate
        {
            get
            {
                //TODO: Check for errors.
                return EditMode &&
                    (Changed == true) &&
                    (Process != null) &&
                    (Reason != null) &&
                    (_bulkUpdateMode == false || _incidSelection != null) &&
                    String.IsNullOrEmpty(this.Error);
                //return EditMode &&
                //    (Changed == true) &&
                //    (_bulkUpdateMode == false || _incidSelection != null) &&
                //    String.IsNullOrEmpty(this.Error);
            }
        }

        /// <summary>
        /// Edit mode is enabled if the user is authorised (i.e. is in the lut_user table),
        /// if there is a GIS application known to be running and if the HLU Layer is currently
        /// being editing in GIS.
        /// </summary>
        public bool EditMode
        {
            get
            {
                // Return false if the current layer hasn't been set yet.
                if (_gisApp.CurrentHluLayer == null)
                    return false;

                // Check if the user is authorised and the HLU layer is editable.
                bool editMode = IsAuthorisedUser && _gisApp.CurrentHluLayer.IsEditable;

                // If the edit mode has changed then update the window properties.
                if (_editMode != editMode)
                {
                    _editMode = editMode;
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(CanBulkUpdate));
                    OnPropertyChanged(nameof(CanBulkUpdateMode));
                    OnPropertyChanged(nameof(CanOSMMUpdateMode));
                    OnPropertyChanged(nameof(CanOSMMBulkUpdateMode));
                    OnPropertyChanged(nameof(ShowReasonProcessGroup));
                    OnPropertyChanged(nameof(CanOSMMUpdateAccept));
                    OnPropertyChanged(nameof(CanOSMMUpdateReject));
                }
                return _editMode;
            }
            set { }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR10 (Attribute updates for incid subsets)
        // Check if the user still wants to go ahead because only a subset
        // of all the features in an incid have been selected. Also checks
        // if the user wants to logically split the subset of features first
        // or updates all the incid features.
        //
        /// <summary>
        /// Confirms with the user if the update is to go ahead.
        /// </summary>
        /// <returns>
        /// True if the update is to go ahead, or false if it is cancelled.
        /// </returns>
        private bool ConfirmSubsetUpdate()
        {
            // The user settings indicate that only the selected features
            // should be updated (by logically splitting them first).
            if (_subsetUpdateAction == 1)
            {
                _updateAllFeatures = false;
                return true;
            }
            // The user settings indicate that all the features for the incid
            // should be updated.
            else if (_subsetUpdateAction == 2)
            {
                _updateAllFeatures = true;
                return true;
            }
            // If the user settings do not indicate that all the features for the
            // incid should be updated, or that only the selected features should
            // be updated, then prompt the user for their choice.
            else
            {
                _updateCancelled = true;

                _windowWarnSubsetUpdate = new WindowWarnOnSubsetUpdate
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowWarnSubsetUpdate.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinWarnSubsetUpdate = new ViewModelWindowWarnOnSubsetUpdate(
                    _fragsIncidGisCount, _fragsIncidDbCount, _gisLayerType);

                // when ViewModel asks to be closed, close window
                _viewModelWinWarnSubsetUpdate.RequestClose +=
                    new ViewModelWindowWarnOnSubsetUpdate.RequestCloseEventHandler(_viewModelWinWarnSubsetUpdate_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowWarnSubsetUpdate.DataContext = _viewModelWinWarnSubsetUpdate;

                // show window
                _windowWarnSubsetUpdate.ShowDialog();

                return (!_updateCancelled);
            }
        }

        /// <summary>
        /// Proceed or cancel the update when the subset update warning
        /// window is closed.
        /// </summary>
        /// <param name="proceed">if set to <c>true</c> [proceed].</param>
        /// <param name="split">if set to <c>true</c> [split].</param>
        void _viewModelWinWarnSubsetUpdate_RequestClose(bool proceed, bool split, int? subsetUpdateAction)
        {
            _viewModelWinWarnSubsetUpdate.RequestClose -= _viewModelWinWarnSubsetUpdate_RequestClose;
            _windowWarnSubsetUpdate.Close();

            // If the user has set a default action for updating subsets of features
            if (subsetUpdateAction.HasValue)
            {
                // Update add-in option
                _subsetUpdateAction = subsetUpdateAction.Value;
                _addInSettings.SubsetUpdateAction = (int)_subsetUpdateAction;

                // Save changes back to XML.
                SaveAddInSettings(_addInSettings);

            }

            // If the user wants to proceed with the update then set whether they
            // want to update all the features or perform a logically split first.
            if (proceed)
            {
                _updateCancelled = false;
                if (split)
                    _updateAllFeatures = false;
                else
                    _updateAllFeatures = true;
            }
            else
            {
                _updateCancelled = true;
            }
            ChangeCursor(Cursors.Arrow, null);
        }
        //---------------------------------------------------------------------

        #endregion

        #region Bulk Update

        /// <summary>
        /// Action the bulk update.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void BulkUpdateClicked(object param)
        {
            _saving = false;
            if (_viewModelBulkUpdate == null)
                _viewModelBulkUpdate = new ViewModelWindowMainBulkUpdate(this, _addInSettings);

            // If already in bulk update mode then perform the bulk update
            // (only possible when this method was called after the 'Apply'
            // button was clicked.
            if (_bulkUpdateMode == true)
            {
                _viewModelBulkUpdate.BulkUpdate();
            }
            else
            {
                // Check there are no outstanding edits.
                _readingMap = false;
                MessageBoxResult userResponse = CheckDirty();

                // Ask the user if they want to apply the
                // outstanding edits.
                switch (userResponse)
                {
                    case MessageBoxResult.Yes:
                        //if (!_viewModelUpd.Update()) return;
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Functionality to process proposed OSMM Updates.
                //
                // Start the bulk update process.
                _viewModelBulkUpdate.StartBulkUpdate(false);
                //---------------------------------------------------------------------
            }
        }

        /// <summary>
        /// Is the user authorised for bulk updates?
        /// </summary>
        public bool CanBulkUpdate
        {
            get
            {
                if (_canBulkUpdate == null) GetUserInfo();

                return _canBulkUpdate == true;
            }
        }

        /// <summary>
        /// Can bulk update mode be started?
        /// </summary>
        public bool CanBulkUpdateMode
        {
            get
            {
                if (_canBulkUpdate == null) GetUserInfo();

                return EditMode &&
                    _canBulkUpdate == true &&
                    _osmmUpdateMode == false &&
                    _osmmBulkUpdateMode == false &&
                    (IsFiltered || _bulkUpdateMode == true);
            }
        }

        /// <summary>
        /// Gets the cancel bulk update command.
        /// </summary>
        /// <value>
        /// The cancel bulk update command.
        /// </value>
         public ICommand CancelBulkUpdateCommand
        {
            get
            {
                if (_cancelBulkUpdateCommand == null)
                {
                    Action<object> cancelBulkUpdateAction = new(this.CancelBulkUpdateClicked);
                    _cancelBulkUpdateCommand = new RelayCommand(cancelBulkUpdateAction, param => this.CanCancelBulkUpdate);
                }
                return _cancelBulkUpdateCommand;
            }
        }

         /// <summary>
         /// Cancel the bulk update.
         /// </summary>
         /// <param name="param">The parameter.</param>
        private void CancelBulkUpdateClicked(object param)
        {
            if (_viewModelBulkUpdate != null)
            {
                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Functionality to process proposed OSMM Updates.
                //
                // If the Cancel button has been clicked then we need
                // to work out which mode was active and cancel the
                // right one
                if (_osmmBulkUpdateMode == true)
                    _viewModelBulkUpdate.CancelOSMMBulkUpdate();
                else
                    //TODO: Await call.
                    _viewModelBulkUpdate.CancelBulkUpdate();
                //---------------------------------------------------------------------

                _viewModelBulkUpdate = null;
            }
        }

        /// <summary>
        /// Cancel the bulk update be cancelled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can cancel bulk update; otherwise, <c>false</c>.
        /// </value>
        public bool CanCancelBulkUpdate { get { return _bulkUpdateMode == true; } }

        internal Nullable<bool> BulkUpdateMode
        {
            get { return _bulkUpdateMode; }
            set
            {
                _bulkUpdateMode = value;

                // Refresh the state of the active layer combo box.
                UpdateComboBoxEnabledState();
            }
        }

        public Visibility HideInBulkUpdateMode
        {
            get
            {
                if (_bulkUpdateMode == true)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
            set { }
        }

        public Visibility ShowInBulkUpdateMode
        {
            get
            {
                if (_bulkUpdateMode == true)
                    return Visibility.Visible;
                else
                    return Visibility.Hidden;
            }
            set { }
        }

        public string BulkUpdateCommandHeader
        {
            get { return (_bulkUpdateMode == true && _osmmBulkUpdateMode == false) ? "Cancel _Bulk Apply Updates" : "_Bulk Apply Updates"; }
        }

        public ICommand BulkUpdateCommandMenu
        {
            get
            {
                if (_bulkUpdateCommandMenu == null)
                {
                    Action<object> bulkUpdateMenuAction = new(this.BulkUpdateCommandMenuClicked);
                    _bulkUpdateCommandMenu = new RelayCommand(bulkUpdateMenuAction);
                }
                return _bulkUpdateCommandMenu;
            }
        }

        private void BulkUpdateCommandMenuClicked(object param)
        {
            if (_bulkUpdateMode == true)
                CancelBulkUpdateClicked(param);
            else
                BulkUpdateClicked(param);
        }

        public string TopControlsGroupHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "INCID";
                else
                    return null;
            }
        }

        public string TopControlsBulkUpdateGroupHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Bulk Update";
                else
                    return null;
            }
        }

        #endregion

        #region OSMM Update

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        // Display and process proposed OSMM Updates.
        //
        /// <summary>
        /// Gets or sets the OSMM Accept button tag (which controls
        /// the text on the button (and whether the <Ctrl> button is
        /// pressed or not.
        /// </summary>
        /// <value>
        /// The osmm accept tag.
        /// </value>
        public string OSMMAcceptTag
        {
            get { return _osmmAcceptTag; }
            set
            {
                _osmmAcceptTag = value;
                if (_osmmUpdateMode == true)
                    OnPropertyChanged(nameof(OSMMAcceptText));
            }
        }

        /// <summary>
        /// Gets or sets the OSMM Reject button tag (which controls
        /// the text on the button (and whether the <Ctrl> button is
        /// pressed or not.
        /// </summary>
        /// <value>
        /// The osmm reject tag.
        /// </value>
        public string OSMMRejectTag
        {
            get { return _osmmRejectTag; }
            set
            {
                _osmmRejectTag = value;
                if (_osmmUpdateMode == true)
                    OnPropertyChanged(nameof(OSMMRejectText));
            }
        }

        /// <summary>
        /// Start the OSMM Update mode.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMUpdateClicked(object param)
        {
            // Can't start OSMM Update mode if the bulk OSMM source hasn't been set.
            if (_addInSettings.BulkOSMMSourceId == null)
            {
                MessageBox.Show("The Bulk OSMM Source has not been set.\n\n" +
                    "Please set the Bulk OSMM Source in the Options.",
                    "HLU: OSMM Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            _saving = false;
            if (_viewModelOSMMUpdate == null)
                _viewModelOSMMUpdate = new ViewModelWindowMainOSMMUpdate(this);

            // If the OSMM update mode is not already started.
            if (_osmmUpdateMode == false)
            {
                // Check there are no outstanding edits.
                _readingMap = false;
                MessageBoxResult userResponse = CheckDirty();

                switch (userResponse)
                {
                    case MessageBoxResult.Yes:
                        //if (!_viewModelUpd.Update()) return;
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }

                // Prevent OSMM updates being actioned too quickly.
                _osmmUpdating = false;

                OnPropertyChanged(nameof(CanOSMMAccept));
                OnPropertyChanged(nameof(CanOSMMSkip));

                // If there is nothing selected force the form to be cleared
                // and open the OSMM Updates query window.
                if (_incidSelection == null || _incidSelection.Rows.Count == 0)
                {
                    // Clear all the form fields (except the habitat class
                    // and habitat type).
                    ClearForm();

                    // Open the OSMM Updates query window.
                    OpenWindowQueryOSMM(true);
                }

                // Start the OSMM update mode
                _viewModelOSMMUpdate.StartOSMMUpdate();
            }
        }

        /// <summary>
        /// Can OSMM Update mode be started?
        /// </summary>
        public bool CanOSMMUpdateMode
        {
            get
            {
                if (_canOSMMUpdate == null)
                {
                    // Check if the user can process OSMM Updates.
                    if (CanBulkUpdate)
                    {
                        // Check if there are incid OSMM updates in the database
                        int incidOSMMUpdatesRowCount = (int)_db.ExecuteScalar(String.Format(
                            "SELECT COUNT(*) FROM {0}", _db.QualifyTableName(_hluDS.incid_osmm_updates.TableName)),
                            _db.Connection.ConnectionTimeout, CommandType.Text);

                        if (incidOSMMUpdatesRowCount > 0)
                            _canOSMMUpdate = true;
                        else
                            _canOSMMUpdate = false;
                    }
                    else
                        _canOSMMUpdate = false;
                }

                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                //
                // Can start OSMM Update mode if in edit mode,
                // and user is authorised,
                // and not currently in bulk update mode.
                return EditMode &&
                        _canOSMMUpdate == true &&
                       _bulkUpdateMode == false;
                //---------------------------------------------------------------------
            }
        }

        /// <summary>
        /// Cancel the OSMM Update mode.
        /// </summary>
        /// <param name="param"></param>
        private void CancelOSMMUpdateClicked(object param)
        {
            if (_viewModelOSMMUpdate != null)
            {
                _osmmUpdatesEmpty = false;

                _viewModelOSMMUpdate.CancelOSMMUpdate();

                _viewModelOSMMUpdate = null;
                // Prevent OSMM updates being actioned too quickly.
                _osmmUpdating = false;
            }
        }

        public bool CanCancelOSMMUpdate { get { return _osmmUpdateMode == true; } }

        /// <summary>
        /// OSMM Skip command.
        /// </summary>
        public ICommand OSMMSkipCommand
        {
            get
            {
                if (_osmmSkipCommand == null)
                {
                    Action<object> osmmSkipAction = new(this.OSMMSkipClicked);
                    //---------------------------------------------------------------------
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    //
                    _osmmSkipCommand = new RelayCommand(osmmSkipAction, param => this.CanOSMMSkip);
                    //---------------------------------------------------------------------
                }
                return _osmmSkipCommand;
            }
        }

        /// <summary>
        /// Skip the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMSkipClicked(object param)
        {

            // Skip the OSMM Update for the current incid (don't wait).
            OSMMSkipAsync();
        }

        /// <summary>
        /// Skip the OSMM Update for the current incid.
        /// </summary>
        private async Task OSMMSkipAsync()
        {
            // Prevent OSMM updates being actioned too quickly.
            // Mark the OSMM Update row as skipped
            // If there are any OSMM Updates for this incid then store the values.
            if (_osmmUpdating == false && _osmmUpdatesEmpty == false)
            {
                if (IncidOSMMStatus > 0)
                {
                    _osmmUpdating = true;

                    // Mark the OSMM Update row as skipped
                    _viewModelOSMMUpdate.OSMMUpdate(1);

                    //---------------------------------------------------------------------
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    //
                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);
                    //---------------------------------------------------------------------

                    _osmmUpdating = false;
                    //---------------------------------------------------------------------
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    //
                    OnPropertyChanged(nameof(CanOSMMAccept));
                    OnPropertyChanged(nameof(CanOSMMSkip));
                    //---------------------------------------------------------------------
                }
                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                //
                else
                {
                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);
                }
                //---------------------------------------------------------------------

                // Check if the GIS and database are in sync.
                CheckInSync("Selection", "Map");
            }
        }

        /// <summary>
        /// Can the proposed OSMM Update for the current incid
        /// be skipped?
        /// </summary>
        public bool CanOSMMSkip
        {
            get
            {
                // Check if there are proposed OSMM Updates
                // for the current filter.
                return (IsFiltered &&
                        _osmmUpdating == false &&
                        _osmmUpdatesEmpty == false &&
                        _incidOSMMUpdatesStatus != null);
            }
        }

        /// <summary>
        /// OSMM Accept command.
        /// </summary>
        public ICommand OSMMAcceptCommand
        {
            get
            {
                if (_osmmAcceptCommand == null)
                {
                    Action<object> osmmAcceptAction = new(this.OSMMAcceptClicked);
                    _osmmAcceptCommand = new RelayCommand(osmmAcceptAction, param => this.CanOSMMAccept);
                }
                return _osmmAcceptCommand;
            }
        }

        /// <summary>
        /// Accept the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMAcceptClicked(object param)
        {
            // Accept the OSMM Update for the current incid. (don't wait).
            OSMMAcceptAsync();
        }

        /// <summary>
        /// Accept the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param"></param>
        private async Task OSMMAcceptAsync()
        {
            // Prevent OSMM updates being actioned too quickly.
            if (_osmmUpdating == false)
            {
                _osmmUpdating = true;

                if (OSMMAcceptTag == "Ctrl")
                {
                    // Mark all the remaining OSMM Update rows as accepted
                    _viewModelOSMMUpdate.OSMMUpdateAll(0);

                    // Reset the button tags
                    OSMMAcceptTag = "";
                    OSMMRejectTag = "";
                }
                else
                {
                    // Mark the OSMM Update row as accepted
                    _viewModelOSMMUpdate.OSMMUpdate(0);

                    //---------------------------------------------------------------------
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    //
                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);
                    //---------------------------------------------------------------------
                }

                _osmmUpdating = false;
                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                //
                OnPropertyChanged(nameof(CanOSMMAccept));
                OnPropertyChanged(nameof(CanOSMMSkip));
                //---------------------------------------------------------------------

                // Check if the GIS and database are in sync.
                CheckInSync("Selection", "Map");
            }
        }

        /// <summary>
        /// Can the proposed OSMM Update for the current incid
        /// be processed?
        /// </summary>
        public bool CanOSMMAccept
        {
            get
            {
                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                // Prevent OSMM updates being actioned too quickly.
                // Check if there are no proposed OSMM Updates
                // for the current filter.
                return (_osmmUpdating == false &&
                    _osmmUpdatesEmpty == false &&
                    _incidOSMMUpdatesStatus != null &&
                    (_incidOSMMUpdatesStatus > 0 || _incidOSMMUpdatesStatus < -1));
                    //(_incidOSMMUpdatesStatus == null || (_incidOSMMUpdatesStatus > 0 || _incidOSMMUpdatesStatus < -1)));
                //---------------------------------------------------------------------
            }
        }

        /// <summary>
        /// OSMM Reject command.
        /// </summary>
        public ICommand OSMMRejectCommand
        {
            get
            {
                if (_osmmRejectCommand == null)
                {
                    Action<object> osmmRejectAction = new(this.OSMMRejectClicked);
                    _osmmRejectCommand = new RelayCommand(osmmRejectAction, param => this.CanOSMMAccept);
                }
                return _osmmRejectCommand;
            }
        }

        /// <summary>
        /// Reject the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMRejectClicked(object param)
        {
            // Reject the OSMM Update for the current incid (don't wait).
            OSMMRejectAsync();
        }

        /// <summary>
        /// Reject the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param"></param>
        private async Task OSMMRejectAsync()
        {
            // Prevent OSMM updates being actioned too quickly.
            if (_osmmUpdating == false)
            {
                _osmmUpdating = true;

                if (OSMMRejectTag == "Ctrl")
                {
                    // Mark all the remaining OSMM Update rows as accepted
                    _viewModelOSMMUpdate.OSMMUpdateAll(-99);

                    // Reset the button tags
                    OSMMAcceptTag = "";
                    OSMMRejectTag = "";
                }
                else
                {
                    // Mark the OSMM Update row as rejected
                    _viewModelOSMMUpdate.OSMMUpdate(-99);

                    //---------------------------------------------------------------------
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    //
                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);
                    //---------------------------------------------------------------------

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map");
                }

                _osmmUpdating = false;
                //---------------------------------------------------------------------
                // FIX: 103 Accept/Reject OSMM updates in edit mode.
                //
                OnPropertyChanged(nameof(CanOSMMAccept));
                OnPropertyChanged(nameof(CanOSMMSkip));
                //---------------------------------------------------------------------
            }
        }

        /// <summary>
        /// If the OSMM Update mode active?
        /// </summary>
        internal Nullable<bool> OSMMUpdateMode
        {
            get { return _osmmUpdateMode; }
            set
            {
                _osmmUpdateMode = value;

                // Refresh the state of the active layer combo box.
                UpdateComboBoxEnabledState();
            }
        }

        /// <summary>
        /// Hide some controls when in OSMM Update mode.
        /// </summary>
        public Visibility HideInOSMMUpdateMode
        {
            get
            {
                if (_osmmUpdateMode == true)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
            set { }
        }

        /// <summary>
        /// Show some controls when in OSMM Update mode.
        /// </summary>
        public Visibility ShowInOSMMUpdateMode
        {
            get
            {
                // Show the group if in osmm update mode
                if (_osmmUpdateMode == true)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        /// <summary>
        /// Set the menu item text depending on whether in OSMM Update mode.
        /// </summary>
        public string OSMMUpdateCommandHeader
        {
            get { return _osmmUpdateMode == true ? "Cancel Review OSMM Updates" : "Review OSMM Updates"; }
        }

        /// <summary>
        /// OSMM Update menu command.
        /// </summary>
        public ICommand OSMMUpdateCommandMenu
        {
            get
            {
                if (_osmmUpdateCommandMenu == null)
                {
                    Action<object> osmmUpdateMenuAction = new(this.OSMMUpdateCommandMenuClicked);
                    _osmmUpdateCommandMenu = new RelayCommand(osmmUpdateMenuAction);
                }
                return _osmmUpdateCommandMenu;
            }
        }

        /// <summary>
        /// Start or cancel the OSMM Update mode (as appropriate).
        /// </summary>
        /// <param name="param"></param>
        private void OSMMUpdateCommandMenuClicked(object param)
        {
            // If already in OSMM update mode
            if (_osmmUpdateMode == true)
            {
                // Cancel the OSMM update mode
                CancelOSMMUpdateClicked(param);
            }
            else
            {
                // Start the OSMM update mode
                OSMMUpdateClicked(param);
            }
        }

        /// <summary>
        /// Whether to create incid history for processing OSMM Updates.
        /// </summary>
        public bool OSMMUpdateCreateHistory
        {
            get { return _osmmUpdateCreateHistory; }
            set { _osmmUpdateCreateHistory = value; }
        }

        /// <summary>
        /// Set the Reject button caption depending on whether the Ctrl button
        /// is held down.
        /// </summary>
        public string OSMMRejectText
        {
            get { return OSMMRejectTag == "Ctrl" ? "Re_ject All" : "Re_ject"; }
        }

        /// <summary>
        /// Set the Accept button caption depending on whether the Ctrl button
        /// is held down.
        /// </summary>
        public string OSMMAcceptText
        {
            get { return OSMMAcceptTag == "Ctrl" ? "A_ccept All" : "A_ccept"; }
        }

        /// <summary>
        /// Get the row counter for the current incid.
        /// </summary>
        public int OSMMIncidCurrentRowIndex
        {
            get { return _osmmUpdatesEmpty ? 0 : _incidCurrentRowIndex; }
        }
        //---------------------------------------------------------------------

        #endregion

        #region OSMM Update Edits

        //---------------------------------------------------------------------
        // FIX: 103 Accept/Reject OSMM updates in edit mode.
        //
        /// <summary>
        /// Accept the proposed OSMM Update.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMUpdateAcceptClicked()
        {
            //Accept the proposed OSMM Update (don't wait).
            OSMMUpdateAcceptAsync();
        }

        //---------------------------------------------------------------------
        // FIX: 103 Accept/Reject OSMM updates in edit mode.
        //
        /// <summary>
        /// Accept the proposed OSMM Update.
        /// </summary>
        /// <param name="param"></param>
        private async Task OSMMUpdateAcceptAsync()
        {
            if (_viewModelOSMMUpdate == null)
                _viewModelOSMMUpdate = new ViewModelWindowMainOSMMUpdate(this);

            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            // Save the current incid
            int incidCurrRowIx = IncidCurrentRowIndex;
            //---------------------------------------------------------------------

            // Mark the OSMM Update row as accepted
            _viewModelOSMMUpdate.OSMMUpdate(0);

            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            // Reload the incid
            await MoveIncidCurrentRowIndexAsync(incidCurrRowIx);

            //OnPropertyChanged(nameof(CanOSMMUpdateAccept));
            //---------------------------------------------------------------------

            // Check if the GIS and database are in sync.
            CheckInSync("Selection", "Map");
        }

        /// <summary>
        /// Can OSMM Update be accepted?
        /// </summary>
        public bool CanOSMMUpdateAccept
        {
            get
            {
                // If not in a bulk mode and a proposed OSMM update is showing
                if (EditMode &&
                    _osmmUpdateMode == false &&
                    _bulkUpdateMode == false &&
                    (_showOSMMUpdates == "Always" ||
                    _showOSMMUpdates == "When Outstanding") &&
                    IncidOSMMStatus > 0)
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Reject the proposed OSMM Update.
        /// </summary>
        private void OSMMUpdateRejectClicked()
        {
            //Reject the proposed OSMM Update (don't wait).
            OSMMUpdateRejectAsync();
        }

        /// <summary>
        /// Reject the proposed OSMM Update.
        /// </summary>
        /// <param name="param"></param>
        private async Task OSMMUpdateRejectAsync()
        {
            if (_viewModelOSMMUpdate == null)
                _viewModelOSMMUpdate = new ViewModelWindowMainOSMMUpdate(this);

            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            // Save the current incid
            int incidCurrRowIx = IncidCurrentRowIndex;
            //---------------------------------------------------------------------

            // Mark the OSMM Update row as rejected
            _viewModelOSMMUpdate.OSMMUpdate(-99);

            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            // Reload the incid
            await MoveIncidCurrentRowIndexAsync(incidCurrRowIx);

            //OnPropertyChanged(nameof(CanOSMMUpdateReject));
            //---------------------------------------------------------------------

            // Check if the GIS and database are in sync.
            CheckInSync("Selection", "Map");
        }

        /// <summary>
        /// Can OSMM Update be rejected?
        /// </summary>
        public bool CanOSMMUpdateReject
        {
            get
            {
                // If not in a bulk mode and a proposed OSMM update is showing
                if (EditMode &&
                    _osmmUpdateMode == false &&
                    _bulkUpdateMode == false &&
                    (_showOSMMUpdates == "Always" ||
                    _showOSMMUpdates == "When Outstanding") &&
                    IncidOSMMStatus > 0)
                    return true;
                else
                    return false;
            }
        }
        //---------------------------------------------------------------------

        #endregion

        #region OSMM Bulk Update

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        // Bulk apply pending OSMM Updates.
        //
        /// <summary>
        /// Start the OSMM Bulk Update mode.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMBulkUpdateClicked(object param)
        {
            _saving = false;
            if (_viewModelBulkUpdate == null)
                _viewModelBulkUpdate = new ViewModelWindowMainBulkUpdate(this, _addInSettings);

            // If the OSMM Bulk update mode is not already started.
            if (_osmmBulkUpdateMode == false)
            {
                // Check there are no outstanding edits.
                _readingMap = false;
                MessageBoxResult userResponse = CheckDirty();

                switch (userResponse)
                {
                    case MessageBoxResult.Yes:
                        //if (!_viewModelUpd.Update()) return;
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }

                // Start the OSMM update mode
                _viewModelBulkUpdate.StartBulkUpdate(true);
            }
        }

        /// <summary>
        /// Can OSMM Bulk Update mode be started?
        /// </summary>
        public bool CanOSMMBulkUpdateMode
        {
            get
            {
                if (_canOSMMUpdate == null)
                {
                    // Check if the user can process OSMM Updates.
                    if (CanBulkUpdate)
                    {
                        // Check if there are incid OSMM updates in the database
                        int incidOSMMUpdatesRowCount = (int)_db.ExecuteScalar(String.Format(
                            "SELECT COUNT(*) FROM {0}", _db.QualifyTableName(_hluDS.incid_osmm_updates.TableName)),
                            _db.Connection.ConnectionTimeout, CommandType.Text);

                        if (incidOSMMUpdatesRowCount > 0)
                            _canOSMMUpdate = true;
                        else
                            _canOSMMUpdate = false;
                    }
                    else
                        _canOSMMUpdate = false;
                }

                // Can start OSMM Bulk Update mode if in edit mode,
                // and user is authorised,
                // and there are incid OSMM updates in the database,
                // and not currently in bulk update mode or osmm update mode.
                return EditMode &&
                    _canOSMMUpdate == true &&
                    _osmmUpdateMode == false &&
                    (_bulkUpdateMode == false || (_bulkUpdateMode == true && _osmmBulkUpdateMode == true));
            }
        }

        /// <summary>
        /// Cancel the OSMM Bulk Update mode.
        /// </summary>
        /// <param name="param"></param>
        private void CancelOSMMBulkUpdateClicked(object param)
        {
            if (_viewModelBulkUpdate != null)
            {
                _viewModelBulkUpdate.CancelOSMMBulkUpdate();
                _viewModelBulkUpdate = null;
            }
        }

        public bool CanCancelOSMMBulkUpdate { get { return _osmmBulkUpdateMode == true; } }

        /// <summary>
        /// If the OSMM Bulk Update mode active?
        /// </summary>
        internal Nullable<bool> OSMMBulkUpdateMode
        {
            get { return _osmmBulkUpdateMode; }
            set { _osmmBulkUpdateMode = value; }
        }

        /// <summary>
        /// Set the menu item text depending on whether in OSMM Bulk Update mode.
        /// </summary>
        public string OSMMBulkUpdateCommandHeader
        {
            get { return _osmmBulkUpdateMode == true ? "Cancel Bulk Apply OSMM Updates" : "Bulk Apply OSMM Updates"; }
        }

        /// <summary>
        /// OSMM Bulk Update menu command.
        /// </summary>
        public ICommand OSMMBulkUpdateCommandMenu
        {
            get
            {
                if (_osmmBulkUpdateCommandMenu == null)
                {
                    Action<object> osmmBulkUpdateMenuAction = new(this.OSMMBulkUpdateCommandMenuClicked);
                    _osmmBulkUpdateCommandMenu = new RelayCommand(osmmBulkUpdateMenuAction);
                }
                return _osmmBulkUpdateCommandMenu;
            }
        }

        /// <summary>
        /// Start or cancel the OSMM Bulk Update mode (as appropriate).
        /// </summary>
        /// <param name="param"></param>
        private void OSMMBulkUpdateCommandMenuClicked(object param)
        {
            // If already in OSMM Bulk update mode
            if (_osmmBulkUpdateMode == true)
                // Cancel the OSMM Bulk update mode
                CancelOSMMBulkUpdateClicked(param);
            else
                // Start the OSMM Bulk update mode
                OSMMBulkUpdateClicked(param);
        }
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // FIX: 103 Accept/Reject OSMM updates in edit mode.
        //
        /// <summary>
        /// OSMM Update Accept menu command.
        /// </summary>
        public ICommand OSMMUpdateAcceptCommandMenu
        {
            get
            {
                if (_osmmUpdateAcceptCommandMenu == null)
                {
                    Action<object> osmmUpdateAcceptMenuAction = new(this.OSMMUpdateAcceptCommandMenuClicked);
                    _osmmUpdateAcceptCommandMenu = new RelayCommand(osmmUpdateAcceptMenuAction);
                }
                return _osmmUpdateAcceptCommandMenu;
            }
        }

        /// <summary>
        /// Accept the OSMM proposed update.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMUpdateAcceptCommandMenuClicked(object param)
        {
            // Accept the OSMM proposed update
            OSMMUpdateAcceptClicked();
        }

        /// <summary>
        /// OSMM Update Reject menu command.
        /// </summary>
        public ICommand OSMMUpdateRejectCommandMenu
        {
            get
            {
                if (_osmmUpdateRejectCommandMenu == null)
                {
                    Action<object> osmmUpdateRejectMenuAction = new(this.OSMMUpdateRejectCommandMenuClicked);
                    _osmmUpdateRejectCommandMenu = new RelayCommand(osmmUpdateRejectMenuAction);
                }
                return _osmmUpdateRejectCommandMenu;
            }
        }

        /// <summary>
        /// Reject the OSMM proposed update.
        /// </summary>
        /// <param name="param"></param>
        private void OSMMUpdateRejectCommandMenuClicked(object param)
        {
            // Reject the OSMM proposed update
            OSMMUpdateRejectClicked();
        }
        //---------------------------------------------------------------------

        #endregion

        #region View

        public ICommand AutoZoomSelectedOffCommand
        {
            get
            {
                if (_autoZoomSelectedOffCommand == null)
                {
                    Action<object> autoZoomSelectionOffAction = new(this.AutoZoomSelectedOffClicked);
                    _autoZoomSelectedOffCommand = new RelayCommand(autoZoomSelectionOffAction, param => this.CanAutoZoomSelected);
                }
                return _autoZoomSelectedOffCommand;
            }
        }

        private void AutoZoomSelectedOffClicked(object param)
        {
            // Update the auto zoom on selected option.
            _autoZoomSelection = (int)AutoZoomSelection.Off;

            // Save the new auto zoom option in the user settings.
            Settings.Default.AutoZoomSelection = _autoZoomSelection;
            Settings.Default.Save();
        }

        public ICommand AutoZoomSelectedWhenCommand
        {
            get
            {
                if (_autoZoomSelectedWhenCommand == null)
                {
                    Action<object> autoZoomSelectionWhenAction = new(this.AutoZoomSelectedWhenClicked);
                    _autoZoomSelectedWhenCommand = new RelayCommand(autoZoomSelectionWhenAction, param => this.CanAutoZoomSelected);
                }
                return _autoZoomSelectedWhenCommand;
            }
        }

        private void AutoZoomSelectedWhenClicked(object param)
        {
            // Update the auto zoom on selected option.
            _autoZoomSelection = (int)AutoZoomSelection.When;

            // Save the new auto zoom option in the user settings.
            Settings.Default.AutoZoomSelection = _autoZoomSelection;
            Settings.Default.Save();
        }

        public ICommand AutoZoomSelectedAlwaysCommand
        {
            get
            {
                if (_autoZoomSelectedAlwaysCommand == null)
                {
                    Action<object> autoZoomSelectionAlwaysAction = new(this.AutoZoomSelectedAlwaysClicked);
                    _autoZoomSelectedAlwaysCommand = new RelayCommand(autoZoomSelectionAlwaysAction, param => this.CanAutoZoomSelected);
                }
                return _autoZoomSelectedAlwaysCommand;
            }
        }

        private void AutoZoomSelectedAlwaysClicked(object param)
        {
            // Update the auto zoom on selected option.
            _autoZoomSelection = (int)AutoZoomSelection.Always;

            // Save the new auto zoom option in the user settings.
            Settings.Default.AutoZoomSelection = _autoZoomSelection;
            Settings.Default.Save();
        }

        //TODO: Unneeded?
        public bool CanAutoZoomSelected { get { return true; } }

        public ICommand AutoSelectOnGisCommand
        {
            get
            {
                if (_autoSelectOnGisCommand == null)
                {
                    Action<object> autoSelectiOnGisAction = new(this.AutoSelectOnGisClicked);
                    _autoSelectOnGisCommand = new RelayCommand(autoSelectiOnGisAction, param => this.CanAutoSelectOnGis);
                }
                return _autoSelectOnGisCommand;
            }
        }

        private void AutoSelectOnGisClicked(object param)
        {
            // Update the auto select on GIS option.
            _autoSelectOnGis = !_autoSelectOnGis;

            // Save the new auto select on GIS option in the user settings.
            Settings.Default.AutoSelectOnGis = _autoSelectOnGis;
            Settings.Default.Save();

        }

        //TODO: Unneeded?
        public bool CanAutoSelectOnGis { get { return true; } }

        public ICommand ZoomSelectionCommand
        {
            get
            {
                if (_zoomSelectionCommand == null)
                {
                    Action<object> zoomSelectionAction = new(this.ZoomSelectionClicked);
                    _zoomSelectionCommand = new RelayCommand(zoomSelectionAction, param => this.CanZoomSelection);
                }
                return _zoomSelectionCommand;
            }
        }

        private void ZoomSelectionClicked(object param)
        {
            // Get the minimum auto zoom value and map distance units.
            string distUnits = Settings.Default.MapDistanceUnits;

            _gisApp.ZoomSelected(_minZoom, distUnits, true);
        }

        public bool CanZoomSelection { get { return _gisSelection != null; } }

        #endregion

        #region Options

        /// <summary>
        /// Save the add-in settings.
        /// </summary>
        /// <param name="addInSettings"></param>
        public void SaveAddInSettings(AddInSettings addInSettings)
        {
            // Save the application settings.
            _xmlSettingsManager.SaveSettings(addInSettings);
        }

        /// <summary>
        /// Apply the settings in the main window.
        /// </summary>
        public void ApplySettings()
        {
            // Store old show source habitat group value
            bool _oldShowSourceHabitatGroup = _showSourceHabitatGroup;

            // Apply application settings.
            ApplyAddInSettings();

            // Apply user settings.
            ApplyUserSettings();

            // If the show source habitat group value has changed and
            // is now true, set the habitat class to null to force
            // the user default value to be set.
            if (_oldShowSourceHabitatGroup != _showSourceHabitatGroup
                && _showSourceHabitatGroup == true)
            {
                _habitatClass = null;
                OnPropertyChanged(nameof(HabitatClass));
            }

            // Refresh the user interface
            RefreshGroupHeaders();

            OnPropertyChanged(nameof(ShowSourceHabitatGroup));
            OnPropertyChanged(nameof(ShowHabitatSecondariesSuggested));
            OnPropertyChanged(nameof(ShowNVCCodes));
            OnPropertyChanged(nameof(ShowHabitatSummary));
            OnPropertyChanged(nameof(ShowIHSTab));
            OnPropertyChanged(nameof(ShowIncidOSMMPendingGroup));

            OnPropertyChanged(nameof(SecondaryGroupCodes));
            SecondaryGroup = _preferredSecondaryGroup;
            OnPropertyChanged(nameof(SecondaryGroup));

            OnPropertyChanged(nameof(SecondaryHabitatCodes));
            RefreshSecondaryHabitats();
            OnPropertyChanged(nameof(HabitatTabLabel));
            OnPropertyChanged(nameof(IncidSecondarySummary));

            OnPropertyChanged(nameof(IncidCondition));
            OnPropertyChanged(nameof(IncidConditionQualifier));
            OnPropertyChanged(nameof(IncidConditionDate));

            OnPropertyChanged(nameof(IncidQualityDetermination));
            OnPropertyChanged(nameof(IncidQualityInterpretation));
            OnPropertyChanged(nameof(IncidQualityComments));

            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(CanBulkUpdate));
        }

        private void ApplyAddInSettings()
        {
            // Apply add-in database options
            _dbConnectionTimeout = _addInSettings.DbConnectionTimeout;
            //TODO - Is IncidPageSize too dangerous to change on the fly?

            // Apply add-in dates options
            VagueDate.Delimiter = _addInSettings.VagueDateDelimiter; // Set in the vague date class
            VagueDate.SeasonNames = _addInSettings.SeasonNames.Cast<string>().ToArray(); // Set in the vague date class

            // Apply add-in validation options
            _habitatSecondaryCodeValidation = _addInSettings.HabitatSecondaryCodeValidation;
            _primarySecondaryCodeValidation = _addInSettings.PrimarySecondaryCodeValidation;
            SecondaryHabitat.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation; // Set in the secondary habitat class
            _qualityValidation = _addInSettings.QualityValidation;
            _potentialPriorityDetermQtyValidation = _addInSettings.PotentialPriorityDetermQtyValidation;
            BapEnvironment.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation; // Used in the priority habitat class

            // Apply add-in updates options
            _subsetUpdateAction = _addInSettings.SubsetUpdateAction;
            _clearIHSUpdateAction = _addInSettings.ClearIHSUpdateAction;
            _secondaryCodeDelimiter = _addInSettings.SecondaryCodeDelimiter;
            _resetOSMMUpdatesStatus = _addInSettings.ResetOSMMUpdatesStatus;

            // Apply add-in bulk update options
            //None - done in bulk update class.
        }

        private void ApplyUserSettings()
        {
            // Apply user GIS options
            _minZoom = Settings.Default.MinAutoZoom;

            // Apply user history options
            _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;
            _historyColumns = InitializeHistoryColumns(_historyColumns);

            // Apply user interface options
            _preferredHabitatClass = Settings.Default.PreferredHabitatClass;
            _showGroupHeaders = Settings.Default.ShowGroupHeaders;
            _showIHSTab = Settings.Default.ShowIHSTab;
            _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
            _showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
            _showNVCCodes = Settings.Default.ShowNVCCodes;
            _showHabitatSummary = Settings.Default.ShowHabitatSummary;
            _showOSMMUpdates = Settings.Default.ShowOSMMUpdatesOption;
            _preferredSecondaryGroup = Settings.Default.PreferredSecondaryGroup;
            _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;

            // Apply user updates options
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;

            // Apply user SQL options
            _warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;
        }

        #endregion

        #region About

        /// <summary>
        /// Gets the about command.
        /// </summary>
        /// <value>
        /// The about command.
        /// </value>
        public ICommand AboutCommand
        {
            get
            {
                if (_aboutCommand == null)
                {
                    Action<object> aboutAction = new(this.AboutClicked);
                    _aboutCommand = new RelayCommand(aboutAction);
                }
                return _aboutCommand;
            }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR9 (Current userid)
        // Retrieve the copyright notice for the assembly to display with the
        // current userid and name in the 'About' box.
        //
        /// <summary>
        /// Gets the assembly copyright notice.
        /// </summary>
        /// <value>The assembly copyright.</value>
        public string AssemblyCopyright
        {
            get
            {
                // Get all Copyright attributes on this assembly
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                // If there aren't any Copyright attributes, return an empty string
                if (attributes.Length == 0)
                    return null;
                // Split the copyright statement at each full stop and
                // wrap it to a new line.
                String copyright = String.Join(Environment.NewLine, ((AssemblyCopyrightAttribute)attributes[0]).Copyright.Split('.'));
                // If there is a Copyright attribute, return its value
                return copyright;
            }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Show the about window.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void AboutClicked(object param)
        {
            //---------------------------------------------------------------------
            // CHANGED: CR30 (Database validation on startup)
            // Show the database version in the 'About' box.
            //
            // CHANGED: CR9 (Current userid)
            // Show the current userid and username together with the version
            // and copyright notice in the 'About' box.
            //
            string dbBackend;
            dbBackend = String.Format("{0}{1}{2}{3}",
                _db.Backend.ToString(),
                String.IsNullOrEmpty(_db.DefaultSchema) ? null : " (",
                _db.DefaultSchema,
                String.IsNullOrEmpty(_db.DefaultSchema) ? null : ")");
            string dbSettings;
            dbSettings = _db.ConnectionString.Replace(";", "\n");
            //---------------------------------------------------------------------

            _windowAbout = new WindowAbout
            {
                //DONE: App.Current.MainWindow
                //_windowAbout.Owner = App.Current.MainWindow;
                //TODO: ArcGIS
                //WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            // Create ViewModel to which main window binds
            _viewModelAbout = new ViewModelWindowAbout
            {
                AppVersion = String.Format("{0} {1}", _appVersion, _betaVersion ? "[Beta]" : null),
                DbVersion = _dbVersion,
                DataVersion = _dataVersion,
                ConnectionType = dbBackend,
                ConnectionSettings = dbSettings,
                UserId = UserID,
                UserName = UserName,
                Copyright = AssemblyCopyright,
                UserGuideURL = "https://readthedocs.org/projects/hlutool-userguide/",
                UserGuideText = "https://readthedocs.org/projects/hlutool-userguide/",
                TechnicalGuideURL = "https://readthedocs.org/projects/hlutool-technicalguide/",
                TechnicalGuideText = "https://readthedocs.org/projects/hlutool-technicalguide/"
            };

            // When ViewModel asks to be closed, close window
            _viewModelAbout.RequestClose += new ViewModelWindowAbout.RequestCloseEventHandler(_viewModelAbout_RequestClose);

            // Allow all controls in window to bind to ViewModel by setting DataContext
            _windowAbout.DataContext = _viewModelAbout;

            // Show window
            _windowAbout.ShowDialog();
        }

        /// <summary>
        /// Closes about window and removes close window handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        internal void _viewModelAbout_RequestClose()
        {
            _viewModelAbout.RequestClose -= _viewModelAbout_RequestClose;
            _windowAbout.Close();
        }

        #endregion

        #region Export

        /// <summary>
        /// Export command.
        /// </summary>
        public ICommand ExportCommand
        {
            get
            {
                if (_exportCommand == null)
                {
                    Action<object> exportAction = new(this.ExportClicked);
                    _exportCommand = new RelayCommand(exportAction, param => this.CanExport);
                }
                return _exportCommand;
            }
        }

        /// <summary>
        /// Initiates the exports process.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void ExportClicked(object param)
        {
            ViewModelWindowMainExport viewModelExport = new(this);
            viewModelExport.InitiateExport();
        }

        public bool CanExport { get { return _bulkUpdateMode == false && _osmmUpdateMode == false && _hluDS != null; } }

        #endregion

        #region Filter by Attributes Command

        /// <summary>
        /// FilterByAttributes command.
        /// </summary>
        public ICommand FilterByAttributesCommand
        {
            get
            {
                if (_filterByAttributesCommand == null)
                {
                    Action<object> filterByAttributesAction = new(this.FilterByAttributesClicked);
                    _filterByAttributesCommand = new RelayCommand(filterByAttributesAction, param => this.CanFilterByAttributes);
                }
                return _filterByAttributesCommand;
            }
        }

        /// <summary>
        /// Opens the relevant query window based on the mode/options.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void FilterByAttributesClicked(object param)
        {
            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Open the OSMM Updates query window if in OSMM Update mode.
            //
            if (_osmmUpdateMode == true || _osmmBulkUpdateMode == true)
            {
                // Can't start OSMM Update mode if the bulk OSMM source hasn't been set.
                if (_addInSettings.BulkOSMMSourceId == null)
                {
                    MessageBox.Show("The Bulk OSMM Source has not been set.\n\n" +
                        "Please set the Bulk OSMM Source in the Options.",
                        "HLU: OSMM Update", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    return;
                }

                OpenWindowQueryOSMM(false);
            }
            else
            {
                // Open the select by attributes interface
                OpenWindowQueryAdvanced();
            }
            //---------------------------------------------------------------------
        }

        /// <summary>
        /// Gets a value indicating whether the filter by attributes command can
        /// be executed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by attributes; otherwise, <c>false</c>.
        /// </value>
        private bool CanFilterByAttributes
        {
            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Enable filter when in OSMM bulk update mode
            // 
            get
            {
                return (_bulkUpdateMode == false || (_bulkUpdateMode == true && _osmmBulkUpdateMode == true))
                && IncidCurrentRow != null;
            }
            //---------------------------------------------------------------------
        }

        /// <summary>
        /// FilterByAttributesOSMM command.
        /// </summary>
        public ICommand FilterByAttributesOSMMCommand
        {
            get
            {
                if (_filterByAttributesOSMMCommand == null)
                {
                    Action<object> filterByAttributesOSMMAction = new(this.FilterByAttributesOSMMClicked);
                    _filterByAttributesOSMMCommand = new RelayCommand(filterByAttributesOSMMAction, param => this.CanFilterByAttributesOSMM);
                }
                return _filterByAttributesOSMMCommand;
            }
        }

        /// <summary>
        /// Opens the relevant query window based on the mode/options.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void FilterByAttributesOSMMClicked(object param)
        {
            // Open the Advanced query window.
            OpenWindowQueryOSMMAdvanced(false);
        }

        /// <summary>
        /// Gets a value indicating whether the filter by attributes OSMM command can
        /// be executed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by attributes OSMM; otherwise, <c>false</c>.
        /// </value>
        private bool CanFilterByAttributesOSMM
        {
            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Enable filter when in OSMM bulk update mode
            // 
            get
            {
                return (_osmmUpdateMode == true && IncidCurrentRow != null);
            }
            //---------------------------------------------------------------------
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Opens the new advanced query window.
        /// </summary>
        /// <exception cref="System.Exception">No parent window loaded</exception>
        private void OpenWindowQueryAdvanced()
        {
            try
            {
                _windowQueryAdvanced = new WindowQueryAdvanced
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowQueryAdvanced.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryAdvanced = new(HluDataset, _db)
                {
                    DisplayName = "Advanced Query Builder"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryAdvanced.RequestClose +=
                    new ViewModelWindowQueryAdvanced.RequestCloseEventHandler(_viewModelWinQueryAdvanced_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryAdvanced.DataContext = _viewModelWinQueryAdvanced;

                // show window
                _windowQueryAdvanced.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Query by Filter", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        /// <summary>
        /// Process the sql when the advanced query window is closed.
        /// </summary>
        /// <param name="sqlFromTables">The tables to query.</param>
        /// <param name="sqlWhereClause">The where clause to apply in the query.</param>
        protected void _viewModelWinQueryAdvanced_RequestClose(string sqlFromTables, string sqlWhereClause)
        {
            _viewModelWinQueryAdvanced.RequestClose -= _viewModelWinQueryAdvanced_RequestClose;
            _windowQueryAdvanced.Close();

            if ((sqlFromTables != null) && (sqlWhereClause != null))
            {
                try
                {
                    ChangeCursor(Cursors.Wait, "Validating ...");

                    // Get a list of all the possible query tables.
                    List<DataTable> tables = [];
                    if ((ViewModelWindowQueryAdvanced.HluDatasetStatic != null))
                    {
                        tables = ViewModelWindowQueryAdvanced.HluDatasetStatic.incid.ChildRelations
                            .Cast<DataRelation>().Select(r => r.ChildTable).ToList();
                        tables.Add(ViewModelWindowQueryAdvanced.HluDatasetStatic.incid);
                    }

                    // Split the string of query table names created by the
                    // user in the form into an array.
                    string[] fromTables = sqlFromTables.Split(',').Select(s => s.Trim(' ')).Distinct().ToArray();

                    // Select only the database tables that are in the query array.
                    List<DataTable> whereTables = tables.Where(t => fromTables.Contains(t.TableName)).ToList();

                    // Backup the current selection (filter).
                    DataTable incidSelectionBackup = _incidSelection;

                    // Replace any connection type specific qualifiers and delimiters.
                    string newWhereClause = null;
                    if (sqlWhereClause != null)
                        newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                    // create a selection DataTable of PK values of IncidTable
                    if (whereTables.Count > 0)
                    {
                        // Create a selection DataTable of PK values of IncidTable.
                        _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                        // Get a list of all the incids in the selection.
                        _incidsSelectedMap = _incidSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                        // Retrospectively set the where clause to match the list
                        // of selected incids (for possible use later).
                        _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);
                    }
                    else
                    {
                        // Clear the selection of incids.
                        _incidSelection = null;

                        // Clear the previous where clause (set when performing the
                        // original query builder or when reading the map selection)
                        // because otherwise it might be used in error later.
                        _incidSelectionWhereClause = null;
                    }

                    // If there are any records in the selection (and the tool is
                    // not currently in bulk update mode).
                    if (IsFiltered)
                    {
                        // Find the expected number of features to be selected in GIS.
                        _toidsSelectedDBCount = 0;
                        _fragsSelectedDBCount = 0;
                        //ExpectedSelectionFeatures(whereTables, sqlWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);
                        ExpectedSelectionFeatures(whereTables, newWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);

                        //---------------------------------------------------------------------
                        // CHANGED: CR12 (Select by attribute performance)
                        // Store the number of incids found in the database
                        _incidsSelectedDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;
                        //---------------------------------------------------------------------

                        ChangeCursor(Cursors.Wait, "Filtering ...");
                        // Select the required incid(s) in GIS.
                        if (PerformGisSelection(true, _fragsSelectedDBCount, _incidsSelectedDBCount))
                        {
                            //---------------------------------------------------------------------
                            // CHANGED: CR21 (Select current incid in map)
                            // Analyse the results, set the filter and reset the cursor AFTER
                            // returning from performing the GIS selection so that other calls
                            // to the PerformGisSelection method can control if/when these things
                            // are done.
                            //
                            // Analyse the results of the GIS selection by counting the number of
                            // incids, toids and fragments selected.
                            AnalyzeGisSelectionSet(true);

                            // Indicate the selection didn't come from the map.
                            _filterByMap = false;

                            // Set the filter back to the first incid.
                            //TODO: await call.
                            SetFilterAsync();

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);
                            //---------------------------------------------------------------------

                            // Check if the GIS and database are in sync.
                            if (CheckInSync("Selection", "Selected", "Not all selected"))
                            {
                                // Check if the counts returned are less than those expected.
                                if ((_toidsIncidGisCount < _toidsSelectedDBCount) ||
                                        (_fragsIncidGisCount < _fragsSelectedDBCount))
                                {
                                    MessageBox.Show("Not all selected features found in active layer.", "HLU: Selection",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                        }
                        else
                        {
                            //---------------------------------------------------------------------
                            // FIX: 110 Clear selection when not found in GIS.
                            //
                            // Restore the previous selection (filter).
                            //_incidSelection = incidSelectionBackup;

                            // Clear the selection (filter).
                            _incidSelection = null;

                            // Indicate the selection didn't come from the map.
                            _filterByMap = false;

                            // Set the filter back to the first incid.
                            //TODO: await call.
                            SetFilterAsync();
                            //---------------------------------------------------------------------

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);

                            // Warn the user that no records were found.
                            MessageBox.Show("No map features found in active layer.", "HLU: Apply Query",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        //---------------------------------------------------------------------
                        // FIX: 110 Clear selection when not found in the database.
                        //
                        // Restore the previous selection (filter).
                        //_incidSelection = incidSelectionBackup;

                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filterByMap = false;

                        // Set the filter back to the first incid.
                        //TODO: await call.
                        SetFilterAsync();
                        //---------------------------------------------------------------------

                        // Reset the cursor back to normal
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found
                        MessageBox.Show("No records found in database.", "HLU: Apply Query",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    _incidSelection = null;
                    ChangeCursor(Cursors.Arrow, null);
                    MessageBox.Show(ex.Message, "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { RefreshStatus(); }
            }
        }

        /// <summary>
        /// Opens the warning on gis selection window to prompt the user
        /// for confirmation before proceeding.
        /// </summary>
        /// <param name="selectByjoin">if set to <c>true</c> [select byjoin].</param>
        /// <param name="expectedNumFeatures">The expected number features.</param>
        /// <param name="expectedNumIncids">The expected number incids.</param>
        /// <returns></returns>
        /// <exception cref="Exception">No parent window loaded</exception>
        private bool ConfirmGISSelect(bool selectByjoin, int expectedNumFeatures, int expectedNumIncids)
        {
            //---------------------------------------------------------------------
            // CHANGED: CR12 (Select by attribute performance)
            // Warn the user either if the user option is set to
            // 'Always' or if a GIS table join will be used and
            // the user option is set to 'Joins'.
            if ((_warnBeforeGISSelect == 0) ||
                (selectByjoin && _warnBeforeGISSelect == 1))
            //---------------------------------------------------------------------
            {
                _windowWarnGISSelect = new()
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowWarnGISSelect.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // Create ViewModel to which main window binds
                _viewModelWinWarnGISSelect = new ViewModelWindowWarnOnGISSelect(
                    expectedNumFeatures, expectedNumIncids, expectedNumFeatures > -1 ? _gisLayerType : GeometryTypes.Unknown, selectByjoin);

                // When ViewModel asks to be closed, close window
                _viewModelWinWarnGISSelect.RequestClose +=
                    new ViewModelWindowWarnOnGISSelect.RequestCloseEventHandler(_viewModelWinWarnGISSelect_RequestClose);

                // Allow all controls in window to bind to ViewModel by setting DataContext
                _windowWarnGISSelect.DataContext = _viewModelWinWarnGISSelect;

                // Show the window
                _windowWarnGISSelect.ShowDialog();

                return IsFiltered;
            }
            else
            {
                // Return true if the user has not been warned.
                return true;
            }
        }

        /// <summary>
        /// Closes the warning gis on selection window.
        /// </summary>
        /// <param name="proceed">if set to <c>true</c> [proceed].</param>
        void _viewModelWinWarnGISSelect_RequestClose(bool proceed)
        {
            _viewModelWinWarnGISSelect.RequestClose -= _viewModelWinWarnGISSelect_RequestClose;
            _windowWarnGISSelect.Close();

            // Update the user warning variable
            _warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;

            // If the user doesn't wish to proceed then clear the
            // current incid filter.
            if (!proceed)
            {
                _incidSelectionWhereClause = null;
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);
            }
        }
        //---------------------------------------------------------------------

        #endregion

        #region Filter by Incid Command

        /// <summary>
        /// FilterByIncid command.
        /// </summary>
        public ICommand FilterByIncidCommand
        {
            get
            {
                if (_filterByIncidCommand == null)
                {
                    Action<object> filterByIncidAction = new(this.FilterByIncidClicked);
                    _filterByIncidCommand = new RelayCommand(filterByIncidAction, param => this.CanFilterByIncid);
                }
                return _filterByIncidCommand;
            }
        }

        /// <summary>
        /// Opens the by filter by incid window.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void FilterByIncidClicked(object param)
        {
            OpenQueryIncid();
        }

        /// <summary>
        /// Gets a value indicating whether the filter by incid command can
        /// be clicked.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by incid; otherwise, <c>false</c>.
        /// </value>
        private bool CanFilterByIncid
        {
            get { return _bulkUpdateMode == false && _osmmUpdateMode == false && IncidCurrentRow != null; }
        }

        /// <summary>
        /// Opens the query by incid window.
        /// </summary>
        /// <exception cref="Exception">No parent window loaded</exception>
        private void OpenQueryIncid()
        {
            try
            {
                _windowQueryIncid = new()
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowQueryIncid.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryIncid = new()
                {
                    DisplayName = "Filter By Incid"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryIncid.RequestClose +=
                    new ViewModelWindowQueryIncid.RequestCloseEventHandler(_viewModelWinQueryIncid_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryIncid.DataContext = _viewModelWinQueryIncid;

                // show window
                _windowQueryIncid.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Query by Incid", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Closes the query incid window and select the required incid.
        /// </summary>
        /// <param name="queryIncid">The query incid.</param>
        protected void _viewModelWinQueryIncid_RequestClose(String queryIncid)
        {
            _viewModelWinQueryIncid.RequestClose -= _viewModelWinQueryIncid_RequestClose;
            _windowQueryIncid.Close();

            if (!String.IsNullOrEmpty(queryIncid))
            {
                try
                {
                    ChangeCursor(Cursors.Wait, "Validating ...");

                    // Select only the incid database table to use in the query.
                    List<DataTable> whereTables = [];
                    whereTables.Add(IncidTable);

                    // Replace any connection type specific qualifiers and delimiters.
                    string newWhereClause = null;

                    // Ensure predicted count of toids/fragment selected works with
                    // any query.
                    string sqlWhereClause = String.Format("[incid].incid = '{0}'", queryIncid);

                    newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                    // Create a selection DataTable of PK values of IncidTable.
                    _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                    // Get a list of all the incids in the selection.
                    _incidsSelectedMap = _incidSelection.AsEnumerable()
                        .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                    // Retrospectively set the where clause to match the list
                    // of selected incids (for possible use later).
                    _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                        IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                    // Backup the current selection (filter).
                    DataTable incidSelectionBackup = _incidSelection;

                    // If there are any records in the selection (and the tool is
                    // not currently in bulk update mode).
                    if (IsFiltered)
                    {
                        // Find the expected number of features to be selected in GIS.
                        _toidsSelectedDBCount = 0;
                        _fragsSelectedDBCount = 0;
                        //ExpectedSelectionFeatures(whereTables, sqlWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);
                        ExpectedSelectionFeatures(whereTables, newWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);

                        //---------------------------------------------------------------------
                        // CHANGED: CR12 (Select by attribute performance)
                        // Store the number of incids found in the database
                        _incidsSelectedDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;
                        //---------------------------------------------------------------------

                        ChangeCursor(Cursors.Wait, "Filtering ...");
                        // Select the required incid(s) in GIS.
                        if (PerformGisSelection(true, _fragsSelectedDBCount, _incidsSelectedDBCount))
                        {
                            //---------------------------------------------------------------------
                            // CHANGED: CR21 (Select current incid in map)
                            // Analyse the results, set the filter and reset the cursor AFTER
                            // returning from performing the GIS selection so that other calls
                            // to the PerformGisSelection method can control if/when these things
                            // are done.
                            //
                            // Analyse the results of the GIS selection by counting the number of
                            // incids, toids and fragments selected.
                            AnalyzeGisSelectionSet(true);

                            // Indicate the selection didn't come from the map.
                            _filterByMap = false;

                            // Set the filter back to the first incid.
                            //TODO: await call.
                            SetFilterAsync();

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);
                            //---------------------------------------------------------------------

                            // Check if the GIS and database are in sync.
                            if ((_toidsIncidGisCount > _toidsIncidDbCount) ||
                               (_fragsIncidGisCount > _fragsIncidDbCount))
                            {
                                if (_fragsIncidGisCount == 1)
                                    MessageBox.Show("Selected feature not found in database.", "HLU: Selection",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                else
                                    MessageBox.Show("Not all selected features found in database.", "HLU: Selection",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            // Check if the counts returned are less than those expected.
                            else if ((_toidsIncidGisCount < _toidsSelectedDBCount) ||
                                    (_fragsIncidGisCount < _fragsSelectedDBCount))
                            {
                                MessageBox.Show("Not all selected features found in active layer.", "HLU: Selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            //---------------------------------------------------------------------
                            // FIX: 110 Clear selection when not found in GIS.
                            //
                            // Restore the previous selection (filter).
                            //_incidSelection = incidSelectionBackup;

                            // Clear the selection (filter).
                            _incidSelection = null;

                            // Indicate the selection didn't come from the map.
                            //TODO: await call.
                            _filterByMap = false;

                            // Set the filter back to the first incid.
                            //TODO: await call.
                            SetFilterAsync();
                            //---------------------------------------------------------------------

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);

                            // Warn the user that no records were found.
                            MessageBox.Show("Map feature not found in active layer.", "HLU: Apply Query",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        //---------------------------------------------------------------------
                        // FIX: 110 Clear selection when not found in the database.
                        //
                        // Restore the previous selection (filter).
                        //_incidSelection = incidSelectionBackup;

                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filterByMap = false;

                        // Set the filter back to the first incid.
                        //TODO: await call.
                        SetFilterAsync();
                        //---------------------------------------------------------------------

                        // Reset the cursor back to normal
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that the record was not found
                        MessageBox.Show("Record not found in database.", "HLU: Apply Query",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    _incidSelection = null;
                    ChangeCursor(Cursors.Arrow, null);
                    MessageBox.Show(ex.Message, "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { RefreshStatus(); }
            }
        }
        //---------------------------------------------------------------------

        #endregion

        #region Filter by OSMM Updates Command

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        // Open the OSMM Updates query window when in OSMM Update mode.
        //
        public void OpenWindowQueryOSMM(bool initialise)
        {
            if (initialise)
            {
                // Clear the selection (filter).
                _incidSelection = null;

                // Indicate the selection didn't come from the map.
                _filterByMap = false;

                // Indicate there are no more OSMM updates to review.
                if (_osmmBulkUpdateMode == false)
                    _osmmUpdatesEmpty = true;

                // Clear all the form fields (except the habitat class
                // and habitat type).
                //ClearForm();      // Already cleared

                // Clear the map selection.
                _gisApp.ClearMapSelection();

                // Reset the map counters
                _incidsSelectedMapCount = 0;
                _toidsSelectedMapCount = 0;
                _fragsSelectedMapCount = 0;

                // Refresh all the controls
                RefreshAll();

                DispatcherHelper.DoEvents();
            }

            try
            {
                _windowQueryOSMM = new WindowQueryOSMM
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowQueryOSMM.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryOSMM = new(HluDataset, _db, this)
                {
                    DisplayName = "OSMM Updates Filter"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryOSMM.RequestClose +=
                    new ViewModelWindowQueryOSMM.RequestCloseEventHandler(_viewModelWinQueryOSMM_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryOSMM.DataContext = _viewModelWinQueryOSMM;

                // show window
                _windowQueryOSMM.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Query by OSMM Updates", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        /// <summary>
        /// Process the sql when the window is closed.
        /// </summary>
        /// <param name="processFlag">The process flag value.</param>
        /// <param name="spatialFlag">The spatial flag value.</param>
        /// <param name="changeFlag">The change flag value.</param>
        /// <param name="status">The OSMM status value.</param>
        /// <param name="apply">Whether to apply (or cancel) the query.</param>
        protected void _viewModelWinQueryOSMM_RequestClose(string processFlag, string spatialFlag, string changeFlag, string status, bool apply)
        {
            // Close the window
            _viewModelWinQueryOSMM.RequestClose -= _viewModelWinQueryOSMM_RequestClose;
            _windowQueryOSMM.Close();

            if (apply == true)
            {

                if (_osmmBulkUpdateMode == true)
                {
                    // Set the default source details
                    IncidSourcesRows[0].source_id = (int)_addInSettings.BulkOSMMSourceId;
                    IncidSourcesRows[0].source_habitat_class = "N/A";
                    //_viewModelMain.IncidSourcesRows[0].source_habitat_type = "N/A";
                    //Date.VagueDateInstance defaultSourceDate = DefaultSourceDate(null, Settings.Default.BulkOSMMSourceId);
                    Date.VagueDateInstance defaultSourceDate = new();
                    IncidSourcesRows[0].source_date_start = defaultSourceDate.StartDate;
                    IncidSourcesRows[0].source_date_end = defaultSourceDate.EndDate;
                    IncidSourcesRows[0].source_date_type = defaultSourceDate.DateType;
                    IncidSourcesRows[0].source_boundary_importance = Settings.Default.SourceImportanceApply1;
                    IncidSourcesRows[0].source_habitat_importance = Settings.Default.SourceImportanceApply1;
                }

                // Apply the OSMM Updates filter
                if (processFlag != null || spatialFlag != null || changeFlag != null || status != null)
                    ApplyOSMMUpdatesFilter(processFlag, spatialFlag, changeFlag, status);
            }
        }

        /// <summary>
        /// Open the OSMM Updates advanced query window when in OSMM Update mode.
        /// </summary>
        /// <param name="initialise">if set to <c>true</c> [initialise].</param>
        /// <exception cref="Exception">No parent window loaded</exception>
        public void OpenWindowQueryOSMMAdvanced(bool initialise)
        {
            if (initialise)
            {
                // Clear the selection (filter).
                _incidSelection = null;

                // Indicate the selection didn't come from the map.
                _filterByMap = false;

                // Indicate there are no more OSMM updates to review.
                if (_osmmBulkUpdateMode == false)
                    _osmmUpdatesEmpty = true;

                // Clear all the form fields (except the habitat class
                // and habitat type).
                ClearForm();

                // Clear the map selection.
                _gisApp.ClearMapSelection();

                // Reset the map counters
                _incidsSelectedMapCount = 0;
                _toidsSelectedMapCount = 0;
                _fragsSelectedMapCount = 0;

                // Refresh all the controls
                RefreshAll();

                DispatcherHelper.DoEvents();
            }

            try
            {
                _windowQueryAdvanced = new()
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowQueryAdvanced.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryAdvanced = new(HluDataset, _db)
                {
                    DisplayName = "OSMM Updates Advanced Filter"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryAdvanced.RequestClose +=
                    new ViewModelWindowQueryAdvanced.RequestCloseEventHandler(_viewModelWinQueryOSMMAdvanced_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryAdvanced.DataContext = _viewModelWinQueryAdvanced;

                // show window
                _windowQueryAdvanced.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Query by OSMM Updates", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        /// <summary>
        /// Process the sql when the advanced query window is closed.
        /// </summary>
        /// <param name="sqlFromTables">The tables to query.</param>
        /// <param name="sqlWhereClause">The where clause to apply in the query.</param>
        protected void _viewModelWinQueryOSMMAdvanced_RequestClose(string sqlFromTables, string sqlWhereClause)
        {
            _viewModelWinQueryAdvanced.RequestClose -= _viewModelWinQueryOSMMAdvanced_RequestClose;
            _windowQueryAdvanced.Close();

            if ((sqlFromTables != null) && (sqlWhereClause != null))
            {

                //if (_osmmBulkUpdateMode == true)
                //{
                //    // Set the default source details
                //    IncidSourcesRows[0].source_id = Settings.Default.BulkOSMMSourceId;
                //    IncidSourcesRows[0].source_habitat_class = "N/A";
                //    //_viewModelMain.IncidSourcesRows[0].source_habitat_type = "N/A";
                //    //Date.VagueDateInstance defaultSourceDate = DefaultSourceDate(null, Settings.Default.BulkOSMMSourceId);
                //    Date.VagueDateInstance defaultSourceDate = new Date.VagueDateInstance();
                //    IncidSourcesRows[0].source_date_start = defaultSourceDate.StartDate;
                //    IncidSourcesRows[0].source_date_end = defaultSourceDate.EndDate;
                //    IncidSourcesRows[0].source_date_type = defaultSourceDate.DateType;
                //    IncidSourcesRows[0].source_boundary_importance = Settings.Default.SourceImportanceApply1;
                //    IncidSourcesRows[0].source_habitat_importance = Settings.Default.SourceImportanceApply1;
                //}

                try
                {
                    ChangeCursor(Cursors.Wait, "Validating ...");
                    DispatcherHelper.DoEvents();

                    // Get a list of all the possible query tables.
                    List<DataTable> tables = [];
                    if ((ViewModelWindowQueryAdvanced.HluDatasetStatic != null))
                    {
                        tables = ViewModelWindowQueryAdvanced.HluDatasetStatic.incid.ChildRelations
                            .Cast<DataRelation>().Select(r => r.ChildTable).ToList();
                        tables.Add(ViewModelWindowQueryAdvanced.HluDatasetStatic.incid);
                    }

                    // Split the string of query table names created by the
                    // user in the form into an array.
                    string[] fromTables = sqlFromTables.Split(',').Select(s => s.Trim(' ')).Distinct().ToArray();

                    // Include the incid_osmm_updates table to use in the query.
                    if (fromTables.Contains(IncidOSMMUpdatesTable.TableName) == false)
                        fromTables = fromTables.Concat([IncidOSMMUpdatesTable.TableName]).ToArray();

                    // Select only the database tables that are in the query array.
                    List<DataTable> whereTables = tables.Where(t => fromTables.Contains(t.TableName)).ToList();

                    // If a status is included in the SQL then also filter out pending
                    // and applied updates, otherwise filter out everything
                    // except proposed updates.
                    if (sqlWhereClause.Contains("[incid_osmm_updates].status") == true)
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status <> 0 AND [incid_osmm_updates].status <> -1", sqlWhereClause);
                    else
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status > 0", sqlWhereClause);

                    // create a selection DataTable of PK values of IncidTable
                    if (whereTables.Count != 0)
                    {

                        // Replace any connection type specific qualifiers and delimiters.
                        string newWhereClause = null;
                        if (sqlWhereClause != null)
                            newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                        // Store the where clause for updating the OSMM updates later.
                        _osmmUpdateWhereClause = null;

                        // Create a selection DataTable of PK values of IncidTable.
                        _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                        // Get a list of all the incids in the selection.
                        _incidsSelectedMap = _incidSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                        // Retrospectively set the where clause to match the list
                        // of selected incids (for possible use later).
                        _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                        // Backup the current selection (filter).
                        DataTable incidSelectionBackup = _incidSelection;

                        // If there are any records in the selection (and the tool is
                        // not currently in bulk update mode).
                        if (IsFiltered)
                        {
                            // Find the expected number of features to be selected in GIS.
                            _toidsSelectedDBCount = 0;
                            _fragsSelectedDBCount = 0;
                            //ExpectedSelectionFeatures(whereTables, sqlWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);
                            ExpectedSelectionFeatures(whereTables, newWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);

                            //---------------------------------------------------------------------
                            // CHANGED: CR12 (Select by attribute performance)
                            // Store the number of incids found in the database
                            _incidsSelectedDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;
                            //---------------------------------------------------------------------

                            ChangeCursor(Cursors.Wait, "Filtering ...");
                            // Select the required incid(s) in GIS.
                            if (PerformGisSelection(true, _fragsSelectedDBCount, _incidsSelectedDBCount))
                            {
                                //---------------------------------------------------------------------
                                // CHANGED: CR21 (Select current incid in map)
                                // Analyse the results, set the filter and reset the cursor AFTER
                                // returning from performing the GIS selection so that other calls
                                // to the PerformGisSelection method can control if/when these things
                                // are done.
                                //
                                // Analyse the results of the GIS selection by counting the number of
                                // incids, toids and fragments selected.
                                AnalyzeGisSelectionSet(true);

                                // Indicate the selection didn't come from the map.
                                _filterByMap = false;

                                if (_osmmBulkUpdateMode == false)
                                {
                                    // Indicate there are more OSMM updates to review.
                                    _osmmUpdatesEmpty = false;
                                    //---------------------------------------------------------------------
                                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                                    //
                                    OnPropertyChanged(nameof(CanOSMMAccept));
                                    OnPropertyChanged(nameof(CanOSMMSkip));
                                    //---------------------------------------------------------------------

                                    // Set the filter to the first incid.
                                    //TODO: await call.
                                    SetFilterAsync();

                                    // Check if the GIS and database are in sync.
                                    if (CheckInSync("Selection", "Selected", "Not all selected"))
                                    {
                                        // Check if the counts returned are less than those expected.
                                        if ((_toidsIncidGisCount < _toidsSelectedDBCount) ||
                                                (_fragsIncidGisCount < _fragsSelectedDBCount))
                                        {
                                            MessageBox.Show("Not all selected features found in active layer.", "HLU: Selection",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                        }
                                    }
                                }

                                // Refresh all the controls
                                RefreshAll();

                                // Reset the cursor back to normal.
                                ChangeCursor(Cursors.Arrow, null);
                                //---------------------------------------------------------------------
                            }
                            else
                            {
                                if (_osmmBulkUpdateMode == false)
                                {
                                    // Clear the selection (filter).
                                    _incidSelection = null;

                                    // Indicate the selection didn't come from the map.
                                    _filterByMap = false;

                                    // Indicate there are no more OSMM updates to review.
                                    if (_osmmBulkUpdateMode == false)
                                        _osmmUpdatesEmpty = true;

                                    // Clear all the form fields (except the habitat class
                                    // and habitat type).
                                    ClearForm();

                                    // Clear the map selection.
                                    _gisApp.ClearMapSelection();

                                    // Reset the map counters
                                    _incidsSelectedMapCount = 0;
                                    _toidsSelectedMapCount = 0;
                                    _fragsSelectedMapCount = 0;

                                    // Refresh all the controls
                                    RefreshAll();
                                }

                                // Reset the cursor back to normal.
                                ChangeCursor(Cursors.Arrow, null);

                                // Warn the user that no records were found.
                                MessageBox.Show("No map features found in active layer.", "HLU: Selection",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            if (_osmmBulkUpdateMode == false)
                            {
                                // Clear the selection (filter).
                                _incidSelection = null;

                                // Indicate the selection didn't come from the map.
                                _filterByMap = false;

                                // Indicate there are no more OSMM updates to review.
                                _osmmUpdatesEmpty = true;

                                // Clear all the form fields (except the habitat class
                                // and habitat type).
                                ClearForm();

                                // Clear the map selection.
                                _gisApp.ClearMapSelection();

                                // Reset the map counters
                                _incidsSelectedMapCount = 0;
                                _toidsSelectedMapCount = 0;
                                _fragsSelectedMapCount = 0;

                                // Refresh all the controls
                                RefreshAll();
                            }
                            else
                            {
                                // Restore the previous selection (filter).
                                _incidSelection = incidSelectionBackup;
                            }

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);

                            // Warn the user that no records were found.
                            MessageBox.Show("No records found in database.", "HLU: Selection",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _incidSelection = null;
                    ChangeCursor(Cursors.Arrow, null);
                MessageBox.Show(ex.Message, "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { RefreshStatus(); }
            }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Applies the OSMM updates filter.
        /// </summary>
        /// <param name="processFlag">The process flag.</param>
        /// <param name="spatialFlag">The spatial flag.</param>
        /// <param name="changeFlag">The change flag.</param>
        /// <param name="status">The status.</param>
        public void ApplyOSMMUpdatesFilter(string processFlag, string spatialFlag, string changeFlag, string status)
        {
            try
            {
                ChangeCursor(Cursors.Wait, "Validating ...");
                DispatcherHelper.DoEvents();

                // Select only the incid_osmm_updates database table to use in the query.
                List<DataTable> whereTables = [];
                whereTables.Add(IncidOSMMUpdatesTable);

                // Always filter out applied updates
                string sqlWhereClause;
                sqlWhereClause = "[incid_osmm_updates].status <> -1";

                // Add any other filter criteria.
                if (processFlag != null && processFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].process_flag = {1}", sqlWhereClause, processFlag);

                if (spatialFlag != null && spatialFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].spatial_flag = '{1}'", sqlWhereClause, spatialFlag);

                if (changeFlag != null && changeFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].change_flag = '{1}'", sqlWhereClause, changeFlag);

                if (status != null && status != _codeAnyRow)
                {
                    int newStatus = status switch
                    {
                        "Rejected" => -99,
                        "Ignored" => -2,
                        "Applied" => -1,
                        "Pending" => 0,
                        "Proposed" => 1,
                        _ => -999
                    };

                    if (newStatus == 1)
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status > 0", sqlWhereClause);
                    else
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status = {1}", sqlWhereClause, newStatus);
                }

                // Don't show pending or applied updates when no status filter is applied
                if (status == null || status == _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status <> 0  AND [incid_osmm_updates].status <> -1", sqlWhereClause);

                // Replace any connection type specific qualifiers and delimiters.
                string newWhereClause = null;
                newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                // Store the where clause for updating the OSMM updates later.
                _osmmUpdateWhereClause = newWhereClause;

                // Create a selection DataTable of PK values of IncidTable.
                _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                // Get a list of all the incids in the selection.
                _incidsSelectedMap = _incidSelection.AsEnumerable()
                    .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                // Retrospectively set the where clause to match the list
                // of selected incids (for possible use later).
                _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                    IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                // If there are any records in the selection (and the tool is
                // not currently in bulk update mode).
                if (IsFiltered)
                {
                    // Find the expected number of features to be selected in GIS.
                    _toidsSelectedDBCount = 0;
                    _fragsSelectedDBCount = 0;
                    //ExpectedSelectionFeatures(whereTables, sqlWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);
                    ExpectedSelectionFeatures(whereTables, newWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);

                    //---------------------------------------------------------------------
                    // CHANGED: CR12 (Select by attribute performance)
                    // Store the number of incids found in the database
                    _incidsSelectedDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;
                    //---------------------------------------------------------------------

                    ChangeCursor(Cursors.Wait, "Filtering ...");
                    // Select the required incid(s) in GIS.
                    if (PerformGisSelection(true, _fragsSelectedDBCount, _incidsSelectedDBCount))
                    {
                        //---------------------------------------------------------------------
                        // CHANGED: CR21 (Select current incid in map)
                        // Analyse the results, set the filter and reset the cursor AFTER
                        // returning from performing the GIS selection so that other calls
                        // to the PerformGisSelection method can control if/when these things
                        // are done.
                        //
                        // Analyse the results of the GIS selection by counting the number of
                        // incids, toids and fragments selected.
                        AnalyzeGisSelectionSet(true);

                        // Indicate the selection didn't come from the map.
                        _filterByMap = false;

                        if (_osmmBulkUpdateMode == false)
                        {
                            // Indicate there are more OSMM updates to review.
                            _osmmUpdatesEmpty = false;

                            // Set the filter to the first incid.
                            //TODO: await call.
                            SetFilterAsync();

                            //---------------------------------------------------------------------
                            // FIX: 103 Accept/Reject OSMM updates in edit mode.
                            //
                            OnPropertyChanged(nameof(CanOSMMAccept));
                            OnPropertyChanged(nameof(CanOSMMSkip));
                            //---------------------------------------------------------------------
                        }

                        // Refresh all the controls
                        RefreshAll();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);
                        //---------------------------------------------------------------------
                    }
                    else
                    {
                        if (_osmmBulkUpdateMode == false)
                        {
                            // Clear the selection (filter).
                            _incidSelection = null;

                            // Indicate the selection didn't come from the map.
                            _filterByMap = false;

                            // Indicate there are no more OSMM updates to review.
                            if (_osmmBulkUpdateMode == false)
                                _osmmUpdatesEmpty = true;

                            // Clear all the form fields (except the habitat class
                            // and habitat type).
                            ClearForm();

                            // Clear the map selection.
                            _gisApp.ClearMapSelection();

                            // Reset the map counters
                            _incidsSelectedMapCount = 0;
                            _toidsSelectedMapCount = 0;
                            _fragsSelectedMapCount = 0;

                            // Refresh all the controls
                            RefreshAll();
                        }

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found.
                        MessageBox.Show("No map features found in active layer.", "HLU: Apply Query",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    if (_osmmBulkUpdateMode == false)
                    {
                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filterByMap = false;

                        // Indicate there are no more OSMM updates to review.
                        _osmmUpdatesEmpty = true;

                        // Clear all the form fields (except the habitat class
                        // and habitat type).
                        ClearForm();

                        // Clear the map selection.
                        _gisApp.ClearMapSelection();

                        // Reset the map counters
                        _incidsSelectedMapCount = 0;
                        _toidsSelectedMapCount = 0;
                        _fragsSelectedMapCount = 0;

                        // Refresh all the controls
                        RefreshAll();
                    }
                    else
                    {
                        // Restore the previous selection (filter).
                        _incidSelection = incidSelectionBackup;
                    }

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow, null);

                    // Warn the user that no records were found.
                    MessageBox.Show("No records found in database.", "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);
                MessageBox.Show(ex.Message, "HLU: Apply Query",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { RefreshStatus(); }
        }

        /// <summary>
        /// Clears the current record for the form.
        /// </summary>
        public void ClearForm()
        {
            // Disable the History tab
            TabItemHistoryEnabled = false;

            // Clear the habitat fields
            _incidIhsHabitat = null;
            _incidPrimary = null;
            _incidNVCCodes = null;
            //_incidSecondarySummary = null;

            // Clear the input habitat class and type (the class will be reset
            // to the default class later).
            HabitatClass = null;
            //HabitatType = null;

            // Get a new incid row.
            IncidCurrentRow = HluDataset.incid.NewincidRow();

            // Get new mulitplex rows.
            IncidIhsMatrixRows = Array.Empty<HluDataSet.incid_ihs_matrixRow>()
                .Select(r => HluDataset.incid_ihs_matrix.Newincid_ihs_matrixRow()).ToArray();

            IncidIhsFormationRows = Array.Empty<HluDataSet.incid_ihs_formationRow>()
                .Select(r => HluDataset.incid_ihs_formation.Newincid_ihs_formationRow()).ToArray();

            IncidIhsManagementRows = Array.Empty<HluDataSet.incid_ihs_managementRow>()
                .Select(r => HluDataset.incid_ihs_management.Newincid_ihs_managementRow()).ToArray();

            IncidIhsComplexRows = Array.Empty<HluDataSet.incid_ihs_complexRow>()
                .Select(r => HluDataset.incid_ihs_complex.Newincid_ihs_complexRow()).ToArray();

            // Get new secondary rows.
            IncidSecondaryRows = Array.Empty<HluDataSet.incid_secondaryRow>()
                .Select(r => HluDataset.incid_secondary.Newincid_secondaryRow()).ToArray();

            // Clear the secondary habitats table and the secondary habitat
            // rows for the class
            _incidSecondaryHabitats = [];
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Clear the secondary groups list and disable the drop-down.
            _secondaryGroupsValid = null;
            OnPropertyChanged(nameof(SecondaryGroupCodes));
            OnPropertyChanged(nameof(SecondaryGroupEnabled));

            // Get a new condition row.
            IncidConditionRows = new HluDataSet.incid_conditionRow[1]
                .Select(r => HluDataset.incid_condition.Newincid_conditionRow()).ToArray();
            for (int i = 0; i < IncidConditionRows.Length; i++)
            {
                IncidConditionRows[i].incid_condition_id = i;
                IncidConditionRows[i].condition = null;
                IncidConditionRows[i].incid = RecIDs.CurrentIncid;
            }

            // Get a new BAP row and reset the auto and user collections.
            IncidBapRows = Array.Empty<HluDataSet.incid_bapRow>()
                .Select(r => HluDataset.incid_bap.Newincid_bapRow()).ToArray();
            IncidBapRowsAuto = [];
            IncidBapRowsUser = [];

            // Get new sources rows.
            IncidSourcesRows = new HluDataSet.incid_sourcesRow[3]
                .Select(r => HluDataset.incid_sources.Newincid_sourcesRow()).ToArray();
            for (int i = 0; i < IncidSourcesRows.Length; i++)
            {
                IncidSourcesRows[i].incid_source_id = i;
                IncidSourcesRows[i].source_id = Int32.MinValue;
                IncidSourcesRows[i].incid = RecIDs.CurrentIncid;
            }

            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            // Clear the OSMM Update fields
            _incidOSMMUpdatesOSMMXref = 0;
            _incidOSMMUpdatesProcessFlag = 0;
            _incidOSMMUpdatesSpatialFlag = null;
            _incidOSMMUpdatesChangeFlag = null;
            _incidOSMMUpdatesStatus = null;
            //---------------------------------------------------------------------


        }
        //---------------------------------------------------------------------

        #endregion

        #region Select On Map Command

        /// <summary>
        /// SelectOnMap command.
        /// </summary>
        public ICommand SelectOnMapCommand
        {
            get
            {
                if (_selectOnMapCommand == null)
                {
                    Action<object> selectOnMapAction = new(this.SelectOnMapClicked);
                    _selectOnMapCommand = new RelayCommand(selectOnMapAction, param => this.CanSelectOnMap);
                }
                return _selectOnMapCommand;
            }
        }

        /// <summary>
        /// Selects the current incid on the map.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void SelectOnMapClicked(object param)
        {
            // Set the status to processing and the cursor to wait.
            ChangeCursor(Cursors.Wait, "Selecting ...");

            SelectOnMap(false);

            // Count the number of toids and fragments for the current incid
            // selected in the GIS and in the database.
            CountToidFrags();

            // Refresh all the status type fields.
            RefreshStatus();

            // Reset the cursor back to normal
            ChangeCursor(Cursors.Arrow, null);

            // Check if the GIS and database are in sync.
            if (CheckInSync("Selection", "Incid", "Not all incid"))
            {
                // Check if the counts returned are less than those expected.
                if ((_toidsIncidGisCount < _toidsIncidDbCount) ||
                        (_fragsIncidGisCount < _fragsIncidDbCount))
                {
                    MessageBox.Show("Not all incid features found in active layer.", "HLU: Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private bool CanSelectOnMap
        {
            get { return _bulkUpdateMode == false && _osmmUpdateMode == false && IncidCurrentRow != null; }
        }

        //TODO: Add wait?
        /// <summary>
        /// Select current DB record on map when button pressed.
        /// </summary>
        public void SelectOnMap(bool updateIncidSelection)
        {
            if (IncidCurrentRow == null) return;

            //---------------------------------------------------------------------
            // CHANGED: CR21 (Select current incid in map)
            // Temporarily store the incid and GIS selections whilst
            // selecting the current incid in GIS so that the selections
            // can be restored again afterwards (so that the filter is
            // still active).
            //
            try
            {
                DataTable prevIncidSelection = NewIncidSelectionTable();
                DataTable prevGISSelection = NewGisSelectionTable();

                // Determine if a filter with more than one incid is currently active.
                bool multiIncidFilter = (IsFiltered && _incidSelection.Rows.Count > 1);

                // Save the current table of selected incids.
                prevIncidSelection = _incidSelection;

                // Save the current table of selected GIS features.
                prevGISSelection = _gisSelection;

                // Reset the table of selected incids.
                _incidSelection = NewIncidSelectionTable();

                // Set the table of selected incids to the current incid.
                DataRow selRow = _incidSelection.NewRow();
                foreach (DataColumn c in _incidSelection.Columns)
                    selRow[c] = IncidCurrentRow[c.ColumnName];
                _incidSelection.Rows.Add(selRow);

                // Select all the features for the current incid in GIS.
                PerformGisSelection(false, -1, -1);

                // If a multi-incid filter was previously active then restore it.
                if (multiIncidFilter)
                {
                    // Restore the previous table of selected incids.
                    _incidSelection = prevIncidSelection;

                    // Count the number of fragments previously selected for this incid.
                    int numFragsOld = 0;
                    if (prevGISSelection != null)
                    {
                        DataRow[] gisRows = prevGISSelection.AsEnumerable()
                            .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                        numFragsOld = gisRows.Length;
                    }

                    // Count the number of fragments now selected for this incid.
                    int numFragsNew = 0;
                    if (_gisSelection != null)
                    {
                        DataRow[] gisRows = _gisSelection.AsEnumerable()
                            .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                        numFragsNew = gisRows.Length;
                    }

                    // Check if the number of fragments now selected for this incid
                    // has changed.
                    if (numFragsNew == numFragsOld)
                    {
                        // If the same number of fragments for this incid has been
                        // selected then just restore the previous table.
                        _gisSelection = prevGISSelection;
                    }
                    else
                    {
                        // If the number of fragments selected has changed for this
                        // incid then add all the rows for all the other incids in
                        // the previous table of selected GIS features to the current
                        // table of selected GIS features (thereby replacing the previously
                        // selected features for the current incid with the new
                        // selection).
                        if (prevGISSelection != null)
                        {
                            selRow = _gisSelection.NewRow();
                            foreach (DataRow row in prevGISSelection.Rows)
                            {
                                if (row[HluDataset.incid.incidColumn.ColumnName].ToString() != _incidCurrentRow.incid)
                                    _gisSelection.ImportRow(row);
                            }
                        }
                    }

                    // Analyse the results of the GIS selection by counting
                    // the number of incids, toids and fragments selected.
                    AnalyzeGisSelectionSet(updateIncidSelection);

                }
                else
                {
                    // Restore the previous table of selected incids.
                    _incidSelection = prevIncidSelection;

                    // Restore the previous table of selected GIS features.
                    //_gisSelection = prevGISSelection;

                    // Analyse the results of the GIS selection by counting
                    // the number of incids, toids and fragments selected.
                    AnalyzeGisSelectionSet(false);

                    // Set the filter back to the first incid.
                    //SetFilter();
                }

                // Indicate the selection didn't come from the map.
                _filterByMap = false;

                // Zoom to the GIS selection if auto zoom is on.
                if (_gisSelection != null && _autoZoomSelection != 0)
                {
                    // Get the map distance units.
                    string distUnits = Settings.Default.MapDistanceUnits;

                    _gisApp.ZoomSelected(_minZoom, distUnits, _autoZoomSelection == 2);
                }

                // Warn the user that no features were found in GIS.
                if ((_gisSelection == null) || (_gisSelection.Rows.Count == 0))
                    MessageBox.Show("No features for incid found in active layer.", "HLU: Selection",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
            }
            //---------------------------------------------------------------------

        }

        #endregion

        #region Read Map Selection Command

        /// <summary>
        /// ReadMapSelectionAsync command.
        /// </summary>
        public ICommand ReadMapSelectionCommand
        {
            get
            {
                if (_readMapSelectionCommand == null)
                {
                    Action<object> readMapSelectionAction = new(this.ReadMapSelectionClicked);
                    _readMapSelectionCommand = new RelayCommand(readMapSelectionAction, param => this.CanReadMapSelection);
                }
                return _readMapSelectionCommand;
            }
        }

        private void ReadMapSelectionClicked(object param)
        {
            // Get the GIS layer selection and warn the user if no
            // features are found (don't wait).
            ReadMapSelectionAsync(true);
        }

        internal bool CanReadMapSelection
        {
                //---------------------------------------------------------------------
                // FIX: 101 Enable get map selection when in OSMM update mode.
                //--------------------------------------------------------------
                //get { return _bulkUpdateMode == false && _osmmUpdateMode == false && HaveGisApp; }
                get { return _bulkUpdateMode == false; }
                //---------------------------------------------------------------------
        }

        internal async Task ReadMapSelectionAsync(bool showMessage)
        {
            try
            {
                // Check there are no outstanding edits.
                MessageBoxResult userResponse = CheckDirty();

                // Process based on the response ...
                // Yes = move to the new incid
                // No = move to the new incid
                // Cancel = don't move to the new incid
                switch (userResponse)
                {
                    case MessageBoxResult.Yes:
                        //if (!_viewModelUpd.Update()) return;
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }

                ChangeCursor(Cursors.Wait, "Filtering ...");

                DispatcherHelper.DoEvents();

                // Initialise the GIS selection table.
                _gisSelection = NewGisSelectionTable();

                // Read which features are selected in GIS (passing it a new
                // GIS selection table so that it knows the columns to return.
                _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);

                // Count how many incids, toids and fragments are selected in GIS
                _incidSelectionWhereClause = null;
                AnalyzeGisSelectionSet(true);

                // Update the number of features found in the database.
                _toidsSelectedDBCount = 0;
                _fragsSelectedDBCount = 0;
                ExpectedSelectionFeatures(_incidSelectionWhereClause, ref _toidsSelectedDBCount, ref _fragsSelectedDBCount);

                // Store the number of incids found in the database
                _incidsSelectedDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                // Indicate the selection came from the map.
                if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                    _filterByMap = true;

                if (_gisSelection.Rows.Count > 0)
                {
                    //---------------------------------------------------------------------
                    // Prevent OSMM updates being actioned too quickly.
                    // FIX: 103 Accept/Reject OSMM updates in edit mode.
                    if (_osmmBulkUpdateMode == false && _osmmUpdateMode == true)
                    {
                        // Indicate there are more OSMM updates to review.
                        _osmmUpdatesEmpty = false;
                        OnPropertyChanged(nameof(CanOSMMAccept));
                        OnPropertyChanged(nameof(CanOSMMSkip));
                    }
                    //---------------------------------------------------------------------

                    // Set flag so that the user isn't prompted to save
                    // any pending edits (again).
                    _readingMap = true;

                    // Set the filter to the first incid.
                    await SetFilterAsync();

                    // Reset the flag again.
                    _readingMap = false;

                    // Perform physical split if the conditions are met.
                    if (_autoSplit && (_gisSelection != null) && (_gisSelection.Rows.Count > 1) && (_incidsSelectedMapCount == 1) &&
                        (_toidsSelectedMapCount == 1) && (_fragsSelectedMapCount == 1))
                    {
                        if (IsAuthorisedUser)
                        {
                            if (!CanPhysicallySplit)
                            {
                                _windowCompSplit = new()
                                {
                                    //DONE: App.Current.MainWindow
                                    //_windowCompSplit.Owner = App.Current.MainWindow;
                                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                                };
                                _vmCompSplit = new ViewModelCompletePhysicalSplit(_reason, _process, _reasonCodes, _processCodes);
                                _vmCompSplit.RequestClose += new ViewModelCompletePhysicalSplit.RequestCloseEventHandler(vmCompSplit_RequestClose);
                                _windowCompSplit.DataContext = _vmCompSplit;
                                _windowCompSplit.ShowDialog();
                            }
                            if (CanPhysicallySplit)
                            {
                                ViewModelWindowMainSplit vmSplit = new(this);

                                if (await vmSplit.PhysicalSplitAsync())
                                    NotifySplitMerge("Physical split completed.");
                            }
                            else
                            {
                                MessageBox.Show("Could not complete physical split.\nPlease invoke the Split command before altering the map selection.",
                                    "HLU: Physical Split", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Could not complete physical split because you are not an authorized user.\n" +
                                "Please undo your map changes to prevent map and database going out of sync.",
                                "HLU: Physical Split", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }
                    }
                    else
                    {
                        ChangeCursor(Cursors.Arrow, null);

                        // Check if the GIS and database are in sync.
                        CheckInSync("Selection", "Map");
                    }
                }
                else
                {
                    // Reset the incid and map selections and move
                    // to the first incid in the database.
                    await ClearFilterAsync(true);

                    //---------------------------------------------------------------------
                    // FIX: 107 Reset filter when no map features selected.
                    //
                    // Indicate the selection didn't come from the map (but only after
                    // the filter has been cleared and the first incid selected so that
                    // the map doesn't auto zoom to the incid).
                    _filterByMap = false;
                    //---------------------------------------------------------------------

                    ChangeCursor(Cursors.Arrow, null);

                    if (showMessage) MessageBox.Show("No map features selected in active layer.", "HLU: Selection",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ChangeCursor(Cursors.Arrow, null);
            }
        }

        //TODO: Remove window and stop split from happening without reason/process set.
        void vmCompSplit_RequestClose(string reason, string process)
        {
            _vmCompSplit.RequestClose -= vmCompSplit_RequestClose;
            _windowCompSplit.Close();
            if (!String.IsNullOrEmpty(reason))
            {
                _reason = reason;
                OnPropertyChanged(nameof(Reason));
            }
            if (!String.IsNullOrEmpty(process))
            {
                _process = process;
                OnPropertyChanged(nameof(Process));
            }
        }

        #endregion

        #region Priority Habitats Command
        //---------------------------------------------------------------------
        // CHANGED: CR54 Add pop-out windows to show/edit priority habitats
        // New pop-out windows to view and edit priority and potential
        // priority habitats more clearly.
        //
        /// <summary>
        /// EditPriorityHabitats command.
        /// </summary>
        public ICommand EditPriorityHabitatsCommand
        {
            get
            {
                if (_editPriorityHabitatsCommand == null)
                {
                    Action<object> editPriorityHabitatsAction = new(this.EditPriorityHabitatsClicked);
                    _editPriorityHabitatsCommand = new RelayCommand(editPriorityHabitatsAction, param => this.CanEditPriorityHabitats);
                }
                return _editPriorityHabitatsCommand;
            }
        }

        private void EditPriorityHabitatsClicked(object param)
        {
            try
            {
                _windowEditPriorityHabitats = new()
                {
                    //DONE: App.Current.MainWindow
                    //_windowEditPriorityHabitats.Owner = App.Current.MainWindow;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinEditPriorityHabitats = new(this, IncidBapHabitatsAuto)
                {
                    DisplayName = "Priority Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinEditPriorityHabitats.RequestClose += new ViewModelWindowEditPriorityHabitats
                    .RequestCloseEventHandler(_viewModelWinEditPriorityHabitats_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowEditPriorityHabitats.DataContext = _viewModelWinEditPriorityHabitats;

                // show window
                _windowEditPriorityHabitats.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Priority Habitats", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        protected void _viewModelWinEditPriorityHabitats_RequestClose(ObservableCollection<BapEnvironment> incidBapHabitatsAuto)
        {
            _viewModelWinEditPriorityHabitats.RequestClose -= _viewModelWinEditPriorityHabitats_RequestClose;
            _windowEditPriorityHabitats.Close();

            if (incidBapHabitatsAuto != null)
            {
                IncidBapHabitatsAuto = incidBapHabitatsAuto;

                // Check if there are any errors in the primary BAP records to see
                // if the Priority tab label should be flagged as also in error.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                    if (countInvalid > 0)
                        AddErrorList(ref _priorityErrors, "BapAuto");
                    else
                        DelErrorList(ref _priorityErrors, "BapAuto");
                }

                OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                OnPropertyChanged(nameof(PriorityTabLabel));
            }
        }

        public bool CanEditPriorityHabitats
        {
            get { return _bulkUpdateMode == false && _osmmUpdateMode == false && BapHabitatsAutoEnabled; }
        }

        #endregion

        #region Potential Priority Habitats Command

        /// <summary>
        /// EditPotentialHabitats command.
        /// </summary>
        public ICommand EditPotentialHabitatsCommand
        {
            get
            {
                if (_editPotentialHabitatsCommand == null)
                {
                    Action<object> editPotentialHabitatsAction = new(this.EditPotentialHabitatsClicked);
                    _editPotentialHabitatsCommand = new RelayCommand(editPotentialHabitatsAction, param => this.CanEditPotentialHabitats);
                }
                return _editPotentialHabitatsCommand;
            }
        }

        private void EditPotentialHabitatsClicked(object param)
        {
            try
            {
                _windowEditPotentialHabitats = new()
                {
                    //DONE: App.Current.MainWindow
                    //_windowEditPotentialHabitats.Owner = App.Current.MainWindow;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinEditPotentialHabitats = new(this, IncidBapHabitatsUser)
                {
                    DisplayName = "Potential Priority Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinEditPotentialHabitats.RequestClose += new ViewModelWindowEditPotentialHabitats
                    .RequestCloseEventHandler(_viewModelWinEditPotentialHabitats_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowEditPotentialHabitats.DataContext = _viewModelWinEditPotentialHabitats;

                // show window
                _windowEditPotentialHabitats.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Potential Priority Habitats", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        protected void _viewModelWinEditPotentialHabitats_RequestClose(ObservableCollection<BapEnvironment> incidBapHabitatsUser)
        {
            _viewModelWinEditPotentialHabitats.RequestClose -= _viewModelWinEditPotentialHabitats_RequestClose;
            _windowEditPotentialHabitats.Close();

            if (incidBapHabitatsUser != null)
            {
                IncidBapHabitatsUser = incidBapHabitatsUser;

                // Check if there are any errors in the optional BAP records to see
                // if the Priority tab label should be flagged as also in error.
                if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
                {
                    int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                    if (countInvalid > 0)
                        AddErrorList(ref _priorityErrors, "BapUser");
                    else
                        DelErrorList(ref _priorityErrors, "BapUser");
                }

                OnPropertyChanged(nameof(IncidBapHabitatsUser));
                OnPropertyChanged(nameof(PriorityTabLabel));
            }
        }

        public bool CanEditPotentialHabitats
        {
            get { return _bulkUpdateMode == false && _osmmUpdateMode == false && BapHabitatsUserEnabled; }
        }
        //---------------------------------------------------------------------

        #endregion

        #region Add Secondary Habitat Command

        /// <summary>
        /// AddSecondaryHabitat command.
        /// </summary>
        public ICommand AddSecondaryHabitatCommand
        {
            get
            {
                if (_addSecondaryHabitatCommand == null)
                {
                    Action<object> addSecondaryHabitatAction = new(this.AddSecondaryHabitatClicked);
                    _addSecondaryHabitatCommand = new RelayCommand(addSecondaryHabitatAction, param => this.CanAddSecondaryHabitat);
                }
                return _addSecondaryHabitatCommand;
            }
        }

        public bool CanAddSecondaryHabitat
        {
            get
            {
                // Check not in OSMM update mode and GIS present and primary
                // code and secondary habitat group and code have been set.
                return (_osmmUpdateMode == false
                    && _incidPrimary != null && _secondaryGroup != null && _secondaryHabitat != null);
            }
        }

        /// <summary>
        /// Add a secondary habitat to the table.
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void AddSecondaryHabitatClicked(object param)
        {
            try
            {
                // Double check secondary habitat group and code have been set.
                if (_secondaryGroup != null && _secondaryHabitat != null)
                {
                    string secondaryGroup = _secondaryGroup;
                    if (secondaryGroup.StartsWith("<All"))
                    {
                        // Lookup the secondary group from the secondary code
                        IEnumerable<string> q = null;
                        q = (from s in SecondaryHabitatCodesAll
                             where s.code == _secondaryHabitat
                             select s.code_group);
                        if ((q != null) && (q.Any())) secondaryGroup = q.First();
                    }

                    // Add secondary habitat to table if it isn't already in the table
                    if (SecondaryHabitat.SecondaryHabitatList == null ||
                        !SecondaryHabitat.SecondaryHabitatList.Any(sh => sh.secondary_habitat == _secondaryHabitat))
                        AddSecondaryHabitat(false, -1, Incid, _secondaryHabitat, secondaryGroup);

                    // Refresh secondary table and summary.
                    RefreshSecondaryHabitats();
                    OnPropertyChanged(nameof(IncidSecondarySummary));

                    // Refresh the BAP habitat environments (in case secondary codes
                    // are, or should be, reflected).
                    GetBapEnvironments();
                    OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                    OnPropertyChanged(nameof(IncidBapHabitatsUser));
                    OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
                    OnPropertyChanged(nameof(BapHabitatsUserEnabled));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Add Secondary Habitat", MessageBoxButton.OK, MessageBoxImage.Error);
                //throw;
            }
        }

        /// <summary>
        /// AddSecondaryHabitatList command.
        /// </summary>
        public ICommand AddSecondaryHabitatListCommand
        {
            get
            {
                if (_addSecondaryHabitatListCommand == null)
                {
                    Action<object> addSecondaryHabitatListAction = new(this.AddSecondaryHabitatListClicked);
                    _addSecondaryHabitatListCommand = new RelayCommand(addSecondaryHabitatListAction, param => this.CanAddSecondaryHabitatList);
                }
                return _addSecondaryHabitatListCommand;
            }
        }

        public bool CanAddSecondaryHabitatList
        {
            get
            {
                // Check not in OSMM update mode and GIS present and primary
                // code and secondary habitat group and code have been set.
                return (_osmmUpdateMode == false
                    && _incidPrimary != null);
            }
        }

        /// <summary>
        /// Opens the query secondaries window.
        /// </summary>
        /// <exception cref="Exception">No parent window loaded</exception>
        private void AddSecondaryHabitatListClicked(object param)
        {
            try
            {
                _windowQuerySecondaries = new()
                {
                    //TODO: App.GetActiveWindow
                    //if ((_windowQuerySecondaries.Owner = App.GetActiveWindow()) == null)
                    //    throw (new Exception("No parent window loaded"));
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // create ViewModel to which main window binds
                _viewModelWinQuerySecondaries = new()
                {
                    DisplayName = "Add Secondary Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQuerySecondaries.RequestClose +=
                    new ViewModelWindowQuerySecondaries.RequestCloseEventHandler(_viewModelWinQuerySecondaries_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQuerySecondaries.DataContext = _viewModelWinQuerySecondaries;

                // show window
                _windowQuerySecondaries.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Add Secondary Habitats", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Closes the query add secondaries window and adds the list of
        /// secondary habitats to the tble.
        /// </summary>
        /// <param name="querySecondaries">The list of secondaries to add.</param>
        protected void _viewModelWinQuerySecondaries_RequestClose(String querySecondaries)
        {
            _viewModelWinQuerySecondaries.RequestClose -= _viewModelWinQuerySecondaries_RequestClose;
            _windowQuerySecondaries.Close();

            if (!String.IsNullOrEmpty(querySecondaries))
            {
                try
                {
                    bool addedCodes = false;
                    List<string> errorCodes = [];

                    // Split the list by spaces, commas or points
                    string pattern = @"\s|\.|\,";
                    Regex rgx = new(pattern);

                    // Process each secondary habitat code
                    string[] secondaryHabitats = rgx.Split(querySecondaries);
                    for (int i = 0; i < secondaryHabitats.Length; i++)
                    {
                        string secondaryHabitat = secondaryHabitats[i];
                        if (secondaryHabitat != null)
                        {
                            // Lookup the secondary group for the secondary code
                            IEnumerable<string> q = null;
                            q = (from s in SecondaryHabitatCodesAll
                                    where s.code == secondaryHabitat
                                    select s.code_group);

                            // If the secondary group has been found
                            string secondaryGroup = null;
                            if ((q != null) && (q.Any()))
                            {
                                secondaryGroup = q.First();

                                // Add secondary habitat if it isn't already in the table
                                if (SecondaryHabitat.SecondaryHabitatList == null ||
                                    !SecondaryHabitat.SecondaryHabitatList.Any(sh => sh.secondary_habitat == secondaryHabitat))
                                {
                                    // Add secondary habitat to table if it isn't already in the table
                                    bool err;
                                    err = AddSecondaryHabitat(false, -1, Incid, secondaryHabitat, secondaryGroup);
                                    if (err == true)
                                        errorCodes.Add(secondaryHabitat);

                                    addedCodes = true;
                                }
                                else
                                    errorCodes.Add(secondaryHabitat);
                            }
                            else
                                errorCodes.Add(secondaryHabitat);
                        }

                        // If any valid codes were entered and were added to the table
                        if (addedCodes == true)
                        {
                            // Refresh secondary table and summary.
                            RefreshSecondaryHabitats();
                            OnPropertyChanged(nameof(IncidSecondarySummary));

                            // Refresh the BAP habitat environments (in case secondary codes
                            // are, or should be, reflected).
                            GetBapEnvironments();
                            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                            OnPropertyChanged(nameof(IncidBapHabitatsUser));
                            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
                            OnPropertyChanged(nameof(BapHabitatsUserEnabled));
                        }

                        // If any codes were invalid then tell the user
                        if (errorCodes != null && errorCodes.Count > 0)
                        {
                            // Sort the distinct secondary codes in error numerically
                            errorCodes = errorCodes.Distinct().OrderBy(e => e.PadLeft(5, '0')).ToList();
                            // Message the user, depending on if there is one or more
                            if (errorCodes.Count == 1)
                                MessageBox.Show("Code '" +
                                    errorCodes.FirstOrDefault() + "' is a duplicate or unknown and has not been added.",
                                    "HLU: Add Secondary Habitats",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            else
                                MessageBox.Show("Codes '" +
                                    String.Join(", ", errorCodes.Take(errorCodes.Count - 1)) + " and " + errorCodes.Last() + "' are duplicates or unknown and have not been added.",
                                    "HLU: Add Secondary Habitats",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ChangeCursor(Cursors.Arrow, null);
                    MessageBox.Show(ex.Message, "HLU: Add Secondary Habitats", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Select All On Map Command

        // Enable all the incids in the current filter to be selected
        // in GIS.
        //
        /// <summary>
        /// SelectAllOnMap command.
        /// </summary>
        public ICommand SelectAllOnMapCommand
        {
            get
            {
                if (_selectAllOnMapCommand == null)
                {
                    Action<object> selectAllOnMapAction = new(this.SelectAllOnMapClicked);
                    _selectAllOnMapCommand = new RelayCommand(selectAllOnMapAction, param => this.CanSelectOnMap);
                }
                return _selectAllOnMapCommand;
            }
        }

        private async void SelectAllOnMapClicked(object param)
        {
            // Select all the incids in the active filter in GIS.
            await SelectAllOnMap();
        }

        /// <summary>
        /// Select all the incids in the active filter in GIS.
        /// </summary>
        public async Task SelectAllOnMap()
        {
            // If there are any records in the selection (and the tool is
            // not currently in bulk update mode).
            if (IsFiltered)
            {
                try
                {
                    // Set the status to processing and the cursor to wait.
                    ChangeCursor(Cursors.Wait, "Selecting ...");

                    // Backup the current selection (filter).
                    DataTable incidSelectionBackup = _incidSelection;

                    // Build a where clause list for the incids to be selected.
                    List<List<SqlFilterCondition>> whereClause = [ScratchDb.GisWhereClause(_incidSelection, null, false)];

                    // Find the expected number of features to be selected in GIS.
                    int expectedNumToids = -1;
                    int expectedNumFeatures = -1;
                    ExpectedSelectionFeatures(whereClause, ref expectedNumToids, ref expectedNumFeatures);

                    // Find the expected number of incids to be selected in GIS.
                    int expectedNumIncids = _incidSelection.Rows.Count;

                    // Select the required incid(s) in GIS.
                    if (PerformGisSelection(true, expectedNumFeatures, expectedNumIncids))
                    {
                        // Analyse the results of the GIS selection by counting the number of
                        // incids, toids and fragments selected.
                        AnalyzeGisSelectionSet(false);

                        // Indicate the selection came from the map.
                        if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                            _filterByMap = true;

                        // Set the filter back to the first incid.
                        await SetFilterAsync();

                        // Zoom to the GIS selection if auto zoom is on.
                        if (_gisSelection != null && _autoZoomSelection != 0)
                        {
                            // Get the map distance units.
                            string distUnits = Settings.Default.MapDistanceUnits;

                            _gisApp.ZoomSelected(_minZoom, distUnits, _autoZoomSelection == 2);
                        }

                        // Warn the user that no records were found.
                        if ((_gisSelection == null) || (_gisSelection.Rows.Count == 0))
                            MessageBox.Show("No incid features found in active layer.", "HLU: Selection",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                        {
                            // Check if the GIS and database are in sync.
                            if (CheckInSync("Selection", "Incid", "Not all incid"))
                            {
                                // Check if the counts returned are less than those expected.
                                if ((_toidsIncidGisCount < expectedNumToids) ||
                                        (_fragsIncidGisCount < expectedNumFeatures))
                                {
                                    MessageBox.Show("Not all incid features found in active layer.", "HLU: Selection",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Restore the previous selection (filter).
                        _incidSelection = incidSelectionBackup;
                    }
                }
                catch (Exception ex)
                {
                    _incidSelection = null;
                    MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Make sure the cursor is always reset.
                finally
                {
                    // Reset the cursor back to normal
                    ChangeCursor(Cursors.Arrow, null);
                }
                //RefreshStatus();
            }
        }

        #endregion

        #region Clear Filter Command

        /// <summary>
        /// ClearFilter command.
        /// </summary>
        public ICommand ClearFilterCommand
        {
            get
            {
                if (_clearFilterCommand == null)
                {
                    Action<object> qryBuilderAction = new(this.ClearFilterClicked);
                    _clearFilterCommand = new RelayCommand(qryBuilderAction, param => this.CanClearFilter);
                }
                return _clearFilterCommand;
            }
        }

        private void ClearFilterClicked(object param)
        {
            // Reset the incid and map selections and move
            // to the first incid in the database (don't wait).
            ClearFilterAsync(true);
        }

        public bool CanClearFilter
        {
            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Don't allow filter to be cleared when in OSMM Update mode or
            // OSMM Bulk Update mode.
            //
            get
            {
                return IsFiltered == true &&
                    _osmmUpdateMode == false &&
                    _osmmBulkUpdateMode == false;
            }
            //---------------------------------------------------------------------
        }

        /// <summary>
        /// Clears any active incid filter and optionally moves to the first incid in the index.
        /// </summary>
        /// <param name="resetRowIndex">If set to <c>true</c> the first incid in the index is loaded.</param>
        internal async Task ClearFilterAsync(bool resetRowIndex)
        {
            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Reset the OSMM Updates filter when in OSMM Update mode.
            //
            if (_osmmUpdateMode == true)
                ApplyOSMMUpdatesFilter(null, null, null, null);
            else if (_osmmBulkUpdateMode == true)
                ApplyOSMMUpdatesFilter(null, null, null, "Pending");
            else
            //---------------------------------------------------------------------
            {
                //if (IsFiltered)
                //{
                _incidSelection = null;
                _incidSelectionWhereClause = null;
                _gisSelection = null;
                _incidsSelectedDBCount = 0;
                _toidsSelectedDBCount = 0;
                _fragsSelectedDBCount = 0;
                _incidsSelectedMapCount = 0;
                _toidsSelectedMapCount = 0;
                _fragsSelectedMapCount = 0;
                _incidPageRowNoMax = -1;

                //---------------------------------------------------------------------
                // CHANGED: CR10 (Attribute updates for incid subsets)
                // Only move to the first incid in the index if required, to save
                // changing the index here and then again immediately after from
                // the calling method.
                if (resetRowIndex)
                {
                    //---------------------------------------------------------------------
                    // CHANGED: CR22 (Record selectors)
                    // Show the wait cursor and processing message in the status area
                    // whilst moving to the new Incid.
                    //ChangeCursor(Cursors.Wait, "Processing ...");

                    _incidCurrentRowIndex = 1;
                    //IncidCurrentRowIndex = 1;

                    //ChangeCursor(Cursors.Arrow, null);
                    //---------------------------------------------------------------------
                }
                //---------------------------------------------------------------------
                //}

                //---------------------------------------------------------------------
                // FIX: 107 Reset filter when no map features selected.
                //
                // Suggest the selection came from the map so that
                // the map doesn't auto zoom to the first incid.
                _filterByMap = true;
                //---------------------------------------------------------------------

                // Re-retrieve the current record (which includes counting the number of
                // toids and fragments for the current incid selected in the GIS and
                // in the database).
                if (resetRowIndex)
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex);
                else
                    // Count the number of toids and fragments for the current incid
                    // selected in the GIS and in the database.
                    CountToidFrags();

                // Refresh all the status type fields.
                RefreshStatus();

                //---------------------------------------------------------------------
                // FIX: 107 Reset filter when no map features selected.
                //
                // Indicate the selection didn't come from the map.
                _filterByMap = false;
                //---------------------------------------------------------------------
            }
        }

        #endregion

        #region Select Helpers

        /// <summary>
        /// Count how many incids, toids and fragments are selected in GIS.
        /// </summary>
        private void AnalyzeGisSelectionSet(bool updateIncidSelection)
        {
            _incidsSelectedMapCount = 0;
            _toidsSelectedMapCount = 0;
            _fragsSelectedMapCount = 0;
            if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
            {
                switch (_gisSelection.Columns.Count)
                {
                    case 3:
                        // Count the number of fragments selected in GIS.
                        _fragsSelectedMap = from r in _gisSelection.AsEnumerable()
                                            group r by new
                                            {
                                                incid = r.Field<string>(0),
                                                toid = r.Field<string>(1),
                                                fragment = r.Field<string>(2)
                                            }
                                                into g
                                                select g.Key.fragment;
                        _fragsSelectedMapCount = _fragsSelectedMap.Count();
                        goto case 2;
                    case 2:
                        // Count the number of toids selected in GIS.
                        _toidsSelectedMap = _gisSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_gisIDColumns[1].ColumnName)).Select(g => g.Key);
                        _toidsSelectedMapCount = _toidsSelectedMap.Count();
                        goto case 1;
                    case 1:
                        // Order the incids selected in the GIS so that the filter
                        // is sorted in incid order.
                        _incidsSelectedMap = _gisSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_gisIDColumns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                        // Count the number of incids selected in GIS.
                        _incidsSelectedMapCount = _incidsSelectedMap.Count();
                        break;
                }

                // Update the database Incid selection only if required.
                if ((updateIncidSelection) && (_incidsSelectedMapCount > 0))
                {
                    // Set the Incid selection where clause to match the list of
                    // selected incids (for possible use later).
                    if (_incidSelectionWhereClause == null)
                        _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                    // Update the database Incid selection to the Incids selected in the map.
                    GisToDbSelection();
                }
            }
            else
            {
                if (updateIncidSelection)
                {
                    _incidSelection = null;
                    _incidSelectionWhereClause = null;

                    //---------------------------------------------------------------------
                    // FIX: 107 Reset filter when no map features selected.
                    // 
                    _incidPageRowNoMax = -1;
                    //---------------------------------------------------------------------
                }
            }
        }

        /// <summary>
        /// Set the database Incid selection based on the Incids selected in the map.
        /// </summary>
        private void GisToDbSelection()
        {
            _incidSelection = NewIncidSelectionTable();
            foreach (string s in _incidsSelectedMap)
                _incidSelection.Rows.Add([s]);
        }

        private DataTable NewIncidSelectionTable()
        {
            DataTable outTable = new();
            outTable.Columns.Add(new DataColumn(IncidTable.incidColumn.ColumnName, IncidTable.incidColumn.DataType));
            outTable.DefaultView.Sort = IncidTable.incidColumn.ColumnName;
            return outTable;
        }

        /// <summary>
        /// Initialise the GIS selection table.
        /// </summary>
        /// <returns></returns>
        private DataTable NewGisSelectionTable()
        {
            DataTable outTable = new();
            foreach (DataColumn c in _gisIDColumns)
                outTable.Columns.Add(new DataColumn(c.ColumnName, c.DataType));
            return outTable;
        }

        //---------------------------------------------------------------------
        // CHANGED: CR5 (Select by attributes interface)
        // Calculate the expected number of GIS features to be selected
        // when using the original interface.
        //
        /// <summary>
        /// Calculates the expected number of GIS features to be selected
        /// by the sql query based upon a list of conditions.
        /// </summary>
        /// <param name="whereClause">The list of where clause conditions.</param>
        /// <returns>An integer of the number of GIS features to be selected.</returns>
        private void ExpectedSelectionFeatures(List<List<SqlFilterCondition>> whereClause, ref int numToids, ref int numFragments)
        {
            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                (whereClause != null) && (whereClause.Count > 0))
            {
                try
                {
                    HluDataSet.incid_mm_polygonsDataTable t = new();
                    DataTable[] selTables = new DataTable[] { t }.ToArray();

                    IEnumerable<DataTable> queryTables = whereClause.SelectMany(cond => cond.Select(c => c.Table)).Distinct();
                    //DataTable[] selTables = new DataTable[] { t }.Union(queryTables).ToArray();

                    var fromTables = queryTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = selTables.Concat(fromTables).ToArray();

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    // If there is only one long list then chunk
                    // it up into smaller lists.
                    if (whereClause.Count == 1)
                    {
                        try
                        {
                            List<SqlFilterCondition> whereCond = [];
                            whereCond = whereClause[0];
                            whereClause = whereCond.ChunkClause(IncidPageSize).ToList();
                        }
                        catch { }
                    }

                    numToids = 0;
                    numFragments = 0;
                    for (int i = 0; i < whereClause.Count; i++)
                    {
                        // If the where conditions are going to be appended
                        // to some join conditions then change the boolean
                        // operator before the first where condition to "AND"
                        // and wrap the where conditions in an extra set of
                        // parentheses.
                        if (joinCond.Any())
                        {
                            List<SqlFilterCondition> whereCond = [];
                            whereCond = whereClause[i];

                            SqlFilterCondition cond = new();
                            cond = whereCond[0];
                            cond.BooleanOperator = "AND";
                            cond.OpenParentheses = "((";

                            cond = whereCond[whereCond.Count - 1];
                            cond.CloseParentheses = "))";
                        }

                        numToids += _db.SqlCount(selTables, String.Format("Distinct {0}", _hluDS.incid_mm_polygons.toidColumn.ColumnName), joinCond.Concat(whereClause[i]).ToList());
                        numFragments += _db.SqlCount(selTables, "*", joinCond.Concat(whereClause[i]).ToList());
                    }
                }
                catch { }
            }
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Calculates the expected number of GIS features to be selected
        /// by the sql query based upon a list of data tables and a sql
        /// where clause.
        /// </summary>
        /// <param name="sqlFromTables">The list of data tables.</param>
        /// <param name="sqlWhereClause">The where clause string.</param>
        /// <returns>An integer of the number of GIS features to be selected.</returns>
        private void ExpectedSelectionFeatures(List<DataTable> sqlFromTables, string sqlWhereClause, ref int numToids, ref int numFragments)
        {
            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                sqlFromTables.Count != 0)
            {
                try
                {
                    HluDataSet.incid_mm_polygonsDataTable t = new();
                    DataTable[] selTables = new DataTable[] { t }.ToArray();

                    var fromTables = sqlFromTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = selTables.Concat(fromTables).ToArray();

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    numToids = _db.SqlCount(whereTables, String.Format("Distinct {0}", _hluDS.incid_mm_polygons.toidColumn.ColumnName), joinCond.ToList(), sqlWhereClause);
                    numFragments = _db.SqlCount(whereTables, "*", joinCond.ToList(), sqlWhereClause);

                    // Create a selection DataTable of PK values of IncidMMPolygons.
                    _incidMMPolygonSelection = _db.SqlSelect(true, false, _hluDS.incid_mm_polygons.PrimaryKey, whereTables.ToList(), joinCond.ToList(), sqlWhereClause);
                    //_incidMMPolygonSelection = _db.SqlSelect(true, false, IncidMMPolygonsTable.PrimaryKey, sqlFromTables, joinCond.ToList(), sqlWhereClause);
                    numFragments = _incidMMPolygonSelection.Rows.Count;

                    //// Change "*" to distinct concatenation of incid, toid and toid fragments
                    //numFragments = _db.SqlCount(whereTables, String.Format("Distinct Convert(varchar, {0}.{1}) + Convert(varchar, {0}.{2}) + Convert(varchar, {0}.{3})",
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.TableName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidfragidColumn.ColumnName)),
                    //    joinCond.ToList(), sqlWhereClause);
                }
                catch { }
            }
        }

        //TODO: Add wait?
        //---------------------------------------------------------------------
        // CHANGED: CR21 (Select current incid in map)
        // No longer set the filter or reset the cursor AFTER performing
        // the GIS selection so that methods that call this method
        // can control if/when these things are done.
        //
        private bool PerformGisSelection(bool confirmSelect, int expectedNumFeatures, int expectedNumIncids)
        {
            if (_gisApp != null)
            {
                //ChangeCursor(Cursors.Wait, "Processing ...");

                // Build a where clause list for the incids to be selected.
                List<SqlFilterCondition> whereClause = [];
                whereClause = ScratchDb.GisWhereClause(_incidSelection, _gisApp, false);

                //---------------------------------------------------------------------
                // CHANGED: CR12 (Select by attribute performance)
                // Calculate the length of the SQL statement to be sent to GIS.
                int sqlLen = _gisApp.SqlLength(_gisIDColumns, whereClause);

                // Check if the length exceeds the maximum for the GIS application.
                bool selectByJoin = (sqlLen > _gisApp.MaxSqlLength);

                // If the length exceeds the maximum for the GIS application then
                // perform the selection using a join.
                if (selectByJoin)
                {
                    if ((!confirmSelect) || (ConfirmGISSelect(true, expectedNumFeatures, expectedNumIncids)))
                    {
                        // Save the incids to the selected to a temporary database
                        ScratchDb.WriteSelectionScratchTable(_gisIDColumns, _incidSelection);
                        DispatcherHelper.DoEvents();

                        // Select all features for incid selection in active layer.
                        _gisSelection = _gisApp.SqlSelect(ScratchDb.ScratchMdbPath,
                            ScratchDb.ScratchSelectionTable, _gisIDColumns);

                        // Check if any features found when applying filter.
                        if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                            return true;
                        else
                            return false;
                    }
                }
                // Otherwise, perform the selection using a SQL query in GIS.
                else
                {
                    if ((!confirmSelect) || (ConfirmGISSelect(false, expectedNumFeatures, expectedNumIncids)))
                    {
                        DispatcherHelper.DoEvents();

                        // Select all features for incid selection in active layer.
                        _gisSelection = _gisApp.SqlSelect(true, false, _gisIDColumns,
                            whereClause);

                        // Check if any features found when applying filter.
                        if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                            return true;
                        else
                            return false;
                    }
                }
                //---------------------------------------------------------------------
            }

            // The selection didn't happen.
            return false;
        }
        //---------------------------------------------------------------------

        private async Task SetFilterAsync()
        {
            try
            {
                if (IsFiltered && (((_incidsSelectedMapCount > 0) || (_gisApp == null)) || _osmmUpdateMode == true))
                    // If currently splitting a feature then go to the last incid
                    // in the filter (which will be the new incid).
                    if (_splitting)
                    {
                        await MoveIncidCurrentRowIndexAsync(IsFiltered ? _incidSelection.Rows.Count : _incidRowCount);
                    }
                    else
                    {
                        await MoveIncidCurrentRowIndexAsync(1);
                    }
            }
            finally
            {
                // Not needed as counted after moving incid
                // Count the number of toids and fragments for the current incid
                // selected in the GIS and in the database.
                //CountToidFrags();

                // Not needed as refreshed after moving incid
                // Refresh all the status type fields.
                //RefreshStatus();
            }
        }

        #endregion

        #region Switch GIS Layer

        private void UpdateComboBoxEnabledState()
        {
            if (_bulkUpdateMode == false && _osmmUpdateMode == false)
            {
                // Can switch if there is more than one map layer.
                if (_gisApp.HluLayerCount > 1)
                {
                    // Enable the combo box.
                    ActiveLayerComboBox.GetInstance()?.UpdateState(true);

                    FrameworkApplication.State.Activate("HLUTool_ActiveLayerComboBox");
                }
            }
            else
            {
                // Disable the combo box.
                ActiveLayerComboBox.GetInstance()?.UpdateState(false);

                FrameworkApplication.State.Deactivate("HLUTool_ActiveLayerComboBox");
            }
        }

        /// <summary>
        /// Gets the name of the active layer to display in the status bar.
        /// </summary>
        /// <value>
        /// The name of the layer.
        /// </value>
        public string ActiveLayerName
        {
            get
            {
                // If no HLU layer has been identified yet (GIS is still loading) then
                // don't return the layer name
                if (_gisApp.CurrentHluLayer == null)
                    return String.Empty;
                else
                {
                    // Return the layer name.
                    return _gisApp.CurrentHluLayer.LayerName;
                }
                //---------------------------------------------------------------------
            }
        }

        #endregion

            #region Data Tables

        public HluDataSet.incidDataTable IncidTable
        {
            get
            {
                //TODO: Is this every true?
                // Load the data table if not already loaded.
                if (HluDataset.incid.IsInitialized && (HluDataset.incid.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incidTableAdapter == null)
                        _hluTableAdapterMgr.incidTableAdapter =
                            new HluTableAdapter<HluDataSet.incidDataTable, HluDataSet.incidRow>(_db);

                    //TODO: Commented out until it's determined this if clause is ever true.
                    // Go to the first incid in the table.
                    //MoveIncidCurrentRowIndexAsync(1);
                }

                return _hluDS.incid;
            }
        }

        public HluDataSet.incid_mm_polygonsDataTable IncidMMPolygonsTable
        {
            get
            {
                if (HluDataset.incid_mm_polygons.IsInitialized && (HluDataset.incid_mm_polygons.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_mm_polygonsTableAdapter == null)
                        _hluTableAdapterMgr.incid_mm_polygonsTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>(_db);
                }
                return _hluDS.incid_mm_polygons;
            }
        }

        public HluDataSet.incid_ihs_matrixDataTable IncidIhsMatrixTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_matrix.IsInitialized && (HluDataset.incid_ihs_matrix.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_ihs_matrixTableAdapter == null)
                        _hluTableAdapterMgr.incid_ihs_matrixTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_ihs_matrixDataTable, HluDataSet.incid_ihs_matrixRow>(_db);
                }

                return _hluDS.incid_ihs_matrix;
            }
        }

        public HluDataSet.incid_ihs_formationDataTable IncidIhsFormationTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_formation.IsInitialized && (HluDataset.incid_ihs_formation.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_ihs_formationTableAdapter == null)
                        _hluTableAdapterMgr.incid_ihs_formationTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_ihs_formationDataTable, HluDataSet.incid_ihs_formationRow>(_db);
                }

                return _hluDS.incid_ihs_formation;
            }
        }

        public HluDataSet.incid_ihs_managementDataTable IncidIhsManagementTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_management.IsInitialized && (HluDataset.incid_ihs_management.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_ihs_managementTableAdapter == null)
                        _hluTableAdapterMgr.incid_ihs_managementTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_ihs_managementDataTable, HluDataSet.incid_ihs_managementRow>(_db);
                }

                return _hluDS.incid_ihs_management;
            }
        }

        public HluDataSet.incid_ihs_complexDataTable IncidIhsComplexTable
        {
            get
            {
                // Load the lookup table if not already loaded.
                if (HluDataset.incid_ihs_complex.IsInitialized && (HluDataset.incid_ihs_complex.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_ihs_complexTableAdapter == null)
                        _hluTableAdapterMgr.incid_ihs_complexTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_ihs_complexDataTable, HluDataSet.incid_ihs_complexRow>(_db);
                }

                return _hluDS.incid_ihs_complex;
            }
        }

        public HluDataSet.incid_bapDataTable IncidBapTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_bap.IsInitialized && (HluDataset.incid_bap.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_bapTableAdapter == null)
                        _hluTableAdapterMgr.incid_bapTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_bapDataTable, HluDataSet.incid_bapRow>(_db);
                }

                return _hluDS.incid_bap;
            }
        }

        public HluDataSet.incid_sourcesDataTable IncidSourcesTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_sources.IsInitialized && (HluDataset.incid_sources.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_sourcesTableAdapter == null)
                        _hluTableAdapterMgr.incid_sourcesTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_sourcesDataTable, HluDataSet.incid_sourcesRow>(_db);
                }

                return _hluDS.incid_sources;
            }
        }

        public HluDataSet.incid_secondaryDataTable IncidSecondaryTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_secondary.IsInitialized && (HluDataset.incid_secondary.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_secondaryTableAdapter == null)
                        _hluTableAdapterMgr.incid_secondaryTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_secondaryDataTable, HluDataSet.incid_secondaryRow>(_db);
                }

                return _hluDS.incid_secondary;
            }
        }

        public HluDataSet.incid_conditionDataTable IncidConditionTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_condition.IsInitialized && (HluDataset.incid_condition.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_conditionTableAdapter == null)
                        _hluTableAdapterMgr.incid_conditionTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_conditionDataTable, HluDataSet.incid_conditionRow>(_db);
                }

                return _hluDS.incid_condition;
            }
        }

        public HluDataSet.incid_osmm_updatesDataTable IncidOSMMUpdatesTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_osmm_updates.IsInitialized && (HluDataset.incid_osmm_updates.Rows.Count == 0))
                {
                    if (_hluTableAdapterMgr.incid_osmm_updatesTableAdapter == null)
                        _hluTableAdapterMgr.incid_osmm_updatesTableAdapter =
                            new HluTableAdapter<HluDataSet.incid_osmm_updatesDataTable, HluDataSet.incid_osmm_updatesRow>(_db);
                }

                return _hluDS.incid_osmm_updates;
            }
        }

        #endregion

        #region Data Rows

        public bool IsFiltered
        {
            get
            {
                return (_bulkUpdateMode != true || (_bulkUpdateMode == true && _osmmBulkUpdateMode == true))
                    && _incidSelection != null
                    && _incidSelection.Rows.Count > 0;
            }
        }

        /// <summary>
        /// Counts the rows in the Incid table.
        /// </summary>
        /// <param name="recount">if set to <c>true</c> [recount].</param>
        /// <returns></returns>
        public int IncidRowCount(bool recount)
        {
            if (recount || (_incidRowCount <= 0))
            {
                try
                {
                    _incidRowCount = (int)_db.ExecuteScalar(String.Format(
                        "SELECT COUNT(*) FROM {0}", _db.QualifyTableName(_hluDS.incid.TableName)),
                        _db.Connection.ConnectionTimeout, CommandType.Text);
                    RefreshStatus();
                }
                catch { return -1; }
            }
            return _incidRowCount;
        }

        public string StatusIncid
        {
            get
            {
                if (OSMMUpdateMode == true && !IsFiltered)
                    return null;

                if (IsFiltered)
                {
                    //---------------------------------------------------------------------
                    // CHANGED: CR22 (Record selectors)
                    // Include the total toid and fragment counts for the current Incid
                    // in the status area in addition to the currently select toid and
                    // fragment counts.
                    //
                    if (_osmmUpdateMode == true)
                        return String.Format(" of {0}* [{1}:{2}]", _incidSelection.Rows.Count.ToString("N0"),
                            _toidsIncidDbCount.ToString(),
                            _fragsIncidDbCount.ToString());
                    else if (_osmmBulkUpdateMode == true)
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _incidSelection.Rows.Count.ToString("N0"),
                            _toidsSelectedMapCount.ToString(),
                            _fragsSelectedMapCount.ToString());
                    else
                        return String.Format(" of {0}* [{1}:{2} of {3}:{4}]", _incidSelection.Rows.Count.ToString("N0"),
                            _toidsIncidGisCount.ToString(),
                            _fragsIncidGisCount.ToString(),
                            _toidsIncidDbCount.ToString(),
                            _fragsIncidDbCount.ToString());
                    //---------------------------------------------------------------------
                }
                else if (_bulkUpdateMode == true)
                {
                    if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0))
                    {
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _incidsSelectedMapCount.ToString("N0"),
                            _toidsSelectedMapCount.ToString(),
                            _fragsSelectedMapCount.ToString());
                    }
                    else
                    {
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _incidsSelectedMapCount.ToString("N0"),
                            _toidsSelectedMapCount.ToString(),
                            _fragsSelectedMapCount.ToString());
                    }
                }
                else
                {
                    // Include the total toid and fragment counts for the current Incid
                    // in the status area, and the currently select toid and fragment
                    // counts, when auto selecting features on change of incid.
                    //
                    if (_osmmUpdateMode == true)
                        return String.Format(" of {0}* [{1}:{2}]", _incidRowCount.ToString("N0"),
                            _toidsIncidDbCount.ToString(),
                            _fragsIncidDbCount.ToString());
                    else
                        return String.Format(" of {0} [{1}:{2} of {3}:{4}]", _incidRowCount.ToString("N0"),
                            _toidsIncidGisCount.ToString(),
                            _fragsIncidGisCount.ToString(),
                            _toidsIncidDbCount.ToString(),
                            _fragsIncidDbCount.ToString());
                }
            }
        }

        public string StatusIncidToolTip { get { return IsFiltered ? "Double click to clear filter" : null; } }

        public string StatusBar
        {
            get { return _windowCursor == Cursors.Wait ? _processingMsg : String.Empty; }
        }

        public int NumIncidSelectedDB
        {
            get { return _incidsSelectedDBCount; }
            set { _incidsSelectedDBCount = value; }
        }

        public int NumToidSelectedDB
        {
            get { return _toidsSelectedDBCount; }
            set { _toidsSelectedDBCount = value; }
        }

        public int NumFragmentsSelectedDB
        {
            get { return _fragsSelectedDBCount; }
            set { _fragsSelectedDBCount = value; }
        }

        public int NumIncidSelectedMap
        {
            get { return _incidsSelectedMapCount; }
            set { }
        }

        public int NumToidSelectedMap
        {
            get { return _toidsSelectedMapCount; }
            set { }
        }

        public int NumFragmentsSelectedMap
        {
            get { return _fragsSelectedMapCount; }
            set { }
        }

        public HluDataSet.incidRow IncidCurrentRow
        {
            get { return _incidCurrentRow; }
            set { _incidCurrentRow = value; }
        }

        /// <summary>
        /// Gets or sets the index of the incid current row.
        /// </summary>
        /// <value>
        /// The index of the incid current row.
        /// </value>
        public int IncidCurrentRowIndex
        {
            get { return _incidCurrentRowIndex; }
            //set { _incidCurrentRowIndex = value; }
            set
            {
                // Move to the required incid current row (don't wait).
                MoveIncidCurrentRowIndexAsync(value);
            }
        }

        /// <summary>
        /// Gets or sets the index of the incid current row.
        /// </summary>
        /// <value>
        /// The index of the incid current row.
        /// </value>
        public async Task MoveIncidCurrentRowIndexAsync(int value)
        {
            MessageBoxResult userResponse = MessageBoxResult.No;
            // Check there are no outstanding edits (unless this has
            // already been checked before reading the map selection).
            if (!_readingMap)
                userResponse = CheckDirty();

            // Process based on the response ...
            // Yes = move to the new incid
            // No = move to the new incid
            // Cancel = don't move to the new incid
            switch (userResponse)
            {
                case MessageBoxResult.Yes:
                    break;
                case MessageBoxResult.No:
                    //---------------------------------------------------------------------
                    // CHANGED: CR49 Process proposed OSMM Updates
                    // Clear the form and warn the user when there are no more records
                    // when in OSMM Update mode.
                    //
                    if (_osmmUpdateMode == true && ((value > 0) &&
                        (IsFiltered && ((_incidSelection == null) || (value > _incidSelection.Rows.Count)))))
                    {
                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filterByMap = false;

                        // Indicate there are no more OSMM updates to review.
                        _osmmUpdatesEmpty = true;

                        // Clear all the form fields (except the habitat class
                        // and habitat type).
                        ClearForm();

                        // Clear the map selection.
                        _gisApp.ClearMapSelection();

                        // Reset the map counters
                        _incidsSelectedMapCount = 0;
                        _toidsSelectedMapCount = 0;
                        _fragsSelectedMapCount = 0;

                        // Refresh all the controls
                        RefreshAll();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no more records were found.
                        MessageBox.Show("No more records found.", "HLU: OSMM Updates",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        break;
                    }
                    //---------------------------------------------------------------------

                    if (_bulkUpdateMode != false || ((value > 0) &&
                        (IsFiltered && ((_incidSelection == null) || (value <= _incidSelection.Rows.Count))) ||
                        (!IsFiltered && ((_incidSelection == null) || (value <= _incidRowCount)))))
                    {
                        _incidCurrentRowIndex = value;
                    }

                    // Move to the new incid
                    await NewIncidCurrentRowAsync();

                    break;
                case MessageBoxResult.Cancel:
                    break;
            }
        }

        // Check if the record has changed and if it hasn't ask the user
        // if they still want to update the record (to create new history).
        //
        /// <summary>
        /// If no changes have been made reset the changed flag and then
        /// check if the user still wants to save the record.
        /// </summary>
        /// <returns>The user's response to save or not save the record.</returns>
        private MessageBoxResult CheckClean()
        {
            MessageBoxResult userResponse = MessageBoxResult.No;

            if (_editMode && (_bulkUpdateMode == false))
            {
                userResponse = MessageBoxResult.Yes;
                if (!IsDirty)
                {
                    userResponse = _saving ? MessageBox.Show("The current record has not been changed.\n" +
                        "Would you still like to save the record?", "HLU: Save",
                        MessageBoxButton.YesNoCancel, MessageBoxImage.Question) : MessageBoxResult.Yes;
                }
            }

            return userResponse;
        }

        /// <summary>
        /// Check there are any outstanding edits for the current incid.
        /// </summary>
        /// <returns>The user's response to save or not save the record.</returns>
        private MessageBoxResult CheckDirty()
        {
            MessageBoxResult userResponse = MessageBoxResult.No;

            //---------------------------------------------------------------------
            // CHANGED: CR49 Process proposed OSMM Updates
            // Don't check for edits when in OSMM Update mode (because the data
            // can't be edited by the user).
            //
            if (_editMode
                && (_splitting == false)
                && (_bulkUpdateMode == false)
                && (_osmmUpdateMode == false)
                && IsDirty)
            //---------------------------------------------------------------------
            {
                if (CanUpdate)
                {
                    userResponse = MessageBox.Show("The current record has been changed." +
                        "\n\nWould you like to leave this record discarding your changes?",
                        "HLU: Selection", MessageBoxButton.YesNo, MessageBoxImage.Exclamation,
                        MessageBoxResult.Yes);
                    if (userResponse == MessageBoxResult.Yes)
                        userResponse = MessageBoxResult.No;
                    else
                        userResponse = MessageBoxResult.Cancel;
                }
                else
                {
                    userResponse = MessageBox.Show("The current record has been changed, " +
                        "but it cannot be saved at this time because it is in error." +
                        "\n\nWould you like to leave this record discarding your changes?",
                        "HLU: Selection", MessageBoxButton.YesNo, MessageBoxImage.Exclamation,
                        MessageBoxResult.No);
                    if (userResponse == MessageBoxResult.Yes)
                        userResponse = MessageBoxResult.No;
                    else
                        userResponse = MessageBoxResult.Cancel;
                }

                // Restore the current row if the user doesn't want to save.
                if (userResponse == MessageBoxResult.No) RestoreIncidCurrentRow();
            }

            return userResponse;
        }

        public bool IsDirty
        {
            get
            {
                if (_saved)
                {
                    _saved = false;
                    return false;
                }

                // Return true if a field in any of the tables has changed.
                return IsDirtyIncid() || IsDirtyIncidSecondary() || IsDirtyIncidCondition() || IsDirtyIncidBap() ||
                    IsDirtyIncidSources();
            }
        }

        #endregion

        #region Data Row Helpers

        /// <summary>
        /// Initiates all the necessary actions when moving to another incid row.
        /// </summary>
        private async Task NewIncidCurrentRowAsync()
        {
            //TODO: Check if the slection is already being read so it
            // doesn't repeat itself.
            // Re-check GIS selection in case it has changed.
            if (_gisApp != null)
            {
                // Initialise the GIS selection table.
                _gisSelection = NewGisSelectionTable();

                // Recheck the selected features in GIS (passing a new GIS
                // selection table so that it knows the columns to return.
                _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);

                _incidSelectionWhereClause = null;

                AnalyzeGisSelectionSet(false);
            }

            bool canMove = false;
            if (!IsFiltered)
            {
                //TODO: Bug here sometimes.
                int newRowIndex = SeekIncid(_incidCurrentRowIndex);
                if ((canMove = newRowIndex != -1))
                    _incidCurrentRow = _hluDS.incid[newRowIndex];
            }
            else
            {
                if ((canMove = (_incidCurrentRowIndex != -1) &&
                    (_incidCurrentRowIndex <= _incidSelection.Rows.Count)))
                    _incidCurrentRow = await SeekIncidFiltered(_incidCurrentRowIndex);
            }

            if (canMove)
            {
                // Clone the current row to use to check for changes later
                CloneIncidCurrentRow();

                _incidArea = -1;
                _incidLength = -1;
                // Flag that the current record has not been changed yet so that the
                // apply button does not appear.
                Changed = false;

                // Clear the habitat type.
                HabitatType = null;
                OnPropertyChanged(nameof(HabitatType));

                // Get the incid table values
                IncidCurrentRowDerivedValuesRetrieve();
                OnPropertyChanged(nameof(IncidPrimary));

                // Get the incid child rows
                GetIncidChildRows(IncidCurrentRow);

                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // If there are any OSMM Updates for this incid then store the values.
                if (_incidOSMMUpdatesRows.Length > 0)
                {
                    _incidOSMMUpdatesOSMMXref = _incidOSMMUpdatesRows[0].osmm_xref_id;
                    _incidOSMMUpdatesProcessFlag = _incidOSMMUpdatesRows[0].process_flag;
                    _incidOSMMUpdatesSpatialFlag = _incidOSMMUpdatesRows[0].Isspatial_flagNull() ? null : _incidOSMMUpdatesRows[0].spatial_flag;
                    _incidOSMMUpdatesChangeFlag = _incidOSMMUpdatesRows[0].Ischange_flagNull() ? null : _incidOSMMUpdatesRows[0].change_flag;
                    _incidOSMMUpdatesStatus = _incidOSMMUpdatesRows[0].status;
                }
                else
                {
                    _incidOSMMUpdatesOSMMXref = 0;
                    _incidOSMMUpdatesProcessFlag = 0;
                    _incidOSMMUpdatesSpatialFlag = null;
                    _incidOSMMUpdatesChangeFlag = null;
                    _incidOSMMUpdatesStatus = null;
                }
                //---------------------------------------------------------------------

                // Enable auto select of features on change of incid.
                if (_gisApp != null && _autoSelectOnGis && _bulkUpdateMode == false && !_filterByMap)
                {
                    // Select the current DB record on the Map.
                    SelectOnMap(false);
                }

                // Count the number of toids and fragments for the current incid
                // selected in the GIS and in the database.
                CountToidFrags();

                OnPropertyChanged(nameof(IncidCurrentRowIndex));
                OnPropertyChanged(nameof(OSMMIncidCurrentRowIndex));
                OnPropertyChanged(nameof(IncidCurrentRow));

                // Refresh all statuses, headers adnd fields
                RefreshStatus();
                RefreshHeader();
                RefreshOSMMUpdate();
                RefreshHabitatTab();
                RefreshIHSTab();
                RefreshPriorityTab();
                RefreshDetailsTab();
                RefreshSource1();
                RefreshSource2();
                RefreshSource3();
                RefreshHistory();
            }
            CheckEditingControlState();
        }

        /// <summary>
        /// Count the number of toids and fragments for the current incid
        /// selected in the GIS and in the database.
        /// </summary>
        public void CountToidFrags()
        {
            //---------------------------------------------------------------------
            // CHANGED: CR10 (Attribute updates for incid subsets)
            // Count the number of toids and fragments for this incid selected
            // in the GIS. They are counted here, once when the incid changes,
            // instead of in StatusIncid() which is constantly being called.
            _toidsIncidGisCount = 0;
            _fragsIncidGisCount = 0;
            if (_gisSelection != null)
            {
                DataRow[] gisRows = _gisSelection.AsEnumerable()
                    .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                _toidsIncidGisCount = gisRows.GroupBy(r => r[HluDataset.incid_mm_polygons.toidColumn.ColumnName]).Count();
                _fragsIncidGisCount = gisRows.Length;
            }
            //---------------------------------------------------------------------

            //---------------------------------------------------------------------
            // CHANGED: CR22 (Record selectors)
            // Count the total number of toids and fragments in the database
            // for this incid so that they can be included in the status area.
            _fragsIncidDbCount = 0;
            _toidsIncidDbCount = 0;

            // Count the total number of fragments in the database for
            // this incid.
            _fragsIncidDbCount = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}",
                _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                _db.QuoteValue(_incidCurrentRow.incid)),
                _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of toids in the database for
            // this incid.
            _toidsIncidDbCount = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM (SELECT DISTINCT {0} FROM {1} WHERE {2} = {3}) AS T",
                _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                _db.QuoteValue(_incidCurrentRow.incid)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            //}
            //---------------------------------------------------------------------

        }

        /// <summary>
        /// Count the number of toids and fragments for all incid
        /// selected in the GIS and in the database.
        /// </summary>
        /// <param name="physicalSplit">if set to <c>true</c> [physical split].</param>
        /// <returns></returns>
        public bool CountSelectedToidFrags(bool physicalSplit)
        {
            foreach (DataRow row in _incidSelection.Rows)
            {
                string incid = row[HluDataset.incid.incidColumn.ColumnName].ToString();

                // Count the number of toids and fragments for all incids in
                // the selection in the GIS.
                int toidsIncidSelectionGisCount = 0;
                int fragsIncidSelectionGisCount = 0;
                if (_gisSelection != null)
                {
                    DataRow[] gisRows = _gisSelection.AsEnumerable()
                        .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(incid)).ToArray();
                    toidsIncidSelectionGisCount = gisRows.GroupBy(r => r[HluDataset.incid_mm_polygons.toidColumn.ColumnName]).Count();
                    fragsIncidSelectionGisCount = gisRows.Length;
                }

                // Count the total number of toids and fragments in the database
                // for this incid so that they can be included in the status area.
                int fragsIncidSelectionDbCount = 0;
                int toidsIncidSelectionDbCount = 0;

                string sqlFragsDbCount = String.Format("SELECT COUNT(*) FROM {0} WHERE {0}.{1} = {2}",
                    _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(incid));

                // Count the total number of fragments in the database for
                // this incid.
                fragsIncidSelectionDbCount = (int)_db.ExecuteScalar(sqlFragsDbCount,
                    _db.Connection.ConnectionTimeout, CommandType.Text);

                // Count the total number of toids in the database for
                // this incid.
                string _sqlToidsDbCount = String.Format(
                    "SELECT COUNT(*) FROM (SELECT DISTINCT {0} FROM {1} WHERE {2} = {3}) AS T",
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(incid));

                toidsIncidSelectionDbCount = (int)_db.ExecuteScalar(_sqlToidsDbCount,
                    _db.Connection.ConnectionTimeout, CommandType.Text);

                if (physicalSplit)
                {
                    // Check there aren't more incids in GIS than in the
                    // database, and there is at least one fragment in the
                    // database.
                    // There will be more fragments in GIS than the database
                    // prior to a physical split so don't check this.
                    if ((toidsIncidSelectionGisCount > toidsIncidSelectionDbCount) ||
                        (fragsIncidSelectionDbCount == 0))
                        return false;
                }
                else
                {
                    // Check there aren't more incids or fragments in GIS than
                    // in the database.
                    if ((toidsIncidSelectionGisCount > toidsIncidSelectionDbCount) ||
                        (fragsIncidSelectionGisCount > fragsIncidSelectionDbCount))
                        return false;
                }
            }

            return true;

        }

        private void IncidCurrentRowDerivedValuesRetrieve()
        {
            _incidLastModifiedUser = _incidCurrentRow.last_modified_user_id;
            _incidLastModifiedDate = Convert.IsDBNull(_incidCurrentRow.last_modified_date) ? DateTime.MinValue : _incidCurrentRow.last_modified_date;
            _incidPrimary = _incidCurrentRow.Ishabitat_primaryNull() ? null : _incidCurrentRow.habitat_primary;
            NewPrimaryHabitat(_incidPrimary);
            _incidIhsHabitat = _incidCurrentRow.Isihs_habitatNull() ? null : _incidCurrentRow.ihs_habitat;
        }

        private void CloneIncidCurrentRow()
        {
            _incidCurrentRowClone = _hluDS.incid.NewincidRow(); // IncidTable.NewincidRow();
            for (int i = 0; i < IncidTable.Columns.Count; i++)
                _incidCurrentRowClone[i] = _incidCurrentRow[i];
        }

        private void RestoreIncidCurrentRow()
        {
            if (_incidCurrentRowClone != null)
            {
                for (int i = 0; i < _hluDS.incid.Columns.Count; i++) // IncidTable.Columns.Count; i++)
                    _incidCurrentRow[i] = _incidCurrentRowClone[i];
            }
        }

        private bool CompareIncidCurrentRowClone()
        {
            if (_incidCurrentRowClone != null)
            {
                for (int i = 0; i < _hluDS.incid.Columns.Count; i++) // IncidTable.Columns.Count; i++)
                {
                    if ((_incidCurrentRow.IsNull(i) != _incidCurrentRowClone.IsNull(i)) ||
                        !_incidCurrentRow[i].Equals(_incidCurrentRowClone[i])) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Finds the DB row corresponding to the incid passed in and loads the next _incidPageSize rows
        /// starting at that row. If the method succeeds, the row corresponding to incid will be row 0 of
        /// the HluDataset.incid DataTable.
        /// </summary>
        /// <param name="incid">The incid whose row is to be made current.</param>
        /// <returns>The row number in HluDataset.incid corresponding to the incid passed in,
        /// or -1 if the search fails.</returns>
        private int GoToIncid(string incid)
        {
            int incidPageRowNoMinBak = _incidPageRowNoMin;
            int incidPageRowNoMaxBak = _incidPageRowNoMax;

            try
            {
                StringBuilder whereClause = new(String.Format("{0} >= {1}",
                    _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName), _db.QuoteValue(incid)));

                int seekRowNumber = (int)_db.ExecuteScalar(
                    String.Format("SELECT COUNT(*) FROM (SELECT {0} FROM {1} ORDER BY {0} ASC) WHERE {2}",
                    _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.incid.TableName), whereClause),
                    _db.Connection.ConnectionTimeout, CommandType.Text);

                whereClause.Append(String.Format(" AND {0} < {1} ORDER BY {0} ASC",
                    _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName),
                    _db.QuoteValue(_recIDs.IncidString(RecordIds.IncidNumber(incid) + IncidPageSize))));

                _hluTableAdapterMgr.Fill(_hluDS, typeof(HluDataSet.incidDataTable), whereClause.ToString(), true);

                _incidPageRowNoMin = seekRowNumber;
                _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count;

                return 0;
            }
            catch
            {
                _incidPageRowNoMin = incidPageRowNoMinBak;
                _incidPageRowNoMax = incidPageRowNoMaxBak;
                return -1;
            }
        }

        /// <summary>
        /// Translates a row number in the incid remote DB table, ordered by incid, into a row number in
        /// the in-memory incid DataTable, which only contains the current page of the entire DB table.
        /// If necessary, a new page is loaded from the database.
        /// </summary>
        /// <param name="seekRowNumber">Row number in the remote DB incid table, ordered by incid, whose
        /// corresponding row number in in-memory DataTable HluDataset.incid is sought.</param>
        /// <returns>The row number in in-memory DataTable HluDataset.incid that corresponds to
        /// row number seekRowNumber in the remote DB incid table, ordered by incid.
        /// If loading of a new page fails, -1 is returned and _incidPageRowNoMin and _incidPageRowNoMax
        /// are reset to their values before the attempted move.</returns>
        private int SeekIncid(int seekRowNumber)
        {
            // If within the current page, return the relative index.
            if ((seekRowNumber >= _incidPageRowNoMin)
                && (seekRowNumber <= _incidPageRowNoMax)
                && _hluDS.incid.Count > 0)
            {
                _incidPageRowNo = seekRowNumber - _incidPageRowNoMin;
                return _incidPageRowNo;
            }

            // Backup values in case of failure.
            int incidPageRowNoBak = _incidPageRowNo;
            int incidPageRowNoMinBak = _incidPageRowNoMin;
            int incidPageRowNoMaxBak = _incidPageRowNoMax;

            // Set-up the SQL load where clause template.
            string loadWhereClauseTemplate = String.Format("{0} >= {{0}} AND {0} < {{1}} ORDER BY {0} ASC",
                _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName));

            try
            {
                int seekIncidNumber = seekRowNumber;

                // If seeking the very early in the table.
                if (seekRowNumber < 2)
                {
                    // Get the first record.
                    seekIncidNumber = RecordIds.IncidNumber(
                        _db.ExecuteScalar(String.Format(
                        "SELECT TOP 1 {0} FROM {1} ORDER BY {0} ASC",
                            _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName),
                            _db.QualifyTableName(_hluDS.incid.TableName)),
                            _db.Connection.ConnectionTimeout, CommandType.Text).ToString());

                    // Fetch records.
                    _hluTableAdapterMgr.Fill(_hluDS, typeof(HluDataSet.incidDataTable),
                        String.Format(loadWhereClauseTemplate,
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber)),
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber + IncidPageSize))), true);

                    // Store the min and max row numbers in the page.
                    _incidPageRowNoMin = seekRowNumber;
                    _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count;

                    // Return the index of the first record in the page.
                    _incidPageRowNo = 0;
                }
                // If seeking very late in the table.
                else if (seekRowNumber >= _incidRowCount)
                {
                    // Get the last record.
                    seekIncidNumber = RecordIds.IncidNumber(
                        _db.ExecuteScalar(String.Format(
                        "SELECT TOP 1 {0} FROM {1} ORDER BY {0} DESC",
                            _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName),
                            _db.QualifyTableName(_hluDS.incid.TableName)),
                            _db.Connection.ConnectionTimeout, CommandType.Text).ToString());

                    // Move back by the page size.
                    if (seekIncidNumber > IncidPageSize)
                        seekIncidNumber -= (IncidPageSize - 1);

                    // Fetch records.
                    _hluTableAdapterMgr.Fill(_hluDS, typeof(HluDataSet.incidDataTable),
                        String.Format(loadWhereClauseTemplate,
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber)),
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber + IncidPageSize))), true);

                    // Move the seek row number back by the number of rows in the page.
                    seekRowNumber -= _hluDS.incid.Count;

                    // Store the min and max row numbers in the page.
                    _incidPageRowNoMin = seekRowNumber;
                    _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count;

                    // Return the index of the last record in the page.
                    _incidPageRowNo = _hluDS.incid.Count - 1;
                }
                else
                {
                    //_incidRowCount = (int)_db.ExecuteScalar(
                    //    String.Format("SELECT COUNT(*) FROM {0}",
                    //        _db.QualifyTableName(_hluDS.incid.TableName)),
                    //    _db.Connection.ConnectionTimeout, CommandType.Text);

                    // Set-up the SQL count clause.
                    string countSql = String.Format("SELECT COUNT(*) FROM {0} WHERE {1} <= {{0}}",
                        _db.QualifyTableName(_hluDS.incid.TableName),
                        _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName));

                    int count = 0;
                    while ((count < seekRowNumber) && (count < _incidRowCount))
                        {
                        // Count the number of records before the seek number.
                        count = (int)_db.ExecuteScalar(String.Format(countSql,
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber))),
                            _db.Connection.ConnectionTimeout, CommandType.Text);

                        seekIncidNumber += seekRowNumber - count;

                    };

                    // If moving backwards.
                    //if (seekRowNumber == oldRowNumber - 1)
                    //{
                    //    // Move back by the page size.
                    //    if (seekIncidNumber > IncidPageSize)
                    //        seekIncidNumber -= IncidPageSize;
                    //}

                    // Fetch records
                    _hluTableAdapterMgr.Fill(_hluDS, typeof(HluDataSet.incidDataTable),
                        String.Format(loadWhereClauseTemplate,
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber)),
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber + IncidPageSize))), true);

                    _incidPageRowNoMin = seekRowNumber;
                    _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count;

                    _incidPageRowNo = 0;
                }
            }
            catch
            {
                _incidPageRowNoMin = incidPageRowNoMinBak;
                _incidPageRowNoMax = incidPageRowNoMaxBak;
                _incidPageRowNo = incidPageRowNoBak;
                return -1;
            }

            return _incidPageRowNo;
        }

        /// <summary>
        /// Retrieves the row of the in-memory incid DataTable that corresponds to the incid of the row
        /// of the _incidSelection DataTable whose row number is passed in as parameter seekRowNumber.
        /// If necessary, a new page of selected incid rows is loaded from the database.
        /// </summary>
        /// <param name="seekRowNumber">Row number in the _incidSelection DataTable whose
        /// corresponding row in in-memory DataTable HluDataset.incid is sought.</param>
        /// <returns>The row of in-memory DataTable HluDataset.incid that corresponds to
        /// row number seekRowNumber in the _incidSelection DataTable.
        /// If loading of a new page fails, null is returned.</returns>
        private async Task<HluDataSet.incidRow> SeekIncidFiltered(int seekRowNumber)
        {
            seekRowNumber--;

            if (seekRowNumber < 0)
                seekRowNumber = 0;
            else if (seekRowNumber > _incidSelection.Rows.Count - 1)
                seekRowNumber = _incidSelection.Rows.Count - 1;

            string seekIncid = (string)_incidSelection.DefaultView[seekRowNumber][0];
            HluDataSet.incidRow returnRow = _hluDS.incid.FindByincid(seekIncid);

            // Enable the Incid table to be forced to refill if it has been
            // updated directly in the database rather than via the
            // local copy.
            if ((returnRow != null) && (!_refillIncidTable))
            {
                return returnRow;
            }
            else
            {
                _refillIncidTable = false;
                int seekIncidNumber = RecordIds.IncidNumber(seekIncid);
                int incidNumberPageMin;
                int incidNumberPageMax;
                if (_hluDS.incid.Rows.Count == 0)
                {
                    incidNumberPageMin = seekIncidNumber;
                    incidNumberPageMax = incidNumberPageMin + IncidPageSize;
                }
                else
                {
                    incidNumberPageMin = RecordIds.IncidNumber(_hluDS.incid[0].incid);
                    incidNumberPageMax = RecordIds.IncidNumber(_hluDS.incid[_hluDS.incid.Count - 1].incid);
                }

                int start = _incidCurrentRowIndex > 0 ? _incidCurrentRowIndex - 1 : 0;
                int stop = start;
                bool moveForward = true;

                //TODO: Check if seekIncidNumber is not -1 first?
                if (seekIncidNumber < incidNumberPageMin) // moving backward
                {
                    start = seekRowNumber - IncidPageSize > 0 ? seekRowNumber - IncidPageSize : 0;
                    stop = seekRowNumber > start ? seekRowNumber : IncidPageSize < _incidSelection.Rows.Count ?
                    IncidPageSize : _incidSelection.Rows.Count - 1;
                    moveForward = false;
                }
                else if (seekIncidNumber > incidNumberPageMax) // moving forward
                {
                    start = seekRowNumber;
                    stop = seekRowNumber + IncidPageSize < _incidSelection.Rows.Count ?
                        seekRowNumber + IncidPageSize : _incidSelection.Rows.Count - 1;
                }

                try
                {
                    string[] incids = new string[start == stop ? 1 : stop - start + 1];

                    for (int i = 0; i < incids.Length; i++)
                        incids[i] = _db.QuoteValue(_incidSelection.DefaultView[start + i][0]);

                    _hluTableAdapterMgr.incidTableAdapter.Fill(_hluDS.incid, String.Format("{0} IN ({1}) ORDER BY {0}",
                        _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName), String.Join(",", incids)));

                    if (_hluDS.incid.Count == 0)
                    {
                        MessageBox.Show("No database record retrieved.", "HLU: Selection",
                            MessageBoxButton.OK, MessageBoxImage.Asterisk);

                        // Reset the incid and map selections and move
                        // to the first incid in the database.
                        await ClearFilterAsync(true);
                        return _incidCurrentRow;
                    }
                    else
                    {
                        // If the table has paged backwards (because the required incid
                        // is lower than the page minimum) and if the row number being
                        // sought is the first (i.e. zero) then return the lowest incid.
                        // Otherwise, return the lowest or highest as appropriate.
                        return (moveForward || seekRowNumber == 0) ? _hluDS.incid[0] : _hluDS.incid[_hluDS.incid.Count - 1];
                    }
                }
                catch { return null; }
            }
        }

        private Dictionary<Type, string> BuildChildRowOrderByClauses()
        {
            Dictionary<Type, string> childRowOrberByDict = new()
            {
                //DONE: Aggregate
                {
                    typeof(HluDataSet.incid_secondaryDataTable),
                    string.Join(",", _hluDS.incid_secondary.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_conditionDataTable),
                    string.Join(",", _hluDS.incid_condition.PrimaryKey.Select(c => String.Format("{0} DESC", _db.QuoteIdentifier(c.ColumnName))))
                },
                {
                    typeof(HluDataSet.incid_ihs_matrixDataTable),
                    string.Join(",", _hluDS.incid_ihs_matrix.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_ihs_formationDataTable),
                    string.Join(",", _hluDS.incid_ihs_formation.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_ihs_managementDataTable),
                    string.Join(",", _hluDS.incid_ihs_management.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_ihs_complexDataTable),
                    string.Join(",", _hluDS.incid_ihs_complex.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_bapDataTable),
                    string.Join(",", _hluDS.incid_bap.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_sourcesDataTable),
                    string.Join(",", _hluDS.incid_sources.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                },
                {
                    typeof(HluDataSet.incid_osmm_updatesDataTable),
                    string.Join(",", _hluDS.incid_osmm_updates.PrimaryKey.Select(c => _db.QuoteIdentifier(c.ColumnName)))
                }
            };

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_secondaryDataTable), _hluDS.incid_secondary.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_conditionDataTable), _hluDS.incid_condition.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + String.Format("{0} DESC", _db.QuoteIdentifier(c.ColumnName))))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_ihs_matrixDataTable), _hluDS.incid_ihs_matrix.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_ihs_formationDataTable), _hluDS.incid_ihs_formation.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_ihs_managementDataTable), _hluDS.incid_ihs_management.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_ihs_complexDataTable), _hluDS.incid_ihs_complex.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_bapDataTable), _hluDS.incid_bap.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_sourcesDataTable), _hluDS.incid_sources.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            //childRowOrberByDict.Add(typeof(HluDataSet.incid_osmm_updatesDataTable), _hluDS.incid_osmm_updates.PrimaryKey
            //    .Aggregate(new(), (sb, c) => sb.Append("," + _db.QuoteIdentifier(c.ColumnName)))
            //    .Remove(0, 1).ToString());

            return childRowOrberByDict;
        }

        private Dictionary<Type, List<SqlFilterCondition>> BuildChildRowFilters()
        {
            Dictionary<Type, List<SqlFilterCondition>> childRowFilterDict =
                new()
                {
                    {
                        typeof(HluDataSet.incid_secondaryDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_secondary)
                    },
                    {
                        typeof(HluDataSet.incid_conditionDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_condition)
                    },
                    {
                        typeof(HluDataSet.incid_ihs_matrixDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_ihs_matrix)
                    },
                    {
                        typeof(HluDataSet.incid_ihs_formationDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_ihs_formation)
                    },
                    {
                        typeof(HluDataSet.incid_ihs_managementDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_ihs_management)
                    },
                    {
                        typeof(HluDataSet.incid_ihs_complexDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_ihs_complex)
                    },
                    {
                        typeof(HluDataSet.incid_bapDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_bap)
                    },
                    {
                        typeof(HluDataSet.incid_sourcesDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_sources)
                    },
                    {
                        typeof(HluDataSet.incid_osmm_updatesDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.incid_osmm_updates)
                    },
                    {
                        typeof(HluDataSet.historyDataTable),
                        ChildRowFilter(_hluDS.incid, _hluDS.history)
                    }
                };

            _incidMMPolygonsIncidFilter = new()
            {
                BooleanOperator = "OR",
                OpenParentheses = "(",
                Column = _hluDS.incid_mm_polygons.incidColumn,
                Table = _hluDS.incid_mm_polygons,
                Value = String.Empty,
                CloseParentheses = ")"
            };

            return childRowFilterDict;
        }

        internal SqlFilterCondition ChildRowFilter<T>(T table, DataColumn incidColumn)
            where T : DataTable
        {
            SqlFilterCondition cond = new()
            {
                BooleanOperator = "OR",
                OpenParentheses = "(",
                Column = incidColumn,
                Table = table,
                Value = String.Empty,
                CloseParentheses = ")"
            };
            return cond;
        }

        internal List<SqlFilterCondition> ChildRowFilter<P, C>(P parentTable, C childTable)
            where P : DataTable
            where C : DataTable
        {
            DataRelation rel = GetRelation<P, C>(parentTable, childTable);
            List<SqlFilterCondition> condList = [];

            for (int i = 0; i < rel.ChildColumns.Length; i++)
            {
                DataColumn c = rel.ChildColumns[i];
                SqlFilterCondition cond = new();
                if (i == 0)
                {
                    cond.BooleanOperator = "OR";
                    cond.OpenParentheses = "(";
                    cond.CloseParentheses = String.Empty;
                }
                else
                {
                    cond.BooleanOperator = "AND";
                    cond.OpenParentheses = String.Empty;
                }
                cond.Column = c;
                cond.Table = childTable;
                cond.ColumnSystemType = c.DataType;
                cond.Operator = "=";
                cond.Value = String.Empty;
                if (i == rel.ChildColumns.Length - 1)
                    cond.CloseParentheses = ")";
                else
                    cond.CloseParentheses = String.Empty;
                condList.Add(cond);
            }

            return condList;
        }

        private void GetIncidChildRows(HluDataSet.incidRow incidRow)
        {
            if (incidRow == null) return;

            string[] relValues = [incidRow.incid];

            HluDataSet.incid_secondaryDataTable secondaryTable = _hluDS.incid_secondary;
            _incidSecondaryRows = GetIncidChildRowsDb(relValues,
               _hluTableAdapterMgr.incid_secondaryTableAdapter, ref secondaryTable);

            GetSecondaryHabitats();

            HluDataSet.incid_conditionDataTable incidConditionTable = _hluDS.incid_condition;
            _incidConditionRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_conditionTableAdapter, ref incidConditionTable);
            _origIncidConditionCount = _incidConditionRows.Length;

            HluDataSet.incid_ihs_matrixDataTable ihsMatrixTable = _hluDS.incid_ihs_matrix;
            _incidIhsMatrixRows = GetIncidChildRowsDb(relValues,
               _hluTableAdapterMgr.incid_ihs_matrixTableAdapter, ref ihsMatrixTable);
            _origIncidIhsMatrixCount = _incidIhsMatrixRows.Length;

            HluDataSet.incid_ihs_formationDataTable ihsFormationTable = _hluDS.incid_ihs_formation;
            _incidIhsFormationRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_ihs_formationTableAdapter, ref ihsFormationTable);
            _origIncidIhsFormationCount = _incidIhsFormationRows.Length;

            HluDataSet.incid_ihs_managementDataTable ihsManagementTable = _hluDS.incid_ihs_management;
            _incidIhsManagementRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_ihs_managementTableAdapter, ref ihsManagementTable);
            _origIncidIhsManagementCount = _incidIhsManagementRows.Length;

            HluDataSet.incid_ihs_complexDataTable ihsComplexTable = _hluDS.incid_ihs_complex;
            _incidIhsComplexRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_ihs_complexTableAdapter, ref ihsComplexTable);
            _origIncidIhsComplexCount = _incidIhsComplexRows.Length;

            HluDataSet.incid_bapDataTable incidBapTable = _hluDS.incid_bap;
            _incidBapRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_bapTableAdapter, ref incidBapTable);

            // Get the BAP habitats and compare them to those relating to the
            // primary and secondary codes.
            GetBapEnvironments();

            HluDataSet.incid_sourcesDataTable incidSourcesTable = _hluDS.incid_sources;
            _incidSourcesRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_sourcesTableAdapter, ref incidSourcesTable);
            _origIncidSourcesCount = _incidSourcesRows.Length;

            HluDataSet.historyDataTable historyTable = _hluDS.history;
            _incidHistoryRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.historyTableAdapter, ref historyTable);

            HluDataSet.incid_osmm_updatesDataTable incidOSMMUpdatesTable = _hluDS.incid_osmm_updates;
            _incidOSMMUpdatesRows = GetIncidChildRowsDb(relValues,
                _hluTableAdapterMgr.incid_osmm_updatesTableAdapter, ref incidOSMMUpdatesTable);

        }

        private DataRelation GetRelation<P, C>(P parentTable, C childTable)
            where P : DataTable
            where C : DataTable
        {
            try
            {
                return _hluDataRelations.Single(r => r.ParentTable == parentTable && r.ChildTable == childTable);
            }
            catch { return null; }
        }

        private R[] GetIncidChildRows<P, C, R>(P parentTable, C childTable)
            where P : DataTable
            where C : DataTable
            where R : DataRow
        {
            if (IncidCurrentRow == null) return null;

            DataRelation rel = GetRelation(parentTable, childTable);
            if (rel != null)
                return (R[])IncidCurrentRow.GetChildRows(rel, DataRowVersion.Default);
            else
                return [];
        }

        internal R[] GetIncidChildRowsDb<C, R>(object[] relValues, HluTableAdapter<C, R> adapter, ref C childTable)
            where C : DataTable, new()
            where R : DataRow
        {
            List<SqlFilterCondition> childConds;

            if (_childRowFilterDict.TryGetValue(typeof(C), out childConds))
            {
                for (int i = 0; i < childConds.Count; i++)
                {
                    SqlFilterCondition cond = childConds[i];
                    cond.Value = relValues[i];
                    childConds[i] = cond;
                }

                // Sort after the Fill as the Select seems to be re-sorting
                // the rows after the Fill.
                string orderByClause;
                adapter.Fill(childTable, childConds);

                if (_childRowOrderByDict.TryGetValue(typeof(C), out orderByClause))
                    return (R[])childTable.Select(null, orderByClause);
                else
                    return (R[])childTable.Select();
            }
            else
            {
                return [];
            }
        }

        internal void GetIncidMMPolygonRows(List<List<SqlFilterCondition>> whereClause,
            ref HluDataSet.incid_mm_polygonsDataTable table)
        {
            if ((whereClause != null) && (whereClause.Count > 0))
            {
                if (_hluTableAdapterMgr.incid_mm_polygonsTableAdapter == null)
                    _hluTableAdapterMgr.incid_mm_polygonsTableAdapter =
                        new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable,
                            HluDataSet.incid_mm_polygonsRow>(_db);

                _hluTableAdapterMgr.incid_mm_polygonsTableAdapter.Fill(table, whereClause);
            }
        }

        internal void GetIncidOSMMUpdatesRows(List<List<SqlFilterCondition>> whereClause,
            ref HluDataSet.incid_osmm_updatesDataTable table)
        {
            if ((whereClause != null) && (whereClause.Count > 0))
            {
                if (_hluTableAdapterMgr.incid_osmm_updatesTableAdapter == null)
                    _hluTableAdapterMgr.incid_osmm_updatesTableAdapter =
                        new HluTableAdapter<HluDataSet.incid_osmm_updatesDataTable,
                            HluDataSet.incid_osmm_updatesRow>(_db);

                _hluTableAdapterMgr.incid_osmm_updatesTableAdapter.Fill(table, whereClause);
            }
        }

        /// <summary>
        /// Determines whether any of the incid tables are dirty].
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncid()
        {
            // If anything has changed in any of the data tables
            return ((_incidCurrentRow != null) && (_incidCurrentRow.RowState != DataRowState.Detached) &&
                ((_incidCurrentRow.Ishabitat_primaryNull() && !String.IsNullOrEmpty(_incidPrimary)) ||
                (!_incidCurrentRow.Ishabitat_primaryNull() && String.IsNullOrEmpty(_incidPrimary)) ||
                (_incidPrimary != _incidCurrentRow.habitat_primary) ||
                !CompareIncidCurrentRowClone()));
        }

        /// <summary>
        /// Determines whether the incid ihs matrix table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsMatrix()
        {
            if (_incidIhsMatrixRows != null)
            {
                if (_incidIhsMatrixRows.Count(r => r != null) != _origIncidIhsMatrixCount) return true;

                foreach (DataRow r in _incidIhsMatrixRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsMatrixCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs formation table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsFormation()
        {
            if (_incidIhsFormationRows != null)
            {
                if (_incidIhsFormationRows.Count(r => r != null) != _origIncidIhsFormationCount) return true;

                foreach (DataRow r in _incidIhsFormationRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsFormationCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs management table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsManagement()
        {
            if (_incidIhsManagementRows != null)
            {
                if (_incidIhsManagementRows.Count(r => r != null) != _origIncidIhsManagementCount) return true;

                foreach (DataRow r in _incidIhsManagementRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsManagementCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs complex table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsComplex()
        {
            if (_incidIhsComplexRows != null)
            {
                if (_incidIhsComplexRows.Count(r => r != null) != _origIncidIhsComplexCount) return true;

                foreach (DataRow r in _incidIhsComplexRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsComplexCount != 0;
        }

        /// <summary>
        /// Determines whether the incid secondary table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidSecondary()
        {
            if (_incidSecondaryRows.Any(r => r.RowState == DataRowState.Deleted)) return true;

            if (_incidSecondaryHabitats != null)
            {
                if (_incidSecondaryHabitats.Any(sh => IncidSecondaryRowDirty(sh))) return true;
            }

            if ((_incidSecondaryRows != null) && (_incidSecondaryHabitats.Count !=
                _incidSecondaryRows.Length)) return true;

            if (_incidSecondaryRows != null)
            {
                foreach (DataRow r in _incidSecondaryRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the incid condition table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidCondition()
        {
            if (_incidConditionRows != null)
            {
                if (_incidConditionRows.Count(r => r != null) != _origIncidConditionCount) return true;

                foreach (DataRow r in _incidConditionRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidConditionCount != 0;
        }

        /// <summary>
        /// Determines whether the incid bap table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidBap()
        {
            if (_incidBapRows.Any(r => r.RowState == DataRowState.Deleted)) return true;
            int incidBapRowsAutoNum = 0;
            if (_incidBapRowsAuto != null)
            {
                incidBapRowsAutoNum = _incidBapRowsAuto.Count;
                if (_incidBapRowsAuto.Any(be => IncidBapRowDirty(be))) return true;
            }
            int incidBapRowsAutoUserNum = 0;
            if (_incidBapRowsUser != null)
            {
                incidBapRowsAutoUserNum = _incidBapRowsUser.Count;
                if (_incidBapRowsUser.Any(be => IncidBapRowDirty(be))) return true;
            }

            if ((_incidBapRows != null) && (incidBapRowsAutoNum + incidBapRowsAutoUserNum !=
                _incidBapRows.Length)) return true;

            if (_incidBapRows != null)
            {
                foreach (DataRow r in _incidBapRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the incid sources table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        private bool IsDirtyIncidSources()
        {
            if (_incidSourcesRows != null)
            {
                if (_incidSourcesRows.Count(r => r != null) != _origIncidSourcesCount) return true;

                foreach (DataRow r in _incidSourcesRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidSourcesCount != 0;
        }

        /// <summary>
        /// Determines whether the incid osmm updates table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidOSMMUpdates()
        {
            if (_incidOSMMUpdatesRows != null)
            {
                foreach (DataRow r in _incidOSMMUpdatesRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        private bool IncidSecondaryRowDirty(SecondaryHabitat sh)
        {
            // deleted secondary habitat row
            var q = _incidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted && r.secondary_id == sh.secondary_id);
            switch (q.Count())
            {
                case 0:
                    return true; // new row;
                case 1:
                    if (!sh.IsValid() && sh.IsAdded) return true;

                    HluDataSet.incid_secondaryRow oldRow = q.ElementAt(0);
                    object[] itemArray = sh.ToItemArray();
                    for (int i = 0; i < itemArray.Length; i++)
                    {
                        if (oldRow.IsNull(i))
                        {
                            if (itemArray[i] != null) return true;
                        }
                        else if (!oldRow[i].Equals(itemArray[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return true; // duplicate row must be new or altered
            }
        }

        private bool IncidBapRowDirty(BapEnvironment be)
        {
            // deleted user BAP row
            var q = _incidBapRows.Where(r => r.RowState != DataRowState.Deleted && r.bap_id == be.bap_id);
            switch (q.Count())
            {
                case 0:
                    return true; // new row;
                case 1:
                    // Only flag an incid_bap row that is invalid as dirty if it has
                    // been added by the user. This allows existing records to be
                    // viewed in the user interface without warning the user that
                    // the data has changed.
                    if (!be.IsValid() && be.IsAdded) return true;

                    HluDataSet.incid_bapRow oldRow = q.ElementAt(0);
                    object[] itemArray = be.ToItemArray();
                    for (int i = 0; i < itemArray.Length; i++)
                    {
                        if (oldRow.IsNull(i))
                        {
                            if (itemArray[i] != null) return true;
                        }
                        else if (!oldRow[i].Equals(itemArray[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return true; // duplicate row must be new or altered
            }
        }

        #endregion

        #region Refresh

        internal void RefreshAll()
        {
            OnPropertyChanged(nameof(CanCopy));
            OnPropertyChanged(nameof(CanPaste));
            OnPropertyChanged(nameof(TabItemSelected));
            RefreshBulkUpdateControls();
            RefreshOSMMUpdateControls();
            RefreshStatus();
            RefreshHeader();
            RefreshOSMMUpdate();
            RefreshHabitatTab();
            RefreshIHSTab();
            RefreshPriorityTab();
            RefreshDetailsTab();
            RefreshSources();
            RefreshHistory();
            CheckEditingControlState();
        }

        private void RefreshBulkUpdateControls()
        {
            OnPropertyChanged(nameof(ShowInBulkUpdateMode));
            OnPropertyChanged(nameof(HideInBulkUpdateMode));
            OnPropertyChanged(nameof(BulkUpdateCommandHeader));
            OnPropertyChanged(nameof(OSMMBulkUpdateCommandHeader));
            OnPropertyChanged(nameof(TopControlsGroupHeader));
            OnPropertyChanged(nameof(TabItemHistoryEnabled));

            OnPropertyChanged(nameof(NumIncidSelectedDB));
            OnPropertyChanged(nameof(NumToidSelectedDB));
            OnPropertyChanged(nameof(NumFragmentsSelectedDB));

            OnPropertyChanged(nameof(NumIncidSelectedMap));
            OnPropertyChanged(nameof(NumToidSelectedMap));
            OnPropertyChanged(nameof(NumFragmentsSelectedMap));

            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));
        }

        private void RefreshOSMMUpdateControls()
        {
            OnPropertyChanged(nameof(ShowInOSMMUpdateMode));
            OnPropertyChanged(nameof(HideInOSMMUpdateMode));
            OnPropertyChanged(nameof(OSMMUpdateCommandHeader));
            OnPropertyChanged(nameof(TopControlsGroupHeader));
            OnPropertyChanged(nameof(ShowReasonProcessGroup));
        }

        private void RefreshStatus()
        {
            OnPropertyChanged(nameof(EditMode));
            OnPropertyChanged(nameof(IncidCurrentRowIndex));
            OnPropertyChanged(nameof(OSMMIncidCurrentRowIndex));
            OnPropertyChanged(nameof(StatusIncid));
            OnPropertyChanged(nameof(StatusIncidToolTip));
            OnPropertyChanged(nameof(StatusBar));
            OnPropertyChanged(nameof(CanZoomSelection));
            OnPropertyChanged(nameof(CanBulkUpdate));
            OnPropertyChanged(nameof(CanBulkUpdateMode));
            OnPropertyChanged(nameof(CanOSMMUpdateMode));
            OnPropertyChanged(nameof(CanOSMMBulkUpdateMode));
            //---------------------------------------------------------------------
            // FIX: 103 Accept/Reject OSMM updates in edit mode.
            //
            OnPropertyChanged(nameof(CanOSMMUpdateAccept));
            OnPropertyChanged(nameof(CanOSMMUpdateReject));
            //---------------------------------------------------------------------
            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(CanClearFilter));
        }

        private void RefreshHeader()
        {
            OnPropertyChanged(nameof(ReasonCodes));
            OnPropertyChanged(nameof(Reason));
            OnPropertyChanged(nameof(ProcessCodes));
            OnPropertyChanged(nameof(Process));
            OnPropertyChanged(nameof(Incid));
            OnPropertyChanged(nameof(IncidArea));
            OnPropertyChanged(nameof(IncidLength));
            OnPropertyChanged(nameof(IncidCreatedDate));
            OnPropertyChanged(nameof(IncidLastModifiedDate));
            OnPropertyChanged(nameof(IncidCreatedUser));
            OnPropertyChanged(nameof(IncidLastModifiedUser));
        }

        private void RefreshGroupHeaders()
        {
            OnPropertyChanged(nameof(TopControlsGroupHeader));

            OnPropertyChanged(nameof(HabitatHeader));
            OnPropertyChanged(nameof(PrimaryHeader));
            OnPropertyChanged(nameof(SecondaryHabitatsHeader));
            OnPropertyChanged(nameof(HabitatSummaryHeader));
            OnPropertyChanged(nameof(LegacyHeader));

            OnPropertyChanged(nameof(IhsHabitatHeader));
            OnPropertyChanged(nameof(IhsMatrixHeader));
            OnPropertyChanged(nameof(IhsFormationHeader));
            OnPropertyChanged(nameof(IhsManagementHeader));
            OnPropertyChanged(nameof(IhsComplexHeader));
            OnPropertyChanged(nameof(IhsSummaryHeader));

            OnPropertyChanged(nameof(DetailsCommentsHeader));
            OnPropertyChanged(nameof(DetailsSiteHeader));
            OnPropertyChanged(nameof(DetailsMapsHeader));
            OnPropertyChanged(nameof(DetailsConditionHeader));
            OnPropertyChanged(nameof(DetailsQualityHeader));

            OnPropertyChanged(nameof(Source1Header));
            OnPropertyChanged(nameof(Source2Header));
            OnPropertyChanged(nameof(Source3Header));
            OnPropertyChanged(nameof(ShowSourceNumbers));
        }

        private void RefreshOSMMUpdate()
        {
            OnPropertyChanged(nameof(ShowIncidOSMMPendingGroup));
            OnPropertyChanged(nameof(IncidOSMMProcessFlag));
            OnPropertyChanged(nameof(IncidOSMMSpatialFlag));
            OnPropertyChanged(nameof(IncidOSMMChangeFlag));
            OnPropertyChanged(nameof(IncidOSMMUpdateStatus));
            OnPropertyChanged(nameof(IncidOSMMHabitatPrimary));
            OnPropertyChanged(nameof(IncidOSMMHabitatSecondaries));
            OnPropertyChanged(nameof(IncidOSMMXRefID));
        }

        private void RefreshHabitatTab()
        {
            OnPropertyChanged(nameof(TabItemHabitatEnabled));
            OnPropertyChanged(nameof(TabHabitatControlsEnabled));
            OnPropertyChanged(nameof(HabitatTabLabel));
            //OnPropertyChanged(nameof(HabitatClassCodes));
            OnPropertyChanged(nameof(HabitatTypeCodes));
            OnPropertyChanged(nameof(HabitatType));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory));
            OnPropertyChanged(nameof(HabitatSecondariesSuggested));
            OnPropertyChanged(nameof(HabitatTips));
            OnPropertyChanged(nameof(HabitatClass));
            OnPropertyChanged(nameof(IncidPrimary));
            OnPropertyChanged(nameof(NvcCodes));
            OnPropertyChanged(nameof(IncidSecondaryHabitats));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory)); //TODO: Needed twice?
            OnPropertyChanged(nameof(IncidSecondarySummary));
            OnPropertyChanged(nameof(LegacyHabitatCodes));
            OnPropertyChanged(nameof(IncidLegacyHabitat));
        }

        private void RefreshIHSTab()
        {
            OnPropertyChanged(nameof(TabItemIHSEnabled));
            OnPropertyChanged(nameof(TabIhsControlsEnabled));
            OnPropertyChanged(nameof(IHSTabLabel));
            OnPropertyChanged(nameof(IncidIhsHabitat));
            RefreshIhsMultiplexValues();
        }

        private void RefreshIhsMultiplexValues()
        {
            OnPropertyChanged(nameof(IncidIhsHabitatText));
            OnPropertyChanged(nameof(IncidIhsMatrix1Text));
            OnPropertyChanged(nameof(IncidIhsMatrix2Text));
            OnPropertyChanged(nameof(IncidIhsMatrix3Text));
            OnPropertyChanged(nameof(IncidIhsFormation1Text));
            OnPropertyChanged(nameof(IncidIhsFormation2Text));
            OnPropertyChanged(nameof(IncidIhsManagement1Text));
            OnPropertyChanged(nameof(IncidIhsManagement2Text));
            OnPropertyChanged(nameof(IncidIhsComplex1Text));
            OnPropertyChanged(nameof(IncidIhsComplex2Text));
            OnPropertyChanged(nameof(IncidIhsSummary));
        }

        private void RefreshPriorityTab()
        {
            OnPropertyChanged(nameof(TabItemPriorityEnabled));
            OnPropertyChanged(nameof(TabPriorityControlsEnabled));
            OnPropertyChanged(nameof(PriorityTabLabel));
            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
            OnPropertyChanged(nameof(IncidBapHabitatsUser));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));
        }

        private void RefreshDetailsTab()
        {
            OnPropertyChanged(nameof(TabItemDetailsEnabled));
            OnPropertyChanged(nameof(TabDetailsControlsEnabled));
            OnPropertyChanged(nameof(DetailsTabLabel));

            OnPropertyChanged(nameof(IncidGeneralComments));
            OnPropertyChanged(nameof(IncidBoundaryBaseMap));
            OnPropertyChanged(nameof(IncidDigitisationBaseMap));
            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));

            OnPropertyChanged(nameof(IncidSiteRef));
            OnPropertyChanged(nameof(IncidSiteName));

            OnPropertyChanged(nameof(ConditionCodes));
            OnPropertyChanged(nameof(IncidCondition));
            OnPropertyChanged(nameof(IncidConditionQualifier));
            OnPropertyChanged(nameof(IncidConditionDate));
            OnPropertyChanged(nameof(IncidConditionEnabled));

            OnPropertyChanged(nameof(QualityDeterminationCodes));
            OnPropertyChanged(nameof(IncidQualityDetermination));
            OnPropertyChanged(nameof(QualityInterpretationCodes));
            OnPropertyChanged(nameof(IncidQualityInterpretation));
            OnPropertyChanged(nameof(IncidQualityComments));
        }

        private void RefreshSources()
        {
            OnPropertyChanged(nameof(TabItemSourcesEnabled));
            OnPropertyChanged(nameof(TabSourcesControlsEnabled));
            OnPropertyChanged(nameof(SourcesTabLabel));
            RefreshSource1();
            RefreshSource2();
            RefreshSource3();
        }

        private void RefreshSource1()
        {
            OnPropertyChanged(nameof(IncidSource1Id));
            OnPropertyChanged(nameof(Source1Names));
            OnPropertyChanged(nameof(IncidSource1Date));
            OnPropertyChanged(nameof(IncidSource1HabitatClass));
            OnPropertyChanged(nameof(IncidSource1HabitatType));
            OnPropertyChanged(nameof(Source1HabitatTypeCodes));
            //---------------------------------------------------------------------
            // FIXED: KI103 (Record selectors)
            // FIXED: KI109 (Source habitat types)
            // Both issues seem to relate to the source habitat type value
            // not being displayed after the list of source habitat types
            // has been refreshed following a change of habitat class.
            // So the 'OnPropertyChanged' method is called again to
            // trigger a display refresh.
            OnPropertyChanged(nameof(IncidSource1HabitatType));
            //---------------------------------------------------------------------
            OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource1HabitatImportance));
            OnPropertyChanged(nameof(IncidSource1Enabled));
        }

        private void RefreshSource2()
        {
            OnPropertyChanged(nameof(IncidSource2Id));
            OnPropertyChanged(nameof(Source2Names));
            OnPropertyChanged(nameof(IncidSource2Date));
            OnPropertyChanged(nameof(IncidSource2HabitatClass));
            OnPropertyChanged(nameof(IncidSource2HabitatType));
            OnPropertyChanged(nameof(Source2HabitatTypeCodes));
            //---------------------------------------------------------------------
            // FIXED: KI103 (Record selectors)
            // FIXED: KI109 (Source habitat types)
            // Both issues seem to relate to the source habitat type value
            // not being displayed after the list of source habitat types
            // has been refreshed following a change of habitat class.
            // So the 'OnPropertyChanged' method is called again to
            // trigger a display refresh.
            OnPropertyChanged(nameof(IncidSource2HabitatType));
            //---------------------------------------------------------------------
            OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource2HabitatImportance));
            OnPropertyChanged(nameof(IncidSource2Enabled));
        }

        private void RefreshSource3()
        {
            OnPropertyChanged(nameof(IncidSource3Id));
            OnPropertyChanged(nameof(Source3Names));
            OnPropertyChanged(nameof(IncidSource3Date));
            OnPropertyChanged(nameof(IncidSource3HabitatClass));
            OnPropertyChanged(nameof(IncidSource3HabitatType));
            OnPropertyChanged(nameof(Source3HabitatTypeCodes));
            //---------------------------------------------------------------------
            // FIXED: KI103 (Record selectors)
            // FIXED: KI109 (Source habitat types)
            // Both issues seem to relate to the source habitat type value
            // not being displayed after the list of source habitat types
            // has been refreshed following a change of habitat class.
            // So the 'OnPropertyChanged' method is called again to
            // trigger a display refresh.
            OnPropertyChanged(nameof(IncidSource3HabitatType));
            //---------------------------------------------------------------------
            OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource3HabitatImportance));
            OnPropertyChanged(nameof(IncidSource3Enabled));
        }

        private void RefreshHistory()
        {
            OnPropertyChanged(nameof(TabItemHistoryEnabled));
            OnPropertyChanged(nameof(IncidHistory));
        }

        #endregion

        #region Lock Editing Controls

        private void CheckEditingControlState()
        {
            OnPropertyChanged(nameof(ReasonProcessEnabled));
            OnPropertyChanged(nameof(TabControlDataEnabled));
        }

        public void ChangeEditingControlState(bool enable)
        {
            _reasonProcessEnabled = enable;
            _tabControlDataEnabled = enable;
            CheckEditingControlState();
        }

        public bool ReasonProcessEnabled
        {
            get
            {
                if ((_bulkUpdateMode == false && _osmmUpdateMode == false) && IncidCurrentRow == null) _reasonProcessEnabled = false;
                return _reasonProcessEnabled;
            }
            set { _reasonProcessEnabled = value; }
        }

        public bool TabControlDataEnabled
        {
            get
            {
                if ((_bulkUpdateMode == false) && IncidCurrentRow == null) _tabControlDataEnabled = false;
                return _windowEnabled && _tabControlDataEnabled;
            }
            set { _tabControlDataEnabled = value; }
        }

        public int TabItemSelected
        {
            get { return _tabItemSelected; }
            set { _tabItemSelected = value; }
        }

        public bool TabItemHabitatEnabled
        {
            get { return _tabItemHabitatEnabled; }
            set { _tabItemHabitatEnabled = value; }
        }

        public bool TabItemPriorityEnabled
        {
            get { return _tabItemPriorityEnabled; }
            set { _tabItemPriorityEnabled = value; }
        }

        public bool TabItemDetailsEnabled
        {
            get { return _tabItemDetailsEnabled; }
            set { _tabItemDetailsEnabled = value; }
        }

        public bool TabItemIHSEnabled
        {
            get { return _tabItemIHSEnabled; }
            set { _tabItemIHSEnabled = value; }
        }

        public bool TabItemSourcesEnabled
        {
            get { return _tabItemSourcesEnabled; }
            set { _tabItemSourcesEnabled = value; }
        }

        public bool TabItemHistoryEnabled
        {
            get { return _tabItemHistoryEnabled; }
            set { _tabItemHistoryEnabled = value; }
        }

        public bool TabHabitatControlsEnabled
        {
            get { return _tabHabitatControlsEnabled; }
            set { _tabHabitatControlsEnabled = value; }
        }

        public bool TabIhsControlsEnabled
        {
            get { return _tabIhsControlsEnabled; }
            set { _tabIhsControlsEnabled = value; }
        }

        public bool TabPriorityControlsEnabled
        {
            get { return _tabPriorityControlsEnabled; }
            set { _tabPriorityControlsEnabled = value; }
        }

        public bool TabDetailsControlsEnabled
        {
            get { return _tabDetailsControlsEnabled; }
            set { _tabDetailsControlsEnabled = value; }
        }

        public bool TabSourcesControlsEnabled
        {
            get { return _tabSourcesControlsEnabled; }
            set { _tabSourcesControlsEnabled = value; }
        }

        #endregion

        #region Header Fields

        public string Incid
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.incidColumn))
                    return IncidCurrentRow.incid;
                else
                    return null;
            }
            set { if ((IncidCurrentRow != null) && (value != null)) IncidCurrentRow.incid = value; }
        }

        public string IncidArea
        {
            get
            {
                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Don't show the area when in OSMM Update mode and there are no
                // updates to process.
                //
                if ((_bulkUpdateMode == false) && (_osmmUpdateMode == false || _osmmUpdatesEmpty == false))
                //---------------------------------------------------------------------
                {
                    GetIncidMeasures();
                    return _incidArea.ToString();
                }
                else
                {
                    return null;
                }
            }
            set { }
        }

        public string IncidLength
        {
            get
            {
                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Don't show the length when in OSMM Update mode and there are no
                // updates to process.
                //
                if ((_bulkUpdateMode == false) && (_osmmUpdateMode == false || _osmmUpdatesEmpty == false))
                //---------------------------------------------------------------------
                {
                    GetIncidMeasures();
                    return _incidLength.ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        private void GetIncidMeasures()
        {
            if (((_incidArea != -1) && (_incidLength != -1)) || (IncidCurrentRow == null)) return;

            _incidMMPolygonsIncidFilter.Value = Incid;
            HluDataSet.incid_mm_polygonsDataTable table = HluDataset.incid_mm_polygons;

            List<SqlFilterCondition> incidCond =
                new([_incidMMPolygonsIncidFilter]);
            List<List<SqlFilterCondition>> incidCondList = [incidCond];
            GetIncidMMPolygonRows(incidCondList, ref table);

            _incidArea = 0;
            _incidLength = 0;
            foreach (HluDataSet.incid_mm_polygonsRow r in table)
            {
                _incidArea += r.shape_area;
                _incidLength += r.shape_length;
            }

            _incidArea = Math.Round(_incidArea / 10000, 4);
            _incidLength = Math.Round(_incidLength / 1000, 3);
        }

        public string IncidCreatedDate
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.created_dateColumn))
                {
                    if (IncidCurrentRow.created_date.ToShortTimeString() == "00:00")
                        return IncidCurrentRow.created_date.ToShortDateString();
                    else
                        return String.Format("{0} {1}", IncidCurrentRow.created_date.ToShortDateString(), IncidCurrentRow.created_date.ToShortTimeString());
                }
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    DateTime newDate;
                    if (DateTime.TryParse(value, out newDate))
                        IncidCurrentRow.created_date = newDate;
                }
            }
        }

        public string IncidLastModifiedDate
        {
            get
            {
                if (IncidCurrentRow != null && _osmmUpdatesEmpty == false && _incidLastModifiedDate != DateTime.MinValue)
                {
                    if (_incidLastModifiedDate.ToShortTimeString() == "00:00")
                        return _incidLastModifiedDate.ToShortDateString();
                    else
                        return String.Format("{0} {1}", _incidLastModifiedDate.ToShortDateString(), _incidLastModifiedDate.ToShortTimeString());
                }
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    DateTime newDate;
                    if (DateTime.TryParse(value, out newDate))
                        _incidLastModifiedDate = newDate;
                }
            }
        }

        public string IncidCreatedUser
        {
            get
            {
                // Display the created user's name from the lut_user table
                // (if found) instead of the user_id
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.created_user_idColumn))
                    return String.IsNullOrEmpty(IncidCurrentRow.lut_userRowByfk_incid_user_created.user_name)
                        ? IncidCurrentRow.created_user_id : IncidCurrentRow.lut_userRowByfk_incid_user_created.user_name;
                else
                    return null;
            }
            set { if ((IncidCurrentRow != null) && (value != null)) IncidCurrentRow.created_user_id = value; }
        }

        public string IncidLastModifiedUser
        {
            get
            {
                // Display the last modified user's name from the lut_user table
                // (if found) instead of the user_id
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.last_modified_user_idColumn))
                    return String.IsNullOrEmpty(IncidCurrentRow.lut_userRowByfk_incid_user_modified.user_name)
                        ? IncidCurrentRow.last_modified_user_id : IncidCurrentRow.lut_userRowByfk_incid_user_modified.user_name;
                else
                    return null;
            }
            set { if ((IncidCurrentRow != null) && (value != null)) _incidLastModifiedUser = value; }
        }

        #endregion

        #region OSMM Updates

        //---------------------------------------------------------------------
        // CHANGED: CR49 Process proposed OSMM Updates
        //
        /// <summary>
        /// Only show the OSMM Updates group if required, otherwise collapse it.
        /// </summary>
        public Visibility ShowIncidOSMMPendingGroup
        {
            get
            {
                // Show the group if not in osmm update mode and
                // show updates are "Always" required, or "When Outstanding"
                // (i.e. update flag is "Proposed" (> 0) or "Pending" = 0).
                if ((_osmmUpdateMode == true) ||
                    (_bulkUpdateMode == false &&
                    (_showOSMMUpdates == "Always" ||
                    (_showOSMMUpdates == "When Outstanding" && (IncidOSMMStatus >= 0)))))
                {
                    // Adjust the window height if not already showing the group.
                    if (!_showingOSMMPendingGroup)
                    {
                        _showingOSMMPendingGroup = true;
                    }

                    return Visibility.Visible;
                }
                else
                {
                    // Adjust the window height if currently showing the group.
                    if (_showingOSMMPendingGroup)
                    {
                        _showingOSMMPendingGroup = false;
                    }

                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        /// <summary>
        /// Gets the OSMM process flag that relates to the selected incid.
        /// It is used to show how the latest OSMM translation was
        /// processed.
        /// </summary>
        /// <value>
        /// The string of Process Flag related to the current incid.
        /// </value>
        public string IncidOSMMProcessFlag
        {
            get
            {
                if (_incidOSMMUpdatesProcessFlag != 0)
                    return _incidOSMMUpdatesProcessFlag.ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the OSMM spatial flag that relates to the selected incid.
        /// It is used to show how the latest OSMM translation was
        /// processed.
        /// </summary>
        /// <value>
        /// The string of Spatial Flag related to the current incid.
        /// </value>
        public string IncidOSMMSpatialFlag
        {
            get
            {
                return _incidOSMMUpdatesSpatialFlag;
            }
        }

        /// <summary>
        /// Gets the OSMM change flag that relates to the selected incid.
        /// It is used to show how the latest OSMM translation was
        /// processed.
        /// </summary>
        /// <value>
        /// The string of Change Flag related to the current incid.
        /// </value>
        public string IncidOSMMChangeFlag
        {
            get
            {
                return _incidOSMMUpdatesChangeFlag;
            }
        }

        /// <summary>
        /// Gets the OSMM updates status that relates to the selected incid.
        /// It contains the update status of the latest OSMM translation.
        /// </summary>
        /// <value>
        /// The integer value of OSMM Updates Status related to the current incid.
        /// </value>
        public Nullable<int> IncidOSMMStatus
        {
            get
            {
                return _incidOSMMUpdatesStatus;
            }
        }

        /// <summary>
        /// Gets the OSMM Update proposed Habitat Primary that relates to the
        /// selected incid. It is used to show how the latest OSMM translates.
        /// </summary>
        /// <value>
        /// The string of OSMM Habitat Primary related to the current incid.
        /// </value>
        public string IncidOSMMHabitatPrimary
        {
            get
            {
                // If there are no OSMM proposed updates or the proposed update
                // for this incid has already been processed then return null.
                if (_osmmUpdatesEmpty == true || _incidOSMMUpdatesOSMMXref <= 0) return null;

                // Return the first proposed update primary code for this incid
                // (there should only be one).
                var q = from ohx in _lutOsmmHabitatXref
                        where ohx.osmm_xref_id == _incidOSMMUpdatesOSMMXref
                        select ohx.habitat_primary;

                if (q.Any())
                    return q.ElementAt(0);
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the OSMM Update proposed Habitat Secondaries that relates to the
        /// selected incid. It is used to show how the latest OSMM translates.
        /// </summary>
        /// <value>
        /// The string of OSMM Habitat Secondaries related to the current incid.
        /// </value>
        public string IncidOSMMHabitatSecondaries
        {
            get
            {
                // If there are no OSMM proposed updates or the proposed update
                // for this incid has already been processed then return null.
                if (_osmmUpdatesEmpty == true || _incidOSMMUpdatesOSMMXref <= 0) return null;

                // Return the first proposed update secondary codes for this incid
                // (there should only be one).
                var q = from ohx in _lutOsmmHabitatXref
                        where ohx.osmm_xref_id == _incidOSMMUpdatesOSMMXref
                        select ohx.habitat_secondaries;

                if (q.Any())
                    return q.ElementAt(0);
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the OSMM XRef ID that relates to the selected Incid.
        /// It shows the cross-reference ID of the latest OSMM and
        /// how it translates directly to the primary and secondary
        /// habitats.
        /// </summary>
        /// <value>
        /// The string of OSMM XRef ID related to the current incid.
        /// </value>
        public string IncidOSMMXRefID
        {
            get
            {
                if (_incidOSMMUpdatesOSMMXref != 0)
                    return _incidOSMMUpdatesOSMMXref.ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the OSMM update flag that relates to the selected incid.
        /// It is used to show the update status of the latest OSMM
        /// translation.
        /// </summary>
        /// <value>
        /// The string interpretation of Update Flag related to the
        /// current incid.
        /// </value>
        public string IncidOSMMUpdateStatus
        {
            get
            {
                if (_incidOSMMUpdatesStatus != null)
                {
                    // Values greater than zero indicate proposed changes
                    if (_incidOSMMUpdatesStatus > 0)
                        return "Proposed";
                    else
                    {
                        return _incidOSMMUpdatesStatus switch
                        {
                            0 => "Pending",
                            -1 => "Applied",
                            -2 => "Ignored",
                            -99 => "Rejected",
                            _ => null
                        };
                    }
                }
                else
                    return null;
            }
        }

        /// <summary>
        /// Indicates if the OSMM updates status should be reset when an incid
        /// is manually updated.
        /// </summary>
        /// <value>
        /// Reset the update status when an incid is manually updated.
        /// </value>
        public bool ResetOSMMUpdatesStatus
        {
            get { return _resetOSMMUpdatesStatus; }
            set { _resetOSMMUpdatesStatus = value; }
        }

        #endregion

        #region Reason and Process

        /// <summary>
        /// Only show the Reason and Process group if the data is editable
        /// and not in OSMM edit mode, otherwise collapse it.
        /// </summary>
        public Visibility ShowReasonProcessGroup
        {
            get
            {
                if (_editMode == true && _osmmUpdateMode == false)
                {
                    if (!_showingReasonProcessGroup)
                    {
                        _showingReasonProcessGroup = true;
                    }
                    return Visibility.Visible;
                }
                else
                {
                    if (_showingReasonProcessGroup)
                    {
                        _showingReasonProcessGroup = false;
                    }

                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        public HluDataSet.lut_reasonRow[] ReasonCodes
        {
            get
            {
                if (_reasonCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _reasonCodes = _lutReason.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _reasonCodes;
            }
            set { }
        }

        public string Reason
        {
            get
            {
                // Get the instance of the active layer ComboBox in the ribbon.
                if (_reasonComboBox == null)
                    _reasonComboBox = ReasonComboBox.GetInstance();

                if (_reasonComboBox.Reason != null)
                    _reason = _reasonComboBox.Reason;

                return _reason;

            }
            set { _reason = value; }
        }

        public HluDataSet.lut_processRow[] ProcessCodes
        {
            get
            {
                if (_processCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _processCodes = _lutProcess.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _processCodes;
            }
            set { }
        }

        public string Process
        {
            get
            {
                // Get the instance of the active layer ComboBox in the ribbon.
                if (_processComboBox == null)
                    _processComboBox = ProcessComboBox.GetInstance();

                if (_processComboBox.Process != null)
                    _process = _processComboBox.Process;

                return _process;

            }
            set { _process = value; }
        }

        #endregion

        #region Habitat Tab

        // Set the Habitat tab label from here so that validation can be done.
        // This will enable tooltips to be shown so that validation errors
        // in any fields in the tab can be highlighted by flagging the tab
        // label as in error.
        public string HabitatTabLabel
        {
            get { return "Habitats"; }
        }

        #region Habitat Class

        public string HabitatHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Source Habitat";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the list of all local habitat class codes that have at
        /// least one habitat type that is local.
        /// </summary>
        /// <value>
        /// A list of habitat class codes.
        /// </value>
        public HluDataSet.lut_habitat_classRow[] HabitatClassCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (_habitatClassCodes == null)
                {
                    // Set the static variable (used in the options window) for all
                    // local habitat classes with local habitat types.
                    HabitatClasses = (from c in _lutHabitatClass
                                      join t in _lutHabitatType on c.code equals t.habitat_class_code
                                      select c).Distinct().OrderBy(c => c.sort_order).ThenBy(c => c.description).ToArray();

                    // Set the habitat classes for all local habitat classes with
                    // local habitat types that relate to at least one primary
                    // habitat type that is local.
                    _habitatClassCodes = (from c in _lutHabitatClass
                                          join t in _lutHabitatType on c.code equals t.habitat_class_code
                                          join tp in _lutHabitatTypePrimary on t.code equals tp.code_habitat_type
                                          select c).Distinct().OrderBy(c => c.sort_order).ThenBy(c => c.description).ToArray();
                }

                return _habitatClassCodes;
            }
        }

        public static HluDataSet.lut_habitat_classRow[] HabitatClasses
        {
            get => _habitatClasses;
            set => _habitatClasses = value;
        }


        /// <summary>
        /// Gets or sets the habitat class which will then load the list
        /// of habitat types related to that class.
        /// </summary>
        /// <value>
        /// The habitat class.
        /// </value>
        public string HabitatClass
        {
            get
            {
                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Don't set the habitat class when there are no OSMM updates to process.
                //
                if (_habitatClass == null && _osmmUpdatesEmpty == false)
                    _habitatClass = _preferredHabitatClass;
                //---------------------------------------------------------------------
                return _habitatClass;
            }
            set
            {
                _habitatClass = value;

                if (!String.IsNullOrEmpty(_habitatClass))
                {
                    // Clear the habitat type and then reload the list of
                    // possible habitat types that relate to the selected
                    // habitat class.
                    _habitatType = null;
                    OnPropertyChanged(nameof(HabitatTypeCodes));

                    HabitatType = null;
                    OnPropertyChanged(nameof(HabitatType));
                }
                else
                {
                    _habitatTypeCodes = null;
                    OnPropertyChanged(nameof(HabitatTypeCodes));
                }

                if ((_habitatTypeCodes != null) && (_habitatTypeCodes.Length == 1))
                    OnPropertyChanged(nameof(HabitatType));
            }
        }

        /// <summary>
        /// Gets the list of all local habitat type codes related to
        /// the selected habitat class that have at least one
        /// cross reference to a primary habitat.
        /// </summary>
        /// <value>
        /// A list of habitat type codes.
        /// </value>
        public HluDataSet.lut_habitat_typeRow[] HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (!String.IsNullOrEmpty(_habitatClass))
                {
                    // Only load codes with a primary habitat type for
                    // the selected class.
                    _habitatTypeCodes = (from ht in _lutHabitatType
                                         join htp in _lutHabitatTypePrimary on ht.code equals htp.code_habitat_type
                                         where ht.habitat_class_code == _habitatClass
                                         select ht).Distinct().OrderBy(c => c.sort_order).ThenBy(c => c.description).ToArray();

                    return _habitatTypeCodes;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets or sets the habitat type which will then load the list
        /// of primary habitats related to that type.
        /// </summary>
        /// <value>
        /// The habitat type.
        /// </value>
        public string HabitatType
        {
            get { return _habitatType; }
            set
            {
                _habitatType = value;

                if (!String.IsNullOrEmpty(_habitatType))
                {
                    // Load all primary habitat codes where the primary habitat code
                    // and primary habitat category are both flagged as local and
                    // are related to the current habitat type.
                    _primaryCodes = (
                         from p in _lutPrimary
                         join pc in _lutPrimaryCategory on p.category equals pc.code // Needed to only include local categories.
                         from htp in _lutHabitatTypePrimary
                         where htp.code_habitat_type == _habitatType
                         && (p.code == htp.code_primary
                         || (htp.code_primary.EndsWith('*') && Regex.IsMatch(p.code, @"\A" + htp.code_primary.TrimEnd('*') + @"")))
                         select new CodeDescriptionBool
                         {
                             code = p.code,
                             description = p.description,
                             nvc_codes = p.nvc_codes,
                             preferred = htp.preferred
                         })
                        .OrderByDescending(x => x.preferred) // Preferred = true first.
                        .ThenBy(x => x.code) // Then sort by code.
                        .ToArray();

                    //_primaryCodes = new ObservableCollection<CodeDescriptionBool>(
                    //    (from p in _lutPrimary
                    //     join pc in _lutPrimaryCategory on p.category equals pc.code // Needed to only include local categories.
                    //     from htp in _lutHabitatTypePrimary
                    //     where htp.code_habitat_type == _habitatType
                    //     && (p.code == htp.code_primary
                    //     || (htp.code_primary.EndsWith('*') && Regex.IsMatch(p.code, @"\A" + htp.code_primary.TrimEnd('*') + @"")))
                    //     select new
                    //     {
                    //         p.code,
                    //         p.description,
                    //         p.nvc_codes,
                    //         htp.preferred
                    //     })
                    //    .GroupBy(x => x.code)
                    //    .Select(g => g.OrderByDescending(x => x.preferred).First()) // Prioritise those that are preferred.
                    //    .Select(x => new CodeDescriptionBool
                    //    {
                    //        code = x.code,
                    //        description = x.description,
                    //        nvc_codes = x.nvc_codes,
                    //        preferred = x.preferred
                    //    }).ToList());

                    // Load all secondary habitat codes where the habitat type
                    // has one of more mandatory codes.
                    IEnumerable<HluDataSet.lut_secondaryRow> secondaryCodesMandatory = (from hts in _lutHabitatTypeSecondary
                                                join s in _lutSecondary on hts.code_secondary equals s.code
                                                where hts.code_habitat_type == _habitatType
                                                && hts.mandatory == 1
                                                select s).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                    // Store the list of mandatory secondary codes.
                    _secondaryCodesMandatory = secondaryCodesMandatory.Select(hts => hts.code);
                    _habitatSecondariesMandatory = string.Join(",", _secondaryCodesMandatory);
                }
                else
                {
                    // Load all primary habitat codes where the primary habitat code
                    // and primary habitat category are both flagged as local.
                    _primaryCodes = (
                         from p in _lutPrimary
                                    join pc in _lutPrimaryCategory on p.category equals pc.code
                         select new CodeDescriptionBool
                         {
                             code = p.code,
                             description = p.description,
                             nvc_codes = p.nvc_codes,
                             preferred = false
                         }).ToArray();

                    // Clear the list of mandatory secondary codes.
                    _secondaryCodesMandatory = [];
                    _habitatSecondariesMandatory = null;
                }

                // Refresh the mandatory habitat secondaries and tips
                OnPropertyChanged(nameof(HabitatSecondariesMandatory));

                OnPropertyChanged(nameof(PrimaryCodes));
                OnPropertyChanged(nameof(PrimaryEnabled));
                //OnPropertyChanged(nameof(NvcCodes));
            }
        }

        /// <summary>
        /// Only show the mandatory habitat secondaries if required.
        /// </summary>
        public Visibility ShowHabitatSecondariesMandatory
        {
            get
            {
                // If the habitat secondary codes validation is warning or error.
                if (_habitatSecondaryCodeValidation > 0)
                    return Visibility.Visible;
                else
                    return Visibility.Hidden;
            }
            set { }
        }

        /// <summary>
        /// Gets the string of mandatory secondaries that are related to the
        /// selected habitat type. It is used as an aid to the user to help
        /// select the correct secondary habitats.
        /// </summary>
        /// <value>
        /// The string of suggested habitat secondaries related to the current
        /// habitat type.
        /// </value>
        public string HabitatSecondariesMandatory
        {
            get
            {
                return _habitatSecondariesMandatory;
            }
        }

        /// <summary>
        /// Only show the suggested habitat secondaries if the option is set, otherwise collapse it.
        /// </summary>
        public Visibility ShowHabitatSecondariesSuggested
        {
            get
            {
                // If should be showing the suggested habitat secondaries
                if (_showHabitatSecondariesSuggested)
                {
                    return Visibility.Visible;
                }
                else  // If shouldn't be showing suggested habitat secondaries
                {
                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        //TODO: Include optional secondary codes in the suggested list?
        /// <summary>
        /// Gets the string of suggested secondaries that are related to the
        /// selected habitat type and primary habitat. It is used as an aid
        /// to the user to help select the correct primary and secondary habitats.
        /// </summary>
        /// <value>
        /// The string of suggested habitat secondaries related to the current
        /// habitat type and primary habitat.
        /// </value>
        public string HabitatSecondariesSuggested
        {
            get
            {
                return _habitatSecondariesSuggested;
            }
        }

        /// <summary>
        /// Gets the string of habitat tips that are related to the
        /// selected habitat type. It is used as an aid to the user to help
        /// select the correct primary and secondary habitats.
        /// </summary>
        /// <value>
        /// The string of habitat tips related to the current habitat type.
        /// </value>
        public string HabitatTips
        {
            get
            {
                return _habitatTips;
            }
        }

        /// <summary>
        /// Only show the source habitat group if the option is set, otherwise collapse it.
        /// </summary>
        public Visibility ShowSourceHabitatGroup
        {
            get
            {
                // If should be showing the habitat categories
                if (_showSourceHabitatGroup)
                {
                    return Visibility.Visible;
                }
                else  // If shouldn't be showing habitat categories
                {
                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        #endregion

        #region Primary Habitat

        public string PrimaryHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Primary Habitat";
                else
                    return null;
            }
        }

        public bool PrimaryEnabled
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the primary codes.
        /// </summary>
        /// <value>
        /// The primary codes.
        /// </value>
        public IEnumerable<CodeDescriptionBool> PrimaryCodes
        {
            get
            {
                // Return the primary codes if already set, otherwise get the
                // current primary code.
                if (_primaryCodes != null)
                {
                    return _primaryCodes;
                }
                else if (!String.IsNullOrEmpty(IncidPrimary))
                {
                    // Load the primary habitat code (where the primary habitat code
                    // and primary habitat category are both flagged as local). There
                    // should be only one.
                    _primaryCodes = (
                         from p in _lutPrimary
                         join pc in _lutPrimaryCategory on p.category equals pc.code
                         where p.code == IncidPrimary
                         select new CodeDescriptionBool
                         {
                             code = p.code,
                             description = p.description,
                             nvc_codes = p.nvc_codes,
                             preferred = false
                         }).ToArray();

                    return _primaryCodes;
                }
                else
                {
                    return null;
                }
            }
        }

        public string IncidPrimary
        {
            get { return _incidPrimary; }
            set
            {
                if (IncidCurrentRow != null)
                {
                    //TODO: What does this actually do?
                    if (_pasting && (_primaryCodes == null || !_primaryCodes.Any(r => r.code == value)))
                    {
                        _pasting = false;
                    }

                    _incidPrimary = value;

                    //TODO: Why are these being set here? Commented out until figured out!
                    //_primaryCodes = (
                    //     from p in _lutPrimary
                    //     join pc in _lutPrimaryCategory on p.category equals pc.code // Needed to only include local categories.
                    //     from htp in _lutHabitatTypePrimary
                    //     where htp.code_habitat_type == _habitatType
                    //     && (p.code == htp.code_primary
                    //     || (htp.code_primary.EndsWith('*') && Regex.IsMatch(p.code, @"\A" + htp.code_primary.TrimEnd('*') + @"")))
                    //     select new CodeDescriptionBool
                    //     {
                    //         code = p.code,
                    //         description = p.description,
                    //         nvc_codes = p.nvc_codes,
                    //         preferred = htp.preferred
                    //     })
                    //    .OrderByDescending(x => x.preferred) // Preferred = true first.
                    //    .ThenBy(x => x.code) // Then sort by code.
                    //    .ToArray();

                    // Set the secondary habitat suggested and tips based on the current
                    // primary code and habitat type.
                    _habitatSecondariesSuggested = null;
                    _habitatTips = null;
                    if (_incidPrimary != null && _habitatType != null)
                    {
                        var q = (from htp in _lutHabitatTypePrimary
                                 where ((htp.code_habitat_type == _habitatType)
                                 && (htp.code_primary == _incidPrimary
                                 || (htp.code_primary.EndsWith('*') && Regex.IsMatch(_incidPrimary, @"\A" + htp.code_primary.TrimEnd('*') + @"") == true)))
                                 select htp);
                        if (q.Any())
                        {
                            //TODO: Add any optional secondary codes to the list of suggested secondary codes?
                            // Split the habitat_secondaries text into a list and combine it with
                            // any optional secondary codes.
                            _habitatSecondariesSuggested = q.ElementAt(0).habitat_secondaries;
                            _habitatTips = q.ElementAt(0).comments;
                        }
                    }

                    // Set the list of secondary codes for the primary habitat.
                    NewPrimaryHabitat(_incidPrimary);

                    // Refresh the suggested habitat secondaries and tips
                    OnPropertyChanged(nameof(HabitatSecondariesSuggested));
                    OnPropertyChanged(nameof(HabitatTips));

                    // Refresh the BAP habitat environments (in case secondary codes
                    // are, or should be, reflected).
                    GetBapEnvironments();
                    OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                    OnPropertyChanged(nameof(IncidBapHabitatsUser));
                    OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
                    OnPropertyChanged(nameof(BapHabitatsUserEnabled));

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        /// <summary>
        /// Actions when the primary code has been changed.
        /// </summary>
        /// <param name="incidPrimary">The incid primary.</param>
        private void NewPrimaryHabitat(string incidPrimary)
        {
            if (incidPrimary != null)
            {
                // Set the primary habitat category.
                _incidPrimaryCategory = _lutPrimary.Where(p => p.code == incidPrimary).ElementAt(0).category;

                // Set NVC codes based on current primary habitat
                _incidNVCCodes = null;
                if (_primaryCodes != null)
                {
                    var q = _primaryCodes.Where(h => h.code == _incidPrimary);
                    if (q.Any())
                        _incidNVCCodes = q.ElementAt(0).nvc_codes;
                }

                // Store all secondary habitat codes that are flagged as local for
                // all secondary groups that relate to the primary habitat category.
                _secondaryCodesValid = (from s in SecondaryHabitatCodesAll
                                        join ps in _lutPrimarySecondary on s.code equals ps.code_secondary
                                        where ((ps.code_primary == _incidPrimary) || (ps.code_primary.EndsWith('*') && Regex.IsMatch(_incidPrimary, @"\A" + ps.code_primary.TrimEnd('*') + @"") == true))
                                        select s).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                // Store the list of valid secondary codes.
                SecondaryHabitat.ValidSecondaryCodes = _secondaryCodesValid.Select(s => s.code);
            }
            else
            {
                _incidPrimaryCategory = null;
                _incidNVCCodes = null;
                _secondaryCodesValid = null;

                // Clear the list of valid secondary codes.
                SecondaryHabitat.ValidSecondaryCodes = null;
            }

            // Refresh the related fields
            OnPropertyChanged(nameof(NvcCodes));

            OnPropertyChanged(nameof(SecondaryGroupCodes));
            _secondaryGroup = _preferredSecondaryGroup;
            OnPropertyChanged(nameof(SecondaryGroup));
            OnPropertyChanged(nameof(SecondaryHabitatCodes));

            OnPropertyChanged(nameof(SecondaryGroupEnabled));
            OnPropertyChanged(nameof(SecondaryHabitatEnabled));

            OnPropertyChanged(nameof(CanAddSecondaryHabitat));
            OnPropertyChanged(nameof(CanAddSecondaryHabitatList));

            // Refresh secondary table to re-trigger the validation.
            RefreshSecondaryHabitats();
        }

        public string IncidPrimaryCategory
        {
            get { return _incidPrimaryCategory; }
        }

        /// <summary>
        /// Only show the NVC Codes if the option is set, otherwise collapse it.
        /// </summary>
        public Visibility ShowNVCCodes
        {
            get
            {
                // If should be showing NVC codes
                if (_showNVCCodes)
                {
                    return Visibility.Visible;
                }
                else  // If shouldn't be showing NVC codes
                {
                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        /// <summary>
        /// Gets the string of NVC codes that are related to the selected
        /// primary habitat. It is used as an aid to the user to help double-
        /// check they have selected the correct primary habitat.
        /// </summary>
        /// <value>
        /// The string of NVC codes related to the current primary habitat.
        /// </value>
        public string NvcCodes
        {
            get
            {
                return _incidNVCCodes;
            }
        }

        #endregion

        #region Secondary Habitats

        public string SecondaryHabitatsHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Secondary Habitats";
                else
                    return null;
            }
        }

        public bool SecondaryGroupEnabled
        {
            get
            {
                return (!String.IsNullOrEmpty(_incidPrimary));
            }
        }

        public bool SecondaryHabitatEnabled
        {
            get
            {
                return (!String.IsNullOrEmpty(_incidPrimary) && !String.IsNullOrEmpty(_secondaryGroup));
            }
        }

        public bool SecondaryHabitatsEnabled
        {
            get
            {
                return true;
            }
        }

        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodes
        {
            get
            {
                // Define the <ALL> group row
                HluDataSet.lut_secondary_groupRow allRow = HluDataset.lut_secondary_group.Newlut_secondary_groupRow();
                allRow.code = "<All>";
                allRow.description = "<All>";
                allRow.sort_order = -1;

                // Define the <ALL Essentials> group row
                HluDataSet.lut_secondary_groupRow allEssRow = HluDataset.lut_secondary_group.Newlut_secondary_groupRow();
                allEssRow.code = "<All Essentials>";
                allEssRow.description = "<All Essentials>";
                allEssRow.sort_order = -1;

                // Set the public and static variables
                if (SecondaryGroupsAll == null || SecondaryGroupsAll.Length == 0)
                {
                    // Set the full list of local secondary groups.
                    _secondaryGroups = (from sg in _lutSecondaryGroup
                                        select sg).OrderBy(r => r.sort_order).ThenBy(r => r.description).Distinct().ToArray();

                    // Set the full list of secondary groups including any <All> groups.
                    HluDataSet.lut_secondary_groupRow[] secondaryGroupsAll;
                    secondaryGroupsAll = _secondaryGroups;
                    if (secondaryGroupsAll != null)
                    {
                        // Add the <ALL> groups containing all and all essential secondary codes.
                        secondaryGroupsAll = new HluDataSet.lut_secondary_groupRow[] { allRow, allEssRow }.Concat(secondaryGroupsAll).ToArray();
                    }

                    // Set the static variable
                    SecondaryGroupsAll = secondaryGroupsAll;

                    // Set the dictionary of local secondary group codes.
                    SecondaryHabitat.SecondaryGroupCodes = (from sg in _lutSecondary
                                                            select sg).OrderBy(r => r.code).ThenBy(r => r.code_group).ToDictionary(r => r.code, r => r.code_group);
                }

                if (!String.IsNullOrEmpty(IncidPrimary))
                {
                    // Set the valid list of secondary groups for the primary code.
                    _secondaryGroupsValid = (from sg in _lutSecondaryGroup
                                             join s in _lutSecondary on sg.code equals s.code_group
                                             join ps in _lutPrimarySecondary on s.code equals ps.code_secondary
                                             where ((ps.code_primary == IncidPrimary) || (ps.code_primary.EndsWith('*') && Regex.IsMatch(IncidPrimary, @"\A" + ps.code_primary.TrimEnd('*') + @"") == true))
                                             select sg).OrderBy(r => r.sort_order).ThenBy(r => r.description).Distinct().ToArray();

                    if (_secondaryGroupsValid != null)
                    {
                        // Add the <ALL> groups containing all and all essential secondary codes.
                        _secondaryGroupsValid = new HluDataSet.lut_secondary_groupRow[] { allRow, allEssRow }.Concat(_secondaryGroupsValid).ToArray();
                    }
                }
                else
                {
                    // Set the valid list of secondary codes to all codes (rather than clearing the list)
                    _secondaryGroupsValid = _secondaryGroups;

                    // Add the <ALL> groups containing all and all essential secondary codes.
                    _secondaryGroupsValid = new HluDataSet.lut_secondary_groupRow[] { allRow, allEssRow }.Concat(_secondaryGroupsValid).ToArray();

                    // Set the combo box list to null (it will also be disabled).
                    return null;
                }

                return _secondaryGroupsValid;
            }
        }

        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodesAll
        {
            get
            {
                // Set the public and static variables
                if (_secondaryGroups == null || _secondaryGroups.Length == 0)
                {
                    // Set the full list of local secondary groups.
                    _secondaryGroups = (from sg in _lutSecondaryGroup
                                        select sg).OrderBy(r => r.sort_order).ThenBy(r => r.description).Distinct().ToArray();
                }

                return _secondaryGroups;
            }
        }

        public static HluDataSet.lut_secondary_groupRow[] SecondaryGroupsAll
        {
            get => _secondaryGroupsAll;
            set => _secondaryGroupsAll = value;
        }

        /// <summary>
        /// Gets or sets the secondary group which will then load the list
        /// of secondary habitats related to that group.
        /// </summary>
        /// <value>
        /// The secondary group.
        /// </value>
        public string SecondaryGroup
        {
            get { return _secondaryGroup; }
            set
            {
                _secondaryGroup = value;
                OnPropertyChanged(nameof(SecondaryHabitatEnabled));
                OnPropertyChanged(nameof(SecondaryHabitatCodes));
                OnPropertyChanged(nameof(CanAddSecondaryHabitat));
            }
        }

        public HluDataSet.lut_secondaryRow[] SecondaryHabitatCodes
        {
            get
            {
                if (!String.IsNullOrEmpty(_incidPrimary) && !String.IsNullOrEmpty(_secondaryGroup))
                {
                    // If the primary/secondary codes must be valid
                    if (_primarySecondaryCodeValidation > 0)
                    {
                        if (_secondaryGroup == "<All>")
                        {
                            // Load all secondary habitat codes that are flagged as local for
                            // all secondary groups that relate to the primary habitat.
                            return _secondaryCodesValid;
                        }
                        else if (_secondaryGroup == "<All Essentials>")
                        {
                            // Load all secondary habitat codes that are flagged as local for
                            // all secondary groups that relate to the primary habitat and
                            // are essential.
                            return _secondaryCodesValid.Where(s => s.sort_order < 100).ToArray();
                        }
                        else
                        {
                            // Load all secondary habitat codes that are flagged as local and
                            // relate to the primary habitat and selected secondary group.
                            return _secondaryCodesValid.Where(s => s.code_group == _secondaryGroup).ToArray();
                        }
                    }
                    else
                    {
                        if (_secondaryGroup == "<All>")
                        {
                            // Load all secondary habitat codes that are flagged as local
                            // regardless of the primary habitat.
                            return _secondaryCodesAll;
                        }
                        else if (_secondaryGroup == "<All Essentials>")
                        {
                            // Load all secondary habitat codes that are flagged as local
                            // regardless of the primary habitat and are essential.
                            return _secondaryCodesAll.Where(s => s.sort_order < 100).ToArray();
                        }
                        else
                        {
                            // Load all secondary habitat codes that are flagged as local
                            // regardless of the primary habitat but related to the
                            // selected secondary group.
                            return _secondaryCodesAll.Where(s => s.code_group == _secondaryGroup).ToArray();
                        }
                    }
                }
                else
                {
                    // Set the combo box list to null (it will also be disabled).
                    return null;
                }
            }
        }

        public HluDataSet.lut_secondaryRow[] SecondaryHabitatCodesAll
        {
            get
            {
                if (_secondaryCodesAll == null)
                {
                    _secondaryCodesAll = (from s in _lutSecondary
                                          select s).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _secondaryCodesAll;
            }
        }

        /// <summary>
        /// Gets or sets the secondary habitat.
        /// </summary>
        /// <value>
        /// The secondary habitat.
        /// </value>
        public string SecondaryHabitatCode
        {
            get { return _secondaryHabitat; }
            set
            {
                _secondaryHabitat = value;
                OnPropertyChanged(nameof(CanAddSecondaryHabitat));
            }
        }

        /// <summary>
        /// The collection of secondary habitats.
        /// </summary>
        public ObservableCollection<SecondaryHabitat> IncidSecondaryHabitats
        {
            get { return _incidSecondaryHabitats; }
            set
            {
                _incidSecondaryHabitats = value;

                // Set the new list of secondary habitat rows for the class.
                SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

                // Refresh the secondary habitat table (as they have been pasted).
                RefreshSecondaryHabitats();
                //OnPropertyChanged(nameof(IncidSecondarySummary));   // Doesn't seem to be needed.

                // Refresh the BAP habitat environments (in case secondary codes
                // are, or should be, reflected).
                GetBapEnvironments();
                OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                OnPropertyChanged(nameof(IncidBapHabitatsUser));
                OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
                OnPropertyChanged(nameof(BapHabitatsUserEnabled));

                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string HabitatSummaryHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Secondary Habitats Summary";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the concatenated secondary habitats summary.
        /// </summary>
        /// <value>
        /// The incid secondary summary.
        /// </value>
        public string IncidSecondarySummary
        {
            get
            {
                _incidSecondarySummary = String.Join(_secondaryCodeDelimiter, _incidSecondaryHabitats
                    .OrderBy(s => s.secondary_habitat_int)
                    .ThenBy(s => s.secondary_habitat)
                    .Select(s => s.secondary_habitat)
                    .Distinct().ToList());
                return _incidSecondarySummary == String.Empty ? null : _incidSecondarySummary;
            }
        }

        /// <summary>
        /// Only show the contatenated habitat summary if the option is set, otherwise collapse it.
        /// </summary>
        public Visibility ShowHabitatSummary
        {
            get
            {
                // If should be showing the concatenated habitat summary
                if (_showHabitatSummary)
                {
                    return Visibility.Visible;
                }
                else  // If shouldn't be showing concatenated habitat summary
                {
                    return Visibility.Collapsed;
                }
            }
            set { }
        }

        /// <summary>
        /// Gets the secondary habitats.
        /// </summary>
        public void GetSecondaryHabitats()
        {
            // Remove any existing handlers before assigning a new collection.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

            // Identify any secondary habitat rows that have not been marked as deleted.
            IEnumerable<HluDataSet.incid_secondaryRow> incidSecondaryRowsUndel =
                _incidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted);

            // If there are any rows not marked as deleted add them to the collection.
            if (incidSecondaryRowsUndel != null)
            {
                // Order the secondary codes as required
                _incidSecondaryHabitats = _secondaryCodeOrder switch
                {
                    "As entered" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_id).Select(r => new SecondaryHabitat(_bulkUpdateMode == true, r))),
                    "By group then code" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_group).ThenBy(r => r.secondary).Select(r => new SecondaryHabitat(_bulkUpdateMode == true, r))),
                    "By code" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary).Select(r => new SecondaryHabitat(_bulkUpdateMode == true, r))),
                    _ => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_id).Select(r => new SecondaryHabitat(_bulkUpdateMode == true, r)))
                };
            }
            else
            {
                // Otherwise there can't be any secondary habitat rows so
                // set a new collection.
                _incidSecondaryHabitats = [];
            }

            // Track any changes to the user rows collection.
            _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

            // Set the new list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Set the validation option in the secondary habitat environment.
            SecondaryHabitat.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation;

            // Check if there are any errors in the secondary habitat records to see
            // if the Habitats tab label should be flagged as also in error.
            if (_incidSecondaryHabitats != null && _incidSecondaryHabitats.Count > 0)
            {
                int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _habitatErrors, "SecondaryHabitat");
                else
                    DelErrorList(ref _habitatErrors, "SecondaryHabitat");
            }
            else
                DelErrorList(ref _habitatErrors, "SecondaryHabitat");

            OnPropertyChanged(nameof(IncidSecondaryHabitats));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory));
            OnPropertyChanged(nameof(HabitatTabLabel));
        }

        /// <summary>
        /// Add a secondary habitat.
        /// </summary>
        public bool AddSecondaryHabitat(bool bulkUpdateMode, int secondary_id, string incid, string secondary_habitat, string secondary_group)
        {
            // Store old secondary habitats list
            ObservableCollection<SecondaryHabitat> oldSecondaryHabs = _incidSecondaryHabitats;

            // Remove any existing handlers before assigning a new collection.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

            // If there are any existing rows add the new row the collection
            // and then sort them.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.Add(new SecondaryHabitat(bulkUpdateMode, -1, Incid, secondary_habitat, secondary_group));
            else
            {
                // Otherwise there can't be any secondary habitat rows so
                // just create a new collection and add the new row.
                _incidSecondaryHabitats = [new SecondaryHabitat(bulkUpdateMode, -1, Incid, secondary_habitat, secondary_group)];
            }

            // Track any changes to the user rows collection.
            _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

            // Set the new list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Track when the secondary habitat records have changed so that the apply
            // button will appear.
            Changed = true;

            return (_incidSecondaryHabitats == null || (oldSecondaryHabs != null && _incidSecondaryHabitats != oldSecondaryHabs));
        }

        /// <summary>
        /// Refresh the secondary habitat table.
        /// </summary>
        public void RefreshSecondaryHabitats()
        {
            // If there are any existing rows then (re)sort them.
            if (_incidSecondaryHabitats != null)
            {
                // Remove any existing handlers before assigning a new collection.
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

                // Order the secondary codes as required
                _incidSecondaryHabitats = _secondaryCodeOrder switch
                {
                    "As entered" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_id)),
                    "By group then code" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_group).ThenBy(r => r.secondary_habitat_int)),
                    "By code" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_habitat_int)),
                    _ => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_id))
                };

                // Track any changes to the user rows collection.
                _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

                // Check if there are any errors in the secondary habitat records to see
                // if the Habitats tab label should be flagged as also in error.
                if (_incidSecondaryHabitats.Count > 0)
                {
                    int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                    if (countInvalid > 0)
                        AddErrorList(ref _habitatErrors, "SecondaryHabitat");
                    else
                        DelErrorList(ref _habitatErrors, "SecondaryHabitat");
                }
                else
                    DelErrorList(ref _habitatErrors, "SecondaryHabitat");

                OnPropertyChanged(nameof(IncidSecondaryHabitats));
                OnPropertyChanged(nameof(HabitatSecondariesMandatory));
                OnPropertyChanged(nameof(HabitatTabLabel));
            }
        }

        private void _incidSecondaryHabitats_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Error));

            // Track when the secondary habitat records have changed so that the apply
            // button will appear.
            Changed = true;

            // Check if there are any errors in the secondary habitat records to see
            // if the Habitats tab label should be flagged as also in error.
            if (_incidSecondaryHabitats != null && _incidSecondaryHabitats.Count > 0)
            {
                int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _habitatErrors, "SecondaryHabitat");
                else
                    DelErrorList(ref _habitatErrors, "SecondaryHabitat");
            }
            else
            {
                DelErrorList(ref _habitatErrors, "SecondaryHabitat");
            }

            // Update the list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Refresh secondary table and summary.
            RefreshSecondaryHabitats();
            OnPropertyChanged(nameof(IncidSecondarySummary));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory));

            // Refresh the BAP habitat environments (in case secondary codes
            // are, or should be, reflected).
            GetBapEnvironments();
            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
            OnPropertyChanged(nameof(IncidBapHabitatsUser));
            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));

            OnPropertyChanged(nameof(HabitatTabLabel));
        }

        #endregion

        #region Legacy

        public string LegacyHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Legacy";
                else
                    return null;
            }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR44 (Editable Legacy Habitat field)
        //
        /// <summary>
        /// Looks up the legacy habitat codes and descriptions from
        /// the database table 'lut_legacy_habitat'.
        /// </summary>
        /// <value>
        /// The sorted rows of all Legacy Habitats from the database.
        /// </value>
        public HluDataSet.lut_legacy_habitatRow[] LegacyHabitatCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (_legacyHabitatCodes == null)
                {
                    _legacyHabitatCodes = _lutLegacyHabitat.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                // Return the list of legacy codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidLegacyHabitat))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_legacy_habitatRow clearRow = HluDataset.lut_legacy_habitat.Newlut_legacy_habitatRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_legacy_habitatRow[] { clearRow }.Concat(_legacyHabitatCodes).ToArray();
                }
                else
                {
                    return _legacyHabitatCodes;
                }
            }
        }
        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        // CHANGED: CR44 (Editable Legacy Habitat field)
        //
        /// <summary>
        /// Gets the Legacy Habitat code for the current incid.
        /// </summary>
        /// <value>
        /// The Legacy Habitat code for the current incid.
        /// </value>
        public string IncidLegacyHabitat
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.legacy_habitatColumn))
                    _incidLegacyHabitat = IncidCurrentRow.legacy_habitat;
                else
                    _incidLegacyHabitat = null;

                return _incidLegacyHabitat;
            }
            set
            {
                if (IncidCurrentRow != null)
                {
                    bool clearCode = value == _codeDeleteRow;
                    bool newCode = false;
                    if (clearCode)
                        value = null;
                    else
                        newCode = ((String.IsNullOrEmpty(_incidLegacyHabitat)) && (!String.IsNullOrEmpty(value)));

                    _incidLegacyHabitat = value;
                    IncidCurrentRow.legacy_habitat = _incidLegacyHabitat;

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;

                    // Refresh legacy habitat list
                    if (clearCode || newCode)
                        OnPropertyChanged(nameof(LegacyHabitatCodes));
                }
            }
        }
        //---------------------------------------------------------------------

        #endregion

        #endregion

        #region IHS Tab

        /// <summary>
        /// Gets the IHS tab label.
        /// </summary>
        /// <value>
        /// The IHS tab label.
        /// </value>
        public string IHSTabLabel
        {
            get { return "IHS"; }
        }

        /// <summary>
        /// Show or hide the IHS tab.
        /// </summary>
        /// <value>
        /// The visibility of the IHS tab.
        /// </value>
        public Visibility ShowIHSTab
        {
            get
            {
                if ((bool)_showIHSTab)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
        }

        #region IHS Habitat

        /// <summary>
        /// Gets the IHS habitat group header.
        /// </summary>
        /// <value>
        /// The IHS habitat header.
        /// </value>
        public string IhsHabitatHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "IHS Habitat";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the incid IHS habitat.
        /// </summary>
        /// <value>
        /// The incid IHS habitat.
        /// </value>
        public string IncidIhsHabitat
        {
            get { return _incidIhsHabitat; }
        }

        /// <summary>
        /// Gets the incid ihs habitat code and description.
        /// </summary>
        /// <value>
        /// The incid ihs habitat code and description.
        /// </value>
        public string IncidIhsHabitatText
        {
            get
            {
                // Return the concatenated habitat code and description.
                if (_incidIhsHabitat == null)
                    return null;
                else
                {
                    var q = _lutIhsHabitat.Where(h => h.code == _incidIhsHabitat);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        #endregion

        #region IHS Matrix

        /// <summary>
        /// Gets the ihs matrix group header.
        /// </summary>
        /// <value>
        /// The ihs matrix group header.
        /// </value>
        public string IhsMatrixHeader
        {
            get
            {
                // Optionally hide group headers to reduce window height.
                if ((bool)_showGroupHeaders)
                    return "IHS Matrix";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first matrix code for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs matrix value.
        /// </value>
        public string IncidIhsMatrix1
        {
            get
            {
                if (!CheckIhsMatrix()) return null;
                if ((_incidIhsMatrixRows.Length > 0) && (_incidIhsMatrixRows[0] != null) &&
                    !_incidIhsMatrixRows[0].IsNull(_hluDS.incid_ihs_matrix.matrixColumn.Ordinal))
                    return _incidIhsMatrixRows[0].matrix;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first combined matrix code and test for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs matrix text.
        /// </value>
        public string IncidIhsMatrix1Text
        {
            get
            {
                // Return the combined code and text value
                if (IncidIhsMatrix1 == null)
                    return null;
                else
                {
                    var q = _lutIhsMatrix.Where(m => m.code == _incidIhsMatrixRows[0].matrix);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Gets the second matrix code for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs matrix value.
        /// </value>
        public string IncidIhsMatrix2
        {
            get
            {
                if (!CheckIhsMatrix()) return null;
                if ((_incidIhsMatrixRows.Length > 1) && (_incidIhsMatrixRows[1] != null) &&
                    !_incidIhsMatrixRows[1].IsNull(_hluDS.incid_ihs_matrix.matrixColumn.Ordinal))
                    return _incidIhsMatrixRows[1].matrix;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the second combined matrix code and test for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs matrix text.
        /// </value>
        public string IncidIhsMatrix2Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsMatrix2 == null)
                    return null;
                else
                {
                    var q = _lutIhsMatrix.Where(m => m.code == _incidIhsMatrixRows[1].matrix);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Gets the third matrix code for this incid.
        /// </summary>
        /// <value>
        /// The third incid ihs matrix value.
        /// </value>
        public string IncidIhsMatrix3
        {
            get
            {
                if (!CheckIhsMatrix()) return null;
                if ((_incidIhsMatrixRows.Length > 2) && (_incidIhsMatrixRows[2] != null) &&
                    !_incidIhsMatrixRows[2].IsNull(_hluDS.incid_ihs_matrix.matrixColumn.Ordinal))
                    return _incidIhsMatrixRows[2].matrix;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the third combined matrix code and test for this incid.
        /// </summary>
        /// <value>
        /// The third incid ihs matrix text.
        /// </value>
        public string IncidIhsMatrix3Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsMatrix3 == null)
                    return null;
                else
                {
                    var q = _lutIhsMatrix.Where(m => m.code == _incidIhsMatrixRows[2].matrix);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs matrix rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsMatrix()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidIhsMatrixRows == null)
            {
                HluDataSet.incid_ihs_matrixDataTable ihsMatrixTable = _hluDS.incid_ihs_matrix;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_matrixTableAdapter, ref ihsMatrixTable);
            }

            return _incidIhsMatrixRows != null;
            //return true;
        }

        /// <summary>
        /// Removes the incid ihs matrix rows.
        /// </summary>
        public void RemoveIncidIhsMatrixRows()
        {
            // Check if there are any valid ihs matrix rows.
            if (CheckIhsMatrix())
            {
                for (int i = 0; i < _incidIhsMatrixRows.Length; i++)
                {
                    if (_incidIhsMatrixRows[i].RowState != DataRowState.Detached)
                        _incidIhsMatrixRows[i].Delete();
                    _incidIhsMatrixRows[i] = null;
                }
            }
        }

        #endregion

        #region IHS Formation

        /// <summary>
        /// Gets the ihs formation group header.
        /// </summary>
        /// <value>
        /// The ihs formation group header.
        /// </value>
        public string IhsFormationHeader
        {
            get
            {
                // Optionally hide group headers to reduce window height.
                if ((bool)_showGroupHeaders)
                    return "IHS Formation";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first formation code for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs formation value.
        /// </value>
        public string IncidIhsFormation1
        {
            get
            {
                if (!CheckIhsFormation()) return null;
                if ((_incidIhsFormationRows.Length > 0) && (_incidIhsFormationRows[0] != null) &&
                    !_incidIhsFormationRows[0].IsNull(_hluDS.incid_ihs_formation.formationColumn.Ordinal))
                    return _incidIhsFormationRows[0].formation;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first combined formation code and test for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs formation text.
        /// </value>
        public string IncidIhsFormation1Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsFormation1 == null)
                    return null;
                else
                {
                    var q = _lutIhsFormation.Where(m => m.code == _incidIhsFormationRows[0].formation);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Gets the second formation code for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs formation value.
        /// </value>
        public string IncidIhsFormation2
        {
            get
            {
                if (!CheckIhsFormation()) return null;
                if ((_incidIhsFormationRows.Length > 1) && (_incidIhsFormationRows[1] != null) &&
                    !_incidIhsFormationRows[1].IsNull(_hluDS.incid_ihs_formation.formationColumn.Ordinal))
                    return _incidIhsFormationRows[1].formation;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the second combined formation code and test for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs formation text.
        /// </value>
        public string IncidIhsFormation2Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsFormation2 == null)
                    return null;
                else
                {
                    var q = _lutIhsFormation.Where(m => m.code == _incidIhsFormationRows[1].formation);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs formation rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsFormation()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidIhsFormationRows == null)
            {
                HluDataSet.incid_ihs_formationDataTable ihsFormationTable = _hluDS.incid_ihs_formation;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_formationTableAdapter, ref ihsFormationTable);
            }
            return _incidIhsFormationRows != null;
        }

        /// <summary>
        /// Removes the incid ihs formation rows.
        /// </summary>
        public void RemoveIncidIhsFormationRows()
        {
            // Check if there are any valid ihs formation rows.
            if (CheckIhsFormation())
            {
                for (int i = 0; i < _incidIhsFormationRows.Length; i++)
                {
                    if (_incidIhsFormationRows[i].RowState != DataRowState.Detached)
                        _incidIhsFormationRows[i].Delete();
                    _incidIhsFormationRows[i] = null;
                }
            }
        }

        #endregion

        #region IHS Management

        /// <summary>
        /// Gets the ihs management group header.
        /// </summary>
        /// <value>
        /// The ihs management group header.
        /// </value>
        public string IhsManagementHeader
        {
            get
            {
                // Optionally hide group headers to reduce window height.
                if ((bool)_showGroupHeaders)
                    return "IHS Management";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first management code for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs management value.
        /// </value>
        public string IncidIhsManagement1
        {
            get
            {
                if (!CheckIhsManagement()) return null;
                if ((_incidIhsManagementRows.Length > 0) && (_incidIhsManagementRows[0] != null) &&
                    !_incidIhsManagementRows[0].IsNull(_hluDS.incid_ihs_management.managementColumn.Ordinal))
                    return _incidIhsManagementRows[0].management;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first combined management code and test for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs management text.
        /// </value>
        public string IncidIhsManagement1Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsManagement1 == null)
                    return null;
                else
                {
                    var q = _lutIhsManagement.Where(m => m.code == _incidIhsManagementRows[0].management);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Gets the second management code for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs management value.
        /// </value>
        public string IncidIhsManagement2
        {
            get
            {
                if (!CheckIhsManagement()) return null;
                if ((_incidIhsManagementRows.Length > 1) && (_incidIhsManagementRows[1] != null) &&
                    !_incidIhsManagementRows[1].IsNull(_hluDS.incid_ihs_management.managementColumn.Ordinal))
                    return _incidIhsManagementRows[1].management;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the second combined management code and test for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs management text.
        /// </value>
        public string IncidIhsManagement2Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsManagement2 == null)
                    return null;
                else
                {
                    var q = _lutIhsManagement.Where(m => m.code == _incidIhsManagementRows[1].management);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs management rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsManagement()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidIhsManagementRows == null)
            {
                HluDataSet.incid_ihs_managementDataTable ihsManagementTable = _hluDS.incid_ihs_management;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_managementTableAdapter, ref ihsManagementTable);
            }

            return _incidIhsManagementRows != null;
        }

        /// <summary>
        /// Removes the incid ihs management rows.
        /// </summary>
        public void RemoveIncidIhsManagementRows()
        {
            // Check if there are any valid ihs management rows.
            if (CheckIhsManagement())
            {
                for (int i = 0; i < _incidIhsManagementRows.Length; i++)
                {
                    if (_incidIhsManagementRows[i].RowState != DataRowState.Detached)
                        _incidIhsManagementRows[i].Delete();
                    _incidIhsManagementRows[i] = null;
                }
            }
        }

        #endregion

        #region IHS Complex

        /// <summary>
        /// Gets the ihs complex group header.
        /// </summary>
        /// <value>
        /// The ihs complex group header.
        /// </value>
        public string IhsComplexHeader
        {
            get
            {
                // Optionally hide group headers to reduce window height.
                if ((bool)_showGroupHeaders)
                    return "IHS Complex";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first complex code for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs complex value.
        /// </value>
        public string IncidIhsComplex1
        {
            get
            {
                if (!CheckIhsComplex()) return null;
                if ((_incidIhsComplexRows.Length > 0) && (_incidIhsComplexRows[0] != null) &&
                    !_incidIhsComplexRows[0].IsNull(_hluDS.incid_ihs_complex.complexColumn.Ordinal))
                    return _incidIhsComplexRows[0].complex;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the first combined complex code and test for this incid.
        /// </summary>
        /// <value>
        /// The first incid ihs complex text.
        /// </value>
        public string IncidIhsComplex1Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsComplex1 == null)
                    return null;
                else
                {
                    var q = _lutIhsComplex.Where(m => m.code == _incidIhsComplexRows[0].complex);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Gets the second complex code for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs complex value.
        /// </value>
        public string IncidIhsComplex2
        {
            get
            {
                if (!CheckIhsComplex()) return null;
                if ((_incidIhsComplexRows.Length > 1) && (_incidIhsComplexRows[1] != null) &&
                    !_incidIhsComplexRows[1].IsNull(_hluDS.incid_ihs_complex.complexColumn.Ordinal))
                    return _incidIhsComplexRows[1].complex;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the second combined complex code and test for this incid.
        /// </summary>
        /// <value>
        /// The second incid ihs complex text.
        /// </value>
        public string IncidIhsComplex2Text
        {
            get
            {
                // Return the combined code and text value.
                if (IncidIhsComplex2 == null)
                    return null;
                else
                {
                    var q = _lutIhsComplex.Where(m => m.code == _incidIhsComplexRows[1].complex);
                    if (q.Any())
                        return String.Concat(q.ElementAt(0).code, " : ", q.ElementAt(0).description);
                    else
                        return null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs complex rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsComplex()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidIhsComplexRows == null)
            {
                HluDataSet.incid_ihs_complexDataTable ihsComplexTable = _hluDS.incid_ihs_complex;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_complexTableAdapter, ref ihsComplexTable);
            }

            return _incidIhsManagementRows != null;
        }

        /// <summary>
        /// Removes the incid ihs complex rows.
        /// </summary>
        public void RemoveIncidIhsComplexRows()
        {
            // Check if there are any valid ihs complex rows.
            if (CheckIhsComplex())
            {
                for (int i = 0; i < _incidIhsComplexRows.Length; i++)
                {
                    if (_incidIhsComplexRows[i].RowState != DataRowState.Detached)
                        _incidIhsComplexRows[i].Delete();
                    _incidIhsComplexRows[i] = null;
                }
            }
        }

        #endregion

        #region IHS Summary

        /// <summary>
        /// Gets the ihs summary group header.
        /// </summary>
        /// <value>
        /// The ihs summary group header.
        /// </value>
        public string IhsSummaryHeader
        {
            get
            {
                // Optionally hide group headers to reduce window height.
                if ((bool)_showGroupHeaders)
                    return "IHS Summary";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the concatenated incid ihs summary.
        /// </summary>
        /// <value>
        /// The concatenated incid ihs summary.
        /// </value>
        public string IncidIhsSummary
        {
            get
            {
                return ViewModelWindowMainHelpers.IhsSummary([
                    IncidIhsHabitat,
                    IncidIhsMatrix1,
                    IncidIhsMatrix2,
                    IncidIhsMatrix3,
                    IncidIhsFormation1,
                    IncidIhsFormation2,
                    IncidIhsManagement1,
                    IncidIhsManagement2,
                    IncidIhsComplex1,
                    IncidIhsComplex2 ]);
            }
        }

        #endregion

        #endregion

        #region Priority Tab

        /// <summary>
        /// Gets the Priority tab label.
        /// </summary>
        /// <value>
        /// The Priority tab label.
        /// </value>
        public string PriorityTabLabel
        {
            get { return "Priority"; }
        }
        //---------------------------------------------------------------------

        #region Priority Habitat

        /// <summary>
        /// Gets the array of all bap habitat codes.
        /// </summary>
        /// <value>
        /// The array of all bap habitat codes.
        /// </value>
        public HluDataSet.lut_habitat_typeRow[] BapHabitatCodes
        {
            get
            {
                // Get the value from the lookup table.
                if (_bapHabitatCodes == null)
                {
                    //---------------------------------------------------------------------
                    // CHANGED: CR52 Enable support for multiple priority habitat classifications
                    // Enable multiple priority habitat types (from the same or different
                    // classifications) to be assigned
                    //
                    _bapHabitatCodes = (from ht in _lutHabitatType
                                        where ht.bap_priority == true
                                        select ht).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                    //---------------------------------------------------------------------
                }

                return _bapHabitatCodes;
            }
        }

        /// <summary>
        /// Gets the array of all determination quality codes.
        /// </summary>
        /// <value>
        /// The array of all determination quality codes.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] DeterminationQualityCodes
        {
            get
            {
                return _lutQualityDetermination.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
            }
        }

        /// <summary>
        /// Gets the array of bap determination quality codes valid for automatically
        /// assigned priority habitats.
        /// </summary>
        /// <value>
        /// The array of automatic bap determination quality codes.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] BapDeterminationQualityCodesAuto
        {
            get
            {
                if (DeterminationQualityCodes != null)
                    return DeterminationQualityCodes.Where(r => r.code != BapEnvironment.BAPDetQltyUserAdded
                        && r.code != BapEnvironment.BAPDetQltyPrevious)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the array of bap determination quality codes valid for user
        /// assigned priority habitats.
        /// </summary>
        /// <value>
        /// The array of user assigned bap determination quality codes.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] BapDeterminationQualityCodesUser
        {
            get
            {
                // Show all determination quality values in the drop-down list (instead
                // of just 'Not present but close to definition') but validate the
                // selected value later.
                return DeterminationQualityCodes;
            }
        }

        /// <summary>
        /// Gets the  array of all interpretation quality codes.
        /// </summary>
        /// <value>
        /// The  array of all interpretation quality codes.
        /// </value>
        public HluDataSet.lut_quality_interpretationRow[] InterpretationQualityCodes
        {
            get
            {
                return _lutQualityInterpretation.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
            }
        }

        /// <summary>
        /// Gets the  array of all bap related interpretation quality codes.
        /// </summary>
        /// <value>
        /// The array of all bap related interpretation quality codes.
        /// </value>
        public HluDataSet.lut_quality_interpretationRow[] BapInterpretationQualityCodes
        {
            get
            {
                return InterpretationQualityCodes;
            }
        }

        /// <summary>
        /// Gets or sets the collection of incid bap habitats automatically assigned.
        /// </summary>
        /// <value>
        /// The collection of incid bap habitats automatically assigned.
        /// </value>
        public ObservableCollection<BapEnvironment> IncidBapHabitatsAuto
        {
            get { return _incidBapRowsAuto; }
            set
            {
                _incidBapRowsAuto = value;
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        /// <summary>
        /// Gets or sets the collection of incid bap habitats assigned by the user.
        /// The bap_id of existing secondary priority habitats is multiplied by -1 (and same again when
        /// saving back to DB) to distinguish them from primary priority habitats in UI validation methods.
        /// </summary>
        /// <value>
        /// The collection of incid bap habitats assigned by the user.
        /// </value>
        public ObservableCollection<BapEnvironment> IncidBapHabitatsUser
        {
            get { return _incidBapRowsUser; }
            set
            {
                _incidBapRowsUser = value;
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public bool BapHabitatsAutoEnabled
        {
            get { return IncidBapHabitatsAuto != null && IncidBapHabitatsAuto.Count > 0; } // return _bulkUpdateMode == false; }
        }

        public bool BapHabitatsUserEnabled
        {
            get
            {
                return true;
                //return _bulkUpdateMode == true || (IncidBapHabitatsAuto != null && 
                //    IncidBapHabitatsAuto.Count > 0) || (IncidBapHabitatsUser.Count > 0);
            }
        }

        /// <summary>
        /// Gets all of the automatically assigned and user assigned bap environments.
        /// </summary>
        public void GetBapEnvironments()
        {
            // Remove any existing handlers before assigning a new collection.
            if (_incidBapRowsAuto != null)
                _incidBapRowsAuto.CollectionChanged -= _incidBapRowsAuto_CollectionChanged;
            if (_incidBapRowsUser != null)
                _incidBapRowsUser.CollectionChanged -= _incidBapRowsUser_CollectionChanged;

            IEnumerable<string> mandatoryBap = null;
            IEnumerable<HluDataSet.incid_bapRow> incidBapRowsUndel = null;
            if (IncidPrimary != null)
            {
                // Identify which primary BAP rows there should be from the
                // primary and secondary codes.
                mandatoryBap = MandatoryBapEnvironments(IncidPrimary, IncidSecondaryHabitats);

                // Identify any BAP rows (both auto generated and user added) that
                // have not been marked as deleted.
                incidBapRowsUndel = _incidBapRows.Where(r => r.RowState != DataRowState.Deleted);
            }

            // If there are any undeleted rows and the IHS codes indicate
            // that there should be some primary BAP (auto) rows then sort out
            // which of the undeleted rows are the auto rows.
            if ((incidBapRowsUndel != null) && (mandatoryBap != null))
            {
                // primary BAP environments from DB (real bap_id) and new (bap_id = -1)
                IEnumerable<BapEnvironment> prevBapRowsAuto = null;
                IEnumerable<BapEnvironment> newBapRowsAuto = null;
                if (incidBapRowsUndel == null)
                {
                    prevBapRowsAuto = Array.Empty<BapEnvironment>().AsEnumerable();
                    newBapRowsAuto = Array.Empty<BapEnvironment>().AsEnumerable();
                }
                else
                {
                    // Which of the undeleted rows are auto rows that
                    // already existed.
                    prevBapRowsAuto = from r in incidBapRowsUndel
                                      join pot in mandatoryBap on r.bap_habitat equals pot
                                      where _incidCurrentRow.incid != null && r.incid == _incidCurrentRow.incid
                                      select new BapEnvironment(false, false, r);

                    // Which of the undeleted rows were previously user
                    // added rows but should now be promoted to auto
                    // rows as a result of changes to the IHS codes.
                    newBapRowsAuto = from r in incidBapRowsUndel
                                     join pot in mandatoryBap on r.bap_habitat equals pot
                                     where !prevBapRowsAuto.Any(p => p.bap_habitat == r.bap_habitat)
                                     select new BapEnvironment(false, false, r);
                }

                // Determine if there are any potential BAP rows that should
                // be added as a result of changes to the IHS codes.
                var potBap = from p in mandatoryBap
                             where !prevBapRowsAuto.Any(a => a.bap_habitat == p)
                             where !incidBapRowsUndel.Any(row => row.bap_habitat == p)
                             select new BapEnvironment(false, false, -1, Incid, p, null, null, null);

                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Concatenate the previous auto rows, the newly promoted auto
                // rows and the potential BAP rows.
                _incidBapRowsAuto = new ObservableCollection<BapEnvironment>(
                    prevBapRowsAuto.Concat(newBapRowsAuto).Concat(potBap));
            }
            else if (incidBapRowsUndel != null)
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // As there should be no primary BAP rows according to the
                // IHS codes then the auto rows should be blank (because any
                // undeleted rows must therefore now be considered as user rows.
                _incidBapRowsAuto = [];
            }
            else if ((mandatoryBap != null) && (mandatoryBap.Any()))
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // If there should be some primary BAP rows according to the
                // IHS codes, but there are no existing undeleted rows, then
                // all the primrary BAP codes must become new auto rows.
                _incidBapRowsAuto = new ObservableCollection<BapEnvironment>(
                    mandatoryBap.Select(p => new BapEnvironment(false, false, -1, Incid, p, null, null, null)));
            }
            else
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // There shouldn't be any primary BAP rows according to the IHS
                // codes, and there are no existing undeleted rows, so there are
                // no auto rows.
                _incidBapRowsAuto = [];
            }

            // Track any changes to the auto rows collection.
            _incidBapRowsAuto.CollectionChanged += _incidBapRowsAuto_CollectionChanged;

            // Track when the auto data has been changed so that the apply button
            // will appear.
            foreach (BapEnvironment be in _incidBapRowsAuto)
            {
                be.DataChanged += _incidBapRowsAuto_DataChanged;
            };

            // Check if there are any errors in the auto BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapAuto");
                else
                    DelErrorList(ref _priorityErrors, "BapAuto");
            }
            else
                DelErrorList(ref _priorityErrors, "BapAuto");

            OnPropertyChanged(nameof(IncidBapHabitatsAuto));

            // If there are undeleted rows and there are some auto rows
            // then sort them out to determine which of the undeleted rows
            // are considered as user added.
            if ((incidBapRowsUndel != null) && (_incidBapRowsAuto != null))
            {
                List<BapEnvironment> prevBapRowsUser = null;
                // If there were no user added rows before then there
                // are no previous user added rows.
                if (_incidBapRowsUser == null)
                {
                    prevBapRowsUser = [];
                }
                else
                {
                    // If there were user added rows before then determine
                    // which of them have not been promoted to auto rows.
                    prevBapRowsUser = (from r in _incidBapRowsUser
                                       where _incidCurrentRow.incid != null && r.incid == _incidCurrentRow.incid
                                       where !_incidBapRowsAuto.Any(row => row.bap_habitat == r.bap_habitat)
                                       select r).ToList();
                    prevBapRowsUser.ForEach(delegate(BapEnvironment be)
                    {
                        // Don't overwrite the determination quality value loaded from the
                        // database with 'Not present but close to definition' as other
                        // values may be valid and will be validated later.
                        //
                        //be.quality_determination = BapEnvironment.BAPDetQltyUserAdded;
                        be.BulkUpdateMode = _bulkUpdateMode == true;
                    });
                }

                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Concatenate the previous user added rows with any remaining
                // undeleted rows that are not auto rows.
                _incidBapRowsUser = new ObservableCollection<BapEnvironment>(prevBapRowsUser.Concat(
                    from r in incidBapRowsUndel
                    where !_incidBapRowsAuto.Any(a => a.bap_habitat == r.bap_habitat)
                    where !prevBapRowsUser.Any(p => p.bap_habitat == r.bap_habitat)
                    select new BapEnvironment(_bulkUpdateMode == true, true, r)));
            }
            // If thereare undeleted rows but no auto rows then all the
            // undeleted rows must be considered user added rows.
            else if (incidBapRowsUndel != null)
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                _incidBapRowsUser = new ObservableCollection<BapEnvironment>(
                   incidBapRowsUndel.Select(r => new BapEnvironment(_bulkUpdateMode == true, true, r)));
            }
            else
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Otherwise there can't be any user added rows.
                _incidBapRowsUser = [];
            }

            // Track any changes to the user rows collection.
            _incidBapRowsUser.CollectionChanged += _incidBapRowsUser_CollectionChanged;

            // Track when the user data has been changed so that the apply button
            // will appear.
            foreach (BapEnvironment be in _incidBapRowsUser)
            {
                be.DataChanged += _incidBapRowsUser_DataChanged;
            };

            // Check if there are any errors in the user BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapUser");
                else
                    DelErrorList(ref _priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    //DONE: Aggregate
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                            group be by be.bap_habitat into g
                                            where g.Count() > 1
                                            select g.Key).ToList();

                    //StringBuilder beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                    //                        group be by be.bap_habitat into g
                    //                        where g.Count() > 1
                    //                        select g.Key).Aggregate(new(), (sb, code) => sb.Append(", " + code));

                    if (beDups.Count > 2)
                        AddErrorList(ref _priorityErrors, "BapUserDup");
                    else
                        DelErrorList(ref _priorityErrors, "BapUserDup");
                }
            }
            else
                DelErrorList(ref _priorityErrors, "BapUser");

            OnPropertyChanged(nameof(IncidBapHabitatsUser));

            // Concatenate the auto rows and the user rows to become the new list
            // of BAP rows.
            BapEnvironment.BapEnvironmentList = _incidBapRowsAuto.Concat(_incidBapRowsUser);

            OnPropertyChanged(nameof(PriorityTabLabel));

        }

        /// <summary>
        /// Track when the BAP primary records have changed so that the apply
        /// button will appear.
        /// </summary>
        private void _incidBapRowsAuto_DataChanged(bool BapChanged)
        {
            Changed = true;

            // Check if there are any errors in the primary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapAuto");
                else
                    DelErrorList(ref _priorityErrors, "BapAuto");
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Track when the BAP secondary records have changed so that the apply
        /// button will appear.
        /// </summary>
        private void _incidBapRowsUser_DataChanged(bool BapChanged)
        {
            Changed = true;

            // Check if there are any errors in the secondary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapUser");
                else
                    DelErrorList(ref _priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and 
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    //DONE: Aggregate
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                            group be by be.bap_habitat into g
                                            where g.Count() > 1
                                            select g.Key).ToList();

                    //StringBuilder beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                    //                        group be by be.bap_habitat into g
                    //                        where g.Count() > 1
                    //                        select g.Key).Aggregate(new(), (sb, code) => sb.Append(", " + code));

                    if (beDups.Count > 2)
                        AddErrorList(ref _priorityErrors, "BapUserDup");
                    else
                        DelErrorList(ref _priorityErrors, "BapUserDup");
                }
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Build a enumerable of the mandatory bap habitats
        /// based on the primary habitat and all the secondary habitats.
        /// </summary>
        /// <param name="primaryHabitat">The primary habitat.</param>
        /// <param name="secondaryHabitats">The secondary habitats.</param>
        /// <returns></returns>
        internal IEnumerable<string> MandatoryBapEnvironments(string primaryHabitat, ObservableCollection<SecondaryHabitat> secondaryHabitats)
        {
            IEnumerable<string> primaryBap = null;
            IEnumerable<string> secondaryBap = null;
            string[] q = null;

            // Get the BAP habitats associated with the primary habitat
            if (!String.IsNullOrEmpty(primaryHabitat))
            {
                try
                {
                    //---------------------------------------------------------------------
                    // CHANGED: CR52 Enable support for multiple priority habitat classifications
                    // Enable multiple priority habitat types (from the same or different
                    // classifications) to be assigned
                    //
                    q = (from pb in _lutPrimaryBapHabitat
                         where pb.code_primary == primaryHabitat
                         select pb.bap_habitat).ToArray();

                    // If any primary bap habitats have been found
                    primaryBap = null;
                    if ((q != null) && (q.Length != 0))
                        primaryBap = q;
                    //---------------------------------------------------------------------
                }
                catch { }
            }

            // Get the BAP habitats associated with all of the secondary habitats
            if (secondaryHabitats != null)
            {
                try
                {
                    q = (from sb in _lutSecondaryBapHabitat
                         join s in secondaryHabitats on sb.code_secondary equals s.secondary_habitat
                         select sb.bap_habitat).ToArray();

                    // If any secondary bap habitats have been found
                    secondaryBap = null;
                    if ((q != null) && (q.Length != 0))
                        secondaryBap = q;
                }
                catch { }
            }

            IEnumerable<string> allBap = null;
            allBap = primaryBap != null ? secondaryBap != null ? primaryBap.Concat(secondaryBap) : primaryBap : secondaryBap;
            if (allBap != null)
                return allBap.Distinct();
            else
                return [];
        }

        private void _incidBapRowsAuto_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Error));

            // Track when the BAP primary records have changed so that the apply
            // button will appear.
            Changed = true;

            // Check if there are any errors in the primary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapAuto");
                else
                    DelErrorList(ref _priorityErrors, "BapAuto");
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        private void _incidBapRowsUser_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //---------------------------------------------------------------------
            // FIXED: KI108 (Deleting potential BAP habitats)
            // Deleting the rows from the _incidBapRows datatable here causes
            // problems if the same row number is deleted twice as the row is
            // marked as deleted (RowState = Deleted) and hence the bap_id
            // cannot be read.  The rows are deleted later anyway when the
            // record is updated so they are left alone here.
            //
            // The user interface source for the potential BAP habtiats is
            // _incidBapRowsUser which is updated automatically when a row
            // is deleted so the row deleted automatically disappears in
            // the user interface.
            //
            //if (e.Action == NotifyCollectionChangedAction.Remove)
            //{
            //    (from r in _incidBapRows
            //     join be in e.OldItems.Cast<BapEnvironment>() on r.bap_id equals be.bap_id
            //     select r).ToList().ForEach(delegate(HluDataSet.incid_bapRow row) { row.Delete(); });
            //}
            //---------------------------------------------------------------------

            OnPropertyChanged(nameof(Error));

            // Track when the BAP secondary records have changed so that the apply
            // button will appear.
            Changed = true;

            // Check if there are any errors in the secondary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddErrorList(ref _priorityErrors, "BapUser");
                else
                    DelErrorList(ref _priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and 
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    //DONE: Aggregate
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                            group be by be.bap_habitat into g
                                            where g.Count() > 1
                                            select g.Key).ToList();

                    //StringBuilder beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                    //                        group be by be.bap_habitat into g
                    //                        where g.Count() > 1
                    //                        select g.Key).Aggregate(new(), (sb, code) => sb.Append(", " + code));

                    if (beDups.Count > 2)
                        AddErrorList(ref _priorityErrors, "BapUserDup");
                    else
                        DelErrorList(ref _priorityErrors, "BapUserDup");
                }
            }
            else
            {
                DelErrorList(ref _priorityErrors, "BapUser");
            }

            OnPropertyChanged(nameof(PriorityTabLabel));

            foreach (BapEnvironment be in _incidBapRowsUser)
            {
                if (be == null)
                    be.DataChanged -= _incidBapRowsUser_DataChanged;
                else if (be.bap_id == -1)
                    be.DataChanged += _incidBapRowsUser_DataChanged;
            }
        }

        #endregion

        #endregion

        #region Details Tab

        // Set the Details tab label from here so that validation can be done.
        // This will enable tooltips to be shown so that validation errors
        // in any fields in the tab can be highlighted by flagging the tab
        // label as in error.
        public string DetailsTabLabel
        {
            get { return "Details"; }
        }

        #region General Comments

        public string DetailsCommentsHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "General Comments";
                else
                    return null;
            }
        }

        public string IncidGeneralComments
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.general_commentsColumn))
                    return IncidCurrentRow.general_comments;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.general_comments = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        #endregion

        #region Maps

        public string DetailsMapsHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Maps";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the list of boundary map codes.
        /// </summary>
        /// <value>
        /// The list of boundary map codes.
        /// </value>
        public HluDataSet.lut_boundary_mapRow[] BoundaryMapCodes
        {
            get
            {
                if (_boundaryMapCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _boundaryMapCodes = _lutBoundaryMap.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _boundaryMapCodes;
            }
        }

        /// <summary>
        /// Gets or sets the incid boundary base map.
        /// </summary>
        /// <value>
        /// The incid boundary base map.
        /// </value>
        public string IncidBoundaryBaseMap
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.boundary_base_mapColumn))
                    return IncidCurrentRow.boundary_base_map;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.boundary_base_map = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        /// <summary>
        /// Gets or sets the incid digitisation base map.
        /// </summary>
        /// <value>
        /// The incid digitisation base map.
        /// </value>
        public string IncidDigitisationBaseMap
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.digitisation_base_mapColumn))
                    return IncidCurrentRow.digitisation_base_map;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.digitisation_base_map = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        #endregion

        #region Site

        public string DetailsSiteHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Site";
                else
                    return null;
            }
        }

        //---------------------------------------------------------------------
        // CHANGED: CR37 (Site reference and site name)
        // Display the site reference with the site name in the interface.
        public string IncidSiteRef
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.Issite_refNull())
                    return IncidCurrentRow.site_ref;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.site_ref = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }
        //---------------------------------------------------------------------

        public string IncidSiteName
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.Issite_nameNull())
                    return IncidCurrentRow.site_name;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.site_name = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        #endregion

        #region Condition
        
        /// <summary>
        /// Gets the details condition group header.
        /// </summary>
        /// <value>
        /// The details condition group header.
        /// </value>
        public string DetailsConditionHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Condition";
                else
                    return null;
            }
        }

        /// <summary>
        /// Check if there are any valid condition rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckCondition()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidConditionRows == null)
            {
                HluDataSet.incid_conditionDataTable incidConditionTable = _hluDS.incid_condition;
                _incidConditionRows = GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_conditionTableAdapter, ref incidConditionTable);
            }

            return _incidConditionRows != null;
        }

        /// <summary>
        /// Gets the list of condition codes.
        /// </summary>
        /// <value>
        /// The list of condition codes.
        /// </value>
        public HluDataSet.lut_conditionRow[] ConditionCodes
        {
            get
            {
                if (_conditionCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _conditionCodes = _lutCondition.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                }

                // Return the list of condition codes, with the clear row if applicable.
                if (_incidConditionRows.Length >= 1 &&
                    _incidConditionRows[0] != null &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.conditionColumn))

                {
                    // Define the <Clear> row
                    HluDataSet.lut_conditionRow clearRow = HluDataset.lut_condition.Newlut_conditionRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_conditionRow[] { clearRow }.Concat(_conditionCodes).ToArray();
                }
                else
                {
                    return _conditionCodes;
                }
            }
        }

        /// <summary>
        /// Gets or sets the incid condition.
        /// </summary>
        /// <value>
        /// The incid condition.
        /// </value>
        public string IncidCondition
        {
            get
            {
                if (!CheckCondition()) return null;

                if (_incidConditionRows.Length < 1)
                {
                    _incidConditionRows = new HluDataSet.incid_conditionRow[1];
                }

                if ((_incidConditionRows[0] != null) &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.conditionColumn))
                    return _incidConditionRows[0].condition;
                else
                    return null;
            }
            set
            {
                if (IncidCurrentRow != null)
                {
                    bool clearCode = value == _codeDeleteRow;
                    bool newCode = false;
                    if (clearCode)
                        value = null;
                    else
                        newCode = ((_incidConditionRows.Length < 1 ||
                            _incidConditionRows[0] == null ||
                            //String.IsNullOrEmpty(_incidConditionRows[0].condition))
                            _incidConditionRows[0].IsNull(HluDataset.incid_condition.conditionColumn))
                            && (!String.IsNullOrEmpty(value)));

                    UpdateIncidConditionRow(0, IncidConditionTable.conditionColumn.Ordinal, value);

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;

                    // If the code has been cleared or it was previously clear and
                    // has been set then refresh the condition codes list and other
                    // condition fields
                    if (clearCode || newCode)
                    {
                        OnPropertyChanged(nameof(ConditionCodes));
                        OnPropertyChanged(nameof(IncidConditionQualifier));
                        OnPropertyChanged(nameof(IncidConditionDate));
                        OnPropertyChanged(nameof(IncidConditionEnabled));
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the list of condition qualifier codes.
        /// </summary>
        /// <value>
        /// The list of condition qualifier codes.
        /// </value>
        public HluDataSet.lut_condition_qualifierRow[] ConditionQualifierCodes
        {
            get
            {
                if (_conditionQualifierCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _conditionQualifierCodes = _lutConditionQualifier.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                }

                return _conditionQualifierCodes;
            }
        }
        
        /// <summary>
        /// Gets or sets the incid condition qualifier.
        /// </summary>
        /// <value>
        /// The incid condition qualifier.
        /// </value>
        public string IncidConditionQualifier
        {
            get
            {
                if (!CheckCondition()) return null;
                if ((_incidConditionRows.Length > 0) && (_incidConditionRows[0] != null) &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.condition_qualifierColumn))
                    return _incidConditionRows[0].condition_qualifier;
                else
                    return null;
            }
            set
            {
                UpdateIncidConditionRow(0, IncidConditionTable.condition_qualifierColumn.Ordinal, value);
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        /// <summary>
        /// Gets or sets the incid condition date.
        /// </summary>
        /// <value>
        /// The incid condition date.
        /// </value>
        public Date.VagueDateInstance IncidConditionDate
        {
            get
            {
                // Check if there are any valid condition rows.
                if (!CheckCondition()) return null;


                if ((_incidConditionRows.Length > 0) && (_incidConditionRows[0] != null) &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.condition_date_startColumn) &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.condition_date_endColumn) &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.condition_date_typeColumn))
                {
                    Date.VagueDateInstance vd = new(_incidConditionRows[0].condition_date_start,
                        _incidConditionRows[0].condition_date_end, _incidConditionRows[0].condition_date_type,
                        _incidConditionDateEntered?.UserEntry);
                    return vd;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                // Update the condition row with the new value
                UpdateIncidConditionRow(0, IncidConditionTable.condition_date_startColumn.Ordinal, value);
                _incidConditionDateEntered = value;
                OnPropertyChanged(nameof(IncidConditionDate));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        /// <summary>
        /// Updates the incid condition row.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rowNumber">The row number.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <param name="newValue">The new value.</param>
        private void UpdateIncidConditionRow<T>(int rowNumber, int columnOrdinal, T newValue)
        {
            try
            {
                if (_incidConditionRows == null) return;

                // If the row is blank
                if (_incidConditionRows[rowNumber] == null)
                {
                    if ((columnOrdinal == HluDataset.incid_condition.conditionColumn.Ordinal) && newValue != null)
                    {
                        // Set the row id
                        HluDataSet.incid_conditionRow newRow = IncidConditionTable.Newincid_conditionRow();
                        newRow.incid_condition_id = NextIncidConditionId;
                        if (_bulkUpdateMode == false)
                            newRow.incid = IncidCurrentRow.incid;
                        _incidConditionRows[rowNumber] = newRow;
                    }
                    else
                    {
                        return;
                    }
                }
                // If the new condition is null
                else if ((columnOrdinal == HluDataset.incid_condition.conditionColumn.Ordinal) && (newValue == null))
                {
                    // Delete the row unless during a bulk update
                    if (_bulkUpdateMode == false)
                    {
                        if (_incidConditionRows[rowNumber].RowState != DataRowState.Detached)
                            _incidConditionRows[rowNumber].Delete();
                        _incidConditionRows[rowNumber] = null;
                    }
                    else
                    {
                        _incidConditionRows[rowNumber] = IncidConditionTable.Newincid_conditionRow();
                        IncidConditionRows[rowNumber].incid_condition_id = rowNumber;
                        IncidConditionRows[rowNumber].condition = null;
                        IncidConditionRows[rowNumber].incid = RecIDs.CurrentIncid;
                    }
                    return;
                }

                // Update the date columns
                if ((columnOrdinal == HluDataset.incid_condition.condition_date_startColumn.Ordinal) ||
                    (columnOrdinal == HluDataset.incid_condition.condition_date_endColumn.Ordinal))
                {
                    if (newValue is Date.VagueDateInstance vd)
                    {
                        _incidConditionRows[rowNumber].condition_date_start = vd.StartDate;
                        _incidConditionRows[rowNumber].condition_date_end = vd.EndDate;
                        _incidConditionRows[rowNumber].condition_date_type = vd.DateType;
                    }
                    else
                    {
                        _incidConditionRows[rowNumber].condition_date_start = VagueDate.DateUnknown;
                        _incidConditionRows[rowNumber].condition_date_end = VagueDate.DateUnknown;
                        _incidConditionRows[rowNumber].condition_date_type = null;
                    }
                }
                // Update all other columns if they have changed
                else if ((((_incidConditionRows[rowNumber].IsNull(columnOrdinal) ^ (newValue == null)) ||
                    ((!_incidConditionRows[rowNumber].IsNull(columnOrdinal) && (newValue != null)))) &&
                    !_incidConditionRows[rowNumber][columnOrdinal].Equals(newValue)))
                {
                    _incidConditionRows[rowNumber][columnOrdinal] = newValue;
                }

                if ((_incidConditionRows[rowNumber].RowState == DataRowState.Detached) &&
                    IsCompleteRow(_incidConditionRows[rowNumber]))
                {
                    IncidConditionTable.Addincid_conditionRow(_incidConditionRows[rowNumber]);
                }
            }
            catch { }
        }

        //
        public bool IncidConditionEnabled
        {
            // Disable remaining condition fields when condition is blank
            get { return (IncidCondition != null); }
        }

        #endregion

        #region Quality

        /// <summary>
        /// Gets the details quality group header.
        /// </summary>
        /// <value>
        /// The details quality group header.
        /// </value>
        public string DetailsQualityHeader
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Quality";
                else
                    return null;
            }
        }
        
        /// <summary>
        /// Gets the list of quality determination codes.
        /// </summary>
        /// <value>
        /// The list of quality determination codes.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] QualityDeterminationCodes
        {
            get
            {
                if (_qualityDeterminationCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _qualityDeterminationCodes = _lutQualityDetermination.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray(); ;
                }

                // Return the list of determination codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidQualityDetermination))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_quality_determinationRow clearRow = HluDataset.lut_quality_determination.Newlut_quality_determinationRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_quality_determinationRow[] { clearRow }.Concat(_qualityDeterminationCodes).ToArray();
                }
                else
                {
                    return _qualityDeterminationCodes;
                }
            }
        }

        /// <summary>
        /// Gets or sets the incid quality determination.
        /// </summary>
        /// <value>
        /// The incid quality determination.
        /// </value>
        public string IncidQualityDetermination
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.quality_determinationColumn))
                    return IncidCurrentRow.quality_determination;
                else
                    return null;
            }
            set
            {
                if (IncidCurrentRow != null)
                {
                    bool clearCode = value == _codeDeleteRow;
                    bool newCode = false;
                    if (clearCode)
                        value = null;
                    else
                        newCode = ((IncidCurrentRow.IsNull(HluDataset.incid.quality_determinationColumn)) && (!String.IsNullOrEmpty(value)));

                    IncidCurrentRow.quality_determination = value;

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;

                    // Refresh quality determnation list
                    if (clearCode || newCode)
                        OnPropertyChanged(nameof(QualityDeterminationCodes));
                }
            }
        }
        
        /// <summary>
        /// Gets the list of quality interpretation codes.
        /// </summary>
        /// <value>
        /// The list of quality interpretation codes.
        /// </value>
        public HluDataSet.lut_quality_interpretationRow[] QualityInterpretationCodes
        {
            get
            {
                if (_qualityInterpretationCodes == null)
                {
                    // Get the list of values from the lookup table.
                    _qualityInterpretationCodes = _lutQualityInterpretation.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray(); ;
                }

                // Return the list of interpretation codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidQualityInterpretation))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_quality_interpretationRow clearRow = HluDataset.lut_quality_interpretation.Newlut_quality_interpretationRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_quality_interpretationRow[] { clearRow }.Concat(_qualityInterpretationCodes).ToArray();
                }
                else
                {
                    return _qualityInterpretationCodes;
                }
            }
        }

        /// <summary>
        /// Gets or sets the incid quality interpretation.
        /// </summary>
        /// <value>
        /// The incid quality interpretation.
        /// </value>
        public string IncidQualityInterpretation
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.quality_interpretationColumn))
                    return IncidCurrentRow.quality_interpretation;
                else
                    return null;
            }
            set
            {
                if (IncidCurrentRow != null)
                {
                    bool clearCode = value == _codeDeleteRow;
                    bool newCode = false;
                    if (clearCode)
                        value = null;
                    else
                        newCode = ((IncidCurrentRow.IsNull(HluDataset.incid.quality_interpretationColumn)) && (!String.IsNullOrEmpty(value)));

                    IncidCurrentRow.quality_interpretation = value;

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;

                    // Refresh quality determnation list
                    if (clearCode || newCode)
                        OnPropertyChanged(nameof(QualityInterpretationCodes));

                    // Revalidate the comments
                    OnPropertyChanged(nameof(IncidQualityComments));
                }
            }
        }

        /// <summary>
        /// Gets or sets the incid quality comments.
        /// </summary>
        /// <value>
        /// The incid quality comments.
        /// </value>
        public string IncidQualityComments
        {
            get
            {
                if ((IncidCurrentRow != null) && !IncidCurrentRow.IsNull(HluDataset.incid.interpretation_commentsColumn))
                    return IncidCurrentRow.interpretation_comments;
                else
                    return null;
            }
            set
            {
                if ((IncidCurrentRow != null) && (value != null))
                {
                    IncidCurrentRow.interpretation_comments = value;
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    Changed = true;
                }
            }
        }

        #endregion

        #endregion

        #region Sources Tab

        /// <summary>
        /// Gets the sources tab group label.
        /// </summary>
        /// <value>
        /// The sources tab group label.
        /// </value>
        public string SourcesTabLabel
        {
            get { return "Sources"; }
        }

        #region Sources

        /// <summary>
        /// Checks if there are any valid source rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckSources()
        {
            if (_bulkUpdateMode == true) return true;

            if (_incidSourcesRows == null)
            {
                HluDataSet.incid_sourcesDataTable incidSourcesTable = _hluDS.incid_sources;
                _incidSourcesRows = GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_sourcesTableAdapter, ref incidSourcesTable);
            }
            return _incidSourcesRows != null;
        }

        /// <summary>
        /// Returns the default date for a given source.
        /// </summary>
        /// <param name="currentDate">The current date.</param>
        /// <param name="sourceID">The source identifier.</param>
        /// <returns></returns>
        public Date.VagueDateInstance DefaultSourceDate(Date.VagueDateInstance currentDate, Nullable<int> sourceID)
        {
            if ((HluDataset == null) || (HluDataset.lut_sources == null)) return currentDate;

            EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                HluDataset.lut_sources.Where(r => r.source_id == sourceID &&
                    !r.IsNull(HluDataset.lut_sources.source_date_defaultColumn));

            if (rows.Any())
            {
                string defaultDate;
                string dateType = VagueDate.GetType(rows.ElementAt(0).source_date_default, out defaultDate);
                int startDate = VagueDate.ToTimeSpanDays(defaultDate, dateType, VagueDate.DateType.Start);
                int endDate = VagueDate.ToTimeSpanDays(defaultDate, dateType, VagueDate.DateType.End);
                return new Date.VagueDateInstance(startDate, endDate, dateType);
            }

            return currentDate;
        }
        
        /// <summary>
        /// Updates the incid sources row.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rowNumber">The row number.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <param name="newValue">The new value.</param>
        private void UpdateIncidSourcesRow<T>(int rowNumber, int columnOrdinal, T newValue)
        {
            try
            {
                if (_incidSourcesRows == null) return;

                // If the row is blank
                if (_incidSourcesRows[rowNumber] == null)
                {
                    if (columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal)
                    {
                        // Set the row id
                        HluDataSet.incid_sourcesRow newRow = IncidSourcesTable.Newincid_sourcesRow();
                        newRow.incid_source_id = NextIncidSourcesId;
                        newRow.incid = IncidCurrentRow.incid;
                        newRow.sort_order = rowNumber + 1;
                        _incidSourcesRows[rowNumber] = newRow;
                    }
                    else
                    {
                        return;
                    }
                }
                // If the new source_id is null
                else if ((columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal) && (newValue == null))
                {
                    // Delete the row unless during a bulk update
                    if (_bulkUpdateMode == false)
                    {
                        if (_incidSourcesRows[rowNumber].RowState != DataRowState.Detached)
                            _incidSourcesRows[rowNumber].Delete();
                        _incidSourcesRows[rowNumber] = null;
                    }
                    else
                    {
                        _incidSourcesRows[rowNumber] = IncidSourcesTable.Newincid_sourcesRow();
                        IncidSourcesRows[rowNumber].incid_source_id = rowNumber;
                        IncidSourcesRows[rowNumber].source_id = Int32.MinValue;
                        IncidSourcesRows[rowNumber].incid = RecIDs.CurrentIncid;
                    }
                    return;
                }

                // Update the date columns
                if ((columnOrdinal == HluDataset.incid_sources.source_date_startColumn.Ordinal) ||
                    (columnOrdinal == HluDataset.incid_sources.source_date_endColumn.Ordinal))
                {
                    if (newValue is Date.VagueDateInstance vd)
                    {
                        _incidSourcesRows[rowNumber].source_date_start = vd.StartDate;
                        _incidSourcesRows[rowNumber].source_date_end = vd.EndDate;
                        _incidSourcesRows[rowNumber].source_date_type = vd.DateType;
                    }
                    else
                    {
                        _incidSourcesRows[rowNumber].source_date_start = VagueDate.DateUnknown;
                        _incidSourcesRows[rowNumber].source_date_end = VagueDate.DateUnknown;
                        _incidSourcesRows[rowNumber].source_date_type = null;
                    }
                }
                // Update all other columns if they have changed
                else if ((((_incidSourcesRows[rowNumber].IsNull(columnOrdinal) ^ (newValue == null)) ||
                    ((!_incidSourcesRows[rowNumber].IsNull(columnOrdinal) && (newValue != null)))) &&
                    !_incidSourcesRows[rowNumber][columnOrdinal].Equals(newValue)))
                {
                    _incidSourcesRows[rowNumber][columnOrdinal] = newValue;
                }

                // If updating the source_id get the default date
                if (columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal)
                {
                    try
                    {
                        HluDataSet.lut_sourcesRow lutRow =
                            HluDataset.lut_sources.Single(r => r.source_id == _incidSourcesRows[rowNumber].source_id);
                        if (!String.IsNullOrEmpty(lutRow.source_date_default))
                        {
                            string defaultDateString;
                            string formatString = VagueDate.GetType(lutRow.source_date_default, out defaultDateString);
                            int defaultStartDate = VagueDate.ToTimeSpanDays(defaultDateString,
                                formatString, VagueDate.DateType.Start);
                            int defaultEndDate = VagueDate.ToTimeSpanDays(defaultDateString,
                                formatString, VagueDate.DateType.End);
                            _incidSourcesRows[rowNumber].source_date_start = defaultStartDate;
                            _incidSourcesRows[rowNumber].source_date_end = defaultEndDate;
                        }
                    }
                    catch { }
                }

                if ((_incidSourcesRows[rowNumber].RowState == DataRowState.Detached) &&
                    IsCompleteRow(_incidSourcesRows[rowNumber]))
                {
                    _incidSourcesRows[rowNumber].sort_order = rowNumber + 1;
                    IncidSourcesTable.Addincid_sourcesRow(_incidSourcesRows[rowNumber]);
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Gets the list of source names.
        /// </summary>
        /// <value>
        /// The list of source names.
        /// </value>
        public HluDataSet.lut_sourcesRow[] SourceNames
        {
            get
            {
                // Get the list of values from the lookup table.
                if (_sourceNames == null)
                {
                    _sourceNames = _lutSources.OrderBy(r => r.sort_order).ThenBy(r => r.source_name).ToArray();
                }

                return _sourceNames;
            }
        }
        
        /// <summary>
        /// Gets the list of source habitat class codes.
        /// </summary>
        /// <value>
        /// The list of source habitat class codes.
        /// </value>
        public HluDataSet.lut_habitat_classRow[] SourceHabitatClassCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (_sourceHabitatClassCodes == null)
                {
                    _sourceHabitatClassCodes = _lutHabitatClass
                        .OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _sourceHabitatClassCodes;
            }
        }
        
        /// <summary>
        /// Gets the list of source importance codes.
        /// </summary>
        /// <value>
        /// The list of source importance codes.
        /// </value>
        public HluDataSet.lut_importanceRow[] SourceImportanceCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (_sourceImportanceCodes == null)
                {
                    _sourceImportanceCodes = _lutImportance.OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();
                }

                return _sourceImportanceCodes;
            }
        }

        #endregion

        #region Source1
        
        public string Source1Header
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Source 1";
                else
                    return null;
            }
        }

        public Visibility ShowSourceNumbers
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        public HluDataSet.lut_sourcesRow[] Source1Names
        {
            get
            {
                // Load the lookup table if not already loaded.
                if (HluDataset.lut_sources.IsInitialized &&
                    HluDataset.lut_sources.Count == 0)
                {
                    if (_hluTableAdapterMgr.lut_sourcesTableAdapter == null)
                        _hluTableAdapterMgr.lut_sourcesTableAdapter =
                            new HluTableAdapter<HluDataSet.lut_sourcesDataTable, HluDataSet.lut_sourcesRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_sourcesDataTable)], false);
                }

                // Return the list of source names, with the clear row if applicable, but
                // exclude the clear row in bulk update mode
                if ((IncidSource1Id != null) && (IncidSource1Id != Int32.MinValue))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_sourcesRow clearRow = HluDataset.lut_sources.Newlut_sourcesRow();
                    clearRow.source_id = -1;
                    clearRow.source_name = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_sourcesRow[] { clearRow }.Concat(SourceNames).ToArray();
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        public Nullable<int> IncidSource1Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = _incidSourcesRows.Concat(tmpRows).ToArray();
                }

                if (_incidSourcesRows[0] != null)
                    return _incidSourcesRows[0].source_id;
                else
                    return null;
            }
            set
            {
                if (value == -1)
                {
                    // delete the row
                    UpdateIncidSourcesRow(0, IncidSourcesTable.source_idColumn.Ordinal, (Nullable<int>)null);

                    // refresh source names list
                    OnPropertyChanged(nameof(Source1Names));

                    // clear all fields of Source 1
                    IncidSource1Date = null;
                    IncidSource1HabitatClass = null;
                    IncidSource1HabitatType = null;
                    IncidSource1BoundaryImportance = null;
                    IncidSource1HabitatImportance = null;
                }
                else if (value != null)
                {
                    // Check for equivalent null value when in bulk update mode
                    bool wasNull = (_incidSourcesRows[0] == null || (int)_incidSourcesRows[0]["source_id"] == Int32.MinValue);

                    UpdateIncidSourcesRow(0, IncidSourcesTable.source_idColumn.Ordinal, value);
                    IncidSource1Date = DefaultSourceDate(IncidSource1Date, IncidSource1Id);
                    // if row added refresh source names list
                    if (wasNull && (_incidSourcesRows[0] != null)) OnPropertyChanged(nameof(Source1Names));
                }
                OnPropertyChanged(nameof(IncidSource1Date));
                OnPropertyChanged(nameof(IncidSource1HabitatClass));
                OnPropertyChanged(nameof(IncidSource1HabitatType));
                OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource1HabitatImportance));
                OnPropertyChanged(nameof(IncidSource1Enabled));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public bool IncidSource1Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource1Id != null); }
        }

        public Date.VagueDateInstance IncidSource1Date
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 0) && (_incidSourcesRows[0] != null) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_date_startColumn) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_date_endColumn) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_date_typeColumn))
                {
                    Date.VagueDateInstance vd = new(_incidSourcesRows[0].source_date_start,
                        _incidSourcesRows[0].source_date_end, _incidSourcesRows[0].source_date_type,
                        _incidSource1DateEntered?.UserEntry);
                    return vd;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                UpdateIncidSourcesRow(0, IncidSourcesTable.source_date_startColumn.Ordinal, value);
                _incidSource1DateEntered = value;
                OnPropertyChanged(nameof(IncidSource1Date));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource1HabitatClass
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 0) && (_incidSourcesRows[0] != null) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_habitat_classColumn))
                    return _incidSourcesRows[0].source_habitat_class;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(0, IncidSourcesTable.source_habitat_classColumn.Ordinal, value);
                OnPropertyChanged(nameof(Source1HabitatTypeCodes));
                OnPropertyChanged(nameof(IncidSource1HabitatType));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public HluDataSet.lut_habitat_typeRow[] Source1HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (!String.IsNullOrEmpty(IncidSource1HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource1HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name).ToArray();

                    // Set the habitat type if there is only one
                    if ((retArray.Length == 1) && (IncidSource1Id != null))
                    {
                        IncidSource1HabitatType = retArray[0].code;
                        OnPropertyChanged(nameof(IncidSource1HabitatType));
                    }

                    return retArray;
                }
                return null;
            }
        }

        public string IncidSource1HabitatType
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 0) && (_incidSourcesRows[0] != null) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_habitat_typeColumn))
                    return _incidSourcesRows[0].source_habitat_type;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(0, IncidSourcesTable.source_habitat_typeColumn.Ordinal, value);
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource1BoundaryImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 0) && (_incidSourcesRows[0] != null) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_boundary_importanceColumn))
                    return _incidSourcesRows[0].source_boundary_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(0, IncidSourcesTable.source_boundary_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource1HabitatImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 0) && (_incidSourcesRows[0] != null) &&
                    !_incidSourcesRows[0].IsNull(HluDataset.incid_sources.source_habitat_importanceColumn))
                    return _incidSourcesRows[0].source_habitat_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(0, IncidSourcesTable.source_habitat_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource1HabitatImportance));
                OnPropertyChanged(nameof(IncidSource2HabitatImportance));
                OnPropertyChanged(nameof(IncidSource3HabitatImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        #endregion

        #region Source2

        public string Source2Header
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Source 2";
                else
                    return null;
            }
        }

        public HluDataSet.lut_sourcesRow[] Source2Names
        {
            get
            {
                // Return the list of source names, with the clear row if applicable, but
                // exclude the clear row in bulk update mode
                if ((IncidSource2Id != null) && (IncidSource2Id != Int32.MinValue))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_sourcesRow clearRow = HluDataset.lut_sources.Newlut_sourcesRow();
                    clearRow.source_id = -1;
                    clearRow.source_name = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return new HluDataSet.lut_sourcesRow[] { clearRow }.Concat(SourceNames).ToArray();
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        public Nullable<int> IncidSource2Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = _incidSourcesRows.Concat(tmpRows).ToArray();
                }
                if (_incidSourcesRows[1] != null)
                    return _incidSourcesRows[1].source_id;
                else
                    return null;
            }
            set
            {
                if (value == -1)
                {
                    // delete the row
                    UpdateIncidSourcesRow(1, IncidSourcesTable.source_idColumn.Ordinal, (Nullable<int>)null);

                    // refresh source names list
                    OnPropertyChanged(nameof(Source2Names));

                    // clear all fields of Source 2
                    IncidSource2Date = null;
                    IncidSource2HabitatClass = null;
                    IncidSource2HabitatType = null;
                    IncidSource2BoundaryImportance = null;
                    IncidSource2HabitatImportance = null;
                }
                else if (value != null)
                {
                    // Check for equivalent null value when in bulk update mode
                    bool wasNull = (_incidSourcesRows[1] == null || (int)_incidSourcesRows[1]["source_id"] == Int32.MinValue);

                    UpdateIncidSourcesRow(1, IncidSourcesTable.source_idColumn.Ordinal, value);
                    IncidSource2Date = DefaultSourceDate(IncidSource2Date, IncidSource2Id);
                    // if row added refresh source names list
                    if (wasNull && (_incidSourcesRows[1] != null)) OnPropertyChanged(nameof(Source2Names));
                }
                OnPropertyChanged(nameof(IncidSource2Date));
                OnPropertyChanged(nameof(IncidSource2HabitatClass));
                OnPropertyChanged(nameof(IncidSource2HabitatType));
                OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource2HabitatImportance));
                OnPropertyChanged(nameof(IncidSource2Enabled));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public bool IncidSource2Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource2Id != null); }
        }

        public Date.VagueDateInstance IncidSource2Date
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 1) && (_incidSourcesRows[1] != null) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_date_startColumn) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_date_endColumn) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_date_typeColumn))
                {
                    Date.VagueDateInstance vd = new(_incidSourcesRows[1].source_date_start,
                        _incidSourcesRows[1].source_date_end, _incidSourcesRows[1].source_date_type,
                        _incidSource2DateEntered?.UserEntry);
                    return vd;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                UpdateIncidSourcesRow(1, IncidSourcesTable.source_date_startColumn.Ordinal, value);
                _incidSource2DateEntered = value;
                OnPropertyChanged(nameof(IncidSource2Date));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource2HabitatClass
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 1) && (_incidSourcesRows[1] != null) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_habitat_classColumn))
                    return _incidSourcesRows[1].source_habitat_class;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(1, IncidSourcesTable.source_habitat_classColumn.Ordinal, value);
                OnPropertyChanged(nameof(Source2HabitatTypeCodes));
                OnPropertyChanged(nameof(IncidSource2HabitatType));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public HluDataSet.lut_habitat_typeRow[] Source2HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table
                if (!String.IsNullOrEmpty(IncidSource2HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource2HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name).ToArray();

                    // Set the habitat type if there is only one
                    if ((retArray.Length == 1) && (IncidSource2Id != null))
                    {
                        IncidSource2HabitatType = retArray[0].code;
                        OnPropertyChanged(nameof(IncidSource2HabitatType));
                    }

                    return retArray;
                }
                return null;
            }
        }

        public string IncidSource2HabitatType
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 1) && (_incidSourcesRows[1] != null) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_habitat_typeColumn))
                    return _incidSourcesRows[1].source_habitat_type;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(1, IncidSourcesTable.source_habitat_typeColumn.Ordinal, value);
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource2BoundaryImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 1) && (_incidSourcesRows[1] != null) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_boundary_importanceColumn))
                    return _incidSourcesRows[1].source_boundary_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(1, IncidSourcesTable.source_boundary_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource2HabitatImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 1) && (_incidSourcesRows[1] != null) &&
                    !_incidSourcesRows[1].IsNull(HluDataset.incid_sources.source_habitat_importanceColumn))
                    return _incidSourcesRows[1].source_habitat_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(1, IncidSourcesTable.source_habitat_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource2HabitatImportance));
                OnPropertyChanged(nameof(IncidSource1HabitatImportance));
                OnPropertyChanged(nameof(IncidSource3HabitatImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        #endregion

        #region Source3

        public string Source3Header
        {
            get
            {
                if ((bool)_showGroupHeaders)
                    return "Source 3";
                else
                    return null;
            }
        }

        public HluDataSet.lut_sourcesRow[] Source3Names
        {
            get
            {
                // Return the list of source names, with the clear row if applicable, but
                // exclude the clear row in bulk update mode
                if ((IncidSource3Id != null) && (IncidSource3Id != Int32.MinValue))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_sourcesRow clearRow = HluDataset.lut_sources.Newlut_sourcesRow();
                    clearRow.source_id = -1;
                    clearRow.source_name = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the clear row
                    return new HluDataSet.lut_sourcesRow[] { clearRow }.Concat(SourceNames).ToArray();
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        public Nullable<int> IncidSource3Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = _incidSourcesRows.Concat(tmpRows).ToArray();
                }
                if (_incidSourcesRows[2] != null)
                    return _incidSourcesRows[2].source_id;
                else
                    return null;
            }
            set
            {
                if (value == -1)
                {
                    // delete the row
                    UpdateIncidSourcesRow(2, IncidSourcesTable.source_idColumn.Ordinal, (Nullable<int>)null);

                    // refresh source names lists (all three)
                    OnPropertyChanged(nameof(Source3Names));

                    // clear all fields of Source 3
                    IncidSource3Date = null;
                    IncidSource3HabitatClass = null;
                    IncidSource3HabitatType = null;
                    IncidSource3BoundaryImportance = null;
                    IncidSource3HabitatImportance = null;
                }
                else if (value != null)
                {
                    // Check for equivalent null value when in bulk update mode
                    bool wasNull = (_incidSourcesRows[2] == null || (int)_incidSourcesRows[2]["source_id"] == Int32.MinValue);

                    UpdateIncidSourcesRow(2, IncidSourcesTable.source_idColumn.Ordinal, value);
                    IncidSource3Date = DefaultSourceDate(IncidSource3Date, IncidSource3Id);
                    // if row added refresh source names lists (all three)
                    if (wasNull && (_incidSourcesRows[2] != null)) OnPropertyChanged(nameof(Source3Names));
                }
                OnPropertyChanged(nameof(IncidSource3Date));
                OnPropertyChanged(nameof(IncidSource3HabitatClass));
                OnPropertyChanged(nameof(IncidSource3HabitatType));
                OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource3HabitatImportance));
                OnPropertyChanged(nameof(IncidSource3Enabled));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public bool IncidSource3Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource3Id != null); }
        }

        public Date.VagueDateInstance IncidSource3Date
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 2) && (_incidSourcesRows[2] != null) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_date_startColumn) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_date_endColumn) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_date_typeColumn))
                {
                    Date.VagueDateInstance vd = new(_incidSourcesRows[2].source_date_start,
                        _incidSourcesRows[2].source_date_end, _incidSourcesRows[2].source_date_type,
                        _incidSource3DateEntered?.UserEntry);
                    return vd;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                UpdateIncidSourcesRow(2, IncidSourcesTable.source_date_startColumn.Ordinal, value);
                _incidSource3DateEntered = value;
                OnPropertyChanged(nameof(IncidSource3Date));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource3HabitatClass
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 2) && (_incidSourcesRows[2] != null) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_habitat_classColumn))
                    return _incidSourcesRows[2].source_habitat_class;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(2, IncidSourcesTable.source_habitat_classColumn.Ordinal, value);
                OnPropertyChanged(nameof(Source3HabitatTypeCodes));
                OnPropertyChanged(nameof(IncidSource3HabitatType));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource3HabitatType
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 2) && (_incidSourcesRows[2] != null) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_habitat_typeColumn))
                    return _incidSourcesRows[2].source_habitat_type;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(2, IncidSourcesTable.source_habitat_typeColumn.Ordinal, value);
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public HluDataSet.lut_habitat_typeRow[] Source3HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table
                if (!String.IsNullOrEmpty(IncidSource3HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource3HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name).ToArray();

                    // Set the habitat type if there is only one
                    if ((retArray.Length == 1) && (IncidSource3Id != null))
                    {
                        IncidSource3HabitatType = retArray[0].code;
                        OnPropertyChanged(nameof(IncidSource3HabitatType));
                    }

                    return retArray;
                }
                return null;
            }
        }

        public string IncidSource3BoundaryImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 2) && (_incidSourcesRows[2] != null) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_boundary_importanceColumn))
                    return _incidSourcesRows[2].source_boundary_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(2, IncidSourcesTable.source_boundary_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
                OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        public string IncidSource3HabitatImportance
        {
            get
            {
                if (!CheckSources()) return null;
                if ((_incidSourcesRows.Length > 2) && (_incidSourcesRows[2] != null) &&
                    !_incidSourcesRows[2].IsNull(HluDataset.incid_sources.source_habitat_importanceColumn))
                    return _incidSourcesRows[2].source_habitat_importance;
                else
                    return null;
            }
            set
            {
                UpdateIncidSourcesRow(2, IncidSourcesTable.source_habitat_importanceColumn.Ordinal, value);
                OnPropertyChanged(nameof(IncidSource3HabitatImportance));
                OnPropertyChanged(nameof(IncidSource1HabitatImportance));
                OnPropertyChanged(nameof(IncidSource2HabitatImportance));
                // Flag that the current record has changed so that the apply button
                // will appear.
                Changed = true;
            }
        }

        #endregion

        #endregion

        # region History Tab

        /// <summary>
        /// Gets the incid history and formats it ready for display in the form.
        /// </summary>
        /// <value>
        /// The incid history.
        /// </value>
        public IEnumerable<string> IncidHistory
        {
            get
            {
                if (_incidHistoryRows == null)
                    return null;
                else
                {
                    // Figure out which history columns to display based on the user options
                    // now that all the available history columns are always updated when
                    // creating history even if the user only wants to display some of them.
                    DataColumn[] displayHistoryColumns;
                    int result;
                    displayHistoryColumns = _gisIDColumns.Concat((from s in Settings.Default.HistoryColumnOrdinals.Cast<string>()
                                                                  where Int32.TryParse(s, out result) && (result >= 0) &&
                                                                       (result < _hluDS.incid_mm_polygons.Columns.Count) &&
                                                                       !_gisIDColumnOrdinals.Contains(result)
                                                                  select _hluDS.incid_mm_polygons.Columns[Int32.Parse(s)])).ToArray();

                    return (from r in _incidHistoryRows.OrderByDescending(r => r.history_id)
                            group r by new
                            {
                                r.incid,
                                // Display the modified_date column from the history with both the
                                // date and time to avoid separate updates with identical details
                                // (except the time) being merged together when displayed.
                                modified_date = !r.Ismodified_dateNull() ?
                                    r.modified_date.ToShortDateString() : String.Empty,
                                modified_time = (!r.Ismodified_dateNull() && r.modified_date != r.modified_date.Date) ?
                                    @" at " + r.modified_date.ToLongTimeString() : String.Empty,
                                modified_user_id = r.lut_userRow != null ? r.lut_userRow.user_name :
                                    !r.Ismodified_user_idNull() ? r.modified_user_id : String.Empty,

                                modified_process = r.lut_processRow != null ? r.lut_processRow.description : String.Empty,
                                modified_reason = r.lut_reasonRow != null ? r.lut_reasonRow.description : String.Empty,
                                modified_operation = r.lut_operationRow != null ? r.lut_operationRow.description : String.Empty,

                                // Only show the previous incid if it was different
                                modified_incid = !r.Ismodified_incidNull() ? String.Format("{0}", r.modified_incid == r.incid ? null : "\n\tPrevious INCID: " + r.modified_incid) : String.Empty,

                                //modified_primary = displayHistoryColumns.Count(hc => hc.ColumnName == "habprimary") == 1 ?
                                //    !r.Ismodified_habprimaryNull() ? String.Format("\n\tPrevious Primary: {0}", r.modified_habprimary) : String.Empty : String.Empty,
                                //modified_secondaries = displayHistoryColumns.Count(hc => hc.ColumnName == "habsecond") == 1 ?
                                //    !r.Ismodified_habsecondNull() ? String.Format("\n\tPrevious Secondaries: {0}", r.modified_habsecond) : String.Empty : String.Empty,
                                modified_primary = displayHistoryColumns.Count(hc => hc.ColumnName == "habprimary") == 1 ?
                                    String.Format("\n\tPrevious Primary: {0}", r.modified_habprimary) : String.Empty,
                                modified_secondaries = displayHistoryColumns.Count(hc => hc.ColumnName == "habsecond") == 1 ?
                                    String.Format("\n\tPrevious Secondaries: {0}", r.modified_habsecond) : String.Empty,

                                // Only show the previous values if they are not null
                                modified_determination = displayHistoryColumns.Count(hc => hc.ColumnName == "determqty") == 1 ?
                                    r.lut_quality_determinationRow != null ? String.Format("\n\tPrevious Determination: {0}", r.lut_quality_determinationRow.description) : String.Empty : String.Empty,
                                modified_intepretation = displayHistoryColumns.Count(hc => hc.ColumnName == "interpqty") == 1 ?
                                    r.lut_quality_interpretationRow != null ? String.Format("\n\tPrevious Interpretation: {0}", r.lut_quality_interpretationRow.description) : String.Empty : String.Empty,

                            } into g
                            select
                                String.Format("{0} on {1}{2} by {3}:", g.Key.modified_operation, g.Key.modified_date, g.Key.modified_time, g.Key.modified_user_id) +

                                String.Format("\n\tProcess: {0}", g.Key.modified_process) +
                                String.Format("\n\tReason: {0}", g.Key.modified_reason) +

                                g.Key.modified_incid +
                                g.Key.modified_primary +
                                g.Key.modified_secondaries +
                                g.Key.modified_determination +
                                g.Key.modified_intepretation +

                                // Show the area and length values in the history as hectares and metres.
                                String.Format("\n\tModified Length: {0} [km]", g.Distinct(_histRowEqComp)
                                    .Sum(r => !r.Ismodified_lengthNull() ? Math.Round(r.modified_length / 1000, 3) : 0).ToString("f3")) +
                                String.Format("\n\tModified Area: {0} [ha]", g.Distinct(_histRowEqComp)
                                    .Sum(r => !r.Ismodified_areaNull() ? Math.Round(r.modified_area / 10000, 4) : 0).ToString("f4")))
                                .Take(_historyDisplayLastN);

                }
            }
        }

        #endregion

        #region Record IDs

        public GeometryTypes GisLayerType { get { return _gisLayerType; } }

        public string SiteID { get { return _recIDs.SiteID; } }

        public string HabitatVersion { get { return _recIDs.HabitatVersion; } }

        public string CurrentIncid { get { return _recIDs.CurrentIncid; } }

        public string NextIncid { get { return _recIDs.NextIncid; } }

        private int CurrentIncidBapId { get { return _recIDs.CurrentIncidBapId; } }

        private int NextIncidBapId { get { return _recIDs.NextIncidBapId; } }

        private int NextIncidSourcesId { get { return _recIDs.NextIncidSourcesId; } }

        private int NextIncidSecondaryId { get { return _recIDs.NextIncidSecondaryId; } }

        private int NextIncidConditionId { get { return _recIDs.NextIncidConditionId; } }

        #endregion

        #region SQLUpdater

        /// <summary>
        /// Replaces any string or date delimiters with connection type specific
        /// versions and qualifies any table names.
        /// </summary>
        /// <param name="words">The words.</param>
        /// <returns></returns>
        internal String ReplaceStringQualifiers(String sqlcmd)
        {
            // Check if a table name (delimited by '[]' characters) is found
            // in the sql command.
            int i1 = 0;
            int i2 = 0;
            String start = String.Empty;
            String end = String.Empty;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('[', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf(']', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the table name.
                        string table = sqlcmd.Substring(i1 + 1, i2 - i1 - 1);

                        // Split the table name from the rest of the sql command.
                        if (i1 == 0)
                            start = String.Empty;
                        else
                            start = sqlcmd.Substring(0, i1);

                        if (i2 == sqlcmd.Length - 1)
                            end = String.Empty;
                        else
                            end = sqlcmd.Substring(i2 + 1);

                        // Replace the table name with a qualified table name.
                        sqlcmd = start + _db.QualifyTableName(table) + end;

                        // Reposition the last index.
                        i2 = sqlcmd.Length - end.Length;
                    }
                }
            }

            // Check if any strings are found (delimited by single quotes)
            // in the sql command.
            i1 = 0;
            i2 = 0;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('\'', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf('\'', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the text string.
                        string text = sqlcmd.Substring(i1 + 1, i2 - i1 - 1);

                        // Split the text string from the rest of the sql command.
                        if (i1 == 0)
                            start = String.Empty;
                        else
                            start = sqlcmd.Substring(0, i1);

                        if (i2 == sqlcmd.Length - 1)
                            end = String.Empty;
                        else
                            end = sqlcmd.Substring(i2 + 1);

                        // Replace any wild characters found in the text.
                        if (start.TrimEnd().EndsWith(" LIKE"))
                        {
                            text = text.Replace("_", _db.WildcardSingleMatch);
                            text = text.Replace("%", _db.WildcardManyMatch);
                        }

                        // Replace the text delimiters with the correct delimiters.
                        sqlcmd = start + _db.QuoteValue(text) + end;

                        // Reposition the last index.
                        i2 = sqlcmd.Length - end.Length;
                    }
                }
            }

            // Check if any dates are found (delimited by '#' characters)
            // in the sql command.
            i1 = 0;
            i2 = 0;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('#', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf('#', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the date string.
                        DateTime dt;
                        //DONE: Added success check on TryParse
                        if (DateTime.TryParse(sqlcmd.AsSpan(i1 + 1, i2 - i1 - 1), out dt))
                        {
                            // Split the date string from the rest of the sql command.
                            if (i1 == 0)
                                start = String.Empty;
                            else
                                start = sqlcmd.Substring(0, i1);

                            if (i2 == sqlcmd.Length - 1)
                                end = String.Empty;
                            else
                                end = sqlcmd.Substring(i2 + 1);

                            // Replace the date delimiters with the correct delimiters.
                            sqlcmd = start + _db.QuoteValue(dt) + end;

                            // Reposition the last index.
                            i2 = sqlcmd.Length - end.Length;
                        }
                    }
                }
            }
            return sqlcmd;
        }

        #endregion

        #region Validation

        //TODO: ArcGIS
        //internal bool HaveGisApp
        //{
        //    get { return _gisApp != null && _gisApp.IsRunning; }
        //}

        internal bool IsCompleteRow(DataRow r)
        {
            if (r == null) return false;

            foreach (DataColumn c in r.Table.Columns)
            {
                if (!c.AllowDBNull && r.IsNull(c)) return false;
            }

            return true;
        }

        private List<string[]> ValidateCondition()
        {
            List<string[]> errors = [];

            // Validate the condition fields if no condition has been entered
            if (IncidCondition == null)
            {
                if (IncidConditionQualifier != null)
                    errors.Add(["IncidConditionQualifier", "Error: Condition qualifier is not valid without a condition"]);
                if (IncidConditionDate != null)
                    errors.Add(["IncidConditionDate", "Error: Condition date is not valid without a condition"]);
            }
            else
            {
                // Check the condition fields if a condition has been entered
                if (IncidConditionQualifier == null)
                    errors.Add(["IncidConditionQualifier", "Error: Condition qualifier is mandatory for a condition"]);
                if (IncidConditionDate == null)
                    errors.Add(["IncidConditionDate", "Error: Condition date is mandatory for a condition"]);
                else if (IncidConditionDate.IsBad)
                    errors.Add(["IncidConditionDate", "Error: Invalid condition vague date"]);
            }

            return errors;
        }

        private List<string[]> ValidateSource1()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource1Id != null && IncidSource1Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource1Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource1Date == null)
                    errors.Add(["IncidSource1Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource1Date.IsBad)
                    errors.Add(["IncidSource1Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource1HabitatClass))
                    errors.Add(["IncidSource1HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource1HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource1HabitatType))
                    errors.Add(["IncidSource1HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource1BoundaryImportance))
                {
                    errors.Add(["IncidSource1BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "1", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "1", errors);
                    //---------------------------------------------------------------------
                }
                if (String.IsNullOrEmpty(IncidSource1HabitatImportance))
                {
                    errors.Add([ "IncidSource1HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "1", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "1", errors);
                    //---------------------------------------------------------------------
                }
            }
            else
            {
                //---------------------------------------------------------------------
                // CHANGED: CR49 Process proposed OSMM Updates
                // Validation for OSMM Bulk Update mode.
                //
                if ((OSMMBulkUpdateMode == true) &&
                    (IncidSource2Id == null || IncidSource2Id == Int32.MinValue) &&
                    (IncidSource3Id == null || IncidSource3Id == Int32.MinValue))
                    errors.Add([ "IncidSource1Id",
                        "Error: At least one source must be specified" ]);
                if (IncidSource1Date != null)
                    errors.Add([ "IncidSource1Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatClass))
                    errors.Add([ "IncidSource1HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatType))
                    errors.Add([ "IncidSource1HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1BoundaryImportance))
                    errors.Add([ "IncidSource1BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatImportance))
                    errors.Add([ "IncidSource1HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
                //---------------------------------------------------------------------
            }

            return errors;
        }

        private List<string[]> ValidateSource2()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource2Id != null && IncidSource2Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource2Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource2Date == null)
                    errors.Add(["IncidSource2Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource2Date.IsBad)
                    errors.Add(["IncidSource2Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource2HabitatClass))
                    errors.Add(["IncidSource2HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource2HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource2HabitatType))
                    errors.Add(["IncidSource2HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource2BoundaryImportance))
                {
                    errors.Add(["IncidSource2BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "2", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "2", errors);
                    //---------------------------------------------------------------------
                }
                if (String.IsNullOrEmpty(IncidSource2HabitatImportance))
                {
                    errors.Add([ "IncidSource2HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "2", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "2", errors);
                    //---------------------------------------------------------------------
                }
            }
            else
            {
                if (IncidSource2Date != null)
                    errors.Add([ "IncidSource2Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatClass))
                    errors.Add([ "IncidSource2HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatType))
                    errors.Add([ "IncidSource2HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2BoundaryImportance))
                    errors.Add([ "IncidSource2BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatImportance))
                    errors.Add([ "IncidSource2HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
            }

            return errors;
        }

        private List<string[]> ValidateSource3()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource3Id != null && IncidSource3Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource3Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource3Date == null)
                    errors.Add(["IncidSource3Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource3Date.IsBad)
                    errors.Add(["IncidSource3Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource3HabitatClass))
                    errors.Add(["IncidSource3HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource3HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource3HabitatType))
                    errors.Add(["IncidSource3HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource3BoundaryImportance))
                {
                    errors.Add(["IncidSource3BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "3", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "3", errors);
                    //---------------------------------------------------------------------
                }
                if (String.IsNullOrEmpty(IncidSource3HabitatImportance))
                {
                    errors.Add([ "IncidSource3HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "3", skipVal, errors);
                    //---------------------------------------------------------------------
                    // CHANGED: CR1 (Boundary and Habitat Importance)
                    /// Validates the source importances by ensuring that boundary and habitat importance
                    /// values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "3", errors);
                    //---------------------------------------------------------------------
                }
            }
            else
            {
                if (IncidSource3Date != null)
                    errors.Add([ "IncidSource3Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatClass))
                    errors.Add([ "IncidSource3HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatType))
                    errors.Add([ "IncidSource3HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3BoundaryImportance))
                    errors.Add([ "IncidSource3BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatImportance))
                    errors.Add([ "IncidSource3HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
            }

            return errors;
        }

        //---------------------------------------------------------------------
        // CHANGED: CR1 (Boundary and Habitat Importance)
        //
        /// <summary>
        /// Validates the source importances by ensuring that boundary and habitat importance
        /// values are applied in order (as specified in the settings).
        /// </summary>
        /// <param name="propNamePat">Pattern for regular expression match of property name in current class.</param>
        /// <param name="propNamePatWildcard">Wildcard element in propNamePat.</param>
        /// <param name="propNameWildcardValCurrProp">Value of propNamePatWildcard for current property to be validated.</param>
        /// <param name="errors">List of errors, composed of name of property in error and error message.
        /// The error message is built by splitting propNamePat on propNamePatWildcard and prepending the
        /// last element of the split array with blanks added in front of capital letters to the string
        /// "must be applied in the order ...".</param>
        private void ValidateSourceImportances(string propNamePat, string propNamePatWildcard,
            string propNameWildcardValCurrProp, List<string[]> errors)
        {
            string propNameCheck = propNamePat.Replace(propNamePatWildcard, propNameWildcardValCurrProp);
            PropertyInfo propInf = this.GetType().GetProperty(propNameCheck);
            if (propInf == null) return;

            string skipVal = Settings.Default.SourceImportanceSkip;
            string ord1val = Settings.Default.SourceImportanceApply1;
            string ord2val = Settings.Default.SourceImportanceApply2;
            string ord3val = Settings.Default.SourceImportanceApply3;

            object checkVal = propInf.GetValue(this, null);
            if ((checkVal == null) || checkVal.Equals(skipVal)) return;

            string[] split = propNamePat.Split([propNamePatWildcard], StringSplitOptions.None);
            string errMsg = String.Format("Error: {0}", split[split.Length - 1]);

            //DONE: Aggregate
            errMsg = string.Join(" ", CapitalisedRegex().Matches(errMsg).Cast<Match>().Select(m => errMsg.Substring(m.Index, m.Length)
                .Concat(string.Format(" must be applied in the order {0}, {1} then {2}", ord1val, ord2val, ord3val))));

            //errMsg = Regex.Matches(errMsg, @"[A-Z][^A-Z\s]*").Cast<Match>()
            //    .Aggregate(new(), (sb, m) => sb.Append(errMsg.Substring(m.Index, m.Length)).Append(' '))
            //    .AppendFormat("must be applied in the order {0}, {1} then {2}", ord1val, ord2val, ord3val).ToString();

            if (!String.IsNullOrEmpty(ord1val))
            {
                int ord1Sources = 0;
                foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => Regex.IsMatch(pn.Name, propNamePat)))
                {
                    object compVal = pi.GetValue(this, null);
                    if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(ord1val))
                    {
                        ord1Sources += 1;
                    }
                }
                if (ord1Sources == 0 && checkVal.Equals(ord2val))
                    errors.Add([propNameCheck, errMsg]);
            }

            if (!String.IsNullOrEmpty(ord2val))
            {
                int ord2Sources = 0;
                foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => Regex.IsMatch(pn.Name, propNamePat)))
                {
                    object compVal = pi.GetValue(this, null);
                    if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(ord2val))
                    {
                        ord2Sources += 1;
                    }
                }
                if (ord2Sources == 0 && checkVal.Equals(ord3val))
                    errors.Add([propNameCheck, errMsg]);
            }

        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Checks all properties of current class whose names follow a specified pattern for duplicate values.
        /// </summary>
        /// <param name="propNamePat">Pattern for regular expression match of property name in current class.</param>
        /// <param name="propNamePatWildcard">Wildcard element in propNamePat.</param>
        /// <param name="propNameWildcardValCurrProp">Value of propNamePatWildcard for current property to be validated.</param>
        /// <param name="skipVal">Value that may occur repeatedly (e.g. "none").</param>
        /// <param name="errors">List of errors, composed of name of property in error and error message.
        /// The error message is built by splitting propNamePat on propNamePatWildcard and prepending the
        /// last element of the split array with blanks added in front of capital letters to the string
        /// "of two sources cannot be equal for the same INCID".</param>
        private void ValidateSourceDuplicates(string propNamePat, string propNamePatWildcard,
            string propNameWildcardValCurrProp, object skipVal, List<string[]> errors)
        {
            string propNameCheck = propNamePat.Replace(propNamePatWildcard, propNameWildcardValCurrProp);
            PropertyInfo propInf = this.GetType().GetProperty(propNameCheck);
            if (propInf == null) return;

            object checkVal = propInf.GetValue(this, null);
            if ((checkVal == null) || checkVal.Equals(skipVal)) return;

            string[] split = propNamePat.Split([propNamePatWildcard], StringSplitOptions.None);
            string errMsg = String.Format("Error: {0}", split[split.Length - 1]);

            //DONE: Aggregate
            errMsg = string.Join(" ", CapitalisedRegex().Matches(errMsg).Cast<Match>().Select(m => errMsg.Substring(m.Index, m.Length)
                .Concat(" of two sources cannot be equal for the same INCID")));

            //errMsg = Regex.Matches(errMsg, @"[A-Z][^A-Z\s]*").Cast<Match>()
            //    .Aggregate(new(), (sb, m) => sb.Append(errMsg.Substring(m.Index, m.Length)).Append(' '))
            //    .Append("of two sources cannot be equal for the same INCID").ToString();

            foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => pn.Name != propNameCheck && Regex.IsMatch(pn.Name, propNamePat)))
            {
                if (pi.Name == propNameCheck) continue;

                object compVal = pi.GetValue(this, null);
                if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(checkVal))
                {
                    errors.Add([propNameCheck, errMsg]);
                    errors.Add([pi.Name, errMsg]);
                }
            }
        }

        #endregion

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets the error message for the property with the given column name.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="errorList"></param>
        /// <returns></returns>
        private string ErrorMessage(string columnName, List<string[]> errorList)
        {
            if (errorList != null)
            {
                IEnumerable<string[]> err = errorList.Where(s => s[0] == columnName);
                if (err.Any()) return err.ElementAt(0)[1];
            }
            return null;
        }

        /// <summary>
        /// Gets a list of all of the errors.
        /// </summary>
        /// <param name="errors"></param>
        /// <returns></returns>
        private string ErrorMessageList(List<string[]> errors)
        {
            if ((errors == null) || (errors.Count == 0)) return null;

            StringBuilder sbMsg = new();

            foreach (string[] e in errors)
            {
                if ((e.Length == 2) && (!String.IsNullOrEmpty(e[1])))
                    sbMsg.Append(Environment.NewLine).Append(e[1]);
            }

            if (sbMsg.Length > 0)
            {
                sbMsg.Remove(0, 1);
                return sbMsg.ToString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a string of all of the errors.
        /// </summary>
        public string Error
        {
            get
            {
                // Show errors in bulk update mode.
                if ((_incidCurrentRow == null) ||
                    (_incidCurrentRow.RowState == DataRowState.Detached && _bulkUpdateMode == false)) return null;

                StringBuilder error = new();

                //TODO: Remove from error checking now on ribbon?
                if (String.IsNullOrEmpty(Reason))
                    error.Append(Environment.NewLine).Append("Reason is mandatory for the history trail of every update");

                //TODO: Remove from error checking now on ribbon?
                if (String.IsNullOrEmpty(Process))
                    error.Append(Environment.NewLine).Append("Process is mandatory for the history trail of every update");

                // If not in bulk update mode.
                if (_bulkUpdateMode == false)
                {
                    if (String.IsNullOrEmpty(IncidBoundaryBaseMap))
                        error.Append(Environment.NewLine).Append("Boundary basemap is mandatory for every INCID");

                    if (String.IsNullOrEmpty(IncidDigitisationBaseMap))
                        error.Append(Environment.NewLine).Append("Digitisation basemap is mandatory for every INCID");

                    // If the quality validation is mandatory.
                    if (_qualityValidation == 1)
                    {
                        if (String.IsNullOrEmpty(IncidQualityDetermination))
                            error.Append(Environment.NewLine).Append("Quality determination is mandatory for every INCID");

                        if (String.IsNullOrEmpty(IncidQualityInterpretation))
                            error.Append(Environment.NewLine).Append("Quality interpretation is mandatory for every INCID");

                        if ((!String.IsNullOrEmpty(IncidQualityComments) && String.IsNullOrEmpty(IncidQualityInterpretation)))
                            error.Append(Environment.NewLine).Append("Interpretation comments are invalid without interpretation quality");
                    }
                }

                // If the habitat primary code is missing and not in bulk update mode.
                if (String.IsNullOrEmpty(IncidPrimary) && _bulkUpdateMode == false)
                    error.Append(Environment.NewLine).Append("Primary Habitat is mandatory for every INCID");

                // If the habitat secondary codes validation is error.
                if (_habitatSecondaryCodeValidation > 1)
                {
                    // If there are any secondary codes that are mandatory.
                    if (_secondaryCodesMandatory != null && _secondaryCodesMandatory.Any())
                    {
                        IEnumerable<string> secondaryCodes = _incidSecondaryHabitats.Select(c => c.secondary_habitat);

                        // If there aren't any secondary codes or there are some mandatory codes missing.
                        if ((secondaryCodes == null) ||
                            (_secondaryCodesMandatory.Except(secondaryCodes).Any()))
                        {
                            error.Append("One or more mandatory secondary habitats for habitat type not found");
                        }
                    }
                }

                // If there are any IHS field errors then show an error on the tab label.
                if (HabitatErrors != null && HabitatErrors.Count > 0)
                    error.Append(Environment.NewLine).Append("One or more habitat fields are in error");

                // If there are any Priority field errors then show an error on the tab label.
                if (PriorityErrors != null && PriorityErrors.Count > 0)
                    error.Append(Environment.NewLine).Append("One or more priority fields are in error");

                // If there are any Detail field errors then show an error on the tab label.
                if (DetailsErrors != null && DetailsErrors.Count > 0)
                    error.Append(Environment.NewLine).Append("One or more detail fields are in error");

                if ((ConditionErrors != null) && (ConditionErrors.Count > 0))
                    error.Append(Environment.NewLine).Append(ErrorMessageList(ConditionErrors));

                // If there are any Source field errors then show an error on the tab label.
                if (((Source1Errors != null) && (Source1Errors.Count > 0)) ||
                    ((Source2Errors != null) && (Source2Errors.Count > 0)) ||
                    ((Source3Errors != null) && (Source3Errors.Count > 0)))
                    error.Append(Environment.NewLine).Append("One or more source fields are in error");

                // Store the Source field errors so that they can be checked
                // at the end to see if the Source tab label should also be flagged
                // as in error.
                //Source1Errors = ValidateSource1();
                if ((Source1Errors != null) && (Source1Errors.Count > 0))
                    error.Append(Environment.NewLine).Append(ErrorMessageList(Source1Errors));

                //Source2Errors = ValidateSource2();
                if ((Source2Errors != null) && (Source2Errors.Count > 0))
                    error.Append(Environment.NewLine).Append(ErrorMessageList(Source2Errors));

                //Source3Errors = ValidateSource3();
                if ((Source3Errors != null) && (Source3Errors.Count > 0))
                    error.Append(Environment.NewLine).Append(ErrorMessageList(Source3Errors));

                if (error.Length > 1)
                    return error.Remove(0, 1).ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the error message for the property with the given column name.
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public string this[string columnName]
        {
            get
            {
                // Show errors in bulk update mode.
                if ((_incidCurrentRow == null) ||
                    (_incidCurrentRow.RowState == DataRowState.Detached && _bulkUpdateMode == false)) return null;

                string error = null;

                //TODO: Remove from error checking now on ribbon?
                switch (columnName)
                {
                    case "Reason":
                        if (String.IsNullOrEmpty(Reason))
                            error = "Error: Reason is mandatory for the history trail of every INCID";
                        break;
                    case "Process":
                        if (String.IsNullOrEmpty(Process))
                            error = "Error: Process is mandatory for the history trail of every INCID";
                        break;

                }

                // Check the individual field errors to see if their parent tab label
                // should be flagged as also in error.
                switch (columnName)
                {
                    case "Incid":
                        break;

                    case "HabitatTabLabel":
                        // If there are any habitat field warnings.
                        if (HabitatWarnings != null && HabitatWarnings.Count > 0)
                            error = "Warning: One or more habitat fields have a warning";
                        // If there are any habitat field errors.
                        if (HabitatErrors != null && HabitatErrors.Count > 0)
                            error = "Error: One or more habitat fields are in error";
                        break;

                    case "IncidPrimary":
                        // If the field is in error add the field name to the list of errors
                        // for the parent tab. Otherwise remove the field from the list.
                        if (String.IsNullOrEmpty(IncidPrimary) && _bulkUpdateMode == false)
                        {
                            error = "Error: Primary Habitat is mandatory for every INCID";
                            AddErrorList(ref _habitatErrors, columnName);
                        }
                        else
                        {
                            DelErrorList(ref _habitatErrors, columnName);
                        }
                        OnPropertyChanged(nameof(HabitatTabLabel));
                        break;

                    case "HabitatSecondariesMandatory":
                        // If the habitat secondary codes validation is warning or error.
                        if (_habitatSecondaryCodeValidation > 0)
                        {
                            // If there are any secondary codes that are mandatory.
                            if (_secondaryCodesMandatory != null && _secondaryCodesMandatory.Any())
                            {
                                IEnumerable<string> secondaryCodes = _incidSecondaryHabitats.Select(c => c.secondary_habitat);

                                // If there aren't any secondary codes or there are some mandatory codes missing.
                                if ((secondaryCodes == null) ||
                                    (_secondaryCodesMandatory.Except(secondaryCodes).Any()))
                                {
                                    // If the habitat secondary codes validation is error.
                                    if (_habitatSecondaryCodeValidation > 1)
                                    {
                                        AddErrorList(ref _habitatErrors, columnName);
                                        error = "Error: One or more mandatory secondary habitats for habitat type not found";
                                    }
                                    // If the habitat secondary codes validation is warning.
                                    else
                                    {
                                        AddErrorList(ref _habitatWarnings, columnName);
                                        error = "Warning: One or more mandatory secondary habitats for habitat type not found";
                                    }
                                }
                                else
                                {
                                    DelErrorList(ref _habitatErrors, columnName);
                                    DelErrorList(ref _habitatWarnings, columnName);
                                }
                            }
                            else
                            {
                                DelErrorList(ref _habitatErrors, columnName);
                                DelErrorList(ref _habitatWarnings, columnName);
                            }
                        }
                        else
                        {
                            DelErrorList(ref _habitatErrors, columnName);
                            DelErrorList(ref _habitatWarnings, columnName);
                        }
                        OnPropertyChanged(nameof(HabitatTabLabel));
                        break;

                    case "IncidCondition":
                    case "IncidConditionQualifier":
                    case "IncidConditionDate":
                        // Store the Source1 field errors so that they can be checked
                        // later to see if the Source tab label should also be flagged
                        // as in error.
                        ConditionErrors = ValidateCondition();
                        error = ErrorMessage(columnName, ConditionErrors);
                        OnPropertyChanged(nameof(DetailsTabLabel));
                        break;

                    case "PriorityTabLabel":
                        // If there are any priority field warnings.
                        if (PriorityWarnings != null && PriorityWarnings.Count > 0)
                            error = "Warning: One or more priority fields have a warning";

                        // If there are any priority field errors.
                        if (PriorityErrors != null && PriorityErrors.Count > 0)
                            error = "Error: One or more priority fields are in error";
                        break;

                    case "DetailsTabLabel":
                        // If there are any details or condition field warnings.
                        if ((DetailsWarnings != null && DetailsWarnings.Count > 0) ||
                            (ConditionWarnings != null && ConditionWarnings.Count > 0))
                            error = "Warning: One or more detail fields have a warning";

                        // If there are any details or condition field errors.
                        if ((DetailsErrors != null && DetailsErrors.Count > 0) ||
                            (ConditionErrors != null && ConditionErrors.Count > 0))
                            error = "Error: One or more details fields are in error";
                        break;

                    case "SourcesTabLabel":
                        // If there are any source field warnings.
                        if ((Source1Warnings != null && Source1Warnings.Count > 0) ||
                            (Source2Warnings != null && Source2Warnings.Count > 0) ||
                            (Source3Warnings != null && Source3Warnings.Count > 0))
                            error = "Warning: One or more source fields have a warning";

                        // If there are any source field errors.
                        if ((Source1Errors != null && Source1Errors.Count > 0) ||
                            (Source2Errors != null && Source2Errors.Count > 0) ||
                            (Source3Errors != null && Source3Errors.Count > 0))
                            error = "Error: One or more source fields are in error";
                        break;

                    case "IncidSource1Id":
                    case "IncidSource1Date":
                    case "IncidSource1HabitatClass":
                    case "IncidSource1HabitatType":
                    case "IncidSource1BoundaryImportance":
                    case "IncidSource1HabitatImportance":
                        // Store the Source1 field errors so that they can be checked
                        // later to see if the Source tab label should also be flagged
                        // as in error.
                        Source1Errors = ValidateSource1();
                        error = ErrorMessage(columnName, Source1Errors);
                        OnPropertyChanged(nameof(SourcesTabLabel));
                        break;

                    case "IncidSource2Id":
                    case "IncidSource2Date":
                    case "IncidSource2HabitatClass":
                    case "IncidSource2HabitatType":
                    case "IncidSource2BoundaryImportance":
                    case "IncidSource2HabitatImportance":
                        // Store the Source2 field errors so that they can be checked
                        // later to see if the Source tab label should also be flagged
                        // as in error.
                        Source2Errors = ValidateSource2();
                        error = ErrorMessage(columnName, Source2Errors);
                        OnPropertyChanged(nameof(SourcesTabLabel));
                        break;

                    case "IncidSource3Id":
                    case "IncidSource3Date":
                    case "IncidSource3HabitatClass":
                    case "IncidSource3HabitatType":
                    case "IncidSource3BoundaryImportance":
                    case "IncidSource3HabitatImportance":
                        // Store the Source3 field errors so that they can be checked
                        // later to see if the Source tab label should also be flagged
                        // as in error.
                        Source3Errors = ValidateSource3();
                        error = ErrorMessage(columnName, Source3Errors);
                        OnPropertyChanged(nameof(SourcesTabLabel));
                        break;
                }

                // Warnings with in Bulk Update mode.
                switch (columnName)
                {
                    case "NumIncidSelectedMap":
                        if (_incidsSelectedMapCount == 0)
                            error = "Warning: No database incids are selected in map";
                        else if (_incidsSelectedMapCount < _incidsSelectedDBCount)
                            error = "Warning: Not all database incids are selected in map";
                        break;

                    case "NumToidSelectedMap":
                        if (_toidsSelectedMapCount == 0)
                            error = "Warning: No database toids are selected in map";
                        else if (_toidsSelectedMapCount < _toidsSelectedDBCount)
                            error = "Warning: Not all database toids are selected in map";
                        break;

                    case "NumFragmentsSelectedMap":
                        if (_fragsSelectedMapCount == 0)
                            error = "Warning: No database fragments are selected in map";
                        else if (_fragsSelectedMapCount < _fragsSelectedDBCount)
                            error = "Warning: Not all database fragments are selected in map";
                        break;

                    case "IncidOSMMUpdateStatus":
                        if (_incidOSMMUpdatesStatus != null & _incidOSMMUpdatesStatus >= 0)
                            error = "Warning: OSMM UpdateAsync is outstanding";
                        break;
                }

                // Exit if in OSMM bulk update mode.
                if (_osmmUpdateMode == true) return null;

                // Additional checks if not in bulk update mode.
                if (_bulkUpdateMode == false)
                {
                    switch (columnName)
                    {
                        case "IncidBoundaryBaseMap":
                            // If the field is in error add the field name to the list of errors
                            // for the parent tab. Otherwise remove the field from the list.
                            if (String.IsNullOrEmpty(IncidBoundaryBaseMap))
                            {
                                error = "Error: Boundary basemap is mandatory for every INCID";
                                AddErrorList(ref _detailsErrors, columnName);
                            }
                            else
                            {
                                DelErrorList(ref _detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidDigitisationBaseMap":
                            // If the field is in error add the field name to the list of errors
                            // for the parent tab. Otherwise remove the field from the list.
                            if (String.IsNullOrEmpty(IncidDigitisationBaseMap))
                            {
                                error = "Error: Digitisation basemap is mandatory for every INCID";
                                AddErrorList(ref _detailsErrors, columnName);
                            }
                            else
                            {
                                DelErrorList(ref _detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidQualityDetermination":
                            // If the quality validation is mandatory.
                            if ((_qualityValidation == 1)
                                && (String.IsNullOrEmpty(IncidQualityDetermination)))
                            {
                                error = "Error: Determination quality is mandatory for every INCID";
                                AddErrorList(ref _detailsErrors, columnName);
                            }
                            else
                            {
                                DelErrorList(ref _detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidQualityInterpretation":
                            // If the quality validation is mandatory.
                            if ((_qualityValidation == 1)
                                && (String.IsNullOrEmpty(IncidQualityInterpretation)))
                            {
                                error = "Error: Interpretation quality is mandatory for every INCID";
                                AddErrorList(ref _detailsErrors, columnName);
                            }
                            else
                            {
                                DelErrorList(ref _detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidQualityComments":
                            // If the quality validation is mandatory.
                            if ((_qualityValidation == 1)
                                && (!String.IsNullOrEmpty(IncidQualityComments))
                                && String.IsNullOrEmpty(IncidQualityInterpretation))
                            {
                                error = "Error: Interpretation comments are invalid without interpretation quality";
                                AddErrorList(ref _detailsErrors, columnName);
                            }
                            else
                            {
                                DelErrorList(ref _detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;
                    }
                }

                // dirty commands registered with CommandManager so they are queried to see if they can execute now
                CommandManager.InvalidateRequerySuggested();

                OnPropertyChanged(nameof(CanCopy));
                OnPropertyChanged(nameof(CanPaste));

                return error;
            }
        }

        /// <summary>
        /// Adds a column name to the list of errors.
        /// </summary>
        /// <param name="errorList"></param>
        /// <param name="columnName"></param>
        public void AddErrorList(ref List<string> errorList, string columnName)
        {
            if (!errorList.Contains(columnName))
                errorList.Add(columnName);
        }

        /// <summary>
        /// Removes a column name from the list of errors.
        /// </summary>
        /// <param name="errorList"></param>
        /// <param name="columnName"></param>
        public void DelErrorList(ref List<string> errorList, string columnName)
        {
            errorList.Remove(columnName);
        }

        /// <summary>
        /// Resets all warnings and errors.
        /// </summary>
        public void ResetWarningsErrors()
        {
            _habitatWarnings = [];
            _habitatErrors = [];
            _priorityWarnings = [];
            _priorityErrors = [];
            _detailsWarnings = [];
            _detailsErrors = [];
            _conditionWarnings = null;
            _conditionErrors = null;
            _source1Warnings = null;
            _source2Warnings = null;
            _source3Warnings = null;
            _source1Errors = null;
            _source2Errors = null;
            _source3Errors = null;
        }

        #endregion IDataErrorInfo Members

        /// <summary>
        /// Defines a compiled regular expression that matches capitalized words in a string.
        /// </summary>
        /// <remarks>
        /// - The pattern `[A-Z][^A-Z]*` matches:
        ///   - An uppercase letter (`[A-Z]`) at the beginning of a word.
        ///   - Followed by zero or more non-uppercase letters (`[^A-Z]*`).
        /// - This effectively extracts words that start with a capital letter and continue until
        ///   the next capital letter is encountered, which is useful for splitting camel case or
        ///   Pascal case strings.
        /// - The `[GeneratedRegex]` attribute compiles the regex at compile time for performance benefits.
        /// </remarks>
        /// <returns>A `Regex` instance that can be used to match capitalized words in a string.</returns>
        [GeneratedRegex("[A-Z][^A-Z]*")]
        private static partial Regex CapitalisedRegex();

    }
}
