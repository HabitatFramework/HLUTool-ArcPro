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
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HLU.Data.Connection;
using HLU.Properties;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    class ViewModelConnectSqlServer : ViewModelBase, IDataErrorInfo
    {
        #region Private Members

        //private IntPtr _windowHandle;
        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private List<String> _servers;
        private List<String> _databases = [];
        private List<String> _schemata = [];
        private string _defaultSchema;
        private SqlConnectionStringBuilder _connStrBuilder;

        #endregion Private Members

        #region Constructor

        /// <summary>
        /// Initialise the connection string builder and set default values for properties. These
        /// will be used to populate the dialog fields and can be changed by the user. The
        /// connection string builder is used to build the connection string based on user input and
        /// also to test the connection when Ok is clicked.
        /// </summary>
        public ViewModelConnectSqlServer()
        {
            _connStrBuilder = [];
        }

        #endregion Constructor

        #region Connection String Builder

        /// <summary>
        /// Get the connection string builder which is used to build the connection string based on
        /// user input and to test the connection when Ok is clicked.
        /// </summary>
        /// <value>The <see cref="SqlConnectionStringBuilder"/> instance used to build the connection string.</value>
        public SqlConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        #endregion Connection String Builder

        #region Display Name

        /// <summary>
        /// Get or set the display name for the dialog.
        /// </summary>
        /// <value>The display name for the dialog.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        #endregion Display Name

        #region Window Title

        /// <summary>
        /// Get the window title for the dialog.
        /// </summary>
        /// <value>The window title for the dialog.</value>
        public override string WindowTitle { get { return DisplayName; } }

        #endregion Window Title

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string connString, string defaultSchema, string errorMsg);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Get the Ok command which is used to test the connection and close the dialog if the
        /// connection is successful.
        /// </summary>
        /// <value>The command to be executed when the Ok button is clicked.</value>
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
                using SqlConnection cn = new(_connStrBuilder.ConnectionString);

                cn.Open();
                // Close() is automatic when using disposes

                _connStrBuilder.PersistSecurityInfo = Settings.Default.DbConnectionPersistSecurityInfo;

                RequestClose?.Invoke(_connStrBuilder.ConnectionString, _defaultSchema, null);
            }
            catch (SqlException exSql)
            {
                MessageBox.Show("SQL Server responded with an error:\n\n" + exSql.Message,
                    "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Get a value indicating whether the Ok command can be executed.
        /// To be enabled the following must be true:
        /// server name and database must be set; if windows authentication is not set then a username and password are required.
        /// </summary>
        /// <value>True if the Ok command can be executed; otherwise, false.</value>
        private bool CanOk
        {
            get
            {
                return !(String.IsNullOrEmpty(_connStrBuilder.DataSource) || String.IsNullOrEmpty(_connStrBuilder.InitialCatalog) ||
                    (!_connStrBuilder.IntegratedSecurity && (String.IsNullOrEmpty(_connStrBuilder.Password) ||
                    String.IsNullOrEmpty(_connStrBuilder.UserID))) && !String.IsNullOrEmpty(_defaultSchema));
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Get the Cancel command which is used to close the dialog without making any changes.
        /// </summary>
        /// <value>The command to be executed when the Cancel button is clicked.</value>
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
            RequestClose?.Invoke(null, null, null);
        }

        #endregion Cancel Command

        #region Server

        /// <summary>
        /// Gets or sets the server name for the connection.
        /// </summary>
        /// <value>The server name for the connection.</value>
        public string Server
        {
            get { return _connStrBuilder.DataSource; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.DataSource))
                    _connStrBuilder.DataSource = value;

                _connStrBuilder.Encrypt = SqlConnectionEncryptOption.Optional;
            }
        }

        /// <summary>
        /// Gets the list of available SQL Server instances on the network.
        /// </summary>
        /// <value>An array of available SQL Server instance names.</value>
        public string[] Servers
        {
            get
            {
                _servers ??= LoadServers();
                return [.. _servers];
            }
            set { }
        }

        /// <summary>
        /// Load the list of available SQL Server instances on the network.
        /// </summary>
        /// <returns>A list of available SQL Server instance names.</returns>
        private List<string> LoadServers()
        {
            try
            {
                // Retrieve enumerator instance and then the data
                SqlDataSourceEnumerator instance = SqlDataSourceEnumerator.Instance;
                DataTable table = instance.GetDataSources();
                List<string> serverList = [];

                // Display contents of table
                foreach (DataRow row in table.Rows)
                    serverList.Add(row["ServerName"].ToString() + @"\" + row["InstanceName"].ToString());

                return serverList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("SQL Server responded with an error: " + ex.Message,
                    "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
        }

        #endregion Server

        #region Authentication

        /// <summary>
        /// Gets or sets a value indicating whether Windows Authentication is used for the
        /// connection.
        /// </summary>
        /// <value>True if Windows Authentication is used; otherwise, false.</value>
        public bool WindowsAuthentication
        {
            get { return _connStrBuilder.IntegratedSecurity; }
            set
            {
                _connStrBuilder.IntegratedSecurity = value;
                OnPropertyChanged(nameof(SQLServerAuthentication));
                OnPropertyChanged(nameof(WindowsAuthentication));
                OnPropertyChanged(nameof(Username));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether SQL Server Authentication is used for the connection.
        /// </summary>
        /// <value>True if SQL Server Authentication is used; otherwise, false.</value>
        public bool SQLServerAuthentication
        {
            get { return !_connStrBuilder.IntegratedSecurity; }
            set
            {
                _connStrBuilder.IntegratedSecurity = !value;
                OnPropertyChanged(nameof(SQLServerAuthentication));
                OnPropertyChanged(nameof(WindowsAuthentication));
                OnPropertyChanged(nameof(Username));
            }
        }

        /// <summary>
        /// Gets or sets the username for SQL Server Authentication.
        /// </summary>
        /// <value>The username for SQL Server Authentication.</value>
        public string Username
        {
            get { return _connStrBuilder.UserID; }
            set { if (value != _connStrBuilder.UserID) _connStrBuilder.UserID = value; }
        }

        /// <summary>
        /// Gets or sets the password for SQL Server Authentication.
        /// </summary>
        /// <value>The password for SQL Server Authentication.</value>
        public string Password
        {
            get { return _connStrBuilder.Password; }
            set { if (value != _connStrBuilder.Password) _connStrBuilder.Password = value; }
        }

        #endregion Authentication

        #region Database

        /// <summary>
        /// Gets or sets the database name for the connection.
        /// </summary>
        /// <value>The database name for the connection.</value>
        public string Database
        {
            get { return _connStrBuilder.InitialCatalog; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.InitialCatalog))
                {
                    _connStrBuilder.InitialCatalog = value;
                    LoadSchemata();
                }
            }
        }

        /// <summary>
        /// Gets the list of available databases on the selected SQL Server instance.
        /// </summary>
        /// <value>An array of available database names on the selected SQL Server instance.</value>
        public string[] Databases
        {
            get
            {
                return [.. _databases];
            }
            set { }
        }

        /// <summary>
        /// Load the list of available databases on the selected SQL Server instance.
        /// </summary>
        private void LoadDatabases()
        {
            try
            {
                if (_connStrBuilder != null)
                {
                    SqlConnection cn = new(_connStrBuilder.ConnectionString);

                    List<String> DatabaseList = [];
                    cn.Open();

                    DataTable dbTable = cn.GetSchema("Databases");
                    _databases = [.. (from r in dbTable.AsEnumerable()
                                  let tableName = r.Field<string>("database_name")
                                  select tableName).OrderBy(t => t)];

                    OnPropertyChanged(nameof(Databases));
                }
            }
            catch (Exception ex)
            {
                _databases = [];
                MessageBox.Show("SQL Server responded with an error: " + ex.Message,
                    "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion Database

        #region Default Schema

        /// <summary>
        /// Gets or sets the list of available schemata for the selected database
        /// </summary>
        /// <value>An array of available schema names for the selected database.</value>
        public string[] Schemata
        {
            get { return [.. _schemata]; }
            set { }
        }

        /// <summary>
        /// Gets or sets the default schema for the connection.
        /// </summary>
        /// <value>The default schema for the connection.</value>
        public string DefaultSchema
        {
            get { return _defaultSchema; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _defaultSchema))
                    _defaultSchema = value;
            }
        }

        /// <summary>
        /// Load the list of available schemata for the selected database. The default schema is set
        /// to the first schema in the list if there is only one, otherwise it is set to the default
        /// schema for SQL Server (dbo).
        /// </summary>
        private void LoadSchemata()
        {
            List<String> schemaList = [];
            SqlConnection cn = null;

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.InitialCatalog))
                {
                    cn = new SqlConnection(_connStrBuilder.ConnectionString);
                    cn.Open();

                    DataTable dbTable = cn.GetSchema("Users");
                    schemaList = [.. (from r in dbTable.AsEnumerable()
                                  let schemaName = r.Field<string>("user_name")
                                  select schemaName).OrderBy(s => s)];
                    _defaultSchema = DbBase.GetDefaultSchema(Backends.SqlServer, _connStrBuilder, schemaList);
                }
            }
            catch (Exception ex)
            {
                if (ex is SqlException)
                    MessageBox.Show(ex.Message, "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show("SQL Server responded with an error: " + ex.Message,
                    "SQL Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if ((cn != null) && (cn.State != ConnectionState.Closed)) cn.Close();

                _schemata = schemaList;
                OnPropertyChanged(nameof(Schemata));

                if (_schemata.Count == 1) _defaultSchema = _schemata[0];
                OnPropertyChanged(nameof(DefaultSchema));
            }
        }

        #endregion Default Schema

        #region View Events

        /// <summary>
        /// View events are raised by the view when certain events occur, such as when a field is
        /// changed or when the dialog is loaded. The view passes the window handle and the name of
        /// the property that has changed to the view model, which can then take appropriate action,
        /// such as loading the list of databases when the server name is changed.
        /// </summary>
        /// <param name="windowHandle"></param>
        /// <param name="propertyName"></param>
        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            //if (windowHandle != IntPtr.Zero) _windowHandle = windowHandle;

            switch (propertyName)
            {
                case "Database":
                    LoadDatabases();
                    break;
            }
        }

        #endregion View Events

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object.
        /// </summary>
        /// <value>An error message indicating what is wrong with this object; otherwise, null or empty string.</value>
        public string Error
        {
            get
            {
                string error = null;

                if (String.IsNullOrEmpty(_connStrBuilder.DataSource) || String.IsNullOrEmpty(_connStrBuilder.InitialCatalog))
                    error = "Error: You must provide at least server name and database";


                if (!_connStrBuilder.IntegratedSecurity && String.IsNullOrEmpty(_connStrBuilder.UserID))
                    error = "Error: You must provide user id (and usually password) if using SQL Server authentication";

                return error;
            }
        }

        /// <summary>
        /// Gets the error message for the property with the given name. This is used to provide
        /// error messages for individual properties, such as when a required field is left empty or
        /// when an invalid value is entered.
        /// </summary>
        /// <param name="columnName"></param>
        /// <value>The error message for the property with the given name; otherwise, null or empty string.</value>
        public string this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "Server":
                        if (String.IsNullOrEmpty(_connStrBuilder.DataSource))
                            error = "Error: You must choose a server";
                        break;
                    case "Username":
                        if ((!_connStrBuilder.IntegratedSecurity) && (String.IsNullOrEmpty(_connStrBuilder.UserID)))
                            error = "Error: You must provide a user id";
                        break;
                    case "Database":
                        if (String.IsNullOrEmpty(_connStrBuilder.InitialCatalog))
                            error = "Error: You must choose a database";
                        break;
                    case "DefaultSchema":
                        if (String.IsNullOrEmpty(_defaultSchema))
                            error = "Error: You must choose a default schema";
                        break;
                }

                // dirty commands registered with CommandManager so they are queried to see if they can execute now
                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        #endregion IDataErrorInfo Members
    }
}