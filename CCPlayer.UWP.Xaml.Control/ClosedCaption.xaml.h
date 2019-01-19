//
// ClosedCaption.xaml.h
// Declaration of the ClosedCaption class
//

#pragma once

#include "ClosedCaption.g.h"
#include "Common.h"
#include <regex>
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Documents;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class ClosedCaption sealed
				{
					//const std::wregex paragraphStartTag(L"\\s*<\\s*Paragraph\\s+(.+?)\\s*>", std::regex_constants::ECMAScript | std::regex_constants::icase);
					//const std::wregex paragraphEndTag(L"\\s*<\\s*/\\s*Paragraph\\s*>\\s*", std::regex_constants::ECMAScript | std::regex_constants::icase);
					const std::wregex rubyProxyTag;
					const std::wregex fontFamilyTag;
					const std::wregex fontSizeTag;
					const std::wregex ForegroundExp;
					const std::wregex FontStyleExp;
					const std::wregex FontWeightExp;
					
					DEPENDENCY_PROPERTY(double, ShadowDepth);
					DEPENDENCY_PROPERTY(double, OutlineDepth);
					DEPENDENCY_PROPERTY(double, OutlineNegativeDepth);
					DEPENDENCY_PROPERTY(double, BaseFontSize);
					DEPENDENCY_PROPERTY(Object^, FontFamiliesSource);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Media::Brush^, Outline);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, BackgroundVisibility);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, ShadowVisibility);
					DEPENDENCY_PROPERTY(Windows::UI::Xaml::Visibility, OutlineVisibility);
					DEPENDENCY_PROPERTY(String^, SelectedSubLanguageCode);
					DEPENDENCY_PROPERTY(Object^, SubLanguageSource);
					
					DEPENDENCY_PROPERTY_WITH_EVENT(bool, EnableStyleOverride);
					DEPENDENCY_PROPERTY_WITH_EVENT(Windows::UI::Text::FontStyle, FontStyleOverride);
					DEPENDENCY_PROPERTY_WITH_EVENT(Windows::UI::Text::FontWeight, FontWeightOverride);
					DEPENDENCY_PROPERTY_WITH_EVENT(Windows::UI::Xaml::Media::Brush^, ForegroundOverride);
					DEPENDENCY_PROPERTY_WITH_EVENT(double, FontSizeRatio);
					DEPENDENCY_PROPERTY_WITH_EVENT(Windows::Data::Json::JsonObject^, JsonData);
					DEPENDENCY_PROPERTY_WITH_EVENT(Size, DisplayVideoSize);
					DEPENDENCY_PROPERTY_WITH_EVENT(Size, NaturalVideoSize);

				private:
					static ClosedCaption^ FindTop(DependencyObject^ obj);
					static void ChangeFontSize(TextElement^ obj, double ratio);

					void SetBaseFontSize();

					Windows::Foundation::Collections::IVector<Windows::UI::Xaml::Controls::RichTextBlock^>^ richTextList;
					Windows::Data::Json::JsonArray^ ccJsonArray;
					Windows::UI::Xaml::Media::Brush^ foregroundBackup;
					
					Thickness _Margin;
					Size _CurrentVideoSize;
					Size _NaturalSubtitleSize;
					float _DefaultFontSize;

				public:
					ClosedCaption();
					virtual ~ClosedCaption();

					void OnLoaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e);
					void ClearClosedCaption(TimeSpan position);
				};
			}
		}
	}

}
