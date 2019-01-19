using CCPlayer.UWP.ViewModels;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
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

// 빈 페이지 항목 템플릿은 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 에 문서화되어 있습니다.

namespace CCPlayer.UWP.Views
{
    /// <summary>
    /// 자체에서 사용하거나 프레임 내에서 탐색할 수 있는 빈 페이지입니다.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel Vm { get { return (MainViewModel)DataContext; } }

        public bool IsAdvertising => Helpers.VersionHelper.IsAdvertisingVersion;
        

        public MainPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            //리마인더 리소스 설정
            RateReminder.CustomReminderText = ResourceLoader.GetForCurrentView().GetString("RateReminder/CustomReminderText");
        }
    }
}
