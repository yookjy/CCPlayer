using System.Diagnostics;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace CCPlayer.WP81.Views
{
    public sealed partial class MediaSearch : UserControl
    {
        public MediaSearch()
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
                System.Diagnostics.Debug.WriteLine("검색 엘리먼트 로딩 완료 :  " + st.Elapsed);
            }
        }
    }
}
