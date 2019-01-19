using CCPlayer.HWCodecs.Matroska.EBML;
using CCPlayer.HWCodecs.Matroska.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using MediaParsers;
using System.IO;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class TestCodec : KnownCodec
    {
        VideoPrivateData videoPrivateData;

        public TestCodec(EBML.TrackEntry trackEntry)
            : base(trackEntry) 
        {
            //http://mpc-sb.googlecode.com/svn-history/r3/trunk/src/filters/parser/MatroskaSplitter/MatroskaSplitter.cpp
            videoPrivateData = new VideoPrivateData(trackEntry.CodecPrivate);
        }


        public override Windows.Media.Core.IMediaStreamDescriptor CreateMediaStreamDescriptor()
        {
            var mep = MediaEncodingProfile.CreateWmv(VideoEncodingQuality.Auto).Video;
            mep.Subtype = MediaEncodingSubtypes.Wmv3;
            mep.Width = (uint)TrackEntry.Video.DisplayWidth;
            mep.Height = (uint)TrackEntry.Video.PixelHeight;
            var descriptor = new VideoStreamDescriptor(mep);
            descriptor.EncodingProperties.SetFormatUserData(videoPrivateData.GetUserData());
            
            //mep.SetFormatUserData(videoPrivateData.GetUserData());
            
            //var fourCC = BitConverterLE.GetBytes(vih.BmiHeader.BiCompression);
            //var fourCCMap = Encoding.UTF8.GetString(fourCC, 0, 4);

            //var properties = VideoEncodingProperties.CreateUncompressed(
            //                        MediaEncodingSubtypes.Wmv3,
            //                        (uint)videoPrivateData.Width,
            //                        (uint)videoPrivateData.Height);
            
            ////properties.Subtype = MediaEncodingSubtypes.Asf;
            //properties.SetFormatUserData(videoPrivateData.GetUserData());
            //var descriptor = new VideoStreamDescriptor(properties);

            return descriptor;
        }

        public override FrameBufferData GetFrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discardable, bool invisible)
        {
            //V_MS/VFW/FOURCC
            ///VIDEOINFOHEADER2 videoInfoHeader = new 
            ///
            byte[] newData = null;
            //else
            {
                //newData = data;
            //    System.Diagnostics.Debug.WriteLine(keyFrame + ":" + BitConverter.ToString(data).Replace("-", string.Empty));
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);
                //ms.Write(videoPrivateData.GetUserData2(), 0, videoPrivateData.GetUserData2().Length);
                newData = ms.ToArray();
                ms.Dispose();
            }


            var frame = new FrameBufferData(newData, timeCode, duration, keyFrame, discardable, invisible);
            return frame;
        }
    }

    /// <summary>
    ///     Class for WMV Codec Private Data.
    /// </summary>
    public class VideoPrivateData
    {
        /// <summary>
        ///     Size of the data, in bytes.
        /// </summary>
        /// <remarks>
        ///     I'm not sure if it's always 45 bytes, but don't tell anyone!
        /// </remarks>
        private const int Size = 45;

        // these I found in the ASF specification (cf. section 9.2)
        private uint EncodedImageWidth;
        private uint EncodedImageHeight;
        private byte ReservedFlags;
        private ushort FormatDataSize;

        // the BITMAPINFOHEADER structure            [offset]
        private uint biSize;                        // 0
        private int biWidth;                    // 4
        private int biHeight;                    // 8
        private ushort biPlanes;                    // 12
        private ushort biBitCount;                    // 14
        private uint biCompression;                // 16
        private uint biSizeImage;                // 20
        private int biXPelsPerMeter;            // 24
        private int biYPelsPerMeter;            // 28
        private uint biClrUsed;                    // 32
        private uint biClrImportant;                // 36
        private byte[] CodecSpecificData = null;    // 40
        public byte[] FormatData { get { return CodecSpecificData; } }
        /// <summary>
        ///     Image width.
        /// </summary>
        public int Width
        {
            get
            {
                return this.biWidth;
            }
        }

        /// <summary>
        ///     Image height.
        /// </summary>
        public int Height
        {
            get
            {
                return this.biHeight;
            }
        }

        /// <summary>
        ///     Creates VideoPrivateData from raw bytes.
        /// </summary>
        /// <param name="data">The data to be parsed.</param>
        public VideoPrivateData(byte[] data)
        {
            // check buffer size
            //if (data.Length != VideoPrivateData.Size)
            //{
            //    throw new Exception("Wrong buffer size.");
            //}
            // read stuff
            this.biSize = BitConverter.ToUInt32(data, 0);
            this.biWidth = BitConverter.ToInt32(data, 4);
            this.biHeight = BitConverter.ToInt32(data, 8);
            this.biPlanes = BitConverter.ToUInt16(data, 12);
            this.biBitCount = BitConverter.ToUInt16(data, 14);
            this.biCompression = BitConverter.ToUInt32(data, 16);
            this.biSizeImage = BitConverter.ToUInt32(data, 20);
            this.biXPelsPerMeter = BitConverter.ToInt32(data, 24);
            this.biYPelsPerMeter = BitConverter.ToInt32(data, 28);
            this.biClrUsed = BitConverter.ToUInt32(data, 32);
            this.biClrImportant = BitConverter.ToUInt32(data, 36);
            this.CodecSpecificData = new byte[data.Length - 40];
            Array.Copy(data, 40, this.CodecSpecificData, 0, this.CodecSpecificData.Length);

            // set some ASF stuff
            this.ReservedFlags = 2; // this should always be 2
            this.FormatDataSize = (ushort)data.Length; ;

            // HACK: I have no idea why, but if I set those two
            // to anything except 320x240, the whole thing just doesn't work.
            this.EncodedImageWidth = (uint)320;
            this.EncodedImageHeight = (uint)240;
        }

        /// <summary>
        ///     Returns a base16-encoded (little-endian order) string containing the private data.
        /// </summary>
        /// <returns>A base16-encoded (little-endian order) string containing the private data.</returns>
        /*
       public string ToBase16()
       {
           string hex = "";
           hex += Encoder.ToBase16(this.EncodedImageWidth);
           hex += Encoder.ToBase16(this.EncodedImageHeight);
           hex += Encoder.ToBase16(this.ReservedFlags);
           hex += Encoder.ToBase16(this.FormatDataSize);
           hex += Encoder.ToBase16(this.biSize);
           hex += Encoder.ToBase16(this.biWidth);
           hex += Encoder.ToBase16(this.biHeight);
           hex += Encoder.ToBase16(this.biPlanes);
           hex += Encoder.ToBase16(this.biBitCount);
           hex += Encoder.ToBase16(this.biCompression);
           hex += Encoder.ToBase16(this.biSizeImage);
           hex += Encoder.ToBase16(this.biXPelsPerMeter);
           hex += Encoder.ToBase16(this.biYPelsPerMeter);
           hex += Encoder.ToBase16(this.biClrUsed);
           hex += Encoder.ToBase16(this.biClrImportant);

           // Now, if someone could tell me WHAT these bytes are,
           // and in what order I should write them...
           foreach (byte b in this.CodecSpecificData)
           {
               hex += Encoder.ToBase16(b);
           }
           return hex;
       }
       */
        public byte[] GetUserData()
        {
            byte[] data = null;
            using(MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(this.EncodedImageWidth), 0, 4);
                ms.Write(BitConverter.GetBytes(this.EncodedImageHeight), 0, 4);
                ms.Write(BitConverter.GetBytes(this.ReservedFlags), 0, 1);
                ms.Write(BitConverter.GetBytes(this.FormatDataSize), 0, 2);
                ms.Write(BitConverter.GetBytes(this.biSize), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biWidth), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biHeight), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biPlanes), 0, 2);
                ms.Write(BitConverter.GetBytes(this.biBitCount), 0, 2);
                ms.Write(BitConverter.GetBytes(this.biCompression), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biSizeImage), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biXPelsPerMeter), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biYPelsPerMeter), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biClrUsed), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biClrImportant), 0, 4);
                ms.Write(this.CodecSpecificData, 0, this.CodecSpecificData.Length);
                data = ms.ToArray();
            }
            return data;
        }

        public byte[] GetUserData2()
        {
            byte[] data = null;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(this.biSize), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biWidth), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biHeight), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biPlanes), 0, 2);
                ms.Write(BitConverter.GetBytes(this.biBitCount), 0, 2);
                ms.Write(BitConverter.GetBytes(this.biCompression), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biSizeImage), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biXPelsPerMeter), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biYPelsPerMeter), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biClrUsed), 0, 4);
                ms.Write(BitConverter.GetBytes(this.biClrImportant), 0, 4);
                ms.Write(this.CodecSpecificData, 0, this.CodecSpecificData.Length);
                data = ms.ToArray();
            }
            return data;
        }
    }

}
