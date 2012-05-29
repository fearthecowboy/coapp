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
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Collections;
    using Extensions;
    using Logging;

    internal class DispatchableMethod {
        internal MethodInfo MethodInfo;
        internal IDictionary<string,PersistableInfo> Parameters;
    }

    public class IncomingCallDispatcher<T> {
        private readonly T _targetObject;
        private readonly XDictionary<string, DispatchableMethod> _methodTargets;

        public IncomingCallDispatcher(T target) {
            _targetObject = target;
            _methodTargets = target.GetType().GetMethods().ToXDictionary(method => method.Name, method => new DispatchableMethod {
                MethodInfo = method,
                Parameters = method.GetParameters().ToXDictionary(each => each.Name, each => each.ParameterType.GetPersistableInfo())
            });
        }

        /// <summary>
        ///   On the server side, we process messages asynchronously, as we want to make them as parallel as possible.
        /// </summary>
        /// <param name="message"> </param>
        /// <returns> </returns>
        public Task Dispatch(UrlEncodedMessage message) {
            return _methodTargets[message.Command].With(method => Task.Factory.StartNew(() => {
                try {
                    method.MethodInfo.Invoke(_targetObject, method.Parameters.Keys.Select(each => message.GetValue(each, method.Parameters[each].Type)).ToArray());
                } catch( Exception e ) {
                    Logger.Error(e);
                }
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
            return _methodTargets[message.Command].With(method => {
                try {
                    method.MethodInfo.Invoke(_targetObject, method.Parameters.Keys.Select(each => message.GetValue(each, method.Parameters[each].Type)).ToArray());
                }
                catch (TargetInvocationException exception) {
                    if (exception.InnerException != null) {
                        if (exception.InnerException is RestartingException) {
                            return false;
                        }
                        throw exception.InnerException;
                    }
                }
                catch (AggregateException exception) {
                    var c = exception.Unwrap();
                    Logger.Error(c);
                    throw c;
                }
                catch (Exception exception) {
                    Logger.Error(exception);
                    throw exception;
                }

                return !(message.Command.Equals("TaskComplete") || message.Command.Equals("OperationCanceled") || message.Command.Equals("Restarting"));
            }, () => {
                throw new MissingMethodException("Method '{0}' does not exist in this interface", message.Command);
            });
        }
    }

    public delegate void WriteAsyncMethod(UrlEncodedMessage message);

    public class OutgoingCallDispatcher : DynamicObject {
        private readonly XDictionary<string, DispatchableMethod> _methodTargets;

        private readonly WriteAsyncMethod _writeAsync;

        public OutgoingCallDispatcher(Type targetInterface, WriteAsyncMethod writeAsync) {
            _writeAsync = writeAsync;

            _methodTargets = targetInterface.GetMethods().ToXDictionary(method => method.Name, method => new DispatchableMethod {
                MethodInfo = method,
                Parameters = method.GetParameters().ToXDictionary(each => each.Name, each => each.ParameterType.GetPersistableInfo())
            });
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            var msg = new UrlEncodedMessage(binder.Name);
            var method = _methodTargets[binder.Name];
            for (int i = 0; i < binder.CallInfo.ArgumentCount; i++) {
                if (args[i] != null) {
                    // var targetType = method.Parameters[binder.CallInfo.ArgumentNames[i]].Type;
                    // var sourceType = args[i].GetType();
                    msg.Add(binder.CallInfo.ArgumentNames[i], args[i], args[i].GetType());
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
    public class RestartingException : Exception {

    }

}

