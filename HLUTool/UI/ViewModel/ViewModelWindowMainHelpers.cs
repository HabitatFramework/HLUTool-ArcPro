﻿// HLUTool is used to view and maintain habitat and land use GIS data.
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
    static partial class ViewModelWindowMainHelpers
    {
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

        public static List<List<SqlFilterCondition>> IncidSelectionToWhereClause<T>(int incidPageSize,
            int incidOrdinal, T incidTable, IEnumerable<string> incidList) where T : DataTable
        {
            if ((incidList == null) || (!incidList.Any())) return null;

            return (from b in incidList.Select((i, index) => new
            {
                Block = index / incidPageSize,
                Condition = new SqlFilterCondition("OR", incidTable, incidTable.Columns[incidOrdinal], i)
            })
                    group b by b.Block into g
                    select g.Select(b => b.Condition).ToList()).ToList();
        }

        public static string IhsSummary(string[] ihsCodes)
        {
            //DONE: Aggregate
            StringBuilder buildSummary = new StringBuilder().Append(String.Join(".", ihsCodes.Where(c => !String.IsNullOrEmpty(c))));

            //StringBuilder buildSummary = ihsCodes.Where(c => !String.IsNullOrEmpty(c))
            //    .Aggregate(new(), (sb, c) => sb.Append(String.Format(".{0}", c)));

            return buildSummary.ToString();
        }

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

        public static string GetOperationsCode(HluDataSet hluDS, Operations modifyOperation)
        {
            if ((hluDS == null) || (hluDS.lut_operation == null)) return null;

            string operationName = Enum.GetName(typeof(Operations), modifyOperation);

            //DONE: Aggregate
            string descriptionPattern = string.Join(@"\s*", CapitalisedRegex().Matches(operationName).Cast<Match>()
                .Select(m => operationName.Substring(m.Index, m.Length))
                .Append(@"\s*").ToString());

            //string descriptionPattern = CapitalisedRegex().Matches(operationName).Cast<Match>()
            //    .Aggregate(new(), (sb, m) => sb.Append(@"\s*" + operationName.Substring(m.Index, m.Length)))
            //    .Append(@"\s*").ToString();

            var o = hluDS.lut_operation
                .Where(r => Regex.IsMatch(r.description, descriptionPattern, RegexOptions.IgnoreCase));
            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        public static string GetReasonCode(HluDataSet hluDS, string reasonDescription)
        {
            if ((hluDS == null) || (hluDS.lut_reason == null)) return null;

            var o = hluDS.lut_reason
                .Where(r => r.description == reasonDescription);

            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        public static string GetProcessCode(HluDataSet hluDS, string processDescription)
        {
            if ((hluDS == null) || (hluDS.lut_process == null)) return null;

            var o = hluDS.lut_reason
                .Where(r => r.description == processDescription);

            if (o.Count() == 1)
                return o.First().code;
            else
                return null;
        }

        [GeneratedRegex("[A-Z][^A-Z]*")]
        private static partial Regex CapitalisedRegex();
    }
}
