using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace CCPlayer.UWP.Models
{
    public class FileInfo : StorageItemInfo
    {
        public FileInfo() : base() { }
        
        public FileInfo(StorageFile storageFile)
            : base(storageFile)
        {
        }

        public async Task<StorageFile> GetStorageFile(bool ignoreError)
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
                catch (Exception e)
                {
                    OccuredError = ResourceLoader.GetForCurrentView().GetString("Message/Error/FileNotFound");
                    //폴더를 불러올 수 없는 경우 무시
                    if (!ignoreError) throw e;
                }
            }

            return storageFile;
        }
    }
}
