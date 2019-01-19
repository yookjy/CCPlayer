using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Views;
using CCPlayer.UWP.Views.Controls;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using Lime.Xaml.Helpers;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using CUXC = CCPlayer.UWP.Xaml.Controls;

namespace CCPlayer.UWP.ViewModels
{
    public class MainViewModel : CCPViewModelBase
    {
        #region private memebers
        private bool _IsFirstPlay;
        private bool _IsLockingByTrialExpried;
        private MediaElementState _BeforeHideMediaElementState;
        private int _SelectedPlayListIndex;
        private int _SelectedMainMenuIndex;
        private string _DragCaption;
        private Loading _Loading;
        private PaidLevelType _PaidLevel;
        private SimpleOrientationSensor _OrientationSensor;
        private Popup _AppProtectionPopup;
        private Popup _LoadingPopup;
        private IMediaItemInfo _ReqPlayListFile;
        private CUXC.MediaElement _MediaElement;
        private DisplayOrientations _PrevDisplayInformation;
        private List<KeyValuePair<string, byte[]>> _SaveFontList;
        private QueryOptions _VideoSubtitleFileQueryOptions;
        private QueryOptions _SubtitleFileQueryOptions;

        #endregion

        #region properties
        public Settings Settings => _Settings;
        public bool IsTrial => Settings.General.PaidLevel == PaidLevelType.Trial;
        public bool IsMobile => App.IsMobile;

        public Frame SplitViewContentFrame { get; set; }

        [DoNotNotify]
        public PaidLevelType PaidLevel
        {
            get { return _PaidLevel; }
            set
            {
                if (Set(ref _PaidLevel, value))
                {
                    Settings.General.PaidLevel = value;
                    RaisePropertyChanged(nameof(IsTrial));
                }
            }
        }

        public bool IsMenuOpen { get; set; }

        public string LoadingText { get; set; }

        [DoNotNotify]
        public int SelectedPlayListIndex
        {
            get { return _SelectedPlayListIndex; }
            set
            {
                if (Set(ref _SelectedPlayListIndex, value))
                {
                    if (value > -1)
                    {
                        var mi = PlayListSource[value];
                        //해당 재생 목록으로 이동
                        MenuAction(mi);
                        //메뉴 패널 닫기
                        CloseMenuPanel(SplitViewContentFrame);
                    }

                    if (_SelectedMainMenuIndex != -1)
                    {
                        _SelectedMainMenuIndex = -1;
                        RaisePropertyChanged("SelectedMainMenuIndex");
                    }
                }
            }
        }

        [DoNotNotify]
        public int SelectedMainMenuIndex
        {
            get { return _SelectedMainMenuIndex; }
            set
            {
                if (Set(ref _SelectedMainMenuIndex, value))
                {
                    if (value > -1)
                    {
                        var mi = MainMenuItemSource[value];
                        //해당 메인 메뉴로 이동
                        MenuAction(mi);
                        //메뉴 패널 닫기
                        CloseMenuPanel(SplitViewContentFrame);
                    }

                    if (_SelectedPlayListIndex != -1)
                    {
                        _SelectedPlayListIndex = -1;
                        RaisePropertyChanged("SelectedPlayListIndex");
                    }
                }
            }
        }

        public FlyoutModelBase<PlayList> NewPlayList { get; set; }

        [DoNotNotify]
        public ICommand MainMenuButtonTappedCommand { get; private set; }

        [DoNotNotify]
        public ObservableCollection<MenuItem> MainMenuItemSource { get; set; }

        [DoNotNotify]
        public ObservableCollection<PlayList> PlayListSource { get; set; }

        [DoNotNotify]
        public AppProtection AppProtection { get; set; }

        public IMediaItemInfo PrevPlayListFile { get; set; }

        public IMediaItemInfo CurrPlayListFile { get; set; }

        public IMediaItemInfo NextPlayListFile { get; set; }

        public string MediaElementVisibleState { get; set; }

        public string ReadyExitAppVisibleState { get; set; }
        
        public double MediaElementSlideDistance { get; set; }

        public double MediaElementSlideDistance2 { get; set; }

        public bool IsCCSettingsOpen { get; set; }

        public double CCSettingsHeight { get; set; }

        public double CCSettingsHorizontalOffset { get; set; }

        public ObservableCollection<CUXC.KeyName> FontSource
        {
            get
            {
                var ssvm = SimpleIoc.Default.GetInstance<SubtitleSettingViewModel>();
                ssvm.LoadFontList();
                return ssvm.FontSource;
            }
        }
        #endregion

        #region public members
        public TappedEventHandler NewPlayListTappedEventHandler;
        public TappedEventHandler AppLockTappedEventHandler;
        public TappedEventHandler SettingsTappedEventHandler;
        #endregion

        #region dependency injections
        [DependencyInjection]
        private FontDAO _FontDAO;

        [DependencyInjection]
        private SettingDAO _SettingDAO;

        [DependencyInjection]
        private Settings _Settings;

        [DependencyInjection]
        private PlayListDAO _PlayListDAO;

        #endregion

        #region implement abstract method
        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
            _SettingDAO = null;
            _PlayListDAO = null;
            _FontDAO = null;
        }
        
