/*
 * DragKnifeViewModel.cs - part of CNC Controls DragKnife library for Grbl
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

using RP.Math;
using System;
using System.Collections.Generic;
using System.Windows;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.DragKnife
{
    public class DragKnifeViewModel : ViewModelBase, IGCodeTransformer
    {
        private double _knifeTipOffset = 1.5d, _cutDepth = -1.00d,  _swivelAngle = 20d,  _dentLength = .02d, _retractAngle = 40d, _retractDepth = -0.05d;
        private bool _retractEnable = false;

        public double KnifeTipOffset {  get { return _knifeTipOffset; }  set { _knifeTipOffset = value; OnPropertyChanged(); } }
        public double CutDepth { get { return _cutDepth; } set { _cutDepth = value; OnPropertyChanged(); } }
        public double SwivelAngle { get { return _swivelAngle; } set { _swivelAngle = value; OnPropertyChanged(); } }
        public double DentLength { get { return _dentLength; } set { _dentLength = value; OnPropertyChanged(); } }
        public double RetractAngle { get { return _retractAngle; } set { _retractAngle = value; OnPropertyChanged(); } }
        public double RetractDepth { get { return _retractDepth; } set { _retractDepth = value; OnPropertyChanged(); } }
        public bool RetractEnable { get { return _retractEnable; } set { _retractEnable = value; OnPropertyChanged(); } }

        private Vector3 StartDirection;

        private class segment
        {
            public Vector3 P1;
            public Vector3 P2;
            public bool First;
            public bool Last;
        }

        public void Apply()
        {
            if (new DragKnifeDialog(this) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return;

            using (new UIUtils.WaitCursor())
            {
                GCodeEmulator emu = new GCodeEmulator();

                //          emu.SetStartPosition(Machine.StartPosition);
                List<segment> polyLine = new List<segment>();
                List<GCodeToken> newToolPath = new List<GCodeToken>();

                newToolPath.Add(new GCComment(Commands.Comment, 0, "Drag knife transform applied"));

                StartDirection = new Vector3(1d, 0d, 0d);

                foreach (var cmd in emu.Execute(GCode.File.Tokens))
                {
                    switch (cmd.Token.Command)
                    {
                        case Commands.G0:
                            if (polyLine.Count > 0)
                            {
                                polyLine[polyLine.Count - 1].Last = true;
                                Transform(polyLine, newToolPath);
                                polyLine.Clear();
                            }
                            newToolPath.Add(cmd.Token);
                            break;

                        case Commands.G1:
                            segment s = new segment();
                            s.P1 = new Vector3(cmd.Start.X, cmd.Start.Y, 0d);
                            s.P2 = new Vector3(cmd.End.X, cmd.End.Y, 0d);
                            if (!s.P1.Equals(s.P2))
                                polyLine.Add(s);
                            break;

                        default:
                            newToolPath.Add(cmd.Token);
                            break;
                    }
                }

                List<string> gc = GCodeParser.TokensToGCode(newToolPath, AppConfig.Settings.Base.AutoCompress);
                //            GCodeParser.Save(@"C:\Users\terjeio\Desktop\Probing\knife.nc", gc);

                GCode.File.AddBlock(string.Format("Drag knife transform applied: {0}", GCode.File.Model.FileName), CNC.Core.Action.New);

                foreach (string block in gc)
                    GCode.File.AddBlock(block, CNC.Core.Action.Add);

                GCode.File.AddBlock("", CNC.Core.Action.End);
            }
        }

        double[] ToPos (Vector3 pos)
        {
            double[] gcpos = pos.Array;

            gcpos[0] = Math.Round(gcpos[0], 3);
            gcpos[1] = Math.Round(gcpos[1], 3);
            gcpos[2] = Math.Round(gcpos[2], 3);

            return gcpos;
        }

        private void Transform (List<segment> polyLine, List<GCodeToken> newToolPath)
        {
            uint lnr = 0;
            double[] ijk = new Vector3(-_knifeTipOffset, 0d, 0d).Array;

            Vector3 end = polyLine[0].P1;

            segment prev = new segment();
            prev.P1 = new Vector3(polyLine[0].P1);
            prev.P2 = new Vector3(polyLine[0].P1 + StartDirection * _knifeTipOffset);

            for (int i = 0; i < polyLine.Count; i++)
            {
                var cp1 = prev.P2 - prev.P1;
                var cp2 = polyLine[i].P2 - polyLine[i].P1;
                var n1 = cp1.NormalizeOrDefault();
                var n2 = cp2.NormalizeOrDefault();
                var angle = cp2.Angle(cp1) * (180d / Math.PI);

                if (i == 0)
                {
                    end = prev.P1 + n1 * _knifeTipOffset;
                    newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(end), AxisFlags.XY));
                    newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(end + new Vector3(0d, 0d, _cutDepth)), AxisFlags.Z));
                }

                if (Math.Abs(angle) > (i == 0 ? 1d : _swivelAngle) && cp2.Magnitude >= _dentLength)
                {
                    //           end = polyLine[i].P1 + offset2;
                    var end1 = polyLine[i].P1 + n2 * _knifeTipOffset;
                    var dir = (i == 0 ? prev.P1 : prev.P2) - end;
                    StartDirection = dir;
                    end = end1;
                    var arcdir = n1.X * n2.Y - n1.Y * n2.X;
                    newToolPath.Add(new GCArc(arcdir < 0d ? Commands.G2 : Commands.G3, lnr++, ToPos(end), AxisFlags.XY, ToPos(dir), IJKFlags.I | IJKFlags.J, 0d, 0, IJKMode.Incremental));
                }
                if (cp2.Magnitude > _knifeTipOffset)
                    end = polyLine[i].P2 + n2 * _knifeTipOffset;
                else
                    end += cp2;

                newToolPath.Add(new GCLinearMotion((i & 1) == 1 ? Commands.G1 : Commands.G1, lnr++, ToPos(end), AxisFlags.XY));
                prev = polyLine[i];

                if (i == polyLine.Count - 1)
                {
                    newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(end + new Vector3(0d, 0d, -_cutDepth)), AxisFlags.Z));
                }
            }
        }

        private void Transformx(List<segment> polyLine, List<GCodeToken> newToolPath)
        {
            uint lnr = 0;
            double[] ijk = new Vector3(-_knifeTipOffset, 0d, 0d).Array;
            var offset2 = new Vector3();
            for (int i = 0; i < polyLine.Count; i++)
            {
                if (i < polyLine.Count - 1)
                {

                    var cp1 = polyLine[i].P2 - polyLine[i].P1;
                    var cp2 = polyLine[i + 1].P2 - polyLine[i + 1].P1;
                    var n1 = cp1.NormalizeOrDefault();
                    var n2 = cp2.NormalizeOrDefault();
                    var sp = n1.DotProduct(n2);
                    var ang1 = cp2.Angle(cp1);
                    var ang = n2.Angle(n1);
                    var ang2 = polyLine[i + 1].P1.Angle(polyLine[i + 1].P2);
                    //                    var angle = Math.Acos(polyLine[i].P1.Normalize().DotProduct(polyLine[i].P2.Normalize()));
                    var ad = (ang1) * (180d / Math.PI);

                    var offset = n1 * _knifeTipOffset;
                    offset2 = n2 * _knifeTipOffset;
                    var start = polyLine[i].P1 + offset;
                    var end = polyLine[i].P2 + offset;
                    var dir = polyLine[i].P2 - end;
                    ////   ijk[0] = _knifeTipOffset * dir.;

                    if (i == 0)
                    {
                        //                        start = StartDirection * _knifeTipOffset;
                        newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(start), AxisFlags.XY));
                        newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(start + new Vector3(0d, 0d, _cutDepth)), AxisFlags.Z));
                    }
               //     else

                        newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(end), AxisFlags.XY));

                    if (Math.Abs(ad) > _swivelAngle && cp2.Magnitude >= _dentLength)
                    {
                        var arcdir = n1.X * n2.Y - n1.Y * n2.X;
                        //                        newToolPath.Add(new GCArc(arcdir < 0d ? Commands.G2 : Commands.G3, lnr++, ToPos(polyLine[i + 1].P1 + offset2), AxisFlags.XY, ToPos(dir), IJKFlags.I | IJKFlags.J, 0d, IJKMode.Incremental));
                        //newToolPath.Add(new GCArc(arcdir < 0d ? Commands.G2 : Commands.G3, lnr++, ToPos(polyLine[i].P1 + offset2), AxisFlags.XY, ToPos(dir), IJKFlags.I | IJKFlags.J, 0d, IJKMode.Incremental));
                        newToolPath.Add(new GCArc(arcdir < 0d ? Commands.G2 : Commands.G3, lnr++, ToPos(polyLine[i + 1].P1 + offset2), AxisFlags.XY, ToPos(dir), IJKFlags.I | IJKFlags.J, 0d, 0, IJKMode.Incremental));
                    }

                }
                if (i == polyLine.Count - 1)
                {
                    newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(polyLine[i].P2 + offset2), AxisFlags.XY));
                    newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(polyLine[i].P2 + offset2 + new Vector3(0d, 0d, -_cutDepth)), AxisFlags.Z));
             //       StartDirection = dir;
                }
                //                newToolPath.Add(new GCLinearMotion(Commands.G1, (uint)i, new double[] { polyLine[i].P2.X, polyLine[i].P2.Y, polyLine[i].P2.Z }, AxisFlags.XY));
            }

        }

    }
}
