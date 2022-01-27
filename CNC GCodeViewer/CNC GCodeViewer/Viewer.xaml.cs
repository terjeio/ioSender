/*
 * Viewer.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-23 / Io Engineering (Terje Io)
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
    public partial class Viewer : UserControl, ICNCView
    {
        private bool isNew = false, isLoaded = false;

        public Viewer()
        {
            InitializeComponent();

            gcodeView.Machine.ShowJobEnvelope = false;
        }

        public ViewType ViewType { get { return ViewType.GCodeViewer; } }
        public bool CanEnable { get { return true; } }

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
                        gcodeView.Render(GCode.File.Tokens);
                        gcodeView.ShowPosition();
                    }
                }

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
    }
}
