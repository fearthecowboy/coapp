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
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Collections;
    using Properties;
    using V1;
    using V2;

    #region Enumerations

    /// <summary>
    ///   Values for days of the week (Monday, Tuesday, etc.)
    /// </summary>
    [Flags]
    public enum DaysOfTheWeek : short {
        /// <summary>
        ///   Sunday
        /// </summary>
        Sunday = 0x1,

        /// <summary>
        ///   Monday
        /// </summary>
        Monday = 0x2,

        /// <summary>
        ///   Tuesday
        /// </summary>
        Tuesday = 0x4,

        /// <summary>
        ///   Wednesday
        /// </summary>
        Wednesday = 0x8,

        /// <summary>
        ///   Thursday
        /// </summary>
        Thursday = 0x10,

        /// <summary>
        ///   Friday
        /// </summary>
        Friday = 0x20,

        /// <summary>
        ///   Saturday
        /// </summary>
        Saturday = 0x40,

        /// <summary>
        ///   All days
        /// </summary>
        AllDays = 0x7F
    }

    /// <summary>
    ///   Values for months of the year (January, February, etc.)
    /// </summary>
    [Flags]
    public enum MonthsOfTheYear : short {
        /// <summary>
        ///   January
        /// </summary>
        January = 0x1,

        /// <summary>
        ///   February
        /// </summary>
        February = 0x2,

        /// <summary>
        ///   March
        /// </summary>
        March = 0x4,

        /// <summary>
        ///   April
        /// </summary>
        April = 0x8,

        /// <summary>
        ///   May
        /// </summary>
        May = 0x10,

        /// <summary>
        ///   June
        /// </summary>
        June = 0x20,

        /// <summary>
        ///   July
        /// </summary>
        July = 0x40,

        /// <summary>
        ///   August
        /// </summary>
        August = 0x80,

        /// <summary>
        ///   September
        /// </summary>
        September = 0x100,

        /// <summary>
        ///   October
        /// </summary>
        October = 0x200,

        /// <summary>
        ///   November
        /// </summary>
        November = 0x400,

        /// <summary>
        ///   December
        /// </summary>
        December = 0x800,

        /// <summary>
        ///   All months
        /// </summary>
        AllMonths = 0xFFF
    }

    /// <summary>
    ///   Defines the type of triggers that can be used by tasks.
    /// </summary>
    public enum TaskTriggerType {
        /// <summary>
        ///   Triggers the task when a specific event occurs. Version 1.2 only.
        /// </summary>
        Event = 0,

        /// <summary>
        ///   Triggers the task at a specific time of day.
        /// </summary>
        Time = 1,

        /// <summary>
        ///   Triggers the task on a daily schedule.
        /// </summary>
        Daily = 2,

        /// <summary>
        ///   Triggers the task on a weekly schedule.
        /// </summary>
        Weekly = 3,

        /// <summary>
        ///   Triggers the task on a monthly schedule.
        /// </summary>
        Monthly = 4,

        /// <summary>
        ///   Triggers the task on a monthly day-of-week schedule.
        /// </summary>
        MonthlyDOW = 5,

        /// <summary>
        ///   Triggers the task when the computer goes into an idle state.
        /// </summary>
        Idle = 6,

        /// <summary>
        ///   Triggers the task when the task is registered. Version 1.2 only.
        /// </summary>
        Registration = 7,

        /// <summary>
        ///   Triggers the task when the computer boots.
        /// </summary>
        Boot = 8,

        /// <summary>
        ///   Triggers the task when a specific user logs on.
        /// </summary>
        Logon = 9,

        /// <summary>
        ///   Triggers the task when a specific user session state changes. Version 1.2 only.
        /// </summary>
        SessionStateChange = 11,
    }

    /// <summary>
    ///   Values for week of month (first, second, ..., last)
    /// </summary>
    [Flags]
    public enum WhichWeek : short {
        /// <summary>
        ///   First week of the month
        /// </summary>
        FirstWeek = 1,

        /// <summary>
        ///   Second week of the month
        /// </summary>
        SecondWeek = 2,

        /// <summary>
        ///   Third week of the month
        /// </summary>
        ThirdWeek = 4,

        /// <summary>
        ///   Fourth week of the month
        /// </summary>
        FourthWeek = 8,

        /// <summary>
        ///   Last week of the month
        /// </summary>
        LastWeek = 0x10,

        /// <summary>
        ///   Every week of the month
        /// </summary>
        AllWeeks = 0x1F
    }

    #endregion Enumerations

    #region Interfaces

    /// <summary>
    ///   Interface for triggers that support a delay.
    /// </summary>
    public interface ITriggerDelay {
        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan Delay { get; set; }
    }

    /// <summary>
    ///   Interface for triggers that support a user identifier.
    /// </summary>
    public interface ITriggerUserId {
        /// <summary>
        ///   Gets or sets the user for the <see cref="Trigger" /> .
        /// </summary>
        string UserId { get; set; }
    }

    #endregion Interfaces

    /// <summary>
    ///   Abstract base class which provides the common properties that are inherited by all trigger classes. A trigger can be created using the <see
    ///    cref="TriggerCollection.Add" /> or the <see cref="TriggerCollection.AddNew" /> method.
    /// </summary>
    public abstract class Trigger : IDisposable, ICloneable {
        internal const string V2BoundaryDateFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFK";

        internal TaskTriggerType ttype;
        internal ITaskTrigger v1Trigger;
        internal TaskTrigger v1TriggerData;
        internal ITrigger v2Trigger;

        /// <summary>
        ///   In testing and may change. Do not use until officially introduced into library.
        /// </summary>
        protected IDictionary<string, object> unboundValues;
        private RepetitionPattern repititionPattern;

        internal Trigger(ITaskTrigger trigger, V1.TaskTriggerType type) {
            v1Trigger = trigger;
            v1TriggerData = trigger.GetTrigger();
            v1TriggerData.Type = type;
            ttype = ConvertFromV1TriggerType(type);
        }

        internal Trigger(ITaskTrigger trigger, TaskTrigger data) {
            v1Trigger = trigger;
            v1TriggerData = data;
            ttype = ConvertFromV1TriggerType(data.Type);
        }

        internal Trigger(ITrigger iTrigger) {
            v2Trigger = iTrigger;
            ttype = iTrigger.Type;
            if (string.IsNullOrEmpty(v2Trigger.StartBoundary)) {
                StartBoundary = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
            }
        }

        internal Trigger(TaskTriggerType triggerType) {
            ttype = triggerType;
            unboundValues = new XDictionary<string, object>();

            v1TriggerData.TriggerSize = (ushort)Marshal.SizeOf(typeof (TaskTrigger));
            try {
                v1TriggerData.Type = ConvertToV1TriggerType(ttype);
            } catch {
            }

            StartBoundary = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
        }

        /// <summary>
        ///   Gets or sets a Boolean value that indicates whether the trigger is enabled.
        /// </summary>
        [DefaultValue(true)]
        public bool Enabled {
            get {
                if (v2Trigger != null) {
                    return v2Trigger.Enabled;
                }
                return !((v1TriggerData.Flags & TaskTriggerFlags.Disabled) == TaskTriggerFlags.Disabled);
            }
            set {
                if (v2Trigger != null) {
                    v2Trigger.Enabled = value;
                } else {
                    if (!value) {
                        v1TriggerData.Flags |= TaskTriggerFlags.Disabled;
                    } else {
                        v1TriggerData.Flags &= ~TaskTriggerFlags.Disabled;
                    }
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["Enabled"] = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets the date and time when the trigger is deactivated. The trigger cannot start the task after it is deactivated.
        /// </summary>
        [DefaultValue(0x2bca2875f4373fffL)]
        public DateTime EndBoundary {
            get {
                if (v2Trigger != null) {
                    return string.IsNullOrEmpty(v2Trigger.EndBoundary) ? DateTime.MaxValue : DateTime.Parse(v2Trigger.EndBoundary);
                }
                return (unboundValues != null && unboundValues.ContainsKey("EndBoundary"))
                    ? (DateTime)unboundValues["EndBoundary"]
                    : v1TriggerData.EndDate;
            }
            set {
                if (v2Trigger != null) {
                    v2Trigger.EndBoundary = value == DateTime.MaxValue ? null : value.ToString(V2BoundaryDateFormat);
                } else {
                    v1TriggerData.EndDate = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["EndBoundary"] = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets the maximum amount of time that the task launched by this trigger is allowed to run. Not available with Task Scheduler 1.0.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan ExecutionTimeLimit {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(v2Trigger.ExecutionTimeLimit);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return ((unboundValues != null && unboundValues.ContainsKey("ExecutionTimeLimit"))
                    ? (TimeSpan)unboundValues["ExecutionTimeLimit"]
                    : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    v2Trigger.ExecutionTimeLimit = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["ExecutionTimeLimit"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the identifier for the trigger. Cannot set with Task Scheduler 1.0.
        /// </summary>
        [DefaultValue(null)]
        public string Id {
            get {
                if (v2Trigger != null) {
                    return v2Trigger.Id;
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("Id") ? (string)unboundValues["Id"] : null);
            }
            set {
                if (v2Trigger != null) {
                    v2Trigger.Id = value;
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["Id"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets a <see cref="RepetitionPattern" /> instance that indicates how often the task is run and how long the repetition pattern is repeated after the task is started.
        /// </summary>
        public RepetitionPattern Repetition {
            get {
                if (repititionPattern == null) {
                    repititionPattern = new RepetitionPattern(this);
                }
                return repititionPattern;
            }
        }

        /// <summary>
        ///   Gets or sets the date and time when the trigger is activated.
        /// </summary>
        public DateTime StartBoundary {
            get {
                if (v2Trigger != null) {
                    return string.IsNullOrEmpty(v2Trigger.StartBoundary) ? DateTime.MinValue : DateTime.Parse(v2Trigger.StartBoundary);
                }
                return (unboundValues != null && unboundValues.ContainsKey("StartBoundary"))
                    ? (DateTime)unboundValues["StartBoundary"]
                    : v1TriggerData.BeginDate;
            }
            set {
                if (v2Trigger != null) {
                    v2Trigger.StartBoundary = value == DateTime.MinValue ? null : value.ToString(V2BoundaryDateFormat);
                } else {
                    v1TriggerData.BeginDate = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["StartBoundary"] = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets the type of the trigger.
        /// </summary>
        /// <value> The <see cref="TaskTriggerType" /> of the trigger. </value>
        public TaskTriggerType TriggerType {
            get {
                return ttype;
            }
        }

        /// <summary>
        ///   In testing and may change. Do not use until officially introduced into library.
        /// </summary>
        internal virtual bool Bound {
            get {
                if (v1Trigger != null) {
                    return v1Trigger.GetTrigger().Equals(v1TriggerData);
                }
                return (v2Trigger != null);
            }
        }

        /// <summary>
        ///   Creates a new <see cref="Trigger" /> that is an unbound copy of this instance.
        /// </summary>
        /// <returns> A new <see cref="Trigger" /> that is an unbound copy of this instance. </returns>
        public object Clone() {
            var ret = CreateTrigger(TriggerType);
            ret.CopyProperties(this);
            return ret;
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public virtual void CopyProperties(Trigger sourceTrigger) {
            Enabled = sourceTrigger.Enabled;
            EndBoundary = sourceTrigger.EndBoundary;
            try {
                ExecutionTimeLimit = sourceTrigger.ExecutionTimeLimit;
            } catch {
            }
            Repetition.Duration = sourceTrigger.Repetition.Duration;
            Repetition.Interval = sourceTrigger.Repetition.Interval;
            Repetition.StopAtDurationEnd = sourceTrigger.Repetition.StopAtDurationEnd;
            StartBoundary = sourceTrigger.StartBoundary;
            if (sourceTrigger is ITriggerDelay && this is ITriggerDelay) {
                try {
                    ((ITriggerDelay)this).Delay = ((ITriggerDelay)sourceTrigger).Delay;
                } catch {
                }
            }
            if (sourceTrigger is ITriggerUserId && this is ITriggerUserId) {
                try {
                    ((ITriggerUserId)this).UserId = ((ITriggerUserId)sourceTrigger).UserId;
                } catch {
                }
            }
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public virtual void Dispose() {
            if (v2Trigger != null) {
                Marshal.ReleaseComObject(v2Trigger);
            }
            if (v1Trigger != null) {
                Marshal.ReleaseComObject(v1Trigger);
            }
        }

        /// <summary>
        ///   Returns a string representing this trigger.
        /// </summary>
        /// <returns> String value of trigger. </returns>
        public override string ToString() {
            if (v1Trigger != null) {
                return v1Trigger.GetTriggerString();
            }
            return V2GetTriggerString() + V2BaseTriggerString();
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents this trigger in a specific language.
        /// </summary>
        /// <param name="culture"> The language of the resulting string. </param>
        /// <returns> String value of trigger. </returns>
        public virtual string ToString(CultureInfo culture) {
            using (new CultureSwitcher(culture)) {
                return ToString();
            }
        }

        internal static TaskTriggerType ConvertFromV1TriggerType(V1.TaskTriggerType v1Type) {
            var v2tt = (int)v1Type + 1;
            if (v2tt > 6) {
                v2tt++;
            }
            return (TaskTriggerType)v2tt;
        }

        internal static V1.TaskTriggerType ConvertToV1TriggerType(TaskTriggerType type) {
            if (type == TaskTriggerType.Registration || type == TaskTriggerType.Event || type == TaskTriggerType.SessionStateChange) {
                throw new NotV1SupportedException();
            }
            var v1tt = (int)type - 1;
            if (v1tt >= 7) {
                v1tt--;
            }
            return (V1.TaskTriggerType)v1tt;
        }

        internal static Trigger CreateTrigger(ITaskTrigger trigger) {
            return CreateTrigger(trigger, trigger.GetTrigger().Type);
        }

        internal static Trigger CreateTrigger(ITaskTrigger trigger, V1.TaskTriggerType triggerType) {
            Trigger t = null;
            switch (triggerType) {
                case V1.TaskTriggerType.RunOnce:
                    t = new TimeTrigger(trigger);
                    break;
                case V1.TaskTriggerType.RunDaily:
                    t = new DailyTrigger(trigger);
                    break;
                case V1.TaskTriggerType.RunWeekly:
                    t = new WeeklyTrigger(trigger);
                    break;
                case V1.TaskTriggerType.RunMonthly:
                    t = new MonthlyTrigger(trigger);
                    break;
                case V1.TaskTriggerType.RunMonthlyDOW:
                    t = new MonthlyDOWTrigger(trigger);
                    break;
                case V1.TaskTriggerType.OnIdle:
                    t = new IdleTrigger(trigger);
                    break;
                case V1.TaskTriggerType.OnSystemStart:
                    t = new BootTrigger(trigger);
                    break;
                case V1.TaskTriggerType.OnLogon:
                    t = new LogonTrigger(trigger);
                    break;
                default:
                    break;
            }
            //if (t != null) t.ttype = triggerType;
            return t;
        }

        internal static Trigger CreateTrigger(ITrigger iTrigger) {
            switch (iTrigger.Type) {
                case TaskTriggerType.Boot:
                    return new BootTrigger(iTrigger);
                case TaskTriggerType.Daily:
                    return new DailyTrigger(iTrigger);
                case TaskTriggerType.Event:
                    return new EventTrigger(iTrigger);
                case TaskTriggerType.Idle:
                    return new IdleTrigger(iTrigger);
                case TaskTriggerType.Logon:
                    return new LogonTrigger(iTrigger);
                case TaskTriggerType.Monthly:
                    return new MonthlyTrigger(iTrigger);
                case TaskTriggerType.MonthlyDOW:
                    return new MonthlyDOWTrigger(iTrigger);
                case TaskTriggerType.Registration:
                    return new RegistrationTrigger(iTrigger);
                case TaskTriggerType.SessionStateChange:
                    return new SessionStateChangeTrigger(iTrigger);
                case TaskTriggerType.Time:
                    return new TimeTrigger(iTrigger);
                case TaskTriggerType.Weekly:
                    return new WeeklyTrigger(iTrigger);
                default:
                    break;
            }
            return null;
        }

        /// <summary>
        ///   Creates the specified trigger.
        /// </summary>
        /// <param name="triggerType"> Type of the trigger to instantiate. </param>
        /// <returns> <see cref="Trigger" /> of specified type. </returns>
        public static Trigger CreateTrigger(TaskTriggerType triggerType) {
            switch (triggerType) {
                case TaskTriggerType.Boot:
                    return new BootTrigger();
                case TaskTriggerType.Daily:
                    return new DailyTrigger();
                case TaskTriggerType.Event:
                    return new EventTrigger();
                case TaskTriggerType.Idle:
                    return new IdleTrigger();
                case TaskTriggerType.Logon:
                    return new LogonTrigger();
                case TaskTriggerType.Monthly:
                    return new MonthlyTrigger();
                case TaskTriggerType.MonthlyDOW:
                    return new MonthlyDOWTrigger();
                case TaskTriggerType.Registration:
                    return new RegistrationTrigger();
                case TaskTriggerType.SessionStateChange:
                    return new SessionStateChangeTrigger();
                case TaskTriggerType.Time:
                    return new TimeTrigger();
                case TaskTriggerType.Weekly:
                    return new WeeklyTrigger();
                default:
                    break;
            }
            return null;
        }

        internal virtual void Bind(ITask iTask) {
            if (v1Trigger == null) {
                ushort idx;
                v1Trigger = iTask.CreateTrigger(out idx);
            }
            SetV1TriggerData();
        }

        internal virtual void Bind(ITaskDefinition iTaskDef) {
            var iTriggers = iTaskDef.Triggers;
            v2Trigger = iTriggers.Create(ttype);
            Marshal.ReleaseComObject(iTriggers);
            foreach (var key in unboundValues.Keys) {
                try {
                    var o = unboundValues[key];
                    CheckBindValue(key, ref o);
                    v2Trigger.GetType().InvokeMember(key, BindingFlags.SetProperty, null, v2Trigger, new[] {o});
                } catch (TargetInvocationException tie) {
                    throw tie.InnerException;
                } catch {
                }
            }
            unboundValues.Clear();
            unboundValues = null;

            repititionPattern = new RepetitionPattern(this);
            repititionPattern.Bind();
        }

        /// <summary>
        ///   Checks the bind value for any conversion.
        /// </summary>
        /// <param name="key"> The key (property) name. </param>
        /// <param name="o"> The value. </param>
        internal virtual void CheckBindValue(string key, ref object o) {
            if (o is TimeSpan) {
                o = Task.TimeSpanToString((TimeSpan)o);
            }
            if (o is DateTime) {
                if ((key == "EndBoundary" && ((DateTime)o) == DateTime.MaxValue) ||
                    (key == "StartBoundary" && ((DateTime)o) == DateTime.MinValue)) {
                    o = null;
                } else {
                    o = ((DateTime)o).ToString(V2BoundaryDateFormat);
                }
            }
        }

        /// <summary>
        ///   Assigns the unbound TriggerData structure to the V1 trigger instance.
        /// </summary>
        internal void SetV1TriggerData() {
            if (v1TriggerData.MinutesInterval != 0 && v1TriggerData.MinutesInterval >= v1TriggerData.MinutesDuration) {
                throw new ArgumentException(
                    "Trigger.Repetition.Interval must be less than Trigger.Repetition.Duration under Task Scheduler 1.0.");
            }
            if (v1Trigger != null) {
                v1Trigger.SetTrigger(ref v1TriggerData);
            }
            Debug.WriteLine(v1TriggerData);
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected virtual string V2GetTriggerString() {
            return string.Empty;
        }

        private string V2BaseTriggerString() {
            var ret = new StringBuilder();
            if (Repetition.Interval != TimeSpan.Zero) {
                var sduration = string.Empty;
                if (Repetition.Duration == TimeSpan.Zero) {
                    sduration = Resources.TriggerDuration0;
                } else {
                    sduration = string.Format(Resources.TriggerDurationNot0, GetBestTimeSpanString(Repetition.Duration));
                }
                ret.AppendFormat(Resources.TriggerRepetition, GetBestTimeSpanString(Repetition.Interval), sduration);
            }
            if (EndBoundary != DateTime.MaxValue) {
                ret.AppendFormat(Resources.TriggerEndBoundary, EndBoundary);
            }
            if (ret.Length > 0) {
                ret.Insert(0, Resources.HyphenSeparator);
            }
            return ret.ToString();
        }

        private static Type timeSpan2Type;
        private static bool foundTimeSpan2;

        /// <summary>
        ///   Gets the best time span string.
        /// </summary>
        /// <param name="span"> The <see cref="TimeSpan" /> to display. </param>
        /// <returns> Either the full string representation created by TimeSpan2 or the default TimeSpan representation. </returns>
        private static string GetBestTimeSpanString(TimeSpan span) {
            // See if the TimeSpan2 assembly is accessible
            if (!foundTimeSpan2) {
                try {
                    var asm = Assembly.LoadFrom("TimeSpan2.dll");
                    if (asm != null) {
                        timeSpan2Type = asm.GetType("System.TimeSpan2", false, false);
                        if (timeSpan2Type != null) {
                            foundTimeSpan2 = true;
                        }
                    }
                } catch {
                }
            }

            // If the TimeSpan2 assembly is available, try to call the ToString("f") method and return the result.
            if (foundTimeSpan2 && timeSpan2Type != null) {
                try {
                    var o = Activator.CreateInstance(timeSpan2Type, span);
                    if (o != null) {
                        var mi = timeSpan2Type.GetMethod("ToString", new[] {typeof (string)});
                        if (mi != null) {
                            var r = mi.Invoke(o, new object[] {"f"});
                            if (r != null) {
                                return r.ToString();
                            }
                        }
                    }
                } catch {
                }
            }

            return span.ToString();
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task when the system is booted.
    /// </summary>
    public sealed class BootTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="BootTrigger" /> .
        /// </summary>
        public BootTrigger()
            : base(TaskTriggerType.Boot) {
        }

        internal BootTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.OnSystemStart) {
        }

        internal BootTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time between when the system is booted and when the task is started.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Delay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IBootTrigger)v2Trigger).Delay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("Delay") ? (TimeSpan)unboundValues["Delay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IBootTrigger)v2Trigger).Delay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["Delay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            return Resources.TriggerBoot1;
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task based on a daily schedule. For example, the task starts at a specific time every day, every other day, every third day, and so on.
    /// </summary>
    public sealed class DailyTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="DailyTrigger" /> .
        /// </summary>
        /// <param name="daysInterval"> Interval between the days in the schedule. </param>
        public DailyTrigger(short daysInterval = 1)
            : base(TaskTriggerType.Daily) {
            DaysInterval = daysInterval;
        }

        internal DailyTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.RunDaily) {
        }

        internal DailyTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Sets or retrieves the interval between the days in the schedule.
        /// </summary>
        [DefaultValue(1)]
        public short DaysInterval {
            get {
                if (v2Trigger != null) {
                    return ((IDailyTrigger)v2Trigger).DaysInterval;
                }
                return (short)v1TriggerData.Data.daily.DaysInterval;
            }
            set {
                if (v2Trigger != null) {
                    ((IDailyTrigger)v2Trigger).DaysInterval = value;
                } else {
                    v1TriggerData.Data.daily.DaysInterval = (ushort)value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["DaysInterval"] = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a delay time that is randomly added to the start time of the trigger.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan RandomDelay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IDailyTrigger)v2Trigger).RandomDelay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RandomDelay") ? (TimeSpan)unboundValues["RandomDelay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IDailyTrigger)v2Trigger).RandomDelay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RandomDelay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan ITriggerDelay.Delay {
            get {
                return RandomDelay;
            }
            set {
                RandomDelay = value;
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                DaysInterval = ((DailyTrigger)sourceTrigger).DaysInterval;
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            if (DaysInterval == 1) {
                return string.Format(Resources.TriggerDaily1, StartBoundary);
            }
            return string.Format(Resources.TriggerDaily2, StartBoundary, DaysInterval);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task when a system event occurs. Not available on Task Scheduler 1.0.
    /// </summary>
    public sealed class EventTrigger : Trigger, ITriggerDelay {
        private NamedValueCollection nvc;

        /// <summary>
        ///   Creates an unbound instance of a <see cref="EventTrigger" /> .
        /// </summary>
        public EventTrigger()
            : base(TaskTriggerType.Event) {
        }

        /// <summary>
        ///   Initializes an unbound instance of the <see cref="EventTrigger" /> class and sets a basic event.
        /// </summary>
        /// <param name="log"> The event's log. </param>
        /// <param name="source"> The event's source. Can be <c>null</c> . </param>
        /// <param name="eventId"> The event's id. Can be <c>null</c> . </param>
        public EventTrigger(string log, string source, int? eventId) : this() {
            SetBasic(log, source, eventId);
        }

        internal EventTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time between when the system is booted and when the task is started.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Delay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IEventTrigger)v2Trigger).Delay);
                }
                return (unboundValues.ContainsKey("Delay") ? (TimeSpan)unboundValues["Delay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IEventTrigger)v2Trigger).Delay = Task.TimeSpanToString(value);
                } else {
                    unboundValues["Delay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the XPath query string that identifies the event that fires the trigger.
        /// </summary>
        [DefaultValue((string)null)]
        public string Subscription {
            get {
                if (v2Trigger != null) {
                    return ((IEventTrigger)v2Trigger).Subscription;
                }
                return (unboundValues.ContainsKey("Subscription") ? (string)unboundValues["Subscription"] : null);
            }
            set {
                if (v2Trigger != null) {
                    ((IEventTrigger)v2Trigger).Subscription = value;
                } else {
                    unboundValues["Subscription"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets a collection of named XPath queries. Each query in the collection is applied to the last matching event XML returned from the subscription query specified in the Subscription property. The name of the query can be used as a variable in the message of a <see
        ///    cref="ShowMessageAction" /> action.
        /// </summary>
        public NamedValueCollection ValueQueries {
            get {
                if (nvc == null) {
                    if (v2Trigger == null) {
                        nvc = new NamedValueCollection();
                    } else {
                        nvc = new NamedValueCollection(((IEventTrigger)v2Trigger).ValueQueries);
                    }
                }
                return nvc;
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                Subscription = ((EventTrigger)sourceTrigger).Subscription;
                ((EventTrigger)sourceTrigger).ValueQueries.CopyTo(ValueQueries);
            }
        }

        /// <summary>
        ///   Gets basic event information.
        /// </summary>
        /// <param name="log"> The event's log. </param>
        /// <param name="source"> The event's source. Can be <c>null</c> . </param>
        /// <param name="eventId"> The event's id. Can be <c>null</c> . </param>
        /// <returns> <c>true</c> if subscription represents a basic event, <c>false</c> if not. </returns>
        public bool GetBasic(out string log, out string source, out int? eventId) {
            log = source = null;
            eventId = null;
            if (!string.IsNullOrEmpty(Subscription)) {
                using (var str = new MemoryStream(Encoding.UTF8.GetBytes(Subscription))) {
                    using (var rdr = new XmlTextReader(str)) {
                        rdr.MoveToContent();
                        rdr.ReadStartElement("QueryList");
                        if (rdr.Name == "Query" && rdr.MoveToAttribute("Path")) {
                            var path = rdr.Value;
                            if (rdr.MoveToElement() && rdr.ReadToDescendant("Select") &&
                                path.Equals(rdr["Path"], StringComparison.InvariantCultureIgnoreCase)) {
                                var content = rdr.ReadString();
                                var m = Regex.Match(content,
                                    @"\*(?:\[System\[(?:Provider\[\@Name='(?<s>[^']+)'\])?(?:\s+and\s+)?(?:EventID=(?<e>\d+))?\]\])",
                                    RegexOptions.IgnoreCase |
                                        RegexOptions.Compiled |
                                            RegexOptions.Singleline |
                                                RegexOptions.IgnorePatternWhitespace);
                                if (m.Success) {
                                    log = path;
                                    if (m.Groups["s"].Success) {
                                        source = m.Groups["s"].Value;
                                    }
                                    if (m.Groups["e"].Success) {
                                        eventId = Convert.ToInt32(m.Groups["e"].Value);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        ///   Sets the subscription for a basic event. This will replace the contents of the <see cref="Subscription" /> property and clear all entries in the <see
        ///    cref="ValueQueries" /> property.
        /// </summary>
        /// <param name="log"> The event's log. </param>
        /// <param name="source"> The event's source. Can be <c>null</c> . </param>
        /// <param name="eventId"> The event's id. Can be <c>null</c> . </param>
        public void SetBasic(string log, string source, int? eventId) {
            var sb = new StringBuilder();
            if (string.IsNullOrEmpty(log)) {
                throw new ArgumentNullException("log");
            }
            sb.AppendFormat("<QueryList><Query Id=\"0\" Path=\"{0}\"><Select Path=\"{0}\">*", log);
            bool hasSource = !string.IsNullOrEmpty(source), hasId = eventId.HasValue;
            if (hasSource || hasId) {
                sb.Append("[System[");
                if (hasSource) {
                    sb.AppendFormat("Provider[@Name='{0}']", source);
                }
                if (hasSource && hasId) {
                    sb.Append(" and ");
                }
                if (hasId) {
                    sb.AppendFormat("EventID={0}", eventId.Value);
                }
                sb.Append("]]");
            }
            sb.Append("</Select></Query></QueryList>");
            ValueQueries.Clear();
            Subscription = sb.ToString();
        }

        internal override void Bind(ITaskDefinition iTaskDef) {
            base.Bind(iTaskDef);
            if (nvc != null) {
                nvc.Bind(((IEventTrigger)v2Trigger).ValueQueries);
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            string log, source;
            int? id;
            if (GetBasic(out log, out source, out id)) {
                var sb = new StringBuilder();
                sb.AppendFormat(Resources.TriggerEventBasic1, log);
                if (!string.IsNullOrEmpty(source)) {
                    sb.AppendFormat(Resources.TriggerEventBasic2, source);
                }
                if (id.HasValue) {
                    sb.AppendFormat(Resources.TriggerEventBasic3, id.Value);
                }
                return sb.ToString();
            }
            return Resources.TriggerEvent1;
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task when the computer goes into an idle state. For information about idle conditions, see Task Idle Conditions.
    /// </summary>
    public sealed class IdleTrigger : Trigger {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="IdleTrigger" /> .
        /// </summary>
        public IdleTrigger()
            : base(TaskTriggerType.Idle) {
        }

        internal IdleTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.OnIdle) {
        }

        internal IdleTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            return Resources.TriggerIdle1;
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task when a user logs on. When the Task Scheduler service starts, all logged-on users are enumerated and any tasks registered with logon triggers that match the logged on user are run. Not available on Task Scheduler 1.0.
    /// </summary>
    public sealed class LogonTrigger : Trigger, ITriggerDelay, ITriggerUserId {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="LogonTrigger" /> .
        /// </summary>
        public LogonTrigger()
            : base(TaskTriggerType.Logon) {
        }

        internal LogonTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.OnLogon) {
        }

        internal LogonTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time between when the system is booted and when the task is started.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Delay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((ILogonTrigger)v2Trigger).Delay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("Delay") ? (TimeSpan)unboundValues["Delay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((ILogonTrigger)v2Trigger).Delay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["Delay"] = value;
                }
            }
        }

        /// <summary>
        ///   <para>Gets or sets The identifier of the user. For example, "MyDomain\MyName" or for a local account, "Administrator".</para> <para>This property can be in one of the following formats:</para> <para>• User name or SID: The task is started when the user logs on to the computer.</para> <para>• NULL: The task is started when any user logs on to the computer.</para>
        /// </summary>
        /// <remarks>
        ///   If you want a task to be triggered when any member of a group logs on to the computer rather than when a specific user logs on, then do not assign a value to the LogonTrigger.UserId property. Instead, create a logon trigger with an empty LogonTrigger.UserId property and assign a value to the principal for the task using the Principal.GroupId property.
        /// </remarks>
        [DefaultValue((string)null)]
        public string UserId {
            get {
                if (v2Trigger != null) {
                    return ((ILogonTrigger)v2Trigger).UserId;
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("UserId") ? (string)unboundValues["UserId"] : null);
            }
            set {
                if (v2Trigger != null) {
                    ((ILogonTrigger)v2Trigger).UserId = value;
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["UserId"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            var user = string.IsNullOrEmpty(UserId) ? Resources.TriggerAnyUser : UserId;
            return string.Format(Resources.TriggerLogon1, user);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task on a monthly day-of-week schedule. For example, the task starts on every first Thursday, May through October.
    /// </summary>
    public sealed class MonthlyDOWTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="MonthlyDOWTrigger" /> .
        /// </summary>
        /// <param name="daysOfWeek"> The days of the week. </param>
        /// <param name="monthsOfYear"> The months of the year. </param>
        /// <param name="weeksOfMonth"> The weeks of the month. </param>
        public MonthlyDOWTrigger(DaysOfTheWeek daysOfWeek = DaysOfTheWeek.Sunday, MonthsOfTheYear monthsOfYear = MonthsOfTheYear.AllMonths,
            WhichWeek weeksOfMonth = WhichWeek.FirstWeek)
            : base(TaskTriggerType.MonthlyDOW) {
            DaysOfWeek = daysOfWeek;
            MonthsOfYear = monthsOfYear;
            WeeksOfMonth = weeksOfMonth;
        }

        internal MonthlyDOWTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.RunMonthlyDOW) {
        }

        internal MonthlyDOWTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets the days of the week during which the task runs.
        /// </summary>
        [DefaultValue(0)]
        public DaysOfTheWeek DaysOfWeek {
            get {
                if (v2Trigger != null) {
                    return (DaysOfTheWeek)((IMonthlyDOWTrigger)v2Trigger).DaysOfWeek;
                }
                return v1TriggerData.Data.monthlyDOW.DaysOfTheWeek;
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyDOWTrigger)v2Trigger).DaysOfWeek = (short)value;
                } else {
                    v1TriggerData.Data.monthlyDOW.DaysOfTheWeek = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["DaysOfWeek"] = (short)value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets the months of the year during which the task runs.
        /// </summary>
        [DefaultValue(0)]
        public MonthsOfTheYear MonthsOfYear {
            get {
                if (v2Trigger != null) {
                    return (MonthsOfTheYear)((IMonthlyDOWTrigger)v2Trigger).MonthsOfYear;
                }
                return v1TriggerData.Data.monthlyDOW.Months;
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyDOWTrigger)v2Trigger).MonthsOfYear = (short)value;
                } else {
                    v1TriggerData.Data.monthlyDOW.Months = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["MonthsOfYear"] = (short)value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a delay time that is randomly added to the start time of the trigger.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan RandomDelay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IMonthlyDOWTrigger)v2Trigger).RandomDelay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RandomDelay") ? (TimeSpan)unboundValues["RandomDelay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyDOWTrigger)v2Trigger).RandomDelay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RandomDelay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets a Boolean value that indicates that the task runs on the last week of the month.
        /// </summary>
        [DefaultValue(false)]
        public bool RunOnLastWeekOfMonth {
            get {
                if (v2Trigger != null) {
                    return ((IMonthlyDOWTrigger)v2Trigger).RunOnLastWeekOfMonth;
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    return (unboundValues.ContainsKey("RunOnLastWeekOfMonth") ? (bool)unboundValues["RunOnLastWeekOfMonth"] : false);
                }
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyDOWTrigger)v2Trigger).RunOnLastWeekOfMonth = value;
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RunOnLastWeekOfMonth"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the weeks of the month during which the task runs.
        /// </summary>
        [DefaultValue(0)]
        public WhichWeek WeeksOfMonth {
            get {
                if (v2Trigger != null) {
                    var ww = (WhichWeek)((IMonthlyDOWTrigger)v2Trigger).WeeksOfMonth;
                    // Following addition give accurate results for confusing RunOnLastWeekOfMonth property (thanks kbergeron)
                    if (((IMonthlyDOWTrigger)v2Trigger).RunOnLastWeekOfMonth) {
                        ww |= WhichWeek.LastWeek;
                    }
                    return ww;
                } else if (v1Trigger != null) {
                    return v1TriggerData.Data.monthlyDOW.V2WhichWeek;
                } else {
                    return (unboundValues.ContainsKey("WeeksOfMonth") ? (WhichWeek)unboundValues["WeeksOfMonth"] : WhichWeek.FirstWeek);
                }
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyDOWTrigger)v2Trigger).WeeksOfMonth = (short)value;
                } else {
                    if (v1Trigger != null) {
                        v1TriggerData.Data.monthlyDOW.V2WhichWeek = value;
                        SetV1TriggerData();
                    } else {
                        unboundValues["WeeksOfMonth"] = (short)value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan ITriggerDelay.Delay {
            get {
                return RandomDelay;
            }
            set {
                RandomDelay = value;
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                DaysOfWeek = ((MonthlyDOWTrigger)sourceTrigger).DaysOfWeek;
                MonthsOfYear = ((MonthlyDOWTrigger)sourceTrigger).MonthsOfYear;
                try {
                    RunOnLastWeekOfMonth = ((MonthlyDOWTrigger)sourceTrigger).RunOnLastWeekOfMonth;
                } catch {
                }
                WeeksOfMonth = ((MonthlyDOWTrigger)sourceTrigger).WeeksOfMonth;
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            var ww = TaskEnumGlobalizer.GetString(WeeksOfMonth);
            var days = TaskEnumGlobalizer.GetString(DaysOfWeek);
            var months = TaskEnumGlobalizer.GetString(MonthsOfYear);
            return string.Format(Resources.TriggerMonthlyDOW1, StartBoundary, ww, days, months);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a job based on a monthly schedule. For example, the task starts on specific days of specific months.
    /// </summary>
    public sealed class MonthlyTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="MonthlyTrigger" /> .
        /// </summary>
        /// <param name="dayOfMonth"> The day of the month. </param>
        /// <param name="monthsOfYear"> The months of the year. </param>
        public MonthlyTrigger(int dayOfMonth = 1, MonthsOfTheYear monthsOfYear = MonthsOfTheYear.AllMonths)
            : base(TaskTriggerType.Monthly) {
            DaysOfMonth = new[] {dayOfMonth};
            MonthsOfYear = monthsOfYear;
        }

        internal MonthlyTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.RunMonthly) {
        }

        internal MonthlyTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets the days of the month during which the task runs.
        /// </summary>
        public int[] DaysOfMonth {
            get {
                if (v2Trigger != null) {
                    return MaskToIndices(((IMonthlyTrigger)v2Trigger).DaysOfMonth);
                }
                return MaskToIndices((int)v1TriggerData.Data.monthlyDate.Days);
            }
            set {
                var mask = IndicesToMask(value);
                if (v2Trigger != null) {
                    ((IMonthlyTrigger)v2Trigger).DaysOfMonth = mask;
                } else {
                    v1TriggerData.Data.monthlyDate.Days = (uint)mask;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["DaysOfMonth"] = mask;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets the months of the year during which the task runs.
        /// </summary>
        [DefaultValue(0)]
        public MonthsOfTheYear MonthsOfYear {
            get {
                if (v2Trigger != null) {
                    return (MonthsOfTheYear)((IMonthlyTrigger)v2Trigger).MonthsOfYear;
                }
                return v1TriggerData.Data.monthlyDOW.Months;
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyTrigger)v2Trigger).MonthsOfYear = (short)value;
                } else {
                    v1TriggerData.Data.monthlyDOW.Months = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["MonthsOfYear"] = (short)value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a delay time that is randomly added to the start time of the trigger.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan RandomDelay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IMonthlyTrigger)v2Trigger).RandomDelay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RandomDelay") ? (TimeSpan)unboundValues["RandomDelay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyTrigger)v2Trigger).RandomDelay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RandomDelay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets a Boolean value that indicates that the task runs on the last day of the month.
        /// </summary>
        [DefaultValue(false)]
        public bool RunOnLastDayOfMonth {
            get {
                if (v2Trigger != null) {
                    return ((IMonthlyTrigger)v2Trigger).RunOnLastDayOfMonth;
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RunOnLastDayOfMonth") ? (bool)unboundValues["RunOnLastDayOfMonth"] : false);
            }
            set {
                if (v2Trigger != null) {
                    ((IMonthlyTrigger)v2Trigger).RunOnLastDayOfMonth = value;
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RunOnLastDayOfMonth"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan ITriggerDelay.Delay {
            get {
                return RandomDelay;
            }
            set {
                RandomDelay = value;
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                DaysOfMonth = ((MonthlyTrigger)sourceTrigger).DaysOfMonth;
                MonthsOfYear = ((MonthlyTrigger)sourceTrigger).MonthsOfYear;
                try {
                    RunOnLastDayOfMonth = ((MonthlyTrigger)sourceTrigger).RunOnLastDayOfMonth;
                } catch {
                }
            }
        }

        /// <summary>
        ///   Converts an array of bit indices into a mask with bits turned ON at every index contained in the array. Indices must be from 1 to 32 and bits are numbered the same.
        /// </summary>
        /// <param name="indices"> An array with an element for each bit of the mask which is ON. </param>
        /// <returns> An interger to be interpreted as a mask. </returns>
        internal static int IndicesToMask(int[] indices) {
            var mask = 0;
            foreach (var index in indices) {
                if (index < 1 || index > 31) {
                    throw new ArgumentException("Days must be in the range 1..31");
                }
                mask = mask | 1 << (index - 1);
            }
            return mask;
        }

        /// <summary>
        ///   Convert an integer representing a mask to an array where each element contains the index of a bit that is ON in the mask. Bits are considered to number from 1 to 32.
        /// </summary>
        /// <param name="mask"> An interger to be interpreted as a mask. </param>
        /// <returns> An array with an element for each bit of the mask which is ON. </returns>
        internal static int[] MaskToIndices(int mask) {
            //count bits in mask
            var cnt = 0;
            for (var i = 0; (mask >> i) > 0; i++) {
                cnt = cnt + (1 & (mask >> i));
            }
            //allocate return array with one entry for each bit
            var indices = new int[cnt];
            //fill array with bit indices
            cnt = 0;
            for (var i = 0; (mask >> i) > 0; i++) {
                if ((1 & (mask >> i)) == 1) {
                    indices[cnt++] = i + 1;
                }
            }
            return indices;
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            var days = string.Join(Resources.ListSeparator, Array.ConvertAll(DaysOfMonth, delegate(int i) {
                return i.ToString();
            }));
            var months = TaskEnumGlobalizer.GetString(MonthsOfYear);
            return string.Format(Resources.TriggerMonthly1, StartBoundary, days, months);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task when the task is registered or updated. Not available on Task Scheduler 1.0.
    /// </summary>
    public sealed class RegistrationTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="RegistrationTrigger" /> .
        /// </summary>
        public RegistrationTrigger()
            : base(TaskTriggerType.Registration) {
        }

        internal RegistrationTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time between when the system is booted and when the task is started.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Delay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IRegistrationTrigger)v2Trigger).Delay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("Delay") ? (TimeSpan)unboundValues["Delay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IRegistrationTrigger)v2Trigger).Delay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["Delay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            return Resources.TriggerRegistration1;
        }
    }

    /// <summary>
    ///   Defines how often the task is run and how long the repetition pattern is repeated after the task is started.
    /// </summary>
    public sealed class RepetitionPattern : IDisposable {
        private readonly Trigger pTrigger;
        private readonly IRepetitionPattern v2Pattern;

        internal RepetitionPattern(Trigger parent) {
            pTrigger = parent;
            if (pTrigger.v2Trigger != null) {
                v2Pattern = pTrigger.v2Trigger.Repetition;
            }
        }

        /// <summary>
        ///   Gets or sets how long the pattern is repeated.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Duration {
            get {
                if (v2Pattern != null) {
                    return Task.StringToTimeSpan(v2Pattern.Duration);
                }
                return TimeSpan.FromMinutes(pTrigger.v1TriggerData.MinutesDuration);
            }
            set {
                if (v2Pattern != null) {
                    v2Pattern.Duration = Task.TimeSpanToString(value);
                } else {
                    pTrigger.v1TriggerData.MinutesDuration = (uint)value.TotalMinutes;
                    Bind();
                }
            }
        }

        /// <summary>
        ///   Gets or sets the amount of time between each restart of the task.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Interval {
            get {
                if (v2Pattern != null) {
                    return Task.StringToTimeSpan(v2Pattern.Interval);
                }
                return TimeSpan.FromMinutes(pTrigger.v1TriggerData.MinutesInterval);
            }
            set {
                if (v2Pattern != null) {
                    v2Pattern.Interval = Task.TimeSpanToString(value);
                } else {
                    pTrigger.v1TriggerData.MinutesInterval = (uint)value.TotalMinutes;
                    Bind();
                }
            }
        }

        /// <summary>
        ///   Gets or sets a Boolean value that indicates if a running instance of the task is stopped at the end of repetition pattern duration.
        /// </summary>
        [DefaultValue(false)]
        public bool StopAtDurationEnd {
            get {
                if (v2Pattern != null) {
                    return v2Pattern.StopAtDurationEnd;
                }
                return (pTrigger.v1TriggerData.Flags & TaskTriggerFlags.KillAtDurationEnd) == TaskTriggerFlags.KillAtDurationEnd;
            }
            set {
                if (v2Pattern != null) {
                    v2Pattern.StopAtDurationEnd = value;
                } else {
                    if (value) {
                        pTrigger.v1TriggerData.Flags |= TaskTriggerFlags.KillAtDurationEnd;
                    } else {
                        pTrigger.v1TriggerData.Flags &= ~TaskTriggerFlags.KillAtDurationEnd;
                    }
                    Bind();
                }
            }
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            if (v2Pattern != null) {
                Marshal.ReleaseComObject(v2Pattern);
            }
        }

        internal void Bind() {
            if (pTrigger.v1Trigger != null) {
                pTrigger.v1Trigger.SetTrigger(ref pTrigger.v1TriggerData);
            } else if (pTrigger.v2Trigger != null) {
                if (pTrigger.v1TriggerData.MinutesInterval != 0) {
                    v2Pattern.Interval = string.Format("PT{0}M", pTrigger.v1TriggerData.MinutesInterval);
                }
                if (pTrigger.v1TriggerData.MinutesDuration != 0) {
                    v2Pattern.Duration = string.Format("PT{0}M", pTrigger.v1TriggerData.MinutesDuration);
                }
                v2Pattern.StopAtDurationEnd = (pTrigger.v1TriggerData.Flags & TaskTriggerFlags.KillAtDurationEnd) ==
                    TaskTriggerFlags.KillAtDurationEnd;
            }
        }
    }

    /// <summary>
    ///   Triggers tasks for console connect or disconnect, remote connect or disconnect, or workstation lock or unlock notifications.
    /// </summary>
    public sealed class SessionStateChangeTrigger : Trigger, ITriggerDelay, ITriggerUserId {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="SessionStateChangeTrigger" /> .
        /// </summary>
        public SessionStateChangeTrigger()
            : base(TaskTriggerType.SessionStateChange) {
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="SessionStateChangeTrigger" /> class.
        /// </summary>
        /// <param name="stateChange"> The state change. </param>
        public SessionStateChangeTrigger(TaskSessionStateChangeType stateChange) : this() {
            StateChange = stateChange;
        }

        internal SessionStateChangeTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time between when the system is booted and when the task is started.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan Delay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((ISessionStateChangeTrigger)v2Trigger).Delay);
                }
                return (unboundValues.ContainsKey("Delay") ? (TimeSpan)unboundValues["Delay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((ISessionStateChangeTrigger)v2Trigger).Delay = Task.TimeSpanToString(value);
                } else {
                    unboundValues["Delay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the kind of Terminal Server session change that would trigger a task launch.
        /// </summary>
        [DefaultValue(0)]
        public TaskSessionStateChangeType StateChange {
            get {
                if (v2Trigger != null) {
                    return ((ISessionStateChangeTrigger)v2Trigger).StateChange;
                }
                return (unboundValues.ContainsKey("StateChange")
                    ? (TaskSessionStateChangeType)unboundValues["StateChange"]
                    : TaskSessionStateChangeType.ConsoleConnect);
            }
            set {
                if (v2Trigger != null) {
                    ((ISessionStateChangeTrigger)v2Trigger).StateChange = value;
                } else {
                    unboundValues["StateChange"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the user for the Terminal Server session. When a session state change is detected for this user, a task is started.
        /// </summary>
        [DefaultValue((string)null)]
        public string UserId {
            get {
                if (v2Trigger != null) {
                    return ((ISessionStateChangeTrigger)v2Trigger).UserId;
                }
                return (unboundValues.ContainsKey("UserId") ? (string)unboundValues["UserId"] : null);
            }
            set {
                if (v2Trigger != null) {
                    ((ISessionStateChangeTrigger)v2Trigger).UserId = value;
                } else {
                    unboundValues["UserId"] = value;
                }
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                StateChange = ((SessionStateChangeTrigger)sourceTrigger).StateChange;
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            var str = Resources.ResourceManager.GetString("TriggerSession" + StateChange.ToString());
            var user = string.IsNullOrEmpty(UserId) ? Resources.TriggerAnyUser : UserId;
            if (StateChange != TaskSessionStateChangeType.SessionLock && StateChange != TaskSessionStateChangeType.SessionUnlock) {
                user = string.Format(Resources.TriggerSessionUserSession, user);
            }
            return string.Format(str, user);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task at a specific date and time.
    /// </summary>
    public sealed class TimeTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="TimeTrigger" /> .
        /// </summary>
        public TimeTrigger()
            : base(TaskTriggerType.Time) {
        }

        /// <summary>
        ///   Creates an unbound instance of a <see cref="TimeTrigger" /> and assigns the execution time.
        /// </summary>
        /// <param name="startBoundary"> Date and time for the trigger to fire. </param>
        public TimeTrigger(DateTime startBoundary)
            : base(TaskTriggerType.Time) {
            StartBoundary = startBoundary;
        }

        internal TimeTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.RunOnce) {
        }

        internal TimeTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets a delay time that is randomly added to the start time of the trigger.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan RandomDelay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((ITimeTrigger)v2Trigger).RandomDelay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RandomDelay") ? (TimeSpan)unboundValues["RandomDelay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((ITimeTrigger)v2Trigger).RandomDelay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RandomDelay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan ITriggerDelay.Delay {
            get {
                return RandomDelay;
            }
            set {
                RandomDelay = value;
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            return string.Format(Resources.TriggerTime1, StartBoundary);
        }
    }

    /// <summary>
    ///   Represents a trigger that starts a task based on a weekly schedule. For example, the task starts at 8:00 A.M. on a specific day of the week every week or every other week.
    /// </summary>
    public sealed class WeeklyTrigger : Trigger, ITriggerDelay {
        /// <summary>
        ///   Creates an unbound instance of a <see cref="WeeklyTrigger" /> .
        /// </summary>
        /// <param name="daysOfWeek"> The days of the week. </param>
        /// <param name="weeksInterval"> The interval between the weeks in the schedule. </param>
        public WeeklyTrigger(DaysOfTheWeek daysOfWeek = DaysOfTheWeek.Sunday, short weeksInterval = 1)
            : base(TaskTriggerType.Weekly) {
            DaysOfWeek = daysOfWeek;
            WeeksInterval = weeksInterval;
        }

        internal WeeklyTrigger(ITaskTrigger iTrigger)
            : base(iTrigger, V1.TaskTriggerType.RunWeekly) {
        }

        internal WeeklyTrigger(ITrigger iTrigger)
            : base(iTrigger) {
        }

        /// <summary>
        ///   Gets or sets the days of the week on which the task runs.
        /// </summary>
        [DefaultValue(0)]
        public DaysOfTheWeek DaysOfWeek {
            get {
                if (v2Trigger != null) {
                    return (DaysOfTheWeek)((IWeeklyTrigger)v2Trigger).DaysOfWeek;
                }
                return v1TriggerData.Data.weekly.DaysOfTheWeek;
            }
            set {
                if (v2Trigger != null) {
                    ((IWeeklyTrigger)v2Trigger).DaysOfWeek = (short)value;
                } else {
                    v1TriggerData.Data.weekly.DaysOfTheWeek = value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["DaysOfWeek"] = (short)value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a delay time that is randomly added to the start time of the trigger.
        /// </summary>
        [DefaultValue(0)]
        public TimeSpan RandomDelay {
            get {
                if (v2Trigger != null) {
                    return Task.StringToTimeSpan(((IWeeklyTrigger)v2Trigger).RandomDelay);
                }
                if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                }
                return (unboundValues.ContainsKey("RandomDelay") ? (TimeSpan)unboundValues["RandomDelay"] : TimeSpan.Zero);
            }
            set {
                if (v2Trigger != null) {
                    ((IWeeklyTrigger)v2Trigger).RandomDelay = Task.TimeSpanToString(value);
                } else if (v1Trigger != null) {
                    throw new NotV1SupportedException();
                } else {
                    unboundValues["RandomDelay"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the interval between the weeks in the schedule.
        /// </summary>
        [DefaultValue(0)]
        public short WeeksInterval {
            get {
                if (v2Trigger != null) {
                    return ((IWeeklyTrigger)v2Trigger).WeeksInterval;
                }
                return (short)v1TriggerData.Data.weekly.WeeksInterval;
            }
            set {
                if (v2Trigger != null) {
                    ((IWeeklyTrigger)v2Trigger).WeeksInterval = value;
                } else {
                    v1TriggerData.Data.weekly.WeeksInterval = (ushort)value;
                    if (v1Trigger != null) {
                        SetV1TriggerData();
                    } else {
                        unboundValues["WeeksInterval"] = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets or sets a value that indicates the amount of time before the task is started.
        /// </summary>
        /// <value> The delay duration. </value>
        TimeSpan ITriggerDelay.Delay {
            get {
                return RandomDelay;
            }
            set {
                RandomDelay = value;
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Trigger" /> the current instance. This will not copy any properties associated with any derived triggers except those supporting the <see
        ///    cref="ITriggerDelay" /> interface.
        /// </summary>
        /// <param name="sourceTrigger"> The source <see cref="Trigger" /> . </param>
        public override void CopyProperties(Trigger sourceTrigger) {
            base.CopyProperties(sourceTrigger);
            if (sourceTrigger.GetType() == GetType()) {
                DaysOfWeek = ((WeeklyTrigger)sourceTrigger).DaysOfWeek;
                WeeksInterval = ((WeeklyTrigger)sourceTrigger).WeeksInterval;
            }
        }

        /// <summary>
        ///   Gets the non-localized trigger string for V2 triggers.
        /// </summary>
        /// <returns> String describing the trigger. </returns>
        protected override string V2GetTriggerString() {
            var days = TaskEnumGlobalizer.GetString(DaysOfWeek);
            return string.Format(WeeksInterval == 1 ? Resources.TriggerWeekly1Week : Resources.TriggerWeeklyMultWeeks,
                StartBoundary, days, WeeksInterval);
        }
    }
}