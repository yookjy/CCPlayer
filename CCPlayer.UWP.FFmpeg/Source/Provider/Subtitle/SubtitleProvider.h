#pragma once

#include "Source\Provider\MFSampleProvider.h"
#include "Subtitle\Subtitle.h"

using namespace Windows::Data::Json;

class SubtitleProvider :
	public MFSampleProvider
{
public:
	SubtitleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex,
		int detectedCodePage);
	virtual ~SubtitleProvider();
	virtual void PushPacket(AVPacket packet);
	virtual HRESULT CreateMediaType(IMFMediaType** mediaType);
	virtual HRESULT WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket);
	virtual void Flush();
	virtual AVPacket PopPacket();
	virtual void ConsumePacket(int index, int64_t pts, int64_t syncts);
	virtual void LoadHeader();
	virtual void WastePackets(int leftCount);
	virtual int GetPacketCount();
	void LoadHeaderIfCodePageChanged();
	HRESULT FullFillPacketQueue();
	HRESULT FillPacketQueue();
	int64_t GetPacketPts();
	//void SetDetectedCodePage(int value);

	int64_t NeedFrameCount;

protected:
	Platform::String^ header;
	Platform::String^ title;
	int currCodePage;
	int detectedCodePage;
	int64_t timeBase;

	void DecodeCharset(AVPacket* avPacket, byte** replacedData, int* replacedSize);
	//void ExtractIfCompressedData(AVPacket* avPacket, byte** replacedData, int* replacedSize);
	void FreeAVPacket(AVPacket* avPacket, byte** replacedData, int* replacedSize);

};

