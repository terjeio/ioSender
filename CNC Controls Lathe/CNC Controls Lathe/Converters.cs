/*
 * Converters.cs - part of CNC Controls Lathe library
 *
 * v0.29 / 2020-09-17 / Io Engineering (Terje Io)
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    public static class Converters
    {
        public static bool IsMetric = true;
        public static CNCMeasureToTextConverter CNCMeasureToTextConverter = new CNCMeasureToTextConverter();
        public static SideToInsideBoolConverter SideToInsideBoolConverter = new SideToInsideBoolConverter();
        public static SideToOutsideBoolConverter SideToOutsideBoolConverter = new SideToOutsideBoolConverter();
        public static SideToIsEnabledConverter SideToIsEnabledConverter = new SideToIsEnabledConverter();
        public static SideToStringConverter SideToStringConverter = new SideToStringConverter();
        public static ToolToRoundedBoolConverter ToolToRoundedBoolConverter = new ToolToRoundedBoolConverter();
        public static ToolToChamferedBoolConverter ToolToChamferedBoolConverter = new ToolToChamferedBoolConverter();
        public static ToolToLabelStringConverter ToolToLabelStringConverter = new ToolToLabelStringConverter();
        public static TaperTypeToBoolConverter TaperTypeToBoolConverter = new TaperTypeToBoolConverter();
    }

    public class CNCMeasureToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = string.Empty;
            bool isMetric = parameter is bool ? (bool)parameter : Converters.IsMetric;

            if (value is double && !double.IsNaN((double)value))
                result = Math.Round((double)value, isMetric ? 3 : 4).ToString((isMetric ? GrblConstants.FORMAT_METRIC : GrblConstants.FORMAT_IMPERIAL), CultureInfo.InvariantCulture);

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CNCMeasureConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double result = double.NaN;
            bool isMetric = parameter is bool ? (bool)parameter : Converters.IsMetric;
            double f = values.Length > 1 && values[1] is double ? (double)values[1] : (isMetric ? 1.0d : 25.4d);

            if (values[0] is double && !double.IsNaN((double)values[0]))
                result = Math.Round((double)values[0] / f, f == 1.0d ? 3 : 4);

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            object[] result = new object[2];

            result[0] = value;
            result[1] = DependencyProperty.UnsetValue;

            return result;
        }
    }

    public class SideToInsideBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Thread.Side && (Thread.Side)value == Thread.Side.Inside;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Thread.Side.Inside : Thread.Side.Outside;
        }
    }

    public class SideToOutsideBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is Thread.Side && (Thread.Side)value == Thread.Side.Outside;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Thread.Side.Outside : Thread.Side.Inside;
        }
    }

    public class SideToIsEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Thread.Side && (Thread.Side)value == Thread.Side.Both;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class SideToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Thread.Side ? ((Thread.Side)value == Thread.Side.Outside ? "Outside diameter:" : "Inside diameter:") : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ToolToRoundedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Thread.Toolshape && (Thread.Toolshape)value == Thread.Toolshape.Rounded;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Thread.Toolshape.Rounded : Thread.Toolshape.Chamfer;
        }
    }
    public class ToolToChamferedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Thread.Toolshape && (Thread.Toolshape)value == Thread.Toolshape.Chamfer;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Thread.Toolshape.Chamfer : Thread.Toolshape.Rounded;
        }
    }
    public class ToolToLabelStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = string.Empty;

            if (value is Thread.Toolshape)
                result = (Thread.Toolshape)value == Thread.Toolshape.Rounded ? "Radius r:" : "Chamfer a:";

            return result;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TaperTypeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is ThreadTaper) && (ThreadTaper)value != ThreadTaper.None;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
