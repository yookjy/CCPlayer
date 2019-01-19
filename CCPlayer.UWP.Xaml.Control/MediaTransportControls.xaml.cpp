//
// MediaTransportControls.xaml.cpp
// Implementation of the MediaTransportControls class
//

#include "pch.h"
#include "MediaTransportControls.xaml.h"
#include "MediaElement.xaml.h"
#include "ClosedCaptions.xaml.h"
#include "Helpers\LanguageCodeHelper.h"
#include <sstream>

//#include <WinSock2.h>
//#include <curl\curl.h>

#define TRANSPORT_CTRL_PANEL        0x0001
#define TRANSPORT_CTRL_FLYOUT       0x0002
#define TRANSPORT_CTRL_LOCK         0x0004
#define TRANSPORT_CTRL_ZOOMLOCK     0x0008

#define GESTURE_MODE_ZOOM		  0x0001
#define GESTURE_MODE_BRIGHTNESS   0x0002
#define GESTURE_MODE_POSITION     0x0004
#define GESTURE_MODE_VOLUME       0x0008

namespace WFC = Windows::Foundation::Collections;
namespace WUX = Windows::UI::Xaml;
namespace CUC = CCPlayer::UWP::Common;
namespace CUX = CCPlayer::UWP::Xaml;

using namespace concurrency;
using namespace Platform;
using namespace Windows::Devices::Sensors;
using namespace Windows::Foundation;
using namespace Windows::UI::Core;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::Storage;
using namespace Windows::Storage::Search;
using namespace Windows::System::Power;
using namespace Windows::System::Profile;

using namespace WFC;
using namespace CUC::Codec;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

TimeSpan CUX::Controls::MediaTransportControls::RunningTime::get()
{
	if (CCPMediaElement != nullptr)
	{
		return CCPMediaElement->NaturalDuration.TimeSpan;
	}
	return TimeSpan();
}

TimeSpan CUX::Controls::MediaTransportControls::RemainingTime::get()
{
	if (CCPMediaElement != nullptr)
	{
		TimeSpan rt;
		rt.Duration = CCPMediaElement->NaturalDuration.TimeSpan.Duration - CCPMediaElement->Position.Duration;
		return rt;
	}
	return TimeSpan();
}

TimeSpan CUX::Controls::MediaTransportControls::Position::get()
{
	if (CCPMediaElement != nullptr)
	{
		auto position = CCPMediaElement->Position;
		return position;
	}
	return TimeSpan();
}

void CUX::Controls::MediaTransportControls::Position::set(TimeSpan value)
{
	if (CCPMediaElement != nullptr)
	{
		auto isEnabled = _PresentationTimer->IsEnabled;
		//다음 파일 재생 시작시 MediaOpen에서 RunningTime 호출시 이전 파일의 시간이 TwoWay에 의해 트리거됨
		if (isEnabled)
		{
			CCPMediaElement->Position = value;
			OnPropertyChanged("Position");
			OnPropertyChanged("RemainingTime");
		}
	}
}
double CUX::Controls::MediaTransportControls::PlaybackRate::get()
{
	if (CCPMediaElement != nullptr)
	{
		return CCPMediaElement->PlaybackRate;
	}
	return 1.0;
}

void CUX::Controls::MediaTransportControls::PlaybackRate::set(double value)
{
	if (CCPMediaElement != nullptr)
	{
		CCPMediaElement->PlaybackRate = value;
		OnPropertyChanged("PlaybackRate");
	}
}

int CUX::Controls::MediaTransportControls::CCCharsetIndex::get()
{
	int index = 0;
	for (unsigned int i = 0; i < CCCharsetSource->Size; i++)
	{
		CUX::Helpers::CodePage^ cp = CCCharsetSource->GetAt(i);
		if (cp->Value == CCPMediaElement->CCCodePage)
		{
			index = i;
			break;
		}
	}
	return index;
}

void CUX::Controls::MediaTransportControls::CCCharsetIndex::set(int value)
{
	auto codePage = CCCharsetSource->GetAt(value);
	auto name = codePage->Name;
	auto val = codePage->Value;
	auto key = codePage->Key;
	int newCP = CCCharsetSource->GetAt(value)->Value;

	if (CCPMediaElement->CCCodePage != newCP)
	{
		//브릿지 설정
		CCPMediaElement->CCCodePage = CCCharsetSource->GetAt(value)->Value;
		//UI갱신
		OnPropertyChanged("CCCharsetIndex");
	}
}

void CUX::Controls::MediaTransportControls::OnFailedSubtitleDecoding(Object ^sender, long long args)
{
	/*
	//현재 선택되어 있는 자막 조회
	Dispatcher->RunAsync(CoreDispatcherPriority::Normal,
		ref new DispatchedHandler([this]()
	{
		UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ extSub = nullptr;
		auto cc = ClosedCaptionSource->GetAt(ClosedCaptionIndex);
		auto ci = dynamic_cast<CCPlayer::UWP::FFmpeg::Information::CodecInformation^>(cc->Payload);
		auto bridge = SubtitleBridge::Instance;

		//키가 URI의 경우 (외부 자막)
		if (cc->Key->GetType()->FullName == String::typeid->FullName)
		{
			extSub = CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser::CreateExternalSubtitleParserFromUri(dynamic_cast<String^>(cc->Key));
		}
		//키가 파일 스트림의 경우 (외부 자막)
		else if (cc->Key->GetType()->FullName == Windows::Storage::Streams::FileRandomAccessStream::typeid->FullName)
		{
			extSub = CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser::CreateExternalSubtitleParserFromStream(dynamic_cast<IRandomAccessStream^>(cc->Key));
		}

		if (extSub != nullptr)
		{
			//현재 스트림 설정
			extSub->SelectedSubtitleStreamIndex = ci->StreamId;

			extSub->FailedDecoding += ref new Windows::Foundation::EventHandler<long long>(this, &CUX::Controls::MediaTransportControls::OnFailedSubtitleDecoding);
			//
			if (bridge->ExternalSubtitleParser != nullptr)
			{
				extSub->SynchronizeTime = bridge->ExternalSubtitleParser->SynchronizeTime;
			}

			//자막 설정
			bridge->SetSubtitleParser(extSub);
		}
	}, CallbackContext::Any));
	*/
}

int CUX::Controls::MediaTransportControls::AspectRatioIndex::get()
{
	int index = 0;
	for (unsigned int i = 0; i < AspectRatioSource->Size; i++)
	{
		KeyName^ kn = AspectRatioSource->GetAt(i);
		if ((AspectRatios)kn->Key == CCPMediaElement->AspectRatio)
		{
			index = i;
			break;
		}
	}
	return index;
}

void CUX::Controls::MediaTransportControls::AspectRatioIndex::set(int value)
{
	if (AspectRatioSource != nullptr && AspectRatioSource->Size > 0)
	{
		auto val = (AspectRatios)AspectRatioSource->GetAt(value)->Key;
		CCPMediaElement->AspectRatio = val;
		OnPropertyChanged("AspectRatioIndex");
	}
}

int CUX::Controls::MediaTransportControls::DisplayRotationIndex::get()
{
	int index = 0;
	for (unsigned int i = 0; i < DisplayRotationSource->Size; i++)
	{
		KeyName^ kn = DisplayRotationSource->GetAt(i);
		if ((DisplayRotations)kn->Key == CCPMediaElement->DisplayRotation)
		{
			index = i;
			break;
		}
	}
	return index;
}

void CUX::Controls::MediaTransportControls::DisplayRotationIndex::set(int value)
{
	if (DisplayRotationSource != nullptr && DisplayRotationSource->Size > 0)
	{
		KeyName^ kn = DisplayRotationSource->GetAt(value);
		if (kn != nullptr)
		{
			DisplayRotations rotation = (DisplayRotations)kn->Key;
			CCPMediaElement->DisplayRotation = rotation;
			OnPropertyChanged("DisplayRotationIndex");
		}
	}
}

IVector<CUX::Helpers::CodePage^>^ CUX::Controls::MediaTransportControls::CCCharsetSource::get()
{
	return CUX::Helpers::CodePageHelper::CharsetCodePage;
}

