/*
 * CNCCameraControl.xaml.cs - part of CNC Controls library
 *
 * v0.01 / 2019-10-13 / Io Engineering (Terje Io)
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

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video.DirectShow;
using CNC.Core;

namespace CNC.Controls.Camera
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class CameraControl : UserControl
    {
        VideoCaptureDevice videoSource = null;
        public FilterInfoCollection LoaclWebCamsCollection;
        private int cpct = 20;

        public delegate void MoveOffsetHandler(CameraMoveMode Mode, double XOffset, double YOffset);
        public event MoveOffsetHandler MoveOffset;

        private RenderTargetBitmap overlay = null;
        private DrawingVisual visual = null;
        private System.Windows.Media.Pen pen = null;

        public CameraControl()
        {
            InitializeComponent();

            XOffset = YOffset = 0.0;
            Mode = CameraMoveMode.BothAxes;

            pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Red, 1);

            DataContext = this;
            Camera = Cameras[0];
            cbxCamera.SelectedItem = Camera;
        }

        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public CameraMoveMode Mode { get; set; }
        public bool IsCameraOpen { get { return videoSource != null; } }
        public FilterInfoCollection Cameras { get; private set; } = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        public FilterInfo Camera { get; private set; }

        public bool OpenVideoSource()
        {
            if (Camera != null)
            {
                videoSource = new VideoCaptureDevice(Camera.MonikerString);
                videoSource.NewFrame += videoSource_NewFrame;
                videoSource.Start();
            }

            return videoSource != null && videoSource.IsRunning;
        }

        public void CloseCurrentVideoSource()
        {
            if (videoSource != null)
            {
                videoSource.NewFrame -= videoSource_NewFrame;
                videoSource.SignalToStop();

                // wait ~ 3 seconds
                for (int i = 0; i < 30; i++)
                {
                    if (!videoSource.IsRunning)
                        break;
                    Thread.Sleep(100);
                }

                if (videoSource.IsRunning)
                    videoSource.Stop();

                videoSource = null;
            }
        }

        public void Overlay (BitmapImage bmp)
        {
            if (overlay == null)
            {
                overlay = new RenderTargetBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, PixelFormats.Pbgra32);
                visual = new DrawingVisual();
                frameHolder.Source = overlay;
            }
            else using (var r = visual.RenderOpen())
            {
                System.Windows.Point center = new System.Windows.Point(bmp.Width / 2.0f, bmp.Height / 2.0f);

                r.DrawImage(bmp, new Rect(0, 0, bmp.Width, bmp.Height));
                r.DrawLine(pen, new System.Windows.Point(0, center.Y), new System.Windows.Point(bmp.Width, center.Y));
                r.DrawLine(pen, new System.Windows.Point(center.X, 0), new System.Windows.Point(center.X, bmp.Height));
                r.DrawEllipse(null, pen, center, center.Y * cpct / 100d, center.Y * cpct / 100d);
            }
            overlay.Render(visual);
        }

        private void videoSource_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            try
            {
                BitmapImage bmp;
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    MemoryStream ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Bmp);
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                }
                bmp.Freeze();
                Dispatcher.BeginInvoke(new ThreadStart(delegate { Overlay(bmp); }));
                //Dispatcher.BeginInvoke(new ThreadStart(delegate { frameHolder.Source = bmp; }));
            }
            catch
            {
            }
        }

        private void sldcircle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            cpct = (int)sldcircle.Value;
        }

        private void btnMove_Click(object sender, RoutedEventArgs e)
        {
            MoveOffset?.Invoke(Mode, XOffset, YOffset);
        }

        private void cbxCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(((ComboBox)sender).IsDropDownOpen)
            {
                CloseCurrentVideoSource();
                OpenVideoSource();
            }
        }
    }
}
