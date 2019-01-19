#include "pch.h"
#include "PGSSampleProvider.h"

using namespace CCPlayer::UWP::Common::Codec;

PGSSampleProvider::PGSSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int codePage) : SubtitleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, codePage)
{
}

void PGSSampleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
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

		while (avPacket.size > 0)
		{
			//zlib으로 압축된 데이터의 경우 압축을 헤재 (External libraries 동작으로 주석처리함 2016.07.08)
			//ExtractIfCompressedData(&avPacket, &replacedData, &replacedSize);

			int decodedBytes = avcodec_decode_subtitle2(m_pAvCodecCtx, &sub, &gotSub, &avPacket);
			if (gotSub)
			{
				Array<byte>^ imgData = nullptr;
				Windows::Foundation::Collections::IMap<String^, ImageData^>^ subImgMap = nullptr;
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

					// Graphics subtitle 
					jo->Insert("PictureWidth", JsonValue::CreateNumberValue(m_pAvCodecCtx->coded_width > 0 ? m_pAvCodecCtx->coded_width : m_pAvCodecCtx->width));
					jo->Insert("PictureHeight", JsonValue::CreateNumberValue(m_pAvCodecCtx->coded_height > 0 ? m_pAvCodecCtx->coded_height : m_pAvCodecCtx->height));
					jo->Insert("Left", JsonValue::CreateNumberValue(sub.rects[i]->x));
					jo->Insert("Top", JsonValue::CreateNumberValue(sub.rects[i]->y));
					jo->Insert("Width", JsonValue::CreateNumberValue(sub.rects[i]->w));
					jo->Insert("Height", JsonValue::CreateNumberValue(sub.rects[i]->h));
					jo->Insert("NumColors", JsonValue::CreateNumberValue(sub.rects[i]->nb_colors));

					if (sub.rects[i]->data[0] != nullptr)
					{
						if (subImgMap == nullptr)
						{
							subImgMap = ref new Platform::Collections::Map<String^, ImageData^>();
						}

						int size = sub.rects[i]->w * sub.rects[i]->h;
						imgData = ref new Array<byte>(size * 4);

						for (int j = 0; j < size; j++)
						{
							byte cc = sub.rects[i]->data[0][j];
							int ii = j * 4;
							int ci = cc * 4;
							imgData[ii] = sub.rects[i]->data[1][ci];
							imgData[ii + 1] = sub.rects[i]->data[1][ci + 1];
							imgData[ii + 2] = sub.rects[i]->data[1][ci + 2];
							imgData[ii + 3] = sub.rects[i]->data[1][ci + 3];
						}
						/*for (int j = 0; j < size; j++)
						{
							imgData[i] = sub.rects[i]->data[0][i];
						}*/

						ImageData^ subImg = ref new ImageData();
						subImg->ImagePixelData = imgData;
						subImg->CodecId = m_pAvCodecCtx->codec_id;
						subImgMap->Insert(pName, subImg);
					}

					subPkt->GetNamedArray("Rects")->Append(jo);
				}

				auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
				if (subtitleDecoderConnector != nullptr)
					subtitleDecoderConnector->PopulatePacket(subPkt, subImgMap);
				OutputDebugMessage(L"Completed population of pgs-subtitle packet.\n");

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