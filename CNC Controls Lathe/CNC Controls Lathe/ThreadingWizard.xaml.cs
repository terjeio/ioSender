/*
 * ThreadingWizard.xaml.cs - part of CNC Controls Lathe library
 *
 * v0.31 / 2021-04-27 / Io Engineering (Terje Io)
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

Additional code:

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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Core;
using System.Collections.Generic;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for ThreadingView.xaml
    /// </summary>
    public partial class ThreadingWizard : UserControl, ICNCView
    {
        private bool initOk = false, resetProfileBindings = true;

        private ThreadLogic logic;
        public WizardConfig config = null;

        private ThreadModel model;

        public event GCodePushHandler GCodePush;

        // private ErrorProvider error;

        public ThreadingWizard()
        {
            InitializeComponent();

            grpOptionsMach3.Visibility = Visibility.Hidden;

            logic = new ThreadLogic();

            DataContext = model = logic.Model;

            logic.Model.ErrorsChanged += Model_ErrorsChanged;
        }

        private void Model_ErrorsChanged(object sender, System.ComponentModel.DataErrorsChangedEventArgs e)
        {
            if(e.PropertyName == "")
                MessageBox.Show(e.PropertyName, string.Join(",", (List<string>)logic.Model.GetErrors(e.PropertyName)));
        }

        private void ThreadingWizard_Loaded(object sender, RoutedEventArgs e)
        {
            //cbxTapertype.ItemsSource = Enum.GetValues(typeof(ThreadTaper));
            //cbxTapertype.SelectedValue = cbxTapertype.Items[0];

            //cbxDepthDegression.Items.Add("None");
            //cbxDepthDegression.Items.Add("1.0");
            //cbxDepthDegression.Items.Add("2.0");
            //cbxDepthDegression.SelectedValue = cbxDepthDegression.Items[0];

            //UIUtils.GroupBoxCaptionBold(grpOptionsLinuxCNC);
            //UIUtils.GroupBoxCaptionBold(grpOptionsMach3);
            //UIUtils.GroupBoxCaptionBold(grpTool);
     //       logic.ResetUI();
        }

        private void Logic_GCodePush(string gcode, Action action)
        {
            GCodePush?.Invoke(gcode, action); // Forward
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.G76Threading; } }
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
                        //config = new WizardConfig(this, "Threading");
                        //config.Load();
                        //cbxProfile.BindOptions(config, mode);
                        //logic.config = config;
                    }

                    model.config.Update();

                    Converters.IsMetric = model.IsMetric = GrblParserState.IsMetric;;
                }
                else
                    model.gCode.Clear();
            }
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
            this.model.wz.ApplySettings(profile.Lathe);
        }

        #endregion

        public void InitUI()
        {
            switch (model.GCodeFormat)
            {
                case Thread.Format.Mach3Native:
                case Thread.Format.Mach3Sandvik:
                    grpOptionsLinuxCNC.Visibility = Visibility.Visible;
                    grpOptionsMach3.Margin = grpOptionsLinuxCNC.Margin; // new Point(grpOptionsLinuxCNC.Location.X, grpOptionsLinuxCNC.Location.Y);
                    grpOptionsMach3.Visibility = Visibility.Visible;
                    break;

                case Thread.Format.LinuxCNC:
                    grpOptionsLinuxCNC.Visibility = Visibility.Visible;
                    grpOptionsMach3.Visibility = Visibility.Hidden;
                    break;
            }

            //  model.rpm = config.rpm;
        }

        private void pictureTool_MouseUp(object sender, MouseButtonEventArgs e)
        {
            model.Tool.Shape = (((Image)sender).Name == "pictureChamfer") ? Thread.Toolshape.Chamfer : Thread.Toolshape.Rounded;
        }

        private void btnCalculate_Click(object sender, RoutedEventArgs e)
        {
            if(!model.Thread.CompoundAngles.Contains(cbxCompoundAngle.Value))
            {
                model.Thread.CompoundAngles.Add(cbxCompoundAngle.Value);
                model.Thread.CompoundAngle = cbxCompoundAngle.Value;
            }
            if (model.Thread.DepthDegression == null)
            {
                model.Thread.DepthDegressions.Add(cbxDepthDegression.Text);
                model.Thread.DepthDegression = cbxDepthDegression.Text;
            }
            logic.Calculate();
        }
    }
}