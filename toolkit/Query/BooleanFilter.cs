namespace CoApp.Toolkit.Query
{
    public class BooleanFilter<T> : Filter<T>
    {
        public IInvokable<T> Left { get; set; }
        public IInvokable<T> Right { get; set; }
        public BooleanFilterOperator Operator { get; set; }


        public override bool Invoke(T item) {
            var left = Left.Invoke(item);
            var right = Right.Invoke(item);
            
            var ret = false;

            switch(Operator) {
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
