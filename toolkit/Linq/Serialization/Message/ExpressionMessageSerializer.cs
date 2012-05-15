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

namespace CoApp.Toolkit.Query.Serialization.Message {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml.Linq;
    using Pipes;
#if NOT_FINISHED
    public class ExpressionMessageSerializer {
        /// <summary>
        ///   generate XML attributes for these primitive Types.
        /// </summary>
        private static readonly Type[] primitiveTypes = new[] { typeof(string), typeof(int), typeof(bool), typeof(ExpressionType) };

        private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();
        private TypeResolver resolver;
        public List<CustomExpressionXmlConverter> Converters { get; private set; }

        public ExpressionMessageSerializer(TypeResolver resolver, IEnumerable<CustomExpressionXmlConverter> converters = null) {
            this.resolver = resolver;
            if (converters != null) {
                Converters = new List<CustomExpressionXmlConverter>(converters);
            }
            else {
                Converters = new List<CustomExpressionXmlConverter>();
            }
        }

        public ExpressionMessageSerializer() {
            resolver = new TypeResolver(null, null);
            Converters = new List<CustomExpressionXmlConverter>();
        }

        /*
         * SERIALIZATION 
         */

        public XElement Serialize(Expression e) {
            if (e.NodeType != ExpressionType.Lambda) {
                e = Evaluator.PartialEval(e); //TODO: decide should we call PartialEval or not at all?
            }
            return GenerateXmlFromExpressionCore(e);
        }

        /// <summary>
        ///   Uses first applicable custom serializer, then returns. Does not attempt to use all custom serializers.
        /// </summary>
        /// <param name="e"> </param>
        /// <param name="result"> </param>
        /// <returns> </returns>
        private bool TryCustomSerializers(Expression e, out XElement result) {
            result = null;
            var i = 0;
            while (i < Converters.Count) {
                if (Converters[i].TrySerialize(e, out result)) {
                    return true;
                }
                i++;
            }
            return false;
        }

        private object GenerateXmlFromProperty(Type propType, string propName, object value) {
            // if( propType.IsParsable()) {
            //  return GenerateXmlFromParseable(propName, value);
            // }

            if (primitiveTypes.Contains(propType)) {
                return GenerateXmlFromPrimitive(propName, value);
            }

            if (propType.Equals(typeof(object))) //expected: caller invokes with value == a ConstantExpression.Value
            {
                return GenerateXmlFromObject(propName, value);
            }
            if (typeof(Expression).IsAssignableFrom(propType)) {
                return GenerateXmlFromExpression(propName, value as Expression);
            }
            if (value is MethodInfo || propType.Equals(typeof(MethodInfo))) {
                return GenerateXmlFromMethodInfo(propName, value as MethodInfo);
            }
            if (value is PropertyInfo || propType.Equals(typeof(PropertyInfo))) {
                return GenerateXmlFromPropertyInfo(propName, value as PropertyInfo);
            }
            if (value is FieldInfo || propType.Equals(typeof(FieldInfo))) {
                return GenerateXmlFromFieldInfo(propName, value as FieldInfo);
            }
            if (value is ConstructorInfo || propType.Equals(typeof(ConstructorInfo))) {
                return GenerateXmlFromConstructorInfo(propName, value as ConstructorInfo);
            }
            if (propType.Equals(typeof(Type))) {
                return GenerateXmlFromType(propName, value as Type);
            }
            if (IsIEnumerableOf<Expression>(propType)) {
                return GenerateXmlFromExpressionList(propName, AsIEnumerableOf<Expression>(value));
            }
            if (IsIEnumerableOf<MemberInfo>(propType)) {
                return GenerateXmlFromMemberInfoList(propName, AsIEnumerableOf<MemberInfo>(value));
            }
            if (IsIEnumerableOf<ElementInit>(propType)) {
                return GenerateXmlFromElementInitList(propName, AsIEnumerableOf<ElementInit>(value));
            }
            if (IsIEnumerableOf<MemberBinding>(propType)) {
                return GenerateXmlFromBindingList(propName, AsIEnumerableOf<MemberBinding>(value));
            }
            throw new NotSupportedException(propName);
        }

        /// <summary>
        ///   Called from somewhere on call stack... from ConstantExpression.Value Modified since original code for this method was incorrectly getting the value as .ToString() for non-primitive types, which ExpressionXmlSerializer was unable to later parse back into a value (ExpressionXmlSerializer.ParseConstantFromElement).
        /// </summary>
        /// <param name="propName"> </param>
        /// <param name="value"> ConstantExpression.Value </param>
        /// <returns> </returns>
        private object GenerateXmlFromObject(string propName, object value) {
            var mscorlib = typeof(string).Assembly;
            object result = null;
            if (value is Type) {
                result = GenerateXmlFromTypeCore((Type)value);
            }
            else if (mscorlib.GetTypes().Any(t => t == value.GetType())) {
                result = value.ToString();
            }
            //else
            //    throw new ArgumentException(string.Format("Unable to generate XML for value of Type '{0}'.\nType is not recognized.", value.GetType().FullName));
            else {
                result = value.ToString();
            }
            return new XElement(propName,
                result);
        }

        /// <summary>
        ///   For use with ConstantExpression.Value
        /// </summary>
        /// <param name="xName"> </param>
        /// <param name="instance"> </param>
        /// <returns> </returns>
        private object GenerateXmlFromKnownTypes(string xName, object instance, Type knownType) {
            string xml;
            XElement xelement;
            dynamic something = instance;

            if (typeof(IQueryable).IsAssignableFrom(instance.GetType())) {
                if (typeof(Query<>).MakeGenericType(knownType).IsAssignableFrom(instance.GetType())) {
                    return instance.ToString();
                }
                something = LinqHelper.CastToGenericEnumerable((IQueryable)instance, knownType);
                something = Enumerable.ToArray(something);
            }
            Type instanceType = something.GetType();
            var serializer = new DataContractSerializer(instanceType, resolver.knownTypes);

            using (var ms = new MemoryStream()) {
                serializer.WriteObject(ms, something);
                ms.Position = 0;
                var reader = new StreamReader(ms, Encoding.UTF8);
                xml = reader.ReadToEnd();
                xelement = new XElement(xName, xml);
                return xelement;
            }
        }

