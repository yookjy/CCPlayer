//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#include "pch.h"
#include "MediaInformation.h"
#include "ThumbnailReader.h"
#include "shcore.h"
#include "Common\FFmpegMacro.h"

extern "C"
{
#include <libavutil/imgutils.h>
}

using namespace concurrency;
using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::FFmpeg::Information;
using namespace Platform;
using namespace Windows::Storage::Streams;
using namespace Windows::Media::MediaProperties;

// Size of the buffer when reading a stream
const int FILESTREAMBUFFERSZ = 16384;

// Static functions passed to FFmpeg for stream interop
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize);
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence);

// Initialize an FFmpegInteropObject
MediaInformation::MediaInformation()
	: avDict(nullptr)
	, avIOCtx(nullptr)
	, avFormatCtx(nullptr)
	, defaultAudioStreamIndex(AVERROR_STREAM_NOT_FOUND)
	, defaultVideoStreamIndex(AVERROR_STREAM_NOT_FOUND)
	, defaultSubtitleStreamIndex(AVERROR_STREAM_NOT_FOUND)
	, fileStreamData(nullptr)
	, fileStreamBuffer(nullptr)
{
	av_register_all();
	
	_CodecInformationList = ref new Platform::Collections::Vector<CodecInformation^>;
}

MediaInformation::~MediaInformation()
{
	// Clear our data
	thumbnailReader = nullptr;

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

	//메모리 릭 제거... (원인 불명..)
	if (fileStreamData != NULL)
	{
		auto stream = reinterpret_cast<IUnknown*>(fileStreamData);
		if (stream != NULL)
			while (stream->Release() > 0);
	}
}

Windows::Foundation::Collections::IVectorView<CodecInformation^>^ MediaInformation::CodecInformationList::get()
{
	return _CodecInformationList->GetView();
}

int MediaInformation::DefaultVideoStreamIndex::get()
{
	return defaultVideoStreamIndex;
}

int MediaInformation::DefaultAudioStreamIndex::get()
{
	return defaultAudioStreamIndex;
}

int MediaInformation::DefaultSubtitleStreamIndex::get()
{
	return defaultSubtitleStreamIndex;
}

TimeSpan MediaInformation::NaturalDuration::get()
{
	return mediaDuration;
}

DecoderTypes MediaInformation::RecommendedDecoderType::get()
{
	if (recommendedDecoderType == DecoderTypes::AUTO)
	{
		recommendedDecoderType = GetRecommendedDecoderType(DefaultVideoStreamIndex, DefaultAudioStreamIndex);
	}
	return recommendedDecoderType;
}

void MediaInformation::RecommendedDecoderType::set(DecoderTypes value)
{
	if (recommendedDecoderType != value)
	{
		recommendedDecoderType = value;
	}
}

MediaInformation^ MediaInformation::CreateMediaInformationFromStream(IRandomAccessStream^ stream, PropertySet^ ffmpegOptions)
{
	auto mediaInformation = ref new MediaInformation();
	if (FAILED(mediaInformation->CreateMediaInformation(stream, ffmpegOptions)))
	{
		// We failed to initialize, clear the variable to return failure
		mediaInformation = nullptr;
	}

	return mediaInformation;
}

MediaInformation^ MediaInformation::CreateMediaInformationFromStream(IRandomAccessStream^ stream)
{
	return CreateMediaInformationFromStream(stream, nullptr);
}

MediaInformation^ MediaInformation::CreateMediaInformationFromUri(String^ uri, PropertySet^ ffmpegOptions)
{
	auto mediaInformation = ref new MediaInformation();
	if (FAILED(mediaInformation->CreateMediaInformation(uri, ffmpegOptions)))
	{
		// We failed to initialize, clear the variable to return failure
		mediaInformation = nullptr;
	}

	return mediaInformation;
}

MediaInformation^ MediaInformation::CreateMediaInformationFromUri(String^ uri)
{
	return CreateMediaInformationFromUri(uri, nullptr);
}