CUX::Controls::MediaTransportControls::MediaTransportControls()
	: _RunningTime(TimeSpan())
	, _Position(0)
	, _PanelDspSec(0)
	, _TimerTick(0.9)
{
	InitializeComponent();

	TimeSpan ts;
	ts.Duration = (long long)(_TimerTick * 10000000L); //0.9초
	_PresentationTimer = ref new DispatcherTimer();
	_PresentationTimer->Interval = ts;

	_AudioStreamLanguageSource = ref new Platform::Collections::Vector<KeyName^>();
	_AspectRatioSource = ref new Platform::Collections::Vector<KeyName^>();
	_DisplayRotationSource = ref new Platform::Collections::Vector<KeyName^>();
	_PlaybackRepeatSource = ref new Platform::Collections::Vector<KeyName^>();
	_ClosedCaptionSource = ref new Platform::Collections::Vector<KeyName^>();
	_ClosedCaptionSubLanguageSource = ref new Platform::Collections::Vector<KeyName^>();
	
	auto resource = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
	
	PlaybackRepeatSource->Append(ref new KeyName(PlaybackRepeats::None, resource->GetString("PlaybackRepeatItems/None")));
	PlaybackRepeatSource->Append(ref new KeyName(PlaybackRepeats::Once, resource->GetString("PlaybackRepeatItems/Once")));
	PlaybackRepeatSource->Append(ref new KeyName(PlaybackRepeats::All, resource->GetString("PlaybackRepeatItems/All")));

	AspectRatioSource->Append(ref new KeyName(AspectRatios::Uniform, resource->GetString("AspectRatioItems/Uniform")));
	AspectRatioSource->Append(ref new KeyName(AspectRatios::UniformToFill, resource->GetString("AspectRatioItems/UniformToFill")));
	AspectRatioSource->Append(ref new KeyName(AspectRatios::Fill, resource->GetString("AspectRatioItems/Fill")));

	DisplayRotationSource->Append(ref new KeyName(DisplayRotations::None, resource->GetString("DisplayRotationItems/None")));
	DisplayRotationSource->Append(ref new KeyName(DisplayRotations::Clockwise90, resource->GetString("DisplayRotationItems/Clockwise90")));
	DisplayRotationSource->Append(ref new KeyName(DisplayRotations::Clockwise180, resource->GetString("DisplayRotationItems/Clockwise180")));
	DisplayRotationSource->Append(ref new KeyName(DisplayRotations::Clockwise270, resource->GetString("DisplayRotationItems/Clockwise270")));

	//패널 숨김
	ControlPanel_ControlPanelVisibilityStates_Border->Opacity = 0;

	//이벤트핸들러 등록
	RegisterEvents();
	
	//윈도우 버전에 따라 프로퍼티 추가 (C#에서 호출시는 값이 돌아오나, CX에서 호출하면 nullptr가 반환됨, 왜???)
	//Object^ prop = CCPlayer::UWP::Common::Helper::ReflectionHelper::GetRuntimeProperty(
	//	"Windows.UI.Xaml.Controls.ComboBox, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime",
	//	"AllowFocusOnInteractionProperty");
	//DependencyProperty^ dp = dynamic_cast<DependencyProperty^>(prop);
	//if (dp != nullptr)
	//{
	//	CCLanguageComboBox->SetValue(dp, true);
	//	CCSubLanguageComboBox->SetValue(dp, true);
	//	CCCharsetsComboBox->SetValue(dp, true);
	//	AudioLanguagesComboBox->SetValue(dp, true);
	//	AspectRatioComboBox->SetValue(dp, true);
	//	DisplayRotationComboBox->SetValue(dp, true);
	//	PlaybackRepeatComboBox->SetValue(dp, true);
	//}

	//CURL *curl;   CURLcode res;

	//curl = curl_easy_init();   
	//if (curl) {
	//	curl_easy_setopt(curl, CURLOPT_URL, "http://www.cnn.com/");
	//	res = curl_easy_perform(curl);

	//	/* always cleanup */
	//	curl_easy_cleanup(curl);
	//}
}

CUX::Controls::MediaTransportControls::~MediaTransportControls()
{
	OutputDebugMessage(L"Called constructor of the MediaTransportControls\n");
}

void CUX::Controls::MediaTransportControls::ApplyComboBoxPatch(DependencyProperty^ dp)
{
	CCLanguageComboBox->SetValue(dp, true);
	CCSubLanguageComboBox->SetValue(dp, true);
	CCCharsetsComboBox->SetValue(dp, true);
	AudioLanguagesComboBox->SetValue(dp, true);
	AspectRatioComboBox->SetValue(dp, true);
	DisplayRotationComboBox->SetValue(dp, true);
	PlaybackRepeatComboBox->SetValue(dp, true);
}

void CUX::Controls::MediaTransportControls::RegisterEvents()
{
	auto ccSource = dynamic_cast<Platform::Collections::Vector<KeyName^>^>(_ClosedCaptionSource);
	if (ccSource != nullptr)
	{
		ccSource->VectorChanged += ref new WFC::VectorChangedEventHandler<CUX::Controls::KeyName ^>(this, &CUX::Controls::MediaTransportControls::OnClosedCaptionSourceChanged);
	}

	CloseButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCloseButtonTapped);
	UnlockControlPanelButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnUnlockControlPanelButtonTapped);
	MoveScreenCompleteButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnMoveScreenCompleteButtonTapped);

	GesturePanel->ManipulationStarting += ref new ManipulationStartingEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelManipulationStarting);
	GesturePanel->ManipulationStarted += ref new ManipulationStartedEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelManipulationStarted);
	GesturePanel->ManipulationDelta += ref new ManipulationDeltaEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelManipulationDelta);
	GesturePanel->ManipulationCompleted += ref new ManipulationCompletedEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelManipulationCompleted);
	GesturePanel->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelTapped);
	GesturePanel->DoubleTapped += ref new DoubleTappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelDoubleTapped);
	GesturePanel->RightTapped += ref new RightTappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnGesturePanelRightTapped);
	ControlPanel_ControlPanelVisibilityStates_Border->PointerEntered += ref new PointerEventHandler(this, &CUX::Controls::MediaTransportControls::OnControlPanelPointerEntered);
	ControlPanel_ControlPanelVisibilityStates_Border->PointerExited += ref new PointerEventHandler(this, &CUX::Controls::MediaTransportControls::OnControlPanelPointerExited);

	//자막 설정 버튼
	ClosedCaptionButton->Flyout->Opening += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutOpening);
	ClosedCaptionButton->Flyout->Closed += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutClosed);
	ImportClosedCaptionFile->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnImportClosedCaptionTapped);
	CCPositionButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCPositionButtonTapped);
	CCSettingsOpenButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSettingsOpenButtonTapped);
	
	CCCharsetButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCCharsetButtonTapped);
	SubtitleOnOffButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnSubtitleOnOffButtonTapped);
	CCSyncResetButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSyncResetButtonTapped);
	CCSyncFastButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSyncFastButtonTapped);
	CCSyncFastHalfButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSyncFastHalfButtonTapped);
	CCSyncSlowHalfButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSyncSlowHalfButtonTapped);
	CCSyncSlowButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnCCSyncSlowButtonTapped);
	FontSizeRatioSlider->ValueChanged += ref new WUX::Controls::Primitives::RangeBaseValueChangedEventHandler(this, &CUX::Controls::MediaTransportControls::OnFontSizeRatioValueChanged);

	//오디오 설정버튼
	AudioButton->Flyout->Opening += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutOpening);
	AudioButton->Flyout->Closed += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutClosed);
	AudioSyncButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnAudioSyncTapped);
	AudioBalanceButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnAudioBalanceButtonTapped);
	VolumeMuteButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnVolumeMuteButtonTapped);
	AudioVolumeBoosterButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnAudioVolumeBoosterButtonTapped);

	//디스플레이 설정 버튼
	DisplayButton->Flyout->Opening += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutOpening);
	DisplayButton->Flyout->Closed += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnFlyoutClosed);
	//Anniversary update (14393)에서는 반드시 아래 항목을 True로 변경해주어야 Flyout의 콤보박스가 정상 작동함
	//DisplayButton->AllowFocusOnInteraction = true;

	DisplayRotationButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnDisplayRotationButtonTapped);
	AspectRatioButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnAspectRatioButtonTapped);
	PlaybackRepeatButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnPlaybackRepeatButtonTapped);
	DisplayZoomSlider->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnDisplayZoomSliderTapped);
	DisplayZoomButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnDisplayZoomButtonTapped);
	BrightnessButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnBrightnessButtonTapped);
	PlaybackRateButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnPlaybackRateButtonTapped);
	PlaybackRateSlider->ValueChanged += ref new WUX::Controls::Primitives::RangeBaseValueChangedEventHandler(this, &CUX::Controls::MediaTransportControls::OnPlaybackRateValueChanged);
	//디코더 변경 버튼
	HWDecoderButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnHWDecoderButtonTapped);
	HybridDecoderButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnHybridDecoderButtonTapped);
	SWDecoderButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnSWDecoderButtonTapped);

	//잠금설정
	LockControlPanelButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnLockControlPanelButtonTapped);

	PreviousMediaButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnPreviousMediaButtonTapped);
	NextMediaButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnNextMediaButtonTapped);
	//재생 관련
	ProgressSlider->ManipulationStarted += ref new ManipulationStartedEventHandler(this, &CUX::Controls::MediaTransportControls::OnProgressSliderManipulationStarted);
	ProgressSlider->ManipulationCompleted += ref new ManipulationCompletedEventHandler(this, &CUX::Controls::MediaTransportControls::OnProgressSliderManipulationCompleted);
	FastRewindButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnFastRewindButtonTapped);
	PlayPauseButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnPlayPauseButtonTapped);
	FastForwardButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnFastForwardButtonTapped);
	FullWindowButton->Tapped += ref new TappedEventHandler(this, &CUX::Controls::MediaTransportControls::OnFullWindowButtonTapped);

	_PresentationTimer->Tick += ref new Windows::Foundation::EventHandler<Object ^>(this, &CUX::Controls::MediaTransportControls::OnPresentationTimerTick);
