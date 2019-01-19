using CCPlayer.WP81.Extensions;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Strings;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CCPlayer.WP81.ViewModel
{
    public enum LoadingMode
    {
        None,
        Caching,
        Syncing
    }

    public class AllVideoViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(AllVideoViewModel).Name;
        #region 데이터 모델

        public Settings.GeneralSetting GeneralSetting { get; private set; }
        private LoadingMode loadingMode;
        private FolderDAO folderDAO;
        private FileDAO fileDAO;
        public CollectionViewSource AllVideoCollection { get; set; }
        private ObservableCollection<JumpListGroup<MediaInfo>> AllVideoSource { get; set; }
        private List<MediaInfo> playlist;
        public MediaInfo SelectedItem { get; set; }
        public double HubWidth { get { return Window.Current.Bounds.Width * 0.84; } }

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

        private bool _SynchronizeButtonEnable;
        public bool SynchronizeButtonEnable
        {
            get
            {
                return _SynchronizeButtonEnable;
            }
            set
            {
                Set(ref _SynchronizeButtonEnable, value);
            }
        }

        private bool _MediaSearchButtonEnable;
        public bool MediaSearchButtonEnable
        {
            get
            {
                return _MediaSearchButtonEnable;
            }
            set
            {
                Set(ref _MediaSearchButtonEnable, value);
            }
        }

        private string _SearchFolderPath;
        public string SearchFolderPath
        {
            get
            {
                return _SearchFolderPath;
            }
            set
            {
                if (Set(ref _SearchFolderPath, value))
                {
                    SearchFolderPathVisibility = string.IsNullOrEmpty(value) ? Windows.UI.Xaml.Visibility.Collapsed : Windows.UI.Xaml.Visibility.Visible;
                }
            }
        }

        private Visibility _SearchFolderPathVisibility;
        public Visibility SearchFolderPathVisibility
        {
            get
            {
                return _SearchFolderPathVisibility;
            }
            set
            {
                Set(ref _SearchFolderPathVisibility, value);
            }
        }

        #endregion

        #region 커맨드
        
        public ICommand SelectionChangedCommand { get; private set; }
        public ICommand ItemClickCommand { get; private set; }

        public ICommand CheckListButtonClickCommand { get; private set; }
        public ICommand MediaSearchButtonClickCommand { get; private set; }
        public ICommand SynchronizeButtonClickCommand { get; private set; }
        public ICommand BackButtonClickCommand { get; set; }
        public ICommand SelectAllButtonClickCommand { get; set; }
        public ICommand PlayButtonClickCommand { get; set; }

        #endregion

        #region 커맨드 핸들러

        void SelectionChangedCommandExecute(SelectionChangedEventArgs e)
        {
            if (SelectionMode == ListViewSelectionMode.Multiple)
            {
                PlayButtonEnable = SelectedItem != null;
            }
        }

        void ItemClickCommandExecute(ItemClickEventArgs e)
        {
            var playItem = e.ClickedItem as MediaInfo;
            if (string.IsNullOrEmpty(playItem.OccuredError))
            {
                var loader = ResourceLoader.GetForCurrentView();
                var msg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
                //재생목록 추가 표시
                playItem.IsAddedPlaylist = true;
                //로딩 패널 표시
                MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);
                //재생 요청
                MessengerInstance.Send<Message>(new Message("PlayList", new MediaInfo[] { playItem }), PlaylistViewModel.NAME);
            }
        }

        private void CheckListButtonClickCommandExecute()
        {
            //선택 모드 변경시
            SelectionMode = ListViewSelectionMode.Multiple;
            ButtonGroupVisible = false;
            PlayButtonEnable = false;
        }

        private void MediaSearchButtonClickCommandExecute()
        {
            MessengerInstance.Send<Message>(new Message("CheckSearchElement", true), MainViewModel.NAME);
            MessengerInstance.Send<Message>(new Message("SearchOpened", true), MediaSearchViewModel.NAME);
        }

        private void SynchronizeButtonClickCommandExecute()
        {
            loadingMode = LoadingMode.Syncing;
            ReloadAllVideo();
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
            //로딩 패널 표시
            var loader = ResourceLoader.GetForCurrentView();
            var msg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
            MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);
            //선택된 리스트 객체 생성 (아래에서 SelectionMode를 변경하므로, 반드시 ToList()등으로 객체를 복제해야함)
            var mediaInfoList = listView.SelectedItems.Cast<MediaInfo>().Where(x => string.IsNullOrEmpty(x.OccuredError)).ToList();

            foreach(var mi in mediaInfoList)
            {
                //재생목록 추가 표시
                mi.IsAddedPlaylist = true;
            }

            MessengerInstance.Send<Message>(new Message("PlayList", mediaInfoList), PlaylistViewModel.NAME);
            //버튼 초기화
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;
        }

        #endregion

        public AllVideoViewModel(FolderDAO folderDAO, FileDAO fileDAO, SettingDAO settingDAO)
        {
            this.folderDAO = folderDAO;
            this.fileDAO = fileDAO;
            this.GeneralSetting = settingDAO.SettingCache.General;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();
        }

        private void CreateModels()
        {
            AllVideoSource = new ObservableCollection<JumpListGroup<MediaInfo>>();
            AllVideoCollection = new CollectionViewSource();
            AllVideoCollection.IsSourceGrouped = true;
            AllVideoCollection.Source = AllVideoSource;
            loadingMode = LoadingMode.Caching;            
            ButtonGroupVisible = true;
            _SearchFolderPathVisibility = Visibility.Collapsed;
        }

        private void CreateCommands()
        {
            SelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(SelectionChangedCommandExecute);
            ItemClickCommand = new RelayCommand<ItemClickEventArgs>(ItemClickCommandExecute);

            //메인버튼
            CheckListButtonClickCommand = new RelayCommand(CheckListButtonClickCommandExecute);
            MediaSearchButtonClickCommand = new RelayCommand(MediaSearchButtonClickCommandExecute);
            SynchronizeButtonClickCommand = new RelayCommand(SynchronizeButtonClickCommandExecute);
            //서브버튼
            BackButtonClickCommand = new RelayCommand(BackButtonClickCommandExecute);
            SelectAllButtonClickCommand = new RelayCommand<ListView>(SelectAllButtonClickCommandExecute);
            PlayButtonClickCommand = new RelayCommand<ListView>(PlayButtonClickCommandExecute);
        }

        private void RegisterMessages()
        {

            //모든 비디오 메세지 수신
            MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
                if (!GeneralSetting.UseAllVideoSection)
                {
                    return;
                }
                switch(msg.Key)
                {
                    case "Activated":
                        if (loadingMode != LoadingMode.None)
                        {
                            ReloadAllVideo();
                        }
                        break;
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        if (SelectionMode != ListViewSelectionMode.None)
                        {
                            //선택 모드 변경
                            SelectionMode = ListViewSelectionMode.None;
                            ButtonGroupVisible = true;
                        }
                        else
                        {
                            //종료 확인
                            MessengerInstance.Send<Message>(new Message("ConfirmTermination", null), MainViewModel.NAME);
                        }
                        break;
                    case "FolderAdded":
                        //탐색기에서 새로운 폴더추가 Trigger
                        loadingMode = LoadingMode.Syncing;
                        break;
                    case "FolderDeleted":
                        //탐색기에서 추가된 폴더 삭제 Trigger
                        loadingMode = LoadingMode.Syncing;
                        break;
                    case "ShowErrorFile":
                        var list = AllVideoSource.Where(g => g.Any()).SelectMany(x => x);
                        if (list.Any())
                        {
                            var kv = msg.GetValue<KeyValuePair<string, MediaInfo>>();
                            var mi = list.FirstOrDefault(f => f.Path == kv.Value.Path);
                            if (mi != null)
                            {
                                mi.OccuredError = kv.Key + "\n";
                            }
                        }
                        break;
                    case "CheckFolderSyncForPlaylist":
                        if (loadingMode == LoadingMode.Syncing)
                        {
                            //미리 강제 초기화
                            List<FolderInfo> folderList = null;
                            InitializeAllVideos(out folderList);
                        }
                        break;
                }
            });
        }

        public void LoadFilesRecursively(StorageFolder storageFolder, Action<IEnumerable<StorageFile>> action)
        {
            if (storageFolder != null)
            {
                //현재 경로 화면 출력
                DispatcherHelper.CheckBeginInvokeOnUI(() => { SearchFolderPath = storageFolder.Path; });

                try
                {
                    //파일 처리
                    var fileList = storageFolder.GetFilesAsync().AsTask().Result;
                    if (fileList != null && fileList.Any())
                    {
                            action.Invoke(fileList);
                    }

                    //하위 폴더 검색
                    var folderList = storageFolder.GetFoldersAsync().AsTask().Result;
                    foreach (var f in folderList)
                    {
                        LoadFilesRecursively(f, action);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.StackTrace);
                }
            }
        }

        public void AddAllVideoJumpList(IEnumerable<StorageFile> fileList)
        {
            var mediaFiles = fileList.Where(x => x.IsVideoFile());//.OrderBy(x => x.DateCreated);

            if (mediaFiles.Any())
            {
                var mediaInfoList = new List<MediaInfo>();
                var subtitleFiles = fileList.Where(x => x.IsSubtitleFile()).ToList();
                
                foreach (var file in mediaFiles)
                {
                    var mi = new MediaInfo(file);

                    //비동기 모드로 자막 정보 로드
                    var asyncAction = ThreadPool.RunAsync((handler) =>
                    {
                        //재생목록 추가여부 표시
                        mi.IsAddedPlaylist = playlist.Any(x => x.Path == mi.Path);

                        var fileName = file.Path.Remove(mi.Path.Length - Path.GetExtension(mi.Path).Length).ToUpper();
                        foreach (var ext in CCPlayerConstant.SUBTITLE_FILE_SUFFIX)
                        {
                            StorageFile subtitleFile = null;
                            try
                            {
                                //System.InvalidOperationException를 방지하기 위해서 새롭게 리스트를 생성
                                subtitleFile = new List<StorageFile>(subtitleFiles).FirstOrDefault(
                                    x => x.Path.Length > ext.Length 
                                    && Path.GetExtension(x.Path).ToUpper() == ext.ToUpper()
                                    && x.Path.Remove(x.Path.Length - ext.Length).ToUpper().Contains(fileName));
                            }
                            catch (Exception) { }    
                            
                            if (subtitleFile != null)
                            {
                                subtitleFiles.Remove(subtitleFile);
                                //자막을 미디어 파일에 연결
                                mi.AddSubtitle(new SubtitleInfo(subtitleFile));
                            }
                        }

                        if (mi.SubtitleFileList != null)
                        {
                            //미디어에 연결된 자막목록을 DB에 등록한다.
                            fileDAO.InsertSubtitles(mi);
                        }
                    });

                    //미디어 파일을 그룹으로 생성할 리스트에 추가
                    mediaInfoList.Add(mi);
                }

                //DB 등록
                fileDAO.InsertMedia(mediaInfoList);

                //그룹 변환
                var ng = mediaInfoList.ToAlphaGroups(x => x.Name);
                //그룹 변환 포인터로 부터 데이터 복사
                var group = new ObservableCollection<JumpListGroup<MediaInfo>>(ng);

                foreach (var jg in group)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        var curGrp = AllVideoSource.FirstOrDefault(x => (string)x.Key == (string)jg.Key);

                        if (curGrp == null)
                        {
                            AllVideoSource.Add(jg);
                        }
                        else
                        {
                            if (jg.Count > 0)
                            {
                                foreach (var fi in jg)
                                {
                                    //동일한 파일이 존재하면 추가하지 않음.
                                    if (curGrp.Any(x => x.Path.ToUpper() == fi.Path.ToUpper()
                                        && x.Name.ToUpper() == fi.Name.ToUpper())) continue;

                                    //삽입할 위치를 검색하여 해당 위치에 새로운 파일을 추가한다.
                                    int idx = curGrp.IndexOf(curGrp.FirstOrDefault(x => string.Compare(fi.Name, x.Name) < 0));
                                    if (idx == -1)
                                    {
                                        curGrp.Add(fi);
                                    }
                                    else
                                    {
                                        if (curGrp.Any(x => x.Name == fi.Name))
                                        {
                                            fi.Name = string.Format("{0} ({1})", fi.Name, Path.GetPathRoot(fi.Path));
                                        }

                                        curGrp.Insert(idx, fi);
                                    }
                                }
                            }
                        }
                    });
                }

                if (mediaInfoList.Count > 0)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() => { EnableButtons(true); });
                }
            }
        }

        async void ReloadAllVideo()
        {
            Stopwatch st = null;
            var loader = ResourceLoader.GetForCurrentView();
            
            //앱바에 시작 상태 통지 
            EnableButtons(false);

            if (Debugger.IsAttached)
            {
                st = new Stopwatch();
                st.Start();
            }

            await ThreadPool.RunAsync(async handler =>
            {
                var mfiList = new List<MediaInfo>();
                //재생목록 로드 (이미 추가된 파일인지 표시를 위해)
                playlist = new List<MediaInfo>();
                fileDAO.LoadPlayList(playlist, 100, 0, false);

                //캐시 로딩의 경우 DB로 부터 캐시를 먼저 로드
                if (loadingMode == LoadingMode.Caching)
                {
                    fileDAO.LoadAllVideoList(mfiList, playlist);
                }

                await DispatcherHelper.RunAsync(() =>
                {
                    //목록 초기화
                    AllVideoSource.Clear();
                    //캐시 로드인 경우 로딩 경로를 "캐시에서 로딩" 으로 변경
                    if (loadingMode == LoadingMode.Caching && mfiList.Count > 0)
                    {
                        SearchFolderPath = loader.GetString("Cache");
                    }
                });

                bool isLoaded = false;
                if (loadingMode == LoadingMode.Caching && mfiList.Count > 0)
                {
                    //로딩 표시
                    loadingMode = LoadingMode.None;
                    //캐시 로딩 처리...
                    var jumpGroupList = mfiList.ToAlphaGroups(x => x.Name);
                    foreach (var jumpGroup in jumpGroupList)
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            AllVideoSource.Add(jumpGroup);
                        });
                    }
                    //캐시 로딩 완료 처리
                    isLoaded = true;
                }
                else
                {
                    //로딩 표시
                    loadingMode = LoadingMode.None;

                    List<FolderInfo> folderList = null;
                    InitializeAllVideos(out folderList);

                    //폴더 목록이 비어 있으면 로딩완료 처리
                    isLoaded = folderList.Count == 0;

                    //캐시 로딩이 아닌경우 (디렉토리 풀스캔)
                    //폴더내 파일 로딩 처리
                    if (!isLoaded)
                    {
                        foreach (var fi in folderList)
                        {
                            LoadFilesRecursively(await fi.GetStorageFolder(true), AddAllVideoJumpList);
                        }

                        isLoaded = true;
                    }
                }

                if (isLoaded)
                {
                    //화면 로딩 상태 제거 (캐시로딩 또는 캐시로딩은 아니지만, 로딩할 폴더 목록이 없는 경우)
                    await DispatcherHelper.RunAsync(() => 
                    {
                        //진행바 및 현재 탐색 폴더 표시 삭제
                        SearchFolderPath = string.Empty;
                        //우측 상단 버튼 그룹 제어
                        EnableButtons(true); 
                        //시크 데이터 정리
                        //fileDAO.DeleteSeekingData();
                        //재생 목록 정리
                        fileDAO.CleanPlayList();
                    });
                }
                
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("전체 비디오 로딩 완료 : " + st.Elapsed);
                }

                //전체 로딩 후 생성 요청...
                MessengerInstance.Send<Message>(new Message("CheckSearchElement", null), MainViewModel.NAME);
            });
        }

        private void InitializeAllVideos(out List<FolderInfo> folderList)
        {
            folderList = new List<FolderInfo>();
            //루트 폴더 목록을 로드
            folderDAO.LoadRootFolderList(folderList, null, null, false);
            //모든 비디오 초기화
            fileDAO.DeleteAllVideos();
            //모든 자막 초기화
            fileDAO.DeleteSubtitles();
        }

        private void EnableButtons(bool isEnable)
        {
            if (isEnable)
            {
                var hasItem = AllVideoSource.Where(g => g.Any()).SelectMany(x => x).Any();
                CheckListButtonEnable = hasItem;
                SynchronizeButtonEnable = true;
                MediaSearchButtonEnable = hasItem;

                if (_MediaSearchButtonEnable)
                {
                    //비디오 목록의 조회가 완료되었음을 통지. (미디어 검색 패널 추가 허용)
                    MessengerInstance.Send<Message>(new Message("PanelAddingAllowed", "Search"), MainViewModel.NAME);
                }
            }
            else
            {
                CheckListButtonEnable = false;
                SynchronizeButtonEnable = false;
                MediaSearchButtonEnable = false;
            }
        }
    }
}
