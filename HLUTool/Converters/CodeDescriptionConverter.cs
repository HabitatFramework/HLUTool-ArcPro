﻿// HLUTool is used to view and maintain habitat and land use GIS data.
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
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using HLU.Properties;

namespace HLU.Converters
{
    class CodeDescriptionConverter : IValueConverter
    {
        string _codeDeleteRow = Settings.Default.CodeDeleteRow;
        internal static readonly string[] _separator = [" : "];

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int codeColumnOrdinal = -1;
            int descriptionColumnOrdinal = -1;
            int sortColumnOrdinal = -1;

            DataTable t = null;

            if ((value is DataRow[] a) && (a.Length > 0))
            {
                t = a[0].Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList(a, codeColumnOrdinal, descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if (value is DataView v)
            {
                t = v.Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out descriptionColumnOrdinal, out sortColumnOrdinal);
                if (!String.IsNullOrEmpty(v.Sort) && t.Columns.Contains(v.Sort))
                    sortColumnOrdinal = t.Columns[v.Sort].Ordinal;
                return FormatList(t.Select(v.RowFilter), codeColumnOrdinal,
                    descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if ((t = value as DataTable) != null)
            {
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList(t.Select(), codeColumnOrdinal,
                    descriptionColumnOrdinal, sortColumnOrdinal);
            }

            if (value is DataRow r)
            {
                t = r.Table;
                GetOrdinals(t, parameter as string, out codeColumnOrdinal,
                    out descriptionColumnOrdinal, out sortColumnOrdinal);
                return FormatList([r], codeColumnOrdinal,
                    descriptionColumnOrdinal, sortColumnOrdinal);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return UnformatString(s);
            else
                return value;
        }

        #endregion

        private void GetOrdinals(DataTable t, string parameter, out int codeColumnOrdinal,
            out int descriptionColumnOrdinal, out int sortColumnOrdinal)
        {
            codeColumnOrdinal = -1;
            descriptionColumnOrdinal = -1;
            sortColumnOrdinal = -1;

            if (t != null)
            {
                if (!String.IsNullOrEmpty(parameter))
                {
                    string[] splitArray = parameter.Split(Settings.Default.ConverterParameterSeparator[0]);

                    switch (splitArray.Length)
                    {
                        case 3:
                            if (t.Columns.Contains(splitArray[2]))
                            {
                                DataColumn cs = t.Columns[splitArray[2]];
                                if (cs.DataType == typeof(int)) sortColumnOrdinal = cs.Ordinal;
                            }
                            goto case 2;
                        case 2:
                            if (t.Columns.Contains(splitArray[1]))
                            {
                                DataColumn cd = t.Columns[splitArray[1]];
                                if (cd.DataType == typeof(string)) descriptionColumnOrdinal = cd.Ordinal;
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

        private object FormatList(DataRow[] rows, int codeColumnOrdinal,
            int descriptionColumnOrdinal, int sortColumnOrdinal)
        {
            if (codeColumnOrdinal == -1) return rows;

            // Sort depending on the source columns
            if ((descriptionColumnOrdinal != -1) && (sortColumnOrdinal != -1))
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            description = FormatDescription(r, codeColumnOrdinal, descriptionColumnOrdinal),
                            sort_order = r.Field<int>(sortColumnOrdinal),
                            sort_order2 = r.Field<string>(codeColumnOrdinal)
                        }).OrderBy(r => r.sort_order).ThenBy(r => r.sort_order2);
            else if (descriptionColumnOrdinal != -1)
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            description = FormatDescription(r, codeColumnOrdinal, descriptionColumnOrdinal),
                            sort_order = r.Field<string>(descriptionColumnOrdinal)
                        }).OrderBy(r => r.sort_order);
            else if (sortColumnOrdinal != -1)
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            description = String.Empty,
                            sort_order = r.Field<int>(sortColumnOrdinal),
                            sort_order2 = r.Field<string>(codeColumnOrdinal)
                        }).OrderBy(r => r.sort_order).ThenBy(r => r.sort_order2);
            else
                return (from r in rows
                        select new
                        {
                            code = r.Field<string>(codeColumnOrdinal),
                            description = String.Empty,
                            sort_order = r.Field<int>(sortColumnOrdinal)
                        }).OrderBy(r => r.sort_order);
        }

        private string FormatDescription(DataRow r, int codeColumnOrdinal, int descriptionColumnOrdinal)
        {
            string code = r.Field<string>(codeColumnOrdinal);

            if (code != _codeDeleteRow)
            {
                return String.Format("{0} : {1}", code, r.Field<string>(descriptionColumnOrdinal));
            }
            else
            {
                return code;
            }
        }

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
                    string[] splitArray = s.Split(_separator, StringSplitOptions.None);
                    return splitArray[0];
                }
            }
            return s;
        }
    }

    class CodeDescriptionMultiConverter : IMultiValueConverter
    {
        internal static readonly string[] _separator = [" : "];

        #region IMultiValueConverter Members

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if ((values != null) && (values.Length == 2))
                return String.Format("{0} : {1}", values[0], values[1]);
            else
                return values;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                string s = value as string;
                return s.Split(_separator, StringSplitOptions.None);
            }
            else
            {
                return [value];
            }
        }

        #endregion
    }
}
