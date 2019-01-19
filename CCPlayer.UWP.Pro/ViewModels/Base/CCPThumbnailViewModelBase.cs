using CCPlayer.UWP.Common.Interface;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using GalaSoft.MvvmLight.Threading;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


namespace CCPlayer.UWP.ViewModels.Base
{
    public abstract class CCPThumbnailViewModelBase : CCPViewModelBase
    {
        protected const int GROUP_MAX_ITME_COUNT = 20;

        public CCPThumbnailViewModelBase() : base()
        {
            ThumbnailSize = new Size(128, 72);
        }

        [DependencyInjection]
        protected Settings _Settings;

        public Settings Settings => _Settings;

        [DependencyInjection]
        protected ThumbnailDAO _ThumbnailDAO;
        
        protected ThumbnailDAO ThumbnailDAO => _ThumbnailDAO;

        [DoNotNotify]
        protected Size ThumbnailSize { get; set; }

        protected async Task<ImageSource> GetThumbnailAsync(StorageFile file, ICollection<Thumbnail> thumbnailList, bool useUnsupportedThumbnail)
        {
            ImageSource imageSource = null;
            var basicProperties = await file.GetBasicPropertiesAsync();
            var fi = new StorageItemInfo(file) { Size = basicProperties.Size };
            var thumbnail = thumbnailList?.FirstOrDefault(x => x.Name == fi.Name.ToLower() && x.ParentPath == fi.ParentFolderPath.ToLower()
                    && x.Size == fi.Size && x.CreatedDateTime == file.DateCreated.DateTime);

            if (thumbnail == null)
            {
                //썸네일 로드
                StorageItemThumbnail thumb = null;
                if (!string.IsNullOrEmpty(file.ContentType))
                {
                    thumb = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                }

                if (thumb?.Type == ThumbnailType.Image)
                {
                    //썸네일 설정
                    await DispatcherHelper.RunAsync(() =>
                    {
                        var bi = new BitmapImage();
                        bi.SetSource(thumb);
                        imageSource = bi;
                    });
                }
                else if (useUnsupportedThumbnail)
                {
                    var stream = await file.OpenReadAsync();
                    //썸네일을 로드하지 못한 경우  FFmpeg으로 처리
                    var ffInfo = CCPlayer.UWP.Factory.MediaInformationFactory.CreateMediaInformationFromStream(stream);
                    imageSource = await GetThumbnailAsync(fi, ffInfo);
                }
            }
            else if (useUnsupportedThumbnail)
            {
                //캐싱 이미지 로드
                imageSource = await GetChachedThumbnail(thumbnail);
            }
            return imageSource;
        }

        protected async Task<ImageSource> GetThumbnailAsync(NetworkItemInfo item, ICollection<Thumbnail> thumbnailList, bool useUnsupportedThumbnail, int codePage)
        {
            ImageSource imageSource = null;
            var thumbnail = thumbnailList?.FirstOrDefault(x => x.Name == item.Name.ToLower() && x.ParentPath == item.ParentFolderPath.ToLower()
                    && x.Size == (ulong)item.Size && x.CreatedDateTime == item.Modified);

            if (thumbnail == null)
            {
                //썸네일 로드
                if (useUnsupportedThumbnail)
                {
                    //썸네일을 로드하지 못한 경우  FFmpeg으로 처리
                    var url = item.GetAuthenticateUrl(Settings.Server);
                    var ffInfo = CCPlayer.UWP.Factory.MediaInformationFactory.CreateMediaInformationFromUri(url);
                    imageSource = await GetThumbnailAsync(item, ffInfo);
                }
            }
            else if (useUnsupportedThumbnail)
            {
                //캐싱 이미지 로드
                imageSource = await GetChachedThumbnail(thumbnail);
            }
            return imageSource;
        }

