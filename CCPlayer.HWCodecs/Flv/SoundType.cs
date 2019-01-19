using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    /// <summary>
    /// Mono or stereo sound
    /// For Nellymoser: always 0
    /// For AAC: always 1
    /// </summary>
    public enum SoundType
    {
        sndMono = 0,
        sndStereo = 1,
    }
}
