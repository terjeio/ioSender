/*
 * TurningWizard.xaml.cs - part of CNC Controls library
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
using System.Collections.ObjectModel;
using System.Windows.Controls;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for TurningWizard.xaml
    /// </summary>
    public partial class TurningWizard : UserControl, ILatheWizardTab
    {
        private bool initOk = false, resetProfileBindings = true;
        private double last_rpm = 0d, last_css = 0d, length = 0d;
        private BaseViewModel model;
        private TurningLogic logic = new TurningLogic();

        public TurningWizard()
        {
            InitializeComponent();

            DataContext = model = logic.Model;
        }

        void TurningWizard_Load(object sender, EventArgs e)
        {
            //if (this.DesignMode)
            //    return;

            //error = new ErrorProvider(this);

            //UIUtils.SetMask(txtTaper, "#0.0##");
            //UIUtils.SetMask(txtSpindleRPM, "###0");
            ////    cvFeedRate.Format = "###0";

            //UIUtils.GroupBoxCaptionBold(groupBox1);
            //UIUtils.GroupBoxCaptionBold(groupBox2);
            taper.OnValueChanged += taperChanged;
            taper.OnTaperEnabledChanged += taperEnabled;
        }

        public ObservableCollection<string> gCode { get; private set; }

        #region Methods and properties required by CNCView interface

        public LatheWizardType LatheWizardType { get { return LatheWizardType.Turning; } }
        public bool CanEnable { get { return true; } }

        public void Activate(bool activate)
        {
            if (activate && GrblSettings.IsLoaded)
            {
                if (!initOk)
                {
                    initOk = true;

                    model.Profiles = model.wz.Load();
                    model.config.Update();

                    Converters.IsMetric = model.IsMetric = GrblParserState.IsMetric;
                    model.XStart = model.IsMetric ? 10.0d : 0.5d;
                }
                else
                {
                    model.gCode.Clear();
                    model.PassData.Clear();
                }
            }
        }

        #endregion
        public void InitUI()
        {

        }

        private void taperEnabled (bool enabled)
        {
            if (enabled)
                length = cvLength.Value;
            else
                cvLength.Value = length;

            cvLength.IsEnabled = !enabled;
        }

        private void taperChanged (double angle)
        {
           if(taper.IsTaperEnabled && angle != 0d && Math.Abs(model.XStart - model.XTarget) != 0.0d)
            {
                double xtarget = model.XTarget;
                double diameter = model.XStart;

                if (model.config.xmode == LatheMode.Radius)
                {
                    xtarget /= 2.0d;
                    diameter /= 2.0d;
                }

                //bool boring = (xtarget - diameter) > 0.0d; ??
                cvLength.Value = (xtarget - diameter) / Math.Tan(Math.PI * angle / 180.0d) * model.config.ZDirection;
            }
        }

        private void btnCalculate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            logic.Calculate();
        }
    }
}
