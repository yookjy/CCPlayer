//
// MediaElement.xaml.cpp
// Implementation of the MediaElement class
//

#include "pch.h"
#include "MediaElement.xaml.h"
#include "MediaTransportControls.xaml.h"
#include "ClosedCaptions.xaml.h"
#include "Helpers\LanguageCodeHelper.h"
#include <regex>
#include <experimental\\resumable>
#include <pplawait.h>

namespace WFC = Windows::Foundation::Collections;
namespace WUX = Windows::UI::Xaml;
namespace CUX = CCPlayer::UWP::Xaml;
namespace CUC = CCPlayer::UWP::Common;
namespace CUF = CCPlayer::UWP::Factory;

using namespace concurrency;
using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::Foundation;
using namespace Windows::Graphics::Display;
using namespace Windows::Media::Core;
using namespace Windows::Storage;
using namespace Windows::Storage::Search;
using namespace Windows::System::Threading;
using namespace WUX::Data;
using namespace WUX::Input;
using namespace WUX::Interop;
using namespace WUX::Media;
using namespace WUX::Navigation;
using namespace Windows::Devices::Sensors;
using namespace Windows::UI::ViewManagement;
using namespace Windows::UI::Core;

using namespace WFC;
using namespace CUC::Codec;
using namespace CUC::Interface;
using namespace CUF::Connector;

#define AV_DEC_CONN "AVDecoderConnector"
#define SUB_DEC_CONN "SubtitleDecoderConnector"
#define ATC_DEC_CONN "AttachmentDecoderConnector"
#define FFMPEG_OPTION "FFmpegOptionProperties"

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
CUX::Controls::MediaElement::MediaElement() :
	_DisplayRequest(nullptr),
	_IsFullScreen(false),
	_PrevActWidth(std::numeric_limits<double>::quiet_NaN()),
	_PrevActHeight(std::numeric_limits<double>::quiet_NaN()),
	_PrevHAlign(WUX::HorizontalAlignment::Stretch),
	_PrevVAlign(WUX::VerticalAlignment::Stretch),
	_UseLimeEngine(true)
{
	InitializeComponent();
	//디코더 등록
	RegisterDecoders();

	this->SizeChanged += ref new WUX::SizeChangedEventHandler(this, &CUX::Controls::MediaElement::OnSizeChanged);
	this->Loaded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnLoaded);
	this->Unloaded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnUnloaded);

	MediaTransportControls->CCPMediaElement = this;
	MediaTransportControls->MoveClosedCaptionPositionStarted += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnClosedCaptionMovePositionStarted);
	MediaTransportControls->ImportClosedCaptionTapped += ref new WUX::Input::TappedEventHandler(this, &CUX::Controls::MediaElement::OnImportClosedCaptionTapped);

	CloseButtonTappedToken = MediaTransportControls->CloseButtonTapped += ref new WUX::Input::TappedEventHandler(this, &CUX::Controls::MediaElement::OnCloseButtonTapped);
	CCSettingsOpenButtonTappedToken = MediaTransportControls->CCSettingsOpenButtonTapped += ref new WUX::Input::TappedEventHandler(this, &CUX::Controls::MediaElement::OnCCSettingsOpenButtonTapped);
	PreviousMediaButtonTappedToken = MediaTransportControls->PreviousMediaButtonTapped += ref new WUX::Input::TappedEventHandler(this, &CUX::Controls::MediaElement::OnPreviousMediaButtonTapped);
	NextMediaButtonTappedToken = MediaTransportControls->NextMediaButtonTapped += ref new WUX::Input::TappedEventHandler(this, &CUX::Controls::MediaElement::OnNextMediaButtonTapped);

	try
	{
		_SimpleOrientationSensor = SimpleOrientationSensor::GetDefault();
		if (_SimpleOrientationSensor != nullptr)
		{
			_SimpleOrientationSensor->OrientationChanged += ref new TypedEventHandler<SimpleOrientationSensor^, SimpleOrientationSensorOrientationChangedEventArgs^>(this, &CUX::Controls::MediaElement::OnOrientationChanged);
		}
	}
	catch (Exception^) {}

	//Window::Current->Activated += ref new WindowActivatedEventHandler(this, &CUX::Controls::MediaElement::OnActivated);
}

//void CUX::Controls::MediaElement::OnActivated(Platform::Object ^sender, Windows::UI::Core::WindowActivatedEventArgs^ e)
//{
//	_CrtCheckMemory();
//}

void CUX::Controls::MediaElement::RegisterDecoders()
{
	_MediaExtensionManager = ref new Windows::Media::MediaExtensionManager();
	_MediaFoundationPropertySet = ref new Windows::Foundation::Collections::PropertySet();

	//디코더 등록
	GUID vidGuid;
	String^ vidTxt = "{C1FC552A-B7B8-4DBB-8A93-5B918B2A082A}";
	if (SUCCEEDED(IIDFromString(vidTxt->Data(), &vidGuid))) {
		//Platform::Guid guid(vidGuid);
		_MediaExtensionManager->RegisterVideoDecoder("FFmpegDecoder.FFmpegUncompressedVideoDecoder", vidGuid, Guid(), _MediaFoundationPropertySet);
	}
	GUID audGuid;
	String^ audTxt = "{6BAE7E7C-1560-4217-8636-71D18D67A9D2}";
	if (SUCCEEDED(IIDFromString(audTxt->Data(), &audGuid))) {
		//Platform::Guid guid(audGuid);
		_MediaExtensionManager->RegisterAudioDecoder("FFmpegDecoder.FFmpegUncompressedAudioDecoder", audGuid, Guid(), _MediaFoundationPropertySet);
	}

	_MediaExtensionManager->RegisterSchemeHandler("FFmpegSource.FFmpegSchemeHandler", "http:", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterSchemeHandler("FFmpegSource.FFmpegSchemeHandler", "https:", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterSchemeHandler("FFmpegSource.FFmpegSchemeHandler", "ftp:", _MediaFoundationPropertySet);

	//바이트 스트림 핸들러 등록
	//MediaFoundationPropertySet["Decoder"] = "AUTO";
	//_MediaFoundationPropertySet["DecoderConnector"] = DecoderConnector.Instance;
	//mediaExtensionManager.RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "");
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/mpeg", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/mp4", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/avi", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-matroska", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/webm", _MediaFoundationPropertySet);
	//mediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "application/x-shockwave-flash");
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-ms-asf", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-ms-wmv", _MediaFoundationPropertySet);
	//mediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/x-flv");
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/vnd.dlna.mpeg-tts", _MediaFoundationPropertySet);
	//mediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/vnd.mts");
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/3gpp", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/3gpp2", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/quicktime", _MediaFoundationPropertySet);
	_MediaExtensionManager->RegisterByteStreamHandler("FFmpegSource.FFmpegByteStreamHandler", ".*", "video/ffmpeg", _MediaFoundationPropertySet);

}

void CUX::Controls::MediaElement::OnLoaded(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
}

void CUX::Controls::MediaElement::OnUnloaded(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	//전체 화면 종료
	IsFullScreen = false;
}

CUX::Controls::MediaElement::~MediaElement()
{
	OutputDebugMessage(L"Called constructor of the MediaElement\n");
}

void CUX::Controls::MediaElement::OnSizeChanged(Platform::Object ^sender, WUX::SizeChangedEventArgs ^e)
{
	float width = e->NewSize.Width;
	float height = e->NewSize.Height;
	
	CCPopup->Width = width;
	CCPopup->Height = height;

	dynamic_cast<FrameworkElement^>(CCPopup->Child)->Width = width;
	dynamic_cast<FrameworkElement^>(CCPopup->Child)->Height = height;

	TCPopup->Width = width;
	TCPopup->Height = height;

	MediaTransportControls->Width = width;
	MediaTransportControls->Height = height;

	if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Mobile"
		&& UIViewSettings::GetForCurrentView()->UserInteractionMode != Windows::UI::ViewManagement::UserInteractionMode::Touch)
	{
		//Maximize버튼에 따라 풀스크린 원복
		auto view = ApplicationView::GetForCurrentView();
		if ((!view->AdjacentToLeftDisplayEdge || !view->AdjacentToRightDisplayEdge) && !view->IsFullScreenMode)
		{
			IsFullScreen = false;
			VisualStateManager::GoToState(MediaTransportControls, "NonFullWindowState", false);
		}
	}
}

Stretch CUX::Controls::MediaElement::Stretch::get()
{
	if (CheckLimeEngine())
	{
		switch (LimeME->AspectRatio)
		{
		case CUX::Controls::AspectRatios::Fill:
			return WUX::Media::Stretch::Fill;
		case CUX::Controls::AspectRatios::Uniform:
			return WUX::Media::Stretch::Uniform;
		case CUX::Controls::AspectRatios::UniformToFill:
			return WUX::Media::Stretch::UniformToFill;
		default:
			return WUX::Media::Stretch::None;
		}
	}
	else
	{
		return WindowsME->Stretch;
	}
}

void CUX::Controls::MediaElement::Stretch::set(WUX::Media::Stretch stretch)
{
	if (CheckLimeEngine())
	{
		switch (stretch)
		{
		case WUX::Media::Stretch::Fill:
			LimeME->AspectRatio = CUX::Controls::AspectRatios::Fill;
			break;
		case WUX::Media::Stretch::Uniform:
			LimeME->AspectRatio = CUX::Controls::AspectRatios::Uniform;
			break;
		case WUX::Media::Stretch::UniformToFill:
			LimeME->AspectRatio = CUX::Controls::AspectRatios::UniformToFill;
			break;
		default:
			LimeME->AspectRatio = CUX::Controls::AspectRatios::None;
			break;
		}
	}
	else
	{
		WindowsME->Stretch = stretch;
	}
}

