//
// ClosedCaption.xaml.cpp
// Implementation of the ClosedCaption class
//

#include "pch.h"
#include "ClosedCaption.xaml.h"
#include <sstream>
#include <string>
#include <iostream>

using namespace std::tr1;
using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::Xaml::Controls;
using namespace Lime::CPPHelper;
using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Controls::Primitives;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::Xaml::Navigation;

double ConvertToPixel(String^ strFontSize, float dpi)
{
	std::string sfs(strFontSize->Begin(), strFontSize->End());
	std::transform(sfs.begin(), sfs.end(), sfs.begin(), tolower);
	double fs = strtod(sfs.c_str(), NULL);

	if (sfs.find("pt") != std::string::npos)
	{
		//fs = (fs * dpi) / 72.0;
		fs = fs * 96.0 / 72.0;
	}
	else if (sfs.find("em") != std::string::npos)
	{
		fs = 12 * fs;
		fs = (fs * dpi) / 72.0;
	}
	else if (sfs.find('%') != std::string::npos)
	{
		fs = 12 * fs / 100;
		fs = (fs * dpi) / 72.0; 
	}
	return fs;
}

Windows::UI::Xaml::Media::SolidColorBrush^ GetColorBrush(std::wstring hexColor)
{
	std::string cv(hexColor.begin(), hexColor.end());

	if (cv.at(0) == '#')
	{
		cv = cv.substr(1, cv.length() - 1);
	}
	long long lColor = strtoll(cv.c_str(), NULL, 16);
	unsigned char a = (lColor >> 24) & 0xFF;
	unsigned char r = (lColor >> 16) & 0xFF;
	unsigned char g = (lColor >> 8) & 0xFF;
	unsigned char b = (lColor)& 0xFF;

	return ref new Windows::UI::Xaml::Media::SolidColorBrush(Windows::UI::ColorHelper::FromArgb(a, r, g, b));
}

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
ClosedCaption^ ClosedCaption::FindTop(DependencyObject^ obj)
{
	DependencyObject^ parent = VisualTreeHelper::GetParent(obj);
	auto p = dynamic_cast<ClosedCaption^>(parent);

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
		auto pp = FindTop(parent);
		if (pp != nullptr)
		{
			return pp;
		}
	}
	return nullptr;
}

