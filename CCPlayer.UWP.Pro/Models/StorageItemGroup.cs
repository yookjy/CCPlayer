using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CCPlayer.UWP.Models
{
    public class StorageItemGroup
    {
        public string Name { get; private set; }

        public StorageItemTypes Type { get; private set; }

        public ObservableCollection<StorageItemInfo> Items { get; set; }

        public StorageItemGroup(StorageItemTypes type)
        {
            this.Type = type;
            this.Items = new ObservableCollection<StorageItemInfo>();
            //this.Name = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString(type == StorageItemTypes.File ? "List/File/Text" : "List/Folder/Text");
        }
        
        public StorageItemGroup(StorageItemTypes type, string name)
        {
            this.Type = type;
            this.Items = new ObservableCollection<StorageItemInfo>();
            this.Name = name;
        }
    }
}
