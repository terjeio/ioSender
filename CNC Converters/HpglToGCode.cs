/*
 * Hpgl2GCode.cs - part of CNC Converters library
 *
 * v0.16 / 2022-12-01 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2022, Io Engineering (Terje Io)
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
using CNC.Controls;
using CNC.Core;

namespace CNC.Converters
{

    public class HpglToGCode : IGCodeConverter
    {

        internal class HPGLCommand
        {
            public string Command;
            public Point3D Pos;
            public double R;
            public bool isDown;
        }

        public class Vector
        {
            public Vector()
            { }

            public Vector(Vector v)
            {
                PolygonId = v.PolygonId;
                IsArc = v.IsArc;
                Start = new Point3D(v.Start.X, v.Start.Y, v.Start.Z);
                End = new Point3D(v.End.X, v.End.Y, v.End.Z);
                R = v.R;
                seq = v.seq;
            }

            public int PolygonId;
            public bool IsArc;
            public Point3D Start;
            public Point3D End;
            public double R;
            public int seq;
        }

        private double scaleFix = 0.025d, tolerance = 0.001d;
        private List<HPGLCommand> commands = new List<HPGLCommand>();
        private List<HPGLCommand> pm0 = new List<HPGLCommand>();
        private List<Vector> vectors = new List<Vector>();
        private List<Polygon> polygons = new List<Polygon>();
        private CNC.Controls.GCode job;
        private bool isCut = false, inPolygon = false;
        private Point3D lastPos = new Point3D();
        private double lastFeedRate = 0d;
        private JobParametersViewModel settings = new JobParametersViewModel();

        bool isDown = false;
        Point3D offset = new Point3D(100000d, 100000d, 0d), pos = new Point3D();

        public string FileType { get { return "plt"; } }

        private void toVectors (List<HPGLCommand> commands)
        {
            vectors.RemoveRange(0, vectors.Count);

            for (int i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];

                try
                {
                    switch (cmd.Command)
                    {
                        case "IN":
                            pos.X = cmd.Pos.X = 0d;
                            pos.Y = cmd.Pos.Y = 0d;
                            isDown = cmd.isDown = false;
                            break;

                        case "PU":
                            if (isDown)
                            {
                                cmd.Pos.X = pos.X;
                                cmd.Pos.Y = pos.Y;
                                isDown = false;
                            }
                            break;

                        case "PD":
                            if (!isDown)
                            {
                                cmd.Pos.X = pos.X;
                                cmd.Pos.Y = pos.Y;
                                isDown = true;
                            }
                            break;

                        case "PA":
                            cmd.Pos.X -= offset.X;
                            cmd.Pos.Y -= offset.Y;
                            if ((cmd.isDown = isDown))
                            {
                                var v = new Vector();
                                v.IsArc = false;
                                v.Start = pos;
                                v.End.X = cmd.Pos.X;
                                v.End.Y = cmd.Pos.Y;
                                vectors.Add(v);
                            }
                            break;

                        case "AA":
                            {
                                cmd.Pos.X = cmd.Pos.X - offset.X;
                                cmd.Pos.Y = cmd.Pos.Y - offset.X;
                                if ((cmd.isDown = isDown))
                                {
                                    var v = new Vector();
                                    v.IsArc = true;
                                    v.Start = pos;
                                    v.End.X = cmd.Pos.X + (cmd.Pos.X - pos.X);
                                    v.End.Y = cmd.Pos.Y + (cmd.Pos.Y - pos.Y);
                                    vectors.Add(v);
                                }
                            }
                            break;
                    }
                    pos.X = cmd.Pos.X;
                    pos.Y = cmd.Pos.Y;
                }
                catch
                {
                }
            }

        }

        public bool LoadFile(CNC.Controls.GCode job, string filename)
        {
            bool ok = true;

            this.job = job;

            if (filename.EndsWith("Edge_Cuts.plt") || filename.EndsWith("Paste.plt"))
                isCut = !filename.EndsWith("Paste.plt");
            else
                isCut = (GrblMode)GrblSettings.GetDouble(GrblSetting.Mode) != GrblMode.Laser;

            settings.EnableToolSelection = true;
            settings.Profile = "HPGL" + (isCut ? "" : "Laser");

            if (new JobParametersDialog(settings) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return false;

            FileInfo file = new FileInfo(filename);
            StreamReader sr = file.OpenText();

            using (new UIUtils.WaitCursor()) {

                string s = sr.ReadLine();

                while (s != null)
                {
                    foreach (string cmd in s.Split(';'))
                    {
                        try
                        {
                            switch (cmd.Substring(0, 2))
                            {
                                case "PM":
                                    if ((inPolygon = dbl.Parse(cmd.Substring(2)) == 0d))
                                    {
                                        pm0.Clear();
                                        HPGLCommand hpgl = new HPGLCommand();
                                        hpgl.Command = commands.Last().Command;
                                        hpgl.Pos.X = commands.Last().Pos.X;
                                        hpgl.Pos.Y = commands.Last().Pos.Y;
                                        pm0.Add(hpgl);
                                    }
                                    else if (pm0.Count > 0)
                                    {
                                        Polygon polygon;
                                        toVectors(pm0);
                                        if ((polygon = Polygon.findPolygon(vectors, tolerance)) != null)
                                            polygons.Add(polygon);
                                    }
                                    break;

                                case "PT":
                                    scaleFix = 0.025d;
                                    break;

                                case "PA":
                                    {
                                        var args = cmd.Substring(2).Split(',');
                                        if (args.Length > 1)
                                        {
                                            HPGLCommand hpgl = new HPGLCommand();
                                            hpgl.Command = cmd.Substring(0, 2);
                                            hpgl.Pos.X = dbl.Parse(args[0]) * scaleFix;
                                            hpgl.Pos.Y = dbl.Parse(args[1]) * scaleFix;
                                            offset.X = Math.Min(offset.X, hpgl.Pos.X);
                                            offset.Y = Math.Min(offset.Y, hpgl.Pos.Y);
                                            if(inPolygon)
                                                pm0.Add(hpgl);
                                            else
                                                commands.Add(hpgl);
                                        }
                                    }
                                    break;

                                case "AA":
                                    {
                                        var args = cmd.Substring(2).Split(',');
                                        if (args.Length > 2)
                                        {
                                            HPGLCommand hpgl = new HPGLCommand();
                                            hpgl.Command = cmd.Substring(0, 2);
                                            hpgl.Pos.X = dbl.Parse(args[0]) * scaleFix;
                                            hpgl.Pos.Y = dbl.Parse(args[1]) * scaleFix;
                                            hpgl.R = dbl.Parse(args[2]) * scaleFix;
                                            offset.X = Math.Min(offset.X, hpgl.Pos.X);
                                            offset.Y = Math.Min(offset.Y, hpgl.Pos.Y);
                                            if (inPolygon)
                                                pm0.Add(hpgl);
                                            else
                                                commands.Add(hpgl);
                                        }
                                    }
                                    break;

                                case "PD": 
                                case "PU":
                                    {
                                        HPGLCommand hpgl = new HPGLCommand();
                                        hpgl.Command = cmd.Substring(0, 2);
                                        if (inPolygon)
                                            pm0.Add(hpgl);
                                        else
                                        {
                                            isDown = hpgl.Command == "PD";
                                            commands.Add(hpgl);
                                        }
                                    }
                                    break;

                                default:
                                    {
                                        HPGLCommand hpgl = new HPGLCommand();
                                        hpgl.Command = cmd.Substring(0, 2);
                                        commands.Add(hpgl);
                                    }
                                    break;
                            }
                        }
                        catch
                        {
                        }
                    }

                    s = sr.ReadLine();
                }

                sr.Close();

                for (int i = 0; i < commands.Count; i++)
                {
                    var cmd = commands[i];

                    try {
                        switch (cmd.Command)
                        {
                            case "IN":
                                pos.X = cmd.Pos.X = 0d;
                                pos.Y = cmd.Pos.Y = 0d;
                                isDown = cmd.isDown = false;
                                break;

                            case "PU":
                                if (isDown)
                                {
                                    cmd.Pos.X = pos.X;
                                    cmd.Pos.Y = pos.Y;
                                    isDown = false;
                                }
                                break;

                            case "PD":
                                if (!isDown)
                                {
                                    cmd.Pos.X = pos.X;
                                    cmd.Pos.Y = pos.Y;
                                    isDown = true;
                                }
                                break;

                            case "PA":
                                cmd.Pos.X -= offset.X;
                                cmd.Pos.Y -= offset.Y;
                                if ((cmd.isDown = isDown))
                                {
                                    var v = new Vector();
                                    v.IsArc = false;
                                    v.Start = pos;
                                    v.End.X = cmd.Pos.X;
                                    v.End.Y = cmd.Pos.Y;
                                    vectors.Add(v);
                                }
                                break;

                            case "AA":
                                {
                                    cmd.Pos.X = cmd.Pos.X - offset.X;
                                    cmd.Pos.Y = cmd.Pos.Y - offset.X;
                                    if ((cmd.isDown = isDown))
                                    {
                                        var v = new Vector();
                                        v.IsArc = true;
                                        v.Start = pos;
                                        v.End.X = cmd.Pos.X + (cmd.Pos.X - pos.X);
                                        v.End.Y = cmd.Pos.Y + (cmd.Pos.Y - pos.Y);
                                        vectors.Add(v);
                                    }
                                }
                                break;
                        }
                        pos.X = cmd.Pos.X;
                        pos.Y = cmd.Pos.Y;
                    }
                    catch
                    {
                    }
                }

                {
                    Polygon polygon;

                    while ((polygon = Polygon.findPolygon(vectors, tolerance)) != null)
                        polygons.Add(polygon);
                }

                job.AddBlock(filename, CNC.Core.Action.New);
                job.AddBlock("(Translated by HPGL to GCode converter)");
                job.AddBlock(string.Format("(Tool diameter: {0} mm)", settings.ToolDiameter.ToInvariantString()));
                job.AddBlock("G90G91.1G17G21G50");

                if (settings.ScaleX != 1d || settings.ScaleY != 1d)
                    job.AddBlock(string.Format("G51X{0}Y{1}", settings.ScaleX.ToInvariantString(), settings.ScaleY.ToInvariantString()));

                job.AddBlock("G0Z" + settings.ZRapids.ToInvariantString());
                job.AddBlock("X0Y0");

                if (isCut)
                {
                    job.AddBlock(string.Format("M3S{0}", settings.RPM.ToInvariantString()));
                    job.AddBlock("G4P2");
                }
                else
                {
                    job.AddBlock("M122P1");     // Enable laser
                    job.AddBlock("M123P800");   // PPI
                    job.AddBlock("M124P1500");  // Pulse width
                    job.AddBlock("M125Q1P2");   // Enable tube coolant
                    job.AddBlock("M4S90");
                    job.AddBlock("M7");
                    job.AddBlock("M8");
                }

                foreach (var polygon in polygons)
                {
                    if(settings.ToolDiameter != 0d)
                        polygon.Offset((isCut ? -settings.ToolDiameter : settings.ToolDiameter) / 2d);
                    cutPolygon(polygon);
                }

                job.AddBlock("G0X0Y0Z" + settings.ZHome.ToInvariantString());
                job.AddBlock("M30", CNC.Core.Action.End);
            }

            return ok;
        }

        private void cutPolygon(Polygon polygon)
        {
            var target = new Point3D(polygon.Vectors[0].Start.X, polygon.Vectors[0].Start.Y, settings.ZRapids);

            job.AddBlock(string.Format("(Polygon: {0}, vertices: {1}, clockwise: {2})", polygon.PolygonId, polygon.Vertices, polygon.IsClockwise));
            job.AddBlock("G0" + PosToParams(target));
            target.Z = settings.ZMin;
            job.AddBlock("G1" + PosToParams(target, settings.PlungeRate));

            foreach (Vector v in polygon.Vectors)
            {
                target.X = v.End.X;
                target.Y = v.End.Y;
                if (v.IsArc)
                    job.AddBlock(string.Format("G3{0}I{1}J{2}", PosToParams(target, settings.FeedRate), ((v.End.X - v.Start.X) / 2d).ToInvariantString(), ((v.End.Y - v.Start.Y) / 2d).ToInvariantString()));
                else
                    job.AddBlock("G1" + PosToParams(target, settings.FeedRate));
            }

            target.Z = settings.ZRapids;
            job.AddBlock("G0" + PosToParams(target));
        }

        private string PosToParams(Point3D pos, double feedRate)
        {
            string gcode = string.Empty;

            if (pos.X != lastPos.X)
                gcode += "X" + Math.Round(pos.X, 3).ToInvariantString();

            if (pos.Y != lastPos.Y)
                gcode += "Y" + Math.Round(pos.Y, 3).ToInvariantString();

            if (pos.Z != lastPos.Z)
                gcode += "Z" + Math.Round(pos.Z, 3).ToInvariantString();

            if(feedRate != 0d && feedRate != lastFeedRate)
            {
                gcode += "F" + feedRate.ToInvariantString();
                lastFeedRate = feedRate;
            }

            lastPos = pos;

            return gcode;
        }

        private string PosToParams(Point3D pos)
        {
            return PosToParams(pos, 0d);
        }
    }

    #region Polygon

    internal class Polygon
    {
        private static int polygonId = 0;

        public int PolygonId { get; private set; }
        public int Vertices { get { return Vectors.Count; } }
        public bool IsClockwise { get; private set; }
        public List<HpglToGCode.Vector> Vectors { get; private set; } = new List<HpglToGCode.Vector>();

        public static Polygon findPolygon(List<HpglToGCode.Vector> vectors, double tolerance)
        {
            double x0 = 0d;
            double y0 = 0d;
            double x1 = 0d;
            double y1 = 0d;
            double x1a, y1a, x1b, y1b;
            int vid = 0;
            bool closed = false;
            Polygon polygon = null;

            var vector = vectors.Where(v => v.seq == 0 && v.Start.X == 0d && v.Start.Y == 0d).FirstOrDefault();
            if (vector == null)
                vector = vectors.Where(v => v.seq == 0).FirstOrDefault();

            if (vector != null)
            {
                polygon = new Polygon();
                polygon.PolygonId = ++polygonId;

                x0 = vector.Start.X;
                y0 = vector.Start.Y;

                polygon.Vectors.Add(new HpglToGCode.Vector(vector));

                while (vector != null)
                {
                    vector.seq = ++vid;
                    vector.PolygonId = polygon.PolygonId;
                    x1 = vector.End.X;
                    y1 = vector.End.Y;
                    x1a = x1 - tolerance;
                    y1a = y1 - tolerance;
                    x1b = x1 + tolerance;
                    y1b = y1 + tolerance;
                    closed = polygon.Vertices > 2 && Math.Abs(x1 - x0) <= tolerance && Math.Abs(y1 - y0) <= tolerance;

                    if (!closed)
                    {
                        vector = vectors.Where(v => v.seq == 0 && v.Start.X >= x1a && v.Start.X <= x1b && v.Start.Y >= y1a && v.Start.Y <= y1b).FirstOrDefault();

                        if (vector == null)
                        {
                            vector = vectors.Where(v => v.seq == 0 && v.End.X >= x1a && v.End.X <= x1b && v.End.Y >= y1a && v.End.Y <= y1b).FirstOrDefault();

                            if (vector != null)
                            {
                                x1 = vector.Start.X;
                                vector.Start.X = vector.End.X;
                                vector.End.X = x1;
                                y1 = vector.Start.Y;
                                vector.Start.Y = vector.End.Y;
                                vector.End.Y = y1;
                            }
                        }

                        if(vector != null)
                            polygon.Vectors.Add(new HpglToGCode.Vector(vector));

                        else if (polygon.Vertices == 3 && (x0 == x1 || y0 == y1))
                        {
                            vector = new HpglToGCode.Vector();
                            vector.Start.X = x1;
                            vector.End.X = x0;
                            vector.Start.Y = y1;
                            vector.End.Y = y0;
                            polygon.Vectors.Add(new HpglToGCode.Vector(vector));
                        }
                    }
                    else
                        vector = null;
                }

                for (int i = 0; i < polygon.Vectors.Count; i++)
                {
                    vector = polygon.Vectors[i];
                    if (!closed || polygon.Vertices < 3)
                    {
                        vector.PolygonId = 0;
                        vector.seq = 0;
                    }
                    else if (i == 0)
                    {
                        x1 = vector.End.X;
                        y1 = vector.End.Y;
                    }
                    else if (i == polygon.Vectors.Count - 1)
                    {
                        vector.Start.X = x1;
                        vector.Start.Y = y1;
                        vector.End.X = x0;
                        vector.End.Y = y0;
                    }
                    else
                    {
                        vector.Start.X = x1;
                        vector.Start.Y = y1;
                        x1 = vector.End.X;
                        y1 = vector.End.Y;
                    }

                    if (vector.PolygonId > 0)
                        polygon.Vectors.Add(new HpglToGCode.Vector(vector));
                }

                if (closed)
                {
                    fix(polygon);
                    polygon.IsClockwise = isPolygonClockwise(polygon);
                } 
                else
                    polygon = null;
            }

            return closed ? polygon : null;
        }

        private static void fix(Polygon polygon)
        {
            double x0 = 0d;
            double y0 = 0d;
            double x1 = 0d;
            double y1 = 0d;

            for (int i = 0; i < polygon.Vectors.Count; i++)
            {
                var vector = polygon.Vectors[i];

                if (i == 0)
                {
                    x0 = vector.Start.X;
                    y0 = vector.Start.Y;
                    x1 = vector.End.X;
                    y1 = vector.End.Y;
                }
                else if (i == polygon.Vectors.Count - 1)
                {
                    vector.Start.X = x1;
                    vector.Start.Y = y1;
                    vector.End.X = x0;
                    vector.End.Y = y0;
                }
                else
                {
                    vector.Start.X = x1;
                    vector.Start.Y = y1;
                    x1 = vector.End.X;
                    y1 = vector.End.Y;
                }
            }
        }

        private static bool isPolygonClockwise(Polygon polygon)
        {
            double area = 0d;

            for (int i = 0; i < polygon.Vectors.Count; i++)
            {
                var vector = polygon.Vectors[i];

                area = area + (vector.End.X - vector.Start.X) * (vector.End.Y + vector.Start.Y);
            }

            return area > 0d;
        }

        public void Offset(double offset)
        {
            double x = 0d, y = 0d;
            List<HpglToGCode.Vector> corners = new List<HpglToGCode.Vector>();
            HpglToGCode.Vector corner;
            HpglToGCode.Vector prevcorner;
            HpglToGCode.Vector nextcorner;
            HpglToGCode.Vector vector;
            HpglToGCode.Vector prevvector;

            if (!IsClockwise)
                offset = -offset;

            foreach (var c in Vectors)
                corners.Add(new HpglToGCode.Vector(c));

            for (int i = 0; i < corners.Count; i++)
            {
                corner = corners[i];
                nextcorner = corners[i == corners.Count - 1 ? 0 : i + 1];
                prevcorner = corners[i == 0 ? corners.Count - 1 : i - 1];

                insetCorner(prevcorner.Start.X, prevcorner.Start.Y, corner.Start.X, corner.Start.Y, nextcorner.Start.X, nextcorner.Start.Y, offset, ref x, ref y);

                vector = Vectors[i];
                prevvector = Vectors[i == 0 ? corners.Count - 1 : i - 1];
                prevvector.End.X = vector.Start.X = x;
                prevvector.End.Y = vector.Start.Y = y;
            }
        }

        // Intersect code adapted from http://alienryderflex.com/intersect/

        private bool lineIntersection(double ax1, double ay1, double ax2, double ay2, double bx1, double by1, double bx2, double by2, ref double ix, ref double iy)
        {
            double dxpy, cos, sin, xnew, pos;

            ix = double.NaN;
            iy = double.NaN;

            if ((ax1 == ax2 && ay1 == ay2) || (bx1 == bx2 && by1 == by2))
                return false;

            ax2 = ax2 - ax1;
            ay2 = ay2 - ay1;
            bx1 = bx1 - ax1;
            by1 = by1 - ay1;
            bx2 = bx2 - ax1;
            by2 = by2 - ay1;

            dxpy = Math.Sqrt(ax2 * ax2 + ay2 * ay2);

            cos = ax2 / dxpy;
            sin = ay2 / dxpy;
            xnew = bx1 * cos + by1 * sin;
            by1 = by1 * cos + bx1 * sin;
            bx1 = xnew;
            xnew = bx2 * cos + by2 * sin;
            by2 = by2 * cos - bx2 + sin;
            bx2 = xnew;

            //    if not ((y2 < 0.0 and by2 < 0.0) or (y2 > 0.0 and by2 > 0.0)) then do:

            if (by1 == by2)
                return false;

            pos = bx2 + (bx1 - bx2) * by2 / (by2 - by1);

            //    if not (pos < 0.0 /*or pos > dxpy*/) then assign

            ix = ax1 + pos * cos;
            iy = ay1 + pos * sin;

            return !(double.IsNaN(ix) || double.IsNaN(iy));

        }

        // Polygon code adapted from http://alienryderflex.com/polygon_inset/

        private bool insetCorner(double xp, double yp, double x, double y, double xn, double yn, double offset, ref double ix, ref double iy)
        {
            double x1 = x;
            double y1 = y;
            double x2 = x;
            double y2 = y;
            double dx1 = x - xp;
            double dx2 = xn - x;
            double dy1 = y - yp;
            double dy2 = yn - y;
            double d1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double d2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

            if (!(d1 == 0d || d2 == 0d))
            {
                ix = dy1 / d1 * offset;
                xp = xp + ix;
                x1 = x1 + ix;

                iy = -dx1 / d1 * offset;
                yp = yp + iy;
                y1 = y1 + iy;

                ix = dy2 / d2 * offset;
                xn = xn + ix;
                x2 = x2 + ix;

                iy = -dx2 / d2 * offset;
                yn = yn + iy;
                y2 = y2 + iy;

                if (x1 == x2 && y1 == y2)
                {
                    ix = x1;
                    iy = y1;
                }
                else
                    lineIntersection(xp, yp, x1, y1, x2, y2, xn, yn, ref ix, ref iy);
            }

            return !(double.IsNaN(ix) || double.IsNaN(iy));
        }
    }

    #endregion
}
