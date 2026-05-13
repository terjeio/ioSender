/*
 * FileActionControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.17 / 2020-04-15 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for FileControl.xaml
    /// </summary>
    /// 

    public partial class FileActionControl : UserControl
    {
        private bool fileChanged = false;

        public FileActionControl()
        {
            InitializeComponent();
        }

        void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Open();
        }
        void btnReload_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Load((DataContext as GrblViewModel).FileName);
        }

        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Save();

            if (DataContext is GrblViewModel model)
                model.IsFileEditing = false;
        }
        void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GrblViewModel model)
                model.IsFileEditing = !model.IsFileEditing;
        }


        void btnClose_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Close();
        }

         private void File_Changed(object sender, FileSystemEventArgs e)
        {
            fileChanged = true;
        }
    }
}
