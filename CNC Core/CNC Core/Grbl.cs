/*
 * Grbl.cs - part of CNC Controls library
 *
 * v0.40 / 2022-07-16 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
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

//#define USE_ASYNC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Data;
using System.Diagnostics;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using CNC.GCode;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CNC.Core
{
    public delegate void GCodePushHandler(string gcode, Action action);

    public class GrblConstants
    {
        public const byte
            CMD_EXIT = 0x03, // ctrl-C
            CMD_RESET = 0x18, // ctrl-X
            CMD_STOP = 0x19, // ctrl-Y
            CMD_STATUS_REPORT = 0x80,
            CMD_CYCLE_START = 0x81,
            CMD_FEED_HOLD = 0x82,
            CMD_GCODE_REPORT = 0x83,
            CMD_SAFETY_DOOR = 0x84,
            CMD_JOG_CANCEL = 0x85,
            CMD_STATUS_REPORT_ALL = 0x87,
            CMD_OPTIONAL_STOP_TOGGLE = 0x88,
            CMD_SINGLE_BLOCK_TOGGLE = 0x89,
            CMD_OVERRIDE_FAN0_TOGGLE = 0x8A,
            CMD_FEED_OVR_RESET = 0x90,
            CMD_FEED_OVR_COARSE_PLUS = 0x91,
            CMD_FEED_OVR_COARSE_MINUS = 0x92,
            CMD_FEED_OVR_FINE_PLUS = 0x93,
            CMD_FEED_OVR_FINE_MINUS = 0x94,
            CMD_RAPID_OVR_RESET = 0x95,
            CMD_RAPID_OVR_MEDIUM = 0x96,
            CMD_RAPID_OVR_LOW = 0x97,
            CMD_SPINDLE_OVR_RESET = 0x99,
            CMD_SPINDLE_OVR_COARSE_PLUS = 0x9A,
            CMD_SPINDLE_OVR_COARSE_MINUS = 0x9B,
            CMD_SPINDLE_OVR_FINE_PLUS = 0x9C,
            CMD_SPINDLE_OVR_FINE_MINUS = 0x9D,
            CMD_SPINDLE_OVR_STOP = 0x9E,
            CMD_COOLANT_FLOOD_OVR_TOGGLE = 0xA0,
            CMD_COOLANT_MIST_OVR_TOGGLE = 0xA1,
            CMD_PID_REPORT = 0xA2,
            CMD_TOOL_ACK = 0xA3,
            CMD_PROBE_CONNECTED_TOGGLE = 0xA4;

        public const string
            CMD_STATUS_REPORT_LEGACY = "?",
            CMD_CYCLE_START_LEGACY = "~",
            CMD_FEED_HOLD_LEGACY = "!",
            CMD_UNLOCK = "$X",
            CMD_HOMING = "$H",
            CMD_CHECK = "$C",
            CMD_GETSETTINGS = "$$",
            CMD_GETSETTINGS_ALL = "$+",
            CMD_GETPARSERSTATE = "$G",
            CMD_GETINFO = "$I",
            CMD_GETINFO_EXTENDED = "$I+",
            CMD_GETNGCPARAMETERS = "$#",
            CMD_GETSTARTUPLINES = "$N",
            CMD_GETSETTINGSDETAILS = "$ES",
            CMD_GETSETTINGSGROUPS = "$EG",
            CMD_GETALARMCODES = "$EA",
            CMD_GETERRORCODES = "$EE",
            CMD_PROGRAM_DEMARCATION = "%",
            CMD_SDCARD_MOUNT = "$FM",
            CMD_SDCARD_DIR = "$F",
            CMD_SDCARD_REWIND = "$FR",
            CMD_SDCARD_RUN = "$F=",
            CMD_SDCARD_UNLINK = "$FD=",
            CMD_SDCARD_DUMP = "$F<=",
            FORMAT_METRIC = "###0.000",
            FORMAT_IMPERIAL = "##0.0000",
            NO_TOOL = "None",
            SIGNALS = "XYZABCEPRDHSBTOW", // Keep in sync with Signals enum below!!
            THCSIGNALS = "AERTOVHDU"; // Keep in sync with THCSignals enum below!!

        public const int
            X_AXIS = 0,
            Y_AXIS = 1,
            Z_AXIS = 2,
            A_AXIS = 3,
            B_AXIS = 4,
            C_AXIS = 5;
    }

    public enum CameraMoveMode
    {
        XAxisFirst = 1,
        YAxisFirst = 2,
        BothAxes = 3
    }

    public enum GrblStates
    {
        Unknown = 0,
        Idle,
        Run,
        Tool,
        Hold,
        Home,
        Check,
        Jog,
        Alarm,
        Door,
        Sleep
    }

    public enum GrblMode
    {
        Normal = 0,
        Laser,
        Lathe
    }

    public enum GrblEncoderMode
    {
        Unknown = 0,
        FeedRate = 1,
        RapidRate = 2,
        SpindleRPM = 3
    }

    public enum GrblSetting
    {
        PulseMicroseconds = 0,
        StepperIdleLockTime = 1,
        StepInvertMask = 2,
        DirInvertMask = 3,
        InvertStepperEnable = 4,
        LimitPinsInvertMask = 5,
        InvertProbePin = 6,
        StatusReportMask = 10,
        JunctionDeviation = 11,
        ArcTolerance = 12,
        ReportInches = 13,
        SoftLimitsEnable = 20,
        HardLimitsEnable = 21,
        HomingEnable = 22,
        HomingDirMask = 23,
        HomingFeedRate = 24,
        HomingSeekRate = 25,
        HomingDebounceDelay = 26,
        HomingPulloff = 27,
        G73Retract = 28,
        PulseDelayMicroseconds = 29,
        RpmMax = 30,
        RpmMin = 31,
        Mode = 32, // enum GrblMode
        PWMFreq = 33,
        PWMOffValue = 34,
        PWMMinValue = 35,
        PWMMaxValue = 36,
        TravelResolutionBase = 100,
        MaxFeedRateBase = 110,
        AccelerationBase = 120,
        MaxTravelBase = 130,
        MotorCurrentBase = 140,
    }

    public enum grblHALSetting
    {
        PulseMicroseconds = 0,
        StepperIdleLockTime = 1,
        StepInvertMask = 2,
        DirInvertMask = 3,
        InvertStepperEnable = 4,
        LimitPinsInvertMask = 5,
        InvertProbePin = 6,
        StatusReportMask = 10,
        JunctionDeviation = 11,
        ArcTolerance = 12,
        ReportInches = 13,
        ControlInvertMask = 14, // Note: Used for detecting GrblHAL firmware
        CoolantInvertMask = 15,
        SpindleInvertMask = 16,
        ControlPullUpDisableMask = 17,
        LimitPullUpDisableMask = 18,
        ProbePullUpDisable = 19,
        SoftLimitsEnable = 20,
        HardLimitsEnable = 21,
        HomingEnable = 22,
        HomingDirMask = 23,
        HomingFeedRate = 24,
        HomingSeekRate = 25,
        HomingDebounceDelay = 26,
        HomingPulloff = 27,
        G73Retract = 28,
        PulseDelayMicroseconds = 29,
        RpmMax = 30,
        RpmMin = 31,
        Mode = 32, // enum GrblMode
        PWMFreq = 33,
        PWMOffValue = 34,
        PWMMinValue = 35,
        PWMMaxValue = 36,
        StepperDeenergizeMask = 37,
        SpindlePPR = 38,
        EnableLegacyRTCommands = 39,
        SoftLimitJogging = 40,
        HomingLocateCycles = 43,
        HomingCycle_1 = 44,
        HomingCycle_2 = 45,
        HomingCycle_3 = 46,
        HomingCycle_4 = 47,
        HomingCycle_5 = 48,
        HomingCycle_6 = 49,
        JogStepSpeed = 50,
        JogSlowSpeed = 51,
        JogFastSpeed = 52,
        JogStepDistance = 53,
        JogSlowDistance = 54,
        JogFastDistance = 55,
        // Per axis settings
        TravelResolutionBase = 100,
        MaxFeedRateBase = 110,
        AccelerationBase = 120,
        MaxTravelBase = 130,
        MotorCurrentBase = 140,
        MicroStepsBase = 150,
        StallGuardBase = 200,
        // End per axis settings
        ToolChangeMode = 341
    }

    public enum StreamingState
    {
        NoFile = 0,
        Idle,
        Send,
        SendMDI,
        Home,
        Halted,
        FeedHold,
        ToolChange,
        Start,
        Stop,
        Paused,
        JobFinished,
        Reset,
        AwaitResetAck,
        Disabled,
        Error
    }

    public enum HomedState
    {
        Unknown = 0,
        NotHomed,
        Homed
    }

    [Flags]
    public enum Signals : int // Keep in sync with SIGNALS constant above
    {
        Off = 0,
        LimitX = 1 << 0,
        LimitY = 1 << 1,
        LimitZ = 1 << 2,
        LimitA = 1 << 3,
        LimitB = 1 << 4,
        LimitC = 1 << 5,
        EStop  = 1 << 6,
        Probe  = 1 << 7,
        Reset = 1 << 8,
        SafetyDoor = 1 << 9,
        Hold = 1 << 10,
        CycleStart = 1 << 11,
        BlockDelete = 1 << 12,
        OptionalStop = 1 << 13,
        ProbeDisconnected = 1 << 14,
        MotorWarning = 1 << 15
    }

    [Flags]
    public enum THCSignals : int
    {
        Off = 0,
        ArcOk = 1 << 0,
        THCEnabled = 1 << 1,
        THCActive = 1 << 2,
        TorchOn = 1 << 3,
        OhmicProbe = 1 << 4,
        VelocityLock = 1 << 5,
        VoidLock = 1 << 6,
        Down = 1 << 7,
        Up = 1 << 8
    }
    public struct GrblState
    {
        public GrblStates State;
        public int Substate;
        public int LastAlarm;
        public int Error;
        public Color Color;
        public bool MPG;
    }

    public class Resources
    {
        public static string Path { get; set; }
        public static string Locale { get; set; }
        public static string IniName { get; set; }
        public static string IniFile { get { return Path + IniName; } }
        public static string DebugFile { get; set; } = string.Empty;
        public static string ConfigName { get; set; }
        public static bool IsLegacyController { get; set; } = false; // Set true if controller is legacy v1.1

        static Resources()
        {
            Path = @"./";
            Locale = "en-US";
            IniName = "App.config";
        }
    }

    public static class Grbl
    {
        public static void Reset()
        {
            Comms.com.WriteByte((byte)GrblConstants.CMD_RESET);
            System.Threading.Thread.Sleep(20);
            //grblState.State = GrblStates.Unknown;
            //grblState.Substate = 0;
        }

        public static GrblViewModel GrblViewModel { get; set; } = null;

        //       public static GrblInfo Info { get; private set; };

        public static bool WaitForResponse(string command)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            if (GrblViewModel == null)
                return false;

            if (GrblViewModel.ResponseLogVerbose)
                GrblViewModel.ResponseLog.Add(command);

            var t = new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => GrblViewModel.OnResponseReceived += a,
                a => GrblViewModel.OnResponseReceived -= a,
                5000, () => GrblViewModel.ExecuteCommand(command));
            }); t.Start();

            while (res == null)
                EventUtils.DoEvents();

            return res == true;
        }

        public static bool WaitForIdle(string command)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            if (GrblViewModel == null)
                return false;

            if (GrblViewModel.ResponseLogVerbose)
                GrblViewModel.ResponseLog.Add(command);

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                cancellationToken,
                null,
                a => GrblViewModel.OnResponseReceived += a,
                a => GrblViewModel.OnResponseReceived -= a,
                1000, () => GrblViewModel.ExecuteCommand(command));
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
                    a => GrblViewModel.OnResponseReceived += a,
                    a => GrblViewModel.OnResponseReceived -= a,
                    5000);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                if (GrblViewModel.GrblState.State != GrblStates.Idle)
                    res = null;
            }

            return res == true;
        }

        public static bool WaitForWcoUpdate()
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            if (GrblViewModel == null)
                return false;

            // Wait for WCO update to get current work offsets

            if (GrblViewModel.Poller.IsEnabled)
            {
                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    null,
                    a => GrblViewModel.OnWCOUpdated += a,
                    a => GrblViewModel.OnWCOUpdated -= a,
                    5000);
                }).Start();
            }

            while (res == null)
                EventUtils.DoEvents();

            return res == true;
        }
    }

    public class CoordinateValues<T> : ViewModelBase
    {
        private bool _suspend = false;
        private T[] arr = new T[6];

        public int Length { get { return 6; } }
        public bool SuspendNotifications
        {
            get { return _suspend; }
            set
            {
                if(!(_suspend = value))
                {
                    //for(int i = 0; i < Length; i++)
                    //  //  if(!double.IsNaN((double)arr[i]))
                    //        OnPropertyChanged(GrblInfo.AxisLetters.Substring(i, 1));
                }
            }
        }

        public T[] Array { get { return arr; } }

        public T this[int i]
        {
            get { return arr[i]; }
            set
            {
                if(!value.Equals(arr[i]))
                {
                    arr[i] = value;
                    if(!_suspend)
                        OnPropertyChanged(GrblInfo.AxisIndexToLetter(i));
                }
            }
        }
    }

    public class Position : ViewModelBase
    {
        public Position()
        {
            init();
        }

        public Position(string values)
        {
            init();
            Parse(values);
        }

        public Position(double x, double y, double z)
        {
            init();
            X = x;
            Y = y;
            Z = z;
        }

        public Position(Position pos)
        {
            init();
            for (var i = 0; i < Values.Length; i++)
                Values[i] = pos.Values[i];
        }

        private void init()
        {
            for (var i = 0; i < Values.Length; i++)
                Values[i] = double.NaN;

            Name = GetType().Name;
            Values.PropertyChanged += Values_PropertyChanged;
        }

        public void Clear()
        {
            bool changed = false;

            for (var i = 0; i < Values.Length; i++)
            {
                changed |= !Values[i].Equals(double.NaN);
                Values[i] = double.NaN;
            }

            if (changed)
                OnPropertyChanged(nameof(Position));
        }

        public void Zero()
        {
            bool changed = false;

            for (var i = 0; i < Values.Length; i++)
            {
                changed |= !Values[i].Equals(0d);
                Values[i] = 0d;
            }

            if (changed)
                OnPropertyChanged(nameof(Position));
        }

        public static Position operator +(Position b, Position c)
        {
            Position a = new Position();

            for (var i = 0; i < a.Values.Length; i++)
                a.Values[i] = b.Values[i] + c.Values[i];

            return a;
        }

        public static Position operator -(Position b, Position c)
        {
            Position a = new Position();

            for (var i = 0; i < a.Values.Length; i++)
                a.Values[i] = b.Values[i] - c.Values[i];

            return a;
        }

        public void Add (Position pos)
        {
            foreach(int i in GrblInfo.AxisFlags.ToIndices())
                Values[i] += pos.Values[i];

            OnPropertyChanged(nameof(Position));
        }

        public void Subtract(Position pos)
        {
            foreach (int i in GrblInfo.AxisFlags.ToIndices())
                Values[i] -= pos.Values[i];

            OnPropertyChanged(nameof(Position));
        }

        public void Set(Position pos)
        {
            foreach (int i in GrblInfo.AxisFlags.ToIndices())
            {
                if (!Values[i].Equals(pos.Values[i]))
                    Values[i] = pos.Values[i];
            }
            OnPropertyChanged(nameof(Position));
        }

        public bool IsSet(AxisFlags axisflags)
        {
            bool ok = true;

            foreach (int i in axisflags.ToIndices())
                ok = ok && !double.IsNaN(Values[i]);

            return ok;
        }

        public bool Equals(Position pos)
        {
            bool equal = true;

            foreach (int i in GrblInfo.AxisFlags.ToIndices())
            {
                if (!(equal = Values[i].Equals(pos.Values[i])))
                    break;
            }

            return equal;
        }

        public string Name { get; private set; }

        public bool SuspendNotifications
        {
            get { return Values.SuspendNotifications; }
            set { Values.SuspendNotifications = value; }
        }

        private void Values_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        public bool Parse(string values)
        {
            bool changed = false, ok = false;

            try
            {
                double[] position = dbl.ParseList(values);

                for (var i = 0; i < position.Length; i++)
                {
                    if (double.IsNaN(Values[i]) ? !double.IsNaN(position[i]) : Values[i] != position[i])
                    {
                        Values[i] = position[i];
                        changed = true;
                    }
                }

                if (changed)
                    OnPropertyChanged("Position");

                ok = true;
            }
            catch { }

            return ok;
        }

        public string ToString(AxisFlags axisflags, int precision = 3)
        {
            string parameters = string.Empty;

            foreach (int i in axisflags.ToIndices())
                parameters += GrblInfo.AxisIndexToLetter(i) + (Math.Round(Values[i], precision).ToInvariantString());

            return parameters;
        }
        public string ToString(AxisFlags axisflags, Direction direction, int precision = 3)
        {
            string parameters = string.Empty;

            foreach (int i in axisflags.ToIndices())
                parameters += GrblInfo.AxisIndexToLetter(i) + (Math.Round(direction == Direction.Negative ? -Values[i] : Values[i], precision).ToInvariantString());

            return parameters;
        }
        public string ToString(AxisFlags axisflags, AxisFlags toNegative, int precision = 3)
        {
            string parameters = string.Empty;

            foreach (int i in axisflags.ToIndices())
                parameters += GrblInfo.AxisIndexToLetter(i) + (Math.Round(toNegative.HasFlag(GCodeParser.AxisFlag[i]) ? -Values[i] : Values[i], precision).ToInvariantString());

            return parameters;
        }

        public CoordinateValues<double> Values { get; private set; } = new CoordinateValues<double>();
        public double X { get { return Values[0]; } set { Values[0] = value; } }
        public double Y { get { return Values[1]; } set { Values[1] = value; } }
        public double Z { get { return Values[2]; } set { Values[2] = value; } }
        public double A { get { return Values[3]; } set { Values[3] = value; } }
        public double B { get { return Values[4]; } set { Values[4] = value; } }
        public double C { get { return Values[5]; } set { Values[5] = value; } }
    }

    public class CoordinateSystem : Position
    {
        string _code = string.Empty;

        public CoordinateSystem() : base()
        { }

        public CoordinateSystem(string code, string data) : base(data)
        {
            Code = code;

            if (code.StartsWith("G5"))
            {
                double id = Math.Round(double.Parse(code.Substring(2), CultureInfo.InvariantCulture) - 3.0d, 1);

                Id = (int)Math.Floor(id) + (int)Math.Round((id - Math.Floor(id)) * 10.0d, 0);
            }
        }

        public int Id { get; private set; }
        public string Code { get { return _code; } set { _code = value; OnPropertyChanged(); } }
    }

    public class Tool : Position
    {
        public Tool(string code) : base()
        {
            Code = code;
        }

        public Tool(string code, string offsets) : base(offsets)
        {
            Code = code;
        }

        public string Code { get; set; }

        double _r;

        public double R { get { return R; } set { _r = value; OnPropertyChanged(); } }

        public new string ToString(AxisFlags axisflags, int precision = 3)
        {
            return "P" + Code + base.ToString(axisflags, precision);
        }
    }

    public static class GrblCommand
    {
        public static string Mist { get; set; } = ((char)GrblConstants.CMD_COOLANT_MIST_OVR_TOGGLE).ToString();
        public static string Flood { get; set; } = ((char)GrblConstants.CMD_COOLANT_FLOOD_OVR_TOGGLE).ToString();
        public static string Fan { get; set; } = ((char)GrblConstants.CMD_OVERRIDE_FAN0_TOGGLE).ToString();
        public static string ToolChange { get; set; } = "T{0}";
    }

    public static class GrblAuxIO
    {
        public static int DigitalInputs { get; internal set; }
        public static int DigitalOutputs { get; internal set; }
        public static int AnalogInputs { get; internal set; }
        public static int AnalogOutputs { get; internal set; }

        public static bool IsEnabled { get { return DigitalInputs + DigitalOutputs + AnalogInputs + AnalogOutputs > 0; } }

        internal static void ParseConfig (string config)
        {
            var values = config.Split(':')[1].TrimEnd(']').Split(',');
            if(values.Length == 4)
            {
                DigitalInputs = int.Parse(values[0]);
                DigitalOutputs = int.Parse(values[1]);
                AnalogInputs = int.Parse(values[2]);
                AnalogOutputs = int.Parse(values[3]);
            }
        }
    }

    public static class GrblInfo
    {
        #region Attributes

        private static bool _probeProtect = false;
        private static int _numAxes;

        static GrblInfo()
        {
            NumAxes = 3;
        }

        public static string AxisLetters { get; private set; } = "XYZABC";
        public static string PositionFormatString { get; private set; } = string.Empty;
        public static string Version { get; private set; } = string.Empty;
        public static int Build { get; private set; } = 0;
        public static bool IsGrblHAL { get; internal set; }
        public static string Firmware { get; internal set; } = "Grbl";
        public static bool ExtendedProtocol { get; internal set; }
        public static string Identity { get; private set; } = string.Empty;
        public static string Options { get; private set; } = string.Empty;
        public static string NewOptions { get; private set; } = string.Empty;
        public static string TrinamicDrivers { get; private set; } = string.Empty;
        public static int SerialBufferSize { get; private set; } = 128;
        public static int PlanBufferSize { get; private set; } = 16;
        public static bool ReportProbeResult { get; internal set; } = false;
        public static bool ForceSetOrigin { get; private set; } = false;
        public static bool ExpressionsSupported { get; private set; } = false;
        public static int NumAxes
        {
            get { return _numAxes;  }
            private set
            {
                _numAxes = value;
                int flags = 0;
                PositionFormatString = string.Empty;
                for (int i = 0; i < _numAxes; i++)
                {
                    flags = (flags << 1) | 0x01;
                    if(!LatheModeEnabled || i != 1)
                        PositionFormatString += AxisIndexToLetter(i) + ": {" + i.ToString() + "}  ";
                }
                if (LatheModeEnabled && _numAxes == 3)
                {
                    flags &= ~0x02;
                    _numAxes--;
                }
                PositionFormatString = PositionFormatString.TrimEnd(' ');
                AxisFlags = (AxisFlags)flags;
            }
        }
        public static Position TravelResolution { get; private set; } = new Position();
        public static Position MaxTravel { get; private set; } = new Position();
        public static Signals OptionalSignals { get; private set; } = Signals.Off;
        public static AxisFlags AxisFlags { get; private set; } = AxisFlags.None;
        public static int NumTools { get; private set; } = 0;
        public static int NumFans { get; private set; } = 0;
        public static bool HasATC { get; private set; }
        public static bool HasEnums { get; private set; }
        public static bool HasSettingDescriptions { get; private set; }
        public static bool HasSimpleProbeProtect { get { return _probeProtect & IsGrblHAL && Build >= 20200924; } internal set { _probeProtect = value; } }
        public static bool ManualToolChange { get; private set; }
        public static bool HasSDCard { get; private set; }
        public static string UploadProtocol { get; private set; } = string.Empty;
        public static string IpAddress { get; private set; } = string.Empty;
        public static bool HasPIDLog { get; private set; }
        public static bool HasProbe { get; private set; } = true;
        public static bool HomingEnabled { get; internal set; } = true;
        public static AxisFlags HomingDirection { get; internal set; } = AxisFlags.None;
        public static bool UseLegacyRTCommands { get; internal set; } = true;
        public static bool MPGMode { get; set; }
        public static bool HasFirmwareJog { get; internal set; } = false;
        public static bool LatheModeEnabled
        {
            get { return GrblParserState.LatheMode != LatheMode.Disabled; }
            set { if (value && GrblParserState.LatheMode == LatheMode.Disabled) { GrblParserState.LatheMode = LatheMode.Radius; } }
        }
        public static ObservableCollection<string> SystemInfo { get; private set; } = new ObservableCollection<string>();
        public static bool IsLoaded { get; private set; }

        #endregion

        public static string AxisIndexToLetter(int index)
        {
            return AxisLetters.Substring(index, 1);
        }

        public static int AxisLetterToIndex(string letter)
        {
            return AxisLetters.IndexOf(letter);
        }

        public static int AxisLetterToIndex(char letter)
        {
            return AxisLetters.IndexOf(letter);
        }

        public static AxisFlags AxisIndexToFlag(int index)
        {
            return (AxisFlags)(1 << index);
        }

        public static AxisFlags AxisLetterToFlag(string letter)
        {
            return (AxisFlags)(1 << AxisLetters.IndexOf(letter));
        }

        public static AxisFlags AxisLetterToFlag(char letter)
        {
            return (AxisFlags)(1 << AxisLetters.IndexOf(letter));
        }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;
            bool getExtended = !Resources.IsLegacyController && ExtendedProtocol; // && Build >= 20201109;
            CancellationToken cancellationToken = new CancellationToken();

            PollGrbl.Suspend();
            SystemInfo.Clear();

            model.Silent = true;
            Firmware = model.Firmware;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    1000, () => Comms.com.WriteCommand(getExtended ? GrblConstants.CMD_GETINFO_EXTENDED : GrblConstants.CMD_GETINFO));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            model.Silent = false;
            PollGrbl.Resume();

            model.NumAxes = NumAxes;
            model.AxisEnabledFlags = AxisFlags;
            model.LatheModeEnabled = LatheModeEnabled;
            model.OptionalSignals.Value = OptionalSignals;
            IsLoaded = res == true;

            if (IsGrblHAL) // For now...
                Firmware = "grblHAL";

            model.Firmware = Firmware;
            model.HasFans = NumFans > 0;

            if(!Resources.IsLegacyController)
                IsGrblHAL = IsGrblHAL || Firmware == "grblHAL";

            return res == true;
        }

        internal static void OnSettingsLoaded (GrblViewModel model)
        {
            model.IsMetric = GrblSettings.GetInteger(GrblSetting.ReportInches) != 1;
            model.Keyboard.IsContinuousJoggingEnabled = IsGrblHAL;
            model.Keyboard.SoftLimits = GrblSettings.GetInteger(GrblSetting.SoftLimitsEnable) == 1;
            model.Keyboard.LimitSwitchesClearance = GrblSettings.GetDouble(GrblSetting.HomingPulloff);
            model.GrblState = model.GrblState; // Temporary hack to enable the Home button when homing is enabled

            HomingDirection = (AxisFlags)GrblSettings.GetInteger(GrblSetting.HomingDirMask);

            if (AxisFlags == AxisFlags.None)
            {
                int i = 0;
                double stepsmm;
                do
                {
                    stepsmm = GrblSettings.GetDouble(GrblSetting.TravelResolutionBase + i);
                    TravelResolution.Values[i++] = 1d / stepsmm;
                    MaxTravel.Values[i++] = GrblSettings.GetDouble(GrblSetting.MaxTravelBase + i); ;
                } while (!double.IsNaN(stepsmm) && i < TravelResolution.Values.Length);
            }
            else foreach (int i in AxisFlags.ToIndices())
            {
                TravelResolution.Values[i] = 1d / GrblSettings.GetDouble(GrblSetting.TravelResolutionBase + i);
                MaxTravel.Values[i] = GrblSettings.GetDouble(GrblSetting.MaxTravelBase + i);
            }

            if (HasFirmwareJog)
            {
                double val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogStepDistance)).Equals(double.NaN))
                    model.Keyboard.JogStepDistance = val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogSlowDistance)).Equals(double.NaN))
                    model.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogFastDistance)).Equals(double.NaN))
                    model.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Fast] = val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogStepSpeed)).Equals(double.NaN))
                    model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Step] = val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogSlowSpeed)).Equals(double.NaN))
                    model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(grblHALSetting.JogFastSpeed)).Equals(double.NaN))
                    model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Fast] = val;
            }
        }

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        public static string Startup(GrblViewModel model)
        {
            bool? res = null;
            int retries = 10;

            PollGrbl.Suspend();
            CancellationToken cancellationToken = new CancellationToken();

            while (retries-- > 0)
            {
                res = null;

                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    OnStartup,
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    250, () => Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                if (Comms.com.Reply.StartsWith("<"))
                    retries = 0;
            }

            if (!ExtendedProtocol)
            {
                res = null;
                Comms.com.PurgeQueue();

                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    OnLegacyStartup,
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    1500, () => Comms.com.WriteByte((byte)GrblConstants.CMD_STATUS_REPORT_LEGACY[0]));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();
            }
            else if(!Resources.IsLegacyController)
                IsGrblHAL = model.Firmware == "grblHAL";

            PollGrbl.Resume();

            return Comms.com.Reply;
        }

        private static void DetectNumAxes (string rt_report)
        {
            var s = rt_report.Split('|');
            if (s.Length > 1)
            {
                var pos = s[1].Split(':');
                if ((pos[0] == "MPos" || pos[0] == "WPos") && NumAxes != pos[1].Split(',').Length)
                {
                    NumAxes = pos[1].Split(',').Length;
                    if (Grbl.GrblViewModel != null)
                    {
                        Grbl.GrblViewModel.NumAxes = NumAxes;
                        Grbl.GrblViewModel.AxisEnabledFlags = AxisFlags;
                        Grbl.GrblViewModel.ClearPosition();
                        Grbl.GrblViewModel.LatheModeEnabled = LatheModeEnabled;
                        Grbl.GrblViewModel.ParseStatus(rt_report);
                    }
                }
            }
        }

        private static void OnStartup(string data)
        {
            if ((ExtendedProtocol = data.StartsWith("<")))
                DetectNumAxes(data);
        }

        private static void OnLegacyStartup(string data)
        {
            if (data.StartsWith("<"))
                DetectNumAxes(data);
        }

        private static void Process(string data)
        {
            if (data.StartsWith("["))
            {
                string[] valuepair = data.Substring(1).TrimEnd(']').Split(':');

                switch (valuepair[0])
                {
                    case "VER":
                        Version = valuepair[1];
                        if (valuepair.Count() > 2)
                            Identity = valuepair[2];
                        if (Version.LastIndexOf('.') > 0)
                        {
                            int build = 0;
                            int.TryParse(Version.Substring(Version.LastIndexOf('.') + 1), out build);
                            Build = build;
                        }
                        break;

                    case "OPT":
                        Options = valuepair[1];
                        string[] s = Options.Split(',');
                        if (s[0].Contains('+'))
                            OptionalSignals |= Signals.SafetyDoor;
                        ForceSetOrigin = s[0].Contains('Z');
                        if (s.Length > 1)
                            PlanBufferSize = int.Parse(s[1], CultureInfo.InvariantCulture);
                        if (s.Length > 2)
                            SerialBufferSize = int.Parse(s[2], CultureInfo.InvariantCulture);
                        if (s.Length > 3 && NumAxes != int.Parse(s[3], CultureInfo.InvariantCulture))
                        {
                            NumAxes = int.Parse(s[3], CultureInfo.InvariantCulture);
                            if (Grbl.GrblViewModel != null)
                                Grbl.GrblViewModel.ClearPosition();
                        }
                        if (s.Length > 4)
                            NumTools = int.Parse(s[4], CultureInfo.InvariantCulture);
                        break;

                    case "NEWOPT":
                        NewOptions = valuepair[1];
                        string[] s2 = valuepair[1].Split(',');
                        foreach (string value in s2)
                        {
                            if (value.StartsWith("TMC="))
                                TrinamicDrivers = value.Substring(4);
                            else switch (value)
                                {
                                    case "ENUMS":
                                        HasEnums = true;
                                        break;

                                    case "EXPR":
                                        ExpressionsSupported = true;
                                        break;

                                    case "TC":
                                        ManualToolChange = true;
                                        break;

                                    case "ATC":
                                        HasATC = true;
                                        break;

                                    case "ETH":
                                        break;

                                    case "HOME":
                                        HomingEnabled = true;
                                        break;

                                    case "SD":
                                        HasSDCard = true;
                                        break;

                                    case "SED":
                                        HasSettingDescriptions = true;
                                        break;

                                    case "YM":
                                        if(UploadProtocol == string.Empty)
                                            UploadProtocol = "YModem";
                                        break;

                                    case "FTP":
                                        UploadProtocol = "FTP";
                                        break;

                                    case "PID":
                                        HasPIDLog = true;
                                        break;

                                    case "NOPROBE":
                                        HasProbe = false;
                                        break;

                                    case "LATHE":
                                        LatheModeEnabled = true;
                                        break;

                                    case "BD":
                                        OptionalSignals |= Signals.BlockDelete;
                                        break;

                                    case "ES":
                                        OptionalSignals |= Signals.EStop;
                                        break;

                                    case "MW":
                                        OptionalSignals |= Signals.MotorWarning;
                                        break;

                                    case "OS":
                                        OptionalSignals |= Signals.OptionalStop;
                                        break;

                                    case "RT+":
                                    case "RT-":
                                        UseLegacyRTCommands = false;
                                        break;
                                }
                        }
                        break;

                    case "FIRMWARE":
                        Firmware = valuepair[1];
                        SystemInfo.Add(data);
                        break;

                    default:
                        SystemInfo.Add(data);
                        if (data.StartsWith("[AUX IO:"))
                            GrblAuxIO.ParseConfig(data);
                        else if (data.StartsWith("[FANS:"))
                            NumFans = int.Parse(data.Substring(6).TrimEnd(']'));
                        else if (data.StartsWith("[IP:"))
                            IpAddress = data.Substring(4).TrimEnd(']');
                        break;
                }
            }
        }
    }

    public static class GrblParserState
    {
        private static string _tool = string.Empty;
        private static Dictionary<string, string> state = new Dictionary<string, string>();

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            PollGrbl.Suspend();

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    400, () => Comms.com.WriteCommand(GrblConstants.CMD_GETPARSERSTATE));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            PollGrbl.Resume();

            return res == true;
        }

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        /* Vanilla grbl workaround */

        public static bool Get(bool addMissing)
        {
            if(addMissing && (addMissing = Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel) && GrblWorkParameters.Get()))
            {
                if (IsActive("G43.1") == null || IsActive("G49") == null)
                {
                    var isOffset = IsPositionOffset(GrblWorkParameters.ToolLengtOffset);
                    if((isOffset ? IsActive("G43.1") : IsActive("G49")) == null)
                        state.Add(isOffset ? "G43.1" : "G49", "");
                }

                if (IsActive("G92") == null)
                {
                    var g92 = GrblWorkParameters.GetCoordinateSystem("G92");
                    if (g92 != null && IsPositionOffset(g92))
                        state.Add("G92", "");
                }
            }

            return addMissing;
        }

        private static bool IsPositionOffset(Position pos)
        {
            bool isOffset = false;

            foreach (int i in GrblInfo.AxisFlags.ToIndices())
                isOffset |= !(double.IsNaN(pos.Values[i]) || pos.Values[i] != 0d);

            return isOffset;
        }

        /* End vanilla grbl workaround */

        public static string Tool
        {
            get { return _tool; }
            set
            {
                _tool = value;
                if (GrblWorkParameters.Tools.Where(t => t.Code == _tool.ToString()).FirstOrDefault() == null)
                    GrblWorkParameters.Tools.Add(new Tool(_tool.ToString()));
            }
        }
        public static string WorkOffset { get; set; }
        public static bool IsLoaded { get { return state.Count > 0; } }

        public static SpindleState SpindleState { get; private set; } = SpindleState.Off;
        public static CoolantState CoolantState { get; private set; } = CoolantState.Off;
        public static MotionMode MotionMode { get; private set; } = MotionMode.G0;
        public static FeedRateMode FeedRateMode { get; private set; } = FeedRateMode.UnitsPerMin;
        public static DistanceMode DistanceMode { get; private set; } = DistanceMode.Absolute;
        public static LatheMode LatheMode { get; set; } = LatheMode.Disabled;
        public static IJKMode IJKMode { get; private set; } = IJKMode.Incremental;
        public static ToolLengthOffset ToolLengthOffset { get; private set; } = ToolLengthOffset.Cancel;
        public static Units Units { get; private set; } = Units.Metric;
        public static bool IsMetric { get { return Units == Units.Metric; } }
        public static Plane Plane { get; private set; } = Plane.XY;
        public static bool IsScalingActive { get; private set; } = false;

        public static string IsActive(string key) // returns null if not active, "" or parsed value if not
        {
            string value = null;

            state.TryGetValue(key, out value);

            return value;
        }

        public static bool Process(string data)
        {
            if (data.StartsWith("[GC:")) try
            {
                state.Clear();
                string[] s = data.Substring(4).TrimEnd(']').Split(' ');
                foreach (string val in s)
                {
                    if (val.StartsWith("G51"))
                        state.Add(val.Substring(0, 3), val.Substring(4));
                    else if (val.StartsWith("G5") && val.Length > 2 && "G54G55G56G57G58G59".Contains(val.Substring(0, 3)))
                        WorkOffset = val;
                    else if ("FST".Contains(val.Substring(0, 1)))
                    {
                        state.Add(val.Substring(0, 1), val.Substring(1));
                        if (val.Substring(0, 1) == "T")
                        {
                            _tool = val.Substring(1);

                            if (_tool == "0")
                                _tool = GrblConstants.NO_TOOL;
                        }
                    }
                    else {
                        state.Add(val, "");
                        switch (val)
                        {
                            case "G0":
                                MotionMode = MotionMode.G0;
                                break;

                            case "G1":
                                MotionMode = MotionMode.G1;
                                break;

                            case "G2":
                                MotionMode = MotionMode.G2;
                                break;

                            case "G3":
                                MotionMode = MotionMode.G3;
                                break;

                            case "G5":
                                MotionMode = MotionMode.G5;
                                break;

                            case "G7":
                                LatheMode = LatheMode.Diameter;
                                break;

                            case "G8":
                                LatheMode = LatheMode.Radius;
                                break;

                            case "G17":
                                Plane = Plane.XY;
                                break;

                            case "G18":
                                Plane = Plane.XZ;
                                break;

                            case "G19":
                                Plane = Plane.YZ;
                                break;

                            case "G20":
                                Units = Units.Imperial;
                                break;

                            case "G21":
                                Units = Units.Metric;
                                break;

                            case "G33":
                                MotionMode = MotionMode.G33;
                                break;

                            case "G38.2":
                                MotionMode = MotionMode.G38_2;
                                break;

                            case "G38.3":
                                MotionMode = MotionMode.G38_3;
                                break;

                            case "G38.4":
                                MotionMode = MotionMode.G38_4;
                                break;

                            case "G38.5":
                                MotionMode = MotionMode.G38_5;
                                break;

                            case "G43":
                                ToolLengthOffset = ToolLengthOffset.Enable;
                                break;

                            case "G43.1":
                                ToolLengthOffset = ToolLengthOffset.EnableDynamic;
                                break;

                            case "G43.2":
                                ToolLengthOffset = ToolLengthOffset.ApplyAdditional;
                                break;

                            case "G49":
                                ToolLengthOffset = ToolLengthOffset.Cancel;
                                break;

                            case "G50":
                            case "G51":
                                IsScalingActive = val == "G51";
                                break;

                            case "G73":
                                MotionMode = MotionMode.G73;
                                break;

                            case "G76":
                                MotionMode = MotionMode.G76;
                                break;

                            case "G80":
                                MotionMode = MotionMode.G80;
                                break;

                            case "G81":
                                MotionMode = MotionMode.G81;
                                break;

                            case "G82":
                                MotionMode = MotionMode.G82;
                                break;

                            case "G83":
                                MotionMode = MotionMode.G83;
                                break;

                            case "G84":
                                MotionMode = MotionMode.G84;
                                break;

                            case "G85":
                                MotionMode = MotionMode.G85;
                                break;

                            case "G86":
                                MotionMode = MotionMode.G86;
                                break;

                            case "G87":
                                MotionMode = MotionMode.G87;
                                break;

                            case "G88":
                                MotionMode = MotionMode.G88;
                                break;

                            case "G89":
                                MotionMode = MotionMode.G89;
                                break;

                            case "G90":
                                DistanceMode = DistanceMode.Absolute;
                                break;

                            case "G90.1": // not supported or reported by grbl
                                IJKMode = IJKMode.Absolute;
                                break;

                            case "G91":
                                DistanceMode = DistanceMode.Incremental;
                                break;

                            case "G91.1": // not reported by grbl, default state
                                IJKMode = IJKMode.Incremental;
                                break;

                            case "G93":
                                FeedRateMode = FeedRateMode.InverseTime;
                                break;

                            case "G94":
                                FeedRateMode = FeedRateMode.UnitsPerMin;
                                break;

                            case "G95":
                                FeedRateMode = FeedRateMode.UnitsPerRev;
                                break;

                            case "M3":
                                SpindleState = SpindleState.CW;
                                break;

                            case "M4":
                                SpindleState = SpindleState.CCW;
                                break;

                            case "M5":
                                SpindleState = SpindleState.Off;
                                break;

                            case "M7":
                                CoolantState |= CoolantState.Mist;
                                break;

                            case "M8":
                                CoolantState |= CoolantState.Flood;
                                break;

                            case "M9":
                                CoolantState = CoolantState.Off;
                                break;
                        }
                    }
                }
                return true;
            }
            catch { }
            return false;
        }
    }

    public class GrblWorkParameters
    {
        private static Dispatcher dispatcher;
        public static bool IsLoaded { get { return CoordinateSystems.Count > 0; } }
        public static LatheMode LatheMode { get; private set; }
        public static double ToolLengthOffsetReference { get; private set; } = double.NaN;
        public static ObservableCollection<CoordinateSystem> CoordinateSystems { get; private set; } = new ObservableCollection<CoordinateSystem>();
        public static ObservableCollection<Tool> Tools { get; private set; } = new ObservableCollection<Tool>();
        public static CoordinateSystem ToolLengtOffset { get; private set; } = new CoordinateSystem("TLO", "");
        public static CoordinateSystem ProbePosition { get; private set; } = new CoordinateSystem("PRB", "");
        public static bool ProbeSuccesful { get; private set; } = false;

        private static Action<string> dataReceived;

        public static CoordinateSystem GetCoordinateSystem(string gCode)
        {
            return CoordinateSystems.Where(x => x.Code == gCode).FirstOrDefault();
        }

        public static double ConvertX(LatheMode source, LatheMode target, double value)
        {
            if (source != target) switch (target)
            {
                case LatheMode.Radius:
                    value /= 2.0d;
                    break;

                case LatheMode.Diameter:
                    value *= 2.0d;
                    break;
            }

            return value;
        }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            if (Tools.Count == 0)
                Tools.Add(new Tool(GrblConstants.NO_TOOL));

            if (!GrblParserState.IsLoaded)
                GrblParserState.Get(model);

            dispatcher = Dispatcher.CurrentDispatcher;
            dataReceived += process;
            LatheMode = GrblParserState.LatheMode;

            model.Silent = true;

            PollGrbl.Suspend();

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => dataReceived(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    400, () => Comms.com.WriteCommand(GrblConstants.CMD_GETNGCPARAMETERS));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            PollGrbl.Resume();

            model.Silent = false;
            dataReceived -= process;

            if (Tools.Count == 1)
            {
                Tools.Add(new Tool("1"));
                Tools.Add(new Tool("2"));
                Tools.Add(new Tool("3"));
                Tools.Add(new Tool("4"));
            }

//            GrblParserState.Tool = GrblParserState.Tool;    // Add tool to Tools if not in list
            model.Tool = model.Tool;                        // Force UI update
            model.ToolOffset.Z = ToolLengtOffset.Z;

            return res == true;
        }

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        private static string extractValues(string data, out string parameters)
        {
            int sep = data.IndexOf(":");
            if (sep > 0)
            {
                parameters = data.Substring(sep + 1).TrimEnd(']');
                return data.Substring(1, sep - 1);
            }
            parameters = "";
            return "";
        }

        public static void RemoveNoTool()
        {
            Tool tool = Tools.Where(x => x.Code == GrblConstants.NO_TOOL).FirstOrDefault();
            if (tool != null)
                Tools.Remove(tool);
        }

        private static void AddOrUpdateTool(string gCode, string data)
        {
            string[] s1 = data.Split('|');
            string[] s2 = s1[1].Split(',');

            Tool tool = Tools.Where(x => x.Code == s1[0]).FirstOrDefault();
            if (tool == null)
            {
                tool = new Tool(s1[0], s1[1]);
                Tools.Add(tool);

            }
            else
                tool.Parse(s1[1]);

            if (s1.Length > 2)
            {
                s2 = s1[2].Split(',');
                tool.R = dbl.Parse(s2[0]);
            }
        }

        private static CoordinateSystem AddOrUpdateCS(string gCode, string data)
        {
            CoordinateSystem cs = CoordinateSystems.Where(x => x.Code == gCode).FirstOrDefault();
            if (cs == null)
                CoordinateSystems.Add(cs = new CoordinateSystem(gCode, data));
            else
                cs.Parse(data);

            return cs;
        }

        private static void process(string data)
        {
            if (Dispatcher.CurrentDispatcher != dispatcher)
            {
                dispatcher.Invoke(dataReceived, data);
                return;
            }

            if (data.StartsWith("["))
            {
                string parameters, gCode = extractValues(data, out parameters);
                switch (gCode)
                {
                    case "G28":
                    case "G30":
                    case "G54":
                    case "G55":
                    case "G56":
                    case "G57":
                    case "G58":
                    case "G59":
                    case "G59.1":
                    case "G59.2":
                    case "G59.3":
                    case "G92":
                        AddOrUpdateCS(gCode, parameters);
                        break;

                    case "T":
                        AddOrUpdateTool(gCode, parameters);
                        break;

                    case "TLO":
                        // Workaround for legacy grbl, it reports only one offset...
                        ToolLengtOffset.SuspendNotifications = true;
                        ToolLengtOffset.Z = double.NaN;
                        ToolLengtOffset.SuspendNotifications = false;
                        // End workaround   

                        ToolLengtOffset.Parse(parameters);

                        // Workaround for legacy grbl, copy X offset to Z (there is no info available for which axis...)
                        if (double.IsNaN(ToolLengtOffset.Z))
                        {
                            ToolLengtOffset.Z = ToolLengtOffset.X;
                            ToolLengtOffset.X = ToolLengtOffset.Y = 0d;
                        }
                        // End workaround

                        //AddOrUpdateCS("G43.1", parameters);
                        break;

                    case "TLR":
                        double tlr;
                        if(double.TryParse(parameters, out tlr))
                            ToolLengthOffsetReference = tlr;
                        break;

                    case "PRB":
                        {
                            var p = parameters.Split(':');
                            if (p.Length == 2) {
                                ProbePosition.Parse(p[0]);
                                ProbeSuccesful = p[1] == "1";
                            }
                        }
                        break;
                }
            }
        }
    }

    public class GrblErrors
    {
        private static Dictionary<int, string> messages = new Dictionary<int, string>();

        public static Dictionary<int, string> List { get { return messages; } }

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;
            bool noload;

            if (GrblInfo.HasEnums && messages.Count == 0)
            {
                PollGrbl.Suspend();
                CancellationToken cancellationToken = new CancellationToken();

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => Process(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        1000, () => Comms.com.WriteCommand(GrblConstants.CMD_GETERRORCODES));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                PollGrbl.Resume();
            }

            if ((noload = messages.Count == 0 ) || Resources.Locale != "en-US")
            {
                try
                {
                    StreamReader file = FileUtils.OpenFile(string.Format("{0}error_codes_{1}.csv", Resources.Path, Resources.Locale == "en-US" ? "en_US" : Resources.Locale));

                    if (file == null && Resources.Locale != "en-US")
                        file = FileUtils.OpenFile(string.Format("{0}error_codes_en_US.csv", Resources.Path));

                    if (file != null)
                    {
                        string msg, line = file.ReadLine(); // Skip header  

                        line = file.ReadLine();

                        while (line != null)
                        {
                            int key;
                            string[] columns = line.Split(',');

                            columns[0].Replace("\"", "");

                            if (columns.Length == 3 && int.TryParse(columns[0], out key))
                            {
                                if (!noload && messages.TryGetValue(key, out msg))
                                    messages.Remove(key);

                                messages.Add(key, columns[1] + ": " + columns[2]);
                            }

                            line = file.ReadLine();
                        }
                    }

                    file.Close();
                }
                catch
                {
                }
            }

            return messages.Count > 0;
        }

        private static void Process(string data)
        {
            if (data.StartsWith("[ERRORCODE:"))
            {
                var details = data.Substring(11).TrimEnd(']').Split('|');
                try
                {
                    if (details.Length == 3)
                        messages.Add(int.Parse(details[0]), details[2] == string.Empty ? details[1] : details[2]);
                    else
                        messages.Add(int.Parse(details[0]), details[1]);
                }
                catch
                {
                }
            }
        }

        static GrblErrors()
        {

        }

        public static string GetMessage(string code)
        {
            string message = null;

            try
            {
                if (messages != null)
                    messages.TryGetValue(int.Parse(code), out message);
            }
            catch
            {
            }

            return message == null ? string.Format("error:{0}", code) : message;
        }
    }

    public class GrblAlarms
    {
        private static Dictionary<int, string> messages = new Dictionary<int, string>();

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        public static Dictionary<int, string> List { get { return messages; } }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;
            bool noload;

            if (GrblInfo.HasEnums && messages.Count == 0)
            {
                PollGrbl.Suspend();
                CancellationToken cancellationToken = new CancellationToken();

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => Process(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        1000, () => Comms.com.WriteCommand(GrblConstants.CMD_GETALARMCODES));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                PollGrbl.Resume();
            }

            if ((noload = messages.Count == 0) || Resources.Locale != "en-US")
            {
                try
                {
                    StreamReader file = FileUtils.OpenFile(string.Format("{0}alarm_codes_{1}.csv", Resources.Path, Resources.Locale == "en-US" ? "en_US" : Resources.Locale));

                    if (file == null && Resources.Locale != "en-US")
                        file = FileUtils.OpenFile(string.Format("{0}alarm_codes_en_US.csv", Resources.Path));

                    if (file != null)
                    {
                        string msg, line = file.ReadLine(); // Skip header

                        line = file.ReadLine();

                        while (line != null)
                        {
                            int key;
                            string[] columns = line.Split(',');

                            columns[0].Replace("\"", "");

                            if (columns.Length == 3 && int.TryParse(columns[0], out key))
                            {
                                if (!noload && messages.TryGetValue(key, out msg))
                                    messages.Remove(key);

                                messages.Add(key, columns[1] + ": " + columns[2]);
                            }

                            line = file.ReadLine();
                        }
                    }

                    file.Close();
                }
                catch
                {
                }
            }

            return messages.Count > 0;
        }

        private static void Process(string data)
        {
            if (data.StartsWith("[ALARMCODE:"))
            {
                var details = data.Substring(11).TrimEnd(']').Split('|');
                try
                {
                    if (details.Length == 3)
                        messages.Add(int.Parse(details[0]), details[1] == string.Empty ? details[2] : details[1] + ": " + details[2]);
                    else
                        messages.Add(int.Parse(details[0]), details[1]);
                }
                catch
                {
                }
            }
        }

        public static string GetMessage(string code)
        {
            string message = "";

            try
            {
                if (messages != null)
                    messages.TryGetValue(int.Parse(code), out message);
            }
            catch
            {
            }

            return message == "" ? string.Format("Alarm {0}", code) : message;
        }
    }

    public class GrblSettingGroup
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Name { get; set; }
        public IEnumerable<GrblSettingDetails> Settings
        {
            get { return GrblSettings.Settings.Where(x => x.GroupId == Id); }
        }

        public GrblSettingGroup (string data)
        {
            string[] values = data.Split('|');

            Id = int.Parse(values[0]);
            ParentId = int.Parse(values[1]);
            Name = values[2];
        }
    }

    public static class GrblSettingGroups
    {
        public static List<GrblSettingGroup> Groups { get; private set; } = new List<GrblSettingGroup>();

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;

            if (GrblInfo.HasEnums && Groups.Count == 0)
            {
                PollGrbl.Suspend();
                CancellationToken cancellationToken = new CancellationToken();

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => Process(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        400, () => Comms.com.WriteCommand(GrblConstants.CMD_GETSETTINGSGROUPS));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                PollGrbl.Resume();
            }

            if (Resources.Locale != "en-US")
            {
                try
                {
                    StreamReader file = new StreamReader(string.Format("{0}setting_groups_{1}.csv", Resources.Path, Resources.Locale));

                    if (file != null)
                    {
                        string line = file.ReadLine(); // Skip header

                        line = file.ReadLine();

                        while (line != null)
                        {
                            string[] columns = line.Split('\t');
                            
                            if (columns.Length == 3)
                            {
                                var group = Groups.Where(x => x.Id == int.Parse(columns[0])).FirstOrDefault();
                                if (group != null)
                                    group.Name = columns[1];
                            }

                            line = file.ReadLine();
                        }
                    }

                    file.Close();
                }
                catch
                {
                }
            }

            return Groups.Count > 0;
        }

        public static void RemoveUnused()
        {
            List<GrblSettingGroup> remove = new List<GrblSettingGroup>();

            foreach(var group in Groups)
            {
                if (GrblSettings.Settings.Where(x => x.GroupId == group.Id).FirstOrDefault() == null)
                    remove.Add(group);
            }

            foreach (var group in remove)
            {
                Groups.Remove(group);
            }
        }

        private static void Process(string data)
        {
            if (data != "ok")
            {
                string[] valuepair = data.TrimEnd(']').Split(':');
                if (valuepair.Length == 2 && valuepair[0] == "[SETTINGGROUP")
                {
                    Groups.Add(new GrblSettingGroup(valuepair[1]));
                }
            }
        }
    }

    public static class GrblStartupLines
    {
        public static List<string> Lines { get; private set; } = new List<string>();

        public static bool Get()
        {
            return Grbl.GrblViewModel != null && Get(Grbl.GrblViewModel);
        }

        public static bool Get(GrblViewModel model)
        {
            bool? res = null;

            Lines.Clear();

            PollGrbl.Suspend();
            CancellationToken cancellationToken = new CancellationToken();

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    400, () => Comms.com.WriteCommand(GrblConstants.CMD_GETSTARTUPLINES));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            PollGrbl.Resume();

            return Lines.Count > 0;
        }

        private static void Process(string data)
        {
            if (data.StartsWith(GrblConstants.CMD_GETSTARTUPLINES))
            {
                string[] valuepair = data.Split('=');
                if (valuepair.Length == 2)
                {
                    Lines.Add(valuepair[1]);
                }
            }
        }
    }

    public class GrblSettingDetails : ViewModelBase
    {
        public enum DataTypes
        {
            BOOL = 0,
            BITFIELD,
            XBITFIELD,
            RADIOBUTTONS,
            AXISMASK,
            INTEGER,
            FLOAT,
            TEXT,
            PASSWORD,
            IP4
        };

        private string _value, _description = null;
        internal bool Silent = true;

        public GrblSettingDetails(string data)
        {
            string[] values = data.Split('|');

            Id = int.Parse(values[0]);
            GroupId = int.Parse(values[1]);
            Name = values[2];
            Unit = values[3];
            DataType = values[4] == string.Empty ? DataTypes.TEXT : (DataTypes)int.Parse(values[4]);
            Format = values[5];
            Min = values[6] == string.Empty ? double.NaN : dbl.Parse(values[6]);
            Max = values[7] == string.Empty ? double.NaN : dbl.Parse(values[7]);
        }

        public int Id { get; internal set; }
        public int GroupId { get; internal set; }
        public string Name { get; internal set; }
        public string Value {
            get { return _value; }
            set
            {
                if (DataType == DataTypes.FLOAT)
                    value = GrblSettings.FormatFloat(value, Format);
                if (_value != value)
                {
                    _value = value;
                    if ((IsDirty = !Silent))
                    {
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(FormattedValue));
                    }
                }
            }
        }
        public string FormattedValue
        {
            get
            {
                if (_value == null)
                    return "n/a";

                switch(DataType)
                {
                    case DataTypes.BOOL:
                        return _value == "0" ? "false" : "true";

                    case DataTypes.BITFIELD:
                        return _value == "0" ? "no" : _value;

                    case DataTypes.XBITFIELD:
                        return _value == "0" ? "disabled" : string.Format("enabled ({0})", _value);

                    case DataTypes.AXISMASK:
                        if(_value != "0")
                        {
                            int axes = int.Parse(_value), idx = 0;
                            string res = string.Empty;
                            while(axes != 0)
                            {
                                if ((axes & 0x01) != 0)
                                    res += GrblInfo.AxisIndexToLetter(idx);
                                axes >>= 1; idx++;
                            }
                            return res;
                        }
                        return "no";

                    case DataTypes.RADIOBUTTONS:
                        return Format.Split(',')[int.Parse(_value)];
                }

                return _value;
            }
        }

        public string Unit { get; internal set; } = string.Empty;
        public string Format { get; internal set; } = string.Empty;
        public DataTypes DataType { get; internal set; }
        public double Min { get; internal set; }
        public double Max { get; internal set; }
        public string Description {
            get
            {
                if(_description == null)
                {
                    if (Grbl.GrblViewModel == null)
                        _description = String.Empty;
                    else
                    {
                        bool? res = null;
                        CancellationToken cancellationToken = new CancellationToken();

                        PollGrbl.Suspend();
                        Grbl.GrblViewModel.Silent = true;

                        new Thread(() =>
                        {
                            res = WaitFor.AckResponse<string>(
                                cancellationToken,
                                response => ProcessDetail(response),
                                a => Grbl.GrblViewModel.OnResponseReceived += a,
                                a => Grbl.GrblViewModel.OnResponseReceived -= a,
                                400, () => Comms.com.WriteCommand("$SED=" + Id.ToString()));
                        }).Start();

                        while (res == null)
                            EventUtils.DoEvents();

                        if (_description == null)
                            _description = String.Empty;

                        Grbl.GrblViewModel.Silent = false;
                        PollGrbl.Resume();
                    }
                }

                return _description;
            }
            internal set
            {
                _description = value;
            }
        }

        private void ProcessDetail(string data)
        {
            if (data != "ok" && data.StartsWith("[SETTINGDESCR:"))
            {
                int pos = data.IndexOf('|');
                _description = data.Substring(pos + 1).TrimEnd(']').Replace("\\n", "\r\n"); ;
            }
        }

        public bool IsDirty { get; internal set; } = false;
    }

    public static class GrblSettings
    {
        public static ObservableCollection<GrblSettingDetails> Settings { get; private set; } = new ObservableCollection<GrblSettingDetails>();

        public static bool IsLoaded { get { return Settings.Count > 0; } }
        public static bool ReportProbeCoordinates { get; private set; }

        public static GrblSettingDetails Get(GrblSetting key)
        {
            return Settings.Where(x => x.Id == ((int)key)).FirstOrDefault();
        }
        public static GrblSettingDetails Get(grblHALSetting key)
        {
            return Settings.Where(x => x.Id == ((int)key)).FirstOrDefault();
        }
        public static bool HasSetting(GrblSetting key)
        {
            return GetString(key) != null;
        }
        public static bool HasSetting(grblHALSetting key)
        {
            return GetString(key) != null;
        }

        public static string GetString(grblHALSetting key)
        {
            var setting = Settings.Where(x => x.Id == ((int)key)).FirstOrDefault();

            return setting != null ? setting.Value : null;
        }

        public static string GetString(GrblSetting key)
        {
            var setting = Settings.Where(x => x.Id == ((int)key)).FirstOrDefault();

            return setting != null ? setting.Value : null;
        }

        public static double GetDouble(GrblSetting key)
        {
            return dbl.Parse(GetString(key));
        }

        public static double GetDouble(grblHALSetting key)
        {
            return dbl.Parse(GetString(key));
        }

        public static int GetInteger(GrblSetting key)
        {
            int res = -1;
            var v = GetString(key);

            if (v != null)
                int.TryParse(v, out res);

            return res;
        }

        public static int GetInteger(grblHALSetting key)
        {
            int res = -1;
            var v = GetString(key);

            if (v != null)
                int.TryParse(v, out res);

            return res;
        }

        public static bool Load(GrblViewModel model)
        {
            bool? res = null;
            bool load, getExtended = !Resources.IsLegacyController && GrblInfo.IsGrblHAL && GrblInfo.Build >= 20200716;
            CancellationToken cancellationToken = new CancellationToken();

            PollGrbl.Suspend();
            model.Silent = true;

            if ((load = Settings.Count == 0) && GrblInfo.HasEnums)
            {
                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => ProcessDetail(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        1000, () => Comms.com.WriteCommand(GrblConstants.CMD_GETSETTINGSDETAILS));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                GrblSettingGroups.Get(model);
            }

            res = null;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    1000, () => Comms.com.WriteCommand(getExtended ? GrblConstants.CMD_GETSETTINGS_ALL : GrblConstants.CMD_GETSETTINGS));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            model.Silent = false;
            PollGrbl.Resume();

            if (load)
            {
                GrblSettingGroups.RemoveUnused();

                if(!GrblInfo.IsGrblHAL || !GrblInfo.HasSettingDescriptions || Resources.Locale != "en-US") try
                {
                    StreamReader file;
                    string filename = string.Format("{0}{1}setting_codes_{2}.txt",
                                                    Resources.Path,
                                                    GrblInfo.IsGrblHAL ? "hal_" : string.Empty,
                                                    Resources.Locale == "en-US" ? "en_US" : Resources.Locale);

                    file = FileUtils.OpenFile(filename);

                    if(file == null && Resources.Locale != "en-US")
                        file = FileUtils.OpenFile(filename.Replace(Resources.Locale, "en_US"));

                    if (file != null)
                    {
                        string line = file.ReadLine();

                        line = file.ReadLine(); // Skip header  

                        while (line != null)
                        {
                            string[] values = line.Split('\t');

                            if (values.Length >= 6)
                            {
                                var setting = Settings.Where(x => x.Id == int.Parse(values[0])).FirstOrDefault();

                                if (setting != null) {

                                    if(setting.Name == string.Empty)
                                    {
                                        try
                                        {
                                            setting.DataType = (GrblSettingDetails.DataTypes)Enum.Parse(typeof(GrblSettingDetails.DataTypes), values[3].ToUpperInvariant());
                                        }
                                        catch
                                        {
                                            setting.DataType = GrblSettingDetails.DataTypes.TEXT;
                                        }
                                        
                                        setting.Name = values[1];
                                        setting.Format = values[4];

                                        if (setting.DataType == GrblSettingDetails.DataTypes.INTEGER || setting.DataType == GrblSettingDetails.DataTypes.FLOAT)
                                            setting.Unit = values[2];

                                        if(values.Length > 6)
                                            setting.Min = values[6] == string.Empty ? double.NaN : dbl.Parse(values[6]);

                                        if (values.Length > 7)
                                            setting.Max = values[7] == string.Empty ? double.NaN : dbl.Parse(values[7]);
                                    }
                                    else if(Resources.Locale != "en-US")
                                    {
                                        setting.Name = values[1];

                                        if (setting.DataType == GrblSettingDetails.DataTypes.INTEGER || setting.DataType == GrblSettingDetails.DataTypes.FLOAT)
                                            setting.Unit = values[2];
                                    }

                                    setting.Description = values[5].Replace("\\n", "\r\n");
                                }
                            }
                            line = file.ReadLine();
                        }
                        file.Close();
                        file.Dispose();
                    }
                }
                catch (Exception ex)
                {
                }
            }

            if(!GrblInfo.IsGrblHAL)
                ReportProbeCoordinates = true;

            GrblInfo.OnSettingsLoaded(model);

            return IsLoaded;
        }

        public static bool Load()
        {
            return Grbl.GrblViewModel != null && Load(Grbl.GrblViewModel);
        }

        public static bool HasChanges()
        {
            var changed = Settings.Where(x => x.IsDirty);

            return changed != null && changed.Count() > 0;
        }

