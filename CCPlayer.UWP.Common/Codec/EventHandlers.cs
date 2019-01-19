using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Common.Codec
{
    public delegate void SubtitlePopulatedEventHandler(ISubtitleDecoderConnector sender, TimelineMarker timelineMarker, IDictionary<string, ImageData> subtitleImageMap);

    public delegate void AttachmentPopulatedEventHandler(IAttachmentDecoderConnector sender, AttachmentData args);

    public delegate void AttachmentCompletedEventHandler(IAttachmentDecoderConnector sender, object args);

}
