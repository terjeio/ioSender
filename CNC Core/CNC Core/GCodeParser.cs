/*
 * GCodeParser.cs - part of CNC Controls library
 *
 * v0.40 / 2022-07-14 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2022, Io Engineering (Terje Io)
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
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using CNC.Core;
using System.Text;

namespace CNC.GCode
{
    public class GCodeParser : Machine
    {
        #region Helper classes, enums etc.

        public enum CommandIgnoreState
        {
            No = 0,
            Prompt,
            Strip,
        }

        public static readonly IJKFlags[] IjkFlag = { IJKFlags.I, IJKFlags.J, IJKFlags.K };
        public static readonly AxisFlags[] AxisFlag = { AxisFlags.X, AxisFlags.Y, AxisFlags.Z, AxisFlags.A, AxisFlags.B, AxisFlags.C };

        internal class GCValues
        {
            public double D;
            public double E;
            public double F;
            public double[] IJK = new double[3];
            public double P;
            public double Q;
            public double R;
            public double S;
            public double[] XYZ = new double[6];
            public uint N;
            public int H;
            public int T;
            public int L;

            public double X { get { return XYZ[0]; } set { XYZ[0] = value; } }
            public double Y { get { return XYZ[1]; } set { XYZ[1] = value; } }
            public double Z { get { return XYZ[2]; } set { XYZ[2] = value; } }
            public double A { get { return XYZ[3]; } set { XYZ[3] = value; } }
            public double B { get { return XYZ[4]; } set { XYZ[4] = value; } }
            public double C { get { return XYZ[5]; } set { XYZ[5] = value; } }

            public double I { get { return IJK[0]; } set { IJK[0] = value; } }
            public double J { get { return IJK[1]; } set { IJK[1] = value; } }
            public double K { get { return IJK[2]; } set { IJK[2] = value; } }

            public void Clear()
            {
                D = E = F = P = Q = R = S = 0d;
                N = 0;
                H = T = L = 0;
                for (int i = 0; i < XYZ.Length; i++)
                    XYZ[i] = 0d;
                for (int i = 0; i < IJK.Length; i++)
                    IJK[i] = 0d;
            }
        }

        [Flags]
        private enum ModalGroups : int
        {
            G0 = 1 << 0,    // [G4,G10,G28,G28.1,G30,G30.1,G53,G92,G92.1] Non-modal
            G1 = 1 << 1,    // [G0,G1,G2,G3,G33,G38.2,G38.3,G38.4,G38.5,G76,G80] Motion
            G2 = 1 << 2,    // [G17,G18,G19] Plane selection
            G3 = 1 << 3,    // [G90,G91] Distance mode
            G4 = 1 << 4,    // [G91.1] Arc IJK distance mode
            G5 = 1 << 5,    // [G93,G94] Feed rate mode
            G6 = 1 << 6,    // [G20,G21] Units
            G7 = 1 << 7,    // [G40] Cutter radius compensation mode. G41/42 NOT SUPPORTED.
            G8 = 1 << 8,    // [G43,G43.1,G49] Tool length offset
            G10 = 1 << 9,   // [G98,G99] Return mode in canned cycles
            G11 = 1 << 10,  // [G50,G51] Scaling
            G12 = 1 << 11,  // [G54,G55,G56,G57,G58,G59] Coordinate system selection
            G13 = 1 << 12,  // [G61] Control mode
            G14 = 1 << 13,  // [G96,G97] Spindle Speed Mode
            G15 = 1 << 14,  // [G7,G8] Lathe Diameter Mode

            M4 = 1 << 15,   // [M0,M1,M2,M30] Stopping
            M6 = 1 << 16,   // [M6] Tool change
            M7 = 1 << 17,   // [M3,M4,M5] Spindle turning
            M8 = 1 << 18,   // [M7,M8,M9] Coolant control
            M9 = 1 << 19,   // [M49,M50,M51,M53,M56] Override control
            M10 = 1 << 20   // User defined M commands
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

        private enum AxisCommand
        {
            None = 0,
            NonModal,
            MotionMode,
            ToolLengthOffset,
            Scaling
        }

        private struct StrReplace
        {
            public int Start, End;
            public string Val;

            public StrReplace (int start, int end, string val)
            {
                Start = start;
                End = end;
                Val = val;
            }
        }

        // Modal Group G8: Tool length offset

        const string ignore = "$!~?;";

        #endregion

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        private bool motionModeChanged = false;
        private GCValues gcValues = new GCValues();
        private GCodeToken last_token = new GCodeToken();
        private NGCExpr ngcexpr;
        private MotionMode motionMode;

        private uint coordSystem;
        private int demarcCount;
        private double[] splinePQ = new double[2];
        private double zorg = 0d, feedRate = 0d;

        // The following variables are only set in tandem with the modal group that triggers their use: 
        private Commands cmdNonModal = Commands.Undefined, cmdProgramFlow = Commands.Undefined, cmdPlane = Commands.Undefined, cmdOverride = Commands.Undefined;
        private Commands cmdDistMode = Commands.Undefined, cmdDistModeIJK = Commands.Undefined;
        private Commands cmdLatheMode = Commands.Undefined, cmdRetractMode = Commands.Undefined, cmdSpindleRpmMode = Commands.Undefined, cmdFeedrateMode = Commands.Undefined;
        private Commands cmdUnits = Commands.Undefined, cmdPathMode = Commands.Undefined;

        public GCodeParser()
        {
            Reset();
            ngcexpr = new NGCExpr(this);
        }

        public static CommandIgnoreState IgnoreM6 { get; set; } = CommandIgnoreState.Prompt;
        public static CommandIgnoreState IgnoreM7 { get; set; } = CommandIgnoreState.No;
        public static CommandIgnoreState IgnoreM8 { get; set; } = CommandIgnoreState.No;
        public static CommandIgnoreState IgnoreG61G64 { get; set; } = CommandIgnoreState.Strip;

        public Dialect Dialect { get; set; } = Dialect.GrblHAL;
        public int Decimals { get; private set; }
        public string NumFormat { get { return "#0." + "000000".Substring(0, Math.Min(6, Decimals)); } }

        public int ToolChanges { get; private set; }
        public bool ProgramEnd { get; private set; }
        public bool HasGoPredefinedPosition { get; private set; }
        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();

        public new void Reset()
        {
            base.Reset();

            // TODO: set defaults from grbl parser state? 
            gcValues.Clear();
            Tokens.Clear();
            ProgramEnd = HasGoPredefinedPosition = false;
            motionMode = MotionMode.G0;
            coordSystem = 0;
            demarcCount = 0;
            ToolChanges = 0;
            IsScaled = motionModeChanged = false;
            Decimals = 3;
            zorg = feedRate = 0d;
        }

        private bool VerifyIgnore(string code, CommandIgnoreState state)
        {
            bool strip = state == CommandIgnoreState.Strip;

            if (!strip && state != CommandIgnoreState.No)
                strip = MessageBox.Show(string.Format(LibStrings.FindResource("ParserStrip"), code), LibStrings.FindResource("ParserStripHdr"), MessageBoxButton.YesNo) == MessageBoxResult.Yes;

            return strip;
        }

        private double ToMetric(double v)
        {
            return IsImperial ? v * 2.54d : v;
        }

        private string TrimBlock(string block)
        {
            bool inComment = false, keep = false;
            StringBuilder sb = new StringBuilder();

            foreach (char c in block)
            {
                switch (c)
                {
                    case '\t':
                    case ' ':
                        if (inComment || keep)
                            sb.Append(' ');
                        break;

                    case '(':
                        inComment = true;
                        sb.Append(c);
                        break;

                    case ')':
                        inComment = false;
                        sb.Append(c);
                        break;

                    case ';':
                        keep = true;
                        sb.Append(c);
                        break;


                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        public bool ParseBlock(ref string line, bool quiet)
        {
            bool isComment;
            return ParseBlock(ref line, quiet, out isComment);
        }

        public bool ParseBlock(ref string line, bool quiet, out bool isComment)
        {
            WordFlags wordFlags = 0, wordFlag = 0;
            AxisFlags axisWords = AxisFlags.None;
            IJKFlags ijkWords = IJKFlags.None;
            ModalGroups modalGroups = 0, modalGroup = 0;
            List<StrReplace> replace = new List<StrReplace>();

        int userMCode = 0;
            bool isScaling = false;
            string comment = string.Empty, block;
            double value;
            AxisCommand axisCommand = AxisCommand.None;

            block = line = TrimBlock(line);

            if (block.Length == 0)
            {
                isComment = false;
                return false;
            }

            if ((isComment = block[0] == ';'))
                return true;

            if (block.Length == 0 || ignore.Contains(block[0]) || ProgramEnd)
                return false;

            if (quiet)
                return true;

            if (block[0] == '%')
            {
                if (++demarcCount == 2)
                    ProgramEnd = true;
                return true;
            }

            gcValues.N++;
            motionModeChanged = false;
            isComment = block[0] == '(' && block.LastIndexOf(')') == block.Length - 1 && !block.StartsWith("(MSG");

            int pos = 0, ppos = 0;

            while(pos < block.Length)
            {
                wordFlag = 0;
                modalGroup = 0;

                if(block[pos] == ' ')
                {
                    pos++;
                    continue;
                }

                if (char.ToUpperInvariant(block[pos]) == 'G')
                {
                    ppos = pos++;
                    if (ngcexpr.ReadParameter(block, ref pos, out value) != NGCExpr.OpStatus.OK)
                    {
                        throw new GCodeException(LibStrings.FindResource("ParserBadExpr"));
                    }
                    if (ngcexpr.WasExpression)
                        replace.Add(new StrReplace(ppos + 1, pos, value.ToInvariantString()));

                    int fv = (int)Math.Round((value - Math.Floor(value)) * 10.0, 0);
                    int iv = (int)Math.Floor(value);

                    switch (iv)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 5:
                            {
                                if (iv == 5 && Dialect == Dialect.Grbl)
                                    throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                                if (axisCommand != AxisCommand.None && cmdNonModal != Commands.G53)
                                    throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                                modalGroup = ModalGroups.G1;
                                axisCommand = AxisCommand.MotionMode;
                                MotionMode newMode = (iv == 5 && fv == 1) ? MotionMode.G5_1 : (MotionMode)(iv * 10);
                                motionModeChanged = motionMode != newMode;
                                motionMode = newMode;
                            }
                            break;

                        case 4:
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G4;
                            break;

                        case 10:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G10;
                            break;

                        case 28:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G28 + fv;
                            break;

                        case 30:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G30 + fv;
                            break;

                        case 92:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G92 + fv;
                            break;

                        case 7:
                        case 8:
                            if (Dialect == Dialect.Grbl)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            cmdLatheMode = Commands.G7 + (iv - 7);
                            modalGroup = ModalGroups.G15;
                            break;

                        case 17:
                        case 18:
                        case 19:
                            cmdPlane = Commands.G17 + (iv - 17);
                            modalGroup = ModalGroups.G2;
                            break;

                        case 20:
                        case 21:
                            cmdUnits = Commands.G20 + (iv - 20);
                            modalGroup = ModalGroups.G6;
                            if (cmdUnits == Commands.G20)
                                Decimals = 4;
                            break;

                        case 33:
                        case 76:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            zorg = gcValues.Z;
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)(iv * 10);
                            break;

                        case 38:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            axisCommand = AxisCommand.MotionMode;
                            modalGroup = ModalGroups.G1;
                            motionMode = (MotionMode)(value * 10);
                            break;

                        case 40:
                            modalGroup = ModalGroups.G7;
                            break;

                        case 43:
                        case 49:
                            if (iv == 49)
                                ToolLengthOffset = ToolLengthOffset.Cancel;
                            else
                                ToolLengthOffset = ToolLengthOffset.Enable + fv;
                            modalGroup = ModalGroups.G8;
                            break;

                        case 50:
                        case 51:
                            if (Dialect != Dialect.GrblHAL)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            // NOTE: not NIST
                            if (iv == 51 && axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            modalGroup = ModalGroups.G11;
                            axisCommand = AxisCommand.Scaling;
                            isScaling = iv == 51;
                            break;

                        case 53:
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G53;
                            break;

                        case 54:
                        case 55:
                        case 56:
                        case 57:
                        case 58:
                        case 59:
                            if (fv > 0 && Dialect == Dialect.Grbl)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            coordSystem = (uint)(iv + fv);
                            modalGroup = ModalGroups.G12;
                            break;

                        case 61:
                        case 64:
                            if (VerifyIgnore(iv == 61 ? "G61" : "G64", IgnoreG61G64))
                                replace.Add(new StrReplace(ppos, pos, string.Empty));
                            else
                            {
                                if (Dialect != Dialect.LinuxCNC && (iv != 61 || fv > 0))
                                    throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                                cmdPathMode = iv == 64 ? Commands.G64 : Commands.G61 + fv;
                                modalGroup = ModalGroups.G13;
                            }
                            break;

                        case 80:
                            //                            if (axisCommand != AxisCommand.None)
                            //                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.None;
                            motionMode = MotionMode.G80;
                            break;

                        case 73:
                        case 81:
                        case 82:
                        case 83:
                        case 85:
                        case 86:
                        case 89:
                            if (Dialect == Dialect.Grbl)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException(LibStrings.FindResource("ParserAxisError"));
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionModeChanged = motionMode != (MotionMode)(iv * 10);
                            motionMode = (MotionMode)(iv * 10);
                            break;

                        case 84:
                        case 87:
                        case 88:
                            if (fv == 0) // test to stop compiler complaining 
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            break;

                        case 90:
                            //if (Dialect != Dialect.LinuxCNC && fv == 1)
                            //    throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            if (fv == 0)
                            {
                                cmdDistMode = Commands.G90;
                                modalGroup = ModalGroups.G3;
                            }
                            else
                            {
                                cmdDistModeIJK = Commands.G90_1;
                                modalGroup = ModalGroups.G4;
                            }
                            break;

                        case 91:
                            if (fv == 0)
                            {
                                cmdDistMode = Commands.G91;
                                modalGroup = ModalGroups.G3;
                            }
                            else
                            {
                                cmdDistModeIJK = Commands.G91_1;
                                modalGroup = ModalGroups.G4;
                            }
                            break;

                        case 93:
                        case 94:
                        case 95:
                            cmdFeedrateMode = Commands.G93 + (iv - 93);
                            modalGroup = ModalGroups.G5;
                            break;

                        case 96:
                        case 97:
                            cmdSpindleRpmMode = Commands.G95 + (iv - 97);
                            modalGroup = ModalGroups.G14;
                            break;

                        case 98:
                        case 99:
                            if (Dialect == Dialect.Grbl)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            cmdRetractMode = Commands.G98 + (iv - 98);
                            modalGroup = ModalGroups.G10;
                            break;
                    }

                    if (modalGroup > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException(LibStrings.FindResource("ParserModalGrpError"));
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (char.ToUpperInvariant(block[pos]) == 'M')
                {
                    #region M-code parsing
                    ppos = pos++;
                    if (ngcexpr.ReadParameter(block, ref pos, out value) != NGCExpr.OpStatus.OK)
                    {
                        throw new GCodeException(LibStrings.FindResource("ParserBadExpr")); ;
                    }
                    if (ngcexpr.WasExpression)
                        replace.Add(new StrReplace(ppos + 1, pos, value.ToInvariantString()));

                    int fv = (int)Math.Round((value - Math.Floor(value)) * 10.0, 0);
                    int iv = (int)Math.Floor(value);

                    switch (iv)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 30:
                            cmdProgramFlow = iv == 30 ? Commands.M30 : (Commands.M0 + iv);
                            modalGroup = ModalGroups.M4;
                            break;

                        case 3:
                        case 4:
                        case 5:
                            SpindleState = iv == 5 ? SpindleState.Off : (iv == 3 ? SpindleState.CW : SpindleState.CCW);
                            modalGroup = ModalGroups.M7;
                            break;

                        case 6:
                            if (VerifyIgnore("M6", IgnoreM6))
                                replace.Add(new StrReplace(ppos, pos, string.Empty));
                            else
                                modalGroup = ModalGroups.M6;
                            break;

                        case 7:
                            if (VerifyIgnore("M7", IgnoreM7))
                                replace.Add(new StrReplace(ppos, pos, string.Empty));
                            else
                            {
                                CoolantState = CoolantState.Mist;
                                modalGroup = ModalGroups.M8;
                            }
                            break;

                        case 8:
                            if (VerifyIgnore("M8", IgnoreM8))
                                replace.Add(new StrReplace(ppos, pos, string.Empty));
                            else
                            {
                                CoolantState = CoolantState.Flood;
                                modalGroup = ModalGroups.M8;
                            }
                            break;

                        case 9:
                            CoolantState = CoolantState.Off;
                            modalGroup = ModalGroups.M8;
                            break;

                        case 48:
                        case 49:
                        case 50:
                        case 51:
                        case 52:
                        case 53:
                        case 56:
                            if (Dialect == Dialect.LinuxCNC && iv == 56)
                                throw new GCodeException(LibStrings.FindResource("ParserUnsupportedCmd"));
                            cmdOverride = iv == 56 ? Commands.M56 : Commands.M48 + (iv - 48);
                            modalGroup = ModalGroups.M9;
                            break;

                        case 61:
                            modalGroup = ModalGroups.M6; //??
                            break;

                        case 62:
                        case 63:
                        case 64:
                        case 65:
                        case 66:
                        case 67:
                        case 68:
                            userMCode = iv;
                            modalGroup = ModalGroups.M10;
                            break;


                        default:
                            //                            if(iv >= 100 && iv < 200)
                            userMCode = iv;
                            modalGroup = ModalGroups.M10; // User defined M-codes
                            break;
                    }

                    #endregion

                    if (modalGroup > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException(LibStrings.FindResource("ParserModalGrpError"));
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (block[pos] == '(')
                {
                    ngcexpr.ReadComment(block, ref pos, out comment);
                }
                else if (block[pos] == '#')
                {
                    ppos = pos;
                    ngcexpr.ReadSetParameter(block, ref pos);
                    replace.Add(new StrReplace(ppos, pos, "(" + block.Substring(ppos, pos - ppos) + ")"));
                }
                else if (block[pos] == ';')
                    pos = block.Length;
                else
                {
                    #region Parse Word values

                    try
                    {
                        ppos = pos++;
                        if (ngcexpr.ReadParameter(block, ref pos, out value) != NGCExpr.OpStatus.OK)
                        {
                            throw new GCodeException(LibStrings.FindResource("ParserBadExpr"));
                        }
                        if (ngcexpr.WasExpression)
                            replace.Add(new StrReplace(ppos + 1, pos, value.ToInvariantString(NumFormat) + "(" + block.Substring(ppos + 1, pos - ppos - 1) + ")"));

                        switch (char.ToUpperInvariant(block[ppos]))
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
                                wordFlag = WordFlags.H;
                                break;

                            case 'I':
                                gcValues.I = Math.Round(value, Decimals);
                                wordFlag = WordFlags.I;
                                ijkWords |= IJKFlags.I;
                                break;

                            case 'J':
                                gcValues.J = Math.Round(value, Decimals);
                                wordFlag = WordFlags.J;
                                ijkWords |= IJKFlags.J;
                                break;

                            case 'K':
                                gcValues.K = Math.Round(value, Decimals);
                                wordFlag = WordFlags.K;
                                ijkWords |= IJKFlags.K;
                                break;

                            case 'L':
                                gcValues.L = (int)value;
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
                                break;

                            case 'X':
                                if (!ngcexpr.WasExpression)
                                {
                                    string val = block.Substring(ppos + 1, pos - ppos - 1);
                                    Decimals = Math.Max(Decimals, val.Length - val.IndexOf('.') - 1);
                                }
                                wordFlag = WordFlags.X;
                                axisWords |= GCode.AxisFlags.X;
                                gcValues.X = Math.Round(value, Decimals);
                                break;

                            case 'Y':
                                wordFlag = WordFlags.Y;
                                axisWords |= GCode.AxisFlags.Y;
                                gcValues.Y = Math.Round(value, Decimals);
                                break;

                            case 'Z':
                                wordFlag = WordFlags.Z;
                                axisWords |= GCode.AxisFlags.Z;
                                gcValues.Z = Math.Round(value, Decimals);
                                break;

                            case 'A':
                                wordFlag = WordFlags.A;
                                axisWords |= GCode.AxisFlags.A;
                                gcValues.A = Math.Round(value, Decimals);
                                break;

                            case 'B':
                                wordFlag = WordFlags.B;
                                axisWords |= GCode.AxisFlags.B;
                                gcValues.B = Math.Round(value, Decimals);
                                break;

                            case 'C':
                                wordFlag = WordFlags.C;
                                axisWords |= GCode.AxisFlags.C;
                                gcValues.C = Math.Round(value, Decimals);
                                break;

                            default:
                                throw new GCodeException(LibStrings.FindResource("ParserCmdUnknown"));
                        }
                    }
                    catch (Exception e)
                    {
                        throw new GCodeException(LibStrings.FindResource("ParserCMDInvalid"), e);
                    }
                    #endregion
                }

                if (wordFlag > 0 && wordFlags.HasFlag(wordFlag))
                {
                    throw new GCodeException(LibStrings.FindResource("ParserWordRepeated"));
                }
                else
                    wordFlags |= wordFlag;
            }

            //
            // String substitutions: strips specified G- and M-codes, replaces parameters and expressions with actual values
            //
            if(replace.Count > 0)
            {
                int i = replace.Count;
                do
                {
                    --i;
                    line = line.Substring(0, replace[i].Start) + replace[i].Val + line.Substring(replace[i].End);
                } while (i > 0);
            }

            if (line == String.Empty)
                line = "(line removed)";

            //
            // 0. Non-specific/common error-checks and miscellaneous setup
            //

            Line = (int)gcValues.N;

            //
            // 1. Comments feedback
            //
            if (comment != string.Empty)
            {
                Tokens.Add(new GCComment(Commands.Comment, gcValues.N, comment));
                comment = string.Empty;
            }

            //
            // 2. Set feed rate mode
            //

            // G93, G94, G95
            if (modalGroups.HasFlag(ModalGroups.G5))
            {
                FeedRateMode = (FeedRateMode)(cmdFeedrateMode - 93);
                Tokens.Add(new GCFeedRateMode(cmdFeedrateMode, gcValues.N));
            }

            //
            // 3. Set feed rate
            //
            if (wordFlags.HasFlag(WordFlags.F))
            {
                feedRate = IsImperial ? gcValues.F * 25.4d : gcValues.F;
                Tokens.Add(new GCFeedrate(Commands.Feedrate, gcValues.N, feedRate));
            }

            //
            // 4. Set spindle speed
            //

            // G96, G97
            if (modalGroups.HasFlag(ModalGroups.G14))
            {
                SpindleRpmMode = cmdSpindleRpmMode == Commands.G97;
                Tokens.Add(new GCodeToken(cmdSpindleRpmMode, gcValues.N));
            }

            if (wordFlags.HasFlag(WordFlags.S))
            {
                Tokens.Add(new GCSpindleRPM(Commands.SpindleRPM, gcValues.N, gcValues.S));
            }

            //
            // 5. Select tool
            //
            if (wordFlags.HasFlag(WordFlags.T))
            {
                Tool = gcValues.T;
                Tokens.Add(new GCToolSelect(Commands.ToolSelect, gcValues.N, gcValues.T));

                if (!quiet && ToolChanged != null && !ToolChanged(gcValues.T))
                    MessageBox.Show(string.Format(LibStrings.FindResource("ParserToolProfile"), gcValues.T.ToString()), "GCode parser", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (modalGroups != ModalGroups.G1)
            {
                //
                // 6. Change tool
                //

                // M6
                if (modalGroups.HasFlag(ModalGroups.M6))
                {
                    ToolChanges++;
                    Tokens.Add(new GCodeToken(Commands.M6, gcValues.N));
                }

                //
                // 7. Spindle control
                //

                // M3, M4, M5
                if (modalGroups.HasFlag(ModalGroups.M7))
                {
                    Tokens.Add(new GCSpindleState(gcValues.N, SpindleState));
                }

                //
                // 8. Coolant control
                //

                // M7, M8, M9
                if (modalGroups.HasFlag(ModalGroups.M8))
                {
                    Tokens.Add(new GCCoolantState(gcValues.N, CoolantState));
                }

                //
                // 9. Override control
                //

                // M49, M50, M51, M52, M53, M56
                if (modalGroups.HasFlag(ModalGroups.M9))
                {
                    Tokens.Add(new GCodeToken(cmdOverride, gcValues.N));
                }

                //
                // 9a. User defined M commands
                //
                if (modalGroups.HasFlag(ModalGroups.M10))
                {
                    switch(userMCode)
                    {
                        case 62:
                        case 63:
                        case 64:
                        case 65:
                            if(wordFlags.HasFlag(WordFlags.P))
                                Tokens.Add(new GCDigitalOutput(Commands.M62 + (userMCode - 62), gcValues.N, (uint)gcValues.P));
                            break;

                        case 66:
                            {
                                if (wordFlags.HasFlag(WordFlags.P) && wordFlags.HasFlag(WordFlags.E))
                                    throw new GCodeException(LibStrings.FindResource("ParserM66PandE"));
                                if (!(wordFlags.HasFlag(WordFlags.P) || wordFlags.HasFlag(WordFlags.E)))
                                    throw new GCodeException(LibStrings.FindResource("ParserM66NoPorE"));
                                uint l = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 0;
                                double q = wordFlags.HasFlag(WordFlags.Q) ? gcValues.Q : 0d;
                                if (l <= 4 && (l == 0 && q != 0d))
                                    Tokens.Add(new GCWaitOnInput(gcValues.N, wordFlags.HasFlag(WordFlags.P) ? (int)gcValues.P : -1,
                                                                              wordFlags.HasFlag(WordFlags.E) ? (int)gcValues.E : -1,
                                                                              l, gcValues.Q));
                            }
                            break;

                        case 67:
                        case 68:
                            if (wordFlags.HasFlag(WordFlags.E) && wordFlags.HasFlag(WordFlags.Q))
                                Tokens.Add(new GCAnalogOutput(Commands.M67 + (userMCode - 67), gcValues.N, (uint)gcValues.P, gcValues.Q));
                            break;

                        default:
                            Tokens.Add(new GCUserMCommand(gcValues.N, (uint)userMCode,
                                                           wordFlags.HasFlag(WordFlags.P) ? gcValues.P : double.NaN,
                                                            wordFlags.HasFlag(WordFlags.Q) ? gcValues.Q : double.NaN));
                            break;
                    }
                }

                //
                // 10. Dwell
                //

                // G4
                if ((modalGroups.HasFlag(ModalGroups.G0) && cmdNonModal == Commands.G4))
                {
                    if (wordFlags.HasFlag(WordFlags.P))
                        Tokens.Add(new GCDwell(gcValues.N, gcValues.P));
                    else
                        throw new GCodeException(LibStrings.FindResource("ParserG6NoP"));
                }

                //
                // 11. Set active plane
                //

                // G17, G18, G19
                if (modalGroups.HasFlag(ModalGroups.G2))
                {
                    Tokens.Add(Plane = new GCPlane(cmdPlane, gcValues.N));
                }

                //
                // 12. Set length units
                //

                // Lathe mode: G7, G8
                if (modalGroups.HasFlag(ModalGroups.G15))
                {
                    var mode = new GCLatheMode(cmdLatheMode, gcValues.N);
                    LatheMode = mode.LatheMode;
                    Tokens.Add(mode);
                }

                // G20, G21
                if (modalGroups.HasFlag(ModalGroups.G6))
                {
                    IsImperial = cmdUnits == Commands.G20;
                    Tokens.Add(new GCUnits(cmdUnits, gcValues.N));
                }

                // Scaling: G50, G51
                if (modalGroups.HasFlag(ModalGroups.G11))
                {
                    Tokens.Add(new GCScaling((isScaling ? Commands.G51 : Commands.G50), gcValues.N, gcValues.XYZ, axisWords));
                    IsScaled = false;
                    if (isScaling)
                    {
                        for (int i = 0; i < GrblInfo.NumAxes; i++)
                        {
                            if (axisWords.HasFlag(AxisFlag[i]))
                                scaleFactors[i] = gcValues.XYZ[i];
                            IsScaled |= scaleFactors[i] != 1d;
                        }
                        axisWords = AxisFlags.None;
                    }
                    else for (int i = 0; i < scaleFactors.Length; i++)
                        scaleFactors[i] = 1d;
                }
            }

            // Perform scaling
            if (axisWords != 0)
            {
                if (IsImperial)
                {
                    foreach(int i in axisWords.ToIndices())
                        gcValues.XYZ[i] *= 25.4d;
                }
                if (IsScaled)
                {
                    foreach (int i in axisWords.ToIndices())
                        gcValues.XYZ[i] *= scaleFactors[i];
                }
                if (LatheMode == LatheMode.Diameter && axisWords.HasFlag(AxisFlags.X))
                    gcValues.X /= 2d;
            }

            if (modalGroups != ModalGroups.G1)
            {
                //
                // 13. Cutter radius compensation
                //

                // G40, G41, G42
                if (modalGroups.HasFlag(ModalGroups.G7))
                {
                    Tokens.Add(new GCPlane(Commands.G40, gcValues.N));
                }

                //
                // 14. Tool length compensation
                //

                // G43, G43.1, G43.2, G49
                if (modalGroups.HasFlag(ModalGroups.G8))
                {
                    switch (ToolLengthOffset)
                    {
                        case ToolLengthOffset.Enable:
                            {
                                var offset = new GCToolOffset(Commands.G43, gcValues.N, (uint)gcValues.H);
                                if (SetToolOffset(offset))
                                    Tokens.Add(offset);
                                // else error?
                            }
                            break;

                        case ToolLengthOffset.ApplyAdditional:
                            {
                                var offset = new GCToolOffset(Commands.G43_2, gcValues.N, (uint)gcValues.H);
                                if(AddToolOffset(offset))
                                    Tokens.Add(offset);
                                // else error?
                            }
                            break;

                        case ToolLengthOffset.EnableDynamic:
                            {
                                var offset = new GCToolOffsets(Commands.G43_1, gcValues.N, gcValues.XYZ, axisWords);
                                DynamicToolOffset(offset);
                                Tokens.Add(offset);
                            }
                            break;

                        case ToolLengthOffset.Cancel:
                            CancelToolCompensation();
                            Tokens.Add(new GCodeToken(Commands.G49, gcValues.N));
                            break;
                    }
                }

                //
                // 15. Coordinate system selection
                //

                // G54 - G59, G59.1 - G59.3
                if (modalGroups.HasFlag(ModalGroups.G12))
                {
                    CoordSystem = (int)coordSystem - 53;
                    Tokens.Add(new GCodeToken(Commands.G54 + CoordSystem - 1, gcValues.N));
                }

                //
                // 16. Set path control mode
                //

                // G61, G61.1, G64
                if (modalGroups.HasFlag(ModalGroups.G13))
                {
                    Tokens.Add(new GCodeToken(cmdPathMode, gcValues.N));
                }

                //
                // 17. Set distance mode
                //

                // G90, G91
                if (modalGroups.HasFlag(ModalGroups.G3))
                {
                    var mode = new GCDistanceMode(cmdDistMode, gcValues.N);
                    DistanceMode = mode.DistanceMode;
                    Tokens.Add(mode);
                }

                // G90.1, G91.1
                if (modalGroups.HasFlag(ModalGroups.G4))
                {
                    var mode = new GCIJKMode(cmdDistModeIJK, gcValues.N);
                    IJKMode = mode.IJKMode;
                    Tokens.Add(mode);
                }

                //
                // 18. Set retract mode
                //

                // G98, G99
                if (modalGroups.HasFlag(ModalGroups.G10))
                {
                    RetractOldZ = cmdRetractMode == Commands.G98;
                    Tokens.Add(new GCodeToken(cmdRetractMode, gcValues.N));
                }

                //
                // 19. Go to predefined position, Set G10, or Set axis offsets
                //

                // G10, G28, G28.1, G30, G30.1, G92, G92.1, G92.2, G92.3
                if (modalGroups.HasFlag(ModalGroups.G0) && cmdNonModal != Commands.Undefined)
                {
                    switch (cmdNonModal)
                    {
                        case Commands.G4:
                            // handled above
                            break;

                        case Commands.G10:
                            switch(gcValues.L)
                            {
                                case 1:
                                    {
                                        var r = wordFlags.HasFlag(WordFlags.R) ? gcValues.R : double.NaN;
                                        var offset = new GCToolTable(Commands.G10, 1, gcValues.N, (uint)gcValues.P, r, gcValues.XYZ, axisWords);
                                        if(SetToolTable(offset))
                                            Tokens.Add(offset);
                                        // else erroor?
                                    }
                                    break;

                                case 10:
                                case 11:
                                    {
                                        // to current position
                                        var r = wordFlags.HasFlag(WordFlags.R) ? gcValues.R : double.NaN;
                                        var offset = new GCToolTable(Commands.G10, gcValues.N, (uint)gcValues.L, (uint)gcValues.P, r, gcValues.XYZ, axisWords);
                                        if (SetToolTable(offset))
                                            Tokens.Add(offset);
                                        // else erroor?
                                    }
                                    break;

                                case 2:
                                case 20:
                                    {
                                        var offset = new GCCoordinateSystem(Commands.G10, gcValues.N, (uint)gcValues.L, (uint)gcValues.P, gcValues.XYZ, axisWords);
                                        if (SetCoordinateSystem(offset))
                                            Tokens.Add(offset);
                                        // else erroor?
                                    }
                                    break;
                            }
                            break;

                        case Commands.G28:
                        case Commands.G30:
                            HasGoPredefinedPosition = true;
                            Tokens.Add(new GCLinearMotion(cmdNonModal, gcValues.N, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G53:
                            if (motionMode == MotionMode.G0 || motionMode == MotionMode.G1)
                            {
                                if (modalGroups.HasFlag(ModalGroups.G1))
                                    Tokens.Add(new GCAbsLinearMotion(cmdNonModal, motionMode == MotionMode.G0 ? Commands.G0 : Commands.G1, gcValues.N, gcValues.XYZ, AxisFlags.None));
                                else
                                {
                                    Tokens.Add(new GCAbsLinearMotion(cmdNonModal, motionMode == MotionMode.G0 ? Commands.G0 : Commands.G1, gcValues.N, gcValues.XYZ, axisWords));
                                    axisWords = AxisFlags.None;
                                }
                            }
                            else
                                throw new GCodeException(LibStrings.FindResource("ParserNoG0orG1"));
                            break;

                        case Commands.G28_1:
                            Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 11, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G30_1:
                            Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 12, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G92:
                            {
                                var offset = new GCCoordinateSystem(cmdNonModal, gcValues.N, 10, gcValues.XYZ, axisWords);
                                SetG92Offset(offset);
                                Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 10, gcValues.XYZ, axisWords));
                            }
                            break;

                        case Commands.G92_1:
                            G92Clear();
                            Tokens.Add(new GCodeToken(cmdNonModal, gcValues.N));
                            break;

                        case Commands.G92_2:
                            G92Active = false;
                            Tokens.Add(new GCodeToken(cmdNonModal, gcValues.N));
                            break;

                        case Commands.G92_3:
                            G92Active = true;
                            Tokens.Add(new GCodeToken(cmdNonModal, gcValues.N));
                            break;
                    }
                    if(cmdNonModal != Commands.G53)
                        axisWords = AxisFlags.None;
                }
            }

            //
            // 20. Motion modes
            //

            // Cancel canned cycle mode: G80
            if (modalGroups.HasFlag(ModalGroups.G1) && axisCommand == AxisCommand.None)
            {
                motionMode = MotionMode.None;
                RetractOldZ = true;
                Tokens.Add(new GCodeToken(Commands.G80, gcValues.N));
            }

            if (motionMode != MotionMode.None && (axisWords != AxisFlags.None || ijkWords != IJKFlags.None || modalGroups.HasFlag(ModalGroups.G1)))
            {
                switch (motionMode)
                {
                    case MotionMode.G0:
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G0, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G1:
                        if (feedRate == 0d)
                            throw new GCodeException(LibStrings.FindResource("ParserG1NoFeed"));
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G1, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G2:
                    case MotionMode.G3:
                        RetractOldZ = true;
                        if (wordFlags.HasFlag(WordFlags.R))
                        {
                            gcValues.IJK[0] = gcValues.IJK[1] = gcValues.IJK[2] = double.NaN;
                            if (IsImperial)
                                gcValues.R *= 25.4d;
                            if (IsScaled)
                                gcValues.R *= scaleFactors[Plane.Axis0] > scaleFactors[Plane.Axis1] ? scaleFactors[Plane.Axis0] : scaleFactors[Plane.Axis1];
                        }
                        else if (ijkWords != 0)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (ijkWords.HasFlag(IjkFlag[i]))
                                {
                                    if (IsImperial)
                                        gcValues.IJK[i] *= 25.4d;
                                    if (IsScaled)
                                        gcValues.IJK[i] *= scaleFactors[i];
                                }
                                else
                                    gcValues.IJK[i] = 0d;
                            }
                        }
                        if (wordFlags.HasFlag(WordFlags.P)) {
                            if(Dialect == Dialect.Grbl)
                                throw new GCodeException(LibStrings.FindResource("ParserCMDInvalid"));
                            if(gcValues.P <= 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserCMDInvalid"));
                        }
                        Tokens.Add(new GCArc(motionMode == MotionMode.G2 ? Commands.G2 : Commands.G3, gcValues.N, gcValues.XYZ, axisWords, gcValues.IJK, ijkWords, gcValues.R, wordFlags.HasFlag(WordFlags.P) ? (int)gcValues.P : 0, IJKMode));
                        break;

                    case MotionMode.G5:
                        if (Plane.Plane != GCode.Plane.XY)
                            throw new GCodeException(LibStrings.FindResource("ParserPlaneNotXY"));
                        if (!(wordFlags.HasFlag(WordFlags.P) && wordFlags.HasFlag(WordFlags.Q)))
                            throw new GCodeException(LibStrings.FindResource("ParserNoPandorQ"));
                        if (motionModeChanged && !(wordFlags.HasFlag(WordFlags.I) && wordFlags.HasFlag(WordFlags.J)))
                            throw new GCodeException(LibStrings.FindResource("ParserNoIandorJ"));
                        if (!(wordFlags.HasFlag(WordFlags.I) && wordFlags.HasFlag(WordFlags.J)))
                        {
                            gcValues.IJK[0] = -splinePQ[0];
                            gcValues.IJK[1] = -splinePQ[1];
                        }
                        else
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                if (IsImperial)
                                    gcValues.IJK[i] *= 25.4d;
                                if (IsScaled)
                                    gcValues.IJK[i] *= scaleFactors[i];
                            }
                        }
                        splinePQ[0] = gcValues.P;
                        splinePQ[1] = gcValues.Q;
                        for (int i = 0; i < 2; i++)
                        {
                            if (IsImperial)
                                splinePQ[i] *= 25.4d;
                            if (IsScaled)
                                splinePQ[i] *= scaleFactors[i];
                        }
                        RetractOldZ = true;
                        Tokens.Add(new GCCubicSpline(Commands.G5, gcValues.N, gcValues.XYZ, axisWords, new double[] { gcValues.IJK[0], gcValues.IJK[1], splinePQ[0], splinePQ[1] }));
                        break;

                    case MotionMode.G5_1:
                        if (Plane.Plane != GCode.Plane.XY)
                            throw new GCodeException(LibStrings.FindResource("ParserPlaneNotXY"));
                        if (!(wordFlags.HasFlag(WordFlags.I) && wordFlags.HasFlag(WordFlags.J)))
                            throw new GCodeException(LibStrings.FindResource("ParserNoIandorJ"));
                        for (int i = 0; i < 2; i++)
                        {
                            if (IsImperial)
                                gcValues.IJK[i] *= 25.4d;
                            if (IsScaled)
                                gcValues.IJK[i] *= scaleFactors[i];
                        }
                        RetractOldZ = true;
                        Tokens.Add(new GCQuadraticSpline(Commands.G5_1, gcValues.N, gcValues.XYZ, axisWords, new double[] { gcValues.IJK[0], gcValues.IJK[1] }));
                        break;

                    case MotionMode.G33:
                        RetractOldZ = true;
                        Tokens.Add(new GCSyncMotion(Commands.G33, gcValues.N, gcValues.XYZ, axisWords, gcValues.K));
                        break;

                    case MotionMode.G38_2:
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G38_2, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G38_3:
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G38_3, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G38_4:
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G38_4, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G38_5:
                        RetractOldZ = true;
                        Tokens.Add(new GCLinearMotion(Commands.G38_5, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.G73:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            if (motionModeChanged && !wordFlags.HasFlag(WordFlags.R))
                                throw new GCodeException(LibStrings.FindResource("ParserNoR"));
                            if (motionModeChanged && !wordFlags.HasFlag(WordFlags.Q) || gcValues.Q <= 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserNoInvalidQ"));
                            Tokens.Add(new GCCannedDrill(Commands.G73, gcValues.N, gcValues.XYZ, axisWords, ToMetric(gcValues.R), repeats, 0d, ToMetric(gcValues.Q)));
                        }
                        break;

                    case MotionMode.G76:
                        {
                            // TODO: add check for mandatory values + sanity
                            ThreadingFlags optFlags = ThreadingFlags.None;
                            double[] optValues = new double[5];

                            if(Plane.Plane != GCode.Plane.XZ)
                                throw new GCodeException(LibStrings.FindResource("ParserPlaneNotZX"));

                            if (axisWords != AxisFlags.Z)
                                throw new GCodeException(LibStrings.FindResource("ParserZPlus"));

                            if (!wordFlags.HasFlag(WordFlags.P))
                                throw new GCodeException(LibStrings.FindResource("ParserNoP"));
                            else if(gcValues.P < 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserNegP"));

                            if (!wordFlags.HasFlag(WordFlags.I))
                                throw new GCodeException(LibStrings.FindResource("ParserNoI"));

                            if (!wordFlags.HasFlag(WordFlags.J))
                                throw new GCodeException(LibStrings.FindResource("ParserNoJ"));
                            else if (gcValues.J < 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserNegJ"));

                            if (!wordFlags.HasFlag(WordFlags.K))
                                throw new GCodeException(LibStrings.FindResource("ParserNoK"));
                            else if (gcValues.K < 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserNegK"));

                            if(gcValues.K <= gcValues.J)
                                throw new GCodeException(LibStrings.FindResource("ParserKlesseqJ"));

                            if (wordFlags.HasFlag(WordFlags.R))
                            {
                                if(gcValues.R < 1d)
                                    throw new GCodeException(LibStrings.FindResource("ParseRless1"));
                                optFlags |= ThreadingFlags.R;
                                optValues[0] = gcValues.R;
                            }
                            if (wordFlags.HasFlag(WordFlags.Q))
                            {
                                optFlags |= ThreadingFlags.Q;
                                optValues[1] = gcValues.Q;
                            }
                            if (wordFlags.HasFlag(WordFlags.H))
                            {
                                optFlags |= ThreadingFlags.H;
                                if (gcValues.H < 0d)
                                    throw new GCodeException(LibStrings.FindResource("ParserNegH"));
                                optValues[2] = gcValues.H;
                            }
                            if (wordFlags.HasFlag(WordFlags.E))
                            {
                                if (gcValues.E  > Math.Abs(gcValues.Z - zorg) / 2d)
                                    throw new GCodeException(LibStrings.FindResource("ParserErrE"));
                                optFlags |= ThreadingFlags.E;
                                optValues[3] = gcValues.E;
                            }
                            if (wordFlags.HasFlag(WordFlags.L))
                            {
                                optFlags |= ThreadingFlags.L;
                                optValues[4] = gcValues.L;
                            }

                            if (LatheMode == LatheMode.Diameter)
                                foreach (int i in ijkWords.ToIndices())
                                    gcValues.IJK[i] /= 2d;
                            RetractOldZ = true;
                            Tokens.Add(new GCThreadingMotion(Commands.G76, gcValues.N, gcValues.P, gcValues.XYZ, axisWords, gcValues.IJK, IJKFlags.All, optValues, optFlags));
                        }
                        break;

                    case MotionMode.G81:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            Tokens.Add(new GCCannedDrill(Commands.G81, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats));
                        }
                        break;

                    case MotionMode.G82:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G82, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats, dwell));
                        }
                        break;

                    case MotionMode.G83:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            if (motionModeChanged && !wordFlags.HasFlag(WordFlags.Q) || gcValues.Q <= 0d)
                                throw new GCodeException(LibStrings.FindResource("ParserNoInvalidQ"));
                            Tokens.Add(new GCCannedDrill(Commands.G83, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats, 0d, ToMetric(gcValues.Q)));
                        }
                        break;

                    case MotionMode.G85:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            Tokens.Add(new GCCannedDrill(Commands.G85, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats));
                        }
                        break;

                    case MotionMode.G86:
                        {
                            // error if spindle not running
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G86, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats, dwell));
                        }
                        break;

                    case MotionMode.G89:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G89, gcValues.N, gcValues.XYZ, axisWords, wordFlags.HasFlag(WordFlags.R) ? ToMetric(gcValues.R) : double.NaN, repeats, dwell));
                        }
                        break;
                }

                MotionMode = motionMode;
            }

            //
            // 21. Program flow
            //

            // M0, M1, M2, M30
            if (modalGroups.HasFlag(ModalGroups.M4))
            {
                ProgramEnd = cmdProgramFlow == Commands.M2 || cmdProgramFlow == Commands.M30;
                Tokens.Add(new GCodeToken(cmdProgramFlow, gcValues.N));
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
                        typeof(GCAbsLinearMotion),
                        typeof(GCArc),
                        typeof(GCCubicSpline),
                        typeof(GCQuadraticSpline),
                        typeof(GCSyncMotion),
                        typeof(GCThreadingMotion),
                        typeof(GCCannedDrill),
                        typeof(GCPlane),
                        typeof(GCDistanceMode),
                        typeof(GCFeedRateMode),
                        typeof(GCIJKMode),
                        typeof(GCUnits),
                        typeof(GCLatheMode),
                        typeof(GCCoordinateSystem),
                        typeof(GCToolTable),
                        typeof(GCToolOffset),
                        typeof(GCToolOffsets),
                        typeof(GCToolSelect),
                        typeof(GCSpindleRPM),
                        typeof(GCSpindleState),
                        typeof(GCCoolantState),
                        typeof(GCFeedrate),
                        typeof(GCComment),
                        typeof(GCDwell),
                        typeof(GCScaling),
                        typeof(GCDigitalOutput),
                        typeof(GCWaitOnInput),
                        typeof(GCAnalogOutput),
                        typeof(GCUserMCommand)
                    });
                    bin.Serialize(stream, objToSerialize);
                }
            }
            catch (IOException e)
            {
            }
        }

        public static void Save(string filePath, List<string> gcode)
        {
            try
            {
                using (StreamWriter stream = new StreamWriter(filePath))
                {
                    foreach (string block in gcode)
                        stream.WriteLine(block);
                }
            }
            catch (IOException)
            {
            }
        }

        public static List<string> TokensToGCode(List<GCodeToken> tokens, bool compress = false)
        {
            List<string> gc = new List<string>();

            string block = string.Empty;
            uint line = 0;

            bool isRelative = false, relativeChanged = true;
            GCodeToken lastMotion = new GCodeToken(Commands.Undefined, 0);

            foreach (var token in tokens)
            {
                if (line != token.LineNumber)
                {
                    if (block != string.Empty)
                    {
                        gc.Add(block);
                        block = string.Empty;
                    }
                    line = token.LineNumber;
                }

                switch (token.Command)
                {
                    case Commands.G0:
                    case Commands.G1:
                        if (compress && lastMotion.Command == token.Command)
                        {
                            if (!isRelative)
                            {
                                if (!relativeChanged)
                                {
                                    if ((token as GCLinearMotion).X == (lastMotion as GCLinearMotion).X)
                                        (token as GCLinearMotion).AxisFlags &= ~AxisFlags.X;
                                    else if ((token as GCLinearMotion).AxisFlags.HasFlag(AxisFlags.X))
                                        (lastMotion as GCLinearMotion).X = (token as GCLinearMotion).X;

                                    if ((token as GCLinearMotion).Y == (lastMotion as GCLinearMotion).Y)
                                        (token as GCLinearMotion).AxisFlags &= ~AxisFlags.Y;
                                    else if ((token as GCLinearMotion).AxisFlags.HasFlag(AxisFlags.Y))
                                        (lastMotion as GCLinearMotion).Y = (token as GCLinearMotion).Y;

                                    if ((token as GCLinearMotion).Z == (lastMotion as GCLinearMotion).Z)
                                        (token as GCLinearMotion).AxisFlags &= ~AxisFlags.Z;
                                    else if ((token as GCLinearMotion).AxisFlags.HasFlag(AxisFlags.Z))
                                        (lastMotion as GCLinearMotion).Z = (token as GCLinearMotion).Z;
                                }
                                else
                                    relativeChanged = false;
                            }
                            block += (token as GCLinearMotion).ToString().Substring(2);
                        }
                        else
                        {
                            lastMotion = token;
                            block += (token as GCLinearMotion).ToString();
                        }
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        if (compress && lastMotion.Command == token.Command)
                        {
                            if (!isRelative)
                            {
                                if (!relativeChanged)
                                {
                                    if ((token as GCArc).X == (lastMotion as GCArc).X)
                                        (token as GCArc).AxisFlags &= ~AxisFlags.X;
                                    else if ((token as GCArc).AxisFlags.HasFlag(AxisFlags.X))
                                        (lastMotion as GCArc).X = (token as GCArc).X;

                                    if ((token as GCArc).Y == (lastMotion as GCArc).Y)
                                        (token as GCArc).AxisFlags &= ~AxisFlags.Y;
                                    else if ((token as GCArc).AxisFlags.HasFlag(AxisFlags.Y))
                                        (lastMotion as GCArc).Y = (token as GCArc).Y;

                                    if ((token as GCArc).Z == (lastMotion as GCArc).Z)
                                        (token as GCArc).AxisFlags &= ~AxisFlags.Z;
                                    else if ((token as GCArc).AxisFlags.HasFlag(AxisFlags.Z))
                                        (lastMotion as GCArc).Z = (token as GCArc).Z;
                                }
                                else
                                    relativeChanged = false;
                            }
                            block += (token as GCArc).ToString().Substring(2);
                        }
                        else
                        {
                            lastMotion = token;
                            block += (token as GCArc).ToString();
                        }
                        break;

                    case Commands.G5:
                        if (compress && lastMotion.Command == token.Command)
                            block += (token as GCCubicSpline).ToString().Substring(2);
                        else
                        {
                            lastMotion = token;
                            block += (token as GCCubicSpline).ToString();
                        }
                        break;

                    case Commands.G5_1:
                        lastMotion = token;
                        block += (token as GCQuadraticSpline).ToString();
                        break;

                    case Commands.G4:
                        block += (token as GCDwell).ToString();
                        break;

                    case Commands.G20:
                        block += "G21"; // Internal representation is in millimeters for now
                        break;

                    case Commands.G28:
                    case Commands.G30:
                        lastMotion = token;
                        block += (token as GCLinearMotion).ToString();
                        break;

                    case Commands.G43:
                    case Commands.G43_2:
                        block += (token as GCToolOffset).ToString();
                        break;

                    case Commands.G43_1:
                        block += (token as GCToolOffsets).ToString();
                        break;

                    case Commands.G51:
                        block += (token as GCScaling).ToString();
                        break;

                    case Commands.G53:
                        lastMotion = token;
                        block += (token as GCAbsLinearMotion).ToString();
                        break;

                    case Commands.G73:
                    case Commands.G81:
                    case Commands.G82:
                    case Commands.G83:
                    case Commands.G85:
                    case Commands.G86:
                    case Commands.G89:
                        if (compress && lastMotion.Command == token.Command)
                        {
                            if ((token as GCCannedDrill).R == (lastMotion as GCCannedDrill).R)
                                (token as GCCannedDrill).R = double.NaN;

                            if ((token as GCCannedDrill).Z == (lastMotion as GCCannedDrill).Z)
                                (token as GCCannedDrill).AxisFlags &= ~AxisFlags.Z;
                            else if ((token as GCCannedDrill).AxisFlags.HasFlag(AxisFlags.Z))
                                (lastMotion as GCCannedDrill).Z = (token as GCCannedDrill).Z;

                            block += (token as GCCannedDrill).ToString().Substring(3);
                        }
                        else
                        {
                            lastMotion = token;
                            block += (token as GCCannedDrill).ToString();
                        }
                        break;

                    case Commands.G80:
                        lastMotion = new GCodeToken(Commands.Undefined, 0);
                        block += token.ToString();
                        break;

                    case Commands.G90:
                        isRelative = false;
                        relativeChanged = true;
                        block += token.ToString();
                        break;

                    case Commands.G91:
                        isRelative = relativeChanged = true;
                        block += token.ToString();
                        break;

                    case Commands.Feedrate:
                        block += (token as GCFeedrate).ToString();
                        break;

                    case Commands.SpindleRPM:
                        block += (token as GCSpindleRPM).ToString();
                        break;

                    case Commands.ToolSelect:
                        block += (token as GCToolSelect).ToString();
                        break;

                    case Commands.M3:
                    case Commands.M4:
                    case Commands.M5:
                        block += (token as GCSpindleState).ToString();
                        break;

                    case Commands.M7:
                    case Commands.M8:
                    case Commands.M9:
                        block += (token as GCCoolantState).ToString();
                        break;

                    case Commands.Comment:
                        block += (token as GCComment).ToString();
                        break;

                    case Commands.M62:
                    case Commands.M63:
                    case Commands.M64:
                    case Commands.M65:
                        block += (token as GCDigitalOutput).ToString();
                        break;

                    case Commands.M66:
                        block += (token as GCWaitOnInput).ToString();
                        break;

                    case Commands.M67:
                    case Commands.M68:
                        block += (token as GCAnalogOutput).ToString();
                        break;

                    case Commands.UserMCommand:
                        block += (token as GCUserMCommand).ToString();
                        break;

                    default:
                        block += token.ToString();
                        break;
                }
            }

            if (block != string.Empty)
                gc.Add(block);

            return gc; 
        }
    }

    #region Classes for GCode tokens

    public class GCodeToken
    {
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

        public new string ToString()
        {
            return Command.ToString().Replace('_', '.');
        }
    }
    public class GCAxisCommand3 : GCodeToken
    {
        public GCAxisCommand3()
        { }

        public GCAxisCommand3(Commands command, uint lnr, double[] values, AxisFlags axisFlags) : base(command, lnr)
        {
            Array.Copy(values, Values, 3);
            AxisFlags = axisFlags;
        }

        [XmlIgnore]
        public double[] Values { get; set; } = new double[3];
        public AxisFlags AxisFlags { get; set; }
        public double X { get { return Values[0]; } set { Values[0] = value; } }
        public double Y { get { return Values[1]; } set { Values[1] = value; } }
        public double Z { get { return Values[2]; } set { Values[2] = value; } }

        public new string ToString()
        {
            string s = base.ToString();

            foreach(int i in AxisFlags.ToIndices())
                s += GrblInfo.AxisIndexToLetter(i) + Values[i].ToInvariantString();

            return s;
        }
    }

    public class GCAxisCommand6 : GCodeToken
    {
        public GCAxisCommand6()
        { }

        public GCAxisCommand6(Commands command, uint lnr, double[] values, AxisFlags axisFlags) : base(command, lnr)
        {
            Array.Copy(values, Values, values.Length); // Only copy for num axes?
            AxisFlags = axisFlags;
        }

        [XmlIgnore]
        public double[] Values { get; set; } = new double[6];
        public AxisFlags AxisFlags { get; set; }
        public double X { get { return Values[0]; } set { Values[0] = value; } }
        public double Y { get { return Values[1]; } set { Values[1] = value; } }
        public double Z { get { return Values[2]; } set { Values[2] = value; } }
        public double A { get { return Values[3]; } set { Values[3] = value; } }
        public double B { get { return Values[4]; } set { Values[4] = value; } }
        public double C { get { return Values[5]; } set { Values[5] = value; } }

        public new string ToString()
        {
            string s = base.ToString();

            foreach (int i in AxisFlags.ToIndices())
                s += GrblInfo.AxisIndexToLetter(i) + Values[i].ToInvariantString();

            return s;
        }
    }

    public class AxisCommand3IJK : GCAxisCommand3
    {
        public AxisCommand3IJK()
        { }

        public AxisCommand3IJK(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double[] ijkValues, IJKFlags ijkFlags) : base(command, lnr, values, axisFlags)
        {
            Array.Copy(ijkValues, IJKvalues, 3);
            IjkFlags = ijkFlags;
        }

        public IJKFlags IjkFlags { get; set; }

        [XmlIgnore]
        public double[] IJKvalues { get; set; } = new double[3];
        public double I { get { return IJKvalues[0]; } set { IJKvalues[0] = value; } }
        public double J { get { return IJKvalues[1]; } set { IJKvalues[1] = value; } }
        public double K { get { return IJKvalues[2]; } set { IJKvalues[2] = value; } }

        public new string ToString()
        {
            string s = base.ToString();

            foreach (int i in IjkFlags.ToIndices())
                s += ((IJKFlags)(1 << i)).ToString() + IJKvalues[i].ToInvariantString();

            return s;
        }
    }

    public class GCLinearMotion : GCAxisCommand6
    {
        public GCLinearMotion()
        { }

        public GCLinearMotion(Commands command, uint lnr, double[] values, AxisFlags axisFlags) : base(command, lnr, values, axisFlags)
        { }

        public new string ToString()
        {
            return base.ToString();
        }
    }

    public class GCAbsLinearMotion : GCAxisCommand6
    {
        public GCAbsLinearMotion()
        { }

        public GCAbsLinearMotion(Commands command, Commands motion, uint lnr, double[] values, AxisFlags axisFlags) : base(command, lnr, values, axisFlags)
        {
            Motion = motion;
        }

        public Commands Motion { get; private set;  }

        public new string ToString()
        {
            return base.ToString();
        }
    }

    public class GCArc : GCAxisCommand3
    {
        private bool center_ok = false;
        private double[] center;
        private double[] end = new double[3];
        private double r = 0d;

        public GCArc()
        { }

        public GCArc(Commands cmd, uint lnr, double[] xyz_values, AxisFlags axisFlags, double[] ijk_values, IJKFlags ijkFlags, double r, int p, IJKMode ijkMode) : base(cmd, lnr, xyz_values, axisFlags)
        {
            Array.Copy(ijk_values, IJKvalues, 3);
            Array.Copy(Values, end, 3);

            IJKMode = ijkMode;
            if((IjkFlags = ijkFlags) == IJKFlags.None)
                R = this.r = r;

            P = p;
        }

        public IJKFlags IjkFlags { get; set; }

        [XmlIgnore]
        public double[] IJKvalues { get; set; } = new double[3];
        public double I { get { return IJKvalues[0]; } set { IJKvalues[0] = value; } }
        public double J { get { return IJKvalues[1]; } set { IJKvalues[1] = value; } }
        public double K { get { return IJKvalues[2]; } set { IJKvalues[2] = value; } }
        public double R { get; set; }
        public int P { get; set; }

        public IJKMode IJKMode { get; set; }
        public bool IsRadiusMode { get { return double.IsNaN(I) && double.IsNaN(J) && double.IsNaN(K); } }
        public bool IsClocwise { get { return Command == Commands.G2; } }

        public new string ToString()
        {
            string s = base.ToString();

            if (IsRadiusMode)
                s += "R" + R.ToInvariantString();
            else foreach(int i in IjkFlags.ToIndices())
                s += GCodeParser.IjkFlag[i].ToString() + IJKvalues[i].ToInvariantString();

            if (P > 0)
                s += 'P' + P.ToString();

            return s;
        }

        public double[] GetCenter(GCPlane plane, double[] start, bool isRelative = false)
        {
            if (!center_ok)
            {
                if (isRelative)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if(i != plane.AxisLinear)
                            end[i] += start[i];
                    }
                }

                if (IsRadiusMode)
                    center = convertRToCenter(plane, start);
                else
                    center = updateCenterWithCommand(plane, start);

                center_ok = true;
            }

            return center;
        }

        private int getQuadrant(double angle)
        {
            int q = 4;
            double da = Math.PI * 2d;

            while ((da - Math.PI / 2d) > angle)
            {
                q--;
                da -= Math.PI / 2d;
            }

            return q;
        }

        public GcodeBoundingBox GetBoundingBox(GCPlane plane, double[] start, bool isRelative = false)
        {
            GcodeBoundingBox bbox = new GcodeBoundingBox();

            if (!center_ok)
                GetCenter(plane, start, isRelative);

            double startAngle = GetStartAngle(plane, start, isRelative);
            double endAngle = GetEndAngle(plane, start, isRelative);

            double z1 = Math.Min(start[plane.AxisLinear], end[plane.AxisLinear]);
            double z2 = Math.Max(start[plane.AxisLinear], end[plane.AxisLinear]);

            if (startAngle == endAngle || P > 1)
            {
                bbox.AddPoint(plane, center[0] - r, center[1] - r, z1);
                bbox.AddPoint(plane, center[0] + r, center[1] + r, z2);
            }
            else
            {
                double sweep;
                double x1 = Math.Min(start[plane.Axis0], end[plane.Axis0]);
                double y1 = Math.Min(start[plane.Axis1], end[plane.Axis1]);
                double x2 = Math.Max(start[plane.Axis0], end[plane.Axis0]);
                double y2 = Math.Max(start[plane.Axis1], end[plane.Axis1]);

                // Fix semantics, if the angle ends at 0 it really should end at 360.
                //if (endAngle == 0d)
                //    endAngle = Math.PI * 2d;

                //if (startAngle == Math.PI * 2d)
                //    startAngle = 0d;

                // Calculate distance along arc.
                if (!IsClocwise && endAngle < startAngle)
                    sweep = ((Math.PI * 2d - startAngle) + endAngle);
                else if (IsClocwise && endAngle > startAngle)
                    sweep = ((Math.PI * 2d - endAngle) + startAngle);
                else
                    sweep = Math.Abs(endAngle - startAngle);

                bbox.AddPoint(plane, x1, y1, z1);
                bbox.AddPoint(plane, x2, y2, z2);

                int q = getQuadrant(startAngle);

                if (q != getQuadrant(endAngle))
                {
                    double da = Math.PI / 2d * q;

                  //  sweep -= da - startAngle;

                    while (sweep > 0d)
                    {
                        switch (q)
                        {
                            case 1:
                                if(IsClocwise)
                                    bbox.AddPoint(plane, center[0] + r, center[1], z1);
                                else
                                    bbox.AddPoint(plane, center[0], center[1] + r, z1);
                                q = IsClocwise ? 4 : 2;
                                break;

                            case 2:
                                if (IsClocwise)
                                    bbox.AddPoint(plane, center[0], center[1] + r, z1);
                                else
                                    bbox.AddPoint(plane, center[0] - r, center[1], z1);
                                q = IsClocwise ? 1 : 3;
                                break;

                            case 3:
                                if (IsClocwise)
                                    bbox.AddPoint(plane, center[0] - r, center[1], z1);
                                else
                                    bbox.AddPoint(plane, center[0], center[1] - r, z1);
                                q = IsClocwise ? 2 : 4;
                                break;

                            case 4:
                                if (IsClocwise)
                                    bbox.AddPoint(plane, center[0], center[1] - r, z1);
                                else
                                    bbox.AddPoint(plane, center[0] + r, center[1], z1);
                                q = IsClocwise ? 3 : 1;
                                break;

                            case 0:
                                break;

                        }
                        sweep -= Math.PI / 2d;
                    }
                }
            }
            bbox.Conclude();

            return bbox;
        }

        public double GetStartAngle(GCPlane plane, double[] start, bool isRelative = false)
        {
            if (!center_ok)
                GetCenter(plane, start, isRelative);

            return getAngle(center, start[plane.Axis0], start[plane.Axis1]);
        }

        public double GetEndAngle(GCPlane plane, double[] start, bool isRelative = false)
        {
            if (!center_ok)
                GetCenter(plane, start, isRelative);

            return getAngle(center, end[plane.Axis0], end[plane.Axis1]);
        }

        /*
         * Return the angle in radians when going from start to end.
         */
        private double getAngle(double[] start, double endX, double endY)
        {
            double deltaX = endX - start[0];
            double deltaY = endY - start[1];
            double angle = 0d;

            if (deltaX != 0d)
            { // prevent div by 0
                // it helps to know what quadrant you are in
                if (deltaY >= 0d)
                {
                    if (deltaX > 0d)
                    {  // 0 - 90
                        angle = Math.Atan(deltaY / deltaX);
                    }
                    else
                    { // 90 to 180
                        angle = Math.PI - Math.Abs(Math.Atan(deltaY / deltaX));
                    }
                }
                else if (deltaX < 0d)
                { // 180 - 270
                    angle = Math.PI + Math.Abs(Math.Atan(deltaY / deltaX));
                }
                else //if (deltaX > 0d && deltaY < 0d)
                { // 270 - 360
                    angle = Math.PI * 2d - Math.Abs(Math.Atan(deltaY / deltaX));
                }
            }
            else
            {
                // 90 deg
                if (deltaY > 0d)
                {
                    angle = Math.PI / 2d;
                }
                // 270 deg
                else
                {
                    angle = Math.PI * 3d / 2d;
                }
            }

            return angle;
        }

        private double[] updateCenterWithCommand(GCPlane plane, double[] initial)
        {
            double[] newPoint = new double[2];

            if (IJKMode == IJKMode.Incremental)
            {
                newPoint[0] = initial[plane.Axis0] + IJKvalues[plane.Axis0];
                newPoint[1] = initial[plane.Axis1] + IJKvalues[plane.Axis1];
            }
            else
            {
                newPoint[0] = IJKvalues[plane.Axis0];
                newPoint[1] = IJKvalues[plane.Axis1];
            }

            if(r == 0d)
                r = Math.Sqrt(IJKvalues[plane.Axis0] * IJKvalues[plane.Axis0] + IJKvalues[plane.Axis1] * IJKvalues[plane.Axis1]);

            return newPoint;
        }

        // Try to create an arc :)
        private double[] convertRToCenter(GCPlane plane, double[] start)
        {
            center = new double[2];

            // This math is copied from GRBL in gcode.c
            double x = end[plane.Axis0] - start[plane.Axis0];
            double y = end[plane.Axis1] - start[plane.Axis1];

            double h_x2_div_d = 4d * R * R - x * x - y * y;
            if (h_x2_div_d < 0d)
            {
                Console.Write(LibStrings.FindResource("ParserRadiusErr"));
            }

            h_x2_div_d = (-Math.Sqrt(h_x2_div_d)) / Math.Sqrt(x * x + y * y);

            if (!IsClocwise)
            {
                h_x2_div_d = -h_x2_div_d;
            }

            // Special message from gcoder to software for which radius
            // should be used.
            //if (radius < 0d)
            //{
            //    h_x2_div_d = -h_x2_div_d;
            //    // TODO: Places that use this need to run ABS on radius.
            //    radius = -radius;
            //}

            double offsetX = 0.5d * (x - (y * h_x2_div_d));
            double offsetY = 0.5d * (y + (x * h_x2_div_d));

            if (IJKMode == IJKMode.Incremental)
            {
                center[0] = start[plane.Axis0] + offsetX;
                center[1] = start[plane.Axis1] + offsetY;
            }
            else
            {
                center[0] = offsetX;
                center[1] = offsetY;
            }

            return center;
        }

        /*
         * Generates the points along an arc including the start and end points.
         */
        public List<Point3D> GeneratePoints(GCPlane plane, double[] start, double arcResolution, bool isRelative = false)
        {
            double sweep;
            int numPoints;
            List<Point3D> pts = new List<Point3D>();

            // Calculate angles from center.
            double startAngle = GetStartAngle(plane, start, isRelative);
            double endAngle = GetEndAngle(plane, start, isRelative);
            double delta_linear = end[plane.AxisLinear] - start[plane.AxisLinear];

            if (startAngle == endAngle)
                sweep = Math.PI * 2d;
            else
            {
                // Fix semantics, if the angle ends at 0 it really should end at 360.
                if (endAngle == 0d)
                    endAngle = Math.PI * 2d;

                // Calculate distance along arc.
                if (!IsClocwise && endAngle < startAngle)
                    sweep = ((Math.PI * 2d - startAngle) + endAngle);
                else if (IsClocwise && endAngle > startAngle)
                    sweep = ((Math.PI * 2d - endAngle) + startAngle);
                else
                    sweep = Math.Abs(endAngle - startAngle);
            }

            if (P > 1)
            {
                int passes = P - 1;
                double arc_travel = Math.PI * 2d * passes + sweep;

                if (arcResolution > 1d)
                    numPoints = (int)arcResolution;
                else
                    numPoints = (int)Math.Floor(Math.Abs(0.5d * Math.PI * 2d * r) / Math.Sqrt(arcResolution * (2.0f * r - arcResolution)));

                while(passes-- > 0)
                    pts.AddRange(generatePointsAlongArcBDring(plane, start, start, startAngle, Math.PI * 2d, numPoints, delta_linear / arc_travel * 2d * Math.PI / numPoints, false));

                delta_linear = end[plane.AxisLinear] - start[plane.AxisLinear];
            }

            if (arcResolution > 1d)
                numPoints = (int)Math.Max(8d, sweep * arcResolution / (Math.PI * 2d));
            else
                numPoints = (int)Math.Floor(Math.Abs(0.5d * sweep * r) / Math.Sqrt(arcResolution * (2.0f * r - arcResolution)));

            pts.AddRange(generatePointsAlongArcBDring(plane, start, end, startAngle, sweep, numPoints, delta_linear / numPoints, true));

            return pts;
        }

        /*
         * Generates the points along an arc including the start and end points.
         */
        private List<Point3D> generatePointsAlongArcBDring(GCPlane plane, double[] start, double[] end, double startAngle, double sweep, int numPoints, double zIncrement, bool lastTurn)
        {

            Point3D lineEnd = new Point3D();
            List<Point3D> segments = new List<Point3D>();
            double angle;

            for (int i = 0; i < numPoints; i++)
            {
                if (IsClocwise)
                    angle = (startAngle - i * sweep / numPoints);
                else
                    angle = (startAngle + i * sweep / numPoints);

                if (angle >= Math.PI * 2d)
                    angle = angle - Math.PI * 2d;

                start[plane.Axis0] = Math.Cos(angle) * r + center[0];
                start[plane.Axis1] = Math.Sin(angle) * r + center[1];

                lineEnd.X = start[0];
                lineEnd.Y = start[1];
                lineEnd.Z = start[2];

                start[plane.AxisLinear] += zIncrement;

                segments.Add(lineEnd);
            }

            if (lastTurn)
            {
                lineEnd.X = end[0];
                lineEnd.Y = end[1];
                lineEnd.Z = end[2];

                segments.Add(lineEnd);
            }

            return segments;
        }
    }

    static public class GCSpline
    {
        public static List<Point3D> GeneratePoints(double[] start, Point first, Point second, double[] end, double arcResolution, bool isRelative = false)
        {
            Point bez_target = new Point(start[0], start[1]);
            List<Point3D> segments = new List<Point3D>();

            double t = 0d, step = 0.1d;

            while (t < 1d)
            {
                // First try to reduce the step in order to make it sufficiently
                // close to a linear interpolation.
                bool did_reduce = false;
                double new_t = t + step;

                if (new_t > 1d)
                    new_t = 1d;

                double new_pos0 = eval_bezier(start[0], first.X, second.X, end[0], new_t),
                       new_pos1 = eval_bezier(start[1], first.Y, second.Y, end[1], new_t);

                if (arcResolution > 1d) // TODO: fix!
                    arcResolution = 0.002d;

                while (new_t - t >= arcResolution)
                {

                    //            if (new_t - t < (BEZIER_MIN_STEP))
                    //                break;

                    double candidate_t = 0.5f * (t + new_t),
                           candidate_pos0 = eval_bezier(start[0], first.X, second.X, end[0], candidate_t),
                           candidate_pos1 = eval_bezier(start[1], first.Y, second.Y, end[1], candidate_t),
                           interp_pos0 = 0.5f * (bez_target.X + new_pos0),
                           interp_pos1 = 0.5f * (bez_target.Y + new_pos1);

                    if (dist1(candidate_pos0, candidate_pos1, interp_pos0, interp_pos1) <= (.1d))
                        break;

                    new_t = candidate_t;
                    new_pos0 = candidate_pos0;
                    new_pos1 = candidate_pos1;
                    did_reduce = true;
                }

                if (!did_reduce) while (new_t - t >= (.002d))
                    {

                        double candidate_t = t + 2d * (new_t - t);

                        if (candidate_t >= 1.0f)
                            break;

                        double candidate_pos0 = eval_bezier(start[0], first.X, second.X, end[0], candidate_t),
                               candidate_pos1 = eval_bezier(start[1], first.Y, second.Y, end[1], candidate_t),
                               interp_pos0 = 0.5f * (bez_target.X + candidate_pos0),
                               interp_pos1 = 0.5f * (bez_target.Y + candidate_pos1);

                        if (dist1(new_pos0, new_pos1, interp_pos0, interp_pos1) > (.1d))
                            break;

                        new_t = candidate_t;
                        new_pos0 = candidate_pos0;
                        new_pos1 = candidate_pos1;
                    }

                step = new_t - t;
                t = new_t;

                bez_target.X = new_pos0;
                bez_target.Y = new_pos1;

                segments.Add(new Point3D(bez_target.X, bez_target.Y, end[2]));
            }

            return segments;
        }

        private static double interp(double a, double b, double t)
        {
            return (1d - t) * a + t * b;
        }

        private static double eval_bezier(double a, double b, double c, double d, double t)
        {
            double iab = interp(a, b, t),
                   ibc = interp(b, c, t),
                   icd = interp(c, d, t),
                   iabc = interp(iab, ibc, t),
                   ibcd = interp(ibc, icd, t);

            return interp(iabc, ibcd, t);
        }

        /**
         * We approximate Euclidean distance with the sum of the coordinates
         * offset (so-called "norm 1"), which is quicker to compute.
         */
        private static double dist1(double x1, double y1, double x2, double y2)
        {
            return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
        }
    }


    public class GCCubicSpline : GCAxisCommand3
    {
        public GCCubicSpline()
        { }

        public GCCubicSpline(Commands cmd, uint lnr, double[] xyz_values, AxisFlags axisFlags, double[] ijpq_values) : base(cmd, lnr, xyz_values, axisFlags)
        {
            Array.Copy(ijpq_values, IJPQKvalues, 4);
        }

        [XmlIgnore]
        public double[] IJPQKvalues { get; set; } = new double[4];
        public double I { get { return IJPQKvalues[0]; } set { IJPQKvalues[0] = value; } }
        public double J { get { return IJPQKvalues[1]; } set { IJPQKvalues[1] = value; } }
        public double P { get { return IJPQKvalues[2]; } set { IJPQKvalues[2] = value; } }
        public double Q { get { return IJPQKvalues[3]; } set { IJPQKvalues[3] = value; } }

        public new string ToString()
        {
            string s = base.ToString();

            s += "I" + I.ToInvariantString() + "J" + J.ToInvariantString() + "P" + P.ToInvariantString() + "Q" + Q.ToInvariantString();

            return s;
        }

        public GcodeBoundingBox GetBoundingBox(GCPlane plane, double[] start, bool isRelative = false)
        {
            GcodeBoundingBox bbox = new GcodeBoundingBox();

            List<Point3D> points = GeneratePoints(start, 0.01d, isRelative);

            bbox.AddPoint(plane, start[0], start[1], Z);

            foreach (Point3D p in points)
                bbox.AddPoint(plane, p.X, p.Y, Z);

            bbox.Conclude();

            return bbox;
        }

        public List<Point3D> GeneratePoints(double[] start, double arcResolution, bool isRelative = false)
        {
            return GCSpline.GeneratePoints(start, new Point(start[0] + I, start[1] + J), new Point(X + P, Y + Q), Values, arcResolution);
        }
    }

    public class GCQuadraticSpline : GCAxisCommand3
    {
        public GCQuadraticSpline()
        { }

        public GCQuadraticSpline(Commands cmd, uint lnr, double[] xyz_values, AxisFlags axisFlags, double[] ij_values) : base(cmd, lnr, xyz_values, axisFlags)
        {
            Array.Copy(ij_values, IJvalues, 2);
        }

        [XmlIgnore]
        public double[] IJvalues { get; set; } = new double[2];
        public double I { get { return IJvalues[0]; } set { IJvalues[0] = value; } }
        public double J { get { return IJvalues[1]; } set { IJvalues[1] = value; } }

        public new string ToString()
        {
            string s = base.ToString();

            s += "I" + I.ToInvariantString() + "J" + J.ToInvariantString();

            return s;
        }

        public GcodeBoundingBox GetBoundingBox(GCPlane plane, double[] start, bool isRelative = false)
        {
            GcodeBoundingBox bbox = new GcodeBoundingBox();

            List<Point3D> points = GeneratePoints(start, 0.01d, isRelative);

            bbox.AddPoint(plane, start[0], start[1], Z);

            foreach (Point3D p in points)
                bbox.AddPoint(plane, p.X, p.Y, Z);
 
            bbox.Conclude();

            return bbox;
        }

        public List<Point3D> GeneratePoints(double[] start, double arcResolution, bool isRelative = false)
        {
            Point first = new Point(start[0] + (I * 2d) / 3d, start[1] + (J * 2d) / 3d);
            Point second = new Point(X + ((start[0] + I - X) *2d / 3d), Y + ((start[1] + J - Y) * 2d / 3d));

            return GCSpline.GeneratePoints(start, first, second, Values, arcResolution);
        }
    }

    public class GCCannedDrill : GCAxisCommand3
    {
        public GCCannedDrill()
        { }

        // G81,G85
        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
            P = Q = 0d;
        }

        // G82,G86,G89
        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l, double p) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
            P = p;
            Q = 0d;
        }

        // G73,G83 // P always 0 for these
        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l, double p, double q) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
            P = p;
            Q = q;
        }

        public uint L { get; set; }
        public double P { get; set; }
        public double Q { get; set; }
        public double R { get; set; }

        public new string ToString()
        {
            return base.ToString() +
                    (double.IsNaN(R) ? "" : "R" + R.ToInvariantString()) +
                     (L <= 1 ? "" : "L" + L.ToString()) +
                      (P == 0d ? "" : "P" + P.ToInvariantString()) +
                       (Q == 0d ? "" : "Q" + Q.ToInvariantString());
        }
    }

    public class GCSyncMotion : GCAxisCommand3
    {
        public GCSyncMotion()
        { }

        public GCSyncMotion(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double k) : base(command, lnr, values, axisFlags)
        {
            K = k;
        }

        public double K { get; set; }

        public new string ToString()
        {
            return base.ToString() + "K" + K.ToInvariantString();
        }
    }

    public class GCThreadingMotion : AxisCommand3IJK
    {
        public GCThreadingMotion()
        { }

        public GCThreadingMotion(Commands command, uint lnr, double p, double[] values, AxisFlags axisFlags, double[] ijkValues, IJKFlags ijkFlags, double[] optvals, ThreadingFlags threadingFlags) : base(command, lnr, values, axisFlags, ijkValues, ijkFlags)
        {
            P = p;

            ThreadingFlags = threadingFlags;
            Array.Copy(optvals, OptionValues, 5);

            switch((uint)L)
            {
                case 1:
                    ThreadTaper = ThreadTaper.Entry;
                    break;
                case 2:
                    ThreadTaper = ThreadTaper.Exit;
                    break;
                case 3:
                    ThreadTaper = ThreadTaper.Both;
                    break;
                default:
                    ThreadTaper = ThreadTaper.None;
                    break;
            }

            if (ThreadTaper == ThreadTaper.None)
                E = 0d;

            if (!ThreadingFlags.HasFlag(ThreadingFlags.R))
                R = 1d;
        }

        [XmlIgnore]
        public double[] OptionValues { get; set; } = new double[5];
        public ThreadingFlags ThreadingFlags { get; set; }
        public ThreadTaper ThreadTaper  { get; set; }
        public double P { get; set; }
        public double R { get { return OptionValues[0]; } set { OptionValues[0] = value; } }
        public double Q { get { return OptionValues[1]; } set { OptionValues[1] = value; } }
        public double H { get { return OptionValues[2]; } set { OptionValues[2] = value; } }
        public double E { get { return OptionValues[3]; } set { OptionValues[3] = value; } }
        public double L { get { return OptionValues[4]; } set { OptionValues[4] = value; } }

        [XmlIgnore]
        public double Pitch { get { return P; } }
        [XmlIgnore]
        public double CutDirection { get { return I > 0d ? 1d : -1d ; } }
        [XmlIgnore]
        public double Peak { get { return Math.Abs(I); } }
        [XmlIgnore]
        public double InitialDepth { get { return J; } }
        [XmlIgnore]
        public double Depth { get { return K; } }
        [XmlIgnore]
        public double DepthDegression { get { return R; } }
        [XmlIgnore]
        public double InfeedAngle { get { return Q; } }
        [XmlIgnore]
        public uint SpringPasses { get { return (uint)H; } }
        [XmlIgnore]
        public double TaperLength { get { return E; } }

        public double CalculateDOC(uint pass)
        {
            return InitialDepth * Math.Pow((double)pass, 1d / R);
        }

        public new string ToString()
        {
            string options = string.Empty;

            foreach (int i in ThreadingFlags.ToIndices())
                options += ((ThreadingFlags)(1 << i)).ToString() + OptionValues[i].ToInvariantString();

            return base.ToString() + string.Format("P{0}K{1}", P.ToInvariantString(), K.ToInvariantString()) + options;
        }
    }

    public class GCCoordinateSystem : GCAxisCommand6
    {
        public GCCoordinateSystem()
        { }

        public GCCoordinateSystem(Commands cmd, uint lnr, uint p, double[] values, AxisFlags axisFlags) : base(cmd, lnr, values, axisFlags)
        {
            L = 0;
            P = p;
            R = double.NaN;
        }
        public GCCoordinateSystem(Commands cmd, uint lnr, uint l, uint p, double[] values, AxisFlags axisFlags) : base(cmd, lnr, values, axisFlags)
        {
            L = l;
            P = p;
            R = double.NaN;
        }

        public uint L { get; set; }
        public uint P { get; set; }
        public double R { get; set; }

        public string Code { get { return "Current,G54,G55,G56,G57,G58,G59,G59.1,G59.2,G59.3,G92,G28,G30".Split(',').ToArray()[P]; } }

        public new string ToString()
        {
            return base.ToString() + (L > 0 ? "L" + L : "") + "P" + P + (double.IsNaN(R) ? "" : "R" + R.ToInvariantString());
        }
    }

    public class GCToolOffset : GCodeToken
    {
        public GCToolOffset()
        { }

        public GCToolOffset(Commands cmd, uint lnr, uint h) : base(cmd, lnr)
        {
            H = h;
        }

        public uint H { get; set; }

        public new string ToString()
        {
            return base.ToString() + "H" + H.ToString();
        }
    }

    public class GCToolOffsets : GCAxisCommand3
    {
        public GCToolOffsets()
        { }

        public GCToolOffsets(Commands cmd, uint lnr, double[] values, AxisFlags axisFlags) : base(cmd, lnr, values, axisFlags)
        {
        }

        public new string ToString()
        {
            return base.ToString();
        }
    }

    public class GCToolTable : GCAxisCommand3
    {
        public GCToolTable()
        { }

        public GCToolTable(Commands cmd, uint lnr, uint l, uint p, double r, double[] values, AxisFlags axisFlags) : base(cmd, lnr, values, axisFlags)
        {
            P = p;
            R = r;
            L = l;
        }

        public uint L { get; set; }
        public uint P { get; set; }
        public double R { get; set; }

        public new string ToString()
        {
            return base.ToString() + string.Format("L{0}P{1}", L, P) + (double.IsNaN(R) ? "" : "R" + R.ToInvariantString());
        }
    }

    public class GCScaling : GCAxisCommand6
    {
        public GCScaling()
        { }

        public GCScaling(Commands command, uint lnr, double[] values, AxisFlags axisFlags) : base(command, lnr, values, axisFlags)
        {
        }

        public new string ToString()
        {
            return Command == Commands.G50 ? Command.ToString() : base.ToString();
        }
    }

    public class GCToolSelect : GCodeToken
    {
        public GCToolSelect()
        { }

        public GCToolSelect(Commands command, uint lnr, int tool) : base(command, lnr)
        {
            Tool = tool;
        }

        public int Tool { get; set; }

        public new string ToString()
        {
            return "T" + Tool.ToString();
        }
    }

    public class GCFeedrate : GCodeToken
    {
        public GCFeedrate()
        { }

        public GCFeedrate(Commands command, uint lnr, double feedrate) : base(command, lnr)
        {
            Feedrate = feedrate;
        }

        public GCFeedrate(uint lnr, double feedrate) : base(Commands.Feedrate, lnr)
        {
            Feedrate = feedrate;
        }

        public double Feedrate { get; set; }

        public new string ToString()
        {
            return "F" + Feedrate.ToInvariantString();
        }
    }

    public class GCSpindleRPM : GCodeToken
    {
        public GCSpindleRPM()
        { }

        public GCSpindleRPM(Commands command, uint lnr, double spindleRPM) : base(command, lnr)
        {
            SpindleRPM = spindleRPM;
        }

        public double SpindleRPM { get; set; }

        public new string ToString()
        {
            return "S" + SpindleRPM.ToInvariantString();
        }
    }

    public class GCSpindleState : GCodeToken
    {
        public GCSpindleState()
        { }

        public GCSpindleState(uint lnr, SpindleState spindleState)
        {
            LineNumber = lnr;
            Command = spindleState == SpindleState.Off ? Commands.M5 : (spindleState == SpindleState.CW ? Commands.M3 : Commands.M4);
            SpindleState = spindleState;
        }

        public SpindleState SpindleState { get; set; }

        public new string ToString()
        {
            return Command.ToString();
        }
    }

    public class GCCoolantState : GCodeToken
    {
        public GCCoolantState()
        { }

        public GCCoolantState(uint lnr, CoolantState coolantState)
        {
            LineNumber = lnr;
            Command = coolantState.HasFlag(CoolantState.Flood) ? Commands.M8 : (coolantState.HasFlag(CoolantState.Mist) ? Commands.M7 : Commands.M9);
            CoolantState = coolantState;

        }

        public CoolantState CoolantState { get; set; }

        public new string ToString()
        {
            return Command.ToString();
        }
    }


    public class GCPlane : GCodeToken
    {
        public GCPlane()
        { }

        public GCPlane(Commands cmd, uint lnr) : base(cmd, lnr)
        {
        }

        public Plane Plane { get { return Command == Commands.G17 ? Plane.XY : (Command == Commands.G18 ? Plane.XZ : Plane.YZ); } }
        public int Axis0 { get { return Plane == Plane.XY ? 0 : (Plane == Plane.XZ ? 2 : 1); } }
        public int Axis1 { get { return Plane == Plane.XY ? 1 : (Plane == Plane.XZ ? 0 : 2); } }
        public int AxisLinear { get { return Plane == Plane.XY ? 2 : (Plane == Plane.XZ ? 1 : 0); } }

        public new string ToString()
        {
            return base.ToString();
        }
    }

    public class GCDistanceMode : GCodeToken
    {
        public GCDistanceMode()
        { }

        public GCDistanceMode(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public GCDistanceMode(uint lnr, DistanceMode distanceMode) : base(distanceMode == DistanceMode.Absolute ? Commands.G90 : Commands.G91, lnr)
        {
        }

        public DistanceMode DistanceMode { get { return Command == Commands.G90 ? DistanceMode.Absolute : DistanceMode.Incremental; } }
    }

    public class GCFeedRateMode : GCodeToken
    {
        public GCFeedRateMode()
        { }

        public GCFeedRateMode(Commands command, uint lnr) : base(command, lnr)
        {
        }

        public GCFeedRateMode(uint lnr, FeedRateMode feedRateMode) : base(feedRateMode == FeedRateMode.UnitsPerMin ? Commands.G94 : (feedRateMode == FeedRateMode.InverseTime ? Commands.G93 : Commands.G95), lnr)
        {
        }

        public FeedRateMode FeedRateMode { get { return Command == Commands.G94 ? FeedRateMode.UnitsPerMin : (Command == Commands.G93 ? FeedRateMode.InverseTime : FeedRateMode.UnitsPerRev); } }
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

    public class GCDwell : GCodeToken
    {
        public GCDwell()
        { }

        public GCDwell(uint lnr, double delay) : base(Commands.G4, lnr)
        {
            Delay = delay;
        }

        public double Delay { get; set; }

        public new string ToString()
        {
            return base.ToString() + "P" + Delay.ToInvariantString();
        }
    }

    public class GCComment : GCodeToken
    {
        public GCComment()
        { }

        public GCComment(Commands command, uint lnr, string comment) : base(command, lnr)
        {
            Comment = comment.Length == 0 || comment[0] == '(' || comment[0] == ';' ? comment : "(" + comment + ")";
        }

        public string Comment { get; set; }

        public new string ToString()
        {
            return Comment;
        }
    }

    public class GCUserMCommand : GCodeToken
    {
        public GCUserMCommand()
        { }

        public GCUserMCommand(uint lnr, uint mCode, double p, double q) : base(Commands.UserMCommand, lnr)
        {
            M = mCode;
            P = p;
            Q = q;
        }

        public uint M { get; set; }
        public double P { get; set; }
        public double Q { get; set; }

        public new string ToString()
        {
            return "M" + M + (double.IsNaN(P) ? "" : "P" + P.ToInvariantString()) + (double.IsNaN(Q) ? "" : "Q" + Q.ToInvariantString());
        }
    }

    public class GCDigitalOutput : GCodeToken
    {
        public GCDigitalOutput()
        { }

        public GCDigitalOutput(Commands command, uint lnr, uint p) : base(command, lnr)
        {
            P = p;
        }

        public uint P { get; set; }

        public new string ToString()
        {
            return base.ToString() + "P" + P;
        }
    }

    public class GCWaitOnInput : GCodeToken
    {
        public GCWaitOnInput()
        { }

        public GCWaitOnInput(uint lnr, int p, int e, uint l, double q) : base(Commands.M67, lnr)
        {
            P = p;
            E = e;
            L = l;
            Q = q;
        }

        public int P { get; set; }
        public int E { get; set; }
        public uint L { get; set; }
        public double Q { get; set; }
        public InputWaitMode InputWaitMode { get { return (InputWaitMode)L; } }

        public new string ToString()
        {
            return base.ToString() + (P == -1 ? "" :"P" + P) + (E == -1 ? "" : "E" + E) + "L" + L + "Q" + Q.ToInvariantString();
        }
    }

    public class GCAnalogOutput : GCodeToken
    {
        public GCAnalogOutput()
        { }

        public GCAnalogOutput(Commands command, uint lnr, uint e, double q) : base(command, lnr)
        {
            E = e;
            Q = q;
        }

        public uint E { get; set; }
        public double Q { get; set; }

        public new string ToString()
        {
            return base.ToString() + "E" + E + "Q" + Q.ToInvariantString();
        }
    }

    #endregion

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
