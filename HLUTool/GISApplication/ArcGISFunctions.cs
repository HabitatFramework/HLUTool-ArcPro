// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2025-2026 Andy Foy Consulting
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
using HLU.Helpers;
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
    /// Provides methods for interacting with ArcGIS Pro application, maps, layers, and tables.
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
        /// Initializes a new instance of the <see cref="ArcProApp"/> class and sets up necessary
        /// structures and type mappings.
        /// </summary>
        public ArcProApp()
        {
            // Get the HLU featureLayer structure from the database.
            _hluLayerStructure ??= new HluGISLayer.incid_mm_polygonsDataTable();

            // Set the data type maps to/from SQL.
            GetTypeMaps(out _typeMapSystemToSQL, out _typeMapSQLToSystem);
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// Gets the name of the active map.
        /// </summary>
        /// <value>The name of the active map, or <c>null</c> if no map is active.</value>
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
        /// <param name="message">The message to write to the Trace log.</param>
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
        /// Get the list of fields for a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<Field>> GetFCFieldsAsync(string layerPath, Map targetMap = null)
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

                IReadOnlyList<Field> fields = null;
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
        /// Get the list of field names for a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>List<string></returns>
        public async Task<List<string>> GetFCFieldNamesAsync(string layerPath, Map targetMap = null)
        {
            // Check there is a layer path.
            if (String.IsNullOrEmpty(layerPath))
                return null;

            try
            {
                // Get the fields from the feature class.
                IReadOnlyList<Field> fields = await GetFCFieldsAsync(layerPath, targetMap);

                if (fields == null)
                    return null;

                // Extract just the field names.
                return [.. fields.Select(f => f.Name)];
            }
            catch (Exception ex)
            {
                TraceLog($"GetFCFieldNamesAsync error: Exception {ex.Message}");
                return null;
            }
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
            List<FeatureLayer> featureLayerList = [.. _activeMap.GetLayersAsFlattenedList().OfType<FeatureLayer>()];

            //List<FeatureLayer> layers = [];
            //foreach (var featureLayer in featureLayers) layers.Add(featureLayer);

            return featureLayerList;
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
}