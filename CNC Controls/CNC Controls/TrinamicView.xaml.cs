/*
 * TrinamicView.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-19 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2021, Io Engineering (Terje Io)
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
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for TrinamicView.xaml
    /// </summary>
    public partial class TrinamicView : UserControl, ICNCView
    {
        private int sg_index = 0;
        private bool plot = false, read_status = false, grbl_reset = false, driver_reset = false;
        private GrblViewModel model = null;

        private List<Line> lines;

        public TrinamicView()
        {
            InitializeComponent();

            double ydelta = SGPlot.Height / 10;
            double ypos = SGPlot.Height - ydelta;

            lines = new List<Line>((int)SGPlot.Width);

            //int lval = 0;

            //while (ypos > 0)
            //{
            //    Label lbl;
            //    grid.Children.Add(lbl = new Label
            //    {
            //        Margin = new Thickness(40, 30 + ypos, 0, 0),
            //        Content = lval.ToString(),
            //        Width = 50,
            //        Height = 24,
            //    });
            //    Grid.SetColumn(lbl, 0);
            //    lval += 100;
            //    ypos -= ydelta;
            //}

            AxisEnabled.PropertyChanged += AxisEnabled_PropertyChanged;
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.TrinamicTuner; } }
        public bool CanEnable { get { return DataContext == null || !(DataContext as GrblViewModel).IsGCLock; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            Comms.com.WriteString(string.Format("M122S{0}H{1}\r", activate ? 1 : 0, SFiltEnabled == true ? 1 : 0));

            if (activate)
            {
                DataContext = model;
                model.OnResponseReceived += ProcessSGValue;
                model.PropertyChanged += OnDataContextPropertyChanged;
                var sgdetails = GrblSettings.Get(grblHALSetting.StallGuardBase + GrblInfo.AxisLetterToIndex(AxisEnabled.Value.ToString()));
                SGValue = int.Parse(sgdetails.Value);
                SGValueMin = (int)sgdetails.Min;
                SGValueMax = (int)sgdetails.Max;
                grbl_reset = false;
            }
            else
            {
                model.OnResponseReceived -= ProcessSGValue;
                model.PropertyChanged -= OnDataContextPropertyChanged;
                DataContext = null;
            }
            model.Poller.SetState(activate ? AppConfig.Settings.Base.PollInterval : 0);
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        #region dependencyproperties

        private void AxisEnabled_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (AxisEnabled.Value.ToString().Length == 1)
            {
                Comms.com.WriteString(string.Format("M122{0}S1\r", AxisEnabled.Value.ToString()));
                SGValue = GrblSettings.GetInteger(grblHALSetting.StallGuardBase + GrblInfo.AxisLetterToIndex(AxisEnabled.Value.ToString()));
            }
        }

        public static readonly DependencyProperty SFiltEnabledProperty = DependencyProperty.Register(nameof(SFiltEnabled), typeof(bool), typeof(TrinamicView), new PropertyMetadata(false, new PropertyChangedCallback(OnSFiltEnabledChanged)));
        public bool SFiltEnabled
        {
            get { return (bool)GetValue(SFiltEnabledProperty); }
            private set { SetValue(SFiltEnabledProperty, value); }
        }
        private static void OnSFiltEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Comms.com.WriteCommand(string.Format("M122H{0}", (bool)e.NewValue == true ? 1 : 0));
        }

        public static readonly DependencyProperty AxisEnabledProperty = DependencyProperty.Register(nameof(AxisEnabled), typeof(EnumFlags<AxisFlags>), typeof(TrinamicView), new PropertyMetadata(new EnumFlags<AxisFlags>(AxisFlags.X)));
        public EnumFlags<AxisFlags> AxisEnabled
        {
            get { return (EnumFlags<AxisFlags>)GetValue(AxisEnabledProperty); }
            private set { SetValue(AxisEnabledProperty, value); }
        }

        public static readonly DependencyProperty DriverStatusProperty = DependencyProperty.Register(nameof(DriverStatus), typeof(string), typeof(TrinamicView));
        public string DriverStatus
        {
            get { return (string)GetValue(DriverStatusProperty); }
            set { SetValue(DriverStatusProperty, value); }
        }

        public static readonly DependencyProperty SGValueMinProperty = DependencyProperty.Register(nameof(SGValueMin), typeof(int), typeof(TrinamicView), new PropertyMetadata(-64));
        public int SGValueMin
        {
            get { return (int)GetValue(SGValueMinProperty); }
            set { SetValue(SGValueMinProperty, value); }
        }

        public static readonly DependencyProperty SGValueMaxProperty = DependencyProperty.Register(nameof(SGValueMax), typeof(int), typeof(TrinamicView), new PropertyMetadata(63));
        public int SGValueMax
        {
            get { return (int)GetValue(SGValueMaxProperty); }
            set { SetValue(SGValueMaxProperty, value); }
        }

        public static readonly DependencyProperty SGValueProperty = DependencyProperty.Register(nameof(SGValue), typeof(int), typeof(TrinamicView));
        public int SGValue
        {
            get { return (int)GetValue(SGValueProperty); }
            set { SetValue(SGValueProperty, value); }
        }
        #endregion

        #region UIEvents

        private void TrinamicView_Loaded(object sender, RoutedEventArgs e)
        {
            if (model == null)
            {
                model = DataContext as GrblViewModel;
                DataContext = null;
            }
        }

        void btnGetState_Click(object sender, EventArgs e)
        {
            GetDriverStatus(AxisEnabled.Value.ToString());
        }

        void btnGetStateAll_Click(object sender, EventArgs e)
        {
            GetDriverStatus(string.Empty);
        }

        private void Slider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Comms.com.WriteString(string.Format("M914{0}{1}\r", AxisEnabled.Value.ToString(), SGValue));
        }

        void btnConfigureSGVal_Click(object sender, EventArgs e)
        {
            Comms.com.WriteString(string.Format("${0}={1}\r", (int)(grblHALSetting.StallGuardBase + GrblInfo.AxisLetterToIndex(AxisEnabled.Value.ToString())), SGValue));
        }

        #endregion

        private void GetDriverStatus (string axis)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();
            var model = DataContext as GrblViewModel;

            DriverStatus = "";

            Comms.com.PurgeQueue();

            model.Poller.SetState(0);
            model.SuspendProcessing = true;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => ProcessStatus(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    800, () => Comms.com.WriteCommand("M122" + axis));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            model.SuspendProcessing = false;
            model.Poller.SetState(AppConfig.Settings.Base.PollInterval);
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.MDI):
                    if (!(sender as GrblViewModel).MDI.ToUpper().StartsWith("M"))
                    {
                        PlotGrid();
                        sg_index = 0;
                        lines.Clear();
                    }
                    //else if ((sender as GrblViewModel).MDI.ToUpper().Replace(" ", "") == "M122I")
                    //     driver_reset = true;
                    break;

                case nameof(GrblViewModel.GrblReset):
                        grbl_reset = true;
                    break;

                case nameof(GrblViewModel.GrblState):
                    if (grbl_reset && (sender as GrblViewModel).GrblState.State == GrblStates.Idle)
                    {
                        Comms.com.WriteString(string.Format("M122S1H{0}\r", SFiltEnabled == true ? 1 : 0));
                            grbl_reset = false;
                    }
                    break;
            }
        }
    
        void PlotGrid ()
        {
            SGPlot.Children.Clear();
            SGPlot.Children.Add(new Line()
            {
                X1 = 0d,
                X2 = SGPlot.Width,
                Y1 = SGPlot.Height / 2d,
                Y2 = SGPlot.Height / 2d,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5d,
                StrokeDashArray = new DoubleCollection() { 2d }
            });

            double ydelta = SGPlot.Height / 10;
            double ypos = SGPlot.Height - ydelta;

            while (ypos > 0)
            {
                SGPlot.Children.Add(new Line()
                {
                    X1 = 0d,
                    X2 = SGPlot.Width,
                    Y1 = ypos,
                    Y2 = ypos,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 0.5d,
                    StrokeDashArray = new DoubleCollection() { 2d }
                });

                ypos -= ydelta;
            }
        }

        private void AddStatusData(string data)
        {
            DriverStatus += (data + "\r\n");
        }

        private void ProcessStatus(string data)
        {
            Action<string> addData = (s) => { AddStatusData(s); };

            if (data == "[TRINAMIC]")
                read_status = true;
            else if (data == "ok")
                read_status = false;
            else if (read_status)
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, addData, data);
        }

        private void PlotSGValue(int value)
        {
            if (lines.Count != (int)SGPlot.Width)
            {
                lines.Add(new Line()
                {
                    X1 = sg_index == 0 ? 0 : sg_index - 1,
                    X2 = sg_index,
                    Y1 = sg_index == 0 ? value : lines[sg_index - 1].Y2,
                    Y2 = value,
                    Stroke = Brushes.Blue,
                });

                SGPlot.Children.Add(lines[sg_index++]);
            }
            else
            {
                sg_index %= (int)SGPlot.Width;
                lines[sg_index].Y1 = sg_index == 0 ? value : lines[sg_index - 1].Y2;
                lines[sg_index++].Y2 = value;
            }
        }

        private void ProcessSGValue(string data)
        {
            if ((plot = data.StartsWith("[SG:")))
            {
                int sep = data.IndexOf(":");
                data = data.Substring(sep + 1, data.IndexOf("]") - sep - 1);

                int value = int.Parse(data) / 4;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => PlotSGValue(value)));
            }
        }
    }
}

