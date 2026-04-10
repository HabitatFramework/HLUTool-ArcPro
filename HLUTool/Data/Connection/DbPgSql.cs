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

using ArcGIS.Desktop.Framework;
using HLU.Enums;
using Npgsql;
using NpgsqlTypes;
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
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.Data.Connection
{
    class DbPgSql : DbBase
    {
        #region Private Members

        private NpgsqlConnectionStringBuilder _connStrBuilder;
        private NpgsqlConnection _connection;
        private NpgsqlCommand _command;
        private NpgsqlDataAdapter _adapter;
        private NpgsqlCommandBuilder _commandBuilder;
        private NpgsqlTransaction _transaction;
        private string _encoding;
        private Dictionary<Type, NpgsqlDataAdapter> _adaptersDic = [];

        private UI.View.Connection.ViewConnectPgSql _connWindow;
        private UI.ViewModel.ViewModelConnectPgSql _connViewModel;

        #endregion Private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DbPgSql class and opens a connection to the PostgreSQL
        /// database using the provided connection string and other parameters. It also sets up the
        /// command, data adapter, and command builder for executing SQL commands and filling data
        /// tables. If the connection string is null or empty, an exception is thrown. Any
        /// exceptions raised during the connection process are propagated back to the calling method.
        /// </summary>
        /// <param name="connString">The connection string for the PostgreSQL database.</param>
        /// <param name="defaultSchema">The default schema to use for the connection.</param>
        /// <param name="promptPwd">Indicates whether to prompt for a password.</param>
        /// <param name="pwdMask">The mask to use for the password.</param>
        /// <param name="useCommandBuilder">Indicates whether to use a command builder.</param>
        /// <param name="useColumnNames">Indicates whether to use column names.</param>
        /// <param name="isUnicode">Indicates whether to use Unicode encoding.</param>
        /// <param name="useTimeZone">Indicates whether to use time zone information.</param>
        /// <param name="textLength">The maximum length of text fields.</param>
        /// <param name="binaryLength">The maximum length of binary fields.</param>
        /// <param name="timePrecision">The precision of time fields.</param>
        /// <param name="numericPrecision">The precision of numeric fields.</param>
        /// <param name="numericScale">The scale of numeric fields.</param>
        /// <param name="connectTimeOut">The connection timeout in seconds.</param>
        public DbPgSql(ref string connString, ref string defaultSchema, ref bool promptPwd, string pwdMask,
            bool useCommandBuilder, bool useColumnNames, bool isUnicode, bool useTimeZone, uint textLength,
            uint binaryLength, uint timePrecision, uint numericPrecision, uint numericScale, int connectTimeOut)
            : base(ref connString, ref defaultSchema, ref promptPwd, pwdMask, useCommandBuilder, useColumnNames,
            isUnicode, useTimeZone, textLength, binaryLength, timePrecision, numericPrecision, numericScale, connectTimeOut)
        {
            if (String.IsNullOrEmpty(ConnectionString)) throw (new Exception("No connection string"));

            try
            {
                // Append connection timeout to connection string.
                string connectionString = String.Format("{0};{1}", ConnectionString, connectTimeOut);

                Login("User Name", connectionString, ref promptPwd, ref _connStrBuilder, ref _connection);

                PopulateTypeMaps(IsUnicode, UseTimeZone, TextLength, BinaryLength,
                    TimePrecision, NumericPrecision, NumericScale);
                SetPgClientEncoding();

                _command = _connection.CreateCommand();
                _adapter = new(_command);
                _commandBuilder = new(_adapter);

                connString = MaskPassword(_connStrBuilder, pwdMask);
                defaultSchema = DefaultSchema;
            }
            catch { throw; }
        }
        #endregion Constructor

        #region Override Members

        /// <summary>
        /// Gets the backend type for this database connection, which is PostgreSQL in this case.
        /// </summary>
        /// <value>The backend type for this database connection.</value>
        public override Backends Backend { get { return Backends.PostgreSql; } }

        /// <summary>
        /// Checks if the provided DataSet contains the necessary tables and columns that match the
        /// schema of the connected PostgreSQL database.
        /// </summary>
        /// <param name="ds">The DataSet to check against the database schema.</param>
        /// <param name="errorMessage">An output parameter that will contain an error message if the schema validation fails.</param>
        /// <returns>True if the DataSet matches the database schema; otherwise, false.</returns>
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

                    if (dbSchemaCols.Any())
                    {
                        messageText.Append(String.Format("\n\nMissing table: {0}", QuoteIdentifier(t.TableName)));
                    }
                    else
                    {
                        string[] checkColumns = [.. (from dsCol in t.Columns.Cast<DataColumn>()
                                                 let dbCols = from dbCol in dbSchemaCols
                                                              where dbCol.ColumnName == dsCol.ColumnName &&
                                                              DbToSystemType(SQLCodeToSQLType(dbCol.DataType)) == dsCol.DataType
                                                              select dbCol
                                                 where !dbCols.Any()
                                                 select QuoteIdentifier(dsCol.ColumnName) + " (" +
                                                 ((SqlDbType)SystemToDbType(dsCol.DataType) + ")").ToString())];
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
        /// Gets the database connection object for this PostgreSQL connection, which is an instance of NpgsqlConnection.
        /// </summary>
        /// <value>The database connection object for this PostgreSQL connection.</value>
        public override IDbConnection Connection { get { return _connection; } }

        /// <summary>
        /// Gets the connection string builder for this PostgreSQL connection, which is an instance of NpgsqlConnectionStringBuilder.
        /// </summary>
        /// <value>The connection string builder for this PostgreSQL connection.</value>
        public override DbConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        /// <summary>
        /// Gets the database transaction object for this PostgreSQL connection, which is an instance of NpgsqlTransaction.
        /// </summary>
        /// <value>The database transaction object for this PostgreSQL connection.</value>
        public override IDbTransaction Transaction
        {
            get { return _transaction; }
        }

        /// <summary>
        /// Creates and returns a new database command object for this PostgreSQL connection, which is an instance of NpgsqlCommand.
        /// </summary>
        /// <returns>A new instance of NpgsqlCommand.</returns>
        public override IDbCommand CreateCommand()
        {
            if (_connection != null)
                return _connection.CreateCommand();
            else
                return new NpgsqlCommand();
        }

        /// <summary>
        /// Creates and returns a new data adapter for the specified DataTable. The method checks if
        /// an adapter for the type of the provided table already exists in the _adaptersDic
        /// dictionary. If it does, it returns the existing adapter. If not, it creates a new
        /// NpgsqlDataAdapter, sets up the necessary commands (SELECT, INSERT, UPDATE, DELETE) based
        /// on the schema of the provided table, and adds it to the _adaptersDic dictionary before
        /// returning it. The method also handles parameters for nullable columns and primary keys
        /// to ensure proper command execution.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable.</typeparam>
        /// <param name="table">The DataTable for which to create the adapter.</param>
        /// <returns>A new instance of NpgsqlDataAdapter for the specified DataTable.</returns>
        public override IDbDataAdapter CreateAdapter<T>(T table)
        {
            table ??= new T();

            NpgsqlDataAdapter adapter;

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

                List<NpgsqlParameter> deleteParams = [];
                List<NpgsqlParameter> insertParams = [];
                List<NpgsqlParameter> updateParams = [];
                List<NpgsqlParameter> updateParamsOrig = [];

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
                NpgsqlDbType isNullType = (NpgsqlDbType)isNullTypeInt;

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
                        (NpgsqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    insColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    insertParams.Add(CreateParameter(insColParamName,
                        (NpgsqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    updateParams.Add(CreateParameter(updColParamName,
                        (NpgsqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName, i + _startParamNo + columnCount + nullParamCount);
                    updateParamsOrig.Add(CreateParameter(updOrigParamName,
                        (NpgsqlDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

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

                adapter.SelectCommand = new()
                {
                    CommandType = CommandType.Text,
                    Connection = _connection,
                    CommandText = String.Format("SELECT {0} FROM {1}", sbTargetList, tableName)
                };

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
                    if (_transaction != null)
                        adapter.SelectCommand.Transaction = (NpgsqlTransaction)_transaction;
                    NpgsqlCommandBuilder cmdBuilder = new(adapter);
                    adapter.DeleteCommand = cmdBuilder.GetDeleteCommand(_useColumnNames);
                    adapter.UpdateCommand = cmdBuilder.GetUpdateCommand(_useColumnNames);
                    adapter.InsertCommand = cmdBuilder.GetInsertCommand(_useColumnNames);
                }

                adapter.UpdateCommand.CommandText += ";\r\n" +
                    String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkUpd);
                adapter.InsertCommand.CommandText += ";\r\n" +
                    String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkIns);

                if (typeof(T) != typeof(DataTable))
                    _adaptersDic.Add(typeof(T), adapter);
            }

            return adapter;
        }

        /// <summary>
        /// Creates and returns a new NpgsqlParameter with the specified properties.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The NpgsqlDbType of the parameter.</param>
        /// <param name="direction">The direction of the parameter.</param>
        /// <param name="srcColumn">The source column of the parameter.</param>
        /// <param name="srcVersion">The source version of the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a null value.</param>
        /// <returns>A new instance of NpgsqlParameter with the specified properties.</returns>
        private NpgsqlParameter CreateParameter(string name, NpgsqlDbType type, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            NpgsqlParameter param = new(name, type)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Creates and returns a new NpgsqlParameter with the specified properties, including the value of the parameter.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="direction">The direction of the parameter.</param>
        /// <param name="srcColumn">The source column of the parameter.</param>
        /// <param name="srcVersion">The source version of the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a null value.</param>
        /// <returns>A new instance of NpgsqlParameter with the specified properties, including the value of the parameter.</returns>
        private NpgsqlParameter CreateParameter(string name, object value, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            NpgsqlParameter param = new(name, value)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Generates a parameter name based on the provided prefix, column name, and parameter number.
        /// </summary>
        /// <param name="prefix">The prefix to use for the parameter name.</param>
        /// <param name="columnName">The name of the column.</param>
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
        /// Generates a parameter name based on the provided parameter name. For PostgreSQL, the
        /// parameter marker is the same as the parameter name.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The parameter marker for the specified parameter name.</returns>
        protected override string ParameterMarker(string parameterName)
        {
            return parameterName;
        }

        /// <summary>
        /// Fills the schema of the provided table based on the specified schema type and SQL query.
        /// If an adapter for the type of the provided table already exists in the _adaptersDic
        /// dictionary, it uses that adapter to fill the schema. Otherwise, it creates a new adapter
        /// using the provided SQL query and fills the schema using that adapter. The method also
        /// handles opening and closing the database connection as needed, and captures any
        /// exceptions that occur during the process, storing the error message in the _errorMessage field.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="schemaType">The type of schema to fill.</param>
        /// <param name="sql">The SQL query to use for filling the schema.</param>
        /// <param name="table">The table to fill the schema for.</param>
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
                NpgsqlDataAdapter adapter = UpdateAdapter(table);
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
        /// Fills the provided table with data based on the specified SQL query. If an adapter for
        /// the type of the provided table already exists in the _adaptersDic dictionary, it uses
        /// that adapter to fill the table. Otherwise, it creates a new adapter using the provided
        /// SQL query and fills the table using that adapter. The method also handles opening and
        /// closing the database connection as needed, and captures any exceptions that occur during
        /// the process, storing the error message in the _errorMessage field. The method returns
        /// the number of rows added to the table, or -1 if an error occurred.
        /// </summary>
        /// <typeparam name="T">The type of the table to fill.</typeparam>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="table">The table to fill with data.</param>
        /// <returns>The number of rows added to the table, or -1 if an error occurred.</returns>
        public override int FillTable<T>(string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return 0;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                NpgsqlDataAdapter adapter = UpdateAdapter(table);
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
        /// Begins a database transaction with the specified isolation level. If there is an
        /// existing transaction, it either commits or rolls back that transaction based on the
        /// value of the commitPrevious parameter before starting a new transaction. The method also
        /// refreshes the command builder schema after starting the transaction. If any exceptions
        /// occur during the process, the error message is stored in the _errorMessage field and the
        /// method returns false; otherwise, it returns true.
        /// </summary>
        /// <param name="commitPrevious">Indicates whether to commit the previous transaction before starting a new one.</param>
        /// <param name="isolationLevel">The isolation level for the new transaction.</param>
        /// <returns>True if the transaction was successfully started; otherwise, false.</returns>
        public override bool BeginTransaction(bool commitPrevious, System.Data.IsolationLevel isolationLevel)
        {
            try
            {
                _errorMessage = String.Empty;
                if (_transaction != null)
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
        /// Commits the current database transaction. If there is an active transaction, it commits
        /// that transaction and refreshes the command builder schema. If any exceptions occur
        /// during the process, the error message is stored in the _errorMessage field and the
        /// method returns false; otherwise, it returns true.
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
        /// Rolls back the current database transaction. If there is an active transaction, it rolls
        /// back that transaction and refreshes the command builder schema. If any exceptions occur
        /// during the process,
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
        /// Executes the specified SQL query and returns an IDataReader containing the results. The
        /// method sets the command type and command timeout for the query, and handles opening the
        /// database connection if it is not already open. If there is an active transaction, it
        /// associates the command with that transaction and refreshes the command builder schema.
        /// If any exceptions occur during the execution of the query, the error message is stored
        /// in the _errorMessage field and the method returns null; otherwise, it returns an
        /// IDataReader with the query results.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>An IDataReader containing the query results, or null if an error occurs.</returns>
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

                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

                if (_transaction != null)
                {
                    _command.Transaction = _transaction;
                    _commandBuilder.RefreshSchema();
                }

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
        /// Executes the specified SQL query and returns the number of rows affected. The method
        /// sets the command type and command timeout for the query, and handles opening the
        /// database connection if it is not already open. If there is an active transaction, it
        /// associates the command with that transaction and refreshes the command builder schema.
        /// If any exceptions occur during the execution of the query, the error message is stored
        /// in the _errorMessage field and the method returns -1; otherwise, it returns the number
        /// of rows affected by the query.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>The number of rows affected by the query, or -1 if an error occurs.</returns>
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

                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();

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
        /// Executes the query and returns the first column of the first row
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>The value of the first column of the first row, or null if an error occurs.</returns>
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

                if (_transaction != null)
                    _command.Transaction = _transaction;

                _commandBuilder.RefreshSchema();

                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open)
                    _connection.Open();

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
        /// Executes the query and returns the first column of the first row
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The value of the first column of the first row, or null if an error occurs.</returns>
        public override async Task<object> ExecuteScalarAsync(string sql, int commandTimeout, CommandType commandType, CancellationToken cancellationToken = default)
        {
            _errorMessage = String.Empty;

            if (String.IsNullOrEmpty(sql)) return null;

            ConnectionState previousConnectionState = _connection.State;

            try
            {
                _command.CommandType = commandType;
                _command.CommandTimeout = commandTimeout;
                _command.CommandText = sql;

                if (_transaction != null)
                    _command.Transaction = _transaction;

                _commandBuilder.RefreshSchema();

                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open)
                    await _connection.OpenAsync(cancellationToken);

                return await _command.ExecuteScalarAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return null;
            }
            finally
            {
                if (previousConnectionState == ConnectionState.Closed)
                    _connection.Close();
            }
        }

        /// <summary>
        /// Validates the SQL query by executing it and throwing
        /// any execeptions raised back to the calling method.
        /// </summary>
        /// <param name="sql">The SQL query to validate (which should be a non-update query).</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>True if the query is valid, false otherwise.</returns>
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

                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

                if (_transaction != null) _command.Transaction = _transaction;
                _commandBuilder.RefreshSchema();

                _command.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                // Return the invalid reason as the error message
                _errorMessage = ex.Message;
                return false;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the data source with the changes made to the provided table. The method sets the
        /// InsertCommand, UpdateCommand, and DeleteCommand of the adapter based on the provided SQL
        /// commands, and then calls the Update method of the adapter to apply the changes to the
        /// data source. The method also handles opening and closing the database connection as
        /// needed, and captures any exceptions that occur during the process, storing the error
        /// message in the _errorMessage field. The method returns the number of rows affected by
        /// the update operation, or -1 if an error occurred.
        /// </summary>
        /// <typeparam name="T">The type of the table or dataset being updated.</typeparam>
        /// <param name="table">The table containing the changes to be applied to the data source.</param>
        /// <param name="insertCommand">The SQL command to use for inserting new rows.</param>
        /// <param name="updateCommand">The SQL command to use for updating existing rows.</param>
        /// <param name="deleteCommand">The SQL command to use for deleting rows.</param>
        /// <returns>The number of rows affected by the update operation, or -1 if an error occurred.</returns>
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

                return _adapter.Update(table);
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return -1;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the data source with the changes made to the provided table. The method
        /// retrieves or creates an adapter for the type of the provided table, and then calls the
        /// Update method of that adapter to apply the changes to the data source. The method also
        /// handles opening and closing the database connection as needed, and captures any
        /// exceptions that occur during the process, storing the error message in the _errorMessage
        /// field. The method returns the number of rows affected by the update operation, or -1 if
        /// an error occurred.
        /// </summary>
        /// <typeparam name="T">The type of the table or dataset being updated.</typeparam>
        /// <param name="table">The table containing the changes to be applied to the data source.</param>
        /// <returns>The number of rows affected by the update operation, or -1 if an error occurred.</returns>
        public override int Update<T>(T table)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open)
                _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;

            try
            {
                NpgsqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the data source with the changes made to the specified table within the provided
        /// dataset. The method retrieves or creates an adapter for the type of the provided table,
        /// and then calls the Update method of that adapter to apply the changes to the data
        /// source. The method also handles opening and closing the database connection as needed,
        /// and captures any exceptions that occur during the process, storing the error message in
        /// the _errorMessage field. The method returns the number of rows affected by the update
        /// operation, or -1 if an error occurred.
        /// </summary>
        /// <typeparam name="T">The type of the dataset containing the table with the changes to be applied to the data source.</typeparam>
        /// <param name="dataSet">The dataset containing the table with the changes to be applied to the data source.</param>
        /// <param name="sourceTable">The name of the table within the dataset that contains the changes to be applied to the data source.</param>
        /// <returns>The number of rows affected by the update operation, or -1 if an error occurred.</returns>
        public override int Update<T>(T dataSet, string sourceTable)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open)
                _connection.Open();

            if ((dataSet == null) || String.IsNullOrEmpty(sourceTable) ||
                !dataSet.Tables.Contains(sourceTable)) return 0;

            try
            {
                DataTable table = dataSet.Tables[sourceTable];

                NpgsqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the data source with the changes made to the provided array of DataRow objects.
        /// The method retrieves or creates an adapter for the type of the provided rows, and then
        /// calls the Update method of that adapter to apply the changes to the data source. The
        /// method also handles opening and closing the database connection as needed, and captures
        /// any exceptions that occur during the process, storing the error message in the
        /// _errorMessage field. The method returns the number of rows affected by the update
        /// operation, or -1 if an error occurred.
        /// </summary>
        /// <typeparam name="T">The type of the dataset containing the table with the changes to be applied to the data source.</typeparam>
        /// <typeparam name="R">The type of the DataRow objects containing the changes to be applied to the data source.</typeparam>
        /// <param name="rows">The array of DataRow objects containing the changes to be applied to the data source.</param>
        /// <returns>The number of rows affected by the update operation, or -1 if an error occurred.</returns>
        public override int Update<T, R>(R[] rows)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open)
                _connection.Open();

            if ((rows == null) || (rows.Length == 0)) return 0;

            try
            {
                T table = (T)rows[0].Table;

                NpgsqlDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(rows);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Retrieves an existing NpgsqlDataAdapter for the type of the provided table from the _adaptersDic dictionary, or creates a new adapter if one does not already exist. The method checks if the provided table is null and returns null if it is. If an adapter for the type of the provided table does not exist in the _adaptersDic dictionary, it calls the CreateAdapter method to create a new adapter for that type. If an adapter is successfully retrieved or created, the method checks if there is an active transaction and, if so, associates the InsertCommand, UpdateCommand, and DeleteCommand of the adapter with that transaction. Finally, the method returns the adapter for the provided table.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable for which to retrieve or create an adapter.</typeparam>
        /// <param name="table">The DataTable for which to retrieve or create an adapter.</param>
        /// <returns>The NpgsqlDataAdapter for the provided table, or null if the table is null or the adapter could not be created.</returns>
        private NpgsqlDataAdapter UpdateAdapter<T>(T table) where T : DataTable, new()
        {
            if (table == null) return null;

            NpgsqlDataAdapter adapter;
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

        #endregion Override Methods

        #region Protected Properties

        /// <summary>
        /// Gets the parameter prefix used for PostgreSQL parameters. In PostgreSQL, the parameter
        /// prefix is "@", so this property returns "@".
        /// </summary>
        /// <value>The parameter prefix used for PostgreSQL parameters.</value>
        protected override string ParameterPrefix
        {
            get { return "@"; }
        }

        #endregion Protected Properties

        #region Browse Connection

        /// <summary>
        /// Displays a connection dialog window for the user to input the connection details for a
        /// PostgreSQL database. The method creates an instance of the connection dialog window and
        /// its associated ViewModel, sets the main application window as the owner of the dialog,
        /// and displays the dialog to the user. When the user submits the connection details or
        /// cancels the dialog, the method captures the connection string, encoding, default schema,
        /// and any error messages from the ViewModel and updates the corresponding fields in the
        /// DbPgSql class. If any exceptions occur during this process, an error message is
        /// displayed to the user in a message box, and the exception is re-thrown.
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
                    DisplayName = "PostgreSQL Connection"
                };

                // when ViewModel asks to be closed, close window
                _connViewModel.RequestClose -= ConnViewModel_RequestClose; // Safety: avoid double subscription.
                _connViewModel.RequestClose +=
                    new UI.ViewModel.ViewModelConnectPgSql.RequestCloseEventHandler(ConnViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _connWindow.DataContext = _connViewModel;

                // show window
                _connWindow.ShowDialog();

                // throw error if connection failed
                if (!String.IsNullOrEmpty(_errorMessage)) throw (new Exception(_errorMessage));
            }
            catch (Exception ex)
            {
                MessageBox.Show("PostgreSQL Server responded with an error:\n\n" + ex.Message,
                     "PostgreSQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Handles the RequestClose event from the connection ViewModel. This method is called when
        /// the user submits the connection details or cancels the dialog. It updates the connection
        /// string, encoding, default schema, and error message fields in the DbPgSql class based on
        /// the values provided by the ViewModel.
        /// </summary>
        /// <param name="connString">The connection string provided by the user.</param>
        /// <param name="encoding">The encoding provided by the user.</param>
        /// <param name="defaultSchema">The default schema provided by the user.</param>
        /// <param name="errorMsg">Any error message provided by the ViewModel.</param>
        protected void ConnViewModel_RequestClose(string connString, string encoding,
            string defaultSchema, string errorMsg)
        {
            _connViewModel.RequestClose -= ConnViewModel_RequestClose;
            _connWindow.Close();

            if (!String.IsNullOrEmpty(errorMsg))
            {
                _errorMessage = errorMsg;
            }
            else if (!String.IsNullOrEmpty(connString))
            {
                ConnectionString = connString;
                DefaultSchema = defaultSchema;
                if (!String.IsNullOrEmpty(encoding)) _encoding = encoding;
            }
        }

        #endregion Browse Connection

        #region SQLBuilder Members

        #region Public Members

        public override string QuotePrefix { get { return "\""; } }

        public override string QuoteSuffix { get { return "\""; } }

        public override string StringLiteralDelimiter { get { return "'"; } }

        public override string DateLiteralPrefix { get { return "'"; } }

        public override string DateLiteralSuffix { get { return "'"; } }

        public override string WildcardSingleMatch { get { return "_"; } }

        public override string WildcardManyMatch { get { return "%"; } }

        public override string ConcatenateOperator { get { return "||"; } }

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
                switch ((NpgsqlDbType)colType)
                {
                    case NpgsqlDbType.Char:
                    case NpgsqlDbType.Name:
                    case NpgsqlDbType.Text:
                    case NpgsqlDbType.Varchar:
                    case NpgsqlDbType.Xml:
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case NpgsqlDbType.Date:
                    case NpgsqlDbType.Time:
                    case NpgsqlDbType.Timestamp:
                    case NpgsqlDbType.TimestampTz:
                    case NpgsqlDbType.TimeTz:
                        if (s.Length == 0) return DateLiteralPrefix + DateLiteralSuffix;
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

        #endregion Public Members

        #endregion SQLBuilder Members

        #region Private Methods

        /// <summary>
        /// Populates the type mapping dictionaries that map between .NET types and PostgreSQL
        /// types. The method takes several parameters that specify characteristics of the types to
        /// be mapped, such as whether they are Unicode, whether they use time zones, and their
        /// lengths and precisions. The method retrieves metadata for the NpgsqlDbType enumeration
        /// and then populates three dictionaries: one that maps .NET types to PostgreSQL types, one
        /// that maps PostgreSQL types to .NET types, and one that maps SQL type synonyms to
        /// PostgreSQL types. These mappings are used throughout the DbPgSql class to handle type
        /// conversions and parameter mappings when executing queries and updates against the
        /// PostgreSQL database.
        /// </summary>
        /// <param name="isUnicode">Indicates whether the types to be mapped are Unicode.</param>
        /// <param name="useTimeZone">Indicates whether the types to be mapped use time zones.</param>
        /// <param name="textLength">Specifies the length of text types to be mapped.</param>
        /// <param name="binaryLength">Specifies the length of binary types to be mapped.</param>
        /// <param name="timePrecision">Specifies the precision of time types to be mapped.</param>
        /// <param name="numericPrecision">Specifies the precision of numeric types to be mapped.</param>
        /// <param name="numericScale">Specifies the scale of numeric types to be mapped.</param>
        private void PopulateTypeMaps(bool isUnicode, bool useTimeZone, uint textLength,
            uint binaryLength, uint timePrecision, uint numericPrecision, uint numericScale)
        {
            string timeZoneSuffix = useTimeZone ? "tz" : "";

            GetMetaData(typeof(NpgsqlDbType), _connection, _transaction);

            Dictionary<Type, int> typeMapSystemToSQLAdd = [];
            typeMapSystemToSQLAdd.Add(typeof(Boolean), (int)NpgsqlDbType.Boolean);
            typeMapSystemToSQLAdd.Add(typeof(Byte), (int) NpgsqlDbType.Smallint);
            typeMapSystemToSQLAdd.Add(typeof(Char), (int) NpgsqlDbType.Char);
            typeMapSystemToSQLAdd.Add(typeof(DateTime), (int)(useTimeZone ? NpgsqlDbType.TimestampTz : NpgsqlDbType.Timestamp));
            typeMapSystemToSQLAdd.Add(typeof(TimeSpan), (int)NpgsqlDbType.Interval);
            typeMapSystemToSQLAdd.Add(typeof(Decimal), (int) NpgsqlDbType.Numeric);
            typeMapSystemToSQLAdd.Add(typeof(Double), (int) NpgsqlDbType.Numeric);
            typeMapSystemToSQLAdd.Add(typeof(Int16), (int) NpgsqlDbType.Smallint);
            typeMapSystemToSQLAdd.Add(typeof(Int32), (int) NpgsqlDbType.Integer);
            typeMapSystemToSQLAdd.Add(typeof(Int64), (int) NpgsqlDbType.Bigint);
            typeMapSystemToSQLAdd.Add(typeof(Object), (int) NpgsqlDbType.Bytea);
            typeMapSystemToSQLAdd.Add(typeof(SByte), (int) NpgsqlDbType.Smallint);
            typeMapSystemToSQLAdd.Add(typeof(Single), (int) NpgsqlDbType.Real);
            typeMapSystemToSQLAdd.Add(typeof(String), (int) NpgsqlDbType.Varchar);
            typeMapSystemToSQLAdd.Add(typeof(UInt16), (int) NpgsqlDbType.Smallint);
            typeMapSystemToSQLAdd.Add(typeof(UInt32), (int) NpgsqlDbType.Integer);
            typeMapSystemToSQLAdd.Add(typeof(UInt64), (int) NpgsqlDbType.Bigint);
            typeMapSystemToSQLAdd.Add(typeof(Byte[]), (int) NpgsqlDbType.Bytea);
            typeMapSystemToSQLAdd.Add(typeof(Char[]), (int) NpgsqlDbType.Varchar);
            typeMapSystemToSQLAdd.Add(typeof(Guid), (int)NpgsqlDbType.Uuid);

            Dictionary<int, Type> typeMapSQLToSystemAdd = [];
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Bigint, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Bit, typeof(Boolean));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Boolean, typeof(Boolean));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Bytea, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Char, typeof(Char));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Date, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Double, typeof(Double));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Integer, typeof(Int32));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Money, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Numeric, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Real, typeof(Single));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Smallint, typeof(Int16));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Text, typeof(String));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Interval, typeof(TimeSpan));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Time, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Timestamp, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.TimestampTz, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Uuid, typeof(Guid));
            typeMapSQLToSystemAdd.Add((int)NpgsqlDbType.Varchar, typeof(String));

            Dictionary<string, int> sqlSynonymsAdd = [];
            sqlSynonymsAdd.Add("bigint", (int)NpgsqlDbType.Bigint);
            sqlSynonymsAdd.Add("bigserial", (int)NpgsqlDbType.Bigint);
            sqlSynonymsAdd.Add("bit varying", (int)NpgsqlDbType.Bytea); // ??
            sqlSynonymsAdd.Add("bit", (int)NpgsqlDbType.Bit);
            sqlSynonymsAdd.Add("bool", (int)NpgsqlDbType.Boolean);
            sqlSynonymsAdd.Add("boolean", (int)NpgsqlDbType.Boolean);
            sqlSynonymsAdd.Add("box", (int)NpgsqlDbType.Box);
            sqlSynonymsAdd.Add("bytea", (int)NpgsqlDbType.Bytea);
            sqlSynonymsAdd.Add("char", (int)NpgsqlDbType.Char);
            sqlSynonymsAdd.Add("character varying", (int)NpgsqlDbType.Varchar);
            sqlSynonymsAdd.Add("character", (int)NpgsqlDbType.Char);
            sqlSynonymsAdd.Add("cidr", (int)NpgsqlDbType.Varchar); // ??
            sqlSynonymsAdd.Add("circle", (int)NpgsqlDbType.Circle);
            sqlSynonymsAdd.Add("date", (int)NpgsqlDbType.Date);
            sqlSynonymsAdd.Add("double precision", (int)NpgsqlDbType.Double);
            sqlSynonymsAdd.Add("float4", (int)NpgsqlDbType.Real);
            sqlSynonymsAdd.Add("float8", (int)NpgsqlDbType.Double);
            sqlSynonymsAdd.Add("inet", (int)NpgsqlDbType.Inet);
            sqlSynonymsAdd.Add("int", (int)NpgsqlDbType.Integer);
            sqlSynonymsAdd.Add("int2", (int)NpgsqlDbType.Smallint);
            sqlSynonymsAdd.Add("int4", (int)NpgsqlDbType.Integer);
            sqlSynonymsAdd.Add("int8", (int)NpgsqlDbType.Bigint);
            sqlSynonymsAdd.Add("integer", (int)NpgsqlDbType.Integer);
            sqlSynonymsAdd.Add("interval", (int)NpgsqlDbType.Interval);
            sqlSynonymsAdd.Add("line", (int)NpgsqlDbType.Line);
            sqlSynonymsAdd.Add("lseg", (int)NpgsqlDbType.LSeg);
            sqlSynonymsAdd.Add("macaddr", (int)NpgsqlDbType.Varchar); // ??
            sqlSynonymsAdd.Add("money", (int)NpgsqlDbType.Money);
            sqlSynonymsAdd.Add("numeric", (int)NpgsqlDbType.Numeric);
            sqlSynonymsAdd.Add("path", (int)NpgsqlDbType.Path);
            sqlSynonymsAdd.Add("point", (int)NpgsqlDbType.Point);
            sqlSynonymsAdd.Add("polygon", (int)NpgsqlDbType.Polygon);
            sqlSynonymsAdd.Add("real", (int)NpgsqlDbType.Real);
            sqlSynonymsAdd.Add("serial", (int)NpgsqlDbType.Integer);
            sqlSynonymsAdd.Add("serial4", (int)NpgsqlDbType.Integer);
            sqlSynonymsAdd.Add("serial8", (int)NpgsqlDbType.Bigint);
            sqlSynonymsAdd.Add("smallint", (int)NpgsqlDbType.Smallint);
            sqlSynonymsAdd.Add("text", (int)NpgsqlDbType.Text);
            sqlSynonymsAdd.Add("time with time zone", (int)NpgsqlDbType.TimeTz);
            sqlSynonymsAdd.Add("time without time zone", (int)NpgsqlDbType.Time);
            sqlSynonymsAdd.Add("time", (int)NpgsqlDbType.Time);
            sqlSynonymsAdd.Add("timestamp with time zone", (int)NpgsqlDbType.TimestampTz);
            sqlSynonymsAdd.Add("timestamp without time zone", (int)NpgsqlDbType.Timestamp);
            sqlSynonymsAdd.Add("timestamp", (int)NpgsqlDbType.Timestamp);
            sqlSynonymsAdd.Add("TimestampTz", (int)NpgsqlDbType.TimestampTz);
            sqlSynonymsAdd.Add("timetz", (int)NpgsqlDbType.TimeTz);
            sqlSynonymsAdd.Add("uuid", (int)NpgsqlDbType.Uuid);
            sqlSynonymsAdd.Add("varbit", (int)NpgsqlDbType.Bytea); // ??
            sqlSynonymsAdd.Add("varchar", (int)NpgsqlDbType.Varchar);
            sqlSynonymsAdd.Add("xml", (int)NpgsqlDbType.Xml);

            foreach (KeyValuePair<Type, int> kv in typeMapSystemToSQLAdd)
            {
                if (!_typeMapSystemToSQL.ContainsKey(kv.Key))
                    _typeMapSystemToSQL.Add(kv.Key, kv.Value);
            }

            ReplaceType(typeof(DateTime), (int)(useTimeZone ? NpgsqlDbType.TimestampTz :
                NpgsqlDbType.Timestamp), _typeMapSystemToSQL);

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

            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Bigint, "bigint");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Bit, "boolean");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Boolean, "boolean");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Bytea, "bytea");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Char, "character");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Date, "date");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Double, "double precision");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Integer, "integer");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Money, "money");

            _typeMapSQLCodeToSQL.Add("bigint", (int)NpgsqlDbType.Bigint);
            //_typeMapSQLCodeToSQL.Add("boolean", (int)NpgsqlDbType.Bit);
            _typeMapSQLCodeToSQL.Add("boolean", (int)NpgsqlDbType.Boolean);
            _typeMapSQLCodeToSQL.Add("bytea", (int)NpgsqlDbType.Bytea);
            _typeMapSQLCodeToSQL.Add("character", (int)NpgsqlDbType.Char);
            _typeMapSQLCodeToSQL.Add("date", (int)NpgsqlDbType.Date);
            _typeMapSQLCodeToSQL.Add("integer", (int)NpgsqlDbType.Integer);
            _typeMapSQLCodeToSQL.Add("money", (int)NpgsqlDbType.Money);

            if ((numericPrecision > 0) && (numericScale > 0))
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Numeric, String.Format("numeric ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLCodeToSQL.Add(String.Format("numeric ({0},{1})", numericPrecision, numericScale), (int)NpgsqlDbType.Numeric);
            }
            else if (numericPrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Numeric, String.Format("numeric ({0})", numericPrecision));
                _typeMapSQLCodeToSQL.Add(String.Format("numeric ({0})", numericPrecision), (int)NpgsqlDbType.Numeric);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Numeric, "numeric");
                _typeMapSQLCodeToSQL.Add("numeric", (int)NpgsqlDbType.Numeric);
            }
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Real, "real");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Smallint, "smallint");
            _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Text, "text");

            _typeMapSQLCodeToSQL.Add("real", (int)NpgsqlDbType.Real);
            _typeMapSQLCodeToSQL.Add("smallint", (int)NpgsqlDbType.Smallint);
            _typeMapSQLCodeToSQL.Add("text", (int)NpgsqlDbType.Text);

            if (timePrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Time, String.Format("time{0} ({1})", timeZoneSuffix, timePrecision));
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Timestamp, String.Format("timestamp ({0})", timePrecision));
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.TimestampTz, String.Format("TimestampTz ({0})", timePrecision));

                _typeMapSQLCodeToSQL.Add(String.Format("time{0} ({1})", timeZoneSuffix, timePrecision), (int)NpgsqlDbType.Time);
                _typeMapSQLCodeToSQL.Add(String.Format("timestamp ({0})", timePrecision), (int)NpgsqlDbType.Timestamp);
                _typeMapSQLCodeToSQL.Add(String.Format("TimestampTz ({0})", timePrecision), (int)NpgsqlDbType.TimestampTz);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Time, String.Format("time{0}", timeZoneSuffix));
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Timestamp, "timestamp");
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.TimestampTz, "TimestampTz");

                _typeMapSQLCodeToSQL.Add(String.Format("time{0}", timeZoneSuffix), (int)NpgsqlDbType.Time);
                _typeMapSQLCodeToSQL.Add("timestamp", (int)NpgsqlDbType.Timestamp);
                _typeMapSQLCodeToSQL.Add("TimestampTz", (int)NpgsqlDbType.TimestampTz);
            }
            if (textLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Varchar, String.Format("varchar ({0})", textLength));
                _typeMapSQLCodeToSQL.Add(String.Format("varchar ({0})", textLength), (int)NpgsqlDbType.Varchar);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)NpgsqlDbType.Varchar, "varchar");
                _typeMapSQLCodeToSQL.Add("varchar", (int)NpgsqlDbType.Varchar);
            }
        }

        /// <summary>
        /// Sets the client encoding for the PostgreSQL connection based on the value of the
        /// _encoding field.
        /// </summary>
        private void SetPgClientEncoding()
        {
            if (String.IsNullOrEmpty(_encoding)) return;

            ConnectionState previousConnectionState = _connection.State;

            try
            {
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

                _command.CommandText = "SET client_encoding TO " + ("'" + _encoding.Trim() + "'").Replace("''", "'");
                _command.ExecuteNonQuery();
            }
            catch { }
            finally { if (previousConnectionState != ConnectionState.Open) _connection.Close() ; }
        }

        #endregion Private Methods
    }
}