bool CUX::Controls::MediaElement::IsFullWindow::get()
{
	//MediaElement의 부모 컨트롤
	auto window = Windows::ApplicationModel::Core::CoreApplication::GetCurrentView()->CoreWindow;

	auto ww = window->Bounds.Width;
	auto wh = window->Bounds.Height;

	auto cw = this->ActualWidth;
	auto ch = this->ActualHeight;

	bool isFullWindow = (ww == cw && wh == ch);
	return isFullWindow;
}

void CUX::Controls::MediaElement::IsFullWindow::set(bool value)
{
	if (value)
	{
		if (!std::isnan(this->Width))
		{
			_PrevActWidth = this->Width;
			this->ClearValue(WidthProperty);
		}

		if (!std::isnan(this->Height))
		{
			_PrevActHeight = this->Height;
			this->ClearValue(HeightProperty);
		}

		_PrevHAlign = this->HorizontalAlignment;
		_PrevVAlign = this->VerticalAlignment;

		this->ClearValue(HorizontalAlignmentProperty);
		this->ClearValue(VerticalAlignmentProperty);
	}
	else
	{
		if (!std::isnan(_PrevActWidth))
		{
			this->Width = _PrevActWidth;
		}

		if (!std::isnan(_PrevActHeight))
		{
			this->Height = _PrevActHeight;
		}

		this->HorizontalAlignment = _PrevHAlign;
		this->VerticalAlignment = _PrevVAlign;
	}
}

bool CUX::Controls::MediaElement::IsFullScreen::get()
{
	return _IsFullScreen;
}

void CUX::Controls::MediaElement::IsFullScreen::set(bool value)
{
	auto view = ApplicationView::GetForCurrentView();
	IsFullWindow = value;

	if (value)
	{
		if (!CheckLimeEngine())
		{
			WindowsME->IsFullWindow = true;
		}

		//풀스크린 요청
		if (_IsFullScreen = view->TryEnterFullScreenMode())
		{
			ApplicationView::PreferredLaunchWindowingMode = ApplicationViewWindowingMode::FullScreen;
			// The SizeChanged event will be raised when the entry to full-screen mode is complete.
		}

		if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Mobile"
			&& UIViewSettings::GetForCurrentView()->UserInteractionMode != Windows::UI::ViewManagement::UserInteractionMode::Touch)
		{
			//데탑에서 이전에 FullScreen인채 종료가 된경우 등.
			if (!_IsFullScreen && view->IsFullScreenMode)
			{
				_IsFullScreen = true;
				IsFullWindow = true;
				//아이콘 모양 강제 변경
				VisualStateManager::GoToState(this->MediaTransportControls, "FullWindowState", false);
			}
		}
	}
	else
	{
		//풀스크린 해제
		if (!CheckLimeEngine())
		{
			WindowsME->IsFullWindow = false;
		}
		_IsFullScreen = false;
		view->ExitFullScreenMode();
		ApplicationView::PreferredLaunchWindowingMode = ApplicationViewWindowingMode::Auto;
		// The SizeChanged event will be raised when the exit from full-screen mode is complete.
	}
	
	//변경된 값에 따라 전원 차단 설정 변경
	if (_IsFullScreen)
	{
		try
		{
			if (_DisplayRequest == nullptr)
			{
				_DisplayRequest = ref new DisplayRequest();
			}

			//화면 계속 켜짐 요청.
			_DisplayRequest->RequestActive();
		}
		catch (Exception^ e) {}
	}
	else
	{
		try
		{
			//화면 계속 켜짐 요청을 해제.
			if (_DisplayRequest != nullptr)
			{
				_DisplayRequest->RequestRelease();
				_DisplayRequest = nullptr;
			}
		}
		catch (Exception^ e) {}
	}

	//모바일의 경우 StatusBar 처리
	if (CheckLimeEngine() && Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
	{
		StatusBar^ statusBar = StatusBar::GetForCurrentView();
		if (_IsFullScreen)
		{
			statusBar->HideAsync();
		}
		else
		{
			statusBar->ShowAsync();
		}
	}
}

bool CUX::Controls::MediaElement::UseLimeEngine::get()
{
	return _UseLimeEngine;
}

void CUX::Controls::MediaElement::UseLimeEngine::set(bool value)
{
	if (_UseLimeEngine != value)
	{
		_UseLimeEngine = value;
		//초기화
		AutoPlay = true;
		IsLooping = false;
		IsMuted = false;
		RealTimePlayback = false;
		AudioStreamIndex = -1;
		PlaybackRate = 1;
		DefaultPlaybackRate = 1;
		Position = TimeSpan();
		
		if (_UseLimeEngine)
		{
			LimeMediaElementPanel->Visibility = WUX::Visibility::Visible;
			WindowsME->Visibility = WUX::Visibility::Collapsed;
		}
		else
		{
			WindowsME->Visibility = WUX::Visibility::Visible;
			LimeMediaElementPanel->Visibility = WUX::Visibility::Collapsed;
		}
	}
	//라임엔진 사용시 특화 기능 활성화
	MediaTransportControls->EnableLimeEngine(value);
}

bool CUX::Controls::MediaElement::CheckLimeEngine()
{
	if (UseLimeEngine)
	{
		if (LimeME == nullptr)
		{
			LimeME = ref new MediaElementCore(LimeMediaElementPanel);
			OutputDebugMessage(L"Created instance of the MediaElementCore \n");
		}
		LimeMediaElementPanel->Visibility = Windows::UI::Xaml::Visibility::Visible;
		return true;
	}
	LimeMediaElementPanel->Visibility = Windows::UI::Xaml::Visibility::Collapsed;
	return false;
}

void CUX::Controls::MediaElement::Stop()
{
	//자막 타이머 정지
	ClosedCaptions->DoStop();
	//자막 제거
	ClearMarkers();

	//재생패널 타이머 정지
	MediaTransportControls->OnStopped();
	//패널 숨김
	MediaTransportControls->Hide();
	
	//전체 화면 종료
	//IsFullScreen = false;

	if (UseLimeEngine && LimeME != nullptr)
	{
		LimeME->Stop();
	}
	else
	{
		WindowsME->Stop();
	}
}

void CUX::Controls::MediaElement::InitSource()
{
	_MediaOpenDataStore.Status = MediaOpenStatus::NewSource;

	//오디오 스트림 목록 초기화
	MediaTransportControls->AudioStreamLanguageSource->Clear();
	AudioStreamLanguageIndex = -1;

	//자막 목록 초기화
	MediaTransportControls->ClosedCaptionSource->Clear();
	MediaTransportControls->ClosedCaptionIndex = -1;
}

void CUX::Controls::MediaElement::SetSource(IRandomAccessStream^ stream, String^ mimeType)
{
	_CurrentFolder = nullptr;
	_CurrentFileName = nullptr;
	//초기화
	InitSource();
	//Uri소스 초기화
	_Uri = nullptr;
	//디코더 변경에서 사용될 스트림과 마임타입 저장
	_Stream = stream;
	_MimeType = mimeType;
	//미디어를 열어 재생 시작
	OpenMedia();
}

Uri^ CUX::Controls::MediaElement::Source::get()
{
	if (CheckLimeEngine())
		return LimeME->Source;
	else
		return WindowsME->Source;
}

void CUX::Controls::MediaElement::Source::set(Uri^ uri)
{
	_CurrentFolder = nullptr;
	_CurrentFileName = nullptr;
	//초기화
	InitSource();
	//스트림소스 초기화
	_Stream = nullptr;
	_MimeType = nullptr;
	//Uri 소스 저장
	_Uri = uri;
	//미디어를 열어 재생 시작
	OpenMedia();
}

void CUX::Controls::MediaElement::AttachMediaElementEvents()
{
	if (UseLimeEngine)
	{
		//신규 미디어 이벤트 추가
		MFMediaOpenedToken = LimeME->MediaOpened += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnMediaOpened);
		MFMediaEndedToken = LimeME->MediaEnded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnMediaEnded);
		MFMediaFailedToken = LimeME->MediaFailed += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnMFMediaFailed);
		MFMarkerReachedToken = LimeME->MarkerReached += ref new WUX::Media::TimelineMarkerRoutedEventHandler(this, &CUX::Controls::MediaElement::OnMarkerReached);
		MFSeekCompletedToken = LimeME->SeekCompleted += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnSeekCompleted);
		MFCurrentStateChangedToken = LimeME->CurrentStateChanged += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnCurrentStateChanged);

		//화면 표시
		this->Stretch = WUX::Media::Stretch::Uniform;
		LimeMediaElementPanel->Visibility = WUX::Visibility::Visible;
	}
	else
	{
		//신규 이벤트 추가
		MediaOpenedToken = WindowsME->MediaOpened += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnMediaOpened);
		MediaEndedToken = WindowsME->MediaEnded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnMediaEnded);
		MediaFailedToken = WindowsME->MediaFailed += ref new WUX::ExceptionRoutedEventHandler(this, &CUX::Controls::MediaElement::OnMediaFailed);
		MarkerReachedToken = WindowsME->MarkerReached += ref new WUX::Media::TimelineMarkerRoutedEventHandler(this, &CUX::Controls::MediaElement::OnMarkerReached);
		SeekCompletedToken = WindowsME->SeekCompleted += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnSeekCompleted);
		CurrentStateChangedToken = WindowsME->CurrentStateChanged += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaElement::OnCurrentStateChanged);

		//화면 표시
		this->Stretch = WUX::Media::Stretch::Uniform;
		WindowsME->Visibility = WUX::Visibility::Visible;
	}
}

