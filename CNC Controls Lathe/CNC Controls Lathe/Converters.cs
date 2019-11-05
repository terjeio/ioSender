/*
 * Converters.cs - part of CNC Controls library
 *
 * v0.01 / 2019-10-31 / Io Engineering (Terje Io)
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe
{
    public static class Converters
    {
        public static bool IsMetric = true;
        public static StringCollectionToTextConverter StringCollectionToTextConverter = new StringCollectionToTextConverter();
        public static CNCMeasureToTextConverter CNCMeasureToTextConverter = new CNCMeasureToTextConverter();
        public static SideToInsideBoolConverter SideToInsideBoolConverter = new SideToInsideBoolConverter();
        public static SideToOutsideBoolConverter SideToOutsideBoolConverter = new SideToOutsideBoolConverter();
        public static SideToIsEnabledConverter SideToIsEnabledConverter = new SideToIsEnabledConverter();
        public static ToolToRoundedBoolConverter ToolToRoundedBoolConverter = new ToolToRoundedBoolConverter();
        public static ToolToChamferedBoolConverter ToolToChamferedBoolConverter = new ToolToChamferedBoolConverter();
        public static ToolToLabelStringConverter ToolToLabelStringConverter = new ToolToLabelStringConverter();
        public static TaperTypeToBoolConverter TaperTypeToBoolConverter = new TaperTypeToBoolConverter();
        public static MultiLineConverter MultiLineConverter = new MultiLineConverter();
    }

    // Adapted from: https://stackoverflow.com/questions/4353186/binding-observablecollection-to-a-textbox/8847910#8847910

    public class StringCollectionToTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var data = values[0] as ObservableCollection<string>;

            if (data != null && data.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var s in data)
                {
                    sb.AppendLine(s.ToString());
                }
                return sb.ToString();
            }
            else
                return String.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CNCMeasureToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = String.Empty;
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

    public class SideToInsideBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is Lathe.Thread.Side && (Lathe.Thread.Side)value == Lathe.Thread.Side.Inside;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Lathe.Thread.Side.Inside : Lathe.Thread.Side.Outside;
        }
    }

    public class SideToOutsideBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is Lathe.Thread.Side && (Lathe.Thread.Side)value == Lathe.Thread.Side.Outside;

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value == true ? Lathe.Thread.Side.Outside : Lathe.Thread.Side.Inside;
        }
    }

    public class SideToIsEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value is Lathe.Thread.Side && (Lathe.Thread.Side)value == Lathe.Thread.Side.Both;

            return result;
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
            bool result = value is Thread.Toolshape && (Thread.Toolshape)value == Thread.Toolshape.Rounded;

            return result;
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
            bool result = value is Thread.Toolshape && (Thread.Toolshape)value == Thread.Toolshape.Chamfer;

            return result;
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
                result = (Thread.Toolshape)value == Thread.Toolshape.Rounded ? "Radius r:" : "Chamfer a";

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
            bool result = (value is ThreadTaper) && (ThreadTaper)value == ThreadTaper.None;

            return result;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // by  D4rth B4n3 - https://stackoverflow.com/questions/30627368/how-to-create-a-tooltip-to-display-multiple-validation-errors-for-a-single-contr
    public class MultiLineConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(values[0] is IEnumerable<ValidationError>))
                return null;

            //string.Join(",", (List<string>)logic.Model.GetErrors(e.PropertyName)));

            var val = values[0] as IEnumerable<ValidationError>;

            string retVal = "";

            foreach (var itm in val)
            {
                if (retVal.Length > 0)
                    retVal += "\n";
                retVal += itm.ErrorContent;

            }
            return retVal;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
