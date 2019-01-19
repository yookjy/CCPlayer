using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;


namespace MediaParsers.FlvParser
{
    public class AVCVideoPacket
    {
        public AVCVideoPacket(byte[] buffer)
        {
            this.AVCPacketType = (AVCPacketType)buffer[0];

            byte[] x = new byte[4];
            x[1] = buffer[1];
            x[2] = buffer[2];
            x[3] = buffer[3];
            this.CompositionTime = (int)BitConverterBE.ToUInt32(x, 0);

            byte[] data = new byte[buffer.Length - 4];
            Buffer.BlockCopy(buffer, 4, data, 0, data.Length);
            if (this.AVCPacketType == AVCPacketType.AVCSequenceHeader)
                AVCDecoderConfigurationRecord = data;
            else
                NALUs = data;
        }
        public AVCPacketType AVCPacketType
        {
            get;
            private set;
        }

        public int CompositionTime
        {
            get;
            private set;
        }

        public byte[] AVCDecoderConfigurationRecord { get; private set; }

        public byte[] NALUs { get; private set; }
    }
}
