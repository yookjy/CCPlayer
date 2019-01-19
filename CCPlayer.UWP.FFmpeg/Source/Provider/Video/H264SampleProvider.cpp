#include "pch.h"
#include "H264AVCSampleProvider.h"
#include "H264SampleProvider.h"


H264SampleProvider::H264SampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::HW)
{
}


H264SampleProvider::~H264SampleProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void H264SampleProvider::Flush()
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

HRESULT H264SampleProvider::WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	HRESULT hr = S_OK;
		
	// On a KeyFrame, write the SPS and PPS
	if (m_pbSPSAndPPS == nullptr)
	{
		hr = GetSPSAndPPSBuffer();
	}

	if (SUCCEEDED(hr))
	{
		BYTE *pbData = nullptr;              // Pointer to the IMFMediaBuffer data.
		ComPtr<IMFMediaBuffer> spBuffer;
		bool isKeyFrame = avPacket->flags & AV_PKT_FLAG_KEY;

		int buffSize = avPacket->size + (isKeyFrame ? m_cbSPSAndPPS : 0);
		hr = MFCreateMemoryBuffer(buffSize, &spBuffer);

		if (SUCCEEDED(hr))
		{
			hr = spBuffer->Lock(&pbData, nullptr, nullptr);
		}
		
		if (isKeyFrame)
		{
			//먼저 SPS&PPS 복사
			CopyMemory(pbData, m_pbSPSAndPPS, m_cbSPSAndPPS);
			CopyMemory(pbData + m_cbSPSAndPPS, avPacket->data, avPacket->size);
		}
		else
		{
			CopyMemory(pbData, avPacket->data, avPacket->size);
		}

		if (SUCCEEDED(hr))
		{
			hr = spBuffer->Unlock();
		}

		if (SUCCEEDED(hr))
		{
			hr = spBuffer->SetCurrentLength(buffSize);
		}

		if (SUCCEEDED(hr))
		{
			hr = MFCreateSample(ppSample);
		}

		if (SUCCEEDED(hr))
		{
			hr = (*ppSample)->AddBuffer(spBuffer.Get());
		}
	}

	// We have a complete frame
	return hr;
}


HRESULT H264SampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	HRESULT hr = S_OK;

	hr = MFCreateMediaType(ppMediaType);

	if (SUCCEEDED(hr))
	{
		hr = (*ppMediaType)->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppMediaType)->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264_ES);
	}

	if (SUCCEEDED(hr))
	{
		//pixel format
		hr = (*ppMediaType)->SetUINT32(MF_MT_MY_FFMPEG_SAMPLE_FMT, (UINT32)m_pAvCodecCtx->pix_fmt);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppMediaType)->SetUINT32(MF_MT_VIDEO_PROFILE, (UINT32)m_pAvCodecCtx->profile);
	}

	// Format details.
	if (SUCCEEDED(hr))
	{
		// Frame size
		hr = MFSetAttributeSize(
				*ppMediaType,
				MF_MT_FRAME_SIZE,
				m_pAvCodecCtx->width,
				m_pAvCodecCtx->height
				);
	}

	if (SUCCEEDED(hr))
	{
		// Detect the correct framerate
		if (m_pAvCodecCtx->framerate.num != 0 || m_pAvCodecCtx->framerate.den != 1)
		{
			hr = MFSetAttributeRatio(
				*ppMediaType,
				MF_MT_FRAME_RATE,
				m_pAvCodecCtx->framerate.num,
				m_pAvCodecCtx->framerate.den
				);
		}
		else if (m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den != 0)
		{
			hr = MFSetAttributeRatio(
				*ppMediaType,
				MF_MT_FRAME_RATE,
				m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num,
				m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den
				);
		}
	}
	
	// Pixel aspect ratio
	if (m_pAvCodecCtx->sample_aspect_ratio.den == 0 || m_pAvCodecCtx->sample_aspect_ratio.num == 0)
	{
		if (SUCCEEDED(hr))
		{
			hr = MFSetAttributeRatio((*ppMediaType), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
		}
	}
	else
	{
		if (SUCCEEDED(hr))
		{
			hr = MFSetAttributeRatio(
					*ppMediaType,
					MF_MT_PIXEL_ASPECT_RATIO,
					m_pAvCodecCtx->sample_aspect_ratio.num,
					m_pAvCodecCtx->sample_aspect_ratio.den
					);
		}
	}

		if (SUCCEEDED(hr))
		{
			// Average bit rate
			hr = (*ppMediaType)->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);

			if (SUCCEEDED(hr))
			{
				//Interlacing(mixed frames)
				hr = (*ppMediaType)->SetUINT32(MF_MT_INTERLACE_MODE, (UINT32)MFVideoInterlace_MixedInterlaceOrProgressive);
			}
		}
	return hr;
}


HRESULT H264SampleProvider::GetSPSAndPPSBuffer()
{
	HRESULT hr = S_OK;

	if (m_pAvCodecCtx->extradata == nullptr && m_pAvCodecCtx->extradata_size < 8)
	{
		// The data isn't present
		hr = E_FAIL;
	}
	else
	{
		m_cbSPSAndPPS = m_pAvCodecCtx->extradata_size;
		m_pbSPSAndPPS = m_pAvCodecCtx->extradata;
	}

	return hr;
}