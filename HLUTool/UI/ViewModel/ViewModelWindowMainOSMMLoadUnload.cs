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
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Enums;
using HLU.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
    internal class ViewModelWindowMainOSMMLoadUnload
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        /// <summary>Field mapping chosen by the user in the OSMM Load setup dialog.</summary>
        private OsmmFieldMapping _fieldMapping;

        /// <summary>Width of formatted fragment ID strings (e.g. 5 ? "00001").</summary>
        private const int FragIdWidth = 5;

        /// <summary>First fragment ID string for a brand-new INCID.</summary>
        private static readonly string FirstFragId = "1".PadLeft(FragIdWidth, '0');

        #endregion Fields

        #region Constructor

        public ViewModelWindowMainOSMMLoadUnload(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Unload

        /// <summary>
        /// Validates that the current selection is suitable for an OSMM unload operation and, if so,
        /// performs the unload.
        /// </summary>
        /// <returns><c>true</c> if the unload completed successfully; otherwise <c>false</c>.</returns>
        internal async Task<bool> OSMMUnloadAsync()
        {
            // Must have a map selection.
            if (_viewModelMain.GisSelection == null || _viewModelMain.GisSelection.Rows.Count == 0)
            {
                _viewModelMain.ShowWarning("Cannot unload: Nothing is selected on the map.", MessageCategory.OSMMLoad);
                return false;
            }

            // All selected features must exist in the database.
            if (!_viewModelMain.CheckSelectedFrags(false))
            {
                _viewModelMain.ShowWarning("Cannot unload: One or more selected map features missing from database.", MessageCategory.OSMMLoad);
                return false;
            }

            return await PerformUnloadAsync();
        }

        /// <summary>
        /// Performs the OSMM unload: deletes the selected GIS features and their corresponding
        /// database records, then cleans up any INCIDs that are left with no remaining features.
        /// </summary>
        private async Task<bool> PerformUnloadAsync()
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Unloading ...");
            bool success = true;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = "OSMM Unload GIS Features"
            };

            bool gisExecuted = false;

            try
            {
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day,
                    currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                // ---------------------------------------------------------------
                // 1. Identify the INCIDs and shadow map-match rows to remove.
                // ---------------------------------------------------------------
                string incidColName = _viewModelMain.GisLayerType switch
                {
                    HluGeometryTypes.Line => _viewModelMain.HluDataset.incid_mm_lines.incidColumn.ColumnName,
                    HluGeometryTypes.Point => _viewModelMain.HluDataset.incid_mm_points.incidColumn.ColumnName,
                    _ => _viewModelMain.HluDataset.incid_mm_polygons.incidColumn.ColumnName
                };

                // Collect the distinct INCIDs that are being removed from GIS.
                List<string> removedIncids = [.. _viewModelMain.IncidsSelectedMap
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Distinct(StringComparer.OrdinalIgnoreCase)];

                // ---------------------------------------------------------------
                // 2. Capture history and queue the GIS deletes.
                // ---------------------------------------------------------------
                DataTable historyTable = await _viewModelMain.GISApplication.DeleteSelectedFeaturesAsync(
                    _viewModelMain.HistoryColumns,
                    editOperation);

                if (historyTable == null || historyTable.Rows.Count == 0)
                    throw new HLUToolException("Failed to capture history for unload: no features found.");

                // ---------------------------------------------------------------
                // 3. Delete the shadow map-match rows for the selected features.
                // ---------------------------------------------------------------
                List<List<SqlFilterCondition>> mmWhereClause = ViewModelWindowMainHelpers.GisSelectionToWhereClause(
                    [.. _viewModelMain.GisSelection.AsEnumerable()],
                    _viewModelMain.GisIDColumnOrdinals,
                    ViewModelWindowMain.IncidPageSize,
                    _viewModelMain.GisMMTable);

                switch (_viewModelMain.GisLayerType)
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

                // ---------------------------------------------------------------
                // 4. Write history for the deleted features.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                // Group history rows by INCID so each INCID gets its own history entry.
                string histIncidColName = historyTable.Columns.Contains(incidColName)
                    ? incidColName
                    : historyTable.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                if (histIncidColName != null)
                {
                    var groups = historyTable.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    foreach (var grp in groups)
                    {
                        string groupIncid = grp.Key;
                        DataTable groupTable = historyTable.Clone();
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
                    vmHist.HistoryWrite(null, historyTable, Operations.OSMMUnload, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 5. Delete orphaned INCID records (those with no remaining map-match rows).
                //    Mirrors the merge-side orphan cleanup from ViewModelWindowMainMerge.
                // ---------------------------------------------------------------
                if (removedIncids.Count > 0)
                {
                    string sqlCount = string.Format(
                        "SELECT {0}.{1} FROM {0} LEFT JOIN {2} ON {2}.{3} = {0}.{1} WHERE {0}.{1} IN ({4}) GROUP BY {0}.{1} HAVING COUNT({2}.{3}) = 0",
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.IncidTable.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(incidColName),
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.GisMMTable.TableName),
                        _viewModelMain.DataBase.QuoteIdentifier(incidColName),
                        string.Join(",", removedIncids.Select(i => _viewModelMain.DataBase.QuoteValue(i))));

                    IDataReader delReader = _viewModelMain.DataBase.ExecuteReader(
                        sqlCount, _viewModelMain.DataBase.Connection.ConnectionTimeout, CommandType.Text);

                    if (delReader == null)
                        throw new Exception($"Error counting incid and {_viewModelMain.GisMMTable.TableName} database records.");

                    List<string> orphanIncids = [];
                    while (delReader.Read())
                        orphanIncids.Add(delReader.GetString(0));
                    delReader.Close();

                    if (orphanIncids.Count > 0)
                    {
                        string deleteStatement = string.Format(
                            "DELETE FROM {0} WHERE {1} IN ({2})",
                            _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid.TableName),
                            _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.incid.incidColumn.ColumnName),
                            string.Join(",", orphanIncids.Select(i => _viewModelMain.DataBase.QuoteValue(i))));

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
                success = false;

                if (!gisExecuted)
                {
                    try { editOperation.Abort(); }
                    catch { /* ignore */ }
                }

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"OSMM Unload failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Unload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    _viewModelMain.IncidRowCount(true);
                    await _viewModelMain.ClearFilterAsync(false);
                    _viewModelMain.RefillIncidTable = true;
                    await _viewModelMain.GetMapSelectionAsync(false);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return success;
        }

        #endregion Unload

        #region Load

        /// <summary>
        /// Validates that the current selection is suitable for an OSMM load operation and, if so,
        /// performs the load. Each selected new (null-INCID) feature is registered under its own new
        /// INCID, using habitat attributes already present on the GIS feature.
        /// </summary>
        /// <param name="fieldMapping">
        /// The layer name and field-name mapping chosen by the user in the OSMM Load setup dialog.
        /// These map the input layer's fields to the five <c>lut_osmm_habitat_xref</c> lookup columns
        /// (<c>make</c>, <c>desc_group</c>, <c>desc_term</c>, <c>theme</c>, <c>feat_code</c>).
        /// </param>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal async Task<(bool success, int featureCount, int incidCount)> OSMMLoadAsync(OsmmFieldMapping fieldMapping)
        {
            _fieldMapping = fieldMapping;

            // Must have a map selection.
            if (_viewModelMain.GisSelection == null || _viewModelMain.GisSelection.Rows.Count == 0)
            {
                _viewModelMain.ShowWarning("Cannot load: Nothing is selected on the map.", MessageCategory.OSMMLoad);
                return (false, 0, 0);
            }

            // All selected features must have a null or empty INCID (i.e. new, unregistered features).
            if (_viewModelMain.IncidsSelectedMap == null ||
                !_viewModelMain.IncidsSelectedMap.All(i => string.IsNullOrEmpty(i)))
            {
                _viewModelMain.ShowWarning("Cannot load: One or more selected features already have an INCID assigned.", MessageCategory.OSMMLoad);
                return (false, 0, 0);
            }

            return await PerformLoadAsync();
        }

        /// <summary>
        /// Performs the OSMM load: registers each selected null-INCID feature under its own new INCID,
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
                Name = "OSMM Load GIS Features"
            };

            bool gisExecuted = false;

            try
            {
                // ---------------------------------------------------------------
                // 1. Get selected OIDs from the OSMM source layer chosen by the user.
                // ---------------------------------------------------------------
                string sourceLayerName = _fieldMapping?.LayerName ?? _viewModelMain.ActiveLayerName;

                IReadOnlyList<long> selectedOids = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    var selection = ArcGIS.Desktop.Mapping.MapView.Active?.Map
                        ?.GetLayersAsFlattenedList()
                        .OfType<ArcGIS.Desktop.Mapping.FeatureLayer>()
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
                // 2b. Read habitat attributes from the HLU GIS layer.
                // ---------------------------------------------------------------
                Dictionary<long, (string habprimary, string habsecond, string determqty, string interpqty)> insertAttribs =
                    await _viewModelMain.GISApplication.ReadInsertAttributesAsync(selectedOids);

                // ---------------------------------------------------------------
                // 3. Build OID ? (incid, fragid, ...) assignment map.
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
                Dictionary<long, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> oidAssignments = [];
                List<(string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> mmRowsToCreate = [];

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

                    oidAssignments[oid] = (newIncid, FirstFragId, validHabprimary, habsecondMM, validDetermqty, validInterpqty);
                    mmRowsToCreate.Add((newIncid, FirstFragId, resolvedHabprimary, habsecondMM, a.determqty, a.interpqty));
                }

                incidCount = orderedOids.Count;
                featureCount = orderedOids.Count;

                // ---------------------------------------------------------------
                // 4. Insert incid_mm_* shadow rows.
                // ---------------------------------------------------------------
                foreach (var (incid, fragid, habprimary, habsecond, determqty, interpqty) in mmRowsToCreate)
                    CreateMMRow(incid, fragid, habprimary, habsecond, determqty, interpqty);

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
                    $"OSMM Load failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    _viewModelMain.IncidRowCount(true);
                    await _viewModelMain.ClearFilterAsync(false);
                    _viewModelMain.RefillIncidTable = true;
                    await _viewModelMain.GetMapSelectionAsync(false);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return (success, featureCount, incidCount);
        }

        #endregion Load

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
        private void CreateMMRow(string incid, string fragid,
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
                string Norm(object v) =>
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
