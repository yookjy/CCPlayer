﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using CCPlayer.UWP.ViewModels;

// 사용자 정의 컨트롤 항목 템플릿에 대한 설명은 http://go.microsoft.com/fwlink/?LinkId=234236에 나와 있습니다.

namespace CCPlayer.UWP.Views.Controls
{
    public sealed partial class GeneralSettings : UserControl
    {
        public GeneralSettingViewModel Vm { get { return (GeneralSettingViewModel)DataContext; } }
        
        public GeneralSettings()
        {
            this.InitializeComponent();
        }
    }
}
