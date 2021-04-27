/*
 * KeypressHandler.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.30 / 2021-04-08 / Io Engineering (Terje Io)
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

        private bool fullJog = false, preCancel = false;
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

            if(!GrblInfo.IsGrblHAL)
                fullJog = AppConfig.Settings.Jog.KeyboardEnable;
        }

        public bool ProcessKeypress(KeyEventArgs e, bool allowJog)
        {
            bool isJogging = IsJogging;

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
                        isJogging = axisjog[2] != Key.PageUp;
                        axisjog[2] = Key.PageUp;
                        break;

                    case Key.PageDown:
                        isJogging = axisjog[2] != Key.PageDown;
                        axisjog[2] = Key.PageDown;
                        break;

                    case Key.Left:
                        isJogging = axisjog[0] != Key.Left;
                        axisjog[0] = Key.Left;
                        break;

                    case Key.Up:
                        isJogging = axisjog[1] != Key.Up;
                        axisjog[1] = Key.Up;
                        break;

                    case Key.Right:
                        isJogging = axisjog[0] != Key.Right;
                        axisjog[0] = Key.Right;
                        break;

                    case Key.Down:
                        isJogging = axisjog[1] != Key.Down;
                        axisjog[1] = Key.Down;
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
                            command += "Z-{0}";
                            break;

                        case Key.Up:
                            command += "X-{0}";
                            break;

                        case Key.Right:
                            command += "Z{0}";
                            break;

                        case Key.Down:
                            command += "X{0}";
                            break;
                    }
                }
                else for (int i = 0; i < 3; i++) switch (axisjog[i])
                {
                    case Key.PageUp:
                        command += "Z{0}";
                        break;

                    case Key.PageDown:
                        command += "Z-{0}";
                        break;

                    case Key.Left:
                        command += "X-{0}";
                        break;

                    case Key.Up:
                        command += "Y{0}";
                        break;

                    case Key.Right:
                        command += "X{0}";
                        break;

                    case Key.Down:
                        command += "Y-{0}";
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

                    if(jogMode != JogMode.None)
                        SendJogCommand("$J=G91" + string.Format(command + "F{1}",
                                                        jogDistance[(int)jogMode].ToInvariantString(),
                                                         jogSpeed[(int)jogMode].ToInvariantString()));

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
