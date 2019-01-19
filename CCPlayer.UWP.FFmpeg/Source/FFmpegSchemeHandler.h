//////////////////////////////////////////////////////////////////////////
//
// FFmpegSchemeHandler.h
// Implements the scheme handler for the FFmpeg source.
//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) L:me All rights reserved.
//
//////////////////////////////////////////////////////////////////////////

#pragma once

//-------------------------------------------------------------------
// CFFmpegSchemeHandler  class 
//
// Scheme handler for FFmpeg streams.
//-------------------------------------------------------------------

class CFFmpegSchemeHandler WrlSealed
	: public Microsoft::WRL::RuntimeClass<
	Microsoft::WRL::RuntimeClassFlags< Microsoft::WRL::RuntimeClassType::WinRtClassicComMix >,
	ABI::Windows::Media::IMediaExtension,
	IMFSchemeHandler >
{
	InspectableClass(L"FFmpegSource.FFmpegSchemeHandler", BaseTrust)
private:
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ m_avDecoderConnector;
	CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ m_subtitleDecoderConnector;
	CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ m_attachmentDecoderConnector;
	PropertySet^ m_ffmpegPropertySet;
public:
	CFFmpegSchemeHandler();
	~CFFmpegSchemeHandler();

	// IMediaExtension
	IFACEMETHOD(SetProperties) (ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration);

	// IMFSchemeHandler
	IFACEMETHOD(BeginCreateObject) (
		_In_ LPCWSTR pwszURL,
		_In_ DWORD dwFlags,
		_In_ IPropertyStore *pProps,
		_COM_Outptr_opt_  IUnknown **ppIUnknownCancelCookie,
		_In_ IMFAsyncCallback *pCallback,
		_In_ IUnknown *punkState);

	IFACEMETHOD(EndCreateObject) (
		_In_ IMFAsyncResult *pResult,
		_Out_  MF_OBJECT_TYPE *pObjectType,
		_Out_  IUnknown **ppObject);

	IFACEMETHOD(CancelObjectCreation) (
		_In_ IUnknown *pIUnknownCancelCookie);
};

