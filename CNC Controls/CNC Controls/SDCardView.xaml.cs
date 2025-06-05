/*
 * SDCardView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.46 / 2025-03-07 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2025, Io Engineering (Terje Io)
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
                CanUpload = GrblInfo.UploadProtocol != string.Empty && (DataContext as GrblViewModel).SDCardMountStatus != SDState.Undetected;
                CanDelete = GrblInfo.Build >= 20210421;
                CanViewAll = GrblInfo.Build >= 20230312;
                CanRewind = GrblInfo.IsGrblHAL;

                if (GrblInfo.HasSDCard && (DataContext as GrblViewModel).SDCardMountStatus == SDState.Undetected)
                {
                    GrblSDCard.Clear();
                    (DataContext as GrblViewModel).Message = (string)FindResource("NoCard");
                } else
                    GrblSDCard.Load(DataContext as GrblViewModel, ViewAll);
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

        public static readonly DependencyProperty ViewAllProperty = DependencyProperty.Register(nameof(ViewAll), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool ViewAll
        {
            get { return (bool)GetValue(ViewAllProperty); }
            set { SetValue(ViewAllProperty, value); }
        }

        public static readonly DependencyProperty CanViewAllProperty = DependencyProperty.Register(nameof(CanViewAll), typeof(bool), typeof(SDCardView), new PropertyMetadata(false));
        public bool CanViewAll
        {
            get { return (bool)GetValue(CanViewAllProperty); }
            set { SetValue(CanViewAllProperty, value); }
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

        private bool isMacro (string filename)
        {
            return filename.ToLower().EndsWith(".macro");
        }

        private void DownloadRun_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null && !isMacro((string)currentFile["Name"]) && MessageBox.Show(string.Format((string)FindResource("DownloandRun"), (string)currentFile["Name"]), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
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

            file.Filter = string.Format("GCode files ({0})|{0}|GCode macros (*.macro)|*.macro|Text files (*.txt)|*.txt|All files (*.*)|*.*", FileUtils.ExtensionsToFilter(GCode.FileTypes));

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
                                int port = GrblSettings.GetInteger(grblHALSetting.FtpPort0);
                                if(port == -1)
                                    port = GrblSettings.GetInteger(grblHALSetting.FtpPort1);
                                if (port == -1)
                                    port = GrblSettings.GetInteger(grblHALSetting.FtpPort2);
                                client.Credentials = new NetworkCredential("grblHAL", "grblHAL");
                                client.UploadFile(string.Format("ftp://{0}:{1}//{2}", GrblInfo.IpAddress, port == -1 ? 21 : port, System.IO.Path.GetFileName(filename)), WebRequestMethods.Ftp.UploadFile, filename);
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

                GrblSDCard.Load(model, ViewAll);
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
        private void ViewAll_Click(object sender, RoutedEventArgs e)
        {
            GrblSDCard.Load(DataContext as GrblViewModel, ViewAll);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(string.Format((string)FindResource("DeleteFile"), (string)currentFile["Name"]), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
            {
                Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_UNLINK + (string)currentFile["Name"]);
                GrblSDCard.Load(DataContext as GrblViewModel, ViewAll);
            }
        }

        private void RunFile()
        {
            if (currentFile != null)
            {
                (DataContext as GrblViewModel).Message = string.Empty;

                if ((bool)currentFile["Invalid"])
                {
                    MessageBox.Show(string.Format(((string)FindResource("IllegalName")).Replace("\\n", "\r\r"), (string)currentFile["Name"]), "ioSender",
                                     MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    if (GrblInfo.ExpressionsSupported && isMacro((string)currentFile["Name"])) {
                        string filename = ((string)currentFile["Name"]).ToLower();
                        filename = filename.Substring(0, filename.LastIndexOf(".macro"));
                        int pos = filename.LastIndexOf("p");
                        if(pos >= 0)
                        {
                            int macro;
                            if(int.TryParse(filename.Substring(pos + 1), out macro) && macro >= 100)
                            {
                                if(MessageBox.Show(string.Format((string)FindResource("RunMacro"), macro), "ioSender",
                                                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                {
                                    Comms.com.WriteCommand("G65P" + macro.ToString());
                                }

                            }
                        }
                        return;
                    }
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
        private static int id = 0;
        private static GrblViewModel grbl;

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

        public static void Clear()
        {
            data.Clear();
        }

        public static void Load(GrblViewModel model, bool ViewAll)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();

            grbl = model;

            data.Clear();

            if (GrblInfo.HasSDCard && grbl.SDCardMountStatus == SDState.Unmounted)
            {
                Comms.com.PurgeQueue();

                new Thread(() =>
                {
                    res = WaitFor.AckResponse<string>(
                        cancellationToken,
                        response => CardCheck(response),
                        a => model.OnResponseReceived += a,
                        a => model.OnResponseReceived -= a,
                        1500, () => Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_MOUNT));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();
            }

            if (!GrblInfo.HasSDCard || grbl.SDCardMountStatus == SDState.Mounted || grbl.SDCardMountStatus == SDState.Detected)
            {
                Comms.com.PurgeQueue();

                id = 0;
                res = null;
                model.Silent = true;

                new Thread(() =>
                {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    2000, () => Comms.com.WriteCommand(ViewAll ? GrblConstants.CMD_SDCARD_DIR_ALL : GrblConstants.CMD_SDCARD_DIR));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                model.Silent = false;

                data.AcceptChanges();
            }
        }

        private static void CardCheck(string data)
        {
            if(data == "ok")
                grbl.SDCardMountStatus = SDState.Mounted;
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
            else if (data == "error:62" || data == "error:64")
                grbl.SDCardMountStatus = SDState.Unmounted;
        }
    }
}
