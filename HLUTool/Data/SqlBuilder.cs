// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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

using HLU.Data.Connection;
using HLU.Helpers;
using HLU.Properties;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HLU.Data
{
    /// <summary>
    /// Represents a condition in a SQL where clause.
    /// </summary>
    public class SqlFilterCondition : ICloneable
    {
        #region Fields

        private string _booleanOperator;
        private string _openParentheses;
        private DataTable _table;
        private DataColumn _column;
        private Type _columnSystemType;
        private string _operator;
        private object _value;
        private string _closeParentheses;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the SqlFilterCondition class with default values.
        /// </summary>
        public SqlFilterCondition()
        {
            SetDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the SqlFilterCondition class using the specified table, column, and filter
        /// value.
        /// </summary>
        /// <param name="table">The DataTable to which the filter condition will be applied. Cannot be null.</param>
        /// <param name="column">The DataColumn within the table to filter on. Cannot be null and must belong to the specified table.</param>
        /// <param name="value">The value to compare against the specified column when evaluating the filter condition. May be null to
        /// represent a database null value.</param>
        public SqlFilterCondition(DataTable table, DataColumn column, object value)
        {
            _table = table;
            _column = column;
            _value = value;
            SetDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the SqlFilterCondition class with the specified boolean operator, data table,
        /// column, and value to filter on.
        /// </summary>
        /// <param name="booleanOp">The logical operator to use for the filter condition, such as "AND" or "OR". Cannot be null or empty.</param>
        /// <param name="table">The DataTable that contains the column to be filtered. Cannot be null.</param>
        /// <param name="column">The DataColumn within the specified table to apply the filter condition to. Cannot be null.</param>
        /// <param name="value">The value to compare against the specified column in the filter condition. May be null to represent a
        /// database null value.</param>
        public SqlFilterCondition(string booleanOp, DataTable table, DataColumn column, object value)
        {
            _booleanOperator = booleanOp;
            _table = table;
            _column = column;
            _value = value;
            SetDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the SqlFilterCondition class using the specified logical operator, table,
        /// column, data type, parentheses, and filter value.
        /// </summary>
        /// <param name="booleanOp">The logical operator to use for combining this filter condition with others. Common values include "AND" or
        /// "OR".</param>
        /// <param name="table">The DataTable that contains the column to which the filter condition applies. Cannot be null.</param>
        /// <param name="column">The DataColumn within the specified table that the filter condition targets. Cannot be null.</param>
        /// <param name="systemDataType">The .NET type of the column's data, used to interpret and compare the filter value. Cannot be null.</param>
        /// <param name="openParentheses">A string representing any opening parentheses to include before the condition in the generated SQL
        /// expression. Can be empty if no parentheses are needed.</param>
        /// <param name="closeParentheses">A string representing any closing parentheses to include after the condition in the generated SQL
        /// expression. Can be empty if no parentheses are needed.</param>
        /// <param name="value">The value to compare against the column in the filter condition. The type should be compatible with the
        /// column's data type.</param>
        public SqlFilterCondition(string booleanOp, DataTable table, DataColumn column, Type systemDataType, string openParentheses, string closeParentheses, object value)
        {
            _booleanOperator = booleanOp;
            _table = table;
            _column = column;
            _columnSystemType = systemDataType;
            _openParentheses = openParentheses;
            _closeParentheses = closeParentheses;
            _value = value;
            SetDefaults();
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Initializes internal fields to their default values.
        /// </summary>
        /// <remarks>This method resets the internal state of the object to ensure consistent default
        /// behavior. It should be called during object initialization to guarantee that all relevant fields are set to
        /// their intended defaults.</remarks>
        private void SetDefaults()
        {
            if (string.IsNullOrEmpty(_booleanOperator))
                _booleanOperator = "AND";
            if (string.IsNullOrEmpty(_openParentheses))
                _openParentheses = string.Empty;
            _operator = "=";
            if (string.IsNullOrEmpty(_closeParentheses))
                _closeParentheses = string.Empty;
        }

        /// <summary>
        /// Creates a new object that is a copy of the current SqlFilterCondition instance.
        /// </summary>
        /// <remarks>The cloned object is a deep copy, so changes to the clone do not affect the original
        /// instance.</remarks>
        /// <returns>A new SqlFilterCondition object that is a copy of this instance.</returns>
        public SqlFilterCondition Clone()
        {
            return (SqlFilterCondition)((ICloneable)this).Clone();
        }

        /// <summary>
        /// Creates a shallow copy of the current object.
        /// </summary>
        /// <remarks>The cloned object will have the same values for its fields as the original object.
        /// Reference-type fields are copied as references, so the original and the clone will reference the same
        /// objects for those fields.</remarks>
        /// <returns>A new object that is a shallow copy of this instance.</returns>
        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// Gets or sets the boolean operator. Defaults to "AND".
        /// </summary>
        /// <value>The logical operator used to combine this filter condition with others, such as "AND" or "OR".</value>
        public string BooleanOperator
        {
            get
            {
                return _booleanOperator;
            }
            set
            {
                _booleanOperator = value;
            }
        }

        /// <summary>
        /// Gets or sets the opening parentheses. Defaults to String.Empty.
        /// </summary>
        /// <value>
        /// A string representing any opening parentheses to include before the condition in the
        /// generated SQL expression. Can be empty if no parentheses are needed.
        /// </value>
        public string OpenParentheses
        {
            get
            {
                return _openParentheses;
            }
            set
            {
                _openParentheses = value;
            }
        }

        /// <summary>
        /// Gets or sets the underlying data table associated with this instance.
        /// </summary>
        /// <value>The DataTable that contains the column to which this filter condition applies.</value>
        public DataTable Table
        {
            get
            {
                return _table;
            }
            set
            {
                _table = value;
            }
        }

        /// <summary>
        /// Gets or sets the data column associated with this instance.
        /// </summary>
        /// <value>The DataColumn within the specified table that this filter condition targets.</value>
        public DataColumn Column
        {
            get
            {
                return _column;
            }
            set
            {
                _column = value;
            }
        }

        /// <summary>
        /// Gets or sets the operator. Defaults to "=".
        /// </summary>
        /// <value>The operator used in the filter condition, such as "=", "<", ">", etc.</value>
        public string Operator
        {
            get
            {
                return _operator;
            }
            set
            {
                _operator = value;
            }
        }

        /// <summary>
        /// Defaults to this.Column.DataType but can be set to typeof(DataColumn) to model table relations.
        /// </summary>
        /// <value>
        /// The .NET type of the column's data, used to interpret and compare the filter value.
        /// Defaults to the data type of the associated column, but can be set to typeof(DataColumn)
        /// to represent table relations.
        /// </value>
        public Type ColumnSystemType
        {
            get
            {
                return _columnSystemType ?? Column.DataType;
            }
            set
            {
                _columnSystemType = value;
            }
        }

        /// <summary>
        /// Gets or sets the value associated with this instance.
        /// </summary>
        /// <value>
        /// The value to compare against the column in the filter condition. The type should be
        /// compatible with the column's data type.
        /// </value>
        public object Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        /// <summary>
        /// Gets or sets the closing parentheses. Defaults to String.Empty.
        /// </summary>
        /// <value></value>
        /// A string representing any closing parentheses to include after the condition in the
        /// generated SQL expression. Can be empty if no parentheses are needed.
        /// </value>
        public string CloseParentheses
        {
            get
            {
                return _closeParentheses;
            }
            set
            {
                _closeParentheses = value;
            }
        }

        #endregion Methods
    }

    /// <summary>
    /// Abstract class to build SQL statements for different database types.
    /// </summary>
    public abstract class SqlBuilder
    {
        #region Abstract

        public abstract string QuotePrefix
        {
            get;
        }

        public abstract string QuoteSuffix
        {
            get;
        }

        public abstract string StringLiteralDelimiter
        {
            get;
        }

        public abstract string DateLiteralPrefix
        {
            get;
        }

        public abstract string DateLiteralSuffix
        {
            get;
        }

        public abstract string WildcardSingleMatch
        {
            get;
        }

        public abstract string WildcardManyMatch
        {
            get;
        }

        public abstract string ConcatenateOperator
        {
            get;
        }

        public abstract string QuoteValue(object value);

        public abstract DataTable SqlSelect(bool selectDistinct, DataColumn[] targetColumns, List<SqlFilterCondition> whereConds);

        public abstract DataTable SqlSelect(bool selectDistinct, DataTable[] targetTables, List<SqlFilterCondition> whereConds);

        #endregion Abstract

        #region Protected

        protected Dictionary<Type, int> _typeMapSystemToSQL;

        protected Dictionary<int, Type> _typeMapSQLToSystem;

        /// <summary>
        /// Maps a database type code to its corresponding .NET system type.
        /// </summary>
        /// <remarks>This method is typically used to translate database-specific type codes to .NET types
        /// for data conversion or schema mapping purposes.</remarks>
        /// <param name="dbTypeCode">The integer code representing the database type to be mapped.</param>
        /// <returns>The .NET <see cref="Type"/> that corresponds to the specified database type code. Returns <see
        /// cref="Type.Missing"/> if the code is not recognized.</returns>
        protected Type DbToSystemType(int dbTypeCode)
        {
            Type sysType;
            if (_typeMapSQLToSystem.TryGetValue(dbTypeCode, out sysType))
                return sysType;
            else
                return (Type)Type.Missing;
        }

        /// <summary>
        /// Maps a .NET type to its corresponding database type code.
        /// </summary>
        /// <param name="sysType">The .NET type to map to a database type code. Cannot be null.</param>
        /// <returns>An integer representing the database type code corresponding to the specified .NET type, or -1 if the type
        /// is not mapped.</returns>
        protected int SystemToDbType(Type sysType)
        {
            int typeCode;
            if (_typeMapSystemToSQL.TryGetValue(sysType, out typeCode))
                return typeCode;
            else
                return -1;
        }

        #endregion Protected

        #region Public Methods

        /// <summary>
        /// Returns the specified database identifier properly quoted for use in SQL statements.
        /// </summary>
        /// <remarks>The quoting style used depends on the database provider implementation. Use this
        /// method to ensure that identifiers with special characters or reserved words are handled correctly.</remarks>
        /// <param name="identifier">The name of the database identifier to quote. This can be a table, column, or other object name. Cannot be
        /// null or empty.</param>
        /// <returns>A string containing the quoted version of the specified identifier, suitable for inclusion in SQL
        /// statements.</returns>
        public abstract string QuoteIdentifier(string identifier);

        /// <summary>
        /// Determines whether the specified columns belong to more than one table.
        /// </summary>
        /// <remarks>Use this method to check if a set of columns spans more than one DataTable, which may
        /// require fully qualifying column names in SQL or data operations.</remarks>
        /// <param name="targetColumns">An array of DataColumn objects to evaluate. Cannot be null or empty.</param>
        /// <returns>true if the columns are from multiple tables; otherwise, false.</returns>
        public virtual bool QualifyColumnNames(DataColumn[] targetColumns)
        {
            if (targetColumns == null || targetColumns.Length == 0)
                return false;

            return (from c in targetColumns
                    group c by c.Table.TableName into g
                    select g).Count() > 1;
        }

        /// <summary>
        /// Returns the alias for the specified data column.
        /// </summary>
        /// <param name="c">The <see cref="DataColumn"/> for which to retrieve the alias. Can be null.</param>
        /// <returns>A string containing the alias for the specified column, or <see cref="string.Empty"/> if <paramref
        /// name="c"/> is null.</returns>
        public virtual string ColumnAlias(DataColumn c)
        {
            if (c == null)
                return string.Empty;
            else
                return ColumnAlias(c.Table.TableName, c.ColumnName);
        }

        /// <summary>
        /// Generates an alias for a database column by combining the table name and column name using a configured
        /// separator.
        /// </summary>
        /// <remarks>The separator used between the table name and column name is defined by
        /// <c>Settings.Default.ColumnTableNameSeparator</c>. This method does not validate the existence of the
        /// specified table or column.</remarks>
        /// <param name="tableName">The name of the table to prefix the column alias. If null or empty, only the column name is used.</param>
        /// <param name="columnName">The name of the column for which to generate an alias. If null or empty, an empty string is returned.</param>
        /// <returns>A string representing the column alias. Returns an empty string if <paramref name="columnName"/> is null or
        /// empty; otherwise, returns either the column name or a combination of the table name and column name
        /// separated by the configured separator.</returns>
        public virtual string ColumnAlias(string tableName, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return string.Empty;
            else if (string.IsNullOrEmpty(tableName))
                return columnName;
            else
                return tableName + Settings.Default.ColumnTableNameSeparator + columnName;
        }

        /// <summary>
        /// Generates a comma-separated list of target column names for use in SQL statements, with optional identifier
        /// quoting and qualification.
        /// </summary>
        /// <remarks>The method is typically used to construct the column list portion of SQL INSERT or
        /// UPDATE statements. The behavior of identifier quoting and qualification depends on the database provider's
        /// conventions. The resultTable output provides additional metadata that may be useful for further
        /// processing.</remarks>
        /// <param name="targetColumns">An array of DataColumn objects representing the columns to include in the target list. Cannot be null or
        /// contain null elements.</param>
        /// <param name="quoteIdentifiers">true to quote column identifiers according to the database provider's rules; otherwise, false.</param>
        /// <param name="checkQualify">true to check whether column names require qualification with table or schema names; otherwise, false.</param>
        /// <param name="qualifyColumns">A reference parameter that, on return, indicates whether the column names were qualified in the generated
        /// list.</param>
        /// <param name="resultTable">When this method returns, contains a DataTable with metadata about the target columns. This parameter is
        /// passed uninitialized.</param>
        /// <returns>A string containing the formatted, comma-separated list of target column names suitable for use in SQL
        /// statements.</returns>
        public abstract string TargetList(DataColumn[] targetColumns, bool quoteIdentifiers,
            bool checkQualify, ref bool qualifyColumns, out DataTable resultTable);

        /// <summary>
        /// Generates a comma-separated list of column identifiers from the specified target tables and returns it as a
        /// string.
        /// </summary>
        /// <remarks>If multiple tables are provided, column names are qualified with their respective
        /// table names to avoid ambiguity. If the input array is null, empty, or contains tables without columns, the
        /// method returns an empty string and sets <paramref name="resultTable"/> to an empty table.</remarks>
        /// <param name="targetTables">An array of <see cref="DataTable"/> objects containing the columns to include in the target list. Must not
        /// be null or empty, and each table must contain at least one column.</param>
        /// <param name="quoteIdentifiers">true to enclose column identifiers in quotes; otherwise, false.</param>
        /// <param name="qualifyColumns">When this method returns, contains true if column names should be qualified with their table names;
        /// otherwise, false. The value is set to true if more than one table is provided.</param>
        /// <param name="resultTable">When this method returns, contains a <see cref="DataTable"/> with the columns included in the target list.
        /// If the input is invalid or an error occurs, this will be an empty table.</param>
        /// <returns>A comma-separated string of column identifiers from the target tables. Returns an empty string if the input
        /// is invalid or an error occurs.</returns>
        public string TargetList(DataTable[] targetTables, bool quoteIdentifiers,
            ref bool qualifyColumns, out DataTable resultTable)
        {
            if (targetTables == null || targetTables.Length == 0 || targetTables[0].Columns.Count == 0)
            {
                resultTable = new DataTable();
                return string.Empty;
            }

            resultTable = null;

            try
            {
                DataColumn[] targetList = [.. targetTables.SelectMany(t => t.Columns.Cast<DataColumn>())];
                qualifyColumns = targetTables.Length > 1;
                return TargetList(targetList, quoteIdentifiers, false, ref qualifyColumns, out resultTable);
            }
            catch { resultTable = new DataTable(); }
            return string.Empty;
        }

        /// <summary>
        /// Generates a SQL FROM clause string based on the specified target tables and filter conditions.
        /// </summary>
        /// <param name="includeFrom">true to include the FROM keyword at the beginning of the clause; otherwise, false.</param>
        /// <param name="quoteIdentifiers">true to enclose table and column identifiers in quotes; otherwise, false.</param>
        /// <param name="targetTables">An array of DataTable objects representing the tables to include in the FROM clause. Cannot be null or
        /// empty.</param>
        /// <param name="whereClause">A reference to a list of SqlFilterCondition objects representing filter conditions to be applied. The list
        /// may be modified by the method.</param>
        /// <param name="additionalTables">When this method returns, contains true if additional tables were required for the FROM clause; otherwise,
        /// false.</param>
        /// <returns>A string containing the constructed SQL FROM clause based on the specified tables and filter conditions.</returns>
        public string FromList(bool includeFrom, bool quoteIdentifiers,
            DataTable[] targetTables, ref List<SqlFilterCondition> whereClause, out bool additionalTables)
        {
            DataColumn[] targetColumns = [.. targetTables.SelectMany(t => t.Columns.Cast<DataColumn>())];
            return FromList(includeFrom, targetColumns, quoteIdentifiers, ref whereClause, out additionalTables);
        }

        /// <summary>
        /// Generates a SQL FROM clause based on the specified target columns and filter conditions, optionally
        /// including the FROM keyword and quoting identifiers as needed.
        /// </summary>
        /// <remarks>The method may modify the whereClause parameter to include join conditions for tables
        /// referenced in filter conditions but not present in the targetColumns array. Use the additionalTables output
        /// parameter to determine if such tables were added.</remarks>
        /// <param name="includeFrom">true to include the FROM keyword at the beginning of the clause; otherwise, false.</param>
        /// <param name="targetColumns">An array of DataColumn objects representing the columns whose tables will be included in the FROM clause.</param>
        /// <param name="quoteIdentifiers">true to quote table and column identifiers in the generated SQL; otherwise, false.</param>
        /// <param name="whereClause">A reference to a list of SqlFilterCondition objects representing filter conditions to be applied. This list
        /// may be modified to include additional join conditions required by the FROM clause.</param>
        /// <param name="additionalTables">When this method returns, contains true if additional tables were added to the FROM clause to satisfy filter
        /// conditions; otherwise, false. This parameter is passed uninitialized.</param>
        /// <returns>A string containing the constructed SQL FROM clause, including any necessary joins and table references.</returns>
        public string FromList(bool includeFrom, DataColumn[] targetColumns,
            bool quoteIdentifiers, ref List<SqlFilterCondition> whereClause, out bool additionalTables)
        {
            DataTable[] colTables = [.. targetColumns.Select(c => c.Table).Distinct()];
            var whereTables = whereClause.Select(con => con.Table).Distinct().Where(t => !colTables.Select(s => s.TableName).Contains(t.TableName));

            int numTables = colTables.Length;
            colTables = [.. colTables, .. whereTables];
            additionalTables = colTables.Length > numTables;

            whereClause = [.. JoinClause(colTables).Concat(whereClause)];

            return FromList(includeFrom, quoteIdentifiers, [.. colTables.Select(t => t.TableName)]);
        }

        /// <summary>
        /// Create a string of database tables that the data will be selected from.
        /// </summary>
        /// <param name="includeFrom">If set to <c>true</c> include 'FROM' in the returned string.</param>
        /// <param name="quoteIdentifiers">If set to <c>true</c> wrap identifiers in quotes.</param>
        /// <param name="targetColumns">The target columns to be selected.</param>
        /// <param name="fromTables">A list of the tables to select from.</param>
        /// <param name="whereClause">The where clause statements generated to satisfy the table joins.</param>
        /// <param name="additionalTables">Set to <c>true</c> if tables, in addition to those relating
        /// to the target columns, are required.</param>
        /// <returns>A string of database tables to select from.</returns>
        public string FromList(bool includeFrom, bool quoteIdentifiers, DataColumn[] targetColumns,
            List<DataTable> fromTables, ref List<SqlFilterCondition> whereClause, out bool additionalTables)
        {
            DataTable[] colTables = [.. targetColumns.Select(c => c.Table).Distinct()];
            var whereTables = fromTables.Distinct().Where(t => !colTables.Select(s => s.TableName).Contains(t.TableName));

            int numTables = colTables.Length;
            colTables = [.. colTables, .. whereTables];
            additionalTables = colTables.Length > numTables;

            whereClause = [.. JoinClause(colTables)];

            return FromList(includeFrom, quoteIdentifiers, [.. colTables.Select(t => t.TableName)]);
        }

        /// <summary>
        /// Builds a comma-separated list of table names for use in a SQL FROM clause, with optional identifier quoting
        /// and inclusion of the 'FROM' keyword.
        /// </summary>
        /// <remarks>If quoteIdentifiers is true, each table name is quoted using the appropriate quoting
        /// method for the database. The order of table names in the result matches the order in the tableNames
        /// array.</remarks>
        /// <param name="includeFrom">true to prefix the result with 'FROM '; otherwise, false.</param>
        /// <param name="quoteIdentifiers">true to quote each table name using the database's identifier quoting rules; otherwise, false.</param>
        /// <param name="tableNames">An array of table names to include in the list. Cannot be null or empty.</param>
        /// <returns>A string containing the formatted list of table names, optionally prefixed with 'FROM '. Returns an empty
        /// string if tableNames is null or empty.</returns>
        public string FromList(bool includeFrom, bool quoteIdentifiers, string[] tableNames)
        {
            if (tableNames == null || tableNames.Length == 0)
                return string.Empty;
            StringBuilder sbFromList = new();
            if (quoteIdentifiers)
            {
                foreach (string tableName in tableNames)
                    sbFromList.Append(',').Append(QuoteIdentifier(tableName));
                return (includeFrom ? " FROM " : "") + sbFromList.Remove(0, 1).ToString();
            }
            else
            {
                return (includeFrom ? " FROM " : "") + string.Join(",", tableNames);
            }
        }

        /// <summary>
        /// Combines multiple lists of SQL filter conditions into a single, logically grouped list based on their
        /// Boolean operators.
        /// </summary>
        /// <remarks>If an inner list begins with a condition whose Boolean operator is 'OR'
        /// (case-insensitive), it starts a new group in the output. Otherwise, its conditions are appended to the
        /// previous group. This method is useful for constructing complex WHERE clauses with mixed AND/OR
        /// logic.</remarks>
        /// <param name="inWhereClause">A list of lists, where each inner list contains SQL filter conditions to be combined. The first condition's
        /// Boolean operator in each inner list determines how the lists are joined.</param>
        /// <returns>A new list of lists representing the joined SQL filter conditions. Lists starting with an 'OR' operator are
        /// kept separate; others are merged with the previous group.</returns>
        public virtual List<List<SqlFilterCondition>> JoinWhereClauseLists(List<List<SqlFilterCondition>> inWhereClause)
        {
            List<List<SqlFilterCondition>> outWhereClause = [];
            foreach (List<SqlFilterCondition> oneWhereClause in inWhereClause)
            {
                if (outWhereClause.Count == 0 || oneWhereClause[0].BooleanOperator.Equals("OR", StringComparison.CurrentCultureIgnoreCase))
                    outWhereClause.Add(oneWhereClause);
                else
                    outWhereClause[^1].AddRange(oneWhereClause);
            }
            return outWhereClause;
        }

        /// <summary>
        /// Builds a SQL WHERE clause string from the specified filter conditions, with options to include the WHERE
        /// keyword, quote identifiers, and qualify column names.
        /// </summary>
        /// <param name="includeWhere">true to include the WHERE keyword at the beginning of the clause; otherwise, false.</param>
        /// <param name="quoteIdentifiers">true to quote SQL identifiers such as column and table names; otherwise, false.</param>
        /// <param name="qualifyColumns">true to qualify column names with their table or alias names; otherwise, false.</param>
        /// <param name="whereConds">A list of lists containing filter conditions to be combined into the WHERE clause. Each inner list
        /// represents a group of conditions.</param>
        /// <returns>A string representing the constructed SQL WHERE clause based on the provided filter conditions and options.
        /// Returns an empty string if no conditions are specified.</returns>
        public virtual string WhereClause(bool includeWhere, bool quoteIdentifiers,
            bool qualifyColumns, List<List<SqlFilterCondition>> whereConds)
        {
            return WhereClause(includeWhere, quoteIdentifiers, qualifyColumns,
                [.. whereConds.SelectMany(cond => cond)]);
        }

        /// <summary>
        /// Builds a SQL WHERE clause string from the specified filter conditions, with options to include the WHERE
        /// keyword, quote identifiers, and qualify column names.
        /// </summary>
        /// <remarks>If all filter conditions target the same table and column with equality or OR
        /// operators, the method optimizes the output by generating an IN clause. The method does not validate SQL
        /// injection or parameterization; callers are responsible for ensuring safe input. The output is intended for
        /// use in dynamically generated SQL statements.</remarks>
        /// <param name="includeWhere">true to include the WHERE keyword at the beginning of the clause; otherwise, false.</param>
        /// <param name="quoteIdentifiers">true to quote table and column identifiers in the generated SQL; otherwise, false.</param>
        /// <param name="qualifyColumns">true to prefix column names with their table names; otherwise, false.</param>
        /// <param name="whereConds">A list of filter conditions to be combined into the WHERE clause. Cannot be null or empty.</param>
        /// <returns>A string containing the constructed SQL WHERE clause based on the provided conditions and formatting
        /// options. Returns an empty string if no conditions are specified.</returns>
        public virtual string WhereClause(bool includeWhere, bool quoteIdentifiers,
            bool qualifyColumns, List<SqlFilterCondition> whereConds)
        {
            // Check parameters.
            if (whereConds == null || whereConds.Count == 0)
                return string.Empty;

            StringBuilder sbWhereClause = new(includeWhere ? " WHERE " : " ");

            int tableCount = whereConds.Select(c => c.Table.TableName).Distinct().Count();
            int columnCount = whereConds.Select(c => c.Column.ColumnName).Distinct().Count();

            // If all conditions are for the same table/column with "=" or "OR",
            // convert to a single IN() clause
            if (tableCount == 1 && columnCount == 1)
            {
                // Check if all conditions are simple equality or OR
                bool allEquality = whereConds.All(c =>
                    (string.IsNullOrEmpty(c.Operator) || c.Operator == "=") &&
                    (string.IsNullOrEmpty(c.BooleanOperator) ||
                     c.BooleanOperator.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                     c.BooleanOperator.Equals("OR", StringComparison.OrdinalIgnoreCase)));

                if (allEquality && whereConds.Count > 1)
                {
                    // Convert to IN() clause
                    SqlFilterCondition firstCond = whereConds[0];

                    // Collect all values
                    List<string> values = [.. whereConds.Where(c => c.Value != null).Select(c => c.Value.ToString()).Where(v => !string.IsNullOrEmpty(v)).Distinct()];

                    if (values.Count > 0)
                    {
                        // Add column name
                        if (quoteIdentifiers)
                        {
                            if (qualifyColumns && !string.IsNullOrEmpty(firstCond.Table.TableName))
                                sbWhereClause.Append(string.Format("{0}.{1}",
                                    QuoteIdentifier(firstCond.Table.TableName),
                                    QuoteIdentifier(firstCond.Column.ColumnName)));
                            else
                                sbWhereClause.Append(QuoteIdentifier(firstCond.Column.ColumnName));
                        }
                        else
                        {
                            if (qualifyColumns && !string.IsNullOrEmpty(firstCond.Table.TableName))
                                sbWhereClause.Append(string.Format("{0}.{1}",
                                    firstCond.Table.TableName,
                                    firstCond.Column.ColumnName));
                            else
                                sbWhereClause.Append(firstCond.Column.ColumnName);
                        }

                        // Add IN clause
                        if (values.Count == 1)
                        {
                            sbWhereClause.Append(" = ");
                            sbWhereClause.Append(QuoteValue(values[0]));
                        }
                        else
                        {
                            sbWhereClause.Append(" IN (");
                            for (int i = 0; i < values.Count; i++)
                            {
                                if (i > 0)
                                    sbWhereClause.Append(',');
                                sbWhereClause.Append(QuoteValue(values[i]));
                            }
                            sbWhereClause.Append(')');
                        }

                        return sbWhereClause.ToString();
                    }
                }
            }

            // Avoid repeated calls to 'GetUnderlyingType' for the same table
            // and column type by checking to see if it is a string (which most
            // fields are).
            bool condString = false;
            if (tableCount == 1 && columnCount == 1)
            {
                SqlFilterCondition sqlTestCond = whereConds[0];
                condString = GetUnderlyingType(sqlTestCond) is string;
            }

            for (int i = 0; i < whereConds.Count; i++)
            {
                SqlFilterCondition sqlCond = whereConds[i];

                if (i != 0)
                {
                    if (!string.IsNullOrEmpty(sqlCond.BooleanOperator))
                        sbWhereClause.Append(string.Format(" {0} ", sqlCond.BooleanOperator));
                    else
                        sbWhereClause.Append(" AND ");
                }

                sbWhereClause.Append(sqlCond.OpenParentheses);

                if (quoteIdentifiers)
                {
                    if (qualifyColumns && !string.IsNullOrEmpty(sqlCond.Table.TableName))
                        sbWhereClause.Append(string.Format("{0}.{1}", QuoteIdentifier(sqlCond.Table.TableName),
                    QuoteIdentifier(sqlCond.Column.ColumnName)));
                    else
                        sbWhereClause.Append(QuoteIdentifier(sqlCond.Column.ColumnName));
                }
                else
                {
                    if (qualifyColumns && !string.IsNullOrEmpty(sqlCond.Table.TableName))
                        sbWhereClause.Append(string.Format("{0}.{1}", sqlCond.Table.TableName, sqlCond.Column.ColumnName));
                    else
                        sbWhereClause.Append(sqlCond.Column.ColumnName);
                }

                if (!string.IsNullOrEmpty(sqlCond.Operator))
                {
                    if (sqlCond.ColumnSystemType == typeof(DataColumn) &&
                        sqlCond.Value is DataColumn c) // table relation
                    {
                        if (quoteIdentifiers)
                        {
                            if (qualifyColumns)
                                sbWhereClause.Append(string.Format(" {0} {1}", sqlCond.Operator,
                            QuoteIdentifier(c.Table.TableName) + "." + QuoteIdentifier(c.ColumnName)));
                            else
                                sbWhereClause.Append(string.Format(" {0} {1}",
                            sqlCond.Operator, QuoteIdentifier(c.ColumnName)));
                        }
                        else
                        {
                            if (qualifyColumns)
                                sbWhereClause.Append(string.Format(" {0} {1}",
                            sqlCond.Operator, c.Table.TableName + "." + c.ColumnName));
                            else
                                sbWhereClause.Append(string.Format(" {0} {1}", sqlCond.Operator, c.ColumnName));
                        }
                    }
                    else if (sqlCond.Operator.ToUpper().EndsWith("NULL"))
                    {
                        sbWhereClause.Append(string.Format(" {0} ", sqlCond.Operator));
                    }
                    else if (sqlCond.ColumnSystemType == typeof(string))
                    {
                        switch (sqlCond.Operator.ToUpper())
                        {
                            case "IN ()":
                            case "NOT IN ()":
                                sbWhereClause.Append(string.Format(" {0}",
                            sqlCond.Operator.Remove(sqlCond.Operator.Length - 1, 1)));
                                Regex r = new(QuotePrefix + @"[^" + QuotePrefix + "]*" + QuoteSuffix + "|[^,]+",
                                RegexOptions.IgnorePatternWhitespace);
                                sbWhereClause.Append(r.Matches(sqlCond.Value.ToString()).Cast<Match>()
                            .Aggregate(new StringBuilder(), (sb, m) => sb.Append(string.Format("{0},",
                                QuoteValue(m.Value)))));
                                sbWhereClause.Remove(sbWhereClause.Length - 1, 1);
                                sbWhereClause.Append(')');
                                break;

                            case "BEGINS WITH":
                                sbWhereClause.Append(" LIKE " + QuoteValue(string.Format("{0}{1}",
                            sqlCond.Value, WildcardManyMatch)));
                                break;

                            case "ENDS WITH":
                                sbWhereClause.Append(" LIKE " + QuoteValue(string.Format("{1}{0}",
                            sqlCond.Value, WildcardManyMatch)));
                                break;

                            case "CONTAINS":
                                sbWhereClause.Append(" LIKE " + QuoteValue(string.Format("{1}{0}{1}",
                            sqlCond.Value, WildcardManyMatch)));
                                break;

                            default:
                                // Avoid repeated calls to 'GetUnderlyingType' for string fields.
                                if (condString)
                                    sbWhereClause.Append(string.Format(" {0} {1}", sqlCond.Operator,
                                QuoteValue(sqlCond.Value)));
                                else
                                    sbWhereClause.Append(string.Format(" {0} {1}", sqlCond.Operator,
                                QuoteValue(GetUnderlyingType(sqlCond))));
                                break;
                        }
                    }
                    else
                    {
                        switch (sqlCond.Operator.ToUpper())
                        {
                            case "IN ()":
                                sbWhereClause.Append(" IN (").Append(sqlCond.Value).Append(") ");
                                break;

                            case "NOT IN ()":
                                sbWhereClause.Append(" NOT IN (").Append(sqlCond.Value).Append(") ");
                                break;

                            default:
                                sbWhereClause.Append(' ').Append(sqlCond.Operator).Append(' ');
                                // Avoid repeated calls to 'GetUnderlyingType' for string fields.
                                if (condString)
                                    sbWhereClause.Append(QuoteValue(sqlCond.Value));
                                else
                                    sbWhereClause.Append(QuoteValue(GetUnderlyingType(sqlCond)));
                                break;
                        }
                    }
                }
                sbWhereClause.Append(sqlCond.CloseParentheses);
            }

            return sbWhereClause.ToString();
        }

        /// <summary>
        /// Creates a list of optimized SQL filter conditions from a DataTable of
        /// selected Incids by grouping continuous ranges.
        /// </summary>
        /// <param name="incidSelection">A DataTable containing the selected Incids.</param>
        /// <param name="incidColumn">The DataColumn representing the Incid field.</param>
        /// <param name="condTable">The DataTable to use in the filter conditions.</param>
        /// <param name="quoteValueFunc">Optional function to quote values for GIS-specific formatting.</param>
        /// <returns>A list of SQL filter conditions.</returns>
        public static List<SqlFilterCondition> BuildIncidWhereClause(
            DataTable incidSelection,
            DataColumn incidColumn,
            DataTable condTable,
            Func<string, string> quoteValueFunc = null)
        {
            List<SqlFilterCondition> whereClause = [];

            // Split the table of selected Incids into chunks of continuous Incids
            var query = incidSelection
                .AsEnumerable()
                .Where(r => !string.IsNullOrWhiteSpace(r.Field<string>(0)))
                .Select((r, index) => new
                {
                    RowIndex = RecordIds.IncidNumber(r.Field<string>(0)) - index,
                    Incid = r.Field<string>(0)
                })
                .ChunkBy(r => r.RowIndex);

            List<string> inList = [];

            // Loop through each chunk/series of Incids
            foreach (var item in query)
            {
                if (item.Count() < 3)
                {
                    // Add small chunks to the IN list
                    if (quoteValueFunc != null)
                        inList.AddRange(item.Select(t => quoteValueFunc(t.Incid)));
                    else
                        inList.AddRange(item.Select(t => t.Incid));
                }
                else
                {
                    // Use range operators for continuous series
                    SqlFilterCondition cond = new()
                    {
                        BooleanOperator = "OR",
                        OpenParentheses = "(",
                        Column = incidColumn,
                        Table = condTable,
                        ColumnSystemType = incidColumn.DataType,
                        Operator = ">=",
                        Value = item.First().Incid,
                        CloseParentheses = string.Empty
                    };
                    whereClause.Add(cond);

                    cond = new()
                    {
                        BooleanOperator = "AND",
                        OpenParentheses = string.Empty,
                        Column = incidColumn,
                        Table = condTable,
                        ColumnSystemType = incidColumn.DataType,
                        Operator = "<=",
                        Value = item.Last().Incid,
                        CloseParentheses = ")"
                    };
                    whereClause.Add(cond);
                }
            }

            // Process remaining Incids using IN operator
            int i = 0;
            while (i < inList.Count)
            {
                int numElems = i < inList.Count - 254 ? 254 : inList.Count - i;
                string[] oneList = new string[numElems];
                inList.CopyTo(i, oneList, 0, numElems);

                SqlFilterCondition cond = new()
                {
                    BooleanOperator = "OR",
                    OpenParentheses = "(",
                    Column = incidColumn,
                    Table = condTable,
                    ColumnSystemType = incidColumn.DataType,
                    Operator = inList.Count == 1 ? "=" : "IN ()",
                    Value = string.Join(",", oneList),
                    CloseParentheses = ")"
                };
                whereClause.Add(cond);

                i += numElems;
            }

            return whereClause;
        }

        /// <summary>
        /// Builds a UNION query from a list of SqlFilterCondition objects.
        /// All constituent SELECT statements use the same target list and FROM
        /// clause but different WHERE clauses.
        /// </summary>
        /// <param name="targetList">Target list for union query.</param>
        /// <param name="fromClause">From clause for union query.</param>
        /// <param name="sortOrdinals">Ordinals of columns to order by. Negative
        /// values indicate descending order.</param>
        /// <param name="whereClause">List of where clauses to build UNION query
        /// from.</param>
        /// <param name="db">Database against which UNION query will be run.</param>
        /// <param name="tableAliases">Optional dictionary mapping qualified table
        /// names to their aliases.</param>
        /// <returns>The complete SQL query string.</returns>
        public string BuildUnionQuery(
            string targetList,
            string fromClause,
            int[] sortOrdinals,
            List<SqlFilterCondition> whereClause,
            DbBase db,
            Dictionary<string, string> tableAliases = null)
        {
            StringBuilder sql = new();

            // Adjust table references if aliases are provided
            List<SqlFilterCondition> adjustedWhereClause = whereClause;
            if (tableAliases != null && tableAliases.Count > 0)
            {
                adjustedWhereClause = [.. whereClause.Select(cond =>
        {
            SqlFilterCondition newCond = new(cond.BooleanOperator,
                cond.Table, cond.Column, cond.Value)
            {
                OpenParentheses = cond.OpenParentheses,
                CloseParentheses = cond.CloseParentheses,
                Operator = cond.Operator,
                ColumnSystemType = cond.ColumnSystemType
            };

            if (cond.Table != null)
            {
                string qualifiedTableName =
                    db.QualifyTableName(cond.Table.TableName);
                if (tableAliases.TryGetValue(qualifiedTableName,
                    out string alias))
                {
                    DataTable aliasedTable = new(alias);
                    foreach (DataColumn col in cond.Table.Columns)
                    {
                        aliasedTable.Columns.Add(col.ColumnName,
                            col.DataType);
                    }
                    newCond.Table = aliasedTable;
                }
            }

            return newCond;
        })];
            }

            // Build the SELECT statement with WHERE clause
            sql.Append(string.Format("SELECT {0} FROM {1}{2}",
                targetList,
                fromClause,
                db.WhereClause(true, true, true, adjustedWhereClause)));

            // Add ORDER BY clause if specified
            if (sortOrdinals != null)
            {
                sql.Append(string.Format(" ORDER BY {0}",
                    string.Join(", ", [.. sortOrdinals.Select(x =>
                x < 0
                    ? string.Format("{0} DESC", Math.Abs(x).ToString())
                    : x.ToString())])));
            }

            return sql.ToString();
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Builds a list of SQL filter conditions representing join clauses between the specified data tables based on
        /// their defined data relations.
        /// </summary>
        /// <remarks>Each join condition corresponds to a data relation between the provided tables. The
        /// resulting list can be used to construct SQL JOIN statements or equivalent query logic.</remarks>
        /// <param name="queryTables">An array of data tables to analyze for join relationships. Cannot be null or empty.</param>
        /// <returns>A list of SQL filter conditions representing the join clauses between related tables. Returns null if no
        /// tables are provided or if the array is empty.</returns>
        private List<SqlFilterCondition> JoinClause(DataTable[] queryTables)
        {
            if (queryTables == null || queryTables.Length == 0)
                return null;

            List<SqlFilterCondition> joinClause = [];

            for (int i = 0; i < queryTables.Length; i++)
            {
                DataTable p = queryTables[i];
                for (int j = 0; j < queryTables.Length; j++)
                {
                    if (j == i)
                        continue;
                    DataTable c = queryTables[j];
                    var children = p.ChildRelations.Cast<DataRelation>().Where(r => r.ChildTable == c);
                    if (children.Any())
                    {
                        DataRelation r = children.ElementAt(0);
                        for (int k = 0; k < r.ParentColumns.Length; k++)
                        {
                            SqlFilterCondition joinWhere = new()
                            {
                                BooleanOperator = string.Empty
                            };
                            if (k == 0)
                            {
                                joinWhere.OpenParentheses = "((";
                                joinWhere.CloseParentheses = "))";
                            }
                            else
                            {
                                joinWhere.OpenParentheses = "(";
                                joinWhere.CloseParentheses = ")";
                            }
                            joinWhere.Table = r.ParentTable;
                            joinWhere.Column = r.ParentColumns[k];
                            joinWhere.ColumnSystemType = typeof(DataColumn);
                            joinWhere.Operator = "=";
                            joinWhere.Value = r.ChildColumns[k];
                            joinClause.Add(joinWhere);
                        }
                    }
                }
            }

            return joinClause;
        }

        /// <summary>
        /// Attempts to convert the specified value in a filter condition to the underlying data type defined by its
        /// associated DataColumn.
        /// </summary>
        /// <remarks>This method uses the schema of the DataTable to determine the target type for
        /// conversion. If the conversion fails, the original value is returned without modification.</remarks>
        /// <param name="sqlCond">The filter condition containing the DataTable, column name, and value to be converted.</param>
        /// <returns>An object representing the value converted to the underlying type of the specified DataColumn. If conversion
        /// is not possible, returns the original value.</returns>
        private object GetUnderlyingType(SqlFilterCondition sqlCond)
        {
            try
            {
                DataRow row = sqlCond.Table.NewRow();
                row[sqlCond.Column] = sqlCond.Value;
                return row[sqlCond.Column];
            }
            catch { return sqlCond.Value; }
        }

        #endregion Private Methods
    }
}