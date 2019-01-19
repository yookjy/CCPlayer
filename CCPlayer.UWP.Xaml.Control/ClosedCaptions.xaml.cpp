//
// ClosedCaptions.xaml.cpp
// Implementation of the ClosedCaptions class
//

#include "pch.h"
#include "ClosedCaptions.xaml.h"
#include "ClosedCaption.xaml.h"
#include "ImageClosedCaption.xaml.h"
#include "MediaTransportControls.xaml.h"
#include "MediaElement.xaml.h"

using namespace Windows::Foundation;
using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::Xaml::Controls;
//using namespace CCPlayer::UWP::FFmpeg::Bridge;
using namespace Windows::Data::Json;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Controls::Primitives;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::Xaml::Navigation;


JsonValueType GuessJsonValueType(String^ jsonString)
{
	JsonValueType resultType = JsonValueType::Null;
	auto data = jsonString->Data();
	auto op = data[0];
	auto ed = data[jsonString->Length() - 1];

	JsonArray^ arr = nullptr;
	if (op == L'[' && ed == L']')
	{
		//JsonArray
		resultType = JsonValueType::Array;
	}
	else if (op == L'{' && ed == '}')
	{
		resultType = JsonValueType::Object;
	}
	return resultType;
}

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

ClosedCaptions::ClosedCaptions()
{
	InitializeComponent();

	TimeSpan ts;
	ts.Duration = (long long)(0.1 * 10000000L); //0.1초
	_Timer = ref new DispatcherTimer();
	_Timer->Interval = ts;

	TextClosedCaptionContents->Tapped += ref new TappedEventHandler(this, &ClosedCaptions::OnTextClosedCaptionContentsTapped);
	TextClosedCaptionPositionPanel->ManipulationDelta += ref new ManipulationDeltaEventHandler(this, &ClosedCaptions::OnTextClosedCaptionPositionPanelManipulationDelta);

	_Timer->Tick += ref new EventHandler<Platform::Object ^>(this, &ClosedCaptions::OnTick);
	this->SizeChanged += ref new Windows::UI::Xaml::SizeChangedEventHandler(this, &ClosedCaptions::OnSizeChanged);
	this->Loaded += ref new Windows::UI::Xaml::RoutedEventHandler(this, &ClosedCaptions::OnLoaded);
	this->Unloaded += ref new Windows::UI::Xaml::RoutedEventHandler(this, &ClosedCaptions::OnUnloaded);
}
ClosedCaptions::~ClosedCaptions()
{
	OutputDebugMessage(L"Called constructor of the ClosedCaptions\n");
	if (_Timer->IsEnabled)
	{
		_Timer->Stop();
	}

	delete ClosedCaptionLT ;
	delete ClosedCaptionCT;
	delete ClosedCaptionRT;

	delete ClosedCaptionLC;
	delete ClosedCaptionCC;
	delete ClosedCaptionRC;

	delete ClosedCaptionLB;
	delete ClosedCaptionCB;
	delete ClosedCaptionRB;

	delete ImageClosedCaption;
}

void ClosedCaptions::ClearTextClosedCaption()
{
	JsonDataLT = nullptr;
	JsonDataCT = nullptr;
	JsonDataRT = nullptr;
	JsonDataLC = nullptr;
	JsonDataCC = nullptr;
	JsonDataRC = nullptr;
	JsonDataLB = nullptr;
	JsonDataRB = nullptr;
	JsonDataCB = nullptr;
}

void ClosedCaptions::ClearImageClosedCaption()
{
	JsonDataIMG = nullptr;
}

void ClosedCaptions::SetClosedCaption(JsonObject^ value)
{
	String^ pos = nullptr;
	auto rect = value->GetNamedArray("Rects")->GetObjectAt(0);
	auto type = (SubtitleContentTypes)(int)rect->GetNamedNumber("Type");

	if (type == SubtitleContentTypes::Bitmap || type == SubtitleContentTypes::None)
	{
		return;
	}

	//텍스트 자막
 	if (rect->HasKey("Style") && rect->GetNamedObject("Style")->HasKey("ClosedCaptionPosition"))
	{
		pos = rect->GetNamedObject("Style")->GetNamedString("ClosedCaptionPosition");
	}

	if (pos == "LT")
	{
		JsonDataLT = value;
	}
	else if (pos == "CT")
	{
		JsonDataCT = value;
	}
	else if (pos == "RT")
	{
		JsonDataRT = value;
	}
	else if (pos == "LC")
	{
		JsonDataLC = value;
	}
	else if (pos == "CC")
	{
		JsonDataCC = value;
	}
	else if (pos == "RC")
	{
		JsonDataRC = value;
	}
	else if (pos == "LB")
	{
		JsonDataLB = value;
	}
	else if (pos == "RB")
	{
		JsonDataRB = value;
	}
	else
	{
		JsonDataCB = value;
	}
}

