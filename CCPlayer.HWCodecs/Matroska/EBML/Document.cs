using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Document : MasterElement
    {
        public List<EBML> Header { get; private set; }

        public List<Segment> Segments { get; private set; }

        private Document()  : base()
        {
            Header = new List<EBML>();
            Segments = new List<Segment>();
        }

        public static Document ReadFromStream(Stream stream)
        {
            Document doc = new Document()
            {
                Offset = 0,
                Stream = stream,
                Size = (ulong)stream.Length
            };
            doc.Header = new List<EBML>();
            doc.Segments = new List<Segment>();

            if (!doc.TryParse(stream, doc.Header, true))
            {
                //mkv 아냥..
                return doc;
            }

            if (!doc.TryParse(stream, doc.Segments, false))
            {
                //Segment 없어!
                return doc;
            }

            foreach(var segment in doc.Segments)
            {
                stream.Seek(segment.Offset, SeekOrigin.Begin);
                segment.SeekHead = new List<SeekHead>();
                segment.TryParse<SeekHead>(stream, segment.SeekHead, true);

                if (segment.SeekHead.Count > 0)
                {
                    //메타 시크가 검색이 되었다면 개별적으로 로드
                    foreach(var seekHead in segment.SeekHead)
                    {
                        foreach(var seek in seekHead.Seek)
                        {
                            //스트림 포지션 이동해야 된다....
                            stream.Seek(segment.Offset + (long)seek.SeekPosition, SeekOrigin.Begin);

                            switch(seek.SeekID)
                            {
                                case ElementID.Info:
                                    //그리고 ConvVlong 과 ToUlong 비교해보자...
                                    segment.TryParse<Info>(stream, segment.Info, true);
                                    break;
                                case ElementID.Tracks:
                                    segment.TryParse<Tracks>(stream, segment.Tracks, true);
                                    break;
                                case ElementID.Cues:
                                    Cues cues = null;
                                    if (segment.TryParse<Cues>(stream, out cues , true))
                                    {
                                        segment.Cues = cues;
                                    }
                                    break;
                                //폰트가 저장할때 사용
                                case ElementID.Attachments:
                                    Attachments attachments = null;
                                    if (segment.TryParse<Attachments>(stream, out attachments, true))
                                    {
                                        segment.Attachments = attachments;
                                    }
                                    break;
                                //사용할일이 없다....
                                case ElementID.Chapters:
                                    Chapters chapters = null;
                                    if (segment.TryParse<Chapters>(stream, out chapters, true))
                                    {
                                    //    segment.Chapters = chapters;
                                    }
                                    break;
                                //사용할일이 현재로썬 없다....
                                case ElementID.Tags:
                                    //segment.TryParse<Tags>(stream, segment.Tags, true);
                                    break;
                            }
                        }
                    }
                }

                if (doc.Segments[0].Tracks == null || !doc.Segments[0].Tracks.Any())
                {
                    //메타 시크가 검색되지 않았으므로, 순차적으로 전체(클러스터 포함) 로딩...(이 경우.. .로딩 프로그레스 처리 고려)
                    segment.LoadChildren();
                }
            }
            return doc;
        }


        public override ulong ID
        {
            get { throw new NotImplementedException(); }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong[] MasterElementIDs
        {
            get { throw new NotImplementedException(); }
        }
    }
}
