using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CNC.Controls.Diagnostics;
using CNC.Controls.Primitives;
using CNC.Core;
using CNC.Core.Diagnostics;
using CNC.Core.Primitives;
using CNC.Framework;

namespace CNC.Controls
{
    public partial class GcodeTextControl : UserControl
    {
        private readonly GCodeLineStatusMargin statusMargin = new GCodeLineStatusMargin();
        private GrblViewModel model;
        private Dictionary<int, uint> editorLineToTokenLine = new Dictionary<int, uint>();

        public GcodeTextControl()
        {
            InitializeComponent();

            statusMargin.Opacity = 0d;
            ApplySyntaxHighlightingForTheme();

            Editor.TextArea.LeftMargins.Insert(1, statusMargin);
            Editor.TextArea.SelectionChanged += Editor_SelectionChanged;
            Editor.TextChanged += Editor_TextChanged;
            GCode.File.GetEditedText = () => Editor.Text;
            ctxMenu.DataContext = this;

            Unloaded += UserControl_Unloaded;
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            int startLine, endLine;
            int selected = GetSelectedLineCount(out startLine, out endLine);

            bool canStart = DataContext is GrblViewModel && (DataContext as GrblViewModel).StartFromBlock.CanExecute(Math.Max(0, startLine - 1));

            SingleSelected = selected == 1 && canStart;
            MultipleSelected = selected >= 1 && canStart;

            // Map editor line numbers to token line numbers
            uint tokenStartLine = MapEditorLineToTokenLine(startLine);
            uint tokenEndLine = MapEditorLineToTokenLine(endLine);

            SelectedLineRangeChanged?.Invoke((int)tokenStartLine, (int)tokenEndLine);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (model != null)
                model.PropertyChanged -= GcodeTextControl_PropertyChanged;

            AppConfig.Settings.PropertyChanged -= AppConfig_PropertyChanged;

            DetachGCodeStatusHandlers();
            
            // Clear the line-to-token mapping
            editorLineToTokenLine.Clear();
        }

        private void Editor_TextChanged(object sender, EventArgs e)
        {
            // Invalidate the line-to-token mapping when text changes
            editorLineToTokenLine.Clear();
        }

        private int GetSelectedLineCount(out int startLine, out int endLine)
        {
            startLine = endLine = 0;

            if (Editor.Document == null || Editor.TextArea.Selection == null || Editor.TextArea.Selection.IsEmpty)
                return 0;

            var selection = Editor.TextArea.Selection.SurroundingSegment;
            if (selection == null)
                return 0;

            startLine = Editor.Document.GetLineByOffset(selection.Offset).LineNumber;
            int endOffset = Math.Max(selection.Offset, selection.EndOffset - 1);
            endLine = Editor.Document.GetLineByOffset(endOffset).LineNumber;

            return (endLine - startLine) + 1;
        }

        private uint MapEditorLineToTokenLine(int editorLineNumber)
        {
            if (editorLineNumber <= 0)
                return 0;

            // Rebuild mapping if cache is empty
            if (editorLineToTokenLine.Count == 0 && Editor.Document != null && Editor.Document.LineCount > 0)
                BuildEditorToTokenLineMapping();

            // Look up the token line for this editor line
            if (editorLineToTokenLine.ContainsKey(editorLineNumber))
                return editorLineToTokenLine[editorLineNumber];

            // If not found, walk backwards to find the previous mapped line
            for (int line = editorLineNumber - 1; line >= 1; line--)
            {
                if (editorLineToTokenLine.ContainsKey(line))
                    return editorLineToTokenLine[line];
            }

            // Fallback: return editor line if no mapping found
            return (uint)editorLineNumber;
        }

        private void BuildEditorToTokenLineMapping()
        {
            editorLineToTokenLine.Clear();

            if (Editor.Document == null || Editor.Document.LineCount == 0)
                return;

            try
            {
                var parser = new GCodeParser();
                bool isComment;
                uint parsedLineNum;

                for (int editorLine = 1; editorLine <= Editor.Document.LineCount; editorLine++)
                {
                    string lineText = GetLineText(editorLine).Trim();

                    if (string.IsNullOrEmpty(lineText))
                        continue;

                    // Make a copy since ParseBlock modifies the string
                    string testLine = lineText;

                    // Try to parse the line
                    if (parser.ParseBlock(ref testLine, true, out parsedLineNum, out isComment))
                    {
                        // If it's not a comment, it produced a token - map this editor line
                        if (!isComment)
                        {
                            editorLineToTokenLine[editorLine] = parsedLineNum;
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, leave mapping empty (fallback to editor line numbers)
            }
        }

        private string GetLineText(int lineNumber)
        {
            if (Editor.Document == null)
                return null;

            var line = Editor.Document.LineByNumber(lineNumber);
            return line?.Length > 0 ? line.Text : null;
        }
    }
}
