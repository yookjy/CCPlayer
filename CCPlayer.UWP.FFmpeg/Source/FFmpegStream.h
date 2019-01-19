//////////////////////////////////////////////////////////////////////////
//
// FFmpegStream.h
// Implements the stream object (IMFMediaStream) for the MPEG-1 source.
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

class CFFmpegSource;

// The media stream object.
class CFFmpegStream WrlSealed : public IMFMediaStream
{
public:

	CFFmpegStream(CFFmpegSource *pSource, IMFStreamDescriptor *pSD, unsigned streamIndex);
	~CFFmpegStream();
	void Initialize();

	// IUnknown
	STDMETHODIMP QueryInterface(REFIID iid, void **ppv);
	STDMETHODIMP_(ULONG) AddRef();
	STDMETHODIMP_(ULONG) Release();

	// IMFMediaEventGenerator
	STDMETHODIMP BeginGetEvent(IMFAsyncCallback *pCallback, IUnknown *punkState);
	STDMETHODIMP EndGetEvent(IMFAsyncResult *pResult, IMFMediaEvent **ppEvent);
	STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent **ppEvent);
	STDMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, const PROPVARIANT *pvValue);

	// IMFMediaStream
	STDMETHODIMP GetMediaSource(IMFMediaSource **ppMediaSource);
	STDMETHODIMP GetStreamDescriptor(IMFStreamDescriptor **ppStreamDescriptor);
	STDMETHODIMP RequestSample(IUnknown *pToken);

	// Other methods (called by source)
	void     Activate(bool fActive);
	void     Start(const PROPVARIANT &varStart, StartFlag flag);
	void     Pause();
	void     Stop();
	void     SetRate(float flRate);
	void     EndOfStream();
	void     Shutdown();

	bool     IsActive() const { return m_fActive; }
	bool     NeedsData();
	bool     IsRequested() const { return m_bRequested; }
	unsigned int GetStreamIndex() const { return m_streamIndex; }
	void   DeliverPayload(IMFSample *pSample);

	// Callbacks
	HRESULT     OnDispatchSamples(IMFAsyncResult *pResult);
	static int IgnoreStreamIndex;
private:

	// SourceLock class:
	// Small helper class to lock and unlock the source.
	class SourceLock
	{
	private:
		CFFmpegSource *m_pSource;
	public:
		_Acquires_lock_(m_pSource)
			SourceLock(CFFmpegSource *pSource);

		_Releases_lock_(m_pSource)
			~SourceLock();
	};

private:

	HRESULT CheckShutdown() const
	{
		return (m_state == STATE_SHUTDOWN ? MF_E_SHUTDOWN : S_OK);
	}
	void DispatchSamples() throw();


private:
	long                m_cRef;                 // reference count

	ComPtr<CFFmpegSource> m_spSource;            // Parent media source
	ComPtr<IMFStreamDescriptor> m_spStreamDescriptor;
	ComPtr<IMFMediaEventQueue> m_spEventQueue;  // Event generator helper

	SourceState         m_state;                // Current state (running, stopped, paused)
	bool                m_fActive;              // Is the stream active?
	bool                m_fEOS;                 // Did the source reach the end of the stream?

	SampleList          m_Samples;              // Samples waiting to be delivered.
	TokenList           m_Requests;             // Sample requests, waiting to be dispatched.

	float               m_flRate;

	bool m_bRequested;
	unsigned int m_streamIndex;
};


