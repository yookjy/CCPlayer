#pragma once

#include "Source\Provider\Subtitle\SubtitleProvider.h"
#include "Subtitle\Subtitle.h"

#include <regex>
#include <string>
#include <string.h>

using namespace std::tr1;

class SAMISampleProvider :
	public SubtitleProvider
{
	
	const regex syncPattern;
	const regex pClsPattern;
	const regex keyValPattern;
	const regex linePatern;

public:
	SAMISampleProvider(
		FFmpegReader* reader,
		AVFormatContext* avFormatCtx,
		AVCodecContext* avCodecCtx,
		int streamIndex,
		int codePage);

	virtual void LoadHeader();
	virtual void ConsumePacket(int index, int64_t pts, int64_t syncts);
	Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ GetSubtitleLanguages();
private:
	

	Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ _SubtitleLanguages;
	PropertySet^ _GlobalStyleProperty;
	Windows::Foundation::Collections::IMap<String^, Windows::Data::Json::JsonObject^>^ _BlockStyleMap;
};

