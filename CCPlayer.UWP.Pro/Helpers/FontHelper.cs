using CCPlayer.UWP.Extensions;
using CCPlayer.UWP.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.Helpers;
using Lime.Models;
using Windows.Storage;
using Windows.System.Threading;
using GalaSoft.MvvmLight.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Input;
using System.Collections.Specialized;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using CCPlayer.UWP.Models.DataAccess;

namespace CCPlayer.UWP.Helpers
{
    public enum FontTypes
    {
        All,
        OS,
        App
    }

    public class NameRecord
    {
        public ushort PlatformID { get; set; }
        public ushort EncodingID { get; set; }
        public ushort LanguageID { get; set; }
        public ushort NameID { get; set; }
        public string Name { get; set; }
    }

    public class FontPathCompare : IEqualityComparer<KeyName>
    {
        public bool Equals(KeyName x, KeyName y)
        {
            return x.Key.ToString() == y.Key.ToString();
        }

        public int GetHashCode(KeyName obj)
        {
            return this.GetHashCode();
        }
    }

    public static class FontHelper
    {
        //폰트 스펙
        //http://www.microsoft.com/typography/otspec/name.htm
        //http://www.microsoft.com/typography/otspec/otff.htm
        public const string FONT_FAMILY_DEFAUT = "Global User Interface";

        public static PropertySet GetFontName(Stream stream)
        {
            System.Globalization.CultureInfo enCI = new System.Globalization.CultureInfo("en-US");
            PropertySet namePropSet = new PropertySet();
            SortedDictionary<ushort, string> fontNameDict = new SortedDictionary<ushort, string>();
            long pos = 0;
            UInt32 ttcNumFonts = 0;
            UInt32[] ttcOffsetTables = null;
            var checkTTC = stream.ReadBytes(ref pos, 4);

            if (Encoding.UTF8.GetString(checkTTC, 0, 4) == "ttcf")
            {
                //TTC Header
                var ttcVersion = stream.ReadBytes(ref pos, 4);
                ttcNumFonts = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                ttcOffsetTables = new UInt32[ttcNumFonts];
                for (UInt32 i = 0; i < ttcNumFonts; i++)
                {
                    ttcOffsetTables[i] = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                }

                if (ttcVersion[1] == 2)
                {
                    var ulDsigTag = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                    var ulDsigLength = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0); ;
                    var ulDsigOffset = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                } 
            }
            else
            {
                ttcNumFonts = 1;
                ttcOffsetTables = new UInt32[ttcNumFonts];
                ttcOffsetTables[0] = 0;
            }

            for (UInt32 idx = 0; idx < ttcNumFonts; idx++)
            {
                pos = ttcOffsetTables[idx];

                var sfntVersion = stream.ReadBytes(ref pos, 4);
                var numTables = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                var searchRange = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                var entrySelector = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                var rangeShift = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                List<NameRecord> nameTable = new List<NameRecord>();

                while (true)
                {
                    var tag = Encoding.UTF8.GetString(stream.ReadBytes(ref pos, 4), 0, 4);
                    var checkSum = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                    var offset = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
                    var length = BitConverterBE.ToUInt32(stream.ReadBytes(ref pos, 4), 0);
              
                    //var sum = CalcTableChecksum(12, length);
                    if (tag == "name") //필수 테이블중 name만 처리하고 break
                    {
                        pos = offset;
                        ushort format = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                        ushort count = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                        ushort stringOffset = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                        //Name Record [count]
                        for (int i = 0; i < count; i++)
                        {
                            var record = new NameRecord()
                            {
                                PlatformID = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0),
                                EncodingID = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0),
                                LanguageID = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0),
                                NameID = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0)
                            };

                            //Unicode플랫폼 및 윈도우플랫폼만 처리 (다른 인코딩은 CodePage로 매핑을 할 수가 없음)
                            if (!(record.PlatformID == 0 || record.PlatformID == 3))
                            {
                                continue;
                            }

                            nameTable.Add(record);

                            ushort nameRecordLength = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                            ushort nameRecordOffset = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);

