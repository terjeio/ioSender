/*
 * GcodeListControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.44 / 2023-10-07 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2023, Io Engineering (Terje Io)
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

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for GCodeListControl.xaml
    /// </summary>
    public partial class GCodeListControl : UserControl
    {
        public ScrollViewer scroll = null;
 
        public GCodeListControl()
        {
            InitializeComponent();

            ctxMenu.DataContext = this;
        }

        #region Dependency properties

        public static readonly DependencyProperty SingleSelectedProperty = DependencyProperty.Register(nameof(SingleSelected), typeof(bool), typeof(GCodeListControl), new PropertyMetadata(false));
        public bool SingleSelected
        {
            get { return (bool)GetValue(SingleSelectedProperty); }
            private set { SetValue(SingleSelectedProperty, value); }
        }

        public static readonly DependencyProperty MultipleSelectedProperty = DependencyProperty.Register(nameof(MultipleSelected), typeof(bool), typeof(GCodeListControl), new PropertyMetadata(false));
        public bool MultipleSelected
        {
            get { return (bool)GetValue(MultipleSelectedProperty); }
            private set { SetValue(MultipleSelectedProperty, value); }
        }
        #endregion

        private void grdGCode_Drag(object sender, DragEventArgs e)
        {
            GCode.File.Drag(sender, e);
        }

        private void grdGCode_Drop(object sender, DragEventArgs e)
        {
            GCode.File.Drop(sender, e);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            scroll = UIUtils.GetScrollViewer(grdGCode);
            grdGCode.DataContext = GCode.File.Data.DefaultView;
            if (DataContext is GrblViewModel)
                (DataContext as GrblViewModel).PropertyChanged += GCodeListControl_PropertyChanged;
        }

        private void GCodeListControl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.ScrollPosition):
                    int sp = ((GrblViewModel)sender).ScrollPosition;
                    if (sp == 0)
                        scroll.ScrollToTop();
                    else
                        scroll.ScrollToVerticalOffset(sp);
                    break;
            }
        }

        void grdGCode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SingleSelected = grdGCode.SelectedItems.Count == 1 && (DataContext as GrblViewModel).StartFromBlock.CanExecute(grdGCode.SelectedIndex);
            MultipleSelected = grdGCode.SelectedItems.Count >= 0 && (DataContext as GrblViewModel).StartFromBlock.CanExecute(grdGCode.SelectedIndex);
        }

        private void StartHere_Click(object sender, RoutedEventArgs e)
        {
            if (grdGCode.SelectedItems.Count == 1 &&
                 MessageBox.Show(string.Format(LibStrings.FindResource("VerifyStartFrom"), ((DataRowView)(grdGCode.SelectedItems[0])).Row["LineNum"]),
                                  "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                (DataContext as GrblViewModel).StartFromBlock.Execute(grdGCode.SelectedIndex);
            }
        }

        private void CopyMDI_Click(object sender, RoutedEventArgs e)
        {
            if (grdGCode.SelectedItems.Count == 1)
                (DataContext as GrblViewModel).MDIText = (string)((DataRowView)(grdGCode.SelectedItems[0])).Row["Data"];
        }

        private void SendController_Click(object sender, RoutedEventArgs e)
        {
            if (grdGCode.SelectedItems.Count >= 1 &&
                 MessageBox.Show(LibStrings.FindResource("VerifySendController"), "ioSender",
                                  MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                var model = DataContext as GrblViewModel;

                if (model.GrblError != 0)
                    model.ExecuteCommand("");

                List<DataRow> rows = new List<DataRow>();

                for (int i = 0; i < grdGCode.SelectedItems.Count; i++)
                    rows.Add(((DataRowView)(grdGCode.SelectedItems[i])).Row);

                rows.Sort(new RowComparer());

                foreach (DataRow row in rows)
                    model.ExecuteCommand((string)row["Data"]);
            }
        }
    }

    internal class RowComparer : IComparer<DataRow>
    {
        public int Compare(DataRow a, DataRow b)
        {
            return (int)a["LineNum"] - (int)b["LineNum"];
        }
    }
}
