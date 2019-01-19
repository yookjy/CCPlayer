using CCPlayer.UWP.Xaml.Helpers;
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
    public class LanguageCodeToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string)
            {
                bool isNativeName = false;
                if (parameter is string)
                {
                    bool.TryParse(parameter as string, out isNativeName);
                }

                string name = string.Empty;
                string threeLetterCode = value as string;
                var twoLetterCode = LanguageCodeHelper.GetTwoLetterCode(threeLetterCode);

                if (twoLetterCode != null && twoLetterCode.Length == 2)
                {
                    var lang = new Windows.Globalization.Language(twoLetterCode);
                    name = isNativeName ? lang.NativeName : lang.DisplayName;
                }
                else
                {
                    name = threeLetterCode;

                    if (string.IsNullOrEmpty(name))
                    {
                        name = "Unknown";
                    }
                }
                return name;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