        protected async void LoadThumbnailAsync(StorageItemInfo file, ICollection<Thumbnail> thumbnailList, bool useUnsupportedThumbnail)
        {
            var child = await file.GetStorageFileAsync();
            var thumbnail = thumbnailList?.FirstOrDefault(x => x.Name == file.Name.ToLower() && x.ParentPath == file.ParentFolderPath.ToLower()
                && x.Size == file.Size && x.CreatedDateTime == child.DateCreated.DateTime);

            if (thumbnail == null)
            {
                //썸네일 로드
                StorageItemThumbnail thumb = null;
                if (!string.IsNullOrEmpty(child.ContentType))
                {
                    thumb = await child.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
                }

                if (thumb?.Type == ThumbnailType.Image)
                {
                    //비디오 런닝타임 설정
                    var videoProperty = await child.Properties.GetVideoPropertiesAsync();
                    file.Duration = videoProperty.Duration;
                    //썸네일 설정
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        var bi = new BitmapImage();
                        bi.SetSourceAsync(thumb).Completed = new AsyncActionCompletedHandler((setSourceAction, setSourceStaus) =>
                        {
                            if (setSourceStaus == Windows.Foundation.AsyncStatus.Completed)
                            {
                                file.ImageItemsSource = bi;
                            }
                        });
                    });
                }
                else if (useUnsupportedThumbnail)
                {
                    child.OpenReadAsync().Completed = (asyncStream, status) =>
                    {
                        if (status == Windows.Foundation.AsyncStatus.Completed)
                        {
                            //썸네일을 로드하지 못한 경우  FFmpeg으로 처리
                            var stream = asyncStream.GetResults();
                            if (stream != null)
                            {
                                var ffInfo = CCPlayer.UWP.Factory.MediaInformationFactory.CreateMediaInformationFromStream(stream);
                                LoadThumbnailFromMediaInformaion(file, ffInfo);
                            }
                        }
                    };
                }
            }
            else if (useUnsupportedThumbnail)
            {
                //캐싱 이미지 로드
                LoadCachedThumbnail(file, thumbnail);
            }
        }

        protected void LoadThumbnailAsync(NetworkItemInfo item, ICollection<Thumbnail> thumbnailList, bool useUnsupportedThumbnail, int codePage)
        {
            var thumbnail = thumbnailList?.FirstOrDefault(x => x.Name == item.Name.ToLower() && x.ParentPath == item.ParentFolderPath.ToLower()
                && x.Size == (ulong)item.Size && x.CreatedDateTime == item.Modified);

            if (thumbnail == null)
            {
                //썸네일 로드
                if (useUnsupportedThumbnail)
                {
                    var url = item.GetAuthenticateUrl(Settings.Server);
                    Windows.Foundation.Collections.PropertySet ps = new Windows.Foundation.Collections.PropertySet();
                    ps["codepage"] = codePage;
                    var ffInfo = CCPlayer.UWP.Factory.MediaInformationFactory.CreateMediaInformationFromUri(url, ps);
                    LoadThumbnailFromMediaInformaion(item, ffInfo);
                }
            }
            else if (useUnsupportedThumbnail)
            {
                //캐싱 이미지 로드
                LoadCachedThumbnail(item, thumbnail);
            }
        }

        protected async Task<ImageSource> GetThumbnailAsync(IMediaItemInfo item, IMediaInformation mediaInformation)
        {
            ImageSource imageSource = null;
            if (mediaInformation != null)
            {
                var buffer = await mediaInformation.GetBitmapPixelBuffer(ThumbnailSize);
                if (buffer != null)
                {
                    await DispatcherHelper.RunAsync(async () =>
                    {
                        //imageSource = await BitmapFactory.New(0, 0).FromPixelBuffer(buffer, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height);
                        imageSource = await BitmapFactory.FromPixelBuffer(buffer, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height);
                        
                        //PNG로 압축 및 DB저장
                        item.Duration = mediaInformation.NaturalDuration;
                        //썸네일 저장
                        if (item is StorageItemInfo)
                            await SaveThumbail(item as StorageItemInfo, buffer.ToArray());
                        else if (item is NetworkItemInfo)
                            await SaveThumbail(item as NetworkItemInfo, buffer.ToArray());
                        //비동기 처리 임으로 여기서 초기화 해야함.
                        mediaInformation = null;
                    });
                }
                else
                {
                    mediaInformation = null;
                }
            }
            return imageSource;
        }

        private async Task<ImageSource> GetChachedThumbnail(Thumbnail thumbnail)
        {
            ImageSource imageSource = null;
            //캐싱 이미지 로드
            ThumbnailDAO.FillThumnailData(thumbnail);
            await DispatcherHelper.RunAsync(async () =>
            {
                //PNG의 경우 스트림으로 로드해야 정상적으로 출력이됨 (WriteableBitmapEx의 FromStream 또는 BitmapDecoder 사용시 await가 걸리지 않는 버그가 있음)
                BitmapImage image = new BitmapImage();
                if (thumbnail.ThumbnailData != null)
                {
                    using (InMemoryRandomAccessStream imras = new InMemoryRandomAccessStream())
                    {
                        await imras.WriteAsync(thumbnail.ThumbnailData.AsBuffer());
                        imras.Seek(0);
                        image.SetSource(imras);
                        imageSource = image;
                    }
                }
                else
                {
                    Debugger.Break();
                    //여기 걸리면 안되는건디...
                }
            });
            return imageSource;
        }

        protected void LoadThumbnailFromMediaInformaion(IMediaItemInfo item, IMediaInformation mediaInformation)
        {
            if (mediaInformation != null)
            {
                //재생 시간 설정
                item.Duration = mediaInformation.NaturalDuration;
                mediaInformation.GetBitmapPixelBuffer(ThumbnailSize).Completed = (bufferResult, bufferStatus) =>
                {
                    if (bufferStatus == AsyncStatus.Completed)
                    {
                        var buffer = bufferResult.GetResults();
                        if (buffer != null)
                        {
                            DispatcherHelper.CheckBeginInvokeOnUI(() =>
                            {
                                WriteableBitmap wb = BitmapFactory.New(0, 0);
                                //wb.FromPixelBuffer(buffer, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height).AsAsyncOperation().Completed = async (bitmap, bitmapStatus) =>
                                BitmapFactory.FromPixelBuffer(buffer, (int)ThumbnailSize.Width, (int)ThumbnailSize.Height).AsAsyncOperation().Completed = async (bitmap, bitmapStatus) =>
                                {
                                    if (bitmapStatus == AsyncStatus.Completed)
                                    {
                                        var newWb = bitmap.GetResults();
                                        item.ImageItemsSource = newWb;
                                        //썸네일 저장
                                        if (item is StorageItemInfo)
                                            await SaveThumbail(item as StorageItemInfo, buffer.ToArray());
                                        else if (item is NetworkItemInfo)
                                            await SaveThumbail(item as NetworkItemInfo, buffer.ToArray());
                                    }
                                };
                            });
                        }
                    }
                    mediaInformation = null;
                };
            }
        }
        
        protected void LoadCachedThumbnail(IMediaItemInfo item, Thumbnail thumbnail)
        {
            //캐싱 이미지 로드
            ThumbnailDAO.FillThumnailData(thumbnail);
            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                //상영시간
                item.Duration = thumbnail.RunningTime;
                //PNG의 경우 스트림으로 로드해야 정상적으로 출력이됨
                using (MemoryStream ms = new MemoryStream(thumbnail.ThumbnailData))
                {
                    //item.ImageItemsSource = await BitmapFactory.New(0, 0).FromStream(ms);
                    item.ImageItemsSource = await BitmapFactory.FromStream(ms);
                }
            });
        }

        protected async Task SaveThumbail(StorageItemInfo file, byte[] data)
        {
            await SaveThumbail((pngPixel) =>
                new Thumbnail()
                {
                    Name = file.Name,
                    ParentPath = file.ParentFolderPath,
                    Size = file.Size,
                    RunningTime = file.Duration,
                    CreatedDateTime = file.DateCreated.DateTime,
                    ThumbnailData = pngPixel
                }, data);
        }

        protected async Task SaveThumbail(NetworkItemInfo item, byte[] data)
        {
            await SaveThumbail((pngPixel) => 
                new Thumbnail()
                {
                    Name = item.Name,
                    ParentPath = item.ParentFolderPath,
                    Size = (ulong)item.Size,
                    RunningTime = item.Duration,
                    CreatedDateTime = item.Modified,
                    ThumbnailData = pngPixel
                }, data);
        }

        protected async Task SaveThumbail(Func<byte[], Thumbnail> funcGetThumbnail, byte[] data)
        {
            using (InMemoryRandomAccessStream ras = new InMemoryRandomAccessStream())
            {
                // Encode pixels into stream 
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)ThumbnailSize.Width, (uint)ThumbnailSize.Height, 96, 96, data);
                await encoder.FlushAsync();

                byte[] pngPixel = new byte[ras.Size];
                ras.AsStreamForRead().Read(pngPixel, 0, (int)ras.Size);

                //DB 등록
                ThumbnailDAO.InsertThumbnail(funcGetThumbnail.Invoke(pngPixel));
            }
        }
    }
}
