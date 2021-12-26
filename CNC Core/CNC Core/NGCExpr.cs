/*
 * NGCExpr.cs - part of CNC Controls library
 *
 * v0.36 / 2021-10-30 / Io Engineering (Terje Io)
 *
 */

// Some parts derived from:

/********************************************************************
* Description: interp_execute.cc
*
*   Derived from a work by Thomas Kramer
*
* Author:
* License: GPL Version 2
* System: Linux
*
* Copyright (c) 2004 All rights reserved.
*
* Last change:
********************************************************************/

// Some parts:

/*

Copyright(c) 2021, Io Engineering (Terje Io)
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
using System.Globalization;

namespace CNC.Core
{
    public class NGCExpr
    {
        private const int MAX_STACK = 7;
        private const double TOLERANCE_EQUAL = 0.001d;
        private const double DEGRAD = 57.29577951d;
        private const double RADDEG = 0.0174532925d;

        private struct NgcRoParam
        {
            public int Min, Max;
            public Func<int, double> Call;

            public NgcRoParam (int min, int max, Func<int, double> fn)
            {
                Min = min;
                Max = max;
                Call = fn;
            }
        }

        private enum BinaryOp {
            NoOp = 0,
            DividedBy,
            Modulo,
            Power,
            Times,
            Binary2 = Times,
            And2,
            ExclusiveOR,
            Minus,
            NotExclusiveOR,
            Plus,
            RightBracket,
            RelationalFirst,
            LT = RelationalFirst,
            EQ,
            NE,
            LE,
            GE,
            GT,
            RelationalLast = GT,
        };

        private enum UnaryOp {
            NoOp = 0,
            ABS,
            ACOS,
            ASIN,
            ATAN,
            COS,
            EXP,
            FIX,
            FUP,
            LN,
            Round,
            SIN,
            SQRT,
            TAN,
            Exists
        };

        public enum NamedParam
        {
            vmajor,
            vminor,
            line,
            motion_mode,
            plane,
            ccomp,
            metric,
            imperial,
            absolute,
            incremental,
            inverse_time,
            units_per_minute,
            units_per_rev,
            coord_system,
            tool_offset,
            retract_r_plane,
            retract_old_z,
            spindle_rpm_mode,
            spindle_css_mode,
            ijk_absolute_mode,
            lathe_diameter_mode,
            lathe_radius_mode,
            spindle_on,
            spindle_cw,
            mist,
            flood,
            speed_override,
            feed_override,
            adaptive_feed,
            feed_hold,
            feed,
            rpm,
            x,
            y,
            z,
            a,
            b,
            c,
            u,
            v,
            w,
            current_tool,
            current_pocket,
            selected_tool,
            selected_pocket,
            NGCParamLast
        };

        public enum OpStatus
        {
            OK = 0,
            ExpressionDivideByZero,
            ExpressionInvalidArgument,
            ExpressionUknownOp,
            ExpressionArgumentOutOfRange,
            BadNumberFormat,
            ExpressionSyntaxError,
            GcodeUnsupportedCommand,
            ExpressionInvalidResult,
            BadComment
        };

        private static List<NgcRoParam> ro_param = new List<NgcRoParam>();
        private Machine machine = null;
        private Dictionary<string, double> paramNamed = new Dictionary<string, double>();
        private Dictionary<int, double> paramNumbered = new Dictionary<int, double>();

        public NGCExpr (Machine machine)
        {
            this.machine = machine;

            ro_param.Add(new NgcRoParam(5061, 5069, probe_coord));          // LinuxCNC
            ro_param.Add(new NgcRoParam(5070, 5070, probe_result));         // LinuxCNC
            ro_param.Add(new NgcRoParam(5161, 5169, g28_home));
            ro_param.Add(new NgcRoParam(5181, 5189, g30_home));
            ro_param.Add(new NgcRoParam(5191, 5199, scaling_factors));      // Mach3
            ro_param.Add(new NgcRoParam(5210, 5210, g92_offset_applied));   // LinuxCNC
            ro_param.Add(new NgcRoParam(5211, 5219, g92_offset));
            ro_param.Add(new NgcRoParam(5220, 5220, coord_system));
            ro_param.Add(new NgcRoParam(5221, 5230, coord_system_offset));
            ro_param.Add(new NgcRoParam(5241, 5250, coord_system_offset));
            ro_param.Add(new NgcRoParam(5261, 5270, coord_system_offset));
            ro_param.Add(new NgcRoParam(5281, 5290, coord_system_offset));
            ro_param.Add(new NgcRoParam(5301, 5310, coord_system_offset));
            ro_param.Add(new NgcRoParam(5321, 5230, coord_system_offset));
            ro_param.Add(new NgcRoParam(5341, 5350, coord_system_offset));
            ro_param.Add(new NgcRoParam(5361, 5370, coord_system_offset));
            ro_param.Add(new NgcRoParam(5381, 5390, coord_system_offset));
            ro_param.Add(new NgcRoParam(5399, 5399, m66_result));           // LinuxCNC
            ro_param.Add(new NgcRoParam(5400, 5400, tool_number));          // LinuxCNC
            ro_param.Add(new NgcRoParam(5401, 5409, tool_offset));          // LinuxCNC
            ro_param.Add(new NgcRoParam(5420, 5428, work_position));        // LinuxCNC
        }

        public bool WasExpression { get; private set; }
        public OpStatus LastError { get; private set; } = OpStatus.OK;

        #region PrivateMethods

        private bool read_double(string line, ref int pos, ref double value)
        {
            int start = pos, end = pos;

            if ("+-.".IndexOf(line[end]) >= 0)
                end++;

            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.'))
                end++;

            pos = end;

            return end > start && double.TryParse(line.Substring(start, end - start), NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }

        /*! \brief Executes the operations: /, MOD, ** (POW), *.

        \param lhs pointer to the left hand side operand and result.
        \param operation \ref BinaryOp enum value.
        \param rhs pointer to the right hand side operand.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus execute_binary1(ref double lhs, BinaryOp operation, ref double rhs)
        {
            OpStatus status = OpStatus.OK;

            switch (operation)
            {
                case BinaryOp.DividedBy:
                    if (rhs == 0d || rhs == -0d)
                        status = OpStatus.ExpressionDivideByZero; // Attempt to divide by zero
                    else
                        lhs = lhs / rhs;
                    break;

                case BinaryOp.Modulo: // always calculates a positive answer
                    lhs = (lhs % rhs);
                    if (lhs < 0d)
                        lhs = lhs + Math.Abs(rhs);
                    break;

                case BinaryOp.Power:
                    if (lhs < 0d && Math.Floor(rhs) != rhs)
                        status = OpStatus.ExpressionInvalidArgument; // Attempt to raise negative value to non-integer power
                    else
                        lhs = Math.Pow(lhs, rhs);
                    break;

                case BinaryOp.Times:
                    lhs = lhs * rhs;
                    break;

                default:
                    status = OpStatus.ExpressionUknownOp;
                    break;
            }

            return status;
        }

        /*! \brief Executes the operations: +, -, AND, OR, XOR, EQ, NE, LT, LE, GT, GE
        The RS274/NGC manual does not say what
        the calculated value of the logical operations should be. This
        function calculates either 1.0 (meaning true) or 0.0 (meaning false).
        Any non-zero input value is taken as meaning true, and only 0.0 means false.

        \param lhs pointer to the left hand side operand and result.
        \param operation \ref BinaryOp enum value.
        \param rhs pointer to the right hand side operand.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus execute_binary2(ref double lhs, BinaryOp operation, ref double rhs)
        {
            switch (operation)
            {

                case BinaryOp.And2:
                    lhs = ((lhs == 0d) || (rhs == 0d)) ? 0d : 1d;
                    break;

                case BinaryOp.ExclusiveOR:
                    lhs = (((lhs == 0d) && (rhs != 0d)) || ((lhs != 0d) && (rhs == 0d))) ? 1d : 0d;
                    break;

                case BinaryOp.Minus:
                    lhs = (lhs - rhs);
                    break;

                case BinaryOp.NotExclusiveOR:
                    lhs = ((lhs != 0d) || (rhs != 0d)) ? 1d : 0d;
                    break;

                case BinaryOp.Plus:
                    lhs = (lhs + rhs);
                    break;

                case BinaryOp.LT:
                    lhs = (lhs < rhs) ? 1d : 0d;
                    break;

                case BinaryOp.EQ:
                    {
                        double diff = lhs - rhs;
                        diff = (diff < 0d) ? -diff : diff;
                        lhs = (diff < TOLERANCE_EQUAL) ? 1d : 0d;
                    }
                    break;

                case BinaryOp.NE:
                    {
                        double diff = lhs - rhs;
                        diff = (diff < 0d) ? -diff : diff;
                        lhs = (diff >= TOLERANCE_EQUAL) ? 1d : 0d;
                    }
                    break;

                case BinaryOp.LE:
                    lhs = (lhs <= rhs) ? 1d : 0d;
                    break;

                case BinaryOp.GE:
                    lhs = (lhs >= rhs) ? 1d : 0d;
                    break;

                case BinaryOp.GT:
                    lhs = (lhs > rhs) ? 1d : 0d;
                    break;

                default:
                    return OpStatus.ExpressionUknownOp;
            }

            return OpStatus.OK;
        }

        /*! \brief Executes a binary operation.

        This just calls either execute_binary1 or execute_binary2.

        \param lhs pointer to the left hand side operand and result.
        \param operation \ref BinaryOp enum value.
        \param rhs pointer to the right hand side operand.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus execute_binary(ref double lhs, BinaryOp operation, ref double rhs)
        {
            if (operation <= BinaryOp.Binary2)
                return execute_binary1(ref lhs, operation, ref rhs);

            return execute_binary2(ref lhs, operation, ref rhs);
        }

        /*! \brief Executes an unary operation: ABS, ACOS, ASIN, COS, EXP, FIX, FUP, LN, ROUND, SIN, SQRT, TAN

        All angle measures in the input or output are in degrees.

        \param operand pointer to the operand.
        \param operation \ref BinaryOp enum value.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus execute_unary(ref double operand, UnaryOp operation)
        {
            OpStatus status = OpStatus.OK;

            switch (operation)
            {

                case UnaryOp.ABS:
                    if (operand < 0d)
                        operand = (-1d * operand);
                    break;

                case UnaryOp.ACOS:
                    if (operand < -1d || operand > 1d)
                        status = OpStatus.ExpressionArgumentOutOfRange; // Argument to ACOS out of range
                    else
                        operand = Math.Acos(operand) * DEGRAD;
                    break;

                case UnaryOp.ASIN:
                    if (operand < -1d || operand > 1d)
                        status = OpStatus.ExpressionArgumentOutOfRange; // Argument to ASIN out of range
                    else
                        operand = Math.Asin(operand) * DEGRAD;
                    break;

                case UnaryOp.COS:
                    operand = Math.Cos(operand * RADDEG);
                    break;

                case UnaryOp.Exists:
                    // do nothing here, result for the EXISTS function is set by read_unary()
                    break;

                case UnaryOp.EXP:
                    operand = Math.Exp(operand);
                    break;

                case UnaryOp.FIX:
                    operand = Math.Floor(operand);
                    break;

                case UnaryOp.FUP:
                    operand = Math.Ceiling(operand);
                    break;

                case UnaryOp.LN:
                    if (operand <= 0d)
                        status = OpStatus.ExpressionArgumentOutOfRange; // Argument to LN out of range
                    else
                        operand = Math.Log(operand);
                    break;

                case UnaryOp.Round:
                    operand = (double)((int)(operand + ((operand < 0d) ? -0.5f : 0.5f)));
                    break;

                case UnaryOp.SIN:
                    operand = Math.Sin(operand * RADDEG);
                    break;

                case UnaryOp.SQRT:
                    if (operand < 0d)
                        status = OpStatus.ExpressionArgumentOutOfRange; // Negative argument to SQRT
                    else
                        operand = Math.Sqrt(operand);
                    break;

                case UnaryOp.TAN:
                    operand = Math.Tan(operand * RADDEG);
                    break;

                default:
                    status = OpStatus.ExpressionUknownOp;
                    break;
            }

            return status;
        }

        /*! \brief Returns an integer representing the precedence level of an operator.

        \param operator \ref BinaryOp enum value.
        \returns precedence level.
        */
        private int precedence(BinaryOp op)
        {
            switch (op)
            {
                case BinaryOp.RightBracket:
                    return 1;

                case BinaryOp.And2:
                case BinaryOp.ExclusiveOR:
                case BinaryOp.NotExclusiveOR:
                    return 2;

                case BinaryOp.LT:
                case BinaryOp.EQ:
                case BinaryOp.NE:
                case BinaryOp.LE:
                case BinaryOp.GE:
                case BinaryOp.GT:
                    return 3;

                case BinaryOp.Minus:
                case BinaryOp.Plus:
                    return 4;

                case BinaryOp.NoOp:
                case BinaryOp.DividedBy:
                case BinaryOp.Modulo:
                case BinaryOp.Times:
                    return 5;

                case BinaryOp.Power:
                    return 6;

                default:
                    break;
            }

            return 0;   // should never happen
        }

        /*! \brief Reads a binary operation out of the line
        starting at the index given by the pos offset. If a valid one is found, the
        value of operation is set to the symbolic value for that operation.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param operation pointer to \ref BinaryOp enum value.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_operation(string line, ref int pos, ref BinaryOp operation)
        {
            char c = line[pos];
            OpStatus status = OpStatus.OK;

            pos++;

            switch (c)
            {
                case '+':
                    operation = BinaryOp.Plus;
                    break;

                case '-':
                    operation = BinaryOp.Minus;
                    break;

                case '/':
                    operation = BinaryOp.DividedBy;
                    break;

                case '*':
                    if (line[pos] == '*')
                    {
                        operation = BinaryOp.Power;
                        pos++;
                    }
                    else
                        operation = BinaryOp.Times;
                    break;

                case ']':
                    operation = BinaryOp.RightBracket;
                    break;

                case 'A':
                    if (line.Substring(pos).StartsWith("ND"))
                    {
                        operation = BinaryOp.And2;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with A
                    break;

                case 'M':
                    if (line.Substring(pos).StartsWith("OD"))
                    {
                        operation = BinaryOp.Modulo;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with M
                    break;

                case 'R':
                    if (line[pos] == 'R')
                    {
                        operation = BinaryOp.NotExclusiveOR;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with R
                    break;

                case 'X':
                    if (line.Substring(pos).StartsWith("OR"))
                    {
                        operation = BinaryOp.ExclusiveOR;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with X
                    break;

                /* relational operators */
                case 'E':
                    if (line[pos] == 'Q')
                    {
                        operation = BinaryOp.EQ;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with E
                    break;

                case 'N':
                    if (line[pos] == 'E')
                    {
                        operation = BinaryOp.NE;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with N
                    break;

                case 'G':
                    if (line[pos] == 'E')
                    {
                        operation = BinaryOp.GE;
                        pos++;
                    }
                    else if (line[pos] == 'T')
                    {
                        operation = BinaryOp.GT;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; // Unknown operation name starting with G
                    break;

                case 'L':
                    if (line[pos] == 'E')
                    {
                        operation = BinaryOp.LE;
                        pos++;
                    }
                    else if (line[pos] == 'T')
                    {
                        operation = BinaryOp.LT;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp; ; // Unknown operation name starting with L
                    break;

                //        case '\0':
                //            status = OpStatus.ExpressionUknownOp; // No operation name found

                default:
                    status = OpStatus.ExpressionUknownOp; // Unknown operation name
                    break;
            }

            return status;
        }

        /*! \brief Reads the name of an unary operation out of the line
        starting at the index given by the pos offset. If a valid one is found, the
        value of operation is set to the symbolic value for that operation.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param operation pointer to \ref UnaryOp enum value.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_operation_unary(string line, ref int pos, ref UnaryOp operation)
        {
            char c = line[pos];
            OpStatus status = OpStatus.OK;

            pos++;

            switch (c)
            {
                case 'A':
                    if (line.Substring(pos).StartsWith("BS"))
                    {
                        operation = UnaryOp.ABS;
                        pos += 2;
                    }
                    else if (line.Substring(pos).StartsWith("COS"))
                    {
                        operation = UnaryOp.ACOS;
                        pos += 3;
                    }
                    else if (line.Substring(pos).StartsWith("SIN"))
                    {
                        operation = UnaryOp.ASIN;
                        pos += 3;
                    }
                    else if (line.Substring(pos).StartsWith("TAN"))
                    {
                        operation = UnaryOp.ATAN;
                        pos += 3;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'C':
                    if (line.Substring(pos).StartsWith("OS"))
                    {
                        operation = UnaryOp.COS;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'E':
                    if (line.Substring(pos).StartsWith("XP"))
                    {
                        operation = UnaryOp.EXP;
                        pos += 2;
                    }
                    else if (line.Substring(pos).StartsWith("XISTS"))
                    {
                        operation = UnaryOp.Exists;
                        pos += 5;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'F':
                    if (line.Substring(pos).StartsWith("IX"))
                    {
                        operation = UnaryOp.FIX;
                        pos += 2;
                    }
                    else if (line.Substring(pos).StartsWith("UP"))
                    {
                        operation = UnaryOp.FUP;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'L':
                    if (line[pos] == 'N')
                    {
                        operation = UnaryOp.LN;
                        pos++;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'R':
                    if (line.Substring(pos).StartsWith("OUND"))
                    {
                        operation = UnaryOp.Round;
                        pos += 4;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'S':
                    if (line.Substring(pos).StartsWith("IN"))
                    {
                        operation = UnaryOp.SIN;
                        pos += 2;
                    }
                    else if (line.Substring(pos).StartsWith("QRT"))
                    {
                        operation = UnaryOp.SQRT;
                        pos += 3;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                case 'T':
                    if (line.Substring(pos).StartsWith("AN"))
                    {
                        operation = UnaryOp.TAN;
                        pos += 2;
                    }
                    else
                        status = OpStatus.ExpressionUknownOp;
                    break;

                default:
                    status = OpStatus.ExpressionUknownOp;
                    break;
            }

            return status;
        }

        /*! \brief Reads the value out of a parameter of the line, starting at the
        index given by the pos offset.

        According to the RS274/NGC manual [NCMS, p. 62], the characters following
        # may be any "parameter expression". Thus, the following are legal
        and mean the same thing (the value of the parameter whose number is
        stored in parameter 2):
          ##2
          #[#2]

        Parameter setting is done in parallel, not sequentially. For example
        if #1 is 5 before the line "#1=10 #2=#1" is read, then after the line
        is is executed, #1 is 10 and #2 is 5. If parameter setting were done
        sequentially, the value of #2 would be 10 after the line was executed.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param value pointer to double where result is to be stored.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_parameter(string line, ref int pos, ref double value, bool check)
        {
            OpStatus status = OpStatus.BadNumberFormat;

            if (line[pos] == '#')
            {
                pos++;

                if (line[pos] == '<')
                {
                    int end = ++pos;

                    while (end < line.Length && line[end] != '>')
                        end++;

                    if (line[end] == '>')
                    {
                        if(GetNamedParameter(line.Substring(pos, end - pos), out value))
                            status = OpStatus.OK;
                        pos = end + 1;
                    }
                }
                else if (read_double(line, ref pos, ref value))
                {
                    if (GetNumberedParameter((int)value, out value))
                        status = OpStatus.OK;
                }
            }

            return status;
        }

        /*! \brief Reads a slash and the second argument to the ATAN function,
        starting at the index given by the pos offset. Then it computes the value
        of the ATAN operation applied to the two arguments.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param value pointer to double where result is to be stored.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_atan(string line, ref int pos, ref double value)
        {
            double argument2 = 0d;

            if (line[pos] != '/')
                return OpStatus.ExpressionSyntaxError; // Slash missing after first ATAN argument

            pos++;

            if (line[pos] != '[')
                return OpStatus.ExpressionSyntaxError; // Left bracket missing after slash with ATAN;

            OpStatus status;

            if ((status = eval(line, ref pos, ref argument2)) == OpStatus.OK)
                value = Math.Atan2(value, argument2) * DEGRAD;  /* value in radians, convert to degrees */

            return status;
        }

        /*! \brief Reads the value out of an unary operation of the line, starting at the
        index given by the pos offset. The ATAN operation is
        handled specially because it is followed by two arguments.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param value pointer to double where result is to be stored.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_unary(string line, ref int pos, ref double value)
        {
            OpStatus status;
            UnaryOp operation = UnaryOp.NoOp;

            if ((status = read_operation_unary(line, ref pos, ref operation)) == OpStatus.OK)
            {
                if (line[pos] != '[')
                    status = OpStatus.ExpressionSyntaxError; // Left bracket missing after unary operation name

                else
                {
                    if (operation == UnaryOp.Exists)
                    {
                        int len = line.Substring(++pos).IndexOf(']');

                        if (len > 0 && line[pos] == '#' && line[pos + 1] == '<')
                        {
                            value = NamedParameterExists(line.Substring(pos + 2, len - 3)) ? 1d : 0d;
                            pos += len + 1;
                        }
                        else
                            status = OpStatus.ExpressionSyntaxError;
                    }
                    else if ((status = eval(line, ref pos, ref value)) == OpStatus.OK)
                    {
                        if (operation == UnaryOp.ATAN)
                            status = read_atan(line, ref pos, ref value);
                        else
                            status = execute_unary(ref value, operation);
                    }
                }
            }

            return status;
        }

        /*! \brief Reads a real value out of the line, starting at the
        index given by the pos offset. The value may be a number, a parameter
        value, a unary function, or an expression. It calls one of four
        other readers, depending upon the first character.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param value pointer to double where result is to be stored.
        \returns #OK enum value if processed without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus read_real_value(string line, ref int pos, ref double value)
        {
            char c = line[pos], c1;

            if (c == '\0')
                return OpStatus.ExpressionSyntaxError; // No characters found when reading real value

            OpStatus status;

            c1 = pos == line.Length ? '\0' : line[pos + 1];

            if (c == '[')
                status = eval(line, ref pos, ref value);
            else if (c == '#')
                status = read_parameter(line, ref pos, ref value, false);
            else if (c == '+' && c1 != '\0' && char.IsDigit(c1) && c1 != '.')
            {
                pos++;
                status = read_real_value(line, ref pos, ref value);
            }
            else if (c == '-' && c1 != '\0' && !char.IsDigit(c1) && c1 != '.')
            {
                pos++;
                status = read_real_value(line, ref pos, ref value);
                value = - value;
            }
            else if ((c >= 'A') && (c <= 'Z'))
                status = read_unary(line, ref pos, ref value);
            else
                status = (read_double(line, ref pos, ref value) ? OpStatus.OK : OpStatus.BadNumberFormat);

            if (double.IsNaN(value))
                status = OpStatus.ExpressionInvalidResult; // Calculation resulted in 'not a number'
            else if (double.IsInfinity(value))
                status = OpStatus.ExpressionInvalidResult; // Calculation resulted in 'not a number'

            return status;
        }

        /*! \brief Evaluate expression and set result if successful.

        \param line pointer to RS274/NGC code (block).
        \param pos offset into line where expression starts.
        \param value pointer to double where result is to be stored.
        \returns #OK enum value if evaluated without error, appropriate \ref OpStatus enum value if not.
        */
        private OpStatus eval(string line, ref int pos, ref double value)
        {
            double[] values = new double[MAX_STACK];
            BinaryOp[] operators = new BinaryOp[MAX_STACK];
            int stack_index = 1;

            if (!(WasExpression = line[pos] == '['))
                return read_double(line, ref pos, ref value) ? OpStatus.OK : OpStatus.BadNumberFormat;

            pos++;

            OpStatus status;

            if ((status = read_real_value(line, ref pos, ref values[0])) != OpStatus.OK)
                return status;

            if ((status = read_operation(line, ref pos, ref operators[0])) != OpStatus.OK)
                return status;

            for (; operators[0] != BinaryOp.RightBracket;)
            {

                if ((status = read_real_value(line, ref pos, ref values[stack_index])) != OpStatus.OK)
                    return status;

                if ((status = read_operation(line, ref pos, ref operators[stack_index])) != OpStatus.OK)
                    return status;

                if (precedence(operators[stack_index]) > precedence(operators[stack_index - 1]))
                    stack_index++;
                else
                { // precedence of latest operator is <= previous precedence
                    for (; precedence(operators[stack_index]) <= precedence(operators[stack_index - 1]);)
                    {
                        if ((status = execute_binary(ref values[stack_index - 1], operators[stack_index - 1], ref values[stack_index])) != OpStatus.OK)
                            return status;

                        operators[stack_index - 1] = operators[stack_index];
                        if ((stack_index > 1) && precedence(operators[stack_index - 1]) <= precedence(operators[stack_index - 2]))
                            stack_index--;
                        else
                            break;
                    }
                }
            }

            value = values[0];

            return OpStatus.OK;
        }

        #endregion

        #region PublicInterface

        public OpStatus Eval(string line, ref int pos, out double value)
        {
            value = 0d;

            return (LastError = eval(line, ref pos, ref value));
        }

        public OpStatus ReadParameter(string line, ref int pos, out double value)
        {
            char c = line[pos];

            value = 0d;
            WasExpression = true;
            LastError = OpStatus.OK;

            if (c == '#')
            {
                pos++;

                if (line[pos] == '<')
                {
                    int end = ++pos;

                    while (end < line.Length && line[end] != '>')
                        end++;

                    if (line[end] == '>')
                    {
                        if (!GetNamedParameter(line.Substring(pos, end - pos), out value))
                            LastError = OpStatus.BadNumberFormat;
                        pos = end + 1;
                    }
                    else
                        LastError = OpStatus.BadNumberFormat;
                }
                else if (read_double(line, ref pos, ref value))
                {
                    if (!GetNumberedParameter((int)value, out value))
                        LastError = OpStatus.BadNumberFormat;
                }
                else
                    LastError = OpStatus.BadNumberFormat;

            }
            else if (c == '[')
                LastError = eval(line, ref pos, ref value);
            else
            {
                WasExpression = false;

                if (!read_double(line, ref pos, ref value))
                    value = double.NaN;
            }

            return LastError;
        }

        public OpStatus ReadComment(string line, ref int pos, out string comment)
        {
            comment = string.Empty;

            int end = pos + 1;

            while (end < line.Length && line[end] != ')')
                end++;

            comment = line.Substring(pos, end - pos + 1);

            /* && code.Length > 5 && code.Substring(0, 5).ToUpperInvariant() == "(MSG,"*/

            pos = end + 1;

            return (LastError = line[end] == ')' ? OpStatus.OK : OpStatus.BadComment);
        }

        public OpStatus ReadSetParameter(string line, ref int pos)
        {
            double value;
            OpStatus status = OpStatus.BadNumberFormat;

            if (line[++pos] == '<')
            {
                int end = ++pos;

                while (end < line.Length && line[end] != '>')
                    end++;

                if(line[end] == '>' && line[++end] == '=')
                {
                    string param = line.Substring(pos, end - pos - 1);
                    pos = ++end;
                    ReadParameter(line, ref pos, out value);
                    SetNamedParameter(param, value);
                }
            }
            else
            {
                double param;
                if ((status = ReadParameter(line, ref pos, out param)) == OpStatus.OK)
                {
                    if (line[pos] == '=')
                    {
                        pos++;
                        ReadParameter(line, ref pos, out value);
                        SetNumberedParameter((int)param, value);
                    }
                }
            }

            return status;
        }

        #endregion

        #region NumberedParameters
        public double GetNumberedParameter(int id)
        {
            double val = 0d;
            if (id >= 5060)
            {
                foreach(var param in ro_param)
                {
                    if(id >= param.Min && id <= param.Max)
                    {
                        val = param.Call(id);
                        break;
                    }
                }
            }
            else
                paramNumbered.TryGetValue(id, out val);

            return val;
        }

        public bool GetNumberedParameter(int id, out double value)
        {
            value = GetNumberedParameter(id);

            return !double.IsNaN(value);
        }

        public void SetNumberedParameter(int id, double val)
        {
            if (id <= 5060) {
                if (paramNumbered.ContainsKey(id))
                    paramNumbered.Remove(id);

                paramNumbered.Add(id, val);
            }
        }

        // Numbered parameters accessors

        private double probe_coord(int id)
        {
            id = id % 10 - 1;

            GrblWorkParameters.Get();

            return id < GrblInfo.NumAxes ? GrblWorkParameters.ProbePosition.Values[id] : 0d;
        }
        private double probe_result(int id)
        {
            GrblWorkParameters.Get();

            return GrblWorkParameters.ProbeSuccesful ? 1d : 0d;
        }
        private double g28_home(int id)
        {
            return machine.GetG28Position(id % 10 - 1);
        }
        private double g30_home(int id)
        {
            return machine.GetG30Position(id % 10 - 1);
        }
        private double g92_offset_applied(int id)
        {
            return machine.G92Active ? 1d : 0d;
        }
        private double scaling_factors(int id)
        {
            return machine.GetScaleFactor(id % 10 - 1);
        }
        private double g92_offset(int id)
        {
            return machine.GetG92Offset(id % 10 - 1);
        }
        private double coord_system(int id)
        {
            return machine.CoordSystem;
        }
        private double coord_system_offset(int id)
        {
            double value = 0.0f;
            int axis = id % 10;

            id = (id - 5220 - axis - (id == 0 ? 10 : 0)) / 20;

            if (axis > 0 && axis <= GrblInfo.NumAxes)
            {
                CoordinateSystem data = machine.GetCoordSystem(id);
                value = data.Values[axis - 1];
            }

            return value;
        }
        private double m66_result(int id)
        {
            return -1d; // For now
        }
        private double tool_number(int id)
        {
            return machine.Tool;
        }
        private double tool_offset(int id)
        {
            return machine.GetToolOffset(id % 10 - 1);
        }
        private double work_position(int id)
        {
            return machine.GetPosition(id % 10 - 1);
        }
        #endregion

        #region NamedParameters

        public double GetNamedParameter(NamedParam param)
        {
            double value = 0d;

            switch (param)
            {
                case NamedParam.vmajor:
                    value = 1d;
                    break;

                case NamedParam.vminor:
                    value = 1d;
                    break;

                case NamedParam.line:
                    value = machine.Line;
                    break;

                case NamedParam.motion_mode:
                    value = (double)machine.MotionMode;
                    break;

                case NamedParam.plane:
                    value = 170d + (double)machine.Plane.Plane * 10d;
                    break;

                case NamedParam.ccomp:
                    value = 400d;
                    break;

                case NamedParam.metric:
                    value = machine.IsImperial ? 0d : 1d;
                    break;

                case NamedParam.imperial:
                    value = machine.IsImperial ? 1d : 0d;
                    break;

                case NamedParam.absolute:
                    value = machine.DistanceMode == GCode.DistanceMode.Absolute ? 1d : 0d;
                    break;

                case NamedParam.incremental:
                    value = machine.DistanceMode == GCode.DistanceMode.Incremental ? 1d : 0d;
                    break;

                case NamedParam.inverse_time:
                    value = machine.FeedRateMode == GCode.FeedRateMode.InverseTime ? 1d : 0d;
                    break;

                case NamedParam.units_per_minute:
                    value = machine.FeedRateMode == GCode.FeedRateMode.UnitsPerMin ? 1d : 0d;
                    break;

                case NamedParam.units_per_rev:
                    value = machine.FeedRateMode == GCode.FeedRateMode.UnitsPerRev ? 1d : 0d;
                    break;

                case NamedParam.coord_system:
                    value = 540d + (machine.CoordSystem == 0 ? 0 : (machine.CoordSystem <= 6 ? (machine.CoordSystem - 1) * 10 : 44 + machine.CoordSystem));
                    break;

                case NamedParam.tool_offset:
                    value = machine.ToolLengthOffset == GCode.ToolLengthOffset.Cancel ? 0d : 1d;
                    break;

                case NamedParam.retract_r_plane:
                    value = machine.RetractOldZ ? 0d : 1d;
                    break;

                case NamedParam.retract_old_z:
                    value = machine.RetractOldZ ? 1d : 0d;
                    break;

                case NamedParam.spindle_rpm_mode:
                    value = machine.SpindleRpmMode ? 1d : 0d;
                    break;

                case NamedParam.spindle_css_mode:
                    value = machine.SpindleRpmMode ? 0d : 1d;
                    break;

                case NamedParam.ijk_absolute_mode:
                    value = machine.IJKMode == GCode.IJKMode.Absolute ? 1d : 0d;
                    break;

                case NamedParam.lathe_diameter_mode:
                    value = machine.LatheMode != GCode.LatheMode.Radius ? 1d : 0d;
                    break;

                case NamedParam.lathe_radius_mode:
                    value = machine.LatheMode == GCode.LatheMode.Radius ? 1d : 0d;
                    break;

                case NamedParam.spindle_on:
                    value = machine.SpindleState != GCode.SpindleState.Off ? 1d : 0d;
                    break;

                case NamedParam.spindle_cw:
                    value = machine.SpindleState == GCode.SpindleState.CW ? 1d : 0d;
                    break;

                case NamedParam.mist:
                    value = machine.CoolantState.HasFlag(GCode.CoolantState.Mist) ? 1d : 0d;
                    break;

                case NamedParam.flood:
                    value = machine.CoolantState.HasFlag(GCode.CoolantState.Flood) ? 1d : 0d;
                    break;

                case NamedParam.speed_override:
                    value = 0d; // TODO
                    break;

                case NamedParam.feed_override:
                    value = 0d; // TODO
                    break;

                case NamedParam.adaptive_feed:
                    value = 0d;
                    break;

                case NamedParam.feed_hold:
                    value = 0d; // TODO
                    break;

                case NamedParam.feed:
                    value = machine.Feedrate;
                    break;

                case NamedParam.rpm:
                    value = machine.SpindleRPM;
                    break;

                case NamedParam.x:
                case NamedParam.y:
                case NamedParam.z:
                case NamedParam.a:
                case NamedParam.b:
                case NamedParam.c:
                    value = machine.GetPosition(param - NamedParam.x);
                    break;

                case NamedParam.u:
                case NamedParam.v:
                case NamedParam.w:
                    value = 0d;
                    break;

                case NamedParam.current_tool:
                    value = machine.Tool;
                    break;

                case NamedParam.selected_tool:
                    value = machine.SelectedTool == null ? -1d : double.Parse(machine.SelectedTool.Code);
                    break;

                case NamedParam.current_pocket:
                    value = 0d;
                    break;

                case NamedParam.selected_pocket:
                    value = -1d;
                    break;

                default:
                    value = double.NaN;
                    break;
            }

            return value;
        }

        public double GetNamedParameter(string name)
        {
            NamedParam param;
            double value = 0d;

            name = name.ToLower();
            if (name.StartsWith("_") && Enum.TryParse(name.Substring(1), out param))
                value = GetNamedParameter(param);
            else if (!paramNamed.TryGetValue(name, out value))
                value = double.NaN;

            return value;
        }

        public bool GetNamedParameter(string name, out double value)
        {
            value = GetNamedParameter(name);

            return !double.IsNaN(value);
        }

        public void SetNamedParameter(string name, double val)
        {
            name = name.ToLower();

            if (paramNamed.ContainsKey(name))
                paramNamed.Remove(name);

            paramNamed.Add(name, val);
        }

        public bool NamedParameterExists(string name)
        {
            return paramNamed.ContainsKey(name.ToLower());
        }
    }

    #endregion
}
