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

        private readonly Dictionary<Type, FieldType> _typeMapSystemToFieldType =
            new()
            {
        { typeof(string), FieldType.String },
        { typeof(int), FieldType.Integer },
        { typeof(long), FieldType.BigInteger },
        { typeof(short), FieldType.SmallInteger },
        { typeof(double), FieldType.Double },
        { typeof(float), FieldType.Single },
        { typeof(DateTime), FieldType.Date },
        { typeof(bool), FieldType.Integer } // if stored as 0/1
            };

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Set the global variables.
        /// </summary>
        public ArcProApp()
        {
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
        public MapView GetActiveMapView()
        {
            // Get the active map view from the ArcGIS Pro application.
            _activeMapView = MapView.Active;

            // Set the map currently displayed in the active map view.
            if (_activeMapView != null)
                _activeMap = _activeMapView.Map;
            else
                _activeMap = null;

            // Return the map view if available; otherwise, return null.
            return _activeMapView;
        }

        /// <summary>
        /// Gets the <see cref="MapView"/> for an open pane whose map name matches the input.
        /// </summary>
        /// <param name="mapName">The name of the map.</param>
        /// <returns>The matching <see cref="MapView"/>, or <c>null</c> if not found.</returns>
        public MapView GetMapViewFromName(string mapName)
        {
            if (String.IsNullOrWhiteSpace(mapName))
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

        #endregion Map

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
            if (String.IsNullOrEmpty(layerName))
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
            if (String.IsNullOrEmpty(layerName))
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
        /// Get the list of fields for a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<ArcGIS.Core.Data.Field>> GetFCFieldsAsync(string layerPath, Map targetMap = null)
        {
            // Check there is an input feature layer path.
            if (String.IsNullOrEmpty(layerPath))
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
            if (String.IsNullOrEmpty(layerPath))
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
            if (String.IsNullOrEmpty(fieldName))
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
            if (String.IsNullOrEmpty(layerPath))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
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
            if (String.IsNullOrEmpty(layerName))
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
            if (String.IsNullOrEmpty(layerName))
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
            if (String.IsNullOrEmpty(groupLayerName))
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
            if (String.IsNullOrEmpty(groupLayerName))
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
            if (String.IsNullOrEmpty(tableName))
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

        #endregion Tables

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
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (String.IsNullOrEmpty(columns))
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
            if (outColumnsList.Count == 0 || String.IsNullOrEmpty(outColumns))
                return -1;

            // Stop if there are any missing columns.
            if (missingColumns || String.IsNullOrEmpty(columns))
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

                    if (!String.IsNullOrEmpty(orderByColumns))
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
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (String.IsNullOrEmpty(columns))
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
            if (outColumnsList.Count == 0 || String.IsNullOrEmpty(outColumns))
                return -1;

            // Stop if there are any missing columns.
            if (missingColumns || String.IsNullOrEmpty(columns))
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

                    if (!String.IsNullOrEmpty(orderByColumns))
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
            if (String.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
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
            if (String.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
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
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output file.
            if (String.IsNullOrEmpty(outFile))
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

        /// <summary>
        /// Determines asynchronously whether the specified layer is a valid HLU layer.
        /// </summary>
        /// <param name="layerName">The name of the GIS feature layer to check. Cannot be null.</param>
        /// <param name="activate">A value indicating whether to activate the layer if it is a valid HLU layer.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// specified layer is a valid HLU layer; otherwise, <see langword="false"/>.
        /// </returns>
        public async Task<bool> IsHluLayerAsync(string layerName, bool activate)
        {
            // Check there is an input GIS featureLayer.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Get the feature featureLayer for the new GIS featureLayer.
            FeatureLayer featureLayer = await FindLayerAsync(layerName);

            // Check if the feature layer a valid HLU layer.
            return await IsHluLayerAsync(featureLayer, activate);
        }

        /// <summary>
        /// Determines asynchronously whether the specified feature layer is a valid HLU layer.
        /// </summary>
        /// <param name="featureLayer">The feature layer to check.</param>
        /// <param name="activate">A value indicating whether to activate the layer if it is a valid HLU layer.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// specified layer is a valid HLU layer; otherwise, <see langword="false"/>.
        /// </returns>
        public async Task<bool> IsHluLayerAsync(FeatureLayer featureLayer, bool activate)
        {
            // Check there is an input GIS featureLayer.
            if (featureLayer == null)
                return false;

            try
            {
                // Do all ArcGIS Pro CIM/Geodatabase object access on the MCT.
                var result = await QueuedTask.Run(() =>
                {
                    // Check the feature featureLayer is valid.
                    FeatureClass featureClass = featureLayer.GetFeatureClass();
                    if (featureClass == null)
                        return HluLayerCheckResult.Invalid();

                    using FeatureClassDefinition definition = featureClass.GetDefinition();
                    if (definition == null)
                        return HluLayerCheckResult.Invalid();

                    // Check the featureLayer is a polygon feature featureLayer.
                    //BasicFeatureLayer basicFeatureLayer = featureLayer as BasicFeatureLayer;
                    if (featureLayer.ShapeType != esriGeometryType.esriGeometryPolygon)
                        return HluLayerCheckResult.Invalid();

                    int[] hluFieldMap = new int[_hluLayerStructure.Columns.Count];
                    string[] hluFieldNames = new string[_hluLayerStructure.Columns.Count];

                    // Loop through the columns in the HLU GIS featureLayer structure.
                    int i = 0;
                    foreach (DataColumn col in _hluLayerStructure.Columns)
                    {
                        // Get the column name.
                        string colName = col.ColumnName;

                        // Get the expected data type.
                        if (!_typeMapSystemToFieldType.TryGetValue(col.DataType, out FieldType expectedFieldType))
                            return HluLayerCheckResult.Invalid();

                        // Get the maximum text length.
                        int colMaxLength = 0;
                        if ((col.MaxLength != -1 && expectedFieldType == FieldType.String))
                            colMaxLength = col.MaxLength;

                        // Find the field ordinal in the feature class definition.
                        int ordinal = GetFieldOrdinal(definition, colName, expectedFieldType, colMaxLength);

                        // If the field is not found return invalid.
                        if (ordinal == -1)
                            return HluLayerCheckResult.Invalid();

                        //if (fcField.Type != fixedField.Type)
                        //    throw (new Exception("Field type does not match the HLU GIS featureLayer structure."));

                        //if ((fcField.Type == esriFieldType.esriFieldTypeString) && (fcField.Length > fixedField.Length))
                        //    throw (new Exception("Field length does not match the HLU GIS featureLayer structure."));

                        hluFieldMap[i] = ordinal;
                        hluFieldNames[i] = colName;
                        i += 1;
                    }

                    // Return a valid HLU layer result from the CIM/Geodatabase check.
                    return HluLayerCheckResult.Valid(
                        featureLayer,
                        featureClass,
                        hluFieldMap,
                        hluFieldNames);
                });

                // If not a valid HLU layer return false.
                if (!result.IsHlu)
                    return false;

                // Should we activate the featureLayer.
                if (activate)
                {
                    _hluLayer = result.FeatureLayer;
                    _hluFieldMap = result.HluFieldMap;
                    _hluFieldNames = result.HluFieldNames; //TODO: Not set when tool first loaded? (only when layer changed)
                    _hluFeatureClass = result.FeatureClass;

                    // Check if the featureLayer is editable.
                    bool isEditable = await IsLayerEditableAsync(_hluLayer);

                    // Set the active HLU layer.
                    _hluActiveLayer = new(result.FeatureLayer.Name, isEditable);
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"IsHluLayerAsync failed: {ex}.");
                return false;
            }
        }

        /// <summary>
        /// Immutable result of an HLU layer validity check.
        /// </summary>
        private readonly struct HluLayerCheckResult
        {
            public bool IsHlu
            {
                get;
            }

            public FeatureLayer FeatureLayer
            {
                get;
            }

            public FeatureClass FeatureClass
            {
                get;
            }

            public int[] HluFieldMap
            {
                get;
            }

            public string[] HluFieldNames
            {
                get;
            }

            private HluLayerCheckResult(
                bool isHlu,
                FeatureLayer featureLayer,
                FeatureClass featureClass,
                int[] hluFieldMap,
                string[] hluFieldNames)
            {
                IsHlu = isHlu;
                FeatureLayer = featureLayer;
                FeatureClass = featureClass;
                HluFieldMap = hluFieldMap;
                HluFieldNames = hluFieldNames;
            }

            public static HluLayerCheckResult Invalid()
                => new(false, null, null, null, null);

            public static HluLayerCheckResult Valid(
                FeatureLayer featureLayer,
                FeatureClass featureClass,
                int[] hluFieldMap,
                string[] hluFieldNames)
                => new(true, featureLayer, featureClass, hluFieldMap, hluFieldNames);
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
        /// Check if the given layer is currently editable (permission + enabled in map).
        /// </summary>
        /// <param name="featureLayer">The feature layer to test.</param>
        /// <returns>True if the layer is editable.</returns>
        public async Task<bool> IsLayerEditableAsync(FeatureLayer featureLayer)
        {
            if (featureLayer == null)
                return false;

            return await QueuedTask.Run(() =>
            {
                // CanEditData() answers "is it possible to edit the datasource?".
                bool canEditData = featureLayer.CanEditData(); // Must be called on the MCT.

                // IsEditable answers "is it currently editable (permissions + enabled in the map)?"
                bool isEditableInMap = (featureLayer as IDisplayTable)?.IsEditable ?? false;

                return canEditData && isEditableInMap;
            });
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(fullPath))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
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
            if (String.IsNullOrEmpty(targetFeatures))
                return false;

            // Check if there is an input join feature class.
            if (String.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
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
            if (String.IsNullOrEmpty(inFeatures))
                return false;

            // Check if there is an input field name.
            if (String.IsNullOrEmpty(inField))
                return false;

            // Check if there is a join feature class.
            if (String.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is a join field name.
            if (String.IsNullOrEmpty(joinField))
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
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output table name.
            if (String.IsNullOrEmpty(outTable))
                return false;

            // Check if there is an input statistics fields string.
            if (String.IsNullOrEmpty(statisticsFields))
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
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
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
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(nearFeatureClass))
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
            if (String.IsNullOrEmpty(fullPath))
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
            if (String.IsNullOrEmpty(fullPath))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(featureClassName))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(tableName))
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
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
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
            if (String.IsNullOrEmpty(fullPath))
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
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
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
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (String.IsNullOrEmpty(outputFeatureClass))
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
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (String.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (String.IsNullOrEmpty(outputDatasetName))
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
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(inTable))
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
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(inTable))
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
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (String.IsNullOrEmpty(outputTable))
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
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (String.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (String.IsNullOrEmpty(outputDatasetName))
                return false;

            string inputTable = inputWorkspace + @"\" + inputDatasetName;
            string outputTable = outputWorkspace + @"\" + outputDatasetName;

            return await CopyTableAsync(inputTable, outputTable);
        }

        #endregion Copy Table
    }
}