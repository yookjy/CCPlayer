//////////////////////////////////////////////////////////////////////////
//
// FFmpegByteStreamHandler.h
// Implements the byte-stream handler for the FFmpeg source.
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
// CFFmpegByteStreamHandler  class 
//
// Byte-stream handler for FFmpeg streams.
//-------------------------------------------------------------------

class CFFmpegByteStreamHandler WrlSealed
	: public Microsoft::WRL::RuntimeClass<
	Microsoft::WRL::RuntimeClassFlags< Microsoft::WRL::RuntimeClassType::WinRtClassicComMix >,
	ABI::Windows::Media::IMediaExtension,
	IMFByteStreamHandler
	>
{
	InspectableClass(L"FFmpegSource.FFmpegByteStreamHandler", BaseTrust)
private:
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ m_avDecoderConnector;
	CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector^ m_subtitleDecoderConnector;
	CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ m_attachmentDecoderConnector;
	PropertySet^ m_ffmpegPropertySet;
public:
	CFFmpegByteStreamHandler();
	virtual ~CFFmpegByteStreamHandler();

	// IMediaExtension
	IFACEMETHOD(SetProperties) (ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration);

	// IMFAsyncCallback
	STDMETHODIMP GetParameters(DWORD *pdwFlags, DWORD *pdwQueue)
	{
		// Implementation of this method is optional.
		return E_NOTIMPL;
	}

	// IMFByteStreamHandler
	STDMETHODIMP BeginCreateObject(
		/* [in] */ IMFByteStream *pByteStream,
		/* [in] */ LPCWSTR pwszURL,
		/* [in] */ DWORD dwFlags,
		/* [in] */ IPropertyStore *pProps,
		/* [out] */ IUnknown **ppIUnknownCancelCookie,
		/* [in] */ IMFAsyncCallback *pCallback,
		/* [in] */ IUnknown *punkState);

	STDMETHODIMP EndCreateObject(
		/* [in] */ IMFAsyncResult *pResult,
		/* [out] */ MF_OBJECT_TYPE *pObjectType,
		/* [out] */ IUnknown **ppObject);

	STDMETHODIMP CancelObjectCreation(IUnknown *pIUnknownCancelCookie);
	STDMETHODIMP GetMaxNumberOfBytesRequiredForResolution(QWORD *pqwBytes);
};
