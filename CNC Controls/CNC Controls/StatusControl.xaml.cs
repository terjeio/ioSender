/*
 * StatusControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.36 / 2021-11-01 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2021, Io Engineering (Terje Io)
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
using System.Windows.Media;
using CNC.Core;

namespace CNC.Controls
{

    public partial class StatusControl : UserControl
    {
        private Brush HomeButtonColor;
        private enum StatusButton
        {
            Home,
            Unlock,
            Reset,
            Check
        }

        public StatusControl()
        {
            InitializeComponent();

            HomeButtonColor = btnHome.Background;

            btnHome.Tag = StatusButton.Home;
            btnReset.Tag = StatusButton.Reset;
            btnUnlock.Tag = StatusButton.Unlock;
            chkCheckMode.Tag = StatusButton.Check;
        }

        void btn_Click(object sender, RoutedEventArgs e)
        {
            switch ((StatusButton)((Control)sender).Tag)
            {
                case StatusButton.Reset:
                    var model = (DataContext as GrblViewModel);
                    if (model.GrblState.State == GrblStates.Alarm && model.GrblState.Substate == 10 && model.Signals.Value.HasFlag(Signals.EStop))
                        MessageBox.Show((string)FindResource("ClearEStop"), "ioSender", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    else
                        Grbl.Reset();
                    break;

                case StatusButton.Unlock:
                    (DataContext as GrblViewModel).ExecuteCommand(GrblConstants.CMD_UNLOCK);
                    break;

                case StatusButton.Home:
                    // ((Control)sender).Background = Brushes.LightSkyBlue;
                    (DataContext as GrblViewModel).ExecuteCommand(GrblConstants.CMD_HOMING);
                    break;

                case StatusButton.Check:
                    GrblStates state = (DataContext as GrblViewModel).GrblState.State;
                    if(state == GrblStates.Check && (sender as CheckBox).IsChecked == false)
                        Grbl.Reset();
                    else if (state == GrblStates.Idle && (sender as CheckBox).IsChecked == true)
                        (DataContext as GrblViewModel).ExecuteCommand(GrblConstants.CMD_CHECK);
                    break;
            }
        }
    }
}
