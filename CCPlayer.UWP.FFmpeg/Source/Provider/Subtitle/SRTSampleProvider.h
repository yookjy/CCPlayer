#pragma once

#include "Source\Provider\Subtitle\SubtitleProvider.h"
#include "Subtitle\Subtitle.h"

class SRTSampleProvider :
	public SubtitleProvider
{
public:
	SRTSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex,
		int codePage);

	virtual void ConsumePacket(int index, int64_t pts, int64_t syncts);
};

