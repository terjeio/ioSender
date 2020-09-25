/*
 * ProbingView.xaml.cs - part of CNC Probing library
 *
 * v0.27 / 2020-09-22 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
using System.Windows.Input;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing
{

    /// <summary>
    /// Interaction logic for ProbingView.xaml
    /// </summary>
    public partial class ProbingView : UserControl, ICNCView
    {
        private bool jogEnabled = false, probeTriggered = false;
        private DistanceMode mode = DistanceMode.Absolute;
        private ProbingViewModel model = null;
        private ProbingProfiles profiles = new ProbingProfiles();
        private KeypressHandler keyboard = null;

        public ProbingView()
        {
            InitializeComponent();

            profiles.Load();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is GrblViewModel)
            {
                if (keyboard == null) {
                    keyboard = new KeypressHandler(DataContext as GrblViewModel);
                    keyboard.AddHandler(Key.R, ModifierKeys.Alt, StartProbe);
                    keyboard.AddHandler(Key.S, ModifierKeys.Alt, StopProbe);
                }
                DataContext = model = new ProbingViewModel(DataContext as GrblViewModel, profiles);
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
            getView(tab.SelectedItem as TabItem)?.Start();

            return true;
        }

        private void DisplayPosition(GrblViewModel grbl)
        {
            model.Position = string.Format("X:{0}  Y:{1}  Z:{2}{3}", grbl.Position.X.ToInvariantString(grbl.Format), grbl.Position.Y.ToInvariantString(grbl.Format), grbl.Position.Z.ToInvariantString(grbl.Format), probeTriggered ? " P" : "");
         //   model.Message = string.Format("X:{0}  Y:{1}  Z:{2}", grbl.Position.X.ToInvariantString(), grbl.Position.Y.ToInvariantString(), grbl.Position.Z.ToInvariantString());
        }

        private void Grbl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var grbl = sender as GrblViewModel;

            if (e.PropertyName == nameof(GrblViewModel.IsJobRunning))
            {
                foreach (TabItem tabitem in tab.Items)
                    tabitem.IsEnabled = !(sender as GrblViewModel).IsJobRunning || tabitem == tab.SelectedItem;
            }

            if(e.PropertyName == nameof(GrblViewModel.Position))
            {
                DisplayPosition(sender as GrblViewModel);
            }

            if (e.PropertyName == nameof(GrblViewModel.Signals))
            {
                probeTriggered = (sender as GrblViewModel).Signals.Value.HasFlag(Signals.Probe);
                DisplayPosition(sender as GrblViewModel);
            }
        }

        #region Methods required by CNCView interface

        public ViewType ViewType { get { return ViewType.Probing; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
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

                if (GrblSettings.IsGrblHAL)
                    Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

                if (!model.Grbl.IsGrblHAL && !AppConfig.Settings.Jog.KeyboardEnable)
                    Jog.Visibility = Visibility.Collapsed;

                GrblParserState.Get(!GrblSettings.IsGrblHAL);
                mode = GrblParserState.DistanceMode;
                model.Tool = model.Grbl.Tool == GrblConstants.NO_TOOL ? "0" : model.Grbl.Tool;
                model.CanProbe = !model.Grbl.Signals.Value.HasFlag(Signals.Probe);
                model.HeightMapApplied = GCode.File.HeightMapApplied;
                int csid = GrblWorkParameters.GetCoordinateSystem(model.Grbl.WorkCoordinateSystem).Id;
                model.CoordinateSystem = csid == 0 || csid >= 9 ? 1 : csid;

                if(model.Grbl.IsTloReferenceSet && !double.IsNaN(model.Grbl.TloReference))
                {
                    model.TloReference = model.Grbl.TloReference;
                    model.ReferenceToolOffset = false;
                }

                getView(tab.SelectedItem as TabItem)?.Activate();

                model.Grbl.PropertyChanged += Grbl_PropertyChanged;

                probeTriggered = model.Grbl.Signals.Value.HasFlag(Signals.Probe);

                DisplayPosition(model.Grbl);
            }
            else
            {
                model.Grbl.PropertyChanged -= Grbl_PropertyChanged;

                // If probing alarm active unlock
                if(model.Grbl.GrblState.State == GrblStates.Alarm && (model.Grbl.GrblState.Substate == 4 || model.Grbl.GrblState.Substate == 5))
                    model.Grbl.ExecuteCommand(GrblConstants.CMD_UNLOCK);
                else if (model.Grbl.GrblError != 0)
                    model.Grbl.ExecuteCommand("");  // Clear error

                model.Grbl.ExecuteCommand(mode == DistanceMode.Absolute ? "G90" : "G91");
            }

            model.Message = string.Empty;
            model.Grbl.Poller.SetState(activate ? AppConfig.Settings.Base.PollInterval : 0);
        }

        public void CloseFile()
        {
        }

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        private void mnu_Click(object sender, RoutedEventArgs e)
        {
            switch ((string)((MenuItem)sender).Header)
            {
                case "Add":
                    profiles.Add(cbxProfile.Text, model);
                    break;

                case "Update":
                    if(model.Profile != null)
                        profiles.Update(model.Profile.Id, cbxProfile.Text, model);
                    break;

                case "Delete":
                    if (model.Profile != null && profiles.Delete(model.Profile.Id))
                        model.Profile = profiles.Profiles[0];
                    break;
            }

            profiles.Save();
        }

        private void btnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            if (model.Profile == null || model.Profiles[model.Profile.Id].Name != cbxProfile.Text)
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
            if (keyboard == null)
                return false;

            return keyboard.ProcessKeypress(e, jogEnabled);
        }

        private void Jog_FocusedChanged(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (keyboard.IsJogging)
                keyboard.JogCancel();
            jogEnabled = btn.IsFocused && keyboard.CanJog;
            btn.Content = jogEnabled ? "Keyboard jogging active" : "Keyboard jogging disabled";
        }

        private void tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            var view = getView(((sender as TabControl).SelectedItem as TabItem));
            if (view != null)
            {
                model.ProbingType = view.ProbingType;
                model.Message = string.Empty;
                model.PreviewEnable = false;
                model.OffsetEnable = view.ProbingType == ProbingType.EdgeFinderInternal || view.ProbingType == ProbingType.EdgeFinderExternal;
                model.XYDEnable = view.ProbingType == ProbingType.EdgeFinderInternal || view.ProbingType == ProbingType.EdgeFinderExternal || view.ProbingType == ProbingType.CenterFinder;
                model.ProbeDiameterEnable = model.XYDEnable;
                model.TouchPlateHeightEnable = view.ProbingType != ProbingType.CenterFinder && !(view.ProbingType == ProbingType.ToolLength && model.FixtureHeightEnable);
                model.FixtureHeightEnable = view.ProbingType == ProbingType.ToolLength;

                if (GrblSettings.IsGrblHAL)
                    Comms.com.WriteByte(GrblConstants.CMD_STATUS_REPORT_ALL);

                view.Activate();
            }
        }
    }
}
