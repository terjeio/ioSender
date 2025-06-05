/*
 * GrblConfigControl.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.46 / 2025-05-23 / Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading;
using CNC.Core;

namespace CNC.Controls
{
    public partial class GrblConfigControl : UserControl, IGrblConfigTab
    {
        private bool active = false;
        private Widget curSetting = null;
        private GrblViewModel model = null;

        private string retval;

        public GrblConfigControl()
        {
            InitializeComponent();
        }

        private void ConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            if(!(DataContext is WidgetViewModel))
                DataContext = new WidgetViewModel(DataContext as GrblViewModel);

            model = (DataContext as WidgetViewModel).Grbl;

            dgrSettings.Visibility = GrblInfo.HasEnums ? Visibility.Collapsed : Visibility.Visible;
            searchField.Visibility = !GrblInfo.HasEnums ? Visibility.Collapsed : Visibility.Visible;
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

        #region Methods required by GrblConfigTab interface

        public GrblConfigType GrblConfigType { get { return GrblConfigType.Base; } }

        public void Activate(bool activate)
        {
            if (model != null)
            {
                btnSave.IsEnabled = !model.IsCheckMode;
                model.Message = string.Empty;

                if (activate)
                {
                    if (active) return;

                    active = true;

                    using (new UIUtils.WaitCursor())
                    {
                        GrblSettings.Load();
                    }

                    if(treeView.SelectedItem != null && treeView.SelectedItem is GrblSettingDetails)
                        ShowSetting(treeView.SelectedItem as GrblSettingDetails, false);
                    else if (dgrSettings.SelectedItem != null)
                        ShowSetting(dgrSettings.SelectedItem as GrblSettingDetails, false);
                }
                else
                {
                    active = false;
                    if (curSetting != null)
                        curSetting.Assign();

                    if (GrblSettings.HasChanges())
                    {
                        if (MessageBox.Show((string)FindResource("SaveSettings"), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                            GrblSettings.Save();
                    }
                }
            }
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
                if (curSetting != null)
                    ShowSetting(curSetting.Setting, false);
            }
        }

        void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            if(GrblSettings.Backup(string.Format("{0}settings.txt", Core.Resources.ConfigPath)))
                model.Message = string.Format((string)FindResource("SettingsWritten"), "settings.txt");
            GrblWorkParameters.Backup(string.Format("{0}offsets.nc", Core.Resources.ConfigPath));
        }

        private void ShowSetting(GrblSettingDetails setting, bool assign)
        {
            details.Visibility = Visibility.Visible;

            if (curSetting != null)
            {
                if (assign)
                    curSetting.Assign();
                canvas.Children.Clear();
                curSetting.Dispose();
            }
            searchField.Value = setting.Id;
            txtDescription.Text = setting.Description;
            curSetting = new Widget(this, new WidgetProperties(setting), canvas);
            curSetting.IsEnabled = true;

            canvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            txtDescription.Height = Math.Max(ActualHeight - 40d - canvas.DesiredSize.Height, 0d);
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

                if (MessageBox.Show(string.Format((string)FindResource("SettingsError"), scmd, retval), "ioSender" + (details == null ? "" : " - " + details.Name), MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                    return false;
            }
            else if (res == false && MessageBox.Show(string.Format((string)FindResource("SettingsTimeout"), scmd), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                return false;

            return true;
        }

        public bool LoadFile(string filename)
        {
            int pos, id, mismatch = 0;
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
                    if (MessageBox.Show(((string)FindResource("SettingsFail")).Replace("\\n", "\r\r"), e.Message, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
                MessageBox.Show((string)FindResource("SettingsInvalid"));
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
                        if (MessageBox.Show(string.Format((string)FindResource("SettingsError"), cmd, retval), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                            break;
                    }
                    else if (res == false && MessageBox.Show(string.Format((string)FindResource("SettingsTimeout"), cmd), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
                        break;
                }

                foreach (var d in dep)
                {
                    if (settings.ContainsKey(d))
                    {
                        var setting = new KeyValuePair<int, string>(d, settings[d]);
                        if (GrblSettings.HasSetting((GrblSetting)setting.Key))
                        {
                            if (!SetSetting(setting))
                            {
                                settings.Clear();
                                break;
                            }
                        }
                        else
                            mismatch++;
                    }
                }

                foreach (var setting in settings)
                {
                    if (GrblSettings.HasSetting((GrblSetting)setting.Key))
                    {
                        if (!dep.Contains(setting.Key))
                        {
                            if (!SetSetting(setting))
                                break;
                        }
                    }
                    else
                        mismatch++;
                }

                if (lines.Count > 0 && lines[0] == "%")
                    Comms.com.WriteCommand("%");

                using (new UIUtils.WaitCursor())
                {
                    GrblSettings.Load();
                }
            }

            model.Message = string.Empty;

            if (mismatch > 0)
                MessageBox.Show(string.Format((string)FindResource("SettingsReloadMismatch"), mismatch), "ioSender", MessageBoxButton.OK, MessageBoxImage.Exclamation);

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

            file.InitialDirectory = Core.Resources.ConfigPath;
            file.Title = (string)FindResource("SettingsRestore");

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
                ShowSetting(e.AddedItems[0] as GrblSettingDetails, true);
        }
        #endregion

        private void treeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e != null && e.NewValue is GrblSettingDetails && (e.NewValue as GrblSettingDetails).Value != null)
                ShowSetting(e.NewValue as GrblSettingDetails, true);
            else
                details.Visibility = Visibility.Hidden;
        }

        private void searchField_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == System.Windows.Input.Key.Return && e.IsDown)
            {
                var setting = GrblSettings.Get((GrblSetting)searchField.Value);

                if (setting != null)
                {
                    foreach (object g in treeView.Items)
                    {
                        if ((g as GrblSettingGroup).Id == setting.GroupId)
                        {
                            TreeViewItem gitm = (TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(g);
                            gitm.IsExpanded = true;
                            gitm.UpdateLayout();
                            gitm.BringIntoView();
                            foreach (object s in gitm.Items)
                            {
                                if ((s as GrblSettingDetails).Id == setting.Id)
                                {
                                    TreeViewItem sitm = (TreeViewItem)gitm.ItemContainerGenerator.ContainerFromItem(s);
                                    if (sitm != null)
                                    {
                                        sitm.IsSelected = true;
                                        sitm.BringIntoView();
//                                        sitm.Focus();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ConfigView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            txtDescription.Height = e.NewSize.Height - 40d - canvas.ActualHeight;
        }
    }
}
