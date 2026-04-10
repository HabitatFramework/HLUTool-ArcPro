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

using HLU.Data.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the data and commands for the Export window.
    /// </summary>
    internal class ViewModelWindowExport : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;
        private string _displayName = "Export";
        private string _layerName;
        private HluDataSet.exportsDataTable _exportFormats;
        private int _exportID = -1;
        private bool _selectedOnly;
        private int _selectedNumber;
        private long _totalCount;
        private ExportOutputType _outputType = ExportOutputType.Shapefile;

        #endregion Fields

        #region Output Type Enum

        /// <summary>
        /// The GIS output format chosen by the user in the Export window.
        /// </summary>
        public enum ExportOutputType
        {
            Shapefile,
            FileGeodatabase
        }

        #endregion Output Type Enum

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowExport class with the specified
        /// selection count, layer name, and available export formats.
        /// </summary>
        /// <param name="numberSelected">
        /// The number of items selected for export. If greater than zero, only the selected items
        /// will be considered.
        /// </param>
        /// <param name="totalCount">
        /// The total number of items in the layer. Used to determine the scope of the export operation.
        /// </param>
        /// <param name="layerName">
        /// The name of the GIS layer associated with the export operation. Cannot be null.
        /// </param>
        /// <param name="exportFormats">
        /// A data table containing the available export formats. Must not be null and should
        /// contain at least one format.
        /// </param>
        public ViewModelWindowExport(int numberSelected, long totalCount, string layerName,
            HluDataSet.exportsDataTable exportFormats)
        {
            // Set the feature counts.
            _selectedNumber = numberSelected;
            _totalCount = totalCount;

            // Determine if only selected features should be exported based on the number of selected features.
            _selectedOnly = _selectedNumber > 0;

            // Set the layer name.
            _layerName = layerName;

            // Set the available export formats and automatically select
            // the format if only one is provided.
            _exportFormats = exportFormats;
            if (_exportFormats.Count == 1)
                _exportID = _exportFormats[0].export_id;
        }

        #endregion Constructor

        #region ViewModelBase Members

        /// <summary>
        /// Gets or sets the display name of the export window.
        /// </summary>
        /// <remarks>
        /// This property is used to set the title of the export window and provide context to the
        /// user about the purpose of the window.
        /// </remarks>
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

        /// <summary>
        /// Gets the title of the export window.
        /// </summary>
        /// <remarks>
        /// This property is used to set the window title when the export window is displayed.
        /// </remarks>
        public override string WindowTitle
        {
            get
            {
                return _displayName;
            }
        }

        #endregion ViewModelBase Members

        #region RequestClose

        // Declare the delegate
        public delegate void RequestCloseEventHandler(
            int exportID,
            bool selectedOnly,
            ExportOutputType outputType);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command to execute when the OK button is clicked.
        /// </summary>
        /// <value>The command to execute when the Ok button is clicked.</value>
        /// <remarks>
        /// This property is used to bind the Ok button in the export window to the command that
        /// handles the Ok operation.
        /// </remarks>
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
        /// Handles the OK button click: raises RequestClose with the selected export ID,
        /// selected-only flag, and output type.
        /// </summary>
        /// <param name="param">
        /// The parameter passed to the command. This parameter is not used in this method.
        /// </param>
        /// <remarks>
        /// This method is called when the Ok button is clicked. It raises the RequestClose event
        /// with the selected export ID and the selectedOnly parameter.
        /// </remarks>
        private void OkCommandClick(object param)
        {
            RequestClose?.Invoke(_exportID, _selectedOnly, _outputType);
        }

        /// <summary>
        /// Returns true when both an export format and an output type have been chosen.
        /// </summary>
        /// <returns>True if the Ok command can be executed; otherwise, false.</returns>
        /// <remarks>
        /// This property is used to determine if the Ok button should be enabled based on the
        /// selected export ID.
        /// </remarks>
        private bool CanOk
        {
            get
            {
                return _exportID != -1;
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the command to execute when the Cancel button is clicked.
        /// </summary>
        /// <value>The command to execute when the Cancel button is clicked.</value>
        /// <remarks>
        /// This property is used to bind the Cancel button in the export window to the command that
        /// handles the cancel operation.
        /// </remarks>
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
        /// Handles the Cancel button click.
        /// </summary>
        /// <param name="param">
        /// The parameter passed to the command. This parameter is not used in this method.
        /// </param>
        /// <remarks>
        /// This method is called when the Cancel button is clicked. It raises the RequestClose
        /// event with a negative export ID and a false value for the selectedOnly parameter.
        /// </remarks>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(-1, false, _outputType);
        }

        #endregion Cancel Command

        #region Control Properties

        /// <summary>
        /// Gets or sets the name of the GIS layer associated with the export operation.
        /// </summary>
        /// <remarks>
        /// This property is used to display the layer name in the export window and provide context
        /// for the use when selecting export options.
        /// </remarks>
        public string LayerName
        {
            get
            {
                return _layerName;
            }
            set
            {
                _layerName = value;
            }
        }

        /// <summary>
        /// Gets the data table containing the available export formats.
        /// </summary>
        /// <remarks>
        /// This property is used to populate the export format combo box in the export window.
        /// </remarks>
        public HluDataSet.exportsDataTable ExportFormats
        {
            get
            {
                return _exportFormats;
            }
            set
            {
            }
        }

        /// <summary>
        /// Gets or sets the ID of the selected export format.
        /// </summary>
        /// <remarks>
        /// This property is used to determine which export format the user has chosen for the
        /// export operation.
        /// </remarks>
        public int ExportID
        {
            get
            {
                return _exportID;
            }
            set
            {
                _exportID = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there are selected features to export.
        /// </summary>
        /// <remarks>
        /// This property is used to determine if the "Selected Only" option should be enabled in
        /// the export window.
        /// </remarks>
        public bool HaveSelection
        {
            get
            {
                return _selectedNumber > 0;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether only selected features should be exported.
        /// </summary>
        /// <remarks>
        /// This property is used to determine the scope of the export operation based on the user's
        /// choice in the export window.
        /// </remarks>
        public bool SelectedOnly
        {
            get
            {
                return _selectedOnly;
            }
            set
            {
                _selectedOnly = value;
            }
        }

        /// <summary>
        /// Gets a formatted string indicating the number of selected features and the total number
        /// of features in the layer.
        /// </summary>
        /// <remarks>
        /// This property is used to display the selection information in the export window.
        /// </remarks>
        public string SelectionText
        {
            get
            {
                return HaveSelection
                    ? String.Format("({0} of {1} feature{2})",
                        _selectedNumber.ToString("N0"),
                        _totalCount.ToString("N0"),
                        _totalCount > 1 ? "s" : String.Empty)
                    : String.Empty;
            }
        }

        /// <summary>
        /// Gets the list of available output type items for the ComboBox. Each item exposes a
        /// <c>Value</c> and a <c>Display</c> string.
        /// </summary>
        public List<OutputTypeItem> OutputTypes
        {
            get;
        } =
        [
            new OutputTypeItem(ExportOutputType.Shapefile,       "Shapefile (.shp)"),
            new OutputTypeItem(ExportOutputType.FileGeodatabase, "File Geodatabase (.gdb)")
        ];

        /// <summary>
        /// Gets or sets the currently selected output type.
        /// </summary>
        public ExportOutputType OutputType
        {
            get
            {
                return _outputType;
            }
            set
            {
                _outputType = value;
            }
        }

        #endregion Control Properties

        #region OutputTypeItem helper

        /// <summary>
        /// A simple display-value pair used to populate the Output Type ComboBox.
        /// </summary>
        public sealed class OutputTypeItem
        {
            /// <summary>
            /// Initializes a new instance of <see cref="OutputTypeItem"/>.
            /// </summary>
            public OutputTypeItem(ExportOutputType value, string display)
            {
                Value = value;
                Display = display;
            }

            /// <summary>Gets the enum value.</summary>
            public ExportOutputType Value
            {
                get;
            }

            /// <summary>Gets the display string.</summary>
            public string Display
            {
                get;
            }
        }

        #endregion OutputTypeItem helper

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets an error message indicating what is wrong with this object.
        /// </summary>
        /// <returns>An error message for the object, or null if there is no error.</returns>
        public string Error
        {
            get
            {
                if (_exportID == -1)
                    return "Please choose an export format";
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets an error message for the property with the given name.
        /// </summary>
        /// <param name="columnName">The name of the property for which to retrieve the error message.</param>
        /// <returns>An error message for the specified property, or null if there is no error.</returns>
        public string this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "ExportID":
                        if (_exportID == -1)
                            error = "Error: You must choose an export format";
                        break;
                }

                return error;
            }
        }

        #endregion IDataErrorInfo Members
    }
}