//	DataContextChanged += ref new Windows::Foundation::TypedEventHandler<WUX::FrameworkElement ^, WUX::DataContextChangedEventArgs ^>(this, &CUX::Controls::MediaTransportControls::OnDataContextChanged);
	
	this->Loaded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaTransportControls::OnLoaded);
	this->Unloaded += ref new WUX::RoutedEventHandler(this, &CUX::Controls::MediaTransportControls::OnUnloaded);
}

void CUX::Controls::MediaTransportControls::OnLoaded(Object ^sender, WUX::RoutedEventArgs ^e)
{
	if (SimpleOrientationSensor::GetDefault() != nullptr)
	{
		VisualStateManager::GoToState(this, "RotationLockAvailable", false);
	}

	if (AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Mobile"
		&& AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Xbox")
	{
		//모바일이 아닌 경우 선택적으로 FullScreen 사용
		VisualStateManager::GoToState(this, "FullWindowAvailable", false);

		//키보드 입력
		_KeyUpListenerToken = WUX::Window::Current->CoreWindow->KeyUp += ref new Windows::Foundation::TypedEventHandler<Windows::UI::Core::CoreWindow ^, Windows::UI::Core::KeyEventArgs ^>(this, &CUX::Controls::MediaTransportControls::OnKeyUp);
		_KeyDownListenerToken = WUX::Window::Current->CoreWindow->KeyDown += ref new Windows::Foundation::TypedEventHandler<Windows::UI::Core::CoreWindow ^, Windows::UI::Core::KeyEventArgs ^>(this, &CUX::Controls::MediaTransportControls::OnKeyDown);
		
		//마우스 
		_PointerMovedListenerToken = WUX::Window::Current->CoreWindow->PointerMoved += ref new Windows::Foundation::TypedEventHandler<Windows::UI::Core::CoreWindow ^, Windows::UI::Core::PointerEventArgs ^>(this, &CUX::Controls::MediaTransportControls::OnPointerMoved);
	}
}

void CUX::Controls::MediaTransportControls::OnUnloaded(Object ^sender, WUX::RoutedEventArgs ^e)
{
	//if (_PresentationTimer->IsEnabled)
	//{
	//	_PresentationTimer->Stop();
	//}
	//_PresentationTimer = nullptr;

	//WUX::Window::Current->CoreWindow->KeyUp -= _KeyUpListenerToken;
	//WUX::Window::Current->CoreWindow->KeyDown -= _KeyDownListenerToken;
	//WUX::Window::Current->CoreWindow->PointerMoved -= _PointerMovedListenerToken;

	//_AudioStreamLanguageSource->Clear();
	//_AudioStreamLanguageSource = nullptr;

	//_AspectRatioSource->Clear();
	//_AspectRatioSource = nullptr;

	//_DisplayRotationSource->Clear();
	//_DisplayRotationSource = nullptr;

	//_PlaybackRepeatSource->Clear();
	//_PlaybackRepeatSource = nullptr;

	//_ClosedCaptionSource->Clear();
	//_ClosedCaptionSource = nullptr;

	//_ClosedCaptionSubLanguageSource->Clear();
	//_ClosedCaptionSubLanguageSource = nullptr;
}

void CUX::Controls::MediaTransportControls::OnCloseButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CloseButtonTapped(sender, e);
}

void CUX::Controls::MediaTransportControls::OnCCSettingsOpenButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	ClosedCaptionButton->Flyout->Hide();
	VisualStateManager::GoToState(this, "ControlPanelFadeOut", false);
	ErrorMessage = "";
	_PanelDspSec = 0;
	CCSettingsOpenButtonTapped(sender, e);
	//마우스 표시
	ShowPointer();
}

void CUX::Controls::MediaTransportControls::OnPreviousMediaButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	PreviousMediaButtonTapped(sender, e);
}

void CUX::Controls::MediaTransportControls::OnNextMediaButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	NextMediaButtonTapped(sender, e);
}

void CUX::Controls::MediaTransportControls::OnCCSyncResetButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCSyncSeconds = 0.0f;
}

void CUX::Controls::MediaTransportControls::OnCCSyncFastButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCSyncSeconds -= 1.0f;
}

void CUX::Controls::MediaTransportControls::OnCCSyncFastHalfButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCSyncSeconds -= 0.5f;
}

void CUX::Controls::MediaTransportControls::OnCCSyncSlowHalfButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCSyncSeconds += 0.5f;
}

void CUX::Controls::MediaTransportControls::OnCCSyncSlowButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCSyncSeconds += 1.0f;
}

void CUX::Controls::MediaTransportControls::OnPlayPauseButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	if (CCPMediaElement->CurrentState == MediaElementState::Playing)
	{
		CCPMediaElement->Pause();
	}
	else
	{
		CCPMediaElement->Play();
	}
}

void CUX::Controls::MediaTransportControls::OnFullWindowButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	ToggleFullScreen();
}

void CUX::Controls::MediaTransportControls::OnVolumeMuteButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	auto value = !CCPMediaElement->IsMuted;
	CCPMediaElement->IsMuted = value;
	VisualStateManager::GoToState(this, value ? "MuteState" : "VolumeState", false);
}

void CUX::Controls::MediaTransportControls::OnAudioVolumeBoosterButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	double newValue = 0.0;
	auto payload = AudioStreamLanguageSource->GetAt(CCPMediaElement->AudioStreamLanguageIndex)->Payload;
	if (payload != nullptr)
	{
		auto ci = dynamic_cast<CodecInformation^>(payload);
		if (ci != nullptr && ci->Channels > 2)
		{
			newValue = 7.0;
		}
	}

	CCPMediaElement->VolumeBoost = newValue;
}

void CUX::Controls::MediaTransportControls::OnAudioSyncTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCPMediaElement->AudioSync = 0;
}

void CUX::Controls::MediaTransportControls::OnAudioBalanceButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCPMediaElement->Balance = 0;
}

void CUX::Controls::MediaTransportControls::OnCCCharsetButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCCharsetIndex = 0;
}

void CUX::Controls::MediaTransportControls::OnSubtitleOnOffButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	auto value = !CCPMediaElement->UseClosedCaptions;
	CCPMediaElement->UseClosedCaptions = value;
}

void CUX::Controls::MediaTransportControls::OnPlaying()
{
	_PresentationTimer->Start();
	if (_DecoderErrorMessage != nullptr && !_DecoderErrorMessage->IsEmpty())
	{
		ErrorMessage = _DecoderErrorMessage;
		VisualStateManager::GoToState(this, "Alert", false);
		_DecoderErrorMessage = nullptr;
	}
	else
	{
		VisualStateManager::GoToState(this, "Normal", false);
	}
	VisualStateManager::GoToState(this, "PauseState", false);
}

void CUX::Controls::MediaTransportControls::OnPaused()
{
	_PresentationTimer->Stop();
	VisualStateManager::GoToState(this, "Normal", false);
	VisualStateManager::GoToState(this, "PlayState", false);
}

void CUX::Controls::MediaTransportControls::OnOpening()
{
	_PresentationTimer->Stop();
	VisualStateManager::GoToState(this, "Loading", false);

	if (!IsKeepOpenState)
	{
		_ControlPanelOpenStates = 0;
	}

	if (_ControlPanelOpenStates == 0)
		FadeInControlPanel();
}
void CUX::Controls::MediaTransportControls::OnBuffering()
{
	_PresentationTimer->Stop();
	VisualStateManager::GoToState(this, "Buffering", false);
}

void CUX::Controls::MediaTransportControls::OnStopped()
{
	//마우스 숨김 해제
	ShowPointer();
	//타이머 정지
	_PresentationTimer->Stop();
}

void CUX::Controls::MediaTransportControls::OnClosed()
{
	_PresentationTimer->Stop();
}

