//////////////////////////////////////////////////////////////////////////
//
// FFmpegByteStreamHandler.cpp
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

#include "pch.h"


//#ifdef _DEBUG
//#define _CRTDBG_MAP_ALLOC
//#include <stdlib.h>
//#include <crtdbg.h>
//new (std::throw) 때문에 안되는데 ...
////#ifndef DBG_NEW
////#define DBG_NEW new ( _NORMAL_BLOCK , __FILE__ , __LINE__ )
////#define new DBG_NEW
////#endif
//#endif  // _DEBUG

using namespace CCPlayer::UWP::Common::Interface;

#include <wrl\module.h>
#include "FFmpegSource.h"
#include "FFmpegByteStreamHandler.h"

ActivatableClass(CFFmpegByteStreamHandler);

//-------------------------------------------------------------------
// CFFmpegByteStreamHandler  class
//-------------------------------------------------------------------
//-------------------------------------------------------------------
// Constructor
//-------------------------------------------------------------------

CFFmpegByteStreamHandler::CFFmpegByteStreamHandler()
{
	//_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
	CheckPlatform();
}

//-------------------------------------------------------------------
// Destructor
//-------------------------------------------------------------------

CFFmpegByteStreamHandler::~CFFmpegByteStreamHandler()
{
	//_CrtDumpMemoryLeaks();
}

//-------------------------------------------------------------------
// IMediaExtension methods
//-------------------------------------------------------------------

//-------------------------------------------------------------------
// SetProperties
// Sets the configuration of the media byte stream handler
//-------------------------------------------------------------------
IFACEMETHODIMP CFFmpegByteStreamHandler::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration)
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

HRESULT CFFmpegByteStreamHandler::BeginCreateObject(
	/* [in] */ IMFByteStream *pByteStream,
	/* [in] */ LPCWSTR pwszURL,
	/* [in] */ DWORD dwFlags,
	/* [in] */ IPropertyStore *pProps,
	/* [out] */ IUnknown **ppIUnknownCancelCookie,  // Can be nullptr
	/* [in] */ IMFAsyncCallback *pCallback,
	/* [in] */ IUnknown *punkState                  // Can be nullptr
	)
{
	HRESULT hr = S_OK;
	try
	{
		//최초 요청 상태가 아니면 튕김 (MediaExtensionManager에서 등록한 숫자만큼 무조건 진입하기 때문에, 불필요한 진행을 사전에 방지함.
		if (m_avDecoderConnector != nullptr && m_avDecoderConnector->Payload.Status != DecoderStates::Requested)
		{
			ThrowException(MF_E_CANNOT_PARSE_BYTESTREAM);
		}

		if (pByteStream == nullptr)
		{
			ThrowException(E_POINTER);
		}

		if (pCallback == nullptr)
		{
			ThrowException(E_POINTER);
		}

		if ((dwFlags & MF_RESOLUTION_MEDIASOURCE) == 0)
		{
			ThrowException(E_INVALIDARG);
		}

		ComPtr<IMFAsyncResult> spResult;
		ComPtr<CFFmpegSource> spSource = CFFmpegSource::CreateInstance(m_avDecoderConnector, m_subtitleDecoderConnector, m_attachmentDecoderConnector, m_ffmpegPropertySet);

		ComPtr<IUnknown> spSourceUnk;
		ThrowIfError(spSource.As(&spSourceUnk));
		ThrowIfError(MFCreateAsyncResult(spSourceUnk.Get(), pCallback, punkState, &spResult));

		// Start opening the source. This is an async operation.
		// When it completes, the source will invoke our callback
		// and then we will invoke the caller's callback.
		ComPtr<CFFmpegByteStreamHandler> spThis = this;
		spSource->OpenAsync(pByteStream).then([this, spThis, spResult, spSource](concurrency::task<void>& openTask)
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
		hr = exc->HResult;
	}

	return hr;
}

//-------------------------------------------------------------------
// EndCreateObject
// Completes the BeginCreateObject operation.
//-------------------------------------------------------------------

HRESULT CFFmpegByteStreamHandler::EndCreateObject(
	/* [in] */ IMFAsyncResult *pResult,
	/* [out] */ MF_OBJECT_TYPE *pObjectType,
	/* [out] */ IUnknown **ppObject)
{
	if (pResult == nullptr || pObjectType == nullptr || ppObject == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = S_OK;

	*pObjectType = MF_OBJECT_INVALID;
	*ppObject = nullptr;

	hr = pResult->GetStatus();

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


HRESULT CFFmpegByteStreamHandler::CancelObjectCreation(IUnknown *pIUnknownCancelCookie)
{
	return E_NOTIMPL;
}

HRESULT CFFmpegByteStreamHandler::GetMaxNumberOfBytesRequiredForResolution(QWORD* pqwBytes)
{
	return E_NOTIMPL;
}

