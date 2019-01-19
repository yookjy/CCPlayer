using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class H264Codec : KnownCodec
    {
        public H264Codec(EBML.TrackEntry trackEntry) : base(trackEntry) 
        {
            //NALUnit헤더 생성
            SetNALUnitHeader(trackEntry.CodecPrivate);
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            var properties = VideoEncodingProperties.CreateH264();
            properties.Width = (uint)TrackEntry.Video.PixelWidth;
            properties.Height = (uint)TrackEntry.Video.PixelHeight;
            
            var descriptor = new VideoStreamDescriptor(properties);
            return descriptor;
        }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            FrameBufferData frame = null;

            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                if (keyFrame)
                {
                    ms.Write(NALUnitHeader, 0, NALUnitHeader.Length);
                }

                int i2 = 0, i = data.Length;
                while (i2 < i)
                {
                    byte[] nalu = GetNALU(data, i2);
                    ms.Write(NALUSpliter, 0, 3);
                    ms.Write(nalu, 0, nalu.Length);
                    i2 += (nalu.Length + NALULength);
                }
                //ms.Position = 0;
                frame = new FrameBufferData(ms.GetWindowsRuntimeBuffer(), timeCode, duration, keyFrame, discardable, invisible);
            }
            
            return frame;
        }

        int NALULength = 4;
        byte[] NALUSpliter = new byte[] { 0, 0, 1 };

        private void SetNALUnitHeader(byte[] rawData)
        {
            byte[] b = rawData;
            if (b[0] != 0x01)
            {
                throw new ArgumentException("CodecPrivateData format error");
            }
            int mp = 5;
            byte x1f = 0x1f;
            NALULength = b[4] & 3 + 1;
            var nsps = b[5] & x1f;
            mp++;
            var lsps = b[6] * 256 + b[7];
            mp += 2;
            int ml = nsps * lsps;
            mp += ml;
            var npps = b[mp];
            var lpps = b[mp + 1] * 256 + b[mp + 2];
            int ml2 = npps * lpps;
            byte[] cpdata = new byte[ml + ml2 + 6];
            cpdata[2] = 1;
            System.Buffer.BlockCopy(b, 8, cpdata, 3, ml);
            cpdata[ml + 5] = 1;
            System.Buffer.BlockCopy(b, mp + 3, cpdata, ml + 6, ml2);
            NALUnitHeader = cpdata;
        }

        private byte[] NALUnitHeader { get; set; }
        
        protected byte[] GetNALU(byte[] Buff, int offset)
        {
            try
            {
                byte[] b = new byte[NALULength];
                System.Buffer.BlockCopy(Buff, offset, b, 0, b.Length);
                var len2 = Element.ConvertVintToULong(b);
                b = new byte[len2];
                System.Buffer.BlockCopy(Buff, offset + NALULength, b, 0, b.Length);
                return b;
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
}
