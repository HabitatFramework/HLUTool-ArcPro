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

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using HLU.Data;
using HLU.Data.Model;
using HLU.Enums;
using HLU.Exceptions;
using HLU.GISApplication;
using HLU.Helpers;
using HLU.Properties;
using HLU.UI.View;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// GIS operations partial for ViewModelWindowMain.
    /// Contains: All ArcGIS Pro SDK interactions, map selection, layer management, spatial operations.
    /// </summary>
    internal partial class ViewModelWindowMain
    {
        #region Fields

        #region Fields - GIS

        private ArcProApp _gisApp;
        private HluGeometryTypes _gisLayerType = HluGeometryTypes.Polygon;

        private DataColumn[] _gisIDColumns;
        private int[] _gisIDColumnOrdinals;

        #endregion Fields - GIS

        #region Fields - Event Tracking

        private bool _mapEventsSubscribed;
        private bool _mapMemberEventsSubscribed;
        private bool _projectClosedEventsSubscribed;
        private bool _layersChangedEventsSubscribed;
        private bool _editCompletedEventsSubscribed;

        #endregion Fields - Event Tracking

        #region Fields - Active Map/Layer

        private MapView _activeMapView;
        private string _activeLayerName;

        #endregion Fields - Active Map/Layer

        #region Fields - Selection State

        // The IDs of the currently selected incids, toids and fragments in GIS (set in AnalyzeGisSelectionSet).
        private IEnumerable<string> _incidsSelectedMap;
        private IEnumerable<string> _toidsSelectedMap;
        private IEnumerable<string> _fragsSelectedMap;

        // How many incids, toids,  fragments and features are selected in GIS (set in AnalyzeGisSelectionSet).
        private int _selectedIncidsInGISCount = 0;
        private int _selectedToidsInGISCount = 0;
        private int _selectedFragsInGISCount = 0;
        private int _selectedFeaturesInGISCount = 0;

        // How many incids and fragments are selected in the database for the current GIS selection
        // (set whenever a filter is applied and in ExpectedSelectionFeatures).
        private int _selectedIncidsInDBCount = 0;
        private int _selectedFragsInDBCount = 0;

        // How many toids and fragments are selected in GIS and the database for the current incid (set in CountFrags).
        private int _currentIncidToidsInGISCount = 0;
        private int _currentIncidFragsInGISCount = 0;
        private int _currentIncidToidsInDBCount = 0;
        private int _currentIncidFragsInDBCount = 0;

        #endregion Fields - Selection State

        #region Fields - Split/Merge

        // Can the current selection be split or merged (set in RefreshSplitMergeStatus).
        private bool _canPhysicallySplit;
        private bool _canLogicallySplit;
        private bool _canPhysicallyMerge;
        private bool _canLogicallyMerge;

        #endregion Fields - Split/Merge

        #region Fields - Feature Insert

        // Can one or more new features (with null incids) in the current selection be registered.
        private bool _canInsertFeature;

        #endregion Fields - Feature Insert

        #region Fields - Bulk Load/Unload

        // Can the Bulk unload or load operation be performed.
        private bool _canBulkUnload;
        private bool _canBulkLoad;

        #endregion Fields - Bulk Load/Unload

        #endregion Fields

        #region Properties

        #region Properties - GIS Info

        /// <summary>
        /// Gets the geometry type of the GIS layer represented by this instance.
        /// </summary>
        /// <remarks>Use this property to determine the spatial data type (such as point, line, or
        /// polygon) associated with the GIS layer. The value can be used to guide rendering, analysis, or data
        /// processing operations that depend on the layer's geometry.</remarks>
        /// <value>The geometry type of the GIS layer.</value>
        public HluGeometryTypes GisLayerType
        {
            get
            {
                return _gisLayerType;
            }
        }

        /// <summary>
        /// Gets the HLU map-match table from the database that corresponds to the active
        /// GIS layer geometry type (polygons, lines or points).
        /// </summary>
        internal DataTable GisMMTable => _hluDS == null ? null : _gisLayerType switch
        {
            HluGeometryTypes.Line => (DataTable)_hluDS.incid_mm_lines,
            HluGeometryTypes.Point => (DataTable)_hluDS.incid_mm_points,
            _ => (DataTable)_hluDS.incid_mm_polygons
        };

        /// <summary>
        /// Gets the <c>shape_length</c> column name for the active GIS map-match table,
        /// or <c>null</c> for point layers that carry no length measure.
        /// </summary>
        internal string GisMMShapeLengthColumnName => _gisLayerType switch
        {
            HluGeometryTypes.Point => null,
            HluGeometryTypes.Line => _hluDS.incid_mm_lines.shape_lengthColumn.ColumnName,
            _ => _hluDS.incid_mm_polygons.shape_lengthColumn.ColumnName
        };

        /// <summary>
        /// Gets the <c>shape_area</c> column name for the active GIS map-match table,
        /// or <c>null</c> for line and point layers that carry no area measure.
        /// </summary>
        internal string GisMMShapeAreaColumnName => _gisLayerType == HluGeometryTypes.Polygon
            ? _hluDS.incid_mm_polygons.shape_areaColumn.ColumnName
            : null;

        #endregion Properties - GIS Info

        #region Properties - Selection State

        /// <summary>
        /// Gets the IDs of the incids currently selected in the map. These are set in
        /// AnalyzeGisSelectionSet when the GIS selection is analyzed and can be used for
        /// status display and selection logic.
        /// </summary>
        /// <value>The IDs of the incids currently selected in the map.</value>
        internal IEnumerable<string> IncidsSelectedMap
        {
            get
            {
                return _incidsSelectedMap;
            }
        }

        /// <summary>
        /// Gets the IDs of the toids currently selected in the map. These are set in
        /// AnalyzeGisSelectionSet when the GIS selection is analyzed and can be used for
        /// status display and selection logic.
        /// </summary>
        /// <value>The IDs of the toids currently selected in the map.</value>
        internal IEnumerable<string> ToidsSelectedMap
        {
            get
            {
                return _toidsSelectedMap;
            }
        }

        /// <summary>
        /// Gets the IDs of the fragments currently selected in the map. These are set in
        /// AnalyzeGisSelectionSet when the GIS selection is analyzed and can be used for
        /// status display and selection logic.
        /// </summary>
        /// <value>The IDs of the fragments currently selected in the map.</value>
        internal IEnumerable<string> FragsSelectedMap
        {
            get
            {
                return _fragsSelectedMap;
            }
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

                // Update the dock pane caption (to show 'Read-only' or not).
                UpdateDockPaneCaption();
            }
        }

        /// <summary>
        /// Can the GIS layer be switched?
        /// </summary>
        /// <value><c>true</c> if the GIS layer can be switched; otherwise, <c>false</c>.</value>
        public bool CanSwitchGISLayer
        {
            get
            {
                // Enable switching only when not in Bulk Update mode or OSMM Review mode and there
                // is more than one HLU layer in the map (otherwise there is nothing to switch to).
                if (IsNotBulkMode && IsNotOsmmReviewMode)
                {
                    return _gisApp?.HluLayerCount > 1;
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
            get
            {
                return IsNotBulkMode && IsNotOsmmReviewMode && IncidCurrentRow != null;
            }
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
            // Can get map selection if not in Bulk Update mode.
            get
            {
                return IsNotBulkMode;
            }
        }

        #endregion Properties - Map Selection

        #region Properties - Selection Counts

        /// <summary>
        /// Gets or sets the count of selected Incids in the database, which is used for status display and selection logic.
        /// </summary>
        /// <value>The count of selected Incids in the database.</value>
        public int SelectedIncidsInDBCount
        {
            get
            {
                return _selectedIncidsInDBCount;
            }
            set
            {
                _selectedIncidsInDBCount = value;
            }
        }

        /// <summary>
        /// Gets or sets the count of selected Frags in the database, which is used for status display and selection logic.
        /// </summary>
        /// <value>The count of selected Frags in the database.</value>
        public int SelectedFragsInDBCount
        {
            get
            {
                return _selectedFragsInDBCount;
            }
            set
            {
                _selectedFragsInDBCount = value;
            }
        }

        /// <summary>
        /// Gets or sets the count of selected Incids in GIS, which is used for status display and selection logic.
        /// </summary>
        /// <value><c>true</c> if the count of selected Incids in GIS can be retrieved; otherwise, <c>false</c>.</value>
        public int SelectedIncidsInGISCount
        {
            get
            {
                return _selectedIncidsInGISCount;
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the count of selected Toids in GIS, which is used for status display and selection logic.
        /// </summary>
        /// <value><c>true</c> if the count of selected Toids in GIS can be retrieved; otherwise, <c>false</c>.</value>
        public int SelectedToidsInGISCount
        {
            get
            {
                return _selectedToidsInGISCount;
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the count of selected Frags in GIS, which is used for status display and selection logic.
        /// </summary>
        /// <value><c>true</c> if the count of selected Frags in GIS can be retrieved; otherwise, <c>false</c>.</value>
        public int SelectedFragsInGISCount
        {
            get
            {
                return _selectedFragsInGISCount;
            }
            set
            {
            }
        }

        #endregion Properties - Selection Counts

        #region Properties - Map Zoom

        /// <summary>
        /// Can the map be zoomed to the current selection?
        /// </summary>
        /// <value><c>true</c> if the map can be zoomed to the current selection; otherwise, <c>false</c>.</value>
        public bool CanZoomToSelection
        {
            get
            {
                return _gisSelection != null;
            }
        }

        #endregion Properties - Map Zoom

        #region Properties - Split/Merge Commands

        /// <summary>
        /// Can a physical split operation be performed?
        /// </summary>
        /// <value><c>true</c> if a physical split operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanPhysicallySplit => _canPhysicallySplit;

        /// <summary>
        /// Can a logical split operation be performed?
        /// </summary>
        /// <value><c>true</c> if a logical split operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanLogicallySplit => _canLogicallySplit;

        /// <summary>
        /// Can a physical merge operation be performed?
        /// </summary>
        /// <value><c>true</c> if a physical merge operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanPhysicallyMerge => _canPhysicallyMerge;

        /// <summary>
        /// Can a logical merge operation be performed?
        /// </summary>
        /// <value><c>true</c> if a logical merge operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanLogicallyMerge => _canLogicallyMerge;

        /// <summary>
        ///  Can a split operation (physical or logical) be performed?
        /// </summary>
        /// <value><c>true</c> if a split operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanSplit => _canPhysicallySplit || _canLogicallySplit;

        /// <summary>
        ///  Can a merge operation (physical or logical) be performed?
        /// </summary>
        /// <value><c>true</c> if a merge operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanMerge => _canPhysicallyMerge || _canLogicallyMerge;

        #endregion Properties - Split/Merge Commands

        #region Properties - Feature Insert Commands

        /// <summary>
        /// Can one or more selected features with null incids be registered against the same new INCID?
        /// </summary>
        /// <value><c>true</c> if a feature insert (same incid) operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanInsertFeatureSameIncid => _canInsertFeature;

        /// <summary>
        /// Can each selected feature with a null incid be registered against its own new INCID?
        /// Only meaningful (and therefore enabled) when more than one feature is selected;
        /// with a single feature the result is identical to <see cref="CanInsertFeatureSameIncid"/>.
        /// </summary>
        /// <value><c>true</c> if a feature insert (separate incids) operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanInsertFeatureSeparateIncid => _canInsertFeature && _gisSelection?.Rows.Count > 1;

        /// <summary>
        /// Can a feature insert operation (same or separate incid) be performed?
        /// </summary>
        /// <value><c>true</c> if any feature insert operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanInsertFeature => _canInsertFeature;

        #endregion Properties - Feature Insert Commands

        #region Properties - Bulk Load/Unload Commands

        /// <summary>
        /// Can a bulk unload operation be performed?
        /// Unload removes selected GIS features that already have an INCID, deletes their shadow
        /// map-match rows, and cleans up any orphaned INCID records.
        /// </summary>
        /// <value><c>true</c> if an bulk unload can be performed; otherwise, <c>false</c>.</value>
        public bool CanBulkUnload => _canBulkUnload;

        /// <summary>
        /// Can a bulk load operation be performed?
        /// Load registers selected new (null-INCID) GIS features under new INCID records, exactly
        /// as the separate-INCID feature insert does, but tagged with the OSMMLoad history operation.
        /// </summary>
        /// <value><c>true</c> if a bulk load can be performed; otherwise, <c>false</c>.</value>
        public bool CanBulkLoad => _canBulkLoad;

        /// <summary>
        /// Can either an bulk unload or load operation be performed?
        /// </summary>
        /// <value><c>true</c> if any bulk load/unload operation can be performed; otherwise, <c>false</c>.</value>
        public bool CanBulkLoadUnload => _canBulkUnload || _canBulkLoad;

        #endregion Properties - Bulk Load/Unload Commands

        #endregion Properties

        #region Methods

        #region GIS Events

        /// <summary>
        /// Event when the active map view changes. Checks that there is an active map view and
        /// that it contains a valid HLU layer, then shows or hides the UI controls as appropriate.
        /// </summary>
        /// <param name="obj">Event arguments containing details about the active map view change.</param>
        private async void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs obj)
        {
            // Clear any existing messages
            ClearAllMessages();

            // If there is no active map view.
            if (MapView.Active == null)
            {
                // Make the UI controls hidden.
                GridMainVisibility = Visibility.Hidden;

                // Display an error message.
                ShowError("No active map.", MessageCategory.GIS);

                return;
            }

            // Determine if this is truly a new map by comparing the map itself, not just the MapView instance
            // This is the key check - if the Map object is the same, it's just a focus change (e.g., attribute table)
            bool isNewMap = _activeMapView == null ||
                            MapView.Active.Map != _activeMapView.Map;

            // If it's the same map, just update the reference and continue
            // This handles attribute table openings and other focus changes within the same map
            if (!isNewMap)
            {
                // Update the MapView reference but don't treat it as a new map
                _activeMapView = MapView.Active;

                // Make the UI controls visible.
                GridMainVisibility = Visibility.Visible;
                return;
            }

            // If we get here, it's truly a new map, so check it
            if (!await CheckActiveMapAsync(forceReset: isNewMap))
            {
                // Only clear _activeMapView if the check failed
                _activeMapView = null;

                // Make the UI controls hidden.
                GridMainVisibility = Visibility.Hidden;
                return;
            }

            // Make the UI controls visible.
            GridMainVisibility = Visibility.Visible;
        }

        /// <summary>
        /// Handles the event that occurs when new layers are added to the map.
        /// </summary>
        /// <remarks>This method verifies that there is an active map with a valid HLU map before
        /// proceeding with further operations. Does not force reset to preserve active layer.</remarks>
        /// <param name="args">Event arguments containing details about the layers that were added.</param>
        private async void OnLayersAdded(LayerEventsArgs args)
        {
            // Check that there is an active map and that it contains a valid HLU map.
            // Don't force reset - preserve the current active layer
            if (!await CheckActiveMapAsync(forceReset: false))
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
        /// <param name="args">Event arguments containing details about the layers that were removed.</param>
        private async void OnLayersRemoved(LayerEventsArgs args)
        {
            bool activeLayerRemoved = false;

            foreach (var layer in args.Layers)
            {
                // If the active layer has been removed, flag it
                if (layer.Name == ActiveLayerName)
                {
                    activeLayerRemoved = true;
                    // Clear the active layer name since it no longer exists
                    ActiveLayerName = null;
                    break;
                }
            }

            // Check that there is an active map and that it contains a valid HLU map.
            // Force reset only if the active layer was removed
            if (!await CheckActiveMapAsync(forceReset: activeLayerRemoved))
            {
                _activeMapView = null;
                return;
            }

            // Refresh the layer name property changed notification
            OnPropertyChanged(nameof(ActiveLayerName));
        }

        /// <summary>
        /// Handles the event raised when an edit operation completes.
        /// Recomputes CanEdit in case the editing session state has changed.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task OnEditCompleted(EditCompletedEventArgs args)
        {
            // Recomputes whether editing is currently possible.
            RefreshEditCapability();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Event when the project is closed. Recomputes whether editing is currently possible and makes the UI controls hidden.
        /// </summary>
        /// <param name="obj">Event arguments containing details about the project that was closed.</param>
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
            // Toggle the tab state to hidden.
            ToggleState("HLUTool_tab_state", false);
        }

        /// <summary>
        /// Event when the DockPane is disposed. Unsubscribes from all events to prevent memory
        /// leaks and calls base to allow parent cleanup.
        /// </summary>
        protected override void OnDispose()
        {
            // Unsubscribe from map events
            if (_mapEventsSubscribed)
            {
                ActiveMapViewChangedEvent.Unsubscribe(OnActiveMapViewChanged);
                _mapEventsSubscribed = false;
            }

            // Unsubscribe from layer events
            if (_layersChangedEventsSubscribed)
            {
                LayersAddedEvent.Unsubscribe(OnLayersAdded);
                LayersRemovedEvent.Unsubscribe(OnLayersRemoved);
                _layersChangedEventsSubscribed = false;
            }

            // Unsubscribe from editing events
            if (_projectClosedEventsSubscribed)
            {
                ProjectClosedEvent.Unsubscribe(OnProjectClosed);
                _projectClosedEventsSubscribed = false;
            }

            // Unsubscribe from map member events
            if (_mapMemberEventsSubscribed)
            {
                MapMemberPropertiesChangedEvent.Unsubscribe(OnMapMemberPropertiesChanged);
                _mapMemberEventsSubscribed = false;
            }

            // Unsubscribe from edit completed events
            if (_editCompletedEventsSubscribed)
            {
                EditCompletedEvent.Unsubscribe(OnEditCompleted);
                _editCompletedEventsSubscribed = false;
            }

            // Clean up all message timers
            foreach (var timer in _messageTimers.Values)
            {
                timer?.Stop();
                timer?.Dispose();
            }
            _messageTimers.Clear();

            // Call base to allow parent cleanup
            base.OnDispose();
        }

        /// <summary>
        /// Handles map member property changes and refreshes edit capability if the active HLU layer is affected.
        /// </summary>
        /// <param name="args">Event arguments containing details about the map member properties that changed.</param>
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
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async Task SelectCurrentOnMapAsync()
        {
            // Clear any existing GIS messages
            ClearMessage(category: MessageCategory.GIS);

            // Check the GIS application is initialised.
            if (_gisApp == null)
            {
                // Display a warning message.
                ShowWarning("GIS application is not initialised.", MessageCategory.GIS);
                return;
            }

            // Check the current incid is set.
            if (_incidCurrentRow == null || String.IsNullOrWhiteSpace(_incidCurrentRow.incid))
            {
                // Display a warning message.
                ShowWarning("No current incid is set.", MessageCategory.GIS);
                return;
            }

            try
            {
                // Set the status to processing and the cursor to wait.
                ChangeCursor(Cursors.Wait, "Selecting in GIS ...");

                // Select the current incid on the map.
                if (!await _gisApp.SelectIncidOnMapAsync(_incidCurrentRow.incid))
                {
                    // Display an error message.
                    ShowError("Error selecting current incid in GIS.", MessageCategory.GIS);

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
                    // Display a warning message.
                    ShowWarning("No features for incid found in active layer.", MessageCategory.GIS);

                    return;
                }

                // Zoom to the GIS selection (if auto zoom configured).
                await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);
            }
            catch (HLUToolException ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.GIS);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.GIS);
            }
            // Make sure the cursor is always reset.
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        /// <summary>
        /// Select the current incid record on the map.
        /// </summary>
        /// <param name="updateIncidSelection">Should the incid selection be updated afterwards?</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SelectOnMapAsync(bool updateIncidSelection)
        {
            // Check there is a current row
            if (IncidCurrentRow == null)
                return;

            // Clear any existing GIS messages
            ClearMessage(category: MessageCategory.GIS, level: MessageType.Information);

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

                // Determine if a filter with more than one incid is currently active
                // (must be checked before resetting _incidSelection).
                bool multiIncidFilter = (IsFiltered && prevIncidSelection != null && prevIncidSelection.Rows.Count > 1);

                // Reset the table of selected incids.
                _incidSelection = NewIncidSelectionTable();

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
                        DataRow[] gisRows = [.. prevGISSelection.AsEnumerable().Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid))];
                        numFragsOld = gisRows.Length;
                    }

                    // Count the number of fragments now selected for this incid.
                    int numFragsNew = 0;
                    if (_gisSelection != null)
                    {
                        DataRow[] gisRows = [.. _gisSelection.AsEnumerable().Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid))];
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
                    ShowWarning("No features for incid found in active layer.", MessageCategory.GIS);
                    return;
                }

                // Zoom to the GIS selection (if auto zoom configured).
                await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.GIS);
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
        /// <param name="param">The parameter passed to the command.</param>
        internal async void GetMapSelectionClicked(object param)
        {
            // Get the GIS layer selection and warn the user if no
            // features are found (don't wait).
            await GetMapSelectionAsync(true);
        }

        /// <summary>
        /// Gets the map selection from the GIS application.
        /// </summary>
        /// <param name="showMessage">Indicates whether to show a message if no features are found.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async Task GetMapSelectionAsync(bool showMessage)
        {
            // Clear any existing GIS messages
            ClearMessage(category: MessageCategory.GIS);

            // Clear any existing info messages
            ClearMessage(level: MessageType.Information);

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

                if (_gisSelection == null)
                    return;

                // Read which features are selected in GIS (passing it a new
                // GIS selection table so that it knows the columns to return.
                try
                {
                    _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);
                }
                catch (HLUToolException ex)
                {
                    // Show warning message
                    ShowWarning(ex.Message, MessageCategory.GIS);
                    return;
                }

                // Count how many incids, toids and fragments are selected in GIS
                _incidSelectionWhereClause = null;
                AnalyzeGisSelectionSet(true);

                // Update the number of features found in the database.
                _selectedFragsInDBCount = await ExpectedSelectionFeaturesAsync(_incidSelectionWhereClause);

                // Store the number of incids found in the database
                _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                // Check if all selected features have null INCIDs (i.e., they are new, unregistered features).
                bool allIncidsNull = (_gisSelection != null) && (_gisSelection.Rows.Count > 0) &&
                                      (_incidsSelectedMap != null) &&
                                      _incidsSelectedMap.All(i => string.IsNullOrEmpty(i));

                // If any GIS features with valid INCIDs were found.
                if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0) && !allIncidsNull)
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

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow);

                    // Check if the GIS and database are in sync.
                    CheckInSync(MessageCategory.GIS, showMessage: showMessage);
                }
                else if (allIncidsNull)
                {
                    // Special case: All selected features have null INCIDs (new, unregistered features).
                    // Keep the GIS selection intact so feature insert operations can work.

                    // Save the feature count for the message.
                    int selectedFeatureCount = _gisSelection?.Rows.Count ?? 0;

                    // Clear the form to avoid displaying stale incid details from the previous selection.
                    ClearForm();

                    // Reset the DB and GIS fragment/toid counts so the status counter
                    // does not show stale values from the previously active layer or selection.
                    _currentIncidToidsInGISCount = 0;
                    _currentIncidFragsInGISCount = 0;
                    _currentIncidToidsInDBCount = 0;
                    _currentIncidFragsInDBCount = 0;

                    // Clear the incid selection but keep the GIS selection intact.
                    _incidSelection = NewIncidSelectionTable();

                    // Indicate the selection came from the map.
                    _filteredByMap = true;

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow);

                    // Display a message about the geometry type.
                    string geomTypeName = _gisLayerType switch
                    {
                        HluGeometryTypes.Line  => "line",
                        HluGeometryTypes.Point => "point",
                        _                      => "polygon"
                    };

                    // Refresh all UI bindings so every property reflects the cleared state.
                    RefreshAll();

                    ShowInfo($"Selected {selectedFeatureCount} new {geomTypeName} {(selectedFeatureCount == 1 ? "feature" : "features")} with no INCID assigned.", MessageCategory.GIS);
                }
                else
                {
                    // No features were selected or no valid features found.

                    // Clear the form to avoid displaying stale incid details from the previous selection.
                    ClearForm();

                    // Reset the DB and GIS fragment/toid counts so the status counter
                    // does not show stale values from the previously active layer or selection.
                    _currentIncidToidsInGISCount = 0;
                    _currentIncidFragsInGISCount = 0;
                    _currentIncidToidsInDBCount = 0;
                    _currentIncidFragsInDBCount = 0;

                    // Check whether the geometry-type map-match table has any records at all.
                    // If it is empty the user has not yet registered any features of this type.
                    int totalDbCount = 0;
                    try
                    {
                        totalDbCount = (int)_db.ExecuteScalar(
                            $"SELECT COUNT(*) FROM {_db.QualifyTableName(GisMMTable.TableName)}",
                            _db.Connection.ConnectionTimeout, CommandType.Text);
                    }
                    catch { }

                    // Reset the incid and map selections if necessary, then move
                    // to the first incid in the database.
                    await ClearFilterAsync(true, totalDbCount != 0);

                    // Indicate the selection didn't come from the map (but only after
                    // the filter has been cleared and the first incid selected so that
                    // the map doesn't auto zoom to the incid).
                    _filteredByMap = false;

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow);

                    // Display a warning message.
                    string geomTypeName = _gisLayerType switch
                    {
                        HluGeometryTypes.Line => "line",
                        HluGeometryTypes.Point => "point",
                        _ => "polygon"
                    };

                    // Show a warning if no features have been registered in the database yet,
                    // otherwise show a warning that no features were selected in the active layer.
                    if (totalDbCount == 0)
                    {
                        // Refresh all UI bindings so every property reflects the cleared state.
                        RefreshAll();

                        ShowWarning($"No {geomTypeName} features have been registered in the database yet.", MessageCategory.GIS);
                    }
                    else
                        ShowWarning($"No features selected in active layer.", MessageCategory.GIS);
                }
            }
            catch (Exception ex)
            {
                // Show error message
                ShowError(ex.Message, MessageCategory.GIS);
            }
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Get Map Selection

        #region Select All On Map Command

        /// <summary>
        /// Select all the incids in the active filter in GIS.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async Task SelectAllOnMapAsync()
        {
            // If there are any records in the selection (and the tool is
            // not currently in Bulk Update mode).
            if (!IsFiltered)
                return;

            try
            {
                // Set the status to processing and the cursor to wait.
                ChangeCursor(Cursors.Wait, "Selecting in GIS ...");

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                // Get the incid column and table for the where clause
                DataColumn incidColumn = _incidSelection.Columns[_hluDS.incid.incidColumn.ColumnName];
                DataTable condTable = GisMMTable;

                // Create a quote function if _gisApp exists, otherwise null.
                Func<string, string> quoteFunc = _gisApp != null ? _gisApp.QuoteValue : null;

                // Build the where clause for the incids to be selected.
                List<SqlFilterCondition> whereConditions = SqlBuilder.BuildIncidWhereClause(
                    _incidSelection,
                    incidColumn,
                    condTable,
                    quoteFunc);

                List<List<SqlFilterCondition>> whereClause = [whereConditions];

                // Find the expected number of features to be selected in GIS
                // (by querying the database).
                int expectedNumFeatures = await ExpectedSelectionFeaturesAsync(whereClause);

                // Find the expected number of incids to be selected in GIS.
                int expectedNumIncids = _incidSelection.Rows.Count;

                // Select the required incid(s) in GIS and read the selection.
                if (await PerformGisSelectionAsync(true, expectedNumFeatures, expectedNumIncids))
                {
                    // Analyse the results of the GIS selection by counting the number of
                    // incids, toids and fragments selected. Do not overwrite DB filter selection.
                    AnalyzeGisSelectionSet(false);

                    // Check if the counts returned are less than those expected.
                    if (_selectedFragsInGISCount < _selectedFragsInDBCount)
                    {
                        // Show a warning message
                        ShowWarning("Not all selected features found in active layer.", MessageCategory.GIS);
                    }

                    // Indicate the selection came from the map.
                    if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
                        _filteredByMap = true;

                    // Set the filter back to the first incid.
                    await SetFilterAsync();

                    // Warn the user that no features were found in GIS.
                    if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                    {
                        // Show an information message.
                        ShowInfo("No features for incids found in active layer.", MessageCategory.GIS);

                        return;
                    }

                    // Zoom to the GIS selection (if auto zoom configured).
                    await _gisApp.ZoomSelectedAsync(_minZoom, _autoZoomToSelection);

                    // Check if the GIS and database are in sync.
                    CheckInSync(MessageCategory.GIS, "Not all incid");
                }
                else
                {
                    // Restore the previous selection (filter).
                    _incidSelection = incidSelectionBackup;
                }
            }
            catch (Exception ex)
            {
                // Clear the selection
                _incidSelection = null;

                // Show error message
                ShowError(ex.Message, MessageCategory.GIS);
            }
            // Make sure the cursor is always reset.
            finally
            {
                // Reset the cursor back to normal
                ChangeCursor(Cursors.Arrow);

                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Select All On Map Command

        #region Selection Tables

        /// <summary>
        /// Initialise the GIS selection table.
        /// </summary>
        /// <returns>A new DataTable for GIS selection.</returns>
        private DataTable NewGisSelectionTable()
        {
            // Check the GIS ID columns have been initialised.
            if (_gisIDColumns == null)
                return null;

            DataTable outTable = new();
            foreach (DataColumn c in _gisIDColumns)
                outTable.Columns.Add(new DataColumn(c.ColumnName, c.DataType) { AllowDBNull = true });
            return outTable;
        }

        #endregion Selection Tables

        #region Map Selection

        /// <summary>
        /// Calculates the expected number of GIS features to be selected
        /// by the sql query based upon a list of conditions.
        /// </summary>
        /// <param name="whereClause">The list of where clause conditions.</param>
        /// <returns>A task of an integer of the number of fragments expected to be selected.</returns>
        private async Task<int> ExpectedSelectionFeaturesAsync(List<List<SqlFilterCondition>> whereClause)
        {
            int numFragments = 0;

            // Track distinct TOIDs across all chunks for an exact unique count.
            HashSet<string> distinctToids = new(StringComparer.OrdinalIgnoreCase);

            // If there is a selection and from tables to query against (and the where clause isn't null or empty).
            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                (whereClause != null) && (whereClause.Count > 0))
            {
                try
                {
                    // Set up the from tables and join conditions for the SQL query based on the
                    // tables in the where clause (which may include joined tables such as the incid table).
                    DataTable t = GisMMTable;
                    DataColumn tIncidColumn = t.Columns[_hluDS.incid_mm_polygons.incidColumn.ColumnName];
                    DataTable[] selTables = [t];

                    IEnumerable<DataTable> queryTables = whereClause.SelectMany(cond => cond.Select(c => c.Table)).Distinct();

                    var fromTables = queryTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = [.. selTables, .. fromTables];

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, tIncidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, tIncidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    // If there is only one long list then chunk it up into smaller lists.
                    if (whereClause.Count == 1)
                    {
                        try
                        {
                            List<SqlFilterCondition> whereCond = [];
                            whereCond = whereClause[0];

                            // Chunk only at top-level OR boundaries while tracking parentheses.
                            // This avoids splitting matched pairs such as "incid >= 1" AND "incid <= 4".
                            whereClause = [.. whereCond.ChunkClauseTopLevel(50, 500)];
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

                            cond = whereCond[^1];
                            cond.CloseParentheses = "))";
                        }

                        // Count the number of fragments for this chunk of where conditions (appended to the join conditions).
                        numFragments += await _db.SqlCountAsync(selTables, "*", [.. joinCond, .. whereClause[i]]);
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
        /// <returns>An integer of the number of fragments expected to be selected.</returns>
        private int ExpectedSelectionFeatures(List<DataTable> sqlFromTables, string sqlWhereClause)
        {
            int numFragments = 0;

            // If there is a selection and from tables to query against (and the where clause isn't null or empty).
            if ((_incidSelection != null) && (_incidSelection.Rows.Count > 0) &&
                sqlFromTables.Count != 0)
            {
                try
                {
                    // Set up the from tables and join conditions for the SQL query based on the
                    // tables in the where clause (which may include joined tables such as the incid table).
                    DataTable t = GisMMTable;
                    DataColumn tIncidColumn = t.Columns[_hluDS.incid_mm_polygons.incidColumn.ColumnName];
                    DataTable[] selTables = [t];

                    var fromTables = sqlFromTables.Distinct().Where(q => !selTables.Select(s => s.TableName).Contains(q.TableName));
                    DataTable[] whereTables = [.. selTables, .. fromTables];

                    DataRelation rel;
                    IEnumerable<SqlFilterCondition> joinCond = fromTables.Select(st =>
                        st.GetType() == typeof(HluDataSet.incidDataTable) ?
                        new SqlFilterCondition("AND", t, tIncidColumn, typeof(DataColumn), "(", ")", st.Columns[_hluDS.incid.incidColumn.Ordinal]) :
                        (rel = GetRelation(_hluDS.incid, st)) != null ?
                        new SqlFilterCondition("AND", t, tIncidColumn, typeof(DataColumn), "(", ")", rel.ChildColumns[0]) : null).Where(c => c != null);

                    // Create a selection DataTable of PK values of the active GIS map-match table.
                    // NOTE: _db.SqlSelect is synchronous - do NOT wrap in Task.Run()
                    // because it can cause deferred LINQ execution issues.
                    _incidMMShadowSelection = _db.SqlSelect(
                        true,
                        false,
                        t.PrimaryKey,
                        [.. whereTables], [.. joinCond], sqlWhereClause);

                    // Count the number of fragments from the selection table.
                    numFragments = _incidMMShadowSelection.Rows.Count;
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
                // Display an error message.
                ShowError("Error selecting current incid in GIS.", MessageCategory.GIS);

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
        public void CountCurrentIncidFrags()
        {
            // Guard against a null current row (e.g. first insert into a new geometry type)
            // or a row with a null/DBNull incid field (e.g. after clearing the filter).
            if (_incidCurrentRow == null || _incidCurrentRow.RowState == DataRowState.Detached ||
                _incidCurrentRow[_hluDS.incid.incidColumn] == DBNull.Value)
            {
                _currentIncidToidsInGISCount = 0;
                _currentIncidFragsInGISCount = 0;
                _currentIncidToidsInDBCount = 0;
                _currentIncidFragsInDBCount = 0;
                return;
            }

            // Count the number of toids and fragments for this incid selected
            // in the GIS. They are counted here, once when the incid changes,
            // instead of in StatusIncid() which is constantly being called.
            if (_gisSelection != null)
            {
                DataRow[] gisRows = [.. _gisSelection.AsEnumerable().Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(_incidCurrentRow.incid))];
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
                    _db.QualifyTableName(GisMMTable.TableName),
                    _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                    _db.QuoteValue(_incidCurrentRow.incid)),
                    _db.Connection.ConnectionTimeout, CommandType.Text);

            // Count the total number of fragments in the database for
            // this incid.
            _currentIncidFragsInDBCount = (int)_db.ExecuteScalar(String.Format(
                "SELECT COUNT(*) FROM {0} WHERE {1} = {2}",
                _db.QualifyTableName(GisMMTable.TableName),
                _db.QuoteIdentifier(_hluDS.incid_mm_polygons.incidColumn.ColumnName),
                _db.QuoteValue(_incidCurrentRow.incid)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
        }

        #endregion Count Features

        #region Check Selected Features

        /// <summary>
        /// Checks the selected frags for the current selection in GIS against the database counts,
        /// and returns false if there are more toids or frags in GIS than in the database, or if there
        /// are no frags in the database for a physical split.
        /// <param name="physicalSplit">if set to <c>true</c> [physical split].</param>
        /// <returns>
        /// <c>true</c> if the selected frags are valid; otherwise, <c>false</c>.
        /// </returns>
        public bool CheckSelectedFrags(bool physicalSplit)
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
                    DataRow[] gisRows = [.. _gisSelection.AsEnumerable().Where(r => r[HluDataset.incid_mm_polygons.incidColumn.ColumnName].Equals(incid))];
                    toidsIncidSelectionGisCount = gisRows.GroupBy(r => r[HluDataset.incid_mm_polygons.toidColumn.ColumnName]).Count();
                    fragsIncidSelectionGisCount = gisRows.Length;
                }

                // Count the total number of toids and fragments in the database
                // for this incid so that they can be included in the status area.
                int fragsIncidSelectionDbCount = 0;
                int toidsIncidSelectionDbCount = 0;

                string sqlFragsDbCount = String.Format("SELECT COUNT(*) FROM {0} WHERE {0}.{1} = {2}",
                    _db.QualifyTableName(GisMMTable.TableName),
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
                    _db.QualifyTableName(GisMMTable.TableName),
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
        /// At least one feature in selection that share the same incid, but *not* toid and fragid.
        /// </summary>
        /// <returns><c>true</c> if a logical split can be performed; otherwise, <c>false</c>.</returns>
        private bool ComputeCanLogicallySplit()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            // There must be at least one feature selected in GIS.
            bool hasSelection = _gisSelection != null;

            // Only one INCID may be selected (logical split creates a new INCID from a subset).
            bool singleIncid = _selectedIncidsInGISCount == 1;

            // Either multiple distinct fragments are selected, or exactly one feature is
            // selected (it can be split away from the remaining DB records for the same INCID).
            bool validSelectionSize = (_gisSelection?.Rows.Count > 1 && _selectedFragsInGISCount > 1)
                                   || (_gisSelection?.Rows.Count == 1);

            // The selection must have originated from the map, not from a database filter.
            bool fromMapSelection = _filteredByMap == true;

            // The database must hold more toids or fragments for this INCID than are currently
            // shown in GIS, confirming there are features left behind to split from.
            bool hasRemainingDbFeatures = (_currentIncidToidsInGISCount < _currentIncidToidsInDBCount)
                                       || (_currentIncidFragsInGISCount < _currentIncidFragsInDBCount);

            return hasSelection && singleIncid && validSelectionSize && fromMapSelection && hasRemainingDbFeatures;
        }

        /// <summary>
        /// Computes whether a logical merge operation can be performed based on the current state.
        /// At least one feature in selection that do not share the same incid or fragid.
        /// </summary>
        /// <returns><c>true</c> if a logical merge can be performed; otherwise, <c>false</c>.</returns>
        private bool ComputeCanLogicallyMerge()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            // More than one feature must be selected.
            bool hasMultipleFeatures = _gisSelection != null && _gisSelection.Rows.Count > 1;

            // The selection must have originated from the map, not from a database filter.
            bool fromMapSelection = _filteredByMap == true;

            // Multiple INCIDs must be selected (logical merge joins features from different INCIDs).
            bool multipleIncids = _selectedIncidsInGISCount > 1;

            // Multiple fragments must be present in the selection.
            bool multipleFragments = _selectedFragsInGISCount > 1;

            return hasMultipleFeatures && fromMapSelection && multipleIncids && multipleFragments;
        }

        /// <summary>
        /// Computes whether a physical split operation can be performed based on the current state.
        /// At least two features in selection that share the same incid, toid and fragid.
        /// Not supported for point layers, where physical splitting is not meaningful.
        /// </summary>
        /// <returns><c>true</c> if a physical split can be performed; otherwise, <c>false</c>.</returns>
        public bool ComputeCanPhysicallySplit()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            // Physical split is not meaningful for point geometry layers.
            if (_gisLayerType == HluGeometryTypes.Point)
                return false;

            // More than one feature must be selected.
            bool hasMultipleFeatures = _gisSelection != null && _gisSelection.Rows.Count > 1;

            // The selection must have originated from the map, not from a database filter.
            bool fromMapSelection = _filteredByMap == true;

            // All selected features must share exactly one INCID, one TOID, and one fragment —
            // meaning they are duplicate polygon records for the same real-world feature.
            bool sameSingleUnit = _selectedIncidsInGISCount == 1
                               && _selectedToidsInGISCount == 1
                               && _selectedFragsInGISCount == 1;

            return hasMultipleFeatures && fromMapSelection && sameSingleUnit;
        }

        /// <summary>
        /// Computes whether a physical merge operation can be performed based on the current state.
        /// At least one feature in selection that share the same incid and toid but *not* the same fragid.
        /// Not supported for point layers; use logical merge to group point features under one INCID.
        /// </summary>
        /// <returns><c>true</c> if a physical merge operation can be performed; otherwise, <c>false</c>.</returns>
        public bool ComputeCanPhysicallyMerge()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            // Physical merge is not meaningful for point geometry layers.
            if (_gisLayerType == HluGeometryTypes.Point)
                return false;

            // More than one feature must be selected.
            bool hasMultipleFeatures = _gisSelection != null && _gisSelection.Rows.Count > 1;

            // The selection must have originated from the map, not from a database filter.
            bool fromMapSelection = _filteredByMap == true;

            // All selected features must share the same INCID and TOID but have different
            // fragment IDs — i.e. separate fragments of the same polygon that can be merged.
            bool sameIncidAndToidMultipleFrags = _selectedIncidsInGISCount == 1
                                              && _selectedToidsInGISCount == 1
                                              && _selectedFragsInGISCount > 1;

            return hasMultipleFeatures && fromMapSelection && sameIncidAndToidMultipleFrags;
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
            RefreshInsertFeatureEnablement();
            RefreshOSMMLoadUnloadEnablement();
        }

        #endregion Split/Merge Capability

        #region Feature Insert Capability

        /// <summary>
        /// Computes whether a feature insert operation can be performed based on the current state.
        /// All selected features must have a null or empty incid (i.e. they are newly created and not
        /// yet registered in the database).
        /// </summary>
        /// <returns><c>true</c> if a feature insert can be performed; otherwise, <c>false</c>.</returns>
        private bool ComputeCanInsertFeature()
        {
            // Must be in a mode that allows edits and ready for edit operations (includes CanEdit + Reason/Process selection).
            if (!IsEditOperationModeReady)
                return false;

            // There must be at least one feature selected.
            if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                return false;

            // All selected features must have a null or empty incid — meaning they are new,
            // unregistered features that the user has created in the active layer.
            return _incidsSelectedMap != null &&
                   _incidsSelectedMap.All(i => string.IsNullOrEmpty(i));
        }

        /// <summary>
        /// Refreshes the cached feature insert enablement value.
        /// </summary>
        private void RefreshInsertFeatureEnablement()
        {
            bool canInsert = ComputeCanInsertFeature();

            // Always notify CanInsertFeatureSeparateIncid because its computed value also
            // depends on the selection count, which may have changed even if canInsert hasn't.
            bool changed = _canInsertFeature != canInsert;
            _canInsertFeature = canInsert;

            if (changed)
            {
                OnPropertyChanged(nameof(CanInsertFeature));
                OnPropertyChanged(nameof(CanInsertFeatureSameIncid));
            }

            // Always fire this one — its value can change even when _canInsertFeature stays true
            // (e.g. selection count moves between 1 and >1).
            OnPropertyChanged(nameof(CanInsertFeatureSeparateIncid));
        }

        #endregion Feature Insert Capability

        #region Bulk Load/Unload Capability

        /// <summary>
        /// Computes whether an OSMM unload can be performed. The selection must contain at least one
        /// registered (non-null INCID) feature that exists in the database.
        /// </summary>
        private bool ComputeCanOSMMUnload()
        {
            // Must be in a mode that allows edits and ready for edit operations.
            if (!IsEditOperationModeReady)
                return false;

            // There must be at least one feature selected.
            if (_gisSelection == null || _gisSelection.Rows.Count == 0)
                return false;

            // All selected features must have a non-null, non-empty INCID (i.e. they are already
            // registered in the database and can therefore be unloaded).
            return _incidsSelectedMap != null &&
                   _incidsSelectedMap.Any() &&
                   _incidsSelectedMap.All(i => !string.IsNullOrEmpty(i));
        }

        /// <summary>
        /// Computes whether an bulk load can be performed.
        /// Bulk Load requires normal update mode (not bulk update, OSMM review, or OSMM bulk update modes).
        /// Note: Bulk Load operates on a source layer (not the active HLU layer), so selection state
        /// is validated later in the load workflow, not here.
        /// </summary>
        private bool ComputeCanOSMMLoad()
        {
            // Must be in normal update mode (not bulk, OSMM review, or OSMM bulk modes).
            return IsEditReady &&
                   !IsBulkMode &&
                   !IsOsmmReviewMode &&
                   !IsOsmmBulkMode;
        }

        /// <summary>
        /// Refreshes the cached bulk load/unload enablement values.
        /// </summary>
        private void RefreshOSMMLoadUnloadEnablement()
        {
            bool canUnload = ComputeCanOSMMUnload();
            bool canLoad = ComputeCanOSMMLoad();

            bool changed = (_canBulkUnload != canUnload) || (_canBulkLoad != canLoad);
            _canBulkUnload = canUnload;
            _canBulkLoad = canLoad;

            if (changed)
            {
                OnPropertyChanged(nameof(CanBulkUnload));
                OnPropertyChanged(nameof(CanBulkLoad));
                OnPropertyChanged(nameof(CanBulkLoadUnload));
            }
        }

        #endregion Bulk Load/Unload Capability

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
            // Clear any existing messages
            ClearAllMessages();

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to split them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                // Warn the user that they need to select features that have been physically split before they can be logically split.
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
            {
                // Show success message
                ShowSuccess("Logical split completed.", MessageCategory.Split);

                // Notify user with pop-up window
                NotifySplitMerge("Logical split completed.");
            }
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
            // Clear any existing messages
            ClearAllMessages();

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Create ViewModel for split class.
            ViewModelWindowMainSplit vmSplit = new(this);

            // Execute the physical split and wait for the result.
            // Notify the user following the completion of the split.
            if (await vmSplit.PhysicalSplitAsync())
            {
                // Show success message
                ShowSuccess("Physical split completed.", MessageCategory.Split);

                // Notify user with pop-up window
                NotifySplitMerge("Physical split completed.");
            }
        }

        /// <summary>
        /// Performs a logical merge operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method retrieves the current map selection and then initiates the logical merge operation.
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LogicalMergeAsync()
        {
            // Clear any existing messages
            ClearAllMessages();

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                // Warn the user that they need to select features that have been physically split before they can be logically merged.
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
            {
                // Show success message
                ShowSuccess("Logical merge completed.", MessageCategory.Merge);

                // Notify user with pop-up window
                NotifySplitMerge("Logical merge completed.");
            }
        }

        /// <summary>
        /// Initiates the reassign features process.
        /// </summary>
        /// <remarks>This method initiates the reassign features process by creating an instance of the
        /// ViewModelWindowMainReassign class and calling its InitiateReassign method.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ReassignFeaturesAsync()
        {
            // Clear any existing messages
            ClearAllMessages();

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Create ViewModel for reassign class.
            ViewModelWindowMainReassign vmReassign = new(this);

            // Initiate the reassign process.
            vmReassign.InitiateReassign();

            // The method completes synchronously, but is declared as async Task to maintain
            // consistency with other similar methods like LogicalMergeAsync and OSMMLoadAsync.
            await Task.CompletedTask;
        }

        /// <summary>
        /// Performs a physical merge operation on the selected GIS layer.
        /// </summary>
        /// <remarks>This method retrieves the current map selection and then initiates the physical merge operation.
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task PhysicalMergeAsync()
        {
            // Clear any existing messages
            ClearAllMessages();

            // Get the GIS layer selection again (just in case).
            await GetMapSelectionAsync(false);

            // Check the selected rows are unique before attempting to merge them.
            if (!await _gisApp.SelectedRowsUniqueAsync())
            {
                // Warn the user that they need to select features that have been physically split before they can be physically merged.
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
            {
                // Show success message
                ShowSuccess("Physical merge completed.", MessageCategory.Merge);

                // Notify user with pop-up window
                NotifySplitMerge("Physical merge completed.");
            }
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
                _viewModelWinWarnSplitMerge.RequestClose -= ViewModelWinWarnSplitMerge_RequestClose; // Safety: avoid double subscription.
                _viewModelWinWarnSplitMerge.RequestClose +=
                    new ViewModelWindowNotifyOnSplitMerge.RequestCloseEventHandler(ViewModelWinWarnSplitMerge_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _windowWarnSplitMerge.DataContext = _viewModelWinWarnSplitMerge;

                // show window
                _windowWarnSplitMerge.ShowDialog();
            }
        }

        /// <summary>
        /// Update the user settings when the split merge request window is closed.
        /// </summary>
        private void ViewModelWinWarnSplitMerge_RequestClose()
        {
            // Remove the event handler and close the window.
            _viewModelWinWarnSplitMerge.RequestClose -= ViewModelWinWarnSplitMerge_RequestClose;
            _windowWarnSplitMerge.Close();

            // Update the user notify setting
            _notifyOnSplitMerge = Settings.Default.NotifyOnSplitMerge;
        }

        #endregion Split/Merge Action

        #region Feature Insert Action

        /// <summary>
        /// Registers all currently selected new (null-incid) features under a single new INCID.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InsertFeatureSameIncidAsync()
        {
            // Clear any existing messages.
            ClearAllMessages();

            // Re-read the map selection to ensure it is current.
            await GetMapSelectionAsync(false);

            // Delegate to the feature insert view model (implemented in stage 4/5).
            ViewModelWindowMainFeatureInsert vmInsert = new(this);
            var (success, featureCount, incidCount) = await vmInsert.InsertFeaturesSameIncidAsync();
            if (success)
            {
                ShowSuccess($"Feature insert completed: {featureCount} {(featureCount == 1 ? "feature" : "features")} registered against 1 new INCID.", MessageCategory.Insert);
            }
        }

        /// <summary>
        /// Registers each currently selected new (null-incid) feature under its own new INCID.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InsertFeatureSeparateIncidsAsync()
        {
            // Clear any existing messages.
            ClearAllMessages();

            // Re-read the map selection to ensure it is current.
            await GetMapSelectionAsync(false);

            // Delegate to the feature insert view model (implemented in stage 4/5).
            ViewModelWindowMainFeatureInsert vmInsert = new(this);
            var (success, featureCount, incidCount) = await vmInsert.InsertFeaturesSeparateIncidsAsync();
            if (success)
            {
                ShowSuccess($"Feature insert completed: {featureCount} {(featureCount == 1 ? "feature" : "features")} registered against {incidCount} new {(incidCount == 1 ? "INCID" : "INCIDs")}.", MessageCategory.Insert);
            }
        }

        #endregion Feature Insert Action

        #region Bulk Load/Unload Action

        /// <summary>
        /// Removes the currently selected registered GIS features from the layer, deletes their
        /// shadow map-match rows, and cleans up any orphaned INCID records.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OSMMUnloadAsync()
        {
            // Clear any existing messages.
            ClearAllMessages();

            // Re-read the map selection to ensure it is current.
            await GetMapSelectionAsync(false);

            // Prompt the user to confirm they have backed up the features before unloading.
            MessageBoxResult result = MessageBox.Show(
                "The Bulk Unload operation will permanently remove the selected features from the active layer and delete their associated database records.\n\n" +
                "Have you exported the selected features as a backup or snapshot and performed a database backup before proceeding?\n\n" +
                "Click 'Yes' to continue with the unload operation.\n" +
                "Click 'No' to cancel and perform an export and create a backup first.",
                "Bulk Unload - Backup Reminder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // If the user clicked No, cancel the operation.
            if (result == MessageBoxResult.No)
            {
                ShowInfo("Bulk Unload operation cancelled. Please create a backup before unloading.", MessageCategory.OSMMLoad);
                return;
            }

            ViewModelWindowMainBulkUnload vmUnload = new(this);
            int featureCount = await vmUnload.OSMMUnloadAsync();
            if (featureCount > 0)
            {
                ShowSuccess(
                    $"Bulk Unload completed: {featureCount} {(featureCount == 1 ? "feature" : "features")} removed.",
                    MessageCategory.OSMMLoad);
            }
        }

        /// <summary>
        /// Registers each currently selected new (null-INCID) GIS feature under its own new INCID,
        /// using habitat attributes already present on the feature, and tags history with the
        /// OSMMLoad operation code.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OSMMLoadAsync()
        {
            // Clear any existing messages.
            ClearAllMessages();

            // Re-read the map selection to ensure it is current.
            await GetMapSelectionAsync(false);

            // Show the Bulk Load setup dialog so the user can select the source layer
            // and map its fields to the lut_osmm_habitat_xref lookup columns.
            var dialogResult = await ShowOSMMLoadDialogAsync();
            if (dialogResult == default)
                return; // user cancelled

            (OsmmFieldMapping fieldMapping, bool selectedOnly, OutputType outputType) = dialogResult;

            // Get the default bulk load path.
            string bulkLoadPath = Settings.Default.DefaultBulkLoadPath;
            if (string.IsNullOrEmpty(bulkLoadPath) || !System.IO.Directory.Exists(bulkLoadPath))
                bulkLoadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Prompt the user for the staging layer output location and feature class name.
            var bulkLoadDetails = await GISApplication.BulkLoadPromptAsync(
                bulkLoadPath,
                outputType == OutputType.FileGeodatabase);

            // If the user didn't provide export details then exit.
            if (bulkLoadDetails == default)
            {
                ShowInfo("Bulk Load cancelled by user.", MessageCategory.OSMMLoad);
                return;
            }

            // Extract the output details.
            string outputWorkspace = bulkLoadDetails.outputWorkspace;
            string outputFeatureClassName = bulkLoadDetails.outputFeatureClassName;

            ViewModelWindowMainBulkLoad vmLoad = new(this);
            var (success, featureCount, incidCount) = await vmLoad.OSMMLoadAsync(
                fieldMapping, outputWorkspace, outputFeatureClassName, selectedOnly);

            if (success)
            {
                ShowSuccess(
                    $"Bulk Load completed: {featureCount} {(featureCount == 1 ? "feature" : "features")} registered against {incidCount} new {(incidCount == 1 ? "INCID" : "INCIDs")}.",
                    MessageCategory.OSMMLoad);
            }
        }

        /// <summary>
        /// Creates, shows and awaits the Bulk Load setup dialog.
        /// </summary>
        /// <returns>
        /// A tuple containing the <see cref="OsmmFieldMapping"/> chosen by the user, the selectedOnly flag,
        /// and the output type; or default if the dialog was cancelled.
        /// </returns>
        private Task<(OsmmFieldMapping mapping, bool selectedOnly, OutputType outputType)> ShowOSMMLoadDialogAsync()
        {
            var tcs = new TaskCompletionSource<(OsmmFieldMapping, bool, OutputType)>();

            var window = new WindowBulkLoad
            {
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            var vm = new ViewModelWindowBulkLoad(this);

            vm.RequestClose -= OnOSMMLoadDialogClose; // safety
            vm.RequestClose += OnOSMMLoadDialogClose;

            window.DataContext = vm;

            // Wire the close event to resolve the task.
            vm.RequestClose += (apply, mapping, selectedOnly, outputType) =>
            {
                window.Close();
                tcs.TrySetResult(apply ? (mapping, selectedOnly, outputType) : default);
            };

            window.ShowDialog();

            return tcs.Task;
        }

        // Dummy handler to satisfy the -= safety unsubscription above (lambda is the real handler).
        private static void OnOSMMLoadDialogClose(bool apply, OsmmFieldMapping mapping, bool selectedOnly, OutputType outputType)
        {
        }

        #endregion Bulk Load/Unload Action

        #endregion Methods
    }
}