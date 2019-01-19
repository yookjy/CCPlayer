#include "pch.h"
#include "SubtitleBridge.h"

using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::FFmpeg::Bridge;
using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::UI::Xaml;



////////////////// SubtitleBridge ///////////////////////////
SubtitleBridge::SubtitleBridge()
	:_InternalSubtitleIndex(-1)
	//,_ExternalSubtitleParser(nullptr)
	, _ExternalSubtitleSource(nullptr)
	,_CodePage(AUTO_DETECT_CODE_PAGE)
{
	NeedChangeCodePage = true;
}

SubtitleBridge::~SubtitleBridge()
{
	Reset();
	_Dispatcher = nullptr;
}

CoreDispatcher^ SubtitleBridge::_Dispatcher;

void SubtitleBridge::Initialize()
{
	if (_Dispatcher != nullptr) return;
	_Dispatcher = Window::Current->Dispatcher;
}

SubtitleBridge^ SubtitleBridge::_Instance;

SubtitleBridge^ SubtitleBridge::Instance::get()
{
	if (_Instance == nullptr)
	{
		_Instance = ref new SubtitleBridge();
	}
	return _Instance;
}

void SubtitleBridge::Reset()
{
	if (_ExternalSubtitleSource != nullptr)
	{
		_ExternalSubtitleSource->IsSeeking = false;
		_ExternalSubtitleSource = nullptr;
	}
/*
	if (_ExternalSubtitleParser != nullptr)
	{
		_ExternalSubtitleParser->Stop();
	}
*/
	_InternalSubtitleIndex = -1;
	//_ExternalSubtitleParser = nullptr;
	CodePage = AUTO_DETECT_CODE_PAGE;
	//NeedChangeCodePage = true;
}

void SubtitleBridge::PopulateSubtitlePacket(Windows::Data::Json::JsonObject^ subPkt, Windows::Foundation::Collections::IMap<String^, ImageData^>^ subtitleImageMap)
{
	if (_Dispatcher == nullptr)
	{
		auto window = Windows::ApplicationModel::Core::CoreApplication::MainView->CoreWindow;
		_Dispatcher = window->Dispatcher;
	}

	if (_Dispatcher != nullptr)
	{
		_Dispatcher->RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal,
			ref new Windows::UI::Core::DispatchedHandler([=] {

			auto ts = Windows::Foundation::TimeSpan();
			ts.Duration = (LONGLONG)(subPkt->GetNamedNumber("Pts") + (subPkt->GetNamedNumber("StartDisplayTime")));

			auto marker = ref new Windows::UI::Xaml::Media::TimelineMarker();
			marker->Time = ts;
			marker->Text = subPkt->Stringify();

			SubtitlePopulatedEvent(this, marker, subtitleImageMap);
		}));
	}
}

int SubtitleBridge::CodePage::get()
{
	return _CodePage;
}

void SubtitleBridge::CodePage::set(int codePage)
{
	if (codePage != _CodePage)
	{
		NeedChangeCodePage = true;
		//변경된 코드페이지를 뒤에서 사용하므로 먼저 변수값을 변경
		_CodePage = codePage;

		//if (_ExternalSubtitleParser != nullptr)
		//{
		//	//코드페이지 처리(자동 검색이면, 코드페이지 디텍터 사용)
		//	_ExternalSubtitleParser->ChangeCodePage();
		//	//헤더를 로드
		//	_ExternalSubtitleParser->LoadHeader();
		//}
	}
}

//CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ SubtitleBridge::ExternalSubtitleParser::get()
//{
//	return _ExternalSubtitleParser;
//}

CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ SubtitleBridge::ExternalSubtitleSource::get()
{
	return _ExternalSubtitleSource;
}


int SubtitleBridge::InternalSubtitleIndex::get()
{
	return _InternalSubtitleIndex;
}

bool SubtitleBridge::IsSeeking::get()
{
	if (_ExternalSubtitleSource != nullptr)
	{
		return _ExternalSubtitleSource->IsSeeking;
	}
	return false;
}

void SubtitleBridge::IsSeeking::set(bool value)
{
	if (_ExternalSubtitleSource != nullptr)
	{
		//OutputDebugMessage(L"IsSeeking === > %d\n", value);
		_ExternalSubtitleSource->IsSeeking = value;
	}
}

//void SubtitleBridge::SetSubtitleParser(CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ externalSubtitleParser)
//{
//	//파서가 변경이 되었다면 헤더를 해당 인코딩으로 다시 로드할 필요가 있음
//	NeedChangeCodePage = _ExternalSubtitleParser != externalSubtitleParser;
//
//	if (externalSubtitleParser != nullptr)
//	{
//		if (NeedChangeCodePage)
//		{
//			//코드페이지 처리(자동 검색이면, 코드페이지 디텍터 사용)
//			externalSubtitleParser->ChangeCodePage();
//			//헤더를 로드
//			externalSubtitleParser->LoadHeader();
//			//선택될 때 해당 위치로 검색
//			externalSubtitleParser->SyncTime();
//			//새로운 파서 설정
//			_ExternalSubtitleParser = externalSubtitleParser;
//		}
//		_InternalSubtitleIndex = -1;
//	}
//}

void SubtitleBridge::SetExternalSubtitleSource(CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ externalSubtitleSource)
{
	//코드 페이지 초기화
	CodePage = AUTO_DETECT_CODE_PAGE;
	//파서가 변경이 되었다면 헤더를 해당 인코딩으로 다시 로드할 필요가 있음
	//NeedChangeCodePage =  _ExternalSubtitleSource != externalSubtitleSource;

	if (externalSubtitleSource != nullptr)
	{
		//if (NeedChangeCodePage)
		{
			////코드페이지 처리(자동 검색이면, 코드페이지 디텍터 사용)
			//externalSubtitleParser->ChangeCodePage();
			////헤더를 로드
			//externalSubtitleParser->LoadHeader();
			////선택될 때 해당 위치로 검색
			//externalSubtitleParser->SyncTime();
			_ExternalSubtitleSource = nullptr;
			//새로운 파서 설정
			_ExternalSubtitleSource = externalSubtitleSource;
		}
		_InternalSubtitleIndex = -1;
	}
}

void SubtitleBridge::SetInternalSubtitleIndex(int internalSubtitleIndex)
{
	if (internalSubtitleIndex != -1)
	{
		//_ExternalSubtitleParser = nullptr;
		_ExternalSubtitleSource = nullptr;
	}
	//코드 페이지 초기화
	CodePage = AUTO_DETECT_CODE_PAGE;
	//파서가 변경이 되었다면 헤더를 해당 인코딩으로 다시 로드할 필요가 있음
	NeedChangeCodePage = _InternalSubtitleIndex != internalSubtitleIndex;
	//새로운 인덱스로 변경
	_InternalSubtitleIndex = internalSubtitleIndex;
}

////////////////// SubtitleBridge ///////////////////////////
