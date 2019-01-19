using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Media.Core;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class PCMCodec : KnownCodec
    {
        public PCMCodec(TrackEntry trackEntry) : base(trackEntry) { }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            FrameBufferData frame = new FrameBufferData(data, timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            Audio audioInfo = TrackEntry.Audio;
            var properties = AudioEncodingProperties.CreatePcm(
                            (uint)audioInfo.SamplingFrequency,
                            (uint)audioInfo.Channels,
                            (uint)audioInfo.BitDepth);

            var descriptor = new AudioStreamDescriptor(properties);
            if (TrackEntry.CodecPrivate != null)
            {
                properties.SetFormatUserData(TrackEntry.CodecPrivate);
            }
            return descriptor;
        }
    }
}
