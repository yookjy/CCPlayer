using CCPlayer.WP81.Extensions;
using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Managers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Strings;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Runtime.InteropServices.WindowsRuntime;

namespace CCPlayer.WP81.ViewModel
{
    public class ExplorerViewModel : ViewModelBase, IFolderPickerContinuable
    {
        public static readonly string NAME = typeof(ExplorerViewModel).Name;

        #region 멤버 필드
        public Settings.GeneralSetting GeneralSetting { get; private set; }
        private FolderDAO folderDAO;
        private FileDAO fileDAO;
        private CancellationTokenSource cancelTokenSource;
        private DateTime? lastBackKeyDttm;
        private FolderInfo currentFolderInfo;
        private List<FolderInfo> protectedFolderList;

        public ObservableCollection<FolderInfo> ExplorerFolderSource { get; set; }
        public ObservableCollection<MediaInfo> ExplorerFileSource { get; set; }
        
        public MediaInfo SelectedFileItem { get; set; }

        private ListViewSelectionMode _SelectionMode;
        public ListViewSelectionMode SelectionMode 
        {
            get
            {
                return _SelectionMode;
            }
            set
            {
                Set(ref _SelectionMode, value);
            }
        }

        private bool _ButtonGroupVisible;
        public bool ButtonGroupVisible
        {
            get
            {
                return _ButtonGroupVisible;
            }
            set
            {
                Set(ref _ButtonGroupVisible, value);
            }
        }

        private bool _CheckListButtonEnable;
        public bool CheckListButtonEnable
        {
            get
            {
                return _CheckListButtonEnable;
            }
            set
            {
                Set(ref _CheckListButtonEnable, value);
            }
        }

        private bool _PlayButtonEnable;
        public bool PlayButtonEnable
        {
            get
            {
                return _PlayButtonEnable;
            }
            set
            {
                Set(ref _PlayButtonEnable, value);
            }
        }

        #endregion

        #region 커맨드 선언

        public ICommand ExplorerFolderLoadedCommand { get; private set; }
        public ICommand ExplorerFileLoadedCommand { get; private set; }
        public ICommand ExplorerFileSelectionChangedCommand { get; private set; }
        public ICommand FileClickCommand { get; private set; }
        public ICommand FolderClickCommand { get; private set; }
        public ICommand RemoveFolderCommand { get; private set; }
        public ICommand LockFolderCommand { get; private set; }
        public ICommand CheckListButtonClickCommand { get; private set; }
        public ICommand SynchronizeButtonClickCommand { get; private set; }
        public ICommand BackButtonClickCommand { get; set; }
        public ICommand SelectAllButtonClickCommand { get; set; }
        public ICommand PlayButtonClickCommand { get; set; }
        
        #endregion

        public ExplorerViewModel(FolderDAO folderDAO, FileDAO fileDAO, SettingDAO settingDAO)
        {
            this.folderDAO = folderDAO;
            this.fileDAO = fileDAO;
            this.GeneralSetting = settingDAO.SettingCache.General;

            this.CreateModels();
            this.CreateCommands();
            this.RegisterMessages();
        }

        private void CreateModels()
        {
            ExplorerFolderSource = new ObservableCollection<FolderInfo>();
            ExplorerFileSource = new ObservableCollection<MediaInfo>();

            ButtonGroupVisible = true;
        } 

        private void CreateCommands()
        {
            ExplorerFileSelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(ExplorerFileSelectionChangedCommandExecute);
            FileClickCommand = new RelayCommand<ItemClickEventArgs>(FileClickCommandExecute);
            FolderClickCommand = new RelayCommand<ItemClickEventArgs>(FolderClickCommandExecute);
            RemoveFolderCommand = new RelayCommand<FolderInfo>(RemoveFolderCommandExecute);
            LockFolderCommand = new RelayCommand<FolderInfo>(LockFolderCommandExecute);

            CheckListButtonClickCommand = new RelayCommand(CheckListButtonClickCommandExecute);
            SynchronizeButtonClickCommand = new RelayCommand(SynchronizeButtonClickCommandExecute);
            BackButtonClickCommand = new RelayCommand(BackButtonClickCommandExecute);
            SelectAllButtonClickCommand = new RelayCommand<ListView>(SelectAllButtonClickCommandExecute);
            PlayButtonClickCommand = new RelayCommand<ListView>(PlayButtonClickCommandExecute);
        }

