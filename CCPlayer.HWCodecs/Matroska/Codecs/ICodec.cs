using CCPlayer.HWCodecs.Matroska;
using CCPlayer.HWCodecs.Matroska.EBML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public interface ICodec
    {
        string CodecID { get; }

        string CodecName { get; }
        
        bool IsSupported { get; }

        bool IsNeedLicense { get; }

        string LicenseCompany { get; }

        TrackTypes CodecType { get; }

        ulong TrackNumber { get; }
    }
}
