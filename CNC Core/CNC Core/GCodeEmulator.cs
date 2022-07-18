/*
 * GCodeEmulator.cs - part of CNC Controls library
 *
 * v0.40 / 2022-07-12 / Io Engineering (Terje Io)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using CNC.GCode;

namespace CNC.Core
{
    public struct RunAction
    {
        public Point3D Start;
        public Point3D End;
        public GCodeToken Token;
        public bool IsRetract;
        public bool IsSpindleSynced;
        public bool IsScaled;
        public Point3D ScaleFactors;
        public bool IsInMachineCoord;
        public uint LineNumber;
    }

    public class GCodeEmulator : Machine
    {
        private bool translate;
        private RunAction action;
        private double retractPosition = double.NaN;

        public GCodeEmulator(bool translate = false) : base()
        {
            this.translate = translate;
        }

        public void SetStartPosition(Point3D pos)
        {
            action.Start = pos;
        }

        public IEnumerable<RunAction> Execute(List<GCodeToken> Tokens)
        {
            Reset();

            foreach (GCodeToken token in Tokens)
            {
                if (action.LineNumber != token.LineNumber)
                    action.IsInMachineCoord = false;

                action.IsInMachineCoord = false;
                action.Token = token;
                action.IsRetract = action.IsSpindleSynced = false;

                switch (token.Command)
                {
                    // G0, G1: Linear Move
                    case Commands.G0:
                    case Commands.G1:
                        {
                            var motion = token as GCLinearMotion;
                            setEndP(motion.Values, motion.AxisFlags);
                        }
                        break;

                    // G2, G3: Arc Move
                    case Commands.G2:
                    case Commands.G3:
                        {
                            var arc = token as GCArc;
                            setEndP(arc.Values, arc.AxisFlags);
                        }
                        break;

                    // G5: Cubic Spline
                    case Commands.G5:
                        {
                            var spline = token as GCCubicSpline;
                            setEndP(spline.Values, spline.AxisFlags);
                        }
                        break;

                    // G5: Quadratic Spline
                    case Commands.G5_1:
                        {
                            var spline = token as GCQuadraticSpline;
                            setEndP(spline.Values, spline.AxisFlags);
                        }
                        break;

                    // G7: Lathe Diameter Mode
                    case Commands.G7:
                        LatheMode = LatheMode.Diameter;
                        break;

                    // G8: Lathe Radius Mode
                    case Commands.G8:
                        LatheMode = LatheMode.Radius;
                        break;

                    // G10: Set Coordinate System
                    case Commands.G10:
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
                        break;

                    // G17: XY Plane Select
                    case Commands.G17:
                    // G18: ZX Plane Select
                    case Commands.G18:
                    // G19: YZ Plane Select
                    case Commands.G19:
                        Plane = (GCPlane)token;
                        break;

                    // G21: Metric Units
                    case Commands.G20:
                        // Tokens are metric and needs to be transformed back...
                        IsImperial = true;
                        //for (int i = 0; i < scaleFactors.Length; i++)
                        //    scaleFactors[i] = 25.4d;
                        break;

                    // G21 Imperial (inches) Units
                    case Commands.G21:
                        IsImperial = false;
                        //for (int i = 0; i < scaleFactors.Length; i++)
                        //    scaleFactors[i] = 1d;
                        break;

                    // G28: Goto Predefined Position
                    case Commands.G28:
                        if(translate)
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
                                machinePos[i] = g28.Values[i]; // - offsets[i] - origin[i];
                            action.End = machinePos.Point3D;
                //            action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.All);
                            action.Token = new GCAbsLinearMotion(token.Command, Commands.G0, token.LineNumber, machinePos.Array, axisFlags);

                            yield return action;
                            action.Start = action.End;
                        }
                        break;

                    // G28.1: Set Predefined Position
                    case Commands.G28_1:
                        {
                            for (int i = 0; i < g28.Values.Length; i++)
                                g28.Values[i] = offsets[i];
                        }
                        break;

                    // G30: Goto Predefined Position
                    case Commands.G30:
                        if (translate)
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
                                machinePos[i] = g30.Values[i]; // - offsets[i] - origin[i];
                            action.End = machinePos.Point3D;
//                            action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.All);
                            action.Token = new GCAbsLinearMotion(token.Command, Commands.G0, token.LineNumber, machinePos.Array, axisFlags);
                            yield return action;
                            action.Start = action.End;
                        }
                        break;

                    // G30.1: Set Predefined Position
                    case Commands.G30_1:
                        {
                            for (int i = 0; i < g30.Values.Length; i++)
                                g30.Values[i] = offsets[i];
                        }
                        break;

                    // G33: Spindle Synchronized Motion
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

                    // G38.x: Probing motion
                    case Commands.G38_2:
                    case Commands.G38_3:
                    case Commands.G38_4:
                    case Commands.G38_5:
                        if (translate)
                        {
                            var motion = token as GCLinearMotion;
                            setEndP(motion.Values, motion.AxisFlags);
                            action.Token = new GCLinearMotion(Commands.G1, token.LineNumber, machinePos.Array, motion.AxisFlags);
                        }
                        break;

                    // G43: Tool Length Offset
                    case Commands.G43:
                        SetToolOffset(token as GCToolOffset);
                        break;

                    // G43.1: Dynamic Tool Length Offset
                    case Commands.G43_1:
                        DynamicToolOffset(token as GCToolOffsets);
                        break;

                    // G43.2: Apply additional Tool Length Offset
                    case Commands.G43_2:
                        AddToolOffset(token as GCToolOffset);
                        break;

                    // G49: Cancel Tool Length Compensation
                    case Commands.G49:
                        CancelToolCompensation();
                        break;

                    // G50: Cancel Scaling
                    case Commands.G50:
                        {
                            IsScaled = false;
                            //for (int i = 0; i < scaleFactors.Length; i++)
                            //    scaleFactors[i] = IsImperial ? 25.4d : 1d;
                            for (int i = 0; i < scaleFactors.Length; i++)
                                scaleFactors[i] = 1d;
                        }
                        break;

                    case Commands.G51:
                        {
                            IsScaled = false;
                            //var scale = token as GCScaling;
                            ////foreach (int i in scale.AxisFlags.ToIndices())
                            ////    scaleFactors[i] = scale.Values[i] * (IsImperial ? 25.4d : 1d);
                            //foreach (int i in scale.AxisFlags.ToIndices())
                            //    scaleFactors[i] = scale.Values[i];
                            //for (int i = 0; i < scaleFactors.Length; i++)
                            //    IsScaled |= scaleFactors[i] != 1d;

                            // Strip G20 for now - Tokens are already scaled and needs to be transformed back...
                            action.Token.Command = Commands.Undefined; // Strip G51 - need to implement unscale...
                        }
                        break;

                    // G53: Move in Machine Coordinates
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

                    // G54-G59.3: Select Coordinate System
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

                    // G76: Threading Cycle
                    case Commands.G76:
                        if(translate) {
                            var thread = token as GCThreadingMotion;
                            uint pass = 1, passes = 0;
                            double doc = thread.InitialDepth, thread_length, main_taper_height = 0d;
                            double t_end_taper_length = thread.TaperLength;
                            double end_tapers = thread.ThreadTaper == ThreadTaper.None ? 0d : (thread.ThreadTaper == ThreadTaper.Both ? 2d : 1d);
                            double infeed_factor = Math.Tan(thread.InfeedAngle * Math.PI / 180d), infeed_offset = 0d;
                            double target_z = thread.Z + thread.Depth * infeed_factor, start_z = action.Start.Z + thread.Depth * infeed_factor;

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
                                action.End.Z = start_z - infeed_offset;
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
                                    action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, machinePos.Array, AxisFlags.X);
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
                                    action.End.Z = start_z - infeed_offset;
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

                    // G80: Cancel Canned Cycle
                    case Commands.G80:
                        break;

                    // G73: Drilling Cycle with Chip Breaking
                    case Commands.G73:
                    // G81: Drilling Cycle
                    case Commands.G81:
                    // G82: Drilling Cycle, Dwell
                    case Commands.G82:
                    // G83: Peck Drilling Cycle
                    case Commands.G83:
                    // G85: Boring Cycle, Feed Out
                    case Commands.G85:
                    // G86: Boring Cycle, Spindle Stop, Rapid Move Out
                    case Commands.G86:
                    // G89: Boring Cycle, Dwell, Feed Out
                    case Commands.G89:
                        // TODO: add plane handling
                        if(translate) {
                            bool wasRelative = isRelative;
                            GCCannedDrill drill = (token as GCCannedDrill);
                            if (!double.IsNaN(drill.R))
                                retractPosition = drill.R;
                            double r = isRelative ? action.End.Z + retractPosition : retractPosition;
                            double z = isRelative ? r + drill.Z : drill.Z;
                            uint repeats = DistanceMode == DistanceMode.Incremental ? drill.L : 1; // no need to draw absolute repeats(?)

                            if (action.End.Z < r)
                            {
                                double[] start = new double[] { action.Start.X, action.Start.Y, r };
                                isRelative = false;
                                setEndP(start, AxisFlags.Z);
                                action.Token = new GCLinearMotion(Commands.G0, token.LineNumber, start, AxisFlags.Z);
                                yield return action;
                                action.Start = action.End;
                            }

                            isRelative = wasRelative;
                            setEndP(drill.Values, AxisFlags.XY);
                            isRelative = false;

                            double[] values = new double[] { action.End.X, action.End.Y, action.End.Z };

                            setEndP(values, AxisFlags.XY);
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

                    // G90.1, G91.1: Arc Distance Mode
                    case Commands.G90: // Absolute
                    case Commands.G91: // Incremental
                        isRelative = (token as GCDistanceMode).DistanceMode == DistanceMode.Incremental;
                        break;

                    // G92: Coordinate System Offset
                    case Commands.G92:
                        {
                            var cs = token as GCCoordinateSystem;
                            foreach (int i in cs.AxisFlags.ToIndices())
                                origin[i] = g92.Values[i] = cs.Values[i];
                        }
                        break;

                    // G92.1: Reset G92 Offsets - Clear Parameters
                    case Commands.G92_1:
                        {
                            for (int i = 0; i < origin.Length; i++)
                                origin[i] = g92.Values[i] = 0d;
                        }
                        break;

                    // G92.1: Reset G92 Offsets - Keep Parameters
                    case Commands.G92_2:
                        {
                            for (int i = 0; i < origin.Length; i++)
                                origin[i] = 0d;
                        }
                        break;

                    //M3, M4, M5: Spindle Control
                    case Commands.M3: // CW
                    case Commands.M4: // CCW
                    case Commands.M5: // Off
                        SpindleState = (token as GCSpindleState).SpindleState;
                        break;

                    // M7, M8, M9: Coolant Control
                    case Commands.M7: // Mist
                    case Commands.M8: // Flood
                    case Commands.M9: // Off
                        CoolantState = (token as GCCoolantState).CoolantState;
                        break;

                    case Commands.Feedrate:
                        Feedrate = (token as GCFeedrate).Feedrate;
                        break;

                    case Commands.SpindleRPM:
                        _rpm = (token as GCSpindleRPM).SpindleRPM;
                        break;

                    // M61, T: Set Current Tool
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

        private new Point3D setEndP(double[] values, AxisFlags axisFlags)
        {
            base.setEndP(values, axisFlags);

            action.End = machinePos.Point3D;

            return action.End;
        }
    }
}
