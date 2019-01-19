#pragma once

#include "Source\Provider\MFSampleProvider.h"

typedef struct _FF_TO_MF_PRIV_DATA_ADTS_PAYLOAD{
	BYTE adtsHeader[7];
	BYTE aacConfig[2]; 
	int sampleRateIndex;
} FF_TO_MF_PRIV_DATA_ADTS_PAYLOAD, *PFF_TO_MF_PRIV_DATA_ADTS_PAYLOAD;

class AACSampleProvider :
	public MFSampleProvider
{
public:
	AACSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex);

	virtual ~AACSampleProvider();
	virtual void Flush();
	HRESULT CreateMediaType(IMFMediaType** mediaType);

private:
	BOOL GetADTSHeader(AVFormatContext* pAvFormatCtx, AVStream* pAvStream, AVPacket* pAvPacket, BYTE* pbADTSHeader);
};

