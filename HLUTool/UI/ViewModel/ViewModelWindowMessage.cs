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
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for a message window.
    /// </summary>
    internal class ViewModelWindowMessage : ViewModelBase
    {
        #region Fields

        private RelayCommand _okCommand;
        private string _messageHeader;
        private string _messageText;

        #endregion Fields

        #region Window Title

        public override string DisplayName
        {
            get
            {
                return _messageHeader;
            }
            set
            {
                _messageHeader = value;
            }
        }

        public override string WindowTitle
        {
            get
            {
                return DisplayName;
            }
        }

        #endregion Window Title

        #region RequestClose

        public EventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command to close the message window when the Ok button is clicked.
        /// </summary>
        /// <value>The command to close the message window.</value>
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
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        #endregion Ok Command

        #region Message

        public string MessageText
        {
            get
            {
                return _messageText;
            }
            set
            {
                _messageText = value;
            }
        }

        public string MessageHeader
        {
            get
            {
                return _messageHeader;
            }
            set
            {
                _messageHeader = value;
            }
        }

        #endregion Message
    }
}