using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Lime.Xaml.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Phone.UI.Input;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CCPlayer.WP81.ViewModel
{
    public class MediaSearchViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(MediaSearchViewModel).Name;
        private FileDAO fileDAO;
        public Settings.GeneralSetting GeneralSetting { get; private set; }
        
        public ObservableCollection<MediaInfo> SearchResultSource { get; set; }
        public string SearchWord { get; set; }
        public bool SearchInResult { get; set; }

        public MediaInfo SelectedItem { get; set; }

        private ListViewSelectionMode _SelectionMode;
        public ListViewSelectionMode SelectionMode
        {
            get
            {
                return _SelectionMode;
            }
            set
            {
                Set(ref _SelectionMode, value);
            }
        }

        private bool _ButtonGroupVisible;
        public bool ButtonGroupVisible
        {
            get
            {
                return _ButtonGroupVisible;
            }
            set
            {
                Set(ref _ButtonGroupVisible, value);
            }
        }

        private bool _IsSearchOpened;
        public bool IsSearchOpened
        {
            get
            {
                return _IsSearchOpened;
            }
            set
            {
                Set(ref _IsSearchOpened, value, true);
            }
        }

        private bool _CheckListButtonEnable;
        public bool CheckListButtonEnable
        {
            get
            {
                return _CheckListButtonEnable;
            }
            set
            {
                Set(ref _CheckListButtonEnable, value);
            }
        }

        private bool _PlayButtonEnable;
        public bool PlayButtonEnable
        {
            get
            {
                return _PlayButtonEnable;
            }
            set
            {
                Set(ref _PlayButtonEnable, value);
            }
        }
        
        public ICommand MediaSearchClickCommand { get; set; }
        public ICommand FileClickCommand { get; set; }
        public ICommand FileSelectionChangedCommand { get; set; }
        public ICommand LayoutLoadedCommand { get; set; }
        public ICommand SearchWordVisibledCommand { get; set; }
        public ICommand SearchWordTextKeyUpCommand { get; set; }

        public ICommand CheckListButtonClickCommand { get; private set; }
        public ICommand BackButtonClickCommand { get; private set; }
        public ICommand SelectAllButtonClickCommand { get; set; }
        public ICommand PlayButtonClickCommand { get; set; }
        
        public MediaSearchViewModel(FileDAO fileDAO, SettingDAO settingDAO)
        {
            this.fileDAO = fileDAO;
            this.GeneralSetting = settingDAO.SettingCache.General;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();
        }
        
        private void CreateModels()
        {
            SearchResultSource = new ObservableCollection<MediaInfo>();
            ButtonGroupVisible = true;
        }

        private void CreateCommands()
        {
            LayoutLoadedCommand = new RelayCommand<Grid>(LayoutLoadedCommandExecute);
            MediaSearchClickCommand = new RelayCommand<RoutedEventArgs>(MediaSearchClickCommandExecute);
            FileClickCommand = new RelayCommand<ItemClickEventArgs>(FileClickCommandExecute);
            FileSelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(FileSelectionChangedCommandExecute);
            SearchWordVisibledCommand = new RelayCommand<TextBox>(SearchWordVisibledCommandExecute);
            SearchWordTextKeyUpCommand = new RelayCommand<KeyRoutedEventArgs>(SearchWordTextKeyUpCommandExecute);

            CheckListButtonClickCommand = new RelayCommand(CheckListButtonClickCommandExecute);
            BackButtonClickCommand = new RelayCommand(BackButtonClickCommandExecute);
            SelectAllButtonClickCommand = new RelayCommand<ListView>(SelectAllButtonClickCommandExecute);
            PlayButtonClickCommand = new RelayCommand<ListView>(PlayButtonClickCommandExecute);
        }
        
        private void RegisterMessages()
        {
            this.MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
                switch(msg.Key)
                {
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        if (SelectionMode != ListViewSelectionMode.None)
                        {
                            //선택 모드 종료
                            SelectionMode = ListViewSelectionMode.None;
                            ButtonGroupVisible = true;
                        }
                        else
                        {
                            //검색 패털 닫기
                            IsSearchOpened = false;
                        }
                        break;
                    case "SearchOpened":
                        //검색 패널 열기
                        IsSearchOpened = true;
                        break;
                }
            });
        }

        void LayoutLoadedCommandExecute(Grid grid)
        {
            RaisePropertyChanged("GeneralSetting");
        }

        async void MediaSearchClickCommandExecute(RoutedEventArgs e)
        {
            var textValue = string.Empty;

            //텍스트 데이터 트림
            if (!string.IsNullOrWhiteSpace(SearchWord))
            {
                textValue = SearchWord.Trim().ToLower();
            }

            if (!SearchInResult)
            {
                SearchResultSource.Clear();
                await ThreadPool.RunAsync(async handler =>
                {
                    List<MediaInfo> resultList = new List<MediaInfo>();
                    fileDAO.SearchAllVideoList(resultList, textValue);

                    foreach(var mi in resultList)
                    {
                        await DispatcherHelper.RunAsync(() => {
                            SearchResultSource.Add(mi);
                        });
                    }
                });
            }
            else
            {
                for (int i = SearchResultSource.Count - 1; i >= 0; i--)
                {
                    var mii = SearchResultSource[i];
                    if (mii.Name.ToLower().IndexOf(textValue) == -1)
                    {
                        SearchResultSource.RemoveAt(i);
                    }
                }
            }
            CheckListButtonEnable = SearchResultSource.Count > 0;
        }

        private void FileClickCommandExecute(ItemClickEventArgs e)
        {
            //유료 버전기능 체크
            if (VersionHelper.CheckPaidFeature())
            {
                var playList = new MediaInfo[] { e.ClickedItem as MediaInfo };
                MessengerInstance.Send<Message>(new Message("PlayList", playList), PlaylistViewModel.NAME);
                //검색 패털 닫기
                IsSearchOpened = false;
            }
        }

        private void FileSelectionChangedCommandExecute(SelectionChangedEventArgs obj)
        {
            PlayButtonEnable = SelectedItem != null;
        }

        void SearchWordVisibledCommandExecute(TextBox textBox)
        {
            if (IsSearchOpened)
            {
                if (SearchResultSource.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(SearchWord))
                    {
                        SearchWord = string.Empty;
                        RaisePropertyChanged("SearchWord");
                    }

                    textBox.Focus(FocusState.Keyboard);
                }
            }
        }

        private void SearchWordTextKeyUpCommandExecute(KeyRoutedEventArgs args)
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                MediaSearchClickCommandExecute(null);
            }
        }

        private void CheckListButtonClickCommandExecute()
        {
            //선택 모드 변경시
            SelectionMode = ListViewSelectionMode.Multiple;
            ButtonGroupVisible = false;
            PlayButtonEnable = false;
        }

        private void BackButtonClickCommandExecute()
        {
            //선택 모드 변경
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;
        }

        private void SelectAllButtonClickCommandExecute(ListView listView)
        {
            if (listView.SelectedItems.Count > 0)
            {
                listView.SelectedItems.Clear();
            }
            else
            {
                listView.SelectAll();
            }
        }

        private void PlayButtonClickCommandExecute(ListView listView)
        {
            //선택된 리스트 객체 생성 (아래에서 SelectionMode를 변경하므로, 반드시 ToList()등으로 객체를 복제해야함)
            var mediaInfoList = listView.SelectedItems.Cast<MediaInfo>().ToList();
            MessengerInstance.Send<Message>(new Message("PlayList", mediaInfoList), PlaylistViewModel.NAME);
            //버튼 초기화
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;
            //검색 패털 닫기
            IsSearchOpened = false;
            //허브 이동 요청 Main->Playlist
            MessengerInstance.Send<Message>(new Message("MoveToPlaylistSection", null), MainViewModel.NAME);
        }
    }
}
