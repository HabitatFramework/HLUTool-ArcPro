using System;
using System.Collections.Generic;
using ArcGIS.Core.Data;

namespace HLU.GISApplication
{
    /// <summary>
    /// Provides helper methods for safely working with rows/features retrieved by ObjectID.
    /// </summary>
    internal static class ArcGISProHelpers
    {
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
    }
}