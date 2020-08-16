/*
 * CenterFinderControl.xaml.cs - part of CNC Probing library
 *
 * v0.22 / 2020-08-15 / Io Engineering (Terje Io)
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
    public enum Center
    {
        None = 0,
        Inside,
        Outside
    }

    /// <summary>
    /// Interaction logic for CenterFinderControl.xaml
    /// </summary>
    public partial class CenterFinderControl : UserControl
    {
        private int pass = 0, passes = 1;
        private Position center;

        public CenterFinderControl()
        {
            InitializeComponent();
        }

        private void Execute()
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.ProbeCenter == Center.None)
            {
                MessageBox.Show("Select type of probe by clicking on one of the images above.", "Center finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            double diameter_2 = probing.WorkpieceDiameter / 2d;

            if (pass == passes)
                probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            string gotoCenter = center.ToString(AxisFlags.X | AxisFlags.Y);

            switch (probing.ProbeCenter)
            {
                case Center.Inside:
                    {
                        Position rapid = new Position(diameter_2 - probing.Offset, diameter_2 - probing.Offset, 0d);

                        probing.Program.AddRapid("Z-" + probing.Depth.ToInvariantString());
                        if (rapid.X > 1d)
                            probing.Program.AddRapid(rapid.ToString(AxisFlags.X, Direction.Negative));
                        probing.Program.AddProbingAction(AxisFlags.X, true);
                        probing.Program.AddRapidToMPos(gotoCenter);
                        if (rapid.X > 1d)
                            probing.Program.AddRapid(rapid.ToString(AxisFlags.X));
                        probing.Program.AddProbingAction(AxisFlags.X, false);
                        probing.Program.AddRapidToMPos(gotoCenter);
                        if (rapid.Y > 1d)
                            probing.Program.AddRapid(rapid.ToString(AxisFlags.Y, Direction.Negative));
                        probing.Program.AddProbingAction(AxisFlags.Y, true);
                        probing.Program.AddRapidToMPos(gotoCenter);
                        if (rapid.Y > 1d)
                            probing.Program.AddRapid(rapid.ToString(AxisFlags.Y));
                        probing.Program.AddProbingAction(AxisFlags.Y, false);
                    }
                    break;

                case Center.Outside:
                    {
                        Position rapid = new Position(diameter_2 + probing.XYClearance, diameter_2 + probing.XYClearance, 0d);
                        Position retract = new Position(probing.XYClearance, probing.XYClearance, 0d);

                        probing.Program.AddRapid(rapid.ToString(AxisFlags.X, Direction.Negative));
                        probing.Program.AddRapid("Z-" + probing.Depth.ToInvariantString());
                        probing.Program.AddProbingAction(AxisFlags.X, false);
                        probing.Program.AddRapid(retract.ToString(AxisFlags.X, Direction.Negative));
                        probing.Program.AddRapid("Z" + probing.Depth.ToInvariantString());
                        probing.Program.AddRapidToMPos(center.ToString(AxisFlags.X));

                        probing.Program.AddRapid(rapid.ToString(AxisFlags.X, Direction.Positive));
                        probing.Program.AddRapid("Z-" + probing.Depth.ToInvariantString());
                        probing.Program.AddProbingAction(AxisFlags.X, true);
                        probing.Program.AddRapid(retract.ToString(AxisFlags.X, Direction.Positive));
                        probing.Program.AddRapid("Z" + probing.Depth.ToInvariantString());
                        probing.Program.AddRapidToMPos(center.ToString(AxisFlags.X));

                        probing.Program.AddRapid(rapid.ToString(AxisFlags.Y, Direction.Negative));
                        probing.Program.AddRapid("Z-" + probing.Depth.ToInvariantString());
                        probing.Program.AddProbingAction(AxisFlags.Y, false);
                        probing.Program.AddRapid(retract.ToString(AxisFlags.Y, Direction.Negative));
                        probing.Program.AddRapid("Z" + probing.Depth.ToInvariantString());
                        probing.Program.AddRapidToMPos(center.ToString(AxisFlags.Y));

                        probing.Program.AddRapid(rapid.ToString(AxisFlags.Y, Direction.Positive));
                        probing.Program.AddRapid("Z-" + probing.Depth.ToInvariantString());
                        probing.Program.AddProbingAction(AxisFlags.Y, true);
                        probing.Program.AddRapid(retract.ToString(AxisFlags.Y, Direction.Positive));
                        probing.Program.AddRapid("Z" + probing.Depth.ToInvariantString());
                    }
                    break;
            }

            probing.Message = string.Format("Probing, pass {0} of {1}", (passes - pass + 1), pass);
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            if(!probing.ValidateInput())
                return;

            if (probing.WorkpieceDiameter <= 0d)
            {
                probing.SetError(nameof(probing.WorkpieceDiameter), "Workpiece diameter cannot be 0.");
                return;
            }

            if (probing.ProbeCenter == Center.Inside && probing.WorkpieceDiameter < probing.Offset * 2d)
            {
                probing.SetError(nameof(probing.WorkpieceDiameter), "Probing offset too large for workpiece diameter.");
                return;
            }

            if (!probing.Program.Init())
                return;

            pass = passes;

            center = new Position(probing.Grbl.MachinePosition);

            probing.PropertyChanged += Probing_PropertyChanged;

            Execute();
            probing.Program.Execute(true);
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProbingViewModel.IsCompleted):

                    bool ok = true;
                    var probing = DataContext as ProbingViewModel;

                    if (probing.IsSuccess && probing.Positions.Count != 4)
                    {
                        probing.PropertyChanged -= Probing_PropertyChanged;
                        probing.IsSuccess = false;
                        probing.Program.End("Probing failed");
                        return;
                    }

                    if (probing.IsSuccess)
                    {
                        center.X = probing.Positions[0].X + (probing.Positions[1].X - probing.Positions[0].X) / 2d;
                        center.Y = probing.Positions[2].Y + (probing.Positions[3].Y - probing.Positions[2].Y) / 2d;

                        switch(probing.ProbeCenter)
                        {
                            case Center.Inside:
                                ok = probing.GotoMachinePosition(center, AxisFlags.X | AxisFlags.Y);
                                probing.GotoMachinePosition(center, AxisFlags.Z);
                                break;

                            case Center.Outside:
                                if (pass > 1)
                                {
                                    Position start = new Position(center);
                                    start.X -= probing.WorkpieceDiameter / 2d + probing.XYClearance;
                                    ok = probing.GotoMachinePosition(start, AxisFlags.X);
                                    probing.GotoMachinePosition(start, AxisFlags.Y);
                                }
                                break;
                        }

                        if (ok)
                        {
                            if (--pass > 0)
                                Execute();
                            else
                            {
                                if (probing.ProbeCenter == Center.Outside)
                                {
//                                    center.Z = probing.Grbl.MachinePosition.Z + probing.Depth;
                                    probing.GotoMachinePosition(center, AxisFlags.Z);
                                    probing.GotoMachinePosition(center, AxisFlags.X | AxisFlags.Y);
                                }
                                if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                                    probing.Grbl.ExecuteCommand("G92X0Y0");
                                else
                                    probing.Grbl.ExecuteCommand(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, center.ToString(AxisFlags.X | AxisFlags.Y)));
                            }
                        }
                        if (!ok || pass == 0)
                        {
                            probing.PropertyChanged -= Probing_PropertyChanged;
                            probing.Program.End(ok ? "Probing completed" : "Probing failed");
                        } else
                            probing.Program.Execute(true);
                    } else
                        probing.PropertyChanged -= Probing_PropertyChanged;
                    break;
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }
    }
}
