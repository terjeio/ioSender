/*
 * OverrideControl.xaml.cs - part of CNC Controls library
 *
 * v0.46 / 2025-05-13 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2024, Io Engineering (Terje Io)
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

using System;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    public partial class OverrideControl : UserControl
    {
        private double lastValue;

        public delegate void CommandGeneratedHandler(byte[] commands, int len);
        public event CommandGeneratedHandler CommandGenerated;

        public OverrideControl()
        {
            InitializeComponent();
        }

        public byte ResetCommand { set; get; }
        public byte FinePlusCommand { set; get; }
        public byte FineMinusCommand { set; get; }
        public byte CoarsePlusCommand { set; get; }
        public byte CoarseMinusCommand { set; get; }

        #region dependencyproperties

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(OverrideControl), new PropertyMetadata(10));
        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(OverrideControl), new PropertyMetadata(200));
        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty TicksProperty = DependencyProperty.Register(nameof(Ticks), typeof(System.Windows.Media.DoubleCollection), typeof(OverrideControl));
        public System.Windows.Media.DoubleCollection Ticks
        {
            get { return (System.Windows.Media.DoubleCollection)GetValue(TicksProperty); }
            set { SetValue(TicksProperty, value); }
        }

        public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(nameof(TickFrequency), typeof(int), typeof(OverrideControl), new PropertyMetadata(1));
        public int TickFrequency
        {
            get { return (int)GetValue(TickFrequencyProperty); }
            set { SetValue(TickFrequencyProperty, value); }
        }

        public static readonly DependencyProperty SliderValueProperty = DependencyProperty.Register(nameof(SliderValue), typeof(double), typeof(OverrideControl), new PropertyMetadata(0d, new PropertyChangedCallback(OnSliderValueChanged)));
        public double SliderValue
        {
            get { return (double)GetValue(SliderValueProperty); }
            set { SetValue(SliderValueProperty, value); }
        }
        private static void OnSliderValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OverrideControl)d).txtOverride.Text = Math.Round((double)e.NewValue).ToString() + "%";
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(OverrideControl), new PropertyMetadata(0d, new PropertyChangedCallback(OnValueChanged)));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OverrideControl)d).SliderValue = Math.Round((double)e.NewValue);
        }

        public static readonly DependencyProperty EncoderModeProperty = DependencyProperty.Register(nameof(EncoderMode), typeof(GrblEncoderMode), typeof(OverrideControl), new PropertyMetadata(GrblEncoderMode.Unknown));
        public GrblEncoderMode EncoderMode
        {
            get { return (GrblEncoderMode)GetValue(EncoderModeProperty); }
            set { SetValue(EncoderModeProperty, value); }
        }

        #endregion

        private void Slider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            int len = 0;
            byte[] cmd = new byte[30];

            if (FinePlusCommand == 0) // Rapids override
            {
                switch((int)SliderValue)
                {
                    case 25:
                        cmd.SetValue(CoarseMinusCommand, len++);
                        break;
                    case 50:
                        cmd.SetValue(FineMinusCommand, len++);
                        break;
                    default:
                        cmd.SetValue(ResetCommand, len++);
                        break;
                }
            } else {

                double coarseDelta = Math.Round(SliderValue) - Value, fineDelta = coarseDelta % 10d;
                byte coarseCmd = coarseDelta < 0d ? CoarseMinusCommand : CoarsePlusCommand,
                     fineCmd = fineDelta < 0d ? FineMinusCommand : FinePlusCommand;

                coarseDelta = Math.Abs(coarseDelta - fineDelta);
                fineDelta = Math.Abs(fineDelta);

                while (coarseDelta != 0d)
                {
                    cmd.SetValue(coarseCmd, len++);
                    coarseDelta -= 10d;
                }
                while (fineDelta != 0d)
                {
                    cmd.SetValue(fineCmd, len++);
                    fineDelta -= 1d;
                }
            }

            if(cmd.Length > 0)
                CommandGenerated?.Invoke(cmd, len);
        }

        void btnOverrideClick(object sender, EventArgs e)
        {
            byte[] cmd = new byte[] { ResetCommand };

            CommandGenerated?.Invoke(cmd, 1);
        }

        private void Slider_GotMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lastValue = Math.Round(Value);
        }
    }
}
