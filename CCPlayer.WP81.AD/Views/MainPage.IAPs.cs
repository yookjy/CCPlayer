using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.ViewModel;
using CCPlayer.WP81.Views.Advertising;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
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

using IAPs = Windows.ApplicationModel.Store.CurrentApp;
//using IAPs = Windows.ApplicationModel.Store.CurrentAppSimulator;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace CCPlayer.WP81.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        LicenseChangedEventHandler licenseChangeHandler = null;

        private void InitializeIAPs()
        {
            LoadInAppPurchaseProxyFileAsync();

            CmdBar.Opened += CmdBar_Opened;
            CmdBar.Closed += CmdBar_Closed;
            RemoveAds.Click += RemoveAds_Click;
            UnlockFeatures.Click += UnlockFeatures_Click;
        }

        private async void LoadInAppPurchaseProxyFileAsync()
        {
            //StorageFolder proxyDataFolder = await Package.Current.InstalledLocation.GetFolderAsync("data");
            //StorageFile proxyFile = await proxyDataFolder.GetFileAsync("in-app-product.xml");
            //await IAPs.ReloadSimulatorAsync(proxyFile);

            licenseChangeHandler = new LicenseChangedEventHandler(InAppPurchaseRefreshScenario);
            IAPs.LicenseInformation.LicenseChanged += licenseChangeHandler;

            try
            {
                ListingInformation listing = await IAPs.LoadListingInformationAsync();

                if (!listing.ProductListings.ContainsKey("CCPLAYER_IAP_REMOVE_ADVERTISING")
                    || !listing.ProductListings.ContainsKey("CCPLAYER_IAP_UNLOCK_FEATURES"))
                {
                    CmdBar.Visibility = Visibility.Collapsed;
                    return;
                }

                ChangeCommandBarState();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                CmdBar.Visibility = Visibility.Collapsed;
            }
            finally
            {
                if (CmdBar.Visibility == Visibility.Collapsed)
                {
                    IAPs.LicenseInformation.LicenseChanged -= licenseChangeHandler;
                    Messenger.Default.Send(true, typeof(AdMainPage).FullName);
                }
                else
                {
                    Messenger.Default.Register<bool>(this, typeof(MainPage).FullName, (val) =>
                    {
                        if (!VersionHelper.IsFullVersion)
                        {
                            CmdBar.Visibility = !val ? Visibility.Visible : Visibility.Collapsed;
                        }
                    });
                }
            }
        }

        private async void InAppPurchaseRefreshScenario()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ChangeCommandBarState();

                if (CmdBar.Visibility == Visibility.Collapsed)
                {
                    IAPs.LicenseInformation.LicenseChanged -= licenseChangeHandler;
                }
            });

        }

        private async void RemoveAds_Click(object sender, RoutedEventArgs e)
        {
            await PurchaseProduct("CCPLAYER_IAP_REMOVE_ADVERTISING", 1);
            ChangeCommandBarState();
        }

        private async void UnlockFeatures_Click(object sender, RoutedEventArgs e)
        {
            await PurchaseProduct("CCPLAYER_IAP_UNLOCK_FEATURES", 2);
            ChangeCommandBarState();
        }

        private async Task PurchaseProduct(string productId, ushort featureLevel)
        {
            LicenseInformation licenseInformation = IAPs.LicenseInformation;
            if (!licenseInformation.ProductLicenses[productId].IsActive)
            {
                try
                {
                    await IAPs.RequestProductPurchaseAsync(productId);
                }
                catch (Exception ex)
                {
                    var dlg = new ContentDialog
                    {
                        Content = new TextBlock()
                        {
                            Text = "Error occured : " + ex.Message,
                            TextWrapping = TextWrapping.Wrap
                        },
                        PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("Confirm"),
                    };

                    await dlg.ShowAsync();
                }
            }
            else
            {
                VersionHelper.FeatureLevel |= featureLevel;
            }
        }
        
        private void ChangeCommandBarState()
        {
            LicenseInformation licenseInformation = IAPs.LicenseInformation;
            if (licenseInformation.ProductLicenses["CCPLAYER_IAP_REMOVE_ADVERTISING"].IsActive)
            {
                VersionHelper.FeatureLevel |= 1;
                AdPanel.Children.Clear();
                Messenger.Default.Send<bool>(true, typeof(TransportControl).FullName);
            }

            if (licenseInformation.ProductLicenses["CCPLAYER_IAP_UNLOCK_FEATURES"].IsActive)
            {
                VersionHelper.FeatureLevel |= 2;
            }

            GalaSoft.MvvmLight.Messaging.Messenger.Default.Send(new Message("RerfershAppVersion"));
            RemoveAds.IsEnabled = VersionHelper.FeatureLevel == 0 || VersionHelper.FeatureLevel == 2;
            UnlockFeatures.IsEnabled = VersionHelper.FeatureLevel == 0 || VersionHelper.FeatureLevel == 1;
            CmdBar.Visibility = VersionHelper.FeatureLevel < 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmdBar_Opened(object sender, object e)
        {
            CmdBar.Opacity = 1;
        }

        private void CmdBar_Closed(object sender, object e)
        {
            CmdBar.Opacity = 0.7;
        }
    }
}
