/*
 * GrblConfigView.xaml.cs - part of CNC Probing library
 *
 * v0.46 / 2025-06-05 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2025, Io Engineering (Terje Io)
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
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for ConfigView.xaml
    /// </summary>
    public partial class GrblConfigView : UserControl, ICNCView
    {
        public GrblConfigView()
        {
            InitializeComponent();
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.GRBLConfig; } }
        public bool CanEnable { get { return DataContext is GrblViewModel ? (DataContext as GrblViewModel).SystemCommandsAllowed : true; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            getView(tabConfig.SelectedItem == null ? tabConfig.Items[0] as TabItem : tabConfig.SelectedItem as TabItem)?.Activate(activate);
        }

        public void CloseFile()
        {

            //if (!string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
            //    MainWindow.EnableView(true, ViewType.TrinamicTuner);
            //else
            //    MainWindow.ShowView(false, ViewType.TrinamicTuner);


            //if (GrblInfo.HasPIDLog)
            //    MainWindow.EnableView(true, ViewType.PIDTuner);
            //else
            //    MainWindow.ShowView(false, ViewType.PIDTuner);

        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        private  TabItem getTab(GrblConfigType tabtype)
        {
            TabItem tab = null;

            foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(tabConfig))
            {
                var view = getView(tabitem);
                if (view != null && view.GrblConfigType == tabtype)
                {
                    tab = tabitem;
                    break;
                }
            }

            return tab;
        }

        private static IGrblConfigTab getView(TabItem tab)
        {
            IGrblConfigTab view = null;

            foreach (UserControl uc in UIUtils.FindLogicalChildren<UserControl>(tab))
            {
                if (uc is IGrblConfigTab)
                {
                    view = (IGrblConfigTab)uc;
                    break;
                }
            }

            return view;
        }

        private void tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Equals(e.OriginalSource, sender))
            {
                if (e.AddedItems.Count == 1)
                {
                    if (e.RemovedItems.Count == 1)
                        getView(e.RemovedItems[0] as TabItem).Activate(false);

                    getView(e.AddedItems[0] as TabItem).Activate(true);
                }
                e.Handled = true;
            }
        }

        private void RemoveTab (GrblConfigType type)
        {
            var ptab = getTab(type);
            if (ptab != null)
                tabConfig.Items.Remove(ptab);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
                RemoveTab(GrblConfigType.Trinamic);

            if (!GrblInfo.HasPIDLog)
                RemoveTab(GrblConfigType.PidTuning);
        }
    }
}
