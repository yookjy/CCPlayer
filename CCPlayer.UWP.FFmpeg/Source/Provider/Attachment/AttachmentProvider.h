#pragma once

#include "Source\Provider\MFSampleProvider.h"

class AttachmentProvider :
	public MFSampleProvider
{
public:
	AttachmentProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);
	virtual ~AttachmentProvider();
	virtual void PushPacket(AVPacket packet);
	virtual HRESULT CreateMediaType(IMFMediaType** mediaType);
	virtual HRESULT WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket);
	void PopulateAttachment(int streamIndex);
	virtual void Flush();
	
private:
};

