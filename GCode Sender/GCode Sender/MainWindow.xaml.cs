/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.11 / 2020-03-09 / Io Engineering (Terje Io)
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

using System;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using CNC.Controls;
#if ADD_CAMERA
using CNC.Controls.Camera;
#endif

namespace GCode_Sender
{

    public partial class MainWindow : Window
    {
        public static MainWindow ui = null;
        public static CNC.Controls.Viewer.Viewer GCodeViewer = null;

        public delegate void GCodePushHandler(string gcode, CNC.Core.Action action);
        public static event GCodePushHandler GCodePush;

        public delegate void FileOpenHandler();
        public static event FileOpenHandler FileOpen; // Issued if File > Open menu clicked

        public delegate void FileLoadHandler(string filename);
        public static event FileLoadHandler FileLoad; // Issued on load of main window if filename provided as command line argument

        static public UIViewModel UIViewModel { get; } = new UIViewModel();

        public MainWindow()
        {
            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;

            InitializeComponent();

            ui = this;
            GCodeViewer = viewer;

            int res;
            if ((res = UIViewModel.Profile.SetupAndOpen(Title, (GrblViewModel)DataContext, App.Current.Dispatcher)) != 0)
                Environment.Exit(res);

            macroControl.Macros = UIViewModel.Profile.Config.Macros;
            macroControl.MacrosChanged += MacroControl_MacrosChanged;

            BaseWindowTitle = Title;

            CNC.Core.Grbl.GrblViewModel = (GrblViewModel)DataContext;
            GrblInfo.LatheModeEnabled = UIViewModel.Profile.Config.Lathe.IsEnabled;

#if ADD_CAMERA
            enableCamera(this);
#else
            menuCamera.Visibility = Visibility.Hidden;
#endif

            turningWizard.GCodePush += wizard_GCodePush;
            threadingWizard.GCodePush += wizard_GCodePush;
      //      facingWizard.GCodePush += wizard_GCodePush;
     //       SDCardControl.FileSelected += new CNC_Controls.SDCardControl.FileSelectedHandler(SDCardControl_FileSelected);


            new PipeServer(App.Current.Dispatcher);
            PipeServer.FileTransfer += Pipe_FileTransfer;
        }

        private void MacroControl_MacrosChanged()
        {
            UIViewModel.Profile.Save();
        }

        public string BaseWindowTitle { get; set; }

        public string WindowTitle
        {
            set
            {
                ui.Title = BaseWindowTitle + (string.IsNullOrEmpty(value) ? "" : " - " + value);
                ui.menuCloseFile.IsEnabled = !(string.IsNullOrEmpty(value) || value.StartsWith("SDCard:"));
            }
        }

        public bool JobRunning
        {
            get { return menuFile.IsEnabled != true; }
            set {
                menuFile.IsEnabled = xx.IsEnabled = !value;
                foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
                    tabitem.IsEnabled = !value || getView(tabitem).ViewType == ViewType.GRBL;
            }
        }

        void wizard_GCodePush(string gcode, CNC.Core.Action action)
        {
            GCodePush?.Invoke(gcode, action);
        }

        #region UIEvents

