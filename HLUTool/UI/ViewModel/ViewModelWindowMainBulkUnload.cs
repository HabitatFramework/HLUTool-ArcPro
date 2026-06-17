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

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Enums;
using HLU.Exceptions;
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
    /// Implements the Bulk Unload operation.
    ///
    /// <para><b>Unload</b> removes the currently selected HLU features from the GIS layer, deletes
    /// their shadow map-match rows, and (for any INCID that no longer has remaining features)
    /// deletes the orphaned INCID and all its child records. A history entry is written for
    /// every deleted feature.</para>
    /// </summary>
    internal class ViewModelWindowMainBulkUnload
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        #endregion Fields

        #region Constructor

        public ViewModelWindowMainBulkUnload(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Unload

        /// <summary>
        /// Validates that the current selection is suitable for an Bulk unload operation and, if so,
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
        /// Shows the Bulk Unload layer-picker dialog and returns the list of layer names
        /// the user checked, or an empty list if cancelled.
        /// </summary>
        private async Task<IReadOnlyList<string>> ShowUnloadLayerPickerAsync()
        {
            var tcs = new TaskCompletionSource<IReadOnlyList<string>>();

            IReadOnlyList<string> allAvailableNames = _viewModelMain.GISApplication.ValidHluLayerNames
                ?? [];
            string activeName = _viewModelMain.ActiveLayerName;

            // Get the geometry type of the active layer to filter other layers.
            HluGeometryTypes activeGeometryType = _viewModelMain.GISApplication.HluGeometryType;

            // Filter layers to only include those with the same geometry type as the active layer.
            var filteredNames = new List<string>();
            foreach (string name in allAvailableNames)
            {
                try
                {
                    HluGeometryTypes layerGeometryType = await _viewModelMain.GISApplication
                        .GetLayerGeometryTypeAsync(name);

                    if (layerGeometryType == activeGeometryType)
                    {
                        filteredNames.Add(name);
                    }
                }
                catch
                {
                    // If geometry type cannot be determined, exclude the layer.
                }
            }

            IReadOnlyList<string> availableNames = filteredNames;

            // Gather selected/total feature counts for every available layer.
            var layerCounts = new Dictionary<string, (int Selected, long Total)>(
                StringComparer.OrdinalIgnoreCase);

            foreach (string name in availableNames)
            {
                try
                {
                    (int sel, long tot) = await _viewModelMain.GISApplication
                        .CountLayerFeaturesAsync(name);
                    layerCounts[name] = (sel, tot);
                }
                catch
                {
                    // If counts cannot be retrieved for a layer, leave it absent
                    // from the dictionary so the dialog shows no count for that entry.
                }
            }

            var vm = new ViewModelWindowBulkUnload(availableNames, activeName, layerCounts);
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

            return tcs.Task.Result;
        }

        /// <summary>
        /// Performs the Bulk unload across the specified HLU layers: for each layer, deletes the
        /// selected GIS features and their shadow map-match rows, then — after all layers have been
        /// processed — cleans up any INCIDs that are left with no remaining features.
        /// </summary>
        /// <param name="targetLayerNames">
        /// The ordered list of valid HLU layer names to process. Each layer is activated in turn so
        /// that the layer-specific GIS and MM-table context is correct.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the number
        /// of features unloaded; 0 if the operation failed.
        /// </returns>
        private async Task<int> PerformUnloadAsync(IReadOnlyList<string> targetLayerNames)
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Unloading ...");

            int totalFeaturesUnloaded = 0;

            // Remember the originally active layer so we can restore it afterwards.
            string originalLayerName = _viewModelMain.ActiveLayerName;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = "Bulk Unload GIS Features"
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

                    // Obtain the FeatureLayer and FeatureClass references used to read and delete the selection.
                    (FeatureLayer featureLayer, FeatureClass layerFeatureClass) =
                        await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                        {
                            var fl = MapView.Active?.Map
                                ?.GetLayersAsFlattenedList()
                                .OfType<FeatureLayer>()
                                .FirstOrDefault(l => l.Name == layerName);
                            return (fl, fl?.GetTable() as FeatureClass);
                        });

                    // If the layer or feature class could not be obtained, throw an exception.
                    if (featureLayer == null)
                        throw new HLUToolException($"Could not locate layer '{layerName}' in the active map.");

                    // If the feature class could not be obtained, throw an exception.
                    if (layerFeatureClass == null)
                        throw new HLUToolException($"Could not obtain feature class for layer '{layerName}'.");

                    // Read this layer's selection into a local GIS selection table
                    // using the current GIS ID column schema.
                    DataTable layerSelection = new();
                    foreach (DataColumn c in _viewModelMain.GisIDColumns)
                        layerSelection.Columns.Add(new DataColumn(c.ColumnName, c.DataType) { AllowDBNull = true });

                    // Get the selected features for this layer into the local selection table.
                    layerSelection = await _viewModelMain.GISApplication.ReadMapSelectionForLayerAsync(
                        featureLayer, layerSelection);

                    // If there are no selected features in this layer, skip to the next layer.
                    if (layerSelection == null || layerSelection.Rows.Count == 0)
                        continue; // Nothing selected in this layer — skip.

                    // Count features in this layer before deletion.
                    totalFeaturesUnloaded += layerSelection.Rows.Count;

                    // Collect the distinct INCIDs being removed from this layer.
                    string localIncidColName = layerSelection.Columns.Contains(incidColName)
                        ? incidColName
                        : null;

                    // If the local selection table has an INCID column, add all its values to the
                    // combined set of removed INCIDs.
                    if (localIncidColName != null)
                    {
                        // Loop through the local selection table and add each INCID to the combined set.
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
                    DataTable layerHistory = await _viewModelMain.GISApplication.DeleteSelectedFeaturesForLayerAsync(
                        featureLayer,
                        layerFeatureClass,
                        _viewModelMain.HistoryColumns,
                        editOperation);

                    // If no history rows were captured, layer may have had no selected features,
                    // skip to the next layer.
                    if (layerHistory == null || layerHistory.Rows.Count == 0)
                        continue;

                    // Merge the history into the combined history table.
                    combinedHistory ??= layerHistory.Clone();
                    foreach (DataRow r in layerHistory.Rows)
                        combinedHistory.ImportRow(r);

                    // ---------------------------------------------------------------
                    // 3. Delete the shadow map-match rows for this layer's selection.
                    // ---------------------------------------------------------------
                    // Get the geometry type of the current layer to determine which MM table to delete from.
                    HluGeometryTypes layerGeomType = _viewModelMain.GISApplication.HluGeometryType;

                    // Get the appropriate MM table for the current layer's geometry type.
                    DataTable layerMMTable = layerGeomType switch
                    {
                        HluGeometryTypes.Line => (DataTable)_viewModelMain.HluDataset.incid_mm_lines,
                        HluGeometryTypes.Point => (DataTable)_viewModelMain.HluDataset.incid_mm_points,
                        _ => (DataTable)_viewModelMain.HluDataset.incid_mm_polygons
                    };

                    // Create a where clause to select the map-match rows corresponding to the
                    // selected features in this layer.
                    List<List<SqlFilterCondition>> mmWhereClause = ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                        [.. layerSelection.AsEnumerable()],
                        _viewModelMain.GisIDColumnOrdinals,
                        ViewModelWindowMain.IncidPageSize,
                        layerMMTable);

                    // Delete the map-match rows from the appropriate MM table based on the geometry type.
                    switch (layerGeomType)
                    {
                        case HluGeometryTypes.Line:
                            {
                                var t = _viewModelMain.HluDataset.incid_mm_lines;
                                _viewModelMain.GetIncidMMLineRows(mmWhereClause, ref t);
                                foreach (DataRow r in t.Rows)
                                    r.Delete();
                                if (_viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter?.Update(t) == -1)
                                    throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                                break;
                            }
                        case HluGeometryTypes.Point:
                            {
                                var t = _viewModelMain.HluDataset.incid_mm_points;
                                _viewModelMain.GetIncidMMPointRows(mmWhereClause, ref t);
                                foreach (DataRow r in t.Rows)
                                    r.Delete();
                                if (_viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter?.Update(t) == -1)
                                    throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                                break;
                            }
                        default:
                            {
                                var t = _viewModelMain.HluDataset.incid_mm_polygons;
                                _viewModelMain.GetIncidMMPolygonRows(mmWhereClause, ref t);
                                foreach (DataRow r in t.Rows)
                                    r.Delete();
                                if (_viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter?.Update(t) == -1)
                                    throw new Exception($"Failed to delete rows from table [{t.TableName}].");
                                break;
                            }
                    }
                }

                // If no history rows were captured across all layers, throw an exception.
                if (combinedHistory == null || combinedHistory.Rows.Count == 0)
                    throw new HLUToolException("Failed to capture history for unload: no features found in any selected layer.");

                // ---------------------------------------------------------------
                // 4. Write history — one entry per INCID across all layers.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                // Determine the correct INCID column name in the combined history table.
                string histIncidColName = combinedHistory.Columns.Contains(incidColName)
                    ? incidColName
                    : combinedHistory.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                // Get the ordinal of the INCID column in the HLU dataset for use in fixedValues.
                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                // If the combined history table has an INCID column, group by INCID and write
                // history for each group.
                if (histIncidColName != null)
                {
                    // Group the combined history rows by INCID.
                    var groups = combinedHistory.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    // Loop through each group of history rows for a specific INCID and write them
                    // to the history table.
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

                        // Write the history for this group of rows to the history table.
                        vmHist.HistoryWrite(fixedValues, groupTable, Operations.OSMMUnload, nowDtTm);
                    }
                }
                else
                {
                    // Write the entire combined history table to the history table without grouping
                    // by INCID.
                    vmHist.HistoryWrite(null, combinedHistory, Operations.OSMMUnload, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 5. Delete orphaned INCID records (those with no remaining map-match rows
                //    in ANY of the three MM tables) after all layers have been processed.
                // ---------------------------------------------------------------
                // If any INCIDs were removed across all layers, check for orphaned INCIDs and delete them.
                if (allRemovedIncids.Count > 0)
                {
                    string incidTableQ = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName);
                    string incidColQ = _viewModelMain.DataBase.QuoteIdentifier(incidColName);
                    string mmPolygonsQ = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_polygons.TableName);
                    string mmLinesQ = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_lines.TableName);
                    string mmPointsQ = _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_mm_points.TableName);

                    // Process INCIDs in batches to avoid SQL statement length limits
                    // and ensure better query performance.
                    List<string> allOrphanIncids = [];
                    List<string> incidList = [.. allRemovedIncids];
                    int batchSize = ViewModelWindowMain.IncidPageSize;

                    // Loop through the list of removed INCIDs in batches to check for orphaned INCIDs.
                    for (int i = 0; i < incidList.Count; i += batchSize)
                    {
                        // Count the number of INCIDs in the current batch (may be less than
                        // batchSize for the last batch).
                        int count = Math.Min(batchSize, incidList.Count - i);

                        // Get the current batch of INCIDs to check for orphaned records.
                        List<string> batchIncids = incidList.GetRange(i, count);

                        // Create a comma-separated list of quoted INCIDs for use in the SQL query.
                        string quotedIncids = string.Join(",",
                            batchIncids.Select(id => _viewModelMain.DataBase.QuoteValue(id)));

                        // Create the SQL query to find orphaned INCIDs that have no remaining
                        // map-match rows in any of the three MM tables.
                        string sqlOrphans = $@"SELECT i.{incidColQ} FROM {incidTableQ} i
                            WHERE i.{incidColQ} IN ({quotedIncids})
                            AND NOT EXISTS (SELECT 1 FROM {mmPolygonsQ} mp WHERE mp.{incidColQ} = i.{incidColQ})
                            AND NOT EXISTS (SELECT 1 FROM {mmLinesQ}    ml WHERE ml.{incidColQ} = i.{incidColQ})
                            AND NOT EXISTS (SELECT 1 FROM {mmPointsQ}   mpt WHERE mpt.{incidColQ} = i.{incidColQ})";

                        // Execute the SQL query to retrieve orphaned INCIDs from the database.
                        IDataReader delReader = _viewModelMain.DataBase.ExecuteReader(
                            sqlOrphans, _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text);

                        // If the data reader is null, throw an exception indicating an error
                        // occurred while checking for orphaned INCID records.
                        if (delReader == null)
                            throw new Exception("Error checking for orphaned INCID records.");

                        // Loop through the results from the data reader and add each orphaned INCID to the list.
                        while (delReader.Read())
                            allOrphanIncids.Add(delReader.GetString(0));

                        // Close the data reader to release database resources.
                        delReader.Close();
                    }

                    // Delete orphan INCIDs in batches.
                    if (allOrphanIncids.Count > 0)
                    {
                        // Loop through the list of orphaned INCIDs in batches to delete them from the database.
                        for (int i = 0; i < allOrphanIncids.Count; i += batchSize)
                        {
                            // Count the number of orphaned INCIDs in the current batch (may be less
                            // than batchSize for the last batch).
                            int count = Math.Min(batchSize, allOrphanIncids.Count - i);

                            // Get the current batch of orphaned INCIDs to delete from the database.
                            List<string> batchOrphans = allOrphanIncids.GetRange(i, count);

                            // Create the SQL DELETE statement to remove orphaned INCIDs from the database.
                            string deleteStatement = string.Format(
                                "DELETE FROM {0} WHERE {1} IN ({2})",
                                incidTableQ,
                                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                                string.Join(",", batchOrphans.Select(id => _viewModelMain.DataBase.QuoteValue(id))));

                            try
                            {
                                // Execute the SQL DELETE statement to remove orphaned INCIDs from the database.
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

                // If the GIS edits could not be executed, throw an exception with the error message.
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";
                    throw new HLUToolException($"Failed to delete GIS features. {details}");
                }

                // Save the edits to the current ArcGIS project. If the save operation fails, throw an exception.
                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");

                gisExecuted = true;

                // Commit the database transaction and accept changes in the HLU dataset to finalize
                // the unload operation.
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();
            }
            catch (Exception ex)
            {
                // Rollback the database transaction in case of any errors during the unload operation.
                _viewModelMain.DataBase.RollbackTransaction();

                // If the GIS edits were not executed, attempt to abort the edit operation to
                // discard any pending changes.
                if (!gisExecuted)
                {
                    try
                    {
                        editOperation.Abort();
                    }
                    catch { /* ignore */ }
                }

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"Bulk Unload failed. The error message returned was:\n\n{exMessage}",
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
                        // Attempt to reactivate the original layer after the unload operation is complete.
                        await _viewModelMain.GISApplication.IsHluLayerAsync(originalLayerName, activate: true);
                    }
                    catch { /* ignore */ }
                }

                // If any features were unloaded, refresh the map selection and update the UI accordingly.
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
    }
}