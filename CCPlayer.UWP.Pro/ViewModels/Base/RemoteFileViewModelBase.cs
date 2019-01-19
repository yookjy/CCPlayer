using CCPlayer.UWP.Models;
using CCPlayer.UWP.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Globalization.Collation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using PropertyChanged;

namespace CCPlayer.UWP.ViewModels.Base
{
    public abstract class RemoteFileViewModelBase : FileViewModelBase
    {
        [DoNotNotify]
        public ObservableCollection<NetworkItemGroup> NetworkItemGroupSource { get; set; }

        public override bool IsStopLoadingIndicator
        {
            get { return _IsStopLoadingIndicator; }
            set
            {
                if (Set(ref _IsStopLoadingIndicator, value))
                {
                    ShowOrderBy = NetworkItemGroupSource.SelectMany(x => x.Items).Count() > 0;
                }
            }
        }

        public bool IsConnecting { get; set; }

        public bool IsDisconnected { get; set; } = true;

        public string ServerType { get; set; }

        public ServerTypes ServerTypes
        {
            get
            {
                ServerTypes st;
                Enum.TryParse<ServerTypes>(ServerType, out st);
                return st;
            }
        }

        [DoNotNotify]
        public ServerTypes ConnectedServerType { get; set; }

        protected override void OrderByChanged()
        {
            string nonGroupFileName = ResourceLoader.GetForCurrentView().GetString("List/File/Text");
            string nonGroupFolderName = ResourceLoader.GetForCurrentView().GetString("List/Folder/Text");

            var isOrderByName = _Sort == SortType.Name || _Sort == SortType.NameDescending;
            List<NetworkItemInfo> folderItems = null;
            List<NetworkItemInfo> fileItems = null;

            switch (_Sort)
            {
                case SortType.Name:
                    folderItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderBy(x => x.Name).ToList();
                    fileItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderBy(x => x.Name).ToList();
                    break;
                case SortType.NameDescending:
                    folderItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderByDescending(x => x.Name).ToList();
                    fileItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderByDescending(x => x.Name).ToList();
                    break;
                case SortType.CreatedDate:
                    folderItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderBy(x => x.Modified).ToList();
                    fileItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderBy(x => x.Modified).ToList();
                    break;
                case SortType.CreatedDateDescending:
                    folderItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.Folder).SelectMany(x => x.Items).OrderByDescending(x => x.Modified).ToList();
                    fileItems = NetworkItemGroupSource.Where(x => x.Type == StorageItemTypes.File).SelectMany(x => x.Items).OrderByDescending(x => x.Modified).ToList();
                    break;
            }

            //리스트 전체 초기화
            NetworkItemGroupSource.Clear();
            //폴더 재추가
            if (folderItems != null && folderItems.Count > 0)
            {
                NetworkItemGroup folderGroup = new NetworkItemGroup(StorageItemTypes.Folder, nonGroupFolderName);
                NetworkItemGroupSource.Add(folderGroup);

                foreach (var item in folderItems)
                {
                    item.IsOrderByName = isOrderByName;
                    folderGroup.Items.Add(item);
                }
            }
            //파일 재추가
            if (fileItems != null && fileItems.Count > 0)
            {
                NetworkItemGroup fileGroup = null;
                var fileStartIndex = NetworkItemGroupSource.Any(x => x.Type == StorageItemTypes.Folder) ? 1 : 0;

                if (isOrderByName && fileItems.Count > GROUP_MAX_ITME_COUNT)
                {
                    foreach (var item in fileItems)
                    {
                        var groupName = _CharacterGroupings.Lookup(item.Name);
                        fileGroup = NetworkItemGroupSource.FirstOrDefault(x => x.Name == groupName);

                        if (fileGroup == null)
                        {
                            fileGroup = new NetworkItemGroup(StorageItemTypes.File, groupName);
                            NetworkItemGroupSource.Add(fileGroup);
                        }

                        item.IsOrderByName = isOrderByName;
                        fileGroup.Items.Add(item);
                    }
                }
                else
                {
                    fileGroup = new NetworkItemGroup(StorageItemTypes.File, nonGroupFileName);
                    NetworkItemGroupSource.Add(fileGroup);

                    foreach (var item in fileItems)
                    {
                        item.IsOrderByName = isOrderByName;
                        fileGroup.Items.Add(item);
                    }
                }
            }
        }

        protected override void SaveOrderBySetting() { }
    }
}
