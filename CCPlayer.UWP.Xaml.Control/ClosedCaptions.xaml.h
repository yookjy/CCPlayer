//
// ClosedCaptionArea.xaml.h
// Declaration of the ClosedCaptionArea class
//

#pragma once

#include "ClosedCaptions.g.h"
#include "Common.h"

using namespace Platform;
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
				public ref class ClosedCaptions sealed
				{
					DEPENDENCY_PROPERTY(Object^, FontFamiliesSource);
					DEPENDENCY_PROPERTY(bool, EnableStyleOverride);
					DEPENDENCY_PROPERTY(Windows::UI::Text::FontStyle, FontStyleOverride);
					DEPENDENCY_PROPERTY(Windows::UI::Text::FontWeight, FontWeightOverride);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Media::Brush^, ForegroundOverride);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, BackgroundVisibility);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, ShadowVisibility);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, OutlineVisibility);
					DEPENDENCY_PROPERTY(double, BaseFontSize);
					DEPENDENCY_PROPERTY(double, FontSizeRatio);
					DEPENDENCY_PROPERTY(double, DefaultSubtitlePosition);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::VerticalAlignment, DefaultSubtitleVerticalAlignment);
					DEPENDENCY_PROPERTY(Point, DefaultSubtitleBlockOrigin);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataLT);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataCT);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataRT);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataLC);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataCC);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataRC);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataLB);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataCB);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataRB);
					DEPENDENCY_PROPERTY(Windows::Data::Json::JsonObject^, JsonDataIMG);
					DEPENDENCY_PROPERTY(Size, DisplayVideoSize);
					DEPENDENCY_PROPERTY(Size, NaturalVideoSize);
					DEPENDENCY_PROPERTY(String^, SelectedSubLanguageCode);
					DEPENDENCY_PROPERTY(Object^, SubLanguageSource);

					property MediaElement^ CCPMediaElement
					{
						MediaElement^ get() { return _CCPMediaElement.Resolve<MediaElement>(); }
						void set(MediaElement^ value) { _CCPMediaElement = value; }
					};
					
				public:
					ClosedCaptions();
					virtual ~ClosedCaptions();

					void ClearTextClosedCaption();
					void ClearImageClosedCaption();
					void SetClosedCaption(Windows::Data::Json::JsonObject^ value);
					String^ MergeTimelineMarkerText(String^ foundData, String^ appendData);
					void SetTimelineMarkerText(String^ value);
					void AppendImageSubtitles(Windows::Foundation::Collections::IMap<String^, CCPlayer::UWP::Common::Codec::ImageData^>^ subtitleImageMap);

					event RoutedEventHandler^ MoveClosedCaptionPositionCompleted;

				private:
					Platform::WeakReference _CCPMediaElement;
					DispatcherTimer^ _Timer;
					bool CheckBeforeMerge(Windows::Data::Json::JsonObject^ prevJson, Windows::Data::Json::JsonObject^ currJson, int rectIndex);
					void SetCCPosition(Windows::UI::Xaml::Controls::Panel^ parent, Windows::UI::Xaml::FrameworkElement^ child, double translationY);
					DependencyObject^ FindUIElement(DependencyObject^ element);
					Windows::Foundation::EventRegistrationToken _WindowSizeChangedToken;
					
					//이벤트 처리기
					void OnTextClosedCaptionContentsTapped(Platform::Object ^sender, Windows::UI::Xaml::Input::TappedRoutedEventArgs ^e);
					void OnTextClosedCaptionPositionPanelManipulationDelta(Platform::Object ^sender, Windows::UI::Xaml::Input::ManipulationDeltaRoutedEventArgs ^e);

				internal:
					void UnlockMovePosition();
					void DoStart();
					void DoStop();

					void OnTick(Platform::Object ^sender, Platform::Object ^args);
					void OnSizeChanged(Platform::Object ^sender, Windows::UI::Xaml::SizeChangedEventArgs ^e);
					void OnLoaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void OnUnloaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					CStopWatch sw;
					void OnWindowSizeChanged(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::WindowSizeChangedEventArgs ^args);
				};
			}
		}
	}
}
