//*****************************************************************************
//
//	Copyright 2015 L:me Corporation
//
//*****************************************************************************

#pragma once
#include <queue>
#include <collection.h>

using namespace CCPlayer::UWP::Common::Codec;

using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Media::Core;

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Decoder
			{
				/*public enum struct DecoderType
				{
					AUTO,
					HW,
					SW,
					Hybrid
				};

				public enum struct DecoderStatus
				{
					Requested,
					CheckError,
					Succeeded,
				};

				public ref struct DecoderPayload sealed
				{
				public:
					DecoderPayload() {};
					DecoderPayload(DecoderPayload^ payload) {};
					property DecoderType ReqDecoderType;
					property DecoderType ResDecoderType;
					property DecoderStatus Status;
				};*/

				public ref class DecoderTypeList sealed
				{
				private:
					Windows::Foundation::Collections::IVector<DecoderTypes>^ types;
					int index;

				public:
					property Windows::Foundation::Collections::IVector<DecoderTypes>^ Types 
					{ 
						Windows::Foundation::Collections::IVector<DecoderTypes>^ get(); 
					}

					property DecoderTypes Current
					{
						DecoderTypes get();
						void set(DecoderTypes type);
					}

					property DecoderTypes Next
					{
						DecoderTypes get();
					}

					property DecoderTypes Previous
					{
						DecoderTypes get();
					}

					DecoderTypeList();
					void Reset();
					void Remove(DecoderTypes type);
				};
			}
		}
	}
}