void ClosedCaption::ChangeFontSize(TextElement^ obj, double ratio)
{
	auto paragraphObj = dynamic_cast<Paragraph^>(obj);
	if (paragraphObj != nullptr)
	{
		for (unsigned int i = 0; i < paragraphObj->Inlines->Size; i++)
		{
			ChangeFontSize(paragraphObj->Inlines->GetAt(i), ratio);
		}
	}
	else
	{
		auto inlineObj = dynamic_cast<Inline^>(obj);
		if (inlineObj != nullptr)
		{
			auto span = dynamic_cast<Span^>(inlineObj);
			if (span != nullptr)
			{
				for (unsigned int i = 0; i < span->Inlines->Size; i++)
				{
					ChangeFontSize(span->Inlines->GetAt(i), ratio);
				}
			}
		}
	}

	auto fs = obj->FontSize;
	if (fs > 0)
	{
		obj->FontSize = fs * ratio;
	}
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::SetBaseFontSize()
{
	if (std::isnan(this->NaturalVideoSize.Width) || std::isinf(this->NaturalVideoSize.Height)
		|| std::isnan(this->_CurrentVideoSize.Width) || std::isinf(this->_CurrentVideoSize.Height)) return;

	//1보다 크면 확대, 작으면 축소
	/*auto widthRatio = this->_CurrentVideoSize.Width / this->NaturalVideoSize.Width;
	auto heightRatio = this->_CurrentVideoSize.Height / this->NaturalVideoSize.Height;
	auto ratio = min(widthRatio, heightRatio);*/

	auto ntRatio = this->NaturalVideoSize.Width / this->NaturalVideoSize.Height;
	auto dsRatio = this->DisplayVideoSize.Width / this->DisplayVideoSize.Height;
	float ratio = 1;

	//현재 영상의 크기는 화면 크기가 아닌 실제 영상의 크기이다. 
	//실제 영상의 크기는 현재 화면의 가로/세로 비율과 원본영상의 가로/세로 비율 중 작은것에 맞춰어 계산한다. 

	//예를 들어 16:9 였다면, 1.78이라 치고, 현재 화면의 가로가 500, 세로가 500이라면...
	//현재 화면의 비율이 1.78이하이므로 가로가 기준이 된다. 1.78을 넘어가면 세로가 기준이 된다. 
	//즉 실제 영상은 가로 500, 세로 500 / 1.78로 나눈값(281)이 세로가 된다. 
	//(실제 영상은 가로 500 / 1920, 세로가 281 / 1080로 실제 비율(2.6)은 같다.)
	//원본 영상에서 현재 영상은 2.6배 줄어 들었고 여기에 원본영상과 자막 크기의 비율을 곲하면 된다.
	if (ntRatio >= dsRatio)
	{
		//가로 기준
		ratio = this->DisplayVideoSize.Width / this->NaturalVideoSize.Width;
	}
	else
	{
		//세로 기준
		ratio = this->DisplayVideoSize.Height / this->NaturalVideoSize.Height;
	}

	if (!std::isnan(ratio) && !std::isnan(ratio) && ratio > 0)
	{
		auto fontSize = this->_DefaultFontSize * ratio;
		if (fontSize > 0)
		{
			this->BaseFontSize = fontSize * this->FontSizeRatio;
			this->ShadowDepth = min(fontSize * this->FontSizeRatio / 18, 6.5);
			this->OutlineDepth = min(fontSize * this->FontSizeRatio / 30, 4.0);
			this->OutlineNegativeDepth = this->OutlineDepth * -1;

			//OutputDebugMessage(L"폰트 사이즈 : %f\n", this->BaseFontSize);

			auto margin = this->_Margin;
			margin.Left *= ratio;
			margin.Top *= ratio;
			margin.Right *= ratio;
			margin.Bottom *= ratio;
			this->Margin = margin;
		}
	}
}

ClosedCaption::ClosedCaption() : 
	_DefaultFontSize(1)
	, rubyProxyTag(L"\\s+FontFamily\\s*=\\s*\"velostep-ruby-tag-support\"\\s*", std::regex_constants::ECMAScript | std::regex_constants::icase)
	, fontFamilyTag(L"\\s+FontFamily\\s*=\\s*\"(.+?)\"\\s*", std::regex_constants::ECMAScript | std::regex_constants::icase)
	, fontSizeTag(L"\\s+FontSize\\s*=\\s*\"(\\d+?)\"\\s*", std::regex_constants::ECMAScript | std::regex_constants::icase)
	, ForegroundExp(L"Foreground\\s*=\\s*\"(.*?)\"", std::regex_constants::ECMAScript | std::regex_constants::icase)
	, FontStyleExp(L"Foreground\\s*=\\s*\"(.*?)\"", std::regex_constants::ECMAScript | std::regex_constants::icase)
	, FontWeightExp(L"Foreground\\s*=\\s*\"(.*?)\"", std::regex_constants::ECMAScript | std::regex_constants::icase)
{

	InitializeComponent();
	Loaded += ref new Windows::UI::Xaml::RoutedEventHandler(this, &CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnLoaded);
	richTextList = ref new Platform::Collections::Vector<Windows::UI::Xaml::Controls::RichTextBlock^>();
	ccJsonArray = ref new Windows::Data::Json::JsonArray();
	
	if (richTextList->Size == 0)
	{
		auto children = ContentPanel->Children;
		for (unsigned int i = 0; i < children->Size; i++)
		{
			auto richTxt = dynamic_cast<RichTextBlock^>(children->GetAt(i));
			if (richTxt != nullptr)
			{
				//auto name = richTxt->Name;
				//OutputDebugMessage(L"%s\n", name->Data());
				richTextList->Append(richTxt);
			}
		}
	}
}

ClosedCaption::~ClosedCaption()
{
	OutputDebugMessage(L"Called constructor of the ClosedCaption\n");
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnLoaded(Platform::Object ^sender, Windows::UI::Xaml::RoutedEventArgs ^e)
{
	Point origin = Point(0, 0);
	switch (this->HorizontalAlignment)
	{
	case Windows::UI::Xaml::HorizontalAlignment::Center:
		origin.X = 0.5;
		break;
	case Windows::UI::Xaml::HorizontalAlignment::Right:
		origin.X = 1;
		break;
	}

	switch (this->VerticalAlignment)
	{
	case Windows::UI::Xaml::VerticalAlignment::Center:
		origin.Y = 0.5;
		break;
	case Windows::UI::Xaml::VerticalAlignment::Bottom:
		origin.Y = 1;
		break;
	}

	this->RenderTransformOrigin = origin;
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::ClearClosedCaption(TimeSpan position)
{
	unsigned int size = this->ccJsonArray->Size;
	//삭제 대상 json 배열
	for (unsigned int j = size; j > 0; j--)
	{
		auto rect = this->ccJsonArray->GetObjectAt(j - 1);

		if (!rect->HasKey("Type") || rect->GetNamedNumber("Type") != 5)
		{
			String^ guid = rect->GetNamedString("Guid");
			//double delPts = rect->GetNamedNumber("StartTimeDuration");
			double delEndTime = rect->GetNamedNumber("EndTimeDuration");
			bool isFound = false;

			if (delEndTime <= position.Duration)
			{
				//리치 텍스트 블록
				for (unsigned int k = 0; k < this->richTextList->Size; k++)
				{
					//파라그래프 블록
					for (unsigned int l = this->richTextList->GetAt(k)->Blocks->Size; l > 0; l--)
					{
						if (this->richTextList->GetAt(k)->Blocks->GetAt(l - 1)->Name == guid)
						{
							this->richTextList->GetAt(k)->Blocks->RemoveAt(l - 1);
							isFound = true;
							break;
						}
					}
				}
				this->ccJsonArray->RemoveAt(j - 1);
			}
			//지워졌다면 타이머 삭제
			if (isFound)
			{
				break;
			}
		}
	}
}

DEPENDENCY_PROPERTY_REGISTER(BackgroundVisibility, Windows::UI::Xaml::Visibility, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Windows::UI::Xaml::Visibility::Collapsed);
DEPENDENCY_PROPERTY_REGISTER(ShadowVisibility, Windows::UI::Xaml::Visibility, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Windows::UI::Xaml::Visibility::Visible);
DEPENDENCY_PROPERTY_REGISTER(OutlineVisibility, Windows::UI::Xaml::Visibility, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Windows::UI::Xaml::Visibility::Visible);
DEPENDENCY_PROPERTY_REGISTER(FontFamiliesSource, Platform::Object, CCPlayer::UWP::Xaml::Controls::ClosedCaption, nullptr);
DEPENDENCY_PROPERTY_REGISTER(ShadowDepth, double, CCPlayer::UWP::Xaml::Controls::ClosedCaption, 0.0);
DEPENDENCY_PROPERTY_REGISTER(OutlineDepth, double, CCPlayer::UWP::Xaml::Controls::ClosedCaption, 0.0);
DEPENDENCY_PROPERTY_REGISTER(OutlineNegativeDepth, double, CCPlayer::UWP::Xaml::Controls::ClosedCaption, 0.0);
DEPENDENCY_PROPERTY_REGISTER(BaseFontSize, double, CCPlayer::UWP::Xaml::Controls::ClosedCaption, BASE_FONT_SIZE);
DEPENDENCY_PROPERTY_REGISTER(Outline, Windows::UI::Xaml::Media::Brush, CCPlayer::UWP::Xaml::Controls::ClosedCaption, ref new Windows::UI::Xaml::Media::SolidColorBrush(Windows::UI::Colors::Black));
DEPENDENCY_PROPERTY_REGISTER(SelectedSubLanguageCode, Platform::String, CCPlayer::UWP::Xaml::Controls::ClosedCaption, "ALLCC");
//DEPENDENCY_PROPERTY_REGISTER(SubLanguageSource, Windows::Foundation::Collections::IVector<CCPlayer::UWP::Common::Codec::SubtitleLanguage^>, CCPlayer::UWP::Xaml::Controls::ClosedCaption, nullptr);
DEPENDENCY_PROPERTY_REGISTER(SubLanguageSource, Object, CCPlayer::UWP::Xaml::Controls::ClosedCaption, nullptr);

DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(JsonData, Windows::Data::Json::JsonObject, CCPlayer::UWP::Xaml::Controls::ClosedCaption, nullptr);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(EnableStyleOverride, bool, CCPlayer::UWP::Xaml::Controls::ClosedCaption, false);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(FontStyleOverride, Windows::UI::Text::FontStyle, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Windows::UI::Text::FontStyle::Normal);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(FontWeightOverride, Windows::UI::Text::FontWeight, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Windows::UI::Text::FontWeights::Normal);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(ForegroundOverride, Windows::UI::Xaml::Media::Brush, CCPlayer::UWP::Xaml::Controls::ClosedCaption, ref new Windows::UI::Xaml::Media::SolidColorBrush(Windows::UI::Colors::White));
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(FontSizeRatio, double, CCPlayer::UWP::Xaml::Controls::ClosedCaption, 1.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(DisplayVideoSize, Size, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Size::Empty);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(NaturalVideoSize, Size, CCPlayer::UWP::Xaml::Controls::ClosedCaption, Size::Empty);

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnFontStyleOverrideChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	/* 중요! */
	//Paragraph생성시 EnableStyleOverride가 true이면 모든 인라인 Foreground, FontStyle, FontWeight 셋팅을 무시(제거)한다.
	//그렇기 때문에 최상위에만 해당 값을 적용하면 된다.
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto fontStyleOverride = static_cast<Windows::UI::Text::FontStyle>(args->NewValue);
	_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontStyleProperty, fontStyleOverride);
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnFontWeightOverrideChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	/* 중요! */
	//Paragraph생성시 EnableStyleOverride가 true이면 모든 인라인 Foreground, FontStyle, FontWeight 셋팅을 무시(제거)한다.
	//그렇기 때문에 최상위에만 해당 값을 적용하면 된다.
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto fontWegihtOverride = static_cast<Windows::UI::Text::FontWeight>(args->NewValue);
	_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontWeightProperty, fontWegihtOverride);
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnForegroundOverrideChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	/* 중요! */
	//Paragraph생성시 EnableStyleOverride가 true이면 모든 인라인 Foreground, FontStyle, FontWeight 셋팅을 무시(제거)한다.
	//그렇기 때문에 최상위에만 해당 값을 적용하면 된다.
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto foregroundOverride = static_cast<Windows::UI::Xaml::Media::Brush^>(args->NewValue);
	_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::ForegroundProperty, foregroundOverride);
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnEnableStyleOverrideChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	/* 중요! */
	//Paragraph생성시 EnableStyleOverride가 true이면 모든 인라인 Foreground, FontStyle, FontWeight 셋팅을 무시(제거)한다.
	//그렇기 때문에 최상위에만 해당 값을 적용하면 된다.
	auto _this = safe_cast<ClosedCaption^>(sender);
	bool enableStyleOverride = static_cast<bool>(args->NewValue);
	Brush^ foreground = nullptr;

	//내부 스타일 먹은 것은 무시함
	if (enableStyleOverride)
	{
		foreground = _this->ForegroundOverride;
		_this->foregroundBackup = _this->Foreground;
		_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::ForegroundProperty, foreground);
		_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontStyleProperty, _this->FontStyleOverride);
		_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontWeightProperty, _this->FontWeightOverride);
	}
	else
	{
		foreground = _this->foregroundBackup;
		_this->SetValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::ForegroundProperty, foreground);
		_this->ClearValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontStyleProperty);
		_this->ClearValue(CCPlayer::UWP::Xaml::Controls::ClosedCaption::FontWeightProperty);
	}
}