        protected override void CreateModel()
        {
            MainMenuItemSource = new ObservableCollection<MenuItem>();
            PlayListSource = new ObservableCollection<PlayList>();
            SplitViewContentFrame = new Frame();
            _SaveFontList = new List<KeyValuePair<string, byte[]>>();

            AppProtection = new AppProtection();
            AppProtection.IsHideAppLockPanel = true;
            AppProtection.LoginSucceed += AppProtection_LoginSucceed;

            if (App.IsMobile)
            {
                AppProtection.Margin = new Thickness(0, -48, 0, 0);
            }

            var resource = ResourceLoader.GetForCurrentView();
            _Loading = new Loading();
            _Loading.DataContext = this;
            LoadingText = resource.GetString("Loading/Playback/Text");
            //비디오 파일 쿼리 옵션 저장
            List<string> suffixes = new List<string>();
            suffixes.AddRange(CUXC.MediaFileSuffixes.VIDEO_SUFFIX);
            suffixes.AddRange(CUXC.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);
            _VideoSubtitleFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, suffixes);
            //자막파일 쿼리 옵션 저장
            _SubtitleFileQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, CUXC.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX);

            _DragCaption = resource.GetString("Button/Play/Content");
        }
        
        protected override void RegisterMessage()
        {
            MessengerInstance.Register<bool>(this, "MainMenuButton", (val) => IsMenuOpen = !IsMenuOpen);

            MessengerInstance.Register<KeyValuePair<string, PlayList>>(this, "PlayListChanged", PlayListChangled);

            MessengerInstance.Register<Message>(this, "MoveToPlayListMenu", MoveToPlayListMenu);
            //설정 메뉴 탭 이벤트
            MessengerInstance.Register<MenuItem>(this, "TappedSettingMenuItem", MenuAction);
            //재생 목록 탭 이벤트
            MessengerInstance.Register<Message<PlayList>>(this, "NewPlayListTapped", NewPlayListTapped);
            //로딩 패널 오픈
            MessengerInstance.Register<Message>(this, "ShowLoadingPanel", ShowLoadingPanel);
            //파일 연결
            MessengerInstance.Register<Message>(this, "FileAssociation", FileAssociation);
            MessengerInstance.Register<Message>(this, "RequestPlayback", RequestPlayback);
            //재생/일시정지(트라이얼의 경우 파일연결이나 Drag&Drop에서 사용)
            MessengerInstance.Register<bool>(this, "LockByTrialExpired", (val) => _IsLockingByTrialExpried = val);
        }
        
        protected override void RegisterEventHandler()
        {
            _OrientationSensor = SimpleOrientationSensor.GetDefault();
            if (_OrientationSensor != null)
            {
                _OrientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
            }

            NewPlayListTappedEventHandler = NewPlayListTapped;
            AppLockTappedEventHandler = AppLockTapped;
            SettingsTappedEventHandler = SettingsTapped;

            MainMenuButtonTappedCommand = new RelayCommand<SplitView>(MainMenuButtonTappedCommandExecute);

            if (App.IsMobile)
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            }
            else
            {
                SystemNavigationManager.GetForCurrentView().BackRequested += Navigation_BackRequested;
            }

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                //타이틀바 버튼 배경색 제거
                Settings.General.ChangeTitleBarColor();              

                App.Current.UnhandledException += Current_UnhandledException;
                Window.Current.VisibilityChanged += Current_VisibilityChanged;
                Window.Current.Activated += Current_Activated;
                Window.Current.SizeChanged += Current_SizeChanged;
            });

            Frame frame = Window.Current.Content as Frame;
            frame.Navigated += Frame_Navigated;
        }
        protected override void InitializeViewModel()
        {
            var resource = ResourceLoader.GetForCurrentView();

            //트라이얼 값셋팅
            PaidLevel = Settings.General.PaidLevel;

            //메인메뉴 데이터 생성
            MainMenuItemSource.Add(new MenuItem() { Type = MenuType.Explorer, Name = resource.GetString("Explorer/Title/Text"), Glyph = "\xE714" });//folder add E1DA
            MainMenuItemSource.Add(new MenuItem() { Type = MenuType.Network, Name = $"{resource.GetString("AddFolder/WebDAV/Text")}/{resource.GetString("AddFolder/FTP/Text")}", Glyph = "\xEC27" });
            MainMenuItemSource.Add(new MenuItem() { Type = MenuType.Cloud, Name = resource.GetString("Cloud/OneDrive/Text"), Glyph = "\xE753" });
            MainMenuItemSource.Add(new PlayList() { Type = MenuType.NowPlaying, Seq = 1, Name = resource.GetString("PlayList/NowPlaying/Text"), Glyph = "\xE768" });

            if (App.IsXbox)
            {
                //Xbox에서 알려진 버그로 전체 화면을 쓸수 있게 해주는 코드임
                //https://msdn.microsoft.com/ko-kr/windows/uwp/xbox-apps/known-issues
                var applicationView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
                applicationView.SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
            }

            var window = Windows.UI.Xaml.Window.Current;
            var width = window.CoreWindow.Bounds.Width;

            if (width > 720)
            {
                IsMenuOpen = true;
            }

            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Windows.Foundation.Size(320, 240)); //4:3
            //Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Windows.Foundation.Size(432, 512));

            SelectedMainMenuIndex = -1;
            SelectedPlayListIndex = -1;

            NewPlayList = new FlyoutModelBase<PlayList>
            {
                PrimaryButtonText = resource.GetString("Button/Save/Content"),
                PrimaryTitle = resource.GetString("PlayList/New/Title"),
                SecondaryContent = resource.GetString("TopMenu/Explorer/NewPlayList/Text"),
                PrimaryButtonCommand = new RelayCommand<TextBox>(SavePlayListTappedCommandExecute)
            };

            var deleteFontList = _FontDAO.GetTempFontList();
            List<string> deletedFontList = new List<string>();

            foreach (var path in deleteFontList)
            {
                try
                {
                    File.Delete(path);
                    deletedFontList.Add(path);
                    System.Diagnostics.Debug.WriteLine($"{path} : 폰트 삭제 OK!");
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine($"{path} : 폰트 삭제 FAILED!");
                }
            }
            //성공한 폰트를 DB에서 삭제
            _FontDAO.DeleteTempFont(deletedFontList);

            MediaElementVisibleState = "None";
            ReadyExitAppVisibleState = "None";
            MediaElementSlideDistance = Window.Current.Bounds.Height;

            //폰트 로드
            FontHelper.LoadAllFont(() =>
            {
                System.Diagnostics.Debug.WriteLine("폰트 로딩 완료.");
            });
        }
        #endregion

        #region message callback handler
        private void PlayListChangled(KeyValuePair<string, PlayList> kv)
        {
            if (kv.Key == "removed")
            {
                //이전 재생목록 검색
                var nextPlayList = PlayListSource.LastOrDefault(x => string.Compare(x.Name, kv.Value.Name) < 0);

                //이전 항목이 없으면 다음 재생목록 검색
                if (nextPlayList == null)
                {
                    nextPlayList = PlayListSource.FirstOrDefault(x => string.Compare(kv.Value.Name, x.Name) < 0);
                }
                //목록 초기화
                PlayListSource.Clear();
                _PlayListDAO.LoadPlayList(PlayListSource, null);
                //재검색 하여 선택시킴
                var newIdx = PlayListSource.IndexOf(PlayListSource.FirstOrDefault(x => x.Seq == nextPlayList.Seq));
                if (newIdx != -1)
                {
                    SelectedPlayListIndex = newIdx;
                }
                else
                {
                    //없는 경우 탐색기
                    SelectedMainMenuIndex = 0;
                }
            }
            else if (kv.Key == "updated")
            {
                PlayListSource[SelectedPlayListIndex].Name = kv.Value.Name;
            }
            else if (kv.Key == "added")
            {
                var last = PlayListSource.LastOrDefault(x => string.Compare(x.Name, kv.Value.Name) < 0);
                var index = PlayListSource.IndexOf(last);
                PlayListSource.Insert(index + 1, kv.Value);
            }
        }
        private void MoveToPlayListMenu(Message message)
        {
            var playList = message.GetValue<PlayList>("PlayList");
            if (playList.Seq == 1)
            {
                //최초인 경우 ViewModel을 미리 로드해야 "PrepareLoadPlayListFile" 메세지를 보낼 수 있음
                SimpleIoc.Default.GetInstance<PlayListViewModel>();
                MessengerInstance.Send(message, "PrepareLoadPlayListFile");
                //지금 재생 중
                SelectedMainMenuIndex = MainMenuItemSource.Count - 1; //맨 마지막 메인메뉴가 지금 재생 중.
            }
            else
            {
                //재생 목록
                SelectedPlayListIndex = PlayListSource.IndexOf(PlayListSource.First(x => x.Seq == playList.Seq));
            }
        }
        private void MenuAction(MenuItem mi)
        {
            if (mi != null)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                MessengerInstance.Send(true, "ResetSelectionMode");
                switch (mi.Type)
                {
                    case MenuType.Explorer:
                        if (SplitViewContentFrame.Navigate(typeof(Views.ExplorerPage)))
                        {
                            SplitViewContentFrame.BackStack.Clear();
                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                    case MenuType.DLNA:
                        if (SplitViewContentFrame.Navigate(typeof(Views.DLNAPage)))
                        {
                            SplitViewContentFrame.BackStack.Clear();
                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                    case MenuType.Network:
                        if (SplitViewContentFrame.Navigate(typeof(Views.NetworkPage)))
                        {
                            SplitViewContentFrame.BackStack.Clear();
                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                    case MenuType.Cloud:
                        if (SplitViewContentFrame.Navigate(typeof(Views.CloudPage)))
                        {
                            SplitViewContentFrame.BackStack.Clear();
                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                    case MenuType.NowPlaying:
                    case MenuType.Playlist:
                        {
                            var playlist = mi as PlayList;
                            var content = SplitViewContentFrame.Content;

                            if (content == null || content.GetType() != typeof(Views.PlayListPage))
                            {
                                if (SplitViewContentFrame.Navigate(typeof(Views.PlayListPage), playlist))
                                {
                                    SplitViewContentFrame.BackStack.Clear();
                                }
                            }
                            else
                            {
                                MessengerInstance.Send(playlist, "ChangePlayList");
                            }

                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = playlist.Seq;
                            break;
                        }
                    case MenuType.Settings:
                        SelectedMainMenuIndex = -1;
                        SelectedPlayListIndex = -1;
                        if (SplitViewContentFrame.Navigate(typeof(Views.SettingsMenuPage), mi))
                        {
                            SplitViewContentFrame.BackStack.Clear();
                            Settings.General.LastMenuType = mi.Type;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                    case MenuType.GeneralSetting:
                    case MenuType.PrivacySetting:
                    case MenuType.PlaybackSetting:
                    case MenuType.SubtitleSetting:
                    case MenuType.FontSetting:
                    case MenuType.AppInfomation:
                        if (SplitViewContentFrame.Navigate(typeof(Views.SettingsDetailPage), mi))
                        {
                            if (SplitViewContentFrame.BackStack.Count > 0)
                            {
                                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                            }
                            Settings.General.LastMenuType = MenuType.Settings;
                            Settings.General.LastPlayListSeq = -1;
                        }
                        break;
                }
            }
        }

        private void NewPlayListTapped(Message<PlayList> message)
        {
            var mainPage = ElementHelper.FindVisualParent<Views.MainPage>(SplitViewContentFrame);
            var button = ElementHelper.FindVisualChild<Button>(mainPage, "NewPlayListButton");
            ShowNewPlayList(button, message.Action);
        }

        private void ShowLoadingPanel(bool isOpen)
        {
            ShowLoadingPanel(new Message("IsOpen", isOpen));
        }

        private async void ShowLoadingPanel(Message msg)
        {
            if (msg.ContainsKey("IsOpen"))
            {
                bool isOpen = msg.GetValue<bool>("IsOpen");
                
                await ThreadPool.RunAsync(async handler =>
                {
                    await DispatcherHelper.RunAsync(() =>
                    {
                        if (isOpen)
                        {
                            if (!AppProtection.IsHideAppLockPanel) return;

                            if (msg.ContainsKey("LoadingTitle"))
                                LoadingText = msg.GetValue<string>("LoadingTitle");
                            else
                                LoadingText = ResourceLoader.GetForCurrentView().GetString("Loading/Playback/Text");

                            _LoadingPopup = new Popup { IsOpen = true };
                            _LoadingPopup.Child = _Loading;
                            _Loading.Width = Window.Current.Bounds.Width;
                            _Loading.Height = Window.Current.Bounds.Height;
                        }
                        else
                        {
                            if (_LoadingPopup != null)
                            {
                                _LoadingPopup.Child = null;
                                _LoadingPopup.IsOpen = false;
                                _LoadingPopup = null;
                            }
                        }
                    });
                }, WorkItemPriority.High);
            }
        }

        private async void FileAssociation(Message message)
        {
            if (_IsLockingByTrialExpried) return;

            var enumFiles = message.GetValue<IEnumerable<IStorageItem>>();
            var playList = MainMenuItemSource.FirstOrDefault(x => x is PlayList && (x as PlayList).Seq == 1) as PlayList;
            
            if (playList != null && enumFiles.Count() > 0)
            {
                var files = enumFiles.Where(x => x.IsOfType(StorageItemTypes.File) 
                                                && (CUXC.MediaFileSuffixes.VIDEO_SUFFIX.Contains(Path.GetExtension(x.Path).ToUpper())) 
                                                    || CUXC.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(Path.GetExtension(x.Path).ToUpper()))
                                          .Select(x => new StorageItemInfo(x, SubType.FileAssociation)).ToArray();

                //var storageItemInfoList = enumFiles.Where(x => x.IsOfType(StorageItemTypes.File)).Select(x => new StorageItemInfo(x, SubType.FileAssociation)).ToArray();
                //if (storageItemInfoList?.Count() > 0)
                if (files?.Count() > 0)
                {
                    //var dbResult = _PlayListDAO.InsertPlayListFiles(playList, storageItemInfoList);
                    var dbResult = _PlayListDAO.InsertPlayListFiles(playList, files);
                    if (dbResult == SQLitePCL.SQLiteResult.DONE)
                    {
                        //var firstItem = storageItemInfoList.FirstOrDefault();
                        var firstItem = files.FirstOrDefault(x => CUXC.MediaFileSuffixes.VIDEO_SUFFIX.Contains(Path.GetExtension(x.Path).ToUpper()));
                        // message.Add("DecoderType", DecoderType.AUTO);
                        message.Add("StorageItemInfo", firstItem);

                        int nowPlayingIndex = MainMenuItemSource.IndexOf(MainMenuItemSource.FirstOrDefault(x => x.Type == MenuType.NowPlaying));
                        if (SelectedMainMenuIndex == nowPlayingIndex)
                        {
                            //현재 화면이 "지금 재생중"인 경우는 이벤트 통지
                            MessengerInstance.Send(message, "SelectPlayListFile");
                        }
                        else
                        {
                            SimpleIoc.Default.GetInstance<PlayListViewModel>();
                            MessengerInstance.Send(message, "PrepareLoadPlayListFile");
                            //지금 재생 중
                            SelectedMainMenuIndex = nowPlayingIndex;
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
        }
        private async void RequestPlayback(Message message)
        {
            LoadingText = ResourceLoader.GetForCurrentView().GetString("Loading/Playback/Text");
            //최초 재생 모드 설정
            _IsFirstPlay = (MediaElementVisibleState != "Visible" && !_IsFirstPlay);

            MediaElementSlideDistance = 0;
            MediaElementVisibleState = "Visible";

            SystemNavigationManager sysNavMgr = SystemNavigationManager.GetForCurrentView();
            sysNavMgr.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            try
            {
                var obj = Common.Helper.ReflectionHelper.GetRuntimeProperty(
                    "Windows.UI.Xaml.Controls.ComboBox, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
                    "AllowFocusOnInteractionProperty");
                
                if (obj is DependencyProperty)
                {
                    var dp = obj as DependencyProperty;
                    _MediaElement.ApplyComboBoxPatch(dp);
                }

                //기본 환경 설정
                _MediaElement.UseLimeEngine = Settings.Playback.UseLimeEngine;
                _MediaElement.UseGpuShader = Settings.Playback.UseGpuShader;
                _MediaElement.UseAttachment = Settings.General.UseSaveFontInMkv;
                _MediaElement.DecoderType = message.GetValue<DecoderTypes>("DecoderType");
                _MediaElement.AspectRatio = Xaml.Controls.AspectRatios.Uniform;
                _MediaElement.AutoPlay = true;
                _MediaElement.DefaultPlaybackRate = 1;

                if (message.IsValueType<PlayListFile>("CurrPlayListFile"))
                {
                    //요청 재생 파일
                    var reqPlayListFile = message.GetValue<PlayListFile>("CurrPlayListFile");
                    _ReqPlayListFile = reqPlayListFile;
                    //이전 파일 바인딩
                    PrevPlayListFile = message.GetValue<PlayListFile>("PrevPlayListFile");
                    //다음 파일 바인딩
                    NextPlayListFile = message.GetValue<PlayListFile>("NextPlayListFile");
                    //파일의 스트림 열기
                    StorageFile file = await reqPlayListFile.GetStorageFileAsync();
                    //var file = await StorageFile.CreateStreamedFileFromUriAsync("bigbuck.mp4", new Uri("http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_1080p_h264.mov", UriKind.Absolute), null);
                    //open시 전체 다운로드가 된다.
                    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                    //비디오 소스 설정
                    _MediaElement.SetSource(stream, string.IsNullOrEmpty(file.ContentType) ? "video/ffmpeg" : file.ContentType);
                    //관련 자막 추가
                    AddClosedCaptions(file);
                }
                else if (message.IsValueType<NetworkItemInfo>("CurrPlayListFile"))
                {
                    //Network
                    //요청 재생 파일
                    var reqPlayListFile = message.GetValue<NetworkItemInfo>("CurrPlayListFile");
                    _ReqPlayListFile = reqPlayListFile;
                    //이전 파일 바인딩
                    PrevPlayListFile = message.GetValue<NetworkItemInfo>("PrevPlayListFile");
                    //다음 파일 바인딩
                    NextPlayListFile = message.GetValue<NetworkItemInfo>("NextPlayListFile");

                    //계정 설정
                    string vidUsr = string.Empty;
                    string vidPwd = string.Empty;
                    int codePage = -1;

                    if (message.ContainsKey("VideoUserName"))
                        vidUsr = message.GetValue<string>("VideoUserName");
                    if (message.ContainsKey("VideoPassword"))
                        vidPwd= message.GetValue<string>("VideoPassword");
                    if (message.ContainsKey("CodePage"))
                        codePage = message.GetValue<int>("CodePage");

                    PropertySet ps = new PropertySet
                    {
                        ["AuthUrl"] = reqPlayListFile.GetAuthenticateUrl(vidUsr, vidPwd),
                        ["CodePage"] = codePage
                    };
                    //FFmpeg으로 전달할 값
                    _MediaElement.Tag = ps;
                    
                    //비디오 소스 설정
                    _MediaElement.Source = reqPlayListFile.Uri;
                    //관련 자막 추가
                    if (_ReqPlayListFile?.SubtitleList != null)
                    {
                        var reqItem = _ReqPlayListFile as NetworkItemInfo;
                        string subUsr = string.Empty;
                        string subPwd = string.Empty;

                        if (reqPlayListFile.ServerType == ServerTypes.Direct)
                        {
                            if (message.ContainsKey("SubtitleUserName"))
                                subUsr = message.GetValue<string>("SubtitleUserName");
                            if (message.ContainsKey("SubtitlePassword"))
                                subPwd = message.GetValue<string>("SubtitlePassword");
                        }
                        else
                        {
                            subUsr = vidUsr;
                            subPwd = vidPwd;
                        }
                        _MediaElement.AddClosedCaptionUriSources(reqItem.GetAuthenticateSubtitleUrl(subUsr, subPwd), codePage);
                    }
                }
            }
            catch (Exception e)
            {
                //로딩창 닫기
                ShowLoadingPanel(false);
                System.Diagnostics.Debug.WriteLine("파일 재생 실패 : " + e.Message);
                //에러 메세지 출력
                var resource = ResourceLoader.GetForCurrentView();
                var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("Message/Error/LoadMedia"),
                        resource.GetString("Message/Error/CheckFile"),
                        resource.GetString("Button/Close/Content"));
                await dlg.ShowAsync();
                App.ContentDlgOp = null;
                //Back스택 제거
                if (sysNavMgr.AppViewBackButtonVisibility == AppViewBackButtonVisibility.Visible
                    && MediaElementVisibleState == "Visible")
                {
                    sysNavMgr.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                    MediaElementVisibleState = "Collapsed";
                    MediaElementSlideDistance = Window.Current.Bounds.Height * -1;
                    MediaElementSlideDistance2 = Window.Current.Bounds.Height;
                }
            }
        }
        #endregion

        #region event handler
        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            await DispatcherHelper.RunAsync(() =>
            {
                //재생패널이 안보일때만 수행, 재생 패널의 회전은 MediaElement내에서 처리한다.
                if (MediaElementVisibleState != "Visible")
                {
                    if (DisplayInformation.GetForCurrentView().NativeOrientation == DisplayOrientations.Portrait)
                    {
                        //기본이 세로 모드인 디바이스 (폰)
                        switch (args.Orientation)
                        {
                            case SimpleOrientation.NotRotated:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                break;
                            case SimpleOrientation.Rotated90DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
                                break;
                            case SimpleOrientation.Rotated180DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                break;
                            case SimpleOrientation.Rotated270DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.LandscapeFlipped;
                                break;
                        }
                    }
                    else
                    {
                        //기본이 가로 모드인 디바이스 (테블릿 등)
                        switch (args.Orientation)
                        {
                            case SimpleOrientation.NotRotated:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
                                break;
                            case SimpleOrientation.Rotated90DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                break;
                            case SimpleOrientation.Rotated180DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.LandscapeFlipped;
                                break;
                            case SimpleOrientation.Rotated270DegreesCounterclockwise:
                                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                break;
                        }
                    }
                }
            });
        }

        private void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            //로딩 패널이 표시되고 있으면 숨김
            ShowLoadingPanel(false);
            System.Diagnostics.Debug.WriteLine(e.Message + "$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            AppProtection.Width = e.Size.Width;
            AppProtection.Height = e.Size.Height;

            _Loading.Width = e.Size.Width;
            _Loading.Height = e.Size.Height;
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                //설정 저장
                _SettingDAO.Replace(Settings);
                System.Diagnostics.Debug.WriteLine("액티베이트 - 설정 저장");
                //모바일의 경우 앱이 비활성화 될때 일시정지 처리
                if (App.IsMobile && _MediaElement != null)
                {
                    //현재 상태 저장
                    _BeforeHideMediaElementState = _MediaElement.CurrentState;
                    //서스펜드 모드 일시 정지 처리
                    if (Settings.Playback.UseSuspendToPause)
                    {
                        //숨김 처리시 일시정지
                        _MediaElement?.Pause();
                    }
                }
            }
        }
        
        private void Current_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            //모바일이 아니면 여기서 일시 정지 처리
            if (!App.IsMobile && _MediaElement != null && !e.Visible)
            {
                //현재 상태 저장
                _BeforeHideMediaElementState = _MediaElement.CurrentState;
                //서스펜드 모드 일시 정지 처리
                if (Settings.Playback.UseSuspendToPause)
                {
                    //숨김 처리시 일시정지
                    _MediaElement?.Pause();
                }
            }

            if (!Settings.Privacy.UseAppLock)
            {
                if (_AppProtectionPopup != null)
                {
                    _AppProtectionPopup.Child = null;
                    _AppProtectionPopup.IsOpen = false;
                    _AppProtectionPopup = null;
                }

                if (e.Visible
                    && _BeforeHideMediaElementState == MediaElementState.Playing
                    && _MediaElement?.CurrentState == MediaElementState.Paused)
                {
                    //화면 활성화시 이전 상태에 따라 재생 시작
                    _MediaElement?.Play();
                }
            }
            else
            {
                if (e.Visible &&
                    Settings.Privacy.UseAppLock && App.StoragePickerStatus != StoragePickerStatus.Opened)
                {
                    System.Diagnostics.Debug.WriteLine("잠금화면 - 보호모드 동작");

                    if (_AppProtectionPopup != null)
                    {
                        _AppProtectionPopup.Child = null;
                    }
                    _AppProtectionPopup = new Popup();
                    _AppProtectionPopup.Child = AppProtection;
                    _AppProtectionPopup.IsOpen = true;

                    AppProtection.RequestedTheme = Settings.General.ElementTheme;
                    AppProtection.Password = Settings.Privacy.AppLockPassword;
                    AppProtection.PasswordHint = Settings.Privacy.AppLockPasswordHint;
                    AppProtection.IsHideAppLockPanel = false;
                    AppProtection.Width = Window.Current.Bounds.Width;
                    AppProtection.Height = Window.Current.Bounds.Height;
                }
                //초기화
                App.StoragePickerStatus = StoragePickerStatus.Closed;
            }
        }

        private void AppProtection_LoginSucceed(object sender, EventArgs e)
        {
            if (_BeforeHideMediaElementState == MediaElementState.Playing
                && _MediaElement?.CurrentState == MediaElementState.Paused)
            {
                //로그인 성공시 재생 시작
                _MediaElement?.Play();
            }
        }

        private async void Frame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            Frame frame = sender as Frame;
            frame.Navigated -= Frame_Navigated;

            try
            {
                var dlnaFolderQuery = KnownFolders.MediaServerDevices.CreateFolderQuery();
                var dlnaFolderCount = await dlnaFolderQuery.GetItemCountAsync();
                if (dlnaFolderCount > 0)
                {
                    MainMenuItemSource.Insert(1, new MenuItem() { Type = MenuType.DLNA, Name = dlnaFolderQuery.Folder.DisplayName, Glyph = "\xE953" });//969? DLNA
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.Message);
            }

            //재생 목록 로드
            _PlayListDAO.LoadPlayList(PlayListSource, null);

            //마지막 화면 로드
            var menuType = Settings.General.LastMenuType;
            var seq = Settings.General.LastPlayListSeq;

            switch (menuType)
            {
                case MenuType.NowPlaying:
                    SelectedMainMenuIndex = MainMenuItemSource.IndexOf(MainMenuItemSource.FirstOrDefault(x => x is PlayList && (x as PlayList).Seq == seq));
                    SelectedPlayListIndex = -1;
                    break;
                case MenuType.Playlist:
                    SelectedMainMenuIndex = -1;
                    SelectedPlayListIndex = PlayListSource.IndexOf(PlayListSource.FirstOrDefault(x => x.Seq == seq));
                    break;
                case MenuType.Settings:
                    SettingsTapped(sender, null);
                    break;
                default:
                    {
                        MenuItem mi = MainMenuItemSource.FirstOrDefault(x => x.Type == menuType);
                        if (mi != null)
                        {
                            SelectedMainMenuIndex = MainMenuItemSource.IndexOf(mi);
                            MenuAction(mi);
                        }
                    }
                    break;
            }

            //폰트 로드
            //FontHelper.LoadAllFont(() =>
            //{
            //    System.Diagnostics.Debug.WriteLine("폰트 로딩 완료.");
            //});

            //MediaElement를 검색하여 변수에 저장
            _MediaElement = Lime.Xaml.Helpers.ElementHelper.FindVisualChild<CUXC.MediaElement>(frame.Content as MainPage);

            if (_MediaElement == null)
            {
                Debugger.Break();
            }
        }
        
        private void HardwareButtons_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            bool handled = e.Handled;
            BackRequested(ref handled);
            e.Handled = handled;

            if (!handled)
            {
                e.Handled = true;
                Frame rootFrame = Window.Current.Content as Frame;
                //성능 개선부터 하고 다시 해보자...
                if (!rootFrame.CanGoBack)
                {
                    if (ReadyExitAppVisibleState != "Visible")
                    {
                        ReadyExitAppVisibleState = "Visible";
                        ThreadPoolTimer.CreateTimer((handler) =>
                        {
                            DispatcherHelper.CheckBeginInvokeOnUI(() => { ReadyExitAppVisibleState = "Collapsed"; });
                        }, TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        App.Current.Exit();
                    }
                }
            }
        }
        private void Navigation_BackRequested(object sender, BackRequestedEventArgs e)
        {
            bool handled = e.Handled;
            BackRequested(ref handled);
            e.Handled = handled;
        }
        private void MainMenuButtonTappedCommandExecute(SplitView splitter)
        {
            //풀사이즈로 표시(DisplayMode에 따라 확장 됨. inline/overlay)
            IsMenuOpen = !IsMenuOpen;
        }
        private void NewPlayListTapped(object sender, TappedRoutedEventArgs args)
        {
            //foreach (var pl in PlayListSource)
            //    playListDAO.DeletePlayList(pl);
            //PlayListSource.Clear();

            var button = sender as Button;
            ShowNewPlayList(button, SelectPlayListIndex);
        }
        private void AppLockTapped(object sender, TappedRoutedEventArgs e)
        {
            //Settings.Privacy.AppLockPassword = string.Empty; //페이지 이동 테스트
            if (Settings.Privacy.CanAppLock)
            {
                if (VersionHelper.CheckPaidFeature())
                {
                    Settings.Privacy.UseAppLock = !Settings.Privacy.UseAppLock;
                    _SettingDAO.Replace(Settings);
                }
            }
            else
            {
                SelectedMainMenuIndex = -1;
                SelectedPlayListIndex = -1;
                //오버레이 모드에서 메뉴 클릭시 스플릿 패널 메뉴 닫음
                CloseMenuPanel(sender as DependencyObject);

                var mi = new MenuItem
                {
                    Type = MenuType.PrivacySetting,
                    Name = ResourceLoader.GetForCurrentView().GetString("Settings/Menu/AppLock/Title")
                };
                if (SplitViewContentFrame.Content != null && SplitViewContentFrame.Content.GetType() == typeof(Views.SettingsDetailPage))
                {
                    var sdvm = SimpleIoc.Default.GetInstance<SettingsDetailViewModel>();
                    sdvm.CurrentDetailMenuItem = mi;
                }
                else
                {
                    if (SplitViewContentFrame.Navigate(typeof(Views.SettingsDetailPage), mi))
                    {
                        SplitViewContentFrame.BackStack.Clear();
                        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                    }
                }
            }
        }
        private void SettingsTapped(object sender, TappedRoutedEventArgs e)
        {
            //메뉴 패널 닫기
            CloseMenuPanel(sender as DependencyObject);
            //설정 메뉴로 이동
            MenuAction(new MenuItem
            {
                Type = MenuType.Settings,
                Name = ResourceLoader.GetForCurrentView().GetString("MainMenu/Settings/Title/Text"),
                Glyph = "\xE713"
            });
        }
        private async void SavePlayListTappedCommandExecute(TextBox value)
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            if (VersionHelper.CheckPaidFeature())
            {
                var pl = new PlayList() { Name = value.Text.Trim() };

                if (string.IsNullOrEmpty(pl.Name))
                {
                    value.Focus(FocusState.Keyboard);
                }
                else if (PlayListSource.Any(x => x.Name == pl.Name))
                {
                    NewPlayList.ShowErrorMessage = true;
                    NewPlayList.ErrorMessage = resource.GetString("Message/Error/DuplicatedName");
                    value.SelectAll();
                    value.Focus(FocusState.Keyboard);
                }
                else
                {
                    NewPlayList.ShowErrorMessage = false;
                    NewPlayList.ErrorMessage = string.Empty;

                    var result = _PlayListDAO.InsertPlayList(pl);
                    if (result == SQLitePCL.SQLiteResult.DONE)
                    {
                        MessengerInstance.Send(new KeyValuePair<string, PlayList>("added", pl), "PlayListChanged");
                        DialogHelper.HideFlyout(App.Current.Resources, "PlayListFlyout");
                        NewPlayList.CallbackAction.Invoke(pl);
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
        public void DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = _DragCaption;
            //e.DragUIOverride.SetContentFromBitmapImage(null); // Sets a custom glyph
            e.DragUIOverride.IsCaptionVisible = true; // Sets if the caption is visible
            e.DragUIOverride.IsContentVisible = true; // Sets if the dragged content is visible
            e.DragUIOverride.IsGlyphVisible = true; // Sets if the glyph is visibile
        }
        public async void Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                List<IStorageItem> fileList = new List<IStorageItem>();
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    if (items.Any(x => x.IsOfType(StorageItemTypes.File)))
                    {
                        var files = items.Where(x => x.IsOfType(StorageItemTypes.File) 
                                            && (CUXC.MediaFileSuffixes.VIDEO_SUFFIX.Contains(Path.GetExtension(x.Path).ToUpper()) || CUXC.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(Path.GetExtension(x.Path).ToUpper())))
                                        .OrderBy(x => x.Path);
                        fileList.AddRange(files);
                    }

                    if (items.Any(x => x.IsOfType(StorageItemTypes.Folder)))
                    {
                        var folders = items.Where(x => x.IsOfType(StorageItemTypes.Folder)).Cast<StorageFolder>();
                        foreach (var folder in folders)
                        {
                            var queryResult = folder.CreateFileQueryWithOptions(_VideoSubtitleFileQueryOptions);
                            var files = await queryResult.GetFilesAsync();
                            fileList.AddRange(files);
                        }
                    }
                }

                if (fileList.Count > 0)
                {
                    //Messenger.Default.Send(new Message(fileList), "FileAssociation");
                    FileAssociation(new Message(fileList));
                }
            }
        }
        #endregion

        #region MediaElement eventhandler
        public void CloseButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            ClosedPlayback();
        }
        public void MediaOpened(object sender, RoutedEventArgs e)
        {
            //로딩 패널 숨김
            ShowLoadingPanel(false);

            if (_ReqPlayListFile is PlayListFile)
            {
                var reqPlayListFile = _ReqPlayListFile as PlayListFile;
                //이전 재생 포지션 지정            
                var pausedTime = reqPlayListFile.PausedTime;
                if (pausedTime.TotalSeconds > 0 &&
                    _MediaElement != null && (UInt64)_MediaElement.NaturalDuration.TimeSpan.TotalSeconds > (UInt64)pausedTime.TotalSeconds)
                {
                    _MediaElement.Position = pausedTime;
                }
            }
            //재생 현재 정보 교체
            CurrPlayListFile = _ReqPlayListFile;
            //모바일이고 첫번째 재생 파일인 경우 화면 가로 회전
            if (Settings.Playback.UseBeginLandscape && App.IsMobile && _IsFirstPlay)
            {
                _PrevDisplayInformation = DisplayInformation.AutoRotationPreferences;
                //실행이 되긴하나 Exception이 발생하는데 왜????
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            _IsFirstPlay = false;
        }
        public void MediaEnded(object sender, RoutedEventArgs e)
        {
            //위치 저장 하면 안됨 (이전 파일과 현재 재생중인 파일이 타임이 꼬임)
            //이동된 위치 저장
            SavePosition();
        }

        public void MediaFailed(object sender, RoutedEventArgs e)
        {
            //로딩 패널 숨김
            ShowLoadingPanel(false);
        }

        public void SeekCompleted(object sender, RoutedEventArgs e)
        {
            //이동된 위치 저장
            SavePosition();
        }

        public void CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var state = _MediaElement.CurrentState;
            System.Diagnostics.Debug.WriteLine($"상태 변경됨 : {state}");
        }
        
        public void PreviousMediaButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            _MediaElement.Stop();
            var content = SplitViewContentFrame.Content;
            
            switch (content?.GetType()?.Name)
            {
                case "DLNAPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", PrevPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "DLNANextPlayListFile");
                    break;
                case "NetworkPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", PrevPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "NetworkNextPlayListFile");
                    break;
                case "CloudPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", PrevPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "CloudNextPlayListFile");
                    break;
                default:
                    MessengerInstance.Send(-1, "NextPlayListFile");
                    break;
            }
        }

        public void NextMediaButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            _MediaElement.Stop();
            var content = SplitViewContentFrame.Content;

            switch (content?.GetType()?.Name)
            {
                case "DLNAPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", NextPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "DLNANextPlayListFile");
                    break;
                case "NetworkPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", NextPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "NetworkNextPlayListFile");
                    break;
                case "CloudPage":
                    MessengerInstance.Send(
                        new Message()
                        .Add("NextPlayListFile", NextPlayListFile)
                        .Add("DecoderType", _MediaElement.DecoderType),
                        "CloudNextPlayListFile");
                    break;
                default:
                    MessengerInstance.Send(1, "NextPlayListFile");
                    break;
            }
        }
        
        public void AttachmentPopulated(Common.Interface.IAttachmentDecoderConnector sender, AttachmentData attachment)
        {
            if (attachment.MimeType == "application/x-truetype-font")
            {
                _SaveFontList.Add(new KeyValuePair<string, byte[]>(attachment.FileName, attachment.BinaryData));
            }
        }

        public void AttachmentCompleted(Common.Interface.IAttachmentDecoderConnector sender, object args)
        {
            if (_SaveFontList.Any())
            {
                FontHelper.InstallFont(_SaveFontList, true);
            }
        }

        public void CCSettingsOpenButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            IsCCSettingsOpen = true;
            CCSettingsHeight = Window.Current.CoreWindow.Bounds.Height;
        }

        public void CCSettingsPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(CCSettingsHorizontalOffset) != e.NewSize.Width)
            {
                CCSettingsHorizontalOffset = e.NewSize.Width * -1;
            }
        }

        #endregion
        
        public async void AddClosedCaptions(StorageFile file)
        {
            //오디오 선택 테스트...해보고 싶다..
           // var audioSelector = Windows.Media.Devices.MediaDevice.GetAudioRenderSelector();
           // var audioEndpoints = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(audioSelector);
            //new Windows.Media.Playback.MediaPlayer().AudioDevice.

            if (_ReqPlayListFile != null && _ReqPlayListFile.SubtitleList != null)
            {
                List<IRandomAccessStream> subtitleStreamList = new List<IRandomAccessStream>();
                foreach (var path in _ReqPlayListFile.SubtitleList)
                {
                    try
                    {
                        var ccFile = await StorageFile.GetFileFromPathAsync(path);
                        subtitleStreamList.Add(await ccFile.OpenAsync(FileAccessMode.Read));
                    }
                    catch (Exception)
                    {
                        System.Diagnostics.Debug.WriteLine($"{path} 자막 파일 처리중 에러 발생...");
                    }
                }
                _MediaElement.AddClosedCaptionStreamSources(subtitleStreamList);
                //"외부 자막 열기" 시도시 저장할 자막 폴더 및 파일명 설정
                try
                {
                    var currFolder = await file.GetParentAsync();
                    if (currFolder != null)
                    {
                        _MediaElement.SetFilePathInfo(currFolder, Path.GetFileNameWithoutExtension(file.Path));
                    }
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine($"{_ReqPlayListFile.ParentFolderPath} 폴더에 대한 접근 권한없음...");
                }
            }
        }

        public void ClosedPlayback()
        {
            //모바일이면 화면 회전 복원
            if (Settings.Playback.UseBeginLandscape && App.IsMobile)
            {
                DisplayInformation.AutoRotationPreferences = _PrevDisplayInformation;
            }
            //재생 위치 저장
            SavePosition();
            //미디어 완전 정지
            _MediaElement.Stop();
            //재생 파일 정보 삭제
            CurrPlayListFile = null;

            MediaElementVisibleState = "Collapsed";
            MediaElementSlideDistance = Window.Current.Bounds.Height * -1;
            MediaElementSlideDistance2 = Window.Current.Bounds.Height;

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;
            _IsFirstPlay = false;
        }

        private void BackRequested(ref bool handled)
        {
            ShowLoadingPanel(false);

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
                return;
            
            var currPageType = rootFrame.CurrentSourcePageType;

            if (MediaElementVisibleState == "Visible" && !handled)
            {
                //재생정지
                ClosedPlayback();
                handled = true;
            }
            else if (currPageType == typeof(CCPlayer.UWP.Views.MainPage))
            {
                if (SplitViewContentFrame.CurrentSourcePageType == typeof(CCPlayer.UWP.Views.SettingsDetailPage))
                {
                    if (SplitViewContentFrame.CanGoBack && !handled)
                    {
                        handled = true;
                        SplitViewContentFrame.GoBack();
                        Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;
                    }
                    //여기서 최종 적으로 변경된 설정을 DB에 저장
                    _SettingDAO.Replace(Settings);
                }
                else if (Settings.General.UseHardwareBackButtonWithinVideo)
                {
                    if (SplitViewContentFrame.CurrentSourcePageType == typeof(CCPlayer.UWP.Views.ExplorerPage))
                    {
                        //상위 폴더로 이동
                        handled = SimpleIoc.Default.GetInstance<ExplorerViewModel>().CurrentFolderInfo != null;
                        if (handled)
                            MessengerInstance.Send(true, "BackRequested");
                    }
                    if (SplitViewContentFrame.CurrentSourcePageType == typeof(CCPlayer.UWP.Views.DLNAPage))
                    {
                        //상위 폴더로 이동
                        handled = SimpleIoc.Default.GetInstance<DLNAViewModel>().CurrentFolderInfo != null;
                        if (handled)
                            MessengerInstance.Send(true, "DLNABackRequested");
                    }
                    if (SplitViewContentFrame.CurrentSourcePageType == typeof(CCPlayer.UWP.Views.NetworkPage))
                    {
                        //상위 폴더로 이동
                        //handled = SimpleIoc.Default.GetInstance<NetworkViewModel>().CurrentFolderInfo != null;
                        handled = SimpleIoc.Default.GetInstance<NetworkViewModel>().HasUpperFolder;
                        if (handled)
                            MessengerInstance.Send(true, "NetworkBackRequested");
                    }
                    if (SplitViewContentFrame.CurrentSourcePageType == typeof(CCPlayer.UWP.Views.CloudPage))
                    {
                        //상위 폴더로 이동
                        handled = SimpleIoc.Default.GetInstance<CloudViewModel>().HasUpperFolder;
                        if (handled)
                            MessengerInstance.Send(true, "CloudBackRequested");
                    }
                }
            }
        }

        private void ShowNewPlayList(Button button, Action<PlayList> action)
        {
            //에러 초기화
            NewPlayList.ShowErrorMessage = false;
            NewPlayList.ErrorMessage = string.Empty;
            //입력 텍스트 초기화
            NewPlayList.PrimaryContent = string.Empty;
            //콜백 액션 정의
            NewPlayList.CallbackAction = action;
            //팝업 보이기
            DialogHelper.ShowFlyout(App.Current.Resources, "PlayListFlyout", button, (ke) =>
            {
                if (ke.Key == Windows.System.VirtualKey.Enter)
                {
                    SavePlayListTappedCommandExecute(ke.OriginalSource as TextBox);
                }
            });
        }

        private void SelectPlayListIndex(PlayList pl)
        {
            SelectedPlayListIndex = PlayListSource.IndexOf(PlayListSource.Last(x => x.Name == pl.Name));
        }
        
        /// <summary>
        /// 오버레이 모드에서 메뉴 클릭시 스플릿 패널 메뉴를 닫는다
        /// </summary>
        /// <param name="obj"></param>
        private void CloseMenuPanel(DependencyObject obj)
        {
            var splitView = ElementHelper.FindVisualParent<SplitView>(obj);
            if (splitView != null && IsMenuOpen
                && (splitView.DisplayMode == SplitViewDisplayMode.Overlay
                || splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay))
            {
                IsMenuOpen = false;
            }
        }

        private void SavePosition()
        {
            var content = SplitViewContentFrame.Content;
            if (content != null && content.GetType() == typeof(Views.DLNAPage))
                return;
            
            if (CurrPlayListFile is PlayListFile)
            {
                var currFile = CurrPlayListFile as PlayListFile;
                if (_MediaElement != null && currFile != null)
                {
                    var state = _MediaElement.CurrentState;
                    if (state == MediaElementState.Playing || state == MediaElementState.Paused || state == MediaElementState.Stopped)
                    {
                        //위치 저장
                        double position = _MediaElement.Position.TotalSeconds;
                        double duration = _MediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                        TimeSpan durrDuration = currFile.Duration;
                        currFile.PausedTime = _MediaElement.Position;

                        Messenger.Default.Send(new Message(CurrPlayListFile), "SavePlayListFile");
                        System.Diagnostics.Debug.WriteLine($"{CurrPlayListFile.DisplayName} : { _MediaElement.CurrentState}  - save position => {currFile.PausedTime}");
                    }
                }
            }
        }
    }
}
