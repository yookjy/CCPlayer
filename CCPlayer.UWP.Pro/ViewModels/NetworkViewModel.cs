using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Extensions;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using Cubisoft.Winrt.Ftp;
using DecaTec.WebDav;
using GalaSoft.MvvmLight.Threading;
using Lime.Xaml.Helpers;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Networking;
using Windows.Security.Credentials;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.Web.Http.Filters;

namespace CCPlayer.UWP.ViewModels
{
    public class NetworkViewModel : RemoteFileViewModelBase
    {
        private const string CONTENT_TYPE_DIRECTORY = "httpd/unix-directory";

        public NetworkItemInfo CurrentFolderInfo
        {
            get { return _CurrentFolderInfo; }
            set
            {
                if (Set(ref _CurrentFolderInfo, value))
                {
                    if (_CurrentFolderInfo?.Uri != null)
                    {
                        DisplayCurrentPath = DecaTec.WebDav.Tools.UriHelper.AddTrailingSlash(_CurrentFolderInfo.Uri?.ToString());
                    }
                }

                if (string.IsNullOrEmpty(DisplayCurrentPath))
                {
                    DisplayCurrentPath = GetUri(false)?.ToString();
                }
            }
        }
        
        public string ServerDirectSubtitleAddress { get; set; }

        public string ServerDirectVideoAddress { get; set; }

        public string ServerHost { get; set; }

        public string ServerPath { get; set; }

        public string ServerUserName { get; set; }

        public string ServerPassword { get; set; }

        public string ServerPort { get; set; }

        public bool? IsSSL { get; set; }

        [DoNotNotify]
        public ObservableCollection<KeyName> ServerTypeSource { get; set; }

        public bool HasUpperFolder { get { return pathStack.Count > 0; } }

        private Stack<NetworkItemInfo> pathStack = new Stack<NetworkItemInfo>();
        private WebDavSession _Session;
        private FtpClient _FtpClient;
        public NetworkItemInfo _CurrentFolderInfo;        
        
        protected override void FakeIocInstanceInitialize()
        {
        }

        protected override void CreateModel()
        {
            ServerTypeSource = new ObservableCollection<KeyName>();

            _ThumbnailListInCurrentFolder = new List<Models.Thumbnail>();
            NetworkItemGroupSource = new ObservableCollection<NetworkItemGroup>();

            OrderBySource = new ObservableCollection<KeyName>();
            OrderBy = SortType.Name.ToString();

            // Get the letters representing each group for current language using CharacterGroupings class
            CreateCharacterGroupings();
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<bool>(this, "NetworkBackRequested", (val) =>
            {
                ToUpperTapped(null, null);
            });

            MessengerInstance.Register<Message>(this, "NetworkNextPlayListFile", (val) =>
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
            ServerType = ServerTypes.WebDAV.ToString();

            var resource = ResourceLoader.GetForCurrentView();
            Title = $"{resource.GetString("AddFolder/WebDAV/Text")}/{resource.GetString("AddFolder/FTP/Text")}";
            //정렬 콤보 생성
            CreateOrderBySource();

            ServerTypeSource.Add(new KeyName(ServerTypes.WebDAV.ToString(), resource.GetString("AddFolder/WebDAV/Text")));
            ServerTypeSource.Add(new KeyName(ServerTypes.FTP.ToString(), resource.GetString("AddFolder/FTP/Text")));
            ServerTypeSource.Add(new KeyName(ServerTypes.Direct.ToString(), resource.GetString("AddFolder/Direct/Text")));

        }
        #region 폴더 로딩

        private void TrimData()
        {
            ServerHost = ServerHost?.Trim().Trim('/');
            ServerUserName = ServerUserName?.Trim();
            ServerPassword = ServerPassword?.Trim();
            ServerPath = ServerPath?.Trim().Trim('/');
            ServerPort = ServerPort?.Trim();
        }

        private bool ValidateData()
        {
            var resource = ResourceLoader.GetForCurrentView();
            if (string.IsNullOrWhiteSpace(ServerHost))
            {
                ErrorMessage = resource.GetString("Message/Error/ServerAddress"); //"(서버 주소)를 입력하세요.";
                ShowErrorMessage = true;
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerUserName))
            {
                ErrorMessage = resource.GetString("Message/Error/UserName"); //"(사용자 아이디)를 입력하세요.";
                ShowErrorMessage = true;
                return false; 
            }

            if (string.IsNullOrWhiteSpace(ServerPassword))
            {
                ErrorMessage = resource.GetString("Message/Error/Password");//"(패스워드)를 입력하세요.";
                ShowErrorMessage = true;
                return false;
            }

            return true;
        }

