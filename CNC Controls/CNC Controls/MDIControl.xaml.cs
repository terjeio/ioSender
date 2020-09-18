/*
 * MDIControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.27 / 2020-09-17 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2020, Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;
using CNC.Core;

namespace CNC.Controls
{
    public partial class MDIControl : UserControl
    {
        public MDIControl()
        {
            InitializeComponent();

            Commands = new ObservableCollection<string>();
        }

        public new bool IsFocused { get { return txtMDI.IsKeyboardFocusWithin; } }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(string), typeof(MDIControl), new PropertyMetadata(""));
        public string Command
        {
            get { return (string)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(ObservableCollection<string>), typeof(MDIControl));
        public ObservableCollection<string> Commands
        {
            get { return (ObservableCollection<string>)GetValue(CommandsProperty); }
            set { SetValue(CommandsProperty, value); }
        }

        private void txtMDI_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return && (DataContext as GrblViewModel).MDICommand.CanExecute(null))
            {
                string cmd = (sender as ComboBox).Text;
                var model = DataContext as GrblViewModel;
                if (!string.IsNullOrEmpty(cmd) && (Commands.Count == 0 || Commands[0] != cmd))
                    Commands.Insert(0, cmd);
                if (model.GrblError != 0)
                    model.ExecuteCommand("");
                model.MDICommand.Execute(cmd);
                (sender as ComboBox).SelectedIndex = -1;
            }
        }

        private void txtMDI_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cmbTextBox = (TextBox)(sender as ComboBox).Template.FindName("PART_EditableTextBox", (sender as ComboBox));
            if (cmbTextBox != null)
            {
                cmbTextBox.Focus();
                cmbTextBox.CaretIndex = cmbTextBox.Text.Length;
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if ((DataContext as GrblViewModel).GrblError != 0)
                (DataContext as GrblViewModel).ExecuteCommand("");

            if (!string.IsNullOrEmpty(Command) && !Commands.Contains(Command))
                Commands.Insert(0, Command);
        }

        private void MDIControl_Loaded(object sender, RoutedEventArgs e)
        {
            var mdi = txtMDI.Template.FindName("PART_EditableTextBox", txtMDI) as TextBox;
            if(mdi != null)
                mdi.Tag = "MDI";
        }
    }
}
