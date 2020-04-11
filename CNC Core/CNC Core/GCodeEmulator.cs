/*
 * GCodeEmulator.cs - part of CNC Controls library
 *
 * v0.15 / 2020-04-11 / Io Engineering (Terje Io)
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
using System.Windows.Media.Media3D;
using CNC.GCode;
using System;

namespace CNC.Core
{
    public struct RunAction
    {
        public Point3D Start;
        public Point3D End;
        public GCodeToken Token;
        public bool IsRetract;
        public bool IsSpindleSynced;
        public bool IsInMachineCoord;
        public uint LineNumber;
    }

    public class GCodeEmulator
    {
        private List<CoordinateSystem> coordinateSystems = new List<CoordinateSystem>();
        private CoordinateSystem coordinateSystem, g28, g30, g92;
        private double[] offsets = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };
        private double[] origin = new double[6] { 0d, 0d, 0d, 0d, 0d, 0d };
        private bool canned;
        private RunAction action;
        private bool isRelative = false;
        Point6D machinePos = new Point6D();
        Point6D absPos = new Point6D();

        private double _rpm = 0d;

        public GCodeEmulator()
        {
            coordinateSystems.Clear();
            foreach (CoordinateSystem c in GrblWorkParameters.CoordinateSystems)
                coordinateSystems.Add(c);

            coordinateSystem = coordinateSystems.Where(x => x.Code == GrblParserState.WorkOffset).FirstOrDefault();
            g28 = coordinateSystems.Where(x => x.Code == "G28").FirstOrDefault();
            g30 = coordinateSystems.Where(x => x.Code == "G30").FirstOrDefault();
            g92 = coordinateSystems.Where(x => x.Code == "G92").FirstOrDefault();


            LatheMode = GrblParserState.LatheMode;
            isRelative = GrblParserState.DistanceMode == DistanceMode.Incremental;

            foreach (int i in AxisFlags.All.ToIndices())
            {
                offsets[i] = coordinateSystem.Values[i];
                origin[i] = g92.Values[i];
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

        public GCPlane Plane { get; private set; }
        public DistanceMode DistanceMode { get { return isRelative ? DistanceMode.Incremental : DistanceMode.Absolute; } }
        public LatheMode LatheMode { get; private set; }
        public CoolantState CoolantState { get; private set; } = CoolantState.Off;
        public SpindleState SpindleState { get; private set; } = SpindleState.Off;
        public double SpindleRPM { get { return SpindleState == SpindleState.Off ? 0d : _rpm; } }
        public double Feedrate { get; private set; } = 0d;
        public int Tool { get; private set; } = 0;
        public bool IsMetric { get; private set; } = true;

        public void SetStartPosition(Point3D pos)
        {
            action.Start = pos;
        }

        public IEnumerable<RunAction> Execute(List<GCodeToken> Tokens)
        {
            foreach (GCodeToken token in Tokens)
            {
                if (action.LineNumber != token.LineNumber)
                    action.IsInMachineCoord = false;

                action.IsInMachineCoord = false;
                action.Token = token;
                action.IsRetract = action.IsSpindleSynced = false;

                switch (token.Command)
                {
                    case Commands.G0:
                    case Commands.G1:
                        {
                            var motion = token as GCLinearMotion;
                            setEndP(motion.Values, motion.AxisFlags);
                        }
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        {
                            var arc = token as GCArc;
                            setEndP(arc.Values, arc.AxisFlags);
                        }
                        break;

                    case Commands.G5:
                        {
                            var spline = token as GCSpline;
                            setEndP(spline.Values, spline.AxisFlags);
                        }
                        break;

                    case Commands.G7:
                        LatheMode = LatheMode.Diameter;
                        break;

                    case Commands.G8:
                        LatheMode = LatheMode.Radius;
                        break;

                    case Commands.G10:
                        {
                            if (token is GCCoordinateSystem)
                            {
                                CoordinateSystem csys;
                                GCCoordinateSystem gcsys = token as GCCoordinateSystem;
                                if (gcsys.P == 0)
                                    csys = coordinateSystem;
                                else
                                    csys = coordinateSystems.Where(x => x.Code == gcsys.Code).FirstOrDefault();
                                foreach (int i in gcsys.AxisFlags.ToIndices())
                                {
                                    csys.Values[i] = gcsys.Values[i];
                                    if (gcsys.P == 0)
                                        offsets[i] = csys.Values[i];
                                }
                            }
                        }
                        break;

                    case Commands.G17:
                    case Commands.G18:
                    case Commands.G19:
                        Plane = (GCPlane)token;
                        break;

                    case Commands.G20:
                        // Strip G20 for now - Tokens are metric and needs to be transformed back...
                        //IsMetric = false;
                        action.Token.Command = Commands.Undefined;
                        break;

                    case Commands.G21:
                        IsMetric = true;
                        break;

                    case Commands.G28:
                        {
                            var motion = token as GCLinearMotion;
                            AxisFlags axisFlags;
                            if (motion.AxisFlags != AxisFlags.None)
                            {
                                axisFlags = motion.AxisFlags;
                                setEndP(motion.Values, motion.AxisFlags);
                                action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, motion.AxisFlags);
                                yield return action;
                                action.Start = action.End;
                            } else
                                axisFlags = AxisFlags.All;

                            foreach (int i in axisFlags.ToIndices())
                                machinePos[i] = g28.Values[i] - offsets[i] - origin[i];
                            action.End = machinePos.Point3D;
                            action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.All);
                            yield return action;
                            action.Start = action.End;
                        }
                        break;      

                    case Commands.G28_1:
                        {
                            for (int i = 0; i < g28.Values.Length; i++)
                                g28.Values[i] = offsets[i];
                        }
                        break;

                    case Commands.G30:
                        {
                            var motion = token as GCLinearMotion;
                            AxisFlags axisFlags;
                            if (motion.AxisFlags != AxisFlags.None)
                            {
                                axisFlags = motion.AxisFlags;
                                setEndP(motion.Values, motion.AxisFlags);
                                action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, motion.AxisFlags);
                                yield return action;
                                action.Start = action.End;
                            }
                            else
                                axisFlags = AxisFlags.All;

                            foreach (int i in axisFlags.ToIndices())
                                machinePos[i] = g30.Values[i] - offsets[i] - origin[i];
                            action.End = machinePos.Point3D;
                            action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.All);
                            yield return action;
                            action.Start = action.End;
                        }
                        break;

                    case Commands.G30_1:
                        {
                            for (int i = 0; i < g30.Values.Length; i++)
                                g30.Values[i] = offsets[i];
                        }
                        break;

                    case Commands.G33:
                        {
                            var motion = token as GCSyncMotion;
                            setEndP(motion.Values, motion.AxisFlags);
                            action.IsSpindleSynced = true;
                            action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, motion.AxisFlags);
                            yield return action;
                            action.IsSpindleSynced = false;
                            action.Start = action.End;
                        }
                        break;

                    case Commands.G53:
                        {
                            var motion = token as GCAbsLinearMotion;
                            action.IsInMachineCoord = true;
                            if (action.IsInMachineCoord)
                            {
                                foreach (int i in motion.AxisFlags.ToIndices())
                                    machinePos[i] = motion.Values[i] - offsets[i] - origin[i];
                                action.End = machinePos.Point3D;
                            }
                            action.Token = new GCLinearMotion(motion.Motion, token.LineNumber, machinePos.Array, motion.AxisFlags);
                            yield return action;
                            action.Start = action.End;
                        }
                        break;

                    case Commands.G54:
                    case Commands.G55:
                    case Commands.G56:
                    case Commands.G57:
                    case Commands.G58:
                    case Commands.G59:
                    case Commands.G59_1:
                    case Commands.G59_2:
                    case Commands.G59_3:
                        {
                            string cs = token.Command.ToString().Replace('_', '.');
                            coordinateSystem = coordinateSystems.Where(x => x.Code == cs).FirstOrDefault();
                            foreach (int i in AxisFlags.All.ToIndices()) // GrblInfo.AxisFlags?
                                offsets[i] = coordinateSystem.Values[i];
                            //    CoordinateSystem = GrblWorkParameters.CoordinateSystems();
                            //GCCoordinateSystem cs = (GCCoordinateSystem)token;
                            // TODO: handle offsets... Need to read current from grbl
                        }
                        break;

                    case Commands.G76:
                        {
                            var thread = token as GCThreadingMotion;
                            uint pass = 1, passes = 0;
                            double doc = thread.InitialDepth, thread_length, main_taper_height = 0d;
                            double t_end_taper_length = thread.TaperLength;
                            double end_tapers = thread.ThreadTaper == ThreadTaper.None ? 0d : (thread.ThreadTaper == ThreadTaper.Both ? 2d : 1d);
                            double infeed_factor = Math.Tan(thread.InfeedAngle * Math.PI / 180d), infeed_offset = 0d;
                            double target_z = thread.Z + thread.Depth * infeed_factor, start_z = action.Start.Z;

                            if (thread.Z > action.Start.Z)
                                infeed_factor = -infeed_factor;

                            var origin = action.End; 

                            // Calculate number of passes

                            while (thread.CalculateDOC(++passes) < thread.Depth);

                            passes += thread.SpringPasses + 1; // TODO: skip rendering of spring passes?

                            if ((thread_length = thread.Z - action.Start.Z) > 0.0f)
                                t_end_taper_length = -t_end_taper_length;

                            thread_length += thread.TaperLength * end_tapers;

                            //if (thread->main_taper_height != 0.0f)
                            //    thread->main_taper_height = thread->main_taper_height * thread_length / (thread_length - thread->end_taper_length * end_taper_factor);

                            // Initial Z-move for compound slide angle offset.
                            if (infeed_factor != 0d)
                            {
                                infeed_offset = doc * infeed_factor;
                                action.End.X = origin.X + (doc - thread.Depth) * thread.CutDirection;
                                action.End.Z -= infeed_offset;
                                action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.XZ);
                                yield return action;
                                action.Start = action.End;
                            }

                            while (--passes > 0)
                            {
                                //action.End.X = origin.X + (doc - thread.Depth) * thread.CutDirection;
                                //action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.X);
                                //yield return action;
                                //action.Start = action.End;

                                // Cut thread pass

                                // 1. Entry taper
                                if (thread.ThreadTaper.HasFlag(ThreadTaper.Entry))
                                {
                                    action.IsSpindleSynced = true;

                                    // TODO: move this segment outside of synced motion?
                                    action.End.X = origin.X + (thread.Peak + doc - thread.Depth) * thread.CutDirection;
                                    action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, AxisFlags.X);
                                    yield return action;
                                    action.Start = action.End;

                                    action.End.X = origin.X + (thread.Peak + doc) * thread.CutDirection;
                                    action.End.Z -= t_end_taper_length;
                                    action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, AxisFlags.XZ);
                                    yield return action;
                                    action.Start = action.End;
                                }
                                else
                                {
                                    // 2. Rapid to DOC
                                    action.End.X = origin.X + (thread.Peak + doc) * thread.CutDirection;
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.X);
                                    yield return action;
                                    action.Start = action.End;
                                    action.IsSpindleSynced = true;
                                }

                                // 3. Exit taper
                                if (thread.ThreadTaper.HasFlag(ThreadTaper.Exit))
                                {
                                    action.End.Z = target_z - infeed_offset + t_end_taper_length;
                                    action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, AxisFlags.Z);
                                    yield return action;
                                    action.Start = action.End;

                                    action.End.X = origin.X + (thread.Peak + doc - thread.Depth) * thread.CutDirection;
                                    action.End.Z -= t_end_taper_length;
                                    action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, AxisFlags.XZ);
                                    yield return action;
                                    action.Start = action.End;
                                }
                                else
                                {
                                    // 2. Main part
                                    action.End.Z = target_z - infeed_offset;
                                    action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, AxisFlags.Z);
                                    yield return action;
                                    action.Start = action.End;
                                }

                                action.IsSpindleSynced = false;

                                if (passes > 1)
                                {
                                    // Get DOC of next pass.
                                    doc = Math.Min(thread.CalculateDOC(++pass), thread.Depth);

                                    // 4. Retract
                                    action.End.X = origin.X + (doc - thread.Depth) * thread.CutDirection;
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.X);
                                    yield return action;
                                    action.Start = action.End;

                                    // 5. Back to start, add compound slide angle offset when commanded.
                                    infeed_offset = infeed_factor != 0d ? doc * infeed_factor : 0d;
                                    action.End.Z = origin.Z - infeed_offset;
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.Z);
                                    yield return action;
                                    action.Start = action.End;
                                }
                                else
                                {
                                    // 6. Retract to target position
                                    doc = thread.Depth;
                                    action.End.X = origin.X;
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.X);
                                    yield return action;
                                    action.Start = action.End;
                                }
                            }
                        }
                        break;

                    case Commands.G80:
                        canned = false;
                        break;

                    case Commands.G73:
                    case Commands.G81:
                    case Commands.G82:
                    case Commands.G83:
                    case Commands.G85:
                    case Commands.G86:
                    case Commands.G89: // TODO: add plane handling
                        {
                            bool wasRelative = isRelative;
                            GCCannedDrill drill = (token as GCCannedDrill);
                            double r = isRelative ? action.End.Z + drill.R : drill.R;
                            double z = isRelative ? action.End.Z + drill.Z : drill.Z;
                            uint repeats = DistanceMode == DistanceMode.Incremental ? drill.L : 1; // no need to draw absolute repeats(?)

                            setEndP(drill.Values, AxisFlags.XY);

                            isRelative = false;

                            if (!canned)
                            {
//                                canned = true;
                                if (action.End.Z < r)
                                {
                                    double[] start = new double[] { action.Start.X, action.Start.Y, r };
                                    setEndP(start, AxisFlags.Z);
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, start, AxisFlags.Z);
                                    yield return action;
                                    action.Start = action.End;
                                }
                            }

                            double[] values = new double[] { action.End.X, action.End.Y, action.End.Z };

                            setEndP(values, AxisFlags.X | AxisFlags.Y);
                            action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, values, AxisFlags.XY);
                            yield return action;
                            action.Start = action.End;

                            do
                            {
                                values[2] = z;
                                setEndP(values, AxisFlags.Z);
                                action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, values, AxisFlags.Z);
                                yield return action;
                                action.Start = action.End;

                                values[2] = r;
                                setEndP(values, AxisFlags.Z);
                                action.IsRetract = true;
                                action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, values, AxisFlags.Z);
                                yield return action;
                                action.Start = action.End;
                                action.IsRetract = false;

                                if (repeats > 1)
                                {
                                    values[0] += drill.X;
                                    values[1] += drill.Y;
                                    setEndP(values, AxisFlags.X | AxisFlags.Y);
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, values, AxisFlags.XY);
                                    yield return action;
                                    action.Start = action.End;
                                }
                            } while (--repeats > 0);

                            isRelative = wasRelative;
                            action.Token = token;
                        }
                        break;

                    case Commands.G90:
                    case Commands.G91:
                        isRelative = (token as GCDistanceMode).DistanceMode == DistanceMode.Incremental;
                        break;

                    case Commands.G92:
                        {
                            var cs = token as GCCoordinateSystem;
                            foreach (int i in cs.AxisFlags.ToIndices())
                                origin[i] = g92.Values[i] = cs.Values[i];
                        }
                        break;

                    case Commands.G92_1:
                        {
                            for (int i = 0; i < origin.Length; i++)
                                origin[i] = g92.Values[i] = 0d;
                        }
                        break;

                    case Commands.G92_2:
                        {
                            for (int i = 0; i < origin.Length; i++)
                                origin[i] = 0d;
                        }
                        break;

                    case Commands.M3:
                    case Commands.M4:
                    case Commands.M5:
                        SpindleState = (token as GCSpindleState).SpindleState;
                        break;

                    case Commands.M7:
                    case Commands.M8:
                    case Commands.M9:
                        CoolantState = (token as GCCoolantState).CoolantState;
                        break;

                    case Commands.Feedrate:
                        Feedrate = (token as GCFeedrate).Feedrate;
                        break;

                    case Commands.SpindleRPM:
                        _rpm = (token as GCSpindleRPM).SpindleRPM;
                        break;

                    case Commands.M61:
                    case Commands.ToolSelect:
                        Tool = (token as GCToolSelect).Tool;
                        break;
                }

                if(action.Token.Command != Commands.Undefined)
                    yield return action;

                action.Start = action.End;
            }
        }

        private Point3D setEndP(double[] values, AxisFlags axisFlags)
        {
            machinePos.Set(values, axisFlags, isRelative);

            action.End = machinePos.Point3D;

            return action.End;
        }
    }
}
