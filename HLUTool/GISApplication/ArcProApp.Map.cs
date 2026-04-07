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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;

namespace HLU.GISApplication
{
    /// <summary>
    /// Provides properties and methods for interacting with ArcGIS Pro application, maps, layers,
    /// and tables.
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

        #region Zoom

        /// <summary>
        /// Zooms the active map view to the current selection in the active HLU layer,
        /// with ArcMap-compatible behaviour:
        /// - "always": always zoom to the selection.
        /// - "when": only zoom if the selection is not fully within the current view extent.
        /// </summary>
        /// <param name="minZoom">
        /// Minimum allowable scale (legacy meaning: do not remain more zoomed-in than this).
        /// Interpreted as a map scale (e.g. 10000).
        /// </param>
        /// <param name="autoZoomToSelection">
        /// 2 = always zoom, 1 = zoom only when selection is outside the visible area, 0 = do not zoom.
        /// </param>
        /// <param name="ratio">
        /// Optional zoom ratio applied after zooming, unless a valid scale list is provided.
        /// </param>
        /// <param name="validScales">
        /// Optional list of valid scales used to snap to the next scale up instead of applying the ratio directly.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ZoomSelectedAsync(
            int minZoom,
            int autoZoomToSelection,
            double? ratio = null,
            List<int> validScales = null)
        {
            // Check the parameters.
            if (_hluLayer == null)
                return;

            // Respect caller setting: 0 means do not zoom.
            if (autoZoomToSelection == 0)
                return;

            // Get the active map view. If there is no active view, we can't zoom, so return.
            MapView view = MapView.Active;
            if (view == null)
                return;

            await QueuedTask.Run(async () =>
            {
                // Get the selection from the HLU layer. If there is no selection, there is nothing to zoom to, so return.
                var selection = _hluLayer.GetSelection();
                if (selection == null || selection.GetCount() == 0)
                    return;

                // Determine whether we should zoom based on autoZoomToSelection meanings:
                //   2 = always zoom to selection.
                //   1 = zoom only when the selection is not fully within the visible map extent.
                //   0 = do not zoom at all.
                bool shouldZoom = (autoZoomToSelection == 2);

                if (autoZoomToSelection == 1)
                {
                    // Get the current visible extent of the active map view.
                    // This represents what the user can currently see on screen.
                    Envelope viewExtent = view.Extent;

                    // Get the extent of the selected features in the HLU layer.
                    // QueryExtent(true) returns the envelope of the current selection only.
                    Envelope selectionExtent = GetSelectionExtent(_hluLayer);

                    // If either extent cannot be determined, default to zooming.
                    // This mirrors the ArcMap behaviour of "better to zoom than do nothing".
                    if (viewExtent == null || selectionExtent == null)
                    {
                        shouldZoom = true;
                    }
                    else
                    {
                        // Ensure both extents are in the map's spatial reference.
                        // Geometry containment tests are unreliable if spatial references differ.
                        SpatialReference mapSpatialReference = view.Map?.SpatialReference;

                        if (mapSpatialReference != null)
                        {
                            if (viewExtent.SpatialReference == null ||
                                !viewExtent.SpatialReference.IsEqual(mapSpatialReference))
                            {
                                viewExtent = (Envelope)GeometryEngine.Instance.Project(
                                    viewExtent,
                                    mapSpatialReference);
                            }

                            if (selectionExtent.SpatialReference == null ||
                                !selectionExtent.SpatialReference.IsEqual(mapSpatialReference))
                            {
                                selectionExtent = (Envelope)GeometryEngine.Instance.Project(
                                    selectionExtent,
                                    mapSpatialReference);
                            }
                        }

                        // Is the selection extent NOT fully contained
                        // within the current visible map extent.
                        shouldZoom = !IsEnvelopeContained(
                            outer: viewExtent,
                            inner: selectionExtent);
                    }
                }

                // If the selection extent is NOT fully contained
                // within the current visible map extent don't zoom.
                if (!shouldZoom)
                    return;

                // Step 1: Zoom to the selection in this layer.
                await view.ZoomToAsync(_hluLayer, true).ConfigureAwait(false);

                // Step 2: Apply additional scale logic to mirror ArcMap's ApplyZoomToMapFrame:
                // - If a ratio is provided, apply ratio or next scale up from validScales.
                ApplyZoomToMapView(
                    view,
                    ratio,
                    null,
                    validScales);

                // Step 3: If minZoom is provided and we haven't already zoomed out to a larger
                // scale from the validScales logic, check if we are currently more zoomed-in than
                // minZoom and zoom out to minZoom.
                if (minZoom > 0 && (validScales == null || validScales.Count < 2) && !ratio.HasValue)
                {
                    // Get the current camera scale.
                    Camera camera = view.Camera;
                    double currentScale = camera.Scale;

                    // Smaller scale = more zoomed-in. If we are closer than minZoom, zoom out to minZoom.
                    if (currentScale < minZoom)
                    {
                        camera.Scale = minZoom;

                        await view.ZoomToAsync(camera, duration: null).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the extent of the currently selected features in a feature layer
        /// by iterating the selection and unioning each feature's shape extent.
        /// </summary>
        /// <remarks>
        /// This avoids FeatureLayer.QueryExtent(true) which can return an unexpectedly
        /// large extent for some layer/data-source types (e.g. query layers/joins).
        /// Must be called on the MCT (i.e. from within QueuedTask.Run).
        /// </remarks>
        /// <param name="featureLayer">The feature layer whose selection extent is required.</param>
        /// <returns>
        /// The envelope of the selected features, or null if there is no selection
        /// or feature shapes cannot be read.
        /// </returns>
        private static Envelope GetSelectionExtent(
            FeatureLayer featureLayer)
        {
            // Check there is an input feature layer.
            if (featureLayer == null)
                return null;

            // Get the selection from the feature layer. If there is no selection, return null.
            Selection selection = featureLayer.GetSelection();
            if (selection == null || selection.GetCount() == 0)
                return null;

            EnvelopeBuilderEx builder = null;

            // Search the selected rows only.
            using RowCursor cursor = selection.Search();

            while (cursor.MoveNext())
            {
                // Get the current row from the cursor.
                using Row row = cursor.Current;

                // Selected rows from a FeatureLayer should be Feature rows.
                if (row is not Feature feature)
                    continue;

                // Get the shape of the feature. If it cannot be read, skip this feature.
                Geometry shape = feature.GetShape();
                if (shape == null)
                    continue;

                // Get the extent of the shape. If it cannot be read, skip this feature.
                Envelope env = shape.Extent;
                if (env == null)
                    continue;

                // Build up a unioned envelope across all selected features.
                if (builder == null)
                {
                    builder = new EnvelopeBuilderEx(env);
                }
                else
                {
                    builder.Union(env);
                }
            }

            // Return the unioned envelope of the selected features, or null if no valid shapes were read.
            return builder?.ToGeometry() as Envelope;
        }

        /// <summary>
        /// Determines whether one envelope is fully contained within another,
        /// using a small tolerance to avoid floating-point precision issues.
        /// </summary>
        /// <param name="outer">The envelope representing the visible map extent.</param>
        /// <param name="inner">The envelope representing the selection extent.</param>
        /// <returns>
        /// True if <paramref name="inner"/> is completely inside <paramref name="outer"/>;
        /// otherwise false.
        /// </returns>
        private static bool IsEnvelopeContained(
            Envelope outer,
            Envelope inner)
        {
            // Check both envelopes are valid.
            if (outer == null || inner == null)
                return false;

            // Tolerance helps prevent false negatives due to floating-point rounding.
            double tolerance = Math.Max(outer.Width, outer.Height) * 1e-9;

            // Return true if the inner envelope is completely within the outer envelope, allowing
            // for tolerance.
            return inner.XMin >= outer.XMin - tolerance &&
                   inner.YMin >= outer.YMin - tolerance &&
                   inner.XMax <= outer.XMax + tolerance &&
                   inner.YMax <= outer.YMax + tolerance;
        }

        #endregion Zoom

        #region Helpers

        /// <summary>
        /// Returns the next scale larger than the current scale from a list of valid scales.
        /// </summary>
        /// <param name="currentScale">
        /// The current map scale (e.g. 5000).
        /// </param>
        /// <param name="scaleList">
        /// A list of valid scales. Must contain at least two values.
        /// </param>
        /// <returns>
        /// The next scale up (i.e. more zoomed-out). If the current scale exceeds
        /// the largest value in the list, the method extrapolates using the last interval.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="scaleList"/> contains fewer than two values.
        /// </exception>
        private static double GetNextScaleUp(
            double currentScale,
            List<int> scaleList)
        {
            if (scaleList == null || scaleList.Count < 2)
                throw new ArgumentException("Scale list must contain at least two values.");

            // Ensure the list is ordered from smallest (most zoomed-in)
            // to largest (most zoomed-out).
            scaleList.Sort();

            // Find the first scale larger than the current scale.
            foreach (int scale in scaleList)
            {
                if (scale > currentScale)
                    return scale;
            }

            // If we reach here, the current scale is beyond the largest defined scale.
            // Extrapolate using the difference between the last two values.
            int count = scaleList.Count;
            int last = scaleList[count - 1];
            int secondLast = scaleList[count - 2];
            int gap = last - secondLast;

            double extrapolated = last;

            while (extrapolated <= currentScale)
            {
                extrapolated += gap;
            }

            return extrapolated;
        }

        /// <summary>
        /// Applies zoom logic to a map view by modifying its camera scale,
        /// based on either a ratio, a fixed scale, or a list of valid scales.
        /// </summary>
        /// <remarks>
        /// This is a MapView-based adaptation of ApplyZoomToMapFrame.
        /// Camera changes are applied using ZoomToAsync rather than direct assignment.
        /// </remarks>
        /// <param name="mapView">
        /// The active map view to apply the zoom to.
        /// </param>
        /// <param name="ratio">
        /// Optional zoom ratio to apply (e.g. 1.5).
        /// Ignored if <paramref name="scale"/> is provided.
        /// </param>
        /// <param name="scale">
        /// Optional fixed scale to apply directly (e.g. 10000).
        /// </param>
        /// <param name="validScales">
        /// Optional list of valid scales. If supplied and <paramref name="ratio"/> is set,
        /// the next scale up from the list is chosen instead of applying the ratio directly.
        /// </param>
        private static void ApplyZoomToMapView(
            MapView mapView,
            double? ratio,
            double? scale,
            List<int> validScales = null)
        {
            if (mapView == null)
                return;

            try
            {
                Camera camera = mapView.Camera;

                // Ratio-based zoom logic.
                if (ratio.HasValue)
                {
                    // If a scale list is supplied, choose the next valid scale up.
                    if (validScales != null && validScales.Count >= 2)
                    {
                        double currentScale = camera.Scale;
                        double nextScale = GetNextScaleUp(currentScale, validScales);

                        camera.Scale = nextScale;

                        // Apply the updated camera.
                        _ = mapView.ZoomToAsync(camera, duration: null);
                    }
                    else
                    {
                        // No scale list supplied: apply the ratio directly.
                        camera.Scale *= ratio.Value;

                        _ = mapView.ZoomToAsync(camera, duration: null);
                    }
                }
                // Fixed-scale zoom logic.
                else if (scale.HasValue && scale.Value > 0)
                {
                    camera.Scale = scale.Value;

                    _ = mapView.ZoomToAsync(camera, duration: null);
                }
            }
            catch (Exception ex)
            {
                // Zoom errors should never be fatal to the calling workflow.
                System.Diagnostics.Trace.WriteLine(
                    $"ApplyZoomToMapView error: {ex.Message}");
            }
        }

        #endregion Helpers

        #region Flash

        /// <summary>
        /// Flashes the features matching the supplied where clause (built from SqlFilterCondition
        /// list). Flashes all matched features at the same time, twice.
        /// </summary>
        /// <param name="whereClause">A list of SQL filter conditions representing the where clause.</param>
        public void FlashSelectedFeature(List<SqlFilterCondition> whereClause)
        {
            _ = FlashSelectedFeatureAsync(whereClause);
        }

        /// <summary>
        /// Flashes the features matching the supplied where clauses (built from SqlFilterCondition
        /// lists). Flashes all matched features at the same time, twice.
        /// </summary>
        /// <param name="whereClauses">
        /// A list of lists of SQL filter conditions representing the where clauses.
        /// </param>
        public void FlashSelectedFeatures(List<List<SqlFilterCondition>> whereClauses)
        {
            _ = FlashSelectedFeaturesAsync(whereClauses);
        }

        /// <summary>
        /// Asynchronously flashes the features matching the supplied where clause.
        /// </summary>
        /// <param name="whereClause">A list of SQL filter conditions representing the where clause.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task FlashSelectedFeatureAsync(List<SqlFilterCondition> whereClause)
        {
            // Check the where clause is not empty.
            if (whereClause == null || whereClause.Count == 0)
                return;

            // Build the where clause string.
            string wc =
                WhereClause(
                    false,
                    false,
                    false,
                    MapWhereClauseFields(_hluLayerStructure, whereClause));

            // Flash the features matching the where clause string.
            await FlashWhereClausesAsync([wc]).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously flashes the features matching the supplied where clauses.
        /// </summary>
        /// <param name="whereClauses">
        /// A list of lists of SQL filter conditions representing the where clauses.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task FlashSelectedFeaturesAsync(List<List<SqlFilterCondition>> whereClauses)
        {
            // Check the where clauses are not empty.
            if (whereClauses == null || whereClauses.Count == 0)
                return;

            // Build the where clause strings.
            List<string> wcs = [];
            foreach (List<SqlFilterCondition> wcList in whereClauses)
            {
                if (wcList == null || wcList.Count == 0)
                    continue;

                string wc =
                    WhereClause(
                        false,
                        false,
                        false,
                        MapWhereClauseFields(_hluLayerStructure, wcList));

                if (!String.IsNullOrWhiteSpace(wc))
                    wcs.Add(wc);
            }

            if (wcs.Count == 0)
                return;

            // Flash the features matching the where clause strings.
            await FlashWhereClausesAsync(wcs).ConfigureAwait(false);
        }

        /// <summary>
        /// Flashes all features matching any of the supplied where clauses, twice, at the same time.
        /// </summary>
        /// <param name="whereClauses">A list of where clause strings to match features to flash.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task FlashWhereClausesAsync(IEnumerable<string> whereClauses)
        {
            // Check prerequisites.
            if (_hluLayer == null)
                return;

            if (_hluFeatureClass == null)
                return;

            if (MapView.Active == null)
                return;

            // Collect OIDs for all where clauses (unioned), on the MCT.
            List<long> oids = await QueuedTask.Run(() =>
            {
                HashSet<long> oidSet = [];

                // Query each where clause in turn.
                foreach (string wc in whereClauses.Where(s => !String.IsNullOrWhiteSpace(s)))
                {
                    // Build a query filter for the where clause.
                    QueryFilter qf = new()
                    {
                        WhereClause = wc
                    };

                    // Search the feature class for matching features.
                    using RowCursor cursor = _hluFeatureClass.Search(qf, false);

                    // Collect OIDs from the result set.
                    while (cursor.MoveNext())
                    {
                        using Row row = cursor.Current;

                        long oid = TryGetObjectId(row);

                        if (oid >= 0)
                            oidSet.Add(oid);
                    }
                }

                return oidSet.Count == 0 ? [] : oidSet.ToList();
            }).ConfigureAwait(false);

            // If no OIDs were found, nothing to flash.
            if (oids.Count == 0)
                return;

            // Build a SelectionSet for “flash all at once”.
            // SelectionSet.FromDictionary expects MapMember → List<long>.
            Dictionary<MapMember, List<long>> selectionDictionary = new()
            {
                { _hluLayer, oids }
            };

            // Flash the features by OID on the MCT.
            await QueuedTask.Run(() =>
            {
                SelectionSet selectionSet = SelectionSet.FromDictionary(selectionDictionary);
                MapView.Active.FlashFeature(selectionSet);
            }).ConfigureAwait(false);

            // Flashing twice is not natively supported, so we flash twice in succession with a short delay.
            //await Task.Delay(50).ConfigureAwait(false);

            //await QueuedTask.Run(() =>
            //{
            //    SelectionSet selectionSet = SelectionSet.FromDictionary(selectionDictionary);
            //    MapView.Active.FlashFeature(selectionSet);
            //}).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to read the ObjectID from a Row in a version-tolerant way.
        /// </summary>
        /// <param name="row"> The Row from which to attempt to read the ObjectID.</param>
        /// <returns>The ObjectID if it can be read; otherwise, -1.</returns>
        private static long TryGetObjectId(Row row)
        {
            // Check there is an input row.
            if (row == null)
                return -1;

            try
            {
                // Attempt to get the ObjectID using the standard method. This should work for most
                // feature classes and tables.
                return row.GetObjectID();
            }
            catch
            {
                // Fallback: many datasets expose OBJECTID/OID as a field value.
                // This is intentionally defensive.
                try
                {
                    object v = row["OBJECTID"];
                    if (v == null || v == DBNull.Value)
                        v = row["OID"];

                    if (v == null || v == DBNull.Value)
                        return -1;

                    return Convert.ToInt64(v, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return -1;
                }
            }
        }

        #endregion Flash

        #region Layers

        /// <summary>
        /// Find a feature layer by name in the active map.
        /// </summary>
        /// <param name="layerName">The name of the layer to find.</param>
        /// <param name="targetMap">
        /// The map in which to search for the layer. If null, the active map is used.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the found
        /// FeatureLayer, or null if not found.
        /// </returns>
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
        /// <param name="layerPath">The path or name of the feature layer.</param>
        /// <param name="targetMap">
        /// The map in which to search for the layer. If null, the active map is used.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the list of
        /// fields, or null if not found.
        /// </returns>
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
        /// <param name="layerPath">The path or name of the feature layer.</param>
        /// <param name="targetMap">
        /// The map in which to search for the layer. If null, the active map is used.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the list of
        /// field names, or null if not found.
        /// </returns>
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
        /// Get the full layer path name for a layer in the map (i.e. to include any parent group names).
        /// </summary>
        /// <param name="layer">The layer for which to get the full path.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the full
        /// layer path, or null if not found.
        /// </returns>
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
        /// <returns></returns>
        /// A list of feature layers in the current map view, or null if no active map is available.
        /// </returns>
        public List<FeatureLayer> GetFeatureLayers()
        {
            // Check there is an active map.
            if (_activeMap == null) return null;

            //Get the feature layers in the active map view.
            List<FeatureLayer> featureLayerList = [.. _activeMap.GetLayersAsFlattenedList().OfType<FeatureLayer>()];

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
                    _hluFieldNames = result.HluFieldNames;
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

            /// <summary>
            /// Initializes a new instance of the <see cref="HluLayerCheckResult"/> struct with the
            /// specified values.
            /// </summary>
            /// <param name="isHlu">Indicates whether the layer is a valid HLU layer.</param>
            /// <param name="featureLayer">The feature layer being checked.</param>
            /// <param name="featureClass">The feature class of the layer.</param>
            /// <param name="hluFieldMap">The field map for the HLU layer.</param>
            /// <param name="hluFieldNames">The field names for the HLU layer.</param>
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

            /// <summary>
            /// Returns an <see cref="HluLayerCheckResult"/> representing an invalid HLU layer check result.
            /// </summary>
            /// <returns>
            /// An <see cref="HluLayerCheckResult"/> representing an invalid HLU layer check result.
            /// </returns>
            public static HluLayerCheckResult Invalid()
                => new(false, null, null, null, null);

            /// <summary>
            /// Returns an <see cref="HluLayerCheckResult"/> representing a valid HLU layer check
            /// result with the specified values.
            /// </summary>
            /// <param name="featureLayer">The feature layer being checked.</param>
            /// <param name="featureClass">The feature class of the layer.</param>
            /// <param name="hluFieldMap">The field map for the HLU layer.</param>
            /// <param name="hluFieldNames">The field names for the HLU layer.</param>
            /// <returns>
            /// An <see cref="HluLayerCheckResult"/> representing a valid HLU layer check result.
            /// </returns>
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
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a boolean
        /// value indicating whether there are any unsaved edits.
        /// </returns>
        public async Task<bool> AnyUnsavedEditsAsync()
        {
            return await QueuedTask.Run(() =>
            {
                // Does the project have unsaved edits? This checks for any edits in the project, not just in the active map/layer.
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
            // Check there is an input feature layer.
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