using CCPlayer.UWP.Models;
using Cubisoft.Winrt.Ftp;
using DecaTec.WebDav;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.Extensions
{
    public static class DataConvertExtensions
    {
        public static NetworkItemInfo ToNetworkItemInfo(this WebDavSessionItem item)
        {
            return ToNetworkItemInfo(item, null, null, null, false);
        }

        public static NetworkItemInfo ToNetworkItemInfo(this WebDavSessionItem item, TappedEventHandler tapped, RightTappedEventHandler rightTapped, HoldingEventHandler holding, bool isOrderByName)
        {
            return new NetworkItemInfo()
            {
                ServerType = ServerTypes.WebDAV,
                ContentType = item.ContentType,
                Created = item.CreationDate.GetValueOrDefault(),
                //IsFile = !item.IsCollection && item.ContentType?.ToLower() != "httpd/unix-directory",
                IsFile = !item.IsFolder.GetValueOrDefault() || item.ContentType?.ToLower() != "httpd/unix-directory",
                Modified = item.LastModified.GetValueOrDefault(),
                Name = item.Name,
                Size = item.ContentLength.GetValueOrDefault(),
                Uri = item.Uri,
                Tapped = tapped,
                RightTapped = rightTapped,
                Holding = holding,
                IsOrderByName = isOrderByName,
            };
        }

        public static NetworkItemInfo ToNetworkItemInfo(this FtpItem item, Uri uri)
        {
            return ToNetworkItemInfo(item, uri, null, null, null, false);
        }

        public static NetworkItemInfo ToNetworkItemInfo(this FtpItem item, Uri uri, TappedEventHandler tapped, RightTappedEventHandler rightTapped, HoldingEventHandler holding, bool isOrderByName)
        {
            return new NetworkItemInfo()
            {
                ServerType = ServerTypes.FTP,
                Created = item.Created,
                IsFile = item.Type == FtpFileSystemObjectType.File,
                Modified = item.Modified,
                Name = item.Name,
                Size = item.Size,
                Uri = new Uri(uri, item.FullName),
                Tapped = tapped,
                RightTapped = rightTapped,
                Holding = holding,
                IsOrderByName = isOrderByName,
            };
        }
        
        public static NetworkItemInfo ToNetworkItemInfo(this Microsoft.OneDrive.Sdk.Item item)
        {
            return ToNetworkItemInfo(item, null, null, null, false);
        }

        public static NetworkItemInfo ToNetworkItemInfo(this Microsoft.OneDrive.Sdk.Item item, TappedEventHandler tapped, RightTappedEventHandler rightTapped, HoldingEventHandler holding, bool isOrderByName)
        {
            var networkItemInfo = new NetworkItemInfo()
            {
                Id = item.Id,
                Created = (DateTime)item.CreatedDateTime?.DateTime,
                Modified = (DateTime)item.LastModifiedDateTime?.DateTime,
                ServerType = ServerTypes.OneDrive,
                IsFile = item.File != null,
                Name = item.Name,
                Size = (long)item.Size,
                ParentFolderPath = item.ParentReference?.Path,
                Tapped = tapped,
                RightTapped = rightTapped,
                Holding = holding,
                IsOrderByName = isOrderByName,
            };
            return networkItemInfo;
        }
    }
}
