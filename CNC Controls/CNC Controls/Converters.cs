/*
 * Converters.cs - part of CNC Controls library for Grbl
 *
 * v0.01 / 2019-10-28 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019, Io Engineering (Terje Io)
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
using System.Windows.Data;
using System.Windows.Media;
using CNC.Core;

namespace CNC.Controls
{
    public static class Converters
    {
        public static LatheModeToStringConverter LatheModeToStringConverter = new LatheModeToStringConverter();
        public static InvertBooleanConverter InvertBooleanConverter = new InvertBooleanConverter();
        public static GrblStateToColorConverter GrblStateToColorConverter = new GrblStateToColorConverter();
        public static GrblStateToStringConverter GrblStateToStringConverter = new GrblStateToStringConverter();
        public static HomedStateToColorConverter HomedStateToColorConverter = new HomedStateToColorConverter();
        public static IsHomingEnabledConverter IsHomingEnabledConverter = new IsHomingEnabledConverter();
    }

    public class LatheModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = String.Empty;

            if (value is LatheMode && (LatheMode)value != LatheMode.Disabled)
                result = (LatheMode)value == LatheMode.Radius ? "Radius" : "Diameter";

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GrblStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Brush result = Brushes.White;

            if (value is GrblState)
                result = new SolidColorBrush(((GrblState)value).Color);

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsHomingEnabledConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = GrblSettings.HomingEnabled && value.Length > 2 && value[1] is bool && !(bool)value[1] && value[2] is bool && !(bool)value[2];

            if (result && value[0] is GrblState)
                result = result && !((GrblState)value[0]).MPG && ((GrblState)value[0]).State == GrblStates.Idle;

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HomedStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Brush result = System.Windows.SystemColors.ControlBrush;

            if (value is HomedState) switch ((HomedState)value)
            {
                case HomedState.NotHomed:
                    result = Brushes.LightYellow;
                    break;

                case HomedState.Homed:
                    result = Brushes.LightGreen;
                    break;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GrblStateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            String result = string.Empty;

            if (value is GrblState && ((GrblState)value).State != GrblStates.Unknown) 
                result = ((GrblState)value).State.ToString().ToUpper() + (((GrblState)value).Substate == -1 ? "" : (":" + ((GrblState)value).Substate.ToString()));

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
