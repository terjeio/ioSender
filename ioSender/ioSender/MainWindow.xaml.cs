/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.40 / 2022-06-12 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2022, Io Engineering (Terje Io)
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
using CNC.Converters;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
#if ADD_CAMERA
using CNC.Controls.Camera;
#endif

namespace GCode_Sender
{

    public partial class MainWindow : Window
    {
        private const string version = "2.0.40";
        public static MainWindow ui = null;
        public static CNC.Controls.Viewer.Viewer GCodeViewer = null;
        public static UIViewModel UIViewModel { get; } = new UIViewModel();

        private bool saveWinSize = false;

        public MainWindow()
        {
            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;

            InitializeComponent();

            ui = this;
//            GCodeViewer = viewer;
            Title = string.Format(Title, version);

            int res;
            if ((res = AppConfig.Settings.SetupAndOpen(Title, (GrblViewModel)DataContext, App.Current.Dispatcher)) != 0)
                Environment.Exit(res);

            BaseWindowTitle = Title;

            CNC.Core.Grbl.GrblViewModel = (GrblViewModel)DataContext;
            GrblInfo.LatheModeEnabled = AppConfig.Settings.Lathe.IsEnabled;

            //       SDCardControl.FileSelected += new CNC_Controls.SDCardControl.FileSelectedHandler(SDCardControl_FileSelected);

            new PipeServer(App.Current.Dispatcher);
            PipeServer.FileTransfer += Pipe_FileTransfer;
            AppConfig.Settings.Base.PropertyChanged += Base_PropertyChanged;
        }

        public string BaseWindowTitle { get; set; }

        public string WindowTitle
        {
            set
            {
                ui.Title = BaseWindowTitle + (string.IsNullOrEmpty(value) ? "" : " - " + value);
                ui.menuCloseFile.IsEnabled = ui.menuSaveFile.IsEnabled = !(string.IsNullOrEmpty(value) || value.StartsWith("SDCard:"));
                ui.menuTransform.IsEnabled = ui.menuCloseFile.IsEnabled && UIViewModel.TransformMenuItems.Count > 0;
            }
        }

        public bool JobRunning
        {
            get { return menuFile.IsEnabled != true; }
            set {
       //         menuFile.IsEnabled = xx.IsEnabled = !value;
                foreach (TabItem tabitem in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
                {
                    var view = getView(tabitem);
                    if (view != null)
                        tabitem.IsEnabled = (!value && view.CanEnable) || tabitem == ui.tabMode.SelectedItem;
                }
            }
        }

        #region UIEvents

        private void Window_Load(object sender, EventArgs e)
        {
            if (AppConfig.Settings.Base.KeepWindowSize)
            {
                if (AppConfig.Settings.Base.WindowWidth == -1)
                    WindowState = WindowState.Maximized;
                else
                {
                    Width = Math.Max(Math.Min(AppConfig.Settings.Base.WindowWidth, SystemParameters.PrimaryScreenWidth), MinWidth);
                    Height = Math.Max(Math.Min(AppConfig.Settings.Base.WindowHeight, SystemParameters.PrimaryScreenHeight), MinHeight);
                    if (Left + Width > SystemParameters.PrimaryScreenWidth)
                        Left = 0d;
                    if (Top + Height > SystemParameters.PrimaryScreenHeight)
                        Top = 0d;
                }
            }
            saveWinSize = AppConfig.Settings.Base.KeepWindowSize;
            var appconf = getView(getTab(ViewType.AppConfig));

            appconf.Setup(UIViewModel, AppConfig.Settings);

            foreach (TabItem tab in UIUtils.FindLogicalChildren<TabItem>(ui.tabMode))
            {
                ICNCView view = getView(tab);
                if (view != null && view != appconf)
                {
                    view.Setup(UIViewModel, AppConfig.Settings);
                    tab.IsEnabled = view.ViewType == ViewType.GRBL || view.ViewType == ViewType.AppConfig;
                }
            }
#if ADD_CAMERA
            enableCamera(this);
#else
            menuCamera.Visibility = Visibility.Hidden;
#endif
            if (!AppConfig.Settings.GCodeViewer.IsEnabled)
                ShowView(false, ViewType.GCodeViewer);

            UIViewModel.ConfigControls.Add(new CNC.Controls.Viewer.ConfigControl());

            xx.ItemsSource = UIViewModel.SidebarItems;
            if(AppConfig.Settings.Jog.Mode != JogConfig.JogMode.Keypad)
                UIViewModel.SidebarItems.Add(new SidebarItem(jogControl));
            UIViewModel.SidebarItems.Add(new SidebarItem(macroControl));
            UIViewModel.SidebarItems.Add(new SidebarItem(gotoControl));
            UIViewModel.SidebarItems.Add(new SidebarItem(outlineFlyout));
            UIViewModel.SidebarItems.Add(new SidebarItem(mposFlyout));
//          UIViewModel.SidebarItems.Add(new SidebarItem(thcControl));

            UIViewModel.CurrentView = getView((TabItem)tabMode.Items[tabMode.SelectedIndex = 0]);
            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();
            UIViewModel.CurrentView.Activate(true, ViewType.Startup);

            if (!string.IsNullOrEmpty(AppConfig.Settings.FileName))
            {
                // Delay loading until app is ready
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() =>
                {
                    GCode.File.Load(AppConfig.Settings.FileName);
                }));
            }

