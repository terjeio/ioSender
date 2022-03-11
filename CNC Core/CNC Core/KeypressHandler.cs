/*
 * KeypressHandler.xaml.cs - part of CNC Controls library
 *
 * v0.37 / 2022-02-27 / Io Engineering (Terje Io)
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;
using CNC.GCode;

namespace CNC.Core
{
    public class KeypressHandler
    {
        public enum JogMode
        {
            Step = 0,
            Slow,
            Fast,
            None // must be last!
        }

        [XmlType(TypeName = "KeyMapping")]
        public class KeypressHandlerFn
        {
            [XmlIgnore]
            internal string method, dummy;

            public Key Key;
            public ModifierKeys Modifiers;
            public bool OnUp;
            [XmlIgnore]
            public UserControl context;
            [XmlIgnore]
            public Func<Key, bool> Call;
            public string Context { get { return context == null ? "null" : context.Name; } set { dummy = value; } }
            public string Method { get { return Call.Method.ReflectedType.Name + "." +  Call.Method.Name; } set { method = value; } }
        }

        private int N_AXIS = 3;
        private bool preCancel = false, allowJog = true;
        private volatile Key[] axisjog = new Key[4] { Key.None, Key.None, Key.None, Key.None };
        private JogMode jogMode = JogMode.None;
        private GrblViewModel grbl;
        private List<KeypressHandlerFn> handlers = new List<KeypressHandlerFn>();

        public void AddHandler(Key key, ModifierKeys modifiers, Func<Key, bool> handler, UserControl context = null, bool onUp = true)
        {
            handlers.Add(new KeypressHandlerFn(){Key = key, Modifiers = modifiers, Call = handler, context = context, OnUp = onUp });
        }
        public void AddHandler(Key key, ModifierKeys modifiers, Func<Key, bool> handler, bool onUp)
        {
            handlers.Add(new KeypressHandlerFn() { Key = key, Modifiers = modifiers, Call = handler, context = null, OnUp = onUp });
        }

        public KeypressHandler(GrblViewModel model)
        {
            grbl = model;
        }

        public double[] JogDistances { get; set; } = new double[3] { 0.01, 500.0, 500.0 };
        public double[] JogFeedrates { get; set; } = new double[3] { 100.0, 200.0, 500.0 };
        public double JogStepDistance { get { return JogDistances[(int)JogMode.Step]; } set { grbl.JogStep = JogDistances[(int)JogMode.Step] = value; } }
        public double LimitSwitchesClearance { get; set; } = .5d;
        public bool SoftLimits { get; set; } = false;
        public bool IsJoggingEnabled { get; set; } = true;
        public bool IsContinuousJoggingEnabled { get; set; }
        public bool IsRepeating { get; private set; } = false;
        public bool CanJog2 { get { return grbl.GrblState.State == GrblStates.Idle || grbl.GrblState.State == GrblStates.Tool || grbl.GrblState.State == GrblStates.Jog; } }
        public bool CanJog { get { return allowJog && (grbl.GrblState.State == GrblStates.Idle || grbl.GrblState.State == GrblStates.Tool || grbl.GrblState.State == GrblStates.Jog); } }
        public bool IsJogging { get { return jogMode != JogMode.None || grbl.GrblState.State == GrblStates.Jog; } }

        public bool SaveMappings (string filename)
        {
            if (handlers.Count == 0)
                return false;

            bool ok = false;

            XmlSerializer xs = new XmlSerializer(typeof(List<KeypressHandlerFn>), new XmlRootAttribute("KeyMappings"));

            try
            {
                FileStream fsout = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                using (fsout)
                {
                    var keymappings = handlers.Where(x => x.Method != "JobControl.FnKeyHandler").ToList();
                    xs.Serialize(fsout, keymappings);
                    ok = true;
                }
            }
            catch
            {
            }

            return ok;
        }

        public bool LoadMappings(string filename)
        {
            if (handlers.Count == 0)
                return false;

            bool ok = false;
            List<KeypressHandlerFn> keymappings = new List<KeypressHandlerFn>();
            XmlSerializer xs = new XmlSerializer(typeof(List<KeypressHandlerFn>), new XmlRootAttribute("KeyMappings"));

            try
            {
                StreamReader reader = new StreamReader(filename);
                keymappings = (List<KeypressHandlerFn>)xs.Deserialize(reader);
                reader.Close();

                foreach(var keymap in keymappings)
                {
                    var map = handlers.Where(x => x.Method == keymap.method).FirstOrDefault();
                    if(map != null)
                    {
                        map.Key = keymap.Key;
                        map.Modifiers = keymap.Modifiers;
                    }
                }

                ok = true;
            }
            catch
            {
                System.Windows.MessageBox.Show("keymap file is corrupt!", "ioSender", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            return ok;
        }

        public bool ProcessKeypress(KeyEventArgs e, bool allowJog, UserControl context = null)
        {
            bool isJogging = IsJogging, jogkeyPressed = false;
            double[] dist = new double[4] { 0d, 0d, 0d, 0d };

            if (e.IsUp && isJogging)
            {
                bool cancel = !allowJog;

                isJogging = false;

                for (int i = 0; i < N_AXIS; i++)
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

            this.allowJog = allowJog;

            if (IsJoggingEnabled && e.IsDown && CanJog)
            {
                // Do not respond to autorepeats!
                if (e.IsRepeat)
                    return true;

                N_AXIS = GrblInfo.AxisFlags.HasFlag(AxisFlags.A) ? 4 : 3;

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

                    case Key.Home:
                        if(N_AXIS == 4) {
                            isJogging = axisjog[GrblConstants.A_AXIS] != Key.Home;
                            axisjog[GrblConstants.A_AXIS] = Key.Home;
                        }
                        break;

                    case Key.End:
                        if (N_AXIS == 4)
                        {
                            isJogging = axisjog[GrblConstants.A_AXIS] != Key.End;
                            axisjog[GrblConstants.A_AXIS] = Key.End;
                        }
                        break;
                }
            }
            else
                jogkeyPressed = !(Keyboard.FocusedElement is System.Windows.Controls.TextBox) && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown);

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
                else for (int i = 0; i < N_AXIS; i++) switch (axisjog[i])
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

                    case Key.Home:
                        dist[GrblConstants.A_AXIS] = 1d;
                        command += "A{4}";
                        break;

                    case Key.End:
                        dist[GrblConstants.A_AXIS] = -1d;
                        command += "A-{4}";
                        break;
                }

                if ((isJogging = command != string.Empty))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        for (int i = 0; i < N_AXIS; i++)
                            axisjog[i] = Key.None;
                        preCancel = !(jogMode == JogMode.Step || jogMode == JogMode.None);
                        jogMode = JogMode.Step;
                        JogDistances[(int)jogMode] = grbl.JogStep;
                    }
                    else if (IsContinuousJoggingEnabled)
                    {
                        preCancel = true;
                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                            jogMode = JogMode.Fast;
                        else
                            jogMode = JogMode.Slow;
                    }
                    else
                    {
                        for (int i = 0; i < N_AXIS; i++)
                            axisjog[i] = Key.None;
                        jogMode = JogMode.None;
                    }

                    if (jogMode != JogMode.None)
                    {
                        if (GrblInfo.IsGrblHAL || !SoftLimits)
                        {
                            var distance = JogDistances[(int)jogMode].ToInvariantString();
                            SendJogCommand("$J=G91G21" + string.Format(command + "F{0}",
                                                             JogFeedrates[(int)jogMode].ToInvariantString(),
                                                              distance, distance, distance, distance));
                        }
                        else
                        {
                            for (int i = 0; i < N_AXIS; i++)
                            {
                                if (dist[i] != 0d)
                                {
                                    dist[i] = grbl.MachinePosition.Values[i] + JogDistances[(int)jogMode] * dist[i];

                                    if (i == GrblConstants.A_AXIS && GrblInfo.MaxTravel.Values[GrblConstants.A_AXIS] == 0d)
                                        continue;

                                    if (GrblInfo.ForceSetOrigin)
                                    {
                                        if (!GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(i)))
                                        {
                                            if (dist[i] > 0)
                                                dist[i] = 0;
                                            else if(dist[i] < (-GrblInfo.MaxTravel.Values[i] + LimitSwitchesClearance))
                                                dist[i] = (-GrblInfo.MaxTravel.Values[i] + LimitSwitchesClearance);
                                        } else
                                        {
                                            if (dist[i] < 0d)
                                                dist[i] = 0d;
                                            else if (dist[i] > (GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance))
                                                dist[i] = GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance;
                                        }
                                    }
                                    else
                                    {
                                        if (dist[i] > -LimitSwitchesClearance)
                                            dist[i] = -LimitSwitchesClearance;
                                        else if (dist[i] < -(GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance))
                                            dist[i] = -(GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance);
                                    }
                                }
                            }

                            SendJogCommand("$J=G53G21" + string.Format(command.Replace('-', ' ') + "F{0}",
                                                             JogFeedrates[(int)jogMode].ToInvariantString(),
                                                              dist[GrblConstants.X_AXIS].ToInvariantString(),
                                                               dist[GrblConstants.Y_AXIS].ToInvariantString(),
                                                                dist[GrblConstants.Z_AXIS].ToInvariantString(),
                                                                 dist[GrblConstants.A_AXIS].ToInvariantString()));
                        }
                    }

                    return jogMode != JogMode.None;
                } 
            }

            IsRepeating = e.IsRepeat;

            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                var handler = handlers.Where(k => k.Modifiers == Keyboard.Modifiers && k.Key == e.SystemKey && k.OnUp == e.IsUp && k.context == context).FirstOrDefault();
                if (handler != null)
                    return handler.Call(e.SystemKey);
                else
                {
                    handler = handlers.Where(k => k.Modifiers == Keyboard.Modifiers && k.Key == e.SystemKey && k.OnUp == e.IsUp && k.context == null).FirstOrDefault();
                    if (handler != null)
                        return handler.Call(e.SystemKey);
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                var handler = handlers.Where(k => k.Modifiers == Keyboard.Modifiers && k.Key == e.Key && k.OnUp == e.IsUp && k.context == context).FirstOrDefault();
                if (handler != null)
                    return handler.Call(e.Key);
                else
                {
                    handler = handlers.Where(k => k.Modifiers == Keyboard.Modifiers && k.Key == e.Key && k.OnUp == e.IsUp && k.context == null).FirstOrDefault();
                    if (handler != null)
                        return handler.Call(e.Key);
                }
            }

            return jogkeyPressed;
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
