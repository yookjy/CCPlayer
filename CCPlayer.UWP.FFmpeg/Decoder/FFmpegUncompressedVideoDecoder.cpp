//////////////////////////////////////////////////////////////////////////
//
// FFmpegUncompressedVideoDecoder.cpp
// Implements the FFmpeg video decoder.
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
#include "FFmpegUncompressedVideoDecoder.h"
#include "VideoBufferLock.h"
#include <wrl\module.h>
#include <time.h>
#include <array>
#include "TextureLock.h"
#include "ColorSpaceConvertModule.h"
#include "Common\FFmpegMacro.h"

using namespace CCPlayer::UWP::Common::Interface;

/*class CStopWatch
{
private:
	clock_t start;
	clock_t finish;

public:
	double GetDuration() { return (double)(finish - start) / CLOCKS_PER_SEC; }
	void Start() { start = clock(); }
	void Stop()  { finish = clock(); }
};*/ 

ActivatableClass(CFFmpegUncompressedVideoDecoder);

ComPtr<ID3D11Texture2D> BufferToDXType(IMFMediaBuffer *pBuffer, _Out_ UINT *uiViewIndex);

//-------------------------------------------------------------------
// CFFmpegUncompressedVideoDecoder class
//-------------------------------------------------------------------

CFFmpegUncompressedVideoDecoder::CFFmpegUncompressedVideoDecoder() :
	m_imageWidthInPixels(0),
	m_imageHeightInPixels(0),
	m_cbImageSize(0),
	m_pSwsCtx(NULL),
	m_pAvCodecCtx(NULL),
	m_pAvFrame(NULL),
	m_avFrameComplete(0),
	m_timeBase(0),
	m_bitRate(0),
	m_fStreamingInitialized(false),
	m_fGpuShader(false)
{
	//Velostep 앱 체크
	CheckPlatform();

	m_frameRate.Numerator = m_frameRate.Denominator = 0;

	avcodec_register_all();
}

CFFmpegUncompressedVideoDecoder::~CFFmpegUncompressedVideoDecoder()
{
	FreeStreamingResources();
}

// Initialize the instance.
STDMETHODIMP CFFmpegUncompressedVideoDecoder::RuntimeClassInitialize()
{
	HRESULT hr = S_OK;

	try
	{
		// Create the attribute store.
		ThrowIfError(MFCreateAttributes(m_spAttributes.ReleaseAndGetAddressOf(), 3));

		// MFT supports DX11 acceleration
		ThrowIfError(m_spAttributes->SetUINT32(MF_SA_D3D_AWARE, 1));
		ThrowIfError(m_spAttributes->SetUINT32(MF_SA_D3D11_AWARE, 1));
		// output attributes
		ThrowIfError(MFCreateAttributes(m_spOutputAttributes.ReleaseAndGetAddressOf(), 1));
		// Load the transform
		m_transform = ref new CColorSpaceConvertModule();
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}

// IMediaExtension methods

//-------------------------------------------------------------------
// Name: SetProperties
// Sets the configuration of the decoder
//-------------------------------------------------------------------
IFACEMETHODIMP CFFmpegUncompressedVideoDecoder::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration)
{
	auto configurartion = reinterpret_cast<Windows::Foundation::Collections::IPropertySet^>(pConfiguration);
	if (configurartion->HasKey(AV_DEC_CONN))
	{
		m_avDecoderConnector = dynamic_cast<IAVDecoderConnector^>(configurartion->Lookup(AV_DEC_CONN));
	}
	return S_OK;
}

// IMFTransform methods. Refer to the Media Foundation SDK documentation for details.

//-------------------------------------------------------------------
// Name: GetStreamLimits
// Returns the minimum and maximum number of streams.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetStreamLimits(
	DWORD   *pdwInputMinimum,
	DWORD   *pdwInputMaximum,
	DWORD   *pdwOutputMinimum,
	DWORD   *pdwOutputMaximum
	)
{

	if ((pdwInputMinimum == nullptr) ||
		(pdwInputMaximum == nullptr) ||
		(pdwOutputMinimum == nullptr) ||
		(pdwOutputMaximum == nullptr))
	{
		return E_POINTER;
	}


	// This MFT has a fixed number of streams.
	*pdwInputMinimum = 1;
	*pdwInputMaximum = 1;
	*pdwOutputMinimum = 1;
	*pdwOutputMaximum = 1;

	return S_OK;
}


//-------------------------------------------------------------------
// Name: GetStreamCount
// Returns the actual number of streams.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetStreamCount(
	DWORD   *pcInputStreams,
	DWORD   *pcOutputStreams
	)
{
	if ((pcInputStreams == nullptr) || (pcOutputStreams == nullptr))

	{
		return E_POINTER;
	}

	// This MFT has a fixed number of streams.
	*pcInputStreams = 1;
	*pcOutputStreams = 1;

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetStreamIDs
// Returns stream IDs for the input and output streams.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetStreamIDs(
	DWORD   /*dwInputIDArraySize*/,
	DWORD   * /*pdwInputIDs*/,
	DWORD   /*dwOutputIDArraySize*/,
	DWORD   * /*pdwOutputIDs*/
	)
{
	// Do not need to implement, because this MFT has a fixed number of
	// streams and the stream IDs match the stream indexes.
	return E_NOTIMPL;
}