        private void OnActivated()
        {
            if (ExplorerFileSource.Count == 0)
            {
                IAsyncAction act = ThreadPool.RunAsync(async handler =>
                {
                    if (currentFolderInfo == null)
                    {
                        //보호되는 폴더목록 로드
                        protectedFolderList = folderDAO.GetProtectedRootFolderList();
                        //마지막 폴더 로드
                        currentFolderInfo = folderDAO.GetLastFolder();

                        if (protectedFolderList.Any(x => currentFolderInfo.Path.Contains(x.Path)))
                        {
                            ContentDialog contentDlg = null;
                            PasswordBox pbox = null;
                            FolderInfo protectedFolderInfo = protectedFolderList.FirstOrDefault(x => currentFolderInfo.Path.Contains(x.Path));
                            await DispatcherHelper.RunAsync(() =>
                            {
                                //메세지 창생성
                                GetPasswordConfirmDialog(protectedFolderInfo, out contentDlg, out pbox);
                                //메세지 창 출력
                                App.ContentDlgOp = contentDlg.ShowAsync();
                                //후처리기 등록
                                App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>(async (op, status) =>
                                {
                                    var result = await op;
                                    if (result == ContentDialogResult.Primary)
                                    {
                                        if (protectedFolderInfo.Passcode == pbox.Password)
                                        {
                                            //자식 로드
                                            LoadChildFolder(currentFolderInfo);
                                        }
                                    }
                                    else
                                    {
                                        //루트 로드
                                        LoadChildFolder(null);
                                    }
                                    App.ContentDlgOp = null;
                                });
                            });
                        }
                        else
                        {
                            //자식 로드
                            LoadChildFolder(currentFolderInfo);
                        }
                    }
                });
//                System.Diagnostics.Debug.WriteLine("폴더 조회 요청 완료");
            }
        }

        private void RegisterMessages()
        {
            MessengerInstance.Register<Message>(this, NAME, msg =>
            {
                switch (msg.Key)
                {
                    case "Activated":
                        OnActivated();
                        RaisePropertyChanged("GeneralSetting");
                        break;
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        if (SelectionMode != ListViewSelectionMode.None)
                        {
                            //선택 모드 변경
                            SelectionMode = ListViewSelectionMode.None;
                            ButtonGroupVisible = true;
                        }
                        else
                        {
                            DateTime now = DateTime.Now;
                            if (GeneralSetting.HardwareBackButtonAction == HardwareBackButtonAction.MoveToUpperFolder.ToString()
                                && ExplorerFolderSource.Any(x => x.Type == FolderType.Upper)
                                && (lastBackKeyDttm == null || now.Subtract((DateTime)lastBackKeyDttm).TotalMilliseconds > 300))
                            {
                                    //완벽할 수는 없으나... 입력시간을 기간 300ms 안에 들어오는것은 버림으로 UI와 ItemSource 동기화 시간을 조금이나마 번다.
                                    //상위 폴더로 이동
                                    ToUpperFolder(msg.GetValue<BackPressedEventArgs>());
                                    lastBackKeyDttm = now;
                            }
                            else
                            {
                                //종료 확인
                                MessengerInstance.Send<Message>(new Message("ConfirmTermination", null), MainViewModel.NAME);
                            }
                        }
                        break;
                    case "ShowErrorFile":
                        if (ExplorerFileSource.Any())
                        {
                            var kv = msg.GetValue<KeyValuePair<string, MediaInfo>>();
                            var mi = ExplorerFileSource.FirstOrDefault(f => f.Path == kv.Value.Path);
                            if (mi != null)
                            {
                                mi.OccuredError = kv.Key + "\n";
                            }
                        }
                        break;
                }
            });
        }

        #region 커맨드 핸들러

        void ExplorerFileSelectionChangedCommandExecute(SelectionChangedEventArgs e)
        {
            if (SelectionMode == ListViewSelectionMode.Multiple)
            {
                //PlayButtonEnable = e.AddedItems != null && e.AddedItems.Count > 0;
                PlayButtonEnable = SelectedFileItem != null;
            }
        }

