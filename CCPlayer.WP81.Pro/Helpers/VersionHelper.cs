using CCPlayer.WP81.Strings;
using GalaSoft.MvvmLight.Command;
using System;
using Lime.Xaml.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.Helpers
{
    public class VersionHelper
    {
        private static double _WindowsVersion;
        public static double WindowsVersion
        {
            get
            {
                if (_WindowsVersion == 0)
                {
                    if (Type.GetType("Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager, Windows.ApplicationModel.DataTransfer, ContentType=WindowsRuntime", false) != null)
                    {
                        _WindowsVersion = 10;
                    }
                    else
                    {
                        _WindowsVersion = 8.1;
                    }
                }
                return _WindowsVersion;
            }
        }

        public static string VersionName
        {
            get
            {
                string name = string.Empty;
                //FeatureLevel 0 광고有/기능無
                //FeatureLevel 1 광고無/기능無 : Light
                //FeatureLevel 2 광고有/기능有 : Basic
                //FeatureLevel 3 광고無/기능有 : Unlimited
                //FeatureLevel 10 베타     : Omega
                //FeatureLevel 20 프로     : Pro
                switch (App.FeatureLevel)
                {
                    case 0:
                        break;
                    case 1:
                        name = "Light";
                        break;
                    case 2:
                        name = "Basic";
                        break;
                    case 3:
                        name = "Unlimited";
                        break;
                    case 10:
                        name = "Omega";
                        break;
                    case 20:
                        name = "Pro";
                        break;
                }
                return name;
            }
        }

        //FeatureLevel 0 광고有/기능無
        //FeatureLevel 1 광고無/기능無 : Light
        //FeatureLevel 2 광고有/기능有 : Basic
        //FeatureLevel 3 광고無/기능有 : Unlimited
        //FeatureLevel 10 베타     : Omega
        //FeatureLevel 20 프로     : Pro
        public static ushort FeatureLevel
        {
            get { return App.FeatureLevel; }
            set { App.FeatureLevel = value; }
        }

        public static bool IsPaidFeature
        {
            get { return FeatureLevel >= 2; }
        }

        public static bool IsFullVersion
        {
            get { return FeatureLevel >= 3; }
        }

        public static bool CheckPaidFeature()
        {
            //if (IsFree)
            if (!IsPaidFeature)
            {
                if (App.ContentDlgOp != null) return false;

                var loader = ResourceLoader.GetForCurrentView();
                bool? result = null;
                var contentDlg = new ContentDialog()
                {
                    Content = new TextBlock 
                    { 
                        Text = string.Format("{0}\n{1}", loader.GetString("Message/Info/Pro/Feature1"), loader.GetString("Message/Info/Pro/Feature2")),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 12, 0, 12)
                    },
                    PrimaryButtonText = loader.GetString("Ok"),
                    PrimaryButtonCommand = new RelayCommand(() => { result = true; }),
                    SecondaryButtonText = loader.GetString("Cancel"),
                    SecondaryButtonCommand = new RelayCommand(() => { result = false; })
                };

                //var msgDlg = new MessageDialog(loader.GetString("Message/Info/Pro/Feature"));
                //msgDlg.Commands.Add(
                //   new UICommand(loader.GetString("Ok"), new UICommandInvokedHandler((cmd) => result = true)));
                //msgDlg.Commands.Add(
                //   new UICommand(loader.GetString("Cancel"), new UICommandInvokedHandler((cmd) => result = false)));


                //메세지 창 출력
                App.ContentDlgOp = contentDlg.ShowAsync();
                //후처리기 등록
                App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>(async (op, status) =>
                {
                    if (result == true)
                    {
                        await Windows.System.Launcher.LaunchUriAsync(UriHelper.GetMarketPlaceUri(CCPlayerConstant.APP_PRO_ID));
                    };
                    App.ContentDlgOp = null;
                });

                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
