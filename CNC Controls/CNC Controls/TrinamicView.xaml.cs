/*
 * TrinamicView.xaml.cs - part of CNC Controls library
 *
 * v0.01 / 2019-05-22 / Io Engineering (Terje Io)
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
using CNC.View;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for TrinamicView.xaml
    /// </summary>
    public partial class TrinamicView : UserControl, CNCView
    {
        private int[] sg_data = new int[512];
        private int sg_index = 0;
        Point a = new Point(0, 0), b = new Point(0, 0);
        Pen ActualPen;
        private bool plot = false, read_status = false;

        private delegate void StatusCallback(string data);

        public TrinamicView()
        {
            InitializeComponent();

            double ydelta = SGPlot.Height / 10;
            double ypos = SGPlot.Height - ydelta;

            ActualPen = new Pen(lblLoad.Foreground, 1);

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

            for (int i = 0; i < SGPlot.Width; i++)
                sg_data[i] = -1;

            btnGetState.Click += btnGetState_Click;
            chkEnableSfilt.Checked += chkEnableSfilt_CheckedChanged;
            //       SGPlot.Paint += new PaintEventHandler(SGPlot_Paint);
        }

        #region Methods required by IRenderer interface

        public ViewType mode { get { return ViewType.TrinamicTuner; } }

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

        #endregion

        #region UIEvents

        //void SGPlot_Paint(object sender, PaintEventArgs e)
        //{
        //    int samples;
        //    // Point a = new Point(0, sg_data[0]), b = new Point(0, sg_data[0]);
        //    // Point c = new Point(0, 0), d = new Point(0, 0);
        //    Pen TargetPen = new Pen(Brushes.LightGray, 1);
        //    Pen ErrorPen = new Pen(Brushes.Red, 1);
        //    Pen BlackPen = new Pen(Brushes.Black, 1);
        //  //  BlackPen.DashStyle = DashStyle.DashesProperty.;

        //    //   e.Graphics.Clear(this.SGPlot.BackColor);
        //    samples = e.ClipRectangle.X & 0xFFE;
        //    e.Graphics.DrawLine(BlackPen, samples, 128, samples + e.ClipRectangle.Width, 128);

        //    double ydelta = SGPlot.Height / 10;
        //    double ypos = SGPlot.Height - ydelta;

        //    while (ypos > 0)
        //    {
        //        e.Graphics.DrawLine(TargetPen, samples, ypos, samples + e.ClipRectangle.Width, ypos);
        //        ypos -= ydelta;
        //    }

        //    samples = e.ClipRectangle.Width; // == 4 ? 1 : e.ClipRectangle.Width;
        //    while (e.ClipRectangle.X + samples > 511)
        //        samples--;
        //    b.X = e.ClipRectangle.X;
        //    a.X = e.ClipRectangle.X == 0 ? e.ClipRectangle.X : e.ClipRectangle.X - 1;
        //    a.Y = 255 - sg_data[a.X];
        //    if (plot || e.ClipRectangle.Width > 2) do
        //        {
        //            if (sg_data[b.X] >= 0)
        //            {
        //                b.Y = 255 - sg_data[b.X];
        //                //      b = a;
        //                e.Graphics.DrawLine(ActualPen, a, b);
        //            }
        //            a = b;
        //            b.X++;
        //        } while (--samples > 0);
        //}

        void chkEnableSfilt_CheckedChanged(object sender, EventArgs e)
        {
            Comms.com.WriteCommand(string.Format("M122H{0}", chkEnableSfilt.IsChecked == true ? 1 : 0));
        }

        void btnGetState_Click(object sender, EventArgs e)
        {
            this.txtStatus.Clear();

            Comms.com.DataReceived += new DataReceivedHandler(ProcessStatus);

            Comms.com.PurgeQueue();
            Comms.com.WriteCommand("M122");

            //while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
            //    Application.DoEvents();

            Comms.com.DataReceived -= ProcessStatus;
        }

        #endregion

        void mdiControl_CommandGenerated(string command)
        {
            if (!command.StartsWith("M"))
            {
                for (sg_index = 0; sg_index < SGPlot.Width; sg_index++)
                    sg_data[sg_index] = -1;
                sg_index = 0;
                plot = false;
            //      SGPlot.Refresh();
            }
            Comms.com.WriteCommand(command);
        }

        private void AddStatusData(string data)
        {
            //if (this.txtStatus.InvokeRequired)
            //    this.BeginInvoke(new StatusCallback(AddStatusData), new object[] { data });
            //else
                this.txtStatus.AppendText(data + "\r\n");
        }

        private void ProcessStatus(string data)
        {
            if (data == "[TRINAMIC]")
                read_status = true;
            else if (data == "ok")
                read_status = false;
            else if (read_status)
                this.AddStatusData(data);
        }

        private void ProcessSGValue(string data)
        {
            if ((plot = data.StartsWith("[SG:")))
            {
                int sep = data.IndexOf(":");
                data = data.Substring(sep + 1, data.IndexOf("]") - sep - 1);

                sg_data[sg_index] = int.Parse(data) / 4;
        //         SGPlot.Invalidate(new Rectangle(sg_index == 0 ? 0 : sg_index - 1, 0, 2, SGPlot.Height));
                //SGPlot.Invalidate(new Rectangle(sg_index, 0, 1, SGPlot.Height));
                if (++sg_index >= sg_data.Length)
                    sg_index = 0;
            }
        }
    }
}

