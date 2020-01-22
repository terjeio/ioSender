/*
 * ProfileDialog.xaml.cs - part of CNC Controls library for Grbl
 *
 * v0.02 / 2020-01-16 / Io Engineering (Terje Io)
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
using System.Windows.Data;
using System;
using System.Globalization;
using System.Collections.ObjectModel;
using CNC.GCode;

namespace CNC.Controls.Lathe
{

    class DialogData : ProfileData
    {
        LatheMode _xmode;
        public LatheMode xmode {
            get { return _xmode; }
            set
            {
                _xmode = value;
                OnPropertyChanged();
            }

        }
        public bool xmodeenabled { get; set; }
        public Visibility threadVisibility { get; set; }
        public Visibility rpmVisibility { get; set; }
        public ObservableCollection<ProfileData> Profiles { get; set; }

        ProfileData _profile;

        public ProfileData Profile
        {
            get { return _profile; }
            set { _profile = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// Interaction logic for ProfileDialog.xaml
    /// </summary>
    public partial class ProfileDialog : Window
    {
        WizardConfig options;

        Window parent;

        private double last_rpm = 0.0, last_css = 0.0;

        DialogData profile = new DialogData();

        public ProfileDialog(WizardConfig options)
        {
            InitializeComponent();
            this.options = options;
            this.parent = Application.Current.MainWindow;
            this.Title += " - " + options.ProfileName;

            profile.xmode = options.ActiveProfile.xmode;
            profile.xmodeenabled = !options.ActiveProfile.xmodelock;
            profile.threadVisibility = options.ProfileName == "Threading" ? Visibility.Hidden : Visibility.Visible;
            profile.rpmVisibility = options.ProfileName != "Threading" ? Visibility.Hidden : Visibility.Visible;
            profile.Profiles = options.Profiles;

            DataContext = profile;

            CopyAll(options.ActiveProfile.Profile, profile);

            profile.Profile = options.ActiveProfile.Profile;
        }

        void cbxProfile_TextChanged(object sender, RoutedEventArgs e)
        {
            btnAddProfile.IsEnabled = cbxProfile.SelectedValue == null;
        }


        private void cbxProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profile.Profile != null)
            {
                last_css = profile.CSS && profile.RPM != 0.0d ? profile.RPM : 0.0d;
                last_rpm = profile.CSS || profile.RPM == 0.0d ? 0.0d : profile.RPM;

                CopyAll(profile.Profile, profile);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Left = parent.Left + (parent.Width - Width) / 2;
            Top = parent.Top + (parent.Height - Height) / 2;
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void btnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            if (profile.Profile == null)
            {
                ProfileData add = options.Add();

                profile.CSSMaxRPM = profile.CSS ? profile.CSSMaxRPM : 0.0d;
                profile.Name = cbxProfile.Text;

                CopyAll(profile, add);

                profile.Profile = add;
            }
        }

        void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (profile.Profile != null)
            {
                profile.CSSMaxRPM = profile.CSS ? profile.CSSMaxRPM : 0.0d;

                CopyAll(profile, profile.Profile);

                options.Update(profile.Profile, profile.XClearance, profile.xmode);

            }
            this.Close();
        }

        public void CopyAll<T>(T source, T target)
        {
            var type = typeof(T);
            foreach (var sourceProperty in type.GetProperties())
            {
                var targetProperty = type.GetProperty(sourceProperty.Name);
                if(targetProperty.CanWrite)
                    targetProperty.SetValue(target, sourceProperty.GetValue(source, null), null);
            }
            //foreach (var sourceField in type.GetFields())
            //{
            //    var targetField = type.GetField(sourceField.Name);
            //    targetField.SetValue(target, sourceField.GetValue(source));
            //}
        }
    }

    public static class DialogConverters
    {
        public static LatheModeRadiusBoolConverter SideToInsideBoolConverter = new LatheModeRadiusBoolConverter();
        public static LatheModeDiameterBoolConverter SideToOutsideBoolConverter = new LatheModeDiameterBoolConverter();
        //public static SideToIsEnabledConverter SideToIsEnabledConverter = new SideToIsEnabledConverter();
    }

    public class LatheModeRadiusBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is LatheMode && (LatheMode)value == LatheMode.Radius;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? LatheMode.Radius : LatheMode.Diameter;
        }
    }
    public class LatheModeDiameterBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is LatheMode && (LatheMode)value == LatheMode.Diameter;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? LatheMode.Diameter : LatheMode.Radius;
        }
    }

    //public class SideToIsEnabledConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        bool result = value is Lathe.Thread.Side && (Lathe.Thread.Side)value == Lathe.Thread.Side.Both;

    //        return result;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        throw new NotImplementedException();
    //    }

    //}
}