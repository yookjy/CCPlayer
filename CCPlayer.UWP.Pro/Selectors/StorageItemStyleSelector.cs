using CCPlayer.UWP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.Selectors
{
    public class StorageItemStyleSelector : StyleSelector
    {
        public Style NoCheckStyle { get; set; }

        public Style DefaultStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var storageItemInfo = item as StorageItemInfo;

            if (storageItemInfo != null)
            {
                if (storageItemInfo.IsFile
                    || storageItemInfo.SubType == SubType.RootFolder)
                {
                    return DefaultStyle;
                }
                else
                {
                    return NoCheckStyle;
                }
            }

            return base.SelectStyleCore(item, container);
        }
    }
}
