using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.Converters
{
    public class TimeConverter2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan ts = TimeSpan.FromSeconds(0);
            if (value is long)
            {
                ts = TimeSpan.FromSeconds((long)value);
            }
            else if (value is double)
            {
                ts = TimeSpan.FromSeconds((double)value);
            }
            var val = ts.ToString(@"hh\:mm\:ss");
            if (ts.Milliseconds < 0)
            {
                return "-" + val;
            }
            
            return "+" + val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
