using CCPlayer.WP81.Strings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.Extensions
{
    public static class Extensions
    {
        #region IStorageItem Extensions

        public static bool IsVideoFile(this IStorageItem storageItem)
        {
            if (!storageItem.IsOfType(StorageItemTypes.File)) return false;

            var ext = Path.GetExtension(storageItem.Path).ToUpper();
            if (string.IsNullOrEmpty(ext)) return false;

            return CCPlayerConstant.VIDEO_FILE_SUFFIX.Contains(ext);
        }

        public static bool IsSubtitleFile(this IStorageItem storageItem)
        {
            if (!storageItem.IsOfType(StorageItemTypes.File)) return false;

            var ext = Path.GetExtension(storageItem.Path).ToUpper();
            if (string.IsNullOrEmpty(ext)) return false;

            return CCPlayerConstant.SUBTITLE_FILE_SUFFIX.Contains(ext);
        }

        public static void RecursiveFolder(this StorageFolder storageFolder, Action<IEnumerable<IStorageItem>, bool> action, TextBlock dirPath)
        {
            if (storageFolder != null)
            {
                if (!dirPath.Dispatcher.HasThreadAccess)
                {
                    var res = dirPath.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        dirPath.Text = storageFolder.Path;
                    });
                }
                else
                {
                    dirPath.Text = storageFolder.Path;
                }
                
                //파일 처리
                action.Invoke(storageFolder.GetFilesAsync().AsTask().Result, true);
                //하위 폴더 검색
                foreach (var f in storageFolder.GetFoldersAsync().AsTask().Result)
                {
                    RecursiveFolder(f, action, dirPath);
                }
            }
        }

        #endregion

        

        #region AppBarButton 

        public static void Disable(this AppBarButton btn, bool isDisable)
        {
            btn.IsEnabled = !isDisable;
            //bug fix (비활성화시 심볼아이콘이 두번 비활성화 되어 비활성화 색이 두번 겹침)
            if (btn.Content is SymbolIcon)
            {
                var si = btn.Content as SymbolIcon;
                si.Foreground = btn.Foreground;
            }
        }

        #endregion


        #region Sqlite
        
        public static string GetText2(this SQLitePCL.ISQLiteStatement stmt, string name)
        {
            return stmt[name] == null ? null : stmt.GetText(name);
        }

        public static long GetInteger2(this SQLitePCL.ISQLiteStatement stmt, string name)
        {
            return stmt[name] == null ? 0 : stmt.GetInteger(name);
        }

        #endregion

        public static string ViewModelName(this HubSection hubSection)
        {
            return hubSection.Name.Replace("Section", "ViewModel");
        }
    }
}
