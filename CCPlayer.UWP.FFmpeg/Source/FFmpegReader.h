#pragma once

extern "C"
{
#include <libavformat/avformat.h>
}

class MFSampleProvider;
typedef std::vector<MFSampleProvider*> ProviderList;

struct SubtitleIndexInfo
{
public:
	int Index;
	int StreamIndex;
	int64_t NeedPreloadFrameCount;
};

class FFmpegReader
{
public:
	FFmpegReader(AVFormatContext* avFormatCtx, 
		CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ avDecoderConnector,
		CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ subtitleDecoderConnector,
		CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ attachmentDecoderConnector);
	virtual ~FFmpegReader();
	virtual int ReadPacket();
	virtual bool IsSupportedMediaType(AVMediaType mediaType);
	void Flush();
	HRESULT GetNextSample(int index, IMFSample** pSample);
	void AddStream(MFSampleProvider* pSampleProvider);
	HRESULT GetNextSampleTime(UINT64* time);
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ GetAVDecoderConnector();
	CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ GetSubtitleDecoderConnector();
	CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ GetAttachmentDecoderConnector();

protected:
	AVFormatContext* m_pAvFormatCtx;
	ProviderList m_providerList;
	std::vector<SubtitleIndexInfo> m_subtitleProviderIndexList;
	int64_t m_needTotalFrameCount;
	bool m_completedAddFrames;

private:
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ m_avDecoderConnector;
	CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ m_subtitleDecoderConnector;
	CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ m_attachmentDecoderConnector;
};

