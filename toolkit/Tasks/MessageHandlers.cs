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
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Threading.Tasks;

    /// <summary>
    /// Base class for all CoApp Task-based quasi-event classes
    /// </summary>
    /// <remarks></remarks>
    public class MessageHandlers {
        /// <summary>
        /// Gets the parameter types of a Delegate
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        private static Type[] GetDelegateParameterTypes(Type d) {
            if (d.BaseType != typeof(MulticastDelegate))
                throw new ApplicationException("Not a delegate.");

            MethodInfo invoke = d.GetMethod("Invoke");
            if (invoke == null)
                throw new ApplicationException("Not a delegate.");

            ParameterInfo[] parameters = invoke.GetParameters();
            Type[] typeParameters = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                typeParameters[i] = parameters[i].ParameterType;
            }
            return typeParameters;
        }

        /// <summary>
        /// Gets the Return type of a delegate
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        private static Type GetDelegateReturnType(Type d) {
            if (d.BaseType != typeof(MulticastDelegate))
                throw new ApplicationException("Not a delegate.");

            MethodInfo invoke = d.GetMethod("Invoke");
            if (invoke == null)
                throw new ApplicationException("Not a delegate.");

            return invoke.ReturnType;
        }

        private object GetDelegate(Task task, FieldInfo field ) {
            var value = field.GetValue(this);
            if (value != null)
                return value;
            try {
                var parentTask = task.GetParentTask();
                if (parentTask != null) {
                    var parentTaskMessageHandler = parentTask.GetMessageHandler(GetType());
                    if (parentTaskMessageHandler != null) {
                        return parentTaskMessageHandler.GetDelegate(parentTask, field);
                    }
                }
            } catch {
                
            }
            return null;
        }

        /// <summary>
        /// Creates do-nothing delegates for events not listened to.
        /// </summary>
        /// <param name="checkParent">if set, this will check to see if there is a parent task with a delegate set that it can copy.</param>
        /// <remarks></remarks>
        public void SetMissingDelegates(bool checkParent = true, MessageHandlers inheritFrom = null) {
            foreach (var field in GetType().GetFields().Where(f => f.FieldType.BaseType == typeof(MulticastDelegate))) {
                // if we have a delegate, we don't need to do anything.
                if (field.GetValue(this) != null) {
                    continue;
                } 

                // if we are inheriting from a parent instance, check that first
                if (inheritFrom != null && GetType().IsAssignableFrom(inheritFrom.GetType())) {
                    var inheritedDelegate = field.GetValue(inheritFrom);
                    if (inheritedDelegate != null) {
                        // found one, copy it.
                        field.SetValue(this, inheritedDelegate);
                        continue;
                    }
                }

                // otherwise, check the parent task for it's value.
                if (checkParent) {
                    object dlg;
                    if ((dlg = GetDelegate(CoTask.CurrentTask, field)) != null) {
                        // found one, copy it.
                        field.SetValue(this, dlg);
                        continue;
                    }
                }

                Type delegateReturnType = GetDelegateReturnType(field.FieldType);
                Type[] delegateParameterTypes = GetDelegateParameterTypes(field.FieldType);

                var dynamicMethod = new DynamicMethod(string.Empty, delegateReturnType, delegateParameterTypes);
                ILGenerator il = dynamicMethod.GetILGenerator();

                if (delegateReturnType.FullName != "System.Void") {
                    if (delegateReturnType.IsValueType) {
                        il.Emit(OpCodes.Ldc_I4, 0);
                    }
                    else {
                        il.Emit(OpCodes.Ldnull);
                    }
                }
                il.Emit(OpCodes.Ret);

                field.SetValue(this, dynamicMethod.CreateDelegate(field.FieldType));
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks></remarks>
    public class MessageHandlers<T> : MessageHandlers where T : MessageHandlers, new() {
        /// <summary>
        /// 
        /// </summary>
        public static T Default { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        /// <remarks></remarks>
        static MessageHandlers() {
            Default = new T();
            Default.SetMissingDelegates(false);
        }

        /// <summary>
        /// Gets the delegate to invoke against.
        /// </summary>
        /// <remarks></remarks>
        public static T Invoke {
            get {
                var currentTask = CoTask.CurrentTask;
                return currentTask == null ? Default : (currentTask.GetMessageHandler(typeof(T)) as T) ?? Default;
            }
        }

        public static T InvokeParent {
            get {
                var currentTask = CoTask.CurrentTask;
                if (currentTask == null) {
                    return Default;
                }

                var parentTask = currentTask.GetParentTask();
                if (parentTask == null) {
                    return Default;
                } 

                return parentTask.GetMessageHandler(typeof(T)) as T ?? Default;
            }
        }

        public void Register() {
            if (CoTask.CurrentTask == null && this as T != null) {
                Default = this as T;
            }
            CoTask.CurrentTask.AddMessageHandler(this);
        }

        public void Unregister() {
            if (CoTask.CurrentTask == null && this as T != null) {
                Default = this as T;
            }
            CoTask.CurrentTask.RemoveMessageHandler(this);
        }

        public T Extend(MessageHandlers inheritFrom = null) {
            SetMissingDelegates(true, inheritFrom);
            return this as T;
        }
    }
}