void CUX::Controls::MediaTransportControls::AppendClosedCaption(CUX::Controls::KeyName^ value)
{
	ClosedCaptionSource->Append(value);
}

void CUX::Controls::MediaTransportControls::AppendAudioStream(CUX::Controls::KeyName^ value)
{
	AudioStreamLanguageSource->Append(value);
}

void CUX::Controls::MediaTransportControls::EnableLimeEngine(bool enabled)
{
	if (enabled)
	{
		if (AspectRatioSource->Size == 3)
		{
			auto resource = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
			AspectRatioSource->Append(ref new KeyName(AspectRatios::R16_9, "16 : 9"));
			AspectRatioSource->Append(ref new KeyName(AspectRatios::R16_10, "16 : 10"));
			AspectRatioSource->Append(ref new KeyName(AspectRatios::R4_3, "4 : 3"));
			AspectRatioSource->Append(ref new KeyName(AspectRatios::R235_1, "2.35 : 1"));
			AspectRatioSource->Append(ref new KeyName(AspectRatios::R185_1, "1.85 : 1"));
		}

		VisualStateManager::GoToState(this, "DisplayRotationAvailable", false);
		VisualStateManager::GoToState(this, "ZoomAvailable", false);
		VisualStateManager::GoToState(this, "HorizontalMirrorAvailable", false);
	}
	else
	{
		while (AspectRatioSource->Size > 3)
		{
			AspectRatioSource->RemoveAtEnd();
		}

		VisualStateManager::GoToState(this, "DisplayRotationUnavailable", false);
		VisualStateManager::GoToState(this, "ZoomUnavailable", false);
		VisualStateManager::GoToState(this, "HorizontalMirrorUnavailable", false);
	}
}

void CUX::Controls::MediaTransportControls::OnMediaOpened(CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ decoderConnector)
{
	OnPropertyChanged("RunningTime");
	OnPropertyChanged("Position");
	OnPropertyChanged("RemainingTime");

	// 선택 디코더가 아닌 다른 디코더로 재생되는 경우 알림
	if (decoderConnector->ReqDecoderType != decoderConnector->ResDecoderType)
	{
		auto resource = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
		_DecoderErrorMessage = Lime::CPPHelper::StringHelper::Format(resource->GetString("NotSupportedDecoder"), decoderConnector->ReqDecoderType.ToString());
	}
	
	//최종적으로 디코더를 현재 디코더와 동기화
	CCPMediaElement->DecoderType = decoderConnector->ResDecoderType;

	//현재 선택된 디코더값 셋팅
	switch (decoderConnector->ResDecoderType)
	{
	case DecoderTypes::HW:
		HWDecoderButton->IsChecked = true;
		VisualStateManager::GoToState(this, "AudioSyncUnavailable", false);
		VisualStateManager::GoToState(this, "HorizontalMirrorUnavailable", false);
		break;
	case DecoderTypes::Hybrid:
		HybridDecoderButton->IsChecked = true;
		VisualStateManager::GoToState(this, "AudioSyncAvailable", false);
		VisualStateManager::GoToState(this, "HorizontalMirrorUnavailable", false);
		break;
	case DecoderTypes::SW:
		SWDecoderButton->IsChecked = true;
		VisualStateManager::GoToState(this, "AudioSyncAvailable", false);
		if (CCPMediaElement->UseLimeEngine)
		{
			VisualStateManager::GoToState(this, "HorizontalMirrorAvailable", false);
		}
		break;
	}

	if (CCPMediaElement->ClosedCaptions)
	{
		FontSizeRatioSlider->Value = CCPMediaElement->ClosedCaptions->FontSizeRatio;
	}

	//오디오 선택 메뉴 활성/비활성화
	bool isMultipleAudio = AudioStreamLanguageSource->Size > 1;
	AudioLanguagesComboBox->IsEnabled = isMultipleAudio;
	VisualStateManager::GoToState(this, isMultipleAudio ? "AudioSelectionAvailable" : "AudioSelectionUnavailable", false);

	//자막 싱크값 초기화
	CCSyncSeconds = 0.0f;
	//자막선택 메뉴 활성/비활성화
	VisualStateManager::GoToState(this, ClosedCaptionSource->Size > 0 ? "CCSelectionAvailable" : "CCSelectionUnavailable", false);

	//비디오 설정값 초기화
	CCPMediaElement->EnabledHorizontalMirror = false;
	CCPMediaElement->ZoomInOut = 1;
	PlaybackRepeatIndex = 0;
	PlaybackRate = 1;
	AspectRatioIndex = 0;
	DisplayRotationIndex = 0;

	//오디오 설정값 초기화
	CCPMediaElement->Volume = 1;
	CCPMediaElement->Balance = 0;
}

void CUX::Controls::MediaTransportControls::OnMediaEnded()
{
	VisualStateManager::GoToState(this, "PlayState", false);
}

void CUX::Controls::MediaTransportControls::OnMediaFailed(int errorCode, CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ decoderConnector)
{
	auto resource = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
	if (errorCode != 0)
	{
		if (errorCode == 4 && decoderConnector->ResDecoderType != DecoderTypes::SW)
		{
			_DecoderErrorMessage = Lime::CPPHelper::StringHelper::Format(resource->GetString("NotSupportedDecoder"), decoderConnector->ReqDecoderType.ToString());
			//에러코드4번 : OS재생불가 포맷, 응답한 디코더가 SW가 아니면 SW디코더로 다시 시작
			if (CCPMediaElement->DecoderType == DecoderTypes::AUTO || CCPMediaElement->DecoderType == DecoderTypes::HW)
			{
				CCPMediaElement->ChangeDecoder(DecoderTypes::Hybrid);
				return;
			}
			else if (CCPMediaElement->DecoderType == DecoderTypes::Hybrid)
			{
				CCPMediaElement->ChangeDecoder(DecoderTypes::SW);
				return;
			}
		}
		
		//위에서 해당되지 않은 에러 errorCode가 4가 아니거나, 디코더타입이 SW인데 재생을 할 수 없는 경우
		std::ostringstream buffer;
		buffer << errorCode;
		std::string str = buffer.str();
		std::wstring wnum(str.begin(), str.end());
		wnum.insert(wnum.begin(), 2 - wnum.length(), '0');
		wnum.insert(0, L"Message/Error/MFEngine");

		String^ resourceKey = ref new String(wnum.c_str());
		ErrorMessage = resource->GetString(resourceKey);
		VisualStateManager::GoToState(this, "Error", false);
		FadeInControlPanel();
	}
	else
	{
		//알수 없는 에러
		ErrorMessage = resource->GetString("Message/Error/MFEngine99");
		VisualStateManager::GoToState(this, "Error", false);
		FadeInControlPanel();
	}
}

void CUX::Controls::MediaTransportControls::OnSeekCompleted()
{
	if (Position.Duration / 10000000 == 0)
	{
		PlaybackRepeats repeat = (PlaybackRepeats)PlaybackRepeatSource->GetAt(PlaybackRepeatIndex)->Key;

		if (repeat != PlaybackRepeats::None)
		{
			CCPMediaElement->SeekSubtitle(Position.Duration, 1);
		}

		if (repeat == PlaybackRepeats::Once)
		{
			//한번 반복후 옵션 초기화
			for (unsigned int i = 0; i < PlaybackRepeatSource->Size; i++)
			{
				if ((PlaybackRepeats)PlaybackRepeatSource->GetAt(i)->Key == PlaybackRepeats::None)
				{
					//프로퍼티 설정시 내부에서 IsLoop속성이 변경됨.
					PlaybackRepeatIndex = i;
					break;
				}
			}
		}
	}

	_IsSeeking = false;
	
	if (_PrevPosition.Duration < Position.Duration)
		CCPMediaElement->SeekSubtitle(Position.Duration, 0);

	if (_PrevPosition.Duration > Position.Duration) //AVSEEK_FLAG_BACKWARD
		CCPMediaElement->SeekSubtitle(Position.Duration, 1);
}

void CUX::Controls::MediaTransportControls::OnFastRewindButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	double time = CCPMediaElement->SeekTimeIntervalValue;
	time = time / 3.0 < 1 ? 1 : ceil(time / 3.0);
	CCPMediaElement->Seek((long long)(time * -10000000));
}

void CUX::Controls::MediaTransportControls::OnFastForwardButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCPMediaElement->Seek(CCPMediaElement->SeekTimeIntervalValue * 10000000);
}

void CUX::Controls::MediaTransportControls::OnPropertyChanged(String^ propertyName)
{
	PropertyChanged(this, ref new WUX::Data::PropertyChangedEventArgs(propertyName));
}

void CUX::Controls::MediaTransportControls::OnHWDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	CCPMediaElement->ChangeDecoder(DecoderTypes::HW);
}

