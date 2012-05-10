namespace CoApp.Toolkit.Query
{
    public class UnaryFilter<T> : Filter<T>
    {
        public IInvokable<T> Left { get; set; }
        public UnaryFilterOperator Operator { get; set; }


        public override bool Invoke(T item) {
            var left = Left.Invoke(item);

            //the operator is always Not
            
            return !left;
        }


        public static bool TryParse(string input, out UnaryFilter<T> obj) {
            obj = null;
            return false;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
    
}
