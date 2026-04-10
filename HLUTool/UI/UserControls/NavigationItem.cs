// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using System.ComponentModel;
using System.Windows.Controls;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// Represents a navigation item for the options window.
    /// </summary>
    public class NavigationItem : INotifyPropertyChanged
    {
        public string Name
        {
            get; set;
        }

        public string Category
        {
            get; set;
        }

        public UserControl Content
        {
            get; set;
        }

        private bool _hasErrors;

        /// <summary>
        /// Gets or sets a value indicating whether this navigation item has any validation errors.
        /// </summary>
        /// <value><c>true</c> if this navigation item has errors; otherwise, <c>false</c>.</value>
        public bool HasErrors
        {
            get => _hasErrors;
            set
            {
                _hasErrors = value;
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        private string _errorMessage;

        /// <summary>
        /// Gets or sets the error message associated with this navigation item, if any.
        /// </summary>
        /// <value>The error message.</value>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        private bool _isSelected;

        /// <summary>
        /// Gets or sets a value indicating whether this navigation item is currently selected.
        /// </summary>
        /// <value><c>true</c> if this navigation item is selected; otherwise, <c>false</c>.</value>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name.
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}