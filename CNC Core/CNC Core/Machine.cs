/*
 * Machine.cs - part of CNC Core library
 *
 * v0.18 / 2020-04-20 / Io Engineering (Terje Io)
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

using System.Collections.Generic;
using System.Linq;
using CNC.GCode;
using System.Windows.Media.Media3D;

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

            LatheMode = GrblParserState.LatheMode;
            isRelative = GrblParserState.DistanceMode == DistanceMode.Incremental;
            IsImperial = !GrblParserState.IsMetric;

            // TODO: set from parser state
            CoolantState = CoolantState.Off;
            SpindleState = SpindleState.Off;
            LatheMode = LatheMode.Disabled;
            DistanceMode = DistanceMode.Absolute;
            ToolLengthOffset = ToolLengthOffset.Cancel;
            IJKMode = IJKMode.Incremental;
            IsScaled = false;
            Tool = 0;
            Feedrate = 0d;
            SelectedTool = null;
            // end TODO

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
        public Tool SelectedTool { get; protected set; }
        public bool IsImperial { get; protected set; }
        public bool IsScaled { get; protected set; }

        protected void setEndP(double[] values, AxisFlags axisFlags)
        {
            machinePos.Set(values, axisFlags, isRelative);
        }

        public bool SetCoordinateSystem(GCCoordinateSystem token)
        {
            var csys = coordinateSystems.Where(x => x.Code == token.Code).FirstOrDefault();

            if (csys != null)
                foreach (int i in token.AxisFlags.ToIndices())
                    csys.Values[i] = token.Values[i];

            return csys != null;
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
