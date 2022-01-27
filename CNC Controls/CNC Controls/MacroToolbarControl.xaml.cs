/*
 * MacroExecuteControl.xaml.cs - part of CNC Controls library
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

using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CNC.Core;

namespace CNC.Controls
{
    /// <summary>
    /// Interaction logic for MacroToolbarControl.xaml
    /// </summary>
    public partial class MacroToolbarControl : UserControl
    {
        public MacroToolbarControl()
        {
            InitializeComponent();
        }

        private void macroToolbarControl_Loaded(object sender, RoutedEventArgs e)
        {
            Macros = AppConfig.Settings.Macros;
        }

        public static readonly DependencyProperty MacrosProperty = DependencyProperty.Register(nameof(MacroToolbarControl.Macros), typeof(ObservableCollection<CNC.GCode.Macro>), typeof(MacroToolbarControl));
        public ObservableCollection<CNC.GCode.Macro> Macros
        {
            get { return (ObservableCollection<CNC.GCode.Macro>)GetValue(MacrosProperty); }
            set { SetValue(MacrosProperty, value); }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var macro = Macros.FirstOrDefault(o => o.Id == (int)(sender as Button).Tag);
            if (macro != null && (!macro.ConfirmOnExecute || MessageBox.Show(string.Format((string)FindResource("RunMacro"), macro.Name), "ioSender", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
                (DataContext as GrblViewModel).ExecuteMacro(macro.Code);
        }
    }
}
