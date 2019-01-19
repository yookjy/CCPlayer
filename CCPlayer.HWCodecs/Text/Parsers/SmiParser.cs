using CCPlayer.HWCodecs.Text.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Text.Parsers
{
    public class SmiParser : SubtitleParser
    {
        protected override CCPlayer.HWCodecs.Text.Models.Subtitle Parse(string content)
        {
            var subtitle = new CCPlayer.HWCodecs.Text.Models.Subtitle();
            Regex expHead = new Regex(@"<\s*head\s*>(.*?)<\s*/\s*head\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex expTitle = new Regex(@"<\s*title\s*>(.*?)<\s*/\s*title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex expStyle = new Regex(@"<\s*style\s*type=(.*?)\s*>(.*?)<\s*/\s*style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex expStyleP = new Regex(@"P\s*?" + Regex.Escape("{") + "(.*?)}.*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex expLang = new Regex(Regex.Escape(".") + "(.*?) {(.*?)}", RegexOptions.IgnoreCase);

            Regex expSync = new Regex(@"<SYNC Start=(.*?)>", RegexOptions.IgnoreCase);
            Regex expP = new Regex(@"<P Class=(.*?)>", RegexOptions.IgnoreCase);
            Regex expCrlf = new Regex(@"\r\n|\r|\n|<\s*/\s*body\s*>|<\s*/\s*sami\s*>|<\s*/\s*p\s*>|<\s*/\s*sync\s*>", RegexOptions.IgnoreCase);
            Regex expQuo = new Regex(@"""|'|\s", RegexOptions.IgnoreCase);

            if (expHead.IsMatch(content))
            {
                Match match = expHead.Match(content);
                string head = match.Groups[1].Value;

                if (expTitle.IsMatch(head))
                {
                    subtitle.Title = expTitle.Match(head).Groups[1].Value;
                }

                if (expStyle.IsMatch(head))
                {
                    string style = expStyle.Match(head).Groups[2].Value;
                    MatchCollection MatchList = null;

                    //스타일에서 P 분리
                    if (expStyleP.IsMatch(style))
                    {
                        MatchList = expStyleP.Matches(style);
                        int styleIndex = 0;
                        foreach (Match FirstMatch in MatchList)
                        {
                            GroupCollection groups = FirstMatch.Groups;
                            string p = groups[1].Value;
//                            System.Diagnostics.Debug.WriteLine("P :" + p);
                            //font-family: Arial; font-weight: normal; color: white; background-color: black; text-align: center; 
                            //margin-left:2pt; margin-right:2pt; margin-bottom:1pt; margin-top:1pt;   text-align:center; font-size:20pt; font-family:Arial, Sans-serif;   font-weight:bold; color:white;
                            subtitle.AddStyle(string.Format("P_{0}", styleIndex++), CCPlayer.HWCodecs.Text.Models.Subtitle.GetAttributes(p));
                        }
                    }
                    //스타일에서 클래스 분리
                    if (expLang.IsMatch(style))
                    {
                        MatchList = expLang.Matches(style);

                        foreach (Match FirstMatch in MatchList)
                        {
                            GroupCollection groups = FirstMatch.Groups;
                            string langClass = expQuo.Replace(groups[1].Value, string.Empty);
                            string langAttr = expQuo.Replace(groups[2].Value, string.Empty);
//                            System.Diagnostics.Debug.WriteLine("Lang Cls : " + langClass);
//                            System.Diagnostics.Debug.WriteLine("Lang attr : " + langAttr);
                            //ENUSCC
                            //name: English; lang: en - US; SAMIType: CC;
                            //KRCC
                            //Name:Korean; lang:ko-KR; SAMIType:CC;
                            subtitle.AddLanguage(langClass, CCPlayer.HWCodecs.Text.Models.Subtitle.GetAttributes(langAttr));
                        }
                    }
                }
            }

            string[] split = expSync.Split(content);
            string script = string.Empty;
            string langClsName = string.Empty;

            for (int i = 1; i < split.Length; i += 2)
            {
                string time = expQuo.Replace(split[0 + i], string.Empty);
                string[] scripts = expP.Split(split[1 + i].Trim());

                if (scripts != null && scripts.Length >= 3)
                {
                    langClsName = expQuo.Replace(scripts[1], string.Empty);
                    script = expCrlf.Replace(scripts[2], string.Empty);

                    StringBuilder ic = new StringBuilder();
                    ic.Append(script);

                    long dTime = 0;
                    if (long.TryParse(time, out dTime))
                    {
                        subtitle.AddSubtitleContent(langClsName, dTime, dTime, ic);
                    }
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("태그가 맞지 않음.");
#endif
                }
            }

            //랭귀지 코드만 있고, 실제 데이터가 없는 자막 목록 삭제
            for (int i = subtitle.Languages.Count - 1; i >= 0; i--)
            {
                var lang = subtitle.Languages[i].LanguageClassName;
                if (!subtitle.SubtitleContents.ContainsKey(lang))
                {
                    subtitle.Languages.RemoveAt(i);
                }
            }
            
            subtitle.SubtitleType = SubtitleTypes.SMI;
            return subtitle;
        }
    }
}
