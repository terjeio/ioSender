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

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for TaperControl.xaml
    /// </summary>
    public partial class TaperControl : UserControl
    {
        public TaperControl()
        {
            InitializeComponent();
        }
        public static readonly DependencyProperty IsTaperEnabledProperty = DependencyProperty.Register(nameof(IsTaperEnabled), typeof(bool), typeof(TaperControl), new PropertyMetadata(/*"Label:"  , new PropertyChangedCallback(OnLabelChanged)*/));
        public bool IsTaperEnabled
        {
            get { return (bool)GetValue(IsTaperEnabledProperty); }
            set { SetValue(IsTaperEnabledProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(TaperControl), new PropertyMetadata(/*"Label:"  , new PropertyChangedCallback(OnLabelChanged)*/));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
    }
}
