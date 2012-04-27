//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
//     TaskScheduler Original Code from http://taskscheduler.codeplex.com/
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// * Copyright (c) 2003-2011 David Hall
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR 
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.

namespace CoApp.Toolkit.TaskService {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Properties;

    /// <summary>
    ///   Functions to provide localized strings for enumerated types and values.
    /// </summary>
    public static class TaskEnumGlobalizer {
        /// <summary>
        ///   Gets a string representing the localized value of the provided enum.
        /// </summary>
        /// <param name="enumValue"> The enum value. </param>
        /// <returns> A localized string, if available. </returns>
        public static string GetString(object enumValue) {
            switch (enumValue.GetType().Name) {
                case "DaysOfTheWeek":
                    return GetCultureEquivalentString((DaysOfTheWeek)enumValue);
                case "MonthsOfTheYear":
                    return GetCultureEquivalentString((MonthsOfTheYear)enumValue);
                case "TaskTriggerType":
                    return BuildEnumString("TriggerType", enumValue);
                case "WhichWeek":
                    return BuildEnumString("WW", enumValue);
                case "TaskActionType":
                    return BuildEnumString("ActionType", enumValue);
                case "TaskState":
                    return BuildEnumString("TaskState", enumValue);
                default:
                    break;
            }
            return enumValue.ToString();
        }

        private static string GetCultureEquivalentString(DaysOfTheWeek val) {
            if (val == DaysOfTheWeek.AllDays) {
                return Resources.DOWAllDays;
            }

            var s = new List<string>(7);
            var vals = Enum.GetValues(val.GetType());
            for (var i = 0; i < vals.Length - 1; i++) {
                if ((val & (DaysOfTheWeek)vals.GetValue(i)) > 0) {
                    s.Add(DateTimeFormatInfo.CurrentInfo.GetDayName((DayOfWeek)i));
                }
            }

            return string.Join(Resources.ListSeparator, s.ToArray());
        }

        private static string GetCultureEquivalentString(MonthsOfTheYear val) {
            if (val == MonthsOfTheYear.AllMonths) {
                return Resources.MOYAllMonths;
            }

            var s = new List<string>(12);
            var vals = Enum.GetValues(val.GetType());
            for (var i = 0; i < vals.Length - 1; i++) {
                if ((val & (MonthsOfTheYear)vals.GetValue(i)) > 0) {
                    s.Add(DateTimeFormatInfo.CurrentInfo.GetMonthName(i + 1));
                }
            }

            return string.Join(Resources.ListSeparator, s.ToArray());
        }

        private static string BuildEnumString(string preface, object enumValue) {
            var vals = enumValue.ToString().Split(new[] {", "}, StringSplitOptions.None);
            if (vals.Length == 0) {
                return string.Empty;
            }
            for (var i = 0; i < vals.Length; i++) {
                vals[i] = Resources.ResourceManager.GetString(preface + vals[i]);
            }
            return string.Join(Resources.ListSeparator, vals);
        }
    }
}