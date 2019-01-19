using CCPlayer.UWP.Common.Codec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Interface
{
    public interface IAVDecoderConnector
    {
        void SetResult(DecoderTypes resDecoderType, DecoderStates status);

        IList<CodecInformation> CodecInformationList { get; set; }
        int EnforceVideoStreamId { get; set; }
        int EnforceAudioStreamId { get; set; }
		long AudioSyncMilliSeconds { get; set; }
        bool UseGPUShader { get; set; }
        DecoderPayload Payload { get; }
        double WindowsVersion { get; set; }
		DecoderTypes ReqDecoderType { get; set; }
        DecoderTypes ResDecoderType { get; }
		double AudioVolumeBoost { get; set; }
    }
}
