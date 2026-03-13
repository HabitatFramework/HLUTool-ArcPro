// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013 Thames Valley Environmental Records Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Properties;

namespace HLU.GISApplication
{
    //TODO: Move these methods to a more general utility class as they are not specific to the scratch database. Also, consider whether the GisWhereClause method could be refactored to be more efficient and easier to read.
    /// <summary>
    /// Contains methods for working with the scratch database, which is used to store the Incid and
    /// Incid_MM tables that are used to filter GIS data based on a selection of Incids.
    /// </summary>
    static class ScratchDb
    {
        // Static variables for storing the Incid and Incid_MM tables from the scratch database.
        // These are used by the GisWhereClause method to determine which table to base the filter
        // conditions on.
        private static HluDataSet.incidDataTable _incidTable = null;
        private static HluDataSet.incid_mm_polygonsDataTable _incidMMTable = null;

        /// <summary>
        /// Create a list of sql filter conditions using a table of selected Incids.
        /// </summary>
        /// <param name="incidSelection">A DataTable containing the selected Incids.</param>
        /// <param name="gisApp">Which GIS system is being used.</param>
        /// <returns>
        /// A list of sql filter conditions.
        /// </returns>
        public static List<SqlFilterCondition> GisWhereClause(DataTable incidSelection, ArcProApp gisApp, bool useIncidTable)
        {
            List<SqlFilterCondition> whereClause = [];
            SqlFilterCondition cond = new();

            //StringBuilder incidList = new();

            // Split the table of selected Incids into chunks of continuous Incids so
            // that each chunk contains a continuous series of one or more Incids.
            var query = incidSelection
                .AsEnumerable()
                .Where(r => !String.IsNullOrWhiteSpace(r.Field<string>(0)))
                .Select((r, index) => new
                {
                    RowIndex = RecordIds.IncidNumber(r.Field<string>(0)) - index,
                    Incid = r.Field<string>(0)
                })
                .ChunkBy(r => r.RowIndex);


            // Create a temporary list for storing some of the Incids.
            List<string> inList = [];

            // Determine which table to base the conditions on.
            DataTable condTable;
            if (useIncidTable)
                condTable = _incidTable;
            else
                condTable = _incidMMTable;

            // Loop through each chunk/series of Incids. If there are at least three
            // then it is worth processing them within ">=" and "<=" operators (as
            // this keeps the filter conditions short). If there are only one or two
            // Incids in the chunk then add them to the temporary 'inList' for
            // processing later.
            foreach (var item in query)
            {
                if (item.Count() < 3)
                {
                    if (gisApp != null)
                        inList.AddRange(item.Select(t => gisApp.QuoteValue(t.Incid)));
                    else
                        inList.AddRange(item.Select(t => t.Incid));
                }
                else
                {
                    cond = new()
                    {
                        BooleanOperator = "OR",
                        OpenParentheses = "(",
                        Column = _incidMMTable.incidColumn,
                        Table = condTable,
                        ColumnSystemType = _incidMMTable.incidColumn.DataType,
                        Operator = ">=",
                        Value = item.First().Incid,
                        CloseParentheses = String.Empty
                    };
                    whereClause.Add(cond);

                    cond = new()
                    {
                        BooleanOperator = "AND",
                        OpenParentheses = String.Empty,
                        Column = _incidMMTable.incidColumn,
                        Table = condTable,
                        ColumnSystemType = _incidMMTable.incidColumn.DataType,
                        Operator = "<=",
                        Value = item.Last().Incid,
                        CloseParentheses = ")"
                    };
                    whereClause.Add(cond);
                }
            }

            // Any Incids that are not part of a continuous series of at least
            // three Incid must be queried using an "=" or "IN" sql operator.
            // So loop through all these Incids and lump them together into
            // strings of 254 Incids at a time so that each string can be used
            // in an "IN" statement.
            int i = 0;
            while (i < inList.Count)
            {
                int numElems = i < inList.Count - 254 ? 254 : inList.Count - i;
                string[] oneList = new string[numElems];
                inList.CopyTo(i, oneList, 0, numElems);

                cond = new()
                {
                    BooleanOperator = "OR",
                    OpenParentheses = "(",
                    Column = _incidMMTable.incidColumn,
                    Table = condTable,
                    ColumnSystemType = _incidMMTable.incidColumn.DataType
                };

                // Use " INCID =" in SQL statement instrad of "INCID IN ()"
                // if there is only on item in the list (as it is much quicker)
                if (inList.Count == 1)
                    cond.Operator = "=";
                else
                    cond.Operator = "IN ()";
                cond.Value = String.Join(",", oneList);
                cond.CloseParentheses = ")";
                whereClause.Add(cond);

                i += numElems;
            }

            return whereClause;
        }

