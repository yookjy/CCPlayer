
#include "pch.h"
#include "Decoder.h"

using namespace CCPlayer::UWP::FFmpeg::Decoder;
using namespace Windows::Foundation::Collections;
using namespace Platform::Collections;

DecoderTypeList::DecoderTypeList()
{
	types = ref new Platform::Collections::Vector<DecoderTypes>();
	Reset();
}

IVector<DecoderTypes>^ DecoderTypeList::Types::get()
{
	return types;
}

DecoderTypes DecoderTypeList::Current::get()
{
	return types->GetAt(index);
}

void DecoderTypeList::Current::set(DecoderTypes value)
{
	for (unsigned int i = 0; i < types->Size; i++)
	{
		if (value == types->GetAt(i))
		{
			index = (int)i;
			break;
		}
	}
}

DecoderTypes DecoderTypeList::Next::get()
{
	int tmp = index;
	tmp++;
	if (types->Size <= (unsigned int)tmp)
	{
		tmp = 0;
	}
	return types->GetAt(tmp);
}

DecoderTypes DecoderTypeList::Previous::get()
{
	int tmp = index;
	tmp--;
	if (tmp < 0)
	{
		tmp = types->Size - 1;
	}
	return types->GetAt(tmp);
}

void DecoderTypeList::Reset()
{
	types->Clear();
	types->Append(DecoderTypes::HW);
	types->Append(DecoderTypes::Hybrid);
	types->Append(DecoderTypes::SW);
	Current = DecoderTypes::HW;
}

void DecoderTypeList::Remove(DecoderTypes type)
{
	DecoderTypes next = Next;
	unsigned int tmp = 0;
	if (types->IndexOf(type, &tmp))
	{
		types->RemoveAt(tmp);
	}

	if (type == Current)
	{
		Current = next;
	}
}
