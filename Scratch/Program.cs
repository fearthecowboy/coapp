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

            
            /*
                        CanonicalName c = new CanonicalName("coapp.toolkit-1.2.0.203-any-1e373a58e25250cb");
                        Console.WriteLine("{0} , {1}",c,c.GeneralName);

                        c = new CanonicalName(CanonicalName.AllPackages);
                        Console.WriteLine("{0} , {1}", c, c.GeneralName);

                        c = new CanonicalName(CanonicalName.CoAppDevtools);
                        Console.WriteLine("{0} , {1}", c, c.GeneralName);

                        c = new CanonicalName(CanonicalName.CoAppItself);
                        Console.WriteLine("{0} , {1}", c, c.GeneralName);

                        c = new CanonicalName(CanonicalName.CoAppPackages);
                        Console.WriteLine("{0} , {1}", c, c.GeneralName);

                        Test t = new Test();
                        t.Name = new CanonicalName("coapp.toolkit-1.2.0.203-any-1e373a58e25250cb");
                        t.somestrings.Add("Hello");
                        t.somestrings.Add("Garrett");

                        t.places.Add("http://slashdot.org".ToUri());
                        t.places.Add("http://coapp.org".ToUri());

                        t.Roles = new XList<Role> {new Role {Name = "RoleName", PackageRole = PackageRole.Application}};


                        t.aDict.Add(new CanonicalName("coapp.devtools-1.2.0.2-any-1e373a58e25250cb"), new XList<Uri> {new Uri("http://coapp.org/current"), new Uri("http://coapp.org/old")});

                        t.shoe = new Shoe {Name = "Garrett", Age = 12, Weight = 15.7};

                        t.shoes = new XList<Shoe> {
                            new Shoe {Name = "Garrett", Age = 2, Weight = 1.7},
                            new Shoe {Name = "Serack", Age = 1, Weight = 5.7},
                        };

                        // XmlSerializer xs = new XmlSerializer(typeof(Test));

                        var output = t.ToXml();

                        Console.WriteLine(output);

                        var t2 = output.FromXml<Test>();
            

                        Console.WriteLine(t2.ToXml());
             * */


            var str =
                @"<coapp:Package xmlns:coapp=""http://coapp.org/atom-package-feed-1.0"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" Vendor=""OUTERCURVE FOUNDATION"" CanonicalName=""coapp:coapp.toolkit-1.2.0.215-any-1e373a58e25250cb"" Name=""coapp.toolkit"" Flavor="""" PackageType=""coapp"" Architecture=""any"" Version=""1.2.0.215"" PublicKeyToken=""1e373a58e25250cb"" DisplayName=""CoApp Package Manager"">
      <coapp:BindingPolicy Minimum=""1.0.0.0"" Maximum=""1.2.0.214"" />
      <coapp:Roles>
        <coapp:Role Name="""" PackageRole=""Application"" />
        <coapp:Role Name=""refasms"" PackageRole=""DeveloperLibrary"" />
        <coapp:Role Name=""CoApp.Toolkit"" PackageRole=""Assembly"" />
        <coapp:Role Name=""CoApp.Client"" PackageRole=""Assembly"" />
      </coapp:Roles>
      <coapp:Details>
        <coapp:AuthorVersion>1.2 PRE-RC</coapp:AuthorVersion>
        <coapp:BugTracker>https://github.com/organizations/coapp/dashboard/issues</coapp:BugTracker>
        <coapp:IconLocations />
        <coapp:Licenses>
          <coapp:License>
            <coapp:LicenseId>Apache20</coapp:LicenseId>
            <coapp:Name>Apache License, 2.0 </coapp:Name>
            <coapp:LicenseUrl>http://opensource.org/licenses/Apache-2.0</coapp:LicenseUrl>
          </coapp:License>
        </coapp:Licenses>
        <coapp:IsNsfw>false</coapp:IsNsfw>
        <coapp:Stability>0</coapp:Stability>
      </coapp:Details>
      <coapp:Feeds>
        <coapp:Uri>http://coapp.org/current</coapp:Uri>
      </coapp:Feeds>
    </coapp:Package>";

    var pm = str.FromXml<PackageModel>();










            Console.ReadLine();


        }


    }
}
