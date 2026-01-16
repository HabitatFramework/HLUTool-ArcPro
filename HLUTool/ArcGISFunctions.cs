// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024-25 Andy Foy Consulting.
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

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Layouts.Utilities;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using HLU.Data;
using HLU.Data.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;

namespace HLU.GISApplication
{
    /// <summary>
    /// This class provides ArcGIS Pro map functions.
    /// </summary>
    internal partial class ArcProApp
    {
        #region Fields

        private Map _activeMap;
        private MapView _activeMapView;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Set the global variables.
        /// </summary>
        public ArcProApp()
        {
            // Get the active map view (if there is one).
            _activeMapView = GetActiveMapView();

            // Set the map currently displayed in the active map view.
            if (_activeMapView != null)
                _activeMap = _activeMapView.Map;
            else
                _activeMap = null;

            // Get the HLU featureLayer structure from the database.
            if (_hluLayerStructure == null)
                _hluLayerStructure = new HluGISLayer.incid_mm_polygonsDataTable();

            // Set the data type maps to/from SQL.
            GetTypeMaps(out _typeMapSystemToSQL, out _typeMapSQLToSystem);
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// The name of the active map.
        /// </summary>
        public string MapName
        {
            get
            {
                // If there is no active map, return null.
                if (_activeMap == null)
                    return null;
                else
                    return _activeMap.Name;
            }
        }

        #endregion Properties

        #region Debug Logging

        /// <summary>
        /// Writes any message to the Trace log with a timestamp.
        /// </summary>
        /// <param name="message"></param>
        private static void TraceLog(string message)
        {
            Trace.WriteLine($"{DateTime.Now:G} : {message}");
        }

        #endregion Debug Logging

        #region Map

        /// <summary>
        /// Retrieves the currently active map view, if one is available.
        /// </summary>
        /// <returns>
        /// The active <see cref="MapView"/> instance, or <c>null</c> if no map view is active.
        /// </returns>
        internal static MapView GetActiveMapView()
        {
            // Get the active map view from the ArcGIS Pro application.
            MapView mapView = MapView.Active;

            // Return the map view if available; otherwise, return null.
            return mapView;
        }

        /// <summary>
        /// Retrieves a map from the current project by its name.
        /// </summary>
        /// <param name="mapName">The name of the map to retrieve.</param>
        /// <returns>
        /// A <see cref="Map"/> instance if found; otherwise, <c>null</c>.
        /// </returns>
        public async Task<Map> GetMapFromNameAsync(string mapName)
        {
            // Return null if the input name is invalid.
            if (mapName == null)
            {
                TraceLog("GetMapFromNameAsync error: No map name provided.");
                return null;
            }

            Map map = null;

            // Run on the CIM thread to access project items safely.
            await QueuedTask.Run(() =>
            {
                // Search for the map project item by name and retrieve the associated Map object.
                map = Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name == mapName)
                    ?.GetMap();
            });

            // Return the found map, or null if not found.
            return map;
        }

        /// <summary>
        /// Resolves the <see cref="Map"/> associated with a map pane using its caption, activating the pane if needed.
        /// </summary>
        /// <param name="mapViewCaption">The caption of the map pane (tab title in ArcGIS Pro).</param>
        /// <returns>
        /// The corresponding <see cref="Map"/> if the pane is open and initialized; otherwise, <c>null</c>.
        /// </returns>
        public async Task<Map> GetMapFromCaptionAsync(string mapViewCaption)
        {
            if (string.IsNullOrWhiteSpace(mapViewCaption))
            {
                TraceLog("GetMapFromCaptionAsync error: No caption provided.");
                return null;
            }

            // Find the map pane by caption (regardless of activation state).
            var pane = FrameworkApplication.Panes
                .OfType<IMapPane>()
                .FirstOrDefault(p => (p as Pane)?.Caption.Equals(mapViewCaption.Trim(), StringComparison.OrdinalIgnoreCase) == true);

            if (pane == null)
            {
                TraceLog($"GetMapFromCaptionAsync error: No map pane found for caption '{mapViewCaption}'.");
                return null;
            }

            // Activate the pane to ensure MapView is fully initialized.
            (pane as Pane)?.Activate();

            // Retry loop: wait for MapView?.Map to be non-null (up to 5 seconds).
            const int maxWaitMs = 5000;
            const int delayIntervalMs = 200;
            int elapsedMs = 0;

            while (elapsedMs < maxWaitMs)
            {
                var map = pane.MapView?.Map;
                if (map != null)
                {
                    return map;
                }

                await Task.Delay(delayIntervalMs);
                elapsedMs += delayIntervalMs;
            }

            TraceLog($"GetMapFromCaptionAsync error: MapView is still null after waiting {maxWaitMs}ms for pane '{mapViewCaption}'.");

            return null;
        }

        /// <summary>
        /// Opens the specified map in a new pane if it's not already open, and activates it.
        /// </summary>
        /// <param name="map">The Map object to activate or open.</param>
        internal static async Task OpenMapAsync(Map map)
        {
            // Check if a pane is already open for this map.
            var pane = ProApp.Panes
                .OfType<Pane>()
                .FirstOrDefault(p =>
                {
                    if (p is IMapPane mp && mp.MapView.Map == map)
                        return true;
                    return false;
                });

            if (pane != null)
            {
                // Already open — activate it.
                pane.Activate();

                // Check it worked.
                var isActive = MapView.Active?.Map == map;
            }
            else
            {
                // Not open — open and activate it.
                var newPane = await ProApp.Panes.CreateMapPaneAsync(map);
            }
        }

        /// <summary>
        /// Activates the pane displaying the specified <see cref="Map"/> and returns its associated <see cref="MapView"/>.
        /// Falls back to the internally stored active map if no map is provided.
        /// </summary>
        /// <param name="targetMap">The map to activate, or <c>null</c> to use the internally stored active map.</param>
        /// <returns>
        /// The <see cref="MapView"/> associated with the activated pane, or <c>null</c> if not found.
        /// </returns>
        public async Task<MapView> ActivateMapAsync(Map targetMap)
        {
            // Use the provided map or fall back to the internally stored active map.
            Map mapToUse = targetMap ?? _activeMap;

            if (mapToUse == null)
            {
                TraceLog("ActivateMapAsync error: No map provided and no fallback map available.");
                return null;
            }

            // Search for an open map pane whose MapView references the target map.
            var pane = FrameworkApplication.Panes
                .OfType<IMapPane>()
                .FirstOrDefault(p => p.MapView?.Map == mapToUse);

            if (pane == null)
            {
                TraceLog($"ActivateMapAsync error: No open pane found for map '{mapToUse.Name}'.");
                return null;
            }

            // Activate the pane.
            (pane as Pane)?.Activate();

            // Retry loop: wait for MapView to be non-null (up to 5 seconds).
            const int maxWaitMs = 5000;
            const int delayIntervalMs = 200;
            int elapsedMs = 0;

            while (elapsedMs < maxWaitMs)
            {
                var mapView = pane.MapView;
                if (mapView != null)
                    return mapView;

                await Task.Delay(delayIntervalMs);
                elapsedMs += delayIntervalMs;
            }

            TraceLog($"ActivateMapAsync error: MapView is still null after waiting {maxWaitMs}ms for map '{mapToUse.Name}'.");

            return null;
        }

        /// <summary>
        /// Gets the <see cref="MapView"/> associated with the specified <see cref="Map"/>.
        /// </summary>
        /// <param name="map">The map to search for in open panes.</param>
        /// <returns>
        /// A task that returns the <see cref="MapView"/> displaying the map,
        /// or <c>null</c> if no such map view is found.
        /// </returns>
        public async Task<MapView> GetMapViewFromMapAsync(Map map)
        {
            if (map == null)
            {
                TraceLog("GetMapViewFromMapAsync error: No map provided.");
                return null;
            }

            // Fast path: active view already showing this map.
            MapView activeMapView = MapView.Active;
            if (activeMapView?.Map == map)
                return activeMapView;

            System.Windows.Threading.Dispatcher dispatcher = FrameworkApplication.Current?.Dispatcher
                ?? System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                TraceLog("GetMapViewFromMapAsync error: Dispatcher not available.");
                return null;
            }

            // UI thread: panes live here.
            MapView mapView = await dispatcher.InvokeAsync(() =>
            {
                return FrameworkApplication.Panes
                    .OfType<IMapPane>()
                    .Select(p => p.MapView)
                    .FirstOrDefault(v => v?.Map == map);
            });

            if (mapView == null)
                TraceLog($"GetMapViewFromMapAsync error: No MapView found for map '{map.Name}'.");

            return mapView;
        }

        /// <summary>
        /// Gets the <see cref="MapView"/> for an open pane whose map name matches the input.
        /// </summary>
        /// <param name="mapName">The name of the map.</param>
        /// <returns>The matching <see cref="MapView"/>, or <c>null</c> if not found.</returns>
        public MapView GetMapViewFromName(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                TraceLog("GetMapViewFromName error: Map name is null or empty.");
                return null;
            }

