/*
 * CssControl.cs - part of CNC Controls Lathe library
 *
 * v0.03 / 2020-01-28 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2020, Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;

namespace CNC.Controls.Lathe
{
    /// <summary>
    /// Interaction logic for CssControl.xaml
    /// </summary>
    public partial class CssControl : UserControl
    {
        public CssControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsCssEnabledProperty = DependencyProperty.Register(nameof(IsCssEnabled), typeof(bool), typeof(CssControl), new PropertyMetadata(new PropertyChangedCallback(OnCssEnabledChanged)));
        public bool? IsCssEnabled
        {
            get { return (bool?)GetValue(IsCssEnabledProperty); }
            set { SetValue(IsCssEnabledProperty, value); }
        }

        private static void OnCssEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CssControl).data.Label = ((CssControl)d).IsCssEnabled == true ? "Speed:" : "Spindle:";
            (d as CssControl).data.Unit = ((CssControl)d).IsCssEnabled == true ? (d as CssControl).Unit : "RPM";
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CssControl), new PropertyMetadata(double.NaN));
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(CssControl), new PropertyMetadata("m/min", new PropertyChangedCallback(OnUnitChanged)));
        public string Unit
        {
            get { return (string)GetValue(UnitProperty); }
            set { SetValue(UnitProperty, value); }
        }
        private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CssControl).OnUnitChanged();
        }
        private void OnUnitChanged()
        {
            if(IsCssEnabled == true)
                data.Unit = Unit;
        }
    }
}
