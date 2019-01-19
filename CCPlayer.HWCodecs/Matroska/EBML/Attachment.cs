using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Attachments : MasterElement
    {
        public List<AttachedFile> AttachedFile { get; set; }
        public Attachments() : base()
        {
            AttachedFile = new List<AttachedFile>();
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.AttachedFile:
                    var child = new AttachedFile
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    AttachedFile.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.Attachments; }
        }

        public override ulong[] MasterElementIDs
        {
            get 
            { 
                return new ulong[] 
                { 
                    ElementID.AttachedFile 
                }; 
            }
        }
    }

    public class AttachedFile : MasterElement
    {
        public string FileDescription { get; set; }
        public string FileName { get; set; }
        public string FileMimeType { get; set; }
        public byte[] FileData { get; set; }
        public ulong FileUID { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.FileDescription:
                    FileDescription = ToString(data);
                    break;
                case ElementID.FileName:
                    FileName = ToString(data);
                    break;
                case ElementID.FileMimeType:
                    FileMimeType = ToString(data);
                    break;
                case ElementID.FileData:
                    FileData = data;
                    break;
                case ElementID.FileUID:
                    FileUID = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.AttachedFile; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
