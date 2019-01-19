using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.System.Threading;
using Windows.UI.Xaml.Media.Imaging;

namespace CCPlayer.UWP.Models
{
    public class MediaInfo : FileInfo
    {
        private StorageFile storageFile;

        public bool IsPlaceHolder { get; set; }

        private MediaInfo _PreviousMediaInfo;
        public MediaInfo PreviousMediaInfo
        {
            get
            {
                return _PreviousMediaInfo;
            }
            set
            {
                if (_PreviousMediaInfo != value)
                {
                    _PreviousMediaInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        private MediaInfo _NextMediaInfo;
        public MediaInfo NextMediaInfo
        {
            get
            {
                return _NextMediaInfo;
            }
            set
            {
                if (_NextMediaInfo != value)
                {
                    _NextMediaInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _Title { get; set; }
        public string Title
        {
            get
            {
                if (string.IsNullOrEmpty(_Title))
                {
                    return System.IO.Path.GetFileNameWithoutExtension(Path);
                }
                else
                {
                    return _Title;
                }
            }
            set
            {
                if (_Title != value)
                {
                    _Title = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _ModifiedDateTime;
        public DateTime ModifiedDateTime
        {
            get
            {
                return _ModifiedDateTime;
            }
            set
            {
                if (_ModifiedDateTime != value)
                {
                    _ModifiedDateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private BitmapImage _Thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                //썸네일을 처음 로딩
                if (_Thumbnail == null)
                {
                    var asyncAction = ThreadPool.RunAsync(async handler =>
                    {
                        //파일이 로딩 되지 않은 경우 로딩시킴
                        if (storageFile == null)
                        {
                            storageFile = await this.GetStorageFile(true);
                        }

                        //이미지 변수
                        StorageItemThumbnail thumnail = null;
                        var strUri = "ms-appx:///Assets/SmallLogo.scale-240.png";

                        if (storageFile != null)
                        {
                            //파일이 로딩 되었다.
                            try
                            {
                                //thumnail = await ((StorageFile)this.StorageItem).GetThumbnailAsync(ThumbnailMode.VideosView, 30);
                                thumnail = await this.storageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 30);
                            }
                            catch (Exception)
                            {
                                //썸네일을 사용할 수 없다. 기본이미지 사용
                            }
                        }

                        DispatcherHelper.CheckBeginInvokeOnUI(() =>
                        {
                            BitmapImage img = null;
                            if (thumnail == null || thumnail.Type == ThumbnailType.Icon)
                            {
                                img = new BitmapImage { UriSource = new Uri(strUri) };
                                IsPlaceHolder = true;
                                OnPropertyChanged("IsPlaceHolder");
                            }
                            else
                            {
                                img = new BitmapImage();
                                img.SetSource(thumnail);
                            }
                            Thumbnail = img;
                        });

                    }, WorkItemPriority.Low);
                }
                return _Thumbnail;
            }
            set
            {
                if (value != null)
                {
                    _Thumbnail = value;
                    OnPropertyChanged();
                }
            }
        }

        private ulong _Size;
        public new ulong Size
        {
            get
            {
                if (BasicProperties == null)
                {
                    var asyncAction = ThreadPool.RunAsync(async handler =>
                    {
                        if (storageFile == null)
                        {
                            storageFile = await this.GetStorageFile(true);
                        }

                        if (storageFile != null)
                        {
                            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
                            {
                                var properties = await this.storageFile.GetBasicPropertiesAsync();
                                this.BasicProperties = properties;
                            });
                        }
                    }, WorkItemPriority.Low);
                }
                return _Size;
            }
            set
            {
                if (_Size != value)
                {
                    _Size = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _AddedDateTime;
        public DateTime AddedDateTime
        {
            get
            {
                return _AddedDateTime;
            }
            set
            {
                if (_AddedDateTime != value)
                {
                    _AddedDateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PlaybackProgress
        {
            get
            {
                return (int)(((double)PausedTime / RunningTime) * 100);
            }
        }

        public BasicProperties _BasicProperties;
        public BasicProperties BasicProperties
        {
            get
            {
                return _BasicProperties;
            }
            set
            {
                if (value != null)
                {
                    _BasicProperties = value;
                    this.ModifiedDateTime = value.DateModified.DateTime;
                    this.Size = value.Size;
                }
            }
        }

        private long _RunningTime;
        public long RunningTime
        {
            get
            {
                return _RunningTime;
            }
            set
            {
                if (value != _RunningTime)
                {
                    _RunningTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private long _PausedTime;
        public long PausedTime
        {
            get
            {
                return _PausedTime;
            }
            set
            {
                if (value != _PausedTime)
                {
                    _PausedTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged("PlaybackProgress");
                }
            }
        }

        private bool _IsAddedPlaylist;
        public bool IsAddedPlaylist
        {
            get
            {
                return _IsAddedPlaylist;
            }
            set
            {
                if (_IsAddedPlaylist != value)
                {
                    _IsAddedPlaylist = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SubtitleExt { get; set; }

        public ObservableCollection<SubtitleInfo> SubtitleFileList { get; set; }

        public MediaInfo() : base()
        {
        }

        public MediaInfo(StorageFile storageFile)
            : base(storageFile)
        {
        }

        public void AddSubtitle(SubtitleInfo si)
        {
            if (SubtitleFileList == null)
            {
                SubtitleFileList = new ObservableCollection<SubtitleInfo>();
            }
            SubtitleFileList.Add(si);

            //부모값 복사
            SubtitleExt = SubtitleFileList.Select(x => System.IO.Path.GetExtension(x.Path.ToUpper()).Replace(".", string.Empty)).Aggregate((x, y) => x + " " + y);

            if (SubtitleExt != null)
            {
                //if (SubtitleExt.Length < 3)
                //{
                //    System.Diagnostics.Debug.WriteLine(this.Path);
                //}

                SubtitleExt = SubtitleExt.Trim();
            }

            OnPropertyChanged("SubtitleExt");
        }

        public MediaInfo Clone()
        {
            return (MediaInfo)this.MemberwiseClone();
        }
    }
}
