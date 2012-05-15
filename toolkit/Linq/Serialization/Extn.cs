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

namespace CoApp.Toolkit.Linq.Serialization {
    using System;
    using System.Linq.Expressions;

    public static class Extn<T> {
        public static Expression<Func<T, TProperty>> Create2<TProperty>(Expression<Func<T, TProperty>> expression) {
            return expression;
        }
    }
}