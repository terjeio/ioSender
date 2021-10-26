/*
 * BaseViewModel.cs - part of CNC Controls Lathe library
 *
 * v0.03 / 2020-01-28 / Io Engineering (Terje Io)
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

using System.Collections.ObjectModel;
using System.Linq;
using CNC.Core;
using System;
using System.Globalization;

namespace CNC.Controls.Lathe
{
    public class BaseViewModel : MeasureViewModel
    {
        double _rpm, _taper;
        double _xlen = double.NaN, _xstart = double.NaN, _zlen = double.NaN, _zstart = double.NaN;
        uint _springPasses = 0, _cssSpeed = 0;
        bool  _isSpringPassesEnabled = false, _isCssEnabled = false, _isTaperEnabled = false;

        Thread.Format _format = Thread.Format.LinuxCNC;

        public WizardConfig wz;

        public BaseViewModel(string profileName)
        {
            gCode = new ObservableCollection<string>();
            PassData = new ObservableCollection<string>();
            GCodeFormat = Thread.Format.LinuxCNC;
            ZLength = 10;
            ZStart = 0;

            wz = new WizardConfig(profileName);
            Profiles = wz.profile.profiles;

            PropertyChanged += BaseViewModel_PropertyChanged;
        }

        private void BaseViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsMetric))
            {
                Profile = Profile;
                OnPropertyChanged(nameof(CssUnit));
            }
        }

        public string FormatValue (double value)
        {
            return Math.Round(value, Precision).ToInvariantString(Format);
        }

        public string CssUnit { get { return IsMetric ? "m/min" : "ft/min"; } }

        public ObservableCollection<string> gCode { get; private set; }
        public ObservableCollection<string> PassData { get; private set; }

        public ActiveProfile config { get { return wz.ActiveProfile; } }

        private ObservableCollection<ProfileData> _profiles;

        public ObservableCollection<ProfileData> Profiles
        {
            get { return _profiles; }
            set
            {
                _profiles = value;
                OnPropertyChanged();
                Profile = _profiles.First();
            }
        }

        public ProfileData Profile
        {
            get { return wz.ActiveProfile.Profile; }
            set { wz.ActiveProfile.Profile = value; OnPropertyChanged(); }
        }

        public Thread.Format GCodeFormat
        {
            get { return _format; }
            set { _format = value; OnPropertyChanged(); }
        }

        public double RPM
        {
            get { return _rpm; }
            set { if (_rpm != value) { _rpm = value; OnPropertyChanged(); } }
        }

        public double XStart
        {
            get { return _xstart; }
            set { if(dbl.Assign(value, ref _xstart)) OnPropertyChanged(); }
        }

        public double XLength
        {
            get { return _xlen; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_xlen) : _xlen != value)
                {
                    _xlen = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZStart
        {
            get { return _zstart; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_zstart) : _zstart != value)
                {
                    _zstart = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ZLength
        {
            get { return _zlen; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_zlen) : _zlen != value)
                {
                    _zlen = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _targetX;

        public double XTarget
        {
            get { return _targetX; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_targetX) : _targetX != value)
                {
                    _targetX = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _targetZ;

        public double ZTarget
        {
            get { return _targetZ; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_targetZ) : _targetZ != value)
                {
                    _targetZ = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _clearanceX;

        public double XClearance
        {
            get { return _clearanceX; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_clearanceX) : _clearanceX != value)
                {
                    _clearanceX = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _clearanceZ;

        public double ZClearance
        {
            get { return _clearanceZ; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_clearanceZ) : _clearanceZ != value)
                {
                    _clearanceZ = value;
                    OnPropertyChanged();
                }
            }
        }


        public bool IsSpringPassesEnabled
        {
            get { return _isSpringPassesEnabled; }
            set
            {
                if(_isSpringPassesEnabled != value)
                {
                    _isSpringPassesEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint SpringPasses
        {
            get { return _springPasses; }
            set
            {
                _springPasses = value;
                OnPropertyChanged();
            }
        }

        public bool IsCssEnabled
        {
            get { return _isCssEnabled; }
            set
            {
                if (_isCssEnabled != value)
                {
                    _isCssEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint CssSpeed
        {
            get { return _cssSpeed; }
            set
            {
                _cssSpeed = value;
                OnPropertyChanged();
            }
        }

        public bool IsTaperEnabled
        {
            get { return _isTaperEnabled; }
            set
            {
                if (_isTaperEnabled != value)
                {
                    _isTaperEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
        public double Taper
        {
            get { return _taper; }
            set
            {
                _taper = value;
                OnPropertyChanged();
            }
        }

        private double _feedRate = 0;

        public double FeedRate
        {
            get { return _feedRate; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_feedRate) : _feedRate != value)
                {
                    _feedRate = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _feedRateLast = 0;

        public double FeedRateLastPass
        {
            get { return _feedRateLast; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_feedRateLast) : _feedRateLast != value)
                {
                    _feedRateLast = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _passdepth;

        public double Passdepth
        {
            get { return _passdepth; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_passdepth) : _passdepth != value)
                {
                    _passdepth = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _passdepthLast;

        public double PassdepthLastPass
        {
            get { return _passdepthLast; }
            set
            {
                if (double.IsNaN(value) ? !double.IsNaN(_passdepthLast) : _passdepthLast != value)
                {
                    _passdepthLast = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
