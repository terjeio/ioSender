/*
 * UIUtils.cs - part of CNC Controls library
 *
 * v0.36 / 2021-12-25 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2021, Io Engineering (Terje Io)
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.ComponentModel;
using CNC.Core;

namespace CNC.Controls
{
    public class NumericProperties
    {
        public int Length;
        public int Precision;
        public bool AllowDP;
        public bool AllowSign;
        public string DisplayFormat;
        public NumberStyles Styles;

        public static string MetricFormat { get { return NumberFormatInfo.CurrentInfo.NegativeSign + GrblConstants.FORMAT_METRIC; } }
        public static string ImperialFormat { get { return NumberFormatInfo.CurrentInfo.NegativeSign + GrblConstants.FORMAT_IMPERIAL; } }

        public NumericProperties()
        {
            Parse(MetricFormat);
        }

        private void Parse (string format)
        {
            AllowSign = format.StartsWith("-");
            DisplayFormat = AllowSign ? format.Substring(1) : format;
            Length = format.Length - (AllowSign ? 1 : 0);

            if ((AllowDP = format.Contains('.')))
            {
                Precision = Length - format.LastIndexOf('.') - (AllowSign ? 0 : 1);
                //    this.Length -= (this.Precision + 1);
            }
            else
                Precision = 0;

            Styles = (AllowDP ? NumberStyles.AllowDecimalPoint : NumberStyles.None) |
                            (AllowSign ? NumberStyles.AllowLeadingSign : NumberStyles.None);
        }

        public static void OnFormatChanged(DependencyObject d, NumericProperties np, string format)
        {
            np.Parse(format);

            Size size = UIUtils.MeasureText("".PadRight(np.Length, '9'), (Control)d);

            ((Control)d).Width = size.Width + (d is NumericTextBox ? 12 : 20);
            //((NumericTextBox)sender).Width = TextRenderer.MeasureText("".PadRight(Length, '9'), ((NumericTextBox)sender).Font);
        }

        public static bool IsStringNumeric(string value, NumericProperties np)
        {
            bool ok = true;
            int len = value.Length, i = 0, dp = -1;

            if (np.AllowSign && value.StartsWith(NumberFormatInfo.CurrentInfo.NegativeSign))
                i++;

            for (; i < len; i++)
            {
                if (np.AllowDP && dp == -1 && value[i] == '.')
                    dp = i;
                else
                    ok &= char.IsDigit(value[i]);
            }

            if (ok && dp >= 0)
                ok = len - dp - 1 <= np.Precision;

            return ok && len <= np.Length;
        }
    }

    public class NumericRangeRule : ValidationRule
    {
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            double v = (double)value;

            if(!double.IsNaN(Min) && !double.IsNaN(Max) && (v < Min || v > Max))
                return new ValidationResult(false, string.Format(LibStrings.FindResource("ValAllowedRange"), Min, Max));

            if (!double.IsNaN(Min) && v < Min)
                return new ValidationResult(false, string.Format(LibStrings.FindResource("ValAllowedMin"), Min));

            if (!double.IsNaN(Max) && v > Max)
                return new ValidationResult(false, string.Format(LibStrings.FindResource("ValAllowedMax"), Max));

            return ValidationResult.ValidResult;
        }
    }

    public class IP4ValueRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string ip4address = (string)value;

            System.Net.IPAddress ip4;
            if(!System.Net.IPAddress.TryParse(ip4address, out ip4))
                return new ValidationResult(false, (string)LibStrings.FindResource("ValNotIPV4"));

            return ValidationResult.ValidResult;
        }
    }

    public class UIUtils
    {
        const int WM_KEYDOWN = 0x100;
        const int WM_SYSKEYDOWN = 0x0104;


        // https://stackoverflow.com/questions/3480966/display-hourglass-when-application-is-busy - by Carlo
        public class WaitCursor : IDisposable
        {
            private Cursor _previousCursor;

            public WaitCursor()
            {
                _previousCursor = Mouse.OverrideCursor;

                Mouse.OverrideCursor = Cursors.Wait;
            }

            #region IDisposable Members

            public void Dispose()
            {
                Mouse.OverrideCursor = _previousCursor;
            }

            #endregion
        }

        public static T TryFindParent<T>(DependencyObject current) where T : class
        {
            DependencyObject parent = VisualTreeHelper.GetParent(current);
            if (parent == null)
                parent = LogicalTreeHelper.GetParent(current);

            if (parent == null)
                return null;

            if (parent is T)
                return parent as T;
            else
                return TryFindParent<T>(parent);
        }

        public static IEnumerable<T> FindFirstLogicalChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                foreach (object rawChild in LogicalTreeHelper.GetChildren(depObj))
                {
                    if (rawChild is DependencyObject)
                    {
                        DependencyObject child = (DependencyObject)rawChild;
                        if (child is T)
                        {
                            yield return (T)child;
                        }

                        else foreach (T childOfChild in FindFirstLogicalChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }

        public static IEnumerable<T> FindLogicalChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                foreach (object rawChild in LogicalTreeHelper.GetChildren(depObj))
                {
                    if (rawChild is DependencyObject)
                    {
                        DependencyObject child = (DependencyObject)rawChild;
                        if (child is T)
                        {
                            yield return (T)child;
                        }

                        foreach (T childOfChild in FindLogicalChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }

        // by WPFGermany - https://stackoverflow.com/questions/41132649/get-datagrids-scrollviewer
        public static ScrollViewer GetScrollViewer(UIElement element)
        {
            if (element == null) return null;

            ScrollViewer retour = null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element) && retour == null; i++)
            {
                if (VisualTreeHelper.GetChild(element, i) is ScrollViewer)
                {
                    retour = (ScrollViewer)(VisualTreeHelper.GetChild(element, i));
                }
                else
                {
                    retour = GetScrollViewer(VisualTreeHelper.GetChild(element, i) as UIElement);
                }
            }
            return retour;
        }

        // By Todd McQuay, https://stackoverflow.com/questions/16342663/winforms-to-wpf-measure-text-widthgraphics-measurecharacterranges
        public static Size MeasureText(string text,
            FontFamily family, double size, FontStyle style, FontWeight weight, FontStretch stretch, TextFormattingMode formattingMode)
        {
            FormattedText formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(family, style, weight, stretch),
                size,
                Brushes.Black,
                null,
                formattingMode);

            return new Size(formattedText.Width, formattedText.Height);
        }

        public static Size MeasureText(string text, Control control)
        {
            return MeasureText(text, control.FontFamily, control.FontSize,
                control.FontStyle, control.FontWeight, control.FontStretch,
                TextOptions.GetTextFormattingMode(control));
        }
        // End byTodd McQuay



        //public static void GroupBoxCaptionBold(GroupBox groupBox)
        //{
        //    foreach (Control c in groupBox.Controls)
        //        c.Font = groupBox.Parent.Font;

        //    groupBox.Font = new Font(groupBox.Font.Name, groupBox.Font.SizeInPoints, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        //}
    }

    // By O. R. Mapper, https://stackoverflow.com/questions/10097417/how-do-i-create-an-autoscrolling-textbox
    public static class TextBoxUtilities
    {
        public static readonly DependencyProperty AlwaysScrollToEndProperty = DependencyProperty.RegisterAttached("AlwaysScrollToEnd",
                                                                                                                  typeof(bool),
                                                                                                                  typeof(TextBoxUtilities),
                                                                                                                  new PropertyMetadata(false, AlwaysScrollToEndChanged));

        private static void AlwaysScrollToEndChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null)
            {
                bool alwaysScrollToEnd = (e.NewValue != null) && (bool)e.NewValue;
                if (alwaysScrollToEnd)
                {
                    tb.ScrollToEnd();
                    tb.TextChanged += TextChanged;
                }
                else
                {
                    tb.TextChanged -= TextChanged;
                }
            }
            else
            {
                throw new InvalidOperationException("The attached AlwaysScrollToEnd property can only be applied to TextBox instances.");
            }
        }

        public static bool GetAlwaysScrollToEnd(TextBox textBox)
        {
            if (textBox == null)
            {
                throw new ArgumentNullException("textBox");
            }

            return (bool)textBox.GetValue(AlwaysScrollToEndProperty);
        }

        public static void SetAlwaysScrollToEnd(TextBox textBox, bool alwaysScrollToEnd)
        {
            if (textBox == null)
            {
                throw new ArgumentNullException("textBox");
            }

            textBox.SetValue(AlwaysScrollToEndProperty, alwaysScrollToEnd);
        }

        private static void TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
        }
    }
}