void CUX::Controls::MediaElement::DetatchMediaElementEvents()
{
	//기본 이벤트 삭제
	if (WindowsME != nullptr)
	{
		WindowsME->MediaOpened -= MediaOpenedToken;
		WindowsME->MediaEnded -= MediaEndedToken;
		WindowsME->MediaFailed -= MediaFailedToken;
		WindowsME->MarkerReached -= MarkerReachedToken;
		WindowsME->SeekCompleted -= SeekCompletedToken;
		WindowsME->CurrentStateChanged -= CurrentStateChangedToken;
	}

	if (LimeME != nullptr)
	{
		LimeME->MediaOpened -= MFMediaOpenedToken;
		LimeME->MediaEnded -= MFMediaEndedToken;
		LimeME->MediaFailed -= MFMediaFailedToken;
		LimeME->MarkerReached -= MFMarkerReachedToken;
		LimeME->SeekCompleted -= MFSeekCompletedToken;
		LimeME->CurrentStateChanged -= MFCurrentStateChangedToken;
	}
}

void CUX::Controls::MediaElement::SetMediaExtensionParameters()
{
	//디코더 설정
	auto avDecoderConnector = ref new AVDecoderConnector();
	avDecoderConnector->ReqDecoderType = DecoderType;
	avDecoderConnector->UseGPUShader = UseGpuShader;
	if (this->_MediaOpenDataStore.Status == MediaOpenStatus::AudioStreamChanging)
	{
		avDecoderConnector->EnforceAudioStreamId = this->_MediaOpenDataStore.EnforceAudioStreamId;
	}
	//자막 디코더 설정
	auto subtitleConnector = ref new SubtitleDecoderConnector(Dispatcher);
	subtitleConnector->SelectedCodePage = this->CCCodePage;
	subtitleConnector->DefaultCodePage = this->CCDefaultCodePage;
	subtitleConnector->SubtitlePopulatedEvent += ref new SubtitlePopulatedEventHandler(this, &CUX::Controls::MediaElement::OnSubtitlePopulatedEvent);
	//첨부 디코더 설정
	auto attachmentConnector = ref new AttachmentDecoderConnector(Dispatcher);
	attachmentConnector->IsSaveAttachment = this->UseAttachment;
	if (UseAttachment)
	{
		attachmentConnector->AttachmentPopulatedEvent += ref new AttachmentPopulatedEventHandler(this, &CUX::Controls::MediaElement::OnAttachmentPopulatedEvent);
		attachmentConnector->AttachmentCompletedEvent += ref new AttachmentCompletedEventHandler(this, &CUX::Controls::MediaElement::OnAttachmentCompletedEvent);
	}
	//FFmpeg옵션
	PropertySet^ ffmpegProp = ref new PropertySet();

	_CodePage = 0;

	if (_Uri != nullptr && this->Tag != nullptr)
	{
		PropertySet^ ps = dynamic_cast<PropertySet^>(this->Tag);
		if (ps != nullptr)
		{
			if (ps->HasKey("AuthUrl"))
				ffmpegProp->Insert("auth_url", ps->Lookup("AuthUrl"));
			if (ps->HasKey("CodePage"))
			{
				_CodePage = static_cast<int>(ps->Lookup("CodePage"));
				ffmpegProp->Insert("codepage", _CodePage);
			}
		}
	}
	
	_MediaFoundationPropertySet->Insert(AV_DEC_CONN, avDecoderConnector);
	_MediaFoundationPropertySet->Insert(SUB_DEC_CONN, subtitleConnector);
	_MediaFoundationPropertySet->Insert(ATC_DEC_CONN, attachmentConnector);
	_MediaFoundationPropertySet->Insert(FFMPEG_OPTION, ffmpegProp);
}

void CUX::Controls::MediaElement::OpenMedia()
{
	//기본 이벤트 삭제
	DetatchMediaElementEvents();
	//커넥터 설정
	SetMediaExtensionParameters();
	//상태 유지 플래그 전달
	MediaTransportControls->IsKeepOpenState = _IsKeepMediaControlPanelOpenState;
	//상태 유지 플래그 초기화
	_IsKeepMediaControlPanelOpenState = false;
	//기본 이벤트 등록
	AttachMediaElementEvents();
	//소스 설정
	if (UseLimeEngine)
	{
		//소스 설정
		if (_Stream != nullptr)
			LimeME->SetSource(_Stream, _MimeType);
		else if (_Uri != nullptr)
		{
			if (DecoderType == DecoderTypes::HW)
				LimeME->Source = _Uri;
			else
				LimeME->Source = ref new Uri("http://127.0.0.1");
		}
	}
	else
	{
		//소스 설정
		if (_Stream != nullptr)
			WindowsME->SetSource(_Stream, _MimeType);
		else if (_Uri != nullptr)
		{
			if (DecoderType == DecoderTypes::HW)
				WindowsME->Source = _Uri;
			else
				WindowsME->Source = ref new Uri("http://127.0.0.1");
		}
	}
		
	if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile"
		|| UIViewSettings::GetForCurrentView()->UserInteractionMode == Windows::UI::ViewManagement::UserInteractionMode::Touch)
	{
		//모바일의 경우 기본 FullScreen
		IsFullScreen = true;
		//아이콘 모양 강제 변경
		VisualStateManager::GoToState(this->MediaTransportControls, "FullWindowState", false);
	}
	else 
	{
		auto view = ApplicationView::GetForCurrentView();
		IsFullScreen = view->IsFullScreenMode;
	}
}

void CUX::Controls::MediaElement::ClearMarkers()
{
	this->Markers->Clear();
	if (this->ClosedCaptions != nullptr)
	{
		this->ClosedCaptions->ClearTextClosedCaption();
		this->ClosedCaptions->ClearImageClosedCaption();
	}
}

void CUX::Controls::MediaElement::SelectLastClosedCaptionIndex(int prevClosedCaptionSize)
{
	auto currSize = MediaTransportControls->ClosedCaptionSource->Size;
	if (currSize > 0 && currSize != prevClosedCaptionSize)
	{
		//존재하는 리스트에 추가하는 경우
		MediaTransportControls->ClosedCaptionIndex = currSize - 1;
	}
}

void CUX::Controls::MediaElement::AddClosedCaptionStreamSource(IRandomAccessStream^ stream)
{
	if (MediaTransportControls != nullptr)
	{
		auto prevSize = MediaTransportControls->ClosedCaptionSource->Size;
		auto mi = CCPlayer::UWP::Factory::MediaInformationFactory::CreateMediaInformationFromStream(stream);
		AddClosedCaptions(mi, stream, nullptr);
		SelectLastClosedCaptionIndex(prevSize);
	}
}

void CUX::Controls::MediaElement::AddClosedCaptionStreamSources(Windows::Foundation::Collections::IVector<IRandomAccessStream^>^ streamList)
{
	if (MediaTransportControls != nullptr && streamList != nullptr)
	{
		auto prevSize = MediaTransportControls->ClosedCaptionSource->Size;
		for (unsigned int i = 0; i < streamList->Size; i++)
		{
			IRandomAccessStream^ stream = streamList->GetAt(i);
			auto prevSize = MediaTransportControls->ClosedCaptionSource->Size;
			auto mi = CCPlayer::UWP::Factory::MediaInformationFactory::CreateMediaInformationFromStream(stream);
			AddClosedCaptions(mi, stream, nullptr);
		}
		SelectLastClosedCaptionIndex(prevSize);
	}
}

void CUX::Controls::MediaElement::AddClosedCaptionUriSource(String^ uri, int codePage)
{
	if (MediaTransportControls != nullptr)
	{
		//FFmpeg옵션
		PropertySet^ ffmpegProp = ref new PropertySet();
		ffmpegProp->Insert("codepage", codePage);

		auto prevSize = MediaTransportControls->ClosedCaptionSource->Size;
		auto mi = CCPlayer::UWP::Factory::MediaInformationFactory::CreateMediaInformationFromUri(uri, ffmpegProp);
		AddClosedCaptions(mi, uri, ffmpegProp);
		SelectLastClosedCaptionIndex(prevSize);
	}
}

void CUX::Controls::MediaElement::AddClosedCaptionUriSources(Windows::Foundation::Collections::IVector<String^>^ uriList, int codePage)
{
	if (MediaTransportControls != nullptr)
	{
		auto prevSize = MediaTransportControls->ClosedCaptionSource->Size;
		for (unsigned int i = 0; i < uriList->Size; i++)
		{
			//FFmpeg옵션
			PropertySet^ ffmpegProp = ref new PropertySet();
			ffmpegProp->Insert("codepage", codePage);

			String^ uri = uriList->GetAt(i);
			auto mi = CCPlayer::UWP::Factory::MediaInformationFactory::CreateMediaInformationFromUri(uri, ffmpegProp);
			AddClosedCaptions(mi, uri, ffmpegProp);
		}
		SelectLastClosedCaptionIndex(prevSize);
	}
}

void CUX::Controls::MediaElement::AddClosedCaptions(IMediaInformation^ mediaInformation, Object^ param, PropertySet^ properties)
{
	if (MediaTransportControls != nullptr && mediaInformation != nullptr && mediaInformation->CodecInformationList != nullptr)
	{
		for (uint32 i = 0; i < mediaInformation->CodecInformationList->Size; i++)
		{
			auto ci = mediaInformation->CodecInformationList->GetAt(i);
			String^ dispName = GetCodecDisplayName(ci);
			auto kn = ref new KeyName(param, dispName, ci);

			kn->Payload2 = properties;
			MediaTransportControls->AppendClosedCaption(kn);
		}

		//자막선택 메뉴 활성/비활성화
		VisualStateManager::GoToState(MediaTransportControls, MediaTransportControls->ClosedCaptionSource->Size > 0 ? "CCSelectionAvailable" : "CCSelectionUnavailable", false);
	}
}

