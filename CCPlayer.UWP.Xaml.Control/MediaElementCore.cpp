//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
#include "pch.h"
#include <wrl.h>
#include <mfapi.h>
#include <strsafe.h>

#include "MediaElementCore.h"
#include "d3dmanagerlock.hxx"
#include <windows.ui.xaml.media.dxinterop.h>


#ifdef _DEBUG
#ifndef DBG_NEW
#define DBG_NEW new ( _NORMAL_BLOCK , __FILE__ , __LINE__ )
#define new DBG_NEW
#endif
#endif  // _DEBUG

using namespace CCPlayer::UWP::Xaml::Controls;
using namespace std;
using namespace Microsoft::WRL;
using namespace Windows::System::Threading;
using namespace Windows::Foundation;
using namespace Windows::ApplicationModel::Core;
using namespace Windows::UI::Core;
using namespace Windows::Storage;
using namespace Windows::Storage::Pickers;
using namespace Windows::Storage::Streams;
using namespace Windows::UI::Xaml::Media;
using namespace concurrency;
using namespace Platform;

// MediaEngineNotify: Implements the callback for Media Engine event notification.
class MediaEngineNotify : public IMFMediaEngineNotify
{
	long m_cRef;
	//MediaEngineNotifyCallback^ m_pCB;
	Platform::WeakReference m_pCB;
	/*property MediaEngineNotifyCallback^ m_pCB
	{
		MediaEngineNotifyCallback^ get() { return m_pCB.Resolve<MediaEngineNotifyCallback>(); }
		void set(MediaEngineNotifyCallback^ value) { _pCB = value; }
	};*/

public:
	MediaEngineNotify() : m_cRef(1)//, m_pCB(nullptr)
	{
	}
	~MediaEngineNotify()
	{
	}

	STDMETHODIMP QueryInterface(REFIID riid, void** ppv)
	{
		if (__uuidof(IMFMediaEngineNotify) == riid)
		{
			*ppv = static_cast<IMFMediaEngineNotify*>(this);
		}
		else
		{
			*ppv = nullptr;
			return E_NOINTERFACE;
		}

		AddRef();

		return S_OK;
	}

	STDMETHODIMP_(ULONG) AddRef()
	{
		return InterlockedIncrement(&m_cRef);
	}

	STDMETHODIMP_(ULONG) Release()
	{
		LONG cRef = InterlockedDecrement(&m_cRef);
		if (cRef == 0)
		{
			delete this;
		}
		return cRef;
	}

	// EventNotify is called when the Media Engine sends an event.
	STDMETHODIMP EventNotify(DWORD meEvent, DWORD_PTR param1, DWORD param2)
	{
		if (meEvent == MF_MEDIA_ENGINE_EVENT_NOTIFYSTABLESTATE)
		{
			SetEvent(reinterpret_cast<HANDLE>(param1));
		}
		else
		{
			//m_pCB->OnMediaEngineEvent(meEvent);
			auto core = m_pCB.Resolve<CCPlayer::UWP::Xaml::Controls::MediaEngineNotifyCallback>();
			if (core)
			{
				core->OnMediaEngineEvent(meEvent);
			}
		}

		return S_OK;
	}

	void MediaEngineNotifyCallback(MediaEngineNotifyCallback^ pCB)
	{
		m_pCB = pCB;
	}
};

MediaElementCore::MediaElementCore(Windows::UI::Xaml::Controls::Panel^ panel)
	: m_spDX11Device(nullptr)
	, m_spDX11DeviceContext(nullptr)
	, m_spDXGIOutput(nullptr)
	, m_spDX11SwapChain(nullptr)
	, m_spDXGIManager(nullptr)
	, m_spMediaEngine(nullptr)
	, m_spEngineEx(nullptr)
	, m_bstrURL(nullptr)
	, m_TimerThreadHandle(nullptr)
	, m_fPlaying(FALSE)
	, m_fLoop(FALSE)
	, m_fEOS(FALSE)
	, m_fStopTimer(TRUE)
	, m_d3dFormat(DXGI_FORMAT_B8G8R8A8_UNORM)
	, m_fInitSuccess(FALSE)
	, m_fExitApp(FALSE)
	, m_fUseDX(TRUE)
	, m_errorCode(0)
	, m_normalizedRect({ 0 })
	, m_scaleX(1.0f)
	, m_scaleY(1.0f)
	, m_rawPixelsPerViewPixel(1.0f)
	, m_matrixScale({ 0 })
	, m_aspectRatio(CCPlayer::UWP::Xaml::Controls::AspectRatios::Uniform)
	, m_isSeeking(false)
	, m_lastMarkerTime(-1)
	, m_isFirstFrame(false)
	, m_autoPlay(false)
{
#ifdef _DEBUG
	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
	_CrtSetReportMode(_CRT_ERROR, _CRTDBG_MODE_DEBUG);
#endif
	
	memset(&m_bkgColor, 0, sizeof(MFARGB));
	//m_bkgColor.rgbBlue = 0;
	//m_bkgColor.rgbGreen = 0;
	//m_bkgColor.rgbRed = 0;
	//m_bkgColor.rgbAlpha = 255;

	InitializeCriticalSectionEx(&m_critSec, 0, 0);

	m_markers = ref new TimelineMarkerCollection();
	//스왑체인패널 생성
	_SwapChainPanel = ref new Windows::UI::Xaml::Controls::SwapChainPanel();
	_SwapChainPanel->RenderTransformOrigin = Windows::Foundation::Point(0.5, 0.5);
	_SwapChainPanel->RenderTransform = ref new Windows::UI::Xaml::Media::CompositeTransform();
	//부모에 추가
	Parent = panel;
	Parent->Children->Append(_SwapChainPanel);

	/**/
	_ScreenOffPanel = ref new Grid();
	_ScreenOffPanel->Background = ref new SolidColorBrush(Windows::UI::Colors::Black);
	Parent->Children->Append(_ScreenOffPanel);

	_ScreenOffPanelStoryBoard = ref new Windows::UI::Xaml::Media::Animation::Storyboard();
	TimeSpan ts = { 3000000 };
	auto animation1 = ref new Windows::UI::Xaml::Media::Animation::DoubleAnimation();
	auto animation2 = ref new Windows::UI::Xaml::Media::Animation::ObjectAnimationUsingKeyFrames();
	auto keyframe = ref new Windows::UI::Xaml::Media::Animation::DiscreteObjectKeyFrame();

	animation1->To = 0.0;
	keyframe->Value = Windows::UI::Xaml::Visibility::Collapsed;

	animation1->Duration = DurationHelper::FromTimeSpan(ts);
	animation2->KeyFrames->Append(keyframe);
	keyframe->KeyTime = Windows::UI::Xaml::Media::Animation::KeyTimeHelper::FromTimeSpan(ts);

	Windows::UI::Xaml::Media::Animation::Storyboard::SetTarget(animation1, _ScreenOffPanel);
	Windows::UI::Xaml::Media::Animation::Storyboard::SetTarget(animation2, _ScreenOffPanel);
	Windows::UI::Xaml::Media::Animation::Storyboard::SetTargetProperty(animation1, "Opacity");
	Windows::UI::Xaml::Media::Animation::Storyboard::SetTargetProperty(animation2, "Visibility");

	_ScreenOffPanelStoryBoard->Children->Append(animation1);
	_ScreenOffPanelStoryBoard->Children->Append(animation2);
	/******************************************************/

	//사이즈 조절 처리를 위한 이벤트 등록
	_SwapChainPanel->SizeChanged += ref new Windows::UI::Xaml::SizeChangedEventHandler(this, &MediaElementCore::OnSizeChanged);
	//객체 초기화
	Initialize();
	OutputDebugMessage(L"L:me MediaElementCore 객체가 생성됨......\n");
}

MediaElementCore::~MediaElementCore()
{
	if (Parent != nullptr)
	{
		auto parent = Parent;
		_SwapChainPanel->Dispatcher->RunAsync(CoreDispatcherPriority::Normal,
			ref new DispatchedHandler([parent] {
			for (unsigned int i = 0; i < parent->Children->Size; i++)
			{
				auto swapChainPanel = dynamic_cast<Windows::UI::Xaml::Controls::SwapChainPanel^>(parent->Children->GetAt(i));
				if (swapChainPanel != nullptr)
				{
					parent->Children->RemoveAt(i);
					swapChainPanel = nullptr;
					break;
				}
			}
		}));
	}

	_SwapChainPanel = nullptr;
	Shutdown();
	MFShutdown();
	DeleteCriticalSection(&m_critSec);
	OutputDebugMessage(L"L:me MediaElementCore 객체가 소멸됨......\n");
	
	_CrtDumpMemoryLeaks();
}

