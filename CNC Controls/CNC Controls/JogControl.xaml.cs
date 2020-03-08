/*
 * JogControl.xaml.cs - part of CNC Controls library
 *
 * v0.10 / 2020-03-03 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
using System;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JogControl.xaml
    /// </summary>
    public partial class JogControl : UserControl
    {
        private bool metricCommand = true, metricInput = true;
        private int distance = 2, feedrate = 2;

        public JogControl()
        {
            InitializeComponent();

            JogData = new JogViewModel();

            IsVisibleChanged += JogControl_IsVisibleChanged;
        }

        public JogViewModel JogData { get; private set; }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            JogData.SetMetric((metricInput = (DataContext as GrblViewModel).IsMetric));
            (DataContext as GrblViewModel).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void JogControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                GrblParserState.Get();
                metricCommand = GrblParserState.IsMetric;
            }
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel && Visibility == Visibility.Visible) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.StreamingState):
                    if ((sender as GrblViewModel).IsJobRunning)
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string cmd = ((string)(sender as Button).Tag) == "stop" ? ((char)GrblConstants.CMD_JOG_CANCEL).ToString() : string.Format("$J =G91{0}{1}F{2}", ((string)(sender as Button).Content).Replace("+", ""), convert(JogData.Distance[distance]).ToInvariantString(), Math.Ceiling(convert(JogData.Feedrate[feedrate])).ToInvariantString());
            (DataContext as GrblViewModel).ExecuteCommand(cmd);
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
        // TODO: calculate sensible feedrates from grbl settings
        int[] feedrate_metric = new int[4] { 5, 100, 500, 1000 };
        double[] distance_metric = new double[4] { .01d, .1d, 1d, 10d };
        int[] feedrate_imperial = new int[4] { 5, 10, 50, 100 };
        double[] distance_imperial = new double[4] { .001d, .01d, .1d, 1d };

        internal ArrayValues<double> Distance { get; } = new ArrayValues<double>();
        internal ArrayValues<int> Feedrate { get; } = new ArrayValues<int>();

        public JogViewModel()
        {
            SetMetric(true);

            Distance.PropertyChanged += Values_PropertyChanged;
            Feedrate.PropertyChanged += Feedrate_PropertyChanged;
        }

        private void Feedrate_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Feedrate) + e.PropertyName);
        }

        private void Values_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Distance) + e.PropertyName);
        }

        public void SetMetric(bool on)
        {
            for (int i = 0; i < Feedrate.Length; i++) {
                Distance[i] = on ? distance_metric[i] : distance_imperial[i];
                Feedrate[i] = on ? feedrate_metric[i] : feedrate_imperial[i];
            }
        }

        public int Feedrate0 { get { return Feedrate[0]; } }
        public int Feedrate1 { get { return Feedrate[1]; } }
        public int Feedrate2 { get { return Feedrate[2]; } }
        public int Feedrate3 { get { return Feedrate[3]; } }

        public double Distance0 { get { return Distance[0]; } }
        public double Distance1 { get { return Distance[1]; } }
        public double Distance2 { get { return Distance[2]; } }
        public double Distance3 { get { return Distance[3]; } }
    }
}
