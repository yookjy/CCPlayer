using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.WP81.Converters
{
    public class ThemePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (language != null && language.ToString() == "Uri")
            {
                return new Uri(string.Format(@"ms-appx:///{0}/{1}/{2}",
                    parameter, Application.Current.RequestedTheme.ToString().ToLower(), value));
            }
            else
            {
                return new BitmapImage(new Uri(string.Format(@"ms-appx:///{0}/{1}/{2}",
                    parameter, Application.Current.RequestedTheme.ToString().ToLower(), value)));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
