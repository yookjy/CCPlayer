#pragma once

#include "Source\Provider\MFSampleProvider.h"

class AC3SampleProvider :
	public MFSampleProvider
{
public:
	AC3SampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~AC3SampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
