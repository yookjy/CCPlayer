#include "pch.h"
#include "SAMISampleProvider.h"

using namespace CCPlayer::UWP::FFmpeg::Subtitle;

SAMISampleProvider::SAMISampleProvider(
	FFmpegReader* reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	int streamIndex,
	int codePage) : SubtitleProvider(reader, avFormatCtx, avCodecCtx, streamIndex, codePage)
	, syncPattern("[\\s]*.+[\\s]+Start[\\s]*=(\\d+)>(.*)", regex_constants::ECMAScript | regex_constants::icase)
	, pClsPattern("<\\s*P\\s+(.+?)>(.*?)")
	, keyValPattern("(\\w+?)=(\\w+)")
	, linePatern("\r\n|\n", regex_constants::ECMAScript | regex_constants::icase)
{
}

void SAMISampleProvider::LoadHeader()
{
	auto cp = currCodePage;
	if (cp != CP_UTF8 && cp != CP_UCS2LE && cp != CP_UCS2BE)
		//문자열 인코딩
		this->header = ToStringHat((char*)m_pAvCodecCtx->extradata, m_pAvCodecCtx->extradata_size, cp);
	else
		//UTF8, 16LE, 16BE 모두 UTF8을 적용 시킴
		this->header = ToStringHat((char*)m_pAvCodecCtx->extradata, m_pAvCodecCtx->extradata_size, CP_UTF8);
	SubtitleHelper::LoadSAMIHeader(header, &title, &_GlobalStyleProperty, &_BlockStyleMap, &_SubtitleLanguages);
}

void SAMISampleProvider::ConsumePacket(int index, int64_t pts, int64_t syncts)
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

		//코드 페이지가 변경이 되었으면 헤더 갱신 및 통지
		LoadHeaderIfCodePageChanged();

		//문자셋 디코드
		DecodeCharset(&avPacket, &replacedData, &replacedSize);

		if (avPacket.size > 0)
		{
			int decodedBytes = avcodec_decode_subtitle2(m_pAvCodecCtx, &sub, &gotSub, &avPacket);
			if (decodedBytes < 0)
			{
				OutputDebugMessage(L"[avcodec_decode_subtitle2() failed] Decoded bytes < 0 !!! -> Sami processing \n");
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
			subPkt->GetNamedArray("Rects")->Append(jo);

			std::wstring guid(Guid(result).ToString()->Data());
			String^ pName = ref new String(guid.substr(1, guid.length() - 2).c_str());
				
			jo->Insert("Guid", JsonValue::CreateStringValue(pName));
			jo->Insert("Type", JsonValue::CreateNumberValue((double)SubtitleContentTypes::Sami));
			jo->Insert("Text", JsonValue::CreateStringValue(""));
			//?? ffmpeg 버그?
			std::string txt((char*)avPacket.data);
			txt = regex_replace(txt, linePatern, "");

			smatch m;
			if (regex_search(txt, m, syncPattern))
			{
				txt = m[2].str();
				std::tr1::sregex_token_iterator token(txt.begin(), txt.end(), pClsPattern, { -1, 1 });
				std::sregex_token_iterator end;

				bool isFirst = true;
				while (token != end)
				{
					auto val = *token;

					if (val.matched)
					{
						if (isFirst)
						{
							isFirst = false;
							String^ langCode = nullptr;
							String^ id = nullptr;

							std::string txt2(val);
							smatch sm2;
							while (regex_search(txt2, sm2, keyValPattern))
							{
								auto sKey = sm2[1].str();
								auto sVal = sm2[2].str();

								std::transform(sKey.begin(), sKey.end(), sKey.begin(), tolower);
								if (sKey == "class")
								{
									langCode = ToStringHat(sVal.c_str(), CP_UTF8);
								}
								else if (sKey == "id")
								{
									id = ToStringHat(sVal.c_str(), CP_UTF8);
								}

								txt2 = sm2.suffix();
							}

							jo->Insert("Lang", JsonValue::CreateStringValue(langCode));
							jo->Insert("Id", JsonValue::CreateStringValue(id));
							jo->Insert("Ass", JsonValue::CreateStringValue(""));
						}
						else
						{
							auto text = ToStringHat(val.str().c_str(), CP_UTF8);
							jo->SetNamedValue("Ass", JsonValue::CreateStringValue(text));
						}
					}
					++token;
				}
			}
			
			auto subtitleDecoderConnector = m_pReader->GetSubtitleDecoderConnector();
			if (subtitleDecoderConnector != nullptr)
			{
				SubtitleHelper::ApplySAMIStyle(subPkt, _GlobalStyleProperty, _BlockStyleMap, _SubtitleLanguages);
				subtitleDecoderConnector->PopulatePacket(subPkt, nullptr);
//				OutputDebugMessage(L"Completed population of srt-subtitle packet.\n");
			}

			avsubtitle_free(&sub);
		}

		FreeAVPacket(&avPacket, &replacedData, &replacedSize);
	}
	else
	{
		this->WastePackets(1);
	}
}

Windows::Foundation::Collections::IVector<SubtitleLanguage^>^ SAMISampleProvider::GetSubtitleLanguages()
{
	return _SubtitleLanguages;
}