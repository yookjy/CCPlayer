#include "pch.h"
#include "ASSSampleProvider.h"

using namespace CCPlayer::UWP::FFmpeg::Subtitle;

ASSSampleProvider::ASSSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int codePage) : SubtitleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, codePage)
{
}

void ASSSampleProvider::LoadHeader()
{
	SubtitleProvider::LoadHeader();
	SubtitleHelper::LoadASSHeader(this->header, &m_scriptInfoProp, &m_styleMap, &m_eventList);
}

void ASSSampleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
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
				int64_t pts = avPacket.pts;
				if (pts == AV_NOPTS_VALUE && avPacket.dts != AV_NOPTS_VALUE)
				{
					pts = avPacket.dts;
				}

				auto subPkt = ref new JsonObject();
				subPkt->Insert("Pts", JsonValue::CreateNumberValue((double)(pts * timeBase + syncts)));
				subPkt->Insert("StartDisplayTime", JsonValue::CreateNumberValue((double)(sub.start_display_time * timeBase)));
				subPkt->Insert("EndDisplayTime", JsonValue::CreateNumberValue((double)(avPacket.duration * timeBase)));
				subPkt->Insert("Format", JsonValue::CreateNumberValue((double)sub.format));
				subPkt->Insert("Rects", ref new JsonArray());
				subPkt->Insert("NumRects", JsonValue::CreateNumberValue((double)sub.num_rects));

				if (m_scriptInfoProp != nullptr && m_scriptInfoProp->Size > 0
					&& m_scriptInfoProp->HasKey("PlayResX") && m_scriptInfoProp->HasKey("PlayResY"))
				{
					auto prx = m_scriptInfoProp->Lookup("PlayResX")->ToString();
					auto pry = m_scriptInfoProp->Lookup("PlayResY")->ToString();

					std::string sprx(prx->Begin(), prx->End());
					std::string spry(pry->Begin(), pry->End());

					double nsw = strtod(sprx.c_str(), NULL);
					double nsh = strtod(spry.c_str(), NULL);

					subPkt->Insert("NaturalSubtitleWidth", JsonValue::CreateNumberValue(nsw));
					subPkt->Insert("NaturalSubtitleHeight", JsonValue::CreateNumberValue(nsh));
				}

				for (uint32 i = 0; i < sub.num_rects; i++)
				{
					GUID result;
					//guid 생성 (오류는 무시)
					CoCreateGuid(&result);
					auto jo = ref new JsonObject();
					std::wstring guid(Guid(result).ToString()->Data());
					String^ pName = ref new String(guid.substr(1, guid.length() - 2).c_str());
					String^ ass = nullptr;

					jo->Insert("Guid", JsonValue::CreateStringValue(pName));
					jo->Insert("Type", JsonValue::CreateNumberValue((double)sub.rects[i]->type));
					jo->Insert("Text", JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->text, CP_UTF8)));
					jo->Insert("Ass", JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->ass, CP_UTF8)));

					if (m_styleMap != nullptr || m_eventList.size() > 0)
					{
						SubtitleHelper::ApplyASSStyle(jo, m_styleMap, &m_eventList);
					}

					subPkt->GetNamedArray("Rects")->Append(jo);
				}

				auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
				if (subtitleDecoderConnector != nullptr)
					subtitleDecoderConnector->PopulatePacket(subPkt, nullptr);
				//OutputDebugMessage(L"Completed population of ass/ssa-subtitle packet.\n");

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