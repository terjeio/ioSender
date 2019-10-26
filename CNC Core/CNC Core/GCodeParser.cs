/*
 * GCodeParser.cs - part of CNC Controls library
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
using System.IO;
using System.Globalization;
using System.Windows;

namespace CNC.Core
{
    public class GCodeParser
    {
        private enum ModalGroup
        {
            ModalGroup_G0 = 0,  // [G4,G10,G28,G28.1,G30,G30.1,G53,G92,G92.1] Non-modal
            ModalGroup_G1,      // [G0,G1,G2,G3,G33,G38.2,G38.3,G38.4,G38.5,G76,G80] Motion
            ModalGroup_G2,      // [G17,G18,G19] Plane selection
            ModalGroup_G3,      // [G90,G91] Distance mode
            ModalGroup_G4,      // [G91.1] Arc IJK distance mode
            ModalGroup_G5,      // [G93,G94] Feed rate mode
            ModalGroup_G6,      // [G20,G21] Units
            ModalGroup_G7,      // [G40] Cutter radius compensation mode. G41/42 NOT SUPPORTED.
            ModalGroup_G8,      // [G43,G43.1,G49] Tool length offset
            ModalGroup_G10,     // [G98,G99] Return mode in canned cycles
            ModalGroup_G11,     // [G50,G51] Scaling
            ModalGroup_G12,     // [G54,G55,G56,G57,G58,G59] Coordinate system selection
            ModalGroup_G13,     // [G61] Control mode
            ModalGroup_G14,     // [G96,G97] Spindle Speed Mode
            ModalGroup_G15,     // [G7,G8] Lathe Diameter Mode

            ModalGroup_M4,      // [M0,M1,M2,M30] Stopping
            ModalGroup_M6,      // [M6] Tool change
            ModalGroup_M7,      // [M3,M4,M5] Spindle turning
            ModalGroup_M8,      // [M7,M8,M9] Coolant control
            ModalGroup_M9,      // [M49,M50,M51,M53,M56] Override control
            ModalGroup_M10      // User defined M commands
        }

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        private GCodeToken last_token = new GCodeToken();
        private DistanceMode distance = DistanceMode.Absolute;

        public List<GCodeToken> Tokens { get; private set; } = new List<GCodeToken>();

        public GCodeParser()
        {
            Reset();
        }

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
                                        MessageBox.Show(string.Format("Tool {0} not associated with a profile!", value.ToString()), "GCode parser", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        public void Reset()
        {
            distance = DistanceMode.Absolute;
            last_token.Clear();
            last_token.command = GCodeToken.Command.Undefined;
            Tokens.Clear();
        }

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

    public class ControlledPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }
}
