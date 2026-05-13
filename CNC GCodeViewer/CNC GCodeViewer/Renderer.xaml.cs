/*
 * Renderer.xaml.cs - part of CNC Controls library
 *
 * v0.43 / 2023-07-05 / Io Engineering (Terje Io)
 *
 */

/* Some parts ported and/or inspired from code at:
  https://www.codeproject.com/Articles/1246255/Plotting-a-Real-time-D-Toolpath-with-Helix-Toolkit
  https://github.com/winder/Universal-G-Code-Sender
  https://github.com/Denvi/Candle
  https://github.com/grbl/grbl (seems to be the origin of arc plotting functions?)
*/

/*

Copyright (c) 2019-2023, Io Engineering (Terje Io)
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

    public enum RenderMode : int
    {
        Mode3D = 0,
        Mode2DXY,
        Mode2DXZ,
        Mode2DYZ,
    }

    public enum ToolVisualizerType : int
    {
        None,
        Cone,
        ToolTrace,
        Crosshair
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

    #region Machine viewmodel

    public class Machine : ViewModelBase
    {
        private GridLinesVisual3D _grid = null;
        private bool _showViewCube = true, _showCoordSystem = false, _showGrid = false, _showAxes = false, _showJobEnvelope = false, _showWorkEnvelope = false, _canRestoreView = false;
        private ModelVisual3D _axes = null;
        private Point3D _startposition = new Point3D();
        private Point3D _limits = new Point3D();
        private Point3D _toolposition = new Point3D();
        private Point3DCollection _toolorigin, _cutlines, _rapidlines, _retractlines, _executedlines, _tooltrail;
        private BoundingBoxWireFrameVisual3D _workEnvelope, _jobEnvelope;
        private Color _cutMotion = Colors.Red, _rapidMotion = Colors.LightPink, _retractMotion = Colors.Green, _toolOrigin = Colors.Red, _gridColor = Colors.LightGray, _highlight = Colors.Crimson, _toolTrailColor = Colors.Yellow;
        private SolidColorBrush _canvas = Brushes.White;
        private ToolVisualizerType _toolmode = ToolVisualizerType.None;
        private RenderMode _renderMode = RenderMode.Mode3D;
        private Dictionary<ToolVisualizerType, string> _toolModes;

        private Dictionary<RenderMode, string> _renderModes = new Dictionary<RenderMode, string>()
        {
            { RenderMode.Mode3D, "3D"},
            { RenderMode.Mode2DXY, "XY"},
            { RenderMode.Mode2DXZ, "XZ"},
            { RenderMode.Mode2DYZ, "YZ"}
        };

        public Machine(UserControl owner)
        {
            _toolModes = new Dictionary<ToolVisualizerType, string>()
            {
                { ToolVisualizerType.None, (string)owner.FindResource("ToolNone") },
                { ToolVisualizerType.Cone, (string)owner.FindResource("ToolCone") },
                { ToolVisualizerType.ToolTrace, (string)owner.FindResource("ToolTrace") },
                { ToolVisualizerType.Crosshair, (string)owner.FindResource("ToolCrosshair") },
            };
        }

        public void Clear()
        {
            //   Grid = null;
            //   BoundingBox = null;
            ToolOrigin = CutLines = RapidLines = RetractLines = ExecutedLines = null;
            // Don't clear ToolTrail - it should persist during job execution
            WorkEnvelope = JobEnvelope = null;
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
            if (DiameterMode)
                x /= 2d;

            switch (RenderMode)
            {
                case RenderMode.Mode3D:
                    _toolposition.X = x;
                    _toolposition.Y = y;
                    _toolposition.Z = z;
                    break;

                case RenderMode.Mode2DXY:
                    _toolposition.X = x;
                    _toolposition.Y = y;
                    _toolposition.Z = 0d;
                    break;

                case RenderMode.Mode2DXZ:
                    _toolposition.X = x;
                    _toolposition.Y = 0d;
                    _toolposition.Z = z;
                    break;

                case RenderMode.Mode2DYZ:
                    _toolposition.X = 0d;
                    _toolposition.Y = y;
                    _toolposition.Z = z;
                    break;
            }

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

        public RenderMode RenderMode { get { return _renderMode; } set { _renderMode = value; OnPropertyChanged(); } }
        public Dictionary<RenderMode, string> RenderModes { get { return _renderModes; } }

        public ToolVisualizerType ToolMode { get { return _toolmode; } set { _toolmode = value; OnPropertyChanged(); } }
        public Dictionary<ToolVisualizerType, string> ToolModes { get { return _toolModes; } }

        public bool ShowViewCube { get { return _showViewCube; } set { _showViewCube = value; OnPropertyChanged(); } }
        public bool ShowGrid { get { return _showGrid; } set { _showGrid = value; OnPropertyChanged(); } }
        public bool ShowAxes { get { return _showAxes; } set { _showAxes = value; OnPropertyChanged(); } }
        public bool ShowJobEnvelope { get { return _showJobEnvelope; } set { _showJobEnvelope = value; OnPropertyChanged(); } }
        public bool ShowWorkEnvelope { get { return _showWorkEnvelope; } set { _showWorkEnvelope = value; OnPropertyChanged(); } }
        public bool ShowCoordinateSystem { get { return _showCoordSystem; } set { _showCoordSystem = value; OnPropertyChanged(); } }
        public bool CanRestoreView { get { return _canRestoreView; } set { _canRestoreView = value; OnPropertyChanged(); } }
        public GridLinesVisual3D Grid { get { return _grid; } set { _grid = value; OnPropertyChanged(); } }
        public ModelVisual3D Axes { get { return _axes; } set { _axes = value; } }
        public Point3D ToolPosition { get { return _toolposition; } }
        public Point3DCollection ToolOrigin { get { return _toolorigin; } set { _toolorigin = value; OnPropertyChanged(); } }
        public Point3DCollection CutLines { get { return _cutlines; } set { _cutlines = value; OnPropertyChanged(); } }
        public Point3DCollection RapidLines { get { return _rapidlines; } set { _rapidlines = value; OnPropertyChanged(); } }
        public Point3DCollection RetractLines { get { return _retractlines; } set { _retractlines = value; OnPropertyChanged(); } }
        public Point3DCollection ExecutedLines { get { return _executedlines; } set { _executedlines = value; OnPropertyChanged(); } }
        public Point3DCollection ToolTrail { get { return _tooltrail; } set { _tooltrail = value; OnPropertyChanged(); } }
        public BoundingBoxWireFrameVisual3D WorkEnvelope { get { return _workEnvelope; } set { _workEnvelope = value; OnPropertyChanged(); } }
        public BoundingBoxWireFrameVisual3D JobEnvelope { get { return _jobEnvelope; } set { _jobEnvelope = value; OnPropertyChanged(); } }
        public Point3D StartPosition { get { return _startposition; } }
        public Point3D Limits { get { return _limits; } }
        public Color CutMotionColor { get { return _cutMotion; } set { _cutMotion = value; OnPropertyChanged(); } }
        public Color RapidMotionColor { get { return _rapidMotion; } set { _rapidMotion = value; OnPropertyChanged(); } }
        public Color RetractMotionColor { get { return _retractMotion; } set { _retractMotion = value; OnPropertyChanged(); } }
        public Color ToolOriginColor { get { return _toolOrigin; } set { _toolOrigin = value; OnPropertyChanged(); } }
        public Color GridColor { get { return _gridColor; } set { _gridColor = value; OnPropertyChanged(); } }
        public Color HighlightColor { get { return _highlight; } set { _highlight = value; OnPropertyChanged(); } }
        public Color ToolTrailColor { get { return _toolTrailColor; } set { _toolTrailColor = value; OnPropertyChanged(); } }
        public SolidColorBrush CanvasColor { get { return _canvas; } set { _canvas = value; OnPropertyChanged(); } }
        public bool DiameterMode { get; set; } = false;
    }

    #endregion

    public partial class Renderer : UserControl
    {
        private Point3D point0;  // last point
        private Vector3D delta0;  // (dx,dy,dz)
        private double minDistanceSquared, _minDistance;
        private double gridWidth = 200d;
        private double gridHeight = 200d;
        private double[] offsets = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };
        private bool _animateSubscribed = false, zoomSubscribed = false, renderExecuted = false, toolAutoScale = false;
        private bool? isLatheMode = null;
        private int cutCount;

        private GrblViewModel model = null;
        private RenderMode lastMode = RenderMode.Mode3D;
        private MoveType lastType;
        private IEnumerator<RunAction> job = null;
        private GCodeEmulator emu = new GCodeEmulator(true);
        private List<GCodeToken> tokens;
        private ProgramLimits renderedProgramLimits = null;

        private Point3D gridOffset;
        private GridLinesVisual3D grid = null;
        private ModelVisual3D axes = new ModelVisual3D();
        private Point3DCollection cutPoints = new Point3DCollection();
        private Point3DCollection rapidPoints = new Point3DCollection();
        private Point3DCollection retractPoints = new Point3DCollection();
        private Point3DCollection positionPoints = new Point3DCollection();
        private Point3DCollection toolPathHistory = new Point3DCollection();
        private List<Point3DCollection> toolCircleHistoryLayers = new List<Point3DCollection>();
        private const int ToolTraceMaxElements = 100;
        private const int ToolCircleSegments = 24;
        private const int ToolCircleHistoryCount = 10;
        private LinesVisual3D toolTrailVisual;
        private List<LinesVisual3D> toolCircleVisuals = new List<LinesVisual3D>();
        private Point3D lastToolPosition;
        private bool wasJobRunning = false;
        private List<BoxVisual3D> pauseMarkers = new List<BoxVisual3D>();

        private TruncatedConeVisual3D tool;
        private BoundingBoxWireFrameVisual3D workEnvelope = new BoundingBoxWireFrameVisual3D()
        {
            Thickness = 1d,
            Color = Colors.DarkBlue
        };
        private BoundingBoxWireFrameVisual3D jobEnvelope = new BoundingBoxWireFrameVisual3D()
        {
            Thickness = 1d,
            Color = Colors.LightGreen
        };


        public Renderer()
        {
            InitializeComponent();

            TickSize = 10d;
            MinDistance = 0.05d;
            Machine = new Machine(this);
            viewport.DataContext = Machine;

            toolTrailVisual = new LinesVisual3D
            {
                Thickness = 1,
                Color = Machine.ToolTrailColor
            };
            viewport.Children.Add(toolTrailVisual);

            for (int i = 0; i < ToolCircleHistoryCount; i++)
            {
                toolCircleHistoryLayers.Add(new Point3DCollection());

                var circleVisual = new LinesVisual3D
                {
                    Thickness = 0.75,
                    Color = Machine.ToolTrailColor
                };

                toolCircleVisuals.Add(circleVisual);
                viewport.Children.Add(circleVisual);
            }

            UpdateToolCircleColors();

            IsVisibleChanged += Renderer_IsVisibleChanged;
            Machine.PropertyChanged += Machine_PropertyChanged;
        }

        #region Public properties

        public double TickSize { get; set; }
        public int ArcResolution { get; set; } = 5;
        public double MinDistance { get { return _minDistance; } set { _minDistance = value; minDistanceSquared = _minDistance * _minDistance; } }
        public bool RenderExecuted { get; set; } = false;
        public bool IsJobLoaded { get { return !(tokens == null || tokens.Count == 0); } }

        public Machine Machine { get; set; }
        public SolidColorBrush ToolBrush { get; set; } = Brushes.Red;
        public SolidColorBrush AxisBrush { get; set; } = Brushes.Gray;

        #endregion

        #region UI events

        private void Renderer_Loaded(object sender, RoutedEventArgs e)
        {
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this) && model == null)
            {
                model = DataContext as GrblViewModel;
                wasJobRunning = model != null && model.IsJobRunning;
                AppConfig.Settings.GCodeViewer.PropertyChanged += GCodeViewer_PropertyChanged;
                Configure();
            }
        }

        private void Renderer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Machine.ToolMode != ToolVisualizerType.None)
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
                    model.PropertyChanged -= Model_PropertyChanged;
                }
            }
        }

        private void Machine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Machine.ToolTrailColor))
            {
                if (toolTrailVisual != null)
                    toolTrailVisual.Color = Machine.ToolTrailColor;
                UpdateToolCircleColors();
                return;
            }

            if (!IsJobLoaded)
                return;

            switch (e.PropertyName)
            {

                case nameof(Machine.RenderMode):
                    Render(tokens, (lastMode == RenderMode.Mode3D || lastMode == RenderMode.Mode2DXY) ? !(Machine.RenderMode == RenderMode.Mode3D || Machine.RenderMode == RenderMode.Mode2DXY) : lastMode != Machine.RenderMode);
                    lastMode = Machine.RenderMode;
                    if (Machine.ToolMode == ToolVisualizerType.Crosshair)
                        ShowCrosshairTool();
                    break;

                case nameof(Machine.ShowGrid):
                    if (Machine.ShowGrid)
                    {
                        if (Machine.Grid == null && (Machine.Grid = grid) != null)
                            viewport.Children.Add(Machine.Grid);
                    }
                    else if (Machine.Grid != null)
                    {
                        viewport.Children.Remove(Machine.Grid);
                        Machine.Grid = null;
                    }
                    break;

                case nameof(Machine.ToolMode):
                    switch (Machine.ToolMode)
                    {
                        case ToolVisualizerType.None:
                            positionPoints.Clear();
                            if (tool != null)
                                viewport.Children.Remove(tool);
                            ShowToolTrace(false);
                            break;

                        case ToolVisualizerType.Cone:
                            if (toolAutoScale & !zoomSubscribed)
                            {
                                zoomSubscribed = true;
                                viewport.PreviewMouseWheel += MouseWheel_Preview;
                            }
                            positionPoints.Clear();
                            if (tool != null)
                            {
                                if (!viewport.Children.Contains(tool))
                                    viewport.Children.Add(tool);
                                ShowConeTool();
                            }
                            ShowToolTrace(false);
                            break;

                        case ToolVisualizerType.ToolTrace:
                            if (tool != null)
                                viewport.Children.Remove(tool);
                            positionPoints.Clear();
                            ShowToolTrace(true);
                            break;

                        case ToolVisualizerType.Crosshair:
                            ShowCrosshairTool();
                            if (tool != null)
                                viewport.Children.Remove(tool);
                            ShowToolTrace(false);
                            break;
                    }
                    if (zoomSubscribed && Machine.ToolMode != ToolVisualizerType.Cone)
                    {
                        zoomSubscribed = false;
                        viewport.PreviewMouseWheel -= MouseWheel_Preview;
                    }
                    else if (!zoomSubscribed && pauseMarkers.Count > 0)
                    {
                        zoomSubscribed = true;
                        viewport.PreviewMouseWheel += MouseWheel_Preview;
                    }
                    break;

                case nameof(Machine.ShowAxes):
                    if (Machine.ShowAxes)
                    {
                        if (Machine.Axes == null && (Machine.Axes = axes) != null)
                            viewport.Children.Add(Machine.Axes);
                    }
                    else if (Machine.Axes != null)
                    {
                        viewport.Children.Remove(Machine.Axes);
                        Machine.Axes = null;
                    }
                    break;

                case nameof(Machine.ShowWorkEnvelope):
                    Machine.WorkEnvelope = Machine.ShowWorkEnvelope && tokens != null ? workEnvelope : null;
                    break;

                case nameof(Machine.ShowJobEnvelope):
                    Machine.JobEnvelope = Machine.ShowJobEnvelope && tokens != null ? jobEnvelope : null;
                    break;
            }
        }

        private void GCodeViewer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Configure();
            if (IsJobLoaded)
                Render(tokens);
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GrblViewModel.Position):

                    AnimateTool();
                    break;

                case nameof(GrblViewModel.LatheMode):
                    Machine.DiameterMode = model.LatheMode == LatheMode.Diameter;
                    break;

                case nameof(GrblViewModel.BlockExecuting):
                    if (renderExecuted)
                        RenderExecuting(model.BlockExecuting);
                    break;

                case nameof(GrblViewModel.WorkPositionOffset):
                    if (Machine.ShowGrid && Machine.Grid != null)
                    {
                        Machine.Grid.Center = getGridAdjust(gridOffset);
                    }
                    AddWorkEnvelope();
                    break;

                case nameof(GrblViewModel.IsJobRunning):
                    if (model.IsJobRunning && !wasJobRunning)
                        ClearToolTraceCache();
                    wasJobRunning = model.IsJobRunning;
                    break;
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

        private void MouseWheel_Preview(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (IsJobLoaded)
            {
                ShowConeTool();
                UpdatePauseMarkerSizes();
            }
            //model.ResponseLog.Add(string.Format("M: {0} {1} {2}", e.Delta, viewport.Camera.Position.Z, viewport.Camera.LookDirection.Z));
            //pl = viewport.Camera.LookDirection.Length;

            //if (e.Delta > 0)
            //{
            //    if (viewport.Camera.LookDirection.Length > 100)
            //    {
            //    }
            //}
            //else
            //{
            //    if (viewport.Camera.LookDirection.Length < 10)
            //    {

            //    }
            //}
        }

        #endregion

        private void Configure()
        {
            toolAutoScale = AppConfig.Settings.GCodeViewer.ToolAutoScale;

            if (tool == null)
            {
                tool = new TruncatedConeVisual3D()
                {
                    Height = 3d,
                    BaseRadius = 0,
                    TopRadius = GetToolTraceDiameter() / 2d,
                    TopCap = true,
                    Normal = new Vector3D(0d, 0d, 1d),
                    Fill = ToolBrush
                };
            }
            else
            {
                tool.TopRadius = GetToolTraceDiameter() / 2d;
                if (!toolAutoScale)
                {
                    tool.Height = 3d;
                    if (zoomSubscribed)
                    {
                        zoomSubscribed = false;
                        viewport.PreviewMouseWheel -= MouseWheel_Preview;
                    }
                }
                else if (!zoomSubscribed)
                {
                    zoomSubscribed = true;
                    viewport.PreviewMouseWheel += MouseWheel_Preview;
                }
            }

            ArcResolution = AppConfig.Settings.GCodeViewer.ArcResolution;
            MinDistance = AppConfig.Settings.GCodeViewer.MinDistance;
            Machine.ShowGrid = AppConfig.Settings.GCodeViewer.ShowGrid;
            Machine.ShowAxes = AppConfig.Settings.GCodeViewer.ShowAxes;
            Machine.ShowJobEnvelope = AppConfig.Settings.GCodeViewer.ShowBoundingBox;
            Machine.ShowWorkEnvelope = AppConfig.Settings.GCodeViewer.ShowWorkEnvelope;
            RenderExecuted = AppConfig.Settings.GCodeViewer.RenderExecuted;
            Machine.ShowViewCube = AppConfig.Settings.GCodeViewer.ShowViewCube;
            Machine.ShowCoordinateSystem = AppConfig.Settings.GCodeViewer.ShowCoordinateSystem;
            Machine.CutMotionColor = AppConfig.Settings.GCodeViewer.CutMotionColor;
            Machine.RapidMotionColor = AppConfig.Settings.GCodeViewer.RapidMotionColor;
            Machine.RetractMotionColor = AppConfig.Settings.GCodeViewer.RetractMotionColor;
            Machine.HighlightColor = AppConfig.Settings.GCodeViewer.HighlightColor;
            Machine.ToolTrailColor = AppConfig.Settings.GCodeViewer.ToolTrailColor;
            if (toolTrailVisual != null)
                toolTrailVisual.Color = Machine.ToolTrailColor;
            UpdateToolCircleColors();
            //     Machine.ToolOriginColor = AppConfig.Settings.GCodeViewer.ToolOriginColor;
            Machine.GridColor = AppConfig.Settings.GCodeViewer.GridColor;
            Machine.CanvasColor = AppConfig.Settings.GCodeViewer.BlackBackground ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
            Machine.CanRestoreView = AppConfig.Settings.GCodeViewer.ViewMode >= 0;
            if (Machine.ToolMode != (ToolVisualizerType)AppConfig.Settings.GCodeViewer.ToolVisualizer)
                Machine.ToolMode = (ToolVisualizerType)AppConfig.Settings.GCodeViewer.ToolVisualizer;
        }

        public void SaveView()
        {
            AppConfig.Settings.GCodeViewer.ViewMode = (int)Machine.RenderMode;
            AppConfig.Settings.GCodeViewer.ToolVisualizer = (int)Machine.ToolMode;
            AppConfig.Settings.GCodeViewer.CameraPosition = new Point3D(viewport.Camera.Position.X, viewport.Camera.Position.Y, viewport.Camera.Position.Z);
            AppConfig.Settings.GCodeViewer.CameraLookDirection = new Vector3D(viewport.Camera.LookDirection.X, viewport.Camera.LookDirection.Y, viewport.Camera.LookDirection.Z);
            AppConfig.Settings.GCodeViewer.CameraUpDirection = new Vector3D(viewport.Camera.UpDirection.X, viewport.Camera.UpDirection.Y, viewport.Camera.UpDirection.Z);
            AppConfig.Settings.Save();
            Machine.CanRestoreView = true;
        }

        public void RestoreView()
        {
            if (AppConfig.Settings.GCodeViewer.ViewMode != -1)
            {
                viewport.Camera.Position = AppConfig.Settings.GCodeViewer.CameraPosition;
                viewport.Camera.LookDirection = AppConfig.Settings.GCodeViewer.CameraLookDirection;
                viewport.Camera.UpDirection = AppConfig.Settings.GCodeViewer.CameraUpDirection;
                if (IsJobLoaded)
                {
                    Machine.RenderMode = (RenderMode)AppConfig.Settings.GCodeViewer.ViewMode;
                    if (Machine.ToolMode != (ToolVisualizerType)AppConfig.Settings.GCodeViewer.ToolVisualizer)
                        Machine.ToolMode = (ToolVisualizerType)AppConfig.Settings.GCodeViewer.ToolVisualizer;
                    AnimateTool();
                }
            }
        }

        public void RefreshView(ProgramLimits bbox)
        {
            // TODO: set a sensible viewing distance dynamically

            if (model.UnitFactor != 1d)
                bbox = new ProgramLimits(bbox, model.UnitFactor);

            double centerX = (bbox.MaxX + bbox.MinX) / 2d;
            double centerY = (bbox.MaxY + bbox.MinY) / 2d;
            double centerZ = (bbox.MaxZ + bbox.MinZ) / 2d;

            if (model.LatheMode == LatheMode.Disabled)
            {
                double pos;
                switch (Machine.RenderMode)
                {
                    case RenderMode.Mode3D:
                    case RenderMode.Mode2DXY:
                        pos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeY) / Math.Tan(ccamera.FieldOfView * Math.PI / 360d));
                        viewport.Camera.Position = new Point3D(centerX, centerY, centerZ + pos);
                        viewport.Camera.LookDirection = new Vector3D(0d, 0d, -pos);
                        viewport.Camera.UpDirection = new Vector3D(0d, 1d, 1d);
                        break;

                    case RenderMode.Mode2DXZ:
                        pos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeZ) / Math.Tan(ccamera.FieldOfView * Math.PI / 360d));
                        viewport.Camera.Position = new Point3D(centerX, centerY - pos, centerZ);
                        viewport.Camera.LookDirection = new Vector3D(0d, pos, 0d);
                        viewport.Camera.UpDirection = new Vector3D(0d, 1d, 1d);
                        break;

                    case RenderMode.Mode2DYZ:
                        pos = Math.Max(5d, Math.Max(bbox.SizeY, bbox.SizeZ) / Math.Tan(ccamera.FieldOfView * Math.PI / 360d));
                        viewport.Camera.Position = new Point3D(centerX + pos, centerY, centerZ);
                        viewport.Camera.LookDirection = new Vector3D(-pos, 0d, 0d);
                        viewport.Camera.UpDirection = new Vector3D(1d, 0d, 1d);
                        break;
                }
            }
            else
            {
                double ypos = Math.Max(5d, Math.Max(bbox.SizeX, bbox.SizeZ) / Math.Tan(ccamera.FieldOfView * Math.PI / 360d));
                viewport.Camera.Position = new Point3D(centerX, centerY - ypos, centerZ);
                viewport.Camera.LookDirection = new Vector3D(0d, ypos, 0d);
                viewport.Camera.UpDirection = new Vector3D(-1d, 0d, 0d);
            }
            //                viewport.CameraController.AddRotateForce(0.001, 0.001); // emulate move camera 
            AnimateTool();
        }

        public void ClearViewport()
        {
            Machine.Clear();
            cutPoints.Clear();
            rapidPoints.Clear();
            retractPoints.Clear();
            toolPathHistory.Clear();
            foreach (var points in toolCircleHistoryLayers)
                points.Clear();

            foreach (var marker in pauseMarkers)
                viewport.Children.Remove(marker);
            pauseMarkers.Clear();

            if (job != null)
            {
                job.Dispose();
                job = null;
                tokens = null;
            }

            renderedProgramLimits = null;

            if (Machine.Grid != null)
                viewport.Children.Remove(Machine.Grid);
            Machine.Grid = grid = null;

            if (Machine.Axes != null)
            {
                viewport.Children.Remove(Machine.Axes);
                Machine.Axes = null;
            }
            axes.Children.Clear();

            if (tool != null && viewport.Children.Contains(tool))
                viewport.Children.Remove(tool);

            if (toolTrailVisual != null)
                toolTrailVisual.Points = toolPathHistory;
            RefreshToolCircleVisuals();
        }


        public void ResetView()
        {
            RefreshView(((GrblViewModel)DataContext).ProgramLimits);
                return;
        }

        private void AddWorkEnvelope()
        {
            Position workPositionOffset = new Position(model.WorkPositionOffset, model.UnitFactor);

            if (isLatheMode == true)
                workEnvelope.BoundingBox = new Rect3D(-workPositionOffset.X, 0d, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, 0d, GrblInfo.MaxTravel.Z);
            else if (GrblInfo.ForceSetOrigin)
                workEnvelope.BoundingBox = new Rect3D(-workPositionOffset.X, -workPositionOffset.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, GrblInfo.MaxTravel.Y, GrblInfo.MaxTravel.Z);
            else
                workEnvelope.BoundingBox = new Rect3D(-GrblInfo.MaxTravel.X - workPositionOffset.X, -GrblInfo.MaxTravel.Y - workPositionOffset.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, GrblInfo.MaxTravel.Y, GrblInfo.MaxTravel.Z);
        }

        private void AnimateTool()
        {
            Position position = new Position(model.Position, model.UnitFactor);

            Machine.SetToolPosition(position.X, position.Y, position.Z);
            // DEBUG: Check if this is being called
            System.Diagnostics.Debug.WriteLine($"AnimateTool called: {position.X}, {position.Y}, {position.Z}, ToolMode: {Machine.ToolMode}");

            // Add current position to history
            if (Machine.ToolMode == ToolVisualizerType.ToolTrace)
            {
                Point3D historyPoint = Machine.ToolPosition;

                bool hasMoved = toolPathHistory.Count == 0 || (historyPoint - lastToolPosition).LengthSquared > 0d;

                if (hasMoved)
                    AddToolCircle(historyPoint);

                if (toolPathHistory.Count > 0)
                {
                    toolPathHistory.Add(lastToolPosition);
                    toolPathHistory.Add(historyPoint);
                }
                else
                {
                    toolPathHistory.Add(historyPoint);
                    toolPathHistory.Add(historyPoint);
                }

                TrimToolPathHistory();

                lastToolPosition = historyPoint;
                Machine.ToolTrail = toolPathHistory;

                if (toolTrailVisual != null)
                {
                    toolTrailVisual.Points = toolPathHistory;
                }

                RefreshToolCircleVisuals();

            }

            if (IsJobLoaded) switch (Machine.ToolMode)
                {
                    case ToolVisualizerType.Cone:
                        ShowConeTool();
                        break;

                    case ToolVisualizerType.Crosshair:
                        ShowCrosshairTool();
                        break;
                }
        }

        private void TrimToolPathHistory()
        {
            while (toolPathHistory.Count > ToolTraceMaxElements)
            {
                toolPathHistory.RemoveAt(0);
                toolPathHistory.RemoveAt(0);
            }
        }

        private Point3D GetToolCirclePoint(Point3D center, double radius, double angle)
        {
            double cs = Math.Cos(angle);
            double sn = Math.Sin(angle);

            switch (Machine.RenderMode)
            {
                case RenderMode.Mode2DXZ:
                    return new Point3D(center.X + radius * cs, center.Y, center.Z + radius * sn);

                case RenderMode.Mode2DYZ:
                    return new Point3D(center.X, center.Y + radius * cs, center.Z + radius * sn);

                case RenderMode.Mode3D:
                case RenderMode.Mode2DXY:
                default:
                    return new Point3D(center.X + radius * cs, center.Y + radius * sn, center.Z);
            }
        }

        private void AddToolCircle(Point3D center)
        {
            double radius = GetToolTraceDiameterInDisplayUnits() / 2d;

            if (radius <= 0d)
                return;

            double step = (Math.PI * 2d) / ToolCircleSegments;
            Point3D p0 = GetToolCirclePoint(center, radius, 0d);
            Point3DCollection circle = new Point3DCollection();

            for (int i = 1; i <= ToolCircleSegments; i++)
            {
                Point3D p1 = GetToolCirclePoint(center, radius, i * step);
                circle.Add(p0);
                circle.Add(p1);
                p0 = p1;
            }

            for (int i = ToolCircleHistoryCount - 1; i > 0; i--)
                toolCircleHistoryLayers[i] = toolCircleHistoryLayers[i - 1];

            toolCircleHistoryLayers[0] = circle;
        }

        private void RefreshToolCircleVisuals()
        {
            for (int i = 0; i < toolCircleVisuals.Count && i < toolCircleHistoryLayers.Count; i++)
                toolCircleVisuals[i].Points = toolCircleHistoryLayers[i];
        }

        private void ShowToolTrace(bool show)
        {
            if (show)
            {
                if (toolTrailVisual != null)
                    toolTrailVisual.Points = toolPathHistory;
                RefreshToolCircleVisuals();
            }
            else
            {
                if (toolTrailVisual != null)
                    toolTrailVisual.Points = null;
                for (int i = 0; i < toolCircleVisuals.Count; i++)
                    toolCircleVisuals[i].Points = null;
            }
        }

        private void ClearToolTraceCache()
        {
            toolPathHistory.Clear();

            foreach (var points in toolCircleHistoryLayers)
                points.Clear();

            Machine.ToolTrail = null;

            if (toolTrailVisual != null)
                toolTrailVisual.Points = null;

            RefreshToolCircleVisuals();
        }

        private void UpdateToolCircleColors()
        {
            if (toolCircleVisuals.Count == 0)
                return;

            const double minDim = 0.15d;
            double maxIndex = Math.Max(1d, ToolCircleHistoryCount - 1d);

            for (int i = 0; i < toolCircleVisuals.Count; i++)
            {
                double factor = minDim + (1d - minDim) * ((maxIndex - i) / maxIndex);
                byte alpha = (byte)Math.Max(1, Math.Min(255, (int)(Machine.ToolTrailColor.A * factor)));
                toolCircleVisuals[i].Color = Color.FromArgb(alpha, Machine.ToolTrailColor.R, Machine.ToolTrailColor.G, Machine.ToolTrailColor.B);
            }
        }

        private double GetToolTraceDiameter()
        {
            double parserToolDiameter = GCode.File.Parser.ToolDiameter;

            if (parserToolDiameter <= 0d)
                parserToolDiameter = GCode.File.Parser.LastToolDiameter;

            if (parserToolDiameter <= 0d)
            {
                Tool parserTool = GCode.File.Parser.SelectedTool;
                if (parserTool != null && parserTool.R > 0d)
                    parserToolDiameter = parserTool.R * 2d;
            }

            if (parserToolDiameter <= 0d && model != null)
            {
                string toolCode = model.Tool;
                if (!string.IsNullOrEmpty(toolCode))
                {
                    Tool workTool = GrblWorkParameters.Tools.Where(t => t.Code == toolCode).FirstOrDefault();
                    if (workTool != null && workTool.R > 0d)
                        parserToolDiameter = workTool.R * 2d;
                }
            }
            parserToolDiameter = Math.Abs(parserToolDiameter);
            return parserToolDiameter > 0d ? parserToolDiameter : AppConfig.Settings.GCodeViewer.ToolDiameter;
        }

        private double GetToolTraceDiameterInDisplayUnits()
        {
            double diameter = GetToolTraceDiameter();
            if (model != null && !model.IsMetric)
                diameter *= 25.4d;
            return diameter;
        }

        private void ShowConeTool()
        {
            if (toolAutoScale)
            {
                tool.Height = Math.Abs(viewport.Camera.Position.DistanceTo(Machine.ToolPosition)) / 1250d / ccamera.FieldOfView;
                tool.TopRadius = tool.Height / 5d;
            }
            tool.Origin = Machine.ToolPosition;
        }

        private void ShowCrosshairTool()
        {
            //            Machine.ToolOrigin = null;
            positionPoints.Clear();

            Action<Point3D, Point3D> addLine = (p, q) =>
            {
                positionPoints.Add(p);
                positionPoints.Add(q);
            };

            Position position = new Position(model.Position, model.UnitFactor);
            Position workPositionOffset = new Position(model.WorkPositionOffset, model.UnitFactor);

            switch (Machine.RenderMode)
            {
                case RenderMode.Mode3D:
                    if (GrblInfo.ForceSetOrigin)
                    {
                        positionPoints.Add(new Point3D(-workPositionOffset.X, position.Y, position.Z));
                        positionPoints.Add(new Point3D(positionPoints.Last().X + GrblInfo.MaxTravel.X, position.Y, position.Z));
                        positionPoints.Add(new Point3D(position.X, -workPositionOffset.Y, position.Z));
                    }
                    else
                    {
                        positionPoints.Add(new Point3D(-GrblInfo.MaxTravel.X - workPositionOffset.X, position.Y, position.Z + .05d));
                        positionPoints.Add(new Point3D(positionPoints.Last().X + GrblInfo.MaxTravel.X, position.Y, position.Z + .05d));
                        positionPoints.Add(new Point3D(position.X, -GrblInfo.MaxTravel.Y - workPositionOffset.Y, position.Z + .05d));
                    }
                    positionPoints.Add(new Point3D(position.X, positionPoints.Last().Y + GrblInfo.MaxTravel.Y, position.Z + .05d));
                    positionPoints.Add(new Point3D(position.X, position.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z));
                    positionPoints.Add(new Point3D(position.X, position.Y, positionPoints.Last().Z + GrblInfo.MaxTravel.Z + .05d));
                    break;

                case RenderMode.Mode2DXY:
                    if (GrblInfo.ForceSetOrigin)
                    {
                        positionPoints.Add(new Point3D(-workPositionOffset.X, position.Y, 0d));
                        positionPoints.Add(new Point3D(positionPoints.Last().X + GrblInfo.MaxTravel.X, position.Y, 0d));
                        positionPoints.Add(new Point3D(position.X, -workPositionOffset.Y, 0d));
                    }
                    else
                    {
                        positionPoints.Add(new Point3D(-GrblInfo.MaxTravel.X - workPositionOffset.X, position.Y, 0d));
                        positionPoints.Add(new Point3D(positionPoints.Last().X + GrblInfo.MaxTravel.X, position.Y, 0d));
                        positionPoints.Add(new Point3D(position.X, -GrblInfo.MaxTravel.Y - workPositionOffset.Y, 0d));
                    }
                    positionPoints.Add(new Point3D(position.X, positionPoints.Last().Y + GrblInfo.MaxTravel.Y, 0d));
                    break;

                case RenderMode.Mode2DXZ:
                    if (GrblInfo.ForceSetOrigin)
                        positionPoints.Add(new Point3D(-workPositionOffset.X, 0d, position.Z));
                    else
                        positionPoints.Add(new Point3D(-GrblInfo.MaxTravel.X - workPositionOffset.X, 0d, position.Z));
                    positionPoints.Add(new Point3D(positionPoints.Last().X + GrblInfo.MaxTravel.X, 0d, position.Z));
                    positionPoints.Add(new Point3D(position.X, 0d, -GrblInfo.MaxTravel.Z - workPositionOffset.Z));
                    positionPoints.Add(new Point3D(position.X, 0d, positionPoints.Last().Z + GrblInfo.MaxTravel.Z));
                    break;

                case RenderMode.Mode2DYZ:
                    if (GrblInfo.ForceSetOrigin)
                        positionPoints.Add(new Point3D(0d, -workPositionOffset.Y, position.Z));
                    else
                        positionPoints.Add(new Point3D(0d, -GrblInfo.MaxTravel.Y - workPositionOffset.Y, position.Z));
                    positionPoints.Add(new Point3D(0d, positionPoints.Last().Y + GrblInfo.MaxTravel.Y, position.Z));
                    positionPoints.Add(new Point3D(0d, position.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z));
                    positionPoints.Add(new Point3D(0d, model.Position.Y, positionPoints.Last().Z + GrblInfo.MaxTravel.Z));
                    break;
            }


            //if (isLatheMode == true)
            //    workEnvelope.BoundingBox = new Rect3D(-workPositionOffset.X, 0d, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, 0d, GrblInfo.MaxTravel.Z);
            //else if (GrblInfo.ForceSetOrigin)
            //    workEnvelope.BoundingBox = new Rect3D(-workPositionOffset.X, -workPositionOffset.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, GrblInfo.MaxTravel.Y, GrblInfo.MaxTravel.Z);
            //else
            //    workEnvelope.BoundingBox = new Rect3D(-GrblInfo.MaxTravel.X - workPositionOffset.X, -GrblInfo.MaxTravel.Y - workPositionOffset.Y, -GrblInfo.MaxTravel.Z - workPositionOffset.Z, GrblInfo.MaxTravel.X, GrblInfo.MaxTravel.Y, GrblInfo.MaxTravel.Z);

            Machine.ToolOrigin = positionPoints;
        }

        public void ShowPosition()
        {
            GrblViewModel model = DataContext as GrblViewModel;
            Position position = new Position(model.Position, model.UnitFactor);
            ProgramLimits programLimits = new ProgramLimits(model.ProgramLimits, model.UnitFactor);

            Machine.ToolOrigin = null;

            positionPoints.Clear();

            if (Machine.Limits.X == 0d)
                Machine.SetLimits(GrblInfo.MaxTravel.X, GrblInfo.MaxTravel.Y, GrblInfo.MaxTravel.Z);

            positionPoints.Add(new Point3D(Math.Min(position.X, programLimits.MinX) - 5d, position.Y, position.Z));
            positionPoints.Add(new Point3D(Machine.Limits.X, position.Y, position.Z));

            positionPoints.Add(new Point3D(position.X, Math.Min(position.Y, programLimits.MinY) - 5d, position.Z));
            positionPoints.Add(new Point3D(position.X, Machine.Limits.Y, position.Z));

            positionPoints.Add(new Point3D(position.X, position.Y, Math.Min(position.Z, programLimits.MinZ) - 5d));
            positionPoints.Add(new Point3D(position.X, position.Y, Machine.Limits.Z));

            Machine.ToolOrigin = positionPoints;
            var orgpos = Machine.StartPosition;
            Machine.SetStartPosition(position.X, position.Y, position.Z);

            if (Machine.RapidLines != null && Machine.RapidLines.Count > 0 && Machine.RapidLines[0].Equals(orgpos))
            {
                Machine.RapidLines.RemoveAt(0);
                Machine.RapidLines.Insert(0, Machine.StartPosition);
            }
        }

        void RenderExecuting(int block)
        {
            if (block <= 0)
            {
                if (Machine.ExecutedLines != null)
                    Machine.ExecutedLines.Clear();
                Machine.RapidLines = null;
                Machine.RetractLines = null;
                if (job != null)
                {
                    job.Dispose();
                    emu.SetStartPosition(Machine.StartPosition);
                    job = emu.Execute(tokens).GetEnumerator();
                    job.MoveNext();
                }
            }
            else if (job != null && block > 0)
            {
                while (job.Current.Token.LineNumber < block)
                {
                    job.MoveNext();
                    point0 = job.Current.Start;

                    switch (job.Current.Token.Command)
                    {
                        case Commands.G1:
                            AddCutMove(job.Current.End, true);
                            break;

                        case Commands.G2:
                        case Commands.G3:
                            DrawArc(job.Current.Token as GCArc, point0.ToArray(), emu.Plane, emu.DistanceMode == DistanceMode.Incremental);
                            break;

                        case Commands.G5:
                            DrawCubicSpline(job.Current.Token as GCCubicSpline, point0.ToArray());
                            break;

                        case Commands.G5_1:
                            DrawQuadraticSpline(job.Current.Token as GCQuadraticSpline, point0.ToArray());
                            break;
                    }
                }
            }
        }

        private double boffset(double w, double wh, double xs, double xm)
        {
            double v, vv = xm - Math.Floor(xm);

            v = xs / 2d + wh;

            return v;
        }

        private double gridsz(double travel)
        {
            travel = Math.Ceiling(travel - travel % TickSize + (travel % TickSize == 0d ? 0d : TickSize));

            if ((travel / 2d) % TickSize > 0d)
                travel += TickSize;

            return travel;
        }

        private Point3D getGridAdjust(Point3D offset)
        {
            Point3D gridfix;
            Position workPositionOffset = new Position(model.WorkPositionOffset, model.UnitFactor);
            ProgramLimits programLimits = new ProgramLimits(model.ProgramLimits, model.UnitFactor);

            if (model.LatheMode == LatheMode.Disabled)
            {
                switch (Machine.RenderMode)
                {
                    case RenderMode.Mode3D:
                    case RenderMode.Mode2DXY:
                        if (model.HomedState != HomedState.Homed)
                            gridfix = new Point3D(-programLimits.MinX, -programLimits.MinY, 0d);
                        else
                            gridfix = new Point3D(workPositionOffset.X, workPositionOffset.Y, 0d);
                        break;

                    case RenderMode.Mode2DXZ:
                        if (model.HomedState != HomedState.Homed)
                            gridfix = new Point3D(-programLimits.MinX, 0d, programLimits.MinZ);
                        else
                            gridfix = new Point3D(workPositionOffset.X, 0d, workPositionOffset.Z);
                        break;

                    default:
                        if (model.HomedState != HomedState.Homed)
                            gridfix = new Point3D(-programLimits.MinX, -programLimits.MinY, 0d);
                        else
                            gridfix = new Point3D(0d, workPositionOffset.Y, workPositionOffset.Z);
                        break;
                }
            }
            else
            {
                if (model.HomedState != HomedState.Homed)
                    gridfix = new Point3D(-programLimits.MinX, 0d, -programLimits.MinX);
                else
                    gridfix = new Point3D(workPositionOffset.X, 0d, workPositionOffset.Y);
            }

            return new Point3D(offset.X - gridfix.X, offset.Y - gridfix.Y, offset.Z - gridfix.Z);
        }

        public void showAdorners(ProgramLimits bbox)
        {
            if (model.UnitFactor != 1d)
                bbox = new ProgramLimits(bbox, model.UnitFactor);

            double lineThickness = bbox.MaxSize / 1000d;
            double arrowOffset = lineThickness * 30d;
            double labelOffset = lineThickness * 50d;

            AxisBrush = new SolidColorBrush(Machine.GridColor);
            gridWidth = gridsz(GrblInfo.MaxTravel.X);

            Position workPositionOffset = new Position(model.WorkPositionOffset, model.UnitFactor);
            ProgramLimits programLimits = new ProgramLimits(model.ProgramLimits, model.UnitFactor);

            if (isLatheMode == null)
            {
                if ((isLatheMode = model.LatheMode != LatheMode.Disabled) == true)
                {
                    viewport.ModelUpDirection = new Vector3D(0d, -1d, 0d);
                    if (tool != null)
                        tool.Normal = new Vector3D(1d, 0d, 0d);
                }
                Machine.DiameterMode = model.LatheMode == LatheMode.Diameter;
            }

            bool latheMode = isLatheMode == true;

            if (model.LatheMode == LatheMode.Disabled)
            {
                Vector3D lengthDirection, normal;
                gridHeight = gridsz(GrblInfo.MaxTravel.Y);

                switch (Machine.RenderMode)
                {
                    case RenderMode.Mode3D:
                    case RenderMode.Mode2DXY:
                        lengthDirection = new Vector3D(1d, 0d, 0d);
                        normal = new Vector3D(0d, 0d, 1d);
                        if (model.HomedState != HomedState.Homed || GrblInfo.ForceSetOrigin)
                            gridOffset = new Point3D(gridWidth / 2d, gridHeight / 2d, programLimits.MinZ);
                        else
                            gridOffset = new Point3D(-gridWidth / 2d, -gridHeight / 2d, programLimits.MinZ);
                        break;

                    case RenderMode.Mode2DXZ:
                        gridHeight = gridsz(GrblInfo.MaxTravel.Z);
                        lengthDirection = new Vector3D(1d, 0d, 0d);
                        normal = new Vector3D(0d, 1d, 0d);
                        if (model.HomedState != HomedState.Homed || GrblInfo.ForceSetOrigin)
                            gridOffset = new Point3D(gridWidth / 2d, 0d, -gridHeight / 2d);
                        else
                            gridOffset = new Point3D(-gridWidth / 2d, 0d, -gridHeight / 2d);
                        break;

                    default:
                        gridWidth = gridHeight;
                        gridHeight = gridsz(GrblInfo.MaxTravel.Z);
                        lengthDirection = new Vector3D(0d, 1d, 0d);
                        normal = new Vector3D(1d, 0d, 0d);
                        if (model.HomedState != HomedState.Homed || GrblInfo.ForceSetOrigin)
                            gridOffset = new Point3D(0d, gridWidth / 2d, -gridHeight / 2d);
                        else
                            gridOffset = new Point3D(0d, -gridWidth / 2d, -gridHeight / 2d);
                        break;
                }

                grid = new GridLinesVisual3D()
                {
                    Center = getGridAdjust(gridOffset),
                    MinorDistance = 2.5d,
                    MajorDistance = TickSize,
                    Width = gridHeight,
                    Length = gridWidth,
                    Thickness = 0.1d,
                    Fill = AxisBrush,
                    LengthDirection = lengthDirection,
                    Normal = normal
                };
            }
            else
            {
                gridHeight = gridsz(GrblInfo.MaxTravel.Z);
                gridOffset = new Point3D(gridWidth / 2d, 0d, -gridHeight / 2d);

                grid = new GridLinesVisual3D()
                {
                    Center = getGridAdjust(gridOffset),
                    MinorDistance = 2.5d,
                    MajorDistance = TickSize,
                    Width = gridWidth,
                    Length = gridHeight,
                    Thickness = lineThickness,
                    Fill = AxisBrush,
                    LengthDirection = new Vector3D(0d, 0d, 1d),
                    Normal = new Vector3D(0d, 1d, 0d)
                };
            }

            axes.Children.Add(new ArrowVisual3D()
            {
                Point2 = new Point3D(bbox.SizeX + arrowOffset, 0d, 0d),
                Diameter = lineThickness * 5,
                Fill = AxisBrush
            });

            axes.Children.Add(new BillboardTextVisual3D()
            {
                Text = "X",
                FontWeight = FontWeights.Bold,
                Foreground = AxisBrush,
                Position = new Point3D(bbox.SizeX + labelOffset, 0d, 0d)
            });

            if (bbox.SizeY > 0d)
            {
                axes.Children.Add(new ArrowVisual3D()
                {
                    Point2 = new Point3D(0d, bbox.SizeY + arrowOffset, 0d),
                    Diameter = lineThickness * 5d,
                    Fill = AxisBrush
                });

                axes.Children.Add(new BillboardTextVisual3D()
                {
                    Text = "Y",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(0d, bbox.SizeY + labelOffset, 0d)
                });
            }

            if (bbox.SizeZ > 0d)
            {
                axes.Children.Add(new ArrowVisual3D()
                {
                    Point1 = latheMode ? new Point3D(0d, 0d, bbox.MaxZ + arrowOffset) : new Point3D(0d, 0d, bbox.MinZ - arrowOffset),
                    Point2 = latheMode ? new Point3D(0d, 0d, bbox.MinZ - arrowOffset) : new Point3D(0d, 0d, bbox.MaxZ + arrowOffset),
                    Diameter = lineThickness * 5d,
                    Fill = AxisBrush,
                });

                axes.Children.Add(new BillboardTextVisual3D()
                {
                    Text = "Z",
                    FontWeight = FontWeights.Bold,
                    Foreground = AxisBrush,
                    Position = new Point3D(0d, 0d, latheMode ? bbox.MinZ - labelOffset : bbox.MaxZ + labelOffset)
                });
            }

            if (Machine.ShowAxes)
                viewport.Children.Add(Machine.Axes = axes);

            if (Machine.ShowGrid)
                viewport.Children.Add(Machine.Grid = grid);

            jobEnvelope.BoundingBox = new Rect3D(bbox.MinX, bbox.MinY, bbox.MinZ, bbox.SizeX, Math.Max(0.001d, bbox.SizeY), bbox.SizeZ);
            if (Machine.ShowJobEnvelope)
                Machine.JobEnvelope = jobEnvelope;

            AddWorkEnvelope();
            if (Machine.ShowWorkEnvelope)
                Machine.WorkEnvelope = workEnvelope;

            Machine.ToolMode = Machine.ToolMode;
        }

        public void Render(List<GCodeToken> tokens, bool refreshCamera = true)
        {
            var bbox = (DataContext as GrblViewModel).ProgramLimits;

            ClearViewport();

            renderedProgramLimits = new ProgramLimits(bbox, 1d);

            this.tokens = tokens;
            renderExecuted = RenderExecuted && !Machine.HighlightColor.Equals(Machine.CutMotionColor) && _animateSubscribed;

            cutCount = 0;
            point0 = Machine.StartPosition;
            lastType = MoveType.None;

            showAdorners(bbox);

            emu.SetStartPosition(Machine.StartPosition);

            foreach (var cmd in emu.Execute(tokens))
            {
                point0 = cmd.Start;

                switch (cmd.Token.Command)
                {
                    case Commands.G0:
                        if (cmd.IsRetract)
                            AddRetractMove(cmd.End);
                        else
                            AddRapidMove(cmd.End);
                        break;

                    case Commands.G1:
                        AddCutMove(cmd.End, true);
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
                        DrawCubicSpline(cmd.Token as GCCubicSpline, point0.ToArray());
                        break;

                    case Commands.G5_1:
                        DrawQuadraticSpline(cmd.Token as GCQuadraticSpline, point0.ToArray());
                        break;

                    case Commands.M0:
                    case Commands.M1:
                        AddPauseMarker(cmd.End, Colors.Red);
                        break;

                    case Commands.G4:
                        AddPauseMarker(cmd.End, Colors.Yellow);
                        break;
                }
            }

            Machine.RapidLines = rapidPoints;
            Machine.RetractLines = retractPoints;

            if (refreshCamera)
                RefreshView(bbox);
            else
                AnimateTool();

            if (RenderExecuted)
            {
                Machine.CutLines = new Point3DCollection(cutPoints);
                cutPoints.Clear();
                Machine.ExecutedLines = cutPoints;
                emu.SetStartPosition(Machine.StartPosition);
                job = emu.Execute(tokens).GetEnumerator();
                job.MoveNext();

            }
            else
                Machine.CutLines = cutPoints;

            if (tool != null)
                tool.TopRadius = GetToolTraceDiameterInDisplayUnits() / 2d;

            if (pauseMarkers.Count > 0 && !zoomSubscribed)
            {
                zoomSubscribed = true;
                viewport.PreviewMouseWheel += MouseWheel_Preview;
            }
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
                cutPoints.Add(cutPoints.Last());
                if (Machine.RenderMode == RenderMode.Mode3D)
                {
                    cutPoints.Add(point);
                }
                else switch (Machine.RenderMode)
                    {
                        case RenderMode.Mode2DXY:
                            cutPoints.Add(new Point3D(point.X, point.Y, 0d));
                            break;

                        case RenderMode.Mode2DXZ:
                            cutPoints.Add(new Point3D(point.X, 0d, point.Z));
                            break;

                        case RenderMode.Mode2DYZ:
                            cutPoints.Add(new Point3D(0d, point.Y, point.Z));
                            break;
                    }
            }

            if (lastType == MoveType.Cut)
                delta0 = new Vector3D(0d, 0d, 0d);

            if (Machine.RenderMode == RenderMode.Mode3D)
            {
                rapidPoints.Add(point0);
                rapidPoints.Add(point);
            }
            else switch (Machine.RenderMode)
                {
                    case RenderMode.Mode2DXY:
                        rapidPoints.Add(new Point3D(point0.X, point0.Y, 0d));
                        rapidPoints.Add(new Point3D(point.X, point.Y, 0d));
                        break;

                    case RenderMode.Mode2DXZ:
                        rapidPoints.Add(new Point3D(point0.X, 0d, point0.Z));
                        rapidPoints.Add(new Point3D(point.X, 0d, point.Z));
                        break;

                    case RenderMode.Mode2DYZ:
                        rapidPoints.Add(new Point3D(0d, point0.Y, point0.Z));
                        rapidPoints.Add(new Point3D(0d, point.Y, point.Z));
                        break;
                }

            cutCount = 0;
            lastType = MoveType.Rapid;
            point0 = point;
        }

        public void AddRetractMove(Point3D point)
        {
            if (cutCount > 1)
            {
                cutPoints.Add(cutPoints.Last());
                if (Machine.RenderMode == RenderMode.Mode3D)
                {
                    cutPoints.Add(point);
                }
                else switch (Machine.RenderMode)
                    {
                        case RenderMode.Mode2DXY:
                            cutPoints.Add(new Point3D(point.X, point.Y, 0d));
                            break;

                        case RenderMode.Mode2DXZ:
                            cutPoints.Add(new Point3D(point.X, 0d, point.Z));
                            break;

                        case RenderMode.Mode2DYZ:
                            cutPoints.Add(new Point3D(0d, point.Y, point.Z));
                            break;
                    }
            }

            if (lastType == MoveType.Cut)
                delta0 = new Vector3D(0d, 0d, 0d);

            if (Machine.RenderMode == RenderMode.Mode3D)
            {
                retractPoints.Add(point0);
                retractPoints.Add(point);
            }
            else switch (Machine.RenderMode)
                {
                    case RenderMode.Mode2DXY:
                        retractPoints.Add(new Point3D(point0.X, point0.Y, 0d));
                        retractPoints.Add(new Point3D(point.X, point.Y, 0d));
                        break;

                    case RenderMode.Mode2DXZ:
                        retractPoints.Add(new Point3D(point0.X, 0d, point0.Z));
                        retractPoints.Add(new Point3D(point.X, 0d, point.Z));
                        break;

                    case RenderMode.Mode2DYZ:
                        retractPoints.Add(new Point3D(0d, point0.Y, point0.Z));
                        retractPoints.Add(new Point3D(0d, point.Y, point.Z));
                        break;
                }
            cutCount = 0;
            lastType = MoveType.Retract;
            point0 = point;
        }

        public void AddCutMove(Point3D point, bool force = false)
        {
            bool sameDir = false;

            if (!force && lastType == MoveType.Cut && minDistanceSquared > 0d)
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
                    cutPoints.RemoveAt(cutPoints.Count - 1);
                    if (Machine.RenderMode == RenderMode.Mode3D)
                    {
                        cutPoints.Add(point);
                    }
                    else switch (Machine.RenderMode)
                        {
                            case RenderMode.Mode2DXY:
                                cutPoints.Add(new Point3D(point.X, point.Y, 0d));
                                break;

                            case RenderMode.Mode2DXZ:
                                cutPoints.Add(new Point3D(point.X, 0d, point.Z));
                                break;

                            case RenderMode.Mode2DYZ:
                                cutPoints.Add(new Point3D(0d, point.Y, point.Z));
                                break;
                        }
                    delta0 += delta;
                    delta0.Normalize();
                    cutCount++;
                }
                else
                {
                    if ((point - point0).LengthSquared < minDistanceSquared)
                        return;  // less than min distance from last point
                                 //      delta0 = delta;
                }
            }

            if (!sameDir)
            {
                cutCount = 1;
                if (Machine.RenderMode == RenderMode.Mode3D)
                {
                    cutPoints.Add(point0);
                    cutPoints.Add(point);
                }
                else switch (Machine.RenderMode)
                    {
                        case RenderMode.Mode2DXY:
                            cutPoints.Add(new Point3D(point0.X, point0.Y, 0d));
                            cutPoints.Add(new Point3D(point.X, point.Y, 0d));
                            break;

                        case RenderMode.Mode2DXZ:
                            cutPoints.Add(new Point3D(point0.X, 0d, point0.Z));
                            cutPoints.Add(new Point3D(point.X, 0d, point.Z));
                            break;

                        case RenderMode.Mode2DYZ:
                            cutPoints.Add(new Point3D(0d, point0.Y, point0.Z));
                            cutPoints.Add(new Point3D(0d, point.Y, point.Z));
                            break;
                    }
            }

            lastType = MoveType.Cut;
            point0 = point;
        }

        private void DrawArc(GCArc arc, double[] start, GCPlane plane, bool isRelative = false)
        {
            List<Point3D> points = arc.GeneratePoints(plane, start, ArcResolution, isRelative); // Dynamic resolution
            int last = points.Count - 1;

            for (int i = 0; i < points.Count; i++)
                AddCutMove(points[i], i == last);
        }

        private void DrawCubicSpline(GCCubicSpline spline, double[] start, bool isRelative = false)
        {
            List<Point3D> points = spline.GeneratePoints(start, ArcResolution, isRelative); // Dynamic resolution

            foreach (Point3D point in points)
                AddCutMove(point);
        }

        private void DrawQuadraticSpline(GCQuadraticSpline spline, double[] start, bool isRelative = false)
        {
            List<Point3D> points = spline.GeneratePoints(start, ArcResolution, isRelative); // Dynamic resolution

            foreach (Point3D point in points)
                AddCutMove(point);
        }

        private void AddPauseMarker(Point3D position, Color color)
        {
            double markerSize = Math.Abs(viewport.Camera.Position.DistanceTo(position)) / 8250d / ccamera.FieldOfView;
            Point3D markerCenter = position;

            switch (Machine.RenderMode)
            {
                case RenderMode.Mode2DXY:
                    markerCenter = new Point3D(position.X, position.Y, 0d);
                    break;

                case RenderMode.Mode2DXZ:
                    markerCenter = new Point3D(position.X, 0d, position.Z);
                    break;

                case RenderMode.Mode2DYZ:
                    markerCenter = new Point3D(0d, position.Y, position.Z);
                    break;
            }

            var marker = new BoxVisual3D
            {
                Center = markerCenter,
                Width = markerSize,
                Height = markerSize,
                Length = markerSize,
                Fill = new SolidColorBrush(color)
            };

            pauseMarkers.Add(marker);
            viewport.Children.Add(marker);
        }

        public void UpdatePauseMarkerSizes()
        {
            foreach (var marker in pauseMarkers)
            {
                double markerSize = Math.Abs(viewport.Camera.Position.DistanceTo(marker.Center)) / 8250d / ccamera.FieldOfView;
                marker.Width = markerSize;
                marker.Height = markerSize;
                marker.Length = markerSize;
            }
        }
    }
}
