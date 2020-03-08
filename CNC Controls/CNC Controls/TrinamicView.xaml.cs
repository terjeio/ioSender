/*
 * TrinamicView.xaml.cs - part of CNC Controls library
 *
 * v0.10 / 2019-03-05 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019, Io Engineering (Terje Io)
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
using CNC.Core;
using System.Windows.Shapes;
using System.Collections.Generic;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for TrinamicView.xaml
    /// </summary>
    public partial class TrinamicView : UserControl, ICNCView
    {
        private int sg_index = 0;
        private bool plot = false, read_status = false;

        private List<Line> lines;

        private delegate void StatusCallback(string data);

        public TrinamicView()
        {
            InitializeComponent();

            double ydelta = SGPlot.Height / 10;
            double ypos = SGPlot.Height - ydelta;

            lines = new List<Line>((int)SGPlot.Width);

            //while (ypos > 0)
            //{
            //    lbl = new System.Windows.Forms.Label();
            //    lbl.Location = new System.Drawing.Point(this.SGPlot.Location.X + SGPlot.Width + 5, this.SGPlot.Location.Y + ypos - 10);
            //    lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //    lbl.Size = new System.Drawing.Size(32, 20);
            //    lbl.Text = lval.ToString();
            //    lbl.ForeColor = lblLoad.ForeColor;
            //    //   lbl.BackColor = System.Drawing.Color.White;
            //    lval += 100;
            //    this.Controls.Add(lbl);
            //    ypos -= ydelta;
            //}

            btnGetState.Click += btnGetState_Click;
            chkEnableSfilt.Checked += chkEnableSfilt_CheckedChanged;
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.TrinamicTuner; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            Comms.com.WriteCommand(string.Format("M122S{0}H{1}", activate ? 1 : 0, chkEnableSfilt.IsChecked == true ? 1 : 0));
            if (activate)
                Comms.com.DataReceived += new DataReceivedHandler(ProcessSGValue);
            else
                Comms.com.DataReceived -= ProcessSGValue;
        }

        public void CloseFile()
        {
        }
        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        #region UIEvents

        void chkEnableSfilt_CheckedChanged(object sender, EventArgs e)
        {
            Comms.com.WriteCommand(string.Format("M122H{0}", chkEnableSfilt.IsChecked == true ? 1 : 0));
        }

        void btnGetState_Click(object sender, EventArgs e)
        {
            txtStatus.Clear();

            Comms.com.DataReceived += ProcessStatus;
            Comms.com.AwaitAck("M122");
            Comms.com.DataReceived -= ProcessStatus;
        }

        #endregion

        void mdiControl_CommandGenerated(string command)
        {
            if (!command.StartsWith("M"))
            {
                PlotGrid();
                for (int i = 0; i < lines.Capacity; i++)
                    lines[i] = null;
                plot = false;
            }
            Comms.com.WriteCommand(command);
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
                    Y1 = SGPlot.Height / 2d,
                    Y2 = SGPlot.Height / 2d,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 0.5d,
                    StrokeDashArray = new DoubleCollection() { 2d }
                });

                ypos -= ydelta;
            }
        }

        private void AddStatusData(string data)
        {
            txtStatus.AppendText(data + "\r\n");
        }

        private void ProcessStatus(string data)
        {
            if (data == "[TRINAMIC]")
                read_status = true;
            else if (data == "ok")
                read_status = false;
            else if (read_status)
                AddStatusData(data);
        }

        private void ProcessSGValue(string data)
        {
            if ((plot = data.StartsWith("[SG:")))
            {
                int sep = data.IndexOf(":");
                data = data.Substring(sep + 1, data.IndexOf("]") - sep - 1);

                int value = int.Parse(data) / 4;

                if (lines[sg_index] != null)
                    SGPlot.Children.Remove(lines[sg_index]);

                lines[sg_index] = new Line()
                {
                    X1 = sg_index == 0 ? 0 : sg_index - 1,
                    X2 = sg_index,
                    Y1 = sg_index == 0 ? value : lines[sg_index - 1].Y2,
                    Y2 = value,
                    Stroke = Brushes.Blue,
                };

                SGPlot.Children.Add(lines[sg_index++]);

                if (++sg_index >= lines.Capacity)
                    sg_index = 0;
            }
        }
    }
}