        private Uri GetUri(bool useAuth = false, string addPath = "")
        {
            UriBuilder builder = null;
            int nPort = 0;
            string scheme = string.Empty;
            string host = string.Empty;
            string path = string.Empty;
            string user = string.Empty;
            string pwd = string.Empty;
            string port = string.Empty;

            switch(ServerTypes)
            {
                case ServerTypes.WebDAV:
                    scheme = Settings.Server.WebDAVSSL ? "https" : "http";
                    host = Settings.Server.WebDAVHost;
                    port = Settings.Server.WebDAVPort;
                    path = string.Join("/", new string[] { Settings.Server.WebDAVPath, addPath });
                    user = Settings.Server.WebDAVUserName;
                    pwd = Settings.Server.WebDAVPassword;
                    break;
                case ServerTypes.FTP:
                    scheme = "ftp";
                    host = Settings.Server.FtpHost;
                    path = addPath;
                    port = Settings.Server.FtpPort;
                    user = Settings.Server.FtpUserName;
                    pwd = Settings.Server.FtpPassword;
                    break;
            }
            
            try
            {
                if (int.TryParse(port, out nPort))
                {
                    builder = new UriBuilder(scheme, host, nPort, path);
                }
                else
                {
                    builder = new UriBuilder(scheme, host);
                    builder.Path = path;
                }

                if (useAuth)
                {
                    builder.UserName = user;
                    builder.Password = pwd;
                }
                return builder.Uri;
            }
            catch(Exception ex)
            {
                ErrorMessage = ex.Message;
                ShowErrorMessage = true;
            }
            return null;
        }

        private void StoreWebDAVSettings()
        {
            if (ServerHost.StartsWith("http://"))
                ServerHost = ServerHost.Remove(0, "http://".Length);

            if (ServerHost.StartsWith("https://"))
                ServerHost = ServerHost.Remove(0, "https://".Length);

            var segments = ServerHost?.Split('/');
            if (segments?.Length > 0)
                ServerHost = segments[0];

            segments = ServerPath?.Split('/');
            if (segments?.Length > 0)
            {
                ServerPath = segments[0];
            }

            Settings.Server.WebDAVSSL = (bool)IsSSL;
            Settings.Server.WebDAVHost = ServerHost;
            Settings.Server.WebDAVPath = ServerPath;
            Settings.Server.WebDAVPort = ServerPort;
            Settings.Server.WebDAVUserName = ServerUserName;
            Settings.Server.WebDAVPassword = ServerPassword;
        }

        private void StoreFtpSettings()
        {
            if (ServerHost.StartsWith("ftp://"))
                ServerHost = ServerHost.Remove(0, "ftp://".Length);

            var segments = ServerHost?.Split('/');
            if (segments?.Length > 0)
                ServerHost = segments[0];

            Settings.Server.FtpHost = ServerHost;
            Settings.Server.FtpPort = ServerPort;
            Settings.Server.FtpUserName = ServerUserName;
            Settings.Server.FtpPassword = ServerPassword;
        }

