#include "pch.h"
#include "BooleanDataTrigger.h"

using namespace CCPlayer::UWP::Xaml::StateTriggers;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Interop;

DependencyProperty^ BooleanDataTrigger::_DataValueProperty = 
	DependencyProperty::RegisterAttached(
		"DataValue", 
		TypeName(bool::typeid), 
		TypeName(BooleanDataTrigger::typeid),
		ref new PropertyMetadata(
			true, 
			ref new PropertyChangedCallback(&BooleanDataTrigger::OnDataValueChanged)
		)
	);

DependencyProperty^ BooleanDataTrigger::_TriggerValueProperty = 
	DependencyProperty::RegisterAttached(
		"TriggerValue", 
		TypeName(bool::typeid), 
		TypeName(BooleanDataTrigger::typeid),
		ref new PropertyMetadata(
			true, 
			ref new PropertyChangedCallback(&BooleanDataTrigger::OnTriggerValueChanged)
		)
	);

bool BooleanDataTrigger::GetDataValue(DependencyObject^ obj)
{
	return (bool)obj->GetValue(DataValueProperty);
}

void BooleanDataTrigger::SetDataValue(DependencyObject^ obj, bool value)
{
	obj->SetValue(DataValueProperty, value);
}

bool BooleanDataTrigger::GetTriggerValue(DependencyObject^ obj)
{
	return (bool)obj->GetValue(TriggerValueProperty);
}

void BooleanDataTrigger::SetTriggerValue(DependencyObject^ obj, bool value)
{
	obj->SetValue(TriggerValueProperty, value);
}

void BooleanDataTrigger::TriggerStateCheck(DependencyObject^ target, bool dataValue, bool triggerValue)
{
	BooleanDataTrigger^ trigger = dynamic_cast<BooleanDataTrigger^>(target);
	if (trigger == nullptr) return;
	trigger->SetActive(triggerValue == dataValue);
}

void BooleanDataTrigger::OnDataValueChanged(DependencyObject^ target, DependencyPropertyChangedEventArgs^ e)
{
	bool triggerValue = (bool)target->GetValue(BooleanDataTrigger::TriggerValueProperty);
	TriggerStateCheck(target, (bool)e->NewValue, triggerValue);
}

void BooleanDataTrigger::OnTriggerValueChanged(DependencyObject^ target, DependencyPropertyChangedEventArgs^ e)
{
	bool dataValue = (bool)target->GetValue(BooleanDataTrigger::DataValueProperty);
	TriggerStateCheck(target, dataValue, (bool)e->NewValue);
}

