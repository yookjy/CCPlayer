using CCPlayer.UWP.Common.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.Selectors
{
    public class CodecItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate VideoCodecItemTemplate { get; set; }
        public DataTemplate AudioCodecItemTemplate { get; set; }
        public DataTemplate SubtitleCodecItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            DataTemplate template = null;
            ListView listView = ItemsControl.ItemsControlFromItemContainer(container) as ListView;
            int index = listView.IndexFromContainer(container);
            var codecInfo = listView.ItemFromContainer(container) as CodecInformation;

            switch (codecInfo.CodecType)
            {
                case 0:
                    template = VideoCodecItemTemplate;
                    break;
                case 1:
                    template = AudioCodecItemTemplate;
                    break;
                case 3:
                    template = SubtitleCodecItemTemplate;
                    break;
                default:
                    template = base.SelectTemplateCore(item, container);
                    break;
            }

            return template;
        }
    }
}
