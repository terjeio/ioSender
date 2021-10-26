/*
 * DROBaseControl.xaml.cs - part of CNC Controls library
 *
 * v0.03 / 2020-01-27 / Io Engineering (Terje Io)
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CNC.Controls
{
    public partial class DROBaseControl : UserControl
    {
        private static Brush ScaledOn = Brushes.Yellow, ScaledOff;

        public delegate void ZeroClickHandler(object sender, RoutedEventArgs e);
        public event ZeroClickHandler ZeroClick;

        public DROBaseControl()
        {
            InitializeComponent();

            ScaledOff = btnScaled.Background;
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(DROBaseControl), new PropertyMetadata());
        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(DROBaseControl), new PropertyMetadata());
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public bool IsReadOnly
        {
            get { return txtReadout.IsReadOnly; }
            set { txtReadout.IsReadOnly = value; }
        }

        public static readonly DependencyProperty IsScaledProperty = DependencyProperty.Register(nameof(IsScaled), typeof(bool), typeof(DROBaseControl), new PropertyMetadata(false, new PropertyChangedCallback(OnIsScaledChanged)));
        public bool IsScaled
        {
            get { return (bool)GetValue(IsScaledProperty); }
            set { SetValue(IsScaledProperty, value); }
        }
        private static void OnIsScaledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DROBaseControl)d).btnScaled.Background = (bool)e.NewValue ? ScaledOn : ScaledOff;
        }

        public new object Tag
        {
            get { return txtReadout.Tag; }
            set { txtReadout.Tag = btnZero.Tag = value; }
        }

        private void btnZero_Click(object sender, RoutedEventArgs e)
        {
            ZeroClick?.Invoke(sender, e);
        }
    }
}

