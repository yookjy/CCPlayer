#pragma once

#include "Source\Provider\MFSampleProvider.h"

class WMASampleProvider :
	public MFSampleProvider
{
public:
	WMASampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~WMASampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
