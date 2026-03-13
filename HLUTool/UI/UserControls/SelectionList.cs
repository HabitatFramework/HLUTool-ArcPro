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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// A list of items that can be selected. The list is ordered by the item value and the selected
    /// items can be retrieved using the SelectedItems property.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list. Must implement IComparable&lt;T&gt;.</typeparam>
    public class SelectionList<T> : ObservableCollection<SelectionItem<T>>
        where T : IComparable<T>
    {
        #region Properties

        /// <summary>
        /// Gets the selected items in the list
        /// </summary>
        /// <value>The selected items in the list.</value>
        public IEnumerable<T> SelectedItems
        {
            get { return this.Where(x => x.IsSelected).Select(x => x.Item); }
        }

        /// <summary>
        /// Gets all of the items in the SelectionList
        /// </summary>
        /// <value>All of the items in the SelectionList.</value>
        public IEnumerable<T> AllItems
        {
            get { return this.Select(x => x.Item); }
        }

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the SelectionList class with the specified collection of items.
        /// </summary>
        /// <param name="col">The collection of items to initialize the SelectionList with.</param>
        public SelectionList(IEnumerable<T> col)
            : base(ToSelectionItemEnumerable(col))
        {
        }

        #endregion Constructor

        #region Public methods

        /// <summary>
        /// Adds the item to the list
        /// </summary>
        /// <param name="item">The item to add to the list.</param>
        public void Add(T item)
        {
            int i = 0;
            foreach (T existingItem in AllItems)
            {
                if (item.CompareTo(existingItem) < 0) break;
                i++;
            }
            Insert(i, new SelectionItem<T>(item));
        }

        /// <summary>
        /// Checks if the item exists in the list
        /// </summary>
        /// <param name="item">The item to check for existence in the list.</param>
        /// <returns>True if the item exists in the list; otherwise, false.</returns>
        public bool Contains(T item)
        {
            return AllItems.Contains(item);
        }

        /// <summary>
        /// Selects all the items in the list
        /// </summary>
        public void SelectAll()
        {
            foreach (SelectionItem<T> selectionItem in this)
            {
                selectionItem.IsSelected = true;
            }
        }

        /// <summary>
        /// Unselects all the items in the list
        /// </summary>
        public void UnselectAll()
        {
            foreach (SelectionItem<T> selectionItem in this)
            {
                selectionItem.IsSelected = false;
            }
        }

        #endregion Public methods

        #region Helper methods

        /// <summary>
        /// Creates an SelectionList from any IEnumerable
        /// </summary>
        /// <param name="items">The collection of items to convert to a SelectionList.</param>
        /// <returns>An IEnumerable of SelectionItem&lt;T&gt;.</returns>
        private static IEnumerable<SelectionItem<T>> ToSelectionItemEnumerable(IEnumerable<T> items)
        {
            List<SelectionItem<T>> list = [];
            foreach (T item in items)
            {
                SelectionItem<T> selectionItem = new(item);
                list.Add(selectionItem);
            }
            return list;
        }

        #endregion Helper methods
    }
}