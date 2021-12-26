/*
 * KeypressHandler.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.35 / 2021-10-20 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2021, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CNC.Core;

namespace CNC.Controls
{
    public class KeypressHandler
    {
        private enum JogMode
        {
            Step = 0,
            Slow,
            Fast,
            None // must be last!
        }

        public class KeypressHandlerFn
        {
            public Key key;
            public ModifierKeys modifiers;
            public Func<Key, bool> Call;
        }

        private bool fullJog = false, preCancel = false, softLimits = false;
        private volatile Key[] axisjog = new Key[3] { Key.None, Key.None, Key.None };
        private double[] jogDistance = new double[3] { 0.05, 500.0, 500.0 };
        private double[] jogSpeed = new double[3] { 100.0, 200.0, 500.0 };
        private JogMode jogMode = JogMode.None;
        private GrblViewModel grbl;
        private List<KeypressHandlerFn> handlers = new List<KeypressHandlerFn>();

        public void AddHandler(Key key, ModifierKeys modifiers, Func<Key, bool> handler)
        {
            handlers.Add(new KeypressHandlerFn(){key = key, modifiers = modifiers, Call = handler});
        }

        public KeypressHandler(GrblViewModel model)
        {
            grbl = model;

            bool useFirmwareJog = false;

            if (GrblSettings.IsLoaded)
            {
                double val;
                if ((useFirmwareJog = !(val = GrblSettings.GetDouble(GrblSetting.JogStepDistance)).Equals(double.NaN)))
                    jogDistance[(int)JogMode.Step] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogSlowDistance)).Equals(double.NaN))
                    jogDistance[(int)JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogFastDistance)).Equals(double.NaN))
                    jogDistance[(int)JogMode.Fast] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogStepSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Step] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogSlowSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Slow] = val;
                if (!(val = GrblSettings.GetDouble(GrblSetting.JogFastSpeed)).Equals(double.NaN))
                    jogSpeed[(int)JogMode.Fast] = val;

                fullJog = GrblInfo.IsGrblHAL;
                model.IsMetric = GrblSettings.GetString(GrblSetting.ReportInches) != "1";
            }

            if (!useFirmwareJog)
            {
                AppConfig.Settings.Jog.PropertyChanged += Jog_PropertyChanged;
                updateConfig();
            }
        }

        private void Jog_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            updateConfig();
        }

        public bool CanJog { get { return grbl.GrblState.State == GrblStates.Idle || grbl.GrblState.State == GrblStates.Tool || grbl.GrblState.State == GrblStates.Jog; } }
        public bool IsJogging {  get { return jogMode != JogMode.None || grbl.GrblState.State == GrblStates.Jog; } }

        private void updateConfig()
        {
            grbl.JogStep = jogDistance[(int)JogMode.Step] = AppConfig.Settings.Jog.StepDistance;
            jogDistance[(int)JogMode.Slow] = AppConfig.Settings.Jog.SlowDistance;
            jogDistance[(int)JogMode.Fast] = AppConfig.Settings.Jog.SlowDistance;
            jogSpeed[(int)JogMode.Step] = AppConfig.Settings.Jog.StepFeedrate;
            jogSpeed[(int)JogMode.Slow] = AppConfig.Settings.Jog.SlowFeedrate;
            jogSpeed[(int)JogMode.Fast] = AppConfig.Settings.Jog.FastFeedrate;

            softLimits = GrblSettings.GetInteger(GrblSetting.SoftLimitsEnable) == 1;

            if (!GrblInfo.IsGrblHAL)
                fullJog = AppConfig.Settings.Jog.KeyboardEnable;
        }

        public bool ProcessKeypress(KeyEventArgs e, bool allowJog)
        {
            bool isJogging = IsJogging;
            double[] dist = new double[3] { 0d, 0d, 0d };

            if (e.IsUp && isJogging)
            {
                bool cancel = !allowJog;

                isJogging = false;

                for (int i = 0; i < 3; i++)
                {
                    if (axisjog[i] == e.Key)
                    {
                        axisjog[i] = Key.None;
                        cancel = true;
                    }
                    else
                        isJogging = isJogging || (axisjog[i] != Key.None);
                }

                isJogging &= allowJog;

                if (cancel && !isJogging && jogMode != JogMode.Step)
                    JogCancel();
            }

            if (!isJogging && allowJog && Comms.com.OutCount != 0)
                return true;

            if (e.IsDown && CanJog && allowJog)
            {
                // Do not respond to autorepeats!
                if (e.IsRepeat)
                    return true;

                switch (e.Key)
                {
                    case Key.PageUp:
                        isJogging = axisjog[GrblConstants.Z_AXIS] != Key.PageUp;
                        axisjog[GrblConstants.Z_AXIS] = Key.PageUp;
                        break;

                    case Key.PageDown:
                        isJogging = axisjog[GrblConstants.Z_AXIS] != Key.PageDown;
                        axisjog[GrblConstants.Z_AXIS] = Key.PageDown;
                        break;

                    case Key.Left:
                        isJogging = axisjog[GrblConstants.X_AXIS] != Key.Left;
                        axisjog[GrblConstants.X_AXIS] = Key.Left;
                        break;

                    case Key.Up:
                        isJogging = axisjog[GrblConstants.Y_AXIS] != Key.Up;
                        axisjog[GrblConstants.Y_AXIS] = Key.Up;
                        break;

                    case Key.Right:
                        isJogging = axisjog[GrblConstants.X_AXIS] != Key.Right;
                        axisjog[GrblConstants.X_AXIS] = Key.Right;
                        break;

                    case Key.Down:
                        isJogging = axisjog[GrblConstants.Y_AXIS] != Key.Down;
                        axisjog[GrblConstants.Y_AXIS] = Key.Down;
                        break;
                }
            }

            if (isJogging)
            {
                string command = string.Empty;

                if (GrblInfo.LatheModeEnabled)
                {
                    for (int i = 0; i < 2; i++) switch (axisjog[i])
                    {
                        case Key.Left:
                            dist[GrblConstants.Z_AXIS] = -1d;
                            command += "Z-{3}";
                            break;

                        case Key.Up:
                            dist[GrblConstants.X_AXIS] = -1d;
                            command += "X-{1}";
                            break;

                        case Key.Right:
                            dist[GrblConstants.Z_AXIS] = 1d;
                            command += "Z{3}";
                            break;

                        case Key.Down:
                            dist[GrblConstants.X_AXIS] = 1d;
                            command += "X{1}";
                            break;
                    }
                }
                else for (int i = 0; i < 3; i++) switch (axisjog[i])
                {
                    case Key.PageUp:
                        dist[GrblConstants.Z_AXIS] = 1d;
                        command += "Z{3}";
                        break;

                    case Key.PageDown:
                        dist[GrblConstants.Z_AXIS] = -1d;
                        command += "Z-{3}";
                        break;

                    case Key.Left:
                        dist[GrblConstants.X_AXIS] = -1d;
                        command += "X-{1}";
                        break;

                    case Key.Up:
                        dist[GrblConstants.Y_AXIS] = 1d;
                        command += "Y{2}";
                        break;

                    case Key.Right:
                        dist[GrblConstants.X_AXIS] = 1d;
                        command += "X{1}";
                        break;

                    case Key.Down:
                        dist[GrblConstants.Y_AXIS] = -1d;
                        command += "Y-{2}";
                        break;
                }

                if ((isJogging = command != string.Empty))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        for (int i = 0; i < 3; i++)
                            axisjog[i] = Key.None;
                        preCancel = !(jogMode == JogMode.Step || jogMode == JogMode.None);
                        jogMode = JogMode.Step;
                        jogDistance[(int)jogMode] = grbl.JogStep;
                    }
                    else if (fullJog)
                    {
                        preCancel = true;
                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                            jogMode = JogMode.Fast;
                        else
                            jogMode = JogMode.Slow;
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                            axisjog[i] = Key.None;
                        jogMode = JogMode.None;
                    }

                    if (jogMode != JogMode.None)
                    {
                        if (GrblInfo.IsGrblHAL || !softLimits)
                        {
                            var distance = jogDistance[(int)jogMode].ToInvariantString();
                            SendJogCommand("$J=G91G21" + string.Format(command + "F{0}",
                                                             jogSpeed[(int)jogMode].ToInvariantString(),
                                                              distance, distance, distance));
                        }
                        else
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (dist[i] != 0d)
                                {
                                    if(GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(i)))
                                        dist[i] = dist[i] < 0d ? grbl.MachinePosition.Values[i] : Math.Max(0d, GrblInfo.MaxTravel.Values[i] - grbl.MachinePosition.Values[i] - .5d);
                                    else
                                        dist[i] = dist[i] > 0d ? (- grbl.MachinePosition.Values[i] - .5d) : Math.Max(0d, GrblInfo.MaxTravel.Values[i] + grbl.MachinePosition.Values[i] - .5d);
                                }
                            }
                            SendJogCommand("$J=G91G21" + string.Format(command + "F{0}",
                                                             jogSpeed[(int)jogMode].ToInvariantString(),
                                                              dist[GrblConstants.X_AXIS].ToInvariantString(),
                                                               dist[GrblConstants.Y_AXIS].ToInvariantString(),
                                                                dist[GrblConstants.Z_AXIS].ToInvariantString()));
                        }
                    }

                    return jogMode != JogMode.None;
                }
            }

            if (e.IsUp)
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    var handler = handlers.Where(k => k.modifiers == Keyboard.Modifiers && k.key == e.SystemKey).FirstOrDefault();
                    if (handler != null)
                        return handler.Call(e.SystemKey);
                }
                else if (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    var handler = handlers.Where(k => k.modifiers == Keyboard.Modifiers && k.key == e.Key).FirstOrDefault();
                    if (handler != null)
                        return handler.Call(e.Key);

                    else switch (e.Key)
                    {
                        case Key.NumPad4:
                            JogControl.JogData.StepDec();
                            return true;
                        //  break;

                        case Key.NumPad6:
                            JogControl.JogData.StepInc();
                            return true;

                        case Key.NumPad8:
                            JogControl.JogData.FeedInc();
                            return true;

                        case Key.NumPad2:
                            JogControl.JogData.FeedDec();
                            return true;
                                //  break;
                    }
                }
            }

            return false;
        }

        public void JogCancel()
        {
            while (Comms.com.OutCount != 0) ;
            Comms.com.WriteByte(GrblConstants.CMD_JOG_CANCEL); // Cancel jog
            jogMode = JogMode.None;
        }

        public void SendJogCommand(string command)
        {
            if (IsJogging)
            {
                while (Comms.com.OutCount != 0) ;
                if(preCancel)
                    Comms.com.WriteByte(GrblConstants.CMD_JOG_CANCEL); // Cancel current jog
            }
            Comms.com.WriteCommand(command);
        }
    }
}
