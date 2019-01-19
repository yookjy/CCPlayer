using GalaSoft.MvvmLight.Messaging;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace CCPlayer.WP81.Views
{
    public sealed partial class TransportControl : UserControl
    {
        public TransportControl()
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
                System.Diagnostics.Debug.WriteLine("티씨컨트롤   " + st.Elapsed);
            }

            //광고 제거
            Messenger.Default.Register<bool>(this, typeof(TransportControl).FullName, (val) =>
            {
                if (val)
                {
                    AdPanel.Children.Clear();
                }
            });
        }
    }
}
