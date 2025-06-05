/*
 * StepperCalibrationWizard.xaml.cs - part of CNC Controls library
 *
 * v0.46 / 2025-05-14 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2025, Io Engineering (Terje Io)
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

using CNC.Core;
using CNC.GCode;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for StepperCalibrationWizard.xaml
    /// </summary>
    public partial class StepperCalibrationWizard : UserControl, IGrblConfigTab
    {
        private bool runExecuted = false, wasMetric, initialDirectionPositive = false;
        private DistanceMode distanceMode;
        private GrblSettingDetails setting;
        private GrblViewModel model = null;
        private const double backlashComp = 0.5d;
        int last = 0;
        public StepperCalibrationWizard()
        {
            InitializeComponent();

            model = DataContext as GrblViewModel;
        }

        #region Methods required by GrblConfigTab interface

        public GrblConfigType GrblConfigType { get { return GrblConfigType.StepperCalibration; } }

        public void Activate(bool activate)
        {
            if (activate)
            {
                model.PropertyChanged += Model_PropertyChanged;
                Axis = last == 0 ? 1 : 0; // force update
                getAxisDetails((Axis = last));

                if (GrblInfo.IsGrblHAL)
                {
                    GrblParserState.Get();
                    GrblWorkParameters.Get();
                }
                else
                    GrblParserState.Get(true);

                wasMetric = GrblParserState.IsMetric;
                distanceMode = GrblParserState.DistanceMode;
                ActualDistance = Distance;
            }
            else
            {
                last = Axis;
                model.PropertyChanged -= Model_PropertyChanged;

                if(!wasMetric)
                    model.ExecuteCommand("G20");

                model.ExecuteCommand(distanceMode == DistanceMode.Absolute ? "G90" : "G91");
            }

            model.Poller.SetState(activate ? AppConfig.Settings.Base.PollInterval : 0);
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GrblViewModel.GrblState)) switch (model.GrblState.State)
            {
                case GrblStates.Run:
                    runExecuted = true;
                    break;

                case GrblStates.Idle:
                    CanUpdate = runExecuted && Distance != 0d;
                    runExecuted = false;
                    break;

                default:
                    runExecuted = false;
                    break;
            }
        }

        #endregion

        private void getAxisDetails (int axis)
        {
            int index = GrblInfo.AxisLetterToIndex(model.Axes[axis].Letter);

            CanUpdate = false;
            setting = GrblSettings.Get(GrblSetting.MaxTravelBase + index);
            DistanceUnit = setting.Unit;
            setting = GrblSettings.Get(GrblSetting.TravelResolutionBase + index);
            Resolution = dbl.Parse(setting.Value);
            ResolutionUnit = setting.Unit;
        }

        public static readonly DependencyProperty AxisProperty = DependencyProperty.Register(nameof(Axis), typeof(int), typeof(StepperCalibrationWizard), new PropertyMetadata(0, new PropertyChangedCallback(OnAxisChanged)));
        public int Axis
        {
            get { return (int)GetValue(AxisProperty); }
            set { SetValue(AxisProperty, value); }
        }
        private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StepperCalibrationWizard)d).getAxisDetails((int)e.NewValue);
        }

        public static readonly DependencyProperty CanUpdateProperty = DependencyProperty.Register(nameof(CanUpdate), typeof(bool), typeof(StepperCalibrationWizard), new PropertyMetadata(false));
        public bool CanUpdate
        {
            get { return (bool)GetValue(CanUpdateProperty); }
            set { SetValue(CanUpdateProperty, value); }
        }

        public static readonly DependencyProperty DistanceProperty = DependencyProperty.Register(nameof(Distance), typeof(double), typeof(StepperCalibrationWizard), new PropertyMetadata(100d, new PropertyChangedCallback(OnDistanceChanged)));
        public double Distance
        {
            get { return (double)GetValue(DistanceProperty); }
            set { SetValue(DistanceProperty, value); }
        }
        private static void OnDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StepperCalibrationWizard)d).CanUpdate = false;
            ((StepperCalibrationWizard)d).ActualDistance = (double)e.NewValue;
        }

        public static readonly DependencyProperty DistanceUnitProperty = DependencyProperty.Register(nameof(DistanceUnit), typeof(string), typeof(StepperCalibrationWizard), new PropertyMetadata(string.Empty));
        public string DistanceUnit
        {
            get { return (string)GetValue(DistanceUnitProperty); }
            set { SetValue(DistanceUnitProperty, value); }
        }

        public static readonly DependencyProperty ActualDistanceProperty = DependencyProperty.Register(nameof(ActualDistance), typeof(double), typeof(StepperCalibrationWizard), new PropertyMetadata(100d, new PropertyChangedCallback(OnActualDistanceChanged)));
        public double ActualDistance
        {
            get { return (double)GetValue(ActualDistanceProperty); }
            set { SetValue(ActualDistanceProperty, value); }
        }
        private static void OnActualDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (StepperCalibrationWizard)d;

            if (!instance.CanUpdate)
                instance.Resolution = dbl.Parse(instance.setting.Value);
            else if (!double.IsInfinity((double)e.NewValue) && !double.IsNaN((double)e.NewValue))
                instance.Resolution = Math.Round(dbl.Parse(instance.setting.Value) / (double)e.NewValue * instance.Distance, GrblInfo.IsGrblHAL ? 6 : 3);
        }

        public static readonly DependencyProperty ResolutionProperty = DependencyProperty.Register(nameof(Resolution), typeof(double), typeof(StepperCalibrationWizard), new PropertyMetadata(0d));
        public double Resolution
        {
            get { return (double)GetValue(ResolutionProperty); }
            set { SetValue(ResolutionProperty, value); }
        }

        public static readonly DependencyProperty ResolutionUnitProperty = DependencyProperty.Register(nameof(ResolutionUnit), typeof(string), typeof(StepperCalibrationWizard), new PropertyMetadata(string.Empty));
        public string ResolutionUnit
        {
            get { return (string)GetValue(ResolutionUnitProperty); }
            set { SetValue(ResolutionUnitProperty, value); }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ((string)(sender as Button).Tag == "stop")
                Comms.com.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STOP : GrblConstants.CMD_RESET);
            else if ((string)(sender as Button).Tag == "save")
            {
                setting.Value = Resolution.ToInvariantString();
                if (GrblSettings.Save())
                    ActualDistance = Distance;
            }
            else
            {
                double distance = Distance;
                bool directionPositive = (string)(sender as Button).Tag == "+";

                if (!CanUpdate)
                    initialDirectionPositive = directionPositive;

                if (initialDirectionPositive != directionPositive)
                    distance += backlashComp;

                (DataContext as GrblViewModel).ExecuteCommand(string.Format("G21G91G0{0}{1}", GrblInfo.AxisIndexToLetter(Axis), (directionPositive ? distance : -distance).ToInvariantString()));
                if (Distance != distance)
                    (DataContext as GrblViewModel).ExecuteCommand(string.Format("G21G91G0{0}{1}", GrblInfo.AxisIndexToLetter(Axis), (directionPositive ? -backlashComp : backlashComp).ToInvariantString()));
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (model == null)
            {
                model = DataContext as GrblViewModel;
                txtWarnings.Text = ((string)FindResource("Warnings")).Replace("\\n", "\n");
                txtInstructions.Text = ((string)FindResource("Instructions1")).Replace("\\n", "\n") + "\n" +
                                        ((string)FindResource("Instructions2")).Replace("\\n", "\n") + "\n" +
                                         ((string)FindResource("Instructions3")).Replace("\\n", "\n") + "\n" +
                                          ((string)FindResource("Instructions4")).Replace("\\n", "\n") + "\n" +
                                           ((string)FindResource("Instructions5")).Replace("\\n", "\n") + "\n" +
                                            ((string)FindResource("Instructions6")).Replace("\\n", "\n") + "\n" +
                                             ((string)FindResource("Instructions7")).Replace("\\n", "\n");
            }
        }
    }
}
