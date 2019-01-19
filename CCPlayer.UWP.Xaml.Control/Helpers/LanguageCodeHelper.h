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
				public ref class LanguageCodeHelper sealed
				{
				private:
					static property IMap<String^, String^>^ _ThreeLetterToTwoLetterLanguageCodeMap;
				public:
					static property IMap<String^, String^>^ ThreeLetterToTwoLetterLanguageCodeMap
					{
						IMap<String^, String^>^  get()
						{
							if (_ThreeLetterToTwoLetterLanguageCodeMap == nullptr)
							{
								_ThreeLetterToTwoLetterLanguageCodeMap = ref new Platform::Collections::Map<String^, String^>();
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("abk", "ab");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("aar", "aa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("afr", "af");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("aka", "ak");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sqi", "sq");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("alb", "sq");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("amh", "am");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ara", "ar");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("arg", "an");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hye", "hy");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("arm", "hy");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("asm", "as");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ava", "av");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ave", "ae");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("aym", "ay");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("aze", "az");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bam", "bm");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bak", "ba");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("eus", "eu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("baq", "eu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bel", "be");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ben", "bn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bih", "bh");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bis", "bi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bos", "bs");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bre", "br");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bul", "bg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mya", "my");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bur", "my");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cat", "ca");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cha", "ch");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("che", "ce");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nya", "ny");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("chi", "zh");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("zho", "zh");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("chv", "cv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cor", "kw");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cos", "co");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cre", "cr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hrv", "hr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cze", "cs");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ces", "cs");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("dan", "da");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("div", "dv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("dut", "nl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nld", "nl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("dzo", "dz");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("eng", "en");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("epo", "eo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("est", "et");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ewe", "ee");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fao", "fo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fij", "fj");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fin", "fi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fre", "fr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fra", "fr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ful", "ff");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("glg", "gl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("geo", "ka");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kat", "ka");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ger", "de");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("deu", "de");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("gre", "el");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ell", "el");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("grn", "gn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("guj", "gu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hat", "ht");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hau", "ha");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("heb", "he");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("her", "hz");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hin", "hi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hmo", "ho");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("hun", "hu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ina", "ia");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ind", "id");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ile", "ie");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("gle", "ga");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ibo", "ig");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ipk", "ik");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ido", "io");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ice", "is");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("isl", "is");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ita", "it");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("iku", "iu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("jpn", "ja");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("jav", "jv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kal", "kl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kan", "kn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kau", "kr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kas", "ks");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kaz", "kk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("khm", "km");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kik", "ki");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kin", "rw");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kir", "ky");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kom", "kv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kon", "kg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kor", "ko");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kur", "ku");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("kua", "kj");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lat", "la");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ltz", "lb");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lug", "lg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lim", "li");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lin", "ln");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lao", "lo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lit", "lt");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lub", "lu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("lav", "lv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("glv", "gv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mac", "mk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mkd", "mk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mlg", "mg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("may", "ms");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("msa", "ms");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mal", "ml");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mlt", "mt");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mao", "mi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mri", "mi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mar", "mr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mah", "mh");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("mon", "mn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nau", "na");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nav", "nv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nde", "nd");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nep", "ne");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ndo", "ng");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nob", "nb");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nno", "nn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nor", "no");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("iii", "ii");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("nbl", "nr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("oci", "oc");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("oji", "oj");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("chu", "cu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("orm", "om");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ori", "or");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("oss", "os");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("pan", "pa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("pli", "pi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("per", "fa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fas", "fa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("pol", "pl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("pus", "ps");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("por", "pt");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("que", "qu");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("roh", "rm");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("run", "rn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("rum", "ro");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ron", "ro");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("rus", "ru");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("san", "sa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("srd", "sc");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("snd", "sd");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sme", "se");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("smo", "sm");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sag", "sg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("srp", "sr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("gla", "gd");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sna", "sn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sin", "si");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("slo", "sk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("slk", "sk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("slv", "sl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("som", "so");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sot", "st");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("spa", "es");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("sun", "su");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("swa", "sw");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ssw", "ss");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("swe", "sv");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tam", "ta");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tel", "te");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tgk", "tg");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tha", "th");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tir", "ti");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tib", "bo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("bod", "bo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tuk", "tk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tgl", "tl");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tsn", "tn");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ton", "to");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tur", "tr");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tso", "ts");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tat", "tt");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("twi", "tw");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("tah", "ty");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("uig", "ug");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ukr", "uk");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("urd", "ur");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("uzb", "uz");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("ven", "ve");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("vie", "vi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("vol", "vo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("wln", "wa");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("wel", "cy");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("cym", "cy");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("wol", "wo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("fry", "fy");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("xho", "xh");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("yid", "yi");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("yor", "yo");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("zha", "za");
								_ThreeLetterToTwoLetterLanguageCodeMap->Insert("zul", "zu");
							}
							return _ThreeLetterToTwoLetterLanguageCodeMap;
						}
					}

					static String^ GetTwoLetterCode(String^ languageCode)
					{
						String^ letterCode = nullptr;
						if (languageCode != nullptr)
						{
							std::wstring code = std::wstring(languageCode->Data());
							size_t offset = code.find_first_of(L"-", 0);


							// -(하이픈)이 존재하면 앞자리만 취득
							if (offset >= 0 && offset < code.length())
							{
								letterCode = ref new String(code.substr(0, offset).c_str());
								//std::wstring rgn = code.substr(offset + 1, code.length() - offset - 1);
							}
							else
							{
								letterCode = ref new String(languageCode->Data());
							}
							//3자리 코드 => 2자리 코드 변환
							if (letterCode->Length() == 3)
							{
								if (ThreeLetterToTwoLetterLanguageCodeMap->HasKey(letterCode))
								{
									letterCode = ThreeLetterToTwoLetterLanguageCodeMap->Lookup(letterCode);
								}
							}
						}

						return letterCode;
					}
				};
			}
		}
	}
}

