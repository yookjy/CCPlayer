#pragma once
#include <collection.h>
#include "Information\MediaInformation.h"

using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::Common::Interface;
using namespace CCPlayer::UWP::FFmpeg::Decoder;
using namespace CCPlayer::UWP::FFmpeg::Information;
using namespace Windows::Storage;
using namespace Platform;

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Bridge
			{
				public ref class DecoderBridge sealed
				{
				public:
					DecoderBridge();
					virtual ~DecoderBridge();

					static property DecoderBridge^ Instance { DecoderBridge^ get(); }
					void SetResult(DecoderTypes resDecoderType, DecoderStates status);

					property Windows::Foundation::Collections::IVector<CodecInformation^>^ CodecInformationList;
					property INT32 EnforceVideoStreamId;
					property INT32 EnforceAudioStreamId;
					property LONG64 AudioSyncMilliSeconds;
					property bool UseGPUShader;
					property DecoderPayload Payload { DecoderPayload get(); }
					property DOUBLE WindowsVersion;
					property DecoderTypes ReqDecoderType { DecoderTypes get(); void set(DecoderTypes value); }
					property DecoderTypes ResDecoderType { DecoderTypes get() { return _Payload.ResDecoderType; } }
					property DOUBLE AudioVolumeBoost;

				private:
					static DecoderBridge^ _Instance;
					DecoderPayload _Payload;
				};
			}
		}
	}
}
