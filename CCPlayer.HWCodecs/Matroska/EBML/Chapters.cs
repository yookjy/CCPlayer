using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Chapters : MasterElement
    {
        public List<EditionEntry> EditionEntry { get; set; }
        public Chapters()
            : base()
        {
            EditionEntry = new List<EditionEntry>();
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.EditionEntry:
                    var child = new EditionEntry
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    EditionEntry.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override ulong ID
        {
            get { return ElementID.Chapters; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.EditionEntry
                };
            }
        }
    }

    public class EditionEntry : MasterElement
    {
        public ulong EditionUID { get; set; }
        public ulong EditionFlagHidden { get; set; }
        public ulong EditionFlagDefault { get; set; }
        public ulong EditionFlagOrdered { get; set; }
        public List<ChapterAtom> ChapterAtom { get; set; }

        public EditionEntry()
            : base()
        {
            ChapterAtom = new List<ChapterAtom>();
        }

        protected override void SetDefaultValues()
        {
            EditionFlagHidden = 0;
            EditionFlagDefault = 0;
            EditionFlagOrdered = 0;
        }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ChapterAtom:
                    var child = new ChapterAtom
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    ChapterAtom.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.EditionUID:
                    EditionUID = ToULong(data);
                    break;
                case ElementID.EditionFlagHidden:
                    EditionFlagHidden = ToULong(data);
                    break;
                case ElementID.EditionFlagDefault:
                    EditionFlagDefault = ToULong(data);
                    break;
                case ElementID.EditionFlagOrdered:
                    EditionFlagOrdered = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.EditionEntry; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.ChapterAtom
                };
            }
        }
    }

    public class ChapterAtom : MasterElement
    {
        public ulong ChapterUID { get; set; }
        public string ChapterStringUID { get; set; }
        public ulong ChapterTimeStart { get; set; }
        public ulong ChapterTimeEnd { get; set; }
        public ulong ChapterFlagHidden { get; set; }
        public ulong ChapterFlagEnabled { get; set; }
        public byte[] ChapterSegmentUID { get; set; }
        public ulong ChapterSegmentEditionUID { get; set; }
        public ulong ChapterPhysicalEquiv { get; set; }
        public ChapterTrack ChapterTrack { get; set; }
        public List<ChapterDisplay> ChapterDisplay { get; set; }
        public List<ChapProcess> ChapProcess { get; set; }
        public ChapterAtom()
            : base()
        {
            ChapterDisplay = new List<ChapterDisplay>();
            ChapProcess = new List<ChapProcess>();
        }

        protected override void SetDefaultValues()
        {
            ChapterFlagHidden = 0;
            ChapterFlagEnabled = 1;
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ChapterTrack:
                    ChapterTrack = new ChapterTrack
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    ChapterTrack.LoadChildren();
                    break;
                case ElementID.ChapterDisplay:
                    {
                        var child = new ChapterDisplay
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        ChapterDisplay.Add(child);
                    }
                    break;
                case ElementID.ChapProcess:
                    {
                        var child = new ChapProcess
                        {
                            Stream = Stream,
                            Offset = Stream.Position,
                            Size = size
                        };
                        child.LoadChildren();
                        ChapProcess.Add(child);
                    }
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapterUID:
                    ChapterUID = ToULong(data);
                    break;
                case ElementID.ChapterStringUID:
                    ChapterStringUID = ToString(data);
                    break;
                case ElementID.ChapterTimeStart:
                    ChapterTimeStart = ToULong(data);
                    break;
                case ElementID.ChapterTimeEnd:
                    ChapterTimeEnd = ToULong(data);
                    break;
                case ElementID.ChapterFlagHidden:
                    ChapterFlagHidden = ToULong(data);
                    break;
                case ElementID.ChapterFlagEnabled:
                    ChapterFlagEnabled = ToULong(data);
                    break;
                case ElementID.ChapterSegmentUID:
                    ChapterSegmentUID = data;
                    break;
                case ElementID.ChapterSegmentEditionUID:
                    ChapterSegmentEditionUID = ToULong(data);
                    break;
                case ElementID.ChapterPhysicalEquiv:
                    ChapterPhysicalEquiv = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ChapterAtom; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.ChapterTrack,
                    ElementID.ChapterDisplay,
                    ElementID.ChapProcess
                };
            }
        }
    }

    public class ChapterTrack : MasterElement
    {
        public ulong ChapterTrackNumber { get; set; }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapterTrackNumber:
                    ChapterTrackNumber = ToULong(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ChapterTrack; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class ChapterDisplay : MasterElement
    {
        public string ChapString { get; set; }
        public string ChapLanguage { get; set; }
        public string ChapCountry { get; set; }

        protected override void SetDefaultValues()
        {
            ChapLanguage = "eng";
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapString:
                    ChapString = ToString(data);
                    break;
                case ElementID.ChapLanguage:
                    ChapLanguage = ToString(data);
                    break;
                case ElementID.ChapCountry:
                    ChapCountry = ToString(data);
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ChapterDisplay; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }

    public class ChapProcess : MasterElement
    {
        public ulong ChapProcessCodecID { get; set; }
        public byte[] ChapProcessPrivate { get; set; }
        public List<ChapProcessCommand> ChapProcessCommand { get; set; }

        public ChapProcess()
            : base()
        {
            ChapProcessCommand = new List<ChapProcessCommand>();
        }

        protected override void SetDefaultValues()
        {
            ChapProcessCodecID = 0;
        }
        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            switch (id)
            {
                case ElementID.ChapProcessCommand:
                    var child = new ChapProcessCommand
                    {
                        Stream = Stream,
                        Offset = Stream.Position,
                        Size = size
                    };
                    child.LoadChildren();
                    ChapProcessCommand.Add(child);
                    break;
            }
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapProcessCodecID:
                    ChapProcessCodecID = ToULong(data);
                    break;
                case ElementID.ChapProcessPrivate:
                    ChapProcessPrivate = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ChapProcess; }
        }

        public override ulong[] MasterElementIDs
        {
            get
            {
                return new ulong[] 
                { 
                    ElementID.ChapProcessCommand
                };
            }
        }
    }

    public class ChapProcessCommand : MasterElement
    {
        public ulong ChapProcessTime { get; set; }
        public byte[] ChapProcessData { get; set; }

        protected override void SetMasterElementValue(ulong id, ulong size)
        {
            throw new NotImplementedException();
        }

        protected override void SetElementValue(ulong id, byte[] data)
        {
            switch (id)
            {
                case ElementID.ChapProcessTime:
                    ChapProcessTime = ToULong(data);
                    break;
                case ElementID.ChapProcessData:
                    ChapProcessData = data;
                    break;
            }
        }

        public override ulong ID
        {
            get { return ElementID.ChapProcessCommand; }
        }

        public override ulong[] MasterElementIDs
        {
            get { return null; }
        }
    }
}
