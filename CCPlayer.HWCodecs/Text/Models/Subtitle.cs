using CCPlayer.HWCodecs.Text.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Text.Models
{
    public enum SubtitleTypes
    {
        NA,
        SMI,
        SRT,
        ASS,
        SSA
    }

    public enum SubtitleEncodingResult
    {
        Success,
        UnkownEncoding,
        Fail
    }

    public enum SubtitleFileKind
    {
        External,
        Internal
    }

    public class SubtitleContent
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public StringBuilder Text { get; set; }
    }

    public class Subtitle
    {
        public SubtitleFileKind SubtitleFileKind { get; set; }

        public SubtitleTypes SubtitleType { get; set; }

        public SubtitleParser Parser { get; set; }

        public Stream Source { get; set; }

        public SubtitleEncodingResult EncodingResult { get; set; }

        public int CurrentCodePage { get; set; }

        public string Title { get; set; }

        public Dictionary<string, List<KeyValuePair<string, string>>> Styles { get; set; }

        private List<SubtitleLanguage> _Languages;

        public List<SubtitleLanguage> Languages 
        {
            get
            {
                if (_Languages == null)
                {
                    _Languages = new List<SubtitleLanguage>();
                    if (SubtitleContents != null && SubtitleContents.Count > 0)
                    {
                        _Languages.Add(new SubtitleLanguage()
                        {
                            LanguageClassName = SubtitleContents.Keys.First()
                        });
                    }
                }
                return _Languages;
            }
            set
            {
                _Languages = value;
            }
        }

        public Dictionary<string, SortedDictionary<long, SubtitleContent>> SubtitleContents { get; set; }

        public void AddLanguage(string langClsName, List<KeyValuePair<string, string>> langAttributes)
        {
            if (Languages == null)
            {
                Languages = new List<SubtitleLanguage>();
            }
            SubtitleLanguage subtitleLanguage = new SubtitleLanguage()
            {
                LanguageClassName = langClsName
            };

            foreach (KeyValuePair<string, string> attribute in langAttributes)
            {
                //Name:KRCC; lang:kr-KR; SAMIType:CC;
                switch (attribute.Key.ToLower())
                {
                    case "name" :
                        subtitleLanguage.LanguageName = attribute.Value;
                        break;
                    case "lang" :
                        subtitleLanguage.LanguageCode = attribute.Value;
                        break;
                    case "samitype" : 
                        subtitleLanguage.Type = attribute.Value;
                        break;
                }
            }

            Languages.Add(subtitleLanguage); ;
        }

        internal void AddStyle(string styleName, List<KeyValuePair<string, string>> style)
        {
            if (Styles == null)
            {
                Styles = new Dictionary<string, List<KeyValuePair<string, string>>>();
            }

            Styles[styleName] = style;
        }

        internal void AddSubtitleContent(string langClsName, long startMilliSec, long endMilliSec, StringBuilder text)
        {
            if (SubtitleContents == null)
            {
                SubtitleContents = new Dictionary<string, SortedDictionary<long, SubtitleContent>>();
            }

            SortedDictionary<long, SubtitleContent> contents = null;

            if (!SubtitleContents.TryGetValue(langClsName, out contents))
            {
                contents = new SortedDictionary<long, SubtitleContent>();
                SubtitleContents[langClsName] = contents;
            }

            if (contents.Any(x => x.Key == startMilliSec))
            {
                var content = contents[startMilliSec];
                //종료타임과 다음 시작 타임이 겹치는 경우 플리커 제거하기 위해 자막 합침
                if (!string.IsNullOrEmpty(content.Text.ToString().Trim()))
                {
                    content.Text.Append("<br/>");
                }
                content.Text.Append(text);
            }
            else
            {
                contents[startMilliSec] = new SubtitleContent()
                {
                    StartTime = startMilliSec,
                    EndTime = endMilliSec,
                    Text = text
                };
            }
            //if (text.ToString() == "알프레드?") System.Diagnostics.Debugger.Break();
            //표시자막과 숨김 자막이 너무 촘촘히 있어서 이벤트가 발생하지 않는것을 방지한다.
            //if (startMilliSec != endMilliSec)
            if (!string.IsNullOrEmpty(text.Replace("&nbsp;", "").ToString().Trim()))
            {
                //System.Diagnostics.Debug.WriteLine("다름");
                if(contents.Any(x =>(x.Value.StartTime - startMilliSec > -500 && x.Value.StartTime - startMilliSec < 500)
                    && (x.Value == null ||  string.IsNullOrEmpty(x.Value.Text.Replace("&nbsp;", "").ToString().Trim()))))
                {
                    var markers = contents.Where(x => (x.Value.StartTime - startMilliSec > -500 && x.Value.StartTime - startMilliSec < 500)
                        && (x.Value == null || string.IsNullOrEmpty(x.Value.Text.Replace("&nbsp;", "").ToString().Trim()))).ToArray();
                    foreach (var marker in markers)
                    {
                        contents.Remove(marker.Key);
                    }
                }
            }
        }

        internal static List<KeyValuePair<string, string>> GetAttributes(string attributes)
        {
            List<KeyValuePair<string, string>> attrList = new List<KeyValuePair<string, string>>();

            string[] attrs = attributes.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string attr in attrs)
            {
                string[] pv = attr.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (pv.Length == 2)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("{0} => {1}", pv[0].Trim(), pv[1].Trim());
#endif
                    attrList.Add(new KeyValuePair<string, string>(pv[0].Trim().ToLower(), pv[1].Trim().ToLower()));
                }
            }
            return attrList;
        }

        //internal bool TryGetValue(string langClsName, long time, out SubtitleContent? ic)
        //{
        //    SortedDictionary<long, SubtitleContent> contents = null;
        //    if (SubtitleContents.TryGetValue(langClsName, out contents))
        //    {
        //        var keys = new List<long>(contents.Keys);
        //        int index = keys.BinarySearch(time);

        //        if (index > -1)
        //        {
        //            ic = contents[keys[index]];
        //            return true;
        //        }
        //        else if (keys.Count != ~index)
        //        {
        //            if (keys[~index] - time < 200)
        //            {
        //                ic = contents[keys[~index]];
        //                return true;    
        //            }
        //        }
        //    }
        //    ic = null;
        //    return false;
        //}
    }
}
