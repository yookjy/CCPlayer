using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Linq;
using System.Text;
using Windows.Media.MediaProperties;
using Windows.Media.Core;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class AC3Codec : KnownCodec
    {
        private Liba52.AC3Decoder decoder;

        public AC3Codec(TrackEntry trackEntry)
            : base(trackEntry)
        {
            //new Windows.Media.MediaExtensionManager().RegisterAudioDecoder("Liba52.AC3Decoder", Guid.Parse("01DC9147-0F0F-4781-869F-39B329BE106D"), Guid.Empty);
            decoder = new Liba52.AC3Decoder();
        }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            FrameBufferData frame = null;
            frame = new FrameBufferData(decoder.Decode(data), timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            Audio audioInfo = TrackEntry.Audio;
            //var properties = AudioEncodingProperties.CreatePcm(
            //                (uint)audioInfo.SamplingFrequency,
            //                (uint)audioInfo.Channels,
            //                audioInfo.BitDepth > 0 ? (uint)audioInfo.BitDepth : 16);
            var properties = AudioEncodingProperties.CreatePcm(
                            Math.Max((UInt32)audioInfo.SamplingFrequency, (UInt32)8000),
                            2,
                            16);

            var descriptor = new AudioStreamDescriptor(properties);
            if (TrackEntry.CodecPrivate != null)
            {
                properties.SetFormatUserData(TrackEntry.CodecPrivate);
            }

            return descriptor;
        }
    }
}
