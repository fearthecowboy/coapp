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

namespace CoApp.Toolkit.Linq.Serialization.Xml {
    using System;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml.Linq;

    public class KnownTypeExpressionXmlConverter : CustomExpressionXmlConverter {
        public KnownTypeExpressionXmlConverter(TypeResolver @resolver) {
            this.resolver = @resolver;
        }

        private TypeResolver resolver;

        ///<summary>
        ///  code originally in method ParseConstantFromElement(XElement xml, string elemName, Type expectedType)
        ///</summary>
        ///<param name="x"> </param>
        ///<param name="e"> </param>
        ///<returns> </returns>
        public override bool TryDeserialize(XElement x, out Expression e) {
            if (x.Name.LocalName == typeof (ConstantExpression).Name) {
                var serializedType = resolver.GetType(x.Element("Type").Value);
                if (resolver.HasMappedKnownType(serializedType)) {
                    var xml = x.Element("Value").Value;
                    DataContractSerializer dserializer;
                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml))) {
                        //if (typeof(IQueryable).IsAssignableFrom(expectedType) && IsIEnumerableOf(expectedType, knownType))
                        //{
                        //    dserializer = new DataContractSerializer(knownType.MakeArrayType(), this.resolver.knownTypes);
                        //    result = dserializer.ReadObject(ms);
                        //    result = Enumerable.ToArray(result);
                        //}					
                        dserializer = new DataContractSerializer(serializedType, resolver.knownTypes);
                        var instance = dserializer.ReadObject(ms);
                        e = Expression.Constant(instance);
                        return true;
                    }
                }
            }

            e = null;
            return false;
        }

        public override bool TrySerialize(Expression e, out XElement x) {
            //!Query`1 && !System.Linq.EnumerableQuery`1
            if (e.NodeType == ExpressionType.Constant && !typeof (IQueryable).IsAssignableFrom(e.Type)) {
                var cx = (ConstantExpression)e;
                Type knownType;
                var actualType = cx.Type;
                if (cx.Value != null && cx.Type != cx.Value.GetType()) {
                    actualType = cx.Value.GetType();
                    //either convert Nullable`1 (cx.Type) to the actual ValueType
                    //or convert the actual ValueType to the Nullable`1
                    //UnaryExpression u = Expression.Convert(cx, cx.Type);
                    //LambdaExpression lambda = Expression.Lambda(u);
                    //Delegate fn = lambda.Compile();
                    //object result = fn.DynamicInvoke(new object[0]);
                    //cx = Expression.Constant(result);
                }
                if (resolver.HasMappedKnownType(actualType, out knownType)) {
                    var instance = cx.Value;
                    var serializer = new DataContractSerializer(actualType, resolver.knownTypes);
                    using (var ms = new MemoryStream()) {
                        serializer.WriteObject(ms, instance);
                        ms.Position = 0;
                        var reader = new StreamReader(ms, Encoding.UTF8);
                        var xml = reader.ReadToEnd();
                        x = new XElement(typeof (ConstantExpression).Name,
                            new XAttribute("NodeType", actualType),
                            new XElement("Type", cx.Type),
                            new XElement("Value", xml));

                        return true;
                    }
                }
            }

            x = null;
            return false;
            //if (typeof(IQueryable).IsAssignableFrom(instance.GetType()))
            //{
            //    if (typeof(Query<>).MakeGenericType(knownType).IsAssignableFrom(instance.GetType()))
            //    {
            //        return instance.ToString();
            //    }
            //    something = LinqHelper.CastToGenericEnumerable((IQueryable)instance, knownType);
            //    something = Enumerable.ToArray(something);
            //}
        }
    }
}