// HLUTool is used to view and maintain habitat and land use GIS data.
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

using HLU.Properties;
using System;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the ViewModel for the Notify On Split/Merge window.
    /// </summary>
    internal class ViewModelWindowNotifyOnSplitMerge : ViewModelBase
    {
        #region Fields

        private string _displayName = "Split Merge";
        private string _msgText;
        private ICommand _okCommand;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialise the ViewModel for the Notify On Split/Merge window with the message to
        /// display to the user.
        /// </summary>
        /// <param name="msgText">The message to display to the user.</param>
        public ViewModelWindowNotifyOnSplitMerge(string msgText)
        {
            _msgText = msgText;
        }

        #endregion Constructor

        #region ViewModelBase members

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

        #endregion ViewModelBase members

        #region RequestClose

        public delegate void RequestCloseEventHandler();

        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command to execute when the Ok button is clicked. This saves the settings and
        /// raises the RequestClose event.
        /// </summary>
        /// <value>The command to execute when the Ok button is clicked.</value>
        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    Action<object> okAction = new(this.OkCommandClick);
                    _okCommand = new RelayCommand(okAction);
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
            Settings.Default.Save();
            RequestClose?.Invoke();
        }

        #endregion Ok Command

        #region Properties

        public string GroupBoxNotifyOnSplitMergeHeader
        {
            get
            {
                return "HLU Tool";
            }
            set
            {
            }
        }

        public string LabelMessage
        {
            get
            {
                return _msgText;
            }
            set
            {
            }
        }

        public bool DoNotTellAgain
        {
            get
            {
                return !Settings.Default.NotifyOnSplitMerge;
            }
            set
            {
                Settings.Default.NotifyOnSplitMerge = !value;
            }
        }

        #endregion Properties
    }
}