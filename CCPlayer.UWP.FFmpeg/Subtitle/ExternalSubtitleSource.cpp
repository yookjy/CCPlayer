//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#include "pch.h"
#include "shcore.h"
#include "ExternalSubtitleSource.h"
#include "Source\Provider\\Subtitle\ASSSampleProvider.h"
#include "Source\Provider\\Subtitle\PGSSampleProvider.h"
#include "Source\Provider\\Subtitle\SAMISampleProvider.h"
#include "Source\Provider\\Subtitle\SRTSampleProvider.h"
#include "Source\Provider\\Subtitle\SubtitleProvider.h"
#include "Source\Provider\\Subtitle\XSUBSampleProvider.h"
#include "Common\FFmpegMacro.h"

//#pragma comment (lib, "iconv.lib")
//#include <iconv.h>

extern "C"
{
#include <libavutil/imgutils.h>
}

using namespace concurrency;
using namespace CCPlayer::UWP::Common::Interface;
using namespace CCPlayer::UWP::FFmpeg::Subtitle;
using namespace Platform;
using namespace Windows::Storage::Streams;
using namespace Windows::Media::MediaProperties;

// Size of the buffer when reading a stream
const int FILESTREAMBUFFERSZ = 16384;

// Static functions passed to FFmpeg for stream interop
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize);
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence);

// Initialize an FFmpegInteropObject
ExternalSubtitleSource::ExternalSubtitleSource()
	: avDict(nullptr)
	, avIOCtx(nullptr)
	, avFormatCtx(nullptr)
	, subtitleReader(nullptr)
	, defaultStreamIndex(AVERROR_STREAM_NOT_FOUND)
	, fileStreamData(nullptr)
	, fileStreamBuffer(nullptr)
	, orgStream(nullptr)
	, selectedSubLanguageCode(nullptr)
	, hasHtmlTag(false)
	, detectedCodePage(AUTO_DETECT_CODE_PAGE)
{
	av_register_all();
}

ExternalSubtitleSource::~ExternalSubtitleSource()
{
	// Clear our data
	delete subtitleReader;
	subtitleReader = NULL;

	if (avIOCtx)
	{
		av_freep(&avIOCtx->buffer);
		av_freep(&avIOCtx);
	}

	if (avFormatCtx)
	{
		avformat_close_input(&avFormatCtx);
	}

	av_dict_free(&avDict);

	while (!avCodecCtxList.empty())
	{
		auto ctx = avCodecCtxList.back();
		avCodecCtxList.pop_back();
		//avcodec_close(ctx);
		avcodec_free_context(&ctx);
	}
	//스트림 반환
	orgStream = nullptr;

	//메모리 릭 제거... (원인 불명..)
	auto stream = reinterpret_cast<IUnknown*>(fileStreamData);
	if (stream != nullptr)
	{
		while (stream->Release() > 0);
	}
}

Windows::Foundation::Collections::IVector<SubtitleLanguage^>^ ExternalSubtitleSource::SubtitleLanguages::get()
{
	if (subtitleReader != NULL)
	{
		for (int i = 0; i < subtitleReader->GetProviderList().size(); i++)
		{
			if (defaultStreamIndex == subtitleReader->GetProviderList().at(i)->GetCurrentStreamIndex())
			{
				SAMISampleProvider* samiProvider = dynamic_cast<SAMISampleProvider*>(subtitleReader->GetProviderList().at(i));
				if (samiProvider != NULL)
				{
					return samiProvider->GetSubtitleLanguages();
				}
			}
		}
	}
	return nullptr;
}

String^ ExternalSubtitleSource::SelectedSubLanguageCode::get()
{
	return selectedSubLanguageCode;
}

void ExternalSubtitleSource::SelectedSubLanguageCode::set(String^ value)
{
	selectedSubLanguageCode = value;
}

void ExternalSubtitleSource::DetectCodePage()
{
	if (subtitleDecoderConnector->SelectedCodePage == AUTO_DETECT_CODE_PAGE)
	{
		//자동 검색
		CDetectCodepage mlang;
		int confidence = 0;

		if (mlang.Init(hasHtmlTag ? MLDETECTCP_HTML : MLDETECTCP_NONE))
		{
			if (fileStreamData != nullptr)
			{
				//파일의 경우
				detectedCodePage = mlang.DetectCodepage(fileStreamData);
			}
			else
			{
				//스트리밍의 경우
				if (avFormatCtx->pb->buffer_size > 10)
				{
					detectedCodePage = mlang.DetectCodepage((char*)avFormatCtx->pb->buffer, avFormatCtx->pb->buffer_size);
				}
			}
		}

		//정확도
		confidence = mlang.GetConfidence();
		if (confidence <= 95)
		{
			if (fileStreamData != nullptr)
			{
				//2차 검색
				PropertySet^ propSet = Lime::CPPHelper::EncodingHelper::CharsetDetectorFromStream(orgStream);

				if (propSet->Size > 0 && propSet->HasKey("IsFound") && static_cast<bool>(propSet->Lookup("IsFound")))
				{
					if (propSet->HasKey("Confidence") && static_cast<int>(propSet->Lookup("Confidence")) >= confidence)
					{
						detectedCodePage = static_cast<int>(propSet->Lookup("CodePage"));
					}
				}
			}
			else
			{
				__debugbreak();
			}
		}

		//검색되지 않은 경우 UTF8 강제 설정
		if (detectedCodePage == AUTO_DETECT_CODE_PAGE)
		{
			detectedCodePage = subtitleDecoderConnector->DefaultCodePage;
		}
	}
}

