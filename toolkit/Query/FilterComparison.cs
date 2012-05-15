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
    using System.Linq;
    using System.Linq.Expressions;
    using Extensions;
    using SLE = System.Linq.Expressions;
    

    public class FilterComparison<T, TProperty> : Filter<T> {

        public FilterComparison(PropertyExpression<T, TProperty> property, FilterOp comparison, TProperty value)
        {
            Property = property;
            Comparison = comparison;
            Value = value;
        }

        private PropertyExpression<T, TProperty> Property { get; set; }
        private FilterOp Comparison { get; set; }
        private TProperty Value { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                ParameterExpression p = SLE.Expression.Parameter(typeof (T));
                var leftInvoke = SLE.Expression.Invoke(Property, p);
                Expression e = null;

                Expression value = SLE.Expression.Constant(Value, typeof(TProperty));
                var stringType = typeof (string);
                var containsMethod = stringType.GetMethod("Contains");
                
                

                var stringExtensionsType = typeof (StringExtensions);
                var newIsWildcardMatchMethod = stringExtensionsType.GetMethod("NewIsWildcardMatch");

                switch (Comparison)
                {
                    case FilterOp.LT:
                        e = SLE.Expression.LessThan(leftInvoke, value);
                        break;
                    case FilterOp.LTE:
                        e = SLE.Expression.LessThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOp.GT:
                        e = SLE.Expression.GreaterThan(leftInvoke, value);
                        break;
                    case FilterOp.GTE:
                        e = SLE.Expression.GreaterThanOrEqual(leftInvoke, value);
                        break;
                    case FilterOp.Contains:
                        if (Value is string)
                        {
                            e = SLE.Expression.Call(leftInvoke, containsMethod, value);
                        }
                        break;
                    case FilterOp.EQ:
                      
                        if (Value is string)
                        {
                            e = SLE.Expression.Call(newIsWildcardMatchMethod, leftInvoke, value, SLE.Expression.Constant(false), SLE.Expression.Constant(null, stringType));
                        }
                        else
                        {
                            e = SLE.Expression.Equal(leftInvoke, value);
                        }
                        break;
                }

                return SLE.Expression.Lambda<Func<T, bool>>(e, p);
            }
        }
    }
}