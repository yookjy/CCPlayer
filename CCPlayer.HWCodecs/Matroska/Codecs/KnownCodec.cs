using CCPlayer.HWCodecs.Matroska.Common;
using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.MKV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public abstract class KnownCodec : ICodec
    {
        public EBML.TrackEntry TrackEntry { get; private set; }

        public byte[] CompressHeader { get; private set; }

        public KnownCodec(EBML.TrackEntry trackEntry)
        {
            TrackEntry = trackEntry;
            
            if (TrackEntry.ContentEncodings != null && TrackEntry.ContentEncodings.ContentEncoding.Count > 0)
            {
                var ce = TrackEntry.ContentEncodings.ContentEncoding[0];
                if (ce.ContentEncodingType == 0)
                {
                    if (ce.ContentCompression != null && ce.ContentCompression.ContentCompAlgo == 3)
                    {
                        CompressHeader = ce.ContentCompression.ContentCompSettings;
                    }
                }
            }
        }

        public string CodecID
        {
            get { return TrackEntry.CodecID; }
        }

        public string CodecName
        {
            get { return TrackEntry.CodecName; }
        }

        public bool IsSupported
        {
            get { return true; }
        }

        public bool IsNeedLicense
        {
            get { return false; }
        }

        public string LicenseCompany
        {
            get { return string.Empty; }
        }

        public TrackTypes CodecType
        {
            get { return TrackEntry.TrackType; }
        }
        public ulong TrackNumber
        {
            get { return TrackEntry.TrackNumber; }
        }

        public abstract FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible);
        public abstract IMediaStreamDescriptor CreateMediaStreamDescriptor();

        public IMediaStreamDescriptor MediaStreamDescriptor 
        { 
            get
            {
                return CreateMediaStreamDescriptor();
            }
        }

    }
}
