using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Views.Controls;
using PropertyChanged;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.ViewModels
{
    public class SettingsDetailViewModel : CCPViewModelBase
    {
        private MenuItem _CurrentDetailMenuItem;
        [DoNotNotify]
        public MenuItem CurrentDetailMenuItem
        {
            get { return _CurrentDetailMenuItem; }
            set
            {
                if (Set(ref _CurrentDetailMenuItem, value))
                {
                    ChangeDetailMenu(value);
                }
            }
        }

        public Control SettingDetailContent { get; set; }

        private GeneralSettings _GeneralSettings;
        private PrivacySettings _PrivacySettings;
        private PlaybackSettings _PlaybackSettings;
        private SubtitleSettings _SubtitleSettings;
        private FontSettings _FontSettings;
        private AppInformation _AppInformation;

        protected override void CreateModel()
        {
            _GeneralSettings = new GeneralSettings();
            _PrivacySettings = new PrivacySettings();
            _PlaybackSettings = new PlaybackSettings();
            _SubtitleSettings = new SubtitleSettings();
            _FontSettings = new FontSettings();
            _AppInformation = new AppInformation();
        }

        protected override void FakeIocInstanceInitialize()
        {
        }

        protected override void InitializeViewModel()
        {
        }

        protected override void RegisterEventHandler()
        {
        }

        protected override void RegisterMessage()
        {
        }

        private void ChangeDetailMenu(MenuItem menuItem)
        {
            switch(menuItem.Type)
            {
                case MenuType.GeneralSetting:
                    SettingDetailContent = _GeneralSettings;
                    break;
                case MenuType.PrivacySetting:
                    SettingDetailContent = _PrivacySettings;
                    break;
                case MenuType.PlaybackSetting:
                    SettingDetailContent = _PlaybackSettings;
                    break;
                case MenuType.SubtitleSetting:
                    SettingDetailContent = _SubtitleSettings;
                    break;
                case MenuType.FontSetting:
                    SettingDetailContent = _FontSettings;
                    break;
                case MenuType.AppInfomation:
                    SettingDetailContent = _AppInformation;
                    break;
            }
        }

    }
}
