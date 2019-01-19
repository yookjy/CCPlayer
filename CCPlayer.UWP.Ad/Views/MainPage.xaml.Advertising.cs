using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Strings;
using Lime.Xaml.Helpers;
using Microsoft.Advertising.WinRT.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace CCPlayer.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        private InterstitialAd interstitialAd;
        private AdControl adControl;

        private string AppId => App.IsMobile ? "2a63ee44-b7f6-4593-8576-2d57d7c7728f" : "9nblggh4z7q0";//"3a0fe7f6-c7e2-4819-9b88-c302ad0a0254";
        private string BannerUnitId => App.IsMobile ? "315081" : "315080";
        private string InterstitialUniId => App.IsMobile ? "315079" : "315078";

        public async void PurchaseTapped(object sender, TappedRoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(UriHelper.GetMarketPlaceUri(CCPlayerConstant.APP_PRO_ID));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (interstitialAd == null)
            {
                // Instantiate the interstitial video ad
                interstitialAd = new InterstitialAd();

                // Attach event handlers
                interstitialAd.ErrorOccurred += OnAdError;
                interstitialAd.AdReady += OnAdReady;
                interstitialAd.Cancelled += OnAdCancelled;
                interstitialAd.Completed += OnAdCompleted;
                GalaSoft.MvvmLight.Messaging.Messenger.Default.Register<Message>(this, "CheckInterstitialAd", (msg) =>
                {
                    var resource = ResourceLoader.GetForCurrentView();
                    Helpers.VersionHelper.CheckPaidFeature(resource.GetString("Message/Info/InterstitialQuestion"), new GalaSoft.MvvmLight.Command.RelayCommand<ContentDialog>((dlg) =>
                    {
                        //interstitialAd.RequestAd(AdType.Video, "d25517cb-12d4-4699-8bdc-52040c712cab", "11389925");
                        interstitialAd.RequestAd(AdType.Video, AppId, InterstitialUniId);
                        dlg.Hide();
                        App.ContentDlgOp = null;
                    }));
                });

            }

            if (adControl == null)
            {
                // Programatically create an ad control. This must be done from the UI thread.
                adControl = new AdControl();

                // Add event handlers if you want
                adControl.ErrorOccurred += OnErrorOccurred;
                adControl.AdRefreshed += OnAdRefreshed;

                // Set the application id and ad unit id
                // The application id and ad unit id can be obtained from Dev Center.
                // See "Monetize with Ads" at https://msdn.microsoft.com/en-us/library/windows/apps/mt170658.aspx
                //adControl.ApplicationId = "3f83fe91-d6be-434d-a0ae-7351c5a997f1";
                //adControl.AdUnitId = "10865270";
                adControl.ApplicationId = AppId;
                adControl.AdUnitId = BannerUnitId;

                if (App.IsMobile)
                {
                    // Set the dimensions
                    adControl.Width = 320;
                    adControl.Height = 50;

                    // Add the ad control to the page.
                    BottomAdPanel.Children.Add(adControl);
                }
                else
                {
                    //adControl.ApplicationId = "d25517cb-12d4-4699-8bdc-52040c712cab";
                    //adControl.AdUnitId = "10043134";

                    //adControl.Width = 160;
                    //adControl.Height = 600;

                    // Set the dimensions
                    adControl.Width = 728;
                    adControl.Height = 90;

                    // Add the ad control to the page.
                    BottomAdPanel.Children.Add(adControl);
                }
            }
        }

        // This is an error handler for the interstitial ad.
        private void OnErrorOccurred(object sender, AdErrorEventArgs e)
        {
            //rootPage.NotifyUser($"An error occurred. {e.ErrorCode}: {e.ErrorMessage}", NotifyType.ErrorMessage);
            VisualStateManager.GoToState(this, "NormalHand", false);
            VisualStateManager.GoToState(this, "PointerDownUp", false);
        }

        // This is an event handler for the ad control. It's invoked when the ad is refreshed.
        private void OnAdRefreshed(object sender, RoutedEventArgs e)
        {
            // We increment the ad count so that the message changes at every refresh.
            //adCount++;
            //rootPage.NotifyUser($"Advertisement #{adCount}", NotifyType.StatusMessage);
        }
        
        // This is an event handler for the interstitial ad. It is triggered when the interstitial ad is ready to play.
        private void OnAdReady(object sender, object e)
        {
            // The ad is ready to show; show it.
            interstitialAd.Show();
        }

        // This is an event handler for the interstitial ad. It is triggered when the interstitial ad is cancelled.
        private async void OnAdCancelled(object sender, object e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            VersionHelper.IsUnlockNetworkPlay = false;
            var dlg = DialogHelper.GetSimpleContentDialog(
                resource.GetString("Message/Alert/Title"),
                resource.GetString("Message/Info/InterstitialCanceled"),
                resource.GetString("Button/Close/Content"));
            await dlg.ShowAsync();
            App.ContentDlgOp = null;
        }

        // This is an event handler for the interstitial ad. It is triggered when the interstitial ad has completed playback.
        private async void OnAdCompleted(object sender, object e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            VersionHelper.IsUnlockNetworkPlay = true;
            var dlg = DialogHelper.GetSimpleContentDialog(
                resource.GetString("Message/Thanks/Title"),
                resource.GetString("Message/Info/InterstitialSucceed"),
                resource.GetString("Button/Close/Content"));
            await dlg.ShowAsync();
            App.ContentDlgOp = null;
        }

        // This is an error handler for the interstitial ad.
        private async void OnAdError(object sender, AdErrorEventArgs e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            var dlg = DialogHelper.GetSimpleContentDialog(
                resource.GetString("Message/Alert/Title"),
                resource.GetString("Message/Info/InterstitialFailed"),
                resource.GetString("Button/Close/Content"));
            await dlg.ShowAsync();
            App.ContentDlgOp = null;
        }
    }
}