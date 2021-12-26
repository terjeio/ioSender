/*
 * JobParametersDialog.xaml.cs - part of CNC Converters library
 *
 * v0.33 / 2021-05-09 / Io Engineering (Terje Io)
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

using System.IO;
using System.Windows;
using System.Xml.Serialization;
using System.Windows.Threading;
using System.Windows.Input;
using CNC.Core;

namespace CNC.Converters
{
    public partial class JobParametersDialog : Window
    {
        const string suffix = "Conversion.xml";

        public JobParametersDialog(JobParametersViewModel model)
        {
            InitializeComponent();

            DataContext = model;

            Title = model.Profile + " conversion parameters";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var parent = Application.Current.MainWindow;

            Left = parent.Left + (parent.Width - Width) / 2d;
            Top = parent.Top + (parent.Height - Height) / 2d;

            try
            {
                using (StreamReader reader = new StreamReader(CNC.Core.Resources.Path + (DataContext as JobParametersViewModel).Profile + suffix))
                {
                    var settings = (JobParametersViewModel)new XmlSerializer(typeof(JobParametersViewModel)).Deserialize(reader);
                    Copy.Properties(settings, DataContext as JobParametersViewModel);
                }
            }
            catch
            {
            }

            (sender as Window).Dispatcher.Invoke(new System.Action(() =>
            {
                (sender as Window).MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.ContextIdle);
        }

        public bool SaveSettings()
        {
            bool ok = false;
            var settings = DataContext as JobParametersViewModel;

            try
            {
                using (FileStream fsout = new FileStream(CNC.Core.Resources.Path + settings.Profile + suffix, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(JobParametersViewModel)).Serialize(fsout, settings);
                    ok = true;
                }
            }
            catch
            {
            }

            return ok;
        }

        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }
        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
