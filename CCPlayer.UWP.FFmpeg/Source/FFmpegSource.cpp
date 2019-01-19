//////////////////////////////////////////////////////////////////////////
//
// FFmpegSource.h
// Implements the FFmpeg media source object.
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
#include "Common\FFmpegMacro.h"
#include "FFmpegSource.h"

#define VID_WH_E(w,h,w2,h2) (w <= w2) && (h <= h2)
#define MAX_AUDIO_COUNT 2

//#include "shcore.h"
// Size of the buffer when reading a stream
const int FILESTREAMBUFFERSZ = 32 * 1024;

// Static functions passed to FFmpeg for stream interop
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize);
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence);

//-------------------------------------------------------------------
//
// Notes:
// This sample contains an FFmpeg source.
//
// - The source parses FFmpeg systems-layer streams and generates
//   samples that contain FFmpeg payloads.
//
//-------------------------------------------------------------------

#pragma warning( push )
#pragma warning( disable : 4355 )  // 'this' used in base member initializer list

using namespace Windows::Data::Json;
using namespace CCPlayer::UWP::Common::Codec;

void GetStreamMajorType(IMFStreamDescriptor *pSD, GUID *pguidMajorType);

/* Public class methods */
//-------------------------------------------------------------------
// Name: CreateInstance
// Static method to create an instance of the source.
//-------------------------------------------------------------------

ComPtr<CFFmpegSource> CFFmpegSource::CreateInstance(
	IAVDecoderConnector^ decoderConnector, 
	ISubtitleDecoderConnector^ subtitleConnector, 
	IAttachmentDecoderConnector^ attachementConnector,
	PropertySet^ ffmpegOptions)
{
	ComPtr<CFFmpegSource> spSource;

	spSource.Attach(new (std::nothrow) CFFmpegSource());
	if (spSource == nullptr)
	{
		throw ref new OutOfMemoryException();
	}
	
	spSource->m_avDecoderConnector = decoderConnector;
	spSource->m_subtitleDecoderConnector = subtitleConnector;
	spSource->m_attachmentDecoderConnector = attachementConnector;
	spSource->m_ffmpegPropertySet = ffmpegOptions;

	return spSource;
}


//-------------------------------------------------------------------
// IUnknown methods
//-------------------------------------------------------------------

HRESULT CFFmpegSource::QueryInterface(REFIID riid, void **ppv)
{
	if (ppv == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = E_NOINTERFACE;
	if (riid == IID_IUnknown ||
		riid == IID_IMFMediaEventGenerator ||
		riid == IID_IMFMediaSource)
	{
		(*ppv) = static_cast<IMFMediaSource *>(this);
		AddRef();
		hr = S_OK;
	}
	else if (riid == IID_IMFGetService)
	{
		(*ppv) = static_cast<IMFGetService*>(this);
		AddRef();
		hr = S_OK;
	}
	else if (riid == IID_IMFRateControl)
	{
		(*ppv) = static_cast<IMFRateControl*>(this);
		AddRef();
		hr = S_OK;
	}
	else if (riid == IID_IMFRateSupport)
	{
		(*ppv) = static_cast<IMFRateSupport*>(this);
		AddRef();
		hr = S_OK;
	}

	return hr;
}

ULONG CFFmpegSource::AddRef()
{
	return _InterlockedIncrement(&m_cRef);
}

ULONG CFFmpegSource::Release()
{
	LONG cRef = _InterlockedDecrement(&m_cRef);
	if (cRef == 0)
	{
		delete this;
	}
	return cRef;
}

//-------------------------------------------------------------------
// IMFMediaEventGenerator methods
//
// All of the IMFMediaEventGenerator methods do the following:
// 1. Check for shutdown status.
// 2. Call the event queue helper object.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::BeginGetEvent(IMFAsyncCallback *pCallback, IUnknown *punkState)
{
	HRESULT hr = S_OK;

	AutoLock lock(m_critSec);

	hr = CheckShutdown();

	if (SUCCEEDED(hr))
	{
		hr = m_spEventQueue->BeginGetEvent(pCallback, punkState);
	}

	return hr;
}

HRESULT CFFmpegSource::EndGetEvent(IMFAsyncResult *pResult, IMFMediaEvent **ppEvent)
{
	HRESULT hr = S_OK;

	AutoLock lock(m_critSec);

	hr = CheckShutdown();

	if (SUCCEEDED(hr))
	{
		hr = m_spEventQueue->EndGetEvent(pResult, ppEvent);
	}

	return hr;
}

HRESULT CFFmpegSource::GetEvent(DWORD dwFlags, IMFMediaEvent **ppEvent)
{
	// NOTE:
	// GetEvent can block indefinitely, so we don't hold the lock.
	// This requires some juggling with the event queue pointer.

	HRESULT hr = S_OK;

	ComPtr<IMFMediaEventQueue> spQueue;

	{
		AutoLock lock(m_critSec);

		// Check shutdown
		hr = CheckShutdown();

		// Get the pointer to the event queue.
		if (SUCCEEDED(hr))
		{
			spQueue = m_spEventQueue;
		}
	}

	// Now get the event.
	if (SUCCEEDED(hr))
	{
		hr = spQueue->GetEvent(dwFlags, ppEvent);
	}

	return hr;
}

HRESULT CFFmpegSource::QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, const PROPVARIANT *pvValue)
{
	HRESULT hr = S_OK;
	
	//윈도우10에서 시크가 연속적으로 일어날 경우 hrStatus가 "현 상태에서 잘못된 요청입니다" 오류가 발생하므로 SKIP처리
	if (SUCCEEDED(hrStatus))
	{
		AutoLock lock(m_critSec);

		hr = CheckShutdown();

		if (SUCCEEDED(hr))
		{
			hr = m_spEventQueue->QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
		}
	}
	return hr;
}

//-------------------------------------------------------------------
// IMFMediaSource methods
//-------------------------------------------------------------------


//-------------------------------------------------------------------
// CreatePresentationDescriptor
// Returns a shallow copy of the source's presentation descriptor.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::CreatePresentationDescriptor(
	IMFPresentationDescriptor **ppPresentationDescriptor
	)
{
	if (ppPresentationDescriptor == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = S_OK;

	AutoLock lock(m_critSec);

	// Fail if the source is shut down.
	hr = CheckShutdown();

	// Fail if the source was not initialized yet.
	if (SUCCEEDED(hr))
	{
		hr = IsInitialized();
	}

	// Do we have a valid presentation descriptor?
	if (SUCCEEDED(hr))
	{
		if (m_spPresentationDescriptor == nullptr)
		{
			hr = MF_E_NOT_INITIALIZED;
		}
	}

	// Clone our presentation descriptor.
	if (SUCCEEDED(hr))
	{
		hr = m_spPresentationDescriptor->Clone(ppPresentationDescriptor);
	}

	return hr;
}


//-------------------------------------------------------------------
// GetCharacteristics
// Returns capabilities flags.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::GetCharacteristics(DWORD *pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = S_OK;

	AutoLock lock(m_critSec);

	hr = CheckShutdown();

	if (SUCCEEDED(hr))
	{
		*pdwCharacteristics = MFMEDIASOURCE_CAN_PAUSE |
			MFMEDIASOURCE_CAN_SKIPFORWARD |
			MFMEDIASOURCE_CAN_SKIPBACKWARD;

		//if (m_pAvIOCtx != nullptr && m_pAvIOCtx->seekable != 0)
		if (m_pAvFormatCtx != nullptr && m_pAvFormatCtx->pb != nullptr && m_pAvFormatCtx->pb->seekable != 0)
		{
			*pdwCharacteristics |= MFMEDIASOURCE_CAN_SEEK | MFMEDIASOURCE_HAS_SLOW_SEEK;
		}
	}

	return hr;
}


//-------------------------------------------------------------------
// Pause
// Pauses the source.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::Pause()
{
	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	// Fail if the source is shut down.
	hr = CheckShutdown();

	// Queue the operation.
	if (SUCCEEDED(hr))
	{
		hr = QueueAsyncOperation(SourceOp::OP_PAUSE, -1);
	}

	return hr;
}

//-------------------------------------------------------------------
// Shutdown
// Shuts down the source and releases all resources.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::Shutdown()
{
	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	CFFmpegStream *pStream = nullptr;

	hr = CheckShutdown();

	if (SUCCEEDED(hr))
	{
		// Shut down the stream objects.
		for (DWORD i = 0; i < m_streams.GetCount(); i++)
		{
			(void)m_streams[i]->Shutdown();
		}
		// Break circular references with streams here.
		m_streams.Clear();

		// Shut down the event queue.
		if (m_spEventQueue)
		{
			(void)m_spEventQueue->Shutdown();
		}

		// Release objects.

		m_spEventQueue.Reset();
		m_spPresentationDescriptor.Reset();
		m_spByteStream.Reset();
		m_spCurrentOp.Reset();

		// Set the state.
		m_state = STATE_SHUTDOWN;
	}

	return hr;
}


