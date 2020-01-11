
using System.Windows;
using System.Windows.Controls;
using CNC.View;

namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for ConfigView.xaml
    /// </summary>
    public partial class ConfigView : UserControl, CNCView
    {
        public ConfigView()
        {
            InitializeComponent();
        }

        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.AppConfig; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            cameraConfig.grblData = (CNC.Core.GrblViewModel)MainWindow.ui.DataContext;
            if(MainWindow.Camera == null || !MainWindow.Camera.HasCamera)
                cameraConfig.Visibility = Visibility.Collapsed;
        }

        public void CloseFile()
        {
        }

        #endregion

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Profile.Save();
        }
    }
}
