/*
 * LimitsControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2019-10-21 / Io Engineering (Terje Io)
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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for LimitsControl.xaml
    /// </summary>
    public partial class LimitsControl : UserControl
    {
        private bool _suppressUnitCommand = false;

        public LimitsControl()
        {
            InitializeComponent();
            DataContextChanged += LimitsControl_DataContextChanged;
        }

        private void LimitsControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GrblViewModel oldModel)
                oldModel.PropertyChanged -= Model_PropertyChanged;

            if (e.NewValue is GrblViewModel model)
            {
                model.PropertyChanged += Model_PropertyChanged;
                SyncRadioButtons(model.IsMetric);
            }
        }

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GrblViewModel.IsMetric))
            {
                var model = (GrblViewModel)sender;
                SyncRadioButtons(model.IsMetric);
            }
            else if (e.PropertyName == nameof(GrblViewModel.FileName))
            {
                if (GCode.File.IsLoaded)
                {
                    bool fileIsMetric = !GCode.File.Parser.IsImperial;
                    SetUnits(fileIsMetric);
                }
            }
        }

        private void SyncRadioButtons(bool isMetric)
        {
            _suppressUnitCommand = true;
            rbMM.IsChecked = isMetric;
            rbIN.IsChecked = !isMetric;
            _suppressUnitCommand = false;
        }

        private void SetUnits(bool isMetric)
        {
            if (Comms.com != null && Comms.com.IsOpen)
            {
                bool restoreSingleBlock = DataContext is GrblViewModel model && model.Signals[Signals.SingleBlock];

                try
                {
                    if (restoreSingleBlock)
                        Comms.com.WriteByte((byte)GrblConstants.CMD_SINGLE_BLOCK_TOGGLE);

                    Comms.com.WriteCommand(isMetric ? "G21" : "G20");
                    Comms.com.AwaitAck();
                    Comms.com.WriteCommand(isMetric ? "$13=0" : "$13=1");
                    Comms.com.AwaitAck();
                    Comms.com.WriteCommand("$G");
                }
                finally
                {
                    if (restoreSingleBlock)
                        Comms.com.WriteByte((byte)GrblConstants.CMD_SINGLE_BLOCK_TOGGLE);
                }
            }
            else
                SyncRadioButtons(isMetric);
        }

        private void rbIN_Checked(object sender, RoutedEventArgs e)
        {
            if (!_suppressUnitCommand)
                SetUnits(false);
        }

        private void rbMM_Checked(object sender, RoutedEventArgs e)
        {
            if (!_suppressUnitCommand)
                SetUnits(true);
        }
    }
}
