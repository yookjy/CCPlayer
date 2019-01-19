using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class SeekHead  : MasterElement
    {
        public SeekHead() : base() 
        {
            Seek = new List<Seek>();
        }

        public List<Seek> Seek { get; set; }

        public override ulong ID
        {
            get { return ElementID.SeekHead; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.Seek:
                    var child = new Seek
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    Seek.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.Seek }; }
        }
    }

    public class Seek : MasterElement
    {

        public Seek() : base() { }

        public ulong SeekID { get; set; }

        public ulong SeekPosition { get; set; }

        public override ulong ID
        {
            get { return ElementID.Seek; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.SeekID:
                    SeekID = ToULong(data);
                    break;
                case ElementID.SeekPosition:
                    SeekPosition = ToULong(data);
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
