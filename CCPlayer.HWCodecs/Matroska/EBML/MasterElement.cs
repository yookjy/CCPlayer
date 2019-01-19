using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    
    public abstract class MasterElement : Element
    {
        public MasterElement Parent { get; set; }

        public bool TryParse<T>(Stream stream, List<T> elementList, bool isLoadChildren) where T : new()
        {
            ulong lengId = 0;
            ulong lengSz = 0;
            ulong tSize = 0;

            while (tSize < this.Size)
            {
                var obj = new T();
                var element = obj as MasterElement;
                if (element == null) return false;

                element.Parent = this;
                element.Stream = stream;

                ulong ebmlId = GetVintId(stream, ref lengId);

                switch (ebmlId)
                {
                    case ElementID.CRC_32:
                        //crc 체크
                        //var crc32 = new CRC32();
                        break;
                    case ElementID.Void:
                        //생략
                        break;
                    default:
                        break;
                }

                if (ebmlId != element.ID)
                {
                    stream.Seek((long)lengId * -1, SeekOrigin.Current);
                    return elementList.Count > 0 ? true : false;
                }

                element.Size = GetVintSize(stream, ref lengSz);
                element.Offset = stream.Position;
                elementList.Add(obj);

                if (isLoadChildren)
                {
                    element.LoadChildren();
                }
                else
                {
                    stream.Seek((long)element.Size, SeekOrigin.Current);
                }

                tSize += lengId + lengSz + element.Size;
            }

            return true;
        }

        public bool TryParse<T>(Stream stream, out T element, bool isLoadChildren) where T : new()
        {
            ulong lengId = 0;
            ulong lengSz = 0;

            var obj = new T();
            var elementObj = obj as MasterElement;
            //기본값 null 셋팅
            element = default(T);
            
            if (elementObj == null)
            {
                return false;
            }

            elementObj.Parent = this;
            elementObj.Stream = stream;

            ulong ebmlId = GetVintId(stream, ref lengId);

            switch (ebmlId)
            {
                case ElementID.NotFound:
                    //아이디 발견하지 못함
                    break;
                case ElementID.CRC_32:
                    //crc 체크
                    //var crc32 = new CRC32();
                    break;
                case ElementID.Void:
                    //생략
                    break;
                default:
                    break;
            }

            if (ebmlId != elementObj.ID)
            {
                stream.Seek((long)lengId * -1, SeekOrigin.Current);
                return false;
            }

            elementObj.Size = GetVintSize(stream, ref lengSz);
            elementObj.Offset = stream.Position;
            element = obj;

            if (isLoadChildren)
            {
                return elementObj.LoadChildren();
            }
            else
            {
                stream.Seek((long)elementObj.Size, SeekOrigin.Current);
            }

            return true;
        }

        public bool LoadChildren() 
        {
            this.Stream.Seek(this.Offset, SeekOrigin.Begin);

            ulong lengId = 0;
            ulong lengSz = 0;
            ulong ts = 0;
            ulong id = 0;
            ulong sz = 0;

            while (ts < this.Size)
            {
                id = GetVintId(this.Stream, ref lengId);

                //잘못된 아이디가 발견됨.
                if (id == 0)
                {
                    Stream.Seek(this.Offset + (long)this.Size, SeekOrigin.Begin);
                    return false;
                }

                sz = GetVintSize(this.Stream, ref lengSz);

                if (MasterElementIDs != null && MasterElementIDs.Contains(id))
                {
                    this.SetMasterElementValue(id, sz);
                }
                else
                {
                    byte[] data = new byte[sz];
                    Stream.Read(data, 0, data.Length);

                    switch(id)
                    {
                        case ElementID.CRC_32:
                            //crc 체크
                            //var crc32 = new CRC32();
                            break;
                        case ElementID.Void:
                            //생략
                            break;
                        default:
                            this.SetElementValue(id, data);
                            break;
                    }
                }
                ts += lengId + lengSz + sz;
            }
            return true;
        }

        protected abstract void SetMasterElementValue(ulong id, ulong size);

        protected abstract void SetElementValue(ulong id, byte[] data);

        public abstract new ulong ID { get; }

        public abstract ulong[] MasterElementIDs { get; }

        public MasterElement()
            : base()
        { }
    }
}
