using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;


#if DUMMY_FACTORY
using MediaInformation = CCPlayer.UWP.Factory.Dummy.MediaInformation;
#else
using MediaInformation = CCPlayer.UWP.FFmpeg.Information.MediaInformation;
#endif

namespace CCPlayer.UWP.Factory
{
    public sealed class MediaInformationFactory
    {
        public static IMediaInformation CreateMediaInformationFromStream(IRandomAccessStream stream)
        {
            return MediaInformation.CreateMediaInformationFromStream(stream);
        }

        public static IMediaInformation CreateMediaInformationFromStream(IRandomAccessStream stream, PropertySet ffmpegOptions)
        {
            return MediaInformation.CreateMediaInformationFromStream(stream, ffmpegOptions);
        }

        public static IMediaInformation CreateMediaInformationFromUri(String uri)
        {
            return MediaInformation.CreateMediaInformationFromUri(uri);
        }

        public static IMediaInformation CreateMediaInformationFromUri(String uri, PropertySet ffmpegOptions)
        {
            return MediaInformation.CreateMediaInformationFromUri(uri, ffmpegOptions);
        }
    }
}
