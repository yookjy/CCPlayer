using CCPlayer.HWCodecs.Matroska.Codecs;
using CCPlayer.HWCodecs.Matroska.Common;
using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.MKV;
using CCPlayer.HWCodecs.Text.Models;
using CCPlayer.HWCodecs.Text.Parsers;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Velostep.Helpers;
using Velostep.Models;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
    public partial class CCPlayerViewModel
    {
        #region Matroska Video Stream Source
        
        async void OpenMkvFile(StorageFile file)
        {
            var randomStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            var doc = Document.ReadFromStream(randomStream.AsStream());

            //MKV 파일 소스 생성
            var mkvFs = new HWCodecs.Matroska.MKV.MKVFileSource(doc);

            //MKV이지만 재생할 수 있는가를 체크
            if (ValidateCodecForMkv(doc, mkvFs))
            {
                //미디어 타입 설정
                CurrentMediaInfo.MediaType = MediaType.MatroskaVideo;

                //폰트 저장으로 설정되어 있는 경우 폰트를 앱내에 저장
                if (Settings.General.UseSaveFontInMkv)
                {
                    if (doc.Segments != null && doc.Segments.Count > 0 && doc.Segments[0].Attachments != null)
                    {
                        var fonts = doc.Segments[0].Attachments.AttachedFile.Where(x => x.FileMimeType == "application/x-truetype-font").Select(x => new KeyValuePair<string, byte[]>(x.FileName, x.FileData));
                        if (fonts != null && fonts.Any())
                        {
                            var resource = ResourceLoader.GetForCurrentView();
                            ShowLoadingBar(string.Format(resource.GetString("Loading"), resource.GetString("FontFamily/Header/Text")));
                            await ThreadPool.RunAsync(async handler =>
                            {
                                await FontHelper.InstallFont(fonts);
                            });
                        }
                    }
                }

                //전역 변수로 저장 (자막 로드전 저장해야 자막을 로드 할 수 있음)
                mediaStreamFileSource = mkvFs;

                //오디오 목록 로드
                SetMkvAudioLanguageList(mkvFs);
                //자막 목록 로드
                SetMkvSubtitleLanguageList(mkvFs);

                //에러 클러스터 처리 이벤트
                mkvFs.SkipErrorClusterStarted += mkvFs_SkipErrorClusterStarted;
                mkvFs.SkipErrorClusterCompleted += mkvFs_SkipErrorClusterCompleted;

                //타이틀 추출
                if (!string.IsNullOrEmpty(mkvFs.Title))
                {
                    CurrentMediaInfo.Title = mkvFs.Title;
                }

                MediaStreamSource mkvStreamSource = mkvFs.MediaStreamSource;
                mkvStreamSource.Starting += mkvStreamSource_Starting;
                mkvStreamSource.SampleRequested += mkvStreamSource_SampleRequested;
                mkvStreamSource.Closed += mkvStreamSource_Closed;
                this.SetMediaStreamSource(mkvStreamSource);
            }
        }
        
        private void OnChangeCurrentAudioStreamInMKV(ICodec codec)
        {
            //재생정지
            Me.Stop();
            Me.AutoPlay = false;
            var mkvFs = mediaStreamFileSource as CCPlayer.HWCodecs.Matroska.MKV.MKVFileSource;
            mkvFs.IsReloading = true;
            CurrentMediaInfo.PausedTime = (long)Me.Position.TotalSeconds;

            var mdiaSource = mkvFs.CreateMediaStreamSource(codec);
            mdiaSource.Starting += mkvStreamSource_Starting;
            mdiaSource.SampleRequested += mkvStreamSource_SampleRequested;
            mdiaSource.Closed += mkvStreamSource_Closed;
            Me.SetMediaStreamSource(mdiaSource);
        }

        void SetMkvAudioLanguageList(MKVFileSource mkvFileSource)
        {
            //MediaStreamSource의 경우 Descriptor에서 Language 및 Name이 추출되지 않으며, 해당 스트림을 선택해도 작동되지 않는다.
            var audioLanguageList = mkvFileSource.UsableMediaCodecs.Where(x => x.CodecType == TrackTypes.Audio).Select((codec, index) => 
                new PickerItem<string, int>
                {
                    Key = index,
                    Name = GetCodecName(codec),
                    Payload = codec
                });

            MessengerInstance.Send(new Message("AudioLoadedInMKV", audioLanguageList), TransportControlViewModel.NAME);
        }

        private void SetMkvSubtitleLanguageList(MKVFileSource mkvFileSource)
        {
            var codecs = mkvFileSource.UsableSubtitleCodecs;
            
            Subtitle subtitle = null;
            byte[] header = null;
            string nameFormat = "[MKV] {0} {1}";
            List<PickerItem<string, string>> list = new List<PickerItem<string, string>>();

            foreach (var codec in codecs)
            {
                header = codec.TrackEntry.CodecPrivate;
                subtitle = new Subtitle()
                {
                    SubtitleFileKind = SubtitleFileKind.Internal
                };
                subtitle.AddLanguage(codec.TrackNumber.ToString(), new List<KeyValuePair<string, string>>());

                if (codec is ASSCodec || codec is SSACodec)
                {
                    //ASS의 경우 스타일 로딩
                    AssParser assParser = new AssParser();
                    assParser.LoadHeader(Encoding.UTF8.GetString(header, 0, header.Length));

                    subtitle.Parser = assParser;
                    subtitle.Title = string.Format(nameFormat, GetCodecName(codec), assParser.GetTitle());
                }
                else if (codec is SRTCodec)
                {
                    subtitle.Parser = new SrtParser();
                    subtitle.Title = string.Format(nameFormat, GetCodecName(codec), string.Empty);
                }
                
                list.Add(new PickerItem<string, string>
                { 
                    Key = codec.TrackNumber.ToString(), 
                    Name = subtitle.Title,
                    Payload = subtitle,
                    Payload2 = (byte)4
                });
            }
            //재생패널 콤보에 추가
            MessengerInstance.Send<Message>(new Message("SubtitlesLoaded", list), TransportControlViewModel.NAME);
        }

        private string GetCodecName(KnownCodec codec)
        {
            string name = string.Empty;
            var cultureInfo = LanguageCodeHelper.LangCodeToCultureInfo(codec.TrackEntry.Language);
            if (cultureInfo != null)
            {
                //langCode = cultureInfo.TwoLetterISOLanguageName;
                if (string.IsNullOrEmpty(codec.TrackEntry.Name))
                {
                    name = string.Format("{0}", cultureInfo.NativeName);
                }
                else
                {
                    name = string.Format("{0} - {1}", cultureInfo.NativeName, codec.TrackEntry.Name);
                }
            }
            else
            {
                //langCode = codec.TrackEntry.Language;
                if (string.IsNullOrEmpty(codec.TrackEntry.Name))
                {
                    name = string.Format("{0}", LanguageCodeHelper.UNKOWN_LANGUAGE);
                }
                else
                {
                    name = string.Format("{0} - {1}", LanguageCodeHelper.UNKOWN_LANGUAGE, codec.TrackEntry.Name);
                }
            }
            return name;
        }

        bool ValidateCodecForMkv(Document doc, MKVFileSource mkvFileSource)
        {
            DialogContent dc = new DialogContent();
            ResourceLoader loader = ResourceLoader.GetForCurrentView();
            IEnumerable<ICodec> codecs = mkvFileSource.UnusableMediaCodecs;

            //MKV가 아닌 경우 예외 발생
            if (doc.Header == null || doc.Header.Count == 0)
            {
                CurrentMediaInfo.MediaType = MediaType.Unkown;
                dc.Content = string.Format(loader.GetString("WrongFileFormat"), "MKV");
                dc.OccueredErrorMediaInfo = dc.Content;
            }
            else
            {
                var videoCodec = codecs.Where(x => x.CodecType == TrackTypes.Video);
                var audioCodec = codecs.Where(x => x.CodecType == TrackTypes.Audio);

                if (videoCodec.Any())
                {
                    dc.Content = loader.GetString("NotSupportedVideoCodec");
                    dc.Description1 = videoCodec.FirstOrDefault().CodecName;
                    dc.Description2 = string.Format(loader.GetString("NotSupportedCodecDesc"), "H264");
                }

                if (audioCodec.Any())
                {
                    var licenseCodec = audioCodec.FirstOrDefault(x => x.IsNeedLicense);

                    if (licenseCodec != null)
                    {
                        dc.Content = string.Format(loader.GetString("NotSupportedAudioLicenseCodec"), licenseCodec.LicenseCompany);
                        dc.Description1 = licenseCodec.CodecName;
                        dc.Description2 = loader.GetString("NotSupportedLicenseCodecDesc");
                    }
                    else
                    {
                        dc.Content = loader.GetString("NotSupportedAudioCodec");
                        dc.Description1 = audioCodec.FirstOrDefault(x => !x.IsNeedLicense).CodecName;
                        dc.Description2 = string.Format(loader.GetString("NotSupportedCodecDesc"), "AAC, MP3, FLAC, PCM");
                    }
                }
            }

            if (!string.IsNullOrEmpty(dc.Content))
            {
                //재생 종료
                StopMedia();
                //화면 닫기
                if (IsPlayerOpened)
                {
                    IsPlayerOpened = false;
                }
                //에러 메세지 처리
                if (string.IsNullOrEmpty(dc.OccueredErrorMediaInfo))
                {
                    dc.OccueredErrorMediaInfo = ResourceLoader.GetForCurrentView().GetString("Message/Error/CodecNotSupported");
                }
                //로딩 패널 숨김
                HideLoadingBar();
                //에러메세지 출력
                ShowDialogMediaStreamSourceError(dc);
                return false;
            }
            return true;
        }

        void mkvFs_SkipErrorClusterStarted(object sender, SkipErrorClusterEventArgs e)
        {
            ResourceLoader resource = ResourceLoader.GetForCurrentView();
            ShowLoadingBar(resource.GetString("Message/Error/Cluster"));
        }

        async void mkvFs_SkipErrorClusterCompleted(object sender, SkipErrorClusterEventArgs e)
        {
            HideLoadingBar();
            await DispatcherHelper.RunAsync(() =>
            {
                MessengerInstance.Send<Message>(new Message("SkipErrorClusterCompleteInMKV", (double)e.Timecode), TransportControlViewModel.NAME);
            });
        }

        void mkvStreamSource_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            MediaStreamSourceStartingRequest request = args.Request;

            if (request.StartPosition.HasValue && sender.CanSeek)
            {
                var deferal = request.GetDeferral();
                var mkvFs = mediaStreamFileSource as CCPlayer.HWCodecs.Matroska.MKV.MKVFileSource;
                if (mkvFs != null)
                {
                    var newTime = mkvFs.Seek((ulong)request.StartPosition.Value.Ticks / 10000);
                    request.SetActualStartPosition(newTime);
                }

                deferal.Complete();
            }
        }

        void mkvStreamSource_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            MediaStreamSample sample = null;
            MediaStreamSourceSampleRequest request = args.Request;
            MediaStreamSourceSampleRequestDeferral deferal = request.GetDeferral();
            try
            {
                var mkvFs = mediaStreamFileSource as CCPlayer.HWCodecs.Matroska.MKV.MKVFileSource;
                FrameBufferData fd = mkvFs.GetFrameData(request.StreamDescriptor);

                if (fd.Data != null)
                {
                    sample = MediaStreamSample.CreateFromBuffer(fd.Data, fd.TimeCode);
                    sample.Duration = fd.Duration;
                    sample.KeyFrame = fd.KeyFrame;
                    //자막을 검색하여 추가
                    MessengerInstance.Send<Message>(new Message("SubtitleFrameInMKV", mkvFs.SubtitleFrames), TransportControlViewModel.NAME);
                }
                else if (System.Diagnostics.Debugger.IsAttached)
                {
                    //NUll이 보고되면 자연스럽게 종료처리가 됨. 즉, MediaEnded Event가 발생함.
                    System.Diagnostics.Debug.WriteLine("***************************** null이 보고 되었음. 종료 코드가 들어옴 => MediaElement의 MediaEndedEvent 발생될 것임.");
                }
                
                request.Sample = sample;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("********************************** 샘플오류 또는 강제 종료 => MediaStreamSource의 Closed 이벤트가 발생될 것임 : " + e.Message);
                //Close 이벤트 발생
                sender.NotifyError(MediaStreamSourceErrorStatus.DecodeError);

            }
            finally
            {
                if (deferal != null)
                {
                    deferal.Complete();
                }
            }
        }

        void mkvStreamSource_Closed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            sender.SampleRequested -= mkvStreamSource_SampleRequested;
            sender.Starting -= mkvStreamSource_Starting;
            sender.Closed -= mkvStreamSource_Closed;
            sender = null;
        }
        #endregion
    }
}