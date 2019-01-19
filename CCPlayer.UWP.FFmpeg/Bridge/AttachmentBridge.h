#pragma once

using namespace Platform;
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
				public ref struct Attachment sealed
				{
				public:
					property String^ FileName;
					property String^ MimeType;
					property Array<uint8_t>^ BinaryData;
				};

				ref class AttachmentBridge;
				public delegate void AttachmentFoundEventHandler(AttachmentBridge^ sender, Attachment^ attachment);
				public delegate void AttachmentCompletedEventHandler(AttachmentBridge^ sender, Windows::Foundation::Collections::IVector<String^>^ savedFontNameList);

				public ref class AttachmentBridge sealed
				{
				public:
					virtual ~AttachmentBridge();
					static property AttachmentBridge^ Instance { AttachmentBridge^ get(); }
					static void Initialize();
					void PopulateAttachment(String^ fileName, String^ mimeType, const Array<uint8_t>^ data);
					void CompleteAttachment();
					void SetUIDispatcher(Object^ uiDispatcher)
					{
						_Dispatcher = safe_cast<CoreDispatcher^>(uiDispatcher);
					}
					void Reset();
					property bool IsSaveAttachment;

					event AttachmentFoundEventHandler^ AttachmentFoundEvent;
					event AttachmentCompletedEventHandler^ AttachmentCompletedEvent;

				private:
					static AttachmentBridge^ _Instance;
					static CoreDispatcher^ _Dispatcher;
					Windows::Foundation::Collections::IVector<String^>^ _FontNameList;

				internal:
					AttachmentBridge();
				};
			}
		}
	}
}