//폴트 크기 슬라이더 이동시 호출 (초기 로드시에도 호출됨)
void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnFontSizeRatioChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ClosedCaption^>(sender);

	auto newRatio = static_cast<double>(args->NewValue);
	auto oldRatio = static_cast<double>(args->OldValue);
	auto ratio = newRatio / oldRatio;

	if (!std::isnan(ratio) && !std::isinf(ratio) && ratio > 0)
	{
		//인라인 폰트 사이즈 변경
		for (unsigned int i = 0; i < _this->richTextList->Size; i++)
		{
			auto blocks = _this->richTextList->GetAt(i)->Blocks;
			for (unsigned int j = 0; j < blocks->Size; j++)
			{
				auto paragraph = dynamic_cast<Paragraph^>(blocks->GetAt(j));
				if (paragraph != nullptr)
				{
					ChangeFontSize(paragraph, ratio);
				}
			}
		}
	}

	//기본 폰트 사이즈 설정
	_this->SetBaseFontSize();
}

//화면 사이즈가 변경될 때마다 호출
void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnDisplayVideoSizeChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto newVal = static_cast<Size>(args->NewValue);
	auto oldVal = static_cast<Size>(args->OldValue);
	//현재 화면의 크기
	_this->_CurrentVideoSize = newVal;

	//비디오 영상의 원본 크기
	auto videoWidth = _this->NaturalVideoSize.Width;
	auto videoHeight = _this->NaturalVideoSize.Height;
	
	//1보다 크면 확대, 작으면 축소
	auto newWidthRatio = newVal.Width / videoWidth;
	auto newHeightRatio = newVal.Height / videoHeight;

	auto oldWidthRatio = oldVal.Width / videoWidth;
	auto oldHeightRatio = oldVal.Height / videoHeight;

	auto newRatio = min(newWidthRatio, newHeightRatio);
	auto oldRatio = min(oldWidthRatio, oldHeightRatio);

	auto ratio = newRatio / oldRatio;

	if (!std::isnan(ratio) && !std::isnan(ratio) && ratio > 0)
	{
		//인라인 폰트 사이즈 변경
		for (unsigned int i = 0; i < _this->richTextList->Size; i++)
		{
			auto blocks = _this->richTextList->GetAt(i)->Blocks;
			for (unsigned int j = 0; j < blocks->Size; j++)
			{
				auto paragraph = dynamic_cast<Paragraph^>(blocks->GetAt(j));
				if (paragraph != nullptr)
				{
					ChangeFontSize(paragraph, ratio);
				}
			}
		}
	}

	//기본 폰트 사이즈 설정 (인라인 객체를 먼저 설정한 후 변경 하여야 한다.)
	_this->SetBaseFontSize();
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnNaturalVideoSizeChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto newSize = static_cast<Size>(args->NewValue);

	if (!std::isnan(newSize.Width) && !std::isinf(newSize.Width) && newSize.Width > 0)
	{
		_this->_DefaultFontSize = newSize.Width / DEFAULT_FONT_SIZE_DEVIDER;
		//기본 폰트 사이즈 설정
		_this->SetBaseFontSize();
	}
}

