
/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.03 / 2019-10-20 / Io Engineering (Terje Io)
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
using System.Windows;
using CNC.Core;
using CNC.Controls;
using CNC.View;

namespace Grbl_Config_App
{

    public partial class MainWindow : Window
    {

        public static AppConfig Profile = new AppConfig();

        public MainWindow()
        {
            InitializeComponent();

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
            }

            if (!Profile.Load(CNC.Core.Resources.IniFile))
            {
                if (MessageBox.Show("Config file not found or invalid, create new?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (!Profile.Save(CNC.Core.Resources.IniFile))
                    {
                        MessageBox.Show("Could not save config file.", Title);
                        Environment.Exit(1);
                    }
                }
                else
                    Environment.Exit(1);
            }

            if (char.IsDigit(Profile.Config.PortParams[0])) // We have an IP address
                new IPComms(Profile.Config.PortParams);
            else
                new SerialComms(Profile.Config.PortParams, Comms.ResetMode.None, App.Current.Dispatcher);

            if (!Comms.com.IsOpen)
            {
                PortDialog portsel = new PortDialog();

                var port = portsel.ShowDialog();
                if (port == null)
                    Environment.Exit(2);

                if (char.IsDigit(port[0]))
                { // We have an IP address
                    Profile.Config.PortParams = port;
                    new IPComms(Profile.Config.PortParams);
                }
                else
                {
                    Profile.Config.PortParams = port + ":" + Profile.Config.PortParams.Substring(Profile.Config.PortParams.IndexOf(':') + 1);
                    new SerialComms(Profile.Config.PortParams, Comms.ResetMode.None, App.Current.Dispatcher);
                }
                Profile.Save(CNC.Core.Resources.IniFile);
            }

            if (!Comms.com.IsOpen)
            {
                MessageBox.Show("Unable to open connection!", Title);
                Environment.Exit(2);
            }

            System.Threading.Thread.Sleep(400); // Wait to see if MPG is polling Grbl

            if (!(Comms.com.Reply == "" || Comms.com.Reply.StartsWith("Grbl")))
            {
                MPGPending await = new MPGPending();
                await.ShowDialog();
                if (await.Cancelled)
                {
                    Comms.com.Close();
                    Environment.Exit(2);
                }
            }
        }

        #region UIEvents

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            using (new UIUtils.WaitCursor())
            {
                GrblInfo.Get();
                GrblSettings.Get();
                GrblParserState.Get();
                GrblWorkParameters.Get();
            }

            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();
            configView.Activate(true, ViewType.Startup);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            configView.Activate(false, ViewType.Shutdown);
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            About about = new About(this);
            about.ShowDialog();
        }

        #endregion  

        private string GetArg(string[] args, int i)
        {
            return i < args.GetLength(0) ? args[i] : null;
        }
    }
}
