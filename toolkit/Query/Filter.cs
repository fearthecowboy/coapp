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
    public class Filter {
        public static UnaryFilter<T> Create<T>(IInvokable<T> left, UnaryFilterOperator op) {
            return new UnaryFilter<T> {Left = left, Operator = op};
        }

        public static BooleanFilter<T> Create<T>(IInvokable<T> left, IInvokable<T> right, BooleanFilterOperator op) {
            return new BooleanFilter<T> {Left = left, Right = right, Operator = op};
        }

        public static FilterComparison<T, TProperty> Create<T, TProperty>(PropRef<T, TProperty> property,
            FilterOp comparison, TProperty value) {
            return new FilterComparison<T, TProperty> {Comparison = comparison, Property = property, Value = value};
        }
    }

    public abstract class Filter<T> : IInvokable<T> {
        public static UnaryFilter<T> operator !(Filter<T> f) {
            return Filter.Create(f, UnaryFilterOperator.Not);
        }

        public static BooleanFilter<T> operator &(Filter<T> f1, Filter<T> f2) {
            return Filter.Create(f1, f2, BooleanFilterOperator.And);
        }

        public static BooleanFilter<T> operator |(Filter<T> f1, Filter<T> f2) {
            return Filter.Create(f1, f2, BooleanFilterOperator.Or);
        }

        public static BooleanFilter<T> operator ^(Filter<T> f1, Filter<T> f2) {
            return Filter.Create(f1, f2, BooleanFilterOperator.Xor);
        }

        public abstract bool Invoke(T item);
    }
}