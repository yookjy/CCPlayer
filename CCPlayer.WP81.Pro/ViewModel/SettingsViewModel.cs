using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Views;
using CCPlayer.WP81.Views.Common;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Windows.Phone.UI.Input;

namespace CCPlayer.WP81.ViewModel
{
    //https://marcominerva.wordpress.com/category/mvvm/ 참고!!!!!!!!!!!! 
    //DialogService in MVVM Light V5 
    // Using “AndContinue” methods in Windows Phone Store apps with MVVM
    //Behaviors to handle StatusBar and ProgressIndicator in Windows Phone 8.1 apps
    public class SettingsViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(SettingsViewModel).Name;

        private bool _IsSettingsOpened;
        public bool IsSettingsOpened
        {
            get
            {
                return _IsSettingsOpened;
            }
            set
            {
                Set(ref _IsSettingsOpened, value, true);
            }
        }

        public Settings Settings { get; set; }
        public SettingDAO SettingDAO { get; set; }

        public SettingsViewModel(SettingDAO settingDAO)
        {
            this.SettingDAO = settingDAO;
            this.Settings = settingDAO.SettingCache;
            this.RegisterMessage();
        }

        private void RegisterMessage()
        {
            MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
               switch (msg.Key)
               {
                   case "SettingsOpened":
                       IsSettingsOpened = true;
                       break;
                   case "BackPressed":
                       msg.GetValue<BackPressedEventArgs>().Handled = true;
                       IsSettingsOpened = false;
                       //여기서 최종 적으로 변경된 설정을 DB에 저장
                       SettingDAO.Replace(Settings);
                       break;
               }
            });
        }
    }
}
