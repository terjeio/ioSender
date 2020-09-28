/*
 * Program.cs - part of CNC Probing library
 *
 * v0.27 / 2020-09-26 / Io Engineering (Terje Io)

 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Threading;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{
    public class Program
    {
        private GrblViewModel Grbl = null;
        private List<string> _program = new List<string>();
        ProbingViewModel probing;
        private bool probeProtect = false;
        private volatile bool _isComplete = false, isRunning = false, isProbing = false, hasPause = false, probeOnCycleStart = false;
        private int step = 0;
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
                        ResponseReceived("fail");
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

        public bool Init()
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
                1000, () => Comms.com.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STATUS_REPORT_ALL : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT)));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            Grbl.Poller.SetState(AppConfig.Settings.Base.PollInterval);

            if (Grbl.GrblState.State == GrblStates.Alarm)
            {
                probing.Message = GrblAlarms.GetMessage(Grbl.GrblState.Substate.ToString());
                res = false;
            }

            if (res == true && Grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected))
            {
                probing.Message = "Probing failed, probe is not connected";
                res = false;
            }

            if (res == true && Grbl.Signals.Value.HasFlag(Signals.Probe))
            {
                probing.Message = "Probing failed, probe signal is asserted";
                res = false;
            }

            if (res == true && !(Grbl.GrblState.State == GrblStates.Idle || Grbl.GrblState.State == GrblStates.Tool))
            {
                probing.Message = "Probing failed, Grbl is not in idle or tool changing state";
                res = false;
            }

            if (res == true && !Grbl.IsMachinePositionKnown)
            {
                probing.Message = "Probing failed, could not establish current machine position";
                res = false;
            }

            probing.StartPosition.Set(probing.Grbl.MachinePosition);

            hasPause = probeOnCycleStart = false;
            _program.Clear();

            //if (res != true) // Reenable status polling if init fails 
            //    Grbl.Poller.SetState(AppConfig.Settings.Base.PollInterval);

            return res == true;
        }

        public void AddProbingAction(AxisFlags axis, bool negative)
        {
            var axisLetter = axis.ToString();
            _program.Add(probing.FastProbe + axisLetter + (negative ? "-" : "") + probing.ProbeDistance.ToInvariantString());
            if (probing.LatchDistance > 0d)
            {
                _program.Add("!" + probing.RapidCommand + axisLetter + (negative ? "" : "-") + probing.LatchDistance.ToInvariantString());
                _program.Add(probing.SlowProbe + axisLetter + (negative ? "-" : "") + probing.ProbeDistance.ToInvariantString());
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
            Comms.com.WriteByte(GrblConstants.CMD_STOP);
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
            }
            _isComplete = true;
            probing.Message = message;
        }

        public bool Execute(bool go)
        {
            _isComplete = isProbing = false;

            probing.ClearExeStatus();

            if (_program.Count > 0)
            {
                step = 0;
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
                    probing.Message = "Probing...";

                Grbl.ExecuteCommand(_program[step]);

                while(!_isComplete)
                    EventUtils.DoEvents();
            }

            return probing.IsSuccess;
        }

        private void ResponseReceived(string response)
        {
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
                        isProbing = _program[step].Contains("G38");
                        Grbl.ExecuteCommand(_program[step]);
                    }
                }
            }

            if (step == _program.Count || response != "ok")
            {
                probing.IsSuccess = step == _program.Count && response == "ok";
                if (!probing.IsSuccess)
                    End("Probing cancelled/failed" + (Grbl.GrblState.State == GrblStates.Alarm ? " (ALARM)" : ""));
                _isComplete = probing.IsCompleted = true;
//                Grbl.Poller.SetState(AppConfig.Settings.Base.PollInterval);
            }
        }

        public override string ToString()
        {
            return string.Join("\n", _program);
        }
    }
}
