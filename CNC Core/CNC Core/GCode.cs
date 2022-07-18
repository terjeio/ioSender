/*
 * GCode.cs - part of CNC Controls library
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
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using CNC.Core;

namespace CNC.GCode
{

    public enum Dialect
    {
        Grbl,
        GrblHAL,
        LinuxCNC
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
        C = 1 << 5,
        XY = 0x03,
        XZ = 0x05,
        XYZ = 0x07,
        All = 0x3F
    }

    [Flags]
    public enum IJKFlags : int
    {
        None = 0,
        I = 1 << 0,
        J = 1 << 1,
        K = 1 << 2,
        All = 0x07
    }

    [Flags]

    public enum ThreadingFlags : int
    {
        None = 0,
        R = 1 << 0,
        Q = 1 << 1,
        H = 1 << 2,
        E = 1 << 3,
        L = 1 << 4,
        All = 0x1F
    }

    public enum Plane
    {
        XY,
        XZ,
        YZ
    }

    public enum DistanceMode
    {
        Absolute,
        Incremental
    }

    public enum FeedRateMode
    {
        InverseTime,    //G93
        UnitsPerMin,    //G94 - default
        UnitsPerRev     //G95
    }

    public enum MotionMode
    {
        G0 = 0,
        G1 = 10,
        G2 = 20,
        G3 = 30,
        G5 = 50,
        G5_1 = 51,
        G5_2 = 52,
        G33 = 330,
        G38_2 = 382,
        G38_3 = 383,
        G38_4 = 384,
        G38_5 = 385,
        G73 = 730,
        G76 = 760,
        G80 = 800,
        None = G80,
        G81 = 810,
        G82 = 820,
        G83 = 830,
        G84 = 840,
        G85 = 850,
        G86 = 860,
        G87 = 870,
        G88 = 880,
        G89 = 890
    }

    public enum IJKMode
    {
        Absolute,
        Incremental
    }

    public enum Units
    {
        Imperial = 0,
        Metric
    }

    [Flags]
    public enum SpindleState : int
    {
        Off = 1 << 0,
        CW = 1 << 1,
        CCW = 1 << 2
    }

    [Flags]
    public enum CoolantState : int
    {
        Off = 0,
        Flood = 1 << 0,
        Mist = 1 << 1,
        Shower = 1 << 2
    }

    public enum ToolLengthOffset
    {
        Cancel = 0,         // G49 (Default: Must be zero)
        Enable = 1,         // G43
        EnableDynamic = 2,  // G43.1
        ApplyAdditional = 3 // G43.2
    }

    [Flags]
    public enum ThreadTaper : int
    {
        None = 0,
        Entry = 1 << 0,
        Exit = 1 << 1,
        Both = Entry | Exit
    }

    [Flags]
    public enum LatheMode : int
    {
        Disabled = 0,
        Diameter = 1, // Do not change
        Radius = 2    // Do not change
    }

    public enum Direction
    {
        Positive = 0,
        Negative
    }

    public enum InputWaitMode
    {
        Immediate = 0,
        Rise,
        Fall,
        High,
        Low
    }

    public enum Commands
    {
        G0,
        G1,
        G2,
        G3,
        G4,
        G5,
        G5_1,
        G7,
        G8,
        G10,
        G17,
        G18,
        G19,
        G20,
        G21,
        G28,
        G28_1,
        G30,
        G30_1,
        G33,
        G38_2,
        G38_3,
        G38_4,
        G38_5,
        G40,
        G43,
        G43_1,
        G43_2,
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
        G61,
        G61_1,
        G64,
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
        G90_1,
        G91,
        G91_1,
        G92,
        G92_1,
        G92_2,
        G92_3,
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
        M48,
        M49,
        M50,
        M51,
        M52,
        M53,
        M56,
        M61,
        M62,
        M63,
        M64,
        M65,
        M66,
        M67,
        M68,
        Feedrate,
        SpindleRPM,
        ToolSelect,
        Comment,
        UserMCommand,
        Undefined
    }

    public static class FlagsExtensions
    {
        public static IEnumerable<int> ToIndices(this AxisFlags axes)
        {
            int i = 0, j = (int)axes;
            while (j != 0)
            {
                if ((j & 0x01) != 0)
                    yield return i;
                i++; j >>= 1;
            }
        }
        public static IEnumerable<int> ToIndices(this IJKFlags ijkFlags)
        {
            int i = 0, j = (int)ijkFlags;
            while (j != 0)
            {
                if ((j & 0x01) != 0)
                    yield return i;
                i++; j >>= 1;
            }
        }
        public static IEnumerable<int> ToIndices(this ThreadingFlags flags)
        {
            int i = 0, j = (int)flags;
            while (j != 0)
            {
                if ((j & 0x01) != 0)
                    yield return i;
                i++; j >>= 1;
            }
        }
    }

    public static class GCodeUtils
    {
        public static string StripSpaces(string line)
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
    }

    [Serializable]
    public class Macro : ViewModelBase
    {
        string _name;

        [XmlIgnore]
        public bool IsSession { get; set; }

        public int Id { get;  set; }
        public string Name { get { return _name; } set { _name = value;  OnPropertyChanged(); } }
        public bool ConfirmOnExecute { get; set; } = true;
        public string Code { get; set; }
    }

    public struct Point6D
    {
        public double X;
        public double Y;
        public double Z;
        public double A;
        public double B;
        public double C;

        public double this [int i]
        {
            get
            {
                switch(i)
                {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                    case 2:
                        return Z;
                    case 3:
                        return A;
                    case 4:
                        return B;
                    case 5:
                        return C;
                    default:
                        throw new ArgumentException("zyz!", "index");
                }
            }
            set
            {
                switch (i)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                    case 3:
                        A = value;
                        break;
                    case 4:
                        B = value;
                        break;
                    case 5:
                        C = value;
                        break;
                }
            }
        }

        public double[] Array  { get { return new[] { X, Y, Z, A, B, C }; } }

        public Point3D Point3D { get { return new Point3D(X, Y, Z); } }

        public void Set (double[] values, AxisFlags axisFlags, bool relative = false)
        {
            if (relative)
                Add(values, axisFlags);
            else foreach (int i in axisFlags.ToIndices())
            {
                switch (i)
                {
                    case 0:
                        X = values[0];
                        break;
                    case 1:
                        Y = values[1];
                        break;
                    case 2:
                        Z = values[2];
                        break;
                    case 3:
                        A = values[3];
                        break;
                    case 4:
                        B = values[4];
                        break;
                    case 5:
                        C = values[5];
                        break;
                }
            }
        }

        public void Add(double[] values, AxisFlags axisFlags)
        {
            foreach (int i in axisFlags.ToIndices())
            {
                switch (i)
                {
                    case 0:
                        X += values[0];
                        break;
                    case 1:
                        Y += values[1];
                        break;
                    case 2:
                        Z += values[2];
                        break;
                    case 3:
                        A += values[3];
                        break;
                    case 4:
                        B += values[4];
                        break;
                    case 5:
                        C += values[5];
                        break;
                }
            }
        }
    }
}
