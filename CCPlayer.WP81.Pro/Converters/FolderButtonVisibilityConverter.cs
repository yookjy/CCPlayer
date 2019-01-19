using CCPlayer.WP81.Models;
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
    public class FolderButtonVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is FolderType && parameter is string)
            {
                var type = (FolderType)value;
                var target = ((string)parameter).ToLower();
                if (target == "root" && type == FolderType.Root)
                {
                    return Visibility.Visible;
                }
                else if (target == "upper" && type == FolderType.Upper)
                {
                    return Visibility.Visible;
                }
                else if (target == "picker" && type == FolderType.Picker)
                {
                    return Visibility.Visible;
                }
            }
                        
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
