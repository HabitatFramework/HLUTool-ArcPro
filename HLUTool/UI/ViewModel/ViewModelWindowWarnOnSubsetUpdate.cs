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

using System;
using System.Globalization;
using System.Windows.Input;
using HLU.Properties;
using HLU.Data;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Cotnains the ViewModel for the WarnOnSubsetUpdate window.
    /// </summary>
    class ViewModelWindowWarnOnSubsetUpdate : ViewModelBase
    {
        #region Fields

        private string _displayName = "Attribute UpdateAsync";
        private int _numFrags;
        private int _numTotalFrags;
        private GeometryTypes _gisFeaturesType;
        private ICommand _yesCommand;
        private ICommand _noCommand;
        private ICommand _cancelCommand;
        private bool _makeDefaultReponse;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowWarnOnSubsetUpdate class.
        /// </summary>
        /// <param name="numFrags">The number of fragments.</param>
        /// <param name="numTotalFrags">The total number of fragments.</param>
        /// <param name="typeFeatures">The type of features.</param>
        public ViewModelWindowWarnOnSubsetUpdate(int numFrags, int numTotalFrags, GeometryTypes typeFeatures)
        {
            _numFrags = numFrags;
            _numTotalFrags = numTotalFrags;
            _gisFeaturesType = typeFeatures;
        }

        #endregion Constructor

        #region ViewModelBase members

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        public override string WindowTitle
        {
            get { return DisplayName; }
        }

        #endregion ViewModelBase members

        #region RequestClose

        public delegate void RequestCloseEventHandler(bool proceed, bool split, int? subsetUpdateAction);

        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Yes Command

        /// <summary>
        /// Gets the command for the Yes button
        /// </summary>
        /// <value>The command to execute when the Yes button is clicked.</value>
        public ICommand YesCommand
        {
            get
            {
                if (_yesCommand == null)
                {
                    Action<object> yesAction = new(this.YesCommandClick);
                    _yesCommand = new RelayCommand(yesAction);
                }

                return _yesCommand;
            }
        }

        /// <summary>
        /// Handles event when Yes button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void YesCommandClick(object param)
        {
            // Set the default value to 'Subset'.
            if (_makeDefaultReponse == true)
                RequestClose?.Invoke(true, true, 1);
            else
                RequestClose?.Invoke(true, true, null);
        }

        #endregion Yes Command

        #region No Command

        /// <summary>
        /// Gets the command for the No button
        /// </summary>
        /// <value>The command to execute when the No button is clicked.</value>
        public ICommand NoCommand
        {
            get
            {
                if (_noCommand == null)
                {
                    Action<object> noAction = new(this.NoCommandClick);
                    _noCommand = new RelayCommand(noAction);
                }

                return _noCommand;
            }
        }

        /// <summary>
        /// Handles event when No button is clicked
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void NoCommandClick(object param)
        {
            // Set the default value to 'All'.
            if (_makeDefaultReponse == true)
                RequestClose?.Invoke(true, true, 2);
            else
                RequestClose?.Invoke(true, true, null);
        }

        #endregion No Command

        #region Cancel Command

        /// <summary>
        /// Gets the command for the Cancel button
        /// </summary>
        /// <value>The command to execute when the Cancel button is clicked.</value>
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
        /// <param name="param">The parameter passed to the command.</param>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(false, false, null);
        }

        #endregion Cancel Command

        #region Content Properties

        public string GroupBoxWarnOnSubsetUpdateHeader
        {
            get { return String.Format("Attempting to update subset of Incid"); }
            set { }
        }

        public string LabelMessage
        {
            get
            {
                return String.Format("Only {0} out of {2} {1}s have been selected for this Incid.\n" +
                    "Would you like to logically split the selected {1}{3} before applying the update?\n\n" +
                    "Clicking 'No' will apply the update to all of the {1}s for this Incid?",
                    _numFrags.ToString(CultureInfo.CurrentCulture),
                    _gisFeaturesType.ToString().ToLower(),
                    _numTotalFrags.ToString(CultureInfo.CurrentCulture),
                    _numFrags > 0 ? "s" : String.Empty);
            }
            set { }
        }

        public bool MakeDefaultReponse
        {
            get { return _makeDefaultReponse; }
            set { _makeDefaultReponse = value; }
        }

        #endregion Content Properties
    }
}