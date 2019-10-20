/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.02 / 2019-10-02 / Io Engineering (Terje Io)
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
using System.Xml;
using CNC.Core;
using CNC.Controls;
using CNC.View;
using System.Globalization;
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
#if ADD_CAMERA
        public static Camera Camera = null;
#endif
        public delegate void GCodePushHandler(string gcode, CNC.Core.Action action);
        public static event GCodePushHandler GCodePush;

        public MainWindow()
        {
            InitializeComponent();

            MainWindow.ui = this;
            MainWindow.GCodeViewer = viewer;

            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;

            string PortParams = "";

            string[] args = Environment.GetCommandLineArgs();

            int p = 0;
            while (p < args.GetLength(0)) switch (args[p++])
            {
                case "-inifile":
                    CNC.Core.Resources.IniName = GetArg(args, p++);
                    break;

                case "-configmapping":
                    CNC.Core.Resources.ConfigName = GetArg(args, p++);
                    break;

                case "-language":
                    CNC.Core.Resources.Language = GetArg(args, p++);
                    break;
            }

            try
            {
                XmlDocument config = new XmlDocument();

                config.Load(CNC.Core.Resources.IniName);

                foreach (XmlNode N in config.SelectNodes("Config/*"))
                {
                    switch (N.Name)
                    {
                        case "PortParams":
                            PortParams = N.InnerText;
                            break;
#if ADD_CAMERA
                        case "CameraXOffset":
                            if (enableCamera(this))
                                Camera.CameraControl.XOffset = dbl.Parse(N.InnerText);
                            break;

                        case "CameraYOffset":
                            if (enableCamera(this))
                                Camera.CameraControl.YOffset = dbl.Parse(N.InnerText);
                            break;

                        case "CameraMoveMode":
                            if (enableCamera(this))
                                Camera.CameraControl.Mode = (CameraControl.MoveMode)Enum.Parse(typeof(CameraControl.MoveMode), N.InnerText);
                            break;
#endif
                        case "LatheMode":
                            if (N.InnerText.ToLower() == "true")
                                GrblInfo.LatheModeEnabled = true;
                            break;
                    }
                }

                foreach (XmlNode N in config.SelectNodes("Config/GCodeViewer/*"))
                {
                    string value = N.InnerText.Trim();
                    switch (N.Name)
                    {
                        case "ArcResolution":
                            GCodeViewer.ArcResolution = int.Parse(value);
                            break;

                        case "MinDistance":
                            GCodeViewer.MinDistance = dbl.Parse(value);
                            break;

                        case "ShowGrid":
                            GCodeViewer.ShowGrid = bool.Parse(value);
                            break;

                        case "ShowAxes":
                            GCodeViewer.ShowGrid = bool.Parse(value);
                            break;

                        case "ShowBoundingBox":
                            GCodeViewer.ShowGrid = bool.Parse(value);
                            break;

                        case "ShowViewCube":
                            GCodeViewer.ShowGrid = bool.Parse(value);
                            break;
                    }
                }
            }
            catch
            {
                MessageBox.Show("Config file not found or invalid.", this.Title);
                System.Environment.Exit(1);
            }

#if ADD_CAMERA
#else
            menuCamera.Visibility = Visibility.Hidden;
#endif

#if DEBUG
            PortParams = "com21:115200,N,8,1,P";
            //PortParams = "10.0.0.75:23";
#endif

            if (Char.IsDigit(PortParams[0])) // We have an IP address
            {
                new IPComms(PortParams);
            }
            else
            {
                new SerialComms(PortParams, Comms.ResetMode.None, App.Current.Dispatcher);
            }

            if (!Comms.com.IsOpen)
            {
                // this.com = null;
                // this.disableUI();
                MessageBox.Show("Unable to open connection!", this.Title);
                System.Environment.Exit(2);
            }

            //            this.SDCardControl.FileSelected += new CNC_Controls.SDCardControl.FileSelectedHandler(SDCardControl_FileSelected);

            System.Threading.Thread.Sleep(400);

            if (!(Comms.com.Reply == "" || Comms.com.Reply.StartsWith("Grbl")))
            {
                MPGPending await = new MPGPending();
                await.ShowDialog();
                if (await.Cancelled)
                {
                    Comms.com.Close();
                    System.Environment.Exit(2);
                }
               // await.re;
            }

      //      turningWizard.GCodePush += wizard_GCodePush;
            threadingWizard.GCodePush += wizard_GCodePush;
            //            facingWizard.GCodePush += wizard_GCodePush;


            this.tabMode.SelectedIndex = 0;

            foreach (TabItem tab in CNC.Controls.UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                tab.IsEnabled = GetRenderer(tab).mode == ViewType.GRBL;
            }

            currentRenderer = GetRenderer((TabItem)tabMode.Items[tabMode.SelectedIndex]);
        }

        public string WindowTitle
        {
            set
            {
                ui.Title = "GCode Sender" + (value == "" ? "" : " - " + value);
                ui.menuCloseFile.IsEnabled = !(value == null || value.StartsWith("SDCard:"));
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
            }
        }

        void wizard_GCodePush(string gcode, CNC.Core.Action action)
        {
            GCodePush?.Invoke(gcode, action);
        }


        private void Window_Closed(object sender, EventArgs e)
        {
  //          Comms.com.Close(); // Makes fking process hang
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
        void exitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        void aboutMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About(this);
            about.ShowDialog();
        }

        private string GetArg(string[] args, int i)
        {
            return i < args.GetLength(0) ? args[i] : null;
        }

        private static TabItem getTab(ViewType mode)
        {
            TabItem tab = null;

            foreach (TabItem tabitem in CNC.Controls.UIUtils.FindLogicalChildren<TabItem>(MainWindow.ui.tabMode))
            {
                if (GetRenderer(tabitem).mode == mode)
                {
                    tab = tabitem;
                    break;
                }
            }

            return tab;
        }

        public static bool enableControl(bool enable, ViewType control)
        {
            TabItem tab = getTab(control);
            if (tab != null)
                tab.IsEnabled = enable;

            return tab != null && enable;
        }

        public static void showControl(bool show, ViewType control)
        {
            TabItem tab = getTab(control);
            if (tab != null && !show)
                ui.tabMode.Items.Remove(tab);
        }
#if ADD_CAMERA
        private static bool enableCamera(MainWindow owner)
        {
            if (Camera == null)
            {
                Camera = new Camera();
        //        Camera.Owner = owner;
                owner.menuCamera.IsEnabled = true;
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

