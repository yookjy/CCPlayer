using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public enum AVCPacketType
    {
        AVCSequenceHeader = 0,
        AVCNALU = 1,
        AVCEndOfSequence = 2,
    }
}
