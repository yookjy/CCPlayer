using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Cluster : MasterElement, IEnumerator<Cluster>
    {
        public ulong PositionInSegment { get; set; }
        public ulong Timecode { get; set; }
        public SilentTracks SilentTracks { get; set; }
        public ulong Position { get; set; }
        public ulong PrevSize { get; set; }
        //BlockGroup 및 SimpleBlock을 모두 Blocks에 저장 (순서가 필요하기 때문)
        private List<Block> Blocks { get; set; }

        private IEnumerator<Block> _Block;
        public IEnumerator<Block> Block
        {
            get
            {
                if (_Block == null)
                {
                    _Block = Blocks.GetEnumerator();
                }
                return _Block;
            }
        }

        public Cluster()
            : base()
        {
            Blocks = new List<Block>();
        }
        
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.SilentTracks:
                    SilentTracks = new SilentTracks
                    {
                        Stream = this.Stream,
                        Offset = this.Stream.Position,
                        Size = size
                    };
                    SilentTracks.LoadChildren();
                    break;
                case ElementID.SimpleBlock:
                    Blocks.Add(new SimpleBlock
                    {
                        Stream = this.Stream,
                        Offset = this.Stream.Position,
                        Size = size
                    });
                    Stream.Seek((long)size, System.IO.SeekOrigin.Current);
                    break;
                case ElementID.BlockGroup:
                    Blocks.Add(new BlockGroup
                    {
                        Stream = this.Stream,
                        Offset = this.Stream.Position,
                        Size = size
                    });
                    Stream.Seek((long)size, System.IO.SeekOrigin.Current);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch(id)
            {
                case ElementID.Timecode:
                    Timecode = ToULong(data);
                    break;
                case ElementID.Position:
                    Position = ToULong(data);
                    break;
                case ElementID.PrevSize:
                    PrevSize = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.Cluster; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.SilentTracks,
                    ElementID.BlockGroup,
                    ElementID.SimpleBlock
                };
            }
        }

        public Cluster Current
        {
            get
            {
                return (Parent as Segment).Cluster;
            }
        }

        object System.Collections.IEnumerator.Current
        {
            get
            {
                return (Parent as Segment).Cluster;
            }
        }

        public bool MoveNext()
        {
            Cluster cluster = null;
            //다음 클러스터를 읽기 위해 스트림 포지션 이동
            Stream.Seek(this.Offset + (long)this.Size, System.IO.SeekOrigin.Begin);
            //System.Diagnostics.Debug.WriteLine(this.Offset + (long)this.Size);
            //다음 클러스터 조회

            Segment segment = Parent as Segment;
            if (segment.TryParse<Cluster>(this.Stream, out cluster, true))
            {
                segment.Cluster = cluster;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            Segment segment = Parent as Segment;
            segment.LoadCluster((long)segment.Cues.CuePoint.Min(cp => cp.CueTrackPositions.Min(ctp => ctp.CueClusterPosition)));
        }

        public void Dispose()
        {
            Block.Dispose();
            Blocks = null;
        }

        public void MoveBlockPointer(int blockIndex)
        {
            var keyFrameBlock = Blocks.Where((x, index) => index >= blockIndex).FirstOrDefault();
            //이동한 첫 블록은 키프레임으로 강제 인식시킴 (간혹 키프레임이 없는 동영상들 때문)
            if (!keyFrameBlock.IsKeyFrame)
            {
                keyFrameBlock.IsKeyFrame = true;
            }

            _Block = Blocks.Where((x, index) => index >= blockIndex).GetEnumerator();
        }
    }

    public class SilentTracks : MasterElement
    {
        public List<ulong> SilentTrackNumber { get; set; }
        public SilentTracks() : base()
        {
            SilentTrackNumber = new List<ulong>();
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch(id)
            {
                case ElementID.SilentTrackNumber:
                    SilentTrackNumber.Add(ToULong(data));
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.SilentTracks; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class SimpleBlock : Block
    {
        public SimpleBlock()
            : base()
        {
        }

        public new void LoadBlockData()
        {
            base.LoadBlockData();
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
        }

        public override ulong ID
        {
            get { return ElementID.SimpleBlock; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public abstract class Block : MasterElement
    {
        protected void LoadBlockData()
        {
            //데이터 처리
            ulong len = 0;
            Stream.Position = Offset;
            TrackNumber = Element.GetVintSize(Stream, ref len);
            byte[] buffer = new byte[3];
            Stream.Read(buffer, 0, buffer.Length);

            Timecode = BitConverter.ToInt16(new[] { buffer[1], buffer[0] }, 0);
            // Console.WriteLine("TrackID:={0},BlockTimeCode:={1}", tid, tc);
            byte flag = buffer[2];
            Invisible = (flag & 0x08) > 0;
            LacingType = flag & 0x06;
            IsKeyFrame = (flag & 0x80) > 0;
            Discardable = (flag & 0x01) > 0;
            FramePosition = len + (ulong)buffer.Length;
            FrameTotalSize = Size - (FramePosition);
            Stream.Seek((long)FrameTotalSize, System.IO.SeekOrigin.Current);
        }
        public ulong TrackNumber { get; set; }
        public short Timecode { get; set; }
        public byte Flags { get; set; }
        public byte[] Data { get; set; }
        public int LacingType { get; set; }
        //No frame after this frame can reference any frame before this frame and vice versa 
        //(in AVC-words: this frame is an IDR frame). 
        //The frame itself doesn't reference any other frames.
        public bool IsKeyFrame { get; set; }
        //duration of this block is 0
        public bool Invisible { get; set; }
        public bool Discardable { get; set; }

        protected ulong FramePosition, FrameTotalSize;

        protected byte[] GetXiphBytes(byte[] source, int Offset, int Length)
        {
            int l = 0;
            for (int i = Offset; i < Length; i++)
            {
                l++;
                if (source[i] != 0xff)
                {
                    break;
                }
            }
            byte[] b = new byte[l];
            Buffer.BlockCopy(source, Offset, b, 0, l);
            return b;
        }

        public IList<byte[]> GetBlockData(byte[] ContentCompression)
        {
            int cc = 0;
            if (ContentCompression != null)
            {
                cc = ContentCompression.Length;
            }
            byte[] b = new byte[cc + (int)FrameTotalSize];
            if (ContentCompression != null)
            {
                Buffer.BlockCopy(ContentCompression, 0, b, 0, cc);
            }
            int i = 0;
            lock (Stream)
            {
                Stream.Position = Offset + (long)FramePosition;
                i = Stream.Read(b, cc, (int)FrameTotalSize);
            }
            i += cc;
            int i2 = 0;
            List<byte[]> Result = new List<byte[]>();
            int framecount = 1;
            long lastsize = 0, totalsize = 0;
            long[] framesizes = null;
            switch (LacingType)
            {
                case 0x00://No
                    //Console.WriteLine("No Lac");
                    Result.Add(b);

                    break;
                case 0x02://Xiph
                    // Console.WriteLine("Xiph Lac");
                    framecount = b[i2] + 1;
                    framesizes = new long[framecount];
                    i2++;
                    var xtmp1 = GetXiphBytes(b, i2, b.Length);
                    i2 += xtmp1.Length;
                    framesizes[0] = xtmp1.Sum(m => m);
                    lastsize = framesizes[0];
                    totalsize = framesizes[0];
                    for (int vs = 1; vs < framecount - 1; vs++)
                    {
                        var cursizeblock = GetXiphBytes(b, i2, b.Length);
                        var cursize = cursizeblock.Sum(m => m);
                        framesizes[vs] = cursize;
                        i2 += cursizeblock.Length;
                        totalsize += cursize;
                    }
                    framesizes[framecount - 1] = b.Length - i2 - totalsize;
                    foreach (var s in framesizes)
                    {
                        byte[] tmp2 = new byte[s];
                        Buffer.BlockCopy(b, i2, tmp2, 0, tmp2.Length);
                        Result.Add(tmp2);
                        i2 += (int)s;
                    }
                    break;
                case 0x04://Fixed
                    // Console.WriteLine("Fixed Lac");
                    framecount = b[i2] + 1;
                    //framesizes = new long[framecount];
                    int fs = 0;
                    i2++;
                    fs = (i - i2) / framecount;
                    for (int i3 = i2; i3 < i; i3 += fs)
                    {
                        byte[] tmp2 = new byte[fs];
                        Buffer.BlockCopy(b, i3, tmp2, 0, fs);
                        Result.Add(tmp2);
                    }
                    break;
                case 0x06://EBML

                    // Console.WriteLine("EBML Lac");
                    framecount = b[i2] + 1;
                    framesizes = new long[framecount];
                    i2++;
                    var tmp1 = Element.GetVintSize(b, i2, b.Length);
                    i2 += tmp1.Length;
                    framesizes[0] = (long)Element.ConvertVintToULong(tmp1);
                    lastsize = framesizes[0];
                    totalsize = framesizes[0];
                    for (int vs = 1; vs < framecount - 1; vs++)
                    {
                        var cursizeblock = Element.GetVintSize(b, i2, b.Length);
                        var cursize = Element.ConvertVintToLong(cursizeblock) + lastsize;
                        framesizes[vs] = cursize;
                        i2 += cursizeblock.Length;
                        lastsize = cursize;
                        totalsize += cursize;
                    }
                    framesizes[framecount - 1] = b.Length - i2 - totalsize;
                    foreach (var s in framesizes)
                    {
                        byte[] tmp2 = new byte[s];
                        Buffer.BlockCopy(b, i2, tmp2, 0, tmp2.Length);
                        Result.Add(tmp2);
                        i2 += (int)s;
                    }
                    break;
                default:
                    break;
            }
            return Result;
        }

        public Block GetBlock(out BlockGroup blockGroup)
        {
            Block block = null;
            if (this is BlockGroup)
            {
                blockGroup = this as BlockGroup;
                //자식 로드
                blockGroup.LoadChildren();
                block = blockGroup.Block;

                //키프레임 오버라이딩
                if (!block.IsKeyFrame && blockGroup.IsKeyFrame)
                {
                    block.IsKeyFrame = blockGroup.IsKeyFrame;
                }
            }
            else// if (this is SimpleBlock)
            {
                blockGroup = null;
                (this as SimpleBlock).LoadBlockData();
                block = this;
            }
            return block;
        }
    }

    public class BlockGroup : Block
    {
        public BlockAdditions BlockAdditions { get; set; }
        public ulong BlockDuration { get; set; }
        public ulong ReferencePriority { get; set; }
        public List<long> ReferenceBlock { get; set; }
        public byte[] CodecState { get; set; }
        public long DiscardPadding { get; set; }
        public Slices Slices { get; set; }
        public SimpleBlock Block { get; set; }
        public BlockGroup() : base()
        {
            ReferenceBlock = new List<long>();
        }

        protected override void SetDefaultValues()
        {
            ReferencePriority = 0;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.BlockAdditions:
                    BlockAdditions = new BlockAdditions
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    BlockAdditions.LoadChildren();
                    break;
                case ElementID.Slices:
                    Slices = new Slices
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    Slices.LoadChildren();
                    break;
                case ElementID.Block:
                    Block = new SimpleBlock
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    Block.LoadBlockData();
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch(id)
            {
                case ElementID.BlockDuration:
                    BlockDuration = ToULong(data);
                    break;
                case ElementID.ReferencePriority:
                    ReferencePriority = ToULong(data);
                    break;
                case ElementID.ReferenceBlock:
                    ReferenceBlock.Add(ToLong(data));
                    break;
                case ElementID.CodecState:
                    CodecState = data;
                    break;
                case ElementID.DiscardPadding:
                    DiscardPadding = ToLong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.BlockGroup; }
        }

        public override ulong[] MasterElementIDs
        {
            get 
            { 
                return new ulong[] 
                { 
                    ElementID.BlockAdditions,
                    ElementID.Slices,
                    ElementID.Block
                };
            }
        }
    }

    public class BlockAdditions : MasterElement
    {
        public List<BlockMore> BlockMore { get; set; }

        public BlockAdditions() : base()
        {
            BlockMore = new List<BlockMore>();
        }
        
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.BlockAdditions; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.BlockMore }; }
        }
    }
    
    public class BlockMore : MasterElement
    {
        public ulong BlockAddID { get; set; }
        public byte[] BlockAdditional { get; set; }

        protected override void SetDefaultValues()
        {
            BlockAddID = 1;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch(id)
            {
                case ElementID.BlockAddID:
                    BlockAddID = ToULong(data);
                    break;
                case ElementID.BlockAdditional:
                    BlockAdditional = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.BlockMore; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class Slices : MasterElement
    {
        public List<TimeSlice> TimeSlice { get; set; }
        public Slices() : base()
        {
            TimeSlice = new List<TimeSlice>();
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch(id)
            {
                case ElementID.TimeSlice:
                    var child = new TimeSlice
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    TimeSlice.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
        }

        public override ulong ID
        {
            get { return ElementID.Slices; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.TimeSlice }; }
        }

    }
    public class TimeSlice : MasterElement
    {
        public ulong LaceNumber { get; set; }

        protected override void SetDefaultValues()
        {
            LaceNumber = 0;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.LaceNumber:
                    LaceNumber = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.TimeSlice; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    
}
