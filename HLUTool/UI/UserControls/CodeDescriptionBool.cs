// HLUTool is used to view and maintain habitat and land use GIS data.
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

using System.ComponentModel;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// A class that represents a code/description pair with a boolean value.
    /// </summary>
    public class CodeDescriptionBool : INotifyPropertyChanged
    {
        #region Fields

        private string _code;
        private string _description;
        private string _nvc_codes;
        private bool _preferred;
        private bool _isSeparator;

        #endregion Fields

        #region Properties

        public string Code
        {
            get => _code;
            set
            {
                _code = value;
                OnPropertyChanged(nameof(Code));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public string Display
        {
            get
            {
                return $"{Code} : {Description}";
            }
        }

        public string NVC_codes
        {
            get => _nvc_codes;
            set
            {
                _nvc_codes = value;
                OnPropertyChanged(nameof(NVC_codes));
            }
        }

        public bool Preferred
        {
            get => _preferred;
            set
            {
                _preferred = value;
                OnPropertyChanged(nameof(Preferred));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this item is a visual separator.
        /// Separator items should not be selectable and are displayed as horizontal lines.
        /// </summary>
        public bool IsSeparator
        {
            get => _isSeparator;
            set
            {
                _isSeparator = value;
                OnPropertyChanged(nameof(IsSeparator));
            }
        }

        #endregion Properties

        #region Events

        /// <summary>
        /// Event raised when a property value changes, to support data binding in WPF.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeDescriptionBool"/> class with default values.
        /// </summary>
        public CodeDescriptionBool()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeDescriptionBool"/> class with the specified code and description.
        /// </summary>
        /// <param name="code">The code of the item.</param>
        /// <param name="description">The description of the item.</param>
        public CodeDescriptionBool(string code, string description)
        {
            this.Code = code;
            this.Description = description;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeDescriptionBool"/> class with the specified code, description, and preferred status.
        /// </summary>
        /// <param name="code">The code of the item.</param>
        /// <param name="description">The description of the item.</param>
        /// <param name="preferred">Indicates whether the item is preferred.</param>
        public CodeDescriptionBool(string code, string description, bool preferred)
        {
            this.Code = code;
            this.Description = description;
            this.Preferred = preferred;
        }

        #endregion Constructor

        #region Event invokers

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event to notify the UI that a property value has changed, enabling data binding updates in WPF.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Event invokers

        #region Methods

        /// <summary>
        /// Returns a string that represents the current object, which is the description in this
        /// case. This is useful for displaying the item in UI elements like ComboBoxes.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return Description;
        }

        #endregion Methods
    }
}