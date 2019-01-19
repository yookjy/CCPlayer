using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.WP81.Strings
{
    public class CCPlayerConstant
    {
        public const string APP_PRO_ID = "1765dc04-edc1-4c07-a850-da8b055a6362";

        public const string MS_PUBCENTER_APP_ID = "60cd0b1c-8ed1-4099-892f-5180848bf827";

        public const string MS_PUBCENTER_SMALL_UNIT_ID = "188258";

        public const string SUBTITLE_TYPE_SMI = ".SMI";
        public const string SUBTITLE_TYPE_SRT = ".SRT";
        public const string SUBTITLE_TYPE_ASS = ".ASS";
        public const string SUBTITLE_TYPE_SSA = ".SSA";

        public static string[] VIDEO_FILE_SUFFIX = { ".ASF", ".WMV", ".MP4", ".M4V", ".MOV", ".AVI", ".CCP", ".MKV", ".FLV", ".3GP", ".3G2", ".DAT", ".RMVB", ".MPG", ".TS", ".MTS", ".WEBM" };
        public static string[] SUBTITLE_FILE_SUFFIX = { SUBTITLE_TYPE_SMI, SUBTITLE_TYPE_SRT, SUBTITLE_TYPE_ASS, SUBTITLE_TYPE_SSA };
    }
}
