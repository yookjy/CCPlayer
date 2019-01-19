//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
#pragma once

#include <pch.h>
#include <d3d11_2.h>
#include <dxgi1_3.h>
#include <mfmediaengine.h>
#include <agile.h>
//thread sleep
//#include <chrono>
//#include <thread>
#include "Common.h"

#ifndef MediaElementCore_H
#define MediaElementCore_H

#define ME_CAN_SEEK 0x00000002

using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::System::Display;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				//-----------------------------------------------------------------------------
				// MediaEngineNotifyCallback
				//
				// Defines the callback method to process media engine events.
				//-----------------------------------------------------------------------------
				ref struct MediaEngineNotifyCallback abstract
				{
				internal:
					virtual void OnMediaEngineEvent(DWORD meEvent) = 0;
				};

				// MediaElementCore: Manages the Media Engine.
				ref class MediaElementCore : public MediaEngineNotifyCallback
				{
					// DX11 related
					Microsoft::WRL::ComPtr<ID3D11Device>                m_spDX11Device;
					Microsoft::WRL::ComPtr<ID3D11DeviceContext>         m_spDX11DeviceContext;
					Microsoft::WRL::ComPtr<IDXGIOutput>                 m_spDXGIOutput;
					Microsoft::WRL::ComPtr<IDXGISwapChain1>             m_spDX11SwapChain;
					Microsoft::WRL::ComPtr<IMFDXGIDeviceManager>        m_spDXGIManager;

					// Media Engine related
					Microsoft::WRL::ComPtr<IMFMediaEngine>              m_spMediaEngine;
					Microsoft::WRL::ComPtr<IMFMediaEngineEx>            m_spEngineEx;

					BSTR                                    m_bstrURL;
					BOOL                                    m_fPlaying;
					BOOL                                    m_fLoop;
					BOOL                                    m_fEOS;
					BOOL                                    m_fStopTimer;
					RECT                                    m_rcTarget;
					DXGI_FORMAT                             m_d3dFormat;
					MFARGB                                  m_bkgColor;

					HANDLE                                  m_TimerThreadHandle;
					CRITICAL_SECTION                        m_critSec;

					concurrency::task<Windows::Storage::StorageFile^>   m_pickFileTask;
					concurrency::cancellation_token_source              m_tcs;
					BOOL                                                m_fInitSuccess;
					BOOL                                                m_fExitApp;
					BOOL                                                m_fUseDX;
					
					MediaElementState m_currentState;


				internal:

					MediaElementCore();
					MediaElementCore(Panel^ panel);

					// DX11 related
					void CreateDX11Device();
					void CreateBackBuffers();

					// Initialize/Shutdown
					void Initialize();
					void Shutdown();
					BOOL ExitApp();

					// Media Engine related
					virtual void OnMediaEngineEvent(DWORD meEvent) override;

					// Loading
					void SetURL(String^ szURL);
					void SetBytestream(Windows::Storage::Streams::IRandomAccessStream^ streamHandle);
					void SetSource(Windows::Storage::Streams::IRandomAccessStream^ stream, String^ mimeType);

					// Transport state
					void Play();
					void Pause();
					void FrameStep();
					void Stop();

					// Video
					//void ResizeVideo(HWND hwnd);
					void EnableVideoEffect(BOOL enable);

					// Seeking and duration.
					//void GetDuration(double *pDuration, BOOL *pbCanSeek);
					BOOL IsSeeking();

					// Window Event Handlers
					void UpdateForWindowSizeChange();
					void UpdateForWindowSizeChange(bool force);

					// Timer thread related
					void StartTimer();
					void StopTimer();
					void OnTimer();
					DWORD RealVSyncTimer();
					int GetMediaErrorCode();
					void SetCurrentTimeMarker(long long time);

					void DisplayFrame();

					// For calling IDXGIDevice3::Trim() when app is suspended
					HRESULT DXGIDeviceTrim();

					void Loop();
					BOOL IsPlaying();
					MediaCanPlayResponse CanPlayType(String^ mimeType);
					String^ GetAudioStreamLanguage(int audioStreamIndex);
					void EnableHorizontalMirrorMode(bool value);

					property CCPlayer::UWP::Xaml::Controls::AspectRatios AspectRatio { CCPlayer::UWP::Xaml::Controls::AspectRatios get(); void set(CCPlayer::UWP::Xaml::Controls::AspectRatios value); }
					property CCPlayer::UWP::Xaml::Controls::DisplayRotations DisplayRotation { CCPlayer::UWP::Xaml::Controls::DisplayRotations get(); void set(CCPlayer::UWP::Xaml::Controls::DisplayRotations value); }
					property TimeSpan Position { TimeSpan get(); void set(TimeSpan value); }
					property MediaElementState CurrentState { MediaElementState get(); void set(MediaElementState value); }
					property Duration NaturalDuration { Duration get(); }
					property int NaturalVideoWidth { int get();  }
					property int NaturalVideoHeight { int get();  }
					property int AudioStreamCount { int get(); }
					property Platform::IBox<int>^ AudioStreamIndex { Platform::IBox<int>^ get(); void set(Platform::IBox<int>^ value); }
					property double Balance { double get(); void set(double value); }
					property double Volume { double get(); void set(double value); }
					property double DefaultPlaybackRate { double get(); void set(double value); }
					property double PlaybackRate { double get(); void set(double value); }
					property bool AutoPlay { bool get(); void set(bool value); }
					property bool RealTimePlayback { bool get(); void set(bool value); }
					property bool IsFullWindow { bool get(); void set(bool value); }
					property bool IsMuted { bool get(); void set(bool value); }
					property bool IsLooping { bool get(); void set(bool value); }
					property bool IsAudioOnly { bool get(); }
					property bool CanPause { bool get(); }
					property bool CanSeek { bool get(); }
					property Uri^ Source { Uri^ get(); void set(Uri^ value); }
					property TimelineMarkerCollection^ Markers { TimelineMarkerCollection^ get(); void set(TimelineMarkerCollection^ value); }
					property Windows::UI::Xaml::Controls::Panel^ Parent
					{
						Windows::UI::Xaml::Controls::Panel^ get() { return _Parent.Resolve<Windows::UI::Xaml::Controls::Panel>(); }
						void set(Windows::UI::Xaml::Controls::Panel^ value) { _Parent = value; }
					};

					event RoutedEventHandler^ CurrentStateChanged;
					event TimelineMarkerRoutedEventHandler^ MarkerReached;
					event RoutedEventHandler^ MediaEnded;
					event RoutedEventHandler^ MediaFailed;
					event RoutedEventHandler^ MediaOpened;
					event RoutedEventHandler^ SeekCompleted;

				private:
					Windows::UI::Xaml::Controls::SwapChainPanel^ _SwapChainPanel;
					Windows::UI::Xaml::Controls::Grid^ _ScreenOffPanel;
					Windows::UI::Xaml::Media::Animation::Storyboard^ _ScreenOffPanelStoryBoard;

					~MediaElementCore();
					bool m_autoPlay;
					bool m_isFirstFrame;
					bool m_isSeeking;
					int64 m_lastMarkerTime;
					bool m_isFullWindow;
					Agile<TimelineMarkerCollection^> m_markers;
					TimelineMarker^ m_marker;
					Uri^ m_source;
					bool canSizeChangeEvent;
					USHORT m_errorCode;
					std::vector<DWORD> m_audioStreamIdexes;
					float m_scaleX;
					float m_scaleY;
					float m_rawPixelsPerViewPixel;
					DXGI_MATRIX_3X2_F m_matrixScale;
					MFVideoNormalizedRect m_normalizedRect;
					CCPlayer::UWP::Xaml::Controls::AspectRatios m_aspectRatio;
					CCPlayer::UWP::Xaml::Controls::DisplayRotations m_displayRotation;
					Platform::WeakReference _Parent;

					void OnSizeChanged(Object ^sender, SizeChangedEventArgs ^e);
					bool CheckStretchMode(CCPlayer::UWP::Xaml::Controls::AspectRatios value);
					void NotifyStateChanged();
				};
			}
		}
	}
}



#endif /* MediaElementCore_H */
