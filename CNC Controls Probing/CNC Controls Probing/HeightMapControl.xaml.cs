/*
 * HeightMapControl.xaml.cs - part of CNC Probing library
 *
 * v0.22 / 2020-08-17 / Io Engineering (Terje Io)
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
using Microsoft.Win32;
using CNC.Core;
using CNC.GCode;
using HelixToolkit.Wpf;

namespace CNC.Controls.Probing
{
    /// <summary>
    /// Interaction logic for HeightMapControl.xaml
    /// </summary>
    public partial class HeightMapControl : UserControl
    {
        private Position origin = null;
        private int x, y;

        public HeightMapControl()
        {
            InitializeComponent();
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            double dir = 1d;
            var probing = DataContext as ProbingViewModel;

            if (!probing.ValidateInput())
                return;

            probing.WaitForIdle(string.Format("G90G0X{0}Y{1}", probing.HeightMap.MinX.ToInvariantString(), probing.HeightMap.MinY.ToInvariantString()));

            if (!probing.Program.Init())
                return;

            probing.PropertyChanged += Probing_PropertyChanged;

            probing.HeightMap.BoundaryPoints = null;
            probing.HeightMap.MapPoints = null;
            probing.HeightMap.MeshGeometry = null;

            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));
            probing.Message = string.Empty;

            probing.HeightMap.HasHeightMap = false;
            probing.HeightMap.Map = new HeightMap(probing.HeightMap.GridSize, new Vector2(probing.HeightMap.MinX, probing.HeightMap.MinY), new Vector2(probing.HeightMap.MaxX, probing.HeightMap.MaxY));

            origin = new Position(probing.Grbl.MachinePosition);

            for (x = 0; x < probing.HeightMap.Map.SizeX; x++)
            {
                for (y = 0; y < probing.HeightMap.Map.SizeY; y++)
                {
                    probing.Program.AddProbingAction(AxisFlags.Z, true);
                    probing.Program.AddRapid("Z" + probing.Depth.ToInvariantString());
                    if(y < (probing.HeightMap.Map.SizeY - 1))
                        probing.Program.AddRapid(string.Format("Y{0}", (probing.HeightMap.GridSize * dir).ToInvariantString()));
                }

                if (x < (probing.HeightMap.Map.SizeX - 1))
                    probing.Program.AddRapid(string.Format("X{0}", probing.HeightMap.GridSize.ToInvariantString()));

                dir *= -1d;
            }

            probing.Program.Execute(true);
        }

        private int toIndex(double val)
        {
            return (int)(Math.Ceiling(val / (DataContext as ProbingViewModel).HeightMap.GridSize - .2d));
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
                        probing.GotoMachinePosition(origin, AxisFlags.Z);
                        probing.GotoMachinePosition(origin, AxisFlags.X | AxisFlags.Y);

                        if(probing.HeightMap.SetToolOffset)
                        {
                            if(probing.CoordinateMode == ProbingViewModel.CoordMode.G10)
                                probing.Grbl.ExecuteCommand(string.Format("G10L2P{0}Z{1}", probing.CoordinateSystem, (probing.Positions[0].Z - probing.Grbl.ToolOffset.Z).ToInvariantString()));
                            else if ((ok == probing.GotoMachinePosition(probing.Positions[0], AxisFlags.Z)))
                            {
                                probing.Grbl.ExecuteCommand("G92Z0");
                                probing.GotoMachinePosition(origin, AxisFlags.Z);
                            }
                        }

                        double Z0 = probing.Positions[0].Z;

                        foreach (var pos in probing.Positions)
                            probing.HeightMap.Map.AddPoint(toIndex(pos.X - origin.X), toIndex(pos.Y - origin.Y), Math.Round(pos.Z - Z0, probing.Grbl.Precision));

                        LinesVisual3D boundary = new LinesVisual3D();
                        PointsVisual3D mapPoints = new PointsVisual3D();
                        MeshGeometryVisual3D mesh = new MeshGeometryVisual3D();

                        // TODO: fix HeightMap object...
                        probing.HeightMap.Map.GetModel(mesh);
                        probing.HeightMap.Map.GetPreviewModel(boundary, mapPoints);

                        probing.HeightMap.MeshGeometry = mesh.MeshGeometry;
                        probing.HeightMap.BoundaryPoints = boundary.Points;
                        probing.HeightMap.MapPoints = mapPoints.Points;
                        probing.HeightMap.HasHeightMap = true;

                        probing.Program.End(ok ? "Probing completed" : "Probing failed");
                    }
                    origin = null;
                    break;
            }
        }

        public void Load (string fileName)
        {
            var probing = DataContext as ProbingViewModel;

            probing.HeightMap.HasHeightMap = false;
            probing.HeightMap.Map = HeightMap.Load(fileName);
            probing.HeightMap.GridSize = probing.HeightMap.Map.GridX;
            probing.HeightMap.MinX = probing.HeightMap.Map.Min.X;
            probing.HeightMap.MinY = probing.HeightMap.Map.Min.Y;
            probing.HeightMap.MaxX = probing.HeightMap.Map.Max.X;
            probing.HeightMap.MaxY = probing.HeightMap.Map.Max.Y;

            LinesVisual3D boundary = new LinesVisual3D();
            PointsVisual3D mapPoints = new PointsVisual3D();
            MeshGeometryVisual3D mesh = new MeshGeometryVisual3D();

            // TODO: fix HeightMap object...
            probing.HeightMap.Map.GetModel(mesh);
            probing.HeightMap.Map.GetPreviewModel(boundary, mapPoints);

            probing.HeightMap.MeshGeometry = mesh.MeshGeometry;
            probing.HeightMap.BoundaryPoints = boundary.Points;
            probing.HeightMap.MapPoints = mapPoints.Points;
            probing.HeightMap.HasHeightMap = true;
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ProbingViewModel).Program.Cancel();
        }

        private void load_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            OpenFileDialog file = new OpenFileDialog();

            file.Title = "Load heightmap";
            file.Filter = string.Format("Heightmap files (*.map)|*.map|All files (*.*)|*.*");

            if (file.ShowDialog() == true)
                Load(file.FileName);
        }

        private void save_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            if (probing.HeightMap.Map == null)
                return;

            SaveFileDialog file = new SaveFileDialog();

            file.AddExtension = true;
            file.CheckPathExists = true;
            file.Title = "Save heightmap";
            file.Filter = string.Format("Heightmap files (*.map)|*.map|All files (*.*)|*.*");

            if (file.ShowDialog() == true)
            {
                probing.HeightMap.Map.Save(file.FileName);

            }
        }

        private void apply_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            using (new UIUtils.WaitCursor())
            {

                GCodeTransform gt = new GCodeTransform(probing);

                gt.ApplyHeightMap(probing);
            }
        }

        private void limits_Click(object sender, RoutedEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            probing.HeightMap.MinX = probing.Grbl.ProgramLimits.MinX;
            probing.HeightMap.MinY = probing.Grbl.ProgramLimits.MinY;
            probing.HeightMap.MaxX = probing.Grbl.ProgramLimits.MaxX;
            probing.HeightMap.MaxY = probing.Grbl.ProgramLimits.MaxY;
        }

        private void viewport_Drag(object sender, DragEventArgs e)
        {
            var probing = DataContext as ProbingViewModel;

            bool allow = !probing.Grbl.IsJobRunning;

            if (allow && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                allow = files.Length == 1 && FileUtils.IsAllowedFile(files[0].ToLower(), "map");
            }

            e.Handled = true;
            e.Effects = allow ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void viewport_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (files.Length == 1)
                Load(files[0]);
        }
    }
}
