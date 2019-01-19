#pragma once

#include "Source\Provider\MFSampleProvider.h"

class WMVSampleProvider :
	public MFSampleProvider
{
public:
	WMVSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~WMVSampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