        /// <summary>
        /// Builds a UNION query from a list of SqlFilterCondition.
        /// All constituent SELECT statement use the same target list and FROM clause but different WHERE clauses.
        /// </summary>
        /// <param name="targetList">Target list for union query. Same for each select query.</param>
        /// <param name="fromClause">From clause for union query. Same for each select query.</param>
        /// <param name="sortOrdinals">Ordinals of columns by which to order output.</param>
        /// <param name="IncidSelectionWhereClause">List of where clauses from which to build UNION query. Input is assumed to be 0 based.</param>
        /// <param name="db">Database against which UNION query will be run.</param>
        /// <param name="tableAliases">Optional dictionary mapping qualified table names to their aliases in the FROM clause.</param>
        /// <returns>The complete SQL query string.</returns>
        public static string UnionQuery(
            string targetList,
            string fromClause,
            int[] sortOrdinals,
            List<SqlFilterCondition> IncidSelectionWhereClause,
            DbBase db,
            Dictionary<string, string> tableAliases = null)
        {
            // Add order by from list of sort ordinals.
            StringBuilder sql = new();

            // If table aliases are provided, replace table names with aliases in the WHERE conditions
            List<SqlFilterCondition> adjustedWhereClause = IncidSelectionWhereClause;
            if (tableAliases != null && tableAliases.Count > 0)
            {
                adjustedWhereClause = [.. IncidSelectionWhereClause.Select(cond =>
                {
                    // Create a copy of the condition
                    SqlFilterCondition newCond = new(cond.BooleanOperator, cond.Table, cond.Column, cond.Value)
                    {
                        OpenParentheses = cond.OpenParentheses,
                        CloseParentheses = cond.CloseParentheses,
                        Operator = cond.Operator,
                        ColumnSystemType = cond.ColumnSystemType
                    };

                    // If the condition's table has an alias, replace the table reference
                    if (cond.Table != null)
                    {
                        string qualifiedTableName = db.QualifyTableName(cond.Table.TableName);
                        if (tableAliases.TryGetValue(qualifiedTableName, out string alias))
                        {
                            // Create a clone of the table with the alias as the table name
                            // This is a workaround - we're using a temporary DataTable just to hold the alias
                            DataTable aliasedTable = new(alias);
                            foreach (DataColumn col in cond.Table.Columns)
                            {
                                aliasedTable.Columns.Add(col.ColumnName, col.DataType);
                            }
                            newCond.Table = aliasedTable;
                        }
                    }

                    return newCond;
                })];
            }

            // Sort negative sortOrdinals in descending order
            sql.Append(String.Format("SELECT {0} FROM {1}{2}", targetList, fromClause, db.WhereClause(true, true, true, adjustedWhereClause)));
            if (sortOrdinals != null)
                sql.Append(String.Format(" ORDER BY {0}", string.Join(", ", [.. sortOrdinals.Select(x => x < 0 ? String.Format("{0} DESC", Math.Abs(x).ToString()) : x.ToString())])));

            return sql.ToString();
        }
    }
}