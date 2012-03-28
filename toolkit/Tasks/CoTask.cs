//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Exceptions;
    using Logging;

    public static class CoTask {
        private static readonly FieldInfo _parentTaskField = typeof (Task).GetField("m_parent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance);
        private static readonly PropertyInfo _currentTaskProperty = typeof(Task).GetProperty("InternalCurrent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static);
        private static readonly Dictionary<Task, List<MessageHandlers>> _tasks = new Dictionary<Task, List<MessageHandlers>>();
        private static readonly Dictionary<Task, Task> _parentTasks = new Dictionary<Task, Task>();

        public static Task<T> AsResultTask<T>(this T result) {
            var x = new TaskCompletionSource<T>(TaskCreationOptions.AttachedToParent);
            x.SetResult(result);
            return x.Task;
        }

        private static bool IsTaskReallyCompleted(Task task) {
            if( !task.IsCompleted )
                return false;

            return !(from child in _parentTasks.Keys where _parentTasks[child] == task && !IsTaskReallyCompleted(child) select child).Any();
        }

        public static void Collect() {
            lock (_tasks) {
                var completedTasks = (from t in _tasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    _tasks.Remove(t);
                }
            }

            lock (_parentTasks) {
                var completedTasks = (from t in _parentTasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    _parentTasks.Remove(t);
                }
            }
        }

        /// <summary>
        /// This associates a child task with the parent task.
        /// 
        /// This isn't necessary (and will have no effect) when the child task is created with AttachToParent in the creation/continuation options,
        /// but it does take a few cycles to validate that there is actually a parent, so don't call this when not needed. 
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static Task AutoManage(this Task task) {

#if DEBUG
            if( task.GetParentTask() != null ) {
                var stackTrace = new StackTrace(true);
                var frames = stackTrace.GetFrames();
                foreach (var frame in frames) {
                    if (frame != null) {
                        var method = frame.GetMethod();
                        var fnName = method.Name;
                        var cls = method.DeclaringType;
                        if (cls.Namespace.Contains("Tasks")) {
                            continue;
                        }
                        Logger.Warning("Unneccesary Automanage() in (in {2}.{3}) call at {0}:{1} ", frame.GetFileName(), frame.GetFileLineNumber(), cls.Name, fnName);
                        break;
                    }
                }
            }
#endif

            if ( task.GetParentTask() == null ) {
                lock( _parentTasks ) {
                    var currentTask = CurrentTask;
                    if (currentTask != null) {
                        // the given task isn't attached to the parent.
                        // we can fake out attachment, by using the current task
                        _parentTasks.Add(task, currentTask);
                    }
                }
            }
            return task;
        }

        public static Task<T> AutoManage<T>(this Task<T> task) {
            AutoManage((Task) task);
            return task;
        }

        internal static Task CurrentTask { get {
            return _currentTaskProperty.GetValue(null, null) as Task;
        }}

        internal static Task GetParentTask( this Task task ) {
            if (task == null) {
                return null;
            }
                
            return _parentTaskField.GetValue(task) as Task ?? (_parentTasks.ContainsKey(task) ? _parentTasks[task] : null);
        }

        internal static Task ParentTask {
            get { return CurrentTask.GetParentTask(); }
        }
  
        /// <summary>
        /// Gets the message handler.
        /// </summary>
        /// <param name="task">The task to get the message handler for.</param>
        /// <param name="messageHandlerType">the message handler class</param>
        /// <returns>A message handler; null if there isn't one. </returns>
        /// <remarks></remarks>
        internal static MessageHandlers GetMessageHandler(this Task task, Type messageHandlerType) {
            if( task == null )
                return null;

            // if the current task has an entry.
            if( _tasks.ContainsKey(task)) {
                return (from handler in _tasks[task] where messageHandlerType.IsInstanceOfType(handler)  select handler).FirstOrDefault() ??
                    GetMessageHandler(task.GetParentTask(), messageHandlerType);
            }

            // otherwise, check with the parent.
            return GetMessageHandler(task.GetParentTask(), messageHandlerType);
        }  


        /// <summary>
        /// Adds a collection of message handlers to a task.
        /// </summary>
        /// <param name="handlers">The handlers.</param>
        /// <remarks></remarks>
        internal static void AddMessageHandlers(this Task task, IEnumerable<MessageHandlers> handlers) {
            foreach (var handler in handlers)
                AddMessageHandler(task, handler);
        }

        /// <summary>
        /// Adds the message handler to the given task.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        internal static MessageHandlers AddMessageHandler(this Task task, MessageHandlers handler) {
            if (handler == null) {
                return null;
            }

            if (task == null) {
                return null;
            }

            for (var count = 10; count > 0 && task.GetParentTask() == null; count--) {
                Thread.Sleep(10); // yeild for a bit
            }

#if DEBUG
            if (task.GetParentTask() == null) {
                var stackTrace = new StackTrace(true);
                var frames = stackTrace.GetFrames();
                foreach (var frame in frames) {
                    if (frame != null) {
                        var method = frame.GetMethod();
                        var fnName = method.Name;
                        var cls = method.DeclaringType;
                        if (cls.Namespace.Contains("Tasks")) {
                            continue;
                        }
                        Logger.Warning("Info: Registering a MessageHandler where no parent task exists in (in {2}.{3}) call at {0}:{1} ", frame.GetFileName(), frame.GetFileLineNumber(), cls.Name, fnName);
                        break;
                    }
                }
            }
#endif

            // ensures that missing delegates are set with at least an empty action/function
            // or if a parent task has one, uses that.
            handler.SetMissingDelegates();

            lock (_tasks) {
                if (!_tasks.ContainsKey(task)) {
                    _tasks.Add(task, new List<MessageHandlers>());
                }
                _tasks[task].Add(handler);
            }
            return handler;
        }

        public static void Iterate<TResult>(this TaskCompletionSource<TResult> tcs, IEnumerable<System.Threading.Tasks.Task> asyncIterator) {
            var enumerator = asyncIterator.GetEnumerator();
            Action<System.Threading.Tasks.Task> recursiveBody = null;
            recursiveBody = completedTask => {
                if (completedTask != null && completedTask.IsFaulted) {
                    tcs.TrySetException(completedTask.Exception.InnerExceptions);
                    enumerator.Dispose();
                }
                else if (enumerator.MoveNext()) {
                    enumerator.Current.ContinueWith(recursiveBody, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);
                }
                else {
                    enumerator.Dispose();
                }
            };
            recursiveBody(null);
        }
        
        public static void Ignore( this AggregateException aggregateException, Type type, Action saySomething = null) {
            foreach (var exception in aggregateException.Flatten().InnerExceptions) {
                if (exception.GetType() == type) {
                    if (saySomething != null)
                        saySomething();
                    continue;
                }
                throw new ConsoleException("Exception Caught: {0}\r\n    {1}", exception.Message, exception.StackTrace);
            }
        }
    }
}