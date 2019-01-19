#include "pch.h"
#include "MFSampleProvider.h"
#include "Common\FFmpegMacro.h"

MFSampleProvider::MFSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	CCPlayer::UWP::Common::Codec::DecoderTypes decoderType) :
	m_licenseMode(0),
	m_pReader(reader),
	m_pAvFormatCtx(avFormatCtx),
	m_pAvCodecCtx(avCodecCtx),
	m_streamIndex(streamIndex),
	m_decoderType(decoderType),
	m_startTime(avFormatCtx->start_time)
{
	CodecInfo = ref new CCPlayer::UWP::Common::Codec::CodecInformation();
	if (avCodecCtx != nullptr)
	{
		SetCodecInformation();
	}
}

MFSampleProvider::~MFSampleProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

HRESULT MFSampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	HRESULT hr = S_OK;
	IMFMediaType* pMediaType = NULL;
	AVStream* stream = m_pAvFormatCtx->streams[m_streamIndex];

	m_pccAavCodecCtx = new CCAVCodecContext();

	switch (m_pAvCodecCtx->codec_type)
	{
	case AVMEDIA_TYPE_VIDEO:
		hr = MFCreateMediaType(ppMediaType);
		pMediaType = *ppMediaType;
		
		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
		}

		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_FFmpeg_SW);
		}

		if (SUCCEEDED(hr))
		{
			/*
			AVCodec* avCodec = avcodec_find_decoder(m_pAvCodecCtx->codec_id);
			if (avCodec)
			{
				AVCodecContext* pAvCodecCtx = avcodec_alloc_context3(avCodec);
				if (pAvCodecCtx)
				{
					if (FILL_CODEC_CTX(pAvCodecCtx, stream) != 0) {
						OutputDebugMessage(L"Couldn't set video codec context\n");
					}
				}
				if (avcodec_open2(pAvCodecCtx, avCodec, NULL) < 0)
				{
					avcodec_free_context(&pAvCodecCtx);
					pAvCodecCtx = nullptr;
					hr = E_FAIL; // Cannot open the video codec
				}
				else
				{
					m_pccAavCodecCtx->CodecContext = pAvCodecCtx;
					hr = pMediaType->SetUnknown(MF_MT_MY_FFMPEG_CODEC_CONTEXT, (IUnknown*)m_pccAavCodecCtx);
				}
			}
			*/
			m_pccAavCodecCtx->Stream = stream;
			hr = pMediaType->SetUnknown(MF_MT_MY_FFMPEG_CODEC_CONTEXT, (IUnknown*)m_pccAavCodecCtx);
		}

		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetUINT32(MF_MT_VIDEO_PROFILE, (UINT32)GET_CODEC_CTX_PARAM(stream, profile));
		}

		// Format details.
		if (SUCCEEDED(hr))
		{
			// Frame size
			hr = MFSetAttributeSize(
				pMediaType,
				MF_MT_FRAME_SIZE,
				GET_CODEC_CTX_PARAM(stream, width),
				GET_CODEC_CTX_PARAM(stream, height)
				);
		}

		if (SUCCEEDED(hr))
		{
			// Detect the correct framerate
			if (m_pAvCodecCtx->framerate.num != 0 || m_pAvCodecCtx->framerate.den != 1)
			{
				hr = MFSetAttributeRatio(
					pMediaType,
					MF_MT_FRAME_RATE,
					m_pAvCodecCtx->framerate.num,
					m_pAvCodecCtx->framerate.den
					);
			}
			else if (stream->avg_frame_rate.num != 0 || stream->avg_frame_rate.den != 0)
			{
				hr = MFSetAttributeRatio(
					pMediaType,
					MF_MT_FRAME_RATE,
					stream->avg_frame_rate.num,
					stream->avg_frame_rate.den
					);
			}
			else if (stream->r_frame_rate.num != 0 || stream->r_frame_rate.den != 0)
			{
				hr = MFSetAttributeRatio(
					pMediaType,
					MF_MT_FRAME_RATE,
					stream->r_frame_rate.num,
					stream->r_frame_rate.den
					);
			}
		}

		if (SUCCEEDED(hr))
		{
			// Pixel aspect ratio
			if (GET_CODEC_CTX_PARAM(stream, sample_aspect_ratio.den) == 0 || GET_CODEC_CTX_PARAM(stream, sample_aspect_ratio.num) == 0)
			{
				hr = MFSetAttributeRatio(pMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
			}
			else
			{
				hr = MFSetAttributeRatio(
					pMediaType,
					MF_MT_PIXEL_ASPECT_RATIO,
					GET_CODEC_CTX_PARAM(stream, sample_aspect_ratio.num),
					GET_CODEC_CTX_PARAM(stream, sample_aspect_ratio.den)
					);
			}
		}

		if (SUCCEEDED(hr))
		{
			// Average bit rate
			hr = pMediaType->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);
		}

		if (SUCCEEDED(hr))
		{
			//Interlacing(progressive frames)
			hr = pMediaType->SetUINT32(MF_MT_INTERLACE_MODE, (UINT32)MFVideoInterlace_Progressive);
		}
		
		if (SUCCEEDED(hr))
		{
			//time base
			hr = pMediaType->SetDouble(MF_MT_MY_FFMPEG_TIME_BASE, av_q2d(stream->time_base));
		}

		if (SUCCEEDED(hr))
		{
			//pixel format
			hr = pMediaType->SetUINT32(MF_MT_MY_FFMPEG_SAMPLE_FMT, (UINT32)m_pAvCodecCtx->pix_fmt);
		}

		break;
	case AVMEDIA_TYPE_AUDIO:
		hr = MFCreateMediaType(ppMediaType);
		pMediaType = *ppMediaType;

		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
		}
		
		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_FFmpeg_SW);
		}

		if (SUCCEEDED(hr))
		{
			//m_pccAavCodecCtx->CodecParameters = stream->codecpar;
			//hr = pMediaType->SetUnknown(MF_MT_MY_FFMPEG_CODEC_PARAMETERS, (IUnknown*)m_pccAavCodecCtx);
			/*
			AVCodec* avCodec = avcodec_find_decoder(m_pAvCodecCtx->codec_id);
			if (avCodec)
			{
				AVCodecContext* pAvCodecCtx = avcodec_alloc_context3(avCodec);
				if (pAvCodecCtx)
				{
					if (FILL_CODEC_CTX(pAvCodecCtx, stream) != 0) {
						OutputDebugMessage(L"Couldn't set audio codec context\n");
					}
				}
				if (avcodec_open2(pAvCodecCtx, avCodec, NULL) < 0)
				{
					avcodec_free_context(&pAvCodecCtx);
					pAvCodecCtx = nullptr;
					hr = E_FAIL; // Cannot open the video codec
				}
				else
				{
					m_pccAavCodecCtx->CodecContext = pAvCodecCtx;
					hr = pMediaType->SetUnknown(MF_MT_MY_FFMPEG_CODEC_CONTEXT, (IUnknown*)m_pccAavCodecCtx);
				}
			}
			*/
			m_pccAavCodecCtx->Stream = stream;
			hr = pMediaType->SetUnknown(MF_MT_MY_FFMPEG_CODEC_CONTEXT, (IUnknown*)m_pccAavCodecCtx);
		}

		// Format details.
		if (SUCCEEDED(hr))
		{
			//Frame size
			hr = pMediaType->SetUINT32(MF_MT_FRAME_SIZE, GET_CODEC_CTX_PARAM(stream, frame_size)); //채널당? 도움말..
		}

		if (SUCCEEDED(hr))
		{
			//channel
			hr = pMediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, GET_CODEC_CTX_PARAM(stream, channels));
		}

		if (SUCCEEDED(hr))
		{
			//bit per sample
			UINT32 bps = m_pAvCodecCtx->bits_per_coded_sample > 0 && m_pAvCodecCtx->bits_per_coded_sample <= 16 ? m_pAvCodecCtx->bits_per_coded_sample : 16;
			hr = pMediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, bps);
		}

		if (SUCCEEDED(hr))
		{
			//sample rate
			hr = pMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, GET_CODEC_CTX_PARAM(stream, sample_rate));
		}

		if (SUCCEEDED(hr))
		{
			//Average bit rate
			hr = pMediaType->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);
		}
		
		if (SUCCEEDED(hr))
		{
			//profile
			hr = pMediaType->SetUINT32(MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, GET_CODEC_CTX_PARAM(stream, profile));
		}

		if (SUCCEEDED(hr))
		{
			//sample fmt
			hr = pMediaType->SetUINT32(MF_MT_MY_FFMPEG_SAMPLE_FMT, (UINT32)m_pAvCodecCtx->sample_fmt); 
		}

		if (SUCCEEDED(hr))
		{
			//time base
			hr = pMediaType->SetDouble(MF_MT_MY_FFMPEG_TIME_BASE, av_q2d(stream->time_base));
		}

		if (SUCCEEDED(hr))
		{
			//license
			hr = pMediaType->SetUINT32(MF_MT_MY_FFMPEG_CODEC_LICENSE, (UINT32)m_licenseMode);
		}

		break;
	default:
		break;
	}

	return hr;
}

