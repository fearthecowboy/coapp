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
    using System.Reflection.Emit;
    using System.Threading;
    using System.Threading.Tasks;
    using Exceptions;
    using Logging;

    public static class CurrentTask {
        public class TaskBoundEvents {
            public static TaskBoundEvents Instance = new TaskBoundEvents();

            private TaskBoundEvents() {
            }

            /// <summary>
            ///   Adds an event handler delegate to the current tasktask
            /// </summary>
            /// <param name="taskBoundEvents"> </param>
            /// <param name="eventHandlerDelegate"> </param>
            /// <returns> </returns>
            public static TaskBoundEvents operator +(TaskBoundEvents taskBoundEvents, Delegate eventHandlerDelegate) {
                CoTask.CurrentTask.AddEventHandler(eventHandlerDelegate);
                return Instance;
            }

            public static TaskBoundEvents operator -(TaskBoundEvents taskBoundEvents, Delegate eventHandlerDelegate) {
                CoTask.CurrentTask.RemoveEventHandler(eventHandlerDelegate);
                return Instance;
            }
        }

        public static TaskBoundEvents Events = TaskBoundEvents.Instance;
    }

    public static class Event<T> where T : class {
        private static T _emptyDelegate;

        /// <summary>
        ///   Gets the parameter types of a Delegate
        /// </summary>
        /// <param name="d"> The d. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static Type[] GetDelegateParameterTypes(Type d) {
            if (d.BaseType != typeof (MulticastDelegate)) {
                throw new ApplicationException("Not a delegate.");
            }

            var invoke = d.GetMethod("Invoke");
            if (invoke == null) {
                throw new ApplicationException("Not a delegate.");
            }

            var parameters = invoke.GetParameters();
            var typeParameters = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                typeParameters[i] = parameters[i].ParameterType;
            }
            return typeParameters;
        }

        /// <summary>
        ///   Gets the Return type of a delegate
        /// </summary>
        /// <param name="d"> The d. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static Type GetDelegateReturnType(Type d) {
            if (d.BaseType != typeof (MulticastDelegate)) {
                throw new ApplicationException("Not a delegate.");
            }

            MethodInfo invoke = d.GetMethod("Invoke");
            if (invoke == null) {
                throw new ApplicationException("Not a delegate.");
            }

            return invoke.ReturnType;
        }

        /// <summary>
        ///   Returns a delegate that does nothing, and returns default(T) that can be used without having to check to see if the delegate is null.
        /// </summary>
        private static T EmptyDelegate {
            get {
                if (_emptyDelegate == null) {
                    Type delegateReturnType = GetDelegateReturnType(typeof (T));
                    Type[] delegateParameterTypes = GetDelegateParameterTypes(typeof (T));

                    var dynamicMethod = new DynamicMethod(string.Empty, delegateReturnType, delegateParameterTypes);
                    ILGenerator il = dynamicMethod.GetILGenerator();

                    if (delegateReturnType.FullName != "System.Void") {
                        if (delegateReturnType.IsValueType) {
                            il.Emit(OpCodes.Ldc_I4, 0);
                        } else {
                            il.Emit(OpCodes.Ldnull);
                        }
                    }
                    il.Emit(OpCodes.Ret);
                    _emptyDelegate = dynamicMethod.CreateDelegate(typeof (T)) as T;
                }
                return _emptyDelegate;
            }
        }

        public static T Raise {
            get {
                return (CoTask.CurrentTask.GetEventHandler(typeof (T)) as T) ?? EmptyDelegate;
            }
        }

        public static T RaiseFirst {
            get {
                var dlg = CoTask.CurrentTask.GetEventHandler(typeof (T));
                return dlg != null ? dlg.GetInvocationList().FirstOrDefault() as T : EmptyDelegate;
            }
        }
    }

    public static class CoTask {
        private static readonly FieldInfo ParentTaskField = typeof (Task).GetField("m_parent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance);
        private static readonly PropertyInfo CurrentTaskProperty = typeof (Task).GetProperty("InternalCurrent", BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static);
        private static readonly Dictionary<Task, List<Delegate>> Tasks = new Dictionary<Task, List<Delegate>>();
        private static readonly Dictionary<Task, Task> ParentTasks = new Dictionary<Task, Task>();
        private static readonly List<Delegate> NullTaskDelegates = new List<Delegate>();

        public static Task<T> AsResultTask<T>(this T result) {
            var x = new TaskCompletionSource<T>(TaskCreationOptions.AttachedToParent);
            x.SetResult(result);
            return x.Task;
        }

        public static Task<T> AsCanceledTask<T>(this T result) {
            var x = new TaskCompletionSource<T>(TaskCreationOptions.AttachedToParent);
            x.SetCanceled();
            return x.Task;
        }

        private static bool IsTaskReallyCompleted(Task task) {
            if (!task.IsCompleted) {
                return false;
            }

            return !(from child in ParentTasks.Keys where ParentTasks[child] == task && !IsTaskReallyCompleted(child) select child).Any();
        }

        public static void Collect() {
            lock (Tasks) {
                var completedTasks = (from t in Tasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    Tasks.Remove(t);
                }
            }

            lock (ParentTasks) {
                var completedTasks = (from t in ParentTasks.Keys where IsTaskReallyCompleted(t) select t).ToArray();
                foreach (var t in completedTasks) {
                    ParentTasks.Remove(t);
                }
            }
        }

        /// <summary>
        ///   This associates a child task with the parent task. This isn't necessary (and will have no effect) when the child task is created with AttachToParent in the creation/continuation options, but it does take a few cycles to validate that there is actually a parent, so don't call this when not needed.
        /// </summary>
        /// <param name="task"> </param>
        /// <returns> </returns>
        public static Task AutoManage(this Task task) {
#if DEBUG
            if (task.GetParentTask() != null) {
                var stackTrace = new StackTrace(true);
                var frames = stackTrace.GetFrames();
                if (frames != null) {
                    foreach (var frame in frames) {
                        if (frame != null) {
                            var method = frame.GetMethod();
                            var fnName = method.Name;
                            var cls = method.DeclaringType;
                            if (cls != null) {
                                if (cls.Namespace != null && cls.Namespace.Contains("Tasks")) {
                                    continue;
                                }
                                Logger.Warning("Unneccesary Automanage() in (in {2}.{3}) call at {0}:{1} ", frame.GetFileName(), frame.GetFileLineNumber(), cls.Name, fnName);
                            }
                            break;
                        }
                    }
                }
            }
#endif

            if (task.GetParentTask() == null) {
                lock (ParentTasks) {
                    var currentTask = CurrentTask;
                    if (currentTask != null) {
                        // the given task isn't attached to the parent.
                        // we can fake out attachment, by using the current task
                        ParentTasks.Add(task, currentTask);
                    }
                }
            }
            return task;
        }

        public static Task<T> AutoManage<T>(this Task<T> task) {
            AutoManage((Task)task);
            return task;
        }

        internal static Task CurrentTask {
            get {
                return CurrentTaskProperty.GetValue(null, null) as Task;
            }
        }

        internal static Task GetParentTask(this Task task) {
            if (task == null) {
                return null;
            }

            return ParentTaskField.GetValue(task) as Task ?? (ParentTasks.ContainsKey(task) ? ParentTasks[task] : null);
        }

        internal static Task ParentTask {
            get {
                return CurrentTask.GetParentTask();
            }
        }

        /// <summary>
        ///   Gets the message handler.
        /// </summary>
        /// <param name="task"> The task to get the message handler for. </param>
        /// <param name="eventDelegateHandlerType"> the delegate handler class </param>
        /// <returns> A delegate handler; null if there isn't one. </returns>
        /// <remarks>
        /// </remarks>
        internal static Delegate GetEventHandler(this Task task, Type eventDelegateHandlerType) {
            if (task == null) {
                return Delegate.Combine((from handlerDelegate in NullTaskDelegates where eventDelegateHandlerType.IsInstanceOfType(handlerDelegate) select handlerDelegate).ToArray());
            }

            // if the current task has an entry.
            if (Tasks.ContainsKey(task)) {
                var result = Delegate.Combine((from handler in Tasks[task] where handler.GetType().IsAssignableFrom(eventDelegateHandlerType) select handler).ToArray());
                return Delegate.Combine(result, GetEventHandler(task.GetParentTask(), eventDelegateHandlerType));
            }

            // otherwise, check with the parent.
            return GetEventHandler(task.GetParentTask(), eventDelegateHandlerType);
        }

        internal static Delegate AddEventHandler(this Task task, Delegate handler) {
            if (handler == null) {
                return null;
            }

            for (var count = 10; count > 0 && task.GetParentTask() == null; count--) {
                Thread.Sleep(10); // yeild for a bit
            }

            lock (Tasks) {
                if (task == null) {
                    NullTaskDelegates.Add(handler);
                } else {
                    if (!Tasks.ContainsKey(task)) {
                        Tasks.Add(task, new List<Delegate>());
                    }
                    Tasks[task].Add(handler);
                }
            }
            return handler;
        }

        internal static void RemoveEventHandler(this Task task, Delegate handler) {
            if (handler != null) {
                lock (Tasks) {
                    if (task == null) {
                        if (NullTaskDelegates.Contains(handler)) {
                            NullTaskDelegates.Remove(handler);
                        }
                    } else {
                        if (Tasks.ContainsKey(task) && Tasks[task].Contains(handler)) {
                            Tasks[task].Remove(handler);
                        }
                    }
                }
            }
        }

        public static void Iterate<TResult>(this TaskCompletionSource<TResult> tcs, IEnumerable<Task> asyncIterator) {
            var enumerator = asyncIterator.GetEnumerator();
            Action<Task> recursiveBody = null;
            recursiveBody = completedTask => {
                if (completedTask != null && completedTask.IsFaulted) {
                    tcs.TrySetException(completedTask.Exception.InnerExceptions);
                    enumerator.Dispose();
                } else if (enumerator.MoveNext()) {
                    enumerator.Current.ContinueWith(recursiveBody, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously);
                } else {
                    enumerator.Dispose();
                }
            };
            recursiveBody(null);
        }

        public static void Ignore(this AggregateException aggregateException, Type type, Action saySomething = null) {
            foreach (var exception in aggregateException.Flatten().InnerExceptions) {
                if (exception.GetType() == type) {
                    if (saySomething != null) {
                        saySomething();
                    }
                    continue;
                }
                throw new ConsoleException("Exception Caught: {0}\r\n    {1}", exception.Message, exception.StackTrace);
            }
        }
    }
}