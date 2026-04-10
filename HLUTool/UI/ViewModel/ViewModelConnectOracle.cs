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

using HLU.Data.Connection;
using HLU.Enums;
using HLU.Properties;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// ViewModel for the Connect Oracle dialog. Builds an Oracle connection string based on user
    /// input and tests the connection when the Ok button is clicked. If the connection is
    /// successful then the connection string and default schema are passed back to the caller via
    /// the RequestClose event.
    /// </summary>
    internal class ViewModelConnectOracle : ViewModelBase, IDataErrorInfo
    {
        internal enum DBAPrivilege
        {
            Normal, SYSDBA, SYSOPER
        }

        #region private Members

        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private Dictionary<string, string> _dataSourcesDic;
        private string[] _dataSources;
        private List<String> _schemata = [];
        private string _defaultSchema;
        private OracleConnectionStringBuilder _connStrBuilder;

        #endregion private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelConnectOracle"/> class.
        /// </summary>
        public ViewModelConnectOracle()
        {
            _connStrBuilder = [];
        }

        #endregion Constructor

        #region Connection String Builder

        /// <summary>
        /// Gets the OracleConnectionStringBuilder which is used to build the connection string
        /// based on user input. The connection string is tested when the Ok button is clicked and
        /// if successful is passed back to the caller via the RequestClose event.
        /// </summary>
        /// <value>The OracleConnectionStringBuilder used to build the connection string.</value>
        public OracleConnectionStringBuilder ConnectionStringBuilder
        {
            get
            {
                return _connStrBuilder;
            }
        }

        #endregion Connection String Builder

        #region Display Name

        /// <summary>
        /// Gets or sets the display name for this ViewModel. This is used as the title of the dialog.
        /// </summary>
        /// <value>The display name.</value>
        public override string DisplayName
        {
            get
            {
                return _displayName;
            }
            set
            {
                _displayName = value;
            }
        }

        #endregion Display Name

        #region Window Title

        /// <summary>
        /// Gets the window title for this ViewModel. This is used as the title of the dialog and is set to the same value as DisplayName.
        /// </summary>
        /// <value>The window title.</value>
        public override string WindowTitle
        {
            get
            {
                return DisplayName;
            }
        }

        #endregion Window Title

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string connString, string defaultSchema, string errMsg);

        // Declare the event that will be raised when the dialog should be closed. The event passes
        // the connection string, default schema and any error message back to the caller.
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Create Ok button command
        /// </summary>
        /// <value>The command for the Ok button.</value>
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
                _connStrBuilder.PersistSecurityInfo = Settings.Default.DbConnectionPersistSecurityInfo;

                using OracleConnection cn = new(_connStrBuilder.ConnectionString);

                cn.Open();
                // Close() is automatic when using disposes

                RequestClose?.Invoke(_connStrBuilder.ConnectionString, _defaultSchema, null);
            }
            catch (OracleException exOra)
            {
                MessageBox.Show("Oracle Server responded with an error:\n\n" + exOra.Message,
                    "Oracle Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determines whether the Ok button is enabled
        /// To be enabled the following must be true:
        /// server name and database must be set; if windows authentication is not set then a username and password are required.
        /// </summary>
        /// <value>True if the Ok button can be enabled; otherwise, false.</value>
        private bool CanOk
        {
            get
            {
                //return !((String.IsNullOrEmpty(_connStrBuilder.DataSource)) ||
                //    (!_connStrBuilder.IntegratedSecurity && String.IsNullOrEmpty(_connStrBuilder.UserID)) ||
                //    String.IsNullOrEmpty(_defaultSchema));
                return !((String.IsNullOrEmpty(_connStrBuilder.DataSource)) || String.IsNullOrEmpty(_connStrBuilder.UserID) ||
                    String.IsNullOrEmpty(_defaultSchema));
            }
        }

        #endregion Ok Command

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
            RequestClose?.Invoke(null, null, null);
        }

        #endregion Cancel Command

        #region View Events

        /// <summary>
        /// Handles events raised by the view. In this case the only event is when the DefaultSchema
        /// combo box is dropped down, at which point the list of schemata is loaded from the database.
        /// </summary>
        /// <param name="windowHandle">The handle of the window raising the event.</param>
        /// <param name="propertyName"></param>
        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            switch (propertyName)
            {
                case "DefaultSchema":
                    LoadSchemata();
                    break;
            }
        }

        /// <summary>
        /// Raises PropertyChanged for the validated properties so that WPF re-evaluates
        /// IDataErrorInfo and shows error adorners immediately when the window first opens
        /// with blank values.
        /// </summary>
        public void NotifyValidationOnLoad()
        {
            OnPropertyChanged(nameof(DataSource));
            OnPropertyChanged(nameof(UserID));
            OnPropertyChanged(nameof(DefaultSchema));
        }

        #endregion View Events

        #region Data Source

        /// <summary>
        /// Gets or sets the list of data sources. The list is retrieved using the OracleClientFactory's
        /// CreateDataSourceEnumerator method.
        /// </summary>
        /// <value>The list of data sources.</value>
        public string[] DataSources
        {
            get
            {
                if (_dataSources == null)
                {
                    OracleClientFactory factory = new();
                    if (factory.CanCreateDataSourceEnumerator)
                    {
                        System.Data.Common.DbDataSourceEnumerator dataSourceEnumarator = factory.CreateDataSourceEnumerator();
                        DataTable dt = dataSourceEnumarator.GetDataSources();
                        _dataSourcesDic = DbOracle.GetConnectionStrings(dt);
                        _dataSources = [.. _dataSourcesDic.Keys];
                        OnPropertyChanged(nameof(DataSources));
                    }
                    else
                    {
                        _dataSources = [];
                    }
                }
                return _dataSources;
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the data source. The data source is set by the user selecting a value from
        /// the list of data sources or by entering a value manually.
        /// </summary>
        /// <value>The data source.</value>
        public string DataSource
        {
            get
            {
                return _connStrBuilder.DataSource;
            }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.DataSource))
                    _connStrBuilder.DataSource = value;
            }
        }

        #endregion Data Source

        #region Authentication

        /// <summary>
        /// Gets or sets the user ID. The user ID is set by the user entering a value manually.
        /// </summary>
        /// <value>The user ID.</value>
        public string UserID
        {
            get
            {
                return _connStrBuilder.UserID;
            }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.UserID))
                {
                    _connStrBuilder.UserID = value;
                    if (String.IsNullOrEmpty(_defaultSchema) && (_schemata != null) && (_schemata.Count > 0))
                    {
                        _defaultSchema = _connStrBuilder.UserID;
                        OnPropertyChanged(nameof(DefaultSchema));
                    }
                    if (DbOracle.GetUserId(_connStrBuilder.UserID) == "SYS")
                    {
                        _connStrBuilder.DBAPrivilege = DBAPrivilege.SYSDBA.ToString();
                        OnPropertyChanged(nameof(DBAPrivilegeOption));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the list of DBA privilege options.
        /// </summary>
        /// <value>The list of DBA privilege options.</value>
        public DBAPrivilege[] DBAPrivilegeOptions
        {
            get
            {
                return (DBAPrivilege[])Enum.GetValues(typeof(DBAPrivilege));
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the DBA privilege option. The option is set by the user selecting a value from
        /// the list of DBA privilege options.
        /// </summary>
        /// <value>The DBA privilege option.</value>
        public DBAPrivilege DBAPrivilegeOption
        {
            get
            {
                if (String.IsNullOrEmpty(_connStrBuilder.DBAPrivilege))
                    return DBAPrivilege.Normal;
                object newValue = Enum.Parse(typeof(DBAPrivilege), _connStrBuilder.DBAPrivilege);
                if (newValue != null)
                    return (DBAPrivilege)newValue;
                else
                    return DBAPrivilege.Normal;
            }
            set
            {
                if (value == DBAPrivilege.Normal)
                    _connStrBuilder.DBAPrivilege = String.Empty;
                else
                    _connStrBuilder.DBAPrivilege = Enum.GetName(typeof(DBAPrivilege), value);
            }
        }

        /// <summary>
        /// Gets or sets the password. The password is set by the user entering a value manually.
        /// </summary>
        /// <value>The password.</value>
        public string Password
        {
            get
            {
                return _connStrBuilder.Password;
            }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.Password))
                    _connStrBuilder.Password = value;
            }
        }

        #endregion Authentication

        #region Default Schema

        /// <summary>
        /// Gets or sets the list of schemata. The list is loaded from the database when the user clicks
        /// </summary>
        /// <value>The list of schemata.</value>
        public string[] Schemata
        {
            get
            {
                return [.. _schemata];
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the default schema. The default schema is set by the user selecting a value from
        /// the list of schemata.
        /// </summary>
        /// <value>The default schema.</value>
        public string DefaultSchema
        {
            get
            {
                return _defaultSchema;
            }
            set
            {
                if (value != _defaultSchema)
                    _defaultSchema = value;
            }
        }

        /// <summary>
        /// Loads the list of schemata from the database. The list is retrieved by opening a connection to the database
        /// and executing a query to fetch all available schemata.
        /// </summary>
        private void LoadSchemata()
        {
            List<String> schemaList = [];
            OracleConnection cn = null;

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.DataSource))
                {
                    cn = new OracleConnection(_connStrBuilder.ConnectionString);
                    cn.Open();

                    OracleCommand cmd = cn.CreateCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT username FROM all_users";
                    OracleDataAdapter adapter = new(cmd);
                    DataTable dbTable = new();

                    try
                    {
                        adapter.Fill(dbTable);
                        schemaList = [.. (from r in dbTable.AsEnumerable()
                                      let schemaName = r.Field<string>("username")
                                      select schemaName).OrderBy(s => s)];
                        _defaultSchema = DbBase.GetDefaultSchema(Backends.Oracle, _connStrBuilder, schemaList);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                if (ex is OracleException)
                    MessageBox.Show(ex.Message, "Oracle Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if ((cn != null) && (cn.State != ConnectionState.Closed))
                    cn.Close();

                _schemata = schemaList;
                OnPropertyChanged(nameof(Schemata));

                if (_schemata.Count == 1)
                    _defaultSchema = _schemata[0];
                OnPropertyChanged(nameof(DefaultSchema));
            }
        }

        #endregion Default Schema

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object. The error message is
        /// generated based on which required properties have not been set by the user.
        /// </summary>
        /// <value>The error message indicating what is wrong with this object.</value>
        string IDataErrorInfo.Error
        {
            get
            {
                StringBuilder error = new();

                if (String.IsNullOrEmpty(_connStrBuilder.DataSource))
                    error.Append(", data source");
                //if (!_connStrBuilder.IntegratedSecurity && String.IsNullOrEmpty(_connStrBuilder.UserID))
                //    error.Append(", user ID");
                if (String.IsNullOrEmpty(_connStrBuilder.UserID))
                    error.Append(", user ID");
                if (String.IsNullOrEmpty(_defaultSchema))
                    error.Append(", default schema");

                if (error.Length > 0)
                    return error.Remove(0, 1).Insert(0, "Please provide a ").ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets an error message for the property with the given name. The error message is generated based on
        /// which required properties have not been set by the user.
        /// </summary>
        /// <param name="columnName">The name of the property for which to get the error message.</param>
        /// <returns>The error message for the specified property.</returns>
        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "DataSource":
                        if (String.IsNullOrEmpty(_connStrBuilder.DataSource))
                            error = "Error: You must provide a data source";
                        break;

                    case "UserID":
                        //if (!_connStrBuilder.IntegratedSecurity && String.IsNullOrEmpty(_connStrBuilder.UserID))
                        if (String.IsNullOrEmpty(_connStrBuilder.UserID))
                            error = "Error: You must provide a user ID";
                        break;

                    case "DefaultSchema":
                        if (String.IsNullOrEmpty(_defaultSchema))
                            error = "Error: You must provide a default schema";
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