        private async void ConnectWebDAVServer()
        {
            //이미 연결중이면 수행하지 않음.
            if (IsConnecting) return;

            //현재 연결이 되어 있다면 연결 해제
            await DisconnectWebDAVServer(false);
            await DisconnectFtpServer(false);

            //입력된 데이터 트림
            TrimData();

            //데이터 체크
            if (!ValidateData())
                return;

            //연결중...
            IsConnecting = true;
            ShowErrorMessage = false;
            Uri uri = null;
            var resource = ResourceLoader.GetForCurrentView();
            string errMsg = resource.GetString("Message/Error/FailServerAuthentication"); //"인증에 실패하였습니다. 아이디/패스워드를 확인하세요. ";
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            //입력 접속 정보를 셋팅 데이터에 저장
            StoreWebDAVSettings();
            uri = GetUri();

            if (uri == null)
            {
                IsConnecting = false;
                return;
            }
            /*
            // The base URL of the WebDAV server.
            // Specify the user credentials and use it to create a WebDavSession instance.
            var credentials = new PasswordCredential(uri.AbsoluteUri, Settings.Server.WebDAVUserName, Settings.Server.WebDAVPassword);
            var httpBaseProtocolFilter = new HttpBaseProtocolFilter();
            httpBaseProtocolFilter.ServerCredential = credentials;

            // Specify the certificate errors which should be ignored.
            // It is recommended to only ignore expired or untrusted certificate errors.
            // When an invalid certificate is used by the WebDAV server and these errors are not ignored, an exception will be thrown when trying to access WebDAV resources.
            httpBaseProtocolFilter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            httpBaseProtocolFilter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            httpBaseProtocolFilter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
            //계정 입력 다이얼로그 비활성화
            httpBaseProtocolFilter.AllowUI = false;

            _Session = new WebDavSession(uri, httpBaseProtocolFilter);
            */
            var credentials = new NetworkCredential(Settings.Server.WebDAVUserName, Settings.Server.WebDAVPassword);
            _Session = new WebDavSession(uri.AbsoluteUri, credentials);

            await ThreadPool.RunAsync(async handler =>
            {
                try
                {
                    var listItem = await _Session.ListAsync(@"/");
                    errMsg = null;
                    var aa = listItem.ToList();
                    if (listItem != null)
                    {
                        await DispatcherHelper.RunAsync(async () =>
                        {
                            //접속된 서버 타입
                            ConnectedServerType = ServerTypes.WebDAV;
                            //루트 로드
                            //var folderList = listItem.Where(x => IsDirectory(x))
                            //                         .Select(x => x.ToNetworkItemInfo(NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName));
                            var rootList = listItem.Select(x => x.ToNetworkItemInfo(NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName));
                            //폴더 로딩 준비
                            PrepareLoadFolders(null);
                            //await LoadWebDAVFoldersAsync(folderList, uri);
                            await LoadWebDAVFoldersAsync(rootList);
                            //인증 성공
                            IsConnecting = false;
                            IsDisconnected = false;
                            //경로 표시
                            DisplayCurrentPath = GetUri(false)?.ToString();
                            DialogHelper.CloseFlyout("ConnectServerFlyoutContent");
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("actual status code: 401"))
                    {
                        string tmp = string.Empty;
                        var enume = ex.Data.Keys.GetEnumerator();
                        while (enume.MoveNext())
                        {
                            var curr = enume.Current;
                            if (curr is string && curr as string == "RestrictedDescription")
                            {
                                if (ex.Data[curr] is string)
                                {
                                    tmp = (ex.Data[curr] as string)?.Trim();
                                }
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(tmp))
                            errMsg = tmp;
                        else
                            errMsg = ex.Message?.Trim();
                    }
                }
                if (!string.IsNullOrEmpty(errMsg))
                {
                    await DispatcherHelper.RunAsync(() =>
                    {
                        pathStack.Clear();
                        ErrorMessage = errMsg;
                        ShowErrorMessage = true;
                        IsConnecting = false;
                        IsDisconnected = true;
                        ShowOrderBy = false;
                        _Session = null;
                    });
                }
            });
        }

        private async void ConnectFtpServer()
        {
            //이미 연결중이면 수행하지 않음.
            if (IsConnecting) return;

            //현재 연결이 되어 있다면 연결 해제
            await DisconnectWebDAVServer(false);
            await DisconnectFtpServer(false);

            //입력된 데이터 트림
            TrimData();

            //데이터 체크
            if (!ValidateData())
                return;

            //연결중...
            IsConnecting = true;
            ShowErrorMessage = false;
            Uri uri = null;
            var resource = ResourceLoader.GetForCurrentView();
            string errMsg = resource.GetString("Message/Error/FailServerAuthentication"); //"인증에 실패하였습니다. 아이디/패스워드를 확인하세요. ";
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            //입력 접속 정보를 셋팅 데이터에 저장
            StoreFtpSettings();
            uri = GetUri();
            try
            {
                _FtpClient = new FtpClient
                {
                    HostName = new HostName(uri.Host),
                    Credentials = new NetworkCredential(Settings.Server.FtpUserName, Settings.Server.FtpPassword),
                    ServiceName = uri.Port.ToString()
                };
                await _FtpClient.ConnectAsync();

                if (_FtpClient.IsConnected)
                {
                    await _FtpClient.SetDataTypeAsync(FtpDataType.Binary);
                    errMsg = null;
                    //접속된 서버 타입
                    ConnectedServerType = ServerTypes.FTP;
                    //루트 로드
                    await LoadFtpFoldersAsync(null);
                    //인증 성공
                    IsConnecting = false;
                    IsDisconnected = false;
                    //경로 표시
                    DisplayCurrentPath = GetUri(false)?.ToString();
                    DialogHelper.CloseFlyout("ConnectServerFlyoutContent");
                }

            }
            catch (Exception ex)
            {
                errMsg = ex.Message?.Trim();
            }

            if (!string.IsNullOrEmpty(errMsg))
            {
                pathStack.Clear();
                ErrorMessage = errMsg;
                ShowErrorMessage = true;
                IsConnecting = false;
                IsDisconnected = true;
                ShowOrderBy = false;
                _Session = null;
            }
        }

        private bool IsDirectory(FtpItem item)
        {
            return item.Type == FtpFileSystemObjectType.Directory;
        }

        private bool IsDirectory(WebDavSessionItem item)
        {
            return item.IsFolder.GetValueOrDefault() || item.ContentType?.ToLower() == CONTENT_TYPE_DIRECTORY;
        }

        private bool IsDirectory(NetworkItemInfo item)
        {
            return !item.IsFile || item.ContentType?.ToLower() == CONTENT_TYPE_DIRECTORY;
        }

        private async void LoadWebDAVFolders(NetworkItemInfo item)
        {
            //세션 체크
            if (_Session == null) return;
            //폴더 로딩 준비
            PrepareLoadFolders(item);
            //폴더 및 파일 로딩 
            await LoadWebDAVFoldersAsync(null);
        }

        private async Task LoadFtpFoldersAsync(NetworkItemInfo item)
        {
            //FTP clinet체크
            if (_FtpClient == null) return;
            //폴더 로딩 준비
            PrepareLoadFolders(item);
            Uri uri = DecaTec.WebDav.Tools.UriHelper.AddTrailingSlash(item?.Uri ?? GetUri());

            //폴더 로딩 
            var folderGroupName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");
            var fileGroupName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            IEnumerable<NetworkItemInfo> items = null;
            try
            {
                await _FtpClient.SetWorkingDirectoryAsync(uri.LocalPath);
                var ftpItems = await _FtpClient.GetListingAsync(null, false);
                if (ftpItems != null)
                {
                    items = ftpItems.Select(x => x.ToNetworkItemInfo(uri, NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName));
                }

                //상위 버튼 및 하드웨어 백버튼 변경 처리
                ChangeToUpperAndBackButton();

                //지난 썸네일 캐시 삭제 및 현재 캐시 로드
                LoadThumbnailMetadata(uri.ToString());
                if (items != null)
                {
                    //폴더 및 썸네일 로드
                    var folderList = items.Where(x => IsDirectory(x));
                    await LoadFtpFolders(folderList, folderGroupName);

                    //파일 로딩
                    var fileList = items.Where(x => !IsDirectory(x));
                    LoadFtpFiles(fileList, fileGroupName);
                }
            }
            catch (FtpCommandException fcex)
            {
                System.Diagnostics.Debug.WriteLine($"Response code : {fcex.CompletionCode} => {fcex.Message}");
            }
            catch (Exception)
            {
                await DisconnectFtpServer(true);
            }
            finally
            {
                IsLoadingFolders = false;
                IsLoadingFiles = false;
            }
        }

        private void PrepareLoadFolders(NetworkItemInfo item)
        {
            CurrentFolderInfo = item;
            _CurrentSubtitleFileList = null;
            
            //모든 자식 요소 삭제
            NetworkItemGroupSource.Clear();
            //로딩 표시 
            IsLoadingFolders = true;
            IsLoadingFiles = true;
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

        private async Task LoadWebDAVFoldersAsync(IEnumerable<NetworkItemInfo> items)
        {
            var folderGroupName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");
            var fileGroupName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;

            await ThreadPool.RunAsync(async handler =>
            {
                try
                {
                    string path = string.Join("/", pathStack.Select(x => x.Name).Reverse());
                    var url = DecaTec.WebDav.Tools.UriHelper.AddTrailingSlash(GetUri(false, path));

                    if (items == null)
                    {
                        var webDavItems = await _Session.ListAsync(url);

                        if (webDavItems != null)
                        {
                            items = from item in webDavItems
                                    select item.ToNetworkItemInfo(NetworkItemTapped, NetworkItemRightTapped, NetworkItemHolding, isOrderByName);
                        }
                    }
                    //상위 버튼 및 하드웨어 백버튼 변경 처리
                    ChangeToUpperAndBackButton();

                    //지난 썸네일 캐시 삭제 및 현재 캐시 로드
                    LoadThumbnailMetadata(url.ToString());
                    //폴더 정보 및 폴더 썸네일 로드
                    var folderList = items.Where(x => IsDirectory(x));
                    await LoadBatchWebDAVFolderAsync(folderList, folderGroupName);
                                   
                    //파일정보 및 파일 썸네일 로드
                    var fileList = items.Where(x => !IsDirectory(x));
                    await LoadBatchWebDAVFilesAsync(fileList, fileGroupName);
                }
                catch (Exception)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(async () => await DisconnectWebDAVServer(true));
                }
                finally
                {
                    //폴더 로딩 표시기 정지
                    await DispatcherHelper.RunAsync(() => IsLoadingFolders = false);
                }
            });
        }

        private void LoadThumbnailMetadata(string path)
        {
            _ThumbnailListInCurrentFolder.Clear();
            //기간 지난 썸네일 캐시 삭제 
            ThumbnailDAO.DeletePastPeriodThumbnail(Settings.Thumbnail.RetentionPeriod);
            //썸네일 캐시 로드
            ThumbnailDAO.LoadThumnailInFolder(path, _ThumbnailListInCurrentFolder);
        }
        
        private async Task LoadBatchWebDAVFolderAsync(IEnumerable<NetworkItemInfo> folderList, string folderGroupName)
        {
            if ((bool)folderList?.Any())
            {
                //화면 조건에 따른 정렬
                Sort(ref folderList);

                int itemCount = folderList.Count();
                List<NetworkItemInfo> addedList = new List<NetworkItemInfo>();
                int offset = 0;
                int patchSize = 5;

                //신규 그룹 생성
                NetworkItemGroup group = new NetworkItemGroup(StorageItemTypes.Folder, folderGroupName);
                await DispatcherHelper.RunAsync(() => NetworkItemGroupSource.Add(group)); ;

                foreach (var folderItem in folderList)
                {
                    //일괄 작업을 위해 리스트에 추가
                    addedList.Add(folderItem);
                    folderItem.Group = group;

                    //리스트가 일정수량에 도달하거나, 마지막까지 도달한 경우
                    if (++offset % patchSize == 0 || offset >= itemCount)
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            foreach (var added in addedList)
                            {
                                added.Group.Items.Add(added);
                                added.Group = null;
                            }
                            addedList.Clear();
                        });
                    }
                }

                //폴더 썸네일 로드
                LoadWebDAVFoldersThumbnail();
            }
            await DispatcherHelper.RunAsync(() => IsLoadingFolders = false);
        }

