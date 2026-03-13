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

namespace HLU.UI.UserControls
{
    /// <summary>
    /// Represents an item that can be selected, along with its selection state.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    public class SelectionItem<T> : INotifyPropertyChanged
    {
        #region Fields

        private bool isSelected;

        private T item;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the item is selected.
        /// </summary>
        /// <value><c>true</c> if the item is selected; otherwise, <c>false</c>.</value>
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (value == isSelected) return;
                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnSelectionChanged();
            }
        }

        /// <summary>
        /// Gets or sets the item associated with this selection state.
        /// </summary>
        /// <value>The item associated with this selection state.</value>
        public T Item
        {
            get { return item; }
            set
            {
                if (value.Equals(item)) return;
                item = value;
                OnPropertyChanged(nameof(Item));
            }
        }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when the selection state changes.
        /// </summary>
        public event EventHandler SelectionChanged;

        #endregion Events

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionItem{T}"/> class with the
        /// specified item and a default selection state of false.
        /// </summary>
        /// <param name="item">The item associated with this selection state.</param>
        public SelectionItem(T item)
            : this(false, item)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionItem{T}"/> class with the
        /// specified item and selection state.
        /// </summary>
        /// <param name="selected">The initial selection state.</param>
        /// <param name="item">The item associated with this selection state.</param>
        public SelectionItem(bool selected, T item)
        {
            this.isSelected = selected;
            this.item = item;
        }

        #endregion Constructor

        #region Event invokers

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="SelectionChanged"/> event to notify subscribers that the selection state has changed.
        /// </summary>
        private void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion Event invokers
    }
}