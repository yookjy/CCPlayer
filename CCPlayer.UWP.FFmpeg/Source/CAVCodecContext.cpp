#include "pch.h"
#include "CAVCodecContext.h"

CCAVCodecContext::CCAVCodecContext() : m_cRef(1)
{
}

ULONG CCAVCodecContext::AddRef()
{
	return _InterlockedIncrement(&m_cRef);
}

ULONG CCAVCodecContext::Release()
{
	LONG cRef = _InterlockedDecrement(&m_cRef);
	if (cRef == 0)
	{
		delete this;
	}
	return cRef;
}

HRESULT CCAVCodecContext::QueryInterface(REFIID riid, void **ppv)
{
	if (ppv == nullptr)
	{
		return E_POINTER;
	}

	HRESULT hr = E_NOINTERFACE;
	if (riid == IID_IUnknown)
	{
		(*ppv) = static_cast<CCAVCodecContext *>(this);
		AddRef();
		hr = S_OK;
	}

	return hr;
}