String^ CUX::Controls::MediaElement::GetCodecDisplayName(CodecInformation^ codecInfo)
{
	String^ fullName = codecInfo->Title;

	if (fullName != nullptr)
	{
		std::wstring tmp(fullName->Data());
		tmp = std::tr1::regex_replace(tmp, wregex(std::wstring(L"([\\s]*Subtitle[s]*[\\s]*)"), regex_constants::ECMAScript | regex_constants::icase), L"");
		fullName = ref new String(tmp.c_str());
	}

	if (fullName == nullptr || codecInfo->CodecId == AV_CODEC_ID_XSUB)
	{
		//fullName = codecInfo->CodecLongName;
		fullName = codecInfo->CodecName + L" [" + codecInfo->StreamId + "]";
	}

	if (codecInfo->Language != nullptr)
	{
		String^ twoLetterCode = Helpers::LanguageCodeHelper::GetTwoLetterCode(codecInfo->Language);

		if (twoLetterCode != nullptr && twoLetterCode->Length() == 2)
		{
			auto lang = ref new Windows::Globalization::Language(twoLetterCode);

			std::wstring tmp(fullName->Data());
			tmp = std::tr1::regex_replace(tmp, wregex(std::wstring(L"[\\s]*(").append(lang->NativeName->Data()).append(L"|").append(lang->DisplayName->Data()).append(L"|Subtitle[s]*)[\\s]*"), regex_constants::ECMAScript | regex_constants::icase), L"");
			fullName = lang->DisplayName + " " + ref new String(tmp.c_str());
		}
	}

	return fullName;
}

String^ CUX::Controls::MediaElement::GetAudioStreamDisplayName(String^ langCode)
{
	String^ fullName = nullptr;
	auto twoLetterCode = Helpers::LanguageCodeHelper::GetTwoLetterCode(langCode);

	if (twoLetterCode != nullptr && twoLetterCode->Length() == 2)
	{
		auto lang = ref new Windows::Globalization::Language(twoLetterCode);
		fullName = lang->DisplayName;
	}
	else
	{
		fullName = langCode;
	}
	return fullName;
}

String^ CUX::Controls::MediaElement::GetAudioStreamDisplayName(CodecInformation^ codecInfo)
{
	auto langCode = codecInfo->Language;
	auto fullName = codecInfo->Title;
	auto channels = codecInfo->Channels;
	auto twoLetterCode = Helpers::LanguageCodeHelper::GetTwoLetterCode(langCode);

	if (twoLetterCode != nullptr && twoLetterCode->Length() == 2)
	{
		auto lang = ref new Windows::Globalization::Language(twoLetterCode);

		if (fullName != nullptr)
		{
			auto englishName = Lime::CPPHelper::LanguageHelper::GetEnglishName(twoLetterCode);
			if (englishName != nullptr)
			{
				std::wstring fname(fullName->Data());
				std::wstring ename(englishName->Data());
				std::wstring dname(lang->DisplayName->Data());

				int offset = 0;
				if ((offset = fname.find(ename, 0)) != std::string::npos)
				{
					fname.replace(offset, ename.length(), dname);
					fullName = ref new String(fname.c_str());
				}
				else if ((offset = fname.find(dname, 0)) != std::string::npos)
				{
					fullName = ref new String(fname.c_str());
				}
				else
				{
					fullName = lang->DisplayName + " : " + fullName;
				}
			}
			else
			{
				fullName = lang->DisplayName + " : " + fullName;
			}
		}
		else
		{
			auto codecName = codecInfo->CodecName;
			fullName = lang->DisplayName + " : " + codecName + " " + channels + "Ch";
		}
	}
	else if (fullName == nullptr || fullName->IsEmpty())
	{
		//fullName = "Unknown";
		auto codecName = codecInfo->CodecName;
		fullName = langCode + " : " + codecName + " " + channels + "Ch";
		
	}
	return fullName;
}

void CUX::Controls::MediaElement::OnSubtitlePopulatedEvent(ISubtitleDecoderConnector ^sender, WUX::Media::TimelineMarker ^timelineMarker, Windows::Foundation::Collections::IMap<String^, ImageData^>^ subtitleImageMap)
{
	if (this->ClosedCaptions != nullptr)
	{
		auto it = std::find_if(begin(Markers), end(Markers), [=](TimelineMarker^ tlmarker)
		{
			return tlmarker->Time.Duration == timelineMarker->Time.Duration;
		});

		if (it != end(Markers))
		{
			//동일한 시작 시간이 존재 한다면 병합한다.
			auto marker = (TimelineMarker^)*it;
			marker->Text = this->ClosedCaptions->MergeTimelineMarkerText(marker->Text, timelineMarker->Text);
		}
		else
		{
			//추가
			Markers->Append(timelineMarker);
		}

		//이미지자막 데이터 추가
		this->ClosedCaptions->AppendImageSubtitles(subtitleImageMap);
	}
	//OutputDebugMessage(L"MediaElement => OnSubtitlePopulatedEvent()\n");
}

void CUX::Controls::MediaElement::OnAttachmentPopulatedEvent(CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ sender, CCPlayer::UWP::Common::Codec::AttachmentData^ attachment)
{
	AttachmentPopulated(sender, attachment);
}

void CUX::Controls::MediaElement::OnAttachmentCompletedEvent(CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ sender, Platform::Object^ args)
{
	AttachmentCompleted(sender, args);
}

void CUX::Controls::MediaElement::OnMarkerReached(Platform::Object ^sender, WUX::Media::TimelineMarkerRoutedEventArgs ^e)
{
	//OutputDebugMessage(L"MediaElement => OnMarkerReached()\n");
	if (this->ClosedCaptions != nullptr)
	{
		this->ClosedCaptions->SetTimelineMarkerText(e->Marker->Text);

		//현재 출력한 마커를 검색
		auto it = std::find_if(begin(this->Markers), end(this->Markers), [=](TimelineMarker^ tlmarker)
		{
			return tlmarker->Time.Duration == e->Marker->Time.Duration;
		});
		//현재 마커 및 지난마커 모두 삭제
		if (it != end(this->Markers))
		{
			int index = std::distance(begin(this->Markers), it);
			for (int i = index; i >= 0; i--)
			{
				this->Markers->RemoveAt(i);
			}
		}
	}

	MarkerReached(this, e);
}

