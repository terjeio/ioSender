/*
 * JogControl.xaml.cs - part of CNC Controls library
 *
 * v0.01 / 2020-01-24 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020, Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for JogControl.xaml
    /// </summary>
    public partial class JogControl : UserControl
    {
        private double distance = 1d, feedrate = 500d;

        public JogControl()
        {
            InitializeComponent();

            DataContextChanged += View_DataContextChanged;
        }
        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null && e.OldValue is INotifyPropertyChanged)
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= OnDataContextPropertyChanged;
            if (e.NewValue != null && e.NewValue is INotifyPropertyChanged)
                (e.NewValue as GrblViewModel).PropertyChanged += OnDataContextPropertyChanged;
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel && Visibility == Visibility.Visible) switch (e.PropertyName)
            {
                case nameof(GrblViewModel.StreamingState):

                        if ((sender as GrblViewModel).IsJobRunning)
                            Visibility = Visibility.Hidden;
                    break;
            }
        }

        private void dx10_Click(object sender, RoutedEventArgs e)
        {
            distance = dbl.Parse((string)((RadioButton)sender).Content);
        }

        private void f1000_Click(object sender, RoutedEventArgs e)
        {
            feedrate = dbl.Parse((string)((RadioButton)sender).Content);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string cmd = string.Format("$J=G91{0}{1}F{2}", ((string)((Button)sender).Content).Replace("+", ""), distance.ToInvariantString(), feedrate.ToInvariantString());
            ((GrblViewModel)DataContext).ExecuteMDI(cmd);
        }
    }
}
