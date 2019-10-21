/*
 * ThreadingWizard.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2019-05-27 / Io Engineering (Terje Io)
 *
 */

/*
 * Adapted from original code by Stephan Brunker (written in FreeBasic)
 *
 * Project Homepage:
 * www.sourceforge.net/p/mach3threadinghelper 
 * 
 */

/*

Additional code:

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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Core;
using CNC.View;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Data;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for ThreadingView.xaml
    /// </summary>
    public partial class ThreadingWizard : UserControl, CNCView
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

            logic = new ThreadLogic();

            DataContext = model = logic.Model;

            logic.GCodePush += Logic_GCodePush;
            logic.Model.ErrorsChanged += Model_ErrorsChanged;
        }
        public void ApplySettings(LatheConfig config)
        {
            model.wz.ApplySettings(config);
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
        }

        private void Logic_GCodePush(string gcode, Action action)
        {
            GCodePush?.Invoke(gcode, action); // Forward
        }

        #region Methods required by CNCView interface

        public ViewType mode { get { return ViewType.G76Threading; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate && GrblSettings.Loaded)
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

                    if (model.IsMetric != model.config.metric)
                    {
                        model.IsMetric = model.config.metric;
                        // cbxThreadType_SelectionChanged(null, null);
                        //  SetUnitLabels(this);
                    }
                }
                else
                    model.gCode.Clear();
            }
        }

        public void CloseFile()
        {
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
            logic.Calculate();
        }
    }
}