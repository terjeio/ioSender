/*
 * GCodeParser.cs - part of CNC Controls library
 *
 * v0.28 / 2020-10-03 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2020, Io Engineering (Terje Io)
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

        // Modal Group G1: Motion modes
        private enum MotionMode
        {
            Seek = 0,                    // G0 (Default: Must be zero)
            Linear = 1,                  // G1 (Do not alter value)
            CwArc = 2,                   // G2 (Do not alter value)
            CcwArc = 3,                  // G3 (Do not alter value)
            CubicSpline = 5,             // G5 (Do not alter value)
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

        // Modal Group G8: Tool length offset

        const string ignore = "$!~?;";
        const string collect = "0123456789.+- ";

        #endregion

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        private bool motionModeChanged = false;
        private GCValues gcValues = new GCValues();
        private GCodeToken last_token = new GCodeToken();
        private MotionMode motionMode;

        private uint coordSystem;
        private int demarcCount;
        private double[] splinePQ = new double[2];
        private double zorg = 0d;

        // The following variables are only set in tandem with the modal group that triggers their use: 
        private Commands cmdNonModal = Commands.Undefined, cmdProgramFlow = Commands.Undefined, cmdPlane = Commands.Undefined, cmdOverride = Commands.Undefined, cmdDistMode = Commands.Undefined;
        private Commands cmdLatheMode = Commands.Undefined, cmdRetractMode = Commands.Undefined, cmdSpindleRpmMode = Commands.Undefined, cmdFeedrateMode = Commands.Undefined;
        private Commands cmdUnits = Commands.Undefined, cmdPathMode = Commands.Undefined;

        public GCodeParser()
        {
            Reset();
        }

        public static CommandIgnoreState IgnoreM6 { get; set; } = CommandIgnoreState.Prompt;
        public static CommandIgnoreState IgnoreM7 { get; set; } = CommandIgnoreState.No;
        public static CommandIgnoreState IgnoreM8 { get; set; } = CommandIgnoreState.No;

        public Dialect Dialect { get; set; } = Dialect.GrblHAL;
        public int ToolChanges { get; private set; }
        public bool ProgramEnd { get; private set; }
        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();

        public new void Reset()
        {
            base.Reset();

            // TODO: set defaults from grbl parser state? 
            gcValues.Clear();
            Tokens.Clear();
            ProgramEnd = false;
            motionMode = MotionMode.Seek;
            coordSystem = 0;
            demarcCount = 0;
            ToolChanges = 0;
            IsScaled = motionModeChanged = false;
            zorg = 0d;
        }

        private string rewrite_block(string remove, List<string> gcodes)
        {
            string block = string.Empty;

            foreach (string gcode in gcodes)
            {
                if (gcode != remove)
                    block += gcode;
            }

            return block == string.Empty ? "(line removed)" : block;
        }

        private bool VerifyIgnore(string code, CommandIgnoreState state)
        {
            bool strip = state == CommandIgnoreState.Strip;

            if (!strip && state != CommandIgnoreState.No)
                strip = MessageBox.Show(string.Format("{0} command found, strip?", code), "Strip command", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

            return strip;
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

            int userMCode = 0;
            bool isDwell = false, isScaling = false, inMessage = false;
            string gcode = string.Empty, comment = string.Empty, block = line;
            double value;
            AxisCommand axisCommand = AxisCommand.None;

            List<string> gcodes = new List<string>();

            isComment = false;

            if (block.Length == 0 || block[0] == ';')
                return block.Length != 0;

            if (block.IndexOf(';') > 0)
                block = block.Substring(0, block.IndexOf(';'));

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

            block += '\r';
            motionModeChanged = false;

            foreach (char c in block)
            {
                if (!collect.Contains(c) && !(inMessage &= (c != ')')))
                {
                    if (gcode.Length > 0)
                    {
                        gcodes.Add(gcode[0] == '(' ? gcode + ')' : gcode);
                        gcode = string.Empty;
                    }
                    if (c != ')')
                    {
                        inMessage = c == '(';
                        gcode += inMessage ? c : char.ToUpperInvariant(c);
                    }
                }
                else if (c > ' ' || inMessage)
                    gcode += c;
            }

            isComment = gcodes.Count == 1 && gcodes[0].First() == '(' && !gcodes[0].StartsWith("(MSG");

            foreach (string code in gcodes)
            {
                wordFlag = 0;
                modalGroup = 0;

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
                        case 5:
                            if (iv == 5 && Dialect == Dialect.Grbl)
                                throw new GCodeException("Unsupported command");
                            if (axisCommand != AxisCommand.None && cmdNonModal != Commands.G53)
                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionModeChanged = motionMode != (MotionMode)iv;
                            motionMode = (MotionMode)iv;
                            break;

                        case 4:
                            isDwell = true;
                            modalGroup = ModalGroups.G0;
                            break;

                        case 10:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G10;
                            break;

                        case 28:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G28 + fv;
                            break;

                        case 30:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G30 + fv;
                            break;

                        case 92:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            cmdNonModal = Commands.G92 + fv;
                            break;

                        case 7:
                        case 8:
                            if (Dialect == Dialect.Grbl)
                                throw new GCodeException("Unsupported command");
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
                            break;

                        case 33:
                        case 76:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            zorg = gcValues.Z;
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)iv;
                            break;

                        case 38:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.MotionMode;
                            modalGroup = ModalGroups.G1;
                            motionMode = MotionMode.ProbeToward + fv;
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
                                throw new GCodeException("Unsupported command");
                            // NOTE: not NIST
                            if (iv == 51 && axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
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
                                throw new GCodeException("Unsupported command");
                            coordSystem = (uint)(iv + fv);
                            modalGroup = ModalGroups.G12;
                            break;

                        case 61:
                        case 64:
                            if (Dialect != Dialect.LinuxCNC && (iv != 61 || fv > 0))
                                throw new GCodeException("Unsupported command");
                            cmdPathMode = iv == 64 ? Commands.G64 : Commands.G61 + fv;
                            modalGroup = ModalGroups.G13;
                            break;

                        case 80:
                            //                            if (axisCommand != AxisCommand.None)
                            //                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.None;
                            motionMode = MotionMode.None;
                            break;

                        case 73:
                        case 81:
                        case 82:
                        case 83:
                        case 85:
                        case 86:
                        case 89:
                            if (Dialect == Dialect.Grbl)
                                throw new GCodeException("Unsupported command");
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionModeChanged = motionMode != (MotionMode)iv;
                            motionMode = (MotionMode)iv;
                            break;

                        case 84:
                        case 87:
                        case 88:
                            if (fv == 0) // test to stop compiler complaining 
                                throw new GCodeException("Unsupported command");
                            break;

                        case 90:
                            //if (Dialect != Dialect.LinuxCNC && fv == 1)
                            //    throw new GCodeException("Unsupported command");
                            cmdDistMode = Commands.G90 + fv;
                            modalGroup = fv == 0 ? ModalGroups.G3 : ModalGroups.G4;
                            break;

                        case 91:
                            cmdDistMode = Commands.G91 + fv;
                            modalGroup = fv == 0 ? ModalGroups.G3 : ModalGroups.G4;
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
                                throw new GCodeException("Unsupported command");
                            cmdRetractMode = Commands.G98 + (iv - 98);
                            modalGroup = ModalGroups.G10;
                            break;
                    }

                    if (modalGroup > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException("Modal group violation");
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (code[0] == 'M')
                {
                    #region M-code parsing

                    value = double.Parse(code.Remove(0, 1), CultureInfo.InvariantCulture);
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
                            if (VerifyIgnore(code, IgnoreM6))
                                line = rewrite_block(code, gcodes);
                            else
                                modalGroup = ModalGroups.M6;
                            break;

                        case 7:
                            if (VerifyIgnore(code, IgnoreM7))
                                line = rewrite_block(code, gcodes);
                            else
                            {
                                CoolantState = CoolantState.Mist;
                                modalGroup = ModalGroups.M8;
                            }
                            break;

                        case 8:
                            if (VerifyIgnore(code, IgnoreM8))
                                line = rewrite_block(code, gcodes);
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
                                throw new GCodeException("Unsupported command");
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
                        throw new GCodeException("Modal group violation");
                    }
                    else
                        modalGroups |= modalGroup;
                }
                else if (code[0] == '(' /* && code.Length > 5 && code.Substring(0, 5).ToUpperInvariant() == "(MSG,"*/)
                {
                    comment = code;
                }
                else if (code[0] != '(')
                {
                    #region Parse Word values

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
                                wordFlag = WordFlags.H;
                                break;

                            case 'I':
                                gcValues.I = value;
                                wordFlag = WordFlags.I;
                                ijkWords |= IJKFlags.I;
                                break;

                            case 'J':
                                gcValues.J = value;
                                wordFlag = WordFlags.J;
                                ijkWords |= IJKFlags.J;
                                break;

                            case 'K':
                                gcValues.K = value;
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
                                wordFlag = WordFlags.X;
                                axisWords |= GCode.AxisFlags.X;
                                gcValues.X = value;
                                break;

                            case 'Y':
                                wordFlag = WordFlags.Y;
                                axisWords |= GCode.AxisFlags.Y;
                                gcValues.Y = value;
                                break;

                            case 'Z':
                                wordFlag = WordFlags.Z;
                                axisWords |= GCode.AxisFlags.Z;
                                gcValues.Z = value;
                                break;

                            case 'A':
                                wordFlag = WordFlags.A;
                                axisWords |= GCode.AxisFlags.A;
                                gcValues.A = value;
                                break;

                            case 'B':
                                wordFlag = WordFlags.B;
                                axisWords |= GCode.AxisFlags.B;
                                gcValues.B = value;
                                break;

                            case 'C':
                                wordFlag = WordFlags.C;
                                axisWords |= GCode.AxisFlags.C;
                                gcValues.C = value;
                                break;

                            default:
                                throw new GCodeException("Command word not recognized");
                        }
                    }
                    catch (Exception e)
                    {
                        throw new GCodeException("Invalid GCode", e);
                    }
                    #endregion
                }

                if (wordFlag > 0 && wordFlags.HasFlag(wordFlag))
                {
                    throw new GCodeException("Command word repeated");
                }
                else
                    wordFlags |= wordFlag;
            }

            //
            // 0. Non-specific/common error-checks and miscellaneous setup
            //

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
                Tokens.Add(new GCodeToken(cmdFeedrateMode, gcValues.N));
            }

            //
            // 3. Set feed rate
            //
            if (wordFlags.HasFlag(WordFlags.F))
            {
                Tokens.Add(new GCFeedrate(Commands.Feedrate, gcValues.N, gcValues.F));
            }

            //
            // 4. Set spindle speed
            //

            // G96, G97
            if (modalGroups.HasFlag(ModalGroups.G14))
            {
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
                Tokens.Add(new GCToolSelect(Commands.ToolSelect, gcValues.N, gcValues.T));

                if (!quiet && ToolChanged != null && !ToolChanged(gcValues.T))
                    MessageBox.Show(string.Format("Tool {0} not associated with a profile!", gcValues.T.ToString()), "GCode parser", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                                    throw new GCodeException("Cannot use both P- and E-word with M66");
                                if (!(wordFlags.HasFlag(WordFlags.P) || wordFlags.HasFlag(WordFlags.E)))
                                    throw new GCodeException("P- or E-word missing for M66");
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
                if ((modalGroups.HasFlag(ModalGroups.G0) && isDwell))
                {
                    isDwell = false;
                    if (wordFlags.HasFlag(WordFlags.P))
                        Tokens.Add(new GCDwell(gcValues.N, gcValues.P));
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
                                var offset = new GCToolOffset(Commands.G43_2, gcValues.N, (uint)gcValues.H);
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
                    Tokens.Add(new GCodeToken(Commands.G54 + (int)coordSystem - 54, gcValues.N));
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
                    var mode = new GCIJKMode(cmdDistMode, gcValues.N);
                    IJKMode = mode.IJKMode;
                    Tokens.Add(mode);
                }

                //
                // 18. Set retract mode
                //

                // G98, G99
                if (modalGroups.HasFlag(ModalGroups.G10))
                {
                    Tokens.Add(new GCodeToken(cmdRetractMode, gcValues.N));
                }

                //
                // 19. Go to predefined position, Set G10, or Set axis offsets
                //

                // G10, G28, G28.1, G30, G30.1, G92, G92.1, G92.2, G92.3
                if (modalGroups.HasFlag(ModalGroups.G0))
                {
                    switch (cmdNonModal)
                    {
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
                            Tokens.Add(new GCLinearMotion(cmdNonModal, gcValues.N, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G53:
                            if (motionMode == MotionMode.Seek || motionMode == MotionMode.Linear)
                            {
                                Tokens.Add(new GCAbsLinearMotion(cmdNonModal, motionMode == MotionMode.Seek ? Commands.G0 : Commands.G1, gcValues.N, gcValues.XYZ, axisWords));
                            } else
                                throw new GCodeException("G0 or G1 not active");
                            break;

                        case Commands.G28_1:
                            Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 11, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G30_1:
                            Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 12, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G92:
                            Tokens.Add(new GCCoordinateSystem(cmdNonModal, gcValues.N, 10, gcValues.XYZ, axisWords));
                            break;

                        case Commands.G92_1:
                        case Commands.G92_2:
                        case Commands.G92_3:
                            Tokens.Add(new GCodeToken(cmdNonModal, gcValues.N));
                            break;
                    }

                    axisWords = 0;
                }
            }

            //
            // 20. Motion modes
            //

            // Cancel canned cycle mode: G80
            if (modalGroups.HasFlag(ModalGroups.G1) && axisCommand == AxisCommand.None)
            {
                motionMode = MotionMode.None;
                Tokens.Add(new GCodeToken(Commands.G80, gcValues.N));
            }

            if (motionMode != MotionMode.None && (axisWords != 0 || ijkWords != 0 || modalGroup == ModalGroups.G1))
            {
                switch (motionMode)
                {
                    case MotionMode.Seek:
                        Tokens.Add(new GCLinearMotion(Commands.G0, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.Linear:
                        Tokens.Add(new GCLinearMotion(Commands.G1, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.CwArc:
                    case MotionMode.CcwArc:
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
                        Tokens.Add(new GCArc(motionMode == MotionMode.CwArc ? Commands.G2 : Commands.G3, gcValues.N, gcValues.XYZ, axisWords, gcValues.IJK, ijkWords, gcValues.R, IJKMode));
                        break;

                    case MotionMode.CubicSpline:
                        if (Plane.Plane != GCode.Plane.XY)
                            throw new GCodeException("Plane not XY");
                        if (!(wordFlags.HasFlag(WordFlags.P) && wordFlags.HasFlag(WordFlags.Q)))
                            throw new GCodeException("P and/or Q word missing");
                        if (motionModeChanged && !(wordFlags.HasFlag(WordFlags.I) && wordFlags.HasFlag(WordFlags.J)))
                            throw new GCodeException("I and/or J word missing");
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
                        Tokens.Add(new GCSpline(Commands.G5, gcValues.N, gcValues.XYZ, axisWords, new double[] { gcValues.IJK[0], gcValues.IJK[1], splinePQ[0], splinePQ[1] }));
                        break;

                    case MotionMode.SpindleSynchronized:
                        Tokens.Add(new GCSyncMotion(Commands.G33, gcValues.N, gcValues.XYZ, axisWords, gcValues.K));
                        break;

                    case MotionMode.ProbeToward:
                        Tokens.Add(new GCLinearMotion(Commands.G38_2, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.ProbeTowardNoError:
                        Tokens.Add(new GCLinearMotion(Commands.G38_3, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.ProbeAway:
                        Tokens.Add(new GCLinearMotion(Commands.G38_4, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.ProbeAwayNoError:
                        Tokens.Add(new GCLinearMotion(Commands.G38_5, gcValues.N, gcValues.XYZ, axisWords));
                        break;

                    case MotionMode.DrillChipBreak:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            if (!wordFlags.HasFlag(WordFlags.R))
                                throw new GCodeException("R word missing");
                            if (!wordFlags.HasFlag(WordFlags.Q) || gcValues.Q <= 0d)
                                throw new GCodeException("Q word missing or out of range");
                            Tokens.Add(new GCCannedDrill(Commands.G73, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats, 0d, gcValues.Q));
                        }
                        break;

                    case MotionMode.Threading:
                        {
                            // TODO: add check for mandatory values + sanity
                            ThreadingFlags optFlags = ThreadingFlags.None;
                            double[] optValues = new double[5];

                            if(Plane.Plane != GCode.Plane.XZ)
                                throw new GCodeException("Plane not ZX");

                            if (axisWords != AxisFlags.Z)
                                throw new GCodeException("Axisword(s) other than Z found");

                            if (!wordFlags.HasFlag(WordFlags.P))
                                throw new GCodeException("P word missing");
                            else if(gcValues.P < 0d)
                                throw new GCodeException("P word negative");

                            if (!wordFlags.HasFlag(WordFlags.I))
                                throw new GCodeException("I word missing");

                            if (!wordFlags.HasFlag(WordFlags.J))
                                throw new GCodeException("J word missing");
                            else if (gcValues.J < 0d)
                                throw new GCodeException("J word negative");

                            if (!wordFlags.HasFlag(WordFlags.K))
                                throw new GCodeException("K word missing");
                            else if (gcValues.K < 0d)
                                throw new GCodeException("K word negative");

                            if(gcValues.K <= gcValues.J)
                                throw new GCodeException("K word must be greater than J word");

                            if (wordFlags.HasFlag(WordFlags.R))
                            {
                                if(gcValues.R < 1d)
                                    throw new GCodeException("R word less than 1");
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
                                    throw new GCodeException("H word is negative");
                                optValues[2] = gcValues.H;
                            }
                            if (wordFlags.HasFlag(WordFlags.E))
                            {
                                if (gcValues.E  > Math.Abs(gcValues.Z - zorg) / 2d)
                                    throw new GCodeException("E word greater than half the drive line length");
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

                            Tokens.Add(new GCThreadingMotion(Commands.G76, gcValues.N, gcValues.P, gcValues.XYZ, axisWords, gcValues.IJK, IJKFlags.All, optValues, optFlags));
                        }
                        break;

                    case MotionMode.CannedCycle81:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            Tokens.Add(new GCCannedDrill(Commands.G81, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats));
                        }
                        break;

                    case MotionMode.CannedCycle82:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G82, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats, dwell));
                        }
                        break;

                    case MotionMode.CannedCycle83:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            if (!wordFlags.HasFlag(WordFlags.Q) || gcValues.Q <= 0d)
                                throw new GCodeException("Q word missing or out of range");
                            Tokens.Add(new GCCannedDrill(Commands.G83, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats, dwell, gcValues.Q));
                        }
                        break;

                    case MotionMode.CannedCycle85:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            Tokens.Add(new GCCannedDrill(Commands.G85, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats));
                        }
                        break;

                    case MotionMode.CannedCycle86:
                        {
                            // error if spindle not running
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G86, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats, dwell));
                        }
                        break;

                    case MotionMode.CannedCycle89:
                        {
                            uint repeats = wordFlags.HasFlag(WordFlags.L) ? (uint)gcValues.L : 1;
                            double dwell = wordFlags.HasFlag(WordFlags.P) ? gcValues.P : 0d;
                            Tokens.Add(new GCCannedDrill(Commands.G86, gcValues.N, gcValues.XYZ, axisWords, gcValues.R, repeats, dwell));
                        }
                        break;
                }
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
                        typeof(GCSpline),
                        typeof(GCSyncMotion),
                        typeof(GCThreadingMotion),
                        typeof(GCCannedDrill),
                        typeof(GCPlane),
                        typeof(GCDistanceMode),
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
            catch (IOException)
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

        public static List<string> TokensToGCode(List<GCodeToken> tokens)
        {
            List<string> gc = new List<string>();

            string block = string.Empty;
            uint line = 0;

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
                        block += (token as GCLinearMotion).ToString();
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        block += (token as GCArc).ToString();
                        break;

                    case Commands.G4:
                        block += (token as GCDwell).ToString();
                        break;

                    case Commands.G51:
                        block += (token as GCScaling).ToString();
                        break;

                    case Commands.G73:
                    case Commands.G81:
                    case Commands.G82:
                    case Commands.G83:
                    case Commands.G85:
                    case Commands.G86:
                    case Commands.G89:
                        block += (token as GCCannedDrill).ToString();
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

        public GCArc()
        { }

        public GCArc(Commands cmd, uint lnr, double[] xyz_values, AxisFlags axisFlags, double[] ijk_values, IJKFlags ijkFlags, double r, IJKMode ijkMode) : base(cmd, lnr, xyz_values, axisFlags)
        {
            Array.Copy(ijk_values, IJKvalues, 3);
            Array.Copy(Values, end, 3);

            IJKMode = ijkMode;
            if((IjkFlags = ijkFlags) == IJKFlags.None)
                R = r;
        }

        public IJKFlags IjkFlags { get; set; }

        [XmlIgnore]
        public double[] IJKvalues { get; set; } = new double[3];
        public double I { get { return IJKvalues[0]; } set { IJKvalues[0] = value; } }
        public double J { get { return IJKvalues[1]; } set { IJKvalues[1] = value; } }
        public double K { get { return IJKvalues[2]; } set { IJKvalues[2] = value; } }
        public double R { get; set; }

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

            return s;
        }

        public double[] GetCenter(GCPlane plane, double[] start, bool isRelative = false)
        {
            if (!center_ok)
            {
                if (isRelative)
                {
                    for (int i = 0; i < 3; i++)
                        end[i] += start[i];
                }

                if (IsRadiusMode)
                    center = convertRToCenter(plane, start);
                else
                    center = updateCenterWithCommand(plane, start);

                center_ok = true;
            }

            return center;
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

            if (startAngle == endAngle)
            {
                bbox.AddPoint(plane, center[0] - R, center[1] - R, z1);
                bbox.AddPoint(plane, center[0] + R, center[1] + R, z2);
            }
            else
            {
                double sweep;
                double x1 = Math.Min(start[plane.Axis0], end[plane.Axis0]);
                double y1 = Math.Min(start[plane.Axis1], end[plane.Axis1]);
                double x2 = Math.Max(start[plane.Axis0], end[plane.Axis0]);
                double y2 = Math.Max(start[plane.Axis1], end[plane.Axis1]);
                int q = 4;

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

                bbox.AddPoint(plane, x1, y1, z1);
                bbox.AddPoint(plane, x2, y2, z2);

                double da = Math.PI * 2d;

                while ((da - Math.PI / 2d) >= startAngle)
                {
                    q--;
                    da -= Math.PI / 2d;
                }

                sweep -= da - startAngle;

                while (sweep >= 0d)
                {
                    switch (q)
                    {
                        case 0:
                            bbox.AddPoint(plane, center[0] + R, y1, z1);
                            bbox.AddPoint(plane, center[0] + R, y2, z2);
                            q = IsClocwise ? 3 : 1;
                            break;

                        case 1:
                            bbox.AddPoint(plane, x1, center[1] + R, z1);
                            bbox.AddPoint(plane, x2, center[1] + R, z2);
                            q = IsClocwise ? 0 : 2;
                            break;

                        case 2:
                            bbox.AddPoint(plane, center[0] - R, y1, z1);
                            bbox.AddPoint(plane, center[0] - R, y2, z2);
                            q = IsClocwise ? 1 : 3;
                            break;


                        case 3:
                            bbox.AddPoint(plane, x1, center[1] - R, z1);
                            bbox.AddPoint(plane, x2, center[1] - R, z2);
                            q = IsClocwise ? 2 : 0;
                            break;
                    }
                    sweep -= Math.PI / 2d;
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

            if(R == 0d)
                R = Math.Sqrt(IJKvalues[plane.Axis0] * IJKvalues[plane.Axis0] + IJKvalues[plane.Axis1] * IJKvalues[plane.Axis1]);

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
                Console.Write("Error computing arc radius.");
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

            // Calculate angles from center.
            double startAngle = GetStartAngle(plane, start, isRelative);
            double endAngle = GetEndAngle(plane, start, isRelative);

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

            int numPoints;

            if (arcResolution > 1d)
                numPoints = (int)Math.Max(1d, (sweep / (Math.PI * 18d / 180d)));
            else
                numPoints = (int)Math.Floor(Math.Abs(0.5d * sweep * R) / Math.Sqrt(arcResolution * (2.0f * R - arcResolution)));

            return generatePointsAlongArcBDring(plane, start, startAngle, sweep, numPoints);
        }

        /*
         * Generates the points along an arc including the start and end points.
         */
        private List<Point3D> generatePointsAlongArcBDring(GCPlane plane, double[] start, double startAngle, double sweep, int numPoints)
        {

            Point3D lineEnd = new Point3D();
            List<Point3D> segments = new List<Point3D>();
            double angle;
            double zIncrement = (end[plane.AxisLinear] - start[plane.AxisLinear]) / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                if (IsClocwise)
                    angle = (startAngle - i * sweep / numPoints);
                else
                    angle = (startAngle + i * sweep / numPoints);

                if (angle >= Math.PI * 2d)
                    angle = angle - Math.PI * 2d;

                start[plane.Axis0] = Math.Cos(angle) * R + center[0];
                start[plane.Axis1] = Math.Sin(angle) * R + center[1];

                lineEnd.X = start[0];
                lineEnd.Y = start[1];
                lineEnd.Z = start[2];

                start[plane.AxisLinear] += zIncrement;

                segments.Add(lineEnd);
            }

            lineEnd.X = end[0];
            lineEnd.Y = end[1];
            lineEnd.Z = end[2];

            segments.Add(lineEnd);

            return segments;
        }
    }

    public class GCSpline : GCAxisCommand3
    {

        public GCSpline()
        { }

        public GCSpline(Commands cmd, uint lnr, double[] xyz_values, AxisFlags axisFlags, double[] ijpq_values) : base(cmd, lnr, xyz_values, axisFlags)
        {
            Array.Copy(ijpq_values, IJPQKvalues, 4);
        }

        [XmlIgnore]
        public double[] IJPQKvalues { get; set; } = new double[4];
        public double I { get { return IJPQKvalues[0]; } set { IJPQKvalues[0] = value; } }
        public double J { get { return IJPQKvalues[1]; } set { IJPQKvalues[1] = value; } }
        public double P { get { return IJPQKvalues[2]; } set { IJPQKvalues[2] = value; } }
        public double Q { get { return IJPQKvalues[3]; } set { IJPQKvalues[3] = value; } }

        public GcodeBoundingBox GetBoundingBox(GCPlane plane, double[] start, bool isRelative = false)
        {
            GcodeBoundingBox bbox = new GcodeBoundingBox();

            bbox.AddPoint(plane, start[0], start[1], Z);
            bbox.AddPoint(plane, X, Y, Z);

            bbox.Conclude();

            return bbox;
        }

        public List<Point3D> GeneratePoints(double[] start, double arcResolution, bool isRelative = false)
        {
            Point bez_target = new Point(start[0], start[1]);
            List<Point3D> segments = new List<Point3D>();
            Point first = new Point(start[0] + I, start[1] + J);
            Point second = new Point(X + P, Y + Q);

            double t = 0d, step = 0.1d;

            while(t < 1d)
            {
                // First try to reduce the step in order to make it sufficiently
                // close to a linear interpolation.
                bool did_reduce = false;
                double new_t = t + step;

                if (new_t > 1d)
                    new_t = 1d;

                double new_pos0 = eval_bezier(start[0], first.X, second.X, X, new_t),
                       new_pos1 = eval_bezier(start[1], first.Y, second.Y, Y, new_t);

                if (arcResolution > 1d) // TODO: fix!
                    arcResolution = 0.002d;

                while (new_t - t >= arcResolution)
                {

                    //            if (new_t - t < (BEZIER_MIN_STEP))
                    //                break;

                    double candidate_t = 0.5f * (t + new_t),
                           candidate_pos0 = eval_bezier(start[0], first.X, second.X, X, candidate_t),
                           candidate_pos1 = eval_bezier(start[1], first.Y, second.Y, Y, candidate_t),
                           interp_pos0 = 0.5f * (bez_target.X + new_pos0),
                           interp_pos1 = 0.5f * (bez_target.Y + new_pos1);

                    if (dist1(candidate_pos0, candidate_pos1, interp_pos0, interp_pos1) <= (.1d))
                        break;

                    new_t = candidate_t;
                    new_pos0 = candidate_pos0;
                    new_pos1 = candidate_pos1;
                    did_reduce = true;
                }

                if(!did_reduce) while(new_t - t >= (.002d))
                {

                    double candidate_t = t + 2d * (new_t - t);

                    if (candidate_t >= 1.0f)
                        break;

                    double candidate_pos0 = eval_bezier(start[0], first.X, second.X, X, candidate_t),
                           candidate_pos1 = eval_bezier(start[1], first.Y, second.Y, Y, candidate_t),
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

                segments.Add(new Point3D(bez_target.X, bez_target.Y, Z));
            }

            return segments;
        }

        private double interp(double a, double b, double t)
        {
            return (1d - t) * a + t * b;
        }

        private double eval_bezier(double a, double b, double c, double d, double t)
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
        private double dist1(double x1, double y1, double x2, double y2)
        {
            return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
        }
    }

    public class GCCannedDrill : GCAxisCommand3
    {
        public GCCannedDrill()
        { }

        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
        }
        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l, double p) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
            P = p;
        }
        public GCCannedDrill(Commands command, uint lnr, double[] values, AxisFlags axisFlags, double r, uint l, double p, double q) : base(command, lnr, values, axisFlags)
        {
            R = r;
            L = l == 0 ? 1 : l;
            Q = q;
        }

        public uint L { get; set; }
        public double P { get; set; }
        public double Q { get; set; }
        public double R { get; set; }

        public new string ToString()
        {
            return base.ToString() +
                    (R == 0d ? "" : "R" + R.ToInvariantString()) +
                     (L == 0 ? "" : "L" + L.ToString()) +
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

        public DistanceMode DistanceMode { get { return Command == Commands.G90 ? DistanceMode.Absolute : DistanceMode.Incremental; } }
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
