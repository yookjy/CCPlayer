using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Models
{
    public class PlayList : MenuItem
    {
        public PlayList() : base() { Type = MenuType.Playlist; } 

        public long Seq { get; set; }

        //public StorageItemInfo StorageItemInfo { get; set; }
    }
}
