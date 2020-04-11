/*
 * JobParametersViewModel.cs - part of CNC Converters library
 *
 * v0.15 / 2020-04-08 / Io Engineering (Terje Io)
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
using System.Xml.Serialization;
using CNC.Core;

namespace CNC.Converters
{
    [XmlRoot(ElementName = "ConversionParameters")]
    public class JobParametersViewModel : ViewModelBase
    {
        public enum ToolType
        {
            Drill = 0,
            Endmill,
            VBit
        }

        public struct Tool
        {
            public int Id;
            public ToolType Type;
            public double Diameter;
        }

        private double _zRapids = 1d, _zHome = 25d, _zMin = -1.8d, _zSafe = 1d;
        private double _rpm = 5000, _toolDiameter = 3d, _feedRate = 300d, _plungeRate = 100d;
        private double _xScale = 1d, _yScale = 1d;
        private bool _enableTool = false;

        public string Profile { get; set; } = "Default";
        [XmlIgnore]
        public List<Tool> ToolBox { get; private set; } = new List<Tool>();
        public double ZHome { get { return _zHome; } set { _zHome = value; OnPropertyChanged(); } }
        public double ZRapids { get { return _zRapids; } set { _zRapids = value; OnPropertyChanged(); } }
        public double ZSafe { get { return _zSafe; } set { _zSafe = value; OnPropertyChanged(); } }
        public double ZMin { get { return _zMin; } set { _zMin = value; OnPropertyChanged(); } }
        public double RPM { get { return _rpm; } set { _rpm = value; OnPropertyChanged(); } }
        public double ToolDiameter { get { return _toolDiameter; } set { _toolDiameter = value; OnPropertyChanged(); } }
        public double FeedRate { get { return _feedRate; } set { _feedRate = value; OnPropertyChanged(); } }
        public double PlungeRate { get { return _plungeRate; } set { _plungeRate = value; OnPropertyChanged(); } }
        public double ScaleX { get { return _xScale; } set { _xScale = value; OnPropertyChanged(); } }
        public double ScaleY { get { return _yScale; } set { _yScale = value; OnPropertyChanged(); } }
        public bool EnableToolSelection { get { return _enableTool; } set { _enableTool = value; OnPropertyChanged(); } }
    }
}

