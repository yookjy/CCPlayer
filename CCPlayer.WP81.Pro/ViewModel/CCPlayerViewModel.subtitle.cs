//using CCPlayer.HWCodecs.Matroska.MKV;
using CCPlayer.HWCodecs.Text.Models;
using CCPlayer.HWCodecs.Text.Parsers;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Strings;
using FFmpegSupport;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Lime.Encoding;
using Lime.Helpers;
using Lime.Models;
using Lime.Xaml.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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
    public partial class CCPlayerViewModel : ViewModelBase, IFileOpenPickerContinuable
    {
        private void OnOpenSubtitlePicker()
        {
            //재생 정지
            Me.Pause();

            FileOpenPicker picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };

            foreach (var suffix in CCPlayerConstant.SUBTITLE_FILE_SUFFIX)
            {
                picker.FileTypeFilter.Add(suffix);
            }

            picker.ContinuationData.Add(ContinuationManager.SOURCE_VIEW_MODEL_TYPE_FULL_NAME, this.GetType().FullName);
            picker.PickSingleFileAndContinue();
        }

        private void ClearSubtitles()
        {
            MessengerInstance.Send(new Message("ClearSubtitles"), TransportControlViewModel.NAME);
        }

        private async void LoadSubtitles(MediaInfo mediaInfo)
        {
            //자막 표시 데이터 초기화
            SubtitleText = string.Empty;

            try
            {
                if (mediaInfo.SubtitleFileList != null)
                {
                    await ThreadPool.RunAsync(handler =>
                    {
                        foreach (var sii in mediaInfo.SubtitleFileList)
                        {
                            LoadSubtitle(sii);
                        }
                    });

                }
                //기본 자막 스타일 로드
                LoadSubtitleStylesBySetting();
            }
            catch (Exception) { }
        }

        private byte SortOrder(SubtitleTypes subtitleType)
        {
            byte order = 0;
            switch (subtitleType)
            {
                case SubtitleTypes.SMI:
                    order = 1;
                    break;
                case SubtitleTypes.SRT:
                    order = 2;
                    break;
                case SubtitleTypes.ASS:
                case SubtitleTypes.SSA:
                    order = 3;
                    break;
                case SubtitleTypes.NA:
                    order = 4;
                    break;
            }
            return order;
        }

        async void LoadSubtitle(StorageFile file, Action<List<PickerItem<string, string>>> action)
        {
            if (file != null)
            {
                //var stream = (file.OpenReadAsync().AsTask().Result).AsStream();
                var randomStream = await file.OpenReadAsync();
                if (randomStream != null)
                {
                    var stream = randomStream.AsStream();
                    var parser = SubtitleParserFactory.CreateParser(file.Name);

                    if (parser != null)
                    {
                        try
                        {
                            Subtitle subtitle = parser.Parse(stream, Settings.Subtitle.DefaultCharset);

                            //인코딩은 되었으나, 데이터를 인식할 수 없어 파싱 결과를 담을 수 없는 경우 초기화
                            if (subtitle != null && subtitle.EncodingResult == SubtitleEncodingResult.Success && subtitle.Languages.Count == 0)
                            {
                                Settings.Subtitle.DefaultCharset = CodePage.AUTO_DETECT_VALUE;
                                subtitle = parser.Parse(stream, Settings.Subtitle.DefaultCharset);
                            }

                            var subtitleLanguageList = new List<PickerItem<string, string>>();
                            foreach (var lang in subtitle.Languages.Select(x => new PickerItem<string, string> { Key = x.LanguageClassName, Name = x.LanguageName, Payload = subtitle, Payload2 = SortOrder(subtitle.SubtitleType) }))
                            {
                                //System.Diagnostics.Debug.WriteLine(lang.Name + " - " + subtitle.Title);
                                string langName = lang.Name;
                                if (!string.IsNullOrEmpty(subtitle.Title))
                                {
                                    lang.Name = string.Concat<string>(new string[] { langName, " - ", subtitle.Title });
                                }

                                subtitleLanguageList.Add(lang);
                            };

                            //자막 인코딩 에러 표시
                            if (subtitleLanguageList.Count > 0 && subtitleLanguageList.Any(x => x.Payload is Subtitle && (x.Payload as Subtitle).EncodingResult != SubtitleEncodingResult.Success))
                            {
                                DispatcherHelper.CheckBeginInvokeOnUI(async() =>
                                {
                                    if (App.ContentDlgOp != null) return;

                                    var contentPanel = new StackPanel();
                                    contentPanel.Children.Add(
                                        new TextBlock()
                                        {
                                            Text = ResourceLoader.GetForCurrentView().GetString("Message/Info/Encoding/Fail"),
                                            TextWrapping = TextWrapping.Wrap,
                                            Margin = new Thickness(0, 12, 0, 12)
                                        });

                                    MessageDialog contentDlg = new MessageDialog(ResourceLoader.GetForCurrentView().GetString("Message/Info/Encoding/Fail"));
                                    contentDlg.CancelCommandIndex = 0;
                                    
                                    //메세지 창 출력
                                    await contentDlg.ShowAsync();

                                    MessengerInstance.Send<Message>(new Message("SettingsOpened", null), TransportControlViewModel.NAME);
                                });
                            }

                            //리스트를 전달
                            action.Invoke(subtitleLanguageList);
                            //MessengerInstance.Send<Message>(new Message("SubtitlesLoaded", subtitleLanguageList), TransportControlViewModel.NAME);
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine(e.StackTrace);
                        }
                    }
                }
            }
        }

        void LoadSubtitleManually(StorageFile file)
        {
            LoadSubtitle(file, (subtitleLanguageList) =>
            {
                //리스트를 전달
                MessengerInstance.Send<Message>(new Message("SubtitlesManuallyLoaded", subtitleLanguageList), TransportControlViewModel.NAME);
            });
        }

        async void LoadSubtitle(SubtitleInfo subtitleInfo)
        {
            //자막 파일의 인코딩 타입 추출하여 설정
            var file = await subtitleInfo.GetStorageFile(true);

            LoadSubtitle(file, (subtitleLanguageList) =>
            {
                //리스트를 전달
                MessengerInstance.Send<Message>(new Message("SubtitlesLoaded", subtitleLanguageList), TransportControlViewModel.NAME);
            });
        }

        private void OnSubtitleChanged(string key, Subtitle subtitle)
        {
            //마커 모두 삭제
            Me.Markers.Clear();
            //현재 자막 데이터 초기화
            SubtitleText = string.Empty;

            //자막 스타일 로딩
            if (subtitle.Styles != null && subtitle.Styles.Count > 0)
            {
                if (subtitle.SubtitleType == SubtitleTypes.SMI)
                {
                    //smi 자막의 경우 첫번째 스타일을 로드
                    LoadSubtitleStyles(subtitle.Styles.FirstOrDefault().Value);
                }
            }

            if (subtitle.SubtitleFileKind == SubtitleFileKind.Internal)
            {
                int subtitleIndex = 0;
                Int32.TryParse(key, out subtitleIndex);
                ffmpegSubtitleSupport.SubtitleIndex = subtitleIndex;
                ffmpegSubtitleSupport.CodePage = subtitle.CurrentCodePage;
            }
            else
            {
                if (subtitle.SubtitleContents != null
                    && subtitle.SubtitleContents.ContainsKey(key)
                    && subtitle.SubtitleContents[key].Count > 0)
                {
                    //외부자막 전체 추가
                    foreach (var content in subtitle.SubtitleContents[key])
                    {
                        Me.Markers.Add(new TimelineMarker
                        {
                            Type = subtitle.SubtitleType.ToString(),
                            Time = TimeSpan.FromMilliseconds(content.Key),
                            Text = content.Value.Text.ToString()
                        });
                    }
                }
            }
        }

        private void LoadSubtitleStylesBySetting()
        {
            //이전에 사용한 NoOverriding value초기화
            Settings.Subtitle.ResetNoOverridingValues();
        }

        private void LoadSubtitleStyles(List<KeyValuePair<string, string>> styleAttributes)
        {
            if (styleAttributes != null && styleAttributes.Count > 0)
            {
                //font-family: Arial; font-weight: normal; color: white; background-color: black; text-align: center; 
                //margin-left:2pt; margin-right:2pt; margin-bottom:1pt; margin-top:1pt;   text-align:center; font-size:20pt; font-family:Arial, Sans-serif;   font-weight:bold; color:white;
                foreach (KeyValuePair<string, string> style in styleAttributes)
                {
                    string key = style.Key.ToLower().Trim();
                    string value = style.Value.ToLower().Trim();
                    switch (key)
                    {
                        case "font-family":
                            //무시
                            break;
                        case "font-size":
                            //무시
                            break;
                        case "font-weight":
                            if (!Settings.Subtitle.FontStyleOverride)
                            {
                                ushort weight = 0;
                                switch (value)
                                {
                                    case "thin":
                                    case "100":
                                        weight = FontWeights.Thin.Weight;
                                        break;
                                    case "extralight":
                                    case "200":
                                        weight = FontWeights.ExtraLight.Weight;
                                        break;
                                    case "light":
                                    case "300":
                                        weight = FontWeights.Light.Weight;
                                        break;
                                    case "lighter":
                                    case "normal":
                                    case "400":
                                        weight = FontWeights.Normal.Weight;
                                        break;
                                    case "medium":
                                    case "500":
                                        weight = FontWeights.Medium.Weight;
                                        break;
                                    case "bolder":
                                    case "semibold":
                                    case "600":
                                        weight = FontWeights.SemiBold.Weight;
                                        break;
                                    case "bold":
                                    case "700":
                                        weight = FontWeights.Bold.Weight;
                                        break;
                                    case "extrabold":
                                    case "800":
                                        weight = FontWeights.ExtraBold.Weight;
                                        break;
                                    case "black":
                                    case "900":
                                        weight = FontWeights.Black.Weight;
                                        break;
                                    case "extrablack":
                                    case "950":
                                        weight = FontWeights.ExtraBlack.Weight;
                                        break;
                                    default:
                                        Settings.Subtitle.NotOverridingFontWeight = weight;
                                        break;
                                }
                                Settings.Subtitle.NotOverridingFontWeight = weight;
                            }
                            break;
                        case "font-style":
                            if (!Settings.Subtitle.FontStyleOverride)
                            {
                                string fontStyle = string.Empty;
                                switch (value)
                                {
                                    case "italic":
                                        fontStyle = FontStyle.Italic.ToString();
                                        break;
                                    case "oblique":
                                        fontStyle = FontStyle.Oblique.ToString();
                                        break;
                                    default:
                                        fontStyle = FontStyle.Normal.ToString();
                                        break;
                                }
                                Settings.Subtitle.NotOverridingFontStyle = fontStyle;
                            }
                            break;
                        case "color":
                            {
                                uint colorCode = GetColorCode(value);
                                if (colorCode > 0)
                                {
                                    Settings.Subtitle.NotOverridingForegroundColor = Lime.Models.ColorItem.ConvertColor(colorCode);
                                }
                            }
                            break;
                        case "background-color":
                            {
                                uint colorCode = GetColorCode(value);
                                if (colorCode > 0)
                                {
                                    var brush = new SolidColorBrush(Lime.Models.ColorItem.ConvertColor(colorCode));
                                    Settings.Subtitle.Background = brush;
                                }
                            }
                            break;
                        case "text-align":
                            switch (value)
                            {
                                case "left":
                                    Settings.Subtitle.SubtitleTextAlignment = TextAlignment.Left;
                                    break;
                                case "right":
                                    Settings.Subtitle.SubtitleTextAlignment = TextAlignment.Right;
                                    break;
                                case "center":
                                    Settings.Subtitle.SubtitleTextAlignment = TextAlignment.Center;
                                    break;
                                case "justify":
                                    Settings.Subtitle.SubtitleTextAlignment = TextAlignment.Justify;
                                    break;
                                default:
                                    Settings.Subtitle.SubtitleTextAlignment = TextAlignment.Center;
                                    break;
                            }
                            break;
                        case "margin-top":
                        case "margin-right":
                        case "margin-bottom":
                        case "margin-left":
                        case "margin":
                            //무시
                            /*
                            (위쪽)	marginTop 속성의 유효한 범위의 마진 너비의 값이다.
                            (오른쪽)	marginRight 속성의 유효한 범위의 마진 너비의 값이다.
                            (아래쪽)	marginBottom 속성의 유효한 범위의 마진 너비의 값이다.
                            (왼쪽)	marginLeft 속성의 유효한 범위의 마진 너비의 값이다.
                             * */
                            break;
                    }
                }
            }
        }

        private uint GetColorCode(string value)
        {
            string colValue = value;
            //먼저 이름에서 검색
            string colorCode = Lime.Models.ColorItem.GetWebColorCode(colValue);
            //이름이 검색되지 않은 경우
            if (string.IsNullOrEmpty(colorCode) && !string.IsNullOrEmpty(colValue))
            {
                colValue = HtmlFontHelper.GetColorCode(colValue);
            }
            else if (!string.IsNullOrEmpty(colorCode))
            {
                colValue = colorCode;
            }

            uint colCode = 0;

            if (!string.IsNullOrEmpty(colValue))
            {
                try
                {
                    colCode = Convert.ToUInt32(colValue.Replace("#", string.Empty), 16);
                }
                catch (Exception) { }

            }
            return colCode;
        }

        private void OnChangeSubtitleSync(double newValue, double oldValue)
        {
            if (Me.Markers.Count > 0)
            {
                double value = newValue - oldValue;
                //System.Diagnostics.Debug.WriteLine("가감치 : "  + value);
                foreach (var marker in Me.Markers)
                {
                    marker.Time = TimeSpan.FromMilliseconds(marker.Time.TotalMilliseconds + value * 1000);
                }
            }
        }

        private void OnSubtitleCharsetChanged(string key, Subtitle subtitle)
        {
            //외부 자막
            if (subtitle.Source != null)
            {
                subtitle = subtitle.Parser.Parse(subtitle.Source, subtitle.CurrentCodePage);
            }
            OnSubtitleChanged(key, subtitle);
        }

        Subtitle CreateSubtitle(SubtitleInfomation subtitleInfomation)
        {

            if (subtitleInfomation.type != SubtitlePacketType.SUBTITLE_ASS 
                && subtitleInfomation.type != SubtitlePacketType.SUBTITLE_TEXT)
            {
                return null;
            }

            Subtitle subtitle = null;
            string nameFormat = "[MKV] {0}";

            subtitle = new Subtitle()
            {
                SubtitleFileKind = SubtitleFileKind.Internal
            };
            subtitle.AddLanguage(subtitleInfomation.language + ":" + subtitleInfomation.streamIndex, new List<KeyValuePair<string, string>>());

            if (subtitleInfomation.type == SubtitlePacketType.SUBTITLE_ASS)
            {
                //ASS의 경우 스타일 로딩
                AssParser assParser = new AssParser();
                assParser.LoadHeader(subtitleInfomation.header);

                subtitle.Parser = assParser;
                subtitle.Title = string.Format(nameFormat, GetSubtitleName(subtitleInfomation));
            }
            else if (subtitleInfomation.type == SubtitlePacketType.SUBTITLE_TEXT)
            {
                subtitle.Parser = new SrtParser();
                subtitle.Title = string.Format(nameFormat, GetSubtitleName(subtitleInfomation));
            }

            return subtitle;
        }

        void ffmpegSubtitleSupport_SubtitleFoundEvent(SubtitleSupport sender, SubtitleInfomation subtitleInfomation)
        {
            Subtitle subtitle = CreateSubtitle(subtitleInfomation); ;
            if (subtitle != null)
            {
                List<PickerItem<string, string>> list = new List<PickerItem<string, string>>();
                list.Add(new PickerItem<string, string>
                {
                    Key = subtitleInfomation.streamIndex.ToString(),
                    Name = subtitle.Title,
                    Payload = subtitle,
                    Payload2 = (byte)4
                });

                //재생패널 콤보에 추가
                MessengerInstance.Send<Message>(new Message("SubtitlesLoaded", list), TransportControlViewModel.NAME);
            }
        }

        void ffmpegSubtitleSupport_SubtitleUpdatedEvent(SubtitleSupport sender, SubtitleInfomation subtitleInfomation)
        {
            Subtitle subtitle = CreateSubtitle(subtitleInfomation); ;
            if (subtitle != null)
            {
                PickerItem<string, string> item = new PickerItem<string, string>
                {
                    Key = subtitleInfomation.streamIndex.ToString(),
                    Name = subtitle.Title,
                    Payload = subtitle,
                    Payload2 = (byte)4
                };

                //재생패널 콤보 변경
                MessengerInstance.Send<Message>(new Message("SubtitleUpdated", item), TransportControlViewModel.NAME);
            }
        }

        private string GetSubtitleName(SubtitleInfomation subtitleInfomation)
        {
            string nativeName = string.Empty;
            string engName = string.Empty;

            if (!string.IsNullOrEmpty(subtitleInfomation.language))
            {
                System.Globalization.CultureInfo cultureInfo = null;

                try
                {
                    if (subtitleInfomation.language.Length == 2 || (subtitleInfomation.language.Length == 5 && subtitleInfomation.language.ElementAt(2) == '-'))
                    {
                        cultureInfo = new System.Globalization.CultureInfo(subtitleInfomation.language);
                    }
                    else if (subtitleInfomation.language.Length == 3)
                    {
                        cultureInfo = LanguageCodeHelper.LangCodeToCultureInfo(subtitleInfomation.language);
                    }

                    if (cultureInfo != null)
                    {
                        nativeName = cultureInfo.NativeName;
                        engName = cultureInfo.EnglishName;
                    }
                }
                catch (Exception) { }
            }

            if (!string.IsNullOrEmpty(subtitleInfomation.title))
            {
                if (string.IsNullOrEmpty(nativeName))
                {
                    nativeName = subtitleInfomation.title;
                }
                else
                {
                    string[] titles = subtitleInfomation.title.Split(new char[] { ':' });

                    if (titles.Length >= 1)
                    {
                        nativeName += " : ";
                        if (titles[0] != engName && titles[0] != nativeName)
                        {
                            nativeName += subtitleInfomation.title;
                        }
                        else if (titles.Length >= 2 && !string.IsNullOrEmpty(titles[1]))
                        {
                            nativeName += titles[1];
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(nativeName))
            {
                nativeName = LanguageCodeHelper.UNKOWN_LANGUAGE;
            }

            return nativeName;
        }

        void ffmpegSubtitleSupport_SubtitlePopulatedEvent(SubtitleSupport sender, SubtitlePacket subtitlePacket)
        {
            MessengerInstance.Send<Message>(new Message("SubtitleInFFmpeg", subtitlePacket), TransportControlViewModel.NAME);
        }

        async void ffmpegAttachmentSupport_AttachmentFoundEvent(AttachmentSupport sender, Attachment attachment)
        {
            //폰트 저장으로 설정되어 있는 경우 폰트를 앱내에 저장
            if (Settings.General.UseSaveFontInMkv)
            {
                if (attachment.MimeType == "application/x-truetype-font")
                {
                    List<KeyValuePair<string, byte[]>> fonts = new List<KeyValuePair<string, byte[]>>();
                    fonts.Add(new KeyValuePair<string, byte[]>(attachment.FileName, attachment.BinaryData));

                    if (fonts != null && fonts.Any())
                    {
                        var resource = ResourceLoader.GetForCurrentView();
                        ShowLoadingBar(string.Format(resource.GetString("Loading"), resource.GetString("FontFamily/Header/Text")) + " " + attachment.FileName);
                        //System.Diagnostics.Debug.WriteLine(attachment.FileName);
                        //await ThreadPool.RunAsync(async handler =>
                        //{
                            await FontHelper.InstallFont(fonts, false);
                        //});
                    }
                }
                //단순 호출 횟수만 카운트
                cntAttachment++;
            }
        }

        void ffmpegAttachmentSupport_AttachmentCompletedEvent(AttachmentSupport sender, UInt32 cbAttachment)
        {
            FontHelper.FireFontFamilyListChanged(cbAttachment);
        }
    }
}