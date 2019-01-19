using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Text.Models
{
    public class SubtitleLanguage
    {
        public string LanguageClassName { get; set; }

        public string LanguageCode { get; set; }

        private string _LanguageName;
        public string LanguageName 
        {
            get
            {
                return string.IsNullOrEmpty(_LanguageName) ? LanguageClassName : _LanguageName;
            }
            set
            {
                _LanguageName = value;
            }
        }

        public string Type { get; set; }
    }
}