void CUX::Controls::MediaTransportControls::OnHybridDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	CCPMediaElement->ChangeDecoder(DecoderTypes::Hybrid);
}

void CUX::Controls::MediaTransportControls::OnSWDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	CCPMediaElement->ChangeDecoder(DecoderTypes::SW);
}

void CUX::Controls::MediaTransportControls::OnPresentationTimerTick(Object ^sender, Object ^args)
{
	//타임 슬라이더 위치 이동
	OnPropertyChanged("Position");
	//남은시간
	OnPropertyChanged("RemainingTime");

	if (_ControlPanelOpenStates & TRANSPORT_CTRL_LOCK)
	{
		if (UnlockControlPanelButton->Opacity == 1)
		{
			if (_PanelDspSec >= 1.8)
			{
				_PanelDspSec = 0;
				VisualStateManager::GoToState(this, "ControlPanelUnlockFadeOut", false);
			}
			else
			{
				_PanelDspSec += _TimerTick;
			}
		}
	}
	else if (_ControlPanelOpenStates & TRANSPORT_CTRL_ZOOMLOCK)
	{
		if (MoveScreenCompleteButton->Opacity == 1)
		{
			if (_PanelDspSec >= 1.8)
			{
				_PanelDspSec = 0;
				VisualStateManager::GoToState(this, "MoveScreenFadeOut", false);
			}
			else
			{
				_PanelDspSec += _TimerTick;
			}
		}
	}
	else if (_ControlPanelOpenStates == 0)
	{
		if (ControlPanel_ControlPanelVisibilityStates_Border->Opacity == 1)
		{
			if (_PanelDspSec >= 1.8)
			{
				_PanelDspSec = 0;
				VisualStateManager::GoToState(this, "Normal", false);
				VisualStateManager::GoToState(this, "ControlPanelFadeOut", false);
				ErrorMessage = "";
			}
			else
			{
				_PanelDspSec += _TimerTick;
			}
		}
	}
	
	if (Window::Current->CoreWindow->PointerCursor != nullptr)
	{
		if (_PointerDspSec >= 1.8)
		{
			HidePointer();
		}
		else
		{
			_PointerDspSec += _TimerTick;
		}
	}
}

void CUX::Controls::MediaTransportControls::ToggleFullScreen()
{
	auto value = !CCPMediaElement->IsFullScreen;
	CCPMediaElement->IsFullScreen = value;
	VisualStateManager::GoToState(this, value ? "FullWindowState" : "NonFullWindowState", false);
}

String^ DischargingIcons[] = { L"\xEBA0", L"\xEBA1", L"\xEBA2", L"\xEBA3", L"\xEBA4", L"\xEBA5", L"\xEBA6", L"\xEBA7", L"\xEBA8", L"\xEBA9", L"\xEBAA" };
String^    ChargingIcons[] = { L"\xEBAB", L"\xEBAC", L"\xEBAD", L"\xEBAE", L"\xEBAF", L"\xEBB0", L"\xEBB1", L"\xEBB2", L"\xEBB3", L"\xEBB4", L"\xEBB5" };
String^ EnergySaverIcons[] = { L"\xEBB6", L"\xEBB7", L"\xEBB8", L"\xEBB9", L"\xEBBA", L"\xEBBB", L"\xEBBC", L"\xEBBD", L"\xEBBE", L"\xEBBF", L"\xEBC0" };

void CUX::Controls::MediaTransportControls::FadeInControlPanel()
{
	//auto report = Windows::Devices::Power::Battery::AggregateBattery->GetReport();
	//Windows::System::Power::BatteryStatus status = report->Status;

	auto status = Windows::System::Power::PowerManager::BatteryStatus;

	Windows::Globalization::Calendar^ c = ref new Windows::Globalization::Calendar;
	c->SetToNow();
		
	if (_TimeFormat == nullptr)
	{
		_TimeFormat = ref new Windows::Globalization::DateTimeFormatting::DateTimeFormatter("shorttime");
	}

	if (status != Windows::System::Power::BatteryStatus::NotPresent)
	{
		/*auto full = report->FullChargeCapacityInMilliwattHours->Value;
		auto curr = report->RemainingCapacityInMilliwattHours->Value;
		auto batteryPercent = (int)ceil((double)curr / (double)full * 100);*/
		//위의 경우 태블릿등에서 값이 null이 나오는 경우가 있어서 수정함.
		//report->FullChargeCapacityInMilliwattHours => null
		//report->RemainingCapacityInMilliwattHours  => null
		auto batteryPercent = PowerManager::RemainingChargePercent;
		
		//안정성을 위해 범위 한정
		if (batteryPercent < 0)
			batteryPercent = 0;
		else if (batteryPercent > 100)
			batteryPercent = 100;

		//배터리 아이콘
		int bp = (int)ceil(batteryPercent / 10.0);
		
		if (PowerManager::EnergySaverStatus == EnergySaverStatus::On)
		{
			BatteryGlyph = EnergySaverIcons[bp];
		}
		else if (status == BatteryStatus::Discharging)
		{
			BatteryGlyph = DischargingIcons[bp];
		}
		else if (status == BatteryStatus::Charging)
		{
			BatteryGlyph = ChargingIcons[bp];
		}
		
		//배터리 백분율
		BatteryText = L"" + batteryPercent;
		//배터리 표시 숨김
		BatteryVisibility = WUX::Visibility::Visible;
	}
	else
	{
		//배터리 표시 
		BatteryVisibility = WUX::Visibility::Collapsed;
	}
	//현재 시간
	TimeText = _TimeFormat->Format(c->GetDateTime());
	//패널 표시
	VisualStateManager::GoToState(this, "ControlPanelFadeIn", false);
	//마우스 표시
	ShowPointer();
}

void CUX::Controls::MediaTransportControls::EnabledZoomLockMode(bool isEnabled, bool isUiUpdate)
{
	if (isEnabled)
	{
		_ControlPanelOpenStates = TRANSPORT_CTRL_ZOOMLOCK;
		if (isUiUpdate)
			VisualStateManager::GoToState(this, "MoveScreenFadeIn", false);
	}
	else
	{
		_ControlPanelOpenStates = 0;
		if (isUiUpdate)
			FadeInControlPanel();
	}
}

void CUX::Controls::MediaTransportControls::OnGesturePanelManipulationStarting(Object ^sender, ManipulationStartingRoutedEventArgs ^e)
{
	ErrorMessage = "";
	//마우스 표시
	ShowPointer();
}

void CUX::Controls::MediaTransportControls::OnGesturePanelManipulationStarted(Object ^sender, ManipulationStartedRoutedEventArgs ^e)
{
	if (_ControlPanelOpenStates & TRANSPORT_CTRL_LOCK)
	{
		e->Complete();
	}

	MovedPosition = Position;
	_PrevZoomScale = CCPMediaElement->ZoomInOut;

	auto grid = dynamic_cast<Grid^>(sender);
	if ((e->Cumulative.Expansion != 0 || CCPMediaElement->ZoomInOut > 1.01) && _ControlPanelOpenStates & TRANSPORT_CTRL_ZOOMLOCK)
	{
		EnabledZoomLockMode(true, true);
		grid->ManipulationMode = ManipulationModes::TranslateX | ManipulationModes::TranslateY | ManipulationModes::Scale;
	}
	else
	{
		grid->ManipulationMode = ManipulationModes::TranslateX | ManipulationModes::TranslateRailsX | ManipulationModes::TranslateY | ManipulationModes::TranslateRailsY | ManipulationModes::Scale;
	}
}

