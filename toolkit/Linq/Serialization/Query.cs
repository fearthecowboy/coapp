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

namespace CoApp.Toolkit.Linq.Serialization {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    /// Explicit interface implementation of IQueryable
    /// </summary>
    /// <typeparam name="T"> </typeparam>
    public class Query<T> : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable, IOrderedQueryable<T>, IOrderedQueryable {
        // // where T : new()
        //QueryProvider provider;
        private IQueryProvider provider;
        private Expression expression;

        //public Query(QueryProvider provider)
        public Query()
            : this(new T[1].AsQueryable().Provider) {
        }

        public Query(IQueryProvider provider) {
            this.provider = provider;
            expression =
                Expression.Constant(this); //this function implicitly calls the ToString method in Debug
        }

        public Query(IQueryProvider provider, Expression expression) {
            if (provider == null) {
                throw new ArgumentNullException("provider");
            }
            if (expression == null) {
                throw new ArgumentNullException("expression");
            }
            if (!(typeof (IQueryable<T>).IsAssignableFrom(expression.Type) || typeof (IEnumerable<T>).IsAssignableFrom(expression.Type))) {
                throw new ArgumentOutOfRangeException("expression");
            }
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression {
            get {
                return expression;
            }
        }

        Type IQueryable.ElementType {
            get {
                return typeof (T);
            }
        }

        IQueryProvider IQueryable.Provider {
            get {
                return provider;
            }
        }

        /// <summary>
        ///   on the call to any of the System.Linq extension methods on IEnumerable{T}, this method will get called. <see
        ///    cref="http://msdn.microsoft.com/en-us/library/system.linq.enumerable.aspx" />
        /// </summary>
        /// <returns> </returns>
        public IEnumerator<T> GetEnumerator() {
            return ((IEnumerable<T>)provider.Execute(expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)provider.Execute(expression)).GetEnumerator();
        }

        /// <summary>
        ///   in Debug, this is called implicitly.
        /// </summary>
        /// <returns> </returns>
        public override string ToString() {
            return GetType().FullName; // this.provider.GetQueryText(this.expression);
        }
    }
}