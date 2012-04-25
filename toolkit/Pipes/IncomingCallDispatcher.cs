//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Pipes {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Collections;
    using Exceptions;
    using Extensions;

    internal class DispatchableMethod {
        internal MethodInfo MethodInfo;
        internal IEnumerable<CachedParameter> Parameters;
    }

    internal enum ParameterType {
        Parseable,
        Array,
        Enumerable,
        Dictionary,
        Nullable,
        String
    }

    internal class CachedParameter {
        internal CachedParameter(ParameterInfo parameterInfo) {
            Name = parameterInfo.Name;

            var t = parameterInfo.ParameterType;
            if (t == typeof (string)) {
                // can be converted to a string I guess.
                ParameterType = ParameterType.String;
                Type = t;
            } else if (t.IsParsable()) {
                ParameterType = ParameterType.Parseable;
                Type = t;
            } else if (t.IsGenericType) {
                var basename = t.Name.Split('`')[0];
                var genericArguments = t.GetGenericArguments();
                // better be IEnumerable,Dictionary or Nullable
                switch (basename) {
                    case "Nullable":
                        ParameterType = ParameterType.Nullable;
                        NullableType = genericArguments[0];
                        break;

                    case "IEnumerable":
                        ParameterType = ParameterType.Enumerable;
                        CollectionType = genericArguments[0];
                        break;

                    case "Dictionary":
                    case "IDictionary":
                    case "EasyDictionary":
                        ParameterType = ParameterType.Dictionary;
                        DictionaryKeyType = genericArguments[0];
                        DictionaryValueType = genericArguments[1];
                        break;

                    default:
                        throw new CoAppException("Unsupported Generic Type");
                }
            } else if (t.IsArray) {
                // an array of soemthing.
                ParameterType = ParameterType.Array;
                CollectionType = t.GetElementType();
            }
        }

        internal string Name { get; set; }
        internal ParameterType ParameterType { get; set; }
        internal Type Type { get; set; }

        internal Type NullableType {
            get {
                return Type;
            }
            set {
                Type = value;
            }
        }

        internal Type CollectionType {
            get {
                return Type;
            }
            set {
                Type = value;
            }
        }

        internal Type DictionaryKeyType {
            get {
                return Type;
            }
            set {
                Type = value;
            }
        }

        internal Type DictionaryValueType { get; set; }

        internal object FromString(UrlEncodedMessage message, string key) {
            switch (ParameterType) {
                case ParameterType.String:
                    return message.GetValueAsString(key);

                case ParameterType.Parseable:
                    return message.GetValueAsPrimitive(key, Type);

                case ParameterType.Nullable:
                    return message.GetValueAsNullable(key, Type);

                case ParameterType.Enumerable:
                    return message.GetValueAsIEnumerable(key, Type);

                case ParameterType.Array:
                    return message.GetValueAsArray(key, Type);

                case ParameterType.Dictionary:
                    return message.GetValueAsDictionary(key, DictionaryKeyType, DictionaryValueType);
            }
            return null;
        }
    }

    public class IncomingCallDispatcher<T> {
        private readonly T _targetObject;
        private readonly EasyDictionary<string, DispatchableMethod> _methodTargets = new EasyDictionary<string, DispatchableMethod>();

        public IncomingCallDispatcher(T target) {
            _targetObject = target;
            foreach (var method in target.GetType().GetMethods()) {
                _methodTargets.Add(method.Name, new DispatchableMethod {
                    MethodInfo = method,
                    Parameters = method.GetParameters().Select(each => new CachedParameter(each))
                });
            }
        }

        /// <summary>
        ///   On the server side, we process messages asynchronously, as we want to make them as parallel as possible.
        /// </summary>
        /// <param name="message"> </param>
        /// <returns> </returns>
        public Task Dispatch(UrlEncodedMessage message) {
            return _methodTargets[message.Command].With(method => Task.Factory.StartNew(() => {
                method.MethodInfo.Invoke(_targetObject, method.Parameters.Select(each => each.FromString(message, each.Name)).ToArray());
            }, TaskCreationOptions.AttachedToParent), () => {
                throw new MissingMethodException("Method '{0}' does not exist in this interface", message.Command);
            });
        }

        /// <summary>
        ///   This dispatches messages synchronously in the client, as we want to ensure that each message is processed in order, and without collision.
        /// </summary>
        /// <param name="message"> </param>
        /// <returns> </returns>
        public bool DispatchSynchronous(UrlEncodedMessage message) {
            _methodTargets[message.Command].With(method => {
                method.MethodInfo.Invoke(_targetObject, method.Parameters.Select(each => each.FromString(message, each.Name)).ToArray());
            }, () => {
                throw new MissingMethodException("Method '{0}' does not exist in this interface", message.Command);
            });

            return !(message.Command.Equals("TaskComplete") || message.Command.Equals("OperationCanceled"));
        }
    }

    public delegate void WriteAsyncMethod(UrlEncodedMessage message);

    public class OutgoingCallDispatcher : DynamicObject {
        private readonly WriteAsyncMethod _writeAsync;

        public OutgoingCallDispatcher(WriteAsyncMethod writeAsync) {
            _writeAsync = writeAsync;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            var msg = new UrlEncodedMessage(binder.Name);
            for (int i = 0; i < binder.CallInfo.ArgumentCount; i++) {
                var arg = args[i];
                var argName = binder.CallInfo.ArgumentNames[i];
                if (arg != null) {
                    var argType = arg.GetType();
                    if (argType == typeof (string) || argType.IsParsable()) {
                        msg.Add(argName, arg.ToString());
                    } else if (argType.IsDictionary()) {
                        msg.AddDictionary(argName, (IDictionary)arg);
                    } else if (argType.IsArray) {
                        msg.AddCollection(argName, ((object[])arg).Select(each => each.ToString()));
                    } else if (argType.IsIEnumerable()) {
                        msg.AddCollection(argName, ((IEnumerable)arg));
                    }
                }
            }
            _writeAsync(msg);

            // our incoming calls appear as return type 'Task' in the interface
            // but that's just so that the client app can treat the interface as async easily.
            // on the servicing side, we just return null, and don't actually expect that the
            // call has a significant return value.
            result = null;
            return true;
        }
    }
}