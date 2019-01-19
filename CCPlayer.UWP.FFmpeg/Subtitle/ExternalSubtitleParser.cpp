#include "pch.h"
#include "Common\CCPlayerConst.h"
#include "ExternalSubtitleParser.h"
#include "Bridge\SubtitleBridge.h"
#include "shcore.h"
//
//
//#pragma comment (lib, "iconv.lib")
//
//#include <iconv.h>

#include <algorithm>
#include <string>

using namespace CCPlayer::UWP::FFmpeg::Subtitle;
using namespace Platform;
using namespace CCPlayer::UWP::FFmpeg::Bridge;

// Size of the buffer when reading a stream
static const int FILESTREAMBUFFERSZ = 32 * 1024;

// Static functions passed to FFmpeg for stream interop
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize);
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence);

ExternalSubtitleParser::ExternalSubtitleParser()
	: m_pAvIOCtx(nullptr)
	, m_pAvFormatCtx(nullptr)
	, m_frameComplete(false)
	, m_detectedCodePage(-1)
{
	av_register_all();

	_SubtitleStreams = ref new Platform::Collections::Vector<SubtitleStream^>();
	_SubLanguages = ref new Platform::Collections::Vector<String^>();
	SelectedSubtitleStreamIndex = -1;
	m_stopped = false;
}

ExternalSubtitleParser::~ExternalSubtitleParser()
{
	CloseFFmpegContext();
}

ExternalSubtitleParser^ ExternalSubtitleParser::CreateExternalSubtitleParserFromStream(IRandomAccessStream^ stream)
{
	auto finder = ref new ExternalSubtitleParser();
	if (FAILED(finder->CreateMediaFileInformation(stream)))
	{
		// We failed to initialize, clear the variable to return failure
		finder = nullptr;
	}

	return finder;
}

ExternalSubtitleParser^ ExternalSubtitleParser::CreateExternalSubtitleParserFromUri(String^ uri)
{
	auto finder = ref new ExternalSubtitleParser();
	if (FAILED(finder->CreateMediaFileInformation(uri)))
	{
		// We failed to initialize, clear the variable to return failure
		finder = nullptr;
	}

	return finder;
}

