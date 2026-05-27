// HLUTool is used to view and maintain habitat and land use GIS data.
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

using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Enums;
using HLU.Exceptions;
using HLU.Helpers;
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
    /// Implements the OSMM Unload and Load operations.
    ///
    /// <para><b>Unload</b> removes the currently selected HLU features from the GIS layer, deletes
    /// their shadow map-match rows, and (for any INCID that no longer has remaining features)
    /// deletes the orphaned INCID and all its child records. A history entry is written for
    /// every deleted feature.</para>
    ///
    /// <para><b>Load</b> treats each selected new (null-INCID) feature exactly as the feature-insert
    /// workflow does: it creates one new INCID per feature (or one shared INCID for all features)
    /// and registers the features in the shadow map-match table. Habitat attributes carried on
    /// the GIS features are used to populate the new INCID records. A history entry is written
    /// for every registered feature.</para>
    /// </summary>
    internal class ViewModelWindowMainBulkLoadUnload
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        /// <summary>Field mapping chosen by the user in the Bulk Load setup dialog.</summary>
        private OsmmFieldMapping _fieldMapping;

        /// <summary>Output workspace for the staging layer (folder for shapefile, .gdb path for geodatabase).</summary>
        private string _outputWorkspace;

        /// <summary>Output feature class name for the staging layer (including .shp for shapefiles).</summary>
        private string _outputFeatureClassName;

        /// <summary>True to load only selected features; false to load all features.</summary>
        private bool _selectedOnly;

        /// <summary>Width of formatted fragment ID strings (e.g. 5 → "00001").</summary>
        private const int FragIdWidth = 5;

        /// <summary>First fragment ID string for a brand-new INCID.</summary>
        private static readonly string FirstFragId = "1".PadLeft(FragIdWidth, '0');

        #endregion Fields

        #region Constructor

        public ViewModelWindowMainBulkLoadUnload(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Unload

        /// <summary>
        /// Validates that the current selection is suitable for an OSMM unload operation and, if so,
        /// shows the layer-picker dialog and performs the unload across all checked layers.
        /// </summary>
        /// <returns>The number of features unloaded; 0 if the operation failed or was cancelled.</returns>
        internal async Task<int> OSMMUnloadAsync()
        {
            // Must have a map selection.
            if (_viewModelMain.GisSelection == null || _viewModelMain.GisSelection.Rows.Count == 0)
            {
                _viewModelMain.ShowWarning("Cannot unload: Nothing is selected on the map.", MessageCategory.OSMMLoad);
                return 0;
            }

            // All selected features must exist in the database.
            if (!_viewModelMain.CheckSelectedFrags(false))
            {
                _viewModelMain.ShowWarning("Cannot unload: One or more selected map features missing from database.", MessageCategory.OSMMLoad);
                return 0;
            }

            // Show the layer-picker dialog so the user can choose which live HLU
            // layers to include in the unload.
            IReadOnlyList<string> targetLayerNames = await ShowUnloadLayerPickerAsync();
            if (targetLayerNames == null || targetLayerNames.Count == 0)
                return 0;

            return await PerformUnloadAsync(targetLayerNames);
        }

        /// <summary>
        /// Shows the OSMM Unload layer-picker dialog and returns the list of layer names
        /// the user checked, or an empty list if cancelled.
        /// </summary>
        private Task<IReadOnlyList<string>> ShowUnloadLayerPickerAsync()
        {
            var tcs = new TaskCompletionSource<IReadOnlyList<string>>();

            IReadOnlyList<string> availableNames = _viewModelMain.GISApplication.ValidHluLayerNames
                ?? [];
            string activeName = _viewModelMain.ActiveLayerName;

            var vm = new ViewModelWindowBulkUnload(availableNames, activeName);
            var window = new WindowBulkUnload
            {
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true,
                DataContext = vm
            };

            void OnRequestClose(bool proceed, IReadOnlyList<string> selectedNames)
            {
                vm.RequestClose -= OnRequestClose;
                tcs.TrySetResult(proceed ? selectedNames : []);
                window.Close();
            }

            vm.RequestClose += OnRequestClose;
            window.ShowDialog();

            return tcs.Task;
        }

        /// <summary>
        /// Performs the OSMM unload across the specified HLU layers: for each layer, deletes
        /// the selected GIS features and their shadow map-match rows, then — after all layers
        /// have been processed — cleans up any INCIDs that are left with no remaining features.
        /// </summary>
        /// <param name="targetLayerNames">
        /// The ordered list of valid HLU layer names to process. Each layer is activated in turn
        /// so that the layer-specific GIS and MM-table context is correct.
        /// </param>
        /// <returns>The number of features unloaded; 0 if the operation failed.</returns>
        private async Task<int> PerformUnloadAsync(IReadOnlyList<string> targetLayerNames)
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Unloading ...");
            int totalFeaturesUnloaded = 0;

            // Remember the originally active layer so we can restore it afterwards.
            string originalLayerName = _viewModelMain.ActiveLayerName;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = "OSMM Unload GIS Features"
            };

            bool gisExecuted = false;

            // Accumulate all removed INCIDs across every layer so that orphan cleanup
            // is done once after all layers have been processed.
            HashSet<string> allRemovedIncids = new(StringComparer.OrdinalIgnoreCase);

            // The incid column name is the same for all geometry types — use the polygon
            // table as the canonical source (the column name is identical across all three).
            string incidColName = _viewModelMain.HluDataset.incid_mm_polygons.incidColumn.ColumnName;

            // Combined history table schema (will be built from the first layer that returns rows).
            DataTable combinedHistory = null;

            try
            {
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day,
                    currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                // ---------------------------------------------------------------
                // Process each target layer in turn.
                // ---------------------------------------------------------------
                foreach (string layerName in targetLayerNames)
                {
                    // Activate the layer so that _hluLayer, _hluFeatureClass, and
                    // _hluGeometryType all reflect this layer for the duration of this iteration.
                    bool activated = await _viewModelMain.GISApplication.IsHluLayerAsync(layerName, activate: true);
                    if (!activated)
                        throw new HLUToolException($"Layer '{layerName}' could not be activated as a valid HLU layer.");

                    // Obtain the FeatureLayer reference used to read the selection.
                    FeatureLayer featureLayer =
                        await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                            MapView.Active?.Map
                                ?.GetLayersAsFlattenedList()
                                .OfType<FeatureLayer>()
                                .FirstOrDefault(l => l.Name == layerName));

                    if (featureLayer == null)
                        throw new HLUToolException($"Could not locate layer '{layerName}' in the active map.");

                    // Read this layer's selection into a local GIS selection table
                    // using the current GIS ID column schema.
                    DataTable layerSelection = new();
                    foreach (DataColumn c in _viewModelMain.GisIDColumns)
                        layerSelection.Columns.Add(new DataColumn(c.ColumnName, c.DataType) { AllowDBNull = true });

                    layerSelection = await _viewModelMain.GISApplication.ReadMapSelectionForLayerAsync(
                        featureLayer, layerSelection);

                    if (layerSelection == null || layerSelection.Rows.Count == 0)
                        continue; // Nothing selected in this layer — skip.

                    // Count features in this layer before deletion.
                    totalFeaturesUnloaded += layerSelection.Rows.Count;

                    // Collect the distinct INCIDs being removed from this layer.
                    string localIncidColName = layerSelection.Columns.Contains(incidColName)
                        ? incidColName
                        : null;

                    if (localIncidColName != null)
                    {
                        foreach (DataRow r in layerSelection.Rows)
                        {
                            string incid = r[localIncidColName]?.ToString();
                            if (!string.IsNullOrEmpty(incid))
                                allRemovedIncids.Add(incid);
                        }
                    }

                    // ---------------------------------------------------------------
                    // 2. Capture history and queue the GIS deletes for this layer.
                    // ---------------------------------------------------------------
                    DataTable layerHistory = await _viewModelMain.GISApplication.DeleteSelectedFeaturesAsync(
                        _viewModelMain.HistoryColumns,
                        editOperation);

                    if (layerHistory == null || layerHistory.Rows.Count == 0)
                        continue; // No history rows captured — layer may have had no selected features.

                    // Merge into the combined history table.
                    combinedHistory ??= layerHistory.Clone();
                    foreach (DataRow r in layerHistory.Rows)
                        combinedHistory.ImportRow(r);

                    // ---------------------------------------------------------------
                    // 3. Delete the shadow map-match rows for this layer's selection.
                    // ---------------------------------------------------------------
                    HluGeometryTypes layerGeomType = _viewModelMain.GISApplication.HluGeometryType;

                    DataTable layerMMTable = layerGeomType switch
                    {
                        HluGeometryTypes.Line  => (DataTable)_viewModelMain.HluDataset.incid_mm_lines,
                        HluGeometryTypes.Point => (DataTable)_viewModelMain.HluDataset.incid_mm_points,
                        _                      => (DataTable)_viewModelMain.HluDataset.incid_mm_polygons
                    };

                    List<List<SqlFilterCondition>> mmWhereClause = ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                        [.. layerSelection.AsEnumerable()],
                        _viewModelMain.GisIDColumnOrdinals,
                        ViewModelWindowMain.IncidPageSize,
                        layerMMTable);

                    switch (layerGeomType)
                    {
                        case HluGeometryTypes.Line:
                        {
                            var t = _viewModelMain.HluDataset.incid_mm_lines;
                            _viewModelMain.GetIncidMMLineRows(mmWhereClause, ref t);
                            foreach (DataRow r in t.Rows) r.Delete();
                            if (_viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter?.Update(t) == -1)
                                throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                            break;
                        }
                        case HluGeometryTypes.Point:
                        {
                            var t = _viewModelMain.HluDataset.incid_mm_points;
                            _viewModelMain.GetIncidMMPointRows(mmWhereClause, ref t);
                            foreach (DataRow r in t.Rows) r.Delete();
                            if (_viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter?.Update(t) == -1)
                                throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                            break;
                        }
                        default:
                        {
                            var t = _viewModelMain.HluDataset.incid_mm_polygons;
                            _viewModelMain.GetIncidMMPolygonRows(mmWhereClause, ref t);
                            foreach (DataRow r in t.Rows) r.Delete();
                            if (_viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter?.Update(t) == -1)
                                throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                            break;
                        }
                    }
                }

                if (combinedHistory == null || combinedHistory.Rows.Count == 0)
                    throw new HLUToolException("Failed to capture history for unload: no features found in any selected layer.");

                // ---------------------------------------------------------------
                // 4. Write history — one entry per INCID across all layers.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                string histIncidColName = combinedHistory.Columns.Contains(incidColName)
                    ? incidColName
                    : combinedHistory.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                if (histIncidColName != null)
                {
                    var groups = combinedHistory.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    foreach (var grp in groups)
                    {
                        string groupIncid = grp.Key;
                        DataTable groupTable = combinedHistory.Clone();
                        foreach (DataRow r in grp)
                            groupTable.ImportRow(r);

                        Dictionary<int, string> fixedValues = new()
                        {
                            { incidColOrdinal, groupIncid }
                        };

                        vmHist.HistoryWrite(fixedValues, groupTable, Operations.OSMMUnload, nowDtTm);
                    }
                }
                else
                {
                    vmHist.HistoryWrite(null, combinedHistory, Operations.OSMMUnload, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 5. Delete orphaned INCID records (those with no remaining map-match rows
                //    in ANY of the three MM tables) after all layers have been processed.
                // ---------------------------------------------------------------
                if (allRemovedIncids.Count > 0)
                {
                    string incidTableQ    = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName);
                    string incidColQ      = _viewModelMain.DataBase.QuoteIdentifier(incidColName);
                    string mmPolygonsQ    = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName);
                    string mmLinesQ       = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_lines.TableName);
                    string mmPointsQ      = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_points.TableName);

                    // Process INCIDs in batches to avoid SQL statement length limits
                    // and ensure better query performance.
                    List<string> allOrphanIncids = [];
                    List<string> incidList = [.. allRemovedIncids];
                    int batchSize = ViewModelWindowMain.IncidPageSize;

                    for (int i = 0; i < incidList.Count; i += batchSize)
                    {
                        int count = Math.Min(batchSize, incidList.Count - i);
                        List<string> batchIncids = incidList.GetRange(i, count);

                        string quotedIncids = string.Join(",",
                            batchIncids.Select(id => _viewModelMain.DataBase.QuoteValue(id)));

                        // Select INCIDs from the current batch that have no rows in any MM table.
                        string sqlOrphans = $@"SELECT i.{incidColQ} FROM {incidTableQ} i
                            WHERE i.{incidColQ} IN ({quotedIncids})
                            AND NOT EXISTS (SELECT 1 FROM {mmPolygonsQ} mp WHERE mp.{incidColQ} = i.{incidColQ})
                            AND NOT EXISTS (SELECT 1 FROM {mmLinesQ}    ml WHERE ml.{incidColQ} = i.{incidColQ})
                            AND NOT EXISTS (SELECT 1 FROM {mmPointsQ}   mpt WHERE mpt.{incidColQ} = i.{incidColQ})";

                        IDataReader delReader = _viewModelMain.DataBase.ExecuteReader(
                            sqlOrphans, _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text);

                        if (delReader == null)
                            throw new Exception("Error checking for orphaned INCID records.");

                        while (delReader.Read())
                            allOrphanIncids.Add(delReader.GetString(0));
                        delReader.Close();
                    }

                    // Delete orphan INCIDs in batches.
                    if (allOrphanIncids.Count > 0)
                    {
                        for (int i = 0; i < allOrphanIncids.Count; i += batchSize)
                        {
                            int count = Math.Min(batchSize, allOrphanIncids.Count - i);
                            List<string> batchOrphans = allOrphanIncids.GetRange(i, count);

                            string deleteStatement = string.Format(
                                "DELETE FROM {0} WHERE {1} IN ({2})",
                                incidTableQ,
                                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                                string.Join(",", batchOrphans.Select(id => _viewModelMain.DataBase.QuoteValue(id))));

                            try
                            {
                                _viewModelMain.DataBase.ExecuteNonQuery(
                                    deleteStatement,
                                    _viewModelMain.DataBase.Connection.ConnectionTimeout,
                                    CommandType.Text);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception(
                                    $"Failed to delete orphaned rows from table [{_viewModelMain.HluDataset.incid.TableName}].", ex);
                            }
                        }
                    }
                }

                // ---------------------------------------------------------------
                // 6. Execute the queued GIS deletes.
                // ---------------------------------------------------------------
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";
                    throw new HLUToolException($"Failed to delete GIS features. {details}");
                }

                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");

                gisExecuted = true;

                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();

                if (!gisExecuted)
                {
                    try { editOperation.Abort(); }
                    catch { /* ignore */ }
                }

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"OSMM Unload failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Unload Error", MessageBoxButton.OK, MessageBoxImage.Error);

                return 0; // Failure
            }
            finally
            {
                // Restore the originally active layer (best-effort; ignore errors).
                if (!string.IsNullOrEmpty(originalLayerName))
                {
                    try
                    {
                        await _viewModelMain.GISApplication.IsHluLayerAsync(originalLayerName, activate: true);
                    }
                    catch { /* ignore */ }
                }

                if (totalFeaturesUnloaded > 0)
                {
                    _viewModelMain.IncidRowCount(true);
                    await _viewModelMain.ClearFilterAsync(false);
                    _viewModelMain.RefillIncidTable = true;
                    await _viewModelMain.GetMapSelectionAsync(false);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return totalFeaturesUnloaded;
        }

        #endregion Unload

        #region Load

        /// <summary>
        /// Validates that the current selection is suitable for a bulk load operation and, if so,
        /// performs the load. Each selected new (null-INCID) feature is registered under its own new
        /// INCID, using habitat attributes already present on the GIS feature.
        /// </summary>
        /// <param name="fieldMapping">
        /// The layer name and field-name mapping chosen by the user in the Bulk Load setup dialog.
        /// These map the input layer's fields to the five <c>lut_osmm_habitat_xref</c> lookup columns
        /// (<c>make</c>, <c>desc_group</c>, <c>desc_term</c>, <c>theme</c>, <c>feat_code</c>).
        /// </param>
        /// <param name="outputWorkspace">The output workspace path (folder for shapefiles, .gdb path for geodatabase).</param>
        /// <param name="outputFeatureClassName">The output feature class name (including .shp for shapefiles).</param>
        /// <param name="selectedOnly">True to load only selected features; false to load all features.</param>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal async Task<(bool success, int featureCount, int incidCount)> OSMMLoadAsync(
            OsmmFieldMapping fieldMapping,
            string outputWorkspace,
            string outputFeatureClassName,
            bool selectedOnly)
        {
            _fieldMapping = fieldMapping;
            _outputWorkspace = outputWorkspace;
            _outputFeatureClassName = outputFeatureClassName;
            _selectedOnly = selectedOnly;

            // If the user specified an output path, delete any pre-existing dataset there
            // before starting the load so that the copy step at the end succeeds cleanly.
            if (!string.IsNullOrEmpty(_outputWorkspace) &&
                !string.IsNullOrEmpty(_outputFeatureClassName))
            {
                bool outputExists = await ArcGISProHelpers.FeatureClassExistsAsync(
                    _outputWorkspace, _outputFeatureClassName);

                if (outputExists)
                {
                    string fullOutputPath = System.IO.Path.Combine(
                        _outputWorkspace, _outputFeatureClassName);

                    bool deleted = await ArcGISProHelpers.DeleteFeatureClassAsync(fullOutputPath);
                    if (!deleted)
                    {
                        _viewModelMain.ShowWarning(
                            $"Cannot load: the existing output layer '{fullOutputPath}' could not be deleted. " +
                            "Please delete it manually or choose a different output path.",
                            MessageCategory.OSMMLoad);
                        return (false, 0, 0);
                    }
                }
            }

            // Show the xref preview window so the user can review attribute
            // combinations and match status before the load proceeds.
            bool proceed = await ShowXrefPreviewAsync();
            if (!proceed)
                return (false, 0, 0);

            return await PerformLoadAsync();
        }

        /// <summary>
        /// Reads the OSMM attribute values for the selected features, joins them against
        /// the xref cache to determine match status, builds the preview rows grouped by
        /// unique attribute combination, and shows the <see cref="WindowOSMMXrefPreview"/>
        /// modal window.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the user chose to proceed; otherwise <see langword="false"/>.
        /// </returns>
        private async Task<bool> ShowXrefPreviewAsync()
        {
            string sourceLayerName = _fieldMapping?.LayerName
                ?? _viewModelMain.ActiveLayerName;

            // Collect selected OIDs from the source layer.
            IReadOnlyList<long> selectedOids = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
            {
                var selection = MapView.Active?.Map
                    ?.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name == sourceLayerName)
                    ?.GetSelection();

                return (IReadOnlyList<long>)(selection?.GetObjectIDs() ?? []);
            });

            if (selectedOids.Count == 0)
                return false;

            // Read OSMM xref attributes for every selected feature.
            Dictionary<long, (string make, string descGroup, string descTerm, string theme, string featCode)> osmmAttribs =
                await _viewModelMain.GISApplication.ReadOsmmAttributesAsync(
                    sourceLayerName,
                    selectedOids,
                    _fieldMapping?.MakeField,
                    _fieldMapping?.DescGroupField,
                    _fieldMapping?.DescTermField,
                    _fieldMapping?.ThemeField,
                    _fieldMapping?.FeatCodeField);

            // Build the xref cache (same call reused in PerformLoadAsync).
            Dictionary<(string, string, string, string, string), (string habprimary, string habsecond)> xrefCache =
                BuildXrefCache();

            static string Norm(string v) =>
                string.IsNullOrWhiteSpace(v) ? string.Empty : v.Trim();

            // Group by unique (make, descGroup, descTerm, theme, featCode) combination.
            var previewRows = osmmAttribs.Values
                .GroupBy(
                    a => (
                        Norm(a.make),
                        Norm(a.descGroup),
                        Norm(a.descTerm),
                        Norm(a.theme),
                        Norm(a.featCode)),
                    comparer: new TupleOrdinalIgnoreCaseComparer())
                .OrderBy(g => g.Key.Item1, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item2,  StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item3,  StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item4,  StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item5,  StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    bool matched = xrefCache.TryGetValue(
                        (g.Key.Item1, g.Key.Item2, g.Key.Item3,
                         g.Key.Item4, g.Key.Item5),
                        out var xref);

                    return new OsmmXrefPreviewRow
                    {
                        Make               = g.Key.Item1,
                        DescGroup          = g.Key.Item2,
                        DescTerm           = g.Key.Item3,
                        Theme              = g.Key.Item4,
                        FeatCode           = g.Key.Item5,
                        Count              = g.Count(),
                        HabitatPrimary     = matched ? xref.habprimary : null,
                        HabitatSecondaries = matched ? xref.habsecond  : null,
                        IsMatched          = matched
                    };
                })
                .ToList();

            // Build and show the modal preview window.
            bool userProceeded = false;

            ViewModelWindowOSMMXrefPreview previewVm = new(previewRows)
            {
                DisplayName = "OSMM Attribute Preview"
            };

            WindowOSMMXrefPreview previewWindow = new()
            {
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true,
                DataContext = previewVm
            };

            void OnPreviewRequestClose(bool proceed)
            {
                previewVm.RequestClose -= OnPreviewRequestClose;
                userProceeded = proceed;
                previewWindow.Close();
            }

            previewVm.RequestClose -= OnPreviewRequestClose; // avoid double subscription
            previewVm.RequestClose += OnPreviewRequestClose;

            previewWindow.ShowDialog();

            return userProceeded;
        }

        /// <summary>
        /// Performs the Bulk load: registers each selected null-INCID feature under its own new INCID,
        /// mirroring the separate-INCID variant of the feature-insert workflow but using
        /// <see cref="Operations.OSMMLoad"/> as the history operation code.
        /// </summary>
        private async Task<(bool success, int featureCount, int incidCount)> PerformLoadAsync()
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Loading ...");
            bool success = false;
            int featureCount = 0;
            int incidCount = 0;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = "Bulk Load GIS Features"
            };

            bool gisExecuted = false;
            string sourceLayerName = _fieldMapping?.LayerName ?? _viewModelMain.ActiveLayerName;
            Dictionary<long, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> oidAssignments = [];
            Dictionary<long, string> toidByOid = [];

            try
            {
                // ---------------------------------------------------------------
                // 1. Get selected OIDs from the OSMM source layer chosen by the user.
                // ---------------------------------------------------------------

                IReadOnlyList<long> selectedOids = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    var selection = MapView.Active?.Map
                        ?.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(l => l.Name == sourceLayerName)
                        ?.GetSelection();

                    return (IReadOnlyList<long>)(selection?.GetObjectIDs() ?? []);
                });

                if (selectedOids.Count == 0)
                    throw new HLUToolException($"No features are selected in layer '{sourceLayerName}'.");

                // ---------------------------------------------------------------
                // 2. Read OSMM xref attributes from the source layer using the field mapping.
                // ---------------------------------------------------------------
                Dictionary<long, (string make, string descGroup, string descTerm, string theme, string featCode)> osmmAttribs =
                    await _viewModelMain.GISApplication.ReadOsmmAttributesAsync(
                        sourceLayerName,
                        selectedOids,
                        _fieldMapping?.MakeField,
                        _fieldMapping?.DescGroupField,
                        _fieldMapping?.DescTermField,
                        _fieldMapping?.ThemeField,
                        _fieldMapping?.FeatCodeField);

                // ---------------------------------------------------------------
                // 2a. Read TOID values from the source layer (if a field was mapped).
                // ---------------------------------------------------------------
                if (!string.IsNullOrEmpty(_fieldMapping?.ToidField))
                {
                    toidByOid = await _viewModelMain.GISApplication.ReadSourceFieldValuesAsync(
                        sourceLayerName, selectedOids, _fieldMapping.ToidField);
                }

                // ---------------------------------------------------------------
                // 2b. Read habitat attributes from the HLU GIS layer.
                // ---------------------------------------------------------------
                Dictionary<long, (string habprimary, string habsecond, string determqty, string interpqty)> insertAttribs =
                    await _viewModelMain.GISApplication.ReadInsertAttributesAsync(selectedOids);

                // ---------------------------------------------------------------
                // 3. Build OID → (incid, fragid, ...) assignment map.
                //    Look up lut_osmm_habitat_xref via SQL using the mapped attribute
                //    values to resolve primary/secondary habitat for each feature.
                //    Each feature gets its own INCID with fragment "00001".
                // ---------------------------------------------------------------
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day,
                    currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                // Pre-build the xref lookup dictionary keyed by (make, descGroup, descTerm, theme, featCode).
                // This avoids a separate SQL round-trip per feature.
                Dictionary<(string, string, string, string, string), (string habprimary, string habsecond)> xrefCache =
                    BuildXrefCache();

                List<long> orderedOids = [.. selectedOids.OrderBy(o => o)];
                List<(string incid, string toid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> mmRowsToCreate = [];

                foreach (long oid in orderedOids)
                {
                    string newIncid = _viewModelMain.NextIncid;

                    insertAttribs.TryGetValue(oid, out var a);

                    // Attempt to resolve primary/secondary from the xref lookup.
                    string resolvedHabprimary = a.habprimary;
                    string resolvedHabsecond  = a.habsecond;

                    if (osmmAttribs != null && osmmAttribs.TryGetValue(oid, out var ox))
                    {
                        var lookupKey = (
                            ox.make     ?? string.Empty,
                            ox.descGroup ?? string.Empty,
                            ox.descTerm  ?? string.Empty,
                            ox.theme     ?? string.Empty,
                            ox.featCode  ?? string.Empty);

                        if (xrefCache.TryGetValue(lookupKey, out var xref))
                        {
                            if (!string.IsNullOrEmpty(xref.habprimary))
                                resolvedHabprimary = xref.habprimary;
                            if (!string.IsNullOrEmpty(xref.habsecond))
                                resolvedHabsecond = xref.habsecond;
                        }
                    }

                    List<string> validSecondaryCodes = ResolveValidSecondaryCodes(resolvedHabsecond);

                    string delimiter = _viewModelMain.SecondaryCodeDelimiter;
                    string habsecondMM = (validSecondaryCodes != null && validSecondaryCodes.Count > 0)
                        ? string.Join(delimiter, validSecondaryCodes)
                        : null;

                    string validHabprimary = (!string.IsNullOrEmpty(resolvedHabprimary) &&
                        _viewModelMain.IsPrimaryValidForLayerType(resolvedHabprimary)) ? resolvedHabprimary : null;
                    string validDetermqty = (!string.IsNullOrEmpty(a.determqty) &&
                        _viewModelMain.HluDataset.lut_quality_determination.Any(r => r.code == a.determqty)) ? a.determqty : null;
                    string validInterpqty = (!string.IsNullOrEmpty(a.interpqty) &&
                        _viewModelMain.HluDataset.lut_quality_interpretation.Any(r => r.code == a.interpqty)) ? a.interpqty : null;

                    CreateIncidRow(newIncid, nowDtTm, resolvedHabprimary, a.determqty, a.interpqty, validSecondaryCodes);
                    CreateSecondaryRows(newIncid, validSecondaryCodes);

                    toidByOid.TryGetValue(oid, out string sourceToid);

                    oidAssignments[oid] = (newIncid, FirstFragId, validHabprimary, habsecondMM, validDetermqty, validInterpqty);
                    mmRowsToCreate.Add((newIncid, sourceToid, FirstFragId, resolvedHabprimary, habsecondMM, a.determqty, a.interpqty));
                }

                incidCount = orderedOids.Count;
                featureCount = orderedOids.Count;

                // ---------------------------------------------------------------
                // 4. Insert incid_mm_* shadow rows.
                // ---------------------------------------------------------------
                foreach (var (incid, toid, fragid, habprimary, habsecond, determqty, interpqty) in mmRowsToCreate)
                    CreateMMRow(incid, toid, fragid, habprimary, habsecond, determqty, interpqty);

                // ---------------------------------------------------------------
                // 5. Write incid + fragid to the GIS layer; capture history.
                // ---------------------------------------------------------------
                DataTable historyTable = await _viewModelMain.GISApplication.RegisterNewFeaturesAsync(
                    oidAssignments,
                    _viewModelMain.HistoryColumns,
                    editOperation);

                if (historyTable == null || historyTable.Rows.Count == 0)
                    throw new HLUToolException("No GIS features were updated during the load.");

                // ---------------------------------------------------------------
                // 6. Execute the queued GIS edits.
                // ---------------------------------------------------------------
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";
                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");

                gisExecuted = true;

                // ---------------------------------------------------------------
                // 7. Write history — one entry per INCID.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                const string OidColumnName = "oid";
                DataTable historyForWrite = historyTable.Copy();
                if (historyForWrite.Columns.Contains(OidColumnName))
                    historyForWrite.Columns.Remove(OidColumnName);

                string incidColName = _viewModelMain.HluDataset.history.incidColumn.ColumnName;
                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                string histIncidColName = historyForWrite.Columns.Contains(incidColName)
                    ? incidColName
                    : historyForWrite.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                if (histIncidColName != null)
                {
                    var groups = historyForWrite.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    foreach (var grp in groups)
                    {
                        string groupIncid = grp.Key;
                        DataTable groupTable = historyForWrite.Clone();
                        foreach (DataRow r in grp)
                            groupTable.ImportRow(r);

                        Dictionary<int, string> fixedValues = new()
                        {
                            { incidColOrdinal, groupIncid }
                        };

                        vmHist.HistoryWrite(fixedValues, groupTable, Operations.OSMMLoad, nowDtTm);
                    }
                }
                else
                {
                    vmHist.HistoryWrite(null, historyForWrite, Operations.OSMMLoad, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 8. Commit.
                // ---------------------------------------------------------------
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                success = true;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();

                if (!gisExecuted)
                {
                    try { editOperation.Abort(); }
                    catch { /* ignore */ }
                }

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"Bulk Load failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Bulk Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    _viewModelMain.IncidRowCount(true);
                    await _viewModelMain.ClearFilterAsync(false);
                    _viewModelMain.RefillIncidTable = true;
                    await _viewModelMain.GetMapSelectionAsync(false);

                    // Create the HLU-schema staging output from the loaded OSMM features.
                    if (!string.IsNullOrEmpty(_outputWorkspace) &&
                        !string.IsNullOrEmpty(_outputFeatureClassName))
                    {
                        string fullOutputPath = System.IO.Path.Combine(
                            _outputWorkspace, _outputFeatureClassName);

                        _viewModelMain.ChangeCursor(Cursors.Wait, "Creating staging output layer ...");

                        bool created = await CreateStagingOutputAsync(
                            sourceLayerName, fullOutputPath, oidAssignments, toidByOid);

                        if (!created)
                        {
                            MessageBox.Show(
                                $"The bulk load completed successfully, but the staging output layer could not be created at:\n\n{fullOutputPath}\n\nYou can manually export the source layer if required.",
                                "HLU: Bulk Load - Output Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return (success, featureCount, incidCount);
        }

        #endregion Load

        #region Helpers — staging output

        /// <summary>
        /// Creates a staging feature class at <paramref name="fullOutputPath"/> that contains
        /// a copy of the selected OSMM source geometry plus the seven mandatory HLU attributes
        /// (<c>incid</c>, <c>toid</c>, <c>fragid</c>, <c>habprimary</c>, <c>habsecond</c>,
        /// <c>determqty</c>, <c>interpqty</c>) populated from <paramref name="oidAssignments"/>.
        /// <para>
        /// The <c>toid</c> field is left blank because the new INCIDs created during an OSMM bulk
        /// load do not yet have TOIDs assigned. <c>fragid</c> is set to the first fragment identifier.
        /// </para>
        /// </summary>
        private async Task<bool> CreateStagingOutputAsync(
            string sourceLayerName,
            string fullOutputPath,
            Dictionary<long, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> oidAssignments,
            Dictionary<long, string> toidByOid = null)
        {
            if (string.IsNullOrEmpty(sourceLayerName) || string.IsNullOrEmpty(fullOutputPath) ||
                oidAssignments == null || oidAssignments.Count == 0)
                return false;

            // Step 1: Copy the selected OSMM source features to the output path.
            bool copied = await ArcGISProHelpers.CopyFeaturesAsync(sourceLayerName, fullOutputPath, addToMap: false);
            if (!copied)
                return false;

            // Step 2: Add the seven mandatory HLU attribute fields.
            // Fields that already exist in the source layer are skipped silently by AddField.
            bool ok = true;
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "incid",      fieldLength: 12);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "toid",       fieldLength: 20);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "fragid",     fieldLength: 5);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "habprimary", fieldLength: 8);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "habsecond",  fieldLength: 80);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "determqty",  fieldLength: 2);
            ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "interpqty",  fieldLength: 2);

            if (!ok)
                return false;

            // Step 3: Populate the HLU fields on each feature by OID and collect non-HLU field names for cleanup.
            // The set of HLU field names that must be retained in the staging layer.
            HashSet<string> hluFieldNames = new(StringComparer.OrdinalIgnoreCase)
            {
                "incid", "toid", "fragid", "habprimary", "habsecond", "determqty", "interpqty"
            };

            List<string> fieldsToDelete = [];

            bool updated = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
            {
                try
                {
                    string dir  = System.IO.Path.GetDirectoryName(fullOutputPath);
                    string name = System.IO.Path.GetFileNameWithoutExtension(fullOutputPath);

                    if (dir == null || !System.IO.Directory.Exists(dir))
                        return false;

                    ArcGIS.Core.Data.FeatureClass fc = null;
                    try
                    {
                        using var gdb = new ArcGIS.Core.Data.Geodatabase(
                            new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(dir)));
                        fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(name);
                    }
                    catch { fc = null; }

                    if (fc == null)
                        return false;

                    using (fc)
                    {
                        ArcGIS.Core.Data.TableDefinition def = fc.GetDefinition();
                        IReadOnlyList<ArcGIS.Core.Data.Field> allFields = def.GetFields();

                        // Identify non-HLU fields to delete after populating (exclude system/geometry fields).
                        HashSet<string> systemFields = new(StringComparer.OrdinalIgnoreCase)
                        {
                            "FID", "OBJECTID", "Shape", "SHAPE",
                            "Shape_Length", "SHAPE_Length", "Shape_Area", "SHAPE_Area",
                            "GlobalID", "GLOBALID"
                        };

                        foreach (var f in allFields)
                        {
                            if (!systemFields.Contains(f.Name) && !hluFieldNames.Contains(f.Name))
                                fieldsToDelete.Add(f.Name);
                        }

                        // Cache field indices.
                        int idxIncid      = def.FindField("incid");
                        int idxToid       = def.FindField("toid");
                        int idxFragid     = def.FindField("fragid");
                        int idxHabprimary = def.FindField("habprimary");
                        int idxHabsecond  = def.FindField("habsecond");
                        int idxDetermqty  = def.FindField("determqty");
                        int idxInterpqty  = def.FindField("interpqty");

                        ArcGIS.Core.Data.QueryFilter qf = new()
                        {
                            ObjectIDs = [.. oidAssignments.Keys]
                        };

                        using ArcGIS.Core.Data.RowCursor cursor = fc.Search(qf, false);

                        while (cursor.MoveNext())
                        {
                            using ArcGIS.Core.Data.Row row = cursor.Current;
                            long oid = row.GetObjectID();

                            if (!oidAssignments.TryGetValue(oid, out var a))
                                continue;

                            string toid = null;
                            toidByOid?.TryGetValue(oid, out toid);

                            if (idxIncid      >= 0) row[idxIncid]      = a.incid      ?? (object)DBNull.Value;
                            if (idxToid       >= 0) row[idxToid]       = toid         ?? (object)DBNull.Value;
                            if (idxFragid     >= 0) row[idxFragid]     = a.fragid     ?? (object)DBNull.Value;
                            if (idxHabprimary >= 0) row[idxHabprimary] = a.habprimary ?? (object)DBNull.Value;
                            if (idxHabsecond  >= 0) row[idxHabsecond]  = a.habsecond  ?? (object)DBNull.Value;
                            if (idxDetermqty  >= 0) row[idxDetermqty]  = a.determqty  ?? (object)DBNull.Value;
                            if (idxInterpqty  >= 0) row[idxInterpqty]  = a.interpqty  ?? (object)DBNull.Value;

                            row.Store();
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            if (!updated)
                return false;

            // Step 4: Remove non-HLU fields from the staging layer now that the HLU values are written.
            if (fieldsToDelete.Count > 0)
            {
                string dropList = string.Join(";", fieldsToDelete);
                // Best-effort: ignore failure — a warning would appear from the GP tool itself.
                await ArcGISProHelpers.DeleteFieldAsync(fullOutputPath, dropList);
            }

            // Step 5: Add the completed staging layer to the map.
            return await ArcGISProHelpers.AddFeatureLayerToMapAsync(
                System.IO.Path.GetDirectoryName(fullOutputPath),
                System.IO.Path.GetFileNameWithoutExtension(fullOutputPath));
        }

        #endregion Helpers — staging output

        #region Helpers — copied from ViewModelWindowMainFeatureInsert

        /// <summary>
        /// Resolves the valid secondary habitat codes for a given raw secondary code string,
        /// filtering to codes that are valid for the active layer geometry type.
        /// </summary>
        private List<string> ResolveValidSecondaryCodes(string habsecond)
        {
            if (string.IsNullOrEmpty(habsecond))
                return null;

            string delimiter = _viewModelMain.SecondaryCodeDelimiter;
            string[] rawCodes = habsecond.Split(
                [delimiter],
                StringSplitOptions.RemoveEmptyEntries);

            return [.. rawCodes
                .Where(c => !string.IsNullOrWhiteSpace(c) &&
                            _viewModelMain.SecondaryHabitatCodesAll?.Any(s => s.code == c) == true)
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Creates a minimal <c>incid</c> row in the database for the supplied INCID string.
        /// </summary>
        private void CreateIncidRow(string newIncid, DateTime nowDtTm,
            string habprimary = null, string determqty = null, string interpqty = null,
            List<string> validSecondaryCodes = null)
        {
            HluDataSet.incidRow newRow = _viewModelMain.IncidTable.NewincidRow();
            newRow.incid = newIncid;
            newRow.habitat_version = "0";
            newRow.boundary_base_map = "UK";
            newRow.digitisation_base_map = "UK";
            newRow.created_date = nowDtTm;
            newRow.created_user_id = _viewModelMain.UserID;
            newRow.last_modified_date = nowDtTm;
            newRow.last_modified_user_id = _viewModelMain.UserID;

            if (!string.IsNullOrEmpty(habprimary) &&
                _viewModelMain.IsPrimaryValidForLayerType(habprimary))
            {
                newRow.habitat_primary = habprimary;
                newRow.habitat_class = _viewModelMain.GetHabitatClassForPrimary(habprimary) ?? "UKHab";
                newRow.habitat_version = _viewModelMain.GetHabitatVersionForPrimary(habprimary) ?? "0";

                if (validSecondaryCodes != null && validSecondaryCodes.Count > 0)
                    newRow.habitat_secondaries = string.Join(
                        _viewModelMain.SecondaryCodeDelimiter, validSecondaryCodes);
            }

            if (!string.IsNullOrEmpty(determqty) &&
                _viewModelMain.HluDataset.lut_quality_determination.Any(r => r.code == determqty))
                newRow.quality_determination = determqty;

            if (!string.IsNullOrEmpty(interpqty) &&
                _viewModelMain.HluDataset.lut_quality_interpretation.Any(r => r.code == interpqty))
                newRow.quality_interpretation = interpqty;

            _viewModelMain.IncidTable.AddincidRow(newRow);

            if (_viewModelMain.HluTableAdapterManager.incidTableAdapter.Update(
                    _viewModelMain.HluDataset.incid) == -1)
                throw new Exception($"Failed to insert row into table [{_viewModelMain.HluDataset.incid.TableName}].");

            _viewModelMain.IncidTable.RejectChanges();
        }

        /// <summary>
        /// Creates the <c>incid_secondary</c> rows for a new INCID.
        /// </summary>
        private void CreateSecondaryRows(string newIncid, List<string> validSecondaryCodes)
        {
            if (validSecondaryCodes == null || validSecondaryCodes.Count == 0)
                return;

            var table = _viewModelMain.HluDataset.incid_secondary;

            foreach (string code in validSecondaryCodes)
            {
                HluDataSet.incid_secondaryRow row = table.Newincid_secondaryRow();
                row.incid = newIncid;
                row.secondary_id = _viewModelMain.RecIDs.NextIncidSecondaryId;
                row.secondary = code;
                table.Addincid_secondaryRow(row);
            }

            if (_viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter?.Update(table) == -1)
                throw new Exception($"Failed to insert rows into table [{table.TableName}].");

            table.RejectChanges();
        }

        /// <summary>
        /// Creates one row in the appropriate <c>incid_mm_*</c> shadow table for the active
        /// GIS layer geometry type, then INSERTs it into the database.
        /// </summary>
        private void CreateMMRow(string incid, string toid, string fragid,
            string habprimary = null, string habsecond = null,
            string determqty = null, string interpqty = null)
        {
            switch (_viewModelMain.GisLayerType)
            {
                case HluGeometryTypes.Line:
                {
                    var table = _viewModelMain.HluDataset.incid_mm_lines;
                    HluDataSet.incid_mm_linesRow row = table.Newincid_mm_linesRow();
                    row.incid = incid;
                    if (!string.IsNullOrEmpty(toid))       row.toid       = toid;
                    row.fragid = fragid;
                    if (!string.IsNullOrEmpty(habprimary)) row.habprimary = habprimary;
                    if (!string.IsNullOrEmpty(habsecond))  row.habsecond  = habsecond;
                    if (!string.IsNullOrEmpty(determqty))  row.determqty  = determqty;
                    if (!string.IsNullOrEmpty(interpqty))  row.interpqty  = interpqty;
                    table.Addincid_mm_linesRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_linesDataTable, HluDataSet.incid_mm_linesRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }

                case HluGeometryTypes.Point:
                {
                    var table = _viewModelMain.HluDataset.incid_mm_points;
                    HluDataSet.incid_mm_pointsRow row = table.Newincid_mm_pointsRow();
                    row.incid = incid;
                    if (!string.IsNullOrEmpty(toid))       row.toid       = toid;
                    row.fragid = fragid;
                    if (!string.IsNullOrEmpty(habprimary)) row.habprimary = habprimary;
                    if (!string.IsNullOrEmpty(habsecond))  row.habsecond  = habsecond;
                    if (!string.IsNullOrEmpty(determqty))  row.determqty  = determqty;
                    if (!string.IsNullOrEmpty(interpqty))  row.interpqty  = interpqty;
                    table.Addincid_mm_pointsRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_pointsDataTable, HluDataSet.incid_mm_pointsRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }

                default:
                {
                    var table = _viewModelMain.HluDataset.incid_mm_polygons;
                    HluDataSet.incid_mm_polygonsRow row = table.Newincid_mm_polygonsRow();
                    row.incid = incid;
                    if (!string.IsNullOrEmpty(toid))       row.toid       = toid;
                    row.fragid = fragid;
                    if (!string.IsNullOrEmpty(habprimary)) row.habprimary = habprimary;
                    if (!string.IsNullOrEmpty(habsecond))  row.habsecond  = habsecond;
                    if (!string.IsNullOrEmpty(determqty))  row.determqty  = determqty;
                    if (!string.IsNullOrEmpty(interpqty))  row.interpqty  = interpqty;
                    table.Addincid_mm_polygonsRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }
            }
        }

        #endregion Helpers — copied from ViewModelWindowMainFeatureInsert

        #region Helpers — OSMM xref lookup

        /// <summary>
        /// Queries <c>lut_osmm_habitat_xref</c> via SQL and returns a dictionary keyed by the
        /// five OSMM attribute values (<c>make</c>, <c>desc_group</c>, <c>desc_term</c>,
        /// <c>theme</c>, <c>feat_code</c>) that maps to the resolved
        /// (<c>habitat_primary</c>, <c>habitat_secondaries</c>) pair.
        /// <para>
        /// Empty-string and <c>NULL</c> column values are both normalised to <see cref="string.Empty"/>
        /// so that the dictionary key comparison is consistent with the values read from the GIS layer.
        /// </para>
        /// </summary>
        private Dictionary<(string make, string descGroup, string descTerm, string theme, string featCode),
                           (string habprimary, string habsecond)> BuildXrefCache()
        {
            var cache = new Dictionary<(string, string, string, string, string), (string, string)>(
                new TupleOrdinalIgnoreCaseComparer());

            var db = _viewModelMain.DataBase;
            var xrefTable = _viewModelMain.HluDataset.lut_osmm_habitat_xref;

            // Column names as they appear in the real database table.
            string qualTable = db.QualifyTableName(xrefTable.TableName);
            string sql = string.Format(
                "SELECT {0}, {1}, {2}, {3}, {4}, {5}, {6} FROM {7}",
                db.QuoteIdentifier("make"),
                db.QuoteIdentifier("desc_group"),
                db.QuoteIdentifier("desc_term"),
                db.QuoteIdentifier("theme"),
                db.QuoteIdentifier("feat_code"),
                db.QuoteIdentifier("habitat_primary"),
                db.QuoteIdentifier("habitat_secondaries"),
                qualTable);

            IDataReader reader = db.ExecuteReader(sql, db.Connection.ConnectionTimeout, CommandType.Text);
            if (reader == null)
                return cache;

            try
            {
                static string Norm(object v) =>
                    (v == null || v is DBNull) ? string.Empty : v.ToString().Trim();

                while (reader.Read())
                {
                    var key = (Norm(reader[0]), Norm(reader[1]), Norm(reader[2]),
                               Norm(reader[3]), Norm(reader[4]));
                    string habprimary = Norm(reader[5]);
                    string habsecond  = Norm(reader[6]);

                    // Keep first matching row only.
                    cache.TryAdd(key, (habprimary, habsecond));
                }
            }
            finally
            {
                reader.Close();
            }

            return cache;
        }

        /// <summary>
        /// Case-insensitive equality comparer for five-element string tuples,
        /// used to key the xref cache.
        /// </summary>
        private sealed class TupleOrdinalIgnoreCaseComparer
            : IEqualityComparer<(string, string, string, string, string)>
        {
            public bool Equals((string, string, string, string, string) x,
                               (string, string, string, string, string) y) =>
                string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item4, y.Item4, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item5, y.Item5, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string, string, string, string, string) obj) =>
                HashCode.Combine(
                    obj.Item1?.ToUpperInvariant(),
                    obj.Item2?.ToUpperInvariant(),
                    obj.Item3?.ToUpperInvariant(),
                    obj.Item4?.ToUpperInvariant(),
                    obj.Item5?.ToUpperInvariant());
        }

        #endregion Helpers — OSMM xref lookup
    }
}