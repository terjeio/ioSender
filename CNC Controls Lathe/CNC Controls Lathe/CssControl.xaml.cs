
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for CssControl.xaml
    /// </summary>
    public partial class CssControl : UserControl
    {
        public CssControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsCssEnabledProperty = DependencyProperty.Register(nameof(IsCssEnabled), typeof(bool), typeof(CssControl), new PropertyMetadata(new PropertyChangedCallback(OnCssEnabledChanged)));
        public bool? IsCssEnabled
        {
            get { return (bool?)GetValue(IsCssEnabledProperty); }
            set { SetValue(IsCssEnabledProperty, value); }
        }

        private static void OnCssEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CssControl)d).data.Label = ((CssControl)d).IsCssEnabled == true ? "Speed:" : "Spindle:";
            ((CssControl)d).data.Unit = ((CssControl)d).IsCssEnabled == true ? "m/min" : "RPM";
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CssControl), new PropertyMetadata(double.NaN));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
    }
}