            IGCodeConverter c = new Excellon2GCode();
            GCode.File.AddConverter(c.GetType(), c.FileType);
            c = new HpglToGCode();
            GCode.File.AddConverter(c.GetType(), c.FileType);

            GCode.File.AddTransformer(typeof(GCodeRotateViewModel), (string)FindResource("MenuRotate"), UIViewModel.TransformMenuItems);
            GCode.File.AddTransformer(typeof(ArcsToLines), (string)FindResource("MenuArcsToLines"), UIViewModel.TransformMenuItems);
            GCode.File.AddTransformer(typeof(GCodeCompress), (string)FindResource("MenuCompress"), UIViewModel.TransformMenuItems);
            GCode.File.AddTransformer(typeof(CNC.Controls.DragKnife.DragKnifeViewModel), (string)FindResource("MenuDragKnife"), UIViewModel.TransformMenuItems);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(saveWinSize && !(AppConfig.Settings.Base.WindowWidth == e.NewSize.Width && AppConfig.Settings.Base.WindowHeight == e.NewSize.Height))
            {
                AppConfig.Settings.Base.WindowWidth = WindowState == WindowState.Maximized ? -1 : e.NewSize.Width;
                AppConfig.Settings.Base.WindowHeight = WindowState == WindowState.Maximized ? -1 : e.NewSize.Height;
                AppConfig.Settings.Save();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!(e.Cancel = !menuFile.IsEnabled))
            {
                UIViewModel.CurrentView.Activate(false, ViewType.Shutdown);

                if (UIViewModel.Console != null)
                    UIViewModel.Console.Close();
#if ADD_CAMERA
                if (UIViewModel.Camera != null)
                {
                    UIViewModel.Camera.CloseCamera();
                    UIViewModel.Camera.Close();
                }
#endif
                Comms.com.DataReceived -= (DataContext as GrblViewModel).DataReceived;

                using (new UIUtils.WaitCursor())
                {
                    Comms.com.Close(); // disconnecting from websocket may take some time...
                    AppConfig.Settings.Shutdown();
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

        void aboutWikiItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/terjeio/ioSender/wiki");
        }

        void tipsWikiItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/terjeio/ioSender/wiki/Usage-tips");
        }

        void briefTour_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.grbl.org/single-post/one-sender-to-rule-them-all");
        }

        void videoTutorials_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://youtube.com/playlist?list=PLnSV6o2cRxM5mQQe4ec5cS2J8jBsEciY3");
        }

        void errorAndAlarms_Click(object sender, EventArgs e)
        {
            new ErrorsAndAlarms(BaseWindowTitle) { Owner = Application.Current.MainWindow }.Show();
        }

        void aboutMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About(BaseWindowTitle) { Owner = Application.Current.MainWindow };
            about.DataContext = DataContext;
            about.ShowDialog();
        }

        private void Base_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(Config.KeepWindowSize))
            {
                if((sender as Config).KeepWindowSize)
                {
                    AppConfig.Settings.Base.WindowWidth = Width;
                    AppConfig.Settings.Base.WindowHeight = Height;
                }
            }
        }

        private void Pipe_FileTransfer(string filename)
        {
            if(!JobRunning)
                GCode.File.Load(filename);
        }

        private void fileSaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Save();
        }

        private void fileOpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Open();
        }

        private void fileCloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            GCode.File.Close();
        }

        private void TabMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(DataContext as GrblViewModel).IsReady)
                return;

            if (Equals(e.OriginalSource, sender) && UIViewModel.CurrentView != null && e.AddedItems.Count == 1)
            {
                ICNCView prevView = UIViewModel.CurrentView, nextView = getView((TabItem)e.AddedItems[0]);
                if (nextView != null && nextView != UIViewModel.CurrentView)
                {
                    UIViewModel.CurrentView = nextView;
                    prevView.Activate(false, nextView.ViewType);
                    nextView.Activate(true, prevView.ViewType);
                }
            }
        }

        private void SDCardView_FileSelected(string filename, bool rewind)
        {
            if((ui.DataContext as GrblViewModel).FileName != filename.Substring(filename.IndexOf(':') + 1))
                GCode.File.Close();
            (ui.DataContext as GrblViewModel).FileName = filename;
            (ui.DataContext as GrblViewModel).SDRewind = rewind;
            Dispatcher.BeginInvoke((System.Action)(() => ui.tabMode.SelectedItem = getTab(ViewType.GRBL)));
        }

        #endregion

        public static void CloseFile ()
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
                var view = getView(tabitem);
                if (view != null && view.ViewType == mode)
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

        private void openConsoleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (UIViewModel.Console == null)
            {
                UIViewModel.Console = new ConsoleWindow();
                UIViewModel.Console.DataContext = DataContext;
                UIViewModel.Console.Show();
            }
            else
            {
                if (UIViewModel.Console.IsVisible)
                    UIViewModel.Console.Visibility = Visibility.Hidden;
                else
                    UIViewModel.Console.Show();
            }
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
