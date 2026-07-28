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

using HLU.Enums;
using System;
using System.Globalization;
using System.Text;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the GIS selection warning dialog.
    /// </summary>
    internal class ViewModelWindowWarnOnGISSelect : ViewModelBase
    {
        #region Fields

        private string _displayName = "Select on Map";
        private int _gisFeaturesNum;
        private int _gisIncidNum;
        private string _gisFeaturesType;
        private ICommand _noCommand;
        private ICommand _yesCommand;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Stores details of the warning dialog to be displayed.
        /// </summary>
        /// <param name="numFeatures">The expected number of features to be selected in GIS (or -1 if unknown).</param>
        /// <param name="numIncids">The expected number of incids to be selected in GIS (or -1 if unknown).</param>
        /// <param name="typeFeatures">The type of features expected to be selected in GIS.</param>
        public ViewModelWindowWarnOnGISSelect(int numFeatures, int numIncids, HluGeometryTypes typeFeatures)
        {
            // Store the expected number of features to be selected in GIS.
            _gisFeaturesNum = numFeatures;

            // Store the expected number of incids to be selected in GIS.
            _gisIncidNum = numIncids;

            // If the type of feature is not known then just use 'feature'.
            if (typeFeatures != HluGeometryTypes.Unknown)
                _gisFeaturesType = typeFeatures.ToString().ToLower();
            else
                _gisFeaturesType = "feature";
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

        public delegate void RequestCloseEventHandler(bool proceed);

        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Yes Command

        /// <summary>
        /// Gets the command for the Yes button.
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
        /// Handles events when the Yes button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        /// <remarks></remarks>
        private void YesCommandClick(object param)
        {
            // Close the window.
            RequestClose?.Invoke(true);
        }

        #endregion Yes Command

        #region No Command

        /// <summary>
        /// Gets the command for the No button.
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
        /// Handles events when the No button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void NoCommandClick(object param)
        {
            RequestClose?.Invoke(false);
        }

        #endregion No Command

        #region Content Properties

        public string GroupBoxWarnOnGISSelectHeader
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
                // Display the expected number of features and/or the expected
                // number of incids to be selected in GIS, depending upon which
                // values are valid.
                StringBuilder labelMsg = new();
                if (_gisFeaturesNum < 0 && _gisIncidNum < 0)
                {
                    labelMsg.Append(String.Format("Could not determine the number of {0}{1} or incids expected to be " +
                        "selected on the map by this operation.",
                        _gisFeaturesType,
                        _gisFeaturesNum > 1 ? "s" : String.Empty));
                }
                else if (_gisFeaturesNum < 0)
                {
                    labelMsg.Append(String.Format("This operation is expected to select {0} incid{1} on the map.\n" +
                        "The expected number of {2}s could not be determined.",
                        String.Format(CultureInfo.CurrentCulture, "{0:N0}", _gisIncidNum),
                        _gisIncidNum > 1 ? "s" : String.Empty,
                        _gisFeaturesType));
                }
                else if (_gisIncidNum < 0)
                {
                    labelMsg.Append(String.Format("This operation is expected to select {0} {1}{2} on the map.\n" +
                        "The expected number of incids could not be determined.",
                        String.Format(CultureInfo.CurrentCulture, "{0:N0}", _gisFeaturesNum),
                        _gisFeaturesType,
                        _gisFeaturesNum > 1 ? "s" : String.Empty));
                }
                else
                {
                    labelMsg.Append(String.Format("This operation is expected to select {0} {1}{2} from {3} incid{4} on the map.",
                        String.Format(CultureInfo.CurrentCulture, "{0:N0}", _gisFeaturesNum),
                        _gisFeaturesType,
                        _gisFeaturesNum > 1 ? "s" : String.Empty,
                        String.Format(CultureInfo.CurrentCulture, "{0:N0}", _gisIncidNum),
                        _gisIncidNum > 1 ? "s" : String.Empty));
                }

                labelMsg.Append("\n\nWould you like to proceed?");

                return labelMsg.ToString();
            }
            set
            {
            }
        }

        #endregion Content Properties
    }
}