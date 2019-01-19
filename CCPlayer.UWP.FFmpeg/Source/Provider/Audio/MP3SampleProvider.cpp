#include "pch.h"
#include "MP3SampleProvider.h"


MP3SampleProvider::MP3SampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::HW)
{
}

MP3SampleProvider::~MP3SampleProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void MP3SampleProvider::Flush()
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

HRESULT MP3SampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	HRESULT hr = S_OK;
	hr = MFCreateMediaType(ppMediaType);
	IMFMediaType* pMediaType = *ppMediaType;

	//MPEGLAYER3WAVEFORMAT format;
	//ZeroMemory(&format, sizeof(format));

	//format.wfx.wFormatTag = WAVE_FORMAT_MPEGLAYER3;
	//format.wfx.nChannels = m_pAvCodecCtx->channels;
	//format.wfx.nSamplesPerSec = m_pAvCodecCtx->sample_rate;
	//format.wfx.wBitsPerSample = 16;
	//format.wfx.nBlockAlign = (WORD)(format.wfx.nChannels * format.wfx.wBitsPerSample / 8);
	//format.wfx.nAvgBytesPerSec = format.wfx.nBlockAlign * m_pAvCodecCtx->sample_rate;
	//format.wfx.cbSize = 12;
	//format.wID = MPEGLAYER3_ID_MPEG;
	//format.fdwFlags = MPEGLAYER3_FLAG_PADDING_ISO;
	//format.nBlockSize = 144 * (m_pAvCodecCtx->bit_rate / m_pAvCodecCtx->sample_rate) + m_pAvCodecCtx->initial_padding;
	//format.nFramesPerBlock = 1;
	//format.nCodecDelay = 0;
	//format.nBlockSize = (WORD)(format.wfx.nChannels * format.wfx.wBitsPerSample / 8);

	//// Use the structure to initialize the Media Foundation media type.
	//ThrowIfError(MFCreateMediaType(ppMediaType));
	//ThrowIfError(MFInitMediaTypeFromWaveFormatEx(*ppMediaType, (const WAVEFORMATEX*)&format, sizeof(format)));
	
	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_MP3);
	}
	
	/*if (SUCCEEDED(hr))
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
		hr = pMediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, m_pAvCodecCtx->sample_rate);
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
		if (m_pAvCodecCtx->bit_rate > 0)
		{
			hr = pMediaType->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);
			hr = pMediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, m_pAvCodecCtx->bit_rate > 0 ? (UINT32)(m_pAvCodecCtx->bit_rate / 8) : 0);
		}
		
	}
	
	return hr;
}
