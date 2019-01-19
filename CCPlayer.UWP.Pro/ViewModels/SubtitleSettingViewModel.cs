using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using CCPlayer.UWP.Xaml.Controls;
using CCPlayer.UWP.Xaml.Helpers;
using GalaSoft.MvvmLight.Threading;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Windows.ApplicationModel.Resources;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.UWP.ViewModels
{
    public class SubtitleSettingViewModel : CCPViewModelBase
    {
        public Size SampleImageSize { get; set; }

        public BitmapImage SampleImageSource { get; set; }

        private System.Collections.IEnumerator _SampleImageUriEnumerator;

        private ComboBox _FontFamilyComboBox;

        [DependencyInjection]
        private Settings _Settings;
        //설정
        public Settings Settings
        {
            get { return _Settings; }
        }

        [DependencyInjection]
        private SettingDAO _SettingDAO;

        [DoNotNotify]
        public ObservableCollection<KeyName> FontSource { get; set; }

        [DoNotNotify]
        public ObservableCollection<KeyName> FontStyleSource { get; set; }

        [DoNotNotify]
        public ObservableCollection<KeyName> FontWeightSource { get; set; }

        [DoNotNotify]
        public ObservableCollection<CodePage> CharsetSource { get; set; }

        public JsonObject SampleSubtitleData { get; set; }

        [DoNotNotify]
        public SizeChangedEventHandler SampleImageSizeChangedEventHandler { get; set; }

        [DoNotNotify]
        public RoutedEventHandler SampleImageOpenedEventHandler { get; set; }

        [DoNotNotify]
        public TappedEventHandler ImageRefreshTappedEventHandler { get; set; }

        [DoNotNotify]
        public RoutedEventHandler FontFamilyComboBoxLoadedEventHandler { get; set; }

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
            _SettingDAO = null;
        }

        protected override void CreateModel()
        {
            FontSource = new ObservableCollection<KeyName>();
            FontStyleSource = new ObservableCollection<KeyName>();
            FontWeightSource = new ObservableCollection<KeyName>();
            CharsetSource = new ObservableCollection<CodePage>();

            //샘플 이미지
            _SampleImageUriEnumerator = new Uri[]
            {
                new Uri("ms-appx:///Assets/SampleSubtitleBackground-Green.jpg"),
                new Uri("ms-appx:///Assets/SampleSubtitleBackground-Blue.jpg"),
                new Uri("ms-appx:///Assets/SampleSubtitleBackground-Red.jpg"),
                new Uri("ms-appx:///Assets/SampleSubtitleBackground-Black.jpg"),
                new Uri("ms-appx:///Assets/SampleSubtitleBackground-White.jpg")
            }.GetEnumerator();

        }

        protected override void RegisterEventHandler()
        {
            //폰트 변경시 재로드 
            FontHelper.FontFamilyListChanged += (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach(KeyName item in e.NewItems)
                    {
                        if (!FontSource.Contains(item))
                        {
                            FontSource.Add(item);
                        }
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    foreach (KeyName item in e.OldItems)
                    {
                        if (!FontSource.Contains(item))
                        {
                            FontSource.Remove(item);
                        }
                    }

                    if (e.OldItems.Cast<KeyName>().Any(x => x.Key.ToString() == Settings.ClosedCaption.CCFontFamily))
                    {
                        Settings.ClosedCaption.CCFontFamily = FontHelper.FONT_FAMILY_DEFAUT;
                        //폰트가 삭제되어 선택이 사라지는 현상을 제거하기 위해 저장
                        _SettingDAO.Replace(Settings);
                    }
                }

                Settings.ClosedCaption.OnPropertyChanged("CCFontFamily");
            };

            SampleImageSizeChangedEventHandler = (sender, args) =>
            {
                if (args.NewSize.Width != 0 && args.NewSize.Height != 0)
                {
                    SampleImageSize = args.NewSize;
                }
            };

            SampleImageOpenedEventHandler = (sender, args) =>
            {
                if (SampleSubtitleData == null)
                {
                    var resource = ResourceLoader.GetForCurrentView();
                   
                    JsonObject obj = new JsonObject()
                    {
                        ["Pts"] = JsonValue.CreateNumberValue(0),
                        ["StartDisplayTime"] = JsonValue.CreateNumberValue(0),
                        ["EndDisplayTime"] = JsonValue.CreateNumberValue(0),
                        ["Format"] = JsonValue.CreateNumberValue(1),
                        ["NumRects"] = JsonValue.CreateNumberValue(1),
                        ["Rects"] = new JsonArray()
                        {
                            new JsonObject()
                            {
                                ["Guid"] = JsonValue.CreateStringValue(""),
                                ["Type"] = JsonValue.CreateNumberValue(3),
                                ["Text"] = JsonValue.CreateStringValue(""),
                                ["Ass"] = JsonValue.CreateStringValue(resource.GetString("Sample/Subtitle/Text")),
                                ["Style"] = new JsonObject()
                                {
                                    ["Name"] = JsonValue.CreateStringValue(""),
                                    ["FontSize"] = JsonValue.CreateStringValue(resource.GetString("Sample/Subtitle/BaseFontSize")),
                                    ["BorderStyle"] = JsonValue.CreateStringValue("1"),
                                    ["OutlineThickness"] = JsonValue.CreateNumberValue(1),
                                    ["ShadowThickness"] = JsonValue.CreateNumberValue(1.5),
                                    ["MarginLeft"] = JsonValue.CreateStringValue("6"),
                                    ["MarginRight"] = JsonValue.CreateStringValue("6"),
                                    ["MarginBottom"] = JsonValue.CreateStringValue("24")
                                }
                            }
                        }
                    };
                    
                    SampleSubtitleData = obj;
                }

                var img = sender as Image;
                var storyboard = img.Resources["FadeInStoryboard"] as Storyboard;
                storyboard.Begin();
            };

            ImageRefreshTappedEventHandler = (sender, args) =>
            {
                if (!_SampleImageUriEnumerator.MoveNext())
                {
                    _SampleImageUriEnumerator.Reset();
                    _SampleImageUriEnumerator.MoveNext();
                }
                
                SampleImageSource = new BitmapImage(_SampleImageUriEnumerator.Current as Uri);
            };

            FontFamilyComboBoxLoadedEventHandler = (sender, args) =>
            {
                _FontFamilyComboBox = sender as ComboBox;
                //폰트 로드
                if(FontSource.Count == 0)
                {
                    LoadFontList();
                }
            };
        }
        
        protected override void RegisterMessage()
        {
        }

        protected override void InitializeViewModel()
        {
            _SampleImageUriEnumerator.MoveNext();
            SampleImageSource = new BitmapImage(_SampleImageUriEnumerator.Current as Uri);
            SampleImageSize = new Size(400, 225);


            //글자 스타일 피커 데이터 생성
            foreach (FontStyle fs in Enum.GetValues(typeof(FontStyle)).Cast<FontStyle>())
            {
                FontStyleSource.Add(new KeyName(fs, fs.ToString()));
            }

            //글자 굵기 피커 데이터 설정
            foreach (var fwProp in typeof(FontWeights).GetRuntimeProperties())
            {
                FontWeightSource.Add(new KeyName(((FontWeight)fwProp.GetValue("Weight")).Weight, fwProp.Name));
            }

            foreach (var cp in CCPlayer.UWP.Xaml.Helpers.CodePageHelper.CharsetCodePage.ToList())
            {
                if (cp.Value != CCPlayer.UWP.Xaml.Helpers.CodePageHelper.AUTO_DETECT)
                    CharsetSource.Add(cp);
            }
        }
                
        internal void LoadFontList()
        {
            FontSource.Clear();
            FontHelper.LoadFonts(FontSource, FontTypes.All, null, () =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    Settings.ClosedCaption.OnPropertyChanged("CCFontFamily");
                });
            });
        }

    }
}
