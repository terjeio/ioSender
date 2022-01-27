/*
 * SidebarItem.cs - part of CNC Controls library for Grbl
 *
 * v0.36 / 2021-12-27 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2021, Io Engineering (Terje Io)
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
    public class SidebarItem : Button
    {
        private static double top = 0;
        private UserControl view { get; }
        private static UserControl last = null;

        public new Visibility Visibility { get { return view.Visibility; } set { view.Visibility = value; } }
        public new bool IsEnabled { get { return base.IsEnabled; } set { base.IsEnabled = value; } }

        public SidebarItem(ISidebarControl view) : base()
        {
            if (view.MenuLabel.Contains("_"))
                Content = new AccessText()
                {
                    Text = view.MenuLabel
                };
            else
                Content = view.MenuLabel;

            this.view = view as UserControl;

            Width = 75;
            Height = 25;
            Focusable = false;

            Canvas.SetTop(this.view, top);
            top += Width;

            try
            {
                Style = Application.Current.FindResource("btnSidebar") as Style;
            }
            catch { }

            LayoutTransform = new RotateTransform(90d);

            Click += button_Click;
        }

        public void PerformClick()
        {
            button_Click(this, null);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (last != null && last != view && last.IsVisible)
                last.Visibility = Visibility.Hidden;

            view.Visibility = view.IsVisible ? Visibility.Hidden : Visibility.Visible;
            last = view;
        }
    }
}
