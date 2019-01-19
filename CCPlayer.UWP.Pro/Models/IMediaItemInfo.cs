using CCPlayer.UWP.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Models
{
    public interface IMediaItemInfo : IMediaItem, INotifyPropertyChanged
    {
        new string DisplayName { get; set; }
        object ImageItemsSource { get; set; }
        bool IsFullFitImage { get; set; }
        string OccuredError { get; set; }
        bool IsFile { get; set; }
        int FileCount { get; set; }
        bool IsOrderByName { get; set; }
        List<string> SubtitleList { get; set; }
        string SubtitleExtensions { get; }
        bool ExistSubtitleExtensions { get; }
        string ParentFolderPath { get; }
        TimeSpan Duration { get; set; }
    }
}