                            if ((record.PlatformID == 0 || record.PlatformID == 3) && (record.NameID == 1 || record.NameID == 2)) //폰트 패밀리 이름
                            {
                                byte[] buff = new byte[nameRecordLength];
                                stream.Position = offset + stringOffset + nameRecordOffset;
                                stream.Read(buff, 0, buff.Length);

                                string name = UnicodeEncoding.BigEndianUnicode.GetString(buff, 0, buff.Length);
                                record.Name = name.Trim();
                            }
                        }

                        if (format == 1)
                        {
                            ushort langTagCount = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                            for (int i = 0; i < langTagCount; i++)
                            {
                                ushort langTagLength = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                                ushort langTagOffset = BitConverterBE.ToUInt16(stream.ReadBytes(ref pos, 2), 0);
                            }
                        }

                        NameRecord rec = nameTable.FirstOrDefault(x =>
                        {
                            if (LcidMap.ContainsKey(x.LanguageID) && x.NameID == 1)
                            {
                                System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo(LcidMap[x.LanguageID].Key);
                                //return (ci.TwoLetterISOLanguageName == System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                                //        || ci.TwoLetterISOLanguageName == System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
                                return (ci.TwoLetterISOLanguageName != enCI.TwoLetterISOLanguageName) && !string.IsNullOrEmpty(x.Name);
                            }
                            return false;
                        });

                        if (rec == null)
                        {
                            rec = nameTable.FirstOrDefault(x => x.NameID == 1 && !string.IsNullOrEmpty(x.Name));
                        }

                        if (rec == null)
                        {
                            break;
                        }

                        string fontName = rec.Name;
                        var subNameRec = nameTable.FirstOrDefault(x => x.LanguageID == rec.LanguageID && x.NameID == 2 && x.Name.Trim().ToLower() != "regular" && x.Name.Trim().ToLower() != "untitle");
                        if (subNameRec != null)
                        {
                            fontName += " ";
                            fontName += subNameRec.Name;
                        }

                        namePropSet.Add(fontName, rec.Name);
                        //ttc의 경우 다시 루프를 돌기 때문에 초기화
                        nameTable.Clear();
                        break;
                    }

                    if (pos >= stream.Length)
                    {
                        break;
                    }
                }
            }
            return namePropSet;
        }

        private static UInt32 CalcTableChecksum(UInt32 Table, UInt32 Length)
        {
            UInt32 Sum = 0;
            UInt32 EndPtr = (UInt32)(Table + ((Length + 3) & ~3) / sizeof(UInt32));
            while (Table < EndPtr)
                Sum += Table++;
            return Sum;
        }

        public static FontDAO FontDAO { get; set; }

        //public static List<string> ReservedDeletionFontList = new List<string>();

        public static string[] IgnoreFonts = {
            "Webdings", "Wingdings", "Wingdings 2", "Wingdings 3", "MT Extra",
            "Bookshelf Symbol 7", "OCR A", "MS Outlook", "MS Reference Specialty",
            "Segoe WP Emoji", "Segoe MDL2 Assets", "Symbol", "Marlett" };      

        static object lockobj = new object();

        /// <summary>
        /// byte배열 형태의 폰트 데이터를 저장한다. (MKV 내의 폰트 등)
        /// </summary>
        /// <param name="fontsData"></param>
        /// <returns></returns>
        public static async void InstallFont(IEnumerable<KeyValuePair<string, byte[]>> fontsData, bool fireListChangedEvent)
        {
            var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("Fonts", CreationCollisionOption.OpenIfExists);
            
            await Task.Factory.StartNew(() =>
            {
                var compare = new FontPathCompare();
                Parallel.ForEach(fontsData, async (fontData) =>
                {
                    StorageFile fontFile = null;
                    
                    fontFile = await folder.CreateFileAsync(fontData.Key, Windows.Storage.CreationCollisionOption.OpenIfExists);
                    var prop = await fontFile.GetBasicPropertiesAsync();

                    if (prop.Size == 0)
                    {
                        //시뉵 폰트 파일 저장
                        await Windows.Storage.FileIO.WriteBytesAsync(fontFile, fontData.Value);
                        //System.Diagnostics.Debug.WriteLine($"폰트 저장 : {fontData.Key}");
                    }
                    else
                    {
                        //이미 폰트가 존재하는 경우, 새롭게 설치 요청이 들어오면 기존 삭제 대상폰트 리스트에서 제거 한다. (초기화)
                        FontDAO.DeleteTempFont(new string[] { fontFile.Path });
                        //System.Diagnostics.Debug.WriteLine($"폰트 이미 존재 : {fontData.Key}");
                    }


                    List<KeyName> addedList = new List<KeyName>();
                    foreach (var font in await GetFontItems(fontFile))
                    {
                        lock (lockobj)
                        {
                            if (!FontList.Contains(font, compare))
                            {
                                if (!addedList.Contains(font))
                                {
                                    addedList.Add(font);
                                }
                                FontList.Add(font);
                            }
                        }
                    }

                    if (fireListChangedEvent)
                    {
                        //이벤트 처리
                        if (FontFamilyListChanged != null && addedList.Count > 0)
                        {
                            FontFamilyListChanged(FontList, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedList));
                            //System.Diagnostics.Debug.WriteLine($"추가 이벤트 발생 : {fontData.Key}");
                        }
                    }
                });
            });
        }

        public static event System.Collections.Specialized.NotifyCollectionChangedEventHandler FontFamilyListChanged;

        /// <summary>
        /// 사용자가 입력한 폰트 파일을 저장한다. 
        /// </summary>
        /// <param name="fontFile"></param>
        /// <returns></returns>
        public static async Task InstallFont(StorageFile fontFile, ObservableCollection<KeyName> fontListSource, TappedEventHandler tapped)
        {
            var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("Fonts", CreationCollisionOption.OpenIfExists);
            try
            {
                //폰트 복사(설치)
                var copy = await fontFile.CopyAsync(folder, Path.GetFileName(fontFile.Name), NameCollisionOption.FailIfExists);
                List<KeyName> addedFontList = new List<KeyName>();

                foreach (var fontItem in await GetFontItems(copy))
                {
                    fontItem.ItemTapped = tapped;
                    if (fontListSource != null)
                    {
                        if (!fontListSource.Any(x => x.Key == fontItem.Key))
                        {
                            var index = fontListSource.IndexOf(fontListSource.LastOrDefault(x => x.Name.CompareTo(fontItem.Name) < 0));
                            fontListSource.Insert(index + 1, fontItem);
                        }
                    }

                    if (FontList != null)
                    {
                        if (!FontList.Any(x => x.Key == fontItem.Key))
                        {
                            var index = FontList.IndexOf(FontList.Where(x => x.Type == FontTypes.App.ToString()).LastOrDefault(x => x.Name.CompareTo(fontItem.Name) < 0));
                            FontList.Insert(index + 1, fontItem);
                        }
                    }

                    if (!addedFontList.Contains(fontItem))
                    {
                        addedFontList.Add(fontItem);
                    }

                    if (fontItem.Payload is StorageFile)
                    {
                        var path = (fontItem.Payload as StorageFile).Path;
                        FontDAO.DeleteTempFont(new string[] { path });
                    }
                }

                //이벤트 처리
                if (FontFamilyListChanged != null)
                {
                    FontFamilyListChanged(FontList, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedFontList));
                }
            }
            catch (Exception) { }
        }
        
        public static void RemoveFont(ObservableCollection<KeyName> fontListSource, KeyName pickerItem)
        {
            List<KeyName> removedFontList = new List<KeyName>();
            if (pickerItem.Payload is StorageFile)
            {
                var file = pickerItem.Payload as StorageFile;
                FontDAO.InsertTempFont(file.Path);
            }

            if (fontListSource != null)
            {
                //공유되는 폰트를 모두 삭제
                foreach (var font in fontListSource.Where(x => x.Payload == pickerItem.Payload).ToList())
                {
                    fontListSource.Remove(font);

                    if (!removedFontList.Contains(font))
                    {
                        removedFontList.Add(font);
                    }
                }
            }

            if (FontList != null)
            {
                foreach (var font in FontList.Where(x => x.Payload == pickerItem.Payload).ToList())
                {
                    FontList.Remove(font);

                    if (!removedFontList.Contains(font))
                    {
                        removedFontList.Add(font);
                    }
                }
            }
            
            //이벤트 처리
            if (FontFamilyListChanged != null)
            {
                FontFamilyListChanged(FontList, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedFontList));
            }
        }
        
        private static async Task<IEnumerable<KeyName>> GetFontItems(StorageFile fontFile)
        {
            using (var stream = await fontFile.OpenStreamForReadAsync())
            {
                return FontHelper.GetFontName(stream).Select(prop => new KeyName(
                            string.Format(@"ms-appdata:///Local/Fonts/{0}#{1}", Path.GetFileName(fontFile.Name), prop.Value),
                            prop.Key,
                            FontTypes.App.ToString(),
                            fontFile));
            }
        }

        public static List<KeyName> FontList { get; private set; }

        public static bool IsLoaded { get; private set; }

        public static async void LoadAllFont(Action complete)
        {
            if (FontList == null)
            {
                IsLoaded = false;
                FontList = new List<KeyName>();
            }

            //사용자 폰트
            var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync("Fonts", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            //삭제 대상 폰트
            var reservedDeletionFontList = FontDAO.GetTempFontList();

            //사용자폰트 로드
            foreach (var file in files.OrderBy(x => x.Name))
            {
                foreach (var fontItem in await GetFontItems(file))
                {
                    if (!FontList.Any(x => x.Key.ToString() == fontItem.Key.ToString()) && !reservedDeletionFontList.Any(x => x == (fontItem.Payload as StorageFile).Path))
                    {
                        FontList.Add(fontItem);
                        //System.Diagnostics.Debug.WriteLine($"모든 폰트 로드 : {fontItem.Key}");
                    }
                }
            }
            //기본값
            FontList.Add(new KeyName(FONT_FAMILY_DEFAUT, FONT_FAMILY_DEFAUT) { Type = FontTypes.OS.ToString() });
            //시스템 폰트
            IList<NativeHelper.Font> systemFontList = NativeHelper.FontList.GetSystemFontList("en-us");
            FontList.AddRange(systemFontList.Where(x => !IgnoreFonts.Contains(x.FamilyName) && !string.IsNullOrWhiteSpace(x.FamilyName))
                .Select(x => new KeyName(x.FamilyName, x.FamilyName) { Type = FontTypes.OS.ToString() }));
            IsLoaded = true;

            complete?.Invoke();
        }
        
        public static void LoadFonts(ObservableCollection<KeyName> target, FontTypes type, TappedEventHandler tapped, Action callback)
        {
            var fonts = FontHelper.FontList.Where(x => type == FontTypes.All ? true : x.Type == type.ToString());
            if (!FontHelper.IsLoaded)
            {
                ThreadPoolTimer.CreatePeriodicTimer((s) =>
                {
                    if (FontHelper.IsLoaded)
                    {
                        s.Cancel();
                        int i = 0;
                        foreach (var font in fonts)
                        {
                            DispatcherHelper.CheckBeginInvokeOnUI(() =>
                            {
                                font.ItemTapped = tapped;
                                target.Add(font);

                                if (callback != null && ++i == fonts.Count())
                                {
                                    callback();
                                }
                            });
                        }
                    }
                }, TimeSpan.FromMilliseconds(300));
            }
            else
            {
                foreach (var font in fonts)
                {
                    font.ItemTapped = tapped;
                    target.Add(font);
                    //System.Diagnostics.Debug.WriteLine($"{type} 폰트 로드 : {font.Key}");
                }

                callback?.Invoke();
            }
        }
        
        private static object lockObj = new object();
        private static Dictionary<ushort, KeyValuePair<String, ushort>> _LcidMap;

        public static Dictionary<ushort, KeyValuePair<String, ushort>> LcidMap
        {
            get
            {
                lock(lockObj)
                {
                    if (_LcidMap == null)
                    {
                        _LcidMap = new Dictionary<ushort, KeyValuePair<String, ushort>>
                        {
                            {0x0036, new KeyValuePair<string, ushort>("af", 1252)},
                            {0x0436, new KeyValuePair<string, ushort>("af-ZA", 1252)},
                            {0x001C, new KeyValuePair<string, ushort>("sq", 1250)},
                            {0x041C, new KeyValuePair<string, ushort>("sq-AL", 1250)},
                            {0x0484, new KeyValuePair<string, ushort>("gsw-FR", 1252)},
                            {0x045E, new KeyValuePair<string, ushort>("am-ET", 0)},
                            {0x0001, new KeyValuePair<string, ushort>("ar", 1256)},
                            {0x1401, new KeyValuePair<string, ushort>("ar-DZ", 1256)},
                            {0x3C01, new KeyValuePair<string, ushort>("ar-BH", 1256)},
                            {0x0C01, new KeyValuePair<string, ushort>("ar-EG", 1256)},
                            {0x0801, new KeyValuePair<string, ushort>("ar-IQ", 1256)},
                            {0x2C01, new KeyValuePair<string, ushort>("ar-JO", 1256)},
                            {0x3401, new KeyValuePair<string, ushort>("ar-KW", 1256)},
                            {0x3001, new KeyValuePair<string, ushort>("ar-LB", 1256)},
                            {0x1001, new KeyValuePair<string, ushort>("ar-LY", 1256)},
                            {0x1801, new KeyValuePair<string, ushort>("ar-MA", 1256)},
                            {0x2001, new KeyValuePair<string, ushort>("ar-OM", 1256)},
                            {0x4001, new KeyValuePair<string, ushort>("ar-QA", 1256)},
                            {0x0401, new KeyValuePair<string, ushort>("ar-SA", 1256)},
                            {0x2801, new KeyValuePair<string, ushort>("ar-SY", 1256)},
                            {0x1C01, new KeyValuePair<string, ushort>("ar-TN", 1256)},
                            {0x3801, new KeyValuePair<string, ushort>("ar-AE", 1256)},
                            {0x2401, new KeyValuePair<string, ushort>("ar-YE", 1256)},
                            {0x002B, new KeyValuePair<string, ushort>("hy", 0)},
                            {0x042B, new KeyValuePair<string, ushort>("hy-AM", 0)},
                            {0x044D, new KeyValuePair<string, ushort>("as-IN", 0)},
                            {0x002C, new KeyValuePair<string, ushort>("az", 1254)},
                            {0x082C, new KeyValuePair<string, ushort>("az-Cyrl-AZ", 1251)},
                            {0x042C, new KeyValuePair<string, ushort>("az-Latn-AZ", 1254)},
                            {0x046D, new KeyValuePair<string, ushort>("ba-RU", 1251)},
                            {0x002D, new KeyValuePair<string, ushort>("eu", 1252)},
                            {0x042D, new KeyValuePair<string, ushort>("eu-ES", 1252)},
                            {0x0023, new KeyValuePair<string, ushort>("be", 1251)},
                            {0x0423, new KeyValuePair<string, ushort>("be-BY", 1251)},
                            {0x0845, new KeyValuePair<string, ushort>("bn-BD", 0)},
                            {0x0445, new KeyValuePair<string, ushort>("bn-IN", 0)},
                            {0x201A, new KeyValuePair<string, ushort>("bs-Cyrl-BA", 1251)},
                            {0x141A, new KeyValuePair<string, ushort>("bs-Latn-BA", 1250)},
                            {0x047E, new KeyValuePair<string, ushort>("br-FR", 1252)},
                            {0x0002, new KeyValuePair<string, ushort>("bg", 1251)},
                            {0x0402, new KeyValuePair<string, ushort>("bg-BG", 1251)},
                            {0x0003, new KeyValuePair<string, ushort>("ca", 1252)},
                            {0x0403, new KeyValuePair<string, ushort>("ca-ES", 1252)},
                            {0x0C04, new KeyValuePair<string, ushort>("zh-HK", 950)},
                            {0x1404, new KeyValuePair<string, ushort>("zh-MO", 950)},
                            {0x0804, new KeyValuePair<string, ushort>("zh-CN", 936)},
                            {0x0004, new KeyValuePair<string, ushort>("zh-Hans", 936)},
                            {0x1004, new KeyValuePair<string, ushort>("zh-SG", 936)},
                            {0x0404, new KeyValuePair<string, ushort>("zh-TW", 950)},
                            {0x7C04, new KeyValuePair<string, ushort>("zh-Hant", 950)},
                            {0x0483, new KeyValuePair<string, ushort>("co-FR", 1252)},
                            {0x001A, new KeyValuePair<string, ushort>("hr", 1250)},
                            {0x041A, new KeyValuePair<string, ushort>("hr-HR", 1250)},
                            {0x101A, new KeyValuePair<string, ushort>("hr-BA", 1250)},
                            {0x0005, new KeyValuePair<string, ushort>("cs", 1250)},
                            {0x0405, new KeyValuePair<string, ushort>("cs-CZ", 1250)},
                            {0x0006, new KeyValuePair<string, ushort>("da", 1252)},
                            {0x0406, new KeyValuePair<string, ushort>("da-DK", 1252)},
                            {0x048C, new KeyValuePair<string, ushort>("prs-AF", 1256)},
                            {0x0065, new KeyValuePair<string, ushort>("div", 0)},
                            {0x0465, new KeyValuePair<string, ushort>("div-MV", 0)},
                            {0x0013, new KeyValuePair<string, ushort>("nl", 1252)},
                            {0x0813, new KeyValuePair<string, ushort>("nl-BE", 1252)},
                            {0x0413, new KeyValuePair<string, ushort>("nl-NL", 1252)},
                            {0x0009, new KeyValuePair<string, ushort>("en", 1252)},
                            {0x0C09, new KeyValuePair<string, ushort>("en-AU", 1252)},
                            {0x2809, new KeyValuePair<string, ushort>("en-BZ", 1252)},
                            {0x1009, new KeyValuePair<string, ushort>("en-CA", 1252)},
                            {0x2409, new KeyValuePair<string, ushort>("en-029", 1252)},
                            {0x4009, new KeyValuePair<string, ushort>("en-IN", 1252)},
                            {0x1809, new KeyValuePair<string, ushort>("en-IE", 1252)},
                            {0x2009, new KeyValuePair<string, ushort>("en-JM", 1252)},
                            {0x4409, new KeyValuePair<string, ushort>("en-MY", 1252)},
                            {0x1409, new KeyValuePair<string, ushort>("en-NZ", 1252)},
                            {0x3409, new KeyValuePair<string, ushort>("en-PH", 1252)},
                            {0x4809, new KeyValuePair<string, ushort>("en-SG", 1252)},
                            {0x1C09, new KeyValuePair<string, ushort>("en-ZA", 1252)},
                            {0x2C09, new KeyValuePair<string, ushort>("en-TT", 1252)},
                            {0x0809, new KeyValuePair<string, ushort>("en-GB", 1252)},
                            {0x0409, new KeyValuePair<string, ushort>("en-US", 1252)},
                            {0x3009, new KeyValuePair<string, ushort>("en-ZW", 1252)},
                            {0x0025, new KeyValuePair<string, ushort>("et", 1257)},
                            {0x0425, new KeyValuePair<string, ushort>("et-EE", 1257)},
                            {0x0038, new KeyValuePair<string, ushort>("fo", 1252)},
                            {0x0438, new KeyValuePair<string, ushort>("fo-FO", 1252)},
                            {0x0464, new KeyValuePair<string, ushort>("fil-PH", 1252)},
                            {0x000B, new KeyValuePair<string, ushort>("fi", 1252)},
                            {0x040B, new KeyValuePair<string, ushort>("fi-FI", 1252)},
                            {0x000C, new KeyValuePair<string, ushort>("fr", 1252)},
                            {0x080C, new KeyValuePair<string, ushort>("fr-BE", 1252)},
                            {0x0C0C, new KeyValuePair<string, ushort>("fr-CA", 1252)},
                            {0x040C, new KeyValuePair<string, ushort>("fr-FR", 1252)},
                            {0x140C, new KeyValuePair<string, ushort>("fr-LU", 1252)},
                            {0x180C, new KeyValuePair<string, ushort>("fr-MC", 1252)},
                            {0x100C, new KeyValuePair<string, ushort>("fr-CH", 1252)},
                            {0x0462, new KeyValuePair<string, ushort>("fy-NL", 1252)},
                            {0x0056, new KeyValuePair<string, ushort>("gl", 1252)},
                            {0x0456, new KeyValuePair<string, ushort>("gl-ES", 1252)},
                            {0x0037, new KeyValuePair<string, ushort>("ka", 0)},
                            {0x0437, new KeyValuePair<string, ushort>("ka-GE", 0)},
                            {0x0007, new KeyValuePair<string, ushort>("de", 1252)},
                            {0x0C07, new KeyValuePair<string, ushort>("de-AT", 1252)},
                            {0x0407, new KeyValuePair<string, ushort>("de-DE", 1252)},
                            {0x1407, new KeyValuePair<string, ushort>("de-LI", 1252)},
                            {0x1007, new KeyValuePair<string, ushort>("de-LU", 1252)},
                            {0x0807, new KeyValuePair<string, ushort>("de-CH", 1252)},
                            {0x0008, new KeyValuePair<string, ushort>("el", 1253)},
                            {0x0408, new KeyValuePair<string, ushort>("el-GR", 1253)},
                            {0x046F, new KeyValuePair<string, ushort>("kl-GL", 1252)},
                            {0x0047, new KeyValuePair<string, ushort>("gu", 0)},
                            {0x0447, new KeyValuePair<string, ushort>("gu-IN", 0)},
                            {0x0468, new KeyValuePair<string, ushort>("ha-Latn-NG", 1252)},
                            {0x000D, new KeyValuePair<string, ushort>("he", 1255)},
                            {0x040D, new KeyValuePair<string, ushort>("he-IL", 1255)},
                            {0x0039, new KeyValuePair<string, ushort>("hi", 0)},
                            {0x0439, new KeyValuePair<string, ushort>("hi-IN", 0)},
                            {0x000E, new KeyValuePair<string, ushort>("hu", 1250)},
                            {0x040E, new KeyValuePair<string, ushort>("hu-HU", 1250)},
                            {0x000F, new KeyValuePair<string, ushort>("is", 1252)},
                            {0x040F, new KeyValuePair<string, ushort>("is-IS", 1252)},
                            {0x0470, new KeyValuePair<string, ushort>("ig-NG", 1252)},
                            {0x0021, new KeyValuePair<string, ushort>("id", 1252)},
                            {0x0421, new KeyValuePair<string, ushort>("id-ID", 1252)},
                            {0x085D, new KeyValuePair<string, ushort>("iu-Latn-CA", 1252)},
                            {0x045D, new KeyValuePair<string, ushort>("iu-Cans-CA", 0)},
                            {0x083C, new KeyValuePair<string, ushort>("ga-IE", 1252)},
                            {0x0434, new KeyValuePair<string, ushort>("xh-ZA", 1252)},
                            {0x0435, new KeyValuePair<string, ushort>("zu-ZA", 1252)},
                            {0x0010, new KeyValuePair<string, ushort>("it", 1252)},
                            {0x0410, new KeyValuePair<string, ushort>("it-IT", 1252)},
                            {0x0810, new KeyValuePair<string, ushort>("it-CH", 1252)},
                            {0x0011, new KeyValuePair<string, ushort>("ja", 932)},
                            {0x0411, new KeyValuePair<string, ushort>("ja-JP", 932)},
                            {0x004B, new KeyValuePair<string, ushort>("kn", 0)},
                            {0x044B, new KeyValuePair<string, ushort>("kn-IN", 0)},
                            {0x003F, new KeyValuePair<string, ushort>("kk", 1251)},
                            {0x043F, new KeyValuePair<string, ushort>("kk-KZ", 1251)},
                            {0x0453, new KeyValuePair<string, ushort>("km-KH", 0)},
                            {0x0486, new KeyValuePair<string, ushort>("qut-GT", 1252)},
                            {0x0487, new KeyValuePair<string, ushort>("rw-RW", 1252)},
                            {0x0041, new KeyValuePair<string, ushort>("sw", 1252)},
                            {0x0441, new KeyValuePair<string, ushort>("sw-KE", 1252)},
                            {0x0057, new KeyValuePair<string, ushort>("kok", 0)},
                            {0x0457, new KeyValuePair<string, ushort>("kok-IN", 0)},
                            {0x0012, new KeyValuePair<string, ushort>("ko", 949)},
                            {0x0412, new KeyValuePair<string, ushort>("ko-KR", 949)},
                            {0x0040, new KeyValuePair<string, ushort>("ky", 1251)},
                            {0x0440, new KeyValuePair<string, ushort>("ky-KG", 1251)},
                            {0x0454, new KeyValuePair<string, ushort>("lo-LA", 0)},
                            {0x0026, new KeyValuePair<string, ushort>("lv", 1257)},
                            {0x0426, new KeyValuePair<string, ushort>("lv-LV", 1257)},
                            {0x0027, new KeyValuePair<string, ushort>("lt", 1257)},
                            {0x0427, new KeyValuePair<string, ushort>("lt-LT", 1257)},
                            {0x082E, new KeyValuePair<string, ushort>("wee-DE", 1252)},
                            {0x046E, new KeyValuePair<string, ushort>("lb-LU", 1252)},
                            {0x002F, new KeyValuePair<string, ushort>("mk", 1251)},
                            {0x042F, new KeyValuePair<string, ushort>("mk-MK", 1251)},
                            {0x003E, new KeyValuePair<string, ushort>("ms", 1252)},
                            {0x083E, new KeyValuePair<string, ushort>("ms-BN", 1252)},
                            {0x043E, new KeyValuePair<string, ushort>("ms-MY", 1252)},
                            {0x044C, new KeyValuePair<string, ushort>("ml-IN", 0)},
                            {0x043A, new KeyValuePair<string, ushort>("mt-MT", 0)},
                            {0x0481, new KeyValuePair<string, ushort>("mi-NZ", 0)},
                            {0x047A, new KeyValuePair<string, ushort>("arn-CL", 1252)},
                            {0x004E, new KeyValuePair<string, ushort>("mr", 0)},
                            {0x044E, new KeyValuePair<string, ushort>("mr-IN", 0)},
                            {0x047C, new KeyValuePair<string, ushort>("moh-CA", 1252)},
                            {0x0050, new KeyValuePair<string, ushort>("mn", 1251)},
                            {0x0450, new KeyValuePair<string, ushort>("mn-MN", 1251)},
                            {0x0850, new KeyValuePair<string, ushort>("mn-Mong-CN", 0)},
                            {0x0461, new KeyValuePair<string, ushort>("ne-NP", 0)},
                            {0x0014, new KeyValuePair<string, ushort>("no", 1252)},
                            {0x0414, new KeyValuePair<string, ushort>("nb-NO", 1252)},
                            {0x0814, new KeyValuePair<string, ushort>("nn-NO", 1252)},
                            {0x0482, new KeyValuePair<string, ushort>("oc-FR", 1252)},
                            {0x0448, new KeyValuePair<string, ushort>("or-IN", 0)},
                            {0x0463, new KeyValuePair<string, ushort>("ps-AF", 0)},
                            {0x0029, new KeyValuePair<string, ushort>("fa", 1256)},
                            {0x0429, new KeyValuePair<string, ushort>("fa-IR", 1256)},
                            {0x0015, new KeyValuePair<string, ushort>("pl", 1250)},
                            {0x0415, new KeyValuePair<string, ushort>("pl-PL", 1250)},
                            {0x0016, new KeyValuePair<string, ushort>("pt", 1252)},
                            {0x0416, new KeyValuePair<string, ushort>("pt-BR", 1252)},
                            {0x0816, new KeyValuePair<string, ushort>("pt-PT", 1252)},
                            {0x0046, new KeyValuePair<string, ushort>("pa", 0)},
                            {0x0446, new KeyValuePair<string, ushort>("pa-IN", 0)},
                            {0x046B, new KeyValuePair<string, ushort>("quz-BO", 1252)},
                            {0x086B, new KeyValuePair<string, ushort>("quz-EC", 1252)},
                            {0x0C6B, new KeyValuePair<string, ushort>("quz-PE", 1252)},
                            {0x0018, new KeyValuePair<string, ushort>("ro", 1250)},
                            {0x0418, new KeyValuePair<string, ushort>("ro-RO", 1250)},
                            {0x0417, new KeyValuePair<string, ushort>("rm-CH", 1252)},
                            {0x0019, new KeyValuePair<string, ushort>("ru", 1251)},
                            {0x0419, new KeyValuePair<string, ushort>("ru-RU", 1251)},
                            {0x243B, new KeyValuePair<string, ushort>("smn-FI", 1252)},
                            {0x103B, new KeyValuePair<string, ushort>("smj-NO", 1252)},
                            {0x143B, new KeyValuePair<string, ushort>("smj-SE", 1252)},
                            {0x0C3B, new KeyValuePair<string, ushort>("se-FI", 1252)},
                            {0x043B, new KeyValuePair<string, ushort>("se-NO", 1252)},
                            {0x083B, new KeyValuePair<string, ushort>("se-SE", 1252)},
                            {0x203B, new KeyValuePair<string, ushort>("sms-FI", 1252)},
                            {0x183B, new KeyValuePair<string, ushort>("sma-NO", 1252)},
                            {0x1C3B, new KeyValuePair<string, ushort>("sma-SE", 1252)},
                            {0x004F, new KeyValuePair<string, ushort>("sa", 0)},
                            {0x044F, new KeyValuePair<string, ushort>("sa-IN", 0)},
                            {0x7C1A, new KeyValuePair<string, ushort>("sr", 1251)},
                            {0x1C1A, new KeyValuePair<string, ushort>("sr-Cyrl-BA", 1251)},
                            {0x0C1A, new KeyValuePair<string, ushort>("sr-Cyrl-SP", 1251)},
                            {0x181A, new KeyValuePair<string, ushort>("sr-Latn-BA", 1250)},
                            {0x081A, new KeyValuePair<string, ushort>("sr-Latn-SP", 1250)},
                            {0x046C, new KeyValuePair<string, ushort>("nso-ZA", 1252)},
                            {0x0432, new KeyValuePair<string, ushort>("tn-ZA", 1252)},
                            {0x045B, new KeyValuePair<string, ushort>("si-LK", 0)},
                            {0x001B, new KeyValuePair<string, ushort>("sk", 1250)},
                            {0x041B, new KeyValuePair<string, ushort>("sk-SK", 1250)},
                            {0x0024, new KeyValuePair<string, ushort>("sl", 1250)},
                            {0x0424, new KeyValuePair<string, ushort>("sl-SI", 1250)},
                            {0x000A, new KeyValuePair<string, ushort>("es", 1252)},
                            {0x2C0A, new KeyValuePair<string, ushort>("es-AR", 1252)},
                            {0x400A, new KeyValuePair<string, ushort>("es-BO", 1252)},
                            {0x340A, new KeyValuePair<string, ushort>("es-CL", 1252)},
                            {0x240A, new KeyValuePair<string, ushort>("es-CO", 1252)},
                            {0x140A, new KeyValuePair<string, ushort>("es-CR", 1252)},
                            {0x1C0A, new KeyValuePair<string, ushort>("es-DO", 1252)},
                            {0x300A, new KeyValuePair<string, ushort>("es-EC", 1252)},
                            {0x440A, new KeyValuePair<string, ushort>("es-SV", 1252)},
                            {0x100A, new KeyValuePair<string, ushort>("es-GT", 1252)},
                            {0x480A, new KeyValuePair<string, ushort>("es-HN", 1252)},
                            {0x080A, new KeyValuePair<string, ushort>("es-MX", 1252)},
                            {0x4C0A, new KeyValuePair<string, ushort>("es-NI", 1252)},
                            {0x180A, new KeyValuePair<string, ushort>("es-PA", 1252)},
                            {0x3C0A, new KeyValuePair<string, ushort>("es-PY", 1252)},
                            {0x280A, new KeyValuePair<string, ushort>("es-PE", 1252)},
                            {0x500A, new KeyValuePair<string, ushort>("es-PR", 1252)},
                            {0x0C0A, new KeyValuePair<string, ushort>("es-ES", 1252)},
                            {0x540A, new KeyValuePair<string, ushort>("es-US", 1252)},
                            {0x380A, new KeyValuePair<string, ushort>("es-UY", 1252)},
                            {0x200A, new KeyValuePair<string, ushort>("es-VE", 1252)},
                            {0x001D, new KeyValuePair<string, ushort>("sv", 1252)},
                            {0x081D, new KeyValuePair<string, ushort>("sv-FI", 1252)},
                            {0x041D, new KeyValuePair<string, ushort>("sv-SE", 1252)},
                            {0x005A, new KeyValuePair<string, ushort>("syr", 0)},
                            {0x045A, new KeyValuePair<string, ushort>("syr-SY", 0)},
                            {0x0428, new KeyValuePair<string, ushort>("tg-Cyrl-TJ", 1251)},
                            {0x085F, new KeyValuePair<string, ushort>("tzm-Latn-DZ", 1252)},
                            {0x0049, new KeyValuePair<string, ushort>("ta", 0)},
                            {0x0449, new KeyValuePair<string, ushort>("ta-IN", 0)},
                            {0x0044, new KeyValuePair<string, ushort>("tt", 1251)},
                            {0x0444, new KeyValuePair<string, ushort>("tt-RU", 1251)},
                            {0x004A, new KeyValuePair<string, ushort>("te", 0)},
                            {0x044A, new KeyValuePair<string, ushort>("te-IN", 0)},
                            {0x001E, new KeyValuePair<string, ushort>("th", 874)},
                            {0x041E, new KeyValuePair<string, ushort>("th-TH", 874)},
                            {0x0451, new KeyValuePair<string, ushort>("bo-CN", 0)},
                            {0x001F, new KeyValuePair<string, ushort>("tr", 1254)},
                            {0x041F, new KeyValuePair<string, ushort>("tr-TR", 1254)},
                            {0x0442, new KeyValuePair<string, ushort>("tk-TM", 1250)},
                            {0x0480, new KeyValuePair<string, ushort>("ug-CN", 1256)},
                            {0x0022, new KeyValuePair<string, ushort>("uk", 1251)},
                            {0x0422, new KeyValuePair<string, ushort>("uk-UA", 1251)},
                            {0x042E, new KeyValuePair<string, ushort>("wen-DE", 1252)},
                            {0x0020, new KeyValuePair<string, ushort>("ur", 1256)},
                            {0x0420, new KeyValuePair<string, ushort>("ur-PK", 1256)},
                            {0x0043, new KeyValuePair<string, ushort>("uz", 1254)},
                            {0x0843, new KeyValuePair<string, ushort>("uz-Cyrl-UZ", 1251)},
                            {0x0443, new KeyValuePair<string, ushort>("uz-Latn-UZ", 1254)},
                            {0x002A, new KeyValuePair<string, ushort>("vi", 1258)},
                            {0x042A, new KeyValuePair<string, ushort>("vi-VN", 1258)},
                            {0x0452, new KeyValuePair<string, ushort>("cy-GB", 1252)},
                            {0x0488, new KeyValuePair<string, ushort>("wo-SN", 1252)},
                            {0x0485, new KeyValuePair<string, ushort>("sah-RU", 1251)},
                            {0x0478, new KeyValuePair<string, ushort>("ii-CN", 0)},
                            {0x046A, new KeyValuePair<string, ushort>("yo-NG", 1252)}
                        };
                    }
                }
                return _LcidMap;
            }
        }
    }
}
