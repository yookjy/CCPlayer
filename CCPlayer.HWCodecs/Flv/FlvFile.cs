using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public class FlvFile
    {
        public Stream Stream { get; private set; }

        public string Path { get; set; }

        public FlvFile(Stream stream, ref long offset)
        {
            this.Stream = stream;
            this.FlvHeader = new FlvHeader(stream, ref offset);
            this.FlvFileBody = new FlvFileBody(stream, ref offset);
        }

        public FlvHeader FlvHeader
        {
            get;
            private set;
        }

        public FlvFileBody FlvFileBody
        {
            get;
            private set;
        }
    }
}
