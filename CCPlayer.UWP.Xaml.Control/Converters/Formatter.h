#pragma once
#include <pch.h>
//#include "Common.h"

using namespace std;
using namespace Platform;
using namespace Windows::Foundation;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Converters
			{
				///
				/// Formatters
				///
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class ZoomValueFormatter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						Platform::String^ result = "";

						if (value != nullptr)
						{
							std::wstring wid_str(value->ToString()->Data());

							size_t offset = wid_str.find_first_of(L".", 0U);
							std::wstring wid_str2(wid_str.substr(0, offset + 3));

							const wchar_t* w_char = wid_str2.c_str();
							result = ref new String(w_char);
						}
						return result;
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						throw ref new NotImplementedException();
					}
				};
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class PercentageFormatter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double per = safe_cast<double>(value);
						if (!std::isnan(per))
						{
							per = per * 100.0;
							return floor(per);
						}
						return 0.0;
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double per = safe_cast<double>(value);
						if (!std::isnan(per))
						{
							return per / 100.0;
						}
						return 0.0;
					}
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class TimeFormatter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						TimeSpan time = safe_cast<TimeSpan>(value);
						long hms = (long)(time.Duration / 10000000L);
						bool isNegative = false;

						if (hms < 0)
						{
							isNegative = true;
							hms *= -1;
						}

						long h = hms / 3600;
						long m = (hms % 3600) / 60;
						long s = (hms % 60);

						wchar_t buff[50];
						swprintf_s(buff, L"%d:%02d:%02d", h, m, s);

						return ref new String(buff);
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						return ref new NotImplementedException();
					}
				};

				///
				/// Converters
				///
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class OpacityConverter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double per = safe_cast<double>(value);
						if (!std::isnan(per))
						{
							return 1 - per / 100;
						}
						return 0.0;
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double per = safe_cast<double>(value);
						if (!std::isnan(per))
						{
							return (int)(100 - per * 100);
						}
						return (Platform::Object ^)0;
					}
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class TimeSpanToDoubleConverter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						TimeSpan time = safe_cast<TimeSpan>(value);
						return (double)(time.Duration / 10000000L);
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double sec = safe_cast<double>(value);
						TimeSpan time;
						time.Duration = (long long)(sec * 10000000L);
						return time;
					}
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class DevideConverter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						if (value == nullptr) return value;

						double val = (double)value;
						double param = (double)parameter;
						auto result = (float)val / param;
						return result;
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						return ref new NotImplementedException();
					}
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class RawPixelsPerViewPixelConverter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						double val = (double)value; //이미 폰트의 크기에 반영이 되어 있다...??
						auto displayInfo = Windows::Graphics::Display::DisplayInformation::GetForCurrentView();
						auto rawPixelsPerViewPixel = (float)displayInfo->RawPixelsPerViewPixel;
						auto sqrt = std::sqrt(rawPixelsPerViewPixel);
						auto result = (double)val / sqrt;
						return result;
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						return ref new NotImplementedException();
					}
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class BoolToNullableBoolConverter sealed : Windows::UI::Xaml::Data::IValueConverter
				{
				public:
					virtual Platform::Object^ Convert(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						return ref new Box<bool>(static_cast<bool>(value));
					}

					// No need to implement converting back on a one-way binding 
					virtual Platform::Object^ ConvertBack(Platform::Object^ value, Windows::UI::Xaml::Interop::TypeName targetType,
						Platform::Object^ parameter, Platform::String^ language)
					{
						Box<bool>^ boolVal = dynamic_cast<Box<bool>^>(value);
						return boolVal->Value;
					}
				};
			}
		}
	}
}

