using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.Common.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.Converters
{
    public class SubtitleExistConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var subExts = value as ObservableCollection<StorageFile>;

            if (subExts != null)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < subExts.Count; i++ )
                {
                    sb.Append(Path.GetExtension(subExts[i].Path));
                }

                if (sb.Length > 0) 
                {
                    var loader = ResourceLoader.GetForCurrentView();
                    //return string.Format(loader.GetString("AttachedSubtitle"), sb.ToString(1, sb.Length - 1).Replace(".", ","));
                    return string.Format(sb.ToString(1, sb.Length - 1).Replace(".", " "));
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
