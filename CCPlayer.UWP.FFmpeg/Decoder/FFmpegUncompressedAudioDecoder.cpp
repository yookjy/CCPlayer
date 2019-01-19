//////////////////////////////////////////////////////////////////////////
//
// FFmpegUncompressedAudioDecoder.cpp
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

#include "pch.h"
#include <wrl\module.h>
#include "FFmpegUncompressedAudioDecoder.h"
#include "Common\FFmpegMacro.h"

using namespace CCPlayer::UWP::Common::Interface;

ActivatableClass(CFFmpegUncompressedAudioDecoder);

//-------------------------------------------------------------------
// CFFmpegUncompressedAudioDecoder class
//-------------------------------------------------------------------

CFFmpegUncompressedAudioDecoder::CFFmpegUncompressedAudioDecoder() :
	m_pAvCodecCtx(NULL),
	m_pSwrCtx(NULL),
	m_pAvFrame(NULL),
	m_avFrameComplete(0),
	m_frameSize(0),
	m_channels(0),
	m_channelLayout(0),
	m_bitsPerSample(0),
	m_sampleRate(0),
	m_sampleFmt(0),
	m_bitRate(0),
	m_timeBase(0),
	m_licenseMode(0),
	m_audioBufferSize(4096),
	m_audioVolumeBoost(0)
{
	//Velostep 앱 체크
	CheckPlatform();
	
	avcodec_register_all();
}

CFFmpegUncompressedAudioDecoder::~CFFmpegUncompressedAudioDecoder()
{
	FreeStreamingResources();
}

// IMediaExtension methods

//-------------------------------------------------------------------
// Name: SetProperties
// Sets the configuration of the decoder
//-------------------------------------------------------------------
IFACEMETHODIMP CFFmpegUncompressedAudioDecoder::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet *pConfiguration)
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

