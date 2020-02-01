using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for ProbeControl.xaml
    /// </summary>
    /// 

    public partial class ProbeControl : UserControl
    {
        //private double distance = 1d, feedrate = 500d;
        private bool initOK = false, sdStream = false;
        private GrblViewModel model;
        public ProbeControl()
        {
            InitializeComponent();
            
            DataContextChanged += View_DataContextChanged;
        }
        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= OnDataContextPropertyChanged;
            if (e.NewValue != null && e.NewValue is INotifyPropertyChanged)
                (e.NewValue as GrblViewModel).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel && Visibility == Visibility.Visible) switch (e.PropertyName)
                {
                    case nameof(GrblViewModel.StreamingState):
                        if ((sender as GrblViewModel).IsJobRunning)
                            Visibility = Visibility.Hidden;
                        break;

                    case nameof(GrblViewModel.ActiveView):
                        if ((sender as GrblViewModel).ActiveView != View.ViewType.GRBL)
                            Visibility = Visibility.Hidden;
                        break;
                }
        }

        public void clickStartProbeButton(object sender, RoutedEventArgs e, string s)
        {
            string cmd = "$J=G91 x1";
            //(model.data as GrblViewModel).ExecuteMDI(cmd);

            
            MessageBox.Show($"cmd: {cmd} {sender} {e} {s}");
            (DataContext as GrblViewModel).ExecuteMDI(cmd);
        }
    }
}