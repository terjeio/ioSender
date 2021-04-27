/*
 * GrblConfigView.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.31 / 2021-04-27 / Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using CNC.Core;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading;

namespace CNC.Controls
{
    public partial class GrblConfigView : UserControl, ICNCView
    {
        private Widget curSetting = null;
        private GrblViewModel model = null;

        private string retval;

        public GrblConfigView()
        {
            InitializeComponent();

            DataContextChanged += GrblConfigView_DataContextChanged;
        }

        private void GrblConfigView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GrblViewModel)
                model = (GrblViewModel)e.OldValue;
        }

        private void ConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = new WidgetViewModel();

            dgrSettings.Visibility = GrblInfo.HasEnums ? Visibility.Collapsed : Visibility.Visible;
            treeView.Visibility = !GrblInfo.HasEnums ? Visibility.Collapsed : Visibility.Visible;
            details.Visibility = GrblInfo.HasEnums && curSetting == null ? Visibility.Hidden : Visibility.Visible;

            if (GrblInfo.HasEnums)
            {
                treeView.ItemsSource = GrblSettingGroups.Groups;
            }
            else
            {
                dgrSettings.DataContext = GrblSettings.Settings;
                dgrSettings.SelectedIndex = 0;
            }
        }

        #region Methods required by CNCView interface

        public ViewType ViewType { get { return ViewType.GRBLConfig; } }
        public bool CanEnable { get { return true; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (model != null)
            {
                btnSave.IsEnabled = !model.IsCheckMode;
                model.Message = string.Empty;
            }
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        #region UIEvents

        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (curSetting != null)
                curSetting.Assign();

            model.Message = string.Empty;

            GrblSettings.Save();
        }

        void btnReload_Click(object sender, RoutedEventArgs e)
        {
            using(new UIUtils.WaitCursor()) {
                GrblSettings.Load();
            }
        }

        void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            GrblSettings.Backup(string.Format("{0}settings.txt", Core.Resources.Path));
            model.Message = "All settings written to settings.txt in the sender folder.";
        }

        private bool SetSetting (KeyValuePair<int, string> setting)
        {
            bool? res = null;
            CancellationToken cancellationToken = new CancellationToken();
            var scmd = string.Format("${0}={1}", setting.Key, setting.Value);

            retval = string.Empty;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    response => Process(response),
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived -= a,
                    400, () => Comms.com.WriteCommand(scmd));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            if (retval != string.Empty)
            {
                if(retval.StartsWith("error:"))
                {
                    var msg = GrblErrors.GetMessage(retval.Substring(6));
                    if(msg != retval)
                        retval += " - \"" + msg + "\"";
                }

                var details = GrblSettings.Get((GrblSetting)setting.Key);

                if (MessageBox.Show(string.Format("Setting {0} returned {1}, continue?", scmd, retval), "ioSender" + (details == null ? "" : " - " + details.Name), MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                    return false;
            }
            else if (res == false && MessageBox.Show(string.Format("Timed out while setting {0} , continue?", scmd), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                return false;

            return true;
        }

        public bool LoadFile(string filename)
        {
            int pos, id;
            List<string> lines = new List<string>();
            List<int> dep = new List<int>();
            Dictionary<int, string> settings = new Dictionary<int, string>();
            FileInfo file = new FileInfo(filename);
            StreamReader sr = file.OpenText();

            string block = sr.ReadLine();

            while (block != null)
            {
                block = block.Trim();
                try
                {
                    if (lines.Count == 0 && model.IsGrblHAL && block == "%")
                        lines.Add(block);
                    else if (block.StartsWith("$") && (pos = block.IndexOf('=')) > 1)
                    {
                        if (int.TryParse(block.Substring(1, pos - 1), out id))
                            settings.Add(id, block.Substring(pos + 1));
                        else
                            lines.Add(block);
                    }

                    block = sr.ReadLine();
                }
                catch (Exception e)
                {
                    if (MessageBox.Show("Bummer...\r\rContinue loading?", e.Message, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        block = sr.ReadLine();
                    else
                    {
                        block = null;
                        settings.Clear();
                        lines.Clear();
                    }
                }
            }

            sr.Close();

            if (settings.Count == 0)
                MessageBox.Show("The file does not contain any settings.", "ioSender");
            else
            {
                bool? res = null;
                CancellationToken cancellationToken = new CancellationToken();

                // List of settings that have other dependent settings and have to be set before them
                dep.Add((int)GrblSetting.HomingEnable);

                foreach (var cmd in lines)
                {
                    res = null;
                    retval = string.Empty;

                    new Thread(() =>
                    {
                        res = WaitFor.AckResponse<string>(
                            cancellationToken,
                            response => Process(response),
                            a => model.OnResponseReceived += a,
                            a => model.OnResponseReceived -= a,
                            400, () => Comms.com.WriteCommand(cmd));
                    }).Start();

                    while (res == null)
                        EventUtils.DoEvents();

                    if (retval != string.Empty)
                    {
                        if (MessageBox.Show(string.Format("Setting {0} returned {1}, continue?", cmd, retval), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                            break;
                    }
                    else if (res == false && MessageBox.Show(string.Format("Timed out while setting {0} , continue?", cmd), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                        break;
                }

                foreach (var d in dep)
                {
                    if (settings.ContainsKey(d))
                    {
                        if (!SetSetting(new KeyValuePair<int, string>(d, settings[d])))
                        {
                            settings.Clear();
                            break;
                        }
                    }
                }

                foreach (var setting in settings)
                {
                    if (!dep.Contains(setting.Key)) {
                        if(!SetSetting(setting))
                            break;
                    }
                }

                if (lines[0] == "%")
                    Comms.com.WriteCommand("%");

                using (new UIUtils.WaitCursor())
                {
                    GrblSettings.Load();
                }
            }

            model.Message = string.Empty;

            return settings.Count > 0;
        }

        private void Process(string data)
        {
            if (data != "ok")
                retval = data;
        }

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog file = new OpenFileDialog();

            file.InitialDirectory = Core.Resources.Path;
            file.Title = "Restore settings from file";

            file.Filter = string.Format("Text files (*.txt)|*.txt");

            if (file.ShowDialog() == true)
            {
                using (new UIUtils.WaitCursor())
                {
                    LoadFile(file.FileName);
                }
            }
        }

        private void dgrSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                details.Visibility = Visibility.Visible;

                if (curSetting != null)
                {
                    curSetting.Assign();
                    canvas.Children.Clear();
                    curSetting.Dispose();
                }

                var setting = e.AddedItems[0] as GrblSettingDetails;
                txtDescription.Text = setting.Description;
                curSetting = new Widget(this, new WidgetProperties(setting), canvas);
                curSetting.IsEnabled = true;
            }
        }
        #endregion

        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e != null && e.NewValue is GrblSettingDetails && (e.NewValue as GrblSettingDetails).Value != null)
            {
                details.Visibility = Visibility.Visible;

                if (curSetting != null)
                {
                    curSetting.Assign();
                    canvas.Children.Clear();
                    curSetting.Dispose();
                }

                var setting = e.NewValue as GrblSettingDetails;
                txtDescription.Text = setting.Description;
                curSetting = new Widget(this, new WidgetProperties(setting), canvas);
                curSetting.IsEnabled = true;
            }
            else
                details.Visibility = Visibility.Hidden;
        }
    }
}
