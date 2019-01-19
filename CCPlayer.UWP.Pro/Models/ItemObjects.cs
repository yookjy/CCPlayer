using System;

namespace CCPlayer.UWP.Models
{
    public class RadioButtonItem<TValue>
    {
        public String Text { get; set; }
        public TValue Key { get; set; }
        public Boolean IsChecked
        {
            get
            {
                return GetChecked(Key);
            }
            set
            {
                SetChecked(Key, value);
            }
        }

        public Func<TValue, Boolean> GetChecked;
        public Action<TValue, Boolean> SetChecked;
    }
}

