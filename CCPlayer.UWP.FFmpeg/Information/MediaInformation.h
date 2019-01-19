//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#pragma once
#include <queue>
#include <collection.h>
#include "Decoder\Decoder.h"
#include "ThumbnailReader.h"

using namespace Platform;
using namespace CCPlayer::UWP::Common::Interface;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Media::Core;

extern "C"
{
#include <libavformat/avformat.h>
//#include <iconv.h>
}

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Information
			{
				public ref class MediaInformation sealed : IMediaInformation
				{
				public:
					static MediaInformation^ CreateMediaInformationFromStream(IRandomAccessStream^ stream, PropertySet^ ffmpegOptions);
					static MediaInformation^ CreateMediaInformationFromStream(IRandomAccessStream^ stream);
					static MediaInformation^ CreateMediaInformationFromUri(String^ uri, PropertySet^ ffmpegOptions);
					static MediaInformation^ CreateMediaInformationFromUri(String^ uri);

					virtual ~MediaInformation();
					virtual IAsyncOperation<IBuffer^>^ GetBitmapPixelBuffer(Size size);
					virtual Array<byte>^ GetThumbnailPixelBytes(Size size);
					virtual CCPlayer::UWP::Common::Codec::DecoderTypes GetRecommendedDecoderType(int videoStreamIndex, int audioStreamIndex);

					virtual property Windows::Foundation::Collections::IVectorView<CodecInformation^>^ CodecInformationList { Windows::Foundation::Collections::IVectorView<CodecInformation^>^ get(); }
					virtual property String^ ContainerName;
					virtual property String^ ContainerFullName;
					virtual property String^ Title;
					virtual property int DefaultVideoStreamIndex { int get(); }
					virtual property int DefaultAudioStreamIndex { int get(); }
					virtual property int DefaultSubtitleStreamIndex { int get(); }
					virtual property TimeSpan NaturalDuration { TimeSpan get(); }
					virtual property CCPlayer::UWP::Common::Codec::DecoderTypes RecommendedDecoderType { CCPlayer::UWP::Common::Codec::DecoderTypes get(); void set(CCPlayer::UWP::Common::Codec::DecoderTypes); }

				private:
					MediaInformation();

					HRESULT CreateMediaInformation(IRandomAccessStream^ stream, PropertySet^ ffmpegOptions);
					HRESULT CreateMediaInformation(String^ uri, PropertySet^ ffmpegOptions);
					HRESULT InitFFmpegContext();
					HRESULT ParseOptions(PropertySet^ ffmpegOptions);

				internal:
					AVDictionary* avDict;
					AVIOContext* avIOCtx;
					AVFormatContext* avFormatCtx;

					std::vector<AVCodecContext*> avCodecCtxList;

				private:
					int defaultVideoStreamIndex;
					int defaultAudioStreamIndex;
					int defaultSubtitleStreamIndex;
					CCPlayer::UWP::Common::Codec::DecoderTypes recommendedDecoderType;

					ThumbnailReader^ thumbnailReader;

					TimeSpan mediaDuration;
					IStream* fileStreamData;
					unsigned char* fileStreamBuffer;

					Windows::Foundation::Collections::IVector<CodecInformation^>^ _CodecInformationList;

				};
			}
		}
	}
}
