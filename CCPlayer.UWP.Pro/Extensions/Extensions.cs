using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.UWP.Extensions
{
    public static class Extensions
    {
        #region UWP
        public static int ToInt(this bool value)
        {
            return value ? 1 : 0;
        }

        public static bool ToBool(this long value)
        {
            return value == 0 ? false : true;
        }

        public static IEnumerable<T> ToIEnumerable<T>(this IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
        #endregion
        #region IStorageItem Extensions
        
        public static bool IsVideoFile(this IStorageItem storageItem)
        {
            if (!storageItem.IsOfType(StorageItemTypes.File)) return false;

            var ext = Path.GetExtension(storageItem.Path).ToUpper();
            if (string.IsNullOrEmpty(ext)) return false;
            
            return CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX.Contains(ext);
        }

        public static bool IsSubtitleFile(this IStorageItem storageItem)
        {
            if (!storageItem.IsOfType(StorageItemTypes.File)) return false;

            var ext = Path.GetExtension(storageItem.Path).ToUpper();
            if (string.IsNullOrEmpty(ext)) return false;

            return CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(ext);
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
            if (stmt.DataType(name) == SQLitePCL.SQLiteType.INTEGER)
            {
                return stmt[name] == null ? 0 : stmt.GetInteger(name);
            }
            else if (stmt.DataType(name) == SQLitePCL.SQLiteType.FLOAT)
            {
                return stmt[name] == null ? 0 : (long)stmt.GetFloat(name);
            }
            return 0;
        }

        public static double GetFloat2(this SQLitePCL.ISQLiteStatement stmt, string name)
        {
            if (stmt.DataType(name) == SQLitePCL.SQLiteType.FLOAT)
            {
                return stmt[name] == null ? 0.0 : stmt.GetFloat(name);
            }
            else if (stmt.DataType(name) == SQLitePCL.SQLiteType.INTEGER)
            {
                return stmt[name] == null ? 0.0 : stmt.GetInteger(name);
            }
            return 0.0;
        }

        #endregion

        public static string ViewModelName(this HubSection hubSection)
        {
            return hubSection.Name.Replace("Section", "ViewModel");
        }

        #region Stream
        public static uint ReadUInt8(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            offset += 1;
            return (uint)stream.ReadByte();
        }

        public static uint ReadUInt24(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] x = new byte[4];
            stream.Read(x, 1, 3);
            offset += 3;
            return BitConverterBE.ToUInt32(x, 0);
        }

        public static uint ReadUInt32(this Stream stream, ref long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] x = new byte[4];
            stream.Read(x, 0, 4);
            offset += 4;
            return BitConverterBE.ToUInt32(x, 0);
        }

        public static byte[] ReadBytes(this Stream stream, ref long offset, int length)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] buff = new byte[length];
            stream.Read(buff, 0, length);
            offset += length;
            return buff;
        }
        #endregion
    }

    public static class BitConverterBE
    {
        public static ulong ToUInt64(byte[] value, int startIndex)
        {
            return
                ((ulong)value[startIndex] << 56) |
                ((ulong)value[startIndex + 1] << 48) |
                ((ulong)value[startIndex + 2] << 40) |
                ((ulong)value[startIndex + 3] << 32) |
                ((ulong)value[startIndex + 4] << 24) |
                ((ulong)value[startIndex + 5] << 16) |
                ((ulong)value[startIndex + 6] << 8) |
                ((ulong)value[startIndex + 7]);
        }

        public static uint ToUInt32(byte[] value, int startIndex)
        {
            return
                ((uint)value[startIndex] << 24) |
                ((uint)value[startIndex + 1] << 16) |
                ((uint)value[startIndex + 2] << 8) |
                ((uint)value[startIndex + 3]);
        }

        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            return (ushort)(
                (value[startIndex] << 8) |
                (value[startIndex + 1]));
        }

        public static byte[] GetBytes(ulong value)
        {
            byte[] buff = new byte[8];
            buff[0] = (byte)(value >> 56);
            buff[1] = (byte)(value >> 48);
            buff[2] = (byte)(value >> 40);
            buff[3] = (byte)(value >> 32);
            buff[4] = (byte)(value >> 24);
            buff[5] = (byte)(value >> 16);
            buff[6] = (byte)(value >> 8);
            buff[7] = (byte)(value);
            return buff;
        }

        public static byte[] GetBytes(uint value)
        {
            byte[] buff = new byte[4];
            buff[0] = (byte)(value >> 24);
            buff[1] = (byte)(value >> 16);
            buff[2] = (byte)(value >> 8);
            buff[3] = (byte)(value);
            return buff;
        }

        public static byte[] GetBytes(ushort value)
        {
            byte[] buff = new byte[2];
            buff[0] = (byte)(value >> 8);
            buff[1] = (byte)(value);
            return buff;
        }
    }

    //public static class BitConverterLE
    //{
    //    public static byte[] GetBytes(ulong value)
    //    {
    //        byte[] buff = new byte[8];
    //        buff[0] = (byte)(value);
    //        buff[1] = (byte)(value >> 8);
    //        buff[2] = (byte)(value >> 16);
    //        buff[3] = (byte)(value >> 24);
    //        buff[4] = (byte)(value >> 32);
    //        buff[5] = (byte)(value >> 40);
    //        buff[6] = (byte)(value >> 48);
    //        buff[7] = (byte)(value >> 56);
    //        return buff;
    //    }

    //    public static byte[] GetBytes(uint value)
    //    {
    //        byte[] buff = new byte[4];
    //        buff[0] = (byte)(value);
    //        buff[1] = (byte)(value >> 8);
    //        buff[2] = (byte)(value >> 16);
    //        buff[3] = (byte)(value >> 24);
    //        return buff;
    //    }

    //    public static byte[] GetBytes(ushort value)
    //    {
    //        byte[] buff = new byte[2];
    //        buff[0] = (byte)(value);
    //        buff[1] = (byte)(value >> 8);
    //        return buff;
    //    }
    //}
}
