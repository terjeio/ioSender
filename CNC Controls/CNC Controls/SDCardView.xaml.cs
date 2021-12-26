/*
 * SDCardView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.36 / 2021-11-10 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2021, Io Engineering (Terje Io)
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
using System.Threading;
using System.Net;
using Microsoft.Win32;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for SDCardView.xaml
    /// </summary>
    public partial class SDCardView : UserControl, ICNCView
    {
        public delegate void FileSelectedHandler(string filename, bool rewind);
        public event FileSelectedHandler FileSelected;

        private DataRow currentFile = null;

        public SDCardView()
        {
            InitializeComponent();
            ctxMenu.DataContext = this;
        }

        #region Methods and properties required by IRenderer interface

        public ViewType ViewType { get { return ViewType.SDCard; } }
        public bool CanEnable { get { return !(DataContext as GrblViewModel).IsGCLock; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GrblSDCard.Load(DataContext as GrblViewModel);
                CanUpload = GrblInfo.UploadProtocol != string.Empty;
                CanDelete = GrblInfo.Build >= 20210421;
                CanRewind = GrblInfo.IsGrblHAL;
            }
            else
                (DataContext as GrblViewModel).Message = string.Empty;
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        #region Dependency properties

        public static readonly DependencyProperty RewindProperty = DependencyProperty.Register(nameof(Rewind), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool Rewind
        {
            get { return (bool)GetValue(RewindProperty); }
            set { SetValue(RewindProperty, value); }
        }

        public static readonly DependencyProperty CanRewindProperty = DependencyProperty.Register(nameof(CanRewind), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool CanRewind
        {
            get { return (bool)GetValue(CanRewindProperty); }
            set { SetValue(CanRewindProperty, value); }
        }

        public static readonly DependencyProperty CanUploadProperty = DependencyProperty.Register(nameof(CanUpload), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool CanUpload
        {
            get { return (bool)GetValue(CanUploadProperty); }
            set { SetValue(CanUploadProperty, value); }
        }

        public static readonly DependencyProperty CanDeleteProperty = DependencyProperty.Register(nameof(CanDelete), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool CanDelete
        {
            get { return (bool)GetValue(CanDeleteProperty); }
            set { SetValue(CanDeleteProperty, value); }
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
            RunFile();
        }

        private void AddBlock(string data)
        {
            GCode.File.AddBlock(data);
        }

        private void DownloadRun_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null && MessageBox.Show(string.Format((string)FindResource("DownloandRun"), (string)currentFile["Name"]), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                var model = DataContext as GrblViewModel;

                using (new UIUtils.WaitCursor())
                {
                    bool? res = null;
                    CancellationToken cancellationToken = new CancellationToken();

                    Comms.com.PurgeQueue();

                    model.SuspendProcessing = true;
                    model.Message = string.Format((string)FindResource("Downloading"), (string)currentFile["Name"]);

                    GCode.File.AddBlock((string)currentFile["Name"], CNC.Core.Action.New);

                    new Thread(() =>
                    {
                        res = WaitFor.AckResponse<string>(
                            cancellationToken,
                            response => AddBlock(response),
                            a => model.OnResponseReceived += a,
                            a => model.OnResponseReceived -= a,
                            400, () => Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_DUMP + (string)currentFile["Name"]));
                    }).Start();

                    while (res == null)
                        EventUtils.DoEvents();

                    model.SuspendProcessing = false;

                    GCode.File.AddBlock(string.Empty, CNC.Core.Action.End);
                }

                model.Message = string.Empty;

                if (Rewind)
                    Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_REWIND);

                FileSelected?.Invoke("SDCard:" + (string)currentFile["Name"], Rewind);
                Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_RUN + (string)currentFile["Name"]);

                Rewind = false;
            }
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            bool ok = false;
            string filename = string.Empty;
            OpenFileDialog file = new OpenFileDialog();

            file.Filter = string.Format("GCode files ({0})|{0}|Text files (*.txt)|*.txt|All files (*.*)|*.*", FileUtils.ExtensionsToFilter(GCode.FileTypes));

            if (file.ShowDialog() == true)
            {
                filename = file.FileName;
            }

            if (filename != string.Empty)
            {
                GrblViewModel model = DataContext as GrblViewModel;

                model.Message = (string)FindResource("Uploading");

                if (GrblInfo.UploadProtocol == "FTP")
                {
                    if (GrblInfo.IpAddress == string.Empty)
                        model.Message = (string)FindResource("NoConnection");
                    else using(new UIUtils.WaitCursor())
                    {
                        model.Message = (string)FindResource("Uploading");
                        try
                        {
                            using (WebClient client = new WebClient())
                            {
                                client.Credentials = new NetworkCredential("grblHAL", "grblHAL");
                                client.UploadFile(string.Format("ftp://{0}/{1}", GrblInfo.IpAddress, filename.Substring(filename.LastIndexOf('\\') + 1)), WebRequestMethods.Ftp.UploadFile, filename);
                                ok = true;
                            }
                        }
                        catch (WebException ex)
                        {
                            model.Message = ex.Message.ToString() + " " + ((FtpWebResponse)ex.Response).StatusDescription;
                        }
                        catch (System.Exception ex)
                        {
                            model.Message = ex.Message.ToString();
                        }
                    }
                }
                else
                {
                    model.Message = (string)FindResource("Uploading");
                    YModem ymodem = new YModem();
                    ymodem.DataTransferred += Ymodem_DataTransferred;
                    ok = ymodem.Upload(filename);
                }

                if(!(GrblInfo.UploadProtocol == "FTP" && !ok))
                    model.Message = (string)FindResource(ok ? "TransferDone" : "TransferAborted");

                GrblSDCard.Load(model);
            }
        }

        private void Ymodem_DataTransferred(long size, long transferred)
        {
            GrblViewModel model = DataContext as GrblViewModel;
            model.Message = string.Format((string)FindResource("Transferring"), transferred, size);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            RunFile();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)FindResource("DeleteFile"), (string)currentFile["Name"]), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_UNLINK + (string)currentFile["Name"]);
                GrblSDCard.Load(DataContext as GrblViewModel);
            }
        }

        private void RunFile()
        {
            if (currentFile != null)
            {
                if ((bool)currentFile["Invalid"])
                {
                    MessageBox.Show(string.Format(((string)FindResource("IllegalName")).Replace("\\n", "\r\r"), (string)currentFile["Name"]), "ioSender",
                                     MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    if (Rewind)
                    {
                        Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_REWIND);
                    }
                    FileSelected?.Invoke("SDCard:" + (string)currentFile["Name"], Rewind);
                    Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_RUN + (string)currentFile["Name"]);
                    Rewind = false;
                }
            }
        }
    }

    public static class GrblSDCard
    {
        private static DataTable data;
        private static bool? mounted = null;
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

        public static void Load(GrblViewModel model)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            data.Clear();

            if (mounted == null)
            {
                Comms.com.PurgeQueue();

                new Thread(() =>
                {
                    mounted = WaitFor.AckResponse<string>(
                        cancellationToken,
                        null,
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        500, () => Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_MOUNT));
                }).Start();

                while (mounted == null)
                    EventUtils.DoEvents();
            }

            if (mounted == true)
            {
                Comms.com.PurgeQueue();

                id = 0;
                model.Silent = true;

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => Process(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        2000, () => Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_DIR));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                model.Silent = false;

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