HRESULT MFSampleProvider::GetNextSample(IMFSample **sample)
{
	//	DebugMessage(L"GetNextSample\n");
	HRESULT hr = S_OK;
	try
	{
		AVPacket avPacket;
		av_init_packet(&avPacket);
		avPacket.data = NULL;
		avPacket.size = 0;

		bool frameComplete = false;
		bool decodeSuccess = true;
		int ret = 0;

		while (SUCCEEDED(hr) && !frameComplete)
		{
			// Continue reading until there is an appropriate packet in the stream
			while (m_packetQueue.empty())
			{
				ret = m_pReader->ReadPacket();
				if (ret < 0)
				{
					if (ret == AVERROR_EOF || (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached))
					{
						DebugMessage(L"GetNextSample reaching EOF\n");
						hr = E_FAIL;
						break;
					}
				}
			}

			if (!m_packetQueue.empty())
			{
				// Pick the packets from the queue one at a time
				avPacket = PopPacket();
				frameComplete = (hr == S_OK);
			}
		}

		if (SUCCEEDED(hr))
		{
			// Use decoding timestamp if presentation timestamp is not valid
			if (avPacket.pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
			{
				avPacket.pts = avPacket.dts;
			}

			if (avPacket.pts == AV_NOPTS_VALUE)
			{
				avPacket.pts = m_startTime;
			}

			// Write the packet out
			hr = WriteAVPacket(sample, &avPacket);

			if (SUCCEEDED(hr))
			{
				LONGLONG pts = ULONGLONG(av_q2d(m_pAvFormatCtx->streams[m_streamIndex]->time_base) * 10000000L * avPacket.pts);
				LONGLONG dur = ULONGLONG(av_q2d(m_pAvFormatCtx->streams[m_streamIndex]->time_base) * 10000000L * avPacket.duration);
				
				auto avDecoderConnector = m_pReader->GetAVDecoderConnector();
				if (avDecoderConnector != nullptr && avDecoderConnector->AudioSyncMilliSeconds != 0
					&& m_pAvCodecCtx->codec_type == AVMediaType::AVMEDIA_TYPE_VIDEO)
				{
					//오디오 싱크를 앞당긴 경우, 비디오 싱크를 늦춘다.
					(*sample)->SetSampleTime(pts + (avDecoderConnector->AudioSyncMilliSeconds * -10000));
				}
				else
				{
					(*sample)->SetSampleTime(pts);
				}
				(*sample)->SetSampleDuration(dur);
			}

			m_startTime = avPacket.pts;
		}

		av_packet_unref(&avPacket);
	}
	catch (Exception^ ade)
	{
		DebugMessage(ade->Message->Data());
	}
	return hr;
}

HRESULT MFSampleProvider::GetNextSampleTime(UINT64* time)
{
	HRESULT hr = S_OK;
	try
	{
		AVPacket avPacket;
		av_init_packet(&avPacket);
		avPacket.data = NULL;
		avPacket.size = 0;

		bool frameComplete = false;
		bool decodeSuccess = true;
		int ret = 0;

		while (SUCCEEDED(hr) && !frameComplete)
		{
			// Continue reading until there is an appropriate packet in the stream
			while (m_packetQueue.empty())
			{
				ret = m_pReader->ReadPacket();
				if (ret < 0)
				{
					if (ret == AVERROR_EOF || (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached))
					{
						DebugMessage(L"GetNextSample reaching EOF\n");
						hr = ERROR_HANDLE_EOF;
						frameComplete = true;
						break;
					}
				}
			}

			if (!m_packetQueue.empty())
			{
				// Pick the packets from the queue one at a time
				avPacket = m_packetQueue.front();

				if (avPacket.pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
				{
					avPacket.pts = avPacket.dts;
				}

				if (avPacket.pts == AV_NOPTS_VALUE)
				{
					avPacket.pts = m_startTime;
				}

				*time = ULONGLONG(av_q2d(m_pAvFormatCtx->streams[m_streamIndex]->time_base) * 10000000 * avPacket.pts);

				frameComplete = (hr == S_OK);
			}
		}
	}
	catch (Exception^ ade)
	{
		DebugMessage(ade->Message->Data());
	}
	return hr;
}

HRESULT MFSampleProvider::WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	HRESULT hr = S_OK;
	ComPtr<IMFMediaBuffer> spBuffer;
	BYTE *pData = nullptr;              // Pointer to the IMFMediaBuffer data.

	hr = MFCreateMemoryBuffer(avPacket->size, &spBuffer);

	if (SUCCEEDED(hr))
	{
		hr = spBuffer->Lock(&pData, nullptr, nullptr);
	}

	CopyMemory(pData, avPacket->data, avPacket->size);

	if (SUCCEEDED(hr))
	{
		hr = spBuffer->Unlock();
	}

	if (SUCCEEDED(hr))
	{
		hr = spBuffer->SetCurrentLength(avPacket->size);
	}

	if (SUCCEEDED(hr))
	{
		hr = MFCreateSample(ppSample);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->AddBuffer(spBuffer.Get());
	}

	//avpacket information
	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT64(MF_MT_MY_FFMPEG_AVPACKET_PTS, avPacket->pts);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT64(MF_MT_MY_FFMPEG_AVPACKET_DTS, avPacket->dts);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT64(MF_MT_MY_FFMPEG_AVPACKET_DURATION, avPacket->duration);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT32(MF_MT_MY_FFMPEG_AVPACKET_STREAM_INDEX, avPacket->stream_index);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT32(MF_MT_MY_FFMPEG_AVPACKET_FLAGS, avPacket->flags);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppSample)->SetUINT64(MF_MT_MY_FFMPEG_AVPACKET_POS, avPacket->pos);
	}

	return hr;
}

