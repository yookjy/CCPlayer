#include "pch.h"
#include "MP4SampleProvider.h"

#define VID_WH_E(w,h,w2,h2) (w <= w2) && (h <= h2)

MP4SampleProvider::MP4SampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::HW)
{
}

MP4SampleProvider::~MP4SampleProvider()
{
	Flush();

	if (m_pAvCodecCtx != NULL)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void MP4SampleProvider::Flush()
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

HRESULT MP4SampleProvider::CreateMediaType(IMFMediaType** ppMediaType)
{
	HRESULT hr = S_OK;
	hr = MFCreateMediaType(ppMediaType);
	IMFMediaType* pMediaType = *ppMediaType;

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
	}

	if (SUCCEEDED(hr))
	{
		if (m_pAvCodecCtx->codec_id == AV_CODEC_ID_H263)
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H263);
		//	if (SUCCEEDED(hr))
		//	{
		//		if (m_pAvCodecCtx->extradata_size > 0)
		//		{
		//			hr = (*ppMediaType)->SetBlob(
		//				MF_MT_USER_DATA,
		//				m_pAvCodecCtx->extradata,			// Byte array
		//				m_pAvCodecCtx->extradata_size		// Size
		//				);
		//		}
		//	}
		//	if (SUCCEEDED(hr))
		//	{
		//		if (m_pAvCodecCtx->extradata_size > 0)
		//		{
		//			hr = (*ppMediaType)->SetBlob(
		//				MF_MT_MPEG_SEQUENCE_HEADER,
		//				m_pAvCodecCtx->extradata,			// Byte array
		//				m_pAvCodecCtx->extradata_size		// Size
		//				);
		//		}
		//	}
		}
		else if (m_pAvCodecCtx->codec_id == AV_CODEC_ID_MPEG4)
		{
			hr = pMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_MP4V);
		}
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_ORIGINAL_4CC, m_pAvCodecCtx->codec_tag);
	}
	
	if (SUCCEEDED(hr))
	{
		pMediaType->SetUINT32(MF_MT_AVI_FLAGS, 1);
	}

	if (SUCCEEDED(hr))
	{
		int level = 0;
		if (VID_WH_E(m_pAvCodecCtx->width, m_pAvCodecCtx->height, 176, 144))
			level = 1;
		else if (VID_WH_E(m_pAvCodecCtx->width, m_pAvCodecCtx->height, 352, 288))
			level = 2;
		else if (VID_WH_E(m_pAvCodecCtx->width, m_pAvCodecCtx->height, 352, 756))
			level = 4;
		else if (VID_WH_E(m_pAvCodecCtx->width, m_pAvCodecCtx->height, 720, 576))
			level = 5;
		else if (VID_WH_E(m_pAvCodecCtx->width, m_pAvCodecCtx->height, 640, 480))
			level = 0x4A;
	
		if (level != 0)
			pMediaType->SetUINT32(MF_MT_MPEG2_LEVEL, level);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_COMPRESSED, TRUE);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, FALSE);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_FIXED_SIZE_SAMPLES, FALSE);
	}

	if (SUCCEEDED(hr))
	{
		hr = pMediaType->SetUINT32(MF_MT_VIDEO_PROFILE, (UINT32)m_pAvCodecCtx->profile);
	}

	// Format details.
	if (SUCCEEDED(hr))
	{
		// Frame size
		hr = MFSetAttributeSize(
			pMediaType,
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
				pMediaType,
				MF_MT_FRAME_RATE,
				m_pAvCodecCtx->framerate.num,
				m_pAvCodecCtx->framerate.den
				);
		}
		else if (m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den != 0)
		{
			hr = MFSetAttributeRatio(
				pMediaType,
				MF_MT_FRAME_RATE,
				m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.num,
				m_pAvFormatCtx->streams[m_streamIndex]->avg_frame_rate.den
				);
		}
		else if (m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num != 0 || m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den != 0)
		{
			hr = MFSetAttributeRatio(
				pMediaType,
				MF_MT_FRAME_RATE,
				m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.num,
				m_pAvFormatCtx->streams[m_streamIndex]->r_frame_rate.den
				);
		}
	}

	if (SUCCEEDED(hr))
	{
		// Pixel aspect ratio
		if (m_pAvCodecCtx->sample_aspect_ratio.den == 0 || m_pAvCodecCtx->sample_aspect_ratio.num == 0)
		{
			hr = MFSetAttributeRatio(pMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
		}
		else
		{
			hr = MFSetAttributeRatio(
				pMediaType,
				MF_MT_PIXEL_ASPECT_RATIO,
				m_pAvCodecCtx->sample_aspect_ratio.num,
				m_pAvCodecCtx->sample_aspect_ratio.den
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
		hr = pMediaType->SetUINT32(MF_MT_INTERLACE_MODE, (UINT32)MFVideoInterlace_MixedInterlaceOrProgressive);
		
	}

	return hr;
}
