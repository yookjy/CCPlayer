#pragma once

#include "Source\Provider\Subtitle\SubtitleProvider.h"
#include "Subtitle\Subtitle.h"

struct XSUBPacket
{
public:
	uint64_t TimeCode;
	AVPacket Packet;
};

class XSUBSampleProvider :
	public SubtitleProvider
{
public:
	XSUBSampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex,
		int codePage);
	virtual ~XSUBSampleProvider();
	virtual void PushPacket(AVPacket packet);
	virtual void ConsumePacket(int index, int64_t pts, int64_t syncts);
	virtual AVPacket PopPacket();
	virtual void WastePackets(int leftCount);
	virtual void Flush();
	virtual int GetPacketCount();

private:
	std::vector<XSUBPacket> m_packetList;
	std::vector<XSUBPacket>::iterator m_currentItorator;
	int64_t m_prevPts;
};

