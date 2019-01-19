#pragma once

#include "Source\Provider\MFSampleProvider.h"

class H264AVCSampleProvider :
	public MFSampleProvider
{
public:
	H264AVCSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~H264AVCSampleProvider();
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