//+-------------------------------------------------------------------------
//
//  Function:   CreateDX11Device()
//
//  Synopsis:   creates a default device
//
//--------------------------------------------------------------------------
void MediaElementCore::CreateDX11Device()
{
	D3D_FEATURE_LEVEL FeatureLevel;
	HRESULT hr = S_OK;
		
	//https://social.msdn.microsoft.com/Forums/en-US/2f467b60-a99b-481d-baa1-fe0be124d8bb/imfmediaenginetransfervideoframe-return-efail-on-64-bit-windows-8-pro-with-gts-250-graphic-card?forum=winappswithnativecode
	D3D_FEATURE_LEVEL levels9[] = {
		D3D_FEATURE_LEVEL_9_3,
		D3D_FEATURE_LEVEL_9_2,
		D3D_FEATURE_LEVEL_9_1
	};

	D3D_FEATURE_LEVEL levels11[] = {
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL_9_3,
		D3D_FEATURE_LEVEL_9_2,
		D3D_FEATURE_LEVEL_9_1
	};

	bool useDX11 = false;
	if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
	{
		useDX11 = true;
	}
	else
	{
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
		CCPlayer::ThrowIfFailed(
			CreateDXGIFactory1(IID_PPV_ARGS(&spFactory))
		);

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
				useDX11 = true;
				break;
			case 0x10DE: //Nvidia
				useDX11 = false;
				break;
			default:
				//DX9의 경우 0
				useDX11 = false;
				break;
			}
			//첫번째 드라이버 찾으면 종료
			break;
		}
		/**********************************************************/
	}

	D3D_FEATURE_LEVEL* pFeatureLevels = useDX11 ? levels11 : levels9;
	UINT featureLevels = useDX11 ? ARRAYSIZE(levels11) : ARRAYSIZE(levels9);

	if (m_fUseDX)
	{
		hr = D3D11CreateDevice(
			nullptr,
			D3D_DRIVER_TYPE_HARDWARE,
			nullptr,
			D3D11_CREATE_DEVICE_VIDEO_SUPPORT | D3D11_CREATE_DEVICE_BGRA_SUPPORT,
			pFeatureLevels,
			featureLevels,
			D3D11_SDK_VERSION,
			&m_spDX11Device,
			&FeatureLevel,
			&m_spDX11DeviceContext
			);
	}

	//Failed to create DX11 Device (using VM?), create device using WARP
	if (FAILED(hr))
	{
		m_fUseDX = FALSE;
	}

	if (!m_fUseDX)
	{
		CCPlayer::ThrowIfFailed(D3D11CreateDevice(
			nullptr,
			D3D_DRIVER_TYPE_WARP,
			nullptr,
			D3D11_CREATE_DEVICE_BGRA_SUPPORT,
			pFeatureLevels,
			featureLevels,
			D3D11_SDK_VERSION,
			&m_spDX11Device,
			&FeatureLevel,
			&m_spDX11DeviceContext
			));
	}

	if (m_fUseDX)
	{
		ComPtr<ID3D10Multithread> spMultithread;
		CCPlayer::ThrowIfFailed(
			m_spDX11Device.Get()->QueryInterface(IID_PPV_ARGS(&spMultithread))
			);

		spMultithread->SetMultithreadProtected(TRUE);
		//// Multithreaded protection also needs to be enabled to allow the video player to safely access the device
		//https://social.msdn.microsoft.com/Forums/en-US/2f467b60-a99b-481d-baa1-fe0be124d8bb/imfmediaenginetransfervideoframe-return-efail-on-64-bit-windows-8-pro-with-gts-250-graphic-card?forum=winappswithnativecode
		//spMultithread->Release(); //<= 안되네..

	}

	return;
}

DXGI_MATRIX_3X2_F back;

//+-----------------------------------------------------------------------------
//
//  Function:   CreateBackBuffers
//
//  Synopsis:   Creates the D3D back buffers
//
//------------------------------------------------------------------------------
void MediaElementCore::CreateBackBuffers()
{
	EnterCriticalSection(&m_critSec);

	// make sure everything is released first;    
	if (m_spDX11Device)
	{
		// Acquire the DXGIdevice lock 
		CAutoDXGILock DXGILock(m_spDXGIManager);
	
		ComPtr<ID3D11Device> spDevice;
		CCPlayer::ThrowIfFailed(
			DXGILock.LockDevice(/*out*/spDevice)
			);
	
		// swap chain does not exist - so create it
		if (m_spDX11SwapChain == nullptr)
		{
			DXGI_SWAP_CHAIN_DESC1 swapChainDesc = { 0 };

			// Don't use Multi-sampling
			swapChainDesc.SampleDesc.Count = 1;
			swapChainDesc.SampleDesc.Quality = 0;

			swapChainDesc.BufferUsage = DXGI_USAGE_BACK_BUFFER | DXGI_USAGE_RENDER_TARGET_OUTPUT;
			swapChainDesc.Scaling = DXGI_SCALING_STRETCH;
			swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;

			// Use more than 1 buffer to enable Flip effect.
			swapChainDesc.BufferCount = 4;

			// Most common swapchain format is DXGI_FORMAT_R8G8B8A8_UNORM
			swapChainDesc.Format = m_d3dFormat;
			swapChainDesc.Width = m_rcTarget.right;
			swapChainDesc.Height = m_rcTarget.bottom;

			// long QI chain to get DXGIFactory from the device
			ComPtr<IDXGIDevice2> spDXGIDevice;
			CCPlayer::ThrowIfFailed(
				spDevice.Get()->QueryInterface(IID_PPV_ARGS(&spDXGIDevice))
				);
	
			// Ensure that DXGI does not queue more than one frame at a time. This both reduces 
			// latency and ensures that the application will only render after each VSync, minimizing 
			// power consumption.
			CCPlayer::ThrowIfFailed(
				spDXGIDevice->SetMaximumFrameLatency(1)
				);
	
			ComPtr<IDXGIAdapter> spDXGIAdapter;
			CCPlayer::ThrowIfFailed(
				spDXGIDevice->GetParent(IID_PPV_ARGS(&spDXGIAdapter))
				);
	
			ComPtr<IDXGIFactory2> spDXGIFactory;
			CCPlayer::ThrowIfFailed(
				spDXGIAdapter->GetParent(IID_PPV_ARGS(&spDXGIFactory))
				);
			
			CCPlayer::ThrowIfFailed(
				spDXGIFactory.Get()->CreateSwapChainForComposition(
					spDevice.Get(),
					&swapChainDesc,
					nullptr,
					&m_spDX11SwapChain
					)
				);
			
			// 스왑 체인을 SwapChainPanel과 연결
			// UI 변경 내용은 UI 스레드에 다시 디스패치해야 함
			_SwapChainPanel->Dispatcher->RunAsync(CoreDispatcherPriority::High,
				ref new DispatchedHandler([=]()
			{
				// 화면 스케일 맞춤
				m_matrixScale._11 = 1.0f / m_rawPixelsPerViewPixel;
				m_matrixScale._22 = 1.0f / m_rawPixelsPerViewPixel;
				m_matrixScale._31 = 0.0f;
				m_matrixScale._32 = 0.0f;

				ComPtr<IDXGISwapChain2> spSwapChain2;
				CCPlayer::ThrowIfFailed(m_spDX11SwapChain.As<IDXGISwapChain2>(&spSwapChain2));
				CCPlayer::ThrowIfFailed(spSwapChain2->SetMatrixTransform(&m_matrixScale));

				// SwapChainPanel에 대한 기본 인터페이스 가져오기
				ComPtr<ISwapChainPanelNative> panelNative;
				CCPlayer::ThrowIfFailed(reinterpret_cast<IUnknown*>(_SwapChainPanel)->QueryInterface(IID_PPV_ARGS(&panelNative)));
				CCPlayer::ThrowIfFailed(panelNative->SetSwapChain(m_spDX11SwapChain.Get()));
				back = m_matrixScale;
			}, CallbackContext::Any));
		}
		else
		{
			// otherwise just resize it
			CCPlayer::ThrowIfFailed(m_spDX11SwapChain->ResizeBuffers(
				4,
				m_rcTarget.right,
				m_rcTarget.bottom,
				m_d3dFormat,
				0
				));

			if (this->NaturalVideoWidth > 0 && this->NaturalVideoHeight > 0)
			{
				_SwapChainPanel->Dispatcher->RunAsync(CoreDispatcherPriority::High,
					ref new DispatchedHandler([=]()
				{
					if (CheckStretchMode(m_aspectRatio))
					{
						m_matrixScale._11 = (float)floor(1.0f / m_rawPixelsPerViewPixel * 10000.0) / 10000.0f * m_scaleX;
						m_matrixScale._22 = (float)floor(1.0f / m_rawPixelsPerViewPixel * 10000.0) / 10000.0f * m_scaleY;
						m_matrixScale._31 = 0.0f;
						m_matrixScale._32 = 0.0f;
					}

					if (m_matrixScale._11 != 1.0f / m_rawPixelsPerViewPixel)
					{
						m_matrixScale._31 = (this->m_rcTarget.right - this->m_rcTarget.right * m_scaleX) / 2 / m_rawPixelsPerViewPixel;
					}

					if (m_matrixScale._22 != 1.0f / m_rawPixelsPerViewPixel)
					{
						m_matrixScale._32 = (this->m_rcTarget.bottom - this->m_rcTarget.bottom * m_scaleY) / 2 / m_rawPixelsPerViewPixel;
					}

					ComPtr<IDXGISwapChain2> spSwapChain2;
					CCPlayer::ThrowIfFailed(m_spDX11SwapChain.As<IDXGISwapChain2>(&spSwapChain2));
					CCPlayer::ThrowIfFailed(spSwapChain2->SetMatrixTransform(&m_matrixScale));
					back = m_matrixScale;
					
				}, CallbackContext::Any));
			}
		}
	}

	LeaveCriticalSection(&m_critSec);

	return;
}

