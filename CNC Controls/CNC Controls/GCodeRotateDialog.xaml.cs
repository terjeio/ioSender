/*
 * GCodeRotateDialog.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.38 / 2022-05-08 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021-2022, Io Engineering (Terje Io)
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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for GCodeRotateControl.xaml
    /// </summary>
    public partial class GCodeRotateDialog : Window
    {
        public GCodeRotateDialog(GCodeRotateViewModel model)
        {
            InitializeComponent();

            DataContext = model;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var parent = Application.Current.MainWindow;

            Left = parent.Left + (parent.Width - Width) / 2d;
            Top = parent.Top + (parent.Height - Height) / 2d;

            (sender as Window).Dispatcher.Invoke(new System.Action(() =>
                {
                    (sender as Window).MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                }), DispatcherPriority.ContextIdle);
        }

        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class GCodeRotateViewModel : ViewModelBase, IGCodeTransformer
    {
        private double _Angle = 0d;
        private OriginControl.Origin _origin = OriginControl.Origin.None;

        public double Angle { get { return _Angle; } set { _Angle = value; OnPropertyChanged(); } }
        public OriginControl.Origin Origin { get { return _origin; } set { _origin = value; OnPropertyChanged(); } }

        public void Apply()
        {
            if (new GCodeRotateDialog(this) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return;

            if (Angle == 0d)
                return; // Nothing to do...

            using (new UIUtils.WaitCursor())
            {
                try
                {
                    RP.Math.Vector3 offset;

                    var limits = GCode.File.Model.ProgramLimits;

                    switch (_origin)
                    {
                        case OriginControl.Origin.A:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.B:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.C:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MaxY, 0d);
                            break;

                        case OriginControl.Origin.D:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MaxY, 0d);
                            break;

                        case OriginControl.Origin.Center:
                            offset = new RP.Math.Vector3(limits.MinX + limits.SizeX / 2d, limits.MinY + limits.SizeY / 2d, 0d);
                            break;

                        case OriginControl.Origin.AB:
                            offset = new RP.Math.Vector3(limits.MinX + limits.SizeX / 2d, limits.MinY, 0d);
                            break;

                        case OriginControl.Origin.AD:
                            offset = new RP.Math.Vector3(limits.MinX, limits.MinY + limits.SizeY / 2d, 0d);
                            break;

                        case OriginControl.Origin.CB:
                            offset = new RP.Math.Vector3(limits.MaxX, limits.MinY + limits.SizeY / 2d, 0d);
                            break;

                        case OriginControl.Origin.CD:
                            offset = new RP.Math.Vector3(limits.MinX + limits.SizeX / 2d, limits.MaxY, 0d);
                            break;

                        default: // Origin.None -> 0,0
                            offset = new RP.Math.Vector3();
                            break;
                    }

                    new GCodeRotate().ApplyRotation(Angle * Math.PI / 180d, offset, AppConfig.Settings.Base.AutoCompress);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "GCode Rotate", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }
    }
}
