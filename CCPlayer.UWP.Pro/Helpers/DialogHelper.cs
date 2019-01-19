using Lime.Xaml.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Helpers
{
    public static class DialogHelper
    {
        public static Flyout GetFlyout(ResourceDictionary resources, string flyoutName)
        {
            Flyout flyout = resources[flyoutName] as Flyout;
            return flyout;
        }

        public static void ShowFlyout(ResourceDictionary resources, string flyoutName, FrameworkElement placementTarget)
        {
            var flyout = GetFlyout(resources, flyoutName);
            if (flyout != null)
            {
                flyout.ShowAt(placementTarget);
            }
        }

        public static Action<KeyRoutedEventArgs> _FlyoutKeyUpCallback;
        public static void ShowFlyout(ResourceDictionary resources, string flyoutName, FrameworkElement placementTarget, Action<KeyRoutedEventArgs> keyUpCallback)
        {
            var flyout = GetFlyout(resources, flyoutName);
            if (flyout != null)
            {
                _FlyoutKeyUpCallback = keyUpCallback;
                flyout.ShowAt(placementTarget);
                //키이벤트 등록
                TextBox tb = ElementHelper.FindVisualChild<TextBox>(flyout.Content);
                if (tb != null) tb.KeyUp += FlyoutKeyUp;
            }
        }

        public static void FlyoutKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (_FlyoutKeyUpCallback != null)
            {
                _FlyoutKeyUpCallback.Invoke(e);
            }
        }

        public static void HideFlyout(ResourceDictionary resources, string flyoutName)
        {
            var flyout = GetFlyout(resources, flyoutName);
            if (flyout != null)
            {
                flyout.Hide();
                //키이벤트 제거
                TextBox tb = ElementHelper.FindVisualChild<TextBox>(flyout.Content);
                if (tb != null)
                {
                    tb.KeyUp -= FlyoutKeyUp;
                    _FlyoutKeyUpCallback = null;
                }
            }
        }

        public static void CloseFlyout(string contentControlName)
        {
            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child != null && popup.Child is FlyoutPresenter)
                {
                    var content = popup.Child as FlyoutPresenter;
                    var panel = content.Content as FrameworkElement;
                    if (panel?.Name == contentControlName)
                    {
                        popup.IsOpen = false;
                        break;
                    }
                }
            }
        }

        public static ContentDialog GetSimpleContentDialog(string title, string content, string primaryButtonText)
        {
            return GetSimpleContentDialog(title, content, primaryButtonText, null);
        }

        public static ContentDialog GetSimpleContentDialog(string title, string content, string primaryButtonText, string secondaryButtonText, string linkText = null, ICommand linkCmd = null)
        {
            if (App.ContentDlgOp != null) return null;

            ContentDialog dlg = new ContentDialog();

            Frame rootFrame = Window.Current.Content as Frame;
            Page page = rootFrame?.Content as Page;
            if (page != null) dlg.RequestedTheme = page.RequestedTheme;

            StackPanel panel = new StackPanel() { Margin = new Thickness(0, 6, 0, 6), VerticalAlignment = VerticalAlignment.Center };

            panel.Children.Add(new TextBlock()
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3)
            });

            if (!string.IsNullOrEmpty(linkText))
            {
                HyperlinkButton hlbtn = new HyperlinkButton()
                {
                    Content = linkText,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                if (linkCmd!= null)
                {
                    hlbtn.Command = linkCmd;
                    hlbtn.CommandParameter = dlg;
                }
                panel.Children.Add(hlbtn);
            }

            dlg.Title = title;
            dlg.Content = panel;
            dlg.IsPrimaryButtonEnabled = true;
            dlg.PrimaryButtonText = primaryButtonText;
            if (!string.IsNullOrEmpty(secondaryButtonText))
            {
                dlg.IsSecondaryButtonEnabled = true;
                dlg.SecondaryButtonText = secondaryButtonText;
            }

            return dlg;
        }

    }
}
