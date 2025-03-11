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
    /// Converts a VagueDateInstance to a string and vice versa.
    /// </summary>
    class VagueDateConverter : IValueConverter
    {
        #region IValueConverter Members

        /// <summary>
        /// from DB to UI : VagueDateInstance -> string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not Date.VagueDateInstance vd)
                    return null;

                if (vd.IsBad)
                    return vd.UserEntry;
                else if (vd.IsUnknown)
                    return VagueDate.VagueDateTypes.Unknown.ToString();
                else
                    return Date.VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
            }
            catch { }

            return value;
        }

        /// <summary>
        /// from UI to DB : string -> VagueDateInstance
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not string vagueDateString) return value;

                // Get the type of date string.
                string formattedDateString;
                string dateType = VagueDate.GetType(vagueDateString, out formattedDateString);

                // Set the start and end dates.
                int startDate = Date.VagueDate.ToTimeSpanDays(formattedDateString, dateType, VagueDate.DateType.Start);
                int endDate = Date.VagueDate.ToTimeSpanDays(formattedDateString, dateType, VagueDate.DateType.End);

                return new Date.VagueDateInstance(startDate, endDate, dateType, vagueDateString);
            }
            catch { }

            return value;
        }

        #endregion IValueConverter Members
    }
}
