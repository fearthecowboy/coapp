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

        public static Expression<Func<T, T>> Then<T>(this Expression<Func<T, T>> first, Expression<Func<T, T>> second) {
            if (null == first) {
                return second;
            }
            if (null == second) {
                return first;
            }
            return p => second.Compile()(first.Compile()(p));
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