using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.Models
{
    public enum FolderType
    {
        Normal,
        Root,
        Picker,
        Upper,
        Last
    }

    public class FolderInfo : ItemInfo
    {
        public FolderType Type { get; set; }

        private string _Passcode;
        public string Passcode
        {
            get
            {
                return _Passcode;
            }
            set
            {
                _Passcode = value;
                if (string.IsNullOrEmpty(value))
                {
                    Glyph2 = "\xE1F7;";
                }
                else
                {
                    Glyph2 = "\xE1F6;";
                }
            }
        }

        public long Level { get; set; }

        public string Glyph1 { get; set; }

        private string _Glyph2;
        public string Glyph2
        {
            get
            {
                return _Glyph2;
            }
            set
            {
                if (_Glyph2 != value)
                {
                    _Glyph2 = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ButtonTappedCommand1 { get; set; }
        public ICommand ButtonTappedCommand2 { get; set; }

        private bool _IsHighlight;
        public bool IsHighlight
        {
            get
            {
                return _IsHighlight;
            }
            set
            {
                if (_IsHighlight != value)
                {
                    _IsHighlight = value;
                    OnPropertyChanged();
                }
            }
        }

        public FolderInfo() { }

        public FolderInfo(StorageFolder storageFolder)
            : base(storageFolder)
        {
        }

        public async Task<StorageFolder> GetStorageFolder(bool ignoreError)
        {
            if (Type == FolderType.Picker) return null;
            var storageFolder = storageItem as StorageFolder;

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
                catch (Exception e)
                {
                    OccuredError = ResourceLoader.GetForCurrentView().GetString("Message/Error/FolderNotFound");
                    //폴더를 불러올 수 없는 경우 무시
                    if (!ignoreError) throw e;
                }
            }

            return storageFolder;
        }
    }
}