String^ ClosedCaptions::MergeTimelineMarkerText(String^ foundData, String^ appendData)
{
	JsonObject^ currJo = nullptr;
	JsonObject::TryParse(appendData, &currJo);

	if (foundData != nullptr && foundData->Length() > 0)
	{
		JsonArray^ arr = nullptr;
		JsonValueType type = GuessJsonValueType(foundData);
		if (type == JsonValueType::Array)
		{
			JsonArray::TryParse(foundData, &arr);
		}
		else if (type == JsonValueType::Object)
		{
			//JsonObject
			JsonObject^ prevJo = nullptr;
			if (JsonObject::TryParse(foundData, &prevJo))
			{
				arr = ref new JsonArray();
				arr->Append(prevJo);
			}
		}
		
		if (arr == nullptr)
		{
			if (!JsonArray::TryParse(foundData, &arr))
			{
				arr = ref new JsonArray();
				JsonObject^ prevJo = nullptr;

				if (JsonObject::TryParse(foundData, &prevJo))
				{
					arr->Append(prevJo);
				}
			}
		}

		JsonObject^ prevJo = nullptr;
		if (currJo != nullptr)
		{
			for (uint32 i = 0; i < arr->Size; i++)
			{
				prevJo = arr->GetObjectAt(i);
				//머지 필요여부 체크및 필요하면 머지
				if (CheckBeforeMerge(prevJo, currJo, i))
				{
					break;
				}
				prevJo = nullptr;
			}

			if (prevJo == nullptr)
			{
				arr->Append(currJo);
			}
		}

		return arr->Stringify();
	}
	return L"";
}

void ClosedCaptions::SetTimelineMarkerText(String^ value)
{
	JsonObject^ json = nullptr;
	JsonArray^ arr = nullptr;

	JsonValueType type = GuessJsonValueType(value);
	if (type == JsonValueType::Array)
	{
		if (JsonArray::TryParse(value, &arr))
		{
			auto size = arr->Size;
			for (uint32 i = 0; i < arr->Size; i++)
			{
				json = arr->GetObjectAt(i);

				if (json->GetNamedNumber("NumRects") > 0)
				{
					if (json->GetNamedNumber("Format") == 0)
					{
						//텍스트 자막 삭제
						ClearTextClosedCaption();
						//이미지 자막
						for (unsigned int i = 0; i < json->GetNamedArray("Rects")->Size; i++)
						{
							auto rt = json->GetNamedArray("Rects")->GetObjectAt(i);
							JsonDataIMG = rt;
						}
					}
					else
					{
						//이미지 자막 삭제
						ClearImageClosedCaption();
						//텍스트 자막 추가
						SetClosedCaption(json);
					}
				}
			}
		}
	}
	else if (type == JsonValueType::Object)
	{
		if (JsonObject::TryParse(value, &json))
		{
			if (json->GetNamedNumber("NumRects") > 0)
			{
				if (json->GetNamedNumber("Format") == 0)
				{
					//텍스트 자막 삭제
					ClearTextClosedCaption();
					//이미지 자막
					for (unsigned int i = 0; i < json->GetNamedArray("Rects")->Size; i++)
					{
						auto rt = json->GetNamedArray("Rects")->GetObjectAt(i);
						JsonDataIMG = rt;
					}
				}
				else
				{
					//이미지 자막 삭제
					ClearImageClosedCaption();
					//텍스트 자막 추가
					SetClosedCaption(json);
				}
			}
		}
	}
}

void ClosedCaptions::AppendImageSubtitles(Windows::Foundation::Collections::IMap<String^, ImageData^>^ subtitleImageMap)
{
	if (subtitleImageMap != nullptr)
	{
		auto iter = subtitleImageMap->First();
		while (iter->HasCurrent)
		{
			if (ImageClosedCaption->SubtitleImageMap->HasKey(iter->Current->Key))
			{
				ImageClosedCaption->SubtitleImageMap->Remove(iter->Current->Key);
			}

			ImageClosedCaption->SubtitleImageMap->Insert(iter->Current->Key, iter->Current->Value);
			iter->MoveNext();
		}
	}
}

