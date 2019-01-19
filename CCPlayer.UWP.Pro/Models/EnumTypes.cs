using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Models
{
    public enum PaidLevelType
    {
        Trial,
        Migrated, //사용하지 않으나 기존 로직을 위해 남겨둠
        Full
    }

    public enum SortType
    {
        Name,
        NameDescending,
        CreatedDate,
        CreatedDateDescending
    }

    public enum MenuType
    {
        Explorer,
        NowPlaying,
        Playlist,
        Settings,
        GeneralSetting,
        PrivacySetting,
        SubtitleSetting,
        FontSetting,
        PlaybackSetting,
        AppInfomation,
        DLNA,
        Network,
        Cloud
    }
}
