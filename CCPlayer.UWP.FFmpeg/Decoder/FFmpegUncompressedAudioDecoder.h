//////////////////////////////////////////////////////////////////////////
//
// FFmpegUncompressedAudioDecoder.h
// Implements the FFmpeg audio decoder.
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
#include <Common\CritSec.h>
#include "Source\CAVCodecContext.h"

extern "C"
{
//ffmepg
#include <libavcodec/avcodec.h>  
//#include <libavformat/avformat.h> 
#include <libswresample/swresample.h>
#include <libavutil/opt.h>

}

class CFFmpegUncompressedAudioDecoder WrlSealed
	: public Microsoft::WRL::RuntimeClass <
	Microsoft::WRL::RuntimeClassFlags< Microsoft::WRL::RuntimeClassType::WinRtClassicComMix >,
	ABI::Windows::Media::IMediaExtension,
	IMFTransform >
{
	InspectableClass(L"FFmpegDecoder.FFmpegUncompressedAudioDecoder", BaseTrust)
private:
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ m_avDecoderConnector;
public:
	CFFmpegUncompressedAudioDecoder();
	~CFFmpegUncompressedAudioDecoder();

	// IMediaExtension
	IFACEMETHOD(SetProperties) (ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration);

	// IMFTransform methods
	STDMETHODIMP GetStreamLimits(
		DWORD   *pdwInputMinimum,
		DWORD   *pdwInputMaximum,
		DWORD   *pdwOutputMinimum,
		DWORD   *pdwOutputMaximum
		);

	STDMETHODIMP GetStreamCount(
		DWORD   *pcInputStreams,
		DWORD   *pcOutputStreams
		);

	STDMETHODIMP GetStreamIDs(
		DWORD   dwInputIDArraySize,
		DWORD   *pdwInputIDs,
		DWORD   dwOutputIDArraySize,
		DWORD   *pdwOutputIDs
		);

	STDMETHODIMP GetInputStreamInfo(
		DWORD                     dwInputStreamID,
		MFT_INPUT_STREAM_INFO *   pStreamInfo
		);

	STDMETHODIMP GetOutputStreamInfo(
		DWORD                     dwOutputStreamID,
		MFT_OUTPUT_STREAM_INFO *  pStreamInfo
		);

	STDMETHODIMP GetAttributes(IMFAttributes **pAttributes);

	STDMETHODIMP GetInputStreamAttributes(
		DWORD           dwInputStreamID,
		IMFAttributes   **ppAttributes
		);

	STDMETHODIMP GetOutputStreamAttributes(
		DWORD           dwOutputStreamID,
		IMFAttributes   **ppAttributes
		);

	STDMETHODIMP DeleteInputStream(DWORD dwStreamID);

	STDMETHODIMP AddInputStreams(
		DWORD   cStreams,
		DWORD   *adwStreamIDs
		);

	STDMETHODIMP GetInputAvailableType(
		DWORD           dwInputStreamID,
		DWORD           dwTypeIndex, // 0-based
		IMFMediaType    **ppType
		);

	STDMETHODIMP GetOutputAvailableType(
		DWORD           dwOutputStreamID,
		DWORD           dwTypeIndex, // 0-based
		IMFMediaType    **ppType
		);

	STDMETHODIMP SetInputType(
		DWORD           dwInputStreamID,
		IMFMediaType    *pType,
		DWORD           dwFlags
		);

	STDMETHODIMP SetOutputType(
		DWORD           dwOutputStreamID,
		IMFMediaType    *pType,
		DWORD           dwFlags
		);

	STDMETHODIMP GetInputCurrentType(
		DWORD           dwInputStreamID,
		IMFMediaType    **ppType
		);

	STDMETHODIMP GetOutputCurrentType(
		DWORD           dwOutputStreamID,
		IMFMediaType    **ppType
		);

	STDMETHODIMP GetInputStatus(
		DWORD           dwInputStreamID,
		DWORD           *pdwFlags
		);

	STDMETHODIMP GetOutputStatus(DWORD *pdwFlags);

	STDMETHODIMP SetOutputBounds(
		LONGLONG        hnsLowerBound,
		LONGLONG        hnsUpperBound
		);

	STDMETHODIMP ProcessEvent(
		DWORD              dwInputStreamID,
		IMFMediaEvent      *pEvent
		);

	STDMETHODIMP ProcessMessage(
		MFT_MESSAGE_TYPE    eMessage,
		ULONG_PTR           ulParam
		);

	STDMETHODIMP ProcessInput(
		DWORD               dwInputStreamID,
		IMFSample           *pSample,
		DWORD               dwFlags
		);

	STDMETHODIMP ProcessOutput(
		DWORD                   dwFlags,
		DWORD                   cOutputBufferCount,
		MFT_OUTPUT_DATA_BUFFER  *pOutputSamples, // one per stream
		DWORD                   *pdwStatus
		);

protected:

	// HasPendingOutput: Returns TRUE if the MFT is holding an input sample.
	bool HasPendingOutput() const { return m_avFrameComplete > 0; }

	// IsValidInputStream: Returns TRUE if dwInputStreamID is a valid input stream identifier.
	static bool IsValidInputStream(DWORD dwInputStreamID)
	{
		return dwInputStreamID == 0;
	}

	// IsValidOutputStream: Returns TRUE if dwOutputStreamID is a valid output stream identifier.
	static bool IsValidOutputStream(DWORD dwOutputStreamID)
	{
		return dwOutputStreamID == 0;
	}

	//  Internal processing routine
	void InternalProcessOutput(IMFSample *pSample, IMFMediaBuffer *pOutputBuffer);
	void OnCheckInputType(IMFMediaType *pmt);
	void OnCheckOutputType(IMFMediaType *pmt);
	void OnSetInputType(IMFMediaType *pmt);
	void OnSetOutputType(IMFMediaType *pmt);
	void OnDiscontinuity();
	void AllocateStreamingResources();
	void FreeStreamingResources();
	void OnFlush();

protected:

	CritSec m_critSec;

	//  Streaming locals
	ComPtr<IMFMediaType> m_spInputType;     // Input media type.
	ComPtr<IMFMediaType> m_spOutputType;    // Output media type.

	//ffmpeg
	AVCodecContext *m_pAvCodecCtx;
	AVDictionary *opts;
	AVFrame *m_pAvFrame;
	int m_avFrameComplete;
	SwrContext* m_pSwrCtx;

	// Fomat information
	UINT32 m_frameSize;
	UINT32 m_channels;
	UINT64 m_channelLayout;
	UINT32 m_bitsPerSample;
	UINT32 m_sampleRate;
	UINT32 m_sampleFmt;
	UINT64 m_bitRate;
	double m_timeBase;
	UINT32 m_audioBufferSize;
	UINT32 m_licenseMode;
	double m_audioVolumeBoost;
};
