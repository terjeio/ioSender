/*
 * GCodeJob.cs - part of CNC Controls library
 *
 * v0.40 / 2022-07-12 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Windows;
using CNC.GCode;
using System.Windows.Media.Media3D;

namespace CNC.Core
{
    public enum Action
    {
        New,
        Add,
        End
    }

    public class GCodeJob
    {
//         public SpindleState BBB;

        uint LineNumber = 1;

        private string filename = string.Empty;
        private DataTable gcode = new DataTable("GCode");

        public Queue<string> commands = new Queue<string>();

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        public delegate void FileChangedHandler(string filename);
        public event FileChangedHandler FileChanged = null;

        public GCodeJob()
        {
            gcode.Columns.Add("LineNum", typeof(int));
            gcode.Columns.Add("Data", typeof(string));
            gcode.Columns.Add("Length", typeof(int));
            gcode.Columns.Add("File", typeof(bool));
            gcode.Columns.Add("IsComment", typeof(bool));
            gcode.Columns.Add("ProgramEnd", typeof(bool));
            gcode.Columns.Add("Sent", typeof(string));
            gcode.Columns.Add("Ok", typeof(bool));
            gcode.PrimaryKey = new DataColumn[] { gcode.Columns["LineNum"] };

            Reset();

            Parser.ToolChanged += Parser_ToolChanged;
        }

        private bool Parser_ToolChanged(int toolNumber)
        {
            return ToolChanged == null ? true : ToolChanged(toolNumber);
        }

        public DataTable Data { get { return gcode; } }
        public bool Loaded { get { return gcode.Rows.Count > 0; } }
        public bool HeightMapApplied { get; set; }

        public List<GCodeToken> Tokens { get { return Parser.Tokens; } }
        public GcodeBoundingBox BoundingBox { get; private set; } = new GcodeBoundingBox();
        public GCodeParser Parser { get; private set; } = new GCodeParser();

        public double min_feed { get; private set; }
        public double max_feed { get; private set; }

        public bool LoadFile(string filename)
        {
            bool ok = true, isComment;

            FileInfo file = new FileInfo(filename);

            StreamReader sr = file.OpenText();

            string block = sr.ReadLine();

            AddBlock(filename, Action.New);

            while (block != null)
            {
                try
                {
                    block = block.Trim();
                    if (Parser.ParseBlock(ref block, false, out isComment))
                    {
                        gcode.Rows.Add(new object[] { LineNumber++, block, block.Length + 1, true, isComment, Parser.ProgramEnd, "", false });
                        while (commands.Count > 0)
                        {
                            block = commands.Dequeue();
                            gcode.Rows.Add(new object[] { LineNumber++, block, block.Length + 1, true, false, false, "", false });
                        }
                    }
                    block = sr.ReadLine();
                }
                catch (Exception e)
                {
                    if ((ok = MessageBox.Show(string.Format(LibStrings.FindResource("LoadError").Replace("\\n", "\r"), e.Message, LineNumber, block), "ioSender", MessageBoxButton.YesNo) == MessageBoxResult.Yes))
                        block = sr.ReadLine();
                    else
                        block = null;
                }
            }

            sr.Close();

            if (ok)
                AddBlock("", Action.End);
            else
                CloseFile();

            return ok;
        }

        public void AddBlock(string block, Action action)
        {
            if (action == Action.New)
            {
                if (Loaded)
                    gcode.Rows.Clear();

                Reset();
                commands.Clear();
                gcode.BeginLoadData();

                filename = block;

            }
            else if (block != null && block.Trim().Length > 0) try
            {
                bool isComment;
                block = block.Trim();
                if (Parser.ParseBlock(ref block, false, out isComment))
                {
                    gcode.Rows.Add(new object[] { LineNumber++, block, block.Length + 1, true, isComment, Parser.ProgramEnd, "", false });
                    while (commands.Count > 0)
                    {
                        block = commands.Dequeue();
                        gcode.Rows.Add(new object[] { LineNumber++, block, block.Length + 1, true, false, false, "", false });
                    }
                }
            }
            catch //(Exception e)
            {
                // 
            }

            if (action == Action.End)
            {
                gcode.EndLoadData();

#if DEBUG
                System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
                stopWatch.Start();
#endif

                // Calculate program limits (bounding box)

                GCodeEmulator emu = new GCodeEmulator(true);

                foreach (var cmd in emu.Execute(Tokens))
                {
                    if(cmd.Token is GCArc)
                        BoundingBox.AddBoundingBox((cmd.Token as GCArc).GetBoundingBox(emu.Plane, new double[]{ cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCCubicSpline)
                        BoundingBox.AddBoundingBox((cmd.Token as GCCubicSpline).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCQuadraticSpline)
                        BoundingBox.AddBoundingBox((cmd.Token as GCQuadraticSpline).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCAxisCommand6)
                        BoundingBox.AddPoint(cmd.End, (cmd.Token as GCAxisCommand6).AxisFlags);
                }

                BoundingBox.Conclude();

#if DEBUG
                stopWatch.Stop();
#endif

                //GCodeParser.Save(@"d:\tokens.xml", Parser.Tokens);
                //GCodeParser.Save(@"d:\file.nc", GCodeParser.TokensToGCode(Parser.Tokens));

                FileChanged?.Invoke(filename);
            }
        }

        public void AddBlock(string block)
        {
            AddBlock(block, Action.Add);
        }

        public void CloseFile()
        {
            if (Loaded)
                gcode.Rows.Clear();

            commands.Clear();

            Reset();

            filename = "";

            FileChanged?.Invoke(filename);
        }

        private void Reset()
        {
            min_feed = double.MaxValue;
            max_feed = double.MinValue;
            BoundingBox.Reset();
            LineNumber = 1;
            HeightMapApplied = false;
            Parser.Reset();
        }
    }

    public class ProgramLimits : ViewModelBase
    {
        public ProgramLimits()
        {
            init();
        }

        private void init()
        {
            Clear();

            MinValues.PropertyChanged += MinValues_PropertyChanged;
            MaxValues.PropertyChanged += MaxValues_PropertyChanged;
        }

        public void Clear()
        {
            for (var i = 0; i < MinValues.Length; i++)
            {
                MinValues[i] = double.NaN;
                MaxValues[i] = double.NaN;
            }
        }

        public bool SuspendNotifications
        {
            get { return MinValues.SuspendNotifications; }
            set { MinValues.SuspendNotifications = MaxValues.SuspendNotifications = value; }
        }

        private void MinValues_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("Min" + e.PropertyName);
        }
        private void MaxValues_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("Max" + e.PropertyName);
        }

        public CoordinateValues<double> MinValues { get; private set; } = new CoordinateValues<double>();
        public double MinX { get { return MinValues[0]; } set { MinValues[0] = value; } }
        public double MinY { get { return MinValues[1]; } set { MinValues[1] = value; } }
        public double MinZ { get { return MinValues[2]; } set { MinValues[2] = value; } }
        public double MinA { get { return MinValues[3]; } set { MinValues[3] = value; } }
        public double MinB { get { return MinValues[4]; } set { MinValues[4] = value; } }
        public double MinC { get { return MinValues[5]; } set { MinValues[5] = value; } }
        public CoordinateValues<double> MaxValues { get; private set; } = new CoordinateValues<double>();
        public double MaxX { get { return MaxValues[0]; } set { MaxValues[0] = value; } }
        public double MaxY { get { return MaxValues[1]; } set { MaxValues[1] = value; } }
        public double MaxZ { get { return MaxValues[2]; } set { MaxValues[2] = value; } }
        public double MaxA { get { return MaxValues[3]; } set { MaxValues[3] = value; } }
        public double MaxB { get { return MaxValues[4]; } set { MaxValues[4] = value; } }
        public double MaxC { get { return MaxValues[5]; } set { MaxValues[5] = value; } }

        public double SizeX { get { return MaxX - MinX; } }
        public double SizeY { get { return MaxY - MinY; } }
        public double SizeZ { get { return MaxZ - MinZ; } }
        public double MaxSize { get { return Math.Max(Math.Max(SizeX, SizeY), SizeZ); } }
    }

    public class GcodeBoundingBox
    {
        public double[] Min = new double[6];
        public double[] Max = new double[6];
        public double[] Size = new double[6];

        public GcodeBoundingBox()
        {
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < Min.Length; i++)
            {
                Min[i] = double.MaxValue;
                Max[i] = double.MinValue;
            }
        }

        public void Conclude()
        {
            for (int i = 0; i < Min.Length; i++)
            {
                if (Max[i] == double.MinValue)
                    Min[i] = Max[i] = 0.0;
                Size[i] = Math.Abs(Max[i] - Min[i]);
            }
        }

        private void AddPoint(double x, double y, double z)
        {
            Min[0] = Math.Min(Min[0], x);
            Max[0] = Math.Max(Max[0], x);

            Min[1] = Math.Min(Min[1], y);
            Max[1] = Math.Max(Max[1], y);

            Min[2] = Math.Min(Min[2], z);
            Max[2] = Math.Max(Max[2], z);
        }

        public void AddPoint(GCPlane plane, double x, double y, double z)
        {
            Min[plane.Axis0] = Math.Min(Min[plane.Axis0], x);
            Max[plane.Axis0] = Math.Max(Max[plane.Axis0], x);

            Min[plane.Axis1] = Math.Min(Min[plane.Axis1], y);
            Max[plane.Axis1] = Math.Max(Max[plane.Axis1], y);

            Min[plane.AxisLinear] = Math.Min(Min[plane.AxisLinear], z);
            Max[plane.AxisLinear] = Math.Max(Max[plane.AxisLinear], z);
        }
        public void AddPoint(GCPlane plane, Point3D point)
        {
            Min[plane.Axis0] = Math.Min(Min[plane.Axis0], point.X);
            Max[plane.Axis0] = Math.Max(Max[plane.Axis0], point.X);

            Min[plane.Axis1] = Math.Min(Min[plane.Axis1], point.Y);
            Max[plane.Axis1] = Math.Max(Max[plane.Axis1], point.Y);

            Min[plane.AxisLinear] = Math.Min(Min[plane.AxisLinear], point.Z);
            Max[plane.AxisLinear] = Math.Max(Max[plane.AxisLinear], point.Z);
        }

        public void AddPoint(Point3D point)
        {
            Min[0] = Math.Min(Min[0], point.X);
            Max[0] = Math.Max(Max[0], point.X);

            Min[1] = Math.Min(Min[1], point.Y);
            Max[1] = Math.Max(Max[1], point.Y);

            Min[2] = Math.Min(Min[2], point.Z);
            Max[2] = Math.Max(Max[2], point.Z);
        }
        public void AddPoint(Point3D point, AxisFlags axisflags)
        {
            if (axisflags.HasFlag(AxisFlags.X))
            {
                Min[0] = Math.Min(Min[0], point.X);
                Max[0] = Math.Max(Max[0], point.X);
            }

            if (axisflags.HasFlag(AxisFlags.Y))
            { 
                Min[1] = Math.Min(Min[1], point.Y);
                Max[1] = Math.Max(Max[1], point.Y);
            }

            if (axisflags.HasFlag(AxisFlags.Z))
            {
                Min[2] = Math.Min(Min[2], point.Z);
                Max[2] = Math.Max(Max[2], point.Z);
            }
        }

        public void AddBoundingBox(GcodeBoundingBox bbox)
        {
            AddPoint(bbox.Min[0], bbox.Min[1], bbox.Min[2]);
            AddPoint(bbox.Max[0], bbox.Max[1], bbox.Max[2]);
        }
    }
}

