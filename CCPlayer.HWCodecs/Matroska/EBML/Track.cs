using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public enum TrackTypes
    {
        None = 0,
        Video = 1,
        Audio = 2,
        Complex = 3,
        Logo = 0x10,
        Subtitle = 0x11,
        Button = 0x12,
        Control = 0x20
    }

    public class Tracks : MasterElement
    {
        public Tracks() : base()
        {
            TrackEntry = new List<TrackEntry>();
        }

        public List<TrackEntry> TrackEntry { get; set; }

        public override ulong ID
        {
            get { return ElementID.Tracks; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.TrackEntry:
                    var child = new TrackEntry
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    TrackEntry.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.TrackEntry }; }
        }
    }

    public class TrackEntry : MasterElement
    {
        public TrackEntry()
            : base()
        {
            TrackTranslate = new List<TrackTranslate>();
        }

        protected override void SetDefaultValues()
        {
            FlagEnabled = 1;
            FlagDefault = 1;
            FlagForced = 0;
            FlagLacing = 1;
            MinCache = 0;
            MaxBlockAdditionID = 0;
            Language = "eng";
            CodecDecodeAll = 1;
            CodecDelay = 0;
            SeekPreRoll = 0;
        }

        public ulong TrackNumber { get; set; }
        public ulong TrackUID { get; set; }
        public TrackTypes TrackType { get; set; }
        public ulong FlagEnabled { get; set; }
        public ulong FlagDefault { get; set; }
        public ulong FlagForced { get; set; }
        public ulong FlagLacing { get; set; }
        public ulong MinCache { get; set; }
        public ulong MaxCache { get; set; }
        //DefaultDuration 는 나노초이며, TimecodeScale에 기반하지 않는다. 
        public ulong DefaultDuration { get; set; }
        public ulong DefaultDecodedFieldDuration { get; set; }
        public ulong MaxBlockAdditionID { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public string CodecID { get; set; }
        public byte[] CodecPrivate { get; set; }
        public string CodecName { get; set; }
        public ulong AttachmentLink { get; set; }
        public ulong CodecDecodeAll { get; set; }
        public ulong TrackOverlay { get; set; }
        public ulong CodecDelay { get; set; }
        public ulong SeekPreRoll { get; set; }
        public List<TrackTranslate> TrackTranslate { get; set; }
        public Video Video { get; set; }
        public Audio Audio { get; set; }
        public TrackOperation TrackOperation { get; set; }
        public ContentEncodings ContentEncodings { get; set; }

        public override ulong ID
        {
            get { return ElementID.TrackEntry; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch(id)
            {
                case ElementID.TrackTranslate:
                    {
                        var child = new TrackTranslate
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        TrackTranslate.Add(child);
                    }
                    break;
                case ElementID.Video:
                    Video = new Video
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    Video.LoadChildren();
                    break;
                case ElementID.Audio:
                    Audio = new Audio
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    Audio.LoadChildren();
                    break;
                case ElementID.TrackOperation:
                    TrackOperation = new TrackOperation
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    TrackOperation.LoadChildren();
                    break;
                case ElementID.ContentEncodings:
                    ContentEncodings = new ContentEncodings
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    ContentEncodings.LoadChildren();
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.TrackNumber:
                    TrackNumber = ToULong(data);
                    break;
                case ElementID.TrackUID:
                    TrackUID = ToULong(data);
                    break;
                case ElementID.TrackType:
                    TrackType = (TrackTypes)ToULong(data);
                    break;
                case ElementID.FlagEnabled:
                    FlagEnabled = ToULong(data);
                    break;
                case ElementID.FlagDefault:
                    FlagDefault = ToULong(data);
                    break;
                case ElementID.FlagForced:
                    FlagForced = ToULong(data);
                    break;
                case ElementID.FlagLacing:
                    FlagLacing = ToULong(data);
                    break;
                case ElementID.MinCache:
                    MinCache = ToULong(data);
                    break;
                case ElementID.MaxCache:
                    MaxCache = ToULong(data);
                    break;
                case ElementID.DefaultDuration:
                    DefaultDuration = ToULong(data);
                    break;
                case ElementID.DefaultDecodedFieldDuration:
                    DefaultDecodedFieldDuration = ToULong(data);
                    break;
                case ElementID.MaxBlockAdditionID:
                    MaxBlockAdditionID = ToULong(data);
                    break;
                case ElementID.Name:
                    Name = ToUtf8String(data);
                    break;
                case ElementID.Language:
                    Language = ToString(data);
                    break;
                case ElementID.CodecID:
                    CodecID = ToString(data);
                    break;
                case ElementID.CodecPrivate:
                    CodecPrivate = data;
                    break;
                case ElementID.CodecName:
                    CodecName = ToUtf8String(data);
                    break;
                case ElementID.AttachmentLink:
                    AttachmentLink = ToULong(data);
                    break;
                case ElementID.CodecDecodeAll:
                    CodecDecodeAll = ToULong(data);
                    break;
                case ElementID.TrackOverlay:
                    TrackOverlay = ToULong(data);
                    break;
                case ElementID.CodecDelay:
                    CodecDelay = ToULong(data);
                    break;
                case ElementID.SeekPreRoll:
                    SeekPreRoll = ToULong(data);
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.TrackTranslate, ElementID.Video, ElementID.Audio, ElementID.TrackOperation, ElementID.ContentEncodings }; }
        }
    }

    public class TrackTranslate: MasterElement {

        public ulong TrackTranslateEditionUID { get; set; }
        public ulong TrackTranslateCodec { get; set; }
        public byte[] TrackTranslateTrackID { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.TrackTranslateEditionUID:
                    TrackTranslateEditionUID = ToULong(data);
                    break;
                case ElementID.TrackTranslateCodec:
                    TrackTranslateCodec = ToULong(data);
                    break;
                case ElementID.TrackTranslateTrackID:
                    TrackTranslateTrackID = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.TrackTranslate; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class Video : MasterElement
    {
        public ulong FlagInterlaced { get; set; }
        public ulong StereoMode { get; set; }
        public ulong AlphaMode { get; set; }
        public ulong PixelWidth { get; set; }
        public ulong PixelHeight { get; set; }
        public ulong PixelCropBottom { get; set; }
        public ulong PixelCropTop { get; set; }
        public ulong PixelCropLeft { get; set; }
        public ulong PixelCropRight { get; set; }
        public ulong DisplayWidth { get; set; }
        public ulong DisplayHeight { get; set; }
        public ulong DisplayUnit { get; set; }
        public ulong AspectRatioType { get; set; }
        public byte[] ColourSpace { get; set; }
        public double GammaValue { get; set; }
        public double FrameRate { get; set; }

        protected override void SetDefaultValues()
        {
            FlagInterlaced = 0;
            StereoMode = 0;
            AlphaMode = 0;
            PixelCropBottom = 0;
            PixelCropTop = 0;
            PixelCropLeft = 0;
            PixelCropRight = 0;
            DisplayUnit = 0;
            AspectRatioType = 0;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.FlagInterlaced:
                    FlagInterlaced = ToULong(data);
                    break;
                case ElementID.StereoMode:
                    StereoMode = ToULong(data);
                    break;
                case ElementID.AlphaMode:
                    AlphaMode = ToULong(data);
                    break;
                case ElementID.PixelWidth:
                    PixelWidth = ToULong(data);
                    if (DisplayUnit == 0 && DisplayWidth == 0)
                    {
                        DisplayWidth = PixelWidth;
                    }
                    break;
                case ElementID.PixelHeight:
                    PixelHeight = ToULong(data);
                    if (DisplayUnit == 0 && DisplayHeight == 0)
                    {
                        DisplayHeight = PixelHeight;
                    }
                    break;
                case ElementID.PixelCropBottom:
                    PixelCropBottom = ToULong(data);
                    break;
                case ElementID.PixelCropTop:
                    PixelCropTop = ToULong(data);
                    break;
                case ElementID.PixelCropLeft:
                    PixelCropLeft = ToULong(data);
                    break;
                case ElementID.PixelCropRight:
                    PixelCropRight = ToULong(data);
                    break;
                case ElementID.DisplayWidth:
                    DisplayWidth = ToULong(data);
                    break;
                case ElementID.DisplayHeight:
                    DisplayHeight = ToULong(data);
                    break;
                case ElementID.DisplayUnit:
                    DisplayUnit = ToULong(data);
                    break;
                case ElementID.AspectRatioType:
                    AspectRatioType = ToULong(data);
                    break;
                case ElementID.ColourSpace:
                    ColourSpace = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.Video; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class Audio : MasterElement
    {
        public double SamplingFrequency { get; set; }
        public double OutputSamplingFrequency { get; set; }
        public ulong Channels { get; set; }
        public ulong BitDepth { get; set; }

        protected override void SetDefaultValues()
        {
            SamplingFrequency = 8000.0;
            Channels = 1;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.SamplingFrequency:
                    SamplingFrequency = ToDouble(data);
                    if (OutputSamplingFrequency == 0)
                    {
                        OutputSamplingFrequency = SamplingFrequency;
                    }
                    break;
                case ElementID.OutputSamplingFrequency:
                    OutputSamplingFrequency = ToDouble(data);
                    break;
                case ElementID.Channels:
                    Channels = ToULong(data);
                    break;
                case ElementID.BitDepth:
                    BitDepth = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.Audio; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class TrackOperation : MasterElement
    {
        public TrackOperation() : base()
        {
        }
        public TrackCombinePlanes TrackCombinePlanes { get; set; }
        public TrackJoinBlocks TrackJoinBlocks { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.TrackCombinePlanes:
                    TrackCombinePlanes = new TrackCombinePlanes
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    TrackCombinePlanes.LoadChildren();
                    break;
                case ElementID.TrackJoinBlocks:
                    TrackJoinBlocks = new TrackJoinBlocks
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    TrackJoinBlocks.LoadChildren();
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.TrackOperation; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.TrackCombinePlanes, ElementID.TrackJoinBlocks }; }
        }
    }

    public class TrackCombinePlanes : MasterElement
    {
        public List<TrackPlane> TrackPlane { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.TrackPlane:
                    {
                        var child = new TrackPlane
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        TrackPlane.Add(child);
                    }
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.TrackCombinePlanes; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.TrackPlane }; }
        }
    }

    public class TrackPlane : MasterElement
    {
        public ulong TrackPlaneUID { get; set; }
        public ulong TrackPlaneType { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.TrackPlaneUID:
                    TrackPlaneUID = ToULong(data);
                    break;
                case ElementID.TrackPlaneType:
                    TrackPlaneType = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.TrackPlane; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class TrackJoinBlocks : MasterElement
    {
        public ulong TrackJoinUID { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.TrackJoinUID:
                    TrackJoinUID = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.TrackJoinBlocks; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class ContentEncodings : MasterElement
    {
        public ContentEncodings() : base()
        {
            ContentEncoding = new List<ContentEncoding>();
        }

        public List<ContentEncoding> ContentEncoding { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ContentEncoding:
                    {
                        var child = new ContentEncoding
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        ContentEncoding.Add(child);
                    }
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.ContentEncodings; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.ContentEncoding }; }
        }
    }

    public class ContentEncoding : MasterElement
    {
        public ContentEncoding()
            : base()
        {
        }
        protected override void SetDefaultValues()
        {
            ContentEncodingOrder = 0;
            ContentEncodingScope = 1;
            ContentEncodingType = 0;
        }

        public ulong ContentEncodingOrder { get; set; }
        public ulong ContentEncodingScope { get; set; }
        public ulong ContentEncodingType { get; set; }
        public ContentCompression ContentCompression { get; set; }
        public ContentEncryption ContentEncryption { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ContentCompression:
                    ContentCompression = new ContentCompression
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    ContentCompression.LoadChildren();
                    break;
                case ElementID.ContentEncryption:
                    ContentEncryption = new ContentEncryption
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    ContentEncryption.LoadChildren();
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ContentEncodingOrder:
                    ContentEncodingOrder = ToULong(data);
                    break;
                case ElementID.ContentEncodingScope:
                    ContentEncodingScope = ToULong(data);
                    break;
                case ElementID.ContentEncodingType:
                    ContentEncodingType = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ContentEncoding; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.ContentCompression, ElementID.ContentEncryption }; }
        }
    }

    public class ContentCompression : MasterElement
    {
        public ContentCompression()
            : base()
        {
        }
        protected override void SetDefaultValues()
        {
            ContentCompAlgo = 0;
        }

        public ulong ContentCompAlgo { get; set; }
        public byte[] ContentCompSettings { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ContentCompAlgo:
                    ContentCompAlgo = ToULong(data);
                    break;
                case ElementID.ContentCompSettings:
                    ContentCompSettings = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ContentCompression; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class ContentEncryption : MasterElement
    {
        public ulong ContentEncAlgo { get; set; }
        public byte[] ContentEncKeyID { get; set; }
        public byte[] ContentSignature { get; set; }
        public byte[] ContentSigKeyID { get; set; }
        public ulong ContentSigAlgo { get; set; }
        public ulong ContentSigHashAlgo { get; set; }
        protected override void SetDefaultValues()
        {
            ContentEncAlgo = 0;
            ContentSigAlgo = 0;
            ContentSigHashAlgo = 0;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ContentEncAlgo:
                    ContentEncAlgo = ToULong(data);
                    break;
                case ElementID.ContentEncKeyID:
                    ContentEncKeyID = data;
                    break;
                case ElementID.ContentSignature:
                    ContentSignature = data;
                    break;
                case ElementID.ContentSigKeyID:
                    ContentSigKeyID = data;
                    break;
                case ElementID.ContentSigAlgo:
                    ContentSigAlgo = ToULong(data);
                    break;
                case ElementID.ContentSigHashAlgo:
                    ContentSigHashAlgo = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ContentEncryption; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
