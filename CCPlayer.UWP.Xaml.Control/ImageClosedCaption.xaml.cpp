//
// ImageClosedCaption.xaml.cpp
// Implementation of the ImageClosedCaption class
//

#include "pch.h"
#include "ImageClosedCaption.xaml.h"
//#include "ClosedCaptions.xaml.h"
#include <Robuffer.h>


using namespace CCPlayer::UWP::Xaml::Controls;

using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::Storage::Streams;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

ImageClosedCaption::ImageClosedCaption()
{
	InitializeComponent();
	SubtitleImageMap = ref new Platform::Collections::Map<String^, CCPlayer::UWP::Common::Codec::ImageData^>();
}

ImageClosedCaption::~ImageClosedCaption()
{
	OutputDebugMessage(L"Called constructor of the ImageClosedCaption\n");
}

void ImageClosedCaption::ClearClosedCaption(TimeSpan position)
{
	//JsonData = nullptr;
}

double ImageClosedCaption::VideoSizeRatio::get()
{
	//float videoSizeRatio = min(this->DisplayVideoSize.Width / this->NaturalVideoSize.Width, this->DisplayVideoSize.Height / this->NaturalVideoSize.Height);
	auto ntRatio = this->NaturalVideoSize.Width / this->NaturalVideoSize.Height;
	auto dsRatio = this->DisplayVideoSize.Width / this->DisplayVideoSize.Height;
	float videoSizeRatio = 1;

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
		videoSizeRatio = this->DisplayVideoSize.Width / this->NaturalVideoSize.Width;
	}
	else
	{
		//세로 기준
		videoSizeRatio = this->DisplayVideoSize.Height / this->NaturalVideoSize.Height;
	}

	if (std::isnan(videoSizeRatio) || std::isinf(videoSizeRatio))
	{
		return 1;
	}
	return videoSizeRatio;
}

DEPENDENCY_PROPERTY_REGISTER(BackgroundVisibility, Windows::UI::Xaml::Visibility, CCPlayer::UWP::Xaml::Controls::ImageClosedCaption, Windows::UI::Xaml::Visibility::Collapsed);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(JsonData, Windows::Data::Json::JsonObject, CCPlayer::UWP::Xaml::Controls::ImageClosedCaption, nullptr);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(FontSizeRatio, double, CCPlayer::UWP::Xaml::Controls::ImageClosedCaption, 1.0);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(DisplayVideoSize, Size, CCPlayer::UWP::Xaml::Controls::ImageClosedCaption, Size::Empty);
DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(NaturalVideoSize, Size, CCPlayer::UWP::Xaml::Controls::ImageClosedCaption, Size::Empty);

void ImageClosedCaption::OnJsonDataChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ e)
{
	auto _this = dynamic_cast<CCPlayer::UWP::Xaml::Controls::ImageClosedCaption^>(sender);
	auto rect = dynamic_cast<Windows::Data::Json::JsonObject^>(e->NewValue);

	if (rect == nullptr)
	{
		_this->ClosedCaptionImage->Source = nullptr;
		_this->ClosedCaptionImage->Margin = Thickness(0);
	}
	else
	{
		String^ guid = rect->GetNamedString("Guid");
		if (_this->SubtitleImageMap->HasKey(guid))
		{
			auto subData = _this->SubtitleImageMap->Lookup(guid);
			auto imgData = subData->ImagePixelData;

			int width = (int)rect->GetNamedNumber("Width");
			int height = (int)rect->GetNamedNumber("Height");
			_this->_CurrentImageSize = Size(width, height);

			auto bitmap = ref new Windows::UI::Xaml::Media::Imaging::WriteableBitmap(width, height);
			byte* pDstPixels = nullptr;
			// Get access to the pixels
			IBuffer^ buffer = bitmap->PixelBuffer;

			// Obtain IBufferByteAccess
			Microsoft::WRL::ComPtr<IBufferByteAccess> pBufferByteAccess;
			Microsoft::WRL::ComPtr<IUnknown> pBuffer((IUnknown*)buffer);
			pBuffer.As(&pBufferByteAccess);

			// Get pointer to pixel bytes
			pBufferByteAccess->Buffer(&pDstPixels);

			if (subData->CodecId == AV_CODEC_ID_HDMV_PGS_SUBTITLE)
			{
				for (int i = 0; i < width * height; i++)
				{
					int pos = i * 4;
					pDstPixels[pos] = imgData[pos]; //b
					pDstPixels[pos + 1] = imgData[pos + 1]; //g
					pDstPixels[pos + 2] = imgData[pos + 2]; //r
					pDstPixels[pos + 3] = imgData[pos + 3]; //a

					if (pDstPixels[pos + 3] < 16)
					{
						pDstPixels[pos] = 0; //b
						pDstPixels[pos + 1] = 0; //g
						pDstPixels[pos + 2] = 0; //r
					}
				}
			}
			else if (subData->CodecId == AV_CODEC_ID_XSUB)
			{
				for (int i = 0; i < width * height; i++)
				{
					int pos = i * 4;
					pDstPixels[pos] = imgData[pos]; //b
					pDstPixels[pos + 1] = imgData[pos + 1]; //g
					pDstPixels[pos + 2] = imgData[pos + 2]; //r
					pDstPixels[pos + 3] = imgData[pos + 3]; //a
				}
			}

			//UI반영
			_this->ClosedCaptionImage->Source = bitmap;
			
			//이미지 자막의 화면 크기
			auto pictWidth = _this->JsonData->GetNamedNumber("PictureWidth");
			auto pictHeight = _this->JsonData->GetNamedNumber("PictureHeight");

			//인코딩된 비디오의 원본대비 크기 비율
			auto vidWidthRatio = _this->NaturalVideoSize.Width / pictWidth;
			auto vidHeightRatio = _this->NaturalVideoSize.Height/ pictHeight;

			//이미지 크기
			//_this->ClosedCaptionImage->Width = width * vidWidthRatio;
			//_this->ClosedCaptionImage->Height = height * vidHeightRatio;
			
			//인코딩된 비디오의 크기 비율
			_this->_VideoEncodedSizeRatio = max(vidWidthRatio, vidHeightRatio);
			//이미지 크기
			_this->ClosedCaptionImage->Width = _this->_CurrentImageSize.Width * _this->VideoSizeRatio * _this->_VideoEncodedSizeRatio * _this->FontSizeRatio;
			_this->ClosedCaptionImage->Height = _this->_CurrentImageSize.Height * _this->VideoSizeRatio * _this->_VideoEncodedSizeRatio * _this->FontSizeRatio;

			if (_this->BackgroundVisibility == Windows::UI::Xaml::Visibility::Visible)
			{
				//(백그라운드 표시를 위한) 여백 설정
				_this->ClosedCaptionImage->Margin = Thickness(8);
			}
			else
			{
				_this->ClosedCaptionImage->Margin = Thickness(0);
			}
			//위치 이동 (좌측,상단 좌표를 중심기준으로 변환)
			_this->SetPosition();
		}
	}
}

