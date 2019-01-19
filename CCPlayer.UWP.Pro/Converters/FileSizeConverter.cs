using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.Common.Helpers;
using Windows.UI.Xaml.Data;

namespace CCPlayer.UWP.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            ulong size = 0;
            if (value is long)
            {
                size = (ulong)(long)value;
            }
            else if (value is ulong)
            {
                size = (ulong)value;
            }

            var fs = FileHelper.FileRoundedSize(size);

            bool flag = false;
            if (!Boolean.TryParse(parameter as string, out flag))
            {
                if (fs == "0B") return string.Empty;
            }
            return fs;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
