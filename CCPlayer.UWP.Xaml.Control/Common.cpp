#pragma once

#include "pch.h"
#include "Common.h"

//CCPlayer::TextureLock::~TextureLock()
//{
//	assert(_pContext);
//	assert(_pTex);
//	if (_pTex && _bLocked)
//	{
//
//		_pContext->Unmap(_pTex.Get(), _uiIndex);
//		_bLocked = false;
//	}
//}

//HRESULT CCPlayer::TextureLock::Map(UINT uiIndex, D3D11_MAP mapType, UINT mapFlags)
//{
//	HRESULT hr = S_OK;
//	assert(_pTex);
//	assert(_pContext);
//	assert(!_bLocked);
//
//	hr = _pContext->Map(_pTex.Get(), uiIndex, mapType, mapFlags, &map);
//	_bLocked = SUCCEEDED(hr);
//	_uiIndex = uiIndex;
//	return hr;
//}

CCPlayer::UWP::Xaml::Controls::KeyName::KeyName(Object^ key, String^ name)
{
	Key = key;
	Name = name;
}

CCPlayer::UWP::Xaml::Controls::KeyName::KeyName(Object^ key, String^ name, Object^ payload)
{
	Key = key;
	Name = name;
	Payload = payload;
}

CCPlayer::UWP::Xaml::Controls::KeyName::KeyName(Object^ key, String^ name, String^ type, Object^ payload)
{
	Key = key;
	Name = name;
	Type = type;
	Payload = payload;
}

CCPlayer::UWP::Xaml::Controls::ClosedCaptionData::ClosedCaptionData(TimeSpan startTime, TimeSpan endTime, Windows::Data::Json::JsonObject^ jsonCCData, Object^ extraData)
{
	StartTime = startTime;
	EndTime = endTime;
	JsonCCData = jsonCCData;
	ExtraData = extraData;

	auto type = jsonCCData->GetNamedNumber("Type");
	String^ text = nullptr;
	if (type == 2)
	{
		text = jsonCCData->GetNamedString("Text");
	}
	else if (type == 3 || type == 4)
	{
		text = jsonCCData->GetNamedString("Ass");
	}

	Text = text;
}


Array<String^>^ CCPlayer::UWP::Xaml::Controls::MediaFileSuffixes::_CLOSED_CAPTION_SUFFIX =
//{
//	".SRT", ".SMI", ".SSA", ".ASS",  ".TTML", 
//	".VTT", ".SUB", ".RT",  ".GSUB", ".JSS", 
//	".AQT", ".PJS", ".PSB", ".STL",  ".SSF", 
//	".USF", ".IDX", ".SUP"
//};
{
	".SRT", ".SMI", ".SSA", ".ASS"
};

Array<String^>^ CCPlayer::UWP::Xaml::Controls::MediaFileSuffixes::_VIDEO_SUFFIX =
{
	".MP4", ".M4V", ".MP4V",
	".WMV", ".WM", ".WMX",
	".ASF", ".ASR", ".ASF",
	".MOV",
	".AVI", 
	".MKV", ".WEBM",
	".3GP", ".3GPP",
	".3G2", ".3GP2",
	".MTS", ".TS", ".M2TS",
	".MPG", ".MPEG", ".MPE",
	".FLV", 
	".RMVB", 
	".PYV", ".DAT", ".4CCP", 
};
//mime type
//http://www.freeformatter.com/mime-types-list.html

//https://msdn.microsoft.com/ko-kr/library/windows/apps/xaml/mt188703.aspx ÂüÁ¶
/*
.wm
.m4v 
.wmv 
.asf 
.mov 
.mp4 
.3g2 
.3gp 
.mp4v 
.avi 
.pyv
.3gpp
.3gp2
*/