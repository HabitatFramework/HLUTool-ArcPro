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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// A DataGridComboBoxColumn that supports binding the ItemsSource property to a collection in the view model.
    /// </summary>
    class DataGridComboBoxColumnWithBinding : DataGridComboBoxColumn
    {
        /// <summary>
        /// Generates the editing element for the cell. This method is called when the cell enters
        /// edit mode. It creates a ComboBox and binds its ItemsSource property to the same source
        /// as the column's ItemsSource property.
        /// </summary>
        /// <param name="cell">The cell that is being edited.</param>
        /// <param name="dataItem">The data item associated with the row.</param>
        /// <returns>The editing element for the cell.</returns>
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            FrameworkElement element = base.GenerateEditingElement(cell, dataItem);
            CopyItemsSource(element);
            return element;
        }

        /// <summary>
        /// Generates the element for the cell when it is not in edit mode. This method creates a ComboBox
        /// and binds its ItemsSource property to the same source as the column's ItemsSource property.
        /// </summary>
        /// <param name="cell">The cell that is not in edit mode.</param>
        /// <param name="dataItem">The data item associated with the row.</param>
        /// <returns>The element for the cell.</returns>
        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            FrameworkElement element = base.GenerateElement(cell, dataItem);
            CopyItemsSource(element);
            return element;
        }

        /// <summary>
        /// Copies the binding of the ItemsSource property from the column to the given element.
        /// This allows the ComboBox in the cell to use the same collection for its items as defined
        /// in the column's ItemsSource property.
        /// </summary>
        /// <param name="element">The element to which the ItemsSource binding will be copied.</param>
        private void CopyItemsSource(FrameworkElement element)
        {
            BindingOperations.SetBinding(element, ComboBox.ItemsSourceProperty,
                BindingOperations.GetBinding(this, ComboBox.ItemsSourceProperty));
        }
    }
}