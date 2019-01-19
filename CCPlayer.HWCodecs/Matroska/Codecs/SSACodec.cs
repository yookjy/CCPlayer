using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class SSACodec: KnownCodec
    {
        public SSACodec(TrackEntry trackEntry) : base(trackEntry) { }
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
