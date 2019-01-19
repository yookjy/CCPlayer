using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.ViewModel;
using CCPlayer.WP81.Views;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Lime.Helpers;
using Lime.Models;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// 새 응용 프로그램 템플릿에 대한 설명은 http://go.microsoft.com/fwlink/?LinkId=391641에 나와 있습니다.

namespace CCPlayer.WP81
{
    /// <summary>
    /// 기본 응용 프로그램 클래스를 보완하는 응용 프로그램별 동작을 제공합니다.
    /// </summary>
    public sealed partial class App : Application
    {
        //FeatureLevel 0 광고有/기능無
        //FeatureLevel 1 광고無/기능無 : Light
        //FeatureLevel 2 광고有/기능有 : Basic
        //FeatureLevel 3 광고無/기능有 : Unlimited
        //FeatureLevel 10 베타     : Omega
        //FeatureLevel 20 프로     : Pro
        public static ushort FeatureLevel = 0;
        public static IAsyncOperation<ContentDialogResult> ContentDlgOp { get; set; }
        private TransitionCollection transitions;
        private ContinuationManager continuationManager;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
        }

        private async Task<Frame> CreateRootFrameAsync(ApplicationExecutionState previousExecutionState, object arguments = null)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                //MVVM 초기화
                GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();

                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                //Associate the frame with a SuspensionManager key                                
                SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                await this.RestoreStatusAsync(previousExecutionState);

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            return rootFrame;
        }

        private async Task RestoreStatusAsync(ApplicationExecutionState previousExecutionState)
        {
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (previousExecutionState == ApplicationExecutionState.Terminated)
            {
                // Restore the saved session state only when appropriate
                try
                {
                    await SuspensionManager.RestoreAsync();
                }
                catch (SuspensionManagerException)
                {
                    //Something went wrong restoring state.
                    //Assume there is no state and continue
                }
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            var rootFrame = await this.CreateRootFrameAsync(e.PreviousExecutionState, e.Arguments);

            // Ensure the current window is active
            Window.Current.Activate();

            //StatusBar 투명 설정
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
            statusBar.BackgroundOpacity = 0;
            var applicationView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
            applicationView.SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);

            //AdDuplex 설정
            AdDuplex.Universal.Controls.WinPhone.XAML.Tracking.AdDuplexTrackingSDK.StartTracking("dbcf38c1-550f-4c0c-9724-c66179682883");
            //

        }

        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await SuspensionManager.SaveAsync();
            deferral.Complete();
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            var rootFrame = await this.CreateRootFrameAsync(args.PreviousExecutionState);

            var continuationEventArgs = args as IContinuationActivatedEventArgs;
            if (continuationEventArgs != null)
            {
                // Call ContinuationManager to handle continuation activation.
                continuationManager = new ContinuationManager();
                continuationManager.Continue(continuationEventArgs);
            }

            Window.Current.Activate();
        }

        protected override async void OnFileActivated(FileActivatedEventArgs e)
        {
            //base.OnFileActivated(e);
            var rootFrame = await this.CreateRootFrameAsync(e.PreviousExecutionState);

            if (rootFrame.Content == null)
            {
                if (!rootFrame.Navigate(typeof(MainPage)))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();

            //메세지 전달
            Messenger.Default.Send(new Message("FileAssociation", e), PlaylistViewModel.NAME);
        }

        //        private Frame CreateRootFrame()
        //        {
        //            Frame rootFrame = Window.Current.Content as Frame;

        //            // Do not repeat app initialization when the Window already has content,
        //            // just ensure that the window is active
        //            if (rootFrame == null)
        //            {
        //                // Create a Frame to act as the navigation context and navigate to the first page
        //                rootFrame = new Frame();

        //                // Set the default language
        //                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
        //                rootFrame.NavigationFailed += OnNavigationFailed;

        //                //SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

        //                // Place the frame in the current Window
        //                Window.Current.Content = rootFrame;
        //            }

        //            return rootFrame;
        //        }

        //        private void RestoreStatus(ApplicationExecutionState previousExecutionState)
        //        {
        //            // Do not repeat app initialization when the Window already has content,
        //            // just ensure that the window is active
        //            if (previousExecutionState == ApplicationExecutionState.Terminated)
        //            {
        //                // Restore the saved session state only when appropriate
        //                //try
        //                //{
        //                //    await SuspensionManager.RestoreAsync();
        //                //}
        //                //catch (SuspensionManagerException)
        //                //{
        //                //    //Something went wrong restoring state.
        //                //    //Assume there is no state and continue
        //                //}
        //            }
        //        }

        //        /// <summary>
        //        /// Invoked when Navigation to a certain page fails
        //        /// </summary>
        //        /// <param name="sender">The Frame which failed navigation</param>
        //        /// <param name="e">Details about the navigation failure</param>
        //        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        //        {
        //            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        //        }

        //        private TransitionCollection transitions;

        //        /// <summary>
        //        /// Singleton 응용 프로그램 개체를 초기화합니다. 이것은 실행되는 작성 코드의 첫 번째
        //        /// 줄이며 따라서 main() 또는 WinMain()과 논리적으로 동일합니다.
        //        /// </summary>
        //        public App()
        //        {
        //            this.InitializeComponent();
        //            this.Suspending += this.OnSuspending;
        //        }

        //        /// <summary>
        //        /// 최종 사용자가 응용 프로그램을 정상적으로 시작할 때 호출됩니다. 다른 진입점은
        //        /// 특정 파일을 열거나, 검색 결과를 표시하는 등 응용 프로그램을 시작할 때
        //        /// 사용됩니다.
        //        /// </summary>
        //        /// <param name="e">시작 요청 및 프로세스에 대한 정보입니다.</param>
        //        protected override void OnLaunched(LaunchActivatedEventArgs e)
        //        {
        //#if DEBUG
        //            if (System.Diagnostics.Debugger.IsAttached)
        //            {
        //                this.DebugSettings.EnableFrameRateCounter = true;
        //            }
        //#endif

        //            Frame rootFrame = CreateRootFrame();

        //            //DB 커넥션 객체 생성
        //            Connection = new SQLitePCL.SQLiteConnection("ccplayer.db");
        //            //MVVM 초기화
        //            GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();


        //            if (rootFrame.Content == null)
        //            {
        //                // 시작을 위해 턴스타일 탐색을 제거합니다.
        //                if (rootFrame.ContentTransitions != null)
        //                {
        //                    this.transitions = new TransitionCollection();
        //                    foreach (var c in rootFrame.ContentTransitions)
        //                    {
        //                        this.transitions.Add(c);
        //                    }
        //                }

        //                rootFrame.ContentTransitions = null;
        //                rootFrame.Navigated += this.RootFrame_FirstNavigated;

        //                // 탐색 스택이 복원되지 않으면 첫 번째 페이지로 돌아가고
        //                // 필요한 정보를 탐색 매개 변수로 전달하여 새 페이지를
        //                // 구성합니다.
        //                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
        //                {
        //                    throw new Exception("Failed to create initial page");
        //                }
        //            }

        //            // 현재 창이 활성 창인지 확인
        //            Window.Current.Activate();
        //            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
        //            //statusBar.BackgroundColor = (App.Current.Resources["PhoneAccentBrush"] as SolidColorBrush).Color;
        //            statusBar.BackgroundOpacity = 0;
        //            var applicationView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
        //            applicationView.SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);

        //            //폰트 리스트 등록
        ////            CCPlayer.WP81.Helpers.FontHelper.SetAllFonts(InstalledFonts);
        //        }

        //        static List<Velostep.Models.PickerItem> _InstalledFonts;
        //        public static List<Velostep.Models.PickerItem> InstalledFonts
        //        {
        //            get
        //            {
        //                if (_InstalledFonts == null)
        //                {
        //                    _InstalledFonts = new List<Velostep.Models.PickerItem>();
        //                }
        //                return _InstalledFonts;
        //            }
        //        }

        //        /// <summary>
        //        /// 응용 프로그램이 시작된 후 콘텐츠 전환을 복구합니다.
        //        /// </summary>
        //        /// <param name="sender">처리가 연결된 개체입니다.</param>
        //        /// <param name="e">탐색 이벤트에 대한 정보입니다.</param>
        //        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        //        {
        //            var rootFrame = sender as Frame;
        //            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
        //            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        //        }

        //        /// <summary>
        //        /// 응용 프로그램 실행이 일시 중지된 경우 호출됩니다. 응용 프로그램이 종료될지
        //        /// 또는 메모리 콘텐츠를 변경하지 않고 다시 시작할지 여부를 결정하지 않은 채
        //        /// 응용 프로그램 상태가 저장됩니다.
        //        /// </summary>
        //        /// <param name="sender">일시 중지된 요청의 소스입니다.</param>
        //        /// <param name="e">일시 중지된 요청에 대한 세부 정보입니다.</param>
        //        private void OnSuspending(object sender, SuspendingEventArgs e)
        //        {
        //            var deferral = e.SuspendingOperation.GetDeferral();

        //            // TODO: 응용 프로그램 상태를 저장하고 백그라운드 작업을 모두 중지합니다.
        //            deferral.Complete();
        //        }

        //        public static EventHandler<IContinuationActivatedEventArgs> ContinuationEventArgsChanged;
        //        public static IContinuationActivatedEventArgs ContinuationActivatedEventArgs { get; private set; }

        //        protected override void OnActivated(IActivatedEventArgs e)
        //        {
        //            ContinuationActivatedEventArgs = e as IContinuationActivatedEventArgs;
        //            if (ContinuationEventArgsChanged != null)
        //            {
        //                ContinuationEventArgsChanged(this, ContinuationActivatedEventArgs);
        //            }
        //        }


        //        protected override void OnFileActivated(FileActivatedEventArgs e)
        //        {
        //            //base.OnFileActivated(args);
        //            //await new MessageDialog((args.Files[0] as Windows.Storage.StorageFile).Path).ShowAsync();
        //            Frame rootFrame = CreateRootFrame();
        //            RestoreStatus(e.PreviousExecutionState);

        //            if (rootFrame.Content == null)
        //            {
        //                if (!rootFrame.Navigate(typeof(MainPage)))
        //                {
        //                    throw new Exception("Failed to create initial page");
        //                }

        //            }

        //            var p = rootFrame.Content as MainPage;
        ////            p.FileEvent = e;
        ////            p.NavigateToFilePage();

        //            // Ensure the current window is active
        //            Window.Current.Activate();

        //        }

        ///상태 mvvm !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        /////
        //  http://obelisk.codeplex.com/
        ///////////////////////////////////////////////////////////////////////////////////////////
    }
}