using FLVParer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaParsers.FlvParser
{
    public class ScriptData : FlvData
    {
        public List<KeyValuePair<string, object>> Values { get; private set; }
        public ScriptData(Stream stream, long offset, long length) : base (stream, offset, length)
        {
            Values = new List<KeyValuePair<string, object>>();
            stream.Position -= length;
            ReadScriptData();
        }
        public void ReadScriptData()
        {
            offset = 0;
            Values.Clear();
            byte[] bs = new byte[3];
            while (offset < this.length)
            {
                stream.Read(bs, 0, 3);
                if (bs[0] == 0 && bs[1] == 0 && bs[2] == 9)
                {
                    offset += 3;
                    break;
                }
                stream.Seek(-3, SeekOrigin.Current);
                AddElement("#" + offset, ReadElement(stream));
            }
        }
        private void AddElement(string key, object o)
        {
            Values.Add(new KeyValuePair<string, object>(key, o));
        }
        private object ReadElement(Stream src)
        {
            int type = src.ReadByte();
            offset++;
            switch (type)
            {
                case 0: // Number - 8
                    return ReadDouble(src);
                case 1: // Boolean - 1
                    return ReadByte(src);
                case 2: // String - 2+n
                    return ReadString(src);
                case 3: // Object
                    return ReadObject(src);
                case 4: // MovieClip
                    return ReadString(src);
                case 5: // Null
                    break;
                case 6: // Undefined
                    break;
                case 7: // Reference - 2
                    return ReadUShort(src);
                case 8: // ECMA array
                    return ReadArray(src);
                case 10: // Strict array
                    return ReadStrictArray(src);
                case 11: // Date - 8+2
                    return ReadDate(src);
                case 12: // Long string - 4+n
                    return ReadLongString(src);
            }
            return null;
        }
        private object ReadObject(Stream src)
        {
            byte[] bs = new byte[3];
            ScriptObject obj = new ScriptObject();
            while (offset < this.length)
            {
                src.Read(bs, 0, 3);
                if (bs[0] == 0 && bs[1] == 0 && bs[2] == 9)
                {
                    offset += 3;
                    break;
                }
                src.Seek(-3, SeekOrigin.Current);
                string key = ReadString(src);
                if (key[0] == 0)
                    break;
                obj[key] = ReadElement(src);
            }
            return obj;
        }
        private double ReadDate(Stream src)
        {
            double d = ReadDouble(src);
            src.Seek(2, SeekOrigin.Current);
            offset += 2;
            return d;
        }
        private ScriptObject ReadArray(Stream src)
        {
            byte[] buffer = new byte[4];
            src.Read(buffer, 0, 4);
            offset += 4;
            uint count = ByteUtils.ByteToUInt(buffer, 4);
            ScriptObject array = new ScriptObject();
            for (uint i = 0; i < count; i++)
            {
                string key = ReadString(src);
                array[key] = ReadElement(src);
            }
            src.Seek(3, SeekOrigin.Current); // 00 00 09
            offset += 3;
            return array;
        }
        private ScriptArray ReadStrictArray(Stream src)
        {
            byte[] bs = new byte[4];
            src.Read(bs, 0, 4);
            offset += 4;
            ScriptArray array = new ScriptArray();
            uint count = ByteUtils.ByteToUInt(bs, 4);
            for (uint i = 0; i < count; i++)
            {
                array.Add(ReadElement(src));
            }
            return array;
        }
        private double ReadDouble(Stream src)
        {
            byte[] buffer = new byte[8];
            src.Read(buffer, 0, 8);
            offset += 8;
            return ByteUtils.ByteToDouble(buffer);
        }
        private byte ReadByte(Stream src)
        {
            offset++;
            return (byte)src.ReadByte();
        }
        private string ReadString(Stream src)
        {
            byte[] bs = new byte[2];
            src.Read(bs, 0, 2);
            offset += 2;
            int n = (int)ByteUtils.ByteToUInt(bs, 2);
            bs = new byte[n];
            src.Read(bs, 0, n);
            offset += n;
            return Encoding.UTF8.GetString(bs, 0, bs.Length);
        }
        private string ReadLongString(Stream src)
        {
            byte[] bs = new byte[4];
            src.Read(bs, 0, 4);
            offset += 4;
            int n = (int)ByteUtils.ByteToUInt(bs, 4);
            bs = new byte[n];
            src.Read(bs, 0, n);
            offset += n;
            //return Encoding.ASCII.GetString(bs);
            return Encoding.UTF8.GetString(bs, 0, bs.Length);
        }
        private ushort ReadUShort(Stream src)
        {
            byte[] buffer = new byte[2];
            src.Read(buffer, 0, 2);
            offset += 2;
            return (ushort)ByteUtils.ByteToUInt(buffer, 2);
        }
    }
    public class ScriptObject
    {
        public static int indent = 0;
        private Dictionary<string, object> values = new Dictionary<string, object>();
        public object this[string key]
        {
            get
            {
                object o;
                values.TryGetValue(key, out o);
                return o;
            }
            set
            {
                if (!values.ContainsKey(key))
                {
                    values.Add(key, value);
                }
            }
        }
        public override string ToString()
        {
            string str = "{\r\n";
            ScriptObject.indent += 2;
            foreach (KeyValuePair<string, object> kv in values)
            {
                str += new string(' ', ScriptObject.indent)
                          + kv.Key + ": " + kv.Value + "\r\n";
            }
            ScriptObject.indent -= 2;
            //if (str.Length > 1)
            // str = str.Substring(0, str.Length - 1);
            str += "}";
            return str;
        }
    }
    public class ScriptArray
    {
        private List<object> values = new List<object>();
        public object this[int index]
        {
            get
            {
                if (index >= 0 && index < values.Count)
                    return values[index];
                return null;
            }
        }
        public void Add(object o)
        {
            values.Add(o);
        }
        public override string ToString()
        {
            string str = "[";
            int n = 0;
            foreach (object o in values)
            {
                if (n % 10 == 0)
                    str += "\r\n";
                n++;
                str += o + ",";
            }
            if (str.Length > 1)
                str = str.Substring(0, str.Length - 1);
            str += "\r\n]";
            return str;
        }

        public List<object> Values
        {
            get
            {
                return values;
            }
        }

    }

    
}
