// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using ArcGIS.Desktop.Framework;
using HLU.Properties;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommandType = System.Data.CommandType;

namespace HLU.Data.Connection
{
    /// <summary>
    /// Class for handling SQL Server database connections and operations.
    /// </summary>
    class DbSqlServer : DbBase
    {
        #region Private Members

        private string _errorMessage;
        private SqlConnectionStringBuilder _connStrBuilder;
        private SqlConnection _connection;
        private SqlCommand _command;
        private SqlDataAdapter _adapter;
        private SqlCommandBuilder _commandBuilder;
        private SqlTransaction _transaction;
        private Dictionary<Type, SqlDataAdapter> _adaptersDic = [];

        private UI.View.Connection.ViewConnectSqlServer _connWindow;
        private UI.ViewModel.ViewModelConnectSqlServer _connViewModel;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DbSqlServer class with the specified connection parameters.
        /// </summary>
        /// <param name="connString">The connection string for the SQL Server database.</param>
        /// <param name="defaultSchema">The default schema for the SQL Server database.</param>
        /// <param name="promptPwd">Indicates whether to prompt for a password.</param>
        /// <param name="pwdMask">The password mask.</param>
        /// <param name="useCommandBuilder">Indicates whether to use a command builder.</param>
        /// <param name="useColumnNames">Indicates whether to use column names.</param>
        /// <param name="isUnicode">Indicates whether to use Unicode.</param>
        /// <param name="useTimeZone">Indicates whether to use time zone.</param>
        /// <param name="textLength">The length of text fields.</param>
        /// <param name="binaryLength">The length of binary fields.</param>
        /// <param name="timePrecision">The precision of time fields.</param>
        /// <param name="numericPrecision">The precision of numeric fields.</param>
        /// <param name="numericScale">The scale of numeric fields.</param>
        /// <param name="connectTimeOut">The connection timeout in seconds.</param>
        public DbSqlServer(ref string connString, ref string defaultSchema, ref bool promptPwd, string pwdMask,
            bool useCommandBuilder, bool useColumnNames, bool isUnicode, bool useTimeZone, uint textLength,
            uint binaryLength, uint timePrecision, uint numericPrecision, uint numericScale, int connectTimeOut)
            : base(ref connString, ref defaultSchema, ref promptPwd, pwdMask, useCommandBuilder, useColumnNames,
            isUnicode, useTimeZone, textLength, binaryLength, timePrecision, numericPrecision, numericScale, connectTimeOut)
        {
            if (String.IsNullOrEmpty(ConnectionString)) throw (new Exception("No connection string"));

            try
            {
                // Append connection timeout to connection string.
                string connectionString = String.Format("{0};Connect Timeout={1}", ConnectionString, connectTimeOut);

                Login("User name", connectionString, ref promptPwd, ref _connStrBuilder, ref _connection);

                PopulateTypeMaps(IsUnicode, TextLength, BinaryLength,
                    TimePrecision, NumericPrecision, NumericScale);

                _command = _connection.CreateCommand();
                _adapter = new(_command);
                _commandBuilder = new(_adapter);

                connString = MaskPassword(_connStrBuilder, pwdMask);
                defaultSchema = DefaultSchema;
            }
            catch { throw; }
        }

        #endregion

        #region DbBase Members

        #region Public Members

        /// <summary>
        /// Gets the backend type for this database connection, which is SQL Server in this case.
        /// </summary>
        public override Backends Backend { get { return Backends.SqlServer; } }

