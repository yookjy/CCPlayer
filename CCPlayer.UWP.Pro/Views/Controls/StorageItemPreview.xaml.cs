using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace CCPlayer.UWP.Views.Controls
{
    public sealed partial class StorageItemPreview : UserControl
    {
        public bool IsFile
        {
            get { return (bool)this.GetValue(IsFileProperty); }
            set { this.SetValue(IsFileProperty, value); }
        }

        public static readonly DependencyProperty IsFileProperty = DependencyProperty.Register(
          "IsFile", typeof(bool), typeof(StorageItemPreview),
          new PropertyMetadata(true,
              new PropertyChangedCallback((sender, args) =>
              {
                  var _this = sender as StorageItemPreview;
                  if (args.NewValue != null)
                  {
                      _this.PlaceHolderIcon.Glyph = (bool)args.NewValue ? "\xE714" : "\xE8B7";
                  }
              })));

        public bool IsFullFit
        {
            get { return (bool)this.GetValue(IsFullFitProperty); }
            set { this.SetValue(IsFullFitProperty, value); }
        }

        public static readonly DependencyProperty IsFullFitProperty = DependencyProperty.Register(
          "IsFullFit", typeof(bool), typeof(StorageItemPreview),
          new PropertyMetadata(true));

        public object ImageItemsSource
        {
            get { return (object)this.GetValue(ImageItemsSourceProperty); }
            set { this.SetValue(ImageItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageItemsSourceProperty = DependencyProperty.Register(
          "ImageItemsSource", typeof(object), typeof(StorageItemPreview), 
          new PropertyMetadata(null, 
              new PropertyChangedCallback((sender, args) => 
              {
                  var _this = sender as StorageItemPreview;
                  _this.ContentPanel.Children.Clear();
                  _this.ContentPanel.ColumnDefinitions.Clear();
                  _this.ContentPanel.RowDefinitions.Clear();

                  if (args.NewValue is ImageSource)
                  {
                      _this.ContentPanel.Children.Add(new Image()
                      {
                          Stretch = _this.IsFullFit ? Stretch.UniformToFill : Stretch.Uniform,
                          Margin = _this.IsFullFit ? new Thickness(0) : new Thickness(12),
                          Source = args.NewValue as ImageSource
                      });
                  }
                  else if (args.NewValue is IList<ImageSource>)
                  {
                      var imageSourceList = args.NewValue as IList<ImageSource>;
                      if (imageSourceList != null)
                      {
                          if (imageSourceList.Count >= 4)
                          {
                              _this.ContentPanel.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                              _this.ContentPanel.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                              _this.ContentPanel.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                              _this.ContentPanel.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                          }

                          foreach (var imageSource in imageSourceList.Select((value, index) => new { index, value }))
                          {
                              var img = new Image()
                              {
                                  Stretch = _this.IsFullFit ? Stretch.UniformToFill : Stretch.Uniform,
                                  Source = imageSource.value
                              };

                              var col = imageSource.index / 2;
                              var row = imageSource.index % 2;

                              Grid.SetColumn(img, col);
                              Grid.SetRow(img, row);
                              
                              _this.ContentPanel.Children.Add(img);

                              if (imageSourceList.Count < 4) break;
                          }
                      }
                  }
                  //앱이 죽는경우가 발생 (ffmpeg으로 썸네일을 로드해놓고 폴더&파일과 재생목록을 오갈때 두번째 동일한 썸네일이 로드 될때, 애니메이션 때문에 죽음)
                  //_this.FadeInStoryboard.Begin();
              })));


        public StorageItemPreview()
        {
            this.InitializeComponent();
        }
    }
}
