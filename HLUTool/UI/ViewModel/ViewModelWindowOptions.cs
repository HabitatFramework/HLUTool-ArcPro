// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.Data.Model;
using HLU.Enums;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.UserControls;
using HLU.UI.View;
using HLU.UI.ViewModel;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for the Options window.
    /// </summary>
    partial class ViewModelWindowOptions : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ViewModelWindowMain _viewModelMain;

        private AddInSettings _addInSettings;

        private ICommand _saveCommand;
        private ICommand _cancelCommand;
        private ICommand _browseSQLPathCommand;
        private ICommand _browseExportPathCommand;
        private ICommand _browseWorkingFileGDBPathCommand;
        private ICommand _openHyperlinkCommand;

        private string _displayName = "Options";

        private HluDataSet.incid_mm_polygonsDataTable _incidMMPolygonsTable = new();
        private List<int> _gisIDColumnOrdinals;

        // Application Database options
        private int? _dbConnectionTimeout;
        private int? _incidTablePageSize;

        // Application Dates options
        private string _seasonSpring;
        private string _seasonSummer;
        private string _seasonAutumn;
        private string _seasonWinter;
        private string _vagueDateDelimiter;

        // Application Validation options
        private int _habitatSecondaryCodeValidation;
        private int _primarySecondaryCodeValidation;
        private int _qualityValidation;
        private int _potentialPriorityDetermQtyValidation;

        // Application Updates options
        private int? _subsetUpdateAction;
        private string[] _clearIHSUpdateActions;
        private string _clearIHSUpdateAction;
        private string _secondaryCodeDelimiter;
        private bool _resetOSMMUpdatesStatus;

        // Application Bulk Update options
        private bool _bulkDeleteOrphanBapHabitats;
        private bool _bulkDeletePotentialBapHabitats;
        private bool _bulkDeleteIHSCodes;
        private bool _bulkDeleteSecondaryCodes;
        private bool _bulkCreateHistoryRecords;
        private string _bulkDeterminationQuality;
        private string _bulkInterpretationQuality;
        private int? _bulkOSMMSourceId;

        // User GIS options
        private string[] _autoZoomToSelectionOptions;
        private string _autoZoomToSelection;
        private int? _minAutoZoom;
        private int _maxAutoZoom;
        private int? _maxFeaturesGISSelect;
        private string _workingFileGDBPath;

        // User History options
        private SelectionList<string> _historyColumns;
        private int? _historyDisplayLastN;
        private bool _historyDisplayGeometry;

        // User Interface options
        private bool _showGroupHeaders;
        private bool _showIHSTab;
        private bool _showSourceHabitatGroup;
        private bool _showHabitatSecondariesSuggested;
        private bool _showNVCCodes;
        private bool _showHabitatSummary;
        private string[] _showOSMMUpdatesOptions;
        private string _showOSMMUpdatesOption;
        private int? _messageAutoDismissError;
        private int? _messageAutoDismissWarning;
        private int? _messageAutoDismissInfo;
        private int? _messageAutoDismissSuccess;

        // User Update options
        private string _defaultReason;
        private string _defaultProcess;
        private string _defaultHabitatClass;
        private string _defaultSecondaryGroup;
        private string[] _secondaryCodeOrderOptions;
        private string _secondaryCodeOrder;
        private bool _notifyOnSplitMerge;

        // User SQL options
        private int? _getValueRows;
        private int _maxGetValueRows;
        private string _sqlPath;

        // User Export options
        private string _exportPath;

        // Backup variables
        private string _bakSQLPath;
        private string _bakExportPath;
        private string _bakWorkingFileGDBPath;

        public ObservableCollection<NavigationItem> NavigationItems { get; }
        public ICollectionView GroupedNavigationItems { get; set; }

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowOptions class and loads the settings from the main window ViewModel.
        /// </summary>
        /// <param name="habitatClasses">The habitat classes to be used in the options window.</param>
        /// <param name="secondaryGroupsAll">The secondary groups to be used in the options window.</param>
        public ViewModelWindowOptions()
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
            _gisIDColumnOrdinals = [.. Settings.Default.GisIDColumnOrdinals.Cast<string>().Select(s => Int32.Parse(s))];

            // Set the application database options
            _dbConnectionTimeout = _addInSettings.DbConnectionTimeout;
            _incidTablePageSize = _addInSettings.IncidTablePageSize;

            // Set the application dates options
            _seasonSpring = _addInSettings.SeasonNames[0];
            _seasonSummer = _addInSettings.SeasonNames[1];
            _seasonAutumn = _addInSettings.SeasonNames[2];
            _seasonWinter = _addInSettings.SeasonNames[3];
            _vagueDateDelimiter = _addInSettings.VagueDateDelimiter;

            // Set the application validation options
            _habitatSecondaryCodeValidation = _addInSettings.HabitatSecondaryCodeValidation;
            _primarySecondaryCodeValidation = _addInSettings.PrimarySecondaryCodeValidation;
            _qualityValidation = _addInSettings.QualityValidation;
            _potentialPriorityDetermQtyValidation = _addInSettings.PotentialPriorityDetermQtyValidation;

            // Set the application updates options
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
            var enumValue = (AutoZoomToSelection)Settings.Default.AutoZoomToSelection;
            _autoZoomToSelection = AutoZoomToSelectionString(enumValue);
            _minAutoZoom = Settings.Default.MinAutoZoom;
            _maxAutoZoom = Settings.Default.MaxAutoZoom;
            _maxFeaturesGISSelect = Settings.Default.MaxFeaturesGISSelect;
            _workingFileGDBPath = Settings.Default.WorkingFileGDBPath;

            // Get the history column ordinals from the settings, excluding the GIS ID columns and
            // shape columns.
            List<int> historyColumnOrdinals = [.. Settings.Default.HistoryColumnOrdinals.Cast<string>()
                .Select(s => Int32.Parse(s)).Where(i => !_gisIDColumnOrdinals.Contains(i) &&
                    !_incidMMPolygonsTable.Columns[i].ColumnName.StartsWith("shape_"))];

            // Get the history columns for the options window by getting the column names from the incid table,
            // excluding the GIS ID columns and shape columns.
            _historyColumns = new SelectionList<string>([.. _incidMMPolygonsTable.Columns.Cast<DataColumn>()
                .Where(c => !_gisIDColumnOrdinals.Contains(c.Ordinal)
                    && !c.ColumnName.StartsWith("shape_"))
                .Select(c => EscapeAccessKey(c.ColumnName))]);

            // Set the IsSelected property of the history columns based on whether their ordinals
            // are in the history column ordinals list.
            foreach (SelectionItem<string> si in _historyColumns)
                si.IsSelected = historyColumnOrdinals.Contains(
                    _incidMMPolygonsTable.Columns[UnescapeAccessKey(si.Item)].Ordinal);

            // Set the history display last number of records option.
            _historyDisplayLastN = Settings.Default.HistoryDisplayLastN;

            // Set the history display geometry option.
            _historyDisplayGeometry = Settings.Default.HistoryDisplayGeometry;

            // Set the user interface options
            _showGroupHeaders = Settings.Default.ShowGroupHeaders;
            _showIHSTab = Settings.Default.ShowIHSTab;
            _showSourceHabitatGroup = Settings.Default.ShowSourceHabitatGroup;
            _showHabitatSecondariesSuggested = Settings.Default.ShowHabitatSecondariesSuggested;
            _showNVCCodes = Settings.Default.ShowNVCCodes;
            _showHabitatSummary = Settings.Default.ShowHabitatSummary;
            _showOSMMUpdatesOption = Settings.Default.ShowOSMMUpdatesOption;
            _messageAutoDismissError = Settings.Default.MessageAutoDismissError;
            _messageAutoDismissWarning = Settings.Default.MessageAutoDismissWarning;
            _messageAutoDismissInfo = Settings.Default.MessageAutoDismissInfo;
            _messageAutoDismissSuccess = Settings.Default.MessageAutoDismissSuccess;

            // Set the user update options
            _defaultReason = Settings.Default.DefaultReason;
            _defaultProcess = Settings.Default.DefaultProcess;
            _defaultHabitatClass = Settings.Default.DefaultHabitatClass;
            _defaultSecondaryGroup = Settings.Default.DefaultSecondaryGroup;
            _secondaryCodeOrder = Settings.Default.SecondaryCodeOrder;
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;

            // Set the user SQL options
            _getValueRows = Settings.Default.GetValueRows;
            _maxGetValueRows = Settings.Default.MaxGetValueRows;
            _sqlPath = Settings.Default.SQLPath;

            // Set the user Export options
            _exportPath = Settings.Default.ExportPath;

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
                new () { Name = "SQL", Category = "User", Content = new UserSQLOptions() },
                new () { Name = "History", Category = "User", Content = new UserHistoryOptions() },
                new () { Name = "Export", Category = "User", Content = new UserExportOptions() }
            ];

            var collectionView = new CollectionViewSource { Source = NavigationItems };
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            GroupedNavigationItems = collectionView.View;

            // Set the default selected view to the first page.
            SelectedView = NavigationItems.First();

            // Initialize error states for all navigation items
            foreach (var item in NavigationItems)
            {
                item.HasErrors = HasErrorsForNavigationItem(item);
                item.ErrorMessage = GetErrorMessageForNavigationItem(item);
            }
        }

        /// <summary>
        /// Escape the access key characters in the column names for display in the options window.
        /// This is needed because the column names are used as items in a context menu which uses
        /// underscores to indicate access keys, and some column names have underscores which would
        /// be incorrectly interpreted as access keys.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <returns>The escaped string.</returns>
        private string EscapeAccessKey(string s)
        {
            return s.Replace("_", "__");
        }

        /// <summary>
        /// Unescape the access key characters in the column names to get the original column names.
        /// This is needed because the column names are used as items in a context menu which uses
        /// underscores to indicate access keys, and some column names have underscores which would
        /// be incorrectly interpreted as access keys.
        /// </summary>
        /// <param name="s">The string to unescape.</param>
        /// <returns>The unescaped string.</returns>
        private string UnescapeAccessKey(string s)
        {
            return s.Replace("__", "_");
        }

        #endregion Constructor

        #region ViewModelBase Members

        /// <summary>
        /// Gets or sets the display name for the view model.
        /// </summary>
        /// <value>The display name for the view model.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        /// <summary>
        /// Gets the window title for the options window, which is the same as the display name.
        /// </summary>
        /// <value>The window title for the options window.</value>
        public override string WindowTitle
        {
            get { return _displayName; }
        }

        #endregion ViewModelBase Members

        #region Button Images

        /// <summary>
        /// Get the image for the ButtonBrowse button.
        /// </summary>
        /// <value>The image for the ButtonBrowse button.</value>
        public static ImageSource ButtonOpenFolderImg
        {
            get
            {
                string imageSource = "pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/FolderOpenState32.png";
                return new BitmapImage(new Uri(imageSource)) as ImageSource;
            }
        }

        #endregion Button Images

        #region Hyperlinks

        /// <summary>
        /// Gets the hyperlink for the application database help page, which is constructed from
        /// the base help URL and the specific help page for the application database options.
        /// </summary>
        /// <value>The hyperlink for the application database help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the application dates help page, which is constructed from
        /// the base help URL and the specific help page for the application dates options.
        /// </summary>
        /// <value>The hyperlink for the application dates help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the application bulk update help page, which is constructed from
        /// the base help URL and the specific help page for the application bulk update options.
        /// </summary>
        /// <value>The hyperlink for the application bulk update help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the application updates help page, which is constructed from
        /// the base help URL and the specific help page for the application updates options.
        /// </summary>
        /// <value>The hyperlink for the application updates help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the application validation help page, which is constructed from
        /// the base help URL and the specific help page for the application validation options.
        /// </summary>
        /// <value>The hyperlink for the application validation help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the user GIS help page, which is constructed from
        /// the base help URL and the specific help page for the user GIS options.
        /// </summary>
        /// <value>The hyperlink for the user GIS help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the user interface help page, which is constructed from
        /// the base help URL and the specific help page for the user interface options.
        /// </summary>
        /// <value>The hyperlink for the user interface help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the user updates help page, which is constructed from
        /// the base help URL and the specific help page for the user updates options.
        /// </summary>
        /// <value>The hyperlink for the user updates help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the user SQL help page, which is constructed from
        /// the base help URL and the specific help page for the user SQL options.
        /// </summary>
        /// <value>The hyperlink for the user SQL help page.</value>
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

        /// <summary>
        /// Gets the hyperlink for the user export help page, which is constructed from
        /// the base help URL and the specific help page for the user export options.
        /// </summary>
        /// <value>The hyperlink for the user export help page.</value>
        public Uri Hyperlink_UserExportHelp
        {
            get
            {
                if (Uri.TryCreate(string.Format("{0}/{1}", _addInSettings.HelpURL, _addInSettings.HelpPages.UserExport), UriKind.Absolute, out Uri uri))
                    return uri;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the hyperlink for the user history help page, which is constructed from
        /// the base help URL and the specific help page for the user history options.
        /// </summary>
        /// <value>The hyperlink for the user history help page.</value>
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

        #endregion Hyperlinks

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(bool saveSettings);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Navigation Items

        private NavigationItem _selectedView;

        /// <summary>
        /// Gets or sets the currently selected navigation view.
        /// </summary>
        /// <value>The currently selected navigation view.</value>
        public NavigationItem SelectedView
        {
            get => _selectedView;
            set
            {
                // Don't allow switching if current tab has errors
                if (_selectedView != null && _selectedView.HasErrors && value != _selectedView)
                {
                    // Show a message to the user
                    MessageBox.Show(
                        "Please correct the validation errors on this tab before switching to another tab.\n\n" +
                        _selectedView.ErrorMessage,
                        "Validation Errors",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Re-select the current item in the ListBox
                    OnPropertyChanged(nameof(SelectedView));
                    return;
                }

                if (_selectedView != value)
                {
                    // Clear previous selection
                    if (_selectedView != null)
                        _selectedView.IsSelected = false;

                    _selectedView = value;

                    // Set new selection
                    if (_selectedView != null)
                        _selectedView.IsSelected = true;

                    OnPropertyChanged(nameof(SelectedView));

                    // Force revalidation of all navigation items when switching tabs
                    NotifyNavigationItemErrorsChanged();
                }
            }
        }

        #endregion Navigation Items

        #region Save Command

        /// <summary>
        /// Gets the command to save the current settings. This command will save the add-in settings to the XML file
        /// </summary>
        /// <value>The command to save the current settings.</value>
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
        /// <param name="param">The parameter passed to the command.</param>
        private void SaveCommandClick(object param)
        {
            // Save add-in settings.
            SaveAddInSettings();

            // Save user settings;
            SaveUserSettings();

            // Close the window and trigger the event to apply the settings.
            RequestClose?.Invoke(true);
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

            // Update add-in validation options
            _addInSettings.HabitatSecondaryCodeValidation = _habitatSecondaryCodeValidation;
            _addInSettings.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation;
            _addInSettings.QualityValidation = _qualityValidation;
            _addInSettings.PotentialPriorityDetermQtyValidation = _potentialPriorityDetermQtyValidation;

            // Update add-in updates options
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
            var enumValue = AutoZoomToSelectionEnum(_autoZoomToSelection);
            Settings.Default.AutoZoomToSelection = (int)enumValue;
            Settings.Default.MinAutoZoom = (int)_minAutoZoom;
            Settings.Default.MaxFeaturesGISSelect = (int)_maxFeaturesGISSelect;
            Settings.Default.WorkingFileGDBPath = _workingFileGDBPath;

            // Update which history columns to display in the history tab.
            Settings.Default.HistoryColumnOrdinals =
            [
                .. _historyColumns.Where(c => c.IsSelected)
                    .Select(c => _incidMMPolygonsTable.Columns[UnescapeAccessKey(c.Item)].Ordinal.ToString()).ToArray(),
            ];

            // Update the history display last number of records option.
            Settings.Default.HistoryDisplayLastN = (int)_historyDisplayLastN;

            // Update the history display geometry option.
            Settings.Default.HistoryDisplayGeometry = _historyDisplayGeometry;

            // Update user interface options
            Settings.Default.ShowGroupHeaders = _showGroupHeaders;
            Settings.Default.ShowIHSTab = _showIHSTab;
            Settings.Default.ShowSourceHabitatGroup = _showSourceHabitatGroup;
            Settings.Default.ShowHabitatSecondariesSuggested = _showHabitatSecondariesSuggested;
            Settings.Default.ShowNVCCodes = _showNVCCodes;
            Settings.Default.ShowHabitatSummary = _showHabitatSummary;
            Settings.Default.ShowOSMMUpdatesOption = _showOSMMUpdatesOption;
            Settings.Default.MessageAutoDismissError = (int)_messageAutoDismissError;
            Settings.Default.MessageAutoDismissWarning = (int)_messageAutoDismissWarning;
            Settings.Default.MessageAutoDismissInfo = (int)_messageAutoDismissInfo;
            Settings.Default.MessageAutoDismissSuccess = (int)_messageAutoDismissSuccess;

            // Update user update options
            Settings.Default.DefaultReason = _defaultReason;
            Settings.Default.DefaultProcess = _defaultProcess;
            Settings.Default.DefaultHabitatClass = _defaultHabitatClass;
            Settings.Default.DefaultSecondaryGroup = _defaultSecondaryGroup;
            Settings.Default.SecondaryCodeOrder = _secondaryCodeOrder;
            Settings.Default.NotifyOnSplitMerge = _notifyOnSplitMerge;

            // Update user SQL options
            Settings.Default.GetValueRows = (int)_getValueRows;
            Settings.Default.SQLPath = _sqlPath;

            // Update user export options
            Settings.Default.ExportPath = _exportPath;

            // Save changes to the settings.
            Settings.Default.Save();
        }

        /// <summary>
        /// Check if the Save button can be clicked (there are no errors).
        /// </summary>
        /// <value>Indicates whether the Save button can be clicked.</value>
        /// <returns>True if there are no errors, otherwise false.</returns>
        private bool CanSave { get { return String.IsNullOrEmpty(Error); } }

        #endregion Save Command

        #region Cancel Command

        /// <summary>
        /// Create the Cancel button command.
        /// </summary>
        /// <value>Indicates whether the Cancel button can be clicked.</value>
        /// <returns>True if the Cancel button can be clicked, otherwise false.</returns>
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
        /// <param name="param">The parameter passed to the command.</param>
        private void CancelCommandClick(object param)
        {
            // Don't save the changes.
            RequestClose?.Invoke(false);
        }

        #endregion Cancel Command

        #region OpenHyperlink Command

        /// <summary>
        /// Create the OpenHyperlink button command.
        /// </summary>
        /// <value>Indicates whether the OpenHyperlink button can be clicked.</value>
        /// <returns>True if the OpenHyperlink button can be clicked, otherwise false.</returns>
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
        /// <param name="param">The parameter passed to the command.</param>
        private void OpenHyperlinkClick(object parameter)
        {
            if (parameter is Uri uri && uri != null)
            {
                string url = uri.AbsoluteUri; // Convert Uri to string
                System.Diagnostics.Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        #endregion OpenHyperlink Command

        #region Application Database

        /// <summary>
        /// Gets or sets the database connection timeout.
        /// </summary>
        /// <value>The database connection timeout in seconds.</value>
        public int? DbConnectionTimeout
        {
            get { return _dbConnectionTimeout; }
            set
            {
                _dbConnectionTimeout = value;
                OnPropertyChanged(nameof(DbConnectionTimeout));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the maximum database connection timeout.
        /// </summary>
        public int MaxDbConnectionTimeout
        {
            get { return 3600; }
        }

        /// <summary>
        /// Gets or sets the incid table page size.
        /// </summary>
        /// <value>The incid table page size.</value>
        public int? IncidTablePageSize
        {
            get { return _incidTablePageSize; }
            set
            {
                _incidTablePageSize = value;
                OnPropertyChanged(nameof(IncidTablePageSize));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the maximum incid table page size.
        /// </summary>
        /// <value>The maximum incid table page size.</value>
        public int MaxIncidTablePageSize
        {
            get { return 1000; }
        }

        #endregion Application Database

        #region Application Date

        /// <summary>
        /// Gets or sets the season name for spring.
        /// </summary>
        /// <value></value>
        /// The season name for spring.
        /// </value>
        public string SeasonSpring
        {
            get
            {
                return _seasonSpring;
            }
            set
            {
                _seasonSpring = value;
                OnPropertyChanged(nameof(SeasonSpring));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the season name for summer.
        /// </summary>
        /// <value></valiue>
        /// The season name for summer.
        /// </value>
        public string SeasonSummer
        {
            get
            {
                return _seasonSummer;
            }
            set
            {
                _seasonSummer = value;
                OnPropertyChanged(nameof(SeasonSummer));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the season name for autumn.
        /// </summary>
        /// <value></value>
        /// The season name for autumn.
        /// </value>
        public string SeasonAutumn
        {
            get
            {
                return _seasonAutumn;
            }
            set
            {
                _seasonAutumn = value;
                OnPropertyChanged(nameof(SeasonAutumn));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the season name for winter.
        /// </summary>
        /// <value></value>
        /// The season name for winter.
        /// </value>
        public string SeasonWinter
        {
            get
            {
                return _seasonWinter;
            }
            set
            {
                _seasonWinter = value;
                OnPropertyChanged(nameof(SeasonWinter));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the delimiter used for vague dates.
        /// </summary>
        /// <value></value>
        /// The delimiter used for vague dates.
        /// </value>
        public string VagueDateDelimiter
        {
            get
            {
                return _vagueDateDelimiter;
            }
            set
            {
                if (value != CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator)
                    _vagueDateDelimiter = value;
            }
        }

        #endregion Application Date

        #region Application Bulk Update

        /// <summary>
        /// Gets whether the user has authority to perform bulk updates.
        /// </summary>
        /// <value>
        /// True if the user has bulk update authority; otherwise, false.
        /// </value>
        public bool CanBulkUpdate
        {
            get
            {
                return _viewModelMain.CanUserBulkUpdate;
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
            get
            {
                return _bulkDeleteSecondaryCodes;
            }
            set
            {
                _bulkDeleteSecondaryCodes = value;
                OnPropertyChanged(nameof(BulkDeleteSecondaryCodes));
                NotifyNavigationItemErrorsChanged();
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
            get
            {
                return _bulkDeleteOrphanBapHabitats;
            }
            set
            {
                _bulkDeleteOrphanBapHabitats = value;
                OnPropertyChanged(nameof(BulkDeleteOrphanBapHabitats));
                NotifyNavigationItemErrorsChanged();
            }
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
            get
            {
                return _bulkDeletePotentialBapHabitats;
            }
            set
            {
                _bulkDeletePotentialBapHabitats = value;
                OnPropertyChanged(nameof(BulkDeletePotentialBapHabitats));
                NotifyNavigationItemErrorsChanged();
            }
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
            get
            {
                return _bulkCreateHistoryRecords;
            }
            set
            {
                _bulkCreateHistoryRecords = value;
                OnPropertyChanged(nameof(BulkCreateHistoryRecords));
                NotifyNavigationItemErrorsChanged();
            }
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
            get
            {
                return _bulkDeleteIHSCodes;
            }
            set
            {
                _bulkDeleteIHSCodes = value;
                OnPropertyChanged(nameof(BulkDeleteIHSCodes));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the list of determination qualities that
        /// can be used when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The list of determination qualities.
        /// </value>
        public HluDataSet.lut_quality_determinationRow[] BulkDeterminationQualityCodes
        {
            get
            {
                return _viewModelMain.BapDeterminationQualityCodesAuto;
            }
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
            get
            {
                return _bulkDeterminationQuality;
            }
            set
            {
                _bulkDeterminationQuality = value;
                OnPropertyChanged(nameof(BulkDeterminationQuality));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the list of interpretation qualities that
        /// can be used when adding BAP habitats during an OSMM bulk
        /// update.
        /// </summary>
        /// <value>
        /// The list of interpretation qualities.
        /// </value>
        public HluDataSet.lut_quality_interpretationRow[] BulkInterpretationQualityCodes
        {
            get
            {
                return _viewModelMain.InterpretationQualityCodes;
            }
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
            get
            {
                return _bulkInterpretationQuality;
            }
            set
            {
                _bulkInterpretationQuality = value;
                OnPropertyChanged(nameof(BulkInterpretationQuality));
                NotifyNavigationItemErrorsChanged();
            }
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
                return _viewModelMain.SourceNames;
            }
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
            get
            {
                return _bulkOSMMSourceId;
            }
            set
            {
                _bulkOSMMSourceId = value;
                OnPropertyChanged(nameof(OSMMSourceId));
                NotifyNavigationItemErrorsChanged();
            }
        }

        #endregion Application Bulk Update

        #region Application Updates

        /// <summary>
        /// Gets the list of available subset update actions from the enum.
        /// </summary>
        /// <value>
        /// The list of subset update actions.
        /// </value>
        public SubsetUpdateActions[] SubsetUpdateActions
        {
            get
            {
                return [.. Enum.GetValues(typeof(SubsetUpdateActions)).Cast<SubsetUpdateActions>()];
            }
        }

        /// <summary>
        /// Gets or sets the preferred subset update action.
        /// </summary>
        /// <value>
        /// The preferred subset update action.
        /// </value>
        public SubsetUpdateActions? SubsetUpdateAction
        {
            get
            {
                return (SubsetUpdateActions)_subsetUpdateAction;
            }
            set
            {
                _subsetUpdateAction = (int)value;
                OnPropertyChanged(nameof(SubsetUpdateAction));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the clear IHS update actions.
        /// </summary>
        /// <value>
        /// The clear IHS update actions.
        /// </value>
        public string[] ClearIHSUpdateActions
        {
            get
            {
                _clearIHSUpdateActions ??= [.. Settings.Default.ClearIHSUpdateActions.Cast<string>()];

                return _clearIHSUpdateActions;
            }
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
                OnPropertyChanged(nameof(ClearIHSUpdateAction));
                NotifyNavigationItemErrorsChanged();
            }
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
            set
            {
                _resetOSMMUpdatesStatus = value;
                OnPropertyChanged(nameof(ResetOSMMUpdatesStatus));
                NotifyNavigationItemErrorsChanged();
            }
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
                return [.. Enum.GetValues(typeof(HabitatSecondaryCodeValidationOptions)).Cast<HabitatSecondaryCodeValidationOptions>()];
            }
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
                OnPropertyChanged(nameof(HabitatSecondaryCodeValidation));
                NotifyNavigationItemErrorsChanged();
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
                return [.. Enum.GetValues(typeof(PrimarySecondaryCodeValidationOptions)).Cast<PrimarySecondaryCodeValidationOptions>()];
            }
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
                OnPropertyChanged(nameof(PrimarySecondaryCodeValidation));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the quality validation options.
        /// </summary>
        /// <value>
        /// The quality validation options.
        /// </value>
        public QualityValidationOptions[] QualityValidationOptions
        {
            get
            {
                return [.. Enum.GetValues(typeof(QualityValidationOptions)).Cast<QualityValidationOptions>()];
            }
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
                OnPropertyChanged(nameof(QualityValidation));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the quality validation options for potential priority determinations.
        /// </summary>
        /// <value>
        /// The quality validation options for potential priority determinations.
        /// </value>
        public PotentialPriorityDetermQtyValidationOptions[] PotentialPriorityDetermQtyValidationOptions
        {
            get
            {
                return [.. Enum.GetValues(typeof(PotentialPriorityDetermQtyValidationOptions)).Cast<PotentialPriorityDetermQtyValidationOptions>()];
            }
        }

        /// <summary>
        /// Gets or sets the quality validation choice for potential priority determinations.
        /// </summary>
        /// <value>
        /// The quality validation choice for potential priority determinations.
        /// </value>
        public PotentialPriorityDetermQtyValidationOptions? PotentialPriorityDetermQtyValidation
        {
            get { return (PotentialPriorityDetermQtyValidationOptions)_potentialPriorityDetermQtyValidation; }
            set
            {
                _potentialPriorityDetermQtyValidation = (int)value;
                OnPropertyChanged(nameof(PotentialPriorityDetermQtyValidation));
                NotifyNavigationItemErrorsChanged();
            }
        }
        #endregion Application Updates

        #region User Interface

        /// <summary>
        /// Gets or sets the preferred option to show or hide group headers.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding group headers.
        /// </value>
        public bool ShowGroupHeaders
        {
            get
            {
                return _showGroupHeaders;
            }
            set
            {
                _showGroupHeaders = value;
                OnPropertyChanged(nameof(ShowGroupHeaders));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide the IHS tab.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hiding the IHS tab.
        /// </value>
        public bool ShowIHSTab
        {
            get
            {
                return _showIHSTab;
            }
            set
            {
                _showIHSTab = value;
                OnPropertyChanged(nameof(ShowIHSTab));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat categories.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat categories.
        /// </value>
        public bool ShowSourceHabitatGroup
        {
            get
            {
                return _showSourceHabitatGroup;
            }
            set
            {
                _showSourceHabitatGroup = value;
                OnPropertyChanged(nameof(ShowSourceHabitatGroup));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat suggestions.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat suggestions.
        /// </value>
        public bool ShowHabitatSecondariesSuggested
        {
            get
            {
                return _showHabitatSecondariesSuggested;
            }
            set
            {
                _showHabitatSecondariesSuggested = value;
                OnPropertyChanged(nameof(ShowHabitatSecondariesSuggested));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide NVC Codes.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding NVC Codes.
        /// </value>
        public bool ShowNVCCodes
        {
            get
            {
                return _showNVCCodes;
            }
            set
            {
                _showNVCCodes = value;
                OnPropertyChanged(nameof(ShowNVCCodes));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the preferred option to show or hide habitat summary.
        /// </summary>
        /// <value>
        /// The preferred option for showing or hidding habitat summary.
        /// </value>
        public bool ShowHabitatSummary
        {
            get
            {
                return _showHabitatSummary;
            }
            set
            {
                _showHabitatSummary = value;
                OnPropertyChanged(nameof(ShowHabitatSummary));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the list of available show OSMM Update options from
        /// the class.
        /// </summary>
        /// <value>
        /// The list of subset update actions.
        /// </value>
        public string[] ShowOSMMUpdatesOptions
        {
            get
            {
                _showOSMMUpdatesOptions ??= [.. Settings.Default.ShowOSMMUpdatesOptions.Cast<string>()];

                return _showOSMMUpdatesOptions;
            }
        }

        /// <summary>
        /// Gets or sets the preferred show OSMM Update option.
        /// </summary>
        /// <value>
        /// The preferred show OSMM Update option.
        /// </value>
        public string ShowOSMMUpdatesOption
        {
            get
            {
                return _showOSMMUpdatesOption;
            }
            set
            {
                _showOSMMUpdatesOption = value;
                OnPropertyChanged(nameof(ShowOSMMUpdatesOption));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the auto-dismiss timeout in seconds for error messages.
        /// 0 means do not auto-dismiss.
        /// </summary>
        /// <value>
        /// The auto-dismiss timeout for error messages (0–60 seconds).
        /// </value>
        public int? MessageAutoDismissError
        {
            get
            {
                return _messageAutoDismissError;
            }
            set
            {
                _messageAutoDismissError = value;
                OnPropertyChanged(nameof(MessageAutoDismissError));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the auto-dismiss timeout in seconds for warning messages.
        /// 0 means do not auto-dismiss.
        /// </summary>
        /// <value>
        /// The auto-dismiss timeout for warning messages (0–60 seconds).
        /// </value>
        public int? MessageAutoDismissWarning
        {
            get
            {
                return _messageAutoDismissWarning;
            }
            set
            {
                _messageAutoDismissWarning = value;
                OnPropertyChanged(nameof(MessageAutoDismissWarning));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the auto-dismiss timeout in seconds for info messages.
        /// 0 means do not auto-dismiss.
        /// </summary>
        /// <value>
        /// The auto-dismiss timeout for info messages (0–60 seconds).
        /// </value>
        public int? MessageAutoDismissInfo
        {
            get
            {
                return _messageAutoDismissInfo;
            }
            set
            {
                _messageAutoDismissInfo = value;
                OnPropertyChanged(nameof(MessageAutoDismissInfo));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the auto-dismiss timeout in seconds for success messages.
        /// 0 means do not auto-dismiss.
        /// </summary>
        /// <value>
        /// The auto-dismiss timeout for success messages (0–60 seconds).
        /// </value>
        public int? MessageAutoDismissSuccess
        {
            get
            {
                return _messageAutoDismissSuccess;
            }
            set
            {
                _messageAutoDismissSuccess = value;
                OnPropertyChanged(nameof(MessageAutoDismissSuccess));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the list of possible habitat class codes.
        /// </summary>
        /// <value>
        /// The list of possible habitat class codes.
        /// </value>
        public HluDataSet.lut_habitat_classRow[] HabitatClassCodes
        {
            get
            {
                return _viewModelMain.HabitatClassCodes;
            }
        }

        /// <summary>
        /// Gets or sets the default habitat class.
        /// </summary>
        /// <value>
        /// The default habitat class.
        /// </value>
        public string DefaultHabitatClass
        {
            get
            {
                var q = HabitatClassCodes.Where(h => h.code == _defaultHabitatClass);
                if (q.Any())
                    return _defaultHabitatClass;
                else
                    return null;
            }
            set
            {
                _defaultHabitatClass = value;
                OnPropertyChanged(nameof(DefaultHabitatClass));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the list of secondary group codes.
        /// </summary>
        /// <value>
        /// The list of secondary group codes.
        /// </value>
        public HluDataSet.lut_secondary_groupRow[] SecondaryGroupCodes
        {
            get
            {
                return _viewModelMain.SecondaryGroupCodesWithAll;
            }
        }

        /// <summary>
        /// Gets or sets the default secondary group.
        /// </summary>
        /// <value>
        /// The default secondary group.
        /// </value>
        public string DefaultSecondaryGroup
        {
            get
            {
                var q = SecondaryGroupCodes.Where(h => h.code == _defaultSecondaryGroup);
                if (q.Any())
                    return _defaultSecondaryGroup;
                else
                    return null;
            }
            set
            {
                _defaultSecondaryGroup = value;
                OnPropertyChanged(nameof(DefaultSecondaryGroup));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the secondary code order options.
        /// </summary>
        /// <value>
        /// The secondary code order options.
        /// </value>
        public string[] SecondaryCodeOrderOptions
        {
            get
            {
                _secondaryCodeOrderOptions ??= [.. Settings.Default.SecondaryCodeOrderOptions.Cast<string>()];

                return _secondaryCodeOrderOptions;
            }
        }

        /// <summary>
        /// Gets or sets the secondary code order choice.
        /// </summary>
        /// <value>
        /// The secondary code order choice.
        /// </value>
        public string SecondaryCodeOrder
        {
            get
            {
                return _secondaryCodeOrder;
            }
            set
            {
                _secondaryCodeOrder = value;
                OnPropertyChanged(nameof(SecondaryCodeOrder));
                NotifyNavigationItemErrorsChanged();
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
            get
            {
                return _secondaryCodeDelimiter;
            }
            set
            {
                _secondaryCodeDelimiter = value;
                OnPropertyChanged(nameof(SecondaryCodeDelimiter));
                NotifyNavigationItemErrorsChanged();
            }
        }

        #endregion User Interface

        #region User GIS

        /// <summary>
        /// Gets the auto zoom selection options.
        /// </summary>
        /// <value>
        /// The auto zoom selection options.
        /// </value>
        public string[] AutoZoomToSelectionOptions
        {
            get
            {
                _autoZoomToSelectionOptions ??= [.. Settings.Default.AutoZoomToSelectionOptions.Cast<string>()];

                return _autoZoomToSelectionOptions;
            }
        }

        /// <summary>
        /// Gets or sets the auto zoom selection choice.
        /// </summary>
        /// <value>
        /// The auto zoom selection choice.
        /// </value>
        public string AutoZoomToSelectionOption
        {
            get
            {
                return _autoZoomToSelection;
            }
            set
            {
                _autoZoomToSelection = value;
                OnPropertyChanged(nameof(AutoZoomToSelectionOption));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Converts an AutoZooToSelection enum to the display string.
        /// </summary>
        /// <param name="value">The AutoZoomToSelection enum value.</param>
        /// <returns>The display string corresponding to the enum value.</returns>
        private string AutoZoomToSelectionString(AutoZoomToSelection value)
        {
            return value switch
            {
                AutoZoomToSelection.Off => "Off",
                AutoZoomToSelection.When => "When out of view",
                AutoZoomToSelection.Always => "Always",
                _ => "Off"
            };
        }

        /// <summary>
        /// Converts a display string to an AutoZoomToSelection enum.
        /// </summary>
        /// <param name="value">The display string value.</param>
        /// <returns>The corresponding AutoZoomToSelection enum value.</returns>
        private static AutoZoomToSelection AutoZoomToSelectionEnum(string value)
        {
            return value switch
            {
                "Off" => AutoZoomToSelection.Off,
                "When out of view" => AutoZoomToSelection.When,
                "Always" => AutoZoomToSelection.Always,
                _ => AutoZoomToSelection.Off
            };
        }

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
                return string.Format("Minimum Auto Zoom [{0}]", distUnits);
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
            get
            {
                return _minAutoZoom;
            }
            set
            {
                _minAutoZoom = value;
                OnPropertyChanged(nameof(MinAutoZoom));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the default maximum auto zoom scale.
        /// </summary>
        /// <value>
        /// The Maximum auto zoom scale.
        /// </value>
        public int MaxAutoZoom
        {
            get
            {
                return _maxAutoZoom;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of features at which to warn the user before selecting.
        /// </summary>
        /// <value>The maximum number of features at which to warn the user before selecting.</value>
        public int? MaxFeaturesGISSelect
        {
            get
            {
                return _maxFeaturesGISSelect;
            }
            set
            {
                _maxFeaturesGISSelect = value;
                OnPropertyChanged(nameof(MaxFeaturesGISSelect));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Get the working File GDB path command.
        /// </summary>
        /// <value>
        /// The browse working File GDB path command.
        /// </value>
        public ICommand BrowseWorkingFileGDBPathCommand
        {
            get
            {
                if (_browseWorkingFileGDBPathCommand == null)
                {
                    Action<object> browseWorkingFileGDBPathAction = new(this.BrowseWorkingFileGDBPathClicked);
                    _browseWorkingFileGDBPathCommand = new RelayCommand(browseWorkingFileGDBPathAction);
                }

                return _browseWorkingFileGDBPathCommand;
            }
        }

        /// <summary>
        /// Action when the browse working File GDB path button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void BrowseWorkingFileGDBPathClicked(object param)
        {
            _bakWorkingFileGDBPath = _workingFileGDBPath;
            WorkingFileGDBPath = String.Empty;
            WorkingFileGDBPath = GetWorkingFileGDBPath();

            if (String.IsNullOrEmpty(WorkingFileGDBPath))
            {
                WorkingFileGDBPath = _bakWorkingFileGDBPath;
            }
            OnPropertyChanged(nameof(WorkingFileGDBPath));
        }

        /// <summary>
        /// Gets or sets the working File GDB path.
        /// </summary>
        /// <value>
        /// The working File GDB path.
        /// </value>
        public string WorkingFileGDBPath
        {
            get
            {
                return _workingFileGDBPath;
            }
            set
            {
                _workingFileGDBPath = value;
                OnPropertyChanged(nameof(WorkingFileGDBPath));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Prompt the user to set the working File GDB path.
        /// </summary>
        /// <returns>The selected working File GDB path, or null if no path was selected.</returns>
        public static string GetWorkingFileGDBPath()
        {
            try
            {
                string workingFileGDBPath = Settings.Default.WorkingFileGDBPath;

                FolderBrowserDialog openFolderDlg = new()
                {
                    Description = "Select Working File GDB Directory",
                    UseDescriptionForTitle = true,
                    SelectedPath = workingFileGDBPath,
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

        #endregion User GIS

        #region User Updates

        /// <summary>
        /// Gets the reason codes that can be used for attribute updates.
        /// </summary>
        /// <value>The reason codes for attribute updates.</value>
        public HluDataSet.lut_reasonRow[] ReasonCodes
        {
            get
            {
                return _viewModelMain.ReasonCodesWithNone;
            }
        }

        /// <summary>
        /// Gets the process codes that can be used for attribute updates.
        /// </summary>
        /// <value>The process codes for attribute updates.</value>
        public HluDataSet.lut_processRow[] ProcessCodes
        {
            get
            {
                return _viewModelMain.ProcessCodesWithNone;
            }
        }

        /// <summary>
        /// Gets or sets the default reason code for attribute updates.
        /// </summary>
        /// <value>The default reason code.</value>
        public string DefaultReason
        {
            get
            {
                var q = ReasonCodes.Where(h => h.code == _defaultReason);
                if (q.Any())
                    return _defaultReason;
                else
                    return null;
            }
            set
            {
                _defaultReason = value;
                OnPropertyChanged(nameof(DefaultReason));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the default process code for attribute updates.
        /// </summary>
        /// <value>The default process code.</value>
        public string DefaultProcess
        {
            get
            {
                var q = ProcessCodes.Where(h => h.code == _defaultProcess);
                if (q.Any())
                    return _defaultProcess;
                else
                    return null;
            }
            set
            {
                _defaultProcess = value;
                OnPropertyChanged(nameof(DefaultProcess));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to notify the user of split/merge updates when applying updates.
        /// </summary>
        /// <value><c>true</c> if the user should be notified; otherwise, <c>false</c>.</value>
        public bool NotifyOnSplitMerge
        {
            get
            {
                return _notifyOnSplitMerge;
            }
            set
            {
                _notifyOnSplitMerge = value;
                OnPropertyChanged(nameof(NotifyOnSplitMerge));
                NotifyNavigationItemErrorsChanged();
            }
        }

        #endregion User Updates

        #region User SQL

        /// <summary>
        /// Gets or sets the maximum number of value rows to retrieve.
        /// </summary>
        /// <value>
        /// The maximum get value rows.
        /// </value>
        public int? GetValueRows
        {
            get
            {
                return _getValueRows;
            }
            set
            {
                _getValueRows = value;
                OnPropertyChanged(nameof(GetValueRows));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the maximum number of value rows to retrieve.
        /// </summary>
        /// <value>
        /// The maximum get value rows.
        /// </value>
        public int MaxGetValueRows
        {
            get
            {
                return _maxGetValueRows;
            }
        }

        /// <summary>
        /// Get the browse SQL path command.
        /// </summary>
        /// <value>
        /// The browse SQL path command.
        /// </value>
        public ICommand BrowseSQLPathCommand
        {
            get
            {
                if (_browseSQLPathCommand == null)
                {
                    Action<object> browseSQLPathAction = new(this.BrowseSQLPathClicked);
                    _browseSQLPathCommand = new RelayCommand(browseSQLPathAction);
                }

                return _browseSQLPathCommand;
            }
        }

        /// <summary>
        /// Action when the browse SQL path button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void BrowseSQLPathClicked(object param)
        {
            _bakSQLPath = _sqlPath;
            SQLPath = String.Empty;
            SQLPath = GetSQLPath();

            if (String.IsNullOrEmpty(SQLPath))
            {
                SQLPath = _bakSQLPath;
            }
            OnPropertyChanged(nameof(SQLPath));
        }

        /// <summary>
        /// Gets or sets the default SQL path.
        /// </summary>
        /// <value>
        /// The SQL path.
        /// </value>
        public string SQLPath
        {
            get
            {
                return _sqlPath;
            }
            set
            {
                _sqlPath = value;
                OnPropertyChanged(nameof(SQLPath));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Prompt the user to set the default SQL path.
        /// </summary>
        /// <returns>The selected SQL path, or null if no path was selected.</returns>
        public static string GetSQLPath()
        {
            try
            {
                string sqlPath = Settings.Default.SQLPath;

                FolderBrowserDialog openFolderDlg = new()
                {
                    Description = "Select SQL Query Default Path",
                    UseDescriptionForTitle = true,
                    SelectedPath = sqlPath,
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

        #endregion User SQL

        #region User History

        /// <summary>
        /// Gets or sets the list of history columns.
        /// </summary>
        /// <value>The list of history columns.</value>
        public SelectionList<string> HistoryColumns
        {
            get
            {
                return _historyColumns;
            }
            set
            {
                _historyColumns = value;
                OnPropertyChanged(nameof(HistoryColumns));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets or sets the number of history entries to display.
        /// </summary>
        /// <value>The number of history entries to display.</value>
        public int? HistoryDisplayLastN
        {
            get
            {
                return _historyDisplayLastN;
            }
            set
            {
                _historyDisplayLastN = value;
                OnPropertyChanged(nameof(HistoryDisplayLastN));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Gets the maximum number of history entries to display.
        /// </summary>
        /// <value>The maximum number of history entries to display.</value>
        public int MaxHistoryDisplayLastN
        {
            get
            {
                return 50;
            }
        }

        /// <summary>
        /// Gets or sets whether to show geometry measurements (length and area) in the history display.
        /// </summary>
        /// <value><c>true</c> if geometry measurements should be shown; otherwise, <c>false</c>.</value>
        public bool HistoryDisplayGeometry
        {
            get
            {
                return _historyDisplayGeometry;
            }
            set
            {
                _historyDisplayGeometry = value;
                OnPropertyChanged(nameof(HistoryDisplayGeometry));
                NotifyNavigationItemErrorsChanged();
            }
        }

        #endregion User History

        #region User Export

        /// <summary>
        /// Get the browse export path command.
        /// </summary>
        /// <value>
        /// The browse export path command.
        /// </value>
        public ICommand BrowseExportPathCommand
        {
            get
            {
                if (_browseExportPathCommand == null)
                {
                    Action<object> browseExportPathAction = new(this.BrowseExportPathClicked);
                    _browseExportPathCommand = new RelayCommand(browseExportPathAction);
                }

                return _browseExportPathCommand;
            }
        }

        /// <summary>
        /// Action when the browse export path button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void BrowseExportPathClicked(object param)
        {
            _bakExportPath = _exportPath;
            _exportPath = String.Empty;
            _exportPath = GetExportPath();

            if (String.IsNullOrEmpty(_exportPath))
            {
                _exportPath = _bakExportPath;
            }
            OnPropertyChanged(nameof(ExportPath));
        }

        /// <summary>
        /// Gets or sets the default export path.
        /// </summary>
        /// <value>
        /// The export path.
        /// </value>
        public string ExportPath
        {
            get { return _exportPath; }
            set
            {
                _exportPath = value;
                OnPropertyChanged(nameof(ExportPath));
                NotifyNavigationItemErrorsChanged();
            }
        }

        /// <summary>
        /// Prompt the user to set the default export path.
        /// </summary>
        /// <returns>The selected export path, or null if no path was selected.</returns>
        public static string GetExportPath()
        {
            try
            {
                string exportPath = Settings.Default.ExportPath;
                FolderBrowserDialog openFolderDlg = new()
                {
                    Description = "Select Export Default Path",
                    UseDescriptionForTitle = true,
                    SelectedPath = exportPath,
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

        #endregion User Export

        #region Error Handling

        /// <summary>
        /// Validates a specific property and returns its error message.
        /// </summary>
        /// <param name="propertyName">The name of the property to validate.</param>
        /// <returns>The error message for the specified property, or null if there are no errors.</returns>
        private string ValidateProperty(string propertyName)
        {
            return propertyName switch
            {
                // Application - Database options
                "DbConnectionTimeout" when (Convert.ToInt32(DbConnectionTimeout) <= 0 || DbConnectionTimeout == null)
                    => "Error: Enter a database timeout greater than 0 seconds.",

                "IncidTablePageSize" when (Convert.ToInt32(IncidTablePageSize) <= 0 || IncidTablePageSize == null)
                    => "Error: Enter a database page size greater than 0 rows.",
                "IncidTablePageSize" when Convert.ToInt32(IncidTablePageSize) > 1000
                    => "Error: Enter a database page size no more than 1000 rows.",

                // Application - Date options
                "SeasonSpring" when String.IsNullOrEmpty(SeasonSpring)
                    => "Error: You must enter a season name for spring.",
                "SeasonSummer" when String.IsNullOrEmpty(SeasonSummer)
                    => "Error: You must enter a season name for summer.",
                "SeasonAutumn" when String.IsNullOrEmpty(SeasonAutumn)
                    => "Error: You must enter a season name for autumn.",
                "SeasonWinter" when String.IsNullOrEmpty(SeasonWinter)
                    => "Error: You must enter a season name for winter.",

                "VagueDateDelimiter" when String.IsNullOrEmpty(VagueDateDelimiter)
                    => "Error: You must enter a vague date delimiter character.",
                "VagueDateDelimiter" when VagueDateDelimiter.Length > 1
                    => "Error: Vague date delimiter must be a single character.",
                "VagueDateDelimiter" when VagueDateDelimeterRegex().IsMatch(VagueDateDelimiter)
                    => "Error: Vague date delimiter cannot contain letters or numbers.",

                // Application - Validation options
                "HabitatSecondaryCodeValidation" when HabitatSecondaryCodeValidation == null
                    => "Error: Select option of when to validate habitat/secondary codes.",
                "PrimarySecondaryCodeValidation" when PrimarySecondaryCodeValidation == null
                    => "Error: Select option of when to validate primary/secondary codes.",
                "QualityValidation" when QualityValidation == null
                    => "Error: Select option of when to validate determination and interpretation quality.",
                "PotentialPriorityDetermQtyValidation" when PotentialPriorityDetermQtyValidation == null
                    => "Error: Select option of when to validate potential priority habitat determination quality.",

                // Application - Update options
                "SubsetUpdateAction" when SubsetUpdateAction == null
                    => "Error: Select the action to take when updating an incid subset.",
                "ClearIHSUpdateAction" when ClearIHSUpdateAction == null
                    => "Error: Select when to clear IHS codes after an update.",

                "SecondaryCodeDelimiter" when String.IsNullOrEmpty(SecondaryCodeDelimiter)
                    => "Error: You must enter a secondary code delimiter character.",
                "SecondaryCodeDelimiter" when SecondaryCodeDelimiter.Length > 2
                    => "Error: Secondary code delimiter must be one or two characters.",
                "SecondaryCodeDelimiter" when SecondaryCodeDelimeterRegex().IsMatch(SecondaryCodeDelimiter)
                    => "Error: Secondary code delimiter cannot contain letters or numbers.",

                // Application - Bulk Update options
                "BulkDeterminationQuality" when BulkDeterminationQuality == null
                    => "Error: Select the default determination quality for new priority habitats.",
                "BulkInterpretationQuality" when BulkInterpretationQuality == null
                    => "Error: Select the default interpretation quality for new priority habitats.",
                "OSMMSourceId" when OSMMSourceId == null
                    => "Error: Select the default source name for OS MasterMap.",

                // User - Interface options
                "ShowOSMMUpdatesOption" when ShowOSMMUpdatesOption == null
                    => "Error: Select option of when to display any OSMM Updates.",

                "MessageAutoDismissError" when MessageAutoDismissError == null
                    => "Error: Enter a timeout for error messages.",
                "MessageAutoDismissError" when MessageAutoDismissError < 0
                    => "Error: Error message timeout must be 0 (never) or between 1 and 60 seconds.",
                "MessageAutoDismissError" when MessageAutoDismissError > 60
                    => "Error: Error message timeout must not be greater than 60 seconds.",

                "MessageAutoDismissWarning" when MessageAutoDismissWarning == null
                    => "Error: Enter a timeout for warning messages.",
                "MessageAutoDismissWarning" when MessageAutoDismissWarning < 0
                    => "Error: Warning message timeout must be 0 (never) or between 1 and 60 seconds.",
                "MessageAutoDismissWarning" when MessageAutoDismissWarning > 60
                    => "Error: Warning message timeout must not be greater than 60 seconds.",

                "MessageAutoDismissInfo" when MessageAutoDismissInfo == null
                    => "Error: Enter a timeout for info messages.",
                "MessageAutoDismissInfo" when MessageAutoDismissInfo < 0
                    => "Error: Info message timeout must be 0 (never) or between 1 and 60 seconds.",
                "MessageAutoDismissInfo" when MessageAutoDismissInfo > 60
                    => "Error: Info message timeout must not be greater than 60 seconds.",

                "MessageAutoDismissSuccess" when MessageAutoDismissSuccess == null
                    => "Error: Enter a timeout for success messages.",
                "MessageAutoDismissSuccess" when MessageAutoDismissSuccess < 0
                    => "Error: Success message timeout must be 0 (never) or between 1 and 60 seconds.",
                "MessageAutoDismissSuccess" when MessageAutoDismissSuccess > 60
                    => "Error: Success message timeout must not be greater than 60 seconds.",

                // User - Interface preferences (now in Updates tab)
                "DefaultHabitatClass" when DefaultHabitatClass == null
                    => "Error: Select your default habitat class.",
                "DefaultSecondaryGroup" when DefaultSecondaryGroup == null
                    => "Error: Select your default secondary group.",
                "SecondaryCodeOrder" when SecondaryCodeOrder == null
                    => "Error: Select display order of secondary codes.",

                // User - GIS options
                "AutoZoomToSelectionOption" when AutoZoomToSelectionOption == null
                    => "Error: Select option of when to auto zoom to selected feature(s).",
                "MinAutoZoom" when (Convert.ToInt32(MinAutoZoom) < 100 || MinAutoZoom == null)
                    => "Error: Minimum auto zoom scale must be at least 100.",
                "MinAutoZoom" when Convert.ToInt32(MinAutoZoom) > Settings.Default.MaxAutoZoom
                    => $"Error: Minimum auto zoom scale must not be greater than {Settings.Default.MaxAutoZoom}.",
                "MaxFeaturesGISSelect" when (Convert.ToInt32(MaxFeaturesGISSelect) < 0 || MaxFeaturesGISSelect == null)
                    => "Error: Maximum features expected before warning on select must be zero or greater.",
                "MaxFeaturesGISSelect" when Convert.ToInt32(MaxFeaturesGISSelect) > 100000
                    => "Error: Maximum features expected before warning on select must not be greater than 10000. Otherwise set to zero to disable warning",
                "WorkingFileGDBPath" when String.IsNullOrEmpty(WorkingFileGDBPath)
                    => "Error: You must enter a working File Geodatabase path.",


                // User - Updates
                "DefaultReason" when DefaultReason == null
                    => "Error: Select default reason for attribute updates.",
                "DefaultProcess" when DefaultProcess == null
                    => "Error: Select default process for attribute updates.",

                // User - SQL options
                "GetValueRows" when (Convert.ToInt32(GetValueRows) <= 0 || GetValueRows == null)
                    => "Error: Number of value rows to be retrieved must be greater than 0.",
                "GetValueRows" when Convert.ToInt32(GetValueRows) > Settings.Default.MaxGetValueRows
                    => $"Error: Number of value rows to be retrieved must not be greater than {Settings.Default.MaxGetValueRows}.",
                "SQLPath" when String.IsNullOrEmpty(SQLPath)
                    => "Error: You must enter a default SQL path.",

                // User - History options
                "HistoryDisplayLastN" when (Convert.ToInt32(HistoryDisplayLastN) <= 0 || HistoryDisplayLastN == null)
                    => "Error: Number of history rows to be displayed must be greater than 0.",

                // User - Export options
                "ExportPath" when String.IsNullOrEmpty(ExportPath)
                    => "Error: You must enter a default export path.",

                _ => null
            };
        }

        /// <summary>
        /// Gets all validation errors for properties in a specific tab/category.
        /// </summary>
        /// <param name="category">The category of the tab.</param>
        /// <param name="tabName">The name of the tab.</param>
        /// <returns>A list of error messages for the specified tab/category.</returns>
        private List<string> GetErrorsForCategory(string category, string tabName)
        {
            var errors = new List<string>();
            var properties = GetPropertiesForTab(category, tabName);

            foreach (var prop in properties)
            {
                var error = ValidateProperty(prop);
                if (!string.IsNullOrEmpty(error))
                {
                    // Remove "Error: " prefix for cleaner bullet list
                    error = error.Replace("Error: ", "");
                    errors.Add($"• {error}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Maps tab names to their relevant property names.
        /// </summary>
        /// <param name="category">The category of the tab.</param>
        /// <param name="tabName">The name of the tab.</param>
        /// <returns>An array of property names associated with the specified tab.</returns>
        private string[] GetPropertiesForTab(string category, string tabName)
        {
            return (category, tabName) switch
            {
                ("Application", "Database") => ["DbConnectionTimeout", "IncidTablePageSize"],
                ("Application", "Dates") => ["SeasonSpring", "SeasonSummer", "SeasonAutumn", "SeasonWinter", "VagueDateDelimiter"],
                ("Application", "Validation") => ["HabitatSecondaryCodeValidation", "PrimarySecondaryCodeValidation", "QualityValidation", "PotentialPriorityDetermQtyValidation"],
                ("Application", "Updates") => ["SubsetUpdateAction", "ClearIHSUpdateAction", "SecondaryCodeDelimiter"],
                ("Application", "Bulk Update") => ["BulkDeterminationQuality", "BulkInterpretationQuality", "OSMMSourceId"],
                ("User", "Interface") => ["ShowOSMMUpdatesOption", "MessageAutoDismissError", "MessageAutoDismissWarning", "MessageAutoDismissInfo", "MessageAutoDismissSuccess"],
                ("User", "GIS") => ["AutoZoomToSelectionOption", "MinAutoZoom", "MaxFeaturesGISSelect", "WorkingFileGDBPath"],
                ("User", "Updates") => ["DefaultReason", "DefaultProcess", "DefaultHabitatClass", "DefaultSecondaryGroup", "SecondaryCodeOrder", "NotifyOnSplitMerge"],
                ("User", "SQL") => ["GetValueRows", "SQLPath"],
                ("User", "History") => ["HistoryColumns", "HistoryDisplayGeometry", "HistoryDisplayLastN"],
                ("User", "Export") => ["ExportPath"],
                _ => []
            };
        }

        /// <summary>
        /// Are there any errors in the settings?
        /// </summary>
        /// <value></value>
        /// A string containing all error messages, or null if there are no errors.
        /// </value>
        public string Error
        {
            get
            {
                // Check all navigation items for errors
                var hasAnyErrors = NavigationItems?.Any(item => HasErrorsForNavigationItem(item)) ?? false;

                if (!hasAnyErrors)
                    return null;

                // Collect all errors
                var allErrors = new StringBuilder();
                foreach (var item in NavigationItems)
                {
                    var errors = GetErrorsForCategory(item.Category, item.Name);
                    foreach (var error in errors)
                    {
                        allErrors.AppendLine(error);
                    }
                }

                return allErrors.Length > 0 ? allErrors.ToString() : null;
            }
        }

        /// <summary>
        /// Get the error message for the specified property.
        /// </summary>
        /// <value>
        /// The error message for the specified property, or null if there are no errors.
        /// </value>
        public string this[string columnName]
        {
            get
            {
                var error = ValidateProperty(columnName);

                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        #endregion Error Handling

        #region Error Display in Navigation

        /// <summary>
        /// Checks if a specific navigation item has validation errors.
        /// </summary>
        /// <param name="item">The navigation item to check.</param>
        /// <returns>True if the item has errors, otherwise false.</returns>
        private bool HasErrorsForNavigationItem(NavigationItem item)
        {
            if (item == null)
                return false;

            // Get all properties for this tab
            var properties = GetPropertiesForTab(item.Category, item.Name);

            // Check if any property has errors
            return properties.Any(prop => !string.IsNullOrEmpty(ValidateProperty(prop)));
        }

        /// <summary>
        /// Gets the specific error message for a navigation item.
        /// </summary>
        /// <param name="item">The navigation item to get errors for.</param>
        /// <returns>A formatted error message describing the validation errors.</returns>
        private string GetErrorMessageForNavigationItem(NavigationItem item)
        {
            if (item == null || !HasErrorsForNavigationItem(item))
                return null;

            StringBuilder errorMsg = new();
            errorMsg.AppendLine("This tab contains validation errors:");
            errorMsg.AppendLine();

            var errors = GetErrorsForCategory(item.Category, item.Name);
            errorMsg.Append(string.Join(Environment.NewLine, errors));

            return errorMsg.ToString().TrimEnd();
        }

        /// <summary>
        /// Updates the error state of all navigation items.
        /// </summary>
        private void NotifyNavigationItemErrorsChanged()
        {
            foreach (var item in NavigationItems)
            {
                item.HasErrors = HasErrorsForNavigationItem(item);
                item.ErrorMessage = GetErrorMessageForNavigationItem(item);
            }
        }

        /// <summary>
        /// Gets the error messages related to database settings.
        /// </summary>
        /// <returns>A string containing all database-related error messages.</returns>
        private string GetDatabaseErrorMessages()
        {
            var errors = GetErrorsForCategory("Application", "Database");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to date settings.
        /// </summary>
        /// <returns>A string containing all date-related error messages.</returns>
        private string GetDatesErrorMessages()
        {
            var errors = GetErrorsForCategory("Application", "Dates");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to validation settings.
        /// </summary>
        /// <returns>A string containing all validation-related error messages.</returns>
        private string GetValidationErrorMessages()
        {
            var errors = GetErrorsForCategory("Application", "Validation");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to update settings.
        /// </summary>
        /// <returns>A string containing all update-related error messages.</returns>
        private string GetUpdatesErrorMessages()
        {
            var errors = GetErrorsForCategory("Application", "Updates");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to bulk update settings.
        /// </summary>
        /// <returns>A string containing all bulk update-related error messages.</returns>
        private string GetBulkUpdateErrorMessages()
        {
            var errors = GetErrorsForCategory("Application", "Bulk Update");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to user interface settings.
        /// </summary>
        /// <returns>A string containing all user interface-related error messages.</returns>
        private string GetInterfaceErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "Interface");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to GIS settings.
        /// </summary>
        /// <returns>A string containing all GIS-related error messages.</returns>
        private string GetGISErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "GIS");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to user updates settings.
        /// </summary>
        /// <returns>A string containing all user updates-related error messages.</returns>
        private string GetUserUpdatesErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "Updates");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to user SQL settings.
        /// </summary>
        /// <returns>A string containing all user SQL-related error messages.</returns>
        private string GetSQLErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "SQL");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to history settings.
        /// </summary>
        /// <returns>A string containing all history-related error messages.</returns>
        private string GetHistoryErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "History");
            return string.Join(Environment.NewLine, errors);
        }

        /// <summary>
        /// Gets the error messages related to export settings.
        /// </summary>
        /// <returns>A string containing all export-related error messages.</returns>
        private string GetExportErrorMessages()
        {
            var errors = GetErrorsForCategory("User", "Export");
            return string.Join(Environment.NewLine, errors);
        }

        #endregion Error Display in Navigation

        #region Regular Expressions

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

        #endregion Regular Expressions
    }
}