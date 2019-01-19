using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Codec
{
    public sealed class ImageData
    {
		public int CodecId { get; set; }
        public byte[] ImagePixelData { get; set; }
    }
}
