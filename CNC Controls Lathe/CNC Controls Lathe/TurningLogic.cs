﻿/*
 * TurningLogic.cs - part of CNC Controls Lathe library
 *
 * v0.46 / 2025-05-13 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2025, Io Engineering (Terje Io)
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
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    class TurningLogic
    {
        private double last_rpm = 0d, last_css = 0d;
        private BaseViewModel model;

        public TurningLogic()
        {
            model = new BaseViewModel("Turning");
            model.PropertyChanged += Model_PropertyChanged;
            SetDefaults();
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(model.Profile):
                    SetDefaults();
                    break;

                case nameof(model.IsCssEnabled):

                    //if (css.IsChecked == true)
                    //    last_rpm = css.Value;
                    //else
                    //    last_css = css.Value;

                    //css.Value = css.IsChecked == true ? (int)last_css : (int)last_rpm;
                    break;
            }
        }

        public BaseViewModel Model { get { return model; } }

        private void SetDefaults()
        {
            if (model.Profile != null && model.config.IsLoaded)
            {
                last_css = model.config.CSS && model.config.RPM != 0.0d ? model.config.RPM / (model.IsMetric ? 1d : model.UnitFactor * 0.12d) : 0.0d;
                last_rpm = model.config.CSS || model.config.RPM == 0.0d ? 0.0d : model.config.RPM;

                model.SpindleDir = model.config.SpindleDir;
                model.XClearance = model.config.XClearance / model.UnitFactor;
                model.Passdepth = model.config.PassDepthFirst / model.UnitFactor;
                model.PassdepthLastPass = model.config.PassDepthLast / model.UnitFactor;
                model.FeedRate = model.config.Feedrate / model.UnitFactor;
                model.FeedRateLastPass = model.config.FeedrateLast / model.UnitFactor;

                model.IsCssEnabled = model.config.CSS;
                model.CssSpeed = (uint)(model.IsCssEnabled ? last_css : last_rpm);
            }
        }

        public void Calculate()
        {
            double speed = model.CssSpeed;

            if (!model.Validate(ref speed))
                return;

            double passdepth = model.Passdepth;
            double passdepth_last = model.PassdepthLastPass;
            double zstart = model.ZStart;
            double zlength = model.ZLength;
            double ztarget = (zstart + zlength * model.config.ZDirection);
            double xclearance = model.XClearance;
            double xtarget = model.XTarget;
            double diameter = model.XStart;

            if (Math.Abs(diameter - xtarget) == 0.0d) // nothing to do...
                return;

            if (model.config.xmode == LatheMode.Radius)
            {
                xtarget /= 2.0d;
                diameter /= 2.0d;
            }
            else
            {
                passdepth *= 2.0d;
                passdepth_last *= 2.0d;
                xclearance *= 2.0d;
            }

            double angle = 0.0d;
            double xdistance = xtarget - diameter;
            bool boring = xdistance > 0.0d;
            double xstart = xtarget;

            PassCalc cut = new PassCalc(xdistance, passdepth, passdepth_last, model.Precision);

            if (model.IsTaperEnabled)
            {
                if (model.Taper >= 90d)
                {
                    model.SetError(nameof(model.Taper), "Error: taper cannot be >= 90 deg.");
                    return;
                }
                angle = Math.Tan(Math.PI * model.Taper / 180.0d);
                model.ZLength = (xtarget - diameter) / angle * model.config.ZDirection;
            }
            //  error.Clear();

            if (cut.Passes < 1)
            {
                model.SetError("Diameter", "Starting diameter must be larger than target.");
                return;
            }

            if (model.IsSpringPassesEnabled)
                cut.Springpasses = (int)model.SpringPasses;

            if (boring)
                xclearance = -xclearance;

            uint pass = 1;

            model.gCode.Clear();
            model.gCode.Add(string.Format("G18 G{0} G{1}", model.config.xmode == LatheMode.Radius ? "8" : "7", model.IsMetric ? "21" : "20"));
            model.gCode.Add(string.Format("M{0}S{1} G4P1", model.SpindleDir == SpindleState.CW ? "3" : "4", speed.ToString()));
            model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(diameter + xclearance)));
            model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(zstart + model.config.ZClearance / model.UnitFactor)));
            model.gCode.Add(model.IsCssEnabled ? string.Format(model.config.CSSMaxRPM > 0.0d ? "G96S{0}D{1}" : "G96S{0}",
                                                                model.CssSpeed, model.config.CSSMaxRPM) : "G97");

            do
            {
                xtarget = cut.GetPassTarget(pass, diameter, !boring);
                double feedrate = cut.IsLastPass ? model.FeedRateLastPass : model.FeedRate;

                model.gCode.Add(string.Format("(Pass: {0}, DOC: {1} {2})", pass, model.FormatValue(xtarget), model.FormatValue(cut.DOC)));

                // diameter = Math.Max(diameter - passdepth, xtarget);
                // TODO: G0 to prev target to keep spindle speed constant?
                //     if (css)
                //         code[i++] = string.Format("G0 X{0}", model.FormatValue(doc_prev));
                if (angle != 0.0d)
                {
                    ztarget = cut.Distance / angle * model.config.ZDirection;
                    model.gCode.Add(string.Format("G1 X{0} F{1}", model.FormatValue(xtarget -= model.config.ZClearance / model.UnitFactor * angle), model.FormatValue(feedrate)));
                    model.gCode.Add(string.Format("G1 X{0} Z{1}", model.FormatValue(diameter), model.FormatValue(zstart + ztarget)));
                    model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(diameter + xclearance)));
                }
                else
                {
                    model.gCode.Add(string.Format("G1 X{0} F{1}", model.FormatValue(xtarget), model.FormatValue(feedrate)));
                    model.gCode.Add(string.Format("G1 Z{0} F{1}", model.FormatValue(ztarget), model.FormatValue(feedrate)));
                    model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xtarget + xclearance)));
                }
                model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(zstart + model.config.ZClearance / model.UnitFactor)));

            } while (++pass <= cut.Passes);

            GCode.File.AddBlock("Wizard: Turning", Core.Action.New);
            GCode.File.AddBlock(string.Format("({0}, Start: {1}, Target: {2}, Length: {3})",
                                        boring ? "Boring" : "Turning",
                                         model.FormatValue(diameter), model.FormatValue(xtarget), model.FormatValue(zlength)), Core.Action.Add);
            GCode.File.AddBlock(string.Format("(Passdepth: {0}, Feedrate: {1}, {2}: {3})",
                                        model.FormatValue(passdepth), model.FormatValue(model.FeedRate),
                                         (model.IsCssEnabled ? "CSS" : "RPM"), model.FormatValue((double)model.CssSpeed)), Core.Action.Add);
            if (angle != 0.0d)
                GCode.File.AddBlock(string.Format("(Taper length: {0})", model.FormatValue(model.ZLength)));

            foreach (string s in model.gCode)
                GCode.File.AddBlock(s, Core.Action.Add);

            GCode.File.AddBlock("M30", Core.Action.End);
        }
    }
}
