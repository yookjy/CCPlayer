using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.WP81.Converters
{
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isReverse = GetReverseValue(parameter);

            if (value is bool)
            {
                bool? isVisible = value as bool?;
                
                if (isReverse)
                {
                    isVisible = !isVisible;
                }

                if (isVisible == true)
                    return Visibility.Visible;
                else
                    return Visibility.Collapsed;
            }
            else if (value is long)
            {
                var val = (long)value;
                if (val > 0 != isReverse)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
            else if (value is double)
            {
                var val = (double)value;
                if (val > 0 != isReverse)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
            else if (value is Symbol)
            {
                return Visibility.Visible;
            }
            else if (value is string && parameter is string)
            {
                if ((string)value == (string)parameter)
                {
                    return Visibility.Visible;
                }
            }
            else if (value is object)
            {
                if (isReverse && value == null)
                {
                    return Visibility.Visible;
                }
                else if (!isReverse && value != null)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        private bool GetReverseValue(object parameter)
        {
            bool isReverse = false;
            if (parameter is bool)
            {
                isReverse = (bool)parameter;
            }
            else if (parameter is string)
            {
                bool.TryParse(parameter as string, out isReverse);
            }
            return isReverse;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
