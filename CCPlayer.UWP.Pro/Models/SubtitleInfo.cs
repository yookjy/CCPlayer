using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CCPlayer.UWP.Models
{
    public class SubtitleInfo : FileInfo
    {
        public string Owner { get; set; }

        public SubtitleInfo() : base() { }

        public SubtitleInfo(StorageFile storageFile)
            : base(storageFile)
        {
        }
    }
}
