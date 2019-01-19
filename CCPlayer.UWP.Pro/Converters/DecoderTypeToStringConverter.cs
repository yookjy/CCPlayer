//using CCPlayer.UWP.FFmpeg.Decoder;
using CCPlayer.UWP.Common.Codec;
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
    public class DecoderTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DecoderTypes)
            {
                var type = (DecoderTypes)value;
                return type.ToString();
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string)
            {
                DecoderTypes type;
                Enum.TryParse<DecoderTypes>(value as string, out type);
                return type;
            }
            else
            {
                return value;
            }
        }
    }
}
