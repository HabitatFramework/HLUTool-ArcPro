// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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
using System.Windows.Data;
using HLU.Date;

namespace HLU.Converters
{
    /// <summary>
    /// A MultiValueConverter that compares two values and returns true if they are equal.
    /// This is useful for binding RadioButton selections where the selected value needs to match an item in a list.
    /// </summary>
    class MultiEqualityConverter : IMultiValueConverter
    {
        #region IValueConverter Members

        /// <summary>
        /// Converts multiple bound values into a single boolean result.
        /// Used to check if a RadioButton should be selected.
        /// </summary>
        /// <param name="values">An array of objects, where:
        ///     - values[0] is the selected value from the ViewModel.
        ///     - values[1] is the current item being checked.</param>
        /// <param name="targetType">The type to convert to (bool for IsChecked).</param>
        /// <param name="parameter">Optional parameter (unused here).</param>
        /// <param name="culture">Culture information (unused here).</param>
        /// <returns>True if the selected value matches the current item, otherwise false.</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            return values[0] != null && values[1] != null && values[0].Equals(values[1]);
        }

        /// <summary>
        /// Converts a boolean value back to a single object.
        /// Used when the user selects a RadioButton, updating the bound property.
        /// </summary>
        /// <param name="value">The boolean value from the RadioButton (true if checked).</param>
        /// <param name="targetTypes">The expected types for conversion.</param>
        /// <param name="parameter">The value to assign if the RadioButton is checked.</param>
        /// <param name="culture">Culture information (unused here).</param>
        /// <returns>The selected value if the RadioButton is checked, otherwise Binding.DoNothing.</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked)
            {
                // Return the current bound item (values[1]) instead of parameter
                return [parameter ?? Binding.DoNothing];
            }

            return [Binding.DoNothing];
        }

        #endregion
    }
}
