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
    using System.Linq;
    using System.Threading.Tasks;
    using Exceptions;

    public static class ExceptionExtensions {
        /// <summary>
        ///   This method will unwrap an AggregateException as far as it can be to get either a single Exception or a single AggregateException that has a flattened collection of Exceptions The unwrap process removes CoApp canceled exceptions, OperationCanceledExceptions and TaskCanceledExceptions and returns an OperationCanceledException if there is no valid exceptions left. Exception Exception Exception Exception Exception.
        /// </summary>
        /// <param name="exception"> </param>
        /// <returns> </returns>
        public static Exception Unwrap(this Exception exception) {
            var aggregate = exception as AggregateException;
            if (aggregate != null) {
                var allActualExceptions = (from e in aggregate.Flatten().InnerExceptions
                    let coappexception = e as CoAppException
                    where (coappexception == null || !coappexception.IsCanceled)
                        && e as OperationCanceledException == null
                            && e as TaskCanceledException == null
                    select e).ToArray();

                switch (allActualExceptions.Length) {
                    case 0:
                        return new OperationCanceledException("All exceptions have been cancelled");
                    case 1:
                        return allActualExceptions.FirstOrDefault();
                    case 2:
                        return new AggregateException(allActualExceptions);
                }
            }
            return exception;
        }
    }
}