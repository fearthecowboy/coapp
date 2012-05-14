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

    public class PropRef<T, TProperty> {
        private readonly Expression<Func<T, TProperty>> _expression;

        public PropRef(Expression<Func<T, TProperty>> expression) {
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

        public static bool TryParse(string input, out PropRef<T, TProperty> obj) {
            obj = null;
            return false;
        }

        public override string ToString() {
            return base.ToString();
        }
    }

    public static class PropRef<T> {
        public static PropRef<T, TProperty> Create<TProperty>(Expression<Func<T, TProperty>> expression) {
            if (expression.Body is MemberExpression) {
                var filter = new PropRef<T, TProperty>(expression);
                return filter;
            }
            return null;
        }
    }
}