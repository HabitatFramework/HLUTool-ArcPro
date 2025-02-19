﻿// HLUTool is used to view and maintain habitat and land use GIS data.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HLU.Data.Connection;
using HLU.Properties;
using Microsoft.Win32;
//DONE: ADODB.Connection
//using MSDASC;
using Microsoft.Data.SqlClient;

namespace HLU.UI.ViewModel
{
    class ViewModelConnectOleDb : ViewModelBase, IDataErrorInfo
    {
        #region private Members

        private IntPtr _windowHandle;
        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private RelayCommand _editConnCommand;
        private RelayCommand _createConnCommand;
        private RelayCommand _browseConnCommand;
        private SqlConnection _connAdo;
        private Backends _backend = Backends.UndeterminedOleDb;
        private List<String> _schemata = [];
        private string _defaultSchema;

        private SqlConnectionStringBuilder _connStrBuilder;

        #endregion

        #region Constructor

        public ViewModelConnectOleDb()
        {
            _connStrBuilder = [];
        }

        #endregion

        #region Connection String Builder

        public SqlConnectionStringBuilder ConnectionStringBuilder { get { return _connStrBuilder; } }

        #endregion

        #region Display Name

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        #endregion

        #region Window Title

        public override string WindowTitle { get { return DisplayName; } }

        #endregion

        #region RequestClose

        // declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(string connString, string defaultSchema, string errorMsg);

        // declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion

        #region Ok Command

