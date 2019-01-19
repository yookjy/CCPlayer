using PropertyChanged;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.Models
{
    [AddINotifyPropertyChangedInterface]
    public class MenuItem 
    {
        [DoNotNotify]
        public MenuType Type { get; set; }

        public string Name { get; set; }

        [DoNotNotify]
        public string Glyph { get; set; }

        [DoNotNotify]
        public string Description { get; set; }

        [DoNotNotify]
        public TappedEventHandler ItemTapped { get; set; }
    }
}
