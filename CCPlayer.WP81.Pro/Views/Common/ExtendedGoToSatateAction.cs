using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CCPlayer.WP81.Views.Common
{
    public class ExtendedGoToStateAction : DependencyObject, IAction
    {
        public string StateName
        {
            get { return (string)GetValue(StateNameProperty); }
            set { SetValue(StateNameProperty, value); }
        }

        public static readonly DependencyProperty StateNameProperty =
            DependencyProperty.Register("StateName", typeof(string), typeof(ExtendedGoToStateAction), new PropertyMetadata(string.Empty));

        public bool UseTransitions
        {
            get { return (bool)GetValue(UseTransitionsProperty); }
            set { SetValue(UseTransitionsProperty, value); }
        }

        public static readonly DependencyProperty UseTransitionsProperty =
            DependencyProperty.Register("UseTransitions", typeof(bool), typeof(ExtendedGoToStateAction), new PropertyMetadata(false));

        public object TargetObject
        {
            get { return GetValue(TargetObjectProperty); }
            set { SetValue(TargetObjectProperty, value); }
        }

        public static readonly DependencyProperty TargetObjectProperty =
            DependencyProperty.Register("TargetObject", typeof(object), typeof(ExtendedGoToStateAction), new PropertyMetadata(null));


        public object Execute(object sender, object parameter)
        {
            var result = ExtendedVisualStateManager.GoToElementState((FrameworkElement)sender, this.TargetObject, this.StateName, this.UseTransitions);

            //if (this.TargetObject is Windows.UI.Xaml.Controls.ListView)
            //{
            //    System.Diagnostics.Debug.WriteLine("메인");
            //}

            return result;
        }
    }
}
