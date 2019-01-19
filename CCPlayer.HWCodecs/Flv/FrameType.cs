using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public enum FrameType
    {
        Keyframe = 1,
        InterFrame = 2,
        /// <summary>
        /// H.263 only
        /// </summary>
        DisposableInterFrame = 3,
        /// <summary>
        /// reserved for server use only
        /// </summary>
        GeneratedKeyframe = 4,
        VideoInfoOrCommandFrame = 5,
    }
}
