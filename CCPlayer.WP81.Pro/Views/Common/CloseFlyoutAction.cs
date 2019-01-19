using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.Views.Common
{
    public class CloseFlyoutAction : DependencyObject, IAction
    {
        public object Execute(object sender, object parameter)
        {
            var flyout = sender as Flyout;
            if (flyout == null)
                throw new ArgumentException("CloseFlyoutAction can be used only with Flyout");

            flyout.Hide();

            return null;
        }
    } 
}
