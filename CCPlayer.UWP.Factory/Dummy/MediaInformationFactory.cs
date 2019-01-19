using CCPlayer.UWP.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;

namespace CCPlayer.UWP.Factory.Dummy
{
    public sealed class MediaInformationFactory
    {
        public static IMediaInformation CreateMediaInformationFromStream(IRandomAccessStream stream)
        {
            return null;
        }

        public static IMediaInformation CreateMediaInformationFromStream(IRandomAccessStream stream, PropertySet ffmpegOptions)
        {
            return null;
        }

        public static IMediaInformation CreateMediaInformationFromUri(String uri)
        {
            return null;
        }

        public static IMediaInformation CreateMediaInformationFromUri(String uri, PropertySet ffmpegOptions)
        {
            return null;
        }
    }
}
