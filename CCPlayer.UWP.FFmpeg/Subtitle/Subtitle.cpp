#include "pch.h"
#include "Subtitle.h"
#include "shcore.h"

#include <regex>
#include <algorithm>
#include <string>
#include <iostream>
#include <sstream>
#include <mutex>

using namespace std::tr1;
using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::FFmpeg::Subtitle;
using namespace Platform;

std::wstring ltrim(std::wstring str) {
	return std::regex_replace(str, std::wregex(L"^\\s+"), std::wstring(L""));
}

std::wstring rtrim(std::wstring str) {
	return std::regex_replace(str, std::wregex(L"\\s+$"), std::wstring(L""));
}

std::wstring trim(std::wstring str) {
	return ltrim(rtrim(str));
}


void SubtitleHelper::LoadASSHeader(String^ header,
	PropertySet^ *scriptInfoProp,
	IMap<String^, Windows::Data::Json::JsonObject^>^ *styleMap,
	std::vector<std::wstring>* eventList)
{
	const  std::wstring delimiter(L",");
	const  wregex keyValueExp(L"(^[^;]\\s*.+?)\\s*:\\s*(.+?)\r\n");
	//const wregex dialogueExp(L"\\s*Dialogue\\s*:\\s*(.+?)\r\n", regex_constants::ECMAScript | regex_constants::icase);
	const  wregex scriptInfoExp(L"\\s*\\[\\s*Script\\s+Info\\s*\\]\\s*\r\n", regex_constants::ECMAScript | regex_constants::icase);
	const  wregex v4StyleExp(L"\\s*\\[\\s*V\\d{1}[\\+]*\\s+Styles\\s*\\]\\s*\r\n", regex_constants::ECMAScript | regex_constants::icase);
	const  wregex eventsExp(L"\\s*\\[\\s*Events\\s*\\]\\s*\r\n", regex_constants::ECMAScript | regex_constants::icase);

	std::wstring wHeader(header->Data());
	wsmatch m;
	bool openScriptInfo = false;
	bool openV4pStyle = false;
	bool isASS = false;
	std::wstring strScriptInfo;
	std::wstring strV4pStyle;
	std::wstring strEvents;

	if (regex_search(wHeader, m, scriptInfoExp))
	{
		openScriptInfo = true;
		auto scriptInfo = m[0].str();
		wHeader = wHeader.substr(m.prefix().length() + scriptInfo.length(), wHeader.size() - m.prefix().length() - scriptInfo.length());
	}

	if (regex_search(wHeader, m, v4StyleExp))
	{
		if (openScriptInfo)
		{
			strScriptInfo = m.prefix().str();
			openScriptInfo = false;
		}

		openV4pStyle = true;
		auto v4pStyle = m[0].str();

		auto v4 = std::regex_replace(v4pStyle, std::wregex(L"\\s+|\\[|\\]"), std::wstring(L""));
		std::transform(v4.begin(), v4.end(), v4.begin(), tolower);
		isASS = (v4 == L"v4+styles");

		wHeader = wHeader.substr(m.prefix().length() + v4pStyle.length(), wHeader.size() - m.prefix().length() - v4pStyle.length());
	}

	bool startEvents = false;
	if (regex_search(wHeader, m, eventsExp))
	{
		if (openScriptInfo)
		{
			strScriptInfo = m.prefix().str();
			openScriptInfo = false;
		}
		else if (openV4pStyle)
		{
			strV4pStyle = m.prefix().str();
			openV4pStyle = false;
		}

		auto events = m[0].str();
		strEvents = wHeader.substr(m.prefix().length() + events.length(), wHeader.size() - m.prefix().length() - events.length());
	}

	if (*scriptInfoProp == nullptr)
	{
		*scriptInfoProp = ref new PropertySet();
	}
	else
	{
		(*scriptInfoProp)->Clear();
	}

	if (*styleMap == nullptr)
	{
		(*styleMap) = ref new Platform::Collections::UnorderedMap<String^, Windows::Data::Json::JsonObject^>();
	}
	else
	{
		(*styleMap)->Clear();
	}

	if (eventList->size() > 0)
	{
		eventList->clear();
	}

	Windows::Foundation::Collections::IVector<String^>^ formatList = ref new Platform::Collections::Vector<String^>();

	if (strScriptInfo.length() > 2 && strScriptInfo.substr(strScriptInfo.length() - 2, 2) != L"\r\n")
	{
		strScriptInfo.append(L"\r\n");
	}
	while (regex_search(strScriptInfo, m, keyValueExp))
	{
		auto key = ref new String(m[1].str().c_str());
		auto value = ref new String(m[2].str().c_str());
		strScriptInfo = m.suffix();

		(*scriptInfoProp)->Insert(key, value);
		//OutputDebugMessage(L"key : %s, value : %s\n", key->Data(), value->Data());
	}

	if (regex_search(strV4pStyle, m, keyValueExp))
	{
		auto key = m[1].str();
		std::transform(key.begin(), key.end(), key.begin(), tolower);

		if (key == L"format")
		{
			auto value = m[2].str();
			size_t pos = 0;
			size_t offset = 0;
			std::wstring token;

			do
			{
				if ((pos = value.find(delimiter, offset)) != std::string::npos)
				{
					token = value.substr(offset, pos - offset);
				}
				else
				{
					token = value.substr(offset, value.length() - offset);
					pos = value.length();
				}
				offset = pos + delimiter.length();
				formatList->Append(ref new String(trim(token).c_str()));
			} while (value.length() > offset);
		}

		strV4pStyle = m.suffix();
		if (strV4pStyle.length() > 2 && strV4pStyle.substr(strV4pStyle.length() - 2, 2) != L"\r\n")
		{
			strV4pStyle.append(L"\r\n");
		}

		while (regex_search(strV4pStyle, m, keyValueExp))
		{
			auto key = m[1].str();
			std::transform(key.begin(), key.end(), key.begin(), tolower);
			auto keyList = ref new Platform::Collections::Vector<String^>();

			if (key == L"style")
			{
				Windows::Data::Json::JsonObject^ v4pStylePropJO = ref new Windows::Data::Json::JsonObject();
				auto value = m[2].str();

				size_t pos = 0;
				size_t offset = 0;
				std::wstring token;

				do
				{
					if ((pos = value.find(delimiter, offset)) != std::string::npos)
					{
						token = value.substr(offset, pos - offset);
					}
					else
					{
						token = value.substr(offset, value.length() - offset);
						pos = value.length();
					}
					offset = pos + delimiter.length();
					auto val = ref new String(trim(token).c_str());
					keyList->Append(val);

					if (keyList->Size <= formatList->Size)
					{
						int index = keyList->Size - 1;
						auto fKey = formatList->GetAt(index);

						if (fKey == "Name")
						{
							v4pStylePropJO->SetNamedValue(fKey, Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						//글꼴
						else if (fKey == "Fontname")
						{
							v4pStylePropJO->SetNamedValue("FontFamily", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						else if (fKey == "Fontsize")
						{
							v4pStylePropJO->SetNamedValue("FontSize", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						//글자색상
						else if (fKey == "PrimaryColour")
						{
							String^ colorCode = GetHexColorCode(val);
							if (colorCode != nullptr)
							{
								v4pStylePropJO->SetNamedValue("Foreground", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
							}
						}
						//글자색상 (충돌시 서브)
						else if (fKey == "SecondaryColour")
						{
							String^ colorCode = GetHexColorCode(val);
							if (colorCode != nullptr)
							{
								v4pStylePropJO->SetNamedValue("Foreground2", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
							}
						}
						//글자 외곽선 색상
						else if (fKey == "TertiaryColour")
						{
							String^ colorCode = GetHexColorCode(val);
							if (colorCode != nullptr)
							{
								v4pStylePropJO->SetNamedValue("Outline", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
							}
						}
						//글자 외곽선 색상
						else if (fKey == "OutlineColour")
						{
							String^ colorCode = GetHexColorCode(val);
							if (colorCode != nullptr)
							{
								v4pStylePropJO->SetNamedValue("Outline", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
							}
						}
						//글자 배경
						else if (fKey == "BackColour")
						{
							String^ colorCode = GetHexColorCode(val);
							if (colorCode != nullptr)
							{
								v4pStylePropJO->SetNamedValue("Background", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
							}
						}
						//굵게
						else if (fKey == "Bold")
						{
							if (val == L"-1")
							{
								v4pStylePropJO->SetNamedValue("FontWeight", Windows::Data::Json::JsonValue::CreateStringValue("Bold"));
							}
						}
						//기울이기
						else if (fKey == "Italic")
						{
							if (val == L"-1")
							{
								v4pStylePropJO->SetNamedValue("FontStyle", Windows::Data::Json::JsonValue::CreateStringValue("Italic"));
							}
						}
						//밑줄
						else if (fKey == "Underline")
						{
							if (val == L"-1")
							{
								v4pStylePropJO->SetNamedValue("Underline", Windows::Data::Json::JsonValue::CreateStringValue("True"));
							}
						}
						else if (fKey == "StrikeOut") {}
						else if (fKey == "ScaleX") {}
						else if (fKey == "ScaleY") {}
						else if (fKey == "Spacing") {}
						else if (fKey == "Angle") {}
						else if (fKey == "BorderStyle")
						{
							v4pStylePropJO->SetNamedValue("BorderStyle", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						else if (fKey == "Outline")
						{
							if (v4pStylePropJO->HasKey("BorderStyle") && v4pStylePropJO->GetNamedString("BorderStyle") == "1")
							{
								//Outline 굵기 처리
								std::string sot(val->Begin(), val->End());
								double outlineThickness = strtod(sot.c_str(), NULL);
								v4pStylePropJO->SetNamedValue("OutlineThickness", Windows::Data::Json::JsonValue::CreateNumberValue(outlineThickness));
							}
						}
						else if (fKey == "Shadow")
						{
							if (v4pStylePropJO->HasKey("BorderStyle") && v4pStylePropJO->GetNamedString("BorderStyle") == "1")
							{
								//Shadow 굵기 처리
								std::string sot(val->Begin(), val->End());
								double shadowThickness = strtod(sot.c_str(), NULL);
								v4pStylePropJO->SetNamedValue("ShadowThickness", Windows::Data::Json::JsonValue::CreateNumberValue(shadowThickness));
							}
						}
						//정렬
						else if (fKey == "Alignment")
						{
							//This sets how text is "justified" within the Left/Right onscreen margins, and also the vertical placing. 
							//Values may be 1=Left, 2=Centered, 3=Right. 
							//Add 4 to the value for a "Toptitle". 
							//Add 8 to the value for a "Midtitle". 
							//eg. 5 = left-justified toptitle
							//ASS is numberpad type
							if (val == L"1")
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("LB"));
							}
							else if (val == L"2")
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("CB"));
							}
							else if (val == L"3")
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("RB"));
							}
							else if ((isASS && val == L"4") || (!isASS && val == L"9"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("LC"));
							}
							else if ((isASS && val == L"5") || (!isASS && val == L"10"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("CC"));
							}
							else if ((isASS && val == L"6") || (!isASS && val == L"11"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("RC"));
							}
							else if ((isASS && val == L"7") || (!isASS && val == L"5"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("LT"));
							}
							else if ((isASS && val == L"8") || (!isASS && val == L"6"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("CT"));
							}
							else if ((isASS && val == L"9") || (!isASS && val == L"7"))
							{
								v4pStylePropJO->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue("RT"));
							}
						}
						//마진 Left
						else if (fKey == "MarginL")
						{
							v4pStylePropJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						//마진 Right
						else if (fKey == "MarginR")
						{
							v4pStylePropJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						//마진 Bottom
						else if (fKey == "MarginV")
						{
							v4pStylePropJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(val));
						}
						else if (fKey == "Encoding")
						{
							v4pStylePropJO->SetNamedValue(fKey, Windows::Data::Json::JsonValue::CreateStringValue(val));
						}

					}
				} while (value.length() > offset);

				if (v4pStylePropJO->HasKey("Name"))
				{
					//String^ name = dynamic_cast<String^>(v4pStyleProp->Lookup("Name"));
					String^ name = v4pStylePropJO->GetNamedString("Name");
					if (name != nullptr && !name->IsEmpty())
					{
						(*styleMap)->Insert(name, v4pStylePropJO);
					}
				}
			}

			strV4pStyle = m.suffix();
		}
	}

	while (regex_search(strEvents, m, keyValueExp))
	{
		auto key = m[1].str();
		std::transform(key.begin(), key.end(), key.begin(), tolower);

		if (key == L"format")
		{
			auto value = m[2].str();
			size_t pos = 0;
			size_t offset = 0;
			std::wstring token;

			do
			{
				if ((pos = value.find(delimiter, offset)) != std::string::npos)
				{
					token = value.substr(offset, pos - offset);
				}
				else
				{
					token = value.substr(offset, value.length() - offset);
					pos = value.length();
				}
				offset = pos + delimiter.length();
				(*eventList).push_back(trim(token));
			} while (value.length() > offset);
		}
		else
		{
			//마커에 담아 두어야 하는 것들... 
			//ex) Comment, Dialogue, etc.
			OutputDebugStringW(L"커멘트들 처리하자!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
		}

		strEvents = m.suffix();
	}
}

void SubtitleHelper::ApplyASSStyle(Windows::Data::Json::JsonObject^ rect,
	IMap<String^, Windows::Data::Json::JsonObject^>^ styleMap,
	std::vector<std::wstring>* eventList)
{
	static const  wregex sTagExp(L"<\\s*(.+?)\\s*>");
	static const  wregex eTagExp(L"<\\s*/\\s*(.+?)\\s*>");
	static const  wregex assCrExp(L"\\\\N|\\r\\n|\\n", regex_constants::ECMAScript | regex_constants::icase);
//	static const  wregex crExp(L"\\n", regex_constants::ECMAScript | regex_constants::icase);
//	static const  wregex assTagExp(L"\\{\\s*(.+?)\\s*\\}(.*?)");

	String^ strAss = rect->GetNamedString("Ass");
	std::wstring ass(strAss->Data());
	std::wsmatch m;

	size_t offset;
	std::wstring dialogue(L"Dialogue:");
	if ((offset = ass.find(dialogue, 0)) != std::string::npos)
	{
		offset = dialogue.size();
		std::map<std::wstring, std::wstring> assMap;
		size_t idx = 0;
		size_t size = eventList->size();

		for (size_t j = offset; j < ass.size(); j++)
		{
			if (ass.at(j) == ',')
			{
				auto key = eventList->at(idx);
				auto value = std::wstring(ass.substr(offset, j - offset));
				assMap[key] = trim(value);

				offset = j + 1;
				idx++;
			}

			if (idx + 1 == size)
			{
				auto key = eventList->at(idx);
				auto value = std::wstring(ass.substr(offset, ass.size() - offset));
				assMap[key] = trim(value);
				break;
			}
		}

		std::wstring textVal = assMap[L"Text"];
		//textVal = L"abc{\\i1}{\\u1}Shedding{\\u0} {\\b1}tears{\\b0} with them?{\\i0}test";
		//textVal = L"{\\2c&HAA69F3&\\c&HAA69F3&\\3c&H100436&\\4c&H100436&\\move(468,-28,450,625,100,1)}프리즈마\\N이리야	";

		//스타일 처리.....
		//1. 줄바꿈 치환
		//textVal = regex_replace(textVal, crExp, L"<br/>");
		textVal = regex_replace(textVal, assCrExp, L"<br/>");
		//textVal = trim(regex_replace(textVal, lfExp, L""));

		//2. 자체 스타일 오버라이드
		std::vector<std::wstring> tagList;
		std::vector<std::wstring> tagStack;
		//textVal = textVal.append(L"{\\EOL}");

		size_t bsOff = 0;
		size_t beOff = 0;
		String^ ClosedCaptionPosition = nullptr;

		while (true)
		{
			std::wstring tagCont;

			if ((bsOff = textVal.find(L"{", bsOff)) != std::string::npos)
			{
				if (bsOff > beOff)
				{
					//auto txt = trim(textVal.substr(beOff, bsOff - beOff));
					auto txt = textVal.substr(beOff, bsOff - beOff);
					if (!txt.empty())
					{
						tagList.push_back(txt);
					}
				}

				bsOff++;

				if ((beOff = textVal.find(L"}", bsOff)) != std::string::npos)
				{
					tagCont = textVal.substr(bsOff, beOff - bsOff);
					beOff++;
				}
			}

			size_t cdcOff = 0;
			if (!tagCont.empty())
			{
				if ((cdcOff = tagCont.find(L"\\", cdcOff)) != std::string::npos)
				{
					//Tag 존재
					size_t cdpOff = ++cdcOff;
					std::wstring tagStr;
					size_t attrOffset = 0;
					while (true)
					{
						if ((cdcOff = tagCont.find(L"\\", cdpOff)) != std::string::npos)
						{
							tagStr = tagCont.substr(cdpOff, cdcOff - cdpOff);
							cdpOff = ++cdcOff;
						}
						else
						{
							tagStr = tagCont.substr(cdpOff, tagCont.size() - cdpOff);
						}

						if (!tagStr.empty())
						{
							//태그 체크 검사
							if (tagStr == L"b1")
							{
								tagList.push_back(L"<b>");
							}
							else if (tagStr == L"b0")
							{
								tagList.push_back(L"</b>");
							}
							else if (tagStr == L"i1")
							{
								tagList.push_back(L"<i>");
							}
							else if (tagStr == L"i0")
							{
								tagList.push_back(L"</i>");
							}
							else if (tagStr == L"u1")
							{
								tagList.push_back(L"<u>");
							}
							else if (tagStr == L"u0")
							{
								tagList.push_back(L"</u>");
							}
							else if ((tagStr == L"fn" || tagStr == L"fs"
								|| tagStr == L"c" || tagStr == L"0c" || tagStr == L"1c") && !tagList.empty())
							{
								tagList.push_back(L"</font>");
							}
							else if (tagStr == L"a1")
							{
								ClosedCaptionPosition = "LB";
							}
							else if (tagStr == L"a2")
							{
								ClosedCaptionPosition = "CB";
							}
							else if (tagStr == L"a3")
							{
								ClosedCaptionPosition = "RB";
							}
							else if (tagStr == L"a5")
							{
								ClosedCaptionPosition = "LT";
							}
							else if (tagStr == L"a6")
							{
								ClosedCaptionPosition = "CT";
							}
							else if (tagStr == L"a7")
							{
								ClosedCaptionPosition = "RT";
							}
							else if (tagStr == L"a9")
							{
								ClosedCaptionPosition = "LC";
							}
							else if (tagStr == L"a10")
							{
								ClosedCaptionPosition = "CC";
							}
							else if (tagStr == L"a11")
							{
								ClosedCaptionPosition = "RC";
							}
							else if (tagStr == L"an1")
							{
								ClosedCaptionPosition = "LB";
							}
							else if (tagStr == L"an2")
							{
								ClosedCaptionPosition = "CB";
							}
							else if (tagStr == L"an3")
							{
								ClosedCaptionPosition = "RB";
							}
							else if (tagStr == L"an4")
							{
								ClosedCaptionPosition = "LC";
							}
							else if (tagStr == L"an5")
							{
								ClosedCaptionPosition = "CC";
							}
							else if (tagStr == L"an6")
							{
								ClosedCaptionPosition = "RC";
							}
							else if (tagStr == L"an7")
							{
								ClosedCaptionPosition = "LT";
							}
							else if (tagStr == L"an8")
							{
								ClosedCaptionPosition = "CT";
							}
							else if (tagStr == L"an9")
							{
								ClosedCaptionPosition = "RT";
							}
							else if ((attrOffset = tagStr.find(L"fn", 0)) != std::string::npos)
							{
								attrOffset = 2;
								auto fn = tagStr.substr(attrOffset, tagStr.size() - attrOffset).insert(0, L"<font face=\"").append(L"\">");
								tagList.push_back(fn);
							}
							else if ((attrOffset = tagStr.find(L"fs", 0)) != std::string::npos)
							{
								attrOffset = 2;
								std::wstring wnum;
								for (size_t k = attrOffset; k < tagStr.size(); k++)
								{
									if (tagStr.at(k) >= 48 && tagStr.at(k) <= 57)
									{
										wnum = wnum + tagStr.at(k);
									}
								}

								std::ostringstream buffer;
								std::string sfs(wnum.begin(), wnum.end());

								//ass의 폰트는 너무 큼
								double fs = strtod(sfs.c_str(), NULL) * 0.8;

								buffer << fs;
								std::string str = buffer.str();
								wnum.assign(str.begin(), str.end());

								auto fontSize = wnum.insert(0, L"<font size=\"").append(L"\">");
								tagList.push_back(fontSize);
							}
							else if (tagStr.at(tagStr.size() - 1) == '&'
								&& ((tagStr.find(L"c&H") != std::string::npos)
									|| (tagStr.find(L"0c&H") != std::string::npos)
									|| (tagStr.find(L"1c&H") != std::string::npos)))
							{
								auto ofs = tagStr.find(L"c&H") + 3;
								auto colorHexStr = tagStr.substr(ofs, tagStr.size() - ofs - 1);
								if (colorHexStr.length() < 6)
								{
									//6자리 미만의 경우 우측 0으로 패딩
									colorHexStr.insert(colorHexStr.end(), 6 - colorHexStr.length(), '0');
								}
								else if (colorHexStr.length() > 6)
								{
									colorHexStr = colorHexStr.substr(colorHexStr.length() - 6, 6);
								}

								auto bb = colorHexStr.substr(0, 2);
								auto gg = colorHexStr.substr(2, 2);
								auto rr = colorHexStr.substr(4, 2);
								std::wstring fontColor;

								fontColor.insert(0, L"<font color=\"#FF").append(rr).append(gg).append(bb).append(L"\">");
								tagList.push_back(fontColor);
							}
						}

						if (cdcOff == std::string::npos) break;
					}
				}
				else
				{
					//Tag 없음...그냥 문자열임.
					tagList.push_back(tagCont);
				}

			}
			else
			{
				//brace 없음...그냥 문자열임.
				tagCont = textVal.substr(beOff, textVal.size() - beOff);
				tagList.push_back(tagCont);
				beOff = textVal.size();
			}

			if (beOff >= textVal.size()) break;
		}

		textVal.clear();
		//태그 정리
		for (size_t j = 0; j < tagList.size(); j++)
		{
			auto tag = tagList.at(j);

			std::wsmatch m3;
			if (regex_match(tag, m3, eTagExp))
			{
				auto tagName = m3[1].str();
				std::wstring eTag;

				do
				{
					if (tagStack.size() == 0)
					{
						break;
					}

					eTag = tagStack.back();
					tagStack.pop_back();

					textVal.append(L"</").append(eTag).append(L">");

				} while (eTag != tagName);
			}
			else if (regex_match(tag, m3, sTagExp))
			{
				auto tagName = trim(m3[1].str());

				if (tagName.at(tagName.length() - 1) == '/')
				{
					//정규식으로 사용시 마지막 단어가 잘림(왜??? ㅜㅜ)
					textVal.append(L"<").append(tagName).append(L">");
				}
				else
				{
					size_t tagPos = tagName.find(L" ", 0);

					if (tagPos != std::string::npos)
					{
						tagStack.push_back(tagName.substr(0, tagPos));
					}
					textVal.append(L"<").append(tagName).append(L">");
				}
			}
			else
			{
				textVal.append(tag);
			}
		}

		//최종 처리 되지 않은 태그 처리
		while (tagStack.size() > 0)
		{
			auto eTag = tagStack.back();
			tagStack.pop_back();
			textVal.append(L"</").append(eTag).append(L">");
		}

		//V4 스타일 적용
		//Layer or Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
		//Marked 0 : not shown
		std::wstring styleVal = assMap[L"Style"];
		std::wstring nameVal = assMap[L"Name"];
		std::wstring marginLVal = assMap[L"MarginL"];
		std::wstring marginRVal = assMap[L"MarginR"];
		std::wstring marginVVal = assMap[L"MarginV"];
		std::wstring effectVal = assMap[L"Effect"];


		String^ styleKeyStr = ref new String(styleVal.c_str());
		if (styleMap->HasKey(styleKeyStr))
		{
			auto stylePropStr = styleMap->Lookup(styleKeyStr)->Stringify();
			Windows::Data::Json::JsonObject^ propJO = nullptr;
			//스타일 프로퍼티 복사
			if (Windows::Data::Json::JsonObject::TryParse(stylePropStr, &propJO))
			{
				//마진 Left
				if (!(marginLVal == L"0" || marginLVal == L"" || marginLVal == L"0000"))
				{
					propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(marginLVal.c_str())));
				}

				//마진 Right
				if (!(marginRVal == L"0" || marginRVal == L"" || marginRVal == L"0000"))
				{
					propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(marginRVal.c_str())));
				}

				//마진 Bottom
				if (!(marginVVal == L"0" || marginVVal == L"" || marginVVal == L"0000"))
				{
					propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(marginVVal.c_str())));
				}

				rect->SetNamedValue("Style", propJO);
			}
		}

		if (ClosedCaptionPosition != nullptr)
		{
			if (!rect->HasKey("Style"))
			{
				rect->SetNamedValue("Style", ref new Windows::Data::Json::JsonObject());
			}
			rect->GetNamedObject("Style")->SetNamedValue("ClosedCaptionPosition", Windows::Data::Json::JsonValue::CreateStringValue(ClosedCaptionPosition));
		}

		rect->SetNamedValue("Ass", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(textVal.c_str())));
	}
}

void SubtitleHelper::LoadSAMIHeader(String^ header,
	String^ *title,
	PropertySet^ *commonStyleProp,
	IMap<String^, Windows::Data::Json::JsonObject^>^ *styleMap,
	IVector<SubtitleLanguage^>^ *languageList)
{
	const wregex wlinePatern(L"\r*\n", regex_constants::ECMAScript | regex_constants::icase);
	const wregex headerPattern(L"(?:.*)<[\\s]*Title[\\s]*>(.*)<[\\s]*/[\\s]*Title[\\s]*>(?:.*)", regex_constants::ECMAScript | regex_constants::icase);
	const wregex stylePattern(L"(?:.*)<[\\s]*STYLE[\\s]+TYPE[\\s]*=[\\s]*[\"|']text/css[\"|']>(.*)<[\\s]*/[\\s]*STYLE[\\s]*>(?:.*)", regex_constants::ECMAScript | regex_constants::icase);
	const wregex pPattern(L"(?:.*)P[\\s]+\\{[\\s]*(.*?)\\}[\\s]*", regex_constants::ECMAScript | regex_constants::icase);
	const wregex langPattern(L"\\.(.+?)[\\s]+\\{(.*?)\\}", regex_constants::ECMAScript | regex_constants::icase);
	const wregex styleIdPattern(L"#(.+?)[\\s]+\\{(.*?)\\}", regex_constants::ECMAScript | regex_constants::icase);
	const wregex propertyPattern(L"[\\s]*(.+?)[\\s]*:[\\s]*(.*?)[\\s]*;", regex_constants::ECMAScript | regex_constants::icase);
	
	if (*languageList == nullptr)
	{
		*languageList = ref new Platform::Collections::Vector<SubtitleLanguage^>();
	}
	else
	{
		(*languageList)->Clear();
	}

	if (*commonStyleProp == nullptr)
	{
		*commonStyleProp = ref new PropertySet();
	}
	else
	{
		(*commonStyleProp)->Clear();
	}

	if (*styleMap == nullptr)
	{
		*styleMap = ref new Platform::Collections::Map<String^, Windows::Data::Json::JsonObject^>();
	}
	else
	{
		(*styleMap)->Clear();
	}

	std::wstring wheader(header->Data());
	wheader = regex_replace(wheader, wlinePatern, L"");

	wsmatch m;
	if (regex_search(wheader, m, headerPattern))
	{
		//타이틀 저장
		*title = ref new String(m[1].str().c_str());
	}

	if (regex_search(wheader, m, stylePattern))
	{
		std::wstring pStr(m[1].str());
		if (regex_search(pStr, m, pPattern))
		{
			std::wstring properties(m[1].str());
			wsmatch m2;

			while (regex_search(properties, m2, propertyPattern))
			{
				auto name = ref new String(m2[1].str().c_str());
				//auto value = ref new String(m2[2].str().c_str());

				std::wstringstream ss(m2[2].str());
				std::wstring token;
				std::wstring result;

				while (std::getline(ss, token, L',')) {
					token.erase(token.begin(), std::find_if(token.begin(), token.end(), std::bind1st(std::not_equal_to<char>(), ' ')));
					token.erase(std::find_if(token.rbegin(), token.rend(), std::bind1st(std::not_equal_to<char>(), ' ')).base(), token.end());
					token.erase(token.begin(), std::find_if(token.begin(), token.end(), std::bind1st(std::not_equal_to<char>(), '\'')));
					token.erase(std::find_if(token.rbegin(), token.rend(), std::bind1st(std::not_equal_to<char>(), '\'')).base(), token.end());
					token.erase(token.begin(), std::find_if(token.begin(), token.end(), std::bind1st(std::not_equal_to<char>(), '"')));
					token.erase(std::find_if(token.rbegin(), token.rend(), std::bind1st(std::not_equal_to<char>(), '"')).base(), token.end());
					if (result.length() > 0)
					{
						result += L",";
					}
					result += token;
				}

				auto value = ref new String(result.c_str());
				(*commonStyleProp)->Insert(name, value);

				properties = m2.suffix();
			}
						
			std::wstring langStr(m.suffix());
			SubtitleLanguage^ subLang = nullptr;
			while (regex_search(langStr, m, langPattern))
			{
				//language
				subLang = ref new SubtitleLanguage();
				(*languageList)->Append(subLang);
				
				subLang->Code = ref new String(m[1].str().c_str());
				properties = std::wstring(m[2].str());

				while (regex_search(properties, m2, propertyPattern))
				{
					auto key = std::wstring(m2[1].str());
					auto value = ref new String(m2[2].str().c_str());

					std::transform(key.begin(), key.end(), key.begin(), tolower);

					if (key == L"name")
					{
						subLang->Name = value;
					}
					else if (key == L"lang")
					{
						subLang->Lang = value;
					}
					else
					{
						subLang->Properties->Insert(ref new String(key.c_str()), value);
					}

					properties = m2.suffix();
				}

				langStr = m.suffix();
			}

			//간혹 lang, name등이 존재하지 않는 경우도 있음. (디폴트 용으로 하나 등록)
			if ((*languageList)->Size == 0)
			{
				//language
				SubtitleLanguage^ subLang = ref new SubtitleLanguage();
				(*languageList)->Append(subLang);
			}

			//styleId #
			while (regex_search(langStr, m, styleIdPattern))
			{
				wsmatch m2;
				auto id = ref new String(m[1].str().c_str());
				properties = std::wstring(m[2].str());

				auto prop = ref new Windows::Data::Json::JsonObject();
				(*styleMap)->Insert(id, prop);

				while (regex_search(properties, m2, propertyPattern))
				{
					auto name = ref new String(m2[1].str().c_str());
					auto value = ref new String(m2[2].str().c_str());
					//prop->Insert(name, value);
					prop->SetNamedValue(name, Windows::Data::Json::JsonValue::CreateStringValue(value));

					properties = m2.suffix();
				}

				langStr = m.suffix();
			}
		}
	}
}

void SubtitleHelper::ApplySAMIStyle(Windows::Data::Json::JsonObject^ pkt,
	PropertySet^ globalStyleProp,
	IMap<String^, Windows::Data::Json::JsonObject^>^ styleMap,
	IVector<SubtitleLanguage^>^ languageList)
{
	int numRects = (int)pkt->GetNamedNumber("NumRects");
	auto testVal = pkt->Stringify();
	auto rects = pkt->GetNamedArray("Rects");

	for (int i = 0; i < numRects; i++)
	{
		auto rect = rects->GetObjectAt(i);

		//std::wstring strAss(rect->GetNamedString("Ass")->Data());
		String^ strLang = rect->GetNamedString("Lang");
		String^ strId = rect->GetNamedString("Id");

		/*pkt->GetNamedArray("Rects")->GetObjectAt(i)->SetNamedValue("Ass",
		Windows::Data::Json::JsonValue::CreateStringValue("<div><div style=\"margin-left:50;margin-right:200;font-weight:900\"><font style=\"color:#FF00FF;font-family:궁서체;\">가나다라마바사ㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁㅁ</div></div>"));*/

		//1. P 스타일
		//p(공통) 스타일 속성 적용
		Windows::Data::Json::JsonObject^ propJO = ref new Windows::Data::Json::JsonObject();
		auto iter2 = globalStyleProp->First();
		while (iter2->HasCurrent)
		{
			auto prop = iter2->Current;
			auto key = prop->Key;
			auto val = dynamic_cast<String^>(prop->Value);

			//여기서...
			SetStyleProperty(key, val, propJO);
			iter2->MoveNext();
		}

		//2. Class (랭귀지) 스타일 속성 적용
		IMap<String^, String^>^ props = nullptr;
		for (uint32 i = 0; i < languageList->Size; i++)
		{
			if (languageList->GetAt(i)->Code == strLang)
			{
				props = languageList->GetAt(i)->Properties;
				break;
			}
		}

		if (props != nullptr)
		{
			//std::wstring ass(strAss->Data());
			auto iter = props->First();
			while (iter->HasCurrent)
			{
				auto prop = iter->Current;
				auto key = prop->Key;
				auto val = prop->Value;
				//클래스(랭귀지)속성 오버라이드
				SetStyleProperty(key, val, propJO);
				iter->MoveNext();
			}
		}

		//3. ID 스타일 속성 적용
		if (styleMap->HasKey(strId))
		{
			auto propSet = styleMap->Lookup(strId);
			auto iter2 = propSet->First();
			while (iter2->HasCurrent)
			{
				auto prop = iter2->Current;
				auto key = prop->Key;
				auto val = prop->Value;
				//ID 스타일 속성 덮어쓰기
				SetStyleProperty(key, val->GetString(), propJO);
				iter2->MoveNext();
			}
		}

		rect->SetNamedValue("Style", propJO);
	}
}

void SubtitleHelper::SetStyleProperty(String^ key, String^ val, Windows::Data::Json::JsonObject^ propJO)
{
	const wregex px4Pattern(L"\\s*(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s*", regex_constants::ECMAScript | regex_constants::icase);
	const wregex px3Pattern(L"\\s*(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s*", regex_constants::ECMAScript | regex_constants::icase);
	const wregex px2Pattern(L"\\s*(\\d+?)\\s*[px]*\\s+(\\d+?)\\s*[px]*\\s*", regex_constants::ECMAScript | regex_constants::icase);
	const wregex px1Pattern(L"\\s*(\\d+?)\\s*[px]*\\s*", regex_constants::ECMAScript | regex_constants::icase);

	if (key == "font-family")
	{
		propJO->SetNamedValue("FontFamily", Windows::Data::Json::JsonValue::CreateStringValue(val));
	}
	else if (key == "font-size")
	{
		propJO->SetNamedValue("FontSize", Windows::Data::Json::JsonValue::CreateStringValue(val));
	}
	else if (key == "font-weight")
	{
		String^ fontWeight = "Medium";
		if (val == "thin" || val == "100")
		{
			fontWeight = "Thin";
		}
		else if (val == "extralight" || val == "200")
		{
			fontWeight = "ExtraLight";
		}
		else if (val == "light" || val == "300")
		{
			fontWeight = "Light";
		}
		else if (val == "lighter" || val == "400")
		{
			fontWeight = "Normal";
		}
		else if (val == "normal" || val == "400")
		{
			fontWeight = "Normal";
		}
		else if (val == "medium" || val == "500")
		{
			fontWeight = "Medium";
		}
		else if (val == "bolder" || val == "semibold" || val == "600")
		{
			fontWeight = "SemiBold";
		}
		else if (val == "bold" || val == "700")
		{
			fontWeight = "Bold";
		}
		else if (val == "extrabold" || val == "800")
		{
			fontWeight = "ExtraBold";
		}
		else if (val == "black" || val == "900")
		{
			fontWeight = "Black";
		}
		else if (val == "extrablack" || val == "950")
		{
			fontWeight = "ExtraBlack";
		}

		if (fontWeight != "Normal")
		{
			propJO->SetNamedValue("FontWeight", Windows::Data::Json::JsonValue::CreateStringValue(fontWeight));
		}
	}
	else if (key == "font-style")
	{
		if (val == "italic")
		{
			propJO->SetNamedValue("FontStyle", Windows::Data::Json::JsonValue::CreateStringValue("Italic"));
		}
		else if (val == "oblique")
		{
			propJO->SetNamedValue("FontStyle", Windows::Data::Json::JsonValue::CreateStringValue("Oblique"));
		}
		else
		{
			propJO->SetNamedValue("FontStyle", Windows::Data::Json::JsonValue::CreateStringValue("Normal"));
		}
	}
	else if (key == "color")
	{
		String^ colorCode = GetHexColorCode(val);
		if (colorCode != nullptr)
		{
			propJO->SetNamedValue("Foreground", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
		}
	}
	else if (key == "background-color")
	{
		String^ colorCode = GetHexColorCode(val);
		if (colorCode != nullptr)
		{
			propJO->SetNamedValue("Background", Windows::Data::Json::JsonValue::CreateStringValue(colorCode));
		}
	}
	else if (key == "text-align")
	{
		String^ textAlignment = "Center";
		if (val == "left")
		{
			textAlignment = "Left";
		}
		else if (val == "right")
		{
			textAlignment = "Right";
		}
		else if (val == "center")
		{
			textAlignment = "Center";
		}
		else if (val == "justify")
		{
			textAlignment = "Justify";
		}
		else
		{
			textAlignment = "DetectFromContent";
		}
		propJO->SetNamedValue("TextAlignment", Windows::Data::Json::JsonValue::CreateStringValue(textAlignment));
	}
	else if (key == "margin-top")
	{
		std::wstring margin(val->Data());
		wsmatch m;
		if (regex_match(margin, m, px1Pattern))
		{
			propJO->SetNamedValue("MarginTop", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
		}
	}
	else if (key == "margin-right")
	{
		std::wstring margin(val->Data());
		wsmatch m;
		if (regex_match(margin, m, px1Pattern))
		{
			propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
		}
	}
	else if (key == "margin-bottom")
	{
		std::wstring margin(val->Data());
		wsmatch m;
		if (regex_match(margin, m, px1Pattern))
		{
			propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
		}
	}
	else if (key == "margin-left")
	{
		std::wstring margin(val->Data());
		wsmatch m;
		if (regex_match(margin, m, px1Pattern))
		{
			propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
		}
	}
	else if (key == "margin")
	{
		std::wstring margin(val->Data());

		wsmatch m;
		if (regex_match(margin, m, px4Pattern))
		{
			propJO->SetNamedValue("MarginTop", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[2].str().c_str())));
			propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[3].str().c_str())));
			propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[4].str().c_str())));
		}
		else if (regex_match(margin, m, px3Pattern))
		{
			propJO->SetNamedValue("MarginTop", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[2].str().c_str())));
			propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[3].str().c_str())));
			propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[2].str().c_str())));
		}
		else if (regex_match(margin, m, px2Pattern))
		{
			propJO->SetNamedValue("MarginTop", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[2].str().c_str())));
			propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[2].str().c_str())));
		}
		else if (regex_match(margin, m, px1Pattern))
		{
			propJO->SetNamedValue("MarginTop", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginRight", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginBottom", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
			propJO->SetNamedValue("MarginLeft", Windows::Data::Json::JsonValue::CreateStringValue(ref new String(m[1].str().c_str())));
		}
	}
}

