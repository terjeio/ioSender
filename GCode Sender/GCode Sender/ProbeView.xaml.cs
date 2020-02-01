
using System.Windows;
using System.Windows.Controls;

using CNC.View;
using CNC.Core;

using System.ComponentModel;

using CNC.Controls;


namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for Probe.xaml
    /// </summary>
    public partial class ProbeView : UserControl, CNCView
    {
        private bool initOK = false, sdStream = false;
        private GrblViewModel model;
        //public string zProbeDistance = "0.0";
        private ProbeControl pc;
        
        public ProbeView()
        {
            InitializeComponent();
            pc = new ProbeControl();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.AppConfig; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            
        }

        private void clickStartProbeButton_Click(object sender, RoutedEventArgs e)
        {
            
            pc.clickStartProbeButton(sender, e, zProbeDistance.Text);
        }

        public void CloseFile()
        {
        }

        #endregion
    }

}