// Create a new instance of the Media Engine.
void MediaElementCore::Initialize()
{
	ComPtr<IMFMediaEngineClassFactory> spFactory;
	ComPtr<IMFAttributes> spAttributes;
	ComPtr<MediaEngineNotify> spNotify;

	HRESULT hr = S_OK;

	CCPlayer::ThrowIfFailed(MFStartup(MF_VERSION));

	EnterCriticalSection(&m_critSec);

	try
	{
		// Create DX11 device.    
		CreateDX11Device();

		UINT resetToken;
		CCPlayer::ThrowIfFailed(
			MFCreateDXGIDeviceManager(&resetToken, &m_spDXGIManager)
			);

		CCPlayer::ThrowIfFailed(
			m_spDXGIManager->ResetDevice(m_spDX11Device.Get(), resetToken)
			);

		// Create our event callback object.
		spNotify = new MediaEngineNotify();
		if (spNotify == nullptr)
		{
			CCPlayer::ThrowIfFailed(E_OUTOFMEMORY);
		}
		//this에 대한 레퍼런스 카운트 증가됨...
		spNotify->MediaEngineNotifyCallback(this);

		// Create the class factory for the Media Engine.
		CCPlayer::ThrowIfFailed(
			CoCreateInstance(CLSID_MFMediaEngineClassFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&spFactory))
			);

		// Set configuration attribiutes.
		CCPlayer::ThrowIfFailed(
			MFCreateAttributes(&spAttributes, 1)
			);

		CCPlayer::ThrowIfFailed(
			spAttributes->SetUnknown(MF_MEDIA_ENGINE_DXGI_MANAGER, (IUnknown*)m_spDXGIManager.Get())
			);
		
		CCPlayer::ThrowIfFailed(
			spAttributes->SetUnknown(MF_MEDIA_ENGINE_CALLBACK, (IUnknown*)spNotify.Get())
			);

		CCPlayer::ThrowIfFailed(
			spAttributes->SetUINT32(MF_MEDIA_ENGINE_VIDEO_OUTPUT_FORMAT, m_d3dFormat)
			);

		// Create the Media Engine.
		const DWORD flags = MF_MEDIA_ENGINE_WAITFORSTABLE_STATE;
		CCPlayer::ThrowIfFailed(
			spFactory->CreateInstance(flags, spAttributes.Get(), &m_spMediaEngine)
			);
		
		CCPlayer::ThrowIfFailed(
			m_spMediaEngine.Get()->QueryInterface(__uuidof(IMFMediaEngine), (void**)&m_spEngineEx)
			);
		
		// Create/Update swap chain
		UpdateForWindowSizeChange();

		m_fInitSuccess = TRUE;

	}
	catch (Platform::Exception^)
	{
		Windows::UI::Popups::MessageDialog^ msgDlg = ref new Windows::UI::Popups::MessageDialog("Failed to initialize DirectX device.");
		task<Windows::UI::Popups::IUICommand^> coreWindowDialogTask(msgDlg->ShowAsync());

		coreWindowDialogTask.then([this](Windows::UI::Popups::IUICommand^ uiCommand) {
			m_fExitApp = TRUE;
		});
	}

	LeaveCriticalSection(&m_critSec);

	return;
}

// Shut down the player and release all interface pointers.
void MediaElementCore::Shutdown()
{
	EnterCriticalSection(&m_critSec);

	StopTimer();

	if (m_spMediaEngine)
	{
		m_spMediaEngine->Shutdown();
	}

	if (nullptr != m_bstrURL)
	{
		::CoTaskMemFree(m_bstrURL);
	}

	LeaveCriticalSection(&m_critSec);

	return;
}

Uri^ MediaElementCore::Source::get()
{
	return m_source;
}

void MediaElementCore::Source::set(Uri^ value)
{
	//if (m_source != value)
	//{
		if (m_fInitSuccess == FALSE)
			return;

		if (IsPlaying())
			Pause();

		if (m_fEOS)
			StopTimer();

		try
		{
			if (!value)
			{
				if (!m_spMediaEngine->HasVideo())
				{
					m_fExitApp = TRUE;
				}
				return;
			}

			try
			{
				m_source = value;
				SetURL(value->AbsoluteUri);

				TimeSpan ts;
				ts.Duration = 0;
				this->Position = ts;
				//자막 초기화
				this->Markers->Clear();
				m_spEngineEx->CancelTimelineMarkerTimer();
				this->Volume = 1.0f;
				this->Balance = 0;
				/*this->PlaybackRate = 1;
				this->DefaultPlaybackRate = 1;*/
				this->IsLooping = false;
				this->IsMuted = false;
				this->RealTimePlayback = false;
				CCPlayer::ThrowIfFailed(m_spEngineEx->SetSource(m_bstrURL));
			}
			catch (Platform::Exception^)
			{
				CCPlayer::ThrowIfFailed(E_UNEXPECTED);
			}

		}
		catch (Platform::Exception^)
		{
			if (!m_spMediaEngine->HasVideo())
			{
				m_fExitApp = TRUE;
			}
		}
	//}
}

void MediaElementCore::SetSource(Windows::Storage::Streams::IRandomAccessStream^ stream, Platform::String^ mimeType)
{
	//mimeType = L"http://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_1080p_h264.mov";
	//mimeType = L"http://qthttp.apple.com.edgesuite.net/1010qwoeiuryfg/sl.m3u8";

	if (m_fInitSuccess == FALSE)
		return;
	
	if (IsPlaying())
		Pause();
	
	if (m_fEOS)
		StopTimer();

	try
	{
		/*if (!stream)
		{
			if (!m_spMediaEngine->HasVideo())
			{
				m_fExitApp = TRUE;
			}
			return;
		}*/

		try
		{
			SetURL(mimeType);

			TimeSpan ts;
			ts.Duration = 0;
			this->Position = ts;
			//자막 초기화
			this->Markers->Clear();
			m_spEngineEx->CancelTimelineMarkerTimer();
			this->Volume = 1.0f;
			this->Balance = 0;
			/*this->PlaybackRate = 1;
			this->DefaultPlaybackRate = 1;*/
			this->IsLooping = false;
			this->IsMuted = false;
			this->RealTimePlayback = false;
			this->SetBytestream(stream);
		}
		catch (Platform::Exception^)
		{
			CCPlayer::ThrowIfFailed(E_UNEXPECTED);
		}

	}
	catch (Platform::Exception^)
	{
		if (!m_spMediaEngine->HasVideo())
		{
			m_fExitApp = TRUE;
		}
	}

	return;
}

// Set a URL
void MediaElementCore::SetURL(Platform::String^ szURL)
{
	if (nullptr != m_bstrURL)
	{
		::CoTaskMemFree(m_bstrURL);
		m_bstrURL = nullptr;
	}

	size_t cchAllocationSize = 1 + ::wcslen(szURL->Data());
	m_bstrURL = (LPWSTR)::CoTaskMemAlloc(sizeof(WCHAR)*(cchAllocationSize));

	if (m_bstrURL == 0)
	{
		CCPlayer::ThrowIfFailed(E_OUTOFMEMORY);
	}

	StringCchCopyW(m_bstrURL, cchAllocationSize, szURL->Data());

	return;
}

