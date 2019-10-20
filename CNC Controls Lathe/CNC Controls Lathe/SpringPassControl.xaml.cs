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
    /// Interaction logic for SpringPassControl.xaml
    /// </summary>
    public partial class SpringPassControl : UserControl
    {
        public SpringPassControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsPassesEnabledProperty = DependencyProperty.Register("IsPassesEnabled", typeof(bool), typeof(SpringPassControl), new PropertyMetadata(/*"Label:"  , new PropertyChangedCallback(OnLabelChanged)*/));
        public bool IsPassesEnabled
        {
            get { return (bool)GetValue(IsPassesEnabledProperty); }
            set { SetValue(IsPassesEnabledProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(SpringPassControl), new PropertyMetadata(double.NaN));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
    }
}

