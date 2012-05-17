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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Extensions;

    public class FilterExpression<T, TProperty> : Filter<T> {
        public FilterExpression(PropertyExpression<T, TProperty> property, FilterOperation comparison, TProperty value) {
            Property = property;
            Comparison = comparison;
            Value = value;
        }
      
        private PropertyExpression<T, TProperty> Property { get; set; }
        private FilterOperation Comparison { get; set; }
        private TProperty Value { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                var p = System.Linq.Expressions.Expression.Parameter(typeof (T));
                var leftInvoke = System.Linq.Expressions.Expression.Invoke(Property, p);
                Expression expression = null;
                Expression value = System.Linq.Expressions.Expression.Constant(Value, typeof (TProperty));

                switch (Comparison) {
                    case FilterOperation.Lt:
                        expression = System.Linq.Expressions.Expression.LessThan(leftInvoke, value);
                        break;
                    case FilterOperation.Lte:
                        expression = System.Linq.Expressions.Expression.LessThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOperation.Gt:
                        expression = System.Linq.Expressions.Expression.GreaterThan(leftInvoke, value);
                        break;
                    case FilterOperation.Gte:
                        expression = System.Linq.Expressions.Expression.GreaterThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOperation.Contains:
                        if (typeof(TProperty) == typeof(string)) {
                            if (Value is string) {
                                expression = System.Linq.Expressions.Expression.Call(leftInvoke, typeof (string).GetMethod("Contains"), value);
                            }
                        } else {
                            if (typeof(TProperty).IsIEnumerable()) {
                                // collection.contains()
                                expression = System.Linq.Expressions.Expression.Call(typeof (IEnumerable).GetMethod("Contains").MakeGenericMethod(typeof (TProperty).GetGenericArguments()[0]),leftInvoke, value);
                            }
                        }
                        break;
                    case FilterOperation.Eq:
                        if (Value is string) {
                            expression = System.Linq.Expressions.Expression.Call(typeof(StringExtensions).GetMethod("NewIsWildcardMatch"), leftInvoke, value, System.Linq.Expressions.Expression.Constant(false), System.Linq.Expressions.Expression.Constant(null, typeof(string)));
                        } else {
                            expression = System.Linq.Expressions.Expression.Equal(leftInvoke, value);
                        }
                        break;
                   
                }
                
                return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(expression, p);
            }
        }
    }

    public class QualifierExpression<T, TProperty> : Filter<T> {
        public QualifierExpression(PropertyExpression<T, TProperty> property, QualifierOperation operation) {
            Property = property;
            Operation = operation;
        }

        private PropertyExpression<T, TProperty> Property { get; set; }
        private QualifierOperation Operation { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                var p = System.Linq.Expressions.Expression.Parameter(typeof (T));
                var leftInvoke = System.Linq.Expressions.Expression.Invoke(Property, p);
                Expression e = null;


                switch (Operation) {
                    case QualifierOperation.Any:
                        if (typeof(TProperty).IsIEnumerable()) {
                            e = System.Linq.Expressions.Expression.Call(typeof(IEnumerable).GetMethod("Any").MakeGenericMethod(typeof(TProperty).GetGenericArguments()[0]),leftInvoke);
                        }
                        break;
                    case QualifierOperation.IsNullOrEmpty:
                        if (typeof(TProperty) == typeof(string)) {
                            e = System.Linq.Expressions.Expression.Call(typeof(String).GetMethod("IsNullOrEmpty"), leftInvoke);
                        } else if (typeof(TProperty).IsIEnumerable()) {
                            e = System.Linq.Expressions.Expression.Call(typeof(CollectionExtensions).GetMethod("IsNullOrEmpty").MakeGenericMethod(typeof(TProperty).GetGenericArguments()[0]), leftInvoke);
                        }
                        break;
                }

                return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(e, p);
            }
        }
    }
}