// Set Bytestream
void MediaElementCore::SetBytestream(IRandomAccessStream^ streamHandle)
{
	HRESULT hr = S_OK;
	ComPtr<IMFByteStream> spMFByteStream = nullptr;

	CCPlayer::ThrowIfFailed(MFCreateMFByteStreamOnStreamEx((IUnknown*)streamHandle, &spMFByteStream));
	CCPlayer::ThrowIfFailed(m_spEngineEx->SetSourceFromByteStream(spMFByteStream.Get(), m_bstrURL));
	
	return;
}

void MediaElementCore::NotifyStateChanged()
{
	auto uiDispatcher = _SwapChainPanel->Dispatcher;
	auto priority = CoreDispatcherPriority::Normal;
	uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] { CurrentStateChanged(_SwapChainPanel, ref new RoutedEventArgs()); }));
}
 
void MediaElementCore::OnMediaEngineEvent(DWORD meEvent)
{
	auto uiDispatcher = _SwapChainPanel->Dispatcher;
	auto priority = CoreDispatcherPriority::High;

	switch (meEvent)
	{
	case MF_MEDIA_ENGINE_EVENT_LOADSTART:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_LOADSTART\n");
		//AutoPlay상태를 무조건 비활성화 시킴 (재생 준비가 다 된다음 활성화 여부에 따라 다시 시작)
		m_spEngineEx->SetAutoPlay(FALSE);
		//미디어 상태가 변경되었음을 통지
		m_currentState = MediaElementState::Opening;
		NotifyStateChanged();
		break;
	case MF_MEDIA_ENGINE_EVENT_LOADEDMETADATA:
	{
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_LOADEDMETADATA\n");
		//오디오 스트림 검색
		m_audioStreamIdexes.clear();
		if (m_spEngineEx)
		{
			if (m_spEngineEx->HasAudio())
			{
				DWORD streamNumber = 0;
				m_spEngineEx->GetNumberOfStreams(&streamNumber);

				for (DWORD index = 0; index < streamNumber; index++)
				{
					PROPVARIANT pvValue;
					m_spEngineEx->GetStreamAttribute(index, MF_MT_MAJOR_TYPE, &pvValue);

					if (pvValue.vt == VT_CLSID)
					{
						if (strcmp("auds", pvValue.pszVal) == 0)
						{
							m_audioStreamIdexes.push_back(index);
						}
						//+CLSID	0x0a6efb38 {CLSID_WDM Streaming Standard Data Type Handler}	_GUID *
						//vt	72	unsigned short
						//+pszVal	0x0a6efb38 "auds"	char *
						//+pcVal	0x0a6efb38 "auds"	char *
						//+pbVal	0x0a6efb38 "auds"	unsigned char *
					}

					PropVariantClear(&pvValue);
				}
			}
		}
		//OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_LOADEDMETADATA");
		m_fEOS = FALSE;
	}
	break;
	case MF_MEDIA_ENGINE_EVENT_LOADEDDATA:
		//MF_MEDIA_ENGINE_EVENT_LOADEDMETADATA 다음 호출
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_LOADEDDATA\n");
		break;
	case MF_MEDIA_ENGINE_EVENT_CANPLAY:
		//MF_MEDIA_ENGINE_EVENT_LOADEDDATA 다음 호출
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_CANPLAY\n");
		//미디어가 재생할 준비가 되었음을 알린다. 
		if (m_spEngineEx)
		{
			m_spEngineEx->EnableTimeUpdateTimer(TRUE);
		}		
		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=]
		{
			MediaOpened(_SwapChainPanel, ref new RoutedEventArgs());
		}));
		break;
	case MF_MEDIA_ENGINE_EVENT_CANPLAYTHROUGH:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_CANPLAYTHROUGH\n");
		break;
	case MF_MEDIA_ENGINE_EVENT_PLAY:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_PLAY\n");
		m_isSeeking = false;
		m_fPlaying = TRUE;
		break;
	case MF_MEDIA_ENGINE_EVENT_FIRSTFRAMEREADY:
		//MF_MEDIA_ENGINE_EVENT_CANPLAY 다음 호출
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_FIRSTFRAMEREADY\n");
		m_isFirstFrame = true;

		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=]
		{
			auto autoPlay = AutoPlay;
			if (m_spEngineEx && AutoPlay)
			{
				HRESULT hr = m_spEngineEx->SetAutoPlay(AutoPlay ? TRUE : FALSE);
				if (FAILED(hr))
				{
					AutoPlay = false;
				}
				else
				{
					//타이머를 시작하고 오디오/비디오 프레임을 전송하기 시작
					Play();
				}
			}
		}));
		break;
	case MF_MEDIA_ENGINE_EVENT_PLAYING:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_PLAYING\n");
		m_isSeeking = false;
		m_currentState = MediaElementState::Playing;
		NotifyStateChanged();
		break;
	case MF_MEDIA_ENGINE_EVENT_PAUSE:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_PAUSE\n");
		m_isSeeking = false;
		m_fPlaying = FALSE;
		m_currentState = MediaElementState::Paused;
		NotifyStateChanged();
		break;
	case MF_MEDIA_ENGINE_EVENT_BUFFERINGSTARTED:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_BUFFERINGSTARTED\n");
		m_currentState = MediaElementState::Buffering;
		NotifyStateChanged();
		break;
	case MF_MEDIA_ENGINE_EVENT_BUFFERINGENDED:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_BUFFERINGENDED\n");
		if (m_spEngineEx)
		{
			m_currentState = m_spEngineEx->IsPaused() ? MediaElementState::Paused : MediaElementState::Playing;
			NotifyStateChanged();
		}
		break;
	case MF_MEDIA_ENGINE_EVENT_SEEKING:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_SEEKING\n");
		m_isSeeking = true;
		m_lastMarkerTime = -1;
		m_currentState = MediaElementState::Buffering;
		NotifyStateChanged();
		break;
	case MF_MEDIA_ENGINE_EVENT_SEEKED:
	{
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_SEEKED\n");
		if (m_fStopTimer) return;
		if (m_spEngineEx)
		{
			//상태 통지
			m_currentState = m_spEngineEx->IsPaused() ? MediaElementState::Paused :  MediaElementState::Playing;
			//탐색 완료 처리
			m_spEngineEx->CancelTimelineMarkerTimer();
			//새로운 자막 위치 등록
			m_isSeeking = false;

			uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] {
				//탐색 완료 통지
				SeekCompleted(_SwapChainPanel, ref new RoutedEventArgs());
				//새로운 자막 위치 등록
				SetCurrentTimeMarker(Position.Duration);
			}));
			NotifyStateChanged();
		}
		break;
	}
	case MF_MEDIA_ENGINE_EVENT_TIMELINE_MARKER:
	{
		//OutputDebugMessage(L"MF_MEDIA_ENGINE_EVENT_TIMELINE_MARKER");
		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] {
			if (m_fStopTimer) return;
			if (m_marker != nullptr && m_markers->Size > 0)
			{
				auto currentTime = Position.Duration;
				auto markerTime = m_marker->Time.Duration;
				
				//마커 중복 발생 방지
				if (m_lastMarkerTime == markerTime)
				{
					//OutputDebugMessage(L"중복 마커 제거 : %I64d\r\n", m_lastMarkerTime);
					return;
				}
				
				//마커가 너무 일직 발생했다 (1초이상 빠르다)
				if (currentTime - markerTime > 10000000L)
				{
					//마커 재등록
					double newMarkerTime = (double)markerTime / 10000000L;
					if (m_spEngineEx)
					{
						m_spEngineEx->SetTimelineMarkerTimer(newMarkerTime);
					}
					return;
				}

				m_lastMarkerTime = markerTime;

				auto args = ref new TimelineMarkerRoutedEventArgs();
				auto mk = ref new TimelineMarker();
				mk->Text = ref new String(m_marker->Text->Data());
				mk->Type = ref new String(m_marker->Type->Data());
				mk->Time = m_marker->Time;
				args->Marker = mk;
				//이벤트 라우팅
				MarkerReached(_SwapChainPanel, args);
				//다음 마커 등록
				SetCurrentTimeMarker(markerTime);
			}
		}));
	}
	break;
	case MF_MEDIA_ENGINE_EVENT_TIMEUPDATE:
		if (m_fStopTimer) return;
		//자막 검사
		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] { SetCurrentTimeMarker(Position.Duration); }));
		break;
	case MF_MEDIA_ENGINE_EVENT_ENDED:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_ENDED\n");
		if (m_spMediaEngine && m_spMediaEngine->HasVideo())
		{
			StopTimer();
		}
		m_fEOS = TRUE;
		m_currentState = MediaElementState::Stopped;
		NotifyStateChanged();
		//미디어가 재생이 종료가 되었음을 알린다. 
		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] { MediaEnded(_SwapChainPanel, ref new RoutedEventArgs()); }));
		break;
	case MF_MEDIA_ENGINE_EVENT_ERROR:
		OutputDebugString(L"MF_MEDIA_ENGINE_EVENT_ERROR\n");
		m_currentState = MediaElementState::Closed;
		m_isSeeking = false;

		if (m_spEngineEx)
		{
			IMFMediaError* pError = nullptr;
			m_spEngineEx->GetError(&pError);

			HRESULT ehr = S_OK;
			if (pError != nullptr)
			{
				m_errorCode = pError->GetErrorCode();
				ehr = pError->GetExtendedErrorCode();
				pError->Release();
			}
		}
		//미디어재생이 실패했음을 알린다. 
		uiDispatcher->RunAsync(priority, ref new DispatchedHandler([=] { 
			MediaFailed(_SwapChainPanel, ref new RoutedEventArgs());

			//이전 영상 표시를 없애기 위해 줄였던 화면 크기를 복원
			_ScreenOffPanel->Opacity = 1;
			_ScreenOffPanel->Visibility = Windows::UI::Xaml::Visibility::Visible;
		}));
		break;
	default:
		//__debugbreak();
		break;
	}
	return;
}

