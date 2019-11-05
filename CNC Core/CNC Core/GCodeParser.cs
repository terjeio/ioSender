/*
 * GCodeParser.cs - part of CNC Controls library
 *
 * v0.02 / 2019-11-05 / Io Engineering (Terje Io)
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
using CNC.Core;

namespace CNC.GCode
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
                    IJK[i] = double.NaN;
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

        // Modal Group G8: Tool length offset
        enum ToolLengthOffset
        {
            Cancel = 0,         // G49 (Default: Must be zero)
            Enable = 1,         // G43
            EnableDynamic = 2,  // G43.1
            ApplyAdditional = 3 // G43.2
        }

        const string ignore = "$!~?;";
        const string collect = "0123456789.+- ";

        #endregion

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        private bool isImperial = false, doScaling = false;
        private GCValues gcValues = new GCValues();
        private GCodeToken last_token = new GCodeToken();
        private GCDistanceMode distanceMode;
        private GCIJKMode ijkMode;
        private SpindleState spindleState;
        private CoolantState coolantState;
        private GCPlane plane;
        private LatheMode latheMode;
        private GCUnits units;
        private MotionMode motionMode;
        private ToolLengthOffset toolLengthOffset;

        private uint coordSystem;
        private int demarcCount;
        private double[] scaleFactors = new double[6];
        private double[] toolOffsets = new double[6];
        private Commands retractMode;
        private Commands spindleRpmMode;
        private Commands feedrateMode;

        private WordFlags[] AxisFlags = new WordFlags[] { WordFlags.X, WordFlags.Y, WordFlags.Z, WordFlags.A, WordFlags.B, WordFlags.C };
        private WordFlags[] IJKFlags = new WordFlags[] { WordFlags.I, WordFlags.J, WordFlags.K };

        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();
        public bool ProgramEnd { get; private set; }

        public GCodeParser()
        {
            Reset();
        }

        public void Reset()
        {
            gcValues.Clear();
            Tokens.Clear();
            ProgramEnd = false;
            motionMode = MotionMode.Seek;
            toolLengthOffset = ToolLengthOffset.Cancel;
            coordSystem = 0;
            plane = new GCPlane(Commands.G17, 0);
            spindleState = SpindleState.Off;
            coolantState = CoolantState.Off;
            latheMode = LatheMode.Disabled;
            retractMode = Commands.G98;
            spindleRpmMode = Commands.G96;
            feedrateMode = Commands.G94;
            units = new GCUnits(Commands.G21, 0);                // mm
            distanceMode = new GCDistanceMode(Commands.G90, 0);  // Absolute
            ijkMode = new GCIJKMode(Commands.G91_1, 0);          // Incremental
            demarcCount = 0;
            doScaling = false;
            for (int i = 0; i < scaleFactors.Length; i++)
                scaleFactors[i] = 1d;
            for (int i = 0; i < toolOffsets.Length; i++)
                toolOffsets[i] = 0d;
        }

        private double scale { get { return isImperial ? 25.4d : 1d;  } }

        public bool ParseBlock(string block, bool quiet)
        {
            WordFlags wordFlags = 0, axisWords = 0, ijkWords = 0, wordFlag = 0;
            ModalGroups modalGroups = 0, modalGroup = 0;

            bool isDwell = false, isScaling = false, inMessage = false;
            string gcode = string.Empty, comment = string.Empty;
            Commands nonModal = Commands.Undefined, programFlow = Commands.Undefined, cmdPlane = Commands.Undefined;
      //      Commands distanceMode, ijkMode;
            double value;
            AxisCommand axisCommand = AxisCommand.None;

            List<string> gcodes = new List<string>();

            if (block.Length == 0 || block[0] == ';')
                return block.Length != 0;

            if (block.IndexOf(';') > 0)
                block = block.Substring(0, block.IndexOf(';'));

            if (block.Length == 0 || ignore.Contains(block[0]) || ProgramEnd)
                return false;

            if (quiet)
                return true;

            if(block[0] == '%')
            {
                if(++demarcCount == 2)
                    ProgramEnd = true;
                return true;
            }

            gcValues.N++;

            block += '\r';

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
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
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
                            nonModal = Commands.G10;
                            break;

                        case 28:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            nonModal = Commands.G28 + fv;
                            break;

                        case 30:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            nonModal = Commands.G30 + fv;
                            break;

                        case 92:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            nonModal = Commands.G92 + fv;
                            break;

                        case 7:
                        case 8:
                            latheMode = iv == 7 ? LatheMode.Diameter : LatheMode.Radius;
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
                            modalGroup = ModalGroups.G6;
                            isImperial = units.Imperial;
                            break;

                        case 33:
                        case 76:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
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
                                toolLengthOffset = ToolLengthOffset.Cancel;
                            else
                                toolLengthOffset = ToolLengthOffset.Enable + fv;
                            modalGroup = ModalGroups.G8;
                            break;

                        case 50:
                        case 51:
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G11;
                            axisCommand = AxisCommand.Scaling;
                            isScaling = iv == 51;
                            break;

                        case 53:
                            axisCommand = AxisCommand.NonModal;
                            modalGroup = ModalGroups.G0;
                            nonModal = Commands.G53;
                            break;

                        case 54:
                        case 55:
                        case 56:
                        case 57:
                        case 58:
                        case 59:
                            coordSystem = (uint)(iv + fv);
                            modalGroup = ModalGroups.G12;
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
                            if (axisCommand != AxisCommand.None)
                                throw new GCodeException("Axis command conflict");
                            modalGroup = ModalGroups.G1;
                            axisCommand = AxisCommand.MotionMode;
                            motionMode = (MotionMode)iv;
                            break;

                        case 90:
                        case 91:
                            if (fv == 0)
                            {
                                modalGroup = ModalGroups.G3;
                                Tokens.Add(distanceMode = new GCDistanceMode(Commands.G90 + (iv - 90), gcValues.N));
                            }
                            else
                            {
                                modalGroup = ModalGroups.G4;
                                Tokens.Add(ijkMode = new GCIJKMode(Commands.G90_1 + (iv - 90), gcValues.N));
                            }
                            break;

                        case 93:
                        case 94:
                        case 95:
                            feedrateMode = Commands.G93 + (iv - 93);
                            modalGroup = ModalGroups.G5;
                            break;

                        case 96:
                        case 97:
                            spindleRpmMode = Commands.G95 + (iv - 97);
                            modalGroup = ModalGroups.G14;
                            break;

                        case 98:
                        case 99:
                            retractMode = Commands.G98 + (iv - 98);
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
                            programFlow = iv == 30 ? Commands.M30 : (Commands.M0 + iv);
                            modalGroup = ModalGroups.M4;
                            break;

                        case 3:
                        case 4:
                        case 5:
                            spindleState = iv == 5 ? SpindleState.Off : (iv == 3 ? SpindleState.CW : SpindleState.CCW);
                            modalGroup = ModalGroups.M7;
                            break;

                        case 6:
                            modalGroup = ModalGroups.M6;
                            break;

                        case 7:
                            coolantState |= CoolantState.Mist;
                            modalGroup = ModalGroups.M8;
                            break;

                        case 8:
                            coolantState |= CoolantState.Flood;
                            modalGroup = ModalGroups.M8;
                            break;

                        case 9:
                            coolantState = CoolantState.Off;
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

                    #endregion

                    if (modalGroup > 0 && modalGroups.HasFlag(modalGroup))
                    {
                        throw new GCodeException("Modal group violation");
                    }
                    else
                        modalGroups |= modalGroup;
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
                                wordFlag = WordFlags.L;
                                break;

                            case 'I':
                                gcValues.I = value;
                                wordFlag = WordFlags.I;
                                ijkWords |= WordFlags.I;
                                break;

                            case 'J':
                                gcValues.J = value;
                                wordFlag = WordFlags.J;
                                ijkWords |= WordFlags.J;
                                break;

                            case 'K':
                                gcValues.K = value;
                                wordFlag = WordFlags.K;
                                ijkWords |= WordFlags.K;
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
                                axisWords |= WordFlags.X;
                                gcValues.X = value;
                                break;

                            case 'Y':
                                wordFlag = WordFlags.Y;
                                axisWords |= WordFlags.Y;
                                gcValues.Y = value;
                                break;

                            case 'Z':
                                wordFlag = WordFlags.Z;
                                axisWords |= WordFlags.Z;
                                gcValues.Z = value;
                                break;

                            case 'A':
                                wordFlag = WordFlags.A;
                                axisWords |= WordFlags.A;
                                gcValues.A = value;
                                break;

                            case 'B':
                                wordFlag = WordFlags.B;
                                axisWords |= WordFlags.B;
                                gcValues.B = value;
                                break;

                            case 'C':
                                wordFlag = WordFlags.C;
                                axisWords |= WordFlags.C;
                                gcValues.C = value;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new GCodeException("Invalid GCode", e);
                    }
                    #endregion
                }
                else if (code[0] == '(' && code.Length > 5 && code.Substring(0, 5).ToUpperInvariant() == "(MSG,")
                    comment = code;

                if (wordFlag > 0 && wordFlags.HasFlag(wordFlag))
                {
                    throw new GCodeException("Word repeated");
                }
                else
                    wordFlags |= wordFlag;
            }

            // [0. Non-specific/common error-checks and miscellaneous setup]:

// [1. Comments feedback]
            
            if(comment != string.Empty)
            {
                Tokens.Add(new GCComment(Commands.Comment, gcValues.N, comment));
                comment = string.Empty;
            }

// [2. Set feed rate mode]

            if (modalGroups.HasFlag(ModalGroups.G11))
            {
                Tokens.Add(new GCodeToken(feedrateMode, gcValues.N));
            }

// [3. Set feed rate]

            if (wordFlags.HasFlag(WordFlags.F))
            {
                Tokens.Add(new GCFeedrate(Commands.Feedrate, gcValues.N, gcValues.F));
            }

// [4. Set spindle speed]

            if (modalGroups.HasFlag(ModalGroups.G14))
            {
                Tokens.Add(new GCodeToken(spindleRpmMode, gcValues.N));
            }

            if (wordFlags.HasFlag(WordFlags.S))
            {
                Tokens.Add(new GCFeedrate(Commands.ToolSelect, gcValues.N, gcValues.S));
            }

// [5. Select tool]

            if (wordFlags.HasFlag(WordFlags.T))
            {
                Tokens.Add(new GCToolSelect (Commands.ToolSelect, gcValues.N, gcValues.T));

                if (!quiet && ToolChanged != null && !ToolChanged(gcValues.T))
                    MessageBox.Show(string.Format("Tool {0} not associated with a profile!", gcValues.T.ToString()), "GCode parser", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

// [6. Change tool]

            // M6
            if (modalGroups.HasFlag(ModalGroups.M6))
            {
                Tokens.Add(new GCodeToken(Commands.M6, gcValues.N));
            }

// [7. Spindle control]

            // M3 - M5
            if (modalGroups.HasFlag(ModalGroups.M7))
            {
                Tokens.Add(new GCSpindleState(gcValues.N, spindleState));
            }

// [8. Coolant control]

            // M7 - M9
            if (modalGroups.HasFlag(ModalGroups.M8))
            {
                Tokens.Add(new GCCoolantState(gcValues.N, coolantState));
            }

// [9. Override control]

            // M49 - M51, M53, M56
            if (modalGroups.HasFlag(ModalGroups.M9))
            {
            }

            // [9a. User defined M commands ]:

// [10. Dwell]

            if(isDwell && wordFlags.HasFlag(WordFlags.P))
            {
                Tokens.Add(new GCDwell(Commands.Dwell, gcValues.N, gcValues.P));
            }

// [11. Set active plane]

            // G17, G18, G19
            if (modalGroups.HasFlag(ModalGroups.G2))
            {
                Tokens.Add(plane = new GCPlane(cmdPlane, gcValues.N));
            }

// [12. Set length units]

            // G20, G21
            if (modalGroups.HasFlag(ModalGroups.G6))
            {
                Tokens.Add(new GCUnits(isImperial ? Commands.G20 : Commands.G21, gcValues.N));
            }

            // Scaling: G50, G51
            if (modalGroups.HasFlag(ModalGroups.G11))
            {
                doScaling = false;
                if (isScaling)
                {
                    for (int i = 0; i < GrblInfo.NumAxes; i++)
                    {
                        if (axisWords.HasFlag(AxisFlags[i]))
                            scaleFactors[i] = gcValues.XYZ[i];
                        doScaling |= scaleFactors[i] != 1d;
                    }
                    axisWords = 0;
                }
                else for (int i = 0; i < scaleFactors.Length; i++)
                    scaleFactors[i] = 1d;
            }

            // Perform scaling
            if (axisWords != 0)
            {
                if (isImperial)
                {
                    for (int i = 0; i < GrblInfo.NumAxes; i++)
                        if (axisWords.HasFlag(AxisFlags[i]))
                            gcValues.XYZ[i] *= 25.4d;
                }
                if (doScaling)
                {
                    for (int i = 0; i < GrblInfo.NumAxes; i++)
                        if (axisWords.HasFlag(AxisFlags[i]))
                            gcValues.XYZ[i] *= scaleFactors[i];
                }
            }

            // [13. Cutter radius compensation]

            // G40
            if (modalGroups.HasFlag(ModalGroups.G7))
            {
                Tokens.Add(new GCPlane(Commands.G40, gcValues.N));
            }

// [14. Tool length compensation]

            // G43, G43.1, G43.2, G49
            if (modalGroups.HasFlag(ModalGroups.G8))
            {
                switch (toolLengthOffset)
                {
                    case ToolLengthOffset.Enable:
                        Tokens.Add(new GCToolOffset(Commands.G43, gcValues.N, (uint)gcValues.H));
                        break;

                    case ToolLengthOffset.ApplyAdditional:
                        Tokens.Add(new GCToolOffset(Commands.G43_2, gcValues.N, (uint)gcValues.H));
                        break;

                    case ToolLengthOffset.EnableDynamic:
                        {
                            for (int i = 0; i < GrblInfo.NumAxes; i++)
                            {
                                if (axisWords.HasFlag(AxisFlags[i]))
                                    toolOffsets[i] = gcValues.XYZ[i];
                            }
                            axisWords = 0;
                        }
                        Tokens.Add(new GCToolOffsets(Commands.G43_1, gcValues.N, toolOffsets));
                        break;

                    case ToolLengthOffset.Cancel:
                        {
                            for (int i = 0; i < toolOffsets.Length; i++)
                                toolOffsets[i] = 0d;
                        }
                        Tokens.Add(new GCodeToken(Commands.G49, gcValues.N));
                        break;
                }
            }

// [15. Coordinate system selection]

            // G54 - G59, G59.1 - G59.3
            if (modalGroups.HasFlag(ModalGroups.G12))
            {
                Tokens.Add(new GCodeToken(Commands.G54 + (int)coordSystem, gcValues.N));
            }

            // [16. Set path control mode ]: G61.1/G64 NOT SUPPORTED

            // [17. Set distance mode ]:
            // G90, G91

            // [18. Set retract mode]

            if (modalGroups.HasFlag(ModalGroups.G10))
            {
                Tokens.Add(new GCodeToken(retractMode, gcValues.N));
            }

            // [19. Go to predefined position, Set G10, or Set axis offsets ]:

            if (modalGroups.HasFlag(ModalGroups.G0))
            {
                switch(nonModal)
                {
                    case Commands.G10:
                        break;

                    case Commands.G28:
                    case Commands.G30:
                    case Commands.G53:
                        Tokens.Add(new GCLinearMotion(nonModal, gcValues.N, gcValues.XYZ));
                        break;

                    case Commands.G28_1:
                        Tokens.Add(new GCCoordinateSystem(retractMode, gcValues.N, 11, gcValues.XYZ));
                        break;

                    case Commands.G30_1:
                        Tokens.Add(new GCCoordinateSystem(retractMode, gcValues.N, 12, gcValues.XYZ));
                        break;

                    case Commands.G92:
                        Tokens.Add(new GCCoordinateSystem(retractMode, gcValues.N, 10, gcValues.XYZ));
                        break;

                    case Commands.G92_1:
                    case Commands.G92_2:
                    case Commands.G92_3:
                        Tokens.Add(new GCodeToken(nonModal, gcValues.N));
                        break;
                }

                axisWords = 0;
            }

// [20. Motion modes]

            // Lathe mode: G7, G8
            if (modalGroups.HasFlag(ModalGroups.G15))
            {
                Tokens.Add(new GCLatheMode(latheMode == LatheMode.Diameter ? Commands.G7 : Commands.G8, gcValues.N));
            }

            // Cancel canned cycle mode: G80
            if(modalGroups.HasFlag(ModalGroups.G1) && axisCommand == AxisCommand.None)
            {
                Tokens.Add(new GCodeToken(Commands.G80, gcValues.N));
            }

            if (motionMode != MotionMode.None && axisWords != 0)
            {
                switch (motionMode)
                {
                    case MotionMode.Seek:
                        Tokens.Add(new GCLinearMotion(Commands.G0, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.Linear:
                        Tokens.Add(new GCLinearMotion(Commands.G1, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.CwArc:
                    case MotionMode.CcwArc:
                        if (wordFlags.HasFlag(WordFlags.R))
                        {
                            gcValues.IJK[0] = gcValues.IJK[1] = gcValues.IJK[2] = double.NaN;
                            if (isImperial)
                                gcValues.R *= 25.4d;
                            if (doScaling)
                                gcValues.R *= scaleFactors[plane.Axis0] > scaleFactors[plane.Axis1] ? scaleFactors[plane.Axis0] : scaleFactors[plane.Axis1];
                        }
                        else if (isImperial && ijkWords != 0)
                        {
                            for (int i = 0; i < 3; i++) {
                                if (ijkWords.HasFlag(IJKFlags[i]))
                                {
                                    gcValues.IJK[i] *= 25.4d;
                                    if (doScaling)
                                        gcValues.IJK[i] *= scaleFactors[i];
                                }
                            }
                        }
                        Tokens.Add(new GCArc(motionMode == MotionMode.CwArc ? Commands.G2 : Commands.G3, gcValues.N, gcValues.XYZ, gcValues.IJK, gcValues.R, ijkMode.IJKMode));
                        break;

                    case MotionMode.ProbeToward:
                        Tokens.Add(new GCLinearMotion(Commands.G38_2, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.ProbeTowardNoError:
                        Tokens.Add(new GCLinearMotion(Commands.G38_3, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.ProbeAway:
                        Tokens.Add(new GCLinearMotion(Commands.G38_4, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.ProbeAwayNoError:
                        Tokens.Add(new GCLinearMotion(Commands.G38_5, gcValues.N, gcValues.XYZ));
                        break;

                    case MotionMode.CannedCycle81:
                        Tokens.Add(new GCCannedDrill(Commands.G81, gcValues.N, gcValues.XYZ, gcValues.R));
                        break;
                }
            }

// [21. Program flow]

            if (modalGroups.HasFlag(ModalGroups.M4))
            {
                ProgramEnd = programFlow == Commands.M2 || programFlow == Commands.M30;
                Tokens.Add(new GCodeToken(programFlow, gcValues.N));
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
                        typeof(GCScaling)
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
    public class GCAxisCommand3 : GCodeToken
    {
        public GCAxisCommand3()
        { }

        public GCAxisCommand3(Commands command, uint lnr, double[] values) : base(command, lnr)
        {
            Array.Copy(values, Values, 3);
        }

        public double[] Values { get; set; } = new double[3];

        public double X { get { return Values[0]; } set { Values[0] = value; } }
        public double Y { get { return Values[1]; } set { Values[1] = value; } }
        public double Z { get { return Values[2]; } set { Values[2] = value; } }
    }
    public class GCAxisCommand6 : GCodeToken
    {
        public GCAxisCommand6()
        { }

        public GCAxisCommand6(Commands command, uint lnr, double[] values) : base(command, lnr)
        {
            Array.Copy(values, Values, 3); // Only copy for num axes?
        }

        public double[] Values { get; set; } = new double[6];
        public double X { get { return Values[0]; } set { Values[0] = value; } }
        public double Y { get { return Values[1]; } set { Values[1] = value; } }
        public double Z { get { return Values[2]; } set { Values[2] = value; } }
        public double A { get { return Values[3]; } set { Values[3] = value; } }
        public double B { get { return Values[4]; } set { Values[4] = value; } }
        public double C { get { return Values[5]; } set { Values[5] = value; } }
    }

    public class GCLinearMotion : GCAxisCommand6
    {
        public GCLinearMotion()
        { }

        public GCLinearMotion(Commands command, uint lnr, double[] values) : base(command, lnr, values)
        { }

        //public GCLinearMotion(Commands command, uint lnr, double[] values) : base(command, lnr)
        //{
        //    X = values[0];
        //    Y = values[1];
        //    Z = values[2];
        //}

        //public double X { get; set; }
        //public double Y { get; set; }
        //public double Z { get; set; }
        //public double A { get; set; }
        //public double B { get; set; }
        //public double C { get; set; }
    }


    public class GCArc : GCAxisCommand3
    {
        public GCArc()
        { }

        public GCArc(Commands cmd, uint lnr, double[] xyz_values, double[] ijk_values, double r, IJKMode ijkMode) : base(cmd, lnr, xyz_values)
        {
            Array.Copy(ijk_values, IJKvalues, 3);

            R = r;
            IJKMode = ijkMode;
        }

        public double[] IJKvalues { get; set; } = new double[3];
        public double I { get { return IJKvalues[0]; } set { IJKvalues[0] = value; } }
        public double J { get { return IJKvalues[1]; } set { IJKvalues[1] = value; } }
        public double K { get { return IJKvalues[2]; } set { IJKvalues[2] = value; } }
        public double R { get; set; }

        public IJKMode IJKMode { get; set; }
        public bool IsRadiusMode { get { return double.IsNaN(I) && double.IsNaN(J) && double.IsNaN(K); } }
        public bool IsClocwise { get { return Command == Commands.G2; } }
    }

    public class GCCannedDrill : GCAxisCommand3
    {
        public GCCannedDrill()
        { }

        public GCCannedDrill(Commands command, uint lnr, double[] values, double r) : base(command, lnr, values)
        {
            R = r;
        }

        public double R { get; set; }
    }

    public class GCCoordinateSystem : GCAxisCommand6
    {
        public GCCoordinateSystem()
        { }

        public GCCoordinateSystem(Commands cmd, uint lnr, uint p, double[] values) : base(cmd, lnr, values)
        {
            P = p;
        }

        public uint P { get; set; }
        public string Code { get { return "Current,G54,G55,G56,G57,G58,G59,G59.1,G59.2,G59.3,G92,G28,G30".Split(',').ToArray()[P]; } }

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
    }

    public class GCToolOffsets : GCAxisCommand3
    {
        public GCToolOffsets()
        { }

        public GCToolOffsets(Commands cmd, uint lnr, double[] values) : base(cmd, lnr, values)
        {
        }
    }

    public class GCToolTable : GCAxisCommand3
    {
        public GCToolTable()
        { }

        public GCToolTable(Commands cmd, uint lnr, uint p, double r, double[] values) : base(cmd, lnr, values)
        {
            P = p;
            R = r;
        }

        public uint P { get; set; }
        public double R { get; set; }
    }

    public class GCScaling : GCAxisCommand6
    {
        public GCScaling()
        { }

        public GCScaling(Commands command, uint lnr, double[] values) : base(command, lnr, values)
        {
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
    }

    public class GCCoolantState : GCodeToken
    {
        public GCCoolantState()
        { }

        public GCCoolantState(uint lnr, CoolantState coolantState)
        {
            LineNumber = lnr;
            Command = Commands.Coolant;
            CoolantState = coolantState;
        }

        public CoolantState CoolantState { get; set; }
    }


    public class GCPlane : GCodeToken
    {
        public GCPlane()
        { }

        public GCPlane(Commands cmd, uint lnr) : base(cmd, lnr)
        {
        }

        public Plane Plane { get { return Command == Commands.G17 ? Plane.XY : (Command == Commands.G18 ? Plane.XZ : Plane.YZ); }}
        public int Axis0 { get { return Plane == Plane.XY ? 0 : (Plane == Plane.XZ ? 0 : 1); } }
        public int Axis1 { get { return Plane == Plane.XY ? 1 : (Plane == Plane.XZ ? 2 : 2); } }
        public int AxisLinear { get { return Plane == Plane.XY ? 2 : (Plane == Plane.XZ ? 1 : 0); } }

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

    public class GCDwell : GCodeToken
    {
        public GCDwell()
        { }

        public GCDwell(Commands command, uint lnr, double time) : base(command, lnr)
        {
            Time = time;
        }

        public double Time { get; set; }
    }

    public class GCComment : GCodeToken
    {
        public GCComment()
        { }

        public GCComment(Commands command, uint lnr, string comment) : base(command, lnr)
        {
            Comment = comment;
        }

        public string Comment { get; set; }
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
