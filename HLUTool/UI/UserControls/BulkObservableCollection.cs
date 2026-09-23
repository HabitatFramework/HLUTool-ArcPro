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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that supports bulk replacement with a single
    /// <see cref="NotifyCollectionChangedAction.Reset"/> notification, eliminating the per-item
    /// <see cref="NotifyCollectionChangedAction.Add"/> notifications that cause WPF to re-evaluate
    /// bound properties (such as SelectedValue) for every item added.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Replaces the entire contents of the collection with <paramref name="newItems"/>
        /// and raises a single <see cref="NotifyCollectionChangedAction.Reset"/> notification.
        /// </summary>
        /// <param name="newItems">The new items to populate the collection with.</param>
        public void Reset(IEnumerable<T> newItems)
        {
            Items.Clear();
            if (newItems != null)
            {
                foreach (T item in newItems)
                    Items.Add(item);
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}