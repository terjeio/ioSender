/*
 * EdgeFinderControl.xaml.cs - part of CNC Probing library
 *
 * v0.19 / 2020-05-20 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
    public partial class EdgeFinderControl : UserControl
    {
        private AxisFlags axisflags = AxisFlags.None;
        private double[] af = new double[3];

        public EdgeFinderControl()
        {
            InitializeComponent();
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput())
                return;

            if (probing.ProbeEdge == Edge.None)
            {
                MessageBox.Show("Select edge or corner to probe by clicking on the relevant part of the image above.", "Edge finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (!probing.Program.Init())
                return;

            probing.PropertyChanged += Probing_PropertyChanged;

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));
            probing.Message = string.Empty;

            Position offset_2 = new Position(probing.XYClearance * 2d, probing.XYClearance * 2d, 0d);

            switch (probing.ProbeEdge)
            {
                case Edge.A:
                    axisflags = AxisFlags.X | AxisFlags.Y;
                    af[GrblConstants.X_AXIS] = af[GrblConstants.Y_AXIS] = 1d;
                    probing.Program.Add("G0Y" + probing.Offset.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.X, false);

                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString() + "Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.Y, false);

                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.B:
                    axisflags = AxisFlags.X | AxisFlags.Y;
                    af[GrblConstants.X_AXIS] = -1d; af[GrblConstants.Y_AXIS] = 1d;
                    probing.Program.Add("G0Y" + probing.Offset.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.X, true);

                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString() + "Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.Y, false);

                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.C:
                    axisflags = AxisFlags.X | AxisFlags.Y;
                    af[GrblConstants.X_AXIS] = af[GrblConstants.Y_AXIS] = -1d;
                    probing.Program.Add("G0Y-" + probing.Offset.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.X, true);

                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString() + "Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.Y, true);

                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.D:
                    axisflags = AxisFlags.X | AxisFlags.Y;
                    af[GrblConstants.X_AXIS] = 1d; af[GrblConstants.Y_AXIS] = -1d;
                    probing.Program.Add("G0Y-" + probing.Offset.ToInvariantString());
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.X, false);

                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString() + "Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());

                    probing.Program.AddProbingAction(AxisFlags.Y, true);

                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z" + probing.Depth.ToInvariantString());
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.Z:
                    axisflags = AxisFlags.Z;
                    af[GrblConstants.Z_AXIS] = 1d;
                    probing.Program.AddProbingAction(AxisFlags.Z, true);
                    break;

                case Edge.AD:
                    axisflags = AxisFlags.X;
                    af[GrblConstants.X_AXIS] = 1d;
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());
                    probing.Program.AddProbingAction(AxisFlags.X, false);
                    probing.Program.Add("G0X-" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.AB:
                    axisflags = AxisFlags.Y;
                    af[GrblConstants.Y_AXIS] = 1d;
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());
                    probing.Program.AddProbingAction(AxisFlags.Y, false);
                    probing.Program.Add("G0Y-" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.CB:
                    axisflags = AxisFlags.X;
                    af[GrblConstants.X_AXIS] = -1d;
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());
                    probing.Program.AddProbingAction(AxisFlags.X, true);
                    probing.Program.Add("G0X" + probing.XYClearance.ToInvariantString());
                    break;

                case Edge.CD:
                    axisflags = AxisFlags.Y;
                    af[GrblConstants.Y_AXIS] = -1d;
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    probing.Program.Add("G0Z-" + probing.Depth.ToInvariantString());
                    probing.Program.AddProbingAction(AxisFlags.Y, true);
                    probing.Program.Add("G0Y" + probing.XYClearance.ToInvariantString());
                    break;
            }

            probing.Program.Execute(true);
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProbingViewModel.IsCompleted):

                    var probing = DataContext as ProbingViewModel;
                    probing.PropertyChanged -= Probing_PropertyChanged;

                    if (probing.IsSuccess)
                    {
                        bool ok = true;
                        int p = 0;
                        Position pos = new Position(probing.Grbl.MachinePosition);

                        foreach(int i in axisflags.ToIndices())
                            pos.Values[i] = probing.Positions[p++].Values[i] + (i == GrblConstants.Z_AXIS ? 0d : probing.ProbeDiameter / 2d * af[i]);

                        Position pz = new Position(pos);
                        pos.Z += probing.Depth; // Sometimes NaN...?

                        if (probing.ProbeZ && axisflags != AxisFlags.Z)
                        {
                            ok = probing.GotoMachinePosition(pos, AxisFlags.Z);
                            pz.X += probing.ProbeDiameter / 2d * af[GrblConstants.X_AXIS];
                            pz.Y += probing.ProbeDiameter / 2d * af[GrblConstants.Y_AXIS];
                            if ((ok = (ok && probing.GotoMachinePosition(pz, axisflags))))
                            {
                                ok = probing.WaitForResponse(probing.FastProbe + "Z-" + probing.Depth.ToInvariantString());
                                ok = ok && probing.WaitForResponse("G0Z" + probing.LatchDistance.ToInvariantString());
                                ok = ok && probing.RemoveLastPosition();
                                if ((ok = ok && probing.WaitForResponse(probing.SlowProbe + "Z-" + probing.Depth.ToInvariantString())))
                                {
                                    axisflags |= AxisFlags.Z;
                                    pos.Z = probing.Grbl.ProbePosition.Z;
                                    pz.Z = pos.Z + probing.Depth;
                                    ok = probing.GotoMachinePosition(pz, AxisFlags.Z);
                                }
                            }
                        }
                        else
                        {
                            ok = probing.GotoMachinePosition(pos, AxisFlags.Z);
                            if (axisflags.HasFlag(AxisFlags.Z))
                            {
                                pos.Z = probing.Positions[0].Z;
                                pz.Z += probing.Depth;
                            }
                        }

                        if (ok && probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                        {
                            if ((ok == probing.GotoMachinePosition(pos, axisflags)))
                            {
                                pos.X = pos.Y = pos.Z = 0d;
                                probing.Grbl.ExecuteCommand("G92" + pos.ToString(axisflags));
                                if(axisflags.HasFlag(AxisFlags.Z))
                                    probing.GotoMachinePosition(pz, AxisFlags.Z);
                            }
                        } else
                            probing.Grbl.ExecuteCommand(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(axisflags)));

                        probing.Program.End(ok ? "Probing completed" : "Probing failed");
                    }
                    probing.Grbl.IsJobRunning = false;
                    break;
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }
    }
}
