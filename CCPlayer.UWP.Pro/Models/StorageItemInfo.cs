using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.UWP.Models
{
    public enum SubType
    {
        None,
        RootFolder,
        LastFolder,
        FileAssociation
    }

    public class StorageItemInfo : IMediaItemInfo
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

        public StorageItemInfo() : base()
        {
            IsFullFitImage = true;
        }

        public StorageItemInfo(IStorageItem storageItem, SubType subType = SubType.None) : base()
        {
            this.StorageItem = storageItem;
            this.DateCreated = storageItem.DateCreated;
            this.IsFile = storageItem.IsOfType(StorageItemTypes.File);
            IsFullFitImage = true;
            this.Name = storageItem.Name;
            this.Path = storageItem.Path;
            this.SubType = subType;
            //폴더이거나 파일 연결의 경우 FAL등록
            if (!IsFile || subType == SubType.FileAssociation)
            {
                //FAL추가 여부 체크
                CheckFutureAccessList(storageItem);
            }
            //표시 이름 설정
            this.SetDisplayName();

            var storageProvider = storageItem as IStorageItemPropertiesWithProvider;
            if (storageProvider != null)
            {
                IsNetworkStorage = storageProvider?.Provider.Id.ToLower() == "network";

            }
        }

        protected IStorageItem StorageItem { get; set; }

        public string RootPath { get; set; }

        public SubType SubType { get; set; }

        public string ContentType { get; protected set; }

        public string Path { get; set; }

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { if (_Name != value) { _Name = value; OnPropertyChanged(); } }
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
            set {  if (_IsFullFitImage != value) { _IsFullFitImage = value;  OnPropertyChanged(); } }
        }

        private string _DisplayName;
        public string DisplayName
        {
            get { return _DisplayName; }
            set { if (_DisplayName != value) { _DisplayName = value; OnPropertyChanged(); } }
        }

        private ulong _SIze;
        public ulong Size
        {
            get { return _SIze; }
            set { if (_SIze != value) { _SIze = value; OnPropertyChanged(); } }
        }

        private DateTimeOffset _DateCreated;
        public DateTimeOffset DateCreated
        {
            get { return _DateCreated; }
            set { if (_DateCreated != value) { _DateCreated = value; OnPropertyChanged(); } }
        }
        
        private TimeSpan _Duration;
        public TimeSpan Duration
        {
            get { return _Duration; }
            set { if (_Duration != value) { _Duration = value; OnPropertyChanged(); } }
        }

        public bool IsFile { get; set; }

        //public string Glyph { get { return IsFile ? "\xE714" : "\xE8B7"; } }

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

        private List<string> _SubtitleList;
        public List<string> SubtitleList
        {
            get { return _SubtitleList; }
            set {
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

        public TappedEventHandler Tapped { get; set; }

        public RightTappedEventHandler RightTapped { get; set; }

        public HoldingEventHandler Holding { get; set; }

        public TappedEventHandler ContextMenuTapped { get; set; }

        public StorageItemGroup Group { get; set; }

        public string FalToken { get; set; }

        public bool NeedToUpdateToken { get; set; }

        private string _OccuredError;
        public string OccuredError
        {
            get { return string.IsNullOrEmpty(_OccuredError) ? string.Empty : _OccuredError; }
            set { if (_OccuredError != value) { _OccuredError = value; OnPropertyChanged(); } }
        }

        public bool IsNetworkStorage { get; set; }

        public string ParentFolderPath
        {
            get
            {
                var parent = Path;

                if (!string.IsNullOrEmpty(Path))
                {
                    if (System.IO.Path.GetPathRoot(Path) != Path)
                    {
                        var p = Path.Replace(System.IO.Path.GetPathRoot(Path), string.Empty);
                        var ps = p.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);

                        if (ps.Length > 0)
                        {
                            var nps = new string[ps.Length - 1];
                            Array.Copy(ps, nps, nps.Length);
                            parent = System.IO.Path.GetPathRoot(Path) + System.IO.Path.Combine(nps);
                        }
                    }
                }

                return parent;
            }
        }
       
        virtual protected void CheckFutureAccessList(IStorageItem item)
        {
            try
            {
                //접근 권한 체크하여 없으면 추가 
                //추가된 폴더이면 무조건 FAL추가 (모바일은 추가된 폴더 이하 권한 없음, PC는 디스크 권한 없음)
                if (!StorageApplicationPermissions.FutureAccessList.CheckAccess(item) 
                    || SubType == SubType.RootFolder)
                {
                    this.FalToken = StorageApplicationPermissions.FutureAccessList.Add(item, this.GetType().ToString());
                }
            }
            catch (Exception) { }
        }

        public async Task<StorageFile> GetStorageFileAsync()
        {
            var storageFile = StorageItem as StorageFile;

            if (storageFile == null)
            {
                try
                {
                    if (string.IsNullOrEmpty(this.FalToken))
                    {
                        storageFile = await StorageFile.GetFileFromPathAsync(this.Path);
                    }
                    else
                    {
                        storageFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(this.FalToken);
                    }

                    ContentType = storageFile.ContentType;
                }
                catch (Exception)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() => 
                    {
                        OccuredError = ResourceLoader.GetForCurrentView().GetString("Message/Error/FileNotFound");
                    });
                }
            }

            StorageItem = storageFile;
            return storageFile;
        }

        public async Task<StorageFolder> GetStorageFolderAsync()
        {
            var storageFolder = StorageItem as StorageFolder;

            if (storageFolder == null)
            {
                try
                {
                    if (string.IsNullOrEmpty(this.FalToken))
                    {
                        storageFolder = await StorageFolder.GetFolderFromPathAsync(this.Path);
                    }
                    else
                    {
                        storageFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(this.FalToken);
                    }

                }
                catch (Exception)
                {
                    //2016-12-04 폴더 로딩 오류 자동 복구 모두 추가
                    System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(this.Path);
                    bool recovered = false;
                    if (di.Exists)
                    {
                        try
                        {
                            var errorToken = this.FalToken;
                            storageFolder = await StorageFolder.GetFolderFromPathAsync(this.Path);
                            CheckFutureAccessList(storageFolder);
                            //에러가 발생한 토큰 백업
                            if (errorToken != this.FalToken)
                            {
                                this.NeedToUpdateToken = true;
                            }
                        }
                        catch(Exception) {}
                        //폴더가 복구됨
                        recovered = true;
                    }

                    if (!recovered)
                    {
                        //복구가 실패한 경우만 에러 표시
                        DispatcherHelper.CheckBeginInvokeOnUI(() => 
                        {
                            OccuredError = ResourceLoader.GetForCurrentView().GetString("Message/Error/FolderNotFound");
                        });
                    }
                }
            }

            StorageItem = storageFolder;
            return storageFolder;
        }

        public void SetDisplayName()
        {
            if (!string.IsNullOrEmpty(_Name) && string.IsNullOrEmpty(_DisplayName))
            {
                string text = _Name.TrimStart();
                var start = text.IndexOf('[');
                if (start == 0)
                {
                    var end = text.IndexOf(']');
                    var len = IsFile ? text.LastIndexOf('.') : text.Length;

                    if (end + 1 < len)
                    {
                        var tmp = text.Substring(end + 1).Trim();
                        if (tmp.IndexOf('-') == 0 || tmp.IndexOf('_') == 0)
                        {
                            tmp = tmp.Substring(1).Trim();
                        }
                        text = tmp;
                    }
                }
                DisplayName = text;
            }
        }
    }
}
