#include "pch.h"
#include "SubtitleProvider.h"

using namespace CCPlayer::UWP::Common::Codec;

SubtitleProvider::SubtitleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int detectedCodePage) : MFSampleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, CCPlayer::UWP::Common::Codec::DecoderTypes::SW)
	, NeedFrameCount(0)
	, detectedCodePage(detectedCodePage)
	, currCodePage(AUTO_DETECT_CODE_PAGE)
{
	timeBase = av_q2d(m_pAvFormatCtx->streams[m_streamIndex]->time_base) * 10000000L;
}

SubtitleProvider::~SubtitleProvider()
{
	Flush();

	if (m_pAvCodecCtx != nullptr)
	{
		avcodec_free_context(&m_pAvCodecCtx);
	}
}

void SubtitleProvider::LoadHeader()
{
	auto cp = currCodePage;
	if (cp != CP_UTF8 && cp != CP_UCS2LE && cp != CP_UCS2BE)
		//문자열 인코딩
		this->header = ToStringHat((char*)m_pAvCodecCtx->subtitle_header, m_pAvCodecCtx->subtitle_header_size, cp);
	else
		//UTF8, 16LE, 16BE 모두 UTF8을 적용 시킴
		this->header = ToStringHat((char*)m_pAvCodecCtx->subtitle_header, m_pAvCodecCtx->subtitle_header_size, CP_UTF8);
}

HRESULT SubtitleProvider::CreateMediaType(IMFMediaType** mediaType)
{
	return E_NOTIMPL;
}

HRESULT SubtitleProvider::WriteAVPacket(IMFSample** ppSample, AVPacket* avPacket)
{
	return E_NOTIMPL;
}

void SubtitleProvider::WastePackets(int leftCount)
{
	if (leftCount < 0) return;
	while (m_packetQueue.size() > leftCount)
	{
		av_packet_unref(&PopPacket());
	}
}

void SubtitleProvider::PushPacket(AVPacket avPacket)
{
	m_packetQueue.push(avPacket);
	//OutputDebugMessage(L">>>>> Push packet => pts : %I64d \n", avPacket.pts);
}

AVPacket SubtitleProvider::PopPacket()
{
	AVPacket avPacket;
	av_init_packet(&avPacket);
	avPacket.data = NULL;
	avPacket.size = 0;

	if (!m_packetQueue.empty())
	{
		avPacket = m_packetQueue.front();
		m_packetQueue.pop();
	}
	//OutputDebugMessage(L"Pop packet => pts : %I64d >>>>>>\n", avPacket.pts);
	return avPacket;
}

int SubtitleProvider::GetPacketCount()
{
	return m_packetQueue.size();
}

//외부 자막의 경우 큐를 수동으로 채워줘야 하기 때문에, 이 함수를 호출
HRESULT SubtitleProvider::FillPacketQueue()
{
	HRESULT hr = S_OK;
	try
	{
		/*if (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached)
			return EOF;*/

		int ret = 0;
		// Continue reading until there is an appropriate packet in the stream
		while (m_packetQueue.empty())
		{
			ret = m_pReader->ReadPacket();
			if (ret < 0)
			{
				if (ret == AVERROR_EOF || (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached))
				{
					DebugMessage(L"Subtitle packet reaching EOF\n");
					hr = EOF;
					break;
				}
			}
		}
	}
	catch (Exception^ ade)
	{
		DebugMessage(ade->Message->Data());
		hr = E_FAIL;
	}
	return hr;
}

HRESULT SubtitleProvider::FullFillPacketQueue()
{
	HRESULT hr = S_OK;
	try
	{
		int ret = 0;
		// Continue reading until there is an appropriate packet in the stream
		while (true)
		{
			ret = m_pReader->ReadPacket();
			if (ret < 0)
			{
				if (ret == AVERROR_EOF || (m_pAvFormatCtx->pb && m_pAvFormatCtx->pb->eof_reached))
				{
					DebugMessage(L"Subtitle packet reaching EOF\n");
					hr = EOF;
					break;
				}
			}
		}
	}
	catch (Exception^ ade)
	{
		DebugMessage(ade->Message->Data());
		hr = E_FAIL;
	}
	return hr;
}

int64_t SubtitleProvider::GetPacketPts()
{
	AVPacket avPacket;
	av_init_packet(&avPacket);
	avPacket.data = NULL;
	avPacket.size = 0;

	if (!m_packetQueue.empty())
	{
		avPacket = m_packetQueue.front();
		return avPacket.pts;
	}
	return -1;
}

void SubtitleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
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
			//zlib으로 압축된 데이터의 경우 압축을 해제 (External libraries 동작으로 주석처리함 2016.07.08)
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
				subPkt->Insert("EndDisplayTime", JsonValue::CreateNumberValue((double)avPacket.duration * timeBase));
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

					if (sub.format != 0)
					{
						// Non-graphics subtitle
						jo->Insert("Text", JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->text, CP_UTF8)));
						jo->Insert("Ass", JsonValue::CreateStringValue(ToStringHat(sub.rects[i]->ass, CP_UTF8)));
					}
					else
					{
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
					}
					subPkt->GetNamedArray("Rects")->Append(jo);
				}

				auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
				if (subtitleDecoderConnector != nullptr)
					subtitleDecoderConnector->PopulatePacket(subPkt, subImgMap);
				OutputDebugMessage(L"Completed population of subtitle packet.\n");

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

void SubtitleProvider::Flush()
{
	//패킷을 모두 소비
	WastePackets(0);

	if (m_pAvCodecCtx != nullptr)
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}

void SubtitleProvider::LoadHeaderIfCodePageChanged()
{
	//코드 페이지가 변경이 되었으면 헤더 갱신 및 통지
	auto currCP = currCodePage;
	auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
	if (subtitleDecoderConnector != nullptr)
	{
		if (subtitleDecoderConnector->SelectedCodePage == AUTO_DETECT_CODE_PAGE)
		{
			currCodePage = detectedCodePage;
		}
		else
		{
			currCodePage = subtitleDecoderConnector->SelectedCodePage;
		}
	}

	if (currCP != currCodePage)
	{
		//코드 페이지에 따라 헤더를 다시 로드
		LoadHeader();
	}
}

void SubtitleProvider::DecodeCharset(AVPacket* avPacket, byte** replacedData, int* replacedSize)
{
	bool isTextSub = m_pAvCodecCtx->codec_descriptor->props & AV_CODEC_PROP_TEXT_SUB;

	if (isTextSub
		&& currCodePage != 0 && currCodePage != 20127 //ASCII
		&& currCodePage != CP_UTF8 && currCodePage != CP_UCS2LE && currCodePage != CP_UCS2BE) //UNICODE
	{
		WCHAR* output = NULL;
		int cchRequiredSize = 0;
		unsigned int cchActualSize = 0;

		cchRequiredSize = (int)MultiByteToWideChar(currCodePage, 0, (char*)avPacket->data, avPacket->size, output, cchRequiredSize); // determine required buffer size
		output = (WCHAR*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, (cchRequiredSize + 1) * sizeof(wchar_t)); // fix: add 1 to required size and zero memory on alloc
		cchActualSize = (int)MultiByteToWideChar(currCodePage, 0, (char*)avPacket->data, avPacket->size, output, cchRequiredSize);

		if (cchActualSize > 0)
		{
			int repSize = WideCharToMultiByte(CP_UTF8, 0, output, -1, NULL, 0, NULL, NULL);
			if (repSize > 0)
			{
				*replacedData = (byte*)malloc(repSize);
				WideCharToMultiByte(CP_UTF8, 0, output, -1, (char*)*replacedData, repSize, NULL, NULL);

				avPacket->data = *replacedData;
				avPacket->size = repSize;

				*replacedSize = repSize;
			}
		}
		HeapFree(GetProcessHeap(), 0, output);
	}
}

/*
void SubtitleProvider::ExtractIfCompressedData(AVPacket* avPacket, byte** replacedData, int* replacedSize)
{
	//zlib으로 압축된 데이터의 경우 압축을 해제
	if (avPacket->size > 2 && avPacket->data[0] == 0x78 && avPacket->data[1] == 0xDA)
	{
		uLong tmp = avPacket->size * 2 + 13;
		*replacedData = (byte*)malloc(tmp);

		Uncompress(avPacket->data, avPacket->size, replacedData, &tmp);

		*replacedSize = (int)tmp;
		avPacket->data = *replacedData;
		avPacket->size = *replacedSize;
	}
}
*/

void SubtitleProvider::FreeAVPacket(AVPacket* avPacket, byte** replacedData, int* replacedSize)
{
	av_packet_unref(avPacket);
	if (*replacedSize > 0 && *replacedData != nullptr)
	{
		free(*replacedData);
		*replacedData = NULL;
		*replacedSize = 0;
	}
}