// 다음 마커를 등록한다. 
// 매개변수는 100나노초 단위
void MediaElementCore::SetCurrentTimeMarker(long long time)
{
	if (m_isSeeking || m_fStopTimer || !m_spEngineEx)
		return;

	//자막 검사
	if (m_markers->Size > 0)
	{
		double reservedMarkerTime = 0;
		m_spEngineEx->GetTimelineMarkerTimer(&reservedMarkerTime);	//단위 : 초
		bool isReserved = !std::isnan(reservedMarkerTime);

		auto it = std::find_if(begin(m_markers.Get()), end(m_markers.Get()), [=](TimelineMarker^ tlmarker)
		{
			LONGLONG currDuration = tlmarker->Time.Duration;
			LONGLONG rsrvDuration = (long long)(reservedMarkerTime * 10000000L);

			//등록된 마커가 없는 경우 새로 등록 : (최초 또는 시크 직후 등)
			if (!isReserved)
			{
				//마커는 미래에 표시될 것을 등록하는 것이므로 현재 시간보다 큰것을 선택		
				return time < currDuration;
			}
			//요청된 시간이 이미 등록된 마커의 시간을 지난 경우, 요청된 시간보다 큰 마커를 선택한다.
			return rsrvDuration < time && time < currDuration;
		});

		if (it != end(Markers))
		{
			//조회한 인덱스를 통해 마커 추출
			TimelineMarker^ newTimeMarker = (TimelineMarker^)*it;
			double newMarkerTime = (double)newTimeMarker->Time.Duration / 10000000L;
			
			//직전에 다시 체크
			m_spEngineEx->GetTimelineMarkerTimer(&reservedMarkerTime);
			isReserved = !std::isnan(reservedMarkerTime);
			//마커가 등록되어 있지 않은 경우 또는 등록할 마커가 등록되어 있는 마커와 다를 경우만 새롭게 등록
			if (!isReserved || newMarkerTime != reservedMarkerTime)
			{
				//등록된 마커가 있으면 취소
				if (isReserved)
				{
					m_spEngineEx->CancelTimelineMarkerTimer();
				}
				//마커 등록
				m_spEngineEx->SetTimelineMarkerTimer(newMarkerTime);
				//OutputDebugMessage(L"current marker time => %f\t next time marker => time : %f cc : %s \n", reservedMarkerTime, newMarkerTime, newTimeMarker->Text->Data());
				m_marker = newTimeMarker;
			}
		}
	}
	else
	{
		m_spEngineEx->CancelTimelineMarkerTimer();
	}
}

// Start playback.
void MediaElementCore::Play()
{
	if (m_spMediaEngine)
	{
		if (m_spMediaEngine->HasVideo() && m_fStopTimer)
		{
			// Start the Timer thread
			StartTimer();
		}

		if (m_fEOS)
		{
			TimeSpan ts = {0};
			Position = ts;
			//m_fPlaying = TRUE;
		}
		/*else
		{*/
			CCPlayer::ThrowIfFailed(
				m_spMediaEngine->Play()
				);
		//}
		m_fPlaying = TRUE;
		m_fEOS = FALSE;
	}
	return;
}

void MediaElementCore::Stop()
{
	StopTimer();

	if (m_spEngineEx)
	{
		m_spEngineEx->Pause();
		m_spEngineEx->CancelTimelineMarkerTimer();
		m_spEngineEx->EnableTimeUpdateTimer(FALSE);
	}

	if (m_markers != nullptr)
	{
		//마커 초기화
		m_markers->Clear();
		m_marker = nullptr;
	}

	//영상 초기화
	//Position = TimeSpan();
	
	//트림
	this->DXGIDeviceTrim();
	UpdateForWindowSizeChange();

	//종료 이벤트 발생 및 시간 / 엔진 초기화
	m_currentState = MediaElementState::Stopped;
	NotifyStateChanged();

	//이전 영상이 다음번 재생 시작시 표시되지 않도록 화면 스케일을 0로 설정하여 안보이도록 줄임
	_ScreenOffPanel->Opacity = 1;
	_ScreenOffPanel->Visibility = Windows::UI::Xaml::Visibility::Visible;
}

// Pause playback.
void MediaElementCore::Pause()
{
	if (m_spMediaEngine)
	{
		CCPlayer::ThrowIfFailed(
			m_spMediaEngine->Pause()
			);
	}
	return;
}

// Step forward one frame.
void MediaElementCore::FrameStep()
{
	if (m_spEngineEx)
	{
		CCPlayer::ThrowIfFailed(m_spEngineEx->FrameStep(TRUE));
	}
	return;
}

// Is the player in the middle of a seek operation?
BOOL MediaElementCore::IsSeeking()
{
	if (m_spMediaEngine)
	{
		return m_spMediaEngine->IsSeeking();
	}
	else
	{
		return FALSE;
	}
}

void MediaElementCore::EnableVideoEffect(BOOL enable)
{
	HRESULT hr = S_OK;

	if (m_spEngineEx)
	{
		CCPlayer::ThrowIfFailed(m_spEngineEx->RemoveAllEffects());
		if (enable)
		{
			ComPtr<IMFActivate> spActivate;
			LPCWSTR szActivatableClassId = WindowsGetStringRawBuffer((HSTRING)Windows::Media::VideoEffects::VideoStabilization->Data(), nullptr);

			CCPlayer::ThrowIfFailed(MFCreateMediaExtensionActivate(szActivatableClassId, nullptr, IID_PPV_ARGS(&spActivate)));

			CCPlayer::ThrowIfFailed(m_spEngineEx->InsertVideoEffect(spActivate.Get(), FALSE));
		}
	}

	return;
}

