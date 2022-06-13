/*
 * CameraControl.xaml.cs - part of CNC Controls Camera library
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

        public delegate void MoveOffsetHandler(CameraMoveMode Mode, double XOffset, double YOffset);
        public event MoveOffsetHandler MoveOffset;

        private RenderTargetBitmap overlay = null;
        private DrawingVisual visual = null;
        private System.Windows.Media.Pen pen = null;
        private double _xOffset = 0d, _yOffset = 0d;

        public CameraControl()
        {
            InitializeComponent();

            DataContext = this;
        }

        private void CameraControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                AppConfig.Settings.Camera.PropertyChanged += Base_PropertyChanged;

            pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Red, 1);

            if (Cameras.Count > 0)
            {
                Camera = Cameras[0];
                if (AppConfig.Settings.Camera.SelectedCamera != string.Empty)
                {
                    foreach (FilterInfo camera in Cameras)
                        if (camera.MonikerString == AppConfig.Settings.Camera.SelectedCamera)
                            Camera = camera;
                }
                cbxCamera.SelectedItem = Camera;
            }
        }

        private void Base_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(AppConfig.Settings.Camera.XOffset):
                    XOffset = (sender as CameraConfig).XOffset;
                    break;

                case nameof(AppConfig.Settings.Camera.YOffset):
                    YOffset = (sender as CameraConfig).YOffset;
                    break;

                case nameof(AppConfig.Settings.Camera.MoveMode):
                    Mode = (sender as CameraConfig).MoveMode;
                    break;
            }
        }

        public static readonly DependencyProperty IsMoveEnabledProperty = DependencyProperty.Register(nameof(IsMoveEnabled), typeof(bool), typeof(CameraControl), new PropertyMetadata(false));
        public bool IsMoveEnabled
        {
            get { return (bool)GetValue(IsMoveEnabledProperty); }
            set { SetValue(IsMoveEnabledProperty, value); }
        }

        public static readonly DependencyProperty GuideScaleProperty = DependencyProperty.Register(nameof(GuideScale), typeof(int), typeof(CameraControl), new PropertyMetadata(10, new PropertyChangedCallback(OnGuideScaleChanged)));
        public int GuideScale
        {
            get { return (int)GetValue(GuideScaleProperty); }
            set { SetValue(GuideScaleProperty, value); }
        }
        private static void OnGuideScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AppConfig.Settings.Camera.GuideScale = (int)e.NewValue;
        }

        public static readonly DependencyProperty MoveCameraToSpindlePositionProperty = DependencyProperty.Register(nameof(MoveCameraToSpindlePosition), typeof(bool), typeof(CameraControl), new PropertyMetadata(false));
        public bool MoveCameraToSpindlePosition
        {
            get { return (bool)GetValue(MoveCameraToSpindlePositionProperty); }
            set { SetValue(MoveCameraToSpindlePositionProperty, value); }
        }

        public GrblViewModel grbl {  get { return Grbl.GrblViewModel;  } }

        public double XOffset
        {
            get { return _xOffset; }
            set { _xOffset = value; IsMoveEnabled = _xOffset != 0d || _yOffset != 0d; }
        }
        public double YOffset
        {
            get { return _yOffset; }
            set { _yOffset = value; IsMoveEnabled = _xOffset != 0d || _yOffset != 0d; }
        }
        public CameraMoveMode Mode { get; set; } = CameraMoveMode.BothAxes;
        public bool HasCamera { get { return Cameras.Count > 0; } }
        public bool IsCameraOpen { get { return videoSource != null; } }
        public FilterInfoCollection Cameras { get; private set; } = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        public FilterInfo Camera { get; private set; }

        public bool OpenVideoSource(FilterInfo camera)
        {
            CloseCurrentVideoSource();

            if (camera != null)
            {
                videoSource = new VideoCaptureDevice(camera.MonikerString);
                videoSource.Start();
                videoSource.NewFrame += videoSource_NewFrame;
            }

            return videoSource != null && videoSource.IsRunning;
        }

        public bool OpenVideoSource()
        {
            return IsCameraOpen || OpenVideoSource(Camera);
        }

        public void CloseCurrentVideoSource()
        {
            if (videoSource != null)
            {
                videoSource.NewFrame -= videoSource_NewFrame;
                videoSource.SignalToStop();

                // wait ~5 seconds
                for (int i = 0; i < 50; i++)
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
                r.DrawEllipse(null, pen, center, center.Y * GuideScale / 100d, center.Y * GuideScale / 100d);
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
         //   AppConfig.Settings.Camera.GuideScale = (int)sldcircle.Value;
        }

        private void btnMove_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConfig.Settings.Camera.ConfirmMove || MessageBox.Show(UIUtils.TryFindParent<Window>(this), (string)FindResource(MoveCameraToSpindlePosition ? "MoveCameraTo" : "MoveSpindleTo"), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (MoveCameraToSpindlePosition)
                    MoveOffset?.Invoke(Mode, -XOffset, -YOffset);
                else
                    MoveOffset?.Invoke(Mode, XOffset, YOffset);

                if (AppConfig.Settings.Camera.InitialMoveToSpindle)
                    MoveCameraToSpindlePosition = !MoveCameraToSpindlePosition;
            }
        }

        private void btnPublish_Click(object sender, RoutedEventArgs e)
        {
            Position pos = new Position(Grbl.GrblViewModel.MachinePosition);

            pos.X += XOffset;
            pos.Y += YOffset;

            Grbl.GrblViewModel.CameraProbed(pos);
        }

        private void cbxCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(((ComboBox)sender).IsDropDownOpen && e.AddedItems.Count == 1)
            {
                CloseCurrentVideoSource();
                OpenVideoSource(e.AddedItems[0] as FilterInfo);
                AppConfig.Settings.Camera.SelectedCamera = (e.AddedItems[0] as FilterInfo).MonikerString;
                AppConfig.Settings.Save();
            }
        }
    }
}