String^ SubtitleHelper::GetHexColorCode(String^ colorValue)
{
	const wregex colorPatern(L"#([0-9|a-f|A-F]{1,2}?)([0-9|a-f|A-F]{1,2}?)([0-9|a-f|A-F]{1,2}?)", regex_constants::ECMAScript | regex_constants::icase);
	const wregex uColorPatern(L"\\s*(\\d+?)\\s*", regex_constants::ECMAScript | regex_constants::icase);
	const wregex rgbPatern(L"RGB\\s*\\(\\s*(\\d+?)\\s*\\,*\\s*(\\d+?)\\s*\\,*\\s*(\\d+?)\\s*\\)", regex_constants::ECMAScript | regex_constants::icase);

	std::wstring wr;
	std::wstring wg;
	std::wstring wb;
	std::wstring wa;
	wsmatch m;

	std::wstring colorCode(L"#");
	std::wstring orgColor(colorValue->Data());
	//테스트
	//orgColor = L"RGB (255 ,255,0)";
	//orgColor = L"#fa0a48";
	//orgColor = L"#f0a";
	//orgColor = L"65535";

	//&h로 시작 (SSA/ASS)
	if (orgColor.at(0) == '&' && (orgColor.at(1) == 'h' || orgColor.at(1) == 'H'))
	{
		orgColor = orgColor.substr(2, orgColor.length() - 2);
		size_t max = 8;
		if (orgColor.length() < max)
		{
			orgColor.insert(orgColor.begin(), max - orgColor.length(), '0');
		}
		wa = orgColor.substr(0, 2);
		wb = orgColor.substr(2, 2);
		wg = orgColor.substr(4, 2);
		wr = orgColor.substr(6, 2);

		if (wa == L"00")
		{
			wa = L"FF";
		}
		else
		{
			std::string sa(wa.begin(), wa.end());
			long lColor = 255 - strtol(sa.c_str(), NULL, 16);
			char hex[3];
			snprintf(hex, 3, "%lx", lColor);

			sa = hex;
			wa.assign(sa.begin(), sa.end());
		}

		colorCode.append(wa).append(wr).append(wg).append(wb);
	}
	//#으로 시작
	else if (regex_match(orgColor, m, colorPatern))
	{
		wr = m[1].str();
		wg = m[2].str();
		wb = m[3].str();
		wa = L"FF";

		wr.insert(wr.end(), 2 - wr.length(), wr.at(0));
		wg.insert(wg.end(), 2 - wg.length(), wg.at(0));
		wb.insert(wb.end(), 2 - wb.length(), wb.at(0));

		colorCode.append(wa).append(wr).append(wg).append(wb);
	}
	//RGB(0,0,0)패턴
	else if (regex_match(orgColor, m, rgbPatern))
	{
		auto rc = _wtoi64(m[1].str().c_str());
		auto gc = _wtoi64(m[2].str().c_str());
		auto bc = _wtoi64(m[3].str().c_str());

		wchar_t rr[3];
		wchar_t gg[3];
		wchar_t bb[3];

		_i64tow_s(rc, rr, 3, 16);
		_i64tow_s(gc, gg, 3, 16);
		_i64tow_s(bc, bb, 3, 16);

		wr = std::wstring(rr);
		wg = std::wstring(gg);
		wb = std::wstring(bb);
		wa = L"FF";

		wr.insert(wr.begin(), 2 - wr.length(), '0');
		wg.insert(wg.begin(), 2 - wg.length(), '0');
		wb.insert(wb.begin(), 2 - wb.length(), '0');

		colorCode.append(wa).append(wr).append(wg).append(wb);
	}
	//숫자형 
	else if (regex_match(orgColor, m, uColorPatern))
	{
		auto nColor = _wtoi64(m[1].str().c_str());
		wchar_t rr[3];
		wchar_t gg[3];
		wchar_t bb[3];
		wchar_t aa[3];

		_i64tow_s((nColor)& 0xFF, rr, 3, 16);
		_i64tow_s((nColor >> 8) & 0xFF, gg, 3, 16);
		_i64tow_s((nColor >> 16) & 0xFF, bb, 3, 16);
		_i64tow_s((nColor >> 24) & 0xFF, aa, 3, 16);

		wr = std::wstring(rr);
		wg = std::wstring(gg);
		wb = std::wstring(bb);
		wa = std::wstring(aa);

		wr.insert(wr.begin(), 2 - wr.length(), '0');
		wg.insert(wg.begin(), 2 - wg.length(), '0');
		wb.insert(wb.begin(), 2 - wb.length(), '0');
		wa.insert(wa.begin(), 2 - wa.length(), '0');

		if (wa == L"00")
		{
			wa = L"FF";
		}

		colorCode.append(wa).append(wr).append(wg).append(wb);
	}
	//컬러명
	else
	{
		const std::string webColorNames[] =
		{
			"greenpeas", "navajowhite", "beetlegreen", "scarlet", "lipstickpink", "pigpink", "olive", "sunyellow", "saddlebrown", "lightblue",
			"forestgreen", "coral", "watermelonpink", "wiseriapurple", "black", "ashgray", "seashell", "thistle", "lemonchiffon", "gunmetal",
			"blueberryblue", "blackeel", "yellowgreen", "teal", "carbongray", "pinkdaisy", "lavenderblush", "rubberduckyyellow", "purpledaffodil", "dragongreen",
			"salmon", "sepia", "mediumslateblue", "silkblue", "linen", "mediumorchid", "nebulagreen", "lightyellow", "lavender", "harvestgold",
			"deeppeach", "dodgerblue", "maroon", "chestnutred", "papayawhip", "mediumspringgreen", "greenyellow", "purple", "purpleflower", "bluekoi",
			"chocolate", "saffron", "red", "orchid", "azure", "sandybrown", "mediumseagreen", "coffee", "emeraldgreen", "limegreen",
			"cyanopaque", "oldlace", "graywolf", "mustard", "rust", "paleturquoise", "oil", "powderblue", "maroon", "schoolbusyellow",
			"palevioletred", "azure", "chillipepper", "tulippink", "chocolate", "gray", "midnightblue", "greenishblue", "fallleafbrown", "darkolivegreen",
			"darkgoldenrod", "palegreen", "stoplightgogreen", "khakirose", "grapefruit", "rubyred", "plumpurple", "darkseagreen", "burgundy", "greenapple",
			"zombiegreen", "darkorchid", "beige", "gingerbrown", "palebluelily", "avocadogreen", "teagreen", "moccasin", "pistachiogreen", "gray",
			"tanbrown", "khaki", "slategray", "mintcream", "mistyrose", "crystalblue", "powderblue", "orangered", "saladgreen", "earthblue",
			"brown", "tomato", "bluehosta", "constructionconeorange", "deepskyblue", "bluediamond", "moccasin", "lightskyblue", "lightpink", "robineggblue",
			"bronze", "cottoncandy", "indigo", "darkviolet", "denimblue", "darkred", "cornyellow", "darkorchid", "darkmagenta", "heliotropepurple",
			"burntpink", "graygoose", "cantaloupe", "northernlightsblue", "darkcyan", "vampiregray", "blueribbon", "butterflyblue", "purplemimosa", "lightgreen",
			"camouflagegreen", "sedona", "lightgoldenrodyellow", "tyrianpurple", "blueviolet", "water", "cornsilk", "blueeyes", "puce", "springgreen",
			"greenonion", "roguepink", "peachpuff", "limegreen", "platinum", "mediumvioletred", "slategray", "bluelagoon", "green", "burlywood",
			"bluedress", "darkslateblue", "antiquewhite", "cyan", "seaturtlegreen", "valentinered", "slimegreen", "sangria", "tronblue", "mediumforestgreen",
			"purpleiris", "blackcat", "honeydew", "slateblue", "mediumpurple", "seablue", "brass", "neonpink", "steelblue", "mistyrose",
			"lightslategray", "greensnake", "rose", "lightcoral", "peach", "pinkrose", "sandstone", "wood", "taupe", "teal",
			"aztechpurple", "brightgold", "green", "lovelypurple", "lilac", "jetgray", "seagreen", "coral", "orangesalmon", "darkseagreen",
			"black", "reddirt", "mangoorange", "goldenrod", "ferrarired", "darksalmon", "aquamarine", "redfox", "macaroniandcheese", "seaweedgreen",
			"tiffanyblue", "electricblue", "cadillacpink", "sunriseorange", "algaegreen", "dullpurple", "mintgreen", "palegoldenrod", "lightcoral", "battleshipgray",
			"lightsteelblue", "silver", "bluezircon", "ferngreen", "periwinkle", "purpleamethyst", "blue", "darkturquoise", "violet", "sandybrown",
			"navy", "crocuspurple", "charcoal", "rosyfinch", "hotpink", "blueivy", "lightslateblue", "amethyst", "khaki", "copper",
			"yellow", "forestgreen", "darkgoldenrod", "mediumturquoise", "snow", "gold", "hotpink", "halloweenorange", "armybrown", "beer",
			"ghostwhite", "carnationpink", "cookiebrown", "lightslate", "brightneonpink", "lavared", "oceanblue", "cobaltblue", "aliengreen", "palevioletred",
			"mocha", "sienna", "cinnamon", "peru", "lightblue", "darkviolet", "navyblue", "flamingopink", "lightsteelblue", "gold",
			"purplemonster", "darkgray", "fireenginered", "columbiablue", "yellow", "papayaorange", "lightsalmon", "violetred", "cadetblue", "greenthumb",
			"rosybrown", "darkblue", "aliceblue", "pumpkinorange", "cream", "lightaquamarine", "grape", "mauve", "orange", "jadegreen",
			"lawngreen", "mediumaquamarine", "skyblue", "blushred", "white", "blushpink", "beige", "mediumspringgreen", "lavender", "gainsboro",
			"babyblue", "floralwhite", "bisque", "metallicsilver", "jeansblue", "plumvelvet", "blueorchid", "darkforrestgreen", "brownbear", "coralblue",
			"eggplant", "clovergreen", "lightseagreen", "thistle", "lightslategray", "lightskyblue", "ivory", "basketballorange", "bluegray", "deeppink",
			"red", "purpledragon", "lapisblue", "dimgray", "darkorange", "shockingorange", "darkslategrey", "royalblue", "indianred", "beanred",
			"yellowgreen", "cornflowerblue", "firebrick", "blanchedalmond", "vanilla", "iguanagreen", "darkkhaki", "grayishturquoise", "dimorphothecamagenta", "pink",
			"lightgrey", "mistblue", "sapphireblue", "graycloud", "pinkcupcake", "marbleblue", "mediumvioletred", "plum", "hummingbirdgreen", "crimson",
			"turquoise", "aliceblue", "mediumturquoise", "lavenderpinocchio", "purple", "aqua", "darkslateblue", "pastelblue", "tan", "burlywood",
			"pinklemonade", "cornsilk", "camelbrown", "jellyfish", "desertsand", "velvetmaroon", "greenyellow", "rosybrown", "froggreen", "royalblue",
			"plumpie", "chartreuse", "skyblue", "blanchedalmond", "firebrick", "pink", "chartreuse", "pinkbow", "lovered", "midnightblue",
			"purplejam", "celeste", "cloudygray", "cornflowerblue", "bashfulpink", "lime", "oakbrown", "bluejay", "goldenbrown", "lightseagreen",
			"darkorange", "lightjade", "magenta", "pinkbubblegum", "goldenrod", "fuchsia", "mediumaquamarine", "blueangel", "purplehaze", "caramel",
			"violapurple", "lightcyan", "turquoise", "sienna", "blossompink", "deepskyblue", "cyanoraqua", "junglegreen", "darksalmon", "purplesagebush",
			"midnight", "milkwhite", "magenta", "jasminepurple", "granite", "brownsugar", "beeyellow", "seagreen", "mediumorchid", "lightpink",
			"mahogany", "mediumseagreen", "slateblue", "bluelotus", "mascawbluegreen", "darkgreen", "blackcow", "smokeygray", "darkslategray", "crimson",
			"lemonchiffon", "seashell", "cranberry", "redwine", "tangerine", "pearl", "parchment", "dollarbillgreen", "iceberg", "graydolphin",
			"deeppink", "aquamarine", "bluewhale", "bloodred", "sand", "venomgreen", "mediumpurple", "kellygreen", "lightsalmon", "darkturquoise",
			"whitesmoke", "pinegreen", "lawngreen", "wheat", "olivedrab", "tigerorange", "cherryred", "springgreen", "shamrockgreen", "mediumblue",
			"lightcyan", "white", "hazelgreen", "steelblue", "antiquewhite", "dodgerblue"
		};

		const  std::string webColorCodes[] =
		{
			"#ff89c35c", "#ffffdead", "#ff4c787e", "#ffff2400", "#ffc48793", "#fffdd7e4", "#ff808000", "#ffffe87c", "#ff8b4513", "#ffadd8e6",
			"#ff228b22", "#ffff7f50", "#fffc6c85", "#ffc6aec7", "#ff000000", "#ff666362", "#fffff5ee", "#ffd8bfd8", "#fffff8c6", "#ff2c3539",
			"#ff0041c2", "#ff463e3f", "#ff9acd32", "#ff008080", "#ff625d5d", "#ffe799a3", "#fffff0f5", "#ffffd801", "#ffb041ff", "#ff6afb92",
			"#fffa8072", "#ff7f462c", "#ff7b68ee", "#ff488ac7", "#fffaf0e6", "#ffba55d3", "#ff59e817", "#ffffffe0", "#ffe3e4fa", "#ffede275",
			"#ffffcba4", "#ff1e90ff", "#ff810541", "#ffc34a2c", "#ffffefd5", "#ff348017", "#ffb1fb17", "#ff800080", "#ffa74ac7", "#ff659ec7",
			"#ffd2691e", "#fffbb917", "#ffff0000", "#ffda70d6", "#fff0ffff", "#ffee9a4d", "#ff3cb371", "#ff6f4e37", "#ff5ffb17", "#ff41a317",
			"#ff92c7c7", "#fffdf5e6", "#ff504a4b", "#ffffdb58", "#ffc36241", "#ffafeeee", "#ff3b3131", "#ffb0e0e6", "#ff800000", "#ffe8a317",
			"#ffdb7093", "#fff0ffff", "#ffc11b17", "#ffc25a7c", "#ffc85a17", "#ff736f6e", "#ff191970", "#ff307d7e", "#ffc8b560", "#ff556b2f",
			"#ffb8860b", "#ff98fb98", "#ff57e964", "#ffc5908e", "#ffdc381f", "#fff62217", "#ff583759", "#ff8fbc8f", "#ff8c001a", "#ff4cc417",
			"#ff54c571", "#ff9932cc", "#fff5f5dc", "#ffc9be62", "#ffcfecec", "#ffb2c248", "#ffccfb5d", "#ff827839", "#ff9dc209", "#ff808080",
			"#ffece5b6", "#fff0e68c", "#ff708090", "#fff5fffa", "#ffffe4e1", "#ff5cb3ff", "#ffc6deff", "#ffff4500", "#ffa1c935", "#ff0000a0",
			"#ffa52a2a", "#ffff6347", "#ff77bfc7", "#fff87431", "#ff3bb9ff", "#ff4ee2ec", "#ffffe4b5", "#ff82cafa", "#fffaafba", "#ffbdedff",
			"#ffcd7f32", "#fffcdfff", "#ff4b0082", "#ff9400d3", "#ff79baec", "#ff8b0000", "#fffff380", "#ff7d1b7e", "#ff8b008b", "#ffd462ff",
			"#ffc12267", "#ffd1d0ce", "#ffffa62f", "#ff78c7c7", "#ff008b8b", "#ff565051", "#ff306eff", "#ff38acec", "#ff9e7bff", "#ff90ee90",
			"#ff78866b", "#ffcc6600", "#fffafad2", "#ffc45aec", "#ff8a2be2", "#ffebf4fa", "#fffff8dc", "#ff1569c7", "#ff7f5a58", "#ff4aa02c",
			"#ff6aa121", "#ffc12869", "#ffffdab9", "#ff32cd32", "#ffe5e4e2", "#ffc71585", "#ff657383", "#ff8eebec", "#ff008000", "#ffdeb887",
			"#ff157dec", "#ff483d8b", "#fffaebd7", "#ff00ffff", "#ff438d80", "#ffe55451", "#ffbce954", "#ff7e3817", "#ff7dfdfe", "#ff347235",
			"#ff571b7e", "#ff413839", "#fff0fff0", "#ff737ca1", "#ff9370db", "#ffc2dfff", "#ffb5a642", "#fff535aa", "#ff4863a0", "#fffbbbb9",
			"#ff778899", "#ff6cbb3c", "#ffe8adaa", "#fff08080", "#ffffe5b4", "#ffe7a1b0", "#ff786d5f", "#ff966f33", "#ff483c32", "#ff008080",
			"#ff893bff", "#fffdd017", "#ff00ff00", "#ff7f38ec", "#ffc8a2c8", "#ff616d7e", "#ff4e8975", "#ffff7f50", "#ffc47451", "#ff8bb381",
			"#ff000000", "#ff7f5217", "#ffff8040", "#ffdaa520", "#fff70d1a", "#ffe9967a", "#ff7fffd4", "#ffc35817", "#fff2bb66", "#ff437c17",
			"#ff81d8d0", "#ff9afeff", "#ffe38aae", "#ffe67451", "#ff64e986", "#ff7f525d", "#ff98ff98", "#ffeee8aa", "#ffe77471", "#ff848482",
			"#ff728fce", "#ffc0c0c0", "#ff57feff", "#ff667c26", "#ffe9cfec", "#ff6c2dc7", "#ff0000ff", "#ff00ced1", "#ffee82ee", "#fff4a460",
			"#ff000080", "#ff9172ec", "#ff34282c", "#ff7f4e52", "#ffff69b4", "#ff3090c7", "#ff736aff", "#ff9966cc", "#ffada96e", "#ffb87333",
			"#ffffff00", "#ff4e9258", "#ffaf7817", "#ff48cccd", "#fffffafa", "#ffd4a017", "#fff660ab", "#ffe66c2c", "#ff827b60", "#fffbb117",
			"#fff8f8ff", "#fff778a1", "#ffc7a317", "#ffccffff", "#fff433ff", "#ffe42217", "#ff2b65ec", "#ff0020c2", "#ff6cc417", "#ffd16587",
			"#ff493d26", "#ffa0522d", "#ffc58917", "#ffcd853f", "#ffaddfff", "#ff842dce", "#ff000080", "#fff9a7b0", "#ffb0c4de", "#ffffd700",
			"#ff461b7e", "#ffa9a9a9", "#fff62817", "#ff87afc7", "#ffffff00", "#ffe56717", "#ffffa07a", "#fff6358a", "#ff5f9ea0", "#ffb5eaaa",
			"#ffb38481", "#ff00008b", "#fff0f8ff", "#fff87217", "#ffffffcc", "#ff93ffe8", "#ff5e5a80", "#ffe0b0ff", "#ffffa500", "#ff5efb6e",
			"#ff7cfc00", "#ff348781", "#ff87ceeb", "#ffe56e94", "#ffffffff", "#ffe6a9ec", "#fff5f5dc", "#ff00fa9a", "#ffe6e6fa", "#ffdcdcdc",
			"#ff95b9c7", "#fffffaf0", "#ffffe4c4", "#ffbcc6cc", "#ffa0cfec", "#ff7d0552", "#ff1f45fc", "#ff254117", "#ff835c3b", "#ffafdcec",
			"#ff614051", "#ff3ea055", "#ff20b2aa", "#ffd2b9d3", "#ff6d7b8d", "#ff87cefa", "#fffffff0", "#fff88158", "#ff98afc7", "#fff52887",
			"#ffff0000", "#ffc38ec7", "#ff15317e", "#ff696969", "#ffff8c00", "#ffe55b3c", "#ff25383c", "#ff4169e1", "#ffcd5c5c", "#fff75d59",
			"#ff52d017", "#ff151b8d", "#ff800517", "#ffffebcd", "#fff3e5ab", "#ff9cb071", "#ffbdb76b", "#ff5e7d7e", "#ffe3319d", "#ffffc0cb",
			"#ffd3d3d3", "#ff646d7e", "#ff2554c7", "#ffb6b6b4", "#ffe45e9d", "#ff566d7e", "#ffca226b", "#ffdda0dd", "#ff7fe817", "#ffe238ec",
			"#ff40e0d0", "#fff0f8ff", "#ff48d1cc", "#ffebdde2", "#ff8e35ef", "#ff00ffff", "#ff2b3856", "#ffb4cfec", "#ffd2b48c", "#ffdeb887",
			"#ffe4287c", "#fffff8dc", "#ffc19a6b", "#ff46c7c7", "#ffedc9af", "#ff7e354d", "#ffadff2f", "#ffbc8f8f", "#ff99c68e", "#ff2b60de",
			"#ff7d0541", "#ff7fff00", "#ff6698ff", "#ffffebcd", "#ffb22222", "#fffaafbe", "#ff8afb17", "#ffc48189", "#ffe41b17", "#ff151b54",
			"#ff6a287e", "#ff50ebec", "#ff6d6968", "#ff6495ed", "#ffc25283", "#ff00ff00", "#ff806517", "#ff2b547e", "#ffeac117", "#ff3ea99f",
			"#fff88017", "#ffc3fdb8", "#ffff00ff", "#ffffdfdd", "#ffedda74", "#ffff00ff", "#ff66cdaa", "#ffb7ceec", "#ff4e387e", "#ffc68e17",
			"#ff7e587e", "#ffe0ffff", "#ff43c6db", "#ff8a4117", "#fff9b7ff", "#ff00bfff", "#ff00ffff", "#ff347c2c", "#ffe18b6b", "#ff7a5dc7",
			"#ff2b1b17", "#fffefcff", "#ffff00ff", "#ffa23bec", "#ff837e7c", "#ffe2a76f", "#ffe9ab17", "#ff2e8b57", "#ffb048b5", "#ffffb6c1",
			"#ffc04000", "#ff306754", "#ff6a5acd", "#ff6960ec", "#ff43bfc7", "#ff006400", "#ff4c4646", "#ff726e6d", "#ff2f4f4f", "#ffdc143c",
			"#fffffacd", "#fffff5ee", "#ff9f000f", "#ff990012", "#ffe78a61", "#fffdeef4", "#ffffffc2", "#ff85bb65", "#ff56a5ec", "#ff5c5858",
			"#ffff1493", "#ff7fffd4", "#ff342d7e", "#ff7e3517", "#ffc2b280", "#ff728c00", "#ff8467d7", "#ff4cc552", "#fff9966b", "#ff3b9c9c",
			"#fff5f5f5", "#ff387c44", "#ff87f717", "#fff5deb3", "#ff6b8e23", "#ffc88141", "#ffc24641", "#ff00ff7f", "#ff347c17", "#ff0000cd",
			"#ffe0ffff", "#ffffffff", "#ff617c58", "#ff4682b4", "#fffaebd7", "#ff1589ff"
		};

		std::transform(orgColor.begin(), orgColor.end(), orgColor.begin(), tolower);

		auto len = sizeof(webColorNames) / sizeof(std::string);
		std::string lowerColor(orgColor.begin(), orgColor.end());
		lowerColor.erase(remove_if(lowerColor.begin(), lowerColor.end(), isspace), lowerColor.end());

		for (size_t i = 0; i < len; i++)
		{
			if (webColorNames[i] == lowerColor)
			{
				colorCode.assign(webColorCodes[i].begin(), webColorCodes[i].end());
				break;
			}
		}
	}

	if (colorCode != L"#")
	{
		return ref new String(colorCode.c_str());
	}
	else
	{
		return nullptr;
	}
}

