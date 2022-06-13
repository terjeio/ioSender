/*
 * RotationControl.xaml.cs - part of CNC Probing library
 *
 * v0.38 / 2022-05-01 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021-2022, Io Engineering (Terje Io)
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

namespace CNC.Controls.Probing
{
    /// <summary>
    /// Interaction logic for RotationControl.xaml
    /// </summary>
    public partial class RotationControl : UserControl, IProbeTab
    {
        private double[] af = new double[3];

        public RotationControl()
        {
            InitializeComponent();
        }

        public ProbingType ProbingType { get { return ProbingType.Rotation; } }

        public void Activate(bool activate)
        {
            var probing = DataContext as ProbingViewModel;

            if (activate) {
                if (probing.ProbeEdge == Edge.A || probing.ProbeEdge == Edge.B || probing.ProbeEdge == Edge.C || probing.ProbeEdge == Edge.D)
                    probing.ProbeEdge = Edge.None;

                probing.Instructions = ((string)FindResource("Instructions")).Replace("\\n", "\n");
                probing.PropertyChanged += Probing_PropertyChanged;
            } else
                probing.PropertyChanged -= Probing_PropertyChanged;
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(ProbingViewModel.CameraPositions))
            {
                var probing = DataContext as ProbingViewModel;

                if (probing.CameraPositions == 1 && probing.ProbeEdge == Edge.None)
                    probing.PreviewText += ((string)grd_action.ToolTip).Replace('.', '!');

                if ((probing.CanApplyTransform = probing.CameraPositions == 2 && probing.ProbeEdge != Edge.None))
                    getAngle();
            }
        }

        public void Start(bool preview = false)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput(probing.ProbeEdge == Edge.Z))
                return;

            if (probing.ProbeEdge == Edge.None)
            {
                MessageBox.Show((string)FindResource("SelectType"), "Rotation", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (!probing.Program.Init())
                return;

            if (preview)
                probing.StartPosition.Zero();

            var XYClearance = probing.XYClearance + probing.ProbeDiameter / 2d;

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            switch (probing.ProbeEdge)
            {
                case Edge.AD:
                    af[GrblConstants.X_AXIS] = 1.0d;
                    af[GrblConstants.Y_AXIS] = -1.0d;
                    AddEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, XYClearance);
                    break;

                case Edge.AB:
                    af[GrblConstants.X_AXIS] = 1.0d;
                    af[GrblConstants.Y_AXIS] = 1.0d;
                    AddEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, XYClearance);
                    break;

                case Edge.CB:
                    af[GrblConstants.X_AXIS] = -1.0d;
                    af[GrblConstants.Y_AXIS] = 1.0d;
                    AddEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, XYClearance);
                    break;

                case Edge.CD:
                    af[GrblConstants.X_AXIS] = -1.0d;
                    af[GrblConstants.Y_AXIS] = -1.0d;
                    AddEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, XYClearance);
                    break;
            }

            if (preview)
            {
                probing.PreviewText = probing.Program.ToString().Replace("G53", string.Empty);
                PreviewOnCompleted();
                probing.PreviewText += "\n; Post XY probe\n" + probing.Program.ToString().Replace("G53", string.Empty);
            }
            else
            {
                probing.Program.Execute(true);
                OnCompleted();
            }
        }

        private void AddEdge(ProbingViewModel probing, int offsetAxis, int clearanceAxis, double XYClearance)
        {
            AxisFlags probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
            Position rapidto = new Position(probing.StartPosition);

            rapidto.Values[clearanceAxis] -= XYClearance * af[clearanceAxis];
            rapidto.Z -= probing.Depth;

            probing.Program.AddRapidToMPos(rapidto, probeAxis);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

            probing.Program.AddProbingAction(probeAxis, af[clearanceAxis] == -1.0d);

            probing.Program.AddRapidToMPos(rapidto, probeAxis);
            rapidto.Values[offsetAxis] = probing.StartPosition.Values[offsetAxis] + probing.Offset * af[offsetAxis];
            probing.Program.AddRapidToMPos(rapidto, GrblInfo.AxisIndexToFlag(offsetAxis));
            probing.Program.AddProbingAction(probeAxis, af[clearanceAxis] == -1.0d);

            probing.Program.AddRapidToMPos(rapidto, probeAxis);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.XY);
        }

        public void Stop()
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private void addpos ()
        {
            var probing = DataContext as ProbingViewModel;

            probing.Positions.Add(new Position(probing.Grbl.MachinePosition));
        }

        private void OnCompleted()
        {
            var probing = DataContext as ProbingViewModel;

            probing.CanApplyTransform = probing.IsSuccess && probing.Positions.Count == 2;

            probing.Program.End((string)FindResource(probing.CanApplyTransform ? "ProbingCompleted" : "ProbingFailed"));

            if (probing.CanApplyTransform)
                getAngle();

            if (!probing.Grbl.IsParserStateLive && probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

            probing.Grbl.IsJobRunning = false;
        }

        private void PreviewOnCompleted()
        {
            var probing = DataContext as ProbingViewModel;
            Position pos = new Position(probing.StartPosition);

            probing.Program.Clear();
        }

        private double getAngle()
        {
            var probing = DataContext as ProbingViewModel;

            double angle = (probing.Positions[1].Y - probing.Positions[0].Y) / (probing.Positions[1].X - probing.Positions[0].X);

            if (probing.ProbeEdge == Edge.CB || probing.ProbeEdge == Edge.AD)
                angle = -1.0d / angle;

            probing.Grbl.Message = string.Format((string)FindResource("ProbedAngle"), Math.Round(Math.Atan(angle) * 180d / Math.PI, 3).ToInvariantString());

            return angle;
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            Start((DataContext as ProbingViewModel).PreviewEnable);
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void apply_Click(object sender, RoutedEventArgs e)
        {
            using (new UIUtils.WaitCursor())
            {
                try {

                    var probing = DataContext as ProbingViewModel;

                    RP.Math.Vector3 offset;

                    var limits = probing.Grbl.ProgramLimits;

                    if (!probing.Grbl.IsFileLoaded)
                        probing.Origin = OriginControl.Origin.None;

                    switch (probing.Origin)
                    {
                        case OriginControl.Origin.A:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.B:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.C:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MaxY, 0d);
                            break;

                        case OriginControl.Origin.D:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MaxY, 0d);
                            break;

                        case OriginControl.Origin.Center:
                            offset = new RP.Math.Vector3(limits.MaxX / 2d, limits.MaxY / 2d, 0d);
                            break;

                        case OriginControl.Origin.AB:
                            offset = new RP.Math.Vector3(limits.MaxX / 2d, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.AD:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MaxY / 2d, 0d);
                            break;

                        case OriginControl.Origin.CB:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MaxY / 2d, 0d);
                            break;

                        case OriginControl.Origin.CD:
                            offset = new RP.Math.Vector3(limits.MaxX / 2d, limits.MaxY, 0d);
                            break;

                        default: // Origin.None -> 0,0
                            offset = new RP.Math.Vector3();
                            break;
                    }

                    new GCodeRotate().ApplyRotation(getAngle(), new RP.Math.Vector3(), AppConfig.Settings.Base.AutoCompress);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "GCode Rotate", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }
    }
}
