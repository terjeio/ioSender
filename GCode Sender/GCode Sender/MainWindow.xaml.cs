/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.03 / 2019-12-01 / Io Engineering (Terje Io)
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

using System;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using CNC.Controls;
using CNC.View;
#if ADD_CAMERA
using CNC.Controls.Camera;
#endif

namespace GCode_Sender
{

    public partial class MainWindow : Window
    {
        private CNCView currentRenderer = null;

        public static MainWindow ui = null;
        public static CNC.Controls.Viewer.Viewer GCodeViewer = null;
        public static AppConfig Profile = new AppConfig();
#if ADD_CAMERA
        public static Camera Camera = null;
#endif
        public delegate void GCodePushHandler(string gcode, CNC.Core.Action action);
        public static event GCodePushHandler GCodePush;

        public delegate void FileOpenHandler();
        public static event FileOpenHandler FileOpen;

        public MainWindow()
        {
            InitializeComponent();

            ui = this;
            GCodeViewer = viewer;

            int res;
            if((res = Profile.SetupAndOpen(Title, App.Current.Dispatcher)) != 0)
                Environment.Exit(res);

            BaseWindowTitle = Title;

            GrblInfo.LatheModeEnabled = Profile.Config.LatheMode;

            turningWizard.ApplySettings(Profile.Config.Lathe);
            threadingWizard.ApplySettings(Profile.Config.Lathe);

            if (Profile.Config.EnableGCodeViewer)
                GCodeViewer.ApplySettings(Profile.Config.GCodeViewer);
            else
                ShowView(false, ViewType.GCodeViewer);

#if ADD_CAMERA
            enableCamera(this);
#else
            menuCamera.Visibility = Visibility.Hidden;
#endif

            turningWizard.GCodePush += wizard_GCodePush;
            threadingWizard.GCodePush += wizard_GCodePush;
            //facingWizard.GCodePush += wizard_GCodePush;
            //SDCardControl.FileSelected += new CNC_Controls.SDCardControl.FileSelectedHandler(SDCardControl_FileSelected);

            tabMode.SelectedIndex = 0;

            foreach (TabItem tab in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
                tab.IsEnabled = GetRenderer(tab).mode == ViewType.GRBL;

            currentRenderer = GetRenderer((TabItem)tabMode.Items[tabMode.SelectedIndex]);
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
                menuFile.IsEnabled = !value;
                foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
                    tabitem.IsEnabled = !value || GetRenderer(tabitem).mode == ViewType.GRBL;
            }
        }

        void wizard_GCodePush(string gcode, CNC.Core.Action action)
        {
            GCodePush?.Invoke(gcode, action);
        }

        #region UIEvents

        private void Window_Load(object sender, EventArgs e)
        {
            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();
            currentRenderer.Activate(true, ViewType.Startup);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!(e.Cancel = !menuFile.IsEnabled))
            {
                currentRenderer.Activate(false, ViewType.Shutdown);
#if ADD_CAMERA
                if (Camera != null)
                {
                    Camera.CloseCamera();
                    Camera.Close();
                }

#endif
                using (new UIUtils.WaitCursor()) // disconnecting from websocket may take some time...
                {
                    if (Comms.com.StreamType != Comms.StreamType.Serial) // Serial makes fking process hang
                        Comms.com.Close();
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
  //          Comms.com.Close(); // Makes fking process hang
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void aboutMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About(this, BaseWindowTitle);
            about.ShowDialog();
        }

        private void fileOpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FileOpen?.Invoke();
        }

        private void fileCloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CNCView view, grbl = GetRenderer(getTab(ViewType.GRBL));
            
            grbl.CloseFile();

            foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                if ((view = GetRenderer(tabitem)) != null && view != grbl)
                    view.CloseFile();
            }
        }

        private void TabMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(currentRenderer != null && e.AddedItems.Count > 0)
            {
                ViewType prevMode = currentRenderer.mode;
                CNCView nextRenderer = GetRenderer((TabItem)tabMode.Items[tabMode.SelectedIndex]);
                if (nextRenderer != currentRenderer)
                {
                    currentRenderer.Activate(false, nextRenderer.mode);
                    currentRenderer = nextRenderer;
                    currentRenderer.Activate(true, prevMode);
                }
            }
        }

        #endregion  

        private static TabItem getTab(ViewType mode)
        {
            TabItem tab = null;

            foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                if (GetRenderer(tabitem).mode == mode)
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
            if (Camera == null)
            {
                Camera = new Camera();
                Camera.ApplySettings(Profile.Config.Camera);
                //        Camera.Owner = owner;
                owner.menuCamera.IsEnabled = Camera.HasCamera;
            }

            return Camera != null;
        }

        private void CameraOpen_Click(object sender, RoutedEventArgs e)
        {
            Camera.Open();
        }
#else
        private void CameraOpen_Click(object sender, RoutedEventArgs e)
        {
        }
#endif

        private static CNCView GetRenderer(TabItem tab)
        {
            CNCView renderer = null;

            foreach (UserControl uc in UIUtils.FindLogicalChildren<UserControl>(tab))
            {
                if (uc is CNCView) { renderer = (CNCView)uc; break; }
            }

            return renderer;
        }
    }
}

