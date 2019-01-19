using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using PropertyChanged;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.ViewModels
{
    public class GeneralSettingViewModel : CCPThumbnailViewModelBase
    {
        [DoNotNotify]
        public ObservableCollection<KeyName> UnsupportedThumbnailSource { get; set; }

        public ulong ThumbnailRetentionSize { get; set; }

        [DoNotNotify]
        public ObservableCollection<KeyName> ThemeSource { get; set; }
        [DoNotNotify]
        public ObservableCollection<KeyName> ThumbnailRetentionPeriodSource { get; set; }

        public RoutedEventHandler LoadedEventHandler;
        public TappedEventHandler ClearThumbnailCacheTappedEventHandler;
        public TappedEventHandler ResetSettingsTappedEventHandler;

        protected override void CreateModel()
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView();
            
            ThemeSource = new ObservableCollection<KeyName>();
            ThumbnailRetentionPeriodSource = new ObservableCollection<KeyName>();
            UnsupportedThumbnailSource = new ObservableCollection<KeyName>();
        }

        protected override void RegisterEventHandler()
        {
            LoadedEventHandler = Loaded;
            ClearThumbnailCacheTappedEventHandler = ClearThumbnailCacheTapped;
            ResetSettingsTappedEventHandler = ResetSettingsTapped;
        }
        
        protected override void RegisterMessage()
        {
        }

        private string GetString(string key)
        {
            return ResourceLoader.GetForCurrentView().GetString(key);
        }

        private string GetThumbKey(string type)
        {
            return $"Thumbnail/Unsupported/{type}/Header";
        }

        private KeyName CreateKeyname(bool useUnsupportedThumbnail, string targetTypeName)
        {
            return new KeyName(useUnsupportedThumbnail, GetString(GetThumbKey(targetTypeName)))
                        { Type = targetTypeName, ItemTapped = UnsupportedThumbnailTapped };
        }

        protected override void InitializeViewModel()
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            ThemeSource.Add(new KeyName((int)ElementTheme.Light, resource.GetString("Theme/Light/Text")));
            ThemeSource.Add(new KeyName((int)ElementTheme.Dark, resource.GetString("Theme/Dark/Text")));

            string fmt = resource.GetString("Thumbnail/Retention/Days/Content");
            ThumbnailRetentionPeriodSource.Add(new KeyName(7, string.Format(fmt, 7)));
            ThumbnailRetentionPeriodSource.Add(new KeyName(15, string.Format(fmt, 15)));
            ThumbnailRetentionPeriodSource.Add(new KeyName(30, string.Format(fmt, 30)));

            var st = Settings.Thumbnail;

            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedLocalFolder, "LocalFolder"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedLocalFile, "LocalFile"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedDLNAFolder, "DLNAFolder"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedDLNAFile, "DLNAFile"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedWebDAVFolder, "WebDAVFolder"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedWebDAVFile, "WebDAVFile"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedFTPFolder, "FTPFolder"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedFTPFile, "FTPFile"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedOneDriveFolder, "OneDriveFolder"));
            UnsupportedThumbnailSource.Add(CreateKeyname(st.UseUnsupportedOneDriveFile, "OneDriveFile"));
        }

        private void UnsupportedThumbnailTapped(object sender, TappedRoutedEventArgs e)
        {
            CheckBox check = sender as CheckBox;
            if (check != null)
            {
                KeyName kn = check.DataContext as KeyName;
                if (kn != null)
                {
                    bool value = (bool)check.IsChecked;
                    switch (kn.Type)
                    {
                        case "LocalFolder":
                            Settings.Thumbnail.UseUnsupportedLocalFolder = value;
                            break;
                        case "LocalFile":
                            Settings.Thumbnail.UseUnsupportedLocalFile= value;
                            break;
                        case "DLNAFolder":
                            Settings.Thumbnail.UseUnsupportedDLNAFolder= value;
                            break;
                        case "DLNAFile":
                            Settings.Thumbnail.UseUnsupportedDLNAFile= value;
                            break;
                        case "WebDAVFolder":
                            Settings.Thumbnail.UseUnsupportedWebDAVFolder= value;
                            break;
                        case "WebDAVFile":
                            Settings.Thumbnail.UseUnsupportedWebDAVFile= value;
                            break;
                        case "FTPFolder":
                            Settings.Thumbnail.UseUnsupportedFTPFolder= value;
                            break;
                        case "FTPFile":
                            Settings.Thumbnail.UseUnsupportedFTPFile= value;
                            break;
                        case "OneDriveFolder":
                            Settings.Thumbnail.UseUnsupportedOneDriveFolder = value;
                            break;
                        case "OneDriveFile":
                            Settings.Thumbnail.UseUnsupportedOneDriveFile = value;
                            break;
                    }
                }
            }
        }

        private void Loaded(object sender, RoutedEventArgs e)
        {
            ThumbnailRetentionSize = ThumbnailDAO.GetThumbnailRetentionSize();
        }
        
        private void ClearThumbnailCacheTapped(object sender, TappedRoutedEventArgs e)
        {
            ThumbnailDAO.DeletePastPeriodThumbnail(0);
            ThumbnailRetentionSize = ThumbnailDAO.GetThumbnailRetentionSize();
        }
        
        protected override void FakeIocInstanceInitialize()
        {
            
        }

        private void ResetSettingsTapped(object sender, TappedRoutedEventArgs e)
        {
            //모든 설정 초기화
            this.Settings.ResetAll();
        }
    }
}
