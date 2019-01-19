using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Codec
{
    public sealed class AttachmentData
    {
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public byte[] BinaryData { get; set; }
    }
}
