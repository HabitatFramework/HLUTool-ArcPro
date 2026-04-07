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
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using HLU.Data.Connection;
using HLU.Enums;
using HLU.Properties;

namespace HLU.UI.ViewModel
{
    class ViewModelSelectConnection : ViewModelBase, IDataErrorInfo
    {
        #region private Members

        private IntPtr _windowHandle;
        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private ConnectionTypes[] _connectionTypes;
        private ConnectionTypes _connectionType;

        #endregion

        #region Constructor

        /// <summary>
        /// Initialise the view model, setting the list of connection types and default connection type from settings.
        /// </summary>
        public ViewModelSelectConnection()
        {
            // Get all values from the ConnectionTypes enum, cast them to ConnectionTypes, and
            // filter out the Unknown type to populate the list of available connection types.
            _connectionTypes = [.. Enum.GetValues(typeof(ConnectionTypes)).Cast<ConnectionTypes>().Where(t => t != ConnectionTypes.Unknown)];

            // Get the default connection type from settings and attempt to parse it as a
            // ConnectionTypes enum value, ignoring case. If parsing fails, initVal will be null.
            object initVal = Enum.Parse(typeof(ConnectionTypes), Settings.Default.DefaultConnectionType, true);

            // If the default connection type from settings is valid, set it as the initial
            // connection type; otherwise, it will remain Unknown.
            if (initVal != null) _connectionType = (ConnectionTypes)initVal;
        }

        #endregion Constructor

        #region Display Name

        /// <summary>
        /// Gets or sets the display name of the view model, which is used as the window title.
        /// </summary>
        /// <value>The display name.</value>
        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        #endregion Display Name

        #region Window Title

        /// <summary>
        /// Gets the window title, which is the same as the display name.
        /// </summary>
        /// <value>The window title.</value>
        public override string WindowTitle { get { return DisplayName; } }

        #endregion Window Title

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(ConnectionTypes connType, string errorMsg);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the Ok button command, which when executed will trigger the RequestClose event with
        /// the selected connection type if it is valid.
        /// </summary>
        /// <value>The Ok command.</value>
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
        /// <param name="param">The command parameter.</param>
        private void OkCommandClick(object param)
        {
            RequestClose?.Invoke(_connectionType, null);
        }

        /// <summary>
        /// Gets a value indicating whether the Ok command can execute, which is true if a valid
        /// connection type is selected.
        /// </summary>
        /// <value><c>true</c> if the Ok command can execute; otherwise, <c>false</c>.</value>
        private bool CanOk { get { return _connectionType != ConnectionTypes.Unknown; } }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the Cancel button command, which when executed will trigger the RequestClose event
        /// with an unknown connection type to indicate cancellation.
        /// </summary>
        /// <value>The Cancel command.</value>
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
        /// <param name="param">The command parameter.</param>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(ConnectionTypes.Unknown, null);
        }

        #endregion Cancel Command

        #region Connection Types

        /// <summary>
        /// Gets the list of available connection types, which is populated from the ConnectionTypes
        /// enum excluding the Unknown type.
        /// </summary>
        /// <value>The connection types.</value>
        public ConnectionTypes[] AvailableConnectionTypes
        {
            get { return _connectionTypes; }
            set { }
        }

        /// <summary>
        /// Gets or sets the selected connection type, which is used to determine if the Ok command
        /// can execute and is passed to the RequestClose event when Ok is clicked.
        /// </summary>
        /// <value>The selected connection type.</value>
        public ConnectionTypes ConnectionType
        {
            get { return _connectionType; }
            set { if (value != _connectionType) _connectionType = value; }
        }

        #endregion Connection Types

        #region View Events

        /// <summary>
        /// Handles events from the view, such as when a property changes. In this implementation,
        /// it listens for changes to the ConnectionType property, but currently does not perform
        /// any actions when it changes.
        /// </summary>
        /// <param name="windowHandle">The handle of the window that raised the event.</param>
        /// <param name="propertyName">The name of the property that changed.</param>
        public void ViewEvents(IntPtr windowHandle, string propertyName)
        {
            if (windowHandle != IntPtr.Zero) _windowHandle = windowHandle;

            switch (propertyName)
            {
                case "ConnectionType":
                    //LoadSchemata();
                    break;
            }
        }

        #endregion View Events

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object. In this implementation,
        /// it returns an error message if the selected connection type is unknown, prompting the
        /// user to choose a valid connection type before they can proceed. If a valid connection
        /// type is selected, it returns null, indicating no errors. This property is used by the
        /// view to display validation errors to the user.
        /// </summary>
        /// <value>The error message.</value>
        string IDataErrorInfo.Error
        {
            get
            {
                if (_connectionType == ConnectionTypes.Unknown)
                    return "Please choose a connection type";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets an error message for the property with the given name. In this implementation, it
        /// checks the ConnectionType property and returns an error message if it is unknown,
        /// prompting the user to choose a valid connection type. If the ConnectionType is valid, it
        /// returns null. This indexer is used by the view to display validation errors for specific properties.
        /// </summary>
        /// <param name="columnName">The name of the property to retrieve the error message for.</param>
        /// <returns>The error message for the specified property, or null if there is no error.</returns>
        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "ConnectionType":
                        if (_connectionType == ConnectionTypes.Unknown)
                            error = "Error: You must choose a connection type";
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