        /// <summary>
        /// Checks if the database schema contains the necessary tables and columns as defined in the provided DataSet.
        /// </summary>
        /// <param name="ds">The DataSet containing the tables and columns to check.</param>
        /// <param name="errorMessage">An error message if the schema does not match.</param>
        /// <returns>True if the schema matches, false otherwise.</returns>
        public override bool ContainsDataSet(DataSet ds, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                DataTable schemaTable = GetSchema("Columns",
                    _restrictionNameSchema, DefaultSchema, _connection, _transaction);
                var dbSchema = schemaTable.AsEnumerable();

                StringBuilder messageText = new();

                foreach (DataTable t in ds.Tables)
                {
                    dbSchema.Select(r => r.Field<string>("TABLE_NAME") == t.TableName);
                    var dbSchemaCols = from r in dbSchema
                                       let tableName = r.Field<string>("TABLE_NAME")
                                       where tableName == t.TableName
                                       select new
                                       {
                                           TableName = tableName,
                                           ColumnName = r.Field<string>("COLUMN_NAME"),
                                           DataType = r.Field<string>("DATA_TYPE")
                                       };

                    if (!dbSchemaCols.Any())
                    {
                        messageText.Append(String.Format("\n\nMissing table: {0}", QuoteIdentifier(t.TableName)));
                    }
                    else
                    {
                        string[] checkColumns = (from dsCol in t.Columns.Cast<DataColumn>()
                                                 let dbCols = from dbCol in dbSchemaCols
                                                              where dbCol.ColumnName == dsCol.ColumnName &&
                                                              DbToSystemType(SQLCodeToSQLType(dbCol.DataType)) == dsCol.DataType
                                                              select dbCol
                                                 where !dbCols.Any()
                                                 select QuoteIdentifier(dsCol.ColumnName) + " (" +
                                                 ((SqlDbType)SystemToDbType(dsCol.DataType) + ")").ToString()).ToArray();
                        if (checkColumns.Length > 0) messageText.Append(String.Format("\n\nTable: {0}\nColumns: {1}",
                            QuoteIdentifier(t.TableName), String.Join(", ", checkColumns)));
                    }
                }

                if (messageText.Length == 0)
                {
                    return true;
                }
                else
                {
                    errorMessage = String.Format("Connection does not point to a valid HLU database." +
                            "\nBad schema objects: {0}", messageText);
                    return false;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Gets the database connection object for this SQL Server connection.
        /// </summary>
        public override IDbConnection Connection { get { return _connection; } }

        /// <summary>
        /// Gets the connection string builder for this SQL Server connection, which allows for constructing and modifying the connection string.
        /// </summary>
        public override DbConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        /// <summary>
        /// Gets the current database transaction for this SQL Server connection, if any. This allows for managing transactions across multiple database operations.
        /// </summary>
        public override IDbTransaction Transaction
        {
            get { return _transaction; }
        }

        /// <summary>
        /// Creates and returns a new database command object for executing SQL queries and commands
        /// against the SQL Server database. If a connection is available, the command will be associated
        /// with that connection; otherwise, a standalone command object will be returned.
        /// </summary>
        /// <returns>A new database command object for executing SQL queries and commands.</returns>
        public override IDbCommand CreateCommand()
        {
            if (_connection != null)
                return _connection.CreateCommand();
            else
                return new SqlCommand();
        }

        /// <summary>
        /// Creates and returns a new data adapter for the specified DataTable type. The adapter is
        /// configured with the appropriate select, insert, update, and delete commands based on the
        /// schema of the provided DataTable. If an adapter for the specified type already exists in
        /// the internal dictionary, it will be returned; otherwise, a new adapter will be created,
        /// configured, and stored in the dictionary before being returned.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable for which to create the adapter.</typeparam>
        /// <param name="table">The DataTable instance for which to create the adapter.</param>
        /// <returns>A new data adapter configured for the specified DataTable type.</returns>
        public override IDbDataAdapter CreateAdapter<T>(T table)
        {
            // If the provided table is null, create a new instance of T to use for adapter creation. This allows
            // the method to proceed with adapter creation even if the caller does not provide a specific DataTable instance.
            table ??= new T();

            SqlDataAdapter adapter;

            // Check if an adapter for this type already exists in the dictionary. If it does, return it.
            if (!_adaptersDic.TryGetValue(typeof(T), out adapter))
            {
                adapter = new();

                DataColumn[] pk = table.PrimaryKey;
                if ((pk == null) || (pk.Length == 0)) return null;

                DataTableMapping tableMapping = new()
                {
                    SourceTable = table.TableName, // "Table";
                    DataSetTable = table.TableName // "Exports";
                };

                List<SqlParameter> deleteParams = [];
                List<SqlParameter> insertParams = [];
                List<SqlParameter> updateParams = [];
                List<SqlParameter> updateParamsOrig = [];

                StringBuilder sbTargetList = new();
                StringBuilder sbInsValues = new();
                StringBuilder sbUpdSetList = new();
                StringBuilder sbWhereDel = new();
                StringBuilder sbWhereUpd = new();
                StringBuilder sbWherePkUpd = new();
                StringBuilder sbWherePkIns = new();

                string tableName = QualifyTableName(table.TableName);

                int isNullTypeInt;
                _typeMapSystemToSQL.TryGetValue(typeof(int), out isNullTypeInt);
                SqlDbType isNullType = (SqlDbType)isNullTypeInt;

                DataColumn c;
                string delOrigParamName;
                string insColParamName;
                string updColParamName;
                string updOrigParamName;
                string delIsNullParamName;
                string updIsNullParamName;
                int columnCount = table.Columns.Count;
                int nullParamCount = 0;
                string delAddString;
                string updAddString;

                // Iterate through each column in the DataTable to configure the adapter's column mappings
                // and commands. For each column, determine the appropriate SQL data type, create parameters
                // for delete, insert, and update operations, and build the SQL command text for the adapter's
                // commands. Special handling is included for columns that allow null values to ensure that the
                // generated SQL commands correctly account for nullability in their WHERE clauses.
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    c = table.Columns[i];
                    tableMapping.ColumnMappings.Add(c.ColumnName, c.ColumnName);
                    string colName = QuoteIdentifier(c.ColumnName);

                    int colType;
                    if (!_typeMapSystemToSQL.TryGetValue(c.DataType, out colType)) continue;

                    if (c.AllowDBNull)
                    {
                        delIsNullParamName = ParameterName(_parameterPrefixNull, c.ColumnName,
                            deleteParams.Count + _startParamNo);
                        deleteParams.Add(CreateParameter(delIsNullParamName, isNullType,
                            ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, true));

                        updIsNullParamName = ParameterName(_parameterPrefixNull, c.ColumnName,
                            i + columnCount + nullParamCount + _startParamNo);
                        updateParamsOrig.Add(CreateParameter(updIsNullParamName, isNullType,
                            ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, true));

                        delAddString = String.Format(" AND ((({0} = 1) AND ({1} IS NULL)) OR ({1} = ",
                            ParameterMarker(delIsNullParamName), colName) + "{0}))";
                        updAddString = String.Format(" AND ((({0} = 1) AND ({1} IS NULL)) OR ({1} = ",
                            ParameterMarker(updIsNullParamName), colName) + "{0}))";

                        nullParamCount++;
                    }
                    else
                    {
                        delAddString = String.Format(" AND ({0} = ", colName) + "{0})";
                        updAddString = String.Format(" AND ({0} = ", colName) + "{0})";
                    }

                    delOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName, deleteParams.Count + _startParamNo);
                    deleteParams.Add(CreateParameter(delOrigParamName,
                        (SqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    insColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    insertParams.Add(CreateParameter(insColParamName,
                        (SqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    updateParams.Add(CreateParameter(updColParamName,
                        (SqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName,
                        i + _startParamNo + columnCount + nullParamCount);
                    updateParamsOrig.Add(CreateParameter(updOrigParamName,
                        (SqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    sbTargetList.Append(", ").Append(colName);
                    sbUpdSetList.Append(String.Format(", {0} = {1}", colName, ParameterMarker(updColParamName)));
                    sbInsValues.Append(", ").Append(ParameterMarker(insColParamName));

                    sbWhereDel.Append(String.Format(delAddString, ParameterMarker(delOrigParamName)));
                    sbWhereUpd.Append(String.Format(updAddString, ParameterMarker(updOrigParamName)));

                    if (Array.IndexOf(pk, c) != -1)
                    {
                        sbWherePkUpd.Append(String.Format(" AND ({0} = {1})", colName, ParameterMarker(updColParamName)));
                        sbWherePkIns.Append(String.Format(" AND ({0} = {1})", colName, ParameterMarker(insColParamName)));
                    }
                }

                updateParams.AddRange(updateParamsOrig);
                sbTargetList.Remove(0, 2);
                sbInsValues.Remove(0, 2);
                sbUpdSetList.Remove(0, 2);
                sbWhereDel.Remove(0, 5);
                sbWhereUpd.Remove(0, 5);
                sbWherePkUpd.Remove(0, 5);
                sbWherePkIns.Remove(0, 5);

                adapter.TableMappings.Add(tableMapping);

                // Configure the SelectCommand for the adapter to retrieve data from the specified table.
                // The command text is constructed using the target column list and the qualified table name.
                adapter.SelectCommand = new()
                {
                    CommandType = CommandType.Text,
                    Connection = _connection,
                    CommandText = String.Format("SELECT {0} FROM {1}", sbTargetList, tableName)
                };

                // If the useCommandBuilder flag is not set, manually create and configure the DeleteCommand,
                // UpdateCommand, and InsertCommand for the adapter using the constructed SQL command text and parameters.
                if (!_useCommandBuilder)
                {
                    adapter.DeleteCommand = new()
                    {
                        CommandType = CommandType.Text,
                        Connection = _connection,
                        CommandText = String.Format("DELETE FROM {0} WHERE {1}", tableName, sbWhereDel)
                    };
                    adapter.DeleteCommand.Parameters.AddRange(deleteParams.ToArray());

                    adapter.UpdateCommand = new()
                    {
                        Connection = _connection,
                        CommandType = CommandType.Text,
                        CommandText =
                        String.Format("UPDATE {0} SET {1} WHERE {2}", tableName, sbUpdSetList, sbWhereUpd)
                    };
                    adapter.UpdateCommand.Parameters.AddRange(updateParams.ToArray());

                    adapter.InsertCommand = new()
                    {
                        CommandType = CommandType.Text,
                        Connection = _connection,
                        CommandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2})",
                        tableName, sbTargetList, sbInsValues)
                    };
                    adapter.InsertCommand.Parameters.AddRange(insertParams.ToArray());
                }
                else
                {
                    SqlCommandBuilder cmdBuilder = new(adapter);
                    adapter.DeleteCommand = cmdBuilder.GetDeleteCommand(_useColumnNames);
                    adapter.UpdateCommand = cmdBuilder.GetUpdateCommand(_useColumnNames);
                    adapter.InsertCommand = cmdBuilder.GetInsertCommand(_useColumnNames);
                }

                // Append SELECT statements to the end of the UpdateCommand and InsertCommand to retrieve
                // the updated or inserted row after the command is executed. This allows for retrieving
                // the new values of the row, including any auto-generated values such as identity columns,
                // after an update or insert operation is performed.
                adapter.UpdateCommand.CommandText += ";\r\n" +
                    String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkUpd);
                adapter.InsertCommand.CommandText += ";\r\n" +
                    String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkIns);

                // Store the configured adapter in the dictionary for future use, using the type of T as the key.
                // This allows for efficient retrieval of the adapter for subsequent operations involving the same
                // type of DataTable without needing to recreate and reconfigure the adapter each time.
                if (typeof(T) != typeof(DataTable))
                    _adaptersDic.Add(typeof(T), adapter);
            }

            return adapter;
        }

        /// <summary>
        /// Creates a new SQL parameter with the specified properties, including name, data type, direction,
        /// source column, source version, and null mapping.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The SQL data type of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, InputOutput, or ReturnValue).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The source version for the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a column that allows null values.</param>
        /// <returns>A new SqlParameter instance with the specified properties.</returns>
        private SqlParameter CreateParameter(string name, SqlDbType type, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            SqlParameter param = new(name, type)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Creates a new SQL parameter with the specified properties, including name, value, direction,
        /// source column, source version, and null mapping.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, InputOutput, or ReturnValue).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The source version for the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a column that allows null values.</param>
        /// <returns>A new SqlParameter instance with the specified properties.</returns>
        private SqlParameter CreateParameter(string name, object value, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            SqlParameter param = new(name, value)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Generates a parameter name based on the specified prefix, column name, and parameter number.
        /// </summary>
        /// <param name="prefix">The prefix for the parameter name.</param>
        /// <param name="columnName">The name of the column associated with the parameter.</param>
        /// <param name="paramNo">The parameter number.</param>
        /// <returns>The generated parameter name.</returns>
        protected override string ParameterName(string prefix, string columnName, int paramNo)
        {
            if (_useColumnNames)
                return ParameterPrefix + prefix + columnName;
            else
                return String.Format("{0}p{1}", ParameterPrefix, paramNo);
        }

        /// <summary>
        /// Formats the parameter name for use in SQL command text. For SQL Server, parameters are typically
        /// prefixed with the '@' symbol.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The formatted parameter name.</returns>
        protected override string ParameterMarker(string parameterName)
        {
            return parameterName;
        }

        /// <summary>
        /// Fills the schema of the provided DataTable based on the specified SQL query and schema type. The method
        /// uses a SqlDataAdapter to retrieve the schema information from the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable.</typeparam>
        /// <param name="schemaType">The type of schema to retrieve.</param>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="table">The DataTable to fill.</param>
        /// <returns>True if the schema was successfully filled; otherwise, false.</returns>
        public override bool FillSchema<T>(SchemaType schemaType, string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return false;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                SqlDataAdapter adapter = UpdateAdapter(table);
                if (adapter != null)
                {
                    if (_transaction != null)
                        adapter.SelectCommand.Transaction = _transaction;
                    adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                    adapter.FillSchema(table, schemaType);
                }
                else
                {
                    _command.CommandText = sql;
                    _command.CommandType = CommandType.Text;
                    if (_transaction != null) _command.Transaction = _transaction;
                    _adapter = new(_command)
                    {
                        MissingSchemaAction = MissingSchemaAction.AddWithKey
                    };
                    _adapter.FillSchema(table, schemaType);
                }
                return true;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Fills the provided DataTable with data based on the specified SQL query. The method uses a SqlDataAdapter
        /// to retrieve the data from the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable.</typeparam>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="table">The DataTable to fill.</param>
        /// <returns>The number of rows added to the DataTable, or -1 if an error occurred.</returns>
        public override int FillTable<T>(string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return 0;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                SqlDataAdapter adapter = UpdateAdapter(table);
                if (adapter != null)
                {
                    if (_transaction != null)
                        adapter.SelectCommand.Transaction = _transaction;
                    return adapter.Fill(table);
                }
                else
                {
                    _command.CommandText = sql;
                    _command.CommandType = CommandType.Text;
                    if (_transaction != null) _command.Transaction = _transaction;
                    _adapter = new(_command);
                    return _adapter.Fill(table);
                }
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return -1;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Executes the specified SQL query and returns a data reader for reading the results. The method configures
        /// the command with the provided timeout and command type, and associates it with the current transaction if available.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (Text, StoredProcedure, etc.).</param>
        /// <returns>An IDataReader for reading the results, or null if an error occurred.</returns>
        public override IDataReader ExecuteReader(string sql, int commandTimeout, CommandType commandType)
        {
            _errorMessage = String.Empty;
            if (String.IsNullOrEmpty(sql)) return null;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _command.CommandType = commandType;
                _command.CommandTimeout = commandTimeout;
                _command.CommandText = sql;

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                return _command.ExecuteReader() as IDataReader;
            }
            catch (Exception ex)
            {
                if (previousConnectionState == ConnectionState.Closed) _connection.Close();
                _errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Executes the specified SQL query and returns the number of rows affected. The method configures
        /// the command with the provided timeout and command type, and associates it with the current transaction if available.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (Text, StoredProcedure, etc.).</param>
        /// <returns>The number of rows affected, or -1 if an error occurred.</returns>
        public override int ExecuteNonQuery(string sql, int commandTimeout, CommandType commandType)
        {
            _errorMessage = String.Empty;
            if (String.IsNullOrEmpty(sql)) return -1;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _command.CommandType = commandType;
                _command.CommandTimeout = commandTimeout;
                _command.CommandText = sql;

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                return _command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return -1;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Executes the query and returns the first column of the first row. The method configures
        /// the command with the provided timeout and command type, and associates it with the current transaction if available.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (Text, StoredProcedure, etc.).</param>
        /// <returns>The first column of the first row, or null if an error occurred.</returns>
        public override object ExecuteScalar(string sql, int commandTimeout, CommandType commandType)
        {
            _errorMessage = String.Empty;
            if (String.IsNullOrEmpty(sql)) return null;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _command.CommandType = commandType;
                _command.CommandTimeout = commandTimeout;
                _command.CommandText = sql;

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                return _command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return null;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Executes the query and returns the first column of the first row. The method configures
        /// the command with the provided timeout and command type, and associates it with the current transaction if available.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (Text, StoredProcedure, etc.).</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The first column of the first row, or null if an error occurred.</returns>
        public override async Task<object> ExecuteScalarAsync(string sql, int commandTimeout, CommandType commandType, CancellationToken cancellationToken = default)
        {
            _errorMessage = String.Empty;

            if (String.IsNullOrEmpty(sql)) return null;

            // Create local copies of the connection and command to avoid race conditions
            // where shared instance members might be disposed/nullified during async operations
            SqlConnection connection = _connection;
            SqlCommand command = null;

            // Capture the previous connection state for local reference
            ConnectionState previousConnectionState = connection?.State ?? ConnectionState.Closed;

            try
            {
                // Check for null before proceeding
                if (connection == null)
                {
                    _errorMessage = "Connection object is null";
                    return null;
                }

                // Create a new command instance for this async operation
                // to avoid sharing command state across concurrent operations
                command = connection.CreateCommand();
                command.CommandType = commandType;
                command.CommandTimeout = commandTimeout;
                command.CommandText = sql;

                // Assign transaction if one exists
                if (_transaction != null)
                    command.Transaction = _transaction;

                // Only refresh schema if command builder is not null
                _commandBuilder?.RefreshSchema();

                // Open connection if needed
                if ((connection.State & ConnectionState.Open) != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);

                // Execute the scalar query
                return await command.ExecuteScalarAsync(cancellationToken);
            }
            catch (ObjectDisposedException ex)
            {
                _errorMessage = "Connection or command has been disposed: " + ex.Message;
                return null;
            }
            catch (NullReferenceException ex)
            {
                _errorMessage = "Null reference during async execution: " + ex.Message;
                return null;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return null;
            }
            finally
            {
                // Clean up the local command instance
                if (command != null)
                {
                    try
                    {
                        command.Dispose();
                    }
                    catch { }
                }

                // Close connection if it was previously closed
                if (previousConnectionState == ConnectionState.Closed && connection?.State == ConnectionState.Open)
                {
                    try
                    {
                        connection.Close();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Validates the SQL query by executing it and throwing
        /// any execeptions raised back to the calling method.
        /// </summary>
        /// <param name="sql">The SQL query to validate (which should be a non-update query).</param>
        /// <param name="commandTimeout">The command timeout.</param>
        /// <param name="commandType">Type of the sql command.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Sql is null or empty</exception>
        public override bool ValidateQuery(string sql, int commandTimeout, CommandType commandType)
        {
            _errorMessage = String.Empty;
            if (String.IsNullOrEmpty(sql)) throw (new Exception("Sql is null or empty"));

            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _command.CommandType = commandType;
                _command.CommandTimeout = commandTimeout;
                _command.CommandText = sql;

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                _command.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                //TODO: throw ex;
                return false;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Begins a database transaction with the specified isolation level. If a transaction is already
        /// in progress, it can either be committed or rolled back based on the commitPrevious parameter
        /// before starting the new transaction. The method also refreshes the command builder's schema
        /// to ensure that any changes to the database schema are reflected in the commands used by the adapter.
        /// </summary>
        /// <param name="commitPrevious">Indicates whether to commit the previous transaction if one is in progress.</param>
        /// <param name="isolationLevel">The isolation level for the new transaction.</param>
        /// <returns>True if the transaction was successfully started; otherwise, false.</returns>
        public override bool BeginTransaction(bool commitPrevious, IsolationLevel isolationLevel)
        {
            try
            {
                _errorMessage = String.Empty;
                if (_transaction != null && _transaction.Connection != null)
                {
                    if (commitPrevious)
                        _transaction.Commit();
                    else
                        _transaction.Rollback();
                }

                if (_connection.State != ConnectionState.Open) _connection.Open();

                _transaction = _connection.BeginTransaction(isolationLevel);
                _commandBuilder.RefreshSchema();
                return true;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Commits the current database transaction. If there is no active transaction, the method does nothing.
        /// </summary>
        /// <returns>True if the transaction was successfully committed; otherwise, false.</returns>
        public override bool CommitTransaction()
        {
            try
            {
                _errorMessage = String.Empty;
                if (_transaction != null)
                {
                    _transaction.Commit();
                    _transaction = null;
                    _commandBuilder.RefreshSchema();
                }
                return false;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Rolls back the current database transaction. If there is no active transaction, the method does nothing.
        /// </summary>
        /// <returns>True if the transaction was successfully rolled back; otherwise, false.</returns>
        public override bool RollbackTransaction()
        {
            try
            {
                _errorMessage = String.Empty;
                if (_transaction != null)
                {
                    _transaction.Rollback();
                    _transaction = null;
                    _commandBuilder.RefreshSchema();
                }
                return true;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided DataTable. The method configures the adapter's
        /// commands for inserting, updating, and deleting rows based on the provided SQL commands.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable or DataSet being updated.</typeparam>
        /// <param name="table">The DataTable or DataSet containing the changes to be applied to the database.</param>
        /// <param name="insertCommand">The SQL command used to insert new rows.</param>
        /// <param name="updateCommand">The SQL command used to update existing rows.</param>
        /// <param name="deleteCommand">The SQL command used to delete rows.</param>
        /// <returns>The number of rows affected by the update operation.</returns>
        public override int Update<T>(T table, string insertCommand, string updateCommand, string deleteCommand)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;
            if (_adapter == null) return -1;

            try
            {
                if (!String.IsNullOrEmpty(insertCommand))
                    _adapter.InsertCommand = new(insertCommand);
                if (!String.IsNullOrEmpty(updateCommand))
                    _adapter.UpdateCommand = new(updateCommand);
                if (!String.IsNullOrEmpty(deleteCommand))
                    _adapter.DeleteCommand = new(deleteCommand);

                return _adapter.Update(table);;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return -1;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided DataTable. The method uses a SqlDataAdapter
        /// to apply the changes to the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable being updated.</typeparam>
        /// <param name="table">The DataTable containing the changes to be applied to the database.</param>
        /// <returns>The number of rows affected by the update operation.</returns>
        public override int Update<T>(T table)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;

            try
            {
                SqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided DataSet. The method uses a SqlDataAdapter
        /// to apply the changes to the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataSet being updated.</typeparam>
        /// <param name="dataSet">The DataSet containing the changes to be applied to the database.</param>
        /// <param name="sourceTable">The name of the table within the DataSet to be updated.</param>
        /// <returns>The number of rows affected by the update operation.</returns>
        public override int Update<T>(T dataSet, string sourceTable)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((dataSet == null) || String.IsNullOrEmpty(sourceTable) ||
                !dataSet.Tables.Contains(sourceTable)) return 0;

            try
            {
                DataTable table = dataSet.Tables[sourceTable];

                SqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided array of DataRows. The method uses a SqlDataAdapter
        /// to apply the changes to the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable containing the DataRows.</typeparam>
        /// <typeparam name="R">The type of the DataRow being updated.</typeparam>
        /// <param name="rows">The array of DataRows containing the changes to be applied to the database.</param>
        /// <returns>The number of rows affected by the update operation.</returns>
        public override int Update<T, R>(R[] rows)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((rows == null) || (rows.Length == 0)) return 0;

            try
            {
                T table = (T)rows[0].Table;

                SqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(rows);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided DataTable. The method checks if a SqlDataAdapter
        /// exists for the DataTable type and creates one if necessary. It also ensures that the adapter's commands
        /// are associated with the current transaction if one exists.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable being updated.</typeparam>
        /// <param name="table">The DataTable containing the changes to be applied to the database.</param>
        /// <returns>The SqlDataAdapter used to update the DataTable.</returns>
        private SqlDataAdapter UpdateAdapter<T>(T table) where T : DataTable, new()
        {
            if (table == null) return null;

            SqlDataAdapter adapter;
            if (!_adaptersDic.TryGetValue(typeof(T), out adapter))
            {
                CreateAdapter<T>(table);
                if (!_adaptersDic.TryGetValue(typeof(T), out adapter)) return null;
            }

            if (_transaction != null)
            {
                if ((adapter.InsertCommand != null) &&
                    ((adapter.InsertCommand.Transaction == null) || !adapter.InsertCommand.Transaction.Equals(_transaction)))
                    adapter.InsertCommand.Transaction = _transaction;
                if ((adapter.UpdateCommand != null) &&
                    ((adapter.UpdateCommand.Transaction == null) || !adapter.UpdateCommand.Transaction.Equals(_transaction)))
                    adapter.UpdateCommand.Transaction = _transaction;
                if ((adapter.DeleteCommand != null) &&
                    ((adapter.DeleteCommand.Transaction == null) || !adapter.DeleteCommand.Transaction.Equals(_transaction)))
                    adapter.DeleteCommand.Transaction = _transaction;
            }

            return adapter;
        }

        #endregion

        #region Protected Members

        /// <summary>
        /// Gets the prefix used for parameter names in SQL commands. For SQL Server, the parameter prefix is typically '@'.
        /// </summary>
        protected override string ParameterPrefix
        {
            get { return "@"; }
        }

        #region Browse Connection

        /// <summary>
        /// Displays a connection dialog to the user for configuring the SQL Server connection. The method creates a new window
        /// and ViewModel for the connection dialog, binds the ViewModel to the window, and handles the closing event of the dialog.
        /// </summary>
        protected override void BrowseConnection()
        {
            try
            {
                _connWindow = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _connViewModel = new()
                {
                    DisplayName = "SQL Server Connection"
                };

                // when ViewModel asks to be closed, close window
                _connViewModel.RequestClose -= _connViewModel_RequestClose; // Safety: avoid double subscription.
                _connViewModel.RequestClose +=
                    new UI.ViewModel.ViewModelConnectSqlServer.RequestCloseEventHandler(_connViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _connWindow.DataContext = _connViewModel;

                // show window
                _connWindow.ShowDialog();

                // throw error if connection failed
                if (!String.IsNullOrEmpty(_errorMessage)) throw (new Exception(_errorMessage));
            }
            catch (Exception ex)
            {
                MessageBox.Show("SQL Server responded with an error:\n\n" + ex.Message,
                    "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Handles the RequestClose event from the connection ViewModel. When the event is raised, the
        /// method unsubscribes from the event, closes the connection window, and updates the connection
        /// string, default schema, or error message based on the event arguments.
        /// </summary>
        /// <param name="connString">The connection string provided by the ViewModel.</param>
        /// <param name="defaultSchema">The default schema provided by the ViewModel.</param>
        /// <param name="errorMsg">The error message provided by the ViewModel, if any.</param>
        protected void _connViewModel_RequestClose(string connString, string defaultSchema, string errorMsg)
        {
            _connViewModel.RequestClose -= _connViewModel_RequestClose;
            _connWindow.Close();

            if (!String.IsNullOrEmpty(errorMsg))
            {
                _errorMessage = errorMsg;
            }
            else if (!String.IsNullOrEmpty(connString))
            {
                ConnectionString = connString;
                DefaultSchema = defaultSchema;
            }
        }

        #endregion

        #endregion

        #endregion

        #region SQLBuilder Members

        #region Public Members

        public override string QuotePrefix { get { return "["; } }

        public override string QuoteSuffix { get { return "]"; } }

        public override string StringLiteralDelimiter { get { return "'"; } }

        public override string DateLiteralPrefix { get { return "'"; } }

        public override string DateLiteralSuffix { get { return "'"; } }

        public override string WildcardSingleMatch { get { return "_"; } }

        public override string WildcardManyMatch { get { return "%"; } }

        public override string ConcatenateOperator { get { return "+"; } }

        /// <summary>
        /// Does not escape string delimiter or other special characters.
        /// Does check if value is already quoted.
        /// </summary>
        /// <param name="value">The value to be quoted.</param>
        /// <returns>The quoted value as a string.</returns>
        public override string QuoteValue(object value)
        {
            if (value == null) return "NULL";
            Type valueType = value.GetType();
            int colType;
            if (_typeMapSystemToSQL.TryGetValue(valueType, out colType))
            {
                string s = valueType == typeof(DateTime) ? ((DateTime)value).ToString("s") : value.ToString();
                switch ((SqlDbType)colType)
                {
                    case SqlDbType.Char:
                    case SqlDbType.Text:
                    case SqlDbType.VarChar:
                    case SqlDbType.Xml:
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case SqlDbType.NChar:
                    case SqlDbType.NText:
                    case SqlDbType.NVarChar:
                        if (s.Length == 0) return "N" + StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = "N" + StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case SqlDbType.Date:
                    case SqlDbType.DateTime:
                    case SqlDbType.DateTime2:
                    case SqlDbType.DateTimeOffset:
                    case SqlDbType.Time:
                    case SqlDbType.Timestamp:
                        if (s.Length == 0) return DateLiteralPrefix + DateLiteralSuffix ;
                        if (!s.StartsWith(DateLiteralPrefix)) s = DateLiteralPrefix + s;
                        if (!s.EndsWith(DateLiteralSuffix)) s += DateLiteralSuffix;
                        return s;
                    default:
                        return s;
                }
            }
            else
            {
                return value.ToString();
            }
        }

        #endregion

        #endregion

        #region Private Methods

        /// <summary>
        /// Populates the type mapping dictionaries that map between .NET types and SQL Server types. The method retrieves
        /// metadata from the SQL Server database and uses it to populate the dictionaries.
        /// </summary>
        /// <param name="isUnicode">Indicates whether the database uses Unicode encoding.</param>
        /// <param name="textLength">The maximum length of text columns.</param>
        /// <param name="binaryLength">The maximum length of binary columns.</param>
        /// <param name="timePrecision">The precision of time columns.</param>
        /// <param name="numericPrecision">The precision of numeric columns.</param>
        /// <param name="numericScale">The scale of numeric columns.</param>
        private void PopulateTypeMaps(bool isUnicode, uint textLength, uint binaryLength,
            uint timePrecision, uint numericPrecision, uint numericScale)
        {
            GetMetaData(typeof(SqlDbType), _connection, _transaction);

            Dictionary<Type, int> typeMapSystemToSQLAdd = [];
            typeMapSystemToSQLAdd.Add(typeof(Object), (int)SqlDbType.Variant);
            typeMapSystemToSQLAdd.Add(typeof(Boolean), (int)SqlDbType.Bit);
            typeMapSystemToSQLAdd.Add(typeof(SByte), (int)SqlDbType.Int);
            typeMapSystemToSQLAdd.Add(typeof(Byte), (int)SqlDbType.TinyInt);
            typeMapSystemToSQLAdd.Add(typeof(Int16), (int)SqlDbType.SmallInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt16), (int)SqlDbType.SmallInt);
            typeMapSystemToSQLAdd.Add(typeof(Int32), (int)SqlDbType.Int);
            typeMapSystemToSQLAdd.Add(typeof(UInt32), (int)SqlDbType.Int);
            typeMapSystemToSQLAdd.Add(typeof(Int64), (int)SqlDbType.BigInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt64), (int)SqlDbType.BigInt);
            typeMapSystemToSQLAdd.Add(typeof(Single), (int)SqlDbType.Real);
            typeMapSystemToSQLAdd.Add(typeof(Double), (int)SqlDbType.Float);
            typeMapSystemToSQLAdd.Add(typeof(Decimal), (int)SqlDbType.Decimal);
            typeMapSystemToSQLAdd.Add(typeof(DateTime), (int)SqlDbType.DateTime2);
            typeMapSystemToSQLAdd.Add(typeof(TimeSpan), (int)SqlDbType.DateTimeOffset);
            typeMapSystemToSQLAdd.Add(typeof(Byte[]), (int)SqlDbType.Image);
            typeMapSystemToSQLAdd.Add(typeof(Guid), (int)SqlDbType.UniqueIdentifier);
            if (isUnicode)
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)SqlDbType.NChar);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)SqlDbType.NVarChar);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)SqlDbType.NVarChar);
            }
            else
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)SqlDbType.Char);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)SqlDbType.VarChar);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)SqlDbType.VarChar);
            }

            Dictionary<int, Type> typeMapSQLToSystemAdd = [];
            typeMapSQLToSystemAdd.Add((int)SqlDbType.BigInt, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Binary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Bit, typeof(Boolean));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Char, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Date, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.DateTime, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.DateTime2, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.DateTimeOffset, typeof(TimeSpan));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Decimal, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Float, typeof(Double));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Image, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Int, typeof(Int32));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Money, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.NChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.NText, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.NVarChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Real, typeof(Single));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.SmallDateTime, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.SmallInt, typeof(Int16));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.SmallMoney, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Text, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Time, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Timestamp, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.TinyInt, typeof(Byte));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.UniqueIdentifier, typeof(Guid));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.VarBinary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.VarChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)SqlDbType.Variant, typeof(Object));

            Dictionary<string, int> sqlSynonymsAdd = [];
            sqlSynonymsAdd.Add("bigint", (int)SqlDbType.BigInt);
            sqlSynonymsAdd.Add("binary", (int)SqlDbType.Binary);
            sqlSynonymsAdd.Add("bit", (int)SqlDbType.Bit);
            sqlSynonymsAdd.Add("char", (int)SqlDbType.Char);
            sqlSynonymsAdd.Add("character", (int)SqlDbType.Char);
            sqlSynonymsAdd.Add("date", (int)SqlDbType.Date);
            sqlSynonymsAdd.Add("datetime", (int)SqlDbType.DateTime);
            sqlSynonymsAdd.Add("datetime2", (int)SqlDbType.DateTime2);
            sqlSynonymsAdd.Add("datetimeoffset", (int)SqlDbType.DateTimeOffset);
            sqlSynonymsAdd.Add("decimal", (int)SqlDbType.Decimal);
            sqlSynonymsAdd.Add("dec", (int)SqlDbType.Decimal);
            sqlSynonymsAdd.Add("numeric", (int)SqlDbType.Decimal);
            sqlSynonymsAdd.Add("float", (int)SqlDbType.Float);
            sqlSynonymsAdd.Add("double precision", (int)SqlDbType.Float);
            sqlSynonymsAdd.Add("image", (int)SqlDbType.Image);
            sqlSynonymsAdd.Add("int", (int)SqlDbType.Int);
            sqlSynonymsAdd.Add("integer", (int)SqlDbType.Int);
            sqlSynonymsAdd.Add("money", (int)SqlDbType.Money);
            sqlSynonymsAdd.Add("nchar", (int)SqlDbType.NChar);
            sqlSynonymsAdd.Add("national character", (int)SqlDbType.NChar);
            sqlSynonymsAdd.Add("national char", (int)SqlDbType.NChar);
            sqlSynonymsAdd.Add("ntext", (int)SqlDbType.NText);
            sqlSynonymsAdd.Add("national text", (int)SqlDbType.NText);
            sqlSynonymsAdd.Add("nvarchar", (int)SqlDbType.NVarChar);
            sqlSynonymsAdd.Add("national character varying", (int)SqlDbType.NVarChar);
            sqlSynonymsAdd.Add("national char varying", (int)SqlDbType.NVarChar);
            sqlSynonymsAdd.Add("real", (int)SqlDbType.Real);
            sqlSynonymsAdd.Add("smaldatetime", (int)SqlDbType.SmallDateTime);
            sqlSynonymsAdd.Add("smallint", (int)SqlDbType.SmallInt);
            sqlSynonymsAdd.Add("smallmoney", (int)SqlDbType.SmallMoney);
            sqlSynonymsAdd.Add("text", (int)SqlDbType.Text);
            sqlSynonymsAdd.Add("time", (int)SqlDbType.Time);
            sqlSynonymsAdd.Add("timestamp", (int)SqlDbType.Timestamp);
            sqlSynonymsAdd.Add("rowversion", (int)SqlDbType.Timestamp);
            sqlSynonymsAdd.Add("tinyint", (int)SqlDbType.TinyInt);
            sqlSynonymsAdd.Add("uniqueidentifier", (int)SqlDbType.UniqueIdentifier);
            sqlSynonymsAdd.Add("varbinary", (int)SqlDbType.VarBinary);
            sqlSynonymsAdd.Add("binary varying", (int)SqlDbType.VarBinary);
            sqlSynonymsAdd.Add("varchar", (int)SqlDbType.VarChar);
            sqlSynonymsAdd.Add("char varying", (int)SqlDbType.VarChar);
            sqlSynonymsAdd.Add("character varying", (int)SqlDbType.VarChar);
            sqlSynonymsAdd.Add("sql_variant", (int)SqlDbType.Variant);

            foreach (KeyValuePair<Type, int> kv in typeMapSystemToSQLAdd)
            {
                if (!_typeMapSystemToSQL.ContainsKey(kv.Key))
                    _typeMapSystemToSQL.Add(kv.Key, kv.Value);
            }

            if (isUnicode)
            {
                ReplaceType(typeof(Char), (int)SqlDbType.NChar, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)SqlDbType.NVarChar, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)SqlDbType.NVarChar, _typeMapSystemToSQL);
            }
            else
            {
                ReplaceType(typeof(Char), (int)SqlDbType.Char, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)SqlDbType.VarChar, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)SqlDbType.VarChar, _typeMapSystemToSQL);
            }

            foreach (KeyValuePair<int, Type> kv in typeMapSQLToSystemAdd)
            {
                if (!_typeMapSQLToSystem.ContainsKey(kv.Key))
                    _typeMapSQLToSystem.Add(kv.Key, kv.Value);
            }

            foreach (KeyValuePair<string, int> kv in sqlSynonymsAdd)
            {
                if (!_sqlSynonyms.ContainsKey(kv.Key))
                    _sqlSynonyms.Add(kv.Key, kv.Value);
            }

            _typeMapSQLToSQLCode = [];
            _typeMapSQLCodeToSQL = [];

            _typeMapSQLToSQLCode.Add((int)SqlDbType.BigInt, "BIGINT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Bit, "BIT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Char, "CHAR");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Date, "DATETIME");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.DateTime, "DATETIME");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Float, "FLOAT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Image, "IMAGE");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Int, "INT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Money, "MONEY");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.NChar, "NCHAR");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.NText, "NTEXT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.SmallDateTime, "SMALLDATETIME");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Real, "REAL");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.SmallInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.SmallMoney, "SMALLMONEY");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Text, "TEXT");

