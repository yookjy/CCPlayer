//*****************************************************************************
//
//	Copyright 2015 Microsoft Corporation
//
//	Licensed under the Apache License, Version 2.0 (the "License");
//	you may not use this file except in compliance with the License.
//	You may obtain a copy of the License at
//
//	http ://www.apache.org/licenses/LICENSE-2.0
//
//	Unless required by applicable law or agreed to in writing, software
//	distributed under the License is distributed on an "AS IS" BASIS,
//	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//	See the License for the specific language governing permissions and
//	limitations under the License.
//
//*****************************************************************************

#include "pch.h"
#include <ppltasks.h>
#include <collection.h>
#include "ThumbnailReader.h"
#include "Common\FFmpegMacro.h"

extern "C"
{
#include <libavutil/imgutils.h>
}

using namespace concurrency;
using namespace Windows::Foundation;
using namespace CCPlayer::UWP::FFmpeg::Information;

ThumbnailReader::ThumbnailReader(
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex)
	: m_pAvFormatCtx(avFormatCtx)
	, m_pAvCodecCtx(avCodecCtx)
	, m_streamIndex(streamIndex)
	, m_pSwsCtx(nullptr)
{
}

ThumbnailReader::~ThumbnailReader()
{
	if (m_pSwsCtx != NULL)
	{
		sws_freeContext(m_pSwsCtx);
	}
}

Array<byte>^ ThumbnailReader::GetBitmapData(Size size)
{
	DebugMessage(L"GetBitmapData\n");

	HRESULT hr = S_OK;
	Array<byte>^ data = nullptr;
	bool frameComplete = false;

	AVFrame* pAvFrame = av_frame_alloc();
	if (pAvFrame == nullptr)
	{
		hr = E_OUTOFMEMORY;
	}

	while (SUCCEEDED(hr) && !frameComplete)
	{
		AVPacket avPacket;
		av_init_packet(&avPacket);
		avPacket.data = NULL;
		avPacket.size = 0;

		if (av_read_frame(m_pAvFormatCtx, &avPacket) < 0)
		{
			DebugMessage(L"GetNextSample reaching EOF\n");
			hr = E_FAIL;
		}
		else
		{
			// Push the packet to the appropriate
			if (m_streamIndex == avPacket.stream_index)
			{
				while (avPacket.size > 0)
				{
					int gotFrame = 0;
					if (decode_frame(m_pAvCodecCtx, pAvFrame, &gotFrame, &avPacket, true) < 0)
					{
						OutputDebugMessage(L"Failed extraction of the thumbnail video frame!\n");
						break;
					}
					
					if (gotFrame && pAvFrame->pict_type == AVPictureType::AV_PICTURE_TYPE_I)
					{
						AVFrame* cFrame = av_frame_alloc();
						if (cFrame == NULL)
						{
							hr = E_OUTOFMEMORY;
						}

						if (SUCCEEDED(hr))
						{
							cFrame->format = AV_PIX_FMT_RGB32;
							cFrame->width = (int)size.Width;
							cFrame->height = (int)size.Height;

							m_pSwsCtx = sws_getCachedContext(m_pSwsCtx,
								pAvFrame->width, pAvFrame->height, (AVPixelFormat)pAvFrame->format,
								cFrame->width, cFrame->height, (AVPixelFormat)cFrame->format,
								SWS_BICUBIC, NULL, NULL, NULL);;

							if (m_pSwsCtx == nullptr)
							{
								hr = E_OUTOFMEMORY;
							}
						}
						
						if (SUCCEEDED(hr))
						{
							if (av_image_alloc(cFrame->data, cFrame->linesize, cFrame->width, cFrame->height, (AVPixelFormat)cFrame->format, 1) < 0)
							{
								hr = E_FAIL;
							}
						}

						if (SUCCEEDED(hr))
						{
							// Convert decoded video pixel format to RGB32 using FFmpeg software scaler
							if (sws_scale(m_pSwsCtx, (const uint8_t **)(pAvFrame->data), pAvFrame->linesize,
								0, m_pAvCodecCtx->height, cFrame->data, cFrame->linesize) < 0)
							{
								hr = E_FAIL;
							}
						}

						if (SUCCEEDED(hr))
						{
							int cbImageSize = av_image_get_buffer_size((AVPixelFormat)cFrame->format, cFrame->width, cFrame->height, 1);
							data = ref new Platform::Array<byte>(cbImageSize);
							CopyMemory(data->Data, cFrame->data[0], cbImageSize);

							frameComplete = true;
						}

						if (cFrame)
						{
							av_freep(&cFrame->data[0]);
							av_frame_free(&cFrame);
						}
						DebugMessage(L"Complete : Found thumbnail from video frame!\n");
					}
					else
					{
						DebugMessage(L"Skip : Couldn't get a frame!\n");
						avPacket.size = 0;
					}
					av_frame_unref(pAvFrame);
				}
			}
				
			av_packet_unref(&avPacket);
		}
	}

	if (pAvFrame)
	{
		av_frame_free(&pAvFrame);
	}
	
	return data;
}




