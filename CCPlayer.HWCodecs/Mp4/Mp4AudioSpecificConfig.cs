using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Mp4
{
    public class Mp4AudioSpecificConfig
    {
        public static uint GetSamplingFrequency(int channelIndex)
        {
            uint samplingFrequency = 0;
            switch(channelIndex)
            {
                case 0: samplingFrequency = 96000; break;
                case 1: samplingFrequency = 88200; break;
                case 2: samplingFrequency = 64000; break;
                case 3: samplingFrequency = 48000; break;
                case 4: samplingFrequency = 44100; break;
                case 5: samplingFrequency = 32000; break;
                case 6: samplingFrequency = 24000; break;
                case 7: samplingFrequency = 22050; break;
                case 8: samplingFrequency = 16000; break;
                case 9: samplingFrequency = 12000; break;
                case 10: samplingFrequency = 11025; break;
                case 11: samplingFrequency = 8000; break;
                case 12: samplingFrequency = 7350; break;

            }
            return samplingFrequency;
        }
    }
}