            _typeMapSQLCodeToSQL.Add("BIGINT", (int)SqlDbType.BigInt);
            _typeMapSQLCodeToSQL.Add("BIT", (int)SqlDbType.Bit);
            _typeMapSQLCodeToSQL.Add("CHAR", (int)SqlDbType.Char);
            //_typeMapSQLCodeToSQL.Add("DATETIME", (int)SqlDbType.Date);
            _typeMapSQLCodeToSQL.Add("DATETIME", (int)SqlDbType.DateTime);
            _typeMapSQLCodeToSQL.Add("FLOAT", (int)SqlDbType.Float);
            _typeMapSQLCodeToSQL.Add("IMAGE", (int)SqlDbType.Image);
            _typeMapSQLCodeToSQL.Add("INT", (int)SqlDbType.Int);
            _typeMapSQLCodeToSQL.Add("MONEY", (int)SqlDbType.Money);
            _typeMapSQLCodeToSQL.Add("NCHAR", (int)SqlDbType.NChar);
            _typeMapSQLCodeToSQL.Add("NTEXT", (int)SqlDbType.NText);
            _typeMapSQLCodeToSQL.Add("REAL", (int)SqlDbType.Real);
            _typeMapSQLCodeToSQL.Add("SMALLDATETIME", (int)SqlDbType.SmallDateTime);
            _typeMapSQLCodeToSQL.Add("SMALLINT", (int)SqlDbType.SmallInt);
            _typeMapSQLCodeToSQL.Add("SMALLMONEY", (int)SqlDbType.SmallMoney);
            _typeMapSQLCodeToSQL.Add("TEXT", (int)SqlDbType.Text);
            if (timePrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Time, String.Format("TIME ({0})", timePrecision));
                _typeMapSQLToSQLCode.Add((int)SqlDbType.DateTime2, String.Format("DATETIME2 ({0})", timePrecision));
                _typeMapSQLToSQLCode.Add((int)SqlDbType.DateTimeOffset, String.Format("DATETIMEOFFSET ({0})", timePrecision));

