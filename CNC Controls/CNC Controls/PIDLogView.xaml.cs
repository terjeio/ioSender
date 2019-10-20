/*
 * PIDLogView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2019-09-26 / Io Engineering (Terje Io)
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CNC.Core;
using CNC.View;
using System.Globalization;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for PIDLogView.xaml
    /// </summary>
    public partial class PIDLogView : UserControl, CNCView
    {
        private double errorScale = 2500.0;

        public PIDLogView()
        {
            InitializeComponent();

            //btnGetPIDData.Click += new EventHandler(btnGetPIDData_Click);
            //PIDPlot.Paint += new PaintEventHandler(PIDPlot_Paint);
            //tbError.Value = (int)errorScale;
            //tbError.ValueChanged += new EventHandler(tbError_ValueChanged);
        }

        void tbError_ValueChanged(object sender, EventArgs e)
        {
            this.errorScale = sldError.Value;
            //   PIDPlot.Refresh();
        }

        //void PIDPlot_Paint(object sender, PaintEventArgs e)
        //{
        //    int center = this.PIDPlot.Height / 2;
        //    double Xstep, Xpos;
        //    Point a = new Point(0, center), b = new Point(0, center);
        //    Point c = new Point(0, center), d = new Point(0, center);
        //    Point g = new Point(0, center), h = new Point(0, center);
        //    Pen TargetPen = new Pen(System.Drawing.Color.Green);
        //    Pen ActualPen = new Pen(System.Drawing.Color.Blue);
        //    Pen ErrorPen = new Pen(System.Drawing.Color.Red);
        //    Pen BlackPen = new Pen(System.Drawing.Color.Black, 1);
        //    BlackPen.DashStyle = DashStyle.Dot;

        //    e.Graphics.Clear(this.PIDPlot.BackColor);
        //    e.Graphics.DrawLine(BlackPen, 0, center, this.PIDPlot.Width, center);

        //    if (GrblPIDData.data.Rows.Count > 0)
        //    {
        //        Xpos = Xstep = this.PIDPlot.Width / GrblPIDData.data.Rows.Count;
        //        foreach (DataRow sample in GrblPIDData.data.Rows)
        //        {
        //            b.Y = center + (int)((double)sample["Target"] * 5.0);
        //            e.Graphics.DrawLine(TargetPen, a, b);
        //            a.X = b.X;
        //            a.Y = b.Y;
        //            b.X = (int)Math.Floor(Xpos);

        //            d.Y = center + (int)((double)sample["Actual"] * 5.0);
        //            e.Graphics.DrawLine(ActualPen, c, d);
        //            c.X = d.X;
        //            c.Y = d.Y;
        //            d.X = (int)Math.Floor(Xpos);

        //            h.Y = center + (int)((double)sample["Error"] * this.errorScale);
        //            e.Graphics.DrawLine(ErrorPen, g, h);
        //            g.X = h.X;
        //            g.Y = h.Y;
        //            h.X = (int)Math.Floor(Xpos);
        //            Xpos += Xstep;
        //        }
        //    }
        //}

        void btnGetPIDData_Click(object sender, EventArgs e)
        {
            btnGetPIDData.IsEnabled = false;
            GrblPIDData.Load();
            //   PIDPlot.Refresh();
            btnGetPIDData.IsEnabled = true;
        }

        public ViewType mode { get { return ViewType.PIDTuner; } }

        public void Activate(bool activate, ViewType chgMode)
        {
        }

        public void CloseFile()
        {
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
            data.PrimaryKey = new DataColumn[] { GrblPIDData.data.Columns["Id"] };
        }

        public static void Load()
        {
            RawData = "";
            data.Clear();

            Comms.com.DataReceived += new DataReceivedHandler(Process);

            Comms.com.PurgeQueue();
            Comms.com.WriteCommand(((char)GrblConstants.CMD_PID_REPORT).ToString(CultureInfo.InvariantCulture));
            Comms.com.AwaitAck();

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
