using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Query
{
    using System.Linq.Expressions;

    public abstract class FilterBase<T, TOut>
    {
        public abstract Expression<Func<T, TOut>> Expression { get; }

        public static implicit operator Expression<Func<T, TOut>>(FilterBase<T, TOut> filter) {
            return filter.Expression;
        }

        public static implicit operator FilterBase<T, TOut>(Expression<Func<T,TOut>> exp) {
            throw new NotImplementedException("We can't implement this right now");
        }
    }
}
