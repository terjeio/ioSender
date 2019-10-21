/*
 * JobView.xaml.cs - part of Grbl Code Sender
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
using System.Windows.Input;
using CNC.View;
using CNC.Core;
using System.ComponentModel;
using System.Threading.Tasks;
using CNC.Controls;
using System.Windows.Threading;
#if ADD_CAMERA
using static CNC.Controls.Camera.CameraControl;
#endif

namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for JobView.xaml
    /// </summary>
    public partial class JobView : UserControl, CNCView
    {
        private bool initOK = false;
        private GrblViewModel model;

     //   private Viewer viewer = null;

    //    private delegate void GcodeCallback(string data);
        public JobView()
        {
            InitializeComponent();

            DataContext = model = GCodeSender.Parameters;

            DRO.DROEnabledChanged += DRO_DROEnabledChanged;


        //    GCodeSender.GotFocus += GCodeSender_GotFocus;

            ((INotifyPropertyChanged)DataContext).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch(e.PropertyName)
            {
                case nameof(GrblViewModel.GrblState):
                    if (!initOK && ((GrblViewModel)sender).GrblState.State != GrblStates.Alarm)
                        InitSystem();
                    break;

                case nameof(GrblViewModel.IsSleepMode):
                    EnableUI(!((GrblViewModel)sender).IsSleepMode);
                    break;

                case nameof(GrblViewModel.IsJobRunning):
                    MainWindow.ui.JobRunning = ((GrblViewModel)sender).IsJobRunning;
                    break;

                case nameof(GrblViewModel.GrblReset):
                    if (((GrblViewModel)sender).GrblReset)
                        Comms.com.WriteCommand(GrblConstants.CMD_GETPARSERSTATE);
                    break;

                case nameof(GrblViewModel.ParserState):
                    if (((GrblViewModel)sender).GrblReset)
                    {
                        EnableUI(true);
                        ((GrblViewModel)sender).GrblReset = false;
                    }
                    break;

                case nameof(GrblViewModel.FileName):
                    string filename = ((GrblViewModel)sender).FileName;
                    MainWindow.ui.WindowTitle = filename;
                    if (filename != string.Empty) {
                        MainWindow.enableControl(true, ViewType.GCodeViewer);
                        MainWindow.GCodeViewer.Open(filename, GCodeSender.GCode.Tokens, GCodeSender.GCode.BoundingBox);
                    }
                    break;
            }
        }

#region Methods required by CNCView interface

        public ViewType mode { get { return ViewType.GRBL; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GCodeSender.RewindFile();
                GCodeSender.SetStreamingState(GCodeSender.GCode.Loaded ? StreamingState.Idle : StreamingState.NoFile);

                if (!initOK)
                {
                    Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT));

                    int timeout = 30; // 1.5s
                    do
                    {
                        System.Threading.Thread.Sleep(50);
                    } while (Comms.com.Reply == "" && --timeout != 0);

                    if (Comms.com.Reply.StartsWith("<Alarm"))
                    {
                        if (Comms.com.Reply.StartsWith("<Alarm:"))
                        {
                            int i = Comms.com.Reply.IndexOf('|');
                            string s = Comms.com.Reply.Substring(7, i - 7);
                            if (!"1,2,10".Contains(s)) // Alarm 1, 2 and 10 are critical events
                                InitSystem();
                            if (s == "11")
                                txtStatus.Text = "<Home> to continue.";
                            else
                                txtStatus.Text = "<Reset> then <Unlock> to continue.";
                        }
                        else
                            txtStatus.Text = "<Reset> then <Unlock> to continue.";
                    }
                    else
                        InitSystem();
                }
                #if ADD_CAMERA
                if (MainWindow.Camera != null)
                {
                    MainWindow.Camera.CameraControl.MoveOffset += Camera_MoveOffset;
                    MainWindow.Camera.Opened += Camera_Opened;
                }
                #endif
                //if (viewer == null)
                //    viewer = new Viewer();

                if (GCodeSender.GCode.Loaded)
                    MainWindow.ui.WindowTitle = ((GrblViewModel)DataContext).FileName;

            }
            else if(mode != ViewType.Shutdown)
            {
                DRO.IsFocusable = false;
                #if ADD_CAMERA
                if (MainWindow.Camera != null)
                    MainWindow.Camera.CameraControl.MoveOffset -= Camera_MoveOffset;
                #endif
            }

            if (GCodeSender.Activate(activate)) {
                Task.Delay(500).ContinueWith(t => DRO.EnableFocus());
                Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    Focus();
                }), DispatcherPriority.Render);
            }
        }

        public void CloseFile()
        {
            GCodeSender.CloseFile();
        }

#endregion

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
                    Grbl.MDICommand(DataContext, string.Format("X{0}Y{1}", XOffset.ToInvariantString("F3"), YOffset.ToInvariantString("F3")));
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

            workParametersControl.ToolChangeCommand = GrblInfo.ManualToolChange ? "T{0}M6" : "T{0}";

            GCodeSender.Config();

MainWindow.enableControl(true, ViewType.G76Threading);
MainWindow.enableControl(true, ViewType.Turning);

            if (GrblInfo.LatheModeEnabled)
            {
                DRO.EnableLatheMode();
                signalsControl.SetLatheMode();
                MainWindow.enableControl(true, ViewType.Turning);
                MainWindow.enableControl(true, ViewType.Facing);
                MainWindow.enableControl(true, ViewType.G76Threading);
            }
            else
            {
                DRO.SetNumAxes(GrblInfo.NumAxes);
                signalsControl.SetNumAxes(GrblInfo.NumAxes);
       //         MainWindow.showControl(false, ViewType.Turning);
                MainWindow.showControl(false, ViewType.Facing);
      //          MainWindow.showControl(false, ViewType.G76Threading);
            }

            if (GrblInfo.HasSDCard)
                MainWindow.enableControl(true, ViewType.SDCard);
            else
                MainWindow.showControl(false, ViewType.SDCard);

            if (GrblInfo.HasPIDLog)
                MainWindow.enableControl(true, ViewType.PIDTuner);
            else
                MainWindow.showControl(false, ViewType.PIDTuner);

            if (GrblInfo.NumTools > 0)
                MainWindow.enableControl(true, ViewType.Tools);
            else
                MainWindow.showControl(false, ViewType.Tools);

            MainWindow.enableControl(true, ViewType.Offsets);
            MainWindow.enableControl(true, ViewType.GRBLConfig);

            if(!string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
                MainWindow.enableControl(true, ViewType.TrinamicTuner);
            else
                MainWindow.showControl(false, ViewType.TrinamicTuner);

            MainWindow.GCodePush += UserUI_GCodePush;

            txtStatus.Text = "";
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

        void UserUI_GCodePush(string gcode, CNC.Core.Action action)
        {
            GCodeSender.GCode.AddBlock(gcode, action);
        }

#region UIevents

        void JobView_Load(object sender, EventArgs e)
        {
            GCodeSender.SetStreamingState(StreamingState.Idle);
        }

        private void JobView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                GCodeSender.Focus();
        }

        private void GCodeSender_GotFocus(object sender, EventArgs e)
        {
          //  Focus();
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
        protected bool ProcessKeyPreview(System.Windows.Input.KeyEventArgs e)
        {
            if (mdiControl.IsFocused || DRO.IsFocused || spindleControl.IsFocused || workParametersControl.IsFocused)
                return false;

            return GCodeSender.ProcessKeypress(e);
        }

#endregion
    }
}
