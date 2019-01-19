using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Info : MasterElement
    {
        public byte[] SegmentUID { get; set; }
        public string SegmentFilename  { get; set; }
        public byte[] PrevUID  { get; set; }
        public  string PrevFilename  { get; set; }
        public byte[] NextUID  { get; set; }
        public  string NextFilename  { get; set; }
        public List<byte[]> SegmentFamily  { get; set; }
        public List<ChapterTranslate> ChapterTranslate { get; set; }
        public ulong TimecodeScale  { get; set; }
        public double Duration { get; set; }
        public DateTime DateUTC  { get; set; }
        public string Title  { get; set; }
        public string MuxingApp  { get; set; }
        public string WritingApp  { get; set; }

        public Info() : base()
        {
            ChapterTranslate = new List<ChapterTranslate>();
            SegmentFamily = new List<byte[]>();
        }

        public override ulong ID
        {
            get { return ElementID.Info; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ChapterTranslate:
                    var child = new ChapterTranslate
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    ChapterTranslate.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.SegmentUID:
                    SegmentUID = data;
                    break;
                case ElementID.SegmentFilename:
                    SegmentFilename = ToUtf8String(data);
                    break;
                case ElementID.PrevUID:
                    PrevUID = data;
                    break;
                case ElementID.PrevFilename:
                    PrevFilename = ToUtf8String(data);
                    break;
                case ElementID.NextUID:
                    NextUID = data;
                    break;
                case ElementID.NextFilename:
                    NextFilename = ToUtf8String(data);
                    break;
                case ElementID.SegmentFamily:
                    SegmentFamily.Add(data);
                    break;
                case ElementID.TimecodeScale:
                    TimecodeScale = ToULong(data);
                    break;
                case ElementID.Duration:
                    Duration = ToDouble(data);
                    break;
                case ElementID.DateUTC:
                    DateUTC = ToDateTime(data);
                    break;
                case ElementID.Title:
                    Title = ToUtf8String(data);
                    break;
                case ElementID.MuxingApp:
                    MuxingApp = ToUtf8String(data); ;
                    break;
                case ElementID.WritingApp:
                    WritingApp = ToUtf8String(data); ;
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.ChapterTranslate }; }
        }
        protected override void SetDefaultValues()
        {
            TimecodeScale = 1000000;
        }
    }

    public class ChapterTranslate : MasterElement 
    {
        public List<ulong> ChapterTranslateEditionUID  { get; set; }
        public ulong ChapterTranslateCodec  { get; set; }
        public byte[] ChapterTranslateID  { get; set; }

        public ChapterTranslate() : base()
        {
            ChapterTranslateEditionUID = new List<ulong>();
        }

        public override ulong ID
        {
            get { return ElementID.ChapterTranslate; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapterTranslateEditionUID:
                    ChapterTranslateEditionUID.Add(ToULong(data));
                    break;
                case ElementID.ChapterTranslateCodec:
                    ChapterTranslateCodec = ToULong(data);
                    break;
                case ElementID.ChapterTranslateID:
                    ChapterTranslateID = data;
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
