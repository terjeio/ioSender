/*
 * ToolView.xaml.cs - part of CNC Controls library
 *
 * v0.01 / 2019-05-31 / Io Engineering (Terje Io)
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
using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using CNC.GCode;
using CNC.View;

namespace CNC.Controls
{
    public partial class ToolView : UserControl, CNCView
    {
        Tool selectedTool = null;
        private GrblViewModel parameters = new GrblViewModel();
        private volatile bool awaitCoord = false;

        public ToolView()
        {
            InitializeComponent();

            parameters.WorkPositionOffset.PropertyChanged += Parameters_PropertyChanged;
        }

        public Position offset { get; private set; } = new Position();

        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.Tools; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GrblWorkParameters.Get();

                Comms.com.DataReceived += DataReceived;

                dgrTools.ItemsSource = GrblWorkParameters.Tools;
                dgrTools.SelectedIndex = 0;
            }
            else
            {
                Comms.com.DataReceived -= DataReceived;

                dgrTools.ItemsSource = null;
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
                    if (parameters.IsMachinePosition)
                        for (int i = 0; i < offset.Values.Length; i++)
                            offset.Values[i] = parameters.MachinePosition.Values[i];
                    else
                        for (int i = 0; i < offset.Values.Length; i++)
                            offset.Values[i] = parameters.WorkPosition.Values[i] + parameters.WorkPositionOffset.Values[i];
                    parameters.WorkPositionOffset.SuspendNotifications = true;
                    parameters.Clear();
                    parameters.WorkPositionOffset.SuspendNotifications = false;
                    awaitCoord = false;
                    break;
            }
        }

        #region UIEvents

        void ToolControl_Load(object sender, EventArgs e)
        {
            if (GrblInfo.LatheModeEnabled)
            {
                dgrTools.Columns[1].Header = string.Format("X offset ({0})", GrblWorkParameters.LatheMode == LatheMode.Radius ? "R" : "D");
                cvXOffset.Label = dgrTools.Columns[1].Header + ":";
            }
        }

        private void dgrTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                selectedTool = (Tool)e.AddedItems[0];

                for (var i = 0; i < offset.Values.Length; i++)
                {
                    if (double.IsNaN(offset.Values[i])) offset.Values[i] = 120; // workaround for binding not propagating

                    offset.Values[i] = selectedTool.Values[i];
                }
            }
            else
                selectedTool = null;
        }

        void btnClear_Click(object sender, RoutedEventArgs e)
        {
            cvXOffset.Value = 0.0d;
            cvYOffset.Value = 0.0d;
            cvZOffset.Value = 0.0d;
    //          cvTipRadius.Value = 0.0d;
        }

        // G10 L1 P- axes <R- I- J- Q-> Set Tool Table
        // L10 - ref G5x + G92 - useful for probe (G38)
        // L11 - ref g59.3 only
        // Q: 1 - 8: 1: 135, 2: 45, 3: 315, 4: 225, 5: 180, 6: 90, 7: 0, 8: 270

        void saveOffset(string axis)
        {
            string s, axes;
            string xOffset = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblInfo.LatheMode, selectedTool.X).ToInvariantString();

            switch (axis)
            {
                case "R":
                    axes = "R{4}";
                    break;

                case "All":
                    axes = "X{1}Y{2}Z{3}R{4}";
                    break;

                default:
                    axes = (axis + "{" + (GrblInfo.AxisLetterToIndex(axis) + 1).ToString() + "}");
                    break;
            }

            s = string.Format("G10L1P{0}" + axes, selectedTool.Code, xOffset, selectedTool.Y.ToInvariantString(), selectedTool.Z.ToInvariantString(), selectedTool.R.ToInvariantString());

            Comms.com.WriteCommand(s);
        }

        void cvOffset_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                string axisletter = (string)((CoordValueSetControl)sender).Tag;
                int axis = GrblInfo.AxisLetterToIndex(axisletter);

                selectedTool.Values[axis] = offset.Values[axis];
                saveOffset(axisletter);
            }
        }

        private void btnSetAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    selectedTool.Values[i] = offset.Values[i];
            }

            string xOffset = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblInfo.LatheMode, cvXOffset.Value).ToInvariantString();

            string s = string.Format("G10L1P{0}X{1}Y{2}Z{3}R{4}", selectedTool.Code, xOffset, cvYOffset.Text, cvZOffset.Text, cvTipRadius.Text);
            Comms.com.WriteCommand(s);
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    offset.Values[i] = selectedTool.Values[i] = 0d;

                string xOffset = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblInfo.LatheMode, cvXOffset.Value).ToInvariantString();

                string s = string.Format("G10L1P{0}X{1}Y{2}Z{3}R{4}", selectedTool.Code, xOffset, cvYOffset.Text, cvZOffset.Text, cvTipRadius.Text);
                Comms.com.WriteCommand(s);
            }
        }

        private void btnCurrPos_Click(object sender, RoutedEventArgs e)
        {
            Comms.com.CommandState = Comms.State.AwaitAck;

            if (double.IsNaN(parameters.WorkPosition.X)) // If not NaN then MPG is polling
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));

            awaitCoord = true;

            while (awaitCoord)
                Comms.com.AwaitResponse(); // TODO: add timeout?
        }

        #endregion

        private void DataReceived(string data)
        {
            if (data.Length > 1 && data.Substring(0, 1) == "<")
                parameters.ParseStatus(data.Remove(data.Length - 1));
        }
    }
}
