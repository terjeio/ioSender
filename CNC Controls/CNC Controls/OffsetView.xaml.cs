/*
 * OffsetView.xaml.cs - part of CNC Controls library
 *
 * v0.31 / 2020-04-27 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2021, Io Engineering (Terje Io)
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
            if(!GrblInfo.IsGrblHAL)
                parameters.PropertyChanged += Parameters_PropertyChanged;
        }

        public AxisFlags AxisEnabledFlags { get { return GrblInfo.AxisFlags; } }
        public CoordinateSystem Offset { get; private set; } = new CoordinateSystem();

        public static readonly DependencyProperty CanEditProperty = DependencyProperty.Register(nameof(CanEdit), typeof(bool), typeof(OffsetView), new PropertyMetadata(false));
        public bool CanEdit
        {
            get { return (bool)GetValue(CanEditProperty); }
            set { SetValue(CanEditProperty, value); }
        }

        public static readonly DependencyProperty IsPredefinedProperty = DependencyProperty.Register(nameof(IsPredefined), typeof(bool), typeof(OffsetView), new PropertyMetadata(false));
        public bool IsPredefined
        {
            get { return (bool)GetValue(IsPredefinedProperty); }
            set { SetValue(IsPredefinedProperty, value); }
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.Offsets; } }
        public bool CanEnable { get { return !(DataContext as GrblViewModel).IsGCLock; } }

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
                case nameof(GrblViewModel.MachinePosition):
                    if (!(awaitCoord = double.IsNaN(parameters.MachinePosition.Values[0])))
                    {
                        Offset.Set(parameters.MachinePosition);
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
                IsPredefined = selectedOffset.Code == "G28" || selectedOffset.Code == "G30";

                for (var i = 0; i < Offset.Values.Length; i++)
                {
                    if (double.IsNaN(Offset.Values[i])) Offset.Values[i] = 120; // workaround for binding not propagating

                    Offset.Values[i] = selectedOffset.Values[i];
                }
                Offset.Code = selectedOffset.Code;

                if (IsPredefined)
                    btnCurrPos_Click(null, null);

                CanEdit = !IsPredefined;
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
            string cmd;

            Position newpos = new Position(Offset);

            newpos.X = GrblWorkParameters.ConvertX(GrblWorkParameters.LatheMode, GrblParserState.LatheMode, selectedOffset.X);

            if (selectedOffset.Id == 0)
            {
                string code = selectedOffset.Code == "G28" || selectedOffset.Code == "G30" ? selectedOffset.Code + ".1" : selectedOffset.Code;

                if (axis == "ClearAll" || IsPredefined)
                    cmd = selectedOffset.Code == "G43.1" ? "G49" : selectedOffset.Code + ".1";
                else
                    cmd = string.Format("G90{0}{1}", code, newpos.ToString(axis == "All" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis)));
            } else
                cmd = string.Format("G90G10L2P{0}{1}", selectedOffset.Id, newpos.ToString(axis == "All" || axis == "ClearAll" ? GrblInfo.AxisFlags : GrblInfo.AxisLetterToFlag(axis)));

            Comms.com.WriteCommand(cmd);
        }

        private void cvOffset_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOffset != null)
            {
                string axisletter = (string)((CoordValueSetControl)sender).Tag;
                int axis = GrblInfo.AxisLetterToIndex(axisletter);

                selectedOffset.Values[axis] = Offset.Values[axis];
                saveOffset(axisletter);
            }
        }

        void btnSetAll_Click(object sender, EventArgs e)
        {
            if (selectedOffset != null)
            {
                for (var i = 0; i < Offset.Values.Length; i++)
                    selectedOffset.Values[i] = Offset.Values[i];

                saveOffset("All");
            }
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOffset != null)
            {
                for (var i = 0; i < Offset.Values.Length; i++)
                    Offset.Values[i] = selectedOffset.Values[i] = 0d;

                saveOffset("ClearAll");
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
