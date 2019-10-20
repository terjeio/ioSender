/*
 * About.xaml.cs - for CO2 Laser Engraver
 *
 * v2.0 / 2019-09-24 / Io Engineering (Terje Io)
 *
 */

/*

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
using CNC.Core;

namespace CNC.Controls
{
    public partial class About : Window
    {
        private Window parent;
        public About(Window parent)
        {
            this.parent = parent;

            InitializeComponent();
        }

        private void About_Load(object sender, System.EventArgs e)
        {
            Left = parent.Left + (parent.Width - Width) / 2;
            Top = parent.Top + 100;
            Title = "About " + parent.Title + "...";

            txtGrblVersion.Content = GrblInfo.Version;
            txtGrblOptions.Content = GrblInfo.Options;
            txtGrblNewOpts.Content = GrblInfo.NewOptions;

            if (GrblInfo.Identity != "")
                grpGrbl.Name += ": " + GrblInfo.Identity;
        }

        private void okButton_Click(object sender, System.EventArgs e)
        {
            Close();
        }
    }
}

