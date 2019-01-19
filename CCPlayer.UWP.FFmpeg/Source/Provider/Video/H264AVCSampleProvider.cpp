#include "pch.h"
#include "H264AVCSampleProvider.h"

H264AVCSampleProvider::H264AVCSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::HW)
	, m_pbSPSAndPPS(NULL)
{
}


H264AVCSampleProvider::~H264AVCSampleProvider()
{
	if (m_pbSPSAndPPS != NULL)
	{
		free(m_pbSPSAndPPS);
		m_pbSPSAndPPS = NULL;
	}

	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void H264AVCSampleProvider::Flush()
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

HRESULT H264AVCSampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	HRESULT hr = S_OK;

	hr = MFCreateMediaType(ppMediaType);

	if (SUCCEEDED(hr))
	{
		hr = (*ppMediaType)->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
	}

	if (SUCCEEDED(hr))
	{
		hr = (*ppMediaType)->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
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
		else if (m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den != 0)
		{
			hr = MFSetAttributeRatio(
				*ppMediaType,
				MF_MT_FRAME_RATE,
				m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num,
				m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den
				);
		}
	}

	// Pixel aspect ratio
	if (SUCCEEDED(hr))
	{
		if (m_pAvCodecCtx->sample_aspect_ratio.den == 0 || m_pAvCodecCtx->sample_aspect_ratio.num == 0)
		{
			hr = MFSetAttributeRatio(*ppMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
		}
		else
		{
			hr = MFSetAttributeRatio(
				*ppMediaType,
				MF_MT_PIXEL_ASPECT_RATIO,
				m_pAvCodecCtx->sample_aspect_ratio.num,
				m_pAvCodecCtx->sample_aspect_ratio.den
				);
		}
	}

	if (SUCCEEDED(hr) && m_pAvCodecCtx->bit_rate > 0)
	{
		// Average bit rate
		hr = (*ppMediaType)->SetUINT64(MF_MT_AVG_BITRATE, m_pAvCodecCtx->bit_rate);
	}

	if (SUCCEEDED(hr))
	{
		//Interlacing(mixed frames)
		hr = (*ppMediaType)->SetUINT32(MF_MT_INTERLACE_MODE, (UINT32)MFVideoInterlace_MixedInterlaceOrProgressive);
	}

	//if (SUCCEEDED(hr))
	//{
	//	//pixel format
	//	hr = (*ppMediaType)->SetBlob(
	//		MF_MT_MY_BLOB_DATA,
	//		m_pAvCodecCtx->extradata,			// Byte array
	//		m_pAvCodecCtx->extradata_size		// Size
	//		);
	//}

	return hr;
}

HRESULT H264AVCSampleProvider::WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	HRESULT hr = S_OK;

	// On a KeyFrame, write the SPS and PPS
	if (m_pbSPSAndPPS == NULL)
	{
		hr = GetSPSAndPPSBuffer();
	}

	if (SUCCEEDED(hr))
	{
		// Convert the packet to NAL format
		hr = WriteNALPacket(ppSample, avPacket);
	}

	// We have a complete frame
	return hr;
}