ExternalSubtitleSource^ ExternalSubtitleSource::CreateExternalSubtitleSourceFromStream(ISubtitleDecoderConnector^ subtitleDecoderConnector, IRandomAccessStream^ stream, PropertySet^ ffmpegOptions)
{
	auto subtitleSource = ref new ExternalSubtitleSource();
	if (FAILED(subtitleSource->CreateExternalSubtitleSource(subtitleDecoderConnector, stream, ffmpegOptions)))
	{
		// We failed to initialize, clear the variable to return failure
		subtitleSource = nullptr;
	}

	return subtitleSource;
}

ExternalSubtitleSource^ ExternalSubtitleSource::CreateExternalSubtitleSourceFromStream(ISubtitleDecoderConnector^ subtitleDecoderConnector, IRandomAccessStream^ stream)
{
	return CreateExternalSubtitleSourceFromStream(subtitleDecoderConnector, stream, nullptr);
}

ExternalSubtitleSource^ ExternalSubtitleSource::CreateExternalSubtitleSourceFromUri(ISubtitleDecoderConnector^ subtitleDecoderConnector, String^ uri, PropertySet^ ffmpegOptions)
{
	auto subtitleSource = ref new ExternalSubtitleSource();
	if (FAILED(subtitleSource->CreateExternalSubtitleSource(subtitleDecoderConnector, uri, ffmpegOptions)))
	{
		// We failed to initialize, clear the variable to return failure
		subtitleSource = nullptr;
	}

	return subtitleSource;
}

ExternalSubtitleSource^ ExternalSubtitleSource::CreateExternalSubtitleSourceFromUri(ISubtitleDecoderConnector^ subtitleDecoderConnector, String^ uri)
{
	return CreateExternalSubtitleSourceFromUri(subtitleDecoderConnector, uri, nullptr);
}

