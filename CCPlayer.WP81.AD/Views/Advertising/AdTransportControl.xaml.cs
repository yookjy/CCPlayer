using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Strings;
using Microsoft.AdMediator.Core.Events;
using Microsoft.AdMediator.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 사용자 정의 컨트롤 항목 템플릿에 대한 설명은 http://go.microsoft.com/fwlink/?LinkId=234236에 나와 있습니다.

namespace CCPlayer.WP81.Views.Advertising
{
    public sealed partial class AdTransportControl : UserControl
    {
        public AdTransportControl()
        {
            this.InitializeComponent();

            if (VersionHelper.FeatureLevel == 0 || VersionHelper.FeatureLevel == 2)
            {
                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.AdDuplex]["Size"] = "480x80";

                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.Smaato]["Margin"] = new Thickness(0,-20,0,0);
                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.Smaato]["Width"] = 320;
                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.Smaato]["Height"] = 50;

                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["Width"] = 320;
                //AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["Height"] = 50;

                AdMediator_TransportControl.AdSdkEvent += AdMediator_TransportControl_AdSdkEvent;
                AdMediator_TransportControl.AdSdkError += AdMediator_TransportControl_AdSdkError;
                AdMediator_TransportControl.AdMediatorFilled += AdMediator_TransportControl_AdMediatorFilled;
                AdMediator_TransportControl.AdMediatorError += AdMediator_TransportControl_AdMediatorError;
            }
            else
            {
                this.LayoutRoot.Children.Clear();
            }
        }


        private void AdMediator_TransportControl_AdMediatorError(object sender, Microsoft.AdMediator.Core.Events.AdMediatorFailedEventArgs e)
        {
            Debug.WriteLine("AdMediator_TransportControl_AdMediatorError:" + e.Error + ", " + e.ErrorCode);
            // if (e.ErrorCode == AdMediatorErrorCode.NoAdAvailable)
            // AdMediator will not show an ad for this mediation cycle
        }

        private void AdMediator_TransportControl_AdMediatorFilled(object sender, Microsoft.AdMediator.Core.Events.AdSdkEventArgs e)
        {
            Debug.WriteLine("AdMediator_TransportControl_AdFilled:" + e.Name);
        }

        private void AdMediator_TransportControl_AdSdkError(object sender, Microsoft.AdMediator.Core.Events.AdFailedEventArgs e)
        {
            Debug.WriteLine("AdMediator_TransportControl_AdSdkError by {0}\n * ErrorCode: {1}\n * ErrorDescription: {2}\n * Error: {3}", e.Name, e.ErrorCode, e.ErrorDescription, e.Error);
            if (e.ErrorCode == AdMediatorErrorCode.NoAdAvailable)
            {
                //if (e.Name != null && e.Name.ToUpper() == "MICROSOFTADVERTISING" && !AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising].ContainsKey("CountryOrRegion"))
                //{
                //    AdMediator_TransportControl.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["CountryOrRegion"] = "US";
                //}
            }
        }

        private void AdMediator_TransportControl_AdSdkEvent(object sender, Microsoft.AdMediator.Core.Events.AdSdkEventArgs e)
        {
            Debug.WriteLine("AdMediator_TransportControl_AdSdk event {0} by {1}", e.EventName, e.Name);
        }

        private async void BuyProVersion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(Lime.Xaml.Helpers.UriHelper.GetMarketPlaceUri(CCPlayerConstant.APP_PRO_ID));
        }
    }
}
