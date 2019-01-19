using CCPlayer.UWP.Common.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace CCPlayer.UWP.Common.Interface
{
    public interface IMediaInformation
    {
        IAsyncOperation<IBuffer> GetBitmapPixelBuffer(Size size);
        byte[] GetThumbnailPixelBytes(Size size);
        DecoderTypes GetRecommendedDecoderType(int videoStreamIndex, int audioStreamIndex);

        IReadOnlyList<CodecInformation> CodecInformationList { get; }
        string ContainerName { get; set; }
        string ContainerFullName { get; set; }
        string Title { get; set; }
        int DefaultVideoStreamIndex { get; }
        int DefaultAudioStreamIndex { get; }
        int DefaultSubtitleStreamIndex { get; }
        TimeSpan NaturalDuration { get; }
        DecoderTypes RecommendedDecoderType { get; set; }
    }
}
