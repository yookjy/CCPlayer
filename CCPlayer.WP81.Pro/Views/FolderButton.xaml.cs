using CCPlayer.WP81.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    public sealed partial class FolderButton : UserControl
    {
        public string ErrorText
        {
            get { return (string)this.GetValue(ErrorTextProperty); }
            set { this.SetValue(ErrorTextProperty, value); }
        }

        public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
          "ErrorText", typeof(string), typeof(FolderButton), new PropertyMetadata(string.Empty));

        public string Text
        {
            get { return (string)this.GetValue(TextProperty); }
            set { this.SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
          "Text", typeof(string), typeof(FolderButton), new PropertyMetadata(string.Empty));

        public string Glyph1
        {
            get { return (string)this.GetValue(GlyphProperty1); }
            set { this.SetValue(GlyphProperty1, value); }
        }
        public string Glyph2
        {
            get { return (string)this.GetValue(GlyphProperty2); }
            set { this.SetValue(GlyphProperty2, value); }
        }

        public static readonly DependencyProperty GlyphProperty1 = DependencyProperty.Register(
          "Glyph1", typeof(string), typeof(FolderButton), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty GlyphProperty2 = DependencyProperty.Register(
          "Glyph2", typeof(string), typeof(FolderButton), new PropertyMetadata(string.Empty));

        public FolderType FolderType
        {
            get { return (FolderType)this.GetValue(FolderTypeProperty); }
            set { this.SetValue(FolderTypeProperty, value); }
        }

        public static readonly DependencyProperty FolderTypeProperty = DependencyProperty.Register(
          "FolderType", typeof(FolderType), typeof(FolderButton), new PropertyMetadata(null));
        
        public Boolean IsHighlight
        {
            get { return (Boolean)this.GetValue(IsHighlightProperty); }
            set { this.SetValue(IsHighlightProperty, value); }
        }

        public static readonly DependencyProperty IsHighlightProperty = DependencyProperty.Register(
          "IsHighlight", typeof(Boolean), typeof(FolderButton), new PropertyMetadata(false));

        public ICommand Command1
        {
            get { return (ICommand)this.GetValue(CommandProperty1); }
            set { this.SetValue(CommandProperty1, value); }
        }

        public static readonly DependencyProperty CommandProperty1 = DependencyProperty.Register(
          "Command1", typeof(ICommand), typeof(FolderButton), new PropertyMetadata(null));

        public ICommand Command2
        {
            get { return (ICommand)this.GetValue(CommandProperty2); }
            set { this.SetValue(CommandProperty2, value); }
        }

        public static readonly DependencyProperty CommandProperty2 = DependencyProperty.Register(
          "Command2", typeof(ICommand), typeof(FolderButton), new PropertyMetadata(null));

        public FolderButton()
        {
            this.InitializeComponent();
        }
    }
}
