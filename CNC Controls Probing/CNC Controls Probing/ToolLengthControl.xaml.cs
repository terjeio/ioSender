/*
 * ToolLengthControl.cs - part of CNC Probing library
 *
 * v0.27 / 2020-09-22 / Io Engineering (Terje Io)
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
    public partial class ToolLengthControl : UserControl, IProbeTab
    {
        private Position safeZ = null, g59_3 = null;

        public ToolLengthControl()
        {
            InitializeComponent();
        }
        public ProbingType ProbingType { get { return ProbingType.ToolLength; } }

        public void Activate()
        {
            var probing = DataContext as ProbingViewModel;
            probing.Instructions = string.Empty;
            if (!probing.Grbl.IsParserStateLive)
                probing.Grbl.ExecuteCommand(probing.Grbl.IsGrblHAL ? GrblConstants.CMD_GETPARSERSTATE : GrblConstants.CMD_GETNGCPARAMETERS);
        }

        public void Start(bool preview = false)
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

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            if (probing.ProbeFixture)
            {
                g59_3 = new Position(GrblWorkParameters.GetCoordinateSystem("G59.3"));
                safeZ = new Position(probing.StartPosition);
                safeZ.Z = System.Math.Max(g59_3.Z, probing.StartPosition.Z) + probing.Depth;
                g59_3.Z += probing.Depth;
                if (safeZ.Z < 0d)
                    probing.Program.AddRapidToMPos(safeZ, AxisFlags.Z);
                else
                    safeZ.Z = g59_3.Z;
                probing.Program.AddRapidToMPos(g59_3, AxisFlags.X | AxisFlags.Y);
                probing.Program.AddRapidToMPos(g59_3, AxisFlags.Z);
                g59_3.Z -= probing.Depth;
            }
            probing.Program.AddProbingAction(AxisFlags.Z, true);
            probing.Program.Execute(true);
            OnCompleted();
        }

        public void Stop()
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private void OnCompleted()
        {
            bool ok = true;
            var probing = DataContext as ProbingViewModel;

            if ((ok = probing.IsSuccess && probing.Positions.Count == 1))
            {
                Position pos = new Position(probing.Positions[0]);

                //if (probing.HasToolTable)
                //    probing.Grbl.ExecuteCommand(string.Format("G43H{0}{1}", probing.Tool, probing.Positions[0].ToString(AxisFlags.Z)));
                //else
                if (probing.ReferenceToolOffset)
                {
                    probing.TloReference = pos.Z; // linear axis?
                    probing.Grbl.ExecuteCommand("G49");
                }
                else
                {
                    double tlo = pos.Z - (double.IsNaN(probing.TloReference) ? 0d : probing.TloReference);
                    if (probing.ProbeFixture)
                       tlo += probing.FixtureHeight;
                    probing.Grbl.ExecuteCommand("G43.1Z" + tlo.ToInvariantString(probing.Grbl.Format));
                }

                if (probing.AddAction && (ok = probing.WaitForResponse(GrblConstants.CMD_GETNGCPARAMETERS)))
                {
                    if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                    {
                        if ((ok = probing.GotoMachinePosition(pos, AxisFlags.Z)))
                        {
                            pos.X = pos.Y = 0d;
                            pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl.ToolOffset.Z;
                            probing.Grbl.ExecuteCommand("G92" + pos.ToString(AxisFlags.Z));
                        }
                    }
                    else
                    {
                        pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl.ToolOffset.Z;
                        probing.Grbl.ExecuteCommand(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(AxisFlags.Z)));
                    }
                }

                //if (probing.Tool != "0")
                //    probing.Grbl.ExecuteCommand(string.Format("G10L11P{0}{1}", probing.Tool, probing.Positions[0].ToString(AxisFlags.Z)));

                // Go back to origin
                if (probing.ProbeFixture) {
                    probing.Program.AddRapidToMPos(safeZ, AxisFlags.Z);
                    probing.GotoMachinePosition(probing.StartPosition, AxisFlags.X | AxisFlags.Y);
                }

                probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
            }

            if (probing.ReferenceToolOffset)
            {
                probing.ReferenceToolOffset = !ok;
                if (GrblInfo.Build >= 20200805 && GrblSettings.IsGrblHAL)
                    probing.Grbl.ExecuteCommand("$TLR"); // Set tool length offset reference in controller
            }

            if (!probing.Grbl.IsParserStateLive)
                probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

            probing.Program.End(ok ? "Probing completed" : "Probing failed");
        }

        private void clearToolOffset_Click(object sender, RoutedEventArgs e)
        {
            var model = DataContext as ProbingViewModel;
            model.ReferenceToolOffset = !(model.Grbl.IsTloReferenceSet && !double.IsNaN(model.Grbl.TloReference));
            model.Grbl.ExecuteCommand("G49");
            if(!model.Grbl.IsParserStateLive)
                model.Grbl.ExecuteCommand(model.Grbl.IsGrblHAL ? GrblConstants.CMD_GETPARSERSTATE : GrblConstants.CMD_GETNGCPARAMETERS);
        }
        private void start_Click(object sender, RoutedEventArgs e)
        {
            Start();
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }
    }
}
