using CCPlayer.UWP.Common.Codec;
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
    public enum MediaStreamTypes
    {
        Video,
        Audio,
        Data,
        Subtitle,
        Attachment
    }

    public class StorageItemCodec
    {
        public string Name { get; private set; }

        public MediaStreamTypes Type { get; private set; }

        public ObservableCollection<CodecInformation> Items { get; set; }

        public StorageItemCodec(MediaStreamTypes type)
        {
            this.Type = type;
            this.Items = new ObservableCollection<CodecInformation>();

            string key = string.Empty;
            switch(type)
            {
                case MediaStreamTypes.Video:
                    key = "Stream/Type/Video";
                    break;
                case MediaStreamTypes.Audio:
                    key = "Stream/Type/Audio";
                    break;
                case MediaStreamTypes.Subtitle:
                    key = "Stream/Type/Subtitle";
                    break;
                case MediaStreamTypes.Attachment:
                    key = "Stream/Type/Attachment";
                    break;
            }

            if (!string.IsNullOrEmpty(key))
            {
                this.Name = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString(key);
            }
        }
    }
}
