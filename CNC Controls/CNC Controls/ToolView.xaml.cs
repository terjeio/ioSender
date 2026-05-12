/*
 * ToolView.xaml.cs - part of CNC Controls library
 *
 * v0.42 / 2023-03-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2023, Io Engineering (Terje Io)
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
using System.Collections.ObjectModel;
using System.Linq;
using CNC.Core;
using CNC.GCode;
using System.Threading;

namespace CNC.Controls
{
    public partial class ToolView : UserControl, ICNCView
    {
        Tool selectedTool = null;
        private GrblViewModel parameters = new GrblViewModel();
        private volatile bool awaitCoord = false;
        private Action<string> GotPosition;

        public static readonly DependencyProperty ToolRadiusProperty = DependencyProperty.Register(nameof(ToolRadius), typeof(double), typeof(ToolView), new PropertyMetadata(0d));
        public double ToolRadius
        {
            get { return (double)GetValue(ToolRadiusProperty); }
            set { SetValue(ToolRadiusProperty, value); }
        }

        public ToolView()
        {
            InitializeComponent();

            parameters.PropertyChanged += Parameters_PropertyChanged;
        }

        public Position offset { get; private set; } = new Position();

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.Tools; } }
        public bool CanEnable { get { return !(DataContext as GrblViewModel).IsGCLock; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                Comms.com.DataReceived += parameters.DataReceived;

                GrblWorkParameters.Get(parameters);

                dgrTools.ItemsSource = new ObservableCollection<Tool>(from tool in GrblWorkParameters.Tools where tool.Code != GrblConstants.NO_TOOL orderby tool.Id select tool);
                dgrTools.SelectedIndex = 0;
            }
            else
            {
                Comms.com.DataReceived -= parameters.DataReceived;
                dgrTools.ItemsSource = null;
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
                case nameof(GrblViewModel.MachinePosition):
                    if (!(awaitCoord = double.IsNaN(parameters.MachinePosition.Values[0])))
                    {
                        offset.Set(parameters.MachinePosition);
                        parameters.SuspendPositionNotifications = true;
                        parameters.Clear();
                        parameters.MachinePosition.Clear();
                        parameters.SuspendPositionNotifications = false;
                    }
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
            } else if (GrblInfo.HasCutterComp)
            {
                dgrTools.Columns[1].Header = "Cutter Radius";
                cvXOffset.Label = dgrTools.Columns[1].Header + ":";
            }
        }

        private double ToDisplayUnits(double mmValue)
        {
            var model = DataContext as GrblViewModel;
            return model != null && !model.IsMetric ? mmValue / 25.4d : mmValue;
        }

        private double ToMM(double displayValue)
        {
            var model = DataContext as GrblViewModel;
            return model != null && !model.IsMetric ? displayValue * 25.4d : displayValue;
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
                ToolRadius = selectedTool.R;
                txtTool.Text = selectedTool.Code;
            }
            else
                selectedTool = null;
        }

        // G10 L1 P- axes <R- I- J- Q-> Set Tool Table
        // L10 - ref G5x + G92 - useful for probe (G38)
        // L11 - ref g59.3 only
        // Q: 1 - 8: 1: 135, 2: 45, 3: 315, 4: 225, 5: 180, 6: 90, 7: 0, 8: 270

        void saveOffset(string axis)
        {
            string axes;

            // Convert display values back to mm for the G10 command
            Position mmpos = new Position(offset);
            for (var i = 0; i < mmpos.Values.Length; i++)
                mmpos.Values[i] = ToMM(mmpos.Values[i]);

            mmpos.X = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, mmpos.X);

            switch (axis)
            {
                case "R":
                    axes = string.Format("R{0}", ToMM(ToolRadius).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case "All":
                    axes = mmpos.ToString(GrblInfo.AxisFlags);
                    break;

                default:
                    axes = mmpos.ToString(GrblInfo.AxisLetterToFlag(axis));
                    break;
            }

            string cmd = string.Format("G10L1P{0}{1}", selectedTool.Code, axes);

            if (!GrblParserState.IsMetric)
            {
                Comms.com.WriteCommand("G21" + cmd);
                Comms.com.WriteCommand("G20");
            }
            else
                Comms.com.WriteCommand(cmd);
        }

        void cvOffset_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                string axisletter = (string)((CoordValueSetControl)sender).Tag;

                if (axisletter == "R")
                {
                    selectedTool.R = ToMM(ToolRadius);
                    saveOffset("R");
                }
                else
                {
                    int axis = GrblInfo.AxisLetterToIndex(axisletter);
                    selectedTool.Values[axis] = ToMM(offset.Values[axis]);
                    saveOffset(axisletter);
                }
            }
        }

        private void btnSetAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    selectedTool.Values[i] = ToMM(offset.Values[i]);
                selectedTool.R = ToMM(ToolRadius);
            }

            saveOffset("All");
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool != null)
            {
                for (var i = 0; i < offset.Values.Length; i++)
                    offset.Values[i] = selectedTool.Values[i] = 0d;
                ToolRadius = selectedTool.R = 0d;

                saveOffset("All");
            }
        }

        private void RequestStatus()
        {
            parameters.Clear();
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
            }
            else
                GotPosition?.Invoke("ok");
        }
    }
}
