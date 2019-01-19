using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Lime.Models;
using Windows.ApplicationModel.Resources;

namespace CCPlayer.WP81.ViewModel
{
    public class PlaybackSettingViewModel : ViewModelBase
    {

        private bool _UseFlipToPause;
        public bool UseFlipToPause
        {
            get
            {
                return Settings.Playback.UseFlipToPause;
            }
            set
            {
                Set(ref _UseFlipToPause, value);
            }
        }

        public ObservableCollection<PickerItem<string, int>> SeekTimeIntervalSource { get; set; }

        public ICommand ToggledCommand { get; set; }

        public Settings Settings { get; set; }
        public SettingDAO SettingDAO { get; set; }

        public PlaybackSettingViewModel(SettingDAO settingDAO)
        {
            this.SettingDAO = settingDAO;
            this.Settings = settingDAO.SettingCache;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();
        }

        private void CreateModels()
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView();

            PickerItem<string, int>[] seekInterval = {
                new PickerItem<string, int>() { Name = loader.GetString("AutoSeekInterval"), Key = 0 },
                new PickerItem<string, int>() { Name = string.Format("{0} {1}",  5, loader.GetString("SubtitleSyncUnit/Text")), Key =  5 },
                new PickerItem<string, int>() { Name = string.Format("{0} {1}", 10, loader.GetString("SubtitleSyncUnit/Text")), Key = 10 },
                new PickerItem<string, int>() { Name = string.Format("{0} {1}", 15, loader.GetString("SubtitleSyncUnit/Text")), Key = 15 },
                new PickerItem<string, int>() { Name = string.Format("{0} {1}", 30, loader.GetString("SubtitleSyncUnit/Text")), Key = 30 },
                new PickerItem<string, int>() { Name = string.Format("{0} {1}", 60, loader.GetString("SubtitleSyncUnit/Text")), Key = 60 },
            };

            SeekTimeIntervalSource = new ObservableCollection<PickerItem<string, int>>(seekInterval);
        }

        private void CreateCommands()
        {
            ToggledCommand = new RelayCommand<string>(ToggledCommandExecute);
        }

        private void RegisterMessages()
        {
        }

        private void ToggledCommandExecute(string value)
        {
            switch(value)
            {
                case "FlipToPause":
                    if (VersionHelper.CheckPaidFeature())
                    {
                        Settings.Playback.UseFlipToPause = _UseFlipToPause;
                    }
                    else
                    {
                        Settings.Playback.UseFlipToPause = false;
                        Set(ref _UseFlipToPause, false);
                        RaisePropertyChanged("UseFlipToPause");
                    }
                    break;
            }
        }
    }
}
