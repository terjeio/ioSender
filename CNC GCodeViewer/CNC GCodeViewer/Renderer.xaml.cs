/*
 * Renderer.xaml.cs - part of CNC Controls library
 *
 * v0.11 / 2019-03-09 / Io Engineering (Terje Io)
 *
 */

/* Some parts ported and/or inspired from code at:
  https://www.codeproject.com/Articles/1246255/Plotting-a-Real-time-D-Toolpath-with-Helix-Toolkit
  https://github.com/winder/Universal-G-Code-Sender
  https://github.com/Denvi/Candle
  https://github.com/grbl/grbl (seems to be the origin of arc plotting functions?)
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CNC.Core;
using CNC.GCode;
using System.Linq;

namespace CNC.Controls.Viewer
{
    public enum MoveType
    {
        None,
        Cut,
        Rapid,
        Retract
    }

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

    public class Machine : ViewModelBase
    {
        GridLinesVisual3D _grid;
        BoundingBoxWireFrameVisual3D _bbox;
        ModelVisual3D _axes = new ModelVisual3D();
        Point3D _startposition = new Point3D();
        Point3D _limits = new Point3D();
        Point3DCollection _toolposition, _cutlines, _rapidlines, _retractlines;

        public void Clear()
        {
         //   Grid = null;
         //   BoundingBox = null;
            ToolPosition = CutLines = RapidLines = RetractLines = null;
        }

        public void SetStartPosition(double x, double y, double z)
        {
            SetStartPosition(new Point3D(x, y, z));
        }

        public void SetStartPosition(Point3D position)
        {
            if (!position.Equals(_startposition))
            {
                _startposition.X = position.X;
                _startposition.Y = position.Y;
                _startposition.Z = position.Z;
                OnPropertyChanged(nameof(StartPosition));
            }
        }

        public void SetLimits(double x, double y, double z)
        {
            SetLimits(new Point3D(x, y, z));
        }

        public void SetLimits(Point3D position)
        {
            if (!position.Equals(_limits))
            {
                _limits.X = position.X;
                _limits.Y = position.Y;
                _limits.Z = position.Z;
                OnPropertyChanged(nameof(Limits));
            }
        }

        public GridLinesVisual3D Grid { get { return _grid; } set { _grid = value; OnPropertyChanged(); } }
        public BoundingBoxWireFrameVisual3D BoundingBox { get { return _bbox; } set { _bbox = value; OnPropertyChanged(); } }
        public ModelVisual3D Axes { get { return _axes; } }
        public Point3DCollection ToolPosition { get { return _toolposition; } set { _toolposition = value; OnPropertyChanged(); } }
        public Point3DCollection CutLines { get { return _cutlines; } set { _cutlines = value; OnPropertyChanged(); } }
        public Point3DCollection RapidLines { get { return _rapidlines; } set { _rapidlines = value; OnPropertyChanged(); } }
        public Point3DCollection RetractLines { get { return _retractlines; } set { _retractlines = value; OnPropertyChanged(); } }
        public Point3D StartPosition { get { return _startposition; } }
        public Point3D Limits { get { return _limits; } }
    }

    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Renderer : UserControl
    {
        private Point3D point0;  // last point
        private Vector3D delta0;  // (dx,dy,dz)
        private double minDistanceSquared, _minDistance;

        bool isRelative = false;
        double[] offsets = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };

        public SolidColorBrush AxisBrush { get; set; }
        public double TickSize { get; set; }
        private GCPlane plane = new GCPlane(Commands.G17, 0);
        DistanceMode distanceMode = DistanceMode.Absolute;
        private List<CoordinateSystem> coordinateSystems = new List<CoordinateSystem>();
        private CoordinateSystem coordinateSystem;

        private int cutCount;
        private MoveType lastType;

        Point3DCollection linePoints = new Point3DCollection();
        Point3DCollection rapidPoints = new Point3DCollection();
        Point3DCollection retractPoints = new Point3DCollection();
        Point3DCollection positionPoints = new Point3DCollection();

        public Machine Machine { get; set; } = new Machine();

        public Renderer()
        {
            InitializeComponent();

            AxisBrush = Brushes.Gray;
            TickSize = 10;
            MinDistance = 0.05d;
            viewport.DataContext = Machine;
        }

        public int ArcResolution { get; set; } = 5;
        public double MinDistance { get { return _minDistance; } set { _minDistance = value; minDistanceSquared = _minDistance * _minDistance; } }
        public bool ShowGrid { get; set; } = true;
        public bool ShowAxes { get; set; } = true;
        public bool ShowBoundingBox { get; set; } = true;

        public void ClearViewport()
        {
            Machine.Clear();
            linePoints.Clear();
            rapidPoints.Clear();
            retractPoints.Clear();

            if (Machine.BoundingBox != null)
                viewport.Children.Remove(Machine.BoundingBox);

            if (Machine.Grid != null)
                viewport.Children.Remove(Machine.Grid);

            viewport.Children.Remove(Machine.Axes);
            Machine.Axes.Children.Clear();
        }

        public void ResetView()
        {
            refreshCamera(((GrblViewModel)DataContext).ProgramLimits);
        }

        public void ShowPosition()
        {
            GrblViewModel model = (GrblViewModel)DataContext;

            Machine.ToolPosition = null;

            positionPoints.Clear();

            if (Machine.Limits.X == 0d)
                Machine.SetLimits(GrblSettings.GetDouble(GrblSetting.AxisSetting_XMaxTravel),
                                   GrblSettings.GetDouble(GrblSetting.AxisSetting_YMaxTravel),
                                    GrblSettings.GetDouble(GrblSetting.AxisSetting_ZMaxTravel));

            positionPoints.Add(new Point3D(Math.Min(model.Position.X, model.ProgramLimits.MinX) - 5d, model.Position.Y, model.Position.Z));
            positionPoints.Add(new Point3D(Machine.Limits.X, model.Position.Y, model.Position.Z));

            positionPoints.Add(new Point3D(model.Position.X, Math.Min(model.Position.Y, model.ProgramLimits.MinY) - 5d, model.Position.Z));
            positionPoints.Add(new Point3D(model.Position.X, Machine.Limits.Y, model.Position.Z));

            positionPoints.Add(new Point3D(model.Position.X, model.Position.Y, Math.Min(model.Position.Z, model.ProgramLimits.MinZ) - 5d));
            positionPoints.Add(new Point3D(model.Position.X, model.Position.Y, Machine.Limits.Z));

            Machine.ToolPosition = positionPoints;
            var orgpos = Machine.StartPosition;
            Machine.SetStartPosition(model.Position.X, model.Position.Y, model.Position.Z);

            if (Machine.RapidLines != null && Machine.RapidLines.Count > 0 && Machine.RapidLines[0].Equals(orgpos))
            {
                Machine.RapidLines.RemoveAt(0);
                Machine.RapidLines.Insert(0, Machine.StartPosition);
            }
        }

        public double boffset (double w, double wh, double xs, double xm)
        {
            double v, vv = xm - Math.Floor(xm);

            v = xs / 2d + wh;

            return v;
        }

        public void Render(List<GCodeToken> tokens)
        {
            var bbox = ((GrblViewModel)DataContext).ProgramLimits;

            double lineThickness = bbox.MaxSize / 1000;
            double arrowOffset = lineThickness * 30;
            double labelOffset = lineThickness * 50;
            bool canned = false;

            ClearViewport();

            coordinateSystems.Clear();
            foreach (CoordinateSystem c in GrblWorkParameters.CoordinateSystems)
                coordinateSystems.Add(c);

            coordinateSystem = coordinateSystems.Where(x => x.Code == GrblParserState.WorkOffset).FirstOrDefault();

            cutCount = 0;
            point0 = Machine.StartPosition;
            lastType = MoveType.None;
            distanceMode = GrblParserState.DistanceMode;

            #region Canvas adorners

            if (ShowGrid)
            {
                double wm = bbox.SizeX % TickSize, w = Math.Ceiling(bbox.SizeX - bbox.SizeX % TickSize + TickSize * 2d);
                double wh = bbox.SizeY % TickSize, h = Math.Ceiling(bbox.SizeY - bbox.SizeY % TickSize + TickSize * 2d);

                Machine.Grid = new GridLinesVisual3D()
                {
                    Center = new Point3D(boffset(bbox.SizeX, bbox.MinX, w, wm) - TickSize, boffset(bbox.SizeY, bbox.MinY, h, wh) - TickSize, 0d),
                    MinorDistance = 2.5d,
                    MajorDistance = TickSize,
                    Width = h,
                    Length = w,
                    Thickness = 0.1d,
                    Fill = AxisBrush
                };

                viewport.Children.Add(Machine.Grid);
            }

            if (ShowAxes)
            {
                Machine.Axes.Children.Add(new ArrowVisual3D() {
                    Point2 = new Point3D(bbox.SizeX + arrowOffset, 0.0, 0.0),
                    Diameter = lineThickness * 5,
                    Fill = AxisBrush
                });

                Machine.Axes.Children.Add(new BillboardTextVisual3D() {
                    Text = "X",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(bbox.SizeX + labelOffset, 0.0, 0.0)
                });

                Machine.Axes.Children.Add(new ArrowVisual3D() {
                    Point2 = new Point3D(0.0, bbox.SizeY + arrowOffset, 0.0),
                    Diameter = lineThickness * 5,
                    Fill = AxisBrush
                });

                Machine.Axes.Children.Add(new BillboardTextVisual3D()
                {
                    Text = "Y",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(0.0, bbox.SizeY + labelOffset, 0.0)
                });

                if (bbox.SizeZ > 0d)
                {
                    Machine.Axes.Children.Add(new ArrowVisual3D() {
                        Point1 = new Point3D(0.0, 0.0, bbox.MinZ),
                        Point2 = new Point3D(0.0, 0.0, bbox.MaxZ + arrowOffset),
                        Diameter = lineThickness * 5,
                        Fill = AxisBrush
                    });

                    Machine.Axes.Children.Add(new BillboardTextVisual3D()
                    {
                        Text = "Z",
                        FontWeight = FontWeights.Bold,
                        Foreground = AxisBrush,
                        Position = new Point3D(0.0, 0.0, bbox.MaxZ + labelOffset)
                    });
                }

                viewport.Children.Add(Machine.Axes);
            }

            if (ShowBoundingBox && bbox.SizeZ > 0d)
            {
                Machine.BoundingBox = new BoundingBoxWireFrameVisual3D()
                {
                    BoundingBox = new Rect3D(bbox.MinX, bbox.MinY, bbox.MinZ, bbox.SizeX, bbox.SizeY, bbox.SizeZ),
                    Thickness = 1d,
                    Color = Colors.LightGreen
                };

                viewport.Children.Add(Machine.BoundingBox);
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
                            //if (last.Command == Commands.G1 && (((GCLinearMotion)last).X != point0.X || ((GCLinearMotion)last).Y != point0.Y))
                            //    path.Points.Add(pt);
                            AddRapidMove(pt);
                        }
                        break;

                    case Commands.G1:
                        {
                            GCLinearMotion motion = (GCLinearMotion)token;
                            var pt = toPoint(motion.Values);
                            if (distanceMode == DistanceMode.Incremental)
                                pt.Offset(point0.X, point0.Y, point0.Z);
                            //if (last.Command == Commands.G0 && (((GCLinearMotion)last).X != point0.X || ((GCLinearMotion)last).Y != point0.Y))
                            //    path.Points.Add(pt);
                            AddCutMove(pt);
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
                                    AddRapidMove(toPoint(point0.X, point0.Y, drill.R));
                            }

                            AddRapidMove(toPoint(drill.X, drill.Y, Math.Max(drill.Z, drill.R)));

                            do
                            {
                                AddCutMove(toPoint(values));
                                AddRetractMove(toPoint(values[0], values[1], drill.R));
                                if (repeats > 1)
                                {
                                    AddRapidMove(toPoint(values[0], values[1], drill.R));
                                    values[0] += drill.X;
                                    values[1] += drill.Y;
                                    AddRapidMove(toPoint(values[0], values[1], drill.R));
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

            Machine.RapidLines = rapidPoints;
            Machine.CutLines = linePoints;
            Machine.RetractLines = retractPoints;

            refreshCamera(bbox);
        }
        public void refreshCamera(ProgramLimits bbox)
        {
            double zpos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeY) / Math.Tan(60 * Math.PI / 360d));

            // TODO: set a sensible viewing distance dynamically

            viewport.Camera.Position = new Point3D((bbox.MaxX + bbox.MinX) / 2d, (bbox.MaxY + bbox.MinY) / 2d, zpos);
            viewport.Camera.LookDirection = new Vector3D(0, 0, -100);
            viewport.Camera.UpDirection = new Vector3D(0, 1, 0.5);

 //                viewport.CameraController.AddRotateForce(0.001, 0.001); // emulate move camera 
        }

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

        public void AddRapidMove(Point3D point)
        {
            if (cutCount > 1)
            {
                linePoints.Add(linePoints.Last());
                linePoints.Add(point);
            }

            if (lastType == MoveType.Cut)
                delta0 = new Vector3D(0d, 0d, 0d);

            rapidPoints.Add(point0);
            rapidPoints.Add(point);

            cutCount = 0;
            lastType = MoveType.Rapid;
            point0 = point;
        }

        public void AddRetractMove(Point3D point)
        {
            if (cutCount > 1)
            {
                linePoints.Add(linePoints.Last());
                linePoints.Add(point);
            }

            if (lastType == MoveType.Cut)
                delta0 = new Vector3D(0d, 0d, 0d);

            retractPoints.Add(point0);
            retractPoints.Add(point);

            cutCount = 0;
            lastType = MoveType.Retract;
            point0 = point;
        }

        public void AddCutMove(Point3D point)
        {
            bool sameDir = false;

            if (lastType == MoveType.Cut && minDistanceSquared > 0d)
            {

                // If line segments AB and BC have the same direction (small cross product) then remove point B.

                var delta = new Vector3D(point.X - point0.X, point.Y - point0.Y, point.Z - point0.Z);
                delta.Normalize();  // use unit vectors (magnitude 1) for the cross product calculations
                Vector3D cp; double xp2;
                //if (path.Points.Count > (trace.Count == 1 ? 1 : 0))
                //{
                    cp = Vector3D.CrossProduct(delta, delta0);
                    xp2 = cp.LengthSquared;
                    sameDir = xp2 > 0d && (xp2 < 0.0005d);  // approx 0.001 seems to be a reasonable threshold from logging xp2 values
                                                            //if (!sameDir) Title = string.Format("xp2={0:F6}", xp2);
                //}

                if (sameDir)  // extend the current line segment
                {
                    //var last = linePoints.Last();
                    //last.X = point.X;
                    //last.Y = point.Y;
                    //last.Z = point.Z;
                    linePoints.RemoveAt(linePoints.Count - 1);
                    linePoints.Add(point);
                    delta0 += delta;
                    cutCount++;
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
                cutCount = 1;
                linePoints.Add(point0);
                linePoints.Add(point);
            }

            lastType = MoveType.Cut;
            point0 = point;
        }

        //-------------

        private void DrawArc(GCPlane plane, double[] start, double[] stop, double radius, bool clockwise)
        {
            double[] center = convertRToCenter(plane, start, stop, radius, false, clockwise);
            List<Point3D> arcpoints = generatePointsAlongArcBDring(plane, start, stop, center, clockwise, 0, ArcResolution); // Dynamic resolution

            Point3D old_point = arcpoints[0];
            arcpoints.RemoveAt(0);

            AddCutMove(old_point);

            foreach (Point3D point in arcpoints)
                AddCutMove(point);
        }

        private void DrawArc(GCPlane plane, double[] start, double[] stop, double[] ijkValues, bool absoluteIJKMode, bool clockwise)
        {
            double[] center = updateCenterWithCommand(plane, start, ijkValues, absoluteIJKMode);

            List<Point3D> arcpoints = generatePointsAlongArcBDring(plane, start, stop, center, clockwise, 0d, ArcResolution); // Dynamic resolution

            foreach (Point3D point in arcpoints)
                AddCutMove(point);
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
