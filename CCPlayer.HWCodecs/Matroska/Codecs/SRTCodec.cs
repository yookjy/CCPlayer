using CCPlayer.HWCodecs.Matroska.Common;
using CCPlayer.HWCodecs.Matroska.EBML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class SRTCodec : KnownCodec
    {
        public SRTCodec(TrackEntry trackEntry) : base(trackEntry) { }
        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            FrameBufferData frame = new FrameBufferData(data, timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            throw new NotImplementedException();
        }
    }
}
