using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows.Input;
using System.Text;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using CNC.Core;

namespace CNC.Controls
{


    public enum GCodeLineStatus
    {
        Ready,
        Complete,
        Error,
        Info
    }

    /// <summary>
    /// Interaction logic for GcodeTextControl.xaml
    /// </summary>
    public partial class GcodeTextControl : UserControl
    {
        private readonly GCodeLineStatusMargin statusMargin = new GCodeLineStatusMargin();
        private GrblViewModel model;

        public GcodeTextControl()
        {
            InitializeComponent();

            statusMargin.Opacity = 0d;
            ApplySyntaxHighlightingForTheme();

            Editor.TextArea.LeftMargins.Insert(1, statusMargin);
            Editor.TextArea.SelectionChanged += Editor_SelectionChanged;
            GCode.File.GetEditedText = () => Editor.Text;
            ctxMenu.DataContext = this;

            Unloaded += UserControl_Unloaded;
        }

        private void ApplySyntaxHighlightingForTheme()
        {
            Editor.SyntaxHighlighting = LoadSyntaxDefinition();
            ApplyGutterThemeFromHighlighting(Editor.SyntaxHighlighting);
        }

        private IHighlightingDefinition LoadSyntaxDefinition()
        {
            bool darkTheme = IsDarkThemeSelected();
            string syntaxFile = darkTheme ? "Dark.xshd" : "Light.xshd";
            EnsureUserSyntaxFile(syntaxFile);

            string userSyntaxPath = GetUserSyntaxPath(syntaxFile);
            if (File.Exists(userSyntaxPath))
            {
                using (var stream = File.OpenRead(userSyntaxPath))
                {
                    var highlighting = LoadHighlightingFromStream(stream);
                    if (highlighting != null)
                        return highlighting;
                }
            }

            using (var stream = OpenBundledSyntaxStream(syntaxFile))
            {
                var highlighting = LoadHighlightingFromStream(stream);
                if (highlighting != null)
                    return highlighting;
            }

            using (var stream = GetType().Assembly.GetManifestResourceStream("CNC.Controls.Resources.gcode.xshd"))
            {
                if (stream != null)
                {
                    using (var reader = XmlReader.Create(stream))
                        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            return HighlightingManager.Instance.GetDefinition("C#");
        }

        private static string GetUserSyntaxPath(string syntaxFile)
        {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ioSender", "Syntax");
            return Path.Combine(basePath, syntaxFile);
        }

        private static void EnsureUserSyntaxFile(string syntaxFile)
        {
            string userPath = GetUserSyntaxPath(syntaxFile);
            string userFolder = Path.GetDirectoryName(userPath);

            if (string.IsNullOrEmpty(userFolder))
                return;

            if (!Directory.Exists(userFolder))
                Directory.CreateDirectory(userFolder);

            if (File.Exists(userPath))
                return;

            using (var bundled = OpenBundledSyntaxStream(syntaxFile))
            {
                if (bundled == null)
                    return;

                using (var output = File.Create(userPath))
                    bundled.CopyTo(output);
            }
        }

        private static Stream OpenBundledSyntaxStream(string syntaxFile)
        {
            if (Application.Current == null)
                return null;

            var info = Application.GetResourceStream(new Uri(string.Format("pack://application:,,,/Syntax/{0}", syntaxFile), UriKind.Absolute));
            return info?.Stream;
        }

        private IHighlightingDefinition LoadHighlightingFromStream(Stream stream)
        {
            if (stream == null)
                return null;

            try
            {
                using (var copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    copy.Position = 0;

                    using (var reader = XmlReader.Create(copy))
                        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            catch
            {
                return null;
            }
        }

        private void ApplyGutterThemeFromHighlighting(IHighlightingDefinition highlighting)
        {
            if (Editor == null)
                return;

            statusMargin.SetTheme(Editor.Background, Editor.Foreground);
        }

        private static string GetHighlightingProperty(IHighlightingDefinition highlighting, string propertyName)
        {
            foreach (var property in highlighting.Properties)
            {
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
        }

        private void ApplyGutterThemeFromSyntaxStream(Stream stream)
        {
            if (stream == null || !stream.CanRead)
                return;

            try
            {
                var doc = XDocument.Load(stream);
                ApplyGutterThemeFromSyntaxDocument(doc);
            }
            catch
            {
            }
        }

        private void ApplyGutterThemeFromSyntaxDocument(XDocument doc)
        {
            if (doc == null || doc.Root == null)
                return;

            XNamespace ns = doc.Root.Name.Namespace;

            string back = GetSyntaxPropertyValue(doc, ns, "GutterBackColor");
            string fore = GetSyntaxPropertyValue(doc, ns, "GutterForeColor");

            if (TryParseSyntaxBrush(back, out var backBrush) && TryParseSyntaxBrush(fore, out var foreBrush))
                statusMargin.SetTheme(backBrush, foreBrush);
        }

        private static string GetSyntaxPropertyValue(XDocument doc, XNamespace ns, string propertyName)
        {
            foreach (var property in doc.Root.Elements(ns + "Property"))
            {
                var name = (string)property.Attribute("name");
                if (string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
                    return (string)property.Attribute("value");
            }

            return null;
        }

        private static bool TryParseSyntaxBrush(string value, out Brush brush)
        {
            if (TryParseLegacyBgrBrush(value, out brush))
                return true;

            return TryParseBrush(value, out brush);
        }

        private static bool TryParseLegacyBgrBrush(string value, out Brush brush)
        {
            brush = null;

            if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
                return false;

            string hex = value.Substring(1);

            try
            {
                byte a = 0xFF;
                byte r;
                byte g;
                byte b;

                // Legacy ordering used by some syntax definitions: #BBGGRR / #AABBGGRR
                if (hex.Length == 6)
                {
                    b = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    r = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                else if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    b = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    r = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                else
                {
                    return false;
                }

                var solid = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                if (solid.CanFreeze)
                    solid.Freeze();

                brush = solid;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseBrush(string value, out Brush brush)
        {
            brush = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                var converted = ColorConverter.ConvertFromString(value);
                if (!(converted is Color color))
                    return false;

                var solid = new SolidColorBrush(color);
                if (solid.CanFreeze)
                    solid.Freeze();

                brush = solid;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDarkThemeSelected()
        {
            string colorMode = AppConfig.ColorMode;
            return !string.IsNullOrEmpty(colorMode) &&
                   (colorMode.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                    colorMode.Equals("Black", StringComparison.OrdinalIgnoreCase));
        }

        #region Dependency properties

        public static readonly DependencyProperty SingleSelectedProperty = DependencyProperty.Register(nameof(SingleSelected), typeof(bool), typeof(GcodeTextControl), new PropertyMetadata(false));
        public bool SingleSelected
        {
            get { return (bool)GetValue(SingleSelectedProperty); }
            private set { SetValue(SingleSelectedProperty, value); }
        }

        public static readonly DependencyProperty MultipleSelectedProperty = DependencyProperty.Register(nameof(MultipleSelected), typeof(bool), typeof(GcodeTextControl), new PropertyMetadata(false));
        public bool MultipleSelected
        {
            get { return (bool)GetValue(MultipleSelectedProperty); }
            private set { SetValue(MultipleSelectedProperty, value); }
        }

        #endregion

        //add AllowEditing property to enable/disable editing of the text
        public bool IsReadonly
        {
            get => Editor.IsReadOnly == false;
            set => Editor.IsReadOnly = !value;
        }

        public void LoadFile(string filename)
        {
            if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
            {
                try
                {
                    // Load the raw file text into the editor so original spacing is preserved
                    var raw = System.IO.File.ReadAllText(filename);
                    Editor.Text = raw ?? string.Empty;
                }
                catch
                {
                    // Fall back to the editor's load if direct read fails for any reason
                    Editor.Load(filename);
                }
            }
            else
            {
                Editor.Clear();
            }

            Editor.IsModified = false;
        }

        public bool SaveAndReload(string filename = null)
        {
            filename = string.IsNullOrEmpty(filename) ? GCode.File.FileName : filename;

            if (string.IsNullOrEmpty(filename))
                return false;

            Editor.Save(filename);
            GCode.File.LoadFromEditor(filename, Editor.Text);
            Editor.IsModified = false;

            return true;
        }

        public void SetRange(int startLine, int endLine)
        {
            statusMargin.SetRange(startLine, endLine, GCodeLineStatus.Ready);
        }

        public void SetRange(int startLine, int endLine, GCodeLineStatus status)
        {
            statusMargin.SetRange(startLine, endLine, status);
        }

        public void AddRange(int startLine, int endLine, GCodeLineStatus status)
        {
            statusMargin.AddRange(startLine, endLine, status);
        }

        public void ClearRange()
        {
            statusMargin.ClearRange();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AttachGCodeStatusHandlers();

            AppConfig.Settings.PropertyChanged -= AppConfig_PropertyChanged;
            AppConfig.Settings.PropertyChanged += AppConfig_PropertyChanged;
            ApplySyntaxHighlightingForTheme();
            statusMargin.Opacity = 1d;

            if (DataContext is GrblViewModel newModel)
            {
                if (model != null)
                    model.PropertyChanged -= GcodeTextControl_PropertyChanged;

                model = newModel;
                model.PropertyChanged += GcodeTextControl_PropertyChanged;
            }

            statusMargin.Refresh();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (model != null)
                model.PropertyChanged -= GcodeTextControl_PropertyChanged;

            AppConfig.Settings.PropertyChanged -= AppConfig_PropertyChanged;

            DetachGCodeStatusHandlers();
        }

        private void AppConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.ColorMode) || string.IsNullOrEmpty(e.PropertyName))
            {
                if (!Dispatcher.CheckAccess())
                    Dispatcher.BeginInvoke((System.Action)(() => ApplySyntaxHighlightingForTheme()));
                else
                    ApplySyntaxHighlightingForTheme();
            }
        }

        private void AttachGCodeStatusHandlers()
        {
            var data = GCode.File.Data;
            if (data == null)
                return;

            data.CollectionChanged -= GCodeData_CollectionChanged;
            data.CollectionChanged += GCodeData_CollectionChanged;

            foreach (var row in data)
                row.PropertyChanged += GCodeRow_PropertyChanged;
        }

        private void DetachGCodeStatusHandlers()
        {
            var data = GCode.File.Data;
            if (data == null)
                return;

            data.CollectionChanged -= GCodeData_CollectionChanged;

            foreach (var row in data)
                row.PropertyChanged -= GCodeRow_PropertyChanged;
        }

        private void GCodeData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    var row = item as GCodeBlock;
                    if (row != null)
                        row.PropertyChanged -= GCodeRow_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    var row = item as GCodeBlock;
                    if (row != null)
                        row.PropertyChanged += GCodeRow_PropertyChanged;
                }
            }

            statusMargin.Refresh();
        }

        private void GCodeRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GCodeBlock.Sent) || string.IsNullOrEmpty(e.PropertyName))
            {
                if (!Dispatcher.CheckAccess())
                    Dispatcher.BeginInvoke((System.Action)(() => statusMargin.Refresh()));
                else
                    statusMargin.Refresh();
            }
        }

        private void GcodeTextControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.ScrollPosition):
                    int sp = ((GrblViewModel)sender).ScrollPosition;
                    if (sp <= 0)
                        Editor.ScrollToHome();
                    else
                        Editor.ScrollTo(sp + 1, 1);
                    break;
            }
        }

        private void Editor_Drag(object sender, DragEventArgs e)
        {
            GCode.File.Drag(sender, e);
        }

        private void Editor_Drop(object sender, DragEventArgs e)
        {
            GCode.File.Drop(sender, e);
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            int startLine, endLine;
            int selected = GetSelectedLineCount(out startLine, out endLine);

            bool canStart = DataContext is GrblViewModel && (DataContext as GrblViewModel).StartFromBlock.CanExecute(Math.Max(0, startLine - 1));

            SingleSelected = selected == 1 && canStart;
            MultipleSelected = selected >= 1 && canStart;
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

        private string GetLineText(int lineNumber)
        {
            if (Editor.Document == null || lineNumber <= 0 || lineNumber > Editor.Document.LineCount)
                return string.Empty;

            return Editor.Document.GetText(Editor.Document.GetLineByNumber(lineNumber));
        }

        private List<string> GetSelectedLines()
        {
            var lines = new List<string>();
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 0)
                return lines;

            for (int line = startLine; line <= endLine; line++)
            {
                string text = GetLineText(line);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }

            return lines;
        }

        private void StartHere_Click(object sender, RoutedEventArgs e)
        {
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 1 &&
                 ShowThemedMessageBox(string.Format(LibStrings.FindResource("VerifyStartFrom"), startLine),
                                      "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                (DataContext as GrblViewModel).StartFromBlock.Execute(Math.Max(0, startLine - 1));
            }
        }

        private void CopyMDI_Click(object sender, RoutedEventArgs e)
        {
            int startLine, endLine;

            if (GetSelectedLineCount(out startLine, out endLine) == 1)
                (DataContext as GrblViewModel).MDIText = GetLineText(startLine);
        }

        private void SendController_Click(object sender, RoutedEventArgs e)
        {
            var lines = GetSelectedLines();

            if (lines.Count >= 1 &&
                 ShowThemedMessageBox(LibStrings.FindResource("VerifySendController"), "ioSender",
                                      MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                var vm = DataContext as GrblViewModel;

                if (vm.GrblError != 0)
                    vm.ExecuteCommand("");

                foreach (var line in lines)
                    vm.ExecuteCommand(line);
            }
        }

        private MessageBoxResult ShowThemedMessageBox(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            var owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
            var dialog = new Window
            {
                Title = caption,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 360,
                MaxWidth = 640,
                Content = BuildDialogContent(message, buttons, defaultResult, icon)
            };

            dialog.ShowDialog();
            return dialog.Tag is MessageBoxResult ? (MessageBoxResult)dialog.Tag : defaultResult;
        }

        private UIElement BuildDialogContent(string message, MessageBoxButton buttons, MessageBoxResult defaultResult, MessageBoxImage icon)
        {
            var root = new DockPanel { Margin = new Thickness(16) };

            var textPanel = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(textPanel, Dock.Top);

            var iconBlock = new TextBlock
            {
                Text = icon == MessageBoxImage.Question ? "?" : icon == MessageBoxImage.Warning ? "!" : "i",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 280,
                MaxWidth = 580
            };

            textPanel.Children.Add(iconBlock);
            textPanel.Children.Add(messageBlock);
            root.Children.Add(textPanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            AddDialogButtons(buttonPanel, buttons, defaultResult);
            root.Children.Add(buttonPanel);

            return root;
        }

        private void AddDialogButtons(StackPanel panel, MessageBoxButton buttons, MessageBoxResult defaultResult)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    panel.Children.Add(CreateDialogButton("OK", MessageBoxResult.OK, defaultResult));
                    break;

                case MessageBoxButton.OKCancel:
                    panel.Children.Add(CreateDialogButton("OK", MessageBoxResult.OK, defaultResult));
                    panel.Children.Add(CreateDialogButton("Cancel", MessageBoxResult.Cancel, defaultResult));
                    break;

                case MessageBoxButton.YesNo:
                    panel.Children.Add(CreateDialogButton("Yes", MessageBoxResult.Yes, defaultResult));
                    panel.Children.Add(CreateDialogButton("No", MessageBoxResult.No, defaultResult));
                    break;

                default:
                    panel.Children.Add(CreateDialogButton("Yes", MessageBoxResult.Yes, defaultResult));
                    panel.Children.Add(CreateDialogButton("No", MessageBoxResult.No, defaultResult));
                    panel.Children.Add(CreateDialogButton("Cancel", MessageBoxResult.Cancel, defaultResult));
                    break;
            }
        }

        private Button CreateDialogButton(string caption, MessageBoxResult result, MessageBoxResult defaultResult)
        {
            var button = new Button
            {
                Content = caption,
                MinWidth = 85,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = result == defaultResult,
                IsCancel = result == MessageBoxResult.Cancel || result == MessageBoxResult.No
            };

            button.Click += (s, e) =>
            {
                var win = Window.GetWindow((Button)s);
                if (win != null)
                {
                    win.Tag = result;
                    win.DialogResult = true;
                    win.Close();
                }
            };

            return button;
        }
    }


    class GCodeLineStatusMargin : AbstractMargin
    {
        private struct StatusIndicator
        {
            public string Glyph { get; set; }
            public Brush Brush { get; set; }
        }

        private struct StatusRange
        {
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public GCodeLineStatus Status { get; set; }
        }

        private Brush backgroundBrush;
        private Pen dividerPen;
        private readonly List<StatusRange> ranges = new List<StatusRange>();
        private string currentToolTip;
        private readonly ToolTip hoverToolTip = new ToolTip();
        private static readonly Typeface IndicatorTypeface = new Typeface(new FontFamily("Segoe UI Symbol"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private const double IndicatorFontSize = 10d;

        public GCodeLineStatusMargin()
        {
            Width = 18d;
            hoverToolTip.Placement = PlacementMode.Mouse;
            hoverToolTip.PlacementTarget = this;
            hoverToolTip.StaysOpen = true;
            hoverToolTip.HasDropShadow = true;
            hoverToolTip.Padding = new Thickness(8, 6, 8, 6);
            SetTheme(new SolidColorBrush(Color.FromRgb(40, 40, 40)), new SolidColorBrush(Color.FromRgb(80, 80, 80)));
        }

        public void SetTheme(Brush background, Brush divider)
        {
            if (background == null || divider == null)
                return;

            backgroundBrush = background;
            if (backgroundBrush.CanFreeze)
                backgroundBrush.Freeze();

            var pen = new Pen(divider, .25d);
            if (pen.Brush != null && pen.Brush.CanFreeze)
                pen.Brush.Freeze();
            if (pen.CanFreeze)
                pen.Freeze();

            dividerPen = pen;
            InvalidateVisual();
        }

        public void Refresh()
        {
            InvalidateVisual();
        }

        public void SetRange(int startLine, int endLine, GCodeLineStatus status)
        {
            ranges.Clear();

            if (startLine <= 0 || endLine <= 0)
            {
                InvalidateVisual();
                return;
            }

            AddRange(startLine, endLine, status);
        }

        public void AddRange(int startLine, int endLine, GCodeLineStatus status)
        {
            if (startLine <= 0 || endLine <= 0)
                return;

            ranges.Add(new StatusRange
            {
                StartLine = Math.Min(startLine, endLine),
                EndLine = Math.Max(startLine, endLine),
                Status = status
            });

            InvalidateVisual();
        }

        public void ClearRange()
        {
            ranges.Clear();
            InvalidateVisual();
        }

        private StatusIndicator? GetIndicator(int lineNumber)
        {
            var data = GCode.File.Data;
            if (data != null && lineNumber >= 1 && lineNumber <= data.Count)
            {
                string sent = data[lineNumber - 1].Sent;

                if (!string.IsNullOrEmpty(sent))
                {
                    if (sent.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                        return GetIndicator(GCodeLineStatus.Error);

                    if (sent == "ok")
                        return GetIndicator(GCodeLineStatus.Complete);

                    return GetIndicator(GCodeLineStatus.Info);
                }
            }

            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                if (lineNumber >= ranges[i].StartLine && lineNumber <= ranges[i].EndLine)
                    return GetIndicator(ranges[i].Status);
            }

            return null;
        }

        private StatusIndicator GetIndicator(GCodeLineStatus status)
        {
            switch (status)
            {
                case GCodeLineStatus.Complete:
                    return new StatusIndicator { Glyph = "✓", Brush = Brushes.LimeGreen };
                case GCodeLineStatus.Error:
                    return new StatusIndicator { Glyph = "✗", Brush = Brushes.Red };
                case GCodeLineStatus.Info:
                    return new StatusIndicator { Glyph = "?", Brush = Brushes.Yellow };
                default:
                    return new StatusIndicator { Glyph = "", Brush = Brushes.Gray };
            }
        }

        private bool TryGetToolTipInfo(int lineNumber, out string title, out string message)
        {
            title = null;
            message = null;

            var data = GCode.File.Data;
            if (data == null || lineNumber < 1 || lineNumber > data.Count)
                return false;

            string sent = data[lineNumber - 1].Sent;
            if (string.IsNullOrWhiteSpace(sent) || !sent.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                return false;

            string code = GetErrorCode(sent);
            if (string.IsNullOrEmpty(code))
            {
                title = "Controller error";
                message = sent;
                return true;
            }

            title = string.Format("Error {0}", code);
            message = GrblErrors.GetMessage(code);

            if (string.IsNullOrEmpty(message) || string.Equals(message, sent, StringComparison.OrdinalIgnoreCase) || string.Equals(message, string.Format("error:{0}", code), StringComparison.OrdinalIgnoreCase))
                message = null;

            return true;
        }

        private object CreateToolTipContent(string title, string message)
        {
            var root = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var icon = new TextBlock
            {
                Text = "⚠",
                Foreground = Brushes.OrangeRed,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold
            });

            if (!string.IsNullOrWhiteSpace(message))
            {
                textPanel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 320,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            root.Children.Add(icon);
            root.Children.Add(textPanel);

            return root;
        }

        private static string GetErrorCode(string sent)
        {
            if (string.IsNullOrWhiteSpace(sent))
                return null;

            int separator = sent.IndexOf(':');
            if (separator < 0 || separator == sent.Length - 1)
                return null;

            var code = new StringBuilder();

            for (int i = separator + 1; i < sent.Length; i++)
            {
                char c = sent[i];
                if (char.IsDigit(c))
                    code.Append(c);
                else if (code.Length != 0)
                    break;
            }

            return code.Length == 0 ? null : code.ToString();
        }

        private int GetLineNumberFromPoint(Point position)
        {
            if (TextView == null || !TextView.VisualLinesValid)
                return 0;

            double y = position.Y + TextView.VerticalOffset;

            foreach (var visualLine in TextView.VisualLines)
            {
                double top = visualLine.VisualTop;
                double bottom = top + visualLine.Height;

                if (y >= top && y < bottom)
                    return visualLine.FirstDocumentLine.LineNumber;
            }

            return 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string title;
            string message;
            if (!TryGetToolTipInfo(GetLineNumberFromPoint(e.GetPosition(this)), out title, out message))
            {
                currentToolTip = null;
                hoverToolTip.IsOpen = false;
                return;
            }

            string toolTip = title + "\n" + (message ?? string.Empty);
            if (toolTip == currentToolTip)
                return;

            currentToolTip = toolTip;
            hoverToolTip.Content = CreateToolTipContent(title, message);
            hoverToolTip.IsOpen = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            currentToolTip = null;
            hoverToolTip.IsOpen = false;
            base.OnMouseLeave(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            drawingContext.DrawRectangle(backgroundBrush, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            drawingContext.DrawLine(dividerPen, new Point(0, 0), new Point(0, RenderSize.Height));

            if (TextView == null || !TextView.VisualLinesValid)
                return;

            foreach (var visualLine in TextView.VisualLines)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;

                if (visualLine.TextLines.Count == 0)
                    continue;

                var indicator = GetIndicator(lineNumber);
                if (!indicator.HasValue || string.IsNullOrEmpty(indicator.Value.Glyph))
                    continue;

                double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextMiddle) - TextView.VerticalOffset;
                var formattedText = new FormattedText(
                    indicator.Value.Glyph,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    IndicatorTypeface,
                    IndicatorFontSize,
                    indicator.Value.Brush);

                var x = (RenderSize.Width - formattedText.Width) / 2d;
                var textY = Math.Max(0d, y - (formattedText.Height / 2d));
                drawingContext.DrawText(formattedText, new Point(x, textY));
            }
        }
    }
}
