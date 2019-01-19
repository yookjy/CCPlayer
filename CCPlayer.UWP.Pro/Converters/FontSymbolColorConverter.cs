using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.Common.Helpers;
using Windows.UI.Xaml.Data;
using CCPlayer.UWP.Helpers;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace CCPlayer.UWP.Converters
{
    public class FontSymbolColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string)
            {
                var colStr = value as string;
                if (colStr == FontTypes.App.ToString())
                {
                    return new SolidColorBrush(Colors.Orange);
                }
                else if (colStr == FontTypes.OS.ToString())
                {
                    return new SolidColorBrush(Colors.DarkGray);
                }
                else
                {
                    return App.Current.Resources["SystemControlForegroundAccentBrush"] as Brush;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