//-------------------------------------------------------------------
// Name: GetInputStreamInfo
// Returns information about an input stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetInputStreamInfo(
	DWORD                     dwInputStreamID,
	MFT_INPUT_STREAM_INFO *   pStreamInfo
	)
{
	if (pStreamInfo == nullptr)
	{
		return E_POINTER;
	}

	if (!IsValidInputStream(dwInputStreamID))
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	pStreamInfo->hnsMaxLatency = 0;

	//  We can process data on any boundary.
	pStreamInfo->dwFlags = 0;

	pStreamInfo->cbSize = 1;
	pStreamInfo->cbMaxLookahead = 0;
	pStreamInfo->cbAlignment = 1;

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetOutputStreamInfo
// Returns information about an output stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetOutputStreamInfo(
	DWORD                     dwOutputStreamID,
	MFT_OUTPUT_STREAM_INFO *  pStreamInfo
	)
{
	//DebugMessage(L"VideoDecoder::GetOutputStreamInfo\r\n");
	if (pStreamInfo == nullptr)
	{
		return E_POINTER;
	}

	if (!IsValidOutputStream(dwOutputStreamID))
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	AutoLock lock(m_critSec);

	// NOTE: This method should succeed even when there is no media type on the
	//       stream. If there is no media type, we only need to fill in the dwFlags
	//       member of MFT_OUTPUT_STREAM_INFO. The other members depend on having a
	//       a valid media type.

	pStreamInfo->dwFlags =
		MFT_OUTPUT_STREAM_WHOLE_SAMPLES |
		MFT_OUTPUT_STREAM_SINGLE_SAMPLE_PER_BUFFER |
		MFT_OUTPUT_STREAM_FIXED_SAMPLE_SIZE;


	if (m_spDX11Manager != nullptr)
	{
		pStreamInfo->dwFlags |= MFT_OUTPUT_STREAM_PROVIDES_SAMPLES;
	}
	
	pStreamInfo->cbSize = m_spInputType == nullptr ? 0 : m_cbImageSize;
	pStreamInfo->cbAlignment = 0;

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetAttributes
// Returns the attributes for the MFT.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetAttributes(IMFAttributes** ppAttributes)
{
	// This MFT does not support any attributes, so the method is not implemented.
	//return E_NOTIMPL;
	//DirectX 추가
	if (ppAttributes == nullptr)
	{
		return E_POINTER;
	}

	AutoLock lock(m_critSec);

	m_spAttributes.CopyTo(ppAttributes);

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetInputStreamAttributes
// Returns stream-level attributes for an input stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetInputStreamAttributes(
	DWORD           /*dwInputStreamID*/,
	IMFAttributes   ** /*ppAttributes*/
	)
{
	// This MFT does not support any attributes, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: GetOutputStreamAttributes
// Returns stream-level attributes for an output stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetOutputStreamAttributes(
	DWORD           dwOutputStreamID,
	IMFAttributes   ** ppAttributes
	)
{
	// This MFT does not support any attributes, so the method is not implemented.
	//return E_NOTIMPL;
	//DirectX 추가
	HRESULT hr = S_OK;

	if (nullptr == ppAttributes)
	{
		return E_POINTER;
	}

	if (dwOutputStreamID != 0)
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	AutoLock lock(m_critSec);

	m_spOutputAttributes.CopyTo(ppAttributes);

	return(hr);
}



//-------------------------------------------------------------------
// Name: DeleteInputStream
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::DeleteInputStream(DWORD /*dwStreamID*/)
{
	// This MFT has a fixed number of input streams, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: AddInputStreams
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::AddInputStreams(
	DWORD   /*cStreams*/,
	DWORD   * /*adwStreamIDs*/
	)
{
	// This MFT has a fixed number of output streams, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: GetInputAvailableType
// Description: Return a preferred input type.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetInputAvailableType(
	DWORD           /*dwInputStreamID*/,
	DWORD           /*dwTypeIndex*/,
	IMFMediaType    ** /*ppType*/
	)
{
	return MF_E_NO_MORE_TYPES;
}



//-------------------------------------------------------------------
// Name: GetOutputAvailableType
// Description: Return a preferred output type.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetOutputAvailableType(
	DWORD           dwOutputStreamID,
	DWORD           dwTypeIndex, // 0-based
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"VideoDecoder::GetOutputAvailableType\r\n");
	HRESULT hr = S_OK;
	try
	{
		if (ppType == nullptr)
		{
			throw ref new InvalidArgumentException();
		}

		if (!IsValidOutputStream(dwOutputStreamID))
		{
			ThrowException(MF_E_INVALIDSTREAMNUMBER);
		}

		if (dwTypeIndex != 0)
		{
			return MF_E_NO_MORE_TYPES;
		}

		AutoLock lock(m_critSec);

		ComPtr<IMFMediaType> spOutputType;

		if (m_spInputType == nullptr)
		{
			return MF_E_TRANSFORM_TYPE_NOT_SET;
		}

		ThrowIfError(MFCreateMediaType(&spOutputType));
		ThrowIfError(spOutputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));

		m_fGpuShader = m_avDecoderConnector != nullptr && m_avDecoderConnector->UseGPUShader
			&& (m_pAvCodecCtx->pix_fmt == AV_PIX_FMT_YUV420P
			|| m_pAvCodecCtx->pix_fmt == AV_PIX_FMT_YUV420P10
			|| m_pAvCodecCtx->pix_fmt == AV_PIX_FMT_YUV420P10BE
			|| m_pAvCodecCtx->pix_fmt == AV_PIX_FMT_YUV420P10LE);

		//데탑에서 GPU Shader를 사용해야 하는 경우 NVidia 체크
		if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Mobile" && m_fGpuShader)
		{
			bool useDX11 = false;
			//임시 처리 (NVidia 에서 디코드 오류)
			/**********************************************************/
			/*
			http://pcidatabase.com/vendors.php?sort=id
			Nvidia: 0x10DE
			AMD: 0x1002, 0x1022
			Intel: 0x163C, 0x8086, 0x8087
			*/
			UINT i = 0;
			ComPtr<IDXGIFactory1> spFactory;
			ThrowIfError(CreateDXGIFactory1(IID_PPV_ARGS(&spFactory)));

			ComPtr<IDXGIAdapter> spAdapter;
			while (spFactory->EnumAdapters(i++, &spAdapter) != DXGI_ERROR_NOT_FOUND)
			{
				DXGI_ADAPTER_DESC pAdpaterDesc;
				spAdapter->GetDesc(&pAdpaterDesc);

				std::wstring desc(pAdpaterDesc.Description);
				if (desc.find(L"Microsoft", 0) != std::wstring::npos || pAdpaterDesc.VendorId == 0x1414) //Microsoft Basic Render Driver
				{
					continue;
				}

				switch (pAdpaterDesc.VendorId)
				{
				case 0x1002: //AMD
				case 0x1022: //AMD
				case 0x163C: //Intel
				case 0x8086: //Intel
				case 0x8087: //Intel
					m_fGpuShader = true;
					break;
				case 0x10DE: //Nvidia의 경우 Shader 비활성화
					m_fGpuShader = false;
					break;
				default:
					//DX9의 경우 0 : Shader 비활성화
					m_fGpuShader = false;
					break;
				}
				//첫번째 드라이버 찾으면 종료
				break;
			}
			/**********************************************************/
		}

		if (m_fGpuShader)
		{
			ThrowIfError(spOutputType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_ARGB32)); 
		}
		else
		{
			ThrowIfError(spOutputType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_NV12));
		}
		
		ThrowIfError(spOutputType->SetUINT32(MF_MT_FIXED_SIZE_SAMPLES, TRUE));
		ThrowIfError(spOutputType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
		ThrowIfError(spOutputType->SetUINT32(MF_MT_SAMPLE_SIZE, m_cbImageSize));
		ThrowIfError(MFSetAttributeSize(spOutputType.Get(), MF_MT_FRAME_SIZE, m_imageWidthInPixels, m_imageHeightInPixels));
		ThrowIfError(MFSetAttributeRatio(spOutputType.Get(), MF_MT_FRAME_RATE, m_frameRate.Numerator, m_frameRate.Denominator));
		ThrowIfError(spOutputType->SetUINT32(MF_MT_INTERLACE_MODE, m_interlace));
		ThrowIfError(MFSetAttributeRatio(spOutputType.Get(), MF_MT_PIXEL_ASPECT_RATIO, m_aspectRatio.Numerator, m_aspectRatio.Denominator));
		if (m_bitRate > 0)
		{
			ThrowIfError(spOutputType->SetUINT64(MF_MT_AVG_BITRATE, m_bitRate));
		}

		*ppType = spOutputType.Detach();
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: SetInputType
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::SetInputType(
	DWORD           dwInputStreamID,
	IMFMediaType    *pType, // Can be nullptr to clear the input type.
	DWORD           dwFlags
	)
{
	HRESULT hr = S_OK;
	//DebugMessage(L"VideoDecoder::SetInputType\r\n");

	try
	{
		if (!IsValidInputStream(dwInputStreamID))
		{
			ThrowException(MF_E_INVALIDSTREAMNUMBER);
		}

		// Validate flags.
		if (dwFlags & ~MFT_SET_TYPE_TEST_ONLY)
		{
			throw ref new InvalidArgumentException();
		}

		AutoLock lock(m_critSec);

		// Does the caller want us to set the type, or just test it?
		bool fReallySet = ((dwFlags & MFT_SET_TYPE_TEST_ONLY) == 0);

		// If we have an input sample, the client cannot change the type now.
		if (HasPendingOutput())
		{
			ThrowException(MF_E_TRANSFORM_CANNOT_CHANGE_MEDIATYPE_WHILE_PROCESSING);
		}

		// Validate the type, if non-nullptr.
		if (pType != nullptr)
		{
			OnCheckInputType(pType);
		}

		// The type is OK.
		// Set the type, unless the caller was just testing.
		if (fReallySet)
		{
			OnSetInputType(pType);

			//DirextX 추가
			// When the type changes, end streaming.
			m_fStreamingInitialized = false;
		}
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: SetOutputType
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::SetOutputType(
	DWORD           dwOutputStreamID,
	IMFMediaType    *pType, // Can be nullptr to clear the output type.
	DWORD           dwFlags
	)
{
	//DebugMessage(L"VideoDecoder::SetOutputType\r\n");
	HRESULT hr = S_OK;
	try
	{
		if (!IsValidOutputStream(dwOutputStreamID))
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		// Validate flags.
		if (dwFlags & ~MFT_SET_TYPE_TEST_ONLY)
		{
			return E_INVALIDARG;
		}

		AutoLock lock(m_critSec);

		// Does the caller want us to set the type, or just test it?
		bool fReallySet = ((dwFlags & MFT_SET_TYPE_TEST_ONLY) == 0);

		// If we have an input sample, the client cannot change the type now.
		if (HasPendingOutput())
		{
			ThrowException(MF_E_TRANSFORM_CANNOT_CHANGE_MEDIATYPE_WHILE_PROCESSING);
		}

		// Validate the type, if non-nullptr.
		if (pType != nullptr)
		{
			OnCheckOutputType(pType);
		}

		if (fReallySet)
		{
			// The type is OK.
			// Set the type, unless the caller was just testing.
			OnSetOutputType(pType);

			//directX 추가
			// When the type changes, end streaming.
			m_fStreamingInitialized = false;
		}
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: GetInputCurrentType
// Description: Returns the current input type.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetInputCurrentType(
	DWORD           dwInputStreamID,
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"VideoDecoder::GetInputCurrentType\r\n");
	if (ppType == nullptr)
	{
		return E_POINTER;
	}

	if (!IsValidInputStream(dwInputStreamID))
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	if (m_spInputType == nullptr)
	{
		hr = MF_E_TRANSFORM_TYPE_NOT_SET;
	}

	if (SUCCEEDED(hr))
	{
		*ppType = m_spInputType.Get();
		(*ppType)->AddRef();
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: GetOutputCurrentType
// Description: Returns the current output type.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetOutputCurrentType(
	DWORD           dwOutputStreamID,
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"VideoDecoder::GetInputCurrentType\r\n");
	if (ppType == nullptr)
	{
		return E_POINTER;
	}

	if (!IsValidOutputStream(dwOutputStreamID))
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	if (m_spOutputType == nullptr)
	{
		hr = MF_E_TRANSFORM_TYPE_NOT_SET;
	}

	if (SUCCEEDED(hr))
	{
		*ppType = m_spOutputType.Get();
		(*ppType)->AddRef();
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: GetInputStatus
// Description: Query if the MFT is accepting more input.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetInputStatus(
	DWORD           dwInputStreamID,
	DWORD           *pdwFlags
	)
{
	//DebugMessage(L"VideoDecoder::GetInputStatus\r\n");
	if (pdwFlags == nullptr)
	{
		return E_POINTER;
	}

	if (!IsValidInputStream(dwInputStreamID))
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	AutoLock lock(m_critSec);

	// If we already have an input sample, we don't accept
	// another one until the client calls ProcessOutput or Flush.
	if (HasPendingOutput())
	{
		*pdwFlags = MFT_INPUT_STATUS_ACCEPT_DATA;
	}
	else
	{
		*pdwFlags = 0;
	}

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetOutputStatus
// Description: Query if the MFT can produce output.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::GetOutputStatus(DWORD *pdwFlags)
{
	//DebugMessage(L"VideoDecoder::GetOutputStatus\r\n");
	if (pdwFlags == nullptr)
	{
		return E_POINTER;
	}

	AutoLock lock(m_critSec);

	// We can produce an output sample if (and only if)
	// we have an input sample.
	if (HasPendingOutput())
	{
		*pdwFlags = MFT_OUTPUT_STATUS_SAMPLE_READY;
	}
	else
	{
		*pdwFlags = 0;
	}

	return S_OK;
}



//-------------------------------------------------------------------
// Name: SetOutputBounds
// Sets the range of time stamps that the MFT will output.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::SetOutputBounds(
	LONGLONG        /*hnsLowerBound*/,
	LONGLONG        /*hnsUpperBound*/
	)
{
	// Implementation of this method is optional.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: ProcessEvent
// Sends an event to an input stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::ProcessEvent(
	DWORD              /*dwInputStreamID*/,
	IMFMediaEvent      * /*pEvent */
	)
{
	// This MFT does not handle any stream events, so the method can
	// return E_NOTIMPL. This tells the pipeline that it can stop
	// sending any more events to this MFT.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: ProcessMessage
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::ProcessMessage(
	MFT_MESSAGE_TYPE    eMessage,
	ULONG_PTR           ulParam
	)
{
	//DebugMessage(L"VideoDecoder::ProcessMessage\r\n");
	HRESULT hr = S_OK;

	try
	{
		AutoLock lock(m_critSec);

		ComPtr<IMFDXGIDeviceManager> pDXGIDeviceManager;

		switch (eMessage)
		{
		case MFT_MESSAGE_COMMAND_FLUSH:
			// Flush the MFT.
			OnFlush();
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_COMMAND_FLUSH\n");
			break;

		case MFT_MESSAGE_COMMAND_DRAIN:
			// Set the discontinuity flag on all of the input.
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_COMMAND_DRAIN\n");
			OnDiscontinuity();
			break;

		case MFT_MESSAGE_SET_D3D_MANAGER:
			// The pipeline should never send this message unless the MFT
			// has the MF_SA_D3D_AWARE attribute set to TRUE. However, if we
			// do get this message, it's invalid and we don't implement it.
			//ThrowException(E_NOTIMPL);
			//DirectX 추가
			if (ulParam != 0)
			{
				ComPtr<IUnknown> spManagerUnk = reinterpret_cast<IUnknown*>(ulParam);

				ThrowIfError(spManagerUnk.As(&pDXGIDeviceManager));

				if (m_spDX11Manager != pDXGIDeviceManager)
				{
					InvalidateDX11Resources();
					m_spDX11Manager = nullptr;
					m_spDX11Manager = pDXGIDeviceManager;

					UpdateDX11Device();

					if (m_spOutputSampleAllocator != nullptr)
					{
						ThrowIfError(m_spOutputSampleAllocator->SetDirectXManager(spManagerUnk.Get()));
					}
				}
			}
			else
			{
				InvalidateDX11Resources();
				m_spDX11Manager = nullptr;
			}

			break;

		case MFT_MESSAGE_NOTIFY_BEGIN_STREAMING:
			AllocateStreamingResources();
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_NOTIFY_BEGIN_STREAMING\n");
			BeginStreaming();
			break;
			//디버거가 실행중에는 앱이 종료시에만 호출되지만, 디버거가 실행(attached) 상태가 아니면 Minimize시 혹은 복귀시에 호출이 되는 것으로 판단된다.
			//그렇기 때문에 여기서 리소스를 해제하면 앱 크래쉬가 발생하는것으로 판단된다.
		case MFT_MESSAGE_NOTIFY_END_STREAMING:	
			//m_fStreamingInitialized = false;
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_NOTIFY_END_STREAMING\n");
			//FreeStreamingResources(); //
			break;
		case MFT_MESSAGE_NOTIFY_START_OF_STREAM: //MFT_MESSAGE_NOTIFY_BEGIN_STREAMING후에 호출된다.
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_NOTIFY_START_OF_STREAM\n");
			break;
		case MFT_MESSAGE_NOTIFY_END_OF_STREAM:			//호출되지 않는다.
			OutputDebugMessage(L"SW Video Decoder : MFT_MESSAGE_NOTIFY_END_OF_STREAM\n");
			break;

		}
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}



//-------------------------------------------------------------------
// Name: ProcessInput
// Description: Process an input sample.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::ProcessInput(
	DWORD               dwInputStreamID,
	IMFSample           *pSample,
	DWORD               dwFlags
	)
{
//	DebugMessage(L"VideoDecoder::ProcessInput\r\n");
	HRESULT hr = S_OK;
	try
	{
		if (pSample == nullptr)
		{
			throw ref new InvalidArgumentException();
		}

		if (dwFlags != 0)
		{
			throw ref new InvalidArgumentException(); // dwFlags is reserved and must be zero.
		}

		AutoLock lock(m_critSec);

		if (!IsValidInputStream(dwInputStreamID))
		{
			ThrowException(MF_E_INVALIDSTREAMNUMBER);
		}

		// Check for valid media types.
		// The client must set input and output types before calling ProcessInput.
		if (m_spInputType == nullptr || m_spOutputType == nullptr)
		{
			ThrowException(MF_E_NOTACCEPTING);   // Client must set input and output types.
		}

		if (HasPendingOutput())
		{
			ThrowException(MF_E_NOTACCEPTING);   // We already have an input sample.
		}

		// Initialize streaming.
		BeginStreaming();

		BYTE *pbData;
		DWORD cbData;
		ComPtr<IMFMediaBuffer> spInput;

		ThrowIfError(pSample->GetBufferByIndex(0, &spInput));
		ThrowIfError(spInput->GetCurrentLength(&cbData));
		ThrowIfError(spInput->Lock(&pbData, nullptr, &cbData));

		AVPacket avpkt;
		av_init_packet(&avpkt);

		uint8_t* pktData = (uint8_t*)av_malloc(cbData);
		CopyMemory(pktData, pbData, cbData);
		av_packet_from_data(&avpkt, pktData, cbData);

		spInput->Unlock();
		spInput.Reset();

		ThrowIfError(pSample->GetUINT64(MF_MT_MY_FFMPEG_AVPACKET_PTS, (UINT64*)&avpkt.pts));
		ThrowIfError(pSample->GetUINT64(MF_MT_MY_FFMPEG_AVPACKET_DTS, (UINT64*)&avpkt.dts));
		ThrowIfError(pSample->GetUINT64(MF_MT_MY_FFMPEG_AVPACKET_DURATION, (UINT64*)&avpkt.duration));
		ThrowIfError(pSample->GetUINT32(MF_MT_MY_FFMPEG_AVPACKET_STREAM_INDEX, (UINT32*)&avpkt.stream_index));
		ThrowIfError(pSample->GetUINT32(MF_MT_MY_FFMPEG_AVPACKET_FLAGS, (UINT32*)&avpkt.flags));
		ThrowIfError(pSample->GetUINT64(MF_MT_MY_FFMPEG_AVPACKET_POS, (UINT64*)&avpkt.pos));

		// Use decoding timestamp if presentation timestamp is not valid
		if (avpkt.pts == AV_NOPTS_VALUE && avpkt.dts != AV_NOPTS_VALUE)
		{
			avpkt.pts = avpkt.dts;
		}

		if (avpkt.pts == AV_NOPTS_VALUE)
		{
			avpkt.pts = 0;
		}

		while (avpkt.size > 0)
		{
			if (decode_frame(m_pAvCodecCtx, m_pAvFrame, &m_avFrameComplete, &avpkt, true) < 0)
			{
				OutputDebugMessage(L"Failed decode video frame!\n");
				break;
			}

			if (m_avFrameComplete)
			{
				m_pAvFrame->pts = av_frame_get_best_effort_timestamp(m_pAvFrame);//2016-03-29 interop참조 수정
				m_pAvFrame->pkt_duration = avpkt.duration;

				if (avpkt.flags & AV_PKT_FLAG_KEY && m_pAvFrame->key_frame == 0)
				{
					m_pAvFrame->key_frame = 1;
				}

				hr = S_OK;
			}
			else
			{
				av_frame_unref(m_pAvFrame);
			}
		}

		//패킷 초기화
		av_packet_unref(&avpkt);
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}
	return hr;
}

//-------------------------------------------------------------------
// Name: ProcessOutput
// Description: Process an output sample.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedVideoDecoder::ProcessOutput(
	DWORD                   dwFlags,
	DWORD                   cOutputBufferCount,
	MFT_OUTPUT_DATA_BUFFER  *pOutputSamples, // one per stream
	DWORD                   *pdwStatus
	)
{
	//	DebugMessage(L"VideoDecoder::ProcessOutput\r\n");
	HRESULT hr = S_OK;
	bool fDeviceLocked = false;
	try
	{
		// Check input parameters...

		// There are no flags that we accept in this MFT.
		// The only defined flag is MFT_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER. This
		// flag only applies when the MFT marks an output stream as lazy or optional.
		// However there are no lazy or optional streams on this MFT, so the flag is
		// not valid.
		if (dwFlags != 0)
		{
			throw ref new InvalidArgumentException();
		}

		if (pOutputSamples == nullptr || pdwStatus == nullptr)
		{
			throw ref new InvalidArgumentException();
		}

		// Must be exactly one output buffer.
		if (cOutputBufferCount != 1)
		{
			throw ref new InvalidArgumentException();
		}

		// It must contain a sample.
		if (pOutputSamples[0].pSample == nullptr && m_spDX11Manager == nullptr)
		{
			throw ref new InvalidArgumentException();
		}

		ComPtr<ID3D11Device> spDevice;

		AutoLock lock(m_critSec);

		// If we don't have an input sample, we need some input before
		// we can generate any output.
		if (!HasPendingOutput())
		{
			//			DebugMessage(L"VideoDecoder::ProcessOutput() ====> Need more input .... \r\n");
			return MF_E_TRANSFORM_NEED_MORE_INPUT;
		}

		// Initialize streaming.
		BeginStreaming();

		// Check that our device is still good
		CheckDX11Device();

		// When using DX we provide the output samples...
		if (m_spDX11Manager != nullptr)
		{
			ThrowIfError(m_spOutputSampleAllocator->AllocateSample(&(pOutputSamples[0].pSample)));;
		}
		
		ComPtr<IMFMediaBuffer> spOutput;
		DWORD cbData = 0;

		// Get the output buffer.
		ThrowIfError(pOutputSamples[0].pSample->GetBufferByIndex(0, &spOutput));
		ThrowIfError(spOutput->GetMaxLength(&cbData));

		if (cbData < m_cbImageSize)
		{
			throw ref new InvalidArgumentException();
		}

		// Attempt to lock the device if necessary
		if (m_spDX11Manager != nullptr)
		{
			ThrowIfError(m_spDX11Manager->LockDevice(m_hDeviceHandle, IID_PPV_ARGS(&spDevice), TRUE));
			fDeviceLocked = true;
		}

		InternalProcessOutput(pOutputSamples[0].pSample, spOutput.Get());
		//  Is there any more data to output at this point?

		//pOutputSamples[0].dwStatus |= MFT_OUTPUT_DATA_BUFFER_INCOMPLETE;
		pOutputSamples[0].dwStatus = 0;

		if (fDeviceLocked)
		{
			ThrowIfError(m_spDX11Manager->UnlockDevice(m_hDeviceHandle, FALSE));
		}
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	if (fDeviceLocked)
	{
		m_spDX11Manager->UnlockDevice(m_hDeviceHandle, FALSE);
	}

	return hr;
}

// Private class methods


void CFFmpegUncompressedVideoDecoder::InternalProcessOutput(IMFSample *pSample, IMFMediaBuffer *pOutputBuffer)
{
	//	DebugMessage(L"VideoDecoder::ProcessOutput => InternalProcessOutput()\r\n");
	if (m_avFrameComplete)
	{
		ThrowIfError(pOutputBuffer->SetCurrentLength((DWORD)m_cbImageSize));
		ThrowIfError(pSample->SetUINT32(MFSampleExtension_CleanPoint, m_pAvFrame->key_frame));

		if (m_fGpuShader)
		{
			//쉐이더를 이용한 색공간 변환
			ConvertColorSpaceByShader(m_pAvFrame, pOutputBuffer);
		}
		else
		{
			//SW 색공간 변환
			ConvertColorSpaceByFFmpeg(m_pAvFrame, pOutputBuffer);
			//직접 CPU로 색공간 변환 YUV420P/10->NV12
			//ConvertColorSpaceByCpu(m_pAvFrame, pOutputBuffer);
		}

		//  Set the timestamp
		//  Uncompressed video must always have a timestamp
		LONGLONG syncSec = m_avDecoderConnector != nullptr ? m_avDecoderConnector->AudioSyncMilliSeconds * -10000 : 0;
		LONGLONG hnsStart = ULONGLONG(m_timeBase * 10000000 * m_pAvFrame->pts + syncSec);
		LONGLONG hnsSampleDuration = ULONGLONG(m_timeBase * 10000000 * m_pAvFrame->pkt_duration);

		ThrowIfError(pSample->SetSampleTime(hnsStart));

		if (hnsSampleDuration >= 0)
		{
			ThrowIfError(pSample->SetSampleDuration(hnsSampleDuration));
		}
	}

	m_avFrameComplete = 0;
	av_frame_unref(m_pAvFrame);
}


// Generate output data.
void CFFmpegUncompressedVideoDecoder::ConvertColorSpaceByShader(AVFrame* frame, IMFMediaBuffer *pOut)
{
	ComPtr<ID3D11Texture2D> spInTexY;
	ComPtr<ID3D11Texture2D> spInTexU;
	ComPtr<ID3D11Texture2D> spInTexV;
	ComPtr<ID3D11Texture2D> spOutTex;
	UINT uiInIndex = 0;
	UINT uiOutIndex = 0;

	// Attempt to convert directly to DX textures
	try
	{
		spOutTex = BufferToDXType(pOut, &uiOutIndex);
	}
	catch (Exception^)
	{
	}

	//bool fNativeIn = spInTex != nullptr;
	bool fNativeOut = spOutTex != nullptr;

	// If the input or output textures' device does not match our device
	// we have to move them to our device
	if (fNativeOut)
	{
		ComPtr<ID3D11Device> spDev;
		spOutTex->GetDevice(&spDev);
		if (spDev != m_spDevice)
		{
			fNativeOut = false;
		}
	}
	
	if (m_spInBufferTexY == nullptr)
	{
		D3D11_TEXTURE2D_DESC descY;
		ZeroMemory(&descY, sizeof(descY));
		descY.Width = frame->width;
		descY.Height = frame->height;
		descY.ArraySize = 1;
		descY.Format = DXGI_FORMAT_A8_UNORM;
		descY.Usage = D3D11_USAGE_DYNAMIC;
		descY.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		descY.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
		descY.MipLevels = 1;
		descY.SampleDesc.Count = 1;

		ThrowIfError(m_spDevice->CreateTexture2D(&descY, nullptr, &m_spInBufferTexY));
	}
	if (m_spInBufferTexU == nullptr)
	{
		D3D11_TEXTURE2D_DESC descU;
		ZeroMemory(&descU, sizeof(descU));
		descU.Width = frame->width >> 1;
		descU.Height = frame->height >> 1;
		descU.ArraySize = 1;
		descU.Format = DXGI_FORMAT_A8_UNORM;
		descU.Usage = D3D11_USAGE_DYNAMIC;
		descU.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		descU.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
		descU.MipLevels = 1;
		descU.SampleDesc.Count = 1;

		ThrowIfError(m_spDevice->CreateTexture2D(&descU, nullptr, &m_spInBufferTexU));
	}
	if (m_spInBufferTexV == nullptr)
	{
		D3D11_TEXTURE2D_DESC descV;
		ZeroMemory(&descV, sizeof(descV));
		descV.Width = frame->width >> 1;
		descV.Height = frame->height >> 1;
		descV.ArraySize = 1;
		descV.Format = DXGI_FORMAT_A8_UNORM;
		descV.Usage = D3D11_USAGE_DYNAMIC;
		descV.BindFlags = D3D11_BIND_SHADER_RESOURCE;
		descV.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
		descV.MipLevels = 1;
		descV.SampleDesc.Count = 1;

		ThrowIfError(m_spDevice->CreateTexture2D(&descV, nullptr, &m_spInBufferTexV));
	}
	//Texture lock block
	{
			
		TextureLock tlockY(m_spContext.Get(), m_spInBufferTexY.Get());
		ThrowIfError(tlockY.Map(uiInIndex, D3D11_MAP_WRITE_DISCARD, 0));

		TextureLock tlockU(m_spContext.Get(), m_spInBufferTexU.Get());
		ThrowIfError(tlockU.Map(uiInIndex, D3D11_MAP_WRITE_DISCARD, 0));

		TextureLock tlockV(m_spContext.Get(), m_spInBufferTexV.Get());
		ThrowIfError(tlockV.Map(uiInIndex, D3D11_MAP_WRITE_DISCARD, 0));

		if ((AVPixelFormat)m_pAvFrame->format == AV_PIX_FMT_YUV420P10
			|| (AVPixelFormat)m_pAvFrame->format == AV_PIX_FMT_YUV420P10BE
			|| (AVPixelFormat)m_pAvFrame->format == AV_PIX_FMT_YUV420P10LE)
		{
			//10비트 비디오 색공간 변환
			byte* py = (byte*)frame->data[0];
			byte* pu = (byte*)frame->data[1];
			byte* pv = (byte*)frame->data[2];
			int halfH = frame->height >> 1;
			int halfW = frame->width >> 1;
			int pitchY = tlockY.map.RowPitch - frame->width;
			int pitchUV = tlockU.map.RowPitch - halfW;
			long offsetY = 0;
			long offsetUV = 0;

			for (int i = 0; i < frame->height; i++)
			{
				for (int j = 0; j < frame->width; j++)
				{
					int hi = j << 1;
					int lo = (j << 1) + 1;
					WORD wy = MAKEWORD(py[hi], py[lo]);
					((BYTE*)tlockY.map.pData)[offsetY] = (byte)(wy >> 2);
					offsetY++;

					if (i < halfH && j < halfW)
					{
						WORD wu = MAKEWORD(pu[hi], pu[lo]);
						WORD wv = MAKEWORD(pv[hi], pv[lo]);

						((BYTE*)tlockV.map.pData)[offsetUV] = (byte)(wv >> 2);
						((BYTE*)tlockU.map.pData)[offsetUV] = (byte)(wu >> 2);
						
						offsetUV++;
					}
				}

				py += frame->linesize[0];
				pu += frame->linesize[1];
				pv += frame->linesize[2];

				if (pitchY > 0)
				{
					offsetY += pitchY;
				}

				if (pitchUV > 0 && i < halfH)
				{
					offsetUV += pitchUV;
				}
			}
		}
		else
		{
			//8비트 비디오 색공간 변환			
			byte* py = (byte*)frame->data[0];
			byte* pu = (byte*)frame->data[1];
			byte* pv = (byte*)frame->data[2];
			int halfH = frame->height >> 1;
			int halfW = frame->width >> 1;
			int pitch = tlockU.map.RowPitch - halfW;
			long offsetY = 0;
			long offsetUV = 0;

			for (int i = 0; i < frame->height; i++)
			{
				CopyMemory((BYTE*)tlockY.map.pData + offsetY, py, frame->width);
				offsetY += tlockY.map.RowPitch;
				py += frame->linesize[0];

				if (i < halfH)
				{
					/*for (int j = 0; j < halfW; j++)
					{
						CopyMemory((BYTE*)tlockU.map.pData + offsetUV, pu + j, 1);
						CopyMemory((BYTE*)tlockV.map.pData + offsetUV, pv + j, 1);

						offsetUV++;
					}*/

					CopyMemory((BYTE*)tlockU.map.pData + offsetUV, pu, halfW);
					CopyMemory((BYTE*)tlockV.map.pData + offsetUV, pv, halfW);
					offsetUV += halfW;

					if (pitch > 0)
					{
						offsetUV += pitch;
					}

					pu += frame->linesize[1];
					pv += frame->linesize[2];
				}
			}
		}
	}

	spInTexY = m_spInBufferTexY;
	spInTexU = m_spInBufferTexU;
	spInTexV = m_spInBufferTexV;

	if (!fNativeOut)
	{
		if (m_spOutBufferTex == nullptr)
		{
			D3D11_TEXTURE2D_DESC desc;
			ZeroMemory(&desc, sizeof(desc));
			desc.Width = m_imageWidthInPixels;
			desc.Height = m_imageHeightInPixels;
			desc.ArraySize = 1;
			desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
			desc.Usage = D3D11_USAGE_DEFAULT;
			desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
			desc.MipLevels = 1;
			desc.SampleDesc.Count = 1;
			ThrowIfError(m_spDevice->CreateTexture2D(&desc, nullptr, &m_spOutBufferTex));
		}
		spOutTex = m_spOutBufferTex;
	}

	// do some processing
	m_transform->ProcessFrame(m_spDevice.Get(), spInTexY.Get(), spInTexU.Get(), spInTexV.Get(), uiInIndex, spOutTex.Get(), uiOutIndex);
	
	// write back pOut if necessary
	if (!fNativeOut)
	{
		if (m_spOutBufferStage == nullptr)
		{
			D3D11_TEXTURE2D_DESC desc;
			ZeroMemory(&desc, sizeof(desc));
			desc.Width = m_imageWidthInPixels;
			desc.Height = m_imageHeightInPixels;
			desc.ArraySize = 1;
			desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
			desc.Usage = D3D11_USAGE_STAGING;
			desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
			desc.MipLevels = 1;
			desc.SampleDesc.Count = 1;
			ThrowIfError(m_spDevice->CreateTexture2D(&desc, nullptr, &m_spOutBufferStage));
		}

		m_spContext->CopyResource(m_spOutBufferStage.Get(), m_spOutBufferTex.Get());

		{
			TextureLock tlock(m_spContext.Get(), m_spOutBufferStage.Get());
			ThrowIfError(tlock.Map(uiOutIndex, D3D11_MAP_READ, 0));

			// scope the video buffer lock
			{
				LONG lStride = m_imageWidthInPixels;
				VideoBufferLock lock(pOut, MF2DBuffer_LockFlags_Write, m_imageHeightInPixels, lStride);
				ThrowIfError(MFCopyImage(lock.GetData(), lock.GetStride(), (BYTE*)tlock.map.pData, tlock.map.RowPitch, abs(lStride), m_imageHeightInPixels));
			}
		}
	}
}

void CFFmpegUncompressedVideoDecoder::ConvertColorSpaceByFFmpeg(AVFrame* frame, IMFMediaBuffer *pOut)
{
	m_pSwsCtx = sws_getCachedContext(m_pSwsCtx,
		frame->width, frame->height, (AVPixelFormat)frame->format, m_pAvCodecCtx->width, m_pAvCodecCtx->height,
		AV_PIX_FMT_NV12, SWS_BICUBIC, NULL, NULL, NULL);

	if (m_pSwsCtx == NULL)
	{
		DebugMessage(L"Cannot initialize the conversion context\n");
		ThrowIfError(E_INVALIDARG);
	}

	AVFrame* cFrame = av_frame_alloc();
	cFrame->format = AV_PIX_FMT_NV12;
	cFrame->width = m_pAvCodecCtx->width;
	cFrame->height = m_pAvCodecCtx->height;

	av_image_alloc(cFrame->data, cFrame->linesize, m_pAvCodecCtx->width, m_pAvCodecCtx->height, AV_PIX_FMT_NV12, 1);

	//FFmpeg util SW색공간 변환
	sws_scale(m_pSwsCtx, frame->data, frame->linesize,
		0, m_pAvCodecCtx->height, cFrame->data, cFrame->linesize);
	
	LONG lStride = m_imageWidthInPixels;
	VideoBufferLock lock(pOut, MF2DBuffer_LockFlags_Write, m_imageHeightInPixels, lStride);
	ThrowIfError(MFCopyImage(lock.GetData(), lock.GetStride(), cFrame->data[0], lStride, abs(lStride), m_imageHeightInPixels + m_imageHeightInPixels / 2));

	//release resource
	av_freep(&cFrame->data[0]);
	av_frame_free(&cFrame);
}

//테스트 전용
void CFFmpegUncompressedVideoDecoder::ConvertColorSpaceByCpu(AVFrame* frame, IMFMediaBuffer *pOut)
{
	BYTE* pbData = NULL;
	DWORD cbData = 0;
	ThrowIfError(pOut->Lock(&pbData, NULL, &cbData));

	long ySize = frame->width * frame->height;
	long uSize = frame->width / 2 * frame->height / 2;
	long vSize = frame->width / 2 * frame->height / 2;
	
	long offset = 0;

	//YUV420P10
	if ((AVPixelFormat)frame->format == AV_PIX_FMT_YUV420P10
		|| (AVPixelFormat)frame->format == AV_PIX_FMT_YUV420P10BE
		|| (AVPixelFormat)frame->format == AV_PIX_FMT_YUV420P10LE)
	{
		//주의) 아직 linesize 및 텍스쳐 row pitch 적용되지 않았음.

		for (int i = 0; i < ySize; i+=2)
		{
			WORD wy = MAKEWORD(frame->data[0][i], frame->data[0][i + 1]);
			*(pbData + offset++) = (byte)(wy / 4);
		}
		for (int i = 0; i < uSize; i+=2)
		{
			WORD wu = MAKEWORD(frame->data[1][i], frame->data[1][i + 1]);
			WORD wv = MAKEWORD(frame->data[2][i], frame->data[2][i + 1]);
			*(pbData + offset++) = (byte)(wu / 4);
			*(pbData + offset++) = (byte)(wv / 4);
		}
	}
	else
	{
		//YUV420P
		byte* py = (byte*)frame->data[0];
		for (int i = 0; i < frame->height; i++)
		{
			CopyMemory(pbData + offset, py, frame->width);
			offset += frame->width;
			py += frame->linesize[0];
		}

		byte* pu = (byte*)frame->data[1];
		byte* pv = (byte*)frame->data[2];
		
		for (int i = 0; i < frame->height / 2; i++)
		{
			for (int j = 0; j < frame->width / 2; j++)
			{
				CopyMemory(pbData + offset++, pu + j, 1);
				CopyMemory(pbData + offset++, pv + j, 1);
			}
			pu += frame->linesize[1];
			pv += frame->linesize[2];
		}
	}

	ThrowIfError(pOut->Unlock());
}

void CFFmpegUncompressedVideoDecoder::OnCheckInputType(IMFMediaType *pmt)
{
	//  Check if the type is already set and if so reject any type that's not identical.
	if (m_spInputType != nullptr)
	{
		DWORD dwFlags = 0;
		if (S_OK == m_spInputType->IsEqual(pmt, &dwFlags))
		{
			return;
		}
		else
		{
			ThrowException(MF_E_INVALIDTYPE);
		}
	}

	GUID majortype = { 0 };
	GUID subtype = { 0 };

	//  We accept MFMediaType_Video, MEDIASUBTYPE_MPEG1Video
	ThrowIfError(pmt->GetMajorType(&majortype));
	if (majortype != MFMediaType_Video)
		ThrowException(MF_E_INVALIDTYPE);

	ThrowIfError(pmt->GetGUID(MF_MT_SUBTYPE, &subtype));
	if (subtype != MFVideoFormat_FFmpeg_SW)
		ThrowException(MF_E_INVALIDTYPE);
}

void CFFmpegUncompressedVideoDecoder::OnSetInputType(IMFMediaType *pmt)
{
	m_spInputType.Reset();

	//ffmpeg
	UINT32 pixFmt = 0;
	UINT32 profile = 0;

	ThrowIfError(MFGetAttributeSize(pmt, MF_MT_FRAME_SIZE, &m_imageWidthInPixels, &m_imageHeightInPixels));
	ThrowIfError(MFGetAttributeRatio(pmt, MF_MT_FRAME_RATE, (UINT32*)&m_frameRate.Numerator, (UINT32*)&m_frameRate.Denominator));
	ThrowIfError(MFGetAttributeRatio(pmt, MF_MT_PIXEL_ASPECT_RATIO, (UINT32*)&m_aspectRatio.Numerator, (UINT32*)&m_aspectRatio.Denominator));
	ThrowIfError(pmt->GetUINT64(MF_MT_AVG_BITRATE, &m_bitRate));
	ThrowIfError(pmt->GetUINT32(MF_MT_VIDEO_PROFILE, &profile));
	ThrowIfError(pmt->GetDouble(MF_MT_MY_FFMPEG_TIME_BASE, &m_timeBase));
	ThrowIfError(pmt->GetUINT32(MF_MT_MY_FFMPEG_SAMPLE_FMT, &pixFmt));
	ThrowIfError(pmt->GetUINT32(MF_MT_INTERLACE_MODE, &m_interlace));

	// Also store the frame duration, derived from the frame rate.
	if (m_frameRate.Numerator == 0 || m_frameRate.Denominator == 0)
		ThrowException(MF_E_INVALIDTYPE);
	
	m_cbImageSize = av_image_get_buffer_size(AV_PIX_FMT_NV12, m_imageWidthInPixels, m_imageHeightInPixels, 1);

	m_spInputType = pmt;

	if (m_pAvCodecCtx == NULL)
	{
		LPVOID unkown = nullptr;

		pmt->GetUnknown(MF_MT_MY_FFMPEG_CODEC_CONTEXT, IID_IUnknown, &unkown);
		CCAVCodecContext* ctx = (CCAVCodecContext*)unkown;

		AVCodec* avCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(ctx->Stream, codec_id));
		if (avCodec)
		{
			m_pAvCodecCtx = avcodec_alloc_context3(avCodec);
			if (m_pAvCodecCtx)
			{
				if (FILL_CODEC_CTX(m_pAvCodecCtx, ctx->Stream) != 0)
				{
					OutputDebugMessage(L"Couldn't set video codec context\n");
				}
			}
			if (avcodec_open2(m_pAvCodecCtx, avCodec, NULL) < 0)
			{
				avcodec_free_context(&m_pAvCodecCtx);
				m_pAvCodecCtx = nullptr;
			}
		}
		ctx->Release();
	}

	if (m_pAvCodecCtx == NULL)
	{
		ThrowException(MF_E_INVALIDTYPE);
	}
}

void CFFmpegUncompressedVideoDecoder::OnCheckOutputType(IMFMediaType *pmt)
{
	//  Check if the type is already set and if so reject any type that's not identical.
	if (m_spOutputType != nullptr)
	{
		DWORD dwFlags = 0;
		if (S_OK == m_spOutputType->IsEqual(pmt, &dwFlags))
		{
			return;
		}
		else
		{
			ThrowException(MF_E_INVALIDTYPE);
		}
	}

	if (m_spInputType == nullptr)
	{
		ThrowException(MF_E_TRANSFORM_TYPE_NOT_SET); // Input type must be set first.
	}

	BOOL fMatch = FALSE;

	ComPtr<IMFMediaType> spOurType;

	// Make sure their type is a superset of our proposed output type.
	ThrowIfError(GetOutputAvailableType(0, 0, &spOurType));

	ThrowIfError(spOurType->Compare(pmt, MF_ATTRIBUTES_MATCH_OUR_ITEMS, &fMatch));

	if (!fMatch)
	{
		ThrowException(MF_E_INVALIDTYPE);
	}
}


void CFFmpegUncompressedVideoDecoder::OnSetOutputType(IMFMediaType *pmt)
{
	m_spOutputType = pmt;
}


void CFFmpegUncompressedVideoDecoder::AllocateStreamingResources()
{
	m_pAvFrame = av_frame_alloc();
	//  Reinitialize variables
	OnDiscontinuity();
}

void CFFmpegUncompressedVideoDecoder::FreeStreamingResources()
{
	if (m_pSwsCtx != NULL)
		sws_freeContext(m_pSwsCtx);
	
	if (m_pAvCodecCtx != NULL)
		avcodec_free_context(&m_pAvCodecCtx);

	if (m_pAvFrame != NULL)
		av_frame_free(&m_pAvFrame);

	m_avFrameComplete = 0;
}

void CFFmpegUncompressedVideoDecoder::OnDiscontinuity()
{
	if (m_pAvFrame != NULL)
		av_frame_unref(m_pAvFrame);
	//  Zero our timestamp
	m_avFrameComplete = 0;
}

void CFFmpegUncompressedVideoDecoder::OnFlush()
{
	OnDiscontinuity();
	if (m_pAvCodecCtx != NULL && m_pAvCodecCtx->codec != NULL && avcodec_is_open(m_pAvCodecCtx))
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}

// PRIVATE METHODS


// Initialize streaming parameters.
void CFFmpegUncompressedVideoDecoder::BeginStreaming()
{
	if (!m_fStreamingInitialized)
	{
		if (m_spDevice == nullptr)
		{
			UpdateDX11Device();
		}

		m_transform->Initialize(m_spDevice.Get(), m_imageWidthInPixels, m_imageHeightInPixels);

		// if the device is dxman we need to alloc samples...
		if (m_spDX11Manager != nullptr)
		{
			DWORD dwBindFlags = MFGetAttributeUINT32(m_spOutputAttributes.Get(), MF_SA_D3D11_BINDFLAGS, D3D11_BIND_RENDER_TARGET);
			dwBindFlags |= D3D11_BIND_RENDER_TARGET;        // render target binding must always be set
			ThrowIfError(m_spOutputAttributes->SetUINT32(MF_SA_D3D11_BINDFLAGS, dwBindFlags));
			ThrowIfError(m_spOutputAttributes->SetUINT32(MF_SA_BUFFERS_PER_SAMPLE, 1));
			ThrowIfError(m_spOutputAttributes->SetUINT32(MF_SA_D3D11_USAGE, D3D11_USAGE_DEFAULT));

			if (nullptr == m_spOutputSampleAllocator)
			{
				ComPtr<IMFVideoSampleAllocatorEx> spVideoSampleAllocator;
				ComPtr<IUnknown> spDXGIManagerUnk;

				ThrowIfError(MFCreateVideoSampleAllocatorEx(IID_PPV_ARGS(&spVideoSampleAllocator)));
				ThrowIfError(m_spDX11Manager.As(&spDXGIManagerUnk));
				ThrowIfError(spVideoSampleAllocator->SetDirectXManager(spDXGIManagerUnk.Get()));
				m_spOutputSampleAllocator.Attach(spVideoSampleAllocator.Detach());
			}

			HRESULT hr = m_spOutputSampleAllocator->InitializeSampleAllocatorEx(1, 10, m_spOutputAttributes.Get(), m_spOutputType.Get());

			if (FAILED(hr))
			{
				if (dwBindFlags != D3D11_BIND_RENDER_TARGET)
				{
					// Try again with only the mandatory "render target" binding
					ThrowIfError(m_spOutputAttributes->SetUINT32(MF_SA_D3D11_BINDFLAGS, D3D11_BIND_RENDER_TARGET));
					ThrowIfError(m_spOutputSampleAllocator->InitializeSampleAllocatorEx(1, 10, m_spOutputAttributes.Get(), m_spOutputType.Get()));
				}
				else
				{
					ThrowException(hr);
				}
			}
		}

		m_fStreamingInitialized = true;
	}
}

// Reads DX buffers from IMFMediaBuffer
ComPtr<ID3D11Texture2D> BufferToDXType(IMFMediaBuffer *pBuffer, _Out_ UINT *uiViewIndex)
{
	ComPtr<IMFDXGIBuffer> spDXGIBuffer;
	ComPtr<ID3D11Texture2D> spTexture;

	if (SUCCEEDED(pBuffer->QueryInterface(IID_PPV_ARGS(&spDXGIBuffer))))
	{
		if (SUCCEEDED(spDXGIBuffer->GetResource(IID_PPV_ARGS(&spTexture))))
		{
			spDXGIBuffer->GetSubresourceIndex(uiViewIndex);
		}
	}

	return spTexture;
}

void CFFmpegUncompressedVideoDecoder::CheckDX11Device()
{
	if (m_spDX11Manager != nullptr && m_hDeviceHandle)
	{
		if (m_spDX11Manager->TestDevice(m_hDeviceHandle) != S_OK)
		{
			InvalidateDX11Resources();

			UpdateDX11Device();

			m_transform->Initialize(m_spDevice.Get(), m_imageWidthInPixels, m_imageHeightInPixels);

		}
	}
}

// Delete any resources dependant on the current device we are using
void CFFmpegUncompressedVideoDecoder::InvalidateDX11Resources()
{
	m_transform->Invalidate();
	m_spDevice = nullptr;
	m_spContext = nullptr;
	m_spInBufferTex = nullptr;
	m_spOutBufferTex = nullptr;
	m_spOutBufferStage = nullptr;

	m_spInBufferTexY = nullptr;
	m_spInBufferTexU = nullptr;
	m_spInBufferTexV = nullptr;
}

// Update the directx device
void CFFmpegUncompressedVideoDecoder::UpdateDX11Device()
{
	HRESULT hr = S_OK;
	try
	{
		if (m_spDX11Manager != nullptr)
		{
			ThrowIfError(m_spDX11Manager->OpenDeviceHandle(&m_hDeviceHandle));

			ThrowIfError(m_spDX11Manager->GetVideoService(m_hDeviceHandle, __uuidof(m_spDevice), (void**)&m_spDevice));
			
			m_spDevice->GetImmediateContext(&m_spContext);
		}
		else
		{
			D3D_FEATURE_LEVEL level;
			D3D_FEATURE_LEVEL levelsWanted[] =
			{
				D3D_FEATURE_LEVEL_11_1,
				D3D_FEATURE_LEVEL_11_0,
				D3D_FEATURE_LEVEL_10_1,
				D3D_FEATURE_LEVEL_10_0,
			};
			DWORD numLevelsWanted = sizeof(levelsWanted) / sizeof(levelsWanted[0]);
			
			ThrowIfError(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, levelsWanted, numLevelsWanted,
				D3D11_SDK_VERSION, &m_spDevice, &level, &m_spContext));
		}
	}
	catch (Exception^)
	{
		InvalidateDX11Resources();
		throw;
	}
}


