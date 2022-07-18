/*
 * SpindleControl.xaml.cs - part of CNC Controls library
 *
 * v0.40 / 2022-07-16 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
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
using System.Windows.Controls;
using CNC.Core;
using System.Windows.Input;

namespace CNC.Controls
{
    public partial class SpindleControl : UserControl
    {
        public SpindleControl()
        {
            InitializeComponent();

            DataContextChanged += SpindleControl_DataContextChanged;

            rbSpindleOff.Tag = "M5";
            rbSpindleCW.Tag = "M3{0}";
            rbSpindleCCW.Tag = "M4{0}";

            overrideControl.ResetCommand = GrblConstants.CMD_SPINDLE_OVR_RESET;
            overrideControl.FineMinusCommand = GrblConstants.CMD_SPINDLE_OVR_FINE_MINUS;
            overrideControl.FinePlusCommand = GrblConstants.CMD_SPINDLE_OVR_FINE_PLUS;
            overrideControl.CoarseMinusCommand = GrblConstants.CMD_SPINDLE_OVR_COARSE_MINUS;
            overrideControl.CoarsePlusCommand = GrblConstants.CMD_SPINDLE_OVR_COARSE_PLUS;

            cvRPM.PreviewKeyUp += txtPos_KeyPress;
            overrideControl.CommandGenerated += overrideControl_CommandGenerated;
        }

        private void SpindleControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= OnDataContextPropertyChanged;
            if (e.NewValue != null && e.NewValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.NewValue).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel)switch (e.PropertyName)
            {
                case nameof(GrblViewModel.GrblState):
                case nameof(GrblViewModel.IsJobRunning):
                    var p = sender as GrblViewModel;
                    IsSpindleStateEnabled = !p.IsJobRunning || p.GrblState.State == GrblStates.Hold || p.GrblState.State == GrblStates.Door;
                    break;
            }
        }

        public static readonly DependencyProperty IsSpindleStateEnabledProperty = DependencyProperty.Register(nameof(IsSpindleStateEnabled), typeof(bool), typeof(SpindleControl), new PropertyMetadata(false));
        public bool IsSpindleStateEnabled
        {
            get { return (bool)GetValue(IsSpindleStateEnabledProperty); }
            set { SetValue(IsSpindleStateEnabledProperty, value); }
        }

        public string SpindleOffCommand { get { return (string)rbSpindleOff.Tag; } set { rbSpindleOff.Tag = value; } }
        public string SpindleCWCommand { get { return (string)rbSpindleCW.Tag; } set { rbSpindleCW.Tag = value; } }
        public string SpindleCCWCommand { get { return (string)rbSpindleCCW.Tag; } set { rbSpindleCCW.Tag = value; } }

        public new bool IsFocused { get { return cvRPM.IsFocused; } }
        public bool SPOr { get { return !(DataContext as GrblViewModel).IsJobRunning || (DataContext as GrblViewModel).GrblState.State == GrblStates.Hold; } }

        private void txtPos_KeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !(DataContext as GrblViewModel).IsJobRunning)
            {
                (DataContext as GrblViewModel).ExecuteCommand(string.Format("S{0}", (sender as NumericTextBox).Value));
            }
        }

        private void rbSpindle_Click(object sender, RoutedEventArgs e)
        {
            var p = DataContext as GrblViewModel;

            if(p.IsJobRunning && p.GrblState.State == GrblStates.Hold)
                p.ExecuteCommand(((char)GrblConstants.CMD_SPINDLE_OVR_STOP).ToString());
            else
            {
                string rpm = p.ProgrammedRPM == 0d ? "S" + p.RPM.ToInvariantString() : "";
                (DataContext as GrblViewModel).ExecuteCommand(string.Format((string)((RadioButton)sender).Tag, rpm));
            }
        }

        void overrideControl_CommandGenerated(string command)
        {
            (DataContext as GrblViewModel).ExecuteCommand(command);
        }
    }
}