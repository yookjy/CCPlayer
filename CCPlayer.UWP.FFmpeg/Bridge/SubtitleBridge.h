#pragma once
#include <collection.h>
#include <queue>
//#include "LinkList.h"
//#include "SafeQueue.h"
#include "Subtitle\Subtitle.h"
//#include "Subtitle\ExternalSubtitleParser.h"
#include "Subtitle\ExternalSubtitleSource.h"

using namespace CCPlayer::UWP::Common::Codec;
using namespace CCPlayer::UWP::FFmpeg::Subtitle;
using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Core;
using namespace Windows::UI::Xaml;

namespace CCPlayer
{
	namespace UWP
	{
		namespace FFmpeg
		{
			namespace Bridge
			{
				ref class SubtitleBridge;
				[Windows::Foundation::Metadata::WebHostHidden]
				public delegate void SubtitleFoundEventHandler(SubtitleBridge^ sender, SubtitleStream^ subtitleStream);
				[Windows::Foundation::Metadata::WebHostHidden]
				public delegate void SubtitleUpdatedEventHandler(SubtitleBridge^ sender, SubtitleStream^ subtitleStream);
				[Windows::Foundation::Metadata::WebHostHidden]
				public delegate void SubtitlePopulatedEventHandler(SubtitleBridge^ sender, Windows::UI::Xaml::Media::TimelineMarker^ timelineMarker, Windows::Foundation::Collections::IMap<String^, ImageData^>^ subtitleImageMap);

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class SubtitleBridge sealed
				{
				public:
					virtual ~SubtitleBridge();
					static property SubtitleBridge^ Instance { SubtitleBridge^ get(); }
					static void Initialize();
					void PopulateSubtitlePacket(Windows::Data::Json::JsonObject^ subtitleJsonPacket, Windows::Foundation::Collections::IMap<String^, ImageData^>^ subtitleImageMap);
					void SetUIDispatcher(Object^ uiDispatcher)
					{
						_Dispatcher = safe_cast<CoreDispatcher^>(uiDispatcher);
					}

					property int CodePage { int get(); void set(int value); }
					//property CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ ExternalSubtitleParser { CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ get(); }
					property CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ ExternalSubtitleSource { CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ get(); }
					property int InternalSubtitleIndex { int get(); }
					property bool IsSeeking { bool get(); void set(bool value); }

					//void SetSubtitleParser(CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ externalSubtitleParser);
					void SetExternalSubtitleSource(CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ externalSubtitleSource);
					//[Windows::Foundation::Metadata::DefaultOverload]
					void SetInternalSubtitleIndex(int internalSubtitleIndex);
					void Reset();

					event SubtitleFoundEventHandler^ SubtitleFoundEvent;
					event SubtitleUpdatedEventHandler^ SubtitleUpdatedEvent;
					event SubtitlePopulatedEventHandler^ SubtitlePopulatedEvent;

				private:
					static SubtitleBridge^ _Instance;
					static CoreDispatcher^ _Dispatcher;
					int _CodePage;
					int _InternalSubtitleIndex;
					//CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleParser^ _ExternalSubtitleParser;
					CCPlayer::UWP::FFmpeg::Subtitle::ExternalSubtitleSource^ _ExternalSubtitleSource;

				internal:
					SubtitleBridge();
					property bool NeedChangeCodePage;
				};
			}
		}
	}

}
