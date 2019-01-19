using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaParsers.FlvParser
{
    public class AudioFormatData
    {
        public byte[] RawData { get; set; }
        public AudioFormatData(byte[] data)
        {
            this.RawData = data;
        }
    }
}
