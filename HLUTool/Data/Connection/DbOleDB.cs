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

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.Data.Connection
{
    /// <summary>
    /// Class for handling database connections using OLE DB providers. This class provides methods
    /// for connecting to a database, executing queries, and managing transactions.
    /// </summary>
    class DbOleDb : DbBase
    {
        #region Private Members

        private string _errorMessage;
        private OleDbConnectionStringBuilder _connStrBuilder;
        private OleDbConnection _connection;
        private OleDbCommand _command;
        private OleDbDataAdapter _adapter;
        private OleDbCommandBuilder _commandBuilder;
        private OleDbTransaction _transaction;
        private Dictionary<Type, OleDbDataAdapter> _adaptersDic = [];

        //TODO: OleDB Connection
        //HLU.UI.View.Connection.ViewConnectOleDb _connWindow;
        //HLU.UI.ViewModel.ViewModelConnectOleDb _connViewModel;

        private Backends _backend;
        private string _quotePrefix;
        private string _quoteSuffix;
        private string _stringLiteralDelimiter;
        private string _dateLiteralPrefix;
        private string _dateLiteralSuffix;
        private string _wildcardSingleMatch;
        private string _wildcardManyMatch;
        private string _concatenateOperator;

        #endregion Private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DbOleDb class with the specified connection string and settings.
        /// </summary>
        /// <param name="connString">The connection string for the OLE DB database.</param>
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
        public DbOleDb(ref string connString, ref string defaultSchema, ref bool promptPwd, string pwdMask,
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

                Login(_backend == Backends.Oracle ? "User ID" : "User name", connectionString,
                    ref promptPwd, ref _connStrBuilder, ref _connection);

                PopulateTypeMaps(IsUnicode, UseTimeZone, TextLength, BinaryLength,
                    TimePrecision, NumericPrecision, NumericScale);
                SetDefaults();

                _command = _connection.CreateCommand();
                _adapter = new(_command);
                _commandBuilder = new(_adapter);

                connString = MaskPassword(_connStrBuilder, pwdMask);
                defaultSchema = DefaultSchema;
            }
            catch { throw; }
        }

        #endregion Constructor

        #region DbBase Members

        /// <summary>
        /// Determines the backend type based on the provider specified in the OleDbConnection.
        /// </summary>
        /// <param name="cn">The OleDbConnection to check.</param>
        /// <returns>The backend type based on the provider.</returns>
        public static Backends GetBackend(OleDbConnection cn)
        {
            ConnectionState previousConnectionState = cn.State;

            if (String.IsNullOrEmpty(cn.Provider) &&
                (previousConnectionState != ConnectionState.Open)) cn.Open();

            string provider = cn.Provider.ToLower();

            if ((cn.State == ConnectionState.Open) &&
                (previousConnectionState != ConnectionState.Open)) cn.Close();

            // Enable connection using Microsoft ACE driver.
            //
            if (provider.StartsWith("microsoft.jet.oledb") ||
                provider.StartsWith("microsoft.ace.oledb.12.0"))
                return Backends.Access;
            else if (provider.StartsWith("sqloledb"))
                return Backends.SqlServer;
            else if (provider.StartsWith("oraoledb"))
                return Backends.Oracle;
            else if (provider.StartsWith("postgresql"))
                return Backends.PostgreSql;
            else if (provider.StartsWith("ibmdadb2"))
                return Backends.DB2;
            else
                return Backends.UndeterminedOleDb;
        }

        /// <summary>
        /// Determines the backend type based on the provider specified in the OleDbConnectionStringBuilder.
        /// </summary>
        /// <param name="connStrBuilder">The OleDbConnectionStringBuilder to check.</param>
        /// <returns>The backend type based on the provider.</returns>
        public static Backends GetBackend(OleDbConnectionStringBuilder connStrBuilder)
        {
            if ((connStrBuilder == null) || String.IsNullOrEmpty(connStrBuilder.ConnectionString))
                return Backends.UndeterminedOdbc;

            try
            {
                OleDbConnection cn = new(connStrBuilder.ConnectionString);
                return GetBackend(cn);
            }
            catch { return Backends.UndeterminedOleDb; }
        }

        /// <summary>
        /// Determines the backend type based on the provider specified in the connection string.
        /// </summary>
        /// <param name="connString">The connection string to check.</param>
        /// <returns>The backend type based on the provider.</returns>
        public static Backends GetBackend(string connString)
        {
            if (String.IsNullOrEmpty(connString)) return Backends.UndeterminedOleDb;

            try
            {
                OleDbConnection cn = new(connString);
                return GetBackend(cn);
            }
            catch { return Backends.UndeterminedOleDb; }
        }

        #endregion DbBase Members

        #region Override Methods

        /// <summary>
        /// Gets the backend type for this connection, which is determined based on the provider specified in the connection string.
        /// </summary>
        /// <value>The backend type for this connection.</value>
        public override Backends Backend { get { return _backend; } }

        /// <summary>
        /// Checks if the provided DataSet contains the necessary tables and columns that match the
        /// schema of the database connected to by this class. It retrieves the schema information
        /// from the database and compares it against the structure of the DataSet. If there are any
        /// discrepancies, it constructs an error message detailing the missing tables or columns.
        /// </summary>
        /// <param name="ds">The DataSet to check against the database schema.</param>
        /// <param name="errorMessage">An error message detailing any discrepancies found.</param>
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
                                           DataType = r.Field<int>("DATA_TYPE")
                                       };

                    if (!dbSchemaCols.Any())
                    {
                        messageText.Append(String.Format("\n\nMissing table: {0}", QuoteIdentifier(t.TableName)));
                    }
                    else
                    {
                        string[] checkColumns = [.. (from dsCol in t.Columns.Cast<DataColumn>()
                                                 let dbCols = from dbCol in dbSchemaCols
                                                              where dbCol.ColumnName == dsCol.ColumnName &&
                                                              DbToSystemType(dbCol.DataType) == dsCol.DataType
                                                              select dbCol
                                                 where !dbCols.Any()
                                                 select QuoteIdentifier(dsCol.ColumnName) + " (" +
                                                 ((OleDbType)SystemToDbType(dsCol.DataType) + ")").ToString())];
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
        /// Gets the current database connection associated with this instance of the DbOleDb class.
        /// This property returns an IDbConnection object that represents the connection to the
        /// database, allowing for operations such as opening, closing, and managing transactions on
        /// the database connection.
        /// </summary>
        /// <value>The current database connection.</value>
        public override IDbConnection Connection { get { return _connection; } }

        /// <summary>
        /// Gets the connection string builder associated with this instance of the DbOleDb class. This property returns a DbConnectionStringBuilder object that allows for constructing and modifying the connection string used to establish a connection to the database. The connection string builder provides a convenient way to set various parameters such as the data source, provider, user credentials, and other connection settings in a structured manner.
        /// </summary>
        /// <value>The connection string builder.</value>
        public override DbConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        /// <summary>
        /// Gets the current database transaction associated with this instance of the DbOleDb
        /// class. This property returns an IDbTransaction object that represents the transaction
        /// context for the database operations performed through this connection. The transaction
        /// object allows for managing and controlling the execution of multiple database commands
        /// as a single unit of work, providing capabilities such as committing or rolling back
        /// changes to maintain data integrity.
        /// </summary>
        /// <value>The current database transaction.</value>
        public override IDbTransaction Transaction
        {
            get { return _transaction; }
        }

        /// <summary>
        /// Creates and returns a new IDbCommand object associated with the current database
        /// connection. This method is used to create a command that can be executed against the
        /// database to perform various operations such as querying data, inserting, updating, or
        /// deleting records. The returned IDbCommand object can be configured with the appropriate
        /// SQL command text, parameters, and other settings before being executed to interact with
        /// the database.
        /// </summary>
        /// <returns>A new IDbCommand object.</returns>
        public override IDbCommand CreateCommand()
        {
            if (_connection != null)
                return _connection.CreateCommand();
            else
                return new OleDbCommand();
        }

        //TODO: CreateAdapter
        //public override IDbDataAdapter CreateAdapter()
        //{
        //    return new OleDbDataAdapter();
        //}

        /// <summary>
        /// Creates and returns an IDbDataAdapter object that is configured to work with the
        /// specified DataTable. This method checks if an adapter for the given type of DataTable
        /// already exists in the internal dictionary. If it does, it returns the existing adapter;
        /// otherwise, it creates a new OleDbDataAdapter, configures it with the appropriate
        /// commands (SELECT, INSERT, UPDATE, DELETE) based on the schema of the provided DataTable,
        /// and adds it to the dictionary for future use. The adapter is set up to handle data
        /// operations for the specified DataTable, allowing for efficient data manipulation and
        /// synchronization with the database.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable for which the adapter is to be created.</typeparam>
        /// <param name="table">The DataTable for which the adapter is to be created.</param>
        /// <returns>An IDbDataAdapter object configured for the specified DataTable.</returns>
        public override IDbDataAdapter CreateAdapter<T>(T table)
        {
            table ??= new T();

            OleDbDataAdapter adapter;

            if (!_adaptersDic.TryGetValue(typeof(T), out adapter))
            {
                adapter = new();

                DataColumn[] pk = table.PrimaryKey;
                if ((pk == null) || (pk.Length == 0)) return null;

                DataTableMapping tableMapping = new()
                {
                    SourceTable = table.TableName,
                    DataSetTable = table.TableName
                };

                List<OleDbParameter> deleteParams = [];
                List<OleDbParameter> insertParams = [];
                List<OleDbParameter> updateParams = [];
                List<OleDbParameter> updateParamsOrig = [];

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
                OleDbType isNullType = (OleDbType)isNullTypeInt;

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

                    if (c.AllowDBNull || ((_backend == Backends.Access) && !pk.Contains(c)))
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
                        (OleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    insColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    insertParams.Add(CreateParameter(insColParamName,
                        (OleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    updateParams.Add(CreateParameter(updColParamName,
                        (OleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName, i + _startParamNo + columnCount + nullParamCount);
                    updateParamsOrig.Add(CreateParameter(updOrigParamName,
                        (OleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

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
                    adapter.DeleteCommand.Parameters.AddRange([.. deleteParams]);

                    adapter.UpdateCommand = new()
                    {
                        Connection = _connection,
                        CommandType = CommandType.Text,
                        CommandText =
                        String.Format("UPDATE {0} SET {1} WHERE {2}", tableName, sbUpdSetList, sbWhereUpd)
                    };
                    adapter.UpdateCommand.Parameters.AddRange([.. updateParams]);

                    adapter.InsertCommand = new()
                    {
                        CommandType = CommandType.Text,
                        Connection = _connection,
                        CommandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2})",
                        tableName, sbTargetList, sbInsValues)
                    };
                    adapter.InsertCommand.Parameters.AddRange([.. insertParams]);
                }
                else
                {
                    OleDbCommandBuilder cmdBuilder = new(adapter);
                    adapter.DeleteCommand = cmdBuilder.GetDeleteCommand(_useColumnNames);
                    adapter.UpdateCommand = cmdBuilder.GetUpdateCommand(_useColumnNames);
                    adapter.InsertCommand = cmdBuilder.GetInsertCommand(_useColumnNames);
                }

                if (_backend != Backends.Access)
                {
                    adapter.UpdateCommand.CommandText += ";\r\n" +
                        String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkUpd);
                    adapter.InsertCommand.CommandText += ";\r\n" +
                        String.Format("SELECT {0} FROM {1} WHERE {2}", sbTargetList, tableName, sbWherePkIns);
                }

                if (typeof(T) != typeof(DataTable))
                    _adaptersDic.Add(typeof(T), adapter);
            }

            return adapter;
        }

        /// <summary>
        /// Creates and returns an OleDbParameter object with the specified properties.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="direction"></param>
        /// <param name="srcColumn"></param>
        /// <param name="srcVersion"></param>
        /// <param name="nullMapping">Indicates whether the parameter allows null values.</param>
        /// <returns>An OleDbParameter object configured with the specified properties.</returns>
        private OleDbParameter CreateParameter(string name, OleDbType type, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OleDbParameter param = new(name, type)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping,

                IsNullable = nullMapping
            };

            return param;
        }

        /// <summary>
        /// Creates and returns an OleDbParameter object with the specified properties, including the value of the parameter.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="direction">The direction of the parameter.</param>
        /// <param name="srcColumn">The source column of the parameter.</param>
        /// <param name="srcVersion">The source version of the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter allows null values.</param>
        /// <returns>An OleDbParameter object configured with the specified properties, including the value of the parameter.</returns>
        private OleDbParameter CreateParameter(string name, object value, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OleDbParameter param = new(name, value)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping,

                IsNullable = nullMapping
            };

            return param;
        }

        /// <summary>
        /// Generates a parameter name based on the specified prefix, column name, and parameter
        /// number. The format of the parameter name is determined by the _useColumnNames flag. If
        /// _useColumnNames is true, the parameter name will be in the format of "ParameterPrefix +
        /// prefix + columnName". If _useColumnNames is false, the parameter name will be in the
        /// format of "ParameterPrefix + 'p' + paramNo". This method is used to create consistent
        /// and unique parameter names for use in SQL commands and queries.
        /// </summary>
        /// <param name="prefix">The prefix to use for the parameter name.</param>
        /// <param name="columnName">The name of the column.</param>
        /// <param name="paramNo">The parameter number.</param>
        /// <returns>A string representing the generated parameter name.</returns>
        protected override string ParameterName(string prefix, string columnName, int paramNo)
        {
            if (_useColumnNames)
                return ParameterPrefix + prefix + columnName;
            else
                return String.Format("{0}p{1}", ParameterPrefix, paramNo);
        }

        /// <summary>
        /// Generates a parameter name based on the specified parameter name. The format of the
        /// parameter name is determined by the _useColumnNames flag. If _useColumnNames is true,
        /// the parameter name will be in the format of "ParameterPrefix + parameterName". If
        /// _useColumnNames is false, the parameter name will be in the format of "ParameterPrefix +
        /// 'p' + parameterName". This method is used to create consistent and unique parameter
        /// names for use in SQL commands and queries.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>A string representing the parameter marker.</returns>
        protected override string ParameterMarker(string parameterName)
        {
            return "?";
        }

        /// <summary>
        /// Fills the schema of the provided table based on the specified SQL query and schema type.
        /// This method executes the SQL query to retrieve the schema information from the database
        /// and populates the structure of the provided table accordingly. The schema type parameter
        /// determines how the schema is filled, such as whether to include primary keys,
        /// constraints, or other metadata. If an adapter for the type of table already exists, it
        /// uses that adapter to fill the schema; otherwise, it creates a new command and adapter to
        /// execute the query and fill the schema. The method returns true if the schema was
        /// successfully filled, or false if an error occurred during the process.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="schemaType">The type of schema to fill.</param>
        /// <param name="sql">The SQL query to retrieve the schema information.</param>
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
                OleDbDataAdapter adapter = UpdateAdapter(table);
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
        /// Fills the provided table with data based on the specified SQL query.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="sql">The SQL query to retrieve the data.</param>
        /// <param name="table">The table to fill with data.</param>
        /// <returns>The number of rows successfully added to the table, or -1 if an error occurred.</returns>
        public override int FillTable<T>(string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return 0;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                OleDbDataAdapter adapter = UpdateAdapter(table);
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
        /// existing transaction, it can either be committed or rolled back based on the value of
        /// the commitPrevious parameter.
        /// </summary>
        /// <param name="commitPrevious">Indicates whether to commit the previous transaction if it exists.</param>
        /// <param name="isolationLevel">The isolation level for the new transaction.</param>
        /// <returns>True if the transaction was successfully started, false otherwise.</returns>
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
        /// Commits the current database transaction.
        /// </summary>
        /// <returns>True if the transaction was successfully committed, false otherwise.</returns>
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
                return false;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Rolls back the current database transaction.
        /// </summary>
        /// <returns>True if the transaction was successfully rolled back, false otherwise.</returns>
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
        /// Executes the specified SQL query and returns an IDataReader object that can be used to
        /// read the results of the query.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>An IDataReader object if the query was successful; otherwise, null.</returns>
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
        /// Executes the specified SQL query and returns the number of rows affected.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>The number of rows affected if the query was successful; otherwise, -1.</returns>
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
        /// Executes the query and returns the first column of the first row.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>The first column of the first row if the query was successful; otherwise, null.</returns>
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
        /// Executes the query and returns the first column of the first row.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The first column of the first row if the query was successful; otherwise, null.</returns>
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
                //TODO: throw ex;
                _errorMessage = ex.Message;
                return false;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided table.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="table">The table containing the changes to update.</param>
        /// <param name="insertCommand">The SQL command for inserting new rows.</param>
        /// <param name="updateCommand">The SQL command for updating existing rows.</param>
        /// <param name="deleteCommand">The SQL command for deleting rows.</param>
        /// <returns>The number of rows affected if the update was successful; otherwise, -1.</returns>
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
        /// Updates the database with the changes made to the provided table.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="table">The table containing the changes to update.</param>
        /// <returns>The number of rows affected if the update was successful; otherwise, -1.</returns>
        public override int Update<T>(T table)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;

            try
            {
                OleDbDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided dataset and source table.
        /// </summary>
        /// <typeparam name="T">The type of the dataset.</typeparam>
        /// <param name="dataSet">The dataset containing the changes to update.</param>
        /// <param name="sourceTable">The name of the source table within the dataset.</param>
        /// <returns>The number of rows affected if the update was successful; otherwise, -1.</returns>
        public override int Update<T>(T dataSet, string sourceTable)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((dataSet == null) || String.IsNullOrEmpty(sourceTable) ||
                !dataSet.Tables.Contains(sourceTable)) return 0;

            try
            {
                DataTable table = dataSet.Tables[sourceTable];

                OleDbDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }

        }

        /// <summary>
        /// Updates the database with the changes made to the provided rows. The type of the rows
        /// should be the same as the type of the table they belong to. The method retrieves the
        /// table from the first row and uses it to get the appropriate data adapter for updating
        /// the database. If the update is successful, it returns the number of rows affected;
        /// otherwise, it returns -1.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <typeparam name="R">The type of the rows.</typeparam>
        /// <param name="rows">The array of rows containing the changes to update.</param>
        /// <returns>The number of rows affected if the update was successful; otherwise, -1.</returns>
        public override int Update<T, R>(R[] rows)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((rows == null) || (rows.Length == 0)) return 0;

            try
            {
                T table = (T)rows[0].Table;

                OleDbDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(rows);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided table. If an adapter for the
        /// type of table does not already exist, it creates a new adapter and stores it in the
        /// _adaptersDic dictionary for future use.
        /// </summary>
        /// <typeparam name="T">The type of the table.</typeparam>
        /// <param name="table">The table containing the changes to update.</param>
        /// <returns>The data adapter if the update was successful; otherwise, null.</returns>
        private OleDbDataAdapter UpdateAdapter<T>(T table) where T : DataTable, new()
        {
            if (table == null) return null;

            OleDbDataAdapter adapter;
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

        #region Protected Members

        /// <summary>
        /// Gets the parameter prefix used in SQL commands. For OleDb, the parameter prefix is an
        /// empty string because OleDb uses positional parameters denoted by "?" instead of named
        /// parameters. This property is overridden to return an empty string to ensure that the
        /// correct parameter syntax is used when constructing SQL commands for OleDb databases.
        /// </summary>
        /// <value>An empty string, indicating that OleDb uses positional parameters.</value>
        protected override string ParameterPrefix
        {
            get { return String.Empty; }
        }

        /// <summary>
        /// Displays a connection browsing interface to the user, allowing them to select and
        /// configure an OleDb connection. This method is responsible for creating and showing the
        /// connection window, as well as handling the user's input and updating the connection
        /// string and default schema based on their selections. If any errors occur during this
        /// process, an error message is displayed to the user, and the exception is rethrown to be
        /// handled by the calling code.
        /// </summary>
        protected override void BrowseConnection()
        {
            try
            {
                //TODO: OleDB Connection
                //_connWindow = new()
                //{
                //    //TODO: App.GetActiveWindow
                //    //if ((_connWindow.Owner = App.GetActiveWindow()) == null)
                //    //    throw (new Exception("No parent window loaded"));

                //    WindowStartupLocation = WindowStartupLocation.CenterOwner
                //};

                //// create ViewModel to which main window binds
                //_connViewModel = new()
                //{
                //    DisplayName = "OleDb Connection"
                //};

                //// when ViewModel asks to be closed, close window
                //_connViewModel.RequestClose +=
                //    new UI.ViewModel.ViewModelConnectOleDb.RequestCloseEventHandler(_connViewModel_RequestClose);

                //// allow all controls in window to bind to ViewModel by setting DataContext
                //_connWindow.DataContext = _connViewModel;

                //_connWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                //_connWindow.Topmost = true;

                //// show window
                //_connWindow.ShowDialog();

                //// throw error if connection failed
                //if (!String.IsNullOrEmpty(_errorMessage)) throw (new Exception(_errorMessage));
            }
            catch (Exception ex)
            {
                MessageBox.Show("OleDb Server responded with an error:\n\n" + ex.Message,
                     "OleDb Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Handles the RequestClose event from the connection ViewModel.
        /// </summary>
        /// <param name="connString">The connection string provided by the user.</param>
        /// <param name="defaultSchema">The default schema selected by the user.</param>
        /// <param name="errorMsg">Any error message returned during the connection process.</param>
        protected void ConnViewModel_RequestClose(string connString, string defaultSchema, string errorMsg)
        {
            //TODO: OleDB Connection
            //_connViewModel.RequestClose -= _connViewModel_RequestClose;
            //_connWindow.Close();

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

        #endregion Protected Members

        #region Overrides

        public override string QuotePrefix { get { return _quotePrefix; } }

        public override string QuoteSuffix { get { return _quoteSuffix; } }

        public override string StringLiteralDelimiter { get { return _stringLiteralDelimiter; } }

        public override string DateLiteralPrefix { get { return _dateLiteralPrefix; } }

        public override string DateLiteralSuffix { get { return _dateLiteralSuffix; } }

        public override string WildcardSingleMatch { get { return _wildcardSingleMatch; } }

        public override string WildcardManyMatch { get { return _wildcardManyMatch; } }

        public override string ConcatenateOperator { get { return _concatenateOperator; } }

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
                // Fractions of a second can cause rounding differences when
                // comparing DateTime fields later in some databases so use
                // DateTime strings not numbers containing fractions.
                string s = valueType == typeof(DateTime) ? ((DateTime)value).ToString("s").Replace("T", " ") : value.ToString();

                switch ((OleDbType)colType)
                {
                    case OleDbType.BSTR:
                    case OleDbType.Char:
                    case OleDbType.LongVarChar:
                    case OleDbType.LongVarWChar:
                    case OleDbType.VarChar:
                    case OleDbType.VarWChar:
                    case OleDbType.WChar:
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case OleDbType.Date:
                    case OleDbType.DBDate:
                    case OleDbType.DBTime:
                    case OleDbType.DBTimeStamp:
                    case OleDbType.Filetime:
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

        #endregion Overrides

        #region Private Methods

        /// <summary>
        /// Populates the type mapping dictionaries with the appropriate mappings between .NET types
        /// and OleDb types. The method takes several parameters that determine how the mappings
        /// should be populated, such as whether to use Unicode types, whether to include time zone
        /// information for date/time types, and the lengths and precisions for various data types.
        /// The method retrieves metadata about the OleDb types and then fills the
        /// _typeMapSystemToSQL and _typeMapSQLToSystem dictionaries with the appropriate mappings
        /// based on the provided parameters and the capabilities of the OleDb provider. It also
        /// populates a dictionary of SQL type synonyms for easier type recognition when working
        /// with SQL queries and commands.
        /// </summary>
        /// <param name="isUnicode">Indicates whether to use Unicode types.</param>
        /// <param name="useTimeZone">Indicates whether to include time zone information for date/time types.</param>
        /// <param name="textLength">Specifies the length for text types.</param>
        /// <param name="binaryLength">Specifies the length for binary types.</param>
        /// <param name="timePrecision">Specifies the precision for time types.</param>
        /// <param name="numericPrecision">Specifies the precision for numeric types.</param>
        /// <param name="numericScale">Specifies the scale for numeric types.</param>
        private void PopulateTypeMaps(bool isUnicode, bool useTimeZone, uint textLength,
            uint binaryLength, uint timePrecision, uint numericPrecision, uint numericScale)
        {
            string sTimeZone = useTimeZone ? " WITH TIME ZONE" : String.Empty;

            GetMetaData(typeof(OleDbType), _connection, _transaction);

            Dictionary<Type, int> typeMapSystemToSQLAdd = [];
            typeMapSystemToSQLAdd.Add(typeof(Object), (int)OleDbType.Variant);
            typeMapSystemToSQLAdd.Add(typeof(Boolean), (int)OleDbType.Boolean);
            typeMapSystemToSQLAdd.Add(typeof(SByte), (int)OleDbType.Integer);
            typeMapSystemToSQLAdd.Add(typeof(Byte), (int)OleDbType.TinyInt);
            typeMapSystemToSQLAdd.Add(typeof(Int16), (int)OleDbType.SmallInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt16), (int)OleDbType.UnsignedSmallInt);
            typeMapSystemToSQLAdd.Add(typeof(Int32), (int)OleDbType.Integer);
            typeMapSystemToSQLAdd.Add(typeof(UInt32), (int)OleDbType.UnsignedInt);
            typeMapSystemToSQLAdd.Add(typeof(Int64), (int)OleDbType.BigInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt64), (int)OleDbType.UnsignedBigInt);
            typeMapSystemToSQLAdd.Add(typeof(Single), (int)OleDbType.Single);
            typeMapSystemToSQLAdd.Add(typeof(Double), (int)OleDbType.Double);
            typeMapSystemToSQLAdd.Add(typeof(Decimal), (int)OleDbType.Decimal);
            typeMapSystemToSQLAdd.Add(typeof(DateTime), (int)OleDbType.Date);
            typeMapSystemToSQLAdd.Add(typeof(TimeSpan), (int)OleDbType.DBTime);
            typeMapSystemToSQLAdd.Add(typeof(Byte[]), (int)OleDbType.Binary);
            typeMapSystemToSQLAdd.Add(typeof(Guid), (int)OleDbType.Guid);
            if (isUnicode)
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)OleDbType.WChar);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)OleDbType.VarWChar);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)OleDbType.VarWChar);
            }
            else
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)OleDbType.Char);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)OleDbType.VarChar);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)OleDbType.VarChar);
            }

            Dictionary<int, Type> typeMapSQLToSystemAdd = [];
            typeMapSQLToSystemAdd.Add((int)OleDbType.BigInt, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Binary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Boolean, typeof(Boolean));
            typeMapSQLToSystemAdd.Add((int)OleDbType.BSTR, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Char, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Currency, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Date, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OleDbType.DBDate, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OleDbType.DBTime, typeof(TimeSpan));
            typeMapSQLToSystemAdd.Add((int)OleDbType.DBTimeStamp, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Decimal, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Double, typeof(Double));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Error, typeof(Exception));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Filetime, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Guid, typeof(Guid));
            typeMapSQLToSystemAdd.Add((int)OleDbType.IDispatch, typeof(Object));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Integer, typeof(Int32));
            typeMapSQLToSystemAdd.Add((int)OleDbType.IUnknown, typeof(Object));
            typeMapSQLToSystemAdd.Add((int)OleDbType.LongVarBinary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OleDbType.LongVarChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.LongVarWChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Numeric, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OleDbType.PropVariant, typeof(Object));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Single, typeof(Single));
            typeMapSQLToSystemAdd.Add((int)OleDbType.SmallInt, typeof(Int16));
            typeMapSQLToSystemAdd.Add((int)OleDbType.TinyInt, typeof(SByte));
            typeMapSQLToSystemAdd.Add((int)OleDbType.UnsignedBigInt, typeof(UInt64));
            typeMapSQLToSystemAdd.Add((int)OleDbType.UnsignedInt, typeof(UInt32));
            typeMapSQLToSystemAdd.Add((int)OleDbType.UnsignedSmallInt, typeof(UInt16));
            typeMapSQLToSystemAdd.Add((int)OleDbType.UnsignedTinyInt, typeof(Byte));
            typeMapSQLToSystemAdd.Add((int)OleDbType.VarBinary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OleDbType.VarChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.Variant, typeof(Object));
            typeMapSQLToSystemAdd.Add((int)OleDbType.VarNumeric, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OleDbType.VarWChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OleDbType.WChar, typeof(String));

            Dictionary<string, int> sqlSynonymsAdd = [];
            sqlSynonymsAdd.Add("bit", (int)OleDbType.Boolean);
            sqlSynonymsAdd.Add("character", (int)OleDbType.Char);
            sqlSynonymsAdd.Add("char", (int)OleDbType.Char);
            sqlSynonymsAdd.Add("date", (int)OleDbType.DBDate);
            sqlSynonymsAdd.Add("time", (int)OleDbType.DBTime);
            sqlSynonymsAdd.Add("time with time zone", (int)OleDbType.DBTime);
            sqlSynonymsAdd.Add("timestamp", (int)OleDbType.DBTimeStamp);
            sqlSynonymsAdd.Add("timestamp with time zone", (int)OleDbType.DBTimeStamp);
            sqlSynonymsAdd.Add("decimal", (int)OleDbType.Decimal);
            sqlSynonymsAdd.Add("dec", (int)OleDbType.Decimal);
            sqlSynonymsAdd.Add("double precision", (int)OleDbType.Double);
            sqlSynonymsAdd.Add("character (36)", (int)OleDbType.Guid);
            sqlSynonymsAdd.Add("integer", (int)OleDbType.Integer);
            sqlSynonymsAdd.Add("int", (int)OleDbType.Integer);
            sqlSynonymsAdd.Add("numeric", (int)OleDbType.Numeric);
            sqlSynonymsAdd.Add("float", (int)OleDbType.Single);
            sqlSynonymsAdd.Add("real", (int)OleDbType.Single);
            sqlSynonymsAdd.Add("smallint", (int)OleDbType.SmallInt);
            sqlSynonymsAdd.Add("character varying", (int)OleDbType.VarChar);
            sqlSynonymsAdd.Add("char varying", (int)OleDbType.VarChar);
            sqlSynonymsAdd.Add("varchar", (int)OleDbType.VarChar);
            sqlSynonymsAdd.Add("bit varying", (int)OleDbType.Variant);
            sqlSynonymsAdd.Add("national character varying", (int)OleDbType.VarWChar);
            sqlSynonymsAdd.Add("national char varying", (int)OleDbType.VarWChar);
            sqlSynonymsAdd.Add("nchar varying", (int)OleDbType.VarWChar);
            sqlSynonymsAdd.Add("national character", (int)OleDbType.WChar);
            sqlSynonymsAdd.Add("national char", (int)OleDbType.WChar);
            sqlSynonymsAdd.Add("nchar", (int)OleDbType.WChar);

            foreach (KeyValuePair<Type, int> kv in typeMapSystemToSQLAdd)
            {
                if (!_typeMapSystemToSQL.ContainsKey(kv.Key))
                    _typeMapSystemToSQL.Add(kv.Key, kv.Value);
            }

            if (isUnicode)
            {
                ReplaceType(typeof(Char), (int)OleDbType.WChar, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)OleDbType.VarWChar, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)OleDbType.VarWChar, _typeMapSystemToSQL);
            }
            else
            {
                ReplaceType(typeof(Char), (int)OleDbType.Char, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)OleDbType.VarChar, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)OleDbType.VarChar, _typeMapSystemToSQL);
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

            _typeMapSQLToSQLCode.Add((int)OleDbType.BigInt, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Boolean, "BIT");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Char, "CHARACTER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Date, "DATE");
            _typeMapSQLToSQLCode.Add((int)OleDbType.DBDate, "DATE");
            _typeMapSQLToSQLCode.Add((int)OleDbType.DBTimeStamp, "TIMESTAMP");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Double, "DOUBLE PRECISION");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Filetime, "DATE");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Guid, "CHARACTER (36)");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Integer, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.Single, "REAL");
            _typeMapSQLToSQLCode.Add((int)OleDbType.SmallInt, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.TinyInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OleDbType.UnsignedBigInt, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.UnsignedInt, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OleDbType.UnsignedSmallInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OleDbType.UnsignedTinyInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OleDbType.WChar, "NATIONAL CHARACTER");

            //_typeMapSQLCodeToSQL.Add("INTEGER", (int)OleDbType.BigInt);
            _typeMapSQLCodeToSQL.Add("BIT", (int)OleDbType.Boolean);
            _typeMapSQLCodeToSQL.Add("CHARACTER", (int)OleDbType.Char);
            _typeMapSQLCodeToSQL.Add("DATE", (int)OleDbType.Date);
            //_typeMapSQLCodeToSQL.Add("DATE", (int)OleDbType.DBDate);
            _typeMapSQLCodeToSQL.Add("TIMESTAMP", (int)OleDbType.DBTimeStamp);
            _typeMapSQLCodeToSQL.Add("DOUBLE PRECISION", (int)OleDbType.Double);
            //_typeMapSQLCodeToSQL.Add("DATE", (int)OleDbType.Filetime);
            _typeMapSQLCodeToSQL.Add("CHARACTER (36)", (int)OleDbType.Guid);
            _typeMapSQLCodeToSQL.Add("INTEGER", (int)OleDbType.Integer);
            _typeMapSQLCodeToSQL.Add("REAL", (int)OleDbType.Single);
            //_typeMapSQLCodeToSQL.Add("INTEGER", (int)OleDbType.SmallInt);
            _typeMapSQLCodeToSQL.Add("SMALLINT", (int)OleDbType.TinyInt);
            //_typeMapSQLCodeToSQL.Add("INTEGER", (int)OleDbType.UnsignedBigInt);
            //_typeMapSQLCodeToSQL.Add("INTEGER", (int)OleDbType.UnsignedInt);
            //_typeMapSQLCodeToSQL.Add("SMALLINT", (int)OleDbType.UnsignedSmallInt);
            //_typeMapSQLCodeToSQL.Add("SMALLINT", (int)OleDbType.UnsignedTinyInt);
            _typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER", (int)OleDbType.WChar);

            if (textLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.BSTR, String.Format("NATIONAL CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarChar, String.Format("CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarWChar, String.Format("NATIONAL CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarChar, String.Format("CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarWChar, String.Format("NATIONAL CHARACTER VARYING ({0})", textLength));

                //_typeMapSQLCodeToSQL.Add(String.Format("NATIONAL CHARACTER VARYING ({0})", textLength), (int)OleDbType.BSTR);
                //_typeMapSQLCodeToSQL.Add(String.Format("CHARACTER VARYING ({0})", textLength), (int)OleDbType.LongVarChar);
                //_typeMapSQLCodeToSQL.Add(String.Format("NATIONAL CHARACTER VARYING ({0})", textLength), (int)OleDbType.LongVarWChar);
                _typeMapSQLCodeToSQL.Add(String.Format("CHARACTER VARYING ({0})", textLength), (int)OleDbType.VarChar);
                _typeMapSQLCodeToSQL.Add(String.Format("NATIONAL CHARACTER VARYING ({0})", textLength), (int)OleDbType.VarWChar);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.BSTR, "NATIONAL CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarChar, "CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarWChar, "NATIONAL CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarChar, "CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarWChar, "NATIONAL CHARACTER VARYING");

                //_typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER VARYING", (int)OleDbType.BSTR);
                //_typeMapSQLCodeToSQL.Add("CHARACTER VARYING", (int)OleDbType.LongVarChar);
                //_typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER VARYING", (int)OleDbType.LongVarWChar);
                _typeMapSQLCodeToSQL.Add("CHARACTER VARYING", (int)OleDbType.VarChar);
                _typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER VARYING", (int)OleDbType.VarWChar);
            }
            if ((numericPrecision > 0) && (numericScale > 0))
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.Currency, String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale));
                //_typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale), (int)OleDbType.Currency);
                _typeMapSQLToSQLCode.Add((int)OleDbType.Decimal, String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale), (int)OleDbType.Decimal);
                _typeMapSQLToSQLCode.Add((int)OleDbType.Numeric, String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale), (int)OleDbType.Numeric);
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarNumeric, String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale));
                //_typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale), (int)OleDbType.VarNumeric);
            }
            else if (numericPrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.Currency, String.Format("DECIMAL ({0})", numericPrecision));
                _typeMapSQLToSQLCode.Add((int)OleDbType.Decimal, String.Format("DECIMAL ({0})", numericPrecision));
                _typeMapSQLToSQLCode.Add((int)OleDbType.Numeric, String.Format("NUMERIC ({0})", numericPrecision));
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarNumeric, String.Format("NUMERIC ({0})", numericPrecision));

                //_typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0})", numericPrecision), (int)OleDbType.Currency);
                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0})", numericPrecision), (int)OleDbType.Decimal);
                _typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0})", numericPrecision), (int)OleDbType.Numeric);
                //_typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0})", numericPrecision), (int)OleDbType.VarNumeric);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.Currency, "DECIMAL");
                _typeMapSQLToSQLCode.Add((int)OleDbType.Decimal, "DECIMAL");
                _typeMapSQLToSQLCode.Add((int)OleDbType.Numeric, "NUMERIC");
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarNumeric, "NUMERIC");

                //_typeMapSQLCodeToSQL.Add("DECIMAL", (int)OleDbType.Currency);
                _typeMapSQLCodeToSQL.Add("DECIMAL", (int)OleDbType.Decimal);
                _typeMapSQLCodeToSQL.Add("NUMERIC", (int)OleDbType.Numeric);
                //_typeMapSQLCodeToSQL.Add("NUMERIC", (int)OleDbType.VarNumeric);
            }
            if (timePrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.DBTime, String.Format("TIME ({0}){1}", timePrecision, sTimeZone));
                _typeMapSQLCodeToSQL.Add(String.Format("TIME ({0}){1}", timePrecision, sTimeZone), (int)OleDbType.DBTime);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.DBTime, String.Format("TIME{0}", sTimeZone));
                _typeMapSQLCodeToSQL.Add(String.Format("TIME{0}", sTimeZone), (int)OleDbType.DBTime);
            }
            if (binaryLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.Binary, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.IDispatch, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarBinary, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.PropVariant, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarBinary, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OleDbType.Variant, String.Format("BIT VARYING ({0})", binaryLength));

                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.Binary);
                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.IDispatch);
                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.LongVarBinary);
                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.PropVariant);
                _typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.VarBinary);
                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OleDbType.Variant);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OleDbType.Binary, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.IDispatch, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.LongVarBinary, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.PropVariant, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.VarBinary, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OleDbType.Variant, "BIT VARYING");

                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.Binary);
                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.IDispatch);
                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.LongVarBinary);
                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.PropVariant);
                _typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.VarBinary);
                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OleDbType.Variant);
            }
        }

        /// <summary>
        /// Sets the default values for various properties based on the detected backend type.
        /// </summary>
        private void SetDefaults()
        {
            _backend = GetBackend(_connStrBuilder);

            switch (_backend)
            {
                case Backends.Access:
                    _quotePrefix = "[";
                    _quoteSuffix = "]";
                    _stringLiteralDelimiter = "\"";
                    _dateLiteralPrefix = "#";
                    _dateLiteralSuffix = "#";
                    _wildcardSingleMatch = "_";
                    _wildcardManyMatch = "%";
                    _concatenateOperator = "&";

                    break;
                case Backends.SqlServer:
                    _quotePrefix = "[";
                    _quoteSuffix = "]";
                    _stringLiteralDelimiter = "'";
                    _dateLiteralPrefix = "'";
                    _dateLiteralSuffix = "'";
                    _wildcardSingleMatch = "_";
                    _wildcardManyMatch = "%";
                    _concatenateOperator = "+";
                    break;
                default:
                    _quotePrefix = "\"";
                    _quoteSuffix = "\"";
                    _stringLiteralDelimiter = "'";
                    _dateLiteralPrefix = "'";
                    _dateLiteralSuffix = "'";
                    _wildcardSingleMatch = "_";
                    _wildcardManyMatch = "%";
                    _concatenateOperator = "&";
                    break;
            }
        }

        #endregion Private Methods
    }
}