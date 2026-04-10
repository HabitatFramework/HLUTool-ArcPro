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
using System.Windows;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    internal class ViewModelPassword : ViewModelBase, IDataErrorInfo
    {
        #region private Members

        private string _displayName;
        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private string _userLabel;
        private string _userText;
        private string _password;

        #endregion private Members

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelPassword class.
        /// </summary>
        public ViewModelPassword()
        {
        }

        #endregion Constructor

        #region Display Name

        /// <summary>
        /// Gets or sets the display name of this view model, which is used as the title of the dialog.
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
        /// Gets the title of the window, which is the same as the display name.
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
        public delegate void RequestCloseEventHandler(string password, string errorMsg);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the Ok button command
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
            try
            {
                RequestClose?.Invoke(_password, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Ok button can be clicked. The Ok button is enabled only when a password is provided.
        /// </summary>
        /// <value>Indicates whether the Ok button can be clicked.</value>
        private bool CanOk
        {
            get
            {
                return !String.IsNullOrEmpty(_password);
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the Cancel button command
        /// </summary>
        /// <value>The command to execute when the cancel button is clicked.</value>
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
            RequestClose?.Invoke(null, null);
        }

        #endregion Cancel Command

        #region User

        /// <summary>
        /// Gets or sets the user label, which is used to prompt the user for input. For example, it
        /// can be "Please enter your password:".
        /// </summary>
        /// <value>The user label.</value>
        public string UserLabel
        {
            get
            {
                return _userLabel;
            }
            set
            {
                _userLabel = value;
            }
        }

        /// <summary>
        /// Gets or sets the user text, which is the text entered by the user. For example, it can
        /// be the password entered by the user.
        /// </summary>
        /// <value>The user text.</value>
        public string UserText
        {
            get
            {
                return _userText;
            }
            set
            {
                _userText = value;
            }
        }

        #endregion User

        #region Password

        /// <summary>
        /// Gets or sets the password entered by the user. This property is used to store the
        /// password and is not directly bound to the UI. The UserText property can be used for data
        /// binding to the UI, and the Password property can be set in the code-behind when the user
        /// clicks the Ok button.
        /// </summary>
        /// <value>The password entered by the user.</value>
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
            }
        }

        #endregion Password

        #region IDataErrorInfo Members

        string IDataErrorInfo.Error
        {
            get
            {
                if (String.IsNullOrEmpty(_password))
                    return "Please provide a password";
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
                    case "Password":
                        if (String.IsNullOrEmpty(_password))
                            error = "Error: You must provide a password";
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