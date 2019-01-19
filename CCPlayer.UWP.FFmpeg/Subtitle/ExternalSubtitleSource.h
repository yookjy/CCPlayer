//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#pragma once

#include <collection.h>
#include "Source\FFmpegReader.h"
#include "ExternalSubtitleReader.h"
#include "Subtitle.h"

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;

extern "C"
{
#include <libavformat/avformat.h>
}

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Subtitle
			{
				public ref class ExternalSubtitleSource sealed
				{
				public:
					[Windows::Foundation::Metadata::DefaultOverload]
					static ExternalSubtitleSource^ CreateExternalSubtitleSourceFromStream(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^, IRandomAccessStream^);
					static ExternalSubtitleSource^ CreateExternalSubtitleSourceFromStream(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^, IRandomAccessStream^, PropertySet^);
					[Windows::Foundation::Metadata::DefaultOverload]
					static ExternalSubtitleSource^ CreateExternalSubtitleSourceFromUri(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^, String^);
					static ExternalSubtitleSource^ CreateExternalSubtitleSourceFromUri(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^, String^, PropertySet^);

					virtual ~ExternalSubtitleSource();
					void ConsumePacket(int index, int64_t pts);
					bool Seek(int64_t pts, int flag);
					//void test();

					property Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ SubtitleLanguages { Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ get(); }
					property String^ SelectedSubLanguageCode { String^ get(); void set(String^ value); }
					//property Windows::Foundation::Collections::IVector<String^>^ SubLanguages { Windows::Foundation::Collections::IVector<String^>^ get(); }
					property LONGLONG SynchronizeTime;
					property bool IsSeeking;
					//property int DetectedCodePage { int get(); }
					//property int SelectedCodePage;

				internal:
					AVDictionary* avDict;
					AVIOContext* avIOCtx;
					AVFormatContext* avFormatCtx;

					std::vector<AVCodecContext*> avCodecCtxList;

				private:
					ExternalSubtitleSource();

					[Windows::Foundation::Metadata::DefaultOverload]
					HRESULT CreateExternalSubtitleSource(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^, IRandomAccessStream^, PropertySet^);
					HRESULT CreateExternalSubtitleSource(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ , String^, PropertySet^);
					HRESULT InitFFmpegContext();
					HRESULT ParseOptions(PropertySet^ ffmpegOptions);
					void DetectCodePage();

					ExternalSubtitleReader* subtitleReader;
					//Windows::Foundation::Collections::IVector<SubtitleLanguage^>^ _SubtitleLanguages;
					//Windows::Foundation::Collections::IVector<String^>^ _SubLanguages;
					int defaultStreamIndex;
					String^ selectedSubLanguageCode;
					CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ subtitleDecoderConnector;

					IRandomAccessStream^ orgStream;
					IStream* fileStreamData;
					unsigned char* fileStreamBuffer;
					int detectedCodePage;
					bool hasHtmlTag;

				};
			}
		}
	}
}
