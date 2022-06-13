/*
 * Widget.cs - part of CNC Controls library for Grbl
 *
 * v0.38 / 2022-03-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
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
using CNC.Core;
using System.Windows.Data;
using CNC.GCode;

namespace CNC.Controls
{

    public class WidgetViewModel : ViewModelBase
    {
        string _tv;
        double _nv;

        public double NumericValue
        {
            get { return _nv; }
            set { _nv = value; OnPropertyChanged(); }
        }

        public string TextValue
        {
            get { return _tv; }
            set { _tv = value; OnPropertyChanged(); }
        }
    }

    public class WidgetProperties
    {

        public int Id { get; private set; }
        public GrblSettingDetails.DataTypes DataType { get; private set; }
        public string Label { get; private set; }
        public string Format { get; private set; }
        public string Unit { get; private set; }
        public string Value { get; private set; }
        public double Min { get; private set; } = double.NaN;
        public double Max { get; private set; } = double.NaN;

        GrblSettingDetails properties = null;

        public void Assign (string value)
        {
            properties.Value = Value = value;
        }

        public WidgetProperties(GrblSettingDetails Properties)
        {
            properties = Properties;

            Id = Properties.Id;
            DataType = Properties.DataType == GrblSettingDetails.DataTypes.PASSWORD ? GrblSettingDetails.DataTypes.TEXT : Properties.DataType;
            Format = Properties.Format;
            Unit = Properties.Unit;
            Label = Properties.Name;
            Value = Properties.Value;
            Min = Properties.Min;
            Max = Properties.Max;
        }
    }

    public class Widget : IDisposable
    {

        private WidgetProperties widget;
        private const int PPU = 8;
        private bool isEnabled = false, disposed = false, Modified = false, has_unit = true;
        private string orgText;
        private double labelWidth = 180d;
        private StackPanel components;
        private WidgetViewModel model;

        public Label wLabel = null, wUnit = null;
        public TextBox wTextBox = null;
        public NumericTextBox wNumericTextBox = null;
        public CheckBox wCheckBox = null;
        public RadioButton wRadiobutton = null;
        private StackPanel Canvas;

        public string BaseValue { get { return widget.Value; } }

        public Widget(GrblConfigView View, WidgetProperties widget, StackPanel Canvas)
        {
            this.Canvas = components = Canvas;
            this.widget = widget;
            model = ((WidgetViewModel)Canvas.DataContext);

            model.NumericValue = double.NaN;

            Grid grid, labelGrid = null;

            switch (widget.DataType)
            {
                case GrblSettingDetails.DataTypes.BOOL:

                    wCheckBox = new CheckBox
                    {
                        Name = "_bool",
                        Content = widget.Label.Trim(),
                        IsEnabled = false
                    };
                    wCheckBox.Margin = new Thickness(0, 6, 0, 0);
                    grid = labelGrid = AddGrid(200);
                    grid.Children.Add(wCheckBox);
                    Grid.SetColumn(wCheckBox, 1);
                    wCheckBox.Checked += wWidget_TextChanged;
                    components.Children.Add(grid);
                    break;

                case GrblSettingDetails.DataTypes.BITFIELD:
                case GrblSettingDetails.DataTypes.XBITFIELD:
                case GrblSettingDetails.DataTypes.AXISMASK:
                    has_unit = false;
                    bool axes = widget.DataType == GrblSettingDetails.DataTypes.AXISMASK || widget.Format == "axes";
                    string[] format = (axes ? "X-Axis,Y-Axis,Z-Axis,A-Axis,B-Axis,C-Axis" : widget.Format).Split(',');
                    for (int i = 0; i < (axes ? 6 : format.Length); i++)
                    {
                        if (axes && !GrblInfo.AxisFlags.HasFlag(GCodeParser.AxisFlag[i]))
                            continue;
                        else if (format[i] == "N/A")
                            continue;

                        wCheckBox = new CheckBox
                        {
                            Name = string.Format("_bitmask{0}", i),
                            Content = format[i].Trim(),
                            //   TabIndex = Canvas.Row,
                            IsEnabled = false,
                            Tag = 1 << i
                        };
                        grid = AddGrid(300);
                        if (i == 0)
                        {
                            labelGrid = grid;
                            wCheckBox.Margin = new Thickness(0, 6, 0, 0);
                        }
                        else
                            grid.Height = 20;
                        grid.Children.Add(wCheckBox);
                        Grid.SetColumn(wCheckBox, 1);
                        wCheckBox.Click += wWidget_TextChanged;
                        components.Children.Add(grid);
                        wCheckBox = null;
                    }
                    break;

                case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                    has_unit = false;
                    string[] rformat = widget.Format.Split(',');
                    for (int i = 0; i < rformat.Length; i++)
                    {
                        wRadiobutton = new RadioButton
                        {
                            Name = string.Format("_radiobutton{0}", i),
                            Content = rformat[i].Trim(),
                            IsEnabled = false,
                            Tag = i
                        };
                        grid = AddGrid(300);
                        if (i == 0)
                        {
                            labelGrid = grid;
                            wRadiobutton.Margin = new Thickness(0, 6, 0, 0);
                        }
                        else
                            grid.Height = 20;
                        grid.Children.Add(wRadiobutton);
                        Grid.SetColumn(wRadiobutton, 1);
                        wRadiobutton.Checked += wWidget_TextChanged;
                        components.Children.Add(grid);
                        wRadiobutton = null;
                    }
                    break;

                case GrblSettingDetails.DataTypes.INTEGER:
                case GrblSettingDetails.DataTypes.FLOAT:
                    wNumericTextBox = new NumericTextBox
                    {
                        Format = widget.Format,
                        Height = 22
                    };
                    labelWidth = 210;
                    grid = labelGrid = AddGrid(wNumericTextBox.Width + 4);
                    grid.Children.Add(wNumericTextBox);
                    Grid.SetColumn(wNumericTextBox, 1);
                    components.Children.Add(grid);
                    wNumericTextBox.TextChanged += wWidget_TextChanged;
                    wNumericTextBox.KeyDown += wWidget_KeyDown;
                    Binding binding = new Binding("Text")
                    {
                        Source = Canvas.DataContext,
                        Path = new PropertyPath("NumericValue"),
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        ValidatesOnDataErrors = true
                    };
                    binding.ValidationRules.Add(new NumericRangeRule()
                    {
                        Min = widget.Min,
                        Max = widget.Max
                    });
                    wNumericTextBox.Style = View.Resources["NumericErrorStyle"] as Style;
                    BindingOperations.SetBinding(wNumericTextBox, NumericTextBox.ValueProperty, binding);
//                    model.NumericValue = dbl.Parse(widget.Value);
                    break;

                default:
                    widget.Format.Replace(" ", "");
                    wTextBox = new TextBox
                    {
                        Name = "tb_name_xxx",
                        MaxLength = widget.Format.Length,
                        VerticalContentAlignment = VerticalAlignment.Bottom,
                        Height = 24
                        //TabIndex = Canvas.Row
                    };
                    if (widget.DataType == GrblSettingDetails.DataTypes.TEXT && widget.Format.StartsWith("x("))
                    {
                        int length = 8;
                        int.TryParse(widget.Format.Substring(2).Replace(")", ""), out length);
                        wTextBox.MaxLength = length;
                        //  this.wTextBox.Size = new System.Drawing.Size(Math.Min(length * PPU, Canvas.Width - x - 15), 20);
                    }
                    else if (widget.DataType == GrblSettingDetails.DataTypes.IP4)
                    {
                        wTextBox.MaxLength = 16;
                        Binding sbinding = new Binding("Text")
                        {
                            Source = Canvas.DataContext,
                            Path = new PropertyPath("TextValue"),
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                            ValidatesOnDataErrors = true
                        };
                        sbinding.ValidationRules.Add(new IP4ValueRule());
                        wTextBox.Style = View.Resources["Ip4ErrorStyle"] as Style;
                        BindingOperations.SetBinding(wTextBox, TextBox.TextProperty, sbinding);
                    }
                    grid = labelGrid = AddGrid();
                    grid.Children.Add(wTextBox);
                    Grid.SetColumn(wTextBox, 1);
                    components.Children.Add(grid);
                    wTextBox.TextChanged += wWidget_TextChanged;
                    wTextBox.KeyDown += wWidget_KeyDown;
                    break;
            }

            if (widget.DataType != GrblSettingDetails.DataTypes.BOOL && labelGrid != null)
            {
                if (widget.Label != "")
                {
                    wLabel = new Label
                    {
                        Width = labelWidth,
                        Height = 26,
                        HorizontalContentAlignment = HorizontalAlignment.Right,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Name = "label_xx",
                        Content = widget.Label + ":"
                    };
                    labelGrid.Children.Add(wLabel);
                    Grid.SetColumn(wLabel, 0);
                }

                if (has_unit && widget.Unit != "")
                {
                    wUnit = new Label
                    {
                        Height = 26,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Name = "unit_xxx",
                        Content = widget.Unit
                    };
                    labelGrid.Children.Add(wUnit);
                    Grid.SetColumn(wUnit, 2);
                }
            }

            Text = widget.Value;
        }

        ~Widget()
        {
            //   components.Children.Clear();
            //   this.Dispose(false);
        }

        Grid AddGrid(double width)
        {
            Grid grid = new Grid
            {
                Width = Canvas.Width,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center
            };

            ColumnDefinition c = new ColumnDefinition();
            c.Width = new GridLength(labelWidth);
            grid.ColumnDefinitions.Add(c);
            c = new ColumnDefinition();
            c.Width = new GridLength(width);
            grid.ColumnDefinitions.Add(c);
            c = new ColumnDefinition();
            c.Width = new GridLength(Math.Max(1d, grid.Width - labelWidth - width));
            grid.ColumnDefinitions.Add(c);

            return grid;
        }
        Grid AddGrid()
        {
            return AddGrid(120d);
        }

        #region Attributes
        public bool IsEnabled
        {
            get { return isEnabled; }
            set
            {
                isEnabled = value;

                switch (widget.DataType)
                {
                    case GrblSettingDetails.DataTypes.BITFIELD:
                    case GrblSettingDetails.DataTypes.AXISMASK:
                        foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(this.Canvas))
                            checkBox.IsEnabled = isEnabled;
                        break;

                    case GrblSettingDetails.DataTypes.XBITFIELD:
                        CheckBox firstCheckBox = null;
                        foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(this.Canvas))
                        {
                            if ((int)checkBox.Tag == 1)
                            {
                                firstCheckBox = checkBox;
                                break;
                            }
                        }
                        foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(this.Canvas))
                            checkBox.IsEnabled = isEnabled && (checkBox == firstCheckBox || firstCheckBox.IsChecked == true);
                        break;

                    case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                        foreach (RadioButton radioButton in UIUtils.FindLogicalChildren<RadioButton>(this.Canvas))
                            radioButton.IsEnabled = isEnabled;
                        break;

                    case GrblSettingDetails.DataTypes.BOOL:
                        wCheckBox.IsEnabled = isEnabled;
                        break;

                    case GrblSettingDetails.DataTypes.INTEGER:
                    case GrblSettingDetails.DataTypes.FLOAT:
                        wNumericTextBox.IsEnabled = isEnabled;
                        break;

                    default:
                        wTextBox.IsEnabled = isEnabled;
                        break;
                }
            }
        }

        public string Text
        {
            get
            {
                string value = "";
                switch (widget.DataType)
                {
                    case GrblSettingDetails.DataTypes.BITFIELD:
                    case GrblSettingDetails.DataTypes.XBITFIELD:
                    case GrblSettingDetails.DataTypes.AXISMASK:
                        int val = 0;
                        foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(this.Canvas))
                        {
                            if (checkBox.IsEnabled && checkBox.IsChecked == true)
                                val |= (int)checkBox.Tag;
                        }
                        value = val.ToString();
                        break;

                    case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                        int rval = 0;
                        foreach (RadioButton radioButton in UIUtils.FindLogicalChildren<RadioButton>(this.Canvas))
                        {
                            if (radioButton.IsChecked == true)
                            {
                                rval = (int)radioButton.Tag;
                                break;
                            }
                        }
                        value = rval.ToString();
                        break;

                    case GrblSettingDetails.DataTypes.INTEGER:
                    case GrblSettingDetails.DataTypes.FLOAT:
                        value = model.NumericValue.ToInvariantString();
                        break;

                    case GrblSettingDetails.DataTypes.BOOL:
                        value = wCheckBox.IsChecked == true ? "1" : "0";
                        break;

                    default:
                        value = wTextBox.Text;
                        break;
                }
                return value.Trim();
            }
            set
            {
                Modified = false;
                switch (widget.DataType)
                {
                    case GrblSettingDetails.DataTypes.BITFIELD:
                    case GrblSettingDetails.DataTypes.XBITFIELD:
                    case GrblSettingDetails.DataTypes.AXISMASK:
                        int val = int.Parse(value);
                        foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(this.Canvas))
                            checkBox.IsChecked = (val & (int)checkBox.Tag) != 0;
                        break;

                    case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                        int rval = int.Parse(value);
                        foreach (RadioButton radioButton in UIUtils.FindLogicalChildren<RadioButton>(this.Canvas))
                            radioButton.IsChecked = rval == (int)radioButton.Tag;
                        break;

                    case GrblSettingDetails.DataTypes.BOOL:
                        wCheckBox.IsChecked = value == "1";
                        break;

                    case GrblSettingDetails.DataTypes.INTEGER:
                    case GrblSettingDetails.DataTypes.FLOAT:
                        model.NumericValue = dbl.Parse(value);
                        break;

                    default:
                        orgText = value;
                        wTextBox.Text = value;
                        break;
                }
            }
        }
        #endregion

        #region UIEvents

        private void wWidget_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                Assign();
        }

        /* Hack for bug in API */
        public void wWidget_TextChanged(object sender, System.EventArgs e)
        {
            //bool changed;

            switch (widget.DataType)
            {
                case GrblSettingDetails.DataTypes.XBITFIELD:
                    CheckBox firstCheckBox = null;
                    foreach (CheckBox checkBox in UIUtils.FindLogicalChildren<CheckBox>(Canvas))
                    {
                        if (firstCheckBox == null)
                            firstCheckBox = checkBox;
                        else
                            checkBox.IsEnabled = firstCheckBox.IsChecked == true;
                    }
                    break;

                case GrblSettingDetails.DataTypes.RADIOBUTTONS:
                    foreach (RadioButton radioButton in UIUtils.FindLogicalChildren<RadioButton>(Canvas))
                    {
                        if (radioButton != (RadioButton)sender)
                            radioButton.IsChecked = false;
                    }
                    break;
            }

            //changed = Modified ? widget.Value == Text : widget.Value != Text;
            //if (changed)
            //    Modified = !Modified;
            //orgText = Text;
        }

        #endregion

        private bool isValid()
        {
            bool ok = true;

            switch (widget.DataType)
            {
                case GrblSettingDetails.DataTypes.INTEGER:
                case GrblSettingDetails.DataTypes.FLOAT:
                    ok = !Validation.GetHasError(wNumericTextBox);
                    break;

                case GrblSettingDetails.DataTypes.IP4:
                    ok = !Validation.GetHasError(wTextBox);
                    break;
            }

            return ok;
        }

        public void Assign()
        {
            if (isValid() && Text != widget.Value)
            {
                Modified = false;
                widget.Assign(Text);
                model.TextValue = Text;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                //          this.components.Dispose();
                //  this.Canvas.Controls.Remove(this);
            }
            disposed = true;
        }
    }
}
