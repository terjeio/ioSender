/*
 * Program.cs - part of CNC Probing library
 *
 * v0.36 / 2022-01-09 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2022, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Threading;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{
    public class LibStrings
    {
        static ResourceDictionary resource = new ResourceDictionary();

        public static string FindResource(string key)
        {
            if (resource.Source == null)
                try
                {
                    resource.Source = new Uri("pack://application:,,,/CNC.Controls.Probing;Component/LibStrings.xaml", UriKind.Absolute);
                }
                catch
                {
                }

            return resource.Source == null || !resource.Contains(key) ? string.Empty : (string)resource[key];
        }
    }

    public class Program
    {
        private GrblViewModel Grbl = null;
        private List<string> _program = new List<string>();
        ProbingViewModel probing;
        private bool probeProtect = false;
        private volatile bool probeAsserted = false, probeConnected = false;
        private volatile bool _isComplete = false, isRunning = false, isProbing = false, hasPause = false, probeOnCycleStart = false;
        private int step = 0;
        private volatile string cmd_response;
        private CancellationToken cancellationToken = new CancellationToken();

        public bool Silent = false;

        public Program(ProbingViewModel model)
        {
            Grbl = model.Grbl;
            probing = model;
        }

        public bool IsCancelled { get; set; }

        private void Grbl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GrblViewModel.IsProbeSuccess):
                    if (Grbl.IsProbeSuccess)
                        probing.Positions.Add(new Position(Grbl.ProbePosition));
                    else
                        ResponseReceived("fail");
                    break;

                case nameof(GrblViewModel.GrblState):
                    if (Grbl.GrblState.State == GrblStates.Alarm)
                        ResponseReceived("alarm");
                    break;

                case nameof(GrblViewModel.Signals):
                    var model = sender as GrblViewModel;
                    if (model.Signals.Value.HasFlag(Signals.CycleStart)) {
                        if (probing.IsPaused && probeOnCycleStart)
                        {
                            probeOnCycleStart = false;
                            probing.IsPaused = false;
                        }
                        //TODO: add start as well?
                    }
                    if (probeProtect && model.Signals.Value.HasFlag(Signals.Probe) && model.GrblState.State == GrblStates.Run & model.GrblState.Substate != 2)
                    {
                        Comms.com.WriteByte(GrblConstants.CMD_STOP);
                        ResponseReceived("fail");
                    }
                    break;

                case nameof(GrblViewModel.Message):
                    if (!Silent)
                        probing.Message = Grbl.Message;
                    break;

                case nameof(GrblViewModel.GrblReset):
                    ResponseReceived("fail");
                    break;
            }
        }

        private void Probing_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProbingViewModel.IsPaused) && hasPause && isRunning)
            {
                if (!probing.IsPaused)
                    ResponseReceived("ok");
                else if (Grbl.ResponseLogVerbose)
                    Grbl.ResponseLog.Add("PM:paused");
            }
        }

        public void Clear()
        {
            _program.Clear();
        }

        private void probeCheck(string data)
        {
            if (data.StartsWith("<"))
            {
                int pos;
                if ((probeAsserted = (pos = data.IndexOf("|Pn:")) > 0))
                {
                    string[] elements = (data.Substring(pos + 4).TrimEnd('>') + "|").Split('|');
                    probeAsserted = elements[0].Contains("P");
                    probeConnected = !elements[0].Contains("O");
                }
            }
        }

        public bool IsProbeReady(bool get_status = true)
        {
            bool ok = true;

            if (get_status) // Check for probe connected and not asserted in next real-time report
            {
                bool? res = null;
                CancellationToken cancellationToken = new CancellationToken();

                probeAsserted = probeConnected = true;

                // Timeout wait for skipping first report as this may have probe asserted true
                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    null,
                    a => Grbl.OnGrblReset += a,
                    a => Grbl.OnGrblReset -= a,
                    AppConfig.Settings.Base.PollInterval * 2 + 50);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                res = null;

                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    probeCheck,
                    a => Grbl.OnResponseReceived += a,
                    a => Grbl.OnResponseReceived -= a,
                    AppConfig.Settings.Base.PollInterval * 5);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();
            }
            else
            {
                probeAsserted = Grbl.Signals.Value.HasFlag(Signals.Probe);
                probeConnected = !Grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected);
            }

            if (ok && !probeConnected)
            {
                probing.Message = LibStrings.FindResource("NoProbe");
                ok = false;
            }

            if (ok && probeAsserted)
            {
                probing.Message = LibStrings.FindResource("ProbeAsserted");
                ok = false;
            }

            return ok;
        }

        public bool Init(bool check_probe = true)
        {
            bool? res = null;

            IsCancelled = false;
            probing.Message = string.Empty;

            Grbl.Poller.SetState(0);  // Disable status polling during initialization

            // Clear error status if set
            if (Grbl.GrblError != 0)
            {
                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    null,
                    a => Grbl.OnResponseReceived += a,
                    a => Grbl.OnResponseReceived -= a,
                    1000, () => Grbl.ExecuteCommand(""));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                res = null;
            }

            // Get a status report in order to establish current machine position
            new Thread(() =>
            {
                res = WaitFor.SingleEvent<string>(
                cancellationToken,
                null,
                a => Grbl.OnResponseReceived += a,
                a => Grbl.OnResponseReceived -= a,
                AppConfig.Settings.Base.PollInterval * 5, () => Comms.com.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STATUS_REPORT_ALL : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT)));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            Grbl.Poller.SetState(AppConfig.Settings.Base.PollInterval);

            if (Grbl.GrblState.State == GrblStates.Alarm)
            {
                probing.Message = GrblAlarms.GetMessage(Grbl.GrblState.Substate.ToString());
                res = false;
            }

            if (res == true && check_probe)
                res = IsProbeReady(false);

            if (res == true && !(Grbl.GrblState.State == GrblStates.Idle || Grbl.GrblState.State == GrblStates.Tool))
            {
                probing.Message = LibStrings.FindResource("FailedNotIdle");
                res = false;
            }

            if (res == true && !Grbl.IsMachinePositionKnown)
            {
                probing.Message = LibStrings.FindResource("FailedNoPos");
                res = false;
            }

            probing.StartPosition.Set(probing.Grbl.MachinePosition);

            hasPause = probeOnCycleStart = false;
            _program.Clear();

            return res == true;
        }

        public void AddProbingAction(AxisFlags axis, bool negative)
        {
            var axisLetter = axis.ToString();
            _program.Add(probing.FastProbe + axisLetter + (negative ? "-" : "") + probing.ProbeDistance.ToInvariantString());
            if (probing.LatchDistance > 0d)
            {
                _program.Add("!" + probing.RapidCommand + axisLetter + (negative ? "" : "-") + probing.LatchDistance.ToInvariantString());
                _program.Add(probing.SlowProbe + axisLetter + (negative ? "-" : "") + Math.Max((probing.LatchDistance * 1.5d), 2d / probing.Grbl.UnitFactor).ToInvariantString());
            }
        }

        public void AddSimulatedProbe(int p)
        {
            probing.IsSuccess = true;
            probing.Positions.Add(new Position(probing.StartPosition));
            probing.Positions.Add(new Position(probing.StartPosition));
        }

        public void Add(string cmd)
        {
            _program.Add(cmd);
        }

        public void AddRapid(string cmd)
        {
            _program.Add(probing.RapidCommand + cmd);
        }

        public void AddPause()
        {
            hasPause = true;
            _program.Add("pause");
        }

        public void AddRapidToMPos(string cmd)
        {
            _program.Add("G53" + probing.RapidCommand + cmd);
        }

        public void AddRapidToMPos(Position pos, AxisFlags axisflags)
        {
            _program.Add("G53" + probing.RapidCommand + pos.ToString(axisflags));
        }

        public void Cancel()
        {
            IsCancelled = true;
            Comms.com.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STOP : GrblConstants.CMD_RESET);
            if(!_isComplete)
                ResponseReceived("cancel");
        }

        public void End(string message)
        {
            if (isRunning)
            {
                Grbl.PropertyChanged -= Grbl_PropertyChanged;
                Grbl.OnCommandResponseReceived -= ResponseReceived;
                if (hasPause)
                    probing.PropertyChanged -= Probing_PropertyChanged;
                isRunning = Grbl.IsJobRunning = false;
                Grbl.ExecuteCommand(probing.DistanceMode == DistanceMode.Absolute ? "G90" : "G91");
            }
            if(!_isComplete || probing.IsSuccess)
                probing.Message = message;
            _isComplete = true;
        }

        public bool Execute(bool go)
        {
            _isComplete = isProbing = false;

            probing.ClearExeStatus();

            if (_program.Count > 0)
            {
                string response;

                step = 0;
                probing.CameraPositions = 0;
                probing.Positions.Clear();
                probing.Machine.Clear();

                Comms.com.PurgeQueue();

                if (!isRunning)
                {
                    isRunning = true;
                    probeProtect = GrblInfo.HasSimpleProbeProtect;

                    Grbl.OnCommandResponseReceived += ResponseReceived;
                    Grbl.PropertyChanged += Grbl_PropertyChanged;
                    if(hasPause)
                        probing.PropertyChanged += Probing_PropertyChanged;
                }

                Grbl.IsJobRunning = true;

                if (probing.Message == string.Empty)
                    probing.Message = LibStrings.FindResource("Probing");

                cmd_response = string.Empty;
                Grbl.ExecuteCommand(_program[step]);

                while (!_isComplete)
                {
                    EventUtils.DoEvents();

                    if(cmd_response != string.Empty)
                    {
                        response = cmd_response;
                        cmd_response = string.Empty;

                        if (Grbl.ResponseLogVerbose)
                            Grbl.ResponseLog.Add("PM:" + response);

                        if (response == "ok")
                        {
                            step++;
                            if (step < _program.Count)
                            {
                                int i;
                                //if ((i = _program[step].IndexOf('$')) > 0)
                                //{
                                //    string rp = _program[step].Substring(i, 2);
                                //    i = GrblInfo.AxisLetterToIndex(rp[1]);
                                //    double val = _positions[_positions.Count - 1].Values[i] + dbl.Parse(_program[step].Substring(i, 3));
                                //    _program[step] = _program[step] + val.ToInvariantString();
                                //}
                                if (_program[step].StartsWith("!"))
                                {
                                    isProbing = false;
                                    _program[step] = _program[step].Substring(1);
                                    probing.RemoveLastPosition();
                                }

                                if (_program[step] == "pause")
                                {
                                    probing.IsPaused = true;
                                    probeOnCycleStart = !probing.Grbl.Signals.Value.HasFlag(Signals.CycleStart);
                                }
                                else
                                {
                                    // This fails with a hang if probe is asserted when it should not be...
                                    //if ((isProbing = _program[step].Contains("G38")) && !IsProbeReady())
                                    //    response = "probe!";
                                    //else
                                        Grbl.ExecuteCommand(_program[step]);
                                }
                            }
                        }

                        if (step == _program.Count || response != "ok")
                        {
                            probing.IsSuccess = step == _program.Count && response == "ok";
                            if (!probing.IsSuccess && response != "probe!")
                                End(LibStrings.FindResource(Grbl.GrblState.State == GrblStates.Alarm ? "FailedAlarm" : "FailedCancelled"));
                            _isComplete = probing.IsCompleted = true;
                        }
                    }

                }

                if (probing.Message == LibStrings.FindResource("Probing"))
                    probing.Message = string.Empty;
            }

            return probing.IsSuccess;
        }

        private void ResponseReceived(string response)
        {
            if(cmd_response != "cancel")
                cmd_response = response;
        }

        public override string ToString()
        {
            return string.Join("\n", _program);
        }
    }
}
