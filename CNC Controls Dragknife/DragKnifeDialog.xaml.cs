using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CNC.Controls.DragKnife
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class DragKnifeDialog : Window
    {
        public DragKnifeDialog(DragKnifeViewModel model)
        {
            InitializeComponent();

            DataContext = model;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var parent = Application.Current.MainWindow;

            Left = parent.Left + (parent.Width - Width) / 2d;
            Top = parent.Top + (parent.Height - Height) / 2d;

            //try
            //{
            //    using (StreamReader reader = new StreamReader(CNC.Core.Resources.Path + (DataContext as JobParametersViewModel).Profile + suffix))
            //    {
            //        var settings = (JobParametersViewModel)new XmlSerializer(typeof(JobParametersViewModel)).Deserialize(reader);
            //        Copy.Properties(settings, DataContext as JobParametersViewModel);
            //    }
            //}
            //catch
            //{
            //}
        }
        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