void MediaElementCore::UpdateForWindowSizeChange(bool force)
{
	double width = 1.0;
	double height = 1.0;

	if (_SwapChainPanel->ActualWidth > 0)
	{
		width = _SwapChainPanel->ActualWidth;
	}

	if (_SwapChainPanel->ActualHeight > 0)
	{
		height = _SwapChainPanel->ActualHeight;
	}

	if ((width != m_rcTarget.right ||
		height != m_rcTarget.bottom) ||
		m_spDX11SwapChain == nullptr || 
		force)
	{
		// Get the bounding rectangle of the window. 
		auto displayInfo = Windows::Graphics::Display::DisplayInformation::GetForCurrentView();
		m_rawPixelsPerViewPixel = (float)displayInfo->RawPixelsPerViewPixel;

		m_rcTarget.left = 0;
		m_rcTarget.top = 0;
		//m_rcTarget.right = (LONG)((width + 1) * displayInfo->RawPixelsPerViewPixel); //소숫점 이하 잘려나간 부분에 대한 보상 + 1
		//m_rcTarget.bottom = (LONG)((height + 1) * displayInfo->RawPixelsPerViewPixel);

		if (m_displayRotation == CCPlayer::UWP::Xaml::Controls::DisplayRotations::Clockwise90
 			|| m_displayRotation == CCPlayer::UWP::Xaml::Controls::DisplayRotations::Clockwise270)
		{
			m_rcTarget.right = (LONG)ceil(height * m_rawPixelsPerViewPixel);
			m_rcTarget.bottom = (LONG)ceil(width * m_rawPixelsPerViewPixel);
		}
		else
		{
			m_rcTarget.right = (LONG)ceil(width * m_rawPixelsPerViewPixel);
			m_rcTarget.bottom = (LONG)ceil(height * m_rawPixelsPerViewPixel);
		}
		
		if (m_spEngineEx)
		{
			CreateBackBuffers();

			if (m_fStopTimer == FALSE && m_currentState == MediaElementState::Paused)
			{
				EnterCriticalSection(&m_critSec);
				//일시정지된 경우 이전 프레임을 다시 텍스쳐를 통해 화면을 그린다.
				DisplayFrame();
				//HRESULT hr = m_spEngineEx->UpdateVideoStream(&m_normalizedRect, &m_rcTarget, &m_bkgColor);
				LeaveCriticalSection(&m_critSec);
			}
		}
	}
	return;
}

// Window Event Handlers
void MediaElementCore::UpdateForWindowSizeChange()
{
	UpdateForWindowSizeChange(true);
}

//Timer related

//+-----------------------------------------------------------------------------
//
//  Function:   StartTimer
//
//  Synopsis:   Our timer is based on the displays VBlank interval
//
//------------------------------------------------------------------------------
void MediaElementCore::StartTimer()
{
	if (!m_spDXGIOutput)
	{
		ComPtr<IDXGIFactory1> spFactory;
		CCPlayer::ThrowIfFailed(
			CreateDXGIFactory1(IID_PPV_ARGS(&spFactory))
		);

		ComPtr<IDXGIAdapter> spAdapter;
		CCPlayer::ThrowIfFailed(
			spFactory->EnumAdapters(0, &spAdapter)
		);

		ComPtr<IDXGIOutput> spOutput;
		CCPlayer::ThrowIfFailed(
			spAdapter->EnumOutputs(0, &m_spDXGIOutput)
		);
	}

	m_fStopTimer = FALSE;

	auto vidPlayer = this;
	task<void> workItem(
		ThreadPool::RunAsync(ref new WorkItemHandler([=](IAsyncAction^ /*sender*/) {
			vidPlayer->RealVSyncTimer();
		}),
		WorkItemPriority::High));

	return;
}

//+-----------------------------------------------------------------------------
//
//  Function:   StopTimer
//
//  Synopsis:   Stops the Timer and releases all its resources
//
//------------------------------------------------------------------------------
void MediaElementCore::StopTimer()
{
	m_fStopTimer = TRUE;
	m_fPlaying = FALSE;

	return;
}

//+-----------------------------------------------------------------------------
//
//  Function:   realVSyncTimer
//
//  Synopsis:   A real VSyncTimer - a timer that fires at approx 60 Hz 
//              synchronized with the display's real VBlank interrupt.
//
//------------------------------------------------------------------------------
DWORD MediaElementCore::RealVSyncTimer()
{
	for (;; )
	{
		if (m_fStopTimer)
		{
			break;
		}
		
		if (SUCCEEDED(m_spDXGIOutput->WaitForVBlank()))
		{
			OnTimer();
		}
		else break;
	}

	return 0;
}

//+-----------------------------------------------------------------------------
//
//  Function:   OnTimer
//
//  Synopsis:   Called at 60Hz - we simply call the media engine and draw
//              a new frame to the screen if told to do so.
//
//------------------------------------------------------------------------------
void MediaElementCore::OnTimer()
{
	if (m_fStopTimer) return; //새로추가 테스트용
	EnterCriticalSection(&m_critSec);

	if (m_spMediaEngine)
	{
		LONGLONG pts;
		if (m_spMediaEngine->OnVideoStreamTick(&pts) == S_OK)
		{
			DisplayFrame();
		}
	}

	LeaveCriticalSection(&m_critSec);

	return;
}

//+-----------------------------------------------------------------------------
//
//  Function:   DisplayFrame
//
//  Synopsis:   프레임을 텍스쳐에 복사한후, 스왑체인에 렌더링한다.
//
//------------------------------------------------------------------------------
void MediaElementCore::DisplayFrame()
{
	if (m_spMediaEngine)
	{
		// new frame available at the media engine so get it 
		ComPtr<ID3D11Texture2D> spTextureDst;

		CCPlayer::ThrowIfFailed(
			m_spDX11SwapChain.Get()->GetBuffer(0, IID_PPV_ARGS(&spTextureDst))
			);
		//m_spEngineEx->EnableHorizontalMirrorMode(TRUE);
		HRESULT hr = m_spMediaEngine->TransferVideoFrame(spTextureDst.Get(), &m_normalizedRect, &m_rcTarget, &m_bkgColor);
		
		if (SUCCEEDED(hr))
		{
			// and the present it to the screen
			m_spDX11SwapChain->Present(1, 0);

			if (m_isFirstFrame)
			{
				m_isFirstFrame = false;
				_SwapChainPanel->Dispatcher->RunAsync(CoreDispatcherPriority::Normal, ref new DispatchedHandler([=]
				{
					//이전 영상 표시를 없애기 위해 줄였던 화면 크기를 복원
					_ScreenOffPanelStoryBoard->Stop();
					_ScreenOffPanelStoryBoard->Begin();
					
				}));
			}
		}
		else
		{
			//NVidia 오류..
			_SwapChainPanel->Dispatcher->RunAsync(CoreDispatcherPriority::Normal, ref new DispatchedHandler([=] {
				CreateBackBuffers();
			}));
		}
	}
}

//+-----------------------------------------------------------------------------
//
//  Function:   DXGIDeviceTrim
//
//  Synopsis:   Calls IDXGIDevice3::Trim() (requirement when app is suspended)
//
//------------------------------------------------------------------------------
HRESULT MediaElementCore::DXGIDeviceTrim()
{
	HRESULT hr = S_OK;
	if (m_fUseDX && m_spDX11Device != nullptr)
	{
		IDXGIDevice3 *pDXGIDevice;
		hr = m_spDX11Device.Get()->QueryInterface(__uuidof(IDXGIDevice3), (void **)&pDXGIDevice);
		if (hr == S_OK)
		{
			pDXGIDevice->Trim();
		}
	}

	return hr;
}

//+-----------------------------------------------------------------------------
//
//  Function:   ExitApp
//
//  Synopsis:   Checks if there has been an error and indicates if the app
//				should exit.
//
//------------------------------------------------------------------------------
BOOL MediaElementCore::ExitApp()
{
	return m_fExitApp;
}

//+-----------------------------------------------------------------------------
//
//  Function:   Loop
//
//  Synopsis:   반복 재생여부를 설정한다.
//
//------------------------------------------------------------------------------
void MediaElementCore::Loop()
{
	if (m_spMediaEngine)
	{
		(m_fLoop) ? m_fLoop = FALSE : m_fLoop = TRUE;
		m_spMediaEngine->SetLoop(m_fLoop);
	}
}

//+-----------------------------------------------------------------------------
//
//  Function:   CanPlayType
//
//  Synopsis:  재생가능여부를 리턴한다.
//
//------------------------------------------------------------------------------
MediaCanPlayResponse MediaElementCore::CanPlayType(String^ mimeType)
{
	MF_MEDIA_ENGINE_CANPLAY pAnswer = {};
	size_t cchAllocationSize = 1 + ::wcslen(mimeType->Data());
	BSTR type = (LPWSTR)::CoTaskMemAlloc(sizeof(WCHAR)*(cchAllocationSize));

	if (type == 0)
	{
		CCPlayer::ThrowIfFailed(E_OUTOFMEMORY);
	}

	StringCchCopyW(type, cchAllocationSize, mimeType->Data());
	if (m_spEngineEx)
	{
		m_spEngineEx->CanPlayType(type, &pAnswer);
	}

	if (nullptr != type)
	{
		::CoTaskMemFree(type);
		type = nullptr;
	}

	MediaCanPlayResponse response;
	switch (pAnswer)
	{
	case MF_MEDIA_ENGINE_CANPLAY::MF_MEDIA_ENGINE_CANPLAY_MAYBE:
		response = MediaCanPlayResponse::Maybe;
		break;
	case MF_MEDIA_ENGINE_CANPLAY::MF_MEDIA_ENGINE_CANPLAY_PROBABLY:
		response = MediaCanPlayResponse::Probably;
		break;
	default:
		response = MediaCanPlayResponse::NotSupported;
		break;
	}

	return response;
}

