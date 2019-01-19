using CCPlayer.UWP.Helpers;
using CCPlayer.UWP.Models;
using CCPlayer.UWP.ViewModels.Base;
using GalaSoft.MvvmLight.Command;
using Lime.Xaml.Helpers;
using Lime.Xaml.Models;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using static Lime.Common.Helpers.EmailHelper;
using static Windows.ApplicationModel.Package;

namespace CCPlayer.UWP.ViewModels
{
    public class Feedback
    {
        public string Name { get; set; }
        public TappedEventHandler Tapped { get; set; }
    }

    public class AppInformationViewModel : CCPViewModelBase
    {
        [DependencyInjection]
        protected Settings _Settings;

        public Settings Settings
        {
            get { return _Settings; }
        }

        public string VersionTypeName => (Settings.General.PaidLevel == PaidLevelType.Trial ? "Trial" : VersionHelper.VersionName);

        public string BuildNumber => ($"{Current.Id.Version.Major}.{Current.Id.Version.Minor}.{Current.Id.Version.Build}.{Current.Id.Version.Revision}");

        [DoNotNotify]
        public List<Feedback> FeedbackSource { get; set; }

        [DoNotNotify]
        public List<Account> TranslatorSource { get; set; }

        [DoNotNotify]
        public List<Account> LibraryCreatorSource { get; set; }

        protected override void CreateModel()
        {
            FeedbackSource = new List<Feedback>();
            TranslatorSource = new List<Account>();
            LibraryCreatorSource = new List<Account>();
        }

        protected override void FakeIocInstanceInitialize()
        {
            _Settings = null;
        }

        protected override void InitializeViewModel()
        {
            AddFeedbacks();
            AddTranslators();
            AddLibraryCreators();
        }

        protected override void RegisterEventHandler()
        {
        }

        protected override void RegisterMessage()
        {
        }

        public async void DeveloperTapped(object sender, TappedRoutedEventArgs e)
        {
            //string prefix = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.mobile" ? "m" : "www";
            string prefix = App.IsMobile ? "m" : "www";
            await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://{prefix}.facebook.com/yookjy"));
        }

        //public void CheerUpTapped(object sender, TappedRoutedEventArgs e)
        //{
        //}

        public async void PrivatePolicyTapped(object sender, TappedRoutedEventArgs e)
        {
            var resource = ResourceLoader.GetForCurrentView();
            ContentDialog dlg = new ContentDialog();

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                Page page = rootFrame.Content as Page;
                if (page != null) dlg.RequestedTheme = page.RequestedTheme;
            }

            Grid panel = new Grid() { Margin = new Thickness(0, 6, 0, 6), Width = 340, Height = 480 };

            if (App.IsMobile)
            {
                panel.Width = Window.Current.Bounds.Width;
                panel.Height = Window.Current.Bounds.Height;
            }

            var webView = new WebView() { Margin = new Thickness(0, 3, 0, 3) };
            panel.Children.Add(webView);
            webView.Navigate(new Uri("ms-appx-web:///Data/PrivatePolicy/index.html"));

            dlg.Title = resource.GetString("Policies/Private/Content");
            dlg.Content = panel;
            dlg.IsPrimaryButtonEnabled = true;
            dlg.PrimaryButtonText = resource.GetString("Button/Ok/Content");

            await dlg.ShowAsync();
        }

