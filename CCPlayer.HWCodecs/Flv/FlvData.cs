using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaParsers.FlvParser
{
    public class FlvData
    {
        protected Stream stream;

        protected long offset;

        protected long length;

        public FlvData(Stream stream, long offset, long length)
        {
            this.stream = stream;
            this.offset = offset;
            this.length = length;
        }
    }
}
