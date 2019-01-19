using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Common.Interface;
using CCPlayer.UWP.Factory;
using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Views.Controls;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using Lime.Xaml.Helpers;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.ViewModels
{
    public class MediaFileInformationViewModel : CCPViewModelBase
    {
        [DependencyInjection]
        private Settings _Settings;
        //설정
        public Settings Settings
        {
            get { return _Settings; }
        }

        public IMediaInformation CurrentMediaInformation { get; set; }

        [DoNotNotify]
        public ObservableCollection<StorageItemCodec> StorageItemCodecSource { get; set; }

        [DoNotNotify]
        public ICommand CodecInfoPlayButtonCommand { get; set; }

        private Action<DecoderTypes> _PlaybackCallback;

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
        }

        protected override void CreateModel()
        {
            StorageItemCodecSource = new ObservableCollection<StorageItemCodec>();
        }

        protected override void RegisterMessage()
        {
            MessengerInstance.Register<Message<DecoderTypes>>(this, "ShowMediaFileInformation", ShowFlyout);
        }
        
        protected override void RegisterEventHandler()
        {
            CodecInfoPlayButtonCommand = new RelayCommand<object>(CodecInfoPlayButtonCommandExecute);
        }
        
        protected override void InitializeViewModel()
        {
        }
        
        private void CodecInfoPlayButtonCommandExecute(object args)
        {
            var mfi = args as MediaFileInformation;

            _PlaybackCallback?.Invoke(CurrentMediaInformation.RecommendedDecoderType);
            DialogHelper.CloseFlyout(mfi.Name);
        }

        private void ShowFlyout(Message<DecoderTypes> message)
        {
            Task.Factory.StartNew(async () =>
            {
                string displayName = null;
                IMediaInformation mediaInformation = null;
                if (message.ContainsKey("StorageItemInfo"))
                {
                    var sii = message.GetValue<StorageItemInfo>("StorageItemInfo");
                    StorageFile sf = await sii.GetStorageFileAsync();
                    if (sf != null)
                    {
                        try
                        {
                            var stream = await sf.OpenReadAsync();
                            mediaInformation = MediaInformationFactory.CreateMediaInformationFromStream(stream);
                            displayName = sf.DisplayName;
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine("로컬 파일 열기 실패 : " + e.Message);
                        }
                    }
                }
                else if (message.ContainsKey("NetworkItemInfo"))
                {
                    var wdii = message.GetValue<NetworkItemInfo>("NetworkItemInfo");

                    if (message.ContainsKey("VideoStream"))
                    {
                        Stream videoStream = message.GetValue<Stream>("VideoStream");
                        mediaInformation = MediaInformationFactory.CreateMediaInformationFromStream(videoStream.AsRandomAccessStream());
                        displayName = wdii.Name;
                    }
                    else
                    {
                        if (wdii.Uri != null)
                        {
                            try
                            {
                                string url = wdii.Uri.AbsoluteUri;
                                Windows.Foundation.Collections.PropertySet ps = null;

                                if (message.ContainsKey("UserName"))
                                {
                                    string username = message.GetValue<string>("UserName");
                                    string password = message.GetValue<string>("Password");
                                    url = wdii.GetAuthenticateUrl(username, password);
                                }

                                if (message.ContainsKey("CodePage"))
                                {
                                    int codepage = message.GetValue<int>("CodePage");
                                    ps = new Windows.Foundation.Collections.PropertySet();
                                    ps["codepage"] = codepage;
                                }
                                
                                mediaInformation = MediaInformationFactory.CreateMediaInformationFromUri(url, ps);
                                displayName = wdii.Name;
                            }
                            catch (Exception e)
                            {
                                System.Diagnostics.Debug.WriteLine("리모트(Uri) 파일 열기 실패 : " + e.Message);
                            }
                        }
                    }
                }

                await DispatcherHelper.RunAsync(async () =>
                {
                    MessengerInstance.Send(new Message("IsOpen", false), "ShowLoadingPanel");

                    if (mediaInformation == null)
                    {
                        System.Diagnostics.Debug.WriteLine("에러 발생.");
                        var resource = ResourceLoader.GetForCurrentView();
                        var dlg = DialogHelper.GetSimpleContentDialog(
                            resource.GetString("Message/Error/LoadMedia"),
                            resource.GetString("Message/Error/CheckFile"),
                            resource.GetString("Button/Close/Content"));
                        await dlg.ShowAsync();
                        App.ContentDlgOp = null;
                    }
                    else
                    {
                        var btnName = message.GetValue<string>("ButtonName");
                        _PlaybackCallback = message.Action;
                        //기본 디코더로 설정
                        mediaInformation.RecommendedDecoderType = Settings.Playback.DefaultDecoderType;
                        //선택된 정보 저장
                        mediaInformation.Title = displayName;
                        CurrentMediaInformation = mediaInformation;

                        StorageItemCodecSource.Clear();
                        foreach (var codecGroup in CurrentMediaInformation.CodecInformationList
                            .GroupBy(x => x.CodecType).OrderBy(x => x.Key)
                            .Select(x => new StorageItemCodec((MediaStreamTypes)x.Key) { Items = new ObservableCollection<CodecInformation>(x.ToArray()) }))
                        {
                            StorageItemCodecSource.Add(codecGroup);
                        }
                        Views.MainPage page = (Window.Current.Content as Frame).Content as Views.MainPage;
                        var button = ElementHelper.FindVisualChild<Button>(page, btnName);

                        //정보창 표시
                        button.Flyout.ShowAt(button);
                    }
                });
            });

            
        }
    }
}
