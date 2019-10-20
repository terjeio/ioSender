using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for CoordValueSetControl.xaml
    /// </summary>
    public partial class CoordValueSetControl : UserControl
    {
        private DependencyPropertyDescriptor label_dpd;

        public delegate void ClickHandler(object sender, RoutedEventArgs e);
        public event ClickHandler Click;

        public CoordValueSetControl()
        {
            InitializeComponent();

            label_dpd = DependencyPropertyDescriptor.FromProperty(CoordValueSetControl.LabelProperty, typeof(CoordValueSetControl));
            label_dpd.AddValueChanged(this, OnLabelChanged);
        }

        ~CoordValueSetControl()
        {
            label_dpd.RemoveValueChanged(this, OnLabelChanged);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(CoordValueSetControl), new PropertyMetadata(double.NaN));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public string Text
        {
            get
            {
                return cvValue.Text;
            }
            //set
            //{
            //    cvValue.Text = value;
            //}
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(CoordValueSetControl), new PropertyMetadata());

        public void OnLabelChanged(object sender, EventArgs args)
        {
            cvValue.Label = Label;
        }

        private void btnSet_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }
    }
}
