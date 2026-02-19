using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using HLU.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HLU.GISApplication
{
    /// <summary>
    /// Provides helper methods for working with ArcGIS Pro geodatabases, feature classes, tables, and related spatial
    /// data operations. Includes utilities for checking existence, creating, deleting, copying, exporting, and joining
    /// geodatabase objects, as well as methods for executing actions on rows and features by ObjectID.
    /// </summary>
    /// <remarks>This class contains static methods intended to simplify common ArcGIS Pro geoprocessing and
    /// data management tasks. Many methods wrap asynchronous geoprocessing tools or geodatabase schema operations, and
    /// are designed for use within ArcGIS Pro add-ins or scripts. Methods typically return <see langword="true"/> if
    /// the operation succeeds, or <see langword="false"/> if it fails or input parameters are invalid. Some methods
    /// require execution within <c>QueuedTask.Run</c> to ensure thread safety when interacting with geodatabase
    /// objects. The class is internal and not intended for public API consumption.</remarks>
    internal static class ArcGISProHelpers
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

        /// <summary>
        /// Cleans up all tables and feature classes from a geodatabase.
        /// </summary>
        /// <param name="gdbPath">Path to the geodatabase.</param>
        /// <returns>Number of items deleted.</returns>
        public static async Task<int> CleanupGeodatabaseAsync(string gdbPath)
        {
            // Check parameters.
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
                return 0;

            int deletedCount = 0;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Get and delete all tables
                    IReadOnlyList<TableDefinition> tableDefs = geodatabase.GetDefinitions<TableDefinition>();
                    foreach (var tableDef in tableDefs)
                    {
                        try
                        {
                            // Create a TableDescription object
                            TableDescription tableDescription = new(tableDef);

                            // Add the deletion for the table to the list of DDL tasks
                            schemaBuilder.Delete(tableDescription);
                            deletedCount++;
                        }
                        catch
                        {
                            // Skip if deletion fails
                        }
                    }

                    // Get and delete all feature classes
                    IReadOnlyList<FeatureClassDefinition> fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();
                    foreach (var fcDef in fcDefs)
                    {
                        try
                        {
                            // Create a FeatureClassDescription object
                            FeatureClassDescription fcDescription = new(fcDef);

                            // Add the deletion for the feature class to the list of DDL tasks
                            schemaBuilder.Delete(fcDescription);
                            deletedCount++;
                        }
                        catch
                        {
                            // Skip if deletion fails
                        }
                    }

                    // Only execute if there are operations to perform
                    if (deletedCount > 0)
                    {
                        bool success = schemaBuilder.Build();

                        // If build failed, reset the count
                        if (!success)
                            deletedCount = 0;
                    }
                });
            }
            catch
            {
                // Silently fail
            }

            return deletedCount;
        }

        #endregion Geodatabase

        #region Indexes

        /// <summary>
        /// Adds an attribute index to a table or feature class for improved join performance.
        /// </summary>
        /// <remarks>
        /// This method checks if an index already exists on the specified field before attempting to create one.
        /// If an index exists, the method returns true without creating a duplicate index.
        /// </remarks>
        /// <param name="inTable">The full path to the input table or feature class.</param>
        /// <param name="fieldNames">The field name(s) to index (comma-separated for composite indexes).</param>
        /// <param name="indexName">The name for the new index.</param>
        /// <returns>True if the index was created successfully or already exists, otherwise false.</returns>
        public static async Task<bool> AddAttributeIndexAsync(string gdbPath, string inTable, string fieldNames, string indexName)
        {
            // Check there is an input geodatabase path.
            if (String.IsNullOrWhiteSpace(gdbPath))
                return false;

            // Check there is an input table path.
            if (String.IsNullOrWhiteSpace(inTable))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrWhiteSpace(fieldNames))
                return false;

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    using Table table = geodatabase.OpenDataset<Table>(inTable);

                    TableDefinition def = table.GetDefinition();
                    IReadOnlyList<ArcGIS.Core.Data.Index> indexes = def.GetIndexes();

                    // Check if any index includes the specified field.
                    bool indexExists = indexes.Any(idx =>
                        idx.GetFields().Any(f =>
                            f.Name.Equals(fieldNames, StringComparison.OrdinalIgnoreCase)));

                    // If the index already exists, return true without creating a duplicate.
                    if (indexExists)
                        return true;

                    // Set the parameters to add the index to the table or feature class.
                    var parameters = Geoprocessing.MakeValueArray(inTable, fieldNames, indexName, "", "");

                    // Make a value array of the environments to be passed to the tool.
                    var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                    // Set the geoprocessing flags.
                    GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread;

                    //Geoprocessing.OpenToolDialog("management.AddIndex", parameters);  // Useful for debugging.

                    // Add the index to the table or feature class.
                    try
                    {
                        // Execute the tool.
                        IGPResult gp_result = await Geoprocessing.ExecuteToolAsync(
                            "management.AddIndex",
                            parameters,
                            environments,
                            null,
                            null,
                            executeFlags);

                        if (gp_result.IsFailed)
                        {
                            Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                            System.Diagnostics.Debug.WriteLine(
                                $"Failed to add index: {string.Join("\n", gp_result.Messages.Select(m => m.Text))}");

                            //var messages = gp_result.Messages;
                            //var errMessages = gp_result.ErrorMessages;
                            return false;
                        }

                        return true;
                    }
                    catch (Exception)
                    {
                        // Handle Exception.
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking for existing index: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion Indexes

        #region Joins

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
        /// Deletes the duplicate join field that was added from the join table.
        /// </summary>
        /// <remarks>
        /// The JoinField geoprocessing tool retains both the original field from the target feature class
        /// and the joined field from the join table. This method removes the duplicate field that was added
        /// during the join operation (typically named "fieldname_1", "fieldname_2", etc.).
        /// </remarks>
        /// <param name="featureClassPath">The full path to the feature class.</param>
        /// <param name="baseFieldName">The base name of the join field (e.g., "incid").</param>
        /// <returns>True if the duplicate field was deleted or didn't exist, otherwise false.</returns>
        public static async Task<bool> DeleteDuplicateJoinFieldAsync(string featureClassPath, string baseFieldName)
        {
            // Check there is an input feature class path.
            if (String.IsNullOrWhiteSpace(featureClassPath))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrWhiteSpace(baseFieldName))
                return false;

            return await QueuedTask.Run(async () =>
            {
                try
                {
                    string gdbPath = System.IO.Path.GetDirectoryName(featureClassPath);
                    string fcName = System.IO.Path.GetFileName(featureClassPath);

                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    using FeatureClass fc = geodatabase.OpenDataset<FeatureClass>(fcName);

                    FeatureClassDefinition def = fc.GetDefinition();
                    IReadOnlyList<Field> fields = def.GetFields();

                    // Find duplicate fields created by JoinField.
                    // The JoinField tool typically appends "_1", "_2", etc. to duplicate field names.
                    var duplicateFields = fields
                        .Where(f => f.Name.StartsWith(baseFieldName + "_", StringComparison.OrdinalIgnoreCase))
                        .Select(f => f.Name)
                        .ToList();

                    // If no duplicate fields found, nothing to do.
                    if (duplicateFields.Count == 0)
                        return true;

                    // Set the geoprocessing flags.
                    GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread;

                    // Make a value array of the environments to be passed to the tool.
                    var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                    // Delete all duplicate fields using the DeleteField geoprocessing tool.
                    foreach (string fieldName in duplicateFields)
                    {
                        // Set the parameters to delete the duplicate field.
                        var parameters = Geoprocessing.MakeValueArray(featureClassPath, fieldName);

                        //Geoprocessing.OpenToolDialog("management.DeleteField", parameters);  // Useful for debugging.

                        // Delete the duplicate field.
                        try
                        {
                            // Execute the tool.
                            IGPResult gp_result = await Geoprocessing.ExecuteToolAsync(
                                "management.DeleteField",
                                parameters,
                                environments,
                                null,
                                null,
                                executeFlags);

                            if (gp_result.IsFailed)
                            {
                                Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                                System.Diagnostics.Debug.WriteLine(
                                    $"Failed to delete field {fieldName}: {string.Join("\n", gp_result.Messages.Select(m => m.Text))}");

                                //var messages = gp_result.Messages;
                                //var errMessages = gp_result.ErrorMessages;
                                return false;
                            }
                        }
                        catch (Exception)
                        {
                            // Handle Exception.
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting duplicate join fields: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion Joins

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

        /// <summary>
        /// Creates a table in a file geodatabase based on a DataTable schema.
        /// </summary>
        /// <param name="gdbPath">Full path to the file geodatabase.</param>
        /// <param name="tableName">Name of the table to create.</param>
        /// <param name="schemaTable">DataTable defining the table structure.</param>
        /// <returns>True if the table was successfully created; false otherwise.</returns>
        public static async Task<bool> CreateTableAsync(string gdbPath, string tableName, DataTable schemaTable)
        {
            // Check there is an input geodatabase path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            // Check there is a schema table.
            if (schemaTable == null || schemaTable.Columns.Count == 0)
                return false;

            try
            {
                // Check if the geodatabase exists.
                if (!Directory.Exists(gdbPath))
                    return false;

                // Delete the table if it already exists.
                bool tableExists = await TableExistsGDBAsync(gdbPath, tableName);
                if (tableExists)
                {
                    bool deleted = await DeleteGeodatabaseTableAsync(gdbPath, tableName);
                    if (!deleted)
                        return false;
                }

                // Build the schema on the MCT.
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    List<FieldDescription> fieldDescriptions = [];

                    // Create a FieldDescription for each column in the schema DataTable and add it to the list of field descriptions for the table.
                    foreach (DataColumn column in schemaTable.Columns)
                    {
                        FieldDescription fieldDesc = CreateFieldDescriptionFromColumn(column);
                        fieldDescriptions.Add(fieldDesc);
                    }

                    // Create a TableDescription for the new table.
                    TableDescription tableDescription = new(tableName, fieldDescriptions);

                    // Create a SchemaBuilder object
                    SchemaBuilder builder = new(geodatabase);
                    builder.Create(tableDescription);

                    // Execute the DDL to create the table.
                    bool built = builder.Build();
                    if (!built)
                    {
                        string msg = builder.ErrorMessages != null && builder.ErrorMessages.Any()
                            ? string.Join(Environment.NewLine, builder.ErrorMessages)
                            : $"SchemaBuilder.Build() failed for '{tableName}'.";

                        throw new Exception(msg);
                    }
                });

                return true;
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }
        }

        /// <summary>
        /// Creates a field description from a DataColumn for geodatabase table creation.
        /// </summary>
        /// <param name="column">The DataColumn to convert.</param>
        /// <returns>A FieldDescription for the geodatabase table.</returns>
        private static FieldDescription CreateFieldDescriptionFromColumn(DataColumn column)
        {
            FieldDescription fieldDesc;
            FieldType fieldType;
            int fieldLength = column.MaxLength;

            // Map .NET type to geodatabase field type.
            if (column.DataType == typeof(string))
            {
                fieldType = FieldType.String;
                fieldLength = fieldLength > 0 ? Math.Min(fieldLength, 254) : 254;
            }
            else if (column.DataType == typeof(int) || column.DataType == typeof(Int32))
            {
                fieldType = FieldType.Integer;
            }
            else if (column.DataType == typeof(double) || column.DataType == typeof(Double))
            {
                fieldType = FieldType.Double;
            }
            else if (column.DataType == typeof(float) || column.DataType == typeof(Single))
            {
                fieldType = FieldType.Single;
            }
            else if (column.DataType == typeof(DateTime))
            {
                fieldType = FieldType.Date;
            }
            else
            {
                // Default to string for unsupported types.
                fieldType = FieldType.String;
                fieldLength = 254;
            }

            // Create the field description.
            if (fieldType == FieldType.String)
            {
                fieldDesc = new FieldDescription(column.ColumnName, fieldType)
                {
                    Length = fieldLength
                };
            }
            else
            {
                fieldDesc = new FieldDescription(column.ColumnName, fieldType);
            }

            return fieldDesc;
        }

        /// <summary>
        /// Performs a bulk insert of rows into a geodatabase table using batched operations.
        /// </summary>
        /// <param name="gdbPath">Full path to the file geodatabase.</param>
        /// <param name="tableName">Name of the table to insert rows into.</param>
        /// <param name="rows">List of rows to insert, where each row is a dictionary of field name to value.</param>
        /// <returns>The number of rows successfully inserted.</returns>
        /// <remarks>
        /// This method uses InsertCursor for efficient batch insertion. Each row dictionary should contain
        /// field names as keys and field values as objects. DBNull.Value is used for null values.
        /// The method processes rows in batches for optimal performance.
        /// </remarks>
        public static async Task<int> BulkInsertRowsAsync(string gdbPath, string tableName, List<Dictionary<string, object>> rows)
        {
            // Check there is an input geodatabase path.
            if (String.IsNullOrEmpty(gdbPath))
                return 0;

            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return 0;

            // Check there are rows to insert.
            if (rows == null || rows.Count == 0)
                return 0;

            try
            {
                int insertedCount = await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    // Open the table for inserting rows.
                    using Table table = geodatabase.OpenDataset<Table>(tableName);

                    // Create an InsertCursor for the table to perform batch inserts.
                    using InsertCursor insertCursor = table.CreateInsertCursor();

                    int count = 0;

                    // Iterate through the rows to insert.
                    foreach (var rowValues in rows)
                    {
                        // Create a new RowBuffer for the table schema.
                        using RowBuffer rowBuffer = table.CreateRowBuffer();

                        // Populate the RowBuffer with values from the row dictionary. Use DBNull.Value for nulls.
                        foreach (var kvp in rowValues)
                        {
                            rowBuffer[kvp.Key] = kvp.Value ?? DBNull.Value;
                        }

                        // Insert the row into the table using the InsertCursor.
                        insertCursor.Insert(rowBuffer);
                        count++;
                    }

                    // Flush all pending inserts in one operation.
                    insertCursor.Flush();

                    return count;
                });

                return insertedCount;
            }
            catch (Exception)
            {
                // Handle Exception.
                return 0;
            }
        }

        /// <summary>
        /// Performs a bulk insert of rows into a geodatabase table using batched operations with multiple batches.
        /// </summary>
        /// <param name="gdbPath">Full path to the file geodatabase.</param>
        /// <param name="tableName">Name of the table to insert rows into.</param>
        /// <param name="batches">List of batches, where each batch contains rows to insert.</param>
        /// <returns>The number of rows successfully inserted across all batches.</returns>
        /// <remarks>
        /// This overload processes multiple batches of rows in a single operation for improved efficiency
        /// when dealing with very large datasets. Each batch is a list of row dictionaries.
        /// </remarks>
        public static async Task<int> BulkInsertRowsAsync(string gdbPath, string tableName, List<List<Dictionary<string, object>>> batches)
        {
            // Check there is an input geodatabase path.
            if (String.IsNullOrEmpty(gdbPath))
                return 0;

            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return 0;

            // Check there are batches to insert.
            if (batches == null || batches.Count == 0)
                return 0;

            try
            {
                int insertedCount = await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    // Open the table for inserting rows.
                    using Table table = geodatabase.OpenDataset<Table>(tableName);

                    // Create an InsertCursor for the table to perform batch inserts.
                    using InsertCursor insertCursor = table.CreateInsertCursor();

                    int count = 0;

                    // Iterate through each batch of rows to insert.
                    foreach (var batch in batches)
                    {
                        // Iterate through each row in the batch.
                        foreach (var rowValues in batch)
                        {
                            // Create a new RowBuffer for the table schema.
                            using RowBuffer rowBuffer = table.CreateRowBuffer();

                            // Populate the RowBuffer with values from the row dictionary. Use DBNull.Value for nulls.
                            foreach (var kvp in rowValues)
                            {
                                rowBuffer[kvp.Key] = kvp.Value ?? DBNull.Value;
                            }

                            // Insert the row into the table using the InsertCursor.
                            insertCursor.Insert(rowBuffer);
                            count++;
                        }
                    }

                    // Flush all pending inserts in one operation.
                    insertCursor.Flush();

                    return count;
                });

                return insertedCount;
            }
            catch (Exception)
            {
                // Handle Exception.
                return 0;
            }
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

        #region ObjectID Actions

        /// <summary>
        /// Executes an action for a row identified by ObjectID.
        /// </summary>
        /// <param name="table">The table to read from.</param>
        /// <param name="objectId">The ObjectID to retrieve.</param>
        /// <param name="action">The action to execute if the row is found.</param>
        /// <returns><see langword="true"/> if the row was found; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// This method is intended to be called within <c>QueuedTask.Run</c>.
        /// The row is only valid during the execution of <paramref name="action"/>.
        /// </remarks>
        internal static bool WithRowByObjectId(
            Table table,
            long objectId,
            Action<Row> action)
        {
            // Check the table is valid.
            ArgumentNullException.ThrowIfNull(table);

            // Check the action is valid.
            ArgumentNullException.ThrowIfNull(action);

            // Build a query filter to retrieve the row by ObjectID.
            QueryFilter qf = new()
            {
                ObjectIDs = [objectId]
            };

            // Search for the row.
            using RowCursor cursor = table.Search(qf, false);

            // If no row found, return false.
            if (cursor.MoveNext() == false)
                return false;

            // Execute the action with the found row.
            using Row row = cursor.Current;
            action(row);

            return true;
        }

        /// <summary>
        /// Executes an action for a feature identified by ObjectID.
        /// </summary>
        /// <param name="featureClass">The feature class to read from.</param>
        /// <param name="objectId">The ObjectID to retrieve.</param>
        /// <param name="action">The action to execute if the feature is found.</param>
        /// <returns><see langword="true"/> if the feature was found; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// This method is intended to be called within <c>QueuedTask.Run</c>.
        /// The feature is only valid during the execution of <paramref name="action"/>.
        /// </remarks>
        internal static bool WithFeatureByObjectId(
            FeatureClass featureClass,
            long objectId,
            Action<Feature> action)
        {
            // Check parameters.
            ArgumentNullException.ThrowIfNull(featureClass);

            ArgumentNullException.ThrowIfNull(action);

            // Use the row helper to get the feature by ObjectID.
            return WithRowByObjectId(featureClass, objectId, row =>
            {
                // Cast the row to a feature and execute the action.
                if (row is Feature feature)
                    action(feature);
            });
        }

        #endregion ObjectID Actions

    }
}