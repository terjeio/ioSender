/*
 * RotationControl.xaml.cs - part of CNC Probing library
 *
 * v0.45 / 2024-07-16 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2024, Io Engineering (Terje Io)
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
        private bool addAction = false;
        private volatile bool isCancelled = false;
        private double[] af = new double[3];
        private double probedAngle = 0d;
        private Position p1, p2;

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

                probing.PropertyChanged += Probing_PropertyChanged;

                probing.AllowMeasure = false;
                probing.AddAction = addAction;
                probing.Instructions = ((string)FindResource("Instructions")).Replace("\\n", "\n");
            } else
                probing.PropertyChanged -= Probing_PropertyChanged;
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            switch (e.PropertyName)
            {
                case nameof(ProbingViewModel.CameraPositions):
                    if (probing.CameraPositions == 1 && probing.ProbeEdge == Edge.None)
                        probing.PreviewText += ((string)grd_action.ToolTip).Replace('.', '!');

                    if ((probing.CanApplyTransform = probing.CameraPositions == 2 && probing.ProbeEdge != Edge.None))
                    {
                        probedAngle = getAngle();
                        OutputAngle();
                    }
                    break;

                case nameof(ProbingViewModel.AddAction):
                    if ((addAction = probing.AddAction))
                    {
                        probing.Origin = OriginControl.Origin.None;
                        probing.ProbeEdge = Edge.AB;
                    }
                    break;

                case nameof(ProbingViewModel.ProbeEdge):
                    if (probing.ProbeEdge != Edge.AB)
                        probing.AddAction = false;
                    break;
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

            if (!probing.VerifyProbe())
                return;

            if (!probing.Program.Init())
                return;

            probedAngle = 0d;
            isCancelled = false;

            if (preview)
                probing.StartPosition.Zero();

            var XYClearance = probing.XYClearance + probing.ProbeRadius;

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
                    if (probing.AddAction && preview)
                        AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, XYClearance);
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

        private void AddActionEdge(ProbingViewModel probing, int offsetAxis, int clearanceAxis, double XYClearance)
        {
            AxisFlags probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
            Position rapidto = new Position(probing.StartPosition);

            rapidto.Values[clearanceAxis] -= XYClearance * af[clearanceAxis];
            rapidto.Values[offsetAxis] += probing.Offset * af[offsetAxis];
            rapidto.Z -= probing.Depth;

            probing.Program.AddRapidToMPos(rapidto, AxisFlags.XY);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

            probing.Program.AddProbingAction(probeAxis, af[clearanceAxis] == -1.0d);

            probing.Program.AddRapidToMPos(rapidto, probeAxis);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        }

        public void Stop()
        {
            isCancelled = true;
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private void OutputAngle ()
        {
            (DataContext as ProbingViewModel).Grbl.Message = string.Format((string)FindResource("ProbedAngle"), Math.Round(Math.Atan(probedAngle) * 180d / Math.PI, 3).ToInvariantString());
        }

        private void OnActionCompleted()
        {
            var probing = DataContext as ProbingViewModel;

            if (!isCancelled && (probing.CanApplyTransform = probing.IsSuccess && probing.Positions.Count == 1))
            {
                Position p3 = new Position(probing.Positions[0]),
                         p4 = new Position(p3.X + probing.Offset * probedAngle, p3.Y - probing.Offset, 0d),
                         pos = new Position(probing.StartPosition);

                p1.Y += probing.ProbeOffsetY + probing.ProbeRadius;
                p2.Y += probing.ProbeOffsetY + probing.ProbeRadius;
                p3.X += probing.ProbeOffsetX + probing.ProbeRadius;
                p4.X += probing.ProbeOffsetX + probing.ProbeRadius;
                var divisor = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
                pos.X = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.X - p4.X) - (p1.X - p2.X) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;
                pos.Y = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;

                if ((probing.CanApplyTransform = probing.GotoMachinePosition(pos, AxisFlags.XY)))
                {
                    switch (probing.CoordinateMode)
                    {
                        case ProbingViewModel.CoordMode.G92:
                            pos.X = pos.Y = 0d;
                            probing.WaitForResponse("G92" + pos.ToString(AxisFlags.XY));
                            break;

                        case ProbingViewModel.CoordMode.G10:
                            probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(AxisFlags.XY)));
                            break;
                    }
                }
            }
        }

        private void OnCompleted()
        {
            var probing = DataContext as ProbingViewModel;

            if (!isCancelled && (probing.CanApplyTransform = probing.IsSuccess && probing.Positions.Count == 2))
            {
                probedAngle = getAngle();

                if (probing.AddAction)
                {
                    p1 = new Position(probing.Positions[0]);
                    p2 = new Position(probing.Positions[1]);

                    probing.Program.Clear();
                    probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

                    switch (probing.ProbeEdge)
                    {
                        case Edge.AD:
                            af[GrblConstants.X_AXIS] = 1.0d;
                            af[GrblConstants.Y_AXIS] = -1.0d;
                            AddActionEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, probing.XYClearance + probing.ProbeRadius);
                            break;

                        case Edge.AB:
                            af[GrblConstants.X_AXIS] = 1.0d;
                            af[GrblConstants.Y_AXIS] = 1.0d;
                            AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, probing.XYClearance + probing.ProbeRadius);
                            break;

                        case Edge.CB:
                            af[GrblConstants.X_AXIS] = -1.0d;
                            af[GrblConstants.Y_AXIS] = 1.0d;
                            AddActionEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, probing.XYClearance + probing.ProbeRadius);
                            break;

                        case Edge.CD:
                            af[GrblConstants.X_AXIS] = -1.0d;
                            af[GrblConstants.Y_AXIS] = -1.0d;
                            AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.Z_AXIS, probing.XYClearance + probing.ProbeRadius);
                            break;
                    }
                    probing.Program.Execute(true);
                    OnActionCompleted();
                }
            }

            probing.Program.End((string)FindResource(probing.CanApplyTransform ? "ProbingCompleted" : "ProbingFailed"));

            if (probing.CanApplyTransform)
                OutputAngle();

            if (!probing.Grbl.IsParserStateLive && probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

            probing.Grbl.IsJobRunning = false;
            probing.Program.OnCompleted?.Invoke(probing.CanApplyTransform);
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

                        case OriginControl.Origin.CurrentPos:
                            offset = new RP.Math.Vector3(probing.Grbl.Position.X, probing.Grbl.Position.Y, 0d);
                            break;

                        default: // Origin.None -> 0,0
                            offset = new RP.Math.Vector3();
                            break;
                    }

                    new GCodeRotate().ApplyRotation(probedAngle, offset, AppConfig.Settings.Base.AutoCompress);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "GCode Rotate", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }
    }
}
