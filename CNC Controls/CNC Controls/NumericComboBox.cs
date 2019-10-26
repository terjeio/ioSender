/*
 * NumericComboBox.cs - part of CNC Controls library
 *
 * v0.02 / 2019-10-15 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CNC.Controls
{
    public class NumericComboBox : ComboBox
    {
        private NumericProperties np = new NumericProperties();

        public NumericComboBox()
        {
            IsEditable = true;
        }

        public double Value
        {
            get
            {
                double value = 0.0d;
                double.TryParse(Text, np.Styles, CultureInfo.InvariantCulture, out value);
                return value;
            }
            set
            {
                Text = Math.Round(value, np.Precision).ToString(np.DisplayFormat, CultureInfo.InvariantCulture);
            }
        }

        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(NumericComboBox), new PropertyMetadata(GrblConstants.FORMAT_METRIC, new PropertyChangedCallback(OnFormatChanged)));
        public string Format
        {
            get { return (string)GetValue(FormatProperty); }
            set { SetValue(FormatProperty, value); }
        }
        private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            NumericProperties.OnFormatChanged(d, ((NumericComboBox)d).np, (string)e.NewValue);
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            TextBox textBox = (TextBox)e.OriginalSource;
            string text = textBox.SelectionLength > 0 ? textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength) : textBox.Text;
            text = text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !NumericProperties.IsStringNumeric(text, np);
            base.OnPreviewTextInput(e);
        }
    }
}
