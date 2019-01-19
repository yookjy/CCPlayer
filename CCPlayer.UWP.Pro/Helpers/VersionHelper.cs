using CCPlayer.UWP.Strings;
using GalaSoft.MvvmLight.Command;
using System;
using Lime.Xaml.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.System.Profile;
using GalaSoft.MvvmLight.Ioc;
using System.Linq;
using System.Windows.Input;

namespace CCPlayer.UWP.Helpers
{
    public static class VersionHelper
    {
        public static void CheckBetaOS(Action<string> betaAction, Action officialAction)
        {
            if (IsBetaOS)
            {
                betaAction.Invoke($"{BuildNumber[0]}.{BuildNumber[1]}.{BuildNumber[2]}.{BuildNumber[3]}");
            }
            else
            {
                officialAction.Invoke();
            }
        }

        public static bool IsBetaOS => BuildNumber[2] >= 14901; //Redstone2

        public static ulong[] BuildNumber
        {
            get
            {
                string sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
                ulong v = ulong.Parse(sv);
                ulong[] buildNo = new ulong[4];
                buildNo[0] = (v & 0xFFFF000000000000L) >> 48;
                buildNo[1] = (v & 0x0000FFFF00000000L) >> 32;
                buildNo[2] = (v & 0x00000000FFFF0000L) >> 16;
                buildNo[3] = (v & 0x000000000000FFFFL);
                return buildNo;
            }
        }

        public static string VersionName
        {
            get
            {
                var cur = Windows.ApplicationModel.Package.Current;
                var names = cur.DisplayName.Split(' ');
                return names.LastOrDefault();
            }
        }

        public static bool IsAdvertisingVersion
        {
            get
            {
                return VersionName == "Ad";
            }
        }

        private static bool _IsUnlockNetworkPlay;
        public static bool IsUnlockNetworkPlay
        {
            get
            {
                if (IsProfessionalVersion) return true;
                else return _IsUnlockNetworkPlay;
            }
            set { _IsUnlockNetworkPlay = value; }
        }

        public static bool IsProfessionalVersion
        {
            get
            {
                return VersionName == "Pro";
            }
        }

        public static bool CheckPaidFeature(string optionText = null, ICommand optionCmd = null)
        {
            if (IsAdvertisingVersion)
            {
                var loader = ResourceLoader.GetForCurrentView();
                bool? result = null;

                var contentDlg = DialogHelper.GetSimpleContentDialog(loader.GetString("AdvertisingTitle/Text"),
                    string.Format("{0}\n{1}", loader.GetString("Message/Info/Pro/Feature1"), loader.GetString("Message/Info/Pro/Feature2")),
                    loader.GetString("Button/Ok/Content"),
                    loader.GetString("Button/Cancel/Content"),
                    optionText, optionCmd);

                contentDlg.PrimaryButtonCommand = new RelayCommand(() => { result = true; });
                contentDlg.SecondaryButtonCommand = new RelayCommand(() => { result = false; });

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
