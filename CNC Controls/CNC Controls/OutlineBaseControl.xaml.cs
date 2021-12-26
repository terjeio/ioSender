/*
 * OutlineBaseControl.xaml.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-08 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2021, Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    public partial class OutlineBaseControl : UserControl
    {
        public OutlineBaseControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty FeedRateProperty = DependencyProperty.Register(nameof(FeedRate), typeof(int), typeof(OutlineBaseControl), new PropertyMetadata(500));
        public int FeedRate
        {
            get { return (int)GetValue(FeedRateProperty); }
            set { SetValue(FeedRateProperty, value); }
        }

        private void button_Go(object sender, RoutedEventArgs e)
        {
            var model = DataContext as GrblViewModel;

            if (AppConfig.Settings.Base.OutlineFeedRate != FeedRate)
            {
                AppConfig.Settings.Base.OutlineFeedRate = FeedRate;
                AppConfig.Settings.Save();
            }

            if (model.IsFileLoaded)
            {
                string gcode = string.Format("G90G1F{0}\r", FeedRate.ToString());

                gcode += string.Format("X{0}Y{1}\r", model.ProgramLimits.MinX.ToInvariantString(), model.ProgramLimits.MinY.ToInvariantString());
                gcode += string.Format("Y{0}\r", model.ProgramLimits.MaxY.ToInvariantString());
                gcode += string.Format("X{0}\r", model.ProgramLimits.MaxX.ToInvariantString());
                gcode += string.Format("Y{0}\r", model.ProgramLimits.MinY.ToInvariantString());
                gcode += string.Format("X{0}\r", model.ProgramLimits.MinX.ToInvariantString());

                model.ExecuteCommand(gcode);
            }
        }

        private void OutlineControl_Loaded(object sender, RoutedEventArgs e)
        {
            FeedRate = AppConfig.Settings.Base.OutlineFeedRate;
        }
    }
}
