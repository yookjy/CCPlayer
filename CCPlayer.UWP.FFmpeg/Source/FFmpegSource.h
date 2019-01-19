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


#pragma once
// Common sample files.
#include "Common\LinkList.h"
#include "Common\asynccb.h"
#include "Common\OpQueue.h"
#include "Common\critsec.h"

#include "Source\Provider\MFSampleProvider.h"
#include "Source\Provider\Video\H264SampleProvider.h"
#include "Source\Provider\Video\H264AVCSampleProvider.h"
//#include "Source\Provider\Video\HEVCSampleProvider.h"
#include "Source\Provider\Video\MP4SampleProvider.h"
#include "Source\Provider\Video\WMVSampleProvider.h"
#include "Source\Provider\Video\VC1SampleProvider.h"
#include "Source\Provider\Audio\AACSampleProvider.h"
#include "Source\Provider\Audio\MP3SampleProvider.h"
#include "Source\Provider\Audio\AC3SampleProvider.h"
#include "Source\Provider\Audio\WMASampleProvider.h"
#include "Source\Provider\Subtitle\SubtitleProvider.h"
#include "Source\Provider\Subtitle\ASSSampleProvider.h"
#include "Source\Provider\Subtitle\SRTSampleProvider.h"
#include "Source\Provider\Subtitle\PGSSampleProvider.h"
#include "Source\Provider\Subtitle\XSUBSampleProvider.h"
#include "Source\Provider\Attachment\AttachmentProvider.h"
#include "FFmpegReader.h"

extern "C"
{
#include <libavutil/imgutils.h>
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>  
#include <libswscale/swscale.h>
#include <libavutil/imgutils.h>
}

// Forward declares
class CFFmpegSource;
class CFFmpegStream;
class SourceOp;

typedef ComPtrList<IMFSample>       SampleList;
typedef ComPtrList<IUnknown, true>  TokenList;    // List of tokens for IMFMediaStream::RequestSample

using namespace CCPlayer::UWP::Common::Interface;

//class CStopWatch
//{
//private:
//	clock_t start;
//	clock_t finish;
//
//public:
//	double GetDuration() { return (double)(finish - start) / CLOCKS_PER_SEC; }
//	void Start() { start = clock(); }
//	void Stop() { finish = clock(); }
//}; 

enum SourceState
{
	STATE_INVALID,      // Initial state. Have not started opening the stream.
	STATE_OPENING,      // BeginOpen is in progress.
	STATE_STOPPED,
	STATE_PAUSED,
	STATE_STARTED,
	STATE_SHUTDOWN
};

enum StartFlag
{
	START,
	PAUSE,
	SEEK
};

#include "FFmpegStream.h"    // FFmpeg stream

const UINT32 MAX_STREAMS = 32;

class StreamList sealed
{
	ComPtr<CFFmpegStream>  m_streams[MAX_STREAMS];
	BYTE m_id[MAX_STREAMS];
	UINT32 m_count;

public:
	StreamList() : m_count(0)
	{
	}

	~StreamList()
	{
		Clear();
	}

	UINT32 GetCount() const { return m_count; }

	void Clear()
	{
		for (UINT32 i = 0; i < MAX_STREAMS; i++)
		{
			m_streams[i].Reset();
		}
		m_count = 0;
	}

	HRESULT AddStream(BYTE id, CFFmpegStream *pStream)
	{
		if (GetCount() >= MAX_STREAMS)
		{
			return E_FAIL;
		}

		m_streams[m_count] = pStream;
		m_id[m_count] = id;
		m_count++;

		return S_OK;
	}

	CFFmpegStream *Find(BYTE id)
	{

		// This method can return nullptr if the source did not create a
		// stream for this ID. In particular, this can happen if:
		//
		// 1) The stream type is not supported. See IsStreamTypeSupported().
		// 2) The source is still opening.
		//
		// Note: This method does not AddRef the stream object. The source
		// uses this method to access the streams. If the source hands out
		// a stream pointer (e.g. in the MENewStream event), the source
		// must AddRef the stream object.

		CFFmpegStream *pStream = nullptr;
		for (UINT32 i = 0; i < m_count; i++)
		{
			if (m_id[i] == id)
			{
				pStream = m_streams[i].Get();
				break;
			}
		}
		return pStream;
	}

	// Accessor.
	CFFmpegStream *operator[](DWORD index)
	{
		assert(index < m_count);
		return m_streams[index].Get();
	}

