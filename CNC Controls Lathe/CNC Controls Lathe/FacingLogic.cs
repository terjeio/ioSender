/*
 * FacingLogic.cs - part of CNC Controls Lathe library
 *
 * v0.43 / 2023-06-06 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2023, Io Engineering (Terje Io)
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
    class FacingLogic
    {
        private double last_rpm = 0d, last_css = 0d;
        private BaseViewModel model;

        public FacingLogic()
        {
            model = new BaseViewModel("Facing");
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

                model.ZClearance = model.config.ZClearance / model.UnitFactor;
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

            if (model.ZStart < model.ZTarget)
            {
                model.SetError(nameof(model.ZStart), "Start must be greater than target.");
                model.SetError(nameof(model.ZTarget), "Start must be greater than target.");
                return;
            }

            double passdepth = model.Passdepth;
            double passdepth_last = model.PassdepthLastPass;
            double zstart = model.ZStart;
            double ztarget = model.ZTarget;
            double xtarget = model.XTarget;
            double xclearance = xtarget + model.XClearance;
            double diameter = model.XStart;

            if (Math.Abs(diameter - xtarget) == 0.0d) // nothing to do...
                return;

            if (model.config.xmode == LatheMode.Radius)
            {
                xtarget /= 2.0d;
                xclearance /= 2.0d;
                diameter /= 2.0d;
            }

            double xstart = diameter;
            double xclear = xstart + xclearance;
            double zclearance = model.ZClearance;

            PassCalc cut = new PassCalc(zstart - ztarget, passdepth, passdepth_last, model.Precision);

            if (cut.Passes < 1)
            {
                model.SetError(nameof(model.XStart), "Starting diameter must be larger than target.");
                return;
            }

            uint pass = 1;
            string cssCmd = model.IsCssEnabled ? string.Format(model.config.CSSMaxRPM > 0.0d ? "G96S{0}D{1}" : "G96S{0}",
                                                                model.CssSpeed, model.config.CSSMaxRPM) : "";

            model.gCode.Clear();
            model.gCode.Add(string.Format("G18 G{0} G{1}", model.config.xmode == LatheMode.Radius ? "8" : "7", model.IsMetric ? "21" : "20"));
            if (!model.IsCssEnabled)
                model.gCode.Add("G97");
            model.gCode.Add(string.Format("M3S{0} G4P1", speed.ToString()));
            model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xclear)));
            model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(zstart + zclearance)));

            do
            {
                ztarget = cut.GetPassTarget(pass, zstart, true);
                double feedrate = cut.IsLastPass ? model.FeedRateLastPass : model.FeedRate;

                model.gCode.Add(string.Format("(Pass: {0}, DOC: {1} {2})", pass, ztarget, cut.DOC));

                if (model.IsCssEnabled)
                    model.gCode.Add(cssCmd);
                model.gCode.Add(string.Format("G1 Z{0} F{1}", model.FormatValue(ztarget), model.FormatValue(feedrate)));
                model.gCode.Add(string.Format("G1 X{0}", model.FormatValue(xtarget)));
                if (!cut.IsLastPass || !(model.IsSpringPassesEnabled && model.SpringPasses > 0))
                {
                    model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(ztarget + zclearance)));
                    if (model.IsCssEnabled)
                        model.gCode.Add(string.Format("G97S{0}", speed.ToString()));
                    model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xclear)));
                }

            } while (++pass <= cut.Passes);

            if(model.IsSpringPassesEnabled && model.SpringPasses > 0)
            {
                model.gCode.Add(string.Format("(Pass: {0}, springpass)", pass));
                model.gCode.Add(string.Format("G1 X{0}", model.FormatValue(xclear)));
                while (model.SpringPasses > 1)
                {
                    model.SpringPasses--;
                    model.gCode.Add(string.Format("(Pass: {0}, springpass)", ++pass));
                    model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(ztarget + zclearance)));
                    model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xtarget)));
                    model.gCode.Add(string.Format("G1 Z{0}", model.FormatValue(ztarget)));
                    model.gCode.Add(string.Format("G1 X{0}", model.FormatValue(xclear)));
                }
            }

            GCode.File.AddBlock("Wizard: Facing", Core.Action.New);
            GCode.File.AddBlock(string.Format("({0}, Start: {1}, Target: {2}, Length{3})",
                                    "Facing",
                                    model.FormatValue(zstart), model.FormatValue(ztarget), model.FormatValue(0d)), Core.Action.Add);
            GCode.File.AddBlock(string.Format("(Passdepth: {0}, Feedrate: {1}, {2}: {3})",
                                    model.FormatValue(passdepth), model.FormatValue(model.FeedRate),
                                         (model.IsCssEnabled ? "CSS" : "RPM"), model.FormatValue((double)model.CssSpeed)), Core.Action.Add);

            foreach (string s in model.gCode)
                GCode.File.AddBlock(s, Core.Action.Add);

            GCode.File.AddBlock("M30", Core.Action.End);
        }
    }
}
