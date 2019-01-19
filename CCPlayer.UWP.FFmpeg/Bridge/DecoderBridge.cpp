#include "pch.h"
#include "DecoderBridge.h"

using namespace CCPlayer::UWP::FFmpeg::Decoder;
using namespace CCPlayer::UWP::FFmpeg::Information;
using namespace CCPlayer::UWP::FFmpeg::Bridge;
using namespace Platform;

DecoderBridge::DecoderBridge()
{
	UseGPUShader = true;
	EnforceAudioStreamId = -1;
	CodecInformationList = ref new Platform::Collections::Vector<CodecInformation^>();
}

DecoderBridge::~DecoderBridge()
{
}

DecoderBridge^ DecoderBridge::_Instance;

DecoderBridge^ DecoderBridge::Instance::get()
{
	if (_Instance == nullptr)
	{
		_Instance = ref new DecoderBridge();
	}
	return _Instance;
}

void DecoderBridge::ReqDecoderType::set(DecoderTypes reqDecoderType)
{
	CodecInformationList->Clear();
	
	_Payload.ReqDecoderType = reqDecoderType;
	_Payload.ResDecoderType = reqDecoderType;
	_Payload.Status = DecoderStates::Requested;
}

DecoderTypes DecoderBridge::ReqDecoderType::get()
{
	return _Payload.ReqDecoderType;
}

DecoderPayload DecoderBridge::Payload::get()
{
	return _Payload;
}

void DecoderBridge::SetResult(DecoderTypes resDecoderType, DecoderStates status)
{
	_Payload.ResDecoderType = resDecoderType;
	_Payload.Status = status;
}