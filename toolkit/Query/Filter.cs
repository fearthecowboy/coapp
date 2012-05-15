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
        public static UnaryFilter<T> Create<T>(Filter<T> left, UnaryFilterOperator op)
        {
            return new UnaryFilter<T>(left, op);
        }

        public static BooleanFilter<T> Create<T>(Filter<T> left, Filter<T> right, BooleanFilterOperator op)
        {
            return new BooleanFilter<T>(left, right, op);
        }

        public static FilterComparison<T, TProperty> Create<T, TProperty>(PropertyExpression<T, TProperty> property,
            FilterOp comparison, TProperty value)
        {
            return new FilterComparison<T, TProperty>(property, comparison, value);
        }
    }

    public abstract class Filter<T>: FilterBase<T, bool> {
        public static UnaryFilter<T> operator !(Filter<T> f) {
            return Filter.Create(f, UnaryFilterOperator.Not);
        }

        public static BooleanFilter<T> operator &(Filter<T> f1, Filter<T> f2)
        {
            return Filter.Create(f1, f2, BooleanFilterOperator.And);
        }

        public static BooleanFilter<T> operator |(Filter<T> f1, Filter<T> f2)
        {
            return Filter.Create(f1, f2, BooleanFilterOperator.Or);
        }

        public static BooleanFilter<T> operator ^(Filter<T> f1, Filter<T> f2)
        {
            return Filter.Create(f1, f2, BooleanFilterOperator.Xor);
        }

       
    }

    
}