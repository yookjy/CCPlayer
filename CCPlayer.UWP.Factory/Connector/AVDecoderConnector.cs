using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPlayer.UWP.Common.Codec;

namespace CCPlayer.UWP.Factory.Connector
{
    public sealed class AVDecoderConnector : IAVDecoderConnector
    {
        public AVDecoderConnector()
        {
            UseGPUShader = true;
            EnforceAudioStreamId = -1;
            CodecInformationList = new List<CodecInformation>();
        }

        private DecoderPayload _Payload;

        public long AudioSyncMilliSeconds { get; set; }

        public double AudioVolumeBoost { get; set; }

        public IList<CodecInformation> CodecInformationList { get; set; }

        public int EnforceAudioStreamId { get; set; }

        public int EnforceVideoStreamId { get; set; }

        public DecoderPayload Payload => _Payload;

        public DecoderTypes ReqDecoderType
        {
            get { return _Payload.ReqDecoderType; }
            set
            {
                CodecInformationList.Clear();

                _Payload.ReqDecoderType = value;
                _Payload.ResDecoderType = value;
                _Payload.Status = DecoderStates.Requested;
            }
        }

        public DecoderTypes ResDecoderType => _Payload.ResDecoderType;

        public bool UseGPUShader { get; set; }

        public double WindowsVersion { get; set; }

        public void SetResult(DecoderTypes resDecoderType, DecoderStates status)
        {
            _Payload.ResDecoderType = resDecoderType;
            _Payload.Status = status;
        }
    }
}
