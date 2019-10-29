/*
 * GCode.cs - part of CNC Controls library
 *
 * v0.02 / 2019-09-23 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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
using System.Linq;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Windows;

namespace CNC.Core
{
    public enum Action
    {
        New,
        Add,
        End
    }

    [Flags]
    public enum AxisFlags : int
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        A = 1 << 3,
        B = 1 << 4,
        C = 1 << 5
    }

    public enum Plane
    {
        XY,
        ZX,
        YZ
    }

    public enum DistanceMode
    {
        Absolute,
        Relative
    }

    public enum IJKMode
    {
        Absolute,
        Incremental
    }

    [Flags]
    public enum SpindleState : int
    {
        Off = 1 << 0,
        CW = 1 << 1,
        CCW = 1 << 2
    }

    public enum ThreadTaper
    {
        None,
        Entry,
        Exit,
        Both
    }

    [Flags]
    public enum LatheMode : int
    {
        Disabled = 0,
        Diameter = 1, // Do not change
        Radius = 2    // Do not change
    }

    public class GCode
    {
//         public SpindleState BBB;

        uint LineNumber = 0;

        private string filename = string.Empty;
        private DataTable gcode = new DataTable("GCode");

        public Queue<string> commands = new Queue<string>();

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        public delegate void FileChangedHandler(string filename);
        public event FileChangedHandler FileChanged = null;

        public GCode()
        {
            gcode.Columns.Add("LineNum", typeof(int));
            gcode.Columns.Add("Data", typeof(string));
            gcode.Columns.Add("Length", typeof(int));
            gcode.Columns.Add("File", typeof(bool));
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
        public List<GCodeToken> Tokens { get { return Parser.Tokens; } }
        public GcodeBoundingBox BoundingBox { get; private set; } = new GcodeBoundingBox();
        public GCodeParser Parser { get; private set; } = new GCodeParser();

        public double min_feed { get; private set; }
        public double max_feed { get; private set; }

        public string StripSpaces(string line)
        {
            string s;
            bool skip = true;

            s = line.ToUpper();

            if (s.Contains("(MSG,"))
            {
                s = "";
                foreach (char c in line)
                {
                    switch (c)
                    {
                        case '(':
                            s += c;
                            skip = false;
                            break;
                        case ')':
                            skip = true;
                            s += c;
                            break;
                        case ' ':
                            if (!skip)
                                s += c;
                            break;
                        default:
                            s += c;
                            break;
                    }
                }
            }
            else
                s = line.Replace(" ", "");

            return s;
        }

        public bool LoadFile(string filename)
        {

            bool ok = true, end;

            FileInfo file = new FileInfo(filename);

            StreamReader sr = file.OpenText();

            string s = sr.ReadLine();

            AddBlock(filename, Action.New);

            while (s != null)
            {
                try
                {
                    if (Parser.ParseBlock(s.Trim() + "\r", false))
                    {
                        end = s == "M30" || s == "M2" || s == "M02";
                        gcode.Rows.Add(new object[] { LineNumber++, s, s.Length + 1, true, end, "", false });
                        while (commands.Count > 0)
                            gcode.Rows.Add(new object[] { LineNumber++, commands.Dequeue(), 20, true, false, "", false });
                    }
                    s = sr.ReadLine();
                }
                catch (Exception e)
                {
                    if ((ok = MessageBox.Show(string.Format("Line: {0}\rBlock: \"{1}\"\r\rContinue loading?", LineNumber, s), e.Message, MessageBoxButton.YesNo ) == MessageBoxResult.Yes))
                        s = sr.ReadLine();
                    else
                        s = null;
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
            bool end;

            if (action == Action.New)
            {
                if (Loaded)
                    gcode.Rows.Clear();

                Reset();
                commands.Clear();
                gcode.BeginLoadData();

                filename = block;

            }
            else if (block != null && block.Trim() != "") try
            {
                if (Parser.ParseBlock(block.Trim() + "\r", false))
                {
                    end = block == "M30" || block == "M2" || block == "M02";
                    gcode.Rows.Add(new object[] { LineNumber++, block, block.Length + 1, true, end, "", false });
                    while (commands.Count > 0)
                        gcode.Rows.Add(new object[] { LineNumber++, commands.Dequeue(), 20, true, false, "", false });
                }
            }
            catch //(Exception e)
            {
                // 
            }

            if (action == Action.End)
            {
                gcode.EndLoadData();

        //        GCodeParser.Save(@"d:\tokens.xml", Tokens);

                foreach (GCodeToken token in Tokens)
                {
                    //min_feed = Math.Min(min_feed, token.f);
                    //max_feed = Math.Max(max_feed, token.f);
                    if(token is GCLinearMotion)
                        BoundingBox.AddPoint(((GCLinearMotion)token).X, ((GCLinearMotion)token).Y, ((GCLinearMotion)token).Z);
                    else if (token is GCArc)
                        BoundingBox.AddPoint(((GCArc)token).X, ((GCArc)token).Y, ((GCArc)token).Z); // Expand...
                    else if (token is GCCannedDrill)
                        BoundingBox.AddPoint(((GCCannedDrill)token).X, ((GCCannedDrill)token).Y, ((GCCannedDrill)token).Z);
                }

                if (max_feed == double.MinValue)
                {
                    min_feed = 0.0;
                    max_feed = 0.0;
                }

                BoundingBox.Normalize();

                FileChanged?.Invoke(filename);
            }
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
            LineNumber = 0;
            Parser.Reset();
        }

        // IMPORTANT: block must be terminated with \r
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

        public void AddPoint(double x, double y, double z)
        {
            Min[0] = Math.Min(Min[0], x);
            Max[0] = Math.Max(Max[0], x);

            Min[1] = Math.Min(Min[1], y);
            Max[1] = Math.Max(Max[1], y);

            Min[2] = Math.Min(Min[2], z);
            Max[2] = Math.Max(Max[2], z);
        }

        public void Normalize()
        {
            if (Max[0] == double.MinValue)
                Min[0] = Max[0] = 0.0;

            if (Max[1] == double.MinValue)
                Min[1] = Max[1] = 0.0;

            if (Max[2] == double.MinValue)
                Min[2] = Max[2] = 0.0;
        }
    }
}

