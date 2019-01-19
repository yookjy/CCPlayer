#pragma once

#include "Source\Provider\MFSampleProvider.h"

class VC1SampleProvider :
	public MFSampleProvider
{
public:
	VC1SampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~VC1SampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
