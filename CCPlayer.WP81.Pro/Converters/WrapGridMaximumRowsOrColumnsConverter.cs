using System;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.Converters
{
    public class WrapGridMaximumRowsOrColumnsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double panelWidth = (double)value;
            double rectWidth = (double)parameter;
            double rightCount = 1;

            if (language.Contains("_"))
            {
                string[] counts = language.Split('_');

                double leftCount = 1;
                if (counts.Length > 0)
                {
                    double.TryParse(counts[0], out leftCount);
                }
                
                if (counts.Length > 1)
                {
                    double.TryParse(counts[1], out rightCount);
                }

                if (rightCount > 1 && (panelWidth < rectWidth * (leftCount + rightCount)))
                {
                    return rightCount / 2;
                }
            }
            else
            {

            }

            return rightCount;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return 0;
        }
    }
}