void MFSampleProvider::PushPacket(AVPacket packet)
{
	//	DebugMessage(L" - PushPacket\n");
	m_packetQueue.push(packet);
}

AVPacket MFSampleProvider::PopPacket()
{
	//	DebugMessage(L" - PopPacket\n");
	AVPacket avPacket;
	av_init_packet(&avPacket);
	avPacket.data = NULL;
	avPacket.size = 0;

	if (!m_packetQueue.empty())
	{
		avPacket = m_packetQueue.front();
		m_packetQueue.pop();
	}

	return avPacket;
}

void MFSampleProvider::Flush()
{
	while (!m_packetQueue.empty())
	{
		av_packet_unref(&PopPacket());
	}

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}

void MFSampleProvider::SetLicense(const bool fLicense)
{
	switch (m_pAvCodecCtx->codec_id)
	{
	case AV_CODEC_ID_AC3:
		//CodecName = L"(Dolby™) AC3";
		CodecInfo->CodecLicense = L"Dolby Laboratories, Inc.";
		m_licenseMode = fLicense ? 0 : 1;
		break;
	case AV_CODEC_ID_DTS:
		//CodecName = L"Digital Theatre System";
		CodecInfo->CodecLicense = L"DTS, Inc.";
		m_licenseMode = 2;
		break;
	default:
		m_licenseMode = 0;
		break;
	}

	//	if (IsOmegaVersion())
	{
		m_licenseMode = 0;
	}
}

