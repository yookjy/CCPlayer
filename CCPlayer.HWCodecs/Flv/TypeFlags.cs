using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public enum TypeFlags
    {
        Reserved = 0,
        AudioVideo = 5,
        Audio = 4,
        Video = 1,
    }
}
