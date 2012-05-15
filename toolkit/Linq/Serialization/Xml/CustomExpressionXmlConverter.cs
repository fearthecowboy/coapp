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
    using System.Linq.Expressions;
    using System.Xml.Linq;

    public abstract class CustomExpressionXmlConverter {
        public abstract bool TryDeserialize(XElement expressionXml, out Expression e);
        public abstract bool TrySerialize(Expression expression, out XElement x);
    }
}