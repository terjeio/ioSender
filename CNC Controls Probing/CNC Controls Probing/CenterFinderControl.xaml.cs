/*
 * CenterFinderControl.xaml.cs - part of CNC Probing library
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
        private enum FindMode
        {
            XY = 0,
            X,
            Y
        }

        private int pass = 0;
        private FindMode mode = FindMode.XY;

        public CenterFinderControl()
        {
            InitializeComponent();
        }

        public ProbingType ProbingType { get { return ProbingType.CenterFinder; } }

        public void Activate(bool activate)
        {
            var probing = DataContext as ProbingViewModel;

            if (activate)
            {
                probing.Instructions = ((string)FindResource("Instructions")).Replace("\\n", "\n");
                probing.PropertyChanged += Probing_PropertyChanged;
            } else
                probing.PropertyChanged -= Probing_PropertyChanged;
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProbingViewModel.CameraPositions))
            {
                var probing = DataContext as ProbingViewModel;
                bool ok = probing.CameraPositions <= 1 || !probing.Positions[probing.CameraPositions - 1].Equals(probing.Positions[probing.CameraPositions - 2]);

                if(ok) switch (probing.CameraPositions)
                {
                    case 2:
                        if(mode == FindMode.Y)
                            ok = probing.Positions[1].X == probing.Positions[0].X && probing.Positions[1].Y > probing.Positions[0].Y;
                        else
                            ok = probing.Positions[1].X > probing.Positions[0].X && probing.Positions[1].Y == probing.Positions[0].Y;
                        break;

                    case 3:
                        ok = !probing.Positions[2].Equals(probing.Positions[0]) && probing.Positions[2].X != probing.Positions[1].X;
                        break;

                    case 4:
                        ok = !probing.Positions[3].Equals(probing.Positions[0]) &&
                                !probing.Positions[3].Equals(probing.Positions[1]) &&
                                probing.Positions[3].X == probing.Positions[2].X &&
                                probing.Positions[3].Y > probing.Positions[2].Y;
                        break;
                }

                if (!ok)
                {
                    MessageBox.Show(LibStrings.FindResource("IllegalPosition"), "ioSender", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    probing.RemoveLastPosition();
                } else
                    probing.CanApplyTransform = probing.CameraPositions == (mode == FindMode.XY ? 4 : 2);
            }
        }

        private bool CreateProgram(bool preview)
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.ProbeCenter == Center.None)
            {
                MessageBox.Show((string)FindResource("SelectType"), "Center finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            if (!probing.Program.Init())
            {
                probing.Message = (string)FindResource("InitFailed");
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

                        if (mode != FindMode.Y)
                        {
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
                        }

                        if (mode != FindMode.X)
                        {
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
                        }

                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    }
                    break;

                case Center.Outside:
                    {
                        rapidto.X -= probing.WorkpieceSizeX / 2d + XYClearance;
                        rapidto.Y -= probing.WorkpieceSizeY / 2d + XYClearance;

                        if (mode != FindMode.Y)
                        {
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
                        }

                        if (mode != FindMode.X)
                        {
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
                        }

                        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    }
                    break;
            }

            if(probing.Passes > 1)
                probing.Message = string.Format((string)FindResource("ProbingPass"), (probing.Passes - pass + 1), probing.Passes);

            return true;
        }

        public void Start(bool preview = false)
        {
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput(false) || probing.Passes == 0)
                return;

            mode = FindMode.XY;

            if (probing.WorkpieceSizeX <= 0d && probing.WorkpieceSizeY <= 0d)
            {
                probing.SetError(nameof(probing.WorkpieceSizeX), string.Format((string)FindResource("WorkPieceSize"), "X"));
                probing.SetError(nameof(probing.WorkpieceSizeY), string.Format((string)FindResource("WorkPieceSize"), "Y"));
                return;
            }

            if (probing.WorkpieceSizeX <= 0d)
            {
                mode = FindMode.Y;
             //   probing.SetError(nameof(probing.WorkpieceSizeX), "Workpiece X size cannot be 0.");
             //   return;
            }

            if (probing.WorkpieceSizeY <= 0d)
            {
                mode = FindMode.X;
                // probing.SetError(nameof(probing.WorkpieceSizeY), "Workpiece Y size cannot be 0.");
                // return;
            }

            if (mode != FindMode.Y && probing.ProbeCenter == Center.Inside && probing.WorkpieceSizeX < probing.XYClearance * 2d + probing.ProbeDiameter)
            {
                probing.SetError(nameof(probing.WorkpieceSizeX), string.Format((string)FindResource("Clearance"), "X"));
                return;
            }

            if (mode != FindMode.X && probing.ProbeCenter == Center.Inside && probing.WorkpieceSizeY < probing.XYClearance * 2d + probing.ProbeDiameter)
            {
                probing.SetError(nameof(probing.WorkpieceSizeY), string.Format((string)FindResource("Clearance"), "Y"));
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

            if (probing.IsSuccess && probing.Positions.Count != (mode == FindMode.XY ? 4 : 2))
            {
                probing.IsSuccess = false;
                probing.Program.End((string)FindResource("ProbingFailed"));
                return false;
            }

            bool ok;
            AxisFlags axisflags = (mode == FindMode.XY ? AxisFlags.XY : (mode == FindMode.X ? AxisFlags.X : AxisFlags.Y));

            if ((ok = probing.IsSuccess))
            {
                var center = new Position(probing.StartPosition);
                double X_distance, Y_distance;

                center.X = mode != FindMode.Y ? probing.Positions[0].X + (probing.Positions[1].X - probing.Positions[0].X) / 2d : 0d;
                X_distance = mode != FindMode.Y ? Math.Abs(probing.Positions[1].X - probing.Positions[0].X) : 0d;

                if (mode == FindMode.XY)
                {
                    center.Y = probing.Positions[2].Y + (probing.Positions[3].Y - probing.Positions[2].Y) / 2d;
                    Y_distance = Math.Abs(probing.Positions[2].Y - probing.Positions[3].Y);
                }
                else
                {
                    center.Y = mode != FindMode.X ? probing.Positions[0].Y + (probing.Positions[1].Y - probing.Positions[0].Y) / 2d : 0d;
                    Y_distance = mode != FindMode.X ? Math.Abs(probing.Positions[0].Y - probing.Positions[1].Y) : 0d;
                }

                switch (probing.ProbeCenter)
                {
                    case Center.Inside:
                        if (mode != FindMode.Y)
                            X_distance += probing.ProbeDiameter;
                        if (mode != FindMode.X)
                            Y_distance += probing.ProbeDiameter;
                        break;

                    case Center.Outside:
                        if (mode != FindMode.Y)
                            X_distance -= probing.ProbeDiameter;
                        if (mode != FindMode.X)
                            Y_distance -= probing.ProbeDiameter;
                        break;
                }

                ok = ok && probing.GotoMachinePosition(center, axisflags);

                if (ok && pass == 1)
                {
                    if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                    {
                        center.X = probing.ProbeOffsetX;
                        center.Y = probing.ProbeOffsetY;
                        probing.WaitForResponse("G92" + center.ToString(axisflags));
                        if (!probing.Grbl.IsParserStateLive)
                            probing.Grbl.ExecuteCommand("$G");
                    }
                    else
                    {
                        center.X += probing.ProbeOffsetX;
                        center.Y += probing.ProbeOffsetY;
                        probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, center.ToString(axisflags)));
                    }
                }

                if (!ok || pass == 1)
                    probing.Program.End(ok ? string.Format((string)FindResource("ProbingCompleted"), X_distance.ToInvariantString(), Y_distance.ToInvariantString()) : (string)FindResource("ProbingFailed"));
            }

            return ok;
        }

        private void PreviewOnCompleted()
        {
            var probing = DataContext as ProbingViewModel;
            AxisFlags axisflags = (mode == FindMode.XY ? AxisFlags.XY : (mode == FindMode.X ? AxisFlags.X : AxisFlags.Y));

            probing.Program.Clear();

            probing.Program.AddRapidToMPos(probing.StartPosition, axisflags);
            if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
            {
                var center = new Position();
                center.X = probing.ProbeOffsetX;
                center.Y = probing.ProbeOffsetY;
                probing.Program.Add("G92" + center.ToString(axisflags));
            }
            else
                probing.Program.Add(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, probing.StartPosition.ToString(axisflags)));
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            Start((DataContext as ProbingViewModel).PreviewEnable);
        }

        private void camera_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.ProbeCenter == Center.None)
            {
                MessageBox.Show((string)FindResource("SelectType"), "Center finder", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (probing.Positions.Count == (mode == FindMode.XY ? 4 : 2))
            {
                probing.IsSuccess = true;
                OnCompleted();
                probing.Positions.Clear();
                probing.CanApplyTransform = probing.PreviewEnable = false;
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }
    }
}
