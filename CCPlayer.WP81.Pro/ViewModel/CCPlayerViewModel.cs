using CCPlayer.UI.Xaml.Controls.WP81;
using CCPlayer.HWCodecs.Text.Models;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Lime.Models;
using Lime.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Media.Core;
using Windows.Phone.UI.Input;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.System.Display;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using CCPlayer.HWCodecs.Text.Parsers;
using Lime.Common.Helpers;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using FFmpegSupport;
using System.Reflection;
using System.Globalization;
using Lime.Helpers;
using System.Text;

namespace CCPlayer.WP81.ViewModel
{
    internal class PagePreviousStatus
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsStatusBar { get; set; }
    }

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
    public partial class CCPlayerViewModel : ViewModelBase, IFileOpenPickerContinuable
    {
        public static readonly string NAME = typeof(CCPlayerViewModel).Name;
        private bool visibleLoadingBar;
        private bool supportedRotationLock;
        private bool isPausedByFlip;
        private Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation deviceInfo;
        private UInt32 cntAttachment;
        private MediaElementState previousState;
        private int tryAudioStreamIndex;

        #region 데이터 모델

        public Settings Settings { get; private set; }
        private FileDAO fileDAO;
        private SettingDAO settingDAO;
        private Windows.Media.MediaExtensionManager extMgr;

        private SimpleOrientationSensor orientationSenser;
        private PagePreviousStatus PagePreviousStatus;
        private MediaElementWrapper Me;
        private SubtitleSupport ffmpegSubtitleSupport;
        private AttachmentSupport ffmpegAttachmentSupport;

        private bool requestedMoveToPlaylist;

        private MediaInfo _CurrentMediaInfo;
        public MediaInfo CurrentMediaInfo
        {
            get
            {
                return _CurrentMediaInfo;
            }
            set
            {
                if (Set(ref _CurrentMediaInfo, value))
                {
                    MessengerInstance.Send<Message>(new Message("MediaFileChanged", value), TransportControlViewModel.NAME);
                }
            }
        }

        private bool _IsFullWindow;
        public bool IsFullWindow 
        { 
            get
            {
                return _IsFullWindow;
            }
            set
            {
                if (Set(ref _IsFullWindow, value))
                {
                    if (Me != null)
                    {
                        Me.IsFullWindow = value;
                        if (value)
                        {
                            // When displaying in full-window mode, center transport controls at the bottom of the window
                            //현재값 백업
                            PagePreviousStatus.Width = this.Width;
                            PagePreviousStatus.Height = this.Height;
                            //사이즈 최대화
                            var bounds = Window.Current.Bounds;
                            this.Width = bounds.Width;
                            this.Height = bounds.Height;

                            //상단 상태바의 호면 표시여부 저장
                            if (StatusBar.GetForCurrentView().OccludedRect.Width > 0 || StatusBar.GetForCurrentView().OccludedRect.Height > 0)
                            {
                                PagePreviousStatus.IsStatusBar = true;
                                DispatcherHelper.CheckBeginInvokeOnUI(async () => { await StatusBar.GetForCurrentView().HideAsync(); });
                            }
                        }
                        else
                        {
                            //사이즈 복원
                            this.Width = PagePreviousStatus.Width;
                            this.Height = PagePreviousStatus.Height;

                            //상단 상태바 복구
                            if (PagePreviousStatus.IsStatusBar)
                            {
                                DispatcherHelper.CheckBeginInvokeOnUI(async () =>
                                {
                                    try
                                    {
                                        await StatusBar.GetForCurrentView().ShowAsync();
                                    }
                                    catch (Exception) { }
                                });
                            }
                        }
                    }
                }
            }
        }

        private List<PickerItem<string, string>> _FontList;
        public List<PickerItem<string, string>> FontList
        {
            get
            {
                return _FontList;
            }
            set
            {
                Set(ref _FontList, value);
            }
        }

        private double _Width;
        public double Width
        {
            get
            {
                return _Width;
            }
            set
            {
                Set(ref _Width, value);
            }
        }

        private double _Height;
        public double Height
        {
            get
            {
                return _Height;
            }
            set
            {
                Set(ref _Height, value);
            }
        }

        //내부 사용 오픈 체크 프로퍼티
        private bool _IsPlayerOpened;
        public bool IsPlayerOpened
        {
            get
            {
                return _IsPlayerOpened;
            }
            set
            {
                Set(ref _IsPlayerOpened, value, true);
            }
        }

        private bool _IsSubtitleOn;
        public bool IsSubtitleOn
        {
            get
            {
                return _IsSubtitleOn;
            }
            set
            {
                Set(ref _IsSubtitleOn, value);
            }
        }

        private bool _IsSubtitleSettingsOn;
        public bool IsSubtitleSettingsOn
        {
            get
            {
                return _IsSubtitleSettingsOn;
            }
            set
            {
                Set(ref _IsSubtitleSettingsOn, value);
            }
        }

        private string _SubtitleText;
        public string SubtitleText
        {
            get
            {
                return _SubtitleText;
            }
            set
            {
                Set(ref _SubtitleText, value);
            }
        }

        private bool _IsSubtitleMoveOn;
        public bool IsSubtitleMoveOn
        {
            get
            {
                return _IsSubtitleMoveOn;
            }
            set
            {
                Set(ref _IsSubtitleMoveOn, value);
            }
        }

        #endregion

        #region 커맨드

        public ICommand LoadedCommand { get; private set; }
        public ICommand MediaOpenedCommand { get; private set; }
        public ICommand MediaEndedCommand { get; private set; }
        public ICommand MediaFailedCommand { get; private set; }
        public ICommand CurrentStateChangedCommand { get; private set;}
        public ICommand SeekCompletedCommand { get; private set; }
        public ICommand MarkerReachedCommand { get; private set; }
        public ICommand SubtitlePositionManipulationDeltaCommand { get; private set; }
        public ICommand SubtitlePositionManipulationCompletedCommand { get; private set; }
        public ICommand TappedCommand { get; private set; }

        #endregion

        #region 커맨드 핸들러
        
        private async void MediaOpenedCommandExecute(RoutedEventArgs args)
        {
//            System.Diagnostics.Debug.WriteLine("오픈");

            //무조건 로딩 패널 닫기
            HideLoadingBar();
            //전체 화면 모드 설정
            ShowPlayer();

            MessengerInstance.Send(new Message("MediaOpend", Me), TransportControlViewModel.NAME);
            
            //폰트 로드
            FontList = await FontHelper.GetAllFont();
            //자막 초기화
            SubtitleText = string.Empty;
        }

        private void MediaEndedCommandCommandExecute(RoutedEventArgs obj)
        {
            //종료된 경우 다음영상이 존재하면 다음영상 재생
            if (CurrentMediaInfo.NextMediaInfo != null)
            {
                if (!Settings.Playback.RemoveCompletedVideo)
                {
                    //마지막 위치 저장
                    SaveLastPosition();
                }
                else
                {
                    MessengerInstance.Send(new Message("RemovePlayList", CurrentMediaInfo), PlaylistViewModel.NAME);
                }
                //미디어 정지
                StopMedia();

                if (Settings.Playback.UseConfirmNextPlay)
                {
                    try
                    {
                        if (App.ContentDlgOp != null) return;
                        
                        ContentDialog contentDlg = new ContentDialog
                        {
                            Content = new TextBlock()
                            {
                                Text = ResourceLoader.GetForCurrentView().GetString("Message/Play/Next"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 12, 0, 12)
                            },
                            PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("Ok"),
                            SecondaryButtonText = ResourceLoader.GetForCurrentView().GetString("Cancel")
                        };

                        //메세지 창 출력
                        App.ContentDlgOp = contentDlg.ShowAsync();
                        App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>(async (op, status) =>
                        {
                            var result = await op;
                            if (result == ContentDialogResult.Primary)
                            {
                                //다음 재생 요청
                                MessengerInstance.Send(new Message("PlayItem", CurrentMediaInfo.NextMediaInfo), PlaylistViewModel.NAME);
                            }
                            else
                            {
                                //재생화면 닫기
                                HidePlayer();
                            }

                            App.ContentDlgOp = null;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
                else
                {
                    //다음 재생 요청
                    MessengerInstance.Send(new Message("NextPlay", CurrentMediaInfo.NextMediaInfo), TransportControlViewModel.NAME);              
                }
            }
            else
            {
                if (!Settings.Playback.RemoveCompletedVideo)
                {
                    //없으면 종료처리
                    SaveData();
                }
                else
                {
                    MessengerInstance.Send(new Message("RemovePlayList", CurrentMediaInfo), PlaylistViewModel.NAME);
                }
                //미디어 정지
                StopMedia();
                //재생화면 닫기
                HidePlayer();
            }
        }
       
        private async void MediaFailedCommandExecute(RoutedEventArgs e)
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            var errCode = 0;
            var errMsg = "99";
                
            if (e is ExceptionRoutedEventArgs)
            {
                //미디어 엘리먼트의 경우
                var exArgs = e as ExceptionRoutedEventArgs;
                if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_NOERROR")) errCode = 0;
                else if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_ABORTED")) errCode = 1;
                else if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_NETWORK")) errCode = 2;
                else if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_DECODE")) errCode = 3;
                else if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED")) errCode = 4;
                else if (exArgs.ErrorMessage.Contains("MF_MEDIA_ENGINE_ERR_ENCRYPTED")) errCode = 5;
            }
            else
            {
                //MF엘리먼트의 경우
                errCode = Me.MediaErrorCode;
            }

            //에러 코드가 존재하는 경우
            if (errCode >= 1 && errCode <= 5) 
            {
                errMsg = string.Format("0{0}", errCode);
            }
            //에러 메세지 생성
            errMsg = resource.GetString(string.Format("Message/Error/MFEngine{0}", errMsg));
            StackPanel contentPanel = new StackPanel();

            DecoderSupport decoderSupport = DecoderSupport.Instance();
            DecoderChangePayload payload = decoderSupport.Payload;
            //실패한 디코더 타입
            var reqDecoderType = payload.ReqDecoderType;
            
            if (errCode == 3)
            {
                if (payload.Status == DecoderChangeStatus.Succeeded
                    && Me.CurrentState == MediaElementState.Closed
                    && Me.Position.TotalMilliseconds == 0
                    && reqDecoderType == DecoderType.MIX
                    && payload.ResDecoderType == DecoderType.MIX)
                {
                    MessengerInstance.Send(new Message("DecoderChangingFailed", reqDecoderType), TransportControlViewModel.NAME);
                    //실패한 디코더를 목록에서 삭제
                    decoderSupport.DecoderTypes.Remove(reqDecoderType);
                    //Mix 모드로 성공적으로 오픈이 되었으나, 하드웨어 코덱에서 디코딩이 실패한경우 SW코덱으로 돌림
                    OpenSupportedMediaFile(CurrentMediaInfo, true, DecoderType.SW);
                    return;
                }
            }
            else if (errCode == 4)
            {
                if ((payload.Status == DecoderChangeStatus.CheckError) 
                    || (payload.Status == DecoderChangeStatus.Requested && reqDecoderType == DecoderType.HW && payload.ResDecoderType == DecoderType.HW))
                {
                    MessengerInstance.Send(new Message("DecoderChangingFailed", reqDecoderType), TransportControlViewModel.NAME);
                    //실패한 디코더를 목록에서 삭제
                    decoderSupport.DecoderTypes.Remove(reqDecoderType);
                    //HW코덱을 요청했으나 실패한 경우, MIX코덱 모드로 다시 오픈하고 에러 메세지 출력
                    OpenSupportedMediaFile(CurrentMediaInfo, true, DecoderType.MIX);
                    return;
                }
                else if (payload.Status == DecoderChangeStatus.Requested && reqDecoderType == DecoderType.AUTO && payload.ResDecoderType == DecoderType.AUTO)
                {
                    //바이트 스트림 핸들러에 해당 타입을 동록 시킨다.
                    OpenSupportedMediaFile(CurrentMediaInfo, true, DecoderType.AUTO, true);
                    return;
                }
                else if (payload.Status == DecoderChangeStatus.Succeeded && reqDecoderType != DecoderType.SW && payload.ResDecoderType != DecoderType.SW)
                {
                    MessengerInstance.Send(new Message("DecoderChangingFailed", reqDecoderType), TransportControlViewModel.NAME);
                    //실패한 디코더를 목록에서 삭제
                    decoderSupport.DecoderTypes.Remove(reqDecoderType);
                    //SW코덱이 아닌 코덱 모드로 오픈하여 실패한 경우, SW코덱 모드로 다시 오픈
                    OpenSupportedMediaFile(CurrentMediaInfo, true, DecoderType.SW);
                    return;
                }
                
                int audioCount = decoderSupport.StreamInformationList.Count(x => x.CodecType == 1); //AVMediaType::AVMEDIA_TYPE_AUDIO
                if (audioCount > 1 && tryAudioStreamIndex == -1)
                {
                    //에러 메세지 
                    var content = new TextBlock()
                    {
                        Text = resource.GetString("Message/Confirm/Source/SelectAudio"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 12, 0, 12)
                    };
                    contentPanel.Children.Add(content);

                    foreach (var streamInfo in decoderSupport.StreamInformationList)
                    {
                        if (streamInfo.CodecType == 1 && streamInfo.CodecId > 0)
                        {
                            CultureInfo cultureInfo = null;
                            string lang = streamInfo.Language;

                            if (!string.IsNullOrEmpty(lang))
                            {
                                if (lang.Length == 2 || (lang.Length == 5 && lang.ElementAt(2) == '-'))
                                {
                                    cultureInfo = new CultureInfo(lang);
                                }
                                else if (lang.Length == 3)
                                {
                                    cultureInfo = LanguageCodeHelper.LangCodeToCultureInfo(lang);
                                }

                                if (cultureInfo != null && !string.IsNullOrEmpty(cultureInfo.NativeName))
                                {
                                    streamInfo.Language = cultureInfo.NativeName;
                                }
                            }

                            StackPanel innerPanel = new StackPanel()
                            {
                                Orientation = Windows.UI.Xaml.Controls.Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Left
                            };

                            var current = new TextBlock()
                            {
                                Text = string.Format(" - {0} {1}", streamInfo.Language, streamInfo.Title),
                                FontSize = (double)App.Current.Resources["TextStyleMediumFontSize"],
                                HorizontalAlignment = HorizontalAlignment.Left
                            };
                            innerPanel.Children.Add(new RadioButton()
                            {
                                IsChecked = streamInfo.IsBestStream,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Content = current,
                                GroupName = "audio",
                                Tag = streamInfo
                            });
                            contentPanel.Children.Add(innerPanel);
                        }
                    }

                    //에러 다이얼로그 표시
                    MessengerInstance.Send(new Message("ShowSelectionAudioMessage", contentPanel), MainViewModel.NAME);
                    return;
                }
                else
                {
                    //포맷 에러인 경우 지원되는 코덱을 메세지 창에 출력
                    //에러 메세지 
                    var content = new TextBlock()
                    {
                        Text = errMsg,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 12, 0, 12)
                    };
                    //생성된 내용 패널에 추가
                    contentPanel.Children.Add(content);

                    foreach (var streamInfo in decoderSupport.StreamInformationList)
                    {
                        if (streamInfo.CodecId > 0)
                        {
                            //재시도된 오디오 스티림인 경우, 해당 오디오 스트림이 아니면 스킵 처리
                            if (tryAudioStreamIndex != -1 && streamInfo.CodecType == 1 && tryAudioStreamIndex != streamInfo.StreamId)
                            {
                                continue;
                            }

                            var current = new TextBlock()
                            {
                                Text = string.Format(" - {0} {1}", streamInfo.CodecLongName, string.IsNullOrEmpty(streamInfo.CodecProfileName) ? string.Empty : streamInfo.CodecProfileName) ,
                                FontSize = (double)App.Current.Resources["TextStyleMediumFontSize"]
                            };
                            contentPanel.Children.Add(current);
                            if (streamInfo.CodecType == 0) //비디오
                            {
                                var detail = new TextBlock()
                                {
                                    Text = string.Format("      {0} x {1} {2}Fps", streamInfo.Width, streamInfo.Height, streamInfo.Fps),
                                    FontSize = (double)App.Current.Resources["TextStyleMediumFontSize"]
                                };
                                contentPanel.Children.Add(detail);
                            }
                            else if (streamInfo.CodecType == 1) //오디오
                            {
                                var detail = new TextBlock()
                                {
                                    Text = string.Format("      {0}Hz  {1}Ch {2}Bit", streamInfo.SampleRate, streamInfo.Channels, streamInfo.Bps),
                                    FontSize = (double)App.Current.Resources["TextStyleMediumFontSize"],
                                    Margin = new Thickness(0, 0, 0, 12)
                                };
                                contentPanel.Children.Add(detail);
                            }
                        
                        }
                    }

                    //지원되는 코덱 링크
                    var hyperLink = new HyperlinkButton()
                    {
                        NavigateUri = new Uri("http://msdn.microsoft.com/library/windows/apps/ff462087(v=vs.105).aspx"),
                        Margin = new Thickness(6, 0, 0, 0)
                    };
                    var linkTitle = new TextBlock()
                    {
                        Margin = new Thickness(0, 12, 0, 0)
                    };
                    var underLine = new Underline();
                    var linkText = new Run()
                    {
                        Text = resource.GetString("Supported/Media/Format"),
                        FontWeight = FontWeights.Bold,
                        FontSize = (double)App.Current.Resources["TextStyleMediumFontSize"]
                    };
                    hyperLink.Content = linkTitle;
                    linkTitle.Inlines.Add(underLine);
                    underLine.Inlines.Add(linkText);
                    contentPanel.Children.Add(hyperLink);

                    await ThreadPool.RunAsync(handler =>
                    {
                        //리스트의 파일아이템에 에러 표시
                        var msg = new Message("ShowErrorFile", new KeyValuePair<string, MediaInfo>(ResourceLoader.GetForCurrentView().GetString("Message/Error/CodecNotSupported"), CurrentMediaInfo));
                        MessengerInstance.Send(msg, ExplorerViewModel.NAME);
                        MessengerInstance.Send(msg, AllVideoViewModel.NAME);
                        MessengerInstance.Send(msg, PlaylistViewModel.NAME);
                    });
                }
            }
            else if (App.ContentDlgOp == null)
            {
                //기타 다른 에러의 경우 메세지 출력
                contentPanel.Children.Add(new TextBlock
                {
                    Text = errMsg,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(6, 0, 0, 12),
                });
            }
            //에러 다이얼로그 표시
            MessengerInstance.Send(new Message("ShowErrorMessage", contentPanel), MainViewModel.NAME);
        }

        private void CurrentStateChangedCommandExecute(RoutedEventArgs obj)
        {
            switch (Me.CurrentState)
            {
                case MediaElementState.Opening:
                case MediaElementState.Closed:
                case MediaElementState.Stopped:
                    //자막 초기화
                    SubtitleText = string.Empty;
                    break;
            }

//            System.Diagnostics.Debug.WriteLine(Me.CurrentState);
            MessengerInstance.Send<Message>(new Message("CurrentStateChanged", Me.CurrentState), TransportControlViewModel.NAME);
        }

        private void SeekCompletedCommandExecute(RoutedEventArgs obj)
        {
            SubtitleText = string.Empty;
        }

        private void MarkerReachedCommandExecute(TimelineMarkerRoutedEventArgs args)
        {
            var transportVm = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<TransportControlViewModel>();
            if (transportVm != null)
            {
                if (transportVm.SubtitleCompensationData.ContainsKey(args.Marker.Time))
                {
                    var subList = transportVm.SubtitleCompensationData[args.Marker.Time];
                    transportVm.SubtitleCompensationData.Remove(args.Marker.Time);
                    if (subList.Count > 0)
                    {
                        var line = subList.Aggregate((x, y) => 
                        {
                            StringBuilder tmp = new StringBuilder();
                            
                            if (!string.IsNullOrEmpty(x))
                            {
                                tmp.Append(x.Trim());
                            }

                            if (!string.IsNullOrEmpty(y))
                            {
                                if (tmp.Length > 0)
                                {
                                    tmp.Append("<br/>");
                                }

                                tmp.Append(y.Trim());
                            }
                            //return x + "<br/>" + y;
                            return tmp.ToString();
                        });
                        //args.Marker.Text += "<br/>" + line;
                        
                        if (!string.IsNullOrEmpty(line))
                        {
                            args.Marker.Text += "<br/>" + line;
                        }
                    }
                }
            }
            
            if (SubtitleText != args.Marker.Text)
            {
                //System.Diagnostics.Debug.WriteLine(SubtitleText + "   |   " + args.Marker.Text + "|" + args.Marker.Time + " =====>" + Me.Position);
                SubtitleText = args.Marker.Text;
            }
        }

        private void LoadedCommandExecute(MediaElementWrapper me)
        {
            Me = me;

            var task = ThreadPool.RunAsync(async handler =>
            {
                //폰트 로드
                await DispatcherHelper.RunAsync(async () => { FontList = await FontHelper.GetAllFont(); });
            });
        }

        private void SubtitlePositionManipulationDeltaCommandExecute(ManipulationDeltaRoutedEventArgs args)
        {
            if (args.Container is Panel)
            {
                var child = args.Container as FrameworkElement;
                var panel = child.Parent as Panel;
                SetSubtitlePosition(panel, child, args.Delta.Translation.Y);
            }
        }

        private void SetSubtitlePosition(Panel parent, FrameworkElement child, double translationY)
        {
            //트랜스폼
            var childTransform = child.RenderTransform as CompositeTransform;
            //이동
            childTransform.TranslateY += translationY;
            //이동한 위치를 기준으로 위치차를 구함
            var rectDiff = child.TransformToVisual(parent).TransformPoint(new Point());
            //위치에 따른 처리
            if (rectDiff.Y < parent.ActualHeight / 2)
            {
                //윗쪽
                if (child.VerticalAlignment != VerticalAlignment.Top)
                {
                    childTransform.TranslateY = parent.ActualHeight + childTransform.TranslateY - child.ActualHeight;
                    Settings.Subtitle.VerticalAlignment = VerticalAlignment.Top.ToString();
                }
                //상단으로 넘어가면 상단에 붙힘
                if (rectDiff.Y < 0)
                {
                    childTransform.TranslateY = 0;
                }
            }
            else if (rectDiff.Y == parent.ActualHeight / 2)
            {
                //가운데
                Settings.Subtitle.VerticalAlignment = VerticalAlignment.Center.ToString();
                childTransform.TranslateY = 0;
            }
            else
            {
                //아랫쪽
                if (child.VerticalAlignment != VerticalAlignment.Bottom)
                {
                    childTransform.TranslateY = childTransform.TranslateY - parent.ActualHeight + child.ActualHeight;
                    Settings.Subtitle.VerticalAlignment = VerticalAlignment.Bottom.ToString();
                }
                //하단이 넘어가면 붙힘
                if (rectDiff.Y + child.ActualHeight > parent.ActualHeight)
                {
                    childTransform.TranslateY = 0;
                }
            }
            //내부 텍스트 객체도 수직정렬을 부모에 맞춘다. (그렇지 않으면 부모를 약간 꿈틀 거리며 위쪽/아랫쪽으로 붙는 현상이 생김)
            if (child is Panel)
            {
                var panel = child as Panel;
                var html = panel.Children.FirstOrDefault(x => x is HtmlTextBlock) as HtmlTextBlock;
                if (html != null)
                {
                    html.VerticalAlignment = child.VerticalAlignment;
                }
            }
        }

        private void SubtitlePositionManipulationCompletedCommandExecute(ManipulationCompletedRoutedEventArgs args)
        {
            if (args.Container is Panel)
            {
                var child = args.Container as FrameworkElement;
                var childTransform = child.RenderTransform as CompositeTransform;

                //DB업데이트
                settingDAO.Replace(Settings);
            }
        }

        private void TappedCommandExecute(RoutedEventArgs args)
        {
            if (IsSubtitleMoveOn && args.OriginalSource is Grid)
            {
                IsSubtitleMoveOn = false;
                MessengerInstance.Send(new Message("SubtitleMoved", true), TransportControlViewModel.NAME);
            }
        }

        #endregion

        public CCPlayerViewModel(FileDAO fileDAO, SettingDAO settingDAO, Windows.Media.MediaExtensionManager extMgr)
        {
            this.tryAudioStreamIndex = -1;
            this.fileDAO = fileDAO;
            this.settingDAO = settingDAO;
            this.Settings = settingDAO.SettingCache;

            //코덱 등록
            this.extMgr = extMgr;
            this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "");
            this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-matroska");
            this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/mp4");
            this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/avi");
            //신규 추가
            this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-ms-asf");
            //this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-ms-wmv");
            //          if (VersionHelper.WindowsVersion == 10)
            //            {
            //                this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-matroska");
            //            }

            //MFVideoFormat_FFmpeg_SW : {C1FC552A-B7B8-4DBB-8A93-5B918B2A082A}
            this.extMgr.RegisterVideoDecoder("FFmpegDecoder.FFmpegUncompressedVideoDecoder", Guid.Parse("{C1FC552A-B7B8-4DBB-8A93-5B918B2A082A}"), Guid.Empty);
            //MFAudioFormat_FFmpeg_SW : {6BAE7E7C-1560-4217-8636-71D18D67A9D2}
            this.extMgr.RegisterAudioDecoder("FFmpegDecoder.FFmpegUncompressedAudioDecoder", Guid.Parse("{6BAE7E7C-1560-4217-8636-71D18D67A9D2}"), Guid.Empty);

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();

            //화면 회전 이벤트 등록
            this.orientationSenser.OrientationChanged += orientationSenser_OrientationChanged;
            //ffmpeg자막 이벤트 등록
            this.ffmpegSubtitleSupport.SubtitleFoundEvent += ffmpegSubtitleSupport_SubtitleFoundEvent;
            this.ffmpegSubtitleSupport.SubtitleUpdatedEvent += ffmpegSubtitleSupport_SubtitleUpdatedEvent;
            this.ffmpegSubtitleSupport.SubtitlePopulatedEvent += ffmpegSubtitleSupport_SubtitlePopulatedEvent;
            //ffmpeg첨부 이벤트 등록
            this.ffmpegAttachmentSupport.AttachmentFoundEvent += ffmpegAttachmentSupport_AttachmentFoundEvent;
            this.ffmpegAttachmentSupport.AttachmentCompletedEvent += ffmpegAttachmentSupport_AttachmentCompletedEvent;
        }
                        
        private void CreateModels()
        {
            this.deviceInfo = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
            this.PagePreviousStatus = new PagePreviousStatus();
            this.orientationSenser = SimpleOrientationSensor.GetDefault();
            this.ffmpegSubtitleSupport = SubtitleSupport.Instance();
            //UI디스패처 설정
            this.ffmpegSubtitleSupport.SetUIDispatcher(DispatcherHelper.UIDispatcher);
            this.ffmpegAttachmentSupport = AttachmentSupport.Instance();
        }

        private void CreateCommands()
        {
            LoadedCommand = new RelayCommand<MediaElementWrapper>(LoadedCommandExecute);
            MediaOpenedCommand = new RelayCommand<RoutedEventArgs>(MediaOpenedCommandExecute);
            MediaEndedCommand = new RelayCommand<RoutedEventArgs>(MediaEndedCommandCommandExecute);
            MediaFailedCommand = new RelayCommand<RoutedEventArgs>(MediaFailedCommandExecute);
            CurrentStateChangedCommand = new RelayCommand<RoutedEventArgs>(CurrentStateChangedCommandExecute);
            SeekCompletedCommand = new RelayCommand<RoutedEventArgs>(SeekCompletedCommandExecute);
            MarkerReachedCommand = new RelayCommand<TimelineMarkerRoutedEventArgs>(MarkerReachedCommandExecute);
            SubtitlePositionManipulationDeltaCommand = new RelayCommand<ManipulationDeltaRoutedEventArgs>(SubtitlePositionManipulationDeltaCommandExecute);
            SubtitlePositionManipulationCompletedCommand = new RelayCommand<ManipulationCompletedRoutedEventArgs>(SubtitlePositionManipulationCompletedCommandExecute);
            TappedCommand = new RelayCommand<RoutedEventArgs>(TappedCommandExecute);
        }

        private void RegisterMessages()
        {
            MessengerInstance.Register<PropertyChangedMessage<double>>(this, msg =>
            {
                switch(msg.PropertyName)
                {
                    case "SubtitleSyncTime":
                        OnChangeSubtitleSync(msg.NewValue, msg.OldValue);
                        break;
                }
            });

            MessengerInstance.Register<PropertyChangedMessage<bool>>(this, msg =>
            {
                switch (msg.PropertyName)
                {
                    case "IsSubtitleOn":
                        IsSubtitleOn = msg.NewValue;
                        break;
                    case "LoadingPanelVisible":
                        visibleLoadingBar = msg.NewValue;
                        break;
                    case "SupportedRotationLock":
                        supportedRotationLock = msg.NewValue;
                        break;
                }
            });

            MessengerInstance.Register<Message>(this, NAME, (msg) => 
            {
                switch (msg.Key)
                {
                    case "Play":
                        OnPlay(msg.GetValue<MediaInfo>());
                        break;
                    case "MoveToPlaylistSection":
                        requestedMoveToPlaylist = msg.GetValue<bool>();
                        break;
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        if (IsSubtitleSettingsOn)
                        {
                            //셋팅 패널 숨김
                            IsSubtitleSettingsOn = false;
                            MessengerInstance.Send<Message>(new Message("SettingsOpened", null), TransportControlViewModel.NAME);
                        }
                        else
                        {
                            SaveData();
                            StopMedia();
                            HidePlayer();
                        }
                        break;
                    case "ExitPlay":
                        SaveData();
                        StopMedia();
                        HidePlayer();
                        break;
                    case "Activated":
                        OnActivated();
                        break;
                    case "Deactivated":
                        OnDeactivated();
                        break;
                    case "OpenSubtitlePicker":
                        OnOpenSubtitlePicker();
                        break;
                    case "MoveSubtitle":
                        //이동하기 준비 마킹
                        IsSubtitleMoveOn = true;
                        break;
                    case "SubtitleChanged":
                        var param = msg.GetValue<KeyValuePair<string, Subtitle>>();
                        //자막 선택 처리
                        OnSubtitleChanged(param.Key, param.Value);
                        break;
                    case "SubtitleCharsetChanged":
                        var subCharset = msg.GetValue<KeyValuePair<string, Subtitle>>();
                        OnSubtitleCharsetChanged(subCharset.Key, subCharset.Value);
                        break;
                    case "SubtitleSettingsTapped":
                        IsSubtitleSettingsOn = true;
                        break;
                    case "ApplyCurrentRotation":
                        OnOrientationChanged(msg.GetValue<SimpleOrientation>(), !Settings.Playback.IsRotationLock);
                        break;
                    case "ApplyForceRotation":
                        OnOrientationChanged(msg.GetValue<SimpleOrientation>(), true);
                        break;
                    case "DecoderChanging":
                        //모든 설정 저장
                        SaveData();
                        //마지막 위치 저장
                        SaveLastPosition();
                        //비디오 정지
                        StopMedia();
                        //디코더 타입
                        DecoderType decoderType = msg.GetValue<DecoderType>();
                        OpenSupportedMediaFile(CurrentMediaInfo, true, decoderType);
                        break;
                    case "MultiAudioSelecting":
                        //비디오 정지
                        StopMedia();
                        //디코더 타입
                        StreamInformation streamInfo = msg.GetValue<StreamInformation>();
                        tryAudioStreamIndex = streamInfo.StreamId;
                        OpenSupportedMediaFile(CurrentMediaInfo, true, DecoderType.MIX);
                        break;
                }
            });   
        }

        private void OnActivated()
        {
            if (previousState == MediaElementState.Playing)
            {
                //재생중 윈도우키를 눌러 나간뒤 돌아올때 버그 수정 (Work around)
                Me.Pause();
                Me.Position = Me.Position.Subtract(TimeSpan.FromSeconds(1));
                Me.Play();
            }
        }

        private void OnDeactivated()
        {
            if (Me.CurrentState == MediaElementState.Playing)
            {
                previousState = Me.CurrentState;
                Me.Pause();
            }

            SaveLastPosition();
            Me.Trim();
        }

        private void OnPlay(MediaInfo mii)
        {
            //File Association에서 넘어오는 경우 CCPlayerElement 생성전에 넘어오므로 객체 생성이 될때까지 루프를 돌며 대기
            if (Me == null)
            {
                ThreadPoolTimer.CreateTimer(handler =>
                {
                    OnPlay(mii);
                }, TimeSpan.FromMilliseconds(300));
            }
            else
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    //audio인덱스 초기화
                    tryAudioStreamIndex = -1;
                    MessengerInstance.Send(new Message("BeforeMediaOpen"), TransportControlViewModel.NAME);
                    OpenSupportedMediaFile(mii, true, DecoderType.AUTO);
                });
            }
        }


        /// <summary>
        /// 미디어 정지
        /// </summary>
        private void StopMedia()
        {
            if (Me.CurrentState != Windows.UI.Xaml.Media.MediaElementState.Stopped)
            {
                //먼저 재생시점을 저장하기 위해 멈춤
                Me.Pause();
            }

            //미디어 엘리먼트의 종료를 요청
            Me.Stop();
        }

        /// <summary>
        /// 데이터 저장
        /// </summary>
        private void SaveData()
        {
            //변경된 설정 데이터 DB반영
            if (settingDAO.SettingCache.Any(x => x.IsUpdated))
            {
                settingDAO.Replace(settingDAO.SettingCache);
            }
        }

        /// <summary>
        /// 재생목록 허브로 이동한다.
        /// </summary>
        private void MoveToPlaylist()
        {
            if (requestedMoveToPlaylist)
            {
                MessengerInstance.Send<Message>(new Message("MoveToPlaylistSection", null), MainViewModel.NAME);
            }
        }

        /// <summary>
        /// 미디어 플레이 화면을 표시한다.
        /// </summary>
        private void ShowPlayer()
        {
            if (!IsPlayerOpened)
            {
                IsFullWindow = true;
                IsPlayerOpened = true;
            }
        }

        /// <summary>
        /// 미디어 플레이어 화면을 숨긴다.
        /// </summary>
        private void HidePlayer()
        {
            if (IsPlayerOpened)
            {
                SubtitleText = string.Empty;
                IsPlayerOpened = false;
                IsFullWindow = false;
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                //미디어 엘리먼트 래퍼에 방향알림 (동기화 되지 않으므로 강제로 설정) => 나중에 동기화 되도록 고쳐야함.
                Orientation = DisplayOrientations.Portrait;
            }
        }

        /// <summary>
        /// 로딩창 표시
        /// </summary>
        /// <param name="msg"></param>
        private void ShowLoadingBar(string msg)
        {
            MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);
        }

        /// <summary>
        /// 로딩창 제거
        /// </summary>
        private void HideLoadingBar()
        {
            MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(string.Empty, false)), MainViewModel.NAME);
        }

        /// <summary>
        /// 마지막 재생지점을 저장한다.
        /// </summary>
        private void SaveLastPosition()
        {
            if (CurrentMediaInfo != null && Me != null)
            {
                CurrentMediaInfo.PausedTime = (long)Me.Position.TotalSeconds;
                MessengerInstance.Send<Message>(new Message("UpdatePausedTime", CurrentMediaInfo), PlaylistViewModel.NAME);
            }
        }
        
        #region Event Handler Method
        //미디어 엘리먼트 래퍼의 회전방향 바인딩 변수
        private DisplayOrientations _Orientation;
        public DisplayOrientations Orientation
        {
            get { return _Orientation; }
            set { Set(ref _Orientation, value); }
        }
        
        async void orientationSenser_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            await DispatcherHelper.RunAsync(() =>
            {
                var orientation = args.Orientation;
                OnOrientationChanged(orientation, !Settings.Playback.IsRotationLock);
            });
        }
        
        async void OnOrientationChanged(SimpleOrientation orientation, bool canRotate)
        {
            await DispatcherHelper.RunAsync(() =>
            {
                if (IsPlayerOpened)
                {
                    if (Settings.Playback.UseFlipToPause 
                        && (orientation == SimpleOrientation.Faceup || orientation == SimpleOrientation.Facedown))
                    {
                        //뒤집히면 정지
                        if (orientation == SimpleOrientation.Facedown && Me.CurrentState == MediaElementState.Playing)
                        {
                            MessengerInstance.Send(new Message("IsPaused", true), TransportControlViewModel.NAME);
                            isPausedByFlip = true;
                        }
                        else if (orientation == SimpleOrientation.Faceup && isPausedByFlip)
                        {
                            MessengerInstance.Send(new Message("IsPaused", false), TransportControlViewModel.NAME);
                            isPausedByFlip = false;
                        }
                        return;
                    }

                    if (canRotate)
                    {
                        switch (orientation)
                        {
                            case SimpleOrientation.NotRotated:
                                if (supportedRotationLock)
                                {
                                    if (DisplayInformation.GetForCurrentView().NativeOrientation == DisplayOrientations.Portrait)
                                    {
                                        DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                        SwapDimension(this.Width > this.Height);
                                    }
                                    else
                                    {
                                        DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
                                        SwapDimension(this.Width < this.Height);
                                    }
                                }
                                break;
                            case SimpleOrientation.Rotated180DegreesCounterclockwise:
                                if (supportedRotationLock)
                                {
                                    if (DisplayInformation.GetForCurrentView().NativeOrientation == DisplayOrientations.Portrait)
                                    {
                                        DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                        SwapDimension(this.Width > this.Height);
                                    }
                                    else
                                    {
                                        DisplayInformation.AutoRotationPreferences = DisplayOrientations.LandscapeFlipped;
                                        SwapDimension(this.Width < this.Height);
                                    }
                                }
                                break;
                            case SimpleOrientation.Rotated270DegreesCounterclockwise:
                                if (DisplayInformation.GetForCurrentView().NativeOrientation == DisplayOrientations.Portrait)
                                {
                                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.LandscapeFlipped;
                                    SwapDimension(this.Width < this.Height);
                                }
                                else
                                {
                                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                    SwapDimension(this.Width > this.Height);
                                }
                                break;
                            case SimpleOrientation.Rotated90DegreesCounterclockwise:
                                if (DisplayInformation.GetForCurrentView().NativeOrientation == DisplayOrientations.Portrait)
                                {
                                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
                                    SwapDimension(this.Width < this.Height);
                                }
                                else
                                {
                                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                                    SwapDimension(this.Width > this.Height);
                                }
                                break;
                        }

                        //미디어 엘리먼트 래퍼에 방향알림
                        Orientation = DisplayInformation.AutoRotationPreferences;
                        //컨트롤 패널에 알림
                        MessengerInstance.Send(new Message("OrientationChanged"), TransportControlViewModel.NAME);
                        //마지막 회전 상태 저장
                        if (Settings.Playback.LastPlaybackOrientation != orientation)
                        {
                            Settings.Playback.LastPlaybackOrientation = orientation;
                        }
                    }
                }
            });
        }

        private void SwapDimension(bool isSwap)
        {
            if (isSwap)
            {
                double tmp = this.Width;
                this.Width = this.Height;
                this.Height = tmp;
            }
        }

        #endregion

        public async void ContinueFileOpenPicker(Windows.ApplicationModel.Activation.FileOpenPickerContinuationEventArgs args)
        {
            // The "args" object contains information about selected file(s).
            if (args.Files.Any())
            {
                var file = args.Files[0];

                var defaultCodePage = Settings.Subtitle.DefaultCharset;;
                var stream = (await file.OpenReadAsync()).AsStream();
                var parser = SubtitleParserFactory.CreateParser(file.Name);

                //선택한 자막 로딩
                LoadSubtitleManually(file);

                //기본 자막 스타일 로드
                LoadSubtitleStylesBySetting();
            }

            Me.Play();
            Me.Position = Me.Position.Subtract(TimeSpan.FromMilliseconds(1000));
        }
        
        public async void OpenSupportedMediaFile(MediaInfo mi, bool autoPlay, DecoderType decoderType, bool registerContentType)
        {
            //자막 문자셋
            if (Settings.Subtitle.DefaultCharset == Lime.Encoding.CodePage.AUTO_DETECT_VALUE)
            {
                this.ffmpegSubtitleSupport.CodePage = Lime.Encoding.CodePage.UTF8_CODE_PAGE;
            }
            else
            {
                this.ffmpegSubtitleSupport.CodePage = Settings.Subtitle.DefaultCharset;
            }

            //미디어 엘리먼트 강제 사용 모드
            Me.ForceUseMediaElement = Settings.Playback.ForceUseMediaElement;
            //풀스크린 방지 모드
            Me.DisableFullScreenMediaElement = Settings.Playback.UseOptimizationEntryModel;
            //자막 목록 삭제
            Me.Markers.Clear();
            //화면에서 자막 제거
            SubtitleText = string.Empty;
            //재생할 파일정보 저장
            CurrentMediaInfo = mi;
            var file = await mi.GetStorageFile(false);
            //자막 초기화
            ClearSubtitles();
            //자막 로딩
            LoadSubtitles(mi);

            if (registerContentType)
            {
                this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", file.FileType, file.ContentType);
            }

            //this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "");
            //this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", file.ContentType);

            //if (decoderType == DecoderType.AUTO || decoderType == DecoderType.HW)
            //{
            //    //workaround : mp4의 경우 MF에서 익셉션을 터트려도 에러가 발생하지 않음
            //    //if (file.ContentType == "video/mp4" || file.ContentType == "video/3gpp2")
            //    if (!ignoreContentType && !string.IsNullOrEmpty(file.ContentType))
            //    {
            //        this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", file.ContentType);
            //    }

            //    //if (ignoreContentType)
            //    //{
            //    //    this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "--");
            //    //}
            //    //else
            //    {
            //        this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "");
            //    }

            //}
            //else
            //{
            //    this.extMgr.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", file.ContentType);
            //}

            //디코더 선택 요청
            DecoderSupport decoderSupport = DecoderSupport.Instance();
            decoderSupport.WindowsVersion = VersionHelper.WindowsVersion;
            decoderSupport.RequestDecoderChange(decoderType);
            decoderSupport.EnforceAudioStreamId = tryAudioStreamIndex;
            decoderSupport.UseGPUShader = Settings.Playback.UseGpuShader;

            //자막 저장 설정 여부
            this.ffmpegAttachmentSupport.IsSaveAttachment = Settings.General.UseSaveFontInMkv;
            //미디어 오픈
            var randomStream = await file.OpenReadAsync();
            Me.AutoPlay = true;
            Me.SetSource(randomStream, file.Path);
        }

        public void OpenSupportedMediaFile(MediaInfo mi, bool autoPlay, DecoderType decoderType)
        {
            OpenSupportedMediaFile(mi, autoPlay, decoderType, false);
        }
    }
}