HRESULT MediaInformation::CreateMediaInformation(String^ uri, PropertySet^ ffmpegOptions)
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

		//charStr = "https://s3.amazonaws.com/x265.org/video/Tears_400_x264.mp4";

		/*char buf[1024];
		const char* input_buf_ptr = uriA.c_str();
		char* output_buf_ptr = buf;
		size_t in_size = uriA.size();
		size_t out_size;
		
		iconv_t it = iconv_open("EUC-KR", "UTF-8");
		int ret = iconv(it, &input_buf_ptr, &in_size, &output_buf_ptr, &out_size);
		iconv_close(it);*/

		// Open media in the given URI using the specified options
		if (avformat_open_input(&avFormatCtx, charStr, NULL, &avDict) < 0)
		{
			hr = E_FAIL; // Error opening file
		}

		// avDict is not NULL only when there is an issue with the given ffmpegOptions such as invalid key, value type etc. Iterate through it to see which one is causing the issue.
		if (avDict != nullptr)
		{
			DebugMessage(L"Free FFmpeg option(s)\n");
			av_dict_free(&avDict);
			avDict = nullptr;
		}
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

HRESULT MediaInformation::CreateMediaInformation(IRandomAccessStream^ stream, PropertySet^ ffmpegOptions)
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
			DebugMessage(L"Free FFmpeg option(s)\n");
			av_dict_free(&avDict);
			avDict = nullptr;
		}
	}

	if (SUCCEEDED(hr))
	{
		hr = InitFFmpegContext();
	}

	return hr;
}

