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
using System.IO;
using System.Text.RegularExpressions;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// ViewModel for the Export to File Geodatabase dialog.
    /// Validates the GDB workspace path and feature class name.
    /// </summary>
    internal partial class ViewModelWindowGdbExport : INotifyPropertyChanged, IDataErrorInfo
    {
        #region Fields

        private string _gdbPath;
        private string _featureClassName;

        /// <summary>
        /// Valid feature class name: starts with a letter, contains only
        /// letters/digits/underscores, maximum 64 characters.
        /// </summary>
        [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{0,63}$")]
        private static partial Regex FeatureClassNameRegex();

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialises the view model with optional pre-populated values.
        /// </summary>
        /// <param name="initialGdbPath">
        /// Pre-populated .gdb folder path, or empty string for none.
        /// </param>
        /// <param name="initialFeatureName">
        /// Pre-populated feature class name suggestion.
        /// </param>
        public ViewModelWindowGdbExport(
            string initialGdbPath = "",
            string initialFeatureName = "HLU_Export")
        {
            _gdbPath = initialGdbPath ?? String.Empty;
            _featureClassName = String.IsNullOrWhiteSpace(initialFeatureName)
                ? "HLU_Export"
                : initialFeatureName;
        }

        #endregion Constructor

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion INotifyPropertyChanged

        #region Properties

        /// <summary>
        /// Gets or sets the selected .gdb folder path.
        /// </summary>
        public string GdbPath
        {
            get => _gdbPath;
            set
            {
                if (_gdbPath == value)
                    return;

                _gdbPath = value;
                OnPropertyChanged(nameof(GdbPath));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        /// <summary>
        /// Gets or sets the feature class name.
        /// </summary>
        public string FeatureClassName
        {
            get => _featureClassName;
            set
            {
                if (_featureClassName == value)
                    return;

                _featureClassName = value;
                OnPropertyChanged(nameof(FeatureClassName));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        /// <summary>
        /// Returns true when both fields are valid and OK can be confirmed.
        /// </summary>
        public bool IsValid =>
            String.IsNullOrEmpty(this[nameof(GdbPath)]) &&
            String.IsNullOrEmpty(this[nameof(FeatureClassName)]);

        #endregion Properties

        #region IDataErrorInfo

        /// <summary>
        /// Gets the overall object error message (unused; field-level errors
        /// are used instead).
        /// </summary>
        public string Error => null;

        /// <summary>
        /// Validates a single property and returns an error message, or null.
        /// </summary>
        public string this[string columnName]
        {
            get
            {
                return columnName switch
                {
                    nameof(GdbPath) => ValidateGdbPath(),
                    nameof(FeatureClassName) => ValidateFeatureClassName(),
                    _ => null
                };
            }
        }

        #endregion IDataErrorInfo

        #region Validation Helpers

        /// <summary>
        /// Validates the GDB path field.
        /// </summary>
        private string ValidateGdbPath()
        {
            if (String.IsNullOrWhiteSpace(_gdbPath))
                return "Error: Please select a File Geodatabase";

            if (!_gdbPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                return "Error: The path must point to a .gdb folder";

            if (!Directory.Exists(_gdbPath))
                return "Error: The File Geodatabase path does not exist";

            return null;
        }

        /// <summary>
        /// Validates the feature class name field.
        /// </summary>
        private string ValidateFeatureClassName()
        {
            if (String.IsNullOrWhiteSpace(_featureClassName))
                return "Error: Please enter a feature class name";

            if (_featureClassName.Length > 64)
                return "Error: Feature class name must be 64 characters or fewer";

            if (!FeatureClassNameRegex().IsMatch(_featureClassName))
            {
                if (!Char.IsLetter(_featureClassName[0]))
                    return "Error: Feature class name must start with a letter";

                return "Error: Feature class name may only contain " +
                       "letters, numbers, and underscores";
            }

            return null;
        }

        #endregion Validation Helpers
    }
}