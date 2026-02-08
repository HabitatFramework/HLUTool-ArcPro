// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using ArcGIS.Core.Data.UtilityNetwork;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU;
using HLU.Data;
using HLU.Date;
using HLU.Properties;
using HLU.UI.UserControls;
using HLU.UI.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    #region enums

    /// <summary>
    /// An enumeration of the different options for what to do when
    /// attempting to update a subset of features for an incid.
    /// </summary>
    public enum SubsetUpdateActions
    {
        Prompt,
        Split,
        All
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to auto zoom to the GIS selection.
    /// </summary>
    public enum AutoZoomToSelection
    {
        Off,
        When,
        Always
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate secondary codes against the habitat type
    /// mandatory codes.
    /// </summary>
    public enum HabitatSecondaryCodeValidationOptions
    {
        Ignore,
        Warning,
        Error
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate secondary codes against the primary code.
    /// </summary>
    public enum PrimarySecondaryCodeValidationOptions
    {
        Ignore,
        Error
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate quality determination and interpretation.
    /// </summary>
    public enum QualityValidationOptions
    {
        Optional,
        Mandatory
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate potential priority habitat quality determination.
    /// </summary>
    public enum PotentialPriorityDetermQtyValidationOptions
    {
        Ignore,
        Error
    };

    /// <summary>
    /// Update operations.
    /// </summary>
    public enum Operations
    {
        PhysicalMerge,
        PhysicalSplit,
        LogicalMerge,
        LogicalSplit,
        AttributeUpdate,
        BulkUpdate,
        OSMMUpdate
    };

    /// <summary>
    /// Represents the current operational state(s) of the HLU tool.
    ///
    /// This enum uses the [Flags] attribute, meaning each value corresponds
    /// to a single bit in a binary number. Because of that, multiple values
    /// can be combined using bitwise OR (e.g. Edit | Bulk).
    ///
    /// For example:
    ///   Edit       = 0001 (1)
    ///   Bulk       = 0010 (2)
    ///   OsmmReview = 0100 (4)
    ///   OsmmBulk   = 1000 (8)
    ///
    /// If the tool is simultaneously in Edit mode and Bulk Update mode,
    /// the combined state is:
    ///   0001 | 0010 = 0011  (decimal value 3)
    ///
    /// Checking whether a specific mode is active is done with:
    ///   WorkMode.HasFlag(HluEditMode.Bulk)
    ///
    /// This creates a clean, extensible state system without relying
    /// on multiple unrelated booleans.
    /// </summary>
    [Flags]
    public enum HluWorkMode
    {
        None = 0,
        Edit = 1 << 0, // Previously EditMode.
        Bulk = 1 << 1, // Previously _bulkUpdateMode.
        OSMMReview = 1 << 2, // Previously _osmmUpdateMode.
        OSMMBulk = 1 << 3  // Previously _osmmBulkUpdateMode.
    }

    #endregion enums

    /// <summary>
    /// Build the DockPane.
    /// </summary>
    public partial class ViewModelWindowMain : PanelViewModelBase, INotifyPropertyChanged
    {

        #region Fields

        private ViewModelWindowMain _dockPane;

        private Task _initializationTask;
        private Exception _initializationException;

        private bool _mapEventsSubscribed;
        private bool _projectClosedEventsSubscribed;
        private bool _layersChangedEventsSubscribed;

        private string _displayName = "HLU Tool";
        private bool _editMode;

        private MapView _activeMapView;

        private XmlSettingsManager _xmlSettingsManager;
        private AddInSettings _addInSettings;

        #endregion Fields

        #region PanelViewModelBase Members

        /// <summary>
        /// Returns the user-friendly name of this object.
        /// Child classes can set this property to a new value,
        /// or override it to determine the value on-demand.
        /// </summary>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        /// <summary>
        /// The title of the main window.
        /// </summary>
        public override string WindowTitle
        {
            get
            {
                return String.Format("{0}{1}", DisplayName, _editMode ? String.Empty : " [READONLY]");
            }
        }

        /// <summary>
        /// Set the global variables.
        /// </summary>
        internal ViewModelWindowMain()
        {
            // Initialise the DockPane components (don't wait for it to complete).
            //_ = EnsureInitializedAsync();
        }

        //TODO: Needed?
        /// <summary>
        /// Set the global variables for just the combo box sources.
        /// </summary>
        internal ViewModelWindowMain(bool minimal)
        {
            ViewModelWindowMain temp = this;
            //TODO: Catch exceptions?
            // Load the data grid combo box sources.
            LoadComboBoxSources();
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
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
        /// <param name="isVisible"></param>
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
                    AsyncHelpers.ObserveTask(
                        checkTask,
                        "HLU Tool",
                        "The HLU Tool encountered an error initialising or checking the active map.");
                }
            }
            else
            {
                // Unsubscribe from events when the dockpane is hidden.
                if (_mapEventsSubscribed)
                {
                    _mapEventsSubscribed = false;

                    // Unsubscribe from ActiveMapViewChangedEvent events.
                    ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
                }

                if (_layersChangedEventsSubscribed)
                {
                    _layersChangedEventsSubscribed = false;

                    // Unsubscribe from the LayersAddedEvents.
                    LayersAddedEvent.Unsubscribe(OnLayersAdded);

                    // Unsubscribe from the LayersRemovedEvents.
                    LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
                }

                if (_projectClosedEventsSubscribed)
                {
                    // Unsubscribe from OnProjectClosed events.
                    _projectClosedEventsSubscribed = false;
                    ProjectClosedEvent.Unsubscribe(OnProjectClosed);
                }
            }

            // If the dockpane is visible.
            if (isVisible == true)
            {
                //TODO: Needed?
                //base.OnShow(isVisible);

                // Toggle the tab state to visible.
                ToggleState("HLUTool_tab_state", true);

                // Clear any messages.
                ClearMessage();

                // Make the UI controls visible.
                GridMainVisibility = Visibility.Visible;
            }
        }

        #endregion ViewModelBase Members

        #region Initialisation

        /// <summary>
        /// Ensures the tool is initialised, then checks that the active map/layers are suitable.
        /// </summary>
        internal async Task InitializeAndCheckAsync()
        {
            // Ensure the DockPane is initialised.
            await EnsureInitializedAsync();

            //TODO: Needed? Done during initialisation above.
            // Check that there is an active map and that it contains a valid HLU map.
            //await CheckActiveMapAsync();
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

                //TODO: What to do if the upgrade fails?
                // Upgrade the user settings if necessary.
                UpgradeUserSettings();

                // Initialise the main view (start the tool).
                await InitializeToolPaneAsync();

                // Flag the initialisation as complete.
                Initialised = true;

                // Refresh the tab control enabled state.
                OnPropertyChanged(nameof(TabControlDataEnabled));

                // Clear any messages.
                ClearMessage();

                // Make the UI controls visible.
                GridMainVisibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                InError = true;
                _initializationException = ex;

                throw;
            }
        }

        /// <summary>
        /// Upgrade the XML settings if necessary.
        /// </summary>
        private void UpgradeXMLSettings()
        {
            // If the XML settings haven't been upgrade yet then upgrade them.
            if (Settings.Default.CallXMLUpgrade)
            {
                // Remove the following nodes from the XML file:
                _xmlSettingsManager.RemoveNode("HelpPages");

                // Set the call upgrade flag to false.
                Settings.Default.CallXMLUpgrade = false;

                // Save the settings.
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Upgrade the application and user settings if necessary.
        /// </summary>
        private void UpgradeUserSettings()
        {
            // If the settings haven't been upgrade yet then upgrade them.
            if (Settings.Default.CallUpgrade)
            {
                // Upgrade the settings.
                Settings.Default.Upgrade();

                // Set the call upgrade flag to false.
                Settings.Default.CallUpgrade = false;

                // Save the settings.
                Settings.Default.Save();
            }
        }

        #endregion Initialisation

        #region Properties

        /// <summary>
        /// ID of the DockPane.
        /// </summary>
        private const string _dockPaneID = "HLUTool_UI_WindowMain";

        public static string DockPaneID
        {
            get => _dockPaneID;
        }

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

        private bool _initialised = false;

        /// <summary>
        /// Has the DockPane been initialised?
        /// </summary>
        public bool Initialised
        {
            get { return _initialised; }
            set
            {
                _initialised = value;
            }
        }

        private bool _inError = false;

        /// <summary>
        /// Is the DockPane in error?
        /// </summary>
        public bool InError
        {
            get { return _inError; }
            set
            {
                _inError = value;
            }
        }

        private bool _formLoading;

        /// <summary>
        /// Is the form loading?
        /// </summary>
        public bool FormLoading
        {
            get { return _formLoading; }
            set { _formLoading = value; }
        }

        private string _helpURL;

        /// <summary>
        /// The URL of the help page.
        /// </summary>
        public string HelpURL
        {
            get { return _helpURL; }
            set { _helpURL = value; }
        }

        public AddInSettings AddInSettings
        {
            get { return _addInSettings; }
            set { _addInSettings = value; }
        }

        #endregion Properties

        #region Active Map View

        private async void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs obj)
        {
            // If there is no active map view.
            if (MapView.Active == null)
            {
                // Reset the active map view.
                _activeMapView = null;

                // Make the UI controls hidden.
                GridMainVisibility = Visibility.Hidden;

                // Display a warning message.
                ShowMessage("No active map.", MessageType.Warning);

                return;
            }

            // If there is an active map view and it has changed, check it is valid.
            if (MapView.Active != _activeMapView)
            {
                // Check that there is an active map and that it contains a valid HLU map.
                if (!await CheckActiveMapAsync())
                {
                    //TODO: Is this needed?
                    // Clear the active map view.
                    //_activeMapView = null;

                    // Make the UI controls hidden.
                    GridMainVisibility = Visibility.Hidden;

                    return;
                }
            }

            // Clear any messages.
            ClearMessage();

            // Make the UI controls visible.
            GridMainVisibility = Visibility.Visible;
        }

        private async void OnLayersAdded(LayerEventsArgs args)
        {
            // Check that there is an active map and that it contains a valid HLU map.
            if (!await CheckActiveMapAsync())
            {
                _activeMapView = null;
                return;
            }
        }

        private async void OnLayersRemoved(LayerEventsArgs args)
        {
            foreach (var layer in args.Layers)
            {
                // If the active layer has been removed force
                // a new GIS functions object to be created.
                //TODO: Clear variables instead of creating new instance?
                if (layer.Name == ActiveLayerName)
                    _gisApp = null;
            }

            // Check that there is an active map and that it contains a valid HLU map.
            if (!await CheckActiveMapAsync())
            {
                _activeMapView = null;
                return;
            }

            // Refresh the layer name (in case it has changed).
            OnPropertyChanged(nameof(ActiveLayerName));
        }

        private void OnProjectClosed(ProjectEventArgs obj)
        {
            if (MapView.Active == null)
            {
                // Make the UI controls hidden.
                GridMainVisibility = Visibility.Hidden;
            }

            _projectClosedEventsSubscribed = false;

            ProjectClosedEvent.Unsubscribe(OnProjectClosed);
        }

        private Visibility _gridmainVisibility = Visibility.Visible;

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

        /// <summary>
        /// Event when the DockPane is hidden.
        /// </summary>
        protected override void OnHidden()
        {
            //TODO: Needed?
            if (_mapEventsSubscribed)
            {
                _mapEventsSubscribed = false;

                // Unsubscribe from ActiveMapViewChangedEvent events
                // (in case the tool is never shown again).
                ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
            }

            //TODO: Needed?
            if (_layersChangedEventsSubscribed)
            {
                _layersChangedEventsSubscribed = false;

                // Unsubscribe from the LayersAddedEvents
                // (in case the tool is never shown again).
                LayersAddedEvent.Unsubscribe(OnLayersAdded);

                // Unsubscribe from the LayersRemovedEvents
                // (in case the tool is never shown again).
                LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
            }

            //TODO: Needed?
            if (_projectClosedEventsSubscribed)
            {
                _projectClosedEventsSubscribed = false;

                // Unsubscribe from the OnProjectClosed events
                // (in case the tool is never shown again).
                ProjectClosedEvent.Unsubscribe(OnProjectClosed);
            }

            // Toggle the tab state to hidden.
            ToggleState("HLUTool_tab_state", false);
        }

        #endregion Active Map View

        #region StatusMessage

        private string _statusMessage;

        /// <summary>
        /// The message to display on the form.
        /// </summary>
        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(HasMessage));
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private MessageType _messageLevel;

        /// <summary>
        /// The type of message; Error, Warning, Confirmation, Information
        /// </summary>
        public MessageType MessageLevel
        {
            get
            {
                return _messageLevel;
            }
            set
            {
                _messageLevel = value;
                OnPropertyChanged(nameof(MessageLevel));
            }
        }

        /// <summary>
        /// Is there a message to display?
        /// </summary>
        public Visibility HasMessage
        {
            get
            {
                if (_dockPane == null
                || String.IsNullOrEmpty(_statusMessage))
                //|| _dockPane.ProcessStatus != null
                    return Visibility.Collapsed;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Show the message with the required icon (message type).
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="messageLevel"></param>
        public void ShowMessage(string msg, MessageType messageLevel)
        {
            MessageLevel = messageLevel;
            StatusMessage = msg;
        }

        /// <summary>
        /// Clear the form messages.
        /// </summary>
        public void ClearMessage()
        {
            StatusMessage = "";
        }

        #endregion StatusMessage

        #region Ribbon Controls

        // ObservableCollection to hold HLU layers combo box items
        private ObservableCollection<string> _availableHLULayerNames = [];

        /// <summary>
        /// The available HLU layers.
        /// </summary>
        public ObservableCollection<string> AvailableHLULayerNames
        {
            get { return _availableHLULayerNames; }
            set
            {
                _availableHLULayerNames = value;
                OnPropertyChanged(nameof(AvailableHLULayerNames));
            }
        }

        /// <summary>
        /// Switch the active layer.
        /// </summary>
        /// <param name="selectedValue"></param>
        public async Task SwitchGISLayerAsync(string selectedValue)
        {
            // Check if the layer name has actually changed.
            if (selectedValue != ActiveLayerName)
            {
                // Create a new GIS functions instance if necessary.
                if (_gisApp == null || _gisApp.MapName == null)
                    _gisApp = new();

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
        /// <param name="stateID"></param>
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

        #endregion

    }
}