void CCPlayer::UWP::Xaml::Controls::ClosedCaption::OnJsonDataChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ e)
{
	auto _this = safe_cast<ClosedCaption^>(sender);
	auto jsonObject = dynamic_cast<Windows::Data::Json::JsonObject^>(e->NewValue);
	
	//아웃라인 초기화
	_this->Outline = ref new Windows::UI::Xaml::Media::SolidColorBrush(Windows::UI::Colors::Black);

	if (jsonObject == nullptr)
	{
		//모든 자막 삭제
		for (unsigned int i = 0; i < _this->richTextList->Size; i++)
		{
			auto richTxt = dynamic_cast<RichTextBlock^>(_this->richTextList->GetAt(i));
			if (richTxt != nullptr)
			{
				_this->richTextList->GetAt(i)->Blocks->Clear();
			}
		}
		//모든 삭제 대상 제거
		_this->ccJsonArray->Clear();
		return;
	}
	else
	{
//		auto str = jsonObject->ToString();
		String^ xamlText = "";
		double pts = jsonObject->GetNamedNumber("Pts");
		double sdTime = jsonObject->GetNamedNumber("StartDisplayTime");
		double edTime = jsonObject->GetNamedNumber("EndDisplayTime");
		TimeSpan sdTS;
		TimeSpan edTS;
		sdTS.Duration = (LONGLONG)(pts + sdTime);
		edTS.Duration = (LONGLONG)(pts + edTime);

		int numRects = (int)jsonObject->GetNamedNumber("NumRects");
		auto rects = jsonObject->GetNamedArray("Rects");

		auto displayInfo = Windows::Graphics::Display::DisplayInformation::GetForCurrentView();
		auto rawPixelsPerViewPixel = (float)displayInfo->RawPixelsPerViewPixel;
		auto fontSize = _this->BaseFontSize;
		
		for (int i = 0; i < numRects; i++)
		{
			bool isUnderline = false;
			std::wstring strParagraph;
			String^ htmlText = nullptr;
			Windows::Data::Json::JsonObject^ rect = rects->GetObjectAt(i);
			auto type = rect->GetNamedNumber("Type");

			rect->SetNamedValue("StartTimeDuration", Windows::Data::Json::JsonValue::CreateNumberValue((double)sdTS.Duration));
			rect->SetNamedValue("EndTimeDuration", Windows::Data::Json::JsonValue::CreateNumberValue((double)edTS.Duration));

			if (type == 2)
			{
				htmlText = rect->GetNamedString("Text");
			}
			else if (type == 3 || type == 4) //ASS, SRT
			{
				htmlText = rect->GetNamedString("Ass");
			}
			else if (type == 5) // SMI
			{
				htmlText = rect->GetNamedString("Ass");
				//rect->SetNamedValue("EndTimeDuration", Windows::Data::Json::JsonValue::CreateNumberValue(10000000L * 60 * 60 * 24 * 365));
			}

			//스타일 적용
			if (rect->HasKey("Style"))
			{
				auto dvs = _this->DisplayVideoSize;
				double sizeRatio = 1;
				String^ ccPos = "CB";

				auto styleJO = rect->GetNamedObject("Style");

				if (jsonObject->HasKey("NaturalSubtitleWidth") && jsonObject->HasKey("NaturalSubtitleHeight"))
				{
					//relative mode
					auto nsw = (float)jsonObject->GetNamedNumber("NaturalSubtitleWidth");
					auto nsh = (float)jsonObject->GetNamedNumber("NaturalSubtitleHeight");
					_this->_NaturalSubtitleSize = Size(nsw, nsh);

					sizeRatio = min(dvs.Width / nsw, dvs.Height / nsh);
				}

				if (std::isnan(sizeRatio) || std::isinf(sizeRatio) || sizeRatio <= 0)
				{
					sizeRatio = 1;
				}

				if (styleJO->HasKey("ClosedCaptionPosition"))
				{
					ccPos = styleJO->GetNamedString("ClosedCaptionPosition");
				}

				if (styleJO->HasKey("FontFamily"))
				{
					strParagraph.append(L" FontFamily=\"").append(styleJO->GetNamedString("FontFamily")->Data()).append(L"\"");
				}
				
				//SAMI는 14pt가 일반적인 자막 사이즈인데 너무 작게 표시되므로 ASS이외에는 무시
				if (styleJO->HasKey("FontSize") && type == 3) 
				{
					fontSize = ConvertToPixel(styleJO->GetNamedString("FontSize"), displayInfo->LogicalDpi);
					fontSize *= sizeRatio * _this->FontSizeRatio;
					//fontSize /= std::sqrt(rawPixelsPerViewPixel);  //각 디바이스의 뷰픽셀에 맞추어 조정
					fontSize *= 0.75; // 기본 크기가 너무 커서 약간 줄임 

					std::ostringstream buffer;
					buffer << fontSize;
					std::string sfs = buffer.str();
					std::wstring wFontSize(sfs.begin(), sfs.end());

					strParagraph.append(L" FontSize=\"").append(wFontSize).append(L"\"");
				}

				if (styleJO->HasKey("FontWeight"))
				{
					strParagraph.append(L" FontWeight=\"").append(styleJO->GetNamedString("FontWeight")->Data()).append(L"\"");
				}

				if (styleJO->HasKey("FontStyle"))
				{
					strParagraph.append(L" FontStyle=\"").append(styleJO->GetNamedString("FontStyle")->Data()).append(L"\"");
				}

				if (styleJO->HasKey("Foreground"))
				{
					strParagraph.append(L" Foreground=\"").append(styleJO->GetNamedString("Foreground")->Data()).append(L"\"");
				}

				if (styleJO->HasKey("Background"))
				{
					std::wstring wcv(styleJO->GetNamedString("Background")->Data());
					_this->Background = GetColorBrush(wcv);
				}

				if (styleJO->HasKey("Outline"))
				{
					auto outline = styleJO->GetNamedString("Outline");
					std::wstring wcv(styleJO->GetNamedString("Outline")->Data());
					_this->Outline = GetColorBrush(wcv);
					//_this->Shadow = GetColorBrush(wcv);
					//_this->OutlineHexColor = styleJO->Lookup("Outline")->GetString();
				}

				if (styleJO->HasKey("TextAlignment"))
				{
					strParagraph.append(L" TextAlignment=\"").append(styleJO->GetNamedString("TextAlignment")->Data()).append(L"\"");
				}

				Thickness margin = _this->Margin;
				std::string tmp;
				
				if (styleJO->HasKey("MarginLeft"))
				{
					auto mv = ConvertToPixel(styleJO->GetNamedString("MarginLeft"), displayInfo->LogicalDpi);
					margin.Left = mv * sizeRatio;
				}

				if (styleJO->HasKey("MarginTop"))
				{
					auto mv = ConvertToPixel(styleJO->GetNamedString("MarginTop"), displayInfo->LogicalDpi);
					margin.Top = mv * sizeRatio;
				}

				if (styleJO->HasKey("MarginRight"))
				{
					auto mv = ConvertToPixel(styleJO->GetNamedString("MarginRight"), displayInfo->LogicalDpi);
					margin.Right = mv * sizeRatio;
				}

				if (styleJO->HasKey("MarginBottom"))
				{
					auto mv = ConvertToPixel(styleJO->GetNamedString("MarginBottom"), displayInfo->LogicalDpi);
					if (type == 3 && (ccPos == "LT" || ccPos == "CT" || ccPos == "RT"))
					{
						margin.Top = mv * sizeRatio;
					}
					else 
					{
						margin.Bottom = mv * sizeRatio;
					}
				}
				_this->Margin = margin;
				_this->_Margin = margin;

				if (type == 4 || type == 5)
				{
					//폰트 및 마진 사이즈 적용
					_this->SetBaseFontSize();
				}

				if (styleJO->HasKey("Underline"))
				{
					isUnderline = true;
				}

				if (styleJO->HasKey("BorderStyle"))
				{
					if (styleJO->GetNamedString("BorderStyle") == "1")
					{
						if (styleJO->HasKey("OutlineThickness"))
						{
							double outlineThickness = styleJO->GetNamedNumber("OutlineThickness");
							//폰트 크기 및 화면 비율 적용
							outlineThickness = sqrt(outlineThickness * _this->FontSizeRatio * sizeRatio);
							outlineThickness = min(outlineThickness, 4.0);
							//외곽선 두께 설정
							_this->OutlineDepth = outlineThickness;
							_this->OutlineNegativeDepth = _this->OutlineDepth * -1;
						}
						if (styleJO->HasKey("ShadowThickness"))
						{
							double shadowThickness = styleJO->GetNamedNumber("ShadowThickness");
							double outlineThickness = _this->OutlineDepth;
							//폰트 크기 및 화면 비율 적용
							shadowThickness = outlineThickness + sqrt(shadowThickness * _this->FontSizeRatio * sizeRatio);
							_this->ShadowDepth = min(shadowThickness, 6.5);
						}
					}
				}
			}
			
			//htmlText = "<font face=\"꼴랑체\" style=\"font-size:15;font-weight:bolder;font-style:italic\">" + htmlText + "</font>";
			////System.Diagnostics.Debug.WriteLine(htmlText);

			try
			{
				if (type != 5) // SUBTITLE_SAMI => 5
				{
					htmlText = ref new String(std::regex_replace(htmlText->Data(), std::wregex(L"&"), std::wstring(L"&amp;")).c_str());
					htmlText = StringHelper::Replace(htmlText, "\n", "<BR>");
				}
				else
				{
					if (htmlText == nullptr)
					{
						htmlText = "&nbsp;";
					}

					//예외 처리... 간혹 &nbsp;에서 세미콜론이 누락되는 경우들이 있음.
					if (htmlText == "&nbsp")
					{
						htmlText = "&nbsp;";
					}

					htmlText = StringHelper::Replace(htmlText, "&nbsp ", "&nbsp;");
					htmlText = StringHelper::Replace(htmlText, "&nbsp\r\n", "&nbsp;\r\n");
					htmlText = StringHelper::Replace(htmlText, "&nbsp\r", "&nbsp;\r");
					htmlText = StringHelper::Replace(htmlText, "&nbsp\n", "&nbsp;\n");
					//	//&nbsp; 
					//	//&amp; => &
					//	//&lt; => <
					//	//&gt; => >
					//	//&quot; => "
				}

				std::wstring strXaml(StringHelper::HtmlTextToXamlText(htmlText)->Data());
				std::wsmatch m;
				if (strXaml.find(L"<Paragraph") != std::string::npos)
				{
					size_t offset = 0;

					if (rect->HasKey("Guid"))
					{
						std::wstring guid;
						if (type == 5)
						{
							guid = L"Guid:" + std::wstring(rect->GetNamedString("Guid")->Data());

							if (rect->HasKey("Lang") && !rect->GetNamedString("Lang")->IsEmpty())
							{
								guid = guid + L",Lang:" + rect->GetNamedString("Lang")->Data();
							}
						}
						else
						{
							guid = rect->GetNamedString("Guid")->Data();
						}

						offset = 10;
						guid.insert(0, L" Name=\"").append(L"\" ");
						strXaml.insert(offset, guid);
						offset += guid.size();
					}

					strXaml.insert(offset, strParagraph);

					if (isUnderline)
					{
						offset = strXaml.find(L">") + 1;
						strXaml.insert(offset, L"<Underline>");

						offset = strXaml.find(L"</Paragraph>");
						strXaml.insert(offset, L"</Underline>");
					}
				}
				
				//루비 태그 변환 => <Span FontFamily="vs-ruby"> 항상 이형태로 변환되서 온다.
				//xamlText = StringHelper::Replace(xamlText, "FontFamily=\"velostep-ruby-tag-support\"", StringHelper::Format("FontSize=\"{0}\"", closedCaption->FontSize * 0.65));
				//xamlText = xamlText.Replace("FontFamily=\"vs-ruby\"", "Typography.Variants=\"Superscript\""); <== 안대..
				if (regex_search(strXaml, m, _this->rubyProxyTag))
				{
					std::ostringstream buffer;
					double fs = _this->BaseFontSize;

					std::wstring tag(m.suffix());
					if (regex_search(tag, m, _this->fontSizeTag))
					{
						std::wstring tmp(m[1].str());
						std::string sfs(tmp.begin(), tmp.end());
						fs = strtod(sfs.c_str(), NULL);
					}
					else
					{
						tag = m.prefix();
						if (regex_search(tag, m, _this->fontSizeTag))
						{
							std::wstring tmp(m[1].str());
							std::string sfs(tmp.begin(), tmp.end());
							fs = strtod(sfs.c_str(), NULL);
						}
					}

					buffer << fs * 0.65;
					std::string tmp(buffer.str());
					std::wstring fontSize(tmp.begin(), tmp.end());
					fontSize.insert(0, L" FontSize=\"").append(L"\" CharacterSpacing=\"100\"");;
					strXaml = regex_replace(strXaml, _this->rubyProxyTag, fontSize);
				}

				auto fontList = dynamic_cast<Windows::Foundation::Collections::IVector<CCPlayer::UWP::Xaml::Controls::KeyName^>^>(_this->FontFamiliesSource);
				if (fontList != nullptr)
				{
					std::wstring tmp(strXaml);
					while (regex_search(tmp, m, _this->fontFamilyTag))
					{
						std::wstring face(m[1].str());
						//이미 바뀐 폰트는 패스
						if (face.find(L"ms-appdata://", 0) == std::string::npos)
						{
							for (uint32 j = 0; j < fontList->Size; j++)
							{
								auto kn = fontList->GetAt(j);
								auto type = kn->Type;
								auto name = std::wstring(kn->Name->Data());

								if (type == "App" && name == face)
								{
									std::wstring oldStr(face);
									oldStr.insert(0, L"FontFamily\\s*=\\s*\"").append(L"\"");
									std::wregex oldStrPattern(oldStr, std::regex_constants::ECMAScript | std::regex_constants::icase);

									std::wstring newStr;
									newStr.append(fontList->GetAt(j)->Key->ToString()->Data());
									newStr.insert(0, L"FontFamily=\"").append(L"\"");

									strXaml = std::regex_replace(strXaml, oldStrPattern, newStr);
									break;
								}
							}
						}
						tmp = m.suffix();
					}
				}

				if (_this->EnableStyleOverride)
				{
					//Foreground 없애기
					strXaml = regex_replace(strXaml, _this->ForegroundExp, L"");
					//FontWeight 없애기
					strXaml = regex_replace(strXaml, _this->FontWeightExp, L"");
					//FontStyle 없애기
					strXaml = regex_replace(strXaml, _this->FontStyleExp, L"");
				}

				xamlText = ref new String(strXaml.c_str());
			}
			catch (Exception^ ex)
			{
				OutputDebugMessage(L"==================== Html => Xaml 변환 오류 ======================\n");
				OutputDebugMessage(ex->Message->Data());
				OutputDebugMessage(L"\n==================================================================\n");

				xamlText = StringHelper::HtmlTextToXamlText("");
			}

			Windows::Data::Json::JsonObject^ continuouslyRect = nullptr;
			int continuouslyRectIndex = -1;

			//연결할 자막 검색 (플리커 현상 방지위해)
			if (_this->ccJsonArray->Size > 0)
			{
				for (unsigned int j = 0; j < _this->ccJsonArray->Size; j++)
				{
					//삭제 대상(이전 자막들)의 "Rect"
					auto rt = _this->ccJsonArray->GetObjectAt(j);
					double lastEndTimeDuration = rt->GetNamedNumber("EndTimeDuration");

					double rsts = floor((double)sdTS.Duration / 1000000);
					double rets = floor(lastEndTimeDuration / 1000000);

					if (rsts == rets)
					{
						continuouslyRect = rt;
						continuouslyRectIndex = j;
						break;
					}
				}
			}

			if (type != 5)
			{
				//삭제대상(이전 자막목록)에 추가
				_this->ccJsonArray->Append(rect);
			}

			//OutputDebugStringW(L"\n");
			//OutputDebugStringW(xamlText->Data());
			//OutputDebugStringW(L"\n");

			String^ outlineXamlText = nullptr;
			String^ paragraphText = nullptr;

			for (uint32 j = 0; j < _this->richTextList->Size; j++)
			{
				auto richTxt = dynamic_cast<RichTextBlock^>(_this->richTextList->GetAt(j));
				if (richTxt != nullptr)
				{
					if (richTxt->Tag->ToString() == "OutlineText" || richTxt->Tag->ToString() == "ShadowText")
					{
						if (outlineXamlText == nullptr)
						{
							std::wstring sxt(xamlText->Data());
							sxt = regex_replace(sxt, _this->ForegroundExp, L"");
							outlineXamlText = ref new String(sxt.c_str());
						}
						paragraphText = outlineXamlText;
						//OutputDebugMessage(L"outline : %s\n", outlineXamlText->Data());
					}
					else
					{
						paragraphText = xamlText;
					}

					auto paragraph = (Paragraph^)Windows::UI::Xaml::Markup::XamlReader::Load(paragraphText);

					//OutputDebugString(paragraphText->Data());
					//OutputDebugString(L"\n");

					if (type == 5) //SAMI 자막의 경우
					{
						std::vector<bool> langSlots;
						IVector<SubtitleLanguage^>^ subLanguages = nullptr;

						if (_this->SubLanguageSource != nullptr)
						{
							String^ selectedSubLangCode = _this->SelectedSubLanguageCode;
							String^ currSubLangCode = rect->GetNamedString("Lang");
							String^ allSubLangCode = "ALLCC";

							subLanguages = dynamic_cast<IVector<SubtitleLanguage^>^>(_this->SubLanguageSource);
							/*if (_this->SelectedSubLanguageCode != "ALLCC" 
								&& _this->SelectedSubLanguageCode != rect->GetNamedString("Lang"))*/
							if (!allSubLangCode->Equals(selectedSubLangCode) && 
								(currSubLangCode != nullptr && !currSubLangCode->Equals(selectedSubLangCode)))
							{
								//전체 표시가 아닌데, 서브 랭귀지가 다른 경우 처리하지 않음
								return;
							}
						}
						else
						{
							subLanguages = ref new Platform::Collections::Vector<SubtitleLanguage^>();
						}

						for (int k = 0; k < subLanguages->Size; k++)
						{
							langSlots.push_back(false);
						}

						//SMI는 종료시간이 바로 다음 자막이므로 통합자막 (멀티 랭귀지)인 경우, 종료시간이 안맞는 경우가 대부분임 => FFmpeg 에서
						//SMI는 원래 자막 스펙에 종료 시간이 없으므로, 현재 표시하려는 Lang코드에 해당 되는 자막 블록을 검색하여 덮어쓴다.
						//해당 블록 교체
						int blockIndex = -1; //교체할 블록의 인덱스
						String^ lang = nullptr;

						for (unsigned int k = richTxt->Blocks->Size; k > 0; k--)
						{
							auto block = richTxt->Blocks->GetAt(k - 1);

							std::wstringstream ss(block->Name->Data());
							std::wstring token;
							std::wstring token2;
							
							while (std::getline(ss, token, L','))
							{
								std::wstringstream ss2(token);
								std::vector<std::wstring> tmp;
								while (std::getline(ss2, token2, L':'))
								{
									tmp.push_back(token2);
								}

								if (!tmp.empty() && tmp.size() == 2)
								{
									if (tmp.at(0) == L"Lang")
									{
										lang = ref new String(tmp.at(1).c_str());
										
										for (int l = 0; l < langSlots.size(); l++)
										{
											if (tmp.at(1) == subLanguages->GetAt(l)->Code->Data())
											{
												langSlots.at(l) = true;
											}
										}
									}
								}
							}

							if (lang != nullptr && lang == rect->GetNamedString("Lang"))
							{
								blockIndex = k - 1;
								//break;
							}
						}

						if (blockIndex != -1 && blockIndex < richTxt->Blocks->Size)
						{
							richTxt->Blocks->RemoveAt(blockIndex);
							richTxt->Blocks->InsertAt(blockIndex, paragraph);
						}
						else if (blockIndex == -1)
						{
							//새롭게 추가 되는 자막.
							//스트림 순서에 맞게 위치를 찾아 삽입한다.
							if (richTxt->Blocks->Size > 0)
							{
								int offset = 0;
								int cnt = 0;
								for (int l = 0; l < langSlots.size(); l++)
								{
									if (!langSlots.at(l))
									{
										if (rect->GetNamedString("Lang") == subLanguages->GetAt(l)->Code)
										{
											//여기가 삽입될 위치
											offset = l;
											break;
										}
										else
										{
											cnt++;
										}
									}
								}
								//해당 자막 언어의 이전까지의 갯수를 빼면 삽입할 인덱스가 된다.
								offset -= cnt;
								if (offset >= 0 && offset < richTxt->Blocks->Size)
								{
									richTxt->Blocks->InsertAt(offset, paragraph);
								}
								else
								{
									richTxt->Blocks->Append(paragraph);
								}
							}
							else
							{
								richTxt->Blocks->Append(paragraph);
							}
						}
						else if (richTxt->Blocks->Size == 0)
						{
							//들어올 수 없음(임시 테스트 중)
							//richTxt->Blocks->Append(paragraph);
							__debugbreak();
						}
					}
					else
					{
						//SMI이외의 자막
						if (continuouslyRect == nullptr)
						{
							//블록에 새롭게 추가
							richTxt->Blocks->Append(paragraph);
						}
						else
						{
							//해당 블록 교체
							for (unsigned int k = richTxt->Blocks->Size; k > 0; k--)
							{
								auto block = richTxt->Blocks->GetAt(k - 1);
								if (block->Name == continuouslyRect->GetNamedString("Guid"))
								{
									richTxt->Blocks->RemoveAt(k - 1);
									richTxt->Blocks->InsertAt(k - 1, paragraph);

									//다음 rect를 위해 교체후 삭제
									if (_this->richTextList->Size == j + 1)
									{
										continuouslyRect = nullptr;
										_this->ccJsonArray->RemoveAt(continuouslyRectIndex);
									}

									break;
								}
							}
						}
					}
				}
			}
		}
	}
}
