using CCPlayer.HWCodecs.Text.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Text.Parsers
{
    public class SrtParser : SubtitleParser
    {
        Regex expBS = new Regex(@"{\s*b\s*}", RegexOptions.IgnoreCase);
        Regex expBE = new Regex(@"{\s*/b\s*}", RegexOptions.IgnoreCase);
        Regex expUS = new Regex(@"{\s*u\s*}", RegexOptions.IgnoreCase);
        Regex expUE = new Regex(@"{\s*/u\s*}", RegexOptions.IgnoreCase);
        Regex expIS = new Regex(@"{\s*i\s*}", RegexOptions.IgnoreCase);
        Regex expIE = new Regex(@"{\s*/i\s*}", RegexOptions.IgnoreCase);

        Regex expBS2 = new Regex(@"&lt;\s*b\s*&gt;", RegexOptions.IgnoreCase);
        Regex expBE2 = new Regex(@"&lt;\s*/b\s*&gt;", RegexOptions.IgnoreCase);
        Regex expUS2 = new Regex(@"&lt;\s*u\s*&gt;", RegexOptions.IgnoreCase);
        Regex expUE2 = new Regex(@"&lt;\s*/u\s*&gt;", RegexOptions.IgnoreCase);
        Regex expIS2 = new Regex(@"&lt;\s*i\s*&gt;", RegexOptions.IgnoreCase);
        Regex expIE2 = new Regex(@"&lt;\s*/i\s*&gt;", RegexOptions.IgnoreCase);
        Regex expFS2 = new Regex(@"&lt;\s*font\s*(.*?)&gt;", RegexOptions.IgnoreCase);
        Regex expFE2 = new Regex(@"&lt;\s*/font\s*&gt;", RegexOptions.IgnoreCase);

        Regex expCrlf = new Regex(@"\r\n|\r|\n", RegexOptions.IgnoreCase);

        public string ConvertLine(string line)
        {
            var srtText = line;

            //html용 태그 변환전 특수 기호 치환 ('와 "는 상관 없음)
            srtText = srtText.Replace("&", "&amp;");
            srtText = srtText.Replace("<", "&lt;");
            srtText = srtText.Replace(">", "&gt;");

            //변환된 것들중 태그만 다시 치환
            srtText = expBS2.Replace(srtText, "<b>");
            srtText = expBE2.Replace(srtText, "</b>");
            srtText = expIS2.Replace(srtText, "<i>");
            srtText = expIE2.Replace(srtText, "</i>");
            srtText = expUS2.Replace(srtText, "<u>");
            srtText = expUE2.Replace(srtText, "</u>");
            srtText = expFE2.Replace(srtText, "</font>");

            if (expFS2.IsMatch(srtText))
            {
                var match = expFS2.Match(srtText);
                var value = match.Groups[1];
                srtText = expFS2.Replace(srtText, string.Format("<font {0}>", value));
            }

            //스타일 태그 변환
            srtText = expBS.Replace(srtText, "<b>");
            srtText = expBE.Replace(srtText, "</b>");
            srtText = expIS.Replace(srtText, "<i>");
            srtText = expIE.Replace(srtText, "</i>");
            srtText = expUS.Replace(srtText, "<u>");
            srtText = expUE.Replace(srtText, "</u>");
            srtText = expCrlf.Replace(srtText, "<br/>");

            return srtText;
        }

        protected override Subtitle Parse(string content)
        {
            Subtitle subtitle = new Subtitle();
            
            //Regex expSrt = new Regex(@"(?<number>\d+)\r\n(?<start>\S+)\s-->\s(?<end>\S+)\r\n(?<text>(.|[\r\n])+?)\r\n\r\n");
            Regex expSrt = new Regex(@"(?<number>\d+)(\n|\r\n)(?<start>\S+)\s-->\s(?<end>\S+)(\n|\r\n)(?<text>(.|[\n]|[\r\n])+?)(\n|\r\n)(\n|\r\n)");

            if (expSrt.IsMatch(content))
            {
                long dsTime = 0, deTime = 0;
                int number = expSrt.GroupNumberFromName("number");
                int start = expSrt.GroupNumberFromName("start");
                int end = expSrt.GroupNumberFromName("end");
                int text = expSrt.GroupNumberFromName("text");
                MatchCollection mc = expSrt.Matches(content);

                foreach (Match m in mc)
                {
                    GroupCollection gc = m.Groups;
                    string strNumber = gc[number].Value;
                    string strStart = gc[start].Value;
                    string strEnd = gc[end].Value;

                    TimeSpan tss;
                    TimeSpan tse;

                    if (TimeSpan.TryParse(strStart.Replace(',', '.'), out tss)
                        && TimeSpan.TryParse(strEnd.Replace(',', '.'), out tse))
                    {
                        //시간 형식 변환
                        dsTime = (long)tss.TotalMilliseconds;
                        deTime = (long)tse.TotalMilliseconds;

                        if (subtitle.SubtitleContents != null && subtitle.SubtitleContents["SubRip"].Values.Any(x => x.EndTime > dsTime))
                        {
                            //이미 지난 자막 무시
                            continue;
                        }
                        //자막 데이터의 태그 변환
                        StringBuilder srtText = new StringBuilder(ConvertLine(gc[text].Value));

                        subtitle.AddSubtitleContent("SubRip", dsTime, deTime, srtText);
                        subtitle.AddSubtitleContent("SubRip", deTime, deTime, new StringBuilder(null));
                    }
                    else
                    {
//                        System.Diagnostics.Debug.WriteLine("srt start time : " + strStart);
//                        System.Diagnostics.Debug.WriteLine("srt end time : " + strEnd);
                    }
                }
            }

            subtitle.SubtitleType = SubtitleTypes.SRT;
            return subtitle;
        }
    }
}
