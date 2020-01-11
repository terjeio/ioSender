/*
 * SDCardView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2020-01-07 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2020, Io Engineering (Terje Io)
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

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Core;
using CNC.View;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for SDCardView.xaml
    /// </summary>
    public partial class SDCardView : UserControl, CNCView
    {
        public delegate void FileSelectedHandler(string filename);
        public event FileSelectedHandler FileSelected;

        private DataRow currentFile = null;

        public SDCardView()
        {
            InitializeComponent();
        }

        #region Methods and properties required by IRenderer interface

        public ViewType mode { get { return ViewType.SDCard; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
                GrblSDCard.Load();
        }

        public void CloseFile()
        {
        }

        #endregion

        private void SDCardView_Loaded(object sender, RoutedEventArgs e)
        {
            dgrSDCard.DataContext = GrblSDCard.Files;
      //      dgrSDCard.SelectedIndex = 0;
        }

        void dgrSDCard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentFile = e.AddedItems.Count == 1 ? ((DataRowView)e.AddedItems[0]).Row : null;
        }

        private void dgrSDCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(currentFile != null)
            {
                if ((bool)currentFile["Invalid"])
                {
                    MessageBox.Show(string.Format("File: \"{0}\"\r\r!,?,~ and SPACE is not supported in filenames, please rename.", (string)currentFile["Name"]), "Unsupported characters in filename",
                                     MessageBoxButton.OK, MessageBoxImage.Error);
                } else {
                    FileSelected?.Invoke("SDCard:" + (string)currentFile["Name"]);
                    Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_RUN + (string)currentFile["Name"]);
                }
            }
        }
    }

    public static class GrblSDCard
    {
        private static DataTable data;
        private static bool mounted = false;
        private static int id = 0;

        static GrblSDCard()
        {
            data = new DataTable("Filelist");

            data.Columns.Add("Id", typeof(int));
            data.Columns.Add("Dir", typeof(string));
            data.Columns.Add("Name", typeof(string));
            data.Columns.Add("Size", typeof(int));
            data.Columns.Add("Invalid", typeof(bool));
            data.PrimaryKey = new DataColumn[] { data.Columns["Id"] };
        }

        public static DataView Files { get { return data.DefaultView; } }
        public static bool Loaded { get { return data.Rows.Count > 0; } }

        public static void Load()
        {
            data.Clear();

            Comms.com.PurgeQueue();

            if (!mounted)
            {
                Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_MOUNT);
                Comms.com.AwaitAck();

                mounted = Comms.com.CommandState == Comms.State.ACK;
            }

            if (mounted)
            {
                id = 0;
                Comms.com.DataReceived += new DataReceivedHandler(Process);

                Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_DIR);
                Comms.com.AwaitAck();

                Comms.com.DataReceived -= GrblSDCard.Process;

                data.AcceptChanges();
            }
        }

        private static void Process(string data)
        {
            string filename = "";
            int filesize = 0;
            bool invalid = false;

            if (data.StartsWith("[FILE:"))
            {
                string[] parameters = data.TrimEnd(']').Split('|');
                foreach (string parameter in parameters)
                {
                    string[] valuepair = parameter.Split(':');
                    switch (valuepair[0])
                    {
                        case "[FILE":
                            filename = valuepair[1];
                            break;

                        case "SIZE":
                            filesize = int.Parse(valuepair[1]);
                            break;

                        case "INVALID":
                            invalid = true;
                            break;
                    }
                }
                GrblSDCard.data.Rows.Add(new object[] { id++, "", filename, filesize, invalid });
            }
        }
    }
}