HRESULT H264AVCSampleProvider::WriteNALPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	HRESULT hr = S_OK;
	int32 buffSize = 0;
	BYTE* pbData = NULL;
	ComPtr<IMFMediaBuffer> spBuffer;

	hr = MFCreateMemoryBuffer(avPacket->size + ((avPacket->flags & AV_PKT_FLAG_KEY) ? m_cbSPSAndPPS : 0), &spBuffer);

	if (SUCCEEDED(hr))
	{
		hr = spBuffer->Lock(&pbData, nullptr, nullptr);
	}
	
	if (SUCCEEDED(hr))
	{
		// On a KeyFrame, write the SPS and PPS
		if (avPacket->flags & AV_PKT_FLAG_KEY)
		{
			//먼저 SPS&PPS 복사
			CopyMemory(pbData, m_pbSPSAndPPS, m_cbSPSAndPPS);
			buffSize += m_cbSPSAndPPS;
		}

		uint32 index = 0;
		int32 size = 0;
		uint32 packetSize = (uint32)avPacket->size;

		do
		{
			// Make sure we have enough data
			if (packetSize < (index + 4))
			{
				DebugMessage(L"H264 packet error!!!! - have not enough data 1 !!! \n");
				//hr = E_FAIL; //스킵해야 하므로 러를 내면 안됨
				break;
			}

			// Grab the size of the blob
			size = (avPacket->data[index] << 24) + (avPacket->data[index + 1] << 16) + (avPacket->data[index + 2] << 8) + avPacket->data[index + 3];

			// Write the NAL unit to the stream
			*(pbData + buffSize++) = 0;
			*(pbData + buffSize++) = 0;
			*(pbData + buffSize++) = 0;
			*(pbData + buffSize++) = 1;
			index += 4;

			if (packetSize < (index + size) || (UINT32_MAX - index) < (unsigned)size)
			{
				DebugMessage(L"H264 packet error!!!! - have not enough data 2 !!! \n");
				//hr = E_FAIL; //스킵해야 하므로 러를 내면 안됨
				break;
			}

			// Write the rest of the packet to the stream
			CopyMemory(pbData + buffSize, (byte*)&avPacket->data[index], size);
			buffSize += size;
			index += size;
		} while (index < packetSize);

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
	
	//spBuffer.Reset();
	return hr;
}

HRESULT H264AVCSampleProvider::GetSPSAndPPSBuffer()
{
	HRESULT hr = S_OK;
	int spsLength = 0;
	int ppsLength = 0;

	byte* spsBuffer = NULL;
	int spsSize = 0;

	// Get the position of the SPS
	if (m_pAvCodecCtx->extradata == nullptr && m_pAvCodecCtx->extradata_size < 8)
	{
		// The data isn't present
		hr = E_FAIL;
	}
	if (SUCCEEDED(hr))
	{
		byte* spsPos = m_pAvCodecCtx->extradata + 8;
		spsLength = spsPos[-1];

		if (m_pAvCodecCtx->extradata_size < (8 + spsLength))
		{
			// We don't have a complete SPS
			hr = E_FAIL;
		}
		else
		{
			spsSize = 4 + spsLength;
			spsBuffer = (byte*)malloc(spsSize);
			// Write the NAL unit for the SPS
			spsBuffer[0] = 0;
			spsBuffer[1] = 0;
			spsBuffer[2] = 0;
			spsBuffer[3] = 1;

			// Write the SPS
			CopyMemory(spsBuffer + 4, spsPos, spsLength);
		}
	}

	if (SUCCEEDED(hr))
	{
		if (m_pAvCodecCtx->extradata_size < (8 + spsLength + 3))
		{
			hr = E_FAIL;
		}

		if (SUCCEEDED(hr))
		{
			byte* ppsPos = m_pAvCodecCtx->extradata + 8 + spsLength + 3;
			ppsLength = ppsPos[-1];

			if (m_pAvCodecCtx->extradata_size < (8 + spsLength + 3 + ppsLength))
			{
				hr = E_FAIL;
			}
			else
			{
				m_cbSPSAndPPS = spsSize + 4 + ppsLength;
				m_pbSPSAndPPS = (byte*)malloc(m_cbSPSAndPPS);

				CopyMemory(m_pbSPSAndPPS, spsBuffer, spsSize);

				// Write the NAL unit for the PPS
				*(m_pbSPSAndPPS + spsSize + 0) = 0;
				*(m_pbSPSAndPPS + spsSize + 1) = 0;
				*(m_pbSPSAndPPS + spsSize + 2) = 0;
				*(m_pbSPSAndPPS + spsSize + 3) = 1;

				// Write the PPS
				CopyMemory(m_pbSPSAndPPS + spsSize + 4, ppsPos, ppsLength);
			}
		}
	}

	if (spsBuffer != nullptr)
	{
		free(spsBuffer);
		spsBuffer = NULL;
	}

	return hr;
}