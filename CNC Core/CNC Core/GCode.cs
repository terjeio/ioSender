/*
 * GCode.cs - part of CNC Controls library
 *
 * v0.02 / 2019-10-31 / Io Engineering (Terje Io)
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

namespace CNC.GCode
{

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
        XZ,
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

    [Flags]
    public enum CoolantState : int
    {
        Off = 0,
        Flood = 1 << 0,
        Mist = 1 << 1,
        Shower = 1 << 2
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

    public enum Commands
    {
        G0,
        G1,
        G2,
        G3,
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
        M49,
        M50,
        M51,
        M53,
        M56,
        M61,
        Feedrate,
        SpindleRPM,
        ToolSelect,
        Dwell,
        Coolant,
        Comment,
        Undefined
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
}
