// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs.
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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using HLU;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI
{
    internal class PaneHeader2ViewModel : PanelViewModelBase, INotifyPropertyChanged
    {
        #region Fields

        private readonly DockpaneMainViewModel _dockPane;

        private bool _countsLoaded = false;
        private bool _tablesLoaded = false;
        private bool _localTableLoaded = false;
        private bool _remoteTableLoaded = false;
        private bool _tablesIdentical = true;
        private bool _syncErrors = false;
        private bool _compareHasRun = false;

        private string _logFilePath;
        private string _logFile;

        // Server fields.
        private string _sdeFileName;
        private string _defaultSchema;
        private string _compareStoredProcedure;
        private string _updateStoredProcedure;
        private string _clearStoredProcedure;

        // Table fields.
        private string _localLayer;
        private string _remoteTableUp;
        private string _remoteTableDown;
        private string _remoteLayer;
        private string _keyColumn;
        private string _spatialColumn;

        private long _localFeatures;
        private long _localBlankKeys;
        private long _localDuplicates;
        private long _remoteFeatures;
        private long _remoteBlankKeys;
        private long _remoteDuplicates;

        private List<ResultSummary> _resultSummary;
        private List<ResultDetail> _resultDetail;

        private string _userID;

        private const string _displayName = "HLUTool";

        private readonly HLUToolConfig _toolConfig;
        private MapFunctions _mapFunctions;
        private SQLServerFunctions _sqlFunctions;

        #endregion Fields

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
        }

        #endregion ViewModelBase Members

        #region Creator

        /// <summary>
        /// Set the global variables.
        /// </summary>
        /// <param name="dockPane"></param>
        /// <param name="toolConfig"></param>
        public PaneHeader2ViewModel(DockpaneMainViewModel dockPane, HLUToolConfig toolConfig)
        {
            _dockPane = dockPane;

            // Return if no config object passed.
            if (toolConfig == null) return;

            // Set the config object.
            _toolConfig = toolConfig;

            InitializeComponent();
        }

        /// <summary>
        /// Initialise the sync pane.
        /// </summary>
        private void InitializeComponent()
        {
            // Set the SDE file name.
            _sdeFileName = _toolConfig.SDEFile;

            // Create a new map functions object.
            _mapFunctions = new();

            // Create a new SQL functions object.
            _sqlFunctions = new(_sdeFileName);

            // Get the relevant config file settings.
            _logFilePath = _toolConfig.LogFilePath;
            _defaultSchema = _toolConfig.DatabaseSchema;
            _compareStoredProcedure = _toolConfig.CompareStoredProcedure;
            _updateStoredProcedure = _toolConfig.UpdateStoredProcedure;
            _clearStoredProcedure = _toolConfig.ClearStoredProcedure;
            _localLayer = _toolConfig.LocalLayer;
            _remoteTableUp = _toolConfig.RemoteTableUp;
            _remoteTableDown = _toolConfig.RemoteTableDown;
            _remoteLayer = _toolConfig.RemoteLayer;
            _keyColumn = _toolConfig.KeyColumn;
            _spatialColumn = _toolConfig.SpatialColumn;

            // Clear the compare flag.
            _compareHasRun = false;
        }

        #endregion Creator

        #region Controls Enabled

        /// <summary>
        /// Is the tables counts list enabled?
        /// </summary>
        public bool TableCountsListEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_countsLoaded));
            }
        }

        /// <summary>
        /// Can the compare button be pressed?
        /// </summary>
        public bool CompareButtonEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_tablesLoaded));
            }
        }

        /// <summary>
        /// Is the list of result summary list enabled?
        /// </summary>
        public bool ResultSummaryListEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultSummaryList != null));
            }
        }

        /// <summary>
        /// Is the hidden zoom to result button enabled.
        /// </summary>
        public bool LoadResultsEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultSummaryList != null));
            }
        }

        /// <summary>
        /// Is the hidden zoom to result button enabled.
        /// </summary>
        public bool ZoomToDetailEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultSummaryList != null));
            }
        }

        /// <summary>
        /// Is the list of result detail list enabled?
        /// </summary>
        public bool ResultDetailListEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultDetailList != null));
            }
        }

        /// <summary>
        /// Can the run button be pressed?
        /// </summary>
        public bool RunButtonEnabled
        {
            get
            {
                return ((_compareHasRun)
                    && (!_tablesIdentical)
                    && (_dockPane.ProcessStatus == null));
            }
        }

        #endregion Controls Enabled

        #region Controls Visibility

        /// <summary>
        /// Are the Result lists visible.
        /// </summary>
        public Visibility ResultVisibility
        {
            get
            {
                if ((_compareHasRun == false))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Are the Options buttons visible.
        /// </summary>
        public Visibility OptionsVisibility
        {
            get
            {
                if ((_compareHasRun == false))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Is the ResultDetailList expand button visible.
        /// </summary>
        public Visibility ResultDetailListExpandButtonVisibility
        {
            get
            {
                if ((_resultDetailList == null) || (_resultDetailList.Count < 19))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        #endregion Controls Visibility

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

        #region Compare Command

        private ICommand _compareCommand;

        /// <summary>
        /// Create Compare button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand CompareCommand
        {
            get
            {
                if (_compareCommand == null)
                {
                    Action<object> compareAction = new(CompareCommandClick);
                    _compareCommand = new RelayCommand(compareAction, param => CompareButtonEnabled);
                }

                return _compareCommand;
            }
        }

        /// <summary>
        /// Handles event when Compare button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CompareCommandClick(object param)
        {
            // Run the compare (don't wait).
            CompareChangesAsync();
        }

        #endregion Compare Command

        #region Run Command

        /// <summary>
        /// Run the sync.
        /// </summary>
        public async void RunSyncAsync()
        {
            // Check results before sync.
            bool warning = false;
            foreach (ResultSummary resultSummary in _resultSummaryList)
            {
                string type = resultSummary.Type.ToLower();
                warning = type switch
                {
                    "empty" => true,
                    "error" => true,
                    "orphan" => true,
                    _ => warning
                };
            }

            // Check if user wants to continue despite warning.
            if (warning)
            {
                if (MessageBox.Show("Warning:\r\nEmpty, error or orphan features will not be updated.\r\n\r\nContinue sync?", _displayName, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;
            }

            // Clear any messages.
            ClearMessage();

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            _dockPane.ProgressUpdate("Applying updates to remote table", -1, -1);

            // Process the sync.
            bool success;
            success = await ApplyChangesAsync();

            // Indicate that the sync process has completed (successfully or not).
            string message;
            string image;

            if (success)
            {
                message = "Process complete!";
                image = "Success";
            }
            else if (_syncErrors)
            {
                message = "Process ended with errors!";
                image = "Error";
            }
            else
            {
                message = "Process ended unexpectedly!";
                image = "Error";
            }

            // Hide progress update (to allow the details to reload).
            _dockPane.ProgressUpdate(null, -1, -1);

            // Finish up now the sync has stopped (successfully or not).
            StopSync(message, image);

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();
        }

        #endregion Run Command

        #region Properties

        private ObservableCollection<TableCount> _tableCountsList;

        public ObservableCollection<TableCount> TableCountsList
        {
            get
            {
                return _tableCountsList;
            }
            set
            {
                _tableCountsList = value;
            }
        }

        /// <summary>
        /// Get the image for the TableCountsListRefresh button.
        /// </summary>
        public static ImageSource ButtonTableCountsListRefreshImg
        {
            get
            {
                var imageSource = System.Windows.Application.Current.Resources["GenericRefresh16"] as ImageSource;
                return imageSource;
            }
        }

        /// <summary>
        /// The list of result summary items.
        /// </summary>
        private ObservableCollection<ResultSummary> _resultSummaryList;

        /// <summary>
        /// Get/Set the list of results summary.
        /// </summary>
        public ObservableCollection<ResultSummary> ResultSummaryList
        {
            get
            {
                return _resultSummaryList;
            }
            set
            {
                _resultSummaryList = value;
            }
        }

        /// <summary>
        /// Triggered when the selection in the summary result list changes.
        /// </summary>
        public int ResultSummaryListSelectedIndex
        {
            set
            {
                // Skip if no item is selected.
                if (value == -1)
                    return;

                // Get the selected item.
                ResultSummary resultSummary = _resultSummaryList[value];

                // Get the selected result type.
                string type = resultSummary.Type;

                // Get the list of result details for the selected result type.
                List<ResultDetail> resultDetail = _resultDetail.Where(r => r.Type == type).ToList();

                // Load the result details for selected type.
                ResultDetailList = new ObservableCollection<ResultDetail>(resultDetail);

                // Clear the selected index for the result details list.
                if (_resultDetailList != null)
                {
                    foreach (ResultDetail detail in _resultDetailList)
                    {
                        detail.IsSelected = false;
                    }
                }

                OnPropertyChanged(nameof(ResultDetailListHeight));

                // Clear any messages.
                ClearMessage();
            }
        }

        /// <summary>
        /// The list of SQL tables.
        /// </summary>
        private ObservableCollection<ResultDetail> _resultDetailList;

        /// <summary>
        /// Get the list of SQL tables.
        /// </summary>
        public ObservableCollection<ResultDetail> ResultDetailList
        {
            get
            {
                return _resultDetailList;
            }
            set
            {
                _resultDetailList = value;
                OnPropertyChanged(nameof(ResultDetailList));
                OnPropertyChanged(nameof(ResultDetailListExpandButtonVisibility));
            }
        }

        int _resultDetailListSelectedIndex;

        public int ResultDetailListSelectedIndex
        {
            set
            {
                _resultDetailListSelectedIndex = value;
            }
        }

        private double _resultDetailListHeight = Double.NaN;

        /// <summary>
        /// Get the height of the result details list.
        /// </summary>
        public double ResultDetailListHeight
        {
            get
            {
                if ((_resultDetailList == null) || (_resultDetailList.Count < 15))
                    return 310;
                else
                    return _resultDetailListHeight;
            }
        }

        /// <summary>
        /// Get the content of the result details list expand button.
        /// </summary>
        public string ResultDetailListExpandButtonContent
        {
            get
            {
                if (!Double.IsFinite(_resultDetailListHeight))
                    return "-";
                else
                    return "+";
            }
        }

        private bool _clearLogFile;

        /// <summary>
        /// Is the log file to be cleared before running the sync?
        /// </summary>
        public bool ClearLogFile
        {
            get
            {
                return _clearLogFile;
            }
            set
            {
                _clearLogFile = value;
            }
        }

        private bool _openLogFile;

        /// <summary>
        /// Is the log file to be opened after running the sync?
        /// </summary>
        public bool OpenLogFile
        {
            get
            {
                return _openLogFile;
            }
            set
            {
                _openLogFile = value;
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Update the fields and buttons in the form.
        /// </summary>
        private void UpdateFormControls()
        {
            UpdateFormFields();

            // Check if the run button is now enabled/disabled.
            _dockPane.CheckRunButton();
        }

        /// <summary>
        /// Update the fields in the form.
        /// </summary>
        private void UpdateFormFields()
        {
            OnPropertyChanged(nameof(CompareButtonEnabled));
            OnPropertyChanged(nameof(ResultSummaryList));
            OnPropertyChanged(nameof(ResultSummaryListEnabled));
            OnPropertyChanged(nameof(ResultDetailList));
            OnPropertyChanged(nameof(ResultDetailListEnabled));
            OnPropertyChanged(nameof(ResultVisibility));
            OnPropertyChanged(nameof(OptionsVisibility));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(HasMessage));
        }

        /// <summary>
        /// Set all of the form fields to their default values.
        /// </summary>
        /// <param name="reset"></param>
        /// <returns></returns>
        public async Task ResetFormAsync(bool reset)
        {
            // Clear the result summary selections first (to avoid selections being retained).
            if (_resultSummaryList != null)
            {
                foreach (ResultSummary layer in _resultSummaryList)
                {
                    layer.IsSelected = false;
                }
            }

            // Clear the result detail selections first (to avoid selections being retained).
            if (_resultDetailList != null)
            {
                foreach (ResultDetail layer in _resultDetailList)
                {
                    layer.IsSelected = false;
                }
            }

            // Log file.
            ClearLogFile = _toolConfig.DefaultClearLogFile;
            OpenLogFile = _toolConfig.DefaultOpenLogFile;

            // Reload the local and remote table counts.
            await LoadTableCountsAsync(reset, false);
        }

        /// <summary>
        /// Load the local and remote table counts.
        /// </summary>
        /// <param name="reset"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task LoadTableCountsAsync(bool reset, bool message)
        {
            //// If already processing then exit.
            //if (_dockPane.ProcessStatus != null)
            //    return;

            // Reset the table counts loaded flag.
            _countsLoaded = false;

            // Reset the compare has run flag.
            _compareHasRun = false;

            // Reset the tables loaded flag.
            _tablesLoaded = false;

            // Clear the local and remote feature counts.
            TableCountsList = [];

            // Update the table counts list.
            OnPropertyChanged(nameof(TableCountsList));

            // Clear the list of result summary and details.
            ClearFormLists();

            _dockPane.FormLoading = true;
            if (reset)
                _dockPane.ProgressUpdate("Refreshing table details...");
            else
                _dockPane.ProgressUpdate("Loading table details...");

            // Clear any messages.
            ClearMessage();

            // Update the fields and buttons in the form.
            UpdateFormControls();

            // Load the local table details (don't wait).
            Task<string> localDetailsTask = LoadLocalDetailsAsync();

            // Load the remote table details (don't wait).
            Task<string> remoteDetailsTask = LoadRemoteDetailsAsync();

            // Wait for all of the lists to load.
            await Task.WhenAll(localDetailsTask, remoteDetailsTask);

            // Hide progress update.
            _dockPane.ProgressUpdate(null, -1, -1);

            // Set flag to show the tables have been loaded.
            if (_localTableLoaded && _remoteTableLoaded)
                _tablesLoaded = true;

            // Indicate the refresh has finished.
            _dockPane.FormLoading = false;

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Show any message from loading the local layer details.
            if (localDetailsTask.Result != null)
            {
                ShowMessage(localDetailsTask.Result, MessageType.Warning);
                if (message)
                    MessageBox.Show(localDetailsTask.Result, _displayName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show any message from loading the remote table details.
            if (remoteDetailsTask.Result != null)
            {
                ShowMessage(remoteDetailsTask.Result, MessageType.Warning);
                if (message)
                    MessageBox.Show(remoteDetailsTask.Result, _displayName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set the local layer tool tip.
            string localToolTip = string.Format("{0} feature{1} with null or blank keys, {2} feature{3} with duplicate keys", _localBlankKeys, _localBlankKeys == 1 ? "" : "s", _localDuplicates, _localDuplicates == 1 ? "" : "s");

            // Add the local details to the table summary.
            TableCount tableCount = new()
            {
                Table = "Local",
                Count = _localFeatures,
                Errors = _localBlankKeys,
                Duplicates = _localDuplicates,
                ToolTip = localToolTip
            };
            _tableCountsList.Add(tableCount);

            // Set the remote table tool tip.
            string remoteToolTip = string.Format("{0} feature{1} with null or blank keys, {2} feature{3} with duplicate keys", _remoteBlankKeys, _remoteBlankKeys == 1 ? "" : "s", _remoteDuplicates, _remoteDuplicates == 1 ? "" : "s");

            // Add the remote details to the table summary.
            tableCount = new()
            {
                Table = "Remote",
                Count = _remoteFeatures,
                Errors = _remoteBlankKeys,
                Duplicates = _remoteDuplicates,
                ToolTip = remoteToolTip
            };
            _tableCountsList.Add(tableCount);

            // Set table counts loaded flag.
            _countsLoaded = true;

            // Update the table counts list and refresh button.
            OnPropertyChanged(nameof(TableCountsList));
            OnPropertyChanged(nameof(TableCountsListEnabled));

            // Check the local and remote feature counts and
            // report any null, blank or duplicate keys.
            if (_localFeatures < 0)
                ShowMessage("Error counting local features", MessageType.Warning);
            else if (_remoteFeatures < 0)
                ShowMessage("Error counting remote features", MessageType.Warning);
            else if ((_localBlankKeys > 0) || (_localDuplicates > 0) || (_remoteBlankKeys > 0) || (_remoteDuplicates > 0))
                ShowMessage(string.Format("Feature(s) with null, blank or duplicate keys"), MessageType.Warning);
        }

        /// <summary>
        /// Load the local table details.
        /// </summary>
        /// <returns>string: error message</returns>
        public async Task<string> LoadLocalDetailsAsync()
        {
            // Set flag to show the table has not loaded yet.
            _localTableLoaded = false;

            if (_mapFunctions == null || _mapFunctions.MapName == null || MapView.Active.Map.Name != _mapFunctions.MapName)
            {
                // Create a new map functions object.
                _mapFunctions = new();
            }

            // Check if there is an active map.
            bool mapOpen = _mapFunctions.MapName != null;

            if (!mapOpen)
                return "No active map open.";

            // Find the map layer by name.
            FeatureLayer LocalFeatureLayer = _mapFunctions.FindLayer(_localLayer);

            // Check the local layer is loaded.
            if (LocalFeatureLayer == null)
                return "Local layer '" + _localLayer + "' not found.";

            // Get the full local layer path (in case it's nested in one or more groups).
            string localLayerPath = _mapFunctions.GetLayerPath(_localLayer);

            // Check the spatial column is in the layer.
            if (!await _mapFunctions.FieldExistsAsync(localLayerPath, _spatialColumn))
                return string.Format("Key column '{0}' not found in local layer '{1}'", _keyColumn, _localLayer);

            // Check the key column is in the layer.
            if (!await _mapFunctions.FieldExistsAsync(localLayerPath, _keyColumn))
                return string.Format("Spatial column '{0}' not found in local layer '{1}'", _keyColumn, _localLayer);

            // Count the number of features in the layer.
            _localFeatures = await ArcGISFunctions.GetFeaturesCountAsync(LocalFeatureLayer);

            // Count the number of features with null/blank keys.
            string whereClause = "(" + _keyColumn + " IS NULL OR " + _keyColumn + " = '')";
            _localBlankKeys = await ArcGISFunctions.GetFeaturesCountAsync(LocalFeatureLayer, whereClause);

            // Count the number of features with duplicate keys.
            whereClause = "(" + _keyColumn + " IS NOT NULL AND " + _keyColumn + " <> '')";
            _localDuplicates = await ArcGISFunctions.GetDuplicateFeaturesCountAsync(LocalFeatureLayer, _keyColumn, whereClause);

            // Set flag to show the table has loaded.
            _localTableLoaded = true;

            return null;
        }

        /// <summary>
        /// Load the remote table details.
        /// </summary>
        /// <returns>string: error message</returns>
        public async Task<string> LoadRemoteDetailsAsync()
        {
            // Set flag to show the table has not loaded yet.
            _remoteTableLoaded = false;

            // Check if there is an active map. There's no point loading the remote details
            // when we can't load the local details.
            if (_mapFunctions.MapName == null)
                return null;

            // Check if the feature class exists.
            if (!await _sqlFunctions.FCExistsAsync(_remoteTableDown))
                return "Remote table '" + _remoteTableDown + "' not found.";

            // Check the spatial column is in the table.
            if (!await _sqlFunctions.FieldExistsAsync(_remoteTableDown, _spatialColumn))
                return string.Format("Key column '{0}' not found in remote table '{1}'", _keyColumn, _remoteTableDown);

            // Check the key column is in the table.
            if (!await _sqlFunctions.FieldExistsAsync(_remoteTableDown, _keyColumn))
                return string.Format("Spatial column '{0}' not found in remote table '{1}'", _keyColumn, _remoteTableDown);

            // Count the number of features in the remote table.
            _remoteFeatures = await _sqlFunctions.GetFeaturesCountAsync(_remoteTableDown);

            // Count the number of features with null/blank keys.
            string whereClause = "(" + _keyColumn + " IS NULL OR " + _keyColumn + " = '')";
            _remoteBlankKeys = await _sqlFunctions.GetFeaturesCountAsync(_remoteTableDown, whereClause);

            // Count the number of features with duplicate keys.
            whereClause = "(" + _keyColumn + " IS NOT NULL AND " + _keyColumn + " <> '')";
            _remoteDuplicates = await _sqlFunctions.GetDuplicateFeaturesCountAsync(_remoteTableDown, _keyColumn, whereClause);

            // Set flag to show the table has loaded.
            _remoteTableLoaded = true;

            return null;
        }

        /// <summary>
        /// Clear the list of result summary and details.
        /// </summary>
        /// <returns></returns>
        public void ClearFormLists()
        {
            // If not already processing.
            if (_dockPane.ProcessStatus == null)
            {
                // Clear the list of result summary.
                ResultSummaryList = [];

                // Clear the list of result detail.
                ResultDetailList = [];

                // Update the fields and buttons in the form.
                UpdateFormControls();
            }
        }

        /// <summary>
        /// Compare the local layer and remote table before updating.
        /// </summary>
        /// <param name="uploadLayer"></param>
        /// <returns>bool</returns>
        private async Task<bool> CompareChangesAsync()
        {
            // Clear any messages.
            ClearMessage();

            // Reset the check has run flag.
            _compareHasRun = false;

            // Indicate compare has started.
            _dockPane.CompareRunning = true;

            // Expand the detail list (ready to be resized later).
            _resultDetailListHeight = Double.NaN;

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Peform the check between the local layer and remote table.
            await PerformCheckAsync(true);

            // Set the check has run flag.
            _compareHasRun = true;

            // Indicate compare has finished.
            _dockPane.CompareRunning = false;

            _dockPane.ProgressUpdate(null, -1, -1);

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Force result detail list height to reset.
            ResultDetailListExpandCommandClick(null);

            return true;
        }

        /// <summary>
        /// Peform the check between the local layer and remote table.
        /// </summary>
        /// <param name="uploadLayer"></param>
        /// <returns>bool</returns>
        private async Task<bool> PerformCheckAsync(bool uploadLayer)
        {
            // Reset the result summary and detail lists.
            ResultSummaryList = [];
            ResultDetailList = [];

            // Clear the local layer features selection.
            await _mapFunctions.ClearLayerSelectionAsync(_localLayer);

            // Set the full remote table path.
            string remoteTablePath = _sdeFileName + @"\" + _defaultSchema + "." + _remoteTableUp;

            if (uploadLayer)
            {
                _dockPane.ProgressUpdate("Uploading local layer to server", -1, -1);

                //FileFunctions.WriteLine(_logFile, "Uploading local layer to server ...");

                // Get the full local layer path (in case it's nested in one or more groups).
                string localLayerPath = _mapFunctions.GetLayerPath(_localLayer);

                if (!await ArcGISFunctions.CopyFeaturesAsync(localLayerPath, remoteTablePath + "_TEMP", false))
                {
                    ShowMessage("Error: Uploading local layer '" + _localLayer + "'", MessageType.Warning);
                    //FileFunctions.WriteLine(_logFile, "Error: Uploading local layer.");
                    //_syncErrors = true;
                    return false;
                }

                //FileFunctions.WriteLine(_logFile, "Upload to server complete.");
            }

            // Check if the remote map layer is loaded.
            if (_mapFunctions.FindLayer(_remoteLayer) == null)
            {
                //FileFunctions.WriteLine(_logFile, "Adding remote layer to map.");

                // Get the position of the local layer in the map.
                int localIndex = _mapFunctions.FindLayerIndex(_localLayer) + 1;

                // Set the full remote table path.
                string remoteLayerPath = _sdeFileName + @"\" + _defaultSchema + "." + _remoteTableDown;

                // Add the remote table to the map below the local table.
                if (!await _mapFunctions.AddLayerToMapAsync(remoteLayerPath, localIndex, _remoteLayer))
                {
                    ShowMessage("Error: Adding remote layer to map", MessageType.Warning);
                    //FileFunctions.WriteLine(_logFile, "Error: Adding remote layer to map.");
                    //_syncErrors = true;
                    return false;
                }
            }

            _dockPane.ProgressUpdate("Comparing local layer and remote table", -1, -1);

            //FileFunctions.WriteLine(_logFile, "Comparing local layer and remote table ...");

            string resultsTable = _remoteTableUp + "_SYNC";

            // Delete the results table before we start.
            if (await _sqlFunctions.TableExistsAsync(resultsTable))
                await _sqlFunctions.DeleteTableAsync(resultsTable);

            // Execute the stored procedure to check the local layer and remote table.
            long resultsCount = await PerformSQLCheckAsync(_defaultSchema, _remoteTableUp, _keyColumn, _spatialColumn, resultsTable);

            // Check the results table has been created.
            if (resultsCount < 0)
            {
                ShowMessage("Error: Comparing local layer and remote table", MessageType.Warning);
                //FileFunctions.WriteLine(_logFile, "Error: Comparing local layer and remote table.");
                //_syncErrors = true;
                return false;
            }

            //FileFunctions.WriteLine(_logFile, "Comparing of local layer and remote table complete.");

            _dockPane.ProgressUpdate("Loading results of comparison", -1, -1);

            // Get all of the sync results.
            string typeColumn = "Type";
            string orderColumn = "Order";
            string descColumn = "Desc";
            string newKeyColumn = "KeyNew";
            string oldKeyColumn = "KeyOld";
            string newAreaColumn = "AreaNew";
            string oldAreaColumn = "AreaOld";
            _resultDetail = await _sqlFunctions.GetSyncResultsAsync(resultsTable, typeColumn, orderColumn, descColumn, newKeyColumn, oldKeyColumn, newAreaColumn, oldAreaColumn);

            // If there are no results then the tables are identical.
            if (_resultDetail == null || _resultDetail.Count == 0)
            {
                // Set identical flag.
                _tablesIdentical = true;

                ShowMessage("Local layer and remote table are identical", MessageType.Information);
                return true;
            }

            // Set identical flag.
            _tablesIdentical = false;

            // Get a summary of the results.
            _resultSummary = (from r in _resultDetail
                              group r by new { r.Type, r.Desc }
                              into grp
                              select new ResultSummary()
                              {
                                  Type = grp.Key.Type,
                                  Count = grp.Count(),
                                  Desc = grp.Key.Desc
                              }).ToList();

            // Set the list of result summary.
            ResultSummaryList = new ObservableCollection<ResultSummary>(_resultSummary);

            return true;
        }

        /// <summary>
        /// Apply the changes to the remote table.
        /// </summary>
        /// <returns>bool</returns>
        private async Task<bool> ApplyChangesAsync()
        {
            // Replace any illegal characters in the user name string.
            _userID = StringFunctions.StripIllegals(Environment.UserName, "_", false);

            // User ID should be something at least.
            if (string.IsNullOrEmpty(_userID))
                _userID = "Temp";

            // Set the destination log file path.
            _logFile = _logFilePath + @"\HLUTool_" + _userID + ".log";

            // Archive the log file (if it exists).
            if (ClearLogFile)
            {
                if (FileFunctions.FileExists(_logFile))
                {
                    // Get the last modified date/time for the current log file.
                    DateTime dateLogFile = File.GetLastWriteTime(_logFile);
                    string dateLastMod = dateLogFile.ToString("yyyy") + dateLogFile.ToString("MM") + dateLogFile.ToString("dd") + "_" +
                        dateLogFile.ToString("HH") + dateLogFile.ToString("mm") + dateLogFile.ToString("ss");

                    // Rename the current log file.
                    string logFileArchive = _logFilePath + @"\HLUTool_" + _userID + "_" + dateLastMod + ".log";
                    if (!FileFunctions.RenameFile(_logFile, logFileArchive))
                    {
                        MessageBox.Show("Error: Cannot rename log file. Please make sure it is not open in another window.", _displayName, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
            }

            // Create log file path.
            if (!FileFunctions.DirExists(_logFilePath))
            {
                try
                {
                    Directory.CreateDirectory(_logFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Cannot create directory " + _logFilePath + ". System error: " + ex.Message);
                    return false;
                }
            }

            FileFunctions.WriteLine(_logFile, "-----------------------------------------------------------------------");
            FileFunctions.WriteLine(_logFile, "Process started");
            FileFunctions.WriteLine(_logFile, "-----------------------------------------------------------------------");
            FileFunctions.WriteLine(_logFile, "");
            FileFunctions.WriteLine(_logFile, string.Format("{0:n0} features in local table.", _localFeatures));
            FileFunctions.WriteLine(_logFile, string.Format("{0:n0} features in remote table.", _remoteFeatures));
            FileFunctions.WriteLine(_logFile, "");

            // Check results before sync.
            bool warning = false;
            foreach (ResultSummary resultSummary in _resultSummaryList)
            {
                FileFunctions.WriteLine(_logFile, string.Format("{0:n0} {1} feature{2} where {3}.", resultSummary.Count, resultSummary.Type.ToLower(), resultSummary.Count == 1 ? "" : "s", resultSummary.Desc));

                string type = resultSummary.Type.ToLower();
                warning = type switch
                {
                    "empty" => true,
                    "error" => true,
                    "orphan" => true,
                    _ => warning
                };
            }
            FileFunctions.WriteLine(_logFile, "");

            // Include a warning in the log file.
            if (warning)
            {
                FileFunctions.WriteLine(_logFile, "Warning: Empty, error or orphan features will not be updated.");
                FileFunctions.WriteLine(_logFile, "");
            }

            // Execute the stored procedure to update the remote table.
            if (!await PerformSQLUpdateAsync(_defaultSchema, _remoteTableUp))
            {
                FileFunctions.WriteLine(_logFile, "Error: Applying updates to remote table.");
                _syncErrors = true;
                return false;
            }

            // Check the updated remote feature class exists.
            if (!await _sqlFunctions.FCExistsAsync(_remoteTableUp))
            {
                FileFunctions.WriteLine(_logFile, "Error: Updated remote table is not found.");
                _syncErrors = true;
                return false;
            }

            FileFunctions.WriteLine(_logFile, "Updates to remote table complete.");

            // Reload the local and remote table counts.
            await LoadTableCountsAsync(true, true);

            //// Re-run the check to show if anything is still outstanding.
            //await CompareChangesAsync(false);

            FileFunctions.WriteLine(_logFile, string.Format("{0:n0} features now in remote table.", _remoteFeatures));

            // Increment the progress value to the last step.
            _dockPane.ProgressUpdate("Cleaning up...", -1, -1);

            // Clean up after the sync.
            await CleanUpSyncAsync();

            return true;
        }

        /// <summary>
        /// Indicate that the sync process has stopped (either
        /// successfully or otherwise).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="image"></param>
        private void StopSync(string message, string image)
        {
            FileFunctions.WriteLine(_logFile, "---------------------------------------------------------------------------");
            FileFunctions.WriteLine(_logFile, message);
            FileFunctions.WriteLine(_logFile, "---------------------------------------------------------------------------");

            // Resume the map redrawing.
            _mapFunctions.PauseDrawing(false);

            // Indicate sync has finished.
            _dockPane.SyncRunning = false;
            _dockPane.ProgressUpdate(null, -1, -1);

            string imageSource = string.Format("pack://application:,,,/HLUTool;component/Images/{0}32.png", image);

            // Notify user of completion.
            Notification notification = new()
            {
                Title = _displayName,
                Severity = Notification.SeverityLevel.High,
                Message = message,
                ImageSource = new BitmapImage(new Uri(imageSource)) as ImageSource
            };
            FrameworkApplication.AddNotification(notification);

            // Open the log file (if required).
            if (OpenLogFile || _syncErrors)
                Process.Start("notepad.exe", _logFile);
        }

        /// <summary>
        /// Clean up after the sync has completed (successfully or not).
        /// </summary>
        /// <returns></returns>
        private async Task CleanUpSyncAsync()
        {
            FileFunctions.WriteLine(_logFile, "");

            // Clear the layer features selection.
            await _mapFunctions.ClearLayerSelectionAsync(_localLayer);

            // Clear the temporary remote tables.
            await ClearSQLTableAsync(_defaultSchema, _remoteTableUp);

            //// Remove the remote layer (if added).
            //if (_remoteLayerAdded)
            //{
            //    FileFunctions.WriteLine(_logFile, "Removing remote layer from map.");
            //    await ClearMapTablesAsync(_remoteLayer);
            //}
        }

        /// <summary>
        /// Perform the spatial selection via a stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="remoteTable"></param>
        /// <param name="keyColumn"></param>
        /// <param name="spatialColumn"></param>
        /// <param name="resultsTable"></param>
        /// <returns>long</returns>
        internal async Task<long> PerformSQLCheckAsync(string schema, string remoteTable, string keyColumn, string spatialColumn, string resultsTable)
        {
            bool success;
            long tableCount = -1;

            // Get the name of the stored procedure to execute selection in SQL Server.
            string storedProcedureName = _compareStoredProcedure;

            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", storedProcedureName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", resultsTable));
            sqlCmd.Append(string.Format(", '{0}'", remoteTable));
            sqlCmd.Append(string.Format(", '{0}'", keyColumn));
            sqlCmd.Append(string.Format(", '{0}'", spatialColumn));

            string sqlOutputTable = schema + '.' + resultsTable;

            try
            {
                //FileFunctions.WriteLine(_logFile, "Executing SQL comparison for '" + remoteTable + "' ...");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabaseAsync(sqlCmd.ToString());

                // Check if the output feature class exists.
                if (!await _sqlFunctions.TableExistsAsync(sqlOutputTable))
                    success = false;

                // Count the number of features in the output feature count.
                tableCount = await _sqlFunctions.GetTableRowCountAsync(sqlOutputTable);

                success = true;
            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Executing the stored procedure: " + ex.Message);
                success = false;
            }

            if (!success)
                return -1;

            //FileFunctions.WriteLine(_logFile, "SQL comparison complete.");

            return tableCount;
        }

        /// <summary>
        /// Perform the update of the remote table via a
        /// stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="remoteTable"></param>
        /// <returns>bool</returns>
        internal async Task<bool> PerformSQLUpdateAsync(string schema, string remoteTable)
        {
            // Get the name of the stored procedure to execute selection in SQL Server.
            string storedProcedureName = _updateStoredProcedure;

            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", storedProcedureName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", remoteTable));
            sqlCmd.Append(string.Format(", '{0}'", "SiteID"));
            sqlCmd.Append(string.Format(", '{0}'", "Shape"));

            try
            {
                FileFunctions.WriteLine(_logFile, "Applying updates to remote table ...");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabaseAsync(sqlCmd.ToString());
            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Executing the stored procedure: " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear the temporary SQL spatial table by running a stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        internal async Task<bool> ClearSQLTableAsync(string schema, string tableName)
        {
            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Get the name of the stored procedure to clear the
            // temporary tables in SQL Server.
            string clearSpatialSPName = _clearStoredProcedure;

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", clearSpatialSPName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", tableName));

            try
            {
                //FileFunctions.WriteLine(_logFile, "Deleting SQL temporary table");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabaseAsync(sqlCmd.ToString());
            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Deleting the SQL temporary tables: " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zoom to the feature matching the key field
        /// in a layer.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="keyValue"></param>
        /// <returns></returns>
        internal async Task ZoomToFeatureAsync(string layerName, string keyValue)
        {
            // Get the feature layer.
            FeatureLayer featurelayer = _mapFunctions.FindLayer(layerName);
            if (featurelayer == null)
            {
                ShowMessage("Layer '" + layerName + "' not found in map", MessageType.Warning);
                return;
            }

            // Create the search query.
            string searchClause;
            if (string.IsNullOrEmpty(keyValue))
                searchClause = _keyColumn + " IS NULL OR " + _keyColumn + " = ''";
            else
                searchClause = _keyColumn + " = '" + keyValue + "'";

            // Select the feature matching the key in the map (don't wait).
            if (!await _mapFunctions.SelectLayerByAttributesAsync(layerName, searchClause, SelectionCombinationMethod.New))
            {
                ShowMessage("Error funding feature in layer '" + layerName + "'", MessageType.Warning);
                return;
            }

            // Exit if no features selected.
            if (featurelayer.SelectionCount == 0)
            {
                ShowMessage("Feature not found in layer '" + layerName + "'", MessageType.Warning);
                return;
            }

            // Get the OID of the selected feature.
            IReadOnlyList<long> selectedOIDs = [];
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Get the oids for the selected features.
                    var gsSelection = featurelayer.GetSelection();
                    selectedOIDs = gsSelection.GetObjectIDs();
                });
            }
            catch
            {
                // Handle Exception.
                return;
            }

            // If any OIDs were found.
            if ((selectedOIDs == null) || (selectedOIDs.Count == 0))
                return;

            // If only one object was found.
            if (selectedOIDs.Count == 1)
            {
                // Zoom to the selected feature.
                long selectedOID = selectedOIDs[0];
                await _mapFunctions.ZoomToLayerAsync(layerName, selectedOID, 2, null);

                // Clear any messages.
                ClearMessage();
            }
            // Multiple objects were found.
            else
            {
                // Zoom to all of the selected features.
                await _mapFunctions.ZoomToLayerAsync(layerName, selectedOIDs);

                // Warn the user there are multiple features.
                ShowMessage("Multiple features found for selected feature", MessageType.Warning);
            }
        }

        #endregion Methods

        #region Tables Counts List

        private ICommand _refreshTableCountsCommand;

        /// <summary>
        /// Create the RefreshTableCountsList button command.
        /// </summary>
        public ICommand RefreshTableCountsCommand
        {
            get
            {
                if (_refreshTableCountsCommand == null)
                {
                    Action<object> refreshAction = new(RefreshTableCountsListCommandClick);
                    _refreshTableCountsCommand = new RelayCommand(refreshAction, param => TableCountsListEnabled);
                }
                return _refreshTableCountsCommand;
            }
        }

        /// <summary>
        /// Handles the event when the refresh table counts list button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private async void RefreshTableCountsListCommandClick(object param)
        {
            // Reload the local and remote table counts.
            await LoadTableCountsAsync(true, false);
        }

        #endregion Tables Counts List

        #region ResultDetailListExpand Command

        private ICommand _resultDetailListExpandCommand;

        /// <summary>
        /// Create ResultDetailList Expand button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand ResultDetailListExpandCommand
        {
            get
            {
                if (_resultDetailListExpandCommand == null)
                {
                    Action<object> expandResultDetailListAction = new(ResultDetailListExpandCommandClick);
                    _resultDetailListExpandCommand = new RelayCommand(expandResultDetailListAction, param => true);
                }
                return _resultDetailListExpandCommand;
            }
        }

        /// <summary>
        /// Handles event when ResultDetailListExpand button is pressed.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void ResultDetailListExpandCommandClick(object param)
        {
            if (!Double.IsFinite(_resultDetailListHeight))
                _resultDetailListHeight = 310;
            else
                _resultDetailListHeight = Double.NaN;

            OnPropertyChanged(nameof(ResultDetailListHeight));
            OnPropertyChanged(nameof(ResultDetailListExpandButtonContent));
        }

        #endregion ResultDetailListExpand Command

        #region ZoomToDetail Command

        private ICommand _zoomToDetailCommand;

        /// <summary>
        /// Create the ZoomToDetail button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand ZoomToDetailCommand
        {
            get
            {
                if (_zoomToDetailCommand == null)
                {
                    Action<object> zoomToDetailAction = new(ZoomToDetailCommandClick);
                    _zoomToDetailCommand = new RelayCommand(zoomToDetailAction, param => ZoomToDetailEnabled);
                }
                return _zoomToDetailCommand;
            }
        }

        /// <summary>
        /// Handles the event when the ZoomToDetail button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void ZoomToDetailCommandClick(object param)
        {
            // Zoom to the feature for the selected result detail item (don't wait).
            ZoomToDetailAsync(_resultDetailListSelectedIndex);
        }

        /// <summary>
        /// Zoom to the detail feature for the selected result item.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public async Task ZoomToDetailAsync(int index)
        {
            // Skip if no item is selected.
            if (index == -1)
                return;

            // Clear any messages.
            ClearMessage();

            // Get the selected item.
            ResultDetail resultDetail = _resultDetailList[index];

            // Get the type of the selected result.
            string resultType = resultDetail.Type;

            // Clear the local layer features selection (don't wait).
            await _mapFunctions.ClearLayerSelectionAsync(_localLayer);

            // Clear the remote layer features selection (don't wait).
            await _mapFunctions.ClearLayerSelectionAsync(_remoteLayer);

            // If the result type is an empty geometry.
            if (resultType == "Empty")
            {
                return;
            }
            //If the result type is a deleted feature.
            else if (resultType == "Deleted")
            {
                // Get the key of the selected result.
                string oldKey = resultDetail.OldKey?.Trim();

                // Zoom to the selected result.
                await ZoomToFeatureAsync(_remoteLayer, oldKey);
            }
            else
            {
                // Get the key of the selected result.
                string newKey = resultDetail.NewKey?.Trim();

                // Zoom to the selected result.
                await ZoomToFeatureAsync(_localLayer, newKey);
            }
        }

        #endregion ZoomToDetail Command

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

    #region TableCount Class

    /// <summary>
    /// TableCount to display.
    /// </summary>
    public class TableCount : INotifyPropertyChanged
    {
        #region Fields

        public string Table { get; set; }

        public long Count { get; set; }

        public long Errors { get; set; }

        public long Duplicates { get; set; }

        public string ToolTip { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        #endregion Fields

        #region Creator

        public TableCount()
        {
            // constructor takes no arguments.
        }

        #endregion Creator

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            //VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #endregion TableCount Class

    #region ResultSummary Class

    /// <summary>
    /// ResultSummary to display.
    /// </summary>
    public class ResultSummary : INotifyPropertyChanged
    {
        #region Fields

        public string Type { get; set; }

        public long Count { get; set; }

        public string Desc { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        #endregion Fields

        #region Creator

        public ResultSummary()
        {
            // constructor takes no arguments.
        }

        public ResultSummary(string summaryType)
        {
            Type = summaryType;
        }

        public ResultSummary(string summaryType, int summaryCount)
        {
            Type = summaryType;
            Count = summaryCount;
        }

        #endregion Creator

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            //VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #endregion ResultSummary Class

    #region ResultDetail Class

    /// <summary>
    /// ResultDetail to display.
    /// </summary>
    public class ResultDetail : INotifyPropertyChanged
    {
        #region Fields

        public string Type { get; set; }

        public int Order { get; set; }

        public string Desc { get; set; }

        public string NewKey { get; set; }

        public string OldKey { get; set; }

        public string NewArea { get; set; }

        public string OldArea { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        #endregion Fields

        #region Creator

        public ResultDetail()
        {
            // constructor takes no arguments.
        }

        public ResultDetail(string type)
        {
            Type = type;
        }

        public ResultDetail(string type, string newKey, string oldKey, float newArea, float oldArea)
        {
            Type = type;
            NewKey = newKey;
            OldKey = oldKey;
            NewArea = newArea.ToString();
            OldArea = oldArea.ToString();
        }

        #endregion Creator

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            //VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #endregion ResultDetail Class
}