#pragma once
#include <pch.h>

using namespace std;
using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace Helpers
			{
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class CodePage sealed
				{
				public:
					property String^ Key;
					property int Value;
					CodePage(String^ key, int value)
					{
						Key = key;
						Value = value;
					}
					property String^ Name
					{
						String^ get()
						{
							auto loader = Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView();
							return loader->GetString("Charset" + Key);
						}
					}

				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class CodePageHelper sealed
				{
				private:
					static property Windows::Foundation::Collections::IVector<CodePage^>^ _CodePages;
					static property Windows::Foundation::Collections::IVector<CodePage^>^ _CharsetCodePage;

				public:
					static property int AUTO_DETECT { int get() { return -1; } }

					static property Windows::Foundation::Collections::IVector<CodePage^>^ CharsetCodePage
					{
						Windows::Foundation::Collections::IVector<CodePage^>^ get()
						{
							if (_CharsetCodePage == nullptr)
							{
								_CharsetCodePage = ref new Platform::Collections::Vector<CodePage^>();
								_CharsetCodePage->Append(ref new CodePage("AutoDetect", AUTO_DETECT));
								_CharsetCodePage->Append(ref new CodePage("UTF_8", 65001));
								_CharsetCodePage->Append(ref new CodePage("UTF_16LE", 1200));
								_CharsetCodePage->Append(ref new CodePage("UTF_16BE", 1201));
								//Windows Code Pages
								_CharsetCodePage->Append(ref new CodePage("CentralEuropeanWindows", 1250));
								_CharsetCodePage->Append(ref new CodePage("CyrillicWindows", 1251));
								_CharsetCodePage->Append(ref new CodePage("WesternEuropeanWindows", 1252));
								_CharsetCodePage->Append(ref new CodePage("GreekWindows", 1253));
								_CharsetCodePage->Append(ref new CodePage("TurkishWindows", 1254));
								_CharsetCodePage->Append(ref new CodePage("HebrewWindows", 1255));
								_CharsetCodePage->Append(ref new CodePage("ArabicWindows", 1256));
								_CharsetCodePage->Append(ref new CodePage("BalticWindows", 1257));
								_CharsetCodePage->Append(ref new CodePage("VietnameseWindows", 1258));
								_CharsetCodePage->Append(ref new CodePage("ThaiWindows", 874));
								_CharsetCodePage->Append(ref new CodePage("JapaneseShift_JIS", 932));
								_CharsetCodePage->Append(ref new CodePage("ChineseSimplifiedGB", 936)); //China, Singapore
								_CharsetCodePage->Append(ref new CodePage("KoreanEUC", 949));
								_CharsetCodePage->Append(ref new CodePage("KoreanMS", 51949));
								_CharsetCodePage->Append(ref new CodePage("ChineseTraditionalBig5", 950)); //Taiwan, Hong Kong, Macau
																										   //OEM Code Pages
								_CharsetCodePage->Append(ref new CodePage("UsOem", 437));
								_CharsetCodePage->Append(ref new CodePage("ArabicOem", 720));
								_CharsetCodePage->Append(ref new CodePage("GreekOem", 737));
								_CharsetCodePage->Append(ref new CodePage("BalticOem", 775));
								_CharsetCodePage->Append(ref new CodePage("MultilingualLatinIOem", 850));
								_CharsetCodePage->Append(ref new CodePage("LatinIIOem", 852));
								_CharsetCodePage->Append(ref new CodePage("CyrillicOem", 855));
								_CharsetCodePage->Append(ref new CodePage("TurkishOem", 857));
								_CharsetCodePage->Append(ref new CodePage("MultilingualLatinIEuroOem", 858));
								_CharsetCodePage->Append(ref new CodePage("HebrewOem", 862));
								_CharsetCodePage->Append(ref new CodePage("RussianOem", 866));
								//ISO Code Pages
								_CharsetCodePage->Append(ref new CodePage("WesternEuropeanISO", 28591)); //Latin1 ISO-8859-1
								_CharsetCodePage->Append(ref new CodePage("CentralEuropeanISO", 28592)); //Latin2 ISO-8859-2
								_CharsetCodePage->Append(ref new CodePage("Latin3", 28593)); //ISO-8859-3
								_CharsetCodePage->Append(ref new CodePage("BalticISO", 28594)); //ISO-8859-4
								_CharsetCodePage->Append(ref new CodePage("CyrillicISO", 28595)); //ISO-8859-5
								_CharsetCodePage->Append(ref new CodePage("ArabicISO", 28596)); //ISO-8859-6
								_CharsetCodePage->Append(ref new CodePage("HebrewISO_Visual", 28598)); //ISO-8859-8
								_CharsetCodePage->Append(ref new CodePage("TurkishISO", 28599)); //ISO-8859-9
								_CharsetCodePage->Append(ref new CodePage("Latin9", 28605)); //ISO-8859-15

							}
							return _CharsetCodePage;
						}
					}

					static property Windows::Foundation::Collections::IVector<CodePage^>^ CodePages
					{
						Windows::Foundation::Collections::IVector<CodePage^>^ get()
						{
							if (_CodePages == nullptr)
							{
								_CodePages = ref new Platform::Collections::Vector<CodePage^>();
								//http://msdn.microsoft.com/en-us/library/ee825488(v=cs.20).aspx  
								_CodePages->Append(ref new CodePage("pl-PL", 1250));
								_CodePages->Append(ref new CodePage("uk-UA", 1250));
								_CodePages->Append(ref new CodePage("sk-SK", 1250));
								_CodePages->Append(ref new CodePage("cs-CZ", 1250));
								_CodePages->Append(ref new CodePage("be-BY", 1250));
								_CodePages->Append(ref new CodePage("lt-LT", 1250));
								_CodePages->Append(ref new CodePage("lv-LV", 1250));
								_CodePages->Append(ref new CodePage("ro-RO", 1250));
								_CodePages->Append(ref new CodePage("hu-HU", 1250));
								_CodePages->Append(ref new CodePage("sl-SI", 1250));
								_CodePages->Append(ref new CodePage("hr-HR", 1250));
								_CodePages->Append(ref new CodePage("Lt-sr-SP", 1250));
								_CodePages->Append(ref new CodePage("ro-RO", 1250));
								_CodePages->Append(ref new CodePage("sq-AL", 1250));
								_CodePages->Append(ref new CodePage("de-AT", 1250));
								_CodePages->Append(ref new CodePage("nl-BE", 1250));
								_CodePages->Append(ref new CodePage("de-DE", 1250));
								_CodePages->Append(ref new CodePage("de-LI", 1250));
								_CodePages->Append(ref new CodePage("de-LU", 1250));
								_CodePages->Append(ref new CodePage("de-CH", 1250));
								_CodePages->Append(ref new CodePage("Lt-uz-UZ", 1250));
								_CodePages->Append(ref new CodePage("Lt-az-AZ", 1250));

								_CodePages->Append(ref new CodePage("ru-RU", 1251));
								_CodePages->Append(ref new CodePage("bg-BG", 1251));
								_CodePages->Append(ref new CodePage("Cy-sr-SP", 1251));
								_CodePages->Append(ref new CodePage("mk-MK", 1251));
								_CodePages->Append(ref new CodePage("kk-KZ", 1251));
								_CodePages->Append(ref new CodePage("ky-KZ", 1251));
								_CodePages->Append(ref new CodePage("mn-MN", 1251));
								_CodePages->Append(ref new CodePage("uk-UA", 1251));
								_CodePages->Append(ref new CodePage("Cy-uz-UZ", 1251));
								_CodePages->Append(ref new CodePage("Cy-az-AZ", 1251));

								_CodePages->Append(ref new CodePage("el-GR", 1253));
								_CodePages->Append(ref new CodePage("tr-TR", 1254));
								_CodePages->Append(ref new CodePage("he-IL", 1255));

								_CodePages->Append(ref new CodePage("ar-DZ", 1256));
								_CodePages->Append(ref new CodePage("ar-BH", 1256));
								_CodePages->Append(ref new CodePage("ar-EG", 1256));
								_CodePages->Append(ref new CodePage("ar-IQ", 1256));
								_CodePages->Append(ref new CodePage("ar-JO", 1256));
								_CodePages->Append(ref new CodePage("ar-KW", 1256));
								_CodePages->Append(ref new CodePage("ar-LB", 1256));
								_CodePages->Append(ref new CodePage("ar-LY", 1256));
								_CodePages->Append(ref new CodePage("ar-MA", 1256));
								_CodePages->Append(ref new CodePage("ar-OM", 1256));
								_CodePages->Append(ref new CodePage("ar-QA", 1256));
								_CodePages->Append(ref new CodePage("ar-SA", 1256));
								_CodePages->Append(ref new CodePage("ar-SY", 1256));
								_CodePages->Append(ref new CodePage("ar-TN", 1256));
								_CodePages->Append(ref new CodePage("ar-AE", 1256));
								_CodePages->Append(ref new CodePage("ar-YE", 1256));

								_CodePages->Append(ref new CodePage("et-EE", 1257));
								_CodePages->Append(ref new CodePage("lv-LV", 1257));
								_CodePages->Append(ref new CodePage("lt-LT", 1257));

								_CodePages->Append(ref new CodePage("vi-VN", 1258));

								_CodePages->Append(ref new CodePage("th-TH", 874));
								_CodePages->Append(ref new CodePage("ja-JP", 932));

								_CodePages->Append(ref new CodePage("zh-CN", 936));
								_CodePages->Append(ref new CodePage("zh-SG", 936));
								_CodePages->Append(ref new CodePage("zh-CHS", 936));

								_CodePages->Append(ref new CodePage("ko-KR", 949));

								_CodePages->Append(ref new CodePage("zh-HK", 950));
								_CodePages->Append(ref new CodePage("zh-MO", 950));
								_CodePages->Append(ref new CodePage("zh-CHT", 950));
							}
							return _CodePages;
						}
					}

					static CodePage^ GetCodePageByCharset(String^ codePageName)
					{
						for (unsigned int i = 0; i < CharsetCodePage->Size; i++)
						{
							if (CharsetCodePage->GetAt(i)->Key == codePageName)
							{
								return CharsetCodePage->GetAt(i);
							}
						}
						return nullptr;
					}

					static CodePage^ GetCodePageByLangCode(String^ langCode)
					{
						for (unsigned int i = 0; i < CodePages->Size; i++)
						{
							if (CodePages->GetAt(i)->Key == langCode)
							{
								return CodePages->GetAt(i);
							}
						}
						return nullptr;
					}
				};


			}
		}
	}
}

