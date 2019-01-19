namespace MediaParsers.Mp4Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// 
    /// </summary>
    public static class Mp4Util
    {
        /// <summary>
        /// 
        /// </summary>
        private static readonly DateTime offsetDate = new DateTime(0x770, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void BytesFromDoubleBE(byte[] bytes, double value)
        {
            byte[] array = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(array);
            }
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void BytesFromUInt16BE(byte[] bytes, ushort value)
        {
            bytes[0] = (byte) (value >> 8);
            bytes[1] = (byte) value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void BytesFromUInt24BE(byte[] bytes, uint value)
        {
            bytes[0] = (byte) (value >> 0x10);
            bytes[1] = (byte) (value >> 8);
            bytes[2] = (byte) value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void BytesFromUInt32BE(byte[] bytes, uint value)
        {
            bytes[0] = (byte) (value >> 0x18);
            bytes[1] = (byte) (value >> 0x10);
            bytes[2] = (byte) (value >> 8);
            bytes[3] = (byte) value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void BytesFromUInt64BE(byte[] bytes, ulong value)
        {
            bytes[0] = (byte) (value >> 0x38);
            bytes[1] = (byte) (value >> 0x30);
            bytes[2] = (byte) (value >> 40);
            bytes[3] = (byte) (value >> 0x20);
            bytes[4] = (byte) (value >> 0x18);
            bytes[5] = (byte) (value >> 0x10);
            bytes[6] = (byte) (value >> 8);
            bytes[7] = (byte) value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static double BytesToDoubleBE(byte[] bytes)
        {
            return BitConverter.Int64BitsToDouble((long) BytesToUInt64BE(bytes));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static ushort BytesToUInt16BE(byte[] bytes)
        {
            return (ushort) ((bytes[0] << 8) | bytes[1]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static uint BytesToUInt24BE(byte[] bytes)
        {
            return (uint) (((bytes[0] << 0x10) | (bytes[1] << 8)) | bytes[2]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static uint BytesToUInt32BE(byte[] bytes)
        {
            return BytesToUInt32BE(bytes, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static uint BytesToUInt32BE(byte[] bytes, int offset)
        {
            return (uint) ((((bytes[offset] << 0x18) | (bytes[offset + 1] << 0x10)) | (bytes[offset + 2] << 8)) | bytes[offset + 3]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static ulong BytesToUInt64BE(byte[] bytes)
        {
            return (ulong) ((((((((bytes[0] << 0x38) | (bytes[1] << 0x30)) | (bytes[2] << 40)) | (bytes[3] << 0x20)) | (bytes[4] << 0x18)) | (bytes[5] << 0x10)) | (bytes[6] << 8)) | bytes[7]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static uint ConvertTime(DateTime date)
        {
            return (uint) date.Subtract(offsetDate).TotalSeconds;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeValue"></param>
        /// <param name="fromTimeScale"></param>
        /// <param name="toTimeScale"></param>
        /// <returns></returns>
        public static ulong ConvertTime(ulong timeValue, uint fromTimeScale, uint toTimeScale)
        {
            if (fromTimeScale == 0)
            {
                return 0L;
            }
            return (ulong) ((timeValue * toTimeScale) / ((double) fromTimeScale));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static uint Create(string str)
        {
            if (str.Length != 4)
            {
                throw new ArgumentException("String must be of lenght four", "str");
            }
            return Create(str[0], str[1], str[2], str[3]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <param name="c3"></param>
        /// <param name="c4"></param>
        /// <returns></returns>
        public static uint Create(char c1, char c2, char c3, char c4)
        {
            return (uint)((((c1 << 0x18) | (c2 << 0x10)) | (c3 << 8)) | c4);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="units"></param>
        /// <param name="units_per_second"></param>
        /// <returns></returns>
        public static uint DurationMsFromUnits(ulong units, uint units_per_second)
        {
            if (units_per_second == 0)
            {
                return 0;
            }
            return (uint) ((units * 1000.0) / ((double) units_per_second));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FormatDouble(ulong value)
        {
            return ((value >> 0x10) + "." + (value & ((ulong) 0xffffL)));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FormatFloat(ushort value)
        {
            return ((value >> 8) + "." + (value & 0xff));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static string FormatFourChars(List<uint> list)
        {
            string str = string.Empty;
            if (list.Count > 0)
            {
                str = str + FormatFourChars(list[0]);
                for (int i = 1; i < list.Count; i++)
                {
                    str = str + "," + FormatFourChars(list[i]);
                }
            }
            return str;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FormatFourChars(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static string FormatTime(ulong time)
        {
            return offsetDate.Add(TimeSpan.FromSeconds((double) time)).ToString("M/d/yyyy hh:mm:ss");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetMpeg4AudioObjectTypeString(Mp4Mpeg4AudioObjectType type)
        {
            switch (type)
            {
                case Mp4Mpeg4AudioObjectType.AAC_MAIN:
                    return "AAC Main Profile";

                case Mp4Mpeg4AudioObjectType.AAC_LC:
                    return "AAC Low Complexity";

                case Mp4Mpeg4AudioObjectType.AAC_SSR:
                    return "AAC Scalable Sample Rate";

                case Mp4Mpeg4AudioObjectType.AAC_LTP:
                    return "AAC Long Term Predictor";

                case Mp4Mpeg4AudioObjectType.SBR:
                    return "Spectral Band Replication";

                case Mp4Mpeg4AudioObjectType.AAC_SCALABLE:
                    return "AAC Scalable";

                case Mp4Mpeg4AudioObjectType.TWINVQ:
                    return "Twin VQ";

                case Mp4Mpeg4AudioObjectType.ER_AAC_LC:
                    return "Error Resilient AAC Low Complexity";

                case Mp4Mpeg4AudioObjectType.ER_AAC_LTP:
                    return "Error Resilient AAC Long Term Prediction";

                case Mp4Mpeg4AudioObjectType.ER_AAC_SCALABLE:
                    return "Error Resilient AAC Scalable";

                case Mp4Mpeg4AudioObjectType.ER_TWINVQ:
                    return "Error Resilient Twin VQ";

                case Mp4Mpeg4AudioObjectType.ER_BSAC:
                    return "Error Resilient Bit Sliced Arithmetic Coding";

                case Mp4Mpeg4AudioObjectType.ER_AAC_LD:
                    return "Error Resilient AAC Low Delay";

                case Mp4Mpeg4AudioObjectType.LAYER_1:
                    return "MPEG Layer 1";

                case Mp4Mpeg4AudioObjectType.LAYER_2:
                    return "MPEG Layer 2";

                case Mp4Mpeg4AudioObjectType.LAYER_3:
                    return "MPEG Layer 3";
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oti"></param>
        /// <returns></returns>
        public static string GetObjectTypeString(Mp4ObjectTypeIndication oti)
        {
            switch (oti)
            {
                case Mp4ObjectTypeIndication.MPEG4_SYSTEM:
                    return "MPEG-4 System";

                case Mp4ObjectTypeIndication.MPEG4_SYSTEM_COR:
                    return "MPEG-4 System COR";

                case Mp4ObjectTypeIndication.MPEG4_VISUAL:
                    return "MPEG-4 Video";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_SIMPLE:
                    return "MPEG-2 Video Simple Profile";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_MAIN:
                    return "MPEG-2 Video Main Profile";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_SNR:
                    return "MPEG-2 Video SNR";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_SPATIAL:
                    return "MPEG-2 Video Spatial";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_HIGH:
                    return "MPEG-2 Video High";

                case Mp4ObjectTypeIndication.MPEG2_VISUAL_422:
                    return "MPEG-2 Video 4:2:2";

                case Mp4ObjectTypeIndication.MPEG2_AAC_AUDIO_MAIN:
                    return "MPEG-2 Audio AAC Main Profile";

                case Mp4ObjectTypeIndication.MPEG2_AAC_AUDIO_LC:
                    return "MPEG-2 Audio AAC Low Complexity";

                case Mp4ObjectTypeIndication.MPEG2_AAC_AUDIO_SSRP:
                    return "MPEG-2 Audio AAC SSRP";

                case Mp4ObjectTypeIndication.MPEG2_PART3_AUDIO:
                    return "MPEG-2 Audio Part-3";

                case Mp4ObjectTypeIndication.MPEG1_VISUAL:
                    return "MPEG-1 Video";

                case Mp4ObjectTypeIndication.MPEG1_AUDIO:
                    return "MPEG-1 Audio";

                case Mp4ObjectTypeIndication.JPEG:
                    return "JPEG";

                case Mp4ObjectTypeIndication.MPEG4_AUDIO:
                    return "MPEG-4 Audio";
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public static string GetProfileName(byte profile)
        {
            switch (profile)
            {
                case 0x42:
                    return "Baseline";

                case 0x4d:
                    return "Main";

                case 0x58:
                    return "Extended";

                case 100:
                    return "High";

                case 110:
                    return "High 10";

                case 0x7a:
                    return "High 4:2:2";

                case 0x90:
                    return "High 4:4:4";
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetStreamTypeString(Mp4StreamType type)
        {
            switch (type)
            {
                case Mp4StreamType.FORBIDDEN:
                    return "INVALID";

                case Mp4StreamType.OD:
                    return "Object Descriptor";

                case Mp4StreamType.CR:
                    return "CR";

                case Mp4StreamType.BIFS:
                    return "BIFS";

                case Mp4StreamType.VISUAL:
                    return "Visual";

                case Mp4StreamType.AUDIO:
                    return "Audio";

                case Mp4StreamType.MPEG7:
                    return "MPEG-7";

                case Mp4StreamType.IPMP:
                    return "IPMP";

                case Mp4StreamType.OCI:
                    return "OCI";

                case Mp4StreamType.MPEGJ:
                    return "MPEG-J";
            }
            return "UNKNOWN";
        }
    }
}

