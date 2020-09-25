/*
 * ProbingViewModel.cs - part of CNC Probing library
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
using System.Collections.ObjectModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{

    class Probing
    {
        public const string Command = "G38.3";
    }

    public class ProbingViewModel : ViewModelBase
    {
        public enum CoordMode
        {
            G10 = 0,
            G92
        }

        private string _message = string.Empty, _tool = string.Empty, _instructions = string.Empty, _position = string.Empty, _probeProgram = string.Empty;
        private string _previewText = string.Empty;
        private double _tpHeight, _fHeight, _ProbeDiameter, _workpieceSizeX = 0d, _workpieceSizeY = 0d, _workpieceHeight = 0d;
        private double _latchDistance, _latchFeedRate;
        private double _probeDistance, _probeFeedRate;
        private double _rapidsFeedRate;
        private double _offset, _xyClearance, _depth;
        private double _tloReferenceOffset = double.NaN;

        private bool _canProbe = false, _isComplete = false, _isSuccess = false, _probeZ = false, _useFixture = false;
        private bool _hasToolTable = false, _hasCs9 = false, _addAction = false, _isPaused = false, _isCorner = false;
        private bool isCancelled = false, wasZselected = false, _referenceToolOffset = true, _workpieceLockXY = true;
        private bool _enableOffset = true, _enableXYD = true, _enableProbeDiameter = true;
        private bool _enableFixtureHeight = true, _enableTouchPlateHeight = true, _enablePreview = false;
        private GrblViewModel _grblmodel = null;
        private List<string> _program = new List<string>();
        private List<Position> _positions = new List<Position>();
        private List<Position> _machine = new List<Position>();
        private CoordMode _cmode = CoordMode.G10;
        private Edge _edge = Edge.None;
        private Center _center = Center.None;
        private int _coordinateSystem = 0, _passes = 1;
        private ProbingType _probingType = ProbingType.None;
        private ProbingProfile _profile;
        private CancellationToken cancellationToken = new CancellationToken();

        public Program Program;

        public ProbingViewModel (GrblViewModel grblmodel, ProbingProfiles profile)
        {
            Grbl = grblmodel;

//            Execute = new ActionCommand<bool>(ExecuteProgram);

            Program = new Program(this);

            Profiles = profile.Profiles;
            Profile = profile.Profiles[0];

            HeightMap.PropertyChanged += HeightMap_PropertyChanged;
        }

        private void HeightMap_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HeightMapViewModel.HasHeightMap))
                HeightMap.CanApply = HeightMap.HasHeightMap && !HeightMapApplied && Grbl.IsFileLoaded;
        }

//        private ICommand Execute { get;  set; }

        public bool RemoveLastPosition()
        {
            bool ok;
            if ((ok = _positions.Count > 0))
                _positions.RemoveAt(_positions.Count - 1);

            return ok;
        }

        public void Cancel ()
        {
        }

        public bool WaitForResponse(string command)
        {
            bool? res = null;

            if (Grbl.ResponseLogVerbose)
                Grbl.ResponseLog.Add(command);

            var t = new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => Grbl.OnResponseReceived += a,
                a => Grbl.OnResponseReceived -= a,
                5000, () => Grbl.ExecuteCommand(command));
            }); t.Start();

            while (res == null)
                EventUtils.DoEvents();

            return res == true;
        }

        public bool WaitForIdle(string command)
        {
            bool? res = null;

            if (Grbl.ResponseLogVerbose)
                Grbl.ResponseLog.Add(command);

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => Grbl.OnResponseReceived += a,
                a => Grbl.OnResponseReceived -= a,
                1000, () => Grbl.ExecuteCommand(command));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            if (res == true)
                res = null;

            while (res == null)
            {
                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    null,
                    a => Grbl.OnResponseReceived += a,
                    a => Grbl.OnResponseReceived -= a,
                    5000);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                if (Grbl.GrblState.State != GrblStates.Idle)
                    res = null;
            }

            return res == true;
        }

        public bool GotoMachinePosition(Position pos, AxisFlags axisflags)
        {
            bool? res = null;
            bool wait = true;

            string command = "G53" + RapidCommand + pos.ToString(axisflags);

            Comms.com.PurgeQueue();

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => Grbl.OnResponseReceived += a,
                a => Grbl.OnResponseReceived -= a,
                1000, () => Grbl.ExecuteCommand(command));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            if (res == true) {

                while (wait && !isCancelled)
                {
                    res = null;

                    new Thread(() =>
                    {
                        res = WaitFor.SingleEvent<string>(
                        cancellationToken,
                        null,
                        a => Grbl.OnRealtimeStatusProcessed += a,
                        a => Grbl.OnRealtimeStatusProcessed -= a,
                        400, () => Comms.com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT)));
                    }).Start();

                    while (res == null)
                        EventUtils.DoEvents();

                    wait = res != true;

                    int i = 0, axes = (int)axisflags;
                    while (axes != 0 && !wait)
                    {
                        if ((axes & 0x01) != 0)
                            wait = Math.Abs(pos.Values[i] - Grbl.MachinePosition.Values[i]) >= 0.003d; // use step resolution plus some?
                        i++; axes >>= 1;
                    }

                    if (wait)
                        Thread.Sleep(200); // needed?
                }
            }

            return isCancelled ? false : !wait;
        }

        public bool ValidateInput ()
        {
            ClearErrors();

            if(XYClearance > ProbeDistance)
            {
                SetError(nameof(XYClearance), "Probing distance must be larger than XY Clearance.");
                SetError(nameof(ProbeDistance), "Probing distance must be larger than XY Clearance.");
            }

            return !HasErrors;
        }

        public void ClearExeStatus ()
        {
            _isComplete = _isSuccess = false;
        }

        public GrblViewModel Grbl { get { return _grblmodel; } private set { _grblmodel = value; OnPropertyChanged(); } }
        public Position StartPosition { get; private set; } = new Position();
        public HeightMapViewModel HeightMap { get; private set; } = new HeightMapViewModel();
        public ObservableCollection<CoordinateSystem> CoordinateSystems { get; private set; } = new ObservableCollection<CoordinateSystem>();
        public ObservableCollection<ProbingProfile> Profiles { get; private set; }
        public ProbingProfile Profile
        {
            get { return _profile; }
            set
            {
                _profile = value;
                RapidsFeedRate = _profile.RapidsFeedRate;
                ProbeFeedRate = _profile.ProbeFeedRate;
                LatchFeedRate = _profile.LatchFeedRate;
                ProbeDistance = _profile.ProbeDistance;
                LatchDistance = _profile.LatchDistance;
                ProbeDiameter = _profile.ProbeDiameter;
                Offset = _profile.Offset;
                XYClearance = _profile.XYClearance;
                Depth = _profile.Depth;
                TouchPlateHeight = _profile.TouchPlateHeight;
                FixtureHeight = _profile.FixtureHeight;
            }
        }

        public ProbingType ProbingType { get { return _probingType; } set { _probingType = value; OnPropertyChanged(); OnPropertyChanged(nameof(TouchPlateHeightEnable)); } }
        public string FastProbe { get { return string.Format(Probing.Command + "F{0}", ProbeFeedRate.ToInvariantString()); } }
        public string SlowProbe { get { return string.Format(Probing.Command + "F{0}", LatchFeedRate.ToInvariantString()); } }
        public string Instructions { get { return _instructions; } set { _instructions = value; OnPropertyChanged(); } }
        public string Position { get { return _position; } set { if (_position != value) { _position = value; OnPropertyChanged(); }  } }

        public int Passes { get { return _passes; } set { _passes = value; OnPropertyChanged(); } }
        public int CoordinateSystem { get { return _coordinateSystem; } set { _coordinateSystem = value; OnPropertyChanged(); } }
        public double ProbeFeedRate { get { return _probeFeedRate; } set { _probeFeedRate = value; OnPropertyChanged(); } }
        public double ProbeDistance { get { return _probeDistance; } set { _probeDistance = value; OnPropertyChanged(); } }
        public double ProbeDiameter { get { return _ProbeDiameter; } set { _ProbeDiameter = value; OnPropertyChanged(); } }
        public double LatchDistance { get { return _latchDistance; } set { _latchDistance = value; OnPropertyChanged(); } }
        public double LatchFeedRate { get { return _latchFeedRate; } set { _latchFeedRate = value; OnPropertyChanged(); } }
        public double RapidsFeedRate { get { return _rapidsFeedRate; } set { _rapidsFeedRate = value; OnPropertyChanged(); } }
        public double TouchPlateHeight { get { return _tpHeight; } set { _tpHeight = value; OnPropertyChanged(); } }
        public double FixtureHeight { get { return _fHeight; } set { _fHeight = value; OnPropertyChanged(); } }
        public bool CanProbe { get { return _canProbe; } set { _canProbe = value; OnPropertyChanged(); } }
        public bool IsCompleted { get { return _isComplete; } set { _isComplete = value; OnPropertyChanged(); } }
        public bool IsSuccess { get { return _isSuccess; } set { _isSuccess = value; OnPropertyChanged(); } }
        public bool IsPaused { get { return _isPaused; } set { _isPaused = value; OnPropertyChanged(); } }
        public bool ProbeZ { get { return _probeZ; } set { _probeZ = value; OnPropertyChanged(); OnPropertyChanged(nameof(TouchPlateHeightEnable)); } }
        public string Tool { get { return _tool; } set { _tool = value; OnPropertyChanged(); } }
        public bool ProbeFixture { get { return _useFixture; } set { _useFixture = value; OnPropertyChanged(); OnPropertyChanged(nameof(FixtureHeightEnable)); OnPropertyChanged(nameof(TouchPlateHeightEnable)); if (_useFixture) AddAction = false; } }
        public bool HasToolTable { get { return _hasToolTable; } set { _hasToolTable = value; OnPropertyChanged(); } }
        public bool HasCoordinateSystem9 { get { return _hasCs9; } set { _hasCs9 = value; OnPropertyChanged(); } }
        public bool ReferenceToolOffset { get { return _referenceToolOffset; } set { _referenceToolOffset = value; OnPropertyChanged(); } }
        public double TloReference { get { return _tloReferenceOffset; } set { _tloReferenceOffset = value; OnPropertyChanged(); } }
        public bool AddAction { get { return _addAction; } set { _addAction = value; OnPropertyChanged(); } }
  
        public bool HeightMapApplied
        {
            get { return GCode.File.HeightMapApplied; }
            set {
                if (GCode.File.HeightMapApplied != value) {
                    GCode.File.HeightMapApplied = value;
                    OnPropertyChanged();
                }
                HeightMap.CanApply = !GCode.File.HeightMapApplied;
            }
        }
//        public List<string> Program { get { return _program;  } }
        public List<Position> Positions { get { return _positions; } }
        public List<Position> Machine { get { return _machine; } }
        public double XYClearance { get { return _xyClearance; } set { _xyClearance = value; OnPropertyChanged(); } }
        public double Offset { get { return _offset; } set { _offset = value; OnPropertyChanged(); } }
        public bool OffsetEnable { get { return _enableOffset && _isCorner; } set { _enableOffset = value;  OnPropertyChanged(); } }
        public double Depth { get { return _depth; } set { _depth = value; OnPropertyChanged(); } }
        public string RapidCommand { get { return RapidsFeedRate == 0d ? "G0" : "G1F" + RapidsFeedRate.ToInvariantString(); } }
        public string ProbeProgram { get { return Program.ToString().Replace("G53", string.Empty); } }
        public bool XYDEnable { get { return _enableXYD; } set { _enableXYD = value; OnPropertyChanged(); } }
        public bool ProbeDiameterEnable { get { return _enableProbeDiameter && (_probingType == ProbingType.EdgeFinderInternal || _probingType == ProbingType.EdgeFinderExternal ? _edge != Edge.Z : true); } set { _enableProbeDiameter = value; OnPropertyChanged(); } }
        public bool FixtureHeightEnable { get { return _enableFixtureHeight && _useFixture; } set { _enableFixtureHeight = value; OnPropertyChanged(); } }
        public bool TouchPlateHeightEnable
        {
            get {
                return _enableTouchPlateHeight && (_probingType == ProbingType.ToolLength ? !_useFixture : _probeZ || _probingType == ProbingType.HeightMap || _probingType == ProbingType.None);
            }
            set { _enableTouchPlateHeight = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value; OnPropertyChanged();
                if (!string.IsNullOrEmpty(_message) && Grbl.ResponseLogVerbose)
                    Grbl.ResponseLog.Add(_message);
                Program.Silent = true;
                Grbl.Message = _message;
                Program.Silent = false;
            }
        }
        public CoordMode CoordinateMode { get { return _cmode; } set { _cmode = value; OnPropertyChanged(); } }

        public bool IsCoordinateModeG92 { get { return _cmode == CoordMode.G92; } set { _cmode = value ? CoordMode.G92 : CoordMode.G10; OnPropertyChanged(); OnPropertyChanged(nameof(CoordinateMode)); } }

        public Edge ProbeEdge
        {
            get { return _edge; }
            set
            {
                if(value == Edge.Z)
                {
                    if (!_probeZ && !ProbeZ)
                    {
                        wasZselected = true;
                        ProbeZ = true;
                    }
                }
                else if(_edge == Edge.Z)
                {
                    if (wasZselected)
                    {
                        wasZselected = false;
                        ProbeZ = false;
                    }
                }
                _edge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProbeDiameterEnable));
                ProbeCorner = _edge == Edge.A || _edge == Edge.B || _edge == Edge.C || _edge == Edge.D;
            }
        }

        public bool PreviewEnable
        {
            get { return _enablePreview; }
            set
            {
                if (_enablePreview != value)
                {
                    _enablePreview = value;
                    OnPropertyChanged();
                    if (!_enablePreview)
                        PreviewText = string.Empty;
                }
            }
        }
        public string PreviewText { get { return _previewText; } set { _previewText = value; OnPropertyChanged(); } }

        public bool ProbeCorner { get { return _isCorner; } set { _isCorner = value; OnPropertyChanged(); OnPropertyChanged(nameof(OffsetEnable)); } }
        public Center ProbeCenter { get { return _center; } set { _center = value; OnPropertyChanged(); } }
        public bool WorkpiecLockXY
        {
            get { return _workpieceLockXY; }
            set {
                _workpieceLockXY = value;
                OnPropertyChanged();
                if (_workpieceLockXY && _workpieceSizeY != _workpieceSizeX)
                {
                    _workpieceSizeY = _workpieceSizeX;
                    OnPropertyChanged(nameof(WorkpieceSizeY));
                }
            }
        }
        public double WorkpieceSizeX
        {
            get { return _workpieceSizeX; }
            set {
                _workpieceSizeX = value;
                OnPropertyChanged();
                if (_workpieceLockXY)
                {
                    _workpieceSizeY = value;
                    OnPropertyChanged(nameof(WorkpieceSizeY));
                }
            }
        }
        public double WorkpieceSizeY
        {
            get { return _workpieceSizeY; }
            set
            {
                _workpieceSizeY = value;
                OnPropertyChanged();
                if (_workpieceLockXY)
                {
                    _workpieceSizeX = value;
                    OnPropertyChanged(nameof(WorkpieceSizeX));
                }
            }
        }
        public double WorkpieceHeight { get { return _workpieceHeight; } set { _workpieceHeight = value; OnPropertyChanged(); } }

    }
}