//-------------------------------------------------------------------
// Start
// Starts or seeks the media source.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::Start(
	IMFPresentationDescriptor *pPresentationDescriptor,
	const GUID *pguidTimeFormat,
	const PROPVARIANT *pvarStartPos
	)
{

	HRESULT hr = S_OK;
	ComPtr<SourceOp> spAsyncOp;

	// Check parameters.

	// Start position and presentation descriptor cannot be nullptr.
	if (pvarStartPos == nullptr || pPresentationDescriptor == nullptr)
	{
		return E_INVALIDARG;
	}

	// Check the time format.
	if ((pguidTimeFormat != nullptr) && (*pguidTimeFormat != GUID_NULL))
	{
		// Unrecognized time format GUID.
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	// Check the data type of the start position.
	if ((pvarStartPos->vt != VT_I8) && (pvarStartPos->vt != VT_EMPTY))
	{
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	AutoLock lock(m_critSec);

	// Fail if the source is shut down.
	hr = CheckShutdown();
	if (FAILED(hr))
	{
		goto done;
	}

	// Fail if the source was not initialized yet.
	hr = IsInitialized();
	if (FAILED(hr))
	{
		goto done;
	}

	// Perform a sanity check on the caller's presentation descriptor.
	hr = ValidatePresentationDescriptor(pPresentationDescriptor);
	if (FAILED(hr))
	{
		goto done;
	}

	// The operation looks OK. Complete the operation asynchronously.

	hr = SourceOp::CreateStartOp(pPresentationDescriptor, &spAsyncOp);
	if (FAILED(hr))
	{
		goto done;
	}

	hr = spAsyncOp->SetData(*pvarStartPos);
	if (FAILED(hr))
	{
		goto done;
	}

	hr = QueueOperation(spAsyncOp.Get());

done:

	return hr;
}


//-------------------------------------------------------------------
// Stop
// Stops the media source.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::Stop()
{
	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	// Fail if the source is shut down.
	hr = CheckShutdown();

	// Fail if the source was not initialized yet.
	if (SUCCEEDED(hr))
	{
		hr = IsInitialized();
	}

	// Queue the operation.
	if (SUCCEEDED(hr))
	{
		hr = QueueAsyncOperation(SourceOp::OP_STOP, -1);
	}

	return hr;
}

//-------------------------------------------------------------------
// IMFMediaSource methods
//-------------------------------------------------------------------

//-------------------------------------------------------------------
// GetService
// Returns a service
//-------------------------------------------------------------------

HRESULT CFFmpegSource::GetService(_In_ REFGUID guidService, _In_ REFIID riid, _Out_opt_ LPVOID *ppvObject)
{
	HRESULT hr = MF_E_UNSUPPORTED_SERVICE;

	if (ppvObject == nullptr)
	{
		return E_POINTER;
	}
	
	if (guidService == MF_RATE_CONTROL_SERVICE) 
	{
		hr = QueryInterface(riid, ppvObject);
	}

	return hr;
}

//-------------------------------------------------------------------
// IMFRateControl methods
//-------------------------------------------------------------------

//-------------------------------------------------------------------
// SetRate
// Sets a rate on the source. 
//-------------------------------------------------------------------

HRESULT CFFmpegSource::SetRate(BOOL fThin, float flRate)
{
	if (fThin)
	{
		return MF_E_THINNING_UNSUPPORTED;
	}

	AutoLock lock(m_critSec);
	HRESULT hr = S_OK;
	SourceOp *pAsyncOp = nullptr;

	if (flRate == m_flRate)
	{
		return S_OK;
	}

	hr = SourceOp::CreateSetRateOp(fThin, flRate, &pAsyncOp);

	if (SUCCEEDED(hr))
	{
		// Queue asynchronous stop
		hr = QueueOperation(pAsyncOp);
	}

	return hr;
}

//-------------------------------------------------------------------
// GetRate
// Returns a current rate.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::GetRate(_Inout_opt_ BOOL *pfThin, _Inout_opt_ float *pflRate)
{
	AutoLock lock(m_critSec);

	if (pfThin == nullptr || pflRate == nullptr)
	{
		return E_INVALIDARG;
	}
	
	*pfThin = FALSE;
	*pflRate = m_flRate;

	return S_OK;
}

HRESULT CFFmpegSource::GetFastestRate(MFRATE_DIRECTION eDirection, BOOL fThin, float *pflRate)
{
	if (fThin)
	{
		return MF_E_THINNING_UNSUPPORTED;
	}
	if (eDirection == MFRATE_DIRECTION::MFRATE_REVERSE || pflRate < 0)
	{
		return MF_E_REVERSE_UNSUPPORTED;
	}

	*pflRate = 4;

	return S_OK;
}

HRESULT CFFmpegSource::GetSlowestRate(MFRATE_DIRECTION eDirection, BOOL fThin, float *pflRate)
{
	if (fThin)
	{
		return MF_E_THINNING_UNSUPPORTED;
	}
	if (eDirection == MFRATE_DIRECTION::MFRATE_REVERSE || pflRate < 0)
	{
		return MF_E_REVERSE_UNSUPPORTED;
	}

	*pflRate = 0.5;

	return S_OK;
}

HRESULT CFFmpegSource::IsRateSupported(BOOL fThin, float flRate, float *pflNearestSupportedRate)
{
	if (fThin)
	{
		return MF_E_THINNING_UNSUPPORTED;
	}
	if (flRate < 0)
	{
		return MF_E_REVERSE_UNSUPPORTED;
	}
	if (flRate < 0.5 || flRate > 4)
	{
		return MF_E_UNSUPPORTED_RATE;
	}

	int tmp = (int)(flRate * 10);
	float nearRate = tmp / 10.0f;
	*pflNearestSupportedRate = nearRate;
	
	return S_OK;
}

//-------------------------------------------------------------------
// Public non-interface methods
//-------------------------------------------------------------------

//-------------------------------------------------------------------
// BeginOpen
// Begins reading the byte stream to initialize the source.
// Called by the byte-stream handler when it creates the source.
//
// This method is asynchronous. When the operation completes,
// the callback is invoked and the byte-stream handler calls
// EndOpen.
//
// pStream: Pointer to the byte stream for the FFmpeg stream.
// pCB: Pointer to the byte-stream handler's callback.
// pState: State object for the async callback. (Can be nullptr.)
//
// Note: The source reads enough data to find one packet header
// for each audio or video stream. This enables the source to
// create a presentation descriptor that describes the format of
// each stream. The source queues the packets that it reads during
// BeginOpen.
//-------------------------------------------------------------------

concurrency::task<void> CFFmpegSource::OpenAsync(IMFByteStream *pStream)
{
	if (pStream == nullptr)
	{
		throw ref new InvalidArgumentException();
	}

	if (m_state != STATE_INVALID)
	{
		ThrowException(MF_E_INVALIDREQUEST);
	}

	HRESULT hr = S_OK;

	if (SUCCEEDED(hr))
	{
		// Setup FFmpeg custom IO to access file as stream. This is necessary when accessing any file outside of app installation directory and appdata folder.
		// Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
		m_fileStreamBuffer = (unsigned char*)av_malloc(FILESTREAMBUFFERSZ);
		if (m_fileStreamBuffer == nullptr)
		{
			hr = E_OUTOFMEMORY;
			throw ref new OutOfMemoryException();
		}
	}
	// Cache the byte-stream pointer.
	m_spByteStream = pStream;

	if (SUCCEEDED(hr))
	{
		m_pAvIOCtx = avio_alloc_context(m_fileStreamBuffer, FILESTREAMBUFFERSZ, 0, m_spByteStream.Get(), FileStreamRead, 0, FileStreamSeek);
		if (m_pAvIOCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
			throw ref new OutOfMemoryException();
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx = avformat_alloc_context();
		if (m_pAvFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
			throw ref new OutOfMemoryException();
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx->pb = m_pAvIOCtx;
		m_pAvFormatCtx->flags |= AVFMT_FLAG_CUSTOM_IO;

		// Open media file using custom IO setup above instead of using file name. Opening a file using file name will invoke fopen C API call that only have
		// access within the app installation directory and appdata folder. Custom IO allows access to file selected using FilePicker dialog.
		if (avformat_open_input(&m_pAvFormatCtx, "", NULL, NULL) < 0)
		{
			hr = E_FAIL; // Error opening file
			throw ref new InvalidArgumentException();
		}
	}
	
	if (SUCCEEDED(hr))
	{
		if (avformat_find_stream_info(m_pAvFormatCtx, NULL) < 0)
		{
			hr = E_FAIL; // Error finding info
			throw ref new InvalidArgumentException();
		}
	}

	if (SUCCEEDED(hr))
	{
		//스트림 생성
		CreateStream();
		
		DWORD dwCaps = 0;

		AutoLock lock(m_critSec);

		// Create the media event queue.
		ThrowIfError(MFCreateEventQueue(m_spEventQueue.ReleaseAndGetAddressOf()));

		// Validate the capabilities of the byte stream.
		// The byte stream must be readable and seekable.
		ThrowIfError(pStream->GetCapabilities(&dwCaps));

		if ((dwCaps & MFBYTESTREAM_IS_SEEKABLE) == 0)
		{
			ThrowException(MF_E_BYTESTREAM_NOT_SEEKABLE);
		}
		else if ((dwCaps & MFBYTESTREAM_IS_READABLE) == 0)
		{
			ThrowException(MF_E_UNSUPPORTED_BYTESTREAM_TYPE);
		}

		m_state = STATE_OPENING;
		
		if (m_state == STATE_OPENING)
		{
			InitPresentationDescriptor();
		}
	}
	return concurrency::create_task(_openedEvent);
}

concurrency::task<void> CFFmpegSource::OpenURLAsync(LPCWSTR pwszURL)
{
	//String^ uri = L"http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_1080p_h264.mov";//
	//String^ uri = L"http://qthttp.apple.com.edgesuite.net/1010qwoeiuryfg/sl.m3u8";

	HRESULT hr = S_OK;
	String^ authUrl = nullptr;
	std::wstring uriW(pwszURL);
	std::string uriA;
	const char* charStr = nullptr;
	int codepage = 0;

	if (pwszURL == nullptr)
	{
		throw ref new InvalidArgumentException();
	}

	if (m_state != STATE_INVALID)
	{
		ThrowException(MF_E_INVALIDREQUEST);
	}

	if (SUCCEEDED(hr))
	{
		m_pAvFormatCtx = avformat_alloc_context();
		if (m_pAvFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
			throw ref new OutOfMemoryException();
		}
	}

	PropertySet^ backup = ref new PropertySet();
	if (m_ffmpegPropertySet != nullptr && m_ffmpegPropertySet->HasKey("codepage"))
	{
		codepage = static_cast<int>(m_ffmpegPropertySet->Lookup("codepage"));
		m_ffmpegPropertySet->Remove("codepage");
		backup->Insert("codepage", codepage);
	}

	if (m_ffmpegPropertySet != nullptr && m_ffmpegPropertySet->HasKey("auth_url"))
	{
		authUrl = dynamic_cast<String^>(m_ffmpegPropertySet->Lookup("auth_url"));
		uriW = std::wstring(authUrl->Data());
		m_ffmpegPropertySet->Remove("auth_url");
		backup->Insert("auth_url", authUrl);
	}

	if (SUCCEEDED(hr))
	{
		//m_ffmpegPropertySet->Insert("seekable", 1);
		// Populate AVDictionary avDict based on PropertySet ffmpegOptions. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
		hr = ParseOptions(m_ffmpegPropertySet);
	}

	if (m_ffmpegPropertySet != nullptr && backup->Size > 0)
	{
		if (backup->HasKey("codepage"))
		{
			m_ffmpegPropertySet->Insert("codepage", backup->Lookup("codepage"));
		}
		if (backup->HasKey("auth_url"))
		{
			m_ffmpegPropertySet->Insert("auth_url", backup->Lookup("auth_url"));
		}

		backup = nullptr;
	}

	if (SUCCEEDED(hr))
	{
		/*auto uri = ref new Uri(ref new String(uriW.c_str()));
		uriW = std::wstring(uri->AbsoluteCanonicalUri->Data());*/

		//auth url이 존재하면 적용, 그렇지 않으면 args적용
		uriA = std::string(uriW.begin(), uriW.end());
		charStr = uriA.c_str();
		//인코딩이 필요한 경우 인코딩해서 엎어씀
		if (codepage > 0)
		{
			charStr = (char*)GetStringBytes(authUrl, codepage)->Data;
		}

		// Open media in the given URI using the specified options
		if (avformat_open_input(&m_pAvFormatCtx, charStr, NULL, &m_pAvDict) < 0)
		{
			hr = E_FAIL; // Error opening file
			throw ref new InvalidArgumentException();
		}

		// avDict is not NULL only when there is an issue with the given ffmpegOptions such as invalid key, value type etc. Iterate through it to see which one is causing the issue.
		if (m_pAvDict != nullptr)
		{
			DebugMessage(L"Invalid FFmpeg option(s)");
			av_dict_free(&m_pAvDict);
			m_pAvDict = nullptr;
		}
	}

	if (SUCCEEDED(hr))
	{
		if (avformat_find_stream_info(m_pAvFormatCtx, NULL) < 0)
		{
			hr = E_FAIL; // Error finding info
			throw ref new InvalidArgumentException();
		}
	}

	if (SUCCEEDED(hr))
	{
		//스트림 생성
		CreateStream();

		DWORD dwCaps = 0;

		AutoLock lock(m_critSec);

		// Create the media event queue.
		ThrowIfError(MFCreateEventQueue(m_spEventQueue.ReleaseAndGetAddressOf()));

		m_state = STATE_OPENING;

		if (m_state == STATE_OPENING)
		{
			InitPresentationDescriptor();
		}
	}

	return concurrency::create_task(_openedEvent);
}

HRESULT CFFmpegSource::ParseOptions(PropertySet^ ffmpegOptions)
{
	HRESULT hr = S_OK;

	// Convert FFmpeg options given in PropertySet to AVDictionary. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
	if (ffmpegOptions != nullptr)
	{
		auto options = ffmpegOptions->First();

		while (options->HasCurrent)
		{
			String^ key = options->Current->Key;
			std::wstring keyW(key->Begin());
			std::string keyA(keyW.begin(), keyW.end());
			const char* keyChar = keyA.c_str();

			// Convert value from Object^ to const char*. avformat_open_input will internally convert value from const char* to the correct type
			String^ value = options->Current->Value->ToString();
			std::wstring valueW(value->Begin());
			std::string valueA(valueW.begin(), valueW.end());
			const char* valueChar = valueA.c_str();

			// Add key and value pair entry
			if (av_dict_set(&m_pAvDict, keyChar, valueChar, 0) < 0)
			{
				hr = E_INVALIDARG;
				break;
			}

			options->MoveNext();
		}
	}

	return hr;
}

/* Private methods */

CFFmpegSource::CFFmpegSource() : OpQueue(m_critSec.m_criticalSection)
, m_cRef(1)
, m_state(STATE_INVALID)
, m_cRestartCounter(0)
, m_flRate(1.0f)
, m_pAvIOCtx(nullptr)
, m_pAvFormatCtx(nullptr)
, m_pAvDict(nullptr)
, m_pReader(nullptr)
, m_mainStreamIndex(AVERROR_STREAM_NOT_FOUND)
, m_addedAudioStreamCount(1)
, m_addedBestAudioStream(false)
{
	//Velostep 앱 체크
	CheckPlatform();

	m_fDolbyCertifiedDevice = IsDolbyCertifiedDevice();
	
	auto module = ::Microsoft::WRL::GetModuleBase();
	if (module != nullptr)
	{
		module->IncrementObjectCount();
		//ffmpeg 초기화
		av_register_all();
	}
}

CFFmpegSource::~CFFmpegSource()
{
	if (m_state != STATE_SHUTDOWN)
	{
		Shutdown();
	}

	auto module = ::Microsoft::WRL::GetModuleBase();
	if (module != nullptr)
	{
		module->DecrementObjectCount();
	}

	if (m_pAvIOCtx)
	{
		av_freep(&m_pAvIOCtx->buffer);
		av_freep(&m_pAvIOCtx);
	}

	if (m_pAvFormatCtx)
	{
		avformat_close_input(&m_pAvFormatCtx);
	}

	if (m_pReader != NULL)
	{
		delete m_pReader;
		m_pReader = NULL;
	}
}


//-------------------------------------------------------------------
// Completeenen
//
// Completes the asynchronous BeginOpen operation.
//
// hrStatus: Status of the BeginOpen operation.
//-------------------------------------------------------------------

void CFFmpegSource::CompleteOpen(HRESULT hrStatus)
{
	assert(!_openedEvent._IsTriggered());
	if (FAILED(hrStatus))
	{
		Shutdown();
		_openedEvent.set_exception(ref new COMException(hrStatus));
	}
	else
	{
		_openedEvent.set();
	}
}


//-------------------------------------------------------------------
// IsInitialized:
// Returns S_OK if the source is correctly initialized with an
// FFmpeg byte stream. Otherwise, returns MF_E_NOT_INITIALIZED.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::IsInitialized() const
{
	if (m_state == STATE_OPENING || m_state == STATE_INVALID)
	{
		return MF_E_NOT_INITIALIZED;
	}
	else
	{
		return S_OK;
	}
}


//-------------------------------------------------------------------
// IsStreamTypeSupported:
// Returns TRUE if the source supports the specified FFmpeg stream
// type.
//-------------------------------------------------------------------

bool CFFmpegSource::IsStreamTypeSupported(AVMediaType type) const
{
	// We support audio and video streams.
	return m_pReader->IsSupportedMediaType(type);
}

//-------------------------------------------------------------------
// InitPresentationDescriptor
//
// Creates the source's presentation descriptor, if possible.
//
// During the BeginOpen operation, the source reads packets looking
// for headers for each stream. This enables the source to create the
// presentation descriptor, which describes the stream formats.
//
// This method tests whether the source has seen enough packets
// to create the PD. If so, it invokes the callback to complete
// the BeginOpen operation.
//-------------------------------------------------------------------

void CFFmpegSource::InitPresentationDescriptor()
{
	DWORD cStreams = 0;
	
	assert(m_spPresentationDescriptor == nullptr);
	assert(m_state == STATE_OPENING);

	// Get the number of streams, as declared in the FFmpeg header, skipping
	// any streams with an unsupported format.
	/*
	uint32 audioStreamCount = 0;
	for (unsigned int i = 0; i < m_pAvFormatCtx->nb_streams; i++)
	{
		if (IsStreamTypeSupported(m_pAvFormatCtx->streams[i]->codecpar->codec_type))
		{
			if (m_pAvFormatCtx->streams[i]->codecpar->codec_type == AVMediaType::AVMEDIA_TYPE_AUDIO)
			{
				if (++audioStreamCount > MAX_AUDIO_COUNT)
				{
					continue;
				}
			}
			else if (m_pAvFormatCtx->streams[i]->codecpar->codec_type == AVMediaType::AVMEDIA_TYPE_VIDEO)
			{
				//첨부 PNG등도 VIDEO로 들어 오므로 스트림 갯수에서 제외
				if (m_pAvFormatCtx->streams[i]->codec->codec == nullptr)
				{
					continue;
				}
			}

			cStreams++;
		}
	}

	// How many streams do we actually have?
	if (cStreams > m_streams.GetCount())
	{
		// Keep reading data until we have seen a packet for each stream.
		return;
	}
	*/

	//테스트 (사실 이미 사용할 스트림만 담은것이므로 의미가 없는 듯)
	cStreams = m_streams.GetCount();

	// We should never create a stream we don't support.
	assert(cStreams == m_streams.GetCount());

	// Ready to create the presentation descriptor.

	// Create an array of IMFStreamDescriptor pointers.
	IMFStreamDescriptor **ppSD =
		new (std::nothrow) IMFStreamDescriptor*[cStreams];

	if (ppSD == nullptr)
	{
		throw ref new OutOfMemoryException();
	}

	ZeroMemory(ppSD, cStreams * sizeof(IMFStreamDescriptor*));

	Exception ^error;
	try
	{

		// Fill the array by getting the stream descriptors from the streams.
		for (DWORD i = 0; i < cStreams; i++)
		{
			ThrowIfError(m_streams[i]->GetStreamDescriptor(&ppSD[i]));
		}

		// Create the presentation descriptor.
		ThrowIfError(MFCreatePresentationDescriptor(cStreams, ppSD,
			&m_spPresentationDescriptor));

		if (m_spByteStream != nullptr)
		{
			QWORD fileSize = 0;
			m_spByteStream->GetLength(&fileSize);
			ThrowIfError(m_spPresentationDescriptor->SetUINT64(MF_PD_TOTAL_FILE_SIZE, fileSize));
		}
		else if (m_pAvFormatCtx->pb != nullptr)
		{
			ThrowIfError(m_spPresentationDescriptor->SetUINT64(MF_PD_TOTAL_FILE_SIZE, m_pAvFormatCtx->pb->maxsize));
		}

		LONGLONG duration = ULONGLONG(m_pAvFormatCtx->duration * 10000000L / double(AV_TIME_BASE));
		ThrowIfError(m_spPresentationDescriptor->SetUINT64(MF_PD_DURATION, duration));

		// Select the first video stream (if any).
		for (DWORD i = 0; i < cStreams; i++)
		{
			ThrowIfError(m_spPresentationDescriptor->SelectStream(i));
		}
		
		// Switch state from "opening" to stopped.
		m_state = STATE_STOPPED;

		// Invoke the async callback to complete the BeginOpen operation.
		CompleteOpen(S_OK);
	}
	catch (Exception ^exc)
	{
		error = exc;
	}

	if (ppSD != nullptr)
	{
		for (DWORD i = 0; i < cStreams; i++)
		{
			ppSD[i]->Release();
		}
		delete[] ppSD;
	}

	if (error != nullptr)
	{
		throw error;
	}
}


//-------------------------------------------------------------------
// QueueAsyncOperation
// Queue an asynchronous operation.
//
// OpType: Type of operation to queue.
//
// Note: If the SourceOp object requires additional information, call
// OpQueue<SourceOp>::QueueOperation, which takes a SourceOp pointer.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::QueueAsyncOperation(SourceOp::Operation OpType, int streamIndex)
{
	HRESULT hr = S_OK;
	ComPtr<SourceOp> spOp;

	hr = SourceOp::CreateOp(OpType, &spOp);

	if (SUCCEEDED(hr))
	{
		spOp->SetStreamIndex(streamIndex);
		hr = QueueOperation(spOp.Get());
	}
	return hr;
}

//-------------------------------------------------------------------
// BeginAsyncOp
//
// Starts an asynchronous operation. Called by the source at the
// begining of any asynchronous operation.
//-------------------------------------------------------------------

void CFFmpegSource::BeginAsyncOp(SourceOp *pOp)
{
	// At this point, the current operation should be nullptr (the
	// previous operation is nullptr) and the new operation (pOp)
	// must not be nullptr.

	if (pOp == nullptr)
	{
		throw ref new InvalidArgumentException();
	}

	if (m_spCurrentOp != nullptr)
	{
		ThrowException(MF_E_INVALIDREQUEST);
	}

	// Store the new operation as the current operation.

	m_spCurrentOp = pOp;
}

//-------------------------------------------------------------------
// CompleteAsyncOp
//
// Completes an asynchronous operation. Called by the source at the
// end of any asynchronous operation.
//-------------------------------------------------------------------

void CFFmpegSource::CompleteAsyncOp(SourceOp *pOp)
{
	HRESULT hr = S_OK;

	// At this point, the current operation (m_spCurrentOp)
	// must match the operation that is ending (pOp).

	if (pOp == nullptr)
	{
		throw ref new InvalidArgumentException();
	}

	if (m_spCurrentOp == nullptr)
	{
		ThrowException(MF_E_INVALIDREQUEST);
	}

	if (m_spCurrentOp.Get() != pOp)
	{
		throw ref new InvalidArgumentException();
	}

	// Release the current operation.
	m_spCurrentOp.Reset();

	// Process the next operation on the queue.
	ThrowIfError(ProcessQueue());
}

//-------------------------------------------------------------------
// DispatchOperation
//
// Performs the asynchronous operation indicated by pOp.
//
// NOTE:
// This method implements the pure-virtual OpQueue::DispatchOperation
// method. It is always called from a work-queue thread.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::DispatchOperation(SourceOp *pOp)
{
	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	if (m_state == STATE_SHUTDOWN)
	{
		return S_OK; // Already shut down, ignore the request.
	}

	try
	{
		switch (pOp->Op())
		{
			// IMFMediaSource methods:
		case SourceOp::OP_START:
			DoStart((StartOp*)pOp);
			break;

		case SourceOp::OP_STOP:
			DoStop(pOp);
			break;

		case SourceOp::OP_PAUSE:
			DoPause(pOp);
			break;

		case SourceOp::OP_SETRATE:
			DoSetRate(pOp);
			break;

			// Operations requested by the streams:
		case SourceOp::OP_REQUEST_DATA:
			OnStreamRequestSample(pOp);
			break;

		case SourceOp::OP_END_OF_STREAM:
			OnEndOfStream(pOp);
			break;

		default:
			ThrowException(E_UNEXPECTED);
		}
	}
	catch (Exception ^exc)
	{
		StreamingError(exc->HResult);
	}

	return hr;
}


//-------------------------------------------------------------------
// ValidateOperation
//
// Checks whether the source can perform the operation indicated
// by pOp at this time.
//
// If the source cannot perform the operation now, the method
// returns MF_E_NOTACCEPTING.
//
// NOTE:
// Implements the pure-virtual OpQueue::ValidateOperation method.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::ValidateOperation(SourceOp *pOp)
{
	if (m_spCurrentOp != nullptr)
	{
		ThrowException(MF_E_NOTACCEPTING);
	}
	return S_OK;
}



//-------------------------------------------------------------------
// DoStart
// Perform an async start operation (IMFMediaSource::Start)
//
// pOp: Contains the start parameters.
//
// Note: This sample currently does not implement seeking, and the
// Start() method fails if the caller requests a seek.
//-------------------------------------------------------------------

void CFFmpegSource::DoStart(StartOp *pOp)
{
	StartFlag flag = StartFlag::START;
	assert(pOp->Op() == SourceOp::OP_START);

	BeginAsyncOp(pOp);

	try
	{
		ComPtr<IMFPresentationDescriptor> spPD;
		
		// Get the presentation descriptor from the SourceOp object.
		// This is the PD that the caller passed into the Start() method.
		// The PD has already been validated.
		ThrowIfError(pOp->GetPresentationDescriptor(&spPD));

		// If the sample supported seeking, we would need to get the
		// start position from the PROPVARIANT data contained in pOp.
		PROPVARIANT startPos = pOp->Data();
		UINT64 actualTime = 0;
		int64_t seekTarget = 0;

		if (startPos.vt == VT_I8)
		{
			// Select the first valid stream either from video or audio
			AVRational timeBase = m_pAvFormatCtx->streams[m_mainStreamIndex]->time_base;
			AVRational timeBaseQ = AVRational();
			timeBaseQ.num = 1;
			timeBaseQ.den = 10000000L; //100ns //AV_TIME_BASE * 10;
			seekTarget = av_rescale_q(startPos.hVal.QuadPart, timeBaseQ, timeBase);

			HRESULT hr = m_pReader->GetNextSampleTime(&actualTime);
			if (SUCCEEDED(hr))
			{
				if ((hr == ERROR_HANDLE_EOF && seekTarget == 0) 
					|| ((long)(actualTime / 1000000L) != (long)(startPos.hVal.QuadPart / 1000000L)))
				{
					flag = StartFlag::SEEK;
				}
			}
		}
		else if (startPos.vt == VT_EMPTY && m_state == STATE_PAUSED)
		{
			flag = StartFlag::PAUSE;
			DebugMessage(L"  Pause => Start\n\r");
		}

		if (flag == StartFlag::SEEK)
		{
			int seekFlag = (LONGLONG)actualTime > startPos.hVal.QuadPart ? AVSEEK_FLAG_BACKWARD : 0;
			int ret = av_seek_frame(m_pAvFormatCtx, m_mainStreamIndex, seekTarget, seekFlag);
			if (ret >= 0)
			{
				//큐 및 코덱 컨텍스트 버퍼 flush
				m_pReader->Flush();

				if (SUCCEEDED(m_pReader->GetNextSampleTime(&actualTime)))
				{
					//검색된 시간으로 다시 셋팅
					startPos.hVal.QuadPart = actualTime;
					pOp->SetData(startPos);
				}
			}
			else
			{
#if _DEBUG
				char errbuff[2048];
				av_strerror(ret, errbuff, 2048);
				std::string err(errbuff);
				DebugMessage(std::wstring(err.begin(), err.end()).c_str());
#endif
			}
		}

		// Select/deselect streams, based on what the caller set in the PD.
		// This method also sends the MENewStream/MEUpdatedStream events.
		SelectStreams(spPD.Get(), pOp->Data(), flag);

		m_state = STATE_STARTED;
		
		// Queue the "started" event. The event data is the start position.
		ThrowIfError(m_spEventQueue->QueueEventParamVar(
			flag == StartFlag::SEEK ? MESourceSeeked : MESourceStarted,
			GUID_NULL,
			S_OK,
			&pOp->Data()
			));
	}
	catch (Exception ^exc)
	{
		// Failure. Send the error code to the application.

		// Note: It's possible that QueueEvent itself failed, in which case it
		// is likely to fail again. But there is no good way to recover in
		// that case.

		(void)m_spEventQueue->QueueEventParamVar(
			flag == StartFlag::SEEK ? MESourceSeeked : MESourceStarted, GUID_NULL, exc->HResult, nullptr);

		CompleteAsyncOp(pOp);

		throw;
	}

	CompleteAsyncOp(pOp);
}


//-------------------------------------------------------------------
// DoStop
// Perform an async stop operation (IMFMediaSource::Stop)
//-------------------------------------------------------------------

void CFFmpegSource::DoStop(SourceOp *pOp)
{
	BeginAsyncOp(pOp);

	try
	{
		// Stop the active streams.
		for (DWORD i = 0; i < m_streams.GetCount(); i++)
		{
			if (m_streams[i]->IsActive())
			{
				m_streams[i]->Stop();
			}
		}
		// Seek to the start of the file. If we restart after stopping,
		// we will start from the beginning of the file again.
		//if (av_seek_frame(m_pAvFormatCtx, m_mainStreamIndex, 0, 0) >= 0)
		//{
		//	//큐비우기
		//	m_pReader->Flush();
		//}
		
		// Increment the counter that tracks "stale" read requests.
		++m_cRestartCounter; // This counter is allowed to overflow.

		m_spSampleRequest.Reset();

		m_state = STATE_STOPPED;

		// Send the "stopped" event. This might include a failure code.
		(void)m_spEventQueue->QueueEventParamVar(MESourceStopped, GUID_NULL, S_OK, nullptr);
	}
	catch (Exception ^exc)
	{
		m_spSampleRequest.Reset();

		m_state = STATE_STOPPED;

		// Send the "stopped" event. This might include a failure code.
		(void)m_spEventQueue->QueueEventParamVar(MESourceStopped, GUID_NULL, exc->HResult, nullptr);

		CompleteAsyncOp(pOp);

		throw;
	}

	CompleteAsyncOp(pOp);
}


//-------------------------------------------------------------------
// DoPause
// Perform an async pause operation (IMFMediaSource::Pause)
//-------------------------------------------------------------------

void CFFmpegSource::DoPause(SourceOp *pOp)
{
	BeginAsyncOp(pOp);

	try
	{
		// Pause is only allowed while running.
		if (m_state != STATE_STARTED)
		{
			ThrowException(MF_E_INVALID_STATE_TRANSITION);
		}

		// Pause the active streams.
		for (DWORD i = 0; i < m_streams.GetCount(); i++)
		{
			if (m_streams[i]->IsActive())
			{
				m_streams[i]->Pause();
			}
		}

		m_state = STATE_PAUSED;

		// Send the "paused" event. This might include a failure code.
		(void)m_spEventQueue->QueueEventParamVar(MESourcePaused, GUID_NULL, S_OK, nullptr);
	}
	catch (Exception ^exc)
	{
		// Send the "paused" event. This might include a failure code.
		(void)m_spEventQueue->QueueEventParamVar(MESourcePaused, GUID_NULL, exc->HResult, nullptr);

		CompleteAsyncOp(pOp);

		throw;
	}

	CompleteAsyncOp(pOp);
}

//-------------------------------------------------------------------
// DoSetRate
// Perform an async set rate operation (IMFRateControl::SetRate)
//-------------------------------------------------------------------

void CFFmpegSource::DoSetRate(SourceOp *pOp)
{
	SetRateOp *pSetRateOp = static_cast<SetRateOp*>(pOp);
	BeginAsyncOp(pOp);

	try
	{
		// Set rate on active streams.
		for (DWORD i = 0; i < m_streams.GetCount(); i++)
		{
			if (m_streams[i]->IsActive())
			{
				m_streams[i]->SetRate(pSetRateOp->GetRate());
			}
		}

		m_flRate = pSetRateOp->GetRate();

		(void)m_spEventQueue->QueueEventParamVar(MESourceRateChanged, GUID_NULL, S_OK, nullptr);
	}
	catch (Exception ^exc)
	{
		// Send the "rate changted" event. This might include a failure code.
		(void)m_spEventQueue->QueueEventParamVar(MESourceRateChanged, GUID_NULL, exc->HResult, nullptr);

		CompleteAsyncOp(pOp);

		throw;
	}

	CompleteAsyncOp(pOp);
}

//-------------------------------------------------------------------
// StreamRequestSample
// Called by streams when they need more data.
//
// Note: This is an async operation. The stream requests more data
// by queueing an OP_REQUEST_DATA operation.
//-------------------------------------------------------------------

void CFFmpegSource::OnStreamRequestSample(SourceOp *pOp)
{
	HRESULT hr = S_OK;

	BeginAsyncOp(pOp);

	// Ignore this request if we are already handling an earlier request.
	// (In that case m_pSampleRequest will be non-nullptr.)

	try
	{
		if (m_spSampleRequest == nullptr)
		{
			// Add the request counter as data to the operation.
			// This counter tracks whether a read request becomes "stale."
			PROPVARIANT var;
			var.vt = VT_UI4;
			var.ulVal = m_cRestartCounter;
			ThrowIfError(pOp->SetData(var));

			// Store this while the request is pending.
			m_spSampleRequest = pOp;

			// Try to parse data - this will invoke a read request if needed.
			DeliverPayload();
		}
	}
	catch (Exception ^exc)
	{
		CompleteAsyncOp(pOp);
		throw;
	}

	CompleteAsyncOp(pOp);
}


//-------------------------------------------------------------------
// OnEndOfStream
// Called by each stream when it sends the last sample in the stream.
//
// Note: When the media source reaches the end of the FFmpeg stream,
// it calls EndOfStream on each stream object. The streams might have
// data still in their queues. As each stream empties its queue, it
// notifies the source through an async OP_END_OF_STREAM operation.
//
// When every stream notifies the source, the source can send the
// "end-of-presentation" event.
//-------------------------------------------------------------------

void CFFmpegSource::OnEndOfStream(SourceOp *pOp)
{
	BeginAsyncOp(pOp);

	try
	{
		--m_cPendingEOS;
		if (m_cPendingEOS == 0)
		{
			// No more streams. Send the end-of-presentation event.
			ThrowIfError(m_spEventQueue->QueueEventParamVar(MEEndOfPresentation, GUID_NULL, S_OK, nullptr));
		}
	}
	catch (Exception ^exc)
	{
		CompleteAsyncOp(pOp);

		throw;
	}

	CompleteAsyncOp(pOp);
}



//-------------------------------------------------------------------
// SelectStreams
// Called during START operations to select and deselect streams.
//-------------------------------------------------------------------

void CFFmpegSource::SelectStreams(
	IMFPresentationDescriptor *pPD,   // Presentation descriptor.
	const PROPVARIANT varStart,        // New start position.
	StartFlag flag
	)
{
	
	BOOL fWasSelected = FALSE;
	DWORD   stream_id = 0;
	BOOL fSelected = FALSE;
	CFFmpegStream *wpStream = nullptr; // Not add-ref'd
	
	// Reset the pending EOS count.
	m_cPendingEOS = 0;
	
	// Loop throught the stream descriptors to find which streams are active.
	for (DWORD i = 0; i < m_streams.GetCount(); i++)
	{
		ComPtr<IMFStreamDescriptor> spSD;

		ThrowIfError(pPD->GetStreamDescriptorByIndex(i, &fSelected, &spSD));
		ThrowIfError(spSD->GetStreamIdentifier(&stream_id));

		wpStream = m_streams.Find((BYTE)stream_id);
		if (wpStream == nullptr)
		{
			ThrowException(E_INVALIDARG);
		}

		// Was the stream active already?
		fWasSelected = wpStream->IsActive();

		// Activate or deactivate the stream.
		wpStream->Activate(!!fSelected);

		if (fSelected)
		{
			m_cPendingEOS++;

			// If the stream was previously selected, send an "updated stream"
			// event. Otherwise, send a "new stream" event.
			MediaEventType met = fWasSelected ? MEUpdatedStream : MENewStream;

			ThrowIfError(m_spEventQueue->QueueEventParamUnk(met, GUID_NULL, S_OK, wpStream));
				
			// Start the stream. The stream will send the appropriate event.
			wpStream->Start(varStart, flag);
		}
	}
}


//-------------------------------------------------------------------
// EndOfMPEGStream:
// Called when the parser reaches the end of the FFmpeg stream.
//-------------------------------------------------------------------

void CFFmpegSource::EndOfFFmpegStream()
{
	// Notify the streams. The streams might have pending samples.
	// When each stream delivers the last sample, it will send the
	// end-of-stream event to the pipeline and then notify the
	// source.

	// When every stream is done, the source sends the end-of-
	// presentation event.

	for (DWORD i = 0; i < m_streams.GetCount(); i++)
	{
		if (m_streams[i]->IsActive())
		{
			m_streams[i]->EndOfStream();
		}
	}
}

void CFFmpegSource::CreateStream()
{
	HRESULT hr = S_OK;
	ComPtr<IMFMediaType> spType;
	MFSampleProvider* pSampleProvider = nullptr;
	int bestVideoStreamIndex = AVERROR_STREAM_NOT_FOUND;
	int bestAudioStreamIndex = AVERROR_STREAM_NOT_FOUND;
	int bestSubtitleStreamIndex = AVERROR_STREAM_NOT_FOUND;
	UINT32 cbAttachment = 0;
	UINT32 cbHybridMode = 0;
	bool forceSWDecoder = false;
	DecoderTypes decoderType = DecoderTypes::AUTO;

	if (m_avDecoderConnector != nullptr)
	{
		decoderType = m_avDecoderConnector->ReqDecoderType;
	}

	m_pReader = new FFmpegReader(m_pAvFormatCtx, m_avDecoderConnector, m_subtitleDecoderConnector, m_attachmentDecoderConnector);
	if (m_pReader == nullptr)
	{
		hr = E_OUTOFMEMORY;
	}

	if (SUCCEEDED(hr))
	{
		//기본 디코더 검색 및 기본 처리
		if (decoderType == DecoderTypes::AUTO)
		{
			//bytestream의 경우 디폴트값을 HW로 설정, uri의 경우 Hybrid로 설정
			if (m_spByteStream != nullptr)
				decoderType = DecoderTypes::HW;
			else
				decoderType = DecoderTypes::Hybrid;

			std::string foramtName(m_pAvFormatCtx->iformat->name);
			int audioCnt = 0;
			/*if (foramtName.find("mpeg") != std::string::npos)
			{
				decoderType = DecoderTypes::SW;
			}
			else*/ if (foramtName.find("flv") != std::string::npos || foramtName.find("rm") != std::string::npos)
			{
				decoderType = DecoderTypes::Hybrid;
			}
			else
			{	
				for (unsigned int i = 0; i < m_pAvFormatCtx->nb_streams; i++)
				{
					AVStream* stream = m_pAvFormatCtx->streams[i];
					AVMediaType codecType = GET_CODEC_CTX_PARAM(stream, codec_type);

					if (codecType == AVMediaType::AVMEDIA_TYPE_SUBTITLE)
					{
						//자막이 포함되어 있으면 Hybrid모드로 전환
						decoderType = DecoderTypes::Hybrid;
						break;
					}
					else if (codecType == AVMediaType::AVMEDIA_TYPE_AUDIO)
					{
						audioCnt++;

						if (!m_fDolbyCertifiedDevice && GET_CODEC_CTX_PARAM(stream, codec_id) == AVCodecID::AV_CODEC_ID_AC3
							&& Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
							decoderType = DecoderTypes::Hybrid;
					}
				}

				if (audioCnt > 2)
				{
					//음성 스트림이 3개 이상이면 SW demux이용
					decoderType = DecoderTypes::Hybrid;
				}
			}
		}

		//변경된 디코더 설정
		if (m_avDecoderConnector != nullptr)
		{
			m_avDecoderConnector->ReqDecoderType = decoderType;
		}
		ComPtr<IMFStreamDescriptor> spSD;
		ComPtr<CFFmpegStream> spStream;
		ComPtr<IMFMediaTypeHandler> spHandler;
		
		std::vector<MFSampleProvider*> sampleProviderList;

		MFSampleProvider* bestVideoSampleProvider = NULL;

		for (unsigned int i = 0; i < m_pAvFormatCtx->nb_streams; i++)
		{
			AVStream* stream = m_pAvFormatCtx->streams[i];
			// First see if the stream already exists.
			if (m_streams.Find(stream->index) != NULL)
			{
				// The stream already exists. Nothing to do.
				continue;
			}
			//프로바이더 초기화
			pSampleProvider = nullptr;
			AVCodec* avCodec = nullptr;
			
			switch (GET_CODEC_CTX_PARAM(stream, codec_type))
			{
			case AVMediaType::AVMEDIA_TYPE_VIDEO:
				// find the video stream and its decoder
				avCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(stream, codec_id));
				if (avCodec)
				{
					if (stream->disposition == AV_DISPOSITION_ATTACHED_PIC)
					{
						//파일내 첨부 이미지의 경우
					}
					else
					{
						AVCodecContext* pAvVideoCodecCtx = avcodec_alloc_context3(avCodec);
						if (pAvVideoCodecCtx)
						{
							if (FILL_CODEC_CTX(pAvVideoCodecCtx, stream) != 0) {
								OutputDebugMessage(L"Couldn't set video codec context\n");
							}
						}
						if (avcodec_open2(pAvVideoCodecCtx, avCodec, NULL) < 0)
						{
							avcodec_free_context(&pAvVideoCodecCtx);
							pAvVideoCodecCtx = nullptr;
							//hr = E_FAIL; // Cannot open the video codec
						}
						else
						{
							//10bit 비디오 체크
							AVPixelFormat fmt = pAvVideoCodecCtx->pix_fmt;
							forceSWDecoder = fmt == AV_PIX_FMT_YUV420P10 || fmt == AV_PIX_FMT_YUV420P10BE || fmt == AV_PIX_FMT_YUV420P10LE;

							if (!forceSWDecoder)
							{
								//MKV, 3GP2 이고 MPEG4(XVid) 체크 
								std::string foramtName(m_pAvFormatCtx->iformat->name);
								forceSWDecoder = pAvVideoCodecCtx->codec_id == AV_CODEC_ID_MPEG4
								//&& (foramtName.find("matroska") != std::string::npos || foramtName.find("3g2") != std::string::npos);
								&& (foramtName.find("3g2") != std::string::npos);
							}

							if (!forceSWDecoder && decoderType == DecoderTypes::Hybrid)
							{
								switch (pAvVideoCodecCtx->codec_id)
								{
								//case AV_CODEC_ID_HEVC:
								////case AV_CODEC_ID_H265:
								//	pSampleProvider = new HEVCSampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
								//	cbHybridMode++;
								//	break;
								case AV_CODEC_ID_H264:
									if (pAvVideoCodecCtx->extradata != nullptr && pAvVideoCodecCtx->extradata_size > 0 && pAvVideoCodecCtx->extradata[0] == 1)
									{
										pSampleProvider = new H264AVCSampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
									}
									else
									{
										pSampleProvider = new H264SampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
									}
									cbHybridMode++;
									break;
								case AV_CODEC_ID_H263:
								case AV_CODEC_ID_MPEG4:
									pSampleProvider = new MP4SampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
									cbHybridMode++;
									break;
									//case AV_CODEC_ID_WMV1:
									//case AV_CODEC_ID_WMV2:
								case AV_CODEC_ID_WMV3:
									pSampleProvider = new WMVSampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
									cbHybridMode++;
									break;
								case AV_CODEC_ID_VC1:
									pSampleProvider = new VC1SampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index);
									cbHybridMode++;
									break;
								}
							}

							if (pSampleProvider == nullptr)
							{
								pSampleProvider = new MFSampleProvider(m_pReader, m_pAvFormatCtx, pAvVideoCodecCtx, stream->index, CCPlayer::UWP::Common::Codec::DecoderTypes::SW);
							}

							if (bestVideoStreamIndex == AVERROR_STREAM_NOT_FOUND)
							{
								bestVideoStreamIndex = av_find_best_stream(m_pAvFormatCtx, AVMEDIA_TYPE_VIDEO, -1, -1, NULL, 0);
							}

							if (bestVideoStreamIndex == stream->index)
							{
								pSampleProvider->CodecInfo->IsBestStream = true;
								bestVideoSampleProvider = pSampleProvider;
							}
						}
					}
				}
				break;
			case AVMediaType::AVMEDIA_TYPE_AUDIO:
				// find the audio stream and its decoder
				avCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(stream, codec_id));
				if (avCodec)
				{
					AVCodecContext* pAvAudioCodecCtx = avcodec_alloc_context3(avCodec);
					if (pAvAudioCodecCtx)
					{
						if (FILL_CODEC_CTX(pAvAudioCodecCtx, stream) != 0) {
							OutputDebugMessage(L"Couldn't set audio codec context\n");
						}
					}
					if (avcodec_open2(pAvAudioCodecCtx, avCodec, NULL) < 0)
					{
						avcodec_free_context(&pAvAudioCodecCtx);
						pAvAudioCodecCtx = nullptr;
						//hr = E_FAIL;
					}
					else
					{
						if (!forceSWDecoder && decoderType == DecoderTypes::Hybrid)
						{
							int64_t bitRate = pAvAudioCodecCtx->bit_rate;
							int channel = pAvAudioCodecCtx->channels;
							int bitDepth = pAvAudioCodecCtx->bits_per_coded_sample > 0 && pAvAudioCodecCtx->bits_per_coded_sample <= 16 ? pAvAudioCodecCtx->bits_per_coded_sample : 16;
							int sampleRate = pAvAudioCodecCtx->sample_rate;

							switch (pAvAudioCodecCtx->codec_id)
							{
							case AV_CODEC_ID_AAC:
								if (bitRate > 0 && bitRate <= 320000 && (channel == 1 || channel == 2) && sampleRate <= 48000)
								{
									pSampleProvider = new AACSampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index);
								}
								break;
							case AV_CODEC_ID_MP3:
								if (bitRate > 0 && bitRate <= 320000 && (channel == 1 || channel == 2) && sampleRate <= 48000)
								{
									pSampleProvider = new MP3SampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index);
								}
								break;
							case AV_CODEC_ID_WMAPRO:
								if (bitRate > 0 && bitRate <= 768000 && (channel == 1 || channel == 2) && sampleRate <= 48000)
								{
									pSampleProvider = new WMASampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index);
								}
								break;
							case AV_CODEC_ID_WMALOSSLESS:
							case AV_CODEC_ID_WMAV1:
							case AV_CODEC_ID_WMAV2:
								if (bitRate > 0 && bitRate <= 384000 && (channel == 1 || channel == 2) && sampleRate <= 48000)
								{
									pSampleProvider = new WMASampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index);
								}
								break;
							case AV_CODEC_ID_AC3:
								if (m_fDolbyCertifiedDevice && bitRate > 0)
								{
									pSampleProvider = new AC3SampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index);
								}
								break;
							}
						}

						if (pSampleProvider == nullptr)
						{
							pSampleProvider = new MFSampleProvider(m_pReader, m_pAvFormatCtx, pAvAudioCodecCtx, stream->index, CCPlayer::UWP::Common::Codec::DecoderTypes::SW);
						}

						//디스크립터 생성 이전에 라이센스 모드 설정
						pSampleProvider->SetLicense(m_fDolbyCertifiedDevice);

						if (bestAudioStreamIndex == AVERROR_STREAM_NOT_FOUND)
						{
							bestAudioStreamIndex = av_find_best_stream(m_pAvFormatCtx, AVMEDIA_TYPE_AUDIO, -1, -1, NULL, 0);
						}
						
						if (bestAudioStreamIndex == stream->index)
						{
							pSampleProvider->CodecInfo->IsBestStream = true;
						}
						//프로바이더 목록에 추가			
						sampleProviderList.push_back(pSampleProvider);
					}
				}
				break;
			case AVMediaType::AVMEDIA_TYPE_SUBTITLE:
				if (decoderType != DecoderTypes::HW)
				{
					// find the audio stream and its decoder
					AVCodec* avSubtitleCodec = avcodec_find_decoder(GET_CODEC_CTX_PARAM(stream, codec_id));
					if (avSubtitleCodec)
					{
						AVCodecContext* pAvSubtitleCodecCtx = avcodec_alloc_context3(avSubtitleCodec);
						if (pAvSubtitleCodecCtx)
						{
							if (FILL_CODEC_CTX(pAvSubtitleCodecCtx, stream) != 0) {
								OutputDebugMessage(L"Couldn't set subtitle codec context\n");
							}
						}
						if (avcodec_open2(pAvSubtitleCodecCtx, avSubtitleCodec, NULL) < 0)
						{
							avcodec_free_context(&pAvSubtitleCodecCtx);
							pAvSubtitleCodecCtx = NULL;
							//hr = E_FAIL;
						}
						else
						{
							SubtitleProvider* subProvider = NULL;

							switch (pAvSubtitleCodecCtx->codec_id)
							{
							case AV_CODEC_ID_SRT:
							case AV_CODEC_ID_SUBRIP:
								subProvider = new SRTSampleProvider(m_pReader, m_pAvFormatCtx, pAvSubtitleCodecCtx, stream->index, CP_UTF8);
								break;
							case AV_CODEC_ID_ASS:
							case AV_CODEC_ID_SSA:
							case AV_CODEC_ID_MOV_TEXT:
								subProvider = new ASSSampleProvider(m_pReader, m_pAvFormatCtx, pAvSubtitleCodecCtx, stream->index, CP_UTF8);
								break;
							case AV_CODEC_ID_HDMV_PGS_SUBTITLE:
								subProvider = new PGSSampleProvider(m_pReader, m_pAvFormatCtx, pAvSubtitleCodecCtx, stream->index, CP_UTF8);
								break;
							case AV_CODEC_ID_XSUB:
								subProvider = new XSUBSampleProvider(m_pReader, m_pAvFormatCtx, pAvSubtitleCodecCtx, stream->index, CP_UTF8);
								break;
							case AV_CODEC_ID_FIRST_SUBTITLE:
								break;
							default:
								subProvider = new SubtitleProvider(m_pReader, m_pAvFormatCtx, pAvSubtitleCodecCtx, stream->index, CP_UTF8);
								break;
							}

							if (subProvider != NULL)
							{
								m_pReader->AddStream(subProvider);

								//코덱 정보 목록에 추가
								if (bestSubtitleStreamIndex == AVERROR_STREAM_NOT_FOUND)
								{
									bestSubtitleStreamIndex = av_find_best_stream(m_pAvFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, -1, NULL, 0);
								}

								if (bestSubtitleStreamIndex == stream->index)
								{
									subProvider->CodecInfo->IsBestStream = true;
								}

								if (m_avDecoderConnector != nullptr)
								{
									m_avDecoderConnector->CodecInformationList->Append(subProvider->CodecInfo);
								}
							}
						}
					}
				}
				break;
			case AVMediaType::AVMEDIA_TYPE_ATTACHMENT:
				if (decoderType != DecoderTypes::HW)
				{
					AttachmentProvider* attachmentProvider = new AttachmentProvider(m_pReader, m_pAvFormatCtx, NULL, stream->index);
					attachmentProvider->PopulateAttachment(stream->index);
					delete attachmentProvider;
					attachmentProvider = NULL;
					cbAttachment++;
				}
				break;
			default:
				// We validate the stream type before calling this method.
				//assert(IsStreamTypeSupported(packetHdr.type));
				//assert(false);
				//ThrowException(E_UNEXPECTED);
				continue;
			}

			if (!IsStreamTypeSupported(GET_CODEC_CTX_PARAM(stream, codec_type)) || pSampleProvider == NULL)
			{
				//지원되지 않는 스트림 (자막, 첨부 등) 또는 프로바이더를 생성하지 못한 경우는 제외시킴
				if (pSampleProvider != nullptr)
				{
					if (bestVideoSampleProvider == pSampleProvider)
					{
						bestVideoSampleProvider = NULL;
					}

					delete pSampleProvider;
					pSampleProvider = NULL;
				}
				continue;
			}

			//코덱 정보를 UI와 공유
			if (m_avDecoderConnector != nullptr)
			{
				m_avDecoderConnector->CodecInformationList->Append(pSampleProvider->CodecInfo);
			}
		}

		if (IsStreamTypeSupported(AVMediaType::AVMEDIA_TYPE_AUDIO))
		{
			if (sampleProviderList.size() > MAX_AUDIO_COUNT)
			{
				//sampleProviderList.erase(sampleProviderList.begin());
				for (int j = sampleProviderList.size(); j > 0; j--)
				{
					auto provider = sampleProviderList.at(j - 1);
					auto streamId = provider->CodecInfo->StreamId;

					if (!provider->CodecInfo->IsBestStream)
					{
						//Best스트림과 강제선택된 스트림을 제외한 모든 오디오 스트림 제거
						if (m_avDecoderConnector != nullptr && (m_avDecoderConnector->EnforceAudioStreamId == -1 || (m_avDecoderConnector->EnforceAudioStreamId != -1 && m_avDecoderConnector->EnforceAudioStreamId != streamId)))
						{
							sampleProviderList.erase(sampleProviderList.begin() + (j - 1));
							//소멸자 호출 확인
							delete provider;
							provider = NULL;
						}
					}

					if (sampleProviderList.size() <= MAX_AUDIO_COUNT)
					{
						break;
					}
				}
			}
		}
		else
		{
			sampleProviderList.clear();
		}

		if (IsStreamTypeSupported(AVMediaType::AVMEDIA_TYPE_VIDEO) && bestVideoSampleProvider != NULL)
		{
			sampleProviderList.insert(sampleProviderList.begin(), bestVideoSampleProvider);
		}
		else if (bestVideoSampleProvider)
		{
			delete bestVideoSampleProvider;
		}

		for (int i = 0; i < sampleProviderList.size(); i++)
		{
			MFSampleProvider* pSampleProvider = sampleProviderList.at(i);
			//기본 스트림으로 표기
			pSampleProvider->CodecInfo->IsBasicStream = true;
			//스트림 추가
			m_pReader->AddStream(pSampleProvider);
			//미디어 타입 설정
			pSampleProvider->CreateMediaType(&spType);
			// Create the stream descriptor from the media type.
			ThrowIfError(MFCreateStreamDescriptor(pSampleProvider->GetCurrentStreamIndex(), 1, spType.GetAddressOf(), &spSD));
			if (pSampleProvider->GetMediaType() == AVMediaType::AVMEDIA_TYPE_AUDIO)
			{
				JsonObject^ streamDescription = ref new JsonObject();
				streamDescription->SetNamedValue("LANG_CODE", JsonValue::CreateStringValue(pSampleProvider->CodecInfo->Language));
				streamDescription->SetNamedValue("TITLE", JsonValue::CreateStringValue(pSampleProvider->CodecInfo->Title));
				streamDescription->SetNamedValue("CODEC_NAME", JsonValue::CreateStringValue(pSampleProvider->CodecInfo->CodecName));
				streamDescription->SetNamedValue("CHANNELS", JsonValue::CreateNumberValue(pSampleProvider->CodecInfo->Channels));
				streamDescription->SetNamedValue("IS_BEST_STREAM", JsonValue::CreateBooleanValue(pSampleProvider->CodecInfo->IsBestStream));
				streamDescription->SetNamedValue("STREAM_INDEX", JsonValue::CreateNumberValue(pSampleProvider->CodecInfo->StreamId));
				//스트림의 언어 설정
				spSD->SetString(MF_SD_LANGUAGE, streamDescription->Stringify()->Data());
				//OutputDebugMessage(L"audio stream Id : %d, isBasic : %d, isBest %d\n", pSampleProvider->CodecInfo->StreamId, pSampleProvider->CodecInfo->IsBasicStream, pSampleProvider->CodecInfo->IsBestStream);
			}

			// Set the default media type on the stream handler.
			ThrowIfError(spSD->GetMediaTypeHandler(&spHandler));
			ThrowIfError(spHandler->SetCurrentMediaType(spType.Get()));

			// Create the new stream.
			spStream.Attach(new (std::nothrow) CFFmpegStream(this, spSD.Get(), pSampleProvider->GetCurrentStreamIndex()));
			if (spStream == NULL)
			{
				throw ref new OutOfMemoryException();
			}
			spStream->Initialize();

			// Add the stream to the array.
			ThrowIfError(m_streams.AddStream(pSampleProvider->GetCurrentStreamIndex(), spStream.Get()));
		}
		
		if (forceSWDecoder)
		{
			if (m_avDecoderConnector != nullptr)
			{
				m_avDecoderConnector->SetResult(DecoderTypes::SW, DecoderStates::Succeeded);
			}
		}
		else if (decoderType == DecoderTypes::HW)
		{
			if (m_avDecoderConnector != nullptr)
			{
				m_avDecoderConnector->SetResult(DecoderTypes::HW, DecoderStates::CheckError);
			}

			if (m_state != STATE_SHUTDOWN)
			{
				Shutdown();
			}
			//Bytestream핸들러로 시작한 경우
			ThrowException(MF_E_CANNOT_PARSE_BYTESTREAM);
			//Scheme핸들러로 시작한 경우 아래와 같은 오류를 반환해야 하는 줄알았으나 아니어도 됨
			//ThrowException(MF_E_UNSUPPORTED_BYTESTREAM_TYPE);
		}
		else
		{
			auto resultDecoderType = DecoderTypes::Hybrid;
			if (cbHybridMode == 0)
			{
				resultDecoderType = DecoderTypes::SW;
			}

			//최종 디코더 모드 결과
			if (m_avDecoderConnector != nullptr)
			{
				m_avDecoderConnector->SetResult(resultDecoderType, DecoderStates::Succeeded);
			}
		}
		
		if (m_mainStreamIndex == AVERROR_STREAM_NOT_FOUND)
		{
			if (bestVideoStreamIndex != AVERROR_STREAM_NOT_FOUND)
			{
				m_mainStreamIndex = bestVideoStreamIndex;
			}
			else if (bestAudioStreamIndex != AVERROR_STREAM_NOT_FOUND)
			{
				m_mainStreamIndex = bestAudioStreamIndex;
			}
			else
			{
				m_mainStreamIndex = 0;
			}
		}

		if (cbAttachment > 0 && m_attachmentDecoderConnector != nullptr)
		{
			m_attachmentDecoderConnector->Completed();
		}
	}
}

