/*
 * CenterFinderControl.xaml.cs - part of CNC Probing library
 *
 * v0.27 / 2020-09-26 / Io Engineering (Terje Io)
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

using System;
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
    public partial class CenterFinderControl : UserControl, IProbeTab
    {
        private int pass = 0;

        public CenterFinderControl()
        {
            InitializeComponent();
        }

        public ProbingType ProbingType { get { return ProbingType.CenterFinder; } }

        public void Activate()
        {
            (DataContext as ProbingViewModel).Instructions = "Click image above to select probing action.\nPlace the probe above the approximate center of the workpiece before start.";
        }

        private bool CreateProgram(bool preview)
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.ProbeCenter == Center.None)
            {
                MessageBox.Show("Select type of probe by clicking on one of the images above.", "Center finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            if (!probing.Program.Init())
            {
                probing.Message = "Init failed!";
                return false;
            }

            if (pass == probing.Passes)
                probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

            if (preview)
                probing.StartPosition.Zero();

            var rapidto = new Position(probing.StartPosition);
            var XYClearance = probing.XYClearance + probing.ProbeDiameter / 2d;

            rapidto.Z -= probing.Depth;

            switch (probing.ProbeCenter)
            {
                case Center.Inside:
                    {
                        double rapid = probing.WorkpieceSizeX / 2d - XYClearance;

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

                        if (rapid > 1d)
                        {
                            rapidto.X -= rapid;
                            probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                            rapidto.X = probing.StartPosition.X + rapid;
                        }

                        probing.Program.AddProbingAction(AxisFlags.X, true);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);

                        probing.Program.AddProbingAction(AxisFlags.X, false);

                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X);

                        rapid = probing.WorkpieceSizeY / 2d - XYClearance;
                        if (rapid > 1d)
                        {
                            rapidto.Y -= rapid;
                            probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                            rapidto.Y = probing.StartPosition.Y + rapid;
                        }

                        probing.Program.AddProbingAction(AxisFlags.Y, true);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);

                        probing.Program.AddProbingAction(AxisFlags.Y, false);

                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Y);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    }
                    break;

                case Center.Outside:
                    {
                        rapidto.X -= probing.WorkpieceSizeX / 2d + XYClearance;
                        rapidto.Y -= probing.WorkpieceSizeY / 2d + XYClearance;

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

                        probing.Program.AddProbingAction(AxisFlags.X, false);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                        rapidto.X += probing.WorkpieceSizeX + XYClearance * 2d;
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

                        probing.Program.AddProbingAction(AxisFlags.X, true);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X);
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

                        probing.Program.AddProbingAction(AxisFlags.Y, false);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                        rapidto.Y += probing.WorkpieceSizeY + XYClearance * 2d;
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);

                        probing.Program.AddProbingAction(AxisFlags.Y, true);

                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    }
                    break;
            }

            if(probing.Passes > 1)
                probing.Message = string.Format("Probing, pass {0} of {1}...", (probing.Passes - pass + 1), probing.Passes);

            return true;
        }

        public void Start(bool preview = false)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput() || probing.Passes == 0)
                return;

            if (probing.WorkpieceSizeX <= 0d)
            {
                probing.SetError(nameof(probing.WorkpieceSizeX), "Workpiece X size cannot be 0.");
                return;
            }

            if (probing.WorkpieceSizeY <= 0d)
            {
                probing.SetError(nameof(probing.WorkpieceSizeY), "Workpiece Y size cannot be 0.");
                return;
            }

            if (probing.ProbeCenter == Center.Inside && probing.WorkpieceSizeX < probing.XYClearance * 2d + probing.ProbeDiameter)
            {
                probing.SetError(nameof(probing.WorkpieceSizeX), "Probing XY clearance too large for workpiece X size.");
                return;
            }

            if (probing.ProbeCenter == Center.Inside && probing.WorkpieceSizeY < probing.XYClearance * 2d + probing.ProbeDiameter)
            {
                probing.SetError(nameof(probing.WorkpieceSizeY), "Probing XY clearance too large for workpiece Y size.");
                return;
            }

            pass = preview ? 1 : probing.Passes;

            if (CreateProgram(preview))
            {
                do
                {
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
                } while (--pass != 0 && CreateProgram(preview));
            }
        }

        public void Stop()
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private bool OnCompleted()
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.IsSuccess && probing.Positions.Count != 4)
            {
                probing.IsSuccess = false;
                probing.Program.End("Probing failed");
                return false;
            }

            bool ok;

            if ((ok = probing.IsSuccess))
            {
                var center = new Position(probing.StartPosition);

                center.X = probing.Positions[0].X + (probing.Positions[1].X - probing.Positions[0].X) / 2d;
                center.Y = probing.Positions[2].Y + (probing.Positions[3].Y - probing.Positions[2].Y) / 2d;

                double X_distance = Math.Abs(probing.Positions[1].X - probing.Positions[0].X);
                double Y_distance = Math.Abs(probing.Positions[2].Y - probing.Positions[3].Y);

                switch (probing.ProbeCenter)
                {
                    case Center.Inside:
                        X_distance += probing.ProbeDiameter;
                        Y_distance += probing.ProbeDiameter;
                        break;

                    case Center.Outside:
                        X_distance -= probing.ProbeDiameter;
                        Y_distance -= probing.ProbeDiameter;
                        break;
                }

                ok = ok && probing.GotoMachinePosition(center, AxisFlags.X | AxisFlags.Y);

                if (ok && pass == 1)
                {
                    if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                    {
                        probing.Grbl.ExecuteCommand("G92X0Y0");
                        if (!probing.Grbl.IsParserStateLive)
                            probing.Grbl.ExecuteCommand("$G");
                    }
                    else
                        probing.Grbl.ExecuteCommand(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, center.ToString(AxisFlags.X | AxisFlags.Y)));
                }

                if (!ok || pass == 1)
                    probing.Program.End(ok ? string.Format("Probing completed: X distance {0}, Y distance {1}", X_distance.ToInvariantString(), Y_distance.ToInvariantString())  : "Probing failed");
            }

            return ok;
        }

        private void PreviewOnCompleted()
        {
            var probing = DataContext as ProbingViewModel;

            probing.Program.Clear();

            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X | AxisFlags.Y);
            if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                probing.Program.Add("G92X0Y0");
            else
                probing.Program.Add(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, probing.StartPosition.ToString(AxisFlags.X | AxisFlags.Y)));
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
