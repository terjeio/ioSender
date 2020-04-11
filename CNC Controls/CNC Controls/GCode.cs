/*
 * GCode.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.15 / 2020-04-10 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2020, Io Engineering (Terje Io)
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

using CNC.Core;
using CNC.GCode;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls
{
    public class GCode
    {
        private struct GCodeConverter
        {
            public Type Type;
            public string FileType;
        }
        private struct GCodeTransformer
        {
            public Type Type;
            public string Name;
        }

        private string allowedTypes = "cnc,nc,ncc,ngc,gcode,tap";

        private GCodeJob Program { get; set; } = new GCodeJob();
        private List<GCodeConverter> Converters = new List<GCodeConverter>();
        private List<GCodeTransformer> Transformers = new List<GCodeTransformer>();

        private static readonly Lazy<GCode> file = new Lazy<GCode>(() => new GCode());

        private GCode()
        {
            Program.FileChanged += Program_FileChanged;
        }

        private void Program_FileChanged(string filename)
        {
            if (Model != null)
            {
                if (filename == "")
                    Model.ProgramLimits.Clear();
                else foreach (int i in AxisFlags.All.ToIndices())
                {
                    Model.ProgramLimits.MinValues[i] = Program.BoundingBox.Min[i];
                    Model.ProgramLimits.MaxValues[i] = Program.BoundingBox.Max[i];
                }

                Model.FileName = filename;
            }
        }

        public static GCode File { get { return file.Value; } }
        public bool IsLoaded { get { return Program.Loaded; } }
        public bool HeightMapApplied { get { return Program.HeightMapApplied; } set { Program.HeightMapApplied = value; } }

        public DataTable Data { get { return Program.Data; } }
        public int Blocks { get { return Program.Data.Rows.Count; } }
        public List<GCodeToken> Tokens { get { return Program.Tokens; } }
        public Queue<string> Commands { get { return Program.commands; } }
        public GCodeParser Parser { get { return Program.Parser; } }

        public GrblViewModel Model { get; set; }

        public bool AddConverter(Type converter, string filetype)
        {
            bool ok = converter.GetInterface("CNC.Controls.IGCodeConverter") != null;
            if (ok)
            {
                Converters.Add(new GCodeConverter { Type = converter, FileType = "." + filetype });
                allowedTypes += "," + filetype;
            }

            return ok;
        }

        public bool AddTransformer(Type converter, string name, ObservableCollection<MenuItem> menu)
        {
            bool ok = converter.GetInterface("CNC.Controls.IGCodeTransformer") != null;
            if (ok)
            {
                Transformers.Add(new GCodeTransformer { Type = converter, Name = name });

                MenuItem item = new MenuItem()
                {
                    Header = name,
                    Tag = menu.Count
                };

                item.Click += TransformMenu_Click;

                menu.Add(item);
            }

            return ok;
        }

        private void TransformMenu_Click(object sender, RoutedEventArgs e)
        {
            Transform((int)(sender as MenuItem).Tag);
        }

        public void Transform(int id)
        {
            if (Transformers.Count > id)
            {
                var loader = (IGCodeTransformer)Activator.CreateInstance(Transformers[id].Type);
                loader.Apply();
            }
        }

        public void AddBlock(string block, Core.Action action)
        {
            Program.AddBlock(block, action);
        }

        public void AddBlock(string block)
        {
            Program.AddBlock(block);
        }

        public void ClearStatus()
        {
            foreach (DataRow row in Program.Data.Rows)
                if ((string)row["Sent"] != string.Empty)
                    row["Sent"] = string.Empty;
        }

        public void Drag(object sender, DragEventArgs e)
        {
            bool allow = Model != null & (Model.StreamingState == StreamingState.Idle || Model.StreamingState == StreamingState.NoFile);

            if (allow && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                allow = files.Count() == 1 && FileUtils.IsAllowedFile(files[0].ToLower(), allowedTypes + ",txt");
            }

            e.Handled = true;
            e.Effects = allow ? DragDropEffects.Copy : DragDropEffects.None;
        }

        public void Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (files.Count() == 1)
            {
                Load(files[0]);
            }
        }

        public void Close()
        {
            Program.CloseFile();
        }

        public void Open()
        {
            string filename = string.Empty;
            OpenFileDialog file = new OpenFileDialog();

            file.Filter = string.Format("GCode files ({0})|{0}|Text files (*.txt)|*.txt|All files (*.*)|*.*", FileUtils.ExtensionsToFilter(allowedTypes));

            if (file.ShowDialog() == true)
            {
                filename = file.FileName;
            }

            if(filename != string.Empty)
                Load(filename);
        }

        public void Load(string filename)
        {
            foreach (var converter in Converters)
            {
                if (filename.EndsWith(converter.FileType))
                {
                    var loader = (IGCodeConverter)Activator.CreateInstance(converter.Type);
                    loader.LoadFile(File, filename);
                    return;
                }
            }

            using (new UIUtils.WaitCursor())
            {
                Program.LoadFile(filename);
            }
        }

        public void Save()
        {
            SaveFileDialog saveDialog = new SaveFileDialog()
            {
                Filter = "GCode file (*.nc)|*.nc",
                AddExtension = true,
                DefaultExt = ".nc",
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    //using (new UIUtils.WaitCursor())
                    //{
                    //    GCodeParser.Save(saveDialog.FileName, GCodeParser.TokensToGCode(File.Tokens));
                    //}

                    using (StreamWriter stream = new StreamWriter(saveDialog.FileName))
                    {
                        using (new UIUtils.WaitCursor())
                        {
                            foreach (DataRow line in Program.Data.Rows)
                                stream.WriteLine((string)line["Data"]);

                        }
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
