#pragma once
#include <collection.h>
#include "Subtitle.h"

using namespace Platform;
using namespace Platform::Collections;
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
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class ExternalSubtitleParser sealed
				{
				public:

					virtual ~ExternalSubtitleParser();

					static ExternalSubtitleParser^ CreateExternalSubtitleParserFromStream(IRandomAccessStream^ stream);
					static ExternalSubtitleParser^ CreateExternalSubtitleParserFromUri(String^ uri);

					property Windows::Foundation::Collections::IVector<SubtitleStream^>^ SubtitleStreams { Windows::Foundation::Collections::IVector<SubtitleStream^>^ get(); }
					property SubtitleStream^ SelectedSubtitleStream { SubtitleStream^ get(); };
					property uint32 SelectedSubtitleStreamIndex;
					property LONGLONG PacketTime;
					property LONGLONG SynchronizeTime;
					property String^ SubLangCode;
					property Windows::Foundation::Collections::IVector<String^>^ SubLanguages { Windows::Foundation::Collections::IVector<String^>^ get(); }

					//Windows::Data::Json::JsonObject^ GetSubtitlePacket();
					Windows::Data::Json::JsonObject^ PopSubtitlePacket(int64_t time);
					
					bool Seek(long long time);
					bool SyncTime();

					event Windows::Foundation::EventHandler<long long>^ FailedDecoding;

				internal:
					//여기부터
					AVIOContext* m_pAvIOCtx;
					AVFormatContext* m_pAvFormatCtx;
					void ChangeCodePage();
					void LoadHeader();
					void Stop();

				private:
					ExternalSubtitleParser();

					HRESULT CreateMediaFileInformation(IRandomAccessStream^ stream);
					HRESULT CreateMediaFileInformation(String^ uri);
					HRESULT InitFFmpegContext();
					HRESULT ReadPacket();
					void CloseFFmpegContext();

					bool m_stopped;
					bool m_frameComplete;
					IStream* m_pFileStreamData;
					IInputStream^ m_orgStream;
					unsigned char* m_pFileStreamBuffer;
					SubtitleAVStreamList m_subtitleAVStreamList;
					int m_detectedCodePage;
					//Windows::Data::Json::JsonObject^ prevPacket;

					SubtitleStreamList^ _SubtitleStreams;
					Windows::Foundation::Collections::IVector<String^>^ _SubLanguages;
				};
			}
		}
	}
}
