/*
 * Viewer.xaml.cs - part of CNC Controls library
 *
<<<<<<< HEAD
 * v0.36 / 2021-12-23 / Io Engineering (Terje Io)
=======
 * v0.33 / 2021-05-16 / Io Engineering (Terje Io)
>>>>>>> 19fdd92047b4cf80b9621a803d965739e89ec2a6
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
using CNC.GCode;
using CNC.Core;

namespace CNC.Controls.Viewer
{
<<<<<<< HEAD
    public partial class Viewer : UserControl, ICNCView
=======
/// <summary>
/// Interaction logic for Viewer.xaml
/// </summary>
public partial class Viewer : UserControl, ICNCView
>>>>>>> 19fdd92047b4cf80b9621a803d965739e89ec2a6
    {
        private bool isNew = false, isLoaded = false;

        public Viewer()
        {
            InitializeComponent();

            gcodeView.Machine.ShowJobEnvelope = false;
        }

        public ViewType ViewType { get { return ViewType.GCodeViewer; } }
        public bool CanEnable { get { return true; } }

<<<<<<< HEAD
=======
        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Configure();
        }

>>>>>>> 19fdd92047b4cf80b9621a803d965739e89ec2a6
        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                var d = DataContext as GrblViewModel;

                if(GCode.File.Tokens != null && isNew)
                {
                    isNew = false;
                    using (new UIUtils.WaitCursor())
                    {
                        gcodeView.ShowPosition();
                        gcodeView.Render(GCode.File.Tokens);
                    }
                }

                gcodeView.ShowPosition();
            }
        }

        public void CloseFile()
        {
            gcodeView.ClearViewport();
        }
        public void Configure()
        {
            gcodeView.ArcResolution = AppConfig.Settings.GCodeViewer.ArcResolution;
            gcodeView.MinDistance = AppConfig.Settings.GCodeViewer.MinDistance;
            gcodeView.ShowGrid = AppConfig.Settings.GCodeViewer.ShowGrid;
            gcodeView.ShowAxes = AppConfig.Settings.GCodeViewer.ShowAxes;
            gcodeView.ShowBoundingBox = AppConfig.Settings.GCodeViewer.ShowBoundingBox;
            gcodeView.Machine.ShowViewCube = AppConfig.Settings.GCodeViewer.ShowViewCube;
            gcodeView.Machine.ShowCoordinateSystem = AppConfig.Settings.GCodeViewer.ShowCoordinateSystem;
            gcodeView.Machine.CutMotionColor = AppConfig.Settings.GCodeViewer.CutMotionColor;
            gcodeView.Machine.RapidMotionColor = AppConfig.Settings.GCodeViewer.RapidMotionColor;
            gcodeView.Machine.RetractMotionColor = AppConfig.Settings.GCodeViewer.RetractMotionColor;
            gcodeView.Machine.ToolOriginColor = AppConfig.Settings.GCodeViewer.ToolOriginColor;
            gcodeView.Machine.GridColor = AppConfig.Settings.GCodeViewer.GridColor;
            gcodeView.Machine.CanvasColor = AppConfig.Settings.GCodeViewer.BlackBackground ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
            model.ConfigControls.Add(new ConfigControl());
<<<<<<< HEAD
        }

        public void Open(List<GCodeToken> tokens)
        {
            if (!(isNew = !IsVisible))
            {
                using (new UIUtils.WaitCursor())
                {
                    gcodeView.ShowPosition();
                    gcodeView.Render(tokens);
                }
            }
        }

        private void button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.ResetView();
=======

            Configure();
        }

        public void Open(List<GCodeToken> tokens)
        {
            if (!(isNew = !IsVisible))
            {
                using (new UIUtils.WaitCursor())
                {
                    gcodeView.ShowPosition();
                    gcodeView.Render(tokens);
                }
            }
        }

        private void button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.ResetView();
        }

        private void control_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!isLoaded && !System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                isLoaded = true;
                AppConfig.Settings.GCodeViewer.PropertyChanged += SettingsChanged;
            }
>>>>>>> 19fdd92047b4cf80b9621a803d965739e89ec2a6
        }
    }
}