HRESULT MediaInformation::InitFFmpegContext()
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
		//컨테이너 명칭
		std::string formatName(avFormatCtx->iformat->name);
		ContainerName = ref new String(std::wstring(formatName.begin(), formatName.end()).c_str());

		std::string formatLongName(avFormatCtx->iformat->long_name);
		ContainerFullName = ref new String(std::wstring(formatLongName.begin(), formatLongName.end()).c_str());

		// Convert media duration from AV_TIME_BASE to TimeSpan unit
		mediaDuration = { LONGLONG(avFormatCtx->duration * 10000000 / double(AV_TIME_BASE)) };
	}

	if (SUCCEEDED(hr))
	{
		//기본 비디오 스트림 인덱스
		if (defaultVideoStreamIndex == AVERROR_STREAM_NOT_FOUND)
		{
			defaultVideoStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_VIDEO, -1, -1, NULL, 0);
		}
		//기본 오디오 스트림 인덱스
		if (defaultAudioStreamIndex == AVERROR_STREAM_NOT_FOUND)
		{
			defaultAudioStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_AUDIO, -1, -1, NULL, 0);
		}
		if (defaultSubtitleStreamIndex == AVERROR_STREAM_NOT_FOUND)
		{
			defaultSubtitleStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, -1, NULL, 0);
		}

		for (unsigned int i = 0; i < avFormatCtx->nb_streams; i++)
		{
			AVStream* stream = avFormatCtx->streams[i];
			// find the stream and its decoder
			AVCodec* pAvCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(stream, codec_id));
			
			if (pAvCodec)
			{
				AVCodecContext* pAvCodecCtx = avcodec_alloc_context3(pAvCodec);
				if (pAvCodecCtx)
				{
					if (FILL_CODEC_CTX(pAvCodecCtx, stream) != 0) {
						OutputDebugMessage(L"MediaInforamtion : Couldn't set codec context\n");
					}
				}
				if (avcodec_open2(pAvCodecCtx, pAvCodec, NULL) < 0)
				{
					avcodec_free_context(&pAvCodecCtx);
					pAvCodecCtx = nullptr;
					hr = E_FAIL;
				}
				else
				{
					//코덱컨텍스트 저장
					avCodecCtxList.push_back(pAvCodecCtx);
					//기본 비디오 컨텍스트의 경우 썸네일리더 생성
					if (defaultVideoStreamIndex == i)
					{
						thumbnailReader = ref new ThumbnailReader(avFormatCtx, pAvCodecCtx, defaultVideoStreamIndex);
					}

					if (pAvCodecCtx->codec_id == AV_CODEC_ID_FIRST_SUBTITLE)
					{
						continue;
					}

					CodecInformation^ codecInfo = ref new CodecInformation();
					_CodecInformationList->Append(codecInfo);

					//코덱 기본 정보
					codecInfo->StreamId = stream->index;
					codecInfo->CodecId = pAvCodecCtx->codec_id;
					codecInfo->CodecType = pAvCodecCtx->codec_type;
					//코덱명 대문자 변환
					std::string cCodecName = std::string(pAvCodecCtx->codec->name);
					std::transform(cCodecName.begin(), cCodecName.end(), cCodecName.begin(), toupper);
					codecInfo->CodecName = ToStringHat(cCodecName.c_str());
					codecInfo->CodecLongName = ToStringHat(pAvCodecCtx->codec->long_name);
					codecInfo->CodecTag = pAvCodecCtx->codec_tag;
					
					if (pAvCodecCtx->codec->profiles != nullptr)
					{
						codecInfo->CodecProfileId = pAvCodecCtx->codec->profiles->profile;
						codecInfo->CodecProfileName = ToStringHat(pAvCodecCtx->codec->profiles->name);
					}

					AVDictionary* dict = stream->metadata;
					AVDictionaryEntry *t = NULL;
					//타이틀 및 언어 조회
					while (t = av_dict_get(dict, "", t, AV_DICT_IGNORE_SUFFIX))
					{
						auto key = t->key;
						auto val = t->value;

						if (strcmp(key, "language") == 0)
						{
							codecInfo->Language = ToStringHat(val);
						}
						else if (strcmp(key, "title") == 0)
						{
							codecInfo->Title = ToStringHat(val);
						}
					}

					AVPixelFormat fmt = pAvCodecCtx->pix_fmt;
					int num = 0, den = 0;
					switch (pAvCodecCtx->codec_type)
					{
					case AVMediaType::AVMEDIA_TYPE_VIDEO:
						//Video FPS
						if (pAvCodecCtx->framerate.num != 0 && pAvCodecCtx->framerate.den != 1)
						{
							num = pAvCodecCtx->framerate.num;
							den = pAvCodecCtx->framerate.den;
						}
						else if (stream->avg_frame_rate.num != 0 && stream->avg_frame_rate.den != 0)
						{
							num = stream->avg_frame_rate.num;
							den = stream->avg_frame_rate.den;
						}
						else if (stream->r_frame_rate.num != 0 && stream->r_frame_rate.den != 0)
						{
							num = stream->r_frame_rate.num;
							den = stream->r_frame_rate.den;
						}
						codecInfo->IsBestStream = defaultVideoStreamIndex == stream->index;
						codecInfo->Fps = (UINT32)ceil((double)num / den);
						//detect image size
						codecInfo->Width = GET_CODEC_CTX_PARAM(stream, width);
						codecInfo->Height = GET_CODEC_CTX_PARAM(stream, height);
						//detect 10Bit video color depth
						codecInfo->Is10BitVideoColor = fmt == AV_PIX_FMT_YUV420P10 || fmt == AV_PIX_FMT_YUV420P10BE || fmt == AV_PIX_FMT_YUV420P10LE;
						break;
					case AVMediaType::AVMEDIA_TYPE_AUDIO:
						codecInfo->IsBestStream = defaultAudioStreamIndex == stream->index;
						codecInfo->Channels = GET_CODEC_CTX_PARAM(stream, channels);
						codecInfo->SampleRate = GET_CODEC_CTX_PARAM(stream, sample_rate);
						codecInfo->Bps = GET_CODEC_CTX_PARAM(stream, bits_per_coded_sample) ? GET_CODEC_CTX_PARAM(stream, bits_per_coded_sample) : 16;
						break;
					case AVMediaType::AVMEDIA_TYPE_SUBTITLE:
						break;
					case AVMediaType::AVMEDIA_TYPE_ATTACHMENT:
						break;
					default:
						break;
					}

					//detect h/w acceleration
					switch (pAvCodecCtx->codec_id)
					{
						//비디오
					case AV_CODEC_ID_H264:
					case AV_CODEC_ID_H263:
					case AV_CODEC_ID_MPEG4:
					case AV_CODEC_ID_WMV3:
					case AV_CODEC_ID_VC1:
						//오디오
					case AV_CODEC_ID_AAC:
					case AV_CODEC_ID_MP3:
					case AV_CODEC_ID_WMALOSSLESS:
					case AV_CODEC_ID_WMAPRO:
					case AV_CODEC_ID_WMAV1:
					case AV_CODEC_ID_WMAV2:
						codecInfo->IsHWAcceleration = true;
						break;
					}
				}
			}
		}
	}

	return hr;
}

