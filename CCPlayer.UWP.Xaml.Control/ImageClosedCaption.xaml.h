//
// ImageClosedCaption.xaml.h
// Declaration of the ImageClosedCaption class
//

#pragma once

#include "ImageClosedCaption.g.h"
#include "Common.h"

using namespace Windows::UI::Xaml;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class ImageClosedCaption sealed
				{
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, BackgroundVisibility);

					DEPENDENCY_PROPERTY_WITH_EVENT(Windows::Data::Json::JsonObject^, JsonData);
					DEPENDENCY_PROPERTY_WITH_EVENT(Size, DisplayVideoSize);
					DEPENDENCY_PROPERTY_WITH_EVENT(Size, NaturalVideoSize);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, FontSizeRatio);

				private:
					void SetPosition();
					void SetScale();
					property double VideoSizeRatio { double get(); }
					double _VideoEncodedSizeRatio;
					Size _CurrentImageSize;
				internal:
					Windows::Foundation::Collections::IMap<String^, CCPlayer::UWP::Common::Codec::ImageData^>^ SubtitleImageMap;

				public:
					ImageClosedCaption();
					virtual ~ImageClosedCaption();

					void ClearClosedCaption(TimeSpan position);
				};
			}
		}
	}
}