String^ MediaElementCore::GetAudioStreamLanguage(int audioStreamIndex)
{
	String^ language = nullptr;
	if (m_audioStreamIdexes.size() > 0)
	{
		PROPVARIANT pvValue;
		DWORD newIndex = m_audioStreamIdexes.at(audioStreamIndex);
		if (m_spEngineEx)
		{
			m_spEngineEx->GetStreamAttribute(newIndex, MF_SD_LANGUAGE, &pvValue);
		}

		if (pvValue.vt == VT_LPWSTR)
		{
			language = ref new String(pvValue.bstrVal);
			//wcscpy_s(nullptr, ::wcslen(pvValue.bstrVal) + 1, pvValue.bstrVal);
		}

		PropVariantClear(&pvValue);
	}
	return language;
}

int MediaElementCore::GetMediaErrorCode()
{
	/*IMFMediaError* error;
	m_spEngineEx->GetError(&error);
	USHORT errCode = error->GetErrorCode();

	return (int)errCode;*/
	return (int)m_errorCode;
	/*MF_MEDIA_ENGINE_ERR err = (MF_MEDIA_ENGINE_ERR)errCode;
	HRESULT hr = error->GetExtendedErrorCode();

	Platform::String^ msg;
	switch (err)
	{
	case MF_MEDIA_ENGINE_ERR_NOERROR:
	msg = "MF_MEDIA_ENGINE_ERR_NOERROR";
	break;
	case MF_MEDIA_ENGINE_ERR_ABORTED:
	msg = "MF_MEDIA_ENGINE_ERR_ABORTED";
	break;
	case MF_MEDIA_ENGINE_ERR_NETWORK:
	msg = "MF_MEDIA_ENGINE_ERR_NETWORK";
	break;
	case MF_MEDIA_ENGINE_ERR_DECODE:
	msg = "MF_MEDIA_ENGINE_ERR_DECODE";
	break;
	case MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED:
	msg = "MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED";
	break;
	case MF_MEDIA_ENGINE_ERR_ENCRYPTED:
	msg = "MF_MEDIA_ENGINE_ERR_ENCRYPTED";
	break;
	}*/

}

void MediaElementCore::OnSizeChanged(Platform::Object ^sender, Windows::UI::Xaml::SizeChangedEventArgs ^e)
{
	//OutputDebugMessage(L"width : %f, height : %f\n", e->NewSize.Width, e->NewSize.Height);
	UpdateForWindowSizeChange();
}

Platform::IBox<int>^ MediaElementCore::AudioStreamIndex::get()
{
	DWORD audioStreamIndex = -1;

	if (m_audioStreamIdexes.size() > 0)
	{
		for (DWORD i = 0; i < m_audioStreamIdexes.size(); i++)
		{
			BOOL pEnable;
			if (m_spEngineEx)
			{
				m_spEngineEx->GetStreamSelection(m_audioStreamIdexes.at(i), &pEnable);

				if (pEnable)
				{
					audioStreamIndex = m_audioStreamIdexes.at(i);
					break;
				}
			}
		}
	}

	return ref new Platform::Box<int>(audioStreamIndex);
}

void MediaElementCore::AudioStreamIndex::set(Platform::IBox<int>^ value)
{
	if (m_spEngineEx)
	{
		if (m_audioStreamIdexes.size() > 0)
		{
			for (DWORD i = 0; i < m_audioStreamIdexes.size(); i++)
			{
				m_spEngineEx->SetStreamSelection(m_audioStreamIdexes.at(i), false);
			}

			DWORD index = -1;
			if (value == nullptr || value->Value < 0 || value->Value >= m_audioStreamIdexes.size())
			{
				if (m_audioStreamIdexes.size() > 0)
				{
					index = 0;
				}
			}
			else
			{
				index = m_audioStreamIdexes[value->Value];
			}

			if (index != -1)
			{
				HRESULT newRslt = m_spEngineEx->SetStreamSelection(index, true);

				if (SUCCEEDED(newRslt))
				{
					m_spEngineEx->ApplyStreamSelections();
				}
			}
		}
	}
}

CCPlayer::UWP::Xaml::Controls::AspectRatios MediaElementCore::AspectRatio::get()
{
	return m_aspectRatio;
}

void MediaElementCore::AspectRatio::set(CCPlayer::UWP::Xaml::Controls::AspectRatios value)
{
	m_aspectRatio = value;

	bool refreshBackBuffer = true;
	float wRatio = this->m_rcTarget.right / (float)this->NaturalVideoWidth;
	float hRatio = this->m_rcTarget.bottom / (float)this->NaturalVideoHeight;

	//if (wRatio != wRatio || wRatio == std::numeric_limits<float>::infinity()
	//	|| hRatio != hRatio || hRatio == std::numeric_limits<float>::infinity())
	if (std::isnan(wRatio) || std::isnan(hRatio)
		|| std::isinf(wRatio) || std::isinf(hRatio))
	{
		m_scaleX = 1.0f;
		m_scaleY = 1.0f;
	}

	switch (value)
	{
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::R16_9:
		m_scaleX = 1.0f;
		m_scaleY = ((float)NaturalVideoWidth / NaturalVideoHeight) / (16 / 9.0f);
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::R16_10:
		m_scaleX = 1.0f;
		m_scaleY = ((float)NaturalVideoWidth / NaturalVideoHeight) / (16 / 10.0f);
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::R4_3:
		m_scaleX = 1.0f;
		m_scaleY = ((float)NaturalVideoWidth / NaturalVideoHeight) / (4 / 3.0f);
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::R185_1:
		m_scaleX = 1.0f;
		m_scaleY = ((float)NaturalVideoWidth / NaturalVideoHeight) / (185 / 100.0f);
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::R235_1:
		m_scaleX = 1.0f;
		m_scaleY = ((float)NaturalVideoWidth / NaturalVideoHeight) / (235 / 100.0f);
		break;
	default:
		refreshBackBuffer = CheckStretchMode(value);
	}

	if (refreshBackBuffer)
	{
		m_matrixScale._11 = (float)floor(1.0f / m_rawPixelsPerViewPixel * 10000.0) / 10000.0f * m_scaleX;
		m_matrixScale._22 = (float)floor(1.0f / m_rawPixelsPerViewPixel * 10000.0) / 10000.0f * m_scaleY;
		m_matrixScale._31 = 0.0f;
		m_matrixScale._32 = 0.0f;

		//화면 버퍼 사이즈 및 배율 갱신
		CreateBackBuffers();

		if (m_fStopTimer == FALSE && m_currentState == MediaElementState::Paused)
		{
			EnterCriticalSection(&m_critSec);
			//일시정지된 경우 이전 프레임을 다시 텍스쳐를 통해 화면을 그린다.
			DisplayFrame();
			//HRESULT hr = m_spEngineEx->UpdateVideoStream(&m_normalizedRect, &m_rcTarget, &m_bkgColor);
			LeaveCriticalSection(&m_critSec);
		}
	}
}

bool MediaElementCore::CheckStretchMode(CCPlayer::UWP::Xaml::Controls::AspectRatios value)
{
	bool isStretchMode = true;
	float wRatio = this->m_rcTarget.right / (float)this->NaturalVideoWidth;
	float hRatio = this->m_rcTarget.bottom / (float)this->NaturalVideoHeight;

	switch (value)
	{
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::None:
		if (wRatio > hRatio)
		{
			m_scaleX = hRatio / wRatio;
			m_scaleY = m_scaleX;
		}
		else
		{
			m_scaleY = wRatio / hRatio;
			m_scaleX = m_scaleY;
		}
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::Fill:
		if (wRatio > hRatio)
		{
			m_scaleX = wRatio / hRatio;
		}
		else
		{
			m_scaleY = hRatio / wRatio;
		}
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::Uniform:
		m_scaleX = 1;
		m_scaleY = 1;
		break;
	case CCPlayer::UWP::Xaml::Controls::AspectRatios::UniformToFill:
		if (wRatio > hRatio)
		{
			m_scaleX = wRatio / hRatio;
			m_scaleY = m_scaleX;
		}
		else
		{
			m_scaleY = hRatio / wRatio;
			m_scaleX = m_scaleY;
		}
		break;
	default:
		isStretchMode = false;
		break;
	}
	return isStretchMode;
}
CCPlayer::UWP::Xaml::Controls::DisplayRotations MediaElementCore::DisplayRotation::get()
{
	return m_displayRotation;
}

