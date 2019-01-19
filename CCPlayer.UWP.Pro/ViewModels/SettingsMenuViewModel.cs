using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using PropertyChanged;
using System.Collections.Generic;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.ViewModels
{
    public class SettingsMenuViewModel : CCPViewModelBase
    {
        [DoNotNotify]
        public List<MenuItem> SettingMenuItemsSource { get; set; }

        public MenuItem CurrentMenuItem { get; set; }

        public double ItemWidth { get; set; }

        public double ItemHeight { get; set; }

        public int ItemRowOrCol { get; set; }

        public Orientation ItemOrientation { get; set; }

        [DoNotNotify]
        public SizeChangedEventHandler SettingGridViewSizeChangedEventHandler { get; set; }

        protected override void FakeIocInstanceInitialize()
        {
        }

        protected override void CreateModel()
        {
            var resource = ResourceLoader.GetForCurrentView();
            SettingMenuItemsSource = new List<MenuItem>()
            {
                new MenuItem()
                {
                    Type = MenuType.GeneralSetting,
                    Name = resource.GetString("Settings/Menu/General/Title"),
                    Glyph = "\xE80F",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/General/Description")
                },
                new MenuItem()
                {
                    Type = MenuType.PrivacySetting,
                    Name = resource.GetString("Settings/Menu/AppLock/Title"),
                    Glyph = "\xE1F6",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/AppLock/Description")
                },
                new MenuItem()
                {
                    Type = MenuType.PlaybackSetting,
                    Name = resource.GetString("Settings/Menu/Playback/Title"),
                    Glyph = "\xE714",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/Playback/Description")
                },
                new MenuItem()
                {
                    Type = MenuType.SubtitleSetting,
                    Name = resource.GetString("Settings/Menu/Subtitle/Title"),
                    Glyph = "\xE7F0",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/Subtitle/Description")
                },
                new MenuItem()
                {
                    Type = MenuType.FontSetting,
                    Name = resource.GetString("Settings/Menu/Font/Title"),
                    //Glyph = "\xE8C1",
                    Glyph = "\xE8D2",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/Font/Description")
                },
                new MenuItem()
                {
                    Type = MenuType.AppInfomation,
                    Name = resource.GetString("Settings/Menu/App/Title"),
                    Glyph = "\xE19F",
                    ItemTapped = MenuItemTapped,
                    Description = resource.GetString("Settings/Menu/App/Description")
                }
            };
        }

        protected override void RegisterEventHandler()
        {
            SettingGridViewSizeChangedEventHandler = SettingGridViewSizeChanged;
        }
        
        protected override void RegisterMessage()
        {
        }

        protected override void InitializeViewModel()
        {
            ItemOrientation = Orientation.Vertical;
            ItemRowOrCol = 2;
        }

        private void MenuItemTapped(object sender, TappedRoutedEventArgs args)
        {
            var fe = args.OriginalSource as FrameworkElement;
            if (fe != null)
            {
                var mi = fe.DataContext as MenuItem;
                if (mi != null)
                {
                    MessengerInstance.Send(mi, "TappedSettingMenuItem");
                }
            }
        }

        private void SettingGridViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var windowWidth = Window.Current.Bounds.Width;

            if (windowWidth <= 432)
            {
                ItemWidth = e.NewSize.Width;
                ItemHeight = 64;
                ItemOrientation = Orientation.Horizontal;
                ItemRowOrCol = 1;
            }
            else
            {
                if (windowWidth < 840)
                {
                    ItemOrientation = Orientation.Vertical;
                    ItemRowOrCol = 3;
                }
                else
                {
                    ItemOrientation = Orientation.Vertical;
                    ItemRowOrCol = 2;
                }

                ItemWidth = 200;
                ItemHeight = 200;
                
            }
            //System.Diagnostics.Debug.WriteLine(windowWidth);
        }
    }
}
