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

using ActiproSoftware.Windows.Controls;
using HLU.Enums;
using HLU.Properties;
using HLU.UI.View.Connection;
using HLU.UI.ViewModel;
using System;
using System.Windows;
using ArcGIS.Desktop.Framework;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.Data.Connection
{
    /// <summary>
    /// Factory class to create database connections based on user settings. If settings are
    /// incomplete or invalid, prompts user to select connection type and enter connection details.
    /// </summary>
    class DbFactory
    {
        #region Fields

        private static ViewSelectConnection _selConnWindow;
        private static ViewModelSelectConnection _selConnViewModel;
        private static ConnectionTypes _connType;
        private static Backends _backend;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the current connection type. This is set when CreateConnection is called, either
        /// from settings or user input.
        /// </summary>
        /// <value>The current connection type.</value>
        public static ConnectionTypes ConnectionType
        {
            get { return _connType; }
        }

        /// <summary>
        /// Gets the current database backend. This is determined from the connection string and
        /// type when CreateConnection is called.
        /// </summary>
        /// <value>The current database backend.</value>
        public static Backends Backend
        {
            get { return _backend; }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Creates a database connection based on user settings. If settings are incomplete or
        /// invalid, prompts user to select connection type and enter connection details.
        /// </summary>
        /// <param name="dbConnectionTimeout">The timeout value for the database connection.</param>
        /// <returns>A database connection object.</returns>
        public static DbBase CreateConnection(int dbConnectionTimeout)
        {
            if (Enum.IsDefined(typeof(ConnectionTypes), Settings.Default.DbConnectionType))
                _connType = (ConnectionTypes)Settings.Default.DbConnectionType;
            else
                _connType = ConnectionTypes.Unknown;

            string connString = Settings.Default.DbConnectionString;
            string defaultSchema = Settings.Default.DbDefaultSchema;
            bool promptPwd = Settings.Default.DbPromptPwd;

            if ((_connType == ConnectionTypes.Unknown) || String.IsNullOrEmpty(connString) ||
                ((DbBase.GetBackend(connString, _connType) != Backends.Access) && String.IsNullOrEmpty(defaultSchema)))
            {
                promptPwd = false;
                SelectConnectionType();
            }

            if (_connType == ConnectionTypes.Unknown) return null;

            DbBase db = null;

            switch (_connType)
            {
                case ConnectionTypes.ODBC:
                    db = new DbOdbc(ref connString, ref defaultSchema, ref promptPwd,
                        Settings.Default.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone,
                        Settings.Default.DbTextLength, Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale, dbConnectionTimeout);
                    break;
                case ConnectionTypes.Oracle:
                    db = new DbOracle(ref connString, ref defaultSchema, ref promptPwd,
                        Settings.Default.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale, dbConnectionTimeout);
                    break;
                case ConnectionTypes.PostgreSQL:
                    db = new DbPgSql(ref connString, ref defaultSchema, ref promptPwd,
                        Settings.Default.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale, dbConnectionTimeout);
                    break;
                case ConnectionTypes.SQLServer:
                    db = new DbSqlServer(ref connString, ref defaultSchema, ref promptPwd,
                        Settings.Default.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                        true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, Settings.Default.DbTextLength,
                        Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                        Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale, dbConnectionTimeout);
                    break;
            }

            _backend = DbBase.GetBackend(connString, _connType);

            if (db != null)
            {
                Settings.Default.DbConnectionType = (int)_connType;
                Settings.Default.DbConnectionString = connString;
                Settings.Default.DbDefaultSchema = defaultSchema;
                Settings.Default.DbPromptPwd = promptPwd;
                Settings.Default.Save();
            }

            return db;
        }

        /// <summary>
        /// Clears the database connection settings. This can be used to reset the connection information
        /// to its default state.
        /// </summary>
        /// <returns><c>true</c> if the settings were successfully cleared; otherwise, <c>false</c>.</returns>
        public static bool ClearSettings()
        {
            try
            {
                Settings.Default.DbConnectionType = (int)ConnectionTypes.Unknown;
                Settings.Default.DbConnectionString = String.Empty;
                Settings.Default.DbDefaultSchema = string.Empty;
                Settings.Default.DbPromptPwd = true;
                Settings.Default.Save();

                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Prompts the user to select a database connection type and enter connection details if necessary.
        /// </summary>
        /// <returns>The selected database connection type.</returns>
        private static ConnectionTypes SelectConnectionType()
        {
            try
            {
                // Create window
                _selConnWindow = new()
                {
                    // Set ArcGIS Pro as the parent
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // create ViewModel to which main window binds
                _selConnViewModel = new()
                {
                    DisplayName = "Connection Type"
                };

                // when ViewModel asks to be closed, close window
                _selConnViewModel.RequestClose -= SelConnViewModel_RequestClose; // Safety: avoid double subscription.
                _selConnViewModel.RequestClose +=
                    new ViewModelSelectConnection.RequestCloseEventHandler(SelConnViewModel_RequestClose);

                // allow all controls in window to bind to ViewModel by setting DataContext
                _selConnWindow.DataContext = _selConnViewModel;

                // show window
                _selConnWindow.ShowDialog();

                return _connType;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return ConnectionTypes.Unknown;
        }

        /// <summary>
        /// Handles the RequestClose event from the ViewModel. Closes the selection window and sets the
        /// selected connection type.
        /// </summary>
        /// <param name="connType">The selected connection type.</param>
        /// <param name="errorMsg">An error message, if any, to display to the user.</param>
        private static void SelConnViewModel_RequestClose(ConnectionTypes connType, string errorMsg)
        {
            _selConnViewModel.RequestClose -= SelConnViewModel_RequestClose;
            _selConnWindow.Close();

            if (!String.IsNullOrEmpty(errorMsg))
                MessageBox.Show(errorMsg, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);

            _connType = connType;
        }

        #endregion Methods
    }
}