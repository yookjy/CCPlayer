using CCPlayer.UWP.Common.Codec;
using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Factory.Dummy.Connector
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

        public bool IsSeeking { get; set; }

        public string LanguageCode { get; set; }

        public IList<SubtitleLanguage> SubtitleLanguage
        {
            get { return null; }
        }

        public long SynchronizeTime { get; set; }

        public bool CanSynchroize => SourceType == SubtitleSourceTypes.External;

        public SubtitleSourceTypes SourceType => _SourceType;

        public object ConnectedSource => _ConnectedSource;

        public void Connect(object obj)
        {
            if (obj is int)
            {
                //내부
                _SourceType = SubtitleSourceTypes.Internal;
                _ConnectedSource = obj;
            }
            else
            {
                //외부
            }
        }

        public void Seek(long pts, int flag)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
            }
        }

        public void ConsumePacket(long pts)
        {
            if (_SourceType == SubtitleSourceTypes.External)
            {
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

                if (SubtitlePopulatedEvent != null)
                    SubtitlePopulatedEvent(this, marker, subtitleImageMap);
            });
        }

        public void SetUICoreDispatcher(CoreDispatcher dispatcher)
        {
            _UIDispatcher = dispatcher;
        }
    }
}
