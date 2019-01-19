#pragma once

extern "C"
{
#include <libavformat/avformat.h>
#include <libavutil/imgutils.h>
#include <libswscale\swscale.h>
}

class CCAVCodecContext WrlSealed : IUnknown
{
public:
	CCAVCodecContext();
	// IUnknown
	STDMETHODIMP QueryInterface(REFIID iid, void **ppv);
	STDMETHODIMP_(ULONG) AddRef();
	STDMETHODIMP_(ULONG) Release();

	AVStream* Stream;

private:
	long                        m_cRef;                     // reference count
};

