/*
 * KeypressHandler.xaml.cs - part of CNC Controls library
 *
 * v0.47 / 2026-03-23 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2026, Io Engineering (Terje Io)
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
        private int N_AXIS = 3;
        private bool preCancel = false, allowJog = true;
        private JogMode jogMode = JogMode.None;
        private GrblViewModel grbl;
        private List<KeypressHandlerFn> handlers = new List<KeypressHandlerFn>();
        private List<HandlerFn> functions = new List<HandlerFn>();
        private AxisJog[] axisjog = new AxisJog[9];
        private JogKey[] jogKeys = new JogKey[] {
            new JogKey(0, Key.Right),
            new JogKey(0, Key.Left),
            new JogKey(1, Key.Up),
            new JogKey(1, Key.Down),
            new JogKey(2, Key.PageUp),
            new JogKey(2, Key.PageDown),
            new JogKey(3, Key.Home),
            new JogKey(3, Key.End),
            new JogKey(4),
            new JogKey(4),
            new JogKey(5),
            new JogKey(5),
            new JogKey(6),
            new JogKey(6),
            new JogKey(7),
            new JogKey(7),
            new JogKey(8),
            new JogKey(8)
        };

        public KeypressHandler(GrblViewModel model)
        {
            grbl = model;
            for (int i = 0; i < axisjog.Length; i++)
                axisjog[i] = new AxisJog();

            AddFunction(FeedOverrideFinePlus, null);
            AddFunction(FeedOverrideFineMinus, null);
            AddFunction(FeedOverrideCoarseMinus, null);
            AddFunction(FeedOverrideCoarsePlus, null);
            AddFunction(FeedOverrideReset, null);
            AddFunction(FeedOverrideRapidsMedium, null);
            AddFunction(FeedOverrideRapidsLow, null);
            AddFunction(FeedOverrideRapidsReset, null);
            AddFunction(FloodOverrideToggle, null);
            AddFunction(MistOverrideToggle, null);
            AddFunction(Fan0Toggle, null);
            AddFunction(SpindleOverrideFinePlus, null);
            AddFunction(SpindleOverrideFineMinus, null);
            AddFunction(SpindleOverrideCoarseMinus, null);
            AddFunction(SpindleOverrideCoarsePlus, null);
            AddFunction(SpindleOverrideStop, null);
            AddFunction(ProbeConnectedToggle, null);
            AddFunction(OptionalStopToggle, null);
            AddFunction(SingleBlockToggle, null);
        }

        public void Configure(int numAxes, string axisLetters, bool lathe)
        {
            N_AXIS = numAxes;
            axisLetters = axisLetters.Replace("-", "");
            for (int i = 0; i < jogKeys.Length; i++)
            {
                jogKeys[i].Command = string.Empty;
            }
            for (int i = 0; i < numAxes; i++)
            {
                var k = lathe ? (i == 0 ? 2 : 0) : i;
                jogKeys[i * 2].Command = axisLetters.Substring(k, 1) + (lathe && i != 0 ? "-{0}" : "{0}");
                jogKeys[i * 2 + 1].Command = axisLetters.Substring(k, 1) + (lathe && i != 0 ? "{0}" : "-{0}");
            }
        }

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
            public string Method { get { return Call == null ? method : Call.Method.ReflectedType.Name + "." +  Call.Method.Name; } set { method = value; } }
        }

        private class HandlerFn
        {
            public UserControl context;
            public Func<Key, bool> Call;
            public string Context { get { return context == null ? "null" : context.Name; } }
            public string Method { get { return Call.Method.ReflectedType.Name + "." + Call.Method.Name; } }
        }

        private class JogKey
        {
            public JogKey (int axisIndex, Key key)
            {
                Key = key;
                Command = string.Empty;
                AxisIndex = axisIndex;
            }
            public JogKey(int axisIndex)
            {
                Key = Key.None;
                Command = string.Empty;
                AxisIndex = axisIndex;
            }

            public Key Key { get; set; }
            public string Command { get; set; }
            public int AxisIndex { get; private set; }
            public bool Remapped { get; set; } = false;
        }

        private class AxisJog
        {
            public AxisJog ()
            {
                Key = Key.None;
                Command = String.Empty;
                Distance = 0d;
            }

            public Key Key { get; set; }
            public string Command { get; set; }
            public double Distance { get; set; }
        }

        public void AddFunction(Func<Key, bool> call, UserControl context)
        {
            var function = functions.Where(k => k.Call == call && k.context == context).FirstOrDefault();
            if (function == null)
                functions.Add(new HandlerFn() { Call = call, context = context });
        }

        public void AddHandler(Key key, ModifierKeys modifiers, Func<Key, bool> handler, UserControl context = null, bool onUp = true)
        {
            AddFunction(handler, context);
            handlers.Add(new KeypressHandlerFn(){ Key = key, Modifiers = modifiers, Call = handler, context = context, OnUp = onUp });
        }
        public void AddHandler(Key key, ModifierKeys modifiers, Func<Key, bool> handler, bool onUp)
        {
            AddFunction(handler, null);
            handlers.Add(new KeypressHandlerFn() { Key = key, Modifiers = modifiers, Call = handler, context = null, OnUp = onUp });
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
                    int remapped_at = handlers.Count, n_remapped = 0;
                    for(var i = 0; i < jogKeys.Length; i++)
                    {
                        if (jogKeys[i].Remapped)
                        {
                            n_remapped++;
                            handlers.Add(new KeypressHandlerFn() { Key = jogKeys[i].Key, Modifiers = ModifierKeys.None, OnUp = false, Method = "Jogkey." + GrblInfo.AxisIndexToLetter(i >> 1) + ((i & 1) == 1 ? "minus" : "plus") });
                        }
                    }

                    xs.Serialize(fsout, handlers.Where(x => x.Method != "JobControl.FnKeyHandler").ToList());

                    if (n_remapped > 0)
                        handlers.RemoveRange(remapped_at, n_remapped);
                    ok = true;
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message, "ioSender", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Exclamation);
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

                foreach (var newmap in keymappings)
                {
                    if (newmap.method.StartsWith("Jogkey.")) {
                        int k = GrblInfo.AxisLetterToIndex(newmap.method.Substring(7, 1));
                        if(k >= 0 && newmap.method.Substring(8) == "plus" || newmap.method.Substring(8) == "minus")
                        {
                            k = k * 2 + (newmap.method.Substring(8) == "minus" ? 1 : 0);
                            jogKeys[k].Key = newmap.Key;
                            jogKeys[k].Remapped = true;
                        }
                    } else {

                        var handler = functions.Where(x => x.Method == newmap.method && x.Context == newmap.Context).FirstOrDefault();
                        var keymap = handlers.Where(k => k.Modifiers == newmap.Modifiers && k.Key == newmap.Key && k.OnUp == newmap.OnUp && k.Context == newmap.Context).FirstOrDefault();

                        if (handler != null)
                        {
                            if (keymap != null)
                            {
                                if (keymap.Method != newmap.method)
                                {
                                    keymap.OnUp = newmap.OnUp;
                                    keymap.Call = handler.Call;
                                }
                            }
                            else
                                handlers.Add(new KeypressHandlerFn() { Key = newmap.Key, Modifiers = newmap.Modifiers, Call = handler.Call, context = handler.context, OnUp = newmap.OnUp });
                        }
                        else if (keymap != null && newmap.method == "None")
                            handlers.Remove(keymap);
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
            JogKey jogKey = null;

            if (e.IsUp && isJogging)
            {
                bool cancel = !allowJog;

                isJogging = false;

                for (int i = 0; i < N_AXIS; i++)
                {
                    if (axisjog[i].Key == e.Key)
                    {
                        axisjog[i].Key = Key.None;
                        axisjog[i].Distance = 0d;
                        cancel = true;
                    }
                    else
                        isJogging = isJogging || (axisjog[i].Key != Key.None);
                }

                isJogging &= allowJog;

                if (cancel && !isJogging && jogMode != JogMode.Step)
                    JogCancel();
            }

            if (!isJogging && allowJog && Comms.com.OutCount != 0)
                return true;

            this.allowJog = allowJog;

            if(IsJoggingEnabled && e.IsDown && CanJog && !(Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) || Keyboard.Modifiers == ModifierKeys.Windows))
                jogKey = jogKeys.Where(p => p.Key == e.Key && p.Command != string.Empty).FirstOrDefault();

            if (jogKey != null)
            {
                // Do not respond to autorepeats!
                if (e.IsRepeat)
                    return true;

                if ((context.DataContext is GrblViewModel && (context.DataContext as GrblViewModel).GrblState.State == GrblStates.Alarm))
                    return true;

                N_AXIS = GrblInfo.AxisFlags.HasFlag(AxisFlags.A) ? 4 : 3;

                isJogging = axisjog[jogKey.AxisIndex].Key != e.Key;
                axisjog[jogKey.AxisIndex].Key = e.Key;
                axisjog[jogKey.AxisIndex].Command = jogKey.Command;
            }
            else
                jogkeyPressed = !(Keyboard.FocusedElement is System.Windows.Controls.TextBox) && (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown);

            if (isJogging)
            {
                string command = string.Empty;

                if ((context.DataContext is GrblViewModel && (context.DataContext as GrblViewModel).GrblState.State == GrblStates.Alarm))
                    return true;

                isJogging = false;

                for (int i = 0; i < N_AXIS; i++)
                {
                    if (axisjog[i].Key != Key.None)
                    {
                       isJogging = true;
                        axisjog[i].Distance = axisjog[i].Command.Contains('-') ? -1d : 1d;
                    }
                    else
                        axisjog[i].Distance = 0d;
                }

                if (isJogging)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        for (int i = 0; i < N_AXIS; i++)
                            axisjog[i].Key = Key.None;
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
                            axisjog[i].Key = Key.None;
                        jogMode = JogMode.None;
                    }

                    if (jogMode != JogMode.None)
                    {
                        if (GrblInfo.IsGrblHAL || !SoftLimits)
                        {
                            var distance = JogDistances[(int)jogMode].ToInvariantString();

                            for (int i = 0; i < N_AXIS; i++)
                            {
                                if (axisjog[i].Distance != 0d)
                                    command += string.Format(axisjog[i].Command, distance);
                            }

                            SendJogCommand("$J=G91G21" + command + string.Format("F{0}", JogFeedrates[(int)jogMode].ToInvariantString()));
                        }
                        else
                        {
                            for (int i = 0; i < N_AXIS; i++)
                            {
                                if (axisjog[i].Distance != 0d)
                                {
                                    axisjog[i].Distance = grbl.MachinePosition.Values[i] + JogDistances[(int)jogMode] * axisjog[i].Distance;

                                    if (i == GrblConstants.A_AXIS && GrblInfo.MaxTravel.Values[GrblConstants.A_AXIS] == 0d)
                                        continue;

                                    if (GrblInfo.ForceSetOrigin)
                                    {
                                        if (!GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(i)))
                                        {
                                            if (axisjog[i].Distance > 0)
                                                axisjog[i].Distance = 0;
                                            else if(axisjog[i].Distance < (-GrblInfo.MaxTravel.Values[i] + LimitSwitchesClearance))
                                                axisjog[i].Distance = (-GrblInfo.MaxTravel.Values[i] + LimitSwitchesClearance);
                                        } else
                                        {
                                            if (axisjog[i].Distance < 0d)
                                                axisjog[i].Distance = 0d;
                                            else if (axisjog[i].Distance > (GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance))
                                                axisjog[i].Distance = GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance;
                                        }
                                    }
                                    else
                                    {
                                        if (axisjog[i].Distance > -LimitSwitchesClearance)
                                            axisjog[i].Distance = -LimitSwitchesClearance;
                                        else if (axisjog[i].Distance < -(GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance))
                                            axisjog[i].Distance = -(GrblInfo.MaxTravel.Values[i] - LimitSwitchesClearance);
                                    }

                                    command += string.Format(axisjog[i].Command, axisjog[i].Distance.ToInvariantString());
                                }
                            }

                            SendJogCommand("$J=G53G21" + string.Format(command.Replace('-', ' ') + "F{0}", JogFeedrates[(int)jogMode].ToInvariantString()));
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

        private bool FeedOverrideFinePlus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_FEED_OVR_FINE_PLUS);

            return true;
        }
        private bool FeedOverrideFineMinus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_FEED_OVR_FINE_MINUS);

            return true;
        }
        private bool FeedOverrideCoarseMinus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_FEED_OVR_COARSE_MINUS);

            return true;
        }
        private bool FeedOverrideCoarsePlus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_FEED_OVR_COARSE_PLUS);

            return true;
        }
        private bool FeedOverrideReset(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_FEED_OVR_RESET);

            return true;
        }
        private bool FeedOverrideRapidsMedium(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_RAPID_OVR_MEDIUM);

            return true;
        }
        private bool FeedOverrideRapidsLow(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_RAPID_OVR_LOW);

            return true;
        }
        private bool FeedOverrideRapidsReset(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_RAPID_OVR_RESET);

            return true;
        }
        private bool FloodOverrideToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_COOLANT_FLOOD_OVR_TOGGLE);

            return true;
        }
        private bool MistOverrideToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_COOLANT_MIST_OVR_TOGGLE);

            return true;
        }
        private bool Fan0Toggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_OVERRIDE_FAN0_TOGGLE);

            return true;
        }
        private bool SpindleOverrideFinePlus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SPINDLE_OVR_FINE_PLUS);

            return true;
        }
        private bool SpindleOverrideFineMinus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SPINDLE_OVR_FINE_MINUS);

            return true;
        }
        private bool SpindleOverrideCoarseMinus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SPINDLE_OVR_COARSE_PLUS);

            return true;
        }
        private bool SpindleOverrideCoarsePlus(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SPINDLE_OVR_COARSE_MINUS);

            return true;
        }
        private bool SpindleOverrideStop(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SPINDLE_OVR_STOP);

            return true;
        }
        private bool ProbeConnectedToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_PROBE_CONNECTED_TOGGLE);

            return true;
        }
        private bool OptionalStopToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_OPTIONAL_STOP_TOGGLE);

            return true;
        }
        private bool SingleBlockToggle(Key key)
        {
            Comms.com.WriteByte(GrblConstants.CMD_SINGLE_BLOCK_TOGGLE);

            return true;
        }
    }
}