//-------------------------------------------------------------------
// ValidatePresentationDescriptor:
// Validates the presentation descriptor that the caller specifies
// in IMFMediaSource::Start().
//
// Note: This method performs a basic sanity check on the PD. It is
// not intended to be a thorough validation.
//-------------------------------------------------------------------

HRESULT CFFmpegSource::ValidatePresentationDescriptor(IMFPresentationDescriptor *pPD)
{
	try
	{
		BOOL fSelected = FALSE;
		DWORD cStreams = 0, cStreams2 = 0;

		// The caller's PD must have the same number of streams as ours.
		ThrowIfError(pPD->GetStreamDescriptorCount(&cStreams));
		if (cStreams != m_streams.GetCount())
		{
			ThrowException(E_INVALIDARG);
		}

		// The caller must select at least one stream.
		for (DWORD i = 0; i < cStreams; i++)
		{
			ComPtr<IMFStreamDescriptor> spSD;
			ThrowIfError(pPD->GetStreamDescriptorByIndex(i, &fSelected, &spSD));
			if (fSelected)
			{
				break;
			}
		}

		if (!fSelected)
		{
			throw ref new InvalidArgumentException();
		}
	}
	catch (Exception ^exc)
	{
		return exc->HResult;
	}
	return S_OK;
}