void CUX::Controls::MediaElement::OnMediaOpened(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	auto openStatus = _MediaOpenDataStore.Status;
	//줌값 초기화
	this->ZoomInOut = 1.0;
	//탐색주기 설정
	SetSeekTimeInterval(NaturalDuration.TimeSpan);
	//New Source의 기본 오디오 스트림
	//int DefaultAudioStreamLanguageIndex = -1;
	//재생 패널 설정
	MediaOpened(this, e);

	if (MediaTransportControls != nullptr)
	{
		auto avDecoderConnector = dynamic_cast<IAVDecoderConnector^>(_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
		
		//새 미디어파일 오픈 처리 (신규 소스라도 사용 못하는 디코더로 시도시 자동적으로 디코더가 변경되서 재시도됨)
		if (openStatus == MediaOpenStatus::NewSource || (MediaTransportControls->AudioStreamLanguageSource->Size == 0 && this->AudioStreamCount > 0))
		{
			Windows::Data::Json::JsonObject^ streamDescription = nullptr;
			String^ lang = nullptr;
			//오디오 스트림 추가
			Platform::Collections::Vector<KeyName^>^ mfAudioStreamList = ref new Platform::Collections::Vector<KeyName^>();
			Platform::Collections::Vector<KeyName^>^ ffmpegAudioStreamList = ref new Platform::Collections::Vector<KeyName^>();
			//먼저 MF에서 오디오 스트림을 구해서 임시 리스트를 생성 (FFmpegSource를 거치지 않은 경우)
			for (int i = 0; i < this->AudioStreamCount; i++)
			{
				lang = this->GetAudioStreamLanguage(i);
				//HW디코더의 경우이며, FFmpegSourc를 거치지 않은 경우도 존재할 수 있음 (모든 ContentType과 확장자를 등록할 수 없기 때문)
				if (!Windows::Data::Json::JsonObject::TryParse(lang, &streamDescription))
				{
					auto fullName = GetAudioStreamDisplayName(lang);
					mfAudioStreamList->Append(ref new KeyName(i, fullName));
				}
			}
			
			int internalSubtitleCount = 0;
			int audioStreamIndex = 0;
			for (uint32 i = 0; i < avDecoderConnector->CodecInformationList->Size; i++)
			{
				auto ci = avDecoderConnector->CodecInformationList->GetAt(i);
				if (ci->CodecType == 1) //오디오코덱 타입 => AVMEDIA_TYPE_AUDIO
				{
					//FFmpegSource를 거친 오디오 리스트 로드
					auto fullName = GetAudioStreamDisplayName(ci);
					//오디오 랭귀지 추가
					ffmpegAudioStreamList->Append(ref new KeyName(audioStreamIndex++, fullName, ci));
				}
				else if (ci->CodecType == 3) //자막코덱 타입 => AVMEDIA_TYPE_SUBTITLE
				{
					//내부자막 추가
					String^ fullName = GetCodecDisplayName(ci);
					MediaTransportControls->AppendClosedCaption(ref new KeyName((int)ci->StreamId, fullName, ci));
					internalSubtitleCount++;
				}
			}

			int bestAudioStreamIndex = -1;
			if (ffmpegAudioStreamList->Size > 0)
			{
				//오디오 스트림을 리스트에 추가
				for (int i = 0; i < ffmpegAudioStreamList->Size; i++)
				{
					auto keyName = ffmpegAudioStreamList->GetAt(i);
					auto ci = dynamic_cast<CodecInformation^>(keyName->Payload);
					
					MediaTransportControls->AppendAudioStream(keyName);
					if (ci != nullptr)
					{
						if (ci->IsBestStream)
						{
							bestAudioStreamIndex = i;
						}
					}
				}
			}
			else
			{
				//FFmpegSource를 거치지 않은 경우의 오디오 스트림을 리스트에 추가
				for (unsigned int i = 0; i < mfAudioStreamList->Size; i++)
				{
					auto keyName = mfAudioStreamList->GetAt(i);
					MediaTransportControls->AppendAudioStream(keyName);
				}
			}

			//기본 자막 선택
			if (MediaTransportControls->ClosedCaptionIndex == -1 && MediaTransportControls->ClosedCaptionSource->Size > 0)
			{
				if (internalSubtitleCount == MediaTransportControls->ClosedCaptionSource->Size)
				{
					//현재 내부 자막만 추가되어 있는 상태임
					for (unsigned int i = 0; i < MediaTransportControls->ClosedCaptionSource->Size; i++)
					{
						auto keyName = MediaTransportControls->ClosedCaptionSource->GetAt(i);

						auto ci = dynamic_cast<CodecInformation^>(keyName->Payload);
						if (ci->IsBestStream)
						{
							MediaTransportControls->ClosedCaptionIndex = i;
							break;
						}
					}
				}
				else
				{
					//외부자막도 추가된 상태라면 
					for (unsigned int i = 0; i < MediaTransportControls->ClosedCaptionSource->Size; i++)
					{
						auto keyName = MediaTransportControls->ClosedCaptionSource->GetAt(i);
						Box<int>^ param = dynamic_cast<Box<int>^>(keyName->Key);

						if (internalSubtitleCount == MediaTransportControls->ClosedCaptionSource->Size)
						{
							//내부자막만 존재하는 상태면 
							auto ci = dynamic_cast<CodecInformation^>(keyName->Payload);
							if (ci->IsBestStream)
							{
								//베스트 스트림을 기본 자막으로 설정
								MediaTransportControls->ClosedCaptionIndex = i;
								break;
							}
						}
						else
						{
							//외부자막도 존재
							if (param == nullptr)
							{
								//외부자막이 존재하는 상태이면, 첫번째 외부자막을 선택
								MediaTransportControls->ClosedCaptionIndex = i;
								break;
							}
						}
					}
				}
			}

			//오디오 스트림 선택
			int audioStreamLangIdx = bestAudioStreamIndex != -1 ? bestAudioStreamIndex : 0;
			int audioStreamLangCnt = (int)MediaTransportControls->AudioStreamLanguageSource->Size;
			this->AudioStreamLanguageIndex = audioStreamLangCnt > audioStreamLangIdx ? audioStreamLangIdx : audioStreamLangCnt > 0 ? 0 : -1;
			//DefaultAudioStreamLanguageIndex = audioStreamLangCnt > audioStreamLangIdx ? audioStreamLangIdx : audioStreamLangCnt > 0 ? 0 : -1;
		}
		else if (openStatus == MediaOpenStatus::DecoderChanging)
		{
			this->Position = _MediaOpenDataStore.Position;
			this->AudioStreamLanguageIndex = _MediaOpenDataStore.AudioStreamLanguageIndex;
			this->VolumeBoost = _MediaOpenDataStore.AudioVolumeBoost;
			MediaTransportControls->ClosedCaptionIndex = _MediaOpenDataStore.ClosedCaptionIndex;
		}
		else if (openStatus == MediaOpenStatus::AudioStreamChanging)
		{
			KeyName^ audioLangComboItem = nullptr;
			Object^ payload = nullptr;
			CodecInformation^ codecInfo = nullptr;

			for (int i = 0; i < MediaTransportControls->AudioStreamLanguageSource->Size; i++) 
			{
				audioLangComboItem = MediaTransportControls->AudioStreamLanguageSource->GetAt(i);
				payload = audioLangComboItem->Payload;
				codecInfo = dynamic_cast<CodecInformation^>(payload);

				for (int j = 0; j < avDecoderConnector->CodecInformationList->Size; j++)
				{
					auto newCi = avDecoderConnector->CodecInformationList->GetAt(j);
					if (codecInfo->StreamId == newCi->StreamId)
					{
						//코덱 정보 교체
						audioLangComboItem->Payload = newCi;
						break;
					}
				}
			}

			this->Position = _MediaOpenDataStore.Position;
			this->AudioStreamLanguageIndex = _MediaOpenDataStore.AudioStreamLanguageIndex;
			this->VolumeBoost = _MediaOpenDataStore.AudioVolumeBoost;
			this->MediaTransportControls->ClosedCaptionIndex = _MediaOpenDataStore.ClosedCaptionIndex;
		}

		//재생 패널 UI에 적용
		MediaTransportControls->OnMediaOpened(avDecoderConnector);
		//오픈상태가 변경이 되지 않았다면 초기화 (최초 오디오 베스트 스트림이 기본 스트림이 아니어서, MediaOpen을 호출한 경우는 MediaOpenStatus::AudioStreamChanging로 변경 되므로
		//다시 여기에 들어올때를 위해 상태를 초기화 하면 안된다. 
		if (openStatus == _MediaOpenDataStore.Status)
		{
			//오픈 상태 초기화
			_MediaOpenDataStore.Status = MediaOpenStatus::None;
		}
		//에러코드 초기화
		_MediaErrorCode = 0;
	}

	if (this->ClosedCaptions != nullptr)
	{
		this->ClosedCaptions->BaseFontSize = BASE_FONT_SIZE;
		this->ClosedCaptions->NaturalVideoSize = Size((float)this->NaturalVideoWidth, (float)this->NaturalVideoHeight);
	}
	
	//이벤트 버블링
	
	//신규 소스의 경우 음성 언어를 여기서 설정
	//멀티플 오디오의 경우, 오디오 디코더에서 OnCheckOutputType()에서 소스가 Match되지 않는 현상이 
	//생기는데, 이로 인해 스트림 위치가 초기화 되는 버그가 있음 
	//=> 중간 재생지점 부터 다시 재생을 시작 하지만 위의 버그로 인해 다시 0의 위치로 시크 됨 
	//이를 방지 하기 위해서 오디오 스트림 설정 위치를 해당 오류 다음으로 이동 시켜 처리
	/*if (DefaultAudioStreamLanguageIndex != -1)
		this->AudioStreamLanguageIndex = DefaultAudioStreamLanguageIndex;*/
}

void CUX::Controls::MediaElement::OnMediaEnded(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	MediaTransportControls->OnMediaEnded();
	MediaEnded(this, e);

	if (NextMediaItem != nullptr)
	{
		NextMediaButtonTapped(sender, ref new TappedRoutedEventArgs());
		_IsKeepMediaControlPanelOpenState = true;
	}
	else
	{
		//VisualStateManager::GoToState(this, "MediaClosedBackground", false);
		CloseButtonTapped(sender, ref new TappedRoutedEventArgs());
		_IsKeepMediaControlPanelOpenState = false;
	}
}

void CUX::Controls::MediaElement::OnMFMediaFailed(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	//재생 패널 통지
	auto avDecoderConnector = dynamic_cast<IAVDecoderConnector^>(_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
	MediaTransportControls->OnMediaFailed(MediaErrorCode, avDecoderConnector);
	MediaFailed(this, e);
	//VisualStateManager::GoToState(this, "MediaClosedBackground", false);
}

void CUX::Controls::MediaElement::OnMediaFailed(Platform::Object ^sender, WUX::ExceptionRoutedEventArgs ^e)
{
	int errCode = 0;
	if (e->GetType()->FullName == WUX::ExceptionRoutedEventArgs::typeid->FullName)
	{
		auto arg = dynamic_cast<WUX::ExceptionRoutedEventArgs^>(e);
		if (arg != nullptr)
		{
			std::wstring errMsg(arg->ErrorMessage->Data());
			if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_NOERROR", 0) != string::npos) errCode = 0;
			else if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_ABORTED", 0) != string::npos) errCode = 1;
			else if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_NETWORK", 0) != string::npos) errCode = 2;
			else if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_DECODE", 0) != string::npos) errCode = 3;
			else if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED", 0) != string::npos) errCode = 4;
			else if (errMsg.find_first_of(L"MF_MEDIA_ENGINE_ERR_ENCRYPTED", 0) != string::npos) errCode = 5;
		}
	}
	_MediaErrorCode = errCode;
	//재생 패널 통지
	auto avDecoderConnector = dynamic_cast<IAVDecoderConnector^>(_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
	MediaTransportControls->OnMediaFailed(MediaErrorCode, avDecoderConnector);
	MediaFailed(this, e);
	//VisualStateManager::GoToState(this, "MediaClosedBackground", false);
}

void CUX::Controls::MediaElement::OnCurrentStateChanged(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	MediaElementState state = MediaElementState::Closed;

	if ((UseLimeEngine && LimeME != nullptr) 
		|| (!UseLimeEngine && WindowsME != nullptr))
	{
		state = this->CurrentState;
	}

	if (this->MediaTransportControls != nullptr)
	{
		//자막컨트롤 상태 전달
		if (this->ClosedCaptions != nullptr && this->MediaTransportControls->ClosedCaptionSource->Size > 0)
		{
			if (state == MediaElementState::Playing)
			{
				this->ClosedCaptions->DoStart();
			}
			else
			{
				if (state == MediaElementState::Stopped)
				{
					this->ClosedCaptions->ClearTextClosedCaption();
					this->ClosedCaptions->ClearImageClosedCaption();
				}
				this->ClosedCaptions->DoStop();
			}
		}

		//재생 패널 상태 전달
		switch (state)
		{
		case MediaElementState::Playing:
			this->MediaTransportControls->OnPlaying();
			break;
		case MediaElementState::Paused:
			this->MediaTransportControls->OnPaused();
			break;
		case MediaElementState::Buffering:
			this->MediaTransportControls->OnBuffering();
			break;
		case MediaElementState::Opening:
			this->MediaTransportControls->OnOpening();
			break;
		case MediaElementState::Stopped:
			this->ClosedCaptions->DoStop();
			this->MediaTransportControls->OnStopped();
			break;
		case MediaElementState::Closed:
			this->ClosedCaptions->DoStop();
			this->MediaTransportControls->OnClosed();
			break;
		}
	}

	CurrentStateChanged(this, e);
}

void CUX::Controls::MediaElement::OnSeekCompleted(Platform::Object ^sender, WUX::RoutedEventArgs ^e)
{
	//이전 마커 전체 삭제
	ClearMarkers();
	//재생 패널 통지
	MediaTransportControls->OnSeekCompleted();
	SeekCompleted(this, e);
}

void CUX::Controls::MediaElement::Trim()
{
	if (CheckLimeEngine())
	{
		LimeME->DXGIDeviceTrim();
	}
}

int CUX::Controls::MediaElement::MediaErrorCode::get()
{
	if (CheckLimeEngine())
	{
		_MediaErrorCode = LimeME->GetMediaErrorCode();
	}
	return _MediaErrorCode;
}

void CUX::Controls::MediaElement::SetSeekTimeInterval(TimeSpan runningTime)
{
	int seekTime = SeekTimeInterval;

	if (seekTime == 0)
	{
		long long totalSeconds = runningTime.Duration / 10000000L;

		// Calculate the slider step frequency based on the timespan length
		if (totalSeconds >= 3600) //60분
		{
			seekTime = 60;
		}
		else if (totalSeconds > 1800) //30분
		{
			seekTime = 30;
		}
		else if (totalSeconds > 900) //15분
		{
			seekTime = 15;
		}
		else if (totalSeconds > 300) //5분
		{
			seekTime = 5;
		}
		else
		{
			seekTime = 3;
		}
	}
	
	SeekTimeIntervalValue = seekTime;
}

void CUX::Controls::MediaElement::Seek(long long nanoSeconds)
{
	if (!CanSeek)
	{
		return;
	}
	long long pos = Position.Duration + nanoSeconds;

	if (pos < 0)
	{
		pos = 0;
	}
	else if (pos >= NaturalDuration.TimeSpan.Duration)
	{
		pos = NaturalDuration.TimeSpan.Duration;
	}

	TimeSpan newPos;
	newPos.Duration = pos;
	Position = newPos;
}

/**
컨트롤 이벤트 처리기
**/
void CUX::Controls::MediaElement::OnCloseButtonTapped(Platform::Object ^sender, WUX::Input::TappedRoutedEventArgs ^e)
{
	IsFullScreen = false;
	ClearMarkers();
	CloseButtonTapped(sender, e);
}

void CUX::Controls::MediaElement::OnCCSettingsOpenButtonTapped(Platform::Object ^sender, WUX::Input::TappedRoutedEventArgs ^e)
{
	CCSettingsOpenButtonTapped(sender, e);
}

void CUX::Controls::MediaElement::OnPreviousMediaButtonTapped(Platform::Object ^sender, WUX::Input::TappedRoutedEventArgs ^e)
{
	ClearMarkers();
	PreviousMediaButtonTapped(sender, e);
}

void CUX::Controls::MediaElement::OnNextMediaButtonTapped(Platform::Object ^sender, WUX::Input::TappedRoutedEventArgs ^e)
{
	ClearMarkers();
	NextMediaButtonTapped(sender, e);
}

void CUX::Controls::MediaElement::OnImportClosedCaptionTapped(Platform::Object ^sender, WUX::Input::TappedRoutedEventArgs ^e)
{
	ImportClosedCaptionAsync();
}

void CUX::Controls::MediaElement::SetFilePathInfo(StorageFolder^ currFolder, String^ currFileNameWidthoutExtenion)
{
	_CurrentFolder = currFolder;
	_CurrentFileName = currFileNameWidthoutExtenion;
}

task<void> CUX::Controls::MediaElement::ImportClosedCaptionAsync()
{
	auto picker = ref new Windows::Storage::Pickers::FileOpenPicker();
	picker->SuggestedStartLocation = Windows::Storage::Pickers::PickerLocationId::VideosLibrary;
	picker->ViewMode = Windows::Storage::Pickers::PickerViewMode::List;

	for (uint32 i = 0; i < CUX::Controls::MediaFileSuffixes::CLOSED_CAPTION_SUFFIX->Length; i++)
	{
		picker->FileTypeFilter->Append(CUX::Controls::MediaFileSuffixes::CLOSED_CAPTION_SUFFIX[i]);
	}

	try 
	{
		auto file = co_await picker->PickSingleFileAsync();
		if (file != nullptr)
		{
			//현재 폴더에 대해서 접근권한(재생시작시 설정)이 있고, 현재 파일명 또한 설정이 된 경우 해당 자막을 폴더에 복사	
			if (_CurrentFolder != nullptr && _CurrentFileName != nullptr)
			{
				Windows::Globalization::Calendar^ c = ref new Windows::Globalization::Calendar;
				c->SetToNow();
				String^ name = _CurrentFileName + "_"
					+ c->YearAsPaddedString(4) + c->MonthAsPaddedNumericString(2) + c->DayAsPaddedString(2)
					+ c->PeriodAsString() + c->HourAsPaddedString(2) + c->MinuteAsPaddedString(2) + c->SecondAsPaddedString(2)
					+ "_COPIED_BY_CCPLAYER"
					+ file->FileType;

				co_await file->CopyAsync(_CurrentFolder, name, Windows::Storage::NameCollisionOption::GenerateUniqueName);
			}

			//자막 로드
			auto stream = co_await file->OpenReadAsync();
			if (stream != nullptr)
			{
				this->AddClosedCaptionStreamSource(stream);
			}
		}
	}
	catch (Exception^ e)
	{
		OutputDebugMessage(L"ImportClosedCaptionAsync() method Error : %s", e->Message->Data());
	}
}

void CUX::Controls::MediaElement::OnClosedCaptionMovePositionStarted(Platform::Object ^sender, WUX::RoutedEventArgs^ e)
{
	if (ClosedCaptions != nullptr)
	{
		MediaTransportControls->Visibility = WUX::Visibility::Collapsed;
		ClosedCaptions->UnlockMovePosition();
	}
}

void CUX::Controls::MediaElement::OnClosedCaptionMovePositionCompleted(Platform::Object ^sender, WUX::RoutedEventArgs^ e)
{
	if (MediaTransportControls != nullptr)
	{
		MediaTransportControls->Visibility = WUX::Visibility::Visible;
	}
}

void CUX::Controls::MediaElement::OnPropertyChanged(String^ propertyName)
{
	PropertyChanged(this, ref new WUX::Data::PropertyChangedEventArgs(propertyName));
}

void CUX::Controls::MediaElement::ChangeDecoder(CCPlayer::UWP::Common::Codec::DecoderTypes decoderType)
{
	if (DecoderType != decoderType)
	{
		//디코더 타입변경
		DecoderType = decoderType;
		//상태에 따른 처리
		if (CurrentState == MediaElementState::Stopped || CurrentState == MediaElementState::Closed)
		{
			//Open에러 등의 경우 미디어를 다시 열어 재생 시작
			this->OpenMedia();
		}
		else
		{
			//재생중 디코더를 변경하는 경우
			this->_MediaOpenDataStore.Status = MediaOpenStatus::DecoderChanging;
			this->_MediaOpenDataStore.Position = this->Position;
			this->_MediaOpenDataStore.AudioStreamLanguageIndex = this->AudioStreamLanguageIndex;
			this->_MediaOpenDataStore.ClosedCaptionIndex = this->MediaTransportControls->ClosedCaptionIndex;
			this->_MediaOpenDataStore.AudioVolumeBoost = this->VolumeBoost;
			//오디오 스트림 목록 선택값 초기화 (재선택을 위해서)
			this->AudioStreamLanguageIndex = -1;
			//자막 목록 선택값 초기화 (재선택을 위해서)
			this->MediaTransportControls->ClosedCaptionIndex = -1;
			//재생정지
			this->Stop();
			//미디어를 다시 열어 재생 시작
			this->OpenMedia();
		}
	}
}

/* DependencyProperty 등록 */

//DependencyProperty^ CUX::Controls::MediaElement::_BalanceProperty = DependencyProperty::Register(
//	"Balance", double::typeid, CUX::Controls::MediaElement::typeid,
//	ref new PropertyMetadata(0.0, ref new PropertyChangedCallback(&CUX::Controls::MediaElement::OnBalanceChanged)));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(Balance, double, CUX::Controls::MediaElement, 0.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ZoomInOut, double, CUX::Controls::MediaElement, 1.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ZoomMove, Point, CUX::Controls::MediaElement, Point());
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(Volume, double, CUX::Controls::MediaElement, 1.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(VolumeBoost, double, CUX::Controls::MediaElement, 0.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(AudioSync, double, CUX::Controls::MediaElement, 0.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(AspectRatio, CUX::Controls::AspectRatios, CUX::Controls::MediaElement, CUX::Controls::AspectRatios::Uniform);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(DisplayRotation, CUX::Controls::DisplayRotations, CUX::Controls::MediaElement, CUX::Controls::DisplayRotations::None);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ClosedCaptions, CUX::Controls::ClosedCaptions, CUX::Controls::MediaElement, nullptr);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(UseClosedCaptions, bool, CUX::Controls::MediaElement, true);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(EnabledHorizontalMirror, bool, CUX::Controls::MediaElement, false);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(PreviousMediaItem, CUX::Controls::IMediaItem, CUX::Controls::MediaElement, "");
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(NextMediaItem, CUX::Controls::IMediaItem, CUX::Controls::MediaElement, "");
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(AudioStreamLanguageIndex, int, CUX::Controls::MediaElement, ref new Platform::Box<int>(-1));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(CCCodePage, int, CUX::Controls::MediaElement, ref new Platform::Box<int>(0));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(CCDefaultCodePage, int, CUX::Controls::MediaElement, ref new Platform::Box<int>(0));

DEPENDENCY_PROPERTY_REGISTER(DecoderType, DecoderTypes, CUX::Controls::MediaElement, DecoderTypes::Hybrid);
DEPENDENCY_PROPERTY_REGISTER(EnabledRotationLock, bool, CUX::Controls::MediaElement, false);
DEPENDENCY_PROPERTY_REGISTER(UseFlipToPause, bool, CUX::Controls::MediaElement, false);
DEPENDENCY_PROPERTY_REGISTER(UseGpuShader, bool, CUX::Controls::MediaElement, false);
DEPENDENCY_PROPERTY_REGISTER(UseAttachment, bool, CUX::Controls::MediaElement, false);
DEPENDENCY_PROPERTY_REGISTER(Brightness, double, CUX::Controls::MediaElement, 100.0);
DEPENDENCY_PROPERTY_REGISTER(SeekTimeInterval, int, CUX::Controls::MediaElement, ref new Platform::Box<int>(0));
DEPENDENCY_PROPERTY_REGISTER(Title, String, CUX::Controls::MediaElement, "");
DEPENDENCY_PROPERTY_REGISTER(CCVerticalAlignment, WUX::VerticalAlignment, CUX::Controls::MediaElement, WUX::VerticalAlignment::Bottom);
DEPENDENCY_PROPERTY_REGISTER(CCPosition, double, CUX::Controls::MediaElement, 0.0);

void CUX::Controls::MediaElement::OnBalanceChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		_this->LimeME->Balance = (double)args->NewValue;
	}
	else
	{
		_this->WindowsME->Balance = (double)args->NewValue;
	}
}

Windows::UI::Xaml::Controls::SwapChainPanel^ CUX::Controls::MediaElement::GetZoomPanel()
{
	SwapChainPanel^ swapChainPanel = nullptr;
	for (unsigned int i = 0; i < this->LimeMediaElementPanel->Children->Size; i++)
	{
		swapChainPanel = dynamic_cast<SwapChainPanel^>(this->LimeMediaElementPanel->Children->GetAt(i));
		if (swapChainPanel != nullptr)
			break;
	}
	return swapChainPanel;
}

void CUX::Controls::MediaElement::OnZoomInOutChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		SwapChainPanel^ swapChainPanel = _this->GetZoomPanel();
		if (swapChainPanel == nullptr) return;

		CompositeTransform^ transform = dynamic_cast<CompositeTransform^>(swapChainPanel->RenderTransform);

		auto newValue = (double)args->NewValue;
		auto oldValue = (double)args->OldValue;

		if (newValue != oldValue)
		{
			if (newValue < 1.1)
			{
				transform->TranslateX = 0;
				transform->TranslateY = 0;
			
			}

			//스케일 변경
			transform->ScaleX = newValue;
			transform->ScaleY = newValue;

			//줌모드
			_this->TC->EnabledZoomLockMode(newValue >= 1.01, false);
		}
	}
}

void CUX::Controls::MediaElement::OnZoomMoveChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		SwapChainPanel^ swapChainPanel = _this->GetZoomPanel();
		if (swapChainPanel == nullptr) return;

		auto transform = dynamic_cast<CompositeTransform^>(swapChainPanel->RenderTransform);
		auto value = (Point)args->NewValue;
	
		if (_this->ZoomInOut < 1.1)
		{
			transform->TranslateX = 0;
			transform->TranslateY = 0;
			return;
		}

		auto r = _this->NaturalVideoWidth / _this->ActualWidth;
		auto w = _this->NaturalVideoWidth / r;
		auto h = _this->NaturalVideoHeight / r;
		auto y = (_this->ActualHeight - h) / 2;

		//대비 영역
		GeneralTransform ^ tr = swapChainPanel->TransformToVisual(_this);
		//Rect rect = tr->TransformBoundsCore(Rect(0, 0, _this->ActualWidth, _this->ActualHeight));
		Rect rect = tr->TransformBounds(Rect(0, y, w, h));
		//OutputDebugMessage(L"TR(%f, %f), WxH(%f, %f), RECT(%f, %f, %f, %f) \n", swapChainPanel->ActualWidth, swapChainPanel->ActualHeight, (float)_this->ActualWidth, (float)_this->ActualHeight, rect.X, rect.Y, rect.Width, rect.Height);

		//이동가능한 범위
		auto rangeX = abs((rect.Width - _this->ActualWidth) / 2) * -1;
		auto rangeY = abs((rect.Height - _this->ActualHeight) / 2) * -1;
		//이동
		if (value.X <= 0) //우측
			transform->TranslateX = max(transform->TranslateX + value.X, rangeX);
		else if (value.X > 0) //우측
			transform->TranslateX = min(transform->TranslateX + value.X, rangeX * -1);
		if (value.Y <= 0) //상측
			transform->TranslateY = max(transform->TranslateY + value.Y, rangeY);
		else if (value.Y > 0) //하측
			transform->TranslateY = min(transform->TranslateY + value.Y, rangeY * -1);
	}
}

