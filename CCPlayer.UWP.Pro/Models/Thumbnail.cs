using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Models
{
    public class Thumbnail
    {
        public string Name { get; set; }

        public string ParentPath { get; set; }

        public ulong Size { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public DateTime AddedDateTime { get; set; }

        public TimeSpan RunningTime { get; set; }

        public byte[] ThumbnailData { get; set; }
    }
}
