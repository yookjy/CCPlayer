//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#pragma once

#include "Source\FFmpegReader.h"

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Subtitle
			{
				class ExternalSubtitleReader : public FFmpegReader
				{
				public:
					ExternalSubtitleReader(AVFormatContext* avFormatCtx, 
						CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ subtitleDecoderConnector);
					virtual bool IsSupportedMediaType(AVMediaType mediaType);
					virtual int ReadPacket();
					ProviderList GetProviderList();
				private:
				
				};
			}
		}
	}
}