//-------------------------------------------------------------------
// StreamingError:
// Handles an error that occurs duing an asynchronous operation.
//
// hr: Error code of the operation that failed.
//-------------------------------------------------------------------

void CFFmpegSource::StreamingError(HRESULT hr)
{
	if (m_state == STATE_OPENING)
	{
		// An error happened during BeginOpen.
		// Invoke the callback with the status code.

		CompleteOpen(hr);
	}
	else if (m_state != STATE_SHUTDOWN)
	{
		// An error occurred during streaming. Send the MEError event
		// to notify the pipeline.

		QueueEvent(MEError, GUID_NULL, hr, nullptr);
	}
}

/* SourceOp class */


//-------------------------------------------------------------------
// CreateOp
// Static method to create a SourceOp instance.
//
// op: Specifies the async operation.
// ppOp: Receives a pointer to the SourceOp object.
//-------------------------------------------------------------------

HRESULT SourceOp::CreateOp(SourceOp::Operation op, SourceOp **ppOp)
{
	if (ppOp == nullptr)
	{
		return E_POINTER;
	}

	SourceOp *pOp = new (std::nothrow) SourceOp(op);

	if (pOp == nullptr)
	{
		return E_OUTOFMEMORY;
	}
	*ppOp = pOp;

	return S_OK;
}

