#pragma once

#include "Source\Provider\MFSampleProvider.h"

class MP4SampleProvider :
	public MFSampleProvider
{
public:
	MP4SampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~MP4SampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
