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
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HLU.Enums;
using HLU.Data.Connection;
using Microsoft.Win32;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    class ViewModelConnectOdbc : ViewModelBase, IDataErrorInfo
    {
        #region Private Members

        private IntPtr _windowHandle;
        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private RelayCommand _manageDsnCommand;
        private string[] _dsnList;
        private bool _userDsn = true;
        private bool _systemDsn;
        private Backends _backend = Backends.UndeterminedOdbc;
        private string _defaultSchema;
        private List<String> _schemata;

        private OdbcConnectionStringBuilder _connStrBuilder;

        #endregion Private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelConnectOdbc class with default values for the
        /// connection string builder and display name.
        /// </summary>
        public ViewModelConnectOdbc()
        {
            _connStrBuilder = [];
        }

        #endregion Constructor

        #region Connection String Builder

        /// <summary>
        /// Gets the OdbcConnectionStringBuilder instance used to build the ODBC connection string based on user input.
        /// </summary>
        /// <value>The <see cref="OdbcConnectionStringBuilder"/> instance used to build the ODBC connection string.</value>
        public OdbcConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        #endregion Connection String Builder

        #region Display Name

        /// <summary>
        /// Gets or sets the display name for the ODBC connection dialog. This is the name that will
        /// be shown in the dialog title and other relevant UI elements.
        /// </summary>
        /// <value>The display name for the ODBC connection dialog.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        #endregion Display Name

        #region Window Title

        /// <summary>
        /// Gets the window title for the ODBC connection dialog. This is typically the same as the
        /// display name, but can be customized if needed.
        /// </summary>
        /// <value>The window title for the ODBC connection dialog.</value>
        public override string WindowTitle { get { return DisplayName; } }

        #endregion Window Title

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string connString, string defaultSchema, string errorMsg);

        // Declare the event to handle dialog closure, passing the connection string, default schema, and any error message back to the caller
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command to execute when the Ok button is clicked.
        /// </summary>
        /// <value>The command to execute when the Ok button is clicked.</value>
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
            OdbcConnection cn = null;

            try
            {
                cn = new OdbcConnection(_connStrBuilder.ConnectionString);

                cn.Open();
                cn.Close();

                if (DbOdbc.GetBackend(cn) == Backends.Access) _defaultSchema = String.Empty;

                RequestClose?.Invoke(_connStrBuilder.ConnectionString, _defaultSchema, null);
            }
            catch (OdbcException exOdbc)
            {
                MessageBox.Show("ODBC Server responded with an error:\n\n" + exOdbc.Message,
                     "ODBC Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { cn.Dispose(); }
        }

        /// <summary>
        /// Gets a value indicating whether the Ok button is enabled.
        /// To be enabled the following must be true:
        /// server name and database must be set; if windows authentication is not set then a username and password are required.
        /// </summary>
        /// <value><c>true</c> if the Ok button is enabled; otherwise, <c>false</c>.</value>
        private bool CanOk
        {
            get
            {
                return !String.IsNullOrEmpty(_connStrBuilder.ConnectionString) &&
                    ((DbOdbc.GetBackend(_connStrBuilder) == Backends.Access) || !String.IsNullOrEmpty(_defaultSchema));
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the command to execute when the Cancel button is clicked.
        /// </summary>
        /// <value>The command to execute when the Cancel button is clicked.</value>
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

        #region Manage DSN Command

        /// <summary>
        /// Gets the command to execute when the Manage DSN button is clicked.
        /// </summary>
        /// <value>The command to execute when the Manage DSN button is clicked.</value>
        public ICommand ManageDsnCommand
        {
            get
            {
                if (_manageDsnCommand == null)
                {
                    Action<object> manageDsnAction = new(this.ManageDsnCommandClick);
                    _manageDsnCommand = new(manageDsnAction);
                }

                return _manageDsnCommand;
            }
        }

        /// <summary>
        /// Handles event when Manage DSN button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void ManageDsnCommandClick(object param)
        {
            OdbcCP32 odbccp32 = new();
            odbccp32.ManageDatasources(_windowHandle);
            OnPropertyChanged(nameof(DsnList));
        }

        #endregion Manage DSN Command

        #region DSN

        /// <summary>
        /// Gets the list of available DSNs (Data Source Names) from the Windows registry based on
        /// whether the user has selected to use User DSNs or System DSNs. The list is retrieved
        /// from the appropriate registry key and returned as an array of strings.
        /// </summary>
        /// <value>An array of strings representing the available DSNs based on the selected type (User or System).</value>
        public string[] DsnList
        {
            get
            {
                RegistryKey rk = _userDsn ? Registry.CurrentUser : Registry.LocalMachine;
                RegistryKey sk = rk.OpenSubKey(@"SOFTWARE\ODBC\ODBC.INI\ODBC Data Sources");
                if (sk != null)
                    _dsnList = sk.GetValueNames();
                else
                    _dsnList = [];
                return _dsnList;
            }
            set { }
        }

        /// <summary>
        /// Gets or sets the selected DSN (Data Source Name) for the ODBC connection.
        /// </summary>
        /// <value>The selected DSN for the ODBC connection.</value>
        public string Dsn
        {
            get { return _connStrBuilder.Dsn; }
            set
            {
                if (!String.IsNullOrEmpty(value) && (value != _connStrBuilder.Dsn))
                {
                    _connStrBuilder.Dsn = value;
                    OnPropertyChanged(nameof(SupportsSchemata));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user has selected to use User DSNs.
        /// </summary>
        /// <value><c>true</c> if the user has selected to use User DSNs; otherwise, <c>false</c>.</value>
        public bool UserDsn
        {
            get { return _userDsn; }
            set
            {
                _userDsn = value;
                _systemDsn = !value;
                OnPropertyChanged(nameof(UserDsn));
                OnPropertyChanged(nameof(SystemDsn));
                OnPropertyChanged(nameof(DsnList));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user has selected to use System DSNs
        /// </summary>
        /// <value><c>true</c> if the user has selected to use System DSNs; otherwise, <c>false</c>.</value>
        public bool SystemDsn
        {
            get { return _systemDsn; }
            set
            {
                _systemDsn = value;
                _userDsn = !value;
                OnPropertyChanged(nameof(UserDsn));
                OnPropertyChanged(nameof(SystemDsn));
                OnPropertyChanged(nameof(DsnList));
            }
        }

        #endregion DSN

        #region Default Schema

        /// <summary>
        /// Gets a value indicating whether the selected ODBC backend supports the concept of
        /// schemata. If the backend is Access then this returns false and the schema selection
        /// controls are hidden.
        /// </summary>
        /// <value><c>true</c> if the selected ODBC backend supports schemata; otherwise, <c>false</c>.</value>
        public bool SupportsSchemata
        {
            get
            {
                LoadSchemata();
                return _backend != Backends.Access;
            }
        }

        /// <summary>
        /// Gets or sets the list of available schemata for the selected ODBC backend. The list is
        /// loaded when the DSN is selected and the backend is determined. If the backend does not
        /// support schemata, this list will be empty.
        /// </summary>
        /// <value>An array of strings representing the available schemata for the selected ODBC backend.</value>
        public string[] Schemata
        {
            get { return [.. _schemata]; }
            set { }
        }

        /// <summary>
        /// Gets or sets the selected default schema for the ODBC connection.
        /// </summary>
        /// <value>The selected default schema for the ODBC connection.</value>
        public string DefaultSchema
        {
            get { return _defaultSchema; }
            set { if (value != _defaultSchema) _defaultSchema = value; }
        }

        /// <summary>
        /// Load the list of available schemata for the selected ODBC backend.
        /// </summary>
        private void LoadSchemata()
        {
            List<String> schemaList = [];
            OdbcConnection cn = null;

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.ConnectionString))
                {
                    cn = new OdbcConnection(_connStrBuilder.ConnectionString);
                    cn.Open();

                    _backend = DbOdbc.GetBackend(cn);

                    if (_backend != Backends.Access)
                    {
                        OdbcCommand cmd = cn.CreateCommand();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = "SELECT SCHEMA_NAME FROM information_schema.schemata" +
                                            " WHERE SCHEMA_NAME <> 'INFORMATION_SCHEMA'";
                        OdbcDataAdapter adapter = new(cmd);
                        DataTable dbTable = new();
                        try
                        {
                            adapter.Fill(dbTable);
                            schemaList = [.. (from r in dbTable.AsEnumerable()
                                          let schemaName = r.Field<string>("SCHEMA_NAME")
                                          select schemaName).OrderBy(s => s)];
                            _defaultSchema = DbBase.GetDefaultSchema(_backend, _connStrBuilder, schemaList);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _connStrBuilder.Dsn = String.Empty;
                if (ex is OdbcException)
                    MessageBox.Show(ex.Message, "ODBC Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                MessageBox.Show(ex.Message, "HLU Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        /// View event handler to receive the window handle from the view when it is loaded.
        /// </summary>
        /// <param name="windowHandle">The handle of the window.</param>
        /// <param name="propertyName">The name of the property associated with the event.</param>
        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            if (windowHandle != IntPtr.Zero) _windowHandle = windowHandle;
        }

        /// <summary>
        /// Raises PropertyChanged for the validated properties so that WPF re-evaluates
        /// IDataErrorInfo and shows error adorners immediately when the window first opens
        /// with blank values.
        /// </summary>
        public void NotifyValidationOnLoad()
        {
            OnPropertyChanged(nameof(Dsn));
            OnPropertyChanged(nameof(DefaultSchema));
        }

        #endregion View Events

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object. The error message is
        /// based on the current state of the connection string builder and the selected backend. If
        /// the DSN is not set, it indicates that a data source must be chosen. If the backend
        /// supports schemata and the default schema is not set, it indicates that a default schema
        /// must be chosen. If there are no errors, it returns null.
        /// </summary>
        /// <value>An error message indicating what is wrong with this object, or null if there are no errors.</value>
        string IDataErrorInfo.Error
        {
            get
            {
                StringBuilder error = new();

                if (String.IsNullOrEmpty(_connStrBuilder.Dsn))
                    error.Append(", a data source");
                if ((_backend != Backends.Access) && String.IsNullOrEmpty(_defaultSchema))
                    error.Append(" , default schema");

                if (error.Length > 1)
                    return error.Remove(0, 1).Insert(0, "Please choose ").ToString();
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets an error message for the property with the given name.
        /// </summary>
        /// <value>An error message for the property with the given name, or null if there are no errors.</value>
        /// <param name="columnName">The name of the property for which to get the error message.</param>
        /// <returns>An error message for the property with the given name, or null if there are no errors.</returns>
        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "Dsn":
                        if (String.IsNullOrEmpty(_connStrBuilder.Dsn))
                            error = "Error: You must choose a data source";
                        break;
                    case "DefaultSchema":
                        if ((_backend != Backends.Access) && String.IsNullOrEmpty(_defaultSchema))
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