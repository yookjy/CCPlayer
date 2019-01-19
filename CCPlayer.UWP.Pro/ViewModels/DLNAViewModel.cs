using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using GalaSoft.MvvmLight.Threading;
using Lime.Xaml.Helpers;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.ViewModels
{
    public class DLNAViewModel : FileViewModelBase
    {
        private const string LastestMediaServerFolder = "vmspath://";

        private Stack<StorageFolder> _FolderStack;

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

        [DoNotNotify]
        public ObservableCollection<StorageItemGroup> StorageItemGroupSource { get; set; }

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
                        try
                        {
                            DisplayCurrentPath = _FolderStack.Select(x => x.DisplayName).Aggregate((x, y) => y + "/" + x);
                        }
                        catch (Exception)
                        {
                            DisplayCurrentPath = value.Path;
                        }
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
        
        protected override void FakeIocInstanceInitialize()
        {
        }

        protected override void CreateModel()
        {
            _ThumbnailListInCurrentFolder = new List<Thumbnail>();
            _FolderStack = new Stack<StorageFolder>();
            StorageItemGroupSource = new ObservableCollection<StorageItemGroup>();

            var resource = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            OrderBySource = new ObservableCollection<KeyName>();
            OrderBySource.Add(new KeyName(SortType.Name.ToString(), resource.GetString("Sort/Name/Ascending")));
            OrderBySource.Add(new KeyName(SortType.NameDescending.ToString(), resource.GetString("Sort/Name/Descending")));
            
            OrderBy = SortType.Name.ToString();

            // Get the letters representing each group for current language using CharacterGroupings class
            CreateCharacterGroupings();

            //비디오파일 쿼리 옵션 저장
            _VideoFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX);
            //_VideoFileQueryOptions.SetThumbnailPrefetch(ThumbnailMode.SingleItem, (uint)ThumbnailSize.Width, ThumbnailOptions.ReturnOnlyIfCached);
            //자막파일 쿼리 옵션 저장
            _SubtitleFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<bool>(this, "DLNABackRequested", (val) =>
            {
                ToUpperTapped(null, null);
            });

            MessengerInstance.Register<Message>(this, "DLNANextPlayListFile", (val) =>
            {
                DecoderTypes decoderType = val.GetValue<DecoderTypes>("DecoderType");
                PlayListFile playListFile = val.GetValue<PlayListFile>("NextPlayListFile");

                RequestPlayback(decoderType, playListFile, true);
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
            Title = KnownFolders.MediaServerDevices.DisplayName;
            LoadLastestFolder();
        }
        #region 폴더 로딩

        private async void LoadLastestFolder()
        {
            StorageFolder folder = null;
            StorageItemInfo folderInfo = null;
            var entry = StorageApplicationPermissions.MostRecentlyUsedList.Entries.FirstOrDefault(x => x.Metadata.StartsWith(LastestMediaServerFolder));

            if (!string.IsNullOrEmpty(entry.Token))
            {
                var paths = entry.Metadata.Substring(LastestMediaServerFolder.Length);
                List<string> pathList = paths.Split(new char[] { '/' }).ToList();

                if (pathList.Count > 0)
                {
                    string path = pathList[0];
                    folder = (await KnownFolders.MediaServerDevices.GetFoldersAsync()).FirstOrDefault(x => x.Name == path);

                    if (folder != null)
                    {
                        //루트 폴더 등록
                        _FolderStack.Push(folder);
                        pathList.RemoveAt(0);

                        foreach (var p in pathList)
                        {
                            var folders = await folder.GetFoldersAsync();
                            folder = folders.FirstOrDefault(x => x.Name == p);

                            if (folder != null)
                                _FolderStack.Push(folder);
                            else
                                break;
                        }
                    }
                }
            }

            if (folder != null)
            {
                folderInfo = new StorageItemInfo(folder);
            }
            
            LoadFoldersAsync(folderInfo);
        }

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
                    this.LoadThumbnailAsync(itemInfo, _ThumbnailListInCurrentFolder, Settings.Thumbnail.UseUnsupportedDLNAFile);
                    //자막 목록 설정
                    if (_CurrentSubtitleFileList != null && _CurrentSubtitleFileList.Any())
                    {
                        itemInfo.SubtitleList = _CurrentSubtitleFileList.Where(x => x.Contains(System.IO.Path.GetFileNameWithoutExtension(itemInfo.Name).ToUpper())).ToList();
                    }
                }
            }
            else
            {
                var folder = await itemInfo.GetStorageFolderAsync();
                if (folder != null)
                {
                    itemInfo.DateCreated = folder.DateCreated;

                    //비디오 파일 갯수를 알아내기 위한 필터링 옵션
                    var queryResult = folder.CreateFileQueryWithOptions(_VideoFileQueryOptions);
                    itemInfo.FileCount = (int)await queryResult.GetItemCountAsync();

                    var folderCount = await folder.CreateFolderQuery().GetItemCountAsync();
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
                            var imgSrc = await GetThumbnailAsync(fileList[i], thumbnailList, Settings.Thumbnail.UseUnsupportedDLNAFolder);
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
                    else if (!_FolderStack.Any())
                    {
                        //NAS아이콘 추출
                        ImageSource imageSource = null;
                        var thumb = await folder.GetThumbnailAsync(ThumbnailMode.SingleItem);
                        if (thumb?.Type == ThumbnailType.Image)
                        {
                            //썸네일 설정
                            await DispatcherHelper.RunAsync(() =>
                            {
                                var bi = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                                bi.SetSource(thumb);
                                imageSource = bi;
                                itemInfo.IsFullFitImage = false;
                                itemInfo.ImageItemsSource = imageSource;
                            });
                        }
                    }
                }
            }
        }

        private async void LoadFoldersAsync(StorageItemInfo fi)
        {
            var groupName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");

            var mru = StorageApplicationPermissions.MostRecentlyUsedList;
            //키에 해당되는 모든 데이터 삭제
            var removeList = mru.Entries.Where(x => x.Metadata.StartsWith(LastestMediaServerFolder)).ToArray();
            foreach (var entry in removeList)
            {
                mru.Remove(entry.Token);
            }

            if (fi != null)
            {
                var folder = await fi.GetStorageFolderAsync();
                if (folder != null && !_FolderStack.Contains(folder))
                {
                    _FolderStack.Push(folder);

                }

                //탭된 폴더 저장
                var fullPath = _FolderStack.Select(x => x.DisplayName).Aggregate((x, y) => y + "/" + x);
                mru.Add(folder, LastestMediaServerFolder + fullPath);
            }

            CurrentFolderInfo = fi;
            _CurrentSubtitleFileList = null;
            IsLoadingFolders = true;

            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
            
            //모든 자식 요소 삭제
            StorageItemGroupSource.Clear();

            _ThumbnailListInCurrentFolder.Clear();
            //기간 지난 썸네일 캐시 삭제 
            ThumbnailDAO.DeletePastPeriodThumbnail(Settings.Thumbnail.RetentionPeriod);

            if (fi == null)
            {
                await ThreadPool.RunAsync(async handler =>
                {
                    var dlna = KnownFolders.MediaServerDevices;
                    var dlnaFolders = await dlna.GetFoldersAsync();
                    //DLNA 루트 폴더 로딩
                    var rootFolderList = dlnaFolders.Select(s => new StorageItemInfo(s)
                    {
                        DateCreated = s.DateCreated,
                        Tapped = FolderTapped,
                        RightTapped = FolderRightTapped,
                        Holding = FolderHolding,
                        IsOrderByName = isOrderByName,
                        SubType = SubType.RootFolder
                    }).TakeWhile(s => { s.SetDisplayName(); return true; }); ;

                    await LoadItemsAsync(rootFolderList, rootFolderList.Count(), groupName, false, 2);
                    
                    await DispatcherHelper.RunAsync(() =>
                    {
                        //폴더 로딩 표시기 정지
                        IsLoadingFolders = false;
                        //정렬 옵션 표시
                        ShowOrderBy = rootFolderList.Count() > 0;
                    });

                });              

                //뒤로버튼 상태 변경
                Frame rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null && !rootFrame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                }
                //위로 버튼 비활성화
                VisibleToUpper = false;
            }
            else
            {
                //위로버튼 활성화
                VisibleToUpper = true;
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

            if (isFile && itemCount > 0 && _CurrentSubtitleFileList == null)
            {
                //자막 검색
                var currentFolder = await CurrentFolderInfo.GetStorageFolderAsync();
                var queryResult = currentFolder.CreateFileQueryWithOptions(_SubtitleFileQueryOptions);
                var list = await queryResult.GetFilesAsync();
                _CurrentSubtitleFileList = list.Select(x => x.Path.ToUpper()).ToList();
                System.Diagnostics.Debug.WriteLine($"DLNA Server : {currentFolder.Path} 폴더내 자막파일 {_CurrentSubtitleFileList.Count}개 검색 됨...");
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

        private void FolderRightTapped(object sender, RightTappedRoutedEventArgs args) { }

        private void FolderHolding(object sender, HoldingRoutedEventArgs args) { }

        private void FolderTapped(object sender, TappedRoutedEventArgs args)
        {
            StorageItemInfo sii = null;
            var elem = args.OriginalSource as FrameworkElement;

            if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
            {
                LoadFoldersAsync(sii);
            }
        }

        private void ShowMediaInfoFlyout(StorageItemInfo sii)
        {
            MessengerInstance.Send(new Message("IsOpen", true)
                                    .Add("LoadingTitle", ResourceLoader.GetForCurrentView().GetString("Loading/MediaInformation/Text")),
                                    "ShowLoadingPanel");

            MessengerInstance.Send<Message<DecoderTypes>>(
                 new Message<DecoderTypes>((decoderType) =>
                 {
                     RequestPlayback(decoderType, sii, false);
                 })
                 .Add("StorageItemInfo", sii)
                 .Add("ButtonName", "CodecInformation"),
                 "ShowMediaFileInformation");
        }

        private async void RequestPlayback(DecoderTypes decoderType, StorageItemInfo storageItemInfo, bool isPrevOrNext)
        {
            if (!VersionHelper.IsUnlockNetworkPlay)
            {
                MessengerInstance.Send(new Message(), "CheckInterstitialAd");
                return;
            }

            if (!isPrevOrNext)
            {
                MessengerInstance.Send(new Message("IsOpen", true)
                    .Add("LoadingTitle", ResourceLoader.GetForCurrentView().GetString("Loading/Playback/Text")), "ShowLoadingPanel");
            }

            //현재 파일 이후의 파일들을 모두 지금 재생중에 추가
            var files = StorageItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).ToList();

            PlayListFile currPlayListFile = null;
            PlayListFile prevPlayListFile = null;
            PlayListFile nextPlayListFile = null;

            for(int i = 0; i < files.Count; i++)
            {
                var file = await files[i].GetStorageFileAsync();
                var orgFile = await storageItemInfo.GetStorageFileAsync();
                if (file.FolderRelativeId == orgFile.FolderRelativeId)
                {
                    currPlayListFile = new PlayListFile(file);

                    if (i > 0)
                    {
                        prevPlayListFile = new PlayListFile(await files[i - 1].GetStorageFileAsync());
                    }

                    if (i + 1 < files.Count)
                    {
                        nextPlayListFile = new PlayListFile(await files[i + 1].GetStorageFileAsync());
                    }
                    break;
                }
            }

            MessengerInstance.Send(
                new Message()
                .Add("PrevPlayListFile", prevPlayListFile)
                .Add("CurrPlayListFile", currPlayListFile)
                .Add("NextPlayListFile", nextPlayListFile)
                .Add("DecoderType", decoderType),
                "RequestPlayback");
        }

        public void FileTapped(object sender, TappedRoutedEventArgs args)
        {
            StorageItemInfo sii = null;
            var elem = args.OriginalSource as FrameworkElement;
            if (elem != null && (sii = elem.DataContext as StorageItemInfo) != null)
            {
                RequestPlayback(Settings.Playback.DefaultDecoderType, sii, false);
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

        public void ToUpperTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator)
                return;

            _FolderStack.Pop();
            
            if (_FolderStack.Count > 0)
            {
                var parent = _FolderStack.Pop();
                LoadFoldersAsync(new StorageItemInfo(parent));
            }
            else
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
                        LoadFoldersAsync(CurrentFolderInfo);
                    }
                    else
                    {
                        LoadFoldersAsync(null);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }
        
        public void PlayListTapped(object sender, TappedRoutedEventArgs args)
        {
            var playList = (sender as FrameworkElement).DataContext as PlayList;
            DialogHelper.CloseFlyout("AddToPlayListFlyoutContent");
        }
        
        public void GridViewLoaded(object sender, RoutedEventArgs e)
        {
            var gridView = sender as GridView;
            if (gridView != null)
            {
                var page = ElementHelper.FindVisualParent<Views.DLNAPage>(gridView);
                if (page != null)
                {
                    Resources = page.Resources;
                    //초기값 로드
                    ChangeStyleSelector("StorageItemStyleSelector", Windows.UI.Xaml.Window.Current.Bounds.Width);
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

        protected override void SaveOrderBySetting()
        {
        }
    }
    
}
