/*
 * Machine.cs - part of CNC Core library
 *
 * v0.36 / 2021-11-30 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2021, Io Engineering (Terje Io)
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

using System.Collections.Generic;
using System.Linq;
using CNC.GCode;
using System.Windows.Media.Media3D;
using System.Globalization;

namespace CNC.Core
{
    public class Machine
    {
        protected double _rpm = 0d;
        protected bool isRelative = false;

        protected int _tool = 0;
        protected double[] offsets = new double[6];
        protected double[] origin = new double[6];
        protected double[] scaleFactors = new double[6];
        protected double[] toolOffsets = new double[6];
        protected List<CoordinateSystem> coordinateSystems = new List<CoordinateSystem>();
        protected CoordinateSystem coordinateSystem, g28, g30, g92;
        protected List<Tool> toolTable = new List<Tool>();
        protected Point6D machinePos = new Point6D();

        public void Reset()
        {
            // Sync with controller
            if (GrblInfo.IsGrblHAL)
            {
                GrblParserState.Get();
                GrblWorkParameters.Get();
            }
            else
                GrblParserState.Get(true);

            coordinateSystems.Clear();
            foreach (CoordinateSystem c in GrblWorkParameters.CoordinateSystems)
                coordinateSystems.Add(c);

            toolTable.Clear();
            if (GrblInfo.NumTools > 0)
                foreach (Tool t in GrblWorkParameters.Tools)
                    toolTable.Add(t);

            coordinateSystem = coordinateSystems.Where(x => x.Code == GrblParserState.WorkOffset).FirstOrDefault();
            g28 = coordinateSystems.Where(x => x.Code == "G28").FirstOrDefault();
            g30 = coordinateSystems.Where(x => x.Code == "G30").FirstOrDefault();
            g92 = coordinateSystems.Where(x => x.Code == "G92").FirstOrDefault();

            isRelative = GrblParserState.DistanceMode == DistanceMode.Incremental;
            IsImperial = !GrblParserState.IsMetric;
            CoolantState = GrblParserState.CoolantState;
            SpindleState = GrblParserState.SpindleState;
            LatheMode = GrblParserState.LatheMode;
            DistanceMode = GrblParserState.DistanceMode;
            ToolLengthOffset = GrblParserState.ToolLengthOffset;
            FeedRateMode = GrblParserState.FeedRateMode;
            MotionMode = GrblParserState.MotionMode;
            IJKMode = GrblParserState.IJKMode;
            IsScaled = GrblParserState.IsActive("G51") != null;
            string val = GrblParserState.IsActive("F");
            Feedrate = val == null ? 0d : double.Parse(val, CultureInfo.InvariantCulture);
            val = GrblParserState.IsActive("S");
            _rpm = val == null ? 0d : double.Parse(val, CultureInfo.InvariantCulture);
            _tool = GrblParserState.Tool == GrblConstants.NO_TOOL ? 0 : int.Parse(GrblParserState.Tool);
            SelectedTool = null;
            RetractOldZ = GrblParserState.IsActive("G99") == null;
            SpindleRpmMode = GrblParserState.IsActive("G96") == null;
            G92Active = GrblParserState.IsActive("G92") != null;

            Line = 0;

            foreach (int i in AxisFlags.All.ToIndices())
            {
                offsets[i] = coordinateSystem == null ? 0d :  coordinateSystem.Values[i];
                origin[i] = g92 == null ? 0d : g92.Values[i];
                machinePos[i] = 0d;
                scaleFactors[i] = 1d;
                toolOffsets[i] = SelectedTool == null ? 0d : SelectedTool.Values[i];
            }

            switch (GrblParserState.Plane)
            {
                case GCode.Plane.XY:
                    Plane = new GCPlane(Commands.G17, 0);
                    break;

                case GCode.Plane.XZ:
                    Plane = new GCPlane(Commands.G18, 0);
                    break;

                case GCode.Plane.YZ:
                    Plane = new GCPlane(Commands.G19, 0);
                    break;
            }
        }

        public int Line { get; protected set; }
        public int CoordSystem {
            get {
                return coordinateSystem == null ? 0 : coordinateSystem.Id;
            }
            set
            {
                coordinateSystem = coordinateSystems.Where(x => x.Id == value).FirstOrDefault();
            }
        }
        public MotionMode MotionMode { get; protected set; }
        public FeedRateMode FeedRateMode { get; protected set; }
        public DistanceMode DistanceMode
        {
            get { return isRelative ? DistanceMode.Incremental : DistanceMode.Absolute; }
            set { isRelative = value == DistanceMode.Incremental; }
        }
        public GCPlane Plane { get; protected set; }
        public IJKMode IJKMode { get; protected set; }
        public LatheMode LatheMode { get; protected set; }
        public CoolantState CoolantState { get; protected set; }
        public SpindleState SpindleState { get; protected set; }
        public ToolLengthOffset ToolLengthOffset { get; protected set; }
        public bool SpindleRpmMode { get; protected set; }
        public double SpindleRPM { get { return SpindleState == SpindleState.Off ? 0d : _rpm; } }
        public double Feedrate { get; protected set; }
        public int Tool {
            get { return _tool; }
            protected set
            {
                _tool = value;
                if (toolTable.Count > 0)
                    SelectedTool = toolTable.Where(t => t.Code == _tool.ToString()).FirstOrDefault();
            }
        }
        public bool RetractOldZ { get; protected set; }
        public Tool SelectedTool { get; protected set; }
        public bool IsImperial { get; protected set; }
        public bool IsScaled { get; protected set; }
        public double GetScaleFactor(int axis)
        {
            return axis < GrblInfo.NumAxes ? scaleFactors[axis] : 0d;
        }

        public double GetPosition(int axis)
        {
            return machinePos[axis];
        }

        public double GetG28Position (int axis)
        {
            return g28 != null && axis < GrblInfo.NumAxes ? g28.Values[axis] : 0d;
        }
        public double GetG30Position(int axis)
        {
            return g30 != null && axis < GrblInfo.NumAxes ? g30.Values[axis] : 0d;
        }
        public bool G92Active { get; protected set; }
        public void G92Clear()
        {
            if (g92 != null)
                g92.Clear();

            G92Active = false;
        }
        public double GetG92Offset(int axis)
        {
            return g92 != null && axis < GrblInfo.NumAxes ? g92.Values[axis] : 0d;
        }

        public bool SetG92Offset(GCCoordinateSystem token)
        {
            if ((G92Active = g92 != null))
                foreach (int i in token.AxisFlags.ToIndices())
                    g92.Values[i] = token.Values[i];

            return G92Active;
        }

        protected void setEndP(double[] values, AxisFlags axisFlags)
        {
            machinePos.Set(values, axisFlags, isRelative);
        }

        public CoordinateSystem GetCoordSystem(int id)
        {
            return coordinateSystems[id];
        }

        public bool SetCoordinateSystem(GCCoordinateSystem token)
        {
            var csys = coordinateSystems.Where(x => x.Code == token.Code).FirstOrDefault();

            if (csys != null)
                foreach (int i in token.AxisFlags.ToIndices())
                    csys.Values[i] = token.Values[i];

            return csys != null;
        }

        public double GetToolOffset(int axis)
        {
            return toolOffsets[axis];
        }

        public bool SetToolOffset(GCToolOffset token)
        {
            var tool = SelectedTool;
            if (token.H != 0)
                tool = toolTable.Where(t => t.Code == token.H.ToString()).FirstOrDefault();
            if (tool != null && tool.Code != 0.ToString())
                foreach (int i in AxisFlags.All.ToIndices())
                    toolOffsets[i] = tool.Values[i];

            return tool != null;
        }

        public bool AddToolOffset(GCToolOffset token)
        {
            var tool = toolTable.Where(t => t.Code == token.H.ToString()).FirstOrDefault();
            if (tool != null)
                foreach (int i in AxisFlags.All.ToIndices())
                    toolOffsets[i] += tool.Values[i];

            return tool != null;
        }

        public void DynamicToolOffset(GCToolOffsets token)
        {
            foreach (int i in token.AxisFlags.ToIndices())
                toolOffsets[i] -= token.Values[i];
        }

        public void CancelToolCompensation()
        {
            foreach (int i in AxisFlags.All.ToIndices())
                toolOffsets[i] = 0d;
        }

        public bool SetToolTable(GCToolTable token)
        {
            var tool = toolTable.Where(t => t.Code == token.P.ToString()).FirstOrDefault();
            if (tool != null)
            {
                foreach (int i in token.AxisFlags.ToIndices())
                {
                    tool.Values[i] = token.Values[i];
                    if(tool == SelectedTool)
                        toolOffsets[i] = tool.Values[i];
                }

                if (!double.IsNaN(token.R))
                    tool.R = token.R;
            }

            return tool != null;
        }
    }
}