HRESULT CFFmpegUncompressedAudioDecoder::GetStreamLimits(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetStreamCount(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetStreamIDs(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetInputStreamInfo(
	DWORD                     dwInputStreamID,
	MFT_INPUT_STREAM_INFO *   pStreamInfo
	)
{
	DebugMessage(L"AudioDecoder::GetInputStreamInfo\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::GetOutputStreamInfo(
	DWORD                     dwOutputStreamID,
	MFT_OUTPUT_STREAM_INFO *  pStreamInfo
	)
{
	//DebugMessage(L"AudioDecoder::GetOutputStreamInfo\r\n");
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

	if (m_spOutputType == nullptr)
	{
		pStreamInfo->cbSize = 0;
		pStreamInfo->cbAlignment = 0;
	}
	else
	{
		pStreamInfo->cbSize = m_audioBufferSize;
		pStreamInfo->cbAlignment = 1;
	}

	return S_OK;
}



//-------------------------------------------------------------------
// Name: GetAttributes
// Returns the attributes for the MFT.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedAudioDecoder::GetAttributes(IMFAttributes** /*pAttributes*/)
{
	// This MFT does not support any attributes, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: GetInputStreamAttributes
// Returns stream-level attributes for an input stream.
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedAudioDecoder::GetInputStreamAttributes(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetOutputStreamAttributes(
	DWORD           /*dwOutputStreamID*/,
	IMFAttributes   ** /*ppAttributes*/
	)
{
	// This MFT does not support any attributes, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: DeleteInputStream
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedAudioDecoder::DeleteInputStream(DWORD /*dwStreamID*/)
{
	// This MFT has a fixed number of input streams, so the method is not implemented.
	return E_NOTIMPL;
}



//-------------------------------------------------------------------
// Name: AddInputStreams
//-------------------------------------------------------------------

HRESULT CFFmpegUncompressedAudioDecoder::AddInputStreams(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetInputAvailableType(
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

HRESULT CFFmpegUncompressedAudioDecoder::GetOutputAvailableType(
	DWORD           dwOutputStreamID,
	DWORD           dwTypeIndex, // 0-based
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"AudioDecoder::GetOutputAvailableType\r\n");
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
		
		WAVEFORMATEX format;
		ZeroMemory(&format, sizeof(format));

		format.wFormatTag = WAVE_FORMAT_PCM;
		format.nChannels = m_channels;
		format.nSamplesPerSec = m_sampleRate;
		format.wBitsPerSample = m_bitsPerSample;
		format.nBlockAlign = (WORD)(m_channels * m_bitsPerSample / 8);
		format.nAvgBytesPerSec = format.nBlockAlign * m_sampleRate;
		format.cbSize = 0;

		//// Use the structure to initialize the Media Foundation media type.
		ThrowIfError(MFCreateMediaType(&spOutputType));
		ThrowIfError(MFInitMediaTypeFromWaveFormatEx(spOutputType.Get(), (const WAVEFORMATEX*)&format, sizeof(format)));
		
		//pOutputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
		////spOutputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
		//spOutputType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
		//spOutputType->SetUINT32(MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, 0x2A /*DP4MEDIA_MP4A_AUDIO_PLI_AAC_L4*/);
		//spOutputType->SetUINT32(MF_MT_AAC_PAYLOAD_TYPE, 1);		// payload 1 = ADTS header
		//spOutputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
		//spOutputType->SetUINT32(MF_MT_AUDIO_CHANNEL_MASK, (2 == 2 ? 0x03 : 0x3F));
		//spOutputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, (UINT32)2);
		//spOutputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, (UINT32)48000);



		///*spOutputType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, m_channels);
		//spOutputType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, m_sampleRate);
		//spOutputType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, m_bitsPerSample);
		//spOutputType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, (m_channels * m_bitsPerSample / 8));
		//spOutputType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, (m_channels * m_bitsPerSample / 8) * m_sampleRate);*/
		////spOutputType->SetUINT32(MF_MT_USER_DATA);
		//byte arrUser[] = { 0x01, 0x00, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x90 };	// hard wired for 2 channel, 48000 kHz
		//spOutputType->SetBlob(MF_MT_USER_DATA, arrUser, 14);

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

HRESULT CFFmpegUncompressedAudioDecoder::SetInputType(
	DWORD           dwInputStreamID,
	IMFMediaType    *pType, // Can be nullptr to clear the input type.
	DWORD           dwFlags
	)
{
	HRESULT hr = S_OK;
	//DebugMessage(L"AudioDecoder::SetInputType\r\n");

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

HRESULT CFFmpegUncompressedAudioDecoder::SetOutputType(
	DWORD           dwOutputStreamID,
	IMFMediaType    *pType, // Can be nullptr to clear the output type.
	DWORD           dwFlags
	)
{
	//DebugMessage(L"AudioDecoder::SetOutputType\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::GetInputCurrentType(
	DWORD           dwInputStreamID,
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"AudioDecoder::GetInputCurrentType\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::GetOutputCurrentType(
	DWORD           dwOutputStreamID,
	IMFMediaType    **ppType
	)
{
	//DebugMessage(L"AudioDecoder::GetInputCurrentType\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::GetInputStatus(
	DWORD           dwInputStreamID,
	DWORD           *pdwFlags
	)
{
	//DebugMessage(L"AudioDecoder::GetInputStatus\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::GetOutputStatus(DWORD *pdwFlags)
{
	//DebugMessage(L"AudioDecoder::GetOutputStatus\r\n");
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

HRESULT CFFmpegUncompressedAudioDecoder::SetOutputBounds(
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

HRESULT CFFmpegUncompressedAudioDecoder::ProcessEvent(
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

HRESULT CFFmpegUncompressedAudioDecoder::ProcessMessage(
	MFT_MESSAGE_TYPE    eMessage,
	ULONG_PTR           /*ulParam*/
	)
{
	//DebugMessage(L"AudioDecoder::ProcessMessage\r\n");
	AutoLock lock(m_critSec);

	HRESULT hr = S_OK;

	try
	{
		switch (eMessage)
		{
		case MFT_MESSAGE_COMMAND_FLUSH:
			// Flush the MFT.
			OnFlush();
			break;

		case MFT_MESSAGE_COMMAND_DRAIN:
			// Set the discontinuity flag on all of the input.
			OnDiscontinuity();
			break;

		case MFT_MESSAGE_SET_D3D_MANAGER:
			// The pipeline should never send this message unless the MFT
			// has the MF_SA_D3D_AWARE attribute set to TRUE. However, if we
			// do get this message, it's invalid and we don't implement it.
			ThrowException(E_NOTIMPL);
			break;


		case MFT_MESSAGE_NOTIFY_BEGIN_STREAMING:
			AllocateStreamingResources();
			break;
			//디버거가 실행중에는 앱이 종료시에만 호출되지만, 디버거가 실행(attached) 상태가 아니면 Minimize시 혹은 복귀시에 호출이 되는 것으로 판단된다.
			//그렇기 때문에 여기서 리소스를 해제하면 앱 크래쉬가 발생하는것으로 판단된다.
		case MFT_MESSAGE_NOTIFY_END_STREAMING:
			//FreeStreamingResources();
			break;

			// These messages do not require a response.
		case MFT_MESSAGE_NOTIFY_START_OF_STREAM:
		case MFT_MESSAGE_NOTIFY_END_OF_STREAM:
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

HRESULT CFFmpegUncompressedAudioDecoder::ProcessInput(
	DWORD               dwInputStreamID,
	IMFSample           *pSample,
	DWORD               dwFlags
	)
{
	//	DebugMessage(L"AudioDecoder::ProcessInput\r\n");

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
			if (decode_frame(m_pAvCodecCtx, m_pAvFrame, &m_avFrameComplete, &avpkt, false) < 0)
			{
				OutputDebugMessage(L"Failed decode audio frame!\n");
				break;
			}

			if (m_avFrameComplete)
			{
				m_pAvFrame->pts = avpkt.pts;
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

HRESULT CFFmpegUncompressedAudioDecoder::ProcessOutput(
	DWORD                   dwFlags,
	DWORD                   cOutputBufferCount,
	MFT_OUTPUT_DATA_BUFFER  *pOutputSamples, // one per stream
	DWORD                   *pdwStatus
	)
{
	//	DebugMessage(L"AudioDecoder::ProcessOutput\r\n");

	HRESULT hr = S_OK;
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
		if (pOutputSamples[0].pSample == nullptr)
		{
			throw ref new InvalidArgumentException();
		}

		AutoLock lock(m_critSec);

		// If we don't have an input sample, we need some input before
		// we can generate any output.
		if (!HasPendingOutput())
		{
			//			DebugMessage(L"AudioDecoder::ProcessOutput() ====> Need more input .... \r\n");
			return MF_E_TRANSFORM_NEED_MORE_INPUT;
		}

		ComPtr<IMFMediaBuffer> spOutput;
		DWORD cbData = 0;

		// Get the output buffer.
		ThrowIfError(pOutputSamples[0].pSample->GetBufferByIndex(0, &spOutput));
		ThrowIfError(spOutput->GetMaxLength(&cbData));

		InternalProcessOutput(pOutputSamples[0].pSample, spOutput.Get());
		//  Is there any more data to output at this point?

		//pOutputSamples[0].dwStatus |= MFT_OUTPUT_DATA_BUFFER_INCOMPLETE;
		pOutputSamples[0].dwStatus = 0;
	}
	catch (Exception ^exc)
	{
		hr = exc->HResult;
	}

	return hr;
}

// Private class methods
void CFFmpegUncompressedAudioDecoder::InternalProcessOutput(IMFSample *pSample, IMFMediaBuffer *pOutputBuffer)
{
	//DebugMessage(L"AudioDecoder::ProcessOutput => InternalProcessOutput()\r\n");
	if (m_avFrameComplete)
	{
		ThrowIfError(pSample->SetUINT32(MFSampleExtension_CleanPoint, m_pAvFrame->key_frame));


		// Resample uncompressed frame to AV_SAMPLE_FMT_S16 PCM format that is expected by Media Element
		uint8_t *resampledData = nullptr;
		unsigned int aBufferSize = av_samples_alloc(&resampledData, NULL, m_pAvFrame->channels, m_pAvFrame->nb_samples, AV_SAMPLE_FMT_S16, m_pAvFrame->format == AV_SAMPLE_FMT_S16 ? 0 : 1);
		//aBufferSize = av_samples_fill_arrays(&resampledData, NULL, (const uint8_t *)m_pAvFrame->data, m_pAvFrame->channels, m_pAvFrame->nb_samples, AV_SAMPLE_FMT_S16, 0);
		
		//if (m_licenseMode == 0)
		{
			auto newBoostValue = m_avDecoderConnector != nullptr ? m_avDecoderConnector->AudioVolumeBoost : 0;
			if (m_pSwrCtx == nullptr || m_audioVolumeBoost != newBoostValue)
			{
				m_audioVolumeBoost = newBoostValue;
				// Set default channel layout when the value is unknown (0)
				int64 inChannelLayout = m_channelLayout ? m_channelLayout : av_get_default_channel_layout(m_channels);
				int64 outChannelLayout = av_get_default_channel_layout(m_channels);
				//int64 outChannelLayout = av_get_default_channel_layout(2);
				
				// Set up resampler to convert any PCM format (e.g. AV_SAMPLE_FMT_FLTP) to AV_SAMPLE_FMT_S16 PCM format that is expected by Media Element.
				// Additional logic can be added to avoid resampling PCM data that is already in AV_SAMPLE_FMT_S16_PCM.
				m_pSwrCtx = swr_alloc_set_opts(
					NULL,
					outChannelLayout,
					AV_SAMPLE_FMT_S16,
					m_sampleRate,
					inChannelLayout,
					(AVSampleFormat)m_pAvFrame->format,
					m_sampleRate,
					0,
					NULL);
				
				auto psdB = m_audioVolumeBoost + "dB";
				std::string sdB(psdB->Begin(), psdB->End());
				av_opt_set(m_pSwrCtx, "rmvol", sdB.c_str(), 0);
				//av_opt_set_int(m_pSwrCtx, "rmvol", m_audioVolumeBoost, 0);
				//av_opt_set_double(m_pSwrCtx, "rematrix_volume", 1.0 + m_avDecoderConnector->AudioVolumeBoost, 0);

				//eq는 어케 쓰는거냐..
				//av_opt_set(m_pSwrCtx, "equalizer", "f=1000:width_type=h:width=200:g=-10", 0);
				//av_opt_set(m_pSwrCtx, "base", "f=100:width_type=o:width=200:g=+20", 0);
				
				
				//https://ffmpeg.org/ffmpeg-resampler.html
				//av_opt_set_double(m_pSwrCtx, "center_mix_level", 32, 0);
				//av_opt_set_double(m_pSwrCtx, "surround_mix_level", 32, 0);
				//av_opt_set_double(m_pSwrCtx, "lfe_mix_level", 32, 0);
				
				if (!m_pSwrCtx)
				{
					ThrowIfError(MF_E_INVALIDTYPE);
				}

				if (swr_init(m_pSwrCtx) < 0)
				{
					ThrowIfError(MF_E_INVALIDTYPE);
				}
			}

			int resampledDataSize = swr_convert(m_pSwrCtx, &resampledData, aBufferSize, (const uint8_t **)m_pAvFrame->extended_data, m_pAvFrame->nb_samples);
			aBufferSize = min(aBufferSize, (unsigned int)(resampledDataSize * m_pAvFrame->channels * av_get_bytes_per_sample(AV_SAMPLE_FMT_S16)));

			BYTE *pbData = NULL;
			DWORD cbData = 0;

			ThrowIfError(pOutputBuffer->Lock(&pbData, NULL, &cbData));
			CopyMemory(pbData, resampledData, aBufferSize);
			ThrowIfError(pOutputBuffer->Unlock());
		}
		
		ThrowIfError(pOutputBuffer->SetCurrentLength((DWORD)aBufferSize));

		//release resource
		av_freep(&resampledData);

		//  Set the timestamp
		//  Uncompressed video must always have a timestamp
		LONGLONG hnsStart = ULONGLONG(m_timeBase * 10000000 * m_pAvFrame->pts);
		LONGLONG hnsSampleDuration = ULONGLONG(m_timeBase * 10000000 * m_pAvFrame->pkt_duration);

		ThrowIfError(pSample->SetSampleTime(hnsStart));
		/*wchar_t* dest = new wchar_t[255];
		swprintf_s(dest, 255, L"AudioDecoder::ProcessOutput => AudioDecoder pts is %I64d\r\n", hnsStart);
		OutputDebugStringW(dest);
		delete[] dest;*/

		if (hnsSampleDuration >= 0)
		{
			ThrowIfError(pSample->SetSampleDuration(hnsSampleDuration));
		}
	}

	m_avFrameComplete = 0;
	av_frame_unref(m_pAvFrame);
}

void CFFmpegUncompressedAudioDecoder::OnCheckInputType(IMFMediaType *pmt)
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

	//  We accept MFMediaType_Audio, MFAudioFormat_FFmpeg_SW
	ThrowIfError(pmt->GetMajorType(&majortype));
	if (majortype != MFMediaType_Audio)
		ThrowException(MF_E_INVALIDTYPE);

	ThrowIfError(pmt->GetGUID(MF_MT_SUBTYPE, &subtype));
	if (subtype != MFAudioFormat_FFmpeg_SW)
		ThrowException(MF_E_INVALIDTYPE);
}


void CFFmpegUncompressedAudioDecoder::OnSetInputType(IMFMediaType *pmt)
{
	m_spInputType.Reset();

	//ffmpeg
	ThrowIfError(pmt->GetUINT32(MF_MT_FRAME_SIZE, &m_frameSize));
	ThrowIfError(pmt->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &m_channels));
	ThrowIfError(pmt->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &m_bitsPerSample));
	ThrowIfError(pmt->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &m_sampleRate));
	ThrowIfError(pmt->GetUINT32(MF_MT_MY_FFMPEG_SAMPLE_FMT, &m_sampleFmt));
	ThrowIfError(pmt->GetUINT64(MF_MT_AVG_BITRATE, &m_bitRate));
	ThrowIfError(pmt->GetDouble(MF_MT_MY_FFMPEG_TIME_BASE, &m_timeBase));
	ThrowIfError(pmt->GetUINT32(MF_MT_MY_FFMPEG_CODEC_LICENSE, &m_licenseMode));

	if (m_pAvCodecCtx == nullptr)
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
					OutputDebugMessage(L"Couldn't set audio codec context\n");
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
	
	m_channelLayout = m_pAvCodecCtx->channel_layout;
	//출력 버퍼 사이즈 설정
	m_audioBufferSize = (UINT32)(m_channels * m_bitsPerSample / 8) * m_sampleRate;
	m_spInputType = pmt;
}

void CFFmpegUncompressedAudioDecoder::OnCheckOutputType(IMFMediaType *pmt)
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


void CFFmpegUncompressedAudioDecoder::OnSetOutputType(IMFMediaType *pmt)
{
	m_spOutputType = pmt;
}


void CFFmpegUncompressedAudioDecoder::AllocateStreamingResources()
{
	m_pAvFrame = av_frame_alloc();
	//  Reinitialize variables
	OnDiscontinuity();
}

void CFFmpegUncompressedAudioDecoder::FreeStreamingResources()
{
	if (m_pSwrCtx != NULL)
		swr_free(&m_pSwrCtx);
	
	if (m_pAvCodecCtx != NULL)
		avcodec_free_context(&m_pAvCodecCtx);

	if (m_pAvFrame != NULL)
		av_frame_free(&m_pAvFrame);

	m_avFrameComplete = 0;
}

void CFFmpegUncompressedAudioDecoder::OnDiscontinuity()
{
	if (m_pAvFrame != NULL)
		av_frame_unref(m_pAvFrame);
	//  Zero our timestamp
	m_avFrameComplete = 0;
}

void CFFmpegUncompressedAudioDecoder::OnFlush()
{
	OnDiscontinuity();
	if (m_pAvCodecCtx != NULL && m_pAvCodecCtx->codec != NULL && avcodec_is_open(m_pAvCodecCtx))
	{
		avcodec_flush_buffers(m_pAvCodecCtx);
	}
}

