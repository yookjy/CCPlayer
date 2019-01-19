using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Media
{
    public enum MediaTypes
    {
        None = 0,
        Video = 1,
        Audio = 2,
        Complex = 3,
        Logo = 0x10,
        Subtitle = 0x11,
        Button = 0x12,
        Control = 0x20
    }
}
