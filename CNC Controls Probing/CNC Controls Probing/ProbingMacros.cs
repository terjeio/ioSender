/*
 * ProbingMacros.cs - part of CNC Probing library
 *
 * v0.46 / 2025-02-14 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2024, @Jay-Tech
Copyright (c) 2024-2025, Io Engineering (Terje Io)
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using CNC.Core;

namespace CNC.Controls.Probing
{
    public class ProbingMacros
    {
        private const string macrosFileName = "ProbingMacros.xml";

        public ObservableCollection<ProbingMacro> Macros { get; private set; } = new ObservableCollection<ProbingMacro>();

        public void Save()
        {
            XmlSerializer xs = new XmlSerializer(typeof(ObservableCollection<ProbingMacro>));
            try
            {
                FileStream fsout = new FileStream(Core.Resources.ConfigPath + macrosFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                using (fsout)
                {
                    xs.Serialize(fsout, Macros);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ioSender", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        public void Load()
        {
            XmlSerializer xs = new XmlSerializer(typeof(ObservableCollection<ProbingMacro>));

            try
            {
                StreamReader reader = new StreamReader(Core.Resources.ConfigPath + macrosFileName);
                Macros = (ObservableCollection<ProbingMacro>)xs.Deserialize(reader);
                reader.Close();
            }
            catch
            {
            }

            foreach (var macro in Macros)
            {
                if (macro.Id == 0)
                {
                    if (macro.Name != "<no action>")
                        macro.Id = ++ProbingMacro.NextId;
                }
            }

            bool noAction = false;
            foreach (var macro in Macros)
            {
                if (macro.Id == 0)
                    noAction = true;
                ProbingMacro.NextId = Math.Max(ProbingMacro.NextId, macro.Id);
            }

            if (!noAction)
                Macros.Insert(0, new ProbingMacro("<no action>", string.Empty, string.Empty, false, 0));
        }
    }

    [Serializable]
    public class ProbingMacro
    {
        public static int NextId = 0;

        public ProbingMacro()
        {
        }

        public ProbingMacro(string name, string preCommand, string postCommand, bool isChecked, int id = -1)
        {
            Id = id == -1 ? ++NextId : id;
            Name = name;
            PreCommands = preCommand;
            PostCommands = postCommand;
            RunOnce = isChecked;
        }

        public int Id { get; set; }

        public string Name { get; set; }

        public string PreCommands { get; set; }

        public string PostCommands { get; set; }

        public bool RunOnce { get; set; }
    }

    public class ProbeMacroViewModel : ViewModelBase
    {
        private bool _runOnce;
        private string _postMacroText;
        private string _preMacroText;
        private ProbingMacro _selectedMacro;
        private readonly ProbingMacros _probeMacros;
        private string[] NoCommands { get; } = { };

        private MacroDialog dialog;

        public ProbeMacroViewModel()
        {
            _probeMacros = new ProbingMacros();
            _probeMacros.Load();

            OpenDialog = new ActionCommand(OpenDialogHandler);
            DeleteCommand = new ActionCommand(DeleteCommandHandler);
            AddCommand = new ActionCommand(AddCommandHandler);
        }

        public ICommand OpenDialog { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddCommand { get; }

        public bool CanAdd
        {
            get { return _selectedMacro == null; }
            set { OnPropertyChanged(); }
        }

        public bool CanEdit
        {
            get { return _selectedMacro == null || _selectedMacro.Id != 0; }
            set { OnPropertyChanged(); }
        }

        public bool CanDelete
        {
            get { return _selectedMacro != null && _selectedMacro.Id > 0; }
            set { OnPropertyChanged(); }
        }

        public ObservableCollection<ProbingMacro> Macros
        {
            get { return _probeMacros.Macros; }
        }

        public string[] PreJobCommands { get { return _selectedMacro == null ? NoCommands : _selectedMacro.PreCommands.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries); } }
        public string[] PostJobCommands { get { return _selectedMacro == null ? NoCommands : _selectedMacro.PostCommands.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries); } }

        public ProbingMacro SelectedMacro
        {
            get { return _selectedMacro; }
            set
            {
                if (value == _selectedMacro) return;
                _selectedMacro = value;
                if (value == null)
                {
                    RunOnce = false;
                    PreMacroText = string.Empty;
                    PostMacroText = string.Empty;
                }
                else
                {
                    RunOnce = SelectedMacro.RunOnce;
                    PostMacroText = SelectedMacro.PostCommands;
                    PreMacroText = SelectedMacro.PreCommands;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAdd));
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(ActiveMacroName));
            }
        }

        public bool RunOnce
        {
            get { return _runOnce; }
            set
            {
                if (value == _runOnce) return;
                _runOnce = value;
                OnPropertyChanged();
            }
        }

        public string ActiveMacroName
        {
            get { return _selectedMacro == null || _selectedMacro.Id == 0 ? string.Empty : _selectedMacro.Name; }
        }

        public string PreMacroText
        {
            get { return _preMacroText; }
            set
            {
                if (value == _preMacroText) return;
                _preMacroText = value;
                OnPropertyChanged();
            }
        }

        public string PostMacroText
        {
            get { return _postMacroText; }
            set
            {
                if (value == _postMacroText) return;
                _postMacroText = value;
                OnPropertyChanged();
            }
        }

        public void Clear()
        {
            SelectedMacro = null;
        }

        public void Close()
        {
            if (_selectedMacro != null)
            {
                if (!(_selectedMacro.RunOnce == RunOnce && _selectedMacro.PreCommands == PreMacroText && _selectedMacro.PostCommands == PostMacroText))
                {
                    if (MessageBox.Show(LibStrings.FindResource("MacroChangedSave"), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                        AddCommandHandler();
                    else
                    {
                        RunOnce = _selectedMacro.RunOnce;
                        PostMacroText = _selectedMacro.PostCommands;
                        PreMacroText = _selectedMacro.PreCommands;
                    }
                }
            }
        }

        private void OpenDialogHandler()
        {
            dialog = new MacroDialog(this)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (_selectedMacro == null)
                SelectedMacro = Macros[0];

            dialog.ShowDialog();
        }

        private void DeleteCommandHandler()
        {
            if (_selectedMacro != null)
            {
                var found = Macros.First(x => x.Name == SelectedMacro.Name);
                if (found != null)
                    Macros.Remove(found);
                _probeMacros.Save();
                SelectedMacro = Macros[0];
            }
        }

        private void AddCommandHandler()
        {
            if (_selectedMacro != null)
            {
                _selectedMacro.RunOnce = RunOnce;
                _selectedMacro.PreCommands = PreMacroText.TrimEnd('\r', '\n');
                _selectedMacro.PostCommands = PostMacroText.TrimEnd('\r', '\n');
            }
            else
            {
                string MacroName = dialog.cbxMacro.Text;

                if (string.IsNullOrEmpty(MacroName))
                    MacroName = $"MC_{ new Random().Next(0, 1000) }";

                Macros.Add(SelectedMacro = new ProbingMacro(MacroName, PreMacroText, PostMacroText, RunOnce));
            }
            _probeMacros.Save();
        }
    }
}
