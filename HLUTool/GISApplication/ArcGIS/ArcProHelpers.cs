using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using HLU.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FieldDescription = ArcGIS.Core.Data.DDL.FieldDescription;

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
        /// <param name="filePath">The path to the file or geodatabase.</param>
        /// <param name="fileName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class exists, otherwise false.</returns>
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
        /// <param name="fullPath">The full path to the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class exists, otherwise false.</returns>
        public static async Task<bool> FeatureClassExistsAsync(string fullPath)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(fullPath))
                return false;

            return await FeatureClassExistsAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Creates a temporary feature class with only the specified GIS fields in the correct order.
        /// </summary>
        /// <remarks>
        /// Uses the CopyFeatures geoprocessing tool with a custom FieldMapping parameter to filter which GIS
        /// fields are included and control their order in the output. System fields (OBJECTID, Shape, etc.)
        /// are always included first. The method wraps the ArcGIS Pro CopyFeatures tool and executes it on
        /// a background thread to avoid blocking the UI.
        /// </remarks>
        /// <param name="sourcePath">The full path to the source feature layer.</param>
        /// <param name="gdbPath">The file geodatabase path where the temporary feature class will be created.</param>
        /// <param name="tempFcName">The name for the temporary feature class.</param>
        /// <param name="fieldMapping">
        /// The field mapping string that controls which fields are copied and their order.
        /// Format: "field1 field1 VISIBLE NONE;field2 field2 VISIBLE NONE;..."
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains true if the temporary feature class was created successfully; otherwise, false.
        /// </returns>
        public static async Task<bool> CreateTempFeatureClassAsync(
            string sourcePath,
            string gdbPath,
            string tempFcName,
            string fieldMapping)
        {
            // Check if there is a source path.
            if (String.IsNullOrEmpty(sourcePath))
                return false;

            // Check if there is a geodatabase path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check if there is a temporary feature class name.
            if (String.IsNullOrEmpty(tempFcName))
                return false;

            // Check if there is a field mapping.
            if (String.IsNullOrEmpty(fieldMapping))
                return false;

            // Build temp feature class path.
            string tempFcPath = System.IO.Path.Combine(gdbPath, tempFcName);

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(
                sourcePath,
                tempFcPath,
                "", // config_keyword
                "", // spatial_grid_1
                "", // spatial_grid_2
                "", // spatial_grid_3
                fieldMapping); // field_mapping

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread;

            //Geoprocessing.OpenToolDialog("management.CopyFeatures", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync(
                    "management.CopyFeatures",
                    parameters,
                    environments,
                    null,
                    null,
                    executeFlags);

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
        /// Delete a feature class by file path and file name.
        /// </summary>
        /// <param name="filePath">The path to the file or geodatabase.</param>
        /// <param name="fileName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class was successfully deleted, otherwise false.</returns>
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
        /// <param name="fileName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class was successfully deleted, otherwise false.</returns>
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
        /// <param name="inTable">The input table or feature class.</param>
        /// <param name="fieldName">The name of the field to add.</param>
        /// <param name="fieldType">The type of the field.</param>
        /// <param name="fieldPrecision">The precision of the field.</param>
        /// <param name="fieldScale">The scale of the field.</param>
        /// <param name="fieldLength">The length of the field.</param>
        /// <param name="fieldAlias">The alias of the field.</param>
        /// <param name="fieldIsNullable">Whether the field is nullable.</param>
        /// <param name="fieldIsRequred">Whether the field is required.</param>
        /// <param name="fieldDomain">The domain of the field.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the field was successfully added, otherwise false.</returns>
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
        /// Delete one or more fields from a feature class or table.
        /// </summary>
        /// <param name="inTable">The input table or feature class.</param>
        /// <param name="dropFields">Semicolon-separated list of field names to delete (e.g., "field1;field2;field3").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the fields were successfully deleted, otherwise false.</returns>
        public static async Task<bool> DeleteFieldAsync(string inTable, string dropFields)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there are fields to delete.
            if (String.IsNullOrEmpty(dropFields))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, dropFields);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread;

            //Geoprocessing.OpenToolDialog("management.DeleteField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.DeleteField", parameters, environments, null, null, executeFlags);

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
        /// <param name="inTable">The input table or feature class.</param>
        /// <param name="outTable">The output table.</param>
        /// <param name="statisticsFields">The fields to calculate statistics on.</param>
        /// <param name="caseFields">The fields to use for case grouping.</param>
        /// <param name="concatenationSeparator">The separator for concatenated values.</param>
        /// <param name="addToMap">Whether to add the output to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the summary statistics were successfully calculated, otherwise false.</returns>
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
        /// <param name="inFeatureClass">The input feature class.</param>
        /// <param name="outFeatureClass">The output feature class.</param>
        /// <param name="pointLocation">The location of the point (e.g., "CENTROID").</param>
        /// <param name="addToMap">Whether to add the output to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the analysis was successful, otherwise false.</returns>
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
        /// <param name="inFeatureClass">The input feature class.</param>
        /// <param name="nearFeatureClass">The near feature class.</param>
        /// <param name="searchRadius">The search radius.</param>
        /// <param name="location">The location.</param>
        /// <param name="angle">The angle.</param>
        /// <param name="method">The method.</param>
        /// <param name="fieldNames">The field names.</param>
        /// <param name="distanceUnit">The distance unit.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the analysis was successful, otherwise false.</returns>
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
        /// <param name="fullPath">The full path to the file geodatabase to create.</param>
        /// <returns>The created geodatabase, or null if creation failed.</returns>
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
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the geodatabase was successfully deleted; false otherwise.</returns>
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
        /// <param name="gdbPath">The path to the geodatabase.</param>
        /// <param name="featureClassName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class exists; otherwise, false.</returns>
        public static async Task<bool> FeatureClassExistsGDBAsync(string gdbPath, string featureClassName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(featureClassName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    // Create a FeatureClassDefinition object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(featureClassName);

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
        /// <param name="gdbPath">The path to the geodatabase.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the table exists; otherwise, false.</returns>
        public static async Task<bool> TableExistsGDBAsync(string gdbPath, string tableName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    // Get all table definitions and check if the table name exists
                    IReadOnlyList<TableDefinition> tableDefinitions = geodatabase.GetDefinitions<TableDefinition>();

                    exists = tableDefinitions.Any(td =>
                        td.GetName().Equals(tableName, StringComparison.OrdinalIgnoreCase));
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
        /// <param name="gdbPath">The path to the geodatabase.</param>
        /// <param name="featureClassName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class was deleted; otherwise, false.</returns>
        public static async Task<bool> DeleteGeodatabaseFCAsync(string gdbPath, string featureClassName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(featureClassName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

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
        /// <param name="geodatabase">The geodatabase containing the feature class.</param>
        /// <param name="featureClassName">The name of the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class was deleted; otherwise, false.</returns>
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
        /// <param name="gdbPath">The path to the geodatabase.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the table was deleted; otherwise, false.</returns>
        public static async Task<bool> DeleteGeodatabaseTableAsync(string gdbPath, string tableName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(gdbPath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

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
        /// Delete a table from a geodatabase.
        /// </summary>
        /// <param name="geodatabase">The geodatabase containing the table.</param>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the table was deleted; otherwise, false.</returns>
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
        /// <param name="gdbPath">The path to the geodatabase.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of items deleted.</returns>
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
        /// <param name="gdbPath">The path to the geodatabase containing the table or feature class.</param>
        /// <param name="inTable">The full path to the input table or feature class.</param>
        /// <param name="fieldNames">The field name(s) to index (comma-separated for composite indexes).</param>
        /// <param name="indexName">The name for the new index.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the index was created successfully or already exists, otherwise false.</returns>
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
        /// <param name="targetFeatures">The target feature class.</param>
        /// <param name="joinFeatures">The join feature class.</param>
        /// <param name="outFeatureClass">The output feature class.</param>
        /// <param name="joinOperation">The join operation.</param>
        /// <param name="joinType">The join type.</param>
        /// <param name="fieldMapping">The field mapping.</param>
        /// <param name="matchOption">The match option.</param>
        /// <param name="searchRadius">The search radius.</param>
        /// <param name="distanceField">The distance field.</param>
        /// <param name="matchFields">The match fields.</param>
        /// <param name="addToMap">Whether to add the output to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the analysis was successful, otherwise false.</returns>
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
        /// Permanently join fields from one table/feature class to another table/feature class.
        /// </summary>
        /// <remarks>
        /// This method uses the JoinField geoprocessing tool to permanently add fields from a join table
        /// to a target table or feature class. The fields parameter can be:
        /// - Empty/null: Join ALL fields from the join table
        /// - Semicolon-separated list: Join only specified fields (e.g., "field1;field2;field3")
        /// </remarks>
        /// <param name="inFeatures">The target table or feature class to join fields to.</param>
        /// <param name="inField">The field in the target table used for the join.</param>
        /// <param name="joinFeatures">The join table containing fields to add.</param>
        /// <param name="joinField">The field in the join table used for the join.</param>
        /// <param name="fields">Semicolon-separated list of field names to join, or empty/null to join all fields.</param>
        /// <param name="fmOption">Use field mapping: "USE_FM" or "NOT_USE_FM" (default).</param>
        /// <param name="fieldMapping">Optional field mapping string (only used if fmOption is "USE_FM").</param>
        /// <param name="indexJoinFields">Index join fields: "INDEX" to add index, "NO_INDEX" to skip (default).</param>
        /// <param name="addToMap">If true, add the result to the active map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if successful, otherwise false.</returns>
        public static async Task<bool> JoinFieldsAsync(
            string inFeatures,
            string inField,
            string joinFeatures,
            string joinField,
            string fields = "",
            string fmOption = "NOT_USE_FM",
            string fieldMapping = "",
            string indexJoinFields = "NO_INDEXES",
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

            // Validate indexJoinFields parameter (must be "NEW_INDEXES", "REPLACE_INDEXES", or "NO_INDEXES")
            if (indexJoinFields != "NEW_INDEXES" && indexJoinFields != "REPLACE_INDEXES" && indexJoinFields != "NO_INDEXES")
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
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the duplicate field was deleted or didn't exist, otherwise false.</returns>
        public static async Task<bool> DeleteDuplicateJoinFieldAsync(string featureClassPath, string baseFieldName)
        {
            // Check there is an input feature class path.
            if (String.IsNullOrWhiteSpace(featureClassPath))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrWhiteSpace(baseFieldName))
                return false;

            List<string> duplicateFields = await QueuedTask.Run(() =>
            {
                try
                {
                    string gdbPath = Path.GetDirectoryName(featureClassPath);
                    string fcName = Path.GetFileName(featureClassPath);

                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));

                    using FeatureClass fc = geodatabase.OpenDataset<FeatureClass>(fcName);

                    FeatureClassDefinition def = fc.GetDefinition();
                    IReadOnlyList<Field> fields = def.GetFields();

                    // Find duplicate fields created by JoinField.
                    // The JoinField tool typically appends "_1", "_2", etc. to duplicate field names.
                    return fields
                        .Where(f => f.Name.StartsWith(baseFieldName + "_", StringComparison.OrdinalIgnoreCase))
                        .Select(f => f.Name)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding duplicate join fields: {ex.Message}");
                    return null;
                }
            });

            // If no duplicate fields found or error occurred, return appropriate result.
            if (duplicateFields == null)
                return false;

            if (duplicateFields.Count == 0)
                return true;

            // Delete all duplicate fields using the DeleteField helper method.
            string fieldsToDelete = string.Join(";", duplicateFields);
            return await DeleteFieldAsync(featureClassPath, fieldsToDelete);
        }

        #endregion Joins

        #region Table

        /// <summary>
        /// Check if a feature class exists in the file path.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class exists; otherwise, false.</returns>
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
        /// <param name="fullPath">The full path to the feature class.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the feature class exists, otherwise false.</returns>
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
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the table was successfully created; false otherwise.</returns>
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
        /// Performs a bulk insert of rows into a geodatabase table using batched operations with multiple batches.
        /// </summary>
        /// <param name="gdbPath">Full path to the file geodatabase.</param>
        /// <param name="tableName">Name of the table to insert rows into.</param>
        /// <param name="rows">List of rows to insert, where each row is a dictionary of field name to value.</param>
        /// <remarks>
        /// This method uses InsertCursor for efficient batch insertion. Each row dictionary should contain
        /// field names as keys and field values as objects. DBNull.Value is used for null values.
        /// The method processes rows in batches for optimal performance.
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows successfully inserted across all batches.</returns>
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

        #endregion Table

        #region Outputs

        /// <summary>
        /// Prompt the user to specify an output file in the required format.
        /// </summary>
        /// <param name="fileType">The type of file to save.</param>
        /// <param name="initialDirectory">The initial directory to open in the save dialog.</param>
        /// <returns>The full path to the selected output file, or null if the user cancels.</returns>
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

        #region Geometry

        /// <summary>
        /// Recalculates geometry attributes (Shape_Length and Shape_Area) for a feature class using geodesic measurements.
        /// </summary>
        /// <remarks>
        /// This method uses the Calculate Geometry Attributes geoprocessing tool to recalculate
        /// Shape_Length and Shape_Area fields for all features in the specified feature class.
        /// This is necessary after programmatic feature creation as these fields are not automatically
        /// calculated when using CreateRow().
        /// </remarks>
        /// <param name="featureClassPath">The full path to the feature class (e.g., "C:\data\test.gdb\myfeatures").</param>
        /// <param name="lengthField">The name of the length field.</param>
        /// <param name="areaField">The name of the area field.</param>
        /// <param name="lengthUnit">Length unit. Default is "METERS".</param>
        /// <param name="areaUnit">Area unit. Default is "SQUARE_METERS".</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if successful, otherwise false.</returns>
        public static async Task<bool> RecalculateGeometryAttributesAsync(
            string featureClassPath,
            string lengthField,
            string areaField,
            string lengthUnit = "METERS",
            string areaUnit = "SQUARE_METERS")
        {
            // Check parameters
            if (string.IsNullOrWhiteSpace(featureClassPath))
                return false;

            if (string.IsNullOrEmpty(lengthField) && string.IsNullOrEmpty(areaField))
                return false;

            // Build the geometry property string based on available fields
            List<string> geometryProps = [];

            if (!string.IsNullOrEmpty(lengthField))
                geometryProps.Add($"{lengthField} PERIMETER_LENGTH");

            if (!string.IsNullOrEmpty(areaField))
                geometryProps.Add($"{areaField} AREA {areaUnit}");

            string geometryProperties = string.Join(";", geometryProps);

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(featureClassPath, geometryProperties, lengthUnit, areaUnit);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geoprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.CalculateGeometryAttributes", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CalculateGeometryAttributes", parameters, environments, null, null, executeFlags);

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
        /// Gets the geometry field names (length and area) for a feature class.
        /// </summary>
        /// <param name="featureClassPath">Full path to the feature class or shapefile.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with (lengthFieldName, areaFieldName), or (null, null) if not found.</returns>
        public static async Task<(string lengthField, string areaField)> GetGeometryFieldNamesAsync(string featureClassPath)
        {
            if (string.IsNullOrWhiteSpace(featureClassPath))
                return (null, null);

            return await QueuedTask.Run(() =>
            {
                FeatureClass fc = null;
                Datastore datastore = null;

                try
                {
                    // Determine if it's a shapefile or geodatabase feature class
                    bool isShapefile = featureClassPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);

                    if (isShapefile)
                    {
                        // Open shapefile using FileSystemDatastore
                        string shapefileFolder = Path.GetDirectoryName(featureClassPath);
                        string shapefileName = Path.GetFileNameWithoutExtension(featureClassPath);

                        FileSystemConnectionPath fileConnection = new(new Uri(shapefileFolder), FileSystemDatastoreType.Shapefile);
                        FileSystemDatastore shapefile = new(fileConnection);
                        fc = shapefile.OpenDataset<FeatureClass>(shapefileName);
                        datastore = shapefile;
                    }
                    else
                    {
                        // Open geodatabase feature class
                        string gdbPath = Path.GetDirectoryName(featureClassPath);
                        string fcName = Path.GetFileName(featureClassPath);

                        Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
                        fc = geodatabase.OpenDataset<FeatureClass>(fcName);
                        datastore = geodatabase;
                    }

                    FeatureClassDefinition def = fc.GetDefinition();
                    IReadOnlyList<Field> fields = def.GetFields();

                    // Find length and area fields
                    // Shapefiles typically use "Shape_Leng" and "Shape_Area"
                    // Geodatabases use "Shape_Length" and "Shape_Area"
                    string lengthField = fields.FirstOrDefault(f =>
                        f.Name.Equals("Shape_Length", StringComparison.OrdinalIgnoreCase) ||
                        f.Name.Equals("Shape_Leng", StringComparison.OrdinalIgnoreCase))?.Name;

                    string areaField = fields.FirstOrDefault(f =>
                        f.Name.Equals("Shape_Area", StringComparison.OrdinalIgnoreCase))?.Name;

                    return (lengthField, areaField);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting geometry field names: {ex.Message}");
                    return (null, null);
                }
                finally
                {
                    // Dispose of resources in the correct order
                    fc?.Dispose();
                    datastore?.Dispose();
                }
            });
        }

        #endregion Geometry

        #region Add to Map

        /// <summary>
        /// Adds a feature class or shapefile to the active map.
        /// </summary>
        /// <param name="featureClassPath">The full path to the feature class or shapefile.</param>
        /// <param name="layerName">Optional name for the layer. If null, uses the feature class name.</param>
        /// <param name="groupLayerName">Optional group layer to add the layer to. If null, adds to top level.</param>
        /// <param name="position">Optional position to insert the layer. If -1, adds to top.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the layer was successfully added; false otherwise.</returns>
        public static async Task<bool> AddFeatureLayerToMapAsync(
            string featureClassPath,
            string layerName = null,
            string groupLayerName = null,
            int position = 0)
        {
            // Check there is an input feature class path.
            if (String.IsNullOrWhiteSpace(featureClassPath))
                return false;

            // Check if the active map view exists.
            MapView mapView = MapView.Active;
            if (mapView == null)
                return false;

            try
            {
                return await QueuedTask.Run(() =>
                {
                    // Get the active map.
                    Map activeMap = mapView.Map;
                    if (activeMap == null)
                        return false;

                    // Create a URI for the feature class path.
                    Uri uri = new(featureClassPath);

                    // Determine the layer name (use feature class name if not specified).
                    string finalLayerName = layerName;
                    if (String.IsNullOrWhiteSpace(finalLayerName))
                    {
                        // Extract feature class name from path.
                        finalLayerName = Path.GetFileNameWithoutExtension(featureClassPath);
                    }

                    // Create the feature layer.
                    FeatureLayer newLayer = null;

                    // Check if we should add to a group layer.
                    if (!String.IsNullOrWhiteSpace(groupLayerName))
                    {
                        // Find or create the group layer.
                        GroupLayer groupLayer = activeMap.FindLayers(groupLayerName, true)
                            .OfType<GroupLayer>()
                            .FirstOrDefault();

                        if (groupLayer == null)
                        {
                            // Create the group layer if it doesn't exist.
                            groupLayer = LayerFactory.Instance.CreateGroupLayer(
                                activeMap,
                                0,
                                groupLayerName);
                        }

                        // Create layer parameters for creating within the group layer.
                        LayerCreationParams layerParams = new(uri)
                        {
                            Name = finalLayerName,
                            MapMemberPosition = MapMemberPosition.AddToTop
                        };

                        // Create the feature layer.
                        newLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(
                            layerParams,
                            activeMap);

                        if (newLayer != null)
                        {
                            // Move the layer into the group at the specified position.
                            activeMap.MoveLayer(newLayer, groupLayer, position);
                        }
                    }
                    else
                    {
                        // Create layer parameters for adding to the map.
                        LayerCreationParams layerParams = new(uri)
                        {
                            Name = finalLayerName,
                            MapMemberPosition = position == 0
                                ? MapMemberPosition.AddToTop
                                : MapMemberPosition.AddToBottom
                        };

                        // Create the feature layer at the specified position.
                        newLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(
                            layerParams,
                            activeMap);
                    }

                    return newLayer != null;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding feature layer to map: {ex.Message}");
                return false;
            }
        }

        #endregion Add to Map

        #region CopyFeatures

        /// <summary>
        /// Copy the input feature class to the output feature class.
        /// </summary>
        /// <param name="inFeatureClass">The input feature class.</param>
        /// <param name="outFeatureClass">The output feature class.</param>
        /// <param name="addToMap">Whether to add the output feature class to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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
        /// <param name="inputWorkspace">The input workspace.</param>
        /// <param name="inputDatasetName">The input dataset name.</param>
        /// <param name="outputFeatureClass">The output feature class.</param>
        /// <param name="addToMap">Whether to add the output feature class to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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
        /// <param name="inputWorkspace">The workspace of the input dataset.</param>
        /// <param name="inputDatasetName">The name of the input dataset.</param>
        /// <param name="outputWorkspace">The workspace of the output dataset.</param>
        /// <param name="outputDatasetName">The name of the output dataset.</param>
        /// <param name="addToMap">Whether to add the output dataset to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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
        /// <param name="inTable">The input table.</param>
        /// <param name="outTable">The output table.</param>
        /// <param name="addToMap">Whether to add the output table to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the export operation was successful, otherwise false.</returns>
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
        /// <param name="inTable">The input table.</param>
        /// <param name="outTable">The output table.</param>
        /// <param name="addToMap">Whether to add the output table to the map.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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
        /// <param name="inputWorkspace">The input workspace.</param>
        /// <param name="inputDatasetName">The input dataset name.</param>
        /// <param name="outputTable">The output table.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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
        /// <param name="inputWorkspace">The input workspace.</param>
        /// <param name="inputDatasetName">The input dataset name.</param>
        /// <param name="outputWorkspace">The output workspace.</param>
        /// <param name="outputDatasetName">The output dataset name.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the copy operation was successful, otherwise false.</returns>
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