using CoApp.Toolkit.Extensions;

namespace CoApp.Toolkit.Query
{
    public class FilterComparison<T, TProperty> : Filter<T>
    {
        public PropRef<T, TProperty> Property { get; set; }
        public FilterOp Comparison { get; set; }
        public TProperty Value { get; set; }



        public override bool Invoke(T item) {
            // we need dynamic or the operators don't like us
            dynamic propertyVal = Property.ToFunc()(item);
            dynamic val = Value;
            var ret = false;
            switch (Comparison) {
                case FilterOp.LT:
                    ret = propertyVal < Value;
                    break;
                case FilterOp.LTE:
                    ret = propertyVal <= Value;
                    break;
                case FilterOp.Contains:
                    
                    ret = ((string)propertyVal).Contains((string)val);
                    break;
                case FilterOp.GT:
                    ret = propertyVal > Value;
                    break;
                case FilterOp.GTE:
                    ret = propertyVal >= Value;
                    break;
                case FilterOp.EQ:
                    if (propertyVal is string) {
                        
                        ret = StringExtensions.NewIsWildcardMatch(propertyVal, (string)val);
                    } else {
                        ret = propertyVal == Value;
                    }
                    break;
            }

            return ret;
        }


        public static bool TryParse(string input, out FilterComparison<T, TProperty> obj) {
            obj = null;
            return false;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    /*
    public static class FilterComparison
    {
        public static FilterComparison<T, TProperty> Create<T, TProperty>(PropRef<T, TProperty> property,
                                                                          FilterOp comparison, TProperty value)
        {
            return new FilterComparison<T, TProperty> {Comparison = comparison, Property = property, Value = value};
        }
    }*/
}