void MFSampleProvider::SetCodecInformation()
{
	CodecInfo->StreamId = m_streamIndex;
	CodecInfo->CodecId = m_pAvCodecCtx->codec_id;
	CodecInfo->CodecType = m_pAvCodecCtx->codec_type;
	//코덱명 대문자 변환
	std::string cCodecName = std::string(m_pAvCodecCtx->codec->name);
	std::transform(cCodecName.begin(), cCodecName.end(), cCodecName.begin(), toupper);
	CodecInfo->CodecName = ToStringHat(cCodecName.c_str());
	CodecInfo->CodecLongName = ToStringHat(m_pAvCodecCtx->codec->long_name);
	CodecInfo->CodecTag = m_pAvCodecCtx->codec_tag;
	CodecInfo->DecoderType = m_decoderType;

	if (m_pAvCodecCtx->codec->profiles != nullptr)
	{
		CodecInfo->CodecProfileId = m_pAvCodecCtx->codec->profiles->profile;
		CodecInfo->CodecProfileName = ToStringHat(m_pAvCodecCtx->codec->profiles->name);
	}

	AVDictionary* dict = m_pAvFormatCtx->streams[m_streamIndex]->metadata;
	AVDictionaryEntry *t = NULL;

	while (t = av_dict_get(dict, "", t, AV_DICT_IGNORE_SUFFIX))
	{
		auto key = t->key;
		auto val = t->value;

		std::string strKey = std::string(key);
		std::transform(strKey.begin(), strKey.end(), strKey.begin(), toupper);

		if (strKey.compare("LANGUAGE") == 0)
		{
			CodecInfo->Language = ToStringHat(val);
		}
		else if (strKey.compare("TITLE") == 0)
		{
			CodecInfo->Title = ToStringHat(val);
		}
	}

	if (m_pAvCodecCtx->codec_type == AVMediaType::AVMEDIA_TYPE_VIDEO)
	{
		CodecInfo->Width = m_pAvCodecCtx->width;
		CodecInfo->Height = m_pAvCodecCtx->height;
		//FPS
		int num = 0, den = 0;
		if (m_pAvCodecCtx->framerate.num != 0 || m_pAvCodecCtx->framerate.den != 1)
		{
			num = m_pAvCodecCtx->framerate.num;
			den = m_pAvCodecCtx->framerate.den;
		}
		else if (m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den != 0)
		{
			num = m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num;
			den = m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den;
		}
		else if (m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den != 0)
		{
			num = m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num;
			den = m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den;
		}

		CodecInfo->Fps = (UINT32)ceil((double)num / den);
	}
	else if (m_pAvCodecCtx->codec_type == AVMediaType::AVMEDIA_TYPE_AUDIO)
	{
		CodecInfo->Channels = m_pAvCodecCtx->channels;
		CodecInfo->SampleRate = m_pAvCodecCtx->sample_rate;
		CodecInfo->Bps = m_pAvCodecCtx->bits_per_coded_sample;
	}
}

