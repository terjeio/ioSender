/*
 * ProbingView.xaml.cs - part of CNC Probing library
 *
 * v0.37 / 2022-02-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2022, Io Engineering (Terje Io)
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
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{

    /// <summary>
    /// Interaction logic for ProbingView.xaml
    /// </summary>
    public partial class ProbingView : UserControl, ICNCView
    {
        private static bool keyboardMappingsOk = false;

        private bool jogEnabled = false, probeTriggered = false, probeDisconnected = false, cycleStartSignal = false;
        private ProbingViewModel model = null;
        private ProbingProfiles profiles = new ProbingProfiles();
        private GrblViewModel grbl = null;
        private IInputElement focusedControl = null;

        public ProbingView()
        {
            InitializeComponent();

            profiles.Load();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!keyboardMappingsOk && DataContext is GrblViewModel)
            {
                grbl = (DataContext as GrblViewModel);
                KeypressHandler keyboard = grbl.Keyboard;

                keyboardMappingsOk = true;

                keyboard.AddHandler(Key.R, ModifierKeys.Alt, StartProbe, this);
                keyboard.AddHandler(Key.S, ModifierKeys.Alt, StopProbe, this);
                keyboard.AddHandler(Key.C, ModifierKeys.Alt, ProbeConnectedToggle, this);

                DataContext = model = new ProbingViewModel(DataContext as GrblViewModel, profiles);

                grbl.OnCameraProbe += addCameraPosition;
            }
        }

        private void addCameraPosition(Position position)
        {
            if (grbl.IsProbing)
            {
                if(model.CameraPositions == 0)
                {
                    model.PreviewText = string.Empty;
                    model.PreviewEnable = true;
                }

                model.Positions.Add(position);
                var positions = model.CameraPositions = model.Positions.Count;

                if(positions == model.CameraPositions) // model.CameraPositions may have been changed elsewhere!
                    model.PreviewText += (model.PreviewText == string.Empty ? string.Empty : "\n") + string.Format((string)FindResource("CameraPosition"), model.CameraPositions, position.X.ToInvariantString(), position.Y.ToInvariantString());

                Jog.Focus();
            }
        }

        private static IProbeTab getView(TabItem tab)
        {
            IProbeTab view = null;

            foreach (UserControl uc in UIUtils.FindLogicalChildren<UserControl>(tab))
            {
                if (uc is IProbeTab)
                {
                    view = (IProbeTab)uc;
                    break;
                }
            }

            return view;
        }

        private bool StopProbe(Key key)
        {
            getView(tab.SelectedItem as TabItem)?.Stop();

            return true;
        }

        private bool StartProbe(Key key)
        {
            focusedControl = Keyboard.FocusedElement;
            getView(tab.SelectedItem as TabItem)?.Start();

            return true;
        }

        private bool ProbeConnectedToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_PROBE_CONNECTED_TOGGLE);
            return true;
        }

        private bool FnKeyHandler(Key key)
        {
            if (!model.Grbl.IsJobRunning)
            {
                int id = int.Parse(key.ToString().Substring(1));
                var macro = AppConfig.Settings.Macros.FirstOrDefault(o => o.Id == id);
                if (macro != null && MessageBox.Show(string.Format((string)FindResource("RunMacro"), macro.Name), "Run macro", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    model.Grbl.ExecuteMacro(macro.Code);
                    return true;
                }
            }
            return false;
        }

        private void DisplayPosition(GrblViewModel grbl)
        {
            model.Position = string.Format("X:{0}  Y:{1}  Z:{2} {3} {4}",
                                            grbl.Position.X.ToInvariantString(grbl.Format),
                                             grbl.Position.Y.ToInvariantString(grbl.Format),
                                              grbl.Position.Z.ToInvariantString(grbl.Format),
                                               probeTriggered ? "P" : "",
                                                probeDisconnected ? "D" : "");
        }

        private void Grbl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var grbl = sender as GrblViewModel;

            switch (e.PropertyName) {

                case nameof(GrblViewModel.IsJobRunning):
                    foreach (TabItem tabitem in tab.Items)
                        tabitem.IsEnabled = !grbl.IsJobRunning || tabitem == tab.SelectedItem;
                    if (!grbl.IsJobRunning && focusedControl != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            focusedControl.Focus();
                            focusedControl = null;
                        }), DispatcherPriority.Render);
                    }
                    break;

                case nameof(GrblViewModel.Position):
                    DisplayPosition(grbl);
                    break;

                case nameof(GrblViewModel.Signals):
                    probeTriggered = grbl.Signals.Value.HasFlag(Signals.Probe);
                    probeDisconnected = grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected);
                    DisplayPosition(grbl);
                    var signals = ((GrblViewModel)sender).Signals.Value;
                    if (signals.HasFlag(Signals.CycleStart) && !signals.HasFlag(Signals.Hold) && !cycleStartSignal)
                        StartProbe(Key.R);
                    cycleStartSignal = signals.HasFlag(Signals.CycleStart);
                    break;
            }
        }

        private void ProbingView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            showProbeProperties();
        }

        private void showProbeProperties()
        {
            double height;

            if (probeProperties.Visibility == Visibility.Collapsed)
            {
                probeProperties.Visibility = Visibility.Hidden;
                probeProperties.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                height = probeProperties.DesiredSize.Height;
                probeProperties.Visibility = Visibility.Collapsed;
            }
            else
                height = probeProperties.ActualHeight;

            probeProperties.Visibility = (dp.ActualHeight - t1.ActualHeight - Jog.ActualHeight + probeProperties.ActualHeight) > height ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.Probing; } }
        public bool CanEnable { get { return DataContext is GrblViewModel ? !(DataContext as GrblViewModel).IsGCLock : !model.Grbl.IsGCLock; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if ((grbl.IsProbing = activate))
            {
                if (model.CoordinateSystems.Count == 0)
                {
                    //                   model.CoordinateSystems.Add(model.CoordinateSystem = new CoordinateSystem("Active", "0"));
                    foreach (var cs in GrblWorkParameters.CoordinateSystems)
                    {
                        if (cs.Id > 0 && cs.Id < 9)
                            model.CoordinateSystems.Add(new CoordinateSystem(cs.Code, "0"));

                        if (cs.Id == 9)
                            model.HasCoordinateSystem9 = true;
                    }
                    model.HasToolTable = GrblInfo.NumTools > 0;
                }

                if (GrblInfo.IsGrblHAL)
                    Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

                if (!model.Grbl.IsGrblHAL && !AppConfig.Settings.Jog.KeyboardEnable)
                    Jog.Visibility = Visibility.Collapsed;

                if (GrblInfo.IsGrblHAL)
                {
                    GrblParserState.Get();
                    GrblWorkParameters.Get();
                }
                else
                    GrblParserState.Get(true);

                model.DistanceMode = GrblParserState.DistanceMode;
                model.Tool = model.Grbl.Tool == GrblConstants.NO_TOOL ? "0" : model.Grbl.Tool;
                model.CanProbe = !model.Grbl.Signals.Value.HasFlag(Signals.Probe);
                model.HeightMapApplied = GCode.File.HeightMapApplied;
                int csid = GrblWorkParameters.GetCoordinateSystem(model.Grbl.WorkCoordinateSystem).Id;
                model.CoordinateSystem = csid == 0 || csid >= 9 ? 1 : csid;
                model.ReferenceToolOffset &= model.CanReferenceToolOffset;

                if (model.Grbl.IsTloReferenceSet && !double.IsNaN(model.Grbl.TloReference))
                {
                    model.TloReference = model.Grbl.TloReference;
                    model.ReferenceToolOffset = false;
                }

                Probing.Command = GrblInfo.ReportProbeResult ? "G38.3" : "G38.2";

                getView(tab.SelectedItem as TabItem)?.Activate(true);

                model.Grbl.PropertyChanged += Grbl_PropertyChanged;

                probeTriggered = model.Grbl.Signals.Value.HasFlag(Signals.Probe);
                probeDisconnected = model.Grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected);
                cycleStartSignal = model.Grbl.Signals.Value.HasFlag(Signals.CycleStart);

                DisplayPosition(model.Grbl);
            }
            else
            {
                model.Grbl.PropertyChanged -= Grbl_PropertyChanged;
                getView(tab.SelectedItem as TabItem)?.Activate(false);

                if (!model.Grbl.IsGCLock)
                {
                    // If probing alarm active unlock
                    //if(model.Grbl.GrblState.State == GrblStates.Alarm && (model.Grbl.GrblState.Substate == 4 || model.Grbl.GrblState.Substate == 5))
                    //    model.WaitForResponse(GrblConstants.CMD_UNLOCK);
                    //else
                    if (model.Grbl.GrblError != 0)
                        model.WaitForResponse("");  // Clear error

                    model.WaitForResponse(model.DistanceMode == DistanceMode.Absolute ? "G90" : "G91");
                }
            }

            model.Message = string.Empty;
            model.Grbl.Poller.SetState(activate ? AppConfig.Settings.Base.PollInterval : 0);
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
            if (!model.IsConfigControlInstantiated<ConfigControl>())
                model.ConfigControls.Add(new ConfigControl());
        }

        #endregion

        private void mnu_Click(object sender, RoutedEventArgs e)
        {
            switch ((string)((MenuItem)sender).Header)
            {
                case "Add":
                    cbxProfile.SelectedValue = profiles.Add(cbxProfile.Text, model);                
                    break;

                case "Update":
                    if(model.Profile != null)
                        profiles.Update(model.Profile.Id, cbxProfile.Text, model);
                    break;

                case "Delete":
                    if (model.Profile != null && profiles.Delete(model.Profile.Id))
                        cbxProfile.SelectedValue = profiles.Profiles[0].Id;
                    break;
            }

            profiles.Save();
        }

        private void btnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            if (model.Profile == null || model.Profile.Name != cbxProfile.Text)
            {
                mnuAdd.IsEnabled = true;
                mnuUpdate.IsEnabled = false;
                mnuDelete.IsEnabled = false;
            }
            else
            {
                mnuAdd.IsEnabled = false;
                mnuUpdate.IsEnabled = true;
                mnuDelete.IsEnabled = model.Profiles.Count > 1;
            }
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
            {
                if (Keyboard.Modifiers == (ModifierKeys.Control|ModifierKeys.Shift))
                    Jog.Focus();
                base.OnPreviewKeyDown(e);
            }
        }
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
                base.OnPreviewKeyDown(e);
        }
        protected bool ProcessKeyPreview(KeyEventArgs e)
        {
            if (grbl.Keyboard == null)
                return false;

            return grbl.Keyboard.ProcessKeypress(e, Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) || jogEnabled, this);
        }

        private void Jog_FocusedChanged(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (grbl.Keyboard.IsJogging)
                grbl.Keyboard.JogCancel();
            jogEnabled = btn.IsFocused && grbl.Keyboard.CanJog2;
            btn.Content = (string)FindResource(jogEnabled ? "JogActive" : "JogDisabled");
        }

        private void tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Equals(e.OriginalSource, sender))
            {
                if (e.AddedItems.Count == 1)
                {
                    var view = getView(e.AddedItems[0] as TabItem);
                    model.Positions.Clear();
                    model.ProbingType = view.ProbingType;
                    model.Message = string.Empty;
                    model.PreviewEnable = false;

                    if (GrblInfo.IsGrblHAL)
                        Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

                    if(e.RemovedItems.Count == 1)
                        getView(e.RemovedItems[0] as TabItem).Activate(false);

                    view.Activate(true);
                }
                e.Handled = true;
            }
        }
    }
}
