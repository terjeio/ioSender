/*
 * ConfigView.xaml.cs - part of Grbl Code Sender
 *
 * v0.02 / 2020-01-16 / Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using CNC.View;
using CNC.Core;

namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for ConfigView.xaml
    /// </summary>
    public partial class ConfigView : UserControl, CNCView
    {
        public ConfigView()
        {
            InitializeComponent();
        }

        #region Methods and properties required by CNCView interface

        public ViewType mode { get { return ViewType.AppConfig; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            cameraConfig.grblData = (GrblViewModel)MainWindow.ui.DataContext;
            if(MainWindow.Camera == null || !MainWindow.Camera.HasCamera)
                cameraConfig.Visibility = Visibility.Collapsed;

            if(GrblSettings.GetString(GrblSetting.JogStepSpeed) != null)
                jogConfig.Visibility = Visibility.Collapsed;
        }

        public void CloseFile()
        {
        }

        #endregion

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Profile.Save();
        }
    }
}