void MediaElementCore::DisplayRotation::set(CCPlayer::UWP::Xaml::Controls::DisplayRotations value)
{
	//UpdateForWindowSizeChange()에서 이 값을 체크하기 때문에 먼저 변경해야 함.
	m_displayRotation = value;

	if (m_spEngineEx)
	{
		ComPtr<IDXGISwapChain2> spSwapChain2;
		CCPlayer::ThrowIfFailed(m_spDX11SwapChain.As<IDXGISwapChain2>(&spSwapChain2));

		switch (value)
		{
		case CCPlayer::UWP::Xaml::Controls::DisplayRotations::None:
			spSwapChain2->SetRotation(DXGI_MODE_ROTATION::DXGI_MODE_ROTATION_IDENTITY);
			break;
		case CCPlayer::UWP::Xaml::Controls::DisplayRotations::Clockwise90:
			spSwapChain2->SetRotation(DXGI_MODE_ROTATION::DXGI_MODE_ROTATION_ROTATE270);
			break;
		case CCPlayer::UWP::Xaml::Controls::DisplayRotations::Clockwise180:
			spSwapChain2->SetRotation(DXGI_MODE_ROTATION::DXGI_MODE_ROTATION_ROTATE180);
			break;
		case CCPlayer::UWP::Xaml::Controls::DisplayRotations::Clockwise270:
			spSwapChain2->SetRotation(DXGI_MODE_ROTATION::DXGI_MODE_ROTATION_ROTATE90);
			break;
		}
		//회전후 이미지가 화면 중심에 위치하도록 화면을 갱신
		UpdateForWindowSizeChange(true);
	}
}

Windows::Foundation::TimeSpan MediaElementCore::Position::get()
{
	//Position은 Transport컨트롤에 바인딩이 되어 L:meME가 nullptr이 되었는데 
	//이벤트에 의해 타이밍적으로 호출 되는 경우가 있어서, 임시방편으로 this에 대한 nullptr을 체크함. 개선 필요.
	if (this != nullptr && m_spMediaEngine != nullptr)
	{
		TimeSpan ts;
		ts.Duration = (long long)(m_spMediaEngine->GetCurrentTime() * 10000000L);
		return ts;
	}
	return TimeSpan();
}

void MediaElementCore::Position::set(Windows::Foundation::TimeSpan position)
{
	if (m_spMediaEngine && this->CanSeek)
	{
		m_spMediaEngine->SetCurrentTime((double)position.Duration / 10000000L);
	}
}

MediaElementState MediaElementCore::CurrentState::get()
{
	return m_currentState;
}

void MediaElementCore::CurrentState::set(MediaElementState currentState)
{
	m_currentState = currentState;
}

Windows::UI::Xaml::Duration MediaElementCore::NaturalDuration::get()
{
	TimeSpan ts = TimeSpan();

	if (this != nullptr && m_spMediaEngine != nullptr)
	{
		double duration = m_spMediaEngine->GetDuration();

		// NOTE:
		// "duration != duration"
		// This tests if duration is NaN, because NaN != NaN
		//if (duration != duration || duration == std::numeric_limits<double>::infinity())
		if (std::isnan(duration) ||  std::isinf(duration))
		{
			ts.Duration = 0;
		}
		else
		{
			ts.Duration = (long long)(duration * 10000000L);
		}
	}
	/*else
	{
		CCPlayer::ThrowIfFailed(E_FAIL);
	}*/

	return DurationHelper::FromTimeSpan(ts);
}

int MediaElementCore::NaturalVideoWidth::get()
{
	DWORD width = 0, height = 0;

	if (m_spEngineEx)
	{
		m_spEngineEx->GetNativeVideoSize(&width, &height);
	}
	return width;
}

int MediaElementCore::NaturalVideoHeight::get()
{
	DWORD width = 0, height = 0;

	if (m_spEngineEx)
	{
		m_spEngineEx->GetNativeVideoSize(&width, &height);
	}
	return height;
}

int MediaElementCore::AudioStreamCount::get()
{
	if (this != nullptr && m_spEngineEx)
	{
		return (int)m_audioStreamIdexes.size();
	}
	return 0;
}

double MediaElementCore::Balance::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetBalance();
	}
	return 0.0;
}

void MediaElementCore::Balance::set(double balance)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetBalance(balance);
	}
}

double MediaElementCore::Volume::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetVolume();
	}
	return 0.0;
}

void MediaElementCore::Volume::set(double volume)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetVolume(volume);
	}
}

double MediaElementCore::DefaultPlaybackRate::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetDefaultPlaybackRate();
	}
	return 1.0;
}

void MediaElementCore::DefaultPlaybackRate::set(double defaultPlaybackRate)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetDefaultPlaybackRate(defaultPlaybackRate);
	}
}

double MediaElementCore::PlaybackRate::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetPlaybackRate();
	}
	return 1.0;
}

void MediaElementCore::PlaybackRate::set(double playbackRate)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetPlaybackRate(playbackRate);
	}
}

bool MediaElementCore::AutoPlay::get()
{
	/*if (m_spEngineEx)
	{
		return m_spEngineEx->GetAutoPlay() == TRUE ? true : false;
	}
	return true;*/
	return m_autoPlay;
}

void MediaElementCore::AutoPlay::set(bool value)
{
	if (m_autoPlay != value)
	{
		m_autoPlay = value;
	}
	//if (m_spEngineEx)
	//{
		//CCPlayer::ThrowIfFailed(m_spEngineEx->SetAutoPlay(autoPlay ? TRUE : FALSE));
	//}
}

bool MediaElementCore::IsFullWindow::get()
{
	return m_isFullWindow;
}

void MediaElementCore::IsFullWindow::set(bool value)
{
	m_isFullWindow = value;
}

bool MediaElementCore::RealTimePlayback::get()
{
	if (m_spEngineEx)
	{
		BOOL isRealTime;
		m_spEngineEx->GetRealTimeMode(&isRealTime);
		return isRealTime == TRUE;
	}
	return true;
}

void MediaElementCore::RealTimePlayback::set(bool value)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetRealTimeMode(value ? TRUE : FALSE);
	}
}

bool MediaElementCore::IsMuted::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetMuted() == TRUE;
	}
	return false;
}

void MediaElementCore::IsMuted::set(bool value)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetMuted(value ? TRUE : FALSE);
	}
}

bool MediaElementCore::IsLooping::get()
{
	if (m_spEngineEx)
	{
		return m_spEngineEx->GetLoop() == TRUE;
	}
	return false;
}

void MediaElementCore::IsLooping::set(bool value)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->SetLoop(value ? TRUE : FALSE);
	}
}

bool MediaElementCore::IsAudioOnly::get()
{
	bool hasAudio = false;

	if (m_spEngineEx)
	{
		hasAudio = m_spEngineEx->HasAudio() == TRUE;
		if (m_spEngineEx->HasVideo() == TRUE)
		{
			return false;
		}
	}
	return hasAudio;
}

bool MediaElementCore::CanPause::get()
{
	IMFMediaTimeRange *ppPlayed;
	if (m_spEngineEx)
	{
		bool result = m_spEngineEx->GetPlayed(&ppPlayed) == S_OK;

		if (ppPlayed)
		{
			ppPlayed->Release();
		}

		return result;
	}
	return false;
}

bool MediaElementCore::CanSeek::get()
{
	DWORD caps = 0;
	bool canSeek = false;
	bool seekable = false;
	if (m_spEngineEx)
	{
		//미디어 소스의 seek 여부
		m_spEngineEx->GetResourceCharacteristics(&caps);
		canSeek = (caps & ME_CAN_SEEK) == ME_CAN_SEEK;
	
		//미디어 엔진의 seek 여부
		IMFMediaTimeRange *ppTimeRange;
		seekable = m_spEngineEx->GetSeekable(&ppTimeRange) == S_OK;
		if (ppTimeRange)
		{
			ppTimeRange->Release();
		}
	}

	return canSeek && seekable;
}

void MediaElementCore::EnableHorizontalMirrorMode(bool value)
{
	if (m_spEngineEx)
	{
		m_spEngineEx->EnableHorizontalMirrorMode(value ? TRUE : FALSE);
	}
}

BOOL MediaElementCore::IsPlaying()
{
	return m_fPlaying;
}

TimelineMarkerCollection^ MediaElementCore::Markers::get()
{
	return m_markers.Get();
}

void MediaElementCore::Markers::set(TimelineMarkerCollection^ value)
{
	m_markers = value;
}
