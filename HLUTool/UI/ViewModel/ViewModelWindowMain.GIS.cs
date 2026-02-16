// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU.Data;
using HLU.Data.Model;
using HLU.GISApplication;
using HLU.Properties;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// GIS operations partial for ViewModelWindowMain.
    /// Contains: All ArcGIS Pro SDK interactions, map selection, layer management, spatial operations.
    /// </summary>
    partial class ViewModelWindowMain
    {
        #region Fields

        #region Fields - GIS

        private ArcProApp _gisApp;
        private GeometryTypes _gisLayerType = GeometryTypes.Polygon;

        private DataColumn[] _gisIDColumns;
        private int[] _gisIDColumnOrdinals;

        #endregion Fields - GIS

        #region Fields - Event Tracking

        private bool _mapEventsSubscribed;
        private bool _mapMemberEventsSubscribed;
        private bool _projectClosedEventsSubscribed;
        private bool _layersChangedEventsSubscribed;

        #endregion Fields - Event Tracking

        #region Fields - Active Map/Layer

        private MapView _activeMapView;
        string _activeLayerName;

        #endregion Fields - Active Map/Layer

        #region Fields - Selection State

        // The IDs of the currently selected incids, toids and fragments in GIS (set in AnalyzeGisSelectionSet).
        private IEnumerable<string> _incidsSelectedMap;
        private IEnumerable<string> _toidsSelectedMap;
        private IEnumerable<string> _fragsSelectedMap;

        // How many incids, toids and fragments are selected in GIS (set in AnalyzeGisSelectionSet).
        private int _selectedIncidsInGISCount = 0;
        private int _selectedToidsInGISCount = 0;
        private int _selectedFragsInGISCount = 0;

        // How many incids and fragments are selected in the database for the current GIS selection
        // (set whenever a filter is applied and in ExpectedSelectionFeatures).
        private int _selectedIncidsInDBCount = 0;
        private int _selectedFragsInDBCount = 0;

        // How many toids and fragments are selected in GIS and the database for the current incid (set in CountToidFrags).
        private int _currentIncidToidsInGISCount = 0;
        private int _currentIncidFragsInGISCount = 0;
        private int _currentIncidToidsInDBCount = 0;
        private int _currentIncidFragsInDBCount = 0;

        #endregion Fields - Selection State

        #region Fields - Split/Merge

        private bool _canPhysicallySplit;
        private bool _canLogicallySplit;
        private bool _canPhysicallyMerge;
        private bool _canLogicallyMerge;

        #endregion Fields - Split/Merge

        #endregion Fields

        #region Properties

        #region Properties - GIS Info

        /// <summary>
        /// Gets the geometry type of the GIS layer represented by this instance.
        /// </summary>
        /// <remarks>Use this property to determine the spatial data type (such as point, line, or
        /// polygon) associated with the GIS layer. The value can be used to guide rendering, analysis, or data
        /// processing operations that depend on the layer's geometry.</remarks>
        public GeometryTypes GisLayerType { get { return _gisLayerType; } }

        #endregion Properties - GIS Info

        #region Properties - Selection State

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

        #endregion Properties - Selection State

        #region Properties - Active Layer

        /// <summary>
        /// Gets or sets the name of the active layer to display in the status bar.
        /// </summary>
        /// <value>
        /// The name of the active layer.
        /// </value>
        public string ActiveLayerName
        {
            get
            {
                // Return the active layer name.
                return _activeLayerName ?? string.Empty;
            }

            set
            {
                // If the active layer name hasn't changed do nothing.
                if (string.Equals(_activeLayerName, value, StringComparison.Ordinal))
                    return;

                // Set the active layer name.
                _activeLayerName = value;
                OnPropertyChanged(nameof(ActiveLayerName));

                // Update the dock pane caption.
                UpdateDockPaneCaption();
            }
        }

        /// <summary>
        /// Can the GIS layer be switched?
        /// </summary>
        public bool CanSwitchGISLayer
        {
            get
            {
                // Enable switching only when not in bulk update mode or OSMM update mode.
                if (IsNotBulkMode && IsNotOsmmReviewMode)
                {
                    // Get the total number of map layers
                    int mapLayersCount = _gisApp.ListHluLayers();

                    // Return true if there is more than one map layer
                    return mapLayersCount > 1;
                }
                else
                    return false;
            }
        }

        #endregion Properties - Active Layer

        #region Properties - Map Selection

        /// <summary>
        /// Can the current incid be selected on the map.
        /// </summary>
        public bool CanSelectOnMap
        {
            get { return IsNotBulkMode && IsNotOsmmReviewMode && IncidCurrentRow != null; }
        }

        /// <summary>
        /// GetMapSelectionAsync command.
        /// </summary>
        public ICommand GetMapSelectionCommand
        {
            get
            {
                if (_getMapSelectionCommand == null)
                {
                    Action<object> getMapSelectionAction = new(this.GetMapSelectionClicked);
                    _getMapSelectionCommand = new RelayCommand(getMapSelectionAction, param => this.CanGetMapSelection);
                }
                return _getMapSelectionCommand;
            }
        }

        /// <summary>
        /// Can the map selection be read.
        /// </summary>
        public bool CanGetMapSelection
        {
            // Can get map selection if not in bulk update mode.
            get { return IsNotBulkMode; }
        }

        #endregion Properties - Map Selection

        #region Properties - Selection Counts

        /// <summary>
        /// Gets or sets the count of selected Incids in the database, which is used for status display and selection logic.
        /// </summary>
        public int SelectedIncidsInDBCount
        {
            get { return _selectedIncidsInDBCount; }
            set { _selectedIncidsInDBCount = value; }
        }

        /// <summary>
        /// Gets or sets the count of selected Frags in the database, which is used for status display and selection logic.
        /// </summary>
        public int SelectedFragsInDBCount
        {
            get { return _selectedFragsInDBCount; }
            set { _selectedFragsInDBCount = value; }
        }

        /// <summary>
        /// Gets or sets the count of selected Incids in GIS, which is used for status display and selection logic.
        /// </summary>
        public int SelectedIncidsInGISCount
        {
            get { return _selectedIncidsInGISCount; }
            set { }
        }

        /// <summary>
        /// Gets or sets the count of selected Toids in GIS, which is used for status display and selection logic.
        /// </summary>
        public int SelectedToidsInGISCount
        {
            get { return _selectedToidsInGISCount; }
            set { }
        }

        /// <summary>
        /// Gets or sets the count of selected Frags in GIS, which is used for status display and selection logic.
        /// </summary>
        public int SelectedFragsInGISCount
        {
            get { return _selectedFragsInGISCount; }
            set { }
        }

        #endregion Properties - Selection Counts

        #region Properties - Map Zoom

        /// <summary>
        /// Can the map be zoomed to the current selection?
        /// </summary>
        public bool CanZoomToSelection { get { return _gisSelection != null; } }

        #endregion Properties - Map Zoom

        #region Properties - Split/Merge Commands

        /// <summary>
        /// Can a physical split operation be performed?
        /// </summary>
        public bool CanPhysicallySplit => _canPhysicallySplit;

        /// <summary>
        /// Can a logical split operation be performed?
        /// </summary>
        public bool CanLogicallySplit => _canLogicallySplit;

        /// <summary>
        /// Can a physical merge operation be performed?
        /// </summary>
        public bool CanPhysicallyMerge => _canPhysicallyMerge;

        /// <summary>
        /// Can a logical merge operation be performed?
        /// </summary>
        public bool CanLogicallyMerge => _canLogicallyMerge;

        /// <summary>
        ///  Can a split operation (physical or logical) be performed?
        /// </summary>
        public bool CanSplit => _canPhysicallySplit || _canLogicallySplit;

        /// <summary>
        ///  Can a merge operation (physical or logical) be performed?
        /// </summary>
        public bool CanMerge => _canPhysicallyMerge || _canLogicallyMerge;

        #endregion Properies - Split/Merge Commands

        #endregion Properties

        #region Methods

        #region GIS Events

        /// <summary>
        /// Event when the active map view changes. Checks that there is an active map view and
        /// that it contains a valid HLU layer, then shows or hides the UI controls as appropriate.
        /// </summary>
        /// <param name="obj"></param>
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

        /// <summary>
        /// Handles the event that occurs when new layers are added to the map.
        /// </summary>
        /// <remarks>This method verifies that there is an active map with a valid HLU map before
        /// proceeding with further operations.</remarks>
        /// <param name="args">An object that contains event data for the layers added event.</param>
        private async void OnLayersAdded(LayerEventsArgs args)
        {
            // Check that there is an active map and that it contains a valid HLU map.
            if (!await CheckActiveMapAsync())
            {
                _activeMapView = null;
                return;
            }
        }

        /// <summary>
        /// Handles the event that occurs when layers are removed from the map. If the active
        /// layer is removed, it forces the creation of a new GIS functions object. It also
        /// checks for an active map with a valid HLU map and refreshes the layer name if necessary.
        /// </summary>
        /// <param name="args"></param>
        private async void OnLayersRemoved(LayerEventsArgs args)
        {
            foreach (var layer in args.Layers)
            {
                // If the active layer has been removed force
                // a new GIS functions object to be created.
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

        /// <summary>
        /// Event when the project is closed. Recomputes whether editing is currently possible and makes the UI controls hidden.
        /// </summary>
        /// <param name="obj"></param>
        private void OnProjectClosed(ProjectEventArgs obj)
        {
            // Recomputes whether editing is currently possible.
            RefreshEditCapability();

            if (MapView.Active == null)
            {
                // Make the UI controls hidden.
                GridMainVisibility = Visibility.Hidden;
            }

            _projectClosedEventsSubscribed = false;

            ProjectClosedEvent.Unsubscribe(OnProjectClosed);
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

        /// <summary>
        /// Handles map member property changes and refreshes edit capability if the active HLU layer is affected.
        /// </summary>
        private void OnMapMemberPropertiesChanged(MapMemberPropertiesChangedEventArgs args)
        {
            if (_gisApp?.ActiveHluLayer == null)
                return;

            if (_gisApp?.HluLayer == null)
                return;

            for (int i = 0; i < args.MapMembers.Count; i++)
            {
                if (!ReferenceEquals(args.MapMembers[i], _gisApp.HluLayer))
                    continue;

                // Reassess edit capability of the current layer asynchronously.
                _ = RefreshIsLayerEditableAsync();

                // Recomputes whether editing is currently possible.
                RefreshEditCapability();

                return;
            }
        }

        #endregion GIS Events

        #region Select On Map Command

        /// <summary>
        /// Selects the current incid on the map and refreshes internal selection state.
        /// </summary>
        internal async Task SelectCurrentOnMapAsync()
        {
            if (_gisApp == null)
            {
                MessageBox.Show("GIS application is not initialised.", "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_incidCurrentRow == null || String.IsNullOrWhiteSpace(_incidCurrentRow.incid))
            {
                MessageBox.Show("No current incid is set.", "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Set the status to processing and the cursor to wait.
                ChangeCursor(Cursors.Wait, "Selecting in GIS ...");

                // Select the current incid on the map.
                if (!await _gisApp.SelectIncidOnMapAsync(_incidCurrentRow.incid))
                {
                    // Display a warning message.
                    ShowMessage("Error selecting current incid in GIS.", MessageType.Warning);

                    return;
                }

                // Initialise the GIS selection table schema.
                _gisSelection = NewGisSelectionTable();

                // Read selection back into the passed table schema.
                _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection).ConfigureAwait(false);

                // Analyse the results of the GIS selection by counting the number of
                // incids, toids and fragments selected. Do not overwrite DB filter selection.
                _incidSelectionWhereClause = null;
                AnalyzeGisSelectionSet(false);

                // Indicate the selection did not originate from "Get map selection".
                _filteredByMap = false;

                // Warn the user that no features were found in GIS.
                if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                {
                    //MessageBox.Show("No features for incid found in active layer.", "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Display a warning message.
                    ShowMessage("No features for incid found in active layer.", MessageType.Warning);

                    return;
                }

                //TODO: Remove minZoom if not needed
                // Zoom to the GIS selection (if auto zoom configured).
                await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);
            }
            catch (HLUToolException ex)
            {
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Make sure the cursor is always reset.
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow, null);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        /// <summary>
        /// Select the current incid record on the map.
        /// </summary>
        /// <param name="updateIncidSelection">Should the incid selection be updated afterwards?</param>
        public async Task SelectOnMapAsync(bool updateIncidSelection)
        {
            if (IncidCurrentRow == null) return;

            // Temporarily store the incid and GIS selections whilst
            // selecting the current incid in GIS so that the selections
            // can be restored again afterwards (so that the filter is
            // still active).
            try
            {
                DataTable prevIncidSelection = NewIncidSelectionTable();
                DataTable prevGISSelection = NewGisSelectionTable();

                // Save the current table of selected incids.
                prevIncidSelection = _incidSelection;

                // Save the current table of selected GIS features.
                prevGISSelection = _gisSelection;

                // Reset the table of selected incids.
                _incidSelection = NewIncidSelectionTable();

                // Determine if a filter with more than one incid is currently active.
                bool multiIncidFilter = (IsFiltered && _incidSelection.Rows.Count > 1);

                // Set the table of selected incids to the current incid.
                DataRow selRow = _incidSelection.NewRow();
                foreach (DataColumn c in _incidSelection.Columns)
                    selRow[c] = IncidCurrentRow[c.ColumnName];
                _incidSelection.Rows.Add(selRow);

                // Select all the features for the current incid in GIS.
                await PerformGisSelectionAsync(false, -1, -1);

                // If a multi-incid filter was previously active then restore it.
                if (multiIncidFilter)
                {
                    // Restore the previous table of selected incids.
                    _incidSelection = prevIncidSelection;

                    // Count the number of fragments previously selected for this incid.
                    int numFragsOld = 0;
                    if (prevGISSelection != null)
                    {
                        DataRow[] gisRows = prevGISSelection.AsEnumerable()
                            .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                        numFragsOld = gisRows.Length;
                    }

                    // Count the number of fragments now selected for this incid.
                    int numFragsNew = 0;
                    if (_gisSelection != null)
                    {
                        DataRow[] gisRows = _gisSelection.AsEnumerable()
                            .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                        numFragsNew = gisRows.Length;
                    }

                    // Check if the number of fragments now selected for this incid
                    // has changed.
                    if (numFragsNew == numFragsOld)
                    {
                        // If the same number of fragments for this incid has been
                        // selected then just restore the previous table.
                        _gisSelection = prevGISSelection;
                    }
                    else
                    {
                        // If the number of fragments selected has changed for this
                        // incid then add all the rows for all the other incids in
                        // the previous table of selected GIS features to the current
                        // table of selected GIS features (thereby replacing the previously
                        // selected features for the current incid with the new selection).
                        if (prevGISSelection != null)
                        {
                            selRow = _gisSelection.NewRow();
                            foreach (DataRow row in prevGISSelection.Rows)
                            {
                                if (row[HluDataset.incid.incidColumn.ColumnName].ToString() != _incidCurrentRow.incid)
                                    _gisSelection.ImportRow(row);
                            }
                        }
                    }

                    // Analyse the results of the GIS selection by counting
                    // the number of incids, toids and fragments selected.
                    AnalyzeGisSelectionSet(updateIncidSelection);

                }
                else
                {
                    // Restore the previous table of selected incids.
                    _incidSelection = prevIncidSelection;

                    // Restore the previous table of selected GIS features.
                    //_gisSelection = prevGISSelection;

                    // Analyse the results of the GIS selection by counting
                    // the number of incids, toids and fragments selected.
                    AnalyzeGisSelectionSet(false);

                    // Set the filter back to the first incid.
                    //SetFilter();
                }

                // Indicate the selection didn't come from the map.
                _filteredByMap = false;

                // Warn the user that no features were found in GIS.
                if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                {
                    //MessageBox.Show("No features for incid found in active layer.", "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Display a warning message.
                    ShowMessage("No features for incid found in active layer.", MessageType.Warning);
                    return;
                }
                else
                {
                    ClearMessage();
                }

                //TODO: Remove minZoom if not needed
                // Zoom to the GIS selection (if auto zoom configured).
                await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Select On Map Command

        #region Get Map Selection

        /// <summary>
        /// Gets the map selection. Called when the command is invoked.
        /// </summary>
        /// <param name="param"></param>
        internal async void GetMapSelectionClicked(object param)
        {
            // Get the GIS layer selection and warn the user if no
            // features are found (don't wait).
            await GetMapSelectionAsync(true);
        }

        /// <summary>
        /// Gets the map selection from the GIS application.
        /// </summary>
        /// <param name="showMessage"></param>
        /// <returns></returns>
        internal async Task GetMapSelectionAsync(bool showMessage)
        {
            try
            {
                // Check there are no outstanding edits.
                MessageBoxResult userResponse = CheckDirty();

                // Process based on the response ...
                // Yes = move to the new incid
                // No = move to the new incid
                // Cancel = don't move to the new incid
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

                ChangeCursor(Cursors.Wait, "Getting map selection ...");

                // Initialise the GIS selection table.
                _gisSelection = NewGisSelectionTable();

                // Read which features are selected in GIS (passing it a new
                // GIS selection table so that it knows the columns to return.
                try
                {
                    _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);
                }
                catch (HLUToolException ex)
                {
                    // Preserve stack trace and wrap in a meaningful type
                    MessageBox.Show(ex.Message, "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Count how many incids, toids and fragments are selected in GIS
                _incidSelectionWhereClause = null;
                AnalyzeGisSelectionSet(true);

                // Update the number of features found in the database.
                _selectedFragsInDBCount = await ExpectedSelectionFeatures(_incidSelectionWhereClause);

                // Store the number of incids found in the database
                _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                // If any GIS features were found.
                if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                {
                    // Indicate the selection came from the map.
                    _filteredByMap = true;

                    // Prevent OSMM updates being actioned too quickly.
                    if (IsNotOsmmBulkMode && IsOsmmReviewMode)
                    {
                        // Indicate there are more OSMM updates to review.
                        _osmmUpdatesEmpty = false;
                        OnPropertyChanged(nameof(CanOSMMAccept));
                        OnPropertyChanged(nameof(CanOSMMSkip));
                    }

                    // Set flag so that the user isn't prompted to save
                    // any pending edits (again).
                    _readingMap = true;

                    // Reset the moving flag in case something has gone wrong earlier.
                    _moving = false;

                    // Set the filter to the first incid.
                    await SetFilterAsync();

                    // Reset the flag again.
                    _readingMap = false;

                    ChangeCursor(Cursors.Arrow, null);

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Map", showMessage: showMessage);

                    // Clear any messages.
                    ClearMessage();
                }
                else
                {
                    // Reset the incid and map selections and move
                    // to the first incid in the database.
                    await ClearFilterAsync(true);

                    // Indicate the selection didn't come from the map (but only after
                    // the filter has been cleared and the first incid selected so that
                    // the map doesn't auto zoom to the incid).
                    _filteredByMap = false;

                    ChangeCursor(Cursors.Arrow, null);

                    // Display a warning message.
                    ShowMessage("No map features selected in active layer.", MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow, null);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Get Map Selection

        #region Select All On Map Command

        /// <summary>
        /// Select all the incids in the active filter in GIS.
        /// </summary>
        internal async Task SelectAllOnMapAsync()
        {
            // If there are any records in the selection (and the tool is
            // not currently in bulk update mode).
            if (!IsFiltered)
                return;

            try
            {
                // Set the status to processing and the cursor to wait.
                ChangeCursor(Cursors.Wait, "Selecting in GIS ...");

                //TODO: Needed?
                // Let WPF render the cursor/message before heavy work begins.
                //await Dispatcher.Yield(DispatcherPriority.Background);

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                //TODO: Keep or replace with GIS query?
                // Build a where clause list for the incids to be selected.
                List<List<SqlFilterCondition>> whereClause = [ScratchDb.GisWhereClause(_incidSelection, null, false)];

                // Find the expected number of features to be selected in GIS
                // (by querying the database).
                int expectedNumFeatures = await ExpectedSelectionFeatures(whereClause);

                // Find the expected number of incids to be selected in GIS.
                int expectedNumIncids = _incidSelection.Rows.Count;

                // Select the required incid(s) in GIS and read the selection.
                if (await PerformGisSelectionAsync(true, expectedNumFeatures, expectedNumIncids))
                {
                    // Analyse the results of the GIS selection by counting the number of
                    // incids, toids and fragments selected. Do not overwrite DB filter selection.
                    AnalyzeGisSelectionSet(false);

                    //TODO: Now doing this before SetFilterAsync() otherwise _selectedFragsInGISCount may
                    // have been changed when auto selecting the first incid.
                    // Check if the counts returned are less than those expected.
                    if (_selectedFragsInGISCount < _selectedFragsInDBCount)
                    {
                        MessageBox.Show("Not all selected features found in active layer.", "HLU: Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    // Indicate the selection came from the map.
                    if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                        _filteredByMap = true;

                    // Set the filter back to the first incid.
                    await SetFilterAsync();

                    // Warn the user that no features were found in GIS.
                    if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                    {
                        MessageBox.Show("No incid features found in active layer.", "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    //TODO: Remove minZoom if not needed
                    // Zoom to the GIS selection (if auto zoom configured).
                    await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);

                    // Check if the GIS and database are in sync.
                    CheckInSync("Selection", "Incid", "Not all incid");
                }
                else
                {
                    // Restore the previous selection (filter).
                    _incidSelection = incidSelectionBackup;
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;
                MessageBox.Show(ex.Message, "HLU: Selection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Make sure the cursor is always reset.
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow, null);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Select All On Map Command

        #region Selection Tables

        /// <summary>
        /// Initialise the GIS selection table.
        /// </summary>
        /// <returns></returns>
        private DataTable NewGisSelectionTable()
        {
            DataTable outTable = new();
            foreach (DataColumn c in _gisIDColumns)
                outTable.Columns.Add(new DataColumn(c.ColumnName, c.DataType));
            return outTable;
        }

        #endregion Selection Tables

        #region Map Selection

        /// <summary>
        /// Calculates the expected number of GIS features to be selected
        /// by the sql query based upon a list of conditions.
        /// </summary>
        /// <param name="whereClause">The list of where clause conditions.</param>
        /// <returns>A tuple of integers of the number of toids and fragments expected to be selected.</returns>
        private async Task<int> ExpectedSelectionFeatures(List<List<SqlFilterCondition>> whereClause)
        {
            int numFragments = 0;

            // Track distinct TOIDs across all chunks for an exact unique count.
            HashSet<string> distinctToids = new(StringComparer.OrdinalIgnoreCase);

            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                (whereClause != null) && (whereClause.Count > 0))
            {
                try
                {
                    HluDataSet.incid_mm_polygonsDataTable t = new();
                    DataTable[] selTables = new DataTable[] { t }.ToArray();

                    IEnumerable<DataTable> queryTables = whereClause.SelectMany(cond => cond.Select(c => c.Table)).Distinct();
                    //DataTable[] selTables = new DataTable[] { t }.Union(queryTables).ToArray();

                    var fromTables = queryTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = selTables.Concat(fromTables).ToArray();

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    // If there is only one long list then chunk
                    // it up into smaller lists.
                    if (whereClause.Count == 1)
                    {
                        try
                        {
                            List<SqlFilterCondition> whereCond = [];
                            whereCond = whereClause[0];

                            // Chunk only at top-level OR boundaries while tracking parentheses.
                            // This avoids splitting matched pairs such as "incid >= 1" AND "incid <= 4".
                            whereClause = whereCond.ChunkClauseTopLevel(50, 500).ToList();
                        }
                        catch
                        {
                            // Ignore chunking failures.
                        }
                    }

                    for (int i = 0; i < whereClause.Count; i++)
                    {
                        // If the where conditions are going to be appended
                        // to some join conditions then change the boolean
                        // operator before the first where condition to "AND"
                        // and wrap the where conditions in an extra set of
                        // parentheses.
                        if (joinCond.Any())
                        {
                            List<SqlFilterCondition> whereCond = [];
                            whereCond = whereClause[i];

                            SqlFilterCondition cond = new();
                            cond = whereCond[0];
                            cond.BooleanOperator = "AND";
                            cond.OpenParentheses = "((";

                            cond = whereCond[whereCond.Count - 1];
                            cond.CloseParentheses = "))";
                        }

                        numFragments += await _db.SqlCount(selTables, "*", joinCond.Concat(whereClause[i]).ToList());
                    }
                }
                catch
                {
                    // Ignore failures.
                }
            }

            return numFragments;
        }

        /// <summary>
        /// Calculates the expected number of GIS features to be selected
        /// by the sql query, based upon a list of data tables and a sql
        /// where clause, in the database.
        /// </summary>
        /// <param name="sqlFromTables">The list of data tables.</param>
        /// <param name="sqlWhereClause">The where clause string.</param>
        /// <returns>An integer of the number of GIS features expected to be selected.</returns>
        private async Task<int> ExpectedSelectionFeatures(List<DataTable> sqlFromTables, string sqlWhereClause)
        {
            int numFragments = 0;

            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                sqlFromTables.Count != 0)
            {
                try
                {
                    HluDataSet.incid_mm_polygonsDataTable t = new();
                    DataTable[] selTables = new DataTable[] { t }.ToArray();

                    var fromTables = sqlFromTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = selTables.Concat(fromTables).ToArray();

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, t.incidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    numFragments = await _db.SqlCount(whereTables, "*", joinCond.ToList(), sqlWhereClause);

                    // Create a selection DataTable of PK values of IncidMMPolygons.
                    _incidMMPolygonSelection = _db.SqlSelect(true, false, _hluDS.incid_mm_polygons.PrimaryKey, whereTables.ToList(), joinCond.ToList(), sqlWhereClause);

                    //TODO: Temporary check.
                    if (numFragments != _incidMMPolygonSelection.Rows.Count)
                        Debug.Print("Diff");

                    //TODO: Why is this set twice?
                    // Count the number of fragments from the selection table.
                    numFragments = _incidMMPolygonSelection.Rows.Count;

                    //TODO: Change "*" to distinct concatenation of incid, toid and toid fragments?
                    //numFragments = _db.SqlCount(whereTables, String.Format("Distinct Convert(varchar, {0}.{1}) + Convert(varchar, {0}.{2}) + Convert(varchar, {0}.{3})",
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.TableName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                    //    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidfragidColumn.ColumnName)),
                    //    joinCond.ToList(), sqlWhereClause);
                }
                catch { }
            }

            return numFragments;
        }

        /// <summary>
        /// Performs the GIS selection for the current incid selection.
        /// In ArcGIS Pro this selects directly on the active HLU FeatureLayer and then
        /// reads the selection back into <see cref="_gisSelection"/>.
        /// </summary>
        /// <param name="confirmSelect">
        /// If true, show the "warn before GIS select" dialog depending on user options.
        /// </param>
        /// <param name="expectedNumFeatures">
        /// Expected number of features (for warning/integrity messaging).
        /// </param>
        /// <param name="expectedNumIncids">
        /// Expected number of incids (for warning/integrity messaging).
        /// </param>
        /// <returns>
        /// True if selection was performed and at least one row was selected; otherwise false.
        /// </returns>
        private async Task<bool> PerformGisSelectionAsync(
            bool confirmSelect,
            int expectedNumFeatures,
            int expectedNumIncids)
        {
            if (_gisApp == null)
                return false;

            // Respect existing warning behaviour.
            // In the old ArcMap code you sometimes warned only for joins; in Pro we no longer join,
            // so the dialog should be treated as a general "large selection" warning.
            if (confirmSelect)
            {
                // Confirm the GIS selection with the user if required based on user options and
                // the expected number of features to be selected in GIS.
                bool proceed = ConfirmGISSelect(
                    expectedNumFeatures: expectedNumFeatures,
                    expectedNumIncids: expectedNumIncids);

                if (!proceed)
                    return false;
            }

            // Select all INCIDs in _incidSelection on the active layer
            if (!await _gisApp.SelectIncidsOnMapAsync(_incidSelection))
            {
                // Display a warning message.
                ShowMessage("Error selecting current incid in GIS.", MessageType.Warning);

                return false;
            }

            // Ensure the output selection table has the right schema before filling it.
            _gisSelection = NewGisSelectionTable();

            // Read the current GIS selection back into the output selection table.
            _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection).ConfigureAwait(false);

            // Return true only if we actually got selected rows back.
            return (_gisSelection != null) && (_gisSelection.Rows.Count > 0);
        }

        #endregion Map Selection

        #region Count Features

        /// <summary>
        /// Count the number of toids and fragments for the current incid
        /// selected in the GIS and in the database.
        /// </summary>
        public void CountCurrentIncidToidFrags()
        {
            // Count the number of toids and fragments for this incid selected
            // in the GIS. They are counted here, once when the incid changes,
            // instead of in StatusIncid() which is constantly being called.
            if (_gisSelection != null)
            {
                DataRow[] gisRows = _gisSelection.AsEnumerable()
                    .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid)).ToArray();
                _currentIncidToidsInGISCount = gisRows.GroupBy(r => r[HluDataset.incid_mm_polygons.toidColumn.ColumnName]).Count();
                _currentIncidFragsInGISCount = gisRows.Length;
            }
            else
            {
                _currentIncidToidsInGISCount = 0;
                _currentIncidFragsInGISCount = 0;
            }

            // Count the total number of toids in the database for
            // this incid.
            _currentIncidToidsInDBCount = (int)_db.ExecuteScalar(String.Format(
                    "SELECT COUNT(*) FROM (SELECT DISTINCT {0} FROM {1} WHERE {2} = {3}) AS T",
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(_incidCurrentRow.incid)),
                    _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of fragments in the database for
            // this incid.
            _currentIncidFragsInDBCount = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}",
                _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                _db.QuoteValue(_incidCurrentRow.incid)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
        }

        #endregion Count Features

        #region Check Selected Features

        /// <summary>
        /// Checks the selected toid frags for the current selection in GIS against the database counts,
        /// and returns false if there are more toids or frags in GIS than in the database, or if there
        /// are no frags in the database for a physical split.
        /// <param name="physicalSplit">if set to <c>true</c> [physical split].</param>
        /// <returns>
        /// <c>true</c> if the selected toid frags are valid; otherwise, <c>false</c>.
        /// </returns>
        public bool CheckSelectedToidFrags(bool physicalSplit)
        {
            foreach (DataRow row in _incidSelection.Rows)
            {
                string incid = row[HluDataset.incid.incidColumn.ColumnName].ToString();

                // Count the number of toids and fragments for all incids in
                // the selection in the GIS.
                int toidsIncidSelectionGisCount = 0;
                int fragsIncidSelectionGisCount = 0;
                if (_gisSelection != null)
                {
                    DataRow[] gisRows = _gisSelection.AsEnumerable()
                        .Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(incid)).ToArray();
                    toidsIncidSelectionGisCount = gisRows.GroupBy(r => r[HluDataset.incid_mm_polygons.toidColumn.ColumnName]).Count();
                    fragsIncidSelectionGisCount = gisRows.Length;
                }

                // Count the total number of toids and fragments in the database
                // for this incid so that they can be included in the status area.
                int fragsIncidSelectionDbCount = 0;
                int toidsIncidSelectionDbCount = 0;

                string sqlFragsDbCount = String.Format("SELECT COUNT(*) FROM {0} WHERE {0}.{1} = {2}",
                    _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(incid));

                // Count the total number of fragments in the database for
                // this incid.
                fragsIncidSelectionDbCount = (int)_db.ExecuteScalar(sqlFragsDbCount,
                    _db.Connection.ConnectionTimeout, CommandType.Text);

                // Count the total number of toids in the database for
                // this incid.
                string _sqlToidsDbCount = String.Format(
                    "SELECT COUNT(*) FROM (SELECT DISTINCT {0} FROM {1} WHERE {2} = {3}) AS T",
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.toidColumn.ColumnName),
                    _db.QualifyTableName(_hluDS.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(incid));

                toidsIncidSelectionDbCount = (int)_db.ExecuteScalar(_sqlToidsDbCount,
                    _db.Connection.ConnectionTimeout, CommandType.Text);

                if (physicalSplit)
                {
                    // Check there aren't more incids in GIS than in the
                    // database, and there is at least one fragment in the
                    // database.
                    // There will be more fragments in GIS than the database
                    // prior to a physical split so don't check this.
                    if ((toidsIncidSelectionGisCount > toidsIncidSelectionDbCount) ||
                        (fragsIncidSelectionDbCount == 0))
                        return false;
                }
                else
                {
                    // Check there aren't more incids or fragments in GIS than
                    // in the database.
                    if ((toidsIncidSelectionGisCount > toidsIncidSelectionDbCount) ||
                        (fragsIncidSelectionGisCount > fragsIncidSelectionDbCount))
                        return false;
                }
            }

            return true;

        }

        #endregion Check Selected Features

        #region Split/Merge Capability

        /// <summary>
        /// Computes whether a logical split operation can be performed based on the current state.
        /// At least one feature in selection that share the same incid, but *not* toid and toidfragid.
        /// </summary>
        private bool ComputeCanLogicallySplit()
        {
            WorkMode currentMode = WorkMode;

            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            return (_gisSelection != null) && (_selectedIncidsInGISCount == 1) &&
                ((_gisSelection.Rows.Count > 1) && (_selectedFragsInGISCount > 1) ||
                (_gisSelection.Rows.Count == 1)) &&
                (_filteredByMap == true) &&
                ((_currentIncidToidsInGISCount < _currentIncidToidsInDBCount) ||
                (_currentIncidFragsInGISCount < _currentIncidFragsInDBCount));
        }

        /// <summary>
        /// Computes whether a logical merge operation can be performed based on the current state.
        /// At least one feature in selection that do not share the same incid or toidfragid.
        /// </summary>
        private bool ComputeCanLogicallyMerge()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            return (_gisSelection != null) && (_gisSelection.Rows.Count > 1) &&
                (_filteredByMap == true) &&
                (_selectedIncidsInGISCount > 1) && (_selectedFragsInGISCount > 1);
        }

        /// <summary>
        /// Computes whether a physical split operation can be performed based on the current state.
        /// At least two features in selection that share the same incid, toid and toidfragid.
        /// </summary>
        public bool ComputeCanPhysicallySplit()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            return (_gisSelection != null) && (_gisSelection.Rows.Count > 1) &&
                (_filteredByMap == true) &&
                (_selectedIncidsInGISCount == 1) && (_selectedToidsInGISCount == 1) && (_selectedFragsInGISCount == 1);
        }

        /// <summary>
        /// Computes whether a physical merge operation can be performed based on the current state.
        /// At least one feature in selection that share the same incid and toid but *not* the same toidfragid.
        /// </summary>
        public bool ComputeCanPhysicallyMerge()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            return (_gisSelection != null) && (_gisSelection.Rows.Count > 1) &&
                (_filteredByMap == true) &&
                (_selectedIncidsInGISCount == 1) && (_selectedToidsInGISCount == 1) && (_selectedFragsInGISCount > 1);
        }

        /// <summary>
        /// Refreshes cached split enablement values.
        /// </summary>
        private void RefreshSplitEnablement()
        {
            // Compute the new values.
            bool canPhysSplit = ComputeCanPhysicallySplit();
            bool canLogSplit = ComputeCanLogicallySplit();

            // Check if any values have changed.
            bool changed =
                (_canPhysicallySplit != canPhysSplit) ||
                (_canLogicallySplit != canLogSplit);

            // Update the cached values.
            _canPhysicallySplit = canPhysSplit;
            _canLogicallySplit = canLogSplit;

            // If nothing changed, exit.
            if (!changed)
                return;

            // Notify property changes.
            OnPropertyChanged(nameof(CanPhysicallySplit));
            OnPropertyChanged(nameof(CanLogicallySplit));
            OnPropertyChanged(nameof(CanSplit));
        }

        /// <summary>
        /// Refreshes cached merge enablement values.
        /// </summary>
        private void RefreshMergeEnablement()
        {
            // Compute the new values.
            bool canPhysMerge = ComputeCanPhysicallyMerge();
            bool canLogMerge = ComputeCanLogicallyMerge();

            // Check if any values have changed.
            bool changed =
                (_canPhysicallyMerge != canPhysMerge) ||
                (_canLogicallyMerge != canLogMerge);

            // Update the cached values.
            _canPhysicallyMerge = canPhysMerge;
            _canLogicallyMerge = canLogMerge;

            // If nothing changed, exit.
            if (!changed)
                return;

            // Notify property changes.
            OnPropertyChanged(nameof(CanPhysicallyMerge));
            OnPropertyChanged(nameof(CanLogicallyMerge));
            OnPropertyChanged(nameof(CanMerge));
        }

        /// <summary>
        /// Refreshes both the cached split and merge enablement values.
        /// </summary>
        private void RefreshSplitMergeEnablement()
        {
            RefreshSplitEnablement();
            RefreshMergeEnablement();
        }

        #endregion Split/Merge Capability

        #region Split/Merge Action

        /// <summary>
        /// Performs a logical split operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method temporarily disables automatic splitting, retrieves the current map
        /// selection,  and then re-enables automatic splitting before initiating the logical split operation.  Upon
        /// successful completion, a notification is displayed to the user.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LogicalSplitAsync()
        {
            //TODO: Needed?
            _autoSplit = false;

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            //TODO: Needed?
            _autoSplit = true;

            // Check the selected rows are unique before attempting to split them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database getting out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            // Create ViewModel for split class.
            ViewModelWindowMainSplit vmSplit = new(this);

            // Execute the logical split and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmSplit.LogicalSplitAsync())
                NotifySplitMerge("Logical split completed.");
        }

        /// <summary>
        /// Performs a physical split operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method temporarily disables automatic splitting, retrieves the current map
        /// selection,  and then re-enables automatic splitting before initiating the physical split operation.  Upon
        /// successful completion, a notification is displayed to the user.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task PhysicalSplitAsync()
        {
            //TODO: Needed?
            _autoSplit = false;

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            //TODO: Needed?
            _autoSplit = true;

            // Create ViewModel for split class.
            ViewModelWindowMainSplit vmSplit = new(this);

            // Execute the physical split and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmSplit.PhysicalSplitAsync())
                NotifySplitMerge("Physical split completed.");
        }

        /// <summary>
        /// Performs a logical merge operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method retrieves the current map selection and then initiates the logical merge operation.
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LogicalMergeAsync()
        {
            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database getting out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            // Create ViewModel for merge class.
            ViewModelWindowMainMerge vmMerge = new(this);

            // Execute the logical merge and wait for the result.
            // Notify the user following the completion of the merge.
            if (await vmMerge.LogicalMergeAsync())
                NotifySplitMerge("Logical merge completed.");
        }

        /// <summary>
        /// Performs a physical merge operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method retrieves the current map selection and then initiates the physical merge operation.
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task PhysicalMergeAsync()
        {
            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                MessageBox.Show("The map selection contains one or more features where a physical split has not been completed.\n\n" +
                    "Please select the features and invoke the Split command to prevent the map and database getting out of sync.",
                    "HLU: Data Integrity", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return;
            }

            // Create ViewModel for merge class.
            ViewModelWindowMainMerge vmMerge = new(this);

            // Execute the physical merge and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmMerge.PhysicalMergeAsync())
                NotifySplitMerge("Physical merge completed.");
        }

        /// <summary>
        /// Notify the user following the completion of a split or merge
        /// if the options specify they want to be notified.
        /// </summary>
        private void NotifySplitMerge(string msgText)
        {
            // If the user wants to be notified following the completion of
            // a split or merge, and display the supplied message if they do.
            if (_notifyOnSplitMerge)
            {
                // Create window to show message
                _windowWarnSplitMerge = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _viewModelWinWarnSplitMerge = new ViewModelWindowNotifyOnSplitMerge(msgText);

                // when ViewModel asks to be closed, close window
                _viewModelWinWarnSplitMerge.RequestClose -= viewModelWinWarnSplitMerge_RequestClose; // Safety: avoid double subscription.
                _viewModelWinWarnSplitMerge.RequestClose +=
                    new ViewModelWindowNotifyOnSplitMerge.RequestCloseEventHandler(viewModelWinWarnSplitMerge_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowWarnSplitMerge.DataContext = _viewModelWinWarnSplitMerge;

                // show window
                _windowWarnSplitMerge.ShowDialog();
            }
        }

        /// <summary>
        /// Update the user settings when the split merge request window is closed.
        /// </summary>
        private void viewModelWinWarnSplitMerge_RequestClose()
        {
            // Remove the event handler and close the window.
            _viewModelWinWarnSplitMerge.RequestClose -= viewModelWinWarnSplitMerge_RequestClose;
            _windowWarnSplitMerge.Close();

            // Update the user notify setting
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;
        }

        #endregion Split/Merge Action

        #endregion Methods

    }
}