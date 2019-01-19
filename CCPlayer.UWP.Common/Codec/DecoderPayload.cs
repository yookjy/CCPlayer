using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Codec
{
    public struct DecoderPayload
    {
        public DecoderTypes ReqDecoderType;
        public DecoderTypes ResDecoderType;
        public DecoderStates Status;
    }
}