bool SubtitleHelper::IsSRTProcessing(AVCodecID codecId)
{
	return codecId == AV_CODEC_ID_SRT
		|| codecId == AV_CODEC_ID_SUBRIP;
}

bool SubtitleHelper::IsASSProcessing(AVCodecID codecId)
{
	return codecId == AV_CODEC_ID_ASS
		|| codecId == AV_CODEC_ID_SSA
		|| codecId == AV_CODEC_ID_MOV_TEXT;
}

bool SubtitleHelper::IsSAMIProcessing(AVCodecID codecId)
{
	return codecId == AV_CODEC_ID_SAMI;
}

CDetectCodepage::CDetectCodepage()
{
	m_mlang = NULL;
	m_dwFlag = MLDETECTCP_NONE;
	m_dwPrefWinCodePage = 0;
}

CDetectCodepage::~CDetectCodepage()
{
	if (m_mlang) m_mlang->Release();
}

//BOOL CDetectCodepage::Init(MLDETECTCP dwFlag = MLDETECTCP_NONE, DWORD dwPrefWinCodePage = CP_ACP)
BOOL CDetectCodepage::Init(MLDETECTCP dwFlag, DWORD dwPrefWinCodePage)
{
	//if (m_mlang) { ASSERT(0); return FALSE; }
	if (m_mlang) { return FALSE; }
	HRESULT hr = CoCreateInstance(CLSID_CMultiLanguage, NULL, CLSCTX_INPROC_SERVER, IID_IMultiLanguage2, (void **)&m_mlang);
	//if (FAILED(hr)) { ASSERT(0); return FALSE; }
	if (FAILED(hr)) { return FALSE; }

	m_dwFlag = dwFlag;
	m_dwPrefWinCodePage = dwPrefWinCodePage;

	return TRUE;
}

