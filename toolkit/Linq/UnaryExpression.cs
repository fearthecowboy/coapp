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

    public class UnaryExpression<T> : Filter<T> {
        public UnaryExpression(Filter<T> left, UnaryOperation op) {
            Left = left;
            Operator = op;
        }

        private Filter<T> Left { get; set; }
        private UnaryOperation Operator { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                var paramExpr = System.Linq.Expressions.Expression.Parameter(typeof (T), "arg");
                Expression e = null;

                switch (Operator) {
                    case UnaryOperation.Not:
                        e = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Invoke(Left, paramExpr));
                        break;
                }

                return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(e, paramExpr);
            }
        }
    }
}