        private void AddFeedbacks()
        {
            var resource = ResourceLoader.GetForCurrentView();
            
            if (VersionHelper.IsProfessionalVersion)
            {
                FeedbackSource.Add(new Feedback
                {
                    Name = resource.GetString("Developer/CheerUp/Content"),
                    Tapped = async (sender, args) =>
                    {
                        ContentDialog dlg = new ContentDialog();

                        Frame rootFrame = Window.Current.Content as Frame;
                        if (rootFrame != null)
                        {
                            Page page = rootFrame.Content as Page;
                            if (page != null) dlg.RequestedTheme = page.RequestedTheme;
                        }

                        StackPanel panel = new StackPanel() { Margin = new Thickness(0, 6, 0, 6), VerticalAlignment = VerticalAlignment.Center };
                        panel.Children.Add(new TextBlock()
                        {
                            Text = resource.GetString("Developer/Donation/Text"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 3, 0, 3)
                        });

                        MessengerInstance.Send<Message<ListingInformation>>(new Message<ListingInformation>((listInfo) =>
                        {
                            List<ProductListing> pList = new List<ProductListing>();
                            var pls = listInfo.ProductListings;
                            var values = pls.Values.GetEnumerator();

                            while (values.MoveNext())
                            {
                                var product = values.Current;

                                if (product.ProductType == ProductType.Consumable)
                                {
                                    pList.Add(product);
                                }
                            }

                            if (pList.Count > 0)
                            {
                                foreach (var product in pList.OrderBy(p => p.ProductId))
                                {
                                    Grid item = new Grid() { Margin = new Thickness(0, 6, 0, 6), VerticalAlignment = VerticalAlignment.Center };
                                    item.Children.Add(new TextBlock()
                                    {
                                        Text = product.Name,
                                        TextWrapping = TextWrapping.Wrap,
                                        Margin = new Thickness(0, 3, 0, 3),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Left
                                    });

                                    item.Children.Add(new Button()
                                    {
                                        Content = product.FormattedPrice,
                                        HorizontalAlignment = HorizontalAlignment.Right,
                                        Command = new RelayCommand(() =>
                                        {
                                            dlg.Hide();
                                            MessengerInstance.Send<string>(product.ProductId, "Donation");
                                        })
                                    });

                                    panel.Children.Add(item);
                                }
                            }

                        }),
                        "RetriveIAPsListingInformation");

                        dlg.Title = resource.GetString("Developer/CheerUp/Content");
                        dlg.Content = panel;
                        dlg.IsPrimaryButtonEnabled = true;
                        dlg.PrimaryButtonText = resource.GetString("Button/Close/Content");

                        await dlg.ShowAsync();
                    }
                });
            }
            
            FeedbackSource.Add(new Feedback
            {
                Name = resource.GetString("Feedback/ReviewRate/Content"),
                Tapped = async (sender, args) =>
                {
                    Uri uri = UriHelper.GetRateAndReviewUri();
                    //Uri uri = new Uri(string.Format("ms-windows-store:reviewapp?appid=1765dc04-edc1-4c07-a850-da8b055a6362"));
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            });
            FeedbackSource.Add(new Feedback
            {
                Name = resource.GetString("Feedback/Facebook/Content"),
                Tapped = async (sender, args) =>
                {
                    string prefix = App.IsMobile ? "m" : "www";
                    await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://{prefix}.facebook.com/CCPlayer-484115588360126"));
                }
            });
            FeedbackSource.Add(new Feedback
            {
                Name = resource.GetString("Feedback/BugReport/Content"),
                Tapped = (sender, args) =>
                {
                    VersionHelper.CheckBetaOS(
                        async (buildNumber) =>
                        {
                            var warn = DialogHelper.GetSimpleContentDialog(
                                $"Current operating system is the beta OS. ({buildNumber})",
                                "You have the responsibility for using the beta OS.\nCCPlayer couldn't work correctly, also doesn't support anything for beta OS.\n",//(Tip : In current beta OS, recommend disable \"Settings > Playback > Use L:me component\")",
                                resource.GetString("Button/Close/Content"),
                                resource.GetString("Feedback/BugReport/Content"));
                            if (await warn.ShowAsync() == ContentDialogResult.Secondary)
                            {
                                Send($"Bug reporting! - {Current.DisplayName} {BuildNumber}");
                            }
                            App.ContentDlgOp = null;
                        },
                        () => Send($"Bug reporting! - {Current.DisplayName} {BuildNumber}"));
                }
            });
            FeedbackSource.Add(new Feedback
            {
                Name = resource.GetString("Feedback/Suggest/Content"),
                Tapped = (sender, args) =>
                {
                    Send($"Suggestion - {Current.DisplayName} {BuildNumber}");
                }
            });
        }

        private void AddTranslators()
        {
            //영어 - English
            TranslatorSource.Add(new Account
            {
                Name = "Jooyong, yook",
                Contact = "mailto://yookjy@limeapp.net",
                Attr1 = "en"
            });
            //일본어 - 日本語
            TranslatorSource.Add(new Account
            {
                Name = "ユクジュヨン",
                Contact = "mailto://yookjy@limeapp.net",
                Attr1 = "ja"
            });
            //터키어 - "Turkish (Türk)
            TranslatorSource.Add(new Account
            {
                Name = "Hayrullah",
                Contact = "mailto:rthayru@gmail.com",
                Attr1 = "tr"
            });
            //스페인어 - Spanish (Español)
            TranslatorSource.Add(new Account
            {
                Name = "Juan Carlos Ábalos Guerrero",
                Contact = "http://about.me/masabalos",
                Attr1 = "es"
            });
            //중국어 - Chinese (中文)
            TranslatorSource.Add(new Account
            {
                Name = "Le Xiazi",
                Contact = "mailto:a102103170@outlook.com",
                Attr1 = "zh-Hans"
            });
            //불가리아어 - Bulgarian (български)
            TranslatorSource.Add(new Account
            {
                Name = "Daniel Peichev",
                Contact = "http://preludebg.com", //mailto:antispamcan@gmail.com
                Attr1 = "bg"
            });
            //베트남어 - Vietnamese (tiếng việt)
            TranslatorSource.Add(new Account
            {
                Name = "Phạm Trần Quốc Linh",
                Contact = "http://www.facebook.com/linh.sida", //mailto://quoclinhav94@gmail.com
                Attr1 = "vi"
            });
            //러시아어 (Russian - русский)
            TranslatorSource.Add(new Account
            {
                Name = "Иван Корниенко",
                Contact = "mailto://luciahedreh@gmail.com",
                Attr1 = "ru"
            });
            //우크라이나어 (Ukrainian - українськa)
            TranslatorSource.Add(new Account
            {
                Name = "Dima Shusha",
                Contact = "mailto://shushikian@gmail.com",
                Attr1 = "uk"
            });
            //포르투갈어-브라질 (Português - Brasil)
            TranslatorSource.Add(new Account
            {
                Name = "Emerson Teles",
                Contact = "https://www.facebook.com/emertels",
                Attr1 = "pt-BR"
            });
            //페르시아어-이란 (Persian - فارسی)
            TranslatorSource.Add(new Account
            {
                Name = "Hosein Mohamadzadeh",
                Contact = "http://www.winphone.ir", //mailto://mohamadzadeh@outlook.com 
                Attr1 = "fa"
            });
            //독일어-독일 (Deutsch - Deutschland)
            TranslatorSource.Add(new Account
            {
                Name = "Jakob H. Preusker",
                Contact = "http://twitter.com/brullsker",
                Attr1 = "de"
            });
        }

        private void AddLibraryCreators()
        {
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "FFmpeg",
                Contact = "https://ffmpeg.org",
                ContactName2 = "LGPL 2.1 or later",
                Contact2= "https://github.com/FFmpeg/FFmpeg/blob/master/COPYING.LGPLv3"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "FFmpegInterop",
                Contact = "https://github.com/Microsoft/FFmpegInterop",
                ContactName2 = "Apache 2.0",
                Contact2 = "http://www.apache.org/licenses/LICENSE-2.0"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "MVVM Light Toolkit",
                Contact = "http://www.mvvmlight.net/",
                ContactName2 = "MIT",
                Contact2 = "http://mvvmlight.codeplex.com/license"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "WriteableBitmapEx",
                Contact = "https://writeablebitmapex.codeplex.com/",
                ContactName2 = "MIT",
                Contact2 = "https://writeablebitmapex.codeplex.com/license"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "SQLite",
                Contact = "http://www.sqlite.org/",
                ContactName2 = "Public Domain",
                Contact2 = "http://www.sqlite.org/copyright.html"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "SQLitePCL",
                Contact = "https://sqlitepcl.codeplex.com/",
                ContactName2 = "Apache 2.0",
                Contact2 = "https://sqlitepcl.codeplex.com/license"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "zlib",
                Contact = "http://zlib.net/",
                ContactName2 = "zlib license",
                Contact2 = "http://zlib.net/zlib_license.html"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "UDE",
                Contact = "https://code.google.com/p/ude/",
                ContactName2 = "MPL 1.1",
                Contact2 = "https://www.mozilla.org/MPL/"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "Cubisoft.Winrt.Ftp",
                Contact = "https://code.msdn.microsoft.com/windowsapps/Windows-8-SocketsFtp-4fc23b33#content",
                ContactName2 = "MS-LPL",
                Contact2 = "https://www.openhub.net/licenses/mslpl"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "DecaTec Portable WebDAV Library",
                Contact = "https://github.com/DecaTec/Portable-WebDAV-Library",
                ContactName2 = "MS-PL",
                Contact2 = "https://github.com/DecaTec/Portable-WebDAV-Library/blob/master/LICENSE"
            });
            LibraryCreatorSource.Add(new Account
            {
                ContactName = "PropertyChanged.Fody",
                Contact = "https://github.com/Fody/PropertyChanged",
                ContactName2 = "MIT",
                Contact2 = "https://github.com/Fody/PropertyChanged/blob/master/license.txt"
            });
        }
    }
}
