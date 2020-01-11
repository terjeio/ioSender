/*
 * Camera.xaml.cs - part of CNC Controls library
 *
 * v0.03 / 2019-12-03 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io) - parts derived from AForge example code
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
    public partial class Camera : Window
    {
        private bool initialOpen = true, userClosing = true;

        public delegate void OpenedHandler();
        public event OpenedHandler Opened;

        public Camera()
        {
            InitializeComponent();
        }

        public bool HasCamera { get { return CNCCamera.HasCamera; } }
        public CameraControl CameraControl { get { return CNCCamera; } }

        public void ApplySettings(CameraConfig config)
        {
            CameraControl.XOffset = config.XOffset;
            CameraControl.YOffset = config.YOffset;
            CameraControl.Mode = config.MoveMode;
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

            if(CNCCamera.OpenVideoSource())
                Opened?.Invoke();
        }

        public void CloseCamera()
        {
            userClosing = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(userClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
                CNCCamera.CloseCurrentVideoSource();
        }
    }
}
