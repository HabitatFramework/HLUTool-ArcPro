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

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using DataTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI
{
    /// <summary>
    /// Build the DockPane.
    /// </summary>
    internal class DockpaneMainViewModel : DockPane, INotifyPropertyChanged
    {
        #region Fields

        private DockpaneMainViewModel _dockPane;

        private PaneHeader1ViewModel _paneH1VM;
        private PaneHeader2ViewModel _paneH2VM;

        private bool _mapEventsSubscribed;
        private bool _projectClosedEventsSubscribed;

        private MapView _activeMapView;

        #endregion Fields

        #region ViewModelBase Members

        /// <summary>
        /// Set the global variables.
        /// </summary>
        protected DockpaneMainViewModel()
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

            // Setup the tab controls.
            PrimaryMenuList.Clear();

            PrimaryMenuList.Add(new TabControl() { Text = "Profile", Tooltip = "Select XML profile" });
            PrimaryMenuList.Add(new TabControl() { Text = "Sync", Tooltip = "Run data sync" });

            // Load the default XML profile (or let the user choose a profile.
            _paneH1VM = new PaneHeader1ViewModel(_dockPane);

            // If the profile was in error.
            if (_paneH1VM.XMLError)
            {
                _inError = true;
                return;
            }

            // If the default (and only) profile was loaded.
            if (_paneH1VM.XMLLoaded)
            {
                // Initialise the sync pane.
                if (!await InitialiseSyncPaneAsync(false))
                    return;

                // Select the profile tab.
                SelectedPanelHeaderIndex = 1;
            }
            else
            {
                // Select the sync tab.
                SelectedPanelHeaderIndex = 0;
            }

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
            DockpaneMainViewModel vm = pane as DockpaneMainViewModel;

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
            //if (MapView.Active == null)
            //    DockpaneVisibility = Visibility.Hidden;

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
                if (_paneH2VM == null)
                    return false;

                return (_paneH2VM.RunButtonEnabled);
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
        private const string _dockPaneID = "DataSync_UI_DockpaneMain";

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
                Process.Start(new ProcessStartInfo
                {
                    FileName = _helpURL,
                    UseShellExecute = true
                });
            }
        }

        private readonly List<TabControl> _primaryMenuList = [];

        /// <summary>
        /// Get the list of dock panes.
        /// </summary>
        public List<TabControl> PrimaryMenuList
        {
            get { return _primaryMenuList; }
        }

        private int _selectedPanelHeaderIndex = 0;

        /// <summary>
        /// Get/Set the active pane.
        /// </summary>
        public int SelectedPanelHeaderIndex
        {
            get { return _selectedPanelHeaderIndex; }
            set
            {
                _selectedPanelHeaderIndex = value;
                OnPropertyChanged(nameof(SelectedPanelHeaderIndex));

                if (_selectedPanelHeaderIndex == 0)
                    CurrentPage = _paneH1VM;
                if (_selectedPanelHeaderIndex == 1)
                    CurrentPage = _paneH2VM;
            }
        }

        private PanelViewModelBase _currentPage;

        /// <summary>
        /// The currently active DockPane.
        /// </summary>
        public PanelViewModelBase CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
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

        #region Methods

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

                // Reload the local and remote table counts (don't wait).
                if (MapView.Active != _activeMapView)
                    _paneH2VM?.LoadTableCountsAsync(true, false);

                // Save the active map view.
                _activeMapView = MapView.Active;
            }
        }

        private void OnProjectClosed(ProjectEventArgs obj)
        {
            if (MapView.Active == null)
            {
                DockpaneVisibility = Visibility.Hidden;

                // Clear the form lists.
                _paneH2VM?.ClearFormLists();
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
        /// Initialise the sync pane.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> InitialiseSyncPaneAsync(bool message)
        {
            _paneH2VM = new PaneHeader2ViewModel(_dockPane, _paneH1VM.ToolConfig);

            string sdeFileName = _paneH1VM.ToolConfig.SDEFile;

            // Check if the SDE file exists.
            if (!FileFunctions.FileExists(sdeFileName))
            {
                if (message)
                    MessageBox.Show("SDE connection file '" + sdeFileName + "' not found.", "HLU", MessageBoxButton.OK, MessageBoxImage.Error);

                _paneH2VM = null;
                return false;
            }

            // Open the SQL Server geodatabase.
            try
            {
                if (!await SQLServerFunctions.CheckSDEConnection(sdeFileName))
                {
                    if (message)
                        MessageBox.Show("SDE connection file '" + sdeFileName + "' not valid.", "HLU", MessageBoxButton.OK, MessageBoxImage.Error);

                    _paneH2VM = null;
                    return false;
                }
            }
            catch (Exception)
            {
                if (message)
                    MessageBox.Show("SDE connection file '" + sdeFileName + "' not valid.", "HLU", MessageBoxButton.OK, MessageBoxImage.Error);

                _paneH2VM = null;
                return false;
            }

            // Load the form (don't wait for the response).
            _paneH2VM.ResetFormAsync(false);

            return true;
        }

        /// <summary>
        /// Reset the sync pane.
        /// </summary>
        public void ClearSyncPane()
        {
            _paneH2VM = null;
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
            DockpaneMainViewModel vm = pane as DockpaneMainViewModel;

            // Force the dockpane to be re-initialised next time it's shown.
            vm.Initialised = false;
        }

        public void RefreshPanel1Buttons()
        {
            _paneH1VM.RefreshButtons();
        }

        #endregion Methods

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
            // Run the sync (but don't wait).
            _paneH2VM.RunSyncAsync();
        }

        #endregion Run Command

        #region Debugging Aides

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (ThrowOnInvalidPropertyName)
                    throw new(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

        #endregion Debugging Aides

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public new event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockpaneMain_ShowButton : Button
    {
        protected override void OnClick()
        {
            //string uri = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Show the dock pane.
            DockpaneMainViewModel.Show();
        }
    }
}