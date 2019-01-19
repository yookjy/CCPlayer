using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Cues : MasterElement
    {
        public Cues() : base () 
        {
            CuePoint = new List<CuePoint>();
        }
        public List<CuePoint> CuePoint { get; set; }

        public override ulong ID
        {
            get { return ElementID.Cues; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.CuePoint:
                    var child = new CuePoint
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    CuePoint.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.CuePoint }; }
        }
    }

    public class CuePoint : MasterElement
    {
        public CuePoint() : base()
        {
            CueTrackPositions = new List<CueTrackPositions>();
        }
        public ulong CueTime { get; set; }

        public List<CueTrackPositions> CueTrackPositions { get; set; }

        public override ulong ID
        {
            get { return ElementID.CuePoint; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.CueTrackPositions:
                    var child = new CueTrackPositions
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    CueTrackPositions.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.CueTime:
                    CueTime = ToULong(data);
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.CueTrackPositions }; }
        }
    }

    public class CueTrackPositions : MasterElement
    {
        public CueTrackPositions() : base()
        {
            CueReference = new List<CueReference>();
        }
        public ulong CueTrack { get; set; }
        public ulong CueClusterPosition { get; set; }
        
        #region version 4 에서 생김.
        public ulong CueRelativePosition { get; set; } 
        public ulong CueDuration { get; set; }
        /// <summary>
        #endregion
        /// </summary>
        public ulong CueBlockNumber { get; set; }
        
        #region version 1은 지원 안함.
        public ulong CueCodecState { get; set; }
        public List<CueReference> CueReference { get; set; }
        
        #endregion
        protected override void SetDefaultValues()
        {
            CueBlockNumber = 1;
            CueCodecState = 0;
        }

        public override ulong ID
        {
            get { return ElementID.CueTrackPositions; }
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.CueReference:
                    {
                        var child = new CueReference
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        CueReference.Add(child);
                    }
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.CueTrack:
                    CueTrack = ToULong(data);
                    break;
                case ElementID.CueClusterPosition:
                    CueClusterPosition = ToULong(data);
                    break;
                case ElementID.CueRelativePosition:
                    CueRelativePosition = ToULong(data);
                    break;
                case ElementID.CueDuration:
                    CueDuration = ToULong(data);
                    break;
                case ElementID.CueBlockNumber:
                    CueBlockNumber = ToULong(data);
                    break;
                case ElementID.CueCodecState:
                    CueCodecState = ToULong(data);
                    break;
            }
        }

        public override ulong[] MasterElementIDs
        {
            get { return new ulong[] { ElementID.CueReference }; }
        }
    }

    public class CueReference : MasterElement
    {
        public ulong CueRefTime { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.CueRefTime:
                    CueRefTime = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.CueReference; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