        void FileClickCommandExecute(ItemClickEventArgs e)
        {
            var playItem = e.ClickedItem as MediaInfo;
            if (string.IsNullOrEmpty(playItem.OccuredError))
            {
                var loader = ResourceLoader.GetForCurrentView();
                var msg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
                //로딩 패널 표시
                MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);

                if (protectedFolderList.Any(x => playItem.Path.Contains(x.Path)))
                {
                    //재생 처리
                    MessengerInstance.Send<Message>(new Message("Play", playItem), CCPlayerViewModel.NAME);
                }
                else
                {
                    //재생리스트 추가표시
                    playItem.IsAddedPlaylist = true;

                    var mediaInfoList = new MediaInfo[] { playItem };
                    //미디어 파일과 자막을 등록
                    InsertMediaInfo(mediaInfoList);

                    //재생 목록 생성및 재생 요청
                    MessengerInstance.Send<Message>(new Message("PlayList", mediaInfoList), PlaylistViewModel.NAME);
                }
            }
        }

        private void GetPasswordConfirmDialog(FolderInfo folderInfo, out ContentDialog dlg, out PasswordBox pwd)
        {
            StackPanel contentPanel = new StackPanel();
            contentPanel.Children.Add(new TextBlock
            {
                Text = ResourceLoader.GetForCurrentView().GetString("Message/Confirm/Input/Password"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 12)
            });

            var pbox = new PasswordBox
            {
                MaxLength = 8,
                Margin = new Thickness(0, 12, 0, 12)
            };

            contentPanel.Children.Add(pbox);

            ContentDialog contentDlg = new ContentDialog
            {
                Content = contentPanel,
                IsPrimaryButtonEnabled = false,
                PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("Ok"),
                SecondaryButtonText = ResourceLoader.GetForCurrentView().GetString("Cancel")
            };

            pbox.PasswordChanged += (sender, args) =>
            {
                contentDlg.IsPrimaryButtonEnabled = folderInfo.Passcode == pbox.Password;
            };

            dlg = contentDlg;
            pwd = pbox;
        }

        void FolderClickCommandExecute(ItemClickEventArgs e)
        {
            var folderInfo = e.ClickedItem as FolderInfo;

            //파일 선택 모드 초기화
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;

            if (folderInfo.Type == FolderType.Picker)
            {
                var folderPicker = new FolderPicker();
                folderPicker.ContinuationData.Add(CCPlayer.WP81.Managers.ContinuationManager.SOURCE_VIEW_MODEL_TYPE_FULL_NAME, this.GetType().FullName);
                folderPicker.PickFolderAndContinue();
            }
            else
            {
                if (!string.IsNullOrEmpty(folderInfo.Passcode))
                {
                    ContentDialog contentDlg = null;
                    PasswordBox pbox = null;
                    GetPasswordConfirmDialog(folderInfo, out contentDlg, out pbox);

                    //메세지 창 출력
                    App.ContentDlgOp = contentDlg.ShowAsync();

                    //후처리기 등록
                    App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>(async (op, status) =>
                    {
                        var result = await op;
                        if (result == ContentDialogResult.Primary)
                        {
                            if (folderInfo.Passcode == pbox.Password)
                            {
                                //선택된 파일 정보
                                currentFolderInfo = folderInfo;
                                //폴더 Dive
                                LoadFilesInFolder(currentFolderInfo);
                            }
                        }
                        App.ContentDlgOp = null;
                    });
                }
                else
                {
                    //선택된 파일 정보
                    currentFolderInfo = folderInfo;
                    //폴더 Dive
                    LoadFilesInFolder(currentFolderInfo);
                }
            }
        }