void CUX::Controls::MediaElement::OnVolumeChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		_this->LimeME->Volume = (double)args->NewValue;
	}
	else
	{
		_this->WindowsME->Volume = (double)args->NewValue;
	}
}

void CUX::Controls::MediaElement::OnVolumeBoostChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	//음량 부스터 설정
	if (_this->_MediaFoundationPropertySet->HasKey(AV_DEC_CONN))
	{
		auto decoder = dynamic_cast<IAVDecoderConnector^>(_this->_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
		if (decoder->AudioVolumeBoost != (double)args->NewValue)
		{
			decoder->AudioVolumeBoost = (double)args->NewValue;
		}
	}
}

void CUX::Controls::MediaElement::OnAudioSyncChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	//음성 싱크값(밀리초) 설정
	if (_this->_MediaFoundationPropertySet->HasKey(AV_DEC_CONN))
	{
		auto decoder = dynamic_cast<IAVDecoderConnector^>(_this->_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
		auto newVal = (long long)((double)args->NewValue * 1000);
		if (decoder->AudioSyncMilliSeconds != newVal)
		{
			decoder->AudioSyncMilliSeconds = newVal;
		}
	}
}

void CUX::Controls::MediaElement::OnAspectRatioChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto value = static_cast<CUX::Controls::AspectRatios>(args->NewValue);
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		_this->LimeME->AspectRatio = value;
	}
	else
	{
		switch (value)
		{
		case CUX::Controls::AspectRatios::UniformToFill:
			_this->WindowsME->Stretch = WUX::Media::Stretch::UniformToFill;
			break;
		case CUX::Controls::AspectRatios::Uniform:
			_this->WindowsME->Stretch = WUX::Media::Stretch::Uniform;
			break;
		case CUX::Controls::AspectRatios::Fill:
			_this->WindowsME->Stretch = WUX::Media::Stretch::Fill;
			break;
		default:
			_this->WindowsME->Stretch = WUX::Media::Stretch::None;
			break;
		}
	}
}

