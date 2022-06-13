/*
 * NumericTextBox.cs - part of CNC Controls library
 *
 * v0.38 / 2022-06-13 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CNC.Controls
{
    public class NumericTextBox : TextBox
    {
        private NumericProperties np = new NumericProperties();

        private bool updateText = true;

        public NumericTextBox()
        {
            HorizontalContentAlignment = HorizontalAlignment.Right;
            VerticalContentAlignment = VerticalAlignment.Bottom;
            Height = 24;
            TextWrapping = TextWrapping.NoWrap;
        }

        public new string Text { get { return base.Text; } set { base.Text = value; } }
        public NumberStyles Styles { get { return np.Styles; } }
        public string DisplayFormat { get { return np.DisplayFormat; } }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericTextBox), new PropertyMetadata(double.NaN, new PropertyChangedCallback(OnValueChanged)));
        public double Value
        {
            get { double v = (double)GetValue(ValueProperty); return v == double.NaN ? 0d : v; }
            set { SetValue(ValueProperty, value); }
        }
        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (((NumericTextBox)d).updateText)
                ((NumericTextBox)d).Text = double.IsNaN((double)e.NewValue) || double.IsNegativeInfinity((double)e.NewValue) ? "" : Math.Round((double)e.NewValue, ((NumericTextBox)d).np.Precision).ToString(((NumericTextBox)d).np.DisplayFormat, CultureInfo.InvariantCulture);
        }
        //        public static bool CoerceValueChanged(DependencyObject d, object value)
        //        {
        //            double v = (double)value;
        //            NumericTextBox ntb = (NumericTextBox)d;
        //            return get { return (double.IsNaN(ValueMin) || Value >= ValueMin) && (double.IsNaN(ValueMax) || Value <= ValueMax); }
        //;
        //        }

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(nameof(Format), typeof(string), typeof(NumericTextBox), new PropertyMetadata(NumericProperties.MetricFormat, new PropertyChangedCallback(OnFormatChanged)));
        public string Format
        {
            get { return (string)GetValue(FormatProperty); }
            set { SetValue(FormatProperty, value); }
        }
        private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            NumericProperties.OnFormatChanged(d, ((NumericTextBox)d).np, (string)e.NewValue);
        }

        public new void Clear()
        {
            updateText = false;
            Value = double.NaN;
            updateText = true;
            Text = string.Empty;
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                string text = SelectionLength > 0 ? Text.Remove(SelectionStart, SelectionLength) : Text;

                updateText = false;
                Value = double.Parse(text == string.Empty || text == "." || text == "-" || text == "-." ? "0" : text, np.Styles, CultureInfo.InvariantCulture);
                updateText = true;
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox)e.OriginalSource;
            string text = textBox.SelectionLength > 0 ? textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength) : textBox.Text;
            text = text.Insert(textBox.CaretIndex, e.Text);
            if (!(e.Handled = !NumericProperties.IsStringNumeric(text, np)))
            {
                updateText = false;
                Value = double.Parse(text == "" || text == "." || text == "-" || text == "-." ? "0" : text, np.Styles, CultureInfo.InvariantCulture);
                updateText = true;
            }

            base.OnPreviewTextInput(e);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            double val = 0d;
            if (double.TryParse(Text == string.Empty ? "NaN" : Text, np.Styles, CultureInfo.InvariantCulture, out val))
            {
                if (!IsReadOnly && IsEnabled)
                {
                    updateText = false;
                    Value = val;
                    updateText = true;
                }

                base.OnTextChanged(e);
            }
            else
                Text = Math.Round(Value, (np.Precision)).ToString(np.DisplayFormat, CultureInfo.InvariantCulture);
        }
    }
}
