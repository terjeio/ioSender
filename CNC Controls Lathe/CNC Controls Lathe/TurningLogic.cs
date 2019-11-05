/*
 * TurningLogic.cs - part of CNC Controls library
 *
 * v0.02 / 2019-10-31 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019, Io Engineering (Terje Io)
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
using System.Globalization;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    class TurningLogic
    {
        private double last_rpm = 0d, last_css = 0d;
        private BaseViewModel model;

        public event GCodePushHandler GCodePush;

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

                case nameof(model.IsCSSEnabled):

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
                last_css = model.config.CSS && model.config.RPM != 0.0d ? model.config.RPM : 0.0d;
                last_rpm = model.config.CSS || model.config.RPM == 0.0d ? 0.0d : model.config.RPM;

                model.XClearance = model.config.XClearance;
                model.Passdepth = model.config.PassDepthFirst;
                model.PassdepthLastPass = model.config.PassDepthLast;
                model.FeedRate = model.config.Feedrate;
                model.FeedRateLastPass = model.config.FeedrateLast;

                model.IsCSSEnabled = model.config.CSS;
                model.CssSpeed = (uint)(model.IsCSSEnabled ? last_css : last_rpm);
            }
        }

        private string FormatValue(double value)
        {
            return (string)Converters.CNCMeasureToTextConverter.Convert(value, typeof(string), model.IsMetric, CultureInfo.InvariantCulture);
        }

        public void Calculate()
        {
            model.ClearErrors();

            double speed = model.CssSpeed;
            bool css = model.IsCSSEnabled;

            if (model.FeedRate > model.config.ZMaxFeedRate)
            {
                model.SetError(nameof(model.FeedRate), "Feed rate > max allowed.");
                return;
            }

            if (model.FeedRate == 0.0d)
            {
                model.SetError(nameof(model.FeedRate), "Feed rate is required.");
                return;
            }

            if (speed == 0.0d)
            {
                model.SetError(nameof(model.RPM), "Spindle RPM is required.");
                return;
            }

            if (css)
                speed = Math.Round(speed / (Math.PI * model.XStart) * (model.IsMetric ? 1000.0d : 12.0d * 25.4d), 0);

            if (speed > model.config.RpmMax && model.config.CSSMaxRPM == 0.0d)
            {
                model.SetError(nameof(model.RPM), "Spindle RPM > max allowed.");
                return;
            }

            if (speed < model.config.RpmMin)
            {
                model.SetError(nameof(model.RPM), "Spindle RPM < min allowed.");
                return;
            }

            double passdepth = model.Passdepth / model.UnitFactor;
            double passdepth_last = model.PassdepthLastPass / model.UnitFactor;

            if (passdepth_last > passdepth)
            {
                model.SetError(nameof(model.Passdepth), "Last pass cut depth must be smaller than cut depth.");
                model.SetError(nameof(model.PassdepthLastPass), "Last pass cut depth must be smaller than cut depth.");
                return;
            }

            double zstart = model.ZStart / model.UnitFactor;
            double zlength = model.ZLength / model.UnitFactor;
            double ztarget = (zstart + zlength * model.config.ZDirection) / model.UnitFactor; ;
            double xclearance = model.XClearance / model.UnitFactor;
            double xtarget = model.XTarget / model.UnitFactor;
            double diameter = model.XStart / model.UnitFactor;

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
                angle = Math.Tan(Math.PI * model.Taper / 180.0d);

            //  error.Clear();

            if (cut.passes < 1)
            {
                model.SetError("Diameter", "Starting diameter must be larger than target.");
                return;
            }

            if (model.IsSpringPassesEnabled)
                cut.springpasses = (int)model.SpringPasses;

            if (boring)
                xclearance = -xclearance;

            uint pass = 1;

            model.gCode.Clear();

            model.gCode.Add(string.Format("G18 G{0} G{1}", model.config.xmode == LatheMode.Radius ? "8" : "7", model.IsMetric ? "21" : "20"));
            model.gCode.Add(string.Format("M3S{0} G4P1", speed.ToString()));
            model.gCode.Add(string.Format("G0 X{0}", FormatValue(diameter + xclearance)));
            model.gCode.Add(string.Format("G0 Z{0}", FormatValue(zstart + model.config.ZClearance / model.UnitFactor)));
            model.gCode.Add(css ? string.Format(model.config.CSSMaxRPM > 0.0d ? "G96S{0}D{1}" : "G96S{0}",
                                             model.CssSpeed, model.config.CSSMaxRPM) : "G97");

            do
            {
                xtarget = cut.GetPassTarget(pass, diameter, !boring);
                double feedrate = cut.IsLastPass ? model.FeedRateLastPass : model.FeedRate;

                model.gCode.Add(string.Format("(Pass: {0}, DOC: {1} {2})", pass, xtarget, cut.DOC));

                // diameter = Math.Max(diameter - passdepth, xtarget);
                // TODO: G0 to prev target to keep spindle speed constant?
                //     if (css)
                //         code[i++] = string.Format("G0 X{0}", FormatValue(doc_prev));
                model.gCode.Add(string.Format("G1 X{0} F{1}", FormatValue(xtarget), FormatValue(feedrate)));
                if (angle != 0.0d)
                {
                    ztarget = cut.Distance / angle * model.config.ZDirection;
                    model.gCode.Add(string.Format("G1 X{0} Z{1}", FormatValue(diameter), FormatValue(zstart + ztarget)));
                }
                else
                    model.gCode.Add(string.Format("G1 Z{0} F{1}", FormatValue(ztarget), FormatValue(feedrate)));
                model.gCode.Add(string.Format("G0 X{0}", FormatValue(xtarget + xclearance)));
                model.gCode.Add(string.Format("G0 Z{0}", FormatValue(zstart + model.config.ZClearance / model.UnitFactor)));

            } while (++pass <= cut.passes);

            if (GCodePush != null)
            {
                GCodePush("Wizard: Turning", Core.Action.New);
                GCodePush(string.Format("({0}, Start: {1}, Target: {2}, Length: {3})",
                                          boring ? "Boring" : "Turning",
                                          FormatValue(diameter), FormatValue(xtarget), FormatValue(zlength)), Core.Action.Add);
                GCodePush(string.Format("(Passdepth: {0}, Feedrate: {1}, {2}: {3})",
                                          FormatValue(passdepth), FormatValue(model.FeedRate),
                                          (css ? "CSS" : "RPM"), FormatValue((double)model.CssSpeed)), Core.Action.Add);

                foreach (string s in model.gCode)
                    GCodePush(s, Core.Action.Add);
                GCodePush("M30", Core.Action.End);
            }
        }
    }
}
