/*
 * TurningWizard.xaml.cs - part of CNC Controls library
 *
 * v0.31 / 2021-04-27 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2021, Io Engineering (Terje Io)
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

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for TurningWizard.xaml
    /// </summary>
    public partial class TurningWizard : UserControl, ICNCView
    {
        private bool initOk = false, resetProfileBindings = true;
        private double last_rpm = 0d, last_css = 0d;
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
        }

        public ObservableCollection<string> gCode { get; private set; }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.Turning; } }
        public bool CanEnable { get { return true; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate && GrblSettings.IsLoaded)
            {
                if (!initOk)
                {
                    initOk = true;
                    if (config == null)
                    {
                        //   cbxProfile.BindOptions(config, mode);
                    }
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

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
            this.model.wz.ApplySettings(profile.Lathe);
            if (!model.IsConfigControlInstantiated<ConfigControl>())
                model.ConfigControls.Add(new ConfigControl());
        }

        #endregion
        public void InitUI()
        {
        }

        public WizardConfig config { get; private set; }

        private void btnCalculate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            logic.Calculate();
        }
    }
}
