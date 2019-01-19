using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Text;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.Converters
{
    public class FontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var fwProp = typeof(FontWeights).GetRuntimeProperties().FirstOrDefault(x => ((FontWeight)x.GetValue("Weight")).Weight == (ushort)value);
            if (fwProp == null) return FontWeights.Medium;
            return (FontWeight)fwProp.GetValue("Weight");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
