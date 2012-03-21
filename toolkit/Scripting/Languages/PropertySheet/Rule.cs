//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Scripting.Languages.PropertySheet {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Text;
    using Extensions;

    public interface IPropertyValue : IEnumerable<string> {
        SourceLocation SourceLocation { get; }
        string Value { get; }
        bool IsSingleValue { get; }
        bool HasMultipleValues { get; }
        string SourceString { get; }
    }

     public class ActualPropertyValue : IPropertyValue {
         private IEnumerable<string> _values;
         internal IEnumerable<string> Values { get { return _values.Select(each => ParentPropertySheet.ResolveMacros(each)); } set { _values = value; } }
         public SourceLocation SourceLocation { get; internal set; }

         private string _value;
         public string Value { get { return ParentPropertySheet.ResolveMacros(_value); } internal set { _value = value; } }
         public bool IsSingleValue { get; internal set; }
         public bool HasMultipleValues { get; internal set; }
         public string SourceString { get; internal set; }
         public IEnumerator<string> GetEnumerator() {
             return Values.GetEnumerator();
         }

         IEnumerator IEnumerable.GetEnumerator() {
             return GetEnumerator();
         }

         internal PropertySheet ParentPropertySheet { get; set; }
     }

    public class PropertyValue : IPropertyValue {
        private static readonly IEnumerable<string> NoCollection = "".SingleItemAsEnumerable();
        internal readonly string _collectionName;

        internal readonly NewRuleProperty ParentRuleProperty;
        private readonly List<string> _values = new List<string>();

        public SourceLocation SourceLocation { get; internal set; }
        internal string Label { get; private set; }

        internal PropertyValue(NewRuleProperty parent, string label, string collectionName = null) {
            ParentRuleProperty = parent;
            Label = label;
            _collectionName = collectionName;
        }
       
        internal IPropertyValue Actual(string label) {
            if( string.IsNullOrEmpty(_collectionName) ) {
                return Label == label ? this : null; // this should shortcut nicely when there is no collection.
            }

            var values = CollectionValues.Where(each => ParentPropertySheet.ResolveMacros(Label, each) == label).SelectMany(each => _values.Select(value => ParentPropertySheet.ResolveMacros(value, each))).ToArray();
            if (values.Length > 0)
                return new ActualPropertyValue {
                    SourceLocation = SourceLocation,
                    IsSingleValue = values.Length == 1,
                    HasMultipleValues = values.Length > 1,
                    SourceString = "",
                    Value = values[0],
                    Values = values,
                    ParentPropertySheet = ParentPropertySheet
                };

            return null;
        }

        private PropertySheet ParentPropertySheet {
            get {
                return ParentRuleProperty.ParentRule.ParentPropertySheet;
            }
        }

        private IEnumerable<object> CollectionValues {
            get {
                return string.IsNullOrEmpty( _collectionName )
                    ? NoCollection  // this makes it so there is a 1-element collection for things that don't have a collection. 
                    : (ParentPropertySheet.GetCollection != null ? ParentPropertySheet.GetCollection(_collectionName)
                    : Enumerable.Empty<object>()); // this is so that when there is supposed to be a collection, but nobody is listening, we get an empty set back.
            }
        }

        public IEnumerator<string> GetEnumerator() {
            return CollectionValues.SelectMany(each => _values.Select(value => ParentPropertySheet.ResolveMacros(value, each))).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public string Value {
            get {
                return ParentPropertySheet.ResolveMacros(this.FirstOrDefault());
            } set {
                _values.Clear();
                _values.Add(value);
            }
        }

        public IEnumerable<string> Values {
            get {
                return this.Select(each => ParentPropertySheet.ResolveMacros(each));
            }
        }

        public int Count {
            get {
                return (CollectionValues.Count()*_values.Count);
            }
        }

        public bool IsSingleValue {
            get {
                return Count == 1;
            }
        }

        public bool HasMultipleValues {
            get {
                return Count > 1;
            }
        }

        internal void Add( string value ) {
            _values.Add(value);
        }

        public string SourceString {
            get {
                if( string.IsNullOrEmpty(_collectionName)) {
                    if( string.IsNullOrEmpty(Label)) {
                        if (_values.Count == 1) {
                            return PropertySheet.QuoteIfNeeded(_values[0]) + ";\r\n";
                        } 
                        if( _values.Count > 1 ) {
                            return _values.Aggregate("{", (current, v) => current + "\r\n        " + PropertySheet.QuoteIfNeeded(v) +",") + "\r\n    };\r\n\r\n";
                        }
                        if( _values.Count == 0 ) {
                            return @"""""; // WARNING--THIS SHOULD NOT BE HAPPENING. EMPTY VALUE LISTS ARE SIGN THAT YOU HAVE NOT PAID ENOUGH ATTENTION";
                        }
                    } 
                    if( _values.Count == 1) {
                        return "{0} = {1};\r\n".format(PropertySheet.QuoteIfNeeded(Label), PropertySheet.QuoteIfNeeded(_values[0]));
                    }
                    if (_values.Count > 1) {
                        return "{0} = {1}".format(PropertySheet.QuoteIfNeeded(Label), _values.Aggregate("{", (current, v) => current + "\r\n        " + PropertySheet.QuoteIfNeeded(v)+",") + "\r\n    };\r\n\r\n");
                    }
                    if (_values.Count == 0) {
                        return @"{0} = """"; // WARNING--THIS SHOULD NOT BE HAPPENING. EMPTY VALUE LISTS ARE SIGN THAT YOU HAVE NOT PAID ENOUGH ATTENTION".format( PropertySheet.QuoteIfNeeded(Label) );
                    }
                } 
                if( string.IsNullOrEmpty(Label)) {
                    return _values.Aggregate("{", (current, v) => current + ("\r\n        " + PropertySheet.QuoteIfNeeded(_collectionName) + " => " + PropertySheet.QuoteIfNeeded(v) + ";")) + "\r\n    };\r\n\r\n";
                }
                return _values.Aggregate("{", (current, v) => current + ("\r\n        " + PropertySheet.QuoteIfNeeded(_collectionName) + " => " + PropertySheet.QuoteIfNeeded(Label) + " = " + PropertySheet.QuoteIfNeeded(v) + ";")) + "\r\n    };\r\n\r\n";
            }
        }

        internal IEnumerable<string> Labels {
            get {
                return CollectionValues.Select(each => ParentPropertySheet.ResolveMacros(Label, each));
            }
        }
    }

    /// <summary>
    ///   A RuleProperty represents a single property name, with potentially multiple property-labels, each label can have 1 or more values.
    /// </summary>
    public class NewRuleProperty : DynamicObject {
        internal readonly Rule ParentRule;
        private readonly List<PropertyValue> _propertyValues = new List<PropertyValue>();
        public SourceLocation SourceLocation { get; internal set; }
        public string Name { get; set; }

        /// <summary>
        /// RuleProperty object must be created by the Rule.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        internal NewRuleProperty(Rule parent, string name ) {
            ParentRule = parent;
            Name = name;
        }

        internal string SourceString {
            get {
                return _propertyValues.Aggregate("", (current, v) => current + "    {0} : {1}".format(Name, v.SourceString));
            }
        }

        public override string ToString() {
            
            var items = Labels.Select(each => new { label = each, values = this[each] ?? Enumerable.Empty<string>()});
            var result = items.Where(item => item.values.Any()).Aggregate("", (current1, item) => current1 + (item.values.Count() == 1 ? 
                PropertySheet.QuoteIfNeeded(Name) + PropertySheet.QuoteIfNeeded(item.label) + " = " + PropertySheet.QuoteIfNeeded(item.values.First()) + ";\r\n" :
                PropertySheet.QuoteIfNeeded(Name) + PropertySheet.QuoteIfNeeded(item.label) + " = {\r\n" + item.values.Aggregate("", (current, each) => current + "        " + PropertySheet.QuoteIfNeeded(each) + ",\r\n") + "    };\r\n"));

            return result;
        }

        public string Value { get {
            var v = this[string.Empty];
            return v == null ? null : this[string.Empty].Value;
        }}

        public IEnumerable<string> Values {
            get {
                return this[string.Empty] ?? Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> Labels {
            get {
                return _propertyValues.SelectMany(each => each.Labels).Distinct();
            }
        }

        public bool HasValues {
            get {
                return _propertyValues.Count > 0;
            }
        }

        public bool HasValue {
            get {
                return _propertyValues.Count > 0;
            }
        }

        public IPropertyValue this[string label] {
            get {
                // looks up the property collection
                return (from propertyValue in _propertyValues let actual = propertyValue.Actual(label) where actual != null select actual).FirstOrDefault();
            }
        }
        
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var primary = ParentRule.ParentPropertySheet.PreferDashedNames ? binder.Name.CamelCaseToDashed() : binder.Name;
            var secondary = ParentRule.ParentPropertySheet.PreferDashedNames ? binder.Name : binder.Name.CamelCaseToDashed();

            result =  GetPropertyValue(!_propertyValues.Where(each => each.Label == primary).Any() && _propertyValues.Where(each => each.Label == secondary).Any() ? secondary : primary);
            return true;
        }


        /// <summary>
        /// Gets Or Adds a PropertyValue with the given label and collection.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        internal PropertyValue GetPropertyValue(string label, string collection = null) {
            var result = _propertyValues.Where(each => each.Label == label).FirstOrDefault();
            if( result == null ) {
                _propertyValues.Add( result = new PropertyValue(this, label, string.IsNullOrEmpty(collection) ? null : collection)); 
            }
            return result;
        }
    }

    public class Rule : DynamicObject {
        internal readonly PropertySheet ParentPropertySheet;
        private readonly List<NewRuleProperty> _properties = new List<NewRuleProperty>();
        public string Class { get; set; }
        public string Id { get; set; }
        public string Name{ get; set; }
        private string _parameter;
        public string Parameter { get {
            return ParentPropertySheet.ResolveMacros(_parameter);
        } set {
            _parameter = value;
        } }
        public SourceLocation SourceLocation;

        /// <summary>
        /// Rules must be created by the property sheet only.
        /// </summary>
        /// <param name="propertySheet"></param>
        internal Rule(PropertySheet propertySheet) {
            Name = "*";
            ParentPropertySheet = propertySheet;
        }

        public NewRuleProperty this[string propertyName] {
            get {
                return _properties.Where(each => each.Name == propertyName).FirstOrDefault();
            }
        }

        public IEnumerable<string> PropertyNames {
            get {
                return _properties.Select(each => each.Name);
            }
        }

        public static string CreateSelectorString( string name, string parameter, string @class , string id) {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(name) && !name.Equals("*")) {
                result.Append(name);
            }

            if (!string.IsNullOrEmpty(parameter)) {
                result.Append("[");
                result.Append(parameter);
                result.Append("]");
            }

            if (!string.IsNullOrEmpty(@class)) {
                result.Append(".");
                result.Append(@class);
            }

            if (!string.IsNullOrEmpty(id)) {
                result.Append("#");
                result.Append(id);
            }

            return result.ToString();
        }

        public string FullSelector {
            get {
                return CreateSelectorString(Name, _parameter, Class, Id);
            }
        }

        public override string ToString() {
            return HasProperties ? _properties.Aggregate(FullSelector + " {\r\n", (current, each) => current + (each.ToString())) + "};\r\n\r\n" : string.Empty;
        }

        public bool HasProperty(string propertyName) {
            return this[propertyName] != null;
        }

        public bool HasProperties { 
            get {
                return _properties.Where( each => each.HasValues).Any();
            }
        }

        internal NewRuleProperty GetRuleProperty(string name) {
            var property = this[name];
            if (property == null) {
                property = new NewRuleProperty(this, name);
                _properties.Add(property);
            }
            return property;
        }

        public string SourceString {
            get {
                return HasProperties ? _properties.Aggregate(FullSelector + " {\r\n", (current, each) => current + (each.SourceString)) + "};\r\n\r\n" : string.Empty;
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var primary = ParentPropertySheet.PreferDashedNames ? binder.Name.CamelCaseToDashed() : binder.Name;
            var secondary = ParentPropertySheet.PreferDashedNames ? binder.Name : binder.Name.CamelCaseToDashed();

            result = GetRuleProperty(this[primary] == null && this[secondary] != null ? secondary : primary);
            return true;
        }
    }
}