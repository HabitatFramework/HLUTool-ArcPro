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

using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU.Data;
using HLU.Data.Model;
using HLU.Date;
using HLU.Enums;
using HLU.Helpers;
using HLU.Properties;
using HLU.UI.UserControls;
using HLU.UI.UserControls.Toolbar;
using HLU.UI.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Timers;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using HLU.Exceptions;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// UI-focused partial for ViewModelWindowMain.
    /// Contains: All bindable properties, UI state, visibility, formatting, commands, UI coordination.
    /// </summary>
    partial class ViewModelWindowMain
    {
        #region Fields

        #region Fields - DockPane/View

        private bool _formLoading;
        private string _helpURL;

        #endregion Fields - DockPane/View

        #region Fields - Display

        // DockPane fields
        private ViewModelWindowMain _dockPane;
        private const string _dockPaneID = "HLUTool_UI_WindowMain";
        private const string _dockPaneBaseCaption = "HLU Tool";

        private string _displayName = "HLU Tool";

        private Visibility _gridmainVisibility = Visibility.Visible;

        // Habitat display fields
        private double _incidArea;
        private double _incidLength;
        private string _process;
        private string _reason;
        private string _habitatClass;
        private string _habitatType;
        private string _habitatSecondariesSuggested;
        private string _habitatTips;
        private string _secondaryGroup;
        private string _secondaryHabitat;

        // IHS/Details display fields
        private string _incidIhsHabitat;
        private string _incidPrimary;
        private string _incidPrimaryCategory;
        private string _incidNVCCodes;
        private string _incidSecondarySummary;
        private string _incidLastModifiedUser;
        private DateTime _incidLastModifiedDate;
        private string _incidLegacyHabitat;

        // OSMM display fields
        private int _incidOSMMUpdatesOSMMXref;
        private int _incidOSMMUpdatesProcessFlag;
        private string _incidOSMMUpdatesSpatialFlag;
        private string _incidOSMMUpdatesChangeFlag;
        private Nullable<int> _incidOSMMUpdatesStatus;

        // OSMM UI State display fields
        private string _osmmAcceptTag = "A_ccept";
        private string _osmmRejectTag = "Re_ject";
        private Nullable<bool> _anyOSMMUpdates;
        private bool _osmmUpdatesEmpty = false;
        private bool _osmmUpdateCreateHistory;

        // Vague date display fields
        private VagueDateInstance _incidConditionDateEntered;
        private VagueDateInstance _incidSource1DateEntered;
        private VagueDateInstance _incidSource2DateEntered;
        private VagueDateInstance _incidSource3DateEntered;

        #endregion Fields - Display

        #region Fields - Messages

        // Queue to hold messages to display on the form
        private ObservableCollection<MessageItem> _messageQueue = [];

        // Dictionary to track auto-dismiss timers per message
        private Dictionary<string, System.Timers.Timer> _messageTimers = [];

        #endregion Fields - Messages

        #region Fields - Tab Control

        private bool _showingOSMMPendingGroup = false;
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

        #endregion Fields - Tab Control

        #region Fields - General

        private bool _windowEnabled = true;
        private bool _pasting = false;
        private Cursor _windowCursor = Cursors.Arrow;

        #endregion Fields - General

        #region Fields - Ribbon Controls

        // ObservableCollection to hold HLU layers combo box items
        private ObservableCollection<string> _availableHLULayerNames = [];

        private ActiveLayerComboBox _activeLayerComboBox;

        #endregion Fields - Ribbon Controls

        #region Fields - ComboBox Sources

        private readonly ObservableCollection<CodeDescriptionBool> _primaryCodes = [];

        private HluDataSet.lut_sourcesRow[] _sourceNames;
        private HluDataSet.lut_habitat_classRow[] _sourceHabitatClassCodes;
        private HluDataSet.lut_importanceRow[] _sourceImportanceCodes;

        private HluDataSet.lut_habitat_typeRow[] _bapHabitatCodes;

        private HluDataSet.lut_boundary_mapRow[] _boundaryMapCodes;

        private HluDataSet.lut_conditionRow[] _conditionCodes;
        private HluDataSet.lut_condition_qualifierRow[] _conditionQualifierCodes;

        private HluDataSet.lut_secondary_groupRow[] _secondaryGroups;
        private HluDataSet.lut_secondary_groupRow[] _secondaryGroupsValid;
        private HluDataSet.lut_secondary_groupRow[] _secondaryGroupsWithAll; // Used in the options window

        private HluDataSet.lut_secondaryRow[] _secondaryCodesAll;
        private HluDataSet.lut_secondaryRow[] _secondaryCodesValid;

        private IEnumerable<string> _secondaryCodesOptional;
        private IEnumerable<string> _secondaryCodesMandatory;

        private ObservableCollection<SecondaryHabitat> _incidSecondaryHabitats;
        private ObservableCollection<BapEnvironment> _incidBapRowsAuto;
        private ObservableCollection<BapEnvironment> _incidBapRowsUser;
        private HluDataSet.lut_legacy_habitatRow[] _legacyHabitatCodes;
        private HistoryRowEqualityComparer _histRowEqComp = new();
        private HluDataSet.lut_habitat_classRow[] _habitatClassCodes;
        private HluDataSet.lut_habitat_classRow[] _habitatClasses; // Used in the options window
        private HluDataSet.lut_habitat_typeRow[] _habitatTypeCodes;

        #endregion Fields - ComboBox Sources

        #region Fields - Progress

        private double _progressValue;
        private double _maxProgressValue;
        private string _processStatus;
        private string _progressText;

        #endregion Fields - Progress

        #region Fields - Commands

        private ICommand _navigateFirstCommand;
        private ICommand _navigatePreviousCommand;
        private ICommand _navigateNextCommand;
        private ICommand _navigateLastCommand;
        private ICommand _navigateIncidCommand;
        private ICommand _filterByAttributesOSMMCommand;
        private ICommand _getMapSelectionCommand;
        private ICommand _editPriorityHabitatsCommand;
        private ICommand _editPotentialHabitatsCommand;
        private ICommand _addSecondaryHabitatCommand;
        private ICommand _addSecondaryHabitatListCommand;
        private ICommand _updateCommand;
        private ICommand _osmmSkipCommand;
        private ICommand _osmmAcceptCommand;
        private ICommand _osmmRejectCommand;

        #endregion Fields - Commands

        #region Fields - Windows/Dialogs

        private WindowMainCopySwitches _copySwitches = new();
        private WindowAbout _windowAbout;
        private ViewModelWindowAbout _viewModelAbout;
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
        private ViewModelWindowWarnOnSubsetUpdate _viewModelWinWarnSubsetUpdate;
        private WindowEditPriorityHabitats _windowEditPriorityHabitats;
        private ViewModelWindowEditPriorityHabitats _viewModelWinEditPriorityHabitats;
        private WindowEditPotentialHabitats _windowEditPotentialHabitats;
        private ViewModelWindowEditPotentialHabitats _viewModelWinEditPotentialHabitats;

        #endregion Fields - Windows/Dialogs

        #endregion Fields

        #region Properties

        #region Properties - DockPane/View

        /// <summary>
        /// Gets or sets a value indicating whether the form is loading.
        /// </summary>
        /// <value><c>true</c> if the form is loading; otherwise, <c>false</c>.</value>
        public bool FormLoading
        {
            get { return _formLoading; }
            set { _formLoading = value; }
        }

        /// <summary>
        /// Gets or sets the URL of the help page.
        /// </summary>
        /// <value>The URL of the help page.</value>
        public string HelpURL
        {
            get { return _helpURL; }
            set { _helpURL = value; }
        }

        /// <summary>
        /// Gets or sets the add-in settings.
        /// </summary>
        /// <value>The add-in settings.</value>
        public AddInSettings AddInSettings
        {
            get { return _addInSettings; }
            set { _addInSettings = value; }
        }

        #endregion Properties - DockPane/View

        #region Properties - Display

        /// <summary>
        /// Gets the DockPane ID for this window, which is used to uniquely identify the DockPane within
        /// the ArcGIS Pro framework and to associate it with the correct view and view model.
        /// </summary>
        /// <value>The DockPane ID for this window.</value>
        public static string DockPaneID
        {
            get => _dockPaneID;
        }

        /// <summary>
        /// Gets the user-friendly name of this object.
        /// Child classes can set this property to a new value,
        /// or override it to determine the value on-demand.
        /// </summary>
        /// <value>The user-friendly name of this object.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        /// <summary>
        /// Gets the title of the main window.
        /// </summary>
        /// <value>The title of the main window.</value>
        public override string WindowTitle
        {
            get
            {
                return String.Format("{0}{1}", DisplayName, CanEdit ? String.Empty : " [READONLY]");
            }
        }

        /// <summary>
        /// Gets the visibility of the main grid containing the UI controls. This is toggled to visible
        /// or hidden based on whether there is an active map view with a valid HLU layer, and
        /// also controls the state of the maingrid via DAML.
        /// </summary>
        /// <value>The visibility of the main grid.</value>
        public Visibility GridMainVisibility
        {
            get
            {
                if (!Initialised || InError)
                    return Visibility.Hidden;
                else
                    return _gridmainVisibility;
            }
            set
            {
                _gridmainVisibility = value;
                OnPropertyChanged(nameof(GridMainVisibility));

                // Toggle the maingrid state to enabled or disabled.
                if (_gridmainVisibility == Visibility.Visible)
                    ToggleState("HLUTool_maingrid_state", true);
                else
                    ToggleState("HLUTool_maingrid_state", false);
            }
        }

        #endregion Properties - Display

        #region Properties - Ribbon Controls

        /// <summary>
        /// Gets or sets the available HLU layers.
        /// </summary>
        /// <value>The available HLU layers.</value>
        public ObservableCollection<string> AvailableHLULayerNames
        {
            get { return _availableHLULayerNames; }
            set
            {
                _availableHLULayerNames = value;
                OnPropertyChanged(nameof(AvailableHLULayerNames));
            }
        }

        #endregion Properties - Ribbon Controls

        #region Properties - User

        /// <summary>
        /// Gets the current user's ID in the format "DOMAIN\username" if a domain is present, or just "username" if not.
        /// </summary>
        /// <value>The current user's ID.</value>
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

        /// <summary>
        /// Gets the current user's name from the database based on their user ID, or "(guest)" if not found or an error occurs.
        /// </summary>
        /// <value>The current user's name.</value>
        public string UserName
        {
            get { return _userName; }
        }

        #endregion Properties - User

        #region Properties - Cursor

        /// <summary>
        /// Gets the cursor to use for the window, which can be set to indicate different states (e.g. waiting, default).
        /// </summary>
        /// <value>The cursor to use for the window.</value>
        public Cursor WindowCursor { get { return _windowCursor; } }

        #endregion Properties - Cursor

        #region Properties - Header / General

        /// <summary>
        /// Gets or sets the incid associated with the current row.
        /// </summary>
        /// <value>The incid associated with the current row</value>
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

        /// <summary>
        /// Gets the total area of the features for the current incid.
        /// </summary>
        /// <value>The total area of the features for the current incid.</value>
        public string IncidArea
        {
            get
            {
                // Don't show the area when in OSMM Update mode and there are no
                // updates to process.
                if ((IsNotBulkMode) && (IsNotOsmmReviewMode || _osmmUpdatesEmpty == false))
                {
                    GetIncidMeasures();
                    return _incidArea.ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the total perimieter length of the features for the current incid.
        /// </summary>
        /// <value>The total perimeter length of the features for the current incid.</value>
        public string IncidLength
        {
            get
            {
                // Don't show the length when in OSMM Update mode and there are no
                // updates to process.
                if ((IsNotBulkMode) && (IsNotOsmmReviewMode || _osmmUpdatesEmpty == false))
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

        /// <summary>
        /// Gets or sets the created date of the current incid.
        /// </summary>
        /// <value>The created date of the current incid.</value>
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

        /// <summary>
        /// Gets or sets the last modified date of the current incid.
        /// </summary>
        /// <value>The last modified date of the current incid.</value>
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

        /// <summary>
        /// Gets or sets the created user's name of the current incid.
        /// </summary>
        /// <value>The created user's name of the current incid.</value>
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

        /// <summary>
        /// Gets or sets the last modified user's name of the current incid.
        /// </summary>
        /// <value>The last modified user's name of the current incid.</value>
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

        /// <summary>
        /// Only show the OSMM Updates group if required, otherwise collapse it.
        /// </summary>
        /// <value>The visibility of the OSMM Updates group.</value>
        public Visibility ShowIncidOSMMPendingGroup
        {
            get
            {
                // Show the group if not in osmm update mode and
                // show updates are "Always" required, or "When Outstanding"
                // (i.e. update flag is "Proposed" (> 0) or "Pending" = 0).
                if ((IsOsmmReviewMode) ||
                    (IsNotBulkMode &&
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

        /// <summary>
        /// Gets the list of reason codes.
        /// </summary>
        /// <value>The list of reason codes.</value>
        public HluDataSet.lut_reasonRow[] ReasonCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                _reasonCodes ??= [.. _lutReason.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return _reasonCodes;
            }
        }

        /// <summary>
        /// Gets the list of reason codes with a "<None>" option added at the beginning of the list.
        /// </summary>
        /// <value>The list of reason codes with a "<None>" option.</value>
        public HluDataSet.lut_reasonRow[] ReasonCodesWithNone
        {
            get
            {
                // Get the list of values from the lookup table.
                _reasonCodes ??= [.. _lutReason.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Add the <None> code to the beginning of the list.
                _reasonCodesWithNone ??= [_noneReasonRow, .. _reasonCodes];

                return _reasonCodesWithNone;
            }
        }

        /// <summary>
        /// Gets or sets the selected reason code.
        /// </summary>
        /// <value>The selected reason code.</value>
        public string Reason
        {
            get
            {
                return _reason;
            }
            set
            {
                // If the reason has changed
                if (_reason != value)
                {
                    _reason = value;
                    OnPropertyChanged(nameof(Reason));

                    // Update the toolbar combo box
                    ReasonComboBox.GetInstance()?.SetSelectedItem(_reason);

                    // Refresh both the cached split and merge enablement values.
                    RefreshSplitMergeEnablement();

                    // Update the work mode flag
                    UpdateReasonAndProcessFlag();
                }
            }
        }

        /// <summary>
        /// Gets the list of process codes.
        /// </summary>
        /// <value>The list of process codes.</value>
        public HluDataSet.lut_processRow[] ProcessCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                _processCodes ??= [.. _lutProcess.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return _processCodes;
            }
        }

        /// <summary>
        /// Gets the list of process codes with a "<None>" option added at the beginning of the list.
        /// </summary>
        /// <value>The list of process codes with a "<None>" option.</value>
        public HluDataSet.lut_processRow[] ProcessCodesWithNone
        {
            get
            {
                // Get the list of values from the lookup table.
                _processCodes ??= [.. _lutProcess.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Add the <None> code to the beginning of the list.
                _processCodesWithNone ??= [_noneProcessRow, .. _processCodes];

                return _processCodesWithNone;
            }
        }

        /// <summary>
        /// Gets or sets the selected process code.
        /// </summary>
        /// <value>The selected process code.</value>
        public string Process
        {
            get
            {
                return _process;

            }
            set
            {
                // If the process hs changed
                if (_process != value)
                {
                    _process = value;
                    OnPropertyChanged(nameof(Process));

                    // Update the toolbar combo box
                    ProcessComboBox.GetInstance()?.SetSelectedItem(_process);

                    // Refresh both the cached split and merge enablement values.
                    RefreshSplitMergeEnablement();

                    // Update the work mode flag
                    UpdateReasonAndProcessFlag();
                }
            }
        }

        #endregion Properties - Header / General

        #region Properties - Tab Control State

        /// <summary>
        /// If the reason and process code controls are enabled.
        /// </summary>
        /// <value><see langword="true"/> if the reason and process code controls are enabled; otherwise, <see langword="false"/>.</value>
        public bool ReasonProcessEnabled
        {
            get
            {
                // Enable when not in bulk/OSMM mode AND there is a current row
                // Disable when not in bulk/OSMM mode AND there is NO current row
                if (IsNotBulkMode && IsNotOsmmReviewMode)
                    _reasonProcessEnabled = IncidCurrentRow != null;

                return _reasonProcessEnabled;
            }
            set
            {
                _reasonProcessEnabled = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the data tab control is enabled.
        /// </summary>
        /// <remarks>The property returns <see langword="false"/> if the control is not in bulk mode and
        /// there is no current row selected, regardless of the underlying value. Setting this property does not
        /// override these conditions.</remarks>
        /// <value><see langword="true"/> if the data tab control is enabled; otherwise, <see langword="false"/>.</value>
        public bool TabControlDataEnabled
        {
            get
            {
                if ((IsNotBulkMode) && IncidCurrentRow == null)
                    return false;

                return _windowEnabled && _tabControlDataEnabled;
            }
            set { _tabControlDataEnabled = value; }
        }

        /// <summary>
        /// Which tab item is selected.
        /// </summary>
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


        #endregion Properties - Tab Control State

        #region Properties - Habitat Tab

        #region Properties - Habitat Label/Header

        // Set the Habitat tab label from here so that validation can be done.
        // This will enable tooltips to be shown so that validation errors
        // in any fields in the tab can be highlighted by flagging the tab
        // label as in error.
        /// <value>The label for the habitat tab.</value>
        public string HabitatTabLabel
        {
            get { return "Habitats"; }
        }

        /// <summary>
        /// Gets the habitat header to be used in the habitat tab. It is shown only
        /// if the option to show group headers is set.
        /// </summary>
        /// <value>The habitat header string if group headers are shown, otherwise null.</value>
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

        #endregion Properties - Habitat Header

        #region Properties - Habitat Class/Type

        /// <summary>
        /// Only show the source habitat group if the option is set, otherwise collapse it.
        /// </summary>
        /// <value>The visibility of the source habitat group.</value>
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
                if (_lutHabitatClass == null || !_lutHabitatClass.Any())
                    return null;

                // Set the habitat classes for all local habitat classes with
                // local habitat types that relate to at least one primary
                // habitat type that is local.
                _habitatClassCodes ??= [.. (from c in _lutHabitatClass
                                          join t in _lutHabitatType on c.code equals t.habitat_class_code
                                          join tp in _lutHabitatTypePrimary on t.code equals tp.code_habitat_type
                                          select c).Distinct().OrderBy(c => c.sort_order).ThenBy(c => c.description)];

                return _habitatClassCodes;
            }
        }

        /// <summary>
        /// Gets the list of habitat classes to be used in the options window.
        /// This is set to all local habitat classes with local habitat types, regardless
        /// of whether they relate to a primary habitat type that is local, as the options
        /// window is used to set which habitat classes and types are flagged as local.
        /// </summary>
        /// <remarks>Used in the options window to display all local habitat classes with local habitat types.</remarks>
        /// <value>The list of habitat classes.</value>
        public HluDataSet.lut_habitat_classRow[] HabitatClasses
        {
            get
            {
                // If not already populated
                if (_habitatClasses == null && _hluDS != null)
                {
                    // Set the full list of habitat classes for all
                    // local habitat classes with local habitat types.
                    _habitatClasses = [.. _hluDS.lut_habitat_class.AsEnumerable()
                        .OrderBy(r => r.sort_order)
                        .ThenBy(r => r.description)];
                }

                return _habitatClasses ?? [];
            }
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
                // Set the habitat class when there are OSMM updates to process.
                if (_habitatClass == null && _osmmUpdatesEmpty == false)
                    _habitatClass = _defaultHabitatClass;

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
        /// the selected habitat class.
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
                    _habitatTypeCodes = [.. (from ht in _lutHabitatType
                                             //join htp in _lutHabitatTypePrimary on ht.code equals htp.code_habitat_type
                                         where ht.habitat_class_code == _habitatClass
                                         select ht).Distinct().OrderBy(c => c.sort_order).ThenBy(c => c.description)];

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
                // Check if the habitat type has changed.
                if (string.Equals(_habitatType, value, StringComparison.Ordinal) && _primaryCodes != null && _primaryCodes.Count > 0)
                    return;

                _habitatType = value;

                // Preserve the current selection if it is still valid after refresh.
                string previousIncidPrimary = _incidPrimary;

                // Clear the current list without replacing the collection instance.
                _primaryCodes.Clear();

                //TODO: Use a default (all habitats) if no primary codes are found?

                if (!String.IsNullOrEmpty(_habitatType))
                {
                    // Load all primary habitat codes where the primary habitat code
                    // and primary habitat category are both flagged as local and
                    // are related to the current habitat type.

                    // Combine both primary and secondary-derived codes,
                    // but allow only one entry per code, preferring the one marked as preferred.
                    CodeDescriptionBool[] primaryCodes = [.. (
                        // First query – directly matched primary codes.
                        from p in _lutPrimary
                        join pc in _lutPrimaryCategory on p.category equals pc.code // Needed to only include local categories.
                        from htp in _lutHabitatTypePrimary
                        where htp.code_habitat_type == _habitatType
                            && (p.code == htp.code_primary
                                || (htp.code_primary.EndsWith('*') &&
                                    Regex.IsMatch(p.code, @"\A" + htp.code_primary.TrimEnd('*') + @"")))
                        select new CodeDescriptionBool
                        {
                            Code = p.code,
                            Description = p.description,
                            NVC_codes = p.nvc_codes,
                            Preferred = htp.preferred
                        })
                    .Concat(
                        // Second query – inferred primary codes via secondary links.
                        from s in _lutSecondary
                        join hts in _lutHabitatTypeSecondary on s.code equals hts.code_secondary
                        join ps in _lutPrimarySecondary on hts.code_secondary equals ps.code_secondary
                        from p in _lutPrimary
                        where (ps.code_primary.EndsWith('*')
                                ? Regex.IsMatch(p.code, @"\A" + ps.code_primary.TrimEnd('*') + @"")
                                : p.code == ps.code_primary)
                        join pc in _lutPrimaryCategory on p.category equals pc.code
                        where hts.code_habitat_type == _habitatType
                        select new CodeDescriptionBool
                        {
                            Code = p.code,
                            Description = p.description,
                            NVC_codes = p.nvc_codes,
                            Preferred = false
                        })
                    .DistinctBy(x => new { x.Code, x.Preferred }) // Remove exact duplicates only.
                    .OrderByDescending(x => x.Preferred) // Prefer 'true' if present.
                    .ThenBy(x => x.Code)];

                    foreach (CodeDescriptionBool item in primaryCodes)
                        _primaryCodes.Add(item);

                    // Load all secondary habitat codes where the habitat type
                    // has one of more optional codes.
                    IEnumerable<HluDataSet.lut_secondaryRow> secondaryCodesOptional =
                        [.. (from hts in _lutHabitatTypeSecondary
                         join s in _lutSecondary on hts.code_secondary equals s.code
                         where hts.code_habitat_type == _habitatType
                             && hts.mandatory == 0
                         select s)
                        .OrderBy(r => r.sort_order)
                        .ThenBy(r => r.description)];

                    // Store the list of optional secondary codes.
                    _secondaryCodesOptional = secondaryCodesOptional.Select(hts => hts.code);

                    // Load all secondary habitat codes where the habitat type
                    // has one of more mandatory codes.
                    IEnumerable<HluDataSet.lut_secondaryRow> secondaryCodesMandatory =
                        [.. (from hts in _lutHabitatTypeSecondary
                         join s in _lutSecondary on hts.code_secondary equals s.code
                         where hts.code_habitat_type == _habitatType
                             && hts.mandatory == 1
                         select s)
                        .OrderBy(r => r.sort_order)
                        .ThenBy(r => r.description)];

                    // Store the list of mandatory secondary codes.
                    _secondaryCodesMandatory = secondaryCodesMandatory.Select(hts => hts.code);
                }
                else
                {
                    // Load all primary habitat codes where the primary habitat code
                    // and primary habitat category are both flagged as local.
                    CodeDescriptionBool[] allPrimaryCodes = [.. (
                        from p in _lutPrimary
                        join pc in _lutPrimaryCategory on p.category equals pc.code
                        select new CodeDescriptionBool
                        {
                            Code = p.code,
                            Description = p.description,
                            NVC_codes = p.nvc_codes,
                            Preferred = false
                        })];

                    foreach (CodeDescriptionBool item in allPrimaryCodes)
                        _primaryCodes.Add(item);

                    // Clear the list of optional and mandatory secondary codes.
                    _secondaryCodesOptional = [];
                    _secondaryCodesMandatory = [];
                }

                // If the previous selection is still valid, keep it.
                if (!String.IsNullOrEmpty(previousIncidPrimary) &&
                    _primaryCodes.Any(p => p.Code == previousIncidPrimary))
                {
                    _incidPrimary = previousIncidPrimary;
                }
                else
                {
                    _incidPrimary = null;
                }

                // Refresh the mandatory habitat secondaries and tips.
                OnPropertyChanged(nameof(HabitatSecondariesMandatory));
                OnPropertyChanged(nameof(HabitatSecondariesSuggested));

                // Refresh selection + dependent UI.
                OnPropertyChanged(nameof(IncidPrimary));
                OnPropertyChanged(nameof(PrimaryEnabled));

                // IMPORTANT: no OnPropertyChanged(nameof(PrimaryCodes)) needed here anymore
                // because the ItemsSource collection instance did not change.
            }
        }

        #endregion Properties - Habitat Class/Type

        #region Properties - Primary Habitat

        /// <summary>
        /// Gets the header for the primary habitat group box. It is only shown if
        /// the option to show group headers is set.
        /// </summary>
        /// <value>The header for the primary habitat group box.</value>
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

        /// <summary>
        /// Gets a value indicating whether the primary habitat code combo box is enabled.
        /// </summary>
        /// <value>A value indicating whether the primary habitat code combo box is enabled.</value>
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
        public ObservableCollection<CodeDescriptionBool> PrimaryCodes
        {
            get { return _primaryCodes; }
        }

        /// <summary>
        /// Gets or sets the primary habitat code. When setting, it also updates the list of secondary
        /// habitat codes and tips based on the current primary code and habitat type.
        /// </summary>
        /// <value>The primary habitat code.</value>
        public string IncidPrimary
        {
            get { return _incidPrimary; }
            set
            {
                if (IncidCurrentRow != null)
                {
                    // Guard: if the value hasn't actually changed, skip all the expensive
                    // downstream work (NewPrimaryHabitat, GetBapEnvironments, etc.) that
                    // would otherwise be re-triggered every time WPF pushes back the same
                    // value via the two-way SelectedValue binding during a refresh.
                    if (string.Equals(_incidPrimary, value, StringComparison.Ordinal))
                        return;

                    //TODO: What does this actually do?
                    if (_pasting && (_primaryCodes == null || !_primaryCodes.Any(r => r.Code == value)))
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
                        // Get exact match on the primary code first.
                        var q = (from htp in _lutHabitatTypePrimary
                                 where ((htp.code_habitat_type == _habitatType)
                                 && (htp.code_primary == _incidPrimary))
                                 select htp);

                        // If there is no exact match.
                        if (!q.Any())
                        {
                            // Try to get a wildcard match on the primary code.
                            q = (from htp in _lutHabitatTypePrimary
                                 where ((htp.code_habitat_type == _habitatType)
                                 && (htp.code_primary.EndsWith('*') && Regex.IsMatch(_incidPrimary, @"\A" + htp.code_primary.TrimEnd('*') + @"") == true))
                                 select htp);
                        }

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
        /// Gets the primary habitat category that relates to the selected primary habitat code.
        /// It is used to show the category of the selected primary habitat code and to validate that the
        /// selected primary habitat code is valid.
        /// </summary>
        /// <value>The primary habitat category that relates to the selected primary habitat code.</value>
        public string IncidPrimaryCategory
        {
            get { return _incidPrimaryCategory; }
        }

        #endregion Properties - Primary Habitat

        #region Properties - Secondaries Mandatory/Optional

        /// <summary>
        /// Only show the mandatory habitat secondaries if required.
        /// </summary>
        /// <value>The visibility of the mandatory habitat secondaries.</value>
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
                if (_secondaryCodesMandatory != null)
                    return string.Join(", ", _secondaryCodesMandatory);
                else
                    return null;
            }
        }

        /// <summary>
        /// Only show the suggested habitat secondaries if the option is set, otherwise collapse it.
        /// </summary>
        /// <value>The visibility of the suggested habitat secondaries.</value>
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
                if ((_habitatSecondariesSuggested == null) && (_secondaryCodesOptional != null))
                    return string.Join(", ", _secondaryCodesOptional);
                else
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

        #endregion Properties - Secondaries Mandatory/Optional

        #region Properties - Secondary Habitats

        /// <summary>
        /// Gets the header for the secondary habitats group box. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The header for the secondary habitats group box.</value>
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

        /// <summary>
        /// Gets a value indicating whether the secondary habitat group box is enabled. It is only enabled if there is a primary habitat code selected.
        /// </summary>
        /// <value>A value indicating whether the secondary habitat group box is enabled.</value>
        public bool SecondaryGroupEnabled
        {
            get
            {
                return (!String.IsNullOrEmpty(_incidPrimary));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the secondary habitat controls are enabled. They are enabled if
        /// there is a primary habitat code selected and there are secondary groups that relate to the
        /// selected primary habitat code.
        /// </summary>
        /// <value>A value indicating whether the secondary habitat controls are enabled.</value>
        public bool SecondaryHabitatEnabled
        {
            get
            {
                return (!String.IsNullOrEmpty(_incidPrimary) && !String.IsNullOrEmpty(_secondaryGroup));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the secondary habitat code combo box is enabled. It is enabled if
        /// there is a primary habitat code selected and there are secondary groups that relate to the
        /// selected primary habitat code.
        /// </summary>
        /// <value>A value indicating whether the secondary habitat code combo box is enabled.</value>
        public bool SecondaryHabitatsEnabled
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the list of valid local secondary groups that relate to the selected primary habitat code.
        /// </summary>
        /// <value>The list of valid local secondary groups.</value>
        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodesValid
        {
            get
            {
                if (HluDataset == null)
                    return null;

                // Set the dictionary of local secondary group codes.
                if (SecondaryHabitat.SecondaryGroupCodes == null || SecondaryHabitat.SecondaryGroupCodes.Count == 0)
                    SecondaryHabitat.SecondaryGroupCodes = (from sg in _lutSecondary
                                                        select sg).OrderBy(r => r.code).ThenBy(r => r.code_group).ToDictionary(r => r.code, r => r.code_group);

                // If there is a primary habitat code selected, set the list of valid secondary groups to
                // those that relate to the primary code, otherwise set to all secondary groups.
                if (!String.IsNullOrEmpty(IncidPrimary))
                {
                    // Set the valid list of secondary groups for the primary code.
                    _secondaryGroupsValid = [.. (from sg in _lutSecondaryGroup
                                             join s in _lutSecondary on sg.code equals s.code_group
                                             join ps in _lutPrimarySecondary on s.code equals ps.code_secondary
                                             where ((ps.code_primary == IncidPrimary) || (ps.code_primary.EndsWith('*') && Regex.IsMatch(IncidPrimary, @"\A" + ps.code_primary.TrimEnd('*') + @"") == true))
                                             select sg).OrderBy(r => r.sort_order).ThenBy(r => r.description).Distinct()];

                    if (_secondaryGroupsValid != null)
                    {
                        // Add the <ALL> groups containing all and all essential secondary codes.
                        _secondaryGroupsValid = [_allRow, _allEssRow, .. _secondaryGroupsValid];
                    }
                }
                else
                {
                    // Set the valid list of secondary codes to all codes including any <All> groups.
                    _secondaryGroupsValid = SecondaryGroupCodesWithAll;

                    // Set the combo box list to null (it will also be disabled).
                    return null;
                }

                return _secondaryGroupsValid;
            }
        }

        /// <summary>
        /// Gets the list of all local secondary groups that have at least one secondary habitat code that is local.
        /// </summary>
        /// <value>The list of all local secondary groups.</value>
        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodesAll
        {
            get
            {
                // Set the full list of local secondary groups.
                if (_secondaryGroups == null || _secondaryGroups.Length == 0)
                {
                    // Set the full list of local secondary groups.
                    _secondaryGroups = [.. (from sg in _lutSecondaryGroup
                                        select sg).OrderBy(r => r.sort_order).ThenBy(r => r.description).Distinct()];
                }

                return _secondaryGroups;
            }
        }

        /// <summary>
        /// Gets the list of secondary groups to be used in the options window.
        /// </summary>
        /// <value>The list of secondary groups including any <All> groups.</value>
        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodesWithAll
        {
            get
            {
                // Set the full list of secondary groups including any <All> groups.
                if (_secondaryGroupsWithAll == null || _secondaryGroupsWithAll.Length == 0)
                {
                    // Set the full list of local secondary groups.
                    _secondaryGroups = SecondaryGroupCodesAll;

                    // Set the full list of secondary groups including any <All> groups.
                    HluDataSet.lut_secondary_groupRow[] secondaryGroupsWithAll;
                    secondaryGroupsWithAll = _secondaryGroups;
                    if (secondaryGroupsWithAll != null)
                    {
                        // Add the <ALL> groups containing all and all essential secondary codes.
                        secondaryGroupsWithAll = [_allRow, _allEssRow, .. secondaryGroupsWithAll];
                    }

                    // Set the full list of secondary groups (used in the options window).
                    _secondaryGroupsWithAll = secondaryGroupsWithAll;

                }

                return _secondaryGroupsWithAll;
            }
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

        /// <summary>
        /// Gets the list of valid local secondary habitat codes that relate to the selected primary habitat code and secondary group.
        /// </summary>
        /// <value>The list of valid local secondary habitat codes.</value>
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
                            return [.. _secondaryCodesValid.Where(s => s.sort_order < 100)];
                        }
                        else
                        {
                            // Load all secondary habitat codes that are flagged as local and
                            // relate to the primary habitat and selected secondary group.
                            return [.. _secondaryCodesValid.Where(s => s.code_group == _secondaryGroup)];
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
                            return [.. _secondaryCodesAll.Where(s => s.sort_order < 100)];
                        }
                        else
                        {
                            // Load all secondary habitat codes that are flagged as local
                            // regardless of the primary habitat but related to the
                            // selected secondary group.
                            return [.. _secondaryCodesAll.Where(s => s.code_group == _secondaryGroup)];
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

        /// <summary>
        /// Gets the list of all local secondary habitat codes that are related to any habitat type or primary habitat.
        /// </summary>
        /// <value>The list of all local secondary habitat codes.</value>
        public HluDataSet.lut_secondaryRow[] SecondaryHabitatCodesAll
        {
            get
            {
                _secondaryCodesAll ??= [.. (from s in _lutSecondary
                                          select s).OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return _secondaryCodesAll;
            }
        }

        /// <summary>
        /// Gets or sets the secondary habitat code which will then be added to the list of secondary habitats for the current incid.
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
        /// <value>The collection of secondary habitats.</value>
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

        #endregion Properties - Secondary Habitats

        #region Properties - NVC Codes

        /// <summary>
        /// Only show the NVC Codes if the option is set, otherwise collapse it.
        /// </summary>
        /// <value>The visibility of the NVC codes.</value>
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

        #endregion Properties - NVC Codes

        #region Properties - Habitat Summary

        /// <summary>
        /// Gets the habitat summary header text. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The habitat summary header text.</value>
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
                // If there are no secondary habitats return null.
                if (_incidSecondaryHabitats == null || _incidSecondaryHabitats.Count == 0)
                    return null;

                // Create the concatenated secondary habitats summary.
                _incidSecondarySummary = String.Join(_secondaryCodeDelimiter, _incidSecondaryHabitats
                    .OrderBy(s => s.Secondary_habitat_int)
                    .ThenBy(s => s.Secondary_habitat)
                    .Select(s => s.Secondary_habitat)
                    .Distinct().ToList());

                return _incidSecondarySummary == String.Empty ? null : _incidSecondarySummary;
            }
        }

        /// <summary>
        /// Only show the contatenated habitat summary if the option is set, otherwise collapse it.
        /// </summary>
        /// <value>The visibility of the habitat summary.</value>
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

        #endregion Properties - Habitat Summary

        #region Properties - Legacy Habitat

        /// <summary>
        ///  Gets the legacy habitat group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The legacy habitat group header.</value>
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

        //TODO: Handle very large number of values.
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
                if (_lutLegacyHabitat == null || !_lutLegacyHabitat.Any())
                    return null;

                // Get the list of values from the lookup table.
                _legacyHabitatCodes ??= [.. _lutLegacyHabitat.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Return the list of legacy codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidLegacyHabitat))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_legacy_habitatRow clearRow = HluDataset.lut_legacy_habitat.Newlut_legacy_habitatRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return [clearRow, .. _legacyHabitatCodes];
                }
                else
                {
                    return _legacyHabitatCodes;
                }
            }
        }

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

        #endregion Properties - Legacy Habitat

        #endregion Properties - Habitat Tab

        #region Properties - IHS Tab

        #region Properties - IHS Label/Header

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

        /// <summary>
        /// Gets the IHS habitat group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Label/Header

        #region Properties - IHS Habitat

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

        #endregion Properties - IHS Habitat

        #region Properties - IHS Matrix

        /// <summary>
        /// Gets the ihs matrix group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Matrix

        #region Properties - IHS Formation

        /// <summary>
        /// Gets the ihs formation group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Formation

        #region Properties - IHS Management

        /// <summary>
        /// Gets the ihs management group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Management

        #region Properties - IHS Complex

        /// <summary>
        /// Gets the ihs complex group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Complex

        #region Properties - IHS Summary

        /// <summary>
        /// Gets the ihs summary group header. It is only shown if the option to show group headers is set.
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

        #endregion Properties - IHS Summary

        #endregion Properties - IHS Tab

        #region Properties - Priority Tab

        #region Properties - Priority Label/Header

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

        #endregion Properties - Priority Label/Header

        #region Properties - Priority Habitats

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
                // Enable multiple priority habitat types (from the same or different
                // classifications) to be assigned
                _bapHabitatCodes ??= [.. (from ht in _lutHabitatType
                                        where ht.bap_priority == true
                                        select ht).OrderBy(r => r.sort_order).ThenBy(r => r.description)];

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
                return [.. _lutQualityDetermination.OrderBy(r => r.sort_order).ThenBy(r => r.description)];
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
                    return [.. DeterminationQualityCodes.Where(r => r.code != BapEnvironment.BAPDetQltyUserAdded
                        && r.code != BapEnvironment.BAPDetQltyPrevious)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.description)];
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
                return [.. _lutQualityInterpretation.OrderBy(r => r.sort_order).ThenBy(r => r.description)];
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

        /// <summary>
        /// Gets a value indicating whether the bap habitats auto is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if bap habitats auto is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool BapHabitatsAutoEnabled
        {
            get { return IncidBapHabitatsAuto != null && IncidBapHabitatsAuto.Count > 0; } // return IsNotBulkMode; }
        }

        /// <summary>
        /// Gets a value indicating whether the bap habitats user is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if bap habitats user is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool BapHabitatsUserEnabled
        {
            get
            {
                //TODO: Review this logic
                return true;
                //return IsBulkMode || (IncidBapHabitatsAuto != null &&
                //    IncidBapHabitatsAuto.Count > 0) || (IncidBapHabitatsUser.Count > 0);
            }
        }

        #endregion Properties - Priority Habitats

        #endregion Properties - Priority Tab

        #region Properties - Details Tab

        #region Properties - Details Label/Header

        /// <summary>
        /// Gets the Details tab label.
        /// </summary>
        /// <value>The Details tab label.</value>
        public string DetailsTabLabel
        {
            get { return "Details"; }
        }

        #endregion Properties - Details Label/Header

        #region Properties - Details General Comments

        /// <summary>
        /// Gets the details general comments group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The details general comments group header.</value>
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

        /// <summary>
        /// Gets or sets the incid general comments.
        /// </summary>
        /// <value>The incid general comments.</value>
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

        #endregion Properties - Details General Comments

        #region Properties - Details Maps

        /// <summary>
        /// Gets the details maps group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The details maps group header.</value>
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
                if (_lutBoundaryMap == null || !_lutBoundaryMap.Any())
                    return null;

                // Get the list of values from the lookup table.
                _boundaryMapCodes ??= [.. _lutBoundaryMap.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

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

        #endregion Properties - Details Maps

        #region Properties - Details Site

        /// <summary>
        /// Gets the details site group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The details site group header.</value>
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

        /// <summary>
        /// Display the site reference with the site name in the interface.
        /// </summary>
        /// <value>The site reference.</value>
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

        /// <summary>
        /// Gets or sets the site name to be displayed in the interface.
        /// </summary>
        /// <value>The site name.</value>
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

        #endregion Properties - Details Site

        #region Properties - Details Condition

        /// <summary>
        /// Gets the details condition group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The details condition group header.</value>
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
        /// Gets the list of condition codes.
        /// </summary>
        /// <value>
        /// The list of condition codes.
        /// </value>
        public HluDataSet.lut_conditionRow[] ConditionCodes
        {
            get
            {
                // If there are no condition rows then return an empty list.
                if (_lutCondition == null || !_lutCondition.Any())
                    return [];

                // Get the list of values from the lookup table.
                _conditionCodes ??= [.. _lutCondition.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Return the list of condition codes, with the clear row if applicable.
                if (_incidConditionRows != null &&
                    _incidConditionRows.Length >= 1 &&
                    _incidConditionRows[0] != null &&
                    !_incidConditionRows[0].IsNull(HluDataset.incid_condition.conditionColumn))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_conditionRow clearRow = HluDataset.lut_condition.Newlut_conditionRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return [clearRow, .. _conditionCodes];
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
                if (_lutConditionQualifier == null || !_lutConditionQualifier.Any())
                    return null;

                // Get the list of values from the lookup table.
                _conditionQualifierCodes ??= [.. _lutConditionQualifier.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

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
        /// Checks if the incid condition is non-null and not blank to determine whether the other condition fields should be enabled.
        /// </summary>
        /// <value><c>true</c> if the incid condition fields should be enabled; otherwise, <c>false</c>.</value>
        public bool IncidConditionEnabled
        {
            // Disable remaining condition fields when condition is blank
            get { return (IncidCondition != null); }
        }

        #endregion Properties - Details Condition

        #region Properties - Details Quality

        /// <summary>
        /// Gets the details quality group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The details quality group header.</value>
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
                if (_lutQualityDetermination == null || !_lutQualityDetermination.Any())
                    return null;

                // Get the list of values from the lookup table.
                _qualityDeterminationCodes ??= [.. _lutQualityDetermination.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Return the list of determination codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidQualityDetermination))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_quality_determinationRow clearRow = HluDataset.lut_quality_determination.Newlut_quality_determinationRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return [.. new HluDataSet.lut_quality_determinationRow[] { clearRow }.Concat(_qualityDeterminationCodes)];
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
                if (_lutQualityInterpretation == null || !_lutQualityInterpretation.Any())
                    return null;

                // Get the list of values from the lookup table.
                _qualityInterpretationCodes ??= [.. _lutQualityInterpretation.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                // Return the list of interpretation codes, with the clear row if applicable.
                if (!String.IsNullOrEmpty(IncidQualityInterpretation))
                {
                    // Define the <Clear> row
                    HluDataSet.lut_quality_interpretationRow clearRow = HluDataset.lut_quality_interpretation.Newlut_quality_interpretationRow();
                    clearRow.code = _codeDeleteRow;
                    clearRow.description = _codeDeleteRow;
                    clearRow.sort_order = -1;

                    // Add the <Clear> row
                    return [clearRow, .. _qualityInterpretationCodes];
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

        #endregion Properties - Details Quality

        #endregion Properties - Details Tab

        #region Properties - Sources Tab

        #region Properties - Sources Label/Header

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

        #endregion Properties - Sources Label/Header

        #region Properties - Sources

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
                if (_lutSources == null || !_lutSources.Any())
                    return null;

                // Get the list of values from the lookup table.
                _sourceNames ??= [.. _lutSources.OrderBy(r => r.sort_order).ThenBy(r => r.source_name)];

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
                if (_lutHabitatClass == null || !_lutHabitatClass.Any())
                    return null;

                // Get the list of values from the lookup table.
                _sourceHabitatClassCodes ??= [.. _lutHabitatClass
                        .OrderBy(r => r.sort_order).ThenBy(r => r.description)];

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
                if (_lutImportance == null || !_lutImportance.Any())
                    return null;

                // Get the list of values from the lookup table.
                _sourceImportanceCodes ??= [.. _lutImportance.OrderBy(r => r.sort_order).ThenBy(r => r.description)];

                return _sourceImportanceCodes;
            }
        }

        #endregion Properties - Sources

        #region Properties - Source 1

        /// <summary>
        /// Gets the source 1 group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The source 1 group header.</value>
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

        /// <summary>
        /// Gets the visibility of the source numbers field. It is only shown when group headers are not shown
        /// as the source number is included in the group header when group headers are shown.
        /// </summary>
        /// <value>The visibility of the source numbers field.</value>
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

        /// <summary>
        /// Gets the list of source names to be shown in the source 1 name drop-down list. It includes
        /// the <Clear> row if a source is currently selected, but excludes the <Clear> row in bulk update mode.
        /// </summary>
        /// <value>An array of source rows for source 1.</value>
        public HluDataSet.lut_sourcesRow[] Source1Names
        {
            get
            {
                if (HluDataset == null)
                    return null;

                // Load the lookup table if not already loaded.
                if (HluDataset.lut_sources.IsInitialized &&
                    HluDataset.lut_sources.Count == 0)
                {
                    _hluTableAdapterMgr.lut_sourcesTableAdapter ??= new HluTableAdapter<HluDataSet.lut_sourcesDataTable, HluDataSet.lut_sourcesRow>(_db);
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
                    return [clearRow, .. SourceNames];
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        /// <summary>
        /// Gets or sets the source 1 name by source_id. Setting this property will update the source_id field
        /// in the incid_sources table and will also set default values for the other source 1 fields if a
        /// new source is selected. If the value is set to -1 then the source will be cleared (i.e. the row
        /// will be deleted) and all source 1 fields will be cleared.
        /// </summary>
        /// <value>The source 1 ID.</value>
        public Nullable<int> IncidSource1Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = [.. _incidSourcesRows, .. tmpRows];
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

        /// <summary>
        /// Gets a boolean value indicating whether the source 1 fields should be enabled.
        /// This is determined by whether a source name has been selected for source 1.
        /// </summary>
        /// <value><c>true</c> if the source 1 fields should be enabled; otherwise, <c>false</c>.</value>
        public bool IncidSource1Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource1Id != null); }
        }

        /// <summary>
        /// Gets or sets the source 1 date as a VagueDateInstance. Setting this property will
        /// update the source_date_start, source_date_end and source_date_type fields
        /// in the incid_sources table for source 1.
        /// </summary>
        /// <value>The source 1 date as a VagueDateInstance.</value>
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

        /// <summary>
        /// Gets or sets the source 1 habitat class code. Setting this property will update the source_habitat_class field
        /// in the incid_sources table for source 1.
        /// </summary>
        /// <value>The source 1 habitat class code.</value>
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

        /// <summary>
        /// Gets the list of source 1 habitat type codes to be shown in the source 1 habitat type drop-down list.
        /// The list is filtered based on the selected source 1 habitat class code. It includes the <Clear> row
        /// if a habitat type is currently selected, but excludes the <Clear> row in bulk update mode.
        /// </summary>
        /// <value>An array of habitat type rows for source 1.</value>
        public HluDataSet.lut_habitat_typeRow[] Source1HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table.
                if (!String.IsNullOrEmpty(IncidSource1HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = [.. _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource1HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name)];

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

        /// <summary>
        /// Gets or sets the source 1 habitat type code. Setting this property will update the source_habitat_type field
        /// in the incid_sources table for source 1.
        /// </summary>
        /// <value>The source 1 habitat type code.</value>
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

        /// <summary>
        /// Gets or sets the source 1 boundary importance code. Setting this property will update the source_boundary_importance field
        /// in the incid_sources table for source 1.
        /// </summary>
        /// <value>The source 1 boundary importance code.</value>
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

        /// <summary>
        /// Gets or sets the source 1 habitat importance code. Setting this property will update the source_habitat_importance field
        /// in the incid_sources table for source 1.
        /// </summary>
        /// <value>The source 1 habitat importance code.</value>
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

        #endregion Properties - Source 1

        #region Properties - Source 2

        /// <summary>
        /// Gets the source 2 group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The source 2 group header.</value>
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

        /// <summary>
        /// Gets the list of source names to be shown in the source 2 name drop-down list. It includes
        /// the <Clear> row if a habitat type is currently selected, but excludes the <Clear> row in bulk update mode.
        /// </summary>
        /// <value>An array of source rows for source 2.</value>
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
                    return [clearRow, .. SourceNames];
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        /// <summary>
        /// Gets or sets the source 2 name by source_id. Setting this property will update the source_id field
        /// in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 ID.</value>
        public Nullable<int> IncidSource2Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = [.. _incidSourcesRows, .. tmpRows];
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

        /// <summary>
        /// Gets a boolean value indicating whether the source 2 fields should be enabled.
        /// This is determined by whether a source name has been selected for source 2.
        /// </summary>
        /// <value><c>true</c> if the source 2 fields should be enabled; otherwise, <c>false</c>.</value>
        public bool IncidSource2Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource2Id != null); }
        }

        /// <summary>
        /// Gets or sets the source 2 date as a VagueDateInstance. Setting this property will
        /// update the source_date_start, source_date_end, and source_date_type fields in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 date as a VagueDateInstance.</value>
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

        /// <summary>
        /// Gets or sets the source 2 habitat class code. Setting this property will update the source_habitat_class field
        /// in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 habitat class code.</value>
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

        /// <summary>
        /// Gets the list of source 2 habitat type codes to be shown in the source 2 habitat type drop-down list.
        /// </summary>
        /// <value>An array of habitat type rows for source 2.</value>
        public HluDataSet.lut_habitat_typeRow[] Source2HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table
                if (!String.IsNullOrEmpty(IncidSource2HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = [.. _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource2HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name)];

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

        /// <summary>
        /// Gets or sets the source 2 habitat type code. Setting this property will update the source_habitat_type field
        /// in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 habitat type code.</value>
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

        /// <summary>
        /// Gets or sets the source 2 boundary importance code. Setting this property will update the source_boundary_importance field
        /// in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 boundary importance code.</value>
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

        /// <summary>
        /// Gets or sets the source 2 habitat importance code. Setting this property will update the source_habitat_importance field
        /// in the incid_sources table for source 2.
        /// </summary>
        /// <value>The source 2 habitat importance code.</value>
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

        #endregion Properties - Source 2

        #region Properties - Source 3

        /// <summary>
        /// Gets the source 3 group header. It is only shown if the option to show group headers is set.
        /// </summary>
        /// <value>The source 3 group header.</value>
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

        /// <summary>
        /// Gets the list of source names to be shown in the source 3 name drop-down list. It includes
        /// the '<Clear>' row if applicable, but excludes the '<Clear>' row in bulk update mode.
        /// </summary>
        /// <value>The list of source 3 names.</value>
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
                    return [clearRow, .. SourceNames];
                }
                else
                {
                    return SourceNames;
                }
            }
        }

        /// <summary>
        /// Gets or sets the source 3 name by source_id. Setting this property will update the source_id field
        /// in the incid_sources table for source 3.
        /// </summary>
        /// <value>The source 3 ID.</value>
        public Nullable<int> IncidSource3Id
        {
            get
            {
                if (!CheckSources()) return null;

                if (_incidSourcesRows.Length < 3)
                {
                    HluDataSet.incid_sourcesRow[] tmpRows = new HluDataSet.incid_sourcesRow[3 - _incidSourcesRows.Length];
                    _incidSourcesRows = [.. _incidSourcesRows, .. tmpRows];
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

        /// <summary>
        /// Gets a boolean value indicating whether the source 3 fields should be enabled.
        /// This is determined by whether a source name has been selected for source 3.
        /// </summary>
        /// <value><c>true</c> if the source 3 fields should be enabled; otherwise, <c>false</c>.</value>
        public bool IncidSource3Enabled
        {
            // Disable remaining source fields when source name is blank
            get { return (IncidSource3Id != null); }
        }

        /// <summary>
        /// Gets or sets the source 3 date as a VagueDateInstance. Setting this property will
        /// update the source_date_start, source_date_end, and source_date_type fields in the incid_sources table for source 3.
        /// </summary>
        /// <value>The source 3 date as a VagueDateInstance.</value>
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

        /// <summary>
        /// Gets or sets the source 3 habitat class code. Setting this property will update the source_habitat_class field
        /// in the incid_sources table for source 3.
        /// </summary>
        /// <value>The source 3 habitat class code.</value>
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

        /// <summary>
        /// Gets the list of source 3 habitat type codes to be shown in the source 3 habitat type drop-down list.
        /// </summary>
        /// <value>The source 3 habitat type codes.</value>
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

        /// <summary>
        /// Gets the list of source 3 habitat type codes to be shown in the source 3 habitat type drop-down list.
        /// </summary>
        /// <value>The source 3 habitat type codes.</value>
        public HluDataSet.lut_habitat_typeRow[] Source3HabitatTypeCodes
        {
            get
            {
                // Get the list of values from the lookup table
                if (!String.IsNullOrEmpty(IncidSource3HabitatClass))
                {
                    // Load the habitat types for the selected habitat class.
                    HluDataSet.lut_habitat_typeRow[] retArray = [.. _lutHabitatType
                        .Where(r => r.habitat_class_code == IncidSource3HabitatClass)
                        .OrderBy(r => r.sort_order).ThenBy(r => r.name)];

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

        /// <summary>
        /// Gets or sets the source 3 boundary importance code. Setting this property will update the source_boundary_importance field
        /// in the incid_sources table for source 3.
        /// </summary>
        /// <value>The source 3 boundary importance code.</value>
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

        /// <summary>
        /// Gets or sets the source 3 habitat importance code. Setting this property will update the source_habitat_importance field
        /// in the incid_sources table for source 3.
        /// </summary>
        /// <value>The source 3 habitat importance code.</value>
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

        #endregion Properties - Source 3

        #endregion Properties - Sources Tab

        #region Properties - History Tab

        /// <summary>
        /// Gets the incid history and formats it ready for display in the form.
        /// </summary>
        /// <value>
        /// The incid history for the current record, formatted as a list of
        /// strings, each representing a history entry ordered from most recent
        /// to least recent, and grouped by history_id to avoid duplicate entries
        /// when multiple columns were modified in the same update. Each entry
        /// includes the modified date and time, user, process, reason, operation,
        /// and any modified values configured to display based on user options.
        /// Geometry values (length and area) are shown in kilometres and hectares
        /// when the user option is enabled. Returns null if no history exists.
        /// </value>
        public IEnumerable<string> IncidHistory
        {
            get
            {
                if (_incidHistoryRows == null)
                    return null;

                DataColumn[] displayColumns =
                    BuildDisplayHistoryColumns();

                return _incidHistoryRows
                    .OrderByDescending(r => r.history_id)
                    .GroupBy(r => BuildHistoryGroupKey(r, displayColumns))
                    .Select(g => FormatHistoryEntry(g))
                    .Take(_historyDisplayLastN);
            }
        }

        /// <summary>
        /// Builds the array of columns to display in the history tab, combining
        /// the GIS ID columns with any user-selected additional columns (excluding
        /// GIS ID column ordinals and shape columns).
        /// </summary>
        /// <returns>
        /// An array of <see cref="DataColumn"/> instances representing the columns
        /// to display in the history tab.
        /// </returns>
        private DataColumn[] BuildDisplayHistoryColumns()
        {
            int result;
            return
            [
                .. _gisIDColumns,
                .. (from s in Settings.Default.HistoryColumnOrdinals.Cast<string>()
                    where Int32.TryParse(s, out result)
                        && result >= 0
                        && result < _hluDS.incid_mm_polygons.Columns.Count
                        && !_gisIDColumnOrdinals.Contains(result)
                    select _hluDS.incid_mm_polygons.Columns[Int32.Parse(s)]),
            ];
        }

        /// <summary>
        /// Builds the anonymous group key for a history row, capturing all the
        /// fields that must be identical for two rows to be considered part of the
        /// same logical history entry.
        /// </summary>
        /// <param name="r">The history row to derive the key from.</param>
        /// <param name="displayColumns">
        /// The display columns used to determine which optional fields to include.
        /// </param>
        /// <returns>An anonymous object suitable for use as a LINQ group key.</returns>
        private static object BuildHistoryGroupKey(
            HluDataSet.historyRow r,
            DataColumn[] displayColumns)
        {
            return new
            {
                r.incid,

                // Include time to prevent separate updates with identical details
                // (except the time) being merged together when displayed.
                modified_date = !r.Ismodified_dateNull()
                    ? r.modified_date.ToShortDateString()
                    : String.Empty,
                modified_time = (!r.Ismodified_dateNull()
                    && r.modified_date != r.modified_date.Date)
                    ? @" at " + r.modified_date.ToLongTimeString()
                    : String.Empty,

                modified_user_id = r.lut_userRow != null
                    ? r.lut_userRow.user_name
                    : !r.Ismodified_user_idNull() ? r.modified_user_id : String.Empty,

                modified_process = r.lut_processRow != null
                    ? r.lut_processRow.description
                    : String.Empty,
                modified_reason = r.lut_reasonRow != null
                    ? r.lut_reasonRow.description
                    : String.Empty,
                modified_operation = r.lut_operationRow != null
                    ? r.lut_operationRow.description
                    : String.Empty,

                // Only show the previous incid when it differs from the current.
                modified_incid = !r.Ismodified_incidNull()
                    ? String.Format("{0}", r.modified_incid == r.incid
                        ? null
                        : "\n\tPrevious INCID: " + r.modified_incid)
                    : String.Empty,

                // Optional habitat columns - only included when the column is selected.
                modified_primary = HasColumn(displayColumns, "habprimary")
                    ? String.Format("\n\tPrevious Primary: {0}", r.modified_habprimary)
                    : String.Empty,
                modified_secondaries = HasColumn(displayColumns, "habsecond")
                    ? String.Format("\n\tPrevious Secondaries: {0}", r.modified_habsecond)
                    : String.Empty,

                // Optional quality columns - only included when not null.
                modified_determination = HasColumn(displayColumns, "determqty")
                    ? r.lut_quality_determinationRow != null
                        ? String.Format("\n\tPrevious Determination: {0}",
                            r.lut_quality_determinationRow.description)
                        : String.Empty
                    : String.Empty,
                modified_interpretation = HasColumn(displayColumns, "interpqty")
                    ? r.lut_quality_interpretationRow != null
                        ? String.Format("\n\tPrevious Interpretation: {0}",
                            r.lut_quality_interpretationRow.description)
                        : String.Empty
                    : String.Empty,
            };
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="columns"/> contains
        /// exactly one column whose name matches <paramref name="columnName"/>.
        /// </summary>
        /// <param name="columns">The display column array to search.</param>
        /// <param name="columnName">The column name to look for.</param>
        /// <returns>
        /// <see langword="true"/> if the column is present; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        private static bool HasColumn(DataColumn[] columns, string columnName)
        {
            return columns.Count(c => c.ColumnName == columnName) == 1;
        }

        /// <summary>
        /// Formats a grouped set of history rows into a single display string.
        /// </summary>
        /// <param name="g">
        /// The group produced by <see cref="IncidHistory"/>, keyed on the fields
        /// built by <see cref="BuildHistoryGroupKey"/>.
        /// </param>
        /// <returns>
        /// A multi-line string representing a single history entry, including the
        /// operation header, process, reason, any changed field values, and
        /// (when the user option is enabled) the modified length and area.
        /// </returns>
        private string FormatHistoryEntry(
            IGrouping<dynamic, HluDataSet.historyRow> g)
        {
            string header =
                String.Format("{0} on {1}{2} by {3}:",
                    g.Key.modified_operation,
                    g.Key.modified_date,
                    g.Key.modified_time,
                    g.Key.modified_user_id);

            string processReason =
                String.Format("\n\tProcess: {0}", g.Key.modified_process) +
                String.Format("\n\tReason: {0}", g.Key.modified_reason);

            string changedFields =
                g.Key.modified_incid +
                g.Key.modified_primary +
                g.Key.modified_secondaries +
                g.Key.modified_determination +
                g.Key.modified_interpretation;

            string geometry = Settings.Default.HistoryDisplayGeometry
                ? FormatHistoryGeometry(g)
                : String.Empty;

            return header + processReason + changedFields + geometry;
        }

        /// <summary>
        /// Formats the modified length and area values for a history entry,
        /// summing distinct polygon rows and expressing the totals in kilometres
        /// and hectares respectively.
        /// </summary>
        /// <param name="g">The grouped history rows for one logical history entry.</param>
        /// <returns>
        /// A two-line string containing the modified length [km] and area [ha],
        /// or an empty string when no geometry data is available.
        /// </returns>
        private string FormatHistoryGeometry(
            IGrouping<dynamic, HluDataSet.historyRow> g)
        {
            IEnumerable<HluDataSet.historyRow> distinctRows =
                g.Distinct(_histRowEqComp);

            double lengthKm = Math.Round(
                distinctRows.Sum(r => !r.Ismodified_lengthNull()
                    ? r.modified_length / 1000 : 0), 3);

            double areaHa = Math.Round(
                distinctRows.Sum(r => !r.Ismodified_areaNull()
                    ? r.modified_area / 10000 : 0), 4);

            return
                String.Format("\n\tModified Length: {0} [km]",
                    lengthKm.ToString("f3")) +
                String.Format("\n\tModified Area: {0} [ha]",
                    areaHa.ToString("f4"));
        }

        #endregion Properties - History Tab

        #region Properties - History Label/Header

        /// <summary>
        /// Gets the history tab group label.
        /// </summary>
        /// <value>
        /// The history tab group label.
        /// </value>
        public string HistoryTabLabel
        {
            get { return "History"; }
        }

        #endregion Properties - History Label/Header

        #region Properties - Site Info

        /// <summary>
        /// Gets the Site ID for the current record.
        /// </summary>
        /// <value>The Site ID for the current record.</value>
        public string SiteID { get { return _recIDs.SiteID; } }

        /// <summary>
        /// Gets the Habitat Version for the current record.
        /// </summary>
        /// <value>The Habitat Version for the current record.</value>
        public string HabitatVersion { get { return _recIDs.HabitatVersion; } }

        #endregion Properties - Site Info

        #region Properties - Status Bar

        /// <summary>
        /// Gets the status string for the current Incid selection.
        /// </summary>
        /// <value>The status string for the current Incid selection.</value>
        public string StatusIncid
        {
            get
            {
                if (IsOsmmReviewMode && !IsFiltered)
                    return null;

                if (IsFiltered)
                {
                    // Include the total toid and fragment counts for the current Incid
                    // in the status area in addition to the currently select toid and
                    // fragment counts.
                    if (IsOsmmReviewMode)
                        return String.Format(" of {0}* [{1}:{2}]", _incidSelection.Rows.Count.ToString("N0"),
                            _currentIncidToidsInDBCount.ToString(),
                            _currentIncidFragsInDBCount.ToString());
                    else if (IsOsmmBulkMode)
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _incidSelection.Rows.Count.ToString("N0"),
                            _selectedToidsInGISCount.ToString(),
                            _selectedFragsInGISCount.ToString());
                    else
                        return String.Format(" of {0}* [{1}:{2} of {3}:{4}]", _incidSelection.Rows.Count.ToString("N0"),
                            _currentIncidToidsInGISCount.ToString(),
                            _currentIncidFragsInGISCount.ToString(),
                            _currentIncidToidsInDBCount.ToString(),
                            _currentIncidFragsInDBCount.ToString());
                }
                else if (IsBulkMode)
                {
                    if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0))
                    {
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _selectedIncidsInGISCount.ToString("N0"),
                            _selectedToidsInGISCount.ToString(),
                            _selectedFragsInGISCount.ToString());
                    }
                    else
                    {
                        return String.Format("[I:{0}] [T: {1}] [F: {2}]", _selectedIncidsInGISCount.ToString("N0"),
                            _selectedToidsInGISCount.ToString(),
                            _selectedFragsInGISCount.ToString());
                    }
                }
                else
                {
                    // Include the total toid and fragment counts for the current Incid
                    // in the status area, and the currently select toid and fragment
                    // counts, when auto selecting features on change of incid.
                    if (IsOsmmReviewMode)
                        return String.Format(" of {0}* [{1}:{2}]", _incidRowCount.ToString("N0"),
                            _currentIncidToidsInDBCount.ToString(),
                            _currentIncidFragsInDBCount.ToString());
                    else
                        return String.Format(" of {0} [{1}:{2} of {3}:{4}]", _incidRowCount.ToString("N0"),
                            _currentIncidToidsInGISCount.ToString(),
                            _currentIncidFragsInGISCount.ToString(),
                            _currentIncidToidsInDBCount.ToString(),
                            _currentIncidFragsInDBCount.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the status string for the current map selection, which is shown in the status bar.
        /// </summary>
        /// <value>The status string for the current map selection.</value>
        public string StatusBar
        {
            get { return _windowCursor == Cursors.Wait ? _processingMsg : String.Empty; }
        }

        #endregion Properties - Status Bar

        #region Properties - Priority Habitats

        /// <summary>
        /// Gets the command to edit priority habitats.
        /// </summary>
        /// <value>The command to edit priority habitats.</value>
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

        /// <summary>
        /// Gets a value indicating whether the priority habitats can be edited.
        /// </summary>
        /// <value><c>true</c> if the priority habitats can be edited; otherwise, <c>false</c>.</value>
        public bool CanEditPriorityHabitats
        {
            get { return IsNotBulkMode && IsNotOsmmReviewMode && BapHabitatsAutoEnabled; }
        }

        /// <summary>
        /// Gets the command to edit potential habitats.
        /// </summary>
        /// <value>The command to edit potential habitats.</value>
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

        /// <summary>
        /// Gets a value indicating whether the potential priority habitats can be edited.
        /// </summary>
        /// <value><c>true</c> if the potential priority habitats can be edited; otherwise, <c>false</c>.</value>
        public bool CanEditPotentialHabitats
        {
            get { return IsNotBulkMode && IsNotOsmmReviewMode && BapHabitatsUserEnabled; }
        }

        /// <summary>
        /// Gets the command to add a secondary habitat.
        /// </summary>
        /// <value>The command to add a secondary habitat.</value>
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

        /// <summary>
        /// Gets a value indicating whether a secondary habitat can be added.
        /// </summary>
        /// <value><c>true</c> if a secondary habitat can be added; otherwise, <c>false</c>.</value>
        public bool CanAddSecondaryHabitat
        {
            get
            {
                // Check not in OSMM update mode and GIS present and primary
                // code and secondary habitat group and code have been set.
                return (IsNotOsmmReviewMode
                    && _incidPrimary != null && _secondaryGroup != null && _secondaryHabitat != null);
            }
        }

        /// <summary>
        /// Gets the command to add a list of secondary habitats.
        /// </summary>
        /// <value>The command to add a list of secondary habitats.</value>
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

        /// <summary>
        /// Gets a value indicating whether a list of secondary habitats can be added.
        /// </summary>
        /// <value><c>true</c> if a list of secondary habitats can be added; otherwise, <c>false</c>.</value>
        public bool CanAddSecondaryHabitatList
        {
            get
            {
                // Check not in OSMM update mode and GIS present and primary
                // code and secondary habitat group and code have been set.
                return (IsNotOsmmReviewMode
                    && _incidPrimary != null);
            }
        }

        #endregion Properties - Priority Habitats

        #region Properties - Export

        /// <summary>
        /// Gets a value indicating whether an export can be performed, which is used to
        /// enable or disable the export command.
        /// </summary>
        /// <value><c>true</c> if an export can be performed; otherwise, <c>false</c>.</value>
        public bool CanExport { get { return IsNotBulkMode && IsNotOsmmReviewMode && _hluDS != null; } }

        #endregion Properties - Export

        #region Properties - Filter

        /// <summary>
        /// Gets a value indicating whether the filter by attributes command can
        /// be executed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by attributes; otherwise, <c>false</c>.
        /// </value>
        public bool CanFilterByAttributes
        {
            get
            {
                // Enable filter when in OSMM bulk update mode
                return (IsNotBulkMode || (IsBulkMode && IsOsmmBulkMode))
                && IncidCurrentRow != null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the filter by incid command can
        /// be clicked.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by incid; otherwise, <c>false</c>.
        /// </value>
        public bool CanFilterByIncid
        {
            get { return IsNotBulkMode && IsNotOsmmReviewMode && IncidCurrentRow != null; }
        }

        /// <summary>
        /// Gets the command to filter by attributes OSMM.
        /// </summary>
        /// <value>The command to filter by attributes OSMM.</value>
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
        /// Gets a value indicating whether the filter by attributes OSMM command can
        /// be executed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can filter by attributes OSMM; otherwise, <c>false</c>.
        /// </value>
        private bool CanFilterByAttributesOSMM
        {
            get
            {
                // Enable filter when in OSMM bulk update mode
                return (IsOsmmReviewMode && IncidCurrentRow != null);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the filter can be cleared.
        /// </summary>
        /// <value><c>true</c> if the filter can be cleared; otherwise, <c>false</c>.</value>
        public bool CanClearFilter
        {
            get
            {
                // Don't allow filter to be cleared when in OSMM Update mode or
                // OSMM Bulk Update mode.
                return IsFiltered == true &&
                    IsNotOsmmReviewMode &&
                    IsNotOsmmBulkMode;
            }
        }

        #endregion Properties - Filter

        #region Properties - Progress/Status

        /// <summary>
        /// Gets a value indicating whether the form is processing.
        /// </summary>
        /// <value><c>true</c> if the form is processing; otherwise, <c>false</c>.</value>
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

        /// <summary>
        /// Gets or sets the value to set on the progress bar.
        /// </summary>
        /// <value>The value to set on the progress bar.</value>
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

        /// <summary>
        /// Gets or sets the maximum value for the progress bar.
        /// </summary>
        /// <value>The maximum value for the progress bar.</value>
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

        /// <summary>
        /// Gets or sets the process status text to be shown in the status bar when processing.
        /// Setting this property will also update the visibility of the progress bar and status text.
        /// </summary>
        /// <value>The text to display for the process status.</value>
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

        /// <summary>
        /// Gets or sets the text to display on the progress bar.
        /// </summary>
        /// <value>The text to display on the progress bar.</value>
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
        /// Gets a value indicating whether the progress wheel is animating.
        /// </summary>
        /// <value><c>true</c> if the progress wheel is animating; otherwise, <c>false</c>.</value>
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

        #endregion Properties - Progress/Status

        #region Properties - Navigation

        /// <summary>
        /// Gets the command to navigate to the first record.
        /// </summary>
        /// <value>The command to navigate to the first record.</value>
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

        /// <summary>
        /// Gets the command to navigate to the previous record.
        /// </summary>
        /// <value>The command to navigate to the previous record.</value>
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

        /// <summary>
        /// Gets a value indicating whether the user can navigate backward.
        /// </summary>
        /// <value><c>true</c> if the user can navigate backward; otherwise, <c>false</c>.</value>
        public bool CanNavigateBackward
        {
            get { return IncidCurrentRowIndex > 1; }
        }

        /// <summary>
        /// Gets the command to navigate to the next record.
        /// </summary>
        /// <value>The command to navigate to the next record.</value>
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

        /// <summary>
        /// Gets the command to navigate to the last record.
        /// </summary>
        /// <value>The command to navigate to the last record.</value>
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

        /// <summary>
        /// Gets a value indicating whether the user can navigate forward.
        /// </summary>
        /// <value><c>true</c> if the user can navigate forward; otherwise, <c>false</c>.</value>
        public bool CanNavigateForward
        {
            get
            {
                return ((IsFiltered && (IncidCurrentRowIndex < _incidSelection.Rows.Count)) ||
                    (!IsFiltered && (IncidCurrentRowIndex < _incidRowCount)));
            }
        }

        /// <summary>
        /// Gets the command to navigate to the specified record.
        /// </summary>
        /// <value>The command to navigate to the specified record.</value>
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

        /// <summary>
        /// Gets a value indicating whether the user can navigate to the specified record.
        /// </summary>
        /// <value><c>true</c> if the user can navigate to the specified record; otherwise, <c>false</c>.</value>
        private bool CanNavigateIncid
        {
            get
            {
                // Is the requested record less than the total number of records
                // if filtered, or the total row count if not filtered?
                return (IncidCurrentRowIndex > 1) ||
                    ((IsFiltered && (IncidCurrentRowIndex < _incidSelection.Rows.Count)) ||
                    (!IsFiltered && (IncidCurrentRowIndex < _incidRowCount)));
            }
        }

        #endregion Properties - Navigation

        #region Properties - Update/Edit

        /// <summary>
        /// Gets the command to update the current record.
        /// </summary>
        /// <value>The command to update the current record.</value>
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

        /// <summary>
        /// Update is disabled if not currently in edit mode, if no changes have been
        /// made by the user, if we're not currently in bulk update mode with no records
        /// selected, or if the current record is in error.
        /// </summary>
        /// <value><c>true</c> if the update can be performed; otherwise, <c>false</c>.</value>
        public bool CanUpdate
        {
            get
            {
                // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
                if (!IsEditOperationModeReady)
                    return false;

                return (Changed == true) &&
                    String.IsNullOrEmpty(this.Error);
            }
        }

        /// <summary>
        /// Indicates whether the HLU layer is currently editable for the
        /// authorised user. This is computed from the GIS application state
        /// (authorisation + layer editability).
        ///
        /// The WorkMode property exposes this state via the WorkMode.CanEdit flag,
        /// but EditMode remains the authoritative source for whether the user can
        /// actually edit features.
        /// </summary>
        /// <value><c>true</c> if the user can edit features; otherwise, <c>false</c>.</value>
        public bool CanEdit
        {
            get
            {
                return WorkMode.HasFlag(WorkMode.CanEdit);
            }
        }

        #endregion Properties - Update/Edit

        #region Properties - Bulk Update

        /// <summary>
        /// Is the user authorised for bulk updates?
        /// </summary>
        /// <value><c>true</c> if the user is authorised for bulk updates; otherwise, <c>false</c>.</value>
        public bool CanUserBulkUpdate
        {
            get
            {
                if (_canUserBulkUpdate == null) GetUserInfo();

                return _canUserBulkUpdate == true;
            }
        }

        /// <summary>
        /// Can bulk update mode be started?
        /// </summary>
        /// <value><c>true</c> if bulk update mode can be started; otherwise, <c>false</c>.</value>
        public bool CanBulkUpdate
        {
            get
            {
                // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection),
                // the user must be authorised for bulk updates,
                // we must not already be in OSMM Review or OSMM Bulk mode,
                // and either there must be a filter active or we must already be in bulk update mode (i.e. we're clicking the Apply button to action the bulk update).
                return IsEditReady &&
                    CanUserBulkUpdate == true &&
                    !WorkMode.HasAny(WorkMode.OSMMReview | WorkMode.OSMMBulk) &&
                    (IsFiltered || WorkMode.HasAll(WorkMode.Bulk));
            }
        }

        /// <summary>
        /// Can the bulk update be cancelled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can cancel bulk update; otherwise, <c>false</c>.
        /// </value>
        public bool CanCancelBulkUpdate { get { return IsBulkMode; } }

        /// <summary>
        /// Indicates whether the Bulk Update workflow is active.
        ///
        /// This is a thin wrapper over WorkMode that toggles the HluEditMode.Bulk
        /// flag while leaving other flags unchanged.
        /// </summary>
        /// <value><c>true</c> if bulk update mode is active; otherwise, <c>false</c>.</value>
        internal bool BulkUpdateMode
        {
            get
            {
                return WorkMode.HasFlag(WorkMode.Bulk);
            }
            set
            {
                // Add or remove the Bulk flag as appropriate.
                SetWorkModeFlag(WorkMode.Bulk, value);
            }
        }

        /// <summary>
        /// Gets the visibility of controls that should be hidden when in bulk update mode.
        /// </summary>
        /// <value>The visibility of controls that should be hidden when in bulk update mode.</value>
        public Visibility HideInBulkUpdateMode
        {
            get
            {
                if (IsBulkMode)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
            set { }
        }

        /// <summary>
        /// Gets the visibility of controls that should be shown when in bulk update mode.
        /// </summary>
        /// <value>The visibility of controls that should be shown when in bulk update mode.</value>
        public Visibility ShowInBulkUpdateMode
        {
            get
            {
                if (IsBulkMode)
                    return Visibility.Visible;
                else
                    return Visibility.Hidden;
            }
            set { }
        }

        /// <summary>
        /// Gets the header for the group of controls related to bulk update, which is shown when in bulk
        /// update mode if the option to show group headers is enabled.
        /// </summary>
        /// <value>The header for the group of controls related to bulk update.</value>
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

        /// <summary>
        /// Gets the header for the group of controls related to bulk update, which is shown when in bulk
        /// update mode if the option to show group headers is enabled.
        /// </summary>
        /// <value>The header for the bulk update group of controls.</value>
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

        #endregion Properties - Bulk Update

        #region Properties - OSMM Update

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
                if (IsOsmmReviewMode)
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
                if (IsOsmmReviewMode)
                    OnPropertyChanged(nameof(OSMMRejectText));
            }
        }

        /// <summary>
        /// Can OSMM Update mode be started?
        /// </summary>
        /// <value><c>true</c> if this instance can OSMM update mode; otherwise, <c>false</c>.</value>
        public bool CanOSMMUpdateMode
        {
            get
            {
                // Must be in edit mode,
                // there must be proposed OSMM Updates for the current filter,
                // the user must be authorised for bulk updates,
                // and we must not already be in OSMM Review or OSMM Bulk mode.
                return IsEditMode &&
                    AnyOSMMUpdates == true &&
                    CanUserBulkUpdate == true &&
                    !WorkMode.HasAny(WorkMode.Bulk | WorkMode.OSMMBulk);
            }
        }

        /// <summary>
        /// Can the OSMM Update be cancelled.
        /// </summary>
        /// <value><c>true</c> if this instance can cancel OSMM update; otherwise, <c>false</c>.</value>
        public bool CanCancelOSMMUpdate { get { return IsOsmmReviewMode; } }

        /// <summary>
        /// OSMM Skip command.
        /// </summary>
        /// <value>The command to skip the proposed OSMM Update for the current incid.</value>
        public ICommand OSMMSkipCommand
        {
            get
            {
                if (_osmmSkipCommand == null)
                {
                    Action<object> osmmSkipAction = new(this.OSMMSkipClicked);
                    _osmmSkipCommand = new RelayCommand(osmmSkipAction, param => this.CanOSMMSkip);
                }
                return _osmmSkipCommand;
            }
        }

        /// <summary>
        /// Can the proposed OSMM Update for the current incid
        /// be skipped?
        /// </summary>
        /// <value><c>true</c> if the proposed OSMM Update for the current incid can be skipped; otherwise, <c>false</c>.</value>
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
        /// <value>The command to accept the proposed OSMM Update for the current incid.</value>
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
        /// OSMM Reject command.
        /// </summary>
        /// <value>The command to reject the proposed OSMM Update for the current incid.</value>
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
        /// Can the proposed OSMM Update for the current incid
        /// be processed?
        /// </summary>
        /// <value><c>true</c> if the proposed OSMM Update for the current incid can be processed; otherwise, <c>false</c>.</value>
        public bool CanOSMMAccept
        {
            get
            {
                // Prevent OSMM updates being actioned too quickly.
                // Check if there are no proposed OSMM Updates
                // for the current filter.
                return (_osmmUpdating == false &&
                    _osmmUpdatesEmpty == false &&
                    _incidOSMMUpdatesStatus != null &&
                    (_incidOSMMUpdatesStatus > 0 || _incidOSMMUpdatesStatus < -1));
            }
        }

        /// <summary>
        /// Indicates whether the OSMM step-through review workflow is active.
        ///
        /// This toggles the HluEditMode.OsmmReview flag while leaving other flags
        /// (Edit, Bulk, OsmmBulk) unchanged.
        /// </summary>
        /// <value><c>true</c> if OSMM Update mode is active; otherwise, <c>false</c>.</value>
        internal bool OSMMUpdateMode
        {
            get
            {
                return WorkMode.HasFlag(WorkMode.OSMMReview);
            }
            set
            {
                // Add or remove the OSMM Review flag as appropriate.
                SetWorkModeFlag(WorkMode.OSMMReview, value);
            }
        }

        /// <summary>
        /// Hide some controls when in OSMM Update mode.
        /// </summary>
        /// <value>The visibility of controls when in OSMM Update mode.</value>
        public Visibility HideInOSMMUpdateMode
        {
            get
            {
                if (IsOsmmReviewMode)
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
            set { }
        }

        /// <summary>
        /// Show some controls when in OSMM Update mode.
        /// </summary>
        /// <value>The visibility of controls in OSMM Update mode.</value>
        public Visibility ShowInOSMMUpdateMode
        {
            get
            {
                // Show the group if in osmm update mode
                if (IsOsmmReviewMode)
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
        /// Whether to create incid history for processing OSMM Updates.
        /// </summary>
        /// <value><c>true</c> to create incid history for processing OSMM Updates; otherwise, <c>false</c>.</value>
        public bool OSMMUpdateCreateHistory
        {
            get { return _osmmUpdateCreateHistory; }
            set { _osmmUpdateCreateHistory = value; }
        }

        /// <summary>
        /// Set the Accept button caption depending on whether the Ctrl button
        /// is held down.
        /// </summary>
        /// <value>The text for the OSMM Accept button.</value>
        public string OSMMAcceptText
        {
            get { return OSMMAcceptTag == "Ctrl" ? "A_ccept All" : "A_ccept"; }
        }

        /// <summary>
        /// Set the Reject button caption depending on whether the Ctrl button
        /// is held down.
        /// </summary>
        /// <value>The text for the OSMM Reject button.</value>
        public string OSMMRejectText
        {
            get { return OSMMRejectTag == "Ctrl" ? "Re_ject All" : "Re_ject"; }
        }

        /// <summary>
        /// Can OSMM Update be accepted?
        /// </summary>
        /// <value><c>true</c> if OSMM Update can be accepted; otherwise, <c>false</c>.</value>
        public bool CanOSMMUpdateAccept
        {
            get
            {
                // If in edit mode,
                // and not in a bulk mode,
                // and a proposed OSMM update is showing,
                // and the OSMM update status for the current incid is greater than 0 (i.e. there are proposed updates for this incid)
                return IsEditMode &&
                    !WorkMode.HasAny(WorkMode.OSMMReview | WorkMode.Bulk) &&
                    (_showOSMMUpdates == "Always" ||
                    _showOSMMUpdates == "When Outstanding") &&
                    IncidOSMMStatus > 0;
            }
        }

        /// <summary>
        /// Can OSMM Update be rejected?
        /// </summary>
        /// <value><c>true</c> if OSMM Update can be rejected; otherwise, <c>false</c>.</value>
        public bool CanOSMMUpdateReject
        {
            get
            {
                // If in edit mode,
                // and not in a bulk mode,
                // and a proposed OSMM update is showing,
                // and the OSMM update status for the current incid is greater than 0 (i.e. there are proposed updates for this incid)
                return IsEditMode &&
                    !WorkMode.HasAny(WorkMode.OSMMReview | WorkMode.Bulk) &&
                    (_showOSMMUpdates == "Always" ||
                    _showOSMMUpdates == "When Outstanding") &&
                    IncidOSMMStatus > 0;
            }
        }

        /// <summary>
        /// Are there any OSMM updates in the database?
        /// </summary>
        /// <value><c>true</c> if there are any OSMM updates in the database; otherwise, <c>false</c>.</value>
        public bool AnyOSMMUpdates
        {
            get
            {
                return _anyOSMMUpdates ?? false;
            }
        }

        /// <summary>
        /// Gets the row counter for the current incid.
        /// </summary>
        /// <value>The row counter for the current incid.</value>
        public int OSMMIncidCurrentRowIndex
        {
            get { return _osmmUpdatesEmpty ? 0 : _incidCurrentRowIndex; }
        }

        #endregion Properties - OSMM Update

        #region Properties - OSMM Bulk Update

        /// <summary>
        /// Can OSMM Bulk Update mode be started?
        /// </summary>
        /// <value><c>true</c> if OSMM Bulk Update mode can be started; otherwise, <c>false</c>.</value>
        public bool CanOSMMBulkUpdateMode
        {
            get
            {
                // Must be in edit mode,
                // there must be proposed OSMM Updates for the current filter,
                // the user must be authorised for bulk updates,
                // and we must not already be in OSMM Review mode,
                return IsEditMode &&
                    AnyOSMMUpdates == true &&
                    CanUserBulkUpdate == true &&
                    !WorkMode.HasAll(WorkMode.OSMMReview) &&
                    (!WorkMode.HasAll(WorkMode.Bulk) || WorkMode.HasAll(WorkMode.OSMMBulk));
            }
        }

        /// <summary>
        /// Can the OSMM Bulk Update be cancelled.
        /// </summary>
        /// <value><c>true</c> if the OSMM Bulk Update can be cancelled; otherwise, <c>false</c>.</value>
        public bool CanCancelOSMMBulkUpdate { get { return IsOsmmBulkMode; } }

        /// <summary>
        /// Gets or sets a value indicating whether the OSMM bulk apply workflow is active.
        ///
        /// This toggles the WorkMode.OSMMBulk flag and, if required by your
        /// rules, also ensures that Bulk mode is active whenever OSMM bulk mode
        /// is enabled.
        /// </summary>
        /// <value><c>true</c> if the OSMM bulk apply workflow is active; otherwise, <c>false</c>.</value>
        internal bool OSMMBulkUpdateMode
        {
            get
            {
                return WorkMode.HasFlag(WorkMode.OSMMBulk);
            }
            set
            {
                // If to be set on add the OSMM Bulk flag and also the bulk flag because OSMM bulk implies bulk.
                if (value)
                {
                    SetWorkModeFlag(WorkMode.OSMMBulk, value);
                    SetWorkModeFlag(WorkMode.Bulk, value);
                    return;
                }

                // Otherwise clear the OSMM Bulk flag (but leave the Bulk flag to be cleared directly).
                SetWorkModeFlag(WorkMode.OSMMBulk, value);
            }
        }

        #endregion Properties - OSMM Bulk Update

        #region Properties - Display Values

        /// <summary>
        /// Gets and sets the value for the last modified date for the current Incid, which is displayed on the form.
        /// </summary>
        /// <value>The last modified date for the current Incid.</value>
        internal DateTime IncidLastModifiedDateVal
        {
            get { return _incidLastModifiedDate; }
            set { _incidLastModifiedDate = value; }
        }

        /// <summary>
        /// Gets and sets the value for the last modified user for the current Incid, which is displayed on the form.
        /// </summary>
        /// <value>The last modified user for the current Incid.</value>
        internal string IncidLastModifiedUserId
        {
            get { return _incidLastModifiedUser; }
            set { if ((IncidCurrentRow != null) && (value != null)) _incidLastModifiedUser = value; }
        }

        #endregion Properties - Display Values

        #region Properties - Copy/Paste

        /// <summary>
        /// Gets or sets the copy switches for the window.
        /// </summary>
        /// <value>The copy switches for the window.</value>
        public WindowMainCopySwitches CopySwitches
        {
            get { return _copySwitches; }
            set { _copySwitches = value; }
        }

        /// <summary>
        /// Can the user copy attribute values from the current Incid row?
        /// </summary>
        /// <value>Indicates whether the user can copy attribute values from the current Incid row.</value>
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
        /// Can the user paste attribute values to the current Incid row?
        /// </summary>
        /// <value>Indicates whether the user can paste attribute values to the current Incid row.</value>
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

        #endregion Properties - Copy/Paste

        #region Properties - About

        /// <summary>
        /// Gets the copyright notice for the assembly to display with the
        /// current userid and name in the 'About' box.
        /// </summary>
        /// <value>The assembly copyright notice.</value>
        public string AssemblyCopyright
        {
            get
            {
                // Get all Copyright attributes on this assembly
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);

                // If there aren't any Copyright attributes, return an empty string
                if (attributes.Length == 0)
                    return null;

                // Split the copyright statement at each full stop, trim spaces and wrap it to a new line.
                String copyright = String.Join(Environment.NewLine, ((AssemblyCopyrightAttribute)attributes[0]).Copyright.Split('.').Select(s => s.Trim()));

                // If there is a Copyright attribute, return its value
                return copyright;
            }
        }

        #endregion Properties - About

        #region Properties - Static Images

        /// <summary>
        /// Gets the image for the AddSecondaryHabitat button.
        /// </summary>
        /// <value>The image source for the AddSecondaryHabitat button.</value>
        public static ImageSource ButtonAddSecondaryHabitatImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Images/AddRowSquare16.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Gets the image for the AddSecondaryHabitatList button.
        /// </summary>
        /// <value>The image source for the AddSecondaryHabitatList button.</value>
        public static ImageSource ButtonAddSecondaryHabitatListImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Images/AddScript16.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Gets the image for the GetMapSelection button.
        /// </summary>
        /// <value>The image source for the GetMapSelection button.</value>
        public static ImageSource ButtonGetMapSelectionImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Images/GetMapSelection16.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Gets the image for the EditPriorityHabitats button.
        /// </summary>
        /// <value>The image source for the EditPriorityHabitats button.</value>
        public static ImageSource ButtonEditPriorityHabitatsImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Images/ZoomTable16.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        /// <summary>
        /// Gets the image for the EditPotentialHabitats button.
        /// </summary>
        /// <value>The image source for the EditPotentialHabitats button.</value>
        public static ImageSource ButtonEditPotentialHabitatsImg
        {
            get
            {
                string imageSource = "pack://application:,,,/HLUTool;component/Images/ZoomTable16.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        #endregion Properties - Static Images

        #endregion Properties

        #region Methods

        #region Message Queue

        /// <summary>
        /// Gets the collection of messages to display.
        /// </summary>
        /// <value>The collection of messages to display.</value>
        public ObservableCollection<MessageItem> MessageQueue
        {
            get
            {
                return _messageQueue;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there are any messages to display.
        /// </summary>
        /// <value><c>true</c> if there are messages to display; otherwise, <c>false</c>.</value>
        public bool HasMessages
        {
            get
            {
                return _messageQueue.Count > 0;
            }
        }

        /// <summary>
        /// Gets the visibility for the message queue area.
        /// </summary>
        /// <value>The visibility for the message queue area.</value>
        public Visibility MessageQueueVisibility
        {
            get
            {
                return HasMessages ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Shows a message in the message queue with optional categorization and auto-dismiss.
        /// If a message with the same text, level, and category already exists, it will be replaced
        /// and its auto-dismiss timer will be refreshed.
        /// </summary>
        /// <param name="msg">The message to display.</param>
        /// <param name="messageLevel">The level of the message (e.g., Info, Warning, Error).</param>
        /// <param name="category">Optional category for the message (e.g., "Navigation", "Validation").</param>
        /// <param name="isDismissible">Whether the user can manually dismiss this message.</param>
        /// <param name="autoDismissSeconds">Auto-dismiss after this many seconds (0 = no auto-dismiss).</param>
        /// <returns>The unique ID of the created or updated message.</returns>
        public string ShowMessage(string msg, MessageType messageLevel, string category = null,
            bool isDismissible = true, int autoDismissSeconds = 0)
        {
            // If the message is null or whitespace, do not add it to the queue
            if (string.IsNullOrWhiteSpace(msg))
                return null;

            string messageId = null;

            // Execute on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Normalize category
                string normalizedCategory = category ?? MessageCategory.General;

                // Check if an identical message already exists (same text, level, and category)
                var existingMessage = _messageQueue.FirstOrDefault(m =>
                    string.Equals(m.Message, msg, StringComparison.Ordinal) &&
                    m.Level == messageLevel &&
                    string.Equals(m.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase));

                if (existingMessage != null)
                {
                    // Cancel the existing auto-dismiss timer
                    CancelAutoDismiss(existingMessage.Id);

                    // Update the existing message's timestamp and auto-dismiss setting
                    existingMessage.Timestamp = DateTime.Now;
                    existingMessage.IsDismissible = isDismissible;
                    existingMessage.AutoDismissSeconds = autoDismissSeconds;

                    // Use the existing message ID
                    messageId = existingMessage.Id;

                    // Re-sort the queue since the timestamp changed
                    SortMessages();
                }
                else
                {
                    // Create a new message item
                    var messageItem = new MessageItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Message = msg,
                        Level = messageLevel,
                        Timestamp = DateTime.Now,
                        Category = normalizedCategory,
                        IsDismissible = isDismissible,
                        AutoDismissSeconds = autoDismissSeconds
                    };

                    messageId = messageItem.Id;

                    // Add to queue
                    _messageQueue.Add(messageItem);
                    SortMessages();
                }

                OnPropertyChanged(nameof(HasMessages));
                OnPropertyChanged(nameof(MessageQueueVisibility));
            });

            // Set up auto-dismiss if requested
            if (autoDismissSeconds > 0)
            {
                SetupAutoDismiss(messageId, autoDismissSeconds);
            }

            return messageId;
        }

        /// <summary>
        /// Clears messages from the queue based on ID or category.
        /// </summary>
        /// <param name="id">The unique ID of a specific message to clear.</param>
        /// <param name="category">The category of messages to clear.</param>
        /// <param name="level">The level of messages to clear.</param>
        public void ClearMessage(string id = null, string category = null, MessageType? level = null)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                List<MessageItem> messagesToRemove = [];

                // Clear by ID
                if (!string.IsNullOrEmpty(id))
                {
                    var message = _messageQueue.FirstOrDefault(m => m.Id == id);
                    if (message != null)
                    {
                        messagesToRemove.Add(message);
                        CancelAutoDismiss(id);
                    }
                }
                // Clear by category
                else if (!string.IsNullOrEmpty(category))
                {
                    messagesToRemove = [.. _messageQueue.Where(m => string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase))];
                }
                // Clear by level
                else if (level.HasValue)
                {
                    messagesToRemove = [.. _messageQueue.Where(m => m.Level == level.Value)];
                }
                // Clear all
                else
                {
                    messagesToRemove = [.. _messageQueue];
                }

                // Remove the messages
                foreach (var msg in messagesToRemove)
                {
                    _messageQueue.Remove(msg);
                    CancelAutoDismiss(msg.Id);
                }

                OnPropertyChanged(nameof(HasMessages));
                OnPropertyChanged(nameof(MessageQueueVisibility));
            });
        }

        /// <summary>
        /// Clears all messages from the queue.
        /// </summary>
        public void ClearAllMessages()
        {
            ClearMessage();
        }

        /// <summary>
        /// Sorts messages in required order.
        /// </summary>
        private void SortMessages()
        {
            var now = DateTime.Now;

            var sorted = _messageQueue
                // First: dismissible messages ordered by expiration time (soonest to expire first)
                .OrderBy(m =>
                {
                    if (m.AutoDismissSeconds > 0)
                    {
                        // Calculate actual expiration time
                        return m.Timestamp.AddSeconds(m.AutoDismissSeconds);
                    }
                    else if (m.IsDismissible)
                    {
                        // Manually dismissible but not auto-dismiss: show after auto-dismiss messages
                        return DateTime.MaxValue.AddDays(-1);
                    }
                    else
                    {
                        // Non-dismissible: show last
                        return DateTime.MaxValue;
                    }
                })
                // Then by priority (Error > Warning > Information)
                .ThenByDescending(m => m.Priority)
                // Then by timestamp (newest first)
                .ThenByDescending(m => m.Timestamp)
                .ToList();

            _messageQueue.Clear();
            foreach (var item in sorted)
            {
                _messageQueue.Add(item);
            }
        }

        /// <summary>
        /// Sets up auto-dismiss for a message after the specified number of seconds.
        /// </summary>
        /// <param name="messageId">The ID of the message to auto-dismiss.</param>
        /// <param name="seconds">Number of seconds before auto-dismiss.</param>
        private void SetupAutoDismiss(string messageId, int seconds)
        {
            // Cancel any existing timer for this message
            CancelAutoDismiss(messageId);

            // Create a new timer
            var timer = new System.Timers.Timer(seconds * 1000)
            {
                AutoReset = false
            };
            timer.Elapsed += (sender, e) =>
            {
                ClearMessage(id: messageId);
                CancelAutoDismiss(messageId);
            };

            _messageTimers[messageId] = timer;
            timer.Start();
        }

        /// <summary>
        /// Cancels the auto-dismiss timer for a message.
        /// </summary>
        /// <param name="messageId">The ID of the message.</param>
        private void CancelAutoDismiss(string messageId)
        {
            if (_messageTimers.TryGetValue(messageId, out var timer))
            {
                timer.Stop();
                timer.Dispose();
                _messageTimers.Remove(messageId);
            }
        }

        /// <summary>
        /// Gets the count of messages by category.
        /// </summary>
        /// <param name="category">The category to count.</param>
        /// <returns>The number of messages in that category.</returns>
        public int GetMessageCountByCategory(string category)
        {
            return _messageQueue.Count(m => string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the count of messages by level.
        /// </summary>
        /// <param name="level">The level to count.</param>
        /// <returns>The number of messages at that level.</returns>
        public int GetMessageCountByLevel(MessageType level)
        {
            return _messageQueue.Count(m => m.Level == level);
        }

        /// <summary>
        /// Checks if there are any error messages in the queue.
        /// </summary>
        /// <value><c>true</c> if there are error messages in the queue; otherwise, <c>false</c>.</value>
        public bool HasErrors
        {
            get
            {
                return _messageQueue.Any(m => m.Level == MessageType.Error);
            }
        }

        /// <summary>
        /// Checks if there are any warning messages in the queue.
        /// </summary>
        /// <value><c>true</c> if there are warning messages in the queue; otherwise, <c>false</c>.</value>
        public bool HasWarnings
        {
            get
            {
                return _messageQueue.Any(m => m.Level == MessageType.Warning);
            }
        }

        /// <summary>
        /// Shows an error message. Error messages do not auto-dismiss by default.
        /// </summary>
        public string ShowError(string message, string category = null, bool isDismissible = true)
        {
            return ShowMessage(message, MessageType.Error, category ?? MessageCategory.General,
                isDismissible, Settings.Default.MessageAutoDismissError);
        }

        /// <summary>
        /// Shows a warning message with auto-dismiss based on user preference.
        /// </summary>
        public string ShowWarning(string message, string category = null, bool isDismissible = true)
        {
            return ShowMessage(message, MessageType.Warning, category ?? MessageCategory.General,
                isDismissible, Settings.Default.MessageAutoDismissWarning);
        }

        /// <summary>
        /// Shows an info message with auto-dismiss based on user preference.
        /// </summary>
        public string ShowInfo(string message, string category = null, bool isDismissible = true)
        {
            return ShowMessage(message, MessageType.Information, category ?? MessageCategory.General,
                isDismissible, Settings.Default.MessageAutoDismissInfo);
        }

        /// <summary>
        /// Shows a success message with auto-dismiss based on user preference.
        /// </summary>
        public string ShowSuccess(string message, string category = null, bool isDismissible = true)
        {
            return ShowMessage(message, MessageType.Information, category ?? MessageCategory.General,
                isDismissible, Settings.Default.MessageAutoDismissSuccess);
        }

        #endregion Message Queue

        #region Message Dismss

        private ICommand _dismissMessageCommand;

        /// <summary>
        /// Command to dismiss a message by ID.
        /// </summary>
        public ICommand DismissMessageCommand
        {
            get
            {
                _dismissMessageCommand ??= new RelayCommand(
                        param => ClearMessage(id: param?.ToString()),
                        param => true);
                return _dismissMessageCommand;
            }
        }

        #endregion Message Dismss

        #region Help

        /// <summary>
        /// Override the default behavior when the dockpane's help icon is clicked
        /// or the F1 key is pressed.
        /// </summary>
        protected override void OnHelpRequested()
        {
            if (_helpURL != null)
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = _helpURL,
                    UseShellExecute = true
                });
            }
        }

        #endregion Help

        #region Ribbon Controls

        /// <summary>
        /// Switch the active layer.
        /// </summary>
        /// <param name="selectedValue">The name of the layer to switch to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SwitchGISLayerAsync(string selectedValue)
        {
            // Check if the layer name has actually changed.
            if (selectedValue != ActiveLayerName)
            {
                // Create a new GIS functions instance (so that it will use the new active layer to set any cached variables).
                _gisApp = new();

                // Recomputes whether editing is currently possible.
                RefreshEditCapability();

                // Get the new active map view (if there is one).
                if (MapView.Active is null || MapView.Active.Map.Name != _gisApp.MapName)
                    _activeMapView = _gisApp.GetActiveMapView();

                // Switch the GIS layer.
                if (await _gisApp.IsHluLayerAsync(selectedValue, true))
                {
                    // Set the active HLU layer name.
                    ActiveLayerName = selectedValue;

                    // Refresh the layer name
                    OnPropertyChanged(nameof(ActiveLayerName));

                    // Get the GIS layer selection and warn the user if no
                    // features are found
                    await GetMapSelectionAsync(true);
                }
            }
        }

        /// <summary>
        /// Activate or Deactivate the specified state. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID">The ID of the state to toggle.</param>
        /// <param name="activate">A boolean value indicating whether to activate or deactivate the state.</param>
        public static void ToggleState(string stateID, bool activate)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                if (!activate)
                    FrameworkApplication.State.Deactivate(stateID);
            }
            else
            {
                if (activate)
                    FrameworkApplication.State.Activate(stateID);
            }
        }

        #endregion Ribbon Controls

        #region UI State

        /// <summary>
        /// Changes the current cursor and updates the processing message displayed to the user.
        /// </summary>
        /// <param name="cursorType">The cursor to display during the processing operation. Typically set to indicate the application's current
        /// state, such as a wait or arrow cursor.</param>
        /// <param name="processingMessage">The message to display to the user while processing is in progress. This message provides feedback about the
        /// current operation.</param>
        public void ChangeCursor(Cursor cursorType, string processingMessage)
        {
            //TODO: ChangeCursor
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

        //TODO: Check if still correct approach with dispatcher
        /// <summary>
        /// Update the progress bar.
        /// </summary>
        /// <param name="processText">The text to display for the current process.</param>
        /// <param name="progressValue">The current progress value.</param>
        /// <param name="maxProgressValue">The maximum progress value.</param>
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
            IncidIhsMatrixRows = [.. Array.Empty<HluDataSet.incid_ihs_matrixRow>()
                .Select(r => HluDataset.incid_ihs_matrix.Newincid_ihs_matrixRow())];

            IncidIhsFormationRows = [.. Array.Empty<HluDataSet.incid_ihs_formationRow>()
                .Select(r => HluDataset.incid_ihs_formation.Newincid_ihs_formationRow())];

            IncidIhsManagementRows = [.. Array.Empty<HluDataSet.incid_ihs_managementRow>()
                .Select(r => HluDataset.incid_ihs_management.Newincid_ihs_managementRow())];

            IncidIhsComplexRows = [.. Array.Empty<HluDataSet.incid_ihs_complexRow>()
                .Select(r => HluDataset.incid_ihs_complex.Newincid_ihs_complexRow())];

            // Get new secondary rows.
            IncidSecondaryRows = [.. Array.Empty<HluDataSet.incid_secondaryRow>()
                .Select(r => HluDataset.incid_secondary.Newincid_secondaryRow())];

            // Clear the secondary habitats table and the secondary habitat
            // rows for the class
            _incidSecondaryHabitats = [];
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Clear the secondary groups list and disable the drop-down.
            _secondaryGroupsValid = null;
            OnPropertyChanged(nameof(SecondaryGroupCodesValid));
            OnPropertyChanged(nameof(SecondaryGroupEnabled));

            // Get a new condition row.
            IncidConditionRows = [.. new HluDataSet.incid_conditionRow[1]
                .Select(r => HluDataset.incid_condition.Newincid_conditionRow())];

            for (int i = 0; i < IncidConditionRows.Length; i++)
            {
                IncidConditionRows[i].incid_condition_id = i;
                IncidConditionRows[i].condition = null;
                IncidConditionRows[i].incid = RecIDs.CurrentIncid;
            }

            // Get a new BAP row and reset the auto and user collections.
            IncidBapRows = [.. Array.Empty<HluDataSet.incid_bapRow>()
                .Select(r => HluDataset.incid_bap.Newincid_bapRow())];
            IncidBapRowsAuto = [];
            IncidBapRowsUser = [];

            // Get new sources rows.
            IncidSourcesRows = [.. new HluDataSet.incid_sourcesRow[3]
                .Select(r => HluDataset.incid_sources.Newincid_sourcesRow())];
            for (int i = 0; i < IncidSourcesRows.Length; i++)
            {
                IncidSourcesRows[i].incid_source_id = i;
                IncidSourcesRows[i].source_id = Int32.MinValue;
                IncidSourcesRows[i].incid = RecIDs.CurrentIncid;
            }

            // Clear the OSMM Update fields
            _incidOSMMUpdatesOSMMXref = 0;
            _incidOSMMUpdatesProcessFlag = 0;
            _incidOSMMUpdatesSpatialFlag = null;
            _incidOSMMUpdatesChangeFlag = null;
            _incidOSMMUpdatesStatus = null;
        }

        #endregion UI State

        #region UI State Management

        /// <summary>
        /// Checks and updates the state of editing-related controls by raising property change notifications.
        /// </summary>
        /// <remarks>Call this method to ensure that UI elements bound to the ReasonProcessEnabled and
        /// TabControlDataEnabled properties reflect the current state. This is typically used after changes that may
        /// affect whether editing controls should be enabled or disabled.</remarks>
        private void CheckEditingControlState()
        {
            OnPropertyChanged(nameof(ReasonProcessEnabled));
            OnPropertyChanged(nameof(TabControlDataEnabled));
        }

        /// <summary>
        /// Enables or disables the editing controls based on the specified value.
        /// </summary>
        /// <param name="enable">true to enable the editing controls; false to disable them.</param>
        public void ChangeEditingControlState(bool enable)
        {
            _reasonProcessEnabled = enable;
            _tabControlDataEnabled = enable;

            // Update the editing control state
            CheckEditingControlState();
        }

        #endregion UI State Management

        #region UI Refresh Methods

        /// <summary>
        /// Refreshes the state of all UI controls and related properties to reflect the current application data and
        /// context.
        /// </summary>
        /// <remarks>Call this method to ensure that all toolbar buttons, menu commands, and tab controls
        /// are updated to match the latest state. This is typically used after changes to the underlying data or
        /// selection to keep the user interface in sync.</remarks>
        internal void RefreshAll()
        {
            // Refresh the tab control enabled state.
            OnPropertyChanged(nameof(TabControlDataEnabled));

            // Update tab control states.
            OnPropertyChanged(nameof(TabItemSelected));

            // Update toolbar and menu command states.
            OnPropertyChanged(nameof(CanCopy));
            OnPropertyChanged(nameof(CanPaste));

            // Update all other controls.
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
        }
        /// <summary>
        /// Refreshes the state of combo box sources, ensuring that any bound user interface elements reflect the current data.
        /// </summary>
        /// <remarks>Call this method after changes that may affect the available options in combo boxes. This method raises
        /// property change notifications for multiple properties, which can trigger UI updates in data-bound scenarios.</remarks>
        private void RefreshComboBoxSources()
        {
            OnPropertyChanged(nameof(BoundaryMapCodes));
            OnPropertyChanged(nameof(ConditionCodes));
            OnPropertyChanged(nameof(ConditionQualifierCodes));
            OnPropertyChanged(nameof(HabitatClassCodes));
            OnPropertyChanged(nameof(SourceHabitatClassCodes));
            OnPropertyChanged(nameof(HabitatTypeCodes));
            OnPropertyChanged(nameof(BapHabitatCodes));
            OnPropertyChanged(nameof(SourceImportanceCodes));
            OnPropertyChanged(nameof(LegacyHabitatCodes));
        }

        /// <summary>
        /// Refreshes the state of properties related to bulk update controls, ensuring that any bound user interface
        /// elements reflect the current data and mode.
        /// </summary>
        /// <remarks>Call this method after changes that may affect the visibility, headers, or enabled
        /// state of bulk update controls. This method raises property change notifications for multiple properties,
        /// which can trigger UI updates in data-bound scenarios.</remarks>
        private void RefreshBulkUpdateControls()
        {
            OnPropertyChanged(nameof(ShowInBulkUpdateMode));
            OnPropertyChanged(nameof(HideInBulkUpdateMode));

            OnPropertyChanged(nameof(TopControlsGroupHeader));
            OnPropertyChanged(nameof(TabItemHistoryEnabled));

            OnPropertyChanged(nameof(SelectedIncidsInDBCount));
            OnPropertyChanged(nameof(SelectedFragsInDBCount));

            OnPropertyChanged(nameof(SelectedIncidsInGISCount));
            OnPropertyChanged(nameof(SelectedToidsInGISCount));
            OnPropertyChanged(nameof(SelectedFragsInGISCount));

            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));
        }

        /// <summary>
        /// Notifies the UI that properties related to OSMM update controls have changed.
        /// </summary>
        /// <remarks>Call this method to refresh the state of UI elements that depend on OSMM update mode
        /// properties. This ensures that any bindings to these properties are updated to reflect the current
        /// state.</remarks>
        private void RefreshOSMMUpdateControls()
        {
            OnPropertyChanged(nameof(ShowInOSMMUpdateMode));
            OnPropertyChanged(nameof(HideInOSMMUpdateMode));

            OnPropertyChanged(nameof(TopControlsGroupHeader));
        }

        /// <summary>
        /// Refreshes the status-related properties and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to update all dependent status properties and ensure that any data
        /// bindings or observers are notified of the latest values. This is typically used after changes that may
        /// affect the state or availability of UI elements bound to these properties.</remarks>
        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(IncidCurrentRowIndex));
            OnPropertyChanged(nameof(OSMMIncidCurrentRowIndex));
            OnPropertyChanged(nameof(StatusIncid));
            OnPropertyChanged(nameof(StatusBar));
            OnPropertyChanged(nameof(ActiveLayerName));
            OnPropertyChanged(nameof(CanZoomToSelection));
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(CanOSMMAccept));
            OnPropertyChanged(nameof(CanOSMMSkip));
            OnPropertyChanged(nameof(CanBulkUpdate));
            OnPropertyChanged(nameof(CanOSMMUpdateMode));
            OnPropertyChanged(nameof(CanOSMMBulkUpdateMode));
            OnPropertyChanged(nameof(CanOSMMUpdateAccept));
            OnPropertyChanged(nameof(CanOSMMUpdateReject));
            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(CanFilterByAttributes));
            OnPropertyChanged(nameof(CanClearFilter));
            OnPropertyChanged(nameof(CanExport));

            // Update the editing control state
            CheckEditingControlState();

            // Update split/merge enablement
            RefreshSplitMergeEnablement();
        }

        /// <summary>
        /// Raises property change notifications for reason and process-related properties to update
        /// data bindings.
        /// </summary>
        /// <remarks>
        /// Call this method to ensure that UI elements or other listeners are notified when reason
        /// or process properties have changed. This is typically used in data-binding scenarios to
        /// refresh displayed values after underlying data is modified.
        /// </remarks>
        public void RefreshReasonProcess()
        {
            OnPropertyChanged(nameof(Reason));
            OnPropertyChanged(nameof(Process));

            // Refresh both the cached split and merge enablement values.
            RefreshSplitMergeEnablement();

            // Check for errors in the toolbar combo boxes
            var reasonComboBox = ReasonComboBox.GetInstance();
            var processComboBox = ProcessComboBox.GetInstance();

            // Show the error if either is found
            if (reasonComboBox?.HasError == true || processComboBox?.HasError == true)
            {
                string errorMsg = "";
                if (reasonComboBox?.HasError == true)
                    errorMsg += reasonComboBox.ErrorMessage;
                if (processComboBox?.HasError == true)
                {
                    if (!string.IsNullOrEmpty(errorMsg))
                        errorMsg += "; ";
                    errorMsg += processComboBox.ErrorMessage;
                }

                ShowError(errorMsg, MessageCategory.GIS);
            }
            else
            {
                ClearMessage();
            }
        }

        /// <summary>
        /// Raises property change notifications for header-related properties to update data bindings.
        /// </summary>
        /// <remarks>
        /// Call this method to ensure that UI elements or other listeners are notified when header
        /// properties have changed. This is typically used in data-binding scenarios to refresh
        /// displayed values after underlying data is modified.
        /// </remarks>
        private void RefreshHeader()
        {
            OnPropertyChanged(nameof(Incid));
            OnPropertyChanged(nameof(IncidArea));
            OnPropertyChanged(nameof(IncidLength));
            OnPropertyChanged(nameof(IncidCreatedDate));
            OnPropertyChanged(nameof(IncidLastModifiedDate));
            OnPropertyChanged(nameof(IncidCreatedUser));
            OnPropertyChanged(nameof(IncidLastModifiedUser));
        }

        /// <summary>
        /// Raises property change notifications for all group header properties to ensure that their values are updated
        /// in the user interface.
        /// </summary>
        /// <remarks>Call this method after making changes that affect any of the group header properties
        /// to refresh their displayed values. This method is typically used to synchronize the UI with the current
        /// state of the underlying data.</remarks>
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

        /// <summary>
        /// Notifies listeners that OSMM-related properties have changed and updates their bindings.
        /// </summary>
        /// <remarks>Call this method to refresh the UI or other observers when OSMM update-related
        /// properties may have changed. This ensures that any data bindings or listeners are notified of the latest
        /// property values.</remarks>
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

        /// <summary>
        /// Refreshes all properties related to the habitat tab, notifying listeners of any changes.
        /// </summary>
        /// <remarks>Call this method to update data bindings and UI elements that depend on
        /// habitat-related properties. This is typically used after changes to underlying habitat data to ensure the
        /// user interface reflects the current state.</remarks>
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
            //OnPropertyChanged(nameof(HabitatSecondariesMandatory)); //TODO: Needed twice?
            OnPropertyChanged(nameof(IncidSecondarySummary));
            OnPropertyChanged(nameof(LegacyHabitatCodes));
            OnPropertyChanged(nameof(IncidLegacyHabitat));
        }

        /// <summary>
        /// Refreshes the state and displayed values of the IHS tab, ensuring that all related properties and controls
        /// reflect the current data.
        /// </summary>
        /// <remarks>Call this method after making changes that affect the IHS tab's data or enabled state
        /// to update the UI accordingly. This method triggers property change notifications for relevant properties and
        /// updates multiplexed values as needed.</remarks>
        private void RefreshIHSTab()
        {
            OnPropertyChanged(nameof(ShowIHSTab));
            OnPropertyChanged(nameof(TabItemIHSEnabled));
            OnPropertyChanged(nameof(TabIhsControlsEnabled));
            OnPropertyChanged(nameof(IHSTabLabel));
            OnPropertyChanged(nameof(IncidIhsHabitat));
            RefreshIhsMultiplexValues();
        }

        /// <summary>
        /// Refreshes all IHS multiplex-related property values and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all dependent IHS multiplex properties are updated
        /// and that any data bindings or observers are notified of their new values. This is typically used after
        /// changes to underlying data that affect these properties.</remarks>
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

        /// <summary>
        /// Refreshes the state of properties related to the priority tab, ensuring that any bound UI elements are
        /// updated to reflect the current values.
        /// </summary>
        /// <remarks>Call this method after making changes that affect the priority tab's properties to
        /// ensure the user interface remains in sync with the underlying data. This method raises property change
        /// notifications for all relevant properties associated with the priority tab.</remarks>
        private void RefreshPriorityTab()
        {
            OnPropertyChanged(nameof(TabItemPriorityEnabled));
            OnPropertyChanged(nameof(TabPriorityControlsEnabled));
            OnPropertyChanged(nameof(PriorityTabLabel));
            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
            OnPropertyChanged(nameof(IncidBapHabitatsUser));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));
            OnPropertyChanged(nameof(CanEditPotentialHabitats));
            OnPropertyChanged(nameof(CanEditPriorityHabitats));
        }

        /// <summary>
        /// Refreshes the state of all properties related to the details tab, notifying listeners of any changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all UI elements and data bindings associated with the
        /// details tab are updated to reflect the current state of the underlying data. This is typically used after
        /// changes that may affect multiple properties displayed in the details tab.</remarks>
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

        /// <summary>
        /// Refreshes the state of all source-related properties and controls.
        /// </summary>
        /// <remarks>Call this method to update the UI and internal state after changes to the underlying
        /// sources. This method raises property change notifications for related properties and refreshes each source
        /// individually. Intended for internal use within the class to ensure consistency between the sources and their
        /// associated UI elements.</remarks>
        private void RefreshSources()
        {
            OnPropertyChanged(nameof(TabItemSourcesEnabled));
            OnPropertyChanged(nameof(TabSourcesControlsEnabled));
            OnPropertyChanged(nameof(SourcesTabLabel));
            RefreshSource1();
            RefreshSource2();
            RefreshSource3();
        }

        /// <summary>
        /// Refreshes all properties related to Source 1 and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all dependent Source 1 properties are updated.</remarks>
        private void RefreshSource1()
        {
            OnPropertyChanged(nameof(IncidSource1Id));
            OnPropertyChanged(nameof(Source1Names));
            OnPropertyChanged(nameof(IncidSource1Date));
            OnPropertyChanged(nameof(IncidSource1HabitatClass));
            OnPropertyChanged(nameof(IncidSource1HabitatType));
            OnPropertyChanged(nameof(Source1HabitatTypeCodes));
            OnPropertyChanged(nameof(IncidSource1HabitatType));
            OnPropertyChanged(nameof(IncidSource1BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource1HabitatImportance));
            OnPropertyChanged(nameof(IncidSource1Enabled));
        }

        /// <summary>
        /// Refreshes all properties related to Source 2 and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all dependent Source 2 properties are updated.</remarks>
        private void RefreshSource2()
        {
            OnPropertyChanged(nameof(IncidSource2Id));
            OnPropertyChanged(nameof(Source2Names));
            OnPropertyChanged(nameof(IncidSource2Date));
            OnPropertyChanged(nameof(IncidSource2HabitatClass));
            OnPropertyChanged(nameof(IncidSource2HabitatType));
            OnPropertyChanged(nameof(Source2HabitatTypeCodes));
            OnPropertyChanged(nameof(IncidSource2HabitatType));
            OnPropertyChanged(nameof(IncidSource2BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource2HabitatImportance));
            OnPropertyChanged(nameof(IncidSource2Enabled));
        }

        /// <summary>
        /// Refreshes all properties related to Source 3 and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all dependent Source 3 properties are updated.</remarks>
        private void RefreshSource3()
        {
            OnPropertyChanged(nameof(IncidSource3Id));
            OnPropertyChanged(nameof(Source3Names));
            OnPropertyChanged(nameof(IncidSource3Date));
            OnPropertyChanged(nameof(IncidSource3HabitatClass));
            OnPropertyChanged(nameof(IncidSource3HabitatType));
            OnPropertyChanged(nameof(Source3HabitatTypeCodes));
            OnPropertyChanged(nameof(IncidSource3HabitatType));
            OnPropertyChanged(nameof(IncidSource3BoundaryImportance));
            OnPropertyChanged(nameof(IncidSource3HabitatImportance));
            OnPropertyChanged(nameof(IncidSource3Enabled));
        }

        /// <summary>
        /// Refreshes all properties related to the history tab and notifies listeners of property changes.
        /// </summary>
        /// <remarks>Call this method to ensure that all UI elements and data bindings associated with the
        /// history tab are updated to reflect the current state of the underlying data.</remarks>
        private void RefreshHistory()
        {
            OnPropertyChanged(nameof(TabItemHistoryEnabled));
            OnPropertyChanged(nameof(IncidHistory));
        }

        #endregion UI Refresh Methods

        #region Navigation Handlers

        /// <summary>
        /// NavigateFirstCommand event handler. Moves to the first record.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void NavigateFirstClicked(object param)
        {
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // Move to first record.
                await NavigateToRecordAsync(1);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Navigation);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// NavigatePreviousCommand event handler. Moves to the previous record.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void NavigatePreviousClicked(object param)
        {
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // Move to previous record.
                await NavigateToRecordAsync(_incidCurrentRowIndex - 1);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Navigation);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// NavigateNextCommand event handler. Moves to the next record.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void NavigateNextClicked(object param)
        {
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // Move to next record.
                await NavigateToRecordAsync(_incidCurrentRowIndex + 1);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Navigation);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// NavigateLastCommand event handler. Moves to the last record, which is either the last
        /// record in the current filter (if a filter is active) or the last record in the
        /// dataset (if no filter is active).
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void NavigateLastClicked(object param)
        {
            if (_isNavigating)
                return;

            try
            {
                _isNavigating = true;

                // Move to last record.
                await NavigateToRecordAsync(IsFiltered ? _incidSelection.Rows.Count : _incidRowCount);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Navigation);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Moves to the specified record index, updating the current record and synchronizing the GIS selection as needed.
        /// </summary>
        /// <param name="value">The index of the record to navigate to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
        /// NavigateIncidCommand event handler.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void NavigateIncidClicked(object param)
        {
            if (param is string newText && Int32.TryParse(newText, out int value))
            {
                // Move to the required incid current row.
                await NavigateToRecordAsync(value);
            }
        }

        #endregion Navigation Handlers

        #region Update Handler

        /// <summary>
        /// UpdateCommand event handler.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void UpdateClicked(object param)
        {
            // Update the attributes (don't wait).
            await UpdateAsync();
        }

        /// <summary>
        /// UpdateCommand event handler.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task UpdateAsync()
        {
            // Check if the GIS and database are in sync.
            if (!CheckInSync("Save", "Cannot save: Map"))
                return;

            // If there are no features selected in the GIS (because there is no
            // active filter).
            if (_selectedIncidsInGISCount <= 0)
            {
                // Ask the user before re-selecting the current incid features in GIS.
                if (MessageBox.Show("There are no features selected in the GIS.\n" +
                            "Would you like to apply the changes to all features for this incid?", "HLU: Save",
                            MessageBoxButton.YesNoCancel, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Set the status to processing and the cursor to wait.
                    ChangeCursor(Cursors.Wait, "Selecting in GIS ...");

                    // Select all features for current incid
                    await SelectOnMapAsync(false);

                    // If there are still no features selected in the GIS this suggests
                    // that the feature layer contains only a subset of the database
                    // features so this incid cannot be updated.
                    if (_selectedIncidsInGISCount <= 0)
                    {
                        // Reset the cursor back to normal
                        ChangeCursor(Cursors.Arrow, null);

                        return;
                    }

                    // Count the number of toids and fragments for the current incid
                    // selected in the GIS and in the database.
                    CountCurrentIncidToidFrags();

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
            if (IsBulkMode)
            {
                ApplyBulkUpdate();
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
            if ((!IsFiltered) || (_currentIncidFragsInGISCount == _currentIncidFragsInDBCount))
            {
                // If saving hasn't already been attempted, when the features for
                // the current incid were selected in the map (above), then
                // do the update now.
                if (!_savingAttempted)
                {
                    // Update the current incid.
                    _saving = true;
                    _savingAttempted = false;

                    //TODO: Catch exceptions?
                    await _viewModelUpd.PerformUpdateAsync();
                }
                return;
            }

            ChangeCursor(Cursors.Wait, "Filtering ...");

            // Initialise the GIS selection table.
            _gisSelection = NewGisSelectionTable();

            // Recheck the selected features in GIS to make sure they
            // all belong to the current incid (passing a new GIS
            // selection table so that it knows the columns to return.
            try
            {
                _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);
            }
            catch (HLUToolException ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Update);

                return;
            }

            // Count the number of toids and fragments for the current incid
            // selected in the GIS and in the database.
            CountCurrentIncidToidFrags();

            // Refresh all the status type fields.
            RefreshStatus();

            ChangeCursor(Cursors.Arrow, null);

            // If there are no features for the current incid
            // selected in GIS then cancel the update.
            if (_currentIncidFragsInGISCount < 1)
            {
                ShowWarning("No map features for the current incid are selected in the active layer.", MessageCategory.GIS);
                return;
            }

            // If all of the features for the current incid have been
            // selected in GIS then update them all.
            if (_currentIncidFragsInGISCount == _currentIncidFragsInDBCount)
            {
                _saving = true;
                _savingAttempted = false;

                //TODO: Catch exceptions?
                await _viewModelUpd.PerformUpdateAsync();
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

                        // Create ViewModel for split class.
                        ViewModelWindowMainSplit vmSplit = new(this);

                        // Logically split the features for the current incid into a new incid.
                        _splitting = true;
                        if (!await vmSplit.LogicalSplitAsync())
                        {
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

                    //TODO: Catch exceptions?
                    await _viewModelUpd.PerformUpdateAsync();

                    // Recount the number of toids and fragments for the current incid
                    // selected in the GIS and in the database.
                    CountCurrentIncidToidFrags();

                    // Refresh all the status type fields.
                    RefreshStatus();

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map");
                }
                else
                {
                    // Display a warning message.
                    ShowWarning("Cannot Save: The changes have not been applied - the update was cancelled.",
                        MessageCategory.Save);

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
        /// Recomputes whether editing is currently possible and synchronises the
        /// <see cref="WorkMode.CanEdit"/> flag accordingly.
        /// </summary>
        private void RefreshEditCapability()
        {
            //TODO: Update IsEditable to consider whether the layer is currently in a state that allows edits?
            bool canEdit =
                _gisApp != null &&
                _gisApp.ActiveHluLayer != null &&
                IsAuthorisedUser &&
                _gisApp.ActiveHluLayer.IsEditable &&
                Project.Current.IsEditingEnabled;

            // Update the WorkMode.CanEdit flag only if it changes.
            bool oldCanEdit = WorkMode.HasAny(WorkMode.CanEdit);

            // If the edit capability has changed then update the WorkMode.CanEdit flag and raise
            if (oldCanEdit != canEdit)
            {
                // Set the WorkMode.CanEdit flag to the new value.
                SetWorkModeFlag(WorkMode.CanEdit, canEdit);

                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanBulkUpdate));
                OnPropertyChanged(nameof(CanOSMMUpdateMode));
                OnPropertyChanged(nameof(CanOSMMBulkUpdateMode));
                OnPropertyChanged(nameof(CanOSMMUpdateAccept));
                OnPropertyChanged(nameof(CanOSMMUpdateReject));
            }
        }

        /// <summary>
        /// Refreshes the edit capability state for the active HLU layer.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task RefreshIsLayerEditableAsync()
        {
            // Check if the GIS application and active HLU layer are available before trying to access them.
            if (_gisApp == null || _gisApp.ActiveHluLayer == null)
                return;

            // Determine whether the active HLU layer is editable for the current user.
            bool isEditable = await _gisApp.IsLayerEditableAsync(_gisApp.HluLayer);

            // Update the active HLU layer editability state in the GIS application.
            _gisApp.ActiveHluLayer.IsEditable = isEditable;

            // Update the dock pane caption (to show 'Read-only' or not).
            UpdateDockPaneCaption();
        }

        /// <summary>
        /// Check if the user still wants to go ahead because only a subset
        /// of all the features in an incid have been selected. Also checks
        /// if the user wants to logically split the subset of features first
        /// or updates all the incid features.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the update is to go ahead, or <c>false</c> if it is cancelled.
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

                // Create window to show message
                _windowWarnSubsetUpdate = new WindowWarnOnSubsetUpdate
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinWarnSubsetUpdate = new ViewModelWindowWarnOnSubsetUpdate(
                    _currentIncidFragsInGISCount, _currentIncidFragsInDBCount, _gisLayerType);

                // when ViewModel asks to be closed, close window
                _viewModelWinWarnSubsetUpdate.RequestClose -= ViewModelWinWarnSubsetUpdate_RequestClose; // Safety: avoid double subscription.
                _viewModelWinWarnSubsetUpdate.RequestClose +=
                    new ViewModelWindowWarnOnSubsetUpdate.RequestCloseEventHandler(ViewModelWinWarnSubsetUpdate_RequestClose);

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
        private void ViewModelWinWarnSubsetUpdate_RequestClose(bool proceed, bool split, int? subsetUpdateAction)
        {
            // Remove the event handler and close the window.
            _viewModelWinWarnSubsetUpdate.RequestClose -= ViewModelWinWarnSubsetUpdate_RequestClose;
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

        #endregion Update Handler

        #region Bulk Update Handler

        /// <summary>
        /// Start the bulk update mode.
        /// </summary>
        public void StartBulkUpdate()
        {
            _saving = false;
            _viewModelBulkUpdate ??= new ViewModelWindowMainBulkUpdate(this, _addInSettings);

            //TODO: Needed?
            //// If already in bulk update mode then perform the bulk update
            //// (only possible when this method was called after the 'Apply'
            //// button was clicked.
            //if (IsBulkMode)
            //{
            //    _viewModelBulkUpdate.ShowBulkUpdateWindow();
            //}
            //else

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

            // Start the standard bulk update process.
            _viewModelBulkUpdate.StartStandardBulkUpdate();
        }

        /// <summary>
        /// Action the bulk update.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void ApplyBulkUpdate()
        {
            _saving = false;
            _viewModelBulkUpdate ??= new ViewModelWindowMainBulkUpdate(this, _addInSettings);

            // If already in bulk update mode then perform the bulk update
            if (IsBulkMode)
                _viewModelBulkUpdate.ShowBulkUpdateWindow();
        }

        /// <summary>
        /// Cancel the bulk update.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void CancelBulkUpdateClicked(object param)
        {
            if (_viewModelBulkUpdate != null)
            {
                // If the Cancel button has been clicked then we need
                // to work out which mode was active and cancel the
                // right one
                if (IsOsmmBulkMode)
                    await _viewModelBulkUpdate.CancelOSMMBulkUpdateAsync();
                else
                    // Cancels the bulk update mode
                    await _viewModelBulkUpdate.CancelBulkUpdateAsync();

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();

                // Reset to normal Edit mode (clears flags and updates button)
                SetWorkMode(WorkMode.Edit);
            }
        }

        #endregion Bulk Update Handler

        #region OSMM Update Handler

        /// <summary>
        /// Start or cancel the OSMM Update mode (as appropriate).
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void OSMMUpdateCommandMenuClicked(object param)
        {
            // If already in OSMM update mode
            if (IsOsmmReviewMode)
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
        /// Start the OSMM Update mode.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void OSMMUpdateClicked(object param)
        {
            // Can't start OSMM Update mode if the bulk OSMM source hasn't been set.
            if (_addInSettings.BulkOSMMSourceId == null)
            {
                // Inform the user that the bulk OSMM source hasn't been set and that they need to
                // set it in the options before they can start the OSMM Update mode.
                ShowWarning("The Bulk OSMM Source has not been set.\n\n" +
                    "Please set the Bulk OSMM Source in the Options.", MessageCategory.OSMMUpdate);

                return;
            }

            _saving = false;
            _viewModelOSMMUpdate ??= new ViewModelWindowMainOSMMUpdate(this);

            // If the OSMM update mode is not already started.
            if (IsNotOsmmReviewMode)
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
        /// Cancel the OSMM Update mode.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void CancelOSMMUpdateClicked(object param)
        {
            if (_viewModelOSMMUpdate != null)
            {
                _osmmUpdatesEmpty = false;

                await _viewModelOSMMUpdate.CancelOSMMUpdateAsync();

                _viewModelOSMMUpdate = null;

                // Prevent OSMM updates being actioned too quickly.
                _osmmUpdating = false;
            }

            // Reset to normal Edit mode (clears flags and updates button)
            SetWorkMode(WorkMode.Edit);
        }

        /// <summary>
        /// Skip the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void OSMMSkipClicked(object param)
        {

            // Skip the OSMM Update for the current incid (don't wait).
            await OSMMSkipAsync();
        }

        /// <summary>
        /// Skip the OSMM Update for the current incid.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);

                    _osmmUpdating = false;

                    OnPropertyChanged(nameof(CanOSMMAccept));
                    OnPropertyChanged(nameof(CanOSMMSkip));
                }
                else
                {
                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);
                }

                // Refresh all the status type fields.
                RefreshStatus();

                // Reset the cursor back to normal.
                ChangeCursor(Cursors.Arrow, null);

                // Check if the GIS and database are in sync.
                CheckInSync("Selection", "Map");
            }
        }

        /// <summary>
        /// Accept the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void OSMMAcceptClicked(object param)
        {
            // Accept the OSMM Update for the current incid. (don't wait).
            await OSMMAcceptAsync();
        }

        /// <summary>
        /// Accept the OSMM Update for the current incid.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMAcceptAsync()
        {
            // Prevent OSMM updates being actioned too quickly.
            if (_osmmUpdating == false)
            {
                _osmmUpdating = true;

                if (OSMMAcceptTag == "Ctrl")
                {
                    // Mark all the remaining OSMM Update rows as accepted
                    await _viewModelOSMMUpdate.OSMMUpdateAllAsync(0);

                    // Reset the button tags
                    OSMMAcceptTag = "";
                    OSMMRejectTag = "";

                    // Refresh all the status type fields.
                    RefreshStatus();

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow, null);
                }
                else
                {
                    // Mark the OSMM Update row as accepted
                    _viewModelOSMMUpdate.OSMMUpdate(0);

                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map");
                }

                _osmmUpdating = false;

                OnPropertyChanged(nameof(CanOSMMAccept));
                OnPropertyChanged(nameof(CanOSMMSkip));
            }
        }

        /// <summary>
        /// Reject the OSMM Update for the current incid.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void OSMMRejectClicked(object param)
        {
            // Reject the OSMM Update for the current incid (don't wait).
            await OSMMRejectAsync();
        }

        /// <summary>
        /// Reject the OSMM Update for the current incid.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMRejectAsync()
        {
            // Prevent OSMM updates being actioned too quickly.
            if (_osmmUpdating == false)
            {
                _osmmUpdating = true;

                if (OSMMRejectTag == "Ctrl")
                {
                    // Mark all the remaining OSMM Update rows as accepted
                    await _viewModelOSMMUpdate.OSMMUpdateAllAsync(-99);

                    // Reset the button tags
                    OSMMAcceptTag = "";
                    OSMMRejectTag = "";

                    // Refresh all the status type fields.
                    RefreshStatus();

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow, null);
                }
                else
                {
                    // Mark the OSMM Update row as rejected
                    _viewModelOSMMUpdate.OSMMUpdate(-99);

                    // Move to the next Incid
                    await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex + 1);

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map");
                }

                _osmmUpdating = false;

                OnPropertyChanged(nameof(CanOSMMAccept));
                OnPropertyChanged(nameof(CanOSMMSkip));
            }
        }

        #endregion OSMM Update Handler

        #region OSMM Update Accept/Reject

        /// <summary>
        /// Accept the proposed OSMM Update.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMUpdateAcceptClickedAsync()
        {
            //Accept the proposed OSMM Update (don't wait).
            await OSMMUpdateAcceptAsync();
        }

        /// <summary>
        /// Accept the proposed OSMM Update.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMUpdateAcceptAsync()
        {
            _viewModelOSMMUpdate ??= new ViewModelWindowMainOSMMUpdate(this);

            // Save the current incid
            int incidCurrRowIx = IncidCurrentRowIndex;

            // Mark the OSMM Update row as accepted
            _viewModelOSMMUpdate.OSMMUpdate(0);

            // Reload the incid
            await MoveIncidCurrentRowIndexAsync(incidCurrRowIx);

            // Check if the GIS and database are in sync.
            CheckInSync("Selection", "Map");
        }

        /// <summary>
        /// Reject the proposed OSMM Update.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMUpdateRejectClickedAsync()
        {
            //Reject the proposed OSMM Update (don't wait).
            await OSMMUpdateRejectAsync();
        }

        /// <summary>
        /// Reject the proposed OSMM Update.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OSMMUpdateRejectAsync()
        {
            _viewModelOSMMUpdate ??= new ViewModelWindowMainOSMMUpdate(this);

            // Save the current incid
            int incidCurrRowIx = IncidCurrentRowIndex;

            // Mark the OSMM Update row as rejected
            _viewModelOSMMUpdate.OSMMUpdate(-99);

            // Reload the incid
            await MoveIncidCurrentRowIndexAsync(incidCurrRowIx);

            // Check if the GIS and database are in sync.
            CheckInSync("Selection", "Map");
        }

        #endregion OSMM Update Accept/Reject

        #region OSMM Bulk Update Handlers

        /// <summary>
        /// Start or cancel the OSMM Bulk Update mode (as appropriate).
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void OSMMBulkUpdateCommandMenuClicked(object param)
        {
            // If already in OSMM Bulk update mode
            if (IsOsmmBulkMode)
                // Cancel the OSMM Bulk update mode
                CancelOSMMBulkUpdateClicked(param);
            else
                // Start the OSMM Bulk update mode
                StartOSMMBulkUpdateClicked(param);
        }

        /// <summary>
        /// Start the OSMM Bulk Update mode.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void StartOSMMBulkUpdateClicked(object param)
        {
            _saving = false;
            _viewModelBulkUpdate ??= new ViewModelWindowMainBulkUpdate(this, _addInSettings);

            // If the OSMM Bulk update mode is not already started.
            if (IsNotOsmmBulkMode)
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
                _viewModelBulkUpdate.StartOSMMBulkUpdate();
            }
        }

        /// <summary>
        /// Cancel the OSMM Bulk Update mode.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private async void CancelOSMMBulkUpdateClicked(object param)
        {
            if (_viewModelBulkUpdate != null)
            {
                await _viewModelBulkUpdate.CancelOSMMBulkUpdateAsync();
                _viewModelBulkUpdate = null;
            }

            // Reset to normal Edit mode (clears flags and updates button)
            SetWorkMode(WorkMode.Edit);
        }

        #endregion OSMM Bulk Update Handlers

        #region About Dialog

        /// <summary>
        /// Show the about window.
        /// </summary>
        public void ShowAbout()
        {
            // Get the database backend and settings
            string dbBackend;
            dbBackend = String.Format("{0}{1}{2}{3}",
                _db.Backend.ToString(),
                String.IsNullOrEmpty(_db.DefaultSchema) ? null : " (",
                _db.DefaultSchema,
                String.IsNullOrEmpty(_db.DefaultSchema) ? null : ")");
            string dbSettings;
            dbSettings = _db.ConnectionString.Replace(";", "\n");

            // Create about window
            _windowAbout = new WindowAbout
            {
                // Set ArcGIS Pro as the parent
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Create ViewModel to which main window binds
            _viewModelAbout = new ViewModelWindowAbout
            {
                AppVersion = _appVersion,
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
            _viewModelAbout.RequestClose -= ViewModelAbout_RequestClose; // Safety: avoid double subscription.
            _viewModelAbout.RequestClose += new ViewModelWindowAbout.RequestCloseEventHandler(ViewModelAbout_RequestClose);

            // Allow all controls in window to bind to ViewModel by setting DataContext
            _windowAbout.DataContext = _viewModelAbout;

            // Show window
            _windowAbout.ShowDialog();
        }

        /// <summary>
        /// Closes about window and removes close window handler
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void ViewModelAbout_RequestClose()
        {
            // Remove the event handler and close the window.
            _viewModelAbout.RequestClose -= ViewModelAbout_RequestClose;
            _windowAbout.Close();
        }

        #endregion About Dialog

        #region Filter by Attributes Dialog

        /// <summary>
        /// Opens the relevant query window based on the mode/options.
        /// </summary>
        public void FilterByAttributes()
        {
            // Open the OSMM Updates query window if in OSMM Update mode.
            if (IsOsmmReviewMode || IsOsmmBulkMode)
            {
                // Can't start OSMM Update mode if the bulk OSMM source hasn't been set.
                if (_addInSettings.BulkOSMMSourceId == null)
                {
                    // Inform the user that the bulk OSMM source hasn't been set and that they need
                    // to set it in the options before they can start the OSMM Update mode.
                    ShowWarning("The Bulk OSMM Source has not been set.\n\n" +
                        "Please set the Bulk OSMM Source in the Options.", MessageCategory.OSMMUpdate);

                    return;
                }

                OpenWindowQueryOSMM(false);
            }
            else
            {
                // Open the select by attributes interface
                OpenWindowQueryAdvanced();
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
        /// Opens the new advanced query window.
        /// </summary>
        /// <exception cref="System.Exception">No parent window loaded</exception>
        private void OpenWindowQueryAdvanced()
        {
            try
            {
                // Create advanced query window
                _windowQueryAdvanced = new WindowQueryAdvanced
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryAdvanced = new(HluDataset, _db)
                {
                    DisplayName = "Advanced Query Builder"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryAdvanced.RequestClose -= ViewModelWinQueryAdvanced_RequestClose; // Safety: avoid double subscription.
                _viewModelWinQueryAdvanced.RequestClose +=
                    new ViewModelWindowQueryAdvanced.RequestCloseEventHandler(ViewModelWinQueryAdvanced_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryAdvanced.DataContext = _viewModelWinQueryAdvanced;

                // show window
                _windowQueryAdvanced.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Update);
                //throw;
            }
        }

        /// <summary>
        /// Process the sql when the advanced query window is closed.
        /// </summary>
        /// <param name="sqlFromTables">The tables to query.</param>
        /// <param name="sqlWhereClause">The where clause to apply in the query.</param>
        private async void ViewModelWinQueryAdvanced_RequestClose(string sqlFromTables, string sqlWhereClause)
        {
            // Remove the event handler and close the window.
            _viewModelWinQueryAdvanced.RequestClose -= ViewModelWinQueryAdvanced_RequestClose;
            _windowQueryAdvanced.Close();

            // If no query was specified, exit (this should not happen).
            if ((sqlFromTables == null) && (sqlWhereClause == null))
                return;

            try
            {
                ChangeCursor(Cursors.Wait, "Validating ...");

                // Get a list of all the possible query tables.
                List<DataTable> tables = [];
                if ((ViewModelWindowQueryAdvanced.HluDatasetStatic != null))
                {
                    tables = [.. ViewModelWindowQueryAdvanced.HluDatasetStatic.incid.ChildRelations
                        .Cast<DataRelation>().Select(r => r.ChildTable)];
                    tables.Add(ViewModelWindowQueryAdvanced.HluDatasetStatic.incid);
                }

                // Split the string of query table names created by the
                // user in the form into an array.
                string[] fromTables = [.. sqlFromTables.Split(',').Select(s => s.Trim(' ')).Distinct()];

                // Select only the database tables that are in the query array.
                List<DataTable> whereTables = [.. tables.Where(t => fromTables.Contains(t.TableName))];

                // Replace any connection type specific qualifiers and delimiters.
                string newWhereClause = null;
                if (sqlWhereClause != null)
                    newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                // Clear the selection of incids.
                _incidSelection = null;

                // Clear the previous where clause (set when performing the
                // original query builder or when reading the map selection)
                // because otherwise it might be used in error later.
                _incidSelectionWhereClause = null;

                // Create a selection DataTable of PK values of IncidTable
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

                // If there are any records in the selection (and the tool is
                // not currently in bulk update mode).
                if (IsFiltered)
                {
                    ChangeCursor(Cursors.Wait, "Counting ...");

                    // Find the expected number of features to be selected from the database.
                    _selectedFragsInDBCount = await ExpectedSelectionFeatures(whereTables, newWhereClause);

                    // Store the number of incids found in the database
                    _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                    // Find the expected number of features to be selected from GIS.
                    (_selectedIncidsInGISCount, _selectedToidsInGISCount, _selectedFragsInGISCount) = await _gisApp.ExpectedSelectionGISFeaturesAsync(_incidSelection);

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow, null);

                    // Check if the counts returned are less than those expected.
                    if (_selectedFragsInGISCount < _selectedFragsInDBCount)
                    {
                        ShowWarning("Not all expected features found in active layer.", MessageCategory.GIS);
                    }

                    //TODO: Needed?
                    // Indicate the selection didn't come from the map.
                    _filteredByMap = false;

                    if (_selectedIncidsInGISCount > 0)
                    {
                        ChangeCursor(Cursors.Wait, "Filtering ...");

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        // Set the filter back to the first incid.
                        await SetFilterAsync();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Check if the GIS and database are in sync.
                        CheckInSync("Selection", "Expected", "Not all expected");
                    }
                    else
                    {
                        // Restore the previous selection (filter).
                        //_incidSelection = incidSelectionBackup;

                        // Clear the selection (filter).
                        _incidSelection = null;

                        ChangeCursor(Cursors.Wait, "Filtering ...");

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        // Set the filter back to the first incid.
                        await SetFilterAsync();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found.
                        ShowInfo("No map features found in active layer.", MessageCategory.GIS);
                    }
                }
                else
                {
                    // Restore the previous selection (filter).
                    //_incidSelection = incidSelectionBackup;

                    // Clear the selection (filter).
                    _incidSelection = null;

                    // Indicate the selection didn't come from the map.
                    _filteredByMap = false;

                    // Set the filter back to the first incid.
                    await SetFilterAsync();

                    // Reset the cursor back to normal
                    ChangeCursor(Cursors.Arrow, null);

                    // Warn the user that no records were found
                    ShowInfo("No records found in database.", MessageCategory.Database);
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);

                // Show error message
                ShowError(ex.Message, MessageCategory.Filter);
            }
            finally { RefreshStatus(); }
        }

        /// <summary>
        /// Opens the warning on gis selection window to prompt the user
        /// for confirmation before proceeding.
        /// </summary>
        /// <param name="expectedNumFeatures">The expected number features.</param>
        /// <param name="expectedNumIncids">The expected number incids.</param>
        /// <returns><c>true</c> if the user confirms the selection; otherwise, <c>false</c>.</returns>
        /// <exception cref="Exception">No parent window loaded</exception>
        private bool ConfirmGISSelect(int expectedNumFeatures, int expectedNumIncids)
        {
            // Warn the user if the expected number of features is going to be above the warning threshold set in the options.
            if ((_warnBeforeMaxFeatures > 0) && (expectedNumFeatures > _warnBeforeMaxFeatures))
            {
                // Create warning GIS on selection window
                _windowWarnGISSelect = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // Create ViewModel to which main window binds
                _viewModelWinWarnGISSelect = new ViewModelWindowWarnOnGISSelect(
                    expectedNumFeatures, expectedNumIncids, expectedNumFeatures > -1 ? _gisLayerType : HluGeometryTypes.Unknown, _warnBeforeMaxFeatures);

                // When ViewModel asks to be closed, close window
                _viewModelWinWarnGISSelect.RequestClose -= ViewModelWinWarnGISSelect_RequestClose; // Safety: avoid double subscription.
                _viewModelWinWarnGISSelect.RequestClose +=
                    new ViewModelWindowWarnOnGISSelect.RequestCloseEventHandler(ViewModelWinWarnGISSelect_RequestClose);

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
        /// <param name="proceed">If set to <c>true</c> [proceed].</param>
        private void ViewModelWinWarnGISSelect_RequestClose(bool proceed)
        {
            // Remove the event handler and close the window.
            _viewModelWinWarnGISSelect.RequestClose -= ViewModelWinWarnGISSelect_RequestClose;
            _windowWarnGISSelect.Close();

            // Update the user warning count threshold in case it was changed in the options within the warning window.
            _warnBeforeMaxFeatures = Settings.Default.MaxFeaturesGISSelect;

            // If the user doesn't wish to proceed then clear the
            // current incid filter.
            if (!proceed)
            {
                _incidSelectionWhereClause = null;
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);
            }
        }

        #endregion Filter by Attributes Dialog

        #region OSMM Filter Dialogs

        /// <summary>
        /// Open the OSMM Updates query window when in OSMM Update mode.
        /// </summary>
        /// <param name="initialise">Whether to initialise the window.</param>
        public void OpenWindowQueryOSMM(bool initialise)
        {
            if (initialise)
            {
                // Clear the selection (filter).
                _incidSelection = null;

                // Indicate the selection didn't come from the map.
                _filteredByMap = false;

                // Indicate there are no more OSMM updates to review.
                if (IsNotOsmmBulkMode)
                    _osmmUpdatesEmpty = true;

                // Clear all the form fields (except the habitat class
                // and habitat type).
                //ClearForm();      // Already cleared

                // Clear the map selection.
                _gisApp.ClearMapSelection();

                // Reset the map counters
                _selectedIncidsInGISCount = 0;
                _selectedToidsInGISCount = 0;
                _selectedFragsInGISCount = 0;

                // Refresh all the controls
                RefreshAll();

                DispatcherHelper.DoEvents(); //TODO: Replace with modern equivalent?
            }

            try
            {
                // Create OSMM Updates query window
                _windowQueryOSMM = new WindowQueryOSMM
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryOSMM = new(HluDataset, _db, this)
                {
                    DisplayName = "OSMM Updates Filter"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryOSMM.RequestClose -= ViewModelWinQueryOSMM_RequestClose; // Safety: avoid double subscription.
                _viewModelWinQueryOSMM.RequestClose +=
                    new ViewModelWindowQueryOSMM.RequestCloseEventHandler(ViewModelWinQueryOSMM_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryOSMM.DataContext = _viewModelWinQueryOSMM;

                // show window
                _windowQueryOSMM.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Filter);
                //throw
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
        private async void ViewModelWinQueryOSMM_RequestClose(string processFlag, string spatialFlag, string changeFlag, string status, bool apply)
        {
            // Remove the event handler and close the window.
            _viewModelWinQueryOSMM.RequestClose -= ViewModelWinQueryOSMM_RequestClose;
            _windowQueryOSMM.Close();

            // If applying the query
            if ((apply == true) && (processFlag != null || spatialFlag != null || changeFlag != null || status != null))
            {
                // If in OSMM Bulk Update mode then set the default source details
                if (IsOsmmBulkMode)
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
                await ApplyOSMMUpdatesFilterAsync(processFlag, spatialFlag, changeFlag, status, true);
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
                _filteredByMap = false;

                // Indicate there are no more OSMM updates to review.
                if (IsNotOsmmBulkMode)
                    _osmmUpdatesEmpty = true;

                // Clear all the form fields (except the habitat class
                // and habitat type).
                ClearForm();

                // Clear the map selection.
                _gisApp.ClearMapSelection();

                // Reset the map counters
                _selectedIncidsInGISCount = 0;
                _selectedToidsInGISCount = 0;
                _selectedFragsInGISCount = 0;

                // Refresh all the controls
                RefreshAll();

                DispatcherHelper.DoEvents(); //TODO: Replace with modern equivalent?
            }

            try
            {
                // Create OSMM Updates advanced query window
                _windowQueryAdvanced = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinQueryAdvanced = new(HluDataset, _db)
                {
                    DisplayName = "OSMM Updates Advanced Filter"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQueryAdvanced.RequestClose -= ViewModelWinQueryOSMMAdvanced_RequestClose; // Safety: avoid double subscription.
                _viewModelWinQueryAdvanced.RequestClose +=
                    new ViewModelWindowQueryAdvanced.RequestCloseEventHandler(ViewModelWinQueryOSMMAdvanced_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQueryAdvanced.DataContext = _viewModelWinQueryAdvanced;

                // show window
                _windowQueryAdvanced.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Filter);
                //throw;
            }
        }

        /// <summary>
        /// Process the sql when the advanced query window is closed.
        /// </summary>
        /// <param name="sqlFromTables">The tables to query.</param>
        /// <param name="sqlWhereClause">The where clause to apply in the query.</param>
        private async void ViewModelWinQueryOSMMAdvanced_RequestClose(string sqlFromTables, string sqlWhereClause)
        {
            // Remove the event handler and close the window.
            _viewModelWinQueryAdvanced.RequestClose -= ViewModelWinQueryOSMMAdvanced_RequestClose;
            _windowQueryAdvanced.Close();

            // If no query was specified, exit (this should not happen).
            if ((sqlFromTables == null) || (sqlWhereClause == null))
                return;

            //if (IsOsmmBulkMode)
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

                // Get a list of all the possible query tables.
                List<DataTable> tables = [];
                if ((ViewModelWindowQueryAdvanced.HluDatasetStatic != null))
                {
                    tables = [.. ViewModelWindowQueryAdvanced.HluDatasetStatic.incid.ChildRelations
                        .Cast<DataRelation>().Select(r => r.ChildTable)];
                    tables.Add(ViewModelWindowQueryAdvanced.HluDatasetStatic.incid);
                }

                // Split the string of query table names created by the
                // user in the form into an array.
                string[] fromTables = [.. sqlFromTables.Split(',').Select(s => s.Trim(' ')).Distinct()];

                // Include the incid_osmm_updates table to use in the query.
                if (fromTables.Contains(IncidOSMMUpdatesTable.TableName) == false)
                    fromTables = [.. fromTables, IncidOSMMUpdatesTable.TableName];

                // Select only the database tables that are in the query array.
                List<DataTable> whereTables = [.. tables.Where(t => fromTables.Contains(t.TableName))];

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
                        _selectedFragsInDBCount = await ExpectedSelectionFeatures(whereTables, newWhereClause);

                        // Store the number of incids found in the database
                        _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                        ChangeCursor(Cursors.Wait, "Filtering ...");

                        // Select the required incid(s) in GIS.
                        if (await PerformGisSelectionAsync(true, _selectedFragsInDBCount, _selectedIncidsInDBCount))
                        {
                            // Analyse the results, set the filter and reset the cursor AFTER
                            // returning from performing the GIS selection so that other calls
                            // to the PerformGisSelection method can control if/when these things
                            // are done.
                            //
                            // Analyse the results of the GIS selection by counting the number of
                            // incids, toids and fragments selected.
                            AnalyzeGisSelectionSet(true);

                            // Check if the counts returned are less than those expected.
                            if (_selectedFragsInGISCount < _selectedFragsInDBCount)
                            {
                                ShowWarning("Not all selected features found in active layer.", MessageCategory.GIS);
                            }

                            // Indicate the selection didn't come from the map.
                            _filteredByMap = false;

                            if (IsNotOsmmBulkMode)
                            {
                                // Indicate there are more OSMM updates to review.
                                _osmmUpdatesEmpty = false;

                                OnPropertyChanged(nameof(CanOSMMAccept));
                                OnPropertyChanged(nameof(CanOSMMSkip));

                                // Set the filter to the first incid.
                                await SetFilterAsync();

                                // Check if the GIS and database are in sync.
                                CheckInSync("Selection", "Selected", "Not all selected");
                            }

                            // Refresh all the controls
                            RefreshAll();

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);
                        }
                        else
                        {
                            if (IsNotOsmmBulkMode)
                            {
                                // Clear the selection (filter).
                                _incidSelection = null;

                                // Indicate the selection didn't come from the map.
                                _filteredByMap = false;

                                // Indicate there are no more OSMM updates to review.
                                if (IsNotOsmmBulkMode)
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
                            }

                            // Reset the cursor back to normal.
                            ChangeCursor(Cursors.Arrow, null);

                            // Warn the user that no records were found.
                            ShowInfo("No map features found in active layer.", MessageCategory.GIS);
                        }
                    }
                    else
                    {
                        if (IsNotOsmmBulkMode)
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
                        }
                        else
                        {
                            // Restore the previous selection (filter).
                            _incidSelection = incidSelectionBackup;
                        }

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found.
                        ShowInfo("No records found in database.", MessageCategory.Database);
                    }
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);

                // Show error message
                ShowError(ex.Message, MessageCategory.Filter);
            }
            finally
            {
                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion OSMM Filter Dialogs

        #region Secondary Habitat Dialogs

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
                        !SecondaryHabitat.SecondaryHabitatList.Any(sh => sh.Secondary_habitat == _secondaryHabitat))
                        AddSecondaryHabitat(false, -1, Incid, _secondaryHabitat, secondaryGroup);

                    // Clear the secondary habitat selection to reset the combo box display.
                    _secondaryHabitat = null;
                    OnPropertyChanged(nameof(SecondaryHabitatCode));

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
                // Show error message
                ShowError(ex.Message, MessageCategory.Update);
                //throw;
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
                // Create window
                _windowQuerySecondaries = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinQuerySecondaries = new()
                {
                    DisplayName = "Add Secondary Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinQuerySecondaries.RequestClose -= ViewModelWinQuerySecondaries_RequestClose; // Safety: avoid double subscription.
                _viewModelWinQuerySecondaries.RequestClose +=
                    new ViewModelWindowQuerySecondaries.RequestCloseEventHandler(ViewModelWinQuerySecondaries_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowQuerySecondaries.DataContext = _viewModelWinQuerySecondaries;

                // show window
                _windowQuerySecondaries.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Filter);
                throw;
            }
        }

        /// <summary>
        /// Closes the query add secondaries window and adds the list of
        /// secondary habitats to the tble.
        /// </summary>
        /// <param name="querySecondaries">The list of secondaries to add.</param>
        private void ViewModelWinQuerySecondaries_RequestClose(String querySecondaries)
        {
            // Remove the event handler and close the window.
            _viewModelWinQuerySecondaries.RequestClose -= ViewModelWinQuerySecondaries_RequestClose;
            _windowQuerySecondaries.Close();

            // If no secondaries were entered then just exit.
            if (String.IsNullOrEmpty(querySecondaries))
                return;

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
                                !SecondaryHabitat.SecondaryHabitatList.Any(sh => sh.Secondary_habitat == secondaryHabitat))
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
                        errorCodes = [.. errorCodes.Distinct().OrderBy(e => e.PadLeft(5, '0'))];
                        // Message the user, depending on if there is one or more
                        if (errorCodes.Count == 1)
                            ShowWarning("Code '" +
                                errorCodes.FirstOrDefault() + "' is a duplicate/unknown and has not been added.",
                                MessageCategory.Update);
                        else
                            ShowWarning("Codes '" +
                                String.Join(", ", errorCodes.Take(errorCodes.Count - 1)) + " and " + errorCodes.Last() + "' are duplicates/unknown and have not been added.",
                                MessageCategory.Update);
                    }
                }
            }
            catch (Exception ex)
            {
                ChangeCursor(Cursors.Arrow, null);

                // Show error message
                ShowError(ex.Message, MessageCategory.Update);
            }
        }

        #endregion Secondary Habitat Dialogs

        #region Priority Habitat Dialogs

        /// <summary>
        /// Opens the Priority Habitats editing dialog, allowing the user to view and modify priority habitat
        /// information.
        /// </summary>
        /// <remarks>The dialog is displayed modally and blocks interaction with the main window until it
        /// is closed. Any changes made in the dialog are applied when the user confirms their edits.</remarks>
        /// <param name="param">An optional parameter that can be used to pass command arguments. This parameter is not used by this method.</param>
        private void EditPriorityHabitatsClicked(object param)
        {
            try
            {
                // Create window
                _windowEditPriorityHabitats = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinEditPriorityHabitats = new(this, IncidBapHabitatsAuto)
                {
                    DisplayName = "Priority Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinEditPriorityHabitats.RequestClose -= ViewModelWinEditPriorityHabitats_RequestClose; // Safety: avoid double subscription.
                _viewModelWinEditPriorityHabitats.RequestClose += new ViewModelWindowEditPriorityHabitats
                    .RequestCloseEventHandler(ViewModelWinEditPriorityHabitats_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowEditPriorityHabitats.DataContext = _viewModelWinEditPriorityHabitats;

                // show window
                _windowEditPriorityHabitats.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Update);
                //throw;
            }
        }

        /// <summary>
        /// Handle the RequestClose event from the Edit Priority Habitats window.
        /// </summary>
        /// <param name="incidBapHabitatsAuto"></param>
        private void ViewModelWinEditPriorityHabitats_RequestClose(ObservableCollection<BapEnvironment> incidBapHabitatsAuto)
        {
            // Remove the event handler and close the window.
            _viewModelWinEditPriorityHabitats.RequestClose -= ViewModelWinEditPriorityHabitats_RequestClose;
            _windowEditPriorityHabitats.Close();

            // If any habitats were returned then update the main window.
            if (incidBapHabitatsAuto != null)
            {
                IncidBapHabitatsAuto = incidBapHabitatsAuto;

                // Check if there are any errors in the primary BAP records to see
                // if the Priority tab label should be flagged as also in error.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                    if (countInvalid > 0)
                        AddToErrorList(_priorityErrors, "BapAuto");
                    else
                        RemoveFromErrorList(_priorityErrors, "BapAuto");
                }

                OnPropertyChanged(nameof(IncidBapHabitatsAuto));
                OnPropertyChanged(nameof(PriorityTabLabel));
            }
        }

        /// <summary>
        /// Opens the Potential Priority Habitats editing window, allowing the user to view and modify potential habitat
        /// data.
        /// </summary>
        /// <remarks>The editing window is displayed as a modal dialog and is centered over the main
        /// ArcGIS Pro window. All controls in the window are bound to the associated ViewModel. If an error occurs
        /// while opening the window, an error message is displayed to the user.</remarks>
        /// <param name="param">An optional parameter that can be used to pass command arguments. This value is not used by this method.</param>
        private void EditPotentialHabitatsClicked(object param)
        {
            try
            {
                // Create window
                _windowEditPotentialHabitats = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinEditPotentialHabitats = new(this, IncidBapHabitatsUser)
                {
                    DisplayName = "Potential Priority Habitats"
                };

                // when ViewModel asks to be closed, close window
                _viewModelWinEditPotentialHabitats.RequestClose -= ViewModelWinEditPotentialHabitats_RequestClose; // Safety: avoid double subscription.
                _viewModelWinEditPotentialHabitats.RequestClose += new ViewModelWindowEditPotentialHabitats
                    .RequestCloseEventHandler(ViewModelWinEditPotentialHabitats_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowEditPotentialHabitats.DataContext = _viewModelWinEditPotentialHabitats;

                // show window
                _windowEditPotentialHabitats.ShowDialog();
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.Update);
                //throw;
            }
        }

        /// <summary>
        /// Handle the RequestClose event from the Edit Potential Priority Habitats window.
        /// </summary>
        /// <param name="incidBapHabitatsUser"></param>
        private void ViewModelWinEditPotentialHabitats_RequestClose(ObservableCollection<BapEnvironment> incidBapHabitatsUser)
        {
            // Remove the event handler and close the window.
            _viewModelWinEditPotentialHabitats.RequestClose -= ViewModelWinEditPotentialHabitats_RequestClose;
            _windowEditPotentialHabitats.Close();

            // If any habitats were returned then update the main window.
            if (incidBapHabitatsUser != null)
            {
                IncidBapHabitatsUser = incidBapHabitatsUser;

                // Check if there are any errors in the optional BAP records to see
                // if the Priority tab label should be flagged as also in error.
                if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
                {
                    int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                    if (countInvalid > 0)
                        AddToErrorList(_priorityErrors, "BapUser");
                    else
                        RemoveFromErrorList(_priorityErrors, "BapUser");
                }

                OnPropertyChanged(nameof(IncidBapHabitatsUser));
                OnPropertyChanged(nameof(PriorityTabLabel));
            }
        }

        #endregion Priority Habitat Dialogs

        #region Active GIS Layer

        /// <summary>
        /// Updates the enabled state of the active layer combo box based on the application mode and
        /// the number of available map layers.
        /// </summary>
        /// <remarks>The combo box is enabled only when neither bulk update nor OSMM update modes are
        /// active, and there is more than one map layer available. Otherwise, the combo box is disabled.
        /// This method should be called whenever the application mode or the set of available map layers
        /// changes to ensure the UI reflects the current state.</remarks>
        private void UpdateActiveLayerComboBoxEnabledState()
        {
            if (IsNotBulkMode && IsNotOsmmReviewMode)
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

        #endregion Active GIS Layer

        #region Dock Pane Caption

        /// <summary>
        /// Updates the dock pane caption to show if read-only or not.
        /// </summary>
        private void UpdateDockPaneCaption()
        {
            string readonlyPart = string.Empty;

            // Set the read-only part.
            if ((_gisApp != null) && (_gisApp.ActiveHluLayer != null) && (!(bool)_gisApp?.ActiveHluLayer?.IsEditable))
                readonlyPart = $" [READONLY]";

            Caption = _dockPaneBaseCaption + readonlyPart;
            OnPropertyChanged(nameof(Caption));

            // Only show the title when the pane is tabbed with other panes.
            TabText = _dockPaneBaseCaption;
        }

        #endregion Dock Pane Caption

        #region Copy/Paste

        /// <summary>
        /// Handles the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for properties related to copy
        /// operations.
        /// </summary>
        /// <remarks>This method raises the <see cref="INotifyPropertyChanged.PropertyChanged"/> event for
        /// the  <see cref="CanCopy"/> property if the changed property name starts with "Copy", or for the  <see
        /// cref="CanPaste"/> property otherwise.</remarks>
        /// <param name="sender">The source of the event, typically the object whose property changed.</param>
        /// <param name="e">The event data containing the name of the property that changed.</param>
        internal void CopySwitches_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.StartsWith("Copy"))
                OnPropertyChanged(nameof(CanCopy));
            else
                OnPropertyChanged(nameof(CanPaste));
        }

        /// <summary>
        /// Copies the attribute values from the current Incid row.
        /// </summary>
        /// <remarks>This method stores the current instance's attribute values</remarks>
        public void CopyAttributes()
        {
            _copySwitches.CopyValues(this);
        }

        /// <summary>
        /// Pastes attribute values from a copied source into the current instance.
        /// </summary>
        /// <remarks>This method applies copied attribute values to the current instance using the
        /// internal copy mechanism.</remarks>
        public void PasteAttributes()
        {
            _copySwitches.PasteValues(this);
        }

        #endregion Copy/Paste

        #region Formatting Helpers

        /// <summary>
        /// Calculates and updates the area and length measures for the current incid based on associated polygon
        /// data.
        /// </summary>
        /// <remarks>This method retrieves polygon records related to the current incid and computes
        /// the total area and length, storing the results in internal fields. The calculation is performed only if the
        /// measures have not already been set and a current incid row is available.</remarks>
        private void GetIncidMeasures()
        {
            // If the measures have already been calculated or there is no current incid, exit the method.
            if (((_incidArea != -1) && (_incidLength != -1)) || (IncidCurrentRow == null)) return;

            // Calculate the area and length measures for the current incid based on associated polygon data.
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

        #endregion Formatting Helpers

        #endregion Methods

    }
}