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

namespace CoApp.Toolkit.Query {
    using System;
    using System.Linq.Expressions;

    using SLE = System.Linq.Expressions;

    public class BooleanFilter<T> : Filter<T> {

        public BooleanFilter(Filter<T> left, Filter<T> right, BooleanFilterOperator op)
        {
            Left = left;
            Right = right;
            Operator = op;
        }

        private Filter<T> Left { get; set; }
        private Filter<T> Right { get; set; }
        private BooleanFilterOperator Operator { get; set; }


        public override Expression<Func<T, bool>> Expression {
            get {

                ParameterExpression p = SLE.Expression.Parameter(typeof (T), "arg");

                var invokeLeft = SLE.Expression.Invoke(Left, p);
                var invokeRight = SLE.Expression.Invoke(Right, p);
                BinaryExpression bin = null;
                switch (Operator) {
                    case BooleanFilterOperator.And:
                        bin = SLE.Expression.And(invokeLeft, invokeRight);
                        break;
                    case BooleanFilterOperator.Or:
                        bin = SLE.Expression.Or(invokeLeft, invokeRight);
                        break;
                    case BooleanFilterOperator.Xor:
                        bin = SLE.Expression.ExclusiveOr(invokeLeft, invokeRight);
                        break;
                }

                if (bin == null)
                    throw new Exception("This should never happen");

                return SLE.Expression.Lambda<Func<T, bool>>(bin, p);
            }
        }
    }
}