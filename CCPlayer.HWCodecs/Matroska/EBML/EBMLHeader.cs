using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class EBML : MasterElement
    {
        public EBML() : base() 
        {
        }

        public ulong EBMLVersion { get; set; }

        public ulong EBMLReadVersion { get; set; }

        public ulong EBMLMaxIDLength { get; set; }

        public ulong EBMLMaxSizeLength { get; set; }

        public string DocType { get; set; }

        public ulong DocTypeVersion { get; set; }

        public ulong DocTypeReadVersion { get; set; }
        
        public override ulong ID
        {
            get { return ElementID.EBML; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.EBMLVersion:
                    EBMLVersion = ToULong(data);
                    break;
                case ElementID.EBMLReadVersion:
                    EBMLReadVersion = ToULong(data);
                    break;
                case ElementID.EBMLMaxIDLength:
                    EBMLMaxIDLength = ToULong(data);
                    break;
                case ElementID.EBMLMaxSizeLength:
                    EBMLMaxSizeLength = ToULong(data);
                    break;
                case ElementID.DocType:
                    DocType = ToString(data);
                    break;
                case ElementID.DocTypeVersion:
                    DocTypeVersion = ToULong(data);
                    break;
                case ElementID.DocTypeReadVersion:
                    DocTypeReadVersion = ToULong(data);
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }

        protected override void SetDefaultValues()
        {
            EBMLVersion = 1;
            EBMLReadVersion = 1;
            EBMLMaxIDLength = 4;
            EBMLMaxSizeLength = 8;
            DocType = "matroska";
            DocTypeVersion = 1;
            DocTypeReadVersion = 1;
        }
    }
}
