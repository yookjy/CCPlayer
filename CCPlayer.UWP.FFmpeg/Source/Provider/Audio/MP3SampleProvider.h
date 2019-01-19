#pragma once

#include "Source\Provider\MFSampleProvider.h"

class MP3SampleProvider :
	public MFSampleProvider
{
public:
	MP3SampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~MP3SampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
