using CCPlayer.WP81.Extensions;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Strings;
using CCPlayer.WP81.Views;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using GalaSoft.MvvmLight.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Lime.Xaml.Helpers;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public partial class PlaylistViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(PlaylistViewModel).Name;
        #region 데이터 모델

        private LoadingMode loadingModel;
        public ObservableCollection<MediaInfo> PlaylistSource { get; set; }
        
        private ListViewReorderMode _ReorderMode;
        public ListViewReorderMode ReorderMode 
        {
            get
            {
                return _ReorderMode;
            }
            set
            {
                var prev = _ReorderMode;
                if (Set(ref _ReorderMode, value))
                {
                    //취소의 경우 재생목록으로 버튼 변경 및 원래 리스트 복원
                    if (prev == ListViewReorderMode.Enabled)
                    {
                        MainButtonGroupVisible = true;
                        CheckListButtonGroupVisible = false;
                        ReorderButtonGroupVisible = false;

                        //다시 로딩
                        PlaylistSource.Clear();
                        LoadFiles();
                    }
                }
            }
        }

        public MediaInfo SelectedItem { get; set; }
        public Settings.GeneralSetting GeneralSetting { get; private set; }
        private FileDAO fileDAO;

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

        private bool _MainButtonGroupVisible;
        public bool MainButtonGroupVisible
        {
            get
            {
                return _MainButtonGroupVisible;
            }
            set
            {
                Set(ref _MainButtonGroupVisible, value);
            }
        }

        private bool _CheckListButtonGroupVisible;
        public bool CheckListButtonGroupVisible
        {
            get
            {
                return _CheckListButtonGroupVisible;
            }
            set
            {
                Set(ref _CheckListButtonGroupVisible, value);
            }
        }

        private bool _ReorderButtonGroupVisible;
        public bool ReorderButtonGroupVisible
        {
            get
            {
                return _ReorderButtonGroupVisible;
            }
            set
            {
                Set(ref _ReorderButtonGroupVisible, value);
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

        private bool _ReorderButtonEnable;
        public bool ReorderButtonEnable
        {
            get
            {
                return _ReorderButtonEnable;
            }
            set
            {
                Set(ref _ReorderButtonEnable, value);
            }
        }

        private bool _RemoveButtonEnable;
        public bool RemoveButtonEnable
        {
            get
            {
                return _RemoveButtonEnable;
            }
            set
            {
                Set(ref _RemoveButtonEnable, value);
            }
        }
        #endregion

        #region 커맨드
        public ICommand LoadedPlaylistCommand { get; private set; }
        public ICommand SelectionChangedCommand { get; private set; }
        public ICommand ItemClickCommand { get; private set; }

        public ICommand CheckListButtonClickCommand { get; private set; }
        public ICommand SynchronizeButtonClickCommand { get; private set; }
        public ICommand ReorderButtonClickCommand { get; private set; }
        public ICommand BackButtonClickCommand { get; set; }
        public ICommand SelectAllButtonClickCommand { get; private set; }
        public ICommand ResetPositionClickCommand { get; private set; }
        public ICommand RemoveButtonClickCommand { get; private set; }
        public ICommand AcceptButtonClickCommand { get; private set; }

        #endregion

        #region 커맨드 핸들러

        void SelectionChangedCommandExecute(SelectionChangedEventArgs e)
        {
            RemoveButtonEnable = SelectedItem != null;
        }

        void ItemClickCommandExecute(ItemClickEventArgs e)
        {
            var mediaInfo = e.ClickedItem as MediaInfo;
            if (string.IsNullOrEmpty(mediaInfo.OccuredError))
            {
                //로딩 패널 표시
                var loader = ResourceLoader.GetForCurrentView();
                var msg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
                MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);

                //재생 시작
                PlayItem(mediaInfo);
            }
        }

        private void CheckListButtonClickCommandExecute()
        {
            //선택 모드 변경시
            SelectionMode = ListViewSelectionMode.Multiple;
            MainButtonGroupVisible = false;
            CheckListButtonGroupVisible = true;
            ReorderButtonGroupVisible = false;
            RemoveButtonEnable = false;
        }

        private void SynchronizeButtonClickCommandExecute()
        {
            PlaylistSource.Clear();
            LoadFiles();
        }

        private void ReorderButtonClickCommandExecute()
        {
            MainButtonGroupVisible = false;
            CheckListButtonGroupVisible = false;
            ReorderButtonGroupVisible = true;
            ReorderMode = ListViewReorderMode.Enabled;
        }

        private void BackButtonClickCommandExecute()
        {
            //선택 모드 변경
            SelectionMode = ListViewSelectionMode.None;
            ReorderMode = ListViewReorderMode.Disabled;
            MainButtonGroupVisible = true;
            CheckListButtonGroupVisible = false;
            ReorderButtonGroupVisible = false;
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

        private void ResetPositionClickCommandExecute(ListView listView)
        {
            for (int j = listView.SelectedItems.Count - 1; j >= 0; j--)
            {
                MediaInfo mi = listView.SelectedItems[j] as MediaInfo;
                mi.PausedTime = 0;
            }
            //재생시간 초기화 저장
            fileDAO.UpdatePlayList(listView.SelectedItems.AsEnumerable());

            SelectionMode = ListViewSelectionMode.None;
            MainButtonGroupVisible = true;
            CheckListButtonGroupVisible = false;
            ReorderButtonGroupVisible = false;
        }

        private void RemoveButtonClickCommandExecute(ListView listView)
        {
            for (int j = listView.SelectedItems.Count - 1; j >= 0; j--)
            {
                OnRemovePlayList(listView.SelectedItems[j] as MediaInfo);
            }

            CheckPlaylistCount();
        }

        private void AcceptButtonClickCommandExecute(ListView listView)
        {
            //다시 저장
            SaveFiles();

            MainButtonGroupVisible = true;
            CheckListButtonGroupVisible = false;
            ReorderButtonGroupVisible = false;
            //성공 처리를 위해 직접 화면 갱신
            _ReorderMode = ListViewReorderMode.Disabled;
            RaisePropertyChanged("ReorderMode");
        }

        #endregion

        public PlaylistViewModel(FileDAO fileDAO, SettingDAO settingDAO)
        {
            this.fileDAO = fileDAO;
            this.GeneralSetting = settingDAO.SettingCache.General;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();
        }

        private void CreateModels()
        {
            loadingModel = LoadingMode.Caching;
            PlaylistSource = new ObservableCollection<MediaInfo>();
            MainButtonGroupVisible = true;
            CheckListButtonGroupVisible = false;
            ReorderButtonGroupVisible = false;
        }

        private void CreateCommands()
        {
            //재생목록 로드
            SelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(SelectionChangedCommandExecute);
            ItemClickCommand = new RelayCommand<ItemClickEventArgs>(ItemClickCommandExecute);

            CheckListButtonClickCommand = new RelayCommand(CheckListButtonClickCommandExecute);
            SynchronizeButtonClickCommand = new RelayCommand(SynchronizeButtonClickCommandExecute);
            ReorderButtonClickCommand = new RelayCommand(ReorderButtonClickCommandExecute);
            BackButtonClickCommand = new RelayCommand(BackButtonClickCommandExecute);
            SelectAllButtonClickCommand = new RelayCommand<ListView>(SelectAllButtonClickCommandExecute);
            ResetPositionClickCommand = new RelayCommand<ListView>(ResetPositionClickCommandExecute);
            RemoveButtonClickCommand = new RelayCommand<ListView>(RemoveButtonClickCommandExecute);
            AcceptButtonClickCommand = new RelayCommand<ListView>(AcceptButtonClickCommandExecute);
        }

        /// <summary>
        /// 다른 뷰모델들로 부터 수신된 메세지를 처리한다.
        /// </summary>
        private void RegisterMessages()
        {
            //재생목록 메세지 수신
            MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
                switch (msg.Key)
                {
                    case "Activated":
                        if (loadingModel != LoadingMode.None)
                        {
                            PlaylistSource.Clear();
                            LoadFiles();
                        }
                        break;
                    case "MoveToSection":
                        //재생목록 섹션으로 이동
                        MoveToSection(msg.GetValue<HubSection>());
                        break;
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        if (SelectionMode != ListViewSelectionMode.None)
                        {
                            //선택 모드 변경
                            SelectionMode = ListViewSelectionMode.None;
                            MainButtonGroupVisible = true;
                            CheckListButtonGroupVisible = false;
                            ReorderButtonGroupVisible = false;
                        }
                        else
                        {
                            //종료 확인
                            MessengerInstance.Send<Message>(new Message("ConfirmTermination", null), MainViewModel.NAME);
                        }
                        break;
                    case "FolderDeleted":
                        //허브섹션을 반대 방향으로 들어오는 경우, 모든 비디오 DB가 초기화 되어 있지 않으면 초기화를 시킨다.
                        MessengerInstance.Send<Message>(new Message("CheckFolderSyncForPlaylist", null), AllVideoViewModel.NAME);
                        //탐색기에서 삭제된 폴더 삭제 Trigger
                        //재생 목록 생성전 모든 비디오 파일과 재생 목록을 동기화하여 존재하지 않는 파일을 재생목록에서 제거
                        fileDAO.CleanPlayList();
                        //로딩 요청 상태 변경
                        loadingModel = LoadingMode.Caching;
                        break;
                    case "PlayItem":
                        PlayItem(msg.GetValue<MediaInfo>());
                        break;
                    case "PlayList":
                        loadingModel = LoadingMode.Caching;
                        var list = msg.GetValue<IEnumerable<MediaInfo>>();
                        MessengerInstance.Send<Message>(new Message("MoveToPlaylistSection", list.Count() > 1), CCPlayerViewModel.NAME);
                        //재생 목록 생성전 모든 비디오 파일과 재생 목록을 동기화하여 존재하지 않는 파일을 재생목록에서 제거
                        fileDAO.CleanPlayList();
                        //재생 목록 생성
                        MakePlaylist(list);
                        break;
                    case "UpdatePausedTime":
                        var source = msg.GetValue<MediaInfo>();
                        fileDAO.UpdatePlayList(new MediaInfo[] { source });
                        //화면 업데이트 처리
                        var item = PlaylistSource.FirstOrDefault(x => x.Path == source.Path);
                        if (item != null)
                        {
                            item.RunningTime = source.RunningTime;
                            item.PausedTime = source.PausedTime;
                        }
//                        System.Diagnostics.Debug.WriteLine(string.Format("재생시간 업데이트 : {0}", TimeSpan.FromSeconds(source.PausedTime)));
                        break;
                    case "FileAssociation":
                        var value = msg.GetValue<FileActivatedEventArgs>();
                        if (value.Files != null && value.Files.Count > 0)
                        {
                            var file = value.Files.FirstOrDefault();

                            if (file != null && file.IsOfType(Windows.Storage.StorageItemTypes.File))
                            {
                                var mi = new MediaInfo((StorageFile)file);

                                //로딩 패널 표시
                                var loader = ResourceLoader.GetForCurrentView();
                                var loadingMsg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
                                MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(loadingMsg, true)), MainViewModel.NAME);
                                
                                DispatcherHelper.CheckBeginInvokeOnUI(async () =>
                                {
                                    try
                                    {
                                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(mi.ParentFolderPath);
                                        IReadOnlyList<StorageFile> fileStorageList = await folder.GetFilesAsync();
                                        List<StorageFile> subtitleList = fileStorageList.Where(x => x.IsSubtitleFile()).ToList();

                                        var pathName = mi.Path.Remove(mi.Path.Length - Path.GetExtension(mi.Path).Length);
                                        //자막 검색
                                        foreach (var ext in CCPlayerConstant.SUBTITLE_FILE_SUFFIX)
                                        {
                                            StorageFile subtitleFile = null;
                                            try
                                            {
                                                subtitleFile = new List<StorageFile>(subtitleList).FirstOrDefault(x => Path.GetExtension(x.Path).ToUpper() == ext.ToUpper()
                                                    && x.Path.Length > ext.Length && x.Path.Remove(x.Path.Length - ext.Length).ToUpper().Contains(pathName.ToUpper()));
                                            }
                                            catch (Exception) { }

                                            if (subtitleFile != null)
                                            {
                                                subtitleList.Remove(subtitleFile);

                                                //자막을 미디어 파일에 연결
                                                mi.AddSubtitle(new SubtitleInfo(subtitleFile));
                                            }
                                        }
                                    }
                                    catch (System.UnauthorizedAccessException)
                                    { }

                                    //재생 처리
                                    MessengerInstance.Send<Message>(new Message("Play", mi), CCPlayerViewModel.NAME);
                                });
                            }
                        }
                        break;
                    case "ShowErrorFile":
                        if (PlaylistSource.Any())
                        {
                            var kv = msg.GetValue<KeyValuePair<string, MediaInfo>>();
                            var mi = PlaylistSource.FirstOrDefault(f => f.Path == kv.Value.Path);
                            if (mi != null)
                            {
                                mi.OccuredError = kv.Key + "\n";
                            }
                        }
                        break;
                    case "RemovePlayList":
                        OnRemovePlayList(msg.GetValue<MediaInfo>());
                        break;
                }
            });
        }

        /// <summary>
        /// 화면에 데이터를 로딩한다.
        /// </summary>
        async void LoadFiles()
        {
            Stopwatch st = null;
            if (Debugger.IsAttached)
            {
                st = new Stopwatch();
                st.Start();
            }

            await ThreadPool.RunAsync(async handler =>
            {
                //완료 기표
                loadingModel = LoadingMode.None;
                //재생목록 DB쿼리 (1 ~ 100개, 자막도 로드)
                var miList = new List<MediaInfo>();
                fileDAO.LoadPlayList(miList, 100, 0, true);
                //화면에 반영
                foreach (var mi in miList)
                {
                    await DispatcherHelper.RunAsync(() => { PlaylistSource.Add(mi); });
                }

                await DispatcherHelper.RunAsync(() => 
                {
                    CheckListButtonEnable = miList.Count > 0;
                    ReorderButtonEnable = miList.Count > 1;
                });
            });

            if (Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine("재생목록 로드 : " + st.Elapsed);
            }
        }

        /// <summary>
        /// 화면의 데이터를 다시 저장한다.
        /// </summary>
        async void SaveFiles()
        {
            await ThreadPool.RunAsync(handler =>
            {
                //재생목록 DB에 파일 추가
                fileDAO.InsertPlayList(PlaylistSource.Reverse());
            });
        }

        /// <summary>
        /// 파일 재생을 요청하고, 단건에 대해 재생목록에 추가한다.
        /// </summary>
        /// <param name="fileInfo"></param>
        void PlayItem(MediaInfo fileInfo)
        {
            //리오더 모드가 아닐때만 처리
            if (ReorderMode == ListViewReorderMode.Disabled
                && string.IsNullOrEmpty(fileInfo.OccuredError))
            {
                //UI에 로딩된 파일이면 업데이트를 위해 데이터 취득
                var mi = PlaylistSource.FirstOrDefault(x => x.Path == fileInfo.Path);
                //화면에 로딩되지 않은 상태이면 DB에서 로딩
                if (mi == null)
                {
                    mi = fileDAO.GetPlayList(fileInfo.Path);
                }
                
                //재생 처리
                MessengerInstance.Send<Message>(new Message("Play", mi), CCPlayerViewModel.NAME);
            }
        }

        /// <summary>
        /// 재생목록을 생성한다.
        /// </summary>
        /// <param name="mediaInfoList"></param>
        void MakePlaylist(IEnumerable<MediaInfo> mediaInfoList)
        {
            if (mediaInfoList != null && mediaInfoList.Count() > 0)
            {
                //재생가능 목록 필터
                var list = mediaInfoList.Where(x => string.IsNullOrEmpty(x.OccuredError));
                if (list != null && list.Any())
                {
                    //순서 뒤집기
                    list = list.Reverse<MediaInfo>();
                    InsertPlayList(list);

                    //재생 처리
                    var playItem = list.LastOrDefault();
                    PlayItem(playItem);
                }
            }
        }

        /// <summary>
        /// 재생목록을 DB에 반영한다.
        /// </summary>
        /// <param name="mediaInfoList"></param>
        private void InsertPlayList(IEnumerable<MediaInfo> mediaInfoList)
        {
            //재생목록 DB에 파일 추가
            fileDAO.InsertPlayList(mediaInfoList);
            //101부터 100개, 자막은 로딩하지 않는다.
            var miList = new List<MediaInfo>();
            fileDAO.LoadPlayList(miList, 100, 100, false);
            if (miList.Count > 0)
            {
                //101부터는 삭제
                fileDAO.DeletePlayList(miList);     
            }
        }

        private void MoveToSection(HubSection section)
        {
            //허브를 재생목록으로 이동
            var hub = Lime.Xaml.Helpers.ElementHelper.FindVisualParent<Hub>(section);
            var playlistSection = hub.Sections.FirstOrDefault(x => x.ViewModelName() == PlaylistViewModel.NAME);

            if (playlistSection != null)
            {
                hub.ScrollToSection(playlistSection);
            }
        }

        private void OnRemovePlayList(MediaInfo mediaInfo)
        {
            for (int i = PlaylistSource.Count - 1; i >= 0; i--)
            {
                if (mediaInfo.Path == PlaylistSource[i].Path)
                {
                    PlaylistSource.RemoveAt(i);
                    fileDAO.DeletePlayList(new MediaInfo[] { mediaInfo });
                    break;
                }
            }

            CheckPlaylistCount();
        }

        private void CheckPlaylistCount()
        {
            CheckListButtonEnable = PlaylistSource.Count > 0;
            ReorderButtonEnable = PlaylistSource.Count > 1;

            if (PlaylistSource.Count == 0)
            {
                SelectionMode = ListViewSelectionMode.None;
                MainButtonGroupVisible = true;
                CheckListButtonGroupVisible = false;
                ReorderButtonGroupVisible = false;
            }
        }
    }
}
