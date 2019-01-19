using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
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

namespace CCPlayer.WP81.Views
{
    public sealed partial class HubButtonGroup : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private Visibility _Visibility1;
        public Visibility Visibility1
        {
            get
            {
                return _Visibility1;
            }
            set
            {
                if (value != _Visibility1)
                {
                    _Visibility1 = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private Visibility _Visibility2;
        public Visibility Visibility2
        {
            get
            {
                return _Visibility2;
            }
            set
            {
                if (value != _Visibility2)
                {
                    _Visibility2 = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private Visibility _Visibility3;
        public Visibility Visibility3
        {
            get
            {
                return _Visibility3;
            }
            set
            {
                if (value != _Visibility3)
                {
                    _Visibility3 = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private Visibility _Visibility4;
        public Visibility Visibility4
        {
            get
            {
                return _Visibility4;
            }
            set
            {
                if (value != _Visibility4)
                {
                    _Visibility4 = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public Boolean Visible
        {
            get { return (Boolean)this.GetValue(VisibleProperty); }
            set { this.SetValue(VisibleProperty, value); }
        }

        public static readonly DependencyProperty VisibleProperty = DependencyProperty.Register(
          "Visible", typeof(Boolean), typeof(HubButtonGroup), new PropertyMetadata(false));

        public Boolean IsEnable1
        {
            get { return (Boolean)this.GetValue(IsEnable1Property); }
            set { this.SetValue(IsEnable1Property, value); }
        }

        public static readonly DependencyProperty IsEnable1Property = DependencyProperty.Register(
          "IsEnable1", typeof(Boolean), typeof(HubButtonGroup), new PropertyMetadata(false));

        public String Glyph1
        {
            get { return (String)this.GetValue(Glyph1Property); }
            set { this.SetValue(Glyph1Property, value); }
        }

        public static readonly DependencyProperty Glyph1Property = DependencyProperty.Register(
          "Glyph1", typeof(String), typeof(HubButtonGroup), new PropertyMetadata(null));

        public double SymbolScale1
        {
            get { return (double)this.GetValue(SymbolScale1Property); }
            set { this.SetValue(SymbolScale1Property, value); }
        }

        public static readonly DependencyProperty SymbolScale1Property = DependencyProperty.Register(
          "SymbolScale1", typeof(double), typeof(HubButtonGroup), new PropertyMetadata(1));

        public ICommand Command1
        {
            get { return (ICommand)this.GetValue(Command1Property); }
            set { this.SetValue(Command1Property, value); }
        }

        public static readonly DependencyProperty Command1Property = DependencyProperty.Register(
          "Command1", typeof(ICommand), typeof(HubButtonGroup), new PropertyMetadata(null, new PropertyChangedCallback((sender, args) => {
              var btnGrp = sender as HubButtonGroup;
              if (args.NewValue != null)
              {
                  btnGrp.Visibility1 = Visibility.Visible;
              }
              else
              {
                  btnGrp.Visibility1 = Visibility.Collapsed;
              }
          })));

        public Object CommandParam1
        {
            get { return (ICommand)this.GetValue(CommandParam1Property); }
            set { this.SetValue(CommandParam1Property, value); }
        }

        public static readonly DependencyProperty CommandParam1Property = DependencyProperty.Register(
          "CommandParam1", typeof(Object), typeof(HubButtonGroup), new PropertyMetadata(null));
       
        public Boolean IsEnable2
        {
            get { return (Boolean)this.GetValue(IsEnable2Property); }
            set { this.SetValue(IsEnable2Property, value); }
        }

        public static readonly DependencyProperty IsEnable2Property = DependencyProperty.Register(
          "IsEnable2", typeof(Boolean), typeof(HubButtonGroup), new PropertyMetadata(false));

        public String Glyph2
        {
            get { return (String)this.GetValue(Glyph2Property); }
            set { this.SetValue(Glyph2Property, value); }
        }

        public static readonly DependencyProperty Glyph2Property = DependencyProperty.Register(
          "Glyph2", typeof(String), typeof(HubButtonGroup), new PropertyMetadata(null));

        public double SymbolScale2
        {
            get { return (double)this.GetValue(SymbolScale2Property); }
            set { this.SetValue(SymbolScale2Property, value); }
        }

        public static readonly DependencyProperty SymbolScale2Property = DependencyProperty.Register(
          "SymbolScale2", typeof(double), typeof(HubButtonGroup), new PropertyMetadata(1));

        public ICommand Command2
        {
            get { return (ICommand)this.GetValue(Command2Property); }
            set { this.SetValue(Command2Property, value); }
        }

        public static readonly DependencyProperty Command2Property = DependencyProperty.Register(
          "Command2", typeof(ICommand), typeof(HubButtonGroup), new PropertyMetadata(null, new PropertyChangedCallback((sender, args) => {
              var btnGrp = sender as HubButtonGroup;
              if (args.NewValue != null)
              {
                  btnGrp.Visibility2 = Visibility.Visible;
              }
              else
              {
                  btnGrp.Visibility2 = Visibility.Collapsed;
              }
          })));

        public Object CommandParam2
        {
            get { return (ICommand)this.GetValue(CommandParam2Property); }
            set { this.SetValue(CommandParam2Property, value); }
        }

        public static readonly DependencyProperty CommandParam2Property = DependencyProperty.Register(
          "CommandParam2", typeof(Object), typeof(HubButtonGroup), new PropertyMetadata(null));
        
        public Boolean IsEnable3
        {
            get { return (Boolean)this.GetValue(IsEnable3Property); }
            set { this.SetValue(IsEnable3Property, value); }
        }

        public static readonly DependencyProperty IsEnable3Property = DependencyProperty.Register(
          "IsEnable3", typeof(Boolean), typeof(HubButtonGroup), new PropertyMetadata(false));

        public String Glyph3
        {
            get { return (String)this.GetValue(Glyph3Property); }
            set { this.SetValue(Glyph3Property, value); }
        }

        public static readonly DependencyProperty Glyph3Property = DependencyProperty.Register(
          "Glyph3", typeof(String), typeof(HubButtonGroup), new PropertyMetadata(null));
        
        public double SymbolScale3
        {
            get { return (double)this.GetValue(SymbolScale3Property); }
            set { this.SetValue(SymbolScale3Property, value); }
        }

        public static readonly DependencyProperty SymbolScale3Property = DependencyProperty.Register(
          "SymbolScale3", typeof(double), typeof(HubButtonGroup), new PropertyMetadata(1));

        public ICommand Command3
        {
            get { return (ICommand)this.GetValue(Command3Property); }
            set { this.SetValue(Command3Property, value); }
        }

        public static readonly DependencyProperty Command3Property = DependencyProperty.Register(
          "Command3", typeof(ICommand), typeof(HubButtonGroup), new PropertyMetadata(null, new PropertyChangedCallback((sender, args) => {
              var btnGrp = sender as HubButtonGroup;
              if (args.NewValue != null)
              {
                  btnGrp.Visibility3 = Visibility.Visible;
              }
              else
              {
                  btnGrp.Visibility3 = Visibility.Collapsed;
              }
          })));

        public Object CommandParam3
        {
            get { return (ICommand)this.GetValue(CommandParam3Property); }
            set { this.SetValue(CommandParam3Property, value); }
        }

        public static readonly DependencyProperty CommandParam3Property = DependencyProperty.Register(
          "CommandParam3", typeof(Object), typeof(HubButtonGroup), new PropertyMetadata(null));

        public Boolean IsEnable4
        {
            get { return (Boolean)this.GetValue(IsEnable4Property); }
            set { this.SetValue(IsEnable4Property, value); }
        }

        public static readonly DependencyProperty IsEnable4Property = DependencyProperty.Register(
          "IsEnable4", typeof(Boolean), typeof(HubButtonGroup), new PropertyMetadata(false));

        public String Glyph4
        {
            get { return (String)this.GetValue(Glyph4Property); }
            set { this.SetValue(Glyph4Property, value); }
        }

        public static readonly DependencyProperty Glyph4Property = DependencyProperty.Register(
          "Glyph4", typeof(String), typeof(HubButtonGroup), new PropertyMetadata(null));

        public double SymbolScale4
        {
            get { return (double)this.GetValue(SymbolScale4Property); }
            set { this.SetValue(SymbolScale4Property, value); }
        }

        public static readonly DependencyProperty SymbolScale4Property = DependencyProperty.Register(
          "SymbolScale4", typeof(double), typeof(HubButtonGroup), new PropertyMetadata(1));

        public ICommand Command4
        {
            get { return (ICommand)this.GetValue(Command4Property); }
            set { this.SetValue(Command4Property, value); }
        }

        public static readonly DependencyProperty Command4Property = DependencyProperty.Register(
          "Command4", typeof(ICommand), typeof(HubButtonGroup), new PropertyMetadata(null, new PropertyChangedCallback((sender, args) => {
              var btnGrp = sender as HubButtonGroup;
              if (args.NewValue != null)
              {
                  btnGrp.Visibility4 = Visibility.Visible;
              }
              else
              {
                  btnGrp.Visibility4 = Visibility.Collapsed;
              }
          })));

        public Object CommandParam4
        {
            get { return (ICommand)this.GetValue(CommandParam4Property); }
            set { this.SetValue(CommandParam4Property, value); }
        }

        public static readonly DependencyProperty CommandParam4Property = DependencyProperty.Register(
          "CommandParam4", typeof(Object), typeof(HubButtonGroup), new PropertyMetadata(null));

        public HubButtonGroup()
        {
            this.InitializeComponent();

            this.Visibility1 = Visibility.Collapsed;
            this.Visibility2 = Visibility.Collapsed;
            this.Visibility3 = Visibility.Collapsed;
            this.Visibility4 = Visibility.Collapsed;

            this.IsEnable1 = true;
            this.IsEnable2 = true;
            this.IsEnable3 = true;
            this.IsEnable4 = true;
        }
    }
}
