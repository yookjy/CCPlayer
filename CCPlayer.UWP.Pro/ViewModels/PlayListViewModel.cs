using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Threading;
using Lime.Xaml.Helpers;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage.Search;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.ViewModels
{
    public class PreparedData
    {
        public DecoderTypes DecoderType { get; set; }
        public StorageItemInfo StorageItemInfo { get; set; }
        public int SyncLoadPlayListFileCount { get; set; }
    }

    public class PlayListViewModel : CCPThumbnailViewModelBase
    {
        private const int LOAD_ITEM_COUNT = 5;

        [DoNotNotify]
        public ObservableCollection<PlayListFile> PlayListFileSource { get; set; }

        [DoNotNotify]
        public PreparedData PreparedData { get; set; }

        //재생목록관련 DAO
        [DependencyInjection]
        private PlayListDAO playListDAO;

        public SelectionChangedEventHandler PlayListListViewSelectionChangedEventHandler;

        public TappedEventHandler RenameTappedEventHandler;
        public TappedEventHandler DeletePlayListTappedEventHandler;
        public TappedEventHandler RefreshTappedEventHandler;
        public TappedEventHandler SelectItemTappedEventHandler;

        public TappedEventHandler BackNormalButtonSetTappedEventHandler;
        public TappedEventHandler AllCheckItemTappedEventHandler;
        public TappedEventHandler ResetPausedTimeItemTappedEventHandler;
        public TappedEventHandler DeleteItemTappedEventHandler;

        private QueryOptions _SubtitleFileQueryOptions;
        private Dictionary<string, List<string>> _SubtitlesList;

        public bool IsStopLoadingIndicator { get; set; }

        private PlayList _CurrentPlayList;
        [DoNotNotify]
        public PlayList CurrentPlayList
        {
            get { return _CurrentPlayList; }
            set
            {
                Set(ref _CurrentPlayList, value);
                IsHideEditPlayListMenu = (value.Seq == 1);
                LoadPlayList();
            }
        }

        public FlyoutModelBase<PlayList> UpdatePlayList { get; set; }

        public string OldPlayListName { get; set; }

        public bool IsHideEditPlayListMenu { get; set; }

        public int SelectedIndex { get; set; } = -1;

        public bool IsSelected { get; set; }

        public ListViewSelectionMode SelectionMode { get; set; } = ListViewSelectionMode.Single;

        public bool IsAddFolder { get; set; }

        public bool IsActionButtonSet { get; set; }

        private bool? _IsReorderMode;
        [DoNotNotify]
        public bool? IsReorderMode
        {
            get { return _IsReorderMode; }
            set
            {
                Set(ref _IsReorderMode, value);
                if (value == false)
                {
                    UpdatePlayListReorder();

                    if (_ChangedIndexList.Count > 0)
                    {
                        int min = _ChangedIndexList.Min();
                        int max = _ChangedIndexList.Max();
                        
                        List<KeyValuePair<int, PlayListFile>> list = new List<KeyValuePair<int, PlayListFile>>();
                        for (int i = max; i >= min; i--)
                        {
                            list.Add(new KeyValuePair<int, PlayListFile>(i, PlayListFileSource[i]));
                            PlayListFileSource.RemoveAt(i);
                        }

                        list.Reverse();
                        foreach (var item in list)
                        {
                            item.Value.NewOrderNo = 0;
                            PlayListFileSource.Insert(item.Key, item.Value);
                        }
                    }
                }
                _ChangedIndexList.Clear();
            }
        }

        private DecoderTypes _RequestedDecoderType;

        private List<int> _ChangedIndexList ;

        #region Implement abstract method 
        protected override void FakeIocInstanceInitialize()
        {
            playListDAO = null;
        }

        protected override void CreateModel()
        {
            PlayListFileSource = new ObservableCollection<PlayListFile>();
            PlayListFileSource.CollectionChanged += PlayListFileSource_CollectionChanged;
            _SubtitlesList = new Dictionary<string, List<string>>();
            _ChangedIndexList = new List<int>();
            _IsReorderMode = false;
            var resource = ResourceLoader.GetForCurrentView();
            UpdatePlayList = new FlyoutModelBase<PlayList>
            {
                PrimaryButtonText = resource.GetString("Button/Save/Content"),
                PrimaryTitle = resource.GetString("PlayList/New/Title"),
                PrimaryButtonCommand = new RelayCommand<TextBox>(SavePlayListTappedCommandExecute)
            };

            _SubtitleFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);
        }

        protected override void InitializeViewModel()
        {
        }

        protected override void RegisterEventHandler()
        {
            PlayListListViewSelectionChangedEventHandler = PlayListListViewSelectionChanged;

            RenameTappedEventHandler = RenameTapped;
            DeletePlayListTappedEventHandler = DeletePlayListTapped;
            RefreshTappedEventHandler = RefreshTapped;
            SelectItemTappedEventHandler = SelectItemTapped;

            BackNormalButtonSetTappedEventHandler = BackNormalButtonSetTapped;
            AllCheckItemTappedEventHandler = AllCheckItemTapped;
            ResetPausedTimeItemTappedEventHandler = ResetPausedTimeItemTapped;
            DeleteItemTappedEventHandler = DeleteItemTapped;
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<PlayList>(this, "ChangePlayList", (playList) =>
            {
                CurrentPlayList = playList;
            });

            MessengerInstance.Register<bool>(this, "ResetSelectionMode", (val) =>
            {
                SelectedIndex = -1;
                BackNormalButtonSetTapped(null, null);
            });

            MessengerInstance.Register<Message>(this, "PrepareLoadPlayListFile", (message) =>
            {
                PreparedData = new PreparedData
                {
                    //재생목록에서 지금재생중으로 이동 직전에 동기로드 카운트 설정 
                    //지금 재생할 비디오와 다음 재생 프리뷰용으로 총 2개
                    SyncLoadPlayListFileCount = 2,
                    DecoderType = message.GetValue<DecoderTypes>("DecoderType"),
                    StorageItemInfo = message.GetValue<StorageItemInfo>("StorageItemInfo")
                };
            });

            //FileAssociation에서 이미 "지금 재생중"이 선택되어진 상태에서 넘어온다.
            MessengerInstance.Register<Message>(this, "SelectPlayListFile", async (message) =>
             {
                 BackNormalButtonSetTapped(null, null);
                 _RequestedDecoderType = message.GetValue<DecoderTypes>("DecoderType");
                var storageItemInfo = message.GetValue<StorageItemInfo>("StorageItemInfo");
                var orderNo = PlayListFileSource.LastOrDefault() != null ? (int)PlayListFileSource.LastOrDefault().OrderNo : 0;

                //새롭게 추가된
                List<PlayListFile> playListFileList = new List<PlayListFile>();
                playListDAO.LoadPlayListFiles(CurrentPlayList, orderNo, (playListFile) =>
                {

                    //이벤트 등록 및 표시명 설정
                    SetPlayListFile(playListFile);
                    //리스트에 바인딩
                    if (PlayListFileSource.All(x => x.Path != playListFile.Path))
                    {
                        PlayListFileSource.Add(playListFile);
                    }
                    else if (PlayListFileSource.Any(x => x.Path == playListFile.Path && x.OrderNo != playListFile.OrderNo))
                    {
                        //기존 리스트에 존재하나 순서가 변경된 경우
                        var tmp = PlayListFileSource.First(x => x.Path == playListFile.Path && x.OrderNo != playListFile.OrderNo);
                        PlayListFileSource.Remove(tmp);
                        PlayListFileSource.Add(playListFile);
                    }
                    //추가 정보 로드
                    LoadExtraInfoAsync(playListFile);
                });

                var newIndex = PlayListFileSource.IndexOf(PlayListFileSource.LastOrDefault(x => x.Path == storageItemInfo.Path));
                if (newIndex > -1 && newIndex < PlayListFileSource.Count)
                {
                    SelectedIndex = newIndex;
                    //로딩 패널 표시
                    MessengerInstance.Send(new Message("IsOpen", true), "ShowLoadingPanel");
                    await ThreadPool.RunAsync(async handler =>
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            //재생요청
                            RequestPlayback(true);
                        });
                    }, WorkItemPriority.Normal);
                }
             });

            MessengerInstance.Register<int>(this, "NextPlayListFile", (index) =>
            {
                var nextIndex = SelectedIndex + index;
                if (nextIndex > -1 && nextIndex < PlayListFileSource.Count)
                {
                    SelectedIndex = nextIndex;
                    PlayListFileSource[SelectedIndex].PausedTime = TimeSpan.FromSeconds(0);
                    RequestPlayback(false);
                }
            });

            MessengerInstance.Register<Message>(this, "SavePlayListFile", (message) =>
            {
                var file = message.GetValue<PlayListFile>();
                playListDAO.UpdatePausedTime(file);
                //System.Diagnostics.Debug.WriteLine("PlayListViewModel : Message - SavePlayListFile =>" + file.PausedTime);
            });
        }
        #endregion

        private void PlayListFileSource_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var oldItems = e.OldItems;
            if (oldItems != null)
            {
                if (IsReorderMode == true)
                {
                    _ChangedIndexList.Add(e.OldStartingIndex);
                }
            }
            
            var items = e.NewItems;
            if (items != null)
            {
                if (IsReorderMode == true && _ChangedIndexList.Count > 0) //순서변경 모드이면서, 먼저 삭제된 아이템이 존재하여야 한다.(재로드시 동작 방지)
                {
                    _ChangedIndexList.Add(e.NewStartingIndex);
                    PlayListFileSource[e.NewStartingIndex].NewOrderNo = e.NewStartingIndex;
                }
            }
        }

        private async void RequestPlaybackWithLoadingPanel(StorageItemInfo item)
        {
            if (item != null)
            {
                //추가되는 경우가 있으므로 만일을 대비해 가장 뒤의 아이템 선택
                var newIndex = PlayListFileSource.IndexOf(PlayListFileSource.LastOrDefault(x => x.Path == item.Path));
                if (newIndex > -1 && newIndex < PlayListFileSource.Count)
                {
                    SelectedIndex = newIndex;
                    //로딩 패널 표시
                    MessengerInstance.Send(new Message("IsOpen", true), "ShowLoadingPanel");
                    await ThreadPool.RunAsync(async handler =>
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            //재생요청
                            RequestPlayback(true);
                        });
                    }, WorkItemPriority.Normal);
                }
            }
        }

        private async void LoadPlayList()
        {
            _SubtitlesList.Clear();
            //목록 초기화
            PlayListFileSource.Clear();
            //DB에서 조회
            List<PlayListFile> playListFileList = new List<PlayListFile>();
            playListDAO.LoadPlayListFiles(CurrentPlayList, playListFileList);
            if (playListFileList.Count > 0)
            {
                IsStopLoadingIndicator = false;
                int count = playListFileList.Count > PreparedData?.SyncLoadPlayListFileCount ? PreparedData.SyncLoadPlayListFileCount : playListFileList.Count;

                //재생목록 파일들의 폴더 리스트 
                foreach (var path in playListFileList.GroupBy(x => x.ParentFolderPath).Select(x => x.Key))
                {
                    _SubtitlesList.Add(path, null);
                }

                //탐색기에서 파일 선택시 선택한 파일 이후를 모두 지금 재생중에 로딩이 되므로 하나를 먼저 로드할 필요가 있음.
                await LoadPlayListFiles(playListFileList.GetRange(0, count));

                if (PlayListFileSource.Any(x => PreparedData?.StorageItemInfo.Path == x.Path))
                {
                    _RequestedDecoderType = PreparedData.DecoderType;
                    RequestPlaybackWithLoadingPanel(PreparedData.StorageItemInfo);
                    //초기화
                    PreparedData = null;
                }

                if (playListFileList.Count == count)
                {
                    //더이상 로딩할 아이템이 없으면 로딩 표시 정지
                    IsStopLoadingIndicator = true;
                }
                else
                {
                    //로딩할 아이템이 남았으면 비동기로 로딩
                    LoadPlayListFilesAsync(playListFileList.GetRange(count, playListFileList.Count - count));
                }
            }
            _ChangedIndexList.Clear();
        }

        private void SetPlayListFile(PlayListFile playListFile)
        {
            //이벤트 할당
            playListFile.Tapped = FileTapped;
            playListFile.RightTapped = FileRightTapped;
            playListFile.Holding = FileHolding;
            //저장된 이름으로 먼저 이름을 설정
            playListFile.Name = Path.GetFileName(playListFile.Path);
            //표시 이름 설정
            playListFile.SetDisplayName();
        }

        private async Task SetSubtitleList(PlayListFile playListFile)
        {
            if (_SubtitlesList.ContainsKey(playListFile.ParentFolderPath))
            {
                List<string> subtitles = _SubtitlesList[playListFile.ParentFolderPath];
                if (subtitles == null)
                {
                    try
                    {
                        var file = await playListFile.GetStorageFileAsync();
                        var currFolder = await file.GetParentAsync();
                        //var currFolder = await StorageFolder.GetFolderFromPathAsync(playListFile.ParentFolderPath);
                        if (currFolder != null)
                        {
                            var queryResult = currFolder.CreateFileQueryWithOptions(_SubtitleFileQueryOptions);
                            var list = await queryResult.GetFilesAsync();
                            subtitles = list.Select(x => x.Path).OrderBy(x => x).ToList();
                            _SubtitlesList[playListFile.ParentFolderPath] = subtitles;
                            System.Diagnostics.Debug.WriteLine($"Play list : {playListFile.ParentFolderPath} 폴더내 자막파일 {subtitles.Count}개 검색 됨...");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"{playListFile.ParentFolderPath} 폴더에 대한 접근 권한없음...");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
            }
        }

        private async void LoadExtraInfoAsync(PlayListFile playListFile)
        {
            //썸네일 및 사이즈 등 추가 데이터 로드
            await Task.Factory.StartNew(async () =>
            {
                var storageItem = await playListFile.GetStorageFileAsync();
                if (storageItem != null)
                {
                    if (playListFile.Name != storageItem.Name)
                    {
                        //이름 변경
                        playListFile.Name = storageItem.Name;
                        //표시 이름 갱신
                        playListFile.SetDisplayName();
                    }
                    playListFile.DateCreated = storageItem.DateCreated;
                    System.Diagnostics.Debug.WriteLine(storageItem.Path);
                    var basicProperties = await storageItem.GetBasicPropertiesAsync();
                    playListFile.Size = basicProperties.Size;

                    List<Thumbnail> thumbnailList = new List<Thumbnail>();

                    //기간 지난 썸네일 캐시 삭제 
                    ThumbnailDAO.DeletePastPeriodThumbnail(Settings.Thumbnail.RetentionPeriod);
                    //썸네일 데이터 로드
                    Thumbnail thumbnail = ThumbnailDAO.GetThumnail(playListFile.ParentFolderPath, playListFile.Name);
                    if (thumbnail != null)
                    {
                        thumbnailList.Add(thumbnail);
                    }
                        
                    //썸네일 로드
                    LoadThumbnailAsync(playListFile as StorageItemInfo, thumbnailList, Settings.Thumbnail.UseUnsupportedLocalFile);
                    //자막 여부 표시
                    try
                    {
                        List<string> tmp = new List<string>();
                        //DB에서 로드된 자막리스트를 다시 추가
                        if (playListFile.SubtitleList != null)
                        {
                            tmp.AddRange(playListFile.SubtitleList);
                        }

                        if (_SubtitlesList.ContainsKey(playListFile.ParentFolderPath))
                        {
                            List<string> subtitles = _SubtitlesList[playListFile.ParentFolderPath];

                            //현재 폴더내에서 검색가능한 경우, 발견된 자막리스트를 추가    
                            if (subtitles != null && subtitles.Count > 0)
                            {
                                tmp.AddRange(subtitles.Where(x => x.ToUpper().Contains(PathHelper.GetFullPathWithoutExtension(playListFile.Path).ToUpper())).ToList());
                            }
                        }

                        //모든 추가된 자막리스트로 교체
                        playListFile.SubtitleList = tmp.Distinct().ToList();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
            });
            //System.Diagnostics.Debug.WriteLine("재생목록 ExtraInfo 로드완료");
        }

        private async Task LoadPlayListFiles(List<PlayListFile> playListFileList)
        {
            for (int i = 0; i < playListFileList.Count; i++)
            {
                var playListFile = playListFileList[i];
                //파일에 필요한 정보 셋팅
                SetPlayListFile(playListFile);
                //자막 목록 미리 로드
                await SetSubtitleList(playListFile);
                //UI리스트에 추가
                PlayListFileSource.Add(playListFile);
                //추가 정보 로드
                LoadExtraInfoAsync(playListFile);
            }
        }

        private async void LoadPlayListFilesAsync(List<PlayListFile> playListFileList)
        {
            await ThreadPool.RunAsync(async handler =>
            {
                if (playListFileList != null)
                {
                    for (int i = 1; i <= playListFileList.Count; i++)
                    {
                        var playListFile = playListFileList[i - 1];
                        //파일에 필요한 정보 셋팅
                        SetPlayListFile(playListFile);
                        //자막 목록 미리 로드
                        await SetSubtitleList(playListFile);

                        int mod = i % LOAD_ITEM_COUNT;
                        if (mod == 0 || i == playListFileList.Count)
                        {
                            await DispatcherHelper.RunAsync(() =>
                            {
                                int j = mod > 0 ? i - mod : i - LOAD_ITEM_COUNT;
                                for (; j < i; j++)
                                {
                                    PlayListFileSource.Add(playListFileList[j]);
                                    //추가 정보 로드
                                    LoadExtraInfoAsync(playListFileList[j]);
                                }
                            });
                        }
                    }
                }
                
                //로딩 완료 처리
                await DispatcherHelper.RunAsync(() =>
                {
                    IsStopLoadingIndicator = true;
                    if (PlayListFileSource.Any(x => PreparedData?.StorageItemInfo.Path == x.Path))
                    {
                        _RequestedDecoderType = PreparedData.DecoderType;
                        RequestPlaybackWithLoadingPanel(PreparedData.StorageItemInfo);
                        //초기화
                        PreparedData = null;
                    }
                });
            }, WorkItemPriority.Low);
        }

        private void PlayListListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var lv = sender as ListView;
            IsSelected = lv.SelectedItems.Count > 0;
        }
        
        public void RenameTapped(object sender, TappedRoutedEventArgs e)
        {
            var button = sender as Button;
            //에러 초기화
            UpdatePlayList.ShowErrorMessage = false;
            UpdatePlayList.ErrorMessage = string.Empty;
            //입력 텍스트 초기화
            UpdatePlayList.PrimaryContent = string.Empty;
            UpdatePlayList.SecondaryContent = CurrentPlayList.Name;
            //팝업 보이기
            DialogHelper.ShowFlyout(App.Current.Resources, "PlayListFlyout", button, (ke) =>
            {
                if (ke.Key == Windows.System.VirtualKey.Enter)
                {
                    SavePlayListTappedCommandExecute(ke.OriginalSource as TextBox);
                }
            });
        }

        public async void DeletePlayListTapped(object sender, TappedRoutedEventArgs e)
        {
            //확인창
            var resource = ResourceLoader.GetForCurrentView();
            ContentDialog dlg = DialogHelper.GetSimpleContentDialog(
                resource.GetString("Message/Information/DeletePlayList"),
                resource.GetString("Message/Confirm/DeletePlayList"),
                resource.GetString("TopMenu/Common/Delete/Text"),
                resource.GetString("Button/Cancel/Content"));
            var dlgResult = await dlg.ShowAsync();
            App.ContentDlgOp = null;
            if (dlgResult == ContentDialogResult.Primary)
            {
                var result = playListDAO.DeletePlayList(CurrentPlayList);
                if (result == SQLitePCL.SQLiteResult.DONE)
                {
                    MessengerInstance.Send(new KeyValuePair<string, PlayList>("removed", CurrentPlayList), "PlayListChanged");
                }
                else
                {
                    dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/Remove"),
                        resource.GetString("Message/Error/Retry"),
                        resource.GetString("Button/Close/Content"));
                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
                }
            }
        }

        public void RefreshTapped(object sender, TappedRoutedEventArgs e) => LoadPlayList(); 

        public void SelectItemTapped(object sender, TappedRoutedEventArgs e)
        {
            SelectionMode = ListViewSelectionMode.Multiple;
            IsActionButtonSet = true;
        }

        private void BackNormalButtonSetTapped(object sender, TappedRoutedEventArgs args)
        {
            SelectionMode = ListViewSelectionMode.Single;
            IsActionButtonSet = false;
        }

        private ListView GetListView(DependencyObject dependencyObject)
        {
            Page page = ElementHelper.FindVisualParent<Page>(dependencyObject as DependencyObject);
            if (page != null)
            {
                return ElementHelper.FindVisualChild<ListView>(page);
            }
            return null;
        }

        private void AllCheckItemTapped(object sender, TappedRoutedEventArgs args)
        {
            ListView listView = GetListView(sender as DependencyObject);
            if (listView != null)
            {
                if (listView.SelectedItems.Count == 0)
                {
                    foreach (var plf in PlayListFileSource)
                    {
                        listView.SelectedItems.Add(plf);
                    }
                }
                else
                {
                    listView.SelectedItems.Clear();
                }
            }
        }
        
        private async void ResetPausedTimeItemTapped(object sender, TappedRoutedEventArgs args)
        {
            ListView listView = GetListView(sender as DependencyObject);
            if (listView != null)
            {
                var resetList = listView.SelectedItems.Cast<PlayListFile>().ToList();
                SQLitePCL.SQLiteResult result = SQLitePCL.SQLiteResult.DONE;

                foreach (var plf in resetList)
                {
                    plf.PausedTime = TimeSpan.Zero;
                    var rslt = playListDAO.UpdatePausedTime(plf);
                    if (rslt != SQLitePCL.SQLiteResult.DONE)
                    {
                        result = rslt;
                    }
                }

                if (result != SQLitePCL.SQLiteResult.DONE)
                {
                    var resource = ResourceLoader.GetForCurrentView();
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/Update"),
                        resource.GetString("Message/Error/Retry"),
                        resource.GetString("Button/Close/Content"));
                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
                }
            }
        }

        private async void DeleteItemTapped(object sender, TappedRoutedEventArgs args)
        {
            ListView listView = GetListView(sender as DependencyObject);
            if (listView != null)
            {
                var removeList = listView.SelectedItems.Cast<PlayListFile>().ToList();

                var result = playListDAO.DeletePlayListFiles(CurrentPlayList, removeList);
                if (result == SQLitePCL.SQLiteResult.DONE)
                {
                    foreach (var plf in removeList)
                    {
                        PlayListFileSource.Remove(plf);
                    }

                    //삭제 후 액션바 이전 상태로 복원
                    if (PlayListFileSource?.Count == 0)
                    {
                        BackNormalButtonSetTapped(null, null);
                    }
                }
                else
                {
                    var resource = ResourceLoader.GetForCurrentView();
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/Remove"),
                        resource.GetString("Message/Error/Retry"),
                        resource.GetString("Button/Close/Content"));
                    await dlg.ShowAsync();
                }
            }
        }
        
        private async void SavePlayListTappedCommandExecute(TextBox value)
        {
            if (VersionHelper.CheckPaidFeature())
            {
                var pl = new PlayList
                {
                    Name = value.Text.Trim(),
                    Seq = CurrentPlayList.Seq,
                };

                var resource = ResourceLoader.GetForCurrentView();
                if (string.IsNullOrEmpty(pl.Name) || CurrentPlayList.Name == pl.Name)
                {
                    value.Focus(FocusState.Keyboard);
                }
                else if (playListDAO.GetPlayList(pl.Name) != null)
                {
                    UpdatePlayList.ShowErrorMessage = true;
                    UpdatePlayList.ErrorMessage = resource.GetString("Message/Error/DuplicatedName");
                    value.SelectAll();
                    value.Focus(FocusState.Keyboard);
                }
                else
                {
                    UpdatePlayList.ShowErrorMessage = false;
                    UpdatePlayList.ErrorMessage = string.Empty;

                    var result = playListDAO.UpdatePlayList(pl);
                    if (result == SQLitePCL.SQLiteResult.DONE)
                    {
                        CurrentPlayList.Name = pl.Name;
                        RaisePropertyChanged("CurrentPlayList");
                        MessengerInstance.Send(new KeyValuePair<string, PlayList>("updated", pl), "PlayListChanged");
                        DialogHelper.HideFlyout(App.Current.Resources, "PlayListFlyout");
                    }
                    else
                    {
                        DialogHelper.HideFlyout(App.Current.Resources, "PlayListFlyout");
                        var dlg = DialogHelper.GetSimpleContentDialog(
                            resource.GetString("Message/Error/Save"),
                            resource.GetString("Message/Error/Retry"),
                            resource.GetString("Button/Close/Content"));
                        await dlg.ShowAsync();
                        App.ContentDlgOp = null;
                    }
                }
            }
        }

        private async void UpdatePlayListReorder()
        {
            if (PlayListFileSource != null && PlayListFileSource.Count > 1)
            {
                var resource = ResourceLoader.GetForCurrentView();
                var result = playListDAO.UpdatePlayListFiles(CurrentPlayList, PlayListFileSource);
                if (result != SQLitePCL.SQLiteResult.DONE)
                {
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/Update"),
                        resource.GetString("Message/Error/Retry"),
                        resource.GetString("Button/Close/Content"));
                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
                }
            }
        }

        private async void FileTapped(object sender, TappedRoutedEventArgs args)
        {
            if (SelectionMode == ListViewSelectionMode.Multiple) return;

            if (IsReorderMode == true)
            {
                IsReorderMode = false;
                return;
            }

            PlayListFile plf = null;
            FrameworkElement elem = args.OriginalSource as FrameworkElement;
            if (elem != null && (plf = elem.DataContext as PlayListFile) != null)
            {
                if (PlayListFileSource[SelectedIndex] == plf)
                {
                    //로딩 패널 표시
                    MessengerInstance.Send(new Message("IsOpen", true), "ShowLoadingPanel");
                    await ThreadPool.RunAsync(async handler =>
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            //재생요청
                            RequestPlayback(true);
                        });
                    }, WorkItemPriority.Normal);
                }
            }
        }

        private void RequestPlayback(bool resetDecoder)
        {
            var mv = SimpleIoc.Default.GetInstance<MainViewModel>();
            if (!mv.AppProtection.IsHideAppLockPanel) return;

            if (SelectedIndex > -1 && SelectedIndex < PlayListFileSource.Count)
            {
                PlayListFile prevPlayListFile = null;
                PlayListFile nextPlayListFile = null;

                if (SelectedIndex - 1 > -1)
                {
                    prevPlayListFile = PlayListFileSource[SelectedIndex - 1];
                }

                if (SelectedIndex + 1 < PlayListFileSource.Count)
                {
                    nextPlayListFile = PlayListFileSource[SelectedIndex + 1];
                }

                //디코더 타입 오버라이드
                if (resetDecoder)
                {
                    _RequestedDecoderType = Settings.Playback.DefaultDecoderType;
                }
                
                var frame = Window.Current.Content as Frame;
                if (frame != null)
                {
                    //재생 요청
                    MessengerInstance.Send(
                        new Message()
                        .Add("CurrPlayListFile", PlayListFileSource[SelectedIndex])
                        .Add("PrevPlayListFile", prevPlayListFile)
                        .Add("NextPlayListFile", nextPlayListFile)
                        .Add("DecoderType", _RequestedDecoderType)
                        ,"RequestPlayback");
                }
            }
        }

        private void FileRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            if (args.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                ShowMediaInfoFlyout(args.OriginalSource as FrameworkElement);
            }
        }

        private void FileHolding(object sender, HoldingRoutedEventArgs args)
        {
            if (args.HoldingState == Windows.UI.Input.HoldingState.Started
                && args.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (IsReorderMode != true)
                {
                    ShowMediaInfoFlyout(args.OriginalSource as FrameworkElement);
                }
            }
        }

        private void ShowMediaInfoFlyout(FrameworkElement elem)
        {
            PlayListFile plf = null;
            if (elem != null && (plf = elem.DataContext as PlayListFile) != null)
            {
                MessengerInstance.Send<Message<DecoderTypes>>(
                    new Message<DecoderTypes>(async (decoderType) =>
                    {
                        int newIndex = PlayListFileSource.IndexOf(PlayListFileSource.FirstOrDefault(x => x == plf));
                        _RequestedDecoderType = decoderType;
                        if (SelectedIndex != newIndex)
                        {
                            SelectedIndex = newIndex;
                        }
                        //로딩 패널 표시
                        MessengerInstance.Send(new Message("IsOpen", true), "ShowLoadingPanel");
                        await ThreadPool.RunAsync(async handler =>
                        {
                            await DispatcherHelper.RunAsync(() =>
                            {
                                //재생요청
                                RequestPlayback(false);
                            });
                        }, WorkItemPriority.Normal);
                    })
                    .Add("StorageItemInfo", plf)
                    .Add("ButtonName", "CodecInformation"),
                    "ShowMediaFileInformation");
            }
        }

    }
}
