using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels;
using CCPlayer.UWP.Views;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;

namespace CCPlayer.UWP.ViewModels
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            //테스트 코드
            //try 
            //{
            //    //문화권 설정
            //    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(Windows.Globalization.Language.CurrentInputMethodLanguageTag);
            //System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            //System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("vi-VN");
            //System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("vi-VN");
            //System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            //}
            //catch (Exception) { }

            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            //SimpleIoc.Default.Register<SQLiteConnection>(() =>
            //{
            //    return new SQLiteConnection("ccplayer.db");
            //});

            var conn = new SQLiteConnection("ccplayer.db");
            SimpleIoc.Default.Register<SQLiteConnection>(() => conn);

            //var navigationService = this.CreateNavigationService();
            //SimpleIoc.Default.Register<INavigationService>(() => navigationService);

            //SimpleIoc.Default.Register<IDialogService, DialogService>();

            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<ExplorerViewModel>();
            SimpleIoc.Default.Register<DLNAViewModel>();
            SimpleIoc.Default.Register<NetworkViewModel>();
            SimpleIoc.Default.Register<CloudViewModel>();
            SimpleIoc.Default.Register<PlayListViewModel>();

            SimpleIoc.Default.Register<SettingsMenuViewModel>();
            SimpleIoc.Default.Register<SettingsDetailViewModel>();

            SimpleIoc.Default.Register<SubtitleSettingViewModel>();
            SimpleIoc.Default.Register<PrivacySettingViewModel>();
            SimpleIoc.Default.Register<GeneralSettingViewModel>();
            SimpleIoc.Default.Register<PlaybackSettingViewModel>();
            SimpleIoc.Default.Register<FontSettingViewModel>();
            SimpleIoc.Default.Register<AppInformationViewModel>();
            SimpleIoc.Default.Register<MediaFileInformationViewModel>();

            SimpleIoc.Default.Register<MediaExtensionManager>();

            SimpleIoc.Default.Register<SettingDAO>(() => new SettingDAO(conn));
            SimpleIoc.Default.Register<FolderDAO>();
            SimpleIoc.Default.Register<ThumbnailDAO>();
            SimpleIoc.Default.Register<PlayListDAO>();
            SimpleIoc.Default.Register<FontDAO>();

            SimpleIoc.Default.Register<Settings>(() => ServiceLocator.Current.GetInstance<SettingDAO>().SettingCache);
        }

        //private INavigationService CreateNavigationService()
        //{
        //    var navigationService = new NavigationService();
        //    navigationService.Configure("Playback", typeof(PlaybackPage));
        //    navigationService.Configure("Playback2", typeof(PlaybackPage2));
        //    // navigationService.Configure("key1", typeof(OtherPage1));
        //    // navigationService.Configure("key2", typeof(OtherPage2));

        //    return navigationService;
        //}

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public PlayListViewModel PlayList => ServiceLocator.Current.GetInstance<PlayListViewModel>();

        public ExplorerViewModel Explorer => ServiceLocator.Current.GetInstance<ExplorerViewModel>();

        public DLNAViewModel DLNA => ServiceLocator.Current.GetInstance<DLNAViewModel>();

        public NetworkViewModel Network => ServiceLocator.Current.GetInstance<NetworkViewModel>();

        public CloudViewModel Cloud => ServiceLocator.Current.GetInstance<CloudViewModel>();

        public SettingsMenuViewModel SettingsMenu => ServiceLocator.Current.GetInstance<SettingsMenuViewModel>();

        public SettingsDetailViewModel SettingsDetail => ServiceLocator.Current.GetInstance<SettingsDetailViewModel>();

        public SubtitleSettingViewModel SubtitleSetting => ServiceLocator.Current.GetInstance<SubtitleSettingViewModel>();

        public PrivacySettingViewModel PrivacySetting => ServiceLocator.Current.GetInstance<PrivacySettingViewModel>();

        public GeneralSettingViewModel GeneralSetting => ServiceLocator.Current.GetInstance<GeneralSettingViewModel>();

        public PlaybackSettingViewModel PlaybackSetting => ServiceLocator.Current.GetInstance<PlaybackSettingViewModel>();

        public FontSettingViewModel FontSetting => ServiceLocator.Current.GetInstance<FontSettingViewModel>();

        public AppInformationViewModel AppInformation => ServiceLocator.Current.GetInstance<AppInformationViewModel>();

        public MediaFileInformationViewModel MediaFileInformation => ServiceLocator.Current.GetInstance<MediaFileInformationViewModel>();
    }
}