            // Fast path: active view already matches.
            MapView activeMapView = MapView.Active;
            if (activeMapView?.Map != null &&
                activeMapView.Map.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase))
            {
                return activeMapView;
            }

            System.Windows.Threading.Dispatcher dispatcher = FrameworkApplication.Current?.Dispatcher
                ?? System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                TraceLog("GetMapViewFromName error: Dispatcher not available.");
                return null;
            }

            // If we're already on the UI thread, do it directly.
            if (dispatcher.CheckAccess())
            {
                return FrameworkApplication.Panes
                    .OfType<IMapPane>()
                    .Select(p => p.MapView)
                    .FirstOrDefault(v => v?.Map != null &&
                                         v.Map.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
            }

            // Otherwise marshal synchronously to UI thread (this method is non-async by design).
            return dispatcher.Invoke(() =>
            {
                return FrameworkApplication.Panes
                    .OfType<IMapPane>()
                    .Select(p => p.MapView)
                    .FirstOrDefault(v => v?.Map != null &&
                                         v.Map.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
            });
        }

        /// <summary>
        /// Pauses or resumes drawing for the specified map, or the active map if none is provided.
        /// </summary>
        /// <param name="pause">If <c>true</c>, drawing will be paused; otherwise, drawing will be resumed.</param>
        /// <param name="targetMap">
        /// Optional map to control drawing for. If <c>null</c>, the internally tracked active map is used.
        /// </param>
        public void PauseDrawing(bool pause, Map targetMap = null)
        {
            // Use the provided map or fall back to the internally stored active map.
            Map mapToUse = targetMap ?? _activeMap;

            // Attempt to retrieve the MapView for the specified map.
            MapView mapViewToUse = GetMapViewFromName(mapToUse.Name);
            if (mapViewToUse == null)
            {
                // Log if the view could not be found — the map may not be open.
                TraceLog("PauseDrawingAsync error: MapView not found.");
                return;
            }

            // Pause or resume drawing depending on the input parameter.
            // This can be useful when performing batch updates or long-running edits.
            mapViewToUse.DrawingPaused = pause;
        }

        /// <summary>
        /// Creates a new map with the specified name and optionally sets it as the active map.
        /// </summary>
        /// <param name="mapName">The name of the new map to create.</param>
        /// <param name="setActive">If true, the new map will be set as active. Otherwise, the current map remains active.</param>
        /// <returns>The name of the newly created map, or null if creation failed.</returns>
        public async Task<string> CreateMapAsync(string mapName, bool setActive = true)
        {
            if (string.IsNullOrEmpty(mapName))
            {
                TraceLog("CreateMapAsync error: Map name is null or empty.");
                return null;
            }

            // Save the current active pane.
            Pane currentPane = ProApp.Panes.ActivePane;
            Map newMap = null;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a new map without a basemap.
                    newMap = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);
                });

                // Create the map pane (this must be awaited as it's async).
                var newPane = await ProApp.Panes.CreateMapPaneAsync(newMap, MapViewingMode.Map);

                if (setActive)
                {
                    _activeMap = newMap;
                }
                else
                {
                    // Return to the previously active pane if available.
                    currentPane?.Activate();
                }

                return newMap.Name;
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"CreateMapAsync error: Failed to create map '{mapName}', Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds a layer from a URL to the specified map, or the active map if none is provided.
        /// </summary>
        /// <param name="url">The URL of the layer to add.</param>
        /// <param name="index">The index at which to insert the layer (default is 0).</param>
        /// <param name="layerName">An optional custom name for the layer. If not provided, a name is derived from the URL.</param>
        /// <param name="targetMap">The target map to which the layer will be added. Defaults to the active map if null.</param>
        /// <returns>True if the layer was added successfully; otherwise, false.</returns>
        public async Task<bool> AddLayerToMapAsync(string url, int index = 0, string layerName = "", Map targetMap = null)
        {
            // If the URL is null or whitespace, return false.
            if (string.IsNullOrWhiteSpace(url))
            {
                TraceLog("AddLayerToMapAsync error: URL is null or empty.");
                return false;
            }

            // Use the provided map, or fall back to the active map if none is given.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a URI object from the input URL.
                    Uri uri = new(url);

                    // Use the filename (without extension) as the default layer name if none is provided.
                    string defaultName = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                    string nameToUse = string.IsNullOrWhiteSpace(layerName) ? defaultName : layerName;

                    // Check whether a layer with the same name already exists in the map.
                    bool layerExists = mapToUse.Layers
                        .Any(l => l.Name.Equals(nameToUse, StringComparison.OrdinalIgnoreCase));

                    // If not found, create and add the layer at the specified index.
                    if (!layerExists)
                        LayerFactory.Instance.CreateLayer(uri, mapToUse, index, nameToUse);
                });

                return true;
            }
            catch (Exception ex)
            {
                // Log and return false if any exception occurs during the process.
                TraceLog($"AddLayerToMapAsync error: Failed to add layer from URL '{url}', Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds a standalone table to the specified map, or to the active map if none is provided.
        /// </summary>
        /// <param name="url">The URL or local path of the table to add.</param>
        /// <param name="index">The index at which to insert the table in the standalone table collection (default is 0).</param>
        /// <param name="tableName">An optional custom name for the table. If not provided, a name is derived from the URL.</param>
        /// <param name="targetMap">The map to add the table to. If null, the active map is used.</param>
        /// <returns>True if the table was added successfully; otherwise, false.</returns>
        public async Task<bool> AddTableToMapAsync(string url, int index = 0, string tableName = "", Map targetMap = null)
        {
            // Validate the input URL.
            if (string.IsNullOrWhiteSpace(url))
            {
                TraceLog("AddTableToMapAsync error: URL is null or empty.");
                return false;
            }

            // Use the provided map or fall back to the active map (guaranteed non-null).
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a URI from the provided URL.
                    Uri uri = new(url);

                    // Use the filename (without extension) as the default layer name if none is provided.
                    string defaultName = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                    string nameToUse = string.IsNullOrWhiteSpace(tableName) ? defaultName : tableName;

                    // Check if a table with the same name already exists in the map.
                    bool tableExists = mapToUse.StandaloneTables
                        .Any(t => t.Name.Equals(nameToUse, StringComparison.OrdinalIgnoreCase));

                    // If not found, create and add the standalone table at the specified index.
                    if (!tableExists)
                        StandaloneTableFactory.Instance.CreateStandaloneTable(uri, mapToUse, index, nameToUse);
                });

                return true;
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"AddTableToMapAsync error: Failed to add table from URL '{url}', Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Zooms to a feature in the specified layer using a given scale or distance factor.
        /// </summary>
        /// <param name="layerName">The name of the feature layer to zoom to.</param>
        /// <param name="objectID">The object ID of the feature to zoom to.</param>
        /// <param name="factor">Optional. The zoom factor to apply (e.g., 2.0 for twice the extent size).</param>
        /// <param name="mapScaleOrDistance">Optional. The desired map scale or distance in map units.</param>
        /// <param name="targetMap">Optional. The target map to use. Defaults to the active map if null.</param>
        /// <returns>True if zoom was successful; otherwise, false.</returns>
        public async Task<bool> ZoomToFeatureInMapAsync(
            string layerName,
            long objectID,
            double? factor,
            double? mapScaleOrDistance,
            Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return false;

            // Check if the input factor is valid.
            if (factor.HasValue && factor.Value <= 0)
                return false;

            // Check if the input mapScaleOrDistance is valid.
            if (mapScaleOrDistance.HasValue && factor.Value <= 0)
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            // Get the map view associated with the map.
            MapView mapViewToUse = GetMapViewFromName(mapToUse.Name);
            if (mapViewToUse == null)
                return false;

            // Find the target feature layer.
            var targetLayer = await FindLayerAsync(layerName, mapToUse);
            if (targetLayer is not FeatureLayer featureLayer)
                return false;

            try
            {
                // Zoom to the extent of the specified object ID.
                await mapViewToUse.ZoomToAsync(
                    featureLayer,
                    objectID,
                    duration: null,
                    maintainViewDirection: true,
                    factor: factor,
                    mapScaleOrDistance: mapScaleOrDistance);
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"ZoomToFeatureInMapAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zooms to the extent of specified object IDs in a feature layer.
        /// </summary>
        /// <param name="layerName">The name of the layer containing the objects.</param>
        /// <param name="objectIDs">A list of object IDs to zoom to.</param>
        /// <param name="targetMap">Optional target map; defaults to _activeMap.</param>
        /// <returns>True if zoom succeeded; false otherwise.</returns>
        public async Task<bool> ZoomToFeaturesInMapAsync(string layerName,
            IEnumerable<long> objectIDs,
            Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
            {
                TraceLog("ZoomToFeaturesInMapAsync error: No layer name provided.");
                return false;
            }

            // Check if there are any input objects.
            if (objectIDs == null || !objectIDs.Any())
            {
                TraceLog("ZoomToFeaturesInMapAsync error: No object IDs provided.");
                return false;
            }

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            // Get the map view associated with the map.
            MapView mapViewToUse = GetMapViewFromName(mapToUse.Name);
            if (mapViewToUse == null)
                return false;

            // Find the target feature layer.
            var targetLayer = await FindLayerAsync(layerName, mapToUse);
            if (targetLayer is not FeatureLayer featureLayer)
                return false;

            try
            {
                // Zoom to the extent of the specified object IDs.
                await mapViewToUse.ZoomToAsync(featureLayer,
                    objectIDs,
                    duration: null,
                    maintainViewDirection: true);
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"ZoomToFeaturesInMapAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zooms to the extent of a layer in a map view for a given ratio or nearest valid scale.
        /// </summary>
        /// <param name="layerName">The name of the layer to zoom to.</param>
        /// <param name="selectedOnly">If true, zooms to selected features only.</param>
        /// <param name="ratio">Optional zoom ratio multiplier.</param>
        /// <param name="scale">Optional fixed scale to set after zooming.</param>
        /// <param name="targetMap">Optional map to use; defaults to _activeMap.</param>
        /// <returns>True if zoom succeeded; false otherwise.</returns>
        public async Task<bool> ZoomToLayerInMapAsync(string layerName,
            bool selectedOnly,
            double? ratio = 1,
            double? scale = 10000,
            Map targetMap = null)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                TraceLog("ZoomToLayerInMapAsync error: No layer name provided.");
                return false;
            }

            if (ratio.HasValue && ratio.Value <= 0)
            {
                TraceLog($"ZoomToLayerInMapAsync error: Invalid zoom ratio: {ratio}.");
                return false;
            }

            if (scale.HasValue && scale.Value <= 0)
            {
                TraceLog($"ZoomToLayerInMapAsync error: Invalid zoom scale: {scale}.");
                return false;
            }

            Map mapToUse = targetMap ?? _activeMap;
            MapView mapViewToUse = GetMapViewFromName(mapToUse.Name);
            if (mapViewToUse == null)
            {
                TraceLog("ZoomToLayerInMapAsync error: Map view could not be found.");
                return false;
            }

            Layer targetLayer = await FindLayerAsync(layerName, mapToUse);
            if (targetLayer == null)
            {
                TraceLog($"ZoomToLayerInMapAsync error: Layer '{layerName}' not found in map.");
                return false;
            }

            try
            {
                // Zoom to the extent of the layer or its selection.
                await mapViewToUse.ZoomToAsync(targetLayer, selectedOnly);

                // Get the current camera.
                var camera = mapViewToUse.Camera;

                // Apply ratio or fixed scale (mutually exclusive).
                if (ratio.HasValue)
                {
                    camera.Scale *= (double)ratio;
                }
                else if (scale.HasValue)
                {
                    camera.Scale = (double)scale;
                }

                // Apply the modified camera.
                await mapViewToUse.ZoomToAsync(camera, duration: null);
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"ZoomToLayerInMapAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts labels to annotation for a specified layer or for all layers in a map.
        /// This wraps the 'Convert Labels to Annotation' geoprocessing tool and supports
        /// standard or feature-linked annotation, with control over extent and conflict options.
        /// </summary>
        /// <param name="input_map">The name of the map containing the layer(s).</param>
        /// <param name="single_layer">The name of the layer to convert. Use an empty string ("") to convert all layers.</param>
        /// <param name="output_geodatabase">The path to the output geodatabase for storing annotation feature classes.</param>
        /// <param name="anno_suffix">The suffix added to the output annotation feature class.</param>
        /// <param name="extent">The extent that contains the labels to convert to annotation</param>
        /// <param name="addToMap">If true, adds the output annotation layer to the map.</param>
        /// <param name="conversion_scale">The reference scale at which annotation is created. If empty, the current map view scale is used.</param>
        /// <param name="featureLinked">If true, creates feature-linked annotation; otherwise, creates standard annotation.</param>
        /// <param name="output_group_layer"></param>
        /// <returns>True if annotation was successfully created; otherwise, false.</returns>
        public async Task<bool> ConvertLabelsToAnnotationAsync(
            string input_map,
            string single_layer,
            string output_geodatabase,
            string anno_suffix,
            string extent = "MAXOF",
            string generate_unplaced = "ONLY_PLACED",
            bool addToMap = false,
            string conversion_scale = "",
            bool featureLinked = false,
            string output_group_layer = "Anno")
        {
            // Check for required parameters.
            if (string.IsNullOrEmpty(input_map) || string.IsNullOrEmpty(output_geodatabase) || string.IsNullOrEmpty(anno_suffix))
            {
                TraceLog("ConvertLabelsToAnnotationAsync error: Missing required parameters.");
                return false;
            }

            // If no reference scale provided, use the current map view scale.
            if (string.IsNullOrWhiteSpace(conversion_scale))
            {
                if (MapView.Active?.Camera != null)
                {
                    conversion_scale = MapView.Active.Camera.Scale.ToString("F0");
                }
                else
                {
                    TraceLog("ConvertLabelsToAnnotationAsync error: Unable to determine reference scale.");
                    return false;
                }
            }

            // Determine annotation type.
            string feature_linked = featureLinked ? "FEATURE_LINKED" : "STANDARD";

            // Determine whether to convert all layers or just the specified one.
            string which_layers = string.IsNullOrWhiteSpace(single_layer)
                ? "ALL_LAYERS"
                : "SINGLE_LAYER";

            // Make a value array of strings to be passed to the tool.
            IReadOnlyList<string> parameters = Geoprocessing.MakeValueArray(
                input_map,             // Name of the map containing the input layer(s).
                conversion_scale,      // Reference scale for the annotation.
                output_geodatabase,    // Output file geodatabase path.
                anno_suffix,           // Suffix for the output annotation feature class.
                extent,                // "MAXOF" (default), "MINOF", "DISPLAY", LayerName.
                generate_unplaced,     // "ONLY_PLACED" (default), "GENERATE_UNPLACED".
                null,                  // "NO_REQUIRE_ID" (default), "REQUIRE_ID".
                feature_linked,        // "STANDARD" (default), "FEATURE_LINKED".
                null,                  // "AUTO_CREATE" (default), "NO_AUTO_CREATE".
                null,                  // "SHAPE_UPDATE" (default), "NO_SHAPE_UPDATE".
                output_group_layer,    // Group layer that will contain the generated annotation.
                which_layers,          // "ALL_LAYERS" (default), "SINGLE_LAYER".
                single_layer,          // Name of the layer to convert ("" = all layers).
                null,                  // "FEATURE_CLASS_PER_FEATURE_LAYER" (default), "SINGLE_FEATURE_CLASS".
                null                   // "NO_MERGE_LABEL_CLASS" (default), "MERGE_LABEL_CLASS".
            );

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("cartography.ConvertLabelsToAnnotation", parameters);  // Useful for debugging.

            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync(
                    "cartography.ConvertLabelsToAnnotation",
                    parameters,
                    environments,
                    null,
                    null,
                    executeFlags
                );

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                TraceLog($"ConvertLabelsToAnnotationAsync error: Map: {input_map}, Layer: {single_layer}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        #endregion Map

        #region Layout

        /// <summary>
        /// Resolves the <see cref="Layout"/> associated with a layout pane using its caption, activating the pane if needed.
        /// </summary>
        /// <param name="layoutViewCaption">The caption of the layout pane (tab title in ArcGIS Pro).</param>
        /// <returns>
        /// The corresponding <see cref="Layout"/> if the pane is open and initialized; otherwise, <c>null</c>.
        /// </returns>
        public async Task<Layout> GetLayoutFromCaptionAsync(string layoutViewCaption)
        {
            if (string.IsNullOrWhiteSpace(layoutViewCaption))
            {
                TraceLog("GetLayoutFromCaptionAsync error: No caption provided.");
                return null;
            }

            // Find the layout pane by caption.
            var pane = FrameworkApplication.Panes
                .OfType<ILayoutPane>()
                .FirstOrDefault(p => (p as Pane)?.Caption.Equals(layoutViewCaption.Trim(), StringComparison.OrdinalIgnoreCase) == true);

            if (pane == null)
            {
                TraceLog($"GetLayoutFromCaptionAsync error: No layout pane found for caption '{layoutViewCaption}'.");
                return null;
            }

            // Activate the pane to force view initialization.
            (pane as Pane)?.Activate();

            // Retry loop: wait for LayoutView?.Layout to be non-null (up to 5 seconds).
            const int maxWaitMs = 5000;
            const int delayIntervalMs = 200;
            int elapsedMs = 0;

            while (elapsedMs < maxWaitMs)
            {
                var layout = pane.LayoutView?.Layout;
                if (layout != null)
                {
                    return layout;
                }

                await Task.Delay(delayIntervalMs);
                elapsedMs += delayIntervalMs;
            }

            TraceLog($"GetLayoutFromCaptionAsync error: Layout is still null after waiting {maxWaitMs}ms for pane '{layoutViewCaption}'.");

            return null;
        }

        /// <summary>
        /// Activates the pane displaying the specified <see cref="Layout"/> and returns its associated <see cref="LayoutView"/>.
        /// </summary>
        /// <param name="targetLayout">The layout to activate, or <c>null</c> to use the internally stored active layout.</param>
        /// <returns>
        /// The <see cref="LayoutView"/> associated with the activated pane, or <c>null</c> if not found.
        /// </returns>
        public async Task<LayoutView> ActivateLayoutAsync(Layout targetLayout)
        {
            if (targetLayout == null)
            {
                TraceLog("ActivateLayoutAsync error: No layout provided and no fallback layout available.");
                return null;
            }

            // Search for an open layout pane whose LayoutView references the target layout.
            var pane = FrameworkApplication.Panes
                .OfType<ILayoutPane>()
                .FirstOrDefault(p => p.LayoutView?.Layout == targetLayout);

            if (pane == null)
            {
                TraceLog($"ActivateLayoutAsync error: No open pane found for layout '{targetLayout.Name}'.");
                return null;
            }

            // Activate the pane.
            (pane as Pane)?.Activate();

            // Retry loop: wait for LayoutView to be non-null (up to 5 seconds).
            const int maxWaitMs = 5000;
            const int delayIntervalMs = 200;
            int elapsedMs = 0;

            while (elapsedMs < maxWaitMs)
            {
                var layoutView = pane.LayoutView;
                if (layoutView != null)
                {
                    return layoutView;
                }

                await Task.Delay(delayIntervalMs);
                elapsedMs += delayIntervalMs;
            }

            TraceLog($"ActivateLayoutAsync error: LayoutView is still null after waiting {maxWaitMs}ms for layout '{targetLayout.Name}'.");

            return null;
        }

        /// <summary>
        /// Updates specific text elements in all currently open layouts matching the provided names.
        /// </summary>
        /// <param name="layoutNames">List of layout names to update.</param>
        /// <param name="siteNameElement">The name of the text element to update for the site name.</param>
        /// <param name="siteName">The new site name text value.</param>
        /// <param name="searchRefElement">The name of the text element to update for the search reference.</param>
        /// <param name="searchRef">The new search reference text value.</param>
        /// <param name="organisationElement">The name of the text element to update for the organisation.</param>
        /// <param name="organisationText">The new organisation value.</param>
        /// <param name="radiusElement">The name of the text element to update for the search radius.</param>
        /// <param name="radiusText">The new search radius text value.</param>
        /// <param name="bespokeElementNames">List of bespoke text element names to update.</param>
        /// <param name="bespokeContents">List of bespoke text values to set for the corresponding elements.</param>
        /// <returns>True if all text updates succeeded across all open layouts; otherwise, false.</returns>
        public async Task<bool> UpdateLayoutsTextAsync(
            List<string> layoutNames,
            string searchRefElement,
            string searchRef,
            string siteNameElement,
            string siteName,
            string organisationElement,
            string organisationText,
            string radiusElement,
            string radiusText,
            List<string> bespokeElementNames,
            List<string> bespokeContents)
        {
            foreach (string layoutName in layoutNames)
            {
                // Attempt to retrieve the layout item by name from the project.
                LayoutProjectItem layoutItem = Project.Current
                    .GetItems<LayoutProjectItem>()
                    .FirstOrDefault(item => item.Name == layoutName);

                if (layoutItem == null)
                {
                    TraceLog($"UpdateLayoutsTextAsync error: Layout '{layoutName}' not found.");
                    continue;
                }

                // Get the layout object from the layout item.
                Layout layout = await QueuedTask.Run(() => layoutItem.GetLayout());

                // Determine if the layout is currently open in a layout view.
                bool isOpen = await QueuedTask.Run(() =>
                {
                    return ProApp.Panes
                        .OfType<ILayoutPane>()
                        .Any(lp => lp.LayoutView?.Layout == layout);
                });

                // Skip updates if the layout is not currently open.
                if (!isOpen)
                {
                    TraceLog($"UpdateLayoutsTextAsync error: Layout '{layoutName}' is not open. Skipping.");
                    continue;
                }

                // Update the search reference text element.
                if (!string.IsNullOrWhiteSpace(searchRefElement))
                {
                    if (!await SetTextElementsAsync(layoutName, searchRefElement, searchRef))
                    {
                        TraceLog($"UpdateLayoutsTextAsync error: Failed to update '{searchRefElement}' in layout '{layoutName}'.");
                        return false;
                    }
                }

                // Update the site name text element.
                if (!string.IsNullOrWhiteSpace(siteNameElement))
                {
                    if (!await SetTextElementsAsync(layoutName, siteNameElement, siteName))
                    {
                        TraceLog($"UpdateLayoutsTextAsync error: Failed to update '{siteNameElement}' in layout '{layoutName}'.");
                        return false;
                    }
                }

                // Update the organisation text element.
                if (!string.IsNullOrWhiteSpace(organisationElement))
                {
                    if (!await SetTextElementsAsync(layoutName, organisationElement, organisationText))
                    {
                        TraceLog($"UpdateLayoutsTextAsync error: Failed to update '{organisationElement}' in layout '{layoutName}'.");
                        return false;
                    }
                }

                // Update the search radius text element.
                if (!string.IsNullOrWhiteSpace(radiusElement))
                {
                    if (!await SetTextElementsAsync(layoutName, radiusElement, radiusText))
                    {
                        TraceLog($"UpdateLayoutsTextAsync error: Failed to update '{radiusElement}' in layout '{layoutName}'.");
                        return false;
                    }
                }

                // Update the bespoke text elements.
                for (int i = 0; i < bespokeElementNames.Count; i++)
                {
                    string bespokeElement = bespokeElementNames[i];
                    string bespokeContent = bespokeContents[i];

                    // Update the bespoke text element.
                    if (!string.IsNullOrWhiteSpace(bespokeElement))
                    {
                        // We assume SetTextElementsAsync accepts a single content string here.
                        // If it needs a list, wrap bespokeContent in a new List<string>.
                        if (!await SetTextElementsAsync(layoutName, bespokeElement, bespokeContent))
                        {
                            TraceLog($"UpdateLayoutsTextAsync error: Failed to update '{bespokeElement}' in layout '{layoutName}'.");
                            return false;
                        }
                    }
                }
            }

            // All updates completed successfully.
            return true;
        }

        /// <summary>
        /// Updates the text content of a named text element in a specified layout.
        /// </summary>
        /// <param name="layoutName">The name of the layout containing the text element.</param>
        /// <param name="textName">The name of the text element to update.</param>
        /// <param name="textString">The new string to set as the text element's content.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> SetTextElementsAsync(string layoutName, string textName, string textString)
        {
            // Validate inputs.
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                TraceLog("SetTextElementsAsync error: Layout name is null or empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(textName))
            {
                TraceLog("SetTextElementsAsync error: Text element name is invalid.");
                return false;
            }

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Get the layout by name from the project.
                    Layout layout = Project.Current.GetItems<LayoutProjectItem>()
                                                   .FirstOrDefault(item => item.Name == layoutName)
                                                   ?.GetLayout();

                    if (layout == null)
                    {
                        TraceLog($"SetTextElementsAsync error: Layout '{layoutName}' not found.");
                        return;
                    }

                    // Attempt to find the specified text element in the layout.
                    if (layout.FindElement(textName) is TextElement textElement)
                    {
                        // Get the text graphic from the element.
                        if (textElement.GetGraphic() is CIMTextGraphic cimTextGraphic)
                        {
                            // Set the new text content.
                            cimTextGraphic.Text = textString;

                            // Apply the updated graphic back to the text element.
                            textElement.SetGraphic(cimTextGraphic);
                        }
                        else
                        {
                            TraceLog($"SetTextElementsAsync error: Failed to get CIMTextGraphic for element '{textName}'.");
                            return;
                        }
                    }
                    //else
                    //{
                    //    TraceLog($"SetTextElementsAsync error: Text element '{textName}' not found in layout '{layoutName}'.");
                    //    return;
                    //}

                    success = true;
                });

                return success;
            }
            catch (Exception ex)
            {
                // Log any unexpected exception and return false.
                TraceLog($"SetTextElementsAsync error: Failed tp update text element '{textName}' in layout '{layoutName}', Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Zooms to a specific feature in a layout's map frame by ObjectID using a given scale or distance factor.
        /// </summary>
        /// <param name="layoutName">The name of the layout containing the map frame.</param>
        /// <param name="layerName">The name of the feature layer to zoom to.</param>
        /// <param name="objectID">The ObjectID of the feature to zoom to.</param>
        /// <param name="ratio">Optional. Zoom ratio multiplier.</param>
        /// <param name="scale">Optional. Fixed scale to set after zooming.</param>
        /// <param name="mapFrameName">Optional. The name of the map frame. Defaults to "Map Frame".</param>
        /// <param name="validScales">Optional. A list of valid scales. If provided, the next scale up is chosen based on the current scale.</param>
        /// <returns>True if zoom was successful; otherwise, false.</returns>
        public async Task<bool> ZoomToFeatureInLayoutAsync(
            string layoutName,
            string layerName,
            long objectID,
            double? ratio = null,
            double? scale = null,
            List<int> validScales = null,
            string mapFrameName = "Map Frame")
        {
            // Validate required parameters.
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                TraceLog("ZoomToFeatureInLayoutAsync error: Layout name is null or empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                TraceLog("ZoomToFeatureInLayoutAsync error: Layer name is null or empty.");
                return false;
            }

            if (objectID < 0)
            {
                TraceLog("ZoomToFeatureInLayoutAsync error: Invalid ObjectID.");
                return false;
            }

            if (ratio.HasValue && ratio.Value <= 0)
            {
                TraceLog($"ZoomToFeatureInLayoutAsync error: Invalid factor value: {ratio}.");
                return false;
            }

            if (scale.HasValue && scale.Value <= 0)
            {
                TraceLog($"ZoomToFeatureInLayoutAsync error: Invalid mapScaleOrDistance value: {scale}.");
                return false;
            }

            // Try to locate the layout by name.
            LayoutProjectItem layoutItem = Project.Current
                .GetItems<LayoutProjectItem>()
                .FirstOrDefault(l => l.Name.Equals(layoutName, StringComparison.OrdinalIgnoreCase));

            if (layoutItem == null)
            {
                TraceLog($"ZoomToFeatureInLayoutAsync error: Layout '{layoutName}' not found.");
                return false;
            }

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // Open the layout.
                    Layout layout = layoutItem.GetLayout();
                    if (layout == null)
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: Layout '{layoutName}' could not be opened.");
                        return false;
                    }

                    // Locate the named map frame.
                    if (layout.FindElement(mapFrameName) is not MapFrame mapFrame)
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: Map frame '{mapFrameName}' not found in layout '{layoutName}'.");
                        return false;
                    }

                    Map map = mapFrame.Map;
                    if (map == null)
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: Map in map frame '{mapFrameName}' is null.");
                        return false;
                    }

                    // Locate the feature layer by name.
                    var layer = await FindLayerAsync(layerName, map);
                    if (layer is not FeatureLayer featureLayer)
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: Feature layer '{layerName}' not found in map.");
                        return false;
                    }

                    // Query the feature geometry by ObjectID.
                    var queryFilter = new QueryFilter
                    {
                        ObjectIDs = [objectID]
                    };

                    RowCursor cursor = featureLayer.Search(queryFilter);
                    if (!cursor.MoveNext())
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: No feature found with ObjectID {objectID} in layer '{layerName}'.");
                        return false;
                    }

                    using var row = cursor.Current as Feature;
                    Geometry geometry = row?.GetShape();

                    if (geometry == null || geometry.IsEmpty)
                    {
                        TraceLog($"ZoomToFeatureInLayoutAsync error: Geometry is null or empty for ObjectID {objectID}.");
                        return false;
                    }

                    // Get the envelope of the geometry.
                    Envelope extent = geometry.Extent;

                    // Set the camera extent on the map frame.
                    mapFrame.SetCamera(extent);

                    // Apply zoom ratio or scale to map frame.
                    //ApplyZoomToMapFrame(mapFrame, ratio, scale, validScales);

                    return true;
                }
                catch (Exception ex)
                {
                    // Log any unexpected exception and return false.
                    TraceLog($"ZoomToFeatureInLayoutAsync error: Problem while zooming to feature. Exception: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Zooms to the extent of specified object IDs in a feature layer within a layout's map frame.
        /// </summary>
        /// <param name="layoutName">The name of the layout containing the map frame.</param>
        /// <param name="layerName">The name of the layer containing the objects.</param>
        /// <param name="objectIDs">A list of object IDs to zoom to.</param>
        /// <param name="ratio">Optional. Zoom ratio multiplier.</param>
        /// <param name="scale">Optional. Fixed scale to set after zooming.</param>
        /// <param name="mapFrameName">Optional. The name of the map frame. Defaults to "Map Frame".</param>
        /// <param name="validScales">Optional. A list of valid scales. If provided, the next scale up is chosen based on the current scale.</param>
        /// <returns>True if zoom succeeded; false otherwise.</returns>
        public async Task<bool> ZoomToFeaturesInLayoutAsync(
            string layoutName,
            string layerName,
            IEnumerable<long> objectIDs,
            double? ratio = 1,
            double? scale = 10000,
            List<int> validScales = null,
            string mapFrameName = "Map Frame")

        {
            // Validate inputs.
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                TraceLog("ZoomToFeaturesInLayoutAsync error: Layout name is null or empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                TraceLog("ZoomToFeaturesInLayoutAsync error: Layer name is null or empty.");
                return false;
            }

            if (objectIDs == null || !objectIDs.Any())
            {
                TraceLog("ZoomToFeaturesInLayoutAsync error: Object ID list is null or empty.");
                return false;
            }

            if (ratio.HasValue && ratio.Value <= 0)
            {
                TraceLog($"ZoomToFeaturesInLayoutAsync error: Invalid ratio value: {ratio}.");
                return false;
            }

            if (scale.HasValue && scale.Value <= 0)
            {
                TraceLog($"ZoomToFeaturesInLayoutAsync error: Invalid scale value: {scale}.");
                return false;
            }

            // Try to find the layout.
            LayoutProjectItem layoutItem = Project.Current
                .GetItems<LayoutProjectItem>()
                .FirstOrDefault(l => l.Name.Equals(layoutName, StringComparison.OrdinalIgnoreCase));

            if (layoutItem == null)
            {
                TraceLog($"ZoomToFeaturesInLayoutAsync error: Layout '{layoutName}' not found.");
                return false;
            }

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // Open layout from the project item.
                    Layout layout = layoutItem.GetLayout();
                    if (layout == null)
                    {
                        TraceLog($"ZoomToFeaturesInLayoutAsync error: Layout '{layoutName}' could not be opened.");
                        return false;
                    }

                    // Get the map frame from the layout.
                    if (layout.FindElement(mapFrameName) is not MapFrame mapFrame)
                    {
                        TraceLog($"ZoomToFeaturesInLayoutAsync error: Map frame '{mapFrameName}' not found in layout '{layoutName}'.");
                        return false;
                    }

                    Map map = mapFrame.Map;
                    if (map == null)
                    {
                        TraceLog($"ZoomToFeaturesInLayoutAsync error: Map in map frame '{mapFrameName}' is null.");
                        return false;
                    }

                    // Find the feature layer.
                    var layer = await FindLayerAsync(layerName, map);
                    if (layer is not FeatureLayer featureLayer)
                    {
                        TraceLog($"ZoomToFeaturesInLayoutAsync error: Feature layer '{layerName}' not found in map.");
                        return false;
                    }

                    // Set up query filter for the object IDs.
                    var filter = new QueryFilter
                    {
                        ObjectIDs = objectIDs.ToList()
                    };

                    // Search for the features and build the combined extent.
                    Envelope combinedExtent = null;

                    using RowCursor cursor = featureLayer.Search(filter, null);
                    while (cursor.MoveNext())
                    {
                        using var row = cursor.Current as Feature;
                        Geometry shape = row?.GetShape();
                        if (shape != null && !shape.IsEmpty)
                        {
                            Envelope shapeExtent = shape.Extent;
                            if (combinedExtent == null)
                            {
                                combinedExtent = shapeExtent;
                            }
                            else
                            {
                                combinedExtent = combinedExtent.Union(shapeExtent);
                            }
                        }
                    }

                    if (combinedExtent == null || combinedExtent.IsEmpty)
                    {
                        TraceLog($"ZoomToFeaturesInLayoutAsync error: No valid geometries found for layer '{layerName}'.");
                        return false;
                    }

                    // Apply the combined extent to the map frame.
                    mapFrame.SetCamera(combinedExtent);

                    // Apply zoom logic using camera scale strategy.
                    //ApplyZoomToMapFrame(mapFrame, ratio, scale, validScales);

                    return true;
                }
                catch (Exception ex)
                {
                    // Log any unexpected exception and return false.
                    TraceLog($"ZoomToFeaturesInLayoutAsync error: Problem while zooming to features. Exception: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Zooms to the extent of a layer in a layout's map frame using the given layout and map frame name.
        /// </summary>
        /// <param name="layout">The layout containing the map frame.</param>
        /// <param name="layerName">The name of the layer to zoom to.</param>
        /// <param name="selectedOnly">If true, zooms to selected features only.</param>
        /// <param name="ratio">Optional zoom ratio multiplier.</param>
        /// <param name="scale">Optional fixed scale to set after zooming.</param>
        /// <param name="mapFrameName">Optional name of the map frame to use; defaults to "Map Frame".</param>
        /// <returns>True if zoom succeeded; false otherwise.</returns>
        public async Task<bool> ZoomToLayerInLayoutAsync(Layout layout,
            string layerName,
            bool selectedOnly,
            double? ratio = 1,
            double? scale = 10000,
            List<int> validScales = null,
            string mapFrameName = "Map Frame")
        {
            // Validate layout and layer names.
            if (layout == null)
            {
                TraceLog("ZoomToLayerInLayoutAsync error: Layout is null.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                TraceLog("ZoomToLayerInLayoutAsync error: Layer name is null or empty.");
                return false;
            }

            // Validate zoom ratio.
            if (ratio.HasValue && ratio.Value <= 0)
            {
                TraceLog($"ZoomToLayerInLayoutAsync error: Invalid ratio value: {ratio}.");
                return false;
            }

            // Validate scale.
            if (scale.HasValue && scale.Value <= 0)
            {
                TraceLog($"ZoomToLayerInLayoutAsync error: Invalid scale value: {scale}.");
                return false;
            }

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // Find the named map frame in the layout.
                    if (layout.FindElement(mapFrameName) is not MapFrame mapFrame)
                    {
                        TraceLog($"ZoomToLayerInLayoutAsync error: Map frame '{mapFrameName}' not found in layout '{layout.Name}'.");
                        return false;
                    }

                    Map map = mapFrame.Map;
                    if (map == null)
                    {
                        TraceLog($"ZoomToLayerInLayoutAsync error: Map in map frame '{mapFrameName}' is null.");
                        return false;
                    }

                    // Find the target layer in the map.
                    Layer targetLayer = await FindLayerAsync(layerName, map);
                    if (targetLayer == null)
                    {
                        TraceLog($"ZoomToLayerInLayoutAsync error: Layer '{layerName}' not found in map.");
                        return false;
                    }

                    // Get extent of the layer or selection.
                    Envelope extent;

                    if (selectedOnly)
                        extent = await GetSelectedExtentAsync(targetLayer);
                    else
                        extent = await QueuedTask.Run(() => targetLayer.QueryExtent());

                    if (extent == null || extent.IsEmpty)
                    {
                        TraceLog($"ZoomToLayerInLayoutAsync error: No extent found for layer '{layerName}'.");
                        return false;
                    }

                    // Apply the extent to the map frame.
                    mapFrame.SetCamera(extent);

                    // Apply zoom logic using camera scale strategy.
                    //ApplyZoomToMapFrame(mapFrame, ratio, scale, validScales);

                    return true;
                }
                catch (Exception ex)
                {
                    // Log any unexpected exception and return false.
                    TraceLog($"ZoomToLayerInLayoutAsync error: Problem while zooming to layer '{layerName}' in layout '{layout.Name}', Exception: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion Layout

        #region Map & Layout Helpers

        /// <summary>
        /// Gets the extent of selected features in a feature layer.
        /// </summary>
        /// <param name="layer">The layer to evaluate, must be a FeatureLayer.</param>
        /// <returns>
        /// The extent of the selected features, or null if there are no selections or the layer is not a FeatureLayer.
        /// </returns>
        private async Task<Envelope> GetSelectedExtentAsync(Layer layer)
        {
            // Ensure the layer is a feature layer.
            if (layer is not FeatureLayer featureLayer)
                return null;

            return await QueuedTask.Run(() =>
            {
                // Get the current selection.
                var selection = featureLayer.GetSelection();
                if (selection.GetCount() == 0)
                    return null;

                // Return the extent of selected features.
                return featureLayer.QueryExtent(true);
            });
        }

        //TODO: Remove
        ///// <summary>
        ///// Returns the next scale up (i.e. more zoomed out) from the list of valid scales,
        ///// or extrapolates using the final gap until the value exceeds the current scale.
        ///// </summary>
        ///// <param name="currentScale">The current map scale.</param>
        ///// <param name="scaleList">A list of valid scales in ascending order.</param>
        ///// <returns>The next scale up from the list or extrapolated value.</returns>
        //private double GetNextScaleUp(double currentScale, List<int> scaleList)
        //{
        //    if (scaleList == null || scaleList.Count < 2)
        //        throw new ArgumentException("Scale list must contain at least two values.");

        //    scaleList.Sort();

        //    foreach (var s in scaleList)
        //    {
        //        if (s > currentScale)
        //            return s;
        //    }

        //    // Extrapolate using the final gap until the value exceeds the current scale.
        //    int count = scaleList.Count;
        //    int last = scaleList[count - 1];
        //    int secondLast = scaleList[count - 2];
        //    int gap = last - secondLast;

        //    double extrapolated = last;

        //    while (extrapolated <= currentScale)
        //    {
        //        extrapolated += gap;
        //    }

        //    return extrapolated;
        //}

        /// <summary>
        /// Applies zoom to a map frame based on ratio, fixed scale, or the next available scale from a scale list.
        /// </summary>
        /// <param name="mapFrame">The map frame to update.</param>
        /// <param name="ratio">Optional zoom ratio to apply to the current scale.</param>
        /// <param name="scale">Optional fixed scale to apply.</param>
        /// <param name="validScales">Optional list of allowed scales to use for zooming out.</param>
        private void ApplyZoomToMapFrame(MapFrame mapFrame,
            double? ratio,
            double? scale,
            List<int> validScales = null)
        {
            if (mapFrame == null)
                return;

            try
            {
                Camera camera = mapFrame.Camera;

                if (ratio.HasValue)
                {
                    // Zoom using the next scale up from the scale list if provided.
                    if (validScales != null && validScales.Count >= 2)
                    {
                        double currentScale = camera.Scale;
                        //double nextScale = GetNextScaleUp(currentScale, validScales);
                        //camera.Scale = nextScale;
                        mapFrame.SetCamera(camera);
                    }
                    else
                    {
                        // No scale list — apply ratio directly.
                        camera.Scale *= ratio.Value;
                        mapFrame.SetCamera(camera);
                    }
                }
                else if (scale.HasValue && scale.Value > 0)
                {
                    // No ratio — use fixed scale.
                    camera.Scale = scale.Value;
                    mapFrame.SetCamera(camera);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exception.
                TraceLog($"ApplyZoomToMapFrame error: Exception {ex.Message}");
            }
        }

        #endregion Map & Layout Helpers

        //TODO: Finish improving the code and add more comments.

        #region Layers

        /// <summary>
        /// Find a feature layer by name in the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>FeatureLayer</returns>
        internal async Task<FeatureLayer> FindLayerAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
            {
                TraceLog("FindLayer error: No layer name provided.");
                return null;
            }

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                return await QueuedTask.Run(() =>
                {
                    return mapToUse.FindLayers(layerName, true)
                                   .OfType<FeatureLayer>()
                                   .FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"FindLayer error: Exception {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find the position index for a feature layer by name in the active map.
        /// </summary>
        /// <param name="layerName">The name of the layer to find.</param>
        /// <param name="targetMap">The map to search; if null, the active map is used.</param>
        /// <returns>The index of the layer, or 0 if not found.</returns>
        internal async Task<int> FindLayerIndexAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return 0;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Run on the CIM thread to safely access layer properties and collection.
                return await QueuedTask.Run(() =>
                {
                    // Iterate through all layers in the map.
                    for (int index = 0; index < mapToUse.Layers.Count; index++)
                    {
                        // Get the index of the first feature layer found by name.
                        // Access to Layer.Name must occur on the CIM thread.
                        if (mapToUse.Layers[index].Name == layerName)
                            return index;
                    }

                    // If no layer matched, return 0 as the default.
                    return 0;
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return 0.
                TraceLog($"FindLayerIndexAsync error: Exception {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Remove a layer by name from the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveLayerAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input layer name.
            if (string.IsNullOrEmpty(layerName))
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Find the layer in the active map.
                FeatureLayer layer = await FindLayerAsync(layerName, mapToUse);

                // Remove the layer.
                if (layer != null)
                    return await RemoveLayerAsync(layer, mapToUse);
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"RemoveLayerAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove a layer from the active map.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveLayerAsync(Layer layer, Map targetMap = null)
        {
            // Check there is an input layer.
            if (layer == null)
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Remove the layer.
                    if (layer != null)
                        mapToUse.RemoveLayer(layer);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"RemoveLayerAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add incremental numbers to the label field in a feature class.
        /// </summary>
        /// <param name="outputFeatureClass"></param>
        /// <param name="outputLayerName"></param>
        /// <param name="labelFieldName"></param>
        /// <param name="keyFieldName"></param>
        /// <param name="startNumber"></param>
        /// <returns>int</returns>
        public async Task<int> AddIncrementalNumbersAsync(string outputFeatureClass, string outputLayerName, string labelFieldName, string keyFieldName,
            int startNumber = 1)
        {
            // Check the input parameters.
            if (!await ArcGISFunctions.FeatureClassExistsAsync(outputFeatureClass))
                return -1;

            if (!await FieldExistsAsync(outputLayerName, labelFieldName, null))
                return -1;

            if (!await FieldIsNumericAsync(outputLayerName, labelFieldName, null))
                return -1;

            if (!await FieldExistsAsync(outputLayerName, keyFieldName, null))
                return -1;

            // Get the feature layer.
            FeatureLayer outputFeaturelayer = await FindLayerAsync(outputLayerName, null);
            if (outputFeaturelayer == null)
                return -1;

            int labelMax;
            if (startNumber > 1)
                labelMax = startNumber - 1;
            else
                labelMax = 0;
            int labelVal = labelMax;

            string keyValue;
            string lastKeyValue = "";

            // Create an edit operation.
            EditOperation editOperation = new();

            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the feature class for the output feature layer.
                    using FeatureClass featureClass = outputFeaturelayer.GetFeatureClass();

                    // Get the feature class defintion.
                    using FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();

                    // Get the key field from the feature class definition.
                    using ArcGIS.Core.Data.Field keyField = featureClassDefinition.GetFields()
                      .First(x => x.Name.Equals(keyFieldName, StringComparison.OrdinalIgnoreCase));

                    // Create a SortDescription for the key field.
                    ArcGIS.Core.Data.SortDescription keySortDescription = new(keyField)
                    {
                        CaseSensitivity = CaseSensitivity.Insensitive,
                        SortOrder = ArcGIS.Core.Data.SortOrder.Ascending
                    };

                    // Create a TableSortDescription.
                    TableSortDescription tableSortDescription = new([keySortDescription]);

                    // Create a cursor of the sorted features.
                    using RowCursor rowCursor = featureClass.Sort(tableSortDescription);
                    while (rowCursor.MoveNext())
                    {
                        // Using the current row.
                        using Row record = rowCursor.Current;

                        // Get the key field value.
                        keyValue = record[keyFieldName].ToString();

                        // If the key value is different.
                        if (keyValue != lastKeyValue)
                        {
                            labelMax++;
                            labelVal = labelMax;
                        }

                        editOperation.Modify(record, labelFieldName, labelVal);

                        lastKeyValue = keyValue;
                    }
                });

                // Execute the edit operation.
                if (!editOperation.IsEmpty)
                {
                    if (!await editOperation.ExecuteAsync())
                    {
                        //MessageBox.Show(editOperation.ErrorMessage);
                        return -1;
                    }
                }

                // Check for unsaved edits.
                if (Project.Current.HasEdits)
                {
                    // Save edits.
                    await Project.Current.SaveEditsAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"AddIncrementalNumbersAsync error: Exception {ex.Message}");
                return -1;
            }

            return labelMax;
        }

        /// <summary>
        /// Update the selected features in a feature class.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="siteColumn"></param>
        /// <param name="siteName"></param>
        /// <param name="orgColumn"></param>
        /// <param name="orgName"></param>
        /// <param name="radiusColumn"></param>
        /// <param name="radiusText"></param>
        /// <returns>bool</returns>
        public async Task<bool> UpdateFeaturesAsync(string layerName, string siteColumn, string siteName,
            string orgColumn, string orgName, string radiusColumn, string radiusText, Map targetMap = null)
        {
            // Check the input parameters.
            if (string.IsNullOrEmpty(layerName))
                return false;

            if (string.IsNullOrEmpty(siteColumn) && string.IsNullOrEmpty(orgColumn) && string.IsNullOrEmpty(radiusColumn))
                return false;

            if (!string.IsNullOrEmpty(siteColumn) && !await FieldExistsAsync(layerName, siteColumn, targetMap))
                return false;

            if (!string.IsNullOrEmpty(orgColumn) && !await FieldExistsAsync(layerName, orgColumn, targetMap))
                return false;

            if (!string.IsNullOrEmpty(radiusColumn) && !await FieldExistsAsync(layerName, radiusColumn, targetMap))
                return false;

            // Get the feature layer.
            FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

            if (featureLayer == null)
                return false;

            // Create an edit operation.
            EditOperation editOperation = new();

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Get the oids for the selected features.
                    using Selection gsSelection = featureLayer.GetSelection();
                    IReadOnlyList<long> selectedOIDs = gsSelection.GetObjectIDs();

                    // Update the attributes of the selected features.
                    Inspector insp = new();
                    insp.Load(featureLayer, selectedOIDs);

                    if (!string.IsNullOrEmpty(siteColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(siteColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[siteColumn] = siteName;
                    }

                    if (!string.IsNullOrEmpty(orgColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(orgColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[orgColumn] = orgName;
                    }

                    if (!string.IsNullOrEmpty(radiusColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(radiusColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[radiusColumn] = radiusText;
                    }

                    editOperation.Modify(insp);
                });

                // Execute the edit operation.
                if (!editOperation.IsEmpty)
                {
                    if (!await editOperation.ExecuteAsync())
                    {
                        //MessageBox.Show(editOperation.ErrorMessage);
                        return false;
                    }
                }

                // Check for unsaved edits.
                if (Project.Current.HasEdits)
                {
                    // Save edits.
                    return await Project.Current.SaveEditsAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"UpdateFeaturesAsync error: Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Select features in feature class by location.
        /// </summary>
        /// <param name="targetLayer"></param>
        /// <param name="searchLayer"></param>
        /// <param name="overlapType"></param>
        /// <param name="searchDistance"></param>
        /// <param name="selectionType"></param>
        /// <returns>bool</returns>
        public static async Task<bool> SelectLayerByLocationAsync(string targetLayer, string searchLayer,
            string overlapType = "INTERSECT", string searchDistance = "", string selectionType = "NEW_SELECTION")
        {
            // Check if there is an input target layer name.
            if (string.IsNullOrEmpty(targetLayer))
                return false;

            // Check if there is an input search layer name.
            if (string.IsNullOrEmpty(searchLayer))
                return false;

            // Make a value array of strings to be passed to the tool.
            IReadOnlyList<string> parameters = Geoprocessing.MakeValueArray(targetLayer, overlapType, searchLayer, searchDistance, selectionType);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.SelectLayerByLocation", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.SelectLayerByLocation", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"SelectLayerByLocationAsync error: Exception occurred while selecting features. TargetLayer: {targetLayer}, SearchLayer: {searchLayer}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Select features in feature class by location.
        /// </summary>
        /// <param name="targetLayer"></param>
        /// <param name="searchLayer"></param>
        /// <param name="overlapType"></param>
        /// <param name="searchDistance"></param>
        /// <param name="selectionType"></param>
        /// <returns></returns>
        public static async Task<bool> SelectLayerByLocationAsync(FeatureLayer targetLayer, FeatureLayer searchLayer,
            string overlapType = "INTERSECT", string searchDistance = "", string selectionType = "NEW_SELECTION")
        {
            // Check there is an input feature layer.
            if (targetLayer == null)
                return false;

            // Check there is an input search layer.
            if (searchLayer == null)
                return false;

            return await QueuedTask.Run(() =>
            {
                // Attempt to get the selected ObjectIDs in the search layer.
                var oidSet = searchLayer.GetSelection()?.GetObjectIDs();

                // Use a query filter — either for selected features or all features.
                QueryFilter queryFilter;

                // If any selected features to build the geometry.
                if (oidSet != null && oidSet.Count > 0)
                {
                    // Use only selected features.
                    queryFilter = new QueryFilter
                    {
                        ObjectIDs = oidSet
                    };
                }
                else
                {
                    // No selected features — fallback to using all features.
                    queryFilter = new QueryFilter();
                }

                // Union geometry of the features in the search layer to use as spatial filter.
                Geometry searchGeometry;

                using (var rowCursor = searchLayer.Search(queryFilter))
                {
                    var geometries = new List<Geometry>();

                    while (rowCursor.MoveNext())
                    {
                        using var feature = rowCursor.Current as Feature;
                        if (feature?.GetShape() != null)
                            geometries.Add(feature.GetShape());
                    }

                    if (geometries.Count == 0)
                        return false;

                    searchGeometry = GeometryEngine.Instance.Union(geometries);
                }

                if (searchGeometry == null)
                    return false;

                // Optionally buffer the search geometry if a distance is provided.
                if (!string.IsNullOrEmpty(searchDistance) && double.TryParse(searchDistance, out double distance) && distance > 0)
                {
                    // Use the spatial reference of the search geometry to maintain units.
                    var spatialRef = searchGeometry.SpatialReference;

                    // Buffer assumes units match geometry’s spatial reference (e.g., meters if projected).
                    searchGeometry = GeometryEngine.Instance.Buffer(searchGeometry, distance);

                    if (searchGeometry == null)
                        return false;
                }

                // Map string overlapType to SpatialRelationship.
                SpatialRelationship spatialRel = overlapType.ToUpper() switch
                {
                    "INTERSECT" => SpatialRelationship.Intersects,
                    "CONTAINS" => SpatialRelationship.Contains,
                    "WITHIN" => SpatialRelationship.Within,
                    "CROSSES" => SpatialRelationship.Crosses,
                    "TOUCHES" => SpatialRelationship.Touches,
                    "OVERLAPS" => SpatialRelationship.Overlaps,
                    _ => SpatialRelationship.Intersects
                };

                // Prepare the spatial query.
                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = searchGeometry,
                    SpatialRelationship = spatialRel
                };

                // Determine selection combination method.
                SelectionCombinationMethod method = selectionType.ToUpper() switch
                {
                    "ADD_TO_SELECTION" => SelectionCombinationMethod.Add,
                    "REMOVE_FROM_SELECTION" => SelectionCombinationMethod.Subtract,
                    "SELECT_NEW" or "NEW_SELECTION" => SelectionCombinationMethod.New,
                    "INTERSECT_WITH_SELECTION" => SelectionCombinationMethod.And,
                    _ => SelectionCombinationMethod.New
                };

                // Perform the selection.
                targetLayer.Select(spatialFilter, method);

                return true;
            });
        }

        /// <summary>
        /// Select features in layerName by attributes.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="whereClause"></param>
        /// <param name="selectionMethod"></param>
        /// <returns>bool</returns>
        public async Task<bool> SelectLayerByAttributesAsync(string layerName, string whereClause, SelectionCombinationMethod selectionMethod = SelectionCombinationMethod.New, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

                if (featureLayer == null)
                    return false;

                // Create a query filter using the where clause.
                QueryFilter queryFilter = new()
                {
                    WhereClause = whereClause
                };

                await QueuedTask.Run(() =>
                {
                    // Select the features matching the search clause.
                    featureLayer.Select(queryFilter, selectionMethod);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"SelectLayerByAttributesAsync error: Exception occurred while selecting features. Layer: {layerName}, WhereClause: {whereClause}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear selected features in a feature layer.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> ClearLayerSelectionAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

                if (featureLayer == null)
                    return false;

                await QueuedTask.Run(() =>
                {
                    // Clear the feature selection.
                    featureLayer.ClearSelection();
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"ClearLayerSelectionAsync error: Exception occurred while clearing selection. Layer: {layerName}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Count the number of selected features in a feature layer.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>long</returns>
        public async Task<long> GetSelectedFeatureCountAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return -1;

            long selectedCount;
            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

                if (featureLayer == null)
                    return -1;

                // Select the features matching the search clause.
                selectedCount = await QueuedTask.Run(() => featureLayer.SelectionCount);
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"GetSelectedFeatureCount error: Exception occurred while counting selected features. Layer: {layerName}, Exception: {ex.Message}");
                return -1;
            }

            return selectedCount;
        }

        /// <summary>
        /// Get the list of fields for a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<ArcGIS.Core.Data.Field>> GetFCFieldsAsync(string layerPath, Map targetMap = null)
        {
            // Check there is an input feature layer path.
            if (string.IsNullOrEmpty(layerPath))
                return null;

            try
            {
                // Find the feature layer by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerPath, targetMap);

                if (featureLayer == null)
                    return null;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using Table table = featureLayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();
                    }
                });

                return fields;
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"GetFCFieldsAsync error: Exception occurred while getting fields. Layer: {layerPath}, Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the list of fields for a standalone table.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<ArcGIS.Core.Data.Field>> GetTableFieldsAsync(string layerPath, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerPath))
                return null;

            try
            {
                // Find the table by name if it exists. Only search existing layers.
                StandaloneTable inputTable = FindTable(layerPath, targetMap);

                if (inputTable == null)
                    return null;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                await QueuedTask.Run(() =>
                {
                    // Get the underlying table.
                    using Table table = inputTable.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();
                    }
                });

                return fields;
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"GetTableFieldsAsync error: Exception occurred while getting fields. Layer: {layerPath}, Exception {ex.Message}");
                return null;
            }
        }

        /// Check a string of field names exists in a list of fields.
        public static bool FieldsExist(IReadOnlyList<ArcGIS.Core.Data.Field> fields, string fieldNames, string separator, bool checkStrings = false)
        {
            // Check there is an input field name.
            if (string.IsNullOrEmpty(fieldNames))
                return false;

            // Split the field names into a list.
            string[] fieldNameArray = fieldNames.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (string fieldName in fieldNameArray)
            {
                if (!FieldExists(fields, fieldName.Trim()))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if a field exists in a list of fields.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public static bool FieldExists(IReadOnlyList<ArcGIS.Core.Data.Field> fields, string fieldName)
        {
            bool fldFound = false;

            // Check there is an input field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            foreach (ArcGIS.Core.Data.Field fld in fields)
            {
                if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                    (fld.AliasName != null && fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    fldFound = true;
                    break;
                }
            }

            return fldFound;
        }

        /// <summary>
        /// Check if a field exists in a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<bool> FieldExistsAsync(string layerPath, string fieldName, Map targetMap = null)
        {
            // Check there is an input feature layer path.
            if (string.IsNullOrEmpty(layerPath))
                return false;

            // Check there is an input field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            try
            {
                // Find the feature layer by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerPath, targetMap);

                if (featureLayer == null)
                    return false;

                bool fldFound = false;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using Table table = featureLayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        IReadOnlyList<ArcGIS.Core.Data.Field> fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                (fld.AliasName != null && fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                            {
                                fldFound = true;
                                break;
                            }
                        }
                    }
                });

                return fldFound;
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"FieldExistsAsync error: Exception occurred while checking field existence. Layer: {layerPath}, Field: {fieldName}, Exception {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a list of fields exists in a feature class and
        /// return a list of those that do.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldNames"></param>
        /// <returns>List<string></returns>
        public async Task<List<string>> GetExistingFieldsAsync(string layerName, List<string> fieldNames, Map targetMap = null)
        {
            List<string> fieldsThatExist = [];
            foreach (string fieldName in fieldNames)
            {
                if (await FieldExistsAsync(layerName, fieldName, targetMap))
                    fieldsThatExist.Add(fieldName);
            }

            return fieldsThatExist;
        }

        /// <summary>
        /// Check if a field is numeric in a feature class.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<bool> FieldIsNumericAsync(string layerName, string fieldName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return false;

            // Check there is an input field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

                if (featureLayer == null)
                    return false;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;

                bool fldIsNumeric = false;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using Table table = featureLayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                (fld.AliasName != null && fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                            {
                                fldIsNumeric = fld.FieldType switch
                                {
                                    FieldType.SmallInteger => true,
                                    FieldType.BigInteger => true,
                                    FieldType.Integer => true,
                                    FieldType.Single => true,
                                    FieldType.Double => true,
                                    _ => false,
                                };

                                break;
                            }
                        }
                    }
                });

                return fldIsNumeric;
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"FieldIsNumericAsync error: Exception occurred while checking field type. Layer: {layerName}, Field: {fieldName}, Exception {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculate the total row length for a feature class
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>int</returns>
        public async Task<int> GetFCRowLengthAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return 0;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

                if (featureLayer == null)
                    return 0;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                int rowLength = 1;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using Table table = featureLayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();

                        int fldLength;

                        // Loop through all fields.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.FieldType == FieldType.Integer)
                                fldLength = 10;
                            else if (fld.FieldType == FieldType.Geometry)
                                fldLength = 0;
                            else
                                fldLength = fld.Length;

                            rowLength += fldLength;
                        }
                    }
                });

                return rowLength;
            }
            catch (Exception ex)
            {
                // Log the exception and return 0.
                TraceLog($"GetFCRowLengthAsync error: Exception occurred while getting row length. Layer: {layerName}, Exception {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes all the fields from a feature class that are not required.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldList"></param>
        /// <returns>bool</returns>
        public async Task<bool> KeepSelectedFieldsAsync(string layerName, List<string> fieldList, Map targetMap = null)
        {
            // Check the input parameters.
            if (string.IsNullOrEmpty(layerName))
                return false;

            if (fieldList == null || fieldList.Count == 0)
                return false;

            // Add a FID field so that it isn't tried to be removed.
            //fieldList.Add("FID");

            // Get the list of fields for the input table.
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields = await GetFCFieldsAsync(layerName, targetMap);

            // Check a list of fields is returned.
            if (inputfields == null || inputfields.Count == 0)
                return false;

            // Get the list of field names for the input table that
            // aren't required fields (e.g. excluding FID and Shape).
            List<string> inputFieldNames = inputfields.Where(x => !x.IsRequired).Select(y => y.Name).ToList();

            // Get the list of fields that do exist in the layer.
            List<string> existingFields = await GetExistingFieldsAsync(layerName, fieldList, targetMap);

            // Get the list of layer fields that aren't in the field list.
            var remainingFields = inputFieldNames.Except(existingFields).ToList();

            if (remainingFields == null || remainingFields.Count == 0)
                return true;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(layerName, remainingFields);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.DeleteField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.DeleteField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"KeepSelectedFieldsAsync error: Exception occurred while deleting fields. Layer: {layerName}, Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the full layer path name for a layer in the map (i.e.
        /// to include any parent group names).
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>string</returns>
        public Task<string> GetLayerPathAsync(Layer layer)
        {
            return QueuedTask.Run(async () =>
            {
                // Check there is an input layer.
                if (layer == null)
                    return null;

                string layerPath = "";

                try
                {
                    // Get the parent for the layer.
                    ILayerContainer layerParent = layer.Parent;

                    // Loop while the parent is a group layer.
                    while (layerParent is GroupLayer)
                    {
                        // Get the parent layer.
                        Layer groupLayer = (Layer)layerParent;

                        // Append the parent name to the full layer path.
                        // Access to groupLayer.Name must occur on the CIM thread.
                        layerPath = groupLayer.Name + "/" + layerPath;

                        // Get the parent for the layer.
                        layerParent = groupLayer.Parent;
                    }

                    // Append the layer name to its full path.
                    // Access to Layer.Name must occur on the CIM thread.
                    layerPath += layer.Name;
                }
                catch (Exception ex)
                {
                    // Access to Layer.Name must occur on the CIM thread.
                    string safeLayerName = await QueuedTask.Run(() => layer.Name);
                    TraceLog($"GetLayerPathAsync error: Exception occurred while getting layer path. Layer: {safeLayerName}, Exception: {ex.Message}");
                    return null;
                }

                return layerPath;
            });
        }

        /// <summary>
        /// Get the full layer path name for a layer name in the map (i.e.
        /// to include any parent group names.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>string</returns>
        public async Task<string> GetLayerPathAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input layer name.
            if (string.IsNullOrEmpty(layerName))
                return null;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                FeatureLayer layer = await FindLayerAsync(layerName, mapToUse);
                if (layer == null)
                    return null;

                return await GetLayerPathAsync(layer);
            }
            catch (Exception ex)
            {
                TraceLog($"GetLayerPathAsync error: Exception occurred while getting layer path. Layer: {layerName}, Exception {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// This method is called to use the current active mapw and retrieve all
        /// feature layers that are part of the map layers in the current map view.
        /// </summary>
        public List<FeatureLayer> GetFeatureLayers()
        {
            if (_activeMap == null) return null;

            //Get the feature layers in the active map view.
            List<FeatureLayer> featureLayerList = _activeMap.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

            //List<FeatureLayer> layers = [];
            //foreach (var featureLayer in featureLayers) layers.Add(featureLayer);

            return featureLayerList;
        }

        /// <summary>
        /// Returns a simplified feature class shape type for a feature layer.
        /// </summary>
        /// <param name="featureLayer"></param>
        /// <returns>string: point, line, polygon</returns>
        public async Task<string> GetFeatureClassTypeAsync(FeatureLayer featureLayer)
        {
            // Check there is an input feature layer.
            if (featureLayer == null)
                return null;

            try
            {
                esriGeometryType shapeType = await QueuedTask.Run(() => featureLayer.ShapeType);

                return shapeType switch
                {
                    esriGeometryType.esriGeometryPoint => "point",
                    esriGeometryType.esriGeometryMultipoint => "point",
                    esriGeometryType.esriGeometryPolygon => "polygon",
                    esriGeometryType.esriGeometryRing => "polygon",
                    esriGeometryType.esriGeometryLine => "line",
                    esriGeometryType.esriGeometryPolyline => "line",
                    esriGeometryType.esriGeometryCircularArc => "line",
                    esriGeometryType.esriGeometryEllipticArc => "line",
                    esriGeometryType.esriGeometryBezier3Curve => "line",
                    esriGeometryType.esriGeometryPath => "line",
                    _ => "other",
                };
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                // Access to Layer.Name must occur on the CIM thread.
                string safeLayerName = await QueuedTask.Run(() => featureLayer.Name);
                TraceLog($"GetFeatureClassTypeAsync error: Exception occurred while getting shape type. Layer: {safeLayerName}, Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns a simplified feature class shape type for a layer name.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>string: point, line, polygon</returns>
        public async Task<string> GetFeatureClassTypeAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input feature layer name.
            if (string.IsNullOrEmpty(layerName))
                return null;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Find the layer in the active map.
                FeatureLayer layer = await FindLayerAsync(layerName, mapToUse);

                if (layer == null)
                    return null;

                return await GetFeatureClassTypeAsync(layer);
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"GetFeatureClassType error: Exception occurred while getting feature class type. Layer: {layerName}, Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Recursively retrieves all feature layers from a collection of layers,
        /// including those nested within group layers.
        /// </summary>
        /// <param name="layers">The layer collection to search.</param>
        /// <returns>All FeatureLayer instances found within the collection.</returns>
        private static IEnumerable<FeatureLayer> GetAllFeatureLayers(IEnumerable<Layer> layers)
        {
            // Loop through each layer in the collection.
            foreach (var layer in layers)
            {
                // If it's a FeatureLayer, return it.
                if (layer is FeatureLayer fl)
                {
                    yield return fl;
                }
                // If it's a GroupLayer, search its children recursively.
                else if (layer is GroupLayer gl)
                {
                    foreach (var child in GetAllFeatureLayers(gl.Layers))
                        yield return child;
                }
            }
        }

        #endregion Layers

        #region Group Layers

        /// <summary>
        /// Finds a group layer by name in the specified or active map.
        /// </summary>
        /// <param name="layerName">The name of the group layer to find.</param>
        /// <param name="targetMap">Optional map to search in; defaults to the active map.</param>
        /// <returns>GroupLayer if found; otherwise, null.</returns>
        internal async Task<GroupLayer> FindGroupLayerAsync(string layerName, Map targetMap = null)
        {
            // Check there is an input group layer name.
            if (string.IsNullOrEmpty(layerName))
            {
                TraceLog("FindGroupLayerAsync error: No layer name provided.");
                return null;
            }

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Run layer lookup on the QueuedTask to comply with ArcGIS Pro threading model.
                return await QueuedTask.Run(() =>
                {
                    return mapToUse.FindLayers(layerName, true)
                                   .OfType<GroupLayer>()
                                   .FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"FindGroupLayerAsync error: Exception occurred while finding group layer. Layer: {layerName}, Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Move a layer into a group layer (creating the group layer if
        /// it doesn't already exist).
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="groupLayerName"></param>
        /// <param name="position"></param>
        /// <returns>bool</returns>
        public async Task<bool> MoveToGroupLayerAsync(Layer layer, string groupLayerName, int position = -1, Map targetMap = null)
        {
            // Check if there is an input layer.
            if (layer == null)
                return false;

            // Check there is an input group layer name.
            if (string.IsNullOrEmpty(groupLayerName))
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            // Does the group layer exist?
            GroupLayer groupLayer = await FindGroupLayerAsync(groupLayerName, mapToUse);
            if (groupLayer == null)
            {
                // Add the group layer to the map.
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        groupLayer = LayerFactory.Instance.CreateGroupLayer(mapToUse, 0, groupLayerName);
                    });
                }
                catch (Exception ex)
                {
                    // Log the exception and return false.
                    string safeLayerName = await QueuedTask.Run(() => layer.Name);
                    TraceLog($"MoveToGroupLayerAsync error: Exception occurred while creating group layer. Layer: {safeLayerName}, GroupLayer: {groupLayerName}, Exception: {ex.Message}");
                    return false;
                }
            }

            // Move the layer into the group.
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Move the layer into the group.
                    mapToUse.MoveLayer(layer, groupLayer, position);

                    // Expand the group.
                    groupLayer.SetExpanded(true);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                string safeLayerName = await QueuedTask.Run(() => layer.Name);
                TraceLog($"MoveToGroupLayerAsync error: Exception occurred while moving layer to group layer. Layer: {safeLayerName}, GroupLayer: {groupLayerName}, Exception {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove a group layer if it is empty.
        /// </summary>
        /// <param name="groupLayerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveGroupLayerAsync(string groupLayerName, Map targetMap = null)
        {
            // Check there is an input group layer name.
            if (string.IsNullOrEmpty(groupLayerName))
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Does the group layer exist?
                GroupLayer groupLayer = await FindGroupLayerAsync(groupLayerName, mapToUse);
                if (groupLayer == null)
                    return false;

                // Count the layers in the group.
                if (groupLayer.Layers.Count != 0)
                    return true;

                await QueuedTask.Run(() =>
                {
                    // Remove the group layer.
                    mapToUse.RemoveLayer(groupLayer);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"RemoveGroupLayerAsync error: Exception occurred while removing group layer. GroupLayer: {groupLayerName}, Exception {ex.Message}");
                return false;
            }

            return true;
        }

        #endregion Group Layers

        #region Tables

        /// <summary>
        /// Find a table by name in the active map.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>StandaloneTable</returns>
        internal StandaloneTable FindTable(string tableName, Map targetMap = null)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(tableName))
                return null;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Finds tables by name and returns a read only list of standalone tables.
                IEnumerable<StandaloneTable> tables = mapToUse.FindStandaloneTables(tableName).OfType<StandaloneTable>();

                while (tables.Any())
                {
                    // Get the first table found by name.
                    StandaloneTable table = tables.First();

                    // Check the table is in the active map.
                    if (table.Map.Name.Equals(mapToUse.Name, StringComparison.OrdinalIgnoreCase))
                        return table;
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return null.
                TraceLog($"FindTable error: Exception occurred while finding table. Table: {tableName}, Exception: {ex.Message}");
                return null;
            }

            return null;
        }

        /// <summary>
        /// Remove a table from the active map.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveTableAsync(string tableName, Map targetMap = null)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(tableName))
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                // Find the table in the active map.
                StandaloneTable table = FindTable(tableName, mapToUse);

                if (table != null)
                {
                    // Remove the table.
                    await RemoveTableAsync(table, mapToUse);
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"RemoveTableAsync error: Exception occurred while removing table. Table: {tableName}, Exception {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a standalone table from the active map.
        /// </summary>
        /// <param name="table"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveTableAsync(StandaloneTable table, Map targetMap = null)
        {
            // Check there is an input table name.
            if (table == null)
                return false;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Remove the table.
                    mapToUse.RemoveStandaloneTable(table);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                string safeTableName = await QueuedTask.Run(() => table.Name);
                TraceLog($"RemoveTableAsync error: Exception occurred while removing table. Table: {safeTableName}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        #endregion Tables

        #region Symbology

        /// <summary>
        /// Apply symbology to a layer by name using a lyrx file.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="layerFile"></param>
        /// <returns>bool</returns>
        public async Task<string> ApplySymbologyFromLayerFileAsync(string layerName, string layerFile, Map targetMap = null)
        {
            // Check there is an input layer name.
            if (string.IsNullOrEmpty(layerName))
                return null;

            // Check the lyrx file exists.
            if (!FileFunctions.FileExists(layerFile))
                return null;

            string nameFromLyrx = null;

            // Use provided map or default to _activeMap.
            Map mapToUse = targetMap ?? _activeMap;

            // Find the layer in the active map.
            FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

            if (featureLayer != null)
            {
                // Apply the layer file symbology to the feature layer.
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        // Get the layer document from the lyrx file.
                        LayerDocument lyrxLayerDocument = new(layerFile);

                        // Get the CIM layer document from the lyrx layer document.
                        CIMLayerDocument lyrxCIMLyrDoc = lyrxLayerDocument.GetCIMLayerDocument();

                        // Get the layer definition from the CIM layer document.
                        CIMFeatureLayer lyrxLayerDefn = (CIMFeatureLayer)lyrxCIMLyrDoc.LayerDefinitions[0];

                        // Set the name of the layer in the map to match the name from the lyrx file.
                        nameFromLyrx = lyrxLayerDefn.Name;
                        if (!string.IsNullOrEmpty(nameFromLyrx))
                            featureLayer.SetName(nameFromLyrx);

                        // Get the renderer from the layer definition.
                        //CIMSimpleRenderer rendererFromLayerFile = ((CIMFeatureLayer)cimLyrDoc.LayerDefinitions[0]).Renderer as CIMSimpleRenderer;
                        CIMRenderer lryxRenderer = lyrxLayerDefn.Renderer;

                        // Apply the renderer to the feature layer.
                        if (featureLayer.CanSetRenderer(lryxRenderer))
                            featureLayer.SetRenderer(lryxRenderer);

                        //Get the label classes from the lyrx layer definition - we need the first one.
                        List<CIMLabelClass> lryxLabelClassesList = [.. lyrxLayerDefn.LabelClasses];
                        CIMLabelClass lyrxLabelClass = lryxLabelClassesList.FirstOrDefault();

                        // Get the input layer definition.
                        CIMFeatureLayer lyrDefn = featureLayer.GetDefinition() as CIMFeatureLayer;

                        // Get the label classes from the input layer definition - we need the first one.
                        List<CIMLabelClass> labelClassesList = [.. lyrDefn.LabelClasses];
                        CIMLabelClass labelClass = labelClassesList.FirstOrDefault();

                        // Copy the lyrx label class to the input layer class.
                        labelClass.CopyFrom(lyrxLabelClass);

                        // Set the label definition back to the input feeature layer.
                        featureLayer.SetDefinition(lyrDefn);

                        // Get the lyrx label visibility.
                        bool lyrxLabelVisible = lyrxLabelClass.Visibility;

                        // Set the label visibilty.
                        featureLayer.SetLabelVisibility(lyrxLabelVisible);
                    });
                }
                catch (Exception ex)
                {
                    // Log the exception and return false.
                    TraceLog($"ApplySymbologyFromLayerFileAsync error: Exception occurred while applying symbology. Layer: {layerName}, LayerFile: {layerFile}, Exception: {ex.Message}");
                    return null;
                }
            }

            // Return the name of the layer from the lyrx file.
            return nameFromLyrx;
        }

        /// <summary>
        /// Apply a label style to a label column of a layer by name.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="labelColumn"></param>
        /// <param name="labelFont"></param>
        /// <param name="labelSize"></param>
        /// <param name="labelStyle"></param>
        /// <param name="labelRed"></param>
        /// <param name="labelGreen"></param>
        /// <param name="labelBlue"></param>
        /// <param name="allowOverlap"></param>
        /// <param name="displayLabels"></param>
        /// <returns>bool</returns>
        public async Task<bool> LabelLayerAsync(string layerName, string labelColumn, string labelFont = "Arial", double labelSize = 10, string labelStyle = "Normal",
                            int labelRed = 0, int labelGreen = 0, int labelBlue = 0, bool allowOverlap = true, bool displayLabels = true, Map targetMap = null)
        {
            // Check there is an input layer.
            if (string.IsNullOrEmpty(layerName))
                return false;

            // Check there is a label columns to set.
            if (string.IsNullOrEmpty(labelColumn))
                return false;

            // Get the input feature layer.
            FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

            if (featureLayer == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    CIMColor textColor = ColorFactory.Instance.CreateRGBColor(labelRed, labelGreen, labelBlue);

                    CIMTextSymbol textSymbol = SymbolFactory.Instance.ConstructTextSymbol(textColor, labelSize, labelFont, labelStyle);

                    // Get the layer definition.
                    CIMFeatureLayer lyrDefn = featureLayer.GetDefinition() as CIMFeatureLayer;

                    // Get the label classes - we need the first one.
                    var listLabelClasses = lyrDefn.LabelClasses.ToList();
                    var labelClass = listLabelClasses.FirstOrDefault();

                    // Set the label text symbol.
                    labelClass.TextSymbol.Symbol = textSymbol;

                    // Set the label expression.
                    labelClass.Expression = "$feature." + labelColumn;

                    // Check if the label engine is Maplex or standard.
                    CIMGeneralPlacementProperties labelEngine =
                       MapView.Active.Map.GetDefinition().GeneralPlacementProperties;

                    // Modify label placement (if standard label engine).
                    if (labelEngine is CIMStandardGeneralPlacementProperties) //Current labeling engine is Standard labeling engine
                        labelClass.StandardLabelPlacementProperties.AllowOverlappingLabels = allowOverlap;

                    // Set the label definition back to the layer.
                    featureLayer.SetDefinition(lyrDefn);

                    // Set the label visibilty.
                    featureLayer.SetLabelVisibility(displayLabels);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"LabelLayerAsync error: Exception occurred while labeling layer. Layer: {layerName}, LabelColumn: {labelColumn}, Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Switch if a layers labels are visible or not.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="displayLabels"></param>
        /// <returns>bool</returns>
        public async Task<bool> SwitchLabelsAsync(string layerName, bool displayLabels, Map targetMap = null)
        {
            // Check there is an input layer.
            if (string.IsNullOrEmpty(layerName))
                return false;

            // Get the input feature layer.
            FeatureLayer featureLayer = await FindLayerAsync(layerName, targetMap);

            if (featureLayer == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Set the label visibilty.
                    featureLayer.SetLabelVisibility(displayLabels);
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return false.
                TraceLog($"SwitchLabelsAsync error: Exception occurred while switching labels. Layer: {layerName}, DisplayLabels: {displayLabels}, Exception {ex.Message}");
                return false;
            }

            return true;
        }

        #endregion Symbology

        #region Export

        /// <summary>
        /// Copy a feature class to a text fiile.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="columns"></param>
        /// <param name="orderByColumns"></param>
        /// <param name="separator"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyFCToTextFileAsync(string inputLayer, string outFile, string columns, string orderByColumns,
             string separator, bool append = false, bool includeHeader = true, Map targetMap = null)
        {
            // Check there is an input layer name.
            if (string.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (string.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (string.IsNullOrEmpty(columns))
                return -1;

            bool missingColumns = false;
            string outColumns;
            FeatureLayer inputFeaturelayer;
            List<string> outColumnsList = [];
            List<string> orderByColumnsList = [];
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields;

            try
            {
                // Get the input feature layer.
                inputFeaturelayer = await FindLayerAsync(inputLayer, targetMap);

                if (inputFeaturelayer == null)
                    return -1;

                // Get the list of fields for the input table.
                inputfields = await GetFCFieldsAsync(inputLayer, targetMap);

                // Check a list of fields is returned.
                if (inputfields == null || inputfields.Count == 0)
                    return -1;

                // Align the columns with what actually exists in the layer.
                List<string> columnsList = [.. columns.Split(',')];
                outColumns = "";
                foreach (string column in columnsList)
                {
                    string columnName = column.Trim();
                    if ((columnName.Substring(0, 1) == "\"") || (FieldExists(inputfields, columnName)))
                    {
                        outColumnsList.Add(columnName);
                        outColumns = outColumns + columnName + separator;
                    }
                    else
                    {
                        missingColumns = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyFCToTextFileAsync error: Exception occurred while copying feature class to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception: {ex.Message}");
                return -1;
            }

            // Stop if there aren't any columns.
            if (outColumnsList.Count == 0 || string.IsNullOrEmpty(outColumns))
                return -1;

            // Stop if there are any missing columns.
            if (missingColumns || string.IsNullOrEmpty(columns))
                return -1;

            // Remove the final separator.
            outColumns = outColumns[..^1];

            // Open output file.
            using StreamWriter txtFile = new(outFile, append);

            // Write the header if required.
            if (!append && includeHeader)
                txtFile.WriteLine(outColumns);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the feature class for the input feature layer.
                    using FeatureClass featureClass = inputFeaturelayer.GetFeatureClass();

                    // Get the feature class defintion.
                    using FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();

                    // Create a row cursor.
                    RowCursor rowCursor;

                    // Create a new list of sort descriptions.
                    List<ArcGIS.Core.Data.SortDescription> sortDescriptions = [];

                    if (!string.IsNullOrEmpty(orderByColumns))
                    {
                        orderByColumnsList = [.. orderByColumns.Split(',')];

                        // Build the list of sort descriptions for each orderby column in the input layer.
                        foreach (string column in orderByColumnsList)
                        {
                            // Get the column name (ignoring any trailing ASC/DESC sort order).
                            string columnName = column.Trim();
                            if (columnName.Contains(' '))
                                columnName = columnName.Split(" ")[0].Trim();

                            // Set the sort order to ascending or descending.
                            ArcGIS.Core.Data.SortOrder sortOrder = ArcGIS.Core.Data.SortOrder.Ascending;
                            if ((column.EndsWith(" DES", true, System.Globalization.CultureInfo.CurrentCulture)) ||
                               (column.EndsWith(" DESC", true, System.Globalization.CultureInfo.CurrentCulture)))
                                sortOrder = ArcGIS.Core.Data.SortOrder.Descending;

                            // If the column is in the input table use it for sorting.
                            if ((columnName.Substring(0, 1) != "\"") && (FieldExists(inputfields, columnName)))
                            {
                                // Get the field from the feature class definition.
                                using ArcGIS.Core.Data.Field field = featureClassDefinition.GetFields()
                                  .First(x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                                // Create a SortDescription for the field.
                                ArcGIS.Core.Data.SortDescription sortDescription = new(field)
                                {
                                    CaseSensitivity = CaseSensitivity.Insensitive,
                                    SortOrder = sortOrder
                                };

                                // Add the SortDescription to the list.
                                sortDescriptions.Add(sortDescription);
                            }
                        }

                        // Create a TableSortDescription.
                        TableSortDescription tableSortDescription = new(sortDescriptions);

                        // Create a cursor of the sorted features.
                        rowCursor = featureClass.Sort(tableSortDescription);
                    }
                    else
                    {
                        // Create a cursor of the features.
                        rowCursor = featureClass.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row record = rowCursor.Current;

                        string newRow = "";
                        foreach (string column in outColumnsList)
                        {
                            string columnName = column.Trim();

                            // If the column name isn't a literal.
                            if (columnName.Substring(0, 1) != "\"")
                            {
                                // Get the field value.
                                var columnValue = record[columnName];
                                columnValue ??= "";

                                // Wrap value if quotes if it is a string that contains a comma
                                if ((columnValue is string) && (columnValue.ToString().Contains(',')))
                                    columnValue = "\"" + columnValue.ToString() + "\"";

                                // Append the column value to the new row.
                                newRow = newRow + columnValue.ToString() + separator;
                            }
                            else
                            {
                                // Append the literal to the new row.
                                newRow = newRow + columnName + separator;
                            }
                        }

                        // Remove the final separator.
                        newRow = newRow[..^1];

                        // Write the new row.
                        txtFile.WriteLine(newRow);
                        intLineCount++;
                    }

                    // Dispose of the objects.
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyFCToTextFileAsync error: Exception occurred while copying feature class to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception: {ex.Message}");
                return -1;
            }
            finally
            {
                // Close the file.
                txtFile.Close();

                // Dispose of the object.
                txtFile.Dispose();
            }

            return intLineCount;
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="columns"></param>
        /// <param name="orderByColumns"></param>
        /// <param name="separator"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyTableToTextFileAsync(string inputLayer, string outFile, string columns, string orderByColumns,
            string separator, bool append = false, bool includeHeader = true, Map targetMap = null)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (string.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (string.IsNullOrEmpty(columns))
                return -1;

            bool missingColumns = false;
            string outColumns;
            StandaloneTable inputTable;
            List<string> outColumnsList = [];
            List<string> orderByColumnsList = [];
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields;

            try
            {
                // Get the input feature layer.
                inputTable = FindTable(inputLayer, targetMap);

                if (inputTable == null)
                    return -1;

                // Get the list of fields for the input table.
                inputfields = await GetTableFieldsAsync(inputLayer, targetMap);

                // Check a list of fields is returned.
                if (inputfields == null || inputfields.Count == 0)
                    return -1;

                // Align the columns with what actually exists in the layer.
                List<string> columnsList = [.. columns.Split(',')];
                outColumns = "";
                foreach (string column in columnsList)
                {
                    string columnName = column.Trim();
                    if ((columnName.Substring(0, 1) == "\"") || (FieldExists(inputfields, columnName)))
                    {
                        outColumnsList.Add(columnName);
                        outColumns = outColumns + columnName + separator;
                    }
                    else
                    {
                        missingColumns = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyTableToTextFileAsync error: Exception occurred while copying table to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception: {ex.Message}");
                return -1;
            }

            // Stop if there aren't any columns.
            if (outColumnsList.Count == 0 || string.IsNullOrEmpty(outColumns))
                return -1;

            // Stop if there are any missing columns.
            if (missingColumns || string.IsNullOrEmpty(columns))
                return -1;

            // Remove the final separator.
            outColumns = outColumns[..^1];

            // Open output file.
            using StreamWriter txtFile = new(outFile, append);

            // Write the header if required.
            if (!append && includeHeader)
                txtFile.WriteLine(outColumns);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the underlying table for the input layer.
                    using Table table = inputTable.GetTable();

                    // Get the table defintion.
                    using TableDefinition tableDefinition = table.GetDefinition();

                    // Create a row cursor.
                    RowCursor rowCursor;

                    // Create a new list of sort descriptions.
                    List<ArcGIS.Core.Data.SortDescription> sortDescriptions = [];

                    if (!string.IsNullOrEmpty(orderByColumns))
                    {
                        orderByColumnsList = [.. orderByColumns.Split(',')];

                        // Build the list of sort descriptions for each orderby column in the input layer.
                        foreach (string column in orderByColumnsList)
                        {
                            // Get the column name (ignoring any trailing ASC/DESC sort order).
                            string columnName = column.Trim();
                            if (columnName.Contains(' '))
                                columnName = columnName.Split(" ")[0].Trim();

                            // Set the sort order to ascending or descending.
                            ArcGIS.Core.Data.SortOrder sortOrder = ArcGIS.Core.Data.SortOrder.Ascending;
                            if ((column.EndsWith(" DES", true, System.Globalization.CultureInfo.CurrentCulture)) ||
                               (column.EndsWith(" DESC", true, System.Globalization.CultureInfo.CurrentCulture)))
                                sortOrder = ArcGIS.Core.Data.SortOrder.Descending;

                            // If the column is in the input table use it for sorting.
                            if ((columnName.Substring(0, 1) != "\"") && (FieldExists(inputfields, columnName)))
                            {
                                // Get the field from the feature class definition.
                                using ArcGIS.Core.Data.Field field = tableDefinition.GetFields()
                                  .First(x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                                // Create a SortDescription for the field.
                                ArcGIS.Core.Data.SortDescription sortDescription = new(field)
                                {
                                    CaseSensitivity = CaseSensitivity.Insensitive,
                                    SortOrder = sortOrder
                                };

                                // Add the SortDescription to the list.
                                sortDescriptions.Add(sortDescription);
                            }
                        }

                        // Create a TableSortDescription.
                        TableSortDescription tableSortDescription = new(sortDescriptions);

                        // Create a cursor of the sorted features.
                        rowCursor = table.Sort(tableSortDescription);
                    }
                    else
                    {
                        // Create a cursor of the features.
                        rowCursor = table.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row record = rowCursor.Current;

                        string newRow = "";
                        foreach (string column in outColumnsList)
                        {
                            string columnName = column.Trim();

                            // If the column name isn't a literal.
                            if (columnName.Substring(0, 1) != "\"")
                            {
                                // Get the field value.
                                var columnValue = record[columnName];
                                columnValue ??= "";

                                // Wrap value if quotes if it is a string that contains a comma
                                if ((columnValue is string) && (columnValue.ToString().Contains(',')))
                                    columnValue = "\"" + columnValue.ToString() + "\"";

                                // Append the column value to the new row.
                                newRow = newRow + columnValue.ToString() + separator;
                            }
                            else
                            {
                                // Append the literal to the new row.
                                newRow = newRow + columnName + separator;
                            }
                        }

                        // Remove the final separator.
                        newRow = newRow[..^1];

                        // Write the new row.
                        txtFile.WriteLine(newRow);
                        intLineCount++;
                    }

                    // Dispose of the objects.
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyTableToTextFileAsync error: Exception occurred while copying table to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception {ex.Message}");
                return -1;
            }
            finally
            {
                // Close the file.
                txtFile.Close();

                // Dispose of the object.
                txtFile.Dispose();
            }

            return intLineCount;
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToCSVAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (string.IsNullOrEmpty(outFile))
                return -1;

            string separator = ",";
            return await CopyToTextFileAsync(inTable, outFile, separator, isSpatial, append);
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToTabAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (string.IsNullOrEmpty(outFile))
                return -1;

            string separator = "\t";
            return await CopyToTextFileAsync(inTable, outFile, separator, isSpatial, append);
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="separator"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToTextFileAsync(string inputLayer, string outFile, string separator, bool isSpatial, bool append = false,
            bool includeHeader = true, Map targetMap = null)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output file.
            if (string.IsNullOrEmpty(outFile))
                return -1;

            string fieldName = null;
            string header = "";
            int ignoreField = -1;

            int intFieldCount;
            try
            {
                IReadOnlyList<ArcGIS.Core.Data.Field> fields;

                if (isSpatial)
                {
                    // Get the list of fields for the input table.
                    fields = await GetFCFieldsAsync(inputLayer, targetMap);
                }
                else
                {
                    // Get the list of fields for the input table.
                    fields = await GetTableFieldsAsync(inputLayer, targetMap);
                }

                // Check a list of fields is returned.
                if (fields == null || fields.Count == 0)
                    return -1;

                intFieldCount = fields.Count;

                // Iterate through the fields in the collection to create header
                // and flag which fields to ignore.
                for (int i = 0; i < intFieldCount; i++)
                {
                    // Get the fieldName name.
                    fieldName = fields[i].Name;

                    using ArcGIS.Core.Data.Field field = fields[i];

                    // Get the fieldName type.
                    FieldType fieldType = field.FieldType;

                    string fieldTypeName = fieldType.ToString();

                    if (fieldName.Equals("sp_geometry", StringComparison.OrdinalIgnoreCase) || fieldName.Equals("shape", StringComparison.OrdinalIgnoreCase))
                        ignoreField = i;
                    else
                        header = header + fieldName + separator;
                }

                if (!append && includeHeader)
                {
                    // Remove the final separator from the header.
                    header = header.Substring(0, header.Length - 1);

                    // Write the header to the output file.
                    FileFunctions.WriteEmptyTextFile(outFile, header);
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyToTextFileAsync error: Exception occurred while copying table to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception: {ex.Message}");
                return -1;
            }

            // Open output file.
            StreamWriter txtFile = new(outFile, append);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(async () =>
                {
                    // Create a row cursor.
                    RowCursor rowCursor;

                    if (isSpatial)
                    {
                        FeatureLayer inputFC;

                        // Get the input feature layer.
                        inputFC = await FindLayerAsync(inputLayer, targetMap);

                        /// Get the underlying table for the input layer.
                        using FeatureClass featureClass = inputFC.GetFeatureClass();

                        // Create a cursor of the features.
                        rowCursor = featureClass.Search();
                    }
                    else
                    {
                        StandaloneTable inputTable;

                        // Get the input table.
                        inputTable = FindTable(inputLayer, targetMap);

                        /// Get the underlying table for the input layer.
                        using Table table = inputTable.GetTable();

                        // Create a cursor of the features.
                        rowCursor = table.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row row = rowCursor.Current;

                        // Loop through the fields.
                        string rowStr = "";
                        for (int i = 0; i < intFieldCount; i++)
                        {
                            // String the column values together (if they are not to be ignored).
                            if (i != ignoreField)
                            {
                                // Get the column value.
                                var colValue = row.GetOriginalValue(i);

                                // Wrap the value if quotes if it is a string that contains a comma
                                string colStr = null;
                                if (colValue != null)
                                {
                                    if ((colValue is string) && (colValue.ToString().Contains(',')))
                                        colStr = "\"" + colValue.ToString() + "\"";
                                    else
                                        colStr = colValue.ToString();
                                }

                                // Add the column string to the row string.
                                rowStr += colStr;

                                // Add the column separator (if not the last column).
                                if (i < intFieldCount - 1)
                                    rowStr += separator;
                            }
                        }

                        // Write the row string to the output file.
                        txtFile.WriteLine(rowStr);
                        intLineCount++;
                    }
                    // Dispose of the objects.
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return -1.
                TraceLog($"CopyToTextFileAsync error: Exception occurred while copying table to text file. Layer: {inputLayer}, OutFile: {outFile}, Exception: {ex.Message}");
                return -1;
            }
            finally
            {
                // Close the output file and dispose of the object.
                txtFile.Close();
                txtFile.Dispose();
            }

            return intLineCount;
        }

        #endregion Export

        #region HLULayers

        public async Task<bool> IsHluLayerAsync(string layerName, bool activate)
        {
            // Check there is an input GIS featureLayer.
            if (layerName == null)
                return false;

            // Get the feature featureLayer for the new GIS featureLayer.
            FeatureLayer featurelayer = await FindLayerAsync(layerName);

            // Check if the feature layer a valid HLU layer.
            return await IsHluLayerAsync(featurelayer, activate);
        }

        /// <summary>
        /// Determines whether the specified new gis featureLayer is a valid HLU featureLayer
        /// and if it is sets the current featureLayer (_hluLayer, etc) properies
        /// to relate this the new GIS featureLayer.
        /// </summary>
        /// <param name="newGISLayer">The new gis featureLayer to test for validity.</param>
        /// <returns>True if the GIS featureLayer is a valid HLU featureLayer, otherwise False</returns>
        public bool IsHluLayer(FeatureLayer newGISLayer)
        {
            //TODO: ArcGIS
            //try
            //{
            //    // Get the correct map based on the map number.
            //    IMap map = Maps(_arcMap).get_Item(mapNum);
            //    _hluView = map as IActiveView;

            //    UID uid = new();
            //    uid.Value = typeof(IFeatureLayer).GUID.ToString("B");

            //    // Loop through each featureLayer in the map looking for the correct featureLayer
            //    // by number (order).
            //    int j = 0;
            //    IEnumLayer layers = map.get_Layers(uid, true);
            //    ILayer featureLayer = layers.Next();
            //    while (featureLayer != null)
            //    {
            //        if (j == layerNum)
            //        {
            //            string layerName = featureLayer.Name;
            //            _templateLayer = (IGeoFeatureLayer)featureLayer;
            //            CreateHluLayer(false, _templateLayer);
            //            CreateFieldMap(5, 3, 1, retList);
            //            _hluCurrentLayer = layerName;
            //            return true;
            //        }
            //        featureLayer = layers.Next();
            //        j++;
            //    }
            //    else
            //    {
            //        _hluLayer = null;
            //    }
            //}
            //catch { }

            //if (_hluLayer != null)
            //{
            //    return true;
            //}
            //else
            //{
            //    DestroyHluLayer();
            //    return false;
            //}
            return false;
        }

        /// <summary>
        /// Is the featureLayer a valid HLU featureLayer.
        /// </summary>
        /// <param name="featureLayer"></param>
        /// <param name="activate"></param>
        /// <returns></returns>
        public async Task<bool> IsHluLayerAsync(FeatureLayer featureLayer, bool activate)
        {
            int[] hluFieldMap = [];
            string[] hluFieldNames = [];

            bool isHlu = false;

            try
            {
                await QueuedTask.Run(async () =>
                {
                    // Check the feature featureLayer is valid.
                    if ((featureLayer == null) || ((featureLayer.GetFeatureClass().GetDefinition() == null)))
                        return;

                    // Get the featureLayer feature class.
                    FeatureClass featureClass = featureLayer.GetFeatureClass();

                    // Check the featureLayer is a polygon feature featureLayer.
                    //BasicFeatureLayer basicFeatureLayer = featureLayer as BasicFeatureLayer;
                    if (featureLayer.ShapeType != esriGeometryType.esriGeometryPolygon)
                        throw (new Exception("Invalid geometry type."));

                    hluFieldMap = new int[_hluLayerStructure.Columns.Count];
                    hluFieldNames = new string[_hluLayerStructure.Columns.Count];

                    int i = 0;

                    // Loop through the columns in the HLU GIS featureLayer structure.
                    foreach (DataColumn col in _hluLayerStructure.Columns)
                    {
                        // Get the column name.
                        string colName = col.ColumnName;

                        // Get the data type.
                        int fieldType;
                        if (!_typeMapSystemToSQL.TryGetValue(col.DataType, out fieldType))
                            throw (new Exception("Invalid field type."));

                        // Get the esri field type.
                        esriFieldType esriColType = (esriFieldType)fieldType;

                        // Get the maximum text length.
                        int colMaxLength = 0;
                        if ((col.MaxLength != -1) && (esriColType == esriFieldType.esriFieldTypeString))
                            colMaxLength = col.MaxLength;

                        int ordinal = await GetFieldOrdinalAsync(featureLayer, colName, esriColType, colMaxLength);

                        //TODO: ArcGIS
                        if (ordinal == -1)
                            throw (new Exception(String.Format("Field {0} not found in HLU GIS featureLayer.", colName)));

                        //if (fcField.Type != fixedField.Type)
                        //    throw (new Exception("Field type does not match the HLU GIS featureLayer structure."));

                        //if ((fcField.Type == esriFieldType.esriFieldTypeString) && (fcField.Length > fixedField.Length))
                        //    throw (new Exception("Field length does not match the HLU GIS featureLayer structure."));

                        hluFieldMap[i] = ordinal;
                        hluFieldNames[i] = colName;
                        i += 1;
                    }

                    // Should we activate the featureLayer.
                    if (activate)
                    {
                        // Check if the featureLayer is editable.
                        bool isEditable = await IsLayerEditableAsync(featureLayer.Name);

                        _hluLayer = featureLayer;
                        _hluFieldMap = hluFieldMap;
                        _hluFieldNames = hluFieldNames; //TODO: Not set when tool first loaded? (only when layer changed)
                        _hluFeatureClass = featureClass;
                        _hluCurrentLayer = new(featureLayer.Name, isEditable);
                    }

                    isHlu = true;
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return isHlu;
        }

        #endregion HLULayers

        #region Editing

        /// <summary>
        /// Check if there are any unsaved edits.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> AnyUnsavedEditsAsync()
        {
            return await QueuedTask.Run(() =>
            {
                return Project.Current.HasEdits;
            });
        }

        /// <summary>
        /// Get a list of editable feature layers.
        /// </summary>
        /// <returns></returns>
        public async Task<List<FeatureLayer>> GetEditableLayersAsync()
        {
            List<FeatureLayer> editableLayers = [];

            // Check the active map
            if (_activeMap == null)
                return editableLayers;

            await QueuedTask.Run(() =>
            {
                //Get the feature layers in the active map view.
                List<FeatureLayer> featureLayerList = _activeMap.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                // Loop through layers and check for edit capability
                foreach (var layer in featureLayerList)
                {
                    if (layer.CanEditData())
                    {
                        editableLayers.Add(layer);
                    }
                }
            });

            return editableLayers;
        }

        /// <summary>
        /// Check if a layer is editable.
        /// </summary>
        public async Task<bool> IsLayerEditableAsync(string layerName)
        {
            // Get the list of editable layers
            List<FeatureLayer> editableLayers = await GetEditableLayersAsync();

            // Check if the specified layer is in the list
            return editableLayers.Any(layer => layer.Name.Equals(layerName, System.StringComparison.OrdinalIgnoreCase));
        }

        #endregion Editing
    }

    /// <summary>
    /// This helper class provides ArcGIS Pro feature class and layer functions.
    /// </summary>
    internal static class ArcGISFunctions
    {
        #region Feature Class

        /// <summary>
        /// Check if the feature class exists in the file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Substring(fileName.Length - 4, 1) == ".")
            {
                // It's a file.
                if (FileFunctions.FileExists(filePath + @"\" + fileName))
                    return true;
                else
                    return false;
            }
            else if (filePath.Substring(filePath.Length - 3, 3).Equals("sde", StringComparison.OrdinalIgnoreCase))
            {
                // It's an SDE class. Not handled (use SQL Server Functions).
                return false;
            }
            else // It is a geodatabase class.
            {
                try
                {
                    return await FeatureClassExistsGDBAsync(filePath, fileName);
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if the feature class exists.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsAsync(string fullPath)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(fullPath))
                return false;

            return await FeatureClassExistsAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Delete a feature class by file path and file name.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteFeatureClassAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            string featureClass = filePath + @"\" + fileName;

            return await DeleteFeatureClassAsync(featureClass);
        }

        /// <summary>
        /// Delete a feature class by file name.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteFeatureClassAsync(string fileName)
        {
            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(fileName);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.Delete", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.Delete", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add a field to a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="fieldType"></param>
        /// <param name="fieldPrecision"></param>
        /// <param name="fieldScale"></param>
        /// <param name="fieldLength"></param>
        /// <param name="fieldAlias"></param>
        /// <param name="fieldIsNullable"></param>
        /// <param name="fieldIsRequred"></param>
        /// <param name="fieldDomain"></param>
        /// <returns>bool</returns>
        public static async Task<bool> AddFieldAsync(string inTable, string fieldName, string fieldType = "TEXT",
            long fieldPrecision = -1, long fieldScale = -1, long fieldLength = -1, string fieldAlias = null,
            bool fieldIsNullable = true, bool fieldIsRequred = false, string fieldDomain = null)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, fieldType,
                fieldPrecision > 0 ? fieldPrecision : null, fieldScale > 0 ? fieldScale : null, fieldLength > 0 ? fieldLength : null,
                fieldAlias ?? null, fieldIsNullable ? "NULLABLE" : "NON_NULLABLE",
                fieldIsRequred ? "REQUIRED" : "NON_REQUIRED", fieldDomain);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.AddField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.AddField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Rename a field in a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="newFieldName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> RenameFieldAsync(string inTable, string fieldName, string newFieldName)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input old field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            // Check if there is an input new field name.
            if (string.IsNullOrEmpty(newFieldName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, newFieldName);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.AlterField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.AlterField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate a field in a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="fieldCalc"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateFieldAsync(string inTable, string fieldName, string fieldCalc)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input field name.
            if (string.IsNullOrEmpty(fieldName))
                return false;

            // Check if there is an input field calculcation string.
            if (string.IsNullOrEmpty(fieldCalc))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, fieldCalc);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.CalculateField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate the geometry of a feature class.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="geometryProperty"></param>
        /// <param name="lineUnit"></param>
        /// <param name="areaUnit"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateGeometryAsync(string inTable, string geometryProperty, string lineUnit = "", string areaUnit = "")
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input geometry property.
            if (string.IsNullOrEmpty(geometryProperty))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, geometryProperty, lineUnit, areaUnit);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.CalculateGeometryAttributes", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CalculateGeometryAttributes", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Count the features in a layer using a search where clause.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="whereClause"></param>
        /// <param name="subfields"></param>
        /// <param name="prefixClause"></param>
        /// <param name="postfixClause"></param>
        /// <returns>long</returns>
        public static async Task<long> GetFeaturesCountAsync(FeatureLayer layer, string whereClause = null, string subfields = null, string prefixClause = null, string postfixClause = null)
        {
            // Check if there is an input layer name.
            if (layer == null)
                return -1;

            long featureCount = 0;
            try
            {
                // Create a query filter using the where clause.
                QueryFilter queryFilter = new();

                // Apply where clause.
                if (!string.IsNullOrEmpty(whereClause))
                    queryFilter.WhereClause = whereClause;

                // Apply subfields clause.
                if (!string.IsNullOrEmpty(subfields))
                    queryFilter.SubFields = subfields;

                // Apply prefix clause.
                if (!string.IsNullOrEmpty(prefixClause))
                    queryFilter.PrefixClause = prefixClause;

                // Apply postfix clause.
                if (!string.IsNullOrEmpty(postfixClause))
                    queryFilter.PostfixClause = postfixClause;

                await QueuedTask.Run(() =>
                {
                    /// Count the number of features matching the search clause.
                    using FeatureClass featureClass = layer.GetFeatureClass();

                    featureCount = featureClass.GetCount(queryFilter);
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            return featureCount;
        }

        /// <summary>
        /// Count the duplicate features in a layer using a search where clause.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="keyField"></param>
        /// <param name="whereClause"></param>
        /// <returns>long</returns>
        public static async Task<long> GetDuplicateFeaturesCountAsync(FeatureLayer layer, string keyField, string whereClause = null)
        {
            // Check if there is an input layer name.
            if (layer == null)
                return -1;

            // Check if there is a input key field.
            if (string.IsNullOrEmpty(keyField))
                return -1;

            long featureCount = 0;
            try
            {
                // Create a query filter using the where clause.
                QueryFilter queryFilter = new();

                // Apply where clause.
                if (!string.IsNullOrEmpty(whereClause))
                    queryFilter.WhereClause = whereClause;

                // Apply subfields clause.
                if (!string.IsNullOrEmpty(keyField))
                    queryFilter.SubFields = keyField;

                List<string> keys = [];

                await QueuedTask.Run(() =>
                {
                    /// Get the feature class for the layer.
                    using FeatureClass featureClass = layer.GetFeatureClass();

                    // Create a cursor of the features.
                    using RowCursor rowCursor = featureClass.Search(queryFilter);

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row record = rowCursor.Current;

                        // Get the key value.
                        string key = Convert.ToString(record[keyField]);
                        key ??= "";

                        // Add the key to the list of keys.
                        keys.Add(key);
                    }
                    // Dispose of the objects.
                    featureClass.Dispose();
                    rowCursor.Dispose();

                    // Get a list of any duplicate keys.
                    List<string> duplicateKeys = keys.GroupBy(x => x)
                      .Where(g => g.Count() > 1)
                      .Select(y => y.Key)
                      .ToList();

                    // Return how many duplicate keys there are.
                    featureCount = duplicateKeys.Count;
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            return featureCount;
        }

        /// <summary>
        /// Buffer the features in a feature class with a specified distance.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="bufferDistance"></param>
        /// <param name="lineSide"></param>
        /// <param name="lineEndType"></param>
        /// <param name="dissolveOption"></param>
        /// <param name="dissolveFields"></param>
        /// <param name="method"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> BufferFeaturesAsync(string inFeatureClass, string outFeatureClass, string bufferDistance,
            string lineSide = "FULL", string lineEndType = "ROUND", string dissolveOption = "NONE", string dissolveFields = "", string method = "PLANAR", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Check if there is an input buffer distance.
            if (string.IsNullOrEmpty(bufferDistance))
                return false;

            // Make a value array of strings to be passed to the tool.
            //List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, bufferDistance, lineSide, lineEndType, method, dissolveOption)];
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, bufferDistance, lineSide, lineEndType, dissolveOption)];
            if (!string.IsNullOrEmpty(dissolveFields))
                parameters.Add(dissolveFields);
            parameters.Add(method);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Buffer", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Buffer", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clip the features in a feature class using a clip feature layer.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="clipFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> ClipFeaturesAsync(string inFeatureClass, string clipFeatureClass, string outFeatureClass, bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an input clip feature class.
            if (string.IsNullOrEmpty(clipFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, clipFeatureClass, outFeatureClass)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Clip", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Clip", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Intersect the features in a feature class with another feature class.
        /// </summary>
        /// <param name="inFeatures"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="joinAttributes"></param>
        /// <param name="outputType"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> IntersectFeaturesAsync(string inFeatures, string outFeatureClass, string joinAttributes = "ALL", string outputType = "INPUT", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatures))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatures, outFeatureClass, joinAttributes, outputType)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Intersect", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Intersect", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Spatially join a feature class with another feature class.
        /// </summary>
        /// <param name="targetFeatures"></param>
        /// <param name="joinFeatures"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="joinOperation"></param>
        /// <param name="joinType"></param>
        /// <param name="fieldMapping"></param>
        /// <param name="matchOption"></param>
        /// <param name="searchRadius"></param>
        /// <param name="distanceField"></param>
        /// <param name="matchFields"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> SpatialJoinAsync(string targetFeatures, string joinFeatures, string outFeatureClass, string joinOperation = "JOIN_ONE_TO_ONE",
            string joinType = "KEEP_ALL", string fieldMapping = "", string matchOption = "INTERSECT", string searchRadius = "0", string distanceField = "",
            string matchFields = "", bool addToMap = false)
        {
            // Check if there is an input target feature class.
            if (string.IsNullOrEmpty(targetFeatures))
                return false;

            // Check if there is an input join feature class.
            if (string.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(targetFeatures, joinFeatures, outFeatureClass, joinOperation, joinType, fieldMapping,
                matchOption, searchRadius, distanceField, matchFields)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.SpatialJoin", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.SpatialJoin", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Permanently join fields from one feature class to another feature class.
        /// </summary>
        /// <param name="inFeatures"></param>
        /// <param name="inField"
        /// <param name="joinFeatures"></param>
        /// <param name="joinField"></param>
        /// <param name="fields"></param>
        /// <param name="fmOption"></param>
        /// <param name="fieldMapping"></param>
        /// <param name="indexJoinFields"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> JoinFieldsAsync(string inFeatures, string inField, string joinFeatures, string joinField,
            string fields = "", string fmOption = "NOT_USE_FM", string fieldMapping = "", string indexJoinFields = "NO_INDEXES",
            bool addToMap = false)
        {
            // Check if there is an input target feature class.
            if (string.IsNullOrEmpty(inFeatures))
                return false;

            // Check if there is an input field name.
            if (string.IsNullOrEmpty(inField))
                return false;

            // Check if there is a join feature class.
            if (string.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is a join field name.
            if (string.IsNullOrEmpty(joinField))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatures, inField, joinFeatures, joinField, fields,
                fmOption, fieldMapping, indexJoinFields)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.JoinField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.JoinField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate the summary statistics for a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="statisticsFields"></param>
        /// <param name="caseFields"></param>
        /// <param name="concatenationSeparator"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateSummaryStatisticsAsync(string inTable, string outTable, string statisticsFields,
            string caseFields = "", string concatenationSeparator = "", bool addToMap = false)
        {
            // Check if there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output table name.
            if (string.IsNullOrEmpty(outTable))
                return false;

            // Check if there is an input statistics fields string.
            if (string.IsNullOrEmpty(statisticsFields))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inTable, outTable, statisticsFields, caseFields, concatenationSeparator)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Statistics", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Statistics", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert the features in a feature class to a point feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="pointLocation"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureToPointAsync(string inFeatureClass, string outFeatureClass, string pointLocation = "CENTROID", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, pointLocation)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.FeatureToPoint", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.FeatureToPoint", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert the features in a feature class to a point feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="nearFeatureClass"></param>
        /// <param name="searchRadius"></param>
        /// <param name="location"></param>
        /// <param name="angle"></param>
        /// <param name="method"></param>
        /// <param name="fieldNames"></param>
        /// <param name="distanceUnit"></param>
        /// <returns>bool</returns>
        public static async Task<bool> NearAnalysisAsync(string inFeatureClass, string nearFeatureClass, string searchRadius = "",
            string location = "NO_LOCATION", string angle = "NO_ANGLE", string method = "PLANAR", string fieldNames = "", string distanceUnit = "")
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(nearFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, nearFeatureClass, searchRadius, location, angle, method, fieldNames, distanceUnit)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("analysis.Near", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Near", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Feature Class

        #region Geodatabase

        /// <summary>
        /// Create a new file geodatabase.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>Geodatabase</returns>
        public static Geodatabase CreateFileGeodatabase(string fullPath)
        {
            // Check if there is an input full path.
            if (string.IsNullOrEmpty(fullPath))
                return null;

            Geodatabase geodatabase;

            try
            {
                // Create a FileGeodatabaseConnectionPath with the name of the file geodatabase you wish to create
                FileGeodatabaseConnectionPath fileGeodatabaseConnectionPath = new(new Uri(fullPath));

                // Create and use the file geodatabase
                geodatabase = SchemaBuilder.CreateGeodatabase(fileGeodatabaseConnectionPath);
            }
            catch
            {
                // Handle Exception.
                return null;
            }

            return geodatabase;
        }

        /// <summary>
        /// Deletes a file geodatabase at the specified path, retrying if it's temporarily locked.
        /// </summary>
        /// <param name="fullPath">The full path to the .gdb folder to delete.</param>
        /// <returns>True if the geodatabase was successfully deleted; false otherwise.</returns>
        public static async Task<bool> DeleteFileGeodatabaseAsync(string fullPath)
        {
            // Check if there is an input full path.
            if (string.IsNullOrEmpty(fullPath))
                return false;

            bool success = false;

            // Try up to 5 times in case the geodatabase is temporarily locked.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    // Run the delete operation on the QueuedTask to ensure it's on the correct ArcGIS Pro thread.
                    await QueuedTask.Run(() =>
                    {
                        // Create a FileGeodatabaseConnectionPath using the full path
                        FileGeodatabaseConnectionPath fileGeodatabaseConnectionPath = new(new Uri(fullPath));

                        // Delete the file geodatabase using SchemaBuilder
                        SchemaBuilder.DeleteGeodatabase(fileGeodatabaseConnectionPath);
                    });

                    // If no exception was thrown, deletion was successful
                    success = true;
                    break;
                }
                catch (IOException)
                {
                    // Likely a file lock — wait briefly before retrying
                    await Task.Delay(2000);
                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    // GDB does not exist or is still open in ArcGIS Pro — not retryable
                    break;
                }
                catch (GeodatabaseTableException)
                {
                    // One or more tables may still be locked or open — not retryable
                    break;
                }
                catch (Exception)
                {
                    // Unexpected error — break to avoid silent failure
                    break;
                }
            }

            // Return whether the operation succeeded
            return success;
        }

        /// <summary>
        /// Check if a feature class exists in a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsGDBAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a FeatureClassDefinition object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(fileName);

                    if (featureClassDefinition != null)
                        exists = true;
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return exists;
        }

        /// <summary>
        /// Check if a layer exists in a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsGDBAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a TableDefinition object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(fileName);

                    if (tableDefinition != null)
                        exists = true;
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return exists;
        }

        /// <summary>
        /// Delete a feature class from a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseFCAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(fileName);

                    // Create a FeatureClassDescription object
                    FeatureClassDescription featureClassDescription = new(featureClassDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(featureClassDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a feature class from a geodatabase.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="featureClassName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseFCAsync(Geodatabase geodatabase, string featureClassName)
        {
            // Check there is an input geodatabase.
            if (geodatabase == null)
                return false;

            // Check there is an input feature class name.
            if (string.IsNullOrEmpty(featureClassName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(featureClassName);

                    // Create a FeatureClassDescription object
                    FeatureClassDescription featureClassDescription = new(featureClassDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(featureClassDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a table from a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseTableAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(fileName);

                    // Create a FeatureClassDescription object
                    TableDescription tableDescription = new(tableDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(tableDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a table from a geodatabase.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseTableAsync(Geodatabase geodatabase, string tableName)
        {
            // Check if the is an input geodatabase
            if (geodatabase == null)
                return false;

            // Check if there is an input table name.
            if (string.IsNullOrEmpty(tableName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Create a FeatureClassDescription object
                    TableDescription tableDescription = new(tableDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(tableDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                return false;
            }

            return success;
        }

        #endregion Geodatabase

        #region Table

        /// <summary>
        /// Check if a feature class exists in the file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Substring(fileName.Length - 4, 1) == ".")
            {
                // It's a file.
                if (FileFunctions.FileExists(filePath + @"\" + fileName))
                    return true;
                else
                    return false;
            }
            else if (filePath.Substring(filePath.Length - 3, 3).Equals("sde", StringComparison.OrdinalIgnoreCase))
            {
                // It's an SDE class. Not handled (use SQL Server Functions).
                return false;
            }
            else // it is a geodatabase class.
            {
                try
                {
                    bool exists = await TableExistsGDBAsync(filePath, fileName);

                    return exists;
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if a feature class exists.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsAsync(string fullPath)
        {
            // Check there is an input full path.
            if (string.IsNullOrEmpty(fullPath))
                return false;

            return await TableExistsAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        #endregion Table

        #region Outputs

        /// <summary>
        /// Prompt the user to specify an output file in the required format.
        /// </summary>
        /// <param name="fileType"></param>
        /// <param name="initialDirectory"></param>
        /// <returns>string</returns>
        public static string GetOutputFileName(string fileType, string initialDirectory = @"C:\")
        {
            BrowseProjectFilter bf = fileType switch
            {
                "Geodatabase FC" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_geodatabaseItems_featureClasses"),
                "Geodatabase Table" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_geodatabaseItems_tables"),
                "Shapefile" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_shapefiles"),
                "CSV file (comma delimited)" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_textFiles_csv"),
                "Text file (tab delimited)" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_textFiles_txt"),
                _ => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_all"),
            };

            // Display the saveItemDlg in an Open Item dialog.
            SaveItemDialog saveItemDlg = new()
            {
                Title = "Save Output As...",
                InitialLocation = initialDirectory,
                //AlwaysUseInitialLocation = true,
                //Filter = ItemFilters.Files_All,
                OverwritePrompt = false,    // This will be done later.
                BrowseFilter = bf
            };

            bool? ok = saveItemDlg.ShowDialog();

            string strOutFile = null;
            if (ok.HasValue)
                strOutFile = saveItemDlg.FilePath;

            return strOutFile; // Null if user pressed exit
        }

        #endregion Outputs

        #region CopyFeatures

        /// <summary>
        /// Copy the input feature class to the output feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inFeatureClass, string outFeatureClass, bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (string.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (string.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.CopyFeatures", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CopyFeatures", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copy the input dataset name to the output feature class.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inputWorkspace, string inputDatasetName, string outputFeatureClass, bool addToMap = false)
        {
            // Check there is an input workspace.
            if (string.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (string.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (string.IsNullOrEmpty(outputFeatureClass))
                return false;

            string inFeatureClass = inputWorkspace + @"\" + inputDatasetName;

            return await CopyFeaturesAsync(inFeatureClass, outputFeatureClass, addToMap);
        }

        /// <summary>
        /// Copy the input dataset to the output dataset.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputWorkspace"></param>
        /// <param name="outputDatasetName"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inputWorkspace, string inputDatasetName, string outputWorkspace, string outputDatasetName, bool addToMap = false)
        {
            // Check there is an input workspace.
            if (string.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (string.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (string.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (string.IsNullOrEmpty(outputDatasetName))
                return false;

            string inFeatureClass = inputWorkspace + @"\" + inputDatasetName;
            string outFeatureClass = outputWorkspace + @"\" + outputDatasetName;

            return await CopyFeaturesAsync(inFeatureClass, outFeatureClass, addToMap);
        }

        #endregion CopyFeatures

        #region Export Features

        /// <summary>
        /// Export the input table to the output table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> ExportFeaturesAsync(string inTable, string outTable, bool addToMap = false)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, outTable);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("conversion.ExportTable", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("conversion.ExportTable", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Export Features

        #region Copy Table

        /// <summary>
        /// Copy the input table to the output table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inTable, string outTable, bool addToMap = false)
        {
            // Check there is an input table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (string.IsNullOrEmpty(inTable))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, outTable);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.CopyRows", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CopyRows", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copy the input dataset name to the output table.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputTable"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inputWorkspace, string inputDatasetName, string outputTable)
        {
            // Check there is an input workspace.
            if (string.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (string.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (string.IsNullOrEmpty(outputTable))
                return false;

            string inputTable = inputWorkspace + @"\" + inputDatasetName;

            return await CopyTableAsync(inputTable, outputTable);
        }

        /// <summary>
        /// Copy the input dataset to the output dataset.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputWorkspace"></param>
        /// <param name="outputDatasetName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inputWorkspace, string inputDatasetName, string outputWorkspace, string outputDatasetName)
        {
            // Check there is an input workspace.
            if (string.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (string.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (string.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (string.IsNullOrEmpty(outputDatasetName))
                return false;

            string inputTable = inputWorkspace + @"\" + inputDatasetName;
            string outputTable = outputWorkspace + @"\" + outputDatasetName;

            return await CopyTableAsync(inputTable, outputTable);
        }

        #endregion Copy Table
    }
}