	// Const accessor.
	CFFmpegStream *const operator[](DWORD index) const
	{
		assert(index < m_count);
		return m_streams[index].Get();
	}
};


// Constants

//const DWORD INITIAL_BUFFER_SIZE = 4 * 1024; // Initial size of the read buffer. (The buffer expands dynamically.)
//const DWORD READ_SIZE = 4 * 1024;           // Size of each read request.
const DWORD SAMPLE_QUEUE = 2;               // How many samples does each stream try to hold in its queue?
// Represents a request for an asynchronous operation.

class SourceOp : public IUnknown
{
public:

	enum Operation
	{
		OP_START,
		OP_PAUSE,
		OP_STOP,
		OP_SETRATE,
		OP_REQUEST_DATA,
		OP_END_OF_STREAM
	};

	static HRESULT CreateOp(Operation op, SourceOp **ppOp);
	static HRESULT CreateStartOp(IMFPresentationDescriptor *pPD, SourceOp **ppOp);
	static HRESULT CreateSetRateOp(BOOL fThin, float flRate, SourceOp **ppOp);

	// IUnknown
	STDMETHODIMP QueryInterface(REFIID iid, void **ppv);
	STDMETHODIMP_(ULONG) AddRef();
	STDMETHODIMP_(ULONG) Release();

	SourceOp(Operation op);
	virtual ~SourceOp();

	HRESULT SetData(const PROPVARIANT &var);
	void	SetStreamIndex(const int streamIndex);
	int	GetStreamIndex() const { return m_streamIndex;  }

	Operation Op() const { return m_op; }
	const PROPVARIANT &Data() { return m_data; }

protected:
	int			m_streamIndex;
	long        m_cRef;     // Reference count.
	Operation   m_op;
	PROPVARIANT m_data;     // Data for the operation.
};

class StartOp WrlSealed : public SourceOp
{
public:
	StartOp(IMFPresentationDescriptor *pPD);
	~StartOp();

	HRESULT GetPresentationDescriptor(IMFPresentationDescriptor **ppPD);

protected:
	ComPtr<IMFPresentationDescriptor> m_spPD; // Presentation descriptor for Start operations.

};

class SetRateOp WrlSealed : public SourceOp
{
public:
	SetRateOp(BOOL fThin, float flRate);
	~SetRateOp();

	BOOL IsThin() const { return m_fThin; }
	float GetRate() const { return m_flRate; }

private:
	BOOL m_fThin;
	float m_flRate;
};

// CFFmpegSource: The media source object.
class CFFmpegSource WrlSealed :
	public OpQueue<CFFmpegSource, SourceOp>,
	public IMFMediaSource,
	public IMFGetService,
	public IMFRateControl,
	public IMFRateSupport
{
public:
	static ComPtr<CFFmpegSource> CreateInstance(IAVDecoderConnector^, ISubtitleDecoderConnector^, IAttachmentDecoderConnector^, PropertySet^);

	// IUnknown
	STDMETHODIMP QueryInterface(REFIID iid, void **ppv);
	STDMETHODIMP_(ULONG) AddRef();
	STDMETHODIMP_(ULONG) Release();

	// IMFMediaEventGenerator
	STDMETHODIMP BeginGetEvent(IMFAsyncCallback *pCallback, IUnknown *punkState);
	STDMETHODIMP EndGetEvent(IMFAsyncResult *pResult, IMFMediaEvent **ppEvent);
	STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent **ppEvent);
	STDMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, const PROPVARIANT *pvValue);
	
	// IMFMediaSource
	STDMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor **ppPresentationDescriptor);
	STDMETHODIMP GetCharacteristics(DWORD *pdwCharacteristics);
	STDMETHODIMP Pause();
	STDMETHODIMP Shutdown();
	STDMETHODIMP Start(
		IMFPresentationDescriptor *pPresentationDescriptor,
		const GUID *pguidTimeFormat,
		const PROPVARIANT *pvarStartPosition
		);
	STDMETHODIMP Stop();

	// IMFGetService
	IFACEMETHOD(GetService) (_In_ REFGUID guidService, _In_ REFIID riid, _Out_opt_ LPVOID *ppvObject);

	// IMFRateControl
	IFACEMETHOD(SetRate) (BOOL fThin, float flRate);
	IFACEMETHOD(GetRate) (_Inout_opt_ BOOL *pfThin, _Inout_opt_ float *pflRate);

	// IMFRateSupport
	STDMETHODIMP GetFastestRate(MFRATE_DIRECTION eDirection, BOOL fThin, float *pflRate);
	STDMETHODIMP GetSlowestRate(MFRATE_DIRECTION eDirection, BOOL fThin, float *pflRate);
	STDMETHODIMP IsRateSupported(BOOL fThin, float flRate, float *pflNearestSupportedRate);

	// Called by the byte stream handler.
	concurrency::task<void> OpenAsync(IMFByteStream *pStream);
	concurrency::task<void> OpenURLAsync(LPCWSTR pwszURL);

	// Queues an asynchronous operation, specify by op-type.
	// (This method is public because the streams call it.)
	HRESULT QueueAsyncOperation(SourceOp::Operation OpType, int streamIndex);
	HRESULT ParseOptions(PropertySet^ ffmpegOptions);

	// Lock/Unlock:
	// Holds and releases the source's critical section. Called by the streams.
	_Acquires_lock_(m_critSec)
		void    Lock() { m_critSec.Lock(); }

	_Releases_lock_(m_critSec)
		void    Unlock() { m_critSec.Unlock(); }

