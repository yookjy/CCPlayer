using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models.DataAccess;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CCPlayer.WP81.Models
{
    public enum HardwareBackButtonAction
    {
        TerminateApplication,
        MoveToUpperFolder
    }

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
            if (Value == null)
            {
                return default(T);
            }

            if (typeof(T) == typeof(bool))
            {
                return (T)(object)(Value != null && Value.ToString() == "Y" ? true : false); 
            }
            else if (typeof(T) == typeof(ushort))
            {
                return (T)(object)(ushort.Parse(Value.ToString()));
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)(int.Parse(Value.ToString()));
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)(double.Parse(Value.ToString(), Settings.NumberFormat));
            }

            return (T)Value;
        }
    }

    public class Settings : List<Setting> 
    {
        public double FontSizeMin { get { return 10; } }
        public double FontSizeMax { get { return 35; } }
        public const double FONT_SIZE_DEFAULT = 30;
        public const string FONT_FAMILY_DEFAUT = "Global User Interface";
        public static System.Globalization.NumberFormatInfo NumberFormat;
        
        public class BaseSetting : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string FOREGROUND_COLOR_DEFAULT = ((SolidColorBrush)Windows.UI.Xaml.Application.Current.Resources["PhoneForegroundBrush"]).Color.ToString();
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

            private string _TYPE; 

            private Settings settings;
            
            public BaseSetting(Settings settings, string type)
            {
                if (NumberFormat == null)
                {
                    NumberFormat = ((System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.Clone());
                    NumberFormat.NumberDecimalSeparator = ".";
                }

                this.settings = settings;
                _TYPE = type;
            }

            public Setting Get(string code)
            {
                return settings.FirstOrDefault(x => x.Code == code && x.Type == _TYPE);
            }

            public bool TryGetValue<T>(string key, out T value, T defaultValue)
            {
                if (Get(key) == null)
                {
                    Set(key, defaultValue);
                    value = defaultValue;
                    return false;
                }
                value = Get(key).GetValue<T>();
                return true;
            }

            public void Set<T>(string code, T value)
            {
                var setting = Get(code);
                
                if (setting == null)
                {
                    setting = new Setting();
                    this.settings.Add(setting);
                    setting.Code = code;
                    setting.Type = _TYPE;

                    if (typeof(T) == typeof(bool))
                    {
                        setting.Value = (bool)(object)value ? "Y" : "N";
                    }
                    else
                    {
                        setting.Value = value;
                    }
                    setting.IsUpdated = true;
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
                        if (!setting.GetValue<T>().Equals(value))
                        {
                            setting.Value = value;
                            setting.IsUpdated = true;
                        }
                    }
                }
                OnPropertyChanged(code.Substring(1));
            }
        }

        public class GeneralSetting : BaseSetting
        {
            private const string _DisplayBrightness = "_DisplayBrightness";
            private const string _BackgroundColor = "_BackgroundColor";
            private const string _ForegroundColor = "_ForegroundColor";
            private const string _SubForegroundColor = "_SubForegroundColor";
            private const string _UseSaveFontInMkv = "_UseSaveFontInMkv";
            private const string _HardwareBackButtonAction = "_HardwareBackButtonAction";
            private const string _StartUpSection = "_StartUpSection";
            private const string _UseAllVideoSection = "_UseAllVideoSection";

            public GeneralSetting(Settings settings)
                : base(settings, "1")
            {
                settings.General = this;
            }

            public void ResetDefaultValue()
            {
                Set(_DisplayBrightness, 100.0D);
                Set(_BackgroundColor, Colors.DarkGray.ToString()); //"#FFA9A9A9"; //DarkGray
                Set(_ForegroundColor, FOREGROUND_COLOR_DEFAULT);
                Set(_SubForegroundColor, Colors.DodgerBlue.ToString()); // "#FFEEE8AA";
                Set(_UseSaveFontInMkv, false);
                Set(_HardwareBackButtonAction, CCPlayer.WP81.Models.HardwareBackButtonAction.MoveToUpperFolder.ToString());
                Set(_StartUpSection, 0);
                Set(_UseAllVideoSection, true);
            }

            public double DisplayBrightness
            {
                get
                {
                    double value = 0;
                    TryGetValue(_DisplayBrightness, out value, 100.0D);
                    return value;
                }
                set
                {
                    if (DisplayBrightness != value)
                    {
                        Set(_DisplayBrightness, value);
                        OnPropertyChanged("DisplayBrightnessOpacity");
                    }
                }
            }

            public double DisplayBrightnessOpacity
            {
                get
                {
                    return ((100 - DisplayBrightness) / 100);
                }
            }

            private SolidColorBrush _Background;
            public SolidColorBrush Background
            {
                get
                {
                    if (_Background == null)
                    {
                        _Background = new SolidColorBrush(BackgroundColor);
                    }
                    return _Background;
                }
            }

            public Color BackgroundColor
            {
                get
                {
                    string hex = string.Empty;
                    TryGetValue(_BackgroundColor, out hex, Colors.DarkGray.ToString());
                    return ToColor(hex);
                }
                set
                {
                    Set(_BackgroundColor, value.ToString());
                    _Background = new SolidColorBrush(BackgroundColor);
                    OnPropertyChanged("Background");
                    OnPropertyChanged("FolderBackgroundColor");
                    OnPropertyChanged("FolderBackground");
                }
            }

            private SolidColorBrush _Foreground;
            public SolidColorBrush Foreground
            {
                get
                {
                    if (_Foreground == null)
                    {
                        _Foreground = new SolidColorBrush(ForegroundColor);
                    }
                    return _Foreground;
                }
            }

            public Color ForegroundColor
            {
                get
                {
                    string hex = string.Empty;
                    TryGetValue(_ForegroundColor, out hex, FOREGROUND_COLOR_DEFAULT);
                    return ToColor(hex);
                }
                set
                {
                    Set(_ForegroundColor, value.ToString());
                    _Foreground = new SolidColorBrush(ForegroundColor);
                    OnPropertyChanged("Foreground");
                }
            }

            private SolidColorBrush _SubForeground;
            public SolidColorBrush SubForeground
            {
                get
                {
                    if (_SubForeground == null)
                    {
                        _SubForeground = new SolidColorBrush(SubForegroundColor);
                    }
                    return _SubForeground;
                }
            }

            public Color SubForegroundColor
            {
                get
                {
                    string hex = string.Empty;
                    TryGetValue(_SubForegroundColor, out hex, Colors.DodgerBlue.ToString());
                    return ToColor(hex);
                }
                set
                {
                    Set(_SubForegroundColor, value.ToString());
                    _Foreground = new SolidColorBrush(SubForegroundColor);
                    OnPropertyChanged("SubForeground");
                }
            }

            private SolidColorBrush _FolderBackground;
            public SolidColorBrush FolderBackground
            {
                get
                {
                    if (_FolderBackground == null)
                    {
                        _FolderBackground = new SolidColorBrush(FolderBackgroundColor);
                    }
                    return _FolderBackground;
                }
            }

            public Color FolderBackgroundColor
            {
                get
                {
                    Windows.UI.Color color;
                    color.A = 64; //약 25%
                    color.R = (byte)(BackgroundColor.R * 0.75);
                    color.G = (byte)(BackgroundColor.G * 0.75);
                    color.B = (byte)(BackgroundColor.B * 0.75);
                    return color;
                }
            }

            public bool UseSaveFontInMkv
            {
                get
                {
                    bool value = false;
                    TryGetValue(_UseSaveFontInMkv, out value, false);
                    return value;
                }
                set { Set(_UseSaveFontInMkv, value); }
            }

            public string HardwareBackButtonAction
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(_HardwareBackButtonAction, out value, CCPlayer.WP81.Models.HardwareBackButtonAction.MoveToUpperFolder.ToString());
                    return value;
                }
                set { Set(_HardwareBackButtonAction, value); }
            }

            public int StartUpSection
            {
                get
                {
                    int value = 0;
                    TryGetValue(_StartUpSection, out value, 0);
                    return value;
                }
                set { Set(_StartUpSection, value); }
            }

            public bool UseAllVideoSection
            {
                get
                {
                    bool value = true;
                    TryGetValue(_UseAllVideoSection, out value, true);
                    return value;
                }
                set { Set(_UseAllVideoSection, value); }
            }
        };

        public class PlaybackSetting :  BaseSetting
        {
            private const string _IsRotationLock = "_IsRotationLock";
            private const string _LastPlaybackOrientation = "_LastPlaybackOrientation";
            private const string _UseFlipToPause = "_UseFlipToPause";
            private const string _SeekTimeInterval = "_SeekTimeInterval";
            private const string _ForceUseMediaElement = "_ForceUseMediaElement";
            private const string _UseOptimizationEntryModel = "_UseOptimizationEntryModel";
            private const string _RemoveCompletedVideo = "_RemoveCompletedVideo";
            private const string _UseConfirmNextPlay = "_UseConfirmNextPlay";
            private const string _UseGpuShader = "_UseGpuShader";

            public PlaybackSetting(Settings settings)
                : base(settings, "2") 
            {
                settings.Playback = this;
            }

            public void ResetDefaultValue()
            {
                Set(_IsRotationLock, false);
                Set(_UseFlipToPause, VersionHelper.IsPaidFeature);
                Set(_SeekTimeInterval, 0);
                Set(_LastPlaybackOrientation, (int)SimpleOrientation.Rotated90DegreesCounterclockwise);
                Set(_ForceUseMediaElement, false);
                Set(_UseOptimizationEntryModel, true);
                Set(_RemoveCompletedVideo, false);
                Set(_UseConfirmNextPlay, false);
                Set(_UseGpuShader, true);
            }

            public bool IsRotationLock
            {
                get
                {
                    bool value = false;
                    TryGetValue(_IsRotationLock, out value, false);
                    return value;
                }
                set { Set(_IsRotationLock, value); }
            }


            public bool UseFlipToPause
            {
                get 
                {
                    bool defaultValue = VersionHelper.IsPaidFeature;
                    bool value = defaultValue;
                    TryGetValue(_UseFlipToPause, out value, defaultValue);
                    return value;
                }
                set { Set(_UseFlipToPause, value); }
            }

            public int SeekTimeInterval
            {
                get
                {
                    int value = 0;
                    TryGetValue(_SeekTimeInterval, out value, 0);
                    return value;
                }
                set { Set(_SeekTimeInterval, value); }
            }

            public SimpleOrientation LastPlaybackOrientation
            {
                get
                {
                    //SimpleOrientation value = SimpleOrientation.Rotated90DegreesCounterclockwise;
                    int value = 0;
                    TryGetValue(_LastPlaybackOrientation, out value, (int)SimpleOrientation.Rotated90DegreesCounterclockwise);
                    return (SimpleOrientation)value;
                }
                set { Set(_LastPlaybackOrientation, (int)value); }
            }

            public bool ForceUseMediaElement
            {
                get
                {
                    bool value = false;
                    TryGetValue(_ForceUseMediaElement, out value, false);
                    return value;
                }
                set { Set(_ForceUseMediaElement, value); }
            }

            public bool UseOptimizationEntryModel
            {
                get
                {
                    bool value = true;
                    TryGetValue(_UseOptimizationEntryModel, out value, true);
                    return value;
                }
                set { Set(_UseOptimizationEntryModel, value); }
            }

            public bool RemoveCompletedVideo
            {
                get
                {
                    bool value = false;
                    TryGetValue(_RemoveCompletedVideo, out value, false);
                    return value;
                }
                set { Set(_RemoveCompletedVideo, value); }
            }

            public bool UseConfirmNextPlay
            {
                get
                {
                    bool value = false;
                    TryGetValue(_UseConfirmNextPlay, out value, false);
                    return value;
                }
                set { Set(_UseConfirmNextPlay, value); }
            }

            public bool UseGpuShader
            {
                get
                {
                    bool value = true;
                    TryGetValue(_UseGpuShader, out value, true);
                    return value;
                }
                set { Set(_UseGpuShader, value); }
            }
        };

        public class SubtitleSetting : BaseSetting
        {
            private const string _DefaultCharset = "_DefaultCharset";
            private const string _FontSize = "_FontSize";
            private const string _FontStyleOverride = "_FontStyleOverride";
            private const string _FontStyle = "_FontStyle";
            private const string _FontWeight = "_FontWeight";
            private const string _UseFontShadow = "_UseFontShadow";
            private const string _UseFontOutline = "_UseFontOutline";
            private const string _FontFamily = "_FontFamily";
            private const string _ForegroundColor = "_SubtitleForegroundColor";
            private const string _UseBackground = "_UseBackground";
            private const string _VerticalAlignment = "_VerticalAlignment";
            private const string _TranslateY = "_TranslateY";

            private string DefaultFontStyle = Windows.UI.Text.FontStyle.Normal.ToString();
            private ushort DefaultFontWeight = Windows.UI.Text.FontWeights.Medium.Weight;
            private string DefaultVerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Bottom.ToString();
            private double DefaultTranslateY = -24;

            public SubtitleSetting(Settings settings) : base(settings, "3") 
            {
                settings.Subtitle = this;

                SubtitleTextAlignment = TextAlignment.Center;
                _Background = SUBTITLE_BACKGROUND_DEFAULT;
                _NotOverridingFontStyle = DefaultFontStyle;
                _NotOverridingFontWeight = Windows.UI.Text.FontWeights.Medium.Weight;
                _NotOverridingForeground = new SolidColorBrush(ToColor(SUBTITLE_COLOR_DEFAULT));
                _NotOverridingForegroundColor = ToColor(SUBTITLE_COLOR_DEFAULT);
            }
            
            public void ResetDefaultvalue()
            {
                Set(_DefaultCharset, Lime.Encoding.CodePage.AUTO_DETECT_VALUE);
                Set(_FontSize, Settings.FONT_SIZE_DEFAULT);
                Set(_FontStyleOverride, false);
                Set(_FontStyle, DefaultFontStyle);
                Set(_FontWeight, DefaultFontWeight);
                Set(_UseFontShadow, true);
                Set(_UseFontOutline, true);
                Set(_FontFamily, Settings.FONT_FAMILY_DEFAUT);
                Set(_UseBackground, false);
                Set(_ForegroundColor, SUBTITLE_COLOR_DEFAULT);
                Set(_VerticalAlignment, DefaultVerticalAlignment);
                Set(_TranslateY, DefaultTranslateY);
            }

            public void ResetNoOverridingValues()
            {
                Background = SUBTITLE_BACKGROUND_DEFAULT;
                NotOverridingFontStyle = DefaultFontStyle;
                NotOverridingFontWeight = Windows.UI.Text.FontWeights.Medium.Weight;
                NotOverridingForegroundColor = ToColor(SUBTITLE_COLOR_DEFAULT);
            }

            public int DefaultCharset
            {
                get
                {
                    int value = 0;
                    TryGetValue(_DefaultCharset, out value, Lime.Encoding.CodePage.AUTO_DETECT_VALUE);
                    return value;
                }
                set { Set<int>(_DefaultCharset, value); }
            }

            public double FontSize
            {
                get
                {
                    double value = 0;
                    TryGetValue(_FontSize, out value, Settings.FONT_SIZE_DEFAULT);
                    return value;
                }
                set { Set<double>(_FontSize, value); }
            }
            
            public bool UseFontShadow
            {
                get
                {
                    bool value = false;
                    TryGetValue(_UseFontShadow, out value, true);
                    return value;
                }
                set { Set<bool>(_UseFontShadow, value); }
            }

            public bool UseFontOutline
            {
                get
                {
                    bool value = false;
                    TryGetValue(_UseFontOutline, out value, true);
                    return value;
                }
                set { Set<bool>(_UseFontOutline, value); }
            }

            public string FontFamily 
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(_FontFamily, out value, Settings.FONT_FAMILY_DEFAUT);
                    return value;
                }
                set 
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        Set<string>(_FontFamily, value); 
                    }
                }
            }

            public bool UseBackground
            {
                get
                {
                    bool value = false;
                    TryGetValue(_UseBackground, out value, false);
                    return value;
                }
                set { Set<bool>(_UseBackground, value); }
            }

            public bool FontStyleOverride
            {
                get
                {
                    bool value = false;
                    TryGetValue(_FontStyleOverride, out value, false);
                    return value;
                }
                set
                {
                    Set<bool>(_FontStyleOverride, value);
                    OnPropertyChanged("FontStyle");
                    OnPropertyChanged("FontWeight");
                    OnPropertyChanged("Foreground");
                }
            }

            private string _NotOverridingFontStyle;
            public string NotOverridingFontStyle
            {
                get
                {
                    return _NotOverridingFontStyle;
                }
                set
                {
                    if (_NotOverridingFontStyle != value)
                    {
                        _NotOverridingFontStyle = value;
                        OnPropertyChanged("FontStyle");
                    }
                }
            }

            public string FontStyle
            {
                get
                {
                    if (!FontStyleOverride)
                    {
                        return NotOverridingFontStyle;
                    }
                    else
                    {
                        string value = string.Empty;
                        TryGetValue(_FontStyle, out value, DefaultFontStyle);
                        return value;
                    }
                }
                set { Set<string>(_FontStyle, value); }
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

            private ushort _NotOverridingFontWeight;
            public ushort NotOverridingFontWeight 
            { 
                set
                {
                    if (_NotOverridingFontWeight != value)
                    {
                        _NotOverridingFontWeight = value;
                        OnPropertyChanged("FontWeight");
                    }
                }
            }

            public ushort FontWeight
            {
                get
                {
                    if (!FontStyleOverride)
                    {
                        return _NotOverridingFontWeight;
                    }
                    else
                    {
                        ushort value = 0;
                        TryGetValue(_FontWeight, out value, DefaultFontWeight);
                        return value;
                    }
                }
                set { Set<ushort>(_FontWeight, value); }
            }

            private SolidColorBrush _NotOverridingForeground;
            private SolidColorBrush _Foreground;
            public SolidColorBrush Foreground
            {
                get
                {
                    if (!FontStyleOverride)
                    {
                        return _NotOverridingForeground;
                    }
                    else
                    {
                        if (_Foreground == null)
                        {
                            _Foreground = new SolidColorBrush(ForegroundColor);
                        }
                    }
                    return _Foreground;
                }
            }

            private Color _NotOverridingForegroundColor;
            public Color NotOverridingForegroundColor
            {
                set
                {
                    if (_NotOverridingForegroundColor != value)
                    {
                        _NotOverridingForegroundColor = value;
                        _NotOverridingForeground = new SolidColorBrush(value);
                        OnPropertyChanged("Foreground");
                    }
                }
            }

            public Color ForegroundColor
            {
                get
                {
                    if (!FontStyleOverride)
                    {
                        return _NotOverridingForegroundColor;
                    }
                    else
                    {
                        string hex = string.Empty;
                        TryGetValue(_ForegroundColor, out hex, SUBTITLE_COLOR_DEFAULT);
                        return ToColor(hex);
                    }
                }
                set
                {
                    Set(_ForegroundColor, value.ToString());
                    _Foreground = new SolidColorBrush(value);
                    OnPropertyChanged("Foreground");
                }
            }

            private SolidColorBrush _Background;
            public SolidColorBrush Background
            {
                get
                {
                    return _Background;
                }
                set
                {
                    if (_Background != value)
                    {
                        _Background = value;
                        OnPropertyChanged();
                    }
                }
            }

            public string VerticalAlignment
            {
                get
                {
                    string value = string.Empty;
                    TryGetValue(_VerticalAlignment, out value, DefaultVerticalAlignment);
                    return value;
                }
                set { Set<string>(_VerticalAlignment, value); }
            }

            public double TranslateY
            {
                get
                {
                    double value = 0;
                    TryGetValue(_TranslateY, out value, DefaultTranslateY);
                    return value;
                }
                set { Set<double>(_TranslateY, value); }
            }
        };

        public GeneralSetting General { get; set; }
        public PlaybackSetting Playback { get; set; }
        public SubtitleSetting Subtitle { get; set; }

        private Setting Get(string code)
        {
            return this.FirstOrDefault(x => x.Code == code);
        }
    }
}
