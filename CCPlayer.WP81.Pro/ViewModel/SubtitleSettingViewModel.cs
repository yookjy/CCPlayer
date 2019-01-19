using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Lime.Models;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;

namespace CCPlayer.WP81.ViewModel
{
    public class SubtitleSettingViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(SubtitleSettingViewModel).Name;

        public Settings Settings { get; set; }
        public SettingDAO SettingDAO { get; set; }

        public ObservableCollection<PickerItem<string, string>> FontSource { get; set; }
        public ObservableCollection<PickerItem<string, string>> FontStyleSource { get; set; }
        public ObservableCollection<PickerItem<string, ushort>> FontWeightSource { get; set; }

        public ICommand LoadedFontListCommand { get; set; }

        private bool _IsFontLoading;
        
        private PickerItem<string, string> _FontFamily;
        public PickerItem<string, string> FontFamily
        {
            get { return _FontFamily; }
            set
            {
                if (Set(ref _FontFamily, value))
                {
                    if (value == null)
                    {
                        Settings.Subtitle.FontFamily = Settings.FONT_FAMILY_DEFAUT;
                    }
                    else
                    {
                        Settings.Subtitle.FontFamily = value.Key;
                    }
                }
            }
        }

        public SubtitleSettingViewModel(SettingDAO settingDAO)
        {
            this.SettingDAO = settingDAO;
            this.Settings = settingDAO.SettingCache;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();

            //폰트 로드 
            FontHelper.FontFamilyListChanged += ((object sender, RoutedEventArgs e) => { LoadFontList(); });
        }

        private void CreateModels()
        {
            FontSource = new ObservableCollection<PickerItem<string, string>>();
            FontStyleSource = new ObservableCollection<PickerItem<string, string>>();
            FontWeightSource = new ObservableCollection<PickerItem<string, ushort>>();

            //글자 스타일 피커 데이터 생성
            foreach (FontStyle fs in Enum.GetValues(typeof(FontStyle)).Cast<FontStyle>())
            {
                PickerItem<string, string> item = new PickerItem<string, string>()
                {
                    Key = fs.ToString(),
                    Name = fs.ToString()
                };
                FontStyleSource.Add(item);
            }

            //글자 굵기 피커 데이터 설정
            foreach (var fwProp in typeof(FontWeights).GetRuntimeProperties())
            {
                PickerItem<string, ushort> item = new PickerItem<string, ushort>()
                {
                    Key = ((FontWeight)fwProp.GetValue("Weight")).Weight,
                    Name = fwProp.Name
                };
                FontWeightSource.Add(item);
            }
        }

        private void CreateCommands()
        {
            LoadedFontListCommand = new RelayCommand<object>((arg) => LoadedFontListCommandExecute(arg));
        }

        private void RegisterMessages()
        {
            MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
                switch (msg.Key)
                {
                    case "DeleteFonts":
                        var fonts = msg.GetValue<List<PickerItem<string, string>>>();
                        for (int i = FontSource.Count - 1; i >= 0; i--)
                        {
                            var storageFile = FontSource[i].Payload2 as StorageFile;
                            if (storageFile != null)
                            {
                                if (fonts.Any(x => (x.Payload2 as StorageFile).Path == storageFile.Path))
                                {
                                    FontSource.RemoveAt(i);
                                }
                            }
                        }
                        break;
                }
            });
        }
        private void LoadedFontListCommandExecute(object arg)
        {
            //폰트 로드
            LoadFontList();
        }
    

        private void LoadFontList()
        {
            if (!_IsFontLoading)
            {
                _IsFontLoading = true;
                FontSource.Clear();

                //폰트 콤보
                FontHelper.LoadAllFont(FontSource, FontTypes.All, () =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        var selected = FontSource.FirstOrDefault(x => x.Key == Settings.Subtitle.FontFamily);
                        if (selected == null)
                        {
                            Settings.Subtitle.FontFamily = Settings.FONT_FAMILY_DEFAUT;
                            selected = FontSource.FirstOrDefault(x => x.Key == Settings.Subtitle.FontFamily);
                        }

                        FontFamily = selected;
                        _IsFontLoading = false;
                    });
                });
            }
        }
    }
}