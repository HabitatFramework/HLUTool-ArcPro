// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014, 2018 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
// Copyright © 2019-2022 Greenspace Information for Greater London CIC
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
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Date;
using HLU.GISApplication;
using HLU.Helpers;
using HLU.Properties;
using HLU.UI.View;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommandType = System.Data.CommandType;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the main window export functionality.
    /// </summary>
    partial class ViewModelWindowMainExport
    {
        #region Fields

        public static HluDataSet HluDatasetStatic = null;

        ViewModelWindowMain _viewModelMain;
        private WindowExport _windowExport;
        private ViewModelWindowExport _viewModelExport;

        private string _lastTableName;
        private int _tableCount;
        private int _incidOrdinal;
        private int _conditionIdOrdinal;
        private int _conditionDateStartOrdinal;
        private int _conditionDateEndOrdinal;
        private int _conditionDateTypeOrdinal;
        private int _matrixIdOrdinal;
        private int _formationIdOrdinal;
        private int _managementIdOrdinal;
        private int _complexIdOrdinal;
        private int _bapIdOrdinal;
        private int _bapTypeOrdinal;
        private int _bapQualityOrdinal;
        private int _sourceIdOrdinal;
        private List<int> _sourceDateStartOrdinals = [];
        private List<int> _sourceDateEndOrdinals = [];
        private List<int> _sourceDateTypeOrdinals = [];
        private int _attributesLength;

        /// <summary>
        /// Cache for vague date calculations to improve performance by avoiding redundant calculations for the same condition/source IDs.
        /// </summary>
        private Dictionary<int, DateTime> _vagueDateCache = [];

        /// <summary>
        /// Number of records to process in each batch when processing a selection that exceeds the maximum SQL length.
        /// </summary>
        private int _batchSize = Settings.Default.BatchProcessingSize;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowMainExport class with a reference to the main view model.
        /// </summary>
        /// <param name="viewModelMain"></param>
        public ViewModelWindowMainExport(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Initiate Export

        /// <summary>
        /// Displays the export dialog, allowing the user to select and initiate an export operation based on available
        /// export formats.
        /// </summary>
        /// <remarks>If no export formats with defined export fields are available, the method displays a
        /// message and does not proceed with the export dialog. The method is asynchronous but returns void, so
        /// exceptions may not be observed by callers. This method is intended to be called from the UI
        /// thread.</remarks>
        public async void InitiateExport()
        {
            // Create the export window.
            _windowExport = new WindowExport
            {
                // Set ArcGIS Pro as the parent
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Fill all export formats if there are any export fields
            // defined for the export format.
            _viewModelMain.HluTableAdapterManager.exportsTableAdapter.ClearBeforeFill = true;
            _viewModelMain.HluTableAdapterManager.exportsTableAdapter.Fill(_viewModelMain.HluDataset.exports,
                String.Format("EXISTS (SELECT {0}.{1} FROM {0} WHERE {0}.{1} = {2}.{3})",
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.export_idColumn.ColumnName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports.export_idColumn.ColumnName)));

            // If there are no exports formats defined that have any
            // export fields then exit.
            if (_viewModelMain.HluDataset.exports.Count == 0)
            {
                MessageBox.Show("Cannot export: There are no export formats defined.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // Display the export interface to prompt the user
            // to select which export format they want to use.
            int fragCount = await _viewModelMain.GISApplication.CountMapSelectionAsync();
            long totalCount = await _viewModelMain.GISApplication.CountMapFeaturesAsync();

            //_viewModelExport = new ViewModelExport(_viewModelMain.GisSelection == null ? 0 :
            //_viewModelMain.GisSelection.Rows.Count, _viewModelMain.GISApplication.HluLayerName,
            //_viewModelMain.GISApplication.ApplicationType, _viewModelMain.HluDataset.exports);
            _viewModelExport = new(_viewModelMain.GisSelection == null ? 0 :
                fragCount, totalCount, _viewModelMain.GISApplication.HluLayerName,
                _viewModelMain.HluDataset.exports)
            {
                DisplayName = "Export"
            };

            // Subscribe to the export window request close event.
            _viewModelExport.RequestClose -= _viewModelExport_RequestClose; // Safety: avoid double subscription.
            _viewModelExport.RequestClose += new ViewModelWindowExport.RequestCloseEventHandler(_viewModelExport_RequestClose);

            // Set the data context for the export window.
            _windowExport.DataContext = _viewModelExport;

            // Show the export window.
            _windowExport.ShowDialog();
        }

        #endregion Initiate Export

        #region Export Request Close

        /// <summary>
        /// Handles the RequestClose event of the _viewModelExport control.
        /// </summary>
        /// <param name="exportID"></param>
        /// <param name="selectedOnly"></param>
        private async void _viewModelExport_RequestClose(int exportID, bool selectedOnly)
        {
            _viewModelExport.RequestClose -= _viewModelExport_RequestClose;
            _windowExport.Close();

            string exportPath = Settings.Default.ExportPath;

            // If the user selected an export format then
            // perform the export using that format.
            if (exportID != -1)
            {
                await ProcessExportAsync(exportID, exportPath, selectedOnly);
            }
        }

        #endregion Export Request Close

        #region Export Processing

        /// <summary>
        /// Exports the combined GIS and database data using the specified export format.
        /// </summary>
        /// <param name="userExportId">The export format selected by the user.</param>
        /// <param name="exportPath">The path to export the data to.</param>
        /// <param name="selectedOnly">If set to <c>true</c> export only selected incids/features.</param>
        /// <returns>A task that represents the asynchronous export operation.</returns>
        private async Task ProcessExportAsync(int userExportId, string exportPath, bool selectedOnly)
        {
            // Check parameters.
            if (userExportId == -1)
            {
                MessageBox.Show("No export format was selected.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (string.IsNullOrEmpty(exportPath) || !Directory.Exists(exportPath))
            {
                MessageBox.Show("The export path is not valid.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // Get the working file geodatabase path (created at initialization)
            string workingFileGDBpath = HLUTool.WorkingGdbPath;

            // Check the path to the working file geodatabase
            if (string.IsNullOrEmpty(workingFileGDBpath))
            {
                MessageBox.Show("Working geodatabase is not available. Please restart the tool.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Prompt user for export details.
            var exportDetails = await _viewModelMain.GISApplication.ExportPromptAsync(
                exportPath);

            // If the user didn't provide export details then exit.
            if (exportDetails == default)
                return;

            // Extract the export details.
            string exportWorkspace = exportDetails.outputWorkspace;
            string exportFeatureClassName = exportDetails.outputFeatureClassName;

            // Clean up all tables and feature classes before starting
            await ArcGISProHelpers.CleanupGeodatabaseAsync(workingFileGDBpath);

            // Generate a unique table name for this export attribute table.
            string attributeTableName = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}";

            try
            {
                _viewModelMain.ChangeCursor(Cursors.Wait, "Creating export table ...");

                // Create a new unique table name to export to.
                string tableAlias = GetTableAlias();
                if (tableAlias == null)
                    throw new Exception("Failed to find a table alias that does not match a table name in the HLU dataset");

                // Retrieve the export fields for the export format selected by the user from the database.
                _viewModelMain.HluTableAdapterManager.exports_fieldsTableAdapter.ClearBeforeFill = true;
                _viewModelMain.HluTableAdapterManager.exports_fieldsTableAdapter.Fill(
                    _viewModelMain.HluDataset.exports_fields, String.Format("{0} = {1} ORDER BY {2}, {3}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.export_idColumn.ColumnName),
                    userExportId,
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.table_nameColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.field_ordinalColumn.ColumnName)));

                // Exit if there are no export fields for this format.
                if (_viewModelMain.HluDataset.exports_fields.Count == 0)
                    throw new Exception($"No export fields are defined for format '{_viewModelMain.HluDataset.exports.FindByexport_id(userExportId).export_name}'");

                // Exit if there is no incid field for this format.
                if (!_viewModelMain.HluDataset.exports_fields.Any(f => f.column_name == _viewModelMain.IncidTable.incidColumn.ColumnName))
                    throw new Exception($"The export format '{_viewModelMain.HluDataset.exports.FindByexport_id(userExportId).export_name}' does not contain the column 'incid'");

                // Build a new export data table and determine field mappings.
                DataTable attributeTable;
                int[][] fieldMapTemplate;
                StringBuilder targetList;
                StringBuilder fromClause;
                int[] sortOrdinals;
                int[] conditionOrdinals;
                int[] matrixOrdinals;
                int[] formationOrdinals;
                int[] managementOrdinals;
                int[] complexOrdinals;
                int[] bapOrdinals;
                int[] sourceOrdinals;
                List<ExportField> exportFields = [];
                Dictionary<string, string> tableAliases;

                // Get the name of the active layer.
                string layerName = _viewModelMain.GISApplication.HluLayerName;

                // Get the field names for the GIS layer to check for any naming conflicts with the export table name alias and to help determine field mappings.
                List<string> gisFieldNames = await _viewModelMain.GISApplication.GetFCFieldNamesAsync(layerName);

                // Get the fields for the GIS layer to help determine field mappings and to calculate the total
                // length of the attributes in the export for setting appropriate field lengths in the export table.
                IReadOnlyList<Field> gisFields = await _viewModelMain.GISApplication.GetFCFieldsAsync(layerName);

                // Construct the export table structure and field mappings based on the export fields defined for this export format.
                ExportJoins(tableAlias, gisFieldNames, gisFields, ref exportFields, out attributeTable,
                    out fieldMapTemplate, out targetList, out fromClause, out sortOrdinals, out conditionOrdinals,
                    out matrixOrdinals, out formationOrdinals, out managementOrdinals, out complexOrdinals,
                    out bapOrdinals, out sourceOrdinals, out tableAliases);

                // Check if output is a shapefile (based on file extension)
                bool isShapefile = exportFeatureClassName.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);

                if (isShapefile)
                {
                    // Add the length of the GIS mandatory fields to the total attribute length.
                    _attributesLength += 20;

                    // Check attribute length limit (recommended, not strictly enforced)
                    if (_attributesLength > 4000)
                    {
                        MessageBoxResult result = MessageBox.Show(
                            $"Warning: The total attribute length ({_attributesLength} bytes) exceeds the recommended shapefile limit (4000 bytes).\n\n" +
                            $"This may cause issues with some GIS applications.\n\n" +
                            $"Do you want to continue anyway?",
                            "HLU: Export",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                            return;
                    }
                }

                // Count the number of incids to be exported.
                int rowCount = selectedOnly ? _viewModelMain.SelectedIncidsInGISCount : _viewModelMain.IncidRowCount(false);

                //TODO: Set this in options?
                // Warn the user if the export is very large.
                if (rowCount > 50000)
                {
                    MessageBoxResult userResponse = MessageBox.Show(
                        "This export operation may take some time.\n\nDo you wish to proceed?",
                        "HLU: Export", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (userResponse != MessageBoxResult.Yes)
                        return;
                }

                // Set the export filter conditions.
                List<List<SqlFilterCondition>> exportFilter = null;
                if (selectedOnly)
                {
                    if ((_viewModelMain.IncidSelectionWhereClause == null) &&
                        (_viewModelMain.GisSelection != null) && (_viewModelMain.GisSelection.Rows.Count > 0))
                    {
                        int incidOrd = _viewModelMain.IncidTable.incidColumn.Ordinal;
                        IEnumerable<string> incidsSelected = _viewModelMain.GisSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_viewModelMain.GisSelection.Columns[0].ColumnName))
                            .Select(g => g.Key).OrderBy(s => s);

                        _viewModelMain.IncidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            250, incidOrd, _viewModelMain.IncidTable, incidsSelected);
                        exportFilter = _viewModelMain.IncidSelectionWhereClause;
                    }
                    else
                    {
                        exportFilter = [_viewModelMain.IncidSelectionWhereClause.SelectMany(l => l).ToList()];
                    }
                }
                else
                {
                    // Filter to only INCIDs that exist in the GIS layer
                    _viewModelMain.ChangeCursor(Cursors.Wait, "Filtering from GIS ...");

                    // Get the INCID field name
                    string incidFieldName = await _viewModelMain.GISApplication.IncidFieldNameAsync();

                    // Get distinct INCIDs from the GIS layer
                    HashSet<string> layerIncids = await ArcGISProHelpers.GetDistinctIncidValuesAsync(
                        _viewModelMain.GISApplication.HluFeatureClass,
                        incidFieldName);

                    // If we got layer INCIDs, attempt to filter to only those INCIDs in the export
                    // query to reduce export size and improve performance.
                    if (layerIncids != null && layerIncids.Count > 0)
                    {
                        // Calculate filter ratio
                        int totalIncids = _viewModelMain.IncidRowCount(false);
                        double filterRatio = (double)layerIncids.Count / totalIncids;

                        // Only filter if < 70% of records needed (otherwise overhead not worth it)
                        if (filterRatio < 0.7)
                        {
                            // Build WHERE clause from layer INCIDs
                            int incidOrd = _viewModelMain.IncidTable.incidColumn.Ordinal;

                            exportFilter = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                                250, incidOrd, _viewModelMain.IncidTable, layerIncids.OrderBy(s => s));
                        }
                        else
                        {
                            // Use ALL records - filtering overhead not worth it
                            SqlFilterCondition cond = new("AND",
                                _viewModelMain.IncidTable, _viewModelMain.IncidTable.incidColumn, null)
                            {
                                Operator = "IS NOT NULL"
                            };
                            exportFilter = new List<List<SqlFilterCondition>>([
                                new List<SqlFilterCondition>([cond])]);
                        }
                    }
                    else
                    {
                        // Fallback: export all INCIDs if we couldn't get layer INCIDs
                        SqlFilterCondition cond = new("AND",
                            _viewModelMain.IncidTable, _viewModelMain.IncidTable.incidColumn, null)
                        {
                            Operator = "IS NOT NULL"
                        };
                        exportFilter = new List<List<SqlFilterCondition>>([
                            new List<SqlFilterCondition>([cond])]);
                    }
                }

                // Create export attribute table in the existing working geodatabase.
                if (!await CreateExportTableAsync(workingFileGDBpath, attributeTable, attributeTableName))
                    return;

                _viewModelMain.ChangeCursor(Cursors.Wait, "Exporting to temporary table ...");

                // Export the attribute data to the working geodatabase.
                int exportRowCount = await ExportToTableAsync(workingFileGDBpath, attributeTableName,
                    targetList.ToString(), fromClause.ToString(), exportFilter,
                    _viewModelMain.DataBase, exportFields, attributeTable, sortOrdinals, conditionOrdinals,
                    matrixOrdinals, formationOrdinals, managementOrdinals, complexOrdinals, bapOrdinals,
                    sourceOrdinals, fieldMapTemplate, tableAliases);
                // Exit if no rows were exported.
                if (exportRowCount == 0)
                    return;

                _viewModelMain.ChangeCursor(Cursors.Wait, "Exporting from GIS ...");

                // Extract the list of GIS fields to include from exportFields
                // Use ColumnName (source name in the GIS layer), not FieldName (export name)
                List<string> gisFieldsToInclude = exportFields
                    .Where(f => f.TableName != null && f.TableName.Equals("<gis>", StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.ColumnName)  // Use ColumnName here
                    .Distinct()
                    .ToList();

                // Call the new export method with field filtering and ordering
                bool exportSuccess = await _viewModelMain.GISApplication.ExportWithJoinAsync(
                    _viewModelMain.GISApplication.HluLayerName,  // Source layer path
                    workingFileGDBpath,                          // Working geodatabase path
                    attributeTableName,                          // Attribute table name
                    isShapefile,                                 // Is output a shapefile?
                    exportWorkspace,                             // Output workspace
                    exportFeatureClassName,                      // Output feature class name
                    selectedOnly,                                // Selected features only?
                    gisFieldsToInclude,                          // Filtered GIS fields
                    null,                                        // No field ordering needed
                    null);                                       // No field renaming needed

                if (!exportSuccess)
                {
                    //TODO: Report failure?
                    DispatcherHelper.DoEvents();
                    return;
                }

                // Inform the user of success and that the output has been added to the current map
                MessageBox.Show($"Export successful! {exportRowCount} records were exported.\n\n" +
                    $"The exported data has been added to the current map.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Export failed. The error message was:\n\n{0}",
                    ex.Message), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                //TODO: DEBUG - Commented out
                // Clean up all tables and feature classes after export
                //await ArcGISProHelpers.CleanupGeodatabaseAsync(workingFileGDBpath);

                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }
        }

        #endregion Export

        #region Export to Geodatabase

        /// <summary>
        /// Creates an export table in the existing working file geodatabase.
        /// </summary>
        /// <param name="workingFileGDBName">The full path and name of the existing working file geodatabase.</param>
        /// <param name="exportTable">The DataTable structure for the export.</param>
        /// <param name="exportTableName">The name of the export table to create.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the table was created successfully.</returns>
        private async Task<bool> CreateExportTableAsync(string workingFileGDBName, DataTable exportTable, string exportTableName)
        {
            try
            {
                // Use the existing working geodatabase.
                if (String.IsNullOrEmpty(workingFileGDBName) || !Directory.Exists(workingFileGDBName))
                {
                    throw new Exception("Working file geodatabase is not available.");
                }

                // Create the table structure in the existing geodatabase.
                bool created = await ArcGISProHelpers.CreateTableAsync(workingFileGDBName, exportTableName, exportTable);

                if (!created)
                    throw new Exception($"Failed to create table '{exportTableName}'.");

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Failed to create export table. The error message was:\n\n{0}.",
                    ex.Message), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Exports the attribute data to the working file geodatabase with optimized batch inserts.
        /// </summary>
        /// <param name="gdbName">The full path and name of the file geodatabase.</param>
        /// <param name="tableName">The name of the table to export to.</param>
        /// <param name="targetListStr">The target list string for the SQL query.</param>
        /// <param name="fromClauseStr">The FROM clause string for the SQL query.</param>
        /// <param name="exportFilter">The list of filter conditions for the export.</param>
        /// <param name="dataBase">The database connection to use for the export.</param>
        /// <param name="exportFields">The list of fields to export.</param>
        /// <param name="exportTable">The DataTable to hold the export data.</param>
        /// <param name="sortOrdinals">The array of sort ordinals.</param>
        /// <param name="conditionOrdinals">The array of condition ordinals.</param>
        /// <param name="matrixOrdinals">The array of matrix ordinals.</param>
        /// <param name="formationOrdinals">The array of formation ordinals.</param>
        /// <param name="managementOrdinals">The array of management ordinals.</param>
        /// <param name="complexOrdinals">The array of complex ordinals.</param>
        /// <param name="bapOrdinals">The array of BAP ordinals.</param>
        /// <param name="sourceOrdinals">The array of source ordinals.</param>
        /// <param name="fieldMap">The array of field maps.</param>
        /// <param name="tableAliases"> The dictionary of table aliases.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows exported.</returns>
        private async Task<int> ExportToTableAsync(
            string gdbName,
            string tableName,
            string targetListStr,
            string fromClauseStr,
            List<List<SqlFilterCondition>> exportFilter,
            DbBase dataBase,
            List<ExportField> exportFields,
            DataTable exportTable,
            int[] sortOrdinals,
            int[] conditionOrdinals,
            int[] matrixOrdinals,
            int[] formationOrdinals,
            int[] managementOrdinals,
            int[] complexOrdinals,
            int[] bapOrdinals,
            int[] sourceOrdinals,
            int[][] fieldMap,
            Dictionary<string, string> tableAliases)
        {
            // Reset export row counter.
            int outputRowCount = 0;

            // Clear cache before export to manage memory.
            _vagueDateCache.Clear();

            // Calculate expected field count
            int expectedFieldCount = exportTable.Columns.Count;

            // Cache column metadata to avoid repeated indexer calls
            string[] columnNames = new string[exportTable.Columns.Count];
            int[] maxLengths = new int[exportTable.Columns.Count];
            for (int i = 0; i < exportTable.Columns.Count; i++)
            {
                columnNames[i] = exportTable.Columns[i].ColumnName;
                maxLengths[i] = exportTable.Columns[i].MaxLength;
            }

            try
            {
                // If there is only one filter chunk and it has a large number of conditions, attempt to
                // split it into smaller chunks to avoid exceeding SQL length limits.
                if (exportFilter.Count == 1)
                {
                    try
                    {
                        List<SqlFilterCondition> whereCond = exportFilter[0];

                        // Use simple chunking instead of range optimization
                        // The range optimization may not play well with complex joins
                        exportFilter = whereCond.ChunkClause(240).ToList();
                    }
                    catch
                    {
                        // Ignore chunking failures and proceed with original clause.
                    }
                }

                // Build a fast lookup for ordinals -> whether they are in a group.
                HashSet<int> conditionOrdinalSet = [.. conditionOrdinals ?? []];
                HashSet<int> matrixOrdinalSet = [.. matrixOrdinals ?? []];
                HashSet<int> formationOrdinalSet = [.. formationOrdinals ?? []];
                HashSet<int> managementOrdinalSet = [.. managementOrdinals ?? []];
                HashSet<int> complexOrdinalSet = [.. complexOrdinals ?? []];
                HashSet<int> bapOrdinalSet = [.. bapOrdinals ?? []];
                HashSet<int> sourceOrdinalSet = [.. sourceOrdinals ?? []];

                // Build fast lookup from FieldOrdinal -> ExportField.
                Dictionary<int, ExportField> exportFieldByOrdinal =
                    exportFields
                        .Where(f => f.FieldOrdinal >= 0)
                        .GroupBy(f => f.FieldOrdinal)
                        .ToDictionary(g => g.Key, g => g.First());

                // Limit how many assembled output rows we hold in memory before staging a write batch,
                // and how many batches we hold in memory before flushing to the geodatabase, to manage memory usage.
                int readFlushThreshold = _batchSize;

                // For each filter chunk, run SQL off-MCT, then write to FGDB on-MCT.
                for (int j = 0; j < exportFilter.Count; j++)
                {
                    // Union the constituent parts of the export query together.
                    string sql = ScratchDb.UnionQuery(
                        targetListStr,
                        fromClauseStr,
                        sortOrdinals,
                        exportFilter[j],
                        dataBase,
                        tableAliases);

                    // Execute the export query and get the results as a list of rows
                    List<Dictionary<string, object>> rowsBatch = new(_batchSize);

                    // Use a data reader for efficient forward-only reading of the export query results.
                    IDataReader reader = null;
                    try
                    {
                        reader = _viewModelMain.DataBase.ExecuteReader(
                            sql,
                            _viewModelMain.DbConnectionTimeout,
                            CommandType.Text);

                        if (reader == null)
                            throw new Exception($"Export query returned null (SQL length: {sql.Length} chars, {exportFilter[j].Count} conditions). This may indicate a database error or timeout.");
                    }
                    catch (Exception ex)
                    {
                        string dbError = DbBase.GetSqlErrorMessage(ex);
                        throw new Exception($"Export query failed (chunk {j + 1}/{exportFilter.Count}, SQL length: {sql.Length} chars, {exportFilter[j].Count} conditions).\n\nDatabase error: {dbError}", ex);
                    }

                    using (reader)
                    {
                        string currIncid = string.Empty;
                        string prevIncid = string.Empty;

                        int currConditionId = -1;
                        int currConditionDateStart = 0;
                        int currConditionDateEnd = 0;
                        string currConditionDateType = string.Empty;

                        int currMatrixId = -1;
                        int currFormationId = -1;
                        int currManagementId = -1;
                        int currComplexId = -1;
                        int currBapId = -1;

                        int currSourceId = -1;
                        int currSourceDateStart = 0;
                        int currSourceDateEnd = 0;
                        string currSourceDateType = string.Empty;

                        // Track IDs already used for the current incid.
                        HashSet<int> conditionIds = [];
                        HashSet<int> matrixIds = [];
                        HashSet<int> formationIds = [];
                        HashSet<int> managementIds = [];
                        HashSet<int> complexIds = [];
                        HashSet<int> bapIds = [];
                        HashSet<int> sourceIds = [];

                        // Track current output row (export column name -> value).
                        Dictionary<string, object> currentRow = null;

                        // Set the field map indexes to the start of the array.
                        int[] fieldMapIndex = new int[fieldMap.Length];
                        for (int k = 0; k < fieldMap.Length; k++)
                            fieldMapIndex[k] = 1;

                        while (reader.Read())
                        {
                            // Get the current incid.
                            currIncid = reader.GetString(_incidOrdinal);

                            // Get current IDs from reader. Skip if the ordinal is -1 (i.e. the field is not in the export).
                            if (_conditionIdOrdinal != -1)
                            {
                                object conditionIdValue = reader.GetValue(_conditionIdOrdinal);
                                currConditionId = conditionIdValue != DBNull.Value ? (int)conditionIdValue : -1;
                            }

                            if (_matrixIdOrdinal != -1)
                            {
                                object matrixIdValue = reader.GetValue(_matrixIdOrdinal);
                                currMatrixId = matrixIdValue != DBNull.Value ? (int)matrixIdValue : -1;
                            }

                            if (_formationIdOrdinal != -1)
                            {
                                object formationIdValue = reader.GetValue(_formationIdOrdinal);
                                currFormationId = formationIdValue != DBNull.Value ? (int)formationIdValue : -1;
                            }

                            if (_managementIdOrdinal != -1)
                            {
                                object managementIdValue = reader.GetValue(_managementIdOrdinal);
                                currManagementId = managementIdValue != DBNull.Value ? (int)managementIdValue : -1;
                            }

                            if (_complexIdOrdinal != -1)
                            {
                                object complexIdValue = reader.GetValue(_complexIdOrdinal);
                                currComplexId = complexIdValue != DBNull.Value ? (int)complexIdValue : -1;
                            }

                            if (_bapIdOrdinal != -1)
                            {
                                object bapIdValue = reader.GetValue(_bapIdOrdinal);
                                currBapId = bapIdValue != DBNull.Value ? (int)bapIdValue : -1;
                            }

                            if (_sourceIdOrdinal != -1)
                            {
                                object sourceIdValue = reader.GetValue(_sourceIdOrdinal);
                                currSourceId = sourceIdValue != DBNull.Value ? (int)sourceIdValue : -1;
                            }

                            // Get source dates.
                            if ((_sourceDateStartOrdinals.Count != 0) && !reader.IsDBNull(_sourceDateStartOrdinals[0]))
                                currSourceDateStart = reader.GetInt32(_sourceDateStartOrdinals[0]);

                            if ((_sourceDateEndOrdinals.Count != 0) && !reader.IsDBNull(_sourceDateEndOrdinals[0]))
                                currSourceDateEnd = reader.GetInt32(_sourceDateEndOrdinals[0]);

                            if ((_sourceDateTypeOrdinals.Count != 0) && !reader.IsDBNull(_sourceDateTypeOrdinals[0]))
                                currSourceDateType = reader.GetString(_sourceDateTypeOrdinals[0]);

                            // Get condition dates.
                            if ((_conditionDateStartOrdinal != -1) && !reader.IsDBNull(_conditionDateStartOrdinal))
                                currConditionDateStart = reader.GetInt32(_conditionDateStartOrdinal);

                            if ((_conditionDateEndOrdinal != -1) && !reader.IsDBNull(_conditionDateEndOrdinal))
                                currConditionDateEnd = reader.GetInt32(_conditionDateEndOrdinal);

                            if ((_conditionDateTypeOrdinal != -1) && !reader.IsDBNull(_conditionDateTypeOrdinal))
                                currConditionDateType = reader.GetString(_conditionDateTypeOrdinal);

                            // If this incid is different from the last record's incid.
                            if (!string.Equals(currIncid, prevIncid, StringComparison.Ordinal))
                            {
                                // Save previous row if exists.
                                if (currentRow != null)
                                {
                                    rowsBatch.Add(currentRow);

                                    // Write batch when it reaches the threshold and clear the rows.
                                    if (rowsBatch.Count >= _batchSize)
                                    {
                                        int written = await ArcGISProHelpers.BulkInsertRowsAsync(gdbName, tableName, rowsBatch);
                                        outputRowCount += written;

                                        // Clear and reset the batch
                                        rowsBatch.Clear();
                                    }
                                }

                                prevIncid = currIncid;

                                // Reset per-incid ID sets.
                                conditionIds = [];
                                matrixIds = [];
                                formationIds = [];
                                managementIds = [];
                                complexIds = [];
                                bapIds = [];
                                sourceIds = [];

                                // Reset field map indexes.
                                for (int k = 0; k < fieldMap.Length; k++)
                                    fieldMapIndex[k] = 1;

                                // Create a new output row with pre-allocated capacity.
                                currentRow = new Dictionary<string, object>(expectedFieldCount);

                                // Process all fields.
                                for (int i = 0; i < fieldMap.GetLength(0); i++)
                                {
                                    if (fieldMap[i] == null || fieldMap[i].Length < 2)
                                        continue;

                                    if (fieldMapIndex[i] >= fieldMap[i].Length)
                                        continue;

                                    // Get the export column for this field map index.
                                    int exportColumn = fieldMap[i][fieldMapIndex[i]];

                                    // Increment field map index for this export column.
                                    fieldMapIndex[i] += 1;

                                    if (fieldMap[i][0] == -1)
                                        continue;

                                    object inValue = reader.GetValue(fieldMap[i][0]);
                                    if (inValue == DBNull.Value)
                                        continue;

                                    // Get the ExportField for this ordinal, if it exists.
                                    exportFieldByOrdinal.TryGetValue(i, out ExportField exportField);

                                    // Convert the input value to the output value, applying any necessary formatting.
                                    object outValue = ConvertInput(
                                        fieldMap[i][0],
                                        inValue,
                                        reader.GetFieldType(fieldMap[i][0]),
                                        exportTable.Columns[exportColumn].DataType,
                                        exportField?.FieldFormat,
                                        currSourceDateStart,
                                        currSourceDateEnd,
                                        currSourceDateType,
                                        currConditionDateStart,
                                        currConditionDateEnd,
                                        currConditionDateType);

                                    // If the output value is not null, add it to the current row.
                                    if (outValue != null)
                                    {
                                        if (outValue is string s)
                                        {
                                            if (maxLengths[exportColumn] != -1 && s.Length > maxLengths[exportColumn])
                                                outValue = s.Substring(0, maxLengths[exportColumn]);
                                        }

                                        currentRow[columnNames[exportColumn]] = outValue;
                                    }
                                }
                            }
                            else
                            {
                                // Handle multiple rows for same incid.
                                for (int i = 0; i < fieldMap.GetLength(0); i++)
                                {
                                    if (fieldMap[i] == null || fieldMap[i].Length < 2)
                                        continue;

                                    if (fieldMapIndex[i] >= fieldMap[i].Length)
                                        continue;

                                    // Get the export column for this field map index.
                                    int exportColumn = fieldMap[i][fieldMapIndex[i]];

                                    // Increment field map index for this export column.
                                    fieldMapIndex[i] += 1;

                                    // Check if we should output this field.
                                    if (ShouldSkipDuplicate(exportColumn, currConditionId, conditionOrdinalSet, conditionIds) ||
                                        ShouldSkipDuplicate(exportColumn, currMatrixId, matrixOrdinalSet, matrixIds) ||
                                        ShouldSkipDuplicate(exportColumn, currFormationId, formationOrdinalSet, formationIds) ||
                                        ShouldSkipDuplicate(exportColumn, currManagementId, managementOrdinalSet, managementIds) ||
                                        ShouldSkipDuplicate(exportColumn, currComplexId, complexOrdinalSet, complexIds) ||
                                        ShouldSkipDuplicate(exportColumn, currBapId, bapOrdinalSet, bapIds) ||
                                        ShouldSkipDuplicate(exportColumn, currSourceId, sourceOrdinalSet, sourceIds))
                                        continue;

                                    // Skip if we've exhausted the field map for this column.
                                    if (fieldMap[i][0] == -1)
                                        continue;

                                    // Get the input value.
                                    object inValue = reader.GetValue(fieldMap[i][0]);
                                    if (inValue == DBNull.Value)
                                        continue;

                                    // Get the ExportField for this ordinal, if it exists.
                                    exportFieldByOrdinal.TryGetValue(i, out ExportField exportField);

                                    // Convert the input value to the output value, applying any necessary formatting.
                                    object outValue = ConvertInput(
                                        fieldMap[i][0],
                                        inValue,
                                        reader.GetFieldType(fieldMap[i][0]),
                                        exportTable.Columns[exportColumn].DataType,
                                        exportField?.FieldFormat,
                                        currSourceDateStart,
                                        currSourceDateEnd,
                                        currSourceDateType,
                                        currConditionDateStart,
                                        currConditionDateEnd,
                                        currConditionDateType);

                                    // If the output value is not null, add it to the current row.
                                    if (outValue != null)
                                    {
                                        if (outValue is string s)
                                        {
                                            if (maxLengths[exportColumn] != -1 && s.Length > maxLengths[exportColumn])
                                                outValue = s.Substring(0, maxLengths[exportColumn]);
                                        }

                                        currentRow[columnNames[exportColumn]] = outValue;
                                    }
                                }
                            }

                            // Store current IDs (but do not store -1 / null sentinel IDs).
                            if (currConditionId != -1)
                                conditionIds.Add(currConditionId);

                            if (currMatrixId != -1)
                                matrixIds.Add(currMatrixId);

                            if (currFormationId != -1)
                                formationIds.Add(currFormationId);

                            if (currManagementId != -1)
                                managementIds.Add(currManagementId);

                            if (currComplexId != -1)
                                complexIds.Add(currComplexId);

                            if (currBapId != -1)
                                bapIds.Add(currBapId);

                            if (currSourceId != -1)
                                sourceIds.Add(currSourceId);
                        }

                        // Save last row if exists.
                        if (currentRow != null)
                            rowsBatch.Add(currentRow);

                        // Flush the remainder of the rows to the FGDB.
                        if (rowsBatch.Count > 0)
                        {
                            int written = await ArcGISProHelpers.BulkInsertRowsAsync(gdbName, tableName, rowsBatch);
                            outputRowCount += written;
                        }
                    } // End using (reader)
                }

                if (outputRowCount < 1)
                    throw new Exception("Export query did not retrieve any rows.");

                return outputRowCount;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed. The error message was:\n\n{ex.Message}",
                    "HLU: Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return 0;
            }
        }

        /// <summary>
        /// Converts the input field into the output field, applying any
        /// required formatting as appropriate.
        /// </summary>
        /// <param name="inOrdinal">The input field ordinal.</param>
        /// <param name="inValue">The input field value.</param>
        /// <param name="inType">Data type of the input field.</param>
        /// <param name="outType">Date type of the output field.</param>
        /// <param name="outFormat">The required output field format.</param>
        /// <param name="sourceDateStart">The source date start.</param>
        /// <param name="sourceDateEnd">The source date end.</param>
        /// <param name="sourceDateType">The source date type.</param>
        /// <param name="conditionDateStart">The condition date start.</param>
        /// <param name="conditionDateEnd">The condition date end.</param>
        /// <param name="conditionDateType">The condition date type.</param>
        /// <returns>The converted value, or null if conversion is not possible.</returns>
        private object ConvertInput(int inOrdinal, object inValue, System.Type inType,
            System.Type outType, string outFormat, int sourceDateStart, int sourceDateEnd, string sourceDateType,
            int conditionDateStart, int conditionDateEnd, string conditionDateType)
        {
            // If the output field is a DateTime.
            if (outType == typeof(DateTime))
            {
                // If the input is already a DateTime, return it directly.
                if (inType == typeof(DateTime))
                {
                    return inValue is DateTime ? inValue : null;
                }
                // Otherwise, if the input is an int and the field is a source date,
                // convert from vague date code to DateTime.
                else if (inType == typeof(int) &&
                    (_sourceDateStartOrdinals.Contains(inOrdinal) || _sourceDateEndOrdinals.Contains(inOrdinal)))
                {
                    int inInt = (int)inValue;

                    // Check cache first (early return if found)
                    if (_vagueDateCache.TryGetValue(inInt, out DateTime cached))
                        return cached;

                    // Only create VagueDateInstance if not cached
                    VagueDateInstance vd = new(inInt, inInt, "D");

                    if ((vd == null) || (vd.IsBad) || (vd.IsUnknown))
                        return null;

                    string itemStr = VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                    if (DateTime.TryParseExact(itemStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newCached))
                    {
                        _vagueDateCache[inInt] = newCached;
                        return newCached;
                    }
                    return null;
                }
                // Otherwise, if the input is an int and the field is a condition date,
                // convert from vague date code to DateTime.
                else if (inType == typeof(int) &&
                    (_conditionDateStartOrdinal == inOrdinal || _conditionDateEndOrdinal == inOrdinal))
                {
                    int inInt = (int)inValue;

                    // Check cache first (early return if found)
                    if (_vagueDateCache.TryGetValue(inInt, out DateTime cached))
                        return cached;

                    // Only create VagueDateInstance if not cached
                    VagueDateInstance vd = new(inInt, inInt, "D");

                    if ((vd == null) || (vd.IsBad) || (vd.IsUnknown))
                        return null;

                    string itemStr = VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                    if (DateTime.TryParseExact(itemStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newCached))
                    {
                        _vagueDateCache[inInt] = newCached;
                        return newCached;
                    }
                    return null;
                }
                // Otherwise, attempt a direct conversion to DateTime.
                else
                {
                    string inStr = inValue.ToString();
                    if (DateTime.TryParseExact(inStr, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime inDate))
                        return inDate;
                    else
                        return null;
                }
            }
            // If output is string with a format specification (for date formatting)
            else if ((outType == typeof(string)) && (outFormat != null))
            {
                if (inType == typeof(DateTime))
                {
                    if (inValue is DateTime inDate)
                    {
                        string inStr = inDate.ToString(outFormat);
                        return inStr ?? null;
                    }
                    return null;
                }
                else if (inType == typeof(int) &&
                    (_sourceDateStartOrdinals.Contains(inOrdinal) || _sourceDateEndOrdinals.Contains(inOrdinal)))
                {
                    VagueDateInstance vd = new(sourceDateStart, sourceDateEnd, sourceDateType);

                    if ((vd == null) || (vd.IsBad))
                        return null;
                    if (vd.IsUnknown)
                        return VagueDate.VagueDateTypes.Unknown.ToString();

                    if (outFormat.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                    }
                    else if (String.IsNullOrEmpty(outFormat))
                    {
                        if (_sourceDateStartOrdinals.Contains(inOrdinal))
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Start);
                        else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                        else
                            return null;
                    }
                    else if (VagueDate.FromCode(outFormat) != VagueDate.VagueDateTypes.Unknown)
                    {
                        if (_sourceDateStartOrdinals.Contains(inOrdinal))
                        {
                            string dateType = outFormat.Substring(0, 1);
                            return VagueDate.FromVagueDateInstance(
                                new VagueDateInstance(sourceDateStart, sourceDateEnd, dateType),
                                VagueDate.DateType.Start);
                        }
                        else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                        {
                            vd.DateType = outFormat.Length == 1 ? outFormat + outFormat : outFormat;
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                        }
                        else
                            return null;
                    }
                    else
                    {
                        VagueDate.DateType dateType = VagueDate.DateType.Vague;
                        if (_sourceDateStartOrdinals.Contains(inOrdinal))
                            dateType = VagueDate.DateType.Start;
                        else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                            dateType = VagueDate.DateType.End;

                        string inStr = VagueDate.FromVagueDateInstance(
                            new VagueDateInstance(sourceDateStart, sourceDateEnd, "D"), dateType);

                        if (!DateTime.TryParseExact(inStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime inDate))
                            return null;

                        return inDate.ToString(outFormat);
                    }
                }
                else if (inType == typeof(int) &&
                    (_conditionDateStartOrdinal == inOrdinal || _conditionDateEndOrdinal == inOrdinal))
                {
                    VagueDateInstance vd = new(conditionDateStart, conditionDateEnd, conditionDateType);

                    if ((vd == null) || (vd.IsBad))
                        return null;
                    if (vd.IsUnknown)
                        return VagueDate.VagueDateTypes.Unknown.ToString();

                    if (outFormat.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                    }
                    else if (String.IsNullOrEmpty(outFormat))
                    {
                        if (_conditionDateStartOrdinal == inOrdinal)
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Start);
                        else if (_conditionDateEndOrdinal == inOrdinal)
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                        else
                            return null;
                    }
                    else if (VagueDate.FromCode(outFormat) != VagueDate.VagueDateTypes.Unknown)
                    {
                        if (_conditionDateStartOrdinal == inOrdinal)
                        {
                            string dateType = outFormat.Substring(0, 1);
                            return VagueDate.FromVagueDateInstance(
                                new VagueDateInstance(conditionDateStart, conditionDateEnd, dateType),
                                VagueDate.DateType.Start);
                        }
                        else if (_conditionDateEndOrdinal == inOrdinal)
                        {
                            vd.DateType = outFormat.Length == 1 ? outFormat + outFormat : outFormat;
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                        }
                        else
                            return null;
                    }
                    else
                    {
                        VagueDate.DateType dateType = VagueDate.DateType.Vague;
                        if (_conditionDateStartOrdinal == inOrdinal)
                            dateType = VagueDate.DateType.Start;
                        else if (_conditionDateEndOrdinal == inOrdinal)
                            dateType = VagueDate.DateType.End;

                        string inStr = VagueDate.FromVagueDateInstance(
                            new VagueDateInstance(conditionDateStart, conditionDateEnd, "D"), dateType);

                        if (!DateTime.TryParseExact(inStr, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime inDate))
                            return null;

                        return inDate.ToString(outFormat);
                    }
                }
                else
                {
                    // For non-date fields with a format specified, just return the raw value
                    // The format specification doesn't apply to non-date fields
                    return inValue;
                }
            }
            else
                // No conversion needed - return as-is
                return inValue;
        }

        #endregion Export Processing

        #region Export Joins

        /// <summary>
        /// Constructs the SQL target list and from clause with the necessary joins based on the export fields defined in the dataset.
        /// </summary>
        /// <param name="tableAlias">The alias to use for the main table in the FROM clause.</param>
        /// <param name="gisLayerFields">The list of GIS layer fields.</param>
        /// <param name="gisFields">The list of GIS fields.</param>
        /// <param name="exportFields">The list of export fields.</param>
        /// <param name="exportTable">The DataTable to hold the export data.</param>
        /// <param name="fieldMapTemplate">The field map template for mapping source fields to export fields.</param>
        /// <param name="targetList">The SQL target list for the export query.</param>
        /// <param name="fromClause">The SQL FROM clause for the export query.</param>
        /// <param name="sortOrdinals">The ordinals for sorting fields.</param>
        /// <param name="conditionOrdinals">The ordinals for condition fields.</param>
        /// <param name="matrixOrdinals">The ordinals for matrix fields.</param>
        /// <param name="formationOrdinals">The ordinals for formation fields.</param>
        /// <param name="managementOrdinals">The ordinals for management fields.</param>
        /// <param name="complexOrdinals">The ordinals for complex fields.</param>
        /// <param name="bapOrdinals">The ordinals for BAP fields.</param>
        /// <param name="sourceOrdinals">The ordinals for source fields.</param>
        /// <param name="tableAliases">The dictionary to hold table aliases.</param>
        private void ExportJoins(
            string tableAlias,
            List<string> gisLayerFields,
            IReadOnlyList<Field> gisFields,
            ref List<ExportField> exportFields,
            out DataTable exportTable,
            out int[][] fieldMapTemplate, out StringBuilder targetList, out StringBuilder fromClause,
            out int[] sortOrdinals, out int[] conditionOrdinals,
            out int[] matrixOrdinals, out int[] formationOrdinals,
            out int[] managementOrdinals, out int[] complexOrdinals,
            out int[] bapOrdinals, out int[] sourceOrdinals,
            out Dictionary<string, string> tableAliases)
        {
            // Ensure we have GIS field information
            if (gisLayerFields == null || gisFields == null)
                throw new ArgumentException("GIS layer field information is required for export.");

            // Initialize output structures
            InitializeExportStructures(out exportTable, out targetList, out fromClause,
                out sortOrdinals, out conditionOrdinals, out matrixOrdinals, out formationOrdinals,
                out managementOrdinals, out complexOrdinals, out bapOrdinals, out sourceOrdinals);

            // Build SQL and field mappings
            var context = new ExportJoinContext(tableAlias);
            BuildSqlTargetListAndFromClause(context, ref exportFields);

            // Build field map template
            fieldMapTemplate = BuildFieldMapTemplate(exportFields, context, exportTable);

            // Add any missing required fields for sorting and relationships
            AddRequiredExtraFields(context, exportFields, ref targetList);

            // Finalize sort and field ordinals
            FinalizeSortAndFieldOrdinals(context, out sortOrdinals, out conditionOrdinals,
                out matrixOrdinals, out formationOrdinals, out managementOrdinals,
                out complexOrdinals, out bapOrdinals, out sourceOrdinals);

            // Set primary key
            if (context.PrimaryKeyOrdinal != -1)
                exportTable.PrimaryKey = [exportTable.Columns[context.PrimaryKeyOrdinal]];

            // Output the built SQL components
            targetList = context.TargetList;
            fromClause = context.FromClause;

            // Output the table aliases
            tableAliases = context.TableAliases;

            // Remove leading comma from target list
            if (targetList.Length > 1) targetList.Remove(0, 1);
        }

        /// <summary>
        /// Context object to hold state during export join construction.
        /// </summary>
        private class ExportJoinContext
        {
            public string TableAlias { get; }
            public StringBuilder TargetList { get; }
            public StringBuilder FromClause { get; }
            public List<string> FromList { get; }
            public List<string> LeftJoined { get; }
            public Dictionary<string, string> TableAliases { get; }

            public int TableAliasNum { get; set; }
            public int SqlFieldOrdinal { get; set; }
            public bool FirstJoin { get; set; }

            // Field tracking
            public int PrimaryKeyOrdinal { get; set; }
            public List<int> SortFields { get; }
            public List<int> ConditionFields { get; }
            public List<int> MatrixFields { get; }
            public List<int> FormationFields { get; }
            public List<int> ManagementFields { get; }
            public List<int> ComplexFields { get; }
            public List<int> BapFields { get; }
            public List<int> SourceFields { get; }

            public int SourceSortOrderOrdinal { get; set; }

            /// <summary>
            /// Initializes a new instance of the ExportJoinContext class with the specified main table alias.
            /// </summary>
            /// <param name="tableAlias"></param>
            public ExportJoinContext(string tableAlias)
            {
                TableAlias = tableAlias;
                TargetList = new StringBuilder();
                FromClause = new StringBuilder();
                FromList = [];
                LeftJoined = [];
                TableAliases = [];

                TableAliasNum = 1;
                SqlFieldOrdinal = 0;
                FirstJoin = true;

                PrimaryKeyOrdinal = -1;
                SortFields = [];
                ConditionFields = [];
                MatrixFields = [];
                FormationFields = [];
                ManagementFields = [];
                ComplexFields = [];
                BapFields = [];
                SourceFields = [];

                SourceSortOrderOrdinal = -1;
            }
        }

        /// <summary>
        /// Initializes all output data structures for the export.
        /// </summary>
        /// <param name="exportTable">The DataTable to hold the export data.</param>
        /// <param name="targetList">The SQL target list for the export query.</param>
        /// <param name="fromClause">The SQL FROM clause for the export query.</param>
        /// <param name="sortOrdinals">The ordinals for sorting fields.</param>
        /// <param name="conditionOrdinals">The ordinals for condition fields.</param>
        /// <param name="matrixOrdinals">The ordinals for matrix fields.</param>
        /// <param name="formationOrdinals">The ordinals for formation fields.</param>
        /// <param name="managementOrdinals">The ordinals for management fields.</param>
        /// <param name="complexOrdinals">The ordinals for complex fields.</param>
        /// <param name="bapOrdinals">The ordinals for BAP fields.</param>
        /// <param name="sourceOrdinals">The ordinals for source fields.</param>
        private void InitializeExportStructures(
            out DataTable exportTable,
            out StringBuilder targetList,
            out StringBuilder fromClause,
            out int[] sortOrdinals,
            out int[] conditionOrdinals,
            out int[] matrixOrdinals,
            out int[] formationOrdinals,
            out int[] managementOrdinals,
            out int[] complexOrdinals,
            out int[] bapOrdinals,
            out int[] sourceOrdinals)
        {
            exportTable = new DataTable("HluExport");
            targetList = new StringBuilder();
            fromClause = new StringBuilder();
            sortOrdinals = null;
            conditionOrdinals = null;
            matrixOrdinals = null;
            formationOrdinals = null;
            managementOrdinals = null;
            complexOrdinals = null;
            bapOrdinals = null;
            sourceOrdinals = null;

            _lastTableName = null;
            _attributesLength = 0;
            _tableCount = 0;

            // Initialize field ordinals
            _incidOrdinal = -1;
            _conditionIdOrdinal = -1;
            _conditionDateStartOrdinal = -1;
            _conditionDateEndOrdinal = -1;
            _conditionDateTypeOrdinal = -1;
            _matrixIdOrdinal = -1;
            _formationIdOrdinal = -1;
            _managementIdOrdinal = -1;
            _complexIdOrdinal = -1;
            _bapIdOrdinal = -1;
            _bapTypeOrdinal = -1;
            _bapQualityOrdinal = -1;
            _sourceIdOrdinal = -1;
            _sourceDateStartOrdinals = [];
            _sourceDateEndOrdinals = [];
            _sourceDateTypeOrdinals = [];
        }

        /// <summary>
        /// Builds the SQL target list and FROM clause by iterating through export fields.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate based on the dataset configuration.</param>
        private void BuildSqlTargetListAndFromClause(ExportJoinContext context, ref List<ExportField> exportFields)
        {
            foreach (HluDataSet.exports_fieldsRow r in
                _viewModelMain.HluDataset.exports_fields.OrderBy(r => r.field_ordinal))
            {
                // Handle GIS fields - add to exportFields but don't add to SQL query
                if (r.table_name.Equals("<gis>", StringComparison.CurrentCultureIgnoreCase))
                {
                    AddGisField(r, ref exportFields);
                    continue;
                }

                // Handle empty fields
                if (r.table_name.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                {
                    AddEmptyField(r, context.SqlFieldOrdinal, ref exportFields);
                    continue;
                }

                // Process regular database field
                ProcessDatabaseField(r, context, ref exportFields);
            }
        }

        /// <summary>
        /// Adds a GIS field to the export fields list without adding it to the SQL query.
        /// GIS fields are sourced from the GIS layer, not the database.
        /// </summary>
        /// <param name="fieldRow">The row representing the GIS field to add.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        private void AddGisField(HluDataSet.exports_fieldsRow fieldRow, ref List<ExportField> exportFields)
        {
            // Get field length from the export definition or use a default
            int fieldLength = !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) && fieldRow.field_length > 0
                ? fieldRow.field_length
                : 254; // Default for GIS text fields

            // Determine field type
            Type dataType;
            int attributeLength;

            switch (fieldRow.field_type)
            {
                case 3:     // Integer
                    dataType = typeof(int);
                    attributeLength = 4;
                    break;
                case 6:     // Single
                    dataType = typeof(float);
                    attributeLength = 4;
                    break;
                case 7:     // Double
                    dataType = typeof(double);
                    attributeLength = 8;
                    break;
                case 8:     // Date/Time
                    dataType = typeof(DateTime);
                    attributeLength = 8;
                    break;
                case 10:    // Text
                    dataType = typeof(string);
                    attributeLength = Math.Min(fieldLength, 254);
                    break;
                default:
                    dataType = typeof(string);
                    attributeLength = Math.Min(fieldLength, 254);
                    break;
            }

            // Create the export field entry
            ExportField fld = new()
            {
                FieldOrdinal = -1,  // Not in SQL query
                TableName = "<gis>",
                ColumnName = fieldRow.column_name,  // Source field name in GIS layer
                FieldName = fieldRow.field_name,    // Export field name
                FieldType = dataType,
                FieldOrder = exportFields.Count + 1,
                FieldLength = fieldLength,
                FieldsCount = 0,
                FieldFormat = !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn)
                    ? fieldRow.field_format
                    : null,
                AutoNum = false
            };

            exportFields.Add(fld);

            // Add to total attribute length for shapefile validation
            _attributesLength += attributeLength;
        }

        /// <summary>
        /// Adds an empty field (placeholder) to the export.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to add.</param>
        /// <param name="sqlFieldOrdinal">The ordinal position of the field in the SQL query.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        private void AddEmptyField(HluDataSet.exports_fieldsRow fieldRow, int sqlFieldOrdinal, ref List<ExportField> exportFields)
        {
            int fieldLength = GetFieldLength(fieldRow.table_name, fieldRow.column_ordinal);

            if (!fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                fieldRow.field_length > 0)
                fieldLength = fieldRow.field_length;

            AddExportColumn(0, fieldRow.table_name, fieldRow.column_name, fieldRow.field_name,
                fieldRow.field_type, fieldLength,
                !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? fieldRow.field_format : null,
                sqlFieldOrdinal,
                ref exportFields);
        }

        /// <summary>
        /// Processes a regular database field and adds it to the target list.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to process.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        private void ProcessDatabaseField(HluDataSet.exports_fieldsRow fieldRow, ExportJoinContext context, ref List<ExportField> exportFields)
        {
            int fieldLength = GetFieldLength(fieldRow.table_name, fieldRow.column_ordinal);
            bool multipleFields = !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.fields_countColumn);

            // Get or create table alias
            string currTable = _viewModelMain.DataBase.QualifyTableName(fieldRow.table_name);
            string currTableAlias = GetOrCreateTableAlias(currTable, fieldRow.table_name, context);

            // Get field format and lookup relations
            string fieldFormat = !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? fieldRow.field_format : null;
            var relations = GetLookupRelations(fieldRow.table_name, fieldRow.column_name, fieldFormat);

            if (!relations.Any())
            {
                // No lookup table - direct field
                AddDirectField(fieldRow, currTableAlias, fieldLength, multipleFields, context, ref exportFields);
            }
            else if (relations.Count() == 1)
            {
                // Has lookup table
                AddFieldWithLookup(fieldRow, currTable, currTableAlias, fieldLength, multipleFields, fieldFormat, relations.First(), context, ref exportFields);
            }
        }

        /// <summary>
        /// Gets or creates a table alias for the specified table.
        /// </summary>
        /// <param name="qualifiedTableName">The fully qualified name of the table.</param>
        /// <param name="tableName"> The base name of the table.</param>
        /// <param name="context"> The context object holding state for SQL construction.</param>
        /// <returns>The table alias to use for the specified table.</returns>
        private string GetOrCreateTableAlias(string qualifiedTableName, string tableName, ExportJoinContext context)
        {
            if (context.FromList.Contains(qualifiedTableName))
            {
                // Table already added, retrieve its alias
                return context.TableAliases[qualifiedTableName];
            }

            // Add table to FROM list
            context.FromList.Add(qualifiedTableName);

            // Create unique alias
            string tableAlias = context.TableAlias + context.TableAliasNum++;
            context.TableAliases[qualifiedTableName] = tableAlias;

            // Build FROM clause with appropriate JOIN
            AddTableToFromClause(qualifiedTableName, tableName, tableAlias, context);

            return tableAlias;
        }

        /// <summary>
        /// Adds a table to the FROM clause with appropriate JOIN syntax.
        /// </summary>
        /// <param name="qualifiedTableName"> The fully qualified name of the table.</param>
        /// <param name="tableName"> The base name of the table.</param>
        /// <param name="tableAlias">The alias to use for the table in the FROM clause.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        private void AddTableToFromClause(string qualifiedTableName, string tableName, string tableAlias, ExportJoinContext context)
        {
            var incidRelation = _viewModelMain.HluDataset.incid.ChildRelations.Cast<DataRelation>()
                .Where(dr => dr.ChildTable.TableName == tableName);

            if (!incidRelation.Any())
            {
                // Simple table reference
                context.FromClause.Append(qualifiedTableName);
                context.FromClause.Append(' ');
                context.FromClause.Append(tableAlias);
            }
            else
            {
                // Table needs LEFT JOIN
                DataRelation incidRel = incidRelation.ElementAt(0);
                if (context.FirstJoin)
                    context.FirstJoin = false;
                else
                    context.FromClause.Insert(0, "(").Append(')');

                // Get the parent table's qualified name and look up its alias
                string parentQualifiedTableName = _viewModelMain.DataBase.QualifyTableName(incidRel.ParentTable.TableName);
                string parentTableAlias;

                // Look up the parent table's alias from the TableAliases dictionary
                if (context.TableAliases.TryGetValue(parentQualifiedTableName, out string parentAlias))
                {
                    parentTableAlias = parentAlias;
                }
                else
                {
                    // If no alias found, use the quoted table name (shouldn't happen normally)
                    parentTableAlias = _viewModelMain.DataBase.QuoteIdentifier(incidRel.ParentTable.TableName);
                }

                context.FromClause.Append(RelationJoinClause("LEFT", qualifiedTableName, true,
                    parentTableAlias,  // Now using the actual alias, not the table name
                    incidRel, context.FromList, tableAlias));

                context.LeftJoined.Add(qualifiedTableName);
            }
        }

        /// <summary>
        /// Gets lookup relations for a field if it has a lookup format.
        /// </summary>
        /// <param name="tableName">The name of the table containing the field.</param>
        /// <param name="columnName">The name of the column representing the field.</param>
        /// <param name="fieldFormat">The field format specified for the field.</param>
        /// <returns>A collection of DataRelation objects representing the lookup relations for the field, or an empty collection if none.</returns>
        private IEnumerable<DataRelation> GetLookupRelations(string tableName, string columnName, string fieldFormat)
        {
            if (fieldFormat != null && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase) ||
                fieldFormat.Equals("lookup", StringComparison.CurrentCultureIgnoreCase)))
            {
                return _viewModelMain.HluDataRelations.Where(rel =>
                    rel.ChildTable.TableName == tableName &&
                    rel.ChildColumns.Count(ch => ch.ColumnName == columnName) == 1);
            }

            return [];
        }

        /// <summary>
        /// Adds a direct field (no lookup) to the target list.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to add.</param>
        /// <param name="tableAlias">The alias of the table containing the field.</param>
        /// <param name="fieldLength">The length of the field.</param>
        /// <param name="multipleFields">Indicates whether the field represents multiple underlying fields.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        private void AddDirectField(HluDataSet.exports_fieldsRow fieldRow, string tableAlias, int fieldLength,
            bool multipleFields, ExportJoinContext context, ref List<ExportField> exportFields)
        {
            // Add to SQL target list
            context.TargetList.Append(String.Format(",{0}.{1} AS {2}", tableAlias,
                _viewModelMain.DataBase.QuoteIdentifier(fieldRow.column_name), fieldRow.field_name.Replace("<no>", "")));

            // Override field length if specified
            if (!fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                fieldRow.field_length > 0)
                fieldLength = fieldRow.field_length;

            // Add to export columns
            AddExportColumn(multipleFields ? fieldRow.fields_count : 0, fieldRow.table_name, fieldRow.column_name, fieldRow.field_name,
                fieldRow.field_type, fieldLength,
                !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? fieldRow.field_format : String.Empty,
                context.SqlFieldOrdinal,
                ref exportFields);

            context.SqlFieldOrdinal++;
        }

        /// <summary>
        /// Adds a field with lookup table to the target list.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to add.</param>
        /// <param name="currTable">The fully qualified name of the current table containing the field.</param>
        /// <param name="currTableAlias">The alias of the current table in the SQL query.</param>
        /// <param name="fieldLength">The length of the field.</param>
        /// <param name="multipleFields">Indicates whether the field represents multiple underlying fields.</param>
        /// <param name="fieldFormat">The field format specified for the field.</param>
        /// <param name="lutRelation">The DataRelation representing the lookup relationship for the field.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        private void AddFieldWithLookup(HluDataSet.exports_fieldsRow fieldRow, string currTable, string currTableAlias,
            int fieldLength, bool multipleFields, string fieldFormat, DataRelation lutRelation,
            ExportJoinContext context, ref List<ExportField> exportFields)
        {
            string parentTable = _viewModelMain.DataBase.QualifyTableName(lutRelation.ParentTable.TableName);
            string parentTableAlias = context.TableAlias + context.TableAliasNum++;
            context.FromList.Add(parentTable);

            // Determine lookup field name and ordinal
            (string lutFieldName, int lutFieldOrdinal) = GetLookupFieldInfo(fieldRow);

            DataColumn[] lutColumns = new DataColumn[lutRelation.ParentTable.Columns.Count];
            lutRelation.ParentTable.Columns.CopyTo(lutColumns, 0);

            // Add field to target list (either by name or ordinal)
            bool fieldAdded = false;
            if (lutRelation.ParentTable.Columns.Contains(lutFieldName))
            {
                fieldAdded = AddLookupFieldByName(fieldRow, currTableAlias, parentTableAlias, lutFieldName,
                    lutColumns, fieldLength, multipleFields, fieldFormat, context, ref exportFields);
            }
            else if (lutRelation.ParentTable.Columns.Count >= lutFieldOrdinal)
            {
                fieldAdded = AddLookupFieldByOrdinal(fieldRow, currTableAlias, parentTableAlias, lutFieldOrdinal,
                    lutRelation, lutColumns, lutFieldName, fieldLength, multipleFields, fieldFormat, context, ref exportFields);
            }

            if (fieldAdded)
            {
                // Add JOIN to FROM clause
                context.LeftJoined.Add(parentTableAlias);

                if (context.FirstJoin)
                    context.FirstJoin = false;
                else
                    context.FromClause.Insert(0, "(").Append(')');

                context.FromClause.Append(RelationJoinClause("LEFT", currTable,
                    false, parentTableAlias, lutRelation, context.FromList, currTableAlias));
            }
        }

        /// <summary>
        /// Gets the lookup field name and ordinal for a field.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to get lookup info for.</param>
        /// <returns>A tuple containing the lookup field name and ordinal.</returns>
        private (string fieldName, int fieldOrdinal) GetLookupFieldInfo(HluDataSet.exports_fieldsRow fieldRow)
        {
            // If field is an ID field in incid_sources, use LutSourceField
            if ((fieldRow.table_name == _viewModelMain.HluDataset.incid_sources.TableName) && IdSuffixRegex().IsMatch(fieldRow.column_name))
            {
                return (Settings.Default.LutSourceFieldName, Settings.Default.LutSourceFieldOrdinal - 1);
            }
            // If it's a user ID field in incid, use LutUserField
            else if ((fieldRow.table_name == _viewModelMain.HluDataset.incid.TableName) && UseridSuffixRegex().IsMatch(fieldRow.column_name))
            {
                return (Settings.Default.LutUserFieldName, Settings.Default.LutUserFieldOrdinal - 1);
            }
            // Otherwise use LutDescriptionField
            else
            {
                return (Settings.Default.LutDescriptionFieldName, Settings.Default.LutDescriptionFieldOrdinal - 1);
            }
        }

        /// <summary>
        /// Adds a lookup field by name to the target list.
        /// </summary>
        /// <param name="fieldRow">The row representing the field to add.</param>
        /// <param name="currTableAlias">The alias of the current table in the SQL query.</param>
        /// <param name="parentTableAlias">The alias of the parent (lookup) table in the SQL query.</param>
        /// <param name="lutFieldName">The name of the lookup field in the parent table.</param>
        /// <param name="lutColumns">The columns of the parent (lookup) table.</param>
        /// <param name="fieldLength">The length of the field.</param>
        /// <param name="multipleFields">Indicates whether the field represents multiple underlying fields.</param>
        /// <param name="fieldFormat">The field format specified for the field.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        /// <returns>True if the field was successfully added; otherwise, false.</returns>
        private bool AddLookupFieldByName(HluDataSet.exports_fieldsRow fieldRow, string currTableAlias, string parentTableAlias,
            string lutFieldName, DataColumn[] lutColumns, int fieldLength, bool multipleFields, string fieldFormat,
            ExportJoinContext context, ref List<ExportField> exportFields)
        {
            if (fieldFormat != null && fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase))
            {
                // Both code and description
                context.TargetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                    currTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(fieldRow.column_name),
                    parentTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                    fieldRow.field_name.Replace("<no>", ""),
                    _viewModelMain.DataBase.ConcatenateOperator,
                    _viewModelMain.DataBase.QuoteValue(" : ")));

                fieldLength += lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength + 3;
            }
            else
            {
                // Just description
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    parentTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                    fieldRow.field_name.Replace("<no>", "")));

                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
            }

            // Override field length if specified
            if (!fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                fieldRow.field_length > 0)
                fieldLength = fieldRow.field_length;

            AddExportColumn(multipleFields ? fieldRow.fields_count : 0, fieldRow.table_name, fieldRow.column_name, fieldRow.field_name,
                fieldRow.field_type, fieldLength,
                !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? fieldRow.field_format : String.Empty,
                context.SqlFieldOrdinal,
                ref exportFields);

            context.SqlFieldOrdinal++;
            return true;
        }

        /// <summary>
        /// Adds a lookup field by ordinal to the target list.
        /// </summary>
        /// <param name="fieldRow"The row representing the field to add.</param>
        /// <param name="currTableAlias">The alias of the current table in the SQL query.</param>
        /// <param name="parentTableAlias">The alias of the parent (lookup) table in the SQL query.</param>
        /// <param name="lutFieldOrdinal">The ordinal position of the lookup field in the parent table.</param>
        /// <param name="lutRelation">The DataRelation representing the lookup relationship for the field.</param>
        /// <param name="lutColumns">The columns of the parent (lookup) table.</param>
        /// <param name="lutFieldName">The name of the lookup field (for error messaging).</param>
        /// <param name="fieldLength">The length of the field.</param>
        /// <param name="multipleFields">Indicates whether the field represents multiple underlying fields.</param>
        /// <param name="fieldFormat">The field format specified for the field.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to populate.</param>
        /// <returns>True if the field was successfully added; otherwise, false.</returns>
        private bool AddLookupFieldByOrdinal(HluDataSet.exports_fieldsRow fieldRow, string currTableAlias, string parentTableAlias,
            int lutFieldOrdinal, DataRelation lutRelation, DataColumn[] lutColumns, string lutFieldName,
            int fieldLength, bool multipleFields, string fieldFormat, ExportJoinContext context, ref List<ExportField> exportFields)
        {
            if (fieldFormat != null && fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase))
            {
                context.TargetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                    currTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(fieldRow.column_name),
                    parentTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                    fieldRow.field_name.Replace("<no>", ""),
                    _viewModelMain.DataBase.ConcatenateOperator,
                    _viewModelMain.DataBase.QuoteValue(" : ")));

                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
            }
            else
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    parentTableAlias,
                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                    fieldRow.field_name.Replace("<no>", "")));

                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
            }

            if (!fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                fieldRow.field_length > 0)
                fieldLength = fieldRow.field_length;

            AddExportColumn(multipleFields ? fieldRow.fields_count : 0, fieldRow.table_name, fieldRow.column_name, fieldRow.field_name,
                fieldRow.field_type, fieldLength,
                !fieldRow.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? fieldRow.field_format : null,
                context.SqlFieldOrdinal,
                ref exportFields);

            context.SqlFieldOrdinal++;
            return true;
        }

        /// <summary>
        /// Builds the field map template from export fields.
        /// </summary>
        /// <param name="exportFields">The list of export fields.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportTable">The DataTable to which export fields will be added.</param>
        /// <returns>A jagged array representing the field map template.</returns>
        private int[][] BuildFieldMapTemplate(List<ExportField> exportFields, ExportJoinContext context, DataTable exportTable)
        {
            var fieldMapTemplate = new int[exportFields.Max(e => e.FieldOrdinal) + 1][];
            int fieldTotal = 0;

            foreach (ExportField f in exportFields.OrderBy(f => f.FieldOrder))
            {
                // Skip GIS fields - they'll be added during the GIS export/join process
                if (f.TableName != null && f.TableName.Equals("<gis>", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                // Add field to export table
                DataColumn c = new(f.FieldName, f.FieldType);
                if (f.AutoNum == true) c.AutoIncrement = true;
                if ((f.FieldType == System.Type.GetType("System.String")) && (f.FieldLength > 0))
                    c.MaxLength = f.FieldLength;

                exportTable.Columns.Add(c);  // Now we have access to exportTable

                if (f.FieldOrdinal == -1)
                {
                    fieldTotal += 1;
                    continue;
                }

                // Track important field ordinals
                TrackImportantFields(f, fieldTotal, context);

                // Build field map
                List<int> fieldMap;
                if ((fieldMapTemplate[f.FieldOrdinal] != null) && (f.AutoNum != true))
                    fieldMap = fieldMapTemplate[f.FieldOrdinal].ToList();
                else
                {
                    fieldMap = [];
                    fieldMap.Add(f.FieldOrdinal);
                }

                fieldMap.Add(fieldTotal);
                fieldMapTemplate[f.FieldOrdinal] = fieldMap.ToArray();

                fieldTotal += 1;
            }

            return fieldMapTemplate;
        }

        /// <summary>
        /// Tracks important field ordinals for sorting and relationships.
        /// </summary>
        /// <param name="f">The export field to track.</param>
        /// <param name="fieldTotal">The current total number of fields processed.</param>
        /// <param name="context">The context object holding state for SQL construction.</param>
        private void TrackImportantFields(ExportField f, int fieldTotal, ExportJoinContext context)
        {
            // Track incid field
            if ((f.FieldsCount == 0) && (f.TableName == _viewModelMain.HluDataset.incid.TableName) &&
                (f.ColumnName == _viewModelMain.HluDataset.incid.incidColumn.ColumnName))
            {
                _incidOrdinal = f.FieldOrdinal;
                context.SortFields.Add(f.FieldOrdinal + 1);
                context.PrimaryKeyOrdinal = fieldTotal;
            }

            // Track condition fields
            if (f.TableName == _viewModelMain.HluDataset.incid_condition.TableName)
            {
                context.ConditionFields.Add(fieldTotal);
                TrackConditionFieldOrdinals(f);
            }
            // Track matrix fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName)
            {
                context.MatrixFields.Add(fieldTotal);
                if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName)
                    _matrixIdOrdinal = f.FieldOrdinal;
            }
            // Track formation fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName)
            {
                context.FormationFields.Add(fieldTotal);
                if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName)
                    _formationIdOrdinal = f.FieldOrdinal;
            }
            // Track management fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName)
            {
                context.ManagementFields.Add(fieldTotal);
                if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName)
                    _managementIdOrdinal = f.FieldOrdinal;
            }
            // Track complex fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName)
            {
                context.ComplexFields.Add(fieldTotal);
                if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName)
                    _complexIdOrdinal = f.FieldOrdinal;
            }
            // Track BAP fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_bap.TableName)
            {
                context.BapFields.Add(fieldTotal);
                if (f.ColumnName == _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName)
                    _bapIdOrdinal = f.FieldOrdinal;
            }
            // Track source fields
            else if (f.TableName == _viewModelMain.HluDataset.incid_sources.TableName)
            {
                context.SourceFields.Add(fieldTotal);

                if ((f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName) &&
                    ((String.IsNullOrEmpty(f.FieldFormat)) || (f.FieldFormat.Equals("code", StringComparison.CurrentCultureIgnoreCase))))
                    _sourceIdOrdinal = f.FieldOrdinal;
                else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName)
                    context.SourceSortOrderOrdinal = f.FieldOrdinal;
                else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName)
                    _sourceDateStartOrdinals.Add(f.FieldOrdinal);
                else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName)
                    _sourceDateEndOrdinals.Add(f.FieldOrdinal);
                else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName)
                    _sourceDateTypeOrdinals.Add(f.FieldOrdinal);
            }
        }

        /// <summary>
        /// Tracks condition field ordinals.
        /// </summary>
        /// <param name="f">The export field representing a condition field.</param>
        private void TrackConditionFieldOrdinals(ExportField f)
        {
            if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName)
                _conditionIdOrdinal = f.FieldOrdinal;
            else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName)
                _conditionDateStartOrdinal = f.FieldOrdinal;
            else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName)
                _conditionDateEndOrdinal = f.FieldOrdinal;
            else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName)
                _conditionDateTypeOrdinal = f.FieldOrdinal;
        }

        /// <summary>
        /// Adds any required extra fields for sorting and relationships that weren't in the export definition.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required extra fields.</param>
        /// <param name="targetList">The StringBuilder representing the SQL target list to which extra fields will be added if needed.</param>
        private void AddRequiredExtraFields(ExportJoinContext context, List<ExportField> exportFields, ref StringBuilder targetList)
        {
            AddConditionExtraFields(context, exportFields);
            AddMatrixExtraFields(context, exportFields);
            AddFormationExtraFields(context, exportFields);
            AddManagementExtraFields(context, exportFields);
            AddComplexExtraFields(context, exportFields);
            AddBapExtraFields(context, exportFields);
            AddSourceExtraFields(context, exportFields);
        }

        /// <summary>
        /// Adds extra condition fields if needed.
        /// </summary>
        /// <param name="context"> The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required condition fields.</param>
        private void AddConditionExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_condition.TableName))
                return;

            string conditionTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_condition.TableName, context);

            if (_conditionIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    conditionTableAlias,
                    _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName));

                _conditionIdOrdinal = context.SqlFieldOrdinal++;
            }

            if (_conditionDateStartOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    conditionTableAlias,
                    _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName));

                _conditionDateStartOrdinal = context.SqlFieldOrdinal++;
            }

            if (_conditionDateEndOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    conditionTableAlias,
                    _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName));

                _conditionDateEndOrdinal = context.SqlFieldOrdinal++;
            }

            if (_conditionDateTypeOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    conditionTableAlias,
                    _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName));

                _conditionDateTypeOrdinal = context.SqlFieldOrdinal++;
            }

            // Add to sort fields (descending order)
            context.SortFields.Add((_conditionIdOrdinal + 1) * -1);
        }

        /// <summary>
        /// Adds extra matrix fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required matrix fields.</param>
        private void AddMatrixExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName))
                return;

            string matrixTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_ihs_matrix.TableName, context);

            if (_matrixIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    matrixTableAlias,
                    _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName));

                _matrixIdOrdinal = context.SqlFieldOrdinal++;
            }

            context.SortFields.Add(_matrixIdOrdinal + 1);
        }

        /// <summary>
        /// Adds extra formation fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required formation fields.</param>
        private void AddFormationExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName))
                return;

            string formationTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_ihs_formation.TableName, context);

            if (_formationIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    formationTableAlias,
                    _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName));

                _formationIdOrdinal = context.SqlFieldOrdinal++;
            }

            context.SortFields.Add(_formationIdOrdinal + 1);
        }

        /// <summary>
        /// Adds extra management fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required management fields.</param>
        private void AddManagementExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName))
                return;

            string managementTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_ihs_management.TableName, context);

            if (_managementIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    managementTableAlias,
                    _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName));

                _managementIdOrdinal = context.SqlFieldOrdinal++;
            }

            context.SortFields.Add(_managementIdOrdinal + 1);
        }

        /// <summary>
        /// Adds extra complex fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required complex fields.</param>
        private void AddComplexExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName))
                return;

            string complexTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_ihs_complex.TableName, context);

            if (_complexIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    complexTableAlias,
                    _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName));

                _complexIdOrdinal = context.SqlFieldOrdinal++;
            }

            context.SortFields.Add(_complexIdOrdinal + 1);
        }

        /// <summary>
        /// Adds extra BAP fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required BAP fields.</param>
        private void AddBapExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_bap.TableName))
                return;

            string bapTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_bap.TableName, context);

            // Add bap_habitat_quality field
            if (_bapQualityOrdinal == -1)
            {
                if ((DbFactory.ConnectionType.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)) ||
                    (DbFactory.Backend.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)))
                {
                    context.TargetList.Append(String.Format(", IIF({0}.{1} = {2}, 2, IIF({0}.{1} = {3}, 1, 0)) AS {4}",
                        bapTableAlias,
                        _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                        _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                        _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                        "bap_habitat_quality"));
                }
                else
                {
                    context.TargetList.Append(String.Format(", CASE {0}.{1} WHEN {2} THEN 2 WHEN {3} THEN 1 ELSE 0 END AS {4}",
                        bapTableAlias,
                        _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                        _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                        _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                        "bap_habitat_quality"));
                }

                _bapQualityOrdinal = context.SqlFieldOrdinal++;
                context.SortFields.Add(_bapQualityOrdinal + 1);
            }

            // Add bap_habitat field
            if (_bapTypeOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    bapTableAlias,
                    _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName));

                _bapTypeOrdinal = context.SqlFieldOrdinal++;
                context.SortFields.Add(_bapTypeOrdinal + 1);
            }

            // Add bap_id field
            if (_bapIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    bapTableAlias,
                    _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName));

                _bapIdOrdinal = context.SqlFieldOrdinal++;
                context.SortFields.Add(_bapIdOrdinal + 1);
            }
        }

        /// <summary>
        /// Adds extra source fields if needed.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction.</param>
        /// <param name="exportFields">The list of export fields to check for required source fields.</param>
        private void AddSourceExtraFields(ExportJoinContext context, List<ExportField> exportFields)
        {
            if (!exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_sources.TableName))
                return;

            string sourceTableAlias = GetTableAliasOrDefault(_viewModelMain.HluDataset.incid_sources.TableName, context);

            // Add source_id field
            if (_sourceIdOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    sourceTableAlias,
                    _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName));

                _sourceIdOrdinal = context.SqlFieldOrdinal++;
            }

            // Add sort_order field
            if (context.SourceSortOrderOrdinal == -1)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    sourceTableAlias,
                    _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName));

                context.SourceSortOrderOrdinal = context.SqlFieldOrdinal++;
            }

            context.SortFields.Add(context.SourceSortOrderOrdinal + 1);

            // Add source_date_start field
            if (_sourceDateStartOrdinals.Count == 0)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    sourceTableAlias,
                    _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName));

                _sourceDateStartOrdinals.Add(context.SqlFieldOrdinal++);
            }

            // Add source_date_end field
            if (_sourceDateEndOrdinals.Count == 0)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    sourceTableAlias,
                    _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName));

                _sourceDateEndOrdinals.Add(context.SqlFieldOrdinal++);
            }

            // Add source_date_type field
            if (_sourceDateTypeOrdinals.Count == 0)
            {
                context.TargetList.Append(String.Format(",{0}.{1} AS {2}",
                    sourceTableAlias,
                    _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName,
                    _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName));

                _sourceDateTypeOrdinals.Add(context.SqlFieldOrdinal++);
            }
        }

        /// <summary>
        /// Gets the table alias for a table name, or returns the table name if no alias exists.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="context">The context object holding state for SQL construction, including table aliases.</param>
        /// <returns>The table alias if it exists; otherwise, the original table name.</returns>
        private string GetTableAliasOrDefault(string tableName, ExportJoinContext context)
        {
            string qualifiedTableName = _viewModelMain.DataBase.QualifyTableName(tableName);
            return context.TableAliases.TryGetValue(qualifiedTableName, out string alias) ? alias : tableName;
        }

        /// <summary>
        /// Finalizes sort and field ordinals from the context.
        /// </summary>
        /// <param name="context">The context object holding state for SQL construction, including field ordinals.</param>
        /// <param name="sortOrdinals">The output array of sort field ordinals.</param>
        /// <param name="conditionOrdinals">The output array of condition field ordinals.</param>
        /// <param name="matrixOrdinals">The output array of matrix field ordinals.</param>
        /// <param name="formationOrdinals">The output array of formation field ordinals.</param>
        /// <param name="managementOrdinals">The output array of management field ordinals.</param>
        /// <param name="complexOrdinals">The output array of complex field ordinals.</param>
        /// <param name="bapOrdinals">The output array of BAP field ordinals.</param>
        /// <param name="sourceOrdinals">The output array of source field ordinals.</param>
        private void FinalizeSortAndFieldOrdinals(ExportJoinContext context,
            out int[] sortOrdinals, out int[] conditionOrdinals, out int[] matrixOrdinals,
            out int[] formationOrdinals, out int[] managementOrdinals, out int[] complexOrdinals,
            out int[] bapOrdinals, out int[] sourceOrdinals)
        {
            sortOrdinals = context.SortFields.ToArray();
            conditionOrdinals = context.ConditionFields.ToArray();
            matrixOrdinals = context.MatrixFields.ToArray();
            formationOrdinals = context.FormationFields.ToArray();
            managementOrdinals = context.ManagementFields.ToArray();
            complexOrdinals = context.ComplexFields.ToArray();
            bapOrdinals = context.BapFields.ToArray();
            sourceOrdinals = context.SourceFields.ToArray();
        }

        #endregion Export Joins

        #region Helper Methods

        /// <summary>
        /// Gets the length of the original input field.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <returns>The length of the field.</returns>
        private int GetFieldLength(string tableName, int columnOrdinal)
        {
            int fieldLength = 0;

            // Get a list of all the incid related tables (including the
            // incid table itself.
            List<DataTable> tables;
            tables = _viewModelMain.HluDataset.incid.ChildRelations
                .Cast<DataRelation>().Select(r => r.ChildTable).ToList();
            tables.Add(_viewModelMain.HluDataset.incid);

            foreach (DataTable t in tables)
            {
                if (t.TableName == tableName)
                {
                    DataColumn[] columns = new DataColumn[t.Columns.Count];
                    t.Columns.CopyTo(columns, 0);

                    // Get the field length.
                    fieldLength = columns[columnOrdinal - 1].MaxLength;
                    break;
                }
            }

            return fieldLength;
        }

        /// <summary>
        /// Adds the export column to the export table.
        /// </summary>
        /// <param name="numFields">The number of occurrences of this field.</param>
        /// <param name="tableName">The name of the source table for this field.</param>
        /// <param name="columnName">The name of the exported column.</param>
        /// <param name="fieldName">The name of the field in the export file.</param>
        /// <param name="fieldType">The data type of the field.</param>
        /// <param name="maxLength">The maximum length of the column.</param>
        /// <param name="fieldFormat">The format of the field.</param>
        /// <param name="sqlFieldOrdinal">The ordinal position of the field in the SQL query.</param>
        /// <param name="exportFields">The list of export fields.</param>
        private void AddExportColumn(int numFields, string tableName, string columnName, string fieldName, int fieldType, int maxLength,
            string fieldFormat, int sqlFieldOrdinal, ref List<ExportField> exportFields)
        {
            Type dataType = null;
            int fieldLength = 0;
            bool autoNum = false;
            int attributeLength = 0;

            // Increment each time a different table is referenced.
            if (tableName != _lastTableName)
                _tableCount += 1;

            // Enable fields to be exported using a different
            // data type.
            switch (fieldType)
            {
                case 3:     // Integer
                    dataType = System.Type.GetType("System.Int32");
                    attributeLength = 2;
                    break;
                case 6:     // Single
                    dataType = System.Type.GetType("System.Single");
                    attributeLength = 4;
                    break;
                case 7:     // Double
                    dataType = System.Type.GetType("System.Double");
                    attributeLength = 8;
                    break;
                case 8:     // Date/Time
                    dataType = System.Type.GetType("System.DateTime");
                    attributeLength = 8;
                    break;
                case 10:    // Text
                    dataType = System.Type.GetType("System.String");
                    if (maxLength > 0)
                    {
                        fieldLength = Math.Min(maxLength, 254);
                        attributeLength = fieldLength;
                    }
                    else
                    {
                        fieldLength = 254;
                        attributeLength = fieldLength;
                    }
                    break;
                case 99:    // Autonumber
                    dataType = System.Type.GetType("System.Int32");
                    autoNum = true;
                    attributeLength = 4;
                    break;
                default:
                    dataType = System.Type.GetType("System.String");
                    fieldLength = maxLength;
                    attributeLength = maxLength;
                    break;
            }

            // If this field has multiple occurrences.
            if (numFields > 0)
            {
                int fieldCount = exportFields.Count + 1;

                for (int i = 1; i <= numFields; i++)
                {
                    // Include the occurrence counter in the field name, either
                    // where the user chooses or at the end.
                    string finalFieldName;
                    if (OccurrenceCounterRegex().IsMatch(fieldName))
                        finalFieldName = fieldName.Replace("<no>", i.ToString());
                    else
                    {
                        if (numFields == 1)
                            finalFieldName = fieldName;
                        else
                            finalFieldName = String.Format("{0}_{1}", fieldName, i);
                    }

                    // Check if this field name is already in the export structure (regardless of case)
                    if (exportFields.Any(f => f.FieldName.Equals(finalFieldName, StringComparison.Ordinal)))
                    {
                        // Skip this field as it's already been added
                        continue;
                    }

                    ExportField fld = new();

                    // Enable new 'empty' fields to be included in exports.
                    if (tableName.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                        fld.FieldOrdinal = -1;
                    else
                        fld.FieldOrdinal = sqlFieldOrdinal;

                    fld.TableName = tableName;
                    fld.ColumnName = columnName;
                    fld.FieldName = finalFieldName;
                    fld.FieldType = dataType;

                    // Interweave multiple record fields from the same table together.
                    fld.FieldOrder = (_tableCount * 1000) + (i * 100) + fieldCount;

                    fld.FieldLength = fieldLength;
                    fld.FieldsCount = numFields;
                    fld.FieldFormat = fieldFormat;
                    fld.AutoNum = autoNum;

                    exportFields.Add(fld);

                    // Add the field attribute length to the running total.
                    _attributesLength += attributeLength;
                }
            }
            else
            {
                // Check if this field name is already in the export structure (regardless of case)
                if (exportFields.Any(f => f.FieldName.Equals(fieldName, StringComparison.Ordinal)))
                {
                    // Skip this field as it's already been added
                    return;
                }

                ExportField fld = new();

                // Enable new 'empty' fields to be included in exports.
                if (tableName.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                    fld.FieldOrdinal = -1;
                else
                    fld.FieldOrdinal = sqlFieldOrdinal;

                fld.TableName = tableName;
                fld.ColumnName = columnName;
                fld.FieldName = fieldName;
                fld.FieldType = dataType;

                // Interweave multiple record fields from the same table together.
                fld.FieldOrder = (_tableCount * 1000) + exportFields.Count + 1;

                fld.FieldLength = fieldLength;
                fld.FieldsCount = numFields;
                fld.FieldFormat = fieldFormat;
                fld.AutoNum = autoNum;

                exportFields.Add(fld);

                // Add the field attribute length to the running total.
                _attributesLength += attributeLength;
            }

            // Store the last table referenced.
            _lastTableName = tableName;
        }

        /// <summary>
        /// Constructs the SQL JOIN clause for a given relationship between tables, based on the specified join type and table aliases.
        /// </summary>
        /// <param name="joinType">The type of SQL JOIN (e.g., INNER, LEFT, RIGHT).</param>
        /// <param name="currTable">The current table involved in the join.</param>
        /// <param name="parentLeft">Indicates if the parent table is on the left side of the join.</param>
        /// <param name="parentTableAlias">The alias for the parent table.</param>
        /// <param name="rel">The DataRelation object representing the relationship between tables.</param>
        /// <param name="fromList">A list of tables already included in the FROM clause.</param>
        /// <param name="currTableAlias">The alias for the current table (optional).</param>
        /// <returns>The SQL JOIN clause as a string.</returns>
        private string RelationJoinClause(string joinType, string currTable, bool parentLeft,
            string parentTableAlias, DataRelation rel, List<string> fromList, string currTableAlias = null)
        {
            StringBuilder joinClausePart = new();

            // Use the alias if provided, otherwise use the table name
            string childTableRef = string.IsNullOrEmpty(currTableAlias) ? currTable : currTableAlias;

            // Build the ON conditions using only aliases
            for (int i = 0; i < rel.ParentColumns.Length; i++)
            {
                joinClausePart.Append(String.Format(" AND {0}.{2} = {1}.{3}", parentTableAlias,
                    childTableRef,
                    _viewModelMain.DataBase.QuoteIdentifier(rel.ParentColumns[i].ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(rel.ChildColumns[i].ColumnName)));
            }

            string tableToJoin = string.Empty;

            if (parentLeft)
            {
                // Parent on left (already in FROM clause), child on right (being added)
                tableToJoin = currTable + " " + childTableRef;
            }
            else
            {
                // Child on left (already in FROM clause), parent on right (being added - lookup table)
                // For lookup tables, qualify the parent table name and add alias
                string qualifiedParentTable = _viewModelMain.DataBase.QualifyTableName(rel.ParentTable.TableName);
                tableToJoin = qualifiedParentTable + " " + parentTableAlias;
            }

            // Remove leading " AND " and prepend the JOIN clause
            return joinClausePart.Remove(0, 5).Insert(0, String.Format(" {0} JOIN {1} ON ",
                joinType, tableToJoin)).ToString();
        }

        /// <summary>
        /// Generates a unique table alias that does not conflict with existing table names in the dataset.
        /// The method iteratively constructs potential aliases by combining characters and checks for their
        /// uniqueness against the dataset's table names using regular expressions.
        /// </summary>
        /// <remarks>The default is generally the lower case letter 'z'.</remarks>
        /// <returns>A unique table alias as a string.</returns>
        private string GetTableAlias()
        {
            // Iterate through potential alias combinations, starting with single characters and increasing in length up to 4 characters.
            for (int i = 1; i < 5; i++)
            {
                // Iterate through ASCII values for lowercase letters (a-z) to construct potential aliases
                // starting from 'z' (122) down to 'a' (97) to create aliases in reverse alphabetical order.
                for (int j = 122; j > 96; j--)
                {
                    // Construct a potential alias by creating a character array of length 'i'
                    // filled with the character corresponding to ASCII value 'j'.
                    char[] testCharArray = new char[i];

                    // Fill the character array with the same character to create aliases like "z", "zz", "zzz", etc.
                    for (int k = 0; k < i; k++)
                        testCharArray[k] = (char)j;

                    // Convert the character array to a string to form the potential alias.
                    string testString = new(testCharArray);

                    // Check if the generated alias does not match any existing table names in the dataset.
                    if (!_viewModelMain.HluDataset.Tables.Cast<DataTable>().Any(t => Regex.IsMatch(t.TableName,
                        testString + "[0-9]+", RegexOptions.IgnoreCase)))
                    {
                        // If the alias is unique, return it for use in SQL queries.
                        return testString;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if a field should be skipped due to duplicate ID detection.
        /// </summary>
        /// <param name="exportColumn">The ordinal of the export column.</param>
        /// <param name="currentId">The current ID being checked.</param>
        /// <param name="ordinalSet">The set of ordinals to check against.</param>
        /// <param name="usedIds">The set of IDs that have already been used.</param>
        /// <returns>True if the field should be skipped; otherwise, false.</returns>
        private static bool ShouldSkipDuplicate(int exportColumn, int currentId, HashSet<int> ordinalSet, HashSet<int> usedIds)
        {
            return currentId != -1 && ordinalSet.Contains(exportColumn) && usedIds.Contains(currentId);
        }

        #endregion Helper Methods

        #region Regex Definitions

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the suffix "_id".
        /// </summary>
        /// <remarks>
        /// - The pattern `(_id)` matches the exact string "_id".
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match an "_id" suffix.</returns>
        [GeneratedRegex(@"(_id)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex IdSuffixRegex();

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the suffix "_user_id".
        /// </summary>
        /// <remarks>
        /// - The pattern `(_user_id)` matches the exact string "_user_id".
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match a "_user_id" suffix.</returns>
        [GeneratedRegex(@"(_user_id)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex UseridSuffixRegex();

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the string "<no>".
        /// </summary>
        /// <remarks>
        /// - The pattern `(<no>)` matches the exact string "<no>", including the angle brackets.
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match the string "<no>".</returns>
        [GeneratedRegex(@"(<no>)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex OccurrenceCounterRegex();

        #endregion Regex Definitions
    }
}