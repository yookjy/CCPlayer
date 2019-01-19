using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace CCPlayer.WP81.Models
{
    public class ItemInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            if (PropertyChanged != null)
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                });
            }
        }

        public string Path { get; set; }

        public string Name { get; set; }

        public string FalToken { get; set; }

        private string _OccuredError;
        public string OccuredError
        {
            get
            {
                return string.IsNullOrEmpty(_OccuredError) ? string.Empty : _OccuredError;
            }
            set
            {
                if (_OccuredError != value)
                {
                    _OccuredError = value;
                    OnPropertyChanged();
                }
            }
        }

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

        protected IStorageItem storageItem;

        public ItemInfo() { }

        public ItemInfo(IStorageItem storageItem)
            : base()
        {
            this.Name = storageItem.Name;
            this.Path = storageItem.Path;
            this.storageItem = storageItem;
            //FAL추겨 여부 체크
            CheckFutureAccessList(storageItem);
        }

        public void CheckFutureAccessList(IStorageItem item)
        {
            try
            {
                //1. 폴더인 경우 무조건 추가
                //2. 파일인 경우 접근 권한 체크하여 없으면 추가 
                //3. 상위폴더가 목록에 존재하면 하위 파일들은 접근권한을 갖으므로 추가되지 않는다. 
                if (item.IsOfType(StorageItemTypes.Folder) 
                    || (item.IsOfType(StorageItemTypes.File) && !StorageApplicationPermissions.FutureAccessList.CheckAccess(item)))
                {
                    this.FalToken = StorageApplicationPermissions.FutureAccessList.Add(item, this.GetType().ToString());
                }
            }
            catch (Exception) { }
        }

    }
}
