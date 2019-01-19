using System;
using System.Collections.Generic;
using System.Diagnostics;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace CCPlayer.WP81.Views
{
    public sealed partial class AllSettings : UserControl
    {
        public AllSettings()
        {
            Stopwatch st = null;
            if (Debugger.IsAttached)
            {
                st = new System.Diagnostics.Stopwatch();
                st.Start();
            }

            this.InitializeComponent();

            if (Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine("셋팅페이지 로딩 완료 :  " + st.Elapsed);
            }
        }
    }
}
