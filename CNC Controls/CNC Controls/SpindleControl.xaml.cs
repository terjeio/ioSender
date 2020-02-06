/*
 * SpindleControl.xaml.cs - part of CNC Controls library
 *
 * v0.05 / 2020-02-01 / Io Engineering (Terje Io)
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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls
{
    public partial class SpindleControl : UserControl
    {
        private double _rpm = 0.0d;
        private bool hold = false;

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
                case nameof(GrblViewModel.SpindleState):
                    var p = (GrblViewModel)sender;
                    hold = p.IsJobRunning || p.GrblState.State == GrblStates.Hold || p.GrblState.State == GrblStates.Door;
                    cvRPM.IsEnabled = !hold && p.SpindleState.Value == SpindleState.Off;
                    break;

                case nameof(GrblViewModel.ActualRPM):
                case nameof(GrblViewModel.ProgrammedRPM):
                    ProgrammedRPM = (sender as GrblViewModel).ProgrammedRPM;
                    if (!double.IsNaN((sender as GrblViewModel).ActualRPM))
                        RPM = (sender as GrblViewModel).ActualRPM;
                    else
                        RPM = ProgrammedRPM;
                break;
            }
        }

        public static readonly DependencyProperty IsSpindleStateEnabledProperty = DependencyProperty.Register(nameof(IsSpindleStateEnabled), typeof(bool), typeof(SpindleControl), new PropertyMetadata(false, new PropertyChangedCallback(IsSpindleStateEnableChanged)));
        public bool IsSpindleStateEnabled
        {
            get { return (bool)GetValue(IsSpindleStateEnabledProperty); }
            set { SetValue(IsSpindleStateEnabledProperty, value); }
        }
        private static void IsSpindleStateEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SpindleControl sc = (SpindleControl)d;
            sc.rbSpindleOff.IsEnabled = sc.rbSpindleCW.IsEnabled = sc.rbSpindleCCW.IsEnabled = sc.cvRPM.IsEnabled = (bool)e.NewValue;
        }

        public string SpindleOffCommand { get { return (string)rbSpindleOff.Tag; } set { rbSpindleOff.Tag = value; } }
        public string SpindleCWCommand { get { return (string)rbSpindleCW.Tag; } set { rbSpindleCW.Tag = value; } }
        public string SpindleCCWCommand { get { return (string)rbSpindleCCW.Tag; } set { rbSpindleCCW.Tag = value; } }

        public double RPM
        {
            get { return cvRPM.Value; }
            set { cvRPM.Value = cvRPM.IsReadOnly && value > 0.0d ? value : _rpm; }
        }

        public double ProgrammedRPM
        {
            get { return _rpm; }
            set { if (value > 0.0f) _rpm = value; }
        }

        public new bool IsFocused { get { return cvRPM.IsFocused; } }

        private void rbSpindle_Click(object sender, RoutedEventArgs e)
        {
            var p = (GrblViewModel)DataContext;

            if (p.SpindleState.Value != SpindleState.Off)
                _rpm = cvRPM.Value;

            if (hold)
                (DataContext as GrblViewModel).ExecuteCommand(((char)GrblConstants.CMD_SPINDLE_OVR_STOP).ToString());
            else
                (DataContext as GrblViewModel).ExecuteCommand(string.Format((string)((RadioButton)sender).Tag, "S" + cvRPM.Value.ToInvariantString()));
        }

        void overrideControl_CommandGenerated(string command)
        {
            (DataContext as GrblViewModel).ExecuteCommand(command);
        }
    }
}