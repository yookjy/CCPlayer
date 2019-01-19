using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.IO;

namespace MediaParsers
{
    public static class Extensions
    {
        public static uint ReadUInt8(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            offset += 1;
            return (uint)stream.ReadByte();
        }

        public static uint ReadUInt24(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] x = new byte[4];
            stream.Read(x, 1, 3);
            offset += 3;
            return BitConverterBE.ToUInt32(x, 0);
        }

        public static uint ReadUInt32(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] x = new byte[4];
            stream.Read(x, 0, 4);
            offset += 4;
            return BitConverterBE.ToUInt32(x, 0);
        }

        public static byte[] ReadBytes(this Stream stream, ref long offset, int length)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] buff = new byte[length];
            stream.Read(buff, 0, length);
            offset += length;
            return buff;
        }
    }
}
