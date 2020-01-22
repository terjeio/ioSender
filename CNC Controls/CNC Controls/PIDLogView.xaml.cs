/*
 * PIDLogView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2019-10-27 / Io Engineering (Terje Io)
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
using CNC.View;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for PIDLogView.xaml
    /// </summary>
    public partial class PIDLogView : UserControl, CNCView
    {
        private double errorScale = 2500d;

        public PIDLogView()
        {
            InitializeComponent();

            sldError.Value = errorScale;
        }

        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.PIDTuner; } }

        public void Activate(bool activate, ViewType chgMode)
        {
        }

        public void CloseFile()
        {
        }

        #endregion

        private void sldError_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            errorScale = sldError.Value;
            PlotData(); // TODO: only plot on mouse up!
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

                    b.Y = center + (int)((double)sample["Target"] * 5.0);

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


                    d.Y = center + (int)((double)sample["Actual"] * 5.0);

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


                    h.Y = center + (int)((double)sample["Error"] * errorScale);

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
            data.WriteXml(CNC.Core.Resources.Path + @"\PIDLog.xml"); // For now...
        }

        private static void Process(string data)
        {
            if (data.StartsWith("[PID:"))
            {
                RawData = data.Substring(0, data.Length - 1).Substring(5);
            }
        }
    }
}
