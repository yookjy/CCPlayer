//////////////////////////////////////////////////////////////////////////
//
// FFmpegUncompressedVideoDecoder.h
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

#pragma once
#include <Common\CritSec.h>
#include "DirectXVideoTransform.h"
#include "Source\CAVCodecContext.h"
#include <d2d1_1.h>


extern "C"
{
	//ffmepg
	#include <libavcodec/avcodec.h>  
	//#include <libavformat/avformat.h> 
	#include <libswscale/swscale.h>
	#include <libavutil/imgutils.h>
}

//static const REFERENCE_TIME INVALID_TIME = _I64_MAX;    //  Not really invalid but unlikely enough for sample code.

//-------------------------------------------------------------------
// CFFmpegUncompressedVideoDecoder
//
// Implements the FFmpeg "FFmpegUncompressedVideoDecoder" MFT.
//
// The decoder outputs RGB-32 only.
//
// Note: This MFT is derived from a sample that used to ship in the
// DirectX SDK.
//-------------------------------------------------------------------

class CFFmpegUncompressedVideoDecoder WrlSealed
	: public Microsoft::WRL::RuntimeClass<
	Microsoft::WRL::RuntimeClassFlags< Microsoft::WRL::RuntimeClassType::WinRtClassicComMix >,
	ABI::Windows::Media::IMediaExtension,
	IMFTransform>
{
	InspectableClass(L"FFmpegDecoder.FFmpegUncompressedVideoDecoder", BaseTrust)

private:
	CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ m_avDecoderConnector;

public:
	CFFmpegUncompressedVideoDecoder();
	~CFFmpegUncompressedVideoDecoder();

	STDMETHOD(RuntimeClassInitialize)();

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
	void ConvertColorSpaceByShader(AVFrame*, IMFMediaBuffer *pOut);
	void ConvertColorSpaceByFFmpeg(AVFrame*, IMFMediaBuffer *pOut);
	void ConvertColorSpaceByCpu(AVFrame* frame, IMFMediaBuffer *pOut);

	//DirectX
	void UpdateDX11Device();
	void CheckDX11Device();
	void InvalidateDX11Resources();
	void BeginStreaming();

protected:

	CritSec m_critSec;

	//  Streaming locals
	ComPtr<IMFMediaType> m_spInputType;     // Input media type.
	ComPtr<IMFMediaType> m_spOutputType;    // Output media type.

	//ffmpeg
	AVCodecContext *m_pAvCodecCtx;
	AVFrame *m_pAvFrame;
	int m_avFrameComplete;
	SwsContext *m_pSwsCtx;

	// Fomat information
	UINT32 m_imageWidthInPixels;
	UINT32 m_imageHeightInPixels;
	UINT32 m_interlace;
	MFRatio m_frameRate;
	MFRatio m_aspectRatio;
	DWORD m_cbImageSize;                    // Image size, in bytes.
	UINT64 m_bitRate;
	double m_timeBase;

	// DirectX manager
	ComPtr<IMFDXGIDeviceManager> m_spDX11Manager;
	HANDLE m_hDeviceHandle;

	// Device resources
	ComPtr<ID3D11Device> m_spDevice;
	ComPtr<ID3D11DeviceContext> m_spContext;
	ComPtr<ID3D11Texture2D> m_spInBufferTex;
	ComPtr<ID3D11Texture2D> m_spOutBufferTex;
	ComPtr<ID3D11Texture2D> m_spOutBufferStage;

	// Transform
	DirectXVideoTransform ^m_transform;

	ComPtr<IMFAttributes> m_spAttributes;
	ComPtr<IMFAttributes> m_spOutputAttributes;
	ComPtr<IMFVideoSampleAllocatorEx> m_spOutputSampleAllocator;

	// Streaming
	bool m_fStreamingInitialized;
	
	ComPtr<ID3D11Texture2D> m_spInBufferTexY;
	ComPtr<ID3D11Texture2D> m_spInBufferTexU;
	ComPtr<ID3D11Texture2D> m_spInBufferTexV;

	bool m_fGpuShader;
};
