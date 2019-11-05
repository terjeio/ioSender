/*
 * AppConfig.cs - part of Grbl Code Sender
 *
 * v0.01 / 2019-10-31 / Io Engineering (Terje Io)
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
using System.IO;
using System.Xml.Serialization;
using CNC.Core;
using System.Windows;
using CNC.GCode;

namespace CNC.Controls
{

    [Serializable]
    public class LatheConfig
    {
        public enum ZDirections
        {
            Positive = 0,
            Negative
        }

        [XmlIgnore]
        public double ZDirFactor { get { return ZDirection== ZDirections.Negative? -1d : 1d;} }

        public LatheMode XMode { get; set; } = LatheMode.Radius;
        public ZDirections ZDirection { get; set; } = ZDirections.Negative;
        public double PassDepthLast { get; set; } = 0.02d;
        public double FeedRate { get; set; } = 300d;
    }

    [Serializable]
    public class CameraConfig
    {
        public double XOffset { get; set; } = 0d;
        public double YOffset { get; set; } = 0d;
        public CameraMoveMode MoveMode { get; set; } = CameraMoveMode.BothAxes;
    }

    [Serializable]
    public class GCodeViewerConfig
    {
        public int ArcResolution { get; set; } = 10;
        public double MinDistance { get; set; } = 0.05d;
        public bool ShowGrid { get; set; } = true;
        public bool ShowAxes { get; set; } = true;
        public bool ShowBoundingBox { get; set; } = false;
        public bool ShowViewCube { get; set; } = true;
    }

    [Serializable]

    public class Config
    {
        public string PortParams { get; set; } = "COM1:115200,N,8,1";
        public bool LatheMode { get; set; } = false;
  
        public LatheConfig Lathe = new LatheConfig();

        public CameraConfig Camera = new CameraConfig();

        public GCodeViewerConfig GCodeViewer = new GCodeViewerConfig();
    }

    public class AppConfig
    {
        public Config Config = null; 

        public bool Save (string filename)
        {
            bool ok = false;

            if(Config == null)
                Config = new Config();

            XmlSerializer xs = new XmlSerializer(typeof(Config));

            try
            {
                FileStream fsout = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                using (fsout)
                {
                    xs.Serialize(fsout, Config);
                    ok = true;
                }
            }
            catch
            {
            }

            return ok;
        }

        public bool Load(string filename)
        {
            bool ok = false;
            XmlSerializer xs = new XmlSerializer(typeof(Config));

            try
            {
                StreamReader reader = new StreamReader(filename);
                Config = (Config)xs.Deserialize(reader);
                reader.Close();
                ok = true;
            }
            catch
            {
            }

            return ok;
        }

        private void setPort (string port)
        {
            Config.PortParams = port + ":" + Config.PortParams.Substring(Config.PortParams.IndexOf(':') + 1);
        }

        public int SetupAndOpen(string appname, System.Windows.Threading.Dispatcher dispatcher)
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
                    status = 1;
            }

            if (!string.IsNullOrEmpty(port))
                selectPort = false;

            if (!selectPort)
            {
                if (!string.IsNullOrEmpty(port))
                    setPort(port);

                if (char.IsDigit(Config.PortParams[0])) // We have an IP address
                    new IPComms(Config.PortParams);
                else
                    new SerialComms(Config.PortParams, Comms.ResetMode.None, dispatcher);
            }

            if ((Comms.com == null || !Comms.com.IsOpen) && string.IsNullOrEmpty(port))
            {
                PortDialog portsel = new PortDialog();

                port = portsel.ShowDialog();
                if (port == null)
                    status = 2;

                if (char.IsDigit(port[0]))
                { // We have an IP address
                    Config.PortParams = port;
                    new IPComms(Config.PortParams);
                }
                else
                {
                    setPort(port);
                    new SerialComms(Config.PortParams, Comms.ResetMode.None, dispatcher);
                }
                Save(CNC.Core.Resources.IniFile);
            }

            if (Comms.com.IsOpen)
            {
                System.Threading.Thread.Sleep(400); // Wait to see if MPG is polling Grbl

                if (!(Comms.com.Reply == "" || Comms.com.Reply.StartsWith("Grbl")))
                {
                    MPGPending await = new MPGPending();
                    await.ShowDialog();
                    if (await.Cancelled)
                    {
                        Comms.com.Close();
                        status = 2;
                    }
                }
            }
            else
            {
                MessageBox.Show(string.Format("Unable to open connection ({0})", Config.PortParams), appname, MessageBoxButton.OK, MessageBoxImage.Error);
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
