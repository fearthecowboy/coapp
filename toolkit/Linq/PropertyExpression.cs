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

    public class PropertyExpression<T, TProperty> : FilterBase<T, TProperty> {
        private readonly Expression<Func<T, TProperty>> _expression;

        public PropertyExpression(Expression<Func<T, TProperty>> expression) {
            _expression = expression;
        }

        public Func<T, TProperty> ToFunc() {
            return _expression.Compile();
        }

        public string PropertyName {
            get {
                return ((MemberExpression)_expression.Body).Member.Name;
            }
        }

        public static implicit operator PropertyExpression<T, TProperty>(Expression<Func<T, TProperty>> exp) {
            return PropertyExpression<T>.Create(exp);
        }

        public override Expression<Func<T, TProperty>> Expression {
            get {
                return _expression;
            }
        }
    }

    public static class PropertyExpression<T> {
        public static PropertyExpression<T, TProperty> Create<TProperty>(Expression<Func<T, TProperty>> expression) {
            if (expression.Body is MemberExpression) {
                var filter = new PropertyExpression<T, TProperty>(expression);
                return filter;
            }
            return null;
        }

    }
}