        /// <summary>
        /// Create Ok button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
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
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void OkCommandClick(object param)
        {
            OleDbConnection cn;

            try
            {
                _connStrBuilder.PersistSecurityInfo = Settings.Default.DbConnectionPersistSecurityInfo;

                cn = new OleDbConnection(_connStrBuilder.ConnectionString);

                cn.Open();
                cn.Close();

                if (DbOleDb.GetBackend(cn) == Backends.Access) _defaultSchema = String.Empty;

                this.RequestClose(_connStrBuilder.ConnectionString, _defaultSchema, null);
            }
            catch (OleDbException exOleDb)
            {
                MessageBox.Show("OleDb Server responded with an error:\n\n" + exOleDb.Message,
                     "OleDb Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { cn = null; }
        }

        /// <summary>
        /// Determines whether the Ok button is enabled
        /// To be enabled the following must be true:
        /// server name and database must be set; if windows authentication is not set then a username and password are required.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool CanOk
        {
            get
            {
                return !String.IsNullOrEmpty(_connStrBuilder.ConnectionString) &&
                    (IsMsAccess(_connAdo) || !String.IsNullOrEmpty(_defaultSchema));
            }
        }

        private bool IsMsAccess(SqlConnection connection)
        {
            if (connection == null)
                return false;
            else
                //DONE: ADODB.Connection
                //// Enable connection using Microsoft ACE driver.
                //connection.ConnectionString
                //return (connection.Provider.ToLower().StartsWith("microsoft.jet.oledb") ||
                //    (connection.Provider.ToLower().StartsWith("microsoft.ace.oledb.12.0")));
                return true;
        }

        //DONE: ADODB.Connection
        //private bool IsSqlServer(ADODB.Connection connection)
        //{
        //    if (connection == null)
        //        return false;
        //    else
        //        return connection.Provider.ToLower().StartsWith("sqloledb");
        //}

        #endregion

        #region Cancel Command

        /// <summary>
        /// Create Cancel button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
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
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CancelCommandClick(object param)
        {
            this.RequestClose(null, null, null);
        }

        #endregion

        #region Create Connection Command

        /// <summary>
        /// Create Create Connection button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand CreateConnCommand
        {
            get
            {
                if (_createConnCommand == null)
                {
                    Action<object> createConnAction = new(this.CreateConnCommandClick);
                    _createConnCommand = new(createConnAction);
                }

                return _createConnCommand;
            }
        }

        /// <summary>
        /// Handles event when Create Connection button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CreateConnCommandClick(object param)
        {
            DataLinks udl = new();
            udl.hWnd = _windowHandle != IntPtr.Zero ? _windowHandle.ToInt32() :
                new WindowInteropHelper(App.Current.MainWindow).Handle.ToInt32();
            _connAdo = udl.PromptNew() as ADODB.Connection;

            if ((_connAdo != null) && TestConnection(_connAdo.ConnectionString))
            {
                _connStrBuilder.ConnectionString = _connAdo.ConnectionString;
                OnPropertyChanged(nameof(ConnectionString));
                OnPropertyChanged(nameof(SupportsSchemata));
            }
        }

        #endregion

        #region Browse Connection Command

        /// <summary>
        /// Create Browse Connection button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand BrowseConnCommand
        {
            get
            {
                if (_browseConnCommand == null)
                {
                    Action<object> browseConnAction = new(this.BrowseConnCommandClick);
                    _browseConnCommand = new(browseConnAction);
                }

                return _browseConnCommand;
            }
        }

        /// <summary>
        /// Handles event when Browse Connection button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void BrowseConnCommandClick(object param)
        {
            OpenFileDialog openFileDlg = new()
            {
                Filter = "Microsoft Data Links (*.udl)|*.udl",
                Multiselect = false,
                RestoreDirectory = true
            };

            if (openFileDlg.ShowDialog() != true) return;

            string testString = "File Name = " + openFileDlg.FileName;
            if (TestConnection(testString))
            {
                _connStrBuilder.ConnectionString = testString;
                OnPropertyChanged(nameof(ConnectionString));
                OnPropertyChanged(nameof(SupportsSchemata));
            }
        }

        #endregion

        #region Edit Connection Command

        /// <summary>
        /// Create Edit Connection button command
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand EditConnCommand
        {
            get
            {
                if (_editConnCommand == null)
                {
                    Action<object> editConnAction = new(this.EditConnCommandClick);
                    _editConnCommand = new(editConnAction, param => this.CanEditConn);
                }

                return _editConnCommand;
            }
        }

        /// <summary>
        /// Handles event when Edit Connection button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void EditConnCommandClick(object param)
        {
            try
            {
                object adoDbConn = (object)_connAdo;

                DataLinks udl = new();
                udl.hWnd = _windowHandle != IntPtr.Zero ? _windowHandle.ToInt32() :
                    new WindowInteropHelper(App.Current.MainWindow).Handle.ToInt32();

                if (udl.PromptEdit(ref adoDbConn))
                {
                    if (TestConnection(_connAdo.ConnectionString))
                    {
                        _connStrBuilder.ConnectionString = _connAdo.ConnectionString;
                        OnPropertyChanged(nameof(ConnectionString));
                        OnPropertyChanged(nameof(SupportsSchemata));
                    }
                }
            }
            catch { _connAdo = null; }
        }

        /// <summary>
        /// Determines whether the Edit Connection button is enabled
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool CanEditConn { get { return _connAdo != null; } }

        #endregion

        #region Connection String

        public string ConnectionString
        {
            get { return HLU.Data.Connection.DbBase.MaskPassword(_connStrBuilder, Settings.Default.PasswordMaskString); }
            set { }
        }

        private bool TestConnection(string connectionString)
        {
            bool success = true;

            try
            {
                OleDbConnection cn = new(connectionString);
                cn.Open();
                cn.Close();

                _connAdo = new();
                _connAdo.ConnectionString = connectionString;
                //DONE: ADODB.Connection
                //_connAdo.Provider = cn.Provider;
            }
            catch (OleDbException exOleDb)
            {
                success = false;
                MessageBox.Show("OleDb Server responded with an error:\n\n" + exOleDb.Message,
                    "OleDb Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        #endregion

        #region Default Schema

        public bool SupportsSchemata
        {
            get
            {
                LoadSchemata();
                return !IsMsAccess(_connAdo);
            }
        }

        public string[] Schemata
        {
            get { return _schemata.ToArray(); }
            set { }
        }

        public string DefaultSchema
        {
            get { return _defaultSchema; }
            set { if (value != _defaultSchema) _defaultSchema = value; }
        }

        private void LoadSchemata()
        {
            List<String> schemaList = [];
            OleDbConnection cn = null;

            try
            {
                if ((_connStrBuilder != null) && !String.IsNullOrEmpty(_connStrBuilder.ConnectionString))
                {
                    cn = new OleDbConnection(_connStrBuilder.ConnectionString);
                    cn.Open();

                    _backend = DbOleDb.GetBackend(cn);

                    if (_backend != Backends.Access)
                    {
                        OleDbCommand cmd = cn.CreateCommand();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA" +
                                            " WHERE SCHEMA_NAME <> 'INFORMATION_SCHEMA'";
                        OleDbDataAdapter adapter = new(cmd);
                        DataTable dbTable = new();
                        try
                        {
                            adapter.Fill(dbTable);
                            schemaList = (from r in dbTable.AsEnumerable()
                                          let schemaName = r.Field<string>("SCHEMA_NAME")
                                          select schemaName).OrderBy(s => s).ToList();
                            _defaultSchema = DbBase.GetDefaultSchema(_backend, _connStrBuilder, schemaList);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _connStrBuilder.ConnectionString = String.Empty;
                if (ex is OleDbException)
                    MessageBox.Show(ex.Message, "OleDb Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion

        #region View Events

        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            if (windowHandle != IntPtr.Zero) _windowHandle = windowHandle;
        }

        #endregion

        #region IDataErrorInfo Members

        string IDataErrorInfo.Error
        {
            get
            {
                StringBuilder error = new();

                if (String.IsNullOrEmpty(_connStrBuilder.ConnectionString))
                    error.Append(", connection");

                // Enable connection using Microsoft ACE driver.
                if ((_connAdo != null) && !IsMsAccess(_connAdo) && 
                    String.IsNullOrEmpty(_defaultSchema)) error.Append(", default schema");

                if (error.Length > 0)
                    return error.Remove(0, 1).Insert(0, "Please provide").ToString();
                else
                    return null;
            }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "ConnectionString":
                        if (String.IsNullOrEmpty(_connStrBuilder.ConnectionString))
                            error = "Error: You must create a connection";
                        break;
                    case "DefaultSchema":
                        // Enable connection using Microsoft ACE driver.
                        if ((_connAdo != null) && !IsMsAccess(_connAdo) &&
                            String.IsNullOrEmpty(_defaultSchema)) error = "Error: You must provide a default schema";
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
