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
            //_viewModelExport = new ViewModelExport(_viewModelMain.GisSelection == null ? 0 :
            //_viewModelMain.GisSelection.Rows.Count, _viewModelMain.GISApplication.HluLayerName,
            //_viewModelMain.GISApplication.ApplicationType, _viewModelMain.HluDataset.exports);
            _viewModelExport = new(_viewModelMain.GisSelection == null ? 0 :
                fragCount, _viewModelMain.GISApplication.HluLayerName,
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
                    out bapOrdinals, out sourceOrdinals);

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
                    SqlFilterCondition cond = new("AND",
                        _viewModelMain.IncidTable, _viewModelMain.IncidTable.incidColumn, null)
                    {
                        Operator = "IS NOT NULL"
                    };
                    exportFilter = new List<List<SqlFilterCondition>>([
                        new List<SqlFilterCondition>([cond])]);
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

                // Create export attribute table in the existing working geodatabase.
                if (!await CreateExportTableAsync(workingFileGDBpath, attributeTable, attributeTableName))
                    return;

                _viewModelMain.ChangeCursor(Cursors.Wait, "Exporting to temporary table ...");

                // Export the attribute data to the working geodatabase.
                int exportRowCount = await ExportToTableAsync(workingFileGDBpath, attributeTableName,
                    targetList.ToString(), fromClause.ToString(), exportFilter,
                    _viewModelMain.DataBase, exportFields, attributeTable, sortOrdinals, conditionOrdinals,
                    matrixOrdinals, formationOrdinals, managementOrdinals, complexOrdinals, bapOrdinals,
                    sourceOrdinals, fieldMapTemplate);

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
        /// <returns>The path to the working file geodatabase with the export table.</returns>
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
            int[][] fieldMap)
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
                        dataBase);

                    //TODO: Debug - Print sql to debug output
                    System.Diagnostics.Debug.WriteLine($"Executing export SQL (chunk {j + 1}/{exportFilter.Count}, length: {sql.Length} chars, {exportFilter[j].Count} conditions):\n{sql}");

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
        /// <returns></returns>
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

        /// <summary>
        /// Creates a field mapping that indicates the desired order of fields in the final output.
        /// </summary>
        /// <param name="exportFields">The list of export fields with order information.</param>
        /// <returns>Dictionary mapping field name to target position (0-based).</returns>
        private Dictionary<string, int> CreateFieldOrderMapping(List<ExportField> exportFields)
        {
            var fieldOrderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Sort by FieldOrder and assign sequential positions
            int position = 0;
            foreach (var field in exportFields.OrderBy(f => f.FieldOrder))
            {
                fieldOrderMap[field.FieldName] = position;
                position++;
            }

            return fieldOrderMap;
        }

        /// <summary>
        /// Creates a field rename mapping from source column names to export field names.
        /// Only includes fields where ColumnName != FieldName.
        /// </summary>
        /// <param name="exportFields">The list of export fields.</param>
        /// <returns>Dictionary mapping source column name to export field name.</returns>
        private Dictionary<string, string> CreateFieldRenameMapping(List<ExportField> exportFields)
        {
            var fieldRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var field in exportFields)
            {
                // Only add to rename map if the name is actually different
                if (!field.ColumnName.Equals(field.FieldName, StringComparison.Ordinal))
                {
                    fieldRenameMap[field.ColumnName] = field.FieldName;
                }
            }

            return fieldRenameMap;
        }

        #endregion Export Processing

        #region Export Joins

        /// <summary>
        /// Constructs the SQL target list and from clause with the necessary joins based on the export fields defined in the dataset.
        /// </summary>
        /// <param name="tableAlias">The alias for the main table in the SQL query.</param>
        /// <param name="gisLayerFields">The list of field names from the GIS layer.</param>
        /// <param name="gisFields">The list of field objects from the GIS layer.</param>
        /// <param name="exportFields">The list of export fields with order information.</param>
        /// <param name="exportTable">The DataTable to hold the export data.</param>
        /// <param name="fieldMapTemplate">The template for mapping fields in the export.</param>
        /// <param name="targetList">The SQL target list for the export query.</param>
        /// <param name="fromClause">The SQL from clause for the export query.</param>
        /// <param name="sortOrdinals">The ordinals for sorting fields.</param>
        /// <param name="conditionOrdinals">The ordinals for condition fields.</param>
        /// <param name="matrixOrdinals">The ordinals for matrix fields.</param>
        /// <param name="formationOrdinals">The ordinals for formation fields.</param>
        /// <param name="managementOrdinals">The ordinals for management fields.</param>
        /// <param name="complexOrdinals">The ordinals for complex fields.</param>
        /// <param name="bapOrdinals">The ordinals for BAP fields.</param>
        /// <param name="sourceOrdinals">The ordinals for source fields.</param>
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
            out int[] bapOrdinals, out int[] sourceOrdinals)
        {
            // Ensure we have GIS field information
            if (gisLayerFields == null || gisFields == null)
                throw new ArgumentException("GIS layer field information is required for export.");

            exportTable = new("HluExport");
            targetList = new();
            List<string> fromList = [];
            List<string> leftJoined = [];
            fromClause = new();
            sortOrdinals = null;
            conditionOrdinals = null;
            matrixOrdinals = null;
            formationOrdinals = null;
            managementOrdinals = null;
            complexOrdinals = null;
            bapOrdinals = null;
            sourceOrdinals = null;

            int tableAliasNum = 1;
            bool firstJoin = true;
            _lastTableName = null;
            int sqlFieldOrdinal = 0;  // NEW: Track actual SQL field position
            int fieldLength = 0;
            _attributesLength = 0;
            _tableCount = 0;

            Dictionary<string, string> tableAliases = new(); // Maps qualified table name -> alias

            // Iterate through the export fields in ordinal order to construct the SQL target list and from clause with necessary joins.
            foreach (HluDataSet.exports_fieldsRow r in
                _viewModelMain.HluDataset.exports_fields.OrderBy(r => r.field_ordinal))
            {
                // Check if this is a GIS layer field - skip it (no GIS fields in attribute table)
                if (r.table_name.Equals("<gis>", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;  // Skip - don't add to exportTable or exportFields for attribute table
                }

                // Get the field length of the input table/column.
                fieldLength = GetFieldLength(r.table_name, r.column_ordinal);

                // Enable new 'empty' fields to be included in exports.
                if (r.table_name.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                {
                    // Override the input field length(s) if an export
                    // field length has been set.
                    if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                        r.field_length > 0)
                        fieldLength = r.field_length;

                    AddExportColumn(0, r.table_name, r.column_name, r.field_name,
                        r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null,
                        sqlFieldOrdinal,
                        ref exportFields);
                    continue;  // Don't increment sqlFieldOrdinal for empty fields
                }

                // Determine if this field is to be output multiple times,
                // once for each row in the relevant table up to the
                // maximum fields_count value.
                bool multipleFields = false;
                if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.fields_countColumn))
                    multipleFields = true;

                // Add the required table to the list of sql tables in
                // the from clause.
                string currTable = _viewModelMain.DataBase.QualifyTableName(r.table_name);
                string currTableAlias = currTable; // Default to full table name

                // If this table has not already been added to the from clause, add it now along with the necessary join(s).
                if (!fromList.Contains(currTable))
                {
                    fromList.Add(currTable);

                    // Assign a unique alias for this table
                    currTableAlias = tableAlias + tableAliasNum++;
                    tableAliases[currTable] = currTableAlias;

                    var incidRelation = _viewModelMain.HluDataset.incid.ChildRelations.Cast<DataRelation>()
                        .Where(dr => dr.ChildTable.TableName == r.table_name);

                    if (!incidRelation.Any())
                    {
                        fromClause.Append(currTable);
                        fromClause.Append(" ");
                        fromClause.Append(currTableAlias);
                    }
                    else
                    {
                        DataRelation incidRel = incidRelation.ElementAt(0);
                        if (firstJoin)
                            firstJoin = false;
                        else
                            fromClause.Insert(0, "(").Append(')');

                        fromClause.Append(RelationJoinClause("LEFT", currTable, true,
                            _viewModelMain.DataBase.QuoteIdentifier(
                            incidRel.ParentTable.TableName), incidRel, fromList, currTableAlias));

                        leftJoined.Add(currTable);
                    }
                }
                else
                {
                    // Table already added, retrieve its alias
                    currTableAlias = tableAliases[currTable];
                }

                // Get the relationships for the table/column if a
                // value from a lookup table is required.
                string fieldFormat = !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null;

                // Get the list of data relations for this table/column.
                var relations = ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)
                    || fieldFormat.Equals("lookup", StringComparison.CurrentCultureIgnoreCase))) ? _viewModelMain.HluDataRelations.Where(rel =>
                    rel.ChildTable.TableName == r.table_name && rel.ChildColumns
                    .Count(ch => ch.ColumnName == r.column_name) == 1) : [];

                switch (relations.Count())
                {
                    case 0:     // If this field does not have any related lookup tables.

                        // Add the field to the sql target list.
                        targetList.Append(String.Format(",{0}.{1} AS {2}", currTableAlias,
                            _viewModelMain.DataBase.QuoteIdentifier(r.column_name), r.field_name.Replace("<no>", "")));

                        // Enable text field lengths to be specified in
                        // the export format.
                        //
                        // Override the input field length(s) if an export
                        // field length has been set.
                        if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                            r.field_length > 0)
                            fieldLength = r.field_length;

                        // Add the field to the sql list of export table columns.
                        AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                            r.field_type, fieldLength,
                            !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : String.Empty,
                            sqlFieldOrdinal,
                            ref exportFields);

                        sqlFieldOrdinal++;  // Increment SQL field position
                        break;

                    case 1:     // If this field has a related lookup table.

                        DataRelation lutRelation = relations.ElementAt(0);
                        string parentTable = _viewModelMain.DataBase.QualifyTableName(lutRelation.ParentTable.TableName);

                        string parentTableAlias = tableAlias + tableAliasNum++;
                        fromList.Add(parentTable);

                        // Determine the related lookup table field name and
                        // field ordinal.
                        string lutFieldName;
                        int lutFieldOrdinal;
                        if ((r.table_name == _viewModelMain.HluDataset.incid_sources.TableName) && (IdSuffixRegex().IsMatch(r.column_name)))
                        {
                            string lutSourceFieldName = Settings.Default.LutSourceFieldName;
                            int lutSourceFieldOrdinal = Settings.Default.LutSourceFieldOrdinal;

                            lutFieldName = lutSourceFieldName;
                            lutFieldOrdinal = lutSourceFieldOrdinal - 1;
                        }
                        else if ((r.table_name == _viewModelMain.HluDataset.incid.TableName) && (UseridSuffixRegex().IsMatch(r.column_name)))
                        {
                            string lutUserFieldName = Settings.Default.LutUserFieldName;
                            int lutUserFieldOrdinal = Settings.Default.LutUserFieldOrdinal;
                            lutFieldName = lutUserFieldName;
                            lutFieldOrdinal = lutUserFieldOrdinal - 1;
                        }
                        else
                        {
                            string lutDescriptionFieldName = Settings.Default.LutDescriptionFieldName;
                            int lutDescriptionFieldOrdinal = Settings.Default.LutDescriptionFieldOrdinal;

                            lutFieldName = lutDescriptionFieldName;
                            lutFieldOrdinal = lutDescriptionFieldOrdinal - 1;
                        }

                        // Get the list of columns for the lookup table.
                        DataColumn[] lutColumns = new DataColumn[lutRelation.ParentTable.Columns.Count];
                        lutRelation.ParentTable.Columns.CopyTo(lutColumns, 0);

                        // If the lookup table contains the required field name.
                        if (lutRelation.ParentTable.Columns.Contains(lutFieldName))
                        {
                            // If both the original field and it's corresponding lookup
                            // table field are required then add them both to the sql
                            // target list.
                            if ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)))
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                                    currTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(r.column_name),
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                                    r.field_name.Replace("<no>", ""),
                                    _viewModelMain.DataBase.ConcatenateOperator,
                                    _viewModelMain.DataBase.QuoteValue(" : ")));

                                // Set the field length of the export field to the input
                                // field length plus the lookup table field length plus 3
                                // for the concatenation string length.
                                fieldLength += lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength + 3;
                            }
                            else
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} AS {2}",
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                                    r.field_name.Replace("<no>", "")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }

                            // Enable text field lengths to be specified in
                            // the export format.
                            //
                            // Override the input field length(s) if an export
                            // field length has been set.
                            if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                                r.field_length > 0)
                                fieldLength = r.field_length;

                            // Add the field to the sql list of export table columns.
                            AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                                r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : String.Empty,
                                sqlFieldOrdinal,
                                ref exportFields);

                            sqlFieldOrdinal++;  // Increment SQL field position
                        }
                        // If the lookup table does not contains the required field
                        // name, but does contain the required field ordinal.
                        else if (lutRelation.ParentTable.Columns.Count >= lutFieldOrdinal)
                        {
                            // If both the original field and it's corresponding lookup
                            // table field are required then add them both to the sql
                            // target list.
                            if ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)))
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                                    currTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(r.column_name),
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                                    r.field_name.Replace("<no>", ""),
                                    _viewModelMain.DataBase.ConcatenateOperator,
                                    _viewModelMain.DataBase.QuoteValue(" : ")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }
                            else
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} AS {2}",
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                                    r.field_name.Replace("<no>", "")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }

                            // Enable text field lengths to be specified in
                            // the export format.
                            //
                            // Override the input field length(s) if an export
                            // field length has been set.
                            if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                                r.field_length > 0)
                                fieldLength = r.field_length;

                            // Add the field to the sql list of export table columns.
                            AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                                r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null,
                                sqlFieldOrdinal,
                                ref exportFields);

                            sqlFieldOrdinal++;  // Increment SQL field position
                        }
                        else
                        {
                            continue;
                        }

                        // Make all joins LEFT joins for simplicity and to allow for null
                        // foreign key values.
                        string joinType = "LEFT";
                        leftJoined.Add(parentTableAlias);

                        if (firstJoin)
                            firstJoin = false;
                        else
                            fromClause.Insert(0, "(").Append(')');

                        fromClause.Append(RelationJoinClause(joinType, currTable,
                            false, parentTableAlias, lutRelation, fromList, currTableAlias));

                        break;
                }
            }

            // Interweave multiple record fields from the same
            // table together.
            //
            // Create a new field map template with as many items
            // as there are input fields.
            fieldMapTemplate = new int[exportFields.Max(e => e.FieldOrdinal) + 1][];

            // Initialize the input field ordinals for any important fields that
            // may be required for formatting or to maintain relationships between
            // tables during the export process.
            int fieldTotal = 0;
            int primaryKeyOrdinal = -1;
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
            int sourceSortOrderOrdinal = -1;

            // Initialize lists to hold the output field positions of fields from important tables that
            // may be required for formatting or to maintain relationships between tables during
            // the export process.
            List<int> sortFields = [];
            List<int> conditionFields = [];
            List<int> matrixFields = [];
            List<int> formationFields = [];
            List<int> managementFields = [];
            List<int> complexFields = [];
            List<int> bapFields = [];
            List<int> sourceFields = [];

            // Loop through all the export fields, adding them as columns
            // in the export table and adding them to the field map template.
            foreach (ExportField f in exportFields.OrderBy(f => f.FieldOrder))
            {
                // Create a new data column for the field.
                DataColumn c = new(f.FieldName, f.FieldType);

                // If the field is an autonumber set the relevant
                // auto increment properties.
                if (f.AutoNum == true) c.AutoIncrement = true;

                // If the field is a text field and has a maximum length
                // then set the maximum length property.
                if ((f.FieldType == System.Type.GetType("System.String")) &&
                    (f.FieldLength > 0)) c.MaxLength = f.FieldLength;

                // Add the field as a new column in the export table.
                exportTable.Columns.Add(c);

                // If the field will not be sourced from the database.
                if (f.FieldOrdinal == -1)
                {
                    // Increment the total number of fields to be exported.
                    fieldTotal += 1;

                    // Skip adding the field to the field map template.
                    continue;
                }

                // If the field is not repeated and refers to the incid column
                // in the incid table.
                if ((f.FieldsCount == 0) && ((f.TableName == _viewModelMain.HluDataset.incid.TableName) &&
                    (f.ColumnName == _viewModelMain.HluDataset.incid.incidColumn.ColumnName)))
                {
                    // Store the input field position for use later
                    // when exporting the data.
                    _incidOrdinal = f.FieldOrdinal;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(f.FieldOrdinal + 1);

                    // Store the output field position for use later
                    // as the primary index field ordinal.
                    primaryKeyOrdinal = fieldTotal;
                }

                // If the table is the incid_condition table.
                if (f.TableName == _viewModelMain.HluDataset.incid_condition.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the condition table.
                    conditionFields.Add(fieldTotal);

                    // If the field refers to the condition_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_condition field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName)
                        _conditionIdOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_start column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName)
                        _conditionDateStartOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_end column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName)
                        _conditionDateEndOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_type column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName)
                        _conditionDateTypeOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_matrix table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the matrix table.
                    matrixFields.Add(fieldTotal);

                    // If the field refers to the matrix_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_matrix field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName)
                        _matrixIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_formation table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the formation table.
                    formationFields.Add(fieldTotal);

                    // If the field refers to the formation_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_formation field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName)
                        _formationIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_management table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the management table.
                    managementFields.Add(fieldTotal);

                    // If the field refers to the management_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_management field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName)
                        _managementIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_complex table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the complex table.
                    complexFields.Add(fieldTotal);

                    // If the field refers to the complex_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_complex field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName)
                        _complexIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_bap table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_bap.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the bap table.
                    bapFields.Add(fieldTotal);

                    // If the field refers to the bap_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_bap field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName)
                        _bapIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_sources table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_sources.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the sources table.
                    sourceFields.Add(fieldTotal);

                    // If the field refers to the source_id column and is
                    // retrieved in it's 'raw' integer state then store
                    // the input field ordinal for use later as the unique
                    // incid_source field ordinal.
                    if ((f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName) &&
                        ((String.IsNullOrEmpty(f.FieldFormat)) || (f.FieldFormat.Equals("code", StringComparison.CurrentCultureIgnoreCase))))
                        _sourceIdOrdinal = f.FieldOrdinal;
                    // If the field refers to the source_sort_order column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName)
                        sourceSortOrderOrdinal = f.FieldOrdinal;
                    // If the field refers to the source_date_start column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName)
                        _sourceDateStartOrdinals.Add(f.FieldOrdinal);
                    // If the field refers to the source_date_end column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName)
                        _sourceDateEndOrdinals.Add(f.FieldOrdinal);
                    // If the field refers to the source_date_type column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName)
                        _sourceDateTypeOrdinals.Add(f.FieldOrdinal);
                }

                // Set the field mapping for the current field ordinal.
                List<int> fieldMap;
                if ((fieldMapTemplate[f.FieldOrdinal] != null) && (f.AutoNum != true))
                    fieldMap = fieldMapTemplate[f.FieldOrdinal].ToList();
                else
                {
                    fieldMap = [];
                    fieldMap.Add(f.FieldOrdinal);
                }

                // Add the current field number to the field map for
                // this field ordinal.
                fieldMap.Add(fieldTotal);

                // Update the field map template for this field ordinal.
                fieldMapTemplate[f.FieldOrdinal] = fieldMap.ToArray();

                // Increment the total number of fields to be exported.
                fieldTotal += 1;
            }

            // Note: sqlFieldOrdinal contains the count of SQL fields added so far
            // Use it when adding extra fields at the end

            // If any incid_condition fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_condition.TableName)))
            {
                // If the incid_condition_id column is not included then add
                // it so that different conditions can be identified.
                if (_conditionIdOrdinal == -1)
                {
                    // Get the alias for the incid_condition table
                    string conditionTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_condition.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_condition.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        conditionTableAlias,
                        _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName));

                    _conditionIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // If the condition_date_start column is not included then add
                // it for use later.
                if (_conditionDateStartOrdinal == -1)
                {
                    // Get the alias for the incid_condition table
                    string conditionTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_condition.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_condition.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        conditionTableAlias,
                        _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName));

                    _conditionDateStartOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // If the condition_date_end column is not included then add
                // it for use later.
                if (_conditionDateEndOrdinal == -1)
                {
                    // Get the alias for the incid_condition table
                    string conditionTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_condition.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_condition.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        conditionTableAlias,
                        _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName));

                    _conditionDateEndOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // If the condition_date_type column is not included then add
                // it for use later.
                if (_conditionDateTypeOrdinal == -1)
                {
                    // Get the alias for the incid_condition table
                    string conditionTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_condition.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_condition.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        conditionTableAlias,
                        _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName));

                    _conditionDateTypeOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records. Multiply
                // by minus 1 to indicate descending order).
                sortFields.Add((_conditionIdOrdinal + 1) * -1);
            }

            // If any incid_ihs_matrix fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName)))
            {
                // If the matrix_id column is not included then add
                // it so that different matrixs can be identified.
                if (_matrixIdOrdinal == -1)
                {
                    // Get the alias for the incid_ihs_matrix table
                    string matrixTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_matrix.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_ihs_matrix.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        matrixTableAlias,
                        _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName));

                    _matrixIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_matrixIdOrdinal + 1);
            }

            // If any incid_ihs_formation fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName)))
            {
                // If the formation_id column is not included then add
                // it so that different formations can be identified.
                if (_formationIdOrdinal == -1)
                {
                    // Get the alias for the incid_ihs_formation table
                    string formationTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_formation.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_ihs_formation.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        formationTableAlias,
                        _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName));

                    _formationIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_formationIdOrdinal + 1);
            }

            // If any incid_ihs_management fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName)))
            {
                // If the management_id column is not included then add
                // it so that different managements can be identified.
                if (_managementIdOrdinal == -1)
                {
                    // Get the alias for the incid_ihs_management table
                    string managementTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_management.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_ihs_management.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        managementTableAlias,
                        _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName));

                    _managementIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_managementIdOrdinal + 1);
            }

            // If any incid_ihs_complex fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName)))
            {
                // If the complex_id column is not included then add
                // it so that different complexs can be identified.
                if (_complexIdOrdinal == -1)
                {
                    // Get the alias for the incid_ihs_complex table
                    string complexTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_ihs_complex.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_ihs_complex.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        complexTableAlias,
                        _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName));

                    _complexIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_complexIdOrdinal + 1);
            }

            // If any incid_bap fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_bap.TableName)))
            {
                // If the bap_habitat_quality column is not included then
                // add it so that the baps can be sorted by quality.
                if (_bapQualityOrdinal == -1)
                {
                    // Get the alias for the incid_bap table
                    string bapTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_bap.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_bap.TableName;

                    // Add a field to the input table to get the determination
                    // quality of the bap habitat so that 'not present' habitats
                    // are listed after 'present' habitats.
                    if ((DbFactory.ConnectionType.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)) ||
                        (DbFactory.Backend.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)))
                        targetList.Append(String.Format(", IIF({0}.{1} = {2}, 2, IIF({0}.{1} = {3}, 1, 0)) AS {4}",
                            bapTableAlias,
                            _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                            "bap_habitat_quality"));
                    else
                        targetList.Append(String.Format(", CASE {0}.{1} WHEN {2} THEN 2 WHEN {3} THEN 1 ELSE 0 END AS {4}",
                            bapTableAlias,
                            _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                            "bap_habitat_quality"));

                    _bapQualityOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapQualityOrdinal + 1);
                }

                // If the bap_habitat_type column is not included then
                // add it so that the baps can be sorted by type.
                if (_bapTypeOrdinal == -1)
                {
                    // Get the alias for the incid_bap table
                    string bapTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_bap.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_bap.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        bapTableAlias,
                        _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName, _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName));

                    _bapTypeOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapTypeOrdinal + 1);
                }

                // If the bap_id column is not included then add
                // it so that different baps can be identified.
                if (_bapIdOrdinal == -1)
                {
                    // Get the alias for the incid_bap table
                    string bapTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_bap.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_bap.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        bapTableAlias,
                        _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName, _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName));

                    _bapIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapIdOrdinal + 1);
                }
            }

            // If any incid_source fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_sources.TableName)))
            {
                // If the source_id column is not included then add
                // it so that different sources can be identified.
                if (_sourceIdOrdinal == -1)
                {
                    // Get the alias for the incid_sources table
                    string sourceTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_sources.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_sources.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        sourceTableAlias,
                        _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName));

                    _sourceIdOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // If the sort_order column is not included then add
                // it so that the sources can be sorted.
                if (sourceSortOrderOrdinal == -1)
                {
                    // Get the alias for the incid_sources table
                    string sourceTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_sources.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_sources.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        sourceTableAlias,
                        _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName));

                    sourceSortOrderOrdinal = sqlFieldOrdinal;
                    sqlFieldOrdinal++;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(sourceSortOrderOrdinal + 1);

                // If the source_date_start column is not included then add
                // it for use later.
                if (_sourceDateStartOrdinals.Count == 0)
                {
                    // Get the alias for the incid_sources table
                    string sourceTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_sources.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_sources.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        sourceTableAlias,
                        _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName));

                    _sourceDateStartOrdinals.Add(sqlFieldOrdinal);
                    sqlFieldOrdinal++;
                }

                // If the source_date_end column is not included then add
                // it for use later.
                if (_sourceDateEndOrdinals.Count == 0)
                {
                    // Get the alias for the incid_sources table
                    string sourceTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_sources.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_sources.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        sourceTableAlias,
                        _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName));

                    _sourceDateEndOrdinals.Add(sqlFieldOrdinal);
                    sqlFieldOrdinal++;
                }

                // If the source_date_type column is not included then add
                // it for use later.
                if (_sourceDateTypeOrdinals.Count == 0)
                {
                    // Get the alias for the incid_sources table
                    string sourceTableAlias = tableAliases.TryGetValue(
                        _viewModelMain.DataBase.QualifyTableName(_viewModelMain.HluDataset.incid_sources.TableName),
                        out string alias) ? alias : _viewModelMain.HluDataset.incid_sources.TableName;

                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}",
                        sourceTableAlias,
                        _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName));

                    _sourceDateTypeOrdinals.Add(sqlFieldOrdinal);
                    sqlFieldOrdinal++;
                }
            }

            // Store which export fields will be used to sort the
            // input records.
            sortOrdinals = sortFields.ToArray();

            // Store the field ordinals for all the fields for
            // every child table.
            conditionOrdinals = conditionFields.ToArray();
            matrixOrdinals = matrixFields.ToArray();
            formationOrdinals = formationFields.ToArray();
            managementOrdinals = managementFields.ToArray();
            complexOrdinals = complexFields.ToArray();
            bapOrdinals = bapFields.ToArray();
            sourceOrdinals = sourceFields.ToArray();

            // Set the incid field as the primary key to the table.
            if (primaryKeyOrdinal != -1)
                exportTable.PrimaryKey = [exportTable.Columns[primaryKeyOrdinal]];

            // Remove the leading comma from the target list of fields.
            if (targetList.Length > 1) targetList.Remove(0, 1);
        }

        #endregion Export Joins

        #region Helper Methods

        /// <summary>
        /// Gets the length of the original input field.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <returns></returns>
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

            for (int i = 0; i < rel.ParentColumns.Length; i++)
            {
                joinClausePart.Append(String.Format(" AND {0}.{2} = {1}.{3}", parentTableAlias,
                    childTableRef, // Use the aliased child table reference
                    _viewModelMain.DataBase.QuoteIdentifier(rel.ParentColumns[i].ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(rel.ChildColumns[i].ColumnName)));
            }

            if (parentTableAlias == _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName))
                parentTableAlias = String.Empty;
            else
                parentTableAlias = " " + parentTableAlias;

            string leftTable = String.Empty;
            string rightTable = string.Empty;
            if (parentLeft)
            {
                leftTable = _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName) + parentTableAlias;
                rightTable = childTableRef; // Use alias here
            }
            else
            {
                leftTable = childTableRef; // Use alias here
                rightTable = _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName) + parentTableAlias;
            }

            if (!fromList.Contains(currTable))
                return joinClausePart.Remove(0, 5).Insert(0, String.Format(" {0} {1} JOIN {2} ON ",
                    leftTable, joinType, rightTable)).ToString();
            else
                return joinClausePart.Remove(0, 5).Insert(0, String.Format(" {0} JOIN {1} ON ",
                    joinType, rightTable)).ToString();
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