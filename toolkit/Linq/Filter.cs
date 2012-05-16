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
    public static class Filter {
        public static UnaryFilter<T> Create<T>(Filter<T> left, UnaryFilterOperator op) {
            return new UnaryFilter<T>(left, op);
        }

        public static BooleanFilter<T> Create<T>(Filter<T> left, Filter<T> right, BooleanFilterOperator op) {
            return new BooleanFilter<T>(left, right, op);
        }

        public static FilterComparison<T, TProperty> Create<T, TProperty>(PropertyExpression<T, TProperty> property, FilterOp comparison, TProperty value) {
            return new FilterComparison<T, TProperty>(property, comparison, value);
        }

        public static FilterComparison<T, TProperty> Is<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.EQ, value);
        }

        public static FilterComparison<T, TProperty> IsLessThan<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.LT, value);
        }

        public static FilterComparison<T, TProperty> IsGreaterThan<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.GT, value);
        }
        public static FilterComparison<T, TProperty> IsLessThanOrEqual<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.LTE, value);
        }

        public static FilterComparison<T, TProperty> IsGreaterThanOrEqual<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.GTE, value);
        }

        public static FilterComparison<T, TProperty> Contains<T, TProperty>(this PropertyExpression<T, TProperty> property, TProperty value) {
            return new FilterComparison<T, TProperty>(property, FilterOp.Contains, value);
        }
    }

    

    public abstract class Filter<T> : FilterBase<T, bool> {
        public static UnaryFilter<T> operator !(Filter<T> f) {
            return Filter.Create(f, UnaryFilterOperator.Not);
        }

        public static Filter<T> operator &(Filter<T> f1, Filter<T> f2) {
            if( null == f1 ) {
                return f2;
            }
            if( null == f2 ) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanFilterOperator.And);
        }

        public static Filter<T> operator |(Filter<T> f1, Filter<T> f2) {
            if (null == f1) {
                return f2;
            }
            if (null == f2) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanFilterOperator.Or);
        }

        public static Filter<T> operator ^(Filter<T> f1, Filter<T> f2) {
            if (null == f1) {
                return f2;
            }
            if (null == f2) {
                return f1;
            }
            return Filter.Create(f1, f2, BooleanFilterOperator.Xor);
        }
    }
}