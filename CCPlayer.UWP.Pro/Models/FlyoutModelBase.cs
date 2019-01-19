using PropertyChanged;
using System;
using System.Windows.Input;

namespace CCPlayer.UWP.Models
{
    [AddINotifyPropertyChangedInterface]
    public class FlyoutModelBase<T>
    {
        public string PrimaryTitle { get; set; }
        
        public string SecondaryTitle { get; set; }
        
        public string PrimaryContent { get; set; }
        
        public string SecondaryContent { get; set; }
        
        public string PrimaryButtonText { get; set; }
        
        public string SecondaryButtonText { get; set; }
        
        public string ErrorMessage { get; set; }
        
        public bool ShowErrorMessage { get; set; }
        
        public Action<T> CallbackAction { get; set; }

        [DoNotNotify]
        public ICommand PrimaryButtonCommand { get; set; }

        [DoNotNotify]
        public ICommand SecondaryButtonCommand { get; set; }
        
        public bool IsProcessingPrimaryButton { get; set; }
    }
}
