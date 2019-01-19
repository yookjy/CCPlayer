#pragma once

using namespace Platform;
using namespace Windows::Foundation;

#define BASE_FONT_SIZE 48.0
#define DEFAULT_FONT_SIZE_DEVIDER 30.0F

#define DEFAULT_READONLY_PROPERTY(TYPE, NAME) \
private: \
	TYPE _##NAME; \
public: \
	property TYPE NAME \
	{\
		TYPE get () { return _##NAME; } \
	}

#define DEFAULT_PROPERTY(TYPE, NAME) \
private: \
	TYPE _##NAME; \
public: \
	property TYPE NAME \
	{\
		TYPE get () { return _##NAME; } \
		void set (TYPE value) { if (_##NAME != value) { _##NAME = value; }} \
	}

#define DEFAULT_NOTIFY_PROPERTY(TYPE, NAME) \
private: \
	TYPE _##NAME; \
public: \
	property TYPE NAME \
	{\
		TYPE get () { return _##NAME; } \
		void set (TYPE value) { if (_##NAME != value) { _##NAME = value; OnPropertyChanged(#NAME); }} \
	}

#define DEPENDENCY_PROPERTY(TYPE, NAME) \
private: \
	static DependencyProperty^ _##NAME##Property; \
public:\
	static property DependencyProperty^ NAME##Property { DependencyProperty^ get() { return _##NAME##Property; } }\
	property TYPE NAME \
	{\
		TYPE get () { return (TYPE)this->GetValue(NAME##Property); } \
		void set (TYPE value) { this->SetValue(NAME##Property, value); } \
	}

#define DEPENDENCY_PROPERTY_WITH_EVENT(TYPE, NAME) \
private: \
	static DependencyProperty^ _##NAME##Property; \
	static void On##NAME##Changed(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ args); \
public:\
	static property DependencyProperty^ NAME##Property { DependencyProperty^ get() { return _##NAME##Property; } }\
	property TYPE NAME \
	{\
		TYPE get () { return (TYPE)this->GetValue(NAME##Property); } \
		void set (TYPE value) { this->SetValue(NAME##Property, value); } \
	}

#define DEPENDENCY_PROPERTY_REGISTER(NAME, PROPERTY_TYPE, OWNER_TYPE, DEFAULT_VALUE) \
DependencyProperty^ OWNER_TYPE##::_##NAME##Property = DependencyProperty::Register( \
#NAME, PROPERTY_TYPE##::typeid, OWNER_TYPE##::typeid, \
ref new PropertyMetadata(DEFAULT_VALUE)); 

#define DEPENDENCY_PROPERTY_REGISTER_WITH_EVENT(NAME, PROPERTY_TYPE, OWNER_TYPE, DEFAULT_VALUE) \
DependencyProperty^ OWNER_TYPE##::_##NAME##Property = DependencyProperty::Register( \
#NAME, PROPERTY_TYPE##::typeid, OWNER_TYPE##::typeid, \
ref new PropertyMetadata(DEFAULT_VALUE, ref new PropertyChangedCallback(&##OWNER_TYPE##::On##NAME##Changed))); 

inline void OutputDebugMessage(const wchar_t *fmt, ...)
{
#if _DEBUG
	wchar_t buf[2048];
	va_list args;
	va_start(args, fmt);
	vswprintf_s(buf, fmt, args);
	va_end(args);
	OutputDebugStringW(buf);
#endif
}

class CStopWatch
{
private:
	clock_t start;
	clock_t finish;

public:
	double GetDuration() { return (double)(finish - start) / CLOCKS_PER_SEC; }
	void Start() { start = clock(); }
	void Stop() { finish = clock(); }
}; // 


namespace CCPlayer
{
	inline void ThrowIfFailed(HRESULT hr)
	{
		if (FAILED(hr))
		{

			// Set a breakpoint on this line to catch DirectX API errors
			throw Exception::CreateException(hr);
		}
	}

	//class TextureLock sealed
	//{
	//public:
	//	TextureLock(ID3D11DeviceContext *pContext, ID3D11Texture2D *pTex) : _pTex(pTex), _pContext(pContext), _bLocked(false) {}
	//	~TextureLock();
	//	HRESULT Map(UINT uiIndex, D3D11_MAP mapType, UINT mapFlags);
	//	D3D11_MAPPED_SUBRESOURCE map;

	//private:
	//	Microsoft::WRL::ComPtr<ID3D11Texture2D> _pTex;
	//	Microsoft::WRL::ComPtr<ID3D11DeviceContext> _pContext;
	//	UINT _uiIndex;
	//	bool _bLocked;
	//};

	namespace UWP
	{
		namespace Xaml
		{
			namespace Controls
			{
				public ref class MediaFileSuffixes sealed
				{
				public:
					static property Array<String^>^ CLOSED_CAPTION_SUFFIX { Array<String^>^ get() { return _CLOSED_CAPTION_SUFFIX; }}
					static property Array<String^>^ VIDEO_SUFFIX { Array<String^>^ get() { return _VIDEO_SUFFIX; }}

				private:
					static Array<String^>^ _CLOSED_CAPTION_SUFFIX;
					static Array<String^>^ _VIDEO_SUFFIX;
				};

				public enum class AspectRatios
				{
					None,
					Uniform,
					UniformToFill,
					Fill,
					R16_9,
					R16_10,
					R4_3,
					R235_1,
					R185_1
				};

				public enum class DisplayRotations
				{
					None,
					Clockwise90,
					Clockwise180,
					Clockwise270
				};

				public enum class PlaybackRepeats
				{
					None,
					Once,
					All
				};

				[Windows::UI::Xaml::Data::BindableAttribute]
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class KeyName sealed
				{
				public:
					KeyName(Object^ key, String^ name);
					KeyName(Object^ key, String^ name, Object^ payload);
					KeyName(Object^ key, String^ name, String^ type, Object^ payload);

					property Object^ Key;
					property String^ Name;
					property String^ Type;
					property Object^ Payload;
					property Object^ Payload2;
					
					property Windows::UI::Xaml::Input::TappedEventHandler^ ItemTapped;
				};

				public enum class ClosedCaptionDataTypes
				{
					Append,
					Destroy
				};

				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class ClosedCaptionData sealed
				{
				public:
					ClosedCaptionData(TimeSpan startTime, TimeSpan endTime, Windows::Data::Json::JsonObject^ jsonCCData, Object^ extraData);

					property TimeSpan StartTime;
					property TimeSpan EndTime;
					property String^ Text;
					property ClosedCaptionDataTypes Flag;
					property Windows::Data::Json::JsonObject^ JsonCCData;
					property Object^ ExtraData;
				};

			}
		}
	}
}