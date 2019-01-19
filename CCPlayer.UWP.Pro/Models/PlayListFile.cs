using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.Models
{
    public class PlayListFile : StorageItemInfo
    {
        public PlayListFile() : base() { this.IsFile = true; }

        public PlayListFile(IStorageItem storageItem, SubType subType = SubType.None) : base(storageItem, subType) { this.IsFile = true; }

        public long Seq { get; set; }

        public long OrderNo { get; set; }

        public long NewOrderNo { get; set; }

        private DateTime _AddedDateTime;
        public DateTime AddedDateTime
        {
            get { return _AddedDateTime; }
            set { if (_AddedDateTime != value) { _AddedDateTime = value; OnPropertyChanged(); } }
        }

        private TimeSpan _PausedTime;
        public TimeSpan PausedTime
        {
            get { return _PausedTime; }
            set { if (value != _PausedTime) { _PausedTime = value; OnPropertyChanged(); } }
        }
    }
}
