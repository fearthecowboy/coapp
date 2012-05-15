using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using System.Linq.Expressions;
    using System.Xml.Serialization;
    using CoApp.Packaging.Common;
    using CoApp.Packaging.Common.Model;
    using CoApp.Toolkit.Collections;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Linq.Serialization;
    using CoApp.Toolkit.Pipes;

    public class SimpleClass {
        public string Name { get; set; }
        public int Value1 { get; set; }
    }

    [XmlRoot("Shoe", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Shoe {
        [XmlElement("Name", IsNullable = false)]
        public string Name;

        [XmlElement("Age", IsNullable = false)]
        public int Age;

        [XmlElement("Weight", IsNullable = false)]
        public double Weight;
    }
    

    [XmlRoot("Test", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Test {

        [XmlElement(IsNullable = false)]
        public XList<Role> Roles { get; set; }

        [XmlElement(IsNullable = false)]
        public CanonicalName Name;

        [XmlElement("strings", IsNullable = false)]
        public XList<string> somestrings = new XList<string>();

        [XmlElement("places", IsNullable = false)]
        public XList<Uri> places = new XList<Uri>();

        [XmlElement("dict", IsNullable = false)]
        public XDictionary<CanonicalName, XList<Uri>> aDict= new XDictionary<CanonicalName, XList<Uri>>();

        [XmlElement("OneShoe", IsNullable = false)]
        public Shoe shoe;

        [XmlElement("ManyShoe", IsNullable = false)]
        public XList<Shoe> shoes;
    }

    class Program {

        static SimpleClass[] _list = new[] {
            new SimpleClass {Name = "foo"}, new SimpleClass {Name = "not foo", Value1 = 10},
            new SimpleClass {Name = "foo is the beginning"}, new SimpleClass {Name = "lame", Value1= 5},  new SimpleClass {Name = "bar", Value1= 3}, new SimpleClass {Name = "baz", Value1= 8}
        };

        [STAThread]
        static void Main(string[] args) {

            Test t = new Test();
            t.Name = new CanonicalName("coapp.toolkit-1.2.0.203-any-1e373a58e25250cb");
            t.somestrings.Add("Hello");
            t.somestrings.Add("Garrett");

            t.places.Add("http://slashdot.org".ToUri());
            t.places.Add("http://coapp.org".ToUri());

            t.Roles = new XList<Role> { new Role { Name = "RoleName", PackageRole = PackageRole.Application } };


            t.aDict.Add(new CanonicalName("coapp.devtools-1.2.0.2-any-1e373a58e25250cb"), new XList<Uri> { new Uri("http://coapp.org/current"), new Uri("http://coapp.org/old") });

            t.shoe = new Shoe { Name = "Garrett", Age = 12, Weight = 15.7 };

            t.shoes = new XList<Shoe> {
                            new Shoe {Name = "Garrett", Age = 2, Weight = 1.7},
                            new Shoe {Name = "Serack", Age = 1, Weight = 5.7},
                        };

            
            // var x =((SimpleClass s) => s.Name)
            var x = Extn<Test>.Create2(s => s.Name);

            var simple = t;

            var answer = x.Compile()(simple);



            Console.WriteLine("Answer: {0}", answer);

            var ser = x.Serialize<Expression>("\r\n");

            // var ser = xs.Serialize(x).ToString();
            Console.WriteLine(ser.ToString().UrlDecode() );

#if TESTING
            var encoded = ser.ToString();
            var unencoded = encoded.UrlDecode();
            var base64 = Convert.ToBase64String(unencoded.ToByteArray());
            var gzip = HttpUtility.UrlEncode(unencoded.Gzip());
            var gzipBase64 = Convert.ToBase64String(unencoded.Gzip());
            var urlEncodedGzipBase64 = gzipBase64.UrlEncode();

            Console.WriteLine("encoded {0} => {1}", encoded.Length, encoded);
            Console.WriteLine("unencoded {0} => {1}", unencoded.Length, unencoded);
            Console.WriteLine("base64 {0} => {1}", base64.Length, base64);
            Console.WriteLine("gzip {0} => {1}", gzip.Length, gzip);
            Console.WriteLine("gzipBase64 {0} => {1}", gzipBase64.Length, gzipBase64);
            Console.WriteLine("urlEncodedGzipBase64 {0} => {1}", urlEncodedGzipBase64.Length, urlEncodedGzipBase64);
#endif 

            // var x2 = xs.Deserialize<Func<Test,CanonicalName>>(XElement.Parse(ser));
            var x2 = (ser.DeserializeTo<Expression>() as Expression<Func<Test, CanonicalName>>);

            var answer2 = x2.Compile()(simple);

            Console.WriteLine("Answer2: {0}", answer2);

            /*
            var msg = t.Serialize("\r\n");
            
            Console.WriteLine(msg.ToString());
            Console.WriteLine();
            Console.WriteLine(msg.ToString().UrlDecode());

            var TT = msg.DeserializeTo<Test>();

            var msg2 = t.Serialize("\r\n");

            Console.WriteLine(msg2.ToString().UrlDecode());
            */
            Console.ReadLine();


        }


    }
}
#if FALSE

using System;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CoApp.Toolkit.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject1
{
    using CoApp.Toolkit.Query;

    [TestClass]
    public class QueryTesting
    {
        SimpleClass[] _list = new[]
                           {
                               new SimpleClass {Name = "foo"}, new SimpleClass {Name = "not foo", Value1 = 10},
                               new SimpleClass {Name = "foo is the beginning"}, new SimpleClass {Name = "lame", Value1= 5},  new SimpleClass {Name = "bar", Value1= 3}, new SimpleClass {Name = "baz", Value1= 8}
                           };
        
        [TestMethod]
        public void BasicEqualForStrings()
        {            

           
            var nameRef = PropRef<SimpleClass>.Create(s => s.Name);
            var filter = Filter.Create(nameRef, FilterOp.EQ, "foo");

            var query = Query.Create(filter, nameRef);
            var result = query.Invoke(_list).ToArray();
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual("foo", result[0].Name);

        }

        [TestMethod]
        public void WildcardEqualForStrings()
        {

           
            var nameRef = PropRef<SimpleClass>.Create(s => s.Name);
            var filter = Filter.Create(nameRef, FilterOp.EQ, "foo*");

            var query = Query.Create(filter, PropRef<SimpleClass>.Create(s => s.Name));
            var result = query.Invoke(_list).ToArray();
            Assert.AreEqual(2, result.Count());
            Assert.AreEqual("foo", result[0].Name);
            Assert.AreEqual("foo is the beginning", result[1].Name);
        }

        [TestMethod]
        public void StringContainsFoo()
        {
            
            var nameRef = PropRef<SimpleClass>.Create(s => s.Name);
            var filter = Filter.Create(nameRef, FilterOp.Contains, "foo");
            var query = Query.Create(filter, nameRef);
            var result = query.Invoke(_list).ToArray();
            Assert.AreEqual(3, result.Count());
            Assert.AreEqual("foo", result[0].Name);
            Assert.AreEqual("foo is the beginning", result[1].Name);
            Assert.AreEqual("not foo", result[2].Name);
        }

        [TestMethod]
        public void StringDoesntContainsFoo()
        {
            
            var nameRef = PropRef<SimpleClass>.Create(s => s.Name);
            var filter = Filter.Create(nameRef, FilterOp.Contains, "foo");
            var notFilter = !filter;
            var query = Query.Create(notFilter, nameRef);
            var result = query.Invoke(_list).ToArray();
            Assert.AreEqual(3, result.Count());
            Assert.AreEqual("bar", result[0].Name);
            Assert.AreEqual("baz", result[1].Name);
            Assert.AreEqual("lame", result[2].Name);
        }

        [TestMethod]
        public void ValueComparisonOps()
        {
         
            
            var notContainingFoo = !Filter.Create(PropRef<SimpleClass>.Create(s => s.Name), FilterOp.Contains, "foo");

            var gt4 = Filter.Create(PropRef<SimpleClass>.Create(s => s.Value1), FilterOp.GT, 4);

            var andFilter = notContainingFoo & gt4;

            var query = Query.Create(andFilter, PropRef<SimpleClass>.Create(s => s.Name), ListSortDirection.Descending);

            var result = query.Invoke(_list).ToArray();

            Assert.AreEqual(2, result.Count());

            Assert.AreEqual("lame", result[0].Name);
            Assert.AreEqual("baz", result[1].Name);

            

        }

        [TestMethod]
        public void ValueComparisonOr()
        {
            var name = PropRef<SimpleClass>.Create(s => s.Name);
            var value1 = PropRef<SimpleClass>.Create(s => s.Value1);

            var notContainingFoo = Filter.Create(Filter.Create(name, FilterOp.Contains, "foo"), UnaryFilterOperator.Not);

            var gt4 = Filter.Create(value1, FilterOp.GT, 4);

            var andFilter = notContainingFoo | gt4;

            var query = Query.Create(andFilter, name, ListSortDirection.Descending);

            var result = query.Invoke(_list).ToArray();

            Assert.AreEqual(4, result.Count());
             Assert.AreEqual("not foo", result[0].Name);
            Assert.AreEqual("lame", result[1].Name);
            Assert.AreEqual("baz", result[2].Name);
            Assert.AreEqual("bar", result[3].Name);

        }


        [TestMethod]
        public void EmbeddingTest()
        {
            var name = PropRef<SimpleClass>.Create(s => s.Name);
            var value1 = PropRef<SimpleClass>.Create(s => s.Value1);
            var notContainingFoo = Filter.Create(Filter.Create(name, FilterOp.Contains, "foo"), UnaryFilterOperator.Not);
            var eq3 = Filter.Create(value1, FilterOp.EQ, 3);
            var gte8 = Filter.Create(value1, FilterOp.GTE, 8);

            var query = Query.Create((notContainingFoo & eq3) | gte8);
            var result = query.Invoke(_list).ToArray();

            Assert.AreEqual(3, result.Count());
            
            Assert.AreEqual("not foo", result[0].Name);
            Assert.AreEqual("bar", result[1].Name);
            Assert.AreEqual("baz", result[2].Name);

        }

    }


    public class SimpleClass
    {
        public string Name { get; set; }
        public int Value1 { get; set; }
    }

    public class BadClass
    {
        
    }
}

#endif 