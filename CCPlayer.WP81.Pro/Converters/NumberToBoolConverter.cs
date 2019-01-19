using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.Converters
{
    public class NumberToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (Regex.IsMatch(value.ToString(), @"\d"))
            {
                int val = 0;
                int param = 0;

                if (parameter != null)
                {
                    Int32.TryParse(parameter.ToString(), out param);
                }

                if (Int32.TryParse(value.ToString(), out val))
                {
                    return val > param;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