private:

	CFFmpegSource();
	~CFFmpegSource();

	// CheckShutdown: Returns MF_E_SHUTDOWN if the source was shut down.
	HRESULT CheckShutdown() const
	{
		return (m_state == STATE_SHUTDOWN ? MF_E_SHUTDOWN : S_OK);
	}

	void        CompleteOpen(HRESULT hrStatus);

	HRESULT     IsInitialized() const;
	bool        IsStreamTypeSupported(AVMediaType type) const;

	void        DoStart(StartOp *pOp);
	void        DoStop(SourceOp *pOp);
	void        DoPause(SourceOp *pOp);
	void        DoSetRate(SourceOp *pOp);
	void        OnStreamRequestSample(SourceOp *pOp);
	void        OnEndOfStream(SourceOp *pOp);

	void        InitPresentationDescriptor();
	void        SelectStreams(IMFPresentationDescriptor *pPD, const PROPVARIANT varStart, StartFlag flag);

	void        EndOfFFmpegStream();

	void        CreateStream();

	HRESULT     ValidatePresentationDescriptor(IMFPresentationDescriptor *pPD);

	// Handler for async errors.
	void        StreamingError(HRESULT hr);

	void        BeginAsyncOp(SourceOp *pOp);
	void        CompleteAsyncOp(SourceOp *pOp);
	HRESULT     DispatchOperation(SourceOp *pOp);
	HRESULT     ValidateOperation(SourceOp *pOp);
	
private:
	long                        m_cRef;                     // reference count

	CritSec                     m_critSec;                  // critical section for thread safety
	SourceState                 m_state;                    // Current state (running, stopped, paused)

	ComPtr<IMFMediaEventQueue>  m_spEventQueue;             // Event generator helper
	ComPtr<IMFPresentationDescriptor> m_spPresentationDescriptor; // Presentation descriptor.

	concurrency::task_completion_event<void> _openedEvent;  // Event used to signalize end of open operation.
	ComPtr<IMFByteStream>       m_spByteStream;

	StreamList                  m_streams;                  // Array of streams.

	DWORD                       m_cPendingEOS;              // Pending EOS notifications.
	ULONG                       m_cRestartCounter;          // Counter for sample requests.

	ComPtr<SourceOp>            m_spCurrentOp;
	ComPtr<SourceOp>            m_spSampleRequest;


	float                       m_flRate;

	//여기부터
	AVDictionary* m_pAvDict;
	AVIOContext* m_pAvIOCtx;
	AVFormatContext* m_pAvFormatCtx;

	unsigned char* m_fileStreamBuffer;
	int m_mainStreamIndex;
	int m_addedAudioStreamCount;
	bool m_addedBestAudioStream;
	
	void DeliverPayload();
	bool IsDolbyCertifiedDevice();

	FFmpegReader* m_pReader;
	bool m_fDolbyCertifiedDevice;
	//CStopWatch gSt = CStopWatch();

	IAVDecoderConnector^ m_avDecoderConnector;
	ISubtitleDecoderConnector^ m_subtitleDecoderConnector;
	IAttachmentDecoderConnector^ m_attachmentDecoderConnector;
	PropertySet^ m_ffmpegPropertySet;
};