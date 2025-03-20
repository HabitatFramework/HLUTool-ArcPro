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
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using HLU.Properties;
using HLU.UI.UserControls;

namespace HLU.Converters
{
    /// <summary>
    /// Converts a collection of CodeDescriptionBool items to a list or vice versa.
    /// </summary>
    class CodeDescriptionBoolConverter : IValueConverter
    {
        string _codeDeleteRow = Settings.Default.CodeDeleteRow;
        internal static readonly string[] _separator = [" : "];

        #region IValueConverter Members

        /// <summary>
        /// Converts a CodeDescriptionBool enumerable to a list of code/description pairs with a bool.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((value is IEnumerable<CodeDescriptionBool> m) && (m.Any()))
                return FormatList(m);

            return value;
        }

        /// <summary>
        /// Converts a code/description pair to a string.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return UnformatString(s);
            else
                return value;
        }

        #endregion IValueConverter Members

        #region Methods

        /// <summary>
        /// Formats an array of code description bool items.
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        private object FormatList(IEnumerable<CodeDescriptionBool> items)
        {
            return items
                .Select(m => new CodeDescriptionBool(m.code, String.Format("{0} : {1}", m.code, m.description), m.preferred))
                .ToList();
            //return items
            //    .OrderBy(m => m.code) // Ensure ordering is applied correctly
            //    .Select(m => new CodeDescriptionBool(m.code, String.Format("{0} : {1}", m.code, m.description), m.preferred))
            //    .ToList();
        }

        /// <summary>
        /// Extracts the first component of a string, unless it matches a special case.
        /// </summary>
        /// <param name="inString">The input string to process.</param>
        /// <returns>
        /// - If the input string is null or empty, it returns the original string.
        /// - If the input string matches the special `_codeDeleteRow`, it is returned as is.
        /// - Otherwise, the string is split using `_separator`, and the first part is returned.
        /// </returns>
        private string UnformatString(string inString)
        {
            if (!String.IsNullOrEmpty(inString))
            {
                if (inString == _codeDeleteRow)
                {
                    return inString;
                }
                else
                {
                    string[] splitArray = inString.Split(_separator, StringSplitOptions.None);
                    return splitArray[0];
                }
            }
            return inString;
        }

        #endregion Methods
    }
}
