/*
 * Camera.xaml.cs - part of CNC Controls Camera library
 *
 * v0.38 / 2022-04-20 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io) - parts derived from AForge example code
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

namespace CNC.Controls.Camera
{
    /// <summary>
    /// Interaction logic for Camera.xaml
    /// </summary>
    public partial class Camera : Window, ICamera
    {
        private bool initialOpen = true, userClosing = true;

        public event IsVisibilityChangedHandler IsVisibilityChanged;
        public event CameraMoveOffsetHandler MoveOffset;

        public Camera()
        {
            InitializeComponent();

            CNCCamera.MoveOffset += CNCCamera_MoveOffset;
        }

        private void CNCCamera_MoveOffset(Core.CameraMoveMode Mode, double XOffset, double YOffset)
        {
            MoveOffset?.Invoke(Mode, XOffset, YOffset);
        }

        public bool HasCamera { get { return CNCCamera.HasCamera; } }
        public CameraControl CameraControl { get { return CNCCamera; } }
        public new bool IsVisible { get { return CNCCamera.IsVisible; } }
        public bool IsMoveEnabled { get { return CNCCamera.IsMoveEnabled; } set { CNCCamera.IsMoveEnabled = value && (CNCCamera.XOffset != 0d || CNCCamera.YOffset != 0d); } }

        public void Setup(UIViewModel model)
        {
            CameraConfig config = AppConfig.Settings.Camera;
            CameraControl.GuideScale = config.GuideScale;
            CameraControl.XOffset = config.XOffset;
            CameraControl.YOffset = config.YOffset;
            CameraControl.Mode = config.MoveMode;

            model.ConfigControls.Add(new ConfigControl());
        }

        public void Open()
        {
            if (initialOpen)
            {
                initialOpen = false;
                //this.Location = new Point(this.Owner.Location.X + 225, this.Owner.Location.Y + 35);
                //this.StartPosition = FormStartPosition.Manual;
            }

            Show();

            CameraControl.MoveCameraToSpindlePosition = AppConfig.Settings.Camera.InitialMoveToSpindle;

            if (CNCCamera.OpenVideoSource())
                IsVisibilityChanged?.Invoke();
        }

        public void CloseCamera()
        {
            userClosing = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (userClosing)
            {
                e.Cancel = true;
                Hide();
            }

            CNCCamera.CloseCurrentVideoSource();

            IsVisibilityChanged?.Invoke();
        }
    }
}