#if USE_ASYNC
        public static async void Save()
#else
        public static bool Save()
#endif
        {
            bool ok = true;
            var changed = Settings.Where(x => x.IsDirty);

            if (changed != null && changed.Count() > 0)
            {
                foreach (var setting in changed)
                {
#if USE_ASYNC
                    var task = Task.Run(() => Comms.com.AwaitAck(string.Format("${0}={1}", Setting.Id, Setting.Value)));
                    await await Task.WhenAny(task, Task.Delay(2500));
#else
                    Comms.com.WriteCommand(string.Format("${0}={1}", setting.Id, setting.Value));
                    Comms.com.AwaitAck();
#endif
                    setting.ClearErrors();
                    if (Comms.com.Reply.StartsWith("error:"))
                    {
                        ok = false;
                        setting.SetError(GrblErrors.GetMessage(Comms.com.Reply.Substring(6)));
                    }

                    setting.IsDirty = setting.HasErrors;
                }
            }

            return ok;
        }

        private static List<string> Export ()
        {
            List<string> exp = new List<string>();

            if (GrblInfo.IsGrblHAL)
                exp.Add("%");

            exp.Add("; " + GrblInfo.Firmware + (GrblInfo.Identity != string.Empty ? ":" + GrblInfo.Identity : ""));
            exp.Add("; " + GrblInfo.Version);
            exp.Add("; [OPT:" + GrblInfo.Options + "]");

            if (GrblInfo.NewOptions != string.Empty)
                exp.Add("; [NEWOPT:" + GrblInfo.NewOptions + "]");

            foreach (string opt in GrblInfo.SystemInfo)
                exp.Add("; " + opt);

            exp.Add(";");

            if (GrblInfo.Identity != string.Empty)
                exp.Add(string.Format("{0}={1}", GrblConstants.CMD_GETINFO, GrblInfo.Identity));

            if (GrblStartupLines.Get())
            {
                int id = 0;
                foreach (var line in GrblStartupLines.Lines)
                {
                    exp.Add(string.Format("{0}{1}={2}", GrblConstants.CMD_GETSTARTUPLINES, id++, line));
                }
            }

            foreach (GrblSettingDetails setting in Settings)
            {
                if(!string.IsNullOrEmpty(setting.Name))
                    exp.Add(string.Format("; {0} - {1}", setting.Id, setting.Name));
                exp.Add(string.Format("${0}={1}", setting.Id, setting.Value));
            }

            if (GrblInfo.IsGrblHAL)
                exp.Add("%");

            return exp;
        }

        public static void CopyToClipboard()
        {
            if (Settings.Count > 0) try
            {
                Clipboard.SetText(string.Join("\r\n", Export().ToArray()));
            }
            catch
            {
            }
        }

        public static void Backup(string filename)
        {
            if (Settings.Count > 0) try
            {
                StreamWriter file = new StreamWriter(filename);
                if (file != null)
                {
                    List<string> settings = Export();

                    foreach (string s in settings)
                        file.WriteLine(s);

                    file.Close();
                }
            }
            catch
            {
            }
        }

        public static string FormatFloat(string value, string format)
        {
            float fval;
            if (float.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out fval))
                value = fval.ToString(format.StartsWith(NumberFormatInfo.CurrentInfo.NegativeSign) ? format.Substring(1) : format, CultureInfo.InvariantCulture);
            return value;
        }

        private static void ProcessDetail(string data)
        {
            if (data != "ok")
            {
                string[] valuepair = data.TrimEnd(']').Split(':');
                if (valuepair.Length == 2 && valuepair[0] == "[SETTING")
                    Settings.Add(new GrblSettingDetails(valuepair[1]));
            }
        }

        private static void Process(string data)
        {
            if (data != "ok")
            {
                int id;
                string[] valuepair = data.Split('=');

                if (valuepair.Length == 2 && int.TryParse(valuepair[0].Substring(1), out id))
                {
                    switch ((GrblSetting)id)
                    {
                        case GrblSetting.HomingEnable: // TODO: remove?
                            GrblInfo.HomingEnabled = valuepair[1] != "0";
                            break;

                        case (GrblSetting)grblHALSetting.EnableLegacyRTCommands: // TODO: remove!
                            GrblInfo.UseLegacyRTCommands = valuepair[1] != "0";
                            break;

                        case (GrblSetting)grblHALSetting.JogStepDistance:
                            GrblInfo.HasFirmwareJog = true;
                            break;


                        case GrblSetting.StatusReportMask:
                            {
                                if (String.IsNullOrEmpty(valuepair[1])) // FluidNC workaround
                                    valuepair[1] = "0";                 // for missing parameter value
                                var value = int.Parse(valuepair[1]);
                                Grbl.GrblViewModel.IsParserStateLive = (value & (1 << 9)) != 0;
                                GrblInfo.ReportProbeResult = ReportProbeCoordinates = (value & (1 << 7)) != 0;
                                GrblInfo.HasSimpleProbeProtect = (value & (1 << 11)) != 0;
                            }
                            break;
                    }

                    var setting = Settings.Where(x => x.Id == id).FirstOrDefault();

                    if (setting == null)
                    {
                        setting = new GrblSettingDetails(id.ToString() + "|0||||||");
                        Settings.Add(setting);
                    }

                    setting.Value = valuepair[1];
                    setting.Silent = setting.IsDirty = false;
                }
            }
        }
    }

    public class PollGrbl
    {
        System.Timers.Timer pollTimer = null;

        private byte RTCommand = GrblConstants.CMD_STATUS_REPORT_ALL;

        internal static bool suspend = false;

        internal static void Suspend()
        {
            suspend = true;
            Comms.com.PurgeQueue();
        }

        internal static void Resume()
        {
            suspend = false;
        }

        public void Run()
        {
            pollTimer = new System.Timers.Timer();
            pollTimer.Elapsed += new System.Timers.ElapsedEventHandler(pollTimer_Elapsed);
            //  this.pollTimer.SynchronizingObject = this;
        }

        public bool IsEnabled { get { return pollTimer.Enabled; } }

        public void SetState(int PollInterval)
        {
            if (PollInterval != 0)
            {
                suspend = false;
                if(pollTimer.Enabled)
                    pollTimer.Stop();
                pollTimer.Interval = PollInterval;
                pollTimer.Start();
                RTCommand = GrblConstants.CMD_STATUS_REPORT_ALL;
            }
            else
                pollTimer.Stop();
        }

        void pollTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if(!suspend)
                Comms.com.WriteByte(RTCommand);

            if (RTCommand == GrblConstants.CMD_STATUS_REPORT_ALL)
                RTCommand = GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT);
        }
    }

    public static class GrblLegacy
    {
        public static byte ConvertRTCommand(byte cmd)
        {
            if (GrblInfo.UseLegacyRTCommands) switch (cmd)
            {
                case GrblConstants.CMD_STATUS_REPORT_ALL:
                    cmd = (byte)GrblConstants.CMD_STATUS_REPORT_LEGACY[0];
                    break;

                case GrblConstants.CMD_STATUS_REPORT:
                    cmd = (byte)GrblConstants.CMD_STATUS_REPORT_LEGACY[0];
                    break;

                case GrblConstants.CMD_CYCLE_START:
                    cmd = (byte)GrblConstants.CMD_CYCLE_START_LEGACY[0];
                    break;

                case GrblConstants.CMD_FEED_HOLD:
                    cmd = (byte)GrblConstants.CMD_FEED_HOLD_LEGACY[0];
                    break;
            }

            return cmd;
        }
    }

    public static class JobTimer
    {
        private static bool paused = false;
        private static Stopwatch stopWatch = new Stopwatch();

        public static bool IsRunning { get { return stopWatch.IsRunning || paused; } }

        public static bool IsPaused { get { return paused; } }

        public static bool Pause
        {
            get
            {
                return paused;
            }
            set
            {
                if (IsRunning)
                {
                    if ((paused = value))
                        stopWatch.Stop();
                    else
                        stopWatch.Start();
                }
            }
        }

        public static string RunTime
        {
            get
            {
                return IsRunning ? string.Format("{0:00}:{1:00}:{2:00}", stopWatch.Elapsed.Hours, stopWatch.Elapsed.Minutes, stopWatch.Elapsed.Seconds)
                                 : "00:00:00";
            }
        }

        public static void Start()
        {
            paused = false;
            stopWatch.Reset();
            stopWatch.Start();
        }

        public static void Stop()
        {
            paused = false;
            stopWatch.Stop();
        }
    }
}
