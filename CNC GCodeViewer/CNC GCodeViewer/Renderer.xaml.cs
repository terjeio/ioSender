/*
 * Renderer.xaml.cs - part of CNC Controls library
 *
 * v0.18 / 2020-04-21 / Io Engineering (Terje Io)
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

#if DEBUG
//#define DEBUG_ARC_BBOXES
#endif

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CNC.Core;
using CNC.GCode;

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
        bool _showViewCube = true;
        BoundingBoxWireFrameVisual3D _bbox;
        ModelVisual3D _axes = new ModelVisual3D();
        Point3D _startposition = new Point3D();
        Point3D _limits = new Point3D();
        Point3D _toolposition = new Point3D();
        Point3DCollection _toolorigin, _cutlines, _rapidlines, _retractlines;

        public void Clear()
        {
         //   Grid = null;
         //   BoundingBox = null;
            ToolOrigin = CutLines = RapidLines = RetractLines = null;
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

        public void SetToolPosition(double x, double y, double z)
        {
            _toolposition.X = x;
            _toolposition.Y = y;
            _toolposition.Z = z;
            OnPropertyChanged(nameof(ToolPosition));
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

        public bool ShowViewCube { get { return _showViewCube; } set { _showViewCube = value; OnPropertyChanged(); } }
        public GridLinesVisual3D Grid { get { return _grid; } set { _grid = value; OnPropertyChanged(); } }
        public BoundingBoxWireFrameVisual3D BoundingBox { get { return _bbox; } set { _bbox = value; OnPropertyChanged(); } }
        public ModelVisual3D Axes { get { return _axes; } }
        public Point3D ToolPosition { get { return _toolposition; } }
        public Point3DCollection ToolOrigin { get { return _toolorigin; } set { _toolorigin = value; OnPropertyChanged(); } }
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

        bool _animateSubscribed = false;
        double[] offsets = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };

        public SolidColorBrush ToolBrush { get; set; } = Brushes.Red;
        public SolidColorBrush AxisBrush { get; set; } = Brushes.Gray;
        public double TickSize { get; set; }

        private GrblViewModel model;
        private bool _animateTool = false;
        private bool? isLatheMode = null;
        private bool isDiameterMode = false;
        private int cutCount;
        private MoveType lastType;

        Point3DCollection linePoints = new Point3DCollection();
        Point3DCollection rapidPoints = new Point3DCollection();
        Point3DCollection retractPoints = new Point3DCollection();
        Point3DCollection positionPoints = new Point3DCollection();

        TruncatedConeVisual3D tool;

        public Machine Machine { get; set; } = new Machine();

        public Renderer()
        {
            InitializeComponent();

            TickSize = 10d;
            MinDistance = 0.05d;
            viewport.DataContext = Machine;

            IsVisibleChanged += Renderer_IsVisibleChanged;
        }

        private void Renderer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (AnimateTool)
            {
                if ((bool)e.NewValue)
                {
                    if (!_animateSubscribed)
                        model.PropertyChanged += Model_PropertyChanged;
                    _animateSubscribed = true;
                }
                else if (_animateSubscribed)
                {
                    _animateSubscribed = false;
                    model.PropertyChanged += Model_PropertyChanged;
                }
            }
        }

        private void Renderer_Loaded(object sender, RoutedEventArgs e)
        {
            model = DataContext as GrblViewModel;

            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                AppConfig.Settings.GCodeViewer.PropertyChanged += GCodeViewer_PropertyChanged;
        }

        private void GCodeViewer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ArcResolution = AppConfig.Settings.GCodeViewer.ArcResolution;
            MinDistance = AppConfig.Settings.GCodeViewer.MinDistance;
            ShowGrid = AppConfig.Settings.GCodeViewer.ShowGrid;
            ShowAxes = AppConfig.Settings.GCodeViewer.ShowAxes;
            ShowBoundingBox = AppConfig.Settings.GCodeViewer.ShowBoundingBox;
            Machine.ShowViewCube = AppConfig.Settings.GCodeViewer.ShowViewCube;
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GrblViewModel.Position))
            {
                Machine.SetToolPosition(model.Position.X / (isDiameterMode ? 2d : 1d), model.Position.Y, model.Position.Z);
                if (tool != null)
                    tool.Origin = Machine.ToolPosition;
            }
            else if (e.PropertyName == nameof(GrblViewModel.LatheMode))
                isDiameterMode = model.LatheMode == LatheMode.Diameter;
        }

        public int ArcResolution { get; set; } = 5;
        public double MinDistance { get { return _minDistance; } set { _minDistance = value; minDistanceSquared = _minDistance * _minDistance; } }
        public bool ShowGrid { get; set; } = true;
        public bool ShowAxes { get; set; } = true;
        public bool ShowBoundingBox { get; set; } = true;
        public bool ShowViewCube { get { return Machine.ShowViewCube; } set { Machine.ShowViewCube = value; } }

        public bool AnimateTool
        {
            get { return _animateTool; }
            set
            {
                if(value != _animateTool)
                {
                    if ((_animateTool = value))
                    {
                        Machine.SetToolPosition(model.Position.X, model.Position.Y, model.Position.Z);

                        tool = new TruncatedConeVisual3D();
                        tool.Height = 3d;
                        tool.BaseRadius = 0d;
                        tool.TopRadius = tool.Height / 5d;
                        tool.TopCap = true;
                        tool.Origin = Machine.ToolPosition;
                        tool.Normal = new Vector3D(0d, 0d, 1d);
                        tool.Fill = ToolBrush;
                        viewport.Children.Add(tool);

                        _animateSubscribed = true;
                        model.PropertyChanged += Model_PropertyChanged;
                    }
                    else
                    {
                        if (tool != null)
                        {
                            viewport.Children.Remove(tool);
                            tool = null;
                        }
                        _animateSubscribed = false;
                        model.PropertyChanged -= Model_PropertyChanged;
                    }
                }
            }
        }

        private void viewport_Drag(object sender, DragEventArgs e)
        {
            GCode.File.Drag(sender, e);
        }

        private void viewport_Drop(object sender, DragEventArgs e)
        {
            GCode.File.Drop(sender, e);
        }

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

            Machine.ToolOrigin = null;

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

            Machine.ToolOrigin = positionPoints;
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
            var bbox = (DataContext as GrblViewModel).ProgramLimits;

            double lineThickness = bbox.MaxSize / 1000d;
            double arrowOffset = lineThickness * 30d;
            double labelOffset = lineThickness * 50d;

            ClearViewport();

            if (isLatheMode == null)
            {
                if ((isLatheMode = model.LatheMode != LatheMode.Disabled) == true)
                {
                    viewport.ModelUpDirection = new Vector3D(0d, -1d, 0d);
                    if (tool != null)
                        tool.Normal = new Vector3D(1d, 0d, 0d);
                }
                isDiameterMode = model.LatheMode == LatheMode.Diameter;
            }

            bool latheMode = isLatheMode == true;

            cutCount = 0;
            point0 = Machine.StartPosition;
            lastType = MoveType.None;
            if (tool != null)
            {
                tool.Height = Math.Min(6d, lineThickness * 100d);
                tool.TopRadius = tool.Height / 5d;
            }

            #region Canvas adorners

            if (ShowGrid)
            {
                double wh, h, wm = bbox.SizeX % TickSize, w = Math.Ceiling(bbox.SizeX - bbox.SizeX % TickSize + TickSize * 2d);

                if (model.LatheMode == LatheMode.Disabled)
                {

                    wh = bbox.SizeY % TickSize;
                    h = Math.Ceiling(bbox.SizeY - bbox.SizeY % TickSize + TickSize * 2d);

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
                }
                else
                {
                    wh = bbox.SizeZ % TickSize;
                    h = Math.Ceiling(bbox.SizeZ - bbox.SizeZ % TickSize + TickSize * 2d);

                    Machine.Grid = new GridLinesVisual3D()
                    {
                        Center = new Point3D(boffset(bbox.SizeX, bbox.MinX, w, wm) - TickSize, 0d, boffset(bbox.SizeZ, bbox.MinZ, h, wh) - TickSize),
                        MinorDistance = 2.5d,
                        MajorDistance = TickSize,
                        Width = w,
                        Length = h,
                        Thickness = lineThickness,
                        Fill = AxisBrush,
                        LengthDirection = new Vector3D(0d, 0d, 1d),
                        Normal = new Vector3D(0d, 1d, 0d)
                    };
                }
                viewport.Children.Add(Machine.Grid);
            }

            if (ShowAxes)
            {
                Machine.Axes.Children.Add(new ArrowVisual3D() {
                    Point2 = new Point3D(bbox.SizeX + arrowOffset, 0d, 0d),
                    Diameter = lineThickness * 5,
                    Fill = AxisBrush
                });

                Machine.Axes.Children.Add(new BillboardTextVisual3D() {
                    Text = "X",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(bbox.SizeX + labelOffset, 0d, 0d)
                });

                if (bbox.SizeY > 0d)
                {
                    Machine.Axes.Children.Add(new ArrowVisual3D()
                    {
                        Point2 = new Point3D(0d, bbox.SizeY + arrowOffset, 0d),
                        Diameter = lineThickness * 5d,
                        Fill = AxisBrush
                    });

                    Machine.Axes.Children.Add(new BillboardTextVisual3D()
                    {
                        Text = "Y",
                        FontWeight = FontWeights.Bold,
                        Foreground = AxisBrush,
                        Position = new Point3D(0d, bbox.SizeY + labelOffset, 0d)
                    });
                }

                if (bbox.SizeZ > 0d)
                {
                    Machine.Axes.Children.Add(new ArrowVisual3D() {
                        Point1 = latheMode ? new Point3D(0d, 0d, bbox.MaxZ + arrowOffset) : new Point3D(0d, 0d, bbox.MinZ - arrowOffset),
                        Point2 = latheMode ? new Point3D(0d, 0d, bbox.MinZ - arrowOffset) : new Point3D(0d, 0d, bbox.MaxZ + arrowOffset),
                        Diameter = lineThickness * 5d,
                        Fill = AxisBrush,
                    });

                    Machine.Axes.Children.Add(new BillboardTextVisual3D()
                    {
                        Text = "Z",
                        FontWeight = FontWeights.Bold,
                        Foreground = AxisBrush,
                        Position = new Point3D(0d, 0d, latheMode ? bbox.MinZ - labelOffset : bbox.MaxZ + labelOffset)
                    });
                }

                viewport.Children.Add(Machine.Axes);
            }

            if (ShowBoundingBox && bbox.SizeZ > 0d)
            {
                Machine.BoundingBox = new BoundingBoxWireFrameVisual3D()
                {
                    BoundingBox = new Rect3D(bbox.MinX, bbox.MinY, bbox.MinZ, bbox.SizeX, Math.Max(0.001d, bbox.SizeY), bbox.SizeZ),
                    Thickness = 1d,
                    Color = Colors.LightGreen
                };

                viewport.Children.Add(Machine.BoundingBox);
            }

            #endregion

            GCodeEmulator emu = new GCodeEmulator(true);

            emu.SetStartPosition(Machine.StartPosition);

            foreach (var cmd in emu.Execute(tokens))
            {
                point0 = cmd.Start;

                switch (cmd.Token.Command)
                {
                    case Commands.G0:
                        if(cmd.IsRetract)
                            AddRetractMove(cmd.End);
                        else
                            AddRapidMove(cmd.End);
                        break;

                    case Commands.G1:
                        AddCutMove(cmd.End);
                        break;

                    case Commands.G2:
                    case Commands.G3:
#if DEBUG_ARC_BBOXES
                        var bb = (cmd.Token as GCArc).GetBoundingBox(emu.Plane, point0.ToArray(), emu.DistanceMode == DistanceMode.Incremental);

                        var abb = new BoundingBoxWireFrameVisual3D()
                        {
                            BoundingBox = new Rect3D(bb.Min[0], bb.Min[1], bb.Min[2], bb.Size[0], bb.Size[1], bb.Size[2]),
                            Thickness = .5d,
                            Color = Colors.Blue
                        };
                        viewport.Children.Add(abb);
#endif
                        DrawArc(cmd.Token as GCArc, point0.ToArray(), emu.Plane, emu.DistanceMode == DistanceMode.Incremental);
                        break;

                    case Commands.G5:
                        DrawSpline(cmd.Token as GCSpline, point0.ToArray());
                        break;
                }
            }

            Machine.RapidLines = rapidPoints;
            Machine.CutLines = linePoints;
            Machine.RetractLines = retractPoints;

            refreshCamera(bbox);
        }
        public void refreshCamera(ProgramLimits bbox)
        {

            if (model.LatheMode == LatheMode.Disabled)
            {

                double zpos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeY) / Math.Tan(camera.FieldOfView * Math.PI / 360d));

                // TODO: set a sensible viewing distance dynamically

                viewport.Camera.Position = new Point3D((bbox.MaxX + bbox.MinX) / 2d, (bbox.MaxY + bbox.MinY) / 2d, zpos);
                viewport.Camera.LookDirection = new Vector3D(0d, 0d, -100d);
                viewport.Camera.UpDirection = new Vector3D(0d, 1d, 0.5d);
            }
            else
            {
                double ypos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeZ) / Math.Tan(camera.FieldOfView * Math.PI / 360d));

                // TODO: set a sensible viewing distance dynamically

                viewport.Camera.Position = new Point3D((bbox.MaxX + bbox.MinX) / 2d, -ypos, (bbox.MaxZ + bbox.MinZ) / 2d);
                viewport.Camera.LookDirection = new Vector3D(0d, 100d, 0d);
                viewport.Camera.UpDirection = new Vector3D(-1d, 0d, 0d);
            }
            //                viewport.CameraController.AddRotateForce(0.001, 0.001); // emulate move camera 
        }
        private Point3D toPoint(double[] values, bool isRelative = false)
        {
            Point3D p = new Point3D(values[0], values[1], values[2]);

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

        private void DrawArc(GCArc arc, double[] start, GCPlane plane, bool isRelative = false)
        {
            List<Point3D> points = arc.GeneratePoints(plane, start, ArcResolution, isRelative); // Dynamic resolution

            foreach (Point3D point in points)
                AddCutMove(point);
        }

        private void DrawSpline(GCSpline spline, double[] start, bool isRelative = false)
        {
            List<Point3D> points = spline.GeneratePoints(start, ArcResolution, isRelative); // Dynamic resolution

            foreach (Point3D point in points)
                AddCutMove(point);
        }
    }
}
