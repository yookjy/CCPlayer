using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Lime.Models;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight.Threading;

namespace CCPlayer.WP81.ViewModel
{
    public class GeneralSettingViewModel : ViewModelBase, IFileOpenPickerContinuable
    {
        public static readonly string NAME = typeof(GeneralSettingViewModel).Name;
        private bool _IsFlyoutOpen;
        public bool IsFlyoutOpen
        {
            get { return _IsFlyoutOpen; }
            set { this.Set(() => IsFlyoutOpen, ref _IsFlyoutOpen, value); }
        }

        public int StartUpSection
        {
            get
            {
                return Settings.General.StartUpSection;
            }
            set
            {
                //App.FeatureLevel = 0; //테스트 값
                if (Settings.General.StartUpSection != value)
                {
                    Settings.General.StartUpSection = value;
                    RaisePropertyChanged();
                    //유료기능 체크
                    CheckPaidFeature(() => 
                    {
                        StartUpSection = 0;
                    });
                }
            }
        }

        private bool isChecking = false;
        private async void CheckPaidFeature(Action action)
        {
            if (!isChecking)
            {
                isChecking = true;
                await System.Threading.Tasks.Task.Delay(100);
                if (!VersionHelper.CheckPaidFeature())
                {
                    action.Invoke();
                }
                isChecking = false;
            }
        }

        private PickerItem<string, int> _AllVideoSectionItem;

        public bool UseAllVideoSection
        {
            get
            {
                return Settings.General.UseAllVideoSection;
            }
            set
            {
                if (Settings.General.UseAllVideoSection != value)
                {
                    Settings.General.UseAllVideoSection = value;

                    if (Settings.General.UseAllVideoSection)
                    {
                        if (!StartUpSectionSource.Contains(_AllVideoSectionItem))
                        {
                            StartUpSectionSource.Insert(1, _AllVideoSectionItem);
                            MessengerInstance.Send<Message>(new Message("InsertAllVideoSectino", null), MainViewModel.NAME);
                        }
                    }
                    else
                    {
                        if (StartUpSectionSource.Contains(_AllVideoSectionItem))
                        {
                            int backup = StartUpSection;
                            StartUpSection = 0;

                            if (StartUpSectionSource.Remove(_AllVideoSectionItem))
                            {
                                MessengerInstance.Send<Message>(new Message("RemoveAllVideoSection", null), MainViewModel.NAME);
                            }
                            else
                            {
                                StartUpSection = backup;
                            }
                        }
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<PickerItem<string, string>> FontSource { get; set; }
        public ObservableCollection<PickerItem<string, string>> HardwareBackButtonActionSource { get; set; }
        public ObservableCollection<PickerItem<string, int>> StartUpSectionSource { get; set; }

        public ICommand LoadedFontListCommand { get; set; }
        public ICommand ImportFontCommand { get; set; }
        public ICommand DeleteFontCommand { get; set; }

        public Settings Settings { get; set; }
        public SettingDAO SettingDAO { get; set; }

        public GeneralSettingViewModel(SettingDAO settingDAO)
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

            PickerItem<string, string>[] hwBackKeyAction = {
                new PickerItem<string, string>() { Name = loader.GetString("MoveToUpperFolder"), Key = HardwareBackButtonAction.MoveToUpperFolder.ToString() },
                new PickerItem<string, string>() { Name = loader.GetString("TerminateApplication"), Key = HardwareBackButtonAction.TerminateApplication.ToString() }
            };

            FontSource = new ObservableCollection<PickerItem<string, string>>();
            HardwareBackButtonActionSource = new ObservableCollection<PickerItem<string, string>>(hwBackKeyAction);

            StartUpSectionSource = new ObservableCollection<PickerItem<string, int>>();
            _AllVideoSectionItem = new PickerItem<string, int>() { Name = loader.GetString("AllVideo/Text"), Key = 1 };
            StartUpSectionSource.Add(new PickerItem<string, int>() { Name = loader.GetString("Explorer/Header"), Key = 0 });
            if (UseAllVideoSection)
            {
                StartUpSectionSource.Add(_AllVideoSectionItem);
            }
            StartUpSectionSource.Add(new PickerItem<string, int>() { Name = loader.GetString("Playlist/Header"), Key = 2 });
            StartUpSectionSource.Add(new PickerItem<string, int>() { Name = loader.GetString("About/Header"), Key = 3 });
        }

        private void CreateCommands()
        {
            LoadedFontListCommand = new RelayCommand<object>(LoadedFontListCommandExecute);
            ImportFontCommand = new RelayCommand(ImportFontCommandExecute);
            DeleteFontCommand = new RelayCommand<RoutedEventArgs>(DeleteFontCommandExecute);
        }
        
        private void RegisterMessages()
        {
        }
        
        private void LoadedFontListCommandExecute(object arg)
        {
            FontSource.Clear();
            FontHelper.LoadAllFont(FontSource, FontTypes.CustomFont, () =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    RaisePropertyChanged("FontSource");
                });
            });
        }

        private void ImportFontCommandExecute()
        {
            var picker = new FileOpenPicker()
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                ViewMode = PickerViewMode.List
            };

            picker.ContinuationData.Add(ContinuationManager.SOURCE_VIEW_MODEL_TYPE_FULL_NAME, this.GetType().FullName);
            picker.FileTypeFilter.Add(".TTF");
            picker.FileTypeFilter.Add(".OTF");
            picker.FileTypeFilter.Add(".TTC");
            picker.FileTypeFilter.Add(".OTC");

            picker.PickSingleFileAndContinue();
        }

        private void DeleteFontCommandExecute(RoutedEventArgs args)
        {
            var element = args.OriginalSource as FrameworkElement;

            if (element != null && element.DataContext is PickerItem<string, string>)
            {
                var listView = Lime.Xaml.Helpers.ElementHelper.FindVisualParent<ListView>(element);
                var pickerItem = element.DataContext as PickerItem<string, string>;
                
                var fonts = new List<PickerItem<string, string>>(FontSource.Where(x => x.Payload2 == pickerItem.Payload2));

                //자막 설정의 폰트 리스트 삭제
                MessengerInstance.Send(new Message("DeleteFonts", fonts), SubtitleSettingViewModel.NAME);
                //폰트 삭제
                FontHelper.RemoveFont(listView, FontSource, pickerItem);
            }
        }

        public async void ContinueFileOpenPicker(Windows.ApplicationModel.Activation.FileOpenPickerContinuationEventArgs args)
        {
            // The "args" object contains information about selected file(s).
            if (args.Files.Any())
            {
                var file = args.Files[0];
                //폰트를 설치
                await FontHelper.InstallFont(file);
                //폰트를 리스트에 적용  => 화면이 로딩되면 다시 리스트 Loaded가 발생하여 자동 갱신됨.
                //await FontHelper.InsertFont(FontSource, file);
            }
            //플라이아웃 다시 열기
            IsFlyoutOpen = true;
        }

    }
}