        private bool IsIEnumerableOf<T>(Type propType) {
            if (!propType.IsGenericType) {
                return false;
            }
            var typeArgs = propType.GetGenericArguments();
            if (typeArgs.Length != 1) {
                return false;
            }
            if (!typeof(T).IsAssignableFrom(typeArgs[0])) {
                return false;
            }
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(propType)) {
                return false;
            }
            return true;
        }

        private bool IsIEnumerableOf(Type enumerableType, Type elementType) {
            if (!enumerableType.IsGenericType) {
                return false;
            }
            var typeArgs = enumerableType.GetGenericArguments();
            if (typeArgs.Length != 1) {
                return false;
            }
            if (!elementType.IsAssignableFrom(typeArgs[0])) {
                return false;
            }
            if (!typeof(IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(enumerableType)) {
                return false;
            }
            return true;
        }

        private IEnumerable<T> AsIEnumerableOf<T>(object value) {
            if (value == null) {
                return null;
            }
            return (value as IEnumerable).Cast<T>();
        }

        private object GenerateXmlFromElementInitList(string propName, IEnumerable<ElementInit> initializers) {
            if (initializers == null) {
                initializers = new ElementInit[] { };
            }
            return new XElement(propName,
                from elementInit in initializers
                select GenerateXmlFromElementInitializer(elementInit));
        }

        private object GenerateXmlFromElementInitializer(ElementInit elementInit) {
            return new XElement("ElementInit",
                GenerateXmlFromMethodInfo("AddMethod", elementInit.AddMethod),
                GenerateXmlFromExpressionList("Arguments", elementInit.Arguments));
        }

        private object GenerateXmlFromExpressionList(string propName, IEnumerable<Expression> expressions) {
            var result = new XElement(propName,
                from expression in expressions
                select GenerateXmlFromExpressionCore(expression));
            return result;
        }

        private object GenerateXmlFromMemberInfoList(string propName, IEnumerable<MemberInfo> members) {
            if (members == null) {
                members = new MemberInfo[] { };
            }
            return new XElement(propName,
                from member in members
                select GenerateXmlFromProperty(member.GetType(), "Info", member));
        }

        private object GenerateXmlFromBindingList(string propName, IEnumerable<MemberBinding> bindings) {
            if (bindings == null) {
                bindings = new MemberBinding[] { };
            }
            return new XElement(propName,
                from binding in bindings
                select GenerateXmlFromBinding(binding));
        }

        private object GenerateXmlFromBinding(MemberBinding binding) {
            switch (binding.BindingType) {
                case MemberBindingType.Assignment:
                    return GenerateXmlFromAssignment(binding as MemberAssignment);
                case MemberBindingType.ListBinding:
                    return GenerateXmlFromListBinding(binding as MemberListBinding);
                case MemberBindingType.MemberBinding:
                    return GenerateXmlFromMemberBinding(binding as MemberMemberBinding);
                default:
                    throw new NotSupportedException(string.Format("Binding type {0} not supported.", binding.BindingType));
            }
        }

        private object GenerateXmlFromMemberBinding(MemberMemberBinding memberMemberBinding) {
            return new XElement("MemberMemberBinding",
                GenerateXmlFromProperty(memberMemberBinding.Member.GetType(), "Member", memberMemberBinding.Member),
                GenerateXmlFromBindingList("Bindings", memberMemberBinding.Bindings));
        }

        private object GenerateXmlFromListBinding(MemberListBinding memberListBinding) {
            return new XElement("MemberListBinding",
                GenerateXmlFromProperty(memberListBinding.Member.GetType(), "Member", memberListBinding.Member),
                GenerateXmlFromProperty(memberListBinding.Initializers.GetType(), "Initializers", memberListBinding.Initializers));
        }

        private object GenerateXmlFromAssignment(MemberAssignment memberAssignment) {
            return new XElement("MemberAssignment",
                GenerateXmlFromProperty(memberAssignment.Member.GetType(), "Member", memberAssignment.Member),
                GenerateXmlFromProperty(memberAssignment.Expression.GetType(), "Expression", memberAssignment.Expression));
        }

        private XElement GenerateXmlFromExpression(string propName, Expression e) {
            return new XElement(propName, GenerateXmlFromExpressionCore(e));
        }

        private object GenerateXmlFromType(string propName, Type type) {
            return new XElement(propName, GenerateXmlFromTypeCore(type));
        }

        private XElement GenerateXmlFromTypeCore(Type type) {
            //vsadov: add detection of VB anon types
            if (type.Name.StartsWith("<>f__") || type.Name.StartsWith("VB$AnonymousType")) {
                return new XElement("AnonymousType",
                    new XAttribute("Name", type.FullName),
                    from property in type.GetProperties()
                    select new XElement("Property",
                        new XAttribute("Name", property.Name),
                        GenerateXmlFromTypeCore(property.PropertyType)),
                    new XElement("Constructor",
                        from parameter in type.GetConstructors().First().GetParameters()
                        select new XElement("Parameter",
                            new XAttribute("Name", parameter.Name),
                            GenerateXmlFromTypeCore(parameter.ParameterType))
                        ));
            }
            else {
                //vsadov: GetGenericArguments returns args for nongeneric types 
                //like arrays no need to save them.
                if (type.IsGenericType) {
                    return new XElement("Type",
                        new XAttribute("Name", type.GetGenericTypeDefinition().FullName),
                        from genArgType in type.GetGenericArguments()
                        select GenerateXmlFromTypeCore(genArgType));
                }
                else {
                    return new XElement("Type", new XAttribute("Name", type.FullName));
                }
            }
        }

        private object GenerateXmlFromParseable(string propName, object value) {
            return new XAttribute(propName, value.ToString());
        }

        private object GenerateXmlFromPrimitive(string propName, object value) {
            return new XAttribute(propName, value);
        }

        private object GenerateXmlFromMethodInfo(string propName, MethodInfo methodInfo) {
            if (methodInfo == null) {
                return new XElement(propName);
            }
            return new XElement(propName,
                new XAttribute("MemberType", methodInfo.MemberType),
                new XAttribute("MethodName", methodInfo.Name),
                GenerateXmlFromType("DeclaringType", methodInfo.DeclaringType),
                new XElement("Parameters",
                    from param in methodInfo.GetParameters()
                    select GenerateXmlFromType("Type", param.ParameterType)),
                new XElement("GenericArgTypes",
                    from argType in methodInfo.GetGenericArguments()
                    select GenerateXmlFromType("Type", argType)));
        }

        private object GenerateXmlFromPropertyInfo(string propName, PropertyInfo propertyInfo) {
            if (propertyInfo == null) {
                return new XElement(propName);
            }
            return new XElement(propName,
                new XAttribute("MemberType", propertyInfo.MemberType),
                new XAttribute("PropertyName", propertyInfo.Name),
                GenerateXmlFromType("DeclaringType", propertyInfo.DeclaringType),
                new XElement("IndexParameters",
                    from param in propertyInfo.GetIndexParameters()
                    select GenerateXmlFromType("Type", param.ParameterType)));
        }

        private object GenerateXmlFromFieldInfo(string propName, FieldInfo fieldInfo) {
            if (fieldInfo == null) {
                return new XElement(propName);
            }
            return new XElement(propName,
                new XAttribute("MemberType", fieldInfo.MemberType),
                new XAttribute("FieldName", fieldInfo.Name),
                GenerateXmlFromType("DeclaringType", fieldInfo.DeclaringType));
        }

        private object GenerateXmlFromConstructorInfo(string propName, ConstructorInfo constructorInfo) {
            if (constructorInfo == null) {
                return new XElement(propName);
            }
            return new XElement(propName,
                new XAttribute("MemberType", constructorInfo.MemberType),
                new XAttribute("MethodName", constructorInfo.Name),
                GenerateXmlFromType("DeclaringType", constructorInfo.DeclaringType),
                new XElement("Parameters",
                    from param in constructorInfo.GetParameters()
                    select new XElement("Parameter",
                        new XAttribute("Name", param.Name),
                        GenerateXmlFromType("Type", param.ParameterType))));
        }

        /*
       * DESERIALIZATION 
       */

        public Expression Deserialize(string xmlText) {
            return Deserialize(XElement.Parse(xmlText));
        }

        public Expression<TDelegate> Deserialize<TDelegate>(string xmlText) {
            return Deserialize<TDelegate>(XElement.Parse(xmlText));
        }

        public Expression Deserialize(XElement xml) {
            parameters.Clear();
            return ParseExpressionFromXmlNonNull(xml);
        }

        public Expression<TDelegate> Deserialize<TDelegate>(XElement xml) {
            var e = Deserialize(xml);
            if (e is Expression<TDelegate>) {
                return e as Expression<TDelegate>;
            }
            throw new Exception("xml must represent an Expression<TDelegate>");
        }

        private Expression ParseExpressionFromXml(XElement xml) {
            if (xml.IsEmpty) {
                return null;
            }

            return ParseExpressionFromXmlNonNull(xml.Elements().First());
        }

        private Expression ParseExpressionFromXmlNonNull(XElement xml) {
            Expression expression;
            if (TryCustomDeserializers(xml, out expression)) {
                return expression;
            }

            if (expression != null) {
                return expression;
            }
            switch (xml.Name.LocalName) {
                case "BinaryExpression":
                    return ParseBinaryExpresssionFromXml(xml);
                case "ConstantExpression":
                case "TypedConstantExpression":
                    return ParseConstantExpressionFromXml(xml);
                case "ParameterExpression":
                    return ParseParameterExpressionFromXml(xml);
                case "LambdaExpression":
                    return ParseLambdaExpressionFromXml(xml);
                case "MethodCallExpression":
                    return ParseMethodCallExpressionFromXml(xml);
                case "UnaryExpression":
                    return ParseUnaryExpressionFromXml(xml);
                case "MemberExpression":
                case "FieldExpression":
                case "PropertyExpression":
                    return ParseMemberExpressionFromXml(xml);
                case "NewExpression":
                    return ParseNewExpressionFromXml(xml);
                case "ListInitExpression":
                    return ParseListInitExpressionFromXml(xml);
                case "MemberInitExpression":
                    return ParseMemberInitExpressionFromXml(xml);
                case "ConditionalExpression":
                    return ParseConditionalExpressionFromXml(xml);
                case "NewArrayExpression":
                    return ParseNewArrayExpressionFromXml(xml);
                case "TypeBinaryExpression":
                    return ParseTypeBinaryExpressionFromXml(xml);
                case "InvocationExpression":
                    return ParseInvocationExpressionFromXml(xml);
                default:
                    throw new NotSupportedException(xml.Name.LocalName);
            }
        }

        /// <summary>
        ///   Uses first applicable custom deserializer, then returns. Does not attempt to use all custom deserializers.
        /// </summary>
        /// <param name="xml"> </param>
        /// <param name="result"> </param>
        /// <returns> </returns>
        private bool TryCustomDeserializers(XElement xml, out Expression result) {
            result = null;
            var i = 0;
            while (i < Converters.Count) {
                if (Converters[i].TryDeserialize(xml, out result)) {
                    return true;
                }
                i++;
            }
            return false;
        }

        private Expression ParseInvocationExpressionFromXml(XElement xml) {
            var expression = ParseExpressionFromXml(xml.Element("Expression"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.Invoke(expression, arguments);
        }

        private Expression ParseTypeBinaryExpressionFromXml(XElement xml) {
            var expression = ParseExpressionFromXml(xml.Element("Expression"));
            var typeOperand = ParseTypeFromXml(xml.Element("TypeOperand"));
            return Expression.TypeIs(expression, typeOperand);
        }

        private Expression ParseNewArrayExpressionFromXml(XElement xml) {
            var type = ParseTypeFromXml(xml.Element("Type"));
            if (!type.IsArray) {
                throw new Exception("Expected array type");
            }
            var elemType = type.GetElementType();
            var expressions = ParseExpressionListFromXml<Expression>(xml, "Expressions");
            switch (xml.Attribute("NodeType").Value) {
                case "NewArrayInit":
                    return Expression.NewArrayInit(elemType, expressions);
                case "NewArrayBounds":
                    return Expression.NewArrayBounds(elemType, expressions);
                default:
                    throw new Exception("Expected NewArrayInit or NewArrayBounds");
            }
        }

        private Expression ParseConditionalExpressionFromXml(XElement xml) {
            var test = ParseExpressionFromXml(xml.Element("Test"));
            var ifTrue = ParseExpressionFromXml(xml.Element("IfTrue"));
            var ifFalse = ParseExpressionFromXml(xml.Element("IfFalse"));
            return Expression.Condition(test, ifTrue, ifFalse);
        }

        private Expression ParseMemberInitExpressionFromXml(XElement xml) {
            var newExpression = ParseNewExpressionFromXml(xml.Element("NewExpression").Element("NewExpression")) as NewExpression;
            var bindings = ParseBindingListFromXml(xml, "Bindings").ToArray();
            return Expression.MemberInit(newExpression, bindings);
        }

        private Expression ParseListInitExpressionFromXml(XElement xml) {
            var newExpression = ParseExpressionFromXml(xml.Element("NewExpression")) as NewExpression;
            if (newExpression == null) {
                throw new Exception("Expceted a NewExpression");
            }
            var initializers = ParseElementInitListFromXml(xml, "Initializers").ToArray();
            return Expression.ListInit(newExpression, initializers);
        }

        private Expression ParseNewExpressionFromXml(XElement xml) {
            var constructor = ParseConstructorInfoFromXml(xml.Element("Constructor"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments").ToArray();
            var members = ParseMemberInfoListFromXml<MemberInfo>(xml, "Members").ToArray();
            if (members.Length == 0) {
                return Expression.New(constructor, arguments);
            }
            return Expression.New(constructor, arguments, members);
        }

        private Expression ParseMemberExpressionFromXml(XElement xml) {
            var expression = ParseExpressionFromXml(xml.Element("Expression"));
            var member = ParseMemberInfoFromXml(xml.Element("Member"));
            return Expression.MakeMemberAccess(expression, member);
        }

        //Expression ParseFieldExpressionFromXml(XElement xml)
        //{
        //    Expression expression = Expression.Field()
        //}

        private MemberInfo ParseMemberInfoFromXml(XElement xml) {
            var memberType = (MemberTypes)ParseConstantFromAttribute<MemberTypes>(xml, "MemberType");
            switch (memberType) {
                case MemberTypes.Field:
                    return ParseFieldInfoFromXml(xml);
                case MemberTypes.Property:
                    return ParsePropertyInfoFromXml(xml);
                case MemberTypes.Method:
                    return ParseMethodInfoFromXml(xml);
                case MemberTypes.Constructor:
                    return ParseConstructorInfoFromXml(xml);
                case MemberTypes.Custom:
                case MemberTypes.Event:
                case MemberTypes.NestedType:
                case MemberTypes.TypeInfo:
                default:
                    throw new NotSupportedException(string.Format("MEmberType {0} not supported", memberType));
            }
        }

        private MemberInfo ParseFieldInfoFromXml(XElement xml) {
            var fieldName = (string)ParseConstantFromAttribute<string>(xml, "FieldName");
            var declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            return declaringType.GetField(fieldName);
        }

        private MemberInfo ParsePropertyInfoFromXml(XElement xml) {
            var propertyName = (string)ParseConstantFromAttribute<string>(xml, "PropertyName");
            var declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("IndexParameters").Elements()
                     select ParseTypeFromXml(paramXml);
            return declaringType.GetProperty(propertyName);
        }

        private Expression ParseUnaryExpressionFromXml(XElement xml) {
            var operand = ParseExpressionFromXml(xml.Element("Operand"));
            var method = ParseMethodInfoFromXml(xml.Element("Method"));
            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // TODO: Why can't we use IsLifted and IsLiftedToNull here?  
            // May need to special case a nodeType if it needs them.
            return Expression.MakeUnary(expressionType, operand, type, method);
        }

        private Expression ParseMethodCallExpressionFromXml(XElement xml) {
            var instance = ParseExpressionFromXml(xml.Element("Object"));
            var method = ParseMethodInfoFromXml(xml.Element("Method"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            if (arguments == null || arguments.Count() == 0) {
                arguments = new Expression[0];
            }
            if (instance == null) //static method
            {
                return Expression.Call(method: method, arguments: arguments);
            }
            else {
                return Expression.Call(instance, method, arguments);
            }
        }

        private Expression ParseLambdaExpressionFromXml(XElement xml) {
            var body = ParseExpressionFromXml(xml.Element("Body"));
            var parameters = ParseExpressionListFromXml<ParameterExpression>(xml, "Parameters");
            var type = ParseTypeFromXml(xml.Element("Type"));
            // We may need to 
            //var lambdaExpressionReturnType = type.GetMethod("Invoke").ReturnType;
            //if (lambdaExpressionReturnType.IsArray)
            //{

            //    type = typeof(IEnumerable<>).MakeGenericType(type.GetElementType());
            //}
            return Expression.Lambda(type, body, parameters);
        }

        private IEnumerable<T> ParseExpressionListFromXml<T>(XElement xml, string elemName) where T : Expression {
            var elements = xml.Elements(elemName).Elements();
            var list = new List<T>();
            foreach (var tXml in elements) {
                object parsed = ParseExpressionFromXmlNonNull(tXml);
                list.Add((T)parsed);
            }
            return list;
            //return from tXml in xml.Element(elemName).Elements()
            //       select (T)ParseExpressionFromXmlNonNull(tXml);
        }

        private IEnumerable<T> ParseMemberInfoListFromXml<T>(XElement xml, string elemName) where T : MemberInfo {
            return from tXml in xml.Element(elemName).Elements()
                   select (T)ParseMemberInfoFromXml(tXml);
        }

        private IEnumerable<ElementInit> ParseElementInitListFromXml(XElement xml, string elemName) {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseElementInitFromXml(tXml);
        }

        private ElementInit ParseElementInitFromXml(XElement xml) {
            var addMethod = ParseMethodInfoFromXml(xml.Element("AddMethod"));
            var arguments = ParseExpressionListFromXml<Expression>(xml, "Arguments");
            return Expression.ElementInit(addMethod, arguments);
        }

        private IEnumerable<MemberBinding> ParseBindingListFromXml(XElement xml, string elemName) {
            return from tXml in xml.Element(elemName).Elements()
                   select ParseBindingFromXml(tXml);
        }

        private MemberBinding ParseBindingFromXml(XElement tXml) {
            var member = ParseMemberInfoFromXml(tXml.Element("Member"));
            switch (tXml.Name.LocalName) {
                case "MemberAssignment":
                    var expression = ParseExpressionFromXml(tXml.Element("Expression"));
                    return Expression.Bind(member, expression);
                case "MemberMemberBinding":
                    var bindings = ParseBindingListFromXml(tXml, "Bindings");
                    return Expression.MemberBind(member, bindings);
                case "MemberListBinding":
                    var initializers = ParseElementInitListFromXml(tXml, "Initializers");
                    return Expression.ListBind(member, initializers);
            }
            throw new NotImplementedException();
        }

        private Expression ParseParameterExpressionFromXml(XElement xml) {
            var type = ParseTypeFromXml(xml.Element("Type"));
            var name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            //vs: hack
            var id = name + type.FullName;
            if (!parameters.ContainsKey(id)) {
                parameters.Add(id, Expression.Parameter(type, name));
            }
            return parameters[id];
        }

        private Expression ParseConstantExpressionFromXml(XElement xml) {
            var type = ParseTypeFromXml(xml.Element("Type"));

            //I changed this to handle Linq.EnumerableQuery: 
            //now the return Type may not necessarily match the type parsed from XML,
            dynamic result = ParseConstantFromElement(xml, "Value", type);
            return Expression.Constant(result, result.GetType());
            //return Expression.Constant(result, type);
        }

        private Type ParseTypeFromXml(XElement xml) {
            Debug.Assert(xml.Elements().Count() == 1);
            return ParseTypeFromXmlCore(xml.Elements().First());
        }

        private Type ParseTypeFromXmlCore(XElement xml) {
            switch (xml.Name.ToString()) {
                case "Type":
                    return ParseNormalTypeFromXmlCore(xml);
                case "AnonymousType":
                    return ParseAnonymousTypeFromXmlCore(xml);
                default:
                    throw new ArgumentException("Expected 'Type' or 'AnonymousType'");
            }
        }

        private Type ParseNormalTypeFromXmlCore(XElement xml) {
            if (!xml.HasElements) {
                return resolver.GetType(xml.Attribute("Name").Value);
            }

            var genericArgumentTypes = from genArgXml in xml.Elements()
                                       select ParseTypeFromXmlCore(genArgXml);
            return resolver.GetType(xml.Attribute("Name").Value, genericArgumentTypes);
        }

        private Type ParseAnonymousTypeFromXmlCore(XElement xElement) {
            var name = xElement.Attribute("Name").Value;
            var properties = from propXml in xElement.Elements("Property")
                             select new TypeResolver.NameTypePair {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };
            var ctr_params = from propXml in xElement.Elements("Constructor").Elements("Parameter")
                             select new TypeResolver.NameTypePair {
                                 Name = propXml.Attribute("Name").Value,
                                 Type = ParseTypeFromXml(propXml)
                             };

            return resolver.GetOrCreateAnonymousTypeFor(name, properties.ToArray(), ctr_params.ToArray());
        }

        private Expression ParseBinaryExpresssionFromXml(XElement xml) {
            var expressionType = (ExpressionType)ParseConstantFromAttribute<ExpressionType>(xml, "NodeType");
            ;
            var left = ParseExpressionFromXml(xml.Element("Left"));
            var right = ParseExpressionFromXml(xml.Element("Right"));

            if (left.Type != right.Type) {
                ParseBinaryExpressionConvert(ref left, ref right);
            }

            var isLifted = (bool)ParseConstantFromAttribute<bool>(xml, "IsLifted");
            var isLiftedToNull = (bool)ParseConstantFromAttribute<bool>(xml, "IsLiftedToNull");
            var type = ParseTypeFromXml(xml.Element("Type"));
            var method = ParseMethodInfoFromXml(xml.Element("Method"));
            var conversion = ParseExpressionFromXml(xml.Element("Conversion")) as LambdaExpression;
            if (expressionType == ExpressionType.Coalesce) {
                return Expression.Coalesce(left, right, conversion);
            }
            return Expression.MakeBinary(expressionType, left, right, isLiftedToNull, method);
        }

        private void ParseBinaryExpressionConvert(ref Expression left, ref Expression right) {
            if (left.Type != right.Type) {
                UnaryExpression unary;
                LambdaExpression lambda;
                if (right is ConstantExpression) {
                    unary = Expression.Convert(left, right.Type);
                    left = unary;
                }
                else //(left is ConstantExpression)				
                {
                    unary = Expression.Convert(right, left.Type);
                    right = unary;
                }
                //lambda = Expression.Lambda(unary);
                //Delegate fn = lambda.Compile();
                //var result = fn.DynamicInvoke(new object[0]);
            }
        }

        private MethodInfo ParseMethodInfoFromXml(XElement xml) {
            if (xml.IsEmpty) {
                return null;
            }
            var name = (string)ParseConstantFromAttribute<string>(xml, "MethodName");
            var declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseTypeFromXml(paramXml);
            var genArgs = from argXml in xml.Element("GenericArgTypes").Elements()
                          select ParseTypeFromXml(argXml);
            return resolver.GetMethod(declaringType, name, ps.ToArray(), genArgs.ToArray());
        }

        private ConstructorInfo ParseConstructorInfoFromXml(XElement xml) {
            if (xml.IsEmpty) {
                return null;
            }
            var declaringType = ParseTypeFromXml(xml.Element("DeclaringType"));
            var ps = from paramXml in xml.Element("Parameters").Elements()
                     select ParseParameterFromXml(paramXml);
            var ci = declaringType.GetConstructor(ps.ToArray());
            return ci;
        }

        private Type ParseParameterFromXml(XElement xml) {
            var name = (string)ParseConstantFromAttribute<string>(xml, "Name");
            var type = ParseTypeFromXml(xml.Element("Type"));
            return type;
        }

        private object ParseConstantFromAttribute<T>(XElement xml, string attrName) {
            var objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(typeof(T))) {
                throw new Exception("We should never be encoding Types in attributes now.");
            }
            if (typeof(Enum).IsAssignableFrom(typeof(T))) {
                return Enum.Parse(typeof(T), objectStringValue, false);
            }
            return Convert.ChangeType(objectStringValue, typeof(T), default(IFormatProvider));
        }

        private object ParseConstantFromAttribute(XElement xml, string attrName, Type type) {
            var objectStringValue = xml.Attribute(attrName).Value;
            if (typeof(Type).IsAssignableFrom(type)) {
                throw new Exception("We should never be encoding Types in attributes now.");
            }
            if (typeof(Enum).IsAssignableFrom(type)) {
                return Enum.Parse(type, objectStringValue, false);
            }
            return Convert.ChangeType(objectStringValue, type, default(IFormatProvider));
        }

        /// <summary>
        ///   returns object for use in a call to Expression.Constant(object, Type)
        /// </summary>
        /// <param name="xml"> </param>
        /// <param name="elemName"> </param>
        /// <param name="expectedType"> </param>
        /// <returns> </returns>
        private object ParseConstantFromElement(XElement xml, string elemName, Type expectedType) {
            var objectStringValue = xml.Element(elemName).Value;
            if (typeof(Type).IsAssignableFrom(expectedType)) {
                return ParseTypeFromXml(xml.Element("Value"));
            }
            if (typeof(Enum).IsAssignableFrom(expectedType)) {
                return Enum.Parse(expectedType, objectStringValue, false);
            }
            return Convert.ChangeType(objectStringValue, expectedType, default(IFormatProvider));
        }

        public XElement GenerateXmlFromExpressionCore(Expression e) {
            XElement replace;
            if (e == null) {
                return null;
            }
            if (TryCustomSerializers(e, out replace)) {
                return replace;
            }
            else if (e is BinaryExpression) {
                return BinaryExpressionToXElement((BinaryExpression)e);
            }
            else if (e is BlockExpression) {
                return BlockExpressionToXElement((BlockExpression)e);
            }
            else if (e is ConditionalExpression) {
                return ConditionalExpressionToXElement((ConditionalExpression)e);
            }
            else if (e is ConstantExpression) {
                return ConstantExpressionToXElement((ConstantExpression)e);
            }
            else if (e is DebugInfoExpression) {
                return DebugInfoExpressionToXElement((DebugInfoExpression)e);
            }
            else if (e is DefaultExpression) {
                return DefaultExpressionToXElement((DefaultExpression)e);
            }
            else if (e is DynamicExpression) {
                return DynamicExpressionToXElement((DynamicExpression)e);
            }
            else if (e is GotoExpression) {
                return GotoExpressionToXElement((GotoExpression)e);
            }
            else if (e is IndexExpression) {
                return IndexExpressionToXElement((IndexExpression)e);
            }
            else if (e is InvocationExpression) {
                return InvocationExpressionToXElement((InvocationExpression)e);
            }
            else if (e is LabelExpression) {
                return LabelExpressionToXElement((LabelExpression)e);
            }
            else if (e is LambdaExpression) {
                return LambdaExpressionToXElement((LambdaExpression)e);
            }
            else if (e is ListInitExpression) {
                return ListInitExpressionToXElement((ListInitExpression)e);
            }
            else if (e is LoopExpression) {
                return LoopExpressionToXElement((LoopExpression)e);
            }
            else if (e is MemberExpression) {
                return MemberExpressionToXElement((MemberExpression)e);
            }
            else if (e is MemberInitExpression) {
                return MemberInitExpressionToXElement((MemberInitExpression)e);
            }
            else if (e is MethodCallExpression) {
                return MethodCallExpressionToXElement((MethodCallExpression)e);
            }
            else if (e is NewArrayExpression) {
                return NewArrayExpressionToXElement((NewArrayExpression)e);
            }
            else if (e is NewExpression) {
                return NewExpressionToXElement((NewExpression)e);
            }
            else if (e is ParameterExpression) {
                return ParameterExpressionToXElement((ParameterExpression)e);
            }
            else if (e is RuntimeVariablesExpression) {
                return RuntimeVariablesExpressionToXElement((RuntimeVariablesExpression)e);
            }
            else if (e is SwitchExpression) {
                return SwitchExpressionToXElement((SwitchExpression)e);
            }
            else if (e is TryExpression) {
                return TryExpressionToXElement((TryExpression)e);
            }
            else if (e is TypeBinaryExpression) {
                return TypeBinaryExpressionToXElement((TypeBinaryExpression)e);
            }
            else if (e is UnaryExpression) {
                return UnaryExpressionToXElement((UnaryExpression)e);
            }
            else {
                return null;
            }
        }

        internal XElement BinaryExpressionToXElement(BinaryExpression e) {
            object value;
            var xName = "BinaryExpression";
            var XElementValues = new object[9];
            value = (e).CanReduce;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            value = (e).Right;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Expression),
                "Right", value ?? string.Empty);
            value = (e).Left;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Left", value ?? string.Empty);
            value = (e).Method;
            XElementValues[3] = GenerateXmlFromProperty(typeof(MethodInfo),
                "Method", value ?? string.Empty);
            value = (e).Conversion;
            XElementValues[4] = GenerateXmlFromProperty(typeof(LambdaExpression),
                "Conversion", value ?? string.Empty);
            value = (e).IsLifted;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "IsLifted", value ?? string.Empty);
            value = (e).IsLiftedToNull;
            XElementValues[6] = GenerateXmlFromProperty(typeof(Boolean),
                "IsLiftedToNull", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[7] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[8] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement BlockExpressionToXElement(BlockExpression e) {
            object value;
            var xName = "BlockExpression";
            var XElementValues = new object[6];
            value = (e).Expressions;
            XElementValues[0] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Expressions", value ?? string.Empty);
            value = (e).Variables;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<ParameterExpression>),
                "Variables", value ?? string.Empty);
            value = (e).Result;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Result", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[3] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement ConditionalExpressionToXElement(ConditionalExpression e) {
            object value;
            var xName = "ConditionalExpression";
            var XElementValues = new object[6];
            value = (e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).Test;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Test", value ?? string.Empty);
            value = (e).IfTrue;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Expression),
                "IfTrue", value ?? string.Empty);
            value = (e).IfFalse;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Expression),
                "IfFalse", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement ConstantExpressionToXElement(ConstantExpression e) {
            object value;
            var xName = "ConstantExpression";
            var XElementValues = new object[4];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Value;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Object),
                "Value", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement DebugInfoExpressionToXElement(DebugInfoExpression e) {
            object value;
            var xName = "DebugInfoExpression";
            var XElementValues = new object[9];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).StartLine;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Int32),
                "StartLine", value ?? string.Empty);
            value = (e).StartColumn;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Int32),
                "StartColumn", value ?? string.Empty);
            value = (e).EndLine;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Int32),
                "EndLine", value ?? string.Empty);
            value = (e).EndColumn;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Int32),
                "EndColumn", value ?? string.Empty);
            value = (e).Document;
            XElementValues[6] = GenerateXmlFromProperty(typeof(SymbolDocumentInfo),
                "Document", value ?? string.Empty);
            value = (e).IsClear;
            XElementValues[7] = GenerateXmlFromProperty(typeof(Boolean),
                "IsClear", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[8] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement DefaultExpressionToXElement(DefaultExpression e) {
            object value;
            var xName = "DefaultExpression";
            var XElementValues = new object[3];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement DynamicExpressionToXElement(DynamicExpression e) {
            object value;
            var xName = "DynamicExpression";
            var XElementValues = new object[6];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Binder;
            XElementValues[2] = GenerateXmlFromProperty(typeof(CallSiteBinder),
                "Binder", value ?? string.Empty);
            value = (e).DelegateType;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Type),
                "DelegateType", value ?? string.Empty);
            value = (e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Arguments", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement GotoExpressionToXElement(GotoExpression e) {
            object value;
            var xName = "GotoExpression";
            var XElementValues = new object[6];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Value;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Value", value ?? string.Empty);
            value = (e).Target;
            XElementValues[3] = GenerateXmlFromProperty(typeof(LabelTarget),
                "Target", value ?? string.Empty);
            value = (e).Kind;
            XElementValues[4] = GenerateXmlFromProperty(typeof(GotoExpressionKind),
                "Kind", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement IndexExpressionToXElement(IndexExpression e) {
            object value;
            var xName = "IndexExpression";
            var XElementValues = new object[6];
            value = (e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).Object;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Object", value ?? string.Empty);
            value = (e).Indexer;
            XElementValues[3] = GenerateXmlFromProperty(typeof(PropertyInfo),
                "Indexer", value ?? string.Empty);
            value = (e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Arguments", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement InvocationExpressionToXElement(InvocationExpression e) {
            object value;
            var xName = "InvocationExpression";
            var XElementValues = new object[5];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Expression;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Expression", value ?? string.Empty);
            value = (e).Arguments;
            XElementValues[3] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Arguments", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement LabelExpressionToXElement(LabelExpression e) {
            object value;
            var xName = "LabelExpression";
            var XElementValues = new object[5];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Target;
            XElementValues[2] = GenerateXmlFromProperty(typeof(LabelTarget),
                "Target", value ?? string.Empty);
            value = (e).DefaultValue;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Expression),
                "DefaultValue", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement LambdaExpressionToXElement(LambdaExpression e) {
            object value;
            var xName = "LambdaExpression";
            var XElementValues = new object[8];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Parameters;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<ParameterExpression>),
                "Parameters", value ?? string.Empty);
            value = (e).Name;
            XElementValues[3] = GenerateXmlFromProperty(typeof(String),
                "Name", value ?? string.Empty);
            value = (e).Body;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Expression),
                "Body", value ?? string.Empty);
            value = (e).ReturnType;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Type),
                "ReturnType", value ?? string.Empty);
            value = (e).TailCall;
            XElementValues[6] = GenerateXmlFromProperty(typeof(Boolean),
                "TailCall", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[7] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement ListInitExpressionToXElement(ListInitExpression e) {
            object value;
            var xName = "ListInitExpression";
            var XElementValues = new object[5];
            value = (e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            value = (e).NewExpression;
            XElementValues[3] = GenerateXmlFromProperty(typeof(NewExpression),
                "NewExpression", value ?? string.Empty);
            value = (e).Initializers;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<ElementInit>),
                "Initializers", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement LoopExpressionToXElement(LoopExpression e) {
            object value;
            var xName = "LoopExpression";
            var XElementValues = new object[6];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Body;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Body", value ?? string.Empty);
            value = (e).BreakLabel;
            XElementValues[3] = GenerateXmlFromProperty(typeof(LabelTarget),
                "BreakLabel", value ?? string.Empty);
            value = (e).ContinueLabel;
            XElementValues[4] = GenerateXmlFromProperty(typeof(LabelTarget),
                "ContinueLabel", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement MemberExpressionToXElement(MemberExpression e) {
            object value;
            var xName = "MemberExpression";
            var XElementValues = new object[5];
            value = (e).Member;
            XElementValues[0] = GenerateXmlFromProperty(typeof(MemberInfo),
                "Member", value ?? string.Empty);
            value = (e).Expression;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Expression),
                "Expression", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement MemberInitExpressionToXElement(MemberInitExpression e) {
            object value;
            var xName = "MemberInitExpression";
            var XElementValues = new object[5];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).NewExpression;
            XElementValues[3] = GenerateXmlFromProperty(typeof(NewExpression),
                "NewExpression", value ?? string.Empty);
            value = (e).Bindings;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<MemberBinding>),
                "Bindings", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement MethodCallExpressionToXElement(MethodCallExpression e) {
            object value;
            var xName = "MethodCallExpression";
            var XElementValues = new object[6];
            value = (e).NodeType;
            XElementValues[0] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Type;
            XElementValues[1] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).Method;
            XElementValues[2] = GenerateXmlFromProperty(typeof(MethodInfo),
                "Method", value ?? string.Empty);
            value = (e).Object;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Expression),
                "Object", value ?? string.Empty);
            value = (e).Arguments;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Arguments", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement NewArrayExpressionToXElement(NewArrayExpression e) {
            object value;
            var xName = "NewArrayExpression";
            var XElementValues = new object[4];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).Expressions;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Expressions", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement NewExpressionToXElement(NewExpression e) {
            object value;
            var xName = "NewExpression";
            var XElementValues = new object[6];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Constructor;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ConstructorInfo),
                "Constructor", value ?? string.Empty);
            value = (e).Arguments;
            XElementValues[3] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<Expression>),
                "Arguments", value ?? string.Empty);
            value = (e).Members;
            XElementValues[4] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<MemberInfo>),
                "Members", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement ParameterExpressionToXElement(ParameterExpression e) {
            object value;
            var xName = "ParameterExpression";
            var XElementValues = new object[5];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Name;
            XElementValues[2] = GenerateXmlFromProperty(typeof(String),
                "Name", value ?? string.Empty);
            value = (e).IsByRef;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Boolean),
                "IsByRef", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement RuntimeVariablesExpressionToXElement(RuntimeVariablesExpression e) {
            object value;
            var xName = "RuntimeVariablesExpression";
            var XElementValues = new object[4];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Variables;
            XElementValues[2] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<ParameterExpression>),
                "Variables", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement SwitchExpressionToXElement(SwitchExpression e) {
            return new XElement("SwitchExpression", new object[] {
                GenerateXmlFromProperty(typeof (Type), "Type", e.Type),
                GenerateXmlFromProperty(typeof (ExpressionType), "NodeType", e.NodeType),
                GenerateXmlFromProperty(typeof (Expression), "SwitchValue", e.SwitchValue),
                GenerateXmlFromProperty(typeof (ReadOnlyCollection<SwitchCase>), "Cases", e.Cases),
                GenerateXmlFromProperty(typeof (Expression), "DefaultBody", e.DefaultBody),
                GenerateXmlFromProperty(typeof (MethodInfo), "Comparison", e.Comparison),
                GenerateXmlFromProperty(typeof (Boolean), "CanReduce", e.CanReduce)
            });
        }

        internal XElement TryExpressionToXElement(TryExpression e) {
            object value;
            var xName = "TryExpression";
            var XElementValues = new object[7];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Body;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Body", value ?? string.Empty);
            value = (e).Handlers;
            XElementValues[3] = GenerateXmlFromProperty(typeof(ReadOnlyCollection<CatchBlock>),
                "Handlers", value ?? string.Empty);
            value = (e).Finally;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Expression),
                "Finally", value ?? string.Empty);
            value = (e).Fault;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Expression),
                "Fault", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[6] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal XElement TypeBinaryExpressionToXElement(TypeBinaryExpression e) {
            object value;
            var xName = "TypeBinaryExpression";
            var XElementValues = new object[5];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Expression;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Expression", value ?? string.Empty);
            value = (e).TypeOperand;
            XElementValues[3] = GenerateXmlFromProperty(typeof(Type),
                "TypeOperand", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }


        internal XElement UnaryExpressionToXElement(UnaryExpression e) {
            object value;
            var xName = "UnaryExpression";
            var XElementValues = new object[7];
            value = (e).Type;
            XElementValues[0] = GenerateXmlFromProperty(typeof(Type),
                "Type", value ?? string.Empty);
            value = (e).NodeType;
            XElementValues[1] = GenerateXmlFromProperty(typeof(ExpressionType),
                "NodeType", value ?? string.Empty);
            value = (e).Operand;
            XElementValues[2] = GenerateXmlFromProperty(typeof(Expression),
                "Operand", value ?? string.Empty);
            value = (e).Method;
            XElementValues[3] = GenerateXmlFromProperty(typeof(MethodInfo),
                "Method", value ?? string.Empty);
            value = (e).IsLifted;
            XElementValues[4] = GenerateXmlFromProperty(typeof(Boolean),
                "IsLifted", value ?? string.Empty);
            value = (e).IsLiftedToNull;
            XElementValues[5] = GenerateXmlFromProperty(typeof(Boolean),
                "IsLiftedToNull", value ?? string.Empty);
            value = (e).CanReduce;
            XElementValues[6] = GenerateXmlFromProperty(typeof(Boolean),
                "CanReduce", value ?? string.Empty);
            return new XElement(xName, XElementValues);
        }

        internal void setup() {
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<Expression>(
                (message, key, serialize) => {

            }, (message, key) => (Expression)message.GetValue(key, message.GetValue<Type>(key + ".Type"))));


            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<UnaryExpression>(
                (message, key, obj) => {
                    message.Add(key + ".Type", obj.Type);
                    message.Add(key + ".NodeType", obj.NodeType);
                    message.Add(key + ".Operand", obj.Operand);
                    message.Add(key + ".Method", obj.Method);
                    message.Add(key + ".IsLifted", obj.IsLifted);
                    message.Add(key + ".IsLiftedToNull", obj.IsLiftedToNull);
                    message.Add(key + ".CanReduce", obj.CanReduce);
                },
                (message, key) => {
                    var method = message.GetValue<MethodInfo>(key + ".Method");
                    var operand = message.GetValue<Expression>(key + ".Operand");
                    var isLifted = message.GetValue<bool>(key + ".IsLifted");
                    var isLiftedToNull = message.GetValue<bool>(key + ".IsLiftedToNull");
                    var expressionType = message.GetValue<ExpressionType>(key + ".NodeType");
                    var type = message.GetValue<Type>(key + ".Type");
                    // TODO: Why can't we use IsLifted and IsLiftedToNull here?  
                    // May need to special case a nodeType if it needs them.
                    return Expression.MakeUnary(expressionType, operand, type, method);
                }));

            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<BinaryExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.MakeBinary();
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<ConstantExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.Constant();
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<ParameterExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.Parameter()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<LambdaExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.Lambda()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<MethodCallExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.MakeMemberAccess()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<NewExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.New()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<ListInitExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.ListInit()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<MemberInitExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.MemberInit()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<ConditionalExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.Condition()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<NewArrayExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.NewArrayInit()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<TypeBinaryExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.TypeIs()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<InvocationExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.Invoke()
            });
            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<MemberExpression>((message, key, obj) => {
                
            },(message, key) => {
                return Expression.MakeMemberAccess()
            });

            UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<FieldExpression>((message, key, obj) => { },(message, key) => { });
UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<PropertyExpression>((message, key, obj) => { },(message, key) => { });
UrlEncodedMessage.AddCustomSerializer(new CustomSerializer<TypedConstantExpression>((message, key, obj) => { },(message, key) => { });

        }
    }
#endif
}