        private async Task LoadFtpFolders(IEnumerable<NetworkItemInfo> folderList, string folderGroupName)
        {
            if ((bool)folderList?.Any())
            {
                //화면 조건에 따른 정렬
                Sort(ref folderList);

                //신규 그룹 생성
                NetworkItemGroup group = group = new NetworkItemGroup(StorageItemTypes.Folder, folderGroupName);
                NetworkItemGroupSource.Add(group);

                IEnumerable<NetworkItemInfo> list = null;
                foreach (var folderItem in folderList)
                {
                    try
                    {
                        if (Settings.Thumbnail.UseUnsupportedFTPFolder)
                        {
                            await _FtpClient.SetWorkingDirectoryAsync(folderItem.Uri.LocalPath);
                            var ftpItems = await _FtpClient.GetListingAsync(null, false);
                            if (ftpItems != null)
                            {
                                list = ftpItems.Select(x => x.ToNetworkItemInfo(folderItem.Uri));
                                //폴더의 하위 비디오갯수 설정
                                SetFolderProperties(folderItem, list);
                                //폴더의 썸네일 설정
                                SetFolderThumbnail(folderItem, list, Settings.Thumbnail.UseUnsupportedFTPFolder, _FtpClient.CodePage);
                            }
                        }
                        else
                        {
                            folderItem.FileCount = -1;
                            folderItem.FileCountDescription = "?";
                        }
                    }
                    catch(FtpCommandException fcex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Response code : {fcex.CompletionCode} => {fcex.Message}");
                    }
                    group.Items.Add(folderItem);
                }
            }
        }

