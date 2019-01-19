using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.ViewModels
{

    public class FontSettingViewModel : CCPViewModelBase
    {
        [DependencyInjection]
        private Settings _Settings;
        //설정
        public Settings Settings
        {
            get { return _Settings; }
        }

        [DoNotNotify]
        public ObservableCollection<KeyName> FontSource { get; set; }

        public RoutedEventHandler LoadedEventHandler;
        public TappedEventHandler ImportFontTappedEventHandler;

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
        }

        protected override void CreateModel()
        {
            FontSource = new ObservableCollection<KeyName>();
        }

        protected override void RegisterEventHandler()
        {
            LoadedEventHandler = Loaded;
            ImportFontTappedEventHandler = ImportFontTapped;
        }
        
        protected override void RegisterMessage()
        {
        }

        protected override void InitializeViewModel()
        {
        }
        
        private void Loaded(object sender, RoutedEventArgs e)
        {
            FontSource.Clear();
            FontHelper.LoadFonts(FontSource, FontTypes.App, DeleteFontTapped, null);
        }

        private async void ImportFontTapped(object sender, TappedRoutedEventArgs e)
        {
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.List
            };

            if (!App.IsMobile)
            {
                picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            }

            picker.FileTypeFilter.Add(".TTF");
            picker.FileTypeFilter.Add(".TTC");
            picker.FileTypeFilter.Add(".OTF");
            picker.FileTypeFilter.Add(".OTC");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                //폰트를 설치
                await FontHelper.InstallFont(file, FontSource, DeleteFontTapped);
            }
        }
        
        private void DeleteFontTapped(object sender, TappedRoutedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var listViewItemData = element.DataContext as KeyName;

            if (element != null && listViewItemData != null)
            {
                //폰트 삭제
                FontHelper.RemoveFont(FontSource, listViewItemData);

                if (listViewItemData.Payload is StorageFile)
                {
                    var sf = listViewItemData.Payload as StorageFile;
                }
            }
        }
    }
}
