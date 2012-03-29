using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoApp.Toolkit.Exceptions;

namespace CoApp.Toolkit.Extensions {
    public static class TaskExtensions {
        /// <summary>
        /// Checks the antecedent task for two conditions:
        ///   IsCancelled : throws an OperationCompletedBeforeResultException
        ///   IsFaulted : throws the first non-aggregate inner exception from the faulted task
        /// 
        ///   Warning: This function will Wait() for the antecendent task to complete.
        /// </summary>
        /// <param name="antecedent">The task to examine results for</param>
        public static void ThrowOnFaultOrCancel(this Task antecedent) {
            if( !antecedent.IsCompleted ) {
                antecedent.Wait();
            }

            if (antecedent.IsCanceled) {
                throw new OperationCompletedBeforeResultException();
            }

            if (antecedent.IsFaulted) {
                throw antecedent.Exception.Unwrap();
            } 
        }

        /// <summary>
        /// Checks the collection of antecedent tasks for two conditions:
        ///   if any are cancelled, throws an OperationCompletedBeforeResultException
        ///   if any are faulted, throws an AggregateException containing all the exceptions from the tasks
        /// 
        ///   Warning: This function will Wait() for the all the antecendent tasks to complete.
        /// </summary>
        /// <param name="antecedent"></param>
        public static void ThrowOnFaultOrCancel(this Task[] antecedents) {
            if( antecedents.Any( each => !each.IsCompleted  )) {
                Task.WaitAll(antecedents);
            }
            
            if( antecedents.Any( each => each.IsCanceled)) {
                throw new OperationCompletedBeforeResultException();
            }

            var exceptions = antecedents.Where(each => each.IsFaulted).SelectMany(each => each.Exception.InnerExceptions);
            if(!exceptions.IsNullOrEmpty()) {
                throw new AggregateException(exceptions);    
            }
        }

