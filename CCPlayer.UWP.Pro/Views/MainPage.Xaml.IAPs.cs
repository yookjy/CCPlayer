using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

#if DEBUG
using IAPs = Windows.ApplicationModel.Store.CurrentAppSimulator;
#else
using IAPs = Windows.ApplicationModel.Store.CurrentApp;
#endif

namespace CCPlayer.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        public async Task ConfigureSimulatorAsync(string filename)
        {
            StorageFile proxyFile = await Package.Current.InstalledLocation.GetFileAsync("Data\\" + filename);
            await CurrentAppSimulator.ReloadSimulatorAsync(proxyFile);
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
#if DEBUG
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!App.IsXbox)
            {
                Messenger.Default.Register<Message<ListingInformation>>(this, "RetriveIAPsListingInformation", RetriveIAPsListingInformation);
                Messenger.Default.Register<string>(this, "Donation", Donate);
                IAPs.LicenseInformation.LicenseChanged += OnLicenseInformationChanged;
                //디버그용
                await ConfigureSimulatorAsync("in-app-purchase.xml");
            }
        }
#else
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!App.IsXbox)
            {
                Messenger.Default.Register<Message<ListingInformation>>(this, "RetriveIAPsListingInformation", RetriveIAPsListingInformation);
                Messenger.Default.Register<string>(this, "Donation", Donate);

                IAPs.LicenseInformation.LicenseChanged += OnLicenseInformationChanged;
            }
        }