HRESULT ExternalSubtitleSource::CreateExternalSubtitleSource(ISubtitleDecoderConnector^ subtitleDecoderConnector, String^ uri, PropertySet^ ffmpegOptions)
{
	HRESULT hr = S_OK;
	const char* charStr = nullptr;
	int codepage = 0;

	if (!uri)
	{
		hr = E_INVALIDARG;
	}

	if (SUCCEEDED(hr))
	{
		avFormatCtx = avformat_alloc_context();
		if (avFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	PropertySet^ backup = ref new PropertySet();
	if (ffmpegOptions != nullptr && ffmpegOptions->HasKey("codepage"))
	{
		codepage = static_cast<int>(ffmpegOptions->Lookup("codepage"));
		ffmpegOptions->Remove("codepage"); 
		backup->Insert("codepage", codepage);
	}

	if (SUCCEEDED(hr))
	{
		// Populate AVDictionary avDict based on PropertySet ffmpegOptions. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
		hr = ParseOptions(ffmpegOptions);
	}

	if (ffmpegOptions != nullptr && backup->Size > 0)
	{
		if (backup->HasKey("codepage"))
		{
			ffmpegOptions->Insert("codepage", backup->Lookup("codepage"));
		}

		backup = nullptr;
	}

	if (SUCCEEDED(hr))
	{
		std::wstring uriW(uri->Begin());
		std::string uriA(uriW.begin(), uriW.end());
		charStr = uriA.c_str();

		if (codepage > 0)
		{
			charStr = (char*)GetStringBytes(uri, codepage)->Data;
		}

		// Open media in the given URI using the specified options
		if (avformat_open_input(&avFormatCtx, charStr, NULL, &avDict) < 0)
		{
			hr = E_FAIL; // Error opening file
		}

		// avDict is not NULL only when there is an issue with the given ffmpegOptions such as invalid key, value type etc. Iterate through it to see which one is causing the issue.
		if (avDict != nullptr)
		{
			DebugMessage(L"Invalid FFmpeg option(s)");
			av_dict_free(&avDict);
			avDict = nullptr;
		}
	}

	this->subtitleDecoderConnector = subtitleDecoderConnector;

	if (SUCCEEDED(hr))
	{
		DetectCodePage();
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

HRESULT ExternalSubtitleSource::CreateExternalSubtitleSource(ISubtitleDecoderConnector^ subtitleDecoderConnector, IRandomAccessStream^ stream, PropertySet^ ffmpegOptions)
{
	HRESULT hr = S_OK;
	if (!stream)
	{
		hr = E_INVALIDARG;
	}
	
	if (SUCCEEDED(hr))
	{
		// Convert asynchronous IRandomAccessStream to synchronous IStream. This API requires shcore.h and shcore.lib
		hr = CreateStreamOverRandomAccessStream(reinterpret_cast<IUnknown*>(stream), IID_PPV_ARGS(&fileStreamData));
		orgStream = stream;
	}

	if (SUCCEEDED(hr))
	{
		// Setup FFmpeg custom IO to access file as stream. This is necessary when accessing any file outside of app installation directory and appdata folder.
		// Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
		fileStreamBuffer = (unsigned char*)av_malloc(FILESTREAMBUFFERSZ);
		if (fileStreamBuffer == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		avIOCtx = avio_alloc_context(fileStreamBuffer, FILESTREAMBUFFERSZ, 0, fileStreamData, FileStreamRead, 0, FileStreamSeek);
		if (avIOCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		avFormatCtx = avformat_alloc_context();
		if (avFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		// Populate AVDictionary avDict based on PropertySet ffmpegOptions. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
		hr = ParseOptions(ffmpegOptions);
	}

	if (SUCCEEDED(hr))
	{
		avFormatCtx->pb = avIOCtx;
		avFormatCtx->flags |= AVFMT_FLAG_CUSTOM_IO;

		// Open media file using custom IO setup above instead of using file name. Opening a file using file name will invoke fopen C API call that only have
		// access within the app installation directory and appdata folder. Custom IO allows access to file selected using FilePicker dialog.
		if (avformat_open_input(&avFormatCtx, "", NULL, &avDict) < 0)
		{
			hr = E_FAIL; // Error opening file
		}

		// avDict is not NULL only when there is an issue with the given ffmpegOptions such as invalid key, value type etc. Iterate through it to see which one is causing the issue.
		if (avDict != nullptr)
		{
			DebugMessage(L"Invalid FFmpeg option(s)");
			av_dict_free(&avDict);
			avDict = nullptr;
		}
	}

	this->subtitleDecoderConnector = subtitleDecoderConnector;

	if (SUCCEEDED(hr))
	{
		DetectCodePage();
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

HRESULT ExternalSubtitleSource::InitFFmpegContext()
{
	HRESULT hr = S_OK;

	if (SUCCEEDED(hr))
	{
		if (avformat_find_stream_info(avFormatCtx, NULL) < 0)
		{
			hr = E_FAIL; // Error finding info
		}
	}
	
	if (SUCCEEDED(hr))
	{
		subtitleReader = new ExternalSubtitleReader(avFormatCtx, subtitleDecoderConnector);
		if (subtitleReader == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		MFSampleProvider* sampleProvider = nullptr;

		for (unsigned int i = 0; i < avFormatCtx->nb_streams; i++)
		{
			AVStream* stream = avFormatCtx->streams[i];
			
			//프로바이더 초기화
			sampleProvider = nullptr;
			AVCodec* avCodec = nullptr;
			AVCodec* avSubtitleCodec = nullptr;

			switch (GET_CODEC_CTX_PARAM(stream, codec_type))
			{
			case AVMediaType::AVMEDIA_TYPE_SUBTITLE:
				// find the audio stream and its decoder
				avSubtitleCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(stream, codec_id));
				if (avSubtitleCodec)
				{
					AVCodecContext* pAvSubtitleCodecCtx = avcodec_alloc_context3(avSubtitleCodec);
					if (pAvSubtitleCodecCtx)
					{
						if (FILL_CODEC_CTX(pAvSubtitleCodecCtx, stream) != 0) {
							OutputDebugMessage(L"Couldn't set external subtitle codec context\n");
						}
					}
					if (avcodec_open2(pAvSubtitleCodecCtx, avSubtitleCodec, NULL) < 0)
					{
						avcodec_free_context(&pAvSubtitleCodecCtx);
						pAvSubtitleCodecCtx = NULL;
						//hr = E_FAIL;
					}
					else
					{
						SubtitleProvider* subProvider = NULL;

						switch (pAvSubtitleCodecCtx->codec_id)
						{
						case AV_CODEC_ID_SAMI:
							subProvider = new SAMISampleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							hasHtmlTag = true;
							break;
						case AV_CODEC_ID_SRT:
						case AV_CODEC_ID_SUBRIP:
							subProvider = new SRTSampleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							hasHtmlTag = true;
							break;
						case AV_CODEC_ID_ASS:
						case AV_CODEC_ID_SSA:
						case AV_CODEC_ID_MOV_TEXT:
							subProvider = new ASSSampleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							break;
						case AV_CODEC_ID_HDMV_PGS_SUBTITLE:
							subProvider = new PGSSampleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							break;
						case AV_CODEC_ID_XSUB:
							subProvider = new XSUBSampleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							break;
						case AV_CODEC_ID_FIRST_SUBTITLE:
							break;
						default:
							subProvider = new SubtitleProvider(subtitleReader, avFormatCtx, pAvSubtitleCodecCtx, stream->index, detectedCodePage);
							break;
						}

						if (subProvider != NULL)
						{
							subProvider->LoadHeaderIfCodePageChanged();
							subtitleReader->AddStream(subProvider);

							//코덱 정보 목록에 추가
							if (defaultStreamIndex == AVERROR_STREAM_NOT_FOUND)
							{
								defaultStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, -1, NULL, 0);
							}

							if (defaultStreamIndex == stream->index)
							{
								subProvider->CodecInfo->IsBestStream = true;
							}
						}
					}
				}
				break;
			case AVMediaType::AVMEDIA_TYPE_VIDEO:
			case AVMediaType::AVMEDIA_TYPE_AUDIO:
			case AVMediaType::AVMEDIA_TYPE_ATTACHMENT:
			default:
				// We validate the stream type before calling this method.
				//assert(IsStreamTypeSupported(packetHdr.type));
				//assert(false);
				//ThrowException(E_UNEXPECTED);
				continue;
			}
		}
	}

	return hr;
}

HRESULT ExternalSubtitleSource::ParseOptions(PropertySet^ ffmpegOptions)
{
	HRESULT hr = S_OK;

	// Convert FFmpeg options given in PropertySet to AVDictionary. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
	if (ffmpegOptions != nullptr)
	{
		auto options = ffmpegOptions->First();

		while (options->HasCurrent)
		{
			String^ key = options->Current->Key;
			std::wstring keyW(key->Begin());
			std::string keyA(keyW.begin(), keyW.end());
			const char* keyChar = keyA.c_str();

			// Convert value from Object^ to const char*. avformat_open_input will internally convert value from const char* to the correct type
			String^ value = options->Current->Value->ToString();
			std::wstring valueW(value->Begin());
			std::string valueA(valueW.begin(), valueW.end());
			const char* valueChar = valueA.c_str();

			// Add key and value pair entry
			if (av_dict_set(&avDict, keyChar, valueChar, 0) < 0)
			{
				hr = E_INVALIDARG;
				break;
			}

			options->MoveNext();
		}
	}

	return hr;
}

void ExternalSubtitleSource::ConsumePacket(int index, int64_t pts)
{
	if (subtitleReader == NULL)
		return;

	auto plist = subtitleReader->GetProviderList();
	if (plist.size() > index && index != -1)
	{
		SubtitleProvider* provider = dynamic_cast<SubtitleProvider*>(plist.at(index));
		HRESULT hr = S_OK;
		if ((hr = provider->FillPacketQueue()) != EOF && hr != E_FAIL)
		{
			//int64_t subPktTime = (int64_t)(packet->GetNamedNumber("Pts") + (packet->GetNamedNumber("StartDisplayTime")) + SynchronizeTime);
			int64_t subPktTime = provider->GetPts(provider->GetPacketPts()) + SynchronizeTime;
			if (pts >= subPktTime)
			{
				provider->ConsumePacket(index, pts, SynchronizeTime);
			}
		}
	}
}

bool ExternalSubtitleSource::Seek(int64_t time, int flags)
{
	if (avFormatCtx == NULL || IsSeeking) 
		return false;

	time += SynchronizeTime;

	if (time < 0)
		time = 0;

	bool result = false;
	// Convert TimeSpan unit to AV_TIME_BASE
	int64_t timestamp = av_rescale_q(time, { 1, 10000000L }, avFormatCtx->streams[defaultStreamIndex]->time_base);
	int64_t min_ts = INT64_MIN, max_ts = INT64_MAX;

	int seeked = 0;
	if (avFormatCtx->iformat->read_seek2 && !avFormatCtx->iformat->read_seek)
		seeked = avformat_seek_file(avFormatCtx, defaultStreamIndex, min_ts, timestamp, max_ts, flags);
	else
		seeked = av_seek_frame(avFormatCtx, defaultStreamIndex, timestamp, flags);

	if (seeked >= 0) //AVERROR(ERANGE); // -34 ? -31?
		subtitleReader->Flush(); //버퍼 플러싀
	else
		DebugMessage(L"\nSeeking Failed!\n");
	
	return result;
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