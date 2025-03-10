// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2019 London & South East Record Centres (LaSER)
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Text.RegularExpressions;
using HLU.Data;
using HLU.Data.Model;

namespace HLU.UI.ViewModel
{
    partial class ViewModelWindowQueryIncid : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;
        private string _displayName = "Query Incid";
        private String _queryIncid;

        #endregion

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        public override string WindowTitle
        {
            get { return DisplayName; }
        }

        #endregion

        #region RequestClose

        // declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(String queryIncid);

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
                    _okCommand = new RelayCommand(okAction, param => this.CanOk);
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
            this.RequestClose(QueryIncid);
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool CanOk
        {
            get
            {
                return (String.IsNullOrEmpty(Error) && (_queryIncid != null));
            }
        }

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
                    _cancelCommand = new RelayCommand(cancelAction);
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
            this.RequestClose(null);
        }

        #endregion

        #region Query Incid

        public string QueryIncid
        {
            get { return _queryIncid; }
            set { _queryIncid = value; }
        }

        #endregion

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                if ((!String.IsNullOrEmpty(QueryIncid)) && (!ValidIncidRegex().IsMatch(QueryIncid)))
                    return "Please enter a valid incid with format {nnnn:nnnnnnn}.";
                else return null;
            }
        }

        public string this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "QueryIncid":
                        if ((!String.IsNullOrEmpty(QueryIncid)) && (!ValidIncidRegex().IsMatch(QueryIncid)))
                            error = "Error: You must enter a valid incid with format {nnnn:nnnnnnn}.";
                        break;
                }

                // dirty commands registered with CommandManager so they are queried to see if they can execute now
                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches a valid incident identifier format.
        /// </summary>
        /// <remarks>
        /// - The pattern `[0-9]{4}:[0-9]{7}` matches:
        ///   - Exactly four numeric digits (`[0-9]{4}`).
        ///   - A colon character (`:`) as a separator.
        ///   - Exactly seven numeric digits (`[0-9]{7}`).
        /// - This format is commonly used for structured incident identifiers.
        /// - The `RegexOptions.IgnoreCase` flag is included but does not affect digit matching.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to validate incident identifiers.</returns>
        [GeneratedRegex(@"[0-9]{4}:[0-9]{7}", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex ValidIncidRegex();

        #endregion
    }
}