void CUX::Controls::MediaTransportControls::OnGesturePanelManipulationDelta(Object ^sender, ManipulationDeltaRoutedEventArgs ^e)
{
	if (e->Cumulative.Expansion != 0)
	{
		if (CCPMediaElement->UseLimeEngine)
		{
			double scale = e->Cumulative.Scale * _PrevZoomScale;
			if (scale >= 0.5 && scale < 4.01)
			{
				CCPMediaElement->ZoomInOut = scale;
			
				if (e->Cumulative.Scale < 1.0)
					VisualStateManager::GoToState(this, "GestureZoomOutIndicatorIcon", false);
				else if (e->Cumulative.Scale > 1.0)
					VisualStateManager::GoToState(this, "GestureZoomInIndicatorIcon", false);
				else
					VisualStateManager::GoToState(this, "GestureZoomIndicatorIcon", false);
			}
			_GestureMode = GESTURE_MODE_ZOOM;
			VisualStateManager::GoToState(this, "ZoomIndicatorFadeIn", false);
		}
	}
	else
	{
		if (_ControlPanelOpenStates & TRANSPORT_CTRL_ZOOMLOCK)
		{
			CCPMediaElement->ZoomMove = Point(e->Delta.Translation.X, e->Delta.Translation.Y);
		}
		else
		{
			double radians = atan2(e->Cumulative.Translation.Y, e->Cumulative.Translation.X);
			double angle = floor(radians * (180 / 3.1415926535897931));

			if ((angle < -70 && angle > -110) || (angle > 70 && angle < 110))
			{
				//상 : angle < -70 && angle > -110
				//하 : angle > 70 && angle < 110
				double val = e->Delta.Translation.Y * -1;
				//값 변환
				if (val > 0) val = 1;
				else if (val < 0) val = -1;

				//세로 스와이프
				if (e->Position.X > 0 && e->Position.X < CCPMediaElement->ActualWidth / 2)
				{
					_GestureMode = GESTURE_MODE_BRIGHTNESS;
					VisualStateManager::GoToState(this, "BrightnessIndicatorFadeIn", false);
					val = CCPMediaElement->Brightness + val;

					if (val > BrightnessSlider->Maximum) val = BrightnessSlider->Maximum;
					else if (val < BrightnessSlider->Minimum) val = BrightnessSlider->Minimum;
					//밝기 설정
					CCPMediaElement->Brightness = (int)val;
				}
				else
				{
					//음량 변환
					_GestureMode = GESTURE_MODE_VOLUME;
					VisualStateManager::GoToState(this, "VolumeIndicatorFadeIn", false);
					val = CCPMediaElement->Volume + (val / 100);
					if (val > VolumeSlider->Maximum / 100) val = VolumeSlider->Maximum / 100;
					else if (val < VolumeSlider->Minimum / 100) val = VolumeSlider->Minimum / 100;
					//음량 설정
					CCPMediaElement->Volume = val;
				}
			}
			else if ((angle > -20 && angle < 20) || (angle > 160 && angle <= 180) || (angle > -179.9 && angle < -160))
			{
				//좌 : angle > -20 && angle < 20
				//우 : (angle > 160 && angle <= 180) || (angle > -179.9 && angle < -160)
				//if (!VersionHelper.IsFree)
				{
					//가로 스와이프
					double val = e->Delta.Translation.X * 0.4;
					//			sliderPressed = true;
					_IsSeeking = true;
					_GestureMode = GESTURE_MODE_POSITION;
					VisualStateManager::GoToState(this, "PositionIndicatorFadeIn", false);

					TimeSpan mp = MovedPosition;
					TimeSpan mt = MovedTime;

					if (mt.Duration == 0)
					{
						VisualStateManager::GoToState(this, "GesturePositionMoveIndicatorIconAvailable", false);
					}

					if ((mp.Duration + val) / 10000000L <= CCPMediaElement->NaturalDuration.TimeSpan.Duration)
					{
						mt.Duration = mt.Duration + (long long)(val * 10000000L);
						MovedTime = mt;

						mp.Duration = mp.Duration + (long long)(val * 10000000L);
						//재생 예정 시간이 마이너스(음수)가 될 수 없다.
						if (mp.Duration < 0)
						{
							mp.Duration = 0;
						}
						MovedPosition = mp;
					}

					if (MovedTime.Duration < 0)
					{
						VisualStateManager::GoToState(this, "GesturePositionMoveIndicatorFastRewindIcon", false);
					}
					else
					{
						VisualStateManager::GoToState(this, "GesturePositionMoveIndicatorFastForwardIcon", false);
					}
				}
			}
		}
	}
}

void CUX::Controls::MediaTransportControls::OnGesturePanelManipulationCompleted(Object ^sender, ManipulationCompletedRoutedEventArgs ^e)
{
	if (MovedTime.Duration != 0)
	{
		Position = MovedPosition;
		MovedTime = TimeSpan();
	}

	switch (_GestureMode)
	{
	case GESTURE_MODE_BRIGHTNESS:
		VisualStateManager::GoToState(this, "BrightnessIndicatorFadeOut", false);
		break;
	case GESTURE_MODE_POSITION:
		VisualStateManager::GoToState(this, "PositionIndicatorFadeOut", false);
		break;
	case GESTURE_MODE_ZOOM:
		VisualStateManager::GoToState(this, "ZoomIndicatorFadeOut", false);
		break;
	case GESTURE_MODE_VOLUME:
		VisualStateManager::GoToState(this, "VolumeIndicatorFadeOut", false);
		break;
	}

	_GestureMode = 0;
}

void CUX::Controls::MediaTransportControls::OnGesturePanelTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	if (_ControlPanelOpenStates & TRANSPORT_CTRL_LOCK)
	{
		if (UnlockControlPanelButton->Opacity == 0)
		{
			VisualStateManager::GoToState(this, "ControlPanelLcokFadeIn", false);
			//마우스 표시
			ShowPointer();
		}
		else
		{
			VisualStateManager::GoToState(this, "ControlPanelUnlockFadeOut", false);
		}
	}
	else if (_ControlPanelOpenStates & TRANSPORT_CTRL_ZOOMLOCK)
	{
		if (MoveScreenCompleteButton->Opacity == 0)
		{
			VisualStateManager::GoToState(this, "MoveScreenFadeIn", false);
			//마우스 표시
			ShowPointer();
		}
		else
		{
			VisualStateManager::GoToState(this, "MoveScreenFadeOut", false);
		}
	}
	else
	{
		if (ControlPanel_ControlPanelVisibilityStates_Border->Opacity == 0)
		{
			FadeInControlPanel();
		}
		else
		{
			VisualStateManager::GoToState(this, "ControlPanelFadeOut", false);
		}
	}

	_PanelDspSec = 0;
}

void CUX::Controls::MediaTransportControls::OnGesturePanelDoubleTapped(Object ^sender, DoubleTappedRoutedEventArgs ^e)
{
	if ((int)(CCPMediaElement->ZoomInOut * 100) != 100)
	{
		CCPMediaElement->ZoomInOut = 1.0;
	}
	else
	{
		if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
		{
			OnPlayPauseButtonTapped(sender, nullptr);
		}
		else
		{
			ToggleFullScreen();
		}
	}

}

void CUX::Controls::MediaTransportControls::OnGesturePanelRightTapped(Object ^sender, RightTappedRoutedEventArgs ^e)
{
	/*if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily != "Windows.Mobile")
	{
		auto owner = dynamic_cast<Grid^>(sender);
		auto name = owner->Name;
		auto aa = owner->GetValue(Windows::UI::Xaml::Controls::Primitives::FlyoutBase::AttachedFlyoutProperty);
		auto flyout = (Windows::UI::Xaml::Controls::MenuFlyout^)aa;
		flyout->ShowAt(owner, e->GetPosition(owner));
	}*/
}

void CUX::Controls::MediaTransportControls::OnControlPanelPointerEntered(Object ^sender, PointerRoutedEventArgs ^e)
{
	_ControlPanelOpenStates |= TRANSPORT_CTRL_PANEL;
}

void CUX::Controls::MediaTransportControls::OnControlPanelPointerExited(Object ^sender, PointerRoutedEventArgs ^e)
{
	_ControlPanelOpenStates &= ~TRANSPORT_CTRL_PANEL;
}

void CUX::Controls::MediaTransportControls::OnFlyoutOpening(Object ^sender, Object ^args)
{
	_ControlPanelOpenStates |= TRANSPORT_CTRL_FLYOUT;
	OutputDebugMessage(L"Open flyout\n");
}

void CUX::Controls::MediaTransportControls::OnFlyoutClosed(Object ^sender, Object ^args)
{
	_ControlPanelOpenStates &= ~TRANSPORT_CTRL_FLYOUT;
	OutputDebugMessage(L"Close flyout\n");
}

void CUX::Controls::MediaTransportControls::OnLockControlPanelButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	_PanelDspSec = 0;
	_ControlPanelOpenStates = TRANSPORT_CTRL_LOCK;
	DisplayButton->Flyout->Hide();
	ErrorMessage = "";
	VisualStateManager::GoToState(this, "ControlPanelLcokFadeIn", false);
}

void CUX::Controls::MediaTransportControls::OnUnlockControlPanelButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	_ControlPanelOpenStates = 0;
	FadeInControlPanel();
}

void CUX::Controls::MediaTransportControls::OnMoveScreenCompleteButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	EnabledZoomLockMode(false, true);
}

//void CUX::Controls::MediaTransportControls::OnCharacterReceived(Windows::UI::Core::CoreWindow^ sender, Windows::UI::Core::CharacterReceivedEventArgs^ e)
//{
//}

void CUX::Controls::MediaTransportControls::OnCCPositionButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	ClosedCaptionButton->Flyout->Hide();
	ErrorMessage = "";
	VisualStateManager::GoToState(this, "ControlPanelFadeOut", false);
	MoveClosedCaptionPositionStarted(this, ref new WUX::RoutedEventArgs());
	_PanelDspSec = 0;
}

