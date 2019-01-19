using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CCPlayer.UWP.StateTriggers
{
    
    public class TimeSpanDataTrigger : StateTriggerBase
    {
        #region DataValue
        public static TimeSpan GetDataValue(DependencyObject obj)
        {
            return (TimeSpan)obj.GetValue(DataValueProperty);
        }

        public static void SetDataValue(DependencyObject obj, Boolean value)
        {
            obj.SetValue(DataValueProperty, value);
        }

        public static readonly DependencyProperty DataValueProperty =
            DependencyProperty.RegisterAttached("DataValue", typeof(TimeSpan),
                typeof(TimeSpanDataTrigger), new PropertyMetadata(false, DataValueChanged));

        private static void DataValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            TimeSpan triggerValue = (TimeSpan)target.GetValue(TimeSpanDataTrigger.TriggerValueProperty);
            TriggerStateCheck(target, (TimeSpan)e.NewValue, triggerValue);
        }
        #endregion

        #region TriggerValue

        public static TimeSpan GetTriggerValue(DependencyObject obj)
        {
            return (TimeSpan)obj.GetValue(TriggerValueProperty);
        }

        public static void SetTriggerValue(DependencyObject obj, TimeSpan value)
        {
            obj.SetValue(TriggerValueProperty, value);
        }

        public static readonly DependencyProperty TriggerValueProperty =
                DependencyProperty.RegisterAttached("TriggerValue", typeof(TimeSpan),
                    typeof(TimeSpanDataTrigger), new PropertyMetadata(false, TriggerValueChanged));

        private static void TriggerValueChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            TimeSpan dataValue = (TimeSpan)target.GetValue(TimeSpanDataTrigger.DataValueProperty);
            TriggerStateCheck(target, dataValue, (TimeSpan)e.NewValue);
        }
        #endregion

        private static void TriggerStateCheck(DependencyObject target, TimeSpan dataValue, TimeSpan triggerValue)
        {
            TimeSpanDataTrigger trigger = target as TimeSpanDataTrigger;
            if (trigger == null) return;
            trigger.SetActive(triggerValue == dataValue);
        }
    }
}
