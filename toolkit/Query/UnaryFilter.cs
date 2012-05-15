namespace CoApp.Toolkit.Query
{
    using System;
    using System.Linq.Expressions;
    using SLE = System.Linq.Expressions;
    public class UnaryFilter<T> : Filter<T>
    {
        public UnaryFilter(Filter<T> left, UnaryFilterOperator op)
        {
            Left = left;
            Operator = op;
        }
        private Filter<T> Left { get; set; }
        private UnaryFilterOperator Operator { get; set; }

        public override Expression<Func<T, bool>> Expression {
            get {
                ParameterExpression paramExpr = SLE.Expression.Parameter(typeof (T), "arg");
                Expression e = null;
                
                switch (Operator)
                {
                    case UnaryFilterOperator.Not:
                        e = SLE.Expression.Not(SLE.Expression.Invoke(Left, paramExpr));
                        break;

                }



                return SLE.Expression.Lambda<Func<T, bool>>(e, paramExpr);
                
            }
        }
    }
    
}
