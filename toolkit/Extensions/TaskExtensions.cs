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

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Tasks;

    public static class TaskExtensions {
        /// <summary>
        ///   Checks the antecedent task for two conditions: IsCanceled : throws an OperationCompletedBeforeResultException IsFaulted : throws the first non-aggregate inner exception from the faulted task Warning: This function will Wait() for the antecendent task to complete.
        /// </summary>
        /// <param name="antecedent"> The task to examine results for </param>
        public static void RethrowWhenFaulted(this Task antecedent) {
            if (!antecedent.IsCompleted) {
                antecedent.Wait();
            }

            if (antecedent.IsFaulted) {
                throw antecedent.Exception.Unwrap();
            }
        }

        /// <summary>
        ///   Checks the collection of antecedent tasks for two conditions: if any are canceled, throws an OperationCompletedBeforeResultException if any are faulted, throws an AggregateException containing all the exceptions from the tasks Warning: This function will Wait() for the all the antecendent tasks to complete.
        /// </summary>
        /// <param name="antecedents"> </param>
        public static void RethrowWhenFaulted(this Task[] antecedents) {
            if (antecedents.Any(each => !each.IsCompleted)) {
                Task.WaitAll(antecedents);
            }

            var exceptions = antecedents.Where(each => each.IsFaulted).SelectMany(each => each.Exception.InnerExceptions);
            if (!exceptions.IsNullOrEmpty()) {
                throw new AggregateException(exceptions);
            }
        }

        public static void RethrowWhenFaulted(this IEnumerable<Task> antecedents) {
            antecedents.ToArrayEvenIfNull().RethrowWhenFaulted();
        }

        /// <summary>
        ///   Checks the collection of antecedent tasks for two conditions: if any are cancelled, throws an OperationCompletedBeforeResultException if any are faulted, throws an AggregateException containing all the exceptions from the tasks Warning: This function will Wait() for the all the antecendent tasks to complete.
        /// </summary>
        /// <param name="antecedent"> </param>
        public static void RethrowWhenFaulted<T>(this Task<T>[] antecedents) {
            if (antecedents.Any(each => !each.IsCompleted)) {
                Task.WaitAll(antecedents);
            }

            var exceptions = antecedents.Where(each => each.IsFaulted).SelectMany(each => each.Exception.InnerExceptions);
            if (!exceptions.IsNullOrEmpty()) {
                throw new AggregateException(exceptions);
            }
        }

        public static void RethrowWhenFaulted<T>(this IEnumerable<Task<T>> antecedents) {
            antecedents.ToArrayEvenIfNull().RethrowWhenFaulted();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. This automatically unwraps the antecedent task's result and passes that in directly to the child function.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <typeparam name="TAntecedentResult"> The result type of the antecedent Task </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childFunction"> The function to run on completion of the antecedent Task </param>
        /// <returns> A Task (TResult)for the child function </returns>
        public static Task<TResult> Continue<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult, TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(a.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueOnFail<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Exception, TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueOnCanceled<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueAlways<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Task<TAntecedentResult>, TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(a), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. This automatically unwraps the antecedent task's result and passes that in directly to the child function.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <typeparam name="TAntecedentResult"> The result type of the antecedent Task </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childTaskFunction"> A function returning a task to run on completion of the antecedent Task </param>
        /// <returns> An unwrapped Task (TResult)for the child function. </returns>
        public static Task<TResult> Continue<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(a.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueOnFail<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Exception, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueOnCanceled<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueAlways<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Task<TAntecedentResult>, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(a), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. Since the parent task isn't returing a result, the child task simply runs on success of the antecedent
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childFunction"> The function to run on completion of the antecedent Task </param>
        /// <returns> A Task (TResult)for the child function </returns>
        public static Task<TResult> Continue<TResult>(this Task antecedent, Func<TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueOnFail<TResult>(this Task antecedent, Func<Exception, TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueOnCanceled<TResult>(this Task antecedent, Func<TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent);
        }

        public static Task<TResult> ContinueAlways<TResult>(this Task antecedent, Func<Task, TResult> childFunction) {
            return antecedent.ContinueWith(a => childFunction(a), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. Since the parent task isn't returing a result, the child task simply runs on success of the antecedent
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childTaskFunction"> A function returning a task to run on completion of the antecedent Task </param>
        /// <returns> An unwrapped Task (TResult)for the child function. </returns>
        public static Task<TResult> Continue<TResult>(this Task antecedent, Func<Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueOnFail<TResult>(this Task antecedent, Func<Exception, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueOnCanceled<TResult>(this Task antecedent, Func<Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task<TResult> ContinueAlways<TResult>(this Task antecedent, Func<Task, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith(a => childTaskFunction(a), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. This automatically unwraps the antecedent task's result and passes that in directly to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResult"> The result type of the antecedent Task </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childAction"> The Action to run on completion of the antecedent Task </param>
        /// <returns> A task representing the child action </returns>
        public static Task Continue<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Action<TAntecedentResult> childAction) {
            return antecedent.ContinueWith(a => childAction(a.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueOnFail<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Action<Exception> childAction) {
            return antecedent.ContinueWith(a => childAction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueOnCanceled<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Action childAction) {
            return antecedent.ContinueWith(a => childAction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueAlways<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Action<Task<TAntecedentResult>> childAction) {
            return antecedent.ContinueWith(a => childAction(a), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task. This automatically unwraps the antecedent task's result and passes that in directly to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResult"> The result type of the antecedent Task </typeparam>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childActionTask"> a function returning a Task to run on completion of the antecedent Task </param>
        /// <returns> A task representing the child action </returns>
        public static Task Continue<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult, Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(a.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueOnFail<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Exception, Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueOnCanceled<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueAlways<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<Task<TAntecedentResult>, Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(a), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task.
        /// </summary>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childAction"> The Action to run on completion of the antecedent Task </param>
        /// <returns> A task representing the child action </returns>
        public static Task Continue(this Task antecedent, Action childAction) {
            return antecedent.ContinueWith(a => childAction(), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueOnFail(this Task antecedent, Action<Exception> childAction) {
            return antecedent.ContinueWith(a => childAction(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueOnCanceled(this Task antecedent, Action childAction) {
            return antecedent.ContinueWith(a => childAction(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent);
        }

        public static Task ContinueAlways(this Task antecedent, Action<Task> childAction) {
            return antecedent.ContinueWith(a => childAction(a), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a given task.
        /// </summary>
        /// <param name="antecedent"> The antecedent Task </param>
        /// <param name="childActionTask"> a function returning a Task to run on completion of the antecedent Task </param>
        /// <returns> A task representing the child action </returns>
        public static Task Continue(this Task antecedent, Func<Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueOnFail(this Task antecedent, Func<Exception, Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(a.Exception.Unwrap()), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueOnCanceled(this Task antecedent, Func<Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(), TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        public static Task ContinueAlways(this Task antecedent, Func<Task, Task> childActionTask) {
            return antecedent.ContinueWith(a => childActionTask(a), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        private static TSource[] ToArrayEvenIfNull<TSource>(this IEnumerable<TSource> source) {
            return source == null ? new TSource[0] : source.ToArray();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks. This collects the results of the antecedent functions and passes that as a collection to the child function.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <typeparam name="TAntecedentResults"> The result type of the antecedent Tasks </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childFunction"> The function to run when all the parents are complete </param>
        /// <returns> A task representing the child function </returns>
        public static Task<TResult> Continue<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>, TResult> childFunction) {
            // if the collection is empty, just start the new task.
            var antes = antecedents.ToArrayEvenIfNull();

            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<TAntecedentResults>()));
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childFunction(aa.Select(each => each.Result)));
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueOnFail<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<Exception, TResult> childFunction) {
            // if the collection is empty, there will be no fails.
            var antes = antecedents.ToArrayEvenIfNull();

            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childFunction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueOnCanceled<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<TResult> childFunction) {
            // if the collection is empty, there will be no cancellations.
            var antes = antecedents.ToArrayEvenIfNull();

            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childFunction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueAlways<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<Task<TAntecedentResults>>, TResult> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task<TAntecedentResults>>()));
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks. This collects the results of the antecedent functions and passes that as a collection to the child function.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <typeparam name="TAntecedentResults"> The result type of the antecedent Tasks </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childTaskFunction"> The function to run when all the parents are complete </param>
        /// <returns> A task representing the child function </returns>
        public static Task<TResult> Continue<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>, Task<TResult>> childTaskFunction) {
            // if the collection is empty, just start the new task.
            var antes = antecedents.ToArrayEvenIfNull();

            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childTaskFunction(Enumerable.Empty<TAntecedentResults>())).Unwrap();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childTaskFunction(aa.Select(each => each.Result)));
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueOnFail<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<Exception, Task<TResult>> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childFunction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueOnCanceled<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<Task<TResult>> childFunction) {
            // if the collection is empty, there will be no cancellations.
            var antes = antecedents.ToArrayEvenIfNull();

            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childFunction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueAlways<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<Task<TAntecedentResults>>, Task<TResult>> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task<TAntecedentResults>>())).Unwrap();
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childFunction"> The function to run when all the parents are complete </param>
        /// <returns> A task representing the child function </returns>
        public static Task<TResult> Continue<TResult>(this IEnumerable<Task> antecedents, Func<TResult> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(childFunction);
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childFunction());
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueOnFail<TResult>(this IEnumerable<Task> antecedents, Func<Exception, TResult> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childFunction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueOnCanceled<TResult>(this IEnumerable<Task> antecedents, Func<TResult> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<TResult>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childFunction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task<TResult> ContinueAlways<TResult>(this IEnumerable<Task> antecedents, Func<IEnumerable<Task>, TResult> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task>()));
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <typeparam name="TResult"> The result type of the child function </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childTaskFunction"> The function to run when all the parents are complete, returning a task </param>
        /// <returns> An unwrapped task representing the child function task </returns>
        public static Task<TResult> Continue<TResult>(this IEnumerable<Task> antecedents, Func<Task<TResult>> childTaskFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(childTaskFunction).Unwrap();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childTaskFunction());
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueOnFail<TResult>(this IEnumerable<Task> antecedents, Func<Exception, Task<TResult>> childTaskFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childTaskFunction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueOnCanceled<TResult>(this IEnumerable<Task> antecedents, Func<Task<TResult>> childTaskFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return default(TResult).AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task<TResult>>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childTaskFunction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task<TResult> ContinueAlways<TResult>(this IEnumerable<Task> antecedents, Func<IEnumerable<Task>, Task<TResult>> childTaskFunction) {
            var antes = antecedents.ToArrayEvenIfNull();
            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childTaskFunction(Enumerable.Empty<Task>())).Unwrap();
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childTaskFunction(aa), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks. This collects the results of the antecedent functions and passes that as a collection to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResults"> The result type of the antecedent Tasks </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childAction"> The function to run when all the parents are complete </param>
        /// <returns> A task representing the child Action </returns>
        public static Task Continue<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Action<IEnumerable<TAntecedentResults>> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childAction(Enumerable.Empty<TAntecedentResults>()));
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        childAction(aa.Select(each => each.Result));
                        tcs.SetResult(null);
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueOnFail<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Action<Exception> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            childAction(exception);
                            tcs.SetResult(null);
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueOnCanceled<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Action childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            childAction();
                            tcs.SetResult(null);
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueAlways<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Action<IEnumerable<Task<TAntecedentResults>>> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task<TAntecedentResults>>()));
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks. This collects the results of the antecedent functions and passes that as a collection to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResults"> The result type of the antecedent Tasks </typeparam>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childTaskAction"> The function to run when all the parents are complete returning a Task </param>
        /// <returns> An unwrapped task representing the child action </returns>
        public static Task Continue<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>, Task> childTaskAction) {
            var antes = antecedents.ToArrayEvenIfNull();
            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childTaskAction(Enumerable.Empty<TAntecedentResults>())).Unwrap();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childTaskAction(aa.Select(each => each.Result)));
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueOnFail<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<Exception, Task> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childAction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueOnCanceled<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<Task> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childAction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueAlways<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<Task<TAntecedentResults>>, Task> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task<TAntecedentResults>>())).Unwrap();
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childAction"> The function to run when all the parents are complete </param>
        /// <returns> A task representing the child action </returns>
        public static Task Continue(this IEnumerable<Task> antecedents, Action childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childAction());
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        childAction();
                        tcs.SetResult(null);
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueOnFail(this IEnumerable<Task> antecedents, Action<Exception> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            childAction(exception);
                            tcs.SetResult(null);
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueOnCanceled(this IEnumerable<Task> antecedents, Action childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<object>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            childAction();
                            tcs.SetResult(null);
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task;
        }

        public static Task ContinueAlways(this IEnumerable<Task> antecedents, Action<IEnumerable<Task>> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();
            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task>()));
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        ///   Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <param name="antecedents"> The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection </param>
        /// <param name="childActionTask"> The function to run when all the parents are complete, which returns a Task </param>
        /// <returns> An unwrapped task representing the child action </returns>
        public static Task Continue(this IEnumerable<Task> antecedents, Func<Task> childActionTask) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childActionTask()).Unwrap();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled || each.IsFaulted)) {
                        // there was a failure somewhere.
                        // don't do anything else.
                        tcs.SetCanceled();
                    }

                    try {
                        tcs.SetResult(childActionTask());
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueOnFail(this IEnumerable<Task> antecedents, Func<Exception, Task> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no fails.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsFaulted)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        var exception = new AggregateException(aa.Where(each => each.IsFaulted).Select(each => each.Exception));
                        try {
                            tcs.SetResult(childAction(exception));
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueOnCanceled(this IEnumerable<Task> antecedents, Func<Task> childAction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, there will be no cancellations.
            if (antes.IsNullOrEmpty()) {
                return "".AsCanceledTask();
            }

            var tcs = new TaskCompletionSource<Task>();
            Task.Factory.ContinueWhenAll(
                antes, aa => {
                    if (aa.Any(each => each.IsCanceled)) {
                        // if we had even a single fault, 
                        // pass the fault to the exception handler.
                        try {
                            tcs.SetResult(childAction());
                        } catch (Exception e) {
                            tcs.SetException(e);
                        }
                    } else {
                        tcs.SetCanceled();
                    }
                }, TaskContinuationOptions.AttachedToParent);

            return tcs.Task.Unwrap();
        }

        public static Task ContinueAlways(this IEnumerable<Task> antecedents, Func<IEnumerable<Task>, Task> childFunction) {
            var antes = antecedents.ToArrayEvenIfNull();

            // if the collection is empty, just start the new task.
            if (antes.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<Task>())).Unwrap();
            }

            return Task.Factory.ContinueWhenAll(antes, aa => childFunction(aa), TaskContinuationOptions.AttachedToParent).Unwrap();
        }
    }
}