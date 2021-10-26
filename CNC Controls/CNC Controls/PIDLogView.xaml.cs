/*
 * PIDLogView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.31 / 2021-04-27 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Globalization;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for PIDLogView.xaml
    /// </summary>
    public partial class PIDLogView : UserControl, ICNCView
    {
        private double errorScale = 2500d;

        public PIDLogView()
        {
            InitializeComponent();

            sldError.Value = errorScale;
        }

        private void PIDLogView_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = new PIDLogViewModel();
            (DataContext as PIDLogViewModel).ErrorScale = 3;
            errorScale = (DataContext as PIDLogViewModel).ScaleFactors[3];
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.PIDTuner; } }
        public bool CanEnable { get { return true; } }

        public void Activate(bool activate, ViewType chgMode)
        {
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        private void sldError_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DataContext is PIDLogViewModel)
            {
                errorScale = (DataContext as PIDLogViewModel).ScaleFactors[(DataContext as PIDLogViewModel).ErrorScale];
                if (GrblPIDData.data.Rows.Count > 0)
                    PlotData(); // TODO: only plot on mouse up!
            }
        }

        private void btnGetPIDData_Click(object sender, RoutedEventArgs e)
        {
            btnGetPIDData.IsEnabled = false;
            GrblPIDData.Load();
            btnGetPIDData.IsEnabled = true;

            PlotData();
        }

        private void PlotData()
        {
            double center = PIDPlot.Height / 2d;
            double Xstep, Xpos;
            Point a = new Point(0d, center), b = new Point(0d, center);
            Point c = new Point(0d, center), d = new Point(0d, center);
            Point g = new Point(0d, center), h = new Point(0d, center);

            PIDPlot.Children.Clear();

            PIDPlot.Children.Add(new Line()
            {
                X1 = 0d,
                X2 = PIDPlot.Width,
                Y1 = center,
                Y2 = center,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5d,
                StrokeDashArray = new DoubleCollection() { 2d }
            });

            if (GrblPIDData.data.Rows.Count > 0)
            {
                Xpos = Xstep = this.PIDPlot.Width / GrblPIDData.data.Rows.Count;

                foreach (DataRow sample in GrblPIDData.data.Rows)
                {

                    b.Y = center - (int)((double)sample["Target"] * 5.0);

                    PIDPlot.Children.Add(new Line()
                    {
                        X1 = a.X,
                        X2 = b.X,
                        Y1 = a.Y,
                        Y2 = b.Y,
                        Stroke = Brushes.Green,
                        StrokeThickness = 1
                    });

                    a.X = b.X;
                    a.Y = b.Y;
                    b.X = (int)Math.Floor(Xpos);

                    d.Y = center - (int)((double)sample["Actual"] * 5.0);

                    PIDPlot.Children.Add(new Line()
                    {
                        X1 = c.X,
                        X2 = d.X,
                        Y1 = c.Y,
                        Y2 = d.Y,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 1
                    });

                    c.X = d.X;
                    c.Y = d.Y;
                    d.X = (int)Math.Floor(Xpos);

                    h.Y = center - (int)((double)sample["Error"] * errorScale);

                    PIDPlot.Children.Add(new Line()
                    {
                        X1 = g.X,
                        X2 = h.X,
                        Y1 = g.Y,
                        Y2 = h.Y,
                        Stroke = Brushes.Red,
                        StrokeThickness = 1
                    });

                    g.X = h.X;
                    g.Y = h.Y;
                    h.X = (int)Math.Floor(Xpos);

                    Xpos += Xstep;
                }
            }
        }
    }

    public static class GrblPIDData
    {
        public static DataTable data;
        private static string RawData;

        static GrblPIDData()
        {
            data = new DataTable("PIDData");

            data.Columns.Add("Id", typeof(int));
            data.Columns.Add("Target", typeof(double));
            data.Columns.Add("Actual", typeof(double));
            data.Columns.Add("Error", typeof(double));
            data.PrimaryKey = new DataColumn[] { data.Columns["Id"] };
        }

        public static void Load()
        {
            RawData = "";
            data.Clear();

            //data.ReadXml(CNC.Core.Resources.Path + "PIDLog.xml");

            Comms.com.DataReceived += Process;
            Comms.com.AwaitAck(((char)GrblConstants.CMD_PID_REPORT).ToString(CultureInfo.InvariantCulture));
            Comms.com.DataReceived -= Process;

            if (RawData != "")
            {
                int i = 0, s = 0; double target = 0.0, actual = 0.0;
                string[] header = RawData.Split('|');
                string[] samples = header[1].Split(',');
                foreach (string sample in samples)
                {
                    switch (s)
                    {
                        case 0:
                            target = dbl.Parse(sample);
                            s++;
                            break;

                        case 1:
                            actual = dbl.Parse(sample);
                            data.Rows.Add(new object[] { ++i, target, actual, Math.Round(actual - target, 3) });
                            s = 0;
                            break;
                    }
                }
            }
            //data.WriteXml(CNC.Core.Resources.Path + "PIDLog.xml");
        }

        private static void Process(string data)
        {
            if (data.StartsWith("[PID:"))
            {
                RawData = data.Substring(0, data.Length - 1).Substring(5);
            }
        }
    }

    public class PIDLogViewModel : ViewModelBase
    {
        private int _errorScale;
        private double[] _grdLabels = new double[4];
        private double[] _scaleFactors = new double[]{ 100d, 200d, 1000d, 2000d, 5000d, 10000d };

        public int ErrorScale
        {
            get { return _errorScale; }
            set
            {
                _errorScale = value;
                double f = _scaleFactors[_errorScale];
                OnPropertyChanged();
                GridLabel4 = 200000d / f;
                GridLabel3 = 200000d / f * .75d;
                GridLabel2 = 200000d / f * .5d;
                GridLabel1 = 200000d / f * .25d;
            }
        }

        public double GridLabel4 { get { return _grdLabels[3]; } private set { _grdLabels[3] = value; OnPropertyChanged(); } }
        public double GridLabel3 { get { return _grdLabels[2]; } private set { _grdLabels[2] = value; OnPropertyChanged(); } }
        public double GridLabel2 { get { return _grdLabels[1]; } private set { _grdLabels[1] = value; OnPropertyChanged(); } }
        public double GridLabel1 { get { return _grdLabels[0]; } private set { _grdLabels[0] = value; OnPropertyChanged(); } }

        public double[] ScaleFactors { get { return _scaleFactors; } }
    }
}