int	CDetectCodepage::DetectCodepage(char* str, int length)
{
	m_nConfidence = 0;
	//if (m_mlang == NULL) { ASSERT(0); return -1; }
	if (m_mlang == NULL) { return -1; }

	DetectEncodingInfo detectEncInfo = { 0, };
	int detectEncCount = 1;
	HRESULT	hr = S_OK;

	hr = m_mlang->DetectInputCodepage(m_dwFlag, m_dwPrefWinCodePage, str, &length, &detectEncInfo, &detectEncCount);
	//if (FAILED(hr)) { ASSERT(0); return -1; }
	if (FAILED(hr)) { return -1; }

	// 확률
	m_nConfidence = detectEncInfo.nConfidence;

	return detectEncInfo.nCodePage;
}

int	CDetectCodepage::DetectCodepage(IStream* stream)
{
	m_nConfidence = 0;
	//if (m_mlang == NULL) { ASSERT(0); return -1; }
	if (m_mlang == NULL) { return -1; }

	DetectEncodingInfo detectEncInfo = { 0, };
	int detectEncCount = 1;
	HRESULT	hr = S_OK;

	hr = m_mlang->DetectCodepageInIStream(m_dwFlag, m_dwPrefWinCodePage, stream, &detectEncInfo, &detectEncCount);
	//if (FAILED(hr)) { ASSERT(0); return -1; }
	if (FAILED(hr)) { return -1; }

	// 확률
	m_nConfidence = detectEncInfo.nConfidence;

	return detectEncInfo.nCodePage;
}


