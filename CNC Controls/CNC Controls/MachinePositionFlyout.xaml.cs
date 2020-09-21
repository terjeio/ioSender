
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls
{
    public partial class MachinePositionFlyout : UserControl
    {
        public MachinePositionFlyout()
        {
            InitializeComponent();
        }

        private void btn_Close(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }
    }
}
