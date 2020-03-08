/*
 * OffsetView.xaml.cs - part of CNC Controls library
 *
 * v0.10 / 2019-03-05 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2020, Io Engineering (Terje Io)
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
using CNC.Core;
using CNC.GCode;
using System.Threading;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for OffsetView.xaml
    /// </summary>
    public partial class OffsetView : UserControl, ICNCView
    {
        CoordinateSystem selectedOffset = null;
        private GrblViewModel parameters = new GrblViewModel();
        private volatile bool awaitCoord = false;
        private Action<string> GotPosition;

        public OffsetView()
        {
            InitializeComponent();

            parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
            if(!GrblSettings.IsGrblHAL)
                parameters.Position.PropertyChanged += Parameters_PropertyChanged;
        }

        public int AxisEnabledFlags { get { return GrblInfo.AxisFlags; } }
        public Position offset { get; private set; } = new Position();

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.Offsets; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                Comms.com.DataReceived += parameters.DataReceived;

                GrblWorkParameters.Get(parameters);

                parameters.AxisEnabledFlags = GrblInfo.AxisFlags;

                dgrOffsets.ItemsSource = GrblWorkParameters.CoordinateSystems;
                dgrOffsets.SelectedIndex = 0;
            }
            else
            {
                Comms.com.DataReceived -= parameters.DataReceived;
                Comms.com.PurgeQueue();
                dgrOffsets.ItemsSource = null;
            }
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        private void Parameters_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Z":
                    if (!(awaitCoord = double.IsNaN(parameters.WorkPositionOffset.Values[0])))
                    {
                        if (parameters.IsMachinePosition)
                            for (int i = 0; i < offset.Values.Length; i++)
                                offset.Values[i] = parameters.MachinePosition.Values[i];
                        else
                            for (int i = 0; i < offset.Values.Length; i++)
                                offset.Values[i] = parameters.WorkPosition.Values[i] + parameters.WorkPositionOffset.Values[i];
                        parameters.Position.SuspendNotifications = parameters.WorkPositionOffset.SuspendNotifications = true;
                        parameters.Clear();
                        parameters.WorkPositionOffset.SuspendNotifications = parameters.Position.SuspendNotifications = false;

                    }
                    break;
            }
        }

        #region UIEvents
        void OffsetControl_Load(object sender, EventArgs e)
        {
            if (GrblInfo.LatheModeEnabled)
            {
                dgrOffsets.Columns[1].Header = string.Format("X offset ({0})", GrblWorkParameters.LatheMode == LatheMode.Radius ? "R" : "D");
                cvXOffset.Label = dgrOffsets.Columns[1].Header + ":";
            }
        }

        private void dgrOffsets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                selectedOffset = (CoordinateSystem)e.AddedItems[0];

                for (var i = 0; i < offset.Values.Length; i++)
                {
                    if (double.IsNaN(offset.Values[i])) offset.Values[i] = 120; // workaround for binding not propagating

                    offset.Values[i] = selectedOffset.Values[i];
                }
                txtOffset.Text = selectedOffset.Code;
            }
            else
                selectedOffset = null;
        }

        // G10 L1 P- axes <R- I- J- Q-> Set Tool Table
        // L10 - ref G5x + G92 - useful for probe (G38)
        // L11 - ref g59.3 only
        // Q: 1 - 8: 1: 135, 2: 45, 3: 315, 4: 225, 5: 180, 6: 90, 7: 0, 8: 270

        void saveOffset(string axis)
        {
            string s, axes = string.Empty;
            string[] soffset = new string[6];

            if(axis == "All")
            {
                int i = 0, axisflags = GrblInfo.AxisFlags;
                while (axisflags != 0)
                {

                    if ((axisflags & 0x01) != 0)
                        axes += string.Format("{0}{{{1}}}", GrblInfo.AxisIndexToLetter(i), i + 1);
                    i++; axisflags >>= 1;
                }
            } else
                axes = axis + "{" + (GrblInfo.AxisLetterToIndex(axis) + 1).ToString() + "}";

            for (int i = 0; i < selectedOffset.Values.Length; i++)
            {
                if(i == 0)
                    soffset[i] = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, selectedOffset.X).ToInvariantString();
                else
                    soffset[i] = selectedOffset.Values[i].ToInvariantString();
            }

            string xOffset = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, selectedOffset.X).ToInvariantString();

            if (selectedOffset.Id == 0)
            {
                string code = selectedOffset.Code == "G28" || selectedOffset.Code == "G30" ? selectedOffset.Code + ".1" : selectedOffset.Code;
                s = string.Format("G90{0}" + axes, code, soffset[0], soffset[1], soffset[2], soffset[3], soffset[4], soffset[5]);
            } else
                s = string.Format("G90G10L2P{0}" + axes, selectedOffset.Id, soffset[0], soffset[1], soffset[2], soffset[3], soffset[4], soffset[5]);

            Comms.com.WriteCommand(s);
        }

        private void cvOffset_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOffset != null)
            {
                string axisletter = (string)((CoordValueSetControl)sender).Tag;
                int axis = GrblInfo.AxisLetterToIndex(axisletter);

                selectedOffset.Values[axis] = offset.Values[axis];
                saveOffset(axisletter);
            }
        }

        void btnSetAll_Click(object sender, EventArgs e)
        {
            if (selectedOffset != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    selectedOffset.Values[i] = offset.Values[i];

                saveOffset("All");
            }
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOffset != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    offset.Values[i] = selectedOffset.Values[i] = 0d;

                saveOffset("All");
            }
        }

        private void RequestStatus ()
        {
            parameters.WorkPositionOffset.Z = double.NaN;
            if (double.IsNaN(parameters.WorkPosition.X) || true) // If not NaN then MPG is polling
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));
        }

        void btnCurrPos_Click(object sender, RoutedEventArgs e)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            awaitCoord = true;

            parameters.OnRealtimeStatusProcessed += DataReceived;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    null,
                    a => GotPosition += a,
                    a => GotPosition -= a,
                    1000, () => RequestStatus());
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            parameters.OnRealtimeStatusProcessed -= DataReceived;
        }

        #endregion

        private void DataReceived(string data)
        {
            if (awaitCoord)
            {
                Thread.Sleep(50);
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));
            } else
                GotPosition?.Invoke("ok");
        }
    }
}
