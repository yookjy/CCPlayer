using CCPlayer.WP81.Helpers;
using CCPlayer.WP81.Models;
using CCPlayer.WP81.Models.DataAccess;
using CCPlayer.WP81.Strings;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Lime.Xaml.Controls;
using Lime.Xaml.Models;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CCPlayer.WP81.ViewModel
{
    public class AboutViewModel : ViewModelBase
    {
        public static readonly string NAME = typeof(AboutViewModel).Name;
        public const string FACEBOOK_SUPPORT = "http://m.facebook.com/{0}";

        public ObservableCollection<GroupCollection<Account>> CreatorList { get; set; }
        private Queue<GroupCollection<Account>> creatorQueue;

        public ObservableCollection<GroupCollection<VersionContent>> WhatsNewList { get; set; }
        private Queue<GroupCollection<VersionContent>> whatsNewQueue;

        public ObservableCollection<GroupCollection<Account>> CreditsList { get; set; }
        private Queue<GroupCollection<Account>> creditsQueue;

        public string PaidAppId { get { return VersionHelper.FeatureLevel < 3 ? CCPlayerConstant.APP_PRO_ID : null; } }

        public string FacebookId { get { return "484115588360126"; } }

        public ICommand FlyoutOpenedCommand { get; set; }

        public AboutViewModel()
        {
            this.CreateAboutModels();
            this.CreateCommands();
            this.RegisterMessages();
        }

        private void CreateAboutModels()
        {
            creatorQueue = new Queue<GroupCollection<Account>>();
            CreatorList = new ObservableCollection<GroupCollection<Account>>();
            CreatorList.CollectionChanged += CreatorList_CollectionChanged;

            whatsNewQueue = new Queue<GroupCollection<VersionContent>>();
            WhatsNewList = new ObservableCollection<GroupCollection<VersionContent>>();
            WhatsNewList.CollectionChanged += WhatsNewList_CollectionChanged;

            creditsQueue = new Queue<GroupCollection<Account>>();
            CreditsList = new ObservableCollection<GroupCollection<Account>>();
            CreditsList.CollectionChanged += CreditsList_CollectionChanged;
        }

        private void CreateCommands()
        {
            FlyoutOpenedCommand = new RelayCommand<object>((sender) =>
            {
                var dataContext = (((sender as Flyout).Content) as Grid).DataContext as AppInfoVersion;

                if (dataContext != null && dataContext.ListViewCollection != null)
                {
                    var loader = ResourceLoader.GetForCurrentView();
                    switch (dataContext.Type)
                    {
                        case SimpleAboutType.Developer:
                            AddCreators(loader);
                            break;
                        case SimpleAboutType.WhatsNew:
                            AddWhatsNew(loader);
                            break;
                        case SimpleAboutType.Credits:
                            AddCredits(loader);
                            break;
                    }
                }
            });
        }

        private void RegisterMessages()
        {
            MessengerInstance.Register<Message>(this, NAME, msg =>
            {
                switch (msg.Key)
                {
                    case "Activated":
                        break;
                    case "BackPressed":
                        msg.GetValue<BackPressedEventArgs>().Handled = true;
                        MessengerInstance.Send<Message>(new Message("ConfirmTermination", null), MainViewModel.NAME);
                        break;
                }
            });

            MessengerInstance.Register<Message>(this, msg =>
            {
                switch (msg.Key)
                {
                    case "RerfershAppVersion":
                        RaisePropertyChanged("PaidAppId");
                        break;
                }
            });
        }

        async void CreatorList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (creatorQueue.Count > 0)
            {
                await Task.Delay(300);
                await DispatcherHelper.RunAsync(() =>
                {
                    if (creatorQueue.Count > 0)
                    {
                        var gc = creatorQueue.Dequeue();
                        CreatorList.Add(gc);
                    }
                });
            }
        }

        async void WhatsNewList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (whatsNewQueue.Count > 0)
            {
                await Task.Delay(500);
                await DispatcherHelper.RunAsync(() =>
                {
                    if (whatsNewQueue.Count > 0)
                    {
                        var gc = whatsNewQueue.Dequeue();
                        WhatsNewList.Add(gc);
                    }
                });
            }
        }

        async void CreditsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (creditsQueue.Count > 0)
            {
                await Task.Delay(500);
                await DispatcherHelper.RunAsync(() =>
                {
                    if (creditsQueue.Count > 0)
                    {
                        var gc = creditsQueue.Dequeue();
                        CreditsList.Add(gc);
                    }
                });
            }
        }

        async void AddCreators(ResourceLoader loader)
        {
            creatorQueue.Clear();
            CreatorList.Clear();

            var developGroup = new GroupCollection<Account>(loader.GetString("AboutDevelopment"));
            developGroup.Add(new Account()
            {
                Contact = string.Format(FACEBOOK_SUPPORT, "yookjy"),
                ContactName = "@facebook",
                ContactType = ContactType.Web,
                Contact2 = "http://yookjy.wordpress.com",
                ContactName2 = "@blog",
                ContactType2 = ContactType.Web,
                Name = loader.GetString("AboutDeveloper")
            });

            var designGroup = new GroupCollection<Account>(loader.GetString("AboutDesign"));
            designGroup.Add(new Account()
            {
                Contact = string.Format(FACEBOOK_SUPPORT, "yookjy"),
                ContactName = "@facebook",
                ContactType = ContactType.Web,
                Contact2 = "http://yookjy.wordpress.com",
                ContactName2 = "@blog",
                ContactType2 = ContactType.Web,
                Name = loader.GetString("AboutDesigner")
            });

            creatorQueue.Enqueue(developGroup);
            creatorQueue.Enqueue(designGroup);

            await DispatcherHelper.RunAsync(() =>
            {
                CreatorList.Add(creatorQueue.Dequeue());
            });
        }

        async void AddWhatsNew(ResourceLoader loader)
        {
            whatsNewQueue.Clear();
            WhatsNewList.Clear();

            //webm, 번역추가...
            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2016, 304, 191),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00079")), //libraries update
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00080")) //asf
            }));

            //webm, 번역추가...
            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2016, 204, 190),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00077")), //포르투갈어/프랑스어
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00078")), //webm지원
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2015, 1231, 189),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00076")), //자막 인코딩 검출 로직 변경
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2015, 1226, 188),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00055")), //앱크래쉬 버그 (이미지 자막의 경우에 rect가 NULL이 될때)
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00075")), //재생 패널위 배터리 및 시간 표시
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2015, 1218, 1871),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00074") + " (SMI Format)"), //일부 자막 스킵 버그 수정
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(2015, 1216, 187),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00073")), //일부 MKV파일 재생 시작시 앱크래쉬 해결
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00074")), //일부 자막 스킵 버그 수정
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 6),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00055")), //앱크래쉬 버그 (자막 avcodec_decode_subtitle2 에서 디코드 실패후 음수값이 나와 패킷 사이즈가 더 커지는 현상)
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00067")), //ASS/SSA 버그
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 5),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00070")), //MKV/3G2컨테이너내 XviD코덱 요류
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00071")), //일부 FLV 파일의 재생 오류
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00072")), //모든 비디오 허브 사용 여부 옵션 추가
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 4),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00068")), //TTC, OTC 폰트 컬렉션 지원
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00069")), //폰트 관련 버그 수정
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00054")), //코덱 호환성 향상
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 3),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00067")), //ASS/SSA 버그 수정
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00002")), //버그 수정
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 2),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00004")), //자막 관련 버그 
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00054")), //코덱 호환성
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 1),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00063")), //일부 자막 깜빡임 수정
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00066")), //디코더 성능
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00064")), //줌 4X 확대 지원
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00065")), //터키어 번역 추가
            }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 8, 0),
            new VersionContent[]
            {
                new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00062")), //Pinch to Zoom (Only Pro Feature)
                new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")), //multi select bug in Explorer
                new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00054")), //improved audio codec compatibility 
            }));

            var gc179 = new GroupCollection<VersionContent>(
            GetVersion(1, 7, 9),
            new VersionContent[]
              {
                 new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00059")), //smi font bug fix
                 new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00060")) //added aspect ratio
              });

            if (!VersionHelper.IsFullVersion)
            {
                gc179.AddItem(new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00061"))); //IAPs
            }
            whatsNewQueue.Enqueue(gc179);

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 7, 8),
            new VersionContent[]
              {
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00058")), //Playback bug fix
              }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            GetVersion(1, 7, 6),
            new VersionContent[]
              {
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00057")), //내장 자막 Timed Text지원
              }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
              GetVersion(1, 7, 5),
              new VersionContent[]
                {
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")), //UI테마 아이콘 색상, 재생 로딩 속도, 핸들러 타는 횟수. 
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
              GetVersion(1, 7, 4),
              new VersionContent[]
                {
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00054")), //코덱 호환성
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00055")), //재생시 앱 크래쉬
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00056")), //볼륨 제스쳐 버그
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
              GetVersion(1, 7, 3),
              new VersionContent[]
                {
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00051")), //자막 플리커 버그 수정
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00052")), //ASS 이펙터 무시
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00053")), //24Bit 오디오 재생 (다운샘플링)
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
              GetVersion(1, 7, 2),
              new VersionContent[]
                {
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00050")), //Shader 색공간 변환 버그 수정
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
               GetVersion(1, 7, 1),
               new VersionContent[]
                {
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00046")), //힌디 추가
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00047")), //폴더보호기능
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00048")), //시작메뉴선택기능
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00049")), //SW디코더 성능개선
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
                GetVersion(1, 7, 0),
                new VersionContent[]
                {
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00044")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00045")),
                }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            //    GetVersion(1, 5, 7),
            //    new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00043")),
            //    }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
                GetVersion(1, 5, 6),
                new VersionContent[]
                {
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00038")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00039")),
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00040")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00041")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00042")),
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
                GetVersion(1, 5, 5),
                new VersionContent[]
                {
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00035")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00036")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00037")),
                }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
                GetVersion(1, 5, 4),
                new VersionContent[]
                {
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00033")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00034")),
                }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            //    GetVersion(1, 5, 3),
            //    new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00032")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            //    GetVersion(1, 5, 2),
            //    new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00031")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
            //    GetVersion(1, 5, 1),
            //    new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00013")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //    }));

            /*
                1. Could you give us the option to choose whether videos resume or play from the beginning by default?
                2. At the end of a video playlist, could you give us the option to either go back to the beginning of said video playlist and play it all over again or just stop altogether?
                3. In the transition from one video to the next, could you give us the option of either a loading animation of sorts or just quick progression to the next video in the playlist?
            */
            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(
                GetVersion(1, 5, 0),
                new VersionContent[]
                {
                    new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00027")),
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00028")),
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00029")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00030")),
                }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 6), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00024")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00025")),
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00026")),
            //    }));

            //비슷한 자막 파일 지원
            //자막 위치 조정기능 추가
            //기타 버그 수정 및 개선
            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 5), new VersionContent[]
                {
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00022")),
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00023")),
                }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 4), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //    }));

            ////0. 모든 비디오 목록에 캐싱 적용
            ////1. Light Theme에서 제스쳐시 상태 표시가 되지 않는 버그 수정
            ////2. Storage 처리 관련 버그 수정 (카드가 바뀌거나 토큰이 다른 경우)
            ////3. Keyframe이 없는 MKV재생 지원
            ////4. 외부 앱에서 CCPlayer호출 관련 버그 수정
            ////5. 520/620 FHD 버그 수정
            ////6. 기타 개선 및 버그 수정
            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 3), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00006")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //        new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00013")),
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00015")),
            //    }));

            var version = new GroupCollection<VersionContent>(GetVersion(1, 2, 2), new VersionContent[]
                {
                    new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00018")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00019")),
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00021")),
                    new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014"))
                });

            if (!VersionHelper.IsFullVersion)
            {
                version.AddItem(new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00020")));
            }
            whatsNewQueue.Enqueue(version);

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 1), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00017")),
            //    }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 2, 0), new VersionContent[]
                {
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00016")),
                }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 9), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00015")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 8), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00013")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 7), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00010")),
            //        new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00011")),
            //        //new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00012")),
            //        new VersionContent(VersionContentType.MOD, loader.GetString("UpdateHistory00013")),
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00014")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 6), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00009")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 5), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00008")),
            //    }));

            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 4), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00006")),
            //        new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00007")),
            //    }));
            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 3), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00004")),
            //        new VersionContent(VersionContentType.IMP, loader.GetString("UpdateHistory00005")),
            //    }));
            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 2), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00002")),
            //    }));
            //whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 1), new VersionContent[] 
            //    {
            //        new VersionContent(VersionContentType.FIX, loader.GetString("UpdateHistory00002")),
            //        new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00003")),
            //    }));

            whatsNewQueue.Enqueue(new GroupCollection<VersionContent>(GetVersion(1, 1, 0), new VersionContent[]
                {
                    new VersionContent(VersionContentType.NEW, loader.GetString("UpdateHistory00001")),
                }));

            await DispatcherHelper.RunAsync(() =>
            {
                WhatsNewList.Add(whatsNewQueue.Dequeue());
            });
        }

        string GetVersion(ushort major, ushort minor, ushort build)//, ushort revision)
        {
            if (major == 0 && minor == 0 && build == 0)
            {
                var packId = Package.Current.Id;
                major = packId.Version.Major;
                minor = packId.Version.Minor;
                build = packId.Version.Build;
                //revision = packId.Version.Revision;
            }

            //return string.Format("version {0}.{1}.{2}.{3}", major, minor, build, revision);
            return string.Format("version {0}.{1}.{2}", major, minor, build);
        }

        async void AddCredits(ResourceLoader loader)
        {
            creditsQueue.Clear();
            CreditsList.Clear();

            var libraryGroup = new GroupCollection<Account>(loader.GetString("AboutLibraryCreators"));
            libraryGroup.Add(new Account()
            {
                Contact = "https://code.google.com/p/ude/",
                ContactType = ContactType.Web,
                Name = "UDE",
                ContactName = "@source code",
                Contact2 = "https://www.mozilla.org/MPL/",
                ContactType2 = ContactType.Web,
                ContactName2 = "/   license (MPL 1.1)",

            });
            libraryGroup.Add(new Account()
            {
                Contact = "http://www.mvvmlight.net/",
                ContactType = ContactType.Web,
                Name = "MVVM Light Toolkit",
                ContactName = "@homepage",
            });
            libraryGroup.Add(new Account()
            {
                Name = "FFmpegInterop library for Windows",
                Contact = "https://github.com/Microsoft/FFmpegInterop",
                ContactType = ContactType.Web,
                ContactName = "@source code",
                Contact2 = "http://www.apache.org/licenses/LICENSE-2.0",
                ContactType2 = ContactType.Web,
                ContactName2 = "/   license (Apache 2.0 License)",
            });
            libraryGroup.Add(new Account()
            {
                Name = "FFmpeg",
                Contact = "http://www.ffmpeg.org/",
                ContactType = ContactType.Web,
                ContactName = "@homepage",
                Contact2 = "https://github.com/FFmpeg/FFmpeg/blob/master/COPYING.LGPLv3",
                ContactType2 = ContactType.Web,
                ContactName2 = "/   license (LGPL v2.1 or later)",
            });

            var specialGroup = new GroupCollection<Account>(loader.GetString("AboutSpecialThanks"));
            specialGroup.Add(new Account()
            {
                Name = loader.GetString("AboutSpecialPeople1")
            });
            //specialGroup.Add(new Account()
            //{
            //    Contact = string.Format(FACEBOOK_SUPPORT, "100001136649926"),
            //    ContactType = ContactType.Web,
            //    ContactName = "@facebook",
            //    Name = loader.GetString("AboutSpecialPeople2")
            //});
            specialGroup.Add(new Account()
            {
                Name = loader.GetString("AboutSpecialPeople3")
            });

            var translateGroup = new GroupCollection<Account>(loader.GetString("AboutTranslators"));
            translateGroup.Add(new Account()
            {
                Contact = "http://about.me/masabalos",
                ContactType = ContactType.Web,
                ContactName = "@homepage",
                Name = loader.GetString("AboutTranslatePeople1"),
                Attr1 = "Spanish (Español)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:luciahedreh@gmail.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople2"),
                Attr1 = "Russian (русский)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "http://m.facebook.com/degetel2007",
                ContactType = ContactType.Web,
                ContactName = "@facebook",
                Name = loader.GetString("AboutTranslatePeople3"),
                Attr1 = "Romanian (Român)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:sebezhetetlen98@gmail.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople4"),
                Attr1 = "Hungarian (Magyar)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "http://preludebg.com/",
                ContactType = ContactType.Web,
                ContactName = "@homepage",
                Name = loader.GetString("AboutTranslatePeople5"),
                Attr1 = "Bulgarian (български)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:a102103170@outlook.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople6"),
                Attr1 = "Chinese (中文)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:voytkiv@msn.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople7"),
                Attr1 = "Ukrainian (Український)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:m4f19@outlook.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople8"),
                Attr1 = "Persian (فارسی)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:xiniuss@live.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople9"),
                Attr1 = "Hindi (भारतीय)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:rthayru@gmail.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople10"),
                Attr1 = "Turkish (Türk)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "http://about.me/eduardorodrigues",
                ContactType = ContactType.Web,
                ContactName = "@homepage",
                Name = loader.GetString("AboutTranslatePeople11"),
                Attr1 = "Portuguese (Português)"
            });
            translateGroup.Add(new Account()
            {
                Contact = "mailto:pkerga@hotmail.com",
                ContactType = ContactType.Email,
                ContactName = "@email",
                Name = loader.GetString("AboutTranslatePeople12"),
                Attr1 = "French (français)"
            });

            creditsQueue.Enqueue(specialGroup);
            creditsQueue.Enqueue(translateGroup);
            creditsQueue.Enqueue(libraryGroup);

            await DispatcherHelper.RunAsync(() =>
            {
                CreditsList.Add(creditsQueue.Dequeue());
            });
        }
    }
}
