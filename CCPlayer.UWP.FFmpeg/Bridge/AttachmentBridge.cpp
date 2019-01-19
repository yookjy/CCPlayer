#include "pch.h"
#include <collection.h>
#include "AttachmentBridge.h"

using namespace CCPlayer::UWP::FFmpeg::Bridge;
using namespace Platform;
using namespace Windows::UI::Xaml;

AttachmentBridge::AttachmentBridge()
{
	IsSaveAttachment = false;
	_FontNameList = ref new Platform::Collections::Vector<String^>();
}

AttachmentBridge::~AttachmentBridge()
{
}

CoreDispatcher^ AttachmentBridge::_Dispatcher;

void AttachmentBridge::Initialize()
{
	if (_Dispatcher != nullptr) return;
	_Dispatcher = Window::Current->Dispatcher;
}


AttachmentBridge^ AttachmentBridge::_Instance;

AttachmentBridge^ AttachmentBridge::Instance::get()
{
	if (_Instance == nullptr)
	{
		_Instance = ref new AttachmentBridge();
	}
	return _Instance;
}

void AttachmentBridge::Reset()
{
	_FontNameList->Clear();
}

void AttachmentBridge::PopulateAttachment(String^ fileName, String^ mimeType, const Array<uint8_t>^ data)
{
	if (IsSaveAttachment)
	{
		_FontNameList->Append(fileName);

		if (_Dispatcher == nullptr)
		{
			auto window = Windows::ApplicationModel::Core::CoreApplication::MainView->CoreWindow;
			_Dispatcher = window->Dispatcher;
		}

		if (_Dispatcher != nullptr)
		{
			_Dispatcher->RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal,
				ref new Windows::UI::Core::DispatchedHandler([=] {

				Attachment^ attachment = ref new Attachment();
				attachment->FileName = fileName;
				attachment->MimeType = mimeType;
				//attachment->BinaryData = ref new Platform::Array<uint8_t>(data, cbData);
				attachment->BinaryData = data;
				try 
				{
					AttachmentFoundEvent(this, attachment);
				}
				catch (Platform::DisconnectedException ^e)
				{
					OutputDebugString(e->Message->Data());
				}
				
			}));
		}
	}
}

void AttachmentBridge::CompleteAttachment()
{
	if (IsSaveAttachment)
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
				AttachmentCompletedEvent(this, _FontNameList);
			}));
		}
	}
}
