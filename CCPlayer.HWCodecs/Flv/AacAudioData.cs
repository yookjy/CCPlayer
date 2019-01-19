using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public class AACAudioData : AudioFormatData
    {
        public AACAudioData(byte[] buffer) : base(buffer)
        {
            this.AACPacketType = (AACPacketType)buffer[0];
            byte[] data = new byte[buffer.Length - 1];
            Buffer.BlockCopy(buffer, 1, data, 0, data.Length);

            if (this.AACPacketType == AACPacketType.AACSequenceHeader)
                AudioSpecificConfig = data;
            else
                RawAACFrameData = data;
        }

        public AACPacketType AACPacketType
        {
            get;
            private set;
        }

        public byte[] AudioSpecificConfig { get; private set; }

        public byte[] RawAACFrameData { get; private set; }

    }
}
