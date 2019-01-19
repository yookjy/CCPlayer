using CCPlayer.HWCodecs.Text.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Lime.Encoding;
using Mozilla.Charset;
#if ARM
using Lime.Helpers;
#endif

namespace CCPlayer.HWCodecs.Text.Parsers
{
    public class SubtitleParserEventArgs : EventArgs
    {
        private Subtitle _Subtitle;

        public Subtitle Subtitle
        {
            get
            {
                return _Subtitle;
            }
        }

        private Exception _Exception;

        public Exception Exception
        {
            get
            {
                return _Exception;
            }
        }

        public SubtitleParserEventArgs()
        {
        }

        public SubtitleParserEventArgs(Subtitle subtitle)
        {
            _Subtitle = subtitle;
        }

        public SubtitleParserEventArgs(Exception exception)
        {
            _Exception = exception;
        }
    }

    public abstract class SubtitleParser
    {
        public delegate void CompletedHandler(object sender, SubtitleParserEventArgs args);
        public delegate void UnknownEncodingHandler(object sender, SubtitleParserEventArgs args);
        public delegate void FailedHandler(object sender, SubtitleParserEventArgs args);

        public event CompletedHandler Completed;
        public event UnknownEncodingHandler UnknownEncoding;
        public event FailedHandler Failed;

        //protected ISubtitleReader CurrentSubtitle;

        public SubtitleParser() 
        {
        }

        //public SubtitleParser(ISubtitleReader CurrentSubtitle)
        //{
        //    this.CurrentSubtitle = CurrentSubtitle;
        //}

        protected void NotifyCompleted(Subtitle subtitle)
        {
            if (Completed != null)
            {
                Completed(this, new SubtitleParserEventArgs(subtitle));
            }
        }

        protected void NotifyUnknownEncoding()
        {
            if (UnknownEncoding != null)
            {
                UnknownEncoding(this, new SubtitleParserEventArgs());
            }
        }

        protected void NotifyFailed(Exception e)
        {
            if (Failed != null)
            {
                Failed(this, new SubtitleParserEventArgs(e));
            }
        }

        public virtual Subtitle Parse(Stream stream, int codePage)
        {
            //스트림 초기화
            stream.Seek(0, SeekOrigin.Begin);
            var encResult = SubtitleEncodingResult.Success;
            Subtitle result = null;
            string subtitleText = null;
            Charset charset = null;

            //자동검색인 경우 인코딩 검색
            if (codePage == CodePage.AUTO_DETECT_VALUE)
            {
                var cpDetect = new NativeHelper.CodepageDetector();

                if (this is SmiParser)
                {
                    cpDetect.Init(NativeHelper.mlDetectCp.MLDETECTCP_HTML, 0);
                }
                else
                {
                    cpDetect.Init(NativeHelper.mlDetectCp.MLDETECTCP_NONE, 0);
                }
                
                codePage = cpDetect.Detect(stream.AsRandomAccessStream());
                var confidence = cpDetect.GetConfidence();
                //System.Diagnostics.Debug.WriteLine("확률.... : " + confi);

                if (codePage == -1 || confidence <= 95)
                {
                    charset = CodePage.Detect(stream, true);

                    if (!charset.IsFound)
                    {
                        encResult = SubtitleEncodingResult.UnkownEncoding;
                        //코드 페이지가 설정되어 있지 않은 경우만 UTF-8을 적용 (시스템 설정이 적용되어 있을수도 있음)
                        if (codePage <= 0) codePage = CodePage.UTF8_CODE_PAGE;
                    }
                    else
                    {
                        //재검색의 정확도가 더 높으면 사용, 아니면 처음것을 사용
                        if (confidence < charset.Confidence)
                        {
                            codePage = charset.CodePage;
                        }
                    }
                }
            }

            try
            {
#if ARM
                subtitleText = EncodingHelper.ConvertEncoding(stream, codePage);
#else
                subtitleText = new StreamReader(stream).ReadToEnd();
#endif
            } 
            catch (Exception)
            {
                encResult = SubtitleEncodingResult.Fail;
                codePage = CodePage.UTF8_CODE_PAGE;

                StreamReader reader = new StreamReader(stream);
                subtitleText = reader.ReadToEnd();
            }
                
            if (string.IsNullOrEmpty(subtitleText))
            {
                encResult = SubtitleEncodingResult.UnkownEncoding;
                codePage = CodePage.UTF8_CODE_PAGE;

                StreamReader reader = new StreamReader(stream);
                subtitleText = reader.ReadToEnd();
            }

            result = Parse(subtitleText);
            result.EncodingResult = encResult;
            result.Parser = this;
            result.CurrentCodePage = codePage;
            
            if (stream.CanSeek)
            {
                if (stream is MemoryStream)
                {
                    result.Source = stream;
                }
                else
                {
                    result.Source = new MemoryStream();
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(result.Source);
                }
            }

            return result;
        }

        abstract protected Subtitle Parse(string parsedString);

    }

    public class SubtitleParserFactory
    {
        public static SubtitleParser CreateParser(string fileName)
        {
            switch (Path.GetExtension(fileName).ToUpper())
            {
                case ".SRT":
                    return new SrtParser();
                case ".SMI":
                    return new SmiParser();
                case ".ASS":
                case ".SSA":
                    return new AssParser();
            }
            return null;
        }
    }
}
