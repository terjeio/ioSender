/*
 * OffsetView.xaml.cs - part of CNC Controls library
 *
* v0.02 / 2020-01-27 / Io Engineering (Terje Io)
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
using CNC.View;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for OffsetView.xaml
    /// </summary>
    public partial class OffsetView : UserControl, CNCView
    {
        CoordinateSystem selectedOffset = null;
        private GrblViewModel parameters = new GrblViewModel();
        private volatile bool awaitCoord = false;

        public OffsetView()
        {
            InitializeComponent();

            parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
            if(!GrblSettings.IsGrblHAL)
                parameters.Position.PropertyChanged += Parameters_PropertyChanged;
        }

        public Position offset { get; private set; } = new Position();

        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.Offsets; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GrblWorkParameters.Get();

                Comms.com.DataReceived += DataReceived;

                dgrOffsets.ItemsSource = GrblWorkParameters.CoordinateSystems;
                dgrOffsets.SelectedIndex = 0;
            }
            else
            {
                Comms.com.DataReceived -= DataReceived;
                Comms.com.PurgeQueue();
                dgrOffsets.ItemsSource = null;
            }
        }

        public void CloseFile()
        {
        }

        #endregion

        private void Parameters_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Z":
                    if (!(awaitCoord = double.IsNaN(parameters.WorkPositionOffset.Values[0]))) {
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
            string s;
            string axes = axis == "All" ? "X{1}Y{2}Z{3}" : (axis + "{" + (GrblInfo.AxisLetterToIndex(axis) + 1).ToString() + "}");
            string xOffset = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, selectedOffset.X).ToInvariantString();

            if (selectedOffset.Id == 0)
            {
                string code = selectedOffset.Code == "G28" || selectedOffset.Code == "G30" ? selectedOffset.Code + ".1" : selectedOffset.Code;
                s = string.Format("G90{0}" + axes, code, xOffset, selectedOffset.Y.ToInvariantString(), selectedOffset.Z.ToInvariantString());
            }
            else
                s = string.Format("G90G10L2P{0}" + axes, selectedOffset.Id, xOffset, selectedOffset.Y.ToInvariantString(), selectedOffset.Z.ToInvariantString());

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

        void btnCurrPos_Click(object sender, RoutedEventArgs e)
        {
            Comms.com.CommandState = Comms.State.AwaitAck;

            if (double.IsNaN(parameters.WorkPosition.X)) // If not NaN then MPG is polling
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));

            awaitCoord = true;

            while (awaitCoord)
            {
                EventUtils.DoEvents();
                Comms.com.AwaitResponse(); // TODO: add timeout?
            }
        }

        #endregion

        private void DataReceived(string data)
        {
            if (data.Length > 1 && data.Substring(0, 1) == "<")
                parameters.ParseStatus(data.Remove(data.Length - 1));

            if (awaitCoord)
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));
        }
    }
}