void CUX::Controls::MediaTransportControls::OnImportClosedCaptionTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	ImportClosedCaptionTapped(sender, e);
}

void CUX::Controls::MediaTransportControls::OnFontSizeRatioValueChanged(Object ^sender, WUX::Controls::Primitives::RangeBaseValueChangedEventArgs ^e)
{
	if (CCPMediaElement->ClosedCaptions != nullptr)
	{
		CCPMediaElement->ClosedCaptions->FontSizeRatio = e->NewValue;
	}
}

void CUX::Controls::MediaTransportControls::OnBrightnessButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCPMediaElement->Brightness = 100;
}

void CUX::Controls::MediaTransportControls::OnPlaybackRateButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	PlaybackRate = 1.0;
}

void CUX::Controls::MediaTransportControls::OnPlaybackRateValueChanged(Object ^sender, WUX::Controls::Primitives::RangeBaseValueChangedEventArgs ^e)
{
	PlaybackRate = e->NewValue;
}

void CUX::Controls::MediaTransportControls::OnDisplayRotationButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	DisplayRotationIndex = 0;
}

void CUX::Controls::MediaTransportControls::OnAspectRatioButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	AspectRatioIndex = 0;
}

void CUX::Controls::MediaTransportControls::OnPlaybackRepeatButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e)
{
	PlaybackRepeatIndex = 0;
}

void CUX::Controls::MediaTransportControls::OnDisplayZoomSliderTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	if (CCPMediaElement->ZoomInOut >= 1.01 && !(_ControlPanelOpenStates & TRANSPORT_CTRL_ZOOMLOCK))
	{
		EnabledZoomLockMode(true, true);
	}
}

void CUX::Controls::MediaTransportControls::OnDisplayZoomButtonTapped(Object ^sender, TappedRoutedEventArgs ^e)
{
	CCPMediaElement->ZoomInOut = 1.0;
}

void CUX::Controls::MediaTransportControls::Show()
{
	ControlPanel_ControlPanelVisibilityStates_Border->Opacity = 1;
	ControlPanel_ControlPanelVisibilityStates_Border->IsHitTestVisible = true;
}

void CUX::Controls::MediaTransportControls::Hide()
{
	ControlPanel_ControlPanelVisibilityStates_Border->Opacity = 0;
	//ControlPanel_ControlPanelVisibilityStates_Border->IsHitTestVisible = false;
}

/* 종속성 프로퍼티 등록 */
DEPENDENCY_PROPERTY_REGISTER(ClosedCaptionSubLanguageVisibility, WUX::Visibility, CUX::Controls::MediaTransportControls, WUX::Visibility::Collapsed);
DEPENDENCY_PROPERTY_REGISTER(ErrorMessage, String, CUX::Controls::MediaTransportControls, nullptr);
DEPENDENCY_PROPERTY_REGISTER(TimeText, String, CUX::Controls::MediaTransportControls, nullptr);
DEPENDENCY_PROPERTY_REGISTER(BatteryGlyph, String, CUX::Controls::MediaTransportControls, nullptr);
DEPENDENCY_PROPERTY_REGISTER(BatteryText, String, CUX::Controls::MediaTransportControls, nullptr);
DEPENDENCY_PROPERTY_REGISTER(BatteryVisibility, WUX::Visibility, CUX::Controls::MediaTransportControls, WUX::Visibility::Collapsed);
DEPENDENCY_PROPERTY_REGISTER(MovedTime, Windows::Foundation::TimeSpan, CUX::Controls::MediaTransportControls, TimeSpan());
DEPENDENCY_PROPERTY_REGISTER(MovedPosition, Windows::Foundation::TimeSpan, CUX::Controls::MediaTransportControls, TimeSpan());
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ClosedCaptionSubLanguageIndex, int, CUX::Controls::MediaTransportControls, ref new Box<int>(-1));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(PlaybackRepeatIndex, int, CUX::Controls::MediaTransportControls, ref new Box<int>(0));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(CCSyncSeconds, float, CUX::Controls::MediaTransportControls, 0.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ClosedCaptionIndex, int, CUX::Controls::MediaTransportControls, ref new Box<int>(-1));

void CUX::Controls::MediaTransportControls::OnPlaybackRepeatIndexChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<CUX::Controls::MediaTransportControls^>(sender);
	int value = static_cast<int>(args->NewValue);
	//루프 프로퍼티 설정
	PlaybackRepeats repeat = (PlaybackRepeats)_this->PlaybackRepeatSource->GetAt(value)->Key;
	_this->CCPMediaElement->IsLooping = (repeat == PlaybackRepeats::Once || repeat == PlaybackRepeats::All);
}

void CUX::Controls::MediaTransportControls::OnCCSyncSecondsChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<CUX::Controls::MediaTransportControls^>(sender);
	auto syncSec = static_cast<float>(args->NewValue);
	//현재 마커들 삭제
	_this->CCPMediaElement->ClearMarkers();
	auto secs = (long long)(syncSec * 10000000L);
	//싱크 적용
	_this->CCPMediaElement->SetSubtitleSyncTime(secs);
	//자막 파일 리딩 포인터 이동
	_this->CCPMediaElement->SeekSubtitle(_this->Position.Duration, secs < 0 ? 1 : 0);
}

void CUX::Controls::MediaTransportControls::OnClosedCaptionIndexChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<CUX::Controls::MediaTransportControls^>(sender);
	auto value = static_cast<int>(args->NewValue);
	auto oldValue = static_cast<int>(args->OldValue);
	//최초에는 버퍼를 삭제 하지 않음
	if (oldValue != -1)
	{
		//현재 자막 삭제 및 마커 초기화 
		_this->CCPMediaElement->ClearMarkers();
	}

	//자막 로드 처리
	if (_this->ClosedCaptionSource->Size > 0 && value > -1)
	{
		//코드페이지 초기화
		_this->CCCharsetIndex = 0;
		
		auto cc = _this->ClosedCaptionSource->GetAt(value);
		Windows::Foundation::Collections::PropertySet^ ps = ref new Windows::Foundation::Collections::PropertySet();
		ps->Insert("Key", cc->Key);
		ps->Insert("Options", cc->Payload2);
		
		_this->CCPMediaElement->ConnectSubtitle(ps);
		if (_this->CCPMediaElement->GetSubtitleSourceType() == SubtitleSourceTypes::Internal) //내장 자막 (SubtitleProvider의 스트림 인덱스)
		{
			//자막에 대한 서브 언어 콤보 비활성화 처리 (SMI에서만 사용)
			_this->ClosedCaptionSubLanguageIndex = -1;
			_this->ClosedCaptionSubLanguageVisibility = WUX::Visibility::Collapsed;

			//자막 싱크버튼 비활성화
			VisualStateManager::GoToState(_this, "CCSyncDisabled", false);
		}
		else if (_this->CCPMediaElement->GetSubtitleSourceType() == SubtitleSourceTypes::External)
		{
			auto subLangList = _this->CCPMediaElement->GetSubtitleSubLanguageSource();
			_this->ClosedCaptionSubLanguageSource->Clear();

			if (_this->CCPMediaElement->ClosedCaptions)
			{
				_this->CCPMediaElement->ClosedCaptions->SubLanguageSource = subLangList;
			}

			if (subLangList == nullptr)
			{
				//SMI용 언어 선택 콤보 비활성화
				_this->ClosedCaptionSubLanguageVisibility = WUX::Visibility::Collapsed;
			}
			else
			{
				for (uint32 i = 0; i < subLangList->Size; i++)
				{
					auto lang = subLangList->GetAt(i);
					//SMI용 언어 선택 콤보 추가
					_this->ClosedCaptionSubLanguageSource->Append(ref new KeyName(lang->Code, lang->Name));
				}

				if (_this->ClosedCaptionSubLanguageSource->Size != 1)
				{
					//헤더가 존재하지 않는 경우도 있다. 니미 SAMI 그러니 니가 안되는 거야.
					//무조건 기본값 하나 등록
					auto resource = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
					_this->ClosedCaptionSubLanguageSource->InsertAt(0, ref new KeyName("ALLCC", resource->GetString("Subtitle/Language/Sub/All")));
				}

				//SMI용 언어 선택 콤보 활성화 (1개일 때는 보일 필요가 없음)
				if (_this->ClosedCaptionSubLanguageSource->Size > 1)
				{
					_this->ClosedCaptionSubLanguageVisibility = WUX::Visibility::Visible;
				}
				else
				{
					_this->ClosedCaptionSubLanguageVisibility = WUX::Visibility::Collapsed;
				}

				if (subLangList->Size > 0)
				{
					//SMI용 언어 선택 콤보 선택
					_this->ClosedCaptionSubLanguageIndex = 0;
				}
				else
				{
					_this->ClosedCaptionSubLanguageIndex = -1;
				}
			}

			//자막 싱크버튼 활성화
			VisualStateManager::GoToState(_this, "CCSyncEnabled", false);
		}
	}
}

