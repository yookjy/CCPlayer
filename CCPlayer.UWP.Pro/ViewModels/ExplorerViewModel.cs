using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using GalaSoft.MvvmLight.Command;
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
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.ViewModels
{
    public class ExplorerViewModel : FileViewModelBase
    {
        [DependencyInjection]
        private FolderDAO _FolderDAO;

        [DependencyInjection]
        private PlayListDAO _PlayListDAO;
        
        protected override void SaveOrderBySetting()
        {
            Settings.General.SortBy = OrderBy;
        }

        public bool HasNoFolder { get; set; }

        public bool IsAddFolder { get; set; }
        
        public override bool IsStopLoadingIndicator
        {
            get { return _IsStopLoadingIndicator; }
            set
            {
                if (Set(ref _IsStopLoadingIndicator, value))
                {
                    //정렬 옵션 표시
                    ShowOrderBy = StorageItemGroupSource.SelectMany(x => x.Items).Count() > 0;

                    if (value)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            Parallel.ForEach(StorageItemGroupSource.SelectMany(x => x.Items), itemInfo =>
                            {
                                SetExtraPropertiesAsync(itemInfo);
                            });
                        }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
                        {
                            System.Diagnostics.Debug.WriteLine("폴더 및 파일 썸네일 로드 패러럴 종료");
                        });
                    }
                }
            }
        }

        public bool IsActionButtonSet { get; set; }

        public bool IsSelected { get; set; }

        public bool? AddedAfterMovePlayList { get; set; }

        public ListViewSelectionMode SelectionMode { get; set; }

        public FlyoutModelBase<string> UNCFolderFlyoutData { get; set; }

        [DoNotNotify]
        public ObservableCollection<StorageItemGroup> StorageItemGroupSource { get; set; }

        [DoNotNotify]
        public ObservableCollection<PlayList> PlayListSource { get; set; }

        public StorageItemInfo _CurrentFolderInfo;
        public StorageItemInfo CurrentFolderInfo
        {
            get { return _CurrentFolderInfo; }
            set
            {
                if (Set(ref _CurrentFolderInfo, value))
                {
                    if (value != null)
                    {
                        DisplayCurrentPath = string.IsNullOrEmpty(value.Path) ? value.DisplayName : value.Path?.Replace("\\", "/")?.Trim();
                    }
                    else
                    {
                        DisplayCurrentPath = string.Empty;
                    }
                }
            }
        }

        private QueryOptions _VideoFileQueryOptions;
        private QueryOptions _SubtitleFileQueryOptions;
        private IList<object> _SelectedItems;
                
        protected override void FakeIocInstanceInitialize()
        {
            _FolderDAO = null;
            _PlayListDAO = null;
        }

        protected override void CreateModel()
        {
            _ThumbnailListInCurrentFolder = new List<Thumbnail>();
            StorageItemGroupSource = new ObservableCollection<StorageItemGroup>();
            PlayListSource = new ObservableCollection<PlayList>();

            OrderBySource = new ObservableCollection<KeyName>();
            OrderBy = Settings.General.SortBy;

            // Get the letters representing each group for current language using CharacterGroupings class
            CreateCharacterGroupings();

            //비디오파일 쿼리 옵션 저장
            _VideoFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX);
            _VideoFileQueryOptions.SetThumbnailPrefetch(ThumbnailMode.SingleItem, (uint)ThumbnailSize.Width, ThumbnailOptions.ReturnOnlyIfCached);
            //자막파일 쿼리 옵션 저장
            _SubtitleFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);

            UNCFolderFlyoutData = new FlyoutModelBase<string>();
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<bool>(this, "BackRequested", (val) =>
            {
                ToUpperTapped(null, null);
            });

            MessengerInstance.Register<bool>(this, "ResetSelectionMode", (val) =>
            {
                BackNormalButtonSetTapped(null, null);
            });

            MessengerInstance.Register<KeyValuePair<string, PlayList>>(this, "PlayListChanged", (val) =>
            {
                //재생목록 갱신
                PlayListSource.Clear();
                PlayListSource.Add(new PlayList
                {
                    Seq = 1,
                    Name = ResourceLoader.GetForCurrentView().GetString("PlayList/NowPlaying/Text"),
                    ItemTapped = PlayListTapped
                });
                _PlayListDAO.LoadPlayList(PlayListSource, PlayListTapped);
            });
        }

        protected override void RegisterEventHandler()
        {
            Windows.UI.Xaml.Window.Current.SizeChanged += Current_SizeChanged;
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            /* 
             * 폴더&파일(그리드 뷰)이 로드된 후, 재생목록에 다녀오면 Load가 다시 일어나면서 UI가 자동 갱신(캐싱)이 되는데
             * 이때에 ItemContainerStyleSelector에 할당되어 있는 값으로 다시 초기화가 되버리는 버그가 있음.
             * (VisualState에서 입력된 StyleSelector가 사용되지 않음)
             * 두번째 항목부터는 정상적으로 VisualState에서 입력된 StyleSelector가 동작함
             * 그래서 기본값을 화면 사이즈에 따라 그때 그때 바인딩 값을 바꾸는 것으로 해결.
             * 그런데 이때 AdaptiveTriiger와 여기서 중복되어 설정하므로 UI갱신이 2번 일어날 수도 있음. (아직 테스트 못해봄)
             */
            ChangeStyleSelector("StorageItemStyleSelector", e.Size.Width);
        }

        protected override void InitializeViewModel()
        {
            var resource = ResourceLoader.GetForCurrentView();
            StorageItemInfo fi = _FolderDAO.GetLastFolder();

            CreateOrderBySource();

            LoadFoldersAsync(fi);
            //재생목록 로드
            PlayListSource.Add(new PlayList
            {
                Seq = 1,
                Name = resource.GetString("PlayList/NowPlaying/Text"),
                ItemTapped = PlayListTapped
            });
            _PlayListDAO.LoadPlayList(PlayListSource, PlayListTapped);

            AddedAfterMovePlayList = Settings.General.AddedAfterMovePlayList;

            UNCFolderFlyoutData.PrimaryButtonCommand = new RelayCommand<TextBox>(UNCFolderConnectButtonCommandExecute);
        }

        #region 폴더 로딩
        private async void SetExtraPropertiesAsync(StorageItemInfo itemInfo)
        {
            if (itemInfo.IsFile)
            {
                //파일 용량 조회
                var file = await itemInfo.GetStorageFileAsync();
                if (file != null)
                {
                    //System.Diagnostics.Debug.WriteLine(file.DisplayName);
                    var bi = await file.GetBasicPropertiesAsync();
                    //파일 크기 설정
                    itemInfo.Size = bi.Size;
                    //썸네일 로드
                    this.LoadThumbnailAsync(itemInfo, _ThumbnailListInCurrentFolder, Settings.Thumbnail.UseUnsupportedLocalFile);
                    //자막 목록 설정
                    if (_CurrentSubtitleFileList != null && _CurrentSubtitleFileList.Any())
                    {
                        itemInfo.SubtitleList = _CurrentSubtitleFileList.Where(x => x.ToUpper().Contains(PathHelper.GetFullPathWithoutExtension(itemInfo.Path).ToUpper())).ToList();
                    }
                }
            }
            else
            {
                var folder = await itemInfo.GetStorageFolderAsync();
                if (folder != null)
                {
                    //2016-12-04 폴더 로딩 오류 자동 복구 모두 추가
                    //FAL Token이 변경이 되었다면, 폴더 로딩 오류에서 복구된 것이므로 해당 StorageItemInfo를 업데이트 할 필요가 있음 
                    if (itemInfo.NeedToUpdateToken)
                    {
                        _FolderDAO.Update(itemInfo);
                        itemInfo.NeedToUpdateToken = false;
                    }

                    uint fileCount = 0;
                    uint folderCount = 0; 
                    itemInfo.DateCreated = folder.DateCreated;
                    //비디오 파일 갯수를 알아내기 위한 필터링 옵션
                    var queryResult = folder.CreateFileQueryWithOptions(_VideoFileQueryOptions);
                    try
                    {
                        fileCount = await queryResult.GetItemCountAsync();
                    }
                    catch(Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ExplorerViewModel Line 352 : {ex.Message}");
                    }
                    itemInfo.FileCount = (int)fileCount;

                    try
                    {
                        folderCount = await folder.CreateFolderQuery().GetItemCountAsync(); ;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ExplorerViewModel Line 363 : {ex.Message}");
                    }

                    if (itemInfo.FileCount > 0)
                        itemInfo.FileCountDescription = itemInfo.FileCount.ToString() + (folderCount > 0 ? "+" : string.Empty);
                    else
                        itemInfo.FileCountDescription = "0" + (folderCount > 0 ? "*" : string.Empty);

                    if (itemInfo.FileCount > 0)
                    {
                        var fileList = await queryResult.GetFilesAsync();
                        List<ImageSource> imageSourceList = new List<ImageSource>();

                        List<Thumbnail> thumbnailList = new List<Thumbnail>();
                        ThumbnailDAO.LoadThumnailInFolder(itemInfo.Path, thumbnailList);

                        for (int i = 0; i < fileList.Count; i++)
                        {
                            //썸네일 로드
                            var imgSrc = await GetThumbnailAsync(fileList[i], thumbnailList, Settings.Thumbnail.UseUnsupportedLocalFolder);
                            if (imgSrc != null)
                            {
                                imageSourceList.Add(imgSrc);
                            }
                            //4장의 이미지를 채우지 못할 것으로 확신되거나, 이미 4장을 채운경우 정지
                            if (((imageSourceList.Count > 0 && imageSourceList.Count >= 4)
                                || (itemInfo.FileCount - (i + 1) + imageSourceList.Count < 4)) && imageSourceList.Count > 0) break;
                        }

                        itemInfo.ImageItemsSource = imageSourceList;
                    }
                }
            }
        }

        private void AddStorageFolder(List<StorageItemInfo> targetFolderList, StorageFolder folder)
        {
            var folderItem = new StorageItemInfo(folder, SubType.RootFolder)
            {
                Tapped = FolderTapped,
                RightTapped = FolderRightTapped,
                Holding = FolderHolding,
                ContextMenuTapped = ContextMenuTapped,
                IsOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending
            };
            folderItem.SetDisplayName();

            targetFolderList.Add(folderItem);
        }

        private async void LoadFoldersAsync(StorageItemInfo fi)
        {
            var groupName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");

            CurrentFolderInfo = fi;
            _CurrentSubtitleFileList = null;
            IsLoadingFolders = true;

            IsActionButtonSet = false;
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            //탭된 폴더 저장
            _FolderDAO.ReplaceLastFolder(fi);

            //모든 자식 요소 삭제
            StorageItemGroupSource.Clear();

            _ThumbnailListInCurrentFolder.Clear();
            //기간 지난 썸네일 캐시 삭제 
            ThumbnailDAO.DeletePastPeriodThumbnail(Settings.Thumbnail.RetentionPeriod);

            if (fi == null)
            {
                await ThreadPool.RunAsync(async handler =>
                {
                    //추가된 폴더 로딩
                    var addedFolderList = new List<StorageItemInfo>();

                    if (App.IsXbox)
                    {
                        //xbox의 경우 2016-10-14일 기준 (윈도우 10.0.14393.0) 으로 XBox에서 FolderPicker가 구현되지 않았음
                        AddStorageFolder(addedFolderList, KnownFolders.CameraRoll);
                        AddStorageFolder(addedFolderList, KnownFolders.MusicLibrary);
                        AddStorageFolder(addedFolderList, KnownFolders.PicturesLibrary);
                        AddStorageFolder(addedFolderList, KnownFolders.RemovableDevices);
                        AddStorageFolder(addedFolderList, KnownFolders.SavedPictures);
                        AddStorageFolder(addedFolderList, KnownFolders.VideosLibrary);
                    }
                    else
                    {
                        //기본적으로 이름에 대한 정렬(오름/내림차순)을 해서 가져옴
                        _FolderDAO.LoadAddedFolderList(addedFolderList, _Sort == SortType.Name, (si) =>
                        {
                            si.Tapped = FolderTapped;
                            si.RightTapped = FolderRightTapped;
                            si.Holding = FolderHolding;
                            si.ContextMenuTapped = ContextMenuTapped;
                            si.IsOrderByName = isOrderByName;
                            si.SetDisplayName();
                        });
                    }

                    await DispatcherHelper.RunAsync(() =>
                    {
                        //노 아이템 텍스트 표시
                        HasNoFolder = addedFolderList.Count == 0;
                    });

                    if (addedFolderList.Count > 0)
                    {
                        if (_Sort == SortType.CreatedDate || _Sort == SortType.CreatedDateDescending)
                        {
                            foreach (var fis in addedFolderList)
                            {
                                var folder = await fis.GetStorageFolderAsync();
                                if (folder != null)
                                {
                                    fis.DateCreated = folder.DateCreated;
                                }
                            }
                        }

                        await LoadItemsAsync(addedFolderList, addedFolderList.Count, groupName, false, 2);
                    }

                    await DispatcherHelper.RunAsync(() =>
                    {
                        //폴더 로딩 표시기 정지
                        IsLoadingFolders = false;
                        //정렬 옵션 표시
                        ShowOrderBy = addedFolderList.Count > 0;
                    });
                });
                
                //뒤로버튼 상태 변경
                Frame rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null && !rootFrame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                }
                //추가버튼 활성화
                IsAddFolder = true;
            }
            else
            {
                //추가버튼 비활성화
                IsAddFolder = false;
                await ThreadPool.RunAsync(async handler => 
                {
                    if (Settings.General.UseHardwareBackButtonWithinVideo)
                    {
                        await DispatcherHelper.RunAsync(() => SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible);
                    }

                    var lastStorageFolder = await fi.GetStorageFolderAsync();
                    if (lastStorageFolder != null)
                    {
                        IReadOnlyList<StorageFolder> folderList = null;
                        try
                        {
                            //부모에 접근 권한이 있는 경우이므로 해당 부모의 자식 폴더 및 파일 로드
                            folderList = await lastStorageFolder.GetFoldersAsync();
                            if (folderList.Count > 0)
                            {
                                await LoadItemsAsync(folderList.Select(x => new StorageItemInfo(x)
                                {
                                    Tapped = FolderTapped,
                                    RightTapped = FolderRightTapped,
                                    Holding = FolderHolding,
                                    ContextMenuTapped = ContextMenuTapped,
                                    RootPath = fi.RootPath,
                                    IsOrderByName = isOrderByName
                                }), folderList.Count, groupName, false, 2);
                            }
                            
                            DispatcherHelper.CheckBeginInvokeOnUI(() =>
                            {
                                //파일 리스트업
                                LoadFilesAsync(fi);
                            });
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine("폴더 로드 실패 : " + e.Message);
                            if  (e is OperationCanceledException)
                            {
                                //폴더 로딩이 취소됨
                                System.Diagnostics.Debug.WriteLine(fi.Path + " 폴더내의 자식 폴더 로딩이 취소됨");
                            }
                        }
                    }
                    else
                    {
                        DispatcherHelper.CheckBeginInvokeOnUI(() =>
                        {
                            LoadFoldersAsync(null);
                        });
                    }
                });
            }
        }
        #endregion
                        
        #region 파일 로딩
        private async void LoadFilesAsync(StorageItemInfo fi)
        {
            if (fi != null)
            {
                string nonGroupName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
                ///파일 로딩 표시기 실행
                IsLoadingFiles = true;
                //폴더 로딩 표시기 정지
                IsLoadingFolders = false;

                await ThreadPool.RunAsync(async (handler) =>
                {
                    StorageFolder currentFolder = await fi.GetStorageFolderAsync();
                    if (currentFolder != null)
                    {
                        var queryResult = currentFolder.CreateFileQueryWithOptions(_VideoFileQueryOptions);
                        var list = await queryResult.GetFilesAsync();
                        if (list.Any())
                        {
                            //썸네일 캐시 로드
                            ThumbnailDAO.LoadThumnailInFolder(fi.Path, _ThumbnailListInCurrentFolder);

                            //아이템을 정렬하여 화면에 표시
                            await LoadItemsAsync(list.Select(x => new StorageItemInfo(x)
                            {
                                Tapped = FileTapped,
                                RightTapped = FileRightTapped,
                                Holding = FileHolding,
                                ContextMenuTapped = ContextMenuTapped,
                                IsOrderByName = (_Sort == SortType.Name || _Sort == SortType.NameDescending)
                            }), list.Count, nonGroupName, true, 9);
                        }
                    }
                    await DispatcherHelper.RunAsync(() => { IsLoadingFiles = false; });
                });
            }
            else
            {
                IsLoadingFolders = false;
            }
        }
        #endregion

        private async Task LoadItemsAsync(IEnumerable<StorageItemInfo> storageItemList, int itemCount, string defaultGroupName, bool isFile, int patchSize)
        {
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
            var isMultiGroup = isFile && isOrderByName && itemCount > GROUP_MAX_ITME_COUNT;
            List<StorageItemInfo> addedList = new List<StorageItemInfo>();
            int offset = 0;

            switch (_Sort)
            {
                case SortType.CreatedDateDescending:
                    storageItemList = storageItemList.OrderByDescending(x => x.DateCreated);
                    break;
                case SortType.CreatedDate:
                    storageItemList = storageItemList.OrderBy(x => x.DateCreated);
                    break;
                case SortType.NameDescending:
                    storageItemList = storageItemList.OrderByDescending(x => x.DisplayName);
                    break;
                default:
                    storageItemList = storageItemList.OrderBy(x => x.DisplayName);
                    break;
            }

            if (isFile && itemCount > 0 && _CurrentSubtitleFileList == null && CurrentFolderInfo != null)
            {
                //자막 검색
                var currentFolder = await CurrentFolderInfo.GetStorageFolderAsync();
                var queryResult = currentFolder.CreateFileQueryWithOptions(_SubtitleFileQueryOptions);
                var list = await queryResult.GetFilesAsync();
                _CurrentSubtitleFileList = list.Select(x => x.Path).ToList();
                System.Diagnostics.Debug.WriteLine($"Explorer : {currentFolder.Path} 폴더내 자막파일 {_CurrentSubtitleFileList.Count}개 검색 됨...");
            }

            StorageItemGroup group = null;
            string groupName = isMultiGroup ? null : defaultGroupName;

            foreach (var storageItem in storageItemList)
            {
                if (isMultiGroup)
                {
                    groupName = _CharacterGroupings.Lookup(storageItem.DisplayName);
                }
                //그룹이 변경되는 경우
                if (group == null || group.Name != groupName)
                {
                    group = new StorageItemGroup(isFile ? StorageItemTypes.File : StorageItemTypes.Folder, groupName);
                    //신규 그룹 생성
                    await DispatcherHelper.RunAsync(() => StorageItemGroupSource.Add(group));
                }

                //일괄 작업을 위해 리스트에 추가
                addedList.Add(storageItem);
                storageItem.Group = group;

                //리스트가 일정수량에 도달하거나, 마지막까지 도달한 경우
                if (++offset % patchSize == 0 || offset >= itemCount)
                {
                    bool isCanceled = false;
                    await DispatcherHelper.RunAsync(() =>
                    {
                        foreach (var added in addedList)
                        {
                            added.Group.Items.Add(added);
                            added.Group = null;
                        }
                        addedList.Clear();
                    });

                    if (isCanceled)
                    {
                        break;
                    }
                }
            }
        }

        private void FolderTapped(object sender, TappedRoutedEventArgs args)
        {
            if (SelectionMode != ListViewSelectionMode.None) return;

            StorageItemInfo sii = null;
            var elem = args.OriginalSource as FrameworkElement;

            if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
            {
                LoadFoldersAsync(sii);
            }
        }

        private void FolderRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            if (args.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement senderElement = sender as FrameworkElement;
                FlyoutBase flyoutBase = Flyout.GetAttachedFlyout(senderElement);
                flyoutBase.ShowAt(senderElement);
            }
        }

        private void FolderHolding(object sender, HoldingRoutedEventArgs args)
        {
            if (args.HoldingState == Windows.UI.Input.HoldingState.Started
                && args.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FrameworkElement senderElement = sender as FrameworkElement;
                FlyoutBase flyoutBase = Flyout.GetAttachedFlyout(senderElement);
                flyoutBase.ShowAt(senderElement);
            }
        }

        private async void ContextMenuTapped(object sender, TappedRoutedEventArgs args)
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            var mfi = sender as MenuFlyoutItem;
            var itemInfo = mfi.DataContext as StorageItemInfo;
            var id = mfi.Tag.ToString();

            switch (id)
            {
                case "FolderToPlayList":
                    if (VersionHelper.CheckPaidFeature())
                    {
                        PlayList playList = PlayListSource.FirstOrDefault(x => x.Name == itemInfo.DisplayName);
                        if (playList == null)
                        {
                            var pl = new PlayList() { Name = itemInfo.DisplayName };
                            var result = _PlayListDAO.InsertPlayList(pl);
                            if (result == SQLitePCL.SQLiteResult.DONE)
                            {
                                MessengerInstance.Send(new KeyValuePair<string, PlayList>("added", pl), "PlayListChanged");
                                playList = pl;
                            }
                            else
                            {
                                var dlg = DialogHelper.GetSimpleContentDialog(
                                    resource.GetString("Message/Error/Save"),
                                    resource.GetString("Message/Error/Retry"),
                                    resource.GetString("Button/Close/Content"));
                                await dlg.ShowAsync();
                                App.ContentDlgOp = null;
                            }
                        }
                        //플레이 리스트가 존재하거나, 정상적으로 생성되었다면 파일을 검색하여 추가
                        if (playList != null)
                        {
                            StorageFolder currentFolder = await itemInfo.GetStorageFolderAsync();
                            if (currentFolder != null)
                            {
                                var queryResult = currentFolder.CreateFileQueryWithOptions(_VideoFileQueryOptions);
                                var list = await queryResult.GetFilesAsync();
                                if (list.Any())
                                {
                                    var infolist = list.Select(x => new StorageItemInfo(x));
                                    IOrderedEnumerable<StorageItemInfo> orderedList = null;
                                    switch (_Sort)
                                    {
                                        case SortType.Name:
                                            orderedList = infolist.OrderBy(x => x.DisplayName);
                                            break;
                                        case SortType.NameDescending:
                                            orderedList = infolist.OrderByDescending(x => x.DisplayName);
                                            break;
                                        case SortType.CreatedDate:
                                            orderedList = infolist.OrderBy(x => x.DateCreated);
                                            break;
                                        case SortType.CreatedDateDescending:
                                            orderedList = infolist.OrderByDescending(x => x.DateCreated);
                                            break;
                                    }

                                    var dbResult = _PlayListDAO.InsertPlayListFiles(playList, orderedList);
                                    if (dbResult != SQLitePCL.SQLiteResult.DONE)
                                    {
                                        var dlg = DialogHelper.GetSimpleContentDialog(
                                            resource.GetString("Message/Error/AddPlayList"),
                                            resource.GetString("Message/Error/Retry"),
                                            resource.GetString("Button/Close/Content"));
                                        await dlg.ShowAsync();
                                        App.ContentDlgOp = null;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private void ShowMediaInfoFlyout(StorageItemInfo sii)
        {
            MessengerInstance.Send<Message<DecoderTypes>>(
                new Message<DecoderTypes>((decoderType) =>
                {
                    RequestPlayback(decoderType, sii);
                })
                .Add("StorageItemInfo", sii)
                .Add("ButtonName", "CodecInformation"),
                "ShowMediaFileInformation");
        }

        private async void RequestPlayback(DecoderTypes decoderType, StorageItemInfo storageItemInfo)
        {
            //즉시 재생 모드
            var nowPlaying = PlayListSource.Where(x => x.Seq == 1).FirstOrDefault();

            //현재 파일 이후의 파일들을 모두 지금 재생중에 추가
            var files = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).ToList();
            var index = files.IndexOf(storageItemInfo);
            var rangeList = files.GetRange(index, files.Count - index);

            //지금 재생중 초기화
            _PlayListDAO.DeletePlayListFiles(nowPlaying);
            //선택한 파일 이후의 모든 파일 추가
            var dbResult = _PlayListDAO.InsertPlayListFiles(nowPlaying, rangeList);
            if (dbResult == SQLitePCL.SQLiteResult.DONE)
            {
                //지금 재생중으로 이동 (메인 => 메뉴 선택 => 지금 재생중 => 재생시작)
                MessengerInstance.Send(new Message("PlayList", nowPlaying)
                    .Add("StorageItemInfo", storageItemInfo)
                    .Add("DecoderType", decoderType),
                    "MoveToPlayListMenu");
            }
            else
            {
                //열려 있는 로딩창이 있으면 닫기
                MessengerInstance.Send(new Message("IsOpen", false), "ShowLoadingPanel");
                //재생목록에 추가 플라이아웃 닫기
                DialogHelper.CloseFlyout("AddToPlayListFlyoutContent");
                var resource = ResourceLoader.GetForCurrentView();
                var dlg = DialogHelper.GetSimpleContentDialog(
                    resource.GetString("Message/Error/AddPlayList"),
                    resource.GetString("Message/Error/Retry"),
                    resource.GetString("Button/Close/Content"));
                await dlg.ShowAsync();
                App.ContentDlgOp = null;
            }
        }

        public void FileTapped(object sender, TappedRoutedEventArgs args)
        {
            if (SelectionMode != ListViewSelectionMode.None) return;

            StorageItemInfo sii = null;
            var elem = args.OriginalSource as FrameworkElement;
            if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
            {
                RequestPlayback(DecoderTypes.AUTO, sii);
            }
        }

        private void FileRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            if (args.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                StorageItemInfo sii = null;
                var elem = args.OriginalSource as FrameworkElement;
                if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
                {
                    ShowMediaInfoFlyout(sii);
                }
            }
        }

        private void FileHolding(object sender, HoldingRoutedEventArgs args)
        {
            if (args.HoldingState == Windows.UI.Input.HoldingState.Started
                && args.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                StorageItemInfo sii = null;
                var elem = args.OriginalSource as FrameworkElement;
                if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
                {
                    ShowMediaInfoFlyout(sii);
                }
            }
        }

        public async void AddLocalFolderTapped(object sender, TappedRoutedEventArgs e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            if (App.IsXbox)
            {
                var dlg = DialogHelper.GetSimpleContentDialog(
                    resource.GetString("Message/Error/XboxIssue"),
                    resource.GetString("Message/Error/NotImplements"),
                    resource.GetString("Button/Close/Content"));
                await dlg.ShowAsync();
                App.ContentDlgOp = null;
                return;
            }

            FolderPicker folderPicker = new FolderPicker();
            folderPicker.ViewMode = PickerViewMode.Thumbnail;
                
            //모바일이 아니거나 SD카드가 없는 경우 Video 표시, 왜냐하면 모바일에서 SD 카드가 안보여 오해의 소지가 있음
            if (!App.IsMobile)
            {
                folderPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            }

            folderPicker.CommitButtonText = resource.GetString("Folder/AddButton/Text");

            foreach (var suffix in CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX)
            {
                folderPicker.FileTypeFilter.Add(suffix);
            }

            App.StoragePickerStatus = StoragePickerStatus.Opened;
            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                AddFolder(folder);
            }
            else
            {
                App.StoragePickerStatus = StoragePickerStatus.Canceled;
            }
        }

        private async void AddFolder(StorageFolder folder)
        {
            //중복된 폴더는 추가하지 않음
            var folderGroup = StorageItemGroupSource.FirstOrDefault(x => x.Type == StorageItemTypes.Folder);

            if (folderGroup == null)
            {
                folderGroup = new StorageItemGroup(StorageItemTypes.Folder);
                StorageItemGroupSource.Insert(0, folderGroup);
            }

            if (!folderGroup.Items.Any(x => x.Path == folder.Path))
            {
                StorageItemInfo folderInfo = new StorageItemInfo(folder, SubType.RootFolder)
                {
                    RootPath = folder.Path,
                    Tapped = FolderTapped,
                    RightTapped = FolderRightTapped,
                    ContextMenuTapped = ContextMenuTapped,
                    Holding = FolderHolding,
                    IsOrderByName = (_Sort == SortType.Name || _Sort == SortType.NameDescending)
                };

                //폴더의 썸네일 및 하위 비디오갯수 설정
                Task.Factory.StartNew(() =>
                {
                    SetExtraPropertiesAsync(folderInfo);
                }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
                {
                    System.Diagnostics.Debug.WriteLine($"추가폴더의 부가정보 로드 : {folderInfo.Path}");
                });

                StorageItemInfo find = null;
                switch (_Sort)
                {
                    case SortType.Name:
                        find = folderGroup.Items.LastOrDefault(x => string.Compare(x.DisplayName, folderInfo.DisplayName) < 0);
                        break;
                    case SortType.NameDescending:
                        find = folderGroup.Items.LastOrDefault(x => string.Compare(folderInfo.DisplayName, x.DisplayName) < 0);
                        break;
                    case SortType.CreatedDate:
                        find = folderGroup.Items.LastOrDefault(x => x.DateCreated < folderInfo.DateCreated);
                        break;
                    case SortType.CreatedDateDescending:
                        find = folderGroup.Items.LastOrDefault(x => folderInfo.DateCreated < x.DateCreated);
                        break;
                }

                //정렬 순서 결정
                int index = folderGroup.Items.IndexOf(find);

                //폴더 추가
                await DispatcherHelper.RunAsync(() =>
                {
                    folderGroup.Items.Insert(index + 1, folderInfo);
                    HasNoFolder = false;
                    ShowOrderBy = true;
                });

                //전체 비디오에 반영
                await ThreadPool.RunAsync(handler =>
                {
                    //선택한 폴더 DB등록
                    _FolderDAO.Insert(folderInfo);
                });
            }
        }

        public void AddUNCFolderTapped(object sender, TappedRoutedEventArgs e)
        {
            var button = ElementHelper.FindVisualChild<Button>(Window.Current.Content, "AddFolderButton");
            DialogHelper.ShowFlyout(App.Current.Resources, "ConnectUNCFolderFlyout", button, (key) =>
            {
                if (key.Key == Windows.System.VirtualKey.Enter)
                {
                    UNCFolderConnectButtonCommandExecute(key.OriginalSource as TextBox);
                }
            });
        }
        
        private async void UNCFolderConnectButtonCommandExecute(TextBox value)
        {
            string path = value.Text.Trim();
            UNCFolderFlyoutData.IsProcessingPrimaryButton = true;

            await ThreadPool.RunAsync(async handler =>
            {
                StorageFolder folder = null;
                try
                {
                    folder = await StorageFolder.GetFolderFromPathAsync(path); ;
                }
                catch (Exception ex)
                {
                    await DispatcherHelper.RunAsync(() =>
                    {
                        UNCFolderFlyoutData.IsProcessingPrimaryButton = false;
                        UNCFolderFlyoutData.ShowErrorMessage = true;
                        UNCFolderFlyoutData.ErrorMessage = ex.Message;
                        value.SelectAll();
                        value.Focus(FocusState.Keyboard);
                    });
                }

                if (folder != null)
                {
                    await DispatcherHelper.RunAsync(() =>
                    {
                        UNCFolderFlyoutData.IsProcessingPrimaryButton = false;
                        UNCFolderFlyoutData.PrimaryContent = string.Empty;
                        UNCFolderFlyoutData.ErrorMessage = string.Empty;
                        UNCFolderFlyoutData.ShowErrorMessage = false;
                        DialogHelper.CloseFlyout("ConnectUNCFolderFlyoutContent");
                        //폴더추가
                        AddFolder(folder);
                    });
                }

            });
        }

        public async void ToUpperTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator)
                return;

            bool isLoaded = false;

            if (CurrentFolderInfo?.SubType != SubType.RootFolder && 
                CurrentFolderInfo?.Path != CurrentFolderInfo.RootPath)
            {
                var last = await CurrentFolderInfo.GetStorageFolderAsync();
                if (last != null)
                {
                    var parent = await last.GetParentAsync();

                    if (parent == null)
                    {
                        if (last.Path.StartsWith(@"\\"))
                        {
                            try
                            {
                                string[] paths = last.Path.Split(new char[] { Path.DirectorySeparatorChar });
                                if (paths.Length > 1)
                                {
                                    var parentPath = paths.ToList().GetRange(0, paths.Length - 1).Aggregate((x, y) => x + Path.DirectorySeparatorChar +  y);
                                    parent = await StorageFolder.GetFolderFromPathAsync(parentPath);
                                }
                            }
                            catch (Exception) {}
                        }
                    }

                    if (parent != null)
                    {
                        LoadFoldersAsync(new StorageItemInfo(parent)
                        {
                            RootPath = CurrentFolderInfo.RootPath
                        });
                        isLoaded = true;
                    }
                }
            }

            if (!isLoaded)
            {
                LoadFoldersAsync(null);
            }
        }

        public async void RefreshTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator)
                return;

            if (CurrentFolderInfo == null)
            {
                LoadFoldersAsync(null);
            }
            else
            {
                try
                {
                    var last = await CurrentFolderInfo.GetStorageFolderAsync();
                    if (last != null)
                    {
                        LoadFoldersAsync(new StorageItemInfo(last)
                        {
                            RootPath = CurrentFolderInfo.RootPath
                        });
                    }
                    else
                    {
                        LoadFoldersAsync(null);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        public void SelectFolderTapped(object sender, TappedRoutedEventArgs e)
        {
            SelectionMode = ListViewSelectionMode.Multiple;
            IsActionButtonSet = true;
        }

        public void BackNormalButtonSetTapped(object sender, TappedRoutedEventArgs args)
        {
            SelectionMode = ListViewSelectionMode.None;
            IsActionButtonSet = false;
        }

        public void AllCheckFolderTapped(object sender, TappedRoutedEventArgs args)
        {
            if (_SelectedItems.Count == 0)
            {
                _SelectedItems.Clear();
                IEnumerable<StorageItemInfo> storageItemInfos = null;
                if (IsAddFolder)
                {
                    //폴더 추가 모드
                    storageItemInfos = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items);
                }
                else
                {
                    storageItemInfos = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items);
                }

                foreach (var fi in storageItemInfos)
                {
                    _SelectedItems.Add(fi);
                }
            }
            else
            {
                _SelectedItems.Clear();
            }
        }

        public void AddToPlayListTapped(object sender, TappedRoutedEventArgs args)
        {
            Button button = sender as Button;
            DialogHelper.ShowFlyout(button.Resources, "AddToPlayListFlyout", button);
        }

        public void RemoveFolderTapped(object sender, TappedRoutedEventArgs args)
        {
            var fg = StorageItemGroupSource.FirstOrDefault(x => x.Type == StorageItemTypes.Folder);
            if (fg != null)
            {
                for (int i = _SelectedItems.Count; i > 0; i--)
                {
                    var fi = _SelectedItems[i - 1] as StorageItemInfo;
                    fg.Items.Remove(fi);
                    _FolderDAO.Delete(fi);
                }

                if (fg.Items.Count == 0)
                {
                    //폴더그룹 삭제
                    StorageItemGroupSource.Remove(fg);
                    //추가메뉴로 변경
                    BackNormalButtonSetTapped(null, null);
                    //Noitem 표시
                    HasNoFolder = true;
                    //정렬 콤보 제거
                    ShowOrderBy = false;
                }
            }
        }

        public void NewPlayListTapped(object sender, TappedRoutedEventArgs args)
        {
            MessengerInstance.Send(new Message<PlayList>(AddToPlayList), "NewPlayListTapped");
        }

        public void PlayListTapped(object sender, TappedRoutedEventArgs args)
        {
            var playList = (sender as FrameworkElement).DataContext as PlayList;
            AddToPlayList(playList);
            DialogHelper.CloseFlyout("AddToPlayListFlyoutContent");
        }

        private async void AddToPlayList(PlayList playList)
        {
            if (_SelectedItems.Count > 0)
            {
                var dbResult = _PlayListDAO.InsertPlayListFiles(playList, _SelectedItems.Cast<StorageItemInfo>());
                if (dbResult == SQLitePCL.SQLiteResult.DONE)
                {
                    var firstItem = _SelectedItems[0];
                    //선택 초기화
                    _SelectedItems.Clear();

                    if (AddedAfterMovePlayList == true)
                    {
                        SelectionMode = ListViewSelectionMode.None;
                        IsActionButtonSet = false;

                        MessengerInstance.Send(new Message("PlayList", playList).Add("StorageItemInfo", firstItem), "MoveToPlayListMenu");
                    }
                }
                else
                {
                    DialogHelper.CloseFlyout("AddToPlayListFlyoutContent");
                    var resource = ResourceLoader.GetForCurrentView();
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/AddPlayList"),
                        resource.GetString("Message/Error/Retry"),
                        resource.GetString("Button/Close/Content"));
                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
    }
            }
        }

        public void ExplorerGridViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var gridView = sender as GridView;
            _SelectedItems = gridView.SelectedItems;
            IsSelected = gridView.SelectedItems.Count > 0;
        }

        public void GridViewLoaded(object sender, RoutedEventArgs e)
        {
            var gridView = sender as GridView;
            if (gridView != null)
            {
                var page = ElementHelper.FindVisualParent<Views.ExplorerPage>(gridView);
                if (page != null)
                {
                    Resources = page.Resources;
                    //초기값 로드
                    ChangeStyleSelector("StorageItemStyleSelector", Windows.UI.Xaml.Window.Current.Bounds.Width);
                }

                if (_SelectedItems == null)
                {
                    _SelectedItems = gridView.SelectedItems;
                }
            }

            //로딩된 리스트가 존재하는 경우, 설정에서 back버튼이 활성화 되어 있으면 상위 폴더가 존재하는 경우 버튼을 활성화 시킴
            if (Settings.General.UseHardwareBackButtonWithinVideo
                && (StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items.Cast<StorageItemInfo>()).Any(x => x.Path != x.RootPath)
                 || StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).Any(x => x.Path != x.ParentFolderPath)))
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
        }

        protected override void OrderByChanged()
        {
            string nonGroupFileName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
            string nonGroupFolderName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");

            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
            List<StorageItemInfo> folderItems = null;
            List<StorageItemInfo> fileItems = null;

            switch (_Sort)
            {
                case SortType.Name:
                    folderItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderBy(x => x.DisplayName).ToList();
                    fileItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderBy(x => x.DisplayName).ToList();
                    break;
                case SortType.NameDescending:
                    folderItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderByDescending(x => x.DisplayName).ToList();
                    fileItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderByDescending(x => x.DisplayName).ToList();
                    break;
                case SortType.CreatedDate:
                    folderItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderBy(x => x.DateCreated).ToList();
                    fileItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderBy(x => x.DateCreated).ToList();
                    break;
                case SortType.CreatedDateDescending:
                    folderItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderByDescending(x => x.DateCreated).ToList();
                    fileItems = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderByDescending(x => x.DateCreated).ToList();
                    break;
            }

            //리스트 전체 초기화
            StorageItemGroupSource.Clear();
            //폴더 재추가
            if (folderItems != null && folderItems.Count > 0)
            {
                StorageItemGroup folderGroup = new StorageItemGroup(StorageItemTypes.Folder, nonGroupFolderName);
                StorageItemGroupSource.Add(folderGroup);

                foreach (var item in folderItems)
                {
                    item.IsOrderByName = isOrderByName;
                    folderGroup.Items.Add(item);
                }
            }
            //파일 재추가
            if (fileItems != null && fileItems.Count > 0)
            {
                StorageItemGroup fileGroup = null;
                var fileStartIndex = StorageItemGroupSource.Any(x => x.Type == StorageItemTypes.Folder) ? 1 : 0;

                if (isOrderByName && fileItems.Count > GROUP_MAX_ITME_COUNT)
                {
                    foreach (var item in fileItems)
                    {
                        var groupName = _CharacterGroupings.Lookup(item.DisplayName);
                        fileGroup = StorageItemGroupSource.FirstOrDefault(x => x.Name == groupName);

                        if (fileGroup == null)
                        {
                            fileGroup = new StorageItemGroup(StorageItemTypes.File, groupName);
                            StorageItemGroupSource.Add(fileGroup);
                        }

                        item.IsOrderByName = isOrderByName;
                        fileGroup.Items.Add(item);
                    }
                }
                else
                {
                    fileGroup = new StorageItemGroup(StorageItemTypes.File, nonGroupFileName);
                    StorageItemGroupSource.Add(fileGroup);

                    foreach (var item in fileItems)
                    {
                        item.IsOrderByName = isOrderByName;
                        fileGroup.Items.Add(item);
                    }
                }
            }
        }
    }
}
