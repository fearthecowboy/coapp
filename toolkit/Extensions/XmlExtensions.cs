//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------
// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Serialization;
    using Collections;

    /// <summary>
    ///   Xml Extension Methods.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class XmlExtensions {
        /// <summary>
        ///   Cache for xml strings and their xml document equivalent.
        /// </summary>
        private static readonly IDictionary<string, XmlDocument> DocCache = new XDictionary<string, XmlDocument>();

        /// <summary>
        ///   Returns a list of strings for the inner text of a collection of XmlNodes
        /// </summary>
        /// <param name="nodeList"> The node list. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> InnerText(this XmlNodeList nodeList) {
            var result = new List<string>();
            foreach (XmlNode node in nodeList) {
                result.Add(node.InnerText);
            }
            return result;
        }

        /// <summary>
        ///   Gets the xmldoc representation of the given string
        /// </summary>
        /// <param name="xmlDoc"> The XML doc. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlDocument XmlDoc(this string xmlDoc) {
            if (DocCache.ContainsKey(xmlDoc)) {
                return DocCache[xmlDoc];
            }

            var doc = new XmlDocument();
            doc.LoadXml(xmlDoc);
            DocCache.Add(xmlDoc, doc);
            return doc;
        }

        /// <summary>
        ///   Gets the xmldoc representation of the given string as a JSON graph.
        /// </summary>
        /// <param name="jsonDoc"> The json doc. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlDocument JsonDoc(this string jsonDoc) {
            if (DocCache.ContainsKey(jsonDoc)) {
                return DocCache[jsonDoc];
            }

            var stream = new MemoryStream(Encoding.Default.GetBytes(jsonDoc));
            var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max);

            var doc = new XmlDocument();
            doc.Load(reader);

            DocCache.Add(jsonDoc, doc);
            return doc;
        }

        /// <summary>
        ///   Converts an XML document to JSON
        /// </summary>
        /// <param name="xmlDoc"> The XML doc. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string ToJsonString(this XmlDocument xmlDoc) {
            var stream = new MemoryStream();
            var writer = JsonReaderWriterFactory.CreateJsonWriter(stream);
            xmlDoc.WriteTo(writer);
            return Encoding.Default.GetString(stream.GetBuffer());
        }

        /// <summary>
        ///   deserializes a JSON doc as given object type.
        /// </summary>
        /// <typeparam name="TType"> The type of the type. </typeparam>
        /// <param name="jsonText"> The json text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static TType JsonDeserialize<TType>(this string jsonText) {
            var stream = new MemoryStream(Encoding.Default.GetBytes(jsonText));
            var json = new DataContractJsonSerializer(typeof (TType));
            return (TType)json.ReadObject(stream);
        }

        /// <summary>
        ///   Serializes a object to JSON
        /// </summary>
        /// <param name="obj"> The obj. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string JsonSerialize(this object obj) {
            var stream = new MemoryStream();
            var json = new DataContractJsonSerializer(obj.GetType());
            json.WriteObject(stream, obj);
            return Encoding.Default.GetString(stream.GetBuffer());
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument for an XPath Expression
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPath(this XmlDocument doc, string XPathExpression, params object[] args) {
            return doc.SelectNodes(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper method to select a single in a xmldocument for an XPath Expression
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNode XPathSingle(this XmlDocument doc, string XPathExpression, params object[] args) {
            return doc.SelectSingleNode(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument using a case insensitive XPath expression.
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathContains(this XmlDocument doc, string XPathExpression, string containsText) {
            return doc.XPath(String.Format(@"{0}[contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}')]", XPathExpression, containsText));
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument excluding a case insensitive XPath expression.
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathExcludes(this XmlDocument doc, string XPathExpression, string containsText) {
            return doc.XPath(String.Format(@"{0}[not(contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}'))]", XPathExpression, containsText));
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument using a parameterized XPath Expression.
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPath(this string doc, string XPathExpression, params object[] args) {
            return doc.XmlDoc().XPath(XPathExpression, args);
        }

        /// <summary>
        ///   wrapper method to a single node in a xmldocument using a parameterized XPath Expression.
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNode XPathSingle(this string doc, string XPathExpression, params object[] args) {
            return doc.XmlDoc().XPathSingle(XPathExpression, args);
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument string
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathContains(this string doc, string XPathExpression, string containsText) {
            return doc.XmlDoc().XPathContains(XPathExpression, containsText);
        }

        /// <summary>
        ///   wrapper method to select nodes in a xmldocument string excluding a string
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathExcludes(this string doc, string XPathExpression, string containsText) {
            return doc.XmlDoc().XPathExcludes(XPathExpression, containsText);
        }

        /// <summary>
        ///   wrapper method
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPath(this XmlNode doc, string XPathExpression, params object[] args) {
            return doc.SelectNodes(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper method
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNode XPathSingle(this XmlNode doc, string XPathExpression, params object[] args) {
            return doc.SelectSingleNode(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper method
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathContains(this XmlNode doc, string XPathExpression, string containsText) {
            return doc.XPath(String.Format(@"{0}[contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}')]", XPathExpression, containsText));
        }

        /// <summary>
        ///   wrapper method
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathExcludes(this XmlNode doc, string XPathExpression, string containsText) {
            return doc.XPath(String.Format(@"{0}[not(contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}'))]", XPathExpression, containsText));
        }

        /// <summary>
        ///   Creates and Xml document from a node list
        /// </summary>
        /// <param name="nodeList"> The node list. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlDocument ToXmlDocument(this XmlNodeList nodeList) {
            var tmpDoc = new StringBuilder();
            tmpDoc.AppendLine("<?xml version=\"1.0\" encoding=\"Windows-1252\"?>\r\n<content>\r\n");

            foreach (XmlNode n in nodeList) {
                tmpDoc.Append(n.OuterXml);
            }

            tmpDoc.AppendLine("</content>");
            return tmpDoc.ToString().XmlDoc();
        }

        /// <summary>
        ///   wrapper function
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPath(this XmlNodeList doc, string XPathExpression, params object[] args) {
            return doc.ToXmlDocument().SelectNodes(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper function
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNode XPathSingle(this XmlNodeList doc, string XPathExpression, params object[] args) {
            return doc.ToXmlDocument().SelectSingleNode(XPathExpression.format(args));
        }

        /// <summary>
        ///   wrapper function
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathContains(this XmlNodeList doc, string XPathExpression, string containsText) {
            return doc.ToXmlDocument().XPath(String.Format(@"{0}[contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}')]", XPathExpression, containsText));
        }

        /// <summary>
        ///   wrapper function
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="XPathExpression"> The X path expression. </param>
        /// <param name="containsText"> The contains text. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList XPathExcludes(this XmlNodeList doc, string XPathExpression, string containsText) {
            return doc.ToXmlDocument().XPath(String.Format(@"{0}[not(contains( translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') , '{1}'))]", XPathExpression, containsText));
        }

        /// <summary>
        ///   wrapper function
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static XmlNodeList DocumentNodes(this XmlDocument doc) {
            return doc.ChildNodes[1].ChildNodes;
        }

        /// <summary>
        ///   Determines whether a given file is an xml document
        /// </summary>
        /// <param name="filename"> The filename. </param>
        /// <returns> <c>true</c> if [is XML file] [the specified filename]; otherwise, <c>false</c> . </returns>
        /// <remarks>
        /// </remarks>
        public static bool IsXmlFile(this string filename) {
            try {
                if (File.Exists(filename)) {
                    using (var s = File.OpenText(filename)) {
                        if (! s.ReadLine().Trim().StartsWith("<")) {
                            return false;
                        }
                    }
                    using (var s = File.OpenText(filename)) {
                        var xr = XmlReader.Create(s);
                        xr.Read();
                        xr.Read();
                        return true;
                    }
                }
            } catch {
            }

            return false;
        }

        /// <summary>
        ///   Pretties the save XML.
        /// </summary>
        /// <param name="xmlDocumentText"> The XML document text. </param>
        /// <param name="outputPath"> The output path. </param>
        /// <remarks>
        /// </remarks>
        public static void PrettySaveXml(this IEnumerable<string> xmlDocumentText, string outputPath) {
            var tempDocument = XDocument.Load(new StringReader(String.Join("\r\n", xmlDocumentText)));
            tempDocument.Save(outputPath, SaveOptions.None);
        }

        /// <summary>
        ///   Pretties the save XML.
        /// </summary>
        /// <param name="xmlDocumentStream"> The XML document stream. </param>
        /// <param name="outputPath"> The output path. </param>
        /// <remarks>
        /// </remarks>
        public static void PrettySaveXml(this MemoryStream xmlDocumentStream, string outputPath) {
            xmlDocumentStream.Seek(0, SeekOrigin.Begin);
            var tempDocument = XDocument.Load(xmlDocumentStream);
            tempDocument.Save(outputPath, SaveOptions.None);
        }

        /// <summary>
        ///   Pretties the save XML.
        /// </summary>
        /// <param name="xmlDocumentStream"> The XML document stream. </param>
        /// <remarks>
        /// </remarks>
        public static string PrettyXml(this MemoryStream xmlDocumentStream) {
            xmlDocumentStream.Seek(0, SeekOrigin.Begin);
            var tempDocument = XDocument.Load(xmlDocumentStream);
            ;
            return tempDocument.ToString();
        }

        public static string ToXml<T>(this T obj, string elementName = null) {
            var attributeOverrides = new XmlAttributeOverrides();
            if (elementName != null) {
                attributeOverrides.Add(typeof (T), new XmlAttributes {
                    XmlRoot = new XmlRootAttribute {
                        ElementName = elementName
                    }
                });
            }

            var xmlSerializer = new XmlSerializer(typeof (T), attributeOverrides);

            using (var ms = new MemoryStream()) {
                using (var writer = XmlWriter.Create(ms)) {
                    xmlSerializer.Serialize(writer, obj);
                    writer.WriteEndDocument();
                    writer.Close();
                    return ms.PrettyXml();
                }
            }
        }

        public static T FromXml<T>(this string xmlText, string elementName = null) where T : class {
            var attributeOverrides = new XmlAttributeOverrides();
            if (elementName != null) {
                attributeOverrides.Add(typeof (T), new XmlAttributes {
                    XmlRoot = new XmlRootAttribute {
                        ElementName = elementName
                    }
                });
            }

            var xmlSerializer = new XmlSerializer(typeof (T), attributeOverrides);

            return xmlSerializer.Deserialize(new StringReader(xmlText)) as T;
        }
    }
}