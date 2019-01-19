using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Extensions;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using Lime.Xaml.Helpers;
using Microsoft.Graph;
using Microsoft.OneDrive.Sdk;
using Microsoft.OneDrive.Sdk.Authentication;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.UWP.ViewModels
{
    public class CloudViewModel : RemoteFileViewModelBase
    {
        [DoNotNotify]
        public IOneDriveClient OneDriveClient { get; set; }

        [DoNotNotify]
        public IAuthenticationProvider AuthProvider { get; set; }

        public bool HasUpperFolder { get { return pathStack.Count > 0; } }
        
        private Stack<NetworkItemInfo> pathStack = new Stack<NetworkItemInfo>();
        private readonly string oneDriveConsumerClientId = "56d883d1-9d6a-4b34-8457-db1170fa5b02"; //CCPlayer OneDrive Connector
        private readonly string oneDriveConsumerReturnUrl = "https://login.live.com/oauth20_desktop.srf";
        private readonly string oneDriveConsumerBaseUrl = "https://api.onedrive.com/v1.0";
        private readonly string[] scopes = new string[] { "onedrive.readonly", "wl.signin", "offline_access" };

        protected override void FakeIocInstanceInitialize() {}

        protected override void CreateModel()
        {
            _ThumbnailListInCurrentFolder = new List<Models.Thumbnail>();
            NetworkItemGroupSource = new ObservableCollection<NetworkItemGroup>();

            OrderBySource = new ObservableCollection<KeyName>();
            OrderBy = SortType.Name.ToString();

            // Get the letters representing each group for current language using CharacterGroupings class
            CreateCharacterGroupings();
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<bool>(this, "CloudBackRequested", (val) =>
            {
                ToUpperTapped(null, null);
            });

            MessengerInstance.Register<Message>(this, "CloudNextPlayListFile", (val) =>
            {
                DecoderTypes decoderType = val.GetValue<DecoderTypes>("DecoderType");
                NetworkItemInfo playListFile = val.GetValue<NetworkItemInfo>("NextPlayListFile");

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
            ChangeStyleSelector("NetworkItemStyleSelector", e.Size.Width);
        }

        protected override void InitializeViewModel()
        {
            ServerType = ServerTypes.OneDrive.ToString();

            var resource = ResourceLoader.GetForCurrentView();
            Title = resource.GetString("Cloud/OneDrive/Text");

            //정렬 콤보 생성
            CreateOrderBySource();
        }
        #region 폴더 로딩

        private async void ConnectOneDriveServer()
        {
            //이미 연결중이면 수행하지 않음.
            if (IsConnecting) return;

            //현재 연결이 되어 있다면 연결 해제
            await DisconnectOneDriveServer(false);

            //연결중...
            IsConnecting = true;
            ShowErrorMessage = false;
            
            var resource = ResourceLoader.GetForCurrentView();
            string errMsg = resource.GetString("Message/Error/FailServerAuthentication"); //"인증에 실패하였습니다. 아이디/패스워드를 확인하세요. ";
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            Task authTask = null;

            var msaAuthProvider = new MsaAuthenticationProvider(
                this.oneDriveConsumerClientId,
                this.oneDriveConsumerReturnUrl,
                this.scopes,
                new CredentialVault(this.oneDriveConsumerClientId));
            
            await msaAuthProvider.SignOutAsync();
            authTask = msaAuthProvider.RestoreMostRecentFromCacheOrAuthenticateUserAsync();
            
            OneDriveClient = new OneDriveClient(this.oneDriveConsumerBaseUrl, msaAuthProvider);
            AuthProvider = msaAuthProvider;

            try
            {
                await authTask;

                //서버타입 설정
                ConnectedServerType = ServerTypes.OneDrive;

                await LoadOneDriveFoldersAsync();

                //인증 성공
                IsConnecting = false;
                IsDisconnected = false;
            }
            catch (ServiceException exception)
            {
                // Swallow the auth exception but write message for debugging.
                Debug.WriteLine(exception.Error.Message);

                pathStack.Clear();
                IsConnecting = false;
                IsDisconnected = true;
                ShowOrderBy = false;
            }
        }

        private bool IsDirectory(NetworkItemInfo item)
        {
            return !item.IsFile;
        }

        private async void LoadOneDriveFolders()
        {
            //세션 체크
            if (OneDriveClient == null) return;
            
            //폴더 및 파일 로딩 
            await LoadOneDriveFoldersAsync();
        }

        private void ChangeToUpperAndBackButton()
        {
            GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (!pathStack.Any())
                {
                    //뒤로버튼 상태 변경
                    Frame rootFrame = Window.Current.Content as Frame;
                    if (rootFrame != null && !rootFrame.CanGoBack)
                    {
                        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                    }
                }
                else
                {
                    if (Settings.General.UseHardwareBackButtonWithinVideo)
                    {
                        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    }
                }

                //상위 이동 버튼 처리
                VisibleToUpper = pathStack.Any();
            });
        }
        
        private async Task LoadOneDriveFoldersAsync()
        {
            if (_CancellationTokenSource != null)
                //썸네일 로드 실행전 이전 요청을 취소시킴
                _CancellationTokenSource.Cancel();

            if (_CancellationTokenSource == null || _CancellationTokenSource.IsCancellationRequested)
                _CancellationTokenSource = new CancellationTokenSource();

            _CurrentSubtitleFileList = null;

            //모든 자식 요소 삭제
            NetworkItemGroupSource.Clear();
            //로딩 표시 
            IsLoadingFolders = true;
            IsLoadingFiles = true;
            
            var folderGroupName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");
            var fileGroupName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            string path = string.Join("/", pathStack.Select(x => x.Name).Reverse());
            //경로를 타이틀 영역에 표시
            DisplayCurrentPath = path;

            await ThreadPool.RunAsync(async handler =>
            {
                try
                {
                    IItemChildrenCollectionPage page = null;
                    //var suffixed = MediaFileSuffixes.VIDEO_SUFFIX.Union(MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);
                    //var endswithFilter = string.Join(" or ", suffixed.Select(s => $"endswith(name, '{s}')"));
                    var filterString = "folder ne null or (file ne null and image eq null and audio eq null)";
                    var sortString = _Sort == SortType.Name ? "name" : _Sort == SortType.NameDescending ? "name desc" : _Sort == SortType.CreatedDate ? "lastModifiedDateTime" : "lastModifiedDateTime desc";


                    if (pathStack.Any())
                    {
                        //var itemRequest = OneDriveClient.Drive.Items[networkItem.Id].Children.Request();// new List<Option> { new QueryOption("filter", "folder ne null or video ne null or (file ne null and image eq null and audio eq null)") });
                        var itemRequest = OneDriveClient.Drive.Root.ItemWithPath(path).Children.Request();
                        page = await itemRequest.Filter(filterString)
                                                .Expand("thumbnails(select=medium)")
                                                .OrderBy(sortString)
                                                .GetAsync();
                    }
                    else
                    {
                        var itemRequest = OneDriveClient.Drive.Root.Children.Request();//.Expand("thumbnails,children(expand=thumbnails)");
                        page = await itemRequest.Filter(filterString)
                                                .Expand("thumbnails(select=medium)")
                                                .OrderBy(sortString)
                                                .GetAsync();
                    }

                    //상위 버튼 및 하드웨어 백버튼 변경 처리
                    ChangeToUpperAndBackButton();
                    
                    _ThumbnailListInCurrentFolder.Clear();
                    //기간 지난 썸네일 캐시 삭제 
                    ThumbnailDAO.DeletePastPeriodThumbnail(Settings.Thumbnail.RetentionPeriod);
                    //썸네일 캐시 (이미지 바이트 데이터 제외)를 로드
                    LoadThumbnailMetadataInFolder(page.CurrentPage);

                    var ffList = page.CurrentPage.Select(x => x.ToNetworkItemInfo(NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName));
                    var folderList = ffList.Where(x => !x.IsFile).ToList();
                    await LoadBatchOneDriveFolderAsync(folderList, folderGroupName);
                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => IsLoadingFolders = false);

                    var fileList = ffList.Where(x => x.IsFile).ToList();
                    await LoadBatchOneDriveFilesAsync(fileList, fileGroupName);
                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => IsLoadingFiles = false);

                    //페이징 처리
                    await Task.Factory.StartNew(async () =>
                    {
                        //폴더 로딩 표시기 시작
                        await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => { IsLoadingFolders = true; IsLoadingFiles = true; });
                        while (page.NextPageRequest != null)
                        {
                            //다음 페이지 패치
                            page = await page.NextPageRequest.GetAsync();

                            //썸네일 캐시 (이미지 바이트 데이터 제외)를 로드
                            LoadThumbnailMetadataInFolder(page.CurrentPage);

                            ffList = page.CurrentPage.Select(x => x.ToNetworkItemInfo(NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName));
                            folderList = ffList.Where(x => !x.IsFile).ToList();
                            await LoadBatchOneDriveFolderAsync(folderList, folderGroupName);

                            fileList = ffList.Where(x => x.IsFile).ToList();
                            await LoadBatchOneDriveFilesAsync(fileList, fileGroupName);
                        }

                        //폴더 로딩 표시기 정지
                        await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => { IsLoadingFolders = false; IsLoadingFiles = false; });
                    });
                }
                catch (Exception)
                {
                    GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(async () =>
                    {
                        await DisconnectOneDriveServer(true);
                    });
                }
            });
        }

        private void LoadThumbnailMetadataInFolder(IList<Item> itemList)
        {
            if (itemList.Any() && !_ThumbnailListInCurrentFolder.Any())
            {
                var thumbDirectory = itemList.Where(x => x.ParentReference != null).Select(x => x.ParentReference.Path).FirstOrDefault();
                ThumbnailDAO.LoadThumnailInFolder(thumbDirectory, _ThumbnailListInCurrentFolder);
            }
        }

        private async Task LoadBatchOneDriveFolderAsync(IEnumerable<NetworkItemInfo> folderList, string folderGroupName)
        {
            if ((bool)folderList?.Any())
            {
                //신규 그룹 생성
                NetworkItemGroup group = null;
                group = NetworkItemGroupSource.FirstOrDefault(x => x.Type == StorageItemTypes.Folder);
                if (group == null)
                {
                    group = new NetworkItemGroup(StorageItemTypes.Folder, folderGroupName);
                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => NetworkItemGroupSource.Insert(0, group));
                }
                foreach (var folderItem in folderList)
                {
                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => group.Items.Add(folderItem));
                }
                
                //폴더 썸네일 로드
                LoadOneDriveFoldersThumbnail(folderList, _CancellationTokenSource.Token);
            }
        }

        CancellationTokenSource _CancellationTokenSource;
        private async Task LoadBatchOneDriveFilesAsync(IEnumerable<NetworkItemInfo> fileList, string fileGroupName)
        {
            if (fileList != null)
            {
                var videoList = fileList.Where(x => MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)));
                //자막 리스트 생성
                _CurrentSubtitleFileList = fileList.Where(x => MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y))).Select(x => x.Name).ToList();

                int itemCount = videoList.Count();
                int itemTotalCount = itemCount;
                await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => itemTotalCount += NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).Count());

                NetworkItemGroup group = null;
                var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
                var isMultiGroup = isOrderByName && itemTotalCount > GROUP_MAX_ITME_COUNT;
                string groupName = isMultiGroup ? null : fileGroupName;

                foreach (var videoItem in videoList)
                {
                    if (isMultiGroup)
                    {
                        groupName = _CharacterGroupings.Lookup(videoItem.Name);
                        var tmp = NetworkItemGroupSource.FirstOrDefault(x => x.Name == fileGroupName);
                        if (tmp != null)
                        {
                            //페이징으로 인해 파일이 계속적으로 추가되어 그룹이 변경되는 경우에 다시 그룹에 추가
                            await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
                            {
                                NetworkItemGroupSource.Remove(tmp);
                                var items = tmp.Items;

                                foreach (var old in tmp.Items)
                                {
                                    var tmpGroupName = _CharacterGroupings.Lookup(old.Name);
                                    var tmpGroup = NetworkItemGroupSource.FirstOrDefault(x => x.Name == tmpGroupName);

                                    if (tmpGroup == null)
                                    {
                                        tmpGroup = new NetworkItemGroup(StorageItemTypes.File, tmpGroupName);
                                        NetworkItemGroupSource.Add(tmpGroup);
                                    }

                                    tmpGroup.Items.Add(old);
                                }
                            });
                        }

                    }
                    //그룹이 변경되는 경우
                    if (group == null || group.Name != groupName)
                    {
                        //신규 그룹 생성
                        await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() =>
                        {
                            group = NetworkItemGroupSource.FirstOrDefault(x => x.Name == groupName);
                            if (group == null)
                            {
                                group = new NetworkItemGroup(StorageItemTypes.File, groupName);
                                NetworkItemGroupSource.Add(group);
                            }
                        });
                    }

                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(() => group.Items.Add(videoItem));
                }
                
                LoadOneDriveFilesThumbnail(videoList, _CancellationTokenSource.Token);
            }
        }
        #endregion

        //원드라이브로 부터 썸네일로 이미지를 다운로드 받아, 
        //NetworItemInfo에 이미지를 로딩 시킨후 DB에 캐쉬로 저장한다.
        private async Task StoreImageFromWeb(NetworkItemInfo networkItem, Microsoft.OneDrive.Sdk.Thumbnail itemThumb)
        {
            if (itemThumb != null)
            {
                try
                {
                    var webReq = System.Net.WebRequest.Create(itemThumb.Url);
                    await GalaSoft.MvvmLight.Threading.DispatcherHelper.RunAsync(async () =>
                    {
                        using (var webRes = await webReq.GetResponseAsync())
                        {
                            using (var imageStream = webRes.GetResponseStream())
                            {
                                WriteableBitmap wb = await BitmapFactory.FromStream(imageStream);
                                networkItem.ImageItemsSource = wb;

                                using (InMemoryRandomAccessStream newStream = new InMemoryRandomAccessStream())
                                {
                                    await wb.ToStream(newStream, BitmapEncoder.PngEncoderId);
                                    byte[] pngData = new byte[newStream.Size];
                                    await newStream.ReadAsync(pngData.AsBuffer(), (uint)pngData.Length, InputStreamOptions.None);

                                    //DB 등록
                                    ThumbnailDAO.InsertThumbnail(new Models.Thumbnail
                                    {
                                        Name = networkItem.Name,
                                        ParentPath = networkItem.ParentFolderPath,
                                        Size = (ulong)networkItem.Size,
                                        RunningTime = networkItem.Duration,
                                        CreatedDateTime = networkItem.Modified,
                                        ThumbnailData = pngData
                                    });
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }
        
        private void LoadOneDriveFoldersThumbnail(IEnumerable<NetworkItemInfo> networkItemList, CancellationToken cancelToken)
        {
            Task.Factory.StartNew(() =>
            {
                ParallelOptions options = new ParallelOptions();
                options.CancellationToken = cancelToken;

                Parallel.ForEach(networkItemList, options, async networkItem =>
                {
                    //썸네일 로드
                    if (Settings.Thumbnail.UseUnsupportedOneDriveFolder)
                    {
                        var thumbnail = FindThumbnail(networkItem);
                        var requestBuilder = OneDriveClient.Drive.Root.ItemWithPath(GetAbsolutePath(networkItem));

                        var items = await requestBuilder.Children.Request().Filter("folder ne null or (file ne null and image eq null and audio eq null)").GetAsync();
                        networkItem.FileCount = items.Where(x => x.File != null && MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y))).Count();

                        var folderCount = items.Count(x => x.Folder != null);

                        if (networkItem.FileCount > 0)
                            networkItem.FileCountDescription = networkItem.FileCount.ToString() + (folderCount > 0 ? "+" : string.Empty);
                        else
                            networkItem.FileCountDescription = "0" + (folderCount > 0 || items.NextPageRequest != null ? "*" : string.Empty);

                        if (thumbnail == null)
                        {
                            if (requestBuilder.Thumbnails != null)
                            {
                                if (networkItem.FileCount > 0 || networkItem.FileCountDescription == "+")
                                {
                                    var itemThumbPage = await requestBuilder.Thumbnails.Request().GetAsync();
                                    var itemThumb = itemThumbPage.CurrentPage.FirstOrDefault()?.Medium;
                                    if (itemThumb != null)
                                        //이미지 다운로드 및 저장
                                        await StoreImageFromWeb(networkItem, itemThumb);
                                    else
                                        //컨텐츠를 다운로드 받아 ffmpeg으로 미디어 정보를 로딩
                                        await LoadMediaInformationByFFmepg(networkItem, requestBuilder);
                                }
                            }
                            else
                            {
                                //컨텐츠를 다운로드 받아 ffmpeg으로 미디어 정보를 로딩
                                await LoadMediaInformationByFFmepg(networkItem, requestBuilder);
                            }
                        }
                        else
                        {
                            //캐싱 이미지 로드
                            LoadCachedThumbnail(networkItem, thumbnail);
                        }
                    }
                    else
                    {
                        networkItem.FileCountDescription = "?";
                    }
                });
            }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
            {
                System.Diagnostics.Debug.WriteLine("원드라이브 폴더 썸네일 로드 패러럴 종료");
            });
        }

        private string GetAbsolutePath(NetworkItemInfo networkItem)
        {
            return GetAbsolutePath(networkItem.ParentFolderPath, networkItem.Name);
        }

        private string GetAbsolutePath(string parentPath, string name)
        {
            string rootPath = "/drive/root:";
            string absolutePath = string.Empty;
            if (parentPath.StartsWith(rootPath))
            {
                absolutePath = parentPath.Substring(rootPath.Length) + "/";
                while (absolutePath.StartsWith("/"))
                {
                    absolutePath = absolutePath.Substring(1);
                }
            }

            return absolutePath + name;
        }

        private Models.Thumbnail FindThumbnail(NetworkItemInfo networkItem)
        {
            var thumbnail = _ThumbnailListInCurrentFolder?.FirstOrDefault(x =>
                            x.Name == networkItem.Name.ToLower() &&
                            x.ParentPath == networkItem.ParentFolderPath.ToLower() &&
                            x.Size == (ulong)networkItem.Size &&
                            x.CreatedDateTime == networkItem.Modified);
            return thumbnail;
        }
        
        private async Task LoadMediaInformationByFFmepg(NetworkItemInfo networkItem, IItemRequestBuilder requestBuilder)
        {
            try
            {
                using (Stream stream = await requestBuilder.Content.Request().GetAsync())
                {
                    var ffInfo = CCPlayer.UWP.Factory.MediaInformationFactory.CreateMediaInformationFromStream(stream.AsRandomAccessStream());
                    networkItem.ImageItemsSource = await this.GetThumbnailAsync(networkItem, ffInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void LoadOneDriveFilesThumbnail(IEnumerable<NetworkItemInfo> networkItemList, CancellationToken cancelToken)
        {
            Task.Factory.StartNew(() =>
            {
                ParallelOptions options = new ParallelOptions();
                options.CancellationToken = cancelToken;

                Parallel.ForEach(networkItemList, options, async networkItem =>
                {
                    var thumbnail = FindThumbnail(networkItem);

                    //썸네일 로드
                    if (Settings.Thumbnail.UseUnsupportedOneDriveFile)
                    {
                        if (thumbnail == null)
                        {
                            var requestBuilder = OneDriveClient.Drive.Root.ItemWithPath(GetAbsolutePath(networkItem));

                            if (requestBuilder.Thumbnails != null)
                            {
                                var item = await requestBuilder.Request().GetAsync();
                                if (item?.Video != null)
                                {
                                    //재생 시간 설정
                                    networkItem.Duration = TimeSpan.FromMilliseconds((double)item.Video.Duration);
                                }
                                var itemThumbPage = await requestBuilder.Thumbnails.Request().GetAsync();
                                var itemThumb = itemThumbPage.CurrentPage.FirstOrDefault()?.Medium;

                                if (itemThumb != null)
                                    //이미지 다운로드 및 저장
                                    await StoreImageFromWeb(networkItem, itemThumb);
                                else
                                    //컨텐츠를 다운로드 받아 ffmpeg으로 미디어 정보를 로딩
                                    await LoadMediaInformationByFFmepg(networkItem, requestBuilder);
                            }
                            else
                            {
                                //컨텐츠를 다운로드 받아 ffmpeg으로 미디어 정보를 로딩
                                await LoadMediaInformationByFFmepg(networkItem, requestBuilder);
                            }
                        }
                        else
                        {
                            //캐싱 이미지 로드
                            LoadCachedThumbnail(networkItem, thumbnail);
                        }
                    }

                    //자막 목록 설정
                    if (_CurrentSubtitleFileList != null && _CurrentSubtitleFileList.Any())
                    {
                        networkItem.SubtitleList = _CurrentSubtitleFileList.Where(x => System.Net.WebUtility.UrlDecode(x).ToUpper().Contains(System.IO.Path.GetFileNameWithoutExtension(networkItem.Name).ToUpper())).ToList();
                    }
                });
            }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
            {
                System.Diagnostics.Debug.WriteLine("원드라이브 파일 썸네일 로드 패러럴 종료");
            });
        }
        
        //OneDrive API는 HTTPS 기반이라 FFmpeg을 --eanbled openssl로 빌드하지 않으면 스트리밍 할 수강 없음
        //Workaround : HTTPS Content API의 Url을 추출하여를 URL을 WebRequest로 호출한 후 응답 url을 추출하여 scheme을 http로 강제 변환후 호출하면 http로 받을 수 있음
        private async Task<Uri> GetStreamingUrlAsync(string path)
        {
            var provider = OneDriveClient.AuthenticationProvider as MsaAuthenticationProvider;
            var url = OneDriveClient.Drive.Root.ItemWithPath(path).Content.Request().RequestUrl + "?access_token=" + provider.CurrentAccountSession.AccessToken;

            var webReq = System.Net.WebRequest.Create(url);
            var webRes = await webReq.GetResponseAsync();

            var uri = webRes.ResponseUri;
            //string url2 = $"http://{uri.Host}:{uri.Port}{uri.LocalPath}";

            //var mediaInformation = Factory.MediaInformationFactory.CreateMediaInformationFromUri(uri.AbsoluteUri);
            //var mediaInformation = Factory.MediaInformationFactory.CreateMediaInformationFromUri(uri.AbsoluteUri.Replace("https://", "http://"));

            return new Uri(uri.AbsoluteUri.Replace("https://", "http://"));
        }

        private async void ShowMediaInfoFlyout(NetworkItemInfo item)
        {
            if (item == null || !item.IsFile) return;
            
            MessengerInstance.Send(new Message("IsOpen", true)
                                    .Add("LoadingTitle", ResourceLoader.GetForCurrentView().GetString("Loading/MediaInformation/Text")), 
                                    "ShowLoadingPanel");

            item.Uri = await GetStreamingUrlAsync(GetAbsolutePath(item));

            MessengerInstance.Send<Message<DecoderTypes>>(
                    new Message<DecoderTypes>((decoderType) =>
                    {
                        RequestPlayback(decoderType, item, false);
                    })
                    .Add("NetworkItemInfo", item)
                    .Add("ButtonName", "CodecInformation"),
                    "ShowMediaFileInformation");
        }

        private async void RequestPlayback(DecoderTypes decoderType, NetworkItemInfo networkItemInfo, bool isPrevOrNext)
        {
            if (!VersionHelper.IsUnlockNetworkPlay)
            {
                MessengerInstance.Send(new Message(), "CheckInterstitialAd");
                return;
            }

            MessengerInstance.Send(new Message("IsOpen", true)
                .Add("LoadingTitle", ResourceLoader.GetForCurrentView().GetString("Loading/Playback/Text")), "ShowLoadingPanel");

            var items = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).ToList();
            var item = networkItemInfo;
            var index = items.IndexOf(item);
            
            NetworkItemInfo prevItemInfo = null;
            NetworkItemInfo currItemInfo = item;
            NetworkItemInfo nextItemInfo = null;

            if ((bool)items?.Any())
            {
                if (index > 0)
                {
                    prevItemInfo = items[index - 1];
                }

                if (index < items.Count - 1)
                {
                    nextItemInfo = items[index + 1];
                }
            }

            var msg = new Message()
                .Add("PrevPlayListFile", prevItemInfo)
                .Add("CurrPlayListFile", currItemInfo)
                .Add("NextPlayListFile", nextItemInfo)
                .Add("DecoderType", decoderType);

            //Stream videoStream = null;
            //IItemContentRequest request = null;

            switch (ConnectedServerType)
            {
                case ServerTypes.OneDrive:
                    /*
                    request = OneDriveClient.Drive.Root.ItemWithPath(GetAbsolutePath(networkItemInfo)).Content.Request();
                   // videoStream = await request.GetAsync();

                    var provider = OneDriveClient.AuthenticationProvider as MsaAuthenticationProvider;
                    var url = request.RequestUrl + "?access_token=" + provider.CurrentAccountSession.AccessToken;

                    var webReq = System.Net.WebRequest.Create(url);
                    var webRes = await webReq.GetResponseAsync();
                    //Stream stream = webRes.GetResponseStream();
                    url = url.Replace("https://", "http://");

                    //        msg.Add("CurrPlayListFileStream", stream.AsRandomAccessStream());
                    msg.Add("CurrPlayListFileUrl", url);
                    msg.Add("CurrPlayListFileContentType", request.ContentType);
                    */

                    for(int i = 0; i < currItemInfo.SubtitleList?.Count; i++)
                    {
                        currItemInfo.SubtitleList[i] = (await GetStreamingUrlAsync(GetAbsolutePath(currItemInfo.ParentFolderPath, currItemInfo.SubtitleList[i]))).AbsoluteUri;
                    }

                    currItemInfo.Uri = await GetStreamingUrlAsync(GetAbsolutePath(currItemInfo));
                    break;
            }
            
            MessengerInstance.Send(msg, "RequestPlayback");
        }

        private void NetworkItemTapped(object sender, TappedRoutedEventArgs args)
        {
            NetworkItemInfo nii = null;
            var elem = args.OriginalSource as FrameworkElement;
            if (elem != null && (nii = elem.DataContext as NetworkItemInfo) != null)
            {
                if (nii.IsFile)
                    RequestPlayback(Settings.Playback.DefaultDecoderType, nii, false);
                else
                {
                    pathStack.Push(nii);

                    switch (nii.ServerType)
                    {
                        case ServerTypes.OneDrive:
                            LoadOneDriveFolders();
                            break;
                    }
                }
            }
        }

        private void NetworkItemRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            if (args.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var elem = args.OriginalSource as FrameworkElement;
                ShowMediaInfoFlyout(elem?.DataContext as NetworkItemInfo);
            }
        }

        private void NetworkItemHolding(object sender, HoldingRoutedEventArgs args)
        {
            if (args.HoldingState == Windows.UI.Input.HoldingState.Started
                && args.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var elem = args.OriginalSource as FrameworkElement;
                ShowMediaInfoFlyout(elem?.DataContext as NetworkItemInfo);
            }
        }

        public async void DisconnectServerTapped(object sender, TappedRoutedEventArgs e)
        {
            switch (ConnectedServerType)
            {
                case ServerTypes.OneDrive:
                    await DisconnectOneDriveServer(false);
                    break;
            };
        }

        private async Task DisconnectOneDriveServer(bool showMessage)
        {
            if (OneDriveClient?.AuthenticationProvider != null)
            {
                var authProvider = OneDriveClient?.AuthenticationProvider as MsaAuthenticationProvider;
                if (authProvider != null && authProvider.IsAuthenticated)
                {
                    await authProvider.SignOutAsync();
                }
                OneDriveClient = null;                
            }

            await DisconnectServer(showMessage);
        }
        
        private async Task DisconnectServer(bool showMessage, string msg = null)
        {
            pathStack.Clear();
            NetworkItemGroupSource.Clear();

            IsLoadingFolders = false;
            IsLoadingFiles = false;
            IsConnecting = false;
            IsDisconnected = true;
            ShowOrderBy = false;
            VisibleToUpper = false;
            //CurrentFolderInfo = null;
            DisplayCurrentPath = string.Empty;

            if (showMessage)
            {
                var resource = ResourceLoader.GetForCurrentView();
                if (string.IsNullOrEmpty(msg))
                    msg = resource.GetString("Message/Error/DisconnectedServer");

                var dlg = DialogHelper.GetSimpleContentDialog(
                    resource.GetString("Server/Disonnect/General/Text"),
                    msg,
                    resource.GetString("Button/Close/Content"));
                await dlg.ShowAsync();
                App.ContentDlgOp = null;
            }
        }

        public void AuthenticationTapped(object sender, TappedRoutedEventArgs e)
        {
            switch (ServerTypes)
            {
                case ServerTypes.OneDrive:
                    ConnectOneDriveServer();
                    break;
            }
        }
        
        public void ToUpperTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator || !VisibleToUpper)
                return;

            if (pathStack.Count > 0)
                pathStack.Pop();

            switch (ServerTypes)
            {
                case ServerTypes.OneDrive:
                    LoadOneDriveFolders();
                    break;
            }
        }

        public void RefreshTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator)
                return;

            switch (ServerTypes)
            {
                case ServerTypes.OneDrive:
                    LoadOneDriveFolders();
                    break;
            }

        }
                
        public void GridViewLoaded(object sender, RoutedEventArgs e)
        {
            var gridView = sender as GridView;
            if (gridView != null)
            {
                var page = ElementHelper.FindVisualParent<Views.CloudPage>(gridView);
                if (page != null)
                {
                    Resources = page.Resources;
                    //초기값 로드
                    ChangeStyleSelector("NetworkItemStyleSelector", Windows.UI.Xaml.Window.Current.Bounds.Width);
                }
            }

            //로딩된 리스트가 존재하는 경우, 설정에서 back버튼이 활성화 되어 있으면 상위 폴더가 존재하는 경우 버튼을 활성화 시킴
            if (Settings.General.UseHardwareBackButtonWithinVideo
                && pathStack.Any())
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
        }
      
        
    }
}
