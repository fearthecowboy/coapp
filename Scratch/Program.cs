using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using System.Xml.Serialization;
    using CoApp.Packaging.Common;
    using CoApp.Packaging.Common.Model;
    using CoApp.Toolkit.Collections;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Pipes;

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


            var msg = t.Serialize("\r\n");
            
            Console.WriteLine(msg.ToString());
            Console.WriteLine();
            Console.WriteLine(msg.ToString().UrlDecode());

            var TT = msg.DeserializeTo<Test>();

            var msg2 = t.Serialize("\r\n");

            Console.WriteLine(msg2.ToString().UrlDecode());

            Console.ReadLine();


        }


    }
}
