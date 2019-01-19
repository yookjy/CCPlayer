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
    public class NetworkItemGroup
    {
        public string Name { get; private set; }

        public StorageItemTypes Type { get; private set; }

        public ObservableCollection<NetworkItemInfo> Items { get; set; }

        public NetworkItemGroup(StorageItemTypes type)
        {
            this.Type = type;
            this.Items = new ObservableCollection<NetworkItemInfo>();
        }
        
        public NetworkItemGroup(StorageItemTypes type, string name)
        {
            this.Type = type;
            this.Items = new ObservableCollection<NetworkItemInfo>();
            this.Name = name;
        }
    }
}
