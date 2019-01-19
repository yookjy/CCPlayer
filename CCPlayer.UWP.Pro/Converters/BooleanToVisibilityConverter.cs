using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.UWP.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
            {
                bool val = (bool)value;
                bool isReverse = false;

                if (parameter is bool)
                {
                    isReverse = (bool)parameter;
                    val ^= isReverse;
                }
                else if (parameter is string)
                {
                    bool.TryParse(parameter as string, out isReverse);
                    val ^= isReverse;
                }

                return val ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (Visibility)value == Visibility.Visible ? true : false;
        }
    }
}
