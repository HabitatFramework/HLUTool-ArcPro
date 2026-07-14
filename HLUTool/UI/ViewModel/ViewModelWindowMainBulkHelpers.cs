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

using ArcGIS.Core.Geometry;
using HLU.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using CommandType = System.Data.CommandType;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Provides shared helper methods for Bulk Load and Bulk Unload operations.
    /// </summary>
    internal static class ViewModelWindowMainBulkHelpers
    {
        #region Geometry helpers

        /// <summary>
        /// Computes the two geometry history values.
        /// Polygons: (length, area). Polylines: (length, -1). Points: (X, Y).
        /// </summary>
        /// <param name="geometry">The geometry to compute the history values for.</param>
        internal static (double Geom1, double Geom2) GetGeometryHistoryValues(Geometry geometry)
        {
            // If the geometry is null, return (-1, -1) to indicate that the geometry history values are not available.
            if (geometry == null)
                return (-1, -1);

            // Compute the geometry history values based on the geometry type.
            switch (geometry.GeometryType)
            {
                case GeometryType.Polygon:
                    return (GeometryEngine.Instance.Length(geometry), GeometryEngine.Instance.Area(geometry));

                case GeometryType.Polyline:
                    return (GeometryEngine.Instance.Length(geometry), -1);

                case GeometryType.Point:
                    MapPoint p = (MapPoint)geometry;
                    return (p.X, p.Y);

                default:
                    return (-1, -1);
            }
        }

        #endregion Geometry helpers

        #region OSMM xref helpers

        /// <summary>
        /// Checks whether a habitat code (primary or secondary) is valid for the specified geometry type
        /// by querying the appropriate lookup table (<c>lut_primary</c> or <c>lut_secondary</c>)
        /// and checking the <c>is_poly</c>, <c>is_line</c>, and <c>is_point</c> flags.
        /// </summary>
        /// <param name="viewModelMain">The main view model instance.</param>
        /// <param name="code">The habitat code to validate.</param>
        /// <param name="geometryType">The geometry type to validate against.</param>
        /// <param name="isPrimary">True to check <c>lut_primary</c>; false to check <c>lut_secondary</c>.</param>
        /// <returns>True if the code is valid for the geometry type; otherwise false.</returns>
        internal static bool IsHabitatCodeValidForGeometryType(
            ViewModelWindowMain viewModelMain,
            string code,
            HluGeometryTypes geometryType,
            bool isPrimary)
        {
            // If the code is null or whitespace, consider it valid (it won't be written to the database).
            if (string.IsNullOrWhiteSpace(code))
                return true;

            // Set up the database connection and query the appropriate lookup table for the code.
            var db = viewModelMain.DataBase;
            string tableName = isPrimary
                ? viewModelMain.HluDataset.lut_primary.TableName
                : viewModelMain.HluDataset.lut_secondary.TableName;

            // Set up the SQL query to check the code against the lookup table.
            string qualTable = db.QualifyTableName(tableName);
            string codeColumn = db.QuoteIdentifier("code");
            string isPolyColumn = db.QuoteIdentifier("polygon");
            string isLineColumn = db.QuoteIdentifier("line");
            string isPointColumn = db.QuoteIdentifier("point");

            string sql = string.Format(
                "SELECT {0}, {1}, {2} FROM {3} WHERE {4} = {5}",
                isPolyColumn, isLineColumn, isPointColumn,
                qualTable,
                codeColumn,
                db.QuoteValue(code));

            IDataReader reader = null;
            try
            {
                // Execute the query and read the results to determine if the code is valid for the specified geometry type.
                reader = db.ExecuteReader(sql, db.Connection.ConnectionTimeout, CommandType.Text);
                if (reader == null || !reader.Read())
                    return false; // Code not found in lookup table

                // Get the boolean flags for polygon, line, and point from the reader, handling DBNull values.
                bool isPoly = !reader.IsDBNull(0) && reader.GetBoolean(0);
                bool isLine = !reader.IsDBNull(1) && reader.GetBoolean(1);
                bool isPoint = !reader.IsDBNull(2) && reader.GetBoolean(2);

                // Return true if the code is valid for the specified geometry type; otherwise false.
                return geometryType switch
                {
                    HluGeometryTypes.Polygon => isPoly,
                    HluGeometryTypes.Line => isLine,
                    HluGeometryTypes.Point => isPoint,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
            finally
            {
                reader?.Close();
            }
        }

        /// <summary>
        /// Queries <c>lut_osmm_habitat_xref</c> via SQL and returns a dictionary keyed by the
        /// five OSMM attribute values (<c>make</c>, <c>desc_group</c>, <c>desc_term</c>,
        /// <c>theme</c>, <c>feat_code</c>) that maps to the resolved
        /// (<c>osmm_xref_id</c>, <c>habitat_primary</c>, <c>habitat_secondaries</c>) tuple.
        /// <para>
        /// Empty-string and <c>NULL</c> column values are both normalised to <see cref="string.Empty"/>
        /// so that the dictionary key comparison is consistent with the values read from the GIS layer.
        /// </para>
        /// </summary>
        internal static Dictionary<(string make, string descGroup, string descTerm, string theme, string featCode),
                           (int xrefId, string habprimary, string habsecond)> BuildXrefCache(ViewModelWindowMain viewModelMain)
        {
            // Create a case-insensitive dictionary to hold the xref cache, using a custom comparer
            // for the five-element tuple keys.
            var cache = new Dictionary<(string, string, string, string, string), (int, string, string)>(
                new TupleOrdinalIgnoreCaseComparer());

            var db = viewModelMain.DataBase;
            var xrefTable = viewModelMain.HluDataset.lut_osmm_habitat_xref;

            // Column names as they appear in the real database table.
            string qualTable = db.QualifyTableName(xrefTable.TableName);
            string sql = string.Format(
                "SELECT {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7} FROM {8}",
                db.QuoteIdentifier("osmm_xref_id"),
                db.QuoteIdentifier("make"),
                db.QuoteIdentifier("desc_group"),
                db.QuoteIdentifier("desc_term"),
                db.QuoteIdentifier("theme"),
                db.QuoteIdentifier("feat_code"),
                db.QuoteIdentifier("habitat_primary"),
                db.QuoteIdentifier("habitat_secondaries"),
                qualTable);

            // Execute the query and read the results to populate the xref cache.
            IDataReader reader = db.ExecuteReader(sql, db.Connection.ConnectionTimeout, CommandType.Text);
            if (reader == null)
                return cache;

            // Define a local function to normalize values read from the database, converting nulls and DBNulls to empty strings.
            try
            {
                static string Norm(object v) =>
                    (v == null || v is DBNull) ? string.Empty : v.ToString().Trim();

                while (reader.Read())
                {
                    int xrefId = reader.GetInt32(0);
                    var key = (Norm(reader[1]), Norm(reader[2]), Norm(reader[3]),
                               Norm(reader[4]), Norm(reader[5]));
                    string habprimary = Norm(reader[6]);
                    string habsecond = Norm(reader[7]);

                    // Keep first matching row only.
                    cache.TryAdd(key, (xrefId, habprimary, habsecond));
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
        internal sealed class TupleOrdinalIgnoreCaseComparer
            : IEqualityComparer<(string, string, string, string, string)>
        {
            /// <summary>
            /// Determines whether the specified five-element string tuples are equal, ignoring case.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public bool Equals((string, string, string, string, string) x,
                               (string, string, string, string, string) y) =>
                string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item4, y.Item4, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Item5, y.Item5, StringComparison.OrdinalIgnoreCase);

            /// <summary>
            /// Returns a hash code for the specified five-element string tuple, ignoring case.
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public int GetHashCode((string, string, string, string, string) obj) =>
                HashCode.Combine(
                    obj.Item1?.ToUpperInvariant(),
                    obj.Item2?.ToUpperInvariant(),
                    obj.Item3?.ToUpperInvariant(),
                    obj.Item4?.ToUpperInvariant(),
                    obj.Item5?.ToUpperInvariant());
        }

        #endregion OSMM xref helpers
    }
}