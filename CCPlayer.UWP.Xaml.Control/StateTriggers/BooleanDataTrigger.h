#pragma once
#include "Common.h"

using namespace Windows::UI::Xaml;

namespace CCPlayer
{
	namespace UWP
	{
		namespace Xaml
		{
			namespace StateTriggers
			{
				[Windows::Foundation::Metadata::WebHostHidden]
				public ref class BooleanDataTrigger sealed : StateTriggerBase
				{
				private:
					static DependencyProperty^ _DataValueProperty;
					static DependencyProperty^ _TriggerValueProperty;
					
					static void OnDataValueChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ e);
					static void OnTriggerValueChanged(DependencyObject^ sender, DependencyPropertyChangedEventArgs^ e);
					static void TriggerStateCheck(DependencyObject^ target, bool dataValue, bool triggerValue);
				public:
					static property DependencyProperty^ DataValueProperty { DependencyProperty^ get() { return _DataValueProperty; } }
					static property DependencyProperty^ TriggerValueProperty { DependencyProperty^ get() { return _TriggerValueProperty; } }
					static bool GetDataValue(DependencyObject^ obj);
					static void SetDataValue(DependencyObject^ obj, bool value);
					static bool GetTriggerValue(DependencyObject^ obj);
					static void SetTriggerValue(DependencyObject^ obj, bool value);

				};
			}
		}
	}
}