void ClosedCaptions::UnlockMovePosition()
{
	VisualStateManager::GoToState(this, "TextClosedCaptionPositionAvailable", false);
}

bool ClosedCaptions::CheckBeforeMerge(JsonObject^ prevJson, JsonObject^ currJson, int rectIndex)
{
	JsonObject^ prevRect = nullptr;
	JsonObject^ currRect = nullptr;

	if (prevJson->HasKey("Rects") && currJson->HasKey("Rects"))
	{
		auto prevRects = prevJson->GetNamedArray("Rects");
		auto currRects = currJson->GetNamedArray("Rects");
		if (prevRects->Size > rectIndex && currRects->Size > rectIndex)
		{
			if (prevRects->GetObjectAt(rectIndex)->HasKey("Style")
				&& currRects->GetObjectAt(rectIndex)->HasKey("Style"))
			{
				prevRect = prevRects->GetObjectAt(rectIndex)->GetNamedObject("Style");
				currRect = currRects->GetObjectAt(rectIndex)->GetNamedObject("Style");

				bool hasPositionInPrevRect = prevRect->HasKey("ClosedCaptionPosition");
				bool hasPositionInCurrRect = currRect->HasKey("ClosedCaptionPosition");

				if ((prevJson->GetNamedNumber("Pts") == currJson->GetNamedNumber("Pts") && prevJson->GetNamedNumber("EndDisplayTime") == currJson->GetNamedNumber("EndDisplayTime"))
					&& ((!hasPositionInPrevRect || !hasPositionInCurrRect) || (hasPositionInPrevRect && hasPositionInCurrRect && prevRect->GetNamedString("ClosedCaptionPosition") == currRect->GetNamedString("ClosedCaptionPosition"))))
				{
					prevJson->SetNamedValue("NumRects", JsonValue::CreateNumberValue(prevJson->GetNamedNumber("NumRects") + currJson->GetNamedNumber("NumRects")));

					auto rects = currJson->GetNamedArray("Rects");
					for (uint32 i = 0; i < rects->Size; i++)
					{
						prevRects->Append(rects->GetObjectAt(i));
					}
					return true;
				}
			}
		}
	}
	
	return false;
}


void ClosedCaptions::SetCCPosition(Panel^ parent, Windows::UI::Xaml::FrameworkElement^ ccMoveElement, double translationY)
{
	//트랜스폼
	auto ccMoveElementTransform = dynamic_cast<Windows::UI::Xaml::Media::CompositeTransform^>(ccMoveElement->RenderTransform);
	if (ccMoveElementTransform != nullptr)
	{
		ClosedCaption^ ccControl = nullptr;
		auto panel = dynamic_cast<Panel^>(ccMoveElement);
		for (unsigned int i = 0; i < panel->Children->Size; i++)
		{
			ccControl = dynamic_cast<ClosedCaption^>(panel->Children->GetAt(i));
			if (ccControl->VerticalAlignment != ccMoveElement->VerticalAlignment)
			{
				//내부 텍스트 객체도 수직정렬을 부모에 맞춘다. (그렇지 않으면 부모를 약간 꿈틀 거리며 위쪽/아랫쪽으로 붙는 현상이 생김)
				ccControl->VerticalAlignment = ccMoveElement->VerticalAlignment;
			}
			break;
		}

		//이동
		ccMoveElementTransform->TranslateY += translationY;
		//이동한 위치를 기준으로 위치차를 구함
		auto rectDiff = ccMoveElement->TransformToVisual(parent)->TransformPoint(Point());
		//위치에 따른 처리
		if (rectDiff.Y < parent->ActualHeight / 2)
		{
			//윗쪽
			if (ccMoveElement->VerticalAlignment != Windows::UI::Xaml::VerticalAlignment::Top)
			{
				ccMoveElementTransform->TranslateY = parent->ActualHeight + ccMoveElementTransform->TranslateY - ccMoveElement->ActualHeight;
				ccMoveElement->VerticalAlignment = Windows::UI::Xaml::VerticalAlignment::Top;
				ccControl->RenderTransformOrigin = Point(0.5, 0);
			}
			//상단으로 넘어가면 상단에 붙힘
			if (rectDiff.Y < 0)
			{
				ccMoveElementTransform->TranslateY = 0;
			}
		}
		else if (rectDiff.Y == parent->ActualHeight / 2)
		{
			//가운데
			ccMoveElementTransform->TranslateY = 0;
			ccMoveElement->VerticalAlignment = Windows::UI::Xaml::VerticalAlignment::Center;
			ccControl->RenderTransformOrigin = Point(0.5, 0.5);
		}
		else
		{
			//아랫쪽
			if (ccMoveElement->VerticalAlignment != Windows::UI::Xaml::VerticalAlignment::Bottom)
			{
				ccMoveElementTransform->TranslateY = ccMoveElementTransform->TranslateY - parent->ActualHeight + ccMoveElement->ActualHeight;
				ccMoveElement->VerticalAlignment = Windows::UI::Xaml::VerticalAlignment::Bottom;
				ccControl->RenderTransformOrigin = Point(0.5, 1);
			}
			//하단이 넘어가면 붙힘
			if (rectDiff.Y + ccMoveElement->ActualHeight > parent->ActualHeight)
			{
				ccMoveElementTransform->TranslateY = 0;
			}
		}
	}
}

