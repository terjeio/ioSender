/*
 * JobView.xaml.cs - part of Grbl Code Sender
 *
 * v0.23 / 2020-08-17 / Io Engineering (Terje Io)
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
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using CNC.Core;
using CNC.Controls;

namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for JobView.xaml
    /// </summary>
    public partial class JobView : UserControl, ICNCView
    {
        private bool? initOK = null;
        private bool sdStream = false;
        private GrblViewModel model;
        private KeypressHandler keyboard = null;
        private IInputElement focusedControl = null;

        public JobView()
        {
            InitializeComponent();

            DRO.DROEnabledChanged += DRO_DROEnabledChanged;
            DataContextChanged += View_DataContextChanged;
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is GrblViewModel)
            {
                model = (GrblViewModel)e.NewValue;
                model.PropertyChanged += OnDataContextPropertyChanged;
                DataContextChanged -= View_DataContextChanged;
                //          model.OnGrblReset += Model_OnGrblReset;
            }
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch(e.PropertyName)
            {
                case nameof(GrblViewModel.GrblState):
                    if (initOK == false && (sender as GrblViewModel).GrblState.State != GrblStates.Alarm)
                        InitSystem();
                    break;

                case nameof(GrblViewModel.IsSleepMode):
                    EnableUI(!(sender as GrblViewModel).IsSleepMode);
                    break;

                case nameof(GrblViewModel.IsJobRunning):
                    MainWindow.ui.JobRunning = (sender as GrblViewModel).IsJobRunning;
                    if(GrblInfo.ManualToolChange)
                        GrblCommand.ToolChange = (sender as GrblViewModel).IsJobRunning ? "T{0}M6" : "M61Q{0}";
                    break;

                case nameof(GrblViewModel.Tool):
                    if (GrblInfo.ManualToolChange && (sender as GrblViewModel).Tool != GrblConstants.NO_TOOL)
                        GrblWorkParameters.RemoveNoTool();
                    break;

                case nameof(GrblViewModel.GrblReset):
                    if ((sender as GrblViewModel).IsReady)
                    {
                        initOK = null;
                        Activate(true, ViewType.GRBL);
                    }
                    break;

                case nameof(GrblViewModel.ParserState):
                    if ((sender as GrblViewModel).GrblReset)
                    {
                        EnableUI(true);
                        (sender as GrblViewModel).GrblReset = false;
                    }
                    break;

                case nameof(GrblViewModel.FileName):
                    string filename = (sender as GrblViewModel).FileName;
                    MainWindow.ui.WindowTitle = filename;

                    if(string.IsNullOrEmpty(filename))
                        MainWindow.CloseFile();
                    else if (filename.StartsWith("SDCard:"))
                    {
                        sdStream = true;
                        MainWindow.EnableView(false, ViewType.GCodeViewer);
                    }
                    else if (filename.StartsWith("Wizard:"))
                    {
                        if (MainWindow.IsViewVisible(ViewType.GCodeViewer))
                        {
                            MainWindow.EnableView(true, ViewType.GCodeViewer);
                            gcodeRenderer.ShowTool = true;
                            gcodeRenderer.Open(GCode.File.Tokens);
                        }
                    }
                    else if (!string.IsNullOrEmpty(filename) && AppConfig.Settings.GCodeViewer.IsEnabled)
                    {
                        MainWindow.GCodeViewer.Open(GCode.File.Tokens);
                        MainWindow.EnableView(true, ViewType.GCodeViewer);
                        GCodeSender.EnablePolling(false);
                        gcodeRenderer.ShowTool = true;
                        gcodeRenderer.Open(GCode.File.Tokens);
                        GCodeSender.EnablePolling(true);
                    }
                    break;
            }
        }

#region Methods required by CNCView interface

        public ViewType ViewType { get { return ViewType.GRBL; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GCodeSender.RewindFile();
                GCodeSender.CallHandler(GCode.File.IsLoaded ? StreamingState.Idle : (sdStream ? StreamingState.Start : StreamingState.NoFile), false);
                sdStream = false;

                if (initOK != true)
                {
                    focusedControl = this;
                    model.Message = string.Format("Waiting for controller ({0})...", AppConfig.Settings.Base.PortParams);

                    Comms.com.PurgeQueue();
                    Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));

                    int timeout = 30; // 1.5s
                    do
                    {
                        System.Threading.Thread.Sleep(50);
                    } while (Comms.com.Reply == "" && --timeout != 0);

                    if (Comms.com.Reply.StartsWith("<Alarm"))
                    {
                        GrblViewModel data = (GrblViewModel)DataContext;
                        data.ParseStatus(Comms.com.Reply);

                        // Alarm 1, 2 and 10 are critical events
                        if (!(data.GrblState.Substate == 1 || data.GrblState.Substate == 2 || data.GrblState.Substate == 10))
                            InitSystem();
                    }
                    else if (Comms.com.Reply != "")
                        InitSystem();
                }

                if (initOK == null)
                    initOK = false;

                #if ADD_CAMERA
                if (MainWindow.UIViewModel.Camera != null)
                {
                    MainWindow.UIViewModel.Camera.MoveOffset += Camera_MoveOffset;
                    MainWindow.UIViewModel.Camera.Opened += Camera_Opened;
                }
                #endif
                //if (viewer == null)
                //    viewer = new Viewer();

                if(GCode.File.IsLoaded)
                    MainWindow.ui.WindowTitle = ((GrblViewModel)DataContext).FileName;

            }
            else if(ViewType != ViewType.Shutdown)
            {
                DRO.IsFocusable = false;
                #if ADD_CAMERA
                if (MainWindow.UIViewModel.Camera != null)
                    MainWindow.UIViewModel.Camera.MoveOffset -= Camera_MoveOffset;
                #endif
                focusedControl = Keyboard.FocusedElement is TextBox && (string)(Keyboard.FocusedElement as TextBox).Tag == "MDI" ? Keyboard.FocusedElement : this;
            }

            if (GCodeSender.Activate(activate)) {
                Task.Delay(500).ContinueWith(t => DRO.EnableFocus());
                Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    focusedControl.Focus();
                }), DispatcherPriority.Render);
            }
        }

        public void CloseFile()
        {
            gcodeRenderer.Close();
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        // https://stackoverflow.com/questions/5707143/how-to-get-the-width-height-of-a-collapsed-control-in-wpf
        private void showProgramLimits()
        {
            double height;

            if (limitsControl.Visibility == Visibility.Collapsed)
            {
                limitsControl.Visibility = Visibility.Hidden;
                limitsControl.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                height = limitsControl.DesiredSize.Height;
                limitsControl.Visibility = Visibility.Collapsed;
            }
            else
                height = limitsControl.ActualHeight;

            limitsControl.Visibility = (dp.ActualHeight - t1.ActualHeight - t2.ActualHeight + limitsControl.ActualHeight) > height ? Visibility.Visible : Visibility.Collapsed;
        }

#if ADD_CAMERA
        void Camera_Opened()
        {
            this.Focus();
        }

        void Camera_MoveOffset(CameraMoveMode Mode, double XOffset, double YOffset)
        {
            Comms.com.WriteString("G91G0\r"); // Enter relative G0 mode - set scale to 1.0?

            switch (Mode)
            {
                case CameraMoveMode.XAxisFirst:
                    Comms.com.WriteString(string.Format("X{0}\r", XOffset.ToInvariantString("F3")));
                    Comms.com.WriteString(string.Format("Y{0}\r", YOffset.ToInvariantString("F3")));
                    break;

                case CameraMoveMode.YAxisFirst:
                    Comms.com.WriteString(string.Format("Y{0}\r", YOffset.ToInvariantString("F3")));
                    Comms.com.WriteString(string.Format("X{0}\r", XOffset.ToInvariantString("F3")));
                    break;

                case CameraMoveMode.BothAxes:
                    ((GrblViewModel)DataContext).ExecuteCommand(string.Format("X{0}Y{1}", XOffset.ToInvariantString("F3"), YOffset.ToInvariantString("F3")));
                    break;
            }

            Comms.com.WriteString("G90\r"); // reset to previous or G80 to cancel motion mode?   
        }
#endif
        private void InitSystem()
        {
            initOK = true;

            // TODO: check if grbl is in a state that allows replies
            using (new UIUtils.WaitCursor())
            {
                GCodeSender.EnablePolling(false);
                GrblInfo.Get();
                GrblSettings.Get();
                GrblParserState.Get();
                GrblWorkParameters.Get();
                GCodeSender.EnablePolling(true);
            }

            model.Message = string.Empty;

            GrblCommand.ToolChange = GrblInfo.ManualToolChange ? "M61Q{0}" : "T{0}";

            if (keyboard == null)
            {
                keyboard = new KeypressHandler(model);
                GCodeSender.Configure(keyboard);
                gcodeRenderer.Configure();
            }

            showProgramLimits();

            if (!AppConfig.Settings.GCodeViewer.IsEnabled)
                tabGCode.Items.Remove(tab3D);

            if (GrblInfo.NumAxes > 3)
                limitsControl.Visibility = Visibility.Collapsed;

            if (GrblInfo.LatheModeEnabled)
            {
                MainWindow.EnableView(true, ViewType.Turning);
          //      MainWindow.EnableView(true, ViewType.Parting);
          //      MainWindow.EnableView(true, ViewType.Facing);
                MainWindow.EnableView(true, ViewType.G76Threading);
            }
            else
            {
                MainWindow.ShowView(false, ViewType.Turning);
                MainWindow.ShowView(false, ViewType.Parting);
                MainWindow.ShowView(false, ViewType.Facing);
                MainWindow.ShowView(false, ViewType.G76Threading);
            }

            if (GrblInfo.HasSDCard)
                MainWindow.EnableView(true, ViewType.SDCard);
            else
                MainWindow.ShowView(false, ViewType.SDCard);

            if (GrblInfo.HasPIDLog)
                MainWindow.EnableView(true, ViewType.PIDTuner);
            else
                MainWindow.ShowView(false, ViewType.PIDTuner);

            if (GrblInfo.NumTools > 0)
                MainWindow.EnableView(true, ViewType.Tools);
            else
                MainWindow.ShowView(false, ViewType.Tools);

            if(GrblSettings.ReportProbeCoordinates)
                MainWindow.EnableView(true, ViewType.Probing);
            MainWindow.EnableView(true, ViewType.Offsets);
            MainWindow.EnableView(true, ViewType.GRBLConfig);

            if(!string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
                MainWindow.EnableView(true, ViewType.TrinamicTuner);
            else
                MainWindow.ShowView(false, ViewType.TrinamicTuner);
        }

        void EnableUI(bool enable)
        {
            foreach (UserControl control in UIUtils.FindFirstLogicalChildren<UserControl>(this))
            {
                if (control.Name != nameof(statusControl))
                    control.IsEnabled = enable;
            }
            // disable ui components when in sleep mode
        }
#region UIevents

        void JobView_Load(object sender, EventArgs e)
        {
            GCodeSender.CallHandler(StreamingState.Idle, true);
        }

        private void JobView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                GCodeSender.Focus();
        }

        private void JobView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (GrblInfo.IsLoaded)
                showProgramLimits();
        }

        private void outside_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
        }

        void DRO_DROEnabledChanged(bool enabled)
        {
            if (!enabled)
                Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
                base.OnPreviewKeyDown(e);
        }
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
                base.OnPreviewKeyDown(e);
        }
        protected bool ProcessKeyPreview(KeyEventArgs e)
        {
            if (keyboard == null)
                return false;

            return keyboard.ProcessKeypress(e, !(mdiControl.IsFocused || DRO.IsFocused || spindleControl.IsFocused || workParametersControl.IsFocused));
        }

#endregion
    }
}
