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
    public class AssParser : SubtitleParser
    {
        Stack<string> TagStack = new Stack<string>();
        StringBuilder htmlText = new StringBuilder();

        Regex sTagExp = new Regex(@"<\s*(?<tagName>(.+?))\s*[^/]\s*>");
        Regex eTagExp = new Regex(@"<\s*/(?<tagName>(.+?))>");
        
        Regex assTagExp = new Regex(@"(?<tags>({\s*\\.+?\s*}))");
        Regex fn2TagExp = new Regex(@"{(move|pos|org|fade|fad|clip|).*}");
        //Regex sepExp = new Regex(@"\\(.+?)");
        Regex colorExp = new Regex(@"(?<num>(\d*?))c&H(?<color>(.+?))&");

        Regex crExp = new Regex(@"\\n", RegexOptions.IgnoreCase);
        Regex lfExp = new Regex(@"\\r", RegexOptions.IgnoreCase);

        Regex keyValueExp = new Regex(@"\s*(?<key>(.+?))\s*:\s*(?<value>(.+?))\r\n");
        Regex dialogueExp = new Regex(@"\s*Dialogue\s*:\s*(?<value>(.+?))\r\n", RegexOptions.IgnoreCase);

        Regex scriptInfoExp = new Regex(@"(\s*\[\s*Script\s+Info\s*\]\s*)\r\n(?<text>(.|[\r\n])+?)\r\n\r\n", RegexOptions.IgnoreCase);
        Regex v4StyleExp = new Regex(@"(\s*\[\s*V\d{1}[\+]*\s+Styles\s*\]\s*)\r\n(?<text>(.|[\r\n])+?)\r\n\r\n", RegexOptions.IgnoreCase);
        //Regex eventsExp = new Regex(@"(\s*\[\s*Events\s*\]\s*)\r\n(?<text>(.|[\r\n])+?)\r\n\r\n", RegexOptions.IgnoreCase);
        Regex eventsExp = new Regex(@"(\s*\[\s*Events\s*\]\s*)\r\n(?<text>(.|[\r\n])+?)\r\n", RegexOptions.IgnoreCase); //마지막에 \r\n이 한번밖에 없는 경우가 있고 Event는 마지막에 오므로 하나만 체크하도록 변경. 2014.10.12일 피드백 => 윤석민 (네이버 메일)
        
        internal List<KeyValuePair<string, string>> ScriptInfoList = new List<KeyValuePair<string, string>>();
        internal Dictionary<string, List<KeyValuePair<string, string>>> V4StyleDict = new Dictionary<string, List<KeyValuePair<string, string>>>();
        public List<string> EventsList = new List<string>();

        private void LoadScriptInfoList(string content)
        {
            //[Script Info] 섹션
            if (scriptInfoExp.IsMatch(content))
            {
                ScriptInfoList.Clear();

                int ti = scriptInfoExp.GroupNumberFromName("text");
                Match m = scriptInfoExp.Match(content);

                GroupCollection gc = m.Groups;
                string text = gc[ti].Value + "\r\n";

                if (keyValueExp.IsMatch(text))
                {
                    MatchCollection mc = keyValueExp.Matches(text);
                    int k = keyValueExp.GroupNumberFromName("key");
                    int v = keyValueExp.GroupNumberFromName("value");

                    foreach (Match match in mc)
                    {
                        gc = match.Groups;
                        string key = gc[k].Value.Trim().ToLower();
                        string val = gc[v].Value.Trim();

                        if (key.IndexOf(";") != 0)
                        {
                            ScriptInfoList.Add(new KeyValuePair<string, string>(key, val));
                        }
                    }
                }
            }
        }

        private void LoadV4StyleDict(string content)
        {
            //[V4+ Style] 섹션
            if (v4StyleExp.IsMatch(content))
            {
                V4StyleDict.Clear();
   
                int ti = v4StyleExp.GroupNumberFromName("text");
                Match m = v4StyleExp.Match(content);

                GroupCollection gc = m.Groups;
                string text = gc[ti].Value + "\r\n";

                if (keyValueExp.IsMatch(text))
                {
                    MatchCollection mc = keyValueExp.Matches(text);
                    int k = keyValueExp.GroupNumberFromName("key");
                    int v = keyValueExp.GroupNumberFromName("value");

                    string[] formats = null;

                    foreach (Match match in mc)
                    {
                        gc = match.Groups;
                        string key = gc[k].Value.Trim().ToLower();
                        string val = gc[v].Value.Trim();

                        if (key == "format")
                        {
                            formats = val.Split(new char[] { ',' });
                        }
                        else if (key == "style")
                        {
                            var styleValues = val.Split(new char[] { ',' });
                            Dictionary<string, string> mapping = new Dictionary<string, string>();

                            for (int i = 0; i < formats.Length; i++)
                            {
                                mapping[formats[i].Trim().ToLower()] = styleValues[i].Trim();
                            }
                            //키 생성
                            V4StyleDict[mapping["name"]] = new List<KeyValuePair<string, string>>();
                            foreach (var mk in mapping.Keys)
                            {
                                V4StyleDict[mapping["name"]].Add(new KeyValuePair<string, string>(mk, mapping[mk]));
                            }
                        }
                    }
                }
            }
        }

        private void LoadEventsList(string content)
        {
            //[Events] 섹션
            if (eventsExp.IsMatch(content))
            {
                EventsList.Clear();

                int ti = eventsExp.GroupNumberFromName("text");
                Match m = eventsExp.Match(content);

                GroupCollection gc = m.Groups;
                string text = gc[ti].Value + "\r\n";

                if (keyValueExp.IsMatch(text))
                {
                    MatchCollection mc = keyValueExp.Matches(text);
                    int k = keyValueExp.GroupNumberFromName("key");
                    int v = keyValueExp.GroupNumberFromName("value");

                    foreach (Match match in mc)
                    {
                        gc = match.Groups;
                        string key = gc[k].Value.Trim().ToLower();
                        string val = gc[v].Value.Trim();

                        if (key == "format")
                        {
                            EventsList.AddRange(val.Split(new char[] { ',' }));
                        }
                    }
                }
            }
        }

        public void LoadHeader(string content)
        {
            //[Script Info] 섹션 파싱
            LoadScriptInfoList(content);
            //[V4+ Styles] 섹션 파싱
            LoadV4StyleDict(content);
            //[Events] 섹션 파싱
            LoadEventsList(content);
        }

        public string GetTitle()
        {
            var title = ScriptInfoList.FirstOrDefault(x => x.Key == "title").Value;

            if (string.IsNullOrEmpty(title))
            {
                var type = ScriptInfoList.FirstOrDefault(x => x.Key == "scripttype").Value;
                if (string.IsNullOrEmpty(type))
                {
                    return "ASS Unknown ver.";
                }
                else
                {
                    return string.Format("ASS {0}", type);
                }
            }
            return title;
        }

        protected override Subtitle Parse(string content)
        {
            Subtitle subtitle = new Subtitle();
            subtitle.SubtitleType = SubtitleTypes.ASS;
            
            //헤더 정보 로드
            LoadHeader(content);

            //Dialogue 파싱
            Dictionary<long, List<KeyValuePair<string, string>>> dialogueDict = new Dictionary<long, List<KeyValuePair<string, string>>>();
            String title = GetTitle(); ;
            if (dialogueExp.IsMatch(content))
            {
                MatchCollection mc = dialogueExp.Matches(content);
                int v = dialogueExp.GroupNumberFromName("value");        

                foreach (Match match in mc)
                {
                    var gc = match.Groups;
                    string val = gc[v].Value;

                    string[] values = val.Split(new char[] { ',' }, EventsList.Count);
                    List<string> valueList = new List<string>(EventsList.Count);

                    Dictionary<string, string> tmp = new Dictionary<string, string>();

                    for (int i = 0; i < EventsList.Count; i++)
                    {
                        tmp[EventsList[i].ToLower().Trim()] = values[i].Trim();
                    }

                    long dsTime = (long)(TimeSpan.Parse(tmp["start"].Replace(',', '.')).TotalMilliseconds);
                    long deTime = (long)(TimeSpan.Parse(tmp["end"].Replace(',', '.')).TotalMilliseconds);
                    
                    //2015.08.07 이펙터 무시
                    //if (!string.IsNullOrEmpty(tmp["effect"]))
                    //{
                    //    Regex effectTagExp = new Regex(@"\s*{(?<effector>(.+?))}\s*(?<text>(.+?))", RegexOptions.IgnoreCase);
                    //    if (effectTagExp.IsMatch(tmp["text"]))
                    //    {
                    //        Match efmc = effectTagExp.Match(tmp["text"]);
                    //        int efv = effectTagExp.GroupNumberFromName("text");

                    //        var efgc = efmc.Groups;
                    //        string efval = efgc[efv].Value;
                    //        tmp["text"] = efval;
                    //    }
                    //}

                    //줄바꿈 치환
                    var text = crExp.Replace(tmp["text"], "<br/>");
                    text = lfExp.Replace(text, string.Empty);

                    //자체 스타일 오버라이드 (가장 안쪽의 스타일이 최우선 스타일 이므로...)
                    text = ConvertLine(text);

                    //스타일 적용 (V4+ Style)
                    string styleName = string.Empty;
                    if (tmp.TryGetValue("style", out styleName))
                    {
                        //공통 스타일 적용
                        SetV4Style(ref text, styleName);
                    }
                    
                    //표시
                    subtitle.AddSubtitleContent(title, dsTime, deTime, new StringBuilder(text));
                    //숨김
                    subtitle.AddSubtitleContent(title, deTime, deTime, new StringBuilder());
                }
            }

            //foreach(var ss in subtitle.SubtitleContents[title])
            //{
            //    System.Diagnostics.Debug.WriteLine(TimeSpan.FromMilliseconds(ss.Value.StartTime) + " : " + ss.Value.Text);
            //}

            return subtitle;
        }

        private void PushTag(string tag)
        {
            if (eTagExp.IsMatch(tag))
            {
                Match match = eTagExp.Match(tag);
                string tagName = match.Groups["tagName"].Value;
                string eTag = null;

                do
                {
                    if (TagStack.Count == 0) return;

                    eTag = TagStack.Pop();
                    htmlText.AppendFormat("</{0}>", eTag);

                } while (eTag != tagName);
            }
            else if (sTagExp.IsMatch(tag))
            {
                Match match = sTagExp.Match(tag);

                string tagName = match.Groups["tagName"].Value.Trim();
                string[] tagAttrs = tagName.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                TagStack.Push(tagAttrs[0]);

                htmlText.Append(tag);
            }
            //else if (crExp.IsMatch(tag))
            //{
            //    htmlText.Append("<br>");
            //}
            else
            {
                htmlText.Append(tag);
            }

        }
        
        private string[] SplitAssString(string assText)
        {
            var text = assText + @"{\EOF}";
            if (assTagExp.IsMatch(text))
            {
                MatchCollection mc = assTagExp.Matches(text);
                List<string> splitString = new List<string>();

                foreach (Match match in mc)
                {
                    var tagStrings = match.Groups["tags"].Value;
                    var sIndex = text.IndexOf(tagStrings);

                    if (sIndex == 0)
                    {
                        text = text.Remove(text.IndexOf(tagStrings), tagStrings.Length);
                        splitString.Add(tagStrings);
                    }
                    else if (sIndex > 0)
                    {
                        var tmp = text.Substring(0, sIndex);
                        text = text.Remove(0, sIndex + tagStrings.Length);

                        tmp = fn2TagExp.Replace(tmp, string.Empty);
                        splitString.Add(tmp);
                        splitString.Add(tagStrings);
                    }
                }
                splitString.Remove(splitString.Last());
                return splitString.ToArray();
            }
            return new string[] { text };
        }

        private string[] ToHtmlTag(string tag)
        {
            if (assTagExp.IsMatch(tag))
            {
                var tags = tag.Substring(1, tag.Length - 2).Split(new string[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);
                List<string> tList = new List<string>();

                foreach (var t in tags)
                {
                    if (t == "b1")
                    {
                        tList.Add("<b>");
                    }
                    else if (t == "b0")
                    {
                        tList.Add("</b>");
                    }
                    else if (t == "i1")
                    {
                        tList.Add("<i>");
                    }
                    else if (t == "i0")
                    {
                        tList.Add("</i>");
                    }
                    else if (t == "u1")
                    {
                        tList.Add("<u>");
                    }
                    else if (t == "u0")
                    {
                        tList.Add("</u>");
                    }
                    else if (t.IndexOf("fs") != -1)
                    {
                        //글자 크기 - 무시함 (크기 조정기능 때문)
                        //string size = t.Replace("fs", string.Empty);
                        //double s = 0;
                        //if (double.TryParse(size, out s))
                        //{
                        //    //xaml에서는 px이 기본이며 v4+ style에서는 point이므로 변환 필요
                        //    s /= (12.0 / 3.0);
                        //    var last = tList.LastOrDefault();
                        //    if (last != null && last.IndexOf("<font") != -1)
                        //    {
                        //        last = last.Replace(">", " size=\"" + s.ToString("#.#") + "\">");
                        //        tList.Remove(tList.Last());
                        //        tList.Add(last);
                        //    }
                        //    else
                        //    {
                        //        tList.Add("<font size=\"" + s.ToString("#.#") + "\">");
                        //    }
                        //}
                    }
                    else if (t.IndexOf("c&H") != -1)
                    {
                        if (colorExp.IsMatch(t))
                        {
                            var colorNum = colorExp.Match(t).Groups["num"].Value;

                            if (colorNum == "" || colorNum == "0" || colorNum == "1")
                            {
                                var colorCode = colorExp.Match(t).Groups["color"].Value;
                                colorCode = colorCode.PadRight(6, '0');

                                var bb = colorCode.Substring(0, 2);
                                var gg = colorCode.Substring(2, 2);
                                var rr = colorCode.Substring(4, 2);

                                var last = tList.LastOrDefault();
                                if (last != null && last.IndexOf("<font") != -1)
                                {
                                    last = last.Replace(">", string.Format(" color=\"#FF{0}{1}{2}\">", rr, gg, bb));
                                    tList.Remove(tList.Last());
                                    tList.Add(last);
                                }
                                else
                                {
                                    tList.Add(string.Format("<font color=\"#FF{0}{1}{2}\">", rr, gg, bb));
                                }
                            }
                        }
                    }
                }
                return tList.ToArray();
            }
            return new string[] { tag };
        }

        public string ConvertLine(string line)
        {
            //초기화
            TagStack.Clear();
            htmlText.Clear();
            
            //1. 태그별로 분리
            var vals = SplitAssString(line.Trim());
            //2. 태그를 html태그로 변환
            foreach (var val in vals)
            {
                var tags = ToHtmlTag(val);
                foreach (var tag in tags)
                {
                    //3. 태그 open/close 정리
                    PushTag(tag);
                }
            }

            while (TagStack.Count > 0)
            {
                htmlText.AppendFormat("</{0}>", TagStack.Pop());
            }

            return htmlText.ToString();
        }

        public void SetV4Style(ref string text, string styleName)
        {
            //스타일 적용 (V4+ Style)
            List<KeyValuePair<string, string>> styleAttrs = null;
            if (V4StyleDict.TryGetValue(styleName, out styleAttrs))
            {
                string fontTag = "<font";

                //글자 크기 - 무시함 (크기 조정기능 때문)
                //double fsize = -1;
                //var fs = styleAttrs.FirstOrDefault(x => x.Key == "fontsize").Value;
                //if (fs != null)
                //{
                //    if (double.TryParse(fs, out fsize))
                //    {
                //        fsize = fsize / (12.0 / 3.0);
                //        fontTag += string.Format(" size=\"{0}\"", fsize);
                //    }
                //}
                //글꼴
                var face = styleAttrs.FirstOrDefault(x => x.Key == "fontname").Value;
                if (face != null)
                {
                    fontTag += string.Format(" face=\"{0}\"", face);
                }

                //글자 색상
                var pcolor = styleAttrs.FirstOrDefault(x => x.Key == "primarycolour").Value;
                int nColor = -1;
                if (pcolor != null)
                {
                    pcolor = pcolor.Trim();
                    if (pcolor.IndexOf("&H") == 0)
                    {
                        pcolor = pcolor.Replace("&H", string.Empty);
                        if (pcolor.Length == 8)
                        {
                            var aa = pcolor.Substring(0, 2);
                            var bb = pcolor.Substring(2, 2);
                            var gg = pcolor.Substring(4, 2);
                            var rr = pcolor.Substring(6, 2);
                            //BGR포맷임
                            fontTag += string.Format(" color=\"#FF{0}{1}{2}\"", rr, gg, bb);
                        }
                    }
                    else if (int.TryParse(pcolor, out nColor))
                    {
                        var rr = (nColor) & 0xFF;
                        var gg = (nColor >> 8) & 0xFF;
                        var bb = (nColor >> 16) & 0xFF;
                        var aa = (nColor >> 24) & 0xFF;

                        fontTag += string.Format(" color=\"#FF{0}{1}{2}\"", rr, gg, bb);
                    }
                    
                }

                if (fontTag != "<font")
                {
                    text = string.Format("{0}>{1}</font>", fontTag, text);
                }

                //굵게
                var bold = styleAttrs.FirstOrDefault(x => x.Key == "bold").Value;
                if (bold != null && bold != "0")
                {
                    text = string.Format("<b>{0}</b>", text);
                }
                //기울이기
                var italic = styleAttrs.FirstOrDefault(x => x.Key == "italic").Value;
                if (italic != null && italic != "0")
                {
                    text = string.Format("<i>{0}</i>", text);
                }
                //밑줄
                var underline = styleAttrs.FirstOrDefault(x => x.Key == "underline").Value;
                if (underline != null && underline != "0")
                {
                    text = string.Format("<u>{0}</u>", text);
                }

                //마진 및 Effect 등은 Skip
            }
        }
    }
}

