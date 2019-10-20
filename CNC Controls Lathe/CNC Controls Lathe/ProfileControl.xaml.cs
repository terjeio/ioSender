using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using CNC.View;
using System.Collections;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for ProfileControl.xaml
    /// </summary>
    public partial class ProfileControl : UserControl
    {
        public delegate void SelectedProfileChangedHandler(int id);
        public event SelectedProfileChangedHandler SelectedProfileChanged;

        WizardConfig options = null;
        ViewType mode;

        public ProfileControl()
        {
            InitializeComponent();
        }

        public void BindOptions(WizardConfig options, ViewType mode)
        {
            this.options = options;
            this.mode = mode;

            cbxProfile.DisplayMemberPath = "Name";
            cbxProfile.SelectedValuePath = "Id";
            cbxProfile.ItemsSource = options.Profiles;
            cbxProfile.SelectedValue = options.Profiles.First().Id;
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(ProfileData), typeof(ProfileControl), new PropertyMetadata());
        public string SelectedItem
        {
            get { return (string)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(ProfileControl), new PropertyMetadata());
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        private void cbxProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems.Count == 1)
                SelectedProfileChanged?.Invoke(((ProfileData)e.AddedItems[0]).Id);
        }

        private void btnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is BaseViewModel)
                new ProfileDialog(((BaseViewModel)DataContext).wz).ShowDialog();
        }
    }
}
