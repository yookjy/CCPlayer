using CCPlayer.HWCodecs.Matroska.EBML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class UnkownCodec : ICodec
    {
        private EBML.TrackEntry _TrackEntry;

        public UnkownCodec(EBML.TrackEntry trackEntry, string codecName,  string licenseCompany)
        {
            this._TrackEntry = trackEntry;
            this._LicenseCompany = licenseCompany;
            this._CodecName = string.IsNullOrEmpty(trackEntry.CodecName) ? codecName : trackEntry.CodecName;
        }

        public string CodecID
        {
            get { return _TrackEntry.CodecID; }
        }

        private string _CodecName;
        public string CodecName
        {
            get { return _CodecName; }
        }

        public bool IsSupported
        {
            get { return false; }
        }

        public bool IsNeedLicense
        {
            get { return !string.IsNullOrEmpty(LicenseCompany); }
        }

        private string _LicenseCompany;
        public string LicenseCompany
        {
            get { return _LicenseCompany; }
        }

        public TrackTypes CodecType
        {
            get { return _TrackEntry.TrackType; }
        }

        public ulong TrackNumber
        {
            get { return _TrackEntry.TrackNumber; }
        }
    }
}
