/*
 * GCodeWrap.cs - part of CNC Controls library for Grbl
 *
 * v0.37 / 2022-02-19 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2022, Io Engineering (Terje Io)
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
using System.Text;
using System.Threading.Tasks;
using CNC.Core;
using CNC.GCode;
using RP.Math;

namespace CNC.Controls
{
    class GCodeWrap
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

        public void ApplyWrap(GCodeWrapViewModel model, bool compress = false)
        {
            uint G53lnr = 0;
            int precision = GCode.File.Decimals;
            double feedrate_pgm = 0d, feedrate = 0d, feedrate_cur = 0d;
            string hdr = string.Format("Wrap applied, diameter: {0}, {1}-axis to {2}-axis", model.Diameter.ToInvariantString(), GrblInfo.AxisIndexToLetter(model.SourceAxis), GrblInfo.AxisIndexToLetter(model.TargetAxis));
            GCPlane plane = new GCPlane(GrblParserState.Plane == Plane.XY ? Commands.G17 : Commands.G18, 0);
            DistanceMode distanceMode = GrblParserState.DistanceMode;
            FeedRateMode feedRateMode = GrblParserState.FeedRateMode;
            AxisFlags targetAxis = GrblInfo.AxisIndexToFlag(model.TargetAxis);
            List<GCodeToken> toolPath = new List<GCodeToken>();
            Point6D pos = new Point6D(), newpos = new Point6D();

            pos.Set(Grbl.GrblViewModel.Position.Values.Array, GrblInfo.AxisFlags);
            if (model.SourceAxis == GrblConstants.X_AXIS)
                pos.X = 0d;
            else
                pos.Y = 0d;

            toolPath.Add(new GCComment(Commands.Comment, 0, hdr));

            double mmDeg = model.Diameter * Math.PI / 360d;

            foreach (var token in GCode.File.Tokens)
            {
                switch (token.Command)
                {
                    case Commands.G0:
                    case Commands.G1:
                        {
                            var motion = token as GCLinearMotion;
                            newpos.Set(motion.Values, motion.AxisFlags);

                            if (motion.AxisFlags.HasFlag(AxisFlags.Y) && G53lnr != token.LineNumber)
                            {
                                switch (model.TargetAxis)
                                {
                                    case GrblConstants.A_AXIS:
                                        newpos.A = Math.Round((model.SourceAxis == GrblConstants.X_AXIS ? motion.X : motion.Y) / mmDeg, precision);
                                        motion.AxisFlags |= AxisFlags.A;
                                        if (model.SourceAxis == GrblConstants.X_AXIS)
                                        {
                                            newpos.X = 0d;
                                            motion.AxisFlags &= ~AxisFlags.X;
                                        }
                                        else
                                        {
                                            newpos.Y = 0d;
                                            motion.AxisFlags &= ~AxisFlags.Y;
                                        }
                                        break;

                                    case GrblConstants.B_AXIS:
                                        newpos.B = Math.Round((model.SourceAxis == GrblConstants.X_AXIS ? motion.X : motion.Y) / mmDeg, precision);
                                        motion.AxisFlags |= AxisFlags.B;
                                        if (model.SourceAxis == GrblConstants.X_AXIS)
                                        {
                                            newpos.X = 0d;
                                            motion.AxisFlags &= ~AxisFlags.X;
                                        }
                                        else
                                        {
                                            newpos.Y = 0d;
                                            motion.AxisFlags &= ~AxisFlags.Y;
                                        }
                                        break;
                                }

                                if (motion.AxisFlags.HasFlag(AxisFlags.Z) && model.Z0atCenter)
                                    motion.Z += model.Diameter / 2d;

                                if (token.Command == Commands.G1)
                                {
                                    double dist = 0d;

                                    for (int i = 0; i < 3; i++)
                                        dist += Math.Pow(distanceMode == DistanceMode.Incremental ? newpos[i] : newpos[i] - pos[i], 2d);

                                    dist = Math.Sqrt(dist);

                                    //if (dist > 0d)
                                    //    toolPath.Add(new GCComment(Commands.Comment, motion.LineNumber, string.Format("dist {0} {1}", dist, feedrate_pgm / dist)));

                                    if (motion.AxisFlags == targetAxis || dist == 0d)
                                    {
                                        motion.AxisFlags = targetAxis;
                                        if (feedRateMode != FeedRateMode.UnitsPerMin)
                                        {
                                            feedrate_cur = 0d;
                                            toolPath.Add(new GCFeedRateMode(motion.LineNumber, feedRateMode = FeedRateMode.UnitsPerMin));
                                        }
                                        if (feedrate_cur != feedrate)
                                            toolPath.Add(new GCFeedrate(motion.LineNumber, feedrate_cur = feedrate));
                                    }
                                    else
                                    {
                                        if (feedRateMode != FeedRateMode.InverseTime)
                                            toolPath.Add(new GCFeedRateMode(motion.LineNumber, feedRateMode = FeedRateMode.InverseTime));
                                        toolPath.Add(new GCFeedrate(motion.LineNumber, Math.Round(feedrate_pgm / dist, 1)));
                                    }

                                    toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, newpos.Array, motion.AxisFlags));
                                    pos.Set(newpos.Array, motion.AxisFlags, distanceMode == DistanceMode.Incremental);
                                }
                                else
                                {
                                    if (motion.AxisFlags.HasFlag(AxisFlags.Z) && model.Z0atCenter)
                                        motion.Z += model.Diameter / 2d;

                                    if (feedRateMode != FeedRateMode.UnitsPerMin)
                                    {
                                        feedrate_cur = 0d;
                                        toolPath.Add(new GCFeedRateMode(motion.LineNumber, feedRateMode = FeedRateMode.UnitsPerMin));
                                    }

                                    if (feedrate_cur != feedrate_pgm)
                                        toolPath.Add(new GCFeedrate(motion.LineNumber, feedrate_cur = feedrate_pgm));

                                    G53lnr = 0;
                                    toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, newpos.Array, motion.AxisFlags));
                                    pos.Set(newpos.Array, motion.AxisFlags, distanceMode == DistanceMode.Incremental);
                                }
                            }
                            else // G0
                            {
                                G53lnr = 0;
                                toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, newpos.Array, motion.AxisFlags));
                                pos.Set(newpos.Array, motion.AxisFlags, distanceMode == DistanceMode.Incremental);
                            }
                        }
                        break;

                    case Commands.G2:
                    case Commands.G3:
                        throw new Exception(LibStrings.FindResource("HasG17G18Arcs"));
                        break;

                    case Commands.G5:
                        throw new Exception(LibStrings.FindResource("HasG17G18Arcs"));
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
                        throw new Exception(LibStrings.FindResource("HasG17G18Arcs"));
                        break;

                    case Commands.G90:
                    case Commands.G91:
                        distanceMode = (token as GCDistanceMode).DistanceMode;
                        toolPath.Add(token);
                        break;

                    case Commands.G93:
                    case Commands.G94:
                    case Commands.G95:
                        feedRateMode = (token as GCFeedRateMode).FeedRateMode;
                        toolPath.Add(token);
                        break;

                    case Commands.Feedrate:
                        feedrate_pgm = (token as GCFeedrate).Feedrate;
                        feedrate = Math.Round(feedrate_pgm / mmDeg, 1);
                        toolPath.Add(token);
                        break;

                    default:
                        toolPath.Add(token);
                        break;
                }
            }

            List<string> gc = GCodeParser.TokensToGCode(toolPath, compress);

//                        GCodeParser.Save(@"C:\Users\terjeio\Desktop\Wrap\file.nc", gc);

            GCode.File.AddBlock(hdr + " - " + Grbl.GrblViewModel.FileName, Core.Action.New);

            foreach (string block in gc)
                GCode.File.AddBlock(block, Core.Action.Add);

            GCode.File.AddBlock("", Core.Action.End);
        }
    }
}
