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
using Oracle.ManagedDataAccess.Client;
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
    class DbOracle : DbBase
    {
        #region Private Members

        private string _errorMessage;
        private OracleConnectionStringBuilder _connStrBuilder;
        private OracleConnection _connection;
        private OracleCommand _command;
        private OracleDataAdapter _adapter;
        private OracleCommandBuilder _commandBuilder;
        private OracleTransaction _transaction;
        private Dictionary<Type, OracleDataAdapter> _adaptersDic = [];

        private UI.View.Connection.ViewConnectOracle _connWindow;
        private UI.ViewModel.ViewModelConnectOracle _connViewModel;

        #endregion Private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the DbOracle class with the specified connection string,
        /// default schema, and other parameters.
        /// </summary>
        /// <param name="connString"></param>
        /// <param name="defaultSchema"></param>
        /// <param name="promptPwd"></param>
        /// <param name="pwdMask"></param>
        /// <param name="useCommandBuilder"></param>
        /// <param name="useColumnNames"></param>
        /// <param name="isUnicode"></param>
        /// <param name="useTimeZone"></param>
        /// <param name="textLength"></param>
        /// <param name="binaryLength"></param>
        /// <param name="timePrecision"></param>
        /// <param name="numericPrecision"></param>
        /// <param name="numericScale"></param>
        /// <param name="connectTimeOut"></param>
        public DbOracle(ref string connString, ref string defaultSchema, ref bool promptPwd,
            string pwdMask, bool useCommandBuilder, bool useColumnNames, bool isUnicode,
            bool useTimeZone, uint textLength, uint binaryLength, uint timePrecision,
            uint numericPrecision, uint numericScale, int connectTimeOut)
            : base(ref connString, ref defaultSchema, ref promptPwd, pwdMask, useCommandBuilder,
            useColumnNames, isUnicode, useTimeZone, textLength, binaryLength, timePrecision,
            numericPrecision, numericScale, connectTimeOut)
        {
            if (String.IsNullOrEmpty(ConnectionString)) throw (new Exception("No connection string"));

            try
            {
                // Append connection timeout to connection string.
                string connectionString = String.Format("{0};{1}", ConnectionString, connectTimeOut);

                Login("User ID", connectionString, ref promptPwd, ref _connStrBuilder, ref _connection);

                PopulateTypeMaps(IsUnicode, UseTimeZone, TextLength, BinaryLength,
                    TimePrecision, NumericPrecision, NumericScale);

                _command = _connection.CreateCommand();
                _adapter = new(_command);
                _commandBuilder = new(_adapter);

                connString = MaskPassword(_connStrBuilder, pwdMask);
                defaultSchema = DefaultSchema;

                _startParamNo = 0;
                _parameterPrefixCurr = "cur_";
                _parameterPrefixNull = "ind_";
                _parameterPrefixOrig = "ori_";
            }
            catch { throw; }
        }

        #endregion Constructor

        #region Public Static

        /// <summary>
        /// Returns a dictionary of connection strings for each data source in the provided DataTable.
        /// </summary>
        /// <param name="dataSources">The DataTable containing the data sources.</param>
        /// <returns>A dictionary of connection strings keyed by instance name.</returns>
        public static Dictionary<string, string> GetConnectionStrings(DataTable dataSources)
        {
            return (from r in dataSources.AsEnumerable()
                    select BuildConnectionString(r.Field<string>("InstanceName"),
                    r.Field<string>("Protocol"), r.Field<string>("ServerName"),
                    r.Field<string>("Port"), r.Field<string>("ServiceName"))
                    ).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        /// <summary>
        /// Builds a connection string for an Oracle database using the provided parameters.
        /// </summary>
        /// <param name="instanceName">The name of the Oracle instance.</param>
        /// <param name="protocol">The protocol to use for the connection.</param>
        /// <param name="serverName">The name of the server hosting the Oracle database.</param>
        /// <param name="port">The port number for the Oracle database connection.</param>
        /// <param name="serviceName">The service name of the Oracle database.</param>
        /// <returns>A key-value pair containing the instance name and the corresponding connection string.</returns>
        public static KeyValuePair<string, string> BuildConnectionString(string instanceName, string protocol,
            string serverName, string port, string serviceName)
        {
            return new KeyValuePair<string, string>(instanceName,
                String.Format("(DESCRIPTION = (ADDRESS_LIST = (ADDRESS = (PROTOCOL = {0})" +
                "(HOST = {1})(PORT = {2}))) (CONNECT_DATA = (SERVICE_NAME = {3})))", protocol,
                serverName, port, serviceName));
        }

        /// <summary>
        /// Extracts and returns the user ID from the provided user ID string. If the string is
        /// enclosed in double quotes, the quotes are removed. Otherwise, the string is converted to uppercase.
        /// </summary>
        /// <param name="userIDstring">The user ID string to process.</param>
        /// <returns>The processed user ID string.</returns>
        public static string GetUserId(string userIDstring)
        {
            if (!String.IsNullOrEmpty(userIDstring))
            {
                if (userIDstring.StartsWith('\"') && userIDstring.EndsWith('\"'))
                    userIDstring = userIDstring.Remove(userIDstring.Length - 1, 1).Remove(0, 1);
                else
                    userIDstring = userIDstring.ToUpper();
            }
            return userIDstring;
        }

        /// <summary>
        /// Extracts and returns the user ID from the provided DbConnectionStringBuilder. It looks
        /// for the "USER ID" key in the connection string builder and processes its value using the
        /// GetUserId(string) method. If the "USER ID" key is not found, an empty string is returned.
        /// </summary>
        /// <param name="connStrBuilder">The DbConnectionStringBuilder containing the connection string.</param>
        /// <returns>The processed user ID string.</returns>
        public static string GetUserId(DbConnectionStringBuilder connStrBuilder)
        {
            if (connStrBuilder == null) return String.Empty;
            object userID;
            if (connStrBuilder.TryGetValue("USER ID", out userID))
                return GetUserId(userID.ToString());
            else
                return String.Empty;
        }

        #endregion Public Static

        #region Override Members

        /// <summary>
        /// Gets the backend type for this database connection, which is Oracle in this case.
        /// </summary>
        /// <value>The backend type for this database connection.</value>
        public override Backends Backend { get { return Backends.Oracle; } }

        /// <summary>
        /// Checks if the provided DataSet contains the necessary schema objects (tables and
        /// columns) to be considered a valid HLU database. It retrieves the schema information from
        /// the database and compares it against the tables and columns in the provided DataSet. If
        /// any required tables or columns are missing, it constructs an error message detailing the
        /// missing schema objects and returns false. If all required schema objects are present, it
        /// returns true.
        /// </summary>
        /// <param name="ds">The DataSet to check for required schema objects.</param>
        /// <param name="errorMessage">The error message detailing any missing schema objects.</param>
        /// <returns>True if all required schema objects are present; otherwise, false.</returns>
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
                                       let numScale = r["SCALE"]
                                       where tableName == t.TableName
                                       select new
                                       {
                                           TableName = tableName,
                                           ColumnName = r.Field<string>("COLUMN_NAME"),
                                           ColumnLength = r.Field<decimal>("LENGTHINCHARS"),
                                           NumericScale = numScale != DBNull.Value ? r.Field<decimal>("SCALE") : 0,
                                           DataType = r.Field<string>("DATATYPE")
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
                                                              TypeMatch(SystemDataType(dbCol.DataType), dbCol.ColumnLength,
                                                                        dbCol.NumericScale, dsCol.DataType, dsCol.MaxLength)
                                                              select dbCol
                                                 where dbCols.Any()
                                                 select QuoteIdentifier(dsCol.ColumnName) + " (" +
                                                 ((OracleDbType)SystemToDbType(dsCol.DataType) + ")").ToString())];
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
        /// Determines if the database column type matches the dataset column type based on their
        /// system types, lengths, and numeric scales. It checks for compatibility between the
        /// database column type and the dataset column type, allowing for certain conversions
        /// (e.g., string to boolean, decimal to integer types) while ensuring that the lengths and
        /// numeric scales are appropriate for the types being compared. The method returns true if
        /// the types are considered a match based on these criteria; otherwise, it returns false.
        /// </summary>
        /// <param name="dbColSysType">The system type of the database column.</param>
        /// <param name="dbColLength">The length of the database column.</param>
        /// <param name="dbColNumScale">The numeric scale of the database column.</param>
        /// <param name="dsColType">The system type of the dataset column.</param>
        /// <param name="dsColLength">The length of the dataset column.</param>
        /// <returns>True if the types are considered a match; otherwise, false.</returns>
        private bool TypeMatch(Type dbColSysType, decimal dbColLength, decimal dbColNumScale, Type dsColType, int dsColLength)
        {
            TypeCode dbColSysTypeCode = Type.GetTypeCode(dbColSysType);
            TypeCode dsColTypeCode = Type.GetTypeCode(dsColType);

            if (dbColSysTypeCode == dsColTypeCode) return true;

            TypeCode[] floatingPoint = [TypeCode.Decimal, TypeCode.Double];

            return dsColTypeCode switch
            {
                TypeCode.Boolean => (dbColSysTypeCode == TypeCode.String && dbColLength == 5),
                TypeCode.Char => (dsColLength == -1 || dbColLength <= dsColLength) && dbColSysTypeCode == TypeCode.String,
                TypeCode.Decimal => dbColNumScale > 0 && Array.IndexOf(floatingPoint, dbColSysTypeCode) != 1,
                TypeCode.Double => Array.IndexOf(floatingPoint, dbColSysTypeCode) != -1,
                TypeCode.Int16 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.UInt16 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.Int32 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.UInt32 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.Int64 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.UInt64 => dbColNumScale == 0 && dbColSysTypeCode == TypeCode.Decimal,
                TypeCode.Object => dbColNumScale > 0 && Array.IndexOf(floatingPoint, dbColSysTypeCode) != 1,
                TypeCode.Single => dbColNumScale > 0 && Array.IndexOf(floatingPoint, dbColSysTypeCode) != 1,
                _ => false
            };
        }

        /// <summary>
        /// Gets the database connection associated with this DbOracle instance.
        /// </summary>
        /// <value>The database connection.</value>
        public override IDbConnection Connection { get { return _connection; } }

        /// <summary>
        /// Gets the DbConnectionStringBuilder associated with this DbOracle instance, which is used
        /// to build and manage the connection string for the Oracle database connection.
        /// </summary>
        /// <value>The DbConnectionStringBuilder for this DbOracle instance.</value>
        public override DbConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        /// <summary>
        /// Gets the current database transaction associated with this DbOracle instance, which can
        /// be used to manage transactions for database operations. If no transaction is currently
        /// active, this property will return null.
        /// </summary>
        /// <value>The current database transaction, or null if no transaction is active.</value>
        public override IDbTransaction Transaction
        {
            get { return _transaction; }
        }

        /// <summary>
        /// Creates and returns a new IDbCommand object associated with the current database connection.
        /// </summary>
        /// <returns>A new IDbCommand object.</returns>
        public override IDbCommand CreateCommand()
        {
            if (_connection != null)
                return _connection.CreateCommand();
            else
                return new OracleCommand();
        }

        //TODO: CreateAdapter
        //public override IDbDataAdapter CreateAdapter()
        //{
        //    return new OracleDataAdapter();
        //}

        /// <summary>
        /// Creates and returns an IDbDataAdapter for the specified table type T. The method checks
        /// if an adapter for the given table type already exists in the _adaptersDic dictionary. If
        /// it does, it returns the existing adapter. If not, it creates a new OracleDataAdapter,
        /// configures it with the appropriate commands (SELECT, INSERT, UPDATE, DELETE) based on
        /// the schema of the provided table, and adds it to the dictionary before returning it. The
        /// method also handles parameter creation and mapping for the commands based on the columns
        /// of the table. If the table does not have a primary key defined, the method returns null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table">The table for which to create the adapter.</param>
        /// <returns>An IDbDataAdapter for the specified table type T, or null if the table does not have a primary key defined.</returns>
        public override IDbDataAdapter CreateAdapter<T>(T table)
        {
            table ??= new T();

            OracleDataAdapter adapter;

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

                List<OracleParameter> deleteParams = [];
                List<OracleParameter> insertParams = [];
                List<OracleParameter> updateParams = [];
                List<OracleParameter> updateParamsOrig = [];

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
                OracleDbType isNullType = (OracleDbType)isNullTypeInt;

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
                        (OracleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

                    insColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    insertParams.Add(CreateParameter(insColParamName,
                        (OracleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updColParamName = ParameterName(_parameterPrefixCurr, c.ColumnName, i + _startParamNo);
                    updateParams.Add(CreateParameter(updColParamName,
                        (OracleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Current, false));

                    updOrigParamName = ParameterName(_parameterPrefixOrig, c.ColumnName, i + _startParamNo + columnCount + nullParamCount);
                    updateParamsOrig.Add(CreateParameter(updOrigParamName,
                        (OracleDbType)colType, ParameterDirection.Input, c.ColumnName, DataRowVersion.Original, false));

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
                    OracleCommandBuilder cmdBuilder = new(adapter);
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
        /// Creates and returns an OracleParameter with the specified properties, including name,
        /// type, direction, source column, source version, and null mapping. The method initializes
        /// a new OracleParameter object with the provided parameters and returns it for use in
        /// database commands. This helper method simplifies the creation of parameters for SQL
        /// commands by encapsulating the parameter configuration logic in one place.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="type">The OracleDbType of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, etc.).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The DataRowVersion for the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps null values.</param>
        /// <returns>An initialized OracleParameter object.</returns>
        private OracleParameter CreateParameter(string name, OracleDbType type, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OracleParameter param = new(name, type)
            {
                Direction = direction,
                SourceColumn = srcColumn,
                SourceVersion = srcVersion,
                SourceColumnNullMapping = nullMapping
            };
            return param;
        }

        /// <summary>
        /// Creates and returns an OracleParameter with the specified properties, including name,
        /// value, direction, source column, source version, and null mapping. The method
        /// initializes a new OracleParameter object with the provided parameters and returns it for
        /// use in database commands. This helper method simplifies the creation of parameters for
        /// SQL commands by encapsulating the parameter configuration logic in one place, allowing
        /// for easy parameter creation with specified values and types.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="direction">The direction of the parameter (Input, Output, etc.).</param>
        /// <param name="srcColumn">The source column for the parameter.</param>
        /// <param name="srcVersion">The DataRowVersion for the parameter.</param>
        /// <param name="nullMapping">Indicates whether the parameter maps null values.</param>
        /// <returns>An initialized OracleParameter object.</returns>
        private OracleParameter CreateParameter(string name, object value, ParameterDirection direction,
            string srcColumn, DataRowVersion srcVersion, bool nullMapping)
        {
            OracleParameter param = new(name, value)
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
        /// <param name="prefix">The prefix for the parameter name.</param>
        /// <param name="columnName">The name of the column associated with the parameter.</param>
        /// <param name="paramNo">The parameter number.</param>
        /// <returns>A generated parameter name.</returns>
        protected override string ParameterName(string prefix, string columnName, int paramNo)
        {
            return ParameterPrefix + prefix +
                (columnName.Length < 7 ? columnName : columnName.Substring(0, 7)) + "_p" + paramNo;
        }

        /// <summary>
        /// Formats the parameter name for use in SQL commands. In this implementation, it simply
        /// returns the parameter name as is, since Oracle parameters are typically referenced by
        /// their names without additional markers (e.g., @ for SQL Server). This method can be
        /// overridden to provide specific formatting if needed for different database systems.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The formatted parameter name.</returns>
        protected override string ParameterMarker(string parameterName)
        {
            return parameterName;
        }

        /// <summary>
        /// Fills the schema of the provided table based on the specified SQL query and schema type.
        /// The method checks if a SQL query is provided and then attempts to fill the schema of the
        /// table using either an existing adapter or by creating a new one based on the SQL query.
        /// It handles connection state and transaction management while performing the schema fill
        /// operation. If successful, it returns true; otherwise, it captures any exceptions and
        /// returns false, along with an error message describing the issue.
        /// </summary>
        /// <typeparam name="T">The type of the table to fill the schema for.</typeparam>
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
                OracleDataAdapter adapter = UpdateAdapter(table);
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
                        // potential data loss with Oracle types: NUMBER, DATE, all Timestamp types, and INTERVAL DAY TO SECOND
                        // _adapter.SafeMapping.Add("ColumnName", typeof(byte[])); _adapter.SafeMapping.Add("ColumnName", typeof(string));
                        // _adapter.SafeMapping.Add("*", typeof(byte[])); _adapter.SafeMapping.Add("*", typeof(string));

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
        /// Fills the provided table with data based on the specified SQL query. The method checks
        /// if a SQL query is provided and then attempts to fill the table using either an existing
        /// adapter or by creating a new one based on the SQL query. It handles connection state and
        /// transaction management while performing the fill operation. If successful, it returns
        /// the number of rows added to the table; otherwise, it captures any exceptions and returns
        /// -1, along with an error message describing the issue.
        /// </summary>
        /// <typeparam name="T">The type of the table to fill.</typeparam>
        /// <param name="sql">The SQL query to use for filling the table.</param>
        /// <param name="table">The table to fill with data.</param>
        /// <returns>The number of rows added to the table if successful; otherwise, -1.</returns>
        public override int FillTable<T>(string sql, ref T table)
        {
            if (String.IsNullOrEmpty(sql)) return 0;
            ConnectionState previousConnectionState = _connection.State;
            try
            {
                _errorMessage = String.Empty;
                table ??= new T();
                if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();
                OracleDataAdapter adapter = UpdateAdapter(table);
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

                    // potential data loss with Oracle types: NUMBER, DATE, all Timestamp types, and INTERVAL DAY TO SECOND
                    // _adapter.SafeMapping.Add("ColumnName", typeof(byte[])); _adapter.SafeMapping.Add("ColumnName", typeof(string));
                    // _adapter.SafeMapping.Add("*", typeof(byte[])); _adapter.SafeMapping.Add("*", typeof(string));

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
        /// existing transaction, it either commits or rolls back the previous transaction based on
        /// the commitPrevious parameter before starting a new transaction. The method also
        /// refreshes the command builder's schema after beginning the transaction. If successful,
        /// it returns true; otherwise, it captures any exceptions and returns false, along with an
        /// error message describing the issue.
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
        /// Commits the current database transaction. If there is an active transaction, it attempts
        /// to commit it and refreshes the command builder's schema afterward. If successful, it
        /// returns true; otherwise, it captures any exceptions and returns false, along with an
        /// error message describing the issue.
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
        /// Rolls back the current database transaction. If there is an active transaction, it
        /// attempts to roll it back and refreshes the command builder's schema afterward. If
        /// successful, it returns true; otherwise, it captures any exceptions and returns false,
        /// along with an error message describing the issue.
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
        /// method configures the command with the provided SQL, command timeout, and command type,
        /// and then executes it while managing the connection state and transaction. If successful,
        /// it returns an IDataReader; otherwise, it captures any exceptions and returns null, along
        /// with an error message describing the issue.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>An IDataReader containing the results of the query, or null if an error occurs.</returns>
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
        /// configures the command with the provided SQL, command timeout, and command type, and
        /// then executes it while managing the connection state and transaction. If successful, it
        /// returns the number of rows affected; otherwise, it captures any exceptions and returns
        /// -1, along with an error message describing the issue.
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
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
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
        /// Executes the query and returns the first column of the first row
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <param name="commandTimeout">The command timeout in seconds.</param>
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
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
        /// <param name="commandType">The type of the command (e.g., Text, StoredProcedure).</param>
        /// <returns>True if the query is valid; otherwise, false.</returns>
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
        /// Updates the database with the changes made to the provided table. The method checks if
        /// the table is null or empty and if an adapter is available. It then configures the
        /// adapter's InsertCommand, UpdateCommand, and DeleteCommand based on the provided SQL
        /// commands. If new commands are set, it can also configure safe type mappings for
        /// potential data loss with certain Oracle types. Finally, it calls the adapter's Update
        /// method to apply the changes to the database and returns the number of rows affected. If
        /// any exceptions occur during this process, it captures the error message and returns -1.
        /// </summary>
        /// <typeparam name="T">The type of the table being updated.</typeparam>
        /// <param name="table">The table containing the changes to be applied to the database.</param>
        /// <param name="insertCommand">The SQL command for inserting new rows.</param>
        /// <param name="updateCommand">The SQL command for updating existing rows.</param>
        /// <param name="deleteCommand">The SQL command for deleting rows.</param>
        /// <returns>The number of rows affected if the update is successful; otherwise, -1.</returns>
        public override int Update<T>(T table, string insertCommand, string updateCommand, string deleteCommand)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;
            if (_adapter == null) return -1;

            try
            {
                bool newCommand = false;

                if ((newCommand = !String.IsNullOrEmpty(insertCommand)))
                    _adapter.InsertCommand = new OracleCommand(insertCommand);
                if ((newCommand |= !String.IsNullOrEmpty(updateCommand)))
                    _adapter.UpdateCommand = new OracleCommand(updateCommand);
                if ((newCommand |= !String.IsNullOrEmpty(deleteCommand)))
                    _adapter.DeleteCommand = new OracleCommand(deleteCommand);

                if (newCommand)
                {
                    // potential data loss with Oracle types: NUMBER, DATE, all Timestamp types, and INTERVAL DAY TO SECOND
                    // _adapter.SafeMapping.Add("ColumnName", typeof(byte[])); _adapter.SafeMapping.Add("ColumnName", typeof(string));
                    // _adapter.SafeMapping.Add("*", typeof(byte[])); _adapter.SafeMapping.Add("*", typeof(string));
                }

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
        /// Updates the database with the changes made to the provided table. The method checks if
        /// the table is null or empty and if an adapter is available. It then calls the adapter's
        /// Update method to apply the changes to the database and returns the number of rows
        /// affected. If any exceptions occur during this process, it captures the error message and
        /// returns -1.
        /// </summary>
        /// <typeparam name="T">The type of the table being updated.</typeparam>
        /// <param name="table">The table containing the changes to be applied to the database.</param>
        /// <returns>The number of rows affected if the update is successful; otherwise, -1.</returns>
        public override int Update<T>(T table)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((table == null) || (table.Rows.Count == 0)) return 0;

            try
            {
                OracleDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided dataset and source table. The
        /// method checks if the dataset is null, if the source table name is null or empty, and if
        /// the dataset contains the specified source table. If any of these conditions are true, it
        /// returns 0. Otherwise, it attempts to update the database using an adapter for the
        /// specified source table. If an adapter is available, it calls the adapter's Update method
        /// to apply the changes to the database and returns the number of rows affected. If no
        /// adapter is available or if any exceptions occur during this process, it captures the
        /// error message and returns -1.
        /// </summary>
        /// <typeparam name="T">The type of the dataset being updated.</typeparam>
        /// <param name="dataSet">The dataset containing the changes to be applied to the database.</param>
        /// <param name="sourceTable">The name of the source table within the dataset.</param>
        /// <returns>The number of rows affected if the update is successful; otherwise, -1.</returns>
        public override int Update<T>(T dataSet, string sourceTable)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((dataSet == null) || String.IsNullOrEmpty(sourceTable) ||
                !dataSet.Tables.Contains(sourceTable)) return 0;

            try
            {
                DataTable table = dataSet.Tables[sourceTable];

                OracleDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(table);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided rows. The method checks if
        /// the rows array is null or empty and if an adapter is available. It then calls the
        /// adapter's Update method to apply the changes to the database and returns the number of
        /// rows affected. If any exceptions occur during this process, it captures the error
        /// message and returns -1.
        /// </summary>
        /// <typeparam name="T">The type of the table containing the rows.</typeparam>
        /// <typeparam name="R">The type of the rows being updated.</typeparam>
        /// <param name="rows">The array of rows containing the changes to be applied to the database.</param>
        /// <returns>The number of rows affected if the update is successful; otherwise, -1.</returns>
        public override int Update<T, R>(R[] rows)
        {
            ConnectionState previousConnectionState = _connection.State;
            if ((_connection.State & ConnectionState.Open) != ConnectionState.Open) _connection.Open();

            if ((rows == null) || (rows.Length == 0)) return 0;

            try
            {
                T table = (T)rows[0].Table;

                OracleDataAdapter adapter = UpdateAdapter(table);

                if (adapter != null)
                    return adapter.Update(rows);
                else
                    return -1;
            }
            catch { return -1; }
            finally { if (previousConnectionState == ConnectionState.Closed) _connection.Close(); }
        }

        /// <summary>
        /// Updates the database with the changes made to the provided table. The method checks if
        /// the table is null and if an adapter is available. If an adapter is not already
        /// associated with the table, it attempts to create one. It then ensures that the adapter's
        /// InsertCommand, UpdateCommand, and DeleteCommand are associated with the current
        /// transaction if one exists. Finally, it returns the adapter for use in updating the
        /// database with the changes made to the table. If any exceptions occur during this
        /// process, it captures the error message and returns null.
        /// </summary>
        /// <typeparam name="T">The type of the table being updated.</typeparam>
        /// <param name="table">The table containing the changes to be applied to the database.</param>
        /// <returns>The OracleDataAdapter associated with the table if available; otherwise, null.</returns>
        private OracleDataAdapter UpdateAdapter<T>(T table) where T : DataTable, new()
        {
            if (table == null) return null;

            OracleDataAdapter adapter;
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
        /// Gets the prefix used for parameter names in SQL queries. In this implementation, it returns a colon (":") which is the standard prefix for parameters in Oracle SQL.
        /// </summary>
        /// <value>The prefix used for parameter names in SQL queries.</value>
        protected override string ParameterPrefix
        {
            get { return ":"; }
        }

        #endregion Protected Members

        #region Browse Connection

        /// <summary>
        /// Displays a connection dialog to the user for establishing a connection to an Oracle
        /// database. The method creates an instance of the connection window and its associated
        /// ViewModel, sets up event handlers for closing the window, and shows the dialog. If the
        /// connection is successful, it updates the ConnectionString and DefaultSchema properties;
        /// if there is an error during the connection process, it captures the error message and
        /// displays it in a message box. The method also ensures that the connection dialog is
        /// centered on the parent window and is modal to prevent interaction with other parts of
        /// the application while the dialog is open.
        /// </summary>
        protected override void BrowseConnection()
        {
            try
            {
                _connWindow = new UI.View.Connection.ViewConnectOracle
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _connViewModel = new()
                {
                    DisplayName = "Oracle Connection"
                };

                // when ViewModel asks to be closed, close window
                _connViewModel.RequestClose -= ConnViewModel_RequestClose; // Safety: avoid double subscription.
                _connViewModel.RequestClose +=
                    new UI.ViewModel.ViewModelConnectOracle.RequestCloseEventHandler(ConnViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _connWindow.DataContext = _connViewModel;

                // show window
                _connWindow.ShowDialog();

                // throw error if connection failed
                if (!String.IsNullOrEmpty(_errorMessage)) throw (new Exception(_errorMessage));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Oracle Server responded with an error:\n\n" + ex.Message,
                     "Oracle Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Handles the RequestClose event from the connection ViewModel. When the event is
        /// triggered, it unsubscribes from the event to prevent memory leaks, closes the connection
        /// window, and checks if there is an error message or a valid connection string. If there
        /// is an error message, it sets the _errorMessage field; if there is a valid connection
        /// string, it updates the ConnectionString and DefaultSchema properties of the main class.
        /// This method ensures that the connection dialog is properly closed and that any resulting
        /// connection information or errors are appropriately handled.
        /// </summary>
        /// <param name="connString"></param>
        /// <param name="defaultSchema"></param>
        /// <param name="errorMsg"></param>
        protected void ConnViewModel_RequestClose(string connString, string defaultSchema, string errorMsg)
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

        #endregion Browse Connection

        #region SQLBuilder Members

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

                switch ((OracleDbType)colType)
                {
                    case OracleDbType.Char:
                    case OracleDbType.Clob:
                    case OracleDbType.NChar:
                    case OracleDbType.NClob:
                    case OracleDbType.NVarchar2:
                    case OracleDbType.Varchar2:
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case OracleDbType.Date:
                    case OracleDbType.IntervalDS:
                    case OracleDbType.IntervalYM:
                    case OracleDbType.TimeStamp:
                    case OracleDbType.TimeStampLTZ:
                    case OracleDbType.TimeStampTZ:
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

        #endregion SQLBuilder Members

        #region Private Methods

        /// <summary>
        /// Populates the type mapping dictionaries that map between .NET types and Oracle SQL
        /// types. The method retrieves metadata for the OracleDbType enumeration and then fills
        /// three dictionaries: one for mapping .NET types to Oracle SQL types, one for mapping
        /// Oracle SQL types to .NET types, and one for mapping SQL type synonyms to Oracle SQL
        /// types. The mappings take into account whether the data is Unicode and whether time zone
        /// information is used, as well as various lengths and precisions for text, binary, time,
        /// and numeric data. This setup allows for accurate type conversions when working with
        /// Oracle databases in .NET applications.
        /// </summary>
        /// <param name="isUnicode">Indicates whether the data is Unicode.</param>
        /// <param name="useTimeZone">Indicates whether time zone information is used.</param>
        /// <param name="textLength">Specifies the length of text data.</param>
        /// <param name="binaryLength">Specifies the length of binary data.</param>
        /// <param name="timePrecision">Specifies the precision of time data.</param>
        /// <param name="numericPrecision">Specifies the precision of numeric data.</param>
        /// <param name="numericScale">Specifies the scale of numeric data.</param>
        private void PopulateTypeMaps(bool isUnicode, bool useTimeZone, uint textLength,
            uint binaryLength, uint timePrecision, uint numericPrecision, uint numericScale)
        {
            GetMetaData(typeof(OracleDbType), _connection, _transaction);

            Dictionary<Type, int> typeMapSystemToSQLAdd = [];
            typeMapSystemToSQLAdd.Add(typeof(Byte), (int)OracleDbType.Byte);
            typeMapSystemToSQLAdd.Add(typeof(Char), (int)OracleDbType.Char);
            typeMapSystemToSQLAdd.Add(typeof(DateTime), (int)(useTimeZone ? OracleDbType.TimeStampTZ : OracleDbType.TimeStamp));
            typeMapSystemToSQLAdd.Add(typeof(TimeSpan), (int)OracleDbType.IntervalDS);
            typeMapSystemToSQLAdd.Add(typeof(Decimal), (int)OracleDbType.Decimal);
            typeMapSystemToSQLAdd.Add(typeof(Double), (int)OracleDbType.Double);
            typeMapSystemToSQLAdd.Add(typeof(Int16), (int)OracleDbType.Int16);
            typeMapSystemToSQLAdd.Add(typeof(Int32), (int)OracleDbType.Int32);
            typeMapSystemToSQLAdd.Add(typeof(Int64), (int)OracleDbType.Int64);
            typeMapSystemToSQLAdd.Add(typeof(Object), (int)OracleDbType.Blob);
            typeMapSystemToSQLAdd.Add(typeof(SByte), (int)OracleDbType.Int16);
            typeMapSystemToSQLAdd.Add(typeof(Single), (int)OracleDbType.Single);
            typeMapSystemToSQLAdd.Add(typeof(String), (int)(isUnicode ? OracleDbType.NVarchar2 : OracleDbType.Varchar2));
            typeMapSystemToSQLAdd.Add(typeof(UInt16), (int)OracleDbType.Int16);
            typeMapSystemToSQLAdd.Add(typeof(UInt32), (int)OracleDbType.Int32);
            typeMapSystemToSQLAdd.Add(typeof(UInt64), (int)OracleDbType.Int64);
            typeMapSystemToSQLAdd.Add(typeof(Byte[]), (int)OracleDbType.Blob);
            typeMapSystemToSQLAdd.Add(typeof(Char[]), (int)OracleDbType.Clob);
            typeMapSystemToSQLAdd.Add(typeof(Guid), (int)OracleDbType.NVarchar2);
            typeMapSystemToSQLAdd.Add(typeof(Boolean), (int)OracleDbType.Char);

            Dictionary<int, Type> typeMapSQLToSystemAdd = [];
            typeMapSQLToSystemAdd.Add((int)OracleDbType.BFile, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Blob, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Byte, typeof(Byte));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Char, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Clob, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Date, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Decimal, typeof(Decimal));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Double, typeof(Double));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Int16, typeof(Int16));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Int32, typeof(Int32));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Int64, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.IntervalDS, typeof(TimeSpan));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.IntervalYM, typeof(Int64));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Long, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.LongRaw, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.NChar, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.NClob, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.NVarchar2, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Object, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Raw, typeof(Byte[]));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.RefCursor, typeof(object));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Single, typeof(Single));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.TimeStamp, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.TimeStampLTZ, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.TimeStampTZ, typeof(DateTime));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.Varchar2, typeof(String));
            typeMapSQLToSystemAdd.Add((int)OracleDbType.XmlType, typeof(string));

            Dictionary<string, int> sqlSynonymsAdd = [];
            sqlSynonymsAdd.Add("bfile", (int)OracleDbType.BFile);
            sqlSynonymsAdd.Add("blob", (int)OracleDbType.Blob);
            sqlSynonymsAdd.Add("character", (int)OracleDbType.Char);
            sqlSynonymsAdd.Add("char", (int)OracleDbType.Char);
            sqlSynonymsAdd.Add("clob", (int)OracleDbType.Clob);
            sqlSynonymsAdd.Add("date", (int)OracleDbType.Date);
            sqlSynonymsAdd.Add("double precision", (int)OracleDbType.Double);
            sqlSynonymsAdd.Add("binary_double", (int)OracleDbType.Double);
            sqlSynonymsAdd.Add("float", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("binary_float", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("real", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("smallint", (int)OracleDbType.Int16);
            sqlSynonymsAdd.Add("integer", (int)OracleDbType.Int32);
            sqlSynonymsAdd.Add("int", (int)OracleDbType.Int32);
            sqlSynonymsAdd.Add("pls_integer", (int)OracleDbType.Int32);
            sqlSynonymsAdd.Add("binary_integer", (int)OracleDbType.Int32);
            sqlSynonymsAdd.Add("interval day to second", (int)OracleDbType.IntervalDS);
            sqlSynonymsAdd.Add("interval year to month", (int)OracleDbType.IntervalYM);
            sqlSynonymsAdd.Add("long raw", (int)OracleDbType.LongRaw);
            sqlSynonymsAdd.Add("long", (int)OracleDbType.Varchar2);
            sqlSynonymsAdd.Add("long varchar", (int)OracleDbType.Varchar2);
            sqlSynonymsAdd.Add("nchar", (int)OracleDbType.NChar);
            sqlSynonymsAdd.Add("national char", (int)OracleDbType.NChar);
            sqlSynonymsAdd.Add("national character", (int)OracleDbType.NChar);
            sqlSynonymsAdd.Add("nclob", (int)OracleDbType.NClob);
            sqlSynonymsAdd.Add("number", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("numeric", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("decimal", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("dec", (int)OracleDbType.Decimal);
            sqlSynonymsAdd.Add("national char varying", (int)OracleDbType.NVarchar2);
            sqlSynonymsAdd.Add("national character varying", (int)OracleDbType.NVarchar2);
            sqlSynonymsAdd.Add("nchar varying", (int)OracleDbType.NVarchar2);
            sqlSynonymsAdd.Add("nvarchar2", (int)OracleDbType.NVarchar2);
            sqlSynonymsAdd.Add("raw", (int)OracleDbType.Raw);
            sqlSynonymsAdd.Add("rowid", (int)OracleDbType.Int64);
            sqlSynonymsAdd.Add("urowid", (int)OracleDbType.Int64);
            sqlSynonymsAdd.Add("timestamp", (int)OracleDbType.TimeStamp);
            sqlSynonymsAdd.Add("timestamp with local time zone", (int)OracleDbType.TimeStampLTZ);
            sqlSynonymsAdd.Add("timestamp with time zone", (int)OracleDbType.TimeStampTZ);
            sqlSynonymsAdd.Add("character varying", (int)OracleDbType.Varchar2);
            sqlSynonymsAdd.Add("char varying", (int)OracleDbType.Varchar2);
            sqlSynonymsAdd.Add("varchar", (int)OracleDbType.Varchar2);
            sqlSynonymsAdd.Add("varchar2", (int)OracleDbType.Varchar2);

            foreach (KeyValuePair<Type, int> kv in typeMapSystemToSQLAdd)
            {
                if (!_typeMapSystemToSQL.ContainsKey(kv.Key))
                    _typeMapSystemToSQL.Add(kv.Key, kv.Value);
            }

            ReplaceType(typeof(DateTime), (int)(useTimeZone ? OracleDbType.TimeStampTZ :
                OracleDbType.TimeStamp), _typeMapSystemToSQL);
            ReplaceType(typeof(String), (int)(isUnicode ? OracleDbType.NVarchar2 :
                OracleDbType.Varchar2), _typeMapSystemToSQL);

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

            _typeMapSQLToSQLCode.Add((int)OracleDbType.BFile, "BFILE");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Blob, "BLOB");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Byte, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Char, "CHAR");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Clob, "CLOB");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Date, "DATE");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Double, "DOUBLE PRECISION");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Int16, "SMALLINT");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Int32, "INTEGER");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.LongRaw, "LONG RAW");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.NClob, "NCLOB");
            _typeMapSQLToSQLCode.Add((int)OracleDbType.NVarchar2, String.Format("NVARCHAR2 ({0})", textLength));
            _typeMapSQLToSQLCode.Add((int)OracleDbType.Raw, binaryLength > 0 ? String.Format("RAW ({0})", binaryLength) : "RAW");
            _typeMapSQLCodeToSQL.Add(binaryLength > 0 ? String.Format("RAW ({0})", binaryLength) : "RAW", (int)OracleDbType.Raw);

            _typeMapSQLCodeToSQL.Add("BFILE", (int)OracleDbType.BFile);
            _typeMapSQLCodeToSQL.Add("BLOB", (int)OracleDbType.Blob);
            _typeMapSQLCodeToSQL.Add("SMALLINT", (int)OracleDbType.Byte);
            _typeMapSQLCodeToSQL.Add("CHAR", (int)OracleDbType.Char);
            _typeMapSQLCodeToSQL.Add("CLOB", (int)OracleDbType.Clob);
            _typeMapSQLCodeToSQL.Add("DATE", (int)OracleDbType.Date);
            _typeMapSQLCodeToSQL.Add("DOUBLE PRECISION", (int)OracleDbType.Double);
            //_typeMapSQLCodeToSQL.Add("SMALLINT", (int)OracleDbType.Int16);
            _typeMapSQLCodeToSQL.Add("INTEGER", (int)OracleDbType.Int32);
            _typeMapSQLCodeToSQL.Add("LONG RAW", (int)OracleDbType.LongRaw);
            _typeMapSQLCodeToSQL.Add("NCLOB", (int)OracleDbType.NClob);
            _typeMapSQLCodeToSQL.Add(String.Format("NVARCHAR2 ({0})", textLength), (int)OracleDbType.NVarchar2);

            if ((numericPrecision > 0) && (numericScale > 0))
            {
                _typeMapSQLToSQLCode.Add((int)OracleDbType.Decimal, String.Format("NUMBER ({0},{1})", numericPrecision, numericScale));
                _typeMapSQLCodeToSQL.Add(String.Format("NUMBER ({0},{1})", numericPrecision, numericScale), (int)OracleDbType.Decimal);
            }
            else if (numericPrecision > 0)
            {
                //_typeMapSQLToSQLCode.Add((int)OracleDbType.Decimal, String.Format("FLOAT ({0})", numericPrecision));
                _typeMapSQLCodeToSQL.Add(String.Format("FLOAT ({0})", numericPrecision), (int)OracleDbType.Decimal);

                _typeMapSQLToSQLCode.Add((int)OracleDbType.Decimal, String.Format("NUMBER ({0})", numericPrecision));
                _typeMapSQLCodeToSQL.Add(String.Format("NUMBER ({0})", numericPrecision), (int)OracleDbType.Decimal);
            }
            else
            {
                //_typeMapSQLToSQLCode.Add((int)OracleDbType.Decimal, "FLOAT");
                _typeMapSQLCodeToSQL.Add("FLOAT", (int)OracleDbType.Decimal);

                _typeMapSQLToSQLCode.Add((int)OracleDbType.Decimal, "NUMBER");
                _typeMapSQLCodeToSQL.Add("NUMBER", (int)OracleDbType.Decimal);
            }
            if (timePrecision > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OracleDbType.IntervalDS,
                    String.Format("INTERVAL DAY ({0}) TO SECOND ({1})", timePrecision, numericPrecision));
                _typeMapSQLToSQLCode.Add((int)OracleDbType.IntervalYM,
                    String.Format("INTERVAL YEAR ({0}) TO MONTH", timePrecision));
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStamp, String.Format("TIMESTAMP ({0})", timePrecision));
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStampLTZ,
                    String.Format("TIMESTAMP ({0}) WITH LOCAL TIME ZONE", timePrecision));
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStampTZ,
                    String.Format("TIMESTAMP ({0}) WITH TIME ZONE", timePrecision));

                _typeMapSQLCodeToSQL.Add(String.Format("INTERVAL DAY ({0}) TO SECOND ({1})", timePrecision,
                    numericPrecision), (int)OracleDbType.IntervalDS);
                _typeMapSQLCodeToSQL.Add(String.Format("INTERVAL YEAR ({0}) TO MONTH", timePrecision),
                    (int)OracleDbType.IntervalYM);
                _typeMapSQLCodeToSQL.Add(String.Format("TIMESTAMP ({0})", timePrecision), (int)OracleDbType.TimeStamp);
                _typeMapSQLCodeToSQL.Add(String.Format("TIMESTAMP ({0}) WITH LOCAL TIME ZONE", timePrecision),
                    (int)OracleDbType.TimeStampLTZ);
                _typeMapSQLCodeToSQL.Add(String.Format("TIMESTAMP ({0}) WITH TIME ZONE", timePrecision),
                    (int)OracleDbType.TimeStampTZ);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OracleDbType.IntervalDS, numericPrecision > 0 ?
                    String.Format("INTERVAL DAY TO SECOND ({0})", numericPrecision) : "INTERVAL DAY TO SECOND");
                _typeMapSQLToSQLCode.Add((int)OracleDbType.IntervalYM, "INTERVAL YEAR TO MONTH");
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStamp, "TIMESTAMP");
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStampLTZ, "TIMESTAMP WITH LOCAL TIME ZONE");
                _typeMapSQLToSQLCode.Add((int)OracleDbType.TimeStampTZ, "TIMESTAMP WITH TIME ZONE");

                _typeMapSQLCodeToSQL.Add(numericPrecision > 0 ? String.Format("INTERVAL DAY TO SECOND ({0})",
                    numericPrecision) : "INTERVAL DAY TO SECOND", (int)OracleDbType.IntervalDS);
                _typeMapSQLCodeToSQL.Add("INTERVAL YEAR TO MONTH", (int)OracleDbType.IntervalYM);
                _typeMapSQLCodeToSQL.Add("TIMESTAMP", (int)OracleDbType.TimeStamp);
                _typeMapSQLCodeToSQL.Add("TIMESTAMP WITH LOCAL TIME ZONE", (int)OracleDbType.TimeStampLTZ);
                _typeMapSQLCodeToSQL.Add("TIMESTAMP WITH TIME ZONE", (int)OracleDbType.TimeStampTZ);
            }
            if (textLength > 0)
            {
                _typeMapSQLToSQLCode.Add((int)OracleDbType.NChar, String.Format("NCHAR({0})", textLength));
                _typeMapSQLToSQLCode.Add((int)OracleDbType.Varchar2, String.Format("VARCHAR2({0})", textLength));

                _typeMapSQLCodeToSQL.Add(String.Format("VARCHAR2 ({0})", textLength), (int)OracleDbType.Varchar2);
                _typeMapSQLCodeToSQL.Add(String.Format("NCHAR ({0})", textLength), (int)OracleDbType.NChar);
            }
            else
            {
                _typeMapSQLToSQLCode.Add((int)OracleDbType.NChar, "NCHAR");
                _typeMapSQLToSQLCode.Add((int)OracleDbType.Varchar2, "VARCHAR2");

                _typeMapSQLCodeToSQL.Add("VARCHAR2", (int)OracleDbType.Varchar2);
                _typeMapSQLCodeToSQL.Add("NCHAR", (int)OracleDbType.NChar);
            }
        }

        #endregion Private Methods
    }
}