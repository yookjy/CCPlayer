using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Linq;
using System.Text;
using Windows.Media.MediaProperties;
using Windows.Media.Core;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class DTSCodec : KnownCodec
    {
#if OMEGA
        libdts.DTSDecoder decoder;
#endif
        public DTSCodec(TrackEntry trackEntry)
            : base(trackEntry)
        {
#if OMEGA
            //new Windows.Media.MediaExtensionManager().RegisterAudioDecoder("libdts.DTSDecoder", Guid.Parse("54123B68-7506-48EE-A4CB-8027E23D5088"), Guid.Empty);
            decoder = new libdts.DTSDecoder();
#endif
        }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            FrameBufferData frame = null;
#if OMEGA
            frame = new FrameBufferData(decoder.Decode(data), timeCode, duration, keyFrame, discardable, invisible);
#endif
            //FrameBufferData frame = new FrameBufferData(data, timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            Audio audioInfo = TrackEntry.Audio;
            //var properties = AudioEncodingProperties.CreatePcm(
            //                (uint)audioInfo.SamplingFrequency,
            //                (uint)audioInfo.Channels,
            //                audioInfo.BitDepth > 0 ? (uint)audioInfo.BitDepth : 24);

            var properties = AudioEncodingProperties.CreatePcm(
                           (uint)audioInfo.SamplingFrequency,
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
