//////////////////////////////////////////////////////////////////////////
//
// CFFmpegSchemeHandler.cpp
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

#include "pch.h"

using namespace CCPlayer::UWP::Common::Interface;

#include "FFmpegSource.h"
#include "FFmpegSchemeHandler.h"
#include <wrl\module.h>

ActivatableClass(CFFmpegSchemeHandler);

CFFmpegSchemeHandler::CFFmpegSchemeHandler(void)
{
	CheckPlatform();
}

CFFmpegSchemeHandler::~CFFmpegSchemeHandler(void)
{
}

//-------------------------------------------------------------------
// IMediaExtension methods
//-------------------------------------------------------------------

//-------------------------------------------------------------------
// SetProperties
// Sets the configuration of the media byte stream handler
//-------------------------------------------------------------------
IFACEMETHODIMP CFFmpegSchemeHandler::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration)
{
	if (pConfiguration != nullptr)
	{
		auto configurartion = reinterpret_cast<Windows::Foundation::Collections::IPropertySet^>(pConfiguration);
		if (configurartion->HasKey(AV_DEC_CONN))
		{
			m_avDecoderConnector = dynamic_cast<IAVDecoderConnector^>(configurartion->Lookup(AV_DEC_CONN));
		}
		if (configurartion->HasKey(SUB_DEC_CONN))
		{
			m_subtitleDecoderConnector = dynamic_cast<ISubtitleDecoderConnector^>(configurartion->Lookup(SUB_DEC_CONN));
		}
		if (configurartion->HasKey(ATC_DEC_CONN))
		{
			m_attachmentDecoderConnector = dynamic_cast<IAttachmentDecoderConnector^>(configurartion->Lookup(ATC_DEC_CONN));
		}
		if (configurartion->HasKey(FFMPEG_OPTION))
		{
			m_ffmpegPropertySet = dynamic_cast<PropertySet^>(configurartion->Lookup(FFMPEG_OPTION));
		}
	}
	return S_OK;
}

//-------------------------------------------------------------------
// IMFByteStreamHandler methods
//-------------------------------------------------------------------


//-------------------------------------------------------------------
// BeginCreateObject
// Starts creating the media source.
//-------------------------------------------------------------------

IFACEMETHODIMP CFFmpegSchemeHandler::BeginCreateObject(
	_In_ LPCWSTR pwszURL,
	_In_ DWORD dwFlags,
	_In_ IPropertyStore *pProps,
	_COM_Outptr_opt_  IUnknown **ppIUnknownCancelCookie,
	_In_ IMFAsyncCallback *pCallback,
	_In_ IUnknown *punkState)
{
	HRESULT hr = S_OK;
	ComPtr<CFFmpegSource> spSource;
	try
	{
		//최초 요청 상태가 아니면 튕김 (MediaExtensionManager에서 등록한 숫자만큼 무조건 진입하기 때문에, 불필요한 진행을 사전에 방지함.
		if (m_avDecoderConnector != nullptr && m_avDecoderConnector->Payload.Status != DecoderStates::Requested)
		{
			ThrowException(MF_E_UNSUPPORTED_BYTESTREAM_TYPE);
		}

		if (pwszURL == nullptr || pCallback == nullptr)
		{
			ThrowException(E_POINTER);
		}

		/*if ((dwFlags & MF_RESOLUTION_BYTESTREAM) == 0)
		{
			ThrowException(E_INVALIDARG);
		}*/

		if ((dwFlags & MF_RESOLUTION_MEDIASOURCE) == 0)
		{
			//스트림 등의 경우 미디어 소스가 아니므로 여기서 리턴시킴.
			ThrowException(E_ACCESSDENIED);
			//ThrowException(MF_E_UNSUPPORTED_BYTESTREAM_TYPE);
		}

		ComPtr<IMFAsyncResult> spResult;
		spSource = CFFmpegSource::CreateInstance(m_avDecoderConnector, m_subtitleDecoderConnector, m_attachmentDecoderConnector, m_ffmpegPropertySet);

		ComPtr<IUnknown> spSourceUnk;
		ThrowIfError(spSource.As(&spSourceUnk));
		ThrowIfError(MFCreateAsyncResult(spSourceUnk.Get(), pCallback, punkState, &spResult));

		// Start opening the source. This is an async operation.
		// When it completes, the source will invoke our callback
		// and then we will invoke the caller's callback.
		ComPtr<CFFmpegSchemeHandler> spThis = this;
		spSource->OpenURLAsync(pwszURL).then([this, spThis, spResult, spSource](concurrency::task<void>& openTask)
		{
			try
			{
				if (spResult == nullptr)
				{
					ThrowIfError(MF_E_UNEXPECTED);
				}

				openTask.get();
			}
			catch (Exception ^exc)
			{
				if (spResult != nullptr)
				{
					spResult->SetStatus(exc->HResult);
				}
			}

			if (spResult != nullptr)
			{
				MFInvokeCallback(spResult.Get());
			}
		});

		if (ppIUnknownCancelCookie)
		{
			*ppIUnknownCancelCookie = nullptr;
		}
	}
	catch (Exception ^exc)
	{
		if (spSource != nullptr)
		{

			spSource->Shutdown();

		}
		hr = exc->HResult;
	}

	return hr;
}

//-------------------------------------------------------------------
// EndCreateObject
// Completes the BeginCreateObject operation.
//-------------------------------------------------------------------

IFACEMETHODIMP CFFmpegSchemeHandler::EndCreateObject(
	_In_ IMFAsyncResult *pResult,
	_Out_  MF_OBJECT_TYPE *pObjectType,
	_Out_  IUnknown **ppObject)
{
	if (pResult == nullptr || pObjectType == nullptr || ppObject == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT hr = pResult->GetStatus();
	*pObjectType = MF_OBJECT_INVALID;
	*ppObject = nullptr;

	if (SUCCEEDED(hr))
	{
		ComPtr<IUnknown> punkSource;
		hr = pResult->GetObject(&punkSource);
		if (SUCCEEDED(hr))
		{
			*pObjectType = MF_OBJECT_MEDIASOURCE;
			*ppObject = punkSource.Detach();
		}
	}

	return hr;
}

IFACEMETHODIMP CFFmpegSchemeHandler::CancelObjectCreation(
	_In_ IUnknown *pIUnknownCancelCookie)
{
	return E_NOTIMPL;
}