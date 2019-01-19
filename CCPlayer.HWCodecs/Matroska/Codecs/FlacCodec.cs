using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using FlacBox;
using System.IO;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class FlacCodec : KnownCodec
    {
        public FlacCodec(TrackEntry trackEntry) : base(trackEntry) { }

        private FlacStreaminfo flacInfo;

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
           //flacInfo.MaxFrameSize, flacInfo.MinFrameSize, data.Length, flacInfo.MaxBlockSize, flacInfo.MinBlockSize);
            byte[] flacData = data;
            using (System.IO.MemoryStream src = new System.IO.MemoryStream(data, 0, data.Length))
            {
                src.Position = 0;
                WaveOverFlacStream flacStream = new WaveOverFlacStream(flacInfo, src);
                
                using (var flacDataStream = new System.IO.MemoryStream())
                {
                    //byte[] b = new byte[data.Length];
                    byte[] b = new byte[16384];
                    while (true)
                    {
                        int i = flacStream.Read(b, 0, b.Length);
                        if (i > 0)
                        {
                            flacDataStream.Write(b, 0, i);
                        }
                        else
                        {
                            flacDataStream.Position = 0;
                            break;
                        }
                    }

                    flacData = flacDataStream.ToArray();
                }
            }


            FrameBufferData frame = new FrameBufferData(flacData, timeCode, duration, keyFrame, discardable, invisible);
            //System.Diagnostics.Debug.WriteLine(frame.TimeCode);
            return frame;
        }

        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            AudioEncodingProperties properties = null;
            using (FlacHeaderReader header = new FlacHeaderReader(new MemoryStream(TrackEntry.CodecPrivate), true))
            {
                while (header.Read()) { }
                flacInfo = header.Streaminfo;

                properties = AudioEncodingProperties.CreatePcm(
                            (uint)flacInfo.SampleRate,
                            (uint)flacInfo.ChannelsCount,
                            (uint)flacInfo.BitsPerSample);
            }

            var descriptor = new AudioStreamDescriptor(properties);
            if (TrackEntry.CodecPrivate != null)
            {
                properties.SetFormatUserData(TrackEntry.CodecPrivate);
            }

            return descriptor;
        }
    }
}