//서브 자막 언어 (SMI전용)
void CUX::Controls::MediaTransportControls::OnClosedCaptionSubLanguageIndexChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<CUX::Controls::MediaTransportControls^>(sender);
	int value = static_cast<int>(args->NewValue);

	//서브 랭귀지 변경
	if (value >= 0 && value < _this->ClosedCaptionSubLanguageSource->Size)
	{
		auto slc = dynamic_cast<String^>(_this->ClosedCaptionSubLanguageSource->GetAt(value)->Key);
		_this->CCPMediaElement->SetSubtitleSubLanguageCode(slc);
	}
	else
	{
		_this->CCPMediaElement->SetSubtitleSubLanguageCode("ALLCC");
	}

	//현재 마커들 삭제
	_this->CCPMediaElement->ClearMarkers();
}


void CUX::Controls::MediaTransportControls::OnKeyUp(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::KeyEventArgs ^args)
{
	if (CCPMediaElement->Visibility == WUX::Visibility::Collapsed) 
		return;

	switch (args->VirtualKey)
	{
	case Windows::System::VirtualKey::Space:
		if (CCPMediaElement->CurrentState == MediaElementState::Playing)
		{
			CCPMediaElement->Pause();
			VisualStateManager::GoToState(this, "ControlPanelFadeIn", false);
			//마우스 표시
			ShowPointer();
		}
		else
		{
			CCPMediaElement->Play();
			ErrorMessage = "";
			VisualStateManager::GoToState(this, "ControlPanelFadeOut", false);
			//마우스 숨김
			HidePointer();
		}
		break;
	case Windows::System::VirtualKey::Enter:
		ToggleFullScreen();
		break;
	case Windows::System::VirtualKey::Escape:
		CCPMediaElement->IsFullScreen = false;
		VisualStateManager::GoToState(this, "NonFullWindowState", false);
		break;
	case Windows::System::VirtualKey::Left:
		CCPMediaElement->SetSubtitleSeekingState(false);
		if (CCPMediaElement->CurrentState == MediaElementState::Playing)
		{
			//AVSEEK_FLAG_BACKWARD
			CCPMediaElement->SeekSubtitle(Position.Duration, 1);
		}
		break;
	case Windows::System::VirtualKey::Right:
		CCPMediaElement->SetSubtitleSeekingState(false);
		if (CCPMediaElement->CurrentState == MediaElementState::Playing)
		{
			CCPMediaElement->SeekSubtitle(Position.Duration, 0);
		}
		break;
	}
}

void CUX::Controls::MediaTransportControls::OnKeyDown(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::KeyEventArgs ^args)
{
	if (CCPMediaElement->Visibility == WUX::Visibility::Collapsed)
		return;
	
	if (ProgressSlider->FocusState == WUX::FocusState::Keyboard)
	{
		FullWindowButton->Focus(WUX::FocusState::Pointer);
	}

	double value = 0.05;
	switch (args->VirtualKey)
	{
	case Windows::System::VirtualKey::Up:
		if (CCPMediaElement->Volume + value <= 1)
		{
			CCPMediaElement->Volume += value;

			if (CCPMediaElement->Volume > 1)
			{
				CCPMediaElement->Volume = 1.0;
			}
		}
		VisualStateManager::GoToState(this, "GestureVolumeIndicatorAvailable", false);
		break;
	case Windows::System::VirtualKey::Down:
		if (CCPMediaElement->Volume - value >= 0)
		{
			CCPMediaElement->Volume -= value;

			if (CCPMediaElement->Volume < 0)
			{
				CCPMediaElement->Volume = 0;
			}
		}
		VisualStateManager::GoToState(this, "GestureVolumeIndicatorAvailable", false);
		break;
	case Windows::System::VirtualKey::Left:
		CCPMediaElement->SetSubtitleSeekingState(true);
		value = ceil(ProgressSlider->Maximum / 600);
		ProgressSlider->Value -= (value < 1 ? 1 : value);
		break;
	case Windows::System::VirtualKey::Right:
		CCPMediaElement->SetSubtitleSeekingState(true);
		value = ceil(ProgressSlider->Maximum / 200);
		ProgressSlider->Value += (value < 1 ? 1 : value);
		break;
	}
}

void CUX::Controls::MediaTransportControls::OnPointerMoved(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::PointerEventArgs ^args)
{
	if (CCPMediaElement->Visibility == WUX::Visibility::Collapsed)
		return;

	//재생 패널 또는 잠금 패널이 표시되지 않고 있을때, 마우스를 움직인 경우
	if (UnlockControlPanelButton->Opacity == 0
		&& MoveScreenCompleteButton->Opacity == 0
		&& ControlPanel_ControlPanelVisibilityStates_Border->Opacity == 0)
	{
		//무엇인가 화면에 떠있다면 포인터만 보이기
		if (ExistsOpenedPopup())
		{
			ShowPointer();
		}
		else
		{
			if (!args->CurrentPoint->Properties->IsLeftButtonPressed)
			{
				//아무것도 화면에 표시되지 않고 있으면, 재생 패널 또는 잠금 패널 표시
				OnGesturePanelTapped(sender, nullptr);
			}
		}
	}
	_PointerDspSec = 0;
}

bool CUX::Controls::MediaTransportControls::ExistsOpenedPopup()
{
	bool existPopup = false;
	auto popups = VisualTreeHelper::GetOpenPopups(Window::Current);
	for (unsigned int i = 0; i < popups->Size; i++)
	{
		auto popup = popups->GetAt(i);
		auto child = dynamic_cast<FrameworkElement^>(popup->Child);
		if (child != nullptr && child->Name == "CCSettingsPanel" && popup->IsOpen)
		{
			existPopup = true;
			break;
		}
	}
	return existPopup;
}

void CUX::Controls::MediaTransportControls::HidePointer()
{
	_PointerDspSec = 0;
	//재생 패널 또는 잠금 패널이 표시되지 않고 있을때 포인터 숨김
	if (UnlockControlPanelButton->Opacity == 0 
		&& ControlPanel_ControlPanelVisibilityStates_Border->Opacity == 0
		&& !ExistsOpenedPopup())
	{
		if (Window::Current->CoreWindow->PointerCursor != nullptr && _PointerCursor == nullptr)
		{
			_PointerCursor = Window::Current->CoreWindow->PointerCursor;
			Window::Current->CoreWindow->PointerCursor = nullptr;
		}
	}
}

void CUX::Controls::MediaTransportControls::ShowPointer()
{
	//포인터가 숨김 상태이면 표시
	_PointerDspSec = 0;
	if (_PointerCursor != nullptr && Window::Current->CoreWindow->PointerCursor == nullptr)
	{
		Window::Current->CoreWindow->PointerCursor = _PointerCursor;
		_PointerCursor = nullptr;
	}
}

void CUX::Controls::MediaTransportControls::OnClosedCaptionSourceChanged(WFC::IObservableVector<CUX::Controls::KeyName ^> ^sender, WFC::IVectorChangedEventArgs ^event)
{
	if (event->CollectionChange == WFC::CollectionChange::Reset)
	{
		this->ClosedCaptionSubLanguageSource->Clear();
/*
		if (CCPMediaElement->ClosedCaptions && CCPMediaElement->ClosedCaptions->SubLanguageSource)
		{
			CCPMediaElement->ClosedCaptions->SubLanguageSource->Clear();
		}*/
	}
}

void CUX::Controls::MediaTransportControls::OnProgressSliderManipulationStarted(Object ^sender, ManipulationStartedRoutedEventArgs ^e)
{
	CCPMediaElement->SetSubtitleSeekingState(true);
	_PrevPosition = Position;
}

void CUX::Controls::MediaTransportControls::OnProgressSliderManipulationCompleted(Object ^sender, ManipulationCompletedRoutedEventArgs ^e)
{
	CCPMediaElement->SetSubtitleSeekingState(false);
	//이미 시크(Seek) 종료 이벤ㅌ가 발생한 상태이지만, 외부 자막 시크는 IsSeek플래긔 때문에 발생하지 않으므로 강제적으로 호출
	if (CCPMediaElement->CurrentState == MediaElementState::Playing)
	{
		if (_PrevPosition.Duration < Position.Duration)
			CCPMediaElement->SeekSubtitle(Position.Duration, 0);
		if (_PrevPosition.Duration > Position.Duration) //AVSEEK_FLAG_BACKWARD
			CCPMediaElement->SeekSubtitle(Position.Duration, 1);
	}
}



