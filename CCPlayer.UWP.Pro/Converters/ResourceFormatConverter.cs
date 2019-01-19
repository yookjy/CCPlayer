using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace CCPlayer.UWP.Converters
{
    public class ResourceFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string key = parameter as string;
            if (!string.IsNullOrEmpty(key))
            {
                var resource = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                return string.Format(resource.GetString(key), value);
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