HRESULT ExternalSubtitleParser::CreateMediaFileInformation(IRandomAccessStream^ stream)
{
	HRESULT hr = S_OK;
	if (!stream)
	{
		hr = E_INVALIDARG;
	}

	if (SUCCEEDED(hr))
	{
		// Convert asynchronous IRandomAccessStream to synchronous IStream. This API requires shcore.h and shcore.lib
		hr = CreateStreamOverRandomAccessStream(reinterpret_cast<IUnknown*>(stream), IID_PPV_ARGS(&m_pFileStreamData));
		m_orgStream = stream;
	}

	if (SUCCEEDED(hr))
	{
		// Setup FFmpeg custom IO to access file as stream. This is necessary when accessing any file outside of app installation directory and appdata folder.
		// Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
		m_pFileStreamBuffer = (unsigned char*)av_malloc(FILESTREAMBUFFERSZ);
		if (m_pFileStreamBuffer == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pAvIOCtx = avio_alloc_context(m_pFileStreamBuffer, FILESTREAMBUFFERSZ, 0, m_pFileStreamData, FileStreamRead, 0, FileStreamSeek);
		if (m_pAvIOCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx = avformat_alloc_context();
		if (m_pAvFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx->pb = m_pAvIOCtx;
		m_pAvFormatCtx->flags |= AVFMT_FLAG_CUSTOM_IO;

		// Open media file using custom IO setup above instead of using file name. Opening a file using file name will invoke fopen C API call that only have
		// access within the app installation directory and appdata folder. Custom IO allows access to file selected using FilePicker dialog.
		if (avformat_open_input(&m_pAvFormatCtx, "", NULL, NULL) < 0)
		{
			hr = E_FAIL; // Error opening file
		}
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

SubtitleStream^ ExternalSubtitleParser::SelectedSubtitleStream::get()
{
	if ((int)SelectedSubtitleStreamIndex > -1 && SelectedSubtitleStreamIndex < _SubtitleStreams->Size)
	{
		return _SubtitleStreams->GetAt(SelectedSubtitleStreamIndex);
	}
	return nullptr;
}

HRESULT ExternalSubtitleParser::CreateMediaFileInformation(String^ uri)
{
	HRESULT hr = S_OK;
	const char* charStr = nullptr;
	if (!uri)
	{
		hr = E_INVALIDARG;
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx = avformat_alloc_context();
		if (m_pAvFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		std::string uriA(uri->Begin(), uri->End());
		charStr = uriA.c_str();

		// Open media file using custom IO setup above instead of using file name. Opening a file using file name will invoke fopen C API call that only have
		// access within the app installation directory and appdata folder. Custom IO allows access to file selected using FilePicker dialog.
		if (avformat_open_input(&m_pAvFormatCtx, charStr, NULL, NULL) < 0)
		{
			hr = E_FAIL; // Error opening file
		}
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

HRESULT ExternalSubtitleParser::InitFFmpegContext()
{
	HRESULT hr = S_OK;

	if (avformat_find_stream_info(m_pAvFormatCtx, NULL) < 0)
	{
		hr = E_FAIL; // Error finding info
	}

	if (SUCCEEDED(hr))
	{
		CDetectCodepage mlang;

		for (unsigned int i = 0; i < m_pAvFormatCtx->nb_streams; i++)
		{
			AVCodec* pAvCodec = nullptr;
			AVCodecContext* pAvCodecCtx = nullptr;
			AVStream* stream = m_pAvFormatCtx->streams[i];

			// find the audio stream and its decoder
			pAvCodec = avcodec_find_decoder(stream->codecpar->codec_id);

			switch (stream->codecpar->codec_type)
			{
			case AVMediaType::AVMEDIA_TYPE_SUBTITLE:
				if (SUCCEEDED(hr))
				{
					if (pAvCodec)
					{
						pAvCodecCtx = avcodec_alloc_context3(pAvCodec);
						if (pAvCodecCtx)
						{
							avcodec_parameters_to_context(pAvCodecCtx, stream->codecpar);
						}

						pAvCodecCtx->debug_mv = 0;
						pAvCodecCtx->debug = 0;
						pAvCodecCtx->workaround_bugs = FF_BUG_AUTODETECT;

						//사용하면 좋겠지만, 윈도우에서 ICONV 빌드에 문제가 있음. (원래 안되는 것일지도 - 원래 UNIX용)
						//pAvCodecCtx->sub_charenc = "CP949";
						int err = avcodec_open2(pAvCodecCtx, pAvCodec, NULL);

						if (err < 0)
						{
							avcodec_free_context(&pAvCodecCtx);
							pAvCodecCtx = nullptr;
							hr = E_FAIL;
						}
						else
						{
							if (SelectedSubtitleStreamIndex == -1)
							{
								//기본 자막 인덱스
								SelectedSubtitleStreamIndex = av_find_best_stream(m_pAvFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, -1, &pAvCodec, 0);
							}

							SubtitleStream^ subtitleStream = ref new SubtitleStream(stream->index, pAvCodecCtx);
							_SubtitleStreams->Append(subtitleStream);
							m_subtitleAVStreamList.push_back(stream);
						}
					}
				}
				break;
			case AVMediaType::AVMEDIA_TYPE_ATTACHMENT:
				break;
			default:
				continue;
			}
		}
	}

	return hr;
}

Windows::Foundation::Collections::IVector<SubtitleStream^>^ ExternalSubtitleParser::SubtitleStreams::get()
{
	return _SubtitleStreams;
}

Windows::Foundation::Collections::IVector<String^>^ ExternalSubtitleParser::SubLanguages::get()
{
	return _SubLanguages;
}

Windows::Data::Json::JsonObject^ ExternalSubtitleParser::PopSubtitlePacket(int64_t time)
{
	if (SelectedSubtitleStream != nullptr && !m_stopped)
	{
		while (SelectedSubtitleStream->GetPacketSize() == 0 && !m_frameComplete)
		{
			ReadPacket();
		}

		if (SelectedSubtitleStream->GetPacketSize() > 0)
		{
			auto packet = SelectedSubtitleStream->LockPacket(0);
			//String^ tmpPacketString = SelectedSubtitleStream->LockPacket(0)->Stringify();
			//SelectedSubtitleStream->UnlockPacket();
			//if (tmpPacketString != nullptr)
			//{
				//Windows::Data::Json::JsonObject^ packet = Windows::Data::Json::JsonObject::Parse(tmpPacketString);
				int64_t subPktTime = (int64_t)(packet->GetNamedNumber("Pts") + (packet->GetNamedNumber("StartDisplayTime")) + SynchronizeTime);

				if (time >= subPktTime)
				{
					SelectedSubtitleStream->UnlockPacket();
					SelectedSubtitleStream->RemovePacket(0);
					//싱크 타임 적용 
					packet->SetNamedValue("Pts", Windows::Data::Json::JsonValue::CreateNumberValue(packet->GetNamedNumber("Pts") + SynchronizeTime));
					return packet;
				}
			//}
			SelectedSubtitleStream->UnlockPacket();
		}
	}
	return nullptr;
}
long long testVal = 0;
bool ExternalSubtitleParser::Seek(long long time)
{
	if (m_pAvFormatCtx == NULL || m_stopped) return false;

	time += SynchronizeTime;

	if (time < 0)
	{
		time = 0;
	}

	int seeked = -1;
	bool result = false;
	//int64_t seekTarget = av_rescale_q(time, { 1, 10000000L }, m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base);
	//int64_t seekTarget = static_cast<int64_t>(time * 10000000LL / (av_q2d(m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base)));
	// Convert TimeSpan unit to AV_TIME_BASE

	int64_t seekTarget = static_cast<int64_t>(time / (av_q2d(m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base) * 10000000));
	
	if (m_pAvFormatCtx->iformat->read_seek != nullptr)
	{
		seeked = av_seek_frame(m_pAvFormatCtx, -1, seekTarget, 0);
	}
	else if (m_pAvFormatCtx->iformat->read_seek2 != nullptr)
	{
		seeked = m_pAvFormatCtx->iformat->read_seek2(m_pAvFormatCtx, SelectedSubtitleStreamIndex, 0, seekTarget, INT64_MAX, 0);
	}

	if (seeked >= 0)
	{
		//저장된 패킷 제거
		for (uint32 i = 0; i < _SubtitleStreams->Size; i++)
		{
			avcodec_flush_buffers(_SubtitleStreams->GetAt(i)->CodecContext);
			_SubtitleStreams->GetAt(i)->ClearePackets();
		}
		result = true;
		//종료 플래그 초기화 (미디어가 끝까지 재생된 후에 다시 재생시 자막 재생이 안되는 현상의 원인)
		m_frameComplete = false;
	}
	else
	{
		DebugMessage(L"Seeking Failed!\n\r");
	}
	return result;
}

bool ExternalSubtitleParser::SyncTime()
{
	return Seek(this->PacketTime);
}

void ExternalSubtitleParser::Stop()
{
	m_stopped = true;
}

void ExternalSubtitleParser::ChangeCodePage()
{
	auto bridge = Bridge::SubtitleBridge::Instance;
	bridge->NeedChangeCodePage = false;

	if (bridge->CodePage == -1)
	{
		bool isHTML = false;
		for (uint32 i = 0; i < SubtitleStreams->Size; i++)
		{
			auto type = SubtitleStreams->GetAt(i)->SubtitleType;
			if (type == SubtitleType::SUBTITLE_SRT || type == SubtitleType::SUBTITLE_SAMI)
			{
				isHTML = true;
				break;
			}
		}
		//자동 검색
		CDetectCodepage mlang;
		int confidence = 0;

		if (mlang.Init(isHTML ? MLDETECTCP_HTML : MLDETECTCP_NONE))
		{
			if (m_pFileStreamData != nullptr)
			{
				//파일의 경우
				m_detectedCodePage = mlang.DetectCodepage(m_pFileStreamData);
			}
			else
			{
				//스트리밍의 경우
				m_detectedCodePage = mlang.DetectCodepage((char*)m_pAvFormatCtx->pb->buffer, m_pAvFormatCtx->pb->buffer_size);
			}
		}

		//정확도
		confidence = mlang.GetConfidence();
		if (confidence <= 95)
		{
			//2차 검색
			PropertySet^ propSet = Lime::CPPHelper::EncodingHelper::CharsetDetectorFromStream(m_orgStream);

			if (propSet->Size > 0 && propSet->HasKey("IsFound") && static_cast<bool>(propSet->Lookup("IsFound")))
			{
				if (propSet->HasKey("Confidence") && static_cast<int>(propSet->Lookup("Confidence")) >= confidence)
				{
					m_detectedCodePage = static_cast<int>(propSet->Lookup("CodePage"));
				}
			}
		}

		//검색되지 않은 경우 UTF8 강제 설정
		if (m_detectedCodePage == -1)
		{
			m_detectedCodePage = CP_UTF8;
		}
	}
	else
	{
		m_detectedCodePage = bridge->CodePage;
	}
}

void ExternalSubtitleParser::LoadHeader()
{
	//헤더를 로드
	SelectedSubtitleStream->LoadHeader(m_detectedCodePage);
}

HRESULT ExternalSubtitleParser::ReadPacket()
{
	HRESULT hr = S_OK;

	if (SelectedSubtitleStreamIndex == -1 || m_frameComplete)
	{
		hr = E_FAIL;
		return hr;
	}

	try
	{
		bool decodeSuccess = false;
		int ret = 0;

		while (!m_frameComplete && !decodeSuccess)
		{
			byte* replacedData = nullptr;
			int replacedSize = 0;
			AVPacket avPacket;
			av_init_packet(&avPacket);
			avPacket.data = NULL;
			avPacket.size = 0;

			ret = av_read_frame(m_pAvFormatCtx, &avPacket);

			if (ret < 0)
			{
				if (ret == AVERROR_EOF || (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached))
				{
					DebugMessage(L"GetNextSample reaching EOF\n");
					//hr = E_FAIL;
					m_frameComplete = true;
					break;
				}
			}

			auto avStream = m_subtitleAVStreamList.at(avPacket.stream_index);
			auto ccStream = _SubtitleStreams->GetAt(avStream->index);

			AVSubtitle sub;
			int gotSub = 0;
			int errCnt = 0;
			int orgSize = avPacket.size;

			///bool isTextSub = avStream->codec->codec_descriptor->props & AV_CODEC_PROP_TEXT_SUB;
			bool isTextSub = ccStream->CodecContext->codec_descriptor->props & AV_CODEC_PROP_TEXT_SUB;
			//==========================================
			if (isTextSub
				&& m_detectedCodePage != 0 && m_detectedCodePage != 20127 //ASCII
				&& m_detectedCodePage != CP_UTF8 && m_detectedCodePage != CP_UCS2LE && m_detectedCodePage != CP_UCS2BE) //UNICODE
			{
				WCHAR* output = NULL;
				int cchRequiredSize = 0;
				unsigned int cchActualSize = 0;

				cchRequiredSize = (int)MultiByteToWideChar(m_detectedCodePage, 0, (char*)avPacket.data, avPacket.size, output, cchRequiredSize); // determine required buffer size
				output = (WCHAR*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, (cchRequiredSize + 1) * sizeof(wchar_t)); // fix: add 1 to required size and zero memory on alloc
				cchActualSize = (int)MultiByteToWideChar(m_detectedCodePage, 0, (char*)avPacket.data, avPacket.size, output, cchRequiredSize);

				if (cchActualSize > 0)
				{
					replacedSize = WideCharToMultiByte(CP_UTF8, 0, output, -1, NULL, 0, NULL, NULL);
					if (replacedSize > 0)
					{
						replacedData = (byte*)malloc(replacedSize);
						WideCharToMultiByte(CP_UTF8, 0, output, -1, (char*)replacedData, replacedSize, NULL, NULL);

						avPacket.data = replacedData;
						avPacket.size = replacedSize;
					}
				}
				HeapFree(GetProcessHeap(), 0, output);
			}
			//==========================================
			

			Platform::Array<uint8_t>^ newData = nullptr;
			while (avPacket.size > 0)
			{
				/*
				//zlib으로 압축된 데이터의 경우 압축을 해제 (External libraries 동작으로 주석처리함 2016.07.08)
				if (avPacket.size > 2 && avPacket.data[0] == 0x78 && avPacket.data[1] == 0xDA)
				{
					//auto data = ref new Platform::Array<byte>(avPacket.size);
					//CopyMemory(data->Data, avPacket.data, avPacket.size);

					//newData = Lime::CPPHelper::Zlib::UncompressBuffer(data);

					//if (newData->Length != avPacket.size)
					//{
					//	avPacket.data = newData->Data;
					//	avPacket.size = newData->Length;
					//}

					uLong tmp = avPacket.size * 2 + 13;
					replacedData = (byte*)malloc(tmp);

					Uncompress(avPacket.data, avPacket.size, &replacedData, &tmp);

					replacedSize = (int)tmp;
					avPacket.data = replacedData;
					avPacket.size = replacedSize;
				}
				*/
				//avcodec_decode_subtitle2  => 무조건 UTF-8이어야 하기 때문에 위에서 UTF-8이 아닌 경우 무조건 UTF-8로 변경
				//int decodedBytes = avcodec_decode_subtitle2(avStream->codec, &sub, &gotSub, &avPacket);
				int decodedBytes = avcodec_decode_subtitle2(ccStream->CodecContext, &sub, &gotSub, &avPacket);

				if (decodedBytes < 0)
				{
					if (gotSub)
					{
						//현재 파서 동작 정지
						Stop();
						//디코드 에러 
						int64_t timeBase = av_q2d(m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base) * 10000000L;
						int64_t pos = (long long)avPacket.pts * timeBase;
						//SRT의 경우 첫바이트가 깨지는 현상이 생기는데...
						//이경우 이후로도 복구가 잘 안됨.. 그래서 다시 파서를 로딩
						FailedDecoding(this, pos);
						OutputDebugMessage(L"Recovery subtitle position.... %I64d 위치를 복구중입니다\n", pos);
						//__debugbreak();
					}
					break;
				}

				if (SubtitleHelper::IsSRTProcessing(avStream->codecpar->codec_id) || (SubtitleHelper::IsSAMIProcessing(avStream->codecpar->codec_id)))
				{
					Array<byte>^ imgData = nullptr;
					Windows::Foundation::Collections::IMap<String^, SubtitleImage^>^ subImgMap = nullptr;

					int64_t timeBase = av_q2d(m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base) * 10000000L;
					auto subPkt = ref new Windows::Data::Json::JsonObject();
					auto jo = ref new Windows::Data::Json::JsonObject();
					int64_t pts = avPacket.pts;
					if (pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
					{
						pts = avPacket.dts;
					}
					//subPkt->Insert("Pts", Windows::Data::Json::JsonValue::CreateNumberValue((double)(sub.pts * 10000000L / double(AV_TIME_BASE))));
					subPkt->Insert("Pts", Windows::Data::Json::JsonValue::CreateNumberValue((double)(pts * timeBase)));
					subPkt->Insert("StartDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue((double)(sub.start_display_time * timeBase)));
					//subPkt->Insert("EndDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue(sub.end_display_time));
					//auto end = sub.end_display_time;

					subPkt->Insert("EndDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue((double)(avPacket.duration * timeBase)));
					subPkt->Insert("Format", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.format));
					subPkt->Insert("Rects", ref new Windows::Data::Json::JsonArray());
					subPkt->Insert("NumRects", Windows::Data::Json::JsonValue::CreateNumberValue(1.0));
					subPkt->GetNamedArray("Rects")->Append(jo);
					
					jo->Insert("Left", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));
					jo->Insert("Top", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));
					jo->Insert("Width", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));
					jo->Insert("Height", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));
					jo->Insert("NumColors", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));
					jo->Insert("Flags", Windows::Data::Json::JsonValue::CreateNumberValue(0.0));

					GUID result;
					if (SUCCEEDED(CoCreateGuid(&result)))
					{
						String^ pName = Guid(result).ToString();
						std::wstring wsguid(pName->Data());
						pName = ref new String(wsguid.substr(1, wsguid.length() - 2).c_str());
						jo->Insert("Guid", Windows::Data::Json::JsonValue::CreateStringValue(pName));
					}

					if (SubtitleHelper::IsSRTProcessing(avStream->codecpar->codec_id))
					{
						//SRT자막
						jo->Insert("Type", Windows::Data::Json::JsonValue::CreateNumberValue((double)SubtitleType::SUBTITLE_SRT));
						jo->Insert("Ass", Windows::Data::Json::JsonValue::CreateStringValue(ToStringHat((char*)avPacket.data, CP_UTF8)));
						jo->Insert("Text", Windows::Data::Json::JsonValue::CreateStringValue(""));
					}
					else
					{
						//SMI자막
						jo->Insert("Type", Windows::Data::Json::JsonValue::CreateNumberValue((double)SubtitleType::SUBTITLE_SAMI));
						jo->Insert("Text", Windows::Data::Json::JsonValue::CreateStringValue(""));
						//?? ffmpeg 버그?
						std::string txt((char*)avPacket.data);
						txt = regex_replace(txt, linePatern, "");

						smatch m;
						if (regex_search(txt, m, syncPattern))
						{
							txt = m[2].str();
							std::tr1::sregex_token_iterator token(txt.begin(), txt.end(), pClsPattern, { -1, 1 });
							std::sregex_token_iterator end;

							bool isFirst = true;
							while (token != end)
							{
								auto val = *token;

								if (val.matched)
								{
									if (isFirst)
									{
										isFirst = false;
										String^ langCode = nullptr;
										String^ id = nullptr;

										std::string txt2(val);
										smatch sm2;
										while (regex_search(txt2, sm2, keyValPattern))
										{
											auto sKey = sm2[1].str();
											auto sVal = sm2[2].str();

											std::transform(sKey.begin(), sKey.end(), sKey.begin(), tolower);
											if (sKey == "class")
											{
												langCode = ToStringHat(sVal.c_str(), CP_UTF8);
											}
											else if (sKey == "id")
											{
												id = ToStringHat(sVal.c_str(), CP_UTF8);
											}

											txt2 = sm2.suffix();
										}
										
										jo->Insert("Lang", Windows::Data::Json::JsonValue::CreateStringValue(langCode));
										jo->Insert("Id", Windows::Data::Json::JsonValue::CreateStringValue(id));
										jo->Insert("Ass", Windows::Data::Json::JsonValue::CreateStringValue(""));
									}
									else
									{
										auto text = ToStringHat(val.str().c_str(), CP_UTF8);
										jo->SetNamedValue("Ass", Windows::Data::Json::JsonValue::CreateStringValue(text));
									}
								}
								++token;
							}
						}
						SubtitleHelper::ApplySAMIStyle(subPkt, ccStream->GlobalStyleProperty, ccStream->BlockStyleMap, ccStream->SubtitleLanguages);
					}
					ccStream->AppendPacket(subPkt);
				
					//루프 종료 조건 초기화
					errCnt = 0;
					orgSize = avPacket.size;
				}
				else
				{
					if (gotSub)
					{
						Array<byte>^ imgData = nullptr;
						Windows::Foundation::Collections::IMap<String^, SubtitleImage^>^ subImgMap = nullptr;

						int64_t timeBase = av_q2d(m_pAvFormatCtx->streams[SelectedSubtitleStreamIndex]->time_base) * 10000000L;
						auto subPkt = ref new Windows::Data::Json::JsonObject();
						int64_t pts = avPacket.pts;
						if (pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
						{
							pts = avPacket.dts;
						}
						//subPkt->Insert("Pts", Windows::Data::Json::JsonValue::CreateNumberValue((double)(sub.pts * 10000000L / double(AV_TIME_BASE))));
						subPkt->Insert("Pts", Windows::Data::Json::JsonValue::CreateNumberValue((double)(pts * timeBase)));
						subPkt->Insert("StartDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue((double)(sub.start_display_time * timeBase)));
						//subPkt->Insert("EndDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue(sub.end_display_time));
						subPkt->Insert("EndDisplayTime", Windows::Data::Json::JsonValue::CreateNumberValue((double)(avPacket.duration * timeBase)));
						subPkt->Insert("Format", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.format));
						subPkt->Insert("Rects", ref new Windows::Data::Json::JsonArray());

						//OutputDebugMessage(L"%I64d %s\n", (sub.pts * 10000000L / double(AV_TIME_BASE)), ToStringHat((char*)avPacket.data, CP_UTF8)->Data());

						/*auto ts = Windows::Foundation::TimeSpan();
						ts.Duration = (LONGLONG)(subPkt->GetNamedNumber("Pts") + (subPkt->GetNamedNumber("StartDisplayTime")));*/

						subPkt->Insert("NumRects", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.num_rects));
						bool isBitmapSubtitle = false;

						auto scriptInfoProp = SelectedSubtitleStream->GlobalStyleProperty;
						if (scriptInfoProp != nullptr && scriptInfoProp->Size > 0
							&& scriptInfoProp->HasKey("PlayResX") && scriptInfoProp->HasKey("PlayResY"))
						{
							auto prx = scriptInfoProp->Lookup("PlayResX")->ToString();
							auto pry = scriptInfoProp->Lookup("PlayResY")->ToString();

							std::string sprx(prx->Begin(), prx->End());
							std::string spry(pry->Begin(), pry->End());

							double nsw = strtod(sprx.c_str(), NULL);
							double nsh = strtod(spry.c_str(), NULL);

							subPkt->Insert("NaturalSubtitleWidth", Windows::Data::Json::JsonValue::CreateNumberValue(nsw));
							subPkt->Insert("NaturalSubtitleHeight", Windows::Data::Json::JsonValue::CreateNumberValue(nsh));
						}

						for (uint32 i = 0; i < sub.num_rects; i++)
						{
							auto jo = ref new Windows::Data::Json::JsonObject();
							jo->Insert("Left", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->x));
							jo->Insert("Top", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->y));
							jo->Insert("Width", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->w));
							jo->Insert("Height", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->h));
							jo->Insert("Flags", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->flags));
							jo->Insert("NumColors", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->nb_colors));
							jo->Insert("Type", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->type));

							GUID result;
							if (SUCCEEDED(CoCreateGuid(&result)))
							{
								String^ pName = Guid(result).ToString();
								std::wstring guid(pName->Data());
								pName = ref new String(guid.substr(1, guid.length() - 2).c_str());
								jo->Insert("Guid", Windows::Data::Json::JsonValue::CreateStringValue(pName));
							}

							if (sub.format != 0)
							{
								jo->Insert("Ass", Windows::Data::Json::JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->ass, CP_UTF8)));
								jo->Insert("Text", Windows::Data::Json::JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->text, CP_UTF8)));
								if (ccStream->BlockStyleMap != nullptr || ccStream->EventList.size() > 0)
								{
									SubtitleHelper::ApplyASSStyle(jo, ccStream->BlockStyleMap, &ccStream->EventList);
								}
							}
							else
							{
								/* Graphics subtitle */
								jo->Insert("Left", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->x));
								jo->Insert("Top", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->y));
								jo->Insert("Width", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->w));
								jo->Insert("Height", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->h));
								jo->Insert("NumColors", Windows::Data::Json::JsonValue::CreateNumberValue((double)sub.rects[i]->nb_colors));
								if (sub.rects[i]->data[0] != nullptr)
								{
									if (subImgMap == nullptr)
									{
										subImgMap = ref new Platform::Collections::Map<String^, SubtitleImage^>();
									}

									int size = sub.rects[i]->w * sub.rects[i]->h;
									imgData = ref new Array<byte>(size * 4);

									for (int j = 0; j < size; j++)
									{
										byte cc = sub.rects[i]->data[0][j];
										int ii = j * 4;
										int ci = cc * 4;
										imgData[ii] = sub.rects[i]->data[1][ci];
										imgData[ii + 1] = sub.rects[i]->data[1][ci + 1];
										imgData[ii + 2] = sub.rects[i]->data[1][ci + 2];
										imgData[ii + 3] = sub.rects[i]->data[1][ci + 3];
									}

									String^ guid = jo->GetNamedString("Guid");
									SubtitleImage^ subImg = ref new SubtitleImage();
									subImg->ImagePixelData = imgData;
									subImgMap->Insert(guid, subImg);
								}
							}
							subPkt->GetNamedArray("Rects")->Append(jo);
						}

						ccStream->AppendPacket(subPkt);
						//루프 종료 조건 초기화
						errCnt = 0;
						orgSize = avPacket.size;
					}
					else
					{
						errCnt++;
					}
				}

				*avPacket.data += decodedBytes;
				avPacket.size -= decodedBytes;

				avsubtitle_free(&sub);
				//sub = NULL;

				if (orgSize == avPacket.size && errCnt > 5 || orgSize < avPacket.size)
				{
					break;
				}
			}

			if (avPacket.stream_index == SelectedSubtitleStreamIndex)
			{
				decodeSuccess = true;
			}

			av_packet_unref(&avPacket);
			if (replacedSize > 0 && replacedData != nullptr)
			{
				free(replacedData);
				replacedData = NULL;
				replacedSize = 0;
			}
		}
	}
	catch (Exception^ ade)
	{
		DebugMessage(ade->Message->Data());
		//hr = E_FAIL;
	}
	return hr;
}

