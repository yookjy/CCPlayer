/*
  In App.xaml:
  <Application.Resources>
      <vm:ViewModelLocator xmlns:vm="clr-namespace:CCPlayer.WP81"
                           x:Key="Locator" />
  </Application.Resources>
  
  In the View:
  DataContext="{Binding Source={StaticResource Locator}, Path=ViewModelName}"

  You can also use Blend to do all this with the tool's support.
  See http://www.galasoft.ch/mvvm
*/

using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Views;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using SQLitePCL;

namespace CCPlayer.WP81.ViewModel
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            ////if (ViewModelBase.IsInDesignModeStatic)
            ////{
            ////    // Create design time view services and models
            ////    SimpleIoc.Default.Register<IDataService, DesignDataService>();
            ////}
            ////else
            ////{
            ////    // Create run time view services and models
            ////    SimpleIoc.Default.Register<IDataService, DataService>();
            ////}

            SimpleIoc.Default.Register<INavigationService>(() =>
            {
                var navigationService = new NavigationService();
                //navigationService.Configure("SettingsPage", typeof(SettingsPage));
                navigationService.Configure("MainPage", typeof(MainPage));
                navigationService.Configure("ExplorerPage", typeof(MainPage));
                return navigationService;
            });

            SimpleIoc.Default.Register<SQLiteConnection>(() =>
            {
                return new SQLiteConnection("ccplayer.db");
            });
            
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<ExplorerViewModel>();
            SimpleIoc.Default.Register<AllVideoViewModel>();
            SimpleIoc.Default.Register<PlaylistViewModel>();
            SimpleIoc.Default.Register<AboutViewModel>();
            SimpleIoc.Default.Register<MediaSearchViewModel>();
            

            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<SubtitleSettingViewModel>();
            SimpleIoc.Default.Register<GeneralSettingViewModel>();
            SimpleIoc.Default.Register<PlaybackSettingViewModel>();

            SimpleIoc.Default.Register<SettingDAO>();
            SimpleIoc.Default.Register<FolderDAO>();
            SimpleIoc.Default.Register<FileDAO>();

            SimpleIoc.Default.Register<Windows.Media.MediaExtensionManager>();

            SimpleIoc.Default.Register<CCPlayerViewModel>();
            SimpleIoc.Default.Register<TransportControlViewModel>();
        }

        public MainViewModel Main
        {
            get
            {
                return ServiceLocator.Current.GetInstance<MainViewModel>();
            }
        }

        public ExplorerViewModel Explorer
        {
            get
            {
                return ServiceLocator.Current.GetInstance<ExplorerViewModel>();
            }
        }

        public AllVideoViewModel AllVideo
        {
            get
            {
                return ServiceLocator.Current.GetInstance<AllVideoViewModel>();
            }
        }

        public PlaylistViewModel Playlist
        {
            get
            {
                return ServiceLocator.Current.GetInstance<PlaylistViewModel>();
            }
        }

        public AboutViewModel About
        {
            get
            {
                return ServiceLocator.Current.GetInstance<AboutViewModel>();
            }
        }

        public MediaSearchViewModel MediaSearch
        {
            get
            {
                return ServiceLocator.Current.GetInstance<MediaSearchViewModel>();
            }
        }

        public SettingsViewModel Settings
        {
            get
            {
                return ServiceLocator.Current.GetInstance<SettingsViewModel>();
            }
        }

        public SubtitleSettingViewModel SubtitleSetting
        {
            get
            {
                return ServiceLocator.Current.GetInstance<SubtitleSettingViewModel>();
            }
        }

        public GeneralSettingViewModel GeneralSetting
        {
            get
            {
                return ServiceLocator.Current.GetInstance<GeneralSettingViewModel>();
            }
        }

        public PlaybackSettingViewModel PlaybackSetting
        {
            get
            {
                return ServiceLocator.Current.GetInstance<PlaybackSettingViewModel>();
            }
        }

        public CCPlayerViewModel CCPlayer
        {
            get
            {
                return ServiceLocator.Current.GetInstance<CCPlayerViewModel>();
            }
        }

        public TransportControlViewModel TransportControl
        {
            get
            {
                return ServiceLocator.Current.GetInstance<TransportControlViewModel>();
            }
        }

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}