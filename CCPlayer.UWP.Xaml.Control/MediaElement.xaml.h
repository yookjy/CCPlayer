//
// MediaElement.xaml.h
// Declaration of the MediaElement class
//

#pragma once

#include "pch.h"
#include "MediaElement.g.h"
#include "MediaElementCore.h"
#include "StateTriggers\BooleanDataTrigger.h"
//#include "Common.h"

using namespace Concurrency;
using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Graphics::Display;
using namespace Windows::Media::Core;
using namespace Windows::Storage::Streams;
using namespace Windows::System::Display;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Media;

#define MEDIA_ELEMENT_FUNCTION_VOID(NAME) \
void NAME() \
{ if (CheckLimeEngine()) { LimeME->##NAME(); } else { WindowsME->##NAME();} }

#define MEDIA_ELEMENT_FUNCTION(RETURN_TYPE, PARAMENTER_TYPE, NAME) \
RETURN_TYPE NAME(PARAMENTER_TYPE value) \
{ if (CheckLimeEngine()) { return LimeME->##NAME(value); } else { return WindowsME->##NAME(value);} }

#define MEDIA_ELEMENT_PROPERTY_GET(TYPE, NAME) \
property TYPE NAME \
{ TYPE get () { if (CheckLimeEngine()) { return LimeME->##NAME; } else { return WindowsME->##NAME; } } }

#define MEDIA_ELEMENT_PROPERTY(TYPE, NAME) \
property TYPE NAME \
{\
	TYPE get () { if (CheckLimeEngine()) { return LimeME->##NAME; } else { return WindowsME->##NAME; } } \
	void set (TYPE value) { if (CheckLimeEngine()) { LimeME->##NAME = value; } else { WindowsME->##NAME = value; } } \
}

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				ref class ClosedCaptions;

				enum class MediaOpenStatus
				{
					None,
					NewSource,
					DecoderChanging,
					AudioStreamChanging,
				};

				struct MediaOpenDataStore
				{
				public:
					int AudioStreamLanguageIndex;
					int EnforceAudioStreamId;
					int ClosedCaptionIndex;
					TimeSpan Position;
					double AudioVolumeBoost;
					MediaOpenStatus Status;
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public interface class IMediaItem
				{
					property Platform::String^ DisplayName;
					property Windows::UI::Xaml::Media::ImageSource^ ImageSource
					{
						Windows::UI::Xaml::Media::ImageSource^ get();
					}
				};
				
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class MediaElement sealed : Windows::UI::Xaml::Data::INotifyPropertyChanged
				{
					DEPENDENCY_PROPERTY_WITH_EVENT(bool, UseClosedCaptions);
					DEPENDENCY_PROPERTY_WITH_EVENT(bool, EnabledHorizontalMirror);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, ZoomInOut);
					DEPENDENCY_PROPERTY_WITH_EVENT(Point, ZoomMove);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, Balance);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, Volume);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, VolumeBoost);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, AudioSync);
					DEPENDENCY_PROPERTY_WITH_EVENT(CCPlayer::UWP::Xaml::Controls::AspectRatios, AspectRatio);
					DEPENDENCY_PROPERTY_WITH_EVENT(CCPlayer::UWP::Xaml::Controls::DisplayRotations, DisplayRotation);
					DEPENDENCY_PROPERTY_WITH_EVENT(CCPlayer::UWP::Xaml::Controls::ClosedCaptions^, ClosedCaptions);
					DEPENDENCY_PROPERTY_WITH_EVENT(IMediaItem^, PreviousMediaItem);
					DEPENDENCY_PROPERTY_WITH_EVENT(IMediaItem^, NextMediaItem);
					DEPENDENCY_PROPERTY_WITH_EVENT(int, AudioStreamLanguageIndex);
					DEPENDENCY_PROPERTY_WITH_EVENT(int, CCCodePage);
					DEPENDENCY_PROPERTY_WITH_EVENT(int, CCDefaultCodePage);
					
					DEPENDENCY_PROPERTY(CCPlayer::UWP::Common::Codec::DecoderTypes, DecoderType);
					DEPENDENCY_PROPERTY(bool, EnabledRotationLock);
					DEPENDENCY_PROPERTY(bool, UseFlipToPause);
					DEPENDENCY_PROPERTY(bool, UseGpuShader);
					DEPENDENCY_PROPERTY(bool, UseAttachment);
					DEPENDENCY_PROPERTY(int, SeekTimeInterval);
					DEPENDENCY_PROPERTY(double, Brightness);
					DEPENDENCY_PROPERTY(double, CCPosition);
					DEPENDENCY_PROPERTY(String^, Title);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::VerticalAlignment, CCVerticalAlignment);

				public:
					MediaElement();
					virtual ~MediaElement();

					MEDIA_ELEMENT_PROPERTY(bool, AutoPlay);
					MEDIA_ELEMENT_PROPERTY(bool, IsLooping);
					MEDIA_ELEMENT_PROPERTY(bool, IsMuted);
					MEDIA_ELEMENT_PROPERTY(bool, RealTimePlayback);
					MEDIA_ELEMENT_PROPERTY(Platform::IBox<int>^, AudioStreamIndex);
					MEDIA_ELEMENT_PROPERTY(double, DefaultPlaybackRate);
					MEDIA_ELEMENT_PROPERTY(Windows::Foundation::TimeSpan, Position);
					MEDIA_ELEMENT_PROPERTY(double, PlaybackRate);
					
					MEDIA_ELEMENT_PROPERTY_GET(Windows::UI::Xaml::Media::MediaElementState, CurrentState);
					MEDIA_ELEMENT_PROPERTY_GET(Windows::UI::Xaml::Media::TimelineMarkerCollection^, Markers);
					MEDIA_ELEMENT_PROPERTY_GET(Duration, NaturalDuration);
					MEDIA_ELEMENT_PROPERTY_GET(int, NaturalVideoHeight);
					MEDIA_ELEMENT_PROPERTY_GET(int, NaturalVideoWidth);
					MEDIA_ELEMENT_PROPERTY_GET(int, AudioStreamCount);
					MEDIA_ELEMENT_PROPERTY_GET(bool, IsAudioOnly);
					MEDIA_ELEMENT_PROPERTY_GET(bool, CanPause);
					MEDIA_ELEMENT_PROPERTY_GET(bool, CanSeek);

					property Windows::UI::Xaml::Media::Stretch Stretch { Windows::UI::Xaml::Media::Stretch get(); void set(Windows::UI::Xaml::Media::Stretch); }
					property bool IsFullWindow { bool get(); void set(bool); }
					property bool IsFullScreen { bool get(); void set(bool); }
					property bool UseLimeEngine { bool get(); void set(bool); }
					property int MediaErrorCode { int get(); }
					property CCPlayer::UWP::Xaml::Controls::MediaTransportControls^ MediaTransportControls { CCPlayer::UWP::Xaml::Controls::MediaTransportControls^ get() { return TC; } }
					property Uri^ Source { Uri^ get(); void set(Uri^); }
					property int SeekTimeIntervalValue;

					virtual event Windows::UI::Xaml::Data::PropertyChangedEventHandler^ PropertyChanged;
					event RoutedEventHandler^ CurrentStateChanged;
					event Media::TimelineMarkerRoutedEventHandler^ MarkerReached;
					event RoutedEventHandler^ MediaEnded;
					event RoutedEventHandler^ MediaFailed;
					event RoutedEventHandler^ MediaOpened;
					event RoutedEventHandler^ SeekCompleted;
					event Input::TappedEventHandler^ CloseButtonTapped;
					event Input::TappedEventHandler^ CCSettingsOpenButtonTapped;
					event Input::TappedEventHandler^ PreviousMediaButtonTapped;
					event Input::TappedEventHandler^ NextMediaButtonTapped;
					event CCPlayer::UWP::Common::Codec::AttachmentPopulatedEventHandler^ AttachmentPopulated;
					event CCPlayer::UWP::Common::Codec::AttachmentCompletedEventHandler^ AttachmentCompleted;
					
					MEDIA_ELEMENT_FUNCTION_VOID(Play);
					MEDIA_ELEMENT_FUNCTION_VOID(Pause);
					
					MEDIA_ELEMENT_FUNCTION(String^, int, GetAudioStreamLanguage);
					MEDIA_ELEMENT_FUNCTION(Media::MediaCanPlayResponse, String ^, CanPlayType);
					
					
					void Stop();
					void SetSource(IRandomAccessStream^ stream, String^ mimeType);
					void AddClosedCaptionStreamSource(IRandomAccessStream^ stream);
					void AddClosedCaptionStreamSources(Windows::Foundation::Collections::IVector<IRandomAccessStream^>^ streamList);
					void AddClosedCaptionUriSource(String^ uri, int codePage);
					void AddClosedCaptionUriSources(Windows::Foundation::Collections::IVector<String^>^ uriList, int codePage);
					void SetFilePathInfo(Windows::Storage::StorageFolder^ currFolder, String^ currFileNameWidthoutExtenion);
					
					void Trim();
					void ClearMarkers();
					void ApplyComboBoxPatch(DependencyProperty^ dp);

				internal:
					MediaElementCore^ LimeME;

					void ChangeDecoder(CCPlayer::UWP::Common::Codec::DecoderTypes decoderType);
					void Seek(long long);
					/* 자막관련 */
					void SeekSubtitle(long long, int);
					void SetSubtitleSeekingState(bool);
					void SetSubtitleSyncTime(long long);
					void SetSubtitleSubLanguageCode(String^);
					void SetSubtitleCodePage(int);
					void SetSubtitleDefaultCodePage(int);
					void ConnectSubtitle(Windows::Foundation::Collections::PropertySet^);
					void ConsumeSubtitle(long long pts);
					Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>^ GetSubtitleSubLanguageSource();
					CCPlayer::UWP::Common::Codec::SubtitleSourceTypes GetSubtitleSourceType();
					/* 자막관련 */
				private:
					
					//전역 변수
					bool _IsUnloaded;
					bool _IsFullScreen;
					bool _UseLimeEngine;
					bool _IsPausedByFlip;
					bool _IsKeepMediaControlPanelOpenState;
					int _CodePage;
					//bool _IsEntryDevice;
					double _PrevActWidth;
					double _PrevActHeight;
					int _MediaErrorCode;
					String^ _MimeType;
					String^ _CurrentFileName;
					DisplayRequest^ _DisplayRequest;
					IRandomAccessStream^ _Stream;
					MediaOpenDataStore _MediaOpenDataStore;
					Uri^ _Uri;

					Windows::Devices::Sensors::SimpleOrientationSensor^ _SimpleOrientationSensor;

					Windows::Foundation::EventRegistrationToken CloseButtonTappedToken;
					Windows::Foundation::EventRegistrationToken CCSettingsOpenButtonTappedToken;
					Windows::Foundation::EventRegistrationToken PreviousMediaButtonTappedToken;
					Windows::Foundation::EventRegistrationToken NextMediaButtonTappedToken;

					Windows::Foundation::EventRegistrationToken SubtitleFoundEventToken;
					Windows::Foundation::EventRegistrationToken SubtitlePopulatedEventToken;

					Windows::Foundation::EventRegistrationToken MediaOpenedToken;
					Windows::Foundation::EventRegistrationToken MediaEndedToken;
					Windows::Foundation::EventRegistrationToken MediaFailedToken;
					Windows::Foundation::EventRegistrationToken MarkerReachedToken;
					Windows::Foundation::EventRegistrationToken SeekCompletedToken;
					Windows::Foundation::EventRegistrationToken CurrentStateChangedToken;

					Windows::Foundation::EventRegistrationToken MFMediaOpenedToken;
					Windows::Foundation::EventRegistrationToken MFMediaEndedToken;
					Windows::Foundation::EventRegistrationToken MFMediaFailedToken;
					Windows::Foundation::EventRegistrationToken MFMarkerReachedToken;
					Windows::Foundation::EventRegistrationToken MFSeekCompletedToken;
					Windows::Foundation::EventRegistrationToken MFCurrentStateChangedToken;

					Windows::Foundation::Collections::PropertySet^ _MediaFoundationPropertySet;
					Windows::Media::MediaExtensionManager^ _MediaExtensionManager;

					Windows::Storage::StorageFolder^ _CurrentFolder;
					Windows::UI::Xaml::HorizontalAlignment _PrevHAlign;
					Windows::UI::Xaml::VerticalAlignment _PrevVAlign;

					void OnPropertyChanged(Platform::String^ propertyName);
					void RegisterDecoders();
					bool CheckLimeEngine();
					void OpenMedia();
					void SetSeekTimeInterval(TimeSpan runningTime);
					void AddClosedCaptions(CCPlayer::UWP::Common::Interface::IMediaInformation^ mediaInformation, Object^ param, Windows::Foundation::Collections::PropertySet^ propertie);
					task<void> ImportClosedCaptionAsync();
					void SelectLastClosedCaptionIndex(int prevClosedCaptionSize);
					String^ GetCodecDisplayName(CCPlayer::UWP::Common::Codec::CodecInformation^ codecInfo);
					String^ GetAudioStreamDisplayName(CCPlayer::UWP::Common::Codec::CodecInformation^ codecInfo);
					String^ GetAudioStreamDisplayName(String^ langCode);
					Windows::UI::Xaml::Controls::SwapChainPanel^ GetZoomPanel();

					void InitSource();
					void AttachMediaElementEvents();
					void DetatchMediaElementEvents();
					void SetMediaExtensionParameters();

					void OnMarkerReached(Platform::Object ^sender, Windows::UI::Xaml::Media::TimelineMarkerRoutedEventArgs ^e);
					void OnMediaOpened(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnMediaEnded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnMediaFailed(Platform::Object ^sender, Windows::UI::Xaml::ExceptionRoutedEventArgs ^e);
					void OnMFMediaFailed(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnCurrentStateChanged(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnSeekCompleted(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					//MediaElement 이벤트
					void OnSizeChanged(Platform::Object ^sender, Windows::UI::Xaml::SizeChangedEventArgs ^e);
					void OnLoaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnUnloaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					//MediaTransportControl 이벤트
					void OnCloseButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnCCSettingsOpenButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnPreviousMediaButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnNextMediaButtonTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnImportClosedCaptionTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnClosedCaptionMovePositionStarted(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					//FFmpge 이벤트
					void OnSubtitlePopulatedEvent(CCPlayer::UWP::Common::Interface::ISubtitleDecoderConnector ^sender, Windows::UI::Xaml::Media::TimelineMarker ^timelineMarker, Windows::Foundation::Collections::IMap<String^, CCPlayer::UWP::Common::Codec::ImageData^>^ subtitleImageMap);
					void OnAttachmentPopulatedEvent(CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ sender, CCPlayer::UWP::Common::Codec::AttachmentData^ attachment);
					void OnAttachmentCompletedEvent(CCPlayer::UWP::Common::Interface::IAttachmentDecoderConnector^ sender, Platform::Object^ attachment);
					//ClosedCaption 이벤트
					void OnClosedCaptionMovePositionCompleted(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnOrientationChanged(Windows::Devices::Sensors::SimpleOrientationSensor^ sender, Windows::Devices::Sensors::SimpleOrientationSensorOrientationChangedEventArgs^ args);

					//void OnActivated(Platform::Object ^sender, Windows::UI::Core::WindowActivatedEventArgs^ e);
};
			}
		}
	}
}
