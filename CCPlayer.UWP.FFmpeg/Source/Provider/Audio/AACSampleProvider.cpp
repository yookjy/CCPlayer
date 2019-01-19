#include "pch.h"
#include "AACSampleProvider.h"

AACSampleProvider::AACSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::HW)
{
}

AACSampleProvider::~AACSampleProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void AACSampleProvider::Flush()
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

HRESULT AACSampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	PBYTE pAudioExtradata = m_pAvCodecCtx->extradata;
	UINT nAudioExtradataSize = m_pAvCodecCtx->extradata_size;
	BYTE buf[128] = {};

	PBYTE pAACStartHeader = NULL;
	PFF_TO_MF_PRIV_DATA_ADTS_PAYLOAD pffADTSExtradata = NULL;
	
	if (pAudioExtradata)
	{
		pAACStartHeader = (PBYTE)calloc(nAudioExtradataSize, sizeof(PBYTE));
		CopyMemory(pAACStartHeader, pAudioExtradata, nAudioExtradataSize);
	}
	
	static const int accSampleRateTable[] = {
		96000, 88200, 64000, 48000, 44100, 32000,
		24000, 22050, 16000, 12000, 11025, 8000, 7350,
		0, 0, 0, 0 };
	int sampleRateIndex = _countof(accSampleRateTable);

	if (m_pAvCodecCtx->codec_id == AV_CODEC_ID_AAC && pAACStartHeader == NULL) //ADTS AAC
	{
		BYTE adtsHeader[7] = {};
		AVPacket tempPacket;
		av_init_packet(&tempPacket);
		if (GetADTSHeader(m_pAvFormatCtx, m_pAvFormatCtx->streams[m_streamIndex], &tempPacket, &adtsHeader[0]))
		{
			if (adtsHeader[0] != 0)
			{
				pffADTSExtradata = (PFF_TO_MF_PRIV_DATA_ADTS_PAYLOAD)malloc(sizeof FF_TO_MF_PRIV_DATA_ADTS_PAYLOAD);
				int adtsProfile = ((adtsHeader[2] & 0xC0) >> 6) + 1;
				int adtsSampleRate = (adtsHeader[2] & 0x3C) >> 2;
				int adtsChannel = ((adtsHeader[2] & 0x1) << 2) | ((adtsHeader[3] & 0xC0) >> 6);
				int adtsConfig0 = (adtsProfile << 3) | ((adtsSampleRate & 0xE) >> 1);
				int adtsConfig1 = ((adtsSampleRate & 0x1) << 7) | (adtsChannel << 3);
				CopyMemory(&pffADTSExtradata->adtsHeader[0], &adtsHeader[0], 7);
				pffADTSExtradata->aacConfig[0] = (BYTE)adtsConfig0;
				pffADTSExtradata->aacConfig[1] = (BYTE)adtsConfig1;
				pffADTSExtradata->sampleRateIndex = adtsSampleRate;
			}
		}
	}

	HRESULT hr = S_OK;

	hr = MFCreateMediaType(ppMediaType);
	IMFMediaType* pMediaType = *ppMediaType;

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
	}

	if (pAACStartHeader)
	{
		sampleRateIndex = ((pAACStartHeader[0] & 7) << 1) + ((pAACStartHeader[1] >> 7) & 1);
		CopyMemory(&buf[12], pAACStartHeader, 2); //5 bits objectTypeIndex, 4 bits sampleRateIndex, 4 bits channels
		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
		}

		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetUINT32(MF_MT_AAC_PAYLOAD_TYPE, 0); //0=Raw AAC
		}
	}
	else if (pffADTSExtradata)
	{
		sampleRateIndex = pffADTSExtradata->sampleRateIndex;
		memcpy(&buf[12], &pffADTSExtradata->aacConfig, 2);
		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
		}

		if (SUCCEEDED(hr))
		{
			hr = pMediaType->SetUINT32(MF_MT_AAC_PAYLOAD_TYPE, 1); //1=ADTS
		}
		buf[0] = 1; //ADTS Stream.
		buf[2] = 0xFE;
	}
	else
	{
		hr = MF_E_INVALIDTYPE;
	}

	if (SUCCEEDED(hr))
	{
		//hr = pMediaType->SetUINT32(MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, 0);
		hr = pMediaType->SetUINT32(MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, (UINT32)m_pAvCodecCtx->profile);
	}
	/*
	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_SAMPLE_SIZE, 1);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_PREFER_WAVEFORMATEX, TRUE);
	}
	*/
	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, m_pAvCodecCtx->block_align > 0 ? m_pAvCodecCtx->block_align : 1);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRateIndex < _countof(accSampleRateTable) ? accSampleRateTable[sampleRateIndex] : 0);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetBlob(MF_MT_USER_DATA, (const PBYTE)&buf, 14);
	}
	
	if (SUCCEEDED(hr))
	{
		if (m_pAvCodecCtx->channel_layout > 0)
		{
			hr = pMediaType->SetUINT32(MF_MT_AUDIO_CHANNEL_MASK, (UINT32)m_pAvCodecCtx->channel_layout);
		}
	}

	if (SUCCEEDED(hr))
	{
		//Frame size
		hr = pMediaType->SetUINT32(MF_MT_FRAME_SIZE, m_pAvCodecCtx->frame_size); //채널당? 도움말..
	}

	if (SUCCEEDED(hr))
	{
		//channel
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, m_pAvCodecCtx->channels);
	}

	if (SUCCEEDED(hr))
	{
		//bit per sample
		UINT32 bps = m_pAvCodecCtx->bits_per_coded_sample ? m_pAvCodecCtx->bits_per_coded_sample : 16;
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, bps);
	}

	if (SUCCEEDED(hr))
	{
		//Average bit rate
		hr = pMediaType->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);
	}

	//if (SUCCEEDED(hr))
	//{
	//	//extradata
	//	hr = pMediaType->SetBlob(
	//		MF_MT_MY_BLOB_DATA,
	//		m_pAvCodecCtx->extradata,			// Byte array
	//		m_pAvCodecCtx->extradata_size		// Size
	//		);
	//}

	if (pAACStartHeader != NULL)
	{
		free(pAACStartHeader);
	}
	
	if (pffADTSExtradata != NULL)
	{
		free(pffADTSExtradata);
	}

	return hr;
}

BOOL AACSampleProvider::GetADTSHeader(AVFormatContext* pAvFormatCtx, AVStream* pAvStream, AVPacket* pAvPacket, BYTE* pbADTSHeader)
{
	BOOL ret = FALSE;
	for (int i = 0; i < 65536; i++)
	{
		if (av_read_frame(pAvFormatCtx, pAvPacket) < 0)
			break;
		if (pAvPacket->stream_index == pAvStream->index)
		{
			if (pAvPacket->size > 7)
			{
				CopyMemory(pbADTSHeader, pAvPacket->data, 7);
				av_packet_unref(pAvPacket);
				ret = TRUE;
				break;
			}
		}
		av_packet_unref(pAvPacket);
	}
	avformat_seek_file(pAvFormatCtx, -1, 0, 0, 0, AVSEEK_FLAG_BYTE);
	return ret;
}
