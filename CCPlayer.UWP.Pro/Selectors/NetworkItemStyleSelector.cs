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
    public class NetworkItemStyleSelector : StyleSelector
    {
        public Style NoCheckStyle { get; set; }

        public Style DefaultStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var networkItemInfo = item as NetworkItemInfo;

            if (networkItemInfo != null)
            {
                if (networkItemInfo.IsFile
                    || networkItemInfo.Uri == null)
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
