/*
 * Renderer.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-25 / Io Engineering (Terje Io)
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

using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Viewer
{
    public partial class RenderControl : UserControl
    {
        private static bool keyboardMappingsOk = false;

        public RenderControl()
        {
            InitializeComponent();
        }

        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            textOverlay.Visibility = AppConfig.Settings.GCodeViewer.ShowTextOverlay ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        }

        public Machine MachineView
        {
            get { return gcodeView.Machine; }
        }

        public void Close()
        {
            gcodeView.ClearViewport();
        }

        public void Open(List<GCodeToken> tokens)
        {
            gcodeView.Render(tokens);
//            gcodeView.ShowPosition();
        }

        #region Keypresshandlers

        private bool ToggleGrid(Key key)
        {
            MachineView.ShowGrid = !MachineView.ShowGrid;
            return true;
        }
        private bool ToggleJobEnvelope(Key key)
        {
            MachineView.ShowJobEnvelope = !MachineView.ShowJobEnvelope;
            return true;
        }
        private bool ToggleWorkEnvelope(Key key)
        {
            MachineView.ShowWorkEnvelope = !MachineView.ShowWorkEnvelope;
            return true;
        }
        private bool RestoreView(Key key)
        {
            gcodeView.RestoreView();
            return true;
        }
        private bool ResetView(Key key)
        {
            gcodeView.ResetView();
            return true;
        }

        #endregion

        private void ResetView_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.ResetView();
        }

        private void SaveView_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.SaveView();
        }

        private void RestoreView_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.RestoreView();
        }

        private void RenderControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            textOverlay.Visibility = AppConfig.Settings.GCodeViewer.ShowTextOverlay ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

            if (!keyboardMappingsOk && DataContext is GrblViewModel)
            {
                KeypressHandler keyboard = (DataContext as GrblViewModel).Keyboard;

                keyboardMappingsOk = true;

                keyboard.AddHandler(Key.V, ModifierKeys.Control, ResetView);
                keyboard.AddHandler(Key.R, ModifierKeys.Control, RestoreView);
                keyboard.AddHandler(Key.G, ModifierKeys.Control, ToggleGrid);
                keyboard.AddHandler(Key.J, ModifierKeys.Control, ToggleJobEnvelope);
                keyboard.AddHandler(Key.W, ModifierKeys.Control, ToggleWorkEnvelope);
            }
        }
    }
}
