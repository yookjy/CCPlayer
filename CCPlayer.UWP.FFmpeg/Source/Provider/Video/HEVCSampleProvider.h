#pragma once

#include "Source\Provider\MFSampleProvider.h"

class HEVCSampleProvider :
	public MFSampleProvider
{
public:
	HEVCSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~HEVCSampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);
};
