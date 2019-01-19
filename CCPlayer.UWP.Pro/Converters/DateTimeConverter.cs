using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace CCPlayer.UWP.Converters
{
    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string format = parameter as string;
            if (!string.IsNullOrEmpty(format))
            {
                //IFormatProvider provider = System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat;
                //var fmt = string.Format(provider, format, value);
                //ViewModelLocator에서 UIThread의 컬쳐인포를 변경하였음
                if (value is DateTime && DateTime.MinValue == (DateTime)value)
                {
                    return string.Empty;
                }

                var fmt = string.Format(format, value);
                return fmt;
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
