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
using System.Threading.Tasks;
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
        /// Gets the database connection settings, prompting the user if necessary.
        /// </summary>
        /// <returns><c>true</c> if valid settings are now stored; <c>false</c> if the user cancelled.</returns>
        public static async Task<bool> GetConnectionSettingsAsync()
        {
            // Read the stored connection type.
            if (Enum.IsDefined(typeof(ConnectionTypes), Settings.Default.DbConnectionType))
                _connType = (ConnectionTypes)Settings.Default.DbConnectionType;
            else
                _connType = ConnectionTypes.Unknown;

            // Get other stored settings that we can check for completeness before prompting the user.
            string connString = Settings.Default.DbConnectionString;
            string defaultSchema = Settings.Default.DbDefaultSchema;

            // If settings are complete, nothing to do.
            if (_connType != ConnectionTypes.Unknown &&
                !String.IsNullOrEmpty(connString) &&
                !String.IsNullOrEmpty(defaultSchema))
                return true;

            // Settings are incomplete — ask the user to choose a connection type.
            // SelectConnectionTypeAsync shows the window via Show() and completes the
            // TaskCompletionSource from the Closed event, so no thread is blocked.
            _connType = await SelectConnectionTypeAsync();

            if (_connType == ConnectionTypes.Unknown)
                return false;

            // Persist the chosen type so CreateConnectionAsync can read it.
            Settings.Default.DbConnectionType = (int)_connType;
            Settings.Default.DbPromptPwd = false;
            Settings.Default.Save();

            return true;
        }

        /// <summary>
        /// Creates a database connection from the settings already stored by
        /// <see cref="GetConnectionSettingsAsync"/>. Does not show any UI.
        /// </summary>
        /// <param name="dbConnectionTimeout">The timeout value for the database connection.</param>
        /// <returns>A task that resolves to a database connection object, or null if settings are missing.</returns>
        public static Task<DbBase> CreateConnectionAsync(int dbConnectionTimeout)
        {
            // Read connection type — must already be set by EnsureConnectionSettingsAsync.
            if (Enum.IsDefined(typeof(ConnectionTypes), Settings.Default.DbConnectionType))
                _connType = (ConnectionTypes)Settings.Default.DbConnectionType;
            else
                _connType = ConnectionTypes.Unknown;

            // If connection type is still unknown, return null — caller should have called
            // EnsureConnectionSettingsAsync first.
            if (_connType == ConnectionTypes.Unknown) return Task.FromResult<DbBase>(null);

            // Read remaining settings — these are already populated by EnsureConnectionSettingsAsync.
            string connString = Settings.Default.DbConnectionString;
            string defaultSchema = Settings.Default.DbDefaultSchema;
            bool promptPwd = Settings.Default.DbPromptPwd;

            DbBase db = null;

            // Create appropriate DbBase subclass based on selected connection type
            switch (_connType)
            {
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

            // Determine the backend based on the connection string and type
            _backend = DbBase.GetBackend(connString, _connType);

            // If a connection was successfully created, save the settings for next time
            if (db != null)
            {
                Settings.Default.DbConnectionType = (int)_connType;
                Settings.Default.DbConnectionString = connString;
                Settings.Default.DbDefaultSchema = defaultSchema;
                Settings.Default.DbPromptPwd = promptPwd;
                Settings.Default.Save();
            }

            // Return the created connection (or null if creation failed)
            return Task.FromResult(db);
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
                // Reset connection settings to defaults
                Settings.Default.DbConnectionType = (int)ConnectionTypes.Unknown;
                Settings.Default.DbConnectionString = String.Empty;
                Settings.Default.DbDefaultSchema = string.Empty;
                Settings.Default.DbPromptPwd = true;

                Settings.Default.ResetDbConnection = false;
                Settings.Default.Save();

                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Prompts the user to select a database connection type. Returns the selected type,
        /// or <see cref="ConnectionTypes.Unknown"/> if the user cancelled.
        /// </summary>
        private static Task<ConnectionTypes> SelectConnectionTypeAsync()
        {
            // TaskCompletionSource carries the result back to the awaiting caller without
            // blocking any thread. The dialog sets it when it closes.
            var tcs = new TaskCompletionSource<ConnectionTypes>(TaskCreationOptions.RunContinuationsAsynchronously);

            var dispatcher = System.Windows.Application.Current.Dispatcher;

            // Window creation and Show must happen on the UI thread.
            if (dispatcher.CheckAccess())
                ShowSelectionWindow(tcs);
            else
                dispatcher.BeginInvoke(() => ShowSelectionWindow(tcs));

            return tcs.Task;
        }

        /// <summary>
        /// Creates and shows the connection type selection window. Must be called on the UI thread.
        /// </summary>
        /// <param name="tcs">Completed with the chosen <see cref="ConnectionTypes"/> when the window closes.</param>
        private static void ShowSelectionWindow(TaskCompletionSource<ConnectionTypes> tcs)
        {
            try
            {
                // Create window
                _selConnWindow = new()
                {
                    Owner = FrameworkApplication.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Topmost = true
                };

                // Create ViewModel to which main window binds
                _selConnViewModel = new()
                {
                    DisplayName = "Connection Type"
                };

                // When ViewModel asks to be closed, close window
                _selConnViewModel.RequestClose -= SelConnViewModel_RequestClose;
                _selConnViewModel.RequestClose +=
                    new ViewModelSelectConnection.RequestCloseEventHandler(SelConnViewModel_RequestClose);

                // Complete the task when the window closes, regardless of how ProWindow
                // handles ShowDialog — this is the authoritative completion signal.
                _selConnWindow.Closed += (_, _) => tcs.TrySetResult(_connType);

                // Allow all controls in window to bind to ViewModel by setting DataContext
                _selConnWindow.DataContext = _selConnViewModel;

                _selConnWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);
                tcs.TrySetResult(ConnectionTypes.Unknown);
            }
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

            // Set _connType before Close() so that the Closed event handler
            // reads the correct value when it calls tcs.TrySetResult(_connType).
            _connType = connType;

            _selConnWindow.Close();

            if (!String.IsNullOrEmpty(errorMsg))
                MessageBox.Show(errorMsg, "Connection Type", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion Methods
    }
}