/*
 * JogBaseControl.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-26 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2021, Io Engineering (Terje Io)
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

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JogControl.xaml
    /// </summary>
    public partial class JogBaseControl : UserControl
    {
        private string mode = "G21"; // Metric
        private bool silent = false;
        private int distance = 2, feedrate = 2;
        private KeypressHandler keyboard;
        private static bool keyboardMappingsOk = false;

        private const Key xplus = Key.H, xminus = Key.J, yplus = Key.K, yminus = Key.L, zplus = Key.I, zminus = Key.M;

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
                    {
                        silent = true;
                        (DataContext as GrblViewModel).JogStep = JogData.Distance;
                        silent = false;
                    }
                    break;
            }
        }

        private void JogControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is GrblViewModel)
            {
                mode = GrblSettings.GetInteger(GrblSetting.ReportInches) == 0 ? "G21" : "G20";
                JogData.SetMetric(mode == "G21");

                if (!GrblInfo.HasFirmwareJog || AppConfig.Settings.Jog.LinkStepJogToUI)
                    JogData.PropertyChanged += JogData_PropertyChanged;

                if (!keyboardMappingsOk)
                {
                    keyboard = (DataContext as GrblViewModel).Keyboard;

                    keyboardMappingsOk = true;

                    if (AppConfig.Settings.Jog.Mode == JogConfig.JogMode.UI)
                    {
                        keyboard.AddHandler(Key.PageUp, ModifierKeys.None, cursorKeyJog, false);
                        keyboard.AddHandler(Key.PageDown, ModifierKeys.None, cursorKeyJog, false);
                        keyboard.AddHandler(Key.Left, ModifierKeys.None, cursorKeyJog, false);
                        keyboard.AddHandler(Key.Up, ModifierKeys.None, cursorKeyJog, false);
                        keyboard.AddHandler(Key.Right, ModifierKeys.None, cursorKeyJog, false);
                        keyboard.AddHandler(Key.Down, ModifierKeys.None, cursorKeyJog, false);
                    }

                    keyboard.AddHandler(xplus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);
                    keyboard.AddHandler(xminus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);
                    keyboard.AddHandler(yplus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);
                    keyboard.AddHandler(yminus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);
                    keyboard.AddHandler(zplus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);
                    keyboard.AddHandler(zminus, ModifierKeys.Control | ModifierKeys.Shift, normalKeyJog, false);

                    if (AppConfig.Settings.Jog.Mode != JogConfig.JogMode.Keypad)
                    {
                        keyboard.AddHandler(Key.End, ModifierKeys.None, endJog, false);

                        keyboard.AddHandler(Key.NumPad0, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad1, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad2, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad3, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad4, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad5, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad6, ModifierKeys.Control, adjustJog);
                        keyboard.AddHandler(Key.NumPad7, ModifierKeys.Control, adjustJog);

                        keyboard.AddHandler(Key.NumPad2, ModifierKeys.None, adjustJog2);
                        keyboard.AddHandler(Key.NumPad4, ModifierKeys.None, adjustJog2);
                        keyboard.AddHandler(Key.NumPad6, ModifierKeys.None, adjustJog2);
                        keyboard.AddHandler(Key.NumPad8, ModifierKeys.None, adjustJog2);
                    }
                }
            }
        }

        private void distance_Click(object sender, RoutedEventArgs e)
        {
            distance = int.Parse((string)(sender as RadioButton).Tag);
        }

        private void feedrate_Click(object sender, RoutedEventArgs e)
        {
            feedrate = int.Parse((string)(sender as RadioButton).Tag);
        }

        private bool endJog(Key key)
        {
            if(!keyboard.IsRepeating && keyboard.IsJogging)
                JogCommand("stop");

            return keyboard.IsJogging;
        }

        private bool adjustJog(Key key)
        {
            switch (key)
            {
                case Key.NumPad0:
                    JogData.StepSize = JogViewModel.JogStep.Step0;
                    return true;

                case Key.NumPad1:
                    JogData.StepSize = JogViewModel.JogStep.Step1;
                    return true;

                case Key.NumPad2:
                    JogData.StepSize = JogViewModel.JogStep.Step2;
                    return true;

                case Key.NumPad3:
                    JogData.StepSize = JogViewModel.JogStep.Step3;
                    return true;

                case Key.NumPad4:
                    JogData.Feed = JogViewModel.JogFeed.Feed0;
                    return true;

                case Key.NumPad5:
                    JogData.Feed = JogViewModel.JogFeed.Feed1;
                    return true;

                case Key.NumPad6:
                    JogData.Feed = JogViewModel.JogFeed.Feed2;
                    return true;

                case Key.NumPad7:
                    JogData.Feed = JogViewModel.JogFeed.Feed3;
                    return true;
            }

            return true;
        }

        private bool adjustJog2(Key key)
        {
            switch (key)
            {
                case Key.NumPad2:
                    JogData.FeedDec();
                    return true;

                case Key.NumPad4:
                    JogData.StepDec();
                    return true;

                case Key.NumPad6:
                    JogData.StepInc();
                    return true;

                case Key.NumPad8:
                    JogData.FeedInc();
                    return true;
            }

            return true;
        }

        private bool normalKeyJog(Key key)
        {
            if (keyboard.CanJog2 && !keyboard.IsRepeating) switch (key)
            {
                case zplus:
                    if (!GrblInfo.LatheModeEnabled)
                        JogCommand("Z+");
                    break;

                case zminus:
                    if (!GrblInfo.LatheModeEnabled)
                        JogCommand("Z-");
                    break;

                case xminus:
                    JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-");
                    break;

                case yplus:
                    JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+");
                    break;

                case xplus:
                    JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+");
                    break;

                case yminus:
                    JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-");
                    break;
            }

            return true;
        }

        private bool cursorKeyJog(Key key)
        {
            if(keyboard.CanJog && !keyboard.IsRepeating) switch (key)
            {
                case Key.PageUp:
                    if (!GrblInfo.LatheModeEnabled)
                        JogCommand("Z+");
                    break;

                case Key.PageDown:
                    if (!GrblInfo.LatheModeEnabled)
                        JogCommand("Z-");
                    break;

                case Key.Left:
                    JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-");
                    break;

                case Key.Up:
                    JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+");
                    break;

                case Key.Right:
                    JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+");
                    break;

                case Key.Down:
                    JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-");
                    break;
            }

            return keyboard.CanJog;
        }

        private void JogCommand(string cmd)
        {
            cmd = cmd == "stop" ? ((char)GrblConstants.CMD_JOG_CANCEL).ToString() : string.Format("$J=G91{0}{1}{2}F{3}", mode, cmd.Replace("+", ""), JogData.Distance.ToInvariantString(), Math.Ceiling(JogData.FeedRate).ToInvariantString());
            (DataContext as GrblViewModel).ExecuteCommand(cmd);
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