        private async Task LoadBatchWebDAVFilesAsync(IEnumerable<NetworkItemInfo> fileList, string fileGroupName)
        {
            if (fileList != null)
            {
                var videoList = fileList.Where(x => MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)));
                //자막 리스트 생성
                _CurrentSubtitleFileList = fileList.Where(x => MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)))
                                                   .Select(x => x.Uri.AbsoluteUri).ToList();

                //화면 조건에 따른 정렬
                Sort(ref videoList);

                int itemCount = videoList.Count();
                List<NetworkItemInfo> addedList = new List<NetworkItemInfo>();
                int offset = 0;
                int patchSize = 5;

                NetworkItemGroup group = null;
                var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
                var isMultiGroup = isOrderByName && itemCount > GROUP_MAX_ITME_COUNT;
                string groupName = isMultiGroup ? null : fileGroupName;

                foreach (var videoItem in videoList)
                {
                    if (isMultiGroup)
                    {
                        groupName = _CharacterGroupings.Lookup(videoItem.Name);
                    }
                    //그룹이 변경되는 경우
                    if (group == null || group.Name != groupName)
                    {
                        group = new NetworkItemGroup(StorageItemTypes.File, groupName);
                        //신규 그룹 생성
                        await DispatcherHelper.RunAsync(() => NetworkItemGroupSource.Add(group));
                    }

                    //일괄 작업을 위해 리스트에 추가
                    addedList.Add(videoItem);
                    videoItem.Group = group;

                    //리스트가 일정수량에 도달하거나, 마지막까지 도달한 경우
                    if (++offset % patchSize == 0 || offset >= itemCount)
                    {
                        await DispatcherHelper.RunAsync(() =>
                        {
                            foreach (var added in addedList)
                            {
                                added.Group.Items.Add(added);
                                added.Group = null;
                            }
                            addedList.Clear();
                        });
                    }
                }
                LoadFileThumbnail(Settings.Thumbnail.UseUnsupportedWebDAVFile, -1);
            }
            await DispatcherHelper.RunAsync(() => IsLoadingFiles = false);
        }
    
        private void LoadFtpFiles(IEnumerable<NetworkItemInfo> fileList, string fileGroupName)
        {
            if (fileList != null)
            {
                var videoList = fileList.Where(x => MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)));
                //자막 리스트 생성
                _CurrentSubtitleFileList = fileList.Where(x => MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)))
                                                   .Select(x => x.Uri.AbsoluteUri).ToList();

                //화면 조건에 따른 정렬
                Sort(ref videoList);

                int itemCount = videoList.Count();
                NetworkItemGroup group = null;
                var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
                var isMultiGroup = isOrderByName && itemCount > GROUP_MAX_ITME_COUNT;
                string groupName = isMultiGroup ? null : fileGroupName;

                foreach (var videoItem in videoList)
                {
                    if (isMultiGroup)
                    {
                        groupName = _CharacterGroupings.Lookup(videoItem.Name);
                    }
                    //그룹이 변경되는 경우
                    if (group == null || group.Name != groupName)
                    {
                        group = new NetworkItemGroup(StorageItemTypes.File, groupName);
                        //신규 그룹 생성
                        NetworkItemGroupSource.Add(group);
                    }
                    //그룹에 파일 추가
                    group.Items.Add(videoItem);
                }
                //파일 썸네일 로드
                LoadFileThumbnail(Settings.Thumbnail.UseUnsupportedFTPFile, _FtpClient.CodePage);
            }
        }

        #endregion

        private void Sort(ref IEnumerable<NetworkItemInfo> networkItemList)
        {
            switch (_Sort)
            {
                case SortType.CreatedDateDescending:
                    networkItemList = networkItemList.OrderByDescending(x => x.Modified);
                    break;
                case SortType.CreatedDate:
                    networkItemList = networkItemList.OrderBy(x => x.Modified);
                    break;
                case SortType.NameDescending:
                    networkItemList = networkItemList.OrderByDescending(x => x.Name);
                    break;
                default:
                    networkItemList = networkItemList.OrderBy(x => x.Name);
                    break;
            }
        }
        
        private void LoadWebDAVFoldersThumbnail()
        {
            Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items), async (networkItem) =>
                {
                    var webDavItems = await _Session.ListAsync(networkItem.Uri);
                    if (webDavItems != null)
                    {
                        IEnumerable<NetworkItemInfo> list = webDavItems.Select(x => x.ToNetworkItemInfo());
                        //하위 비디오갯수 설정
                        SetFolderProperties(networkItem, list);
                        //폴더의 썸네일
                        SetFolderThumbnail(networkItem, list, Settings.Thumbnail.UseUnsupportedWebDAVFolder, -1);
                    }
                });
            }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
            {
                System.Diagnostics.Debug.WriteLine("WebDAV 폴더 썸네일 로드 패러럴 종료");
            });
        }

        private void LoadFileThumbnail(bool loadUnsupportedThumbnail, int codePage)
        {
            Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items), networkItem =>
                {
                    //썸네일 로드
                    this.LoadThumbnailAsync(networkItem, _ThumbnailListInCurrentFolder, loadUnsupportedThumbnail, codePage);
                    //자막 목록 설정
                    if (_CurrentSubtitleFileList != null && _CurrentSubtitleFileList.Any())
                    {
                        networkItem.SubtitleList = _CurrentSubtitleFileList.Where(x => System.Net.WebUtility.UrlDecode(x).ToUpper().Contains(System.IO.Path.GetFileNameWithoutExtension(networkItem.Name).ToUpper())).ToList();
                    }
                });
            }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
            {
                System.Diagnostics.Debug.WriteLine("파일 썸네일 로드 패러럴 종료");
            });
        }

        private void SetFolderThumbnail(NetworkItemInfo networkItem, IEnumerable<NetworkItemInfo> list, bool loadUnsupportedThumbnail, int codePage)
        {
            if (list != null && list.Any())
            {
                var videoList = list.Where(x => !IsDirectory(x) && MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)));
                if (videoList.Count() > 0)
                {
                    List<ImageSource> imageSourceList = new List<ImageSource>();
                    List<Thumbnail> thumbnailList = new List<Thumbnail>();
                    ThumbnailDAO.LoadThumnailInFolder(DecaTec.WebDav.Tools.UriHelper.AddTrailingSlash(networkItem.Uri.ToString()), thumbnailList);

                    Task.Factory.StartNew(async () =>
                    {
                        if (networkItem.FileCount >= 4)
                        {
                            Parallel.ForEach(videoList, async (video, state) =>
                            {
                                if (!state.IsStopped)
                                {
                                    var imgSrc = await GetThumbnailAsync(video, thumbnailList, loadUnsupportedThumbnail, codePage);
                                    if (!state.IsStopped)
                                    {
                                        if (imageSourceList.Count >= 4)
                                        {
                                            networkItem.ImageItemsSource = imageSourceList;
                                            System.Diagnostics.Debug.WriteLine($"{video.Name} : 종료 뿅.");
                                            state.Stop();
                                        }
                                        else if (imgSrc != null && !state.IsStopped)
                                        {
                                            imageSourceList.Add(imgSrc);
                                            System.Diagnostics.Debug.WriteLine($"{video.Name} : 추가 뿅.");
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            networkItem.ImageItemsSource = await GetThumbnailAsync(videoList.First(), thumbnailList, loadUnsupportedThumbnail, codePage);
                        }
                    }).AsAsyncAction().Completed = new AsyncActionCompletedHandler((act, st) =>
                    {
                        System.Diagnostics.Debug.WriteLine("폴더 이미지 로드 패러럴 종료");
                    });
                }
            }
        }

        private void SetFolderProperties(NetworkItemInfo networkItem, IEnumerable<NetworkItemInfo> list)
        {
            if (list != null)
            {
                //비디오 파일 갯수를 알아내기 위한 필터링 옵션
                var videoList = list.Where(x => !IsDirectory(x) && MediaFileSuffixes.VIDEO_SUFFIX.Any(y => x.Name.ToUpper().EndsWith(y)));
                networkItem.FileCount = videoList.Count();
                var folderCount = list.Count(x => IsDirectory(x));

                if (networkItem.FileCount > 0)
                    networkItem.FileCountDescription = networkItem.FileCount.ToString() + (folderCount > 0 ? "+" : string.Empty);
                else
                    networkItem.FileCountDescription = "0" + (folderCount > 0 ? "*" : string.Empty);
            }
        }

        private void ShowMediaInfoFlyout(NetworkItemInfo item)
        {
            if (item == null || !item.IsFile) return;

            string usr = string.Empty;
            string pwd= string.Empty;
            int cp = -1;

            switch(ConnectedServerType)
            {
                case ServerTypes.WebDAV:
                    usr = Settings.Server.WebDAVUserName;
                    pwd = Settings.Server.WebDAVPassword;
                    break;
                case ServerTypes.FTP:
                    usr = Settings.Server.FtpUserName;
                    pwd = Settings.Server.FtpPassword;
                    cp = _FtpClient.CodePage;
                    break;
            }

            MessengerInstance.Send(new Message("IsOpen", true)
                                    .Add("LoadingTitle", ResourceLoader.GetForCurrentView().GetString("Loading/MediaInformation/Text")),
                                    "ShowLoadingPanel");

            MessengerInstance.Send<Message<DecoderTypes>>(
                 new Message<DecoderTypes>((decoderType) =>
                 {
                     RequestPlayback(decoderType, item, false);
                 })
                 .Add("NetworkItemInfo", item)
                 .Add("CodePage", cp)
                 .Add("UserName", usr)
                 .Add("Password", pwd)
                 .Add("ButtonName", "CodecInformation"),
                 "ShowMediaFileInformation");
        }

        bool isLockingPlay = VersionHelper.IsAdvertisingVersion;

        private void RequestPlayback(DecoderTypes decoderType, NetworkItemInfo networkItemInfo, bool isPrevOrNext)
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

            var items = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).ToList();
            var item = networkItemInfo;
            var index = items.IndexOf(item);
            
            NetworkItemInfo prevItemInfo = null;
            NetworkItemInfo currItemInfo = item;
            NetworkItemInfo nextItemInfo = null;

            string usr = string.Empty;
            string pwd = string.Empty;
            int cp = -1;

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

            switch (ConnectedServerType)
            {
                case ServerTypes.WebDAV:
                    usr = Settings.Server.WebDAVUserName;
                    pwd = Settings.Server.WebDAVPassword;
                    break;
                case ServerTypes.FTP:
                    usr = Settings.Server.FtpUserName;
                    pwd = Settings.Server.FtpPassword;
                    cp = _FtpClient.CodePage;
                    break;
            }

            MessengerInstance.Send(
                new Message()
                .Add("PrevPlayListFile", prevItemInfo)
                .Add("CurrPlayListFile", currItemInfo)
                .Add("NextPlayListFile", nextItemInfo)
                .Add("DecoderType", decoderType)
                .Add("CodePage", cp)
                .Add("VideoUserName", usr)
                .Add("VideoPassword", pwd),
                "RequestPlayback");
        }

        private async void NetworkItemTapped(object sender, TappedRoutedEventArgs args)
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
                        case ServerTypes.WebDAV:
                            LoadWebDAVFolders(nii);
                            break;
                        case ServerTypes.FTP:
                            await LoadFtpFoldersAsync(nii);
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
                case ServerTypes.WebDAV:
                    await DisconnectWebDAVServer(false);
                    break;
                case ServerTypes.FTP:
                    await DisconnectFtpServer(false);
                    break;
            };
        }

        private async Task DisconnectWebDAVServer(bool showMessage)
        {
            if (_Session != null)
            {
                _Session.Dispose();
                _Session = null;
            }

            await DisconnectServer(showMessage);
        }

        private async Task DisconnectFtpServer(bool showMessage)
        {
            string msg = null;
            if (_FtpClient != null)
            {
                if (_FtpClient.IsConnected)
                {
                    try
                    {
                        await _FtpClient.DisconnectAsync();
                    }
                    catch (Exception e)
                    {
                        string[] msgs = e.Message?.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        if (msgs?.Count() > 0)
                            msg = msgs[0];
                    }
                }
                _FtpClient = null;
            }

            await DisconnectServer(showMessage, msg);
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
            CurrentFolderInfo = null;
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
                case ServerTypes.WebDAV:
                    ConnectWebDAVServer();
                    break;
                case ServerTypes.FTP:
                    ConnectFtpServer();
                    break;
                case ServerTypes.Direct:
                    PlayDirectVideo();
                    break;
            }
        }

        private void PlayDirectVideo()
        {
            Uri videoUri = null;
            Uri ccUri = null;
            ResourceLoader resource = ResourceLoader.GetForCurrentView();

            ServerDirectVideoAddress = ServerDirectVideoAddress?.Trim();
            ServerDirectSubtitleAddress = ServerDirectSubtitleAddress?.Trim();

            //입력 체크
            if (string.IsNullOrEmpty(ServerDirectVideoAddress))
            {
                ShowErrorMessage = true;
                ErrorMessage = resource.GetString("Message/Error/EmptyVideoUrl");
                return;
            }
            else
            {
                string vidUsr = string.Empty;
                string vidPwd = string.Empty;
                string subUsr = string.Empty;
                string subPwd = string.Empty;

                //url 체크
                try { videoUri = new Uri(ServerDirectVideoAddress); }
                catch(Exception e) 
                {
                    ShowErrorMessage = true;
                    ErrorMessage = $"Video url : {e.Message}";
                    return;
                }
                //확장자 체크
                var ext = Path.GetExtension(ServerDirectVideoAddress);
                if (!CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX.Contains(ext.ToUpper()))
                {
                    ShowErrorMessage = true;
                    ErrorMessage = $"Video url : {resource.GetString("Message/Error/UnsupportedExtension")}";
                    return;
                }
                //자막 계정 파싱(MF에서 계정이 있으면 오류)
                if (!string.IsNullOrEmpty(videoUri.UserInfo))
                {
                    var auth = videoUri.UserInfo.Split(':');
                    if (auth.Length > 0)
                        vidUsr = auth[0];
                    if (auth.Length == 2)
                        vidPwd = auth[1];
                    videoUri = new Uri($"{videoUri.Scheme}://{videoUri.Host}:{videoUri.Port}{videoUri.AbsolutePath}");
                }

                //비디오 url 추가
                NetworkItemInfo videoFile = new NetworkItemInfo { ServerType = ServerTypes.Direct, Uri = videoUri };

                Message msg = new Message()
                    .Add("PrevPlayListFile", null)
                    .Add("CurrPlayListFile", videoFile)
                    .Add("NextPlayListFile", null)
                    .Add("DecoderType", Settings.Playback.DefaultDecoderType);

                if (!string.IsNullOrEmpty(vidUsr))
                    msg.Add("VideoUserName", vidUsr);
                if (!string.IsNullOrEmpty(vidPwd))
                    msg.Add("VideoPassword", vidPwd);

                //자막이 입력되었으면 체크
                if (!string.IsNullOrEmpty(ServerDirectSubtitleAddress))
                {
                    //url 체크
                    try { ccUri = new Uri(ServerDirectSubtitleAddress); }
                    catch (Exception e)
                    {
                        ShowErrorMessage = true;
                        ErrorMessage = $"Subtitle url : {e.Message}";
                        return;
                    }
                    //확장자 체크
                    ext = Path.GetExtension(ServerDirectSubtitleAddress);
                    if (!CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(ext.ToUpper()))
                    {
                        ShowErrorMessage = true;
                        ErrorMessage = $"Subtitle url : {resource.GetString("Message/Error/UnsupportedExtension")}";
                        return;
                    }
                    //자막 계정 파싱
                    if (!string.IsNullOrEmpty(ccUri.UserInfo))
                    {
                        var auth = ccUri.UserInfo.Split(':');
                        if (auth.Length > 0)
                            subUsr = auth[0];
                        if (auth.Length == 2)
                            subPwd = auth[1];
                    }

                    if (!string.IsNullOrEmpty(subUsr))
                        msg.Add("SubtitleUserName", subUsr);
                    if (!string.IsNullOrEmpty(subPwd))
                        msg.Add("SubtitlePassword", subPwd);
                    //자막 추가
                    videoFile.SubtitleList = new List<string>();
                    videoFile.SubtitleList.Add($"{ccUri.Scheme}://{ccUri.Host}:{ccUri.Port}{ccUri.AbsolutePath}");

                }
                //재생 요청
                MessengerInstance.Send(msg, "RequestPlayback");
                //연결창 닫기
                DialogHelper.CloseFlyout("ConnectServerFlyoutContent");
            }
        }

        public async void ToUpperTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator || !VisibleToUpper)
                return;

            if (pathStack.Count > 0)
                pathStack.Pop();

            NetworkItemInfo wdii = new NetworkItemInfo()
            {
                Uri = new Uri(CurrentFolderInfo.ParentFolderPath)
            };
            switch (ServerTypes)
            {
                case ServerTypes.WebDAV:
                    LoadWebDAVFolders(wdii);
                    break;
                case ServerTypes.FTP:
                    await LoadFtpFoldersAsync(wdii);
                    break;
            }
        }

        public async void RefreshTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsStopLoadingIndicator)
                return;

            switch (ServerTypes)
            {
                case ServerTypes.WebDAV:
                    LoadWebDAVFolders(CurrentFolderInfo);
                    break;
                case ServerTypes.FTP:
                    await LoadFtpFoldersAsync(CurrentFolderInfo);
                    break;
            }

        }
                
        public void GridViewLoaded(object sender, RoutedEventArgs e)
        {
            var gridView = sender as GridView;
            if (gridView != null)
            {
                var page = ElementHelper.FindVisualParent<Views.NetworkPage>(gridView);
                if (page != null)
                {
                    Resources = page.Resources;
                    //초기값 로드
                    ChangeStyleSelector("NetworkItemStyleSelector", Windows.UI.Xaml.Window.Current.Bounds.Width);
                }
            }

            //로딩된 리스트가 존재하는 경우, 설정에서 back버튼이 활성화 되어 있으면 상위 폴더가 존재하는 경우 버튼을 활성화 시킴
            if (Settings.General.UseHardwareBackButtonWithinVideo
                //&& (NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items.Cast<NetworkItemInfo>()).Any(x => x.Uri != null)
                // || NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).Any(x => x.Uri.ToString() != x.ParentFolderPath)))
                && pathStack.Any())
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
        }

        public void ServerTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //에러 표시 초기화
            ShowErrorMessage = false;
            ErrorMessage = string.Empty;

            var resouce = ResourceLoader.GetForCurrentView();
            switch(ServerTypes)
            {
                case ServerTypes.WebDAV:
                    ServerHost = Settings.Server.WebDAVHost;
                    ServerPath = Settings.Server.WebDAVPath;
                    ServerPort = Settings.Server.WebDAVPort;
                    ServerUserName = Settings.Server.WebDAVUserName;
                    ServerPassword = Settings.Server.WebDAVPassword;
                    IsSSL = Settings.Server.WebDAVSSL;
                    break;
                case ServerTypes.FTP:
                    ServerHost = Settings.Server.FtpHost;
                    ServerPort = Settings.Server.FtpPort;
                    ServerUserName = Settings.Server.FtpUserName;
                    ServerPassword = Settings.Server.FtpPassword;
                    break;
            }
        }
    }
}