        async void RemoveFolderCommandExecute(FolderInfo folderInfo)
        {
            //Db에서 삭제
            await ThreadPool.RunAsync(handler =>
            {
                var result = folderDAO.Delete(folderInfo);
                //화면에서 삭제
                if (result == SQLitePCL.SQLiteResult.DONE)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() => 
                    { 
                        ExplorerFolderSource.Remove(folderInfo);
                        //애드 폴더 갱신
                        var addFolder = ExplorerFolderSource.FirstOrDefault(x => x.Type == FolderType.Picker);
                        if (ExplorerFolderSource.Count == 1 && addFolder != null)
                        {
                            addFolder.IsHighlight = true;
                        }

                        //전체 비디오에서 삭제 요청
                        MessengerInstance.Send<Message>(new Message("FolderDeleted", folderInfo), AllVideoViewModel.NAME);
                        //재생 목록에서 삭제 요청
                        MessengerInstance.Send<Message>(new Message("FolderDeleted", folderInfo), PlaylistViewModel.NAME);
                    });
                }
            });
        }

        private void LockFolderCommandExecute(FolderInfo folderInfo)
        {
            if (!VersionHelper.CheckPaidFeature())
            {
                return;
            }

            bool isLocking = false;
            string title = string.Empty;

            if (string.IsNullOrEmpty(folderInfo.Passcode))
            {
                isLocking = false;
                title = ResourceLoader.GetForCurrentView().GetString("Message/Confirm/Create/Password");
            }
            else
            {
                isLocking = true;
                title = ResourceLoader.GetForCurrentView().GetString("Message/Confirm/Remove/Password");
            }

            StackPanel contentPanel = new StackPanel();
            contentPanel.Children.Add(new TextBlock
            {
                Text = title,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 12)
            });

            PasswordBox pbox = new PasswordBox
            {
                MaxLength = 8,
                Margin = new Thickness(0, 12, 0, 12)
            };
            
            contentPanel.Children.Add(pbox);
            
            ContentDialog contentDlg = new ContentDialog
            {
                Content = contentPanel,
                IsPrimaryButtonEnabled = false,
                PrimaryButtonText = ResourceLoader.GetForCurrentView().GetString("Ok"),
                SecondaryButtonText = ResourceLoader.GetForCurrentView().GetString("Cancel")
            };

            pbox.PasswordChanged += (sender, args) =>
            {
                if (isLocking)
                {
                    contentDlg.IsPrimaryButtonEnabled = folderInfo.Passcode == pbox.Password;
                }
                else
                {
                    contentDlg.IsPrimaryButtonEnabled = !string.IsNullOrEmpty(pbox.Password);
                }
            };
          
            //메세지 창 출력
            App.ContentDlgOp = contentDlg.ShowAsync();

            //후처리기 등록
            App.ContentDlgOp.Completed = new AsyncOperationCompletedHandler<ContentDialogResult>(async (op, status) =>
            {
                var result = await op;
                if (result == ContentDialogResult.Primary)
                {
                    if (isLocking)
                    {
                        if (folderInfo.Passcode == pbox.Password)
                        {
                            //잠금 해제
                            folderInfo.Passcode = string.Empty;
                            folderDAO.Update(folderInfo);

                            //전체 비디오에서 추가 요청
                            MessengerInstance.Send<Message>(new Message("FolderAdded", folderInfo), AllVideoViewModel.NAME);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(pbox.Password))
                        {
                            //잠금 설정
                            folderInfo.Passcode = pbox.Password;
                            folderDAO.Update(folderInfo);

                            //전체 비디오에서 삭제 요청
                            MessengerInstance.Send<Message>(new Message("FolderDeleted", folderInfo), AllVideoViewModel.NAME);
                            //재생 목록에서 삭제 요청
                            MessengerInstance.Send<Message>(new Message("FolderDeleted", folderInfo), PlaylistViewModel.NAME);
                        }
                    }
                }
                App.ContentDlgOp = null;
            });
        }

        private void CheckListButtonClickCommandExecute()
        {
            //선택 모드 변경시
            SelectionMode = ListViewSelectionMode.Multiple;
            ButtonGroupVisible = false;
            PlayButtonEnable = false;
        }

        private void SynchronizeButtonClickCommandExecute()
        {
            //현재 폴더 재로드
            if (currentFolderInfo != null)
            {
                LoadFilesInFolder(currentFolderInfo);
            }
        }

        private void BackButtonClickCommandExecute()
        {
            //선택 모드 변경
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;
        }

        private void SelectAllButtonClickCommandExecute(ListView listView)
        {
            if (listView.SelectedItems.Count > 0)
            {
                listView.SelectedItems.Clear();
            }
            else
            {
                listView.SelectAll();
            }
        }

        private void PlayButtonClickCommandExecute(ListView listView)
        {
            //로딩 표시
            var loader = ResourceLoader.GetForCurrentView();
            var msg = string.Format(loader.GetString("Loading"), loader.GetString("Video"));
            MessengerInstance.Send(new Message("ShowLoadingPanel", new KeyValuePair<string, bool>(msg, true)), MainViewModel.NAME);
            //선택된 리스트 객체 생성 (아래에서 SelectionMode를 변경하므로, 반드시 ToList()등으로 객체를 복제해야함)
            var mediaInfoList = listView.SelectedItems.Cast<MediaInfo>().Where(x => string.IsNullOrEmpty(x.OccuredError)).ToList();
            //미디어 파일과 자막을 등록
            InsertMediaInfo(mediaInfoList);
            //재생 목록 생성및 재생 요청
            MessengerInstance.Send<Message>(new Message("PlayList", mediaInfoList), PlaylistViewModel.NAME);
            //버튼 초기화
            SelectionMode = ListViewSelectionMode.None;
            ButtonGroupVisible = true;
        }
        #endregion

        void InsertMediaInfo(IEnumerable<MediaInfo> mediaInfoList)
        {
            //미디어 등록 (있으면 수정)
            fileDAO.InsertMedia(mediaInfoList);
            //자막 등록 (있으면 수정)
            foreach (var mi in mediaInfoList)
            {
                //재생목록 추가 표시
                mi.IsAddedPlaylist = true;
                if (mi.SubtitleFileList != null)
                {
                    fileDAO.InsertSubtitles(mi);
                }
            }
        }

        private async void LoadRootFolders()
        {
            try
            {
                //폴더 추가 버튼
                await DispatcherHelper.RunAsync(async () =>
                {
                    if (ExplorerFolderSource.Count > 0)
                    {
                        ExplorerFolderSource.Clear();
                    }

                    if (ExplorerFileSource.Count > 0)
                    {
                        ExplorerFileSource.Clear();
                    }

                    //탐색기 루트를 로드
                    folderDAO.LoadRootFolderList(ExplorerFolderSource, LockFolderCommand, RemoveFolderCommand, true);

                    ExplorerFolderSource.Add(new FolderInfo()
                    {
                        Name = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView().GetString("AddFolder"),
                        Glyph1 = "\xE109", //&#xE109;
                        Type = FolderType.Picker,
                        IsHighlight = ExplorerFolderSource.Count == 0,
                        Level = 1
                    });
                    
                    ///////////////////////////// 윈10 10549 버그 Workaround//////////////////////////////////
                    if (VersionHelper.WindowsVersion == 10 && !ExplorerFolderSource.Any(x => x.Type != FolderType.Picker))
                    {
                        List<StorageFolder> folders = new List<StorageFolder>();
                        folders.Add(KnownFolders.VideosLibrary);

                        var external = await KnownFolders.RemovableDevices.GetFoldersAsync();
                        if (external != null && external.Any())
                        {
                            folders.AddRange(external);
                        }

                        foreach (var folder in folders)
                        {
                            bool found = false;
                            //중복된 폴더는 추가하지 않음
                            foreach (var added in ExplorerFolderSource)
                            {
                                var sf = await added.GetStorageFolder(true);
                                if (sf != null && sf.FolderRelativeId == folder.FolderRelativeId)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                //전체 비디오에 반영
                                var folderInfo = new FolderInfo(folder)
                                {
                                    Level = 1,
                                    Type = FolderType.Root,
                                    ButtonTappedCommand1 = LockFolderCommand,
                                    ButtonTappedCommand2 = RemoveFolderCommand,
                                    Passcode = string.Empty
                                };

                                //선택한 폴더 DB등록
                                folderDAO.Insert(folderInfo);

                                var addFolder = ExplorerFolderSource.FirstOrDefault(x => x.Type == FolderType.Picker);
                                addFolder.IsHighlight = false;
                                var index = ExplorerFolderSource.IndexOf(addFolder);
                                ExplorerFolderSource.Insert(index, folderInfo);

                                //전체 비디오에 반영
                                MessengerInstance.Send<Message>(new Message("FolderAdded", folderInfo), AllVideoViewModel.NAME);
                            }
                        }
                    }
                    //////////////////////////////////////////////////////////////////////////////////////////
                });
            }
            catch (Exception e) 
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
        }
        
        private async void LoadChildFolder(FolderInfo folder)
        {
            cancelTokenSource = new CancellationTokenSource();
            
            await Task.Run(async () =>
            {
                await DispatcherHelper.RunAsync(() => { CheckListButtonEnable = false; });

                StorageFolder stFolder = null;

                //현재 폴더가 저장되지 않았거나, 위로 버튼을 눌러 이동하다 최 상위가 된 경우
                if (folder == null || folder.Level == 0)
                {
                    LoadRootFolders();
                    return;
                }
                else
                {
                    try
                    {
                        stFolder = await folder.GetStorageFolder(false);
                    }
                    catch (Exception)
                    {
                        //현재 폴더 삭제 DB 물리적 삭제
                        if (folder.Type == FolderType.Last)
                        {
                            var asyncAction = folderDAO.Delete(folder);
                            //문제가 발생한 경우 루트 폴더를 로드
                            LoadRootFolders();
                        }
                        
                        return;
                    }
                }

                await DispatcherHelper.RunAsync(() =>
                {
                    if (ExplorerFolderSource.Count > 0)
                    {
                        ExplorerFolderSource.Clear();
                    }

                    if (ExplorerFileSource.Count > 0)
                    {
                        ExplorerFileSource.Clear();
                    }

                    ExplorerFolderSource.Add(new FolderInfo()
                    {
                        Level = folder.Level - 1,
                        Name = string.Format(".. ({0})", ResourceLoader.GetForCurrentView().GetString("ToUpper")),
                        Path = folder.ParentFolderPath,
                        Type = FolderType.Upper,
                    });
                });
                
                try 
                {
                    var folderList = (await stFolder.GetFoldersAsync()).OrderBy(x => x.Name);
                    if (folderList != null && folderList.Any())
                    {
                        await LoadFolderList(folderList, folder, cancelTokenSource.Token);
                    }

                    var fileStorageList = (await stFolder.GetFilesAsync()).OrderBy(x => x.Name);
                    var fileList = fileStorageList.Where(x => x.IsVideoFile());
                    if (fileList != null && fileList.Any())
                    {
                        await AddExplorerList(fileList, fileStorageList.Where(x => x.IsSubtitleFile()).ToList(), folder, cancelTokenSource.Token);
                        await DispatcherHelper.RunAsync(() => { CheckListButtonEnable = true; });
                    }
                }
                catch (OperationCanceledException ex)
                {
                    //DispatcherHelper.CheckBeginInvokeOnUI(() => { ExplorerFolderSource.Clear(); });
                    System.Diagnostics.Debug.WriteLine("폴더 네비게이션 취소됨 : " + ex.CancellationToken.GetHashCode());
                }
                
            }, cancelTokenSource.Token);
        }

        private async Task LoadFolderList(IEnumerable<StorageFolder> storageFolder, FolderInfo parentFolder, CancellationToken token)
        {
            foreach (var item in storageFolder)
            {
                var fi = new FolderInfo(item)
                {
                    Level = parentFolder.Level + 1,
                };
                
                //취소가 들어온 경우라면 Exception 발생
                token.ThrowIfCancellationRequested();
                //화면 리스트에 폴더 추가
                await DispatcherHelper.RunAsync(() =>
                {
                    if (currentFolderInfo.Path == fi.ParentFolderPath)
                    {
                        ExplorerFolderSource.Add(fi);
                    }
                });
            }
        }

        private async Task AddExplorerList(IEnumerable<StorageFile> storageFile, 
            List<StorageFile> subtitleList, FolderInfo parentFolder, CancellationToken token)
        {
            var prevFolderName = string.Empty;
            //재생목록 로드
            List<MediaInfo> playlist = null;
            
            foreach (var item in storageFile)
            {
                var mi = new MediaInfo(item);
               
                if (subtitleList != null)
                {
                    //비동기 모드로 파일의 기본 정보(사이즈, 생성일자) 로드
                    var asyncAction = ThreadPool.RunAsync((handler) =>
                    {
                        if (playlist == null)
                        {
                            playlist = new List<MediaInfo>();
                            fileDAO.LoadPlayList(playlist, 100, 0, false);
                        }

                        //재생목록 존재여부 체크
                        mi.IsAddedPlaylist = playlist.Any(x => x.Path == mi.Path);

                        var pathName = mi.Path.Remove(mi.Path.Length - Path.GetExtension(mi.Path).Length);
                        //자막 검색
                        foreach (var ext in CCPlayerConstant.SUBTITLE_FILE_SUFFIX)
                        {
                            StorageFile subtitleFile = null;
                            try
                            {
                                subtitleFile = new List<StorageFile>(subtitleList).FirstOrDefault(x => Path.GetExtension(x.Path).ToUpper() == ext.ToUpper()
                                    && x.Path.Length > ext.Length && x.Path.Remove(x.Path.Length - ext.Length).ToUpper().Contains(pathName.ToUpper()));
                            }
                            catch (Exception) { }

                            if (subtitleFile != null)
                            {
                                subtitleList.Remove(subtitleFile);

                                //자막을 미디어 파일에 연결
                                mi.AddSubtitle(new SubtitleInfo(subtitleFile));
                            }
                        }
                    }, WorkItemPriority.Low);
                }
                else
                {
                    if (prevFolderName == item.Name)
                    {
                        mi.Name = string.Format("{0} ({1})", mi.Name, Path.GetPathRoot(mi.Path));
                    }
                    prevFolderName = item.Name;
                }

                token.ThrowIfCancellationRequested();

                await DispatcherHelper.RunAsync(() =>
                {
                    //if (token == cancelTokenSource.Token)
                    if (currentFolderInfo.Path == mi.ParentFolderPath)
                    {
                        ExplorerFileSource.Add(mi); 
                    }
                });
            }
        }


        public async void ContinueFolderPicker(FolderPickerContinuationEventArgs args)
        {
            var folder = args.Folder;
            if (folder != null)
            {
                //중복된 폴더는 추가하지 않음
                if (!ExplorerFolderSource.Any(x => x.Path == folder.Path))
                {
                    //전체 비디오에 반영
                    await ThreadPool.RunAsync(async handler =>
                    {
                        var folderInfo = new FolderInfo(folder)
                        {
                            Level = 1,
                            Type = FolderType.Root,
                            ButtonTappedCommand1 = LockFolderCommand,
                            ButtonTappedCommand2 = RemoveFolderCommand,
                            Passcode = string.Empty
                        };

                        //선택한 폴더 DB등록
                        folderDAO.Insert(folderInfo);

                        await DispatcherHelper.RunAsync(() =>
                        {
                            //Add Folder 바로 앞에 추가
                            var addFolder = ExplorerFolderSource.FirstOrDefault(x => x.Type == FolderType.Picker);
                            addFolder.IsHighlight = false;
                            var index = ExplorerFolderSource.IndexOf(addFolder);
                            ExplorerFolderSource.Insert(index, folderInfo);

                            //전체 비디오에 반영
                            MessengerInstance.Send<Message>(new Message("FolderAdded", folderInfo), AllVideoViewModel.NAME);
                        });
                    });
                }
            }
            else
            {
                //간간히 화면에 렌더링이 안되는 경우가 생김. 전체 새로 고침을 통해서 다시 렌더링.
                LoadRootFolders();
            }
        }

        async void LoadFilesInFolder(FolderInfo folderInfo)
        {
            //파일 선택 모드 초기화
            SelectionMode = ListViewSelectionMode.None;

            if (cancelTokenSource != null && !cancelTokenSource.IsCancellationRequested)
            {
                cancelTokenSource.Cancel();
            }

            await ThreadPool.RunAsync(handler =>
            {
                //선택한 폴더를 복사하여 등록 및 이전 폴더 삭제
                folderDAO.ReplaceLastFolder(folderInfo);
                    
                //자식 로드
                LoadChildFolder(folderInfo);
            });
        }


        public void ToUpperFolder(BackPressedEventArgs e)
        {
            var upperFolder = ExplorerFolderSource.FirstOrDefault(x => x.Type == FolderType.Upper);

            if (upperFolder != null)
            {
                currentFolderInfo = upperFolder;
//                System.Diagnostics.Debug.WriteLine(upperFolder.Path);
                LoadFilesInFolder(upperFolder);
            }
            //위로 가기 폴더가 없으면 최상위
            e.Handled = upperFolder != null;
        }

    }
}
