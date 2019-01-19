using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace CCPlayer.WP81.Views.Common
{
    public class CCPFlyout : Flyout
    {
        public event EventHandler<object> FlyoutOpening;
        public event EventHandler<object> FlyoutOpened;
        public event EventHandler<object> FlyoutClosed;

        public CCPFlyout()
        {
            this.Opening += CCPFlyout_Opening;
            this.Opened += VsFlyout_Opened;
            this.Closed += CCPFlyout_Closed;
        }


        void CCPFlyout_Opening(object sender, object e)
        {
            if (FlyoutOpening != null)
            {
                FlyoutOpening(sender, sender);
            }
        }

        void VsFlyout_Opened(object sender, object e)
        {
            if (FlyoutOpened != null)
            {
                FlyoutOpened(sender, sender);
            }
        }


        void CCPFlyout_Closed(object sender, object e)
        {
            if (FlyoutClosed != null)
            {
                FlyoutClosed(sender, sender);
            }
        }
    }
}