void CUX::Controls::MediaElement::OnDisplayRotationChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		auto value = static_cast<CUX::Controls::DisplayRotations>(args->NewValue);
		_this->LimeME->DisplayRotation = value;
	}
}

void CUX::Controls::MediaElement::OnClosedCaptionsChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	CUX::Controls::ClosedCaptions^ old = dynamic_cast<CUX::Controls::ClosedCaptions^>(args->OldValue);
	CUX::Controls::ClosedCaptions^ value = dynamic_cast<CUX::Controls::ClosedCaptions^>(args->NewValue);

	if (value != old && _this->CCPopupPanel->Children->Size > 0)
	{
		_this->CCPopupPanel->Children->Clear();
	}
	_this->CCPopupPanel->Children->InsertAt(0, value);
	value->MoveClosedCaptionPositionCompleted += ref new WUX::RoutedEventHandler(_this, &CUX::Controls::MediaElement::OnClosedCaptionMovePositionCompleted);
}

void CUX::Controls::MediaElement::OnUseClosedCaptionsChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->ClosedCaptions != nullptr)
	{
		_this->ClosedCaptions->Visibility = static_cast<bool>(args->NewValue) ? WUX::Visibility::Visible : WUX::Visibility::Collapsed;
	}
	if (_this->MediaTransportControls != nullptr)
	{
		VisualStateManager::GoToState(_this->MediaTransportControls, static_cast<bool>(args->NewValue) ? "ClosedCaptionOnState" : "ClosedCaptionOffState", false);
	}
}

