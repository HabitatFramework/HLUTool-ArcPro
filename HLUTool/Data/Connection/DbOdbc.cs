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
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommandType = System.Data.CommandType;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.Data.Connection
{
    /// <summary>
    /// This class provides an implementation of the DbBase class for ODBC connections. It uses the
    /// System.Data.Odbc namespace to manage database connections, commands, and data adapters. The
    /// class supports various ODBC backends, including Access, SQL Server, Oracle, PostgreSQL, and
    /// DB2. It provides methods for executing SQL queries, managing transactions, and filling
    /// DataTables with schema and data. The class also includes error handling and connection
    /// management features to ensure robust database interactions.
    /// </summary>
    class DbOdbc : DbBase
    {
        #region Private Members

        private string _errorMessage;
        private OdbcConnectionStringBuilder _connStrBuilder;
        private OdbcConnection _connection;
        private OdbcCommand _command;
        private OdbcDataAdapter _adapter;
        private OdbcCommandBuilder _commandBuilder;
        private OdbcTransaction _transaction;
        private Dictionary<Type, OdbcDataAdapter> _adaptersDic = [];

        private UI.View.Connection.ViewConnectOdbc _connWindow;
        private UI.ViewModel.ViewModelConnectOdbc _connViewModel;

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
        /// Initializes a new instance of the DbOdbc class with the specified connection string,
        /// default schema, and other parameters. The constructor attempts to establish a connection
        /// to the database using the provided connection string, and if successful, it populates
        /// the type maps and sets up the command and data adapter for executing SQL queries. If the
        /// connection string is invalid or if any errors occur during the connection process, an
        /// exception is thrown with an appropriate error message. The constructor also handles
        /// password prompting and masking if required, and it ensures that the connection is
        /// properly configured for the specified backend database.
        /// </summary>
        /// <param name="connString">The connection string for the database.</param>
        /// <param name="defaultSchema">The default schema for the database.</param>
        /// <param name="promptPwd">Indicates whether to prompt for a password.</param>
        /// <param name="pwdMask">The password mask string.</param>
        /// <param name="useCommandBuilder">Indicates whether to use automatic command builders.</param>
        /// <param name="useColumnNames">Indicates whether to use column names.</param>
        /// <param name="isUnicode">Indicates whether to use Unicode encoding.</param>
        /// <param name="useTimeZone">Indicates whether to use time zone information.</param>
        /// <param name="textLength">The maximum length of text fields.</param>
        /// <param name="binaryLength">The maximum length of binary fields.</param>
        /// <param name="timePrecision">The precision of time fields.</param>
        /// <param name="numericPrecision">The precision of numeric fields.</param>
        /// <param name="numericScale">The scale of numeric fields.</param>
        /// <param name="connectTimeOut">The timeout value for the database connection.</param>
        public DbOdbc(ref string connString, ref string defaultSchema, ref bool promptPwd,
            string pwdMask, bool useCommandBuilder, bool useColumnNames, bool isUnicode,
            bool useTimeZone, uint textLength, uint binaryLength, uint timePrecision,
            uint numericPrecision, uint numericScale, int connectTimeOut)
            : base(ref connString, ref defaultSchema, ref promptPwd, pwdMask,
            useCommandBuilder, useColumnNames, isUnicode, useTimeZone, textLength,
            binaryLength, timePrecision, numericPrecision, numericScale, connectTimeOut)
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

        #region Public Static

        /// <summary>
        /// Determines the backend type of the database based on the ODBC driver name. The method
        /// checks the driver name against known patterns for Access, SQL Server, Oracle,
        /// PostgreSQL, and DB2 ODBC drivers. If the driver name matches one of the known patterns,
        /// the corresponding backend type is returned. If the driver name does not match any known
        /// patterns, the method returns UndeterminedOdbc. The method also handles opening and
        /// closing the connection if necessary to retrieve the driver information. This allows for
        /// dynamic identification of the database backend based on the ODBC connection provided.
        /// </summary>
        /// <param name="cn">The ODBC connection to evaluate.</param>
        /// <returns>The determined backend type.</returns>
        public static Backends GetBackend(OdbcConnection cn)
        {
            ConnectionState previousConnectionState = cn.State;
            if (String.IsNullOrEmpty(cn.Driver) &&
                (previousConnectionState != ConnectionState.Open)) cn.Open();

            string driver = cn.Driver.ToLower();

            if ((cn.State == ConnectionState.Open) &&
                (previousConnectionState != ConnectionState.Open)) cn.Close();

            if (driver.StartsWith("odbcjt32"))
                return Backends.Access;
            else if ((driver.StartsWith("sqlncli")) || (driver.StartsWith("sqlsrv")))
                return Backends.SqlServer;
            else if ((driver.StartsWith("sqora")) || (driver.StartsWith("msorcl")))
                return Backends.Oracle;
            else if (driver.StartsWith("psql"))
                return Backends.PostgreSql;
            else if (driver.StartsWith("db2"))
                return Backends.DB2;
            else
                return Backends.UndeterminedOdbc;
        }

        /// <summary>
        /// Determines the backend type of the database based on the ODBC connection string. The
        /// method attempts to create an ODBC connection using the provided connection string and
        /// then retrieves the driver information to identify the backend type. If the connection
        /// string is null or empty, or if any errors occur while creating the connection or
        /// retrieving the driver information, the method returns UndeterminedOdbc. This allows for
        /// dynamic identification of the database backend based on the ODBC connection string
        /// provided by the user or application configuration.
        /// </summary>
        /// <param name="connStrBuilder">The ODBC connection string builder to evaluate.</param>
        /// <returns>The determined backend type.</returns>
        public static Backends GetBackend(OdbcConnectionStringBuilder connStrBuilder)
        {
            if ((connStrBuilder == null) || String.IsNullOrEmpty(connStrBuilder.ConnectionString))
                return Backends.UndeterminedOdbc;

            try
            {
                OdbcConnection cn = new(connStrBuilder.ConnectionString);
                return GetBackend(cn);
            }
            catch { return Backends.UndeterminedOdbc; }
        }

        /// <summary>
        /// Determines the backend type of the database based on the ODBC connection string. The
        /// method attempts to create an ODBC connection using the provided connection string and
        /// then retrieves the driver information to identify the backend type. If the connection
        /// string is null or empty, or if any errors occur while creating the connection or
        /// retrieving the driver information, the method returns UndeterminedOdbc. This allows for
        /// dynamic identification of the database backend based on the ODBC connection string
        /// provided by the user or application configuration.
        /// </summary>
        /// <param name="connString">The ODBC connection string to evaluate.</param>
        /// <returns>The determined backend type.</returns>
        public static Backends GetBackend(string connString)
        {
            if (String.IsNullOrEmpty(connString)) return Backends.UndeterminedOdbc;

            try
            {
                OdbcConnection cn = new(connString);
                return GetBackend(cn);
            }
            catch { return Backends.UndeterminedOdbc; }
        }

        #endregion Public Static

        #region Public Members

        /// <summary>
        /// Gets the backend type of the database connection. This property returns the backend type
        /// that was determined during the initialization of the DbOdbc instance. The backend type
        /// indicates the specific database system (e.g., Access, SQL Server, Oracle, PostgreSQL,
        /// DB2) that the ODBC connection is configured to interact with. This information can be
        /// used by other methods in the class to tailor SQL queries and commands to the specific
        /// syntax and features of the identified database backend.
        /// </summary>
        /// <value>The backend type of the database connection.</value>
        public override Backends Backend { get { return _backend; } }

        /// <summary>
        /// Determines whether the specified DataSet contains the necessary tables and columns that
        /// match the schema of the database connection. The method retrieves the schema information
        /// for the columns from the database and compares it against the tables and columns defined
        /// in the provided DataSet. If any required tables or columns are missing, or if there are
        /// data type mismatches, the method constructs an error message detailing the
        /// discrepancies. If the DataSet matches the database schema, the method returns true;
        /// otherwise, it returns false and provides an error message indicating the issues found.
        /// This validation is crucial for ensuring that operations performed on the DataSet will be
        /// compatible with the underlying database schema.
        /// </summary>
        /// <param name="ds">The DataSet to validate against the database schema.</param>
        /// <param name="errorMessage">
        /// An output parameter that receives the error message if the DataSet does not match the
        /// database schema.
        /// </param>
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
                                           DataType = r.Field<string>("TYPE_NAME") //<int>("DATA_TYPE")
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
                                                              DbToSystemType(SQLCodeToSQLType(dbCol.DataType)) == dsCol.DataType
                                                              select dbCol
                                                 where !dbCols.Any()
                                                 select QuoteIdentifier(dsCol.ColumnName) + " (" +
                                                 ((OdbcType)SystemToDbType(dsCol.DataType) + ")").ToString())];
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
        /// Gets the ODBC connection associated with this DbOdbc instance. This property returns the OdbcConnection object that was created and initialized during the construction of the DbOdbc instance. The connection object is used for executing SQL commands, managing transactions, and interacting with the database. It is important to note that the connection may not be open at all times, and it should be properly opened and closed as needed when performing database operations. The property provides access to the underlying ODBC connection for advanced scenarios where direct manipulation of the connection may be required.
        /// </summary>
        /// <value>The ODBC connection associated with this DbOdbc instance.</value>
        public override IDbConnection Connection { get { return _connection; } }

        /// <summary>
        /// Gets the connection string builder for the ODBC connection. This property returns the
        /// OdbcConnectionStringBuilder object that was created and initialized during the
        /// construction of the DbOdbc instance. The connection string builder allows for
        /// programmatic manipulation of the connection string, such as setting or retrieving
        /// individual connection parameters (e.g., server name, database name, user ID, password).
        /// It provides a convenient way to manage the connection string without having to parse and
        /// concatenate strings manually. The property gives access to the underlying connection
        /// string builder for scenarios where dynamic modification of the connection string is necessary.
        /// </summary>
        /// <value>The connection string builder for the ODBC connection.</value>
        public override DbConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        /// <summary>
        /// Gets the current transaction associated with the ODBC connection. This property returns
        /// the OdbcTransaction object that represents the current transaction in progress on the
        /// ODBC connection. If there is no active transaction, this property will return null. The
        /// transaction object can be used to commit or roll back the transaction as needed when
        /// performing database operations. It is important to manage transactions properly to
        /// ensure data integrity and consistency when executing multiple related database commands.
        /// The property provides access to the current transaction for advanced scenarios where
        /// explicit transaction control is required.
        /// </summary>
        /// <value>The current transaction associated with the ODBC connection.</value>
        public override IDbTransaction Transaction
        {
            get { return _transaction; }
        }

        /// <summary>
        /// Creates and returns a new ODBC command object associated with the current connection.
        /// This method checks if the ODBC connection is not null and, if so, it creates a new
        /// OdbcCommand object using the CreateCommand method of the OdbcConnection. If the
        /// connection is null, it returns a new instance of OdbcCommand without associating it with
        /// a connection. The returned command object can be used to execute SQL queries and
        /// commands against the database. It is important to ensure that the command is properly
        /// configured with the necessary SQL text, parameters, and transaction context before
        /// executing it.
        /// </summary>
        /// <returns>A new ODBC command object associated with the current connection.</returns>
        public override IDbCommand CreateCommand()
        {
            if (_connection != null)
                return _connection.CreateCommand();
            else
                return new OdbcCommand();
        }

        //TODO: CreateAdapter
        //public override IDbDataAdapter CreateAdapter()
        //{
        //    return new();
        //}

        /// <summary>
        /// Creates and returns an ODBC data adapter for the specified DataTable. The method checks
        /// if the provided DataTable is null and, if so, it initializes a new instance of the
        /// DataTable. It then checks if an adapter for the type of the DataTable already exists in
        /// the _adaptersDic dictionary. If an adapter does not exist, it creates a new
        /// OdbcDataAdapter and configures it with the appropriate commands (SelectCommand,
        /// InsertCommand, UpdateCommand, DeleteCommand) based on the schema of the DataTable. The
        /// method constructs SQL command texts for each operation and adds the necessary parameters
        /// to the commands. If the _useCommandBuilder flag is set, it uses an OdbcCommandBuilder to
        /// automatically generate the commands. Finally, it adds the newly created adapter to the
        /// _adaptersDic dictionary for future reuse and returns the adapter. This method allows for
        /// efficient management of data adapters based on the structure of the DataTable being used.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable for which to create the ODBC data adapter.</typeparam>
        /// <param name="table">The DataTable for which to create the ODBC data adapter.</param>
        /// <returns>An ODBC data adapter configured for the specified DataTable.</returns>
        public override IDbDataAdapter CreateAdapter<T>(T table)
        {
            table ??= new T();

            OdbcDataAdapter adapter;

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

                List<OdbcParameter> deleteParams = [];
                List<OdbcParameter> insertParams = [];
                List<OdbcParameter> updateParams = [];
                List<OdbcParameter> updateParamsOrig = [];

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
                OdbcType isNullType = (OdbcType)isNullTypeInt;

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
                        (OdbcType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    insColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    insertParams.Add(CreateParameter(insColParamName,
                        (OdbcType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    updateParams.Add(CreateParameter(updColParamName,
                        (OdbcType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName, i + _startParamNo + columnCount + nullParamCount);
                    updateParamsOrig.Add(CreateParameter(updOrigParamName,
                        (OdbcType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

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
                    OdbcCommandBuilder cmdBuilder = new(adapter);
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
        /// Creates and returns a new ODBC parameter with the specified properties. This method is a
        /// helper function to simplify the creation of OdbcParameter objects with consistent
        /// settings. It takes parameters such as the name, data type, direction, source column,
        /// source version, and null mapping to configure the OdbcParameter accordingly. The created
        /// parameter can then be added to ODBC commands for use in SQL queries and commands. This
        /// method helps to ensure that parameters are created with the correct properties and
        /// reduces code duplication when setting up command parameters.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The data type of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, InputOutput, or ReturnValue).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The version of the data row to use.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a null value.</param>
        /// <returns>The created OdbcParameter.</returns>
        private OdbcParameter CreateParameter(string name, OdbcType type, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OdbcParameter param = new(name, type)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Creates and returns a new ODBC parameter with the specified properties. This method is a
        /// helper function to simplify the creation of OdbcParameter objects with consistent
        /// settings. It takes parameters such as the name, value, direction, source column, source
        /// version, and null mapping to configure the OdbcParameter accordingly. The created
        /// parameter can then be added to ODBC commands for use in SQL queries and commands. This
        /// method helps to ensure that parameters are created with the correct properties and
        /// reduces code duplication when setting up command parameters.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, InputOutput, or ReturnValue).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The version of the data row to use.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps to a null value.</param>
        /// <returns>The created OdbcParameter.</returns>
        private OdbcParameter CreateParameter(string name, object value, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OdbcParameter param = new(name, value)
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
        /// <param name="prefix">The prefix to use for the parameter name.</param>
        /// <param name="columnName">The name of the column for the parameter.</param>
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
        /// Returns the parameter marker for the ODBC command. In ODBC, the parameter marker is typically a question mark (?).
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The parameter marker for the ODBC command.</returns>
        protected override string ParameterMarker(string parameterName)
        {
            return "?";
        }

        /// <summary>
        /// Fills the schema of the specified table based on the provided SQL query and schema type.
        /// The method executes the SQL query to retrieve the schema information from the database
        /// and populates the structure of the provided table accordingly. It uses an
        /// OdbcDataAdapter to fill the schema of the table, and if a specific adapter for the type
        /// of the table exists, it utilizes that adapter. The method also handles opening and
        /// closing the database connection as needed during the operation. If any errors occur
        /// during the process, it captures the error message and returns false; otherwise, it
        /// returns true upon successful completion of filling the schema. This method is essential
        /// for ensuring that the DataTable structure matches the schema of the database query
        /// results before performing data operations.
        /// </summary>
        /// <typeparam name="T">The type of the table to fill the schema for.</typeparam>
        /// <param name="schemaType">The type of schema to retrieve.</param>
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
                OdbcDataAdapter adapter = UpdateAdapter(table);
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
        /// Fills the specified table with data based on the provided SQL query. The method executes
        /// the SQL query to retrieve data from the database and populates the provided table with
        /// the results. It uses an OdbcDataAdapter to fill the table, and if a specific adapter for
        /// the type of the table exists, it utilizes that adapter. The method also handles opening
        /// and closing the database connection as needed during the operation. If any errors occur
        /// during the process, it captures the error message and returns -1; otherwise, it returns
        /// the number of rows added to the table upon successful completion of filling the data.
        /// This method is essential for retrieving data from the database and populating a
        /// DataTable or similar structure for further processing in the application.
        /// </summary>
        /// <typeparam name="T">The type of the table to fill.</typeparam>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="table">The table to fill with data.</param>
        /// <returns>The number of rows added to the table, or -1 if an error occurs.</returns>
        public override int FillTable<T>(string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return 0;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                _command.CommandText = sql;
                _command.CommandType = CommandType.Text;
                if (_transaction != null) _command.Transaction = _transaction;
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                OdbcDataAdapter adapter = UpdateAdapter(table);
                if (adapter != null)
                {
                    return adapter.Fill(table);
                }
                else
                {
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
        /// Begins a new transaction on the ODBC connection with the specified isolation level. If
        /// there is an existing transaction, the method will either commit or roll back the
        /// previous transaction based on the value of the commitPrevious parameter before starting
        /// a new transaction. The method also refreshes the command builder schema after beginning
        /// the transaction to ensure that any changes in the database schema are reflected in the
        /// command builder. If any errors occur during the process, it captures the error message
        /// and returns false; otherwise, it returns true upon successfully beginning the
        /// transaction. This method is crucial for managing transactions and ensuring data
        /// integrity when performing multiple related database operations.
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
        /// Commits the current transaction on the ODBC connection. If there is an active
        /// transaction, the method will commit it and refresh the command builder schema to reflect
        /// any changes in the database. If any errors occur during the commit process, it captures
        /// the error message and returns false; otherwise, it returns true upon successfully
        /// committing the transaction. This method is essential for finalizing a transaction and
        /// ensuring that all operations performed within the transaction are saved to the database.
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
                return false;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Rolls back the current transaction on the ODBC connection. If there is an active
        /// transaction, the method will roll it back and refresh the command builder schema to
        /// reflect any changes in the database. If any errors occur during the rollback process, it
        /// captures the error message and returns false; otherwise, it returns true upon
        /// successfully rolling back the transaction. This method is crucial for undoing a
        /// transaction and ensuring that any operations performed within the transaction are not
        /// saved to the database in case of errors or other conditions that require a rollback.
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
        /// method takes parameters for the SQL query, command timeout, and command type to
        /// configure the execution of the query. It handles opening the database connection if it
        /// is not already open and ensures that any active transaction is associated with the
        /// command. If any errors occur during the execution of the query, it captures the error
        /// message and returns null; otherwise, it returns an IDataReader with the query results.
        /// This method is essential for executing read operations against the database and
        /// retrieving data in a forward-only, read-only manner using an IDataReader.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of command to execute.</param>
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
        /// Executes the specified SQL query and returns the number of rows affected. The method
        /// takes parameters for the SQL query, command timeout, and command type to configure the
        /// execution of the query. It handles opening the database connection if it is not already
        /// open and ensures that any active transaction is associated with the command. If any
        /// errors occur during the execution of the query, it captures the error message and
        /// returns -1; otherwise, it returns the number of rows affected by the query. This method
        /// is essential for executing non-query operations against the database, such as INSERT,
        /// UPDATE, or DELETE commands.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of command to execute.</param>
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
        /// Executes the query and returns the first column of the first row
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of command to execute.</param>
        /// <returns>The first column of the first row in the result set, or null if an error occurs.</returns>
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
                    _connection.OpenAsync();

                return _command.ExecuteScalarAsync();
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
        /// Executes the query and returns the first column of the first row
        /// </summary>
        /// <param name="sql">The SQL query to execute. </param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of command to execute.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The first column of the first row in the result set, or null if an error occurs.</returns>
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
        /// <param name="commandType">The type of command to execute.</param>
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
        /// Updates the database with the changes made to the specified table. The method takes
        /// parameters for the table to update, as well as optional SQL commands for inserting,
        /// updating, and deleting records. It uses an OdbcDataAdapter to perform the update
        /// operation, and if specific commands are provided, it sets them on the adapter before
        /// executing the update. The method also handles opening and closing the database
        /// connection as needed during the operation. If any errors occur during the update
        /// process, it captures the error message and returns -1; otherwise, it returns the number
        /// of rows affected by the update. This method is essential for saving changes made to a
        /// DataTable or similar structure back to the database using ODBC.
        /// </summary>
        /// <typeparam name="T">The type of the table or dataset to update.</typeparam>
        /// <param name="table">The table containing the changes to update.</param>
        /// <param name="insertCommand">The SQL command for inserting records.</param>
        /// <param name="updateCommand">The SQL command for updating records.</param>
        /// <param name="deleteCommand">The SQL command for deleting records.</param>
        /// <returns>The number of rows affected by the update, or -1 if an error occurs.</returns>
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

                return _adapter.Update(table); ;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                return -1;
            }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the specified table. The method uses an
        /// OdbcDataAdapter to perform the update operation, and it retrieves or creates the
        /// appropriate adapter for the type of the table. The method also handles opening and
        /// closing the database connection as needed during the operation. If any errors occur
        /// during the update process, it captures the error message and returns -1; otherwise, it
        /// returns the number of rows affected by the update. This method is essential for saving
        /// changes made to a DataTable or similar structure back to the database using ODBC,
        /// without requiring explicit SQL commands for inserting, updating, or deleting records.
        /// </summary>
        /// <typeparam name="T">The type of the table or dataset to update.</typeparam>
        /// <param name="table">The table containing the changes to update.</param>
        /// <returns>The number of rows affected by the update, or -1 if an error occurs.</returns>
        public override int Update<T>(T table)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;

            try
            {
                OdbcDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the specified dataset and source table.
        /// The method uses an OdbcDataAdapter to perform the update operation, and it retrieves or
        /// creates the appropriate adapter for the type of the table within the dataset. The method
        /// also handles opening and closing the database connection as needed during the operation.
        /// If any errors occur during the update process, it captures the error message and returns
        /// -1; otherwise, it returns the number of rows affected by the update. This method is
        /// essential for saving changes made to a DataSet or similar structure back to the database
        /// using ODBC, allowing for updates to specific tables within a dataset.
        /// </summary>
        /// <typeparam name="T">The type of the dataset to update.</typeparam>
        /// <param name="dataSet">The dataset containing the table to update.</param>
        /// <param name="sourceTable">The name of the table within the dataset to update.</param>
        /// <returns>The number of rows affected by the update, or -1 if an error occurs.</returns>
        public override int Update<T>(T dataSet, string sourceTable)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((dataSet == null) || String.IsNullOrEmpty(sourceTable) ||
                !dataSet.Tables.Contains(sourceTable)) return 0;

            try
            {
                DataTable table = dataSet.Tables[sourceTable];

                OdbcDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the specified rows. The method uses an
        /// OdbcDataAdapter to perform the update operation, and it retrieves or creates the
        /// appropriate adapter for the type of the table associated with the rows. The method also
        /// handles opening and closing the database connection as needed during the operation. If
        /// any errors occur during the update process, it captures the error message and returns
        /// -1; otherwise, it returns the number of rows affected by the update. This method is
        /// essential for saving changes made to specific rows within a DataTable or similar
        /// structure back to the database using ODBC, allowing for more granular updates based on
        /// row-level changes.
        /// </summary>
        /// <typeparam name="T">The type of the table associated with the rows.</typeparam>
        /// <typeparam name="R">The type of the rows to update.</typeparam>
        /// <param name="rows">The array of rows to update in the database.</param>
        /// <returns>The number of rows affected by the update, or -1 if an error occurs.</returns>
        public override int Update<T, R>(R[] rows)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((rows == null) || (rows.Length == 0)) return 0;

            try
            {
                T table = (T)rows[0].Table;

                OdbcDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(rows);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the specified table. The method retrieves
        /// or creates an OdbcDataAdapter for the type of the table and ensures that any active
        /// transaction is associated with the adapter's commands. It checks if the insert, update,
        /// and delete commands of the adapter are not null and if their transactions are either
        /// null or different from the current transaction, in which case it assigns the current
        /// transaction to those commands. This method is essential for preparing the
        /// OdbcDataAdapter to perform update operations on the specified table while ensuring that
        /// it is properly associated with any active transaction on the ODBC connection.
        /// </summary>
        /// <typeparam name="T">The type of the table associated with the adapter.</typeparam>
        /// <param name="table">The table for which to update the adapter.</param>
        /// <returns>The OdbcDataAdapter associated with the specified table, or null if none is found.</returns>
        private OdbcDataAdapter UpdateAdapter<T>(T table) where T : DataTable, new()
        {
            if (table == null) return null;

            OdbcDataAdapter adapter;
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

        #endregion Public Members

        #region Protected Members

        /// <summary>
        /// Gets the parameter prefix used in ODBC commands. In ODBC, the parameter prefix is
        /// typically a question mark (?), but for named parameters, it can be an at symbol (@).
        /// This property returns the appropriate parameter prefix based on the configuration of the
        /// ODBC connection and how parameters are defined in the SQL commands. It is essential for
        /// constructing SQL queries with parameters correctly when using ODBC to ensure that the
        /// parameters are recognized and processed by the database server.
        /// </summary>
        /// <value>The parameter prefix for ODBC commands.</value>
        protected override string ParameterPrefix
        {
            get { return "@"; }
        }

        /// <summary>
        /// Displays a connection dialog window for the user to input ODBC connection details.
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
                    DisplayName = "ODBC Connection"
                };

                // when ViewModel asks to be closed, close window
                _connViewModel.RequestClose -= ConnViewModel_RequestClose; // Safety: avoid double subscription.
                _connViewModel.RequestClose +=
                    new UI.ViewModel.ViewModelConnectOdbc.RequestCloseEventHandler(ConnViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _connWindow.DataContext = _connViewModel;

                // show window
                _connWindow.ShowDialog();

                if (!String.IsNullOrEmpty(_errorMessage)) throw (new Exception(_errorMessage));
            }
            catch (Exception ex)
            {
                MessageBox.Show("ODBC Server responded with an error:\n\n" + ex.Message, "ODBC Server Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Handler for the RequestClose event of the connection ViewModel.
        /// </summary>
        /// <param name="connString">The connection string provided by the user.</param>
        /// <param name="defaultSchema">The default schema selected by the user.</param>
        /// <param name="errorMsg">Any error message encountered during the connection process.</param>
        void ConnViewModel_RequestClose(string connString, string defaultSchema, string errorMsg)
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
            }
        }

        #endregion Protected Members

        #region Public Members

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
                // Ensure that updates to databases using ODBC connection type
                // include the time when updating DateTime fields.
                string s = valueType == typeof(DateTime) ? ((DateTime)value).ToString("s").Replace("T", " ") : value.ToString();

                switch ((OdbcType)colType)
                {
                    case OdbcType.Char:
                    case OdbcType.NChar:
                    case OdbcType.NText:
                    case OdbcType.NVarChar:
                    case OdbcType.Text:
                    case OdbcType.VarChar:
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case OdbcType.Date:
                    case OdbcType.DateTime:
                    case OdbcType.SmallDateTime:
                    case OdbcType.Time:
                    case OdbcType.Timestamp:
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

        #region Private Methods

        /// <summary>
        /// Populates the type mapping dictionaries for system types to SQL types and vice versa, as
        /// well as SQL type synonyms. The method takes parameters to determine if the connection is
        /// Unicode, whether to use time zone information for date/time types, and the lengths and
        /// precisions for various data types. It then populates the dictionaries with appropriate
        /// mappings based on these parameters. This method is crucial for ensuring that the correct
        /// data types are used when interacting with the database through ODBC, allowing for proper
        /// handling of data conversions and compatibility between .NET types and SQL types.
        /// </summary>
        /// <param name="isUnicode">Indicates whether the connection should use Unicode types.</param>
        /// <param name="useTimeZone">Indicates whether to use time zone information for date/time types.</param>
        /// <param name="textLength">Specifies the length for text data types.</param>
        /// <param name="binaryLength">Specifies the length for binary data types.</param>
        /// <param name="timePrecision">Specifies the precision for time data types.</param>
        /// <param name="numericPrecision">Specifies the precision for numeric data types.</param>
        /// <param name="numericScale">Specifies the scale for numeric data types.</param>
        private void PopulateTypeMaps(bool isUnicode, bool useTimeZone, uint textLength, uint binaryLength,
            uint timePrecision, uint numericPrecision, uint numericScale)
        {
            string sTimeZone = useTimeZone ? " WITH TIME ZONE" : "";

            GetMetaData(typeof(OdbcType), _connection, _transaction);

            Dictionary<Type, int> typeMapSystemToSQLAdd = [];
            typeMapSystemToSQLAdd.Add(typeof(Object), (int)OdbcType.VarBinary);
            typeMapSystemToSQLAdd.Add(typeof(Boolean), (int)OdbcType.Bit);
            typeMapSystemToSQLAdd.Add(typeof(SByte), (int)OdbcType.Int);
            typeMapSystemToSQLAdd.Add(typeof(Byte), (int)OdbcType.TinyInt);
            typeMapSystemToSQLAdd.Add(typeof(Int16), (int)OdbcType.SmallInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt16), (int)OdbcType.SmallInt);
            typeMapSystemToSQLAdd.Add(typeof(Int32), (int)OdbcType.Int);
            typeMapSystemToSQLAdd.Add(typeof(UInt32), (int)OdbcType.Int);
            typeMapSystemToSQLAdd.Add(typeof(Int64), (int)OdbcType.BigInt);
            typeMapSystemToSQLAdd.Add(typeof(UInt64), (int)OdbcType.BigInt);
            typeMapSystemToSQLAdd.Add(typeof(Single), (int)OdbcType.Real);
            typeMapSystemToSQLAdd.Add(typeof(Double), (int)OdbcType.Double);
            typeMapSystemToSQLAdd.Add(typeof(Decimal), (int)OdbcType.Decimal);
            typeMapSystemToSQLAdd.Add(typeof(DateTime), (int)OdbcType.DateTime);
            typeMapSystemToSQLAdd.Add(typeof(TimeSpan), (int)OdbcType.DateTime);
            typeMapSystemToSQLAdd.Add(typeof(Byte[]), (int)OdbcType.VarBinary);
            typeMapSystemToSQLAdd.Add(typeof(Guid), (int)OdbcType.UniqueIdentifier);
            if (isUnicode)
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)OdbcType.NChar);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)OdbcType.NText);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)OdbcType.NText);
            }
            else
            {
                typeMapSystemToSQLAdd.Add(typeof(Char), (int)OdbcType.Char);
                typeMapSystemToSQLAdd.Add(typeof(String), (int)OdbcType.Text);
                typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)OdbcType.Text);
            }

            Dictionary<int, Type> typeMapSQLToSystemAdd = [];
            typeMapSQLToSystemAdd.Add((int)OdbcType.BigInt, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Binary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Bit, typeof(Boolean));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Char, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Date, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Decimal, typeof(System.Decimal));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Double, typeof(Double));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Image, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Int, typeof(Int32));
            typeMapSQLToSystemAdd.Add((int)OdbcType.NChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OdbcType.NText, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Numeric, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OdbcType.NVarChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Real, typeof(Single));
            typeMapSQLToSystemAdd.Add((int)OdbcType.SmallDateTime, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OdbcType.SmallInt, typeof(Int16));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Text, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Time, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OdbcType.Timestamp, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OdbcType.TinyInt, typeof(Byte));
            typeMapSQLToSystemAdd.Add((int)OdbcType.UniqueIdentifier, typeof(Guid));
            typeMapSQLToSystemAdd.Add((int)OdbcType.VarBinary, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OdbcType.VarChar, typeof(String));

            Dictionary<string, int> sqlSynonymsAdd = [];
            sqlSynonymsAdd.Add("bit", (int)OdbcType.Bit);
            sqlSynonymsAdd.Add("character", (int)OdbcType.Char);
            sqlSynonymsAdd.Add("char", (int)OdbcType.Char);
            sqlSynonymsAdd.Add("date", (int)OdbcType.Date);
            sqlSynonymsAdd.Add("decimal", (int)OdbcType.Decimal);
            sqlSynonymsAdd.Add("dec", (int)OdbcType.Decimal);
            sqlSynonymsAdd.Add("float", (int)OdbcType.Double);
            sqlSynonymsAdd.Add("double precision", (int)OdbcType.Double);
            sqlSynonymsAdd.Add("integer", (int)OdbcType.Int);
            sqlSynonymsAdd.Add("int", (int)OdbcType.Int);
            sqlSynonymsAdd.Add("national character", (int)OdbcType.NChar);
            sqlSynonymsAdd.Add("national char", (int)OdbcType.NChar);
            sqlSynonymsAdd.Add("nchar", (int)OdbcType.NChar);
            sqlSynonymsAdd.Add("numeric", (int)OdbcType.Numeric);
            sqlSynonymsAdd.Add("national character varying", (int)OdbcType.NVarChar);
            sqlSynonymsAdd.Add("national char varying", (int)OdbcType.NVarChar);
            sqlSynonymsAdd.Add("nchar varying", (int)OdbcType.NVarChar);
            sqlSynonymsAdd.Add("real", (int)OdbcType.Real);
            sqlSynonymsAdd.Add("smallint", (int)OdbcType.SmallInt);
            sqlSynonymsAdd.Add("time", (int)OdbcType.Time);
            sqlSynonymsAdd.Add("time with time zone", (int)OdbcType.Time);
            sqlSynonymsAdd.Add("timestamp", (int)OdbcType.Timestamp);
            sqlSynonymsAdd.Add("timestamp with time zone", (int)OdbcType.Timestamp);
            sqlSynonymsAdd.Add("bit varying", (int)OdbcType.VarBinary);
            sqlSynonymsAdd.Add("character varying", (int)OdbcType.VarChar);
            sqlSynonymsAdd.Add("char varying", (int)OdbcType.VarChar);
            sqlSynonymsAdd.Add("varchar", (int)OdbcType.VarChar);

            foreach (KeyValuePair<Type, int> kv in typeMapSystemToSQLAdd)
            {
                if (!_typeMapSystemToSQL.ContainsKey(kv.Key))
                    _typeMapSystemToSQL.Add(kv.Key, kv.Value);
            }

            if (isUnicode)
            {
                ReplaceType(typeof(Char), (int)OdbcType.NChar, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)OdbcType.NText, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)OdbcType.NText, _typeMapSystemToSQL);
            }
            else
            {
                ReplaceType(typeof(Char), (int)OdbcType.Char, _typeMapSystemToSQL);
                ReplaceType(typeof(String), (int)OdbcType.Text, _typeMapSystemToSQL);
                ReplaceType(typeof(Char[]), (int)OdbcType.Text, _typeMapSystemToSQL);
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

            _typeMapSQLToSQLCode.Add((int)OdbcType.BigInt, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Bit, "BIT");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Char, "CHARACTER");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Date, "DATE");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Double, "DOUBLE PRECISION");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Int, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OdbcType.NChar, "NATIONAL CHARACTER");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Real, "REAL");
            _typeMapSQLToSQLCode.Add((int)OdbcType.SmallDateTime, "DATE");
            _typeMapSQLToSQLCode.Add((int)OdbcType.SmallInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OdbcType.Timestamp, String.Format("TIMESTAMP ({0})", sTimeZone));
            _typeMapSQLToSQLCode.Add((int)OdbcType.TinyInt, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OdbcType.UniqueIdentifier, "CHARACTER (36)");

            //_typeMapSQLCodeToSQL.Add("INTEGER", (int)OdbcType.BigInt);
            _typeMapSQLCodeToSQL.Add("INTEGER", (int)OdbcType.Int);
            _typeMapSQLCodeToSQL.Add("BIT", (int)OdbcType.Bit);
            _typeMapSQLCodeToSQL.Add("CHARACTER", (int)OdbcType.Char);
            //_typeMapSQLCodeToSQL.Add("CHARACTER (36)", (int)OdbcType.UniqueIdentifier);
            _typeMapSQLCodeToSQL.Add("DATE", (int)OdbcType.Date);
            //_typeMapSQLCodeToSQL.Add("DATE", (int)OdbcType.SmallDateTime);
            _typeMapSQLCodeToSQL.Add("REAL", (int)OdbcType.Real);
            _typeMapSQLCodeToSQL.Add("SMALLINT", (int)OdbcType.SmallInt);
            //_typeMapSQLCodeToSQL.Add("SMALLINT", (int)OdbcType.TinyInt);

            if (binaryLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Binary, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OdbcType.Image, String.Format("BIT VARYING ({0})", binaryLength));
                _typeMapSQLToSQLCode.Add((int)OdbcType.VarBinary, String.Format("BIT VARYING ({0})", binaryLength));

                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OdbcType.Binary);
                //_typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OdbcType.Image);
                _typeMapSQLCodeToSQL.Add(String.Format("BIT VARYING ({0})", binaryLength), (int)OdbcType.VarBinary);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Binary, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OdbcType.Image, "BIT VARYING");
                _typeMapSQLToSQLCode.Add((int)OdbcType.VarBinary, "BIT VARYING");

                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OdbcType.Binary);
                //_typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OdbcType.Image);
                _typeMapSQLCodeToSQL.Add("BIT VARYING", (int)OdbcType.VarBinary);
            }
            if (textLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.NText, String.Format("NATIONAL CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OdbcType.NVarChar, String.Format("NATIONAL CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OdbcType.Text, String.Format("CHARACTER VARYING ({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OdbcType.VarChar, String.Format("CHARACTER VARYING ({0})", textLength));

                //_typeMapSQLCodeToSQL.Add(String.Format("NATIONAL CHARACTER VARYING ({0})", textLength), (int)OdbcType.NText);
                _typeMapSQLCodeToSQL.Add(String.Format("NATIONAL CHARACTER VARYING ({0})", textLength), (int)OdbcType.NVarChar);
                //_typeMapSQLCodeToSQL.Add(String.Format("CHARACTER VARYING ({0})", textLength), (int)OdbcType.Text);
                _typeMapSQLCodeToSQL.Add(String.Format("CHARACTER VARYING ({0})", textLength), (int)OdbcType.VarChar);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.NText, "NATIONAL CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OdbcType.NVarChar, "NATIONAL CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OdbcType.Text, "CHARACTER VARYING");
                _typeMapSQLToSQLCode.Add((int)OdbcType.VarChar, "CHARACTER VARYING");

                //_typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER VARYING", (int)OdbcType.NText);
                _typeMapSQLCodeToSQL.Add("NATIONAL CHARACTER VARYING", (int)OdbcType.NVarChar);
                //_typeMapSQLCodeToSQL.Add("CHARACTER VARYING", (int)OdbcType.Text);
                _typeMapSQLCodeToSQL.Add("CHARACTER VARYING", (int)OdbcType.VarChar);
            }
            if ((numericPrecision > 0) && (numericScale > 0))
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Decimal, String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLToSQLCode.Add((int)OdbcType.Numeric, String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale));

                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0},{1})", numericPrecision, numericScale), (int)OdbcType.Decimal);
                _typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0},{1})", numericPrecision, numericScale), (int)OdbcType.Numeric);
            }
            else if (numericPrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Decimal, String.Format("DECIMAL ({0})", numericPrecision));
                _typeMapSQLToSQLCode.Add((int)OdbcType.Numeric, String.Format("NUMERIC ({0})", numericPrecision));

                _typeMapSQLCodeToSQL.Add(String.Format("DECIMAL ({0})", numericPrecision), (int)OdbcType.Decimal);
                _typeMapSQLCodeToSQL.Add(String.Format("NUMERIC ({0})", numericPrecision), (int)OdbcType.Numeric);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Decimal, "DECIMAL");
                _typeMapSQLToSQLCode.Add((int)OdbcType.Numeric, "NUMERIC");

                _typeMapSQLCodeToSQL.Add("DECIMAL", (int)OdbcType.Decimal);
                _typeMapSQLCodeToSQL.Add("NUMERIC", (int)OdbcType.Numeric);
            }
            if (timePrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Time, String.Format("TIME ({0}){1}", timePrecision, sTimeZone));
                _typeMapSQLCodeToSQL.Add(String.Format("TIME ({0}){1}", timePrecision, sTimeZone), (int)OdbcType.Time);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OdbcType.Time, String.Format("TIME ({0})", sTimeZone));
                _typeMapSQLCodeToSQL.Add(String.Format("TIME ({0})", sTimeZone), (int)OdbcType.Time);
            }
        }

        /// <summary>
        /// Sets the default values for quoting identifiers, string literals, date literals,
        /// wildcard characters, and the concatenate operator based on the determined backend type
        /// of the ODBC connection.
        /// </summary>
        private void SetDefaults()
        {
            _backend = GetBackend(_connection);

            switch (_backend)
            {
                case Backends.Access:
                    _quotePrefix = "[";
                    _quoteSuffix = "]";
                    _stringLiteralDelimiter = "'";
                    _dateLiteralPrefix = "#";
                    _dateLiteralSuffix = "#";
                    _wildcardSingleMatch = "?";
                    _wildcardManyMatch = "*";
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