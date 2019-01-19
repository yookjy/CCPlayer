using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaParsers.FlvParser
{
    public class FlvHeader
    {
        public FlvHeader(Stream stream, ref long offset)
        {
            var f = stream.ReadUInt8(ref offset);
            var l = stream.ReadUInt8(ref offset);
            var v = stream.ReadUInt8(ref offset);
            this.Signature = string.Concat((char)f, (char)l, (char)v);

            this.Version = stream.ReadUInt8(ref offset);

            this.TypeFlags = (TypeFlags)stream.ReadUInt8(ref offset);

            this.DataOffset = stream.ReadUInt32(ref offset);
        }

        public string Signature
        {
            get;
            private set;
        }

        public uint Version
        {
            get;
            private set;
        }

        public uint DataOffset
        {
            get;
            private set;
        }

        public TypeFlags TypeFlags
        {
            get;
            private set;
        }
    }
}