//-------------------------------------------------------------------
// CreateStartOp:
// Static method to create a SourceOp instance for the Start()
// operation.
//
// pPD: Presentation descriptor from the caller.
// ppOp: Receives a pointer to the SourceOp object.
//-------------------------------------------------------------------

HRESULT SourceOp::CreateStartOp(IMFPresentationDescriptor *pPD, SourceOp **ppOp)
{
	if (ppOp == nullptr)
	{
		return E_POINTER;
	}

	SourceOp *pOp = new (std::nothrow) StartOp(pPD);
	
	if (pOp == nullptr)
	{
		return E_OUTOFMEMORY;
	}

	*ppOp = pOp;
	return S_OK;
}

//-------------------------------------------------------------------
// CreateSetRateOp:
// Static method to create a SourceOp instance for the SetRate()
// operation.
//
// fThin: TRUE - thinning is on, FALSE otherwise
// flRate: New rate
// ppOp: Receives a pointer to the SourceOp object.
//-------------------------------------------------------------------

HRESULT SourceOp::CreateSetRateOp(BOOL fThin, float flRate, SourceOp **ppOp)
{
	if (ppOp == nullptr)
	{
		return E_POINTER;
	}

	SourceOp *pOp = new (std::nothrow) SetRateOp(fThin, flRate);

	if (pOp == nullptr)
	{
		return E_OUTOFMEMORY;
	}

	*ppOp = pOp;
	return S_OK;
}

