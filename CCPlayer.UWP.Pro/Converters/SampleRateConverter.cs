using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace CCPlayer.UWP.Converters
{
    public class SampleRateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int)
            {
                var sampleRate = (int)value;
                var formatedValue = string.Format("{0:F1}", sampleRate / 1000.0);

                if (formatedValue.Substring(formatedValue.IndexOf(".")) == ".0")
                {
                    return formatedValue.Substring(0, formatedValue.Length - 2);
                }
                return formatedValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