void CCPlayer::UWP::Xaml::Controls::ImageClosedCaption::OnFontSizeRatioChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ImageClosedCaption^>(sender);
	//이미지 크기
	_this->SetScale();
	//위치 이동 (좌측,상단 좌표를 중심기준으로 변환)
	_this->SetPosition();
}

void CCPlayer::UWP::Xaml::Controls::ImageClosedCaption::OnDisplayVideoSizeChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ImageClosedCaption^>(sender);
	//이미지 크기
	_this->SetScale();
	//위치 이동 (좌측,상단 좌표를 중심기준으로 변환)
	_this->SetPosition();
}

void CCPlayer::UWP::Xaml::Controls::ImageClosedCaption::OnNaturalVideoSizeChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args)
{
	auto _this = safe_cast<ImageClosedCaption^>(sender);
	//이미지 크기
	_this->SetScale();
	//위치 이동 (좌측,상단 좌표를 중심기준으로 변환)
	_this->SetPosition();
}

void CCPlayer::UWP::Xaml::Controls::ImageClosedCaption::SetPosition()
{
	if (this->JsonData != nullptr)
	{
		//좌측/상단 기준좌표를 중심기준좌표로 변환후 이동

		//비디오 영상의 원본 크기
		auto videoWidth = this->NaturalVideoSize.Width;
		auto videoHeight = this->NaturalVideoSize.Height;

		//이미지 자막의 화면 크기
		auto pictWidth = this->JsonData->GetNamedNumber("PictureWidth");
		auto pictHeight = this->JsonData->GetNamedNumber("PictureHeight");

		//실제 비디오와 자막 화면 크기의 비율
		auto vidWidthRatio = videoWidth / pictWidth;
		auto vidHeightRatio = videoHeight / pictHeight;
		
		//이미지 자막의 센터 x,y (실제 비디오 크기에 따른 비례 적용 전)
		auto subCenterX = this->JsonData->GetNamedNumber("Width") / 2 + this->JsonData->GetNamedNumber("Left");
		auto subCenterY = this->JsonData->GetNamedNumber("Height") / 2 + this->JsonData->GetNamedNumber("Top");

		//이미지 자막의 센터 x,y (실제 비디오 크기에 따른 비례 적용 후)
		subCenterX *= vidWidthRatio;
		subCenterY *= vidHeightRatio;

		//실제 비디오의 중심점 x,y (실제 비디오 크기에 따른 비례 적용 전)
		auto vidCenterX = videoWidth / 2;
		auto vidCenterY = videoHeight / 2;

		auto translateX = subCenterX - vidCenterX;
		auto translateY = subCenterY - vidCenterY;

		auto transform = safe_cast<Windows::UI::Xaml::Media::CompositeTransform^>(this->ImageBorder->RenderTransform);

		transform->TranslateX = translateX * this->DisplayVideoSize.Width / this->NaturalVideoSize.Width;
		transform->TranslateY = translateY * this->DisplayVideoSize.Height / this->NaturalVideoSize.Height;
	}
}

void CCPlayer::UWP::Xaml::Controls::ImageClosedCaption::SetScale()
{
	//auto transform = safe_cast<Windows::UI::Xaml::Media::CompositeTransform^>(this->ImageBorder->RenderTransform);
	//auto scale = this->VideoSizeRatio * this->FontSizeRatio;
	//transform->ScaleX = scale;
	//transform->ScaleY = scale;

	this->ClosedCaptionImage->Width = this->_CurrentImageSize.Width * this->VideoSizeRatio * this->_VideoEncodedSizeRatio * this->FontSizeRatio;
	this->ClosedCaptionImage->Height = this->_CurrentImageSize.Height * this->VideoSizeRatio * this->_VideoEncodedSizeRatio * this->FontSizeRatio;
}