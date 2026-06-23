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
using ArcGIS.Desktop.Mapping;
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
    /// View model for registering newly created GIS features (features with a null INCID)
    /// against new database records.  Supports two modes:
    /// <list type="bullet">
    ///   <item>All selected features ? a single new INCID (same-INCID insert).</item>
    ///   <item>Each selected feature ? its own new INCID (separate-INCID insert).</item>
    /// </list>
    /// </summary>
    internal class ViewModelWindowMainFeatureInsert
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        /// <summary>Width of the formatted fragment ID strings (e.g. 5 ? "00001").</summary>
        private const int FragIdWidth = 5;

        /// <summary>First fragment ID string for a brand-new INCID.</summary>
        private static readonly string FirstFragId = "1".PadLeft(FragIdWidth, '0');

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of <see cref="ViewModelWindowMainFeatureInsert"/>.
        /// </summary>
        public ViewModelWindowMainFeatureInsert(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Public Entry Points

        /// <summary>
        /// Registers all currently selected new (null-INCID) features under a single new INCID,
        /// assigning each a unique sequential fragment ID.
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal Task<(bool success, int featureCount, int incidCount)> InsertFeaturesSameIncidAsync() =>
            PerformInsertAsync(sameIncid: true);

        /// <summary>
        /// Registers each currently selected new (null-INCID) feature under its own new INCID,
        /// assigning every feature fragment ID "00001".
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal Task<(bool success, int featureCount, int incidCount)> InsertFeaturesSeparateIncidsAsync() =>
            PerformInsertAsync(sameIncid: false);

        #endregion Public Entry Points

        #region Core Logic

        /// <summary>
        /// Core insert logic shared by both entry points.
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        private async Task<(bool success, int featureCount, int incidCount)> PerformInsertAsync(bool sameIncid)
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Inserting features ...");
            bool success = false;
            int featureCount = 0;
            int incidCount = 0;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = sameIncid ? "Insert Features — Same INCID" : "Insert Features — Separate INCIDs"
            };

            bool gisExecuted = false;

            try
            {
                // ---------------------------------------------------------------
                // 1. Get the selected OIDs from the active map selection.
                //    Must run on the Queued Task thread.
                // ---------------------------------------------------------------
                IReadOnlyList<long> selectedOids = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    var selection = MapView.Active?.Map
                        ?.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(l => l.Name == _viewModelMain.ActiveLayerName)
                        ?.GetSelection();

                    return (IReadOnlyList<long>)(selection?.GetObjectIDs() ?? []);
                });

                if (selectedOids.Count == 0)
                    throw new HLUToolException("No features are selected in the active layer.");

                // ---------------------------------------------------------------
                // 2. Read the four habitat attributes from the GIS layer.
                //     Any field absent from the layer yields null for that OID.
                // ---------------------------------------------------------------
                Dictionary<long, (string habprimary, string habsecond, string determqty, string interpqty)> insertAttribs =
                    await _viewModelMain.GISApplication.ReadInsertAttributesAsync(selectedOids);

                // For a same-INCID insert all features must carry identical attribute values
                // so that the single new INCID record is unambiguous.
                if (sameIncid && insertAttribs.Count > 1)
                {
                    // Get the attribute values for the first OID (arbitrary).
                    var (habprimary, habsecond, determqty, interpqty) = insertAttribs.Values.First();

                    // Compare the attributes of all OIDs against the first; string equality is
                    // sufficient since values are stored as codes.
                    bool allMatch = insertAttribs.Values.All(a =>
                        string.Equals(a.habprimary, habprimary, StringComparison.Ordinal) &&
                        string.Equals(a.habsecond,  habsecond,  StringComparison.Ordinal) &&
                        string.Equals(a.determqty,  determqty,  StringComparison.Ordinal) &&
                        string.Equals(a.interpqty,  interpqty,  StringComparison.Ordinal));

                    // If any attribute differs between features, the insert cannot proceed in same-INCID mode.
                    if (!allMatch)
                        throw new HLUToolException(
                            "The selected features have different habitat attributes " +
                            "(habprimary, habsecond, determqty, interpqty). " +
                            "Features with different attributes cannot be combined into a single INCID. " +
                            "Please use 'Insert as Separate INCIDs' or correct the attribute values.");
                }

                // ---------------------------------------------------------------
                // 3. Build the OID ? (incid, fragid) assignment map.
                // ---------------------------------------------------------------
                // Round timestamp to whole seconds to avoid rounding differences.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day,
                    currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                List<long> orderedOids = [.. selectedOids.OrderBy(o => o)];
                Dictionary<long, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> oidAssignments = [];

                // Tracks all (incid, fragid, habprimary, habsecond, determqty, interpqty) tuples that need mm rows, in OID order.
                List<(string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> mmRowsToCreate = [];

                if (sameIncid)
                {
                    // All features share one new INCID; each gets a sequential fragid.

                    // Get the next INCID value from the view model; this does not increment the counter yet.
                    string newIncid = _viewModelMain.NextIncid;

                    // Get the habitat attributes for the first OID (arbitrary, since all must match) or default to nulls.
                    var (habprimary, habsecond, determqty, interpqty) = insertAttribs.Count > 0 ? insertAttribs.Values.First() : default;

                    // Resolve the valid secondary codes for the shared habsecond value (filtered
                    // to codes that are valid for the geometry type of the active layer).
                    List<string> validSecondaryCodes = ResolveValidSecondaryCodes(habsecond);

                    // Build the habsecond summary string (secondary codes only, delimiter-separated)
                    // that is stored in the mm shadow table.
                    string delimiter = _viewModelMain.SecondaryCodeDelimiter;
                    string habsecondMM = (validSecondaryCodes != null && validSecondaryCodes.Count > 0)
                        ? string.Join(delimiter, validSecondaryCodes)
                        : null;

                    // Compute validated values to write back to the GIS layer.  Invalid codes
                    // (wrong geometry type, not in lookup table) are replaced with null so the
                    // GIS field is cleared rather than left with a stale/invalid value.
                    string validHabprimary = (!string.IsNullOrEmpty(habprimary) &&
                        _viewModelMain.IsPrimaryValidForLayerType(habprimary)) ? habprimary : null;
                    string validDetermqty  = (!string.IsNullOrEmpty(determqty) &&
                        _viewModelMain.HluDataset.lut_quality_determination.Any(r => r.code == determqty)) ? determqty : null;
                    string validInterpqty  = (!string.IsNullOrEmpty(interpqty) &&
                        _viewModelMain.HluDataset.lut_quality_interpretation.Any(r => r.code == interpqty)) ? interpqty : null;

                    // Create the single new INCID row in the database.
                    CreateIncidRow(newIncid, nowDtTm, habprimary, determqty, interpqty, validSecondaryCodes);

                    // Create the associated secondary rows for the shared habsecond value.
                    CreateSecondaryRows(newIncid, validSecondaryCodes);

                    // Loop through the OIDs in ascending order, assigning each the same INCID and a
                    // sequential fragid.
                    int fragNum = 1;
                    foreach (long oid in orderedOids)
                    {
                        string fragid = fragNum.ToString().PadLeft(FragIdWidth, '0');
                        oidAssignments[oid] = (newIncid, fragid, validHabprimary, habsecondMM, validDetermqty, validInterpqty);
                        mmRowsToCreate.Add((newIncid, fragid, habprimary, habsecondMM, determqty, interpqty));
                        fragNum++;
                    }

                    // Counts: one INCID for all features, and one fragment per feature.
                    incidCount = 1;
                    featureCount = orderedOids.Count;
                }
                else
                {
                    // Each feature gets its own new INCID, all with fragment "00001".
                    foreach (long oid in orderedOids)
                    {
                        // Get the next INCID value from the view model; this does not increment the counter yet.
                        string newIncid = _viewModelMain.NextIncid;

                        // Get the habitat attributes for the OID.
                        insertAttribs.TryGetValue(oid, out var a);

                        // Resolve the valid secondary codes for this feature (filtered
                        // to codes that are valid for the geometry type of the active layer).
                        List<string> validSecondaryCodes = ResolveValidSecondaryCodes(a.habsecond);

                        // Build the habsecond summary string (secondary codes only, delimiter-separated)
                        // that is stored in the mm shadow table.
                        string delimiter = _viewModelMain.SecondaryCodeDelimiter;
                        string habsecondMM = (validSecondaryCodes != null && validSecondaryCodes.Count > 0)
                            ? string.Join(delimiter, validSecondaryCodes)
                            : null;

                        // Compute validated values to write back to the GIS layer.  Invalid codes
                        // (wrong geometry type, not in lookup table) are replaced with null so the
                        // GIS field is cleared rather than left with a stale/invalid value.
                        string validHabprimary = (!string.IsNullOrEmpty(a.habprimary) &&
                            _viewModelMain.IsPrimaryValidForLayerType(a.habprimary)) ? a.habprimary : null;
                        string validDetermqty  = (!string.IsNullOrEmpty(a.determqty) &&
                            _viewModelMain.HluDataset.lut_quality_determination.Any(r => r.code == a.determqty)) ? a.determqty : null;
                        string validInterpqty  = (!string.IsNullOrEmpty(a.interpqty) &&
                            _viewModelMain.HluDataset.lut_quality_interpretation.Any(r => r.code == a.interpqty)) ? a.interpqty : null;

                        // Create the single new INCID row in the database.
                        CreateIncidRow(newIncid, nowDtTm, a.habprimary, a.determqty, a.interpqty, validSecondaryCodes);

                        // Create the associated secondary rows for the habsecond value.
                        CreateSecondaryRows(newIncid, validSecondaryCodes);

                        oidAssignments[oid] = (newIncid, FirstFragId, validHabprimary, habsecondMM, validDetermqty, validInterpqty);
                        mmRowsToCreate.Add((newIncid, FirstFragId, a.habprimary, habsecondMM, a.determqty, a.interpqty));
                    }

                    // Counts: one INCID per feature, and one fragment per feature.
                    incidCount = orderedOids.Count;
                    featureCount = orderedOids.Count;
                }

                // ---------------------------------------------------------------
                // 4. Insert incid_mm_* rows into the database shadow copy.
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
                    throw new HLUToolException("No GIS features were updated during the insert.");

                // ---------------------------------------------------------------
                // 6. Execute the queued GIS edits.
                // ---------------------------------------------------------------
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (String.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";
                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");

                gisExecuted = true;

                // ---------------------------------------------------------------
                // 7. Write history — one call per unique INCID.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                // Remove the "oid" helper column before passing to HistoryWrite.
                const string OidColumnName = "oid";
                DataTable historyForWrite = historyTable.Copy();
                if (historyForWrite.Columns.Contains(OidColumnName))
                    historyForWrite.Columns.Remove(OidColumnName);

                // Group history rows by the incid column so HistoryWrite gets the right fixedValues.
                string incidColName = _viewModelMain.HluDataset.history.incidColumn.ColumnName;
                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                // Find the incid column in the history table (may be named "incid" or "modified_incid").
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

                        vmHist.HistoryWrite(fixedValues, groupTable, Operations.FeatureInsert, nowDtTm);
                    }
                }
                else
                {
                    // Fallback: write all rows without a per-incid fixedValue (history will be incomplete).
                    vmHist.HistoryWrite(null, historyForWrite, Operations.FeatureInsert, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 8. Commit and tidy up.
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
                    $"Feature insert failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Insert Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    // Recount and refresh so the new INCIDs appear in the UI.
                    _viewModelMain.IncidRowCount(true);

                    // Clear the filter first (without moving to first record).
                    await _viewModelMain.ClearFilterAsync(false);

                    // Re-read the map selection to pick up the newly registered features.
                    await _viewModelMain.GetMapSelectionAsync(false);

                    // Request the incid table to be refilled on next access.
                    // This must come AFTER GetMapSelectionAsync to avoid blocking.
                    _viewModelMain.RefillIncidTable = true;
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return (success, featureCount, incidCount);
        }

        #endregion Core Logic

        #region Helpers

        /// <summary>
        /// Creates a minimal <c>incid</c> row in the database for the supplied INCID string,
        /// setting the required audit fields and mandatory defaults.
        /// <c>habitat_version</c> is set to "0" (no primary code assigned yet) and
        /// <c>boundary_base_map</c>/<c>digitisation_base_map</c> default to "UK" (unknown).
        /// The row is rejected from the in-memory DataTable immediately after the DB INSERT
        /// so that normal navigation is unaffected.
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

            // Apply GIS layer attributes when present and valid for the active layer geometry type;
            // invalid or geometry-incompatible values are ignored.
            if (!string.IsNullOrEmpty(habprimary) &&
                _viewModelMain.IsPrimaryValidForLayerType(habprimary))
            {
                newRow.habitat_primary = habprimary;
                newRow.habitat_class = _viewModelMain.GetHabitatClassForPrimary(habprimary) ?? "UKHab";
                newRow.habitat_version = _viewModelMain.GetHabitatVersionForPrimary(habprimary) ?? "0";

                // Build habitat_secondaries: secondary codes only, delimiter-separated
                // (e.g. "200.516.823") — the primary code is NOT included.
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

            // Discard the added row from in-memory; it was persisted to the DB above.
            _viewModelMain.IncidTable.RejectChanges();
        }

        /// <summary>
        /// Creates one row in the appropriate <c>incid_mm_*</c> shadow table for the
        /// geometry type of the active GIS layer, then INSERTs it into the database.
        /// The row is rejected from the in-memory DataTable afterwards.
        /// Habitat attribute columns are set from the supplied values; shape dimension
        /// columns are left as <see cref="DBNull"/> — they are populated when the GIS
        /// layer geometry is written.
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
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
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
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
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

                default: // Polygon
                {
                    var table = _viewModelMain.HluDataset.incid_mm_polygons;
                    HluDataSet.incid_mm_polygonsRow row = table.Newincid_mm_polygonsRow();
                    row.incid = incid;
                    row.fragid = fragid;
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
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

        /// <summary>
        /// Parses the space/comma-separated secondary habitat code string taken from the
        /// active GIS layer's <c>habsecond</c> field and returns the list of valid,
        /// non-duplicate codes that are applicable to the active layer geometry type.
        /// Codes not found in the layer's secondary-habitat lookup are silently ignored.
        /// </summary>
        private List<string> ResolveValidSecondaryCodes(string habsecond)
        {
            if (string.IsNullOrWhiteSpace(habsecond))
                return [];

            // Split on spaces, commas, and full-stops — same delimiters used by
            // ViewModelWinQuerySecondaries_RequestClose.
            string[] rawCodes = [.. habsecond
                .Split([' ', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()];

            List<string> validCodes = [];
            foreach (string code in rawCodes)
            {
                // Accept only codes present in the geometry-type-filtered secondary lookup.
                if (_viewModelMain.SecondaryHabitatCodesAll?.Any(s => s.code == code) == true)
                    validCodes.Add(code);
            }

            return validCodes;
        }

        /// <summary>
        /// Inserts one <c>incid_secondary</c> database row for each code in
        /// <paramref name="validCodes"/> (pre-validated by <see cref="ResolveValidSecondaryCodes"/>).
        /// The row is persisted immediately via the table adapter.
        /// </summary>
        private void CreateSecondaryRows(string incid, List<string> validCodes)
        {
            if (validCodes == null || validCodes.Count == 0)
                return;

            _viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter ??=
                new HluTableAdapter<HluDataSet.incid_secondaryDataTable, HluDataSet.incid_secondaryRow>(
                    _viewModelMain.DataBase);

            foreach (string code in validCodes)
            {
                string group = _viewModelMain.SecondaryHabitatCodesAll
                    .First(s => s.code == code)
                    .code_group;

                HluDataSet.incid_secondaryRow newRow =
                    _viewModelMain.IncidSecondaryTable.Newincid_secondaryRow();
                newRow.secondary_id = _viewModelMain.RecIDs.NextIncidSecondaryId;
                newRow.incid = incid;
                newRow.secondary = code;
                newRow.secondary_group = group;

                _viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter.Insert(newRow);
            }
        }

        #endregion Helpers
    }
}
