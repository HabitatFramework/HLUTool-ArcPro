// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using HLU.Data.Connection;

namespace HLU.Helpers
{
    /// <summary>
    /// Static helper class for building SQL queries and WHERE clauses.
    /// </summary>
    public static class QueryBuilder
    {
        #region WHERE Clause Building

        /// <summary>
        /// Builds a SQL WHERE clause from a list of filter conditions.
        /// </summary>
        /// <param name="conditions">The list of SQL filter conditions to combine.</param>
        /// <param name="startWithWhere">If true, prepends "WHERE " to the output.</param>
        /// <returns>A SQL WHERE clause string, or empty string if no conditions.</returns>
        public static string BuildWhereClause(List<SqlFilterCondition> conditions, bool startWithWhere = false)
        {
            if (conditions == null || conditions.Count == 0)
                return string.Empty;

            StringBuilder sb = new();

            if (startWithWhere)
                sb.Append("WHERE ");

            bool first = true;
            foreach (var cond in conditions)
            {
                if (!first && !string.IsNullOrEmpty(cond.BooleanOperator))
                    sb.Append($" {cond.BooleanOperator} ");

                sb.Append(cond.OpenParentheses);
                sb.Append($"{QuoteIdentifier(cond.Column.ColumnName)} {cond.Operator} {QuoteValue(cond.Value, cond.ColumnSystemType)}");
                sb.Append(cond.CloseParentheses);

                first = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds a nested WHERE clause from blocks of filter conditions.
        /// </summary>
        /// <param name="conditionBlocks">A list of condition blocks, where each block is a list of conditions.</param>
        /// <param name="blockOperator">The operator to use between blocks (e.g., "OR").</param>
        /// <returns>A SQL WHERE clause string.</returns>
        public static string BuildWhereClauseFromBlocks(
            List<List<SqlFilterCondition>> conditionBlocks,
            string blockOperator = "OR")
        {
            if (conditionBlocks == null || conditionBlocks.Count == 0)
                return string.Empty;

            StringBuilder sb = new();

            for (int i = 0; i < conditionBlocks.Count; i++)
            {
                if (i > 0)
                    sb.Append($" {blockOperator} ");

                sb.Append($"({BuildWhereClause(conditionBlocks[i])})");
            }

            return sb.ToString();
        }

        #endregion WHERE Clause Building

        #region IN List Building

        /// <summary>
        /// Builds a SQL IN list from a collection of values.
        /// </summary>
        /// <typeparam name="T">The type of values in the collection.</typeparam>
        /// <param name="values">The collection of values.</param>
        /// <returns>A string like "('value1', 'value2', 'value3')".</returns>
        public static string BuildInList<T>(IEnumerable<T> values)
        {
            if (values == null || !values.Any())
                return "()";

            var quotedValues = values.Select(v => QuoteValue(v, typeof(T)));
            return $"({string.Join(", ", quotedValues)})";
        }

        /// <summary>
        /// Builds a SQL IN clause from a column name and collection of values.
        /// </summary>
        /// <typeparam name="T">The type of values in the collection.</typeparam>
        /// <param name="columnName">The column name.</param>
        /// <param name="values">The collection of values.</param>
        /// <returns>A string like "column_name IN ('value1', 'value2')".</returns>
        public static string BuildInClause<T>(string columnName, IEnumerable<T> values)
        {
            if (string.IsNullOrEmpty(columnName) || values == null || !values.Any())
                return string.Empty;

            return $"{QuoteIdentifier(columnName)} IN {BuildInList(values)}";
        }

        #endregion IN List Building

        #region Value Formatting

        /// <summary>
        /// Quotes a value for SQL based on its type.
        /// </summary>
        /// <param name="value">The value to quote.</param>
        /// <param name="valueType">The system type of the value.</param>
        /// <returns>A SQL-safe quoted string representation of the value.</returns>
        public static string QuoteValue(object value, Type valueType)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            // Handle string types
            if (valueType == typeof(string))
            {
                // Escape single quotes by doubling them
                string stringValue = value.ToString().Replace("'", "''");
                return $"'{stringValue}'";
            }

            // Handle date/time types
            if (valueType == typeof(DateTime))
            {
                DateTime dateValue = (DateTime)value;
                return $"'{dateValue:yyyy-MM-dd HH:mm:ss}'";
            }

            // Handle boolean types
            if (valueType == typeof(bool))
            {
                return (bool)value ? "1" : "0";
            }

            // Handle numeric types (no quoting needed)
            if (IsNumericType(valueType))
            {
                return value.ToString();
            }

            // Default: treat as string
            return $"'{value}'";
        }

        /// <summary>
        /// Quotes an identifier (table/column name) for SQL.
        /// </summary>
        /// <param name="identifier">The identifier to quote.</param>
        /// <returns>A quoted identifier (e.g., [TableName] or "TableName").</returns>
        public static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return identifier;

            // Use square brackets for SQL Server-style quoting
            // (You may need to adapt this based on your database backend)
            return $"[{identifier}]";
        }

        /// <summary>
        /// Checks if a type is numeric.
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(decimal) ||
                   type == typeof(double) ||
                   type == typeof(float);
        }

        #endregion Value Formatting

        #region SELECT Query Building

        /// <summary>
        /// Builds a simple SELECT query.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="columns">The columns to select (or "*" if null/empty).</param>
        /// <param name="whereClause">Optional WHERE clause (without "WHERE" keyword).</param>
        /// <param name="orderBy">Optional ORDER BY clause (without "ORDER BY" keyword).</param>
        /// <returns>A complete SELECT query string.</returns>
        public static string BuildSelectQuery(
            string tableName,
            IEnumerable<string> columns = null,
            string whereClause = null,
            string orderBy = null)
        {
            StringBuilder sb = new("SELECT ");

            // Columns
            if (columns == null || !columns.Any())
                sb.Append('*');
            else
                sb.Append(string.Join(", ", columns.Select(QuoteIdentifier)));

            // Table
            sb.Append($" FROM {QuoteIdentifier(tableName)}");

            // WHERE clause
            if (!string.IsNullOrEmpty(whereClause))
                sb.Append($" WHERE {whereClause}");

            // ORDER BY
            if (!string.IsNullOrEmpty(orderBy))
                sb.Append($" ORDER BY {orderBy}");

            return sb.ToString();
        }

        #endregion SELECT Query Building
    }
}