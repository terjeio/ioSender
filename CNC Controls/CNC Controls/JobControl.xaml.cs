/*
 * JobControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.12 / 2020-03-11 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2020, Io Engineering (Terje Io)
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
using CNC.Core;
using CNC.GCode;
using Microsoft.Win32;

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

        private enum StreamingHandler
        {
            Idle = 0,
            SendFile,
            FeedHold,
            ToolChange,
            AwaitAction,
            AwaitIdle,
            Previous,
            Max // only used for array instantiation
        }

        private struct StreamingHandlerFn
        {
            public StreamingHandler Handler;
            public bool Count;
            public Func<StreamingState, bool, bool> Call;
        }

        private struct JobData
        {
            public int CurrLine, PendingLine, PgmEndLine, ACKPending, serialUsed;
            public bool Started, Complete, IsSDFile;
            public DataRow CurrentRow, NextRow;
        }

        private int serialSize = 128;
        private bool holdSignal = false, initOK = false, useBuffering = false;
        private volatile Key[] axisjog = new Key[3] { Key.None, Key.None, Key.None };
        private double[] jogDistance = new double[3] { 0.05, 500.0, 500.0 };
        private double[] jogSpeed = new double[3] { 100.0, 200.0, 500.0 };
        private volatile StreamingState streamingState = StreamingState.NoFile;
        private JogMode jogMode = JogMode.None;
        private GrblState grblState;
        private GrblViewModel model;
        private PollGrbl poller = null;
        private Thread polling = null;
        private JobData job;
        private int missed = 0;

        private StreamingHandlerFn[] streamingHandlers = new StreamingHandlerFn[(int)StreamingHandler.Max];
        private StreamingHandlerFn streamingHandler;

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

            job.PgmEndLine = -1;

            streamingHandlers[(int)StreamingHandler.Idle].Call = StreamingIdle;
            streamingHandlers[(int)StreamingHandler.Idle].Count = false;

            streamingHandlers[(int)StreamingHandler.SendFile].Call = StreamingSendFile;
            streamingHandlers[(int)StreamingHandler.SendFile].Count = true;

            streamingHandlers[(int)StreamingHandler.ToolChange].Call = StreamingToolChange;
            streamingHandlers[(int)StreamingHandler.ToolChange].Count = false;

            streamingHandlers[(int)StreamingHandler.FeedHold].Call = StreamingFeedHold;
            streamingHandlers[(int)StreamingHandler.FeedHold].Count = true;

            streamingHandlers[(int)StreamingHandler.AwaitAction].Call = StreamingAwaitAction;
            streamingHandlers[(int)StreamingHandler.AwaitAction].Count = true;

            streamingHandlers[(int)StreamingHandler.AwaitIdle].Call = StreamingAwaitIdle;
            streamingHandlers[(int)StreamingHandler.AwaitIdle].Count = false;

            streamingHandler = streamingHandlers[(int)StreamingHandler.Previous] = streamingHandlers[(int)StreamingHandler.Idle];

            for (int i = 0; i < streamingHandlers.Length; i++)
                streamingHandlers[i].Handler = (StreamingHandler)i;

            poller = new PollGrbl();
            polling = new Thread(new ThreadStart(poller.Run));
            polling.Start();
            Thread.Sleep(100);
        }

        private void JobControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= OnDataContextPropertyChanged;
            if (e.NewValue != null && e.NewValue is INotifyPropertyChanged)
            {
                model = (GrblViewModel)e.NewValue;
                model.PropertyChanged += OnDataContextPropertyChanged;
                model.OnRealtimeStatusProcessed += RealtimeStatusProcessed;
                model.OnCommandResponseReceived += ResponseReceived;
                GCode.File.Model = model;
            }
        }

        private void RealtimeStatusProcessed(string response)
        {
            if (JobTimer.IsRunning && !JobTimer.IsPaused)
                model.RunTime = JobTimer.RunTime;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
                {
                    case nameof(GrblViewModel.GrblState):
                        GrblStateChanged(((GrblViewModel)sender).GrblState);
                        break;

                    case nameof(GrblViewModel.MDI):
                        SendCommand(((GrblViewModel)sender).MDI);
                        break;

                    case nameof(GrblViewModel.IsMPGActive):
                        grblState.MPG = ((GrblViewModel)sender).IsMPGActive == true;
                        poller.SetState(grblState.MPG ? 0 : model.PollInterval);
                        streamingHandler.Call(grblState.MPG ? StreamingState.Disabled : StreamingState.Idle, false);
                        break;

                    case nameof(GrblViewModel.Signals):
                        {
                            var signals = ((GrblViewModel)sender).Signals;
                            if (JobPending && signals[Signals.CycleStart] && !signals[Signals.Hold] && holdSignal)
                                CycleStart();
                            holdSignal = signals[Signals.Hold];
                        }
                        break;

                    case nameof(GrblViewModel.ProgramEnd):
                        if (!GCode.File.IsLoaded)
                            streamingHandler.Call(job.IsSDFile ? StreamingState.JobFinished : StreamingState.NoFile, job.IsSDFile);
                        else if(JobTimer.IsRunning && !job.Complete)
                            streamingHandler.Call(StreamingState.JobFinished, true);
                        break;

                    case nameof(GrblViewModel.FileName):
                        {
                            if(((GrblViewModel)sender).FileName != "") {
                                job.IsSDFile = false;
                                job.CurrLine = job.PendingLine = job.ACKPending = 0;
                                job.PgmEndLine = GCode.File.Blocks - 1;
                                streamingHandler.Call(GCode.File.IsLoaded ? StreamingState.Idle : StreamingState.NoFile, false);
                            }
                            break;
                        }

                    case nameof(GrblViewModel.GrblReset):
                        {
                            JobTimer.Stop();
                            streamingHandler.Call(StreamingState.Stop, true);
                        }
                        break;
                }
        }

        public bool canJog { get { return grblState.State == GrblStates.Idle || grblState.State == GrblStates.Tool || grblState.State == GrblStates.Jog; } }
        public bool JobPending { get { return GCode.File.IsLoaded && !JobTimer.IsRunning; } }

        public void CloseFile()
        {
            job.NextRow = null;
            GCode.File.Close();
        }

        public bool Activate(bool activate)
        {
            if (activate && !initOK)
            {
                initOK = true;
                serialSize = Math.Min(300, (int)(GrblInfo.SerialBufferSize * 0.9f)); // size should be less than hardware handshake HWM
                GCode.File.Parser.Dialect = GrblSettings.IsGrblHAL ? Dialect.GrblHAL : Dialect.Grbl;
            }

            EnablePolling(activate);

            return activate;
        }

        public void EnablePolling(bool enable)
        {
            if (enable)
            {
                if (!poller.IsEnabled)
                {
                    //        Comms.com.DataReceived += model.DataReceived;
                    poller.SetState(model.PollInterval);
                }
            }
            else
            {
                if (poller.IsEnabled)
                {
                    poller.SetState(0);
                    //             Comms.com.DataReceived -= model.DataReceived;
                }
            }
        }

        // Configure to match Grbl settings (if loaded)
        public bool Config(Config config)
        {
            bool useFirmwareJog = false;

            if (GrblSettings.Loaded)
            {
                double val;
                if ((useFirmwareJog = !(val = GrblSettings.GetDouble(GrblSetting.JogStepDistance)).Equals(double.NaN)))
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

                model.IsMetric = GrblSettings.GetString(GrblSetting.ReportInches) != "1";
            }

            if (!useFirmwareJog)
            {
                jogDistance[(int)JogMode.Step] = config.Jog.StepDistance;
                jogDistance[(int)JogMode.Slow] = config.Jog.SlowDistance;
                jogDistance[(int)JogMode.Fast] = config.Jog.SlowDistance;
                jogSpeed[(int)JogMode.Step] = config.Jog.StepFeedrate;
                jogSpeed[(int)JogMode.Slow] = config.Jog.SlowFeedrate;
                jogSpeed[(int)JogMode.Fast] = config.Jog.FastFeedrate;
            }

            GCodeParser.IgnoreM6 = config.IgnoreM6;
            GCodeParser.IgnoreM7 = config.IgnoreM7;
            GCodeParser.IgnoreM8 = config.IgnoreM8;

            useBuffering = config.UseBuffering && GrblSettings.IsGrblHAL;

            return GrblSettings.Loaded;
        }

        public bool CallHandler (StreamingState state, bool always)
        {
            return streamingHandler.Call(state, always);
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
                    streamingHandler.Call(StreamingState.Stop, false);
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
            streamingHandler.Call(streamingState, true);
        }

        void btnHold_Click(object sender, RoutedEventArgs e)
        {
            Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_FEED_HOLD));
        }

        void btnStop_Click(object sender, RoutedEventArgs e)
        {
            streamingHandler.Call(StreamingState.Stop, true);
        }

        void btnStart_Click(object sender, RoutedEventArgs e)
        {
            CycleStart();
        }

        private void grdGCode_Drag(object sender, DragEventArgs e)
        {
            GCode.File.Drag(sender, e);
        }

        private void grdGCode_Drop(object sender, DragEventArgs e)
        {
            GCode.File.Drop(sender, e);
        }

        #endregion

        public void CycleStart()
        {
            if (grblState.State == GrblStates.Hold || grblState.State == GrblStates.Tool || (grblState.State == GrblStates.Run && grblState.Substate == 1))
                Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_CYCLE_START));
            else if(JobTimer.IsRunning)
            {
                model.Message = "";
                JobTimer.Pause = false;
                streamingHandler.Call(StreamingState.Send, false);
            }
            else if (GCode.File.IsLoaded)
            {
                model.RunTime = "";
                job.ACKPending = job.CurrLine = job.ACKPending = job.serialUsed = 0;
                job.Started = false;
                job.NextRow = GCode.File.Data.Rows[0];
                System.Threading.Thread.Sleep(250);
                Comms.com.PurgeQueue();
                model.Message = "";
                JobTimer.Start();
                streamingHandler.Call(StreamingState.Send, false);
                SendNextLine();
            }
        }

        public void SendReset()
        {
            Comms.com.WriteByte(GrblConstants.CMD_RESET);
            System.Threading.Thread.Sleep(20);
            grblState.State = GrblStates.Unknown;
            grblState.Substate = 0;
        }

        public void JogCancel()
        {
            streamingState = StreamingState.Idle;
            while (Comms.com.OutCount != 0) ;
            //    Application.DoEvents(); //??
            Comms.com.WriteByte(GrblConstants.CMD_JOG_CANCEL); // Cancel jog
            jogMode = JogMode.None;
        }

        public void SendJogCommand(string command)
        {
            if (streamingState == StreamingState.Jogging || grblState.State == GrblStates.Jog)
            {
                while (Comms.com.OutCount != 0) ;
                //Application.DoEvents(); //??
                Comms.com.WriteByte(GrblConstants.CMD_JOG_CANCEL); // Cancel current jog
            }
            streamingState = StreamingState.Jogging;
            Comms.com.WriteCommand(command);
        }

        public void SendRTCommand(string command)
        {
            Comms.com.WriteByte((byte)command[0]);
        }

        private void SendCommand(string command)
        {
            model.Message = "";

            if (command.Length == 1)
                SendRTCommand(command);
            else if (streamingState == StreamingState.Idle || streamingState == StreamingState.NoFile || streamingState == StreamingState.ToolChange || command == GrblConstants.CMD_UNLOCK)
            {
                //                command = command.ToUpper();
                try
                {
                    GCode.File.Parser.ParseBlock(ref command, true);
                    GCode.File.Commands.Enqueue(command);
                    if (streamingState != StreamingState.SendMDI)
                    {
                        streamingState = StreamingState.SendMDI;
                        ResponseReceived("go");
                    }
                }
                catch
                {
                }
            }
        }

        public void RewindFile()
        {
            job.Complete = false;

            if (GCode.File.IsLoaded)
            {
                using (new UIUtils.WaitCursor())
                {
                    btnStart.IsEnabled = false;

   //                 grdGCode.DataContext = null;

                    GCode.File.ClearStatus();

                    //                  grdGCode.DataContext = GCode.File.Data.DefaultView;
                    model.ScrollPosition = 0;
                    job.CurrLine = job.PendingLine = job.ACKPending = 0;
                    job.PgmEndLine = GCode.File.Blocks - 1;

                    btnStart.IsEnabled = true;
                }
            }
        }

        private void SetStreamingHandler(StreamingHandler handler)
        {
            if (handler == StreamingHandler.Previous)
                streamingHandler = streamingHandlers[(int)StreamingHandler.Previous];
            else if (streamingHandler.Handler != handler)
            {
                if (handler == StreamingHandler.Idle)
                    streamingHandler = streamingHandlers[(int)StreamingHandler.Previous] = streamingHandlers[(int)StreamingHandler.Idle];
                else {
                    streamingHandlers[(int)StreamingHandler.Previous] = streamingHandler;
                    streamingHandler = streamingHandlers[(int)handler];
                }
            }
        }

        public bool StreamingToolChange(StreamingState newState, bool always)
        {
            switch (newState)
            {
                case StreamingState.ToolChange:
                    btnStart.IsEnabled = true;
                    btnHold.IsEnabled = false;
                    break;

                case StreamingState.Send:
                case StreamingState.Error:
                    SetStreamingHandler(StreamingHandler.Previous);
                    break;

                case StreamingState.Stop:
                    SetStreamingHandler(StreamingHandler.Idle);
                    break;
            }

            if (streamingHandler.Handler != StreamingHandler.ToolChange)
                return streamingHandler.Call(newState, true);
            else
                return true;
        }

        public bool StreamingFeedHold(StreamingState newState, bool always)
        {
            bool changed = streamingState != newState;

            if (always || changed)
            {
                switch (newState)
                {
                    case StreamingState.FeedHold:
                        btnStart.IsEnabled = true;
                        btnHold.IsEnabled = false;
                        btnStop.IsEnabled = model.IsJobRunning;
                        break;

                    case StreamingState.Send:
                    case StreamingState.Error:
                    case StreamingState.Idle:
                        SetStreamingHandler(StreamingHandler.Previous);
                        break;

                    case StreamingState.Stop:
                        SetStreamingHandler(StreamingHandler.Idle);
                        break;
                }
            }

            if (streamingHandler.Handler != StreamingHandler.FeedHold)
                return streamingHandler.Call(newState, true);
            else if (changed)
            {
                model.StreamingState = streamingState = newState;
                StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
            }

            return true;
        }

        public bool StreamingSendFile(StreamingState newState, bool always)
        {
            bool changed = streamingState != newState;

            if (changed || always)
            {
                switch (newState)
                {
                    case StreamingState.Idle:
                        if(streamingState == StreamingState.Error)
                        {
                            btnStart.IsEnabled = !GrblSettings.IsGrblHAL; // BAD! ?
                            btnHold.IsEnabled = false;
                            btnStop.IsEnabled = true;
                            SetStreamingHandler(StreamingHandler.AwaitAction);
                        }
                        else
                            changed = false; // ignore
                        break;

                    case StreamingState.Send:
                        btnStart.IsEnabled = false;
                        btnHold.IsEnabled = true;
                        btnStop.IsEnabled = true;
                        btnRewind.IsEnabled = false;
                        break;

                    case StreamingState.Error:
                    case StreamingState.Halted:
                        btnHold.IsEnabled = false;
                        break;

                    case StreamingState.FeedHold:
                        SetStreamingHandler(StreamingHandler.FeedHold);
                        break;

                    case StreamingState.ToolChange:
                        SetStreamingHandler(StreamingHandler.ToolChange);
                        break;

                    case StreamingState.JobFinished:
                        if (grblState.State == GrblStates.Idle || grblState.State == GrblStates.Check)
                            newState = StreamingState.Idle;
                        job.Complete = true;
                        job.ACKPending = job.CurrLine = 0;
                        SetStreamingHandler(StreamingHandler.AwaitIdle);
                        break;

                    case StreamingState.Stop:
                        if (GrblSettings.IsGrblHAL)
                            SetStreamingHandler(StreamingHandler.Idle);
                        else
                        {
                            newState = StreamingState.Paused;
                            SetStreamingHandler(StreamingHandler.AwaitAction);
                        }
                        break;
                }
            }

            if (streamingHandler.Handler != StreamingHandler.SendFile)
                return streamingHandler.Call(newState, true);
            else if (changed)
            {
                model.StreamingState = streamingState = newState;
                StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
            }

            return true;
        }

        public bool StreamingAwaitAction(StreamingState newState, bool always)
        {
            bool changed = streamingState != newState || newState == StreamingState.Idle;

            if (changed || always)
            {
                switch (newState)
                {
                    case StreamingState.Idle:
                        btnStart.IsEnabled = !GrblSettings.IsGrblHAL;
                        break;

                    case StreamingState.Stop:
                        if (GrblSettings.IsGrblHAL && !model.GrblReset)
                            Comms.com.WriteByte(GrblConstants.CMD_STOP);
                        newState = StreamingState.Idle;
                        SetStreamingHandler(StreamingHandler.AwaitIdle);
                        break;

                    case StreamingState.Paused:
                        btnHold.IsEnabled = false;
                        btnStop.IsEnabled = true;
                        break;

                    case StreamingState.Send:
                        SetStreamingHandler(StreamingHandler.SendFile);
                        SendNextLine();
                        break;

                    case StreamingState.JobFinished:
                        SetStreamingHandler(StreamingHandler.SendFile);
                        break;
                }
            }

            if (streamingHandler.Handler != StreamingHandler.AwaitAction)
                return streamingHandler.Call(newState, true);
            else if (changed)
            {
                model.StreamingState = streamingState = newState;
                StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
            }

            return true;
        }

        public bool StreamingAwaitIdle(StreamingState newState, bool always)
        {
            bool changed = streamingState != newState || newState == StreamingState.Idle;

            if (changed || always)
            {
                switch (newState)
                {
                    case StreamingState.Idle:
                        model.RunTime = JobTimer.RunTime;
                        JobTimer.Stop();
                        RewindFile();
                        SetStreamingHandler(StreamingHandler.Idle);
                        break;

                    case StreamingState.Error:
                    case StreamingState.Halted:
                        btnStart.IsEnabled = !GrblSettings.IsGrblHAL;
                        btnHold.IsEnabled = false;
                        btnStop.IsEnabled = true;
                        break;

                    case StreamingState.Send:
                        btnStart.IsEnabled = false;
                        btnHold.IsEnabled = true;
                        btnStop.IsEnabled = true;
                        btnRewind.IsEnabled = false;
                        break;

                    case StreamingState.FeedHold:
                        SetStreamingHandler(StreamingHandler.FeedHold);
                        break;

                    case StreamingState.Stop:
                        SetStreamingHandler(StreamingHandler.Idle);
                        break;
                }
            }

            if (streamingHandler.Handler != StreamingHandler.AwaitIdle)
                return streamingHandler.Call(newState, true);
            else if (changed)
            {
                model.StreamingState = streamingState = newState;
                StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
            }

            return true;
        }

        public bool StreamingIdle(StreamingState newState, bool always)
        {
            bool changed = streamingState != newState || newState == StreamingState.Idle;

            if (changed || always)
            {
                switch (newState)
                {
                    case StreamingState.Disabled:
                        IsEnabled = false;
                        break;

                    case StreamingState.Idle:
                    case StreamingState.NoFile:
                        IsEnabled = !grblState.MPG;
                        btnStart.IsEnabled = GCode.File.IsLoaded;
                        btnStop.IsEnabled = false;
                        btnHold.IsEnabled = !grblState.MPG;
                        btnRewind.IsEnabled = !grblState.MPG && GCode.File.IsLoaded && job.CurrLine != 0;
                        model.IsJobRunning = false;
                        break;

                    case StreamingState.Send:
                        if (!string.IsNullOrEmpty(model.FileName) && !grblState.MPG)
                            model.IsJobRunning = true;
                        if (JobTimer.IsRunning)
                            SetStreamingHandler(StreamingHandler.SendFile);
                        else
                            btnStop.IsEnabled = true;
                        break;

                    case StreamingState.Start: // Streaming from SD Card
                        job.IsSDFile = true;
                        JobTimer.Start();
                        break;

                    case StreamingState.Error:
                    case StreamingState.Halted:
                        btnStart.IsEnabled = !grblState.MPG;
                        btnHold.IsEnabled = false;
                        btnStop.IsEnabled = !grblState.MPG;
                        break;

                    case StreamingState.FeedHold:
                        SetStreamingHandler(StreamingHandler.FeedHold);
                        break;

                    case StreamingState.ToolChange:
                        SetStreamingHandler(StreamingHandler.ToolChange);
                        break;

                    case StreamingState.Stop:
                        btnHold.IsEnabled = !(grblState.MPG || grblState.State == GrblStates.Alarm);
                        btnStart.IsEnabled = btnHold.IsEnabled && GCode.File.IsLoaded; //!GrblSettings.IsGrblHAL;
                        btnStop.IsEnabled = false;
                        btnRewind.IsEnabled = false;
                        job.IsSDFile = false;
                        model.IsJobRunning = false;
                        if (!grblState.MPG)
                        {
                            if (GrblSettings.IsGrblHAL && !model.GrblReset)
                                Comms.com.WriteByte(GrblConstants.CMD_STOP);
                        }
                        if (JobTimer.IsRunning)
                        {
                            always = false;
                            model.StreamingState = streamingState = streamingState == StreamingState.Error ? StreamingState.Idle : newState;
                            SetStreamingHandler(StreamingHandler.AwaitIdle);
                        } else if(grblState.State != GrblStates.Alarm)
                            return streamingHandler.Call(StreamingState.Idle, true);
                        break;
                }
            }

            if (streamingHandler.Handler != StreamingHandler.Idle)
                return streamingHandler.Call(newState, always);
            else if (changed)
            {
                model.StreamingState = streamingState = newState;
                StreamingStateChanged?.Invoke(streamingState, grblState.MPG);
            }

            return true;
        }

        void GrblStateChanged(GrblState newstate)
        {
            switch (newstate.State)
            {
                case GrblStates.Idle:
                    streamingHandler.Call(StreamingState.Idle, false);
                    break;

                case GrblStates.Jog:
                    model.IsJobRunning = true;
                    break;

                //case GrblStates.Check:
                //    streamingHandler.Call(StreamingState.Send, false);
                //    break;

                case GrblStates.Run:
                    if (JobTimer.IsPaused)
                        JobTimer.Pause = false;
                    if (model.StreamingState != StreamingState.Error)
                        streamingHandler.Call(StreamingState.Send, false);
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
                    streamingHandler.Call(StreamingState.ToolChange, false);
                    if (!grblState.MPG)
                        Comms.com.WriteByte(GrblConstants.CMD_TOOL_ACK);
                    break;

                case GrblStates.Hold:
                    streamingHandler.Call(StreamingState.FeedHold, false);
                    break;

                case GrblStates.Door:
                    if (newstate.Substate > 0)
                    {
                        if (streamingState == StreamingState.Send)
                            streamingHandler.Call(StreamingState.FeedHold, false);
                    }
                    break;
            }

            grblState.State = newstate.State;
            grblState.Substate = newstate.Substate;
            grblState.MPG = newstate.MPG;
        }

        private void ResponseReceived(string response)
        {
            if (streamingHandler.Count)
            {
                if (job.ACKPending > 0)
                    job.ACKPending--;

                if (!job.IsSDFile && (string)GCode.File.Data.Rows[job.PendingLine]["Sent"] == "*")
                    job.serialUsed = Math.Max(0, job.serialUsed - (int)GCode.File.Data.Rows[job.PendingLine]["Length"]);

                //if (streamingState == StreamingState.Send || streamingState == StreamingState.Paused)
                //{
                    bool isError = response.StartsWith("error");

                    if (!job.IsSDFile)
                    {
                        GCode.File.Data.Rows[job.PendingLine]["Sent"] = response;

                        if (job.PendingLine > 5)
                        {
                            if (grblState.State != GrblStates.Check || isError || (job.PendingLine % 50) == 0)
                               model.ScrollPosition = job.PendingLine - 5;
                        }
                    }
                    if (isError)
                        streamingHandler.Call(StreamingState.Error, true);
                    else if (job.PgmEndLine == job.PendingLine)
                        streamingHandler.Call(StreamingState.JobFinished, true);
                    else
                        SendNextLine();
                //}

                if (!job.Complete)
                    job.PendingLine++;
            }
            else if (response == "ok")
                missed++;

            switch (streamingState)
            {
                case StreamingState.Send:
                    if(response == "start")
                        SendNextLine();
                    break;

                case StreamingState.SendMDI:
                    if (GCode.File.Commands.Count > 0)
                        Comms.com.WriteCommand(GCode.File.Commands.Dequeue());
                    if (GCode.File.Commands.Count == 0)
                        streamingState = StreamingState.Idle;
                    break;

                case StreamingState.Reset:
                    Comms.com.WriteCommand(GrblConstants.CMD_UNLOCK);
                    streamingState = StreamingState.AwaitResetAck;
                    break;

                case StreamingState.AwaitResetAck:
                    streamingHandler.Call(GCode.File.IsLoaded ? StreamingState.Idle : StreamingState.NoFile, false);
                    break;
            }
        }

        void SendNextLine()
        {
            while (job.NextRow != null && job.serialUsed < (serialSize - (int)job.NextRow["Length"]))
            {
                if (GCode.File.Commands.Count > 0)
                    Comms.com.WriteCommand(GCode.File.Commands.Dequeue());
                else
                {
                    job.CurrentRow = job.NextRow;
                    string line = (string)job.CurrentRow["Data"]; //  GCodeUtils.StripSpaces((string)currentRow["Data"]);

                    job.CurrentRow["Sent"] = "*";
                    if (line == "%")
                    {
                        if (!(job.Started = !job.Started))
                            job.PgmEndLine = job.CurrLine;
                    }
                    else if ((bool)job.CurrentRow["ProgramEnd"])
                        job.PgmEndLine = job.CurrLine;
                    job.NextRow = job.PgmEndLine == job.CurrLine ? null : GCode.File.Data.Rows[++job.CurrLine];
                    //            ParseBlock(line + "\r");
                    job.serialUsed += (int)job.CurrentRow["Length"];
                    Comms.com.WriteString(line + '\r');
                }
                job.ACKPending++;

                if (!useBuffering)
                    break;
            }
        }
    }
}
