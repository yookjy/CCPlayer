using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models.DataAccess;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using System.Reflection;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using CCPlayer.UWP.Common.Codec;

namespace CCPlayer.UWP.Models
{
    public class Setting
    {
        public string Code { get; set; }
        public object Value { get; set; }
        public string Type { get; set; }
        public string Attr1 { get; set; }
        public string Attr2 { get; set; }
        public bool IsUpdated { get; set; }

        public T GetValue<T>()
        {
            string val = Value?.ToString();
            if (val == null)
            {
                return default(T);
            }

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T) (object) (val == "Y" ? true : false);
                }
                else if (typeof(T) == typeof(ushort))
                {
                    return (T) (object) ushort.Parse(val);
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T) (object) int.Parse(val);
                }
                else if (typeof(T) == typeof(long))
                {
                    return (T) (object) long.Parse(val);
                }
                else if (typeof(T) == typeof(double))
                {
                    double f;
                    double? result;
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.CurrentUICulture.NumberFormat, out f)) 
                        result = f;
                    else if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                        result = f;
                    else if (double.TryParse(val, out f))
                        result = f;
                    else 
                        result = double.Parse(val, CultureInfo.CurrentUICulture.NumberFormat);

                    return (T) (object) result;
                }
            }
            catch (Exception)
            {
                return default(T);
            }

            return (T)Value;
        }
    }

    public class Settings : List<Setting>
    {
        public const double FONT_SIZE_RATIO_DEFAULT = 1.0;

        public class BaseSetting : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string SUBTITLE_COLOR_DEFAULT = Colors.White.ToString();
            public SolidColorBrush SUBTITLE_BACKGROUND_DEFAULT = new SolidColorBrush(Colors.Black);

            public void OnPropertyChanged([CallerMemberName] string property = "")
            {
                if (PropertyChanged != null)
                {
                    GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(property));
                    });
                }
            }

            public FontWeight ToFontWeight(ushort value)
            {
                var fwProp = typeof(FontWeights).GetRuntimeProperties().FirstOrDefault(x => ((FontWeight)x.GetValue("Weight")).Weight == (ushort)value);
                if (fwProp == null) return FontWeights.Medium;
                return (FontWeight)fwProp.GetValue("Weight");
            }

            public byte[] ToArgb(string hex)
            {
                var val = hex.Replace("#", string.Empty);
                return Enumerable.Range(0, val.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(val.Substring(x, 2), 16))
                                 .ToArray();
            }

            public Color ToColor(string hex)
            {
                byte[] argb = ToArgb(hex);
                return Color.FromArgb(argb[0], argb[1], argb[2], argb[3]);
            }

            private readonly string _type;

            private readonly Settings _settings;

            public BaseSetting(Settings settings, string type)
            {
                _settings = settings;
                _type = type;
            }

            public Setting Get(string code)
            {
                return _settings?.FirstOrDefault(x => x.Code == code && x.Type == _type);
            }

            public bool TryGetValue<T>(string code, out T value, T defaultValue)
            {
                var setting = Get(code);
                if (setting == null)
                {
                    Set(code, defaultValue);
                    value = defaultValue;
                    return false;
                }
                value = setting.GetValue<T>();
                return true;
            }

            public void Set<T>(string code, T value, bool isUiUpdate = true)
            {
                var setting = Get(code);
                if (setting == null)
                {
                    object obj;
                    if (typeof(T) == typeof(bool))
                        obj = (bool)(object)value ? "Y" : "N";
                    else
                        obj = value;

                    setting = new Setting
                    {
                        Code = code,
                        Type = _type,
                        Value = obj,
                        IsUpdated = true
                    };
                    
                    _settings.Add(setting);
                }
                else
                {
                    if (typeof(T) == typeof(bool))
                    {
                        bool boolVal = (bool)(object)value;
                        if (setting.GetValue<bool>() != boolVal)
                        {
                            setting.Value = boolVal ? "Y" : "N";
                            setting.IsUpdated = true;
                        }
                    }
                    else
                    {
                        if (setting.GetValue<T>()?.Equals(value) == false)
                        {
                            setting.Value = value;
                            setting.IsUpdated = true;
                        }
                    }
                }
                if (isUiUpdate)
                    if (code != null) OnPropertyChanged(code);
            }
        }

        public class GeneralSetting : BaseSetting
        {
            public GeneralSetting(Settings settings)
                : base(settings, "1")
            {
                settings.General = this;
            }
            
            public void ResetDefaultValue()
            {
                Set(nameof(LastMenuType), (int)MenuType.Explorer);
                Set(nameof(LastPlayListSeq), -1);
                
                if (App.IsMobile)
                {
                    Set(nameof(Theme), (int)ElementTheme.Dark);
                }
                else
                {
                    Set(nameof(Theme), (int)(Application.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark));
                }
                Set(nameof(UseHardwareBackButtonWithinVideo), true);
                Set(nameof(UseIndependentRotationLock), true);
                Set(nameof(UseSaveFontInMkv), false);
                Set(nameof(AddedAfterMovePlayList), true);
                Set(nameof(SortBy), SortType.Name.ToString());
                Set(nameof(PaidLevelValue), (int)PaidLevelType.Full);
            }

            public MenuType LastMenuType
            {
                get
                {
                    int type = 0;
                    TryGetValue(nameof(LastMenuType), out type, (int)MenuType.Explorer);
                    return (MenuType)type;
                }
                set
                {
                    Set(nameof(LastMenuType), (int)value);
                }
            }

            public long LastPlayListSeq
            {
                get
                {
                    long seq;
                    TryGetValue(nameof(LastPlayListSeq), out seq, -1);
                    return seq;
                }
                set
                {
                    Set(nameof(LastPlayListSeq), value);
                }
            }

            public ElementTheme ElementTheme
            {
                get
                {
                    return (ElementTheme)Theme;
                }
            }

            public int Theme
            {
                get
                {
                    int value;
                    TryGetValue(nameof(Theme), out value, 0);
                    return value;
                }
                set {
                    Set(nameof(Theme), (int)value); OnPropertyChanged("ElementTheme");
                    ChangeTitleBarColor();
                }
            }

            public void ChangeTitleBarColor()
            {
                var fgColor = ElementTheme == ElementTheme.Light ? Colors.Black : Colors.White;
                if (App.IsMobile)
                {
                    var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                    if (statusBar != null)
                    {
                        var bgColor = ElementTheme == ElementTheme.Light ? Color.FromArgb(255, 242, 242, 242) : Color.FromArgb(255, 23, 23, 23);

                        statusBar.BackgroundOpacity = 1;
                        statusBar.BackgroundColor = bgColor;
                        statusBar.ForegroundColor = fgColor;
                    }
                }
                else
                {
                    var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
                    if (titleBar != null)
                    {
                        var bgColor = ElementTheme == ElementTheme.Light ? Color.FromArgb(255, 230, 230, 230) : Color.FromArgb(255, 31, 31, 31);
                        var psColor = ElementTheme == ElementTheme.Light ? Color.FromArgb(255, 187, 187, 187) : Color.FromArgb(255, 85, 85, 85);
                        var hbColor = ElementTheme == ElementTheme.Light ? Color.FromArgb(255, 204, 204, 204) : Color.FromArgb(255, 51, 51, 51);

                        titleBar.BackgroundColor = bgColor;
                        titleBar.ButtonBackgroundColor = bgColor;
                        titleBar.InactiveBackgroundColor = bgColor;
                        titleBar.ButtonInactiveBackgroundColor = bgColor;
                        titleBar.ButtonHoverBackgroundColor = hbColor;
                        titleBar.ButtonHoverForegroundColor = fgColor;
                        titleBar.ButtonPressedBackgroundColor = psColor;
                        titleBar.ButtonPressedForegroundColor = fgColor;
                    }
                }
            }

            public bool UseHardwareBackButtonWithinVideo
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseHardwareBackButtonWithinVideo), out value, true);
                    return value;
                }
                set { Set(nameof(UseHardwareBackButtonWithinVideo), value); }
            }

            public bool UseIndependentRotationLock
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseIndependentRotationLock), out value, true);
                    return value;
                }
                set { Set(nameof(UseIndependentRotationLock), value); }
            }

            public bool UseSaveFontInMkv
            {
                get
                {
                    bool value = false;
                    TryGetValue(nameof(UseSaveFontInMkv), out value, false);
                    return value;
                }
                set { Set(nameof(UseSaveFontInMkv), value); }
            }
            
            public bool AddedAfterMovePlayList
            {
                get
                {
                    bool value = true;
                    TryGetValue(nameof(AddedAfterMovePlayList), out value, true);
                    return value;
                }
                set { Set(nameof(AddedAfterMovePlayList), value); }
            }

            public string SortBy
            {
                get
                {
                    string value = SortType.Name.ToString();
                    TryGetValue(nameof(SortBy), out value, SortType.Name.ToString());
                    return value;
                }
                set { Set(nameof(SortBy), value); }
            }

            public PaidLevelType PaidLevel
            {
                get
                {
                    return (PaidLevelType)PaidLevelValue != PaidLevelType.Full ? PaidLevelType.Trial : PaidLevelType.Full;
                }
                set
                {
                    var newVal = (PaidLevelType)value != PaidLevelType.Full ? PaidLevelType.Trial : PaidLevelType.Full;
                    if ((PaidLevelType)PaidLevelValue != newVal)
                    {
                        PaidLevelValue = (int)newVal;
                    }
                }
            }

            private int PaidLevelValue
            {
                get
                {
                    int value;
                    TryGetValue(nameof(PaidLevelValue), out value, (int)PaidLevelType.Trial);
                    return value;
                }
                set { Set(nameof(PaidLevelValue), value); }
            }
        };

        public class PlaybackSetting : BaseSetting
        {
            public PlaybackSetting(Settings settings)
                : base(settings, "2")
            {
                settings.Playback = this;
            }
            public void ResetDefaultValue()
            {
                Set(nameof(UseRotationLock), false);
                Set(nameof(UseFlipToPause), true);
                Set(nameof(UseSuspendToPause), true);
                Set(nameof(UseBeginLandscape), true);
                Set(nameof(SeekTimeInterval), 0);
                Set(nameof(LastPlaybackOrientation), (ushort)SimpleOrientation.Rotated90DegreesCounterclockwise);

                var isMobile = App.IsMobile;
                try
                {
                    if (!isMobile)
                    {
                        //CPU정보를 읽는다.
                        var asyncOp = Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                            @"System.Devices.InterfaceClassGuid:=""{97FADB10-4E33-40AE-359C-8BEF029DBDD0}""");

                        while (asyncOp.Status == AsyncStatus.Started)
                            CoreWindow.GetForCurrentThread().Dispatcher.ProcessEvents(CoreProcessEventsOption.ProcessAllIfPresent);

                        //Atom cpu이면 모바일로 분류
                        if (asyncOp.Status == AsyncStatus.Completed)
                            isMobile = asyncOp.GetResults().Any(x => x.Id.Contains("Intel(R)_Atom(TM)"));
                    }
                }
                finally
                {
                    Set(nameof(DefaultDecoderType), (ushort)(isMobile ? DecoderTypes.Hybrid : DecoderTypes.SW));
                }

                Set(nameof(UseGpuShader), true);
                Set(nameof(UseLimeEngine), true);
                Set(nameof(Brightness), 100.0D);
            }
            public bool UseRotationLock
            {
                get
                {
                    bool value = false;
                    TryGetValue(nameof(UseRotationLock), out value, false);
                    return value;
                }
                set { Set(nameof(UseRotationLock), value); }
            }
            public bool UseFlipToPause
            {
                get
                {
                    var value = true;
                    TryGetValue(nameof(UseFlipToPause), out value, true);
                    return value;
                }
                set { Set(nameof(UseFlipToPause), value); }
            }
            public bool UseSuspendToPause
            {
                get
                {
                    var value = true;
                    TryGetValue(nameof(UseSuspendToPause), out value, true);
                    return value;
                }
                set { Set(nameof(UseSuspendToPause), value); }
            }
            public bool UseBeginLandscape
            {
                get
                {
                    var value = true;
                    TryGetValue(nameof(UseBeginLandscape), out value, true);
                    return value;
                }
                set { Set(nameof(UseBeginLandscape), value); }
            }
            public int SeekTimeInterval
            {
                get
                {
                    int value = 0;
                    TryGetValue(nameof(SeekTimeInterval), out value, 0);
                    return value;
                }
                set { Set(nameof(SeekTimeInterval), value); }
            }
            public SimpleOrientation LastPlaybackOrientation
            {
                get
                {
                    ushort value = 0;
                    TryGetValue(nameof(LastPlaybackOrientation), out value, (ushort)SimpleOrientation.Rotated90DegreesCounterclockwise);
                    return (SimpleOrientation)value;
                }
                set { Set(nameof(LastPlaybackOrientation), (ushort)value); }
            }
            
            public DecoderTypes DefaultDecoderType
            {
                get
                {
                    ushort value = 0;
                    ushort defaultValue = (ushort)(App.IsMobile ? DecoderTypes.Hybrid : DecoderTypes.SW);
                    TryGetValue(nameof(DefaultDecoderType), out value, defaultValue);
                    return (DecoderTypes)value;
                }
                set { Set(nameof(DefaultDecoderType), (ushort)value); }
            }
            public bool UseGpuShader
            {
                get
                {
                    bool value = true;
                    TryGetValue(nameof(UseGpuShader), out value, true);
                    return value;
                }
                set { Set(nameof(UseGpuShader), value); }
            }
            public bool UseLimeEngine
            {
                get
                {
                    bool value = true;
                    TryGetValue(nameof(UseLimeEngine), out value, true);
                    return value;
                }
                set { Set(nameof(UseLimeEngine), value); }
            }

            public double Brightness
            {
                get
                {
                    double value = 0;
                    const double defaultValue = 100.0;
                    TryGetValue(nameof(Brightness), out value, defaultValue);

                    if (value > defaultValue) return defaultValue;
                    if (value < 20.0) return 20.0;

                    return value;
                }
                set
                {
                    Set(nameof(Brightness), value);
                    //OnPropertyChanged();
                }
            }
        };

        public class ClosedCaptionSetting : BaseSetting
        {
            private ushort DefaultCCFontStyle = (ushort)Windows.UI.Text.FontStyle.Normal;
            private ushort DefaultCCFontWeight = Windows.UI.Text.FontWeights.Medium.Weight;
            private ushort DefaultCCVerticalAlignment = (ushort)Windows.UI.Xaml.VerticalAlignment.Bottom;
            private double DefaultCCTranslateY = -24.0;
            private int DefaultCCDefaultCodePage = 65001;

            public ClosedCaptionSetting(Settings settings) : base(settings, "3")
            {
                settings.ClosedCaption = this;

                SubtitleTextAlignment = TextAlignment.Center;
                _CCBackground = SUBTITLE_BACKGROUND_DEFAULT;
                _NotOverridingCCFontStyle = (FontStyle)DefaultCCFontStyle;
                _NotOverridingCCFontWeight = Windows.UI.Text.FontWeights.Medium;
                _NotOverridingCCForeground = new SolidColorBrush(ToColor(SUBTITLE_COLOR_DEFAULT));
                _NotOverridingCCForegroundColor = ToColor(SUBTITLE_COLOR_DEFAULT);
            }

            public void ResetDefaultvalue()
            {
                Set(nameof(CCCodePage), Xaml.Helpers.CodePageHelper.AUTO_DETECT);
                Set(nameof(CCFontSizeRatio), Settings.FONT_SIZE_RATIO_DEFAULT);
                Set(nameof(CCStyleOverride), false);
                Set(nameof(CCFontStyle), (ushort)DefaultCCFontStyle);
                Set(nameof(CCFontWeight), DefaultCCFontWeight);
                Set(nameof(CCShadowVisibility), (ushort)Visibility.Visible);
                Set(nameof(CCOutlineVisibility), (ushort)Visibility.Visible);
                Set(nameof(CCBackgroundVisibility), (ushort)Visibility.Collapsed);
                Set(nameof(CCFontFamily), FontHelper.FONT_FAMILY_DEFAUT);
                Set(nameof(CCForegroundColor), SUBTITLE_COLOR_DEFAULT);
                Set(nameof(CCVerticalAlignment), DefaultCCVerticalAlignment);
                Set(nameof(CCTranslateY), DefaultCCTranslateY);
                Set(nameof(ShowClosedCaption), true);
                Set(nameof(CCDefaultCodePage), DefaultCCDefaultCodePage);
            }

            public void ResetNoOverridingValues()
            {
                CCBackground = SUBTITLE_BACKGROUND_DEFAULT;
                NotOverridingCCFontStyle = (FontStyle)DefaultCCFontStyle;
                NotOverridingCCFontWeight = Windows.UI.Text.FontWeights.Medium;
                NotOverridingCCForegroundColor = ToColor(SUBTITLE_COLOR_DEFAULT);
            }

            public int CCCodePage
            {
                get
                {
                    int value = 0;
                    TryGetValue(nameof(CCCodePage), out value, Xaml.Helpers.CodePageHelper.AUTO_DETECT);
                    return value;
                }
                set { Set<int>(nameof(CCCodePage), value); }
            }
            
            public double CCFontSizeRatio
            {
                get
                {
                    double value = 0;
                    TryGetValue(nameof(CCFontSizeRatio), out value, Settings.FONT_SIZE_RATIO_DEFAULT);
                    return value;
                }
                set { Set<double>(nameof(CCFontSizeRatio), value); }
            }

            public Visibility CCShadowVisibility
            {
                get
                {
                    ushort value = 0;
                    TryGetValue(nameof(CCShadowVisibility), out value, value);
                    return (Visibility)value;
                }
                set { Set(nameof(CCShadowVisibility), (ushort)value); }
            }

            public Visibility CCOutlineVisibility
            {
                get
                {
                    ushort value = 0;
                    TryGetValue(nameof(CCOutlineVisibility), out value, value);
                    return (Visibility)value;
                }
                set { Set(nameof(CCOutlineVisibility), (ushort)value); }
            }

            public Visibility CCBackgroundVisibility
            {
                get
                {
                    ushort value = 0;
                    TryGetValue(nameof(CCBackgroundVisibility), out value, value);
                    var visibility =  (Visibility)value;
                    return visibility;
                }
                set { Set(nameof(CCBackgroundVisibility), (ushort)value); }
            }

            public string CCFontFamily
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(CCFontFamily), out value, FontHelper.FONT_FAMILY_DEFAUT);
                    return value;
                }
                set
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        Set(nameof(CCFontFamily), value);
                    }
                }
            }
            
            public bool CCStyleOverride
            {
                get
                {
                    bool value = false;
                    TryGetValue(nameof(CCStyleOverride), out value, false);
                    return value;
                }
                set
                {
                    Set<bool>(nameof(CCStyleOverride), value);
                    OnPropertyChanged("CCFontStyle");
                    OnPropertyChanged("CCFontWeight");
                    OnPropertyChanged("CCForeground");
                }
            }

            private FontStyle _NotOverridingCCFontStyle;
            public FontStyle NotOverridingCCFontStyle
            {
                get
                {
                    return _NotOverridingCCFontStyle;
                }
                set
                {
                    if (_NotOverridingCCFontStyle != value)
                    {
                        _NotOverridingCCFontStyle = value;
                        OnPropertyChanged("CCFontStyle");
                    }
                }
            }

            public FontStyle CCFontStyle
            {
                get
                {
                    if (!CCStyleOverride)
                    {
                        return NotOverridingCCFontStyle;
                    }
                    else
                    {
                        ushort value = 0;
                        TryGetValue(nameof(CCFontStyle), out value, DefaultCCFontStyle);
                        return (FontStyle)value;
                    }
                }
                set { Set(nameof(CCFontStyle), (ushort)value); }
            }

            private TextAlignment _SubtitleTextAlignment;
            public TextAlignment SubtitleTextAlignment
            {
                get
                {
                    return _SubtitleTextAlignment;
                }
                set
                {
                    if (_SubtitleTextAlignment != value)
                    {
                        _SubtitleTextAlignment = value;
                        OnPropertyChanged();
                    }
                }
            }

            private FontWeight _NotOverridingCCFontWeight;
            public FontWeight NotOverridingCCFontWeight
            {
                set
                {
                    if (_NotOverridingCCFontWeight.Weight != value.Weight)
                    {
                        _NotOverridingCCFontWeight = value;
                        OnPropertyChanged("CCFontWeight");
                    }
                }
            }

            public FontWeight CCFontWeight
            {
                get
                {
                    if (!CCStyleOverride)
                    {
                        return _NotOverridingCCFontWeight;
                    }
                    else
                    {
                        ushort value;
                        TryGetValue(nameof(CCFontWeight), out value, DefaultCCFontWeight);
                        return ToFontWeight(value);
                    }
                }
                set { Set<ushort>(nameof(CCFontWeight), value.Weight); }
            }
            //FontWeight는 == 연산을 할수 없으므로 .Weight를 사용 (.Weight는 프로퍼티가 아니므로 Xaml에서 바인딩 변수로 사용할 수 없다)
            public ushort CCFontWeightValue
            {
                get
                {
                    return CCFontWeight.Weight;
                }
                set { CCFontWeight = ToFontWeight(value); }
            }
            
            private SolidColorBrush _NotOverridingCCForeground;
            private SolidColorBrush _CCForeground;
            public SolidColorBrush CCForeground
            {
                get
                {
                    if (!CCStyleOverride)
                    {
                        return _NotOverridingCCForeground;
                    }
                    else
                    {
                        if (_CCForeground == null)
                        {
                            _CCForeground = new SolidColorBrush(CCForegroundColor);
                        }
                    }
                    return _CCForeground;
                }
            }

            private Color _NotOverridingCCForegroundColor;
            public Color NotOverridingCCForegroundColor
            {
                set
                {
                    if (_NotOverridingCCForegroundColor != value)
                    {
                        _NotOverridingCCForegroundColor = value;
                        _NotOverridingCCForeground = new SolidColorBrush(value);
                        OnPropertyChanged("CCForeground");
                    }
                }
            }

            public Color CCForegroundColor
            {
                get
                {
                    if (!CCStyleOverride)
                    {
                        return _NotOverridingCCForegroundColor;
                    }
                    else
                    {
                        string hex = string.Empty;
                        TryGetValue(nameof(CCForegroundColor), out hex, SUBTITLE_COLOR_DEFAULT);
                        return ToColor(hex);
                    }
                }
                set
                {
                    Set(nameof(CCForegroundColor), value.ToString());
                    _CCForeground = new SolidColorBrush(value);
                    OnPropertyChanged("CCForeground");
                }
            }

            private SolidColorBrush _CCBackground;
            public SolidColorBrush CCBackground
            {
                get
                {
                    return _CCBackground;
                }
                set
                {
                    if (_CCBackground != value)
                    {
                        _CCBackground = value;
                        OnPropertyChanged();
                    }
                }
            }

            public VerticalAlignment CCVerticalAlignment
            {
                get
                {
                    ushort value = 0;
                    TryGetValue(nameof(CCVerticalAlignment), out value, DefaultCCVerticalAlignment);
                    return (VerticalAlignment)value;
                }
                set { Set(nameof(CCVerticalAlignment), (ushort)value, false); }
            }

            public double CCTranslateY
            {
                get
                {
                    double value = 0;
                    TryGetValue(nameof(CCTranslateY), out value, DefaultCCTranslateY);
                    return value;
                }
                set { Set<double>(nameof(CCTranslateY), value, false); }
            }

            public bool ShowClosedCaption
            {
                get
                {
                    bool value = true;
                    TryGetValue(nameof(ShowClosedCaption), out value, true);
                    return value;
                }
                set { Set<bool>(nameof(ShowClosedCaption), value); }
            }

            public int CCDefaultCodePage
            {
                get
                {
                    int value = 0;
                    TryGetValue(nameof(CCDefaultCodePage), out value, DefaultCCDefaultCodePage);
                    return value;
                }
                set { Set<int>(nameof(CCDefaultCodePage), value); }
            }
        };

        public class PrivacySetting : BaseSetting
        {
            public PrivacySetting(Settings settings)
                : base(settings, "4")
            {
                settings.Privacy = this;
            }

            public void ResetDefaultValue()
            {
                Set(nameof(UseAppLock), false);
                Set(nameof(AppLockPassword), string.Empty);
                Set(nameof(AppLockPasswordHint), string.Empty);
                Set(nameof(CanAppLock), false);
            }

            public bool CanAppLock
            {
                get { return !string.IsNullOrEmpty(AppLockPassword); }
            }

            public bool UseAppLock
            {
                get
                {
                    bool value = false;
                    TryGetValue(nameof(UseAppLock), out value, false);
                    return value;
                }
                set { Set(nameof(UseAppLock), value); }
            }

            public string AppLockPassword
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(AppLockPassword), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(AppLockPassword), value);
                    OnPropertyChanged("CanAppLock");
                }
            }

            public string AppLockPasswordHint
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(AppLockPasswordHint), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(AppLockPasswordHint), value);
                }
            }
        };

        public class ServerSetting : BaseSetting
        {
            public ServerSetting(Settings settings)
                : base(settings, "5")
            {
                settings.Server = this;
            }

            public void ResetDefaultValue()
            {
                Set(nameof(WebDAVSSL), false);
                Set(nameof(WebDAVHost), string.Empty);
                Set(nameof(WebDAVPath), string.Empty);
                Set(nameof(WebDAVPort), string.Empty);
                Set(nameof(WebDAVUserName), string.Empty);
                Set(nameof(WebDAVPassword), string.Empty);

                Set(nameof(FtpHost), string.Empty);
                Set(nameof(FtpPort), string.Empty);
                Set(nameof(FtpUserName), string.Empty);
                Set(nameof(FtpPassword), string.Empty);
            }

            public string WebDAVHost
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(WebDAVHost), out value, string.Empty);
                    return value;
                }
                set { Set(nameof(WebDAVHost), value); }
            }

            public string WebDAVPath
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(WebDAVPath), out value, string.Empty);
                    return value;
                }
                set { Set(nameof(WebDAVPath), value); }
            }

            public bool WebDAVSSL
            {
                get
                {
                    bool value = false;
                    TryGetValue(nameof(WebDAVSSL), out value, false);
                    return value;
                }
                set { Set<bool>(nameof(WebDAVSSL), value); }
            }

            public string WebDAVPort
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(WebDAVPort), out value, string.Empty);
                    return value;
                }
                set { Set<string>(nameof(WebDAVPort), value); }
            }

            public string WebDAVUserName
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(WebDAVUserName), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(WebDAVUserName), value);
                }
            }

            public string WebDAVPassword
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(WebDAVPassword), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(WebDAVPassword), value);
                }
            }

            public string FtpHost
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(FtpHost), out value, string.Empty);
                    return value;
                }
                set { Set(nameof(FtpHost), value); }
            }

            public string FtpPort
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(FtpPort), out value, string.Empty);
                    return value;
                }
                set { Set<string>(nameof(FtpPort), value); }
            }

            public string FtpUserName
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(FtpUserName), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(FtpUserName), value);
                }
            }

            public string FtpPassword
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(nameof(FtpPassword), out value, string.Empty);
                    return value;
                }
                set
                {
                    Set(nameof(FtpPassword), value);
                }
            }
        };

        public class ThumbnailSetting : BaseSetting
        {
            public ThumbnailSetting(Settings settings)
                : base(settings, "6")
            {
                settings.Thumbnail = this;
            }

            public void ResetDefaultValue()
            {
                Set(nameof(UseUnsupportedLocalFile), true);
                Set(nameof(UseUnsupportedLocalFolder), true);
                Set(nameof(UseUnsupportedDLNAFile), true);
                Set(nameof(UseUnsupportedDLNAFolder), true);
                Set(nameof(UseUnsupportedWebDAVFile), true);
                Set(nameof(UseUnsupportedWebDAVFolder), true);
                Set(nameof(UseUnsupportedFTPFile), true);
                Set(nameof(UseUnsupportedFTPFolder), false);
                Set(nameof(UseUnsupportedOneDriveFile), true);
                Set(nameof(UseUnsupportedOneDriveFolder), true);
                Set(nameof(RetentionPeriod), 7);
            }

            public bool UseUnsupportedLocalFile
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedLocalFile), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedLocalFile), value); }
            }
            public bool UseUnsupportedLocalFolder
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedLocalFolder), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedLocalFolder), value); }
            }
            public bool UseUnsupportedDLNAFile
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedDLNAFile), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedDLNAFile), value); }
            }
            public bool UseUnsupportedDLNAFolder
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedDLNAFolder), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedDLNAFolder), value); }
            }
            public bool UseUnsupportedWebDAVFile
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedWebDAVFile), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedWebDAVFile), value); }
            }
            public bool UseUnsupportedWebDAVFolder
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedWebDAVFolder), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedWebDAVFolder), value); }
            }
            public bool UseUnsupportedFTPFile
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedFTPFile), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedFTPFile), value); }
            }
            public bool UseUnsupportedFTPFolder
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedFTPFolder), out value, false);
                    return value;
                }
                set { Set(nameof(UseUnsupportedFTPFolder), value); }
            }

            public bool UseUnsupportedOneDriveFile
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedOneDriveFile), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedOneDriveFile), value); }
            }
            public bool UseUnsupportedOneDriveFolder
            {
                get
                {
                    bool value;
                    TryGetValue(nameof(UseUnsupportedOneDriveFolder), out value, true);
                    return value;
                }
                set { Set(nameof(UseUnsupportedOneDriveFolder), value); }
            }

            public int RetentionPeriod
            {
                get
                {
                    int value;
                    TryGetValue(nameof(RetentionPeriod), out value, 7);
                    return value;
                }
                set { Set(nameof(RetentionPeriod), (int)value); }
            }

        };

        public GeneralSetting General { get; set; }
        public PlaybackSetting Playback { get; set; }
        public ClosedCaptionSetting ClosedCaption { get; set; }
        public PrivacySetting Privacy { get; set; }
        public ServerSetting Server { get; set; }
        public ThumbnailSetting Thumbnail { get; set; }

        private Setting Get(string code)
        {
            return this.FirstOrDefault(x => x.Code == code);
        }

        public void ResetAll()
        {
            General.ResetDefaultValue();
            Playback.ResetDefaultValue();
            ClosedCaption.ResetDefaultvalue();
            Privacy.ResetDefaultValue();
            Server.ResetDefaultValue();
            Thumbnail.ResetDefaultValue();
        }
    }
}
