// HLUTool is used to view and maintain habitat and land use GIS data.
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
using ArcGIS.Desktop.Framework;
using System.Text.RegularExpressions;
using HLU.Data.Model;
using HLU.UI.ViewModel;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.UserControls;
using System.Collections.ObjectModel;
using HLU.UI.View;
using System.Windows.Data;
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Navigation;
using Microsoft.IdentityModel.Tokens;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for the Options window.
    /// </summary>
    partial class ViewModelOptions : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;

        private AddInSettings _addInSettings;

        private ICommand _saveCommand;
        private ICommand _cancelCommand;
        private ICommand _browseSqlPathCommand;
        private ICommand _openHyperlinkCommand;

        private string _displayName = "Options";

        private HluDataSet.incid_mm_polygonsDataTable _incidMMPolygonsTable = new();
        private List<int> _gisIDColumnOrdinals;

        // Database options
        private int? _dbConnectionTimeout;
        private int? _incidTablePageSize;

        // Dates options
        private string _seasonSpring;
        private string _seasonSummer;
        private string _seasonAutumn;
        private string _seasonWinter;
        private string _vagueDateDelimiter;

        // Updates options
        private int _habitatSecondaryCodeValidation;
        private int _primarySecondaryCodeValidation;
        private int _qualityValidation;
        private int _potentialPriorityDetermQtyValidation;
        private int? _subsetUpdateAction;
        private string[] _clearIHSUpdateActions;
        private string _clearIHSUpdateAction;
        private string _secondaryCodeDelimiter;
        private bool _resetOSMMUpdatesStatus;

        // Bulk Update options
        private bool _bulkDeleteOrphanBapHabitats;
        private bool _bulkDeletePotentialBapHabitats;
        private bool _bulkDeleteIHSCodes;
        private bool _bulkDeleteSecondaryCodes;
        private bool _bulkCreateHistoryRecords;
        private string _bulkDeterminationQuality;
        private string _bulkInterpretationQuality;
        private int? _bulkOSMMSourceId;

        // User GIS options
        private int? _minAutoZoom;
        private int _maxAutoZoom;

        // User History options
        private SelectionList<string> _historyColumns;
        private int? _historyDisplayLastN;

        // User Interface options
        private bool _showGroupHeaders;
        private bool _showIHSTab;
        private bool _showSourceHabitatGroup;
        private bool _showHabitatSecondariesSuggested;
        private bool _showNVCCodes;
        private bool _showHabitatSummary;
        private string[] _showOSMMUpdatesOptions;
        private string _showOSMMUpdatesOption;
        private string _preferredHabitatClass;
        private string _preferredSecondaryGroup;
        private string[] _secondaryCodeOrderOptions;
        private string _secondaryCodeOrder;

        // User Updates options
        private bool _notifyOnSplitMerge;

        // Filter options
        private int? _getValueRows;
        private int _maxGetValueRows;
        private int? _warnBeforeGISSelect;
        private string _sqlPath;

        // Backup variables
        private string _bakSqlPath;

        public ObservableCollection<NavigationItem> NavigationItems { get; }
        public ICollectionView GroupedNavigationItems { get; set; }
        private readonly Dictionary<string, System.Windows.Controls.UserControl> _controlInstances = [];

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public ViewModelOptions()
        {
           // Get the dockpane DAML id.
           DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModelMain = pane as ViewModelWindowMain;

            // Get the AddInSettings from the ViewModel.
            _addInSettings = _viewModelMain.AddInSettings;

            // Get the GIS ID Column Ordinals.
            _gisIDColumnOrdinals = Settings.Default.GisIDColumnOrdinals.Cast<string>()
                .Select(s => Int32.Parse(s)).ToList();

            // Set the application database options
            _dbConnectionTimeout = _addInSettings.DbConnectionTimeout;
            _incidTablePageSize = _addInSettings.IncidTablePageSize;

            // Set the application dates options
            _seasonSpring = _addInSettings.SeasonNames[0];
            _seasonSummer = _addInSettings.SeasonNames[1];
            _seasonAutumn = _addInSettings.SeasonNames[2];
            _seasonWinter = _addInSettings.SeasonNames[3];
            _vagueDateDelimiter = _addInSettings.VagueDateDelimiter;

            // Set the application updates options
            _habitatSecondaryCodeValidation = _addInSettings.HabitatSecondaryCodeValidation;
            _primarySecondaryCodeValidation = _addInSettings.PrimarySecondaryCodeValidation;
            _qualityValidation = _addInSettings.QualityValidation;
            _potentialPriorityDetermQtyValidation = _addInSettings.PotentialPriorityDetermQtyValidation;
            _subsetUpdateAction = _addInSettings.SubsetUpdateAction;
            _clearIHSUpdateAction = _addInSettings.ClearIHSUpdateAction;
            _secondaryCodeDelimiter = _addInSettings.SecondaryCodeDelimiter;
            _resetOSMMUpdatesStatus = _addInSettings.ResetOSMMUpdatesStatus;

            // Set the application bulk Update options
            _bulkDeleteOrphanBapHabitats = _addInSettings.BulkUpdateDeleteOrphanBapHabitats;
            _bulkDeletePotentialBapHabitats = _addInSettings.BulkUpdateDeletePotentialBapHabitats;
            _bulkDeleteIHSCodes = _addInSettings.BulkUpdateDeleteIHSCodes;
            _bulkDeleteSecondaryCodes = _addInSettings.BulkUpdateDeleteSecondaryCodes;
            _bulkCreateHistoryRecords = _addInSettings.BulkUpdateCreateHistoryRecords;
            _bulkDeterminationQuality = _addInSettings.BulkUpdateDeterminationQuality;
            _bulkInterpretationQuality = _addInSettings.BulkUpdateInterpretationQuality;
            _bulkOSMMSourceId = _addInSettings.BulkOSMMSourceId;

            // Set the user GIS options
            _minAutoZoom = Settings.Default.MinAutoZoom;
            _maxAutoZoom = Settings.Default.MaxAutoZoom;

            // User History options
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

            _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

            // Set the user interface options
            _showGroupHeaders = Settings.Default.ShowGroupHeaders;
            _showIHSTab = Settings.Default.ShowIHSTab;
            _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
            _showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
            _showNVCCodes = Settings.Default.ShowNVCCodes;
            _showHabitatSummary = Settings.Default.ShowHabitatSummary;
            _showOSMMUpdatesOption = Settings.Default.ShowOSMMUpdatesOption;
            _preferredHabitatClass = Settings.Default.PreferredHabitatClass;
            _preferredSecondaryGroup = Settings.Default.PreferredSecondaryGroup;
            _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;

            // Set the user updates options
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;

            // Set the user SQL options
            _getValueRows = Settings.Default.GetValueRows;
            _maxGetValueRows = Settings.Default.MaxGetValueRows;
            _warnBeforeGISSelect = Settings.Default.WarnBeforeGISSelect;
            _sqlPath = Settings.Default.SqlPath;

            // Load the navigation items (the individual options pages).
            NavigationItems =
            [
                new() { Name = "Database", Category = "Application", Content = new AppDatabaseOptions() },
                new () { Name = "Dates", Category = "Application", Content = new AppDatesOptions() },
                new () { Name = "Validation", Category = "Application", Content = new AppValidationOptions() },
                new () { Name = "Updates", Category = "Application", Content = new AppUpdatesOptions() },
                new () { Name = "Bulk Update", Category = "Application", Content = new AppBulkUpdateOptions() },
                new () { Name = "Interface", Category = "User", Content = new UserInterfaceOptions() },
                new () { Name = "GIS", Category = "User", Content = new UserGISOptions() },
                new () { Name = "Updates", Category = "User", Content = new UserUpdatesOptions() },
                new () { Name = "History", Category = "User", Content = new UserHistoryOptions() },
                new () { Name = "SQL", Category = "User", Content = new UserSQLOptions() }
            ];

            var collectionView = new CollectionViewSource { Source = NavigationItems };
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            GroupedNavigationItems = collectionView.View;

            // Set the default selected view to the first page.
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

        #region Button Images

        /// <summary>
        /// Get the image for the ButtonBrowseSql button.
        /// </summary>
        public static ImageSource ButtonOpenFolderImg
        {
            get
            {
                string imageSource = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/FolderOpenState32.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
                //var imageSource = System.Windows.Application.Current.Resources["FolderOpenState16"] as ImageSource;
                //return imageSource;
            }
        }

        #endregion Button Images

        #region Hyperlinks

        public Uri Hyperlink_AppDatabaseHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.AppDatabase), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_AppDatesHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.AppDates), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_AppBulkUpdatesHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.AppBulkUpdate), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_AppUpdatesHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.AppUpdates), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_AppValidationHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.AppValidation), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_UserGISHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserGIS), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_UserInterfaceHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserInterface), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_UserUpdatesHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserUpdates), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_UserSQLHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserSQL), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        public Uri Hyperlink_UserHistoryHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserHistory), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
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
        /// Create the Save button command.
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
        /// Handles event when Save button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void SaveCommandClick(object param)
        {
            // Save add-in settings.
            SaveAddInSettings();

            // Save user settings;
            SaveUserSettings();

            // Close the window and trigger the event to apply the settings.
            this.RequestClose(true);
        }

        /// <summary>
        /// Save the add-in settings.
        /// </summary>
        private void SaveAddInSettings()
        {
            // Update add-in database options
            _addInSettings.DbConnectionTimeout = (int)_dbConnectionTimeout;
            _addInSettings.IncidTablePageSize = (int)_incidTablePageSize;

            // Update add-in dates options
            _addInSettings.SeasonNames[0] = _seasonSpring;
            _addInSettings.SeasonNames[1] = _seasonSummer;
            _addInSettings.SeasonNames[2] = _seasonAutumn;
            _addInSettings.SeasonNames[3] = _seasonWinter;
            _addInSettings.VagueDateDelimiter = _vagueDateDelimiter;

            // Update add-in updates options
            _addInSettings.HabitatSecondaryCodeValidation = _habitatSecondaryCodeValidation;
            _addInSettings.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation;
            _addInSettings.QualityValidation = _qualityValidation;
            _addInSettings.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation;
            _addInSettings.SubsetUpdateAction = (int)_subsetUpdateAction;
            _addInSettings.ClearIHSUpdateAction = _clearIHSUpdateAction;
            _addInSettings.SecondaryCodeDelimiter = _secondaryCodeDelimiter;
            _addInSettings.ResetOSMMUpdatesStatus = _resetOSMMUpdatesStatus;

            // Update add-in bulk update options
            _addInSettings.BulkUpdateDeleteOrphanBapHabitats = _bulkDeleteOrphanBapHabitats;
            _addInSettings.BulkUpdateDeletePotentialBapHabitats = _bulkDeletePotentialBapHabitats;
            _addInSettings.BulkUpdateDeleteSecondaryCodes = _bulkDeleteSecondaryCodes;
            _addInSettings.BulkUpdateCreateHistoryRecords = _bulkCreateHistoryRecords;
            _addInSettings.BulkUpdateDeleteIHSCodes = _bulkDeleteIHSCodes;

            // Update add-in OSMM bulk update options
            _addInSettings.BulkUpdateDeterminationQuality = _bulkDeterminationQuality;
            _addInSettings.BulkUpdateInterpretationQuality = _bulkInterpretationQuality;
            _addInSettings.BulkOSMMSourceId = (int)_bulkOSMMSourceId;

            // Save changes back to XML in main window.
            _viewModelMain.SaveAddInSettings(_addInSettings);
        }

        /// <summary>
        /// Save the user settings.
        /// </summary>
        private void SaveUserSettings()
        {
            // Update user GIS options
            Settings.Default.MinAutoZoom = (int)_minAutoZoom;

            // Update user history options
            //TOOO: Fix this
            //Settings.Default.HistoryColumnOrdinals =
            //[
            //    .. _historyColumns.Where(c => c.IsSelected)
            //        .Select(c => _incidMMPolygonsTable.Columns[UnescapeAccessKey(c.Item)].Ordinal.ToString()).ToArray(),
            //];
            Settings.Default.HistoryDisplayLastN = (int)_historyDisplayLastN;

            // Update user interface options
            Settings.Default.PreferredHabitatClass = _preferredHabitatClass;
            Settings.Default.ShowGroupHeaders = _showGroupHeaders;
            Settings.Default.ShowIHSTab = _showIHSTab;
            Settings.Default.ShowSourceHabitatGroup = _showSourceHabitatGroup;
            Settings.Default.ShowHabitatSecondariesSuggested = _showHabitatSecondariesSuggested;
            Settings.Default.ShowNVCCodes = _showNVCCodes;
            Settings.Default.ShowHabitatSummary = _showHabitatSummary;
            Settings.Default.ShowOSMMUpdatesOption = _showOSMMUpdatesOption;

            Settings.Default.PreferredSecondaryGroup = _preferredSecondaryGroup;
            Settings.Default.SecondaryCodeOrder = _secondaryCodeOrder;

            // Update user SQL options
            Settings.Default.GetValueRows = (int)_getValueRows;
            Settings.Default.WarnBeforeGISSelect = (int)_warnBeforeGISSelect;
            Settings.Default.SqlPath = _sqlPath;

            // Update user updates options
            Settings.Default.NotifyOnSplitMerge = _notifyOnSplitMerge;

            // Save changes to the settings.
            Settings.Default.Save();
        }

        /// <summary>
        /// Check if the Save button can be clicked (there are no errors).
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool CanSave { get { return String.IsNullOrEmpty(Error); } }

        #endregion

        #region Cancel Command

        /// <summary>
        /// Create the Cancel button command.
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
        /// Handles event when Cancel button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CancelCommandClick(object param)
        {
            // Don't save the changes.
            this.RequestClose(false);
        }

        #endregion

        #region OpenHyperlink Command

        /// <summary>
        /// Create the OpenHyperlink button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand OpenHyperlinkCommand
        {
            get
            {
                if (_openHyperlinkCommand == null)
                {
                    Action<object> openHyperlinkAction = new(this.OpenHyperlinkClick);
                    _openHyperlinkCommand = new RelayCommand(openHyperlinkAction);
                }

                return _openHyperlinkCommand;
            }
        }

        /// <summary>
        /// Handles event when OpenHyperlink is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void OpenHyperlinkClick(object parameter)
        {
            if (parameter is Uri uri && uri != null)
            {
                string url = uri.AbsoluteUri; // Convert Uri to string
                System.Diagnostics.Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        #endregion

        #region Application Database

        public int? DbConnectionTimeout
        {
            get { return _dbConnectionTimeout; }
            set { _dbConnectionTimeout = value; }
        }

        public int MaxDbConnectionTimeout
        {
            get { return 3600; }
        }

        public int? IncidTablePageSize
        {
            get { return _incidTablePageSize; }
            set { _incidTablePageSize = value; }
        }

        public int MaxIncidTablePageSize
        {
            get { return 1000; }
        }

        #endregion

        #region User GIS/Export

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
                return string.Format("Minimum auto zoom [{0}]", distUnits);
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

        /// <summary>
        /// Gets the default maximum auto zoom scale.
        /// </summary>
        /// <value>
        /// The Maximum auto zoom scale.
        /// </value>
        public int MaxAutoZoom
        {
            get { return _maxAutoZoom; }
        }

        #endregion

        #region User History

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

        public int MaxHistoryDisplayLastN
        {
            get { return 50; }
        }

        #endregion

        #region User Interface

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
        public bool ShowHabitatSecondariesSuggested
        {
            get { return _showHabitatSecondariesSuggested; }
            set { _showHabitatSecondariesSuggested = value; }
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

        #region Application Updates

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

        /// <summary>
        /// Gets the primary/secondary code validation options.
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

        #region User Updates

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

        #endregion

        #region User SQL

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

        /// <summary>
        /// Gets or sets the maximum number of value rows to retrieve.
        /// </summary>
        /// <value>
        /// The maximum get value rows.
        /// </value>
        public int MaxGetValueRows
        {
            get { return _maxGetValueRows; }
        }

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

        #endregion

        #region Application Date

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

        #region Application Bulk Update

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
        public int? OSMMSourceId
        {
            get { return _bulkOSMMSourceId; }
            set { _bulkOSMMSourceId = value; }
        }

        #endregion

        #region IDataErrorInfo Members

        /// <summary>
        /// Are there any errors in the settings?
        /// </summary>
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
                    Match m = SecondaryCodeDelimeterRegex().Match(SecondaryCodeDelimiter);
                    if (m.Success == true)
                        error.Append("\n" + "Secondary code delimiter cannot contain letters or numbers.");
                }

                // Update options
                if (SubsetUpdateAction == null)
                    error.Append("Select the action to take when updating an incid subset.");
                if (ClearIHSUpdateAction == null)
                    error.Append("Select when to clear IHS codes after an update.");

                // SQL options
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
                    Match m = VagueDateDelimeterRegex().Match(VagueDateDelimiter);
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

        /// <summary>
        /// Get the error message for the specified column.
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
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

                    // Update options
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
                    case "SubsetUpdateAction":
                        if (SubsetUpdateAction == null)
                            error = "Error: Select the action to take when updating an incid subset.";
                        break;
                    case "ClearIHSUpdateAction":
                        if (ClearIHSUpdateAction == null)
                            error = "Error: Select when to clear IHS codes after an update.";
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
                            Match m = SecondaryCodeDelimeterRegex().Match(SecondaryCodeDelimiter);
                            if (m.Success == true)
                                error = "Error: Secondary code delimiter cannot contain letters or numbers.";
                        }
                        break;

                    // SQL options
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
                            Match m = VagueDateDelimeterRegex().Match(VagueDateDelimiter);
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

        /// <summary>
        /// Defines a compiled regular expression that matches any single alphanumeric character.
        /// </summary>
        /// <remarks>
        /// - The pattern `[a-zA-Z0-9]` matches:
        ///   - Any lowercase letter (`a-z`).
        ///   - Any uppercase letter (`A-Z`).
        ///   - Any numeric digit (`0-9`).
        /// - This regex is useful for detecting or validating alphanumeric characters.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match a single alphanumeric character.</returns>
        [GeneratedRegex(@"[a-zA-Z0-9]")]
        private static partial Regex SecondaryCodeDelimeterRegex();



        /// <summary>
        /// Defines a compiled regular expression that matches any single alphanumeric character.
        /// </summary>
        /// <remarks>
        /// - The pattern `[a-zA-Z0-9]` matches:
        ///   - Any lowercase letter (`a-z`).
        ///   - Any uppercase letter (`A-Z`).
        ///   - Any numeric digit (`0-9`).
        /// - This regex can be used to detect or validate alphanumeric characters that serve as vague date delimiters.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match a single alphanumeric character.</returns>
        [GeneratedRegex(@"[a-zA-Z0-9]")]
        private static partial Regex VagueDateDelimeterRegex();


        #endregion
    }
}
