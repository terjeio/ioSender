/*
 * JogFlyoutControl.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-25 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021, Io Engineering (Terje Io)
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

using System.ComponentModel;
using System.Windows;
using CNC.Core;

namespace CNC.Controls
{
    public partial class JogFlyoutControl : ISidebarControl
    {
        public JogFlyoutControl()
        {
            InitializeComponent();
        }

        public string MenuLabel { get { return (string)FindResource("MenuLabel"); } }

        private void btn_Close(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

        private void JogControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                (DataContext as GrblViewModel).PropertyChanged += OnDataContextPropertyChanged;
            }
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.StreamingState):
                    if (Visibility == Visibility.Visible && (sender as GrblViewModel).IsJobRunning)
                        Visibility = Visibility.Hidden;
                    break;
            }
        }
    }
}
