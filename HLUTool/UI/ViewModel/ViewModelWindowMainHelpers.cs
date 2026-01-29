// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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
using System.Text.RegularExpressions;
using HLU.Data.Model;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Helper methods for the main window view model.
    /// </summary>
    static partial class ViewModelWindowMainHelpers
    {
        #region Helpers

        /// <summary>
        /// Converts a selection of GIS-related database rows into a structured SQL WHERE clause.
        /// </summary>
        /// <typeparam name="T">A type that derives from <see cref="DataTable"/> representing the target table.</typeparam>
        /// <param name="selectedRows">An array of <see cref="DataRow"/> objects representing the selected records.</param>
        /// <param name="keyColumOrdinals">An array of column indices that define the primary key or unique identifiers for filtering.</param>
        /// <param name="blockSize">The maximum number of conditions to include in each WHERE clause block.</param>
        /// <param name="targetTable">The target table to which the conditions should be applied.</param>
        /// <returns>
        /// A list of lists, where each inner list represents a block of <see cref="SqlFilterCondition"/> objects that
        /// together form a structured WHERE clause segment.
        /// </returns>
        /// <remarks>
        /// - This method converts selected database rows into SQL filter conditions that can be used
        ///   in WHERE clauses.
        /// - Each row's key columns are converted into filter conditions using the "=" operator.
        /// - The conditions are structured using `OR` and `AND` logic:
        ///   - The first key column condition uses "OR" and an opening parenthesis.
        ///   - Subsequent key conditions use "AND".
        ///   - The last key column closes the parentheses.
        /// - The method groups conditions into blocks based on the specified `blockSize`
        ///   to prevent excessively large SQL queries.
        /// - This helps optimize database queries by chunking selections into manageable parts.
        /// </remarks>
        public static List<List<SqlFilterCondition>> GisSelectionToWhereClause<T>(
            DataRow[] selectedRows, int[] keyColumOrdinals, int blockSize, T targetTable)
            where T : DataTable
        {
            DataTable selectionTable = selectedRows[0].Table;

            List<List<SqlFilterCondition>> whereClause = [];

            int i = 0;
            while (i < selectedRows.Length)
            {
                List<SqlFilterCondition> whereClauseBlock = [];
                int j = i;

                while (j < selectedRows.Length)
                {
                    DataRow r = selectedRows[j];

                    for (int k = 0; k < keyColumOrdinals.Length; k++)
                    {
                        SqlFilterCondition cond = new();

                        if (k == 0)
                        {
                            cond.BooleanOperator = "OR";
                            cond.OpenParentheses = "(";
                        }
                        else
                        {
                            cond.BooleanOperator = "AND";
                            cond.OpenParentheses = String.Empty;
                        }
                        cond.Column = selectionTable.Columns[keyColumOrdinals[k]];
                        cond.Table = targetTable;
                        cond.ColumnSystemType = selectionTable.Columns[k].DataType;
                        cond.Operator = "=";
                        cond.Value = r[keyColumOrdinals[k]];
                        if (k == keyColumOrdinals.Length - 1)
                            cond.CloseParentheses = ")";
                        else
                            cond.CloseParentheses = String.Empty;

                        whereClauseBlock.Add(cond);
                    }

                    j++;
                    if (whereClauseBlock.Count >= blockSize)
                        break;
                }

                if (whereClauseBlock.Count > 0)
                    whereClause.Add(whereClauseBlock);

                i = j;
            }

            return whereClause;
        }

        /// <summary>
        /// Converts a list of incident identifiers into structured SQL WHERE clause conditions,
        /// grouped into blocks of a specified size.
        /// </summary>
        /// <typeparam name="T">A type that derives from <see cref="DataTable"/> representing the target table.</typeparam>
        /// <param name="incidPageSize">The maximum number of conditions per block to prevent excessively large queries.</param>
        /// <param name="incidOrdinal">The column index of the incident identifier within the target table.</param>
        /// <param name="incidTable">The target table to which the conditions should be applied.</param>
        /// <param name="incidList">A collection of incident identifiers to be converted into SQL filter conditions.</param>
        /// <returns>
        /// A list of lists, where each inner list represents a block of <see cref="SqlFilterCondition"/> objects
        /// that together form a structured WHERE clause segment. Returns <c>null</c> if the input list is empty or null.
        /// </returns>
        /// <remarks>
        /// - Each entry in `incidList` is converted into a <see cref="SqlFilterCondition"/> using the "OR" operator.
        /// - The method divides the conditions into blocks of `incidPageSize` to optimize query performance.
        /// - The LINQ query groups conditions into blocks by calculating `index / incidPageSize`,
        ///   ensuring that each block contains up to `incidPageSize` elements.
        /// - The grouped conditions are returned as a list of lists, where each inner list represents
        ///   a WHERE clause segment.
        /// - This helps structure large selections into manageable SQL queries.
        /// </remarks>
        public static List<List<SqlFilterCondition>> IncidSelectionToWhereClause<T>(int incidPageSize,
            int incidOrdinal, T incidTable, IEnumerable<string> incidList) where T : DataTable
        {
            // If the incident list is null or empty, return null.
            if ((incidList == null) || (!incidList.Any())) return null;

            // Sort incids to provide consistent UI ordering and stable paging.
            IEnumerable<string> orderedIncids = incidList.OrderBy(i => i);

            // Group the incident identifiers into blocks of `incidPageSize` conditions.
            return (from b in incidList.Select((i, index) => new
            {
                // Determine the block number based on the index and page size.
                Block = index / incidPageSize,

                // Create a new filter condition for each incident identifier.
                Condition = new SqlFilterCondition("OR", incidTable, incidTable.Columns[incidOrdinal], i)
            })
                    // Group the conditions by block number.
                    group b by b.Block into g
                    select g.Select(b => b.Condition).ToList()).ToList();
        }

        /// <summary>
        /// Get the IHS summary for a given array of IHS codes.
        /// </summary>
        /// <param name="ihsCodes"></param>
        /// <returns></returns>
        public static string IhsSummary(string[] ihsCodes)
        {
            // Concatenate the IHS codes into a single string separated by full stops.
            StringBuilder buildSummary = new StringBuilder().Append(String.Join(".", ihsCodes.Where(c => !String.IsNullOrEmpty(c))));

            return buildSummary.ToString();
        }

        /// <summary>
        /// Check if a row is dirty.
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="row"></param>
        /// <returns></returns>
        public static bool RowIsDirty<R>(R row)
            where R : DataRow
        {
            // If the row is not null and is not unchanged or detached
            // (i.e. it has been added, deleted or modified)

            //return row != null && row.RowState != DataRowState.Unchanged &&
            //    row.RowState != DataRowState.Detached;
            if (row != null && row.RowState != DataRowState.Unchanged &&
                row.RowState != DataRowState.Detached)
            {
                // If the row has been modified then compare the original and
                // current values of every column to see if they are dirty.
                if (row.RowState == DataRowState.Modified)
                {
                    foreach (DataColumn dc in row.Table.Columns)
                    {
                        string a = row[dc, DataRowVersion.Original].ToString();
                        string b = row[dc, DataRowVersion.Current].ToString();
                        if (!row[dc, DataRowVersion.Original].Equals(
                             row[dc, DataRowVersion.Current]))
                            return true;
                    }
                }
                else
                    // If the row has been added or deleted then it must be
                    // dirty.
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get the operations code for a given operation description.
        /// </summary>
        /// <param name="hluDS"></param>
        /// <param name="modifyOperation"></param>
        /// <returns></returns>
        public static string GetOperationsCode(HluDataSet hluDS, Operations modifyOperation)
        {
            // If the hluDS is null or the lut_operation is null then return null.
            if ((hluDS == null) || (hluDS.lut_operation == null)) return null;

            // Get the operation name from the enum value.
            string operationName = Enum.GetName(typeof(Operations), modifyOperation);

            // Create a regex pattern that matches the operation name with any
            string descriptionPattern = string.Join(@"\s*", CapitalisedRegex().Matches(operationName).Cast<Match>()
                .Select(m => operationName.Substring(m.Index, m.Length))) + @"\s*";

            // Find the operation code that matches the description pattern.
            var o = hluDS.lut_operation
                .Where(r => Regex.IsMatch(r.description, descriptionPattern, RegexOptions.IgnoreCase));

            // If there is exactly one match then return the code.
            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        /// <summary>
        /// Get the reason code for a given reason description.
        /// </summary>
        /// <param name="hluDS"></param>
        /// <param name="reasonDescription"></param>
        /// <returns></returns>
        public static string GetReasonCode(HluDataSet hluDS, string reasonDescription)
        {
            // If the hluDS is null or the lut_reason is null then return null.
            if ((hluDS == null) || (hluDS.lut_reason == null)) return null;

            // Find the reason code that matches the description.
            var o = hluDS.lut_reason
                .Where(r => r.description == reasonDescription);

            // If there is exactly one match then return the code.
            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        /// <summary>
        /// Get the process code for a given process description.
        /// </summary>
        /// <param name="hluDS"></param>
        /// <param name="processDescription"></param>
        /// <returns></returns>
        public static string GetProcessCode(HluDataSet hluDS, string processDescription)
        {
            // If the hluDS is null or the lut_process is null then return null.
            if ((hluDS == null) || (hluDS.lut_process == null)) return null;

            // Find the process code that matches the description.
            var o = hluDS.lut_process
                .Where(r => r.description == processDescription);

            // If there is exactly one match then return the code.
            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        #endregion Helpers

        #region Regex Definitions

        /// <summary>
        /// Defines a compiled regular expression that matches capitalized words in a string.
        /// </summary>
        /// <remarks>
        /// - The pattern `[A-Z][^A-Z]*` matches:
        ///   - An uppercase letter (`[A-Z]`) at the beginning of a word.
        ///   - Followed by zero or more non-uppercase letters (`[^A-Z]*`).
        /// - This effectively extracts words that start with a capital letter and continue until
        ///   the next capital letter is encountered.
        /// - The `[GeneratedRegex]` attribute compiles the regex at compile time for performance benefits.
        /// </remarks>
        /// <returns>A `Regex` instance that can be used to match capitalized words in a string.</returns>
        [GeneratedRegex("[A-Z][^A-Z]*")]
        private static partial Regex CapitalisedRegex();

        #endregion Regex Definitions
    }
}