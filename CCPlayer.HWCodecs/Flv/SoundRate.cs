using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    /// <summary>
    /// For AAC: always 3
    /// </summary>
    public enum SoundRate
    {
        _5kHz = 0,
        _11kHz = 1,
        _22kHz = 2,
        _44kHz = 3,
    }
}
