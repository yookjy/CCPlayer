using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Codec
{
    public sealed class SubtitleLanguage
    {
        public SubtitleLanguage()
        {
            Code = string.Empty;
            Name = string.Empty;
            Lang = string.Empty;
        }

        public string Code { get; set; }
        public string Name{ get; set; }
        public string Lang { get; set; }

        public IDictionary<string, string> Properties => new Dictionary<string, string>();
    }
}
