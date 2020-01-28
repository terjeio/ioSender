/*
 * ThreadViewModel.cs - part of CNC Controls Lathe library
 *
 * v0.01 / 2020-01-17 / Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    public class ThreadModel : BaseViewModel
    {
        private InchProperties _inch;
        private ToolProperties _tool;
        private ThreadProperties _thread;

        public ThreadModel() : base("Threading")
        {
            Inch = new InchProperties();
            Tool = new ToolProperties();
            Thread = new ThreadProperties();
            GCodeFormat = Lathe.Thread.Format.LinuxCNC;
            ZLength = 10d;
            ZStart = 0d;

            PropertyChanged += ThreadModel_PropertyChanged;
        }

        private void ThreadModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsMetric))
            {
                Thread.ThreadSize = Thread.ThreadSize; // Force recalculation of model values
                ZLength = IsMetric ? 10.0d : 0.5d;
            }
        }

        public InchProperties Inch
        {
            get { return _inch; }
            private set { _inch = value; OnPropertyChanged(); }
        }

        public ToolProperties Tool
        {
            get { return _tool; }
            private set { _tool = value; OnPropertyChanged(); }
        }

        public ThreadProperties Thread
        {
            get { return _thread; }
            private set { _thread = value; OnPropertyChanged(); }
        }
    }

    public class ThreadProperties : MeasureViewModel
    {

        private bool _oneLead = false;
        Thread.Type _ttype = Lathe.Thread.Type.M_6G6H;
        private double _retract = 360;

        public ThreadProperties()
        {
            ESide = new EnumFlags<Thread.Side>(Thread.Side.Inside);

            ThreadData threads = new ThreadData();

            threads.AddThreads();

            ThreadSizes = new DataView(Thread.data) {
                AllowNew = false,
                AllowDelete = false,
                AllowEdit = false
            };
//            Type = Thread.type.First().Key;

            CompoundAngles = new List<double>();
            CompoundAngles.Add(0d);
            CompoundAngles.Add(29d);
            CompoundAngles.Add(29.5d);
            CompoundAngles.Add(30d);

            DepthDegressions = new List<string>();
            DepthDegressions.Add("None");
            DepthDegressions.Add("1");
            DepthDegressions.Add("2");
            _depthDegression = "None";

            PropertyChanged += Thread_PropertyChanged;
        }

        private void Thread_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(IsMetric))
            {
                OnPropertyChanged(nameof(TpiFormat));
                OnPropertyChanged(nameof(TpiLabel));
            }
        }

        public EnumFlags<Thread.Side> ESide
        {
            get; private set;
        }

        public Dictionary<Thread.Type, string> Types
        {
            get { return Thread.type; }
        }

        public Thread.Type Type
        {
            get { return _ttype; }
            set
            {
                _ttype = value;
                ThreadSizes.RowFilter = "Type = " + (int)_ttype;
                ThreadSize = ThreadSizes[(string)ThreadSizes[0].Row["Name"] == "" ? 1 : 0];
               // suspend...
                OnPropertyChanged();
            }
        }

        private DataView _threadSizes;

        public DataView ThreadSizes
        {
            get { return _threadSizes; }
            private set { _threadSizes = value; OnPropertyChanged(); }
        }

        DataRowView _sizerow = null;

        public DataRowView ThreadSize
        {
            get { return _sizerow; }
            set { _sizerow = value; OnPropertyChanged(); }
        }

        public double RetractDegrees
        {
            get { return _retract; }
            set { _retract = value; OnPropertyChanged(); }
        }

        public ThreadTaper[] TaperTypes { get { return (ThreadTaper[])Enum.GetValues(typeof(ThreadTaper)); } }

        private ThreadTaper _taperType = ThreadTaper.None;
        public ThreadTaper TaperType
        {
            get { return _taperType; }
            set { _taperType = value; OnPropertyChanged(); }
        }

        private double _taperLength = 0d;

        public double TaperLength
        {
            get { return _taperLength; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_taperLength) : _taperLength != value)
                {
                    _taperLength = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OneLead
        {
            get { return _oneLead; }
            set
            {
                if (_oneLead != value)
                {
                    if ((_oneLead = value))
                        Starts = 1;
                    OnPropertyChanged();
                }
            }
        }

        public bool _fixedLength = false;

        public bool FixedLength
        {
            get { return _fixedLength; }
            set {
                if (_fixedLength != value)
                {
                    _fixedLength = value; // TODO: clear Zlength if fixed
                    OnPropertyChanged();
                }
            }
        }

        public uint _starts = 1;
        public uint Starts
        {
            get { return _starts; }
            set { _starts = value; OnPropertyChanged(); }
        }

        private double _tpi = double.NaN;

        public double TPI
        {
            get { return _tpi; }
            set { _tpi = value; OnPropertyChanged(); }
        }

        public string TpiLabel
        {
            get { return IsMetric ? "in" : "TPI"; }
        }

        public string TpiFormat
        {
            get { return IsMetric ? GrblConstants.FORMAT_IMPERIAL : "##0"; }
        }

        public List<string> DepthDegressions { get; private set; }

        private string _depthDegression;

        public string DepthDegression
        {
            get { return _depthDegression; }
            set {
                if (_depthDegression != value)
                {
                    _depthDegression = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<double> CompoundAngles { get; private set; }

        private double _compoundAngle;

        public double CompoundAngle
        {
            get { return _compoundAngle; }
            set {
                if (double.IsNaN(value) ? !double.IsNaN(_compoundAngle) : _compoundAngle != value)
                {
                    _compoundAngle = value;
                    OnPropertyChanged();
                }
            }
        }

        private uint _mach3Passes = 1;
        public uint Mach3Passes
        {
            get { return _mach3Passes; }
            set {
                if (double.IsNaN(value) ? !double.IsNaN(_mach3Passes) : _mach3Passes != value)
                {
                    _mach3Passes = value;
                    OnPropertyChanged();
                }
            }
        }

        private uint _mach3PassesExecuted = 0;
        public uint Mach3PassesExecuted
        {
            get { return _mach3PassesExecuted; }
            set  {
                if (double.IsNaN(value) ? !double.IsNaN(_mach3PassesExecuted) : _mach3PassesExecuted != value)
                {
                    _mach3PassesExecuted = value;
                    OnPropertyChanged();
                }
            }
        }

        private Lathe.Thread.Side _side = Lathe.Thread.Side.Outside;
        public Lathe.Thread.Side Side
        {
            get { return _side; }
            set {
                if (value != Lathe.Thread.Side.Both && value != _side)
                {
                    _side = value;
                    OnPropertyChanged();
                }
            }
        }

        private Lathe.Thread.Side _sides = Lathe.Thread.Side.Outside;

        public Lathe.Thread.Side Sides
        {
            get { return _sides; }
            set
            {
                if (value != _sides)
                {
                    if ((_sides = value) != Lathe.Thread.Side.Both)
                        Side = _sides;
                    OnPropertyChanged();
                }
            }
        }

        private double _lead;
        public double Lead
        {
            get { return _lead; }
            set { _lead = value; OnPropertyChanged(); }
        }

        double _diaNom;

        public double DiameterNominal
        {
            get { return _diaNom; }
            set { _diaNom = value; OnPropertyChanged(); }
        }

        private double _diameter;

        public double Diameter // Major/Minor - https://en.wikipedia.org/wiki/Screw_thread#Diameters
        {
            get { return _diameter; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_diameter) : _diameter != value)
                {
                    _diameter = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _diameterTolerance;
        public double DiameterTolerance
        {
            get { return _diameterTolerance; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_diameterTolerance) : _diameterTolerance != value)
                {
                    _diameterTolerance = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _pitchDiameter;

        public double PitchDiameter
        {
            get { return _pitchDiameter; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_pitchDiameter) : _pitchDiameter != value)
                {
                    _pitchDiameter = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _pitchDiameterTolerance;

        public double PitchDiameterTolerance
        {
            get { return _pitchDiameterTolerance; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_pitchDiameterTolerance) : _pitchDiameterTolerance != value)
                {
                    _pitchDiameterTolerance = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _cutDepth;

        public double CutDepth
        {
            get { return _cutDepth; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_cutDepth) : _cutDepth != value)
                {
                    _cutDepth = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _maxDiameter;

        public double MaxDiameter
        {
            get { return _maxDiameter; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_maxDiameter) : _maxDiameter != value)
                {
                    _maxDiameter = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _measurePosition;

        public double MeasurePosition
        {
            get { return _measurePosition; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_measurePosition) : _measurePosition != value)
                {
                    _measurePosition = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _taper;

        public double Taper
        {
            get { return _taper; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_taper) : _taper != value)
                {
                    _taper = value;
                    OnPropertyChanged();
                }
            }
        }

        private Visibility _showTaperControls = Visibility.Hidden;

        public Visibility TaperControlsVisibility
        {
            get { return _showTaperControls; }
            set
            {
                if (_showTaperControls != value)
                {
                    _showTaperControls = value;
                    OnPropertyChanged();
                }
            }
        }

    }

    public class ToolProperties : ViewModelBase
    {
        private double _angle = 33, _tip = 0, _tipMin = 0, _tipMax = 0;
        bool _isToolRounded = true;
        Thread.Toolshape _toolshape = Thread.Toolshape.Rounded;

        public Thread.Toolshape Shape
        {
            get { return _toolshape; }
            set
            {
                _toolshape = value;
                _isToolRounded = value == Thread.Toolshape.Rounded;
                OnPropertyChanged();
            }
        }

        public double Angle
        {
            get { return _angle; }
            set { _angle = value; OnPropertyChanged(); }
        }
        public double Tip
        {
            get { return _tip; }
            set { _tip = value; OnPropertyChanged(); }
        }

        public double TipMinimum
        {
            get { return _tipMin; }
            set { _tipMin = value; OnPropertyChanged(); }
        }

        public double TipMaximum
        {
            get { return _tipMax; }
            set { _tipMax = value; OnPropertyChanged(); }
        }
    }

    public class InchProperties : ViewModelBase
    {
        private double _inchWhole = double.NaN, _inchNum = double.NaN, _inchDenom = double.NaN;
        private bool _isReadOnly = true, _isi = true;

        public InchProperties()
        {
            SuspendNotifications = false;
        }

        public double Value // in mm
        {
            get { return ((double.IsNaN(_inchWhole) ? 0.0d : _inchWhole) + (double.IsNaN(_inchNum) ? 0.0d : (_inchNum / _inchDenom))) * 25.4d; }
        }

        public bool SuspendNotifications { get; set; }

        public bool IsReadonly
        {
            get { return _isReadOnly; }
            set
            {
                if ((_isReadOnly = value)) // TODO: TPI must be added too
                    Whole = Numerator = Denominator = double.NaN;

                OnPropertyChanged();
            }
        }
        public bool IsMetricInputReadonly
        {
            get { return _isi; }
            set { _isi = value; OnPropertyChanged(); }
        }

        public double Whole
        {
            get { return _inchWhole; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_inchWhole) : _inchWhole != value)
                {
                    _inchWhole = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Numerator
        {
            get { return _inchNum; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_inchNum) : _inchNum != value)
                {
                    _inchNum = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Denominator
        {
            get { return _inchDenom; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_inchDenom) : _inchDenom != value)
                {
                    _inchDenom = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
