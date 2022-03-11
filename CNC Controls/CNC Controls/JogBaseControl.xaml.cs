/*
 * JogBaseControl.xaml.cs - part of CNC Controls library
 *
 * v0.37 / 2022-02-27 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2022, Io Engineering (Terje Io)
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
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JogControl.xaml
    /// </summary>
    public partial class JogBaseControl : UserControl
    {
        private string mode = "G21"; // Metric
        private bool softLimits = false;
        private int distance = 2, feedrate = 2, jogAxis = -1;
        private double limitSwitchesClearance = .5d, position = 0d;
        private KeypressHandler keyboard;
        private static bool keyboardMappingsOk = false;

        private const Key xplus = Key.J, xminus = Key.H, yplus = Key.K, yminus = Key.L, zplus = Key.I, zminus = Key.M, aplus = Key.U, aminus = Key.N;

        public JogBaseControl()
        {
            InitializeComponent();

            JogData = new JogViewModel();

            Focusable = true;
        }

        public static JogViewModel JogData { get; private set; }
        public string MenuLabel { get { return (string)FindResource("MenuLabel"); } }

        private void JogData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(JogViewModel.Distance):
                    if (AppConfig.Settings.Jog.Mode == JogConfig.JogMode.UI || (AppConfig.Settings.Jog.LinkStepJogToUI && JogData.StepSize != JogViewModel.JogStep.Step3))
                        (DataContext as GrblViewModel).JogStep = JogData.Distance;
                    break;
            }
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GrblViewModel.MachinePosition) || e.PropertyName == nameof(GrblViewModel.GrblState))
            {
                if ((sender as GrblViewModel).GrblState.State != GrblStates.Jog)
                    jogAxis = -1;
            }
        }

        private void JogControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is GrblViewModel)
            {
                mode = GrblSettings.GetInteger(GrblSetting.ReportInches) == 0 ? "G21" : "G20";
                softLimits = !(GrblInfo.IsGrblHAL && GrblSettings.GetInteger(grblHALSetting.SoftLimitJogging) == 1) && GrblSettings.GetInteger(GrblSetting.SoftLimitsEnable) == 1;
                limitSwitchesClearance = GrblSettings.GetDouble(GrblSetting.HomingPulloff);

                JogData.SetMetric(mode == "G21");

                if (!keyboardMappingsOk)
                {
                    if (!GrblInfo.HasFirmwareJog || AppConfig.Settings.Jog.LinkStepJogToUI)
                        JogData.PropertyChanged += JogData_PropertyChanged;

                    if (softLimits)
                        (DataContext as GrblViewModel).PropertyChanged += Model_PropertyChanged;

                    keyboard = (DataContext as GrblViewModel).Keyboard;

                    keyboardMappingsOk = true;

                    if (AppConfig.Settings.Jog.Mode == JogConfig.JogMode.UI)
                    {
                        keyboard.AddHandler(Key.PageUp, ModifierKeys.None, CursorJogZplus, false);
                        keyboard.AddHandler(Key.PageDown, ModifierKeys.None, CursorJogZminus, false);
                        keyboard.AddHandler(Key.Left, ModifierKeys.None, CursorJogXminus, false);
                        keyboard.AddHandler(Key.Up, ModifierKeys.None, CursorJogYplus, false);
                        keyboard.AddHandler(Key.Right, ModifierKeys.None, CursorJogXplus, false);
                        keyboard.AddHandler(Key.Down, ModifierKeys.None, CursorJogYminus, false);
                    }

                    keyboard.AddHandler(xplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogXplus, false);
                    keyboard.AddHandler(xminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogXminus, false);
                    keyboard.AddHandler(yplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogYplus, false);
                    keyboard.AddHandler(yminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogYminus, false);
                    keyboard.AddHandler(zplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogZplus, false);
                    keyboard.AddHandler(zminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogZminus, false);
                    if(GrblInfo.AxisFlags.HasFlag(AxisFlags.A)) {
                        keyboard.AddHandler(aplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogAplus, false);
                        keyboard.AddHandler(aminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogAminus, false);
                    }

                    if (AppConfig.Settings.Jog.Mode != JogConfig.JogMode.Keypad)
                    {
                        keyboard.AddHandler(Key.End, ModifierKeys.None, EndJog, false);

                        keyboard.AddHandler(Key.NumPad0, ModifierKeys.Control, JogStep0);
                        keyboard.AddHandler(Key.NumPad1, ModifierKeys.Control, JogStep1);
                        keyboard.AddHandler(Key.NumPad2, ModifierKeys.Control, JogStep2);
                        keyboard.AddHandler(Key.NumPad3, ModifierKeys.Control, JogStep3);
                        keyboard.AddHandler(Key.NumPad4, ModifierKeys.Control, JogFeed0);
                        keyboard.AddHandler(Key.NumPad5, ModifierKeys.Control, JogFeed1);
                        keyboard.AddHandler(Key.NumPad6, ModifierKeys.Control, JogFeed2);
                        keyboard.AddHandler(Key.NumPad7, ModifierKeys.Control, JogFeed3);

                        keyboard.AddHandler(Key.NumPad2, ModifierKeys.None, FeedDec);
                        keyboard.AddHandler(Key.NumPad4, ModifierKeys.None, StepDec);
                        keyboard.AddHandler(Key.NumPad6, ModifierKeys.None, StepInc);
                        keyboard.AddHandler(Key.NumPad8, ModifierKeys.None, FeedInc);
                    }
                }
            }
        }

        private bool KeyJogXplus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+");

            return true;
        }

        private bool KeyJogXminus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-");

            return true;
        }

        private bool KeyJogYplus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+");

            return true;
        }

        private bool KeyJogYminus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-");

            return true;
        }

        private bool KeyJogZplus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating && !GrblInfo.LatheModeEnabled)
                JogCommand("Z+");

            return true;
        }

        private bool KeyJogZminus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating && !GrblInfo.LatheModeEnabled)
                JogCommand("Z-");

            return true;
        }

        private bool KeyJogAplus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand("A+");

            return true;
        }

        private bool KeyJogAminus(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating)
                JogCommand("A-");

            return true;
        }

        private bool CursorJogXplus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+");

            return true;
        }

        private bool CursorJogXminus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-");

            return true;
        }

        private bool CursorJogYplus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+");

            return true;
        }

        private bool CursorJogYminus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating)
                JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-");

            return true;
        }

        private bool CursorJogZplus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating && !GrblInfo.LatheModeEnabled)
                JogCommand("Z+");

            return true;
        }

        private bool CursorJogZminus(Key key)
        {
            if (keyboard.CanJog && !keyboard.IsRepeating && !GrblInfo.LatheModeEnabled)
                JogCommand("Z-");

            return true;
        }

        private void distance_Click(object sender, RoutedEventArgs e)
        {
            distance = int.Parse((string)(sender as RadioButton).Tag);
        }

        private void feedrate_Click(object sender, RoutedEventArgs e)
        {
            feedrate = int.Parse((string)(sender as RadioButton).Tag);
        }

        private bool EndJog(Key key)
        {
            if(!keyboard.IsRepeating && keyboard.IsJogging)
                JogCommand("stop");

            return keyboard.IsJogging;
        }

        private bool JogStep0(Key key)
        {
            JogData.StepSize = JogViewModel.JogStep.Step0;

            return true;
        }

        private bool JogStep1(Key key)
        {
            JogData.StepSize = JogViewModel.JogStep.Step1;

            return true;
        }

        private bool JogStep2(Key key)
        {
            JogData.StepSize = JogViewModel.JogStep.Step2;

            return true;
        }

        private bool JogStep3(Key key)
        {
            JogData.StepSize = JogViewModel.JogStep.Step3;

            return true;
        }

        private bool JogFeed0(Key key)
        {
            JogData.Feed = JogViewModel.JogFeed.Feed0;

            return true;
        }

        private bool JogFeed1(Key key)
        {
            JogData.Feed = JogViewModel.JogFeed.Feed1;

            return true;
        }
        private bool JogFeed2(Key key)
        {
            JogData.Feed = JogViewModel.JogFeed.Feed2;

            return true;
        }
        private bool JogFeed3(Key key)
        {
            JogData.Feed = JogViewModel.JogFeed.Feed3;

            return true;
        }

        private bool FeedDec(Key key)
        {
            JogData.FeedDec();

            return true;
        }
        private bool FeedInc(Key key)
        {
            JogData.FeedInc();

            return true;
        }

        private bool StepDec(Key key)
        {
            JogData.StepDec();

            return true;
        }

        private bool StepInc(Key key)
        {
            JogData.StepInc();

            return true;
        }

        private void JogCommand(string cmd)
        {
            GrblViewModel model = DataContext as GrblViewModel;

            if (cmd == "stop")
                cmd = ((char)GrblConstants.CMD_JOG_CANCEL).ToString();

            else {

                var distance = cmd[1] == '-' ? -JogData.Distance : JogData.Distance;

                if (softLimits)
                {
                    int axis = GrblInfo.AxisLetterToIndex(cmd[0]);

                    if (jogAxis != -1 && axis != jogAxis)
                        return;

                    if (axis != jogAxis)
                        position = distance + model.MachinePosition.Values[axis];
                    else
                        position += distance;

                    if (GrblInfo.ForceSetOrigin)
                    {
                        if (!GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(axis)))
                        {
                            if (position > 0d)
                                position = 0d;
                            else if (position < (-GrblInfo.MaxTravel.Values[axis] + limitSwitchesClearance))
                                position = (-GrblInfo.MaxTravel.Values[axis] + limitSwitchesClearance);
                        }
                        else
                        {
                            if (position < 0d)
                                position = 0d;
                            else if (position > (GrblInfo.MaxTravel.Values[axis] - limitSwitchesClearance))
                                position = GrblInfo.MaxTravel.Values[axis] - limitSwitchesClearance;
                        }
                    }
                    else
                    {
                        if (position > -limitSwitchesClearance)
                            position = -limitSwitchesClearance;
                        else if (position < -(GrblInfo.MaxTravel.Values[axis] - limitSwitchesClearance))
                            position = -(GrblInfo.MaxTravel.Values[axis] - limitSwitchesClearance);
                    }

                    if (position == 0d)
                        return;

                    jogAxis = axis;

                    cmd = string.Format("$J=G53{0}{1}{2}F{3}", mode, cmd.Substring(0, 1), position.ToInvariantString(), Math.Ceiling(JogData.FeedRate).ToInvariantString());
                } else
                    cmd = string.Format("$J=G91{0}{1}{2}F{3}", mode, cmd.Substring(0, 1), distance.ToInvariantString(), Math.Ceiling(JogData.FeedRate).ToInvariantString());
            }

            model.ExecuteCommand(cmd);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            JogCommand((string)(sender as Button).Tag == "stop" ? "stop" : (string)(sender as Button).Content);
        }
    }

    internal class ArrayValues<T> : ViewModelBase
    {
        private T[] arr = new T[4];

        public int Length { get { return arr.Length; } }

        public T this[int i]
        {
            get { return arr[i]; }
            set
            {
                if (!value.Equals(arr[i]))
                {
                    arr[i] = value;
                    OnPropertyChanged(i.ToString());
                }
            }
        }
    }

    public class JogViewModel : ViewModelBase
    {
        public enum JogStep
        {
            Step0 = 0,
            Step1,
            Step2,
            Step3
        }
        public enum JogFeed
        {
            Feed0 = 0,
            Feed1,
            Feed2,
            Feed3
        }

        JogStep _jogStep = JogStep.Step1;
        JogFeed _jogFeed = JogFeed.Feed1;
        private double[] _distance = new double[4];
        private int[] _feedRate = new int[4];

        public void SetMetric(bool on)
        {
            for (int i = 0; i < _feedRate.Length; i++)
            {
                _distance[i] = on ? AppConfig.Settings.JogUiMetric.Distance[i] : AppConfig.Settings.JogUiImperial.Distance[i];
                _feedRate[i] = on ? AppConfig.Settings.JogUiMetric.Feedrate[i] : AppConfig.Settings.JogUiImperial.Feedrate[i];
                OnPropertyChanged("Feedrate" + i.ToString());
                OnPropertyChanged("Distance" + i.ToString());
            }
        }

        public JogStep StepSize { get { return _jogStep; } set { _jogStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(Distance)); } }
        public double Distance { get { return _distance[(int)_jogStep]; } }
        public JogFeed Feed { get { return _jogFeed; } set { _jogFeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(FeedRate)); } }
        public double FeedRate { get { return _feedRate[(int)_jogFeed]; } }

        public int Feedrate0 { get { return _feedRate[0]; } }
        public int Feedrate1 { get { return _feedRate[1]; } }
        public int Feedrate2 { get { return _feedRate[2]; } }
        public int Feedrate3 { get { return _feedRate[3]; } }

        public double Distance0 { get { return _distance[0]; } }
        public double Distance1 { get { return _distance[1]; } }
        public double Distance2 { get { return _distance[2]; } }
        public double Distance3 { get { return _distance[3]; } }

        public void StepInc()
        {
            if (StepSize != JogStep.Step3)
                StepSize += 1;
        }
        public void StepDec()
        {
            if (StepSize != JogStep.Step0)
                StepSize -= 1;
        }

        public void FeedInc()
        {
            if (Feed != JogFeed.Feed3)
                Feed += 1;
        }

        public void FeedDec()
        {
            if (Feed != JogFeed.Feed0)
                Feed -= 1;
        }
    }
}