        /// <summary>
        /// Checks the collection of antecedent tasks for two conditions:
        ///   if any are cancelled, throws an OperationCompletedBeforeResultException
        ///   if any are faulted, throws an AggregateException containing all the exceptions from the tasks
        /// 
        ///   Warning: This function will Wait() for the all the antecendent tasks to complete.
        /// </summary>
        /// <param name="antecedent"></param>
        public static void ThrowOnFaultOrCancel<T>(this Task<T>[] antecedents) {
            if (antecedents.Any(each => !each.IsCompleted)) {
                Task.WaitAll(antecedents);
            }

            if (antecedents.Any(each => each.IsCanceled)) {
                throw new OperationCompletedBeforeResultException();
            }

            var exceptions = antecedents.Where(each => each.IsFaulted).SelectMany(each => each.Exception.InnerExceptions);
            if (!exceptions.IsNullOrEmpty()) {
                throw new AggregateException(exceptions);
            }
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// This automatically unwraps the antecedent task's result and passes that in directly to the child function.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <typeparam name="TAntecedentResult">The result type of the antecedent Task</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childFunction">The function to run on completion of the antecedent Task</param>
        /// <returns>A Task<TResult> for the child function </returns>
        public static Task<TResult> Continue<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult, TResult> childFunction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childFunction(a.Result);
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// This automatically unwraps the antecedent task's result and passes that in directly to the child function.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <typeparam name="TAntecedentResult">The result type of the antecedent Task</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childTaskFunction">A function returning a task to run on completion of the antecedent Task</param>
        /// <returns>An unwrapped Task<TResult> for the child function.</returns>
        public static Task<TResult> Continue<TResult, TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult, Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childTaskFunction(a.Result);
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// Since the parent task isn't returing a result, the child task simply runs on success of the antecedent
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childFunction">The function to run on completion of the antecedent Task</param>
        /// <returns>A Task<TResult> for the child function </returns>
        public static Task<TResult> Continue<TResult>(this Task antecedent, Func<TResult> childFunction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childFunction();
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// Since the parent task isn't returing a result, the child task simply runs on success of the antecedent
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childTaskFunction">A function returning a task to run on completion of the antecedent Task</param>
        /// <returns>An unwrapped Task<TResult> for the child function.</returns>
        public static Task<TResult> Continue<TResult>(this Task antecedent, Func<Task<TResult>> childTaskFunction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childTaskFunction();
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// This automatically unwraps the antecedent task's result and passes that in directly to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResult">The result type of the antecedent Task</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childAction">The Action to run on completion of the antecedent Task</param>
        /// <returns>A task representing the child action</returns>
        public static Task Continue<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Action<TAntecedentResult> childAction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                childAction(a.Result);
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// 
        /// This automatically unwraps the antecedent task's result and passes that in directly to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResult">The result type of the antecedent Task</typeparam>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childActionTask">a function returning a Task to run on completion of the antecedent Task</param>
        /// <returns>A task representing the child action</returns>
        public static Task Continue<TAntecedentResult>(this Task<TAntecedentResult> antecedent, Func<TAntecedentResult,Task> childActionTask) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childActionTask(a.Result);
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// </summary>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childAction">The Action to run on completion of the antecedent Task</param>
        /// <returns>A task representing the child action</returns>
        public static Task Continue(this Task antecedent, Action childAction) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                childAction();
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a given task.
        /// </summary>
        /// <param name="antecedent">The antecedent Task</param>
        /// <param name="childActionTask">a function returning a Task to run on completion of the antecedent Task</param>
        /// <returns>A task representing the child action</returns>
        public static Task Continue(this Task antecedent, Func<Task> childActionTask) {
            return antecedent.ContinueWith((a) => {
                a.ThrowOnFaultOrCancel();
                return childActionTask();
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// 
        /// This collects the results of the antecedent functions and passes that as a collection to the child function.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <typeparam name="TAntecedentResults">The result type of the antecedent Tasks</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childFunction">The function to run when all the parents are complete</param>
        /// <returns>A task representing the child function</returns>
        public static Task<TResult> Continue<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>, TResult> childFunction) {
            if( antecedents.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childFunction(Enumerable.Empty<TAntecedentResults>()));
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childFunction(aa.Select(each => each.Result));
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// 
        /// This collects the results of the antecedent functions and passes that as a collection to the child function.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <typeparam name="TAntecedentResults">The result type of the antecedent Tasks</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childTaskFunction">The function to run when all the parents are complete</param>
        /// <returns>A task representing the child function</returns>
        public static Task<TResult> Continue<TResult, TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>, Task<TResult>> childTaskFunction) {
            if (antecedents.IsNullOrEmpty()) {
                return childTaskFunction(Enumerable.Empty<TAntecedentResults>());
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childTaskFunction(aa.Select(each => each.Result));
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childFunction">The function to run when all the parents are complete</param>
        /// <returns>A task representing the child function</returns>
        public static Task<TResult> Continue<TResult>(this IEnumerable<Task> antecedents, Func<TResult> childFunction) {
            if (antecedents.IsNullOrEmpty()) {
                return Task.Factory.StartNew(childFunction);
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childFunction();
            }, TaskContinuationOptions.AttachedToParent);
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <typeparam name="TResult">The result type of the child function</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childTaskFunction">The function to run when all the parents are complete, returning a task</param>
        /// <returns>An unwrapped task representing the child function task</returns>
        public static Task<TResult> Continue<TResult>(this IEnumerable<Task> antecedents, Func<Task<TResult>> childTaskFunction) {
            if (antecedents.IsNullOrEmpty()) {
                return childTaskFunction();
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childTaskFunction();
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// 
        /// This collects the results of the antecedent functions and passes that as a collection to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResults">The result type of the antecedent Tasks</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childAction">The function to run when all the parents are complete</param>
        /// <returns>A task representing the child Action</returns>
        public static Task Continue<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Action<IEnumerable<TAntecedentResults>> childAction) {
            if (antecedents.IsNullOrEmpty()) {
                return Task.Factory.StartNew(() => childAction(Enumerable.Empty<TAntecedentResults>()));
            }
            
            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                childAction(aa.Select(each => each.Result));
            }, TaskContinuationOptions.AttachedToParent);
        }


        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// 
        /// This collects the results of the antecedent functions and passes that as a collection to the child Action.
        /// </summary>
        /// <typeparam name="TAntecedentResults">The result type of the antecedent Tasks</typeparam>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childTaskAction">The function to run when all the parents are complete returning a Task</param>
        /// <returns>An unwrapped task representing the child action</returns>
        public static Task Continue<TAntecedentResults>(this IEnumerable<Task<TAntecedentResults>> antecedents, Func<IEnumerable<TAntecedentResults>,Task> childTaskAction) {
            if (antecedents.IsNullOrEmpty()) {
                return childTaskAction(Enumerable.Empty<TAntecedentResults>());
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childTaskAction(aa.Select(each => each.Result));
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }

        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childAction">The function to run when all the parents are complete</param>
        /// <returns>A task representing the child action</returns>
        public static Task Continue(this IEnumerable<Task> antecedents, Action childAction) {
            if (antecedents.IsNullOrEmpty()) {
                return Task.Factory.StartNew(childAction);
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                childAction();
            }, TaskContinuationOptions.AttachedToParent);
        }


        /// <summary>
        /// Performs a continuation (attached to parent) for a collection of given tasks.
        /// </summary>
        /// <param name="antecedents">The colleciton of antecedent tasks. WARNING: This will call ToArray() on the collection, togenerate an immutable collection</param>
        /// <param name="childActionTask">The function to run when all the parents are complete, which returns a Task</param>
        /// <returns>An unwrapped task representing the child action</returns>
        public static Task Continue(this IEnumerable<Task> antecedents, Func<Task> childActionTask) {
            if (antecedents.IsNullOrEmpty()) {
                return childActionTask();
            }

            return Task.Factory.ContinueWhenAll(antecedents.ToArray(), aa => {
                aa.ThrowOnFaultOrCancel();
                return childActionTask();
            }, TaskContinuationOptions.AttachedToParent).Unwrap();
        }
    }
}