SubtitleStream::SubtitleStream(int index, AVCodecContext* avctx)
	: _Index(index)
	, m_pAvCodecContext(avctx)
{
	_Packets = ref new Platform::Collections::Vector<Windows::Data::Json::JsonObject^>();
}

SubtitleStream::~SubtitleStream()
{
	if (m_pAvCodecContext != NULL)
	{
		avcodec_free_context(&m_pAvCodecContext);
	}
}

//SubtitleStream::SubtitleStream(int index, AVCodecID codecId, char* extradata, char* subtitleHeader)
//	: _Index(index)
//	, _CodecId(codecId)
//	, m_extradata(extradata)
//	, m_subtitleHeader(subtitleHeader)
//{
//	_Packets = ref new Platform::Collections::Vector<Windows::Data::Json::JsonObject^>();
//}

int SubtitleStream::Index::get()
{
	return _Index;
}

Windows::Foundation::Collections::IVector<SubtitleLanguage^>^ SubtitleStream::SubtitleLanguages::get()
{
	return _SubtitleLanguages;
}

PropertySet^ SubtitleStream::GlobalStyleProperty::get()
{
	return _GlobalStyleProperty;
}

Windows::Foundation::Collections::IMap<String^, Windows::Data::Json::JsonObject^>^ SubtitleStream::BlockStyleMap::get()
{
	return _BlockStyleMap;
}

