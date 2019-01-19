using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Text;
using Windows.UI.Xaml.Media;

namespace CCPlayer.WP81.Models
{
    public class SubtitleContext : INotifyPropertyChanged
    {
        private string _Text;
        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                Set(ref _Text, value);
            }
        }

        private double _FontSize;
        public double FontSize
        {
            get
            {
                return _FontSize;
            }
            set
            {
                Set(ref _FontSize, value);
            }
        }

        private FontStyle _FontStyle;
        public FontStyle FontStyle
        {
            get
            {
                return _FontStyle;
            }
            set
            {
                Set(ref _FontStyle, value);
            }
        }

        private string _FontFamily;
        public string FontFamily
        {
            get
            {
                return _FontFamily;
            }
            set
            {
                Set(ref _FontFamily, value);
            }
        }

        private FontWeight _FontWeight;
        public FontWeight FontWeight
        {
            get
            {
                return _FontWeight;
            }
            set
            {
                Set(ref _FontWeight, value);
            }
        }

        private Brush _Foreground;
        public Brush Foreground
        {
            get
            {
                return _Foreground;
            }
            set
            {
                Set(ref _Foreground, value);
            }
        }

        private bool _ShadowVisibility;
        public bool ShadowVisibility
        {
            get
            {
                return _ShadowVisibility;
            }
            set
            {
                Set(ref _ShadowVisibility, value);
            }
        }

        private bool _OutlineVisibility;
        public bool OutlineVisibility
        {
            get
            {
                return _OutlineVisibility;
            }
            set
            {
                Set(ref _OutlineVisibility, value);
            }
        }

        private bool _BackgroundVisibility;
        public bool BackgroundVisibility
        {
            get
            {
                return _BackgroundVisibility;
            }
            set
            {
                Set(ref _BackgroundVisibility, value);
            }
        }

        private bool Set<T>(ref T prevValue, T value)
        {
            bool result = false;
            if (!prevValue.Equals(value))
            {
                result = true;
                prevValue = value;
                OnPropertyChanged();
            }
            return result;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }

    }
}
