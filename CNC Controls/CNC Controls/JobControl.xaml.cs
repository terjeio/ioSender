/*
 * JobControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.02 / 2019-10-12 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JobControl.xaml
    /// </summary>
    public partial class JobControl : UserControl
    {
        private enum JogMode
        {
            Step = 0,
            Slow,
            Fast,
            None // must be last!
        }

        private volatile int serialUsed = 0;
        private bool holdSignal = false;
        private JogMode jogMode = JogMode.None;
        private volatile Key[] axisjog = new Key[3] { Key.None, Key.None, Key.None };
        private double[] jogDistance = new double[3] { 0.05, 500.0, 500.0 };
        private double[] jogSpeed = new double[3] { 100.0, 200.0, 500.0 };
        private volatile StreamingState streamingState = StreamingState.NoFile;
        private GrblState grblState;
        private GrblViewModel model = new GrblViewModel();
        private ScrollViewer scroll = null;

        private int PollInterval = 200, serialSize = 128, CurrLine = 0, PendingLine = 0, PgmEndLine = -1, ACKPending = 0;
        private bool initOK = false, pgmStarted = false, pgmComplete = false;
        private PollGrbl poller = null;
        private Thread polling = null;
        private DataRow currentRow = null, nextRow = null;

 //       private delegate void GcodeCallback(string data);

        public delegate void StreamingStateChangedHandler(StreamingState state, bool MPGMode);
        public event StreamingStateChangedHandler StreamingStateChanged;

        public JobControl()
        {
            InitializeComponent();

            DataContextChanged += JobControl_DataContextChanged;

            grblState.State = GrblStates.Unknown;
            grblState.Substate = 0;
            grblState.MPG = false;

            GCode.FileChanged += gcode_FileChanged;

            poller = new PollGrbl();
            polling = new Thread(new ThreadStart(poller.Run));
            polling.Start();
            Thread.Sleep(100);
        }

        void gcode_FileChanged(string filename)
        {
            ((GrblViewModel)DataContext).FileName = filename;
        }

        private void JobControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= OnDataContextPropertyChanged;
            if (e.NewValue != null && e.NewValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.NewValue).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.GrblState):
                    GrblStateChanged(((GrblViewModel)sender).GrblState);
                    break;

                case nameof(GrblViewModel.MDICommand):
                    SendMDICommand(((GrblViewModel)sender).MDICommand);
                    ((GrblViewModel)sender).MDICommand = string.Empty;
                    break;

                case nameof(GrblViewModel.IsMPGActive):
                    grblState.MPG = ((GrblViewModel)sender).IsMPGActive == true;
                    SetStreamingState(grblState.MPG ? StreamingState.Disabled : StreamingState.Idle);
                    break;

                case nameof(GrblViewModel.Signals):
                {
                    var signals = ((GrblViewModel)sender).Signals;
                    if (JobPending && signals[Signals.CycleStart] && !signals[Signals.Hold] && holdSignal)
                        CycleStart();
                    holdSignal = signals[Signals.Hold];
                }
                break;
            }
        }

        public GrblViewModel Parameters { get { return model;  } }
        public GrblStates state { get { return grblState.State; } }
        public GCode GCode { get; private set; } = new GCode();
        public StreamingState StreamingState { get { return streamingState; } }
        public bool canJog { get { return grblState.State == GrblStates.Idle || grblState.State == GrblStates.Tool || grblState.State == GrblStates.Jog; } }
        public bool JobPending { get { return GCode.Loaded && !JobTimer.IsRunning; } }

        public void CloseFile()
        {
            GCode.CloseFile();
        }

        public bool Activate(bool activate)
        {
            if (activate)
            {
                if (!initOK)
                {
                    initOK = true;
                    serialSize = Math.Min(300, (int)(GrblInfo.SerialBufferSize * 0.9f)); // size should be less than hardware handshake HWM
                }
                Comms.com.DataReceived += DataReceived;
                //if (activate) // Request a complete status report
                //    Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL));
                poller.SetState(PollInterval);
            }
            else
            {
                poller.SetState(0);
                Comms.com.DataReceived -= DataReceived;
            }

            return activate;
        }

        // Configure to match Grbl settings (if loaded)
        public bool Config()
        {
            if (GrblSettings.Loaded)
            {
                double val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogStepDistance)).Equals(double.NaN))
                    jogDistance[(int)JogMode.Step] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogSlowDistance)).Equals(double.NaN))
                    jogDistance[(int)JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogFastDistance)).Equals(double.NaN))
                    jogDistance[(int)JogMode.Fast] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogStepSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Step] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogSlowSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogFastSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Fast] = val;
            }

            return GrblSettings.Loaded;
        }

        #region UIevents
        public bool ProcessKeypress(KeyEventArgs e)
        {
            string command = "";

            if (e.Key == Key.Space && grblState.State != GrblStates.Idle)
            {
                btnHold_Click(null, null);
                return true;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (e.SystemKey == Key.S)
                {
                    SetStreamingState(StreamingState.Stop);
                    return true;
                }

                if (e.SystemKey == Key.R)
                {
                    CycleStart();
                    return true;
                }
            }

            bool isJogging = jogMode != JogMode.None;

            if (e.IsUp && (isJogging || grblState.State == GrblStates.Jog))
            {
                bool cancel = false;

                isJogging = false;

                for (int i = 0; i < 3; i++)
                {
                    if (axisjog[i] == e.Key)
                    {
                        axisjog[i] = Key.None;
                        cancel = true;
                    }
                    else
                        isJogging = isJogging || (axisjog[i] != Key.None);
                }

                if (cancel && !isJogging && jogMode != JogMode.Step)
                    JogCancel();
            }

            if (!isJogging && Comms.com.OutCount != 0)
                return true;

            //            if ((keycode == Keys.ShiftKey || keycode == Keys.ControlKey) && !isJogging)
            //                return false;

            if (e.IsDown && canJog)
            {
                // Do not respond to autorepeats!
                if (e.IsRepeat)
                    return true;

                switch (e.Key)
                {
                    case Key.PageUp:
                        isJogging = axisjog[2] != Key.PageUp;
                        axisjog[2] = Key.PageUp;
                        break;

                    case Key.PageDown:
                        isJogging = axisjog[2] != Key.PageDown;
                        axisjog[2] = Key.PageDown;
                        break;

                    case Key.Left:
                        isJogging = axisjog[0] != Key.Left;
                        axisjog[0] = Key.Left;
                        break;

                    case Key.Up:
                        isJogging = axisjog[1] != Key.Up;
                        axisjog[1] = Key.Up;
                        break;

                    case Key.Right:
                        isJogging = axisjog[0] != Key.Right;
                        axisjog[0] = Key.Right;
                        break;

                    case Key.Down:
                        isJogging = axisjog[1] != Key.Down;
                        axisjog[1] = Key.Down;
                        break;
                }
            }

            if (isJogging)
            {
                if (GrblInfo.LatheModeEnabled)
                {
                    for (int i = 0; i < 2; i++) switch (axisjog[i])
                        {
                            case Key.Left:
                                command += "Z-{0}";
                                break;

                            case Key.Up:
                                command += "X-{0}";
                                break;

                            case Key.Right:
                                command += "Z{0}";
                                break;

                            case Key.Down:
                                command += "X{0}";
                                break;
                        }
                }
                else for (int i = 0; i < 3; i++) switch (axisjog[i])
                {
                    case Key.PageUp:
                        command += "Z{0}";
                        break;

                    case Key.PageDown:
                        command += "Z-{0}";
                        break;

                    case Key.Left:
                        command += "X-{0}";
                        break;

                    case Key.Up:
                        command += "Y{0}";
                        break;

                    case Key.Right:
                        command += "X{0}";
                        break;

                    case Key.Down:
                        command += "Y-{0}";
                        break;
                }

                if ((isJogging = command != ""))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        jogMode = JogMode.Step;
                    else if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                        jogMode = JogMode.Fast;
                    else
                        jogMode = JogMode.Slow;

                    SendJogCommand("$J=G91" + string.Format(command + "F{1}",
                                                    jogDistance[(int)jogMode].ToInvariantString(),
                                                     jogSpeed[(int)jogMode].ToInvariantString()));
                }
            }

            return isJogging;
        }

        void btnRewind_Click(object sender, RoutedEventArgs e)
        {
            RewindFile();
            SetStreamingState(streamingState);
        }

        void btnHold_Click(object sender, RoutedEventArgs e)
        {
            Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_FEED_HOLD));
        }

        void btnStop_Click(object sender, RoutedEventArgs e)
        {
            SetStreamingState(StreamingState.Stop);
        }

        void btnStart_Click(object sender, RoutedEventArgs e)
        {
            CycleStart();
        }

        private void grdGCode_DragEnter(object sender, DragEventArgs e)
        {
            bool allow = streamingState == StreamingState.Idle || streamingState == StreamingState.NoFile;

            if (allow && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                allow = files.Count() == 1 && (files[0].Contains(".nc") || files[0].Contains(".gcode") || files[0].Contains(".txt"));
            }

            e.Effects = allow ? DragDropEffects.All : DragDropEffects.None;
        }

        private void grdGCode_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (files.Count() == 1)
            {
                using (new UIUtils.WaitCursor())
                {

                    GCode.LoadFile(files[0]);

                    //      ppiControl.Speed = file.max_feed;

                    grdGCode.DataContext = GCode.Data.DefaultView;
                    CurrLine = 0;
                    PendingLine = 0;
                    PgmEndLine = GCode.Data.Rows.Count - 1;
                    scroll = UIUtils.GetScrollViewer(grdGCode);

                    SetStreamingState(GCode.Loaded ? StreamingState.Idle : StreamingState.NoFile);
                }
            }
        }

        #endregion
        public void CycleStart()
        {
            if (grblState.State == GrblStates.Hold || grblState.State == GrblStates.Tool || (grblState.State == GrblStates.Run && grblState.Substate == 1))
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_CYCLE_START));
            else if (GCode.Loaded)
            {
                lblRunTime.Content = "";
                ACKPending = CurrLine = serialUsed = 0;
                pgmStarted = false;
                System.Threading.Thread.Sleep(250);
                Comms.com.PurgeQueue();
                model.Message = "";
                nextRow = GCode.Data.Rows[0];
                JobTimer.Start();
                SetStreamingState(StreamingState.Send);
                //         DataReceived("!start");
            }
        }

        public void SendReset()
        {
            Comms.com.WriteByte((byte)GrblConstants.CMD_RESET);
            System.Threading.Thread.Sleep(20);
            grblState.State = GrblStates.Unknown;
            grblState.Substate = 0;
        }

        public void JogCancel()
        {
            streamingState = StreamingState.Idle;
            while (Comms.com.OutCount != 0);
            //    Application.DoEvents(); //??
            Comms.com.WriteByte((byte)GrblConstants.CMD_JOG_CANCEL); // Cancel jog
            jogMode = JogMode.None;
        }

        public void SendJogCommand(string command)
        {
            if (streamingState == StreamingState.Jogging || grblState.State == GrblStates.Jog)
            {
                while (Comms.com.OutCount != 0);
                    //Application.DoEvents(); //??
                Comms.com.WriteByte((byte)GrblConstants.CMD_JOG_CANCEL); // Cancel current jog
            }
            streamingState = StreamingState.Jogging;
            Comms.com.WriteCommand(command);
        }

        public void SendRTCommand(string command)
        {
            Comms.com.WriteByte((byte)command[0]);
        }

        private void SendMDICommand(string command)
        {
            model.Message = "";

            if (command.Length == 1)
                SendRTCommand(command);
            else if (streamingState == StreamingState.Idle || streamingState == StreamingState.NoFile || streamingState == StreamingState.ToolChange || command == GrblConstants.CMD_UNLOCK)
            {
                //                command = command.ToUpper();
                try
                {
                    GCode.ParseBlock(command + "\r", true);
                    GCode.commands.Enqueue(command);
                    if (streamingState != StreamingState.SendMDI)
                    {
                        streamingState = StreamingState.SendMDI;
                        DataReceived("ok");
                    }
                }
                catch
                {
                }
            }
        }

        public void RewindFile()
        {
            pgmComplete = false;

            if (GCode.Loaded)
            {
                grdGCode.DataContext = null;

                foreach (DataRow row in GCode.Data.Rows)
                    row["Sent"] = "";

                grdGCode.DataContext = GCode.Data.DefaultView;

                if(scroll == null)
                    scroll = UIUtils.GetScrollViewer(grdGCode);

                scroll.ScrollToTop();

                CurrLine = PendingLine = 0;
                PgmEndLine = GCode.Data.Rows.Count - 1;

                SetStreamingState(StreamingState.Idle);
            }
        }

        public void SetStreamingState(StreamingState newState)
        {
            switch (newState)
            {
                case StreamingState.Disabled:
                    IsEnabled = false;
                    break;

                case StreamingState.Idle:
                case StreamingState.NoFile:
                    IsEnabled = !grblState.MPG;
                    btnStart.IsEnabled = GCode.Loaded;
                    btnStop.IsEnabled = false;
                    btnHold.IsEnabled = !grblState.MPG;
                    btnRewind.IsEnabled = !grblState.MPG && GCode.Loaded && CurrLine != 0;
                    model.IsJobRunning = false;
                    break;

                case StreamingState.Send:
                    btnStart.IsEnabled = false;
                    btnHold.IsEnabled = !grblState.MPG;
                    btnStop.IsEnabled = !grblState.MPG;
                    btnRewind.IsEnabled = false;
                    if (GCode.Loaded && !grblState.MPG)
                    {
                        model.IsJobRunning = true;
                        SendNextLine();
                    }
                    break;

                case StreamingState.Halted:
                    btnStart.IsEnabled = !grblState.MPG;
                    btnHold.IsEnabled = false;
                    btnStop.IsEnabled = !grblState.MPG;
                    break;

                case StreamingState.FeedHold:
                    btnStart.IsEnabled = !grblState.MPG;
                    btnHold.IsEnabled = false;
                    break;

                case StreamingState.ToolChange:
                    btnStart.IsEnabled = !grblState.MPG;
                    btnHold.IsEnabled = false;
                    break;

                case StreamingState.Stop:
                    btnStart.IsEnabled = false;
                    btnStop.IsEnabled = false;
                    btnRewind.IsEnabled = !grblState.MPG;
                    model.IsJobRunning = false;
                    if (!grblState.MPG)
                    {
                        Comms.com.WriteByte((byte)GrblConstants.CMD_STOP);
                        if (JobTimer.IsRunning)
                            JobTimer.Stop();
                    }
                    break;
            }

            model.StreamingState = streamingState = newState;

            StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
        }

        void GrblStateChanged(GrblState newstate)
        {
            switch (newstate.State)
            {
                case GrblStates.Idle:
                    if (pgmComplete)
                    {
                        JobTimer.Stop();
                        lblRunTime.Content = JobTimer.RunTime;
                        RewindFile();
                    }
                    if (JobTimer.IsRunning)
                        JobTimer.Pause = true;
                    else
                        SetStreamingState(StreamingState.Idle);
                    break;

                case GrblStates.Jog:
                    model.IsJobRunning = true;
                    break;

                case GrblStates.Run:
                    if (JobTimer.IsPaused)
                        JobTimer.Pause = false;
                    SetStreamingState(StreamingState.Send);
                    if (newstate.Substate == 1)
                    {
                        btnStart.IsEnabled = !grblState.MPG;
                        btnHold.IsEnabled = false;
                    }
                    else if (grblState.Substate == 1)
                    {
                        btnStart.IsEnabled = false;
                        btnHold.IsEnabled = !grblState.MPG;
                    }
                    break;

                case GrblStates.Tool:
                    SetStreamingState(StreamingState.ToolChange);
                    if (!grblState.MPG)
                        Comms.com.WriteByte((byte)GrblConstants.CMD_TOOL_ACK);
                    break;

                case GrblStates.Hold:
                    SetStreamingState(StreamingState.FeedHold);
                    break;

                case GrblStates.Door:
                    if (newstate.Substate > 0)
                    {
                        if (streamingState == StreamingState.Send)
                            SetStreamingState(StreamingState.FeedHold);
                    }
                    break;
            }

            grblState.State = newstate.State;
            grblState.Substate = newstate.Substate;
            grblState.MPG = newstate.MPG;
        }

        void SendNextLine()
        {
            while (nextRow != null && serialUsed < (serialSize - (int)nextRow["Length"]))
            {
                if (GCode.commands.Count > 0)
                    Comms.com.WriteCommand(GCode.commands.Dequeue());
                else
                {
                    currentRow = nextRow;
                    string line = GCode.StripSpaces((string)currentRow["Data"]);
                    currentRow["Sent"] = "*";
                    if (line == "%")
                    {
                        if (!(pgmStarted = !pgmStarted))
                            PgmEndLine = CurrLine;
                    }
                    else if ((bool)currentRow["ProgramEnd"])
                        PgmEndLine = CurrLine;
                    nextRow = PgmEndLine == CurrLine ? null : GCode.Data.Rows[++CurrLine];
                    //            ParseBlock(line + "\r");
                    serialUsed += (int)currentRow["Length"];
                    Comms.com.WriteCommand(line);
                }
                ACKPending++;
            }
        }

        void DataReceived(string data)
        {
            if (data.Length == 0)
                return;

            if (data.Substring(0, 1) == "<")
            {
                model.ParseStatus(data.Remove(data.Length - 1));

                if (JobTimer.IsRunning && !JobTimer.IsPaused)
                    lblRunTime.Content = JobTimer.RunTime;
            }
            else if (data.StartsWith("ALARM"))
            {
                string[] alarm = data.Split(':');

                model.SetGRBLState("Alarm", alarm.Length == 2 ? int.Parse(alarm[1]) : -1, false);
            }
            else if (data.StartsWith("[GC:"))
                model.ParseGCStatus(data);
            else if (data.StartsWith("["))
            {
                if (!GCode.Loaded && data == "[MSG:Pgm End]")
                    SetStreamingState(StreamingState.NoFile);

                model.Message = data;
            }
            else if (data.StartsWith("Grbl"))
            {
                model.GrblReset = true;
            }
            else if (streamingState != StreamingState.Jogging)
            {
                if (data != "ok")
                {
                    if (data.StartsWith("error:"))
                    {
                        try
                        {
                            model.SetError(int.Parse(data.Substring(6)));
                        }
                        catch
                        {
                        }
                    }
                    else
                        model.Message = data;
                }

                if (ACKPending > 0 && streamingState == StreamingState.Send)
                {
                    ACKPending--;
                    if ((string)GCode.Data.Rows[PendingLine]["Sent"] == "*")
                        serialUsed -= (int)GCode.Data.Rows[PendingLine]["Length"];
                    if (serialUsed < 0)
                        serialUsed = 0;
                    GCode.Data.Rows[PendingLine]["Sent"] = data;

                    if (PendingLine > 5)
                        scroll.ScrollToVerticalOffset(PendingLine - 5);

                    if (streamingState == StreamingState.Send)
                    {
                        if (data.StartsWith("error"))
                        {
                            SetStreamingState(StreamingState.Halted);
                            //   Comms.com.WriteByte((byte)GrblConstants.CMD_JOG_CANCEL); // Flush grbl buffers
                        }
                        else if ((pgmComplete = PgmEndLine == PendingLine))
                        {
                            ACKPending = CurrLine = 0;
                            if (grblState.State == GrblStates.Idle)
                                model.SetGRBLState(GrblStates.Idle.ToString(), -1, true);
                        }
                        else
                            SendNextLine();
                    }
                    PendingLine++;
                }

                switch (streamingState)
                {
                    case StreamingState.Send:
                        SendNextLine();
                        break;

                    case StreamingState.SendMDI:
                        if (GCode.commands.Count > 0)
                            Comms.com.WriteCommand(GCode.commands.Dequeue());
                        if (GCode.commands.Count == 0)
                            streamingState = StreamingState.Idle;
                        break;

                    case StreamingState.Reset:
                        Comms.com.WriteCommand(GrblConstants.CMD_UNLOCK);
                        streamingState = StreamingState.AwaitResetAck;
                        break;

                    case StreamingState.AwaitResetAck:
                        SetStreamingState(GCode.Loaded ? StreamingState.Idle : StreamingState.NoFile);
                        break;
                }
            }
        }
    }
}

