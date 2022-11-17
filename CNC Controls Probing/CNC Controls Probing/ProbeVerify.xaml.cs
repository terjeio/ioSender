/*
 * ProbingVerify.cs - part of CNC Probing library
 *
 * v0.41 / 2022-11-13 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2022, Io Engineering (Terje Io)
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
using System.Windows.Input;
using System.Windows.Threading;
using CNC.Core;

namespace CNC.Controls.Probing
{

    public partial class ProbeVerify : Window
    {
        public ProbeVerify(ProbingViewModel model)
        {
            InitializeComponent();

            DataContext = model;
            model.Grbl.PropertyChanged += Grbl_PropertyChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var parent = Application.Current.MainWindow;

            Left = parent.Left + (parent.Width - Width) / 2d;
            Top = parent.Top + (parent.Height - Height) / 2d;

            (sender as Window).Dispatcher.Invoke(new System.Action(() =>
            {
                (sender as Window).MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.ContextIdle);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            (DataContext as ProbingViewModel).Grbl.PropertyChanged -= Grbl_PropertyChanged;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled && e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
                Close();
        }

        private void Grbl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GrblViewModel.Signals) && (sender as GrblViewModel).Signals.Value.HasFlag(Signals.Probe))
            {
                (DataContext as ProbingViewModel).ProbeVerified = true;
                Close();
            }
        }
    }
}
