using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Views;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.System.Profile;
using Microsoft.Azure.Mobile;
using Microsoft.Azure.Mobile.Analytics;
using Microsoft.Azure.Mobile.Crashes;

namespace CCPlayer.UWP
{
    /// <summary>
    /// 기본 응용 프로그램 클래스를 보완하는 응용 프로그램별 동작을 제공합니다.
    /// </summary>
    sealed partial class App : Application
    {
        //파일/폴더 피커의 상태 저장
        public static StoragePickerStatus StoragePickerStatus { get; set; }

        public static IAsyncOperation<ContentDialogResult> ContentDlgOp { get; set; }

        private static bool? _isMobile;

        public static bool IsMobile
        {
            get
            {
                if (_isMobile == null)
                {
                    _isMobile = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile";
                }
                return (bool)_isMobile;
            }
        }

        private static bool? _isXbox;

        public static bool IsXbox
        {
            get
            {
                if (_isXbox == null)
                {
                    _isXbox = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox";
                }
                return (bool)_isXbox;
            }
        }

        /// <summary>
        /// Singleton 응용 프로그램 개체를 초기화합니다. 이것은 실행되는 작성 코드의 첫 번째
        /// 줄이며 따라서 main() 또는 WinMain()과 논리적으로 동일합니다.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// 최종 사용자가 응용 프로그램을 정상적으로 시작할 때 호출됩니다. 다른 진입점은
        /// 특정 파일을 여는 등 응용 프로그램을 시작할 때
        /// </summary>
        /// <param name="e">시작 요청 및 프로세스에 대한 정보입니다.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#region for Mobile Center
            // Get the user's geographic region and its two-letter identifier for this region.
            var geographicRegion = new Windows.Globalization.GeographicRegion();
            var code = geographicRegion.CodeTwoLetter;

            //MobileCenter.LogLevel = LogLevel.Verbose;
            MobileCenter.SetCountryCode(code);
            MobileCenter.Start("e7c53299-f7ce-4aa2-85cd-082b383de045", typeof(Analytics), typeof(Crashes));
#endregion

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                //this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;
            
            // 창에 콘텐츠가 이미 있는 경우 앱 초기화를 반복하지 말고,
            // 창이 활성화되어 있는지 확인하십시오.
            if (rootFrame == null)
            {
                //MVVM 초기화
                GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();
                FontHelper.FontDAO = SimpleIoc.Default.GetInstance<FontDAO>();

                // 탐색 컨텍스트로 사용할 프레임을 만들고 첫 페이지로 이동합니다.
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: 이전에 일시 중지된 응용 프로그램에서 상태를 로드합니다.
                }

                // 현재 창에 프레임 넣기
                Window.Current.Content = rootFrame; 
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    //이전에 풀스크린으로 끝났었다면 강제로 종료시킴
                    if (IsMobile || UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Touch)
                    {
                        var cv = ApplicationView.GetForCurrentView();
                        if (cv.IsFullScreenMode)
                        {
                            cv.ExitFullScreenMode();
                        }
                    }

                    // 탐색 스택이 복원되지 않으면 첫 번째 페이지로 돌아가고
                    // 필요한 정보를 탐색 매개 변수로 전달하여 새 페이지를
                    // 구성합니다.
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // 현재 창이 활성 창인지 확인
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// 특정 페이지 탐색에 실패한 경우 호출됨
        /// </summary>
        /// <param name="sender">탐색에 실패한 프레임</param>
        /// <param name="e">탐색 실패에 대한 정보</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// 응용 프로그램 실행이 일시 중단된 경우 호출됩니다.  응용 프로그램이 종료될지
        /// 또는 메모리 콘텐츠를 변경하지 않고 다시 시작할지 여부를 결정하지 않은 채
        /// 응용 프로그램 상태가 저장됩니다.
        /// </summary>
        /// <param name="sender">일시 중단 요청의 소스입니다.</param>
        /// <param name="e">일시 중단 요청에 대한 세부 정보입니다.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 응용 프로그램 상태를 저장하고 백그라운드 작업을 모두 중지합니다.
            deferral.Complete();
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // 창에 콘텐츠가 이미 있는 경우 앱 초기화를 반복하지 말고,
            // 창이 활성화되어 있는지 확인하십시오.
            if (rootFrame == null)
            {
                //MVVM 초기화
                GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();
                FontHelper.FontDAO = SimpleIoc.Default.GetInstance<FontDAO>();

                // 탐색 컨텍스트로 사용할 프레임을 만들고 첫 페이지로 이동합니다.
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: 이전에 일시 중지된 응용 프로그램에서 상태를 로드합니다.
                }
                
                // 현재 창에 프레임 넣기
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                if (IsMobile || UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Touch)
                {
                    //이전에 풀스크린으로 끝났었다면 강제로 종료시킴
                    var cv = ApplicationView.GetForCurrentView();
                    if (cv.IsFullScreenMode)
                    {
                        cv.ExitFullScreenMode();
                    }
                }
                // 탐색 스택이 복원되지 않으면 첫 번째 페이지로 돌아가고
                // 필요한 정보를 탐색 매개 변수로 전달하여 새 페이지를
                // 구성합니다.
                rootFrame.Navigate(typeof(MainPage));
            }
            // 현재 창이 활성 창인지 확인
            Window.Current.Activate();
            
            //메세지 전달
            Messenger.Default.Send(new Message(args.Files), "FileAssociation");

        }
    }
}