HRESULT MediaInformation::ParseOptions(PropertySet^ ffmpegOptions)
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

IAsyncOperation<IBuffer^>^ MediaInformation::GetBitmapPixelBuffer(Size size)
{
	return create_async([this, size]()
	{
		IBuffer^ pixelBuffer = nullptr;
		Array<byte>^ buffer = GetThumbnailPixelBytes(size);

		if (buffer != nullptr)
		{
			DataWriter^ dw = ref new DataWriter();
			dw->WriteBytes(buffer);
			pixelBuffer = dw->DetachBuffer();
		}

		return pixelBuffer;
	});
}

Array<byte>^ MediaInformation::GetThumbnailPixelBytes(Size size)
{
	Array<byte>^ buffer = nullptr;
	if (defaultVideoStreamIndex != AVERROR_STREAM_NOT_FOUND)
	{
		auto duration = avFormatCtx->duration;
		int64_t target = duration * 0.04;
		AVRational timeBase = avFormatCtx->streams[defaultVideoStreamIndex]->time_base;
		AVRational timeBaseQ = { 1, AV_TIME_BASE };
		/*AVRational timeBaseQ = AVRational();
		timeBaseQ.num = 1;
		timeBaseQ.den = AV_TIME_BASE;*/
		int64_t seekTarget = av_rescale_q(target, timeBaseQ, timeBase);
		auto ret = av_seek_frame(avFormatCtx, defaultVideoStreamIndex, seekTarget, 0);

		if (thumbnailReader)
		{
			if (ret >= 0)
			{
				for (int i = 0; i < avCodecCtxList.size(); i++)
				{
					avcodec_flush_buffers(avCodecCtxList.at(i));
				}
			}

			buffer = thumbnailReader->GetBitmapData(size);
		}
	}

	return buffer;
}

DecoderTypes MediaInformation::GetRecommendedDecoderType(int videoStreamIndex, int audioStreamIndex)
{
	DecoderTypes videoDecoderType = DecoderTypes::AUTO;
	DecoderTypes audioDecoderType = DecoderTypes::AUTO;
	bool hasSubtitle = false;

	if (CodecInformationList->Size == 0)
	{
		throw ref new Exception(-1, "Does not Initialized");
	}

	std::wstring containerName(ContainerName->Data());

	if (containerName.find(L"mpeg") != std::string::npos)
	{
		return DecoderTypes::SW;
	}

	for (unsigned int i = 0; i < CodecInformationList->Size; i++)
	{
		CodecInformation^ ci = CodecInformationList->GetAt(i);
		if (ci->StreamId == videoStreamIndex)
		{
			if (ci->IsHWAcceleration)
			{
				videoDecoderType = DecoderTypes::HW;
			}
			else
			{
				videoDecoderType = DecoderTypes::SW;
			}

			if (ci->Is10BitVideoColor)
			{
				videoDecoderType = DecoderTypes::SW;
			}
		}
		else if (ci->StreamId == audioStreamIndex)
		{
			if (ci->IsHWAcceleration)
			{
				audioDecoderType = DecoderTypes::HW;
			}
			else
			{
				audioDecoderType = DecoderTypes::SW;
			}
		}
		else if (ci->CodecType == 3) //AVMEDIA_TYPE_SUBTITLE
		{
			hasSubtitle = true;
		}
	}

	if (videoDecoderType == DecoderTypes::SW
		|| audioDecoderType == DecoderTypes::SW)
	{
		return DecoderTypes::SW;
	}
	else
	{
		if (hasSubtitle
			|| containerName.find(L"flv") != std::string::npos
			|| containerName.find(L"rm") != std::string::npos)
		{
			return DecoderTypes::Hybrid;
		}
		else if (containerName.find(L"mpeg") != std::string::npos)
		{
			DecoderTypes::SW;
		}

		return DecoderTypes::HW;
	}
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