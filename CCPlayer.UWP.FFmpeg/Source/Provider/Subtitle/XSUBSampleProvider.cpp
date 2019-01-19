#include "pch.h"
#include "XSUBSampleProvider.h"

using namespace CCPlayer::UWP::Common::Codec;

static const uint8_t tc_offsets[9] = { 0, 1, 3, 4, 6, 7, 9, 10, 11 };
static const uint8_t tc_muls[9] = { 10, 6, 10, 6, 10, 10, 10, 10, 1 };

static uint64_t parse_timecode(const uint8_t *buf) {
	int i;
	int64_t ms = 0;
	if (buf[2] != ':' || buf[5] != ':' || buf[8] != '.')
		return AV_NOPTS_VALUE;
	
	for (i = 0; i < sizeof(tc_offsets); i++) {
		uint8_t c = buf[tc_offsets[i]] - '0';
		if (c > 9) return AV_NOPTS_VALUE;
		ms = (ms + c) * tc_muls[i];
	}
	return ms;
}

XSUBSampleProvider::XSUBSampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int codePage) : SubtitleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, codePage)
{
	int64_t nbFrames = avFormatCtx->streams[streamIndex]->nb_frames;
	NeedFrameCount = nbFrames > 0 ? nbFrames : avFormatCtx->streams[streamIndex]->codec_info_nb_frames;
	m_prevPts = 0;
}

XSUBSampleProvider::~XSUBSampleProvider()
{
	//패킷을 모두 소비
	XSUBPacket xSubPacket;
	while (!m_packetList.empty())
	{
		xSubPacket = m_packetList.back();
		m_packetList.pop_back();
		av_packet_unref(&xSubPacket.Packet);
	}

	Flush();

	if (m_pAvCodecCtx != nullptr)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void XSUBSampleProvider::PushPacket(AVPacket avPacket)
{
	// check that at least header fits
	if (avPacket.size < 27 + 7 * 2 + 4 * 3) {
		//av_log(avctx, AV_LOG_ERROR, "coded frame too small\n");
		return;
	}
	
	// read start and end time
	if (avPacket.data[0] != '[' || avPacket.data[13] != '-' || avPacket.data[26] != ']') {
		//av_log(avctx, AV_LOG_ERROR, "invalid time code\n");
		return;
	}

	auto startDisplayTime = parse_timecode(avPacket.data + 1) * 10000;
	XSUBPacket newPkt = { startDisplayTime, avPacket };

	//m_packetQueue.push(avPacket);
	m_packetList.push_back(newPkt);
}

int XSUBSampleProvider::GetPacketCount()
{
	return m_packetList.size();
}

void XSUBSampleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
{
	if (m_packetList.empty())
		return;

	if (index == this->GetCurrentStreamIndex())
	{
		if (m_prevPts == 0 || m_prevPts >= pts)
		{
			//이전 pts가 0이면 초시 시작 상태, 현재 pts보다 크면 시크로 뒤로 돌아간 상태이므로 이터레이터 초기화
			m_currentItorator = m_packetList.begin();
		}
		//pts설정
		m_prevPts = pts;

		auto itor = std::find_if(m_currentItorator, m_packetList.end(), [=](XSUBPacket packet) {
			return pts >= packet.TimeCode && pts - packet.TimeCode < 5000000; //500ms 이하의 차이에서 pts가 큰것
		});

		if (itor != m_packetList.end())
		{
			m_currentItorator = itor;
			XSUBPacket xSubPacket = *m_currentItorator;
			AVPacket avPacket = xSubPacket.Packet;
			byte* replacedData = nullptr;
			int replacedSize = 0;
			AVSubtitle sub;
			int gotSub = 0;
			//다음 이터레이터
			m_currentItorator = m_currentItorator + 1;

			//코드 페이지가 변경이 되었으면 헤더 갱신 및 통지
			LoadHeaderIfCodePageChanged();

			if (avPacket.size > 0)
			{
				int decodedBytes = avcodec_decode_subtitle2(m_pAvCodecCtx, &sub, &gotSub, &avPacket);
				if (gotSub)
				{
					Array<byte>^ imgData = nullptr;
					Windows::Foundation::Collections::IMap<String^, ImageData^>^ subImgMap = nullptr;

					auto subPkt = ref new JsonObject();
					subPkt->Insert("Pts", JsonValue::CreateNumberValue((double)((uint64_t)sub.start_display_time * 10000L)));
					subPkt->Insert("StartDisplayTime", JsonValue::CreateNumberValue(0.0));
					subPkt->Insert("EndDisplayTime", JsonValue::CreateNumberValue((double)((uint64_t)(sub.end_display_time - sub.start_display_time) * 10000L)));
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
					OutputDebugMessage(L"Completed population of xsub-subtitle packet.\n");
				}
				
				avsubtitle_free(&sub);
			}
			
			if (replacedSize > 0 && replacedData != nullptr)
			{
				free(replacedData);
				replacedData = NULL;
				replacedSize = 0;
			}
		}
		else
		{
			/*if (m_currentItorator != m_packetList.rend())
			{
				auto sp = *m_packetList.rbegin();
				if (pts > sp.TimeCode)
				{
					m_currentItorator = m_packetList.rend();
				}
			}*/
		}
	}
}

AVPacket XSUBSampleProvider::PopPacket()
{
	AVPacket avPacket;
	av_init_packet(&avPacket);
	avPacket.data = NULL;
	avPacket.size = 0;
	return avPacket;
}

void XSUBSampleProvider::WastePackets(int leftCount)
{
}

void XSUBSampleProvider::Flush()
{
	if (m_pAvCodecCtx != NULL)
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}
