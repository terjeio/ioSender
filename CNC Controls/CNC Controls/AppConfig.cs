/*
 * AppConfig.cs - part of Grbl Code Sender
 *
 * v0.01 / 2019-10-20 / Io Engineering (Terje Io)
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
    }
}