void ClosedCaptions::DoStart()
{
	this->_Timer->Start();
}

void ClosedCaptions::DoStop()
{
	this->_Timer->Stop();
}

void ClosedCaptions::OnTextClosedCaptionContentsTapped(Platform::Object ^sender, TappedRoutedEventArgs ^e)
{
	VisualStateManager::GoToState(this, "TextClosedCaptionPositionUnavailable", false);
	MoveClosedCaptionPositionCompleted(this, ref new RoutedEventArgs());
}

void ClosedCaptions::OnTextClosedCaptionPositionPanelManipulationDelta(Platform::Object ^sender, ManipulationDeltaRoutedEventArgs ^e)
{
	auto child = dynamic_cast <FrameworkElement^>(e->Container);
	if (child != nullptr)
	{
		auto panel = dynamic_cast<Panel^>(child->Parent);
		if (panel != nullptr)
		{
			SetCCPosition(panel, child, e->Delta.Translation.Y);
		}
	}
}

DependencyObject^ ClosedCaptions::FindUIElement(DependencyObject^ element)
{
	DependencyObject^ parent = VisualTreeHelper::GetParent(element);
	auto p = dynamic_cast<MediaElement^>(parent);

	if (p != nullptr)
	{
		return p;
	}
	else if (parent == nullptr)
	{
		return nullptr;
	}
	else
	{
		auto pp = FindUIElement(parent);
		if (pp != nullptr)
		{
			return pp;
		}
	}

	return nullptr;
}

void ClosedCaptions::OnTick(Platform::Object ^sender, Platform::Object ^args)
{
	if (_Timer->IsEnabled)
	{
		auto position = CCPMediaElement->Position;

		auto decoderType = CCPMediaElement->DecoderType;
		auto subSrcType = CCPMediaElement->GetSubtitleSourceType();

		//HW디코더인 경우 외부자막이 존재하면, 1.5초 뒤의 자막 프레임을 마커에 등록
		if (decoderType == DecoderTypes::HW && subSrcType == SubtitleSourceTypes::External)
		{
			long long currentTime = position.Duration;
			CCPMediaElement->ConsumeSubtitle(position.Duration + 15000000L);
		}
		
		//이미 지난 삭제 대상의 자막 정리
		ClosedCaptionLT->ClearClosedCaption(position);
		ClosedCaptionCT->ClearClosedCaption(position);
		ClosedCaptionRT->ClearClosedCaption(position);
		ClosedCaptionLC->ClearClosedCaption(position);
		ClosedCaptionCC->ClearClosedCaption(position);
		ClosedCaptionRC->ClearClosedCaption(position);
		ClosedCaptionLB->ClearClosedCaption(position);
		ClosedCaptionCB->ClearClosedCaption(position);
		ClosedCaptionRB->ClearClosedCaption(position);
		ImageClosedCaption->ClearClosedCaption(position);
	}
	/*else
	{
		__debugbreak();
	}*/
}

void ClosedCaptions::OnSizeChanged(Platform::Object ^sender, SizeChangedEventArgs ^e)
{
	auto size = e->NewSize;
	DisplayVideoSize = size;
}

