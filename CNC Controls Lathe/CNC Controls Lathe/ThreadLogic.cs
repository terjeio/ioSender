/*
 * ThreadLogic.cs - part of CNC Controls Lathe library
 *
 * v0.15 / 2020-04-04 / Io Engineering (Terje Io)
 *
 */

/*
 * Adapted from original code by Stephan Brunker (written in FreeBasic) [r16 - v0.32]
 *
 * Project Homepage:
 * www.sourceforge.net/p/mach3threadinghelper 
 * 
 */

/*

Copyright (c) 2019-2020, Io Engineering (Terje Io)
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
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    class ThreadLogic
    {
        private ThreadModel model = new ThreadModel();
        private bool suspendFractionInput = false;

        public ThreadLogic()
        {
            model.PropertyChanged += Model_PropertyChanged;
            model.Thread.PropertyChanged += Model_ThreadPropertyChanged;
            model.Inch.PropertyChanged += Inch_PropertyChanged;
            model.Thread.Type = Thread.type.First().Key;
            SetDefaults();
        }

        private void Inch_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!suspendFractionInput && !model.Inch.IsReadonly)
            {
                if (!(bool)model.Thread.ThreadSize.Row["Free"])
                {
                    if ((bool)model.Thread.ThreadSizes[0].Row["Free"])
                        model.Thread.ThreadSize = model.Thread.ThreadSizes[0];
                }

                switch (e.PropertyName)
                {
                    case nameof(model.Inch.Whole):
                    case nameof(model.Inch.Numerator):
                    case nameof(model.Inch.Denominator):
                        model.Thread.DiameterNominal = model.Inch.Value;
                        break;
                }
            }
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(model.Profile))
            {
                SetDefaults();
            }
        }

        private void SetDefaults()
        {
            if (model.Profile != null && model.config.IsLoaded)
            {
                model.RPM = model.Profile.RPM;
            }
        }

        public void ResetUI()
        {
            model.ClearErrors();
            model.gCode.Clear();
            model.PassData.Clear();

            model.Tool.Angle = 60;
            model.Tool.TipMinimum = double.NaN;
            model.Tool.TipMaximum = double.NaN;
            model.Thread.CutDepth = double.NaN;
            model.Thread.Diameter = double.NaN;
            model.Thread.DiameterTolerance = double.NaN;
        }

        void EnableMetricInput(bool enable)
        {
            model.Inch.IsMetricInputReadonly = !enable;
            model.Thread.IsMetric = model.IsMetric || enable;
        }

        void OnlyInside(bool enable)
        {
            model.Thread.Sides = enable ? Thread.Side.Inside : Thread.Side.Both;
            ResetUI();
        }

        void OnlyOutside(bool enable)
        {
            model.Thread.Sides = enable ? Thread.Side.Outside : Thread.Side.Both;
            ResetUI();
        }

        void CombotextToMetric(string text)
        {
            string[] values = text.Split(' ');
            model.Thread.DiameterNominal = double.Parse(values[1], CultureInfo.InvariantCulture);
            model.Thread.Lead = double.Parse(values[3], CultureInfo.InvariantCulture);
            if (!model.IsMetric)
            {
                model.Thread.TPI = model.Thread.Lead / model.UnitFactor;
                double size = model.Thread.DiameterNominal / model.UnitFactor;
                double inches = Math.Floor(size);
                model.Inch.Whole = inches == 0.0d ? double.NaN : inches;
                model.Inch.Numerator = (Math.Round((size - inches), 3) * 1000.0d);
                model.Inch.Denominator = 1000;
            }
        }

        void CombotextToInches(string text)
        {
            string[] values = text.Split('-');

            values[0] = values[0].Trim();

            double size, tpi = values.Length == 1 ? 0.0d : Fraction(ref values[1]);
            string[] fractions;

            if (values[0].StartsWith("#"))
            {
                size = ThreadData.TUN[uint.Parse(values[0].TrimStart('#'))];

                model.Inch.Whole = double.NaN;
                model.Inch.Numerator = size * 1000.0d;
                model.Inch.Denominator = 1000;
            }
            else
            {
                size = Fraction(ref values[0]);
                fractions = values[0].Split(' ');
                model.Inch.Whole = fractions[0] == "" ? double.NaN : double.Parse(fractions[0]);
                model.Inch.Numerator = fractions.Length > 1 ? double.Parse(fractions[1]) : double.NaN;
                model.Inch.Denominator = fractions.Length > 2 ? double.Parse(fractions[2]) : double.NaN;
            }

            if (tpi > 0.0d)
            {
                model.Thread.TPI = tpi;
                model.Thread.Lead = (model.IsMetric ? 25.4d : 1.0d) / tpi;
                model.Thread.DiameterNominal = size * (model.IsMetric ? 25.4d : 1.0d);
            }
        }
        double Fraction(ref string value)
        {
            double fraction = 0.0d;
            char[] charsToTrim = { '"', '\'' };

            string[] values = value.Trim().TrimEnd(charsToTrim).Split(' ');

            value = "";

            foreach (string v in values)
            {
                if (v.Contains('/'))
                {
                    string[] x = v.Split('/');
                    fraction += double.Parse(x[0]) / double.Parse(x[1]);
                    value += " " + x[0] + " " + x[1];
                }
                else if (v.Length > 0 && char.IsDigit(v, 0))
                {
                    fraction += double.Parse(v);
                    value = v;
                }
            }

            return fraction;
        }

        private void Model_ThreadPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(model.Thread.ThreadSize) && model.Thread.ThreadSize != null)
            {
                suspendFractionInput = true;

                DataRow selection = model.Thread.ThreadSize.Row;

                if (!(bool)selection["Free"])
                {
                    model.Thread.FixedLength = false;
                    model.Thread.OneLead = false;
                    model.Inch.IsReadonly = true;
                    EnableMetricInput(false);
                    model.Thread.Sides = Thread.Side.Both;
                    model.Thread.RetractDegrees = 360;
                    model.Thread.TaperControlsVisibility = Visibility.Hidden;
                    model.Tool.Angle = 60;

                    Thread.Type ttype = (Thread.Type)(selection["type"]);

                    switch (ttype)
                    {
                        case Thread.Type.M_4G4H:
                        case Thread.Type.M_6G6H:
                            EnableMetricInput(true);
                            CombotextToMetric((string)selection["Name"]);
                            break;

                        case Thread.Type.M_KEG_K:
                        case Thread.Type.M_KEG_L:
                            model.Thread.OneLead = true;
                            OnlyOutside(true);
                            model.Thread.FixedLength = true;
                            EnableMetricInput(true);
                            model.Thread.TaperControlsVisibility = Visibility.Visible;
                            model.Thread.Taper = 1.7899d;
                            CombotextToMetric((string)selection["Name"]);
                            break;

                        case Thread.Type.UNC_2:
                        case Thread.Type.UNC_3:
                        case Thread.Type.UNF_2:
                        case Thread.Type.UNF_3:
                        case Thread.Type.UNEF_2:
                        case Thread.Type.UNEF_3:
                        case Thread.Type.BSW:
                        case Thread.Type.BSF:
                            if (ttype == Thread.Type.BSW || ttype == Thread.Type.BSF)
                                model.Tool.Angle = 55;
                            model.Inch.IsReadonly = false;
                            CombotextToInches((string)selection["Name"]);
                            break;

                        case Thread.Type.G_A:
                        case Thread.Type.R:
                            {
                                double tpi, dia;
                                if (ttype == Thread.Type.G_A)
                                {
                                    ThreadG Gdata = new ThreadG(selection);
                                    tpi = Gdata.tpi;
                                    dia = Gdata.dia;
                                }
                                else
                                {
                                    ThreadR Rdata = new ThreadR(selection);
                                    tpi = Rdata.tpi;
                                    dia = Rdata.dia;
                                }
                                model.Tool.Angle = 55;
                                model.Thread.TPI = tpi;
                                model.Thread.Lead = 25.4d / tpi;
                                model.Thread.DiameterNominal = dia;
                                model.Thread.OneLead = true;
                                if (ttype == Thread.Type.R)
                                {
                                    model.Thread.FixedLength = true;
                                    model.Thread.Taper = 1.7899d;
                                }
                                model.Thread.TaperControlsVisibility = ttype == Thread.Type.R ? Visibility.Visible : Visibility.Hidden;
                                CombotextToInches((string)selection["Name"]);
                            }
                            break;

                        case Thread.Type.NPT:
                        case Thread.Type.NPTF:
                            model.Thread.OneLead = true;
                            model.Thread.FixedLength = true;
                            model.Thread.TaperControlsVisibility = Visibility.Visible;
                            model.Thread.Taper = 1.7899d;
                            model.Thread.RetractDegrees = ttype == Thread.Type.NPT ? 1260 : 720;
                            CombotextToInches((string)selection["Name"]);
                            break;

                        case Thread.Type.NPSM:
                        case Thread.Type.NPSC:
                        case Thread.Type.NPSF:
                            if (ttype == Thread.Type.NPSC || ttype == Thread.Type.NPSF)
                                OnlyInside(true);
                            model.Thread.OneLead = true;
                            CombotextToInches((string)selection["Name"]);
                            break;
                    }

                }
                suspendFractionInput = false;
            }
        }

        public ThreadModel Model { get { return model; } }
        public WizardConfig config { get; set; }

        public void Calculate()
        {
            double cta, cti, di, da, h, pitch, taperminus, taperplus;
            double fmax = 0.0d, fmin = 0.0d, nrmin = 0.0d, nrmax = 0.0d, minabfl = 0, maxabfl = 0;
            double fidia, fadia, tolfi, tolfa, toldi, tolda, tolab = 0.0d, testlena = 0.0d, testleni = 0.0d, zlena = 0.0d, zleni = 0.0d, bezlen, totlen = 0, maxdia = 0.0d, mindia = 0.0d;
            bool keg = false;
            double unit = 1.0; // Todo: fix

            double driver = 1; // smoothstepper vs paralell (3)
            double rfact = (double)model.config.xmode * unit;

            // stop compiler complaining...
            cta = cti = da = tolda = fadia = tolfa = pitch = fidia = tolfi = di = toldi = 0.0d;

            Thread.Type ttype = (Thread.Type)model.Thread.ThreadSize["type"];

            // ------------------------------------------------------
            // 	Get Data from ViewModel, convert everything to metric
            // ------------------------------------------------------

            double dia = model.Thread.DiameterNominal * model.Thread.UnitFactor; // thread dep!
            double lead = model.Thread.Lead * model.Thread.UnitFactor;
            double zlen = model.ZLength * model.UnitFactor;
            double tpi = model.Thread.TPI;
            double angle = Math.PI * model.Tool.Angle / 360.0d; // angle / 2 for further calculations
            double tw = model.Tool.Tip; // cvTool.Value;
            double retract = model.Thread.RetractDegrees;
            double taper = Math.PI * model.Thread.Taper / 180.0d;
            uint ph = model.Thread.Starts;//(uint)txtStarts.Value; // starts
            Thread.Toolshape toolshape = model.Tool.Shape;
            bool ag = model.Thread.Side == Thread.Side.Outside; // btnOutside.IsChecked == true;

            double xclear = model.config.XClearance;
            double incrmin = model.config.PassDepthMin;
            double rpm = model.RPM;

            model.ClearErrors();
            model.Tool.ClearErrors();
            model.Thread.ClearErrors();
            model.gCode.Clear();
            model.PassData.Clear();

            if (rpm * lead >= model.config.ZMaxFeedRate * 0.8d)
            {
                model.Thread.SetError(nameof(model.Thread.Lead), "Spindle RPM is too high for pitch.");
                model.SetError(nameof(model.RPM), "Spindle RPM is too high for pitch.");
                return;
            }

            if (rpm > model.config.RpmMax)
            {
                model.SetError(nameof(model.RPM), "Spindle RPM > max allowed.");
                return;
            }

            if (rpm < model.config.RpmMin)
            {
                model.SetError(nameof(model.RPM), "Spindle RPM < min allowed.");
                return;
            }

            ThreadCheckRange(ttype, dia, lead, ph, tpi);

            switch (ttype)
            {
                #region Metric
                case Thread.Type.M_4G4H:
                case Thread.Type.M_6G6H:
                    pitch = lead / ph;
                    h = pitch * Math.Sqrt(3.0d) * 0.5d;
                    cta = h * (7.0d / 8.0d);
                    cti = h * (3.0d / 4.0d);

                    tolab = pitch * 0.01077d + 0.016d;

                    if (ttype == Thread.Type.M_6G6H)
                    {
                        tolda = 0.00072d * Math.Pow(pitch, 3.0d) - 0.014398d * Math.Pow(pitch, 2.0d) + 0.1538d * pitch + 0.033d;
                        toldi = 0.0012945d * Math.Pow(pitch, 3.0d) - 0.021303 * Math.Pow(pitch, 2.0d) + 0.208d * pitch + 0.037d;
                        tolfa = dia * 0.00025d + 0.0007672d * Math.Pow(pitch, 3.0) - 0.012209d * Math.Pow(pitch, 2.0d) + 0.08219d * pitch + 0.0365d;
                        tolfi = dia * 0.0004d + 0.00087436d * Math.Pow(pitch, 3.0d) - 0.014196d * Math.Pow(pitch, 2.0d) + 0.10091d * pitch + 0.054d;
                    }
                    else
                    {
                        tolda = 0.00074732d * Math.Pow(pitch, 3.0d) - 0.012864d * Math.Pow(pitch, 2.0d) + 0.1095d * pitch + 0.014d;
                        toldi = 0.0010564d * Math.Pow(pitch, 3.0d) - 0.016647d * Math.Pow(pitch, 2.0d) + 0.1424d * pitch + 0.015d;
                        tolfa = dia * 0.00015d + 0.0004277d * Math.Pow(pitch, 3.0d) - 0.006975d * Math.Pow(pitch, 2.0d) + 0.0498d * pitch + 0.024d;
                        tolfi = dia * 0.00025d + 0.00055134d * Math.Pow(pitch, 3.0d) - 0.0092223d * Math.Pow(pitch, 2.0d) + 0.06553d * pitch + 0.032d;
                    }

                    cta += (tolfa - tolda) / 4.0d;
                    cti += (tolfi - toldi) / 4.0d;

                    da = dia - tolab - tolda / 2.0d;
                    di = dia - h * (5.0d / 4.0d) + toldi / 2.0d;
                    fadia = dia - h * (6.0d / 8.0d) - tolab - tolfa / 2.0d;
                    fidia = dia - h * (6.0d / 8.0d) + tolfi / 2.0d;

                    nrmax = ag ? h / 6.0d : (h / 8.0) / Math.Cos(angle);

                    fmax = 2.0d * nrmax * ((1.0d / Math.Cos(angle) - Math.Tan(angle)));
                    nrmin = 0.0d;
                    fmin = 0.0d;
                    break;
                #endregion
                #region MetricTapered
                case Thread.Type.M_KEG_L:
                case Thread.Type.M_KEG_K: // M Keg lang (nur Außengewinde)
                    keg = true;
                    pitch = lead;
                    h = pitch * Math.Sqrt(3.0d) * 0.5d;
                    cta = h * (7.0d / 8.0d);
                    if (ttype == Thread.Type.M_KEG_L)
                    {
                        totlen = 5.0d * pitch + 1.0d;
                        bezlen = 2.15d * pitch + 0.3d;    // Bezugslänge: wo es den Nominaldurchmesser hat
                        testlena = 5.28d * pitch - 1.42d; // Prüflänge
                        tolfa = 2.0d * pitch * 0.047d - 0.007d;
                    }
                    else
                    { // if ttype = M_KEG_K Then
                        totlen = 7.0d * pitch - 3.0d;
                        bezlen = pitch + 1.0d;
                        testlena = pitch * 5.0d - 2.0d;
                        tolfa = 2.0d * pitch * 0.034d - 0.004d;
                    }
                    tolda = tolfa;
                    zlena = totlen + retract / 360.0d * pitch;
                    fadia = dia - h * (6.0d / 8.0d) + (testlena - bezlen) * Math.Tan(taper) * 2.0d;
                    da = dia + (testlena - bezlen) * Math.Tan(taper) * 2.0d;
                    taperminus = bezlen * Math.Tan(taper);
                    taperplus = (totlen - bezlen) * Math.Tan(taper);
                    maxdia = (dia * 0.5d + taperplus) * 2.0d;

                    // Tool tip
                    nrmax = h / 6.0d;
                    fmax = 2.0d * nrmax * ((1.0d / Math.Cos(angle) - Math.Tan(angle)));
                    nrmin = 0.0d;
                    fmin = 0.0d;
                    break;
                #endregion
                #region UN*
                case Thread.Type.UNC_2:
                case Thread.Type.UNC_3:
                case Thread.Type.UNF_2:
                case Thread.Type.UNF_3:
                case Thread.Type.UNEF_2:
                case Thread.Type.UNEF_3:

                    dia *= unit;
                    lead = 25.4d / tpi;
                    pitch = lead / ph;
                    h = 0.8660254d * pitch;

                    cta = h * (7.0d / 8.0d); // nominal depth
                    cti = h * (3.0d / 4.0d); //

                    switch (ttype)
                    {

                        case Thread.Type.UNC_2:
                            toldi = pitch <= 1.27d ? dia * -0.04d - Math.Pow(pitch, 2.0d) * 0.171d + pitch * 0.634d + 0.006d
                                                   : Math.Pow(pitch, 2.0d) * -0.0158d + pitch * 0.251d - 0.003d;
                            tolfi = dia * 0.0008d + Math.Pow(pitch, 3.0d) * 0.000664d - Math.Pow(pitch, 2.0d) * 0.01038d + pitch * 0.0796d + 0.034d;
                            tolda = Math.Pow(pitch, 3.0d) * 0.00129 - Math.Pow(pitch, 2.0d) * 0.0181 + pitch * 0.152d + 0.039d;
                            tolfa = dia * 0.00045d + Math.Pow(pitch, 3.0d) * 0.00029d - Math.Pow(pitch, 2.0d) * 0.00551d + pitch * 0.0566d + 0.027d;
                            tolab = tolfa * 0.3d;
                            break;

                        case Thread.Type.UNC_3:
                            toldi = pitch <= 1.27d ? dia * -0.04d + Math.Pow(pitch, 3.0d) * 0.300086d + Math.Pow(pitch, 2.0d) * 0.86077d + pitch * 1.088d - 0.080d
                                                   : Math.Pow(pitch, 3.0d) * -0.002713d + Math.Pow(pitch, 2.0d) * 0.0406d - pitch * 0.0771d + 0.309d;
                            tolfi = dia * 0.0006d + Math.Pow(pitch, 3.0d) * 0.000424d - Math.Pow(pitch, 2.0d) * 0.00695 + pitch * 0.0575d + 0.026d;
                            tolda = Math.Pow(pitch, 3.0d) * 0.00129d - Math.Pow(pitch, 2.0d) * 0.0181d + pitch * 0.152d + 0.039d;
                            tolfa = dia * 0.0005d + Math.Pow(pitch, 3.0d) * 0.000271d - Math.Pow(pitch, 2.0d) * 0.00476d + pitch * 0.042d + 0.022d;
                            tolab = 0.0d;
                            break;

                        case Thread.Type.UNF_2:
                            toldi = pitch <= 0.794d ? dia * -0.03d + Math.Pow(pitch, 3.0d) * 0.798379d - Math.Pow(pitch, 2.0d) * 1.762d + pitch * 1.5429d - 0.165d
                                                    : Math.Pow(pitch, 3.0d) * -0.049524d + Math.Pow(pitch, 2.0d) * 0.22231d - pitch * 0.115d + 0.18d;
                            tolfi = dia * 0.0014d - Math.Pow(pitch, 2.0d) * 0.0149d + pitch * 0.0929d + 0.027d;
                            tolda = Math.Pow(pitch, 3.0d) * 0.008586d - Math.Pow(pitch, 2.0d) * 0.04953d + pitch * 0.1904d + 0.027d;
                            tolfa = dia * 0.001d + Math.Pow(pitch, 3.0d) * 0.000809d - Math.Pow(pitch, 2.0d) * 0.0117d + pitch * 0.069d + 0.023d;
                            tolab = tolfa * 0.3d;
                            break;

                        case Thread.Type.UNF_3:
                            toldi = pitch <= 0.794d ? dia * -0.03d - Math.Pow(pitch, 2.0d) * 0.28947d + pitch * 0.6625d
                                                    : Math.Pow(pitch, 3.0d) * -0.05026d + Math.Pow(pitch, 2.0d) * 0.27256d - pitch * 0.4197d + 0.397d;
                            tolfi = dia * 0.0012d - Math.Pow(pitch, 3.0d) * 0.00198d - Math.Pow(pitch, 2.0d) * 0.00453d + pitch * 0.0613d + 0.023d;
                            tolda = Math.Pow(pitch, 3.0d) * 0.008586d - Math.Pow(pitch, 2.0d) * 0.04953d + pitch * 0.1904d + 0.027d;
                            tolfa = dia * 0.001d - Math.Pow(pitch, 3.0d) * 0.000417d - Math.Pow(pitch, 2.0d) * 0.00892d + pitch * 0.0534d + 0.016d;
                            tolab = 0.0d;
                            break;

                        case Thread.Type.UNEF_2:
                            toldi = pitch * 0.191d + 0.05d;
                            tolfi = dia * 0.0005d + Math.Pow(pitch, 3.0d) * 0.207367d - Math.Pow(pitch, 2.0d) * 0.72142d + pitch * 0.8861d - 0.251d;
                            tolda = pitch * 0.114d + 0.06d;
                            tolfa = dia * 0.0006d + Math.Pow(pitch, 3.0d) * 0.10514d - Math.Pow(pitch, 2.0d) * 0.386d + pitch * 0.5035d - 0.133d;
                            tolab = tolfa * 0.3d;
                            break;

                        case Thread.Type.UNEF_3:
                            toldi = pitch * 0.0908d + 0.079d;
                            tolfi = dia * 0.0005d - Math.Pow(pitch, 3.0d) * 0.015389d + Math.Pow(pitch, 2.0d) * 0.01568d + pitch * 0.0648d + 0.023d;
                            tolda = pitch * 0.114d + 0.06d;
                            tolfa = dia * 0.0004 + Math.Pow(pitch, 3.0d) * 0.04014d - Math.Pow(pitch, 2.0d) * 0.15557d + pitch * 0.226d - 0.043d;
                            tolab = 0.0d;
                            break;
                    }

                    cta += (tolfa - tolda) / 4.0d;
                    cti += (tolfi - toldi) / 4.0d;

                    da = dia - tolab - tolda / 2.0d;
                    di = dia - h * (5.0d / 4.0d) + toldi / 2.0d;
                    fadia = dia - h * (6.0d / 8.0d) - tolab - tolfa / 2.0d;
                    fidia = dia - h * (6.0d / 8.0d) + tolfi / 2.0d;

                    // Tool tip
                    // Gewindegrund gerundet in Standard
                    nrmax = ag ? nrmax = h / 6.0d : (h / 8.0d) / Math.Cos(angle);
                    fmax = 2.0d * nrmax * ((1.0d / Math.Cos(angle) - Math.Tan(angle)));

                    nrmin = 0.0d;
                    fmin = 0.0d;
                    break;
                #endregion
                #region TubeThreads
                case Thread.Type.G_A:
                case Thread.Type.BSW:
                case Thread.Type.BSF:   //Rohrgewinde, Daten aus Tabelle

                    dia *= unit;
                    lead = 25.4d / tpi;
                    pitch = lead / ph;
                    h = 0.960491d * pitch;

                    switch (ttype)
                    {
                        case Thread.Type.G_A:
                            ThreadG ThreadGdata = new ThreadG(model.Thread.ThreadSize.Row);

                            tolfi = ThreadGdata.tdf;
                            tolfa = tolfi;
                            tolda = ThreadGdata.tda;
                            toldi = ThreadGdata.tdi;
                            tolab = 0.0d;
                            dia = ThreadGdata.dia;
                            nrmax = pitch * 0.137929d;
                            nrmin = 0.0d;
                            break;

                        case Thread.Type.BSW:
                            tolfi = dia * 0.0009d + Math.Pow(pitch, 3.0d) * 0.000249d - Math.Pow(pitch, 2.0d) * 0.00597d + pitch * 0.0535d + 0.032d;
                            tolfa = tolfi;
                            tolab = dia < 20.638d ? 0.03d : (pitch > 1.27d ? pitch * 0.0126d + 0.014d : 0.0d);
                            tolda = dia * 0.0009d + Math.Pow(pitch, 3.0d) * 0.00044d - Math.Pow(pitch, 2.0d) * 0.0096d + pitch * 0.084d + 0.053d;
                            toldi = dia < 6.35d ? pitch * 0.24d + 0.084d : pitch * 0.2d + 0.178d;

                            nrmax = pitch * 0.13733;

                            if (ag)
                            {
                                minabfl = (Math.Pow(pitch, 3.0d) * 0.000107d - Math.Pow(pitch, 2.0d) * 0.00213d + pitch * 0.0138d + 0.12d) * pitch;
                                nrmin = minabfl / ((1.0d / Math.Sin(angle)) - 1.0d);
                            }
                            else
                                nrmin = 0.0d;
                            break;

                        case Thread.Type.BSF:
                            tolfi = dia * 0.0011d + Math.Pow(pitch, 3.0d) * 0.000328d - Math.Pow(pitch, 2.0d) * 0.00754d + pitch * 0.0575d + 0.038d;
                            tolfa = tolfi;
                            if (dia < 20.638d)
                                tolab = pitch * 0.0148d + 0.014d;

                            tolda = dia * 0.001d + Math.Pow(pitch, 3.0d) * 0.000764d - Math.Pow(pitch, 2.0d) * 0.013d + pitch * 0.093d + 0.059d;
                            toldi = dia < 9.525d ? pitch * 0.274d + 0.041d : pitch * 0.2d + 0.178d;

                            nrmax = pitch * 0.13733d;
                            if (ag)
                            {
                                minabfl = (Math.Pow(pitch, 3.0d) * 0.000107 - Math.Pow(pitch, 2.0d) * 0.00213d + pitch * 0.0138d + 0.12d) * pitch;
                                nrmin = minabfl / ((1.0d / Math.Sin(angle)) - 1.0d);
                            }
                            else
                                nrmin = 0.0d;
                            break;

                    }

                    cta = h * (5.0d / 6.0d); // nominal depth (gleich für Außen- und Innengewinde)
                    cti = cta;

                    cta += (tolfa - tolda) / 4.0d; // plus Flankenspiel minus Außenspiel (bereits im Durchmesser berücksichtigt)
                    cti += (tolfi - toldi) / 4.0d;

                    da = dia - tolab - tolda / 2.0d;
                    di = dia - h * (4.0d / 3.0d) + toldi / 2.0d;
                    fadia = dia - h * (2.0d / 3.0d) - tolab - tolfa / 2.0d;
                    fidia = dia - h * (2.0d / 3.0d) + tolfi / 2.0d;

                    //  'Tool tip
                    fmax = 2.0d * nrmax * ((1.0d / Math.Cos(angle) - Math.Tan(angle)));
                    fmin = 2.0d * nrmin * ((1.0d / Math.Cos(angle) - Math.Tan(angle)));

                    break;
                #endregion
                #region TubethreadsR
                case Thread.Type.R:
                    // Kegeliges Rohrgewinde als Innen und Außengewinde
                    // Übergabewerte an Schnittfunktion: keg=TRUE, taper (Winkel), ct (Schnitttiefe für Spitze); pitch = lead 
                    //											  zlena/i( Gesamtlänge mit Auslauf); di/da (Außen und Kerndurchmesser am Gewindeanfang)
                    // Übergabewerte Messwerte: testlena/i (Position Messebene), maxdia/mindia (Durchmesser am Ende des nutzbaren Gewindes);
                    // 									da/di (Durchmesser in der Messebene), fadia/fidia (Flankendurchmesser)
                    // 								  tolfa/tolfi, tolda/toldi wie normales Gewinde
                    // totlen ist nur intern
                    dia *= unit;
                    keg = true;
                    pitch = 25.4d / tpi;
                    lead = pitch;
                    h = 0.960491d * pitch;
                    cta = h * (5.0d / 6.0d); // nominal depth
                    cti = cta;

                    ThreadR Rdata = new ThreadR(model.Thread.ThreadSize.Row);

                    dia = Rdata.dia;
                    // calculate thread length
                    zlena = Rdata.tl + pitch * retract / 360.0d;
                    zleni = zlena;
                    taperminus = Rdata.pl * Math.Tan(taper);             // radiusvergrößerung von Start bis Prüflänge				
                    taperplus = (Rdata.tl - Rdata.pl) * Math.Tan(taper); // radiusvergrößerung von Prüflänge bis nutzbare Länge
                    testlena = Rdata.pl;
                    testleni = testlena;
                    tolab = 0.0d;
                    if (dia < 59.615d)
                    {
                        tolfa = 2.0d * pitch / 16.0d;
                        tolfi = 2.0d * pitch * 1.25 / 16.0d;
                    }
                    else
                    {
                        tolfa = 2.0d * pitch * 1.5d / 16.0d;
                        tolfi = 2.0d * pitch * 1.5d / 16.0d;
                    }
                    tolda = tolfa;
                    toldi = tolfi;

                    // Messwerte							
                    da = dia;
                    di = (dia * 0.5d - h * 4.0d / 3.0d + taperplus) * 2.0d;
                    maxdia = (da * 0.5d + taperplus) * 2.0d;
                    mindia = (di * 0.5d - taperplus) * 2.0d;

                    fadia = dia - h * (2.0d / 3.0d);
                    fidia = fadia;

                    // Tool tip
                    nrmax = pitch * 0.137929d;
                    fmax = 2.0d * nrmax * ((1 / Math.Cos(angle) - Math.Tan(angle)));
                    nrmin = 0.0d;
                    fmin = 0.0d;
                    break;
                #endregion
                #region NPSM
                case Thread.Type.NPSM:

                    dia *= unit;
                    lead = 25.4 / tpi;
                    pitch = lead * ph;
                    h = 0.8660254d * pitch;

                    cta = h * (7.0d / 8.0d);        // nominal depth
                    cti = h * (3.0d / 4.0d);

                    ThreadNPSM NPSMdata = new ThreadNPSM(model.Thread.ThreadSize.Row);

                    tolfi = NPSMdata.tfi;
                    tolfa = NPSMdata.tfa;
                    tolda = NPSMdata.tda;
                    toldi = NPSMdata.tdi;
                    tolab = 0.0d;
                    da = NPSMdata.da - tolda / 2.0d;
                    di = NPSMdata.kd + toldi / 2.0d;
                    fadia = NPSMdata.da - h * (6.0d / 8.0d) - tolfa / 2.0d;
                    fidia = NPSMdata.kd + h * (4.0d / 8.0d) + tolfi / 2.0d;

                    // Gewindegrund flach in Standard
                    fmax = 2.0d * h * (1.0d / 8.0d) * Math.Tan(angle);
                    nrmax = fmax / (2.0d * Math.Cos(angle));
                    nrmin = 0.0d;
                    fmin = 0.0d;
                    break;
                #endregion
                #region NPT*
                case Thread.Type.NPT:
                case Thread.Type.NPTF:

                    keg = true;
                    lead = 25.4d / tpi;
                    pitch = lead;
                    h = pitch * Math.Sqrt(3.0d) * 0.5d;

                    ThreadNPT NPTdata = new ThreadNPT(model.Thread.ThreadSize.Row);

                    TAbfl TAbflNPT = new TAbfl(ttype, NPTdata.tpi);
                    if (!TAbflNPT.found)
                        return;

                    switch (ttype)
                    {
                        case Thread.Type.NPT:
                            maxabfl = TAbflNPT.smax;
                            minabfl = TAbflNPT.smin;
                            toldi = (maxabfl - minabfl) * 2.0d;
                            tolda = toldi;
                            cta = h - (maxabfl + minabfl) / 2.0d;
                            cti = cta;
                            break;

                        case Thread.Type.NPTF:
                            tolda = TAbflNPT.smax - TAbflNPT.smin; // Abflachung an der Spitze = Toleranz Durchmesser
                            toldi = tolda;
                            maxabfl = TAbflNPT.gmax;    // Abflachung am Grund = Werkzeugform
                            minabfl = TAbflNPT.gmin;
                            cta = h - TAbflNPT.smax + TAbflNPT.smin / 2.0d;
                            cti = cta;
                            break;
                    }

                    // Innengewinde 
                    testleni = NPTdata.pli;
                    fidia = NPTdata.fdi;

                    switch (ttype)
                    {
                        case Thread.Type.NPT:
                            totlen = NPTdata.tpi > 8.0d ? NPTdata.pli + 3.0d * pitch
                                                        : NPTdata.pli + 2.0d * pitch;
                            di = fidia - h + maxabfl + minabfl;
                            break;

                        case Thread.Type.NPTF:
                            totlen = NPTdata.pli + 4.0d * pitch;
                            di = fidia - h + TAbflNPT.smax + TAbflNPT.smin;
                            break;
                    }

                    taperplus = (totlen - testleni) * Math.Tan(taper);
                    mindia = di - taperplus;
                    zleni = totlen + pitch * retract / 360.0d;
                    tolfi = pitch * Math.Tan(taper) * 4.0d;

                    // Außengewinde
                    switch (ttype)
                    {
                        case Thread.Type.NPT:
                            totlen = NPTdata.pla; // L2
                            testlena = NPTdata.pla - 2.0d * pitch; // L5 = L2 - 2P
                            fadia = NPTdata.fda - (4.0d * pitch * Math.Tan(taper));
                            da = fadia + h - maxabfl - minabfl;
                            break;

                        case Thread.Type.NPTF:
                            totlen = NPTdata.pla + 1.0d * pitch;
                            testlena = NPTdata.pla;
                            fadia = NPTdata.fda;
                            da = fadia + h - TAbflNPT.smax - TAbflNPT.smin;
                            break;
                    }

                    taperplus = (totlen - testlena) * Math.Tan(taper);
                    maxdia = da + taperplus;
                    zlena = totlen + pitch * retract / 360.0d;
                    tolfa = tolfi;

                    // tool tip
                    fmax = 2.0d * maxabfl * Math.Tan(angle);
                    fmin = 2.0d * minabfl * Math.Tan(angle);
                    nrmax = maxabfl * Math.Tan(angle) / Math.Cos(angle);
                    nrmin = minabfl / ((1.0d / Math.Sin(angle)) - 1.0d);
                    break;
                #endregion
                #region NPS*
                case Thread.Type.NPSC:
                case Thread.Type.NPSF:  // nur Innengewinde
                    pitch = 25.4d / tpi;
                    lead = pitch;
                    h = 0.8660254d * pitch;

                    TAbfl TAbflNPT2 = new TAbfl(ttype == Thread.Type.NPSF ? Thread.Type.NPTF : Thread.Type.NPT, tpi);
                    if (!TAbflNPT2.found)
                        return;

                    ThreadNPSC NPSCdata = new ThreadNPSC(model.Thread.ThreadSize.Row);

                    switch (ttype)
                    {

                        case Thread.Type.NPSC:

                            maxabfl = TAbflNPT2.smax;
                            minabfl = TAbflNPT2.smin;
                            toldi = (maxabfl - minabfl) * 2.0d;
                            fidia = NPSCdata.fd;
                            tolfi = NPSCdata.tolf;
                            di = fidia - h + maxabfl + minabfl;
                            cti = h - (maxabfl + minabfl) / 2.0d;
                            break;

                        case Thread.Type.NPSF:
                            toldi = TAbflNPT2.smax - TAbflNPT2.smin;
                            maxabfl = TAbflNPT2.gmax;
                            minabfl = TAbflNPT2.gmin;
                            fidia = NPSCdata.fd;
                            tolfi = NPSCdata.tolf;
                            di = fidia - h + TAbflNPT2.smax + TAbflNPT2.smin;
                            cti = h - (TAbflNPT2.smax + TAbflNPT2.smin) / 2.0d;
                            break;
                    }

                    // Kerndurchmesser ist relativ zu Flankendurchmesser		
                    // tool tip
                    fmax = 2.0d * maxabfl * Math.Tan(angle);
                    fmin = 2.0d * minabfl * Math.Tan(angle);
                    nrmax = maxabfl * Math.Tan(angle) / Math.Cos(angle);
                    nrmin = minabfl / ((1.0d / Math.Sin(angle)) - 1.0d);
                    break;
                    #endregion
            }

            //---------------------------
            //	Limit calculations Part 2
            //---------------------------

            G76Parameters thread = new G76Parameters();

            thread.push = true; // TODO: add checkbox
            thread.xmode = model.config.xmode;
            thread.starts = ph;
            thread.intern = !ag;
            thread.tapered = keg;
            thread.taperangle = taper;
            thread.testlen = ag ? testlena : testleni;
            thread.maxdiameter = ag ? maxdia : mindia;
            thread.firstpass = model.config.PassDepthFirst;
            thread.pitch = lead;
            thread.diameter = (ag ? da : di);
            thread.xclear = model.config.XClearance;
            thread.depth = ag ? cta : cti; // cut depth - always in radius
            thread.zstart = model.ZStart;
            thread.rpm = rpm; // cvSpindleRPM.Value;
            thread.zaccdist = model.config.ZClearance + lead * 0.5d + lead * driver + Math.Pow(thread.rpm * lead / 60.0d, 2.0d) / (2.0d * model.config.ZAcceleration);
            thread.springpasses = model.SpringPasses; // springPasses.IsChecked == true ? (uint)springPasses.Value : 0;

            // Mach3 options
            thread.angle = Math.Round(angle * 180.0d / Math.PI, 1) - 0.5d;
            thread.retract = model.Thread.RetractDegrees;
            // end Mach3 options
            //LinuxCNC options
            thread.taperlength = model.Thread.TaperLength;
            thread.taper = model.Thread.TaperType;
            thread.compoundangle = model.Thread.CompoundAngle;
            thread.degression = model.Thread.DepthDegression == "None" ? 0d : dbl.Parse(model.Thread.DepthDegression); // == "None" ? 0.0f : cbxDepthDegression.Value;
            if (thread.degression > 0.0d && thread.degression < 1.0d)
            {
                model.Thread.SetError(nameof(model.Thread.DepthDegression), "Minimum value is 1.0");
                return;
            }
            // End LinuxCNC options

            if (model.config.xmode == LatheMode.Diameter)
            {
                incrmin *= 0.5d;
                xclear *= 0.5d;
            }

            // correction of depth for toolshape
            // only warning if radius/chamfer too small
            /*
            nrmax = Math.Round(nrmax, 2);
            nrmin = Math.Round(nrmin, 2);
            fmax = Math.Round(fmax, 2);
            fmin = Math.Round(fmin, 2);
            */
            if (toolshape == Thread.Toolshape.Rounded)
            {
                thread.depth += tw - (tw / Math.Sin(angle));
                if (tw > nrmax)
                {
                    model.Tool.SetError(nameof(model.Tool.Tip), string.Format("Tooltip is too big (max {0}).", model.FormatValue(nrmax)));
                    return;
                }
                else if (tw < nrmin)
                {
                    model.Tool.SetError(nameof(model.Tool.Tip), string.Format("Warning: Tooltip is too small (min {0}).", model.FormatValue(nrmin)));
                }
            }
            else
            { // Chamfer
                thread.depth -= tw / (2.0d * Math.Tan(angle));
                if (tw > fmax)
                {
                    model.Tool.SetError(nameof(model.Tool.Tip), string.Format("Tooltip is too big (max {0}).", model.FormatValue(fmax)));
                    return;
                }
                else if (tw < fmin)
                {
                    model.Tool.SetError(nameof(model.Tool.Tip), string.Format("Warning: Tooltip is too small (min {0}).", model.FormatValue(fmin)));
                }
            }

            if (model.GCodeFormat != Thread.Format.LinuxCNC)
            {
                // calculate x retraction
                // calculate min value for retraction
                double retmin = ag ? ((da + tolda / 2.0d) - (fadia - tolfa / 2.0d)) * 0.5 * Math.Tan(angle) + pitch / 4.0
                                   : ((fidia + tolfi / 2.0d) - (di - toldi / 2.0d)) * 0.5 * Math.Tan(angle) + pitch / 4.0;
                if (retmin > (retract / 360.0d) * lead)
                {
                    retract = -Math.Round(-retmin * 360.0d / lead, 0);
                    model.SetError("Retraction is negative in Z, using minimum.");
                }

                // calculate speed and acceleration
                double sacc = Math.Pow(model.config.XMaxFeedRate / 60.0d, 2.0d) / (2.0d * model.config.XAcceleration); // x acceleration distance
                double leadspeed = lead * thread.rpm / 60.0d;                                 // Z speed in mm/sec
                double sbreak = Math.Pow(leadspeed, 2.0d) / (2.0d * model.config.ZAcceleration);           // Z deceleration distance
                double sret = retract * lead / 360;                                           // Z retraction distance
                double tretZ = 0.0d, tretX;

                if (sret > sbreak) // breaking distance smaller than deceleration
                                   // total time for retraction in Z
                    tretZ = leadspeed / model.config.ZAcceleration + (sret - sbreak) / leadspeed; // breaking distance plus normal speed for rest
                else
                {
                    model.SetError(nameof(model.RPM), "Retraction too short / rotational speed too high for breaking Z-axis");
                    model.Thread.SetError(nameof(model.Thread.RetractDegrees), "Retraction too short / rotational speed too high for breaking Z-axis");
                    return;
                }

                if (thread.depth + xclear <= 2.0d * sacc)
                {
                    // only retraction part
                    if (xclear < sacc)
                        // total movement including clearance, clear only deceleration
                        tretX = Math.Sqrt((thread.depth + xclear) / model.config.XAcceleration) * 2.0d - Math.Sqrt(2.0 * xclear / model.config.XAcceleration);
                    else
                        tretX = Math.Sqrt(2.0d * thread.depth / model.config.XAcceleration); // ct only acceleration
                }
                else
                {
                    if (xclear < sacc)
                    {
                        tretX = 2 * Math.Sqrt(2.0d * sacc / model.config.XAcceleration) + (thread.depth + xclear - 2.0d * sacc) * 60.0d / model.config.XMaxFeedRate;
                        tretX -= Math.Sqrt(2.0d * xclear / model.config.XAcceleration);  //clear only deceleration
                    }
                    else if (thread.depth < sacc)
                        tretX = Math.Sqrt(2.0d * thread.depth / model.config.XAcceleration); //		'ct only acceleration
                    else
                        tretX = Math.Sqrt(2.0d * sacc / model.config.XAcceleration) + (thread.depth - sacc) * 60.0d / model.config.XMaxFeedRate; //  'ct accel plus constant velocity
                }
                if (tretX > tretZ)
                {
                    model.SetError(nameof(model.RPM), "Retraction too short / rotational speed too high for acceleration X-axis");
                    model.Thread.SetError(nameof(model.Thread.RetractDegrees), "Retraction too short / rotational speed too high for acceleration X-axis");
                    return;
                }
            }

            //---------------------------
            //	show calculated diameters
            //---------------------------

            model.Thread.Diameter = thread.diameter;
            model.Thread.DiameterTolerance = (ag ? tolda : toldi) / 2.0d;
            model.Thread.PitchDiameter = (ag ? fadia : fidia);
            model.Thread.PitchDiameterTolerance = (ag ? tolfa : tolfi) / 2.0d;

            if (thread.tapered)
            {
                zlen = ag ? zlena : zleni;
                model.ZLength = zlen / model.UnitFactor;
                model.Thread.MeasurePosition = thread.testlen;
                model.Thread.MaxDiameter = thread.maxdiameter;
            }

            model.Tool.TipMaximum = (toolshape == Thread.Toolshape.Rounded ? nrmax : fmax);
            model.Tool.TipMinimum = (toolshape == Thread.Toolshape.Rounded ? nrmin : fmin);
            model.Thread.CutDepth = thread.depth * 2.0d / (double)model.config.xmode;

            thread.ztarget = thread.zstart + zlen * model.config.ZDirection;

            // -----------------
            // 	Calculate passes
            // -----------------

            if (model.GCodeFormat == Thread.Format.LinuxCNC)
            {
                double depth = 0.0d, depth_prev = 0.0d, inv_degression = 1.0d / (thread.degression == 0.0d ? 1.0d : thread.degression);
                double area = 0.0d, area_prev = 0.0d;
                uint passes = 0;

                model.PassData.Add("P   Depth Cut    Area");

                while (depth < thread.depth)
                {
                    depth = Math.Min(model.config.PassDepthFirst * Math.Pow(++passes, inv_degression), thread.depth);
                    area = Math.Tan(angle) * depth * depth;
                    model.PassData.Add(string.Format("{0} {1}  {2} {3}", passes.ToString("00"), model.FormatValue(depth * 2.0 / rfact), model.FormatValue((depth - depth_prev) * 2.0 / rfact), Math.Round(area - area_prev, 3)));
                    depth_prev = depth;
                    area_prev = area;
                }
            }
            else
            {
                thread.lastpass = model.config.PassDepthLast;
                uint passes = model.Thread.Mach3Passes;

                double cutpos = 0.0d, cutposold, increment = 0.0d, calcpos = 0.0d;
                bool exitloop = false, mindecr = true;
                double rct = thread.depth - thread.lastpass; // roughing cut depth, in radius
                if (thread.lastpass != 0.0d)
                    passes -= 1;
                for (int i = 1; i <= passes; i++)
                {
                    cutposold = cutpos;
                    if (mindecr)
                    {
                        switch (model.GCodeFormat)
                        {
                            case Thread.Format.Mach3Native: // equal cut area  d / sqr(t) * sqr(n)
                                calcpos = rct * Math.Sqrt((double)i) / Math.Sqrt((double)passes);
                                break;
                            case Thread.Format.Mach3Sandvik:
                                if (passes > 1)
                                    calcpos = i == 1 ? rct * Math.Sqrt(0.3d) / Math.Sqrt((double)(passes - 1)) : rct * Math.Sqrt((double)(i - 1)) / Math.Sqrt((double)(passes - 1));
                                else
                                    calcpos = rct;
                                break;
                        }
                        increment = calcpos - cutposold;
                        if (increment >= incrmin)
                            cutpos = calcpos;
                        else
                            mindecr = false;
                    }
                    if (!mindecr)
                    { // minimum decrement mode
                        cutpos = cutposold + incrmin;
                        if (cutpos > rct)
                        { // check if it's over (from M1076)
                            cutpos = rct;
                            exitloop = true;

                        }
                        increment = cutpos - cutposold;
                    }

                    if (i == 1)
                        thread.firstpass = increment;

                    model.PassData.Add(string.Format("{0} {1} {2}", i, model.FormatValue(increment * 2.0 / rfact), model.FormatValue(cutpos * 2.0 / rfact)));

                    if (exitloop || i == passes)
                        model.Thread.Mach3PassesExecuted = (uint)i;

                    if (exitloop)
                        break;
                }
                if (thread.lastpass != 0.0d)
                {
                    cutpos += thread.lastpass;

                    model.PassData.Add(string.Format("{0} {1} {2}", 6, model.FormatValue(thread.lastpass * 2.0 / rfact), model.FormatValue(cutpos * 2.0 / rfact)));

                    // show # of passes calculated
                }
            }

            if (model.GCodeFormat == Thread.Format.LinuxCNC)
                GCodeLinuxCNC(thread);
            else
                GCodeMach3(thread);
        }

        #region GCodeGenerator LinuxCNC

        void GCodeLinuxCNC(G76Parameters thread)
        {
            if (!thread.intern)
                thread.xclear = -thread.xclear;

            double xstart = thread.diameter - thread.xclear;

            if (thread.xmode == LatheMode.Radius)
            {
                xstart /= 2.0d;
                thread.xclear /= 2.0d;
            }
            else
            {
                thread.depth *= 2.0;
                thread.firstpass *= 2.0d;
            }

            if (!model.IsMetric)
                thread.ToImperial();

            model.gCode.Clear();
            model.gCode.Add(string.Format("G18 G{0} G{1}", thread.xmode == LatheMode.Radius ? "8" : "7", thread.IsImperial ? "20" : "21"));
            model.gCode.Add(string.Format("M3S{0} G4P1", ((uint)thread.rpm).ToString()));
            model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xstart)));
            model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(thread.zstart + model.config.ZClearance)));

            string code;

            while (thread.starts-- > 0)
            {
                code = string.Format("G76 P{0} Z{1} I{2} J{3} K{4}", model.FormatValue(thread.pitch), model.FormatValue(thread.ztarget), model.FormatValue(thread.xclear), model.FormatValue(thread.firstpass), model.FormatValue(thread.depth));

                if (thread.springpasses > 0)
                    code += string.Format(" H{0}", thread.springpasses);

                if (thread.compoundangle > 0.0d)
                    code += string.Format(" Q{0}", model.FormatValue(thread.compoundangle));

                if (thread.degression >= 1.0d)
                    code += string.Format(" R{0}", model.FormatValue(thread.degression));

                if (thread.taper != ThreadTaper.None && thread.taperlength > 0.0d)
                    code += string.Format(" L{0} E{1}", (uint)thread.taper, model.FormatValue(thread.taperlength));

                model.gCode.Add(code);
            }

            if (thread.push)
            {
                string threadSize = (string)model.Thread.ThreadSize["Name"];

                GCode.File.AddBlock(string.Format("Wizard: {0}{1}", model.Thread.Type.ToString(), threadSize == "" ? "" : ", " + threadSize.Trim()), Core.Action.New);
                if (threadSize != "")
                    GCode.File.AddBlock(string.Format("({0})", Uncomment(threadSize)), Core.Action.Add);
                foreach (string s in model.gCode)
                    GCode.File.AddBlock(s, Core.Action.Add);
                GCode.File.AddBlock("M30", Core.Action.End);
            }
        }

        #endregion

        #region GCodeGenerator Mach3

        void GCodeMach3(G76Parameters thread)
        {
            // TODO: verify!

            if (!thread.intern)
                thread.xclear = -thread.xclear;

            double xstart = -thread.diameter + thread.xclear;

            if (thread.xmode == LatheMode.Radius)
            {
                xstart /= 2.0d;
                thread.xclear /= 2.0d;
                //          thread.testlen /= 2.0d;

                thread.diameter /= 2.0d;
            }
            else
            {
                thread.firstpass *= 2.0d;
                thread.depth *= 2.0;
            }

            if (!model.IsMetric)
                thread.ToImperial();

            if (thread.tapered)
            {
                thread.diameter -= thread.testlen * Math.Tan(thread.taperangle);
                // make taper here...
                thread.diameter += Math.Tan(thread.taperangle) * thread.zaccdist * (thread.intern ? 1.0d : -1.0d);
            }

            model.gCode.Clear();
            model.gCode.Add(string.Format("G18 G{0} G{1}", thread.xmode == LatheMode.Radius ? "8" : "7", thread.IsImperial ? "20" : "21"));
            model.gCode.Add(string.Format("M3S{0} G4P1", ((uint)thread.rpm).ToString()));
            model.gCode.Add(string.Format("G0 X{0}", model.FormatValue(xstart)));
            model.gCode.Add(string.Format("G0 Z{0}", model.FormatValue(model.config.ZClearance)));

            uint i = 0;
            string code;

            while (thread.starts-- > 0)
            {
                code = string.Format("G76 P{0} X{1} Z{2} H{3} I{4} C{5}",
                    model.FormatValue(thread.pitch), model.FormatValue(thread.diameter + thread.depth * (thread.intern ? 1.0d : -1.0d)), model.FormatValue(thread.ztarget),
                    model.FormatValue(thread.firstpass), model.FormatValue(thread.angle), model.FormatValue(thread.xclear));

                code += string.Format(" R{0}", model.FormatValue(thread.diameter)); // X start (optional)

                code += string.Format(" K{0}", model.FormatValue(thread.zstart - (thread.pitch * i++ + thread.zaccdist) * model.config.ZDirection)); // Z start (optional)

                if (thread.lastpass != 0.0d)
                    code += string.Format(" B{0}", thread.lastpass);

                if (thread.springpasses > 0)
                    code += string.Format(" Q{0}", thread.springpasses);

                if (thread.retract != 0.0d) // = linucnc chamfer but in degrees
                    code += string.Format(" L{0}", model.FormatValue(thread.retract));

                model.gCode.Add(code);
            }
        }

        #endregion

        string Uncomment(string s)
        {
            return s.Replace('(', '[').Replace(')', ']').Trim();
        }

        bool ThreadCheckRange(Thread.Type ttype, double dia, double lead, uint ph, double tpi)
        {
            bool error = false;
            double pitch = lead / (double)ph;

            if (model.IsMetric) switch (ttype)
                {
                    case Thread.Type.BSW:
                    case Thread.Type.BSF:
                    case Thread.Type.UNC_2:
                    case Thread.Type.UNC_3:
                    case Thread.Type.UNF_2:
                    case Thread.Type.UNF_3:
                    case Thread.Type.UNEF_2:
                    case Thread.Type.UNEF_3:
                        dia /= 25.4;
                        break;
                }

            switch (ttype)
            {
                case Thread.Type.M_6G6H:
                case Thread.Type.M_4G4H:
                    error = pitch > 6.0d || dia > 180.0d;
                    break;

                case Thread.Type.M_KEG_L:
                case Thread.Type.M_KEG_K:
                    error = pitch < 0.8d || pitch > 1.5d || dia < 5.0d || dia > 30.0d;
                    break;

                case Thread.Type.BSW:
                    error = tpi < 2.5d || tpi > 60.0d || dia < 0.06125d || dia > 6.0d;
                    break;

                case Thread.Type.BSF:
                    error = tpi < 4.0d || tpi > 32.0d || dia < 0.073d || dia > 4.0d;
                    break;

                case Thread.Type.UNC_2:
                case Thread.Type.UNC_3:
                    error = tpi < 4.0d || tpi > 80.0d || dia < 0.073d || dia > 4.0d;
                    break;

                case Thread.Type.UNF_2:
                case Thread.Type.UNF_3:
                    error = tpi < 12.0d || tpi > 80.0d || dia < 0.06d || dia > 1.5d;
                    break;

                case Thread.Type.UNEF_2:
                case Thread.Type.UNEF_3:
                    error = tpi < 18.0d || tpi > 32.0d || dia < 0.2166d || dia > 1.6875d;
                    break;
            }

            if (error)
                model.SetError("You are outside the range of predefined thread parameters.\r\nCalculated values cannot be relied on!");

            return error;
        }
    }

    class G76Parameters
    {
        public double xclear, diameter, maxdiameter, depth, firstpass, angle, retract;
        public double zstart, ztarget, pitch, lastpass, testlen, taperangle;
        public double zaccdist, rpm, taperlength, compoundangle, degression;
        public uint springpasses, starts;
        public bool intern, push, tapered;
        public LatheMode xmode;
        public ThreadTaper taper;
        public bool IsImperial { get; private set; }

        public G76Parameters()
        {
            IsImperial = false;
        }

        public void ToImperial()
        {
            if (!IsImperial)
            {
                pitch /= 25.4d;
                diameter /= 25.4d;
                xclear /= 25.4d;
                zaccdist /= 25.4d;
                depth /= 25.4d;
                firstpass /= 25.4d;
                zstart /= 25.4d;
                ztarget /= 25.4d;
                taperlength /= 25.4d;
                testlen /= 25.4d;
                maxdiameter /= 25.4d;
                IsImperial = true;
            }
        }
    }
}
