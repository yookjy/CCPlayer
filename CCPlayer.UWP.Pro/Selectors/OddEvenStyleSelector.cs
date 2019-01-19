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
    public class OddEvenStyleSelector : StyleSelector
    {
        public Style OddStyle { get; set; }

        public Style EvenStyle { get; set; }

        public Style OrderChnagedStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            Style st = null; 
            ListView listView = ItemsControl.ItemsControlFromItemContainer(container) as ListView;
            int index = listView.IndexFromContainer(container);
            var plf = listView.ItemFromContainer(container) as PlayListFile;

            if (listView.ReorderMode == ListViewReorderMode.Enabled && plf.NewOrderNo != 0)
            {
                st = OrderChnagedStyle;
            }
            else
            {
                if (index % 2 == 0)
                {
                    st = OddStyle;
                }
                else
                {
                    st = EvenStyle;
                }
            }

            return st;

        }
    }
}
