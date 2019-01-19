using System;
using System.Collections.Generic;
//using System.ComponentModel;
using System.IO;
using System.Linq;
//using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace CCPlayer.UWP.Views.Controls
{
    public sealed partial class AppProtection : UserControl
    {
        public event EventHandler LoginSucceed;

        public TappedEventHandler AppExitTappedEventHandler;
        public TappedEventHandler AppLoginTappedEventHandler;
        public KeyEventHandler PasswordKeyUpEventHandler;

        public string Password
        {
            get { return (string)this.GetValue(PasswordPanelProperty); }
            set { this.SetValue(PasswordPanelProperty, value); }
        }

        public static readonly DependencyProperty PasswordPanelProperty = DependencyProperty.Register(
          "Password", typeof(string), typeof(AppProtection), new PropertyMetadata(string.Empty));

        public string PasswordHint
        {
            get { return (string)this.GetValue(PasswordHintProperty); }
            set { this.SetValue(PasswordHintProperty, value); }
        }

        public static readonly DependencyProperty PasswordHintProperty = DependencyProperty.Register(
          "PasswordHint", typeof(string), typeof(AppProtection), new PropertyMetadata(string.Empty));

        public bool IsHideAppLockPanel
        {
            get { return (bool)this.GetValue(IsHideAppLockPanelProperty); }
            set
            {
                this.SetValue(IsHideAppLockPanelProperty, value);
                if (!value)
                {
                    PwdBox.IsEnabled = true;
                    PwdBox.Focus(FocusState.Programmatic);
                }
            }
        }

        public static readonly DependencyProperty IsHideAppLockPanelProperty = DependencyProperty.Register(
          "IsHideAppLockPanel", typeof(bool), typeof(AppProtection), 
          new PropertyMetadata(false, 
              new PropertyChangedCallback((sender, e) =>
              {
                  //var _this = sender as AppProtection;
                  //if (e.NewValue != null && !(bool)e.NewValue)
                  //{
                  //    _this.PwdBox.Focus(FocusState.Keyboard);
                  //}
              })));

        public bool InvalidPassword
        {
            get { return (bool)this.GetValue(InvalidPasswordProperty); }
            set { this.SetValue(InvalidPasswordProperty, value); }
        }

        public static readonly DependencyProperty InvalidPasswordProperty = DependencyProperty.Register(
          "InvalidPassword", typeof(bool), typeof(AppProtection), new PropertyMetadata(false));

        public double SlideDistance
        {
            get { return (double)this.GetValue(SlideDistanceProperty); }
            set { this.SetValue(SlideDistanceProperty, value); }
        }

        public static readonly DependencyProperty SlideDistanceProperty = DependencyProperty.Register(
          "SlideDistance", typeof(double), typeof(AppProtection), new PropertyMetadata(0.0));


        public AppProtection()
        {
            this.InitializeComponent();
            
            AppExitTappedEventHandler = AppExitTapped;
            AppLoginTappedEventHandler = AppLoginTapped;
            PasswordKeyUpEventHandler = PasswordKeyUp;
        }

        public void AppExitTapped(object sender, TappedRoutedEventArgs args)
        {
             App.Current.Exit();
        }

        public void AppLoginTapped(object sender, TappedRoutedEventArgs args)
        {
            InvalidPassword = false;
            SlideDistance = Window.Current.Bounds.Height * -1;
            if (!string.IsNullOrWhiteSpace(Password)
                && !string.IsNullOrWhiteSpace(PwdBox.Password)
                && PwdBox.Password == Password)
            {
                InvalidPassword = false;
                IsHideAppLockPanel = true;
                PwdBox.Password = string.Empty;
                //키보드 Hide
                PwdBox.IsEnabled = false;
                
                if (LoginSucceed != null)
                {
                    LoginSucceed(this, new EventArgs());
                }
            }
            else
            {
                InvalidPassword = true;
                PwdBox.Focus(FocusState.Programmatic);
            }
        }

        private void PasswordKeyUp(object sender, KeyRoutedEventArgs e)
        {
            InvalidPassword = false;
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AppLoginTapped(sender, null);
            }
        }
    }
}
