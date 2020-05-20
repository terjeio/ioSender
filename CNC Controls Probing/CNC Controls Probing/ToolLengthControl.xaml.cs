/*
 * ToolLengthControl.cs - part of CNC Probing library
 *
 * v0.19 / 2020-05-09 / Io Engineering (Terje Io)
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
    /// <summary>
    /// Interaction logic for ToolLengthControl.xaml
    /// </summary>
    public partial class ToolLengthControl : UserControl
    {
        Position origin = null, g59_3 = null;

        public ToolLengthControl()
        {
            InitializeComponent();
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput())
                return;

            if (probing.ProbeFixture && probing.Grbl.HomedState != HomedState.Homed)
            {
                MessageBox.Show("Axes must be homed before probing the fixture!", "Probing");
                return;
            }

            if (!probing.Program.Init())
                return;

            probing.PropertyChanged += Probing_PropertyChanged;

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            origin = new Position(probing.Grbl.MachinePosition);

            if (probing.ProbeFixture)
            {
                g59_3 = new Position(GrblWorkParameters.GetCoordinateSystem("G59.3"));
                var safeZ = System.Math.Max(g59_3.Z, origin.Z) + probing.Depth;
                g59_3.Z += probing.Depth;
                probing.Program.Add("G53G0Z" + safeZ.ToInvariantString());
                probing.Program.Add("G53G0" + g59_3.ToString(AxisFlags.X | AxisFlags.Y));
                probing.Program.Add("G53G0" + g59_3.ToString(AxisFlags.Z));
                g59_3.Z -= probing.Depth;
            }
            probing.Program.AddProbingAction(AxisFlags.Z, true);
            probing.Program.Execute(true);
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProbingViewModel.IsCompleted):
                    bool ok = true;
                    var probing = DataContext as ProbingViewModel;

                    probing.PropertyChanged -= Probing_PropertyChanged;

                    if (probing.IsSuccess)
                    {
                        probing.GotoMachinePosition(probing.Positions[0], AxisFlags.Z);

                        if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                        {
                            var touchOffset = probing.ProbeFixture ? probing.FixtureHeight : (probing.WorkpieceHeight + probing.TouchPlateHeight);
                            probing.Grbl.ExecuteCommand(string.Format("G92Z{0}", (touchOffset).ToInvariantString()));
                        }
                        else
                        {
                            if (probing.ProbeFixture)
                            {
                                //if (probing.HasToolTable)
                                //    probing.Grbl.ExecuteCommand(string.Format("G43H{0}{1}", probing.Tool, probing.Positions[0].ToString(AxisFlags.Z)));
                                //else
                                var tofs = new Position(probing.Positions[0]);
                                tofs.Z = - (probing.Positions[0].Z -= g59_3.Z);
                                probing.Grbl.ExecuteCommand("G43.1" + tofs.ToString(AxisFlags.Z));
                                //if (probing.Tool != "0")
                                //    probing.Grbl.ExecuteCommand(string.Format("G10L11P{0}{1}", probing.Tool, probing.Positions[0].ToString(AxisFlags.Z)));
                                probing.Positions[0].Z += g59_3.Z + probing.Depth;
                                probing.GotoMachinePosition(probing.Positions[0], AxisFlags.Z);
                            }
                        }

                        if (origin.Z > probing.Positions[0].Z)
                        {
                            probing.GotoMachinePosition(origin, AxisFlags.Z);
                            probing.GotoMachinePosition(origin, AxisFlags.X | AxisFlags.Y);
                        }
                        else
                        {
                            probing.GotoMachinePosition(origin, AxisFlags.X | AxisFlags.Y);
                            probing.GotoMachinePosition(origin, AxisFlags.Z);
                        }
                        probing.Program.End(ok ? "Probing completed" : "Probing failed");
                    }
                    origin = null;
                    break;
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }
    }
}
