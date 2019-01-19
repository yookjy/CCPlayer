using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.Models.DataAccess;
using CCPlayer.UWP.ViewModels.Base;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.ViewModels
{
    public class PrivacySettingViewModel : CCPViewModelBase
    {
        [DependencyInjection]
        private Settings _Settings;
        //설정
        public Settings Settings
        {
            get { return _Settings; }
        }

        [DependencyInjection]
        public SettingDAO settingDAO;

        public RoutedEventHandler LoadedEventHandler;

        public TappedEventHandler SavePasswordTappedEventHandler;

        public TappedEventHandler ChangeFileExtensionTappedEventHandler;

        public bool EnabledSaveButton { get; set; }

        private string _Password;
        [DoNotNotify]
        public string Password
        {
            get { return _Password; }
            set
            {
                if (Set(ref _Password, value))
                {
                    EnabledSaveButton = Settings.Privacy.AppLockPassword != value && value.Length >= 4;
                } 
            }
        }

        protected override void CreateModel()
        {
        }

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
            settingDAO = null;
        }

        protected override void InitializeViewModel()
        {
        }

        protected override void RegisterEventHandler()
        {
            LoadedEventHandler = Loaded;
            SavePasswordTappedEventHandler = SavePasswordTapped;
            ChangeFileExtensionTappedEventHandler = ChangeFileExtensionTapped;
        }

        protected override void RegisterMessage()
        {
        }

        public void Loaded(object sender,RoutedEventArgs e)
        {
            Password = Settings.Privacy.AppLockPassword;
        }

        public async void SavePasswordTapped(object sender, TappedRoutedEventArgs e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            var password = Password.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                var dlg = DialogHelper.GetSimpleContentDialog(
                    resource.GetString("AppLock/Password/Validate1/Title"),
                    resource.GetString("AppLock/Password/Validate/Text"),
                    resource.GetString("ConentDialog/Button/Save"));

                await dlg.ShowAsync();
                App.ContentDlgOp = null;
                Password = password;
            }
            else
            {
                if (password.Length != Password.Length)
                {
                    var dlg = DialogHelper.GetSimpleContentDialog(
                        resource.GetString("AppLock/Password/Validate2/Title"),
                        resource.GetString("AppLock/Password/Validate/Text"),
                        resource.GetString("Button/Ok/Content"));

                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
                    Password = password;
                }
                else
                {
                    ContentDialog dlg = null;

                    if (!Settings.Privacy.UseAppLock)
                    {
                        dlg = DialogHelper.GetSimpleContentDialog(
                            resource.GetString("AppLock/Password/Validate3/Title"),
                            resource.GetString("AppLock/Password/Validate3/Text"),
                            resource.GetString("Button/AppLock/TurnOff"),
                            resource.GetString("Button/AppLock/TurnOn"));

                        ContentDialogResult result = await dlg.ShowAsync();
                        App.ContentDlgOp = null;

                        if (result == ContentDialogResult.Secondary && VersionHelper.CheckPaidFeature())
                        {
                            Settings.Privacy.UseAppLock = true;
                        }
                    }
                    else
                    {
                        dlg = DialogHelper.GetSimpleContentDialog(
                            resource.GetString("AppLock/Password/Validate3/Title"),
                            resource.GetString("AppLock/Login/Title/Text"),
                            resource.GetString("Button/Ok/Content"));

                        await dlg.ShowAsync();
                        App.ContentDlgOp = null;
                    }
                }
                Settings.Privacy.AppLockPassword = Password;

                //DB에 저장
                settingDAO.Replace(Settings);
                EnabledSaveButton = false;
            }
        }

        public void UseAppLockSwitchToggled(object sender, RoutedEventArgs e)
        {
            var tw = e.OriginalSource as ToggleSwitch;
            if (tw.IsOn)
            {
                if (VersionHelper.CheckPaidFeature())
                    Settings.Privacy.UseAppLock = true;
                else
                    tw.IsOn = false;
            }
            else
            {
                Settings.Privacy.UseAppLock = false;
            }
        }

        public async void ChangeFileExtensionTapped(object sender, TappedRoutedEventArgs e)
        {
            if (VersionHelper.CheckPaidFeature())
            {
                string newExtension = ".4CCP";
                var resource = ResourceLoader.GetForCurrentView();
                var picker = new FileOpenPicker()
                {
                    SuggestedStartLocation = PickerLocationId.VideosLibrary,
                    ViewMode = PickerViewMode.List,
                    CommitButtonText = resource.GetString("FileAssociation/Change/Commit/Content")
                };

                foreach(var suffix in CCPlayer.UWP.Xaml.Controls.MediaFileSuffixes.VIDEO_SUFFIX.Where(x => x.ToUpper() != newExtension))
                {
                    picker.FileTypeFilter.Add(suffix);
                }

                //picker.PickSingleFileAndContinue();
                IReadOnlyList<StorageFile> fileList = await picker.PickMultipleFilesAsync();
                if (fileList != null && fileList.Count > 0)
                {
                
                    string orgName = string.Empty;
                    string fileName = string.Empty;
                    int count = 0;
                    foreach(var file in fileList)
                    {
                        try
                        {
                            orgName = file.Name;
                            await file.RenameAsync(Path.GetFileNameWithoutExtension(orgName) + newExtension, NameCollisionOption.FailIfExists);
                            if (string.IsNullOrEmpty(fileName))
                            {
                                fileName = orgName;
                            }
                            else
                            {
                                count++;
                            }
                        }
                        catch(Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(file.Name + "은 이미 존재하는 파일입니다. =>" + ex.Message);
                        }
                    }

                    string title = string.Empty;
                    string content = string.Empty;

                    if (count > 0)
                    {
                        title = resource.GetString("FileAssociation/Change/Success/Title");
                        content = string.Format(resource.GetString("FileAssociation/Change/Success1/Content"), fileName, count, newExtension);
                    }
                    else if (!string.IsNullOrEmpty(fileName))
                    {
                        title = resource.GetString("FileAssociation/Change/Success/Title");
                        content = string.Format(resource.GetString("FileAssociation/Change/Success2/Content"), fileName, newExtension);
                    }
                    else
                    {
                        title = resource.GetString("FileAssociation/Change/Fail/Title");
                        content = string.Format(resource.GetString("FileAssociation/Change/Fail/Content"), newExtension);
                    }

                    var dlg = DialogHelper.GetSimpleContentDialog(title, content, resource.GetString("Button/Ok/Content"));
                    await dlg.ShowAsync();
                    App.ContentDlgOp = null;
                }
            }
        }
    }
}