                _typeMapSQLCodeToSQL.Add(String.Format("TIME ({0})", timePrecision), (int)SqlDbType.Time);
                _typeMapSQLCodeToSQL.Add(String.Format("DATETIME2 ({0})", timePrecision), (int)SqlDbType.DateTime2);
                _typeMapSQLCodeToSQL.Add(String.Format("DATETIMEOFFSET ({0})", timePrecision), (int)SqlDbType.DateTimeOffset);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Time, "TIME");
                _typeMapSQLToSQLCode.Add((int)SqlDbType.DateTime2, "DATETIME2");
                _typeMapSQLToSQLCode.Add((int)SqlDbType.DateTimeOffset, "DATETIMEOFFSET");

                _typeMapSQLCodeToSQL.Add("TIME", (int)SqlDbType.Time);
                _typeMapSQLCodeToSQL.Add("DATETIME2", (int)SqlDbType.DateTime2);
                _typeMapSQLCodeToSQL.Add("DATETIMEOFFSET", (int)SqlDbType.DateTimeOffset);
            }
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Timestamp, "TIMESTAMP");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.TinyInt, "TINYINT");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.UniqueIdentifier, "UNIQUEIDENTIFIER");
            _typeMapSQLToSQLCode.Add((int)SqlDbType.Variant, "SQL_VARIANT");

            _typeMapSQLCodeToSQL.Add("TIMESTAMP", (int)SqlDbType.Timestamp);
            _typeMapSQLCodeToSQL.Add("TINYINT", (int)SqlDbType.TinyInt);
            _typeMapSQLCodeToSQL.Add("UNIQUEIDENTIFIER", (int)SqlDbType.UniqueIdentifier);
            _typeMapSQLCodeToSQL.Add("SQL_VARIANT", (int)SqlDbType.Variant);

            if (binaryLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Binary, String.Format("BINARY ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)SqlDbType.VarBinary, String.Format("VARBINARY ({0})", binaryLength));

                _typeMapSQLCodeToSQL.Add(String.Format("BINARY ({0})", binaryLength), (int)SqlDbType.Binary);
                _typeMapSQLCodeToSQL.Add(String.Format("VARBINARY ({0})", binaryLength), (int)SqlDbType.VarBinary);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Binary, "BINARY");
                _typeMapSQLToSQLCode.Add((int)SqlDbType.VarBinary, "VARBINARY");

                _typeMapSQLCodeToSQL.Add("BINARY", (int)SqlDbType.Binary);
                _typeMapSQLCodeToSQL.Add("VARBINARY", (int)SqlDbType.VarBinary);
            }
            if ((numericPrecision > 0) && (numericScale > 0))
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Decimal, String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale), (int)SqlDbType.Decimal);
            }
            else if (numericPrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Decimal, String.Format("DECIMAL ({0})", numericPrecision));
                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0})", numericPrecision), (int)SqlDbType.Decimal);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.Decimal, "DECIMAL");
                _typeMapSQLCodeToSQL.Add("DECIMAL", (int)SqlDbType.Decimal);
            }
            if (textLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.NVarChar, String.Format("NVARCHAR ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)SqlDbType.VarChar, String.Format("VARCHAR ({0})", textLength));

                _typeMapSQLCodeToSQL.Add(String.Format("NVARCHAR ({0})", textLength), (int)SqlDbType.NVarChar);
                _typeMapSQLCodeToSQL.Add(String.Format("VARCHAR ({0})", textLength), (int)SqlDbType.VarChar);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)SqlDbType.NVarChar, "NVARCHAR");
                _typeMapSQLToSQLCode.Add((int)SqlDbType.VarChar, "VARCHAR");

                _typeMapSQLCodeToSQL.Add("NVARCHAR", (int)SqlDbType.NVarChar);
                _typeMapSQLCodeToSQL.Add("VARCHAR", (int)SqlDbType.VarChar);
            }
        }

        #endregion
    }
}