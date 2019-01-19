using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace CCPlayer.UWP.Converters
{
    public class DurationTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TimeSpan)
            {
                TimeSpan ts = (TimeSpan)value;

                if ((int)ts.TotalSeconds == 0)
                {
                    return string.Empty;
                }

                var pv = parameter as string;

                if (pv == "h:m:s")
                {
                    var key = string.Empty;
                    if (ts.Hours > 0)
                    {
                        return ts.ToString(@"h\:mm\:ss");
                    }
                    else
                    {
                        return ts.ToString(@"m\:ss");
                    }
                }
                else if (string.IsNullOrEmpty(pv))
                {
                    string fmt = string.Empty;
                    object[] values = null;
                    var key = string.Empty;
                    if (ts.Hours > 0)
                    {
                        key = "Format/Duration/Time/HM";
                        values = new object[] { ts.Hours, ts.Minutes };
                    }
                    else if (ts.Minutes > 0)
                    {
                        key = "Format/Duration/Time/MS";
                        values = new object[] { ts.Minutes, ts.Seconds };
                    }
                    else if (ts.Seconds > 0)
                    {
                        key = "Format/Duration/Time/S";
                        values = new object[] { ts.Seconds };
                    }

                    fmt = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString(key);
                    return string.Format(fmt, values);
                }
            }
            //return DependencyProperty.UnsetValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
