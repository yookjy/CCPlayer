using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Strings;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.AdMediator.Core.Events;
using Microsoft.AdMediator.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    public sealed partial class AdMainPage : UserControl
    {

        public AdMainPage()
        {
            this.InitializeComponent();

            if (VersionHelper.FeatureLevel == 0 || VersionHelper.FeatureLevel == 2)
            {
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.AdDuplex]["Size"] = "480x80";
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.AdDuplex]["Size"] = new Size(480, 80);
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.Smaato]["Margin"] = new Thickness(0,-20,0,0);
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.Smaato]["Width"] = 320d;
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.Smaato]["Height"] = 50d;
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["Width"] = 320d;
                //AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["Height"] = 50d;

                AdMediator_MainPage.AdSdkEvent += AdMediator_MainPage_AdSdkEvent;
                AdMediator_MainPage.AdSdkError += AdMediator_MainPage_AdSdkError;
                AdMediator_MainPage.AdMediatorFilled += AdMediator_MainPage_AdMediatorFilled;
                AdMediator_MainPage.AdMediatorError += AdMediator_MainPage_AdMediatorError;
            }
            else
            {
                this.LayoutRoot.Children.Clear();
            }

            Messenger.Default.Register<bool>(this, typeof(AdMainPage).FullName, (val) =>
            {
                AdMediator_MainPage.Margin = new Thickness(0);
            });
        }

        private void AdMediator_MainPage_AdMediatorError(object sender, Microsoft.AdMediator.Core.Events.AdMediatorFailedEventArgs e)
        {
            Debug.WriteLine("AdMediator_MainPage_AdMediatorError:" + e.Error + ", " + e.ErrorCode);
            // if (e.ErrorCode == AdMediatorErrorCode.NoAdAvailable)
            // AdMediator will not show an ad for this mediation cycle
        }

        private void AdMediator_MainPage_AdMediatorFilled(object sender, Microsoft.AdMediator.Core.Events.AdSdkEventArgs e)
        {
            Debug.WriteLine("AdMediator_MainPage_AdFilled:" + e.Name);
        }

        private void AdMediator_MainPage_AdSdkError(object sender, Microsoft.AdMediator.Core.Events.AdFailedEventArgs e)
        {
            Debug.WriteLine("AdMediator_MainPage_AdSdkError by {0}\n * ErrorCode: {1}\n * ErrorDescription: {2}\n * Error: {3}", e.Name, e.ErrorCode, e.ErrorDescription, e.Error);
            if (e.ErrorCode == AdMediatorErrorCode.NoAdAvailable)
            {
                //if (e.Name != null && e.Name.ToUpper() == "MICROSOFTADVERTISING" && !AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising].ContainsKey("CountryOrRegion"))
                //{
                //    AdMediator_MainPage.AdSdkOptionalParameters[AdSdkNames.MicrosoftAdvertising]["CountryOrRegion"] = "US";
                //}
            }
        }

        private void AdMediator_MainPage_AdSdkEvent(object sender, Microsoft.AdMediator.Core.Events.AdSdkEventArgs e)
        {
            Debug.WriteLine("AdMediator_MainPage_AdSdk event {0} by {1}", e.EventName, e.Name);
        }

        private async void BuyProVersion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(Lime.Xaml.Helpers.UriHelper.GetMarketPlaceUri(CCPlayerConstant.APP_PRO_ID));
        }
    }
}
