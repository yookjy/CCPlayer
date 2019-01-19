using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.ViewModel;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace CCPlayer.WP81.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            Stopwatch st = null;
            if (Debugger.IsAttached)
            {
                st = new System.Diagnostics.Stopwatch();
                st.Start();
            }
            this.InitializeComponent();
            //기존 허브 섹션 유지
//            this.NavigationCacheMode = NavigationCacheMode.Required;
            if (Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine("메인 페이지 로딩 완료 :  " + st.Elapsed);
            }

            RateReminder.CustomReminderText = ResourceLoader.GetForCurrentView().GetString("RateReminder/CustomReminderText");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            InitializeIAPs();
            try
            {
                var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("TempFonts", CreationCollisionOption.OpenIfExists);
                //var files = await folder.GetFilesAsync();
                await folder.DeleteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("============ 폰트파일 삭제 에러 발생 ==========");
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        
    }
}
