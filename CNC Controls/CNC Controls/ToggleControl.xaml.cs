using System;
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for ToggleControl.xaml
    /// </summary>
    public partial class ToggleControl : UserControl
    {
        public event RoutedEventHandler Click;

        public ToggleControl()
        {
            InitializeComponent();

            tsw.Click += tsw_Click;
        }

        private void tsw_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleControl), new PropertyMetadata("Label"));
        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ToggleControl), new PropertyMetadata(false));
        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
    }
}
