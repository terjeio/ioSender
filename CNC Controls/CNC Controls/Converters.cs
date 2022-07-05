/*
 * Converters.cs - part of CNC Controls library for Grbl
 *
 * v0.39 / 2022-06-24 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2021, Io Engineering (Terje Io)
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
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls
{
    public static class Converters
    {
        public static StringCollectionToTextConverter StringCollectionToTextConverter = new StringCollectionToTextConverter();
        public static LatheModeToStringConverter LatheModeToStringConverter = new LatheModeToStringConverter();
        public static GrblStateToColorConverter GrblStateToColorConverter = new GrblStateToColorConverter();
        public static EncoderModeToColorConverter EncoderModeToColorConverter = new EncoderModeToColorConverter();
        public static GrblStateToStringConverter GrblStateToStringConverter = new GrblStateToStringConverter();
        public static BlocksToStringConverter BlocksToStringConverter = new BlocksToStringConverter();
        public static GrblStateToBooleanConverter GrblStateToBooleanConverter = new GrblStateToBooleanConverter();
        public static GrblStateToIsJoggingConverter GrblStateToIsJoggingConverter = new GrblStateToIsJoggingConverter();
        public static HomedStateToColorConverter HomedStateToColorConverter = new HomedStateToColorConverter();
        public static IsHomingEnabledConverter IsHomingEnabledConverter = new IsHomingEnabledConverter();
        public static HomedStateToBooleanConverter HomedStateToBooleanConverter = new HomedStateToBooleanConverter();
        public static LogicalNotConverter LogicalNotConverter = new LogicalNotConverter();
        public static LogicalAndConverter LogicalAndConverter = new LogicalAndConverter();
        public static LogicalOrConverter LogicalOrConverter = new LogicalOrConverter();
        public static BoolToVisibleConverter BoolToVisibleConverter = new BoolToVisibleConverter();
        public static IsAxisVisibleConverter HasAxisConverter = new IsAxisVisibleConverter();
        public static IsSignalVisibleConverter IsSignalVisibleConverter = new IsSignalVisibleConverter();
        public static EnumValueToBooleanConverter EnumValueToBooleanConverter = new EnumValueToBooleanConverter();
        public static StringAddToConverter StringAddToConverter = new StringAddToConverter();
        public static MultiLineConverter MultiLineConverter = new MultiLineConverter();
        public static PositionToStringConverter PositionToStringConverter = new PositionToStringConverter();
        public static FeedSpeedToStringConverter FeedSpeedToStringConverter = new FeedSpeedToStringConverter();

        internal static string numBlocks = LibStrings.FindResource("NumBlocks");
        internal static string blockOfBlocks = LibStrings.FindResource("BlockOfBlocks");
        internal static Lazy<Dictionary<GrblStates, string>> grblState = new Lazy<Dictionary<GrblStates, string>>(() =>
            new Dictionary<GrblStates, string> {
                { GrblStates.Unknown, LibStrings.FindResource("StateUnknown") },
                { GrblStates.Idle, LibStrings.FindResource("StateIdle") },
                { GrblStates.Run, LibStrings.FindResource("StateRun") },
                { GrblStates.Tool, LibStrings.FindResource("StateTool") },
                { GrblStates.Hold, LibStrings.FindResource("StateHold") },
                { GrblStates.Home, LibStrings.FindResource("StateHome") },
                { GrblStates.Check, LibStrings.FindResource("StateCheck") },
                { GrblStates.Jog, LibStrings.FindResource("StateJog") },
                { GrblStates.Alarm, LibStrings.FindResource("StateAlarm") },
                { GrblStates.Door, LibStrings.FindResource("StateDoor") },
                { GrblStates.Sleep, LibStrings.FindResource("StateSleep") }
            });
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
                return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // --

    public class LatheModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = string.Empty;

            if (value is LatheMode && (LatheMode)value != LatheMode.Disabled)
                result = (LatheMode)value == LatheMode.Radius ? "Radius" : "Diameter";

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BlocksToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            return value[0] is int && value[1] is int ? (string.Format((int)value[1] == 0 ? Converters.numBlocks : Converters.blockOfBlocks, value[1], value[0])) : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PositionToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            string res = string.Empty;
            string format = value.Length > 1 && value[1] is string ? value[1] as string : "####0.000";

            if(value[0] is Position) switch(GrblInfo.NumAxes)
            {
                case 4:
                    res = string.Format(GrblInfo.PositionFormatString,
                                        (value[0] as Position).X.ToInvariantString(format),
                                         (value[0] as Position).Y.ToInvariantString(format),
                                          (value[0] as Position).Z.ToInvariantString(format),
                                           (value[0] as Position).A.ToInvariantString(format));
                    break;

                case 5:
                    res = string.Format(GrblInfo.PositionFormatString,
                                        (value[0] as Position).X.ToInvariantString(format),
                                         (value[0] as Position).Y.ToInvariantString(format),
                                          (value[0] as Position).Z.ToInvariantString(format),
                                           (value[0] as Position).A.ToInvariantString(format),
                                            (value[0] as Position).B.ToInvariantString(format));
                    break;

                case 6:
                    res = string.Format(GrblInfo.PositionFormatString,
                                        (value[0] as Position).X.ToInvariantString(format),
                                         (value[0] as Position).Y.ToInvariantString(format),
                                          (value[0] as Position).Z.ToInvariantString(format),
                                           (value[0] as Position).A.ToInvariantString(format),
                                            (value[0] as Position).B.ToInvariantString(format),
                                             (value[0] as Position).C.ToInvariantString(format));
                    break;

                default:
                    res = string.Format(GrblInfo.PositionFormatString,
                                        (value[0] as Position).X.ToInvariantString(format),
                                         (value[0] as Position).Y.ToInvariantString(format),
                                          (value[0] as Position).Z.ToInvariantString(format));
                    break;
            }

            return res;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FeedSpeedToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Length == 2 && value[0] is double && value[1] is double
                    ? string.Format("F: {0}  S: {1}", ((double)value[0]).ToInvariantString(), ((double)value[1]).ToInvariantString())
                    : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
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
            GrblStates state = value[0] is GrblState ? ((GrblState)value[0]).State : GrblStates.Unknown;

            // If ALARM:11 homing is required
            bool result = state == GrblStates.Alarm && ((GrblState)value[0]).Substate == 11;

            // value[1] = IsJobRunning
            // value[2] = IsSleeping

            if (!result && GrblInfo.HomingEnabled && value.Length > 2 && value[1] is bool && !(bool)value[1] && value[2] is bool && !(bool)value[2])
                result = state != GrblStates.Unknown && !((GrblState)value[0]).MPG && (state == GrblStates.Idle || state == GrblStates.Alarm || !GrblInfo.IsGrblHAL);

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

    public class HomedStateToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is HomedState && (HomedState)value == HomedState.Homed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EncoderModeToColorConverter : IMultiValueConverter
    {
        public static SolidColorBrush ReadOnlyBackGround { get; } = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFF8F8F8"));

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool result = true;

            foreach (var value in values)
                result &= value is bool && (bool)value;

            return values.Length == 2 && values[0] is GrblEncoderMode && !values[0].Equals(GrblEncoderMode.Unknown) && values[1] is GrblEncoderMode && values[0].Equals(values[1]) ? Brushes.Salmon : ReadOnlyBackGround;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GrblStateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = string.Empty;

            Converters.grblState.Value.TryGetValue(((GrblState)value).State, out result);
            int substate = ((GrblState)value).State == GrblStates.Alarm && ((GrblState)value).LastAlarm > 0 ? ((GrblState)value).LastAlarm : ((GrblState)value).Substate;

            if (value is GrblState && ((GrblState)value).State != GrblStates.Unknown) 
                result = (result == string.Empty ? ((GrblState)value).State.ToString().ToUpper() : result) + (substate == -1 ? "" : (":" + substate.ToString()));

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GrblStateToBooleanConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return values.Length == 2 && values[0] is GrblState && values[1] is GrblStates && ((GrblState)values[0]).State == (GrblStates)values[1];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GrblStateToIsJoggingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is GrblState && ((GrblState)value).State == GrblStates.Jog;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogicalNotConverter : IValueConverter
    {
        public IValueConverter FinalConverter { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = (value is bool ? !(bool)value : ((value is bool?) ? (bool?)value != true : ((value is int) ? (int)value == 0 : false))) || value == null;

            return FinalConverter == null ? result : FinalConverter.Convert(result, targetType, parameter, culture);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }

    public class LogicalAndConverter : IMultiValueConverter
    {
        public IValueConverter FinalConverter { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool result = true;

            foreach (var value in values)
                result &= value is bool && (bool)value;

            return FinalConverter == null ? result : FinalConverter.Convert(result, targetType, parameter, culture);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogicalOrConverter : IMultiValueConverter
    {
        public IValueConverter FinalConverter { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool result = false;

            foreach (var value in values)
                result |= value is bool && (bool)value;

            return FinalConverter == null ? result : FinalConverter.Convert(result, targetType, parameter, culture);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    public class IsAxisVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool enabled = false;

            if(values.Length == 2 && values[0] is int && values[1] is int && (int)values[0] >= (int)values[1])
                enabled = ((int)values[0] & (int)values[1]) != 0;

            if(values.Length == 2 && values[0] is AxisFlags && values[1] is AxisFlags)
                enabled = ((AxisFlags)values[0]).HasFlag((AxisFlags)values[1]);

            return enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsSignalVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool enabled = false;

            if (values.Length == 2 && values[0] is int && values[1] is int && (int)values[0] >= (int)values[1])
                enabled = ((int)values[0] & (int)values[1]) != 0;

            if (values.Length == 2 && values[0] is EnumFlags<Signals> && values[1] is Signals)
                enabled = ((EnumFlags<Signals>)values[0]).Value.HasFlag((Signals)values[1]);

            return enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringAddToConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return values.Length == 2 ? values[0].ToString() + string.Format((string)parameter, values[1].ToString()) : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumValueToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();
            return checkValue.Equals(targetValue,
                     StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            bool useValue = (bool)value;
            string targetValue = parameter.ToString();
            if (useValue)
                return Enum.Parse(targetType, targetValue);

            return null;
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
