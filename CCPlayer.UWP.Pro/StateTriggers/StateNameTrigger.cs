using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CCPlayer.UWP.StateTriggers
{
    
    public class StateNameTrigger : StateTriggerBase
    {
        #region StateName
        public static string GetStateName(DependencyObject obj)
        {
            return (string)obj.GetValue(StateNameProperty);
        }

        public static void SetStateName(DependencyObject obj, string value)
        {
            obj.SetValue(StateNameProperty, value);
        }

        public static readonly DependencyProperty StateNameProperty =
            DependencyProperty.RegisterAttached("StateName", typeof(string),
                typeof(StateNameTrigger), new PropertyMetadata(string.Empty, StateNameChanged));

        private static void StateNameChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            string triggerValue = (string)target.GetValue(StateNameTrigger.TriggerNameProperty);
            TriggerStateCheck(target, (string)e.NewValue, triggerValue);
        }
        #endregion

        #region TriggerName

        public static string GetTriggerName(DependencyObject obj)
        {
            return (string)obj.GetValue(TriggerNameProperty);
        }

        public static void SetTriggerName(DependencyObject obj, string value)
        {
            obj.SetValue(TriggerNameProperty, value);
        }

        public static readonly DependencyProperty TriggerNameProperty =
                DependencyProperty.RegisterAttached("TriggerName", typeof(string),
                    typeof(StateNameTrigger), new PropertyMetadata("None", TriggerNameChanged));

        private static void TriggerNameChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            string dataValue = (string)target.GetValue(StateNameTrigger.StateNameProperty);
            TriggerStateCheck(target, dataValue, (string)e.NewValue);
        }
        #endregion

        private static void TriggerStateCheck(DependencyObject target, string dataValue, string triggerValue)
        {
            StateNameTrigger trigger = target as StateNameTrigger;
            if (trigger == null) return;
            trigger.SetActive(triggerValue == dataValue);
        }
    }
}
