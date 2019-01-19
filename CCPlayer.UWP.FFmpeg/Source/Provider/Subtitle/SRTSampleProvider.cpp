#include "pch.h"
#include "SRTSampleProvider.h"

SRTSampleProvider::SRTSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int codePage) : SubtitleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, codePage)
{
}

void SRTSampleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
{
	if (m_packetQueue.empty())
		return;

	if (index == this->GetCurrentStreamIndex())
	{
		AVPacket avPacket = PopPacket();
		byte* replacedData = nullptr;
		int replacedSize = 0;
		AVSubtitle sub;
		int gotSub = 0;
		int errCnt = 0;
		int orgSize = avPacket.size;

		//코드 페이지가 변경이 되었으면 헤더 갱신 및 통지
		LoadHeaderIfCodePageChanged();

		//문자셋 디코드
		DecodeCharset(&avPacket, &replacedData, &replacedSize);

		while (avPacket.size > 0)
		{
			int decodedBytes = avcodec_decode_subtitle2(m_pAvCodecCtx, &sub, &gotSub, &avPacket);
			if (gotSub)
			{
				if (decodedBytes < 0)
				{
					OutputDebugMessage(L"[avcodec_decode_subtitle2() failed] Decoded bytes < 0 !!! -> Srt processing \n");
					//break;
				}

				int64_t pts = avPacket.pts;
				if (pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
				{
					pts = avPacket.dts;
				}

				auto subPkt = ref new JsonObject();
				subPkt->Insert("Pts", JsonValue::CreateNumberValue((double)(pts * timeBase + syncts)));
				subPkt->Insert("StartDisplayTime", JsonValue::CreateNumberValue((double)(sub.start_display_time * timeBase)));
				subPkt->Insert("EndDisplayTime", JsonValue::CreateNumberValue((double)(avPacket.duration * timeBase)));
				subPkt->Insert("Format", JsonValue::CreateNumberValue(1.0));
				subPkt->Insert("Rects", ref new JsonArray());
				subPkt->Insert("NumRects", JsonValue::CreateNumberValue(1.0));

				GUID result;
				//guid 생성 (오류는 무시)
				CoCreateGuid(&result);
				auto jo = ref new JsonObject();
				std::wstring guid(Guid(result).ToString()->Data());
				String^ pName = ref new String(guid.substr(1, guid.length() - 2).c_str());

				jo->Insert("Guid", JsonValue::CreateStringValue(pName));
				jo->Insert("Type", JsonValue::CreateNumberValue((double)SubtitleContentTypes::Srt));
				jo->Insert("Text", JsonValue::CreateStringValue(""));
				jo->Insert("Ass", JsonValue::CreateStringValue(ToStringHat((char*)avPacket.data, CP_UTF8)));
				subPkt->GetNamedArray("Rects")->Append(jo);

				auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
				if (subtitleDecoderConnector != nullptr)
					subtitleDecoderConnector->PopulatePacket(subPkt, nullptr);
//				OutputDebugMessage(L"Completed population of srt-subtitle packet.\n");

				//루프 종료 조건 초기화
				errCnt = 0;
				orgSize = avPacket.size;
			}
			else
			{
				errCnt++;
			}

			*avPacket.data += decodedBytes;
			avPacket.size -= decodedBytes;

			avsubtitle_free(&sub);

			if ((orgSize == avPacket.size && errCnt > 5) || orgSize < avPacket.size)
			{
				break;
			}
		}

		FreeAVPacket(&avPacket, &replacedData, &replacedSize);
	}
	else
	{
		this->WastePackets(1);
	}
}
