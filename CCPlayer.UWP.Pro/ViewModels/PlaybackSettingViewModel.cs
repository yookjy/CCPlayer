using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using Lime.Models;
using PropertyChanged;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;

namespace CCPlayer.UWP.ViewModels
{
    public class PlaybackSettingViewModel : CCPViewModelBase
    {
        public bool IsMobile { get; set; }

        public bool IsEnabledFlipToPause { get; set; }

        [DoNotNotify]
        public ObservableCollection<PickerItem<string, int>> SeekTimeIntervalSource { get; set; }

        [DependencyInjection]
        private Settings _Settings;
        //설정
        public Settings Settings
        {
            get { return _Settings; }
        }

        [DependencyInjection]
        public SettingDAO settingDAO;

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
            settingDAO = null;
        }

        protected override void CreateModel()
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView();
            var syncUnit = loader.GetString("SubtitleSyncUnit/Text");
            PickerItem<string, int>[] seekInterval = {
                new PickerItem<string, int>() { Name = loader.GetString("AutoSeekInterval"), Key = 0 },
                new PickerItem<string, int>() { Name = $"{5} {syncUnit}", Key =  5 },
                new PickerItem<string, int>() { Name = $"{10} {syncUnit}", Key =  10 },
                new PickerItem<string, int>() { Name = $"{15} {syncUnit}", Key =  15 },
                new PickerItem<string, int>() { Name = $"{30} {syncUnit}", Key =  30 },
                new PickerItem<string, int>() { Name = $"{60} {syncUnit}", Key =  60 },
            };

            SeekTimeIntervalSource = new ObservableCollection<PickerItem<string, int>>(seekInterval);
        }

        protected override void RegisterEventHandler()
        {
        }

        protected override void RegisterMessage()
        {
        }

        protected override void InitializeViewModel()
        {
            IsEnabledFlipToPause = SimpleOrientationSensor.GetDefault() != null;
            IsMobile = App.IsMobile;
        }
    }
}
