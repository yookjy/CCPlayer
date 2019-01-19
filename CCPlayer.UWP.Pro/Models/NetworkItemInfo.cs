using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Models
{
    public enum ServerTypes
    {
        WebDAV,
        FTP,
        Direct,
        OneDrive
    }

    public class NetworkItemInfo : IMediaItemInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            });
        }

        public NetworkItemInfo() { this.IsFullFitImage = true; }

        public TappedEventHandler Tapped { get; set; }

        public RightTappedEventHandler RightTapped { get; set; }

        public HoldingEventHandler Holding { get; set; }
        public Settings.ServerSetting ServerSetting { get; set; }
        public ServerTypes ServerType { get; set; }
        public string ContentType { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
        public long Size { get; set; }
        private Uri _Uri;
        public Uri Uri
        {
            get { return _Uri; }
            set
            {
                if (_Uri != value)
                {
                    _Uri = value;

                    if (ServerType != ServerTypes.OneDrive)
                    {
                        var segments = Uri.Segments.Select(x => System.Net.WebUtility.UrlDecode(x)).ToArray();
                        if (segments.Length > 1)
                        {
                            var path = segments.ToList().GetRange(0, segments.Length - 1).Aggregate((x, y) => x + y);
                            ParentFolderPath = DecaTec.WebDav.Tools.UriHelper.AddTrailingSlash($"{Uri.Scheme}://{Uri.Host}{path}");
                        }
                        else if (segments.Length == 1)
                        {
                            ParentFolderPath = segments.FirstOrDefault();
                        }
                        else
                        {
                            ParentFolderPath = string.Empty;
                        }
                    }
                }
            }
        }

        public string DisplayName
        {
            get { return Name; }
            set {}
        }

        public ImageSource ImageSource { get { return _ImageItemsSource as ImageSource; } }

        private object _ImageItemsSource;
        public object ImageItemsSource
        {
            get { return _ImageItemsSource; }
            set { if (_ImageItemsSource != value) { _ImageItemsSource = value; OnPropertyChanged(); OnPropertyChanged("ImageSource"); } }
        }

        private bool _IsFullFitImage;
        public bool IsFullFitImage
        {
            get { return _IsFullFitImage; }
            set { if (_IsFullFitImage != value) { _IsFullFitImage = value; OnPropertyChanged(); } }
        }

        private string _OccuredError;
        public string OccuredError
        {
            get { return string.IsNullOrEmpty(_OccuredError) ? string.Empty : _OccuredError; }
            set { if (_OccuredError != value) { _OccuredError = value; OnPropertyChanged(); } }
        }
        
        private TimeSpan _Duration;
        public TimeSpan Duration
        {
            get { return _Duration; }
            set { if (_Duration != value) { _Duration = value; OnPropertyChanged(); } }
        }

        public bool IsFile { get; set; }

        private int _FileCount;
        public int FileCount
        {
            get { return _FileCount; }
            set { if (_FileCount != value) { _FileCount = value; OnPropertyChanged(); } }
        }

        private string _FileCountDescription;
        public string FileCountDescription
        {
            get { return _FileCountDescription; }
            set { if (_FileCountDescription != value) { _FileCountDescription = value; OnPropertyChanged(); } }
        }

        private bool _IsOrderByName;
        public bool IsOrderByName
        {
            get { return _IsOrderByName; }
            set { if (_IsOrderByName != value) { _IsOrderByName = value; OnPropertyChanged(); } }
        }
        public NetworkItemGroup Group { get; set; }

        private List<string> _SubtitleList;
        public List<string> SubtitleList
        {
            get { return _SubtitleList; }
            set
            {
                if (_SubtitleList != value)
                {
                    _SubtitleList = value;
                    OnPropertyChanged();

                    if (SubtitleList == null || SubtitleList.Count() == 0)
                    {
                        _SubtitleExtensions = string.Empty;
                    }
                    else
                    {
                        _SubtitleExtensions = SubtitleList.Select(x => System.IO.Path.GetExtension(x.ToUpper()).Replace(".", string.Empty)).Distinct().Aggregate((x, y) => (x + " " + y).Trim());
                    }
                    OnPropertyChanged("SubtitleExtensions");
                    OnPropertyChanged("ExistSubtitleExtensions");
                }
            }
        }

        private string _SubtitleExtensions;
        public string SubtitleExtensions { get { return _SubtitleExtensions; } }
        
        //public bool ExistSubtitleExtensions { get { return _SubtitleExtensions != null && _SubtitleExtensions.Length > 0; } }
        public bool ExistSubtitleExtensions => _SubtitleExtensions?.Length > 0;

        public string ParentFolderPath { get; set; }

        public string GetAuthenticateUrl(Settings.ServerSetting settings)
        {
            string url = string.Empty;
            switch (ServerType)
            {
                case ServerTypes.FTP:
                    url = GetAuthenticateUrl(settings.FtpUserName, settings.FtpPassword, this.Uri);
                    break;
                default:
                    url = GetAuthenticateUrl(settings.WebDAVUserName, settings.WebDAVPassword, this.Uri);
                    break;
            }
            return url;
        }

        public string GetAuthenticateUrl(string username, string password)
        {
            return GetAuthenticateUrl(username, password, this.Uri);
        }

        public IList<string> GetAuthenticateSubtitleUrl(string username, string password)
        {
            IList<string> pathList = new List<string>();
            if (SubtitleList != null)
            {
                foreach (var path in SubtitleList)
                {
                    try
                    {
                        var uri = new Uri(path);
                        pathList.Add(GetAuthenticateUrl(username, password, uri));
                    }
                    catch (Exception) { }
                }
            }
            return pathList;
        }

        private string GetAuthenticateUrl(string username, string password, Uri uri)
        {
            string url = string.Empty;

            string account = string.Empty;

            if (!string.IsNullOrEmpty(username))
            {
                account = $"{username}@";

                if (!string.IsNullOrEmpty(password))
                    {
                    account = $"{username}:{password}@";
                }
            }

            switch(ServerType)
            {
                case ServerTypes.FTP:
                    url = $"{uri.Scheme}://{account}{uri.Host}:{uri.Port}{uri.LocalPath}";
                    break;
                default:
                    url = $"{uri.Scheme}://{account}{uri.Host}:{uri.Port}{uri.AbsolutePath}"; 
                    break;
            }
            
            return url;
        }
    }
}
