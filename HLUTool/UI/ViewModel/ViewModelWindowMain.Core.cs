// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
// Copyright © 2019-2022 Greenspace Information for Greater London CIC
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

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Date;
using HLU.Enums;
using HLU.Exceptions;
using HLU.GISApplication;
using HLU.Helpers;
using HLU.Properties;
using HLU.UI.Services;
using HLU.UI.UserControls.Toolbar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Core infrastructure partial for ViewModelWindowMain.
    /// Contains: Fields, Properties, Constructor, Lifecycle management, Shared state.
    /// </summary>
    partial class ViewModelWindowMain : PanelViewModelBase, IDataErrorInfo
    {
        #region Fields

        #region Fields - Dataset

        private HluDataSet _hluDS;
        private TableAdapterManager _hluTableAdapterMgr;
        private IEnumerable<DataRelation> _hluDataRelations;

        #endregion Fields - Dataset

        #region Fields - Current Record State

        private RecordIds _recIDs;
        private int _incidCurrentRowIndex;
        private DataTable _incidSelection;
        private DataTable _incidMMPolygonSelection;

        private HluDataSet.incidRow _incidCurrentRow;
        private HluDataSet.incidRow _incidCurrentRowClone;

        private int _incidRowCount;
        private int _incidPageRowNo;
        private int _incidPageRowNoMin = 0;
        private int _incidPageRowNoMax = 0;

        #endregion Fields - Current Record State

        #region Fields - Configuration Settings

        private XmlSettingsManager _xmlSettingsManager;
        private AddInSettings _addInSettings;

        #endregion Fields - Configuration Settings

        #region Fields - Initialisation

        private bool _initialised = false;
        private bool _inError = false;
        private Task _initializationTask;
        private Exception _initializationException;

        #endregion Fields - Initialisation

        #region Fields - Edit State

        // Primary state variables that track what's happening now
        private WorkMode _workMode = WorkMode.None;
        private bool _changed = false;
        private bool _readingMap = false;
        private bool _moving = false;
        private bool _saving = false;
        private bool _splitting = false;
        private bool _filteredByMap = false;
        private bool _osmmUpdating = false;

        #endregion Fields - Edit State

        #region Fields - Work Mode

        // Supporting infrastructure for work mode logic
        private const WorkMode DisallowEditOperationsMask = WorkMode.Bulk | WorkMode.OSMMReview | WorkMode.OSMMBulk;
        private bool _isNavigating = false;

        #endregion Fields - Work Mode

        #region Fields - Change Tracking

        private int _origIncidConditionCount = 0;
        private int _origIncidIhsMatrixCount = 0;
        private int _origIncidIhsFormationCount = 0;
        private int _origIncidIhsManagementCount = 0;
        private int _origIncidIhsComplexCount = 0;
        private int _origIncidSourcesCount = 0;

        #endregion Fields - Change Tracking

        #region Fields - Options/Settings

        // Database options
        private int _dbConnectionTimeout;

        // Export options
        private int _minZoom;
        private int _autoZoomToSelection;
        private int _warnBeforeMaxFeatures;

        // Export options
        private string _workingFileGDBPath;

        // History options
        private DataColumn[] _historyColumns;
        private int _historyDisplayLastN;

        // Interface options
        private bool _showGroupHeaders;
        private bool _showIHSTab;
        private bool _showSourceHabitatGroup;
        private bool _showHabitatSecondariesSuggested;
        private bool _showNVCCodes;
        private bool _showHabitatSummary;
        private string _showOSMMUpdates;

        // Updates options
        private int _subsetUpdateAction;
        private string _clearIHSUpdateAction;
        private string _defaultReason;
        private string _defaultProcess;
        private string _defaultHabitatClass;
        private string _defaultSecondaryGroup;
        private string _secondaryCodeOrder;
        private string _secondaryCodeDelimiter;
        private bool _notifyOnSplitMerge;

        // Validation options
        private bool _resetOSMMUpdatesStatus;
        private int _habitatSecondaryCodeValidation;
        private int _primarySecondaryCodeValidation;
        private int _qualityValidation;
        private int _potentialPriorityDetermQtyValidation;

        // Filter options
        // None

        // Dates options
        // None

        #endregion Fields - Options/Settings

        #region Fields - Lookup Tables

        private HluDataSet.lut_reasonRow[] _reasonCodes;
        private HluDataSet.lut_reasonRow[] _reasonCodesWithNone;
        private HluDataSet.lut_processRow[] _processCodes;
        private HluDataSet.lut_processRow[] _processCodesWithNone;
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

        #endregion Fields - Lookup Tables

        #region Fields - Child Rows

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

        #endregion Fields - Child Rows

        #region Fields - User/Version Info

        private string _userName;
        private string _appVersion;
        private string _dbVersion;
        private string _dataVersion;
        private Nullable<bool> _isAuthorisedUser;
        private Nullable<bool> _canUserBulkUpdate;

        #endregion Fields - User/Version Info

        #region Fields - Misc State

        private string _codeAnyRow;
        private bool _suppressUserNotifications;
        private string _codeDeleteRow;
        private HluDataSet.lut_secondary_groupRow _allRow;
        private HluDataSet.lut_secondary_groupRow _allEssRow;

        private HluDataSet.lut_reasonRow _noneReasonRow;
        private HluDataSet.lut_processRow _noneProcessRow;

        private bool _saved = false;
        private bool _savingAttempted;

        #endregion - Fields Misc State

        #region Fields - Update Control

        private bool _updateCancelled = true;
        private bool _updateAllFeatures = true;
        private bool _refillIncidTable = false;
        private bool _autoSelectOnGis;

        #endregion Fields - Update Control

        #region Fields - Static Config

        internal static string _historyGeometry1ColumnName;
        internal static string _historyGeometry2ColumnName;
        internal static int _incidPageSize;

        #endregion Fields - Static Config

        #region Fields - Workflow Controllers

        private ViewModelWindowMainBulkUpdate _viewModelBulkUpdate;
        private ViewModelWindowMainOSMMUpdate _viewModelOSMMUpdate;
        private ViewModelWindowMainUpdate _viewModelUpd;

        #endregion Fields - Workflow Controllers

        #endregion Fields

        #region Properties

        #region Properties - Core Infrastructure

        /// <summary>
        /// Gets the ArcPro application instance, to be used for accessing GIS operations. The specific application type may vary.
        /// </summary>
        /// <value>The ArcPro application instance, to be used for accessing GIS operations.</value>
        internal ArcProApp GISApplication
        {
            get { return _gisApp; }
        }

        /// <summary>
        /// Gets the database instance, to be used for accessing database operations. The specific database type may vary.
        /// </summary>
        /// <value>The database instance, to be used for accessing database operations.</value>
        internal DbBase DataBase
        {
            get { return _db; }
        }

        /// <summary>
        /// Gets the HLU dataset, to be used for accessing data tables. The specific tables included may vary based on the database type.
        /// </summary>
        /// <value>The HLU dataset, to be used for accessing data tables.</value>
        internal HluDataSet HluDataset
        {
            get { return _hluDS; }
        }

        /// <summary>
        /// Gets the collection of DataRelations in the HLU dataset, to be used for navigating between related tables. The
        /// specific DataRelations included may vary based on the database type.
        /// </summary>
        /// <value>The collection of DataRelations in the HLU dataset, to be used for navigating between related tables.</value>
        internal IEnumerable<DataRelation> HluDataRelations
        {
            get { return _hluDataRelations; }
        }

        /// <summary>
        /// Gets the TableAdapterManager for the HLU dataset, to be used for loading/saving data tables. The
        /// specific TableAdapters included may vary based on the database type.
        /// </summary>
        /// <value>The TableAdapterManager for the HLU dataset, to be used for loading/saving data tables.</value>
        internal TableAdapterManager HluTableAdapterManager
        {
            get { return _hluTableAdapterMgr; }
        }

        /// <summary>
        /// Gets the GIS ID column ordinals for the current database type, to be used for identifying GIS features.
        /// </summary>
        /// <value>The GIS ID column ordinals for the current database type, to be used for identifying GIS features.</value>
        internal int[] GisIDColumnOrdinals
        {
            get { return _gisIDColumnOrdinals; }
        }

        /// <summary>
        /// Gets the GIS ID columns for the current database type, to be used for identifying GIS features.
        /// </summary>
        /// <value>The GIS ID columns for the current database type, to be used for identifying GIS features.</value>
        internal DataColumn[] GisIDColumns
        {
            get { return _gisIDColumns; }
        }

        /// <summary>
        /// Gets the history columns (geometry and date) for the current database type, to be used
        /// for displaying Incid history in the UI. The specific columns included may vary based
        /// on the database type (e.g. Oracle may have additional geometry columns).
        /// </summary>
        /// <value>The history columns (geometry and date) for the current database type, to be
        /// used for displaying Incid history in the UI.</value>
        internal DataColumn[] HistoryColumns
        {
            get { return _historyColumns; }
        }

        /// <summary>
        /// Gets and sets the name of the first history geometry column, to be used for displaying Incid history in the UI.
        /// <value>The name of the first history geometry column.</value>
        public static string HistoryGeometry1ColumnName { get => _historyGeometry1ColumnName; set => _historyGeometry1ColumnName = value; }

        /// <summary>
        /// Gets and sets the name of the second history geometry column, to be used for displaying Incid history in the UI.
        /// </summary>
        /// <value>The name of the second history geometry column.</value>
        public static string HistoryGeometry2ColumnName { get => _historyGeometry2ColumnName; set => _historyGeometry2ColumnName = value; }

        /// <summary>
        /// Gets and sets the Incid page size, to be used for displaying Incid history in the UI.
        /// </summary>
        /// <value>The Incid page size.</value>
        public static int IncidPageSize { get => _incidPageSize; set => _incidPageSize = value; }

        #endregion Properties - Core Infrastructure

        #region Properties - Initialisation

        /// <summary>
        /// Has the DockPane been initialised?
        /// </summary>
        /// <value><c>true</c> if initialised; otherwise, <c>false</c>.</value>
        public bool Initialised
        {
            get { return _initialised; }
            set
            {
                _initialised = value;
            }
        }

        /// <summary>
        /// Is the DockPane in error?
        /// </summary>
        /// <value><c>true</c> if in error; otherwise, <c>false</c>.</value>
        public bool InError
        {
            get { return _inError; }
            set
            {
                _inError = value;
            }
        }

        #endregion Properties - Initialisation

        #region Properties - Data Tables

        /// <summary>
        /// Gets the Incid data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid data table.</value>
        public HluDataSet.incidDataTable IncidTable
        {
            get
            {
                return _hluDS.incid;
            }
        }

        /// <summary>
        /// Gets the Incid MM Polygons data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid MM Polygons data table.</value>
        public HluDataSet.incid_mm_polygonsDataTable IncidMMPolygonsTable
        {
            get
            {
                if (HluDataset.incid_mm_polygons.IsInitialized && (HluDataset.incid_mm_polygons.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_mm_polygonsTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>(_db);
                }
                return _hluDS.incid_mm_polygons;
            }
        }

        /// <summary>
        /// Gets the Incid IHS Matrix data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid IHS Matrix data table.</value>
        public HluDataSet.incid_ihs_matrixDataTable IncidIhsMatrixTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_matrix.IsInitialized && (HluDataset.incid_ihs_matrix.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_ihs_matrixTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_ihs_matrixDataTable, HluDataSet.incid_ihs_matrixRow>(_db);
                }

                return _hluDS.incid_ihs_matrix;
            }
        }

        /// <summary>
        /// Gets the Incid IHS Formation data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid IHS Formation data table.</value>
        public HluDataSet.incid_ihs_formationDataTable IncidIhsFormationTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_formation.IsInitialized && (HluDataset.incid_ihs_formation.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_ihs_formationTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_ihs_formationDataTable, HluDataSet.incid_ihs_formationRow>(_db);
                }

                return _hluDS.incid_ihs_formation;
            }
        }

        /// <summary>
        /// Gets the Incid IHS Management data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid IHS Management data table.</value>
        public HluDataSet.incid_ihs_managementDataTable IncidIhsManagementTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_ihs_management.IsInitialized && (HluDataset.incid_ihs_management.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_ihs_managementTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_ihs_managementDataTable, HluDataSet.incid_ihs_managementRow>(_db);
                }

                return _hluDS.incid_ihs_management;
            }
        }

        /// <summary>
        /// Gets the Incid IHS Complex data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid IHS Complex data table.</value>
        public HluDataSet.incid_ihs_complexDataTable IncidIhsComplexTable
        {
            get
            {
                // Load the lookup table if not already loaded.
                if (HluDataset.incid_ihs_complex.IsInitialized && (HluDataset.incid_ihs_complex.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_ihs_complexTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_ihs_complexDataTable, HluDataSet.incid_ihs_complexRow>(_db);
                }

                return _hluDS.incid_ihs_complex;
            }
        }

        /// <summary>
        /// Gets the Incid BAP data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid BAP data table.</value>
        public HluDataSet.incid_bapDataTable IncidBapTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_bap.IsInitialized && (HluDataset.incid_bap.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_bapTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_bapDataTable, HluDataSet.incid_bapRow>(_db);
                }

                return _hluDS.incid_bap;
            }
        }

        /// <summary>
        /// Gets the Incid Sources data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid Sources data table.</value>
        public HluDataSet.incid_sourcesDataTable IncidSourcesTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_sources.IsInitialized && (HluDataset.incid_sources.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_sourcesTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_sourcesDataTable, HluDataSet.incid_sourcesRow>(_db);
                }

                return _hluDS.incid_sources;
            }
        }

        /// <summary>
        /// Gets the Incid Secondary data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid Secondary data table.</value>
        public HluDataSet.incid_secondaryDataTable IncidSecondaryTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_secondary.IsInitialized && (HluDataset.incid_secondary.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_secondaryTableAdapter ??=
                            new HluTableAdapter<HluDataSet.incid_secondaryDataTable, HluDataSet.incid_secondaryRow>(_db);
                }

                return _hluDS.incid_secondary;
            }
        }

        /// <summary>
        /// Gets the Incid Condition data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid Condition data table.</value>
        public HluDataSet.incid_conditionDataTable IncidConditionTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_condition.IsInitialized && (HluDataset.incid_condition.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_conditionTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_conditionDataTable, HluDataSet.incid_conditionRow>(_db);
                }

                return _hluDS.incid_condition;
            }
        }

        /// <summary>
        /// Gets the Incid OSMM Updates data table, loading it if necessary.
        /// </summary>
        /// <value>The Incid OSMM Updates data table.</value>
        public HluDataSet.incid_osmm_updatesDataTable IncidOSMMUpdatesTable
        {
            get
            {
                // Load the data table if not already loaded.
                if (HluDataset.incid_osmm_updates.IsInitialized && (HluDataset.incid_osmm_updates.Rows.Count == 0))
                {
                    _hluTableAdapterMgr.incid_osmm_updatesTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_osmm_updatesDataTable, HluDataSet.incid_osmm_updatesRow>(_db);
                }

                return _hluDS.incid_osmm_updates;
            }
        }

        #endregion Properties - Data Tables

        #region Properties - Temp GDB

        /// <summary>
        /// Gets or sets the path to the working file geodatabase.
        /// </summary>
        /// <value>The path to the working file geodatabase.</value>
        public string WorkingFileGDBPath
        {
            get { return _workingFileGDBPath; }
            set { _workingFileGDBPath = value; }
        }

        #endregion Properties - Temp GDB

        #region Properties - Configuration

        /// <summary>
        /// Gets or sets the database connection timeout in seconds. See configuration setting "DbConnectionTimeout" for details.
        /// </summary>
        /// <value>The database connection timeout in seconds.</value>
        public int DbConnectionTimeout
        {
            get { return _dbConnectionTimeout; }
        }

        /// <summary>
        /// Gets or sets the action to take regarding clearing IHS updates when updating an Incid
        /// from the GIS. See configuration setting "ClearIHSUpdateAction" for details.
        /// </summary>
        /// <value>The action to take regarding clearing IHS updates when updating an Incid from the GIS.</value>
        internal string ClearIHSUpdateAction
        {
            get { return _clearIHSUpdateAction; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the system should automatically select features
        /// on the GIS.
        /// </summary>
        /// <value><c>true</c> if the system should automatically select features on the GIS;
        /// otherwise, <c>false</c>.</value>
        internal bool AutoSelectOnGis
        {
            get { return _autoSelectOnGis; }
        }

        #endregion Properties - Configuration

        #region Properties - Child Rows

        /// <summary>
        /// Gets or sets the collection of Incid IHS Matrix rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid IHS Matrix rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_ihs_matrixRow[] IncidIhsMatrixRows
        {
            get { return _incidIhsMatrixRows; }
            set { _incidIhsMatrixRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid IHS Formation rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid IHS Formation rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_ihs_formationRow[] IncidIhsFormationRows
        {
            get { return _incidIhsFormationRows; }
            set { _incidIhsFormationRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid IHS Management rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid IHS Management rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_ihs_managementRow[] IncidIhsManagementRows
        {
            get { return _incidIhsManagementRows; }
            set { _incidIhsManagementRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid IHS Complex rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid IHS Complex rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_ihs_complexRow[] IncidIhsComplexRows
        {
            get { return _incidIhsComplexRows; }
            set { _incidIhsComplexRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid Secondary rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid Secondary rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_secondaryRow[] IncidSecondaryRows
        {
            get { return _incidSecondaryRows; }
            set { _incidSecondaryRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid Condition rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid Condition rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_conditionRow[] IncidConditionRows
        {
            get { return _incidConditionRows; }
            set { _incidConditionRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of BAP rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of BAP rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_bapRow[] IncidBapRows
        {
            get { return _incidBapRows; }
            set { _incidBapRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid Sources rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid Sources rows related to the current Incid, that have been
        /// automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_sourcesRow[] IncidSourcesRows
        {
            get { return _incidSourcesRows; }
            set { _incidSourcesRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of Incid OSMM Updates rows related to the current Incid, that
        /// have been automatically added/edited by the system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of Incid OSMM Updates rows related to the current Incid, that have
        /// been automatically added/edited by the system and not yet saved to the database.</value>
        internal HluDataSet.incid_osmm_updatesRow[] IncidOSMMUpdatesRows
        {
            get { return _incidOSMMUpdatesRows; }
            set { _incidOSMMUpdatesRows = value; }
        }

        /// <summary>
        /// Gets or sets the collection of BAP rows that have been automatically added/edited by the
        /// system and not yet saved to the database.
        /// </summary>
        /// <value>The collection of BAP rows that have been automatically added/edited by the system
        /// and not yet saved to the database.</value>
        internal ObservableCollection<BapEnvironment> IncidBapRowsAuto
        {
            get { return _incidBapRowsAuto; }
            set { _incidBapRowsAuto = value; }
        }

        /// <summary>
        /// Gets or sets the collection of BAP rows that have been added/edited by the user and not
        /// yet saved to the database.
        /// </summary>
        /// <value>The collection of BAP rows that have been added/edited by the user and not yet
        /// saved to the database.</value>
        internal ObservableCollection<BapEnvironment> IncidBapRowsUser
        {
            get { return _incidBapRowsUser; }
            set { _incidBapRowsUser = value; }
        }

        #endregion Properties - Child Rows

        #region Properties - Record IDs

        /// <summary>
        /// Gets or sets the record IDs.
        /// </summary>
        /// <value>The record IDs.</value>
        internal RecordIds RecIDs
        {
            get { return _recIDs; }
            set { _recIDs = value; }
        }

        public string CurrentIncid { get { return _recIDs.CurrentIncid; } }

        public string NextIncid { get { return _recIDs.NextIncid; } }

        private int CurrentIncidBapId { get { return _recIDs.CurrentIncidBapId; } }

        private int NextIncidBapId { get { return _recIDs.NextIncidBapId; } }

        private int NextIncidSourcesId { get { return _recIDs.NextIncidSourcesId; } }

        private int NextIncidSecondaryId { get { return _recIDs.NextIncidSecondaryId; } }

        private int NextIncidConditionId { get { return _recIDs.NextIncidConditionId; } }

        #endregion Properties - Record IDs

        #region Properties - Save State

        /// <summary>
        /// Gets or sets a value indicating whether the current state has been saved.
        /// </summary>
        /// <value><c>true</c> if the current state has been saved; otherwise, <c>false</c>.</value>
        internal bool Saved
        {
            get { return _saved; }
            set { _saved = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether data is currently being pasted from the clipboard.
        /// </summary>
        /// <value><c>true</c> if data is currently being pasted; otherwise, <c>false</c>.</value>
        internal bool Pasting
        {
            get { return _pasting; }
            set { _pasting = value; }
        }

        /// <summary>
        /// Indicates whether changes have been made to the data by the user that have not yet been saved.
        /// </summary>
        /// <value><c>true</c> if changes have been made; otherwise, <c>false</c>.</value>
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

        /// <summary>
        /// Gets or sets a value indicating whether the data is currently being saved to the database.
        /// </summary>
        /// <value><c>true</c> if the data is currently being saved; otherwise, <c>false</c>.</value>
        internal bool Saving
        {
            get { return _saving; }
            set { _saving = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an attempt to save the data has been made.
        /// </summary>
        /// <value><c>true</c> if an attempt to save the data has been made; otherwise, <c>false</c>.</value>
        internal bool SavingAttempted
        {
            get { return _savingAttempted; }
            set { _savingAttempted = value; }
        }

        #endregion Properties - Save State

        #region Properties - Workflow Controllers

        /// <summary>
        /// Gets the ViewModel for the Bulk Update workflow, which is lazily initialised when first accessed.
        /// </summary>
        /// <value>The ViewModel for the Bulk Update workflow.</value>
        internal ViewModelWindowMainUpdate ViewModelUpdate
        {
            get { return _viewModelUpd; }
        }

        #endregion Properties - Workflow Controllers

        #region Properties - Work Mode

        /// <summary>
        /// Represents the combined operational state of the HLU tool.
        ///
        /// This replaces the four standalone boolean mode flags by using a
        /// single bitmask enum (WorkMode). Multiple modes can be active
        /// at the same time, and callers can check specific modes using:
        ///
        ///     WorkMode.HasAll(WorkMode.Bulk)
        ///
        /// Whenever WorkMode changes, this setter keeps the legacy boolean
        /// fields (_canEdit, _bulkUpdateMode, _osmmReviewMode,
        /// _osmmBulkUpdateMode) in sync for backward compatibility and then
        /// refreshes all dependent UI state.
        /// </summary>
        /// <value>The current work mode of the tool.</value>
        public WorkMode WorkMode
        {
            get => _workMode;
            set
            {
                if (_workMode == value)
                    return;

                _workMode = value;

                // Refresh the state of the active layer combo box.
                UpdateActiveLayerComboBoxEnabledState();
            }
        }

        /// <summary>
        /// Returns true when the tool is in a state where edit operations (add, update, delete)
        /// are allowed, based on the current WorkMode.
        /// </summary>
        /// <value><c>true</c> if the tool is in a state where edit operations are allowed; otherwise, <c>false</c>.</value>
        private bool IsEditOperationModeReady =>
            !WorkMode.HasAny(DisallowEditOperationsMask) &&
            WorkMode.HasAll(WorkMode.EditReady);

        /// <summary>
        /// Returns true when the tool is in a generally editable state, regardless of Reason/Process.
        /// </summary>
        /// <value><c>true</c> if the tool is in edit mode; otherwise, <c>false</c>.</value>
        private bool IsEditMode =>
            WorkMode.HasAll(WorkMode.CanEdit);

        /// <summary>
        /// Returns true when the tool is in a generally editable state and both a reason and process have been selected.
        /// </summary>
        /// <value><c>true</c> if the tool is in edit ready mode; otherwise, <c>false</c>.</value>
        private bool IsEditReady =>
            WorkMode.HasAll(WorkMode.EditReady);

        /// <summary>
        /// Returns true when the tool is in Bulk Update mode, which disables normal edit operations and enables bulk update functionality.
        /// </summary>
        /// <value><c>true</c> if the tool is in Bulk Update mode; otherwise, <c>false</c>.</value>
        private bool IsBulkMode =>
            WorkMode.HasFlag(WorkMode.Bulk);

        /// <summary>
        /// Returns true when the tool is not in Bulk Update mode.
        /// </summary>
        /// <value><c>true</c> if the tool is not in Bulk Update mode; otherwise, <c>false</c>.</value>
        private bool IsNotBulkMode =>
            !IsBulkMode;

        /// <summary>
        /// Returns true when the tool is in OSMM review mode.
        /// </summary>
        /// <value><c>true</c> if the tool is in OSMM review mode; otherwise, <c>false</c>.</value>
        private bool IsOsmmReviewMode =>
            WorkMode.HasFlag(WorkMode.OSMMReview);

        /// <summary>
        /// Returns true when the tool is not in OSMM review mode.
        /// </summary>
        /// <value><c>true</c> if the tool is not in OSMM review mode; otherwise, <c>false</c>.</value>
        private bool IsNotOsmmReviewMode =>
            !IsOsmmReviewMode;

        /// <summary>
        /// Returns true when the tool is in OSMM Bulk Update mode, which disables normal edit operations and enables OSMM-specific bulk update functionality.
        /// </summary>
        /// <value><c>true</c> if the tool is in OSMM Bulk Update mode; otherwise, <c>false</c>.</value>
        internal bool IsOsmmBulkMode =>
            WorkMode.HasFlag(WorkMode.OSMMBulk);

        /// <summary>
        /// Returns true when the tool is not in OSMM Bulk Update mode.
        /// </summary>
        /// <value><c>true</c> if the tool is not in OSMM Bulk Update mode; otherwise, <c>false</c>.</value>
        private bool IsNotOsmmBulkMode =>
            !IsOsmmBulkMode;

        #endregion Properties - Work Mode

        #region Properties - User Info

        /// <summary>
        /// Returns true if the current user is found in the database lut_user table and has bulk update authority,
        /// false if not found or does not have authority, and null if an error occurs during the check.
        /// </summary>
        /// <value><c>true</c> if the current user is authorised; otherwise, <c>false</c>.</value>
        public bool IsAuthorisedUser
        {
            get
            {
                if (_isAuthorisedUser == null) GetUserInfo();
                return _isAuthorisedUser == true;
            }
        }

        #endregion Properties - User Info

        #region Properties - Current Row

        /// <summary>
        /// Gets or sets the current incid row.
        /// </summary>
        /// <value>
        /// The current incid row.
        /// </value>
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
            set
            {
                // Call the safe fire and forget helper to move to the
                // new current row index asynchronously.
                AsyncHelpers.SafeFireAndForget(MoveIncidCurrentRowIndexAsync(value),
                Exception => Debug.WriteLine(Exception.Message));
            }
        }

        #endregion Properties - Current Row

        #region Properties - Dirty State

        /// <summary>
        /// Gets a value indicating whether any tracked data has been modified since the last save operation.
        /// </summary>
        /// <remarks>Use this property to determine if there are unsaved changes in any of the associated
        /// tables. Accessing this property may reset its value to false if a save operation was recently
        /// performed.</remarks>
        /// <value><c>true</c> if any tracked data has been modified; otherwise, <c>false</c>.</value>
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

        /// <summary>
        /// Suppresses the dirty checks and user notifications when moving
        /// between records (after changing mode).
        /// </summary>
        /// <value>
        /// <c>true</c> if user notifications are suppressed; otherwise, <c>false</c>.
        /// </value>
        public bool SuppressUserNotifications
        {
            get { return _suppressUserNotifications; }
            set { _suppressUserNotifications = value; }
        }

        #endregion Properties - Dirty State

        #region Properties - Error

        /// <summary>
        /// Gets a string of all of the errors.
        /// </summary>
        /// <value>
        /// A string containing all error messages, or null if there are no errors.
        /// </value>
        public string Error
        {
            get
            {
                // Show errors in Bulk Update mode.
                if ((_incidCurrentRow == null) ||
                    (_incidCurrentRow.RowState == DataRowState.Detached && IsNotBulkMode)) return null;

                StringBuilder error = new();

                // If not in Bulk Update mode.
                if (IsNotBulkMode)
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

                // If the habitat primary code is missing and not in Bulk Update mode.
                if (String.IsNullOrEmpty(IncidPrimary) && IsNotBulkMode)
                    error.Append(Environment.NewLine).Append("Primary Habitat is mandatory for every INCID");

                // If the habitat secondary codes validation is error.
                if (_habitatSecondaryCodeValidation > 1)
                {
                    // If there are any secondary codes that are mandatory.
                    if (_secondaryCodesMandatory != null && _secondaryCodesMandatory.Any())
                    {
                        IEnumerable<string> secondaryCodes = _incidSecondaryHabitats.Select(c => c.Secondary_habitat);

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
        /// <param name="columnName">The name of the column for which to retrieve the error message.</param>
        /// <returns>The error message for the specified column, or null if there is no error.</returns>
        public string this[string columnName]
        {
            get
            {
                // Show errors in Bulk Update mode.
                if ((_incidCurrentRow == null) ||
                    (_incidCurrentRow.RowState == DataRowState.Detached && IsNotBulkMode)) return null;

                string error = null;

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
                        if (String.IsNullOrEmpty(IncidPrimary) && IsNotBulkMode)
                        {
                            error = "Error: Primary Habitat is mandatory for every INCID";
                            AddToErrorList(_habitatErrors, columnName);
                        }
                        else
                        {
                            RemoveFromErrorList(_habitatErrors, columnName);
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
                                IEnumerable<string> secondaryCodes = _incidSecondaryHabitats.Select(c => c.Secondary_habitat);

                                // If there aren't any secondary codes or there are some mandatory codes missing.
                                if ((secondaryCodes == null) ||
                                    (_secondaryCodesMandatory.Except(secondaryCodes).Any()))
                                {
                                    // If the habitat secondary codes validation is error.
                                    if (_habitatSecondaryCodeValidation > 1)
                                    {
                                        AddToErrorList(_habitatErrors, columnName);
                                        error = "Error: One or more mandatory secondary habitats for habitat type not found";
                                    }
                                    // If the habitat secondary codes validation is warning.
                                    else
                                    {
                                        AddToErrorList(_habitatWarnings, columnName);
                                        error = "Warning: One or more mandatory secondary habitats for habitat type not found";
                                    }
                                }
                                else
                                {
                                    RemoveFromErrorList(_habitatErrors, columnName);
                                    RemoveFromErrorList(_habitatWarnings, columnName);
                                }
                            }
                            else
                            {
                                RemoveFromErrorList(_habitatErrors, columnName);
                                RemoveFromErrorList(_habitatWarnings, columnName);
                            }
                        }
                        else
                        {
                            RemoveFromErrorList(_habitatErrors, columnName);
                            RemoveFromErrorList(_habitatWarnings, columnName);
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
                    case "SelectedIncidsInGISCount":
                        if (_selectedIncidsInGISCount == 0)
                            error = "Warning: No database incids are selected in map";
                        else if (_selectedIncidsInGISCount < _selectedIncidsInDBCount)
                            error = "Warning: Not all database incids are selected in map";
                        break;

                    case "SelectedFragsInGISCount":
                        if (_selectedFragsInGISCount == 0)
                            error = "Warning: No database fragments are selected in map";
                        else if (_selectedFragsInGISCount < _selectedFragsInDBCount)
                            error = "Warning: Not all database fragments are selected in map";
                        break;

                    case "IncidOSMMUpdateStatus":
                        if (_incidOSMMUpdatesStatus != null & _incidOSMMUpdatesStatus >= 0)
                            error = "Warning: OSMM Update is outstanding";
                        break;
                }

                // Exit if in OSMM Bulk Update mode.
                if (IsOsmmReviewMode) return null;

                // Additional checks if not in Bulk Update mode.
                if (IsNotBulkMode)
                {
                    switch (columnName)
                    {
                        case "IncidBoundaryBaseMap":
                            // If the field is in error add the field name to the list of errors
                            // for the parent tab. Otherwise remove the field from the list.
                            if (String.IsNullOrEmpty(IncidBoundaryBaseMap))
                            {
                                error = "Error: Boundary basemap is mandatory for every INCID";
                                AddToErrorList(_detailsErrors, columnName);
                            }
                            else
                            {
                                RemoveFromErrorList(_detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidDigitisationBaseMap":
                            // If the field is in error add the field name to the list of errors
                            // for the parent tab. Otherwise remove the field from the list.
                            if (String.IsNullOrEmpty(IncidDigitisationBaseMap))
                            {
                                error = "Error: Digitisation basemap is mandatory for every INCID";
                                AddToErrorList(_detailsErrors, columnName);
                            }
                            else
                            {
                                RemoveFromErrorList(_detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidQualityDetermination":
                            // If the quality validation is mandatory.
                            if ((_qualityValidation == 1)
                                && (String.IsNullOrEmpty(IncidQualityDetermination)))
                            {
                                error = "Error: Determination quality is mandatory for every INCID";
                                AddToErrorList(_detailsErrors, columnName);
                            }
                            else
                            {
                                RemoveFromErrorList(_detailsErrors, columnName);
                            }
                            OnPropertyChanged(nameof(DetailsTabLabel));
                            break;

                        case "IncidQualityInterpretation":
                            // If the quality validation is mandatory.
                            if ((_qualityValidation == 1)
                                && (String.IsNullOrEmpty(IncidQualityInterpretation)))
                            {
                                error = "Error: Interpretation quality is mandatory for every INCID";
                                AddToErrorList(_detailsErrors, columnName);
                            }
                            else
                            {
                                RemoveFromErrorList(_detailsErrors, columnName);
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
                                AddToErrorList(_detailsErrors, columnName);
                            }
                            else
                            {
                                RemoveFromErrorList(_detailsErrors, columnName);
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

        #endregion Properties - Error

        #region Properties - Refresh Control

        /// <summary>
        /// Gets or sets a value indicating whether the incid table needs to be refreshed. This is set to true
        /// whenever the table needs to be refilled with data.
        /// </summary>
        /// <value><c>true</c> if the incid table needs to be refreshed; otherwise, <c>false</c>.</value>
        internal bool RefillIncidTable
        {
            get { return _refillIncidTable; }
            set { _refillIncidTable = true; }
        }

        #endregion Properties - Refresh Control

        #endregion Properties

        #region Methods

        #region Constructor

        /// <summary>
        /// Constructs the ViewModel for the main window of the HLU tool. This constructor
        /// should not perform any long-running operations or operations that require the
        /// ArcGIS Pro SDK to be initialized, as it may be called on a background thread
        /// during DockPane initialization. Any such operations should be performed in the
        /// InitializeAndCheckAsync method instead, which will be called on the UI thread
        /// after construction and is allowed to perform asynchronous operations.
        /// </summary>
        internal ViewModelWindowMain()
        {
            // Initialise the DockPane components (don't wait for it to complete).
            //_ = EnsureInitializedAsync();
        }

        #endregion Constructor

        #region Initialization

        /// <summary>
        /// Ensures the tool is initialised, then checks that the active map/layers are suitable.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal async Task InitializeAndCheckAsync()
        {
            // Ensure the DockPane is initialised.
            await EnsureInitializedAsync();
        }

        /// <summary>
        /// Ensures the DockPane is initialised exactly once and returns the in-flight task.
        /// </summary>
        /// <returns>A task that completes when initialisation completes.</returns>
        internal Task EnsureInitializedAsync()
        {
            // If initialisation has already started, always return the same task
            if (_initializationTask != null)
                return _initializationTask;

            // Start initialisation once
            _initializationTask = InitializeOnceAsync();

            return _initializationTask;
        }

        /// <summary>
        /// Runs the initialisation sequence exactly once and manages state consistently.
        /// </summary>
        /// <returns>A task that completes when initialisation completes.</returns>
        private async Task InitializeOnceAsync()
        {
            try
            {
                _dockPane = this;
                _inError = false;
                _initialised = false;
                _initializationException = null;

                // Open the add-in XML settings file.
                _xmlSettingsManager = new();

                // Upgrade the XML settings if necessary.
                UpgradeXMLSettings();

                // Load the add-in settings from the XML file.
                _addInSettings = _xmlSettingsManager.LoadSettings();

                // Set the help URL.
                _dockPane.HelpURL = _addInSettings.HelpURL;

                // Upgrade the user settings if necessary.
                UpgradeUserSettings();

                // If the user requested a connection reset via the Options screen,
                // clear the stored connection settings now so GetConnectionSettingsAsync
                // will re-prompt them.
                if (Settings.Default.ResetDbConnection)
                {
                    DbFactory.ClearSettings();
                }

                // Prompt for database connection type/details if not already configured.
                // This must happen before InitializeToolPaneAsync so all UI dialogs are
                // shown here on the UI thread, not buried inside tool initialisation.
                if (!await DbFactory.GetConnectionSettingsAsync())
                    throw new UserCancelledException();

                // Apply add-in settings now so _dbConnectionTimeout is set before
                // the connection is opened.
                ApplyAddInSettings();

                // Open the database connection and validate the dataset.
                // This is separated from InitializeToolPaneAsync so that connection
                // UI and errors are handled cleanly before tool setup begins.
                await OpenDatabaseConnectionAsync();

                // Initialise the main view (start the tool).
                await InitializeToolPaneAsync();

                // Create the working geodatabase for exports and queries.
                await CreateWorkingGeodatabaseAsync();

                // Flag the initialisation as complete.
                Initialised = true;

                // Refresh all controls
                RefreshAll();

                // Clear any messages.
                ClearMessage();

                // Make the UI controls visible.
                GridMainVisibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                InError = true;
                _initializationException = ex;
                _initializationTask = null;

                throw;
            }
        }

        /// <summary>
        /// Upgrade the XML settings if necessary.
        /// </summary>
        private void UpgradeXMLSettings()
        {
            // If the XML settings don't need upgrading then exit.
            if (!Settings.Default.CallXMLUpgrade)
                return;

            try
            {
                // Remove the following nodes from the XML file:
                _xmlSettingsManager.RemoveNode("HelpPages");

                // Set the call upgrade flag to false.
                Settings.Default.CallXMLUpgrade = false;

                // Save the settings.
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Non-fatal: log and continue with existing XML settings.
                Debug.WriteLine(
                    $"UpgradeXMLSettings failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Upgrade the application and user settings if necessary.
        /// </summary>
        private void UpgradeUserSettings()
        {
            // If the settings don't need upgrading then exit.
            if (!Settings.Default.CallUpgrade)
                return;

            try
            {
                // Upgrade the settings.
                Settings.Default.Upgrade();

                // Set the call upgrade flag to false.
                Settings.Default.CallUpgrade = false;

                // Save the settings.
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Non-fatal: log and continue with existing user settings.
                Debug.WriteLine(
                    $"UpgradeUserSettings failed: {ex.Message}");
            }
}

/// <summary>
/// Creates the working geodatabase for exports and advanced queries.
/// </summary>
/// <returns>A task that represents the asynchronous operation.</returns>
private async Task CreateWorkingGeodatabaseAsync()
        {
            try
            {
                string workingGdbDirectory = Settings.Default.WorkingFileGDBPath;

                if (!string.IsNullOrEmpty(workingGdbDirectory))
                {
                    // Create a unique name for this session
                    string uniqueID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                    string workingGdbPath = Path.Combine(workingGdbDirectory, $"HLUTool_{uniqueID}.gdb");

                    // Create the working file geodatabase
                    await Task.Run(() =>
                    {
                        var tempGDB = ArcGISProHelpers.CreateFileGeodatabase(workingGdbPath);

                        if (tempGDB != null)
                        {
                            // Store the path in the HLUTool module for cleanup on exit
                            HLUTool.WorkingGdbPath = workingGdbPath;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail initialization - exports just won't work
                System.Diagnostics.Debug.WriteLine($"Failed to create working GDB: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the database connection and validates that it points to a valid HLU dataset.
        /// Connection settings must already have been obtained by <see cref="DbFactory.GetConnectionSettingsAsync"/>
        /// and add-in settings applied (for <see cref="DbConnectionTimeout"/>) before calling this method.
        /// </summary>
        /// <returns>A task that completes when the connection is open and validated.</returns>
        /// <exception cref="HLUToolException">Thrown if no connection can be created or the dataset is invalid.</exception>
        private async Task OpenDatabaseConnectionAsync()
        {
            if ((_db = await DbFactory.CreateConnectionAsync(DbConnectionTimeout)) == null)
                throw new HLUToolException("No database connection.");

            _hluDS = new HluDataSet();

            string errorMessage;
            if (!_db.ContainsDataSet(_hluDS, out errorMessage))
            {
                // Handle any error message returned from the dataset validation.
                if (errorMessage.Length > 200)
                {
                    if (MessageBox.Show("There were errors loading data from the database." +
                        "\n\nWould like to see a list of those errors?", "HLU: Initialise Dataset",
                        MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                        ShowMessageWindow.ShowMessageDialog(errorMessage, "HLU Dataset");
                }
                else if (!String.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.Show("There were errors loading data from the database." +
                        "\n\n" + errorMessage, "HLU: Initialise Dataset",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }

                throw new HLUToolException("Database does not contain a valid HLU dataset.");
            }
        }

        /// <summary>
        /// Initialise settings for main window.
        /// </summary>
        /// <returns>Returns a task that resolves to true if the tool pane was initialized successfully; false otherwise.</returns>
        internal async Task<bool> InitializeToolPaneAsync()
        {
            // Get incid table size setting.
            _incidPageSize = _addInSettings.IncidTablePageSize;

            // Get user settings;
            ApplyUserSettings();

            // Get application settings
            _codeDeleteRow = Settings.Default.CodeDeleteRow;
            _autoSelectOnGis = Settings.Default.AutoSelectOnGis;
            _codeAnyRow = Settings.Default.CodeAnyRow;

            // Initialise statics.
            HistoryGeometry1ColumnName = Settings.Default.HistoryGeometry1ColumnName;
            HistoryGeometry2ColumnName = Settings.Default.HistoryGeometry2ColumnName;

            // Open the database connection using settings already obtained by the caller.
            try
            {
                ChangeCursor(Cursors.Wait, "Initialising ...");

                // Create table adapter manager for the dataset and connection
                _hluTableAdapterMgr = new TableAdapterManager(_db, TableAdapterManager.Scope.AllButMMPolygonsHistory);

                // Fill a dictionary of parent-child tables and relations between them
                _hluDataRelations = HluDataset.Relations.Cast<DataRelation>();

                // Translate DataRelation objects into database condtions and build order by clauses
                _childRowFilterDict = BuildChildRowFilters();
                _childRowOrderByDict = BuildChildRowOrderByClauses();

                // Fill lookup tables (at least lut_site_id must be filled at this point)
                _hluTableAdapterMgr.Fill(_hluDS, TableAdapterManager.Scope.Lookup, false);

                // Load all of the lookup tables
                LoadLookupTables();

                // Refresh combo box sources that depend on lookup tables
                RefreshComboBoxSources();

                // Create RecordIds object for the db
                _recIDs = new RecordIds(_db, _hluDS, _hluTableAdapterMgr, GisLayerType);

                // Check the assembly version is not earlier than the
                // minimum required dataset application version.
                if (!CheckVersion())
                    return false;

                // Set the <ALL> group row
                if (_allRow == null)
                {
                    _allRow = HluDataset.lut_secondary_group.Newlut_secondary_groupRow();
                    _allRow.code = "<All>";
                    _allRow.is_local = true;
                    _allRow.description = "<All>";
                    _allRow.sort_order = -1;
                }

                // Set the <ALL Essentials> group row
                if (_allEssRow == null)
                {
                    _allEssRow = HluDataset.lut_secondary_group.Newlut_secondary_groupRow();
                    _allEssRow.code = "<All Essentials>";
                    _allEssRow.is_local = true;
                    _allEssRow.description = "<All Essentials>";
                    _allEssRow.sort_order = -1;
                }

                // Set the <None> reason row
                if (_noneReasonRow == null)
                {
                    _noneReasonRow = HluDataset.lut_reason.Newlut_reasonRow();
                    _noneReasonRow.code = "<None>";
                    _noneReasonRow.description = "<None>";
                    _noneReasonRow.sort_order = -1;
                }

                // Set the <None> process row
                if (_noneProcessRow == null)
                {
                    _noneProcessRow = HluDataset.lut_process.Newlut_processRow();
                    _noneProcessRow.code = "<None>";
                    _noneProcessRow.description = "<None>";
                    _noneProcessRow.sort_order = -1;
                }

                // Wire up event handler for copy switches
                _copySwitches.PropertyChanged += new PropertyChangedEventHandler(CopySwitches_PropertyChanged);

                int result;
                // Columns that identify map polygons and are returned by GIS
                _gisIDColumnOrdinals = [.. (from s in Settings.Default.GisIDColumnOrdinals.Cast<string>()
                                        where Int32.TryParse(s, out result) && (result >= 0) &&
                                        (result < _hluDS.incid_mm_polygons.Columns.Count)
                                        select Int32.Parse(s))];
                _gisIDColumns = [.. _gisIDColumnOrdinals.Select(i => _hluDS.incid_mm_polygons.Columns[i])];

                // Columns to be saved in the history table when records are updated.
                _historyColumns = InitializeHistoryColumns(_historyColumns);

                // Count rows of incid table
                IncidRowCount(true);

                // Check for any pending OSMM updates.
                await CheckAnyOSMMUpdatesAsync();

                // Check the active map is valid
                if (await CheckActiveMapAsync())
                {
                    // If it is valid move to first row
                    await MoveIncidCurrentRowIndexAsync(1);
                }

                // Initialise the main update view model
                _viewModelUpd = new ViewModelWindowMainUpdate(this);

                // Get the BAP determination quality defaults
                GetBapDefaults();

                // Set the validation option for potential priority habitats
                BapEnvironment.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation;

                // Set default reason and process from settings and update toolbar combo boxes
                if (!string.IsNullOrWhiteSpace(_defaultReason) && _defaultReason != "<None>")
                {
                    // _defaultReason stores the code; resolve to description for the VM.
                    var reasonRow = _lutReason?.FirstOrDefault(r =>
                        string.Equals(r.code, _defaultReason, StringComparison.Ordinal));
                    if (reasonRow != null)
                        Reason = reasonRow.description;
                }

                if (!string.IsNullOrWhiteSpace(_defaultProcess) && _defaultProcess != "<None>")
                {
                    // _defaultProcess stores the code; resolve to description for the VM.
                    var processRow = _lutProcess?.FirstOrDefault(r =>
                        string.Equals(r.code, _defaultProcess, StringComparison.Ordinal));
                    if (processRow != null)
                        Process = processRow.description;
                }

                // Force all ribbon controls to re-evaluate their enabled state
                RefreshRibbonControls();

                // Reset the cursor back to normal.
                ChangeCursor(Cursors.Arrow);

                return true;
            }
            catch (UserCancelledException)
            {
                // User declined to connect — not an error; return false silently.
                return false;
            }
        }

        /// <summary>
        /// Check that there is an active map and that it contains a valid HLU map.
        /// </summary>
        /// <param name="forceReset">Forces creation of a new GIS functions instance, discarding cached layer.</param>
        /// <returns>Returns a task that resolves to true if there is a valid active map; false otherwise.</returns>
        internal async Task<bool> CheckActiveMapAsync(bool forceReset = false)
        {
            // Check the GIS map
            ChangeCursor(Cursors.Wait, "Checking GIS map ...");

            // Store the current active layer name before validation
            string currentActiveLayerName = ActiveLayerName;

            // Determine if we need to create a new GIS functions instance
            bool needsNewGisApp = _gisApp == null || forceReset;

            // Check if the map has actually changed (not just focus change)
            bool mapChanged = MapView.Active == null ||
                              _activeMapView == null ||
                              MapView.Active != _activeMapView ||
                              (_gisApp != null && MapView.Active?.Map.Name != _gisApp.MapName);

            // Only create new GIS app if forced, doesn't exist, or map changed
            if (needsNewGisApp || mapChanged)
            {
                // Create a new GIS functions instance
                _gisApp = new();

                // If map changed, update the cached view
                if (mapChanged)
                {
                    _activeMapView = _gisApp.GetActiveMapView();

                    // Only clear layer name if this is truly a different map
                    if (_activeMapView != null && _gisApp.MapName != null)
                    {
                        // Map changed - clear the active layer name so a new one is selected
                        currentActiveLayerName = null;
                    }
                }
            }

            // Check if there is no active map.
            if (_gisApp.MapName == null)
            {
                // Reset the cursor back to normal.
                ChangeCursor(Cursors.Arrow);

                // Recomputes whether editing is currently possible.
                RefreshEditCapability();

                // Display an error message.
                ShowError("No active map.", MessageCategory.GIS);

                // Clear the active map view and layer name.
                _activeMapView = null;
                ActiveLayerName = null;

                return false;
            }

            // Get the instance of the active layer ComboBox in the ribbon.
            _activeLayerComboBox ??= ActiveLayerComboBox.GetInstance();

            // Check if the GIS map is valid (pass the current active layer name to validate)
            if (!await _gisApp.IsHluMapAsync(currentActiveLayerName))
            {
                // Reset the cursor back to normal.
                ChangeCursor(Cursors.Arrow);

                // Recomputes whether editing is currently possible.
                RefreshEditCapability();

                // Clear the list of valid HLU layer names.
                AvailableHLULayerNames = [];

                // Force the ComboBox to reinitialise (if it is loaded).
                _activeLayerComboBox?.Initialize();

                // Update the selection in the ComboBox (if it is loaded)
                // to match the current active layer.
                _activeLayerComboBox?.SetSelectedItem(ActiveLayerName, true);

                // Display an error message.
                ShowError("Active map does not contain valid HLU layers.", MessageCategory.GIS);

                return false;
            }

            // Set the list of valid HLU layer names.
            AvailableHLULayerNames = new ObservableCollection<string>(_gisApp.ValidHluLayerNames);

            // Determine the target layer name without assigning it yet
            // (IsEditable is not set until IsHluLayerAsync activates the layer).
            string targetLayerName;
            if (!string.IsNullOrEmpty(currentActiveLayerName) &&
                _gisApp.ValidHluLayerNames.Contains(currentActiveLayerName))
            {
                // Keep the current active layer if it's still valid.
                targetLayerName = currentActiveLayerName;
            }
            else
            {
                // Use the layer determined by the GIS application (e.g., first valid layer).
                targetLayerName = _gisApp.ActiveHluLayer?.LayerName ?? string.Empty;
            }

            // Activate the current active HLU layer so GIS internals are set,
            // including _hluActiveLayer.IsEditable, BEFORE assigning ActiveLayerName.
            // ActiveLayerName.set calls UpdateDockPaneCaption(), which reads IsEditable,
            // so IsEditable must be up-to-date first.
            var isActiveHluLayer = !string.IsNullOrEmpty(targetLayerName) &&
                await _gisApp.IsHluLayerAsync(targetLayerName, true);

            // Now assign ActiveLayerName — IsEditable is correctly set so
            // UpdateDockPaneCaption() and RefreshEditCapability() see the right value.
            ActiveLayerName = targetLayerName;

            // Recomputes whether editing is currently possible.
            RefreshEditCapability();

            // If the active HLU layer is valid, refresh the map selection only if map changed.
            if (isActiveHluLayer && mapChanged)
            {
                // Refresh selection stats.
                await GetMapSelectionAsync(true);
            }

            // Sync the ribbon ComboBox display (if it exists).
            if (_activeLayerComboBox != null)
            {
                // If the map has changed, reinitialize the ComboBox.
                if (mapChanged)
                {
                    _activeLayerComboBox.Initialize();
                }

                // This should *only* select, not activate/switch the layer.
                _activeLayerComboBox.SetSelectedItem(ActiveLayerName, true);
            }

            // Reset the cursor back to normal.
            ChangeCursor(Cursors.Arrow);

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
                return
                [
                    .. _gisIDColumns,
                    .. _hluDS.incid_mm_polygons.Columns.Cast<DataColumn>()
                        .Where(c => !_gisIDColumnOrdinals.Contains(c.Ordinal)
                            && !c.ColumnName.StartsWith("shape_")),
                ];
            }
            catch { return historyColumns; }
        }

        /// <summary>
        /// Check the addin version is greater than or equal to the
        /// application version from the lut_version table in the database.
        /// </summary>
        /// <returns><c>true</c> if the addin version is compatible with the database; <c>false</c>
        /// if there was an error checking the version.</returns>
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
            System.Version addVersion = new(addInVersion);
            System.Version appVersion = new(lutAppVersion);

            // Compare the addin and application versions.
            if (addVersion.CompareTo(appVersion) < 0)
            {
                // Trap error if database requires a later application version.
                throw new HLUToolException($"The minimum application version must be {appVersion}.");
            }

            // Get the minimum database version.
            string minDbVersion = Settings.Default.MinimumDbVersion;

            // Compare the minimum database version.
            if (Base36.Base36ToNumber(lutDbVersion) < Base36.Base36ToNumber(minDbVersion))
            {
                // Trap error if application requires a later database version.
                throw new HLUToolException($"The minimum database version must be {minDbVersion}.");
            }

            // Store the application, database and data versions for displaying in the 'About' box.
            _appVersion = addInVersion;
            _dbVersion = lutDbVersion;
            _dataVersion = lutDataVersion;

            return true;
        }

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

        /// <summary>
        /// Forces all ribbon controls to re-evaluate their enabled/disabled state.
        /// Call this after changing WorkMode or other state that affects button availability.
        /// </summary>
        private void RefreshRibbonControls()
        {
            FrameworkApplication.Current.Dispatcher.BeginInvoke(() =>
            {
                CommandManager.InvalidateRequerySuggested();
            });
        }

        #endregion Initialization

        #region Load Lookup Tables

        /// <summary>
        /// Loads all of the lookup tables (with the exception of a few loaded elsewhere).
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
                    _hluTableAdapterMgr.lut_boundary_mapTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_conditionTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_condition_qualifierTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_habitat_classTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_habitat_typeTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_habitat_type_primaryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_habitat_type_secondaryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_ihs_formationTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_ihs_habitatTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_ihs_managementTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_ihs_matrixTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_importanceTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_legacy_habitatTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_osmm_habitat_xrefTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_primaryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_primary_bap_habitatTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_primary_categoryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_primary_secondaryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_processTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_quality_determinationTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_quality_interpretationTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_reasonTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_secondaryTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_secondary_bap_habitatTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_secondary_groupTableAdapter ??=
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
                    _hluTableAdapterMgr.lut_sourcesTableAdapter ??=
                            new HluTableAdapter<HluDataSet.lut_sourcesDataTable,
                                HluDataSet.lut_sourcesRow>(_db);
                    _hluTableAdapterMgr.Fill(HluDataset,
                        [typeof(HluDataSet.lut_sourcesDataTable)], false);
                }

                // Get the list of values from the lookup table.
                _lutSources = HluDataset.lut_sources;
            }
        }

        #endregion Load Lookup Tables

        #region Show DockPane

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal static Task ShowDockPane()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(DockPaneID);
            if (pane == null)
                return Task.CompletedTask;

            // Active the dockpane.
            pane.Activate();

            //// Get the ViewModel by casting the dockpane.
            //if (pane is ViewModelWindowMain vm)
            //{
            //    AsyncHelpers.ObserveTask(
            //        vm.InitializeAndCheckAsync(),
            //        "HLU Tool",
            //        "The HLU Tool encountered an error initialising.");
            //}

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the DockPane is shown or hidden.
        /// </summary>
        /// <param name="isVisible">A value indicating whether the DockPane is visible.</param>
        protected override void OnShow(bool isVisible)
        {
            // Make the UI controls hidden if there is no active map.
            if (MapView.Active == null)
                GridMainVisibility = Visibility.Hidden;

            // Is the dockpane visible (or is the window not showing the map).
            if (isVisible)
            {
                // Subscribe to events when the dockpane is shown.
                if (!_mapEventsSubscribed)
                {
                    _mapEventsSubscribed = true;

                    // Subscribe from ActiveMapViewChangedEvent events.
                    ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
                }

                // Subscribe to map member property changes to track editability changes.
                if (!_mapMemberEventsSubscribed)
                {
                    _mapMemberEventsSubscribed = true;

                    // Subscribe to map member property changes to track editability changes.
                    MapMemberPropertiesChangedEvent.Subscribe(OnMapMemberPropertiesChanged, keepSubscriberAlive: true);
                }

                if (!_projectClosedEventsSubscribed)
                {
                    _projectClosedEventsSubscribed = true;

                    // Subscribe to OnProjectClosed events.
                    ProjectClosedEvent.Subscribe(OnProjectClosed);
                }

                if (!_layersChangedEventsSubscribed)
                {
                    _layersChangedEventsSubscribed = true;

                    // Subscribe to the LayersAddedEvent
                    LayersAddedEvent.Subscribe(OnLayersAdded);

                    // Subscribe to the LayersRemovedEvent
                    LayersRemovedEvent.Subscribe(OnLayersRemoved);
                }

                // If there are no errors in the dockpane.
                if (!InError)
                {
                    // Ensure the tool is initialised.
                    Task checkTask = InitializeAndCheckAsync();

                    // Trap and report any exceptions to the user.
                    // Suppress UserCancelledException (user cancelled – not an error) and
                    // suppress anything already shown by another observer (InError is set).
                    AsyncHelpers.ObserveTask(
                        checkTask,
                        "HLU Tool",
                        "The HLU Tool encountered an error initialising or checking the active map.",
                        ex => ex is UserCancelledException || InError);
                }
            }

            // If the dockpane is visible.
            if (isVisible == true)
            {
                // Toggle the tab state to visible.
                ToggleState("HLUTool_tab_state", true);

                // Clear any messages.
                ClearMessage();

                // Only make the UI controls visible if initialisation succeeded.
                if (!InError)
                    GridMainVisibility = Visibility.Visible;
            }
        }

        #endregion Show DockPane

        #region Work Mode

        /// <summary>
        /// Sets or clears a <see cref="ViewModel.WorkMode"/> flag on <see cref="WorkMode"/>.
        /// </summary>
        /// <param name="flag">The work mode flag to set or clear.</param>
        /// <param name="isEnabled">A value indicating whether to set or clear the flag.</param>
        private void SetWorkModeFlag(WorkMode flag, bool isEnabled)
        {
            WorkMode = isEnabled
                ? (WorkMode | flag)
                : (WorkMode & ~flag);

            // Recompute edit capability and refresh ribbon/UI once.
            RefreshEditCapability();
            RefreshRibbonControls();
            RefreshAll();
        }

        /// <summary>
        /// Sets or clears multiple <see cref="ViewModel.WorkMode"/> flags atomically,
        /// triggering the refresh cycle only once.
        /// </summary>
        /// <param name="isEnabled">A value indicating whether to set or clear the flags.</param>
        /// <param name="flags">One or more work mode flags to set or clear together.</param>
        private void SetWorkModeFlags(bool isEnabled, params WorkMode[] flags)
        {
            WorkMode combined = flags.Aggregate(WorkMode.None, (acc, f) => acc | f);

            WorkMode = isEnabled
                ? (WorkMode | combined)
                : (WorkMode & ~combined);

            // Recompute edit capability and refresh ribbon/UI once.
            RefreshEditCapability();
            RefreshRibbonControls();
            RefreshAll();
        }

        /// <summary>
        /// Switches to the specified work mode, clearing conflicting flags as needed.
        /// Also updates the work mode button to reflect the new mode.
        /// </summary>
        /// <param name="newMode">The target work mode flags to activate</param>
        public async Task SetWorkMode(WorkMode newMode)
        {
            // If switching to normal Edit mode (Update Mode)
            if (newMode == WorkMode.Edit)
            {
                // Delegate to the appropriate reset method so that the filter,
                // GIS selection, tabs and cursor are all properly restored.
                if (IsOsmmBulkMode)
                {
                    // Switching from OSMM Bulk mode → cancel it cleanly.
                    _viewModelBulkUpdate ??=
                        new ViewModelWindowMainBulkUpdate(this, _addInSettings);
                    await _viewModelBulkUpdate.CancelOSMMBulkUpdateAsync();
                }
                else if (IsBulkMode)
                {
                    // Switching from standard Bulk mode → cancel it cleanly.
                    _viewModelBulkUpdate ??=
                        new ViewModelWindowMainBulkUpdate(this, _addInSettings);
                    await _viewModelBulkUpdate.CancelBulkUpdateAsync();
                }
                else if (IsOsmmReviewMode)
                {
                    // Switching from OSMM Review mode → cancel it cleanly.
                    _viewModelOSMMUpdate ??=
                        new ViewModelWindowMainOSMMUpdate(this);
                    await _viewModelOSMMUpdate.CancelOSMMReviewAsync();
                }
                else
                {
                    // Already in plain Edit mode – nothing to change.
                    RefreshEditCapability();
                    RefreshRibbonControls();
                    RefreshAll();
                }
            }
            // If switching to OSMM Review mode
            else if (newMode.HasFlag(WorkMode.OSMMReview))
            {
                // Start OSMM Review mode (which will set the flags)
                StartOSMMReviewClicked(null);
            }
            // If switching to Bulk OSMM mode
            else if (newMode.HasFlag(WorkMode.OSMMBulk))
            {
                // If currently in OSMM Review mode, cancel it cleanly first.
                if (IsOsmmReviewMode)
                {
                    _viewModelOSMMUpdate ??= new ViewModelWindowMainOSMMUpdate(this);
                    await _viewModelOSMMUpdate.CancelOSMMReviewAsync();
                }

                // Start OSMM Bulk Update mode (which will set the flags)
                StartOSMMBulkUpdateClicked(null);
            }
            // If switching to normal Bulk mode
            else if (newMode.HasFlag(WorkMode.Bulk))
            {
                // Start Bulk Update mode (which will set the flags)
                StartBulkUpdate();
            }

            DispatcherHelper.DoEvents();
        }

        #endregion Work Mode

        #region User Info

        /// <summary>
        /// Checks the current userid is found in the lut_table, determines
        /// if the user has bulk update authority and retrieves the user's
        /// name.
        /// </summary>
        private void GetUserInfo()
        {
            try
            {
                // Check if the database connection is available.
                if (_db == null)
                    return;

                // Find the current user's record in the lut_user table and get the value of the
                // bulk update column to determine if they are an authorised user and can bulk update.
                object result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_user.bulk_updateColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_user.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_user.user_idColumn.ColumnName),
                    _db.QuoteValue(this.UserID)), _db.Connection.ConnectionTimeout, CommandType.Text);

                // If a record was found, the user is an authorised user and the value of the bulk
                // update column determines if they can bulk update.
                if (result != null)
                {
                    _isAuthorisedUser = true;
                    _canUserBulkUpdate = (bool)result;
                }
                else
                {
                    // Otherwise, the user is not an authorised user and cannot bulk update.
                    _isAuthorisedUser = false;
                    _canUserBulkUpdate = false;
                }

                // Get the current user's username from the lut_table to display with
                // the userid in the 'About' box.
                result = _db.ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDS.lut_user.user_nameColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.lut_user.TableName),
                    _db.QuoteIdentifier(_hluDS.lut_user.user_idColumn.ColumnName),
                    _db.QuoteValue(this.UserID)), _db.Connection.ConnectionTimeout, CommandType.Text);

                // If a record was found, get the username; otherwise, use "(guest)".
                if (result != null)
                {
                    _userName = (string)result;
                }
                else
                {
                    _userName = "(guest)";
                }
            }
            catch
            {
                // If there was an error accessing the database, assume the user is not an
                // authorised user and cannot bulk update.
                _isAuthorisedUser = null;
                _canUserBulkUpdate = null;
            }
        }

        #endregion User Info

        #region Settings Management

        /// <summary>
        /// Sets the auto select on GIS option.
        /// </summary>
        /// <param name="autoSelectOnGis">The new value for the auto select on GIS option.</param>
        public void SetAutoSelectOnGis(bool autoSelectOnGis)
        {
            // Update the auto select on GIS option.
            _autoSelectOnGis = !_autoSelectOnGis;

            // Save the new auto select on GIS option in the user settings.
            Settings.Default.AutoSelectOnGis = _autoSelectOnGis;
            Settings.Default.Save();

        }

        /// <summary>
        /// Save the add-in settings.
        /// </summary>
        /// <param name="addInSettings">The add-in settings to be saved.</param>
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

            // Set the default reason for updates if not already set.
            if ((String.IsNullOrWhiteSpace(Reason)) && (_defaultReason != "<None>"))
            {
                // _defaultReason stores the code; resolve to description for the VM.
                var reasonRow = _lutReason?.FirstOrDefault(r =>
                    string.Equals(r.code, _defaultReason, StringComparison.Ordinal));
                if (reasonRow != null)
                    Reason = reasonRow.description;
            }

            // Set the default process for updates if not already set.
            if ((String.IsNullOrWhiteSpace(Process)) && (_defaultProcess != "<None>"))
            {
                // _defaultProcess stores the code; resolve to description for the VM.
                var processRow = _lutProcess?.FirstOrDefault(r =>
                    string.Equals(r.code, _defaultProcess, StringComparison.Ordinal));
                if (processRow != null)
                    Process = processRow.description;
            }

            // Refresh the group headers to reflect the new settings.
            RefreshGroupHeaders();

            // Refresh the visibility of controls affected by settings changes.
            OnPropertyChanged(nameof(ShowSourceHabitatGroup));
            OnPropertyChanged(nameof(ShowHabitatSecondariesSuggested));
            OnPropertyChanged(nameof(ShowNVCCodes));
            OnPropertyChanged(nameof(ShowHabitatSummary));
            OnPropertyChanged(nameof(IHSTabVisibility));
            OnPropertyChanged(nameof(ShowIncidOSMMPendingGroup));

            // Refresh the validity of secondary group codes based on the new settings.
            OnPropertyChanged(nameof(SecondaryGroupCodesValid));
            SecondaryGroup = _defaultSecondaryGroup;
            OnPropertyChanged(nameof(SecondaryGroup));

            // Refresh secondary table to re-trigger the validation.
            RefreshSecondaryHabitats();

            // Refresh current incid properties affected by settings changes.
            OnPropertyChanged(nameof(IncidSecondarySummary));

            OnPropertyChanged(nameof(IncidCondition));
            OnPropertyChanged(nameof(IncidConditionQualifier));
            OnPropertyChanged(nameof(IncidConditionDate));

            OnPropertyChanged(nameof(IncidQualityDetermination));
            OnPropertyChanged(nameof(IncidQualityInterpretation));
            OnPropertyChanged(nameof(IncidQualityComments));

            OnPropertyChanged(nameof(IncidHistory));

            // Refresh the edit capability based on the new settings.
            OnPropertyChanged(nameof(CanUpdate));
        }

        /// <summary>
        /// Applies the add-in settings to the application.
        /// </summary>
        private void ApplyAddInSettings()
        {
            // Get add-in database options
            _dbConnectionTimeout = _addInSettings.DbConnectionTimeout;
            //IncidPageSize is too dangerous to change on the fly.

            // Get add-in dates options
            VagueDate.Delimiter = _addInSettings.VagueDateDelimiter; // Set in the vague date class
            VagueDate.SeasonNames = [.. _addInSettings.SeasonNames.Cast<string>()]; // Set in the vague date class

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

            // Get add-in bulk update options
            //None - done in bulk update class.
        }

        /// <summary>
        /// Applies the user settings to the application.
        /// </summary>
        private void ApplyUserSettings()
        {
            // Get user GIS options
            _minZoom = Settings.Default.MinAutoZoom;
            _autoZoomToSelection = Settings.Default.AutoZoomToSelection;
            _warnBeforeMaxFeatures = Settings.Default.MaxFeaturesGISSelect;
            _workingFileGDBPath = Settings.Default.WorkingFileGDBPath;

            // Get user history options
            _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

            // Get user interface options
            _showGroupHeaders = Settings.Default.ShowGroupHeaders;
            _showIHSTab = Settings.Default.ShowIHSTab;
            _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
            _showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
            _showNVCCodes = Settings.Default.ShowNVCCodes;
            _showHabitatSummary = Settings.Default.ShowHabitatSummary;
            _showOSMMUpdates = Settings.Default.ShowOSMMUpdatesOption;

            // Get user updates options
            _defaultReason = Settings.Default.DefaultReason;
            _defaultProcess = Settings.Default.DefaultProcess;
            _defaultHabitatClass = Settings.Default.DefaultHabitatClass;
            _defaultSecondaryGroup = Settings.Default.DefaultSecondaryGroup;
            _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;
        }

        #endregion Settings Management

        #region Reason/Process

        /// <summary>
        /// Updates the <see cref="WorkMode.HasReasonAndProcess"/> flag based on the current values.
        /// </summary>
        private void UpdateReasonAndProcessFlag()
        {
            bool hasReasonAndProcess =
                !String.IsNullOrWhiteSpace(Reason) &&
                !String.IsNullOrWhiteSpace(Process);

            // Set the reason and process flag based on whether both reason and process have values.
            SetWorkModeFlag(WorkMode.HasReasonAndProcess, hasReasonAndProcess);
        }

        #endregion Reason/Process

        #region Record Navigation

        /// <summary>
        /// Sets the index of the incid current row.
        /// </summary>
        /// <param name="value">The new index for the incid current row.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task MoveIncidCurrentRowIndexAsync(int value)
        {
            // Check if already moving
            if (_moving)
                return;

            // Flag that we are moving
            _moving = true;

            try
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
                        // Clear the form and warn the user when there are no more records
                        // when in OSMM Review mode.
                        if (IsOsmmReviewMode && ((value > 0) &&
                            (IsFiltered && ((_incidSelection == null) || (value > _incidSelection.Rows.Count)))))
                        {
                            // Clear the selection (filter).
                            _incidSelection = null;

                            // Indicate the selection didn't come from the map.
                            _filteredByMap = false;

                            // Indicate there are no more OSMM updates to review.
                            _osmmUpdatesEmpty = true;

                            // Clear all the form fields (except the habitat class
                            // and habitat type).
                            ClearForm();

                            // Clear the map selection.
                            await _gisApp.ClearMapSelectionAsync();

                            // Reset the map counters
                            _selectedIncidsInGISCount = 0;
                            _selectedToidsInGISCount = 0;
                            _selectedFragsInGISCount = 0;

                            // Refresh all the controls
                            RefreshAll();

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow);

                            // Warn the user that no more records were found.
                            ShowWarning("No more records in filter.", MessageCategory.Navigation);

                            break;
                        }

                        // If in Bulk Update mode.
                        if (IsBulkMode)
                        {
                            // Set the new current row index.
                            _incidCurrentRowIndex = value;
                        }
                        else
                        {
                            // If the record number is valid.
                            if ((value > 0) &&
                            (IsFiltered && ((_incidSelection == null) || (value <= _incidSelection.Rows.Count))) ||
                            (!IsFiltered && ((_incidSelection == null) || (value <= _incidRowCount))))
                            {
                                // Set the new current row index.
                                _incidCurrentRowIndex = value;

                                // Clear any existing navigation warning messages
                                ClearMessage(category: MessageCategory.Navigation, level: MessageType.Warning);
                            }
                            else
                            {
                                // Warn the user that record is not found.
                                ShowWarning("Record not found in filtered records.", MessageCategory.Navigation);
                            }
                        }

                        // Move to the new incid
                        await NewIncidCurrentRowAsync();

                        break;
                    case MessageBoxResult.Cancel:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MoveIncidCurrentRowIndexAsync error: {ex.Message}");

                // Display an error message.
                ShowError(ex.Message, MessageCategory.Navigation);
            }
            finally
            {
                // Always reset the moving flag, even if an exception occurred.
                _moving = false;
            }
        }

        #endregion Record Navigation

        #region Seek

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

                // If seeking very early in the table.
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

                    // Store the min and max row numbers in the page (max is inclusive).
                    _incidPageRowNoMin = seekRowNumber;
                    _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count - 1;

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

                    // Store the min and max row numbers in the page (the last page ends at _incidRowCount).
                    _incidPageRowNoMin = _incidRowCount - _hluDS.incid.Count + 1;
                    _incidPageRowNoMax = _incidRowCount;

                    // Return the index of the last record in the page.
                    _incidPageRowNo = _hluDS.incid.Count - 1;
                }
                else
                {
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

                    // Fetch records
                    _hluTableAdapterMgr.Fill(_hluDS, typeof(HluDataSet.incidDataTable),
                        String.Format(loadWhereClauseTemplate,
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber)),
                            _db.QuoteValue(_recIDs.IncidString(seekIncidNumber + IncidPageSize))), true);

                    // Store the min and max row numbers in the page (max is inclusive).
                    _incidPageRowNoMin = seekRowNumber;
                    _incidPageRowNoMax = _incidPageRowNoMin + _hluDS.incid.Count - 1;

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
        /// <returns>A task that represents the asynchronous operation. The task result contains the
        /// row of in-memory DataTable HluDataset.incid that corresponds to
        /// row number seekRowNumber in the _incidSelection DataTable.
        /// If loading of a new page fails, null is returned.</returns>
        private async Task<HluDataSet.incidRow> SeekIncidFiltered(int seekRowNumber)
        {
            // Adjust the seek row number to be zero-based instead of one-based.
            seekRowNumber--;

            // If the seek row number is out of bounds, set it to the nearest valid value.
            if (seekRowNumber < 0)
                seekRowNumber = 0;
            else if (seekRowNumber > _incidSelection.Rows.Count - 1)
                seekRowNumber = _incidSelection.Rows.Count - 1;

            // Get the incid corresponding to the seek row number from the selection and try to find it in the current page.
            string seekIncid = (string)_incidSelection.DefaultView[seekRowNumber][0];
            HluDataSet.incidRow returnRow = _hluDS.incid.FindByincid(seekIncid);

            // Enable the Incid table to be forced to refill if it has been
            // updated directly in the database rather than via the
            // local copy.
            if ((returnRow != null) && (!_refillIncidTable))
            {
                return returnRow;
            }

            // Get the incid number from the incid string.
            int seekIncidNumber = RecordIds.IncidNumber(seekIncid);

            _refillIncidTable = false;
            int incidNumberPageMin;
            int incidNumberPageMax;

            // Set the page min and max based on the current page if there are records in the page,
            // otherwise set based on the seek incid number.
            if (_hluDS.incid.Rows.Count == 0)
            {
                incidNumberPageMin = seekIncidNumber;
                incidNumberPageMax = incidNumberPageMin + IncidPageSize;
            }
            else
            {
                incidNumberPageMin = RecordIds.IncidNumber(_hluDS.incid[0].incid);
                incidNumberPageMax = RecordIds.IncidNumber(_hluDS.incid[^1].incid);
            }

            // Set the start and stop row numbers for the page to be loaded based on the seek row
            // number and the current page, and set the direction of movement.
            int start = _incidCurrentRowIndex > 0 ? _incidCurrentRowIndex - 1 : 0;
            int stop = start;
            bool moveForward = true;

            // If the seek incid number is outside the current page, set the start and stop row numbers for the new page to be loaded
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

            // Try to load the page of incids corresponding to the seek row number and return the
            // required row from that page.
            try
            {
                // Get the incids for the new page.
                string[] incids = new string[start == stop ? 1 : stop - start + 1];

                // Quote the incids for use in the SQL IN clause.
                for (int i = 0; i < incids.Length; i++)
                    incids[i] = _db.QuoteValue(_incidSelection.DefaultView[start + i][0]);

                // Set up the SQL to load the page of incids based on the incids in the selection.
                string fillSql = String.Format("{0} IN ({1}) ORDER BY {0}",
                    _db.QuoteIdentifier(_hluDS.incid.incidColumn.ColumnName),
                    String.Join(",", incids));

                // Load the page of incids from the database.
                await Task.Run(() =>
                    _hluTableAdapterMgr.incidTableAdapter.Fill(
                        _hluDS.incid, fillSql));

                // If no records were loaded, warn the user and move to the first or last record in
                // the current page as appropriate.
                if (_hluDS.incid.Count == 0)
                {
                    ShowWarning("No database record retrieved.", MessageCategory.Database);

                    // Clear the current row without abandoning the active filter
                    // or the GIS selection — the filter is still valid even though
                    // this particular page load returned nothing.
                    _incidCurrentRow = null;

                    return _incidCurrentRow;
                }
                else
                {
                    // Clear any existing database warning messages
                    ClearMessage(category: MessageCategory.Database, level: MessageType.Warning);

                    // If the table has paged backwards (because the required incid
                    // is lower than the page minimum) and if the row number being
                    // sought is the first (i.e. zero) then return the lowest incid.
                    // Otherwise, return the lowest or highest as appropriate.
                    return (moveForward || seekRowNumber == 0) ? _hluDS.incid[0] : _hluDS.incid[^1];
                }
            }
            catch { return null; }
        }

        #endregion Seek

        #region Clean/Dirty Check

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

            if (IsEditMode && IsNotBulkMode)
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

            // Don't check for edits when in OSMM Review mode (because the data
            // can't be edited by the user).
            if (IsEditMode
                && (_splitting == false)
                && (!SuppressUserNotifications)
                && IsNotBulkMode
                && IsNotOsmmReviewMode
                && IsDirty)
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

        #endregion Clean/Dirty Check

        #region Row Management

        /// <summary>
        /// Retrieves the derived values from the current row and updates the corresponding properties.
        /// </summary>
        private void IncidCurrentRowDerivedValuesRetrieve()
        {
            _incidLastModifiedUser = _incidCurrentRow.last_modified_user_id;
            _incidLastModifiedDate = Convert.IsDBNull(_incidCurrentRow.last_modified_date) ? DateTime.MinValue : _incidCurrentRow.last_modified_date;
            _incidPrimary = _incidCurrentRow.Ishabitat_primaryNull() ? null : _incidCurrentRow.habitat_primary;
            NewPrimaryHabitat(_incidPrimary);
            _incidIhsHabitat = _incidCurrentRow.Isihs_habitatNull() ? null : _incidCurrentRow.ihs_habitat;
        }

        /// <summary>
        /// Creates a clone of the current row, which can be used to restore the original values if needed.
        /// </summary>
        public void CloneIncidCurrentRow()
        {
            _incidCurrentRowClone = _hluDS.incid.NewincidRow(); // IncidTable.NewincidRow();
            for (int i = 0; i < IncidTable.Columns.Count; i++)
                _incidCurrentRowClone[i] = _incidCurrentRow[i];
        }

        /// <summary>
        /// Restores the values in the current row to those in the clone of the current row, which should be the
        /// original values before any edits were made.
        /// </summary>
        private void RestoreIncidCurrentRow()
        {
            if (_incidCurrentRowClone != null)
            {
                for (int i = 0; i < _hluDS.incid.Columns.Count; i++) // IncidTable.Columns.Count; i++)
                    _incidCurrentRow[i] = _incidCurrentRowClone[i];
            }
        }

        /// <summary>
        /// Compares the current row with the clone of the current row and returns true if they are
        /// the same and false if they are different.
        /// </summary>
        /// <returns><c>true</c> if the current row and its clone are the same; otherwise, <c>false</c>.</returns>
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

        #endregion Row Management

        #region Child Row Filters

        /// <summary>
        /// Builds a mapping of child data table types to their corresponding SQL ORDER BY clauses based on primary key
        /// columns.
        /// </summary>
        /// <remarks>The returned dictionary can be used to construct SQL queries that require consistent
        /// ordering of child rows by their primary keys. The ORDER BY clauses are formatted and quoted according to the
        /// database provider's requirements. For some tables, the ordering may be descending as appropriate for the
        /// data model.</remarks>
        /// <returns>A dictionary that maps each supported child data table type to a string containing the SQL ORDER BY clause
        /// for that table's primary key columns.</returns>
        private Dictionary<Type, string> BuildChildRowOrderByClauses()
        {
            Dictionary<Type, string> childRowOrberByDict = new()
            {
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

        /// <summary>
        /// Builds a dictionary that maps child DataTable types to their corresponding SQL filter conditions based on
        /// the parent 'incid' table.
        /// </summary>
        /// <remarks>The returned dictionary can be used to efficiently retrieve or filter child rows
        /// related to the 'incid' parent table in the HluDataSet. Each entry represents the filter conditions necessary
        /// to associate a specific child table with its parent records.</remarks>
        /// <returns>A dictionary where each key is a child DataTable type and each value is a list of SQL filter conditions to
        /// apply for that child table.</returns>
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

        /// <summary>
        /// Creates a SQL filter condition for child rows using the specified table and data column.
        /// </summary>
        /// <typeparam name="T">The type of the data table to which the filter will be applied. Must inherit from DataTable.</typeparam>
        /// <param name="table">The data table representing the child rows to filter.</param>
        /// <param name="incidColumn">The data column used as the filter criterion within the specified table.</param>
        /// <returns>A SqlFilterCondition configured to filter child rows based on the provided table and column.</returns>
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

        /// <summary>
        /// Creates a list of SQL filter conditions that represent the relationship between a parent and child data
        /// table.
        /// </summary>
        /// <remarks>The generated filter conditions correspond to the columns defined in the data
        /// relation between the specified parent and child tables. These conditions can be used to construct SQL WHERE
        /// clauses or similar filtering logic when querying the child table.</remarks>
        /// <typeparam name="P">The type of the parent data table. Must derive from DataTable.</typeparam>
        /// <typeparam name="C">The type of the child data table. Must derive from DataTable.</typeparam>
        /// <param name="parentTable">The parent data table used to determine the relationship and filter conditions.</param>
        /// <param name="childTable">The child data table for which the filter conditions are generated.</param>
        /// <returns>A list of SqlFilterCondition objects representing the filter criteria for the child table based on its
        /// relationship to the parent table. The list will be empty if there are no related columns.</returns>
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

        #endregion Child Row Filters

        #region Child Row Retrieval

        /// <summary>
        /// Retrieves and updates all child data rows related to the specified incid row within the dataset.
        /// </summary>
        /// <remarks>This method populates various child row collections for the given incid, including
        /// secondary, condition, IHS matrix, IHS formation, IHS management, IHS complex, BAP, sources, history, and
        /// OSMM updates rows. If the provided incid row is null, no action is taken.</remarks>
        /// <param name="incidRow">The incid row for which to load associated child rows. Cannot be null.</param>
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

        /// <summary>
        /// Retrieves the data relation that links the specified parent and child tables.
        /// </summary>
        /// <typeparam name="P">The type of the parent table. Must derive from DataTable.</typeparam>
        /// <typeparam name="C">The type of the child table. Must derive from DataTable.</typeparam>
        /// <param name="parentTable">The parent table in the relation. Cannot be null.</param>
        /// <param name="childTable">The child table in the relation. Cannot be null.</param>
        /// <returns>The DataRelation that links the specified parent and child tables, or null if no such relation exists.</returns>
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

        /// <summary>
        /// Retrieves child rows from the specified child table that match the given relationship values, using the
        /// provided table adapter.
        /// </summary>
        /// <remarks>The returned rows are sorted if an order-by clause is defined for the child table
        /// type; otherwise, the default order is used. The method does not modify the original child table except to
        /// fill it with data as needed.</remarks>
        /// <typeparam name="C">The type of the child DataTable. Must derive from DataTable and have a parameterless constructor.</typeparam>
        /// <typeparam name="R">The type of DataRow contained in the child table.</typeparam>
        /// <param name="relValues">An array of values corresponding to the relationship columns used to filter the child rows. The order of
        /// values must match the expected filter conditions for the child table type.</param>
        /// <param name="adapter">The table adapter used to fill the child table with data based on the specified filter conditions.</param>
        /// <param name="childTable">A reference to the child DataTable to be filled and queried for matching rows.</param>
        /// <returns>An array of child rows of type R that match the specified relationship values. Returns an empty array if no
        /// matching filter conditions are defined for the child table type.</returns>
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

        #endregion Child Row Retrieval

        #endregion Methods
   }
}