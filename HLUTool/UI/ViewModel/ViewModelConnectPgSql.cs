// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HLU.Data.Connection;
using HLU.Enums;
using Npgsql;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    class ViewModelConnectPgSql : ViewModelBase, IDataErrorInfo
    {
        #region private Members

        private IntPtr _windowHandle;
        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private string[] _sslModes;
        private string[] _encodings;
        private string _encoding = "<default>";
        private string[] _databases = [];
        private List<String> _schemata = [];

        private NpgsqlConnectionStringBuilder _connStrBuilder;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelConnectPgSql"/> class.
        /// </summary>
        public ViewModelConnectPgSql()
        {
            _connStrBuilder = new()
            {
                Host = "localhost",
                Port = 5432,
                SslMode = Npgsql.SslMode.Prefer
            };
        }

        #endregion

        #region Connection String Builder

        /// <summary>
        /// Gets the connection string builder which is used to build the PostgreSQL connection
        /// string based on user input in the view.
        /// </summary>
        /// <value>The NpgsqlConnectionStringBuilder used to build the connection string.</value>
        public NpgsqlConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        #endregion

        #region Display Name

        /// <summary>
        /// Gets or sets the display name of the view model, which is used as the title of the
        /// connection window.
        /// </summary>
        /// <value>The display name of the view model.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        #endregion

        #region Window Title

        /// <summary>
        /// Gets the title of the connection window, which is set to the display name of the view model.
        /// </summary>
        /// <value>The title of the connection window.</value>
        public override string WindowTitle { get { return DisplayName; } }

        #endregion

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string connString, string encoding, string defaultSchema, string errorMsg);

        // Declare the event that is raised when the connection window should be closed, passing the
        // connection string, encoding, default schema and any error message back to the caller.
        public event RequestCloseEventHandler RequestClose;

        #endregion

        #region Ok Command

        /// <summary>
        /// Create Ok button command
        /// </summary>
        /// <value>The command for the Ok button.</value>
        /// <returns>The command for the Ok button.</returns>
        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    Action<object> okAction = new(this.OkCommandClick);
                    _okCommand = new(okAction, param => this.CanOk);
                }

                return _okCommand;
            }
        }

        /// <summary>
        /// Handles event when Ok button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void OkCommandClick(object param)
        {
            try
            {
                using NpgsqlConnection connection = new(_connStrBuilder.ConnectionString);

                connection.Open();
                connection.Close();

                RequestClose?.Invoke(_connStrBuilder.ConnectionString,
                    _encoding != _encodings[0] ? _encoding : null,
                    _connStrBuilder.SearchPath.Split(',')[0],
                    null);
            }
            catch (NpgsqlException exNpgsql)
            {
                MessageBox.Show("PostgreSQL Server responded with an error:\n\n" + exNpgsql.Message,
                    "PostgreSQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Ok button can be enabled based on the current state
        /// of the connection string builder. To be enabled the following must be true: server name
        /// and database must be set; if windows authentication is not set then a username and
        /// password are required.
        /// </summary>
        /// <value>True if the Ok button can be enabled; otherwise, false.</value>
        private bool CanOk
        {
            get
            {
                return !(String.IsNullOrEmpty(_connStrBuilder.Host) || (_connStrBuilder.Port == 0) ||
                    String.IsNullOrEmpty(_connStrBuilder.Database) || String.IsNullOrEmpty(_connStrBuilder.Username) ||
                    String.IsNullOrEmpty(_connStrBuilder.SearchPath));
            }
        }

        #endregion

        #region Cancel Command

        /// <summary>
        /// Create Cancel button command
        /// </summary>
        /// <value>The command for the Cancel button.</value>
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    Action<object> cancelAction = new(this.CancelCommandClick);
                    _cancelCommand = new(cancelAction);
                }

                return _cancelCommand;
            }
        }

        /// <summary>
        /// Handles event when Cancel button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(null, null, null, null);
        }

        #endregion

        #region Host

        /// <summary>
        /// Gets or sets the host name of the PostgreSQL server to which to connect. This is a
        /// required property for a valid connection string.
        /// </summary>
        /// <value>The host name of the PostgreSQL server.</value>
        public string Host
        {
            get { return _connStrBuilder.Host; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.Host))
                    _connStrBuilder.Host = value;
            }
        }

        /// <summary>
        /// Gets or sets the port number of the PostgreSQL server to which to connect. This is a
        /// required property for a valid connection string.
        /// </summary>
        /// <value>The port number of the PostgreSQL server.</value>
        public int Port
        {
            get { return _connStrBuilder.Port; }
            set { if (value != _connStrBuilder.Port) _connStrBuilder.Port = value; }
        }

        /// <summary>
        /// Gets the available SSL modes for PostgreSQL connections. This is used to populate the
        /// SSL mode dropdown in the view.
        /// </summary>
        /// <value>An array of available SSL modes for PostgreSQL connections.</value>
        public string[] SslModes
        {
            get
            {
                _sslModes ??= ["Allow", "Disable", "Prefer", "Require"];
                return _sslModes;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the SSL mode for the PostgreSQL connection. This is used to specify whether
        /// to use SSL when connecting to the PostgreSQL server.
        /// </summary>
        /// <value>The SSL mode for the PostgreSQL connection.</value>
        public string SslMode
        {
            get { return Enum.GetName(typeof(Npgsql.SslMode), _connStrBuilder.SslMode); }
            set
            {
                if (Enum.IsDefined(typeof(Npgsql.SslMode), value))
                {
                    _connStrBuilder.SslMode = (Npgsql.SslMode)Enum.Parse(typeof(Npgsql.SslMode), value);

                    //TODO: PgSql SSL = true/false
                    //if (_connStrBuilder.SslMode == Npgsql.SslMode.Require)
                    //    _connStrBuilder.SSL = true;
                    //else if (_connStrBuilder.SslMode == Npgsql.SslMode.Disable)
                    //    _connStrBuilder.SSL = false;
                }

            }
        }

        #endregion

        #region Database

        /// <summary>
        /// Gets the available databases on the PostgreSQL server. This is used to populate the
        /// database dropdown in the view after a connection to the server has been established.
        /// </summary>
        /// <value>An array of available databases on the PostgreSQL server.</value>
        public string[] Databases
        {
            get { return _databases; }
            set { }
        }

        /// <summary>
        /// Gets or sets the database name to which to connect on the PostgreSQL server. This is a
        /// required property for a valid connection string.
        /// </summary>
        /// <value>The database name to which to connect on the PostgreSQL server.</value>
        public string Database
        {
            get { return _connStrBuilder.Database; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.Database))
                {
                    _connStrBuilder.Database = value;
                    LoadSchemata();
                }
            }
        }

        /// <summary>
        /// Loads the available databases from the PostgreSQL server and updates the Databases property.
        /// </summary>
        private void LoadDatabases()
        {
            string[] databaseList = [];

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.Host))
                {
                    using NpgsqlConnection cn = new(_connStrBuilder.ConnectionString);
                    cn.Open();

                    DataTable databases = cn.GetSchema("Databases", null);
                    databaseList = [.. (from r in databases.AsEnumerable()
                            let dbName = r.Field<string>("database_name")
                            where !dbName.StartsWith("template", StringComparison.CurrentCultureIgnoreCase)
                            select dbName).OrderBy(t => t)];
                }
            }
            catch (Exception ex)
            {
                if (ex is NpgsqlException)
                    MessageBox.Show(ex.Message, "PostgreSQL Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _databases = databaseList;
                OnPropertyChanged(nameof(Databases));

                if (_databases.Length == 1)
                    _connStrBuilder.Database = _databases[0];
                OnPropertyChanged(nameof(Database));
            }
        }

        #endregion

        #region Encoding

        /// <summary>
        /// Gets the available encodings for PostgreSQL connections. This is used to populate the encoding
        /// dropdown in the connection window.
        /// </summary>
        /// <value>An array of available encodings for PostgreSQL connections.</value>
        public string[] Encodings
        {
            get
            {
                _encodings ??= [ "<default>", "BIG5", "EUC_CN", "EUC_JP", "EUC_KR", "EUC_TW",
                        "GB18030", "GBK", "ISO_8859_5", "ISO_8859_6", "ISO_8859_7", "ISO_8859_8", "JOHAB",
                        "KOI8", "LATIN1", "LATIN2", "LATIN3", "LATIN4", "LATIN5", "LATIN6", "LATIN7", "LATIN8",
                        "LATIN9", "LATIN10", "MULE_INTERNAL", "SJIS", "SQL_ASCII", "UHC", "UTF8", "WIN866",
                        "WIN874", "WIN1250", "WIN1251", "WIN1252", "WIN1253", "WIN1254", "WIN1255", "WIN1256",
                        "WIN1257","WIN1258" ];
                return _encodings;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the encoding to use for the PostgreSQL connection. This is used to specify
        /// the client encoding.
        /// </summary>
        /// <value>The encoding to use for the PostgreSQL connection.</value>
        public string Encoding
        {
            get { return _encoding; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _encoding))
                    _encoding = value;
            }
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Gets or sets the user name to use for authentication when connecting to the PostgreSQL
        /// server. This is a required field for establishing a connection.
        /// </summary>
        /// <value>The user name to use for authentication when connecting to the PostgreSQL server.</value>
        public string UserName
        {
            get { return _connStrBuilder.Username; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.Username))
                    _connStrBuilder.Username = value;
            }
        }

        /// <summary>
        /// Gets or sets the password to use for authentication when connecting to the PostgreSQL
        /// server. This is a required field for establishing a connection.
        /// </summary>
        /// <value>The password to use for authentication when connecting to the PostgreSQL server.</value>
        public string Password
        {
            get { return _connStrBuilder.Password; }
            set { if (value != _connStrBuilder.Password) _connStrBuilder.Password = value; }
        }

        #endregion

        #region Default Schema

        /// <summary>
        /// Gets the available schemata in the selected database on the PostgreSQL server. This is
        /// used to populate the schemata dropdown in the connection window.
        /// </summary>
        /// <value>An array of available schemata in the selected database on the PostgreSQL server.</value>
        public string[] Schemata
        {
            get { return [.. _schemata]; }
            set { }
        }

        /// <summary>
        /// Gets or sets the search path to use for the PostgreSQL connection. This is used to
        /// specify the default schema
        /// </summary>
        /// <value>The search path to use for the PostgreSQL connection.</value>
        public string SearchPath
        {
            get { return _connStrBuilder.SearchPath; }
            set { if (value != _connStrBuilder.SearchPath) _connStrBuilder.SearchPath = value; }
        }

        /// <summary>
        /// Loads the available schemata from the selected database on the PostgreSQL server and
        /// updates the Schemata property.
        /// </summary>
        private void LoadSchemata()
        {
            List<String> schemaList = [];

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.Host))
                {
                    using NpgsqlConnection cn = new(_connStrBuilder.ConnectionString);
                    cn.Open();

                    using NpgsqlCommand cmd = cn.CreateCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT schema_name FROM information_schema.schemata" +
                    " WHERE schema_name !~* '^(pg|information)_'" +
                    " AND catalog_name = @database";
                    cmd.Parameters.AddWithValue("@database", _connStrBuilder.Database);

                    using NpgsqlDataAdapter adapter = new(cmd);
                    DataTable dbTable = new();

                    try
                    {
                        adapter.Fill(dbTable);
                        schemaList = [.. (from r in dbTable.AsEnumerable()
                              let schemaName = r.Field<string>("schema_name")
                              select schemaName).OrderBy(s => s)];
                        _connStrBuilder.SearchPath = DbBase.GetDefaultSchema(Backends.PostgreSql, _connStrBuilder, schemaList);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                if (ex is NpgsqlException)
                    MessageBox.Show(ex.Message, "PostgreSQL Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _schemata = schemaList;
                OnPropertyChanged(nameof(Schemata));

                if (_schemata.Count == 1)
                    _connStrBuilder.SearchPath = _schemata[0];
                OnPropertyChanged(nameof(SearchPath));
            }
        }

        #endregion

        #region View Events

        /// <summary>
        /// Handles events from the view to trigger loading of databases and schemata when the user
        /// interacts with the corresponding dropdowns in the connection window. The window handle
        /// is passed in to allow the view model to be aware of which window is active when handling events.
        /// </summary>
        /// <param name="windowHandle">The handle of the window that triggered the event.</param>
        /// <param name="propertyName">The name of the property that triggered the event.</param>
        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            if (windowHandle != IntPtr.Zero) _windowHandle = windowHandle;

            switch (propertyName)
            {
                case "Database":
                    LoadDatabases();
                    break;
                case "SearchPath":
                    LoadSchemata();
                    break;
            }
        }

        #endregion

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object. The error message can be
        /// used to provide feedback to the user in the UI.
        /// </summary>
        /// <value>An error message indicating what is wrong with this object; otherwise, null or empty string.</value>
        string IDataErrorInfo.Error
        {
            get
            {
                StringBuilder error = new();

                if (String.IsNullOrEmpty(_connStrBuilder.Host))
                    error.Append(", host name");
                if (_connStrBuilder.Port == 0)
                    error.Append(", port");
                if (String.IsNullOrEmpty(_connStrBuilder.Database))
                    error.Append(", database name");
                if (String.IsNullOrEmpty(_connStrBuilder.Username))
                    error.Append(", user name");
                if (String.IsNullOrEmpty(_connStrBuilder.SearchPath))
                    error.Append(", search path");

                if (error.Length > 0)
                    return error.Remove(0, 1).Insert(0, "Please provide").ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets an error message for the property with the given name. This is used to provide feedback to the user
        /// in the UI.
        /// </summary>
        /// <param name="columnName">The name of the property for which to get the error message.</param>
        /// <returns>An error message for the specified property; otherwise, null or empty string.</returns>
        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "Host":
                        if (String.IsNullOrEmpty(_connStrBuilder.Host))
                            error = "Error: You must provide a host name";
                        break;
                    case "Port":
                        if (_connStrBuilder.Port == 0)
                            error = "Error: You must provide a port";
                        break;
                    case "Database":
                        if (String.IsNullOrEmpty(_connStrBuilder.Database))
                            error = "Error: You must provide a database name";
                        break;
                    case "UserName":
                        if (String.IsNullOrEmpty(_connStrBuilder.Username))
                            error = "Error: You must provide a user name";
                        break;
                    case "SearchPath":
                        if (String.IsNullOrEmpty(_connStrBuilder.SearchPath))
                            error = "Error: You must provide a search path";
                        break;
                }

                // dirty commands registered with CommandManager so they are queried to see if they can execute now
                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        #endregion
    }
}