using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public enum TagType
    {
        None = 0,
        Audio = 8,
        Video = 9,
        ScriptData = 18,
    }
}
