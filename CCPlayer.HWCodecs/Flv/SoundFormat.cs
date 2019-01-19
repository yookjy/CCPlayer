using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public enum SoundFormat
    {
        None = 0,
        ADPCM = 1,
        MP3 = 2,
        AAC = 10,
        Speex = 11,
    }
}
