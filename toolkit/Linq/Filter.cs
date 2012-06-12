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
    using System.Linq;
    using System.Linq.Expressions;
    using Collections;
    using Extensions;

    public static class Filter {
        public static UnaryExpression<T> Create<T>(Filter<T> left, UnaryOperation op) {
            return new UnaryExpression<T>(left, op);
        }

        public static BooleanExpression<T> Create<T>(Filter<T> left, Filter<T> right, BooleanOperation op) {
            return new BooleanExpression<T>(left, right, op);
        }

        public static FilterExpression<T, TProperty> Create<T, TProperty>(PropertyExpression<T, TProperty> property, FilterOperation comparison, TProperty value) {
            return new FilterExpression<T, TProperty>(property, comparison, value);
        }

        public static FilterExpression<T, TProperty> Is<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Eq, value);
        }

        public static FilterExpression<T, TProperty> IsNot<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Neq, value);
        }

        public static FilterExpression<T, TProperty> IsLessThan<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Lt, value);
        }

        public static FilterExpression<T, TProperty> IsGreaterThan<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Gt, value);
        }

        public static FilterExpression<T, TProperty> IsLessThanOrEqual<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Lte, value);
        }

        public static FilterExpression<T, TProperty> IsGreaterThanOrEqual<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Gte, value);
        }

        public static FilterExpression<T, TProperty> StringContains<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterExpression<T, TProperty>(property, FilterOperation.Contains, value);
        }

        public static QualifierExpression<T, TProperty> Any<T, TProperty>(this PropertyExpression<T, TProperty> property) {
            return new QualifierExpression<T, TProperty>(property, QualifierOperation.Any);
        }

        public static Expression<Func<T, T>> ThenCompile<T>(this Expression<Func<T, T>> first, Expression<Func<T, T>> second) {
            if (null == first) {
                return second;
            }
            if (null == second) {
                return first;
            }

            
            // var r = System.Linq.Expressions.Expression.Invoke(first, second);
            // var r = System.Linq.Expressions.Expression.Lambda(first,true, second);
            // var r = Expression.(first, true, second);

            // var r = System.Linq.Expressions.Expression.Invoke(second, first);
            // Console.WriteLine(r.ToString());
            // Console.WriteLine(r.Type);
            

            // var pexp = (T p) => Expression.Invoke(second, Expression.Invoke(first, new T[] {p}));
            // var r = p => second.Update(second.Body, );

            return p => second.Compile()(first.Compile()(p));
        }
       

        public static XList<Expression<Func<T, T>>> Then<T>(this Expression<Func<T, T>> first, Expression<Func<T, T>> second) {
            if( first == null && second == null ) {
                return null;
            }

            if (first == null) {
                return new XList<Expression<Func<T, T>>> { second };
            }
            if (second == null) {
                return new XList<Expression<Func<T, T>>> { first};
            }
            return new XList<Expression<Func<T, T>>> { first,second };
        }

        public static XList<Expression<Func<T, T>>> Then<T>(this XList<Expression<Func<T, T>>> first, Expression<Func<T, T>> second) {
            if (null == first) {
                return new XList<Expression<Func<T, T>>> { second };
            }
            if (null != second) {
                first.Add(second);
            }
            return first;
        }

        public static Func<T, T> Compile<T>(this XList<Expression<Func<T, T>>> expressions) {
            if ( expressions.IsNullOrEmpty() ) {
                return (p) => p;
            }

            if( expressions.Count == 1 ) {
                return expressions[0].Compile();
            }
            
            // return expressions.Aggregate<Expression<Func<T, T>>, Expression<Func<T, T>>>(p => expressions[0].Compile(p), (current, v) => current.ThenCompile(v)).Compile()
            var resultExpression = expressions[0];
            foreach( var ex in expressions.Skip(1) ) {
                Expression<Func<T, T>> currentExpression = resultExpression;
                resultExpression = p => ex.Compile()(currentExpression.Compile()(p));
            }
            return resultExpression.Compile();
        }
    }

    public abstract class Filter<T> : ExpressionBase<T, bool> {
        public static UnaryExpression<T> operator !(Filter<T> f) {
            return Filter.Create(f, UnaryOperation.Not);
        }

        public static Filter<T> operator &(Filter<T> f1, Filter<T> f2) {
            if (null == f1) {
                return f2;
            }
            if (null == f2) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanOperation.And);
        }

        public static Filter<T> operator |(Filter<T> f1, Filter<T> f2) {
            if (null == f1) {
                return f2;
            }
            if (null == f2) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanOperation.Or);
        }

        public static Filter<T> operator ^(Filter<T> f1, Filter<T> f2) {
            if (null == f1) {
                return f2;
            }
            if (null == f2) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanOperation.Xor);
        }
    }
}