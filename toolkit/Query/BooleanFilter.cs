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
    public class BooleanFilter<T> : Filter<T> {
        public IInvokable<T> Left { get; set; }
        public IInvokable<T> Right { get; set; }
        public BooleanFilterOperator Operator { get; set; }

        public override bool Invoke(T item) {
            var left = Left.Invoke(item);
            var right = Right.Invoke(item);

            var ret = false;

            switch (Operator) {
                case BooleanFilterOperator.And:
                    ret = left && right;
                    break;
                case BooleanFilterOperator.Or:
                    ret = left || right;
                    break;
                case BooleanFilterOperator.Xor:
                    ret = left ^ right;
                    break;
            }

            return ret;
        }
    }
}