using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Tags : MasterElement
    {
        public List<Tag> Tag { get; set; }

        public Tags()
            : base()
        {
            Tag = new List<Tag>();
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.Tag:
                    var child = new Tag
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    Tag.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.Tags; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.Tag
                };
            }
        }
    }

    public class Tag : MasterElement
    {
        public Targets Targets { get; set; }
        public List<SimpleTag> SimpleTag { get; set; }

        public Tag()
            : base()
        {
            SimpleTag = new List<SimpleTag>();
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.Targets:
                    Targets = new Targets
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    Targets.LoadChildren();
                    break;
                case ElementID.SimpleTag:
                    var child = new SimpleTag
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    SimpleTag.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.Tag; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.Targets,
                    ElementID.SimpleTag
                };
            }
        }
    }

    public class Targets : MasterElement
    {
        protected override void SetDefaultValues()
        {
            TargetTypeValue = 50;
            TagTrackUID = 0;
            TagEditionUID = 0;
            TagChapterUID = 0;
            TagAttachmentUID = 0;
        }

        public ulong TargetTypeValue { get; set; }
        public string TargetType { get; set; }
        public ulong TagTrackUID { get; set; }
        public ulong TagEditionUID { get; set; }
        public ulong TagChapterUID { get; set; }
        public ulong TagAttachmentUID { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.TargetTypeValue:
                    TargetTypeValue = ToULong(data);
                    break;
                case ElementID.TargetType:
                    TargetType = ToString(data);
                    break;
                case ElementID.TagTrackUID:
                    TagTrackUID = ToULong(data);
                    break;
                case ElementID.TagEditionUID:
                    TagEditionUID = ToULong(data);
                    break;
                case ElementID.TagChapterUID:
                    TagChapterUID = ToULong(data);
                    break;
                case ElementID.TagAttachmentUID:
                    TagAttachmentUID = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.Targets; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class SimpleTag : MasterElement
    {
        public string TagName { get; set; }
        public string TagLanguage { get; set; }
        public ulong TagDefault { get; set; }
        public string TagString { get; set; }
        public byte[] TagBinary { get; set; }

        protected override void SetDefaultValues()
        {
            TagLanguage = "und";
            TagDefault = 1;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch(id)
            {
                case ElementID.TagName:
                    TagName = ToString(data);
                    break;
                case ElementID.TagLanguage:
                    TagLanguage = ToString(data);
                    break;
                case ElementID.TagDefault:
                    TagDefault = ToULong(data);
                    break;
                case ElementID.TagString:
                    TagString = ToString(data);
                    break;
                case ElementID.TagBinary:
                    TagBinary = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.SimpleTag; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
