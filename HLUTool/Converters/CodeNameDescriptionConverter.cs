// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2014 Sussex Biodiversity Record Centre
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
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using HLU.Properties;

namespace HLU.Converters
{
    /// <summary>
    /// A converter class that generates the display value for
    /// habitat type fields by combining the code, name and
    /// description fields depending on their values.
    /// </summary>
    class CodeNameDescriptionConverter : IValueConverter
    {
        string _codeDeleteRow = Settings.Default.CodeDeleteRow;

        #region IValueConverter Members

        /// <summary>
        /// Converts the value of a habitat type field to a display value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int codeColumnOrdinal = -1;
            int nameColumnOrdinal = -1;
            int descriptionColumnOrdinal = -1;
            int sortColumnOrdinal = -1;

            DataTable t = null;

            if ((value is DataRow[] a) && (a.Length > 0))
            {
                t = a[0].Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out nameColumnOrdinal, out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList(a, codeColumnOrdinal, nameColumnOrdinal, descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if (value is DataView v)
            {
                t = v.Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out nameColumnOrdinal, out descriptionColumnOrdinal, out sortColumnOrdinal);
                if (!String.IsNullOrEmpty(v.Sort) && t.Columns.Contains(v.Sort))
                    sortColumnOrdinal = t.Columns[v.Sort].Ordinal;
                return FormatList(t.Select(v.RowFilter), codeColumnOrdinal,
                    nameColumnOrdinal, descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if ((t = value as DataTable) != null)
            {
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out nameColumnOrdinal, out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList(t.Select(), codeColumnOrdinal,
                    nameColumnOrdinal, descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if (value is DataRow r)
            {
                t = r.Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out nameColumnOrdinal, out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList([r], codeColumnOrdinal,
                    nameColumnOrdinal, descriptionColumnOrdinal, sortColumnOrdinal);
            }

            return value;
        }

        /// <summary>
        /// Converts the display value of a habitat type field back to the
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
        /// Extracts the column ordinals from the parameter string.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="parameter"></param>
        /// <param name="codeColumnOrdinal"></param>
        /// <param name="nameColumnOrdinal"></param>
        /// <param name="descriptionColumnOrdinal"></param>
        /// <param name="sortColumnOrdinal"></param>
        private void GetOrdinals(DataTable t, string parameter, out int codeColumnOrdinal,
            out int nameColumnOrdinal, out int descriptionColumnOrdinal, out int sortColumnOrdinal)
        {
            codeColumnOrdinal = -1;
            nameColumnOrdinal = -1;
            descriptionColumnOrdinal = -1;
            sortColumnOrdinal = -1;

            if (t != null)
            {
                if (!String.IsNullOrEmpty(parameter))
                {
                    string[] splitArray = parameter.Split(Settings.Default.ConverterParameterSeparator[0]);

                    switch (splitArray.Length)
                    {
                        case 4:
                            if (t.Columns.Contains(splitArray[3]))
                            {
                                DataColumn cs = t.Columns[splitArray[3]];
                                if (cs.DataType == typeof(int)) sortColumnOrdinal = cs.Ordinal;
                            }
                            goto case 3;
                        case 3:
                            if (t.Columns.Contains(splitArray[2]))
                            {
                                DataColumn cd = t.Columns[splitArray[2]];
                                if (cd.DataType == typeof(string)) descriptionColumnOrdinal = cd.Ordinal;
                            }
                            goto case 2;
                        case 2:
                            if (t.Columns.Contains(splitArray[1]))
                            {
                                DataColumn cd = t.Columns[splitArray[1]];
                                if (cd.DataType == typeof(string)) nameColumnOrdinal = cd.Ordinal;
                            }
                            goto case 1;
                        case 1:
                            if (t.Columns.Contains(splitArray[0]))
                            {
                                DataColumn cc = t.Columns[splitArray[0]];
                                if (cc.DataType == typeof(string)) codeColumnOrdinal = cc.Ordinal;
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Formats a list of habitat type rows to return a combined name and description,
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="codeColumnOrdinal"></param>
        /// <param name="nameColumnOrdinal"></param>
        /// <param name="descriptionColumnOrdinal"></param>
        /// <param name="sortColumnOrdinal"></param>
        /// <returns></returns>
        private object FormatList(DataRow[] rows, int codeColumnOrdinal,
            int nameColumnOrdinal, int descriptionColumnOrdinal, int sortColumnOrdinal)
        {
            if (codeColumnOrdinal == -1) return rows;

            // Sort depending on the source columns
            if ((nameColumnOrdinal != -1) && (descriptionColumnOrdinal != -1) && (sortColumnOrdinal != -1))
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            name = r.Field<string>(nameColumnOrdinal),
                            description = FormatDescription(r, codeColumnOrdinal, nameColumnOrdinal, descriptionColumnOrdinal),
                            sort_order = r.Field<int>(sortColumnOrdinal),
                            sort_order2 = r.Field<string>(nameColumnOrdinal)
                        }).OrderBy(r => r.sort_order).ThenBy(r => r.sort_order2);
            if ((nameColumnOrdinal != -1) && (descriptionColumnOrdinal != -1))
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            name = r.Field<string>(nameColumnOrdinal),
                            description = FormatDescription(r, codeColumnOrdinal, nameColumnOrdinal, descriptionColumnOrdinal),
                            sort_order = r.Field<string>(nameColumnOrdinal)
                        }).OrderBy(r => r.sort_order);
            if ((nameColumnOrdinal != -1) && (sortColumnOrdinal != -1))
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            name = r.Field<string>(nameColumnOrdinal),
                            description = String.Empty,
                            sort_order = r.Field<int>(sortColumnOrdinal),
                            sort_order2 = r.Field<string>(nameColumnOrdinal)
                        }).OrderBy(r => r.sort_order).ThenBy(r => r.sort_order2);
            else if ((descriptionColumnOrdinal != -1) && (sortColumnOrdinal != -1))
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            name = String.Empty,
                            description = FormatDescription(r, codeColumnOrdinal, nameColumnOrdinal, descriptionColumnOrdinal),
                            sort_order = r.Field<int>(sortColumnOrdinal),
                            sort_order2 = r.Field<string>(descriptionColumnOrdinal)
                        }).OrderBy(r => r.sort_order).ThenBy(r => r.sort_order2);
            else if (descriptionColumnOrdinal != -1)
                return (from r in rows
                       select new
                       {
                           code = r.Field<string>(codeColumnOrdinal),
                           name = String.Empty,
                           description = FormatDescription(r, codeColumnOrdinal, nameColumnOrdinal, descriptionColumnOrdinal),
                           sort_order = r.Field<string>(descriptionColumnOrdinal)
                       }).OrderBy(r => r.sort_order);
            else if (sortColumnOrdinal != -1)
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            name = String.Empty,
                            description = String.Empty,
                            sort_order = r.Field<int>(sortColumnOrdinal)
                        }).OrderBy(r => r.sort_order);
            else
                return from r in rows
                       select new
                       {
                           code = r.Field<string>(codeColumnOrdinal),
                           name = r.Field<string>(nameColumnOrdinal),
                           description = String.Empty,
                           sort_order = r.Field<int>(sortColumnOrdinal)
                       };
        }

