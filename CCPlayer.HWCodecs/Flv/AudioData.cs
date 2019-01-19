using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public class AudioData
    {
        public AudioData(uint mediaInfo, byte[] data)
        {
            this.SoundFormat = (SoundFormat)(mediaInfo >> 4);
            this.SoundRate = (SoundRate)(mediaInfo >> 2 & 0x03);
            this.SoundSize = (SoundSize)(mediaInfo >> 1 & 0x01);
            this.SoundType = (SoundType)(mediaInfo & 0x01);

            switch(this.SoundFormat)
            {
                case FlvParser.SoundFormat.AAC:
                    this.SoundData = new AACAudioData(data);
                    break;
                case FlvParser.SoundFormat.ADPCM:
                    this.SoundData = new AudioFormatData(data);
                    break;
                case FlvParser.SoundFormat.MP3:
                    this.SoundData = new AudioFormatData(data);
                    break;
            }

            
        }

        public SoundFormat SoundFormat
        {
            get;
            private set;
        }

        public SoundRate SoundRate
        {
            get;
            private set;
        }

        public SoundSize SoundSize
        {
            get;
            private set;
        }

        public SoundType SoundType
        {
            get;
            private set;
        }

        public AudioFormatData SoundData
        {
            get;
            private set;
        }
    }
}