void ClosedCaptions::OnLoaded(Platform::Object ^sender, RoutedEventArgs ^e)
{
	if (CCPMediaElement == nullptr)
	{
		auto popList = Media::VisualTreeHelper::GetOpenPopups(Window::Current);
		Primitives::Popup^ popup = nullptr;
		for (unsigned int i = 0; i < popList->Size; i++)
		{
			popup = popList->GetAt(i);
			if (popup->Name == "CCPopup")
			{
				break;
			}
		}
		auto me = safe_cast<MediaElement^>(FindUIElement(popup));
		if (me != nullptr)
		{
			CCPMediaElement = me;
		}
	}
	//모바일의 경우 자막 컨트롤의 SizeChanged이벤트가 발생하지 않으므로, 현재 창의 이벤트를 사용
	if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
	{
		_WindowSizeChangedToken = Window::Current->CoreWindow->SizeChanged += ref new TypedEventHandler<Windows::UI::Core::CoreWindow ^, Windows::UI::Core::WindowSizeChangedEventArgs ^>(this, &ClosedCaptions::OnWindowSizeChanged);
	}
}

void ClosedCaptions::OnUnloaded(Platform::Object ^sender, RoutedEventArgs ^e)
{
	if (Windows::System::Profile::AnalyticsInfo::VersionInfo->DeviceFamily == "Windows.Mobile")
	{
		Window::Current->CoreWindow->SizeChanged -= _WindowSizeChangedToken;
	}
}

void ClosedCaptions::OnWindowSizeChanged(Windows::UI::Core::CoreWindow ^sender, Windows::UI::Core::WindowSizeChangedEventArgs ^args)
{
	auto size = args->Size;
	DisplayVideoSize = size;
}

DEPENDENCY_PROPERTY_REGISTER(FontFamiliesSource, Object, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(EnableStyleOverride, bool, ClosedCaptions, false);
DEPENDENCY_PROPERTY_REGISTER(FontStyleOverride, Windows::UI::Text::FontStyle, ClosedCaptions, Windows::UI::Text::FontStyle::Normal);
DEPENDENCY_PROPERTY_REGISTER(FontWeightOverride, Windows::UI::Text::FontWeight, ClosedCaptions, Windows::UI::Text::FontWeights::Normal);
DEPENDENCY_PROPERTY_REGISTER(ForegroundOverride, Media::Brush, ClosedCaptions, ref new Media::SolidColorBrush(Windows::UI::Colors::White));
DEPENDENCY_PROPERTY_REGISTER(BackgroundVisibility, Windows::UI::Xaml::Visibility, ClosedCaptions, Windows::UI::Xaml::Visibility::Collapsed);
DEPENDENCY_PROPERTY_REGISTER(ShadowVisibility, Windows::UI::Xaml::Visibility, ClosedCaptions, Windows::UI::Xaml::Visibility::Visible);
DEPENDENCY_PROPERTY_REGISTER(OutlineVisibility, Windows::UI::Xaml::Visibility, ClosedCaptions, Windows::UI::Xaml::Visibility::Visible);
DEPENDENCY_PROPERTY_REGISTER(BaseFontSize, double, ClosedCaptions, BASE_FONT_SIZE);
DEPENDENCY_PROPERTY_REGISTER(FontSizeRatio, double, ClosedCaptions, 1.0);
DEPENDENCY_PROPERTY_REGISTER(JsonDataLT, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataCT, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataRT, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataLC, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataCC, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataRC, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataLB, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataCB, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataRB, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(JsonDataIMG, JsonObject, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(DefaultSubtitlePosition, double, ClosedCaptions, 0.0);
DEPENDENCY_PROPERTY_REGISTER(DefaultSubtitleVerticalAlignment, Windows::UI::Xaml::VerticalAlignment, ClosedCaptions, Windows::UI::Xaml::VerticalAlignment::Bottom);
DEPENDENCY_PROPERTY_REGISTER(DefaultSubtitleBlockOrigin, Point, ClosedCaptions, Point(0.5, 1));
DEPENDENCY_PROPERTY_REGISTER(DisplayVideoSize, Size, ClosedCaptions, Size::Empty);
DEPENDENCY_PROPERTY_REGISTER(NaturalVideoSize,Size, ClosedCaptions, Size::Empty);
DEPENDENCY_PROPERTY_REGISTER(SelectedSubLanguageCode, String, ClosedCaptions, nullptr);
DEPENDENCY_PROPERTY_REGISTER(SubLanguageSource, Object, ClosedCaptions, nullptr);


