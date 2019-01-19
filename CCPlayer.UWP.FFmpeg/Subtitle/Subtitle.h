#pragma once
#include <collection.h>
#include <queue>

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;

extern "C"
{
#include <libavformat/avformat.h>
}

#define CP_UCS2LE                   1200       // UTF-16LE translation
#define CP_UCS2BE                   1201       // UTF-16BE translation

#include <stdio.h>
#include <locale.h>
#include <tchar.h>
#include <mlang.h>

/*
#include <zlib.h>
#pragma comment (lib, "zlib.lib")

//Decompress an STL string using zlib and return the original data. 
inline int Uncompress(byte* src, uLong srcSize, byte** dst, uLong* dstSize)
{
	uint8_t *data = *dst;
	int isize = *dstSize;
	uint8_t *pkt_data = NULL;
	//uint8_t av_unused *newpktdata;
	uint8_t *newpktdata;
	int pkt_size = isize;
	int result = 0;

	z_stream zstream = { 0 };
	if (inflateInit(&zstream) != Z_OK)
		return -1;
	zstream.next_in = (Bytef*)src;
	zstream.avail_in = srcSize;
	do {
		pkt_size *= 3;

		//newpktdata = (uint8_t*)av_realloc(pkt_data, pkt_size);
		newpktdata = (uint8_t*)realloc(pkt_data, pkt_size);
		if (!newpktdata) {
			inflateEnd(&zstream);
			result = AVERROR(ENOMEM);
			goto failed;
		}
		pkt_data = newpktdata;
		zstream.avail_out = pkt_size - zstream.total_out;
		zstream.next_out = pkt_data + zstream.total_out;
		result = inflate(&zstream, Z_NO_FLUSH);
	} while (result == Z_OK && pkt_size < 10000000);
	pkt_size = zstream.total_out;
	inflateEnd(&zstream);
	if (result != Z_STREAM_END) {
		if (result == Z_MEM_ERROR)
			result = AVERROR(ENOMEM);
		else
			result = AVERROR_INVALIDDATA;
		goto failed;
	}

	*dst = pkt_data;
	*dstSize = pkt_size;
	return 0;

failed:
	//av_free(pkt_data);
	free(pkt_data);
	return result;
}
*/

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Subtitle
			{
				class CDetectCodepage
				{
				public:
					CDetectCodepage();
					~CDetectCodepage();
					BOOL Init(MLDETECTCP dwFlag = MLDETECTCP_NONE, DWORD dwPrefWinCodePage = CP_ACP);
					int	DetectCodepage(char* str, int length);
					int	DetectCodepage(IStream* stream);
					int	GetConfidence() { return m_nConfidence; }

				private:
					IMultiLanguage2*	m_mlang;
					MLDETECTCP			m_dwFlag;
					DWORD				m_dwPrefWinCodePage;

					int					m_nConfidence;					// 정답일 확률. 0~100 (100이상일 수 있음)
				};

				//public enum class SubtitleType {
				//	SUBTITLE_NONE,

				//	SUBTITLE_BITMAP,                ///< A bitmap, pict will be set

				//									/**
				//									* Plain text, the text field must be set by the decoder and is
				//									* authoritative. ass and pict fields may contain approximations.
				//									*/
				//	SUBTITLE_TEXT,

				//	/**
				//	* Formatted text, the ass field must be set by the decoder and is
				//	* authoritative. pict and text fields may contain approximations.
				//	*/
				//	SUBTITLE_ASS,

				//	//추가
				//	SUBTITLE_SRT, //4

				//	SUBTITLE_SAMI //5
				//};

				/*public ref class SubtitleLanguage sealed
				{
				private:
					Windows::Foundation::Collections::IMap<String^, String^>^ _Properties;
				public:
					SubtitleLanguage()
					{
						_Properties = ref new Platform::Collections::Map<String^, String^>();
					}
					property String^ Code;
					property String^ Name;
					property String^ Lang;
					property Windows::Foundation::Collections::IMap<String^, String^>^ Properties { Windows::Foundation::Collections::IMap<String^, String^>^ get() { return _Properties; } }
				};*/

				ref class SubtitleHelper sealed
				{
				internal:
					static void LoadASSHeader(String^ header,
						PropertySet^ *scriptInfoProp,
						IMap<String^, Windows::Data::Json::JsonObject^>^ *styleMap,
						std::vector<std::wstring>* eventList);

					static void ApplyASSStyle(Windows::Data::Json::JsonObject^ rect,
						IMap<String^, Windows::Data::Json::JsonObject^>^ styleMap,
						std::vector<std::wstring>* eventList);

					static void LoadSAMIHeader(String^ header,
						String^* title,
						PropertySet^ *commonStyleProp,
						IMap<String^, Windows::Data::Json::JsonObject^>^ *styleMap,
						IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ *languageList);

					static void ApplySAMIStyle(Windows::Data::Json::JsonObject^ pkt,
						PropertySet^ commonStyleProp,
						IMap<String^, Windows::Data::Json::JsonObject^>^ styleMap,
						IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ languageList);

					static bool IsSRTProcessing(AVCodecID codecId);

					static bool IsASSProcessing(AVCodecID codecId);

					static bool IsSAMIProcessing(AVCodecID codecId);

					static String^ GetHexColorCode(String^ colorValue);

					static void SetStyleProperty(String^ key, String^ value, Windows::Data::Json::JsonObject^ propJO);
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class SubtitleStream sealed
				{
				public:
					property CCPlayer::UWP::Common::Codec::SubtitleContentTypes SubtitleType { CCPlayer::UWP::Common::Codec::SubtitleContentTypes get(); }
					//property int CodePage { int get(); void set(int codePage); }
					property String^ Header { String^ get(); }
					property String^ Title { String^ get(); }
					property int Index { int get(); }
					property Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ SubtitleLanguages { Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ get(); }

					void LoadHeader(int codePage);
					Windows::Data::Json::JsonObject^ LockPacket(uint32 index);
					void UnlockPacket();
					void AppendPacket(Windows::Data::Json::JsonObject^ packet);
					void RemovePacket(uint32 index);
					void ClearePackets();
					uint32 GetPacketSize();
					virtual ~SubtitleStream();
				internal:
					//SubtitleStream(int index, AVCodecID codecId, char* extradata, char* subtitleHeader);
					SubtitleStream(int index, AVCodecContext* avctx);
					property PropertySet^ GlobalStyleProperty { PropertySet^ get(); }
					property Windows::Foundation::Collections::IMap<String^, Windows::Data::Json::JsonObject^>^ BlockStyleMap { Windows::Foundation::Collections::IMap<String^, Windows::Data::Json::JsonObject^>^ get(); }
					property std::vector<std::wstring> EventList { std::vector<std::wstring> get(); }
					property AVCodecContext* CodecContext { AVCodecContext* get(); }

				private:
					AVCodecContext* m_pAvCodecContext;
					//AVCodecID _CodecId;
					int _CodePage;
					int _Index;
					Windows::Foundation::Collections::IVector<Windows::Data::Json::JsonObject^>^ _Packets;
					Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ _SubtitleLanguages;
					PropertySet^ _GlobalStyleProperty;
					Windows::Foundation::Collections::IMap<String^, Windows::Data::Json::JsonObject^>^ _BlockStyleMap;
					std::vector<std::wstring> _EventList;
					String^ _Header;
					String^ _Title;
					/*char* m_extradata;
					char* m_subtitleHeader;*/
					std::mutex mutex;
				};

				typedef Windows::Foundation::Collections::IVector<SubtitleStream^> SubtitleStreamList;
				typedef std::vector<AVStream*> SubtitleAVStreamList;
			}
		}
	}
}
