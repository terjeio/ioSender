/*
 * GCodeRotate.cs - part of CNC Controls library for Grbl
 *
 * v0.40 / 2022-07-12 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021-2022, Io Engineering (Terje Io)
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
using CNC.Core;
using CNC.GCode;
using RP.Math;

namespace CNC.Controls
{
    public class GCodeRotate
    {
        private Vector3 ToAbsolute(Vector3 orig, double[] values, bool isRelative = false)
        {
            Vector3 p;

            if (isRelative)
                p = orig + new Vector3(values[0], values[1], values[2]);
            else
                p = new Vector3(values[0], values[1], values[2]);

            return p;
        }

        public void ApplyRotation(double angle, Vector3 offset, bool compress = false)
        {
            uint G53lnr = 0;
            int precision = GCode.File.Decimals;
            GCPlane plane = new GCPlane(GrblParserState.Plane == Plane.XY ? Commands.G17 : Commands.G18, 0);
            DistanceMode distanceMode = GrblParserState.DistanceMode;
            Vector3 pos = new Vector3(Grbl.GrblViewModel.Position.X, Grbl.GrblViewModel.Position.Y, Grbl.GrblViewModel.Position.Z);
            List<GCodeToken> toolPath = new List<GCodeToken>();

            toolPath.Add(new GCComment(Commands.Comment, 0, string.Format("{0} degree rotation applied", Math.Round(angle * 180d / Math.PI, 1).ToInvariantString())));

            foreach (var token in GCode.File.Tokens)
            {
                switch (token.Command)
                {
                    case Commands.G0:
                    case Commands.G1:
                        {
                            var motion = token as GCLinearMotion;

                            if (motion.AxisFlags != AxisFlags.None && G53lnr != token.LineNumber)
                            {
                                var target = ToAbsolute(pos, motion.Values);

                                if (distanceMode == DistanceMode.Incremental)
                                {
                                    if (!motion.AxisFlags.HasFlag(AxisFlags.X))
                                        target = new Vector3(0d, target.Y, 0d);

                                    if (!motion.AxisFlags.HasFlag(AxisFlags.Y))
                                        target = new Vector3(target.X, 0d, 0d);

                                    target = target.RotateZ(0d, 0d, angle).Round(precision);
                                }
                                else
                                    target = target.RotateZ(offset.X, offset.Y, angle).Round(precision);

                                if (target.X != pos.X)
                                    motion.AxisFlags |= AxisFlags.X;

                                if (target.Y != pos.Y)
                                    motion.AxisFlags |= AxisFlags.Y;

                                if (distanceMode == DistanceMode.Incremental)
                                    pos += target;
                                else
                                    pos = target;

                                toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, target.Array, motion.AxisFlags));

                            }
                            else
                            {
                                G53lnr = 0;
                                toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, motion.Values, motion.AxisFlags));
                            }
                        }
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        {
                            if (plane.Plane != Plane.XY)
                                throw new Exception(LibStrings.FindResource("HasG17G18Arcs"));

                            if ((token as GCArc).IsRadiusMode) // for now...
                                throw new Exception(LibStrings.FindResource("HasRadiusArcs"));

                            var arc = token as GCArc;

                            Vector3 target = ToAbsolute(pos, arc.Values).RotateZ(offset.X, offset.Y, angle).Round(precision);
                            Vector3 targetijk = arc.IsRadiusMode ? new Vector3(double.NaN, double.NaN, double.NaN) : new Vector3(arc.IJKvalues).RotateZ(0d, 0d, angle).Round(precision);

                            if (pos.X != target.X)
                                arc.AxisFlags |= AxisFlags.X;

                            if (pos.Y != target.Y)
                                arc.AxisFlags |= AxisFlags.Y;

                            pos = target;

                            toolPath.Add(new GCArc(arc.Command, arc.LineNumber, pos.Array, arc.AxisFlags, targetijk.Array, arc.IjkFlags, arc.R, arc.P, arc.IJKMode));
                        }
                        break;

                    case Commands.G5:
                        {
                            var spline = token as GCCubicSpline;
                            pos = new Vector3(spline.X, spline.Y, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                            var ij = new Vector3(spline.I, spline.J, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                            var pq = new Vector3(spline.P, spline.Q, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);

                            toolPath.Add(new GCCubicSpline(spline.Command, spline.LineNumber, pos.Array, spline.AxisFlags, new double[] { ij.X, ij.Y, pq.X, pq.Y }));
                        }
                        break;

                    case Commands.G5_1:
                        {
                            var spline = token as GCQuadraticSpline;
                            pos = new Vector3(spline.X, spline.Y, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                            var ij = new Vector3(spline.I, spline.J, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);

                            toolPath.Add(new GCQuadraticSpline(spline.Command, spline.LineNumber, pos.Array, spline.AxisFlags, new double[] { ij.X, ij.Y }));
                        }
                        break;

                    case Commands.G17:
                    case Commands.G18:
                    case Commands.G19:
                        plane = token as GCPlane;
                        toolPath.Add(token);
                        break;

                    case Commands.G53:
                        G53lnr = token.LineNumber; // No rotation for next linear move
                        toolPath.Add(token);
                        break;

                    case Commands.G73:
                    case Commands.G81:
                    case Commands.G82:
                    case Commands.G83:
                    case Commands.G85:
                    case Commands.G86:
                    case Commands.G89:
                        {
                            var drill = token as GCCannedDrill;
                            var target = ToAbsolute(pos, drill.Values).RotateZ(offset.X, offset.Y, angle);

                            if (pos.X != target.X)
                                drill.AxisFlags |= AxisFlags.X;

                            if (pos.Y != target.Y)
                                drill.AxisFlags |= AxisFlags.Y;

                            if (distanceMode == DistanceMode.Incremental)
                                pos = new Vector3(pos.X + target.X * drill.L, pos.Y + target.Y * drill.L, pos.Z + target.Z);
                            else
                                pos = target;

                            toolPath.Add(new GCCannedDrill(drill.Command, drill.LineNumber, target.Round(precision).Array, drill.AxisFlags, drill.R, drill.L, drill.P, drill.Q));
                        }
                        break;

                    case Commands.G90:
                    case Commands.G91:
                        distanceMode = (token as GCDistanceMode).DistanceMode;
                        toolPath.Add(token);
                        break;

                    default:
                        toolPath.Add(token);
                        break;
                }
            }

            List<string> gc = GCodeParser.TokensToGCode(toolPath, compress);

            //            GCodeParser.Save(@"C:\Users\terjeio\Desktop\Probing\file.nc", gc);

            GCode.File.AddBlock(string.Format("{0} degree rotation applied: {1}", Math.Round(angle * 180d / Math.PI, 1).ToInvariantString(), Grbl.GrblViewModel.FileName), Core.Action.New);

            foreach (string block in gc)
                GCode.File.AddBlock(block, Core.Action.Add);

            GCode.File.AddBlock("", Core.Action.End);
        }
    }
}