        /// <summary>
        /// Formats a habitat type row to return a combined name and description,
        /// if they are different, or just the name or description if they are the same.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="codeColumnOrdinal"></param>
        /// <param name="nameColumnOrdinal"></param>
        /// <param name="descriptionColumnOrdinal"></param>
        /// <returns></returns>
        private string FormatDescription(DataRow r, int codeColumnOrdinal, int nameColumnOrdinal, int descriptionColumnOrdinal)
        {
            string code = r.Field<string>(codeColumnOrdinal);
            string name = r.Field<string>(nameColumnOrdinal);
            string description = r.Field<string>(descriptionColumnOrdinal);

            if (code != _codeDeleteRow)
            {
                if (name != description)
                {
                    if ((!String.IsNullOrEmpty(name)) && (!String.IsNullOrEmpty(description)))
                        return String.Format("{0} : {1}", name, description);
                    else
                        return name;
                }
                else
                {
                    if (!String.IsNullOrEmpty(description))
                        return description;
                    else
                        return name;
                }
            }
            else
            {
                return code;
            }
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
        private string UnformatString(string s)
        {
            if (!String.IsNullOrEmpty(s))
            {
                if (s == _codeDeleteRow)
                {
                    return s;
                }
                else
                {
                    string[] splitArray = s.Split(" : ", StringSplitOptions.None);
                    return splitArray[0];
                }
            }
            return s;
        }

        #endregion Methods
    }

    /// <summary>
    /// A converter class that generates the display value for
    /// habitat type fields by combining the code, name and
    /// description fields depending on their values.
    /// </summary>
    class CodeNameDescriptionMultiConverter : IMultiValueConverter
    {
        #region IMultiValueConverter Members

        /// <summary>
        /// Converts the value of a habitat type field to a display value
        /// by combining the code, name and description fields.
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debugger.Break();
            ShowMessageWindow.ShowMessage(parameter.ToString(), "Convert");

            if ((values != null) && (values.Length == 3))
                if ((values[2] != null) && (values[1] != values[2]))
                    return String.Format("{0} : {1}", values[1], values[2]);
                else
                    return values[1].ToString();
            else
                return values;
        }

        /// <summary>
        /// Converts the display value of a habitat type field back to the
        /// code value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetTypes"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debugger.Break();
            ShowMessageWindow.ShowMessage(parameter.ToString(), "ConvertBack");

            if (value != null)
            {
                string s = value as string;
                if (s.Contains(" : "))
                    return s.Split(" : ", StringSplitOptions.None);
                else
                    return s.Split(" : ", StringSplitOptions.None);
            }
            else
            {
                return [value];
            }
        }

        #endregion
    }
}