#endif 

        private async void RetriveIAPsListingInformation(Message<ListingInformation> msg)
        {
            var listingInformation = await IAPs.LoadListingInformationAsync();
            msg.Action.Invoke(listingInformation);
        }

        /// <summary>
        /// Invoked when this page is about to unload
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            IAPs.LicenseInformation.LicenseChanged -= OnLicenseInformationChanged;
            base.OnNavigatingFrom(e);
        }
        
        /// <summary>
        /// Invoked when the licensing information changes.
        /// </summary>
        private void OnLicenseInformationChanged()
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ResourceLoader resource = ResourceLoader.GetForCurrentView();
                LicenseInformation licenseInformation = IAPs.LicenseInformation;
                //만료 또는 트라이얼의 경우
                if (!licenseInformation.IsActive || licenseInformation.IsTrial)
                {
                    //PaidLevel 설정
                    if (Vm.PaidLevel == Models.PaidLevelType.Full)
                    {
                        Vm.PaidLevel = Models.PaidLevelType.Trial;
                    }

                    if (Vm.IsTrial)
                    {
                        bool expired = false;

                        if (licenseInformation.IsActive)
                        {
                            if (licenseInformation.IsTrial)
                            {
                                var remainingTrialTime = (licenseInformation.ExpirationDate - DateTime.Now).Days;
                                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title 
                                    = string.Format(resource.GetString("IAPs/Purchase/ExpiredLeftDay/Title"), remainingTrialTime);
                                expired = remainingTrialTime < 0;
                            }
                        }
                        else
                        {
                            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = resource.GetString("IAPs/Purchase/Expired/Title");
                            expired = true;
                        }

                        if (expired)
                        {
                            Messenger.Default.Send(true, "LockByTrialExpired");
                            var dlg = DialogHelper.GetSimpleContentDialog(
                                resource.GetString("IAPs/Purchase/Expired/Title"), 
                                resource.GetString("IAPs/Purchase/Expired/Content"),
                                resource.GetString("Button/CCPClose/Content"), 
                                resource.GetString("MainMenu/IAPs/Button/Text"));
                            var result = await dlg.ShowAsync();
                            if (result == ContentDialogResult.Primary)
                            {
                                App.Current.Exit();
                            }
                            else
                            {
                                PurchaseFullLicense(true);
                            }
                            App.ContentDlgOp = null;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Invoked when the user asks purchase the app.
        /// </summary>
        private async void PurchaseFullLicense(bool isExitApp)
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            bool failed = false;
            string msg = null;
            LicenseInformation licenseInformation = IAPs.LicenseInformation;

            if (licenseInformation.IsTrial)
            {
                try
                {
                    await IAPs.RequestAppPurchaseAsync(false);
                    if (!licenseInformation.IsTrial && licenseInformation.IsActive)
                    {
                        Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = "";
                        //구매해 주셔서 감사합니다.
                        Vm.PaidLevel = Models.PaidLevelType.Full;
                        Messenger.Default.Send(false, "LockByTrialExpired");
                    }
                    else
                    {
                        failed = true;
                        msg = resource.GetString("IAPs/Purchase/Failed/Content");
                    }
                }
                catch (Exception ex)
                {
                    failed = true;
                    msg = resource.GetString("IAPs/Purchase/Failed/Content") + "\n(" + ex.Message + ")";
                }

                if (failed)
                {
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("IAPs/Purchase/Failed/Title"), 
                        msg, 
                        resource.GetString(isExitApp ? "Button/CCPClose/Content" : "Button/Close/Content"),
                        resource.GetString("IAPs/Purchase/Button/Retry"));
                    var result = await dlg.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        if (isExitApp) App.Current.Exit();
                    }
                    else
                    {
                        PurchaseFullLicense(isExitApp);
                    }
                    App.ContentDlgOp = null;
                }
            }
        }

        public void PurchaseTapped(object sender, TappedRoutedEventArgs e)
        {
            PurchaseFullLicense(false);
        }

        public async void Donate(string productId)
        {
            string title = null;
            string msg = null;
            try
            {
                PurchaseResults purchaseResults = await IAPs.RequestProductPurchaseAsync(productId);
                switch (purchaseResults.Status)
                {
                    case ProductPurchaseStatus.Succeeded:
                        //GrantFeatureLocally(purchaseResults.TransactionId);
                        FulfillProduct(productId, purchaseResults.TransactionId);
                        break;
                    case ProductPurchaseStatus.NotFulfilled:
                        // The purchase failed because we haven't confirmed fulfillment of a previous purchase.
                        // Fulfill it now.
                        //if (!IsLocallyFulfilled(purchaseResults.TransactionId))
                        //{
                        //    GrantFeatureLocally(purchaseResults.TransactionId);
                        //}
                        FulfillProduct(productId, purchaseResults.TransactionId);
                        break;
                    case ProductPurchaseStatus.NotPurchased:
                        //rootPage.NotifyUser("Product 1 was not purchased.", NotifyType.StatusMessage);
                        title = "IAPs/Purchase/Failed/Title";
                        msg = "IAPs/Purchase/Failed/Content";
                        break;
                }
            }
            catch (Exception)
            {
                //rootPage.NotifyUser("Unable to buy Product 1.", NotifyType.ErrorMessage);
                title = "IAPs/Purchase/Failed/Title";
                msg = "IAPs/Purchase/Failed/Content";
            }

            if (title != null && msg != null)
            {
                ResourceLoader resource = ResourceLoader.GetForCurrentView();
                var dlg = DialogHelper.GetSimpleContentDialog(
                                resource.GetString(title),
                                resource.GetString(msg),
                                resource.GetString("Button/Ok/Content"));
                await dlg.ShowAsync();
                App.ContentDlgOp = null;
            }
        }

        private async void FulfillProduct(string productId, Guid transactionId)
        {
            try
            {
                FulfillmentResult result = await IAPs.ReportConsumableFulfillmentAsync(productId, transactionId);
                switch (result)
                {
                    case FulfillmentResult.Succeeded:
                        break;
                    case FulfillmentResult.NothingToFulfill:
                        //rootPage.NotifyUser("There is no purchased product 1 to fulfill.", NotifyType.StatusMessage);
                        break;
                    case FulfillmentResult.PurchasePending:
                        //rootPage.NotifyUser("You bought product 1. The purchase is pending so we cannot fulfill the product.", NotifyType.StatusMessage);
                        break;
                    case FulfillmentResult.PurchaseReverted:
                        //rootPage.NotifyUser("You bought product 1, but your purchase has been reverted.", NotifyType.StatusMessage);
                        // Since the user's purchase was revoked, they got their money back.
                        // You may want to revoke the user's access to the consumable content that was granted.
                        break;
                    case FulfillmentResult.ServerError:
                        //rootPage.NotifyUser("You bought product 1. There was an error when fulfilling.", NotifyType.StatusMessage);
                        break;
                }
            }
            catch (Exception)
            {
                //rootPage.NotifyUser("You bought Product 1. There was an error when fulfilling.", NotifyType.ErrorMessage);
            }
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            var dlg = DialogHelper.GetSimpleContentDialog(
                            resource.GetString("IAPs/Donation/Thanks/Title"),
                            resource.GetString("IAPs/Donation/Thanks/Content"),
                            resource.GetString("Button/Ok/Content"));
            await dlg.ShowAsync();
            App.ContentDlgOp = null;
        }
    }
}
