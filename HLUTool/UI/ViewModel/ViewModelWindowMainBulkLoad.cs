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
using ArcGIS.Core.Geometry;
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
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Implements the Bulk Load operation.
    ///
    /// <para><b>Load</b> treats each selected new (null-INCID) feature exactly as the feature-insert
    /// workflow does: it creates one new INCID per feature (or one shared INCID for all features)
    /// and registers the features in the shadow map-match table. Habitat attributes carried on
    /// the GIS features are used to populate the new INCID records. A history entry is written
    /// for every registered feature.</para>
    /// </summary>
    internal class ViewModelWindowMainBulkLoad
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

        /// <summary>
        /// Constructs a new instance of <see cref="ViewModelWindowMainBulkLoad"/> with the
        /// specified parent <see cref="ViewModelWindowMain"/>.
        /// </summary>
        /// <param name="viewModelMain"></param>
        public ViewModelWindowMainBulkLoad(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

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
                // Check if the output feature class already exists.
                bool outputExists = await ArcGISProHelpers.FeatureClassExistsAsync(
                    _outputWorkspace, _outputFeatureClassName);

                // If it exists, attempt to delete it. If deletion fails, show a warning and abort the load.
                if (outputExists)
                {
                    // Set the full output path for deletion.
                    string fullOutputPath = System.IO.Path.Combine(
                        _outputWorkspace, _outputFeatureClassName);

                    // Delete the existing feature class. If deletion fails, show a warning and abort the load.
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

            // Perform the bulk load operation.
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
            // Set the source layer name from the field mapping or use the active layer if not specified.
            string sourceLayerName = _fieldMapping?.LayerName
                ?? _viewModelMain.ActiveLayerName;

            // Get the geometry type of the active layer for validation.
            HluGeometryTypes activeGeometryType = _viewModelMain.GISApplication.HluGeometryType;

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

            // If no features are selected, return false to indicate that the load should not proceed.
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
                ViewModelWindowMainBulkHelpers.BuildXrefCache(_viewModelMain);

            // Helper function to normalize string values for grouping: trims whitespace and
            // converts null/empty to empty string.
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
                    comparer: new ViewModelWindowMainBulkHelpers.TupleOrdinalIgnoreCaseComparer())
                .OrderBy(g => g.Key.Item1, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item2, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item3, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item4, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Item5, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    bool matched = xrefCache.TryGetValue(
                        (g.Key.Item1, g.Key.Item2, g.Key.Item3,
                         g.Key.Item4, g.Key.Item5),
                        out var xref);

                    // Validate primary and secondary codes against geometry type.
                    bool isPrimaryValid = true;
                    bool areSecondariesValid = true;

                    // If matched, check the primary and secondary habitat codes for validity
                    // against the active geometry type.
                    if (matched)
                    {
                        // If the primary habitat code is not null or empty, validate it against the
                        // geometry type.
                        if (!string.IsNullOrEmpty(xref.habprimary))
                            isPrimaryValid = ViewModelWindowMainBulkHelpers.IsHabitatCodeValidForGeometryType(
                                _viewModelMain, xref.habprimary, activeGeometryType, true);

                        // If the secondary habitat codes are not null or empty, split them by the
                        // delimiter and validate each code against the geometry type.
                        if (!string.IsNullOrEmpty(xref.habsecond))
                        {
                            string[] secondaryCodes = xref.habsecond.Split(
                                [_viewModelMain.SecondaryCodeDelimiter],
                                StringSplitOptions.RemoveEmptyEntries);

                            areSecondariesValid = secondaryCodes.All(code =>
                                ViewModelWindowMainBulkHelpers.IsHabitatCodeValidForGeometryType(
                                    _viewModelMain, code.Trim(), activeGeometryType, false));
                        }
                    }

                    // Return a new preview row with the grouped attribute values, count, match
                    // status, and validity flags.
                    return new OsmmXrefPreviewRow
                    {
                        Make = g.Key.Item1,
                        DescGroup = g.Key.Item2,
                        DescTerm = g.Key.Item3,
                        Theme = g.Key.Item4,
                        FeatCode = g.Key.Item5,
                        Count = g.Count(),
                        HabitatPrimary = matched ? xref.habprimary : null,
                        HabitatSecondaries = matched ? xref.habsecond : null,
                        IsMatched = matched,
                        IsPrimaryValid = isPrimaryValid,
                        AreSecondariesValid = areSecondariesValid
                    };
                })
                .ToList();

            // Build and show the modal preview window.
            bool userProceeded = false;

            // Create the preview view model with the grouped rows and set its display name.
            ViewModelWindowOSMMXrefPreview previewVm = new(previewRows)
            {
                DisplayName = "OSMM Attribute Preview"
            };

            // Create the preview window, set its owner, startup location, topmost property, and data context.
            WindowOSMMXrefPreview previewWindow = new()
            {
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true,
                DataContext = previewVm
            };

            // Define the event handler for the RequestClose event of the preview view model.
            void OnPreviewRequestClose(bool proceed)
            {
                previewVm.RequestClose -= OnPreviewRequestClose;
                userProceeded = proceed;
                previewWindow.Close();
            }

            previewVm.RequestClose -= OnPreviewRequestClose; // avoid double subscription
            previewVm.RequestClose += OnPreviewRequestClose;

            // Show the preview window as a modal dialog and wait for user interaction.
            previewWindow.ShowDialog();

            // Return whether the user chose to proceed with the bulk load operation.
            return userProceeded;
        }

        /// <summary>
        /// Performs the Bulk load: registers each selected null-INCID feature under its own new INCID,
        /// mirroring the separate-INCID variant of the feature-insert workflow but using
        /// <see cref="Operations.OSMMLoad"/> as the history operation code.
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        private async Task<(bool success, int featureCount, int incidCount)> PerformLoadAsync()
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Loading ...");

            int featureCount = 0;
            int incidCount = 0;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            bool loadSuccess = false;
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

                // If no features are selected, throw an exception to indicate that the load cannot proceed.
                if (selectedOids.Count == 0)
                {
                    throw new HLUToolException($"No features are selected in layer '{sourceLayerName}'.");
                }

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
                    ViewModelWindowMainBulkHelpers.BuildXrefCache(_viewModelMain);

                List<long> orderedOids = [.. selectedOids.OrderBy(o => o)];
                List<(string incid, string toid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> mmRowsToCreate = [];

                // Loop through each selected OID, resolve its habitat attributes, and assign a new
                // INCID and fragment ID.
                int assignmentCount = 0;
                foreach (long oid in orderedOids)
                {
                    // Get the next available INCID from the main view model.
                    string newIncid = _viewModelMain.NextIncid;

                    // Get the habitat attributes for this OID from the insertAttribs dictionary.
                    insertAttribs.TryGetValue(oid, out var a);

                    // Attempt to resolve primary/secondary from the xref lookup.
                    string resolvedHabprimary = a.habprimary;
                    string resolvedHabsecond = a.habsecond;

                    // If the OSMM attributes are available for this OID, look them up in the xref
                    // cache to resolve primary/secondary habitat codes.
                    if (osmmAttribs != null && osmmAttribs.TryGetValue(oid, out var ox))
                    {
                        var lookupKey = (
                            ox.make ?? string.Empty,
                            ox.descGroup ?? string.Empty,
                            ox.descTerm ?? string.Empty,
                            ox.theme ?? string.Empty,
                            ox.featCode ?? string.Empty);

                        // If the lookup key exists in the xref cache, use the resolved
                        // primary/secondary habitat codes.
                        if (xrefCache.TryGetValue(lookupKey, out var xref))
                        {
                            if (!string.IsNullOrEmpty(xref.habprimary))
                                resolvedHabprimary = xref.habprimary;
                            if (!string.IsNullOrEmpty(xref.habsecond))
                                resolvedHabsecond = xref.habsecond;
                        }
                    }

                    // Get the list of valid secondary codes from the resolved secondary string.
                    List<string> validSecondaryCodes = ResolveValidSecondaryCodes(resolvedHabsecond);

                    // Set the parameters for the new INCID and fragment row, including the resolved
                    // primary/secondary habitat codes and quality codes.
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

                    // Create the new INCID row and the associated secondary rows in the shadow tables.
                    CreateIncidRow(newIncid, nowDtTm, resolvedHabprimary, a.determqty, a.interpqty, validSecondaryCodes);

                    // Create the secondary rows in the shadow table for the valid secondary codes.
                    CreateSecondaryRows(newIncid, validSecondaryCodes);

                    // Get the TOID value for this OID from the toidByOid dictionary, if available.
                    toidByOid.TryGetValue(oid, out string sourceToid);

                    // Assign the new INCID and fragment ID, along with the resolved habitat and
                    // quality codes, to the oidAssignments dictionary.
                    oidAssignments[oid] = (newIncid, FirstFragId, validHabprimary, habsecondMM, validDetermqty, validInterpqty);

                    // Add the new INCID and associated data to the mmRowsToCreate list for later
                    // insertion into the shadow map-match table.
                    mmRowsToCreate.Add((newIncid, sourceToid, FirstFragId, resolvedHabprimary, habsecondMM, a.determqty, a.interpqty));

                    // Increment the assignment count for progress tracking.
                    assignmentCount++;
                }

                // Get the total number of INCIDs and features processed for reporting.
                incidCount = orderedOids.Count;
                featureCount = orderedOids.Count;

                // ---------------------------------------------------------------
                // 4. Insert incid_mm_* shadow rows.
                // ---------------------------------------------------------------
                foreach (var (incid, toid, fragid, habprimary, habsecond, determqty, interpqty) in mmRowsToCreate)
                    CreateMMRow(incid, toid, fragid, habprimary, habsecond, determqty, interpqty);

                // ---------------------------------------------------------------
                // 5. Create the staging output (this populates the HLU fields).
                //    The OSMM source layer is read-only, so we skip GIS edits
                //    and write directly to the staging output instead.
                // ---------------------------------------------------------------
                if (string.IsNullOrEmpty(_outputWorkspace) ||
                    string.IsNullOrEmpty(_outputFeatureClassName))
                {
                    throw new HLUToolException(
                        "Bulk load requires an output workspace and feature class name for the staging layer.");
                }

                // Get the full output path for the staging layer (folder for shapefile, .gdb path
                // for geodatabase).
                string fullOutputPath = System.IO.Path.Combine(
                    _outputWorkspace, _outputFeatureClassName);

                _viewModelMain.ChangeCursor(Cursors.Wait, "Creating staging output layer ...");

                // Create the staging output layer with the assigned INCID, fragment ID, and
                // resolved habitat attributes.
                bool created = await CreateStagingOutputAsync(
                    sourceLayerName, fullOutputPath, oidAssignments, toidByOid);

                // If the staging output layer could not be created, throw an exception to indicate failure.
                if (!created)
                    throw new HLUToolException(
                        $"The staging output layer could not be created at:\n\n{fullOutputPath}");

                // ---------------------------------------------------------------
                // 6. Build history from the staging output layer.
                //    The staging output only has the 7 HLU fields (incid, toid, fragid,
                //    habprimary, habsecond, determqty, interpqty) plus geometry.
                //    For bulk load history, we only need these fields — HistoryWrite will
                //    handle filling in the standard history metadata fields.
                // ---------------------------------------------------------------

                // Build a minimal history table with only the fields that exist in the staging output.
                DataTable historyTable = new();

                // Add only the HLU fields that we know exist in the staging output.
                string[] stagingFields = ["incid", "toid", "fragid", "habprimary", "habsecond", "determqty", "interpqty"];
                foreach (string fieldName in stagingFields)
                {
                    historyTable.Columns.Add(new DataColumn(fieldName, typeof(string)));
                }

                // Add geometry history columns.
                historyTable.Columns.Add(new DataColumn(ViewModelWindowMain.HistoryGeometry1ColumnName, typeof(double)));
                historyTable.Columns.Add(new DataColumn(ViewModelWindowMain.HistoryGeometry2ColumnName, typeof(double)));

                // Read the staging output to build history rows.
                string stagingLayerName = System.IO.Path.GetFileNameWithoutExtension(_outputFeatureClassName);
                bool isShapefile = fullOutputPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);

                // Use QueuedTask.Run to read the staging output feature class and build history rows.
                await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    try
                    {
                        string dir = System.IO.Path.GetDirectoryName(fullOutputPath);
                        string name = System.IO.Path.GetFileNameWithoutExtension(fullOutputPath);

                        // Open the staging output feature class (shapefile or geodatabase) and read its features.
                        FeatureClass fc;
                        if (isShapefile)
                        {
                            // Open shapefile from folder - use full path including .shp extension
                            var folderConnection = new FileSystemConnectionPath(
                                new Uri(dir), FileSystemDatastoreType.Shapefile);
                            using var folderDatastore = new FileSystemDatastore(folderConnection);
                            // For shapefiles, include the .shp extension in the dataset name
                            fc = folderDatastore.OpenDataset<FeatureClass>(name + ".shp");
                        }
                        else
                        {
                            // Open feature class from geodatabase.
                            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(dir)));
                            fc = gdb.OpenDataset<FeatureClass>(name);
                        }

                        using (fc)
                        {
                            // Get the feature class definition to build a field index map for the staging fields.
                            using var def = fc.GetDefinition();

                            // Build field index map for staging fields.
                            Dictionary<string, int> fieldIndexMap = [];
                            foreach (string fieldName in stagingFields)
                            {
                                int idx = def.FindField(fieldName);
                                if (idx >= 0)
                                    fieldIndexMap[fieldName] = idx;
                            }

                            // Read ALL features from staging output (no OID filter needed).
                            QueryFilter qf = new();

                            // Create a row cursor to iterate through the features in the staging
                            // output feature class.
                            using RowCursor cursor = fc.Search(qf, false);

                            // Loop through each feature in the staging output and build a
                            // corresponding history row.
                            while (cursor.MoveNext())
                            {
                                // Get the current feature from the cursor. If it's null, skip to the next iteration.
                                using Feature feature = cursor.Current as Feature;
                                if (feature == null)
                                    continue;

                                // Create a new DataRow for the history table.
                                DataRow historyRow = historyTable.NewRow();

                                // Read only the fields that exist in the staging output.
                                foreach (string fieldName in stagingFields)
                                {
                                    // If the field exists in the staging output, get its value; otherwise, set it to DBNull.
                                    if (fieldIndexMap.TryGetValue(fieldName, out int idx))
                                    {
                                        object val = feature[idx];
                                        // Convert empty strings back to DBNull for proper history storage.
                                        historyRow[fieldName] = (val != null && val.ToString() == string.Empty) ? DBNull.Value : val;
                                    }
                                    else
                                    {
                                        historyRow[fieldName] = DBNull.Value;
                                    }
                                }

                                // Get geometry history values.
                                Geometry geom = feature.GetShape();
                                (double geom1, double geom2) = ViewModelWindowMainBulkHelpers.GetGeometryHistoryValues(geom);
                                historyRow[ViewModelWindowMain.HistoryGeometry1ColumnName] = geom1;
                                historyRow[ViewModelWindowMain.HistoryGeometry2ColumnName] = geom2;

                                // Add the history row to the history table.
                                historyTable.Rows.Add(historyRow);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new HLUToolException("Error building history from staging output: " + ex.Message, ex);
                    }
                });

                // If no history rows were built, throw an exception to indicate failure.
                if (historyTable == null || historyTable.Rows.Count == 0)
                {
                    throw new HLUToolException("No history rows were built from staging output.");
                }

                // ---------------------------------------------------------------
                // 7. Write history — one entry per INCID.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                // Set the parameters for the history write operation: fixed values, new history
                // records, operation code, and timestamp.
                const string OidColumnName = "oid";
                DataTable historyForWrite = historyTable.Copy();
                if (historyForWrite.Columns.Contains(OidColumnName))
                    historyForWrite.Columns.Remove(OidColumnName);
                string incidColName = _viewModelMain.HluDataset.history.incidColumn.ColumnName;
                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                // Determine the correct column name for INCID in the history table, accounting for
                // possible "modified_" prefix.
                string histIncidColName = historyForWrite.Columns.Contains(incidColName)
                    ? incidColName
                    : historyForWrite.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                // If the INCID column is found, group the history rows by INCID and write history for each group.
                if (histIncidColName != null)
                {
                    var groups = historyForWrite.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    // Loop through each group of history rows for a unique INCID and write them to
                    // the history table.
                    int groupCount = 0;
                    foreach (var grp in groups)
                    {
                        // Get the unique INCID for this group and increment the group count.
                        string groupIncid = grp.Key;
                        groupCount++;

                        // Clone the history table structure and import the rows for this group into a new DataTable.
                        DataTable groupTable = historyForWrite.Clone();
                        foreach (DataRow r in grp)
                            groupTable.ImportRow(r);

                        // Build fixed values - only include INCID.
                        // Reason and Process will be taken from the current ribbon values by HistoryWrite.
                        // Only the Operation is set to OSMMLoad (which maps to "OL" in lut_operation).
                        Dictionary<int, string> fixedValues = new()
                        {
                            { incidColOrdinal, groupIncid }
                        };

                        // Write the history for this group of rows, catching any exceptions and rethrowing them after logging.
                        try
                        {
                            vmHist.HistoryWrite(fixedValues, groupTable, Operations.OSMMLoad, nowDtTm);
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null)
                            {
                            }
                            throw;
                        }
                    }
                }
                else
                {
                    // No fixed values needed - reason and process will come from the ribbon.
                    // Only the Operation is set to OSMMLoad (which maps to "OL" in lut_operation).
                    vmHist.HistoryWrite(null, historyForWrite, Operations.OSMMLoad, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 8. Commit.
                // ---------------------------------------------------------------
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                // Indicate that the load was successful.
                loadSuccess = true;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"Bulk Load failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Bulk Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // If the load was successful, update the main view model to reflect the new data:
                // refresh the INCID row count, clear any filters, refill the INCID table, and get
                // the current map selection.
                if (loadSuccess)
                {
                    // Update the INCID row count in the main view model to reflect the new data.
                    _viewModelMain.IncidRowCount(true);

                    // Clear any filters in the main view model to ensure that all data is visible.
                    await _viewModelMain.ClearFilterAsync(false);

                    // Set the RefillIncidTable flag to true to indicate that the INCID table should be refilled.
                    _viewModelMain.RefillIncidTable = true;

                    // Get the current map selection to update the main view model's selection state.
                    await _viewModelMain.GetMapSelectionAsync(false);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            // Return a tuple indicating whether the load was successful, along with the feature count and INCID count.
            return (loadSuccess, featureCount, incidCount);
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
        /// <para><b>Note on Shapefile Nullable Fields:</b></para>
        /// <para>
        /// All HLU fields are created with <c>fieldIsNullable: true</c> to match the live HLU layer schema.
        /// For file geodatabases, this creates truly nullable fields. However, for shapefiles, the nullable
        /// parameter is <b>ignored</b> because the DBF format does not support true NULL values for text fields.
        /// Shapefiles use empty strings to represent missing values instead, which is why Step 3 writes empty
        /// strings rather than <c>null</c> or <c>DBNull.Value</c> for shapefile text fields.
        /// </para>
        /// </summary>
        /// <param name="sourceLayerName">The name of the source layer from which to create the staging output.</param>
        /// <param name="fullOutputPath">The full path to the output feature class (including .shp for shapefiles).</param>
        /// <param name="oidAssignments">A dictionary mapping OIDs to their corresponding HLU attribute values.</param>
        /// <param name="toidByOid">An optional dictionary mapping OIDs to TOIDs.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the staging output was successfully created.</returns>
        private async Task<bool> CreateStagingOutputAsync(
            string sourceLayerName,
            string fullOutputPath,
            Dictionary<long, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> oidAssignments,
            Dictionary<long, string> toidByOid = null)
        {
            // If any of the required parameters are null or empty, return false to indicate failure.
            if (string.IsNullOrEmpty(sourceLayerName) || string.IsNullOrEmpty(fullOutputPath) ||
                oidAssignments == null || oidAssignments.Count == 0)
            {
                return false;
            }

            // Validate that the output directory exists and is writable before attempting to copy features.
            string outputDir = System.IO.Path.GetDirectoryName(fullOutputPath);

            // If the output directory is null, empty, or does not exist, return false to indicate failure.
            if (string.IsNullOrEmpty(outputDir) || !System.IO.Directory.Exists(outputDir))
            {
                return false;
            }

            // Step 1: Copy the selected OSMM source features with controlled HLU schema.
            // Use FeatureClassToFeatureClass with field mapping to ensure:
            // - HLU fields have data model lengths (especially toid = 20 chars)
            // - Fields are in data model order
            // - Shapefile geometry fields (Shape_Leng, Shape_Area) are included
            bool isShapefile = fullOutputPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);

            // Use the polygon MM table as the canonical source for field metadata (field
            // names/lengths are identical across all geometry types).
            DataTable mmTable = (DataTable)_viewModelMain.HluDataset.incid_mm_polygons;

            // Create the staging feature class by copying the source layer and applying the HLU
            // field schema.
            bool copied = await CreateHluStagingFeatureClassAsync(sourceLayerName, fullOutputPath, isShapefile, mmTable);

            // If the copy operation failed, throw an exception to indicate that the staging feature
            // class could not be created.
            if (!copied)
            {
                throw new HLUToolException("Failed to create staging feature class.");
            }

            // Step 1a: Read the field names already present in the copied feature class so that
            // Step 2 can skip AddField for any field that was carried over from the source layer
            // (e.g. 'toid'). management.AddField raises ERROR 000012 if the field already exists.
            HashSet<string> existingFieldNames = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
            {
                try
                {
                    // Get the directory and name of the output feature class to open it for reading.
                    string dir = System.IO.Path.GetDirectoryName(fullOutputPath);
                    string name = System.IO.Path.GetFileNameWithoutExtension(fullOutputPath);

                    // If the directory is null or does not exist, return an empty set of field names.
                    if (dir == null || !System.IO.Directory.Exists(dir))
                    {
                        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    // Open the copied feature class (shapefile or geodatabase) and read its field
                    // names into a HashSet.
                    FeatureClass fc;
                    if (isShapefile)
                    {
                        // Open shapefile from folder - use full path including .shp extension
                        var folderConnection = new ArcGIS.Core.Data.FileSystemConnectionPath(
                            new Uri(dir), ArcGIS.Core.Data.FileSystemDatastoreType.Shapefile);
                        using var folderDatastore = new ArcGIS.Core.Data.FileSystemDatastore(folderConnection);
                        // For shapefiles, include the .shp extension in the dataset name
                        fc = folderDatastore.OpenDataset<ArcGIS.Core.Data.FeatureClass>(name + ".shp");
                    }
                    else
                    {
                        // Open feature class from geodatabase.
                        using var gdb = new ArcGIS.Core.Data.Geodatabase(
                            new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(dir)));
                        fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(name);
                    }

                    // Read the field names from the feature class definition and return them as a HashSet.
                    using (fc)
                    {
                        var fieldNames = new HashSet<string>(
                            fc.GetDefinition().GetFields().Select(f => f.Name),
                            StringComparer.OrdinalIgnoreCase);

                        return fieldNames;
                    }
                }
                catch
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            });

            // Step 2: Add the seven mandatory HLU attribute fields, skipping any that already
            // exist in the copied feature class to avoid GP ERROR 000012.
            // All fields are explicitly created as nullable to match the live HLU layer schema.
            bool ok = true;
            if (!existingFieldNames.Contains("incid"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "incid", fieldLength: 12, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("toid"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "toid", fieldLength: 20, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("fragid"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "fragid", fieldLength: 5, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("habprimary"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "habprimary", fieldLength: 8, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("habsecond"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "habsecond", fieldLength: 80, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("determqty"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "determqty", fieldLength: 2, fieldIsNullable: true);
            }
            if (!existingFieldNames.Contains("interpqty"))
            {
                ok &= await ArcGISProHelpers.AddFieldAsync(fullOutputPath, "interpqty", fieldLength: 2, fieldIsNullable: true);
            }

            // If any of the AddField operations failed, return false to indicate that the staging
            // output could not be created.
            if (!ok)
            {
                return false;
            }

            // Step 2b: Build a mapping from source TOID to assignment data, since the copied
            // output features have new OIDs (1, 2, 3, ...) not the original source OIDs.
            Dictionary<string, (string incid, string fragid, string habprimary, string habsecond, string determqty, string interpqty)> assignmentByToid = [];

            // If the toidByOid dictionary is provided, populate the assignmentByToid dictionary by
            // looking up the TOID for each source OID and mapping it to the corresponding
            // assignment data.
            if (toidByOid != null)
            {
                // Loop through each OID assignment and map it to the corresponding TOID in the
                // assignmentByToid dictionary.
                foreach (var kvp in oidAssignments)
                {
                    // Get the source OID from the key of the kvp (key-value pair).
                    long sourceOid = kvp.Key;
                    if (toidByOid.TryGetValue(sourceOid, out string toid) && !string.IsNullOrEmpty(toid))
                    {
                        assignmentByToid[toid] = kvp.Value;
                    }
                }
            }

            // Step 3: Populate the HLU fields on each feature by TOID and collect non-HLU field names for cleanup.
            // The set of HLU field names that must be retained in the staging layer.
            HashSet<string> hluFieldNames = new(StringComparer.OrdinalIgnoreCase)
            {
                "incid", "toid", "fragid", "habprimary", "habsecond", "determqty", "interpqty"
            };

            // Identify non-HLU fields to delete after populating (exclude system/geometry fields).
            List<string> fieldsToDelete = [];
            bool updated = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
            {
                try
                {
                    // Get the directory and name of the output feature class to open it for reading
                    // and updating.
                    string dir = System.IO.Path.GetDirectoryName(fullOutputPath);
                    string name = System.IO.Path.GetFileNameWithoutExtension(fullOutputPath);

                    // If the directory is null or does not exist, return false to indicate failure.
                    if (dir == null || !System.IO.Directory.Exists(dir))
                    {
                        return false;
                    }

                    // Open the copied feature class (shapefile or geodatabase) and update its HLU
                    // fields based on the assignmentByToid mapping.
                    FeatureClass fc;
                    if (isShapefile)
                    {
                        // Open shapefile from folder - use full path including .shp extension
                        var folderConnection = new FileSystemConnectionPath(
                            new Uri(dir), FileSystemDatastoreType.Shapefile);
                        using var folderDatastore = new FileSystemDatastore(folderConnection);
                        // For shapefiles, include the .shp extension in the dataset name
                        fc = folderDatastore.OpenDataset<FeatureClass>(name + ".shp");
                    }
                    else
                    {
                        // Open feature class from geodatabase.
                        using var gdb = new Geodatabase(
                            new FileGeodatabaseConnectionPath(new Uri(dir)));
                        fc = gdb.OpenDataset<FeatureClass>(name);
                    }

                    using (fc)
                    {
                        // Get the feature class definition to identify non-HLU fields for deletion after populating.
                        TableDefinition def = fc.GetDefinition();
                        IReadOnlyList<Field> allFields = def.GetFields();

                        // Identify non-HLU fields to delete after populating (exclude system/geometry fields).
                        HashSet<string> systemFields = new(StringComparer.OrdinalIgnoreCase)
                        {
                            "FID", "OBJECTID", "Shape", "SHAPE",
                            "Shape_Length", "SHAPE_Length", "Shape_Area", "SHAPE_Area",
                            "GlobalID", "GLOBALID"
                        };

                        // Loop through all fields in the feature class definition and add any
                        // non-HLU, non-system fields to the fieldsToDelete list.
                        foreach (var f in allFields)
                        {
                            if (!systemFields.Contains(f.Name) && !hluFieldNames.Contains(f.Name))
                                fieldsToDelete.Add(f.Name);
                        }

                        // Cache field indices.
                        int idxIncid = def.FindField("incid");
                        int idxToid = def.FindField("toid");
                        int idxFragid = def.FindField("fragid");
                        int idxHabprimary = def.FindField("habprimary");
                        int idxHabsecond = def.FindField("habsecond");
                        int idxDetermqty = def.FindField("determqty");
                        int idxInterpqty = def.FindField("interpqty");

                        // Query ALL features (no OID filter) since the copied output has new OIDs
                        QueryFilter qf = new();

                        // Get the total feature count for progress tracking (optional).
                        long totalFeatures = fc.GetCount();

                        // Create a row cursor to iterate through the features in the staging output feature class.
                        using RowCursor cursor = fc.Search(qf, false);

                        // Loop through each feature in the staging output and populate the HLU fields based on the assignmentByToid mapping.
                        int rowsUpdated = 0;
                        while (cursor.MoveNext())
                        {
                            // Get the current row and oid from the cursor
                            using Row row = cursor.Current;
                            long oid = row.GetObjectID();

                            // Look up assignment by TOID (if available)
                            string featureToid = null;
                            if (idxToid >= 0 && row[idxToid] != null && row[idxToid] != DBNull.Value)
                                featureToid = row[idxToid].ToString();

                            // If the feature's TOID is null or not found in the assignmentByToid
                            // mapping, skip to the next feature.
                            if (string.IsNullOrEmpty(featureToid) || !assignmentByToid.TryGetValue(featureToid, out var a))
                                continue;

                            // Write empty strings for empty values (shapefiles don't support true NULL for text fields)
                            if (idxIncid >= 0)
                                row[idxIncid] = a.incid ?? string.Empty;
                            if (idxFragid >= 0) // toid is already present from the copy, don't overwrite
                                row[idxFragid] = a.fragid ?? string.Empty;
                            if (idxHabprimary >= 0)
                                row[idxHabprimary] = a.habprimary ?? string.Empty;
                            if (idxHabsecond >= 0)
                                row[idxHabsecond] = a.habsecond ?? string.Empty;
                            if (idxDetermqty >= 0)
                                row[idxDetermqty] = a.determqty ?? string.Empty;
                            if (idxInterpqty >= 0)
                                row[idxInterpqty] = a.interpqty ?? string.Empty;

                            // Store the updated row back to the feature class and increment the rowsUpdated counter.
                            row.Store();
                            rowsUpdated++;
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            });

            // If the update operation failed, return false to indicate that the staging output
            // could not be created.
            if (!updated)
            {
                return false;
            }

            // Step 4: Remove non-HLU fields from the staging layer now that the HLU values are written.
            if (fieldsToDelete.Count > 0)
            {
                string dropList = string.Join(";", fieldsToDelete);
                // Best-effort: ignore failure — a warning would appear from the GP tool itself.
                bool deleted = await ArcGISProHelpers.DeleteFieldAsync(fullOutputPath, dropList);
            }

            // Step 5: Add the completed staging layer to the map.
            bool addedToMap = await ArcGISProHelpers.AddFeatureLayerToMapAsync(fullOutputPath);

            return addedToMap;
        }

        /// <summary>
        /// Creates a staging feature class by copying features from a source layer and then adding
        /// HLU fields with correct lengths from the data model. This approach avoids field mapping
        /// issues when the source layer doesn't already have the HLU fields.
        /// </summary>
        /// <param name="sourceLayerName">The name of the source layer in the map to copy from.</param>
        /// <param name="fullOutputPath">The full path to the output feature class (including .shp for shapefiles).</param>
        /// <param name="isShapefile">True if the output is a shapefile; false for geodatabase.</param>
        /// <param name="mmTable">The MM table to extract field metadata from (typically incid_mm_polygons).</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if successful.</returns>
        /// <exception cref="Exception">Thrown when the staging feature class creation fails.</exception>
        private static async Task<bool> CreateHluStagingFeatureClassAsync(string sourceLayerName, string fullOutputPath, bool isShapefile, DataTable mmTable)
        {
            // If the source layer name or output path is null or empty, throw an exception to
            // indicate invalid parameters.
            if (string.IsNullOrEmpty(sourceLayerName) || string.IsNullOrEmpty(fullOutputPath))
            {
                throw new ArgumentException("Invalid parameters: source layer name or output path is empty.");
            }

            // Get the output directory from the full output path and validate that it exists.
            string outputDir = System.IO.Path.GetDirectoryName(fullOutputPath);

            // If the output directory is null, empty, or does not exist, throw an exception to
            // indicate that the staging feature class cannot be created.
            if (string.IsNullOrEmpty(outputDir) || !System.IO.Directory.Exists(outputDir))
            {
                throw new ArgumentException($"Output directory does not exist: {outputDir}");
            }

            try
            {
                // Step 1: Copy features from source layer using CopyFeatures (works with map layers).
                bool copied = await ArcGISProHelpers.CopyFeaturesAsync(
                    sourceLayerName,
                    fullOutputPath,
                    addToMap: false);

                // If the copy operation failed, throw an exception to indicate that the staging
                // feature class could not be created.
                if (!copied)
                {
                    throw new Exception("Failed to copy features from source layer.");
                }

                // Step 2: Add the HLU fields that don't exist in the source (all except toid, which may already exist).

                // Define the HLU fields in data model order using column metadata (skip toid since it may exist).
                string[] hluFieldNames = ["incid", "fragid", "habprimary", "habsecond", "determqty", "interpqty"];

                // Loop through each HLU field name and add it to the staging feature class with the
                // correct length and nullable property.
                foreach (string fieldName in hluFieldNames)
                {
                    // Get the DataColumn from the MM table to retrieve the field length and other metadata.
                    DataColumn col = mmTable.Columns[fieldName];

                    // If the column exists, get its length and add the field to the staging feature class.
                    if (col != null)
                    {
                        int length = col.MaxLength > 0 ? col.MaxLength : 0;

                        // Add the field to the staging feature class with the specified length and
                        // nullable property.
                        bool added = await ArcGISProHelpers.AddFieldAsync(fullOutputPath, fieldName, fieldLength: length, fieldIsNullable: true);
                        if (!added)
                        {
                            throw new Exception($"Failed to add field '{fieldName}' to staging layer.");
                        }
                    }
                }

                // For shapefiles, recalculate the geometry fields to populate Shape_Leng and Shape_Area.
                if (isShapefile)
                {
                    // Calculate geometry attributes for shapefiles to populate Shape_Leng and
                    // Shape_Area fields.
                    bool calcSuccess = await ArcGISProHelpers.CalculateGeometryAttributesAsync(
                        fullOutputPath,
                        "Shape_Leng LENGTH_GEODESIC;Shape_Area AREA_GEODESIC",
                        "",  // length_unit (default)
                        "",  // area_unit (default)
                        ""); // coordinate_system (default)

                    // Non-fatal: geometry fields may not be critical for validation.
                    if (!calcSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine("CalculateGeometryAttributes failed (non-fatal).");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                }

                string errorMsg = $"Exception creating staging layer: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $"\n\nInner exception: {ex.InnerException.Message}";
                }

                throw new Exception(errorMsg, ex);
            }
        }

        #endregion Helpers — staging output

        #region Helpers — data model operations

        /// <summary>
        /// Resolves the valid secondary habitat codes for a given raw secondary code string,
        /// filtering to codes that are valid for the active layer geometry type.
        /// </summary>
        /// <param name="habsecond">
        /// The raw secondary habitat code string, potentially containing multiple codes separated
        /// by a delimiter.
        /// </param>
        private List<string> ResolveValidSecondaryCodes(string habsecond)
        {
            // If the input string is null or empty, return null to indicate no valid codes.
            if (string.IsNullOrEmpty(habsecond))
                return null;

            // Get the delimiter for splitting the secondary codes from the main view model.
            string delimiter = _viewModelMain.SecondaryCodeDelimiter;

            // Split the raw secondary code string into individual codes, removing empty entries.
            string[] rawCodes = habsecond.Split(
                [delimiter],
                StringSplitOptions.RemoveEmptyEntries);

            // Filter the raw codes to include only those that are non-empty, non-whitespace, and
            // exist in the list of valid secondary habitat codes for the active layer geometry
            // type. Trim whitespace from each code and return a distinct list of valid codes,
            // ignoring case.
            return [.. rawCodes
                .Where(c => !string.IsNullOrWhiteSpace(c) &&
                            _viewModelMain.SecondaryHabitatCodesAll?.Any(s => s.code == c) == true)
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Creates a minimal <c>incid</c> row in the database for the supplied INCID string.
        /// </summary>
        /// <param name="newIncid">The new INCID string to insert.</param>
        /// <param name="nowDtTm">The current date and time.</param>
        /// <param name="habprimary">The primary habitat code.</param>
        /// <param name="determqty">The determination quality code.</param>
        /// <param name="interpqty">The interpretation quality code.</param>
        /// <param name="validSecondaryCodes">The list of valid secondary habitat codes.</param>
        private void CreateIncidRow(string newIncid, DateTime nowDtTm,
            string habprimary = null, string determqty = null, string interpqty = null,
            List<string> validSecondaryCodes = null)
        {
            // Create a new row in the incid table and populate its fields with the provided values.
            HluDataSet.incidRow newRow = _viewModelMain.IncidTable.NewincidRow();

            // Set the mandatory fields for the new row, including the new INCID, habitat version,
            newRow.incid = newIncid;
            newRow.habitat_version = "0";
            newRow.boundary_base_map = "UK";
            newRow.digitisation_base_map = "UK";
            newRow.created_date = nowDtTm;
            newRow.created_user_id = _viewModelMain.UserID;
            newRow.last_modified_date = nowDtTm;
            newRow.last_modified_user_id = _viewModelMain.UserID;

            // If a valid primary habitat code is provided, set the primary habitat, habitat class,
            // and habitat version fields.
            if (!string.IsNullOrEmpty(habprimary) &&
                _viewModelMain.IsPrimaryValidForLayerType(habprimary))
            {
                newRow.habitat_primary = habprimary;
                newRow.habitat_class = _viewModelMain.GetHabitatClassForPrimary(habprimary) ?? "UKHab";
                newRow.habitat_version = _viewModelMain.GetHabitatVersionForPrimary(habprimary) ?? "0";

                // If valid secondary habitat codes are provided, join them into a single string
                // using the specified delimiter and set the habitat_secondaries field.
                if (validSecondaryCodes != null && validSecondaryCodes.Count > 0)
                    newRow.habitat_secondaries = string.Join(
                        _viewModelMain.SecondaryCodeDelimiter, validSecondaryCodes);
            }

            // If a valid determination quality code is provided, set the quality_determination field.
            if (!string.IsNullOrEmpty(determqty) &&
                _viewModelMain.HluDataset.lut_quality_determination.Any(r => r.code == determqty))
                newRow.quality_determination = determqty;

            // If a valid interpretation quality code is provided, set the quality_interpretation field.
            if (!string.IsNullOrEmpty(interpqty) &&
                _viewModelMain.HluDataset.lut_quality_interpretation.Any(r => r.code == interpqty))
                newRow.quality_interpretation = interpqty;

            // Add the new row to the incid table in the main view model and attempt to update the database.
            _viewModelMain.IncidTable.AddincidRow(newRow);

            // Update the database with the new row. If the update fails (returns -1), throw an exception.
            if (_viewModelMain.HluTableAdapterManager.incidTableAdapter.Update(
                    _viewModelMain.HluDataset.incid) == -1)
                throw new Exception($"Failed to insert row into table [{_viewModelMain.HluDataset.incid.TableName}].");

            // Reject changes in the incid table to clear any pending changes and ensure the table
            // reflects the current state of the database.
            _viewModelMain.IncidTable.RejectChanges();
        }

        /// <summary>
        /// Creates the <c>incid_secondary</c> rows for a new INCID.
        /// </summary>
        /// <param name="newIncid">The new INCID string for which to create secondary rows.</param>
        /// <param name="validSecondaryCodes">A list of valid secondary habitat codes to insert.</param>
        private void CreateSecondaryRows(string newIncid, List<string> validSecondaryCodes)
        {
            // If there are no valid secondary codes provided, return early without creating any rows.
            if (validSecondaryCodes == null || validSecondaryCodes.Count == 0)
                return;

            var table = _viewModelMain.HluDataset.incid_secondary;

            // Loop through each valid secondary code and create a new row in the incid_secondary table.
            foreach (string code in validSecondaryCodes)
            {
                HluDataSet.incid_secondaryRow row = table.Newincid_secondaryRow();
                row.incid = newIncid;
                row.secondary_id = _viewModelMain.RecIDs.NextIncidSecondaryId;
                row.secondary = code;
                table.Addincid_secondaryRow(row);
            }

            // Update the database with the new secondary rows. If the update fails (returns -1),
            // throw an exception.
            if (_viewModelMain.HluTableAdapterManager.incid_secondaryTableAdapter?.Update(table) == -1)
                throw new Exception($"Failed to insert rows into table [{table.TableName}].");

            // Reject changes in the incid_secondary table to clear any pending changes and ensure
            // the table reflects the current state of the database.
            table.RejectChanges();
        }

        /// <summary>
        /// Creates one row in the appropriate <c>incid_mm_*</c> shadow table for the active
        /// GIS layer geometry type, then INSERTs it into the database.
        /// </summary>
        /// <param name="incid">The INCID string for the new row.</param>
        /// <param name="toid">The TOID string for the new row.</param>
        /// <param name="fragid">The FRAGID string for the new row.</param>
        /// <param name="habprimary">The primary habitat code for the new row.</param>
        /// <param name="habsecond">The secondary habitat code for the new row.</param>
        /// <param name="determqty">The determination quality code for the new row.</param>
        /// <param name="interpqty">The interpretation quality code for the new row.</param>
        private void CreateMMRow(string incid, string toid, string fragid,
            string habprimary = null, string habsecond = null,
            string determqty = null, string interpqty = null)
        {
            // Create a new row in the appropriate incid_mm_* table based on the active GIS layer
            // geometry type.
            switch (_viewModelMain.GisLayerType)
            {
                case HluGeometryTypes.Line:
                    {
                        var table = _viewModelMain.HluDataset.incid_mm_lines;
                        HluDataSet.incid_mm_linesRow row = table.Newincid_mm_linesRow();
                        row.incid = incid;
                        if (!string.IsNullOrEmpty(toid))
                            row.toid = toid;
                        row.fragid = fragid;
                        if (!string.IsNullOrEmpty(habprimary))
                            row.habprimary = habprimary;
                        if (!string.IsNullOrEmpty(habsecond))
                            row.habsecond = habsecond;
                        if (!string.IsNullOrEmpty(determqty))
                            row.determqty = determqty;
                        if (!string.IsNullOrEmpty(interpqty))
                            row.interpqty = interpqty;
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
                        if (!string.IsNullOrEmpty(toid))
                            row.toid = toid;
                        row.fragid = fragid;
                        if (!string.IsNullOrEmpty(habprimary))
                            row.habprimary = habprimary;
                        if (!string.IsNullOrEmpty(habsecond))
                            row.habsecond = habsecond;
                        if (!string.IsNullOrEmpty(determqty))
                            row.determqty = determqty;
                        if (!string.IsNullOrEmpty(interpqty))
                            row.interpqty = interpqty;
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
                        if (!string.IsNullOrEmpty(toid))
                            row.toid = toid;
                        row.fragid = fragid;
                        if (!string.IsNullOrEmpty(habprimary))
                            row.habprimary = habprimary;
                        if (!string.IsNullOrEmpty(habsecond))
                            row.habsecond = habsecond;
                        if (!string.IsNullOrEmpty(determqty))
                            row.determqty = determqty;
                        if (!string.IsNullOrEmpty(interpqty))
                            row.interpqty = interpqty;
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

        #endregion Helpers — data model operations
    }
}