// HLUTool is used to view and maintain habitat and land use GIS data.
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
using System.Windows.Media.Imaging;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Data.Model.HluDataSetTableAdapters;
using HLU.Date;
//using HLU.GISApplication;
using HLU.GISApplication.ArcGIS;
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
using System.Windows.Media;

using CommandType = System.Data.CommandType;


namespace HLU.UI.ViewModel
{

    internal class ViewModelWindowMain_TEMP
    {
        #region Fields

        #region Commands

        private ICommand _navigateFirstCommand;
        private ICommand _navigatePreviousCommand;
        private ICommand _navigateNextCommand;
        private ICommand _navigateLastCommand;
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
        private ICommand _switchGISLayerCommand;
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
        private ICommand _optionsCommand;
        private ICommand _aboutCommand;

        #endregion Commands

        #region Windows

        private WindowMainCopySwitches _copySwitches = new();
        private WindowAbout _windowAbout;
        private ViewModelWindowAbout _viewModelAbout;
        private WindowOptions _windowOptions;
        private ViewModelOptions _viewModelOptions;
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
        private WindowSwitchGISLayer _windowSwitchGISLayer;
        private ViewModelWindowSwitchGISLayer _viewModelSwitchGISLayer;
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
        private ArcMapApp _gisApp;
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

        private HluDataSet.lut_primaryRow[] _primaryCodes;
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

        private int _mapWindowsCount;
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

        #endregion Fields

        #region Static Variables

        private string _historyGeometry1ColumnName;
        private string _historyGeometry2ColumnName;
        internal string LutDescriptionFieldName;
        internal int LutDescriptionFieldOrdinal;
        internal string LutSourceFieldName;
        internal int LutSourceFieldOrdinal;
        internal string LutUserFieldName;
        internal int LutUserFieldOrdinal;
        internal int IncidPageSize;

        #endregion Static fields

        #endregion

        #region Internal properties

        internal ArcMapApp GISApplication
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
                //TODO - Startup
                //if (_hluDS == null) Initialize();
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

        public string HistoryGeometry1ColumnName { get => _historyGeometry1ColumnName; set => _historyGeometry1ColumnName = value; }

        public string HistoryGeometry2ColumnName { get => _historyGeometry2ColumnName; set => _historyGeometry2ColumnName = value; }

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
            //TODO - Startup
            //set { if ((IncidCurrentRow != null) && (value != null)) _incidLastModifiedUser = value; }
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
                //TODO - Startup
                //// If this is another change by the user but the data is no longer
                //// dirty (i.e. the user has reversed out their changes) then
                //// reset the changed flag.
                //if (value == true && !IsDirty)
                //    _changed = false;
                //else
                //    _changed = value;
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

        internal int DBConnectionTimeout
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
            //ProgressUpdate(processingMessage, -1, -1);

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


        //TODO: To add to initialisation
        //// Database options
        //_dbConnectionTimeout = Settings.Default.DbConnectionTimeout;

        //// GIS/Export options
        //_minZoom = Settings.Default.MinAutoZoom;

        //// History options
        //DataColumn[] _historyColumns;
        //_historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

        //// Interface options
        //_preferredHabitatClass = Settings.Default.PreferredHabitatClass;
        //_showGroupHeaders = Settings.Default.ShowGroupHeaders;
        //_showIHSTab = Settings.Default.ShowIHSTab;
        //_showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
        //_showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
        //_showNVCCodes = Settings.Default.ShowNVCCodes;
        //_showHabitatSummary = Settings.Default.ShowHabitatSummary;
        //_showOSMMUpdates = Settings.Default.ShowOSMMUpdatesOption;
        //_preferredSecondaryGroup = Settings.Default.PreferredSecondaryGroup;
        //_secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;
        //_secondaryCodeDelimiter = Settings.Default.SecondaryCodeDelimiter;

        //// Updates options
        //_subsetUpdateAction = Settings.Default.SubsetUpdateAction;
        //_clearIHSUpdateAction = Settings.Default.ClearIHSUpdateAction;
        //_notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;
        //_resetOSMMUpdatesStatus = Settings.Default.ResetOSMMUpdatesStatus;
        //_habitatSecondaryCodeValidation = Settings.Default.HabitatSecondaryCodeValidation;
        //_primarySecondaryCodeValidation = Settings.Default.PrimarySecondaryCodeValidation;
        //_qualityValidation = Settings.Default.QualityValidation;
        //_potentialPriorityDetermQtyValidation = Settings.Default.PotentialPriorityDetermQtyValidation;

        //// Filter options
        //_warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;

        //// Dates options
        //// None



        //TODO: To add to initialisation
        //_codeDeleteRow = Settings.Default.CodeDeleteRow;
        //_autoZoomSelection = Settings.Default.AutoZoomSelection;
        //_autoSelectOnGis = Settings.Default.AutoSelectOnGis;
        //_codeAnyRow = Settings.Default.CodeAnyRow;
        ////private bool _bulkUpdatePrimaryBap = Settings.Default.BulkUpdatePotentialBap;


        //TODO: To add to initialisation
        //_historyGeometry1ColumnName = Settings.Default.HistoryGeometry1ColumnName;
        //_historyGeometry2ColumnName = Settings.Default.HistoryGeometry2ColumnName;
        //LutDescriptionFieldName = Settings.Default.LutDescriptionFieldName;
        //LutDescriptionFieldOrdinal = Settings.Default.LutDescriptionFieldOrdinal;
        //LutSourceFieldName = Settings.Default.LutSourceFieldName;
        //LutSourceFieldOrdinal = Settings.Default.LutSourceFieldOrdinal;
        //LutUserFieldName = Settings.Default.LutUserFieldName;
        //LutUserFieldOrdinal = Settings.Default.LutUserFieldOrdinal;
        //IncidPageSize = Settings.Default.IncidTablePageSize;


    }
}