void CUX::Controls::MediaElement::OnEnabledHorizontalMirrorChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	if (_this->CheckLimeEngine())
	{
		_this->LimeME->EnableHorizontalMirrorMode(static_cast<bool>(args->NewValue));
	}
}

void CUX::Controls::MediaElement::OnPreviousMediaItemChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	//컨트롤 패널의 이전 비디오 버튼 활성/비활성화
	String^ state = "PreviousMediaButtonUnavailable";
	VisualStateManager::GoToState(_this->MediaTransportControls, _this->PreviousMediaItem != nullptr ? "PreviousMediaButtonAvailable" : "PreviousMediaButtonUnavailable", false);
}

void CUX::Controls::MediaElement::OnNextMediaItemChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	//컨트롤 패널의 다음 비디오 버튼 활성/비활성화
	VisualStateManager::GoToState(_this->MediaTransportControls, _this->NextMediaItem != nullptr ? "NextMediaButtonAvailable" : "NextMediaButtonUnavailable", false);
}

void CUX::Controls::MediaElement::OnAudioStreamLanguageIndexChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	VisualStateManager::GoToState(_this->MediaTransportControls, "AudioBoosterUnavailable", false);

	int newIndex = (int)args->NewValue;
	if (newIndex > -1)
	{
		bool isBasicStream = true;
		int streamId = -1;
		if (_this->MediaTransportControls->AudioStreamLanguageSource->Size > 0)
		{
			auto payload = _this->MediaTransportControls->AudioStreamLanguageSource->GetAt(newIndex)->Payload;
			if (payload != nullptr)
			{
				auto ci = dynamic_cast<CodecInformation^>(payload);
				if (ci != nullptr)
				{
					//부스터 초기화
					_this->VolumeBoost = 0;
					//기본 스트림 여부
					isBasicStream = ci->IsBasicStream;
					streamId = ci->StreamId;

					DecoderTypes resDecoderType = DecoderTypes::HW;
					if (_this->_MediaFoundationPropertySet->HasKey(AV_DEC_CONN))
					{
						auto decoder = dynamic_cast<IAVDecoderConnector^>(_this->_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
						resDecoderType = decoder->ResDecoderType;
					}

					if (resDecoderType != DecoderTypes::HW && ci->DecoderType == DecoderTypes::SW)
					{
						if (ci->Channels > 2 && _this->VolumeBoost < 7.0)
						{
							_this->VolumeBoost = 7.0;
						}
						VisualStateManager::GoToState(_this->MediaTransportControls, "AudioBoosterAvailable", false);
					}
				}
			}
		}

		if (isBasicStream)
		{
			int bsIndex = -1;
			for (unsigned int i = 0; i < _this->MediaTransportControls->AudioStreamLanguageSource->Size; i++)
			{
				auto payload = _this->MediaTransportControls->AudioStreamLanguageSource->GetAt(i)->Payload;
				auto ci = dynamic_cast<CodecInformation^>(payload);

				if (ci != nullptr)
				{
					if (ci->IsBasicStream)
					{
						bsIndex++;
					}

					if (ci->StreamId == streamId)
					{
						break;
					}
				}
			}

			_this->AudioStreamIndex = bsIndex != -1 ? bsIndex : newIndex;
			if (_this->_MediaFoundationPropertySet->HasKey(AV_DEC_CONN))
			{
				auto decoder = dynamic_cast<IAVDecoderConnector^>(_this->_MediaFoundationPropertySet->Lookup(AV_DEC_CONN));
				decoder->EnforceAudioStreamId = -1;
			}
		}
		else if (streamId != -1)
		{
			//이전 데이터 저장
			_this->_MediaOpenDataStore.Status = MediaOpenStatus::AudioStreamChanging;
			_this->_MediaOpenDataStore.Position = _this->Position;
			_this->_MediaOpenDataStore.AudioStreamLanguageIndex = _this->AudioStreamLanguageIndex;
			_this->_MediaOpenDataStore.ClosedCaptionIndex = _this->MediaTransportControls->ClosedCaptionIndex;
			_this->_MediaOpenDataStore.AudioVolumeBoost = _this->VolumeBoost;
			//오디오 스트림 목록 선택값 초기화 (Changed이벤트 발생)
			_this->AudioStreamLanguageIndex = -1;
			//자막 목록 선택값 초기화 (재선택을 위해서)
			_this->MediaTransportControls->ClosedCaptionIndex = -1;
			
			//변경할 스트림 인덱스 값 저장
			auto payload = _this->MediaTransportControls->AudioStreamLanguageSource->GetAt(_this->_MediaOpenDataStore.AudioStreamLanguageIndex)->Payload;
			auto ci = dynamic_cast<CodecInformation^>(payload);
			_this->_MediaOpenDataStore.EnforceAudioStreamId = ci->StreamId;
			
			//미디어를 열어 재생 시작
			_this->OpenMedia();
		}
	}
}

void CUX::Controls::MediaElement::OnCCCodePageChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	int cp = (int)args->NewValue;
	_this->SetSubtitleCodePage(cp);
}

void CUX::Controls::MediaElement::OnCCDefaultCodePageChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<MediaElement^>(sender);
	int cp = (int)args->NewValue;
	_this->SetSubtitleDefaultCodePage(cp);
}

void CUX::Controls::MediaElement::SeekSubtitle(long long pts, int flag)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->Seek(pts, flag);
	}
}

void CUX::Controls::MediaElement::SetSubtitleSyncTime(long long diff)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->SynchronizeTime = diff;
	}
}

void CUX::Controls::MediaElement::SetSubtitleSeekingState(bool state)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->IsSeeking = state;
	}
}

void CUX::Controls::MediaElement::SetSubtitleSubLanguageCode(String^ langCode)
{
	if (ClosedCaptions != nullptr)
	{
		ClosedCaptions->SelectedSubLanguageCode = langCode;
	}

	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->LanguageCode = langCode;
	}
}

void CUX::Controls::MediaElement::SetSubtitleCodePage(int codePage)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->SelectedCodePage = codePage;
	}
}

void CUX::Controls::MediaElement::SetSubtitleDefaultCodePage(int codePage)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->DefaultCodePage = codePage;
	}
}

void CUX::Controls::MediaElement::ConnectSubtitle(Windows::Foundation::Collections::PropertySet^ propertySet)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));

		subtitleDecoder->Connect(propertySet);
		if (Position.Duration > 20000000)
		{
			subtitleDecoder->Seek(Position.Duration - 20000000, 1);
		}
	}
}

void CUX::Controls::MediaElement::ConsumeSubtitle(long long pts)
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		subtitleDecoder->ConsumePacket(pts);
	}
}

IVector<SubtitleLanguage^>^ CUX::Controls::MediaElement::GetSubtitleSubLanguageSource()
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		return subtitleDecoder->SubtitleLanguage;
	}
	return nullptr;
}

SubtitleSourceTypes CUX::Controls::MediaElement::GetSubtitleSourceType()
{
	if (_MediaFoundationPropertySet->HasKey(SUB_DEC_CONN))
	{
		auto subtitleDecoder = dynamic_cast<ISubtitleDecoderConnector^>(_MediaFoundationPropertySet->Lookup(SUB_DEC_CONN));
		return subtitleDecoder->SourceType;
	}
	return SubtitleSourceTypes::None;
}


void CUX::Controls::MediaElement::OnOrientationChanged(SimpleOrientationSensor^ sender, SimpleOrientationSensorOrientationChangedEventArgs^ args)
{
	Dispatcher->RunAsync(CoreDispatcherPriority::Normal, ref new DispatchedHandler([=]()
	{
		if (this->Visibility == Windows::UI::Xaml::Visibility::Visible)
		{
			if (UseFlipToPause)
			{
				switch (args->Orientation)
				{
				case SimpleOrientation::Facedown:
					//재생 일시정지
					_IsPausedByFlip = true;
					Pause();
					break;
				case SimpleOrientation::Faceup:
					//재생 다시시작
					if (_IsPausedByFlip)
					{
						_IsPausedByFlip = false;
						Play();
					}
					break;
				}
			}

			if (!EnabledRotationLock)
			{
				if (DisplayInformation::GetForCurrentView()->NativeOrientation == DisplayOrientations::Portrait)
				{
					//기본이 세로 모드인 디바이스 (폰)
					switch (args->Orientation)
					{
					case SimpleOrientation::NotRotated:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Portrait;
						break;
					case SimpleOrientation::Rotated90DegreesCounterclockwise:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Landscape;
						break;
					case SimpleOrientation::Rotated180DegreesCounterclockwise:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Portrait;
						break;
					case SimpleOrientation::Rotated270DegreesCounterclockwise:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::LandscapeFlipped;
						break;
					}
				}
				else
				{
					//기본이 가로 모드인 디바이스 (테블릿 등)
					switch (args->Orientation)
					{
					case SimpleOrientation::NotRotated:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Landscape;
						break;
					case SimpleOrientation::Rotated90DegreesCounterclockwise:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Portrait;
						break;
					case SimpleOrientation::Rotated180DegreesCounterclockwise:
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::LandscapeFlipped;
						break;
					case SimpleOrientation::Rotated270DegreesCounterclockwise:
						//DisplayInformation::AutoRotationPreferences = DisplayOrientations::PortraitFlipped;
						DisplayInformation::AutoRotationPreferences = DisplayOrientations::Portrait;
						break;
					}
				}
			}
		}
	}, CallbackContext::Any));
}

void CUX::Controls::MediaElement::ApplyComboBoxPatch(DependencyProperty^ dp)
{
	TC->ApplyComboBoxPatch(dp);
}