/*
[Script Info]
Title: HorribleSubs
ScriptType: v4.00+
WrapStyle: 0
PlayResX: 848
PlayResY: 480
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Baar Sophia,32,&H00FFFFFF,&H00FFFFFF,&H00020203,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,40,40,35,1
Style: PV,Baar Sophia,32,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,79,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text

Dialogue: 0,0:01:30.72,0:01:35.64,Default,,0000,0000,0000,,{\i1}Creeping Shadow{\i0}\N\N\N
Dialogue: 0,0:01:38.35,0:01:39.43,Default,,0000,0000,0000,,Jiraiya Sensei…
Dialogue: 0,0:01:39.43,0:01:41.52,Default,,0000,0000,0000,,What is it, Kakashi?
Dialogue: 0,0:01:42.35,0:01:43.94,Default,,0000,0000,0000,,There's something I'd like to tell you.
Dialogue: 0,0:01:44.98,0:01:48.53,Default,,0000,0000,0000,,Is it urgent? I'm kind of busy now.
Dialogue: 0,0:01:50.74,0:01:54.87,Default,,0000,0000,0000,,There you go again,\Nsounding so dejected, Kakashi.
Dialogue: 0,0:01:55.45,0:01:56.91,Default,,0000,0000,0000,,No matter how difficult something is,
Dialogue: 0,0:01:56.95,0:01:59.29,Default,,0000,0000,0000,,when you make things seem\Nlike such a drag,
Dialogue: 0,0:01:59.33,0:02:01.50,Default,,0000,0000,0000,,you make the people\Naround you uncomfortable.
Dialogue: 0,0:02:02.58,0:02:04.38,Default,,0000,0000,0000,,I'm sorry to have bothered you.
Dialogue: 0,0:02:04.88,0:02:08.63,Default,,0000,0000,0000,,Wait, wait. You're here now. Speak.
Dialogue: 0,0:02:09.34,0:02:10.26,Default,,0000,0000,0000,,But…
Dialogue: 0,0:02:10.72,0:02:13.14,Default,,0000,0000,0000,,I don't want to interrupt\Nwhen you have important business…
Dialogue: 0,0:02:13.47,0:02:18.43,Default,,0000,0000,0000,,SPECIAL SWIMSUIT EDITION\N\NOn second thought, there is nothing\Nmore important than you.
Dialogue: 0,0:02:18.47,0:02:19.77,Default,,0000,0000,0000,,I know you.
Dialogue: 0,0:02:20.39,0:02:22.52,Default,,0000,0000,0000,,You haven't spoken to\Nanyone today, have you?
Dialogue: 0,0:02:23.60,0:02:25.44,Default,,0000,0000,0000,,You know…
Dialogue: 0,0:02:25.48,0:02:29.19,Default,,0000,0000,0000,,people should talk to others every day,\Neven if it's small talk.
Dialogue: 0,0:02:29.23,0:02:31.28,Default,,0000,0000,0000,,Otherwise, their hearts will grow\Ndark and serious.
Dialogue: 0,0:02:31.70,0:02:36.03,Default,,0000,0000,0000,,Talking to others builds bonds,
Dialogue: 0,0:02:36.07,0:02:39.12,Default,,0000,0000,0000,,and makes you feel grateful\Nthat you're alive.
Dialogue: 0,0:02:39.49,0:02:41.87,Default,,0000,0000,0000,,That's the kind of creatures\Nwe humans are.
Dialogue: 0,0:02:44.08,0:02:45.92,Default,,0000,0000,0000,,So, what happened?
Dialogue: 0,0:02:47.09,0:02:47.92,Default,,0000,0000,0000,,Well…
Dialogue: 0,0:02:48.63,0:02:51.92,Default,,0000,0000,0000,,It's regarding the three children
Dialogue: 0,0:02:51.97,0:02:54.34,Default,,0000,0000,0000,,from the Hidden Rain\Nyou once taught, Jiraiya Sensei.
Dialogue: 0,0:02:54.80,0:02:59.85,Default,,0000,0000,0000,,Oh, you mean Yahiko, Nagato\Nand Konan?
Dialogue: 0,0:03:00.39,0:03:01.27,Default,,0000,0000,0000,,Yes.
Dialogue: 0,0:03:02.18,0:03:05.35,Default,,0000,0000,0000,,Recently, they have been trying to bring\Nabout change in Hidden Rain Village.
Dialogue: 0,0:03:05.40,0:03:07.23,Default,,0000,0000,0000,,Or so I heard.
Dialogue: 0,0:03:08.48,0:03:11.94,Default,,0000,0000,0000,,Without using military might,\Nbut through dialogue.
Dialogue: 0,0:03:12.44,0:03:13.49,Default,,0000,0000,0000,,Oh?
Dialogue: 0,0:03:19.37,0:03:21.87,Default,,0000,0000,0000,,NAGATO                    YAHIKO\N\N
Dialogue: 0,0:03:32.51,0:03:34.67,Default,,0000,0000,0000,,NAGATO    YAHIKO    KONAN\NThose three, eh…
Dialogue: 0,0:03:35.38,0:03:39.14,Default,,0000,0000,0000,,I see. So they've started walking\Non their own feet.
Dialogue: 0,0:03:39.68,0:03:41.43,Default,,0000,0000,0000,,How would you describe them?
Dialogue: 0,0:03:45.94,0:03:49.77,Default,,0000,0000,0000,,{\i1}He's curious about strangers…{\i0}\N{\i1}Now that's a good sign!{\i0}
Dialogue: 0,0:03:50.77,0:03:57.16,Default,,0000,0000,0000,,Yahiko is passionate and\Ncompletely one-track minded.
Dialogue: 0,0:03:58.37,0:04:01.08,Default,,0000,0000,0000,,{\i1}I want to become stronger!{\i0}
Dialogue: 0,0:04:01.37,0:04:02.74,Default,,0000,0000,0000,,{\i1}And then…{\i0}
Dialogue: 0,0:04:03.41,0:04:05.75,Default,,0000,0000,0000,,{\i1}And then,{\i0}\N{\i1}I'm going to change the world!{\i0}
Dialogue: 0,0:04:07.25,0:04:11.55,Default,,0000,0000,0000,,Konan is a girl who never\Nshows her emotions.
Dialogue: 0,0:04:12.17,0:04:15.51,Default,,0000,0000,0000,,But she was pretty and very talented.
Dialogue: 0,0:04:15.76,0:04:19.05,Default,,0000,0000,0000,,She must've grown into a beauty.
Dialogue: 0,0:04:20.30,0:04:22.31,Default,,0000,0000,0000,,As for Nagato…
Dialogue: 0,0:04:31.61,0:04:32.77,Default,,0000,0000,0000,,What happened?
Dialogue: 0,0:04:33.28,0:04:34.32,Default,,0000,0000,0000,,He…
Dialogue: 0,0:04:34.78,0:04:39.16,Default,,0000,0000,0000,,He's a remnant shinobi who told us\Nto hand over our food and valuables…
Dialogue: 0,0:04:39.49,0:04:41.91,Default,,0000,0000,0000,,But then Nagato…
Dialogue: 0,0:04:50.63,0:04:53.04,Default,,0000,0000,0000,,What is it, Nagato?
Dialogue: 0,0:04:56.34,0:04:57.34,Default,,0000,0000,0000,,Jiraiya Sensei…
Dialogue: 0,0:05:00.51,0:05:01.43,Default,,0000,0000,0000,,I'm…
Dialogue: 0,0:05:02.30,0:05:04.14,Default,,0000,0000,0000,,I'm scared of myself.
Dialogue: 0,0:05:04.39,0:05:07.81,Default,,0000,0000,0000,,What I did… Was it wrong?
Dialogue: 0,0:05:08.69,0:05:13.06,Default,,0000,0000,0000,,I really don’t know…\Nif it was right or wrong.
Dialogue: 0,0:05:13.36,0:05:17.32,Default,,0000,0000,0000,,But thanks to you,\NYahiko didn’t die.
Dialogue: 0,0:05:17.36,0:05:18.86,Default,,0000,0000,0000,,You protected your friend.
Dialogue: 0,0:05:19.36,0:05:22.20,Default,,0000,0000,0000,,That had to be the right thing to do.
Dialogue: 0,0:05:23.16,0:05:26.16,Default,,0000,0000,0000,,No one can blame you for that.
Dialogue: 0,0:05:27.29,0:05:29.87,Default,,0000,0000,0000,,When you are hurt,\Nyou learn to hate.
Dialogue: 0,0:05:29.92,0:05:32.92,Default,,0000,0000,0000,,On the other hand, when you hurt someone,\Nyou are resented.
Dialogue: 0,0:05:32.96,0:05:34.79,Default,,0000,0000,0000,,But you start to feel guilty as well.
Dialogue: 0,0:05:35.46,0:05:40.76,Default,,0000,0000,0000,,Understanding such pain\Nenables you to be kind to others…
Dialogue: 0,0:05:41.30,0:05:44.51,Default,,0000,0000,0000,,By understanding pain, humans grow.
Dialogue: 0,0:05:45.35,0:05:50.31,Default,,0000,0000,0000,,By knowing pain, thinking about it\Nand figuring out what to do.
Dialogue: 0,0:05:50.77,0:05:55.32,Default,,0000,0000,0000,,That is what growing up is all about.
Dialogue: 0,0:05:56.86,0:05:59.32,Default,,0000,0000,0000,,I just want to protect those two.
Dialogue: 0,0:06:00.45,0:06:03.37,Default,,0000,0000,0000,,No matter what kind of pain\NI have to endure.
Dialogue: 0,0:06:06.16,0:06:07.04,Default,,0000,0000,0000,,I see.
Dialogue: 0,0:06:09.00,0:06:14.50,Default,,0000,0000,0000,,What are you always\Nthinking about, Sensei?
Dialogue: 0,0:06:15.25,0:06:17.25,Default,,0000,0000,0000,,There is so much war in this world.
Dialogue: 0,0:06:18.00,0:06:20.01,Default,,0000,0000,0000,,Only hatred exists.
Dialogue: 0,0:06:20.92,0:06:23.97,Default,,0000,0000,0000,,I want to do something about it.
Dialogue: 0,0:06:26.05,0:06:30.52,Default,,0000,0000,0000,,And to know what the answer\Nto true peace is…
Dialogue: 0,0:06:36.31,0:06:37.44,Default,,0000,0000,0000,,Jiraiya Sensei…
Dialogue: 0,0:06:37.57,0:06:39.44,Default,,0000,0000,0000,,Hmm? Oh…
Dialogue: 0,0:06:39.94,0:06:40.82,Default,,0000,0000,0000,,What's wrong?
Dialogue: 0,0:06:43.61,0:06:48.49,Default,,0000,0000,0000,,Nagato was a youth who could have been\Nthe hero in my novels.
Dialogue: 0,0:06:49.20,0:06:51.20,Default,,0000,0000,0000,,The hero in your novel?
Dialogue: 0,0:06:51.75,0:06:56.29,Default,,0000,0000,0000,,As long as I'm alive,\Nthere's a chance I'll see him…
Dialogue: 0,0:06:57.96,0:06:58.92,Default,,0000,0000,0000,,Kakashi…
Dialogue: 0,0:06:59.46,0:07:04.05,Default,,0000,0000,0000,,Hurry and become a man\Nwho can be a hero in my novels.
Dialogue: 0,0:07:11.02,0:07:13.85,Default,,0000,0000,0000,,Brother, we have a lot of requests.
Dialogue: 0,0:07:14.10,0:07:15.85,Default,,0000,0000,0000,,I don't know if I can fulfill them all.
Dialogue: 0,0:07:16.02,0:07:17.02,Default,,0000,0000,0000,,We'll do them all.
Dialogue: 0,0:07:17.31,0:07:19.82,Default,,0000,0000,0000,,If there are people in need,\Nwe'll save all of them.
Dialogue: 0,0:07:20.53,0:07:22.69,Default,,0000,0000,0000,,That's the purpose of the Akatsuki.
Dialogue: 0,0:07:23.32,0:07:27.16,Default,,0000,0000,0000,,At this rate, it would be great if we had\Nan organization as big as Hanzo's.
Dialogue: 0,0:07:27.49,0:07:30.79,Default,,0000,0000,0000,,Hanzo is setting an example\Nfor us shinobi
Dialogue: 0,0:07:30.87,0:07:34.16,Default,,0000,0000,0000,,by fighting to bring peace to this world.
Dialogue: 0,0:07:34.79,0:07:38.88,Default,,0000,0000,0000,,We will prove that the Akatsuki\Ncan surpass him one day!
Dialogue: 0,0:07:43.55,0:07:44.59,Default,,0000,0000,0000,,Akatsuki?
Dialogue: 0,0:07:45.01,0:07:48.26,Default,,0000,0000,0000,,Yes… I'm hearing rumors\Nhere and there lately.
Dialogue: 0,0:07:48.68,0:07:52.14,Default,,0000,0000,0000,,Hmm… To think there are shinobi\Nlike them in our village…
Dialogue: 0,0:07:52.68,0:07:56.35,Default,,0000,0000,0000,,I approve of an organization that seeks to\Nresolve issues through pacifistic means.
Dialogue: 0,0:07:56.39,0:08:00.52,Default,,0000,0000,0000,,It will help realize my dream of uniting\Nthe Five Nations without force.
Dialogue: 0,0:08:01.27,0:08:04.44,Default,,0000,0000,0000,,But they sure made\Na sudden appearance.
Dialogue: 0,0:08:04.44,0:08:06.32,Default,,0000,0000,0000,,Is there someone behind them?
Dialogue: 0,0:08:06.61,0:08:10.28,Default,,0000,0000,0000,,According to rumors, they're pupils of\NJiraiya of Hidden Leaf Village.
Dialogue: 0,0:08:10.45,0:08:13.75,Default,,0000,0000,0000,,Jiraiya? One of the Leaf's Sannin?
Dialogue: 0,0:08:13.95,0:08:16.96,Default,,0000,0000,0000,,Yes. After he fought you, Master Hanzo,
Dialogue: 0,0:08:17.00,0:08:21.34,Default,,0000,0000,0000,,Jiraiya stayed in this area for about three\Nyears, teaching ninjutsu to war orphans.
Dialogue: 0,0:08:28.13,0:08:30.60,Default,,0000,0000,0000,,So they are the apprentices of that brat!
Dialogue: 0,0:08:32.06,0:08:34.31,Default,,0000,0000,0000,,The compassion to help people…
Dialogue: 0,0:08:34.43,0:08:38.44,Default,,0000,0000,0000,,Akatsuki could just be the repayment of\Na favor that's finally come around.
Dialogue: 0,0:08:45.11,0:08:46.44,Default,,0000,0000,0000,,Say, Brother Yahiko…
Dialogue: 0,0:08:47.03,0:08:50.91,Default,,0000,0000,0000,,Seems villages along the border\Nare being attacked lately.
Dialogue: 0,0:08:51.45,0:08:52.49,Default,,0000,0000,0000,,What is that about?
Dialogue: 0,0:08:52.78,0:08:57.12,Default,,0000,0000,0000,,It's hard to tell for sure since they're in\Nout of the way places with little contact.
Dialogue: 0,0:08:57.71,0:09:00.67,Default,,0000,0000,0000,,But it seems the border\Nwith the Land of Earth is pretty bad.
Dialogue: 0,0:09:01.33,0:09:02.75,Default,,0000,0000,0000,,That's the Seventh Ward…
Dialogue: 0,0:09:03.38,0:09:05.46,Default,,0000,0000,0000,,Shall we go and check, Nagato, Konan?
Dialogue: 0,0:09:05.46,0:09:06.51,Default,,0000,0000,0000,,Yes.
Dialogue: 0,0:09:06.55,0:09:08.01,Default,,0000,0000,0000,,Kyusuke, Daibutsu…
Dialogue: 0,0:09:08.51,0:09:09.76,Default,,0000,0000,0000,,Keep an eye on things in our absence.
Dialogue: 0,0:09:09.97,0:09:11.18,Default,,0000,0000,0000,,You got it.
Dialogue: 0,0:09:11.22,0:09:12.47,Default,,0000,0000,0000,,Leave it to us, Brother!
Dialogue: 0,0:09:18.77,0:09:19.85,Default,,0000,0000,0000,,How terrible…
Dialogue: 0,0:09:20.65,0:09:22.02,Default,,0000,0000,0000,,Who would do such a thing?
Dialogue: 0,0:09:22.31,0:09:26.57,Default,,0000,0000,0000,,This is far worse than anything\Nwe've ever handled.
Dialogue: 0,0:09:28.45,0:09:29.70,Default,,0000,0000,0000,,Who's there?! Come out!
Dialogue: 0,0:09:38.37,0:09:40.50,Default,,0000,0000,0000,,Who are you guys?
Dialogue: 0,0:09:40.92,0:09:42.08,Default,,0000,0000,0000,,Akatsuki.
Dialogue: 0,0:09:42.25,0:09:43.38,Default,,0000,0000,0000,,Akatsuki?
Dialogue: 0,0:09:44.67,0:09:47.38,Default,,0000,0000,0000,,You seem to be shinobi\Nfrom the Hidden Rain.
Dialogue: 0,0:09:47.42,0:09:50.72,Default,,0000,0000,0000,,We are an organization working\Nto bring peace to this village.
Dialogue: 0,0:09:51.63,0:09:54.30,Default,,0000,0000,0000,,We heard this area was under attack,\Nso we came to investigate.
Dialogue: 0,0:09:54.68,0:09:56.14,Default,,0000,0000,0000,,Who did this?
Dialogue: 0,0:09:56.26,0:09:58.31,Default,,0000,0000,0000,,– What do we do?\N– Can we trust them?
Dialogue: 0,0:09:59.10,0:10:00.27,Default,,0000,0000,0000,,The Hidden Stone.
Dialogue: 0,0:10:00.44,0:10:01.44,Default,,0000,0000,0000,,The Hidden Stone?
Dialogue: 0,0:10:01.52,0:10:02.31,Default,,0000,0000,0000,,No way!
Dialogue: 0,0:10:02.85,0:10:07.03,Default,,0000,0000,0000,,I heard that the Stone and\Nthe Leaf are negotiating a cease-fire.
Dialogue: 0,0:10:07.23,0:10:08.99,Default,,0000,0000,0000,,How the hell should I know!
Dialogue: 0,0:10:09.44,0:10:10.82,Default,,0000,0000,0000,,Look at this village!
Dialogue: 0,0:10:11.03,0:10:14.45,Default,,0000,0000,0000,,The Hidden Stone ravaged\Nthis entire area one after another!
Dialogue: 0,0:10:14.87,0:10:17.87,Default,,0000,0000,0000,,What do you plan to do\Nwith such weapons?
Dialogue: 0,0:10:18.04,0:10:20.50,Default,,0000,0000,0000,,Fight! What else?
Dialogue: 0,0:10:20.71,0:10:23.33,Default,,0000,0000,0000,,We've already formed an alliance!
Dialogue: 0,0:10:23.50,0:10:27.09,Default,,0000,0000,0000,,Hold on! That will only cause\Nmore casualties!
Dialogue: 0,0:10:27.17,0:10:28.76,Default,,0000,0000,0000,,Then what are we supposed to do?
Dialogue: 0,0:10:28.84,0:10:32.51,Default,,0000,0000,0000,,Are you telling us to sit here\Nand wait for our turn to die?!
Dialogue: 0,0:10:32.76,0:10:35.22,Default,,0000,0000,0000,,We'll go talk about this\Nwith the Hidden Stone.
Dialogue: 0,0:10:35.51,0:10:36.47,Default,,0000,0000,0000,,What?
Dialogue: 0,0:10:36.97,0:10:40.43,Default,,0000,0000,0000,,If we acknowledge their pain too,\Nthen we can speak honestly.
Dialogue: 0,0:10:40.48,0:10:41.73,Default,,0000,0000,0000,,They're sure to understand!
Dialogue: 0,0:10:41.85,0:10:43.39,Default,,0000,0000,0000,,Enough with your ideals!
Dialogue: 0,0:10:43.39,0:10:46.44,Default,,0000,0000,0000,,What about getting even\Nfor our friends who were killed?!
Dialogue: 0,0:10:48.48,0:10:52.49,Default,,0000,0000,0000,,Only by understanding the pain of others\Nand shedding tears like them
Dialogue: 0,0:10:52.49,0:10:55.49,Default,,0000,0000,0000,,can we get close to the real world…
Dialogue: 0,0:10:56.45,0:10:58.41,Default,,0000,0000,0000,,{\i1}Shedding tears with them?{\i0}
Dialogue: 0,0:10:58.83,0:11:00.66,Default,,0000,0000,0000,,{\i1}So you mean revenge?{\i0}
Dialogue: 0,0:11:01.33,0:11:03.46,Default,,0000,0000,0000,,{\i1}No, that's not what I meant.{\i0}
Dialogue: 0,0:11:03.75,0:11:05.42,Default,,0000,0000,0000,,{\i1}I meant understanding each other.{\i0}
Dialogue: 0,0:11:06.13,0:11:09.09,Default,,0000,0000,0000,,{\i1}Stop talking so idealistically.{\i0}
Dialogue: 0,0:11:09.42,0:11:11.51,Default,,0000,0000,0000,,{\i1}There's no such thing in this world.{\i0}
Dialogue: 0,0:11:12.83,0:11:14.12,Default,,0000,0000,0000,,What's going on?
Dialogue: 0,0:11:14.41,0:11:18.71,Default,,0000,0000,0000,,If this goes on, the peace between\Nthe Stone and Leaf will be shattered.
Dialogue: 0,0:11:19.50,0:11:21.92,Default,,0000,0000,0000,,Maybe the Hidden Stone aren't unified.
Dialogue: 0,0:11:22.34,0:11:24.38,Default,,0000,0000,0000,,Maybe the leaders are pursuing peace,
Dialogue: 0,0:11:24.42,0:11:27.13,Default,,0000,0000,0000,,but the shinobi under them\Ndon't want it.
Dialogue: 0,0:11:27.59,0:11:30.34,Default,,0000,0000,0000,,It could also be the work\Nof Rogue Ninja.
Dialogue: 0,0:11:31.34,0:11:34.68,Default,,0000,0000,0000,,Whatever the case, we must settle this\Nquickly with the Hidden Stone!
Dialogue: 0,0:11:36.98,0:11:37.60,Default,,0000,0000,0000,,Scatter!
Dialogue: 0,0:11:39.31,0:11:40.23,Default,,0000,0000,0000,,What?!
Dialogue: 0,0:11:44.27,0:11:45.82,Default,,0000,0000,0000,,So many waiting to ambush us!
Dialogue: 0,0:11:47.90,0:11:48.61,Default,,0000,0000,0000,,Wait!
Dialogue: 0,0:11:49.20,0:11:50.99,Default,,0000,0000,0000,,You're Hidden Stone Shinobi, right?
Dialogue: 0,0:11:51.53,0:11:54.24,Default,,0000,0000,0000,,We are the Akatsuki,\Nshinobi from the Hidden Rain!
Dialogue: 0,0:11:54.99,0:11:56.54,Default,,0000,0000,0000,,We've come to talk!
Dialogue: 0,0:11:58.33,0:11:59.33,Default,,0000,0000,0000,,Damn!
Dialogue: 0,0:11:59.33,0:12:00.96,Default,,0000,0000,0000,,They refuse to talk!
Dialogue: 0,0:12:01.33,0:12:02.33,Default,,0000,0000,0000,,What do we do, Yahiko?
Dialogue: 0,0:12:02.96,0:12:05.38,Default,,0000,0000,0000,,Our goal is negotiation, at all cost!
Dialogue: 0,0:12:06.00,0:12:07.01,Default,,0000,0000,0000,,Do not kill them!
Dialogue: 0,0:12:15.97,0:12:17.47,Default,,0000,0000,0000,,{\i1}This can't be real!{\i0}
Dialogue: 0,0:12:17.72,0:12:18.77,Default,,0000,0000,0000,,Yahiko!
Dialogue: 0,0:12:23.02,0:12:23.86,Default,,0000,0000,0000,,Yahiko!
Dialogue: 0,0:12:24.15,0:12:24.86,Default,,0000,0000,0000,,Konan!
Dialogue: 0,0:12:52.38,0:12:53.34,Default,,0000,0000,0000,,{\i1}Who's there?!{\i0}
Dialogue: 0,0:12:55.72,0:12:57.01,Default,,0000,0000,0000,,{\i1}Damn! The enemy?!{\i0}
Dialogue: 0,0:12:57.39,0:12:59.02,Default,,0000,0000,0000,,{\i1}Run now!{\i0}
Dialogue: 0,0:12:59.89,0:13:01.35,Default,,0000,0000,0000,,{\i1}Nagato, go!{\i0}
Dialogue: 0,0:13:09.19,0:13:12.95,Default,,0000,0000,0000,,{\i1}Nagato… Hurry… Get away…{\i0}
Dialogue: 0,0:13:16.99,0:13:18.37,Default,,0000,0000,0000,,{\i1}Run, Nagato!{\i0}
Dialogue: 0,0:13:19.33,0:13:21.12,Default,,0000,0000,0000,,{\i1}Run away! Hurry!{\i0}
Dialogue: 0,0:13:49.15,0:13:50.11,Default,,0000,0000,0000,,Nagato!
Dialogue: 0,0:13:50.28,0:13:51.36,Default,,0000,0000,0000,,Stop, Nagato!
Dialogue: 0,0:13:52.24,0:13:53.03,Default,,0000,0000,0000,,Stop!
Dialogue: 0,0:13:53.36,0:13:54.57,Default,,0000,0000,0000,,Stop it, Nagato!
Dialogue: 0,0:13:56.45,0:13:57.32,Default,,0000,0000,0000,,Yahiko…
Dialogue: 0,0:14:02.75,0:14:04.04,Default,,0000,0000,0000,,We retreat for now!
Dialogue: 0,0:14:12.38,0:14:14.88,Default,,0000,0000,0000,,So that was the power\Nof the Rinnegan…
Dialogue: 0,0:14:15.34,0:14:17.59,Default,,0000,0000,0000,,Such a frightening power.
Dialogue: 0,0:14:18.35,0:14:19.39,Default,,0000,0000,0000,,What should we do?
Dialogue: 0,0:14:19.72,0:14:22.14,Default,,0000,0000,0000,,If the Akatsuki start moving,
Dialogue: 0,0:14:22.18,0:14:24.77,Default,,0000,0000,0000,,our other teams disguised as shinobi\Nfrom the other side will be in danger.
Dialogue: 0,0:14:25.27,0:14:27.31,Default,,0000,0000,0000,,For the future of the Hidden Leaf,
Dialogue: 0,0:14:27.35,0:14:29.90,Default,,0000,0000,0000,,peace must not exist\Nwith the Hidden Stone.
Dialogue: 0,0:14:30.32,0:14:35.32,Default,,0000,0000,0000,,This strategy was precisely\Nto obstruct peace.
Dialogue: 0,0:14:36.07,0:14:38.78,Default,,0000,0000,0000,,Perhaps we can use that power.
Dialogue: 0,0:14:39.45,0:14:41.58,Default,,0000,0000,0000,,Collect all of the corpses here!
Dialogue: 0,0:14:41.62,0:14:44.54,Default,,0000,0000,0000,,Get rid of any traces of the Hidden Leaf!
Dialogue: 0,0:14:44.83,0:14:45.37,Default,,0000,0000,0000,,Sir!
Dialogue: 0,0:14:48.33,0:14:49.79,Default,,0000,0000,0000,,Danzo, huh…
Dialogue: 0,0:14:50.09,0:14:52.30,Default,,0000,0000,0000,,The Hidden Leaf is working\Nbehind the scenes…
Dialogue: 0,0:14:58.01,0:14:58.89,Default,,0000,0000,0000,,Nagato…
Dialogue: 0,0:14:59.84,0:15:02.97,Default,,0000,0000,0000,,Yahiko… That jutsu that\NNagato used earlier…
Dialogue: 0,0:15:03.47,0:15:06.39,Default,,0000,0000,0000,,It's probably a jutsu he can perform\Nbecause he has the Rinnegan.
Dialogue: 0,0:15:06.98,0:15:07.98,Default,,0000,0000,0000,,That's…
Dialogue: 0,0:15:08.31,0:15:09.85,Default,,0000,0000,0000,,Yeah, I felt it.
Dialogue: 0,0:15:10.48,0:15:11.73,Default,,0000,0000,0000,,It was just like that time…
Dialogue: 0,0:15:20.12,0:15:22.78,Default,,0000,0000,0000,,That intense desire to kill made\Nmy hairs stand on end…
Dialogue: 0,0:15:23.24,0:15:25.70,Default,,0000,0000,0000,,That power is not of this world.
Dialogue: 0,0:15:26.16,0:15:27.96,Default,,0000,0000,0000,,We must not let Nagato use it!
Dialogue: 0,0:15:28.50,0:15:31.96,Default,,0000,0000,0000,,If he does… I think it will kill him.
Dialogue: 0,0:15:52.61,0:15:54.36,Default,,0000,0000,0000,,Konan, where's Yahiko?
Dialogue: 0,0:15:55.90,0:15:57.24,Default,,0000,0000,0000,,He went to look for food.
Dialogue: 0,0:15:57.53,0:15:58.28,Default,,0000,0000,0000,,Oh…
Dialogue: 0,0:16:00.53,0:16:01.45,Default,,0000,0000,0000,,What's the matter?
Dialogue: 0,0:16:03.78,0:16:06.41,Default,,0000,0000,0000,,I'm scared…of my power.
Dialogue: 0,0:16:08.33,0:16:09.29,Default,,0000,0000,0000,,Nagato…
Dialogue: 0,0:16:10.96,0:16:13.08,Default,,0000,0000,0000,,Before, I had Jiraiya Sensei.
Dialogue: 0,0:16:14.21,0:16:19.42,Default,,0000,0000,0000,,I'm positive he taught me ninjutsu\Nso that I could control this power.
Dialogue: 0,0:16:20.88,0:16:23.89,Default,,0000,0000,0000,,But Jiraiya Sensei is no longer here…
Dialogue: 0,0:16:24.72,0:16:27.52,Default,,0000,0000,0000,,Yes, Jiraiya Sensei is gone…
Dialogue: 0,0:16:28.35,0:16:29.39,Default,,0000,0000,0000,,But…
Dialogue: 0,0:16:30.27,0:16:31.94,Default,,0000,0000,0000,,You have us.
Dialogue: 0,0:16:32.65,0:16:35.32,Default,,0000,0000,0000,,We'll control your power.
Dialogue: 0,0:16:35.61,0:16:36.98,Default,,0000,0000,0000,,For as long as it takes…
Dialogue: 0,0:16:37.61,0:16:38.57,Default,,0000,0000,0000,,Konan…
Dialogue: 0,0:16:56.71,0:16:58.42,Default,,0000,0000,0000,,Be careful, Yahiko.
Dialogue: 0,0:16:59.21,0:17:00.76,Default,,0000,0000,0000,,You don't need to see me off!
Dialogue: 0,0:17:01.84,0:17:03.89,Default,,0000,0000,0000,,See to Nagato's wound, will you?
Dialogue: 0,0:17:05.35,0:17:08.85,Default,,0000,0000,0000,,From now on,\Nsave your worrying for him.
Dialogue: 0,0:17:09.68,0:17:11.68,Default,,0000,0000,0000,,Nagato is the keystone of the Akatsuki!
Dialogue: 0,0:17:12.27,0:17:15.73,Default,,0000,0000,0000,,He's the one who will change\Nthis nation…this world.
Dialogue: 0,0:17:16.31,0:17:18.36,Default,,0000,0000,0000,,The same can be said of you…
Dialogue: 0,0:17:18.94,0:17:22.07,Default,,0000,0000,0000,,Everyone respects and admires you.
Dialogue: 0,0:17:22.28,0:17:24.82,Default,,0000,0000,0000,,Nagato is no different.
Dialogue: 0,0:17:26.20,0:17:30.37,Default,,0000,0000,0000,,Nagato is going to be\Nthe bridge to peace.
Dialogue: 0,0:17:32.75,0:17:36.96,Default,,0000,0000,0000,,My role is to be a pillar\Nthat supports that bridge.
Dialogue: 0,0:17:38.75,0:17:39.84,Default,,0000,0000,0000,,Yahiko…
Dialogue: 0,0:17:42.76,0:17:45.89,Default,,0000,0000,0000,,This nation continues to weep.
Dialogue: 0,0:17:46.64,0:17:48.47,Default,,0000,0000,0000,,It continues to endure pain.
Dialogue: 0,0:17:49.64,0:17:53.14,Default,,0000,0000,0000,,I used to hate this land that\Ncries all the time.
Dialogue: 0,0:17:54.23,0:17:55.94,Default,,0000,0000,0000,,But now, I want to save it.
Dialogue: 0,0:17:57.40,0:17:59.11,Default,,0000,0000,0000,,It's what I want with all my heart.
Dialogue: 0,0:18:02.32,0:18:05.53,Default,,0000,0000,0000,,It reminds me of the crybaby\Nthat I used to be and I can't ignore it.
Dialogue: 0,0:18:07.32,0:18:08.24,Default,,0000,0000,0000,,Yahiko!
Dialogue: 0,0:18:11.66,0:18:12.83,Default,,0000,0000,0000,,Be careful.
Dialogue: 0,0:18:14.12,0:18:15.92,Default,,0000,0000,0000,,He is the bridge to peace.…
Dialogue: 0,0:18:17.25,0:18:19.21,Default,,0000,0000,0000,,His will itself is the bridge.
Dialogue: 0,0:18:21.17,0:18:22.09,Default,,0000,0000,0000,,Nagato…
Dialogue: 0,0:18:23.13,0:18:25.09,Default,,0000,0000,0000,,We believe in Yahiko!
Dialogue: 0,0:18:25.93,0:18:26.97,Default,,0000,0000,0000,,Let's go, everyone!
Dialogue: 0,0:18:27.34,0:18:28.39,Default,,0000,0000,0000,,Right!
Dialogue: 0,0:18:40.27,0:18:42.36,Default,,0000,0000,0000,,What made this happen?
Dialogue: 0,0:18:45.07,0:18:47.32,Default,,0000,0000,0000,,It was too late when we found them.
Dialogue: 0,0:18:48.03,0:18:51.62,Default,,0000,0000,0000,,We were on the way to a peace conference\Nwith the Land of Earth…
Dialogue: 0,0:18:51.70,0:18:55.25,Default,,0000,0000,0000,,We recognized them\Nas members of your clan, and…
Dialogue: 0,0:18:56.92,0:19:00.29,Default,,0000,0000,0000,,Lord Danzo, did you see the perpetrators?
Dialogue: 0,0:19:00.54,0:19:02.84,Default,,0000,0000,0000,,No doubt it was the Akatsuki.
Dialogue: 0,0:19:03.25,0:19:05.72,Default,,0000,0000,0000,,Akatsuki?! But the Akatsuki is…
Dialogue: 0,0:19:06.76,0:19:08.18,Default,,0000,0000,0000,,A group that expounds peace
Dialogue: 0,0:19:08.22,0:19:11.47,Default,,0000,0000,0000,,and has been growing in influence\Nlately in this village.
Dialogue: 0,0:19:11.51,0:19:15.85,Default,,0000,0000,0000,,Yes, I hear they are an organization\Nthat shares my will!
Dialogue: 0,0:19:16.85,0:19:18.23,Default,,0000,0000,0000,,So why…?
Dialogue: 0,0:19:19.19,0:19:22.61,Default,,0000,0000,0000,,Hanzo, you left them on their own too long.
Dialogue: 0,0:19:23.32,0:19:24.28,Default,,0000,0000,0000,,What?
Dialogue: 0,0:19:25.36,0:19:28.40,Default,,0000,0000,0000,,It's fine to be idealistic.
Dialogue: 0,0:19:28.45,0:19:31.24,Default,,0000,0000,0000,,However, therein lies your weakness.
Dialogue: 0,0:19:31.74,0:19:33.45,Default,,0000,0000,0000,,My weakness?
Dialogue: 0,0:19:34.45,0:19:37.00,Default,,0000,0000,0000,,Because of your high ideals,
Dialogue: 0,0:19:37.04,0:19:39.87,Default,,0000,0000,0000,,you are blind to those\Nwith similar motives.
Dialogue: 0,0:19:40.21,0:19:44.25,Default,,0000,0000,0000,,But they became the enemy\Nand turned against you.
Dialogue: 0,0:19:44.30,0:19:46.67,Default,,0000,0000,0000,,That is what caused this tragedy.
Dialogue: 0,0:19:47.42,0:19:53.18,Default,,0000,0000,0000,,However noble your ideals,\Nnot everyone will buy into them.
Dialogue: 0,0:19:53.93,0:19:56.89,Default,,0000,0000,0000,,They may act like\Nthey share your hopes,
Dialogue: 0,0:19:56.93,0:19:59.27,Default,,0000,0000,0000,,but they're just a disorderly mob.
Dialogue: 0,0:19:59.39,0:20:01.44,Default,,0000,0000,0000,,Ultimately, they'll be obsessed\Nby greed and power
Dialogue: 0,0:20:01.48,0:20:03.61,Default,,0000,0000,0000,,turn into a cast of evil across the land.
Dialogue: 0,0:20:04.02,0:20:06.57,Default,,0000,0000,0000,,Cast off the sheep's clothing\Nfrom the Akatsuki
Dialogue: 0,0:20:06.61,0:20:09.82,Default,,0000,0000,0000,,and you'll find they're just wolves\Nout to take over the village too.
Dialogue: 0,0:20:10.20,0:20:11.53,Default,,0000,0000,0000,,Lord Hanzo…
Dialogue: 0,0:20:14.03,0:20:18.58,Default,,0000,0000,0000,,If nothing is done,\Nthey will take over this village.
Dialogue: 0,0:20:24.21,0:20:25.92,Default,,0000,0000,0000,,Danzo of the Hidden Leaf, huh?
Dialogue: 0,0:20:26.42,0:20:28.63,Default,,0000,0000,0000,,An interesting person\Nhas started taking action.
Dialogue: 0,0:20:28.67,0:20:32.84,Default,,0000,0000,0000,,Just like Obito said,\Nthe clouds are moving strangely.
Dialogue: 0,0:20:33.34,0:20:35.81,Default,,0000,0000,0000,,There is no such thing as peace\Nin this world.
Dialogue: 0,0:20:35.85,0:20:37.18,Default,,0000,0000,0000,,That is the reality.
Dialogue: 0,0:20:37.93,0:20:40.98,Default,,0000,0000,0000,,Reality is like this grand river\Nthat flows here.
Dialogue: 0,0:20:41.39,0:20:43.31,Default,,0000,0000,0000,,No matter how hard one tries to stop it,
Dialogue: 0,0:20:43.31,0:20:48.57,Default,,0000,0000,0000,,reality will swallow you up whole\Nand mercilessly crush you.
Dialogue: 0,0:20:48.94,0:20:52.57,Default,,0000,0000,0000,,There is only way to escape that suffering.
Dialogue: 0,0:20:54.24,0:20:58.41,Default,,0000,0000,0000,,That is the Infinite Tsukuyomi,\NProject Tsuki no Me!
Dialogue: 0,0:22:37.22,0:22:40.93,PV,,0000,0000,0000,,Nagato, reality is\Nmoving ahead quickly.\N\N
Dialogue: 0,0:22:41.47,0:22:43.31,PV,,0000,0000,0000,,Coordinate your actions with me.\N\N
Dialogue: 0,0:22:43.94,0:22:46.65,PV,,0000,0000,0000,,What Yahiko talks about\Nare just ideals.\N\N
Dialogue: 0,0:22:47.06,0:22:50.40,PV,,0000,0000,0000,,No matter what you say,\NI believe in Yahiko's ideals!\N\N
Dialogue: 0,0:22:50.90,0:22:55.28,PV,,0000,0000,0000,,Even though you possess the Rinnegan,\Nyou still think like an average person.\N\N
Dialogue: 0,0:22:56.24,0:22:59.78,PV,,0000,0000,0000,,Next time on Naruto Shippuden:\N"The New Akatsuki"\N\N\N
Dialogue: 0,0:23:00.33,0:23:03.35,PV,,0000,0000,0000,,Very well…\NGo see reality for yourself!\N\N\N
Dialogue: 0,0:23:03.35,0:23:05.12,PV,,0000,0000,0000,,THE NEW AKATSUKI\N\N\N
Dialogue: 0,0:23:05.16,0:23:09.42,PV,,0000,0000,0000,,Tune in again!\N\N\N



*/

/*
[Script Info]
; Script generated by Aegisub 2.1.8
; http://www.aegisub.org/
Title: Default Aegisub file
ScriptType: v4.00+
WrapStyle: 0
PlayResX: 640
PlayResY: 480
ScaledBorderAndShadow: yes
Video Aspect Ratio: 0
Video Zoom: 6
Video Position: 0

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text

Dialogue: 0,0:00:01.00,0:00:10.00,Default,,0000,0000,0000,,{\b1\c&H0000FF&\fs36}Phim dành cho người lớn{\b0}\N {\i1\b1\c&H00FFFF&\fs36}Cấm trẻ em dưới 18 tuổi & người trên 81 tuổi !{\i0\b0}
Dialogue: 0,0:00:11.00,0:00:15.00,Default,,0000,0000,0000,,{\c&H00FFFF&\b1\fs36}EMPIRE.Vol.1 \N 50 Bukkake & Creampie
Dialogue: 0,0:00:15.00,0:00:19.00,Default,,0000,0000,0000,,Starring:{\b1\i1\c&HFFFF00&}Yui Hatano [波多野結衣]  \N {\b0\c&H838219&}Height: 163cm  -  B88 W59 H85{\b1}
Dialogue: 0,0:10:00.00,0:10:10.00,Default,,0000,0000,0000,,{\b1\c&HFFFF00&\fs72}LauXanh.Us{\b0} \N {\c&HBF9AC5&\fs36}Mang cả thế giới vào giường của bạn !
*/