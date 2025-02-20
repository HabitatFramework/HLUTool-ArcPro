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
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU;
using HLU.Properties;
using HLU.UI.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ArcGIS.Desktop.Internal.Framework.Controls;
using System.Collections.ObjectModel;
using HLU.Data;
using System.Linq;

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
    /// An enumeration of the different options for when to warn
    /// the user before performing a GIS selection.
    /// </summary>
    public enum WarnBeforeGISSelect
    {
        Always,
        Joins,
        Never
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to auto zoom to the GIS selection.
    /// </summary>
    public enum AutoZoomSelection
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

    //---------------------------------------------------------------------
    // CHANGED: CR49 Process proposed OSMM Updates
    // Functionality to process proposed OSMM Updates.
    //
    /// <summary>
    /// Update operations.
    /// </summary>
    public enum Operations { PhysicalMerge, PhysicalSplit, LogicalMerge, LogicalSplit, AttributeUpdate, BulkUpdate, OSMMUpdate };
    //---------------------------------------------------------------------

    /// <summary>
    /// User Interface control visibility values.
    /// </summary>
    //public enum Visibility { Visible, Hidden, Collapsed };

    #endregion enums

    /// <summary>
    /// Build the DockPane.
    /// </summary>
    public partial class ViewModelWindowMain : PanelViewModelBase, INotifyPropertyChanged
    {

        #region Fields

        private ViewModelWindowMain _dockPane;

        private bool _mapEventsSubscribed;
        private bool _projectClosedEventsSubscribed;
        private bool _layersChangedEventsSubscribed;

        private string _displayName = "HLU Tool";
        private bool _editMode;

        private MapView _activeMapView;

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
            // Initialise the DockPane components and wait for it to complete.
            InitializeComponentAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Set the global variables.
        /// </summary>
        internal ViewModelWindowMain(bool minimal)
        {
            // Load the data grid combo box sources and wait for it to complete.
            LoadComboBoxSourcesAsync().GetAwaiter().GetResult();

            // Subscribe to the active layer combobox selection event
            //ActiveLayerComboBox.OnComboBoxSelectionChanged += HandleComboBoxSelection;
        }

        /// <summary>
        /// Initialise the DockPane components.
        /// </summary>
        public async Task InitializeComponentAsync()
        {
            //if (!Initialised)
            //{
            _dockPane = this;
            _initialised = false;
            _inError = false;

            // Set the help URL.
            _dockPane.HelpURL = Settings.Default.HelpURL;

            try
            {
                //TODO: What to do if the upgrade fails?
                UpgradeSettings();

                // Initialise the main view (start the tool)
                if (!await InitializeToolPaneAsync())
                {
                    //TODO: What to do if initialise fails?
                }
            }
            finally
            {
                // Indicate that the dockpane has been initialised.
                _initialised = true;
            }
        }
        //}

        private void UpgradeSettings()
        {
            if (Settings.Default.CallUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.CallUpgrade = false;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            ViewModelWindowMain vm = pane as ViewModelWindowMain;

            //// If the ViewModel is uninitialised then initialise it.
            if (!vm.Initialised)
                vm.InitializeComponentAsync();

            // If the ViewModel is in error then don't show the dockpane.
            if (vm.InError)
            {
                pane = null;
                return;
            }

            // Active the dockpane.
            pane.Activate();
        }

        protected override void OnShow(bool isVisible)
        {
            // Hide the dockpane if there is no active map.
            if (MapView.Active == null)
                DockpaneVisibility = Visibility.Hidden;

            // Is the dockpane visible (or is the window not showing the map).
            if (isVisible)
            {
                if (!_mapEventsSubscribed)
                {
                    _mapEventsSubscribed = true;

                    // Subscribe from map changed events.
                    ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChangedAsync);
                }

                if (!_projectClosedEventsSubscribed)
                {
                    _projectClosedEventsSubscribed = true;

                    // Suscribe to project closed events.
                    ProjectClosedEvent.Subscribe(OnProjectClosed);
                }

                if (!_layersChangedEventsSubscribed)
                {
                    _layersChangedEventsSubscribed = true;

                    // Subscribe to the LayersAddedEvent
                    LayersAddedEvent.Subscribe(OnLayersAddedAsync);

                    // Subscribe to the LayersRemovedEvent
                    LayersRemovedEvent.Subscribe(OnLayersRemovedAsync);
                }
            }
            else
            {
                if (_mapEventsSubscribed)
                {
                    _mapEventsSubscribed = false;

                    // Unsubscribe from map changed events.
                    ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChangedAsync);
                }

                if (_layersChangedEventsSubscribed)
                {
                    _layersChangedEventsSubscribed = false;

                    // Subscribe to the LayersAddedEvent
                    LayersAddedEvent.Unsubscribe(OnLayersAddedAsync);

                    // Subscribe to the LayersRemovedEvent
                    LayersRemovedEvent.Unsubscribe(OnLayersRemovedAsync);
                }
            }

            base.OnShow(isVisible);

            // Toggle the tab state to visible.
            ToggleState("tab_state", true);
        }

        #endregion ViewModelBase Members

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

        #endregion Properties

        #region Active Map View

        private async void OnActiveMapViewChangedAsync(ActiveMapViewChangedEventArgs obj)
        {
            if (MapView.Active == null)
            {
                // Hide the dockpane.
                DockpaneVisibility = Visibility.Hidden;

                // Display an error message.
                ShowMessage("No active map.", MessageType.Warning);

                // Clear the form lists.
                //_paneH2VM?.ClearFormLists();
            }
            else
            {
                //TODO: UI
                // Check the active map is valid.
                if (MapView.Active != _activeMapView)
                {
                    if (!await CheckActiveMapAsync())
                    {
                        _activeMapView = null;
                        return;
                    }
                }

                // Clear any messages.
                ClearMessage();

                DockpaneVisibility = Visibility.Visible;

                // Save the active map view.
                _activeMapView = MapView.Active;
            }
        }

        private async void OnLayersAddedAsync(LayerEventsArgs args)
        {
            if (!await CheckActiveMapAsync())
            {
                _activeMapView = null;
                return;
            }
        }

        private async void OnLayersRemovedAsync(LayerEventsArgs args)
        {
            foreach (var layer in args.Layers)
            {
                // If the active layer has been removed force
                // a new GIS functions object to be created.
                //TODO: Clear variables instead of creating new instance?
                if (layer.Name == ActiveLayerName)
                    _gisApp = null;
            }

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
                DockpaneVisibility = Visibility.Hidden;

                //TODO: UI
                // Do something when the active map view closes
            }

            _projectClosedEventsSubscribed = false;

            ProjectClosedEvent.Unsubscribe(OnProjectClosed);
        }

        private Visibility _dockpaneVisibility = Visibility.Visible;

        public Visibility DockpaneVisibility
        {
            get { return _dockpaneVisibility; }
            set
            {
                _dockpaneVisibility = value;
                OnPropertyChanged(nameof(DockpaneVisibility));
            }
        }

        /// <summary>
        /// Event when the DockPane is hidden.
        /// </summary>
        protected override void OnHidden()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            ViewModelWindowMain vm = pane as ViewModelWindowMain;

            // Force the dockpane to be re-initialised next time it's shown.
            vm.Initialised = false;

            // Toggle the tab state to hidden.
            ToggleState("tab_state", false);
        }

        #endregion Active Map View

        #region Message

        private string _message;

        /// <summary>
        /// The message to display on the form.
        /// </summary>
        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged(nameof(HasMessage));
                OnPropertyChanged(nameof(Message));
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
                if (_dockPane.ProcessStatus != null
                || string.IsNullOrEmpty(_message))
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
            Message = msg;
        }

        /// <summary>
        /// Clear the form messages.
        /// </summary>
        public void ClearMessage()
        {
            Message = "";
        }

        #endregion Message

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

        // These properties are used by the ribbon controls
        //private ActiveLayerComboBox _switchLayerComboBox;

        //internal ActiveLayerComboBox HLULayerComboBox
        //{
        //    get { return _switchLayerComboBox; }
        //    set
        //    {
        //        _switchLayerComboBox = value;

        //        //InitializeLayerComboBox();
        //    }
        //}

        //public static async void HandleComboBoxSelectionStatic(string selectedValue)
        //{
        //    //TODO: Switch active layer (if different).
        //    MessageBox.Show($"ComboBox selection changed: {selectedValue}");

        //    // Get the dockpane DAML id.
        //    DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        //    if (pane == null)
        //        return;

        //    // Get the ViewModel by casting the dockpane.
        //    ViewModelWindowMain vm = pane as ViewModelWindowMain;

        //    if (await vm._gisApp.IsHluLayer(selectedValue, true))
        //    {
        //        // Refresh the layer name
        //        vm.OnPropertyChanged(nameof(ActiveLayerName));

        //        // Get the GIS layer selection and warn the user if no
        //        // features are found
        //        //ReadMapSelection(true);
        //    }
        //}

        public async void HandleComboBoxSelection(string selectedValue)
        {
            // Create a new GIS functions object if necessary.
            if (_gisApp == null || _gisApp.MapName == null || MapView.Active is null || MapView.Active.Map.Name != _gisApp.MapName)
                _gisApp = new();

            // Switch the GIS layer.
            if (await _gisApp.IsHluLayer(selectedValue, true))
            {
                // Refresh the layer name
                OnPropertyChanged(nameof(ActiveLayerName));

                // Get the GIS layer selection and warn the user if no
                // features are found
                ReadMapSelection(true);
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

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class WindowMain_ShowButton : Button
    {
        protected override void OnClick()
        {
            // Show the dock pane.
            ViewModelWindowMain.Show();
        }
    }
}