using CCPlayer.UWP.Common.Interface;
using CCPlayer.UWP.FFmpeg.Subtitle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPlayer.UWP.Common.Codec;
using Windows.UI.Core;
using Windows.Storage.Streams;
using Windows.Data.Json;
using Windows.UI.Xaml.Media;
using Windows.Foundation.Collections;

namespace CCPlayer.UWP.Factory.Connector
{
    public sealed class SubtitleDecoderConnector : ISubtitleDecoderConnector
    {
        private SubtitleDecoderConnector() { }

        public SubtitleDecoderConnector(CoreDispatcher dispatcher) 
        {
            _SelectedCodePage = -1;
            _DefaultCodePage = 65001;
            SetUICoreDispatcher(dispatcher);
        }

        public event SubtitlePopulatedEventHandler SubtitlePopulatedEvent;

        private SubtitleSourceTypes _SourceType;

        private CoreDispatcher _UIDispatcher;

        private object _ConnectedSource;

        private int _SelectedCodePage;

        private int _DefaultCodePage;
        
        public int SelectedCodePage
        {
            get { return _SelectedCodePage; }
            set
            {
                if (_SelectedCodePage != value)
                {
                    _SelectedCodePage = value;
                }
            }
        }

        public int DefaultCodePage
        {
            get { return _DefaultCodePage; }
            set
            {
                if (_DefaultCodePage != value)
                {
                    _DefaultCodePage = value;
                }
            }
        }

        public bool IsSeeking
        {
            get { return (bool)GetExtenralSourceGetter((src) => src.IsSeeking, false); }
            set { SetExternalSourceSetter((src) => src.IsSeeking = value); }
        }
        
        public string LanguageCode
        {
            get { return (string)GetExtenralSourceGetter((src) => src.SelectedSubLanguageCode, null); }
            set { SetExternalSourceSetter((src) => src.SelectedSubLanguageCode = value); }
        }

        public IList<SubtitleLanguage> SubtitleLanguage
        {
            get { return (IList<SubtitleLanguage>)GetExtenralSourceGetter((src) => src.SubtitleLanguages, null); }
        }

        public long SynchronizeTime
        {
            get { return (long)GetExtenralSourceGetter((src) => src.SynchronizeTime, 0); }
            set { SetExternalSourceSetter((src) => src.SynchronizeTime = value); }
        }

        public bool CanSynchroize => SourceType == SubtitleSourceTypes.External;

        public SubtitleSourceTypes SourceType => _SourceType;

        public object ConnectedSource => _ConnectedSource;

        public void SetUICoreDispatcher(CoreDispatcher dispatcher)
        {
            _UIDispatcher = dispatcher;
        }

        public void Connect(object obj)
        {
            if (obj is PropertySet)
            {
                var ps = obj as PropertySet;
                if (ps.ContainsKey("Key"))
                {
                    object key = ps["Key"];
                    if (key is int)
                    {
                        //내부
                        _SourceType = SubtitleSourceTypes.Internal;
                        _ConnectedSource = key;
                    }
                    else
                    {
                        PropertySet options = null;
                        if (ps.ContainsKey("Options"))
                        {
                            options = ps["Options"] as PropertySet;
                        }

                        //외부
                        _SourceType = SubtitleSourceTypes.External;
                        //키가 URI의 경우 (외부 자막)
                        if (key is string)
                        {
                            _ConnectedSource = ExternalSubtitleSource.CreateExternalSubtitleSourceFromUri(this, key as string, options);
                        }
                        //키가 파일 스트림의 경우 (외부 자막)
                        else if (key is IRandomAccessStream)
                        {
                            _ConnectedSource = ExternalSubtitleSource.CreateExternalSubtitleSourceFromStream(this, key as IRandomAccessStream, null);
                        }
                    }
                }
            }
            
        }

        public void Seek(long pts, int flag)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
                var src = _ConnectedSource as ExternalSubtitleSource;
                src.Seek(pts, flag);
            }
        }

        public void ConsumePacket(long pts)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
                SetExternalSourceSetter((src) => src.ConsumePacket(0, pts));
            }
        }

        public async void PopulatePacket(JsonObject subPkt, IDictionary<String, ImageData> subtitleImageMap)
        {
            await _UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var hns = (long)(subPkt.GetNamedNumber("Pts") + (subPkt.GetNamedNumber("StartDisplayTime")));
                var ts = new TimeSpan(hns);

                var marker = new TimelineMarker();
                marker.Time = ts;
                marker.Text = subPkt.Stringify();

                SubtitlePopulatedEvent?.Invoke(this, marker, subtitleImageMap);
            });
        }

        object GetExtenralSourceGetter(Func<ExternalSubtitleSource, object> func, object defaultValue)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
                var src = _ConnectedSource as ExternalSubtitleSource;
                if (src != null)
                    return func.Invoke(src);
            }
            return defaultValue;
        }
        void SetExternalSourceSetter(Action<ExternalSubtitleSource> action)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
                var src = _ConnectedSource as ExternalSubtitleSource;
                if (src != null)
                    action.Invoke(src);
            }
        }
    }
}
