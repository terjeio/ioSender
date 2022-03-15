/*
 * GCodeWrapDialog.cs - part of CNC Controls library for Grbl
 *
 * v0.37 / 2022-02-22 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2022, Io Engineering (Terje Io)
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
using System.Collections.ObjectModel;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for GCode_WrapDialog.xaml
    /// </summary>
    public partial class GCodeWrapDialog : Window
    {
        public GCodeWrapDialog(GCodeWrapViewModel model)
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

    public class WrapAxis : ViewModelBase
    {
        public WrapAxis(int axisid, string name)
        {
            AxisId = axisid;
            Name = name;
        }

        public int AxisId { get; private set; }
        public string Name { get; private set; }
    }

    public class GCodeWrapViewModel : ViewModelBase, IGCodeTransformer
    {
        public GCodeWrapViewModel ()
        {
            SourceAxes.Add(new WrapAxis(GrblConstants.X_AXIS, "X-axis"));
            SourceAxes.Add(new WrapAxis(GrblConstants.Y_AXIS, "Y-axis"));
            if(GrblInfo.NumAxes > 3)
                TargetAxes.Add(new WrapAxis(GrblConstants.A_AXIS, "A-axis"));
            if (GrblInfo.NumAxes > 4)
                TargetAxes.Add(new WrapAxis(GrblConstants.B_AXIS, "B-axis"));
        }

        private bool _Z0atCenter = false;
        private double _Diameter = 50d;
        private int _sourceAxis = GrblConstants.Y_AXIS, targetAxis = GrblConstants.A_AXIS;

        public bool Z0atCenter { get { return _Z0atCenter; } set { _Z0atCenter = value; OnPropertyChanged(); } }
        public double Diameter { get { return _Diameter; } set { _Diameter = value; OnPropertyChanged(); } }
        public int SourceAxis { get { return _sourceAxis; } set { _sourceAxis = value; OnPropertyChanged(); } }
        public ObservableCollection<WrapAxis> SourceAxes { get; private set; } = new ObservableCollection<WrapAxis>();
        public int TargetAxis { get { return targetAxis; } set { targetAxis = value; OnPropertyChanged(); } }
        public ObservableCollection<WrapAxis> TargetAxes { get; private set; } = new ObservableCollection<WrapAxis>();

        public void Apply()
        {
            if (new GCodeWrapDialog(this) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return;

            if (Diameter == 0d)
                return; // Nothing to do...

            using (new UIUtils.WaitCursor())
            {
                try
                {
                    var limits = GCode.File.Model.ProgramLimits;

                    new GCodeWrap().ApplyWrap(this, AppConfig.Settings.Base.AutoCompress);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "GCode Wrap", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }
    }
}
