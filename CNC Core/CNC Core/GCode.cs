/*
 * GCode.cs - part of CNC Controls library
 *
 * v0.02 / 2019-09-21 / Io Engineering (Terje Io)
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
using System.Globalization;
using System.Windows.Forms;

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

        private string filename;
        private DataTable gcode = new DataTable("GCode");
        private BindingSource source = new BindingSource();
        private DistanceMode distance = DistanceMode.Absolute;
        private GCodeToken last_token = new GCodeToken();

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

            filename = "";
            source.DataSource = gcode;

            Reset();
        }

        public DataTable Data { get { return gcode; } }
        public BindingSource Source { get { return source; } }
        public bool Loaded { get { return gcode.Rows.Count > 0; } }

        public gcodeBoundingBox BoundingBox { get; private set; } = new gcodeBoundingBox();
        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();

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
                    if (ParseBlock(s.Trim() + "\r", false))
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
                    if ((ok = MessageBox.Show(string.Format("Line: {0}\rBlock: \"{1}\"\r\rContinue loading?", LineNumber, s), e.Message, MessageBoxButtons.YesNo) == DialogResult.Yes))
                        s = sr.ReadLine();
                    else
                        s = null;
                }
            }

            AddBlock("", Action.End);

            sr.Close();

            if (ok)
                FileChanged?.Invoke(filename);
            else
                CloseFile();

            return true;
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
                if (ParseBlock(block.Trim() + "\r", false))
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

//                Serializer.Save(@"d:\tokens.xml", Tokens);


                foreach (GCodeToken token in Tokens)
                {
                    min_feed = Math.Min(min_feed, token.f);
                    max_feed = Math.Max(max_feed, token.f);

                    BoundingBox.AddPoint(token.x, token.y, token.z);
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
            distance = DistanceMode.Absolute;
            last_token.Clear();
            last_token.command = GCodeToken.Command.Undefined;
            BoundingBox.Reset();
            Tokens.Clear();
            LineNumber = 0;
        }

        // IMPORTANT: block must be terminated with \r
        public bool ParseBlock(string block, bool quiet)
        {
            const string ignore = "$!~?";
            const string codes = "MTSGFXYZIJKR";
            const string all = "MTFGPSXYZIJKRHD [](\r";
            const string special = "HTSFXYZIJKRD";

            bool collect = false, axis_cmd = false;
            string gcode = "";
            GCodeToken.Command cmd = GCodeToken.Command.Undefined;
            double value;
            List<string> gcodes = new List<string>();

            block = block.ToUpper();

            if (block.Length == 0 || ignore.Contains(block.Substring(0, 1)))
                return false;
            if (quiet)
                return true;

            foreach (char c in block)
            {
                if (all.Contains(c))
                {
                    collect = false;

                    if (gcode != "")
                    {
                        gcodes.Add(gcode);
                        gcode = "";
                    }

                    if (c == '(')
                        break;

                    if (codes.Contains(c))
                    {
                        collect = true;
                        gcode += c;
                    }
                }
                else if (collect)
                    gcode += c;
            }

            foreach (string code in gcodes)
            {
                if (code.Substring(0, 1) == "G")
                {
                    if (cmd != GCodeToken.Command.Undefined)
                    {
                        last_token.command = cmd;
                        axis_cmd = false;
                        Tokens.Add(new GCodeToken(cmd, last_token));
                        cmd = GCodeToken.Command.Undefined;
                    }

                    value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);
                    int fv = (int)Math.Round((value - Math.Floor(value)) * 10.0, 0);
                    int iv = (int)Math.Floor(value);

                    switch (iv)
                    {
                        case 0:
                        case 1:
                            cmd = (GCodeToken.Command)iv;
                            break;
                        case 2:
                        case 3:
                            cmd = (GCodeToken.Command)iv;
                            break;

                        case 17:
                        case 18:
                        case 19:
                            cmd = GCodeToken.Command.G17 + (iv - 17);
                            break;

                        case 20:
                            cmd = GCodeToken.Command.G20;
                            break;

                        case 21:
                            cmd = GCodeToken.Command.G21;
                            break;

                        case 80:
                            cmd = GCodeToken.Command.G80;
                            last_token.z = last_token.r;
                            break;

                        case 81:
                            // add default retract distance?
                            cmd = GCodeToken.Command.G81;
                            break;

                        case 90:
                            if (fv == 0)
                                distance = DistanceMode.Absolute;
                            cmd = fv == 0 ? GCodeToken.Command.G90 : GCodeToken.Command.G90_1;
                            break;

                        case 91:
                            if (fv == 0)
                                distance = DistanceMode.Relative;
                            cmd = fv == 0 ? GCodeToken.Command.G91 : GCodeToken.Command.G91_1;
                            break;
                    }

                    //if (cmd != GCodeToken.Command.Undefined)
                    //{
                    //    added = true;
                    //    tokens.Add((last_token = new GCodeToken(cmd, last_token)));
                    //}

                }
                else if (special.Contains(code.Substring(0, 1)))
                {
                    try
                    {
                        value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);

                        switch (code.Substring(0, 1))
                        {
                            case "F":
                                last_token.f = value;
                                break;

                            case "I":
                                last_token.i = value;
                                break;

                            case "J":
                                last_token.j = value;
                                break;

                            case "K":
                                last_token.k = value;
                                break;

                            case "R":
                                last_token.r = value;
                                break;

                            case "T":
                                if (!quiet && ToolChanged != null)
                                {
                                    if (!ToolChanged((int)value))
                                        MessageBox.Show(string.Format("Tool {0} not associated with a profile!", value.ToString()), "GCode parser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                                break;

                            case "X":
                                axis_cmd = true;
                                if (distance == DistanceMode.Relative)
                                    last_token.x += value;
                                else
                                    last_token.x = value;
                                break;

                            case "Y":
                                axis_cmd = true;
                                if (distance == DistanceMode.Relative)
                                    last_token.y += value;
                                else
                                    last_token.y = value;
                                break;

                            case "Z":
                                axis_cmd = true;
                                if (distance == DistanceMode.Relative)
                                    last_token.z += value;
                                else
                                    last_token.z = value;
                                break;


                        }
                    }
                    catch (Exception e)
                    {
                        throw new System.ArgumentException("Invalid GCode", e);
                    }

                }
                else switch (code)
                    {
                        case "G20":
                            break;
                        case "M6":
                        case "M06":
                            string s = code;
                            break;
                    }
            }

            if (cmd != GCodeToken.Command.Undefined)
            {
                last_token.command = cmd;
                Tokens.Add(new GCodeToken(cmd, last_token));
                cmd = GCodeToken.Command.Undefined;
            }
            else if (axis_cmd && last_token.command != GCodeToken.Command.Undefined)
                Tokens.Add(new GCodeToken(last_token.command, last_token));

            return true;
        }
    }

    public class ControlledPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }

    public class gcodeBoundingBox
    {
        public gcodeBoundingBox()
        {
            Reset();
        }

        public void Reset()
        {
            MinX = MinY = MinZ = double.MaxValue;
            MaxX = MaxY = MaxZ = double.MinValue;
        }

        public void AddPoint(double x, double y, double z)
        {
            MinX = Math.Min(MinX, x);
            MaxX = Math.Max(MaxX, x);

            MinY = Math.Min(MinY, y);
            MaxY = Math.Max(MaxY, y);

            MinZ = Math.Min(MinZ, z);
            MaxZ = Math.Max(MaxZ, z);
        }

        public void Normalize()
        {
            if (MaxX == double.MinValue)
                MinX = MaxX = 0.0;

            if (MaxY == double.MinValue)
                MinY = MaxY = 0.0;

            if (MaxZ == double.MinValue)
                MinZ = MaxZ = 0.0;
        }

        public double MaxX { get; set; }
        public double MinX { get; set; }
        public double MaxY { get; set; }
        public double MinY { get; set; }
        public double MaxZ { get; set; }
        public double MinZ { get; set; }
        public double SizeX { get { return MaxX - MinX; } }
        public double SizeY { get { return MaxY - MinY; } }
        public double SizeZ { get { return MaxZ - MinZ; } }
        public double MaxSize { get { return Math.Max(Math.Max(SizeX, SizeY), SizeZ); } }
    }

    [Serializable]
    public class GCodeToken
    {
        public enum Command
        {
            G0,
            G1,
            G2,
            G3,
            G17,
            G18,
            G19,
            G20,
            G21,
            G29,
            G30,
            G50,
            G51,
            G80,
            G81,
            G90,
            G90_1,
            G91,
            G91_1,
            Undefined
        }

        public GCodeToken()
        {
            Clear();
        }

        public GCodeToken(Command command, GCodeToken values)
        {
            this.command = command;
            f = values.f;
            i = values.i;
            j = values.j;
            k = values.k;
            r = values.r;
            x = values.x;
            y = values.y;
            z = values.z;
        }

        public void Clear()
        {
            command = Command.G0;
            f = r = x = y = z = 0.0;
            i = j = k = double.NaN;
        }

        public Command command;
        public double f;
        public double i;
        public double j;
        public double k;
        public double r;
        public double x;
        public double y;
        public double z;
    }

    public static class Serializer
    {
        public static void Save(string filePath, List<GCodeToken> objToSerialize)
        {
            try
            {
                using (Stream stream = File.Open(filePath, FileMode.Create))
                {
                    System.Xml.Serialization.XmlSerializer bin = new System.Xml.Serialization.XmlSerializer((typeof(List<GCodeToken>)));
                    bin.Serialize(stream, objToSerialize);
                }
            }
            catch (IOException)
            {
            }
        }

        public static T Load<T>(string filePath) where T : new()
        {
            T rez = new T();

            try
            {
                using (Stream stream = File.Open(filePath, FileMode.Open))
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bin = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    rez = (T)bin.Deserialize(stream);
                }
            }
            catch (IOException)
            {
            }

            return rez;
        }
    }
}

