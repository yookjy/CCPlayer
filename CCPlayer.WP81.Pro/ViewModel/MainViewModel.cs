using CCPlayer.WP81.Extensions;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Views;
using FFmpegSupport;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

/**
    * FutureAccessList에서 "All videos" 및 "File Accosiation"을 제외한 파일들이다. 
    * 
    * !!!! 개선사항
    *  - File Accosication 및 FutureAccessList의 효율적 활용 그리고 SQL Lite를 고려하자.
    *  SQL Lite : 
    *  https://code.msdn.microsoft.com/windowsapps/WindowsPhone-8-SQLite-96a1e43b
    *  http://www.sqlite.org/docs.html
    *  http://channel9.msdn.com/Series/Building-Apps-for-Windows-Phone-8-1/19
    *  SQLite-NET vs SQLitePCL (SQLite portable class library)
    */
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
    public partial class MainViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(MainViewModel).Name;
        //참고 command http://rarcher.azurewebsites.net/Post/PostContent/26
        //참고 behavior http://www.reflectionit.nl/Blog/2013/windows-8-xaml-tips-conditional-behaviors
        #region 데이터 모델

        //허브 타이틀
        public string AppVersion { get { return VersionHelper.VersionName; } }
        
        //설정
        public Settings Settings { get; set; }
        //설정관련 DAO
        private SettingDAO settingDAO;
        private Hub hub;
        //전체 비디오 허브 섹션 백업용
        private HubSection currentHubSection;
        //전체 비디오 삭제 백업용
        private HubSection allVideoHubSection;

        private bool isPlayerOpened;
        private bool isSearchOpened;
        private bool isSettingsOpened;

        private bool _LoadingPanelVisible;
        public bool LoadingPanelVisible
        {
            get { return _LoadingPanelVisible; } 
            set 
            {
                if (_LoadingPanelVisible != value)
                Set(ref _LoadingPanelVisible, value, true);
            }
        }

        private string _LoadingPanelText;
        public string LoadingPanelText
        {
            get { return _LoadingPanelText; }
            set { Set(ref _LoadingPanelText, value); }
        }

        #endregion

        #region 커맨드

        public ICommand SectionsInViewChangedCommand { get; private set; }
        public ICommand NavigateToSettingsCommand { get; set; }
        
        #endregion
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(SettingDAO settingDAO, Windows.Media.MediaExtensionManager extMgr)
        {
            this.settingDAO = settingDAO;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();

            //셋팅 데이터 로드
            LoadSetting();
            //뒤로가기 버튼 이벤트 등록
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            //화면 회전 고정
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
            //에러 이벤트 추가
            App.Current.UnhandledException += Current_UnhandledException;
            //활성화 이벤트 
            DispatcherHelper.CheckBeginInvokeOnUI(() => { Window.Current.Activated += Current_Activated; });
        }

        private void CreateModels()
        {
        }
        
        private void CreateCommands()
        {
            SectionsInViewChangedCommand = new RelayCommand<SectionsInViewChangedEventArgs>(SectionsInViewChangedCommandExecute);
            NavigateToSettingsCommand = new RelayCommand(NavigateToSettingsCommandExecute);
        }

        private void RegisterMessages()
        {
            MessengerInstance.Register<PropertyChangedMessage<bool>>(this, msg =>
                {
                    switch(msg.PropertyName)
                    {
                        case "IsPlayerOpened":
                            isPlayerOpened = msg.NewValue;
                            MessengerInstance.Send(isPlayerOpened, typeof(MainPage).FullName);
                            break;
                        case "IsSearchOpened":
                            isSearchOpened = msg.NewValue;
                            break;
                        case "IsSettingsOpened":
                            isSettingsOpened = msg.NewValue;
                            break;
                    }
                }
            );

            MessengerInstance.Register<Message>(this, msg =>
            {
                switch (msg.Key)
                {
                    case "RerfershAppVersion":
                        RaisePropertyChanged("AppVersion");
                        break;
                }
            });

            MessengerInstance.Register<Message>(this, NAME, (msg) =>
            {
                switch(msg.Key)
                {
                    case "ConfirmTermination" :
                        //종료 확인
                        ApplicationExit();
                        break;
                    case "MoveToPlaylistSection":
                        //플레이 리스트로 허브섹션 이동
                        MessengerInstance.Send<Message>(new Message("MoveToSection", currentHubSection), PlaylistViewModel.NAME);
                        break;
                    case "CheckSearchElement":
                        OnCheckSearchElement();
                        break;
                    case "ShowLoadingPanel":
                        OnShowLoadingPanel(msg.GetValue<KeyValuePair<string, bool>>());
                        break;
                    case "ShowErrorMessage":
                        OnShowErrorMessage(msg.GetValue<StackPanel>());
                        break;
                    case "ShowSelectionAudioMessage":
                        ShowSelectionAudioMessage(msg.GetValue<StackPanel>());
                        break;
                    case "RemoveAllVideoSection":
                        if (hub != null)
                        {
                            if (hub.Sections.Any(x => x.Name == "AllVideoSection"))
                            {
                                allVideoHubSection = hub.SectionsInView.First(x => x.Name == "AllVideoSection");
                                hub.Sections.Remove(allVideoHubSection);
                            }
                        }
                        break;
                    case "InsertAllVideoSectino":
                        if (hub != null)
                        {
                            if (!hub.Sections.Any(x => x.Name == "AllVideoSection"))
                            {
                                hub.Sections.Insert(1, allVideoHubSection);
                            }
                        }
                        break;
                }
            });
        }
        
        async void OnShowLoadingPanel(KeyValuePair<string, bool> param)
        {
            await ThreadPool.RunAsync(async handler =>
            {
                await Task.Delay(100);
                await DispatcherHelper.RunAsync(() =>
                {
                    LoadingPanelText = param.Key;
                    LoadingPanelVisible = param.Value;
                });
            });
        }

        private void OnShowErrorMessage(StackPanel contentPanel)
        {
            try
            {
                //재생패널 닫기 요청
                if (isPlayerOpened)
                {
                    MessengerInstance.Send(new Message("ExitPlay", true), CCPlayerViewModel.NAME);
                }

                //로딩 패널 제거
                if (LoadingPanelVisible)
                {
                    OnShowLoadingPanel(new KeyValuePair<string, bool>(string.Empty, false));
                }

                if (App.ContentDlgOp != null) return;

                ContentDialog contentDlg = new ContentDialog
                {
                    Content = contentPanel,
                    PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("Close")
                };
                //메세지 창 출력
                App.ContentDlgOp = contentDlg.ShowAsync();
                //후처리기 등록
                App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>((op, status) =>
                {
                    App.ContentDlgOp = null;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private void ShowSelectionAudioMessage(StackPanel contentPanel)
        {
            try
            {
                //재생패널 닫기 요청
                if (isPlayerOpened)
                {
                    MessengerInstance.Send(new Message("ExitPlay", true), CCPlayerViewModel.NAME);
                }

                //로딩 패널 제거
                if (LoadingPanelVisible)
                {
                    OnShowLoadingPanel(new KeyValuePair<string, bool>(string.Empty, false));
                }

                if (App.ContentDlgOp != null) return;

                ContentDialog contentDlg = new ContentDialog
                {
                    Content = contentPanel,
                    PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("OK")
                };
                //메세지 창 출력
                App.ContentDlgOp = contentDlg.ShowAsync();
                //후처리기 등록
                App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>((op, status) =>
                {
                    App.ContentDlgOp = null;
                    StreamInformation streamInfo = null;

                    foreach (StackPanel panel in contentPanel.Children.Where(x => x is StackPanel))
                    {
                        if (panel != null)
                        {
                            RadioButton rb = panel.Children.FirstOrDefault(x => x is RadioButton) as RadioButton;
                            if (rb != null && rb.IsChecked == true)
                            {
                                streamInfo = rb.Tag as StreamInformation;
                                break;
                            }
                        }
                    }

                    if (streamInfo != null)
                    {
                        MessengerInstance.Send(new Message("MultiAudioSelecting", streamInfo), CCPlayerViewModel.NAME);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private void LoadSetting()
        {
            Settings = settingDAO.SelectAll();
        }

        #region 커맨드핸들러

        void SectionsInViewChangedCommandExecute(SectionsInViewChangedEventArgs e)
        {
            if (hub == null)
            {
                HubSection section = null;
                if (e.AddedSections.Count > 0)
                {
                    section = e.AddedSections[0];
                }
                else if (e.RemovedSections.Count > 0)
                {
                    section = e.RemovedSections[0];
                }
                else
                {
//                    System.Diagnostics.Debug.WriteLine("허브 예외");
                    return;
                }

                hub = Lime.Xaml.Helpers.ElementHelper.FindVisualParent<Hub>(section);
            }

            if (!Settings.General.UseAllVideoSection && hub.Sections.Any(x => x.Name == "AllVideoSection"))
            {
                allVideoHubSection = hub.Sections.First(x => x.Name == "AllVideoSection");
                hub.Sections.Remove(allVideoHubSection);
            }
            
            //if (e.AddedSections.Count > 0)
            //{
            //    //빠른 앱바 처리를 위해 아래와 같이 노가다 조작
            //    HubSection newSection = null;
            //    if (e.AddedSections[0] == hub.SectionsInView.FirstOrDefault())
            //    {
            //        //좌로 이동
            //        newSection = e.AddedSections[0];
            //    }
            //    else if (e.AddedSections[0] == hub.SectionsInView.LastOrDefault())
            //    {
            //        //우로 이동
            //        newSection = hub.SectionsInView[1];
            //    }
            //    else
            //    {
            //        //첫 페이지를 거꾸로 들어온 경우
            //        newSection = hub.SectionsInView[0];
            //    }

            //    this.MessengerInstance.Send<Message>(new Message("Activated", newSection), newSection.ViewModelName());  
            //    currentHubSection = newSection;
            //}
            //else
            {
                //위의 경우에서 노가다 조작이 최종 상태를 반영 못하는 경우를 보정
                var newSection = hub.SectionsInView[0];
                //이전과 다른 경우만 처리
                if (currentHubSection != newSection)
                {
                    this.MessengerInstance.Send<Message>(new Message("Activated", newSection), newSection.ViewModelName());  
                    currentHubSection = newSection;
                }
            }
        }

        private void NavigateToSettingsCommandExecute()
        {
            MessengerInstance.Send(new Message("SettingsOpened", true), SettingsViewModel.NAME);
        }
        #endregion

        void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                MessengerInstance.Send(new Message("Deactivated"), CCPlayerViewModel.NAME);
            }
            else
            {
                MessengerInstance.Send(new Message("Activated"), CCPlayerViewModel.NAME);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            if (e != null)
            {
#if AD
                Exception exception = e.Exception;
                if (exception is NullReferenceException && exception.ToString().ToUpper().Contains("SOMA"))
                {
                    Debug.WriteLine("Handled Smaato null reference exception {0}", exception);
                    e.Handled = true;
                    return;
                }
                else if (exception is NullReferenceException && exception.ToString().ToUpper().Contains("MICROSOFT.ADVERTISING"))
                {
                    Debug.WriteLine("Handled Microsoft.Advertising exception {0}", exception);
                    e.Handled = true;
                    return;
                }


#endif
                // APP SPECIFIC HANDLING HERE
                if (Debugger.IsAttached)
                {
                    // An unhandled exception has occurred; break into the debugger
                    Debugger.Break();
                }

                //처리 완료 여부 설정
                e.Handled = true;

                StackPanel content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = e.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 12, 0, 12)
                });

                OnShowErrorMessage(content);
            }
            
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            var targetName = currentHubSection.ViewModelName();
            //로딩 패널이 표시되고 있으면 숨김
            if (LoadingPanelVisible)
            {
                LoadingPanelVisible = false;
            }
            
            if (SimpleIoc.Default.ContainsCreated<CCPlayerViewModel>() && isPlayerOpened)
            {
                targetName = CCPlayerViewModel.NAME;
            }
            else if (SimpleIoc.Default.ContainsCreated<MediaSearchViewModel>()  && isSearchOpened)
            {
                targetName = MediaSearchViewModel.NAME;
            }
            else if (SimpleIoc.Default.ContainsCreated<SettingsViewModel>() && isSettingsOpened)
            {
                targetName = SettingsViewModel.NAME;
            }
            
            this.MessengerInstance.Send<Message>(new Message("BackPressed", e), targetName);       
        }

        private void ApplicationExit()
        {
            if (App.ContentDlgOp != null) return;

            var loader = ResourceLoader.GetForCurrentView();
            bool? result = null;
            var contentDlg = new ContentDialog()
            {
                Content = new TextBlock { Text = loader.GetString("Message/Exit"), TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = loader.GetString("Ok"),
                PrimaryButtonCommand = new RelayCommand(() => { result = true; }),
                SecondaryButtonText = loader.GetString("Cancel"),
                SecondaryButtonCommand = new RelayCommand(() => { result = false; })
            };

            //메세지 창 출력
            App.ContentDlgOp = contentDlg.ShowAsync();
            //후처리기 등록
            App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>((op, status) =>
            {
                if (result == true)
                {
                    Window.Current.Activated -= Current_Activated;
                    //프로그램 종료 요청
                    Application.Current.Exit();
                };
                App.ContentDlgOp = null;
            });
        }

        private async void OnCheckSearchElement()
        {
            if (!SimpleIoc.Default.ContainsCreated<MediaSearchViewModel>())
            {
//                System.Diagnostics.Debug.WriteLine("검색 패널 생성중...");
                var mediaSearchDataContext = SimpleIoc.Default.GetInstance<MediaSearchViewModel>();
                await DispatcherHelper.RunAsync(() =>
                {
                    var frame = Window.Current.Content as Frame;
                    if (mediaSearchDataContext != null &&
                        frame != null && frame.Content is Page)
                    {
                        var main = frame.Content as Page;
                        MediaSearch ms = null;
                        (main.Content as Panel).Children.Add(ms =
                            new MediaSearch()
                            {
                                DataContext = mediaSearchDataContext,
                                Visibility = Visibility.Collapsed
                            });
                        Grid.SetRowSpan(ms, 2);
//                        System.Diagnostics.Debug.WriteLine("검색 패널 추가 완료");
                    }
                });
            }
        }
    }
}
