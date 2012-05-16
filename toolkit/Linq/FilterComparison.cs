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
    using Extensions;

    public class FilterComparison<T, TProperty> : Filter<T> {
        public FilterComparison(PropertyExpression<T, TProperty> property, FilterOp comparison, TProperty value) {
            Property = property;
            Comparison = comparison;
            Value = value;
        }

        private PropertyExpression<T, TProperty> Property { get; set; }
        private FilterOp Comparison { get; set; }
        private TProperty Value { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                var p = System.Linq.Expressions.Expression.Parameter(typeof (T));
                var leftInvoke = System.Linq.Expressions.Expression.Invoke(Property, p);
                Expression e = null;

                Expression value = System.Linq.Expressions.Expression.Constant(Value, typeof (TProperty));
                var stringType = typeof (string);
                var containsMethod = stringType.GetMethod("Contains");

                var stringExtensionsType = typeof (StringExtensions);
                var newIsWildcardMatchMethod = stringExtensionsType.GetMethod("NewIsWildcardMatch");

                switch (Comparison) {
                    case FilterOp.LT:
                        e = System.Linq.Expressions.Expression.LessThan(leftInvoke, value);
                        break;
                    case FilterOp.LTE:
                        e = System.Linq.Expressions.Expression.LessThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOp.GT:
                        e = System.Linq.Expressions.Expression.GreaterThan(leftInvoke, value);
                        break;
                    case FilterOp.GTE:
                        e = System.Linq.Expressions.Expression.GreaterThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOp.Contains:
                        if (Value is string) {
                            e = System.Linq.Expressions.Expression.Call(leftInvoke, containsMethod, value);
                        }
                        break;
                    case FilterOp.EQ:
                        if (Value is string) {
                            e = System.Linq.Expressions.Expression.Call(newIsWildcardMatchMethod, leftInvoke, value, System.Linq.Expressions.Expression.Constant(false), System.Linq.Expressions.Expression.Constant(null, stringType));
                        } else {
                            e = System.Linq.Expressions.Expression.Equal(leftInvoke, value);
                        }
                        break;
                }

                return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(e, p);
            }
        }
    }
}