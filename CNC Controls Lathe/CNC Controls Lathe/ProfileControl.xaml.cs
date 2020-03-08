/*
 * ConfigControl.xaml.cs - part of CNC Controls Lathe
 *
 * v0.01 / 2019-10-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(ProfileData), typeof(ProfileControl), new PropertyMetadata());
        public string SelectedItem
        {
            get { return (string)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ProfileControl), new PropertyMetadata());
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
                new ProfileDialog(((BaseViewModel)DataContext).wz) { Owner = Application.Current.MainWindow }.ShowDialog();
        }
    }
}
