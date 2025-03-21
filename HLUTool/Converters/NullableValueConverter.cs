﻿// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013 Thames Valley Environmental Records Centre
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
    /// Converts a null value to a string.Empty value and vice versa.
    /// </summary>
    class NullableValueConverter : IValueConverter
    {
         #region IValueConverter Members

         public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
         {
             return value;
         }

         public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
         {
            if (value == null || string.IsNullOrEmpty(value.ToString()))
                return null;

             return value;
         }

        #endregion IValueConverter Members
    }
}
