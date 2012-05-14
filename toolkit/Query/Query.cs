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
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    public class Query<T, TProperty> : Query<T> {
        public PropRef<T, TProperty> SortProperty { get; set; }
        public ListSortDirection SortDirection { get; set; }

        public override IEnumerable<T> Invoke(IEnumerable<T> input) {
            var output = base.Invoke(input);
            if (SortProperty != null) {
                //we can sort!
                if (SortDirection == ListSortDirection.Ascending) {
                    output = output.OrderBy(i => SortProperty.ToFunc()(i));
                } else {
                    output = output.OrderByDescending(i => SortProperty.ToFunc()(i));
                }
            }

            return output;
        }

        public static bool TryParse(string input, out Query<T, TProperty> obj) {
            obj = null;
            return false;
        }

        public override string ToString() {
            return base.ToString();
        }
    }

    public class Query<T> {
        public IInvokable<T> Filter { get; set; }

        public virtual IEnumerable<T> Invoke(IEnumerable<T> input) {
            return input.Where(i => Filter.Invoke(i));
        }

        public static bool TryParse(string input, out Query<T> obj) {
            obj = null;
            return false;
        }

        public override string ToString() {
            return base.ToString();
        }
    }

    public static class Query {
        public static Query<T> Create<T>(IInvokable<T> filter) {
            return new Query<T> {Filter = filter};
        }

        public static Query<T, TProperty> Create<T, TProperty>(IInvokable<T> filter, PropRef<T, TProperty> sortProperty, ListSortDirection sortDirection = ListSortDirection.Ascending) {
            return new Query<T, TProperty> {Filter = filter, SortDirection = sortDirection, SortProperty = sortProperty};
        }
    }
}