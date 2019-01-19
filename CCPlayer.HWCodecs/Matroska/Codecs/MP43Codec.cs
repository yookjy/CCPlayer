using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class MP43Codec : KnownCodec
    {
        public MP43Codec(EBML.TrackEntry trackEntry)
            : base(trackEntry) 
        {
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            var properties = VideoEncodingProperties.CreateH264();
            properties.Subtype = MediaEncodingSubtypes.Rgb24;
            properties.Width = (uint)TrackEntry.Video.PixelWidth;
            properties.Height = (uint)TrackEntry.Video.PixelHeight;
            
            var descriptor = new VideoStreamDescriptor(properties);
            return descriptor;
        }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {

            if (keyFrame)
            {
                //ms.Write(NALUnitHeader, 0, NALUnitHeader.Length);
            }

            var frame = new FrameBufferData(data, timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }
    }
}
