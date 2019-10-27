/*
 * GrblCore.cs - part of CNC Controls library
 *
 * v0.02 / 2019-10-23 / Io Engineering (Terje Io)
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
            CMD_TOOL_ACK = 0xA3;

        public const string
            CMD_STATUS_REPORT_LEGACY = "?",
            CMD_CYCLE_START_LEGACY = "~",
            CMD_FEED_HOLD_LEGACY = "!",
            CMD_UNLOCK = "$X",
            CMD_HOMING = "$H",
            CMD_CHECK = "$C",
            CMD_GETSETTINGS = "$$",
            CMD_GETPARSERSTATE = "$G",
            CMD_GETINFO = "$I",
            CMD_GETNGCPARAMETERS = "$#",
            CMD_PROGRAM_DEMARCATION = "%",
            CMD_SDCARD_MOUNT = "$FM",
            CMD_SDCARD_DIR = "$F",
            CMD_SDCARD_RUN = "$F=",
            FORMAT_METRIC = "###0.000",
            FORMAT_IMPERIAL = "##0.0000",
            NO_TOOL = "None",
            SIGNALS = "XYZABCEPRDHSBT"; // Keep in sync with Signals enum below!!

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
        LaserMode = 32,
        PWMFreq = 33,
        PWMOffValue = 34,
        PWMMinValue = 35,
        PWMMaxValue = 36,
        StepperDeenergizeMask = 37,
        SpindlePPR = 38,
        EnableLegacyRTCommands = 39,
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
        AxisSetting_XMaxRate = 110,
        AxisSetting_XAcceleration = 120,
        AxisSetting_XMaxTravel = 130,
        AxisSetting_YMaxRate = 111,
        AxisSetting_YAcceleration = 121,
        AxisSetting_YMaxTravel = 131,
        AxisSetting_ZMaxRate = 112,
        AxisSetting_ZAcceleration = 122,
        AxisSetting_ZMaxTravel = 132
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
        Stop,
        Reset,
        AwaitResetAck,
        Jogging,
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
        OptionalStop = 1 << 13
    }

    public struct GrblState
    {
        public GrblStates State;
        public int Substate;
        public int Error;
        public Color Color;
        public bool MPG;
    }

    public class Resources
    {
        public static string Path { get; set; }
        public static string Language { get; set; }
        public static string IniName { get; set; }
        public static string IniFile { get { return Path + IniName; } }
        public static string ConfigName { get; set; }

        static Resources()
        {
            Path = @"./";
            Language = "en_US";
            IniName = "App.config";
            ConfigName = string.Format("setting_codes_{0}.txt", Language);
        }
    }

    public static class Grbl
    {
        public static void MDICommand (object context, string command)
        {
            if (context != null && context is GrblViewModel)
                ((GrblViewModel)context).MDICommand = command;
        }

        public static void Reset()
        {
            Comms.com.WriteByte((byte)GrblConstants.CMD_RESET);
            System.Threading.Thread.Sleep(20);
            //grblState.State = GrblStates.Unknown;
            //grblState.Substate = 0;
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

        public T this[int i]
        {
            get { return arr[i]; }
            set
            {
                //double v = (double)(object)arr[i];
                //if (dbl.Assign((double)(object)value, ref v))
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

        private void init()
        {
            Clear();

            Values.PropertyChanged += Values_PropertyChanged;
        }

        public void Clear()
        {
            for (var i = 0; i < Values.Length; i++)
                Values[i] = double.NaN;
        }

        public bool SuspendNotifications
        {
            get { return Values.SuspendNotifications; }
            set { Values.SuspendNotifications = value; }
        }

        private void Values_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        public void Parse(string values)
        {
            double[] position = dbl.ParseList(values); 
            for (var i = 0; i < position.Length; i++) {
                if(double.IsNaN(Values[i]) ? !double.IsNaN(position[i]) : Values[i] != position[i])
                    Values[i] = position[i];
            }
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
        public CoordinateSystem(string code, string data)
        {
            Code = code;

            Parse(data);

            if (code.StartsWith("G5"))
            {
                double id = Math.Round(double.Parse(code.Substring(2), CultureInfo.InvariantCulture) - 3.0d, 1);

                Id = (int)Math.Floor(id) + (int)Math.Round((id - Math.Floor(id)) * 10.0d, 0);
            }
        }

        public int Id { get; private set; }
        public string Code { get; set; }
    }

    public class Tool : Position
    {
        public Tool(string code)
        {
            Code = code;
        }

        public Tool(string code, string offsets)
        {
            Code = code;

            Parse(offsets);
        }

        public string Code { get; set; }

        double _r;

        public double R { get { return R; } set { _r = value; OnPropertyChanged(); } }

    }

    public static class GrblInfo
    {
        #region Attributes

        public static string AxisLetters { get; private set; } = "XYZABC";
        public static string Version { get; private set; } = string.Empty;
        public static string Identity { get; private set; } = string.Empty;
        public static string Options { get; private set; } = string.Empty;
        public static string NewOptions { get; private set; } = string.Empty;
        public static string TrinamicDrivers { get; private set; } = string.Empty;
        public static int SerialBufferSize { get; private set; } = 128;
        public static int NumAxes { get; private set; } = 3;
        public static int NumTools { get; private set; } = 0;
        public static bool HasATC { get; private set; }
        public static bool ManualToolChange { get; private set; }
        public static bool HasSDCard { get; private set; }
        public static bool HasPIDLog { get; private set; }
        public static bool MPGMode { get; set; }
        public static bool LatheModeEnabled
        {
            get { return LatheMode != Core.LatheMode.Disabled; }
            set { if(value && LatheMode == Core.LatheMode.Disabled) LatheMode = Core.LatheMode.Radius; }
        }
        public static LatheMode LatheMode { get; set; } = Core.LatheMode.Disabled;
        public static bool Loaded { get; private set; }

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

#if USE_ASYNC
        public static async void Get()
#else
        public static bool Get()
#endif
        {
            NumAxes = 3;
            SerialBufferSize = 128;
            HasATC = false;

            Comms.com.DataReceived += Process;
#if USE_ASYNC
            var task = Task.Run(() => Comms.com.AwaitAck(GrblConstants.CMD_GETINFO));
            await await Task.WhenAny(task, Task.Delay(2500));
#else
            Comms.com.PurgeQueue();
            Comms.com.WriteCommand(GrblConstants.CMD_GETINFO);
            Comms.com.AwaitAck();
#endif
            Comms.com.DataReceived -= Process;

            Loaded = true;

            return Loaded;
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
                        break;

                    case "OPT":
                        Options = valuepair[1];
                        string[] s = Options.Split(',');
                        if (s.Length > 2)
                            SerialBufferSize = int.Parse(s[2], CultureInfo.InvariantCulture);
                        if (s.Length > 3)
                            NumAxes = int.Parse(s[3], CultureInfo.InvariantCulture);
                        if (s.Length > 4)
                            NumTools = int.Parse(s[4], CultureInfo.InvariantCulture);
                        break;

                    case "NEWOPT":
                        NewOptions = valuepair[1];
                        string[] s2 = valuepair[1].Split(',');
                        foreach (string value in s2)
                        {
                            if (value.StartsWith("TMC:"))
                                TrinamicDrivers = value.Substring(4);
                            else switch (value)
                                {
                                    case "TC":
                                        ManualToolChange = true;
                                        break;

                                    case "ATC":
                                        HasATC = true;
                                        break;

                                    case "ETH":
                                        break;

                                    case "SD":
                                        HasSDCard = true;
                                        break;

                                    case "PID":
                                        HasPIDLog = true;
                                        break;

                                    case "LATHE":
                                        LatheModeEnabled = true;
                                        break;
                                }
                        }
                        break;
                }
            }
        }
    }

    public static class GrblParserState
    {
        private static string _tool = string.Empty;
        private static Dictionary<string, string> state = new Dictionary<string, string>();

#if USE_ASYNC
        public async static void Get()
#else
        public static bool Get()
#endif
        {
            Comms.com.DataReceived += Process;
#if USE_ASYNC
            var task = Task.Run(() => Comms.com.AwaitAck(GrblConstants.CMD_GETPARSERSTATE));
            await await Task.WhenAny(task, Task.Delay(2500));
#else
            Comms.com.PurgeQueue();
            Comms.com.WriteCommand(GrblConstants.CMD_GETPARSERSTATE);
            Comms.com.AwaitAck();
#endif
            Comms.com.DataReceived -= Process;

            return Loaded;
        }

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
        public static bool Loaded { get { return state.Count > 0; } }

        public static string IsActive(string key) // returns null if not active, "" or parsed value if not
        {
            string value = null;

            state.TryGetValue(key, out value);

            return value;
        }

        public static void Process(string data)
        {
            if (data.StartsWith("[GC:"))
            {
                state.Clear();
                string[] s = data.Substring(4).Split(' ');
                foreach (string val in s)
                {
                    if (val.StartsWith("G51"))
                        state.Add(val.Substring(0, 3), val.Substring(4));
                    else if (val.StartsWith("G5") && "G54G55G56G57G58G59".Contains(val.Substring(0, 3)))
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
                            case "G7":
                                GrblInfo.LatheMode = LatheMode.Radius;
                                break;

                            case "G8":
                                GrblInfo.LatheMode = LatheMode.Diameter;
                                break;
                        }
                    }
                }
            }
        }
    }

    public class GrblWorkParameters
    {
        public static bool Loaded { get { return CoordinateSystems.Count > 0; } }
        public static LatheMode LatheMode { get; private set; }
        public static ObservableCollection<CoordinateSystem> CoordinateSystems { get; private set; } = new ObservableCollection<CoordinateSystem>();
        public static ObservableCollection<Tool> Tools { get; private set; } = new ObservableCollection<Tool>();
        public static CoordinateSystem ToolLengtOffset { get; private set; } = new CoordinateSystem("TLO", "");
        public static CoordinateSystem ProbePosition { get; private set; } = new CoordinateSystem("PRB", "");

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

#if USE_ASYNC
        public async static void Load()
#else
        public static bool Get()
#endif
        {
            if (Tools.Count == 0)
                Tools.Add(new Tool("None"));

            if (!GrblParserState.Loaded)
                GrblParserState.Get();

            LatheMode = GrblInfo.LatheMode;

            Comms.com.DataReceived += process;
#if USE_ASYNC
            var task = Task.Run(() => Comms.com.AwaitAck(GrblConstants.CMD_GETNGCPARAMETERS));
            await await Task.WhenAny(task, Task.Delay(2500));
#else
            Comms.com.PurgeQueue();
            Comms.com.WriteCommand(GrblConstants.CMD_GETNGCPARAMETERS);
            Comms.com.AwaitAck();
#endif
            Comms.com.DataReceived -= process;

            if (Tools.Count == 1)
            {
                Tools.Add(new Tool("1"));
                Tools.Add(new Tool("2"));
                Tools.Add(new Tool("3"));
                Tools.Add(new Tool("4"));
            }

            GrblParserState.Tool = GrblParserState.Tool; // Add tool to Tools if not in list

            return Loaded;
        }

        private static string extractValues(string data, out string parameters)
        {
            int sep = data.IndexOf(":");
            if (sep > 0)
            {
                parameters = data.Substring(sep + 1, data.IndexOf("]") - sep - 1);
                return data.Substring(1, sep - 1);
            }
            parameters = "";
            return "";
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
                        ToolLengtOffset.Parse(parameters);
                        break;

                    case "PRB":
                        ProbePosition.Parse(parameters.Substring(0, parameters.IndexOf(":") - 1));
                        break;
                }
            }
        }
    }

    public class GrblErrors
    {
        private static Dictionary<string, string> messages = null;

        static GrblErrors()
        {
            try
            {
                StreamReader file = new StreamReader(string.Format("{0}\\error_codes_{1}.csv", Resources.Path, Resources.Language));

                if (file != null)
                {
                    messages = new Dictionary<string, string>();

                    string line = file.ReadLine();

                    line = file.ReadLine(); // Skip header  

                    while (line != null)
                    {
                        string[] columns = line.Split(',');

                        if (columns.Length == 3)
                            messages.Add(columns[0], columns[1] + ": " + columns[2]);

                        line = file.ReadLine();
                    }
                }

                file.Close();
            }
            catch
            {
            }
        }

        public static string GetMessage(string key)
        {
            string message = null;

            if (messages != null)
                messages.TryGetValue(key, out message);

            return message == null ? string.Format("Error {0}", key) : message;
        }
    }

    public class GrblAlarms
    {
        private static Dictionary<string, string> messages = null;

        static GrblAlarms()
        {
            try
            {
                StreamReader file = new StreamReader(string.Format("{0}\\alarm_codes_{1}.csv", Resources.Path, Resources.Language));

                if (file != null)
                {
                    messages = new Dictionary<string, string>();

                    string line = file.ReadLine();

                    line = file.ReadLine(); // Skip header  

                    while (line != null)
                    {
                        string[] columns = line.Split(',');

                        if (columns.Length == 3)
                            messages.Add(columns[0], columns[1] + ": " + columns[2]);

                        line = file.ReadLine();
                    }
                }

                file.Close();
            }
            catch
            {
            }
        }

        public static string GetMessage(string key)
        {
            string message = "";

            if (messages != null)
                messages.TryGetValue(key, out message);

            return message == "" ? string.Format("Alarm {0}", key) : message;
        }
    }

    public static class GrblSettings
    {
        public static DataTable settings;

        static GrblSettings()
        {
            settings = new DataTable("Setting");

            settings.Columns.Add("Id", typeof(int));
            settings.Columns.Add("Name", typeof(string));
            settings.Columns.Add("Value", typeof(string));
            settings.Columns.Add("Unit", typeof(string));
            settings.Columns.Add("Description", typeof(string));
            settings.Columns.Add("DataType", typeof(string));
            settings.Columns.Add("DataFormat", typeof(string));
            settings.Columns.Add("Min", typeof(double));
            settings.Columns.Add("Max", typeof(double));
            settings.PrimaryKey = new DataColumn[] { settings.Columns["Id"] };

            UseLegacyRTCommands = true;
        }

        public static DataView Settings { get { return settings.DefaultView; } }
        public static bool Loaded { get { return settings.Rows.Count > 0; } }
        public static bool HomingEnabled { get; private set; }
        public static bool UseLegacyRTCommands { get; private set; }
        public static bool IsGrblHAL { get; private set; }

        public static string GetString(GrblSetting key)
        {
            DataRow[] rows = settings.Select("Id = " + ((int)key).ToString());

            return rows.Count() == 1 ? (string)rows[0]["Value"] : null;
        }

        public static double GetDouble(GrblSetting key)
        {
            return dbl.Parse(GetString(key));
        }

#if USE_ASYNC
        public async static void Load()
#else
        public static bool Get()
#endif
        {
            settings.Clear();

            Comms.com.DataReceived += Process;
#if USE_ASYNC
            var task = Task.Run(() => Comms.com.AwaitAck(GrblConstants.CMD_GETSETTINGS));
            await await Task.WhenAny(task, Task.Delay(2500));
#else
            Comms.com.PurgeQueue();
            Comms.com.WriteCommand(GrblConstants.CMD_GETSETTINGS);
            Comms.com.AwaitAck();
#endif
            Comms.com.DataReceived -= Process;

            if (IsGrblHAL && !Resources.ConfigName.StartsWith("hal_"))
                Resources.ConfigName = "hal_" + Resources.ConfigName;

            try
            {
                StreamReader file = new StreamReader(string.Format("{0}\\{1}", Resources.Path, Resources.ConfigName));

                if (file != null)
                {
                    string line = file.ReadLine();

                    line = file.ReadLine(); // Skip header  

                    while (line != null)
                    {
                        string[] columns = line.Split('\t');

                        if (columns.Length >= 6)
                        {
                            DataRow[] rows = settings.Select("Id=" + columns[0]);
                            if (rows.Count() == 1)
                            {
                                rows[0]["Name"] = columns[1];
                                rows[0]["Unit"] = columns[2];
                                rows[0]["DataType"] = columns[3];
                                rows[0]["DataFormat"] = columns[4];
                                rows[0]["Description"] = columns[5];
                                if (columns.Length >= 7)
                                    rows[0]["Min"] = dbl.Parse(columns[6]);
                                if (columns.Length >= 8)
                                    rows[0]["Max"] = dbl.Parse(columns[7]);
                                if ((string)rows[0]["DataType"] == "float")
                                    rows[0]["Value"] = GrblSettings.FormatFloat((string)rows[0]["Value"], (string)rows[0]["DataFormat"]);
                            }
                        }
                        line = file.ReadLine();
                    }
                    file.Close();
                    file.Dispose();
                }
            }
            catch
            {
            }

            settings.AcceptChanges();

            return Loaded;
        }

#if USE_ASYNC
        public static async void Save()
#else
        public static void Save()
#endif
        {
            DataTable Settings = settings.GetChanges();
            if (Settings != null)
            {
                foreach (DataRow Setting in Settings.Rows)
                {
#if USE_ASYNC
                    var task = Task.Run(() => Comms.com.AwaitAck(string.Format("${0}={1}", (int)Setting["Id"], (string)Setting["Value"])));
                    await await Task.WhenAny(task, Task.Delay(2500));
#else
                    Comms.com.WriteCommand(string.Format("${0}={1}", (int)Setting["Id"], (string)Setting["Value"]));
                    Comms.com.AwaitAck();
#endif
                }
                settings.AcceptChanges();
            }
        }

        public static void Backup(string filename)
        {
            if (settings != null) try
                {
                    StreamWriter file = new StreamWriter(filename);
                    if (file != null)
                    {
                        if (IsGrblHAL)
                            file.WriteLine('%');
                        foreach (DataRow Setting in settings.Rows)
                        {
                            file.WriteLine(string.Format("${0}={1}", (int)Setting["Id"], (string)Setting["Value"]));
                        }
                        if (IsGrblHAL)
                            file.WriteLine('%');
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

        private static void Process(string data)
        {
            if (data != "ok")
            {
                string[] valuepair = data.Split('=');
                if (valuepair.Length == 2 && valuepair[1] != "")
                {
                    GrblSetting id = (GrblSetting)int.Parse(valuepair[0].Substring(1));
                    switch (id)
                    {
                        case GrblSetting.HomingEnable:
                            HomingEnabled = valuepair[1] != "0";
                            break;

                        case GrblSetting.EnableLegacyRTCommands:
                            UseLegacyRTCommands = valuepair[1] != "0";
                            break;

                        case GrblSetting.ControlInvertMask:
                            IsGrblHAL = true;
                            break;
                    }

                    settings.Rows.Add(new object[] { id, "", valuepair[1], "", "", "", "", double.NaN, double.NaN });
                }
            }
        }
    }

    public class PollGrbl
    {
        System.Timers.Timer pollTimer = null;

        private byte RTCommand = GrblConstants.CMD_STATUS_REPORT_ALL;

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
                pollTimer.Interval = PollInterval;
                pollTimer.Start();
                RTCommand = GrblConstants.CMD_STATUS_REPORT_ALL;
            }
            else
                pollTimer.Stop();
        }

        void pollTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Comms.com.WriteByte(RTCommand);

            if (RTCommand == GrblConstants.CMD_STATUS_REPORT_ALL)
                RTCommand = GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT);
        }
    }

    public static class GrblLegacy
    {
        public static byte ConvertRTCommand(byte cmd)
        {
            if (GrblSettings.UseLegacyRTCommands) switch (cmd)
                {
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
                if (value)
                {
                    if (stopWatch.IsRunning)
                    {
                        stopWatch.Stop();
                        paused = true;
                    }
                }
                else if (paused)
                {
                    stopWatch.Start();
                    paused = false;
                }
            }
        }

        public static string RunTime
        {
            get
            {
                return string.Format("{0:00}:{1:00}:{2:00}",
                                        stopWatch.Elapsed.Hours, stopWatch.Elapsed.Minutes, stopWatch.Elapsed.Seconds);
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
