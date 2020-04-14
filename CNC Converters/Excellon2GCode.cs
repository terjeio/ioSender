/*
 * Excellon2GCode.cs - part of CNC Converters library
 *
 * v0.16 / 2020-04-11 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using CNC.Core;
using CNC.Controls;

namespace CNC.Converters
{

    public class Excellon2GCode : IGCodeConverter
    {
        public struct ExcellonCommand
        {
            public string Command;
            public int tool;
            public Point3D Start;
            public Point3D End;
        }

        internal enum Command
        {
            M48,
            M71,
            M72,
            M95,
            G05
        }

        private bool isDrillMode = false, isMetric = true;
        private bool? isHeader = null;
        private List<JobParametersViewModel.Tool> tools = new List<JobParametersViewModel.Tool>();
        private List<ExcellonCommand> commands = new List<ExcellonCommand>();
        private CNC.Controls.GCode job;
        private Point3D lastPos = new Point3D();
        private JobParametersViewModel settings = new JobParametersViewModel();

        public string FileType { get { return "drl"; } }

        // M48 Start header
        // M71 metric
        // M72 Inch
        // G04Xn Dwell
        // INCH,LZ - LZ = LeadingZeros
        // M95 or % end header
        // G05 Drill Mode (modal)

        public bool LoadFile(CNC.Controls.GCode job, string filename)
        {
            bool ok = true, leadingZeros = false;
            JobParametersViewModel.Tool tool = new JobParametersViewModel.Tool { Id = 0, Diameter = 0d };

            this.job = job;

            settings.Profile = "Excellon";

            if (new JobParametersDialog(settings) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return false;

            FileInfo file = new FileInfo(filename);
            StreamReader sr = file.OpenText();

            using (new UIUtils.WaitCursor())
            {

                string s = sr.ReadLine();

                while (s != null)
                {
                    try
                    {
                        s = s.Trim();

                        if (isHeader == null)
                            isHeader = s == "M48";

                        else if (isHeader == true)
                        {
                            switch (s)
                            {
                                case "METRIC":
                                    isMetric = true;
                                    leadingZeros = false;
                                    break;

                                case "METRIC,TZ":
                                    isMetric = true;
                                    leadingZeros = true;
                                    break;

                                case "INCH":
                                    isMetric = false;
                                    leadingZeros = false;
                                    break;

                                case "INCH,TZ":
                                    isMetric = false;
                                    leadingZeros = true;
                                    break;

                                case "%":
                                case "M95":
                                    isHeader = false;
                                    break;
                            }

                            if (s[0] == 'T')
                            {
                                int cpos = s.IndexOf('C');
                                tools.Add(new JobParametersViewModel.Tool { Id = int.Parse(s.Substring(1, cpos - 1)), Diameter = dbl.Parse(s.Substring(cpos + 1)) });
                            }
                        }
                        else
                        {
                            switch (s[0])
                            {
                                case 'G':
                                    switch (s)
                                    {
                                        case "G00":
                                        case "G01":
                                        case "G02":
                                        case "G03":
                                            isDrillMode = false;
                                            break;

                                        case "G05":
                                            isDrillMode = true;
                                            break;
                                    }
                                    break;

                                case 'X':
                                    if (isDrillMode)
                                    {
                                        int g85pos = s.IndexOf("G85");
                                        string args = g85pos >= 0 ? s.Substring(0, g85pos) : s;
                                        int ypos = args.IndexOf('Y');
                                        double factor = leadingZeros ? (ypos == 8 ? 1000d : 10000d) : 1.0;
                                        var cmd = new ExcellonCommand();
                                        cmd.Command = g85pos >= 0 ? "Slot" : "Drill";
                                        cmd.tool = tool.Id;
                                        cmd.Start.X = dbl.Parse(args.Substring(1, ypos - 1)) / factor / (isMetric ? 1d : 25.4d);
                                        cmd.Start.Y = dbl.Parse(args.Substring(ypos + 1)) / factor / (isMetric ? 1d : 25.4d);
                                        if (g85pos >= 0)
                                        {
                                            isDrillMode = false;
                                            args = s.Substring(g85pos + 3);
                                            ypos = args.IndexOf('Y');
                                            cmd.End.X = dbl.Parse(args.Substring(1, ypos - 1)) / (isMetric ? 1d : 25.4d);
                                            cmd.End.Y = dbl.Parse(args.Substring(ypos + 1)) / (isMetric ? 1d : 25.4d);
                                        }
                                        commands.Add(cmd);
                                    }
                                    break;

                                case 'T':
                                    tool = tools.Where(x => x.Id == int.Parse(s.Substring(1))).FirstOrDefault();
                                    break;
                            }
                        }
                    }
                    catch
                    {
                    }
                    s = sr.ReadLine();
                }

                sr.Close();

                job.AddBlock(filename, CNC.Core.Action.New);
                job.AddBlock("(Translated by Excellon to GCode converter)");
                job.AddBlock("G90G17G21G50");

                if (settings.ScaleX != 1d || settings.ScaleY != 1d)
                    job.AddBlock(string.Format("G51X{0}Y{1}", settings.ScaleX.ToInvariantString(), settings.ScaleY.ToInvariantString()));

                job.AddBlock("G0Z" + settings.ZRapids.ToInvariantString());
                job.AddBlock("X0Y0");

                var target = new Point3D(0d, 0d, settings.ZRapids);

                foreach (var t in tools)
                {
                    job.AddBlock("M5");
                    target.X = 0d;
                    target.Y = 0d;
                    target.Z = settings.ZHome;
                    job.AddBlock("G0" + PosToParams(target));
                    job.AddBlock(string.Format("M6 (MSG, {0} mm {1})", (t.Diameter < settings.ToolDiameter ? t.Diameter : settings.ToolDiameter).ToInvariantString(), t.Diameter < settings.ToolDiameter ? "drill" : "mill"));
                    job.AddBlock("M3S" + settings.RPM.ToInvariantString());
                    job.AddBlock("G4P1");
                    target.Z = settings.ZRapids;
                    job.AddBlock("G0" + PosToParams(target));
                    job.AddBlock("F" + settings.PlungeRate.ToInvariantString());

                    foreach (var cmd in commands)
                    {
                        if (cmd.tool == t.Id)
                        {
                            switch (cmd.Command)
                            {
                                case "Drill":
                                    if (t.Diameter > settings.ToolDiameter)
                                    {
                                        double r = (t.Diameter - settings.ToolDiameter) / 2d;
                                        target.X = cmd.Start.X + r;
                                        target.Y = cmd.Start.Y;
                                        string p = PosToParams(target);
                                        if (p.Length > 0)
                                            job.AddBlock("G0" + p);
                                        target.Z = settings.ZMin;
                                        job.AddBlock("G1" + PosToParams(target));
                                        job.AddBlock(string.Format("G{0}X{1}I-{2}", "2", target.X.ToInvariantString(), r.ToInvariantString()));
                                        target.Z = settings.ZRapids;
                                        job.AddBlock("G0" + PosToParams(target));
                                    }
                                    else
                                        OutputG81(new Point3D(cmd.Start.X, cmd.Start.Y, settings.ZMin));
                                    break;
                                case "Slot":
                                    OutputSlot(cmd, t.Diameter);
                                    break;
                            }
                        }
                    }
                }

                job.AddBlock("G0X0Y0Z" + settings.ZHome.ToInvariantString());
                job.AddBlock("M30", CNC.Core.Action.End);
            }

            return ok;
        }

        string PosToParams (Point3D pos)
        {
            string gcode = string.Empty;

            if (pos.X != lastPos.X)
                gcode += "X" + Math.Round(pos.X, 3).ToInvariantString();

            if (pos.Y != lastPos.Y)
                gcode += "Y" + Math.Round(pos.Y, 3).ToInvariantString();

            if (pos.Z != lastPos.Z)
                gcode += "Z" + Math.Round(pos.Z, 3).ToInvariantString();

            lastPos = pos;

            return gcode;
        }

        void OutputSlot(ExcellonCommand cmd, double tsize)
        {
            double x = cmd.End.X - cmd.Start.X;
            double y = cmd.End.Y - cmd.Start.Y;
            double dist = Math.Sqrt(x * x + y * y);
            int holes = (int)Math.Round(dist / (tsize / 3d) + 0.5d, 0);
            double factor = dist / (holes - 1);
            x = x / dist * factor;
            y = y / dist * factor;

            job.AddBlock(string.Format("(Slot {0};{1} - {2};{3})", cmd.Start.X.ToInvariantString(), cmd.Start.Y.ToInvariantString(), cmd.End.X.ToInvariantString(), cmd.End.Y.ToInvariantString()));

            Point3D target = new Point3D(cmd.Start.X, cmd.Start.Y, settings.ZMin);

            OutputG81(target);

            while (--holes > 0)
            {
                target.X += x;
                target.Y += y;
                OutputG81(target);
            }

            lastPos = target;    
        }

        void OutputG81(Point3D pos)
        {
            string p = PosToParams(pos);

            if (p.Length > 0)
                job.AddBlock("G81" + p + "R" + settings.ZSafe.ToInvariantString());
        }
    }
}
