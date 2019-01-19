using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Codec
{
    public sealed class CodecInformation
    {
        public CodecInformation()
        {
            Title = string.Empty;
            Language = string.Empty;
            CodecName = string.Empty;
            CodecLongName = string.Empty;
            CodecLicense = string.Empty;
            CodecProfileName = string.Empty;
        }

        public int StreamId { get; set; }
        public string Title { get; set; }
        public string Language { get; set; }
        public bool IsBestStream { get; set; }
        public bool Is10BitVideoColor { get; set; }
        public bool IsHWAcceleration { get; set; }
        public int  CodecTag { get; set; }
        public int  CodecType { get; set; }
		/*AVMEDIA_TYPE_UNKNOWN = -1,  ///< Usually treated as AVMEDIA_TYPE_DATA
		AVMEDIA_TYPE_VIDEO,
		AVMEDIA_TYPE_AUDIO,
		AVMEDIA_TYPE_DATA,          ///< Opaque data information usually continuous
		AVMEDIA_TYPE_SUBTITLE,
		AVMEDIA_TYPE_ATTACHMENT,    ///< Opaque data information usually sparse
		AVMEDIA_TYPE_NB*/
		public int  CodecId { get; set; }
		public string CodecName { get; set; }
		public string CodecLongName { get; set; }
		public string CodecLicense { get; set; }
		public int   CodecProfileId { get; set; }
		public string CodecProfileName { get; set; }
		public int  Width { get; set; }
		public int  Height { get; set; }
		public int  Fps { get; set; }
		public int  SampleRate { get; set; }
		public ushort  Channels { get; set; }
		public int  Bps { get; set; }
		public bool IsBasicStream { get; set; } //기본 스트림으로 MF에서 자동 처리가능한 오디오 스트림을 나타낸다.
        public DecoderTypes DecoderType { get; set; } //생성된 디코더의 타입을 나타낸다. 
    }
}
