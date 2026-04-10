// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2019 London & South East Record Centres (LaSER)
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
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the data and commands for the About window.
    /// </summary>
    internal class ViewModelWindowAbout : ViewModelBase
    {
        #region Fields

        private RelayCommand _okCommand;
        private string _displayName = "About HLU Tool";
        private string _appVersion;
        private string _dbVersion;
        private string _dataVersion;
        private string _connectionType;
        private string _connectionSettings;
        private string _userId;
        private string _userName;
        private string _copyright;
        private string _userGuideURL;
        private string _userGuideText;
        private string _technicalGuideURL;
        private string _technicalGuideText;

        #endregion Fields

        #region ViewModelBase Members

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

        public override string WindowTitle
        {
            get
            {
                return DisplayName;
            }
        }

        #endregion ViewModelBase Members

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler();

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command to close the About window when the Ok button is clicked.
        /// </summary>
        /// <value>The command to execute when the Ok button is clicked.</value>
        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    Action<object> okAction = new(this.Ok);
                    _okCommand = new(okAction);
                }
                return _okCommand;
            }
        }

        /// <summary>
        /// Handles event when Ok button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void Ok(object param)
        {
            RequestClose?.Invoke();
        }

        #endregion Ok Command

        #region Control Properties

        public string AppVersion
        {
            get
            {
                return _appVersion;
            }
            set
            {
                _appVersion = value;
            }
        }

        public string DbVersion
        {
            get
            {
                return _dbVersion;
            }
            set
            {
                _dbVersion = value;
            }
        }

        public string DataVersion
        {
            get
            {
                return _dataVersion;
            }
            set
            {
                _dataVersion = value;
            }
        }

        public string ConnectionType
        {
            get
            {
                return _connectionType;
            }
            set
            {
                _connectionType = value;
            }
        }

        public string ConnectionSettings
        {
            get
            {
                return _connectionSettings;
            }
            set
            {
                _connectionSettings = value;
            }
        }

        public string UserId
        {
            get
            {
                return _userId;
            }
            set
            {
                _userId = value;
            }
        }

        public string UserName
        {
            get
            {
                return _userName;
            }
            set
            {
                _userName = value;
            }
        }

        public string Copyright
        {
            get
            {
                return _copyright;
            }
            set
            {
                _copyright = value;
            }
        }

        public string UserGuideURL
        {
            get
            {
                return _userGuideURL;
            }
            set
            {
                _userGuideURL = value;
            }
        }

        public string UserGuideText
        {
            get
            {
                return _userGuideText;
            }
            set
            {
                _userGuideText = value;
            }
        }

        public string TechnicalGuideURL
        {
            get
            {
                return _technicalGuideURL;
            }
            set
            {
                _technicalGuideURL = value;
            }
        }

        public string TechnicalGuideText
        {
            get
            {
                return _technicalGuideText;
            }
            set
            {
                _technicalGuideText = value;
            }
        }

        #endregion Control Properties
    }
}