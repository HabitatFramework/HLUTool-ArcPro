// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2019 London & South East Record Centres (LaSER)
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

namespace HLU.Converters
{
    /// <summary>
    /// Converter to split leading text (before first colon)
    /// from an error message to determine the error level
    /// (e.g. "Error" or "Warning") so they can be displayed
    /// differently in the interface.
    /// </summary>
    /// <seealso cref="System.Windows.Data.IValueConverter" />
    class ErrorMessageConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null or non-string input
            if (value == null)
                return string.Empty;

            // Ensure value is a string
            if (value is not string val)
                return string.Empty;

            // Handle empty strings
            if (string.IsNullOrWhiteSpace(val))
                return string.Empty;

            // Attempt to split input message by colon
            string[] parts = val.Split(':');

            // Invalid input message format
            if (parts == null || parts.Length == 0)
                return string.Empty;

            // Return input message before first colon (trimmed)
            if (parts.Length == 1)
                return val.Trim();
            else
            {
                return parts[0].Trim();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        #endregion IValueConverter Members
    }
}