std::vector<std::wstring> SubtitleStream::EventList::get()
{
	return _EventList;
}

String^ SubtitleStream::Header::get()
{
	return _Header;
}

String^ SubtitleStream::Title::get()
{
	return _Title;
}

SubtitleContentTypes SubtitleStream::SubtitleType::get()
{
	SubtitleContentTypes packetType;
	switch (m_pAvCodecContext->codec_id)
	{
	case AV_CODEC_ID_MOV_TEXT: //created to ass foramt by ffmpeg  (Timed Text 경우)
	case AV_CODEC_ID_ASS:
	case AV_CODEC_ID_SSA:
		packetType = SubtitleContentTypes::Ass;
		break;
	case AV_CODEC_ID_SUBRIP:  //created to ass foramt by ffmpeg 
	case AV_CODEC_ID_SRT:
		packetType = SubtitleContentTypes::Srt;
		break;
		/*case AV_CODEC_ID_HDMV_PGS_SUBTITLE:
		packetType = SubtitlePacketType::SUBTITLE_TEXT;
		break;*/
	case AV_CODEC_ID_SAMI:
		packetType = SubtitleContentTypes::Sami;
		break;
	default:
		packetType = SubtitleContentTypes::None;
		break;
	}
	return packetType;
}

void SubtitleStream::LoadHeader(int codePage)
{
	char* header = m_pAvCodecContext->codec_id == AV_CODEC_ID_SAMI ? (char*)m_pAvCodecContext->extradata : (char*)m_pAvCodecContext->subtitle_header;
	if (codePage != CP_UTF8 && codePage != CP_UCS2LE && codePage != CP_UCS2BE)
	{
		//문자열 인코딩
		_Header = ToStringHat(header, codePage);
	}
	else
	{
		_Header = ToStringHat(header, CP_UTF8);
	}
	if (SubtitleHelper::IsSAMIProcessing(m_pAvCodecContext->codec_id))
	{
		SubtitleHelper::LoadSAMIHeader(_Header, &_Title, &_GlobalStyleProperty, &_BlockStyleMap, &_SubtitleLanguages);
	}
	else if (SubtitleHelper::IsASSProcessing(m_pAvCodecContext->codec_id))
	{
		SubtitleHelper::LoadASSHeader(_Header, &_GlobalStyleProperty, &_BlockStyleMap, &_EventList);
	}
	if (_Header == nullptr) _Header = "";
	if (_Title == nullptr) _Title = "";
}

Windows::Data::Json::JsonObject^ SubtitleStream::LockPacket(uint32 index)
{
	//std::lock_guard<std::mutex> lock(mutex);
	mutex.lock();
	return _Packets->GetAt(index);
}

void SubtitleStream::UnlockPacket()
{
	mutex.unlock();
}

void SubtitleStream::AppendPacket(Windows::Data::Json::JsonObject^ packet)
{
	std::lock_guard<std::mutex> lock(mutex);
	_Packets->Append(packet);

}

void SubtitleStream::RemovePacket(uint32 index)
{
	std::lock_guard<std::mutex> lock(mutex);
	_Packets->RemoveAt(index);
}

void SubtitleStream::ClearePackets()
{
	std::lock_guard<std::mutex> lock(mutex);
	_Packets->Clear();
}

uint32 SubtitleStream::GetPacketSize()
{
	return _Packets->Size;
}

AVCodecContext* SubtitleStream::CodecContext::get()
{
	return m_pAvCodecContext;
}