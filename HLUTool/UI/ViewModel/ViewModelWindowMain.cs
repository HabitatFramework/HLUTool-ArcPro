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
    internal partial class ViewModelWindowMain : PanelViewModelBase, INotifyPropertyChanged
    {

        #region Fields

        private ViewModelWindowMain _dockPane;

        private bool _mapEventsSubscribed;
        private bool _projectClosedEventsSubscribed;

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

        //TODO: Static variables
        // Static constructor
        //static ViewModelWindowMain()
        //{
        //    _historyGeometry1ColumnName = Settings.Default.HistoryGeometry1ColumnName;
        //    _historyGeometry2ColumnName = Settings.Default.HistoryGeometry2ColumnName;
        //    LutDescriptionFieldName = Settings.Default.LutDescriptionFieldName;
        //    LutDescriptionFieldOrdinal = Settings.Default.LutDescriptionFieldOrdinal;
        //    LutSourceFieldName = Settings.Default.LutSourceFieldName;
        //    LutSourceFieldOrdinal = Settings.Default.LutSourceFieldOrdinal;
        //    LutUserFieldName = Settings.Default.LutUserFieldName;
        //    LutUserFieldOrdinal = Settings.Default.LutUserFieldOrdinal;
        //    IncidPageSize = Settings.Default.IncidTablePageSize;
        //}

        /// <summary>
        /// Set the global variables.
        /// </summary>
        protected ViewModelWindowMain()
        {
            InitializeComponentAsync();
        }

        /// <summary>
        /// Initialise the DockPane components.
        /// </summary>
        public async void InitializeComponentAsync()
        {
            _dockPane = this;
            _initialised = false;
            _inError = false;

            // Indicate that the dockpane has been initialised.
            _initialised = true;
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

            // If the ViewModel is uninitialised then initialise it.
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
                    ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
                }

                if (!_projectClosedEventsSubscribed)
                {
                    _projectClosedEventsSubscribed = true;

                    // Suscribe to project closed events.
                    ProjectClosedEvent.Subscribe(OnProjectClosed);
                }
            }
            else
            {
                if (_mapEventsSubscribed)
                {
                    _mapEventsSubscribed = false;

                    // Unsubscribe from map changed events.
                    ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
                }
            }

            base.OnShow(isVisible);
        }

        #endregion ViewModelBase Members

        #region Controls Enabled

        /// <summary>
        /// Can the Run button be pressed?
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool RunButtonEnabled
        {
            get
            {
                //TODO: UI
                return true;
            }
        }

        public void CheckRunButton()
        {
            OnPropertyChanged(nameof(RunButtonEnabled));
        }

        #endregion Controls Enabled

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

        private bool _compareRunning;

        /// <summary>
        /// Is the compare running?
        /// </summary>
        public bool CompareRunning
        {
            get { return _compareRunning; }
            set { _compareRunning = value; }
        }

        private bool _syncRunning;

        /// <summary>
        /// Is the sync running?
        /// </summary>
        public bool SyncRunning
        {
            get { return _syncRunning; }
            set { _syncRunning = value; }
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

        /// <summary>
        /// Get the image for the Run button.
        /// </summary>
        public static ImageSource ButtonRunImg
        {
            get
            {
                var imageSource = Application.Current.Resources["GenericRun16"] as ImageSource;
                return imageSource;
            }
        }

        #endregion Properties

        #region Active Map View

        private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs obj)
        {
            if (MapView.Active == null)
            {
                DockpaneVisibility = Visibility.Hidden;

                // Clear the form lists.
                //_paneH2VM?.ClearFormLists();
            }
            else
            {
                DockpaneVisibility = Visibility.Visible;


                //TODO: UI
                // Do something when the active map view changes


                // Save the active map view.
                _activeMapView = MapView.Active;
            }
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
        }

        #endregion Active Map View

        #region Run Command

        private ICommand _runCommand;

        /// <summary>
        /// Create Run button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand RunCommand
        {
            get
            {
                if (_runCommand == null)
                {
                    Action<object> runAction = new(RunCommandClick);
                    _runCommand = new RelayCommand(runAction, param => RunButtonEnabled);
                }

                return _runCommand;
            }
        }

        /// <summary>
        /// Handles event when Run button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void RunCommandClick(object param)
        {
            //TODO: UI
            // Do something when the run button is clicked.
        }

        #endregion Run Command

    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class WindowMain_ShowButton : Button
    {
        protected override void OnClick()
        {
            //string uri = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Show the dock pane.
            ViewModelWindowMain.Show();
        }
    }
}