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

namespace CoApp.Toolkit.Linq {
    using System;
    using System.Linq.Expressions;

    public abstract class FilterBase<T, TOut> {
        public abstract Expression<Func<T, TOut>> Expression { get; }

        public static implicit operator Expression<Func<T, TOut>>(FilterBase<T, TOut> filter) {
            return filter != null ? filter.Expression: null;
        }

        public static implicit operator FilterBase<T, TOut>(Expression<Func<T, TOut>> exp) {
            throw new NotImplementedException("We can't implement this right now");
        }
    }
}