using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Segment : MasterElement
    {
        //internal Segment(Stream stream, long offset) : base(stream, offset) {}
        public Segment() : base() 
        {
            SeekHead = new List<SeekHead>();
            Info = new List<Info>();
            Tracks = new List<Tracks>();
            //Cluster = new List<Cluster>();
            Tags = new List<Tags>();
        }
        
        public override ulong ID
        {
            get { return ElementID.Segment; }
        }

        public List<SeekHead> SeekHead { get; set; }
        public List<Info> Info { get; set; }
        public Cues Cues { get; set; }
        public List<Tracks> Tracks { get; set; }
        //public List<Cluster> Cluster { get; set; }
        public Cluster Cluster { get; set; }
        public Attachments Attachments { get; set; }
        public Chapters Chapters { get; set; }
        public List<Tags> Tags { get; set; }

        public void LoadCluster(long clusterPositionInSegment)
        {
            //클러스터의 위치로 스트림 이동
            this.Stream.Seek(this.Offset + clusterPositionInSegment, SeekOrigin.Begin);
            //System.Diagnostics.Debug.WriteLine(this.Offset + clusterPositionInSegment);
            //클러스터 조회
            Cluster cluster = null;
            this.TryParse<Cluster>(this.Stream, out cluster, true);
            Cluster = cluster;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.SeekHead:
                    Stream.Seek((long)size, SeekOrigin.Current);
                    break;
                case ElementID.Info:
                    {
                        var child = new Info
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        Info.Add(child);
                    }
                    break;
                case ElementID.Cues:
                    {
                        Cues = new Cues
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        Cues.LoadChildren();
                    }
                    break;
                case ElementID.Tracks:
                    {
                        var child = new Tracks
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        Tracks.Add(child);
                    }
                    break;
                case ElementID.Cluster:
                    if (Cluster == null)
                    {
                        Cluster = new Cluster()
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size,
                            Parent = this
                        };
                    }
                    Stream.Seek((long)size, SeekOrigin.Current);
                    break;
                case ElementID.Attachments:
                    {
                        Attachments = new Attachments
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        Attachments.LoadChildren();
                    }
                    break;
                case ElementID.Chapters:
                    {
                        Chapters = new Chapters
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        Chapters.LoadChildren();
                    }
                    break;
                case ElementID.Tags:
                    {
                        var child = new Tags
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        Tags.Add(child);
                    }
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            //throw new NotImplementedException();
        }

        public override ulong[] MasterElementIDs
        {
            get 
            { 
                return new ulong[] 
                { 
                    ElementID.SeekHead, 
                    ElementID.Info, 
                    ElementID.Cues, 
                    ElementID.Tracks, 
                    ElementID.Cluster,
                    ElementID.Attachments,
                    ElementID.Chapters,
                    ElementID.Tags
                }; 
            }
        }
    }
}
