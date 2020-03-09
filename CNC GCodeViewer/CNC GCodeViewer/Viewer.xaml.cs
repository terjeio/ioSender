/*
 * Viewer.xaml.cs - part of CNC Controls library
 *
 * v0.10 / 2019-03-05 / Io Engineering (Terje Io)
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

using System.Collections.Generic;
using System.Windows.Controls;
using CNC.GCode;

namespace CNC.Controls.Viewer
{
/// <summary>
/// Interaction logic for Viewer.xaml
/// </summary>
public partial class Viewer : UserControl, ICNCView
    {
        public Viewer()
        {
            InitializeComponent();

            gcodeView.ShowBoundingBox = false;
        }

        public int ArcResolution {
            get { return gcodeView.ArcResolution; }
            set { gcodeView.ArcResolution = value; }
        }
        public double MinDistance
        {
            get { return gcodeView.MinDistance; }
            set { gcodeView.MinDistance = value; }
        }
        public bool ShowGrid
        {
            get { return gcodeView.ShowGrid; }
            set { gcodeView.ShowGrid = value; }
        }
        public bool ShowAxes
        {
            get { return gcodeView.ShowAxes; }
            set { gcodeView.ShowAxes = value; }
        }
        public bool ShowBoundingBox
        {
            get { return gcodeView.ShowBoundingBox; }
            set { gcodeView.ShowBoundingBox = value; }
        }

        public bool ShowViewCube
        {
            get { return gcodeView.viewport.ShowViewCube; }
            set { gcodeView.viewport.ShowViewCube = value; }
        }

        public ViewType ViewType { get { return ViewType.GCodeViewer; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                gcodeView.ShowPosition();
            }
        }

        public void CloseFile()
        {
            gcodeView.ClearViewport();
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
            model.ConfigControls.Add(new ConfigControl());

            ArcResolution = profile.Config.GCodeViewer.ArcResolution;
            MinDistance = profile.Config.GCodeViewer.MinDistance;
            ShowGrid = profile.Config.GCodeViewer.ShowGrid;
            ShowAxes = profile.Config.GCodeViewer.ShowAxes;
            ShowBoundingBox = profile.Config.GCodeViewer.ShowBoundingBox;
            ShowViewCube = profile.Config.GCodeViewer.ShowViewCube;
        }

        public void Open(string title, List<GCodeToken> tokens)
        {
            gcodeView.ShowPosition();
            gcodeView.Render(tokens);
        }

        private void button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            gcodeView.ResetView();
        }
    }
}
