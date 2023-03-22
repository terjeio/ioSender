/*
 * ColoPicker.xaml.cs - part of Grbl Code Sender
 *
 * v0.42 / 2023-02-17 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2023, Io Engineering (Terje Io)
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CNC.Controls.Viewer
{
    /// <summary>
    /// Interaction logic for ColorPicker.xaml
    /// </summary>
    public partial class ColorPicker : UserControl
    {
        bool xx = false;

        public ColorPicker()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPicker), new PropertyMetadata(Colors.AliceBlue, new PropertyChangedCallback(OnIsSelectedColorChanged)));
        public Color SelectedColor
        {
            get { return (Color)GetValue(SelectedColorProperty); }
            set { SetValue(SelectedColorProperty, value); }
        }
        private static void OnIsSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorPicker)d).cbut.Background = new SolidColorBrush((Color)e.NewValue);
        }

        public static readonly DependencyProperty IsPickerOpenProperty = DependencyProperty.Register(nameof(IsPickerOpen), typeof(bool), typeof(ColorPicker), new PropertyMetadata(false));
        public bool IsPickerOpen
        {
            get { return (bool)GetValue(IsPickerOpenProperty); }
            set { SetValue(IsPickerOpenProperty, value); }
        }

        private void Popup_Open(object sender, RoutedEventArgs e)
        {
            if ((IsPickerOpen = !IsPickerOpen) && !xx)
            {
                xx = true;
                var ccv = new ColorConverter();
                var colors = (typeof(Colors)).GetProperties();

                const double btnSize = 18d;

                for (var i = 0; i < colors.Length; i++)
                {

//                    if (colors[i].Name == "Transparent")
//                        continue;

                    var color = (Color)ColorConverter.ConvertFromString(colors[i].Name);

                    Button b = new Button
                    {
                        Width = btnSize,
                        Height = btnSize,
                        Focusable = false,
                        Background = new SolidColorBrush(color)
                    };

                    b.Click += Color_Click;

                    popup.Children.Add(b);
                    Canvas.SetTop(b, 14d + Math.Floor((double)(i / 10)) * (btnSize + 2d));
                    Canvas.SetLeft(b, 4d + (i % 10) * (btnSize + 2d));
                }

                popup.Height = 16d + 15d * (btnSize + 2d);
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = ((SolidColorBrush)(sender as Button).Background).Color;
            IsPickerOpen = false;
        }

        private void Popup_Close(object sender, RoutedEventArgs e)
        {
            IsPickerOpen = false;
        }
    }
}