        private void Window_Load(object sender, EventArgs e)
        {
            foreach (TabItem tab in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                ICNCView view = getView(tab);
                view.Setup(UIViewModel, UIViewModel.Profile);
                tab.IsEnabled = view.ViewType == ViewType.GRBL || view.ViewType == ViewType.AppConfig;
            }

            if (!UIViewModel.Profile.Config.GCodeViewer.IsEnabled)
                ShowView(false, ViewType.GCodeViewer);

            xx.ItemsSource = UIViewModel.SidebarItems;
            UIViewModel.SidebarItems.Add(new SidebarItem("Jog", jogControl));
            UIViewModel.SidebarItems.Add(new SidebarItem("Macros", macroControl));
            UIViewModel.SidebarItems.Add(new SidebarItem("Goto", gotoControl));

            UIViewModel.CurrentView = getView((TabItem)tabMode.Items[tabMode.SelectedIndex = 0]);
            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();
            UIViewModel.CurrentView.Activate(true, ViewType.Startup);

            if (!string.IsNullOrEmpty(UIViewModel.Profile.FileName))
                FileLoad?.Invoke(UIViewModel.Profile.FileName);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!(e.Cancel = !menuFile.IsEnabled))
            {
                UIViewModel.CurrentView.Activate(false, ViewType.Shutdown);
#if ADD_CAMERA
                if (UIViewModel.Camera != null)
                {
                    UIViewModel.Camera.CloseCamera();
                    UIViewModel.Camera.Close();
                }
#endif
                Comms.com.DataReceived -= (DataContext as GrblViewModel).DataReceived;

                using (new UIUtils.WaitCursor()) // disconnecting from websocket may take some time...
                {
                     Comms.com.Close();
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //Comms.com.Close(); // Makes fking process hang
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void aboutMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About(BaseWindowTitle) { Owner = Application.Current.MainWindow };
            about.DataContext = DataContext;
            about.ShowDialog();
        }

        private void Pipe_FileTransfer(string filename)
        {
            FileLoad?.Invoke(filename);
        }

        private void fileOpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FileOpen?.Invoke();
        }

        private void fileCloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            closeFile();
        }

        private void TabMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UIViewModel.CurrentView != null && e.AddedItems.Count > 0)
            {
                ViewType prevMode = UIViewModel.CurrentView.ViewType;
                ICNCView nextView = getView((TabItem)tabMode.Items[tabMode.SelectedIndex]);
                if (nextView != UIViewModel.CurrentView)
                {
                    UIViewModel.CurrentView.Activate(false, nextView.ViewType);
                    UIViewModel.CurrentView = nextView;
                    UIViewModel.CurrentView.Activate(true, prevMode);
                }
            }
        }

        private void SDCardView_FileSelected(string filename)
        {
            closeFile();
            ((GrblViewModel)ui.DataContext).FileName = filename;
            Dispatcher.BeginInvoke((System.Action)(() => ui.tabMode.SelectedItem = getTab(ViewType.GRBL)));
        }

        #endregion

        private static void closeFile ()
        {
            ICNCView view, grbl = getView(getTab(ViewType.GRBL));

            grbl.CloseFile();

            foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                if ((view = getView(tabitem)) != null && view != grbl)
                    view.CloseFile();
            }
        }

        private static TabItem getTab(ViewType mode)
        {
            TabItem tab = null;

            foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                if (getView(tabitem).ViewType == mode)
                {
                    tab = tabitem;
                    break;
                }
            }

            return tab;
        }

        public static bool EnableView(bool enable, ViewType view)
        {
            TabItem tab = getTab(view);
            if (tab != null)
                tab.IsEnabled = enable;

            return tab != null && enable;
        }

        public static void ShowView(bool show, ViewType view)
        {
            TabItem tab = getTab(view);
            if (tab != null && !show)
                ui.tabMode.Items.Remove(tab);
        }

        public static bool IsViewVisible(ViewType view)
        {
            TabItem tab = getTab(view);

            return tab != null;
        }

#if ADD_CAMERA
        private static bool enableCamera(MainWindow owner)
        {
            if (UIViewModel.Camera == null)
            {
                UIViewModel.Camera = new Camera();
                UIViewModel.Camera.Setup(UIViewModel);
                //        Camera.Owner = owner;
                owner.menuCamera.IsEnabled = UIViewModel.Camera.HasCamera;
            }

            return UIViewModel.Camera != null;
        }

        private void CameraOpen_Click(object sender, RoutedEventArgs e)
        {
            UIViewModel.Camera.Open();
        }
#else
        private void CameraOpen_Click(object sender, RoutedEventArgs e)
        {
        }
#endif

        private static ICNCView getView(TabItem tab)
        {
            ICNCView view = null;

            foreach (UserControl uc in UIUtils.FindLogicalChildren<UserControl>(tab))
            {
                if (uc is ICNCView) {
                    view = (ICNCView)uc;
                    break;
                }
            }

            return view;
        }
    }
}