void ExternalSubtitleParser::CloseFFmpegContext()
{
	if (m_pAvIOCtx)
	{
		av_freep(&m_pAvIOCtx->buffer);
		av_freep(&m_pAvIOCtx);
	}

	if (m_pAvFormatCtx)
	{
		avformat_close_input(&m_pAvFormatCtx);
	}

	//for (unsigned int i = 0; i < m_subtitleAVStreamList.size(); i++)
	//{
	//	auto stream = m_subtitleAVStreamList.at(i);
	//avcodec_close(stream->codec);
	//}
	m_subtitleAVStreamList.clear();
	_SubtitleStreams->Clear();
	_SubLanguages->Clear();

	m_orgStream = nullptr;

	//메모리 릭 제거... (원인 불명..)
	auto stream = reinterpret_cast<IUnknown*>(m_pFileStreamData);
	while (stream->Release() > 0);
}

// Static function to read file stream and pass data to FFmpeg. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize)
{
	//return bytesRead;
	IStream* pStream = reinterpret_cast<IStream*>(ptr);
	ULONG bytesRead = 0;
	HRESULT hr = pStream->Read(buf, bufSize, &bytesRead);

	if (FAILED(hr))
	{
		return -1;
	}

	// If we succeed but don't have any bytes, assume end of file
	if (bytesRead == 0)
	{
		return AVERROR_EOF;  // Let FFmpeg know that we have reached eof
	}

	return bytesRead;

}

// Static function to seek in file stream. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence)
{
	IStream* pStream = reinterpret_cast<IStream*>(ptr);
	LARGE_INTEGER in;
	in.QuadPart = pos;
	ULARGE_INTEGER out = { 0 };

	if (FAILED(pStream->Seek(in, whence, &out)))
	{
		return -1;
	}

	return out.QuadPart; // Return the new position:
}
