/*
 * JogControl.xaml.cs - part of CNC Controls library
 *
 * v0.30 / 2021-04-08 / Io Engineering (Terje Io)
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
using CNC.Core;
using System.Windows.Input;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JogControl.xaml
    /// </summary>
    public partial class JogControl : UserControl
    {
        private bool metricCommand = true, metricInput = true, silent = false;
        private int distance = 2, feedrate = 2;

        public JogControl()
        {
            InitializeComponent();

            JogData = new JogViewModel();

            JogData.PropertyChanged += JogData_PropertyChanged;

            Focusable = true;
        }

        private void JogData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(JogViewModel.Distance):
                    if (JogData.StepSize != JogViewModel.JogStep.Step3)
                    {
                        silent = true;
                        (DataContext as GrblViewModel).JogStep = JogData.Distance;
                        silent = false;
                    }
                    break;
            }                
        }

        public static JogViewModel JogData { get; private set; }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                JogData.SetMetric((metricInput = (DataContext as GrblViewModel).IsMetric));
                (DataContext as GrblViewModel).PropertyChanged += OnDataContextPropertyChanged;
            }
        }

        private void JogControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                GrblParserState.Get();
                metricCommand = GrblParserState.IsMetric;
                Focus();
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

                case nameof(GrblViewModel.IsMetric):
                    JogData.SetMetric((metricInput = (sender as GrblViewModel).IsMetric));
                    break;
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

        private void btn_Close(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

        private double convert (double val)
        {
            if (metricCommand)
            {
                if (!metricInput)
                    val *= 25.4d;
            }
            else if (metricInput)
                val /= 25.4d;

            return Math.Round(val, (DataContext as GrblViewModel).Precision);

        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            bool handled = Visibility == Visibility.Visible;

            if (handled && !e.IsRepeat) switch (e.Key)
            {
                case Key.PageUp:
                    if(!GrblInfo.LatheModeEnabled)
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

                case Key.NumPad2:
                    JogData.Feed = (JogData.Feed == JogViewModel.JogFeed.Feed0 ? JogViewModel.JogFeed.Feed0 : JogData.Feed - 1);
                    break;

               case Key.NumPad4:
                    JogData.StepSize = (JogData.StepSize == JogViewModel.JogStep.Step0 ? JogViewModel.JogStep.Step0 : JogData.StepSize - 1);
                    break;

                case Key.NumPad6:
                    JogData.StepSize = (JogData.StepSize == JogViewModel.JogStep.Step3 ? JogViewModel.JogStep.Step3 : JogData.StepSize + 1);
                    break;

                case Key.NumPad8:
                    JogData.Feed = (JogData.Feed == JogViewModel.JogFeed.Feed3 ? JogViewModel.JogFeed.Feed3 : JogData.Feed + 1);
                    break;

                default:
                    handled = false;
                    break;
            }

            if ((e.Handled = handled))
                Focus();
        }

        private void JogCommand(string cmd)
        {
            cmd = cmd == "stop" ? ((char)GrblConstants.CMD_JOG_CANCEL).ToString() : string.Format("$J=G91{0}{1}F{2}", cmd.Replace("+", ""), convert(JogData.Distance).ToInvariantString(), Math.Ceiling(convert(JogData.FeedRate)).ToInvariantString());
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

        // TODO: calculate sensible feedrates from grbl settings
        int[] feedrate_metric = new int[4] { 5, 100, 500, 1000 };
        double[] distance_metric = new double[4] { .01d, .1d, 1d, 10d };
        int[] feedrate_imperial = new int[4] { 5, 10, 50, 100 };
        double[] distance_imperial = new double[4] { .001d, .01d, .1d, 1d };
        JogStep _jogStep = JogStep.Step1;
        JogFeed _jogFeed = JogFeed.Feed1;
        private double[] _distance = new double[4];
        private int[] _feedRate = new int[4];

        public JogViewModel()
        {
            SetMetric(true);
        }

        public void SetMetric(bool on)
        {
            for (int i = 0; i < _feedRate.Length; i++) {
                _distance[i] = on ? distance_metric[i] : distance_imperial[i];
                _feedRate[i] = on ? feedrate_metric[i] : feedrate_imperial[i];
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

        public void StepInc ()
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