ULONG SourceOp::AddRef()
{
	return _InterlockedIncrement(&m_cRef);
}

ULONG SourceOp::Release()
{
	LONG cRef = _InterlockedDecrement(&m_cRef);
	if (cRef == 0)
	{
		delete this;
	}
	return cRef;
}

HRESULT SourceOp::QueryInterface(REFIID riid, void **ppv)
{
	if (ppv == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = E_NOINTERFACE;
	if (riid == IID_IUnknown)
	{
		(*ppv) = static_cast<IUnknown *>(this);
		AddRef();
		hr = S_OK;
	}

	return hr;
}

SourceOp::SourceOp(Operation op) : m_cRef(1), m_op(op)
{
	ZeroMemory(&m_data, sizeof(m_data));
}

SourceOp::~SourceOp()
{
	PropVariantClear(&m_data);
}

HRESULT SourceOp::SetData(const PROPVARIANT &var)
{
	return PropVariantCopy(&m_data, &var);
}

void SourceOp::SetStreamIndex(const int streamIndex)
{
	m_streamIndex = streamIndex;
}

StartOp::StartOp(IMFPresentationDescriptor *pPD) : SourceOp(SourceOp::OP_START), m_spPD(pPD)
{
}

StartOp::~StartOp()
{
}


HRESULT StartOp::GetPresentationDescriptor(IMFPresentationDescriptor **ppPD)
{
	if (ppPD == nullptr)
	{
		return E_POINTER;
	}
	if (m_spPD == nullptr)
	{
		return MF_E_INVALIDREQUEST;
	}
	*ppPD = m_spPD.Get();
	(*ppPD)->AddRef();
	return S_OK;
}

SetRateOp::SetRateOp(BOOL fThin, float flRate)
	: SourceOp(SourceOp::OP_SETRATE)
	, m_fThin(fThin)
	, m_flRate(flRate)
{
}

SetRateOp::~SetRateOp()
{
}

/*  Static functions */

// Get the major media type from a stream descriptor.
void GetStreamMajorType(IMFStreamDescriptor *pSD, GUID *pguidMajorType)
{
	if (pSD == nullptr || pguidMajorType == nullptr)
	{
		throw ref new InvalidArgumentException();
	}

	ComPtr<IMFMediaTypeHandler> spHandler;

	ThrowIfError(pSD->GetMediaTypeHandler(&spHandler));
	ThrowIfError(spHandler->GetMajorType(pguidMajorType));
}

void CFFmpegSource::DeliverPayload()
{
	// When this method is called, the read buffer contains a complete
	// payload, and the payload belongs to a stream whose type we support.
	CFFmpegStream *wpStream = nullptr;   // not AddRef'd
		
	int streamIndex = m_spSampleRequest->GetStreamIndex();
	// Deliver the payload to the stream.
	wpStream = m_streams.Find(streamIndex);
	assert(wpStream != nullptr);

	if (wpStream->IsRequested())
	{
		ComPtr<IMFSample> spSample;
		BYTE *pData = nullptr;              // Pointer to the IMFMediaBuffer data.
		
		HRESULT hr = m_pReader->GetNextSample(streamIndex, &spSample);

		if (FAILED(hr))
		{
			wpStream->EndOfStream();
		}
		else if (spSample.Get() != NULL)
		{
			wpStream->DeliverPayload(spSample.Get());
		}
	}

	//상태 초기화
	m_spSampleRequest.Reset();
}

bool CFFmpegSource::IsDolbyCertifiedDevice()
{
	bool isCertified = false;
	String^ deviceName = (ref new Windows::Security::ExchangeActiveSyncProvisioning::EasClientDeviceInformation())->SystemProductName;

	const int len = 7;
	String^ deviceNames[len] = {
		/*Lumia 1520 */ "RM-937", "RM-938", "RM-939", "RM-940",
		/*Lumia  930 */ "RM-1045",
		/*Lumia  830 */ "RM-984", "RM-985",
	};

	for (int i = 0; i < len; i++)
	{
		auto cdn = deviceName->Begin();
		auto dn = deviceNames[i]->Begin();

		if (deviceName->Length() >= deviceNames[i]->Length())
		{
			auto isSame = true;
			for (unsigned int j = 0; j < deviceNames[i]->Length(); j++)
			{
				if (dn[j] != cdn[j])
				{
					isSame = false;
					continue;
				}
			}

			if (isSame)
			{
				isCertified = true;
				break;
			}
		}
	}

	return isCertified;
}

// Static function to read file stream and pass data to FFmpeg. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize)
{
	IMFByteStream* pStream = reinterpret_cast<IMFByteStream*>(ptr);
	ULONG bytesRead = 0;
	HRESULT hr = pStream->Read(buf, bufSize, &bytesRead);

	if (FAILED(hr))
	{
		return -1;
	}

	// If we succeed but don't have any bytes, assume end of file
	if (bytesRead == 0)
	{
		return AVERROR_EOF;  // Let FFmpeg know that we have reached eof
	}
	
	return bytesRead;
}

// Static function to seek in file stream. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence)
{
	IMFByteStream* pStream = reinterpret_cast<IMFByteStream*>(ptr);

	QWORD out = 0;
	HRESULT hr = E_UNEXPECTED;
	switch (whence)
	{
	case SEEK_SET:
		hr = pStream->Seek(msoBegin, pos, MFBYTESTREAM_SEEK_FLAG_CANCEL_PENDING_IO, &out);
		break;
	case SEEK_CUR:
		hr = pStream->Seek(msoCurrent, pos, MFBYTESTREAM_SEEK_FLAG_CANCEL_PENDING_IO, &out);
		break;
	case SEEK_END:
	{
		QWORD length = 0;
		hr = pStream->GetLength(&length);
		if (!FAILED(hr))
		{
			hr = pStream->Seek(msoBegin, length + pos, MFBYTESTREAM_SEEK_FLAG_CANCEL_PENDING_IO, &out);
		}
		break;
	}
	case AVSEEK_SIZE:
		hr = pStream->GetLength(&out);
		break;
	}

	int64_t result = FAILED(hr) ? -1 : out;
	return result;
}

#pragma warning( pop )
