/*
 * Renderer.xaml.cs - part of CNC Controls library
 *
 * v0.02 / 2020-01-27 / Io Engineering (Terje Io)
 *
 */

/* Some parts ported and/or inspired from code at:
  https://www.codeproject.com/Articles/1246255/Plotting-a-Real-time-D-Toolpath-with-Helix-Toolkit
  https://github.com/winder/Universal-G-Code-Sender
  https://github.com/Denvi/Candle
  https://github.com/grbl/grbl (seems to be the origin of arc plotting functions?)
*/

/*

Copyright (c) 2019, Io Engineering (Terje Io)
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CNC.Core;
using CNC.GCode;
using System.Collections.ObjectModel;
using System.Linq;

namespace CNC.Controls.Viewer
{

    public static class ex3d
    {
        public static double[] ToArray(this Point3D point)
        {
            double[] values = new double[3];

            values[0] = point.X;
            values[1] = point.Y;
            values[2] = point.Z;

            return values;
        }
    }

    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Renderer : UserControl
    {
        private Point3D point0;  // last point
        private Vector3D delta0;  // (dx,dy,dz)
        private List<LinesVisual3D> trace;
        private List<LinesVisual3D> position = new List<LinesVisual3D>();
        private LinesVisual3D path;
        private double minDistanceSquared;

        bool isRelative = false;
        double[] offsets = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };

        public SolidColorBrush AxisBrush { get; set; }
        public double TickSize { get; set; }
        private GCPlane plane = new GCPlane(Commands.G17, 0);
        DistanceMode distanceMode = DistanceMode.Absolute;
        private List<CoordinateSystem> coordinateSystems = new List<CoordinateSystem>();
        private CoordinateSystem coordinateSystem;

        public Renderer()
        {
            InitializeComponent();

            minDistanceSquared = MinDistance * MinDistance;
            AxisBrush = Brushes.Gray;
            TickSize = 10;
        }

        public int ArcResolution { get; set; } = 5;
        public double MinDistance { get; set; } = 0.05;
        public bool ShowGrid { get; set; } = true;
        public bool ShowAxes { get; set; } = true;
        public bool ShowBoundingBox { get; set; } = true;

        public void ClearViewport()
        {
            viewport.Children.Clear();
        }

        public void ShowPosition()
        {
            GrblViewModel model = (GrblViewModel)DataContext;

            foreach (var path in position)
                viewport.Children.Remove(path);

            position.Clear();

            double maxX = GrblSettings.GetDouble(GrblSetting.AxisSetting_XMaxTravel);
            double maxY = GrblSettings.GetDouble(GrblSetting.AxisSetting_YMaxTravel);
            double maxZ = GrblSettings.GetDouble(GrblSetting.AxisSetting_ZMaxTravel);

            var positionX = new LinesVisual3D();
            positionX.Color = Colors.Green;
            positionX.Thickness = 1;
            positionX.Points.Add(new Point3D(model.ProgramLimits.MinX - 5d, model.Position.Y, model.Position.Z));
            positionX.Points.Add(new Point3D(maxX, model.Position.Y, model.Position.Z));
            position.Add(positionX);

            var positionY = new LinesVisual3D();
            positionY.Color = Colors.Green;
            positionY.Thickness = 1;
            positionY.Points.Add(new Point3D(model.Position.X, model.ProgramLimits.MinY - 5d, model.Position.Z));
            positionY.Points.Add(new Point3D(model.Position.X, maxY, model.Position.Z));
            position.Add(positionY);

            var positionZ = new LinesVisual3D();
            positionZ.Color = Colors.Green;
            positionZ.Thickness = 1;
            positionZ.Points.Add(new Point3D(model.Position.X, model.Position.Y, model.ProgramLimits.MinZ - 5d));
            positionZ.Points.Add(new Point3D(model.Position.X, model.Position.Y, maxZ));
            position.Add(positionZ);

            foreach (var path in position)
                viewport.Children.Add(path);
        }

        public void Render(List<GCodeToken> tokens)
        {
            var bbox = ((GrblViewModel)DataContext).ProgramLimits;

            double lineThickness = bbox.MaxSize / 1000;
            double arrowOffset = lineThickness * 30;
            double labelOffset = lineThickness * 50;
            bool canned = false;

            //trace.Clear();
            trace = null;
            viewport.Children.Clear();

            coordinateSystems.Clear();

            distanceMode = GrblParserState.DistanceMode;

            foreach (CoordinateSystem c in GrblWorkParameters.CoordinateSystems)
                coordinateSystems.Add(c);

            coordinateSystem = coordinateSystems.Where(x => x.Code == GrblParserState.WorkOffset).FirstOrDefault();

            #region Canvas adorners

            if (ShowGrid)
            {
                viewport.Children.Add(new GridLinesVisual3D()
                {
                    Center = new Point3D(bbox.SizeX / 2d - TickSize - bbox.MinX / 2d, bbox.SizeY / 2d - TickSize - bbox.MinY / 2d, 0.0),
                    MinorDistance = TickSize,
                    MajorDistance = bbox.MaxSize,
                    Width = bbox.SizeY + TickSize * 2d,
                    Length = bbox.SizeX + TickSize * 2d,
                    Thickness = lineThickness,
                    Fill = AxisBrush
                });
            }

            if (ShowAxes)
            {
                viewport.Children.Add(new ArrowVisual3D() {
                    Point2 = new Point3D(bbox.SizeX + arrowOffset, 0.0, 0.0),
                    Diameter = lineThickness * 5,
                    Fill = AxisBrush
                });

                viewport.Children.Add(new BillboardTextVisual3D() {
                    Text = "X",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(bbox.SizeX + labelOffset, 0.0, 0.0)
                });

                viewport.Children.Add(new ArrowVisual3D() {
                    Point2 = new Point3D(0.0, bbox.SizeY + arrowOffset, 0.0),
                    Diameter = lineThickness * 5,
                    Fill = AxisBrush
                });

                viewport.Children.Add(new BillboardTextVisual3D()
                {
                    Text = "Y",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(0.0, bbox.SizeY + labelOffset, 0.0)
                });

                if (bbox.SizeZ > 0d)
                {
                    viewport.Children.Add(new ArrowVisual3D() {
                        Point1 = new Point3D(0.0, 0.0, bbox.MinZ),
                        Point2 = new Point3D(0.0, 0.0, bbox.MaxZ + arrowOffset),
                        Diameter = lineThickness * 5,
                        Fill = AxisBrush
                    });

                    viewport.Children.Add(new BillboardTextVisual3D()
                    {
                        Text = "Z",
                        FontWeight = FontWeights.Bold,
                        Foreground = AxisBrush,
                        Position = new Point3D(0.0, 0.0, bbox.MaxZ + labelOffset)
                    });
                }
            }

            if (ShowBoundingBox && bbox.SizeZ > 0d)
            {
                viewport.Children.Add(new BoundingBoxWireFrameVisual3D()
                {
                    BoundingBox = new Rect3D(bbox.MinX, bbox.MinY, bbox.MinZ, bbox.SizeX, bbox.SizeY, bbox.SizeZ),
                    Thickness = 1d,
                    Color = Colors.Gray
                });
            }

            #endregion

            GCodeToken last = new GCodeToken();

            foreach (GCodeToken token in tokens)
            {
                switch (token.Command)
                {
                    case Commands.G0:
                        {
                            GCLinearMotion motion = (GCLinearMotion)token;
                            var pt = toPoint(motion.Values);
                            if (distanceMode == DistanceMode.Incremental)
                                pt.Offset(point0.X, point0.Y, point0.Z);
                            if (last.Command == Commands.G1 && (((GCLinearMotion)last).X != point0.X || ((GCLinearMotion)last).Y != point0.Y))
                                path.Points.Add(pt);
                            AddPoint(pt, Colors.Red, 0.5);
                        }
                        break;

                    case Commands.G1:
                        {
                            GCLinearMotion motion = (GCLinearMotion)token;
                            var pt = toPoint(motion.Values);
                            if (distanceMode == DistanceMode.Incremental)
                                pt.Offset(point0.X, point0.Y, point0.Z);
                            if (last.Command == Commands.G0 && (((GCLinearMotion)last).X != point0.X || ((GCLinearMotion)last).Y != point0.Y))
                                path.Points.Add(pt);
                            AddPoint(point0, Colors.Blue, 1);
                            AddPoint(pt, Colors.Blue, 1);
                        }
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        GCArc arc = (GCArc)token;
                        if (distanceMode == DistanceMode.Incremental)
                        {
                            arc.X += point0.X;
                            arc.Y += point0.Y;
                            arc.Z += point0.Z;
                        }
                        if (arc.IsRadiusMode)
                            DrawArc(plane, point0.ToArray(), arc.Values, arc.R, arc.IsClocwise);
                        else
                            DrawArc(plane, point0.ToArray(), arc.Values, arc.IJKvalues, arc.IJKMode == IJKMode.Absolute, arc.IsClocwise);
                        break;

                    case Commands.G10:
                    case Commands.G92:
                        {
                            if (token is GCCoordinateSystem)
                            {
                                CoordinateSystem csys;
                                GCCoordinateSystem gcsys = (GCCoordinateSystem)token;
                                if (gcsys.P == 0)
                                    csys = coordinateSystem;
                                else
                                    csys = coordinateSystems.Where(x => x.Code == gcsys.Code).FirstOrDefault();
                                for (int i = 0; i < 3; i++)
                                {
                                    csys.Values[i] = gcsys.Values[i];
                                    if (gcsys.P == 0)
                                        offsets[i] = coordinateSystem.Values[i];
                                }
                            }
                        }
                        break;

                    case Commands.G17:
                    case Commands.G18:
                    case Commands.G19:
                        plane = (GCPlane)token;
                        break;

                    //case Commands.G20:
                    //case Commands.G21:
                    //case Commands.G50:
                    //case Commands.G51:
                    //    !! Scaling is taken care of in the parser
                    //    break;

                    case Commands.G28_1:
                    case Commands.G30_1:
                    case Commands.G54:
                    case Commands.G55:
                    case Commands.G56:
                    case Commands.G57:
                    case Commands.G58:
                    case Commands.G59:
                    case Commands.G59_1:
                    case Commands.G59_2:
                    case Commands.G59_3:
                    case Commands.G92_1:
                        {
                            string cs = token.Command.ToString().Replace('_', '.');
                            coordinateSystem = coordinateSystems.Where(x => x.Code == cs).FirstOrDefault();
                            for (int i = 0; i < 3; i++)
                                offsets[i] = coordinateSystem.Values[i];
                            //    CoordinateSystem = GrblWorkParameters.CoordinateSystems();
                            //GCCoordinateSystem cs = (GCCoordinateSystem)token;
                            // TODO: handle offsets... Need to read current from grbl
                        }
                        break;

                    case Commands.G80:
                        canned = false;
                        break;

                    case Commands.G81: // TODO: add plane handling
                        {
                            GCCannedDrill drill = (GCCannedDrill)token;
                            uint repeats = distanceMode == DistanceMode.Incremental ? drill.L : 1; // no need to draw absolute repeats(?)
                            double[] values = new double[3];

                            for (var i = 0; i < values.Length; i++)
                                values[i] = distanceMode == DistanceMode.Incremental && i < 2 ? 0d : drill.Values[i];

                            if (!canned)
                            {
                                canned = true;
                                if (point0.Z < drill.R)
                                    AddPoint(toPoint(point0.X, point0.Y, drill.R), Colors.Red, 1);
                            }

                            AddPoint(toPoint(drill.X, drill.Y, Math.Max(drill.Z, drill.R)), Colors.Red, 1);

                            do
                            {
                                AddPoint(toPoint(values), Colors.Blue, 3d);
                                AddPoint(toPoint(values[0], values[1], drill.R), Colors.Green, 1);
                                if(repeats > 1)
                                {
                                    AddPoint(toPoint(values[0], values[1], drill.R), Colors.Red, 1);
                                    values[0] += drill.X;
                                    values[1] += drill.Y;
                                    AddPoint(toPoint(values[0], values[1], drill.R), Colors.Red, 1);
                                }
                            } while (--repeats > 0);
                        }
                        break;

                    case Commands.G90:
                    case Commands.G91:
                        distanceMode = ((GCDistanceMode)token).DistanceMode;
                        break;
                }
                last = token;
            }
            last = null;

            if(trace != null) foreach (var path in trace)
                viewport.Children.Add(path);

            refreshCamera(bbox);

        }
        public void refreshCamera(ProgramLimits bbox)
        {
            double zpos = Math.Max(bbox.SizeX, bbox.SizeY) / Math.Tan(60 * Math.PI / 360d);

            // TODO: set a sensible viewing distance dynamically
            var position = new Point3D(Math.Max(10d, bbox.SizeX) / 2d, Math.Max(10d, bbox.SizeY) / 2d, zpos);

            viewport.Camera.Position = position;
            viewport.DefaultCamera.Position = position;
            //                viewport.CameraController.AddRotateForce(0.001, 0.001); // emulate move camera 
        }

        //private void DrawLine(LinesVisual3D lines, double x_start, double y_start, double z_start, double x_stop, double y_stop, double z_stop)
        //{
        //    lines.Points.Add(new Point3D(x_start, y_start, z_start));
        //    lines.Points.Add(new Point3D(x_stop, y_stop, z_stop));
        //}

        //private void DrawLine(LinesVisual3D lines, Point3D start, Point3D end)
        //{
        //    lines.Points.Add(start);
        //    lines.Points.Add(end);
        //}

        private Point3D toPoint(double[] values)
        {
            Point3D p = new Point3D(values[0], values[1], values[2]);

            if (isRelative)
                p.Offset(point0.X, point0.Y, point0.Z);

            return p;
        }
        private Point3D toPoint(double X, double Y, double Z)
        {
            Point3D p = new Point3D(X, Y, Z);

            if (isRelative)
                p.Offset(point0.X, point0.Y, point0.Z);

            return p;
        }

        public void NewTrace(Point3D point, Color color, double thickness = 1)
        {
            path = new LinesVisual3D();
            path.Color = color;
            //       path.Points.Add(point);
            path.Thickness = thickness;
            trace = new List<LinesVisual3D>();
            trace.Add(path);
            //    viewport.Children.Add(path);
            point0 = point;
            delta0 = new Vector3D();

            /*
            if (marker != null)
            {
                marker.Origin = point;
                coords.Position = new Point3D(point.X - labelOffset, point.Y - labelOffset, point.Z + labelOffset);
                coords.Text = string.Format(coordinateFormat, point.X, point.Y, point.Z);
            } */
        }
        public void NewTrace(double x, double y, double z, Color color, double thickness = 1)
        {
            NewTrace(new Point3D(x, y, z), color, thickness);
        }

        public void AddPoint(Point3D point, Color color, double thickness = -1)
        {
            if (trace == null)
            {
                NewTrace(point, color, (thickness > 0) ? thickness : 1);
                return;
            }


            bool sameDir = false;

            if (path.Color != color || (thickness > 0.0 && path.Thickness != thickness))
            {
                if (thickness <= 0.0)
                    thickness = path.Thickness;

                path = new LinesVisual3D();
                path.Color = color;
                path.Thickness = thickness;

                trace.Add(path);
                //  viewport.Children.Add(path);
            }
            else
            {

                // If line segments AB and BC have the same direction (small cross product) then remove point B.

                var delta = new Vector3D(point.X - point0.X, point.Y - point0.Y, point.Z - point0.Z);
                delta.Normalize();  // use unit vectors (magnitude 1) for the cross product calculations
                Vector3D cp; double xp2;
                if (path.Points.Count > (trace.Count == 1 ? 1 : 0))
                {
                    cp = Vector3D.CrossProduct(delta, delta0);
                    xp2 = cp.LengthSquared;
                    sameDir = xp2 > 0d && (xp2 < 0.0005d);  // approx 0.001 seems to be a reasonable threshold from logging xp2 values
                                                            //if (!sameDir) Title = string.Format("xp2={0:F6}", xp2);
                }

                if (sameDir)  // extend the current line segment
                {
                    path.Points[path.Points.Count - 1] = point;
                    delta0 += delta;
                }
                else
                {
                    if ((point - point0).LengthSquared < minDistanceSquared)
                        return;  // less than min distance from last point
                    delta0 = delta;
                }
            }

            if (!sameDir)
            {
                path.Points.Add(point0);
                path.Points.Add(point);
            }

            point0 = point;

            //if (marker != null)
            //{
            //    marker.Origin = point;
            //    coords.Position = new Point3D(point.X - labelOffset, point.Y - labelOffset, point.Z + labelOffset);
            //    coords.Text = string.Format(coordinateFormat, point.X, point.Y, point.Z);
            //}
        }

        //-------------

        private void DrawArc(GCPlane plane, double[] start, double[] stop, double radius, bool clockwise)
        {
            double[] center = convertRToCenter(plane, start, stop, radius, false, clockwise);
            List<Point3D> arcpoints = generatePointsAlongArcBDring(plane, start, stop, center, clockwise, 0, ArcResolution); // Dynamic resolution

            Point3D old_point = arcpoints[0];
            arcpoints.RemoveAt(0);

            AddPoint(old_point, Colors.Blue);

            foreach (Point3D point in arcpoints)
                AddPoint(point, Colors.Blue);
        }

        private void DrawArc(GCPlane plane, double[] start, double[] stop, double[] ijkValues, bool absoluteIJKMode, bool clockwise)
        {
            double[] center = updateCenterWithCommand(plane, start, ijkValues, absoluteIJKMode);

            List<Point3D> arcpoints = generatePointsAlongArcBDring(plane, start, stop, center, clockwise, 0d, ArcResolution); // Dynamic resolution

            foreach (Point3D point in arcpoints)
                AddPoint(point, Colors.Blue);
        }

        /**
        * Generates the points along an arc including the start and end points.
        */
        public static List<Point3D> generatePointsAlongArcBDring(GCPlane plane, double[] p1, double[] p2, double[] center, bool isCw, double radius, int arcResolution)
        {
            double sweep;

            // Calculate radius if necessary.
            if (radius == 0d)
                radius = Hypotenuse(p1[plane.Axis0] - center[0], p1[plane.Axis1] - center[1]);

            // Calculate angles from center.
            double startAngle = getAngle(center, p1[plane.Axis0], p1[plane.Axis1]);
            double endAngle = getAngle(center, p2[plane.Axis0], p2[plane.Axis1]);

            if (startAngle == endAngle)
                sweep = Math.PI * 2d;

            else
            {
                // Fix semantics, if the angle ends at 0 it really should end at 360.
                if (endAngle == 0d)
                    endAngle = Math.PI * 2d;

                // Calculate distance along arc.
                if (!isCw && endAngle < startAngle)
                    sweep = ((Math.PI * 2d - startAngle) + endAngle);
                else if (isCw && endAngle > startAngle)
                    sweep = ((Math.PI * 2d - endAngle) + startAngle);
                else
                    sweep = Math.Abs(endAngle - startAngle);
            }

            arcResolution = (int)Math.Max(1d, (sweep / (Math.PI * 18d / 180d)));

         //   arcResolution = (int)Math.Ceiling((sweep * radius) / .1d);

            //if (arcDegreeMode && arcPrecision > 0)
            //{
            //    numPoints = qMax(1.0, sweep / (M_PI * arcPrecision / 180));
            //}
            //else
            //{
            //    if (arcPrecision <= 0 && minArcLength > 0)
            //    {
            //        arcPrecision = minArcLength;
            //    }
            //    numPoints = (int)ceil(arcLength / arcPrecision);
            //}

            return generatePointsAlongArcBDring(plane, p1, p2, center, isCw, radius, startAngle, sweep, arcResolution);
        }

        /*
         * Generates the points along an arc including the start and end points.
         */
        public static List<Point3D> generatePointsAlongArcBDring(GCPlane plane, double[] p1,
                double[] p2, double[] center, bool isCw, double radius,
                double startAngle, double sweep, int numPoints)
        {

            Point3D lineEnd = new Point3D();
            List<Point3D> segments = new List<Point3D>();
            double angle;
            double zIncrement = (p2[plane.AxisLinear] - p1[plane.AxisLinear]) / numPoints;

            for (int i = 0; i < numPoints; i++)
            {
                if (isCw)
                    angle = (startAngle - i * sweep / numPoints);
                else
                    angle = (startAngle + i * sweep / numPoints);

                if (angle >= Math.PI * 2d)
                    angle = angle - Math.PI * 2d;

                p1[plane.Axis0] = Math.Cos(angle) * radius + center[0];
                p1[plane.Axis1] = Math.Sin(angle) * radius + center[1];

                lineEnd.X = p1[0];
                lineEnd.Y = p1[1];
                lineEnd.Z = p1[2];

                p1[plane.AxisLinear] += zIncrement;

                segments.Add(lineEnd);
            }

            lineEnd.X = p2[0];
            lineEnd.Y = p2[1];
            lineEnd.Z = p2[2];

            segments.Add(lineEnd);

            return segments;
        }

        /** 
        * Return the angle in radians when going from start to end.
        */
        public static double getAngle(double[] start, double endX, double endY)
        {
            double deltaX = endX - start[0];
            double deltaY = endY - start[1];

            double angle = 0d;

            if (deltaX != 0d)
            { // prevent div by 0
                // it helps to know what quadrant you are in
                if (deltaX > 0d && deltaY >= 0d)
                {  // 0 - 90
                    angle = Math.Atan(deltaY / deltaX);
                }
                else if (deltaX < 0d && deltaY >= 0d)
                { // 90 to 180
                    angle = Math.PI - Math.Abs(Math.Atan(deltaY / deltaX));
                }
                else if (deltaX < 0d && deltaY < 0d)
                { // 180 - 270
                    angle = Math.PI + Math.Abs(Math.Atan(deltaY / deltaX));
                }
                else if (deltaX > 0d && deltaY < 0d)
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

        static public double[] updateCenterWithCommand(GCPlane plane, double[] initial, double[] ijkValues, bool absoluteIJKMode)
        {
            double[] newPoint = new double[2];

            if (absoluteIJKMode)
            {
                newPoint[0] = ijkValues[plane.Axis0];
                newPoint[1] = ijkValues[plane.Axis1];
            }
            else
            {
                newPoint[0] = initial[plane.Axis0] + ijkValues[plane.Axis0];
                newPoint[1] = initial[plane.Axis1] + ijkValues[plane.Axis1];
            }

            return newPoint;
        }

        public static double Hypotenuse(double a, double b)
        {
            return Math.Sqrt(a * a + b * b);
        }

        // Try to create an arc :)
        public static double[] convertRToCenter(GCPlane plane, double[] start, double[] end, double radius, bool absoluteIJK, bool clockwise)
        {
            double[] center = new double[2];

            // This math is copied from GRBL in gcode.c
            double x = end[plane.Axis0] - start[plane.Axis0];
            double y = end[plane.Axis1] - start[plane.Axis1];

            double h_x2_div_d = 4d * radius * radius - x * x - y * y;
            if (h_x2_div_d < 0d)
            {
                Console.Write("Error computing arc radius.");
            }

            h_x2_div_d = (-Math.Sqrt(h_x2_div_d)) / Hypotenuse(x, y);

            if (!clockwise)
            {
                h_x2_div_d = -h_x2_div_d;
            }

            // Special message from gcoder to software for which radius
            // should be used.
            if (radius < 0d)
            {
                h_x2_div_d = -h_x2_div_d;
                // TODO: Places that use this need to run ABS on radius.
                radius = -radius;
            }

            double offsetX = 0.5d * (x - (y * h_x2_div_d));
            double offsetY = 0.5d * (y + (x * h_x2_div_d));

            if (!absoluteIJK)
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
    }
}
