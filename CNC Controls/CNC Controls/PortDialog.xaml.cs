/*
 * PortDialog.xaml.cs - part of CNC Controls library
 *
 * v0.07 / 2020-02-21 / Io Engineering (Terje Io)
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

using System.Windows;
using CNC.Core;
using System;

namespace CNC.Controls
{
    public partial class PortDialog : Window
    {
        private string port = null;
        public PortDialog()
        {
            InitializeComponent();

            DataContext = new SerialPorts();
        }

        private void CbxPorts_DropDownOpened(object sender, System.EventArgs e)
        {
            ((SerialPorts)DataContext).Refresh();
        }

        private bool PortAvailable(string port)
        {
            bool found = false;

            foreach (string p in ((SerialPorts)DataContext).PortNames)
                found = found || p == port;

            return found;
        }

        public string ShowDialog(string orgport)
        {
            if (!string.IsNullOrEmpty(orgport)) {
                string portname = orgport.Substring(0, orgport.IndexOf(':'));
                if (PortAvailable(portname))
                {
                    ((SerialPorts)DataContext).SelectedPort = portname;
                    string[] values = orgport.Split(':')[1].Split(',');
                    if(values.Length > 5)
                    {
                        Comms.ResetMode mode = Comms.ResetMode.None; 
                        Enum.TryParse(values[5], true, out mode);
                        if(mode != Comms.ResetMode.None)
                        {
                            foreach (ConnectMode m in ((SerialPorts)DataContext).ConnectModes)
                                if (m.Mode == mode)
                                    ((SerialPorts)DataContext).SelectedMode = m;
                        }
                    }
                }
            }

            ShowDialog();

            return port;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            port = ((SerialPorts)DataContext).SelectedPort;

            if (((SerialPorts)DataContext).SelectedMode.Mode != Comms.ResetMode.None)
                port += "!" + ((SerialPorts)DataContext).SelectedMode.Mode.ToString();

            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
