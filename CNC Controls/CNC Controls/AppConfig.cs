/*
 * AppConfig.cs - part of CNC Controls library for Grbl
 *
 * v0.17 / 2020-04-15 / Io Engineering (Terje Io)
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
using System.IO;
using System.Xml.Serialization;
using System.Windows;
using System.Collections.ObjectModel;
using CNC.Core;
using System.Threading;
using CNC.GCode;
using static CNC.GCode.GCodeParser;

namespace CNC.Controls
{
    [Serializable]
    public class LatheConfig : ViewModelBase
    {
        private bool _isEnabled = false;
        private LatheMode _latheMode = LatheMode.Disabled;

        [XmlIgnore]
        public double ZDirFactor { get { return ZDirection == ZDirection.Negative ? -1d : 1d; } }

        [XmlIgnore]
        public LatheMode[] LatheModes { get { return (LatheMode[])Enum.GetValues(typeof(LatheMode)); } }

        [XmlIgnore]
        public ZDirection[] ZDirections { get { return (ZDirection[])Enum.GetValues(typeof(ZDirection)); } }

        [XmlIgnore]
        public bool IsEnabled { get { return _isEnabled; } set { _isEnabled = value; OnPropertyChanged(); } }

        public LatheMode XMode { get { return _latheMode; } set { _latheMode = value; IsEnabled = value != LatheMode.Disabled; } }
        public ZDirection ZDirection { get; set; } = ZDirection.Negative;
        public double PassDepthLast { get; set; } = 0.02d;
        public double FeedRate { get; set; } = 300d;
    }

    [Serializable]
    public class CameraConfig : ViewModelBase
    {
        double _xoffset = 0d, _yoffset = 0d;

        [XmlIgnore]
        public CameraMoveMode[] MoveModes { get { return (CameraMoveMode[])Enum.GetValues(typeof(CameraMoveMode)); } }

        public double XOffset { get { return _xoffset; } set { _xoffset = value; OnPropertyChanged(); } }
        public double YOffset { get { return _yoffset; } set { _yoffset = value; OnPropertyChanged(); } }
        public CameraMoveMode MoveMode { get; set; } = CameraMoveMode.BothAxes;
    }

    [Serializable]
    public class GCodeViewerConfig : ViewModelBase
    {
        private bool _isEnabled = true;

        public bool IsEnabled { get { return _isEnabled; } set { _isEnabled = value; OnPropertyChanged(); } }
        public int ArcResolution { get; set; } = 10;
        public double MinDistance { get; set; } = 0.05d;
        public bool ShowGrid { get; set; } = true;
        public bool ShowAxes { get; set; } = true;
        public bool ShowBoundingBox { get; set; } = false;
        public bool ShowViewCube { get; set; } = true;
    }

    [Serializable]
    public class JogConfig : ViewModelBase
    {
        private bool _kbEnable;
        private double _fastFeedrate = 500d, _slowFeedrate = 200d, _stepFeedrate = 100d;
        private double _fastDistance = 500d, _slowDistance = 500d, _stepDistance = 0.05d;

        public bool KeyboardEnable { get { return _kbEnable; } set { _kbEnable = value; OnPropertyChanged(); } }
        public double FastFeedrate { get { return _fastFeedrate; } set { _fastFeedrate = value; OnPropertyChanged(); } }
        public double SlowFeedrate { get { return _slowFeedrate; } set { _slowFeedrate = value; OnPropertyChanged(); } }
        public double StepFeedrate { get { return _stepFeedrate; } set { _stepFeedrate = value; OnPropertyChanged(); } }
        public double FastDistance { get { return _fastDistance; } set { _fastDistance = value; OnPropertyChanged(); } }
        public double SlowDistance { get { return _slowDistance; } set { _slowDistance = value; OnPropertyChanged(); } }
        public double StepDistance { get { return _stepDistance; } set { _stepDistance = value; OnPropertyChanged(); } }
    }

    [Serializable]
    public class Macros : ViewModelBase
    {
        public ObservableCollection<CNC.GCode.Macro> Macro { get; private set; } = new ObservableCollection<CNC.GCode.Macro>();
    }

    [Serializable]
    public class Config : ViewModelBase
    {
        private int _pollInterval = 200; // ms

        public int PollInterval { get { return _pollInterval < 100 ? 100 : _pollInterval; } set { _pollInterval = value; OnPropertyChanged(); } }
        public string PortParams { get; set; } = "COMn:115200,N,8,1";
        public int ResetDelay { get; set; } = 2000;
        public bool UseBuffering { get; set; } = false;
        public string Editor { get; set; } = "notepad.exe";

        [XmlIgnore]
        public CommandIgnoreState[] CommandIgnoreStates { get { return (CommandIgnoreState[])Enum.GetValues(typeof(CommandIgnoreState)); } }
        public CommandIgnoreState IgnoreM6 { get; set; } = CommandIgnoreState.No;
        public CommandIgnoreState IgnoreM7 { get; set; } = CommandIgnoreState.No;
        public CommandIgnoreState IgnoreM8 { get; set; } = CommandIgnoreState.No;
        public ObservableCollection<CNC.GCode.Macro> Macros { get; set; } = new ObservableCollection<CNC.GCode.Macro>();
        public JogConfig Jog { get; set; } = new JogConfig();
        public LatheConfig Lathe { get; set; } = new LatheConfig();
        public CameraConfig Camera { get; set; } = new CameraConfig();
        public GCodeViewerConfig GCodeViewer { get; set; } = new GCodeViewerConfig();
    }

    public class AppConfig
    {
        private string configfile = null;
        private bool? MPGactive = null;

        public string FileName { get; private set; }

        private static readonly Lazy<AppConfig> settings = new Lazy<AppConfig>(() => new AppConfig());

        private AppConfig()
        { }

        public static AppConfig Settings { get { return settings.Value; } }

        public Config Base { get; private set; } = null;
        public ObservableCollection<CNC.GCode.Macro> Macros { get { return Base == null ? null : Base.Macros; } }
        public JogConfig Jog { get { return Base == null ? null : Base.Jog; } }
        public CameraConfig Camera { get { return Base == null ? null : Base.Camera; } }
        public LatheConfig Lathe { get { return Base == null ? null : Base.Lathe; } }
        public GCodeViewerConfig GCodeViewer { get { return Base == null ? null : Base.GCodeViewer; } }

        public bool Save(string filename)
        {
            bool ok = false;

            if (Base == null)
                Base = new Config();

            XmlSerializer xs = new XmlSerializer(typeof(Config));

            try
            {
                FileStream fsout = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                using (fsout)
                {
                    xs.Serialize(fsout, Base);
                    configfile = filename;
                    ok = true;
                }
            }
            catch
            {
            }

            return ok;
        }

        public bool Save()
        {
            return configfile != null && Save(configfile);
        }

        public bool Load(string filename)
        {
            bool ok = false;
            XmlSerializer xs = new XmlSerializer(typeof(Config));

            try
            {
                StreamReader reader = new StreamReader(filename);
                Base = (Config)xs.Deserialize(reader);
                reader.Close();
                configfile = filename;

                // temp hack...
                foreach (var macro in Base.Macros)
                {
                    if (macro.IsSession)
                        Base.Macros.Remove(macro);
                }

                ok = true;
            }
            catch
            {
            }

            return ok;
        }

        private void setPort(string port)
        {
            Base.PortParams = port;
            if (!(Base.PortParams.ToLower().StartsWith("ws://") || char.IsDigit(Base.PortParams[0])) && Base.PortParams.IndexOf(':') == -1)
            {
                string[] values = Base.PortParams.Split('!');
                Base.PortParams = values[0] + ":115200,N,8,1" + (values.Length > 1 ? ",," + values[1] : "");
            }
        }

        public int SetupAndOpen(string appname, GrblViewModel model, System.Windows.Threading.Dispatcher dispatcher)
        {
            int status = 0;
            bool selectPort = false;
            string port = string.Empty;

            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;

            string[] args = Environment.GetCommandLineArgs();

            int p = 0;
            while (p < args.GetLength(0)) switch (args[p++])
                {
                    case "-inifile":
                        CNC.Core.Resources.IniName = GetArg(args, p++);
                        break;

                    case "-configmapping":
                        CNC.Core.Resources.ConfigName = GetArg(args, p++);
                        break;

                    case "-language":
                        CNC.Core.Resources.Language = GetArg(args, p++);
                        break;

                    case "-port":
                        port = GetArg(args, p++);
                        break;

                    case "-selectport":
                        selectPort = true;
                        break;

                    default:
                        if (!args[p - 1].EndsWith(".exe") && File.Exists(args[p - 1]))
                            FileName = args[p - 1];
                        break;
                }

            if (!Load(CNC.Core.Resources.IniFile))
            {
                if (MessageBox.Show("Config file not found or invalid, create new?", appname, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (!Save(CNC.Core.Resources.IniFile))
                    {
                        MessageBox.Show("Could not save config file.", appname);
                        status = 1;
                    }
                }
                else
                    return 1;
            }

            if (!string.IsNullOrEmpty(port))
                selectPort = false;

            if (!selectPort)
            {
                if (!string.IsNullOrEmpty(port))
                    setPort(port);
#if USEWEBSOCKET
                if (Base.PortParams.ToLower().StartsWith("ws://"))
                    new WebsocketStream(Base.PortParams, dispatcher);
                else
#endif
                if (char.IsDigit(Base.PortParams[0])) // We have an IP address
                    new TelnetStream(Base.PortParams, dispatcher);
                else
#if USEELTIMA
                    new EltimaStream(Config.PortParams, Config.ResetDelay, dispatcher);
#else
                    new SerialStream(Base.PortParams, Base.ResetDelay, dispatcher);
#endif
            }

            if ((Comms.com == null || !Comms.com.IsOpen) && string.IsNullOrEmpty(port))
            {
                PortDialog portsel = new PortDialog();

                port = portsel.ShowDialog(Base.PortParams);
                if (string.IsNullOrEmpty(port))
                    status = 2;

                else
                {
                    setPort(port);
#if USEWEBSOCKET
                    if (port.ToLower().StartsWith("ws://"))
                        new WebsocketStream(Base.PortParams, dispatcher);
                    else
#endif
                    if (char.IsDigit(port[0])) // We have an IP address
                        new TelnetStream(Base.PortParams, dispatcher);
                    else
#if USEELTIMA
                        new EltimaStream(Config.PortParams, Config.ResetDelay, dispatcher);
#else
                        new SerialStream(Base.PortParams, Base.ResetDelay, dispatcher);
#endif
                    Save(CNC.Core.Resources.IniFile);
                }
            }

            if (Comms.com != null && Comms.com.IsOpen)
            {
                Comms.com.DataReceived += model.DataReceived;

                CancellationToken cancellationToken = new CancellationToken();

                // Wait 400ms to see if a MPG is polling Grbl...

                new Thread(() =>
                {
                    MPGactive = WaitFor.SingleEvent<string>(
                    cancellationToken,
                    null,
                    a => model.OnRealtimeStatusProcessed += a,
                    a => model.OnRealtimeStatusProcessed -= a,
                    500);
                }).Start();

                while (MPGactive == null)
                    EventUtils.DoEvents();

                // ...if so show dialog for wait for it to stop polling and relinquish control.
                if (MPGactive == true)
                {
                    MPGPending await = new MPGPending(model);
                    await.ShowDialog();
                    if (await.Cancelled)
                    {
                        Comms.com.Close(); //!!
                        status = 2;
                    }
                }

                model.IsReady = true;
                model.PollInterval = Base.PollInterval;
            }
            else if (status != 2)
            {
                MessageBox.Show(string.Format("Unable to open connection ({0})", Base.PortParams), appname, MessageBoxButton.OK, MessageBoxImage.Error);
                status = 2;
            }

            return status;
        }

        private string GetArg(string[] args, int i)
        {
            return i < args.GetLength(0) ? args[i] : null;
        }
    }
}
