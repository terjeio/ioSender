/*
 * GCodeParser.cs - part of CNC Controls library
 *
 * v0.02 / 2019-10-29 / Io Engineering (Terje Io)
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
using System.IO;
using System.Globalization;
using System.Windows;

namespace CNC.Core
{
    public class GCodeParser
    {

        #region Helper classes, enums etc.

        internal class GCValues
        {
            public double D;
            public double E;
            public double F;
            public double[] IJK = new double[3];
            public double K;
            public double P;
            public double Q;
            public double R;
            public double S;
            public double[] XYZ = new double[6];
            public uint N;
            public int H;
            public int T;
            public int L;

            public void Clear()
            {
                D = E = F = K = P = Q = R = S = 0d;
                N = 0;
                H = T = L = 0;
                for (int i = 0; i < XYZ.Length; i++)
                    XYZ[i] = 0d;
                for (int i = 0; i < IJK.Length; i++)
                    IJK[i] = double.NaN;
            }
        }

        [Flags]
        private enum ModalGroups : int
        {
            G0 = 1 << 0,  // [G4,G10,G28,G28.1,G30,G30.1,G53,G92,G92.1] Non-modal
            G1 = 1 << 1,      // [G0,G1,G2,G3,G33,G38.2,G38.3,G38.4,G38.5,G76,G80] Motion
            G2 = 1 << 2,      // [G17,G18,G19] Plane selection
            G3 = 1 << 3,      // [G90,G91] Distance mode
            G4 = 1 << 4,      // [G91.1] Arc IJK distance mode
            G5 = 1 << 5,      // [G93,G94] Feed rate mode
            G6 = 1 << 6,      // [G20,G21] Units
            G7 = 1 << 7,      // [G40] Cutter radius compensation mode. G41/42 NOT SUPPORTED.
            G8 = 1 << 8,      // [G43,G43.1,G49] Tool length offset
            G10 = 1 << 9,     // [G98,G99] Return mode in canned cycles
            G11 = 1 << 10,     // [G50,G51] Scaling
            G12 = 1 << 11,     // [G54,G55,G56,G57,G58,G59] Coordinate system selection
            G13 = 1 << 12,     // [G61] Control mode
            G14 = 1 << 13,     // [G96,G97] Spindle Speed Mode
            G15 = 1 << 14,     // [G7,G8] Lathe Diameter Mode

            M4 = 1 << 15,      // [M0,M1,M2,M30] Stopping
            M6 = 1 << 16,      // [M6] Tool change
            M7 = 1 << 17,      // [M3,M4,M5] Spindle turning
            M8 = 1 << 18,      // [M7,M8,M9] Coolant control
            M9 = 1 << 19,      // [M49,M50,M51,M53,M56] Override control
            M10 = 1 << 20      // User defined M commands
        }

        [Flags]
        private enum WordFlags : int
        {
	        A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            H = 1 << 6,
            I = 1 << 9,
            J = 1 << 10,
            K = 1 << 11,
            L = 1 << 12,
            N = 1 << 13,
            P = 1 << 14,
            R = 1 << 15,
            S = 1 << 16,
            T = 1 << 17,
            X = 1 << 18,
            Y = 1 << 19,
            Z = 1 << 20,
            Q = 1 << 21
        }

        // Modal Group G1: Motion modes
        private enum MotionMode {
            Seek = 0,                    // G0 (Default: Must be zero)
            Linear = 1,                  // G1 (Do not alter value)
            CwArc = 2,                   // G2 (Do not alter value)
            CcwArc = 3,                  // G3 (Do not alter value)
            SpindleSynchronized = 33,    // G33 (Do not alter value)
            DrillChipBreak = 73,         // G73 (Do not alter value)
            Threading = 76,              // G76 (Do not alter value)
            CannedCycle81 = 81,          // G81 (Do not alter value)
            CannedCycle82 = 82,          // G82 (Do not alter value)
            CannedCycle83 = 83,          // G83 (Do not alter value)
            CannedCycle85 = 85,          // G85 (Do not alter value)
            CannedCycle86 = 86,          // G86 (Do not alter value)
            CannedCycle89 = 89,          // G89 (Do not alter value)
            ProbeToward = 140,           // G38.2 (Do not alter value)
            ProbeTowardNoError = 141,    // G38.3 (Do not alter value)
            ProbeAway = 142,             // G38.4 (Do not alter value)
            ProbeAwayNoError = 143,      // G38.5 (Do not alter value)
            None = 80                    // G80 (Do not alter value)
        }

        private enum AxisCommand
        {
            None = 0,
            NonModal,
            MotionMode,
            ToolLengthOffset,
            Scaling
        }

        #endregion

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        private GCValues gcValues = new GCValues();
        private GCodeToken last_token = new GCodeToken();
        private GCDistanceMode distanceMode;
        private GCIJKMode ijkMode;
        private GCPlane plane;
        private GCodeToken.Commands motioncommand = GCodeToken.Commands.Undefined;
        private MotionMode motionMode;
        private AxisCommand axisCommand;

        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();

        public GCodeParser()
        {
            Reset();
        }

        public void Reset()
        {
            gcValues.Clear();
            motionMode = MotionMode.Seek;
            axisCommand = AxisCommand.None;
            plane = new GCPlane(GCodeToken.Commands.G17, 0);
            distanceMode = new GCDistanceMode(GCodeToken.Commands.G90, 0);  // Absolute
            ijkMode = new GCIJKMode(GCodeToken.Commands.G91_1, 0);          // Incremental
            Tokens.Clear();
        }

        public bool ParseBlock(string block, bool quiet)
        {
            const string ignore = "$!~?";
            const string codes = "MTSGFXYZIJKR";
            const string all = "MTFGPSXYZIJKRHD [](\r";
            const string special = "HTSFXYZIJKRD";

            WordFlags wordFlags = 0, axisWords = 0, ijkWords = 0, wordFlag = 0;
            ModalGroups modalGroups = 0, modalGroup = 0;

            bool collect = false;
            string gcode = string.Empty;
            GCodeToken.Commands cmd = GCodeToken.Commands.Undefined;
            double value;
            List<string> gcodes = new List<string>();

            block = block.ToUpper();

            if (block.Length == 0 || ignore.Contains(block.Substring(0, 1)))
                return false;
            if (quiet)
                return true;

            gcValues.N++;

            foreach (char c in block)
            {
                if (all.Contains(c))
                {
                    collect = false;

                    if (gcode != string.Empty)
                    {
                        gcodes.Add(gcode);
                        gcode = string.Empty;
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
                if (code[0] == 'G')
                {
                    value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);
                    int fv = (int)Math.Round((value - Math.Floor(value)) * 10.0, 0);
                    int iv = (int)Math.Floor(value);

                    switch (iv)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)iv;
                            cmd = (GCodeToken.Commands)iv;
                            break;

                        case 10:
                        case 28:
                        case 30:
                        case 92:
                        case 4:
                        case 53:
                            modalGroup = ModalGroups.G0;
                            break;

                        case 7:
                        case 8:
                            modalGroup = ModalGroups.G15;
                            Tokens.Add(new GCLatheMode(GCodeToken.Commands.G7 + (iv - 7), gcValues.N));
                            break;

                        case 17:
                        case 18:
                        case 19:
                            modalGroup = ModalGroups.G2;
                            Tokens.Add(plane = new GCPlane(GCodeToken.Commands.G17 + (iv - 17), gcValues.N));
                            break;

                        case 20:
                        case 21:
                            modalGroup = ModalGroups.G6;
                            Tokens.Add(new GCUnits(GCodeToken.Commands.G20 + (iv - 20), gcValues.N));
                            break;

                        case 33:
                        case 76:
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)iv;
                            break;

                        case 40:
                            modalGroup = ModalGroups.G7;
                            break;

                        case 43:
                        case 49:
                            modalGroup = ModalGroups.G8;
                            break;

                        case 50:
                        case 51:
                            modalGroup = ModalGroups.G11;
                            axisCommand = AxisCommand.Scaling;
                            break;

                        case 54:
                        case 55:
                        case 56:
                        case 57:
                        case 58:
                        case 59:
                            if (fv == 0)
                                Tokens.Add(new GCodeToken(GCodeToken.Commands.G54 + (iv - 54), gcValues.N));
                            else
                                Tokens.Add(new GCodeToken(GCodeToken.Commands.G59 + fv, gcValues.N));
                            modalGroup = ModalGroups.G12;
                            break;

                        case 80:
                            modalGroup = ModalGroups.G1;
                            Tokens.Add(new GCodeToken(GCodeToken.Commands.G80, gcValues.N));
                            axisCommand = AxisCommand.None;
                            break;

                        case 73:
                        case 81:
                        case 82:
                        case 83:
                        case 85:
                        case 86:
                        case 89:
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)iv;
                            cmd = GCodeToken.Commands.G81;
                            break;

                        case 90:
                        case 91:
                            if (fv == 0)
                            {
                                modalGroup = ModalGroups.G3;
                                Tokens.Add(distanceMode = new GCDistanceMode(GCodeToken.Commands.G90 + (iv - 90), gcValues.N));
                            }
                            else
                            {
                                modalGroup = ModalGroups.G4;
                                Tokens.Add(ijkMode = new GCIJKMode(GCodeToken.Commands.G90_1 + (iv - 90), gcValues.N));
                            }
                            break;

                        case 93:
                        case 94:
                        case 95:
                            modalGroup = ModalGroups.G5;
                            break;

                        case 96:
                        case 97:
                            modalGroup = ModalGroups.G14;
                            break;

                        case 98:
                        case 99:
                            modalGroup = ModalGroups.G11;
                            break;
                    }

                    if (modalGroups > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException("Modal group violation");
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (code[0] == 'M')
                {
                    value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);
                    int fv = (int)Math.Round((value - Math.Floor(value)) * 10.0, 0);
                    int iv = (int)Math.Floor(value);

                    switch (iv)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 30: // Stop parsing!
                            modalGroup = ModalGroups.M4;
                            break;

                        case 3:
                        case 4:
                        case 5:
                            modalGroup = ModalGroups.M7;
                            break;

                        case 7:
                        case 8:
                        case 9:
                            modalGroup = ModalGroups.M8;
                            break;

                        case 49:
                        case 50:
                        case 51:
                        case 53:
                        case 56:
                            modalGroup = ModalGroups.M9;
                            break;

                        case 61:                          
                            modalGroup = ModalGroups.M6; //??
                            break;

                        default:
                            modalGroup = ModalGroups.M10; // User defined M-codes
                            break;
                    }

                    if (modalGroups > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException("Modal group violation");
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (special.Contains(code.Substring(0, 1)))
                {
                    try
                    {
                        value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);

                        switch (code[0])
                        {
                            case 'D':
                                gcValues.D = value;
                                wordFlag = WordFlags.D;
                                break;

                            case 'E':
                                gcValues.E = value;
                                wordFlag = WordFlags.E;
                                break;

                            case 'F':
                                gcValues.F = value;
                                wordFlag = WordFlags.F;
                                break;

                            case 'H':
                                gcValues.H = (int)value;
                                wordFlag = WordFlags.L;
                                break;

                            case 'I':
                                gcValues.IJK[0] = value;
                                wordFlag = WordFlags.I;
                                ijkWords |= WordFlags.I;
                                break;

                            case 'J':
                                gcValues.IJK[1] = value;
                                wordFlag = WordFlags.J;
                                ijkWords |= WordFlags.J;
                                break;

                            case 'K':
                                gcValues.K = gcValues.IJK[2] = value;
                                wordFlag = WordFlags.K;
                                ijkWords |= WordFlags.K;
                                break;

                            case 'L':
                                gcValues.K = gcValues.L = (int)value;
                                wordFlag = WordFlags.L;
                                break;

                            case 'N':
                                gcValues.K = gcValues.N = (uint)value;
                                wordFlag = WordFlags.N;
                                break;

                            case 'P':
                                gcValues.P = value;
                                wordFlag = WordFlags.P;
                                break;

                            case 'Q':
                                gcValues.Q = value;
                                wordFlag = WordFlags.Q;
                                break;

                            case 'R':
                                gcValues.R = value;
                                wordFlag = WordFlags.R;
                                break;

                            case 'S':
                                gcValues.S = value;
                                wordFlag = WordFlags.S;
                                break;

                            case 'T':
                                gcValues.T = (int)value;
                                wordFlag = WordFlags.T;
                                if (!quiet && ToolChanged != null)
                                {
                                    if (!ToolChanged((int)value))
                                        MessageBox.Show(string.Format("Tool {0} not associated with a profile!", value.ToString()), "GCode parser", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                                break;

                            case 'X':
                                wordFlag = WordFlags.X;
                                axisWords |= WordFlags.Z;
                                //if (distance == DistanceMode.Relative)
                                //    gcValues.XYZ[0] += value;
                                //else
                                gcValues.XYZ[0] = value;
                                break;

                            case 'Y':
                                wordFlag = WordFlags.Y;
                                axisWords |= WordFlags.Y;
                                //if (distance == DistanceMode.Relative)
                                //    gcValues.XYZ[1] += value;
                                //else
                                gcValues.XYZ[1] = value;
                                break;

                            case 'Z':
                                wordFlag = WordFlags.Z;
                                axisWords |= WordFlags.Z;
                                //if (distance == DistanceMode.Relative)
                                //    gcValues.XYZ[2] += value;
                                //else
                                gcValues.XYZ[2] = value;
                                break;


                        }
                    }
                    catch (Exception e)
                    {
                        throw new GCodeException("Invalid GCode", e);
                    }
                }
            }

            if (wordFlags > 0 && wordFlags.HasFlag(wordFlag))
            {
                throw new GCodeException("Word repeated");
            }
            else
                wordFlags |= wordFlag;

            if (axisCommand == AxisCommand.MotionMode)
            {
                if (cmd == GCodeToken.Commands.Undefined)
                    cmd = motioncommand;
                else
                    motioncommand = cmd;

                if (axisWords != 0) switch(cmd)
                {
                    case GCodeToken.Commands.G0:
                    case GCodeToken.Commands.G1:
                        Tokens.Add(new GCLinearMotion(cmd, gcValues.N, gcValues.XYZ));
                        break;

                    case GCodeToken.Commands.G2:
                    case GCodeToken.Commands.G3:
                        if (wordFlags.HasFlag(WordFlags.R))
                            gcValues.IJK[0] = gcValues.IJK[1] = gcValues.IJK[2] = double.NaN;
                        Tokens.Add(new GCArc(cmd, gcValues.N, gcValues.XYZ, gcValues.IJK, gcValues.R, ijkMode.IJKMode));
                        break;

                    case GCodeToken.Commands.G81:
                        Tokens.Add(new GCCannedDrill(cmd, gcValues.N, gcValues.XYZ, gcValues.R));
                        break;
                }
            }

            return true;
        }

        public static void Save(string filePath, List<GCodeToken> objToSerialize)
        {
            try
            {
                using (Stream stream = File.Open(filePath, FileMode.Create))
                {
                    System.Xml.Serialization.XmlSerializer bin = new System.Xml.Serialization.XmlSerializer(typeof(List<GCodeToken>), new[] {
                        typeof(GCodeToken),
                        typeof(GCLinearMotion),
                        typeof(GCArc),
                        typeof(GCPlane),
                        typeof(GCDistanceMode),
                        typeof(GCIJKMode),
                        typeof(GCUnits),
                        typeof(GCLatheMode),
                        typeof(GCCoordinateSystem),
                        typeof(GCToolTable)
                    });
                    bin.Serialize(stream, objToSerialize);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    #region Classes for GCode tokens

    public class GCodeToken
    {

        #region Commands enum

        public enum Commands
        {
            G0,
            G1,
            G2,
            G3,
            G7,
            G8,
            G17,
            G18,
            G19,
            G20,
            G21,
            G29,
            G30,
            G33,
            G40,
            G43,
            G49,
            G50,
            G51,
            G53,
            G54,
            G55,
            G56,
            G57,
            G58,
            G59,
            G59_1,
            G59_2,
            G59_3,
            G73,
            G76,
            G80,
            G81,
            G82,
            G83,
            G85,
            G86,
            G89,
            G90,
            G91,
            G90_1,
            G91_1,
            G92,
            G93,
            G94,
            G95,
            G96,
            G97,
            G98,
            G99,
            M0,
            M1,
            M2,
            M3,
            M4,
            M5,
            M6,
            M7,
            M8,
            M9,
            M30,
            M49,
            M50,
            M51,
            M53,
            M56,
            M61,
            Undefined
        }

        #endregion

        public uint LineNumber { get; set; }
        public Commands Command { get; set; }

        public GCodeToken()
        {
            Command = Commands.Undefined;
        }

        public GCodeToken(Commands command, uint lnr)
        {
            Command = command;
            LineNumber = lnr;
        }
    }

    public class GCLinearMotion : GCodeToken
    {
        public GCLinearMotion()
        { }

        public GCLinearMotion(Commands command, uint lnr, double[] values) : base(command, lnr)
        {
            X = values[0];
            Y = values[1];
            Z = values[2];
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }


    public class GCArc : GCodeToken
    {
        public GCArc()
        { }

        public GCArc(Commands cmd, uint lnr, double[] xyz_values, double[] ijk_values, double r, IJKMode ijkMode)
        {
            Command = cmd;
            LineNumber = lnr;

            X = xyz_values[0];
            Y = xyz_values[1];
            Z = xyz_values[2];

            I = ijk_values[0];
            J = ijk_values[1];
            K = ijk_values[2];

            R = r;
            IJKMode = ijkMode;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double I { get; set; }
        public double J { get; set; }
        public double K { get; set; }
        public double R { get; set; }

        public IJKMode IJKMode { get; set; }
        public bool IsRadiusMode { get { return double.IsNaN(I) && double.IsNaN(J) && double.IsNaN(K); } }
        public bool IsClocwise { get { return Command == Commands.G2; } }
    }

    public class GCCannedDrill : GCodeToken
    {
        public GCCannedDrill()
        { }

        public GCCannedDrill(Commands command, uint lnr, double[] values, double r) : base(command, lnr)
        {
            X = values[0];
            Y = values[1];
            Z = values[2];
            R = r;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double R { get; set; }
    }

    public class GCCoordinateSystem : GCodeToken
    {
        public GCCoordinateSystem()
        { }

        public GCCoordinateSystem(Commands cmd, uint lnr, uint p, double[] xyz_values)
        {
            Command = cmd;
            LineNumber = lnr;

            X = xyz_values[0];
            Y = xyz_values[1];
            Z = xyz_values[2];

            P = p;
        }

        public uint P { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class GCToolTable : GCodeToken
    {
        public GCToolTable()
        { }

        public GCToolTable(Commands cmd, uint lnr, uint p, double r, double[] xyz_values)
        {
            Command = cmd;
            LineNumber = lnr;

            X = xyz_values[0];
            Y = xyz_values[1];
            Z = xyz_values[2];

            P = p;
            R = r;
        }

        public uint P { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double R { get; set; }
    }

    public class GCPlane : GCodeToken
    {
        public GCPlane()
        { }

        public GCPlane(Commands cmd, uint lnr) : base(cmd, lnr)
        {
        }

        public Plane Plane { get { return Command == Commands.G17 ? Plane.XY : (Command == Commands.G18 ? Plane.YZ : Plane.ZX); }}
    }

    public class GCDistanceMode : GCodeToken
    {
        public GCDistanceMode()
        { }

        public GCDistanceMode(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public DistanceMode DistanceMode { get { return Command == Commands.G90 ? DistanceMode.Absolute : DistanceMode.Relative; } }
    }

    public class GCIJKMode : GCodeToken
    {
        public GCIJKMode()
        { }

        public GCIJKMode(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public IJKMode IJKMode { get { return Command == Commands.G90_1 ? IJKMode.Absolute : IJKMode.Incremental; } }
    }

    public class GCUnits : GCodeToken
    {
        public GCUnits()
        { }

        public GCUnits(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public bool Imperial { get { return Command == Commands.G20; } }
        public bool Metric { get { return Command == Commands.G21; } }
    }

    public class GCLatheMode : GCodeToken
    {
        public GCLatheMode()
        { }

        public GCLatheMode(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public LatheMode LatheMode { get { return Command == Commands.G7 ? LatheMode.Diameter : LatheMode.Radius; } }
    }

    #endregion

    public class ControlledPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }

    [Serializable]
    public class GCodeException : Exception
    {
        public GCodeException()
        {
        }

        public GCodeException(string message) : base(message)
        {
        }

        public GCodeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
