using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HLU.Converters
{
    /// <summary>
    /// Convert a number to a string with thousand separator and no decimal.
    /// </summary>
    public class CountToStringConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string count = string.Format("{0:n0}", value);
            return count;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        #endregion IValueConverter Members
    }

    /// <summary>
    /// Convert a number to a string with thousand separator and 2 decimals.
    /// </summary>
    public class AreaToStringConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string count;
            if (double.TryParse(value.ToString(), out double area))
                count = string.Format("{0:0.00}", area);
            else
                count = value.ToString();

            return count;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        #endregion IValueConverter Members
    }
}
