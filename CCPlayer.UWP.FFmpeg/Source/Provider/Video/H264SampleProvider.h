#pragma once

#include "Source\Provider\MFSampleProvider.h"

class H264SampleProvider : 
	public MFSampleProvider
{
public:
	H264SampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~H264SampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);

protected:
	virtual HRESULT WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket);

private:
	HRESULT WriteNALPacket(IMFSample** ppSample, AVPacket* avPacket);
	HRESULT GetSPSAndPPSBuffer();

	BYTE* m_pbSPSAndPPS = NULL;
	UINT32 m_cbSPSAndPPS = 0;
};

