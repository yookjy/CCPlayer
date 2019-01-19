//
// MediaTransportControls.xaml.h
// Declaration of the MediaTransportControls class
//

#pragma once

#include "MediaTransportControls.g.h"
#include "Converters\Formatter.h"
#include "Helpers\CodePageHelper.h"
#include "Common.h"

using namespace Platform;
using namespace Windows::Foundation;

using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Xaml;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				ref class MediaElement;

				[Windows::Foundation::Metadata::WebHostHidden]
				[Windows::UI::Xaml::Data::Bindable]
				public ref class MediaTransportControls sealed : Windows::UI::Xaml::Data::INotifyPropertyChanged
				{
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, ClosedCaptionSubLanguageVisibility);
					DEPENDENCY_PROPERTY(String^, ErrorMessage);
					DEPENDENCY_PROPERTY(String^, TimeText);
					DEPENDENCY_PROPERTY(String^, BatteryGlyph);
					DEPENDENCY_PROPERTY(String^, BatteryText);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, BatteryVisibility);
					
					DEPENDENCY_PROPERTY(Windows::Foundation::TimeSpan, MovedTime);
					DEPENDENCY_PROPERTY(Windows::Foundation::TimeSpan, MovedPosition);

					DEPENDENCY_PROPERTY_WITH_EVENT(int, ClosedCaptionSubLanguageIndex);
					DEPENDENCY_PROPERTY_WITH_EVENT(int, ClosedCaptionIndex);
					DEPENDENCY_PROPERTY_WITH_EVENT(int, PlaybackRepeatIndex);
					DEPENDENCY_PROPERTY_WITH_EVENT(float, CCSyncSeconds);
					
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, ClosedCaptionSource);
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, ClosedCaptionSubLanguageSource);
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, AudioStreamLanguageSource);
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, AspectRatioSource);
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, DisplayRotationSource);
					DEFAULT_READONLY_PROPERTY(Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^, PlaybackRepeatSource);
					
				private:
					Platform::WeakReference _CCPMediaElement;
					DispatcherTimer^ _PresentationTimer;
					TimeSpan _RunningTime;
					TimeSpan _PrevPosition;
					
					bool _IsSeeking;
					int _Position;
					int _ControlPanelOpenStates;
					int _GestureMode;
					double _PanelDspSec;
					double _PointerDspSec;
					double _TimerTick;
					double _PrevZoomScale;
					double _PlaybackRate;
					String^ _DecoderErrorMessage;
										
					Windows::UI::Core::CoreCursor^ _PointerCursor;
					Windows::Storage::StorageFolder^ _CurrentFolder;
					String^ _CurrentFileName;

					Windows::Globalization::DateTimeFormatting::DateTimeFormatter^ _TimeFormat;

					void OnPropertyChanged(Platform::String^ propertyName);

					void OnCloseButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnFastRewindButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnPlayPauseButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnFastForwardButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnFullWindowButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnVolumeMuteButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnAudioVolumeBoosterButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnAudioBalanceButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnAudioSyncTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnBrightnessButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnPlaybackRateButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnDisplayZoomSliderTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnDisplayZoomButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnLockControlPanelButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnUnlockControlPanelButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnMoveScreenCompleteButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnDisplayRotationButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnAspectRatioButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnPlaybackRepeatButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCPositionButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCCharsetButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSyncResetButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSyncFastButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSyncFastHalfButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSyncSlowHalfButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSyncSlowButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnImportClosedCaptionTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSettingsOpenButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnPreviousMediaButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnNextMediaButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnSubtitleOnOffButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnGesturePanelTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnGesturePanelDoubleTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::DoubleTappedRoutedEventArgs ^e);
					void OnGesturePanelRightTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::RightTappedRoutedEventArgs ^e);
					void OnProgressSliderManipulationStarted(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationStartedRoutedEventArgs ^e);
					void OnProgressSliderManipulationCompleted(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationCompletedRoutedEventArgs ^e);

					void OnGesturePanelManipulationStarting(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationStartingRoutedEventArgs ^e);
					void OnGesturePanelManipulationStarted(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationStartedRoutedEventArgs ^e);
					void OnGesturePanelManipulationDelta(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationDeltaRoutedEventArgs ^e);
					void OnGesturePanelManipulationCompleted(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationCompletedRoutedEventArgs ^e);

					void OnControlPanelPointerEntered(Platform::Object ^sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs ^e);
					void OnControlPanelPointerExited(Platform::Object ^sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs ^e);

					void OnHWDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnHybridDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnSWDecoderButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					
					void OnFontSizeRatioValueChanged(Platform::Object ^sender, Windows::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs ^e);
					void OnPlaybackRateValueChanged(Platform::Object ^sender, Windows::UI::Xaml::Controls::Primitives::RangeBaseValueChangedEventArgs ^e);

					void OnFailedSubtitleDecoding(Platform::Object ^sender, long long args);
					void OnPresentationTimerTick(Platform::Object ^sender, Platform::Object ^args);
					void OnFlyoutOpening(Platform::Object ^sender, Platform::Object ^args);
					void OnFlyoutClosed(Platform::Object ^sender, Platform::Object ^args);

					void OnKeyUp(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::KeyEventArgs ^args);
					void OnKeyDown(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::KeyEventArgs ^args);
					//void OnCharacterReceived(Windows::UI::Core::CoreWindow^ sender, Windows::UI::Core::CharacterReceivedEventArgs^ e);
					void OnPointerMoved(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::PointerEventArgs ^args);				

					void OnClosedCaptionSourceChanged(Windows::Foundation::Collections::IObservableVector<CCPlayer::UWP::Xaml::Controls::KeyName ^> ^sender, Windows::Foundation::Collections::IVectorChangedEventArgs ^event);

					Windows::Foundation::EventRegistrationToken _KeyUpListenerToken;
					Windows::Foundation::EventRegistrationToken _KeyDownListenerToken;
					Windows::Foundation::EventRegistrationToken _PointerMovedListenerToken;

					void RegisterEvents();
					void ToggleFullScreen();
					void FadeInControlPanel();
					bool ExistsOpenedPopup();

					CStopWatch sw;
				internal:
					void HidePointer();
					void ShowPointer();
					void EnabledZoomLockMode(bool, bool);

				public:
					MediaTransportControls();
					virtual ~MediaTransportControls();
					
					property bool IsKeepOpenState;
					property TimeSpan RunningTime { TimeSpan get(); }
					property TimeSpan RemainingTime { TimeSpan get(); }
					property IVector<CCPlayer::UWP::Xaml::Helpers::CodePage^>^ CCCharsetSource { IVector<CCPlayer::UWP::Xaml::Helpers::CodePage^>^ get(); }
					
					property TimeSpan Position { TimeSpan get(); void set(TimeSpan value); }
					property double PlaybackRate { double get(); void set(double value); }
					property int CCCharsetIndex { int get(); void set(int value); }
					property int AspectRatioIndex { int get(); void set(int value); }
					property int DisplayRotationIndex { int get(); void set(int value); }
					property MediaElement^ CCPMediaElement
					{
						MediaElement^ get() { return _CCPMediaElement.Resolve<MediaElement>(); }
						void set(MediaElement^ value) { _CCPMediaElement = value; OnPropertyChanged("CCPMediaElement"); }
					};
					
					void AppendClosedCaption(CCPlayer::UWP::Xaml::Controls::KeyName^ value);
					void AppendAudioStream(CCPlayer::UWP::Xaml::Controls::KeyName^ value);
					void EnableLimeEngine(bool enabled);
					void OnMediaOpened(CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ value);
					void OnMediaEnded();
					void OnMediaFailed(int errorCode, CCPlayer::UWP::Common::Interface::IAVDecoderConnector^ value);
					void OnSeekCompleted();
					void OnPlaying();
					void OnPaused();
					void OnOpening();
					void OnBuffering();
					void OnStopped();
					void OnClosed();
					
					void ApplyComboBoxPatch(DependencyProperty^ dp);
					
					void Show();
					void Hide();

					event Windows::UI::Xaml::Input::TappedEventHandler^ CloseButtonTapped;
					event Windows::UI::Xaml::Input::TappedEventHandler^ CCSettingsOpenButtonTapped;
					event Windows::UI::Xaml::Input::TappedEventHandler^ PreviousMediaButtonTapped;
					event Windows::UI::Xaml::Input::TappedEventHandler^ NextMediaButtonTapped;
					event Windows::UI::Xaml::Input::TappedEventHandler^ ImportClosedCaptionTapped;

					virtual event Windows::UI::Xaml::Data::PropertyChangedEventHandler^ PropertyChanged;
					event RoutedEventHandler^ MoveClosedCaptionPositionStarted;

					void OnLoaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnUnloaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					
					
};
			}
		}
	}
}
