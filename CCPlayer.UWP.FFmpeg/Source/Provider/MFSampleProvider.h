#pragma once
#include <queue>
#include "Source\FFmpegReader.h"
#include "Information\MediaInformation.h"
#include "Source\CAVCodecContext.h"

class FFmpegReader;

class MFSampleProvider
{
public:
	MFSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex,
		CCPlayer::UWP::Common::Codec::DecoderTypes decoderType);
	
	virtual ~MFSampleProvider();
	
	virtual HRESULT CreateMediaType(IMFMediaType** mediaType);
	virtual HRESULT GetNextSample(IMFSample **sample);
	virtual HRESULT WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket);
	virtual void PushPacket(AVPacket packet);
	virtual void Flush();
	virtual AVPacket PopPacket();

	int const GetCurrentStreamIndex() { return m_streamIndex; }
	HRESULT GetNextSampleTime(UINT64* time);
	AVMediaType const GetMediaType() { return m_pAvCodecCtx->codec_type; }
	void SetLicense(const bool fLicense);
	void SetCodecInformation();
	LONGLONG GetPts(int64_t pts) { return (ULONGLONG)(av_q2d(m_pAvFormatCtx->streams[m_streamIndex]->time_base) * 10000000L * pts); }
	
	CodecInformation^ CodecInfo;
	
protected:
	std::queue<AVPacket> m_packetQueue;
	int m_streamIndex;
	LONGLONG m_startTime;

	FFmpegReader* m_pReader;
	AVFormatContext* m_pAvFormatCtx;
	AVCodecContext* m_pAvCodecCtx;
	CCAVCodecContext* m_pccAavCodecCtx;
	SwsContext* SwsCtx = nullptr;
	uint64 seq = 0;
	UINT32 m_licenseMode;
	CCPlayer::UWP::Common::Codec::DecoderTypes m_decoderType;
};

