/*
 * EdgeFinderControl.xaml.cs - part of CNC Probing library
 *
 * v0.37 / 2022-02-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2022, Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{
    public enum Edge
    {
        None = 0,
        A,
        B,
        C,
        D,
        Z,
        AB,
        AD,
        CB,
        CD
    }

    // D |-----| C
    //   |  Z  |
    // A | ----| B

    /// <summary>
    /// Interaction logic for EdgeFinderControl.xaml
    /// </summary>
    public partial class EdgeFinderControl : UserControl, IProbeTab
    {
        private volatile bool isCancelled = false;
        private AxisFlags axisflags = AxisFlags.None;
        private double[] af = new double[3];

        public EdgeFinderControl()
        {
            InitializeComponent();
        }

        public ProbingType ProbingType { get { return ProbingType.EdgeFinderExternal; } }

        public void Activate(bool activate)
        {
            if(activate)
                (DataContext as ProbingViewModel).Instructions = ((string)FindResource("Instructions")).Replace("\\n", "\n");
        }

        public void Start(bool preview = false)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput(probing.ProbeEdge == Edge.Z))
                return;

            if (probing.ProbeEdge == Edge.None)
            {
                MessageBox.Show((string)FindResource("SelectType"), "Edge finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (!probing.Program.Init())
                return;

            isCancelled = false;

            if (preview)
                probing.StartPosition.Zero();

            var XYClearance = probing.XYClearance + probing.ProbeDiameter / 2d;

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            switch (probing.ProbeEdge)
            {
                case Edge.A:
                    AddCorner(probing, false, false, XYClearance);
                    break;

                case Edge.B:
                    AddCorner(probing, true, false, XYClearance);
                    break;

                case Edge.C:
                    AddCorner(probing, true, true, XYClearance);
                    break;

                case Edge.D:
                    AddCorner(probing, false, true, XYClearance);
                    break;

                case Edge.Z:
                    axisflags = AxisFlags.Z;
                    af[GrblConstants.Z_AXIS] = 1d;
                    probing.Program.AddProbingAction(AxisFlags.Z, true);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    break;

                case Edge.AD:
                    AddEdge(probing, 'X', false, XYClearance);
                    break;

                case Edge.AB:
                    AddEdge(probing, 'Y', false, XYClearance);
                    break;

                case Edge.CB:
                    AddEdge(probing, 'X', true, XYClearance);
                    break;

                case Edge.CD:
                    AddEdge(probing, 'Y', true, XYClearance);
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

        private void AddEdge(ProbingViewModel probing, char axisletter, bool negative, double XYClearance)
        {
            int axis = GrblInfo.AxisLetterToIndex(axisletter);

            af[axis] = negative ? -1d : 1d;

            axisflags = GrblInfo.AxisLetterToFlag(axisletter);

            Position rapidto = new Position(probing.StartPosition);
            rapidto.Values[axis] -= XYClearance * af[axis];
            rapidto.Z -= probing.Depth;

            probing.Program.AddRapidToMPos(rapidto, axisflags);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

            probing.Program.AddProbingAction(axisflags, negative);

            rapidto.Values[axis] = probing.StartPosition.Values[axis] - probing.Offset * af[axis];
            probing.Program.AddRapidToMPos(rapidto, axisflags);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        }

        private void AddCorner(ProbingViewModel probing, bool negx, bool negy, double XYClearance)
        {
            af[GrblConstants.X_AXIS] = negx ? -1d : 1d;
            af[GrblConstants.Y_AXIS] = negy ? -1d : 1d;

            axisflags = AxisFlags.X | AxisFlags.Y;

            Position rapidto = new Position(probing.StartPosition);
            rapidto.X -= XYClearance * af[GrblConstants.X_AXIS];
            rapidto.Y += probing.Offset * af[GrblConstants.Y_AXIS];
            rapidto.Z -= probing.Depth;

            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

            probing.Program.AddProbingAction(AxisFlags.X, negx);

            probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X);

            rapidto.X = probing.StartPosition.X + probing.Offset * af[GrblConstants.X_AXIS];
            rapidto.Y = probing.StartPosition.Y;
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.X | AxisFlags.Y);
            rapidto.Y = probing.StartPosition.Values[GrblConstants.Y_AXIS] - XYClearance * af[GrblConstants.Y_AXIS];
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

            probing.Program.AddProbingAction(AxisFlags.Y, negy);

            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        }

        public void Stop()
        {
            isCancelled = true;
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private void OnCompleted ()
        {
            bool ok;

            var probing = DataContext as ProbingViewModel;

            if ((ok = probing.IsSuccess && probing.Positions.Count > 0))
            {
                int p = 0;
                Position pos = new Position(probing.StartPosition);

                foreach (int i in axisflags.ToIndices())
                    pos.Values[i] = probing.Positions[p++].Values[i] + (i == GrblConstants.Z_AXIS ? 0d : probing.ProbeDiameter / 2d * af[i]);

                if (double.IsNaN(pos.Z))
                {
                    probing.Grbl.IsJobRunning = false;
                    probing.Program.End((string)FindResource("PositionUnknown"));
                    return;
                }

                if (probing.ProbeZ && axisflags != AxisFlags.Z)
                {
                    Position pz = new Position(pos);

                    pz.X += probing.ProbeDiameter / 2d * af[GrblConstants.X_AXIS];
                    pz.Y += probing.ProbeDiameter / 2d * af[GrblConstants.Y_AXIS];
                    if ((ok = !isCancelled && probing.GotoMachinePosition(pz, axisflags)))
                    {
                        ok = !isCancelled && probing.WaitForResponse(probing.FastProbe + "Z-" + probing.Depth.ToInvariantString());
                        ok = ok && !isCancelled && probing.WaitForResponse(probing.RapidCommand + "Z" + probing.LatchDistance.ToInvariantString());
                        ok = ok && !isCancelled && probing.RemoveLastPosition();
                        if ((ok = ok && !isCancelled && probing.WaitForResponse(probing.SlowProbe + "Z-" + probing.Depth.ToInvariantString())))
                        {
                            pos.Z = probing.Grbl.ProbePosition.Z;
                            ok = !isCancelled && probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
                        }
                    }
                }

                ok = ok && !isCancelled && probing.GotoMachinePosition(pos, AxisFlags.Y);
                ok = ok && !isCancelled && probing.GotoMachinePosition(pos, AxisFlags.X);

                if (probing.ProbeZ)
                    axisflags |= AxisFlags.Z;

                if (ok)
                {
                    if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                    {
                        if((ok = !isCancelled && probing.GotoMachinePosition(pos, AxisFlags.Z)))
                        {
                            pos.X = -probing.ProbeOffsetX;
                            pos.Y = -probing.ProbeOffsetY;
                            pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight;
                            probing.WaitForResponse("G92" + pos.ToString(axisflags));
                            if (!isCancelled && axisflags.HasFlag(AxisFlags.Z))
                                probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
                        }
                    }
                    else
                    {
                        pos.X += probing.ProbeOffsetX;
                        pos.Y += probing.ProbeOffsetY;
                        pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl.ToolOffset.Z;
                        probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(axisflags)));
                    }
                }

                probing.Program.End((string)FindResource(ok ? "ProbingCompleted" : "ProbingFailed"));
            }

            if (!probing.Grbl.IsParserStateLive && probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

            probing.Grbl.IsJobRunning = false;
        }

        private void PreviewOnCompleted()
        {
            var probing = DataContext as ProbingViewModel;
            Position pos = new Position(probing.StartPosition);

            probing.Program.Clear();

            foreach (int i in axisflags.ToIndices())
                pos.Values[i] = probing.StartPosition.Values[i] + (i == GrblConstants.Z_AXIS ? 0d : probing.ProbeDiameter / 2d * af[i]);

            if (probing.ProbeZ && axisflags != AxisFlags.Z)
            {
                Position pz = new Position(pos);

                pz.X += probing.ProbeDiameter / 2d * af[GrblConstants.X_AXIS];
                pz.Y += probing.ProbeDiameter / 2d * af[GrblConstants.Y_AXIS];

                probing.Program.AddRapidToMPos(pz, axisflags);
                probing.Program.AddProbingAction(AxisFlags.Z, true);
                probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);

                //                    pos.Z = probing.Grbl.ProbePosition.Z;

            }

            probing.Program.AddRapidToMPos(pos, AxisFlags.Y);
            probing.Program.AddRapidToMPos(pos, AxisFlags.X);

            if (probing.ProbeZ)
                axisflags |= AxisFlags.Z;

            if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
            {
                probing.Program.AddRapidToMPos(pos, AxisFlags.Z);
                pos.X = -probing.ProbeOffsetX;
                pos.Y = -probing.ProbeOffsetY;
                pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight;
                probing.Program.Add("G92" + pos.ToString(axisflags));
                if (axisflags.HasFlag(AxisFlags.Z))
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
            }
            else
            {
                pos.X += probing.ProbeOffsetX;
                pos.Y += probing.ProbeOffsetY;
                pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl.ToolOffset.Z;
                probing.Program.Add(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(axisflags)));
            }
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            Start((DataContext as ProbingViewModel).PreviewEnable);
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }
    }
}
