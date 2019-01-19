using CCPlayer.UWP.Common.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Common.Interface
{
    public interface ISubtitleDecoderConnector
    {
        event SubtitlePopulatedEventHandler SubtitlePopulatedEvent;

        int SelectedCodePage { get; set; }

        int DefaultCodePage { get; set; }

        long SynchronizeTime { get; set; }

        bool IsSeeking { get; set; }

        IList<SubtitleLanguage> SubtitleLanguage { get; }

        string LanguageCode { get; set; }

        bool CanSynchroize { get; }

        object ConnectedSource { get; }

        SubtitleSourceTypes SourceType { get; }

        void SetUICoreDispatcher(CoreDispatcher dispatcher);

        void Connect(object obj);

        void Seek(long pts, int flag);

        void ConsumePacket(long pts);

        void PopulatePacket(JsonObject subPkt, IDictionary<string, ImageData> subtitleImageMap);
    }
}
