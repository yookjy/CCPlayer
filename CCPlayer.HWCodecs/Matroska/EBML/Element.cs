using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.EBML
{
    public class Element
    {
        public ulong ID { get; set; }
        public ulong Size { get; set; }
        public long Offset { get; set; }
        public Stream Stream { get; set; }
        
        public Element() 
        {
            SetDefaultValues();
        }
        protected virtual void SetDefaultValues() { }

        public static ulong GetVintId(Stream stream, ref ulong len)
        {
            ulong id = 0;
            ulong cnt = 0;
            using (MemoryStream ms = new MemoryStream())
            {
                byte bits = 0;
                int length = 0;
                byte[] buffer = null;

                bits = (byte)stream.ReadByte();
                cnt++;
                if (bits != 0)
                {
                    var bitString = Convert.ToString(bits, 2);
                    length = (8 - (bitString.Length - 1));

                    ms.WriteByte(bits);
                    if (length != 1)
                    {
                        buffer = new byte[(int)length - 1];
                        stream.Read(buffer, 0, buffer.Length);
                        cnt += (uint)buffer.Length;
                        ms.Write(buffer, 0, buffer.Length);
                    }

                    len = (uint)ms.Length;
                    id = ConvertVintToULong(ms.ToArray());
                }
            }
            return id;
        }
        public static ulong GetVintSize(Stream stream, ref ulong len)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte bits = 0;
                uint length = 0;
                byte[] buffer = null;

                do
                {
                    bits = (byte)stream.ReadByte();

                    if (bits == 0)
                    {
                        length += 8;
                        //추가
                        ms.WriteByte(bits);
                    }
                    else
                    {
                        var bitString = Convert.ToString(bits, 2);
                        var leng = (8 - (bitString.Length - 1));
                        //변환
                        bits &= (byte)(255 >> leng);
                        length += (uint)leng;
                        //추가
                        ms.WriteByte(bits);
                        break;
                    }

                } while (bits == 0);

                if (ms.Length < length)
                {
                    buffer = new byte[(int)(length - ms.Length)];
                    stream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, buffer.Length);
                }

                len = (uint)ms.Length;
                return ConvertVintToULong(ms.ToArray());
            }
        }

        public static byte[] GetVintSize(byte[] source, int Offset, int Length)
        {
            int Width = 0;
            byte Align = 0;
            byte[] Tail = new byte[1];
            for (int i = Offset; i < Length; i++)
            {
                if (source[i] == 0)
                {
                    Width += 8;
                }
                else
                {
                    byte mask = 128;
                    int lt = 0;
                    while (mask > 0)
                    {
                        if ((source[i] & mask) > 0)
                        {
                            Align = (byte)(source[i] - mask);
                            Width += lt;
                            break;
                        }
                        else
                        {
                            mask /= 2;
                            lt++;
                        }
                    }
                    Tail = new byte[Width + 1];
                    Tail[0] = Align;
                    if (Width > 0)
                    {
                        System.Buffer.BlockCopy(source, i + 1, Tail, 1, Width);
                    }
                    break;
                }
            }
            return Tail;
        }

        public static ulong ConvertVintToULong(byte[] Tail)
        {
            ulong result = 0;
            for (int i = 0; i < Tail.Length; i++)
            {
                result = result * 256 + Tail[i];
            }
            return result;
        }
        public static long ConvertVintToLong(byte[] Tail)
        {
            long result = 0;
            for (int i = 0; i < Tail.Length; i++)
            {
                result = result * 256 + Tail[i];
            }
            switch (Tail.Length)
            {
                case 1:
                    result -= 63;
                    break;
                case 2:
                    result -= 8191;
                    break;
                case 3:
                    result -= 1048575L;
                    break;
                case 4:
                    result -= 134217727L;
                    break;
            }
            return result;
        }

        public static string ToString(byte[] data)
        {
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        public static string ToUtf8String(byte[] data)
        {
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        public static ulong ToULong(byte[] data)
        {
            byte[] b = new byte[8];
            System.Buffer.BlockCopy(data.Reverse().ToArray(), 0, b, 0, data.Length);
            return BitConverter.ToUInt64(b, 0);
        }

        public static long ToLong(byte[] data)
        {
            byte[] b = new byte[8];
            System.Buffer.BlockCopy(data.Reverse().ToArray(), 0, b, 0, data.Length);
            return BitConverter.ToInt64(b, 0);
        }

        public static double ToDouble(byte[] data)
        {
            double result = 0;
            if (data.Length ==  4)
            {
                result = BitConverter.ToSingle(data.Reverse().ToArray(), 0);
            }
            else if (data.Length == 8)
            {
                result = BitConverter.ToDouble(data.Reverse().ToArray(), 0);
            }
            return result;            
        }

        public static ulong NANO_SECONDS = 1000000000;
        public static long TIC_MILLISECONDS = 10000;

        public static DateTime ToDateTime(byte[] data)
        {
            byte[] b = new byte[8];
            System.Buffer.BlockCopy(data.Reverse().ToArray(), 0, b, 0, data.Length);
            var tick = BitConverter.ToInt64(b, 0);
            DateTime dt = new DateTime(2001, 01, 01, 00, 00, 00, DateTimeKind.Utc);
            return dt.AddSeconds((double)tick / NANO_SECONDS);
        }
    }
}
