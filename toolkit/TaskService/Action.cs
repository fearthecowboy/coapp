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
    using System.Reflection;
    using System.Runtime.InteropServices;
    using Collections;
    using Properties;
    using V1;
    using V2;

    /// <summary>
    ///   Defines the type of actions a task can perform.
    /// </summary>
    /// <remarks>
    ///   The action type is defined when the action is created and cannot be changed later. See <see
    ///    cref="ActionCollection.AddNew" /> .
    /// </remarks>
    public enum TaskActionType {
        /// <summary>
        ///   This action fires a handler.
        /// </summary>
        ComHandler = 5,

        /// <summary>
        ///   This action performs a command-line operation. For example, the action can run a script, launch an executable, or, if the name of a document is provided, find its associated application and launch the application with the document.
        /// </summary>
        Execute = 0,

        /// <summary>
        ///   This action sends and e-mail.
        /// </summary>
        SendEmail = 6,

        /// <summary>
        ///   This action shows a message box.
        /// </summary>
        ShowMessage = 7
    }

    /// <summary>
    ///   Abstract base class that provides the common properties that are inherited by all action objects. An action object is created by the <see
    ///    cref="ActionCollection.AddNew" /> method.
    /// </summary>
    public abstract class Action : IDisposable, ICloneable {
        internal IAction iAction;

        /// <summary>
        ///   List of unbound values when working with Actions not associated with a registered task.
        /// </summary>
        protected IDictionary<string, object> unboundValues = new XDictionary<string, object>();

        internal virtual bool Bound {
            get {
                return iAction != null;
            }
        }

        internal virtual void Bind(ITask iTask) {
        }

        internal virtual void Bind(ITaskDefinition iTaskDef) {
            var iActions = iTaskDef.Actions;
            switch (GetType().Name) {
                case "ComHandlerAction":
                    iAction = iActions.Create(TaskActionType.ComHandler);
                    break;
                case "ExecAction":
                    iAction = iActions.Create(TaskActionType.Execute);
                    break;
                case "EmailAction":
                    iAction = iActions.Create(TaskActionType.SendEmail);
                    break;
                case "ShowMessageAction":
                    iAction = iActions.Create(TaskActionType.ShowMessage);
                    break;
                default:
                    throw new ArgumentException();
            }
            Marshal.ReleaseComObject(iActions);
            foreach (var key in unboundValues.Keys) {
                try {
                    iAction.GetType().InvokeMember(key, BindingFlags.SetProperty, null, iAction, new[] {unboundValues[key]});
                } catch (TargetInvocationException tie) {
                    throw tie.InnerException;
                } catch {
                }
            }
            unboundValues.Clear();
        }

        /// <summary>
        ///   Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns> A new object that is a copy of this instance. </returns>
        public object Clone() {
            var ret = CreateAction(ActionType);
            ret.CopyProperties(this);
            return ret;
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Action" /> the current instance.
        /// </summary>
        /// <param name="sourceAction"> The source <see cref="Action" /> . </param>
        protected virtual void CopyProperties(Action sourceAction) {
            Id = sourceAction.Id;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public virtual void Dispose() {
            if (iAction != null) {
                Marshal.ReleaseComObject(iAction);
            }
        }

        /// <summary>
        ///   Gets the type of the action.
        /// </summary>
        /// <value> The type of the action. </value>
        public TaskActionType ActionType {
            get {
                if (iAction != null) {
                    return iAction.Type;
                }
                if (this is ComHandlerAction) {
                    return TaskActionType.ComHandler;
                }
                if (this is ShowMessageAction) {
                    return TaskActionType.ShowMessage;
                }
                if (this is EmailAction) {
                    return TaskActionType.SendEmail;
                }
                return TaskActionType.Execute;
            }
        }

        /// <summary>
        ///   Gets or sets the identifier of the action.
        /// </summary>
        public virtual string Id {
            get {
                return (iAction == null) ? (unboundValues.ContainsKey("Id") ? (string)unboundValues["Id"] : null) : iAction.Id;
            }
            set {
                if (iAction == null) {
                    unboundValues["Id"] = value;
                } else {
                    iAction.Id = value;
                }
            }
        }

        /// <summary>
        ///   Returns the action Id.
        /// </summary>
        /// <returns> String representation of action. </returns>
        public override string ToString() {
            return Id;
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents this action.
        /// </summary>
        /// <param name="culture"> The culture. </param>
        /// <returns> String representation of action. </returns>
        public virtual string ToString(CultureInfo culture) {
            using (new CultureSwitcher(culture)) {
                return ToString();
            }
        }

        /// <summary>
        ///   Creates a specialized class from a defined interface.
        /// </summary>
        /// <param name="iAction"> Version 2.0 Action interface. </param>
        /// <returns> Specialized action class </returns>
        internal static Action CreateAction(IAction iAction) {
            switch (iAction.Type) {
                case TaskActionType.ComHandler:
                    return new ComHandlerAction((IComHandlerAction)iAction);
                case TaskActionType.SendEmail:
                    return new EmailAction((IEmailAction)iAction);
                case TaskActionType.ShowMessage:
                    return new ShowMessageAction((IShowMessageAction)iAction);
                case TaskActionType.Execute:
                default:
                    return new ExecAction((IExecAction)iAction);
            }
        }

        /// <summary>
        ///   Creates the specified action.
        /// </summary>
        /// <param name="actionType"> Type of the action to instantiate. </param>
        /// <returns> <see cref="Action" /> of specified type. </returns>
        public static Action CreateAction(TaskActionType actionType) {
            switch (actionType) {
                case TaskActionType.ComHandler:
                    return new ComHandlerAction();
                case TaskActionType.SendEmail:
                    return new EmailAction();
                case TaskActionType.ShowMessage:
                    return new ShowMessageAction();
                case TaskActionType.Execute:
                default:
                    return new ExecAction();
            }
        }
    }

    /// <summary>
    ///   Represents an action that fires a handler. Only available on Task Scheduler 2.0.
    /// </summary>
    public sealed class ComHandlerAction : Action {
        /// <summary>
        ///   Creates an unbound instance of <see cref="ComHandlerAction" /> .
        /// </summary>
        public ComHandlerAction() {
        }

        /// <summary>
        ///   Creates an unbound instance of <see cref="ComHandlerAction" /> .
        /// </summary>
        /// <param name="classId"> Identifier of the handler class. </param>
        /// <param name="data"> Addition data associated with the handler. </param>
        public ComHandlerAction(Guid classId, string data) {
            ClassId = classId;
            Data = data;
        }

        internal ComHandlerAction(IComHandlerAction action) {
            iAction = action;
        }

        /// <summary>
        ///   Gets or sets the identifier of the handler class.
        /// </summary>
        public Guid ClassId {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("ClassId") ? (Guid)unboundValues["ClassId"] : Guid.Empty)
                    : new Guid(((IComHandlerAction)iAction).ClassId);
            }
            set {
                if (iAction == null) {
                    unboundValues["ClassId"] = value.ToString();
                } else {
                    ((IComHandlerAction)iAction).ClassId = value.ToString();
                }
            }
        }

        /// <summary>
        ///   Gets or sets additional data that is associated with the handler.
        /// </summary>
        public string Data {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Data") ? (string)unboundValues["Data"] : null)
                    : ((IComHandlerAction)iAction).Data;
            }
            set {
                if (iAction == null) {
                    unboundValues["Data"] = value;
                } else {
                    ((IComHandlerAction)iAction).Data = value;
                }
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Action" /> the current instance.
        /// </summary>
        /// <param name="sourceAction"> The source <see cref="Action" /> . </param>
        protected override void CopyProperties(Action sourceAction) {
            if (sourceAction.GetType() == GetType()) {
                base.CopyProperties(sourceAction);
                ClassId = ((ComHandlerAction)sourceAction).ClassId;
                Data = ((ComHandlerAction)sourceAction).Data;
            }
        }

        /// <summary>
        ///   Gets a string representation of the <see cref="ComHandlerAction" /> .
        /// </summary>
        /// <returns> String represention this action. </returns>
        public override string ToString() {
            return string.Format(Resources.ComHandlerAction, ClassId, Data, Id);
        }
    }

    /// <summary>
    ///   Represents an action that executes a command-line operation.
    /// </summary>
    public sealed class ExecAction : Action {
        private readonly ITask v1Task;

        /// <summary>
        ///   Creates a new instance of an <see cref="ExecAction" /> that can be added to <see cref="TaskDefinition.Actions" /> .
        /// </summary>
        public ExecAction() {
        }

        /// <summary>
        ///   Creates a new instance of an <see cref="ExecAction" /> that can be added to <see cref="TaskDefinition.Actions" /> .
        /// </summary>
        /// <param name="path"> Path to an executable file. </param>
        /// <param name="arguments"> Arguments associated with the command-line operation. This value can be null. </param>
        /// <param name="workingDirectory"> Directory that contains either the executable file or the files that are used by the executable file. This value can be null. </param>
        public ExecAction(string path, string arguments = null, string workingDirectory = null) {
            Path = path;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        internal ExecAction(ITask task) {
            v1Task = task;
        }

        internal ExecAction(IExecAction action) {
            iAction = action;
        }

        internal override bool Bound {
            get {
                if (v1Task != null) {
                    return true;
                }
                return base.Bound;
            }
        }

        internal override void Bind(ITask v1Task) {
            object o = null;
            unboundValues.TryGetValue("Path", out o);
            v1Task.SetApplicationName(o == null ? string.Empty : o.ToString());
            o = null;
            unboundValues.TryGetValue("Arguments", out o);
            v1Task.SetParameters(o == null ? string.Empty : o.ToString());
            o = null;
            unboundValues.TryGetValue("WorkingDirectory", out o);
            v1Task.SetWorkingDirectory(o == null ? string.Empty : o.ToString());
        }

        /// <summary>
        ///   Gets or sets the identifier of the action.
        /// </summary>
        public override string Id {
            get {
                if (v1Task != null) {
                    return System.IO.Path.GetFileNameWithoutExtension(Task.GetV1Path(v1Task)) + "_Action";
                }
                return base.Id;
            }
            set {
                if (v1Task != null) {
                    throw new NotV1SupportedException();
                }
                base.Id = value;
            }
        }

        /// <summary>
        ///   Gets or sets the path to an executable file.
        /// </summary>
        public string Path {
            get {
                if (v1Task != null) {
                    return v1Task.GetApplicationName();
                }
                if (iAction != null) {
                    return ((IExecAction)iAction).Path;
                }
                return unboundValues.ContainsKey("Path") ? (string)unboundValues["Path"] : null;
            }
            set {
                if (v1Task != null) {
                    v1Task.SetApplicationName(value);
                } else if (iAction != null) {
                    ((IExecAction)iAction).Path = value;
                } else {
                    unboundValues["Path"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the arguments associated with the command-line operation.
        /// </summary>
        public string Arguments {
            get {
                if (v1Task != null) {
                    return v1Task.GetParameters();
                }
                if (iAction != null) {
                    return ((IExecAction)iAction).Arguments;
                }
                return unboundValues.ContainsKey("Arguments") ? (string)unboundValues["Arguments"] : null;
            }
            set {
                if (v1Task != null) {
                    v1Task.SetParameters(value);
                } else if (iAction != null) {
                    ((IExecAction)iAction).Arguments = value;
                } else {
                    unboundValues["Arguments"] = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the directory that contains either the executable file or the files that are used by the executable file.
        /// </summary>
        public string WorkingDirectory {
            get {
                if (v1Task != null) {
                    return v1Task.GetWorkingDirectory();
                }
                if (iAction != null) {
                    return ((IExecAction)iAction).WorkingDirectory;
                }
                return unboundValues.ContainsKey("WorkingDirectory") ? (string)unboundValues["WorkingDirectory"] : null;
            }
            set {
                if (v1Task != null) {
                    v1Task.SetWorkingDirectory(value);
                } else if (iAction != null) {
                    ((IExecAction)iAction).WorkingDirectory = value;
                } else {
                    unboundValues["WorkingDirectory"] = value;
                }
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Action" /> the current instance.
        /// </summary>
        /// <param name="sourceAction"> The source <see cref="Action" /> . </param>
        protected override void CopyProperties(Action sourceAction) {
            if (sourceAction.GetType() == GetType()) {
                base.CopyProperties(sourceAction);
                Path = ((ExecAction)sourceAction).Path;
                Arguments = ((ExecAction)sourceAction).Arguments;
                WorkingDirectory = ((ExecAction)sourceAction).WorkingDirectory;
            }
        }

        /// <summary>
        ///   Gets a string representation of the <see cref="ExecAction" /> .
        /// </summary>
        /// <returns> String represention this action. </returns>
        public override string ToString() {
            return string.Format(Resources.ExecAction, Path, Arguments, WorkingDirectory, Id);
        }
    }

    /// <summary>
    ///   Represents an action that sends an e-mail.
    /// </summary>
    public sealed class EmailAction : Action {
        /// <summary>
        ///   Creates an unbound instance of <see cref="EmailAction" /> .
        /// </summary>
        public EmailAction() {
        }

        /// <summary>
        ///   Creates an unbound instance of <see cref="EmailAction" /> .
        /// </summary>
        /// <param name="subject"> Subject of the e-mail. </param>
        /// <param name="from"> E-mail address that you want to send the e-mail from. </param>
        /// <param name="to"> E-mail address or addresses that you want to send the e-mail to. </param>
        /// <param name="body"> Body of the e-mail that contains the e-mail message. </param>
        /// <param name="mailServer"> Name of the server that you use to send e-mail from. </param>
        public EmailAction(string subject, string from, string to, string body, string mailServer) {
            Subject = subject;
            From = from;
            To = to;
            Body = body;
            Server = mailServer;
        }

        internal EmailAction(IEmailAction action) {
            iAction = action;
        }

        internal override void Bind(ITaskDefinition iTaskDef) {
            base.Bind(iTaskDef);
            if (nvc != null) {
                nvc.Bind(((IEmailAction)iAction).HeaderFields);
            }
        }

        /// <summary>
        ///   Gets or sets the name of the server that you use to send e-mail from.
        /// </summary>
        public string Server {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Server") ? (string)unboundValues["Server"] : null)
                    : ((IEmailAction)iAction).Server;
            }
            set {
                if (iAction == null) {
                    unboundValues["Server"] = value;
                } else {
                    ((IEmailAction)iAction).Server = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the subject of the e-mail.
        /// </summary>
        public string Subject {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Subject") ? (string)unboundValues["Subject"] : null)
                    : ((IEmailAction)iAction).Subject;
            }
            set {
                if (iAction == null) {
                    unboundValues["Subject"] = value;
                } else {
                    ((IEmailAction)iAction).Subject = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the e-mail address or addresses that you want to send the e-mail to.
        /// </summary>
        public string To {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("To") ? (string)unboundValues["To"] : null)
                    : ((IEmailAction)iAction).To;
            }
            set {
                if (iAction == null) {
                    unboundValues["To"] = value;
                } else {
                    ((IEmailAction)iAction).To = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the e-mail address or addresses that you want to Cc in the e-mail.
        /// </summary>
        public string Cc {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Cc") ? (string)unboundValues["Cc"] : null)
                    : ((IEmailAction)iAction).Cc;
            }
            set {
                if (iAction == null) {
                    unboundValues["Cc"] = value;
                } else {
                    ((IEmailAction)iAction).Cc = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the e-mail address or addresses that you want to Bcc in the e-mail.
        /// </summary>
        public string Bcc {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Bcc") ? (string)unboundValues["Bcc"] : null)
                    : ((IEmailAction)iAction).Bcc;
            }
            set {
                if (iAction == null) {
                    unboundValues["Bcc"] = value;
                } else {
                    ((IEmailAction)iAction).Bcc = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the e-mail address that you want to reply to.
        /// </summary>
        public string ReplyTo {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("ReplyTo") ? (string)unboundValues["ReplyTo"] : null)
                    : ((IEmailAction)iAction).ReplyTo;
            }
            set {
                if (iAction == null) {
                    unboundValues["ReplyTo"] = value;
                } else {
                    ((IEmailAction)iAction).ReplyTo = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the e-mail address that you want to send the e-mail from.
        /// </summary>
        public string From {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("From") ? (string)unboundValues["From"] : null)
                    : ((IEmailAction)iAction).From;
            }
            set {
                if (iAction == null) {
                    unboundValues["From"] = value;
                } else {
                    ((IEmailAction)iAction).From = value;
                }
            }
        }

        private NamedValueCollection nvc;

        /// <summary>
        ///   Gets or sets the header information in the e-mail message to send.
        /// </summary>
        public NamedValueCollection HeaderFields {
            get {
                if (nvc == null) {
                    if (iAction != null) {
                        nvc = new NamedValueCollection(((IEmailAction)iAction).HeaderFields);
                    } else {
                        nvc = new NamedValueCollection();
                    }
                }
                return nvc;
            }
        }

        /// <summary>
        ///   Gets or sets the body of the e-mail that contains the e-mail message.
        /// </summary>
        public string Body {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Body") ? (string)unboundValues["Body"] : null)
                    : ((IEmailAction)iAction).Body;
            }
            set {
                if (iAction == null) {
                    unboundValues["Body"] = value;
                } else {
                    ((IEmailAction)iAction).Body = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets an array of attachments that is sent with the e-mail.
        /// </summary>
        public object[] Attachments {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Attachments") ? (object[])unboundValues["Attachments"] : null)
                    : ((IEmailAction)iAction).Attachments;
            }
            set {
                if (iAction == null) {
                    unboundValues["Attachments"] = value;
                } else {
                    ((IEmailAction)iAction).Attachments = value;
                }
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Action" /> the current instance.
        /// </summary>
        /// <param name="sourceAction"> The source <see cref="Action" /> . </param>
        protected override void CopyProperties(Action sourceAction) {
            if (sourceAction.GetType() == GetType()) {
                base.CopyProperties(sourceAction);
                if (((EmailAction)sourceAction).Attachments != null) {
                    Attachments = (object[])((EmailAction)sourceAction).Attachments.Clone();
                }
                Bcc = ((EmailAction)sourceAction).Bcc;
                Body = ((EmailAction)sourceAction).Body;
                Cc = ((EmailAction)sourceAction).Cc;
                From = ((EmailAction)sourceAction).From;
                if (((EmailAction)sourceAction).nvc != null) {
                    ((EmailAction)sourceAction).HeaderFields.CopyTo(HeaderFields);
                }
                ReplyTo = ((EmailAction)sourceAction).ReplyTo;
                Server = ((EmailAction)sourceAction).Server;
                Subject = ((EmailAction)sourceAction).Subject;
                To = ((EmailAction)sourceAction).To;
            }
        }

        /// <summary>
        ///   Gets a string representation of the <see cref="EmailAction" /> .
        /// </summary>
        /// <returns> String represention this action. </returns>
        public override string ToString() {
            return string.Format(Resources.EmailAction, Subject, To, Cc, Bcc, From, ReplyTo, Body, Server, Id);
        }
    }

    /// <summary>
    ///   Represents an action that shows a message box when a task is activated.
    /// </summary>
    public sealed class ShowMessageAction : Action {
        /// <summary>
        ///   Creates a new unbound instance of <see cref="ShowMessageAction" /> .
        /// </summary>
        public ShowMessageAction() {
        }

        /// <summary>
        ///   Creates a new unbound instance of <see cref="ShowMessageAction" /> .
        /// </summary>
        /// <param name="messageBody"> Message text that is displayed in the body of the message box. </param>
        /// <param name="title"> Title of the message box. </param>
        public ShowMessageAction(string messageBody, string title) {
            MessageBody = messageBody;
            Title = title;
        }

        internal ShowMessageAction(IShowMessageAction action) {
            iAction = action;
        }

        /// <summary>
        ///   Gets or sets the title of the message box.
        /// </summary>
        public string Title {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("Title") ? (string)unboundValues["Title"] : null)
                    : ((IShowMessageAction)iAction).Title;
            }
            set {
                if (iAction == null) {
                    unboundValues["Title"] = value;
                } else {
                    ((IShowMessageAction)iAction).Title = value;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the message text that is displayed in the body of the message box.
        /// </summary>
        public string MessageBody {
            get {
                return (iAction == null)
                    ? (unboundValues.ContainsKey("MessageBody") ? (string)unboundValues["MessageBody"] : null)
                    : ((IShowMessageAction)iAction).MessageBody;
            }
            set {
                if (iAction == null) {
                    unboundValues["MessageBody"] = value;
                } else {
                    ((IShowMessageAction)iAction).MessageBody = value;
                }
            }
        }

        /// <summary>
        ///   Copies the properties from another <see cref="Action" /> the current instance.
        /// </summary>
        /// <param name="sourceAction"> The source <see cref="Action" /> . </param>
        protected override void CopyProperties(Action sourceAction) {
            if (sourceAction.GetType() == GetType()) {
                base.CopyProperties(sourceAction);
                Title = ((ShowMessageAction)sourceAction).Title;
                MessageBody = ((ShowMessageAction)sourceAction).MessageBody;
            }
        }

        /// <summary>
        ///   Gets a string representation of the <see cref="ShowMessageAction" /> .
        /// </summary>
        /// <returns> String represention this action. </returns>
        public override string ToString() {
            return string.Format(Resources.ShowMessageAction, Title, MessageBody, Id);
        }
    }
}