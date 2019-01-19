using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velostep.Common.Helpers;

namespace CCPlayer.HWCodecs.Matroska.Codecs
{
    public class CodecFactory
    {
        /// <summary>
        /// Lumia 1520 Series
        /// RM-937 - global version
        /// RM-938 - US version
        /// RM-939 - WCDMA HSPA+ version
        /// RM-940 - AT&T version
        /// 
        /// Lumia 930 Series
        /// 
        /// Lumia 830 Series
        /// RM-984 - global version
        /// RM-985 - US version
        /// </summary>
        /// <returns></returns>
        private static bool IsDolbyCertifiedDevice()
        {
            return DeviceHelper.CheckDeviceId(new String[]{
                            /*Lumia 1520 */ "RM-937", "RM-938", "RM-939", "RM-940" ,
                            /*Lumia  930 */ "RM-1045",
                            /*Lumia  830 */ "RM-984", "RM-985",
                        });
        }

        public static IEnumerable<ICodec> Create(List<EBML.TrackEntry> trackEntries)
        {
            List<ICodec> codecList = new List<ICodec>();
            foreach(var trackEntry in trackEntries.OrderByDescending(x => x.FlagDefault))
            {
                ICodec codec = null;
                string licenseCompany = string.Empty;
                string codecName = string.Empty;
                
                switch (trackEntry.CodecID)
                {
                    /* Video Codecs */
                    case @"V_MS/VFW/FOURCC":
#if DEBUG
                        codec = new TestCodec(trackEntry);
#endif
                        break;
                    case @"V_UNCOMPRESSED":
                        break;
                    //case @"V_MPEG4/ISO":
                    //    break;
                    case @"V_MPEG4/ISO/SP":
                        break;
                    case @"V_MPEG4/ISO/ASP":
                        //codec = new H264Codec(trackEntry);
                        break;
                    case @"V_MPEG4/ISO/AP":
                        break;
                    case @"V_MPEG4/ISO/AVC":
                        codec = new H264Codec(trackEntry);
                        break;
                    case @"V__MPEG4/MS/V3":
                        break;
                    case @"V_MMPEG1":
                        break;
                    case @"V_MPEG2":
                        break;
                    case @"V_REAL":
                        break;
                    case @"V_REAL/RV10":
                        break;
                    case @"V_REAL/RV20":
                        break;
                    case @"V_REAL/RV30":
                        break;
                    case @"V_REAL/RV40":
                        break;
                    case @"V_QUICKTIME":
                        break;
                    case @"V_THEORA":
                        break;
                    case @"V_PRORES":
                        break;
                    /* Audio Codecs */
                    case @"A_MPEG/L3":
                        codec = new MP3Codec(trackEntry);
                        break;
                    case @"A_MPEG/L2":
                        break;
                    case @"A_MPEG/L1":
                        break;
                    case @"A_PCM/INT/BIG":
                        break;
                    case @"A_PCM/INT/LIT":
                        codec = new PCMCodec(trackEntry);
                        break;
                    case @"A_PCM/FLOAT/IEEE":
                        break;
                    case @"A_MPC":
                        break;
                    case @"A_ALAC":
                        break;
                    case @"A_AC3":
                    case @"A_AC3/BSID9":
                    case @"A_AC3/BSID10":
                        //돌비 인증기기의 경우 코덱 사용
                        if (IsDolbyCertifiedDevice())
                        {
                            codec = new AC3Codec(trackEntry);
                        }
                        else
                        {
                            licenseCompany = "Dolby Laboratories, Inc.";
                            codecName = "(Dolby™) AC3";
                        }
                        break;
#if OMEGA
                    case @"A_DTS":
                    case @"A_DTS/EXPRESS":
                    case @"A_DTS/LOSSLESS":
                        codec = new DTSCodec(trackEntry);
                        break;
#else
                    case @"A_DTS":
                        licenseCompany = "DTS, Inc.";
                        codecName = "Digital Theatre System";
                        break;
                    case @"A_DTS/EXPRESS":
                        licenseCompany = "DTS, Inc.";
                        codecName = "Digital Theatre System Express";
                        break;
                    case @"A_DTS/LOSSLESS":
                        licenseCompany = "DTS, Inc.";
                        codecName = "Digital Theatre System Lossless";
                        break;
#endif
                    case @"A_VORBIS":
                        break;
                    case @"A_FLAC":
                        codec = new FlacCodec(trackEntry);
                        break;
                    case @"A_REAL":
                        break;
                    case @"A_REAL/14_4":
                        break;
                    case @"A_REAL/28_8":
                        break;
                    case @"A_REAL/COOK":
                        break;
                    case @"A_REAL/SIPR":
                        break;
                    case @"A_REAL/RALF":
                        break;
                    case @"A_AAC":
                    case @"A_AAC/MPEG2/MAIN":
                    case @"A_AAC/MPEG2/LC":
                    case @"A_AAC/MPEG2/LC/SBR":
                    case @"A_AAC/MPEG2/SSR":
                    case @"A_AAC/MPEG4/MAIN":
                    case @"A_AAC/MPEG4/LC":
                    case @"A_AAC/MPEG4/LC/SBR":
                    case @"A_AAC/MPEG4/SSR":
                    case @"A_AAC/MPEG4/LTP":
                        codec = new AACCodec(trackEntry);
                        break;
                    case @"A_QUICKTIME":
                        break;
                    case @"A_QUICKTIME/QDMC":
                        break;
                    case @"A_QUICKTIME/QDM2":
                        break;
                    case @"A_TTA1":
                        break;
                    case @"A_WAVPACK4":
                        break;
                    /* Subtitle */
                    case @"S_TEXT/UTF8":
                        codec = new SRTCodec(trackEntry);
                        break;
                    case @"S_TEXT/SSA":
                        codec = new SSACodec(trackEntry);
                        break;
                    case @"S_TEXT/ASS":
                        codec = new ASSCodec(trackEntry);
                        break;
                    case @"S_TEXT/USF":
                        break;
                    case @"S_IMAGE/BMP":
                        break;
                    case @"S_VOBSUB":
                        break;
                    case @"S_KATE":
                        break;
                    /* Subtitle */
                    case @"B_VOBBTN":
                        break;
                }

                if (codec == null)
                {
                    if (string.IsNullOrEmpty(codecName) && !string.IsNullOrEmpty(trackEntry.CodecID))
                    {
                        codecName = trackEntry.CodecID.Substring(1).Replace("_", " ");
                    }

                    codec = new UnkownCodec(trackEntry, codecName, licenseCompany);
                }
                codecList.Add(codec);
            